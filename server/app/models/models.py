"""SQLAlchemyモデル定義"""
from datetime import datetime, timezone
from typing import List, Optional

from sqlalchemy import (
    Boolean, Column, DateTime, ForeignKey, Integer, String, Text,
    ARRAY, func
)
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.orm import relationship

from app.core.database import Base


class User(Base):
    """ユーザーモデル"""
    __tablename__ = "users"

    id = Column(Integer, primary_key=True, index=True)
    username = Column(String(50), unique=True, nullable=False, index=True)
    email = Column(String(255), unique=True, nullable=False, index=True)
    password_hash = Column(String(255), nullable=False)
    is_active = Column(Boolean, default=True)
    is_admin = Column(Boolean, default=False)
    totp_enabled = Column(Boolean, default=False)
    created_at = Column(DateTime(timezone=True), server_default=func.now())
    updated_at = Column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())
    last_login_at = Column(DateTime(timezone=True), nullable=True)

    # リレーション
    sessions = relationship("Session", back_populates="user", cascade="all, delete-orphan")
    totp_secret = relationship("TOTPSecret", back_populates="user", uselist=False, cascade="all, delete-orphan")
    api_keys = relationship("APIKey", back_populates="user", cascade="all, delete-orphan")


class Session(Base):
    """セッションモデル"""
    __tablename__ = "sessions"

    id = Column(Integer, primary_key=True, index=True)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="CASCADE"), nullable=False)
    token_hash = Column(String(64), nullable=False, index=True)
    expires_at = Column(DateTime(timezone=True), nullable=False)
    absolute_expires_at = Column(DateTime(timezone=True), nullable=True)  # 絶対タイムアウト
    created_at = Column(DateTime(timezone=True), server_default=func.now())
    is_revoked = Column(Boolean, default=False)

    # リレーション
    user = relationship("User", back_populates="sessions")
    csrf_tokens = relationship("CSRFToken", back_populates="session", cascade="all, delete-orphan")


class Instance(Base):
    """インスタンスモデル"""
    __tablename__ = "instances"

    id = Column(Integer, primary_key=True, index=True)
    instance_id = Column(String(500), unique=True, nullable=False, index=True)
    world_id = Column(String(100), nullable=True)
    created_at = Column(DateTime(timezone=True), server_default=func.now())
    last_activity_at = Column(DateTime(timezone=True), server_default=func.now())
    total_rounds = Column(Integer, default=0)

    # リレーション（ラウンドはインスタンス削除時にinstance_id=NULLになる）
    rounds = relationship("Round", back_populates="instance")


class Round(Base):
    """ラウンドモデル"""
    __tablename__ = "rounds"

    id = Column(Integer, primary_key=True, index=True)
    # インスタンスが削除されてもラウンドは保持（NULLになる）
    instance_id = Column(Integer, ForeignKey("instances.id", ondelete="SET NULL"), nullable=True)
    fingerprint = Column(String(64), unique=True, nullable=False, index=True)
    round_type = Column(String(50), nullable=False, index=True)
    map_name = Column(String(100), nullable=True)
    terrors = Column(ARRAY(Text), nullable=True)
    started_at = Column(DateTime(timezone=True), server_default=func.now(), index=True)
    player_count = Column(Integer, default=0)
    survivor_count = Column(Integer, default=0)

    # リレーション
    instance = relationship("Instance", back_populates="rounds")
    player_rounds = relationship("PlayerRound", back_populates="round", cascade="all, delete-orphan")


class Player(Base):
    """VRChatプレイヤーモデル"""
    __tablename__ = "players"

    id = Column(Integer, primary_key=True, index=True)
    vrchat_id = Column(String(100), unique=True, nullable=True, index=True)  # VRChat GUID (usr_xxx)
    vrchat_name = Column(String(100), nullable=False, index=True)  # VRChat表示名（変更可能）
    api_key_id = Column(Integer, ForeignKey("api_keys.id", ondelete="SET NULL"), nullable=True)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="SET NULL"), nullable=True)
    avatar_seed = Column(String(32), nullable=True)  # ランダムアバター用シード
    bio = Column(Text, nullable=True)
    is_public = Column(Boolean, default=True)  # プロフィール公開設定
    created_at = Column(DateTime(timezone=True), server_default=func.now())
    updated_at = Column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())

    # 統計キャッシュ
    total_rounds = Column(Integer, default=0)
    total_survivals = Column(Integer, default=0)

    # リレーション
    api_key = relationship("APIKey")
    user = relationship("User")
    player_rounds = relationship("PlayerRound", back_populates="player", cascade="all, delete-orphan")


class PlayerRound(Base):
    """プレイヤーのラウンド参加記録"""
    __tablename__ = "player_rounds"

    id = Column(Integer, primary_key=True, index=True)
    player_id = Column(Integer, ForeignKey("players.id", ondelete="CASCADE"), nullable=False, index=True)
    round_id = Column(Integer, ForeignKey("rounds.id", ondelete="CASCADE"), nullable=False, index=True)
    survived = Column(Boolean, nullable=False)
    items = Column(ARRAY(Text), nullable=True)  # 所持アイテムリスト
    notes = Column(Text, nullable=True)  # メモ
    created_at = Column(DateTime(timezone=True), server_default=func.now())

    # リレーション
    player = relationship("Player", back_populates="player_rounds")
    round = relationship("Round", back_populates="player_rounds")


