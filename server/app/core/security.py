"""セキュリティ関連の処理"""
import hashlib
import secrets
import base64
from datetime import datetime, timedelta, timezone
from typing import Optional, Tuple
from cryptography.fernet import Fernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC

import httpx
import pyotp
from jose import JWTError, jwt
from passlib.hash import bcrypt
from sqlalchemy import select, delete, and_
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings


# ========== パスワード関連 ==========

def hash_password(password: str) -> str:
    """パスワードをbcryptでハッシュ化"""
    return bcrypt.hash(password)


def verify_password(plain_password: str, hashed_password: str) -> bool:
    """パスワードを検証"""
    return bcrypt.verify(plain_password, hashed_password)


# ========== JWT関連 ==========

def create_access_token(data: dict, expires_delta: Optional[timedelta] = None) -> str:
    """JWTアクセストークンを生成"""
    to_encode = data.copy()
    if expires_delta:
        expire = datetime.now(timezone.utc) + expires_delta
    else:
        expire = datetime.now(timezone.utc) + timedelta(minutes=settings.JWT_EXPIRE_MINUTES)

    to_encode.update({"exp": expire})
    encoded_jwt = jwt.encode(
        to_encode,
        settings.SECRET_KEY,
        algorithm=settings.JWT_ALGORITHM
    )
    return encoded_jwt


def decode_access_token(token: str) -> Optional[dict]:
    """JWTトークンをデコード"""
    try:
        payload = jwt.decode(
            token,
            settings.SECRET_KEY,
            algorithms=[settings.JWT_ALGORITHM]
        )
        return payload
    except JWTError:
        return None


# ========== トークンハッシュ ==========

def hash_token(token: str) -> str:
    """トークンをSHA256でハッシュ化（DB保存用）"""
    return hashlib.sha256(token.encode()).hexdigest()


def generate_secure_token(length: int = 32) -> str:
    """セキュアなランダムトークンを生成"""
    return secrets.token_urlsafe(length)


# ========== Turnstile検証 ==========

async def verify_turnstile(token: str, ip: Optional[str] = None) -> bool:
    """Cloudflare Turnstileトークンを検証"""
    if not settings.TURNSTILE_SECRET_KEY:
        # 開発環境でTurnstileが設定されていない場合はスキップ
        return settings.DEBUG

    async with httpx.AsyncClient() as client:
        data = {
            "secret": settings.TURNSTILE_SECRET_KEY,
            "response": token
        }
        if ip:
            data["remoteip"] = ip

        try:
            response = await client.post(
                settings.TURNSTILE_VERIFY_URL,
                data=data
            )
            result = response.json()
            return result.get("success", False)
        except Exception:
            return False


# ========== フィンガープリント ==========

