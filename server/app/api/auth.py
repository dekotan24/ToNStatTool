"""認証API"""
import io
import base64
from datetime import datetime, timedelta, timezone
from typing import Optional

import qrcode
from fastapi import APIRouter, Depends, HTTPException, Request, Response, status, Header
from pydantic import BaseModel, EmailStr, field_validator
from sqlalchemy import select, and_
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.core.database import get_db
from app.core.security import (
    hash_password,
    verify_password,
    create_access_token,
    decode_access_token,
    hash_token,
    generate_secure_token,
    verify_turnstile,
    generate_csrf_token,
    generate_totp_secret,
    encrypt_totp_secret,
    decrypt_totp_secret,
    get_totp_uri,
    verify_totp,
    generate_backup_codes,
    verify_backup_code,
    log_security_event,
    SecurityEventType,
    check_account_locked,
    record_login_attempt
)
from app.core.security import generate_api_key
from app.models import User, Session, CSRFToken, TOTPSecret, PasswordResetToken, APIKey

router = APIRouter(prefix="/api/v1/auth", tags=["auth"])


# ========== Pydantic Models ==========

class RegisterRequest(BaseModel):
    username: str
    email: EmailStr
    password: str
    turnstile_token: str

    @field_validator("username")
    @classmethod
    def validate_username(cls, v):
        if len(v) < 3 or len(v) > 50:
            raise ValueError("ユーザー名は3〜50文字で入力してください")
        if not v.replace("_", "").replace("-", "").isalnum():
            raise ValueError("ユーザー名は英数字、アンダースコア、ハイフンのみ使用できます")
        return v

    @field_validator("password")
    @classmethod
    def validate_password(cls, v):
        if len(v) < 8:
            raise ValueError("パスワードは8文字以上で入力してください")
        return v


class LoginRequest(BaseModel):
    username: str
    password: str
    turnstile_token: str
    totp_code: Optional[str] = None  # 2FA有効時に必要


class TokenResponse(BaseModel):
    message: str
    username: str
    requires_totp: bool = False


class UserResponse(BaseModel):
    id: int
    username: str
    email: str
    is_admin: bool
    totp_enabled: bool
    created_at: datetime


class CSRFResponse(BaseModel):
    csrf_token: str


class TOTPSetupResponse(BaseModel):
    secret: str
    qr_code: str  # Base64エンコードされたQRコード画像
    backup_codes: list[str]


class TOTPVerifyRequest(BaseModel):
    code: str


class PasswordResetRequestModel(BaseModel):
    email: EmailStr
    turnstile_token: str


class PasswordResetConfirmModel(BaseModel):
    token: str
    new_password: str
    turnstile_token: str

    @field_validator("new_password")
    @classmethod
    def validate_password(cls, v):
        if len(v) < 8:
            raise ValueError("パスワードは8文字以上で入力してください")
        return v


class ChangePasswordRequest(BaseModel):
    current_password: str
    new_password: str

    @field_validator("new_password")
    @classmethod
    def validate_password(cls, v):
        if len(v) < 8:
            raise ValueError("パスワードは8文字以上で入力してください")
        return v


# ========== Helper Functions ==========

def get_client_ip(request: Request) -> str:
    """クライアントIPを取得"""
    forwarded = request.headers.get("x-forwarded-for")
    if forwarded:
        return forwarded.split(",")[0].strip()
    return request.client.host if request.client else "unknown"


def get_user_agent(request: Request) -> str:
    """User-Agentを取得"""
    return request.headers.get("user-agent", "unknown")[:500]


# ========== Dependencies ==========

