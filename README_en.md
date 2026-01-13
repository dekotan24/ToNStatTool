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
| 🔮 **Next Round Prediction** | Predicts the next round type based on round cycles |
| 👻 **Terror Information** | Displays current terrors with icons in real-time |
| 📊 **Statistics Tracking** | Automatically records round and terror encounter stats |
| 👥 **Player Management** | Shows player survival status in real-time |
| ⚠️ **Warning System** | Notifies when specific users join |
| 🔔 **Item Reminder** | Notifies to re-equip items after 8 Pages/Unbound |
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
2. Launch `ToNStatTool.exe`
3. Click the "Connect" button
```

---

## 📖 Feature Details

### 🔮 Next Round Prediction

Analyzes in-game round cycles to predict the next round type:

- **Normal** - Next round is Normal (Classic/RUN)
- **Special** - Next round is Special (Alternate, Punished, etc.)
- **Normal or Special** - Either is possible
- **Moon** - Predicted when unlock conditions are met

> ⚠️ Predictions are probability-based and not 100% accurate

### 👻 Terror Information

Displays current terrors with the following information:

| Icon | Meaning |
|------|---------|
| 🟢 | Stunnable |
| 🟡 | Conditional Stun |
| 🔴 | Do NOT Stun |
| ⚪ | Stun has no effect |
| 🟣 | Stun status unknown |

**Trait Icons:**
- ➡️ Chaser | 🔄 Wanderer | ⚡ Teleport | 💀 Instant Kill
- ➕ Summoner | ⬇️ Debuff | ↩️ Counter | ••• Multiple

### 👥 Player Management

- Real-time survival/death status display
- Total player and survivor count
- Double-click to add/remove from warning list

### ⚠️ Warning User Feature

Detects and notifies when specific users join:

- Audio alert
- Window title notification
- Orange highlight in player list

Configuration:
1. Add usernames to `warn_user.txt` (one per line)
2. Or double-click in the player list

### 🔔 Item Reminder

Notifies you to re-equip items at the following times:

- **After 8 Pages / Unbound** - When rounds that confiscate items end
- **After respawn and rejoin** - When you rejoin the game after respawning
- **When becoming killer in Sabotage**

Displays "Please re-equip your items" in the Terror Display Window.

### 📊 Statistics & Logs

- **Session Stats** - Survivals, deaths, stuns, damage taken
- **Round Stats** - Count and percentage by round type
- **Terror Stats** - Encounter count by terror
- **Round Log** - Time, round type, map, terrors, result

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

---

## 📁 File Structure

```
ToNStatTool/
├── ToNStatTool.exe       # Main executable
├── terrorsInfo.json      # Terror information database
├── warn_user.txt         # Warning user list
├── settings.json         # App settings (auto-generated)
├── sound_settings.json   # Sound settings (auto-generated)
├── warning.mp3           # Warning sound (optional)
├── masterchange.mp3      # Instance master change sound (optional)
├── item.mp3              # Item reminder sound (optional)
├── player_join.mp3       # Player join sound (optional)
├── player_leave.mp3      # Player leave sound (optional)
└── licenses/             # License files
```

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
- **Development Support**: [Claude](https://claude.ai) (Opus 4.5)

---

**Play fair and have fun!**
