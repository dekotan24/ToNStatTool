# ToN Stats Server セットアップ手順書

## 前提条件

- Python 3.9以上
- PostgreSQL 13以上
- nginx
- Cloudflare アカウント（Turnstile用）

---

## 1. PostgreSQL セットアップ

### 1.1 データベースとユーザーの作成

```bash
# PostgreSQLにログイン
sudo -u postgres psql

# データベース作成
CREATE DATABASE ton_stats;

# ユーザー作成（パスワードは変更してください）
CREATE USER ton_user WITH ENCRYPTED PASSWORD 'your_secure_password_here';

# 権限付与
GRANT ALL PRIVILEGES ON DATABASE ton_stats TO ton_user;

# PostgreSQLから抜ける
\q
```

### 1.2 スキーマの適用

```bash
# スキーマを適用
psql -U ton_user -d ton_stats -f sql/001_schema.sql
```

---

## 2. Python環境セットアップ

### 2.1 仮想環境の作成

```bash
cd server
python -m venv venv

# Linux/Mac
source venv/bin/activate

# Windows
venv\Scripts\activate
```

### 2.2 依存関係のインストール

```bash
pip install -r requirements.txt
```

---

## 3. 環境設定

### 3.1 .envファイルの作成

`server/.env` ファイルを作成し、以下を設定：

```env
# データベース設定
DATABASE_URL=postgresql://ton_user:your_secure_password_here@localhost:5432/ton_stats

# セキュリティ設定（必ず変更してください）
SECRET_KEY=your-very-long-and-random-secret-key-here-at-least-32-chars
JWT_ALGORITHM=HS256
JWT_EXPIRE_MINUTES=1440

# Cloudflare Turnstile設定
TURNSTILE_SITE_KEY=your-turnstile-site-key
TURNSTILE_SECRET_KEY=your-turnstile-secret-key

# サーバー設定
HOST=127.0.0.1
PORT=8000
DEBUG=false

# CORS設定（本番環境では適切なドメインを指定）
ALLOWED_ORIGINS=https://your-domain.com
```

### 3.2 SECRET_KEYの生成

```python
import secrets
print(secrets.token_hex(32))
```

---

## 4. Cloudflare Turnstile 設定

1. [Cloudflare Dashboard](https://dash.cloudflare.com/) にログイン
2. サイドバーから「Turnstile」を選択
3. 「サイトを追加」をクリック
4. サイト名とドメインを入力
5. ウィジェットタイプ:「Managed」を選択
6. 作成後、Site KeyとSecret Keyを.envに設定

---

## 5. nginx設定

### 5.1 設定ファイルの配置

`nginx/ton_stats.conf` を nginx の設定ディレクトリにコピー：

```bash
# Linux
sudo cp nginx/ton_stats.conf /etc/nginx/sites-available/ton_stats
sudo ln -s /etc/nginx/sites-available/ton_stats /etc/nginx/sites-enabled/

# 設定テスト
sudo nginx -t

# nginx再起動
sudo systemctl reload nginx
```

### 5.2 SSL証明書（Let's Encrypt）

```bash
sudo certbot --nginx -d your-domain.com
```

---

## 6. アプリケーション起動

### 6.1 開発環境

```bash
cd server
source venv/bin/activate
python main.py
```

### 6.2 本番環境（systemdサービス）

`/etc/systemd/system/ton_stats.service` を作成：

```ini
[Unit]
Description=ToN Stats API Server
After=network.target postgresql.service

[Service]
Type=simple
User=www-data
WorkingDirectory=/path/to/server
Environment="PATH=/path/to/server/venv/bin"
ExecStart=/path/to/server/venv/bin/uvicorn main:app --host 127.0.0.1 --port 8000
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable ton_stats
sudo systemctl start ton_stats
```

---

## 7. 管理者アカウントの作成

初期管理者アカウントを作成：

```bash
cd server
source venv/bin/activate
python scripts/create_admin.py
```

または手動でSQL実行：

```sql
-- パスワードは bcrypt でハッシュ化する必要があります
-- Python: from passlib.hash import bcrypt; print(bcrypt.hash("your_password"))
INSERT INTO users (username, email, password_hash, is_admin)
VALUES ('admin', 'admin@example.com', '$2b$12$...hashed_password...', TRUE);
```

---

## 8. 動作確認

```bash
# ヘルスチェック
curl http://localhost:8000/api/v1/health

# 期待される応答
{"status":"ok","timestamp":"..."}
```

---

## トラブルシューティング

### データベース接続エラー
- PostgreSQLが起動しているか確認: `sudo systemctl status postgresql`
- 接続情報が正しいか確認
- pg_hba.conf でローカル接続が許可されているか確認

### 502 Bad Gateway
- アプリケーションが起動しているか確認: `sudo systemctl status ton_stats`
- ログを確認: `journalctl -u ton_stats -f`

### Turnstileエラー
- Site KeyとSecret Keyが正しいか確認
- ドメインがTurnstile設定に登録されているか確認
