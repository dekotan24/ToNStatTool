"""統計API"""
from datetime import datetime, timedelta, timezone
from typing import List, Optional

from fastapi import APIRouter, Depends, Query
from pydantic import BaseModel
from sqlalchemy import select, func, desc, or_, any_
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
    instance_id: Optional[str] = None
    instance_db_id: Optional[int] = None
    round_type: str
    map_name: Optional[str]
    terrors: List[str]
    player_count: int
    started_at: datetime


class ActiveInstance(BaseModel):
    id: int
    instance_id: str
    total_rounds: int
    last_activity_at: datetime
    # Latest round info
    latest_round_type: Optional[str] = None
    latest_map: Optional[str] = None
    latest_terrors: List[str] = []
    latest_player_count: int = 0
    latest_survivor_count: int = 0


class InstanceSearchResult(BaseModel):
    id: int
    instance_id: str
    total_rounds: int
    created_at: datetime
    last_activity_at: datetime
    # Latest round info
    latest_round_type: Optional[str] = None
    latest_map: Optional[str] = None
    latest_terrors: List[str] = []
    latest_player_count: int = 0
    latest_survivor_count: int = 0


class InstanceSearchResponse(BaseModel):
    results: List[InstanceSearchResult]
    total: int
    page: int
    per_page: int


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
    limit: int = Query(default=20, le=500)
):
    """最近のラウンドを取得"""
    result = await db.execute(
        select(Round, Instance.instance_id, Instance.id)
        .join(Instance, Round.instance_id == Instance.id)
        .order_by(desc(Round.started_at))
        .limit(limit)
    )
    rows = result.all()

    return [
        RecentRound(
            id=r.id,
            instance_id=inst_id,
            instance_db_id=inst_db_id,
            round_type=r.round_type,
            map_name=r.map_name,
            terrors=r.terrors or [],
            player_count=r.player_count,
            started_at=r.started_at
        )
        for r, inst_id, inst_db_id in rows
    ]


@router.get("/active-instances", response_model=List[ActiveInstance])
async def get_active_instances(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
    hours: int = Query(default=1, le=24)
):
    """アクティブなインスタンスを取得（最新ラウンド情報付き）"""
    cutoff = datetime.now(timezone.utc) - timedelta(hours=hours)

    result = await db.execute(
        select(Instance)
        .where(Instance.last_activity_at >= cutoff)
        .order_by(desc(Instance.last_activity_at))
        .limit(50)
    )
    instances = result.scalars().all()

    active_instances = []
    for i in instances:
        # Get the latest round for this instance
        round_result = await db.execute(
            select(Round)
            .where(Round.instance_id == i.id)
            .order_by(desc(Round.started_at))
            .limit(1)
        )
        latest_round = round_result.scalar_one_or_none()

        active_instances.append(ActiveInstance(
            id=i.id,
            instance_id=i.instance_id[:50] + "..." if len(i.instance_id) > 50 else i.instance_id,
            total_rounds=i.total_rounds,
            last_activity_at=i.last_activity_at,
            latest_round_type=latest_round.round_type if latest_round else None,
            latest_map=latest_round.map_name if latest_round else None,
            latest_terrors=latest_round.terrors if latest_round and latest_round.terrors else [],
            latest_player_count=latest_round.player_count if latest_round else 0,
            latest_survivor_count=latest_round.survivor_count if latest_round else 0
        ))

    return active_instances


@router.get("/instances/search", response_model=InstanceSearchResponse)
async def search_instances(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db),
    instance_id: Optional[str] = Query(default=None, description="Instance ID (partial match)"),
    terror: Optional[str] = Query(default=None, description="Terror name to search"),
    round_type: Optional[str] = Query(default=None, description="Round type to filter"),
    map_name: Optional[str] = Query(default=None, description="Map name to filter"),
    page: int = Query(default=1, ge=1),
    per_page: int = Query(default=20, le=100)
):
    """インスタンスを検索"""
    # Base query to find instances that have matching rounds
    # We need to find instances whose rounds match the filters

    # First, let's build a subquery to find instance IDs that match our criteria
    round_filters = []

    if terror:
        # PostgreSQL array contains check
        round_filters.append(Round.terrors.any(terror))

    if round_type:
        round_filters.append(Round.round_type.ilike(f"%{round_type}%"))

    if map_name:
        round_filters.append(Round.map_name.ilike(f"%{map_name}%"))

    # Build the main query
    query = select(Instance)

    if instance_id:
        # Search for instance_id containing the search term
        query = query.where(Instance.instance_id.ilike(f"%{instance_id}%"))

    if round_filters:
        # Get instance IDs that have matching rounds
        matching_instance_ids_query = (
            select(Round.instance_id)
            .where(*round_filters)
            .distinct()
        )
        query = query.where(Instance.id.in_(matching_instance_ids_query))

    # Count total
    count_query = select(func.count()).select_from(query.subquery())
    total_result = await db.execute(count_query)
    total = total_result.scalar() or 0

    # Apply pagination
    offset = (page - 1) * per_page
    query = query.order_by(desc(Instance.last_activity_at)).offset(offset).limit(per_page)

    result = await db.execute(query)
    instances = result.scalars().all()

    # Build results with latest round info
    search_results = []
    for i in instances:
        # Get the latest round for this instance
        round_result = await db.execute(
            select(Round)
            .where(Round.instance_id == i.id)
            .order_by(desc(Round.started_at))
            .limit(1)
        )
        latest_round = round_result.scalar_one_or_none()

        search_results.append(InstanceSearchResult(
            id=i.id,
            instance_id=i.instance_id,
            total_rounds=i.total_rounds,
            created_at=i.created_at,
            last_activity_at=i.last_activity_at,
            latest_round_type=latest_round.round_type if latest_round else None,
            latest_map=latest_round.map_name if latest_round else None,
            latest_terrors=latest_round.terrors if latest_round and latest_round.terrors else [],
            latest_player_count=latest_round.player_count if latest_round else 0,
            latest_survivor_count=latest_round.survivor_count if latest_round else 0
        ))

    return InstanceSearchResponse(
        results=search_results,
        total=total,
        page=page,
        per_page=per_page
    )


