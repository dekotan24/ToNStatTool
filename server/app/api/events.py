"""イベント受信API（クライアントアプリからのデータ受信）"""
import secrets
from datetime import datetime, timezone
from typing import List, Optional

from fastapi import APIRouter, Depends, HTTPException, status, Header
from pydantic import BaseModel
from sqlalchemy import select, update
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.database import get_db
from app.core.security import generate_fingerprint, hash_token
from app.models import Instance, Round, TerrorStats, RoundTypeStats, MapStats, APIKey, Player, PlayerRound, ItemStats

router = APIRouter(prefix="/api/v1/events", tags=["events"])


# ========== API Key Authentication ==========

async def verify_api_key(
    x_api_key: Optional[str] = Header(None, alias="X-API-Key"),
    db: AsyncSession = Depends(get_db)
) -> APIKey:
    """APIキーを検証"""
    if not x_api_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="APIキーが必要です",
            headers={"WWW-Authenticate": "API-Key"}
        )

    # プレフィックスチェック
    if not x_api_key.startswith("ton_"):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="無効なAPIキー形式です"
        )

    # ハッシュ化してDB検索
    key_hash = hash_token(x_api_key)
    result = await db.execute(
        select(APIKey).where(
            APIKey.key_hash == key_hash,
            APIKey.is_active == True
        )
    )
    api_key = result.scalar_one_or_none()

    if not api_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="無効または失効したAPIキーです"
        )

    # 最終使用日時を更新
    api_key.last_used_at = datetime.now(timezone.utc)

    return api_key


# ========== Pydantic Models ==========

class RoundInfo(BaseModel):
    type: str
    mapName: Optional[str] = None
    terrors: List[str] = []


class InstanceInfo(BaseModel):
    playerCount: int = 0
    survivorCount: int = 0


class PlayerInfo(BaseModel):
    """ToNStatToolから送信されるプレイヤー情報"""
    vrchatName: str  # VRChat表示名
    vrchatId: Optional[str] = None  # VRChat GUID (usr_xxx) - 推奨
    survived: bool  # このラウンドで生存したか
    items: List[str] = []  # 所持アイテムリスト
    notes: Optional[str] = None  # メモ（任意）


class RoundEndEvent(BaseModel):
    eventType: str = "roundEnd"
    instanceId: str
    timestamp: datetime
    round: RoundInfo
    instance: InstanceInfo
    player: Optional[PlayerInfo] = None  # プレイヤー情報（オプション、後方互換性）


class InstanceUpdateEvent(BaseModel):
    eventType: str = "instanceUpdate"
    instanceId: str
    timestamp: datetime
    state: dict


# ========== Endpoints ==========

@router.post("")
async def receive_event(
    event: dict,
    api_key: APIKey = Depends(verify_api_key),
    db: AsyncSession = Depends(get_db)
):
    """イベントを受信して処理（APIキー認証必須）"""
    event_type = event.get("eventType")

    if event_type == "roundEnd":
        return await handle_round_end(RoundEndEvent(**event), api_key, db)
    elif event_type == "instanceUpdate":
        return await handle_instance_update(InstanceUpdateEvent(**event), db)
    else:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unknown event type: {event_type}"
        )


async def handle_round_end(event: RoundEndEvent, api_key: APIKey, db: AsyncSession):
    """ラウンド終了イベントを処理"""
    # インスタンスIDが空の場合はスキップ
    if not event.instanceId:
        return {"status": "skipped", "reason": "empty instance_id"}

    # インスタンスを取得または作成
    result = await db.execute(
        select(Instance).where(Instance.instance_id == event.instanceId)
    )
    instance = result.scalar_one_or_none()

    if not instance:
        # ワールドIDを抽出（wrld_xxx~... から wrld_xxx を取得）
        world_id = event.instanceId.split("~")[0] if "~" in event.instanceId else None

        instance = Instance(
            instance_id=event.instanceId,
            world_id=world_id,
            total_rounds=0
        )
        db.add(instance)
        await db.flush()

    # フィンガープリントを生成
    fingerprint = generate_fingerprint(
        event.instanceId,
        event.round.type,
        event.round.terrors,
        event.timestamp
    )

    # 重複チェック
    result = await db.execute(
        select(Round).where(Round.fingerprint == fingerprint)
    )
    existing_round = result.scalar_one_or_none()

    round_id = None
    is_new_round = False

    if existing_round:
        # 既存のラウンドがあれば生存者数を更新（より正確な値として）
        if event.instance.survivorCount > existing_round.survivor_count:
            existing_round.survivor_count = event.instance.survivorCount
        round_id = existing_round.id
    else:
        # 新しいラウンドを作成
        new_round = Round(
            instance_id=instance.id,
            fingerprint=fingerprint,
            round_type=event.round.type,
            map_name=event.round.mapName,
            terrors=event.round.terrors,
            started_at=event.timestamp,
            player_count=event.instance.playerCount,
            survivor_count=event.instance.survivorCount
        )
        db.add(new_round)
        await db.flush()
        round_id = new_round.id
        is_new_round = True

        # インスタンスの統計を更新
        instance.total_rounds += 1
        instance.last_activity_at = datetime.now(timezone.utc)

        # ラウンドタイプ統計、マップ統計、テラー統計はPlayerRound作成時に更新

    # プレイヤー情報を処理
    player_id = None
    if event.player and event.player.vrchatName:
        player_id = await process_player_data(
            db,
            api_key,
            event.player,
            round_id,
            event.round.type,
            event.round.mapName,
            event.round.terrors
        )

    await db.flush()

    return {
        "status": "created" if is_new_round else "duplicate",
        "round_id": round_id,
        "player_id": player_id
    }


