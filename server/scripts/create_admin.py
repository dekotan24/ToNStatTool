#!/usr/bin/env python3
"""
管理者ユーザー作成スクリプト

使用方法:
    python scripts/create_admin.py

環境変数 DATABASE_URL が設定されている必要があります。
"""
import asyncio
import getpass
import sys
import os

# プロジェクトルートをパスに追加
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from sqlalchemy import select
from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession, async_sessionmaker

from app.core.config import settings
from app.core.security import hash_password
from app.models import User


async def create_admin():
    """対話形式で管理者ユーザーを作成"""
    print("=" * 50)
    print("ToN Stats - Admin User Creation")
    print("=" * 50)
    print()

    # ユーザー名入力
    while True:
        username = input("Username: ").strip()
        if len(username) < 3:
            print("Error: Username must be at least 3 characters")
            continue
        if not username.replace("_", "").replace("-", "").isalnum():
            print("Error: Username can only contain letters, numbers, _ and -")
            continue
        break

    # メールアドレス入力
    while True:
        email = input("Email: ").strip()
        if "@" not in email:
            print("Error: Invalid email address")
            continue
        break

    # パスワード入力
    while True:
        password = getpass.getpass("Password: ")
        if len(password) < 8:
            print("Error: Password must be at least 8 characters")
            continue

        confirm = getpass.getpass("Confirm Password: ")
        if password != confirm:
            print("Error: Passwords do not match")
            continue
        break

    print()
    print(f"Creating admin user: {username}")
    print(f"Email: {email}")
    print()

    confirm = input("Proceed? (y/N): ").strip().lower()
    if confirm != "y":
        print("Cancelled.")
        return

    # データベース接続
    engine = create_async_engine(settings.DATABASE_URL)
    async_session = async_sessionmaker(engine, class_=AsyncSession)

    async with async_session() as session:
        # 重複チェック
        result = await session.execute(
            select(User).where(User.username == username)
        )
        if result.scalar_one_or_none():
            print(f"Error: Username '{username}' already exists")
            return

        result = await session.execute(
            select(User).where(User.email == email)
        )
        if result.scalar_one_or_none():
            print(f"Error: Email '{email}' already exists")
            return

        # ユーザー作成
        user = User(
            username=username,
            email=email,
            password_hash=hash_password(password),
            is_admin=True,
            is_active=True
        )
        session.add(user)
        await session.commit()

        print()
        print("=" * 50)
        print(f"Admin user '{username}' created successfully!")
        print("=" * 50)


if __name__ == "__main__":
    asyncio.run(create_admin())
