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
		// 警告ユーザー参加時サウンド
		public bool EnableWarningUserSound { get; set; } = true;
		public string WarningUserSoundPath { get; set; } = "";
	}

	/// <summary>
	/// インスタンス状態を保持するクラス（ラウンド予測用）
	/// </summary>
	public class InstanceState
	{
		// インスタンス作成者判定
		public bool IsInstanceOwner { get; set; } = false;
		
		// 特殊ラウンド解放状態（インスタンス全体で3回生存）
		public bool SpecialUnlocked { get; set; } = true; // 途中参加を考慮してデフォルトtrue
		
		// 通常ラウンド連続回数（特殊後にリセット）
		public int NormalRoundCount { get; set; } = 0;
		
		// 前回のラウンドタイプ
		public string LastRoundType { get; set; } = "";
		
		// 現在のラウンドタイプ（次ラウンド予測用）
		public string CurrentRoundType { get; set; } = "";
		
		// インスタンス全体の推定生存カウント
		public int EstimatedSurvivalCount { get; set; } = 0;
		
		// 鳥遭遇状態
		public bool MetBigBird { get; set; } = false;
		public bool MetJudgementBird { get; set; } = false;
		public bool MetPunishingBird { get; set; } = false;
		
		// Moon解禁状態
		public bool BloodMoonUnlocked { get; set; } = false;
		public bool TwilightUnlocked { get; set; } = false;
		public bool MysticMoonUnlocked { get; set; } = false;
		public bool SolsticeUnlocked { get; set; } = false;
		
		// Midnight生存済みフラグ
		public bool MidnightSurvived { get; set; } = false;
		
		/// <summary>
		/// 3鳥コンプリート判定
		/// </summary>
		public bool AllBirdsMet => MetBigBird && MetJudgementBird && MetPunishingBird;
		
		/// <summary>
		/// 全Moon解禁判定
		/// </summary>
		public bool AllMoonsUnlocked => BloodMoonUnlocked && TwilightUnlocked && MysticMoonUnlocked;
		
		/// <summary>
		/// 状態をリセット
		/// </summary>
		public void Reset()
		{
			IsInstanceOwner = false;
			SpecialUnlocked = true;
			NormalRoundCount = 0;
			LastRoundType = "";
			CurrentRoundType = "";
			EstimatedSurvivalCount = 0;
			MetBigBird = false;
			MetJudgementBird = false;
			MetPunishingBird = false;
			BloodMoonUnlocked = false;
			TwilightUnlocked = false;
			MysticMoonUnlocked = false;
			SolsticeUnlocked = false;
			MidnightSurvived = false;
		}
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