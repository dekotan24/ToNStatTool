"""データベース接続設定"""
from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession, async_sessionmaker
from sqlalchemy.orm import declarative_base

from app.core.config import settings

# 非同期エンジン作成
engine = create_async_engine(
    settings.DATABASE_URL,
    echo=settings.DEBUG,
    pool_pre_ping=True,
    pool_size=10,
    max_overflow=20
)

# セッションファクトリ
async_session_maker = async_sessionmaker(
    engine,
    class_=AsyncSession,
    expire_on_commit=False
)

# ベースクラス
Base = declarative_base()


async def get_db() -> AsyncSession:
    """データベースセッションを取得"""
    async with async_session_maker() as session:
        try:
            yield session
            await session.commit()
        except Exception:
            await session.rollback()
            raise
        finally:
            await session.close()


async def init_db():
    """データベース初期化"""
    from sqlalchemy import text

    async with engine.begin() as conn:
        # api_keysテーブルにkey_prefixカラムがなければ追加
        try:
            await conn.execute(text(
                "ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS key_prefix VARCHAR(16)"
            ))
            await conn.execute(text(
                "ALTER TABLE api_keys ADD COLUMN IF NOT EXISTS use_count INTEGER DEFAULT 0"
            ))
        except Exception:
            pass  # カラムが既に存在する場合はスキップ

        await conn.run_sync(Base.metadata.create_all)
