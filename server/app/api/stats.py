"""統計API"""
from datetime import datetime, timedelta, timezone
from typing import List, Optional

from fastapi import APIRouter, Depends, Query
from pydantic import BaseModel
from sqlalchemy import select, func, desc
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.database import get_db
from app.api.auth import get_current_user
from app.models import User, Instance, Round, TerrorStats, RoundTypeStats, MapStats

router = APIRouter(prefix="/api/v1/stats", tags=["stats"])


# ========== Pydantic Models ==========

class OverviewStats(BaseModel):
    total_rounds: int
    total_instances: int
    total_terrors_encountered: int
    average_survival_rate: float
    last_24h_rounds: int


class TerrorStatItem(BaseModel):
    name: str
    encounter_count: int
    survival_rate: float


class RoundTypeStatItem(BaseModel):
    round_type: str
    occurrence_count: int
    survival_rate: float
    percentage: float


class MapStatItem(BaseModel):
    map_name: str
    occurrence_count: int
    percentage: float


class RecentRound(BaseModel):
    id: int
    round_type: str
    map_name: Optional[str]
    terrors: List[str]
    player_count: int
    survivor_count: int
    started_at: datetime


class ActiveInstance(BaseModel):
    id: int
    instance_id: str
    total_rounds: int
    last_activity_at: datetime


# ========== Endpoints ==========

@router.get("/overview", response_model=OverviewStats)
async def get_overview(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """全体統計を取得"""
    # 総ラウンド数
    result = await db.execute(select(func.count(Round.id)))
    total_rounds = result.scalar() or 0

    # 総インスタンス数
    result = await db.execute(select(func.count(Instance.id)))
    total_instances = result.scalar() or 0

    # ユニークなテラー数
    result = await db.execute(select(func.count(TerrorStats.id)))
    total_terrors = result.scalar() or 0

    # 平均生存率
    result = await db.execute(
        select(
            func.sum(Round.survivor_count),
            func.sum(Round.player_count)
        )
    )
    row = result.one()
    total_survivors = row[0] or 0
    total_players = row[1] or 0
    average_survival_rate = (total_survivors / total_players * 100) if total_players > 0 else 0

    # 過去24時間のラウンド数
    yesterday = datetime.now(timezone.utc) - timedelta(days=1)
    result = await db.execute(
        select(func.count(Round.id)).where(Round.started_at >= yesterday)
    )
    last_24h_rounds = result.scalar() or 0

    return OverviewStats(
        total_rounds=total_rounds,
        total_instances=total_instances,
        total_terrors_encountered=total_terrors,
        average_survival_rate=round(average_survival_rate, 2),
        last_24h_rounds=last_24h_rounds
    )


@router.get("/terrors", response_model=List[TerrorStatItem])
async def get_terror_stats(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
    limit: int = Query(default=50, le=200),
    offset: int = Query(default=0, ge=0)
):
    """テラー統計を取得"""
    result = await db.execute(
        select(TerrorStats)
        .order_by(desc(TerrorStats.encounter_count))
        .limit(limit)
        .offset(offset)
    )
    stats = result.scalars().all()

    items = []
    for stat in stats:
        survival_rate = 0
        if stat.total_rounds > 0:
            survival_rate = stat.total_survivors / stat.total_rounds * 100

        items.append(TerrorStatItem(
            name=stat.terror_name,
            encounter_count=stat.encounter_count,
            survival_rate=round(survival_rate, 2)
        ))

    return items


@router.get("/round-types", response_model=List[RoundTypeStatItem])
async def get_round_type_stats(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """ラウンドタイプ統計を取得"""
    result = await db.execute(
        select(RoundTypeStats).order_by(desc(RoundTypeStats.occurrence_count))
    )
    stats = result.scalars().all()

    # 合計を計算
    total_occurrences = sum(s.occurrence_count for s in stats)

    items = []
    for stat in stats:
        survival_rate = 0
        if stat.total_players > 0:
            survival_rate = stat.total_survivors / stat.total_players * 100

        percentage = 0
        if total_occurrences > 0:
            percentage = stat.occurrence_count / total_occurrences * 100

        items.append(RoundTypeStatItem(
            round_type=stat.round_type,
            occurrence_count=stat.occurrence_count,
            survival_rate=round(survival_rate, 2),
            percentage=round(percentage, 2)
        ))

    return items


@router.get("/maps", response_model=List[MapStatItem])
async def get_map_stats(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
    limit: int = Query(default=50, le=200)
):
    """マップ統計を取得"""
    result = await db.execute(
        select(MapStats)
        .order_by(desc(MapStats.occurrence_count))
        .limit(limit)
    )
    stats = result.scalars().all()

    # 合計を計算
    total_occurrences = sum(s.occurrence_count for s in stats)

    items = []
    for stat in stats:
        percentage = 0
        if total_occurrences > 0:
            percentage = stat.occurrence_count / total_occurrences * 100

        items.append(MapStatItem(
            map_name=stat.map_name,
            occurrence_count=stat.occurrence_count,
            percentage=round(percentage, 2)
        ))

    return items


@router.get("/recent-rounds", response_model=List[RecentRound])
async def get_recent_rounds(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
    limit: int = Query(default=20, le=100)
):
    """最近のラウンドを取得"""
    result = await db.execute(
        select(Round)
        .order_by(desc(Round.started_at))
        .limit(limit)
    )
    rounds = result.scalars().all()

    return [
        RecentRound(
            id=r.id,
            round_type=r.round_type,
            map_name=r.map_name,
            terrors=r.terrors or [],
            player_count=r.player_count,
            survivor_count=r.survivor_count,
            started_at=r.started_at
        )
        for r in rounds
    ]


@router.get("/active-instances", response_model=List[ActiveInstance])
async def get_active_instances(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
    hours: int = Query(default=1, le=24)
):
    """アクティブなインスタンスを取得"""
    cutoff = datetime.now(timezone.utc) - timedelta(hours=hours)

    result = await db.execute(
        select(Instance)
        .where(Instance.last_activity_at >= cutoff)
        .order_by(desc(Instance.last_activity_at))
        .limit(50)
    )
    instances = result.scalars().all()

    return [
        ActiveInstance(
            id=i.id,
            instance_id=i.instance_id[:50] + "..." if len(i.instance_id) > 50 else i.instance_id,
            total_rounds=i.total_rounds,
            last_activity_at=i.last_activity_at
        )
        for i in instances
    ]