async def process_player_data(
    db: AsyncSession,
    api_key: APIKey,
    player_info: PlayerInfo,
    round_id: int,
    round_type: str,
    map_name: Optional[str],
    terrors: List[str]
) -> int:
    """プレイヤーデータを処理"""
    player = None

    # VRChat GUIDが提供されている場合は優先的に検索
    if player_info.vrchatId:
        result = await db.execute(
            select(Player).where(Player.vrchat_id == player_info.vrchatId)
        )
        player = result.scalar_one_or_none()

        if player:
            # GUIDで見つかった場合、表示名が変わっていたら更新
            if player.vrchat_name != player_info.vrchatName:
                player.vrchat_name = player_info.vrchatName

    # GUIDで見つからない場合は名前で検索（後方互換性）
    if not player:
        result = await db.execute(
            select(Player).where(Player.vrchat_name == player_info.vrchatName)
        )
        player = result.scalar_one_or_none()

        # 名前で見つかった場合、GUIDを更新（GUIDが提供されていれば）
        if player and player_info.vrchatId and not player.vrchat_id:
            player.vrchat_id = player_info.vrchatId

    if not player:
        player = Player(
            vrchat_id=player_info.vrchatId,
            vrchat_name=player_info.vrchatName,
            api_key_id=api_key.id,
            user_id=api_key.user_id,
            avatar_seed=secrets.token_hex(16),
            total_rounds=0,
            total_survivals=0
        )
        db.add(player)
        await db.flush()
    else:
        # 既存のプレイヤーの場合、API keyが未設定なら更新
        if not player.api_key_id:
            player.api_key_id = api_key.id
        if not player.user_id and api_key.user_id:
            player.user_id = api_key.user_id

    # このラウンドへの参加記録が既に存在するかチェック
    result = await db.execute(
        select(PlayerRound).where(
            PlayerRound.player_id == player.id,
            PlayerRound.round_id == round_id
        )
    )
    existing_player_round = result.scalar_one_or_none()

    if not existing_player_round:
        # 新しいプレイヤーラウンド記録を作成
        player_round = PlayerRound(
            player_id=player.id,
            round_id=round_id,
            survived=player_info.survived,
            items=player_info.items if player_info.items else None,
            notes=player_info.notes
        )
        db.add(player_round)

        # プレイヤー統計を更新
        player.total_rounds += 1
        if player_info.survived:
            player.total_survivals += 1

        # アイテム統計を更新
        if player_info.items:
            for item_name in player_info.items:
                await update_item_stats(db, item_name, player_info.survived)

        # ラウンドタイプ統計を更新（プレイヤー参加ベース）
        await update_round_type_stats(db, round_type, player_info.survived)

        # マップ統計を更新（プレイヤー参加ベース）
        if map_name:
            await update_map_stats(db, map_name, player_info.survived)

        # テラー統計を更新（プレイヤー参加ベース）
        for terror_name in terrors:
            await update_terror_stats(db, terror_name, player_info.survived)

    return player.id


async def update_item_stats(db: AsyncSession, item_name: str, survived: bool):
    """アイテム統計を更新"""
    stmt = insert(ItemStats).values(
        item_name=item_name,
        total_held=1,
        total_survivals=1 if survived else 0
    ).on_conflict_do_update(
        index_elements=["item_name"],
        set_={
            "total_held": ItemStats.total_held + 1,
            "total_survivals": ItemStats.total_survivals + (1 if survived else 0)
        }
    )
    await db.execute(stmt)


async def handle_instance_update(event: InstanceUpdateEvent, db: AsyncSession):
    """インスタンス状態更新イベントを処理"""
    # 現在は特に処理しない（将来の拡張用）
    return {"status": "ok"}


async def update_round_type_stats(
    db: AsyncSession,
    round_type: str,
    survived: bool
):
    """ラウンドタイプ統計を更新（プレイヤー参加ベース）"""
    stmt = insert(RoundTypeStats).values(
        round_type=round_type,
        occurrence_count=1,  # 参加回数
        total_players=1,     # 参加回数（後方互換性のため残す）
        total_survivors=1 if survived else 0
    ).on_conflict_do_update(
        index_elements=["round_type"],
        set_={
            "occurrence_count": RoundTypeStats.occurrence_count + 1,
            "total_players": RoundTypeStats.total_players + 1,
            "total_survivors": RoundTypeStats.total_survivors + (1 if survived else 0)
        }
    )
    await db.execute(stmt)


async def update_map_stats(db: AsyncSession, map_name: str, survived: bool):
    """マップ統計を更新（プレイヤー参加ベース）"""
    stmt = insert(MapStats).values(
        map_name=map_name,
        occurrence_count=1,  # 参加回数
        total_players=1,     # 参加回数（後方互換性のため残す）
        total_survivors=1 if survived else 0
    ).on_conflict_do_update(
        index_elements=["map_name"],
        set_={
            "occurrence_count": MapStats.occurrence_count + 1,
            "total_players": MapStats.total_players + 1,
            "total_survivors": MapStats.total_survivors + (1 if survived else 0)
        }
    )
    await db.execute(stmt)


async def update_terror_stats(
    db: AsyncSession,
    terror_name: str,
    survived: bool
):
    """テラー統計を更新（プレイヤー参加ベース）"""
    stmt = insert(TerrorStats).values(
        terror_name=terror_name,
        encounter_count=1,  # 遭遇回数（参加回数）
        total_rounds=1,     # 遭遇回数（後方互換性のため残す）
        total_survivors=1 if survived else 0
    ).on_conflict_do_update(
        index_elements=["terror_name"],
        set_={
            "encounter_count": TerrorStats.encounter_count + 1,
            "total_rounds": TerrorStats.total_rounds + 1,
            "total_survivors": TerrorStats.total_survivors + (1 if survived else 0)
        }
    )
    await db.execute(stmt)