async def get_current_user(
    request: Request,
    db: AsyncSession = Depends(get_db)
) -> User:
    """現在のログインユーザーを取得"""
    token = request.cookies.get(settings.SESSION_COOKIE_NAME)
    if not token:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="認証が必要です"
        )

    payload = decode_access_token(token)
    if not payload:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="無効なトークンです"
        )

    user_id = payload.get("sub")
    if not user_id:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="無効なトークンです"
        )

    # セッションの有効性を確認
    token_hash = hash_token(token)
    now = datetime.now(timezone.utc)
    result = await db.execute(
        select(Session).where(
            Session.token_hash == token_hash,
            Session.is_revoked == False,
            Session.expires_at > now
        )
    )
    session = result.scalar_one_or_none()
    if not session:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="セッションが無効または期限切れです"
        )

    # 絶対タイムアウトのチェック
    if session.absolute_expires_at and session.absolute_expires_at < now:
        session.is_revoked = True
        await log_security_event(
            db, SecurityEventType.SESSION_EXPIRED,
            user_id=int(user_id),
            ip_address=get_client_ip(request)
        )
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="セッションの有効期限が切れました。再ログインしてください。"
        )

    # ユーザーを取得
    result = await db.execute(select(User).where(User.id == int(user_id)))
    user = result.scalar_one_or_none()
    if not user or not user.is_active:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="ユーザーが見つかりません"
        )

    return user


async def get_optional_user(
    request: Request,
    db: AsyncSession = Depends(get_db)
) -> Optional[User]:
    """ログインユーザーを取得（未ログインはNone）"""
    try:
        return await get_current_user(request, db)
    except HTTPException:
        return None


async def verify_csrf_token(
    request: Request,
    db: AsyncSession = Depends(get_db),
    x_csrf_token: Optional[str] = Header(None)
) -> bool:
    """CSRFトークンを検証"""
    if request.method in ("GET", "HEAD", "OPTIONS"):
        return True

    if not x_csrf_token:
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="CSRFトークンがありません"
        )

    token_hash = hash_token(x_csrf_token)
    now = datetime.now(timezone.utc)

    result = await db.execute(
        select(CSRFToken).where(
            CSRFToken.token_hash == token_hash,
            CSRFToken.expires_at > now
        )
    )
    csrf = result.scalar_one_or_none()

    if not csrf:
        await log_security_event(
            db, SecurityEventType.CSRF_FAILED,
            ip_address=get_client_ip(request),
            user_agent=get_user_agent(request)
        )
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="無効なCSRFトークンです"
        )

    return True


# ========== Endpoints ==========

@router.post("/register", response_model=TokenResponse)
async def register(
    request: Request,
    response: Response,
    data: RegisterRequest,
    db: AsyncSession = Depends(get_db)
):
    """新規ユーザー登録"""
    ip = get_client_ip(request)
    ua = get_user_agent(request)

    # Turnstile検証
    if not await verify_turnstile(data.turnstile_token, ip):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="認証に失敗しました。もう一度お試しください。"
        )

    # ユーザー名の重複チェック（曖昧なエラーメッセージ）
    result = await db.execute(select(User).where(User.username == data.username))
    if result.scalar_one_or_none():
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="登録できませんでした。入力内容を確認してください。"
        )

    # メールアドレスの重複チェック（曖昧なエラーメッセージ）
    result = await db.execute(select(User).where(User.email == data.email))
    if result.scalar_one_or_none():
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="登録できませんでした。入力内容を確認してください。"
        )

    # ユーザー作成
    user = User(
        username=data.username,
        email=data.email,
        password_hash=hash_password(data.password)
    )
    db.add(user)
    await db.flush()

    # セッション作成
    token = create_access_token({"sub": str(user.id)})
    expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.JWT_EXPIRE_MINUTES)
    absolute_expires_at = datetime.now(timezone.utc) + timedelta(hours=settings.SESSION_ABSOLUTE_TIMEOUT_HOURS)

    session = Session(
        user_id=user.id,
        token_hash=hash_token(token),
        expires_at=expires_at,
        absolute_expires_at=absolute_expires_at
    )
    db.add(session)

    # セキュリティログ
    await log_security_event(
        db, SecurityEventType.REGISTER,
        user_id=user.id,
        username=user.username,
        ip_address=ip,
        user_agent=ua
    )

    # Cookieにセット
    response.set_cookie(
        key=settings.SESSION_COOKIE_NAME,
        value=token,
        httponly=settings.SESSION_COOKIE_HTTPONLY,
        secure=settings.SESSION_COOKIE_SECURE,
        samesite=settings.SESSION_COOKIE_SAMESITE,
        max_age=settings.JWT_EXPIRE_MINUTES * 60
    )

    return TokenResponse(message="登録が完了しました", username=user.username)