def generate_fingerprint(instance_id: str, round_type: str, terrors: list, timestamp: datetime) -> str:
    """ラウンドのフィンガープリントを生成（重複排除用）"""
    # タイムスタンプを30秒単位に丸める
    rounded_ts = timestamp.replace(second=(timestamp.second // 30) * 30, microsecond=0)

    # テラーをソートして結合
    terrors_str = ",".join(sorted(terrors)) if terrors else ""

    # フィンガープリント生成
    data = f"{instance_id}:{round_type}:{terrors_str}:{rounded_ts.isoformat()}"
    return hashlib.sha256(data.encode()).hexdigest()


# ========== APIキー ==========

def generate_api_key() -> Tuple[str, str]:
    """APIキーを生成（平文キー, ハッシュ）"""
    key = f"ton_{secrets.token_urlsafe(32)}"
    key_hash = hash_token(key)
    return key, key_hash


# ========== CSRF ==========

def generate_csrf_token() -> str:
    """CSRFトークンを生成"""
    return secrets.token_urlsafe(32)


# ========== 2FA (TOTP) ==========

def get_encryption_key() -> bytes:
    """TOTP暗号化用のキーを取得"""
    if settings.TOTP_ENCRYPTION_KEY:
        # 環境変数から取得（すでにbase64エンコードされたキー）
        return settings.TOTP_ENCRYPTION_KEY.encode()
    else:
        # SECRET_KEYから派生
        kdf = PBKDF2HMAC(
            algorithm=hashes.SHA256(),
            length=32,
            salt=b"totp_secret_salt",
            iterations=100000,
        )
        return base64.urlsafe_b64encode(kdf.derive(settings.SECRET_KEY.encode()))


def encrypt_totp_secret(secret: str) -> str:
    """TOTPシークレットを暗号化"""
    f = Fernet(get_encryption_key())
    return f.encrypt(secret.encode()).decode()


def decrypt_totp_secret(encrypted: str) -> str:
    """TOTPシークレットを復号化"""
    f = Fernet(get_encryption_key())
    return f.decrypt(encrypted.encode()).decode()


def generate_totp_secret() -> str:
    """TOTPシークレットを生成"""
    return pyotp.random_base32()


def get_totp_uri(secret: str, username: str) -> str:
    """TOTP URIを生成（QRコード用）"""
    totp = pyotp.TOTP(secret)
    return totp.provisioning_uri(name=username, issuer_name=settings.TOTP_ISSUER)


def verify_totp(secret: str, code: str) -> bool:
    """TOTPコードを検証"""
    totp = pyotp.TOTP(secret)
    return totp.verify(code, valid_window=1)  # 前後30秒を許容


def generate_backup_codes(count: int = 10) -> Tuple[list, list]:
    """バックアップコードを生成（平文リスト, ハッシュリスト）"""
    codes = [secrets.token_hex(4).upper() for _ in range(count)]
    hashes = [hash_token(code) for code in codes]
    return codes, hashes


def verify_backup_code(code: str, hashes: list) -> Tuple[bool, Optional[int]]:
    """バックアップコードを検証（成功, 使用したインデックス）"""
    code_hash = hash_token(code.upper().replace("-", "").replace(" ", ""))
    for i, h in enumerate(hashes):
        if h and h == code_hash:
            return True, i
    return False, None


# ========== セキュリティログ ==========

class SecurityEventType:
    """セキュリティイベントタイプ"""
    LOGIN_SUCCESS = "LOGIN_SUCCESS"
    LOGIN_FAILED = "LOGIN_FAILED"
    LOGIN_LOCKED = "LOGIN_LOCKED"
    LOGOUT = "LOGOUT"
    REGISTER = "REGISTER"
    PASSWORD_CHANGE = "PASSWORD_CHANGE"
    PASSWORD_RESET_REQUEST = "PASSWORD_RESET_REQUEST"
    PASSWORD_RESET_SUCCESS = "PASSWORD_RESET_SUCCESS"
    TOTP_ENABLED = "TOTP_ENABLED"
    TOTP_DISABLED = "TOTP_DISABLED"
    TOTP_FAILED = "TOTP_FAILED"
    API_KEY_CREATED = "API_KEY_CREATED"
    API_KEY_REVOKED = "API_KEY_REVOKED"
    SESSION_EXPIRED = "SESSION_EXPIRED"
    CSRF_FAILED = "CSRF_FAILED"


async def log_security_event(
    db: AsyncSession,
    event_type: str,
    user_id: Optional[int] = None,
    username: Optional[str] = None,
    ip_address: Optional[str] = None,
    user_agent: Optional[str] = None,
    details: Optional[dict] = None
):
    """セキュリティイベントをログに記録"""
    from app.models import SecurityLog

    log = SecurityLog(
        event_type=event_type,
        user_id=user_id,
        username=username,
        ip_address=ip_address,
        user_agent=user_agent,
        details=details
    )
    db.add(log)
    # コミットは呼び出し元で行う


# ========== ログイン試行制限 ==========

async def check_account_locked(db: AsyncSession, username: str) -> Optional[datetime]:
    """アカウントがロックされているか確認（ロック解除時刻を返す）"""
    from app.models import AccountLock

    result = await db.execute(
        select(AccountLock).where(
            AccountLock.username == username,
            AccountLock.locked_until > datetime.now(timezone.utc)
        )
    )
    lock = result.scalar_one_or_none()
    return lock.locked_until if lock else None


async def record_login_attempt(
    db: AsyncSession,
    username: str,
    ip_address: str,
    success: bool
) -> Optional[datetime]:
    """ログイン試行を記録し、ロックが必要な場合はロック"""
    from app.models import LoginAttempt, AccountLock

    # 試行を記録
    attempt = LoginAttempt(
        username=username,
        ip_address=ip_address,
        success=success
    )
    db.add(attempt)

    if success:
        # 成功時は古い失敗記録を削除
        await db.execute(
            delete(LoginAttempt).where(
                LoginAttempt.username == username,
                LoginAttempt.success == False
            )
        )
        # ロックも解除
        await db.execute(
            delete(AccountLock).where(AccountLock.username == username)
        )
        return None

    # 失敗回数をカウント
    window_start = datetime.now(timezone.utc) - timedelta(minutes=settings.LOGIN_ATTEMPT_WINDOW_MINUTES)
    result = await db.execute(
        select(LoginAttempt).where(
            LoginAttempt.username == username,
            LoginAttempt.success == False,
            LoginAttempt.attempted_at >= window_start
        )
    )
    failed_attempts = len(result.scalars().all())

    if failed_attempts >= settings.LOGIN_MAX_ATTEMPTS:
        # アカウントをロック
        locked_until = datetime.now(timezone.utc) + timedelta(minutes=settings.LOGIN_LOCKOUT_MINUTES)

        # 既存のロックを削除して新しいロックを作成
        await db.execute(
            delete(AccountLock).where(AccountLock.username == username)
        )

        lock = AccountLock(
            username=username,
            locked_until=locked_until,
            lock_reason=f"Failed login attempts: {failed_attempts}"
        )
        db.add(lock)
        return locked_until

    return None
