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
		// アイテムリマインダー設定（8ページ/アンバウンド/サボタージュ）
		public bool EnableItemReminder { get; set; } = true;
		public bool EnableItemReminderSound { get; set; } = true;
		public string ItemReminderSoundPath { get; set; } = "";
		public int ItemReminderDurationSeconds { get; set; } = 10;
		// リスポーン後リマインダー設定
		public bool EnableRespawnReminder { get; set; } = true;
		// マスター変更音設定
		public bool EnableMasterChangeSound { get; set; } = true;
		public string MasterChangeSoundPath { get; set; } = "";
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
		
		// 前回のラウンドタイプ（Enum）
		public ToNRoundType LastRoundType { get; set; } = ToNRoundType.Intermission;
		
		// 現在のラウンドタイプ（Enum）
		public ToNRoundType CurrentRoundType { get; set; } = ToNRoundType.Intermission;
		
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
		
		// Moon解禁直後フラグ（次のラウンドがそのMoonになる可能性があることを示す）
		// 次のラウンド開始時にリセットされる
		public bool BloodMoonJustUnlocked { get; set; } = false;
		public bool TwilightJustUnlocked { get; set; } = false;
		public bool MysticMoonJustUnlocked { get; set; } = false;
		
		// Midnight生存済みフラグ
		public bool MidnightSurvived { get; set; } = false;
		
		// マスター変更フラグ（次ラウンドが特殊確定）
		public bool MasterChanged { get; set; } = false;
		
		// インスタンスURL
		public string InstanceUrl { get; set; } = "";
		
		// ローカルプレイヤーのゲーム参加状態
		public bool IsOptedIn { get; set; } = true;
		
		// リスポーン追跡用フラグ
		public bool WasOptedInThisInstance { get; set; } = false;  // このインスタンスで一度でもopted_inしたか
		public bool HadRespawnedInRound { get; set; } = false;     // リスポーン後の再参加待ち状態
		public bool IsRespawnSaveCode { get; set; } = false;       // 次のセーブコードがリスポーン用か
		
		// 現在所持しているアイテム名
		public string CurrentItem { get; set; } = "";
		
		// 現在のラウンドが初回Moonかどうか（次ラウンド予測用）
		public bool IsCurrentRoundFirstMoon { get; set; } = false;
		
		// ラウンド開始時のNormalRoundCount（次ラウンド予測用）
		// ラウンド終了時にNormalRoundCountが更新されるため、開始時の値を保存しておく
		public int NormalRoundCountAtRoundStart { get; set; } = 0;
		
		// 現在のラウンドが上書きラウンドかどうか（表示用）
		// 通常確定時にOverrideラウンドまたは特殊ラウンドが出た場合にtrue
		public bool IsCurrentRoundOverride { get; set; } = false;
		
		/// <summary>
		/// 3鳥コンプリート判定
		/// </summary>
		public bool AllBirdsMet => MetBigBird && MetJudgementBird && MetPunishingBird;
		
		/// <summary>
		/// 全Moon解禁判定
		/// </summary>
		public bool AllMoonsUnlocked => BloodMoonUnlocked && TwilightUnlocked && MysticMoonUnlocked;
		
		/// <summary>
		/// 現在のラウンドタイプが有効（Intermission以外）かどうか
		/// </summary>
		public bool HasCurrentRound => CurrentRoundType != ToNRoundType.Intermission;
		
		/// <summary>
		/// 前回のラウンドタイプが有効（Intermission以外）かどうか
		/// </summary>
		public bool HasLastRound => LastRoundType != ToNRoundType.Intermission;
		
		/// <summary>
		/// 状態をリセット
		/// </summary>
		public void Reset()
		{
			IsInstanceOwner = false;
			SpecialUnlocked = true;
			NormalRoundCount = 0;
			LastRoundType = ToNRoundType.Intermission;
			CurrentRoundType = ToNRoundType.Intermission;
			EstimatedSurvivalCount = 0;
			MetBigBird = false;
			MetJudgementBird = false;
			MetPunishingBird = false;
			BloodMoonUnlocked = false;
			TwilightUnlocked = false;
			MysticMoonUnlocked = false;
			SolsticeUnlocked = false;
			BloodMoonJustUnlocked = false;
			TwilightJustUnlocked = false;
			MysticMoonJustUnlocked = false;
			MidnightSurvived = false;
			MasterChanged = false;
			InstanceUrl = "";
			IsOptedIn = true;
			CurrentItem = "";
			IsCurrentRoundFirstMoon = false;
			NormalRoundCountAtRoundStart = 0;
			IsCurrentRoundOverride = false;
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
		
		/// <summary>
		/// ラウンドタイプ（Enum）
		/// </summary>
		public ToNRoundType RoundType { get; set; } = ToNRoundType.Intermission;
		
		/// <summary>
		/// ラウンドタイプの表示名を取得
		/// </summary>
		public string RoundTypeDisplayName => ToNRoundTypeHelper.GetDisplayName(RoundType);
		
		public string MapName { get; set; }
		public string TerrorNames { get; set; }
		public string Items { get; set; }
		public bool Survived { get; set; }
		
		/// <summary>
		/// ラウンド開始時にゲームに参加していたか
		/// </summary>
		public bool WasOptedIn { get; set; } = true;
	}

	/// <summary>
	/// ラウンド統計情報を保持するクラス
	/// </summary>
	public class RoundStats
	{
		/// <summary>
		/// ラウンドタイプごとのカウント（Enumベース）
		/// </summary>
		public Dictionary<ToNRoundType, int> RoundTypeCounts { get; set; } = new Dictionary<ToNRoundType, int>();
		
		public Dictionary<string, int> TerrorCounts { get; set; } = new Dictionary<string, int>();
		public int TotalRounds { get; set; } = 0;
		public int SurvivedRounds { get; set; } = 0;
		
		/// <summary>
		/// 指定したラウンドタイプのカウントを取得
		/// </summary>
		public int GetCount(ToNRoundType roundType)
		{
			return RoundTypeCounts.TryGetValue(roundType, out int count) ? count : 0;
		}
		
		/// <summary>
		/// 指定したラウンドタイプのカウントを増加
		/// </summary>
		public void IncrementCount(ToNRoundType roundType)
		{
			if (RoundTypeCounts.ContainsKey(roundType))
			{
				RoundTypeCounts[roundType]++;
			}
			else
			{
				RoundTypeCounts[roundType] = 1;
			}
		}
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

	/// <summary>
	/// セーブコード情報を保持するクラス
	/// </summary>
	public class SaveCodeInfo
	{
		public string Code { get; set; } = "";
		public string RoundTypeName { get; set; } = "";
		public string TerrorNames { get; set; } = "";  // テラー名を追加
		public DateTime Timestamp { get; set; } = DateTime.Now;
		
		public override string ToString()
		{
			return $"{Timestamp:HH:mm:ss} - {RoundTypeName}";
		}
	}

	/// <summary>
	/// セッション統計情報を保持するクラス（アプリ起動中のみ有効）
	/// </summary>
	public class SessionStats
	{
		public int Survivals { get; set; } = 0;
		public int Deaths { get; set; } = 0;
		public int Stuns { get; set; } = 0;
		public int StunsAll { get; set; } = 0;  // 全員のスタン
		public int TopStuns { get; set; } = 0;  // 1ラウンド最高スタン
		public int TopStunsAll { get; set; } = 0;
		public int DamageTaken { get; set; } = 0;
		public int RoundDamage { get; set; } = 0;  // 現在ラウンドのダメージ
		public int RoundStuns { get; set; } = 0;   // 現在ラウンドのスタン
		public int RoundStunsAll { get; set; } = 0;
		
		public int TotalRounds => Survivals + Deaths;
		public double SurvivalRate => TotalRounds > 0 ? (double)Survivals / TotalRounds * 100 : 0;
		
		/// <summary>
		/// ラウンド終了時に呼び出し（スタン記録更新）
		/// </summary>
		public void OnRoundEnd(bool survived)
		{
			if (survived)
				Survivals++;
			else
				Deaths++;
			
			// 最高記録更新
			if (RoundStuns > TopStuns)
				TopStuns = RoundStuns;
			if (RoundStunsAll > TopStunsAll)
				TopStunsAll = RoundStunsAll;
			
			// ラウンド単位のカウンターをリセット
			RoundDamage = 0;
			RoundStuns = 0;
			RoundStunsAll = 0;
		}
		
		/// <summary>
		/// 統計をリセット
		/// </summary>
		public void Reset()
		{
			Survivals = 0;
			Deaths = 0;
			Stuns = 0;
			StunsAll = 0;
			TopStuns = 0;
			TopStunsAll = 0;
			DamageTaken = 0;
			RoundDamage = 0;
			RoundStuns = 0;
			RoundStunsAll = 0;
		}
	}
}
