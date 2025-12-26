using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ToNStatTool
{
	/// <summary>
	/// テラー情報を保持するクラス
	/// </summary>
	public class TerrorInfo
	{
		public string Name { get; set; }
		public string DisplayName { get; set; }
		public uint DisplayColor { get; set; }
		public TerrorStunType StunType { get; set; }
		public string IconPath { get; set; }
		public string Description { get; set; }
	}

	/// <summary>
	/// プレイヤー情報を保持するクラス
	/// </summary>
	public class PlayerInfo
	{
		public string Name { get; set; } = "";
		public string UserId { get; set; } = "";
		public bool IsLocal { get; set; }
		public bool IsAlive { get; set; } = true;
		public DateTime LastSeen { get; set; } = DateTime.Now;
		public DateTime JoinedAt { get; set; } = DateTime.Now;
		public bool IsWarningUser { get; set; } = false;
	}

	/// <summary>
	/// サウンド設定を保持するクラス
	/// </summary>
	public class SoundSettings
	{
		public bool EnableJoinSound { get; set; } = false;
		public bool EnableLeaveSound { get; set; } = false;
		public string JoinSoundPath { get; set; } = "";
		public string LeaveSoundPath { get; set; } = "";
	}

	/// <summary>
	/// ゲームイベント情報を保持するクラス
	/// </summary>
	public class GameEvent
	{
		public string Type { get; set; }
		public DateTime Timestamp { get; set; }
		public string Description { get; set; }
		public JObject RawData { get; set; }
	}

	/// <summary>
	/// ラウンドログ情報を保持するクラス
	/// </summary>
	public class RoundLog
	{
		public DateTime Timestamp { get; set; }
		public string RoundType { get; set; }
		public string MapName { get; set; }
		public string TerrorNames { get; set; }
		public string Items { get; set; }
		public bool Survived { get; set; }
	}

	/// <summary>
	/// ラウンド統計情報を保持するクラス
	/// </summary>
	public class RoundStats
	{
		public Dictionary<string, int> RoundTypeCounts { get; set; } = new Dictionary<string, int>();
		public Dictionary<string, int> TerrorCounts { get; set; } = new Dictionary<string, int>();
		public int TotalRounds { get; set; } = 0;
		public int SurvivedRounds { get; set; } = 0;
	}

	/// <summary>
	/// テラー統計情報を保持するクラス
	/// </summary>
	public class TerrorStats
	{
		public Dictionary<string, int> TerrorTypeCounts { get; set; } = new Dictionary<string, int>();
		public Dictionary<string, int> TerrorCounts { get; set; } = new Dictionary<string, int>();
		public int TerrorsMet { get; set; } = 0;
	}
}