@router.get("/filter-options")
async def get_filter_options(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """検索フィルター用のオプションを取得"""
    # Get all round types
    round_types_result = await db.execute(
        select(RoundTypeStats.round_type)
        .order_by(desc(RoundTypeStats.occurrence_count))
    )
    round_types = [r[0] for r in round_types_result.all()]

    # Get all terrors
    terrors_result = await db.execute(
        select(TerrorStats.terror_name)
        .order_by(desc(TerrorStats.encounter_count))
        .limit(100)
    )
    terrors = [r[0] for r in terrors_result.all()]

    # Get all maps
    maps_result = await db.execute(
        select(MapStats.map_name)
        .order_by(desc(MapStats.occurrence_count))
        .limit(100)
    )
    maps = [r[0] for r in maps_result.all()]

    return {
        "round_types": round_types,
        "terrors": terrors,
        "maps": maps
    }


# ========== Instance Detail Models ==========

class InstanceRoundDetail(BaseModel):
    id: int
    round_type: str
    map_name: Optional[str]
    terrors: List[str]
    player_count: int
    survivor_count: int
    started_at: datetime


class InstanceTerrorStat(BaseModel):
    terror_name: str
    encounter_count: int


class InstanceDetail(BaseModel):
    id: int
    instance_id: str
    world_id: Optional[str]
    total_rounds: int
    created_at: datetime
    last_activity_at: datetime
    rounds: List[InstanceRoundDetail]
    terror_stats: List[InstanceTerrorStat]
    round_type_stats: dict
    map_stats: dict


@router.get("/instance/{instance_short_id}", response_model=InstanceDetail)
async def get_instance_detail(
    instance_short_id: str,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """インスタンス詳細を取得（インスタンスURLの5桁IDで検索）"""
    # インスタンスIDに短縮IDを含むものを検索
    # 例: wrld_xxx:12345~region(...) から 12345 で検索
    # まずチルダ付きで検索、見つからなければチルダなしでも検索
    result = await db.execute(
        select(Instance).where(
            Instance.instance_id.like(f"%:{instance_short_id}~%")
        ).order_by(desc(Instance.last_activity_at))
        .limit(1)
    )
    instance = result.scalar_one_or_none()

    # チルダなしの形式でも検索（フォールバック）
    if not instance:
        result = await db.execute(
            select(Instance).where(
                Instance.instance_id.like(f"%:{instance_short_id}")
            ).order_by(desc(Instance.last_activity_at))
            .limit(1)
        )
        instance = result.scalar_one_or_none()

    if not instance:
        from fastapi import HTTPException, status
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="Instance not found"
        )

    # ラウンド履歴を取得（直近50件）
    rounds_result = await db.execute(
        select(Round)
        .where(Round.instance_id == instance.id)
        .order_by(desc(Round.started_at))
        .limit(50)
    )
    rounds = rounds_result.scalars().all()

    # ラウンド詳細リスト
    round_details = [
        InstanceRoundDetail(
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

    # テラー統計を集計
    terror_counts = {}
    round_type_counts = {}
    map_counts = {}

    for r in rounds:
        # テラー統計
        if r.terrors:
            for terror in r.terrors:
                terror_counts[terror] = terror_counts.get(terror, 0) + 1

        # ラウンドタイプ統計
        round_type_counts[r.round_type] = round_type_counts.get(r.round_type, 0) + 1

        # マップ統計
        if r.map_name:
            map_counts[r.map_name] = map_counts.get(r.map_name, 0) + 1

    # テラー統計をソートしてリストに変換
    terror_stats = [
        InstanceTerrorStat(terror_name=name, encounter_count=count)
        for name, count in sorted(terror_counts.items(), key=lambda x: x[1], reverse=True)
    ]

    return InstanceDetail(
        id=instance.id,
        instance_id=instance.instance_id,
        world_id=instance.world_id,
        total_rounds=instance.total_rounds,
        created_at=instance.created_at,
        last_activity_at=instance.last_activity_at,
        rounds=round_details,
        terror_stats=terror_stats,
        round_type_stats=round_type_counts,
        map_stats=map_counts
    )
