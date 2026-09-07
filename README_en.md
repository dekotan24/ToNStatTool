# 🎮 ToNStatTool

Real-time Statistics Tracking Tool for **Terror of Nowhere**

[![Windows](https://img.shields.io/badge/Platform-Windows-0078d4?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![.NET Framework](https://img.shields.io/badge/.NET_Framework-4.8.1-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)
[![GitHub release](https://img.shields.io/github/v/release/dekotan24/ToNStatTool?style=flat-square&logo=github)](https://github.com/dekotan24/ToNStatTool/releases)

English | [日本語](README.md)

![Screenshot](https://raw.githubusercontent.com/dekotan24/ToNStatTool/main/docs/screenshot.png)

---

## ✨ Features

ToNStatTool is a Windows application that tracks and displays game data from the VRChat world "**Terror of Nowhere**" in real-time. It works through the WebSocket API of [ToNSaveManager](https://github.com/ChrisFeline/ToNSaveManager).

### 🎯 Key Features

| Feature | Description |
|---------|-------------|
| 🔮 **Next Round Prediction** | Predicts the next round type based on round cycles and Moon unlock conditions |
| 👻 **Terror Information** | Displays current terrors with stun status and trait icons in real-time |
| 📊 **Statistics Tracking** | Automatically records session stats and round/terror encounter stats |
| 📝 **Round Log** | Records every round with filtering and CSV export |
| 🌐 **Instance Type Display** | Automatically detects Public / Friends+ / Invite / Group+ and the region |
| 🖱️ **Overlay Controls** | Remembers position and size, click-through, global hotkeys |
| 👥 **Player Management** | Shows player survival status in real-time |
| ⚠️ **Warning System** | Notifies with sound and highlight when specific users join |
| 🔔 **Item Reminder** | Notifies to re-equip items after 8 Pages/Punished and more |
| 🥽 **VR Notifications** | Push notifications into your VR headset via XSOverlay |
| ☁️ **Cloud Sync** | Sends round info and fetches past logs / instance state |
| 💾 **Save Codes** | One-click access to recently received save codes |
| 🎨 **Theme Switching** | Supports dark/light themes |

---

## 🚀 Quick Start

### Requirements

- Windows 10/11
- [.NET Framework 4.8.1](https://dotnet.microsoft.com/download/dotnet-framework)
- [ToNSaveManager](https://github.com/ChrisFeline/ToNSaveManager) (with WebSocket API enabled)

### Installation

1. Download `ToNStatTool_vx.x.zip` from [Latest Release](https://github.com/dekotan24/ToNStatTool/releases)
2. Extract to any folder

### Getting Started

```
1. Start ToNSaveManager and enable WebSocket API Server
2. Launch `ToNStatTool.exe` (it will try to auto-connect on startup)
3. If not connected, click the "Connect" button
```

---

## 📖 Feature Details

### 🔮 Next Round Prediction

Analyzes in-game round cycles to predict the next round type:

- **Normal** - Next round is Normal (Classic/RUN)
- **Special** - Next round is Special (Alternate, Punished, etc.)
- **Normal or Special** - Either is possible
- **Moon** - Predicted when unlock conditions are met (3 birds, survival count, Midnight survival, etc.)
- **Special (Master Change)** - Guaranteed Special due to instance master change

> ⚠️ Predictions are probability-based and not 100% accurate.
> To improve accuracy, adjust the "Instance State" settings manually (see below).

### 👻 Terror Information

Displays current terrors with stun status, behavior traits, and terror images.
The compact "Terror Display Window" (with adjustable opacity) is handy for streaming layouts or a second monitor.

**Stun Status Icons:**

| Icon | Meaning |
|:---:|---------|
| ![Stunnable](docs/stun/safe.png) | Stunnable |
| ![Conditional Stun](docs/stun/caution.png) | Conditional Stun (use caution) |
| ![Do NOT Stun](docs/stun/forbidden.png) | Do NOT Stun |
| ![No effect](docs/stun/ineffective.png) | Stun has no effect |
| ![Unknown](docs/stun/unknown.png) | Stun status unknown |

**Trait Icons:**

Terror behavior traits are displayed as colored badge icons. Hover over an icon to see a detailed description.

| Icon | Trait | Icon | Trait |
|:---:|------|:---:|------|
| ![Chaser](docs/traits/chase.png) | Chaser | ![Summoner](docs/traits/summon.png) | Summoner |
| ![Wanderer](docs/traits/wander.png) | Wanderer | ![Multiple](docs/traits/multiple.png) | Multiple |
| ![Wall Pass](docs/traits/wallpass.png) | Wall Pass | ![Transform](docs/traits/transform.png) | Transform |
| ![Instant Kill](docs/traits/instantkill.png) | Instant Kill | ![Stop](docs/traits/stop.png) | Stop |
| ![Debuff](docs/traits/debuff.png) | Debuff | ![Speed](docs/traits/speed.png) | Speed (number = max speed) |
| ![Grab](docs/traits/grab.png) | Grab | ![Counter](docs/traits/counter.png) | Counter |
| ![Sight Damage](docs/traits/sight.png) | Sight Damage | ![Stun Attack](docs/traits/stun.png) | Stun Attack |
| ![Teleport](docs/traits/teleport.png) | Teleport | ![Unknown](docs/traits/unknown.png) | Other / Unknown |

**Additional Displays:**

- 🏠 **HFA (Homefield Advantage)** - Shows a house icon when the current map is a combination where the terror is empowered
- 🔀 **Unbound Breakdown** - During Unbound rounds, expands the display into the individual terrors

### 🌐 Instance Type Display

The instance ID you are in is parsed and shown in the "Round Info" group title (e.g. `Round Info － Group+ / JP`), color-coded by type. Age-gated (18+) instances are marked as well.

Detected types: Public, Friends+, Friends, Invite+, Invite, Group Public, Group+, Group.

Hover the 🔗 button to see details such as the world ID, instance name and group ID.

### 🖱️ Overlay Controls (Terror Display Window)

Configurable from the "Overlay" tab in the settings window.

- **Remember position and size** - The window reopens where you left it. Drag the bottom-right corner to resize. If a monitor layout change leaves it off-screen, it returns to the default position automatically
- **Click-through** - Clicks pass through the overlay to the window behind it (dragging and resizing are disabled while enabled)
- **Global hotkeys** - Toggle the overlay and click-through even while another app has focus. Defaults are `Ctrl+Shift+T` and `Ctrl+Shift+C` (disabled by default; click the input box and press a key to change it, Backspace to clear)

### 👥 Player Management

- Real-time survival/death status display
- Total player and survivor count
- Double-click to add/remove from warning list

### ⚠️ Warning User Feature

Detects and notifies when specific users join:

- Audio alert
- Window title notification
- Orange highlight in player list
- View, remove, and reload the list from within the app

Configuration:
1. Add usernames to `warn_user.txt` (one per line)
2. Or double-click in the player list

### 🔔 Item Reminder

Notifies you to re-equip items at the following times:

- **After 8 Pages / Punished** - When rounds that confiscate items end
- **After respawn and rejoin** - When you rejoin the game after respawning
- **When becoming killer in Sabotage**

Displays "Please re-equip your items" in the Terror Display Window.

### 🥽 VR Notifications (XSOverlay Integration)

Shows push notifications inside your VR headset via the [XSOverlay](https://store.steampowered.com/app/1173510/XSOverlay/) Notification API:

| Event | Description |
|-------|-------------|
| Next Round Prediction | Notifies the next round prediction when a round ends |
| Terror Information | Notifies terror names with stun status when a round starts |
| Warning User Join | When a user on the warning list joins |
| Item Reminder | Item re-equip reminders, also in VR |

- Enable and toggle each event from the "VR通知" (VR Notify) tab in Settings (disabled by default)
- Use the test button to verify the notification appears in VR
- XSOverlay must be running (default UDP port: 42069)

### 📊 Statistics & Logs

- **Session Stats** - Survivals, deaths, survival rate, stuns and damage taken, shown side by side as "total" and "current instance"
- **Instance History** - Time spent, round count and survival rate per instance (via the 📊 button)
- **Round Stats** - Count and percentage by round type
- **Terror Stats** - Encounter count by terror
- **Round Log** - Time, round type, map, terrors, items, result
  - Filter by round type and terror name
  - Export to CSV file
- **Save Codes** - View and copy recently received save codes (up to 5) via the 💾 button

### ☁️ Cloud Sync

Connects to a cloud server to share round information:

- **Send** - Sends terror, map, and survivor info to the cloud when a round ends
- **Fetch** - Fetches past round logs of the same instance and merges them into the local log
- **State Sharing** - Fetches instance state (cycle, Moon unlocks, etc.) when joining mid-instance to improve prediction accuracy

Enable it from the "その他" (Other) tab in Settings (disabled by default). An API key is required.

### 🌙 Instance State Management

Manual settings to improve Moon round prediction accuracy:

- Bird encounter status (Big Bird, Judgement Bird, Punishing Bird)
- Moon unlock status (Blood Moon, Twilight, Mystic Moon, Solstice)
- Estimated survival count

---

## ⚙️ Settings

### WebSocket Connection

Default URL: `ws://localhost:11398`

Adjust the connection URL if you've changed ToNSaveManager's port settings.

### Sound Settings

Configure from "🔊 Sound Settings":

| Sound | Description |
|-------|-------------|
| Player Join | When someone joins |
| Player Leave | When someone leaves |
| Warning User Join | When a user on the warning list joins |
| Master Change | When instance master changes |
| Item Reminder | When item re-equip notification triggers |

Supported formats: MP3, WAV

### Theme

Switch between dark/light themes from the settings screen.

### VR Notifications

Enable XSOverlay notifications, toggle individual events, and change the UDP port from the "VR通知" (VR Notify) tab in Settings.

### Overlay

Configure the terror display window's position memory, click-through and global hotkeys from the "オーバーレイ" (Overlay) tab in Settings. "位置をリセット" (Reset position) puts the window back to the top-right of the screen.

### Cloud Sync

Enable cloud sync and set the server URL / API key from the "その他" (Other) tab in Settings.

---

## 📁 File Structure

```
ToNStatTool/
├── ToNStatTool.exe       # Main executable
├── terrorsInfo.json      # Terror information database
├── unboundInfo.json      # Unbound breakdown data
├── hfaInfo.json          # HFA (Homefield Advantage) data
├── warn_user.txt         # Warning user list
├── settings.json         # App settings (auto-generated)
├── sound_settings.json   # Sound settings (auto-generated)
├── warning.mp3           # Warning sound (optional)
├── masterchange.mp3      # Instance master change sound (optional)
├── item.mp3              # Item reminder sound (optional)
├── player_join.mp3       # Player join sound (optional)
├── player_leave.mp3      # Player leave sound (optional)
├── images/               # Terror images folder (optional)
├── images_sample/        # Sample image filenames (192 empty files)
│   └── README.md         # Image placement instructions
└── licenses/             # License files
```

### Adding Terror Images (Optional)

If you want to display terror images in the Terror Display window, place terror images in the `images` folder.

- Images are not included due to copyright restrictions
- Name files like `The_Painter.png` using the terror name
- Supported formats: PNG, JPG, GIF, BMP
- Placeholder images are shown when images are not available

#### How to Set Up Images

1. Rename the `images_sample` folder to `images`
2. Replace the empty files with actual image files

The `images_sample` folder contains 192 empty placeholder files showing the required image filenames.

#### Filename Conversion Rules

When converting terror names to filenames, the following rules apply:

> **Any character that is NOT a letter, number, or underscore is replaced with underscore (`_`)**
> 
> - Allowed characters: `A-Z`, `a-z`, `0-9`, `_` (underscore)
> - All other characters (spaces, periods, brackets, symbols, etc.) become `_`

| Terror Name | Filename | Explanation |
|-------------|----------|-------------|
| The Painter | `The_Painter.png` | Space → `_` |
| Dr. Tox | `Dr__Tox.png` | `.` and space → `__` |
| MR.MEGA | `MR_MEGA.png` | `.` → `_` |
| S.O.S | `S_O_S.png` | `.` → `_` |
| [CENSORED] | `_CENSORED_.png` | `[` and `]` → `_` |

See `images/README.md` for details.

### Updating Terror Information

Get the latest `terrorsInfo.json` from the following repository:

🔗 [ToNRoundCounter - lovetwice1012/ToNRoundCounter](https://github.com/lovetwice1012/ToNRoundCounter)

---

## 🔧 Troubleshooting

### Cannot Connect

- Verify ToNSaveManager is running
- Verify WebSocket API Server is enabled
- Check if URL is correct (default: `ws://localhost:11398`)

### Terror Information Not Showing

- Verify `terrorsInfo.json` is in the same folder
- Check if JSON file format is valid

### Players Not Showing

- Some special rounds may not track players
- Players are reflected after a round starts when you join an instance

### Next Round Prediction is Wrong

- Prediction accuracy decreases immediately after joining mid-instance
- Manually adjusting bird encounters / Moon unlocks in "Instance State" settings helps

### VR Notifications Not Showing

- Verify XSOverlay is running
- Press "テスト通知を送信" (Send test notification) in the VR Notify tab to check connectivity
- Verify the UDP port setting (default: 42069) matches XSOverlay

---

## 🤝 Contributing

Bug reports and feature requests are welcome on [Issues](https://github.com/dekotan24/ToNStatTool/issues)!

> ⚠️ **Important**: For questions or bug reports about this tool, please **only use the Issues on this repository**.
> 
> **DO NOT** contact **Beyond** (ToN world creator) or **ChrisFeline** (ToNSaveManager creator) about this tool. This is an unofficial tool created by an individual and is not affiliated with them.

---

## 📜 License

This project is dual-licensed:

| Target | License |
|--------|---------|
| Source Code | MIT License |
| JSON Data Files | ToNRoundCounter License |
| Sound Files | CC BY 4.0 |

See [LICENSE](LICENSE) file and `licenses/` directory for details.

---

## 🙏 Credits

- **terrorsInfo.json**: yussy - [ToNRoundCounter](https://github.com/lovetwice1012/ToNRoundCounter)
- **Sound Assets**: [OtoLogic](https://otologic.jp) (CC BY 4.0)
- **ToNSaveManager**: [ChrisFeline](https://github.com/ChrisFeline/ToNSaveManager)
- **Development Support**: [Claude](https://claude.ai)

---

**Play fair and have fun!**