@router.post("/login", response_model=TokenResponse)
async def login(
    request: Request,
    response: Response,
    data: LoginRequest,
    db: AsyncSession = Depends(get_db)
):
    """ログイン"""
    ip = get_client_ip(request)
    ua = get_user_agent(request)

    # Turnstile検証
    if not await verify_turnstile(data.turnstile_token, ip):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="認証に失敗しました。もう一度お試しください。"
        )

    # アカウントロックチェック
    locked_until = await check_account_locked(db, data.username)
    if locked_until:
        await log_security_event(
            db, SecurityEventType.LOGIN_LOCKED,
            username=data.username,
            ip_address=ip,
            user_agent=ua
        )
        remaining = int((locked_until - datetime.now(timezone.utc)).total_seconds() / 60) + 1
        raise HTTPException(
            status_code=status.HTTP_429_TOO_MANY_REQUESTS,
            detail=f"アカウントがロックされています。{remaining}分後に再試行してください。"
        )

    # ユーザー検索
    result = await db.execute(select(User).where(User.username == data.username))
    user = result.scalar_one_or_none()

    if not user or not verify_password(data.password, user.password_hash):
        # ログイン失敗を記録
        locked_until = await record_login_attempt(db, data.username, ip, False)
        await log_security_event(
            db, SecurityEventType.LOGIN_FAILED,
            username=data.username,
            ip_address=ip,
            user_agent=ua
        )

        if locked_until:
            remaining = int((locked_until - datetime.now(timezone.utc)).total_seconds() / 60) + 1
            raise HTTPException(
                status_code=status.HTTP_429_TOO_MANY_REQUESTS,
                detail=f"ログイン試行回数が上限に達しました。{remaining}分後に再試行してください。"
            )

        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="ユーザー名またはパスワードが正しくありません"
        )

    if not user.is_active:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="このアカウントは無効化されています"
        )

    # 2FA検証
    if user.totp_enabled:
        if not data.totp_code:
            return TokenResponse(
                message="2FAコードが必要です",
                username=user.username,
                requires_totp=True
            )

        # TOTPシークレットを取得
        result = await db.execute(
            select(TOTPSecret).where(
                TOTPSecret.user_id == user.id,
                TOTPSecret.is_enabled == True
            )
        )
        totp_secret = result.scalar_one_or_none()

        if totp_secret:
            secret = decrypt_totp_secret(totp_secret.secret_encrypted)
            if not verify_totp(secret, data.totp_code):
                # バックアップコードを試す
                if totp_secret.backup_codes_hash:
                    valid, index = verify_backup_code(data.totp_code, totp_secret.backup_codes_hash)
                    if valid:
                        # 使用したバックアップコードを無効化
                        codes = list(totp_secret.backup_codes_hash)
                        codes[index] = None
                        totp_secret.backup_codes_hash = codes
                    else:
                        await log_security_event(
                            db, SecurityEventType.TOTP_FAILED,
                            user_id=user.id,
                            username=user.username,
                            ip_address=ip,
                            user_agent=ua
                        )
                        raise HTTPException(
                            status_code=status.HTTP_401_UNAUTHORIZED,
                            detail="2FAコードが正しくありません"
                        )
                else:
                    await log_security_event(
                        db, SecurityEventType.TOTP_FAILED,
                        user_id=user.id,
                        username=user.username,
                        ip_address=ip,
                        user_agent=ua
                    )
                    raise HTTPException(
                        status_code=status.HTTP_401_UNAUTHORIZED,
                        detail="2FAコードが正しくありません"
                    )

    # ログイン成功を記録
    await record_login_attempt(db, data.username, ip, True)

    # 最終ログイン時刻を更新
    user.last_login_at = datetime.now(timezone.utc)

    # セッション作成
    token = create_access_token({"sub": str(user.id)})
    expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.JWT_EXPIRE_MINUTES)
    absolute_expires_at = datetime.now(timezone.utc) + timedelta(hours=settings.SESSION_ABSOLUTE_TIMEOUT_HOURS)

    session = Session(
        user_id=user.id,
        token_hash=hash_token(token),
        expires_at=expires_at,
        absolute_expires_at=absolute_expires_at
    )
    db.add(session)

    # セキュリティログ
    await log_security_event(
        db, SecurityEventType.LOGIN_SUCCESS,
        user_id=user.id,
        username=user.username,
        ip_address=ip,
        user_agent=ua
    )

    # Cookieにセット
    response.set_cookie(
        key=settings.SESSION_COOKIE_NAME,
        value=token,
        httponly=settings.SESSION_COOKIE_HTTPONLY,
        secure=settings.SESSION_COOKIE_SECURE,
        samesite=settings.SESSION_COOKIE_SAMESITE,
        max_age=settings.JWT_EXPIRE_MINUTES * 60
    )

    return TokenResponse(message="ログインしました", username=user.username)


