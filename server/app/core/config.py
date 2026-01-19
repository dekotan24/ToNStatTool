"""アプリケーション設定"""
import os
from typing import List
from dotenv import load_dotenv

load_dotenv()


class Settings:
    # データベース設定
    DATABASE_URL: str = os.getenv(
        "DATABASE_URL",
        "postgresql+asyncpg://ton_user:password@localhost:5432/ton_stats"
    )

    # セキュリティ設定
    SECRET_KEY: str = os.getenv("SECRET_KEY", "change-this-in-production")
    JWT_ALGORITHM: str = os.getenv("JWT_ALGORITHM", "HS256")
    JWT_EXPIRE_MINUTES: int = int(os.getenv("JWT_EXPIRE_MINUTES", "1440"))  # 24時間

    # Cloudflare Turnstile
    TURNSTILE_SITE_KEY: str = os.getenv("TURNSTILE_SITE_KEY", "")
    TURNSTILE_SECRET_KEY: str = os.getenv("TURNSTILE_SECRET_KEY", "")
    TURNSTILE_VERIFY_URL: str = "https://challenges.cloudflare.com/turnstile/v0/siteverify"

    # サーバー設定
    HOST: str = os.getenv("HOST", "127.0.0.1")
    PORT: int = int(os.getenv("PORT", "8000"))
    DEBUG: bool = os.getenv("DEBUG", "false").lower() == "true"

    # CORS設定
    ALLOWED_ORIGINS: List[str] = os.getenv(
        "ALLOWED_ORIGINS",
        "http://localhost:3000"
    ).split(",")

    # レート制限
    RATE_LIMIT_PER_MINUTE: int = int(os.getenv("RATE_LIMIT_PER_MINUTE", "60"))

    # セッション設定
    SESSION_COOKIE_NAME: str = "ton_session"
    SESSION_COOKIE_SECURE: bool = not DEBUG
    SESSION_COOKIE_HTTPONLY: bool = True
    SESSION_COOKIE_SAMESITE: str = "strict"  # CSRFリスク軽減のためstrictに変更

    # セッションタイムアウト設定
    SESSION_ABSOLUTE_TIMEOUT_HOURS: int = int(os.getenv("SESSION_ABSOLUTE_TIMEOUT_HOURS", "72"))  # 絶対タイムアウト

    # ログイン試行制限
    LOGIN_MAX_ATTEMPTS: int = int(os.getenv("LOGIN_MAX_ATTEMPTS", "5"))
    LOGIN_LOCKOUT_MINUTES: int = int(os.getenv("LOGIN_LOCKOUT_MINUTES", "15"))
    LOGIN_ATTEMPT_WINDOW_MINUTES: int = int(os.getenv("LOGIN_ATTEMPT_WINDOW_MINUTES", "15"))

    # パスワードリセット
    PASSWORD_RESET_EXPIRE_MINUTES: int = int(os.getenv("PASSWORD_RESET_EXPIRE_MINUTES", "30"))

    # CSRF設定
    CSRF_TOKEN_EXPIRE_MINUTES: int = int(os.getenv("CSRF_TOKEN_EXPIRE_MINUTES", "60"))

    # 2FA設定
    TOTP_ISSUER: str = os.getenv("TOTP_ISSUER", "ToN Stats")
    TOTP_ENCRYPTION_KEY: str = os.getenv("TOTP_ENCRYPTION_KEY", "")  # 32バイトのキー

    # メール設定（パスワードリセット用）
    SMTP_HOST: str = os.getenv("SMTP_HOST", "")
    SMTP_PORT: int = int(os.getenv("SMTP_PORT", "587"))
    SMTP_USER: str = os.getenv("SMTP_USER", "")
    SMTP_PASSWORD: str = os.getenv("SMTP_PASSWORD", "")
    SMTP_FROM: str = os.getenv("SMTP_FROM", "noreply@example.com")
    SMTP_TLS: bool = os.getenv("SMTP_TLS", "true").lower() == "true"

    # サイトURL（パスワードリセットリンク用）
    SITE_URL: str = os.getenv("SITE_URL", "http://localhost:8000")


settings = Settings()
