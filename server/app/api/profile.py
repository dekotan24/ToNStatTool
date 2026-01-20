"""プロフィールAPI"""
from datetime import datetime, timezone
from typing import List, Optional

from fastapi import APIRouter, Depends, HTTPException, Query, status
from pydantic import BaseModel
from sqlalchemy import select, func, desc, case
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy.orm import selectinload

from app.core.database import get_db
from app.api.auth import get_current_user, get_optional_user
from app.models import User, Player, PlayerRound, Round, ItemStats

router = APIRouter(prefix="/api/v1/profile", tags=["profile"])


# ========== Pydantic Models ==========

class PlayerProfile(BaseModel):
    id: int
    username: str  # Webアカウントのユーザー名（URL用）
    vrchat_name: str  # VRChatのプレイヤー名（表示用）
    avatar_seed: Optional[str]
    bio: Optional[str]
    is_public: bool
    total_rounds: int
    total_survivals: int
    survival_rate: float
    created_at: datetime
    is_own_profile: bool = False


class PlayerRoundHistory(BaseModel):
    id: int
    round_type: str
    map_name: Optional[str]
    terrors: List[str]
    survived: bool
    items: List[str]
    player_count: int
    survivor_count: int
    started_at: datetime
    notes: Optional[str] = None


class PlayerSearchResult(BaseModel):
    id: int
    username: str  # Webアカウントのユーザー名（URL用）
    vrchat_name: str  # VRChatのプレイヤー名（表示用）
    avatar_seed: Optional[str]
    total_rounds: int
    survival_rate: float


class ItemSurvivalStat(BaseModel):
    item_name: str
    times_held: int
    times_survived: int
    survival_rate: float
    hold_rate: Optional[float] = None  # 所持率（グローバル統計用）


class TerrorSurvivalStat(BaseModel):
    terror_name: str
    encounters: int
    survivals: int
    survival_rate: float


class RoundTypeSurvivalStat(BaseModel):
    round_type: str
    rounds_played: int
    survivals: int
    survival_rate: float


class DetailedStats(BaseModel):
    item_stats: List[ItemSurvivalStat]
    terror_stats: List[TerrorSurvivalStat]
    round_type_stats: List[RoundTypeSurvivalStat]


# ========== Endpoints ==========

@router.get("/search", response_model=List[PlayerSearchResult])
async def search_players(
    q: str = Query(..., min_length=1, description="Search query"),
    db: AsyncSession = Depends(get_db),
    limit: int = Query(default=20, le=50)
):
    """プレイヤーを検索（公開プロフィールのみ、Webアカウント紐付け必須）"""
    result = await db.execute(
        select(Player, User)
        .join(User, Player.user_id == User.id)
        .where(
            Player.is_public == True,
            Player.user_id.isnot(None),
            Player.vrchat_name.ilike(f"%{q}%")
        )
        .order_by(desc(Player.total_rounds))
        .limit(limit)
    )
    rows = result.all()

    return [
        PlayerSearchResult(
            id=p.id,
            username=u.username,
            vrchat_name=p.vrchat_name,
            avatar_seed=p.avatar_seed,
            total_rounds=p.total_rounds,
            survival_rate=round(p.total_survivals / p.total_rounds * 100, 2) if p.total_rounds > 0 else 0
        )
        for p, u in rows
    ]


@router.get("/me", response_model=PlayerProfile)
async def get_my_profile(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """自分のプロフィールを取得"""
    result = await db.execute(
        select(Player).where(Player.user_id == user.id)
    )
    player = result.scalar_one_or_none()

    if not player:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Player profile not found"
        )

    return PlayerProfile(
        id=player.id,
        username=user.username,
        vrchat_name=player.vrchat_name,
        avatar_seed=player.avatar_seed,
        bio=player.bio,
        is_public=player.is_public,
        total_rounds=player.total_rounds,
        total_survivals=player.total_survivals,
        survival_rate=round(player.total_survivals / player.total_rounds * 100, 2) if player.total_rounds > 0 else 0,
        created_at=player.created_at,
        is_own_profile=True
    )