@router.post("/logout")
async def logout(
    request: Request,
    response: Response,
    db: AsyncSession = Depends(get_db)
):
    """ログアウト"""
    token = request.cookies.get(settings.SESSION_COOKIE_NAME)
    if token:
        token_hash = hash_token(token)
        result = await db.execute(
            select(Session).where(Session.token_hash == token_hash)
        )
        session = result.scalar_one_or_none()
        if session:
            session.is_revoked = True
            await log_security_event(
                db, SecurityEventType.LOGOUT,
                user_id=session.user_id,
                ip_address=get_client_ip(request)
            )

    response.delete_cookie(settings.SESSION_COOKIE_NAME)
    return {"message": "ログアウトしました"}


@router.get("/me", response_model=UserResponse)
async def get_me(user: User = Depends(get_current_user)):
    """現在のユーザー情報を取得"""
    return UserResponse(
        id=user.id,
        username=user.username,
        email=user.email,
        is_admin=user.is_admin,
        totp_enabled=user.totp_enabled,
        created_at=user.created_at
    )


@router.get("/csrf", response_model=CSRFResponse)
async def get_csrf_token(
    request: Request,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """CSRFトークンを取得"""
    token = request.cookies.get(settings.SESSION_COOKIE_NAME)
    token_hash = hash_token(token)

    result = await db.execute(
        select(Session).where(Session.token_hash == token_hash)
    )
    session = result.scalar_one_or_none()

    if not session:
        raise HTTPException(status_code=401, detail="セッションが見つかりません")

    # CSRFトークン生成
    csrf_token = generate_csrf_token()
    expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.CSRF_TOKEN_EXPIRE_MINUTES)

    csrf = CSRFToken(
        token_hash=hash_token(csrf_token),
        session_id=session.id,
        expires_at=expires_at
    )
    db.add(csrf)

    return CSRFResponse(csrf_token=csrf_token)


# ========== 2FA Endpoints ==========

@router.post("/totp/setup", response_model=TOTPSetupResponse)
async def setup_totp(
    request: Request,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """2FAセットアップを開始"""
    if user.totp_enabled:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="2FAは既に有効です"
        )

    # 既存のシークレットがあれば削除
    result = await db.execute(
        select(TOTPSecret).where(TOTPSecret.user_id == user.id)
    )
    existing = result.scalar_one_or_none()
    if existing:
        await db.delete(existing)

    # 新しいシークレット生成
    secret = generate_totp_secret()
    backup_codes, backup_hashes = generate_backup_codes()

    totp_secret = TOTPSecret(
        user_id=user.id,
        secret_encrypted=encrypt_totp_secret(secret),
        is_enabled=False,
        backup_codes_hash=backup_hashes
    )
    db.add(totp_secret)

    # QRコード生成
    uri = get_totp_uri(secret, user.username)
    qr = qrcode.QRCode(version=1, box_size=10, border=5)
    qr.add_data(uri)
    qr.make(fit=True)
    img = qr.make_image(fill_color="black", back_color="white")

    buffer = io.BytesIO()
    img.save(buffer, format="PNG")
    qr_base64 = base64.b64encode(buffer.getvalue()).decode()

    return TOTPSetupResponse(
        secret=secret,
        qr_code=f"data:image/png;base64,{qr_base64}",
        backup_codes=backup_codes
    )


