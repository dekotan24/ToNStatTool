"""イベント受信API（クライアントアプリからのデータ受信）"""
from datetime import datetime, timezone
from typing import List, Optional

from fastapi import APIRouter, Depends, HTTPException, status, Header
from pydantic import BaseModel
from sqlalchemy import select, update
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.database import get_db
from app.core.security import generate_fingerprint, hash_token
from app.models import Instance, Round, TerrorStats, RoundTypeStats, MapStats, APIKey

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

    # 使用回数と最終使用日時を更新
    api_key.use_count += 1
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


class RoundEndEvent(BaseModel):
    eventType: str = "roundEnd"
    instanceId: str
    timestamp: datetime
    round: RoundInfo
    instance: InstanceInfo


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
        return await handle_round_end(RoundEndEvent(**event), db)
    elif event_type == "instanceUpdate":
        return await handle_instance_update(InstanceUpdateEvent(**event), db)
    else:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Unknown event type: {event_type}"
        )


async def handle_round_end(event: RoundEndEvent, db: AsyncSession):
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

    if existing_round:
        # 既存のラウンドがあれば生存者数を更新（より正確な値として）
        if event.instance.survivorCount > existing_round.survivor_count:
            existing_round.survivor_count = event.instance.survivorCount
        return {"status": "duplicate", "round_id": existing_round.id}

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

    # インスタンスの統計を更新
    instance.total_rounds += 1
    instance.last_activity_at = datetime.now(timezone.utc)

    # ラウンドタイプ統計を更新
    await update_round_type_stats(
        db,
        event.round.type,
        event.instance.playerCount,
        event.instance.survivorCount
    )

    # マップ統計を更新
    if event.round.mapName:
        await update_map_stats(db, event.round.mapName)

    # テラー統計を更新
    for terror_name in event.round.terrors:
        await update_terror_stats(
            db,
            terror_name,
            event.instance.survivorCount
        )

    await db.flush()

    return {"status": "created", "round_id": new_round.id}


async def handle_instance_update(event: InstanceUpdateEvent, db: AsyncSession):
    """インスタンス状態更新イベントを処理"""
    # 現在は特に処理しない（将来の拡張用）
    return {"status": "ok"}


async def update_round_type_stats(
    db: AsyncSession,
    round_type: str,
    player_count: int,
    survivor_count: int
):
    """ラウンドタイプ統計を更新"""
    stmt = insert(RoundTypeStats).values(
        round_type=round_type,
        occurrence_count=1,
        total_players=player_count,
        total_survivors=survivor_count
    ).on_conflict_do_update(
        index_elements=["round_type"],
        set_={
            "occurrence_count": RoundTypeStats.occurrence_count + 1,
            "total_players": RoundTypeStats.total_players + player_count,
            "total_survivors": RoundTypeStats.total_survivors + survivor_count
        }
    )
    await db.execute(stmt)


async def update_map_stats(db: AsyncSession, map_name: str):
    """マップ統計を更新"""
    stmt = insert(MapStats).values(
        map_name=map_name,
        occurrence_count=1
    ).on_conflict_do_update(
        index_elements=["map_name"],
        set_={"occurrence_count": MapStats.occurrence_count + 1}
    )
    await db.execute(stmt)


async def update_terror_stats(
    db: AsyncSession,
    terror_name: str,
    survivor_count: int
):
    """テラー統計を更新"""
    stmt = insert(TerrorStats).values(
        terror_name=terror_name,
        encounter_count=1,
        total_rounds=1,
        total_survivors=survivor_count
    ).on_conflict_do_update(
        index_elements=["terror_name"],
        set_={
            "encounter_count": TerrorStats.encounter_count + 1,
            "total_rounds": TerrorStats.total_rounds + 1,
            "total_survivors": TerrorStats.total_survivors + survivor_count
        }
    )
    await db.execute(stmt)
