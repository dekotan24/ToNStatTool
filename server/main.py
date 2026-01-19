"""ToN Stats Server - Main Application"""
from datetime import datetime, timezone
from pathlib import Path

from fastapi import FastAPI, Request, Depends
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from fastapi.responses import HTMLResponse, RedirectResponse
from fastapi.middleware.cors import CORSMiddleware
from starlette.middleware.base import BaseHTTPMiddleware
from slowapi import Limiter, _rate_limit_exceeded_handler
from slowapi.util import get_remote_address
from slowapi.errors import RateLimitExceeded

from app.core.config import settings
from app.core.database import init_db
from app.api import auth_router, events_router, stats_router
from app.api.auth import get_current_user, get_optional_user
from app.models import User


# ========== Security Headers Middleware ==========

class SecurityHeadersMiddleware(BaseHTTPMiddleware):
    """セキュリティヘッダーを追加するミドルウェア"""

    async def dispatch(self, request: Request, call_next):
        response = await call_next(request)

        # Content Security Policy
        csp_directives = [
            "default-src 'self'",
            "script-src 'self' 'unsafe-inline' https://challenges.cloudflare.com",
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: blob:",
            "font-src 'self'",
            "connect-src 'self'",
            "frame-src https://challenges.cloudflare.com",
            "frame-ancestors 'none'",
            "form-action 'self'",
            "base-uri 'self'",
            "object-src 'none'"
        ]
        response.headers["Content-Security-Policy"] = "; ".join(csp_directives)

        # その他のセキュリティヘッダー
        response.headers["X-Content-Type-Options"] = "nosniff"
        response.headers["X-Frame-Options"] = "DENY"
        response.headers["X-XSS-Protection"] = "1; mode=block"
        response.headers["Referrer-Policy"] = "strict-origin-when-cross-origin"
        response.headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()"

        # HSTSはHTTPS環境でのみ
        if not settings.DEBUG:
            response.headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains"

        return response

# パスの設定
BASE_DIR = Path(__file__).resolve().parent
TEMPLATES_DIR = BASE_DIR / "templates"
STATIC_DIR = BASE_DIR / "static"

# レート制限
limiter = Limiter(key_func=get_remote_address)

# アプリケーション作成
app = FastAPI(
    title="ToN Stats API",
    description="Terror of Nowhere Statistics API",
    version="1.0.0",
    docs_url="/docs" if settings.DEBUG else None,
    redoc_url="/redoc" if settings.DEBUG else None
)

# レート制限エラーハンドラー
app.state.limiter = limiter
app.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)

# CORS設定
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.ALLOWED_ORIGINS,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# セキュリティヘッダーミドルウェア
app.add_middleware(SecurityHeadersMiddleware)

# 静的ファイル
app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")

# テンプレート
templates = Jinja2Templates(directory=TEMPLATES_DIR)


# ========== Startup ==========

@app.on_event("startup")
async def startup():
    """アプリケーション起動時の処理"""
    await init_db()


# ========== API Routes ==========

app.include_router(auth_router)
app.include_router(events_router)
app.include_router(stats_router)


# ========== Health Check ==========

@app.get("/api/v1/health")
async def health_check():
    """ヘルスチェック"""
    return {
        "status": "ok",
        "timestamp": datetime.now(timezone.utc).isoformat()
    }


# ========== Page Routes ==========

@app.get("/", response_class=HTMLResponse)
async def index(request: Request, user: User = Depends(get_optional_user)):
    """トップページ"""
    if not user:
        return RedirectResponse(url="/login", status_code=302)
    return templates.TemplateResponse(
        "index.html",
        {
            "request": request,
            "user": user,
            "turnstile_site_key": settings.TURNSTILE_SITE_KEY
        }
    )


@app.get("/login", response_class=HTMLResponse)
async def login_page(request: Request, user: User = Depends(get_optional_user)):
    """ログインページ"""
    if user:
        return RedirectResponse(url="/", status_code=302)
    return templates.TemplateResponse(
        "login.html",
        {
            "request": request,
            "turnstile_site_key": settings.TURNSTILE_SITE_KEY
        }
    )


@app.get("/register", response_class=HTMLResponse)
async def register_page(request: Request, user: User = Depends(get_optional_user)):
    """登録ページ"""
    if user:
        return RedirectResponse(url="/", status_code=302)
    return templates.TemplateResponse(
        "register.html",
        {
            "request": request,
            "turnstile_site_key": settings.TURNSTILE_SITE_KEY
        }
    )


@app.get("/dashboard", response_class=HTMLResponse)
async def dashboard(request: Request, user: User = Depends(get_current_user)):
    """ダッシュボード"""
    return templates.TemplateResponse(
        "dashboard.html",
        {
            "request": request,
            "user": user
        }
    )


@app.get("/terrors", response_class=HTMLResponse)
async def terrors_page(request: Request, user: User = Depends(get_current_user)):
    """テラー統計ページ"""
    return templates.TemplateResponse(
        "terrors.html",
        {
            "request": request,
            "user": user
        }
    )


@app.get("/rounds", response_class=HTMLResponse)
async def rounds_page(request: Request, user: User = Depends(get_current_user)):
    """ラウンド統計ページ"""
    return templates.TemplateResponse(
        "rounds.html",
        {
            "request": request,
            "user": user
        }
    )


# ========== Error Handlers ==========

@app.exception_handler(401)
async def unauthorized_handler(request: Request, exc):
    """未認証エラーハンドラー"""
    if request.url.path.startswith("/api/"):
        return {"detail": "認証が必要です"}
    return RedirectResponse(url="/login", status_code=302)


@app.exception_handler(404)
async def not_found_handler(request: Request, exc):
    """404エラーハンドラー"""
    if request.url.path.startswith("/api/"):
        return {"detail": "Not Found"}
    return templates.TemplateResponse(
        "404.html",
        {"request": request},
        status_code=404
    )


# ========== robots.txt ==========

@app.get("/robots.txt")
async def robots():
    """robots.txt - クロール禁止"""
    return HTMLResponse(
        content="User-agent: *\nDisallow: /\n",
        media_type="text/plain"
    )


# ========== Main ==========

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG
    )