@router.post("/totp/verify")
async def verify_totp_setup(
    request: Request,
    data: TOTPVerifyRequest,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """2FAセットアップを完了"""
    result = await db.execute(
        select(TOTPSecret).where(
            TOTPSecret.user_id == user.id,
            TOTPSecret.is_enabled == False
        )
    )
    totp_secret = result.scalar_one_or_none()

    if not totp_secret:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="2FAセットアップが開始されていません"
        )

    secret = decrypt_totp_secret(totp_secret.secret_encrypted)
    if not verify_totp(secret, data.code):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="コードが正しくありません"
        )

    # 2FAを有効化
    totp_secret.is_enabled = True
    totp_secret.verified_at = datetime.now(timezone.utc)
    user.totp_enabled = True

    await log_security_event(
        db, SecurityEventType.TOTP_ENABLED,
        user_id=user.id,
        username=user.username,
        ip_address=get_client_ip(request)
    )

    return {"message": "2FAが有効になりました"}


@router.post("/totp/disable")
async def disable_totp(
    request: Request,
    data: TOTPVerifyRequest,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """2FAを無効化"""
    if not user.totp_enabled:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="2FAは有効ではありません"
        )

    result = await db.execute(
        select(TOTPSecret).where(
            TOTPSecret.user_id == user.id,
            TOTPSecret.is_enabled == True
        )
    )
    totp_secret = result.scalar_one_or_none()

    if not totp_secret:
        raise HTTPException(status_code=400, detail="2FA設定が見つかりません")

    secret = decrypt_totp_secret(totp_secret.secret_encrypted)
    if not verify_totp(secret, data.code):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="コードが正しくありません"
        )

    # 2FAを無効化
    user.totp_enabled = False
    await db.delete(totp_secret)

    await log_security_event(
        db, SecurityEventType.TOTP_DISABLED,
        user_id=user.id,
        username=user.username,
        ip_address=get_client_ip(request)
    )

    return {"message": "2FAが無効になりました"}


# ========== Password Reset Endpoints ==========

@router.post("/password/reset-request")
async def request_password_reset(
    request: Request,
    data: PasswordResetRequestModel,
    db: AsyncSession = Depends(get_db)
):
    """パスワードリセットをリクエスト"""
    ip = get_client_ip(request)

    # Turnstile検証
    if not await verify_turnstile(data.turnstile_token, ip):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="認証に失敗しました"
        )

    # ユーザーを検索（存在しなくても同じレスポンスを返す）
    result = await db.execute(select(User).where(User.email == data.email))
    user = result.scalar_one_or_none()

    if user:
        # トークン生成
        token = generate_secure_token(32)
        expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.PASSWORD_RESET_EXPIRE_MINUTES)

        reset_token = PasswordResetToken(
            user_id=user.id,
            token_hash=hash_token(token),
            expires_at=expires_at
        )
        db.add(reset_token)

        await log_security_event(
            db, SecurityEventType.PASSWORD_RESET_REQUEST,
            user_id=user.id,
            username=user.username,
            ip_address=ip
        )

        # メール送信（本番環境では実装）
        reset_url = f"{settings.SITE_URL}/reset-password?token={token}"
        # TODO: send_email(user.email, "パスワードリセット", reset_url)
        print(f"Password reset URL: {reset_url}")  # 開発用

    # 常に同じレスポンス（タイミング攻撃対策）
    return {"message": "パスワードリセットのメールを送信しました（登録済みの場合）"}


