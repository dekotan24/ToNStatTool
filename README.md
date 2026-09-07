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
| 🔮 **次ラウンド予測** | ラウンド周期・Moon解禁条件に基づいて次のラウンドタイプを予測 |
| 👻 **テラー情報表示** | 現在のテラーをスタン可否・特性アイコン付きでリアルタイム表示 |
| 📊 **統計トラッキング** | セッション統計・ラウンド/テラー別の遭遇統計を自動記録 |
| 📝 **ラウンドログ** | 全ラウンドの履歴を記録。フィルタ・CSV出力対応 |
| 🌐 **インスタンス種別表示** | Public / Friends+ / Invite / Group+ などの種別とリージョンを自動判定 |
| 🖱️ **オーバーレイ操作** | 位置・サイズの記憶、クリックスルー、グローバルホットキー |
| 👥 **プレイヤー管理** | 参加プレイヤーの生存状態をリアルタイム表示 |
| ⚠️ **警告システム** | 特定ユーザーの参加を音とハイライトで通知 |
| 🔔 **アイテムリマインダー** | 8ページ/パニッシュ終了後などにアイテム装備を通知 |
| 🥽 **VR通知** | XSOverlay連携でVRヘッドセット内にプッシュ通知 |
| ☁️ **クラウド同期** | ラウンド情報の送信と、過去ログ・インスタンス状態の取得 |
| 💾 **セーブコード表示** | 受信した最近のセーブコードをワンクリックで確認 |
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
2. `ToNStatTool.exe`を起動（起動時に自動接続を試行します）
3. 接続されない場合は「接続」ボタンをクリック
```

---

## 📖 機能詳細

### 🔮 次ラウンド予測

ゲーム内のラウンド周期を分析し、次のラウンドタイプを予測します：

- **通常** - 次は通常ラウンド（Classic/RUN）
- **特殊** - 次は特殊ラウンド（Alternate, Punished等）
- **通常 or 特殊** - どちらの可能性もあり
- **Moon系** - 解禁条件（3鳥遭遇・生存数・Midnight生存など）を満たした場合の予測
- **特殊 (マスター変更)** - インスタンスマスター変更による特殊確定

> ⚠️ 予測は確率に基づくもので、100%正確ではありません。
> 精度を上げたい場合は「インスタンス状態設定」を手動で調整してください（後述）。

### 👻 テラー情報

現在のテラーを、スタン可否・行動特性・テラー画像とともに表示します。
コンパクトな「テラー表示ウィンドウ」（透明度調整可）を出しておけば、配信画面やサブモニタでの確認にも便利です。

**スタン可否アイコン:**

| アイコン | 意味 |
|:---:|------|
| ![スタン可能](docs/stun/safe.png) | スタン可能 |
| ![条件付きスタン](docs/stun/caution.png) | 条件付きスタン（注意が必要） |
| ![スタン厳禁](docs/stun/forbidden.png) | スタン厳禁 |
| ![スタン効果なし](docs/stun/ineffective.png) | スタン効果なし |
| ![スタン可否不明](docs/stun/unknown.png) | スタン可否不明 |

**特性アイコン:**

テラーの行動特性を色付きバッジアイコンで表示します。アイコンにカーソルを合わせると詳細な説明が表示されます。

| アイコン | 特性 | アイコン | 特性 |
|:---:|------|:---:|------|
| ![追跡型](docs/traits/chase.png) | 追跡型 | ![召喚](docs/traits/summon.png) | 召喚 |
| ![徘徊型](docs/traits/wander.png) | 徘徊型 | ![複数体](docs/traits/multiple.png) | 複数体 |
| ![壁貫通](docs/traits/wallpass.png) | 壁貫通 | ![変身](docs/traits/transform.png) | 変身・形態変化 |
| ![即死](docs/traits/instantkill.png) | 即死 | ![停止](docs/traits/stop.png) | 停止 |
| ![デバフ](docs/traits/debuff.png) | デバフ | ![速度](docs/traits/speed.png) | 速度（数値は最大速度） |
| ![掴み](docs/traits/grab.png) | 掴み | ![カウンター](docs/traits/counter.png) | カウンター |
| ![視界ダメージ](docs/traits/sight.png) | 視界ダメージ | ![スタン](docs/traits/stun.png) | スタン攻撃 |
| ![テレポート](docs/traits/teleport.png) | テレポート | ![不明](docs/traits/unknown.png) | その他・不明 |

**その他の表示:**

- 🏠 **HFA（Homefield Advantage）** - 現在のマップがテラーの強化される組み合わせのとき、家アイコンで警告します
- 🔀 **Unbound内訳表示** - Unboundラウンドでは、内訳となるテラーに展開して表示します

### 🌐 インスタンス種別表示

参加中のインスタンスIDを解析して、種別とリージョンを「ラウンド情報」グループのタイトルに表示します（例：`ラウンド情報 － Group+ / JP`）。種別ごとに色分けされ、18+インスタンスの場合はその旨も表示されます。

判定できる種別：

| 表示 | 対応するインスタンス |
|------|---------------------|
| Public | パブリック |
| Friends+ | フレンド＋（Friends of Guests） |
| Friends | フレンドのみ |
| Invite+ | Invite Plus |
| Invite | Invite |
| Group Public | グループ公開 |
| Group+ | グループ＋ |
| Group | グループメンバー限定 |

🔗ボタンにカーソルを合わせると、ワールドID・インスタンス名・グループIDなどの詳細が確認できます。

### 🖱️ オーバーレイ操作（テラー表示ウィンドウ）

設定画面の「オーバーレイ」タブから設定できます。

- **位置・サイズの記憶** - 次回起動時も同じ位置・サイズで開きます。ウィンドウ右下をドラッグするとサイズを変更できます。モニタ構成が変わって画面外になった場合は自動的に既定位置へ戻ります
- **クリックスルー** - オーバーレイの上でクリックしても背後のウィンドウが反応するようになります（有効中はドラッグ移動とリサイズができなくなります）
- **グローバルホットキー** - 他のアプリを操作中でも、オーバーレイの表示ON/OFFとクリックスルーの切り替えができます。既定は `Ctrl+Shift+T` と `Ctrl+Shift+C`（既定では無効。入力欄を選んでキーを押すと変更でき、Backspaceで解除できます）

### 👥 プレイヤー管理

- リアルタイムの生存/死亡状態表示
- 総人数・生存者数のカウント
- ダブルクリックで警告リストに追加/削除

### ⚠️ 警告ユーザー機能

特定ユーザーの参加を検知して通知：

- 音声アラート
- ウィンドウタイトル通知
- オレンジ色でハイライト表示
- 一覧の表示・削除・再読み込みもアプリ内から可能

設定方法：
1. `warn_user.txt`に1行1名でユーザー名を記入
2. またはプレイヤー一覧でダブルクリック

### 🔔 アイテムリマインダー

以下のタイミングでアイテム装備忘れを通知：

- **8ページ / パニッシュ終了後** - アイテムが没収されるラウンド終了時
- **リスポーン後の再参加時** - ゲームに再参加した際
- **サボタージュでキラー側になった時**

テラー表示ウィンドウに「アイテムを持ち直してください」と表示されます。

### 🥽 VR通知（XSOverlay連携）

[XSOverlay](https://store.steampowered.com/app/1173510/XSOverlay/)のNotification APIを使って、VRヘッドセット内にプッシュ通知を表示します：

| イベント | 内容 |
|---------|------|
| 次ラウンド予測 | ラウンド終了時に次ラウンドの予測を通知 |
| テラー情報 | ラウンド開始時にテラー名とスタン可否を通知 |
| 警告ユーザー参加 | 警告リストのユーザーが参加した時に通知 |
| アイテムリマインダー | アイテム装備忘れをVR内にも通知 |

- 設定画面の「VR通知」タブから有効化・イベント別ON/OFFを設定できます（デフォルトは無効）
- 「テスト通知を送信」ボタンでVR内の表示を確認できます
- XSOverlayが起動している必要があります（UDPポート既定: 42069）

### 📊 統計・ログ

- **セッション統計** - 生存数、死亡数、生存率、スタン回数、ダメージを「累計」と「このインスタンス」の2列で表示
- **インスタンス履歴** - インスタンスごとの滞在時間・ラウンド数・生存率を一覧表示（📊ボタンから確認）
- **ラウンド統計** - ラウンドタイプ別の回数と確率
- **テラー統計** - テラー別の遭遇回数
- **ラウンドログ** - 時刻、ラウンド種別、マップ、テラー、アイテム、結果
  - ラウンド種別・テラー名によるフィルタ表示
  - CSVファイルへのエクスポート
- **セーブコード** - 受信した最近のセーブコード（最大5件）を💾ボタンから確認・コピー

### ☁️ クラウド同期

クラウドサーバーと連携して、ラウンド情報を共有できます：

- **送信** - ラウンド終了時にテラー・マップ・生存人数などの情報をクラウドに送信
- **取得** - 同じインスタンスの過去のラウンドログを取得してローカルログにマージ
- **状態共有** - 途中参加時にインスタンス状態（周期・Moon解禁状況など）を取得して予測精度を向上

設定画面の「その他」タブから有効化できます（デフォルトは無効）。使用にはAPIキーの発行が必要です。

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

### VR通知

設定画面の「VR通知」タブからXSOverlay通知の有効化・イベント別ON/OFF・UDPポートを設定可能。

### オーバーレイ

設定画面の「オーバーレイ」タブから、テラー表示ウィンドウの位置記憶・クリックスルー・グローバルホットキーを設定可能。「位置をリセット」で既定位置（画面右上）に戻せます。

### クラウド同期

設定画面の「その他」タブからクラウド同期の有効化・サーバーURL・APIキーを設定可能。

---

## 📁 ファイル構成

```
ToNStatTool/
├── ToNStatTool.exe       # メイン実行ファイル
├── terrorsInfo.json      # テラー情報データベース
├── unboundInfo.json      # Unbound内訳データ
├── hfaInfo.json          # HFA（Homefield Advantage）データ
├── warn_user.txt         # 警告ユーザーリスト
├── settings.json         # アプリ設定（自動生成）
├── sound_settings.json   # サウンド設定（自動生成）
├── warning.mp3           # 警告音（オプション）
├── masterchange.mp3      # インスタンスマスター変更通知音（オプション）
├── item.mp3              # アイテムリマインダー通知音（オプション）
├── player_join.mp3       # プレイヤー参加通知音（オプション）
├── player_leave.mp3      # プレイヤー退出通知音（オプション）
├── images/               # テラー画像フォルダ（オプション）
├── images_sample/        # 画像ファイル名サンプル（空ファイル192個）
│   └── README.md         # 画像の配置方法
└── licenses/             # ライセンスファイル
```

### テラー画像の追加（オプション）

テラー表示ウィンドウにテラー画像を表示したい場合は、`images`フォルダにテラー画像を配置してください。

- 著作権の関係で、画像は同梱されていません
- `The_Painter.png`のように、テラー名をファイル名にしてください
- 対応形式: PNG, JPG, GIF, BMP
- 画像がない場合はプレースホルダーが表示されます

#### 画像の設置方法

1. `images_sample`フォルダ名を`images`に変更
2. 空のファイルを実際の画像ファイルで上書き

`images_sample`フォルダには192個の空のプレースホルダーファイルが含まれており、必要な画像ファイル名がわかります。

#### ファイル名の変換ルール

テラー名からファイル名を作成する際、以下のルールが適用されます：

> **英数字とアンダースコア以外の文字は、すべてアンダースコア（`_`）に置き換わります**
> 
> - 使える文字: `A-Z`, `a-z`, `0-9`, `_`（アンダースコア）
> - 使えない文字（全部`_`に変換）: スペース、ピリオド、カッコ、記号など

| テラー名 | ファイル名 | 説明 |
|----------|-----------|------|
| The Painter | `The_Painter.png` | スペース → `_` |
| Dr. Tox | `Dr__Tox.png` | `.`とスペース → `__` |
| MR.MEGA | `MR_MEGA.png` | `.` → `_` |
| S.O.S | `S_O_S.png` | `.` → `_` |
| [CENSORED] | `_CENSORED_.png` | `[`と`]` → `_` |

詳細は`images/README.md`を参照してください。

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
- 「インスタンス状態設定」で鳥遭遇・Moon解禁状況を手動調整すると改善します

### VR通知が表示されない

- XSOverlayが起動しているか確認
- 設定画面の「VR通知」タブで「テスト通知を送信」を押して疎通確認
- UDPポート設定（既定: 42069）がXSOverlay側と一致しているか確認

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
- **開発支援**: [Claude](https://claude.ai)

---

**不正はせずに、楽しくプレイしましょう。**
