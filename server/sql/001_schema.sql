-- ToN Stats Database Schema
-- PostgreSQL 13+

-- ユーザーテーブル
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    email VARCHAR(255) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    is_admin BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_login_at TIMESTAMP WITH TIME ZONE
);

-- セッションテーブル（JWT無効化用）
CREATE TABLE IF NOT EXISTS sessions (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    token_hash VARCHAR(64) NOT NULL,
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    is_revoked BOOLEAN DEFAULT FALSE
);

-- インスタンステーブル
CREATE TABLE IF NOT EXISTS instances (
    id SERIAL PRIMARY KEY,
    instance_id VARCHAR(500) UNIQUE NOT NULL,  -- wrld_xxx~region~nonce
    world_id VARCHAR(100),                      -- wrld_xxx
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_activity_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    total_rounds INTEGER DEFAULT 0
);

-- ラウンドテーブル
CREATE TABLE IF NOT EXISTS rounds (
    id SERIAL PRIMARY KEY,
    instance_id INTEGER REFERENCES instances(id) ON DELETE CASCADE,
    fingerprint VARCHAR(64) UNIQUE NOT NULL,    -- 重複排除用ハッシュ
    round_type VARCHAR(50) NOT NULL,
    map_name VARCHAR(100),
    terrors TEXT[],                              -- テラー名の配列
    started_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    player_count INTEGER DEFAULT 0,
    survivor_count INTEGER DEFAULT 0
);

-- テラー統計テーブル（集計用）
CREATE TABLE IF NOT EXISTS terror_stats (
    id SERIAL PRIMARY KEY,
    terror_name VARCHAR(100) UNIQUE NOT NULL,
    encounter_count INTEGER DEFAULT 0,
    total_rounds INTEGER DEFAULT 0,
    total_survivors INTEGER DEFAULT 0,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- ラウンドタイプ統計テーブル（集計用）
CREATE TABLE IF NOT EXISTS round_type_stats (
    id SERIAL PRIMARY KEY,
    round_type VARCHAR(50) UNIQUE NOT NULL,
    occurrence_count INTEGER DEFAULT 0,
    total_players INTEGER DEFAULT 0,
    total_survivors INTEGER DEFAULT 0,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- マップ統計テーブル（集計用）
CREATE TABLE IF NOT EXISTS map_stats (
    id SERIAL PRIMARY KEY,
    map_name VARCHAR(100) UNIQUE NOT NULL,
    occurrence_count INTEGER DEFAULT 0,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- インデックス
CREATE INDEX IF NOT EXISTS idx_sessions_user_id ON sessions(user_id);
CREATE INDEX IF NOT EXISTS idx_sessions_token_hash ON sessions(token_hash);
CREATE INDEX IF NOT EXISTS idx_sessions_expires_at ON sessions(expires_at);
CREATE INDEX IF NOT EXISTS idx_instances_instance_id ON instances(instance_id);
CREATE INDEX IF NOT EXISTS idx_instances_last_activity ON instances(last_activity_at);
CREATE INDEX IF NOT EXISTS idx_rounds_instance_id ON rounds(instance_id);
CREATE INDEX IF NOT EXISTS idx_rounds_fingerprint ON rounds(fingerprint);
CREATE INDEX IF NOT EXISTS idx_rounds_started_at ON rounds(started_at);
CREATE INDEX IF NOT EXISTS idx_rounds_round_type ON rounds(round_type);
CREATE INDEX IF NOT EXISTS idx_terror_stats_name ON terror_stats(terror_name);

-- 更新日時自動更新用トリガー関数
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- トリガー設定
DROP TRIGGER IF EXISTS update_users_updated_at ON users;
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_terror_stats_updated_at ON terror_stats;
CREATE TRIGGER update_terror_stats_updated_at
    BEFORE UPDATE ON terror_stats
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_round_type_stats_updated_at ON round_type_stats;
CREATE TRIGGER update_round_type_stats_updated_at
    BEFORE UPDATE ON round_type_stats
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_map_stats_updated_at ON map_stats;
CREATE TRIGGER update_map_stats_updated_at
    BEFORE UPDATE ON map_stats
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