class ItemStats(Base):
    """アイテム統計モデル"""
    __tablename__ = "item_stats"

    id = Column(Integer, primary_key=True, index=True)
    item_name = Column(String(100), unique=True, nullable=False, index=True)
    total_held = Column(Integer, default=0)  # 所持された回数
    total_survivals = Column(Integer, default=0)  # 所持時の生存回数
    updated_at = Column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())


class TerrorStats(Base):
    """テラー統計モデル"""
    __tablename__ = "terror_stats"

    id = Column(Integer, primary_key=True, index=True)
    terror_name = Column(String(100), unique=True, nullable=False, index=True)
    encounter_count = Column(Integer, default=0)
    total_rounds = Column(Integer, default=0)
    total_survivors = Column(Integer, default=0)
    updated_at = Column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())


class RoundTypeStats(Base):
    """ラウンドタイプ統計モデル"""
    __tablename__ = "round_type_stats"

    id = Column(Integer, primary_key=True, index=True)
    round_type = Column(String(50), unique=True, nullable=False, index=True)
    occurrence_count = Column(Integer, default=0)
    total_players = Column(Integer, default=0)
    total_survivors = Column(Integer, default=0)
    updated_at = Column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())


class MapStats(Base):
    """マップ統計モデル"""
    __tablename__ = "map_stats"

    id = Column(Integer, primary_key=True, index=True)
    map_name = Column(String(100), unique=True, nullable=False, index=True)
    occurrence_count = Column(Integer, default=0)
    updated_at = Column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())


# ========== セキュリティ関連モデル ==========

class APIKey(Base):
    """APIキーモデル"""
    __tablename__ = "api_keys"

    id = Column(Integer, primary_key=True, index=True)
    key_hash = Column(String(64), unique=True, nullable=False, index=True)
    key_prefix = Column(String(16), nullable=False)  # ton_XXXXXXXX
    name = Column(String(100), nullable=False)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="CASCADE"), nullable=True)
    is_active = Column(Boolean, default=True)
    use_count = Column(Integer, default=0)
    created_at = Column(DateTime(timezone=True), server_default=func.now())
    last_used_at = Column(DateTime(timezone=True), nullable=True)
    expires_at = Column(DateTime(timezone=True), nullable=True)

    # リレーション
    user = relationship("User", back_populates="api_keys")


class LoginAttempt(Base):
    """ログイン試行モデル"""
    __tablename__ = "login_attempts"

    id = Column(Integer, primary_key=True, index=True)
    username = Column(String(50), nullable=False, index=True)
    ip_address = Column(String(45), nullable=False, index=True)
    success = Column(Boolean, nullable=False)
    attempted_at = Column(DateTime(timezone=True), server_default=func.now(), index=True)


class AccountLock(Base):
    """アカウントロックモデル"""
    __tablename__ = "account_locks"

    id = Column(Integer, primary_key=True, index=True)
    username = Column(String(50), unique=True, nullable=False, index=True)
    locked_until = Column(DateTime(timezone=True), nullable=False)
    lock_reason = Column(String(255), nullable=True)
    created_at = Column(DateTime(timezone=True), server_default=func.now())


class SecurityLog(Base):
    """セキュリティイベントログモデル"""
    __tablename__ = "security_logs"

    id = Column(Integer, primary_key=True, index=True)
    event_type = Column(String(50), nullable=False, index=True)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="SET NULL"), nullable=True)
    username = Column(String(50), nullable=True)
    ip_address = Column(String(45), nullable=True)
    user_agent = Column(Text, nullable=True)
    details = Column(JSONB, nullable=True)
    created_at = Column(DateTime(timezone=True), server_default=func.now(), index=True)


class PasswordResetToken(Base):
    """パスワードリセットトークンモデル"""
    __tablename__ = "password_reset_tokens"

    id = Column(Integer, primary_key=True, index=True)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="CASCADE"), nullable=False)
    token_hash = Column(String(64), unique=True, nullable=False, index=True)
    expires_at = Column(DateTime(timezone=True), nullable=False)
    used_at = Column(DateTime(timezone=True), nullable=True)
    created_at = Column(DateTime(timezone=True), server_default=func.now())


class TOTPSecret(Base):
    """TOTP（2FA）シークレットモデル"""
    __tablename__ = "totp_secrets"

    id = Column(Integer, primary_key=True, index=True)
    user_id = Column(Integer, ForeignKey("users.id", ondelete="CASCADE"), unique=True, nullable=False)
    secret_encrypted = Column(String(255), nullable=False)
    is_enabled = Column(Boolean, default=False)
    backup_codes_hash = Column(ARRAY(Text), nullable=True)
    created_at = Column(DateTime(timezone=True), server_default=func.now())
    verified_at = Column(DateTime(timezone=True), nullable=True)

    # リレーション
    user = relationship("User", back_populates="totp_secret")


class CSRFToken(Base):
    """CSRFトークンモデル"""
    __tablename__ = "csrf_tokens"

    id = Column(Integer, primary_key=True, index=True)
    token_hash = Column(String(64), unique=True, nullable=False, index=True)
    session_id = Column(Integer, ForeignKey("sessions.id", ondelete="CASCADE"), nullable=False)
    expires_at = Column(DateTime(timezone=True), nullable=False)
    created_at = Column(DateTime(timezone=True), server_default=func.now())

    # リレーション
    session = relationship("Session", back_populates="csrf_tokens")
