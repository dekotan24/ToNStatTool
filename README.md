# 🎮 ToNStatTool

**Terror of Nowhere** のリアルタイム統計トラッキングツール

[![Windows](https://img.shields.io/badge/Platform-Windows-0078d4?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![.NET Framework](https://img.shields.io/badge/.NET_Framework-4.8.1-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)
[![GitHub release](https://img.shields.io/github/v/release/dekotan24/ToNStatTool?style=flat-square&logo=github)](https://github.com/dekotan24/ToNStatTool/releases)

[English](README_en.md) | 日本語

![Screenshot](https://raw.githubusercontent.com/dekotan24/ToNStatTool/main/docs/screenshot.png)

---

## ✨ 特徴

ToNStatToolは、VRChatワールド「**Terror of Nowhere**」のゲームデータをリアルタイムで追跡・表示するWindowsアプリケーションです。[ToNSaveManager](https://github.com/ChrisFeline/ToNSaveManager)のWebSocket APIを利用して動作します。

### 🎯 主な機能

| 機能 | 説明 |
|------|------|
| 🔮 **次ラウンド予測** | ラウンド周期に基づいて次のラウンドタイプを予測 |
| 👻 **テラー情報表示** | 現在のテラーをアイコン付きでリアルタイム表示 |
| 📊 **統計トラッキング** | ラウンド・テラーの遭遇統計を自動記録 |
| 👥 **プレイヤー管理** | 参加プレイヤーの生存状態をリアルタイム表示 |
| ⚠️ **警告システム** | 特定ユーザーの参加を通知・警告 |
| 🔔 **アイテムリマインダー** | 8ページ/アンバウンド終了後にアイテム装備を通知 |
| 🎨 **テーマ切替** | ダーク/ライトテーマに対応 |

---

## 🚀 クイックスタート

### 必要条件

- Windows 10/11
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/download/dotnet-framework)
- [ToNSaveManager](https://github.com/ChrisFeline/ToNSaveManager)（WebSocket API有効化済み）

### インストール

1. [最新リリース](https://github.com/dekotan24/ToNStatTool/releases)から`ToNStatTool_vx.x.zip`をダウンロード
2. 任意のフォルダに解凍

### 起動手順

```
1. ToNSaveManagerを起動し、WebSocket APIサーバを有効化
2. `ToNStatTool.exe`を起動
3. 「接続」ボタンをクリック
```

---

## 📖 機能詳細

### 🔮 次ラウンド予測

ゲーム内のラウンド周期を分析し、次のラウンドタイプを予測します：

- **通常** - 次は通常ラウンド（Classic/RUN）
- **特殊** - 次は特殊ラウンド（Alternate, Punished等）
- **通常 or 特殊** - どちらの可能性もあり
- **Moon系** - 解禁条件を満たした場合の予測

> ⚠️ 予測は確率に基づくもので、100%正確ではありません

### 👻 テラー情報

現在のテラーを以下の情報とともに表示：

| アイコン | 意味 |
|---------|------|
| 🟢 | スタン可能 |
| 🟡 | 条件付きスタン |
| 🔴 | スタン厳禁 |
| ⚪ | スタン効果なし |
| 🟣 | スタン可否不明 |

**特性アイコン:**
- ➡️ 追跡型 | 🔄 徘徊型 | ⚡ テレポート | 💀 即死
- ➕ 召喚 | ⬇️ デバフ | ↩️ カウンター | ••• 複数体

### 👥 プレイヤー管理

- リアルタイムの生存/死亡状態表示
- 総人数・生存者数のカウント
- ダブルクリックで警告リストに追加/削除

### ⚠️ 警告ユーザー機能

特定ユーザーの参加を検知して通知：

- 音声アラート
- ウィンドウタイトル通知
- オレンジ色でハイライト表示

設定方法：
1. `warn_user.txt`に1行1名でユーザー名を記入
2. またはプレイヤー一覧でダブルクリック

### 🔔 アイテムリマインダー

以下のタイミングでアイテム装備忘れを通知：

- **8ページ / アンバウンド終了後** - アイテムが没収されるラウンド終了時
- **リスポーン後の再参加時** - ゲームに再参加した際
- **サボタージュでキラー側になった時**

テラー表示ウィンドウに「アイテムを持ち直してください」と表示されます。

### 📊 統計・ログ

- **セッション統計** - 生存数、死亡数、スタン回数、ダメージ
- **ラウンド統計** - ラウンドタイプ別の回数と確率
- **テラー統計** - テラー別の遭遇回数
- **ラウンドログ** - 時刻、ラウンド種別、マップ、テラー、結果

### 🌙 インスタンス状態管理

Moon系ラウンドの予測精度を上げるための手動設定：

- 鳥の遭遇状況（Big Bird, Judgement Bird, Punishing Bird）
- Moon解禁状況（Blood Moon, Twilight, Mystic Moon, Solstice）
- 推定生存カウント

---

## ⚙️ 設定

### WebSocket接続

デフォルトURL: `ws://localhost:11398`

ToNSaveManagerのポート設定を変更している場合は、接続URLを調整してください。

### サウンド設定

「🔊 サウンド設定」から以下を設定可能：

| サウンド | 説明 |
|---------|------|
| プレイヤー参加 | 誰かが参加した時 |
| プレイヤー退出 | 誰かが退出した時 |
| 警告ユーザー参加 | 警告リストのユーザーが参加した時 |
| マスター変更 | インスタンスマスターが変更された時 |
| アイテムリマインダー | アイテム装備通知時 |

対応形式: MP3, WAV

### テーマ

設定画面からダーク/ライトテーマを切り替え可能。

---

## 📁 ファイル構成

```
ToNStatTool/
├── ToNStatTool.exe       # メイン実行ファイル
├── terrorsInfo.json      # テラー情報データベース
├── warn_user.txt         # 警告ユーザーリスト
├── settings.json         # アプリ設定（自動生成）
├── sound_settings.json   # サウンド設定（自動生成）
├── warning.mp3           # 警告音（オプション）
├── masterchange.mp3      # インスタンスマスター変更通知音（オプション）
├── item.mp3              # アイテムリマインダー通知音（オプション）
├── player_join.mp3       # プレイヤー参加通知音（オプション）
├── player_leave.mp3      # プレイヤー退出通知音（オプション）
└── licenses/             # ライセンスファイル
```

### テラー情報の更新

最新の`terrorsInfo.json`は以下のリポジトリから取得できます：

🔗 [ToNRoundCounter - lovetwice1012/ToNRoundCounter](https://github.com/lovetwice1012/ToNRoundCounter)

---

## 🔧 トラブルシューティング

### 接続できない

- ToNSaveManagerが起動しているか確認
- WebSocket APIサーバが有効か確認
- URLが正しいか確認（デフォルト: `ws://localhost:11398`）

### テラー情報が表示されない

- `terrorsInfo.json`が同じフォルダにあるか確認
- JSONファイルの形式が正しいか確認

### プレイヤーが表示されない

- 一部の特殊ラウンドでは追跡できない場合があります
- インスタンスに参加してからラウンドが開始すると反映されます

### 次ラウンド予測が正しくない

- インスタンス途中参加直後の場合、予測精度が下がります

---

## 🤝 コントリビュート

バグ報告・機能要望は[Issues](https://github.com/dekotan24/ToNStatTool/issues)へ！

> ⚠️ **重要**: 本ツールに関する質問やバグ報告は、**必ず本リポジトリのIssue**にお願いします。
> 
> **Beyond氏**（ToNワールド作者）や**ChrisFeline氏**（ToNSaveManager作者）への問い合わせは絶対にしないでください。本ツールは個人が作成した非公式ツールであり、彼らとは無関係です。

---

## 📜 ライセンス

このプロジェクトはデュアルライセンスです：

| 対象 | ライセンス |
|------|-----------|
| ソースコード | MIT License |
| JSONデータファイル | ToNRoundCounter License |
| サウンドファイル | CC BY 4.0 |

詳細は[LICENSE](LICENSE)ファイルと`licenses/`ディレクトリを参照してください。

---

## 🙏 クレジット

- **terrorsInfo.json**: yussy - [ToNRoundCounter](https://github.com/lovetwice1012/ToNRoundCounter)
- **サウンド素材**: [OtoLogic](https://otologic.jp) (CC BY 4.0)
- **ToNSaveManager**: [ChrisFeline](https://github.com/ChrisFeline/ToNSaveManager)
- **開発支援**: [Claude](https://claude.ai) (Opus 4.5)

---

**不正はせずに、楽しくプレイしましょう。**
