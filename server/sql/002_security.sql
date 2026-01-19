-- ToN Stats Security Schema
-- PostgreSQL 13+

-- APIキーテーブル（クライアントアプリ認証用）
CREATE TABLE IF NOT EXISTS api_keys (
    id SERIAL PRIMARY KEY,
    key_hash VARCHAR(64) UNIQUE NOT NULL,       -- SHA256ハッシュ
    name VARCHAR(100) NOT NULL,                  -- キーの説明
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_used_at TIMESTAMP WITH TIME ZONE,
    expires_at TIMESTAMP WITH TIME ZONE          -- NULL = 無期限
);

-- ログイン試行テーブル（ブルートフォース対策）
CREATE TABLE IF NOT EXISTS login_attempts (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) NOT NULL,
    ip_address VARCHAR(45) NOT NULL,            -- IPv6対応
    success BOOLEAN NOT NULL,
    attempted_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- アカウントロックテーブル
CREATE TABLE IF NOT EXISTS account_locks (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    locked_until TIMESTAMP WITH TIME ZONE NOT NULL,
    lock_reason VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- セキュリティイベントログテーブル
CREATE TABLE IF NOT EXISTS security_logs (
    id SERIAL PRIMARY KEY,
    event_type VARCHAR(50) NOT NULL,            -- LOGIN_SUCCESS, LOGIN_FAILED, LOGOUT, etc.
    user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
    username VARCHAR(50),                        -- ユーザーが存在しない場合も記録
    ip_address VARCHAR(45),
    user_agent TEXT,
    details JSONB,                               -- 追加情報
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- パスワードリセットトークンテーブル
CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(64) UNIQUE NOT NULL,     -- SHA256ハッシュ
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    used_at TIMESTAMP WITH TIME ZONE,           -- 使用済みの場合
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- TOTP（2FA）シークレットテーブル
CREATE TABLE IF NOT EXISTS totp_secrets (
    id SERIAL PRIMARY KEY,
    user_id INTEGER UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    secret_encrypted VARCHAR(255) NOT NULL,     -- 暗号化されたシークレット
    is_enabled BOOLEAN DEFAULT FALSE,           -- 有効化済みか
    backup_codes_hash TEXT[],                   -- バックアップコードのハッシュ配列
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    verified_at TIMESTAMP WITH TIME ZONE        -- 初回検証日時
);

-- CSRFトークンテーブル
CREATE TABLE IF NOT EXISTS csrf_tokens (
    id SERIAL PRIMARY KEY,
    token_hash VARCHAR(64) UNIQUE NOT NULL,
    session_id INTEGER REFERENCES sessions(id) ON DELETE CASCADE,
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- usersテーブルに2FA関連カラムを追加
ALTER TABLE users ADD COLUMN IF NOT EXISTS totp_enabled BOOLEAN DEFAULT FALSE;
ALTER TABLE users ADD COLUMN IF NOT EXISTS absolute_session_timeout_at TIMESTAMP WITH TIME ZONE;

-- sessionsテーブルに絶対タイムアウトを追加
ALTER TABLE sessions ADD COLUMN IF NOT EXISTS absolute_expires_at TIMESTAMP WITH TIME ZONE;

-- インデックス
CREATE INDEX IF NOT EXISTS idx_api_keys_hash ON api_keys(key_hash);
CREATE INDEX IF NOT EXISTS idx_api_keys_user ON api_keys(user_id);
CREATE INDEX IF NOT EXISTS idx_login_attempts_username ON login_attempts(username);
CREATE INDEX IF NOT EXISTS idx_login_attempts_ip ON login_attempts(ip_address);
CREATE INDEX IF NOT EXISTS idx_login_attempts_time ON login_attempts(attempted_at);
CREATE INDEX IF NOT EXISTS idx_account_locks_username ON account_locks(username);
CREATE INDEX IF NOT EXISTS idx_security_logs_type ON security_logs(event_type);
CREATE INDEX IF NOT EXISTS idx_security_logs_user ON security_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_security_logs_time ON security_logs(created_at);
CREATE INDEX IF NOT EXISTS idx_password_reset_tokens_hash ON password_reset_tokens(token_hash);
CREATE INDEX IF NOT EXISTS idx_csrf_tokens_hash ON csrf_tokens(token_hash);

-- 古いログイン試行を削除する関数（定期実行用）
CREATE OR REPLACE FUNCTION cleanup_old_login_attempts()
RETURNS void AS $$
BEGIN
    DELETE FROM login_attempts WHERE attempted_at < NOW() - INTERVAL '24 hours';
    DELETE FROM account_locks WHERE locked_until < NOW();
    DELETE FROM csrf_tokens WHERE expires_at < NOW();
    DELETE FROM password_reset_tokens WHERE expires_at < NOW() AND used_at IS NULL;
END;
$$ LANGUAGE plpgsql;