@router.get("/player/{username}", response_model=PlayerProfile)
async def get_player_profile(
    username: str,
    user: Optional[User] = Depends(get_optional_user),
    db: AsyncSession = Depends(get_db)
):
    """プレイヤープロフィールを取得（Webアカウントのユーザー名で検索）"""
    # Webアカウントを検索
    user_result = await db.execute(
        select(User).where(User.username == username)
    )
    target_user = user_result.scalar_one_or_none()

    if not target_user:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )

    # 紐づいたプレイヤーを取得
    result = await db.execute(
        select(Player).where(Player.user_id == target_user.id)
    )
    player = result.scalar_one_or_none()

    if not player:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Player profile not found"
        )

    # 自分のプロフィールかチェック
    is_own = user and player.user_id == user.id

    # 非公開で他人のプロフィールの場合は拒否
    if not player.is_public and not is_own:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="This profile is private"
        )

    return PlayerProfile(
        id=player.id,
        username=target_user.username,
        vrchat_name=player.vrchat_name,
        avatar_seed=player.avatar_seed,
        bio=player.bio,
        is_public=player.is_public,
        total_rounds=player.total_rounds,
        total_survivals=player.total_survivals,
        survival_rate=round(player.total_survivals / player.total_rounds * 100, 2) if player.total_rounds > 0 else 0,
        created_at=player.created_at,
        is_own_profile=is_own
    )


@router.get("/player/{username}/history", response_model=List[PlayerRoundHistory])
async def get_player_history(
    username: str,
    user: Optional[User] = Depends(get_optional_user),
    db: AsyncSession = Depends(get_db),
    limit: int = Query(default=50, le=200),
    offset: int = Query(default=0, ge=0)
):
    """プレイヤーのラウンド履歴を取得"""
    # Webアカウントを検索
    user_result = await db.execute(
        select(User).where(User.username == username)
    )
    target_user = user_result.scalar_one_or_none()

    if not target_user:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )

    # 紐づいたプレイヤーを取得
    result = await db.execute(
        select(Player).where(Player.user_id == target_user.id)
    )
    player = result.scalar_one_or_none()

    if not player:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Player profile not found"
        )

    # 自分のプロフィールかチェック
    is_own = user and player.user_id == user.id

    # 非公開で他人のプロフィールの場合は拒否
    if not player.is_public and not is_own:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="This profile is private"
        )

    # プレイヤーラウンドを取得
    result = await db.execute(
        select(PlayerRound, Round)
        .join(Round, PlayerRound.round_id == Round.id)
        .where(PlayerRound.player_id == player.id)
        .order_by(desc(Round.started_at))
        .limit(limit)
        .offset(offset)
    )
    rows = result.all()

    return [
        PlayerRoundHistory(
            id=pr.id,
            round_type=r.round_type,
            map_name=r.map_name,
            terrors=r.terrors or [],
            survived=pr.survived,
            items=pr.items or [],
            player_count=r.player_count,
            survivor_count=r.survivor_count,
            started_at=r.started_at,
            notes=pr.notes if is_own else None  # メモは本人のみ
        )
        for pr, r in rows
    ]


@router.get("/player/{username}/stats", response_model=DetailedStats)
async def get_player_detailed_stats(
    username: str,
    user: Optional[User] = Depends(get_optional_user),
    db: AsyncSession = Depends(get_db)
):
    """プレイヤーの詳細統計を取得"""
    # Webアカウントを検索
    user_result = await db.execute(
        select(User).where(User.username == username)
    )
    target_user = user_result.scalar_one_or_none()

    if not target_user:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="User not found"
        )

    # 紐づいたプレイヤーを取得
    result = await db.execute(
        select(Player).where(Player.user_id == target_user.id)
    )
    player = result.scalar_one_or_none()

    if not player:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Player profile not found"
        )

    # 自分のプロフィールかチェック
    is_own = user and player.user_id == user.id

    # 非公開で他人のプロフィールの場合は拒否
    if not player.is_public and not is_own:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="This profile is private"
        )

    # アイテム別統計
    item_stats = await get_player_item_stats(db, player.id)

    # テラー別統計
    terror_stats = await get_player_terror_stats(db, player.id)

    # ラウンドタイプ別統計
    round_type_stats = await get_player_round_type_stats(db, player.id)

    return DetailedStats(
        item_stats=item_stats,
        terror_stats=terror_stats,
        round_type_stats=round_type_stats
    )


async def get_player_item_stats(db: AsyncSession, player_id: int) -> List[ItemSurvivalStat]:
    """プレイヤーのアイテム別統計を取得"""
    # プレイヤーのすべてのラウンドを取得
    result = await db.execute(
        select(PlayerRound)
        .where(PlayerRound.player_id == player_id)
    )
    player_rounds = result.scalars().all()

    # アイテム統計を集計
    item_data = {}
    for pr in player_rounds:
        if pr.items:
            for item in pr.items:
                if item not in item_data:
                    item_data[item] = {"held": 0, "survived": 0}
                item_data[item]["held"] += 1
                if pr.survived:
                    item_data[item]["survived"] += 1

    return [
        ItemSurvivalStat(
            item_name=item,
            times_held=data["held"],
            times_survived=data["survived"],
            survival_rate=round(data["survived"] / data["held"] * 100, 2) if data["held"] > 0 else 0
        )
        for item, data in sorted(item_data.items(), key=lambda x: x[1]["held"], reverse=True)
    ]