@router.post("/password/reset-confirm")
async def confirm_password_reset(
    request: Request,
    data: PasswordResetConfirmModel,
    db: AsyncSession = Depends(get_db)
):
    """パスワードリセットを実行"""
    ip = get_client_ip(request)

    # Turnstile検証
    if not await verify_turnstile(data.turnstile_token, ip):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="認証に失敗しました"
        )

    # トークン検証
    token_hash = hash_token(data.token)
    now = datetime.now(timezone.utc)

    result = await db.execute(
        select(PasswordResetToken).where(
            PasswordResetToken.token_hash == token_hash,
            PasswordResetToken.expires_at > now,
            PasswordResetToken.used_at.is_(None)
        )
    )
    reset_token = result.scalar_one_or_none()

    if not reset_token:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="無効または期限切れのトークンです"
        )

    # ユーザー取得
    result = await db.execute(select(User).where(User.id == reset_token.user_id))
    user = result.scalar_one_or_none()

    if not user:
        raise HTTPException(status_code=400, detail="ユーザーが見つかりません")

    # パスワード更新
    user.password_hash = hash_password(data.new_password)
    reset_token.used_at = now

    # 全セッションを無効化
    result = await db.execute(
        select(Session).where(Session.user_id == user.id, Session.is_revoked == False)
    )
    sessions = result.scalars().all()
    for session in sessions:
        session.is_revoked = True

    await log_security_event(
        db, SecurityEventType.PASSWORD_RESET_SUCCESS,
        user_id=user.id,
        username=user.username,
        ip_address=ip
    )

    return {"message": "パスワードが更新されました。再ログインしてください。"}


@router.post("/password/change")
async def change_password(
    request: Request,
    data: ChangePasswordRequest,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """パスワードを変更"""
    if not verify_password(data.current_password, user.password_hash):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="現在のパスワードが正しくありません"
        )

    user.password_hash = hash_password(data.new_password)

    await log_security_event(
        db, SecurityEventType.PASSWORD_CHANGE,
        user_id=user.id,
        username=user.username,
        ip_address=get_client_ip(request)
    )

    return {"message": "パスワードが変更されました"}


# ========== API Key Management ==========

class CreateAPIKeyRequest(BaseModel):
    name: str


class APIKeyResponse(BaseModel):
    id: int
    name: str
    key_prefix: str
    is_active: bool
    created_at: datetime
    last_used_at: Optional[datetime]


class APIKeyCreatedResponse(BaseModel):
    id: int
    name: str
    api_key: str  # 作成時のみ平文を返す
    message: str


@router.post("/api-keys", response_model=APIKeyCreatedResponse)
async def create_api_key(
    request: Request,
    data: CreateAPIKeyRequest,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """新しいAPIキーを作成"""
    # 名前のバリデーション
    if not data.name or len(data.name) > 100:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="APIキー名は1〜100文字で入力してください"
        )

    # キー生成
    api_key, key_hash = generate_api_key()

    new_key = APIKey(
        user_id=user.id,
        name=data.name,
        key_hash=key_hash,
        key_prefix=api_key[:12]  # ton_XXXXXXXX
    )
    db.add(new_key)
    await db.flush()

    await log_security_event(
        db, SecurityEventType.API_KEY_CREATED,
        user_id=user.id,
        username=user.username,
        ip_address=get_client_ip(request),
        details={"key_name": data.name}
    )

    return APIKeyCreatedResponse(
        id=new_key.id,
        name=new_key.name,
        api_key=api_key,
        message="APIキーが作成されました。このキーは一度だけ表示されます。安全に保管してください。"
    )


@router.get("/api-keys", response_model=list[APIKeyResponse])
async def list_api_keys(
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """ユーザーのAPIキー一覧を取得"""
    result = await db.execute(
        select(APIKey).where(APIKey.user_id == user.id).order_by(APIKey.created_at.desc())
    )
    keys = result.scalars().all()

    return [
        APIKeyResponse(
            id=key.id,
            name=key.name,
            key_prefix=key.key_prefix,
            is_active=key.is_active,
            created_at=key.created_at,
            last_used_at=key.last_used_at
        )
        for key in keys
    ]


@router.delete("/api-keys/{key_id}")
async def revoke_api_key(
    request: Request,
    key_id: int,
    user: User = Depends(get_current_user),
    db: AsyncSession = Depends(get_db)
):
    """APIキーを無効化"""
    result = await db.execute(
        select(APIKey).where(
            APIKey.id == key_id,
            APIKey.user_id == user.id
        )
    )
    api_key = result.scalar_one_or_none()

    if not api_key:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="APIキーが見つかりません"
        )

    api_key.is_active = False

    await log_security_event(
        db, SecurityEventType.API_KEY_REVOKED,
        user_id=user.id,
        username=user.username,
        ip_address=get_client_ip(request),
        details={"key_name": api_key.name}
    )

    return {"message": "APIキーが無効化されました"}