async def get_player_terror_stats(db: AsyncSession, player_id: int) -> List[TerrorSurvivalStat]:
    """プレイヤーのテラー別統計を取得"""
    # プレイヤーのすべてのラウンドとラウンド情報を取得
    result = await db.execute(
        select(PlayerRound, Round)
        .join(Round, PlayerRound.round_id == Round.id)
        .where(PlayerRound.player_id == player_id)
    )
    rows = result.all()

    # テラー統計を集計
    terror_data = {}
    for pr, r in rows:
        if r.terrors:
            for terror in r.terrors:
                if terror not in terror_data:
                    terror_data[terror] = {"encounters": 0, "survived": 0}
                terror_data[terror]["encounters"] += 1
                if pr.survived:
                    terror_data[terror]["survived"] += 1

    return [
        TerrorSurvivalStat(
            terror_name=terror,
            encounters=data["encounters"],
            survivals=data["survived"],
            survival_rate=round(data["survived"] / data["encounters"] * 100, 2) if data["encounters"] > 0 else 0
        )
        for terror, data in sorted(terror_data.items(), key=lambda x: x[1]["encounters"], reverse=True)
    ]


async def get_player_round_type_stats(db: AsyncSession, player_id: int) -> List[RoundTypeSurvivalStat]:
    """プレイヤーのラウンドタイプ別統計を取得"""
    # プレイヤーのすべてのラウンドとラウンド情報を取得
    result = await db.execute(
        select(PlayerRound, Round)
        .join(Round, PlayerRound.round_id == Round.id)
        .where(PlayerRound.player_id == player_id)
    )
    rows = result.all()

    # ラウンドタイプ統計を集計
    type_data = {}
    for pr, r in rows:
        if r.round_type not in type_data:
            type_data[r.round_type] = {"played": 0, "survived": 0}
        type_data[r.round_type]["played"] += 1
        if pr.survived:
            type_data[r.round_type]["survived"] += 1

    return [
        RoundTypeSurvivalStat(
            round_type=rt,
            rounds_played=data["played"],
            survivals=data["survived"],
            survival_rate=round(data["survived"] / data["played"] * 100, 2) if data["played"] > 0 else 0
        )
        for rt, data in sorted(type_data.items(), key=lambda x: x[1]["played"], reverse=True)
    ]


class ProfileUpdateRequest(BaseModel):
    is_public: Optional[bool] = None
    bio: Optional[str] = None


@router.patch("/me")
async def update_my_profile(
    data: ProfileUpdateRequest,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """プロフィール設定を更新"""
    result = await db.execute(
        select(Player).where(Player.user_id == user.id)
    )
    player = result.scalar_one_or_none()

    if not player:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Player profile not found. Play some rounds first!"
        )

    if data.is_public is not None:
        player.is_public = data.is_public
    if data.bio is not None:
        player.bio = data.bio[:500] if data.bio else None  # 500文字制限

    await db.commit()

    return {"status": "updated"}


@router.patch("/me/settings")
async def update_profile_settings(
    is_public: Optional[bool] = None,
    bio: Optional[str] = None,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """プロフィール設定を更新（レガシー）"""
    result = await db.execute(
        select(Player).where(Player.user_id == user.id)
    )
    player = result.scalar_one_or_none()

    if not player:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Player profile not found. Play some rounds first!"
        )

    if is_public is not None:
        player.is_public = is_public
    if bio is not None:
        player.bio = bio[:500] if bio else None  # 500文字制限

    await db.commit()

    return {"status": "updated"}


@router.get("/items", response_model=List[ItemSurvivalStat])
async def get_global_item_stats(
    db: AsyncSession = Depends(get_db),
    limit: int = Query(default=50, le=200)
):
    """グローバルアイテム統計を取得"""
    # 全PlayerRound数を取得（所持率計算用）
    total_result = await db.execute(select(func.count(PlayerRound.id)))
    total_player_rounds = total_result.scalar() or 0

    result = await db.execute(
        select(ItemStats)
        .order_by(desc(ItemStats.total_held))
        .limit(limit)
    )
    items = result.scalars().all()

    return [
        ItemSurvivalStat(
            item_name=item.item_name,
            times_held=item.total_held,
            times_survived=item.total_survivals,
            survival_rate=round(item.total_survivals / item.total_held * 100, 2) if item.total_held > 0 else 0,
            hold_rate=round(item.total_held / total_player_rounds * 100, 2) if total_player_rounds > 0 else 0
        )
        for item in items
    ]
