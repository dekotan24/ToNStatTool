using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ToNStatTool.Services
{
	/// <summary>
	/// クラウドサーバーへのデータ送信を管理するサービス
	/// </summary>
	public class CloudService : IDisposable
	{
		private readonly HttpClient httpClient;
		private string serverUrl;
		private string apiKey;
		private bool isEnabled;

		public CloudService()
		{
			httpClient = new HttpClient();
			httpClient.Timeout = TimeSpan.FromSeconds(10);

			// 設定を読み込み
			var settings = AppSettings.Load();
			serverUrl = settings.CloudServerUrl;
			apiKey = settings.CloudApiKey;
			isEnabled = settings.EnableCloudSync;

			// APIキーヘッダーを設定
			UpdateApiKeyHeader();
		}

		/// <summary>
		/// クラウド同期の有効/無効を設定
		/// </summary>
		public void SetEnabled(bool enabled)
		{
			isEnabled = enabled;
		}

		/// <summary>
		/// サーバーURLを設定
		/// </summary>
		public void SetServerUrl(string url)
		{
			serverUrl = url;
		}

		/// <summary>
		/// APIキーを設定
		/// </summary>
		public void SetApiKey(string key)
		{
			apiKey = key;
			UpdateApiKeyHeader();
		}

		/// <summary>
		/// APIキーヘッダーを更新
		/// </summary>
		private void UpdateApiKeyHeader()
		{
			httpClient.DefaultRequestHeaders.Remove("X-API-Key");
			if (!string.IsNullOrEmpty(apiKey))
			{
				httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
			}
		}

		/// <summary>
		/// ラウンド終了イベントを送信
		/// </summary>
		public async Task SendRoundEndAsync(CloudRoundEndEvent roundEvent)
		{
			if (!isEnabled || string.IsNullOrEmpty(serverUrl))
				return;

			try
			{
				var json = JsonConvert.SerializeObject(roundEvent);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				var response = await httpClient.PostAsync($"{serverUrl.TrimEnd('/')}/api/v1/events", content);

				if (!response.IsSuccessStatusCode)
				{
					Logger.Warn("CloudService", $"Failed to send round end event: {response.StatusCode}");
				}
			}
			catch (Exception ex)
			{
				Logger.Error("CloudService", $"Error sending round end event: {ex.Message}");
			}
		}

		/// <summary>
		/// インスタンス状態更新イベントを送信
		/// </summary>
		public async Task SendInstanceUpdateAsync(CloudInstanceUpdateEvent updateEvent)
		{
			if (!isEnabled || string.IsNullOrEmpty(serverUrl))
				return;

			try
			{
				var json = JsonConvert.SerializeObject(updateEvent);
				var content = new StringContent(json, Encoding.UTF8, "application/json");

				var response = await httpClient.PostAsync($"{serverUrl.TrimEnd('/')}/api/v1/events", content);

				if (!response.IsSuccessStatusCode)
				{
					Logger.Warn("CloudService", $"Failed to send instance update: {response.StatusCode}");
				}
			}
			catch (Exception ex)
			{
				Logger.Error("CloudService", $"Error sending instance update: {ex.Message}");
			}
		}

		/// <summary>
		/// インスタンスの状態をWebから取得
		/// </summary>
		/// <param name="instanceId">インスタンスID（例: wrld_xxx:12345~region(...)）</param>
		/// <returns>インスタンス状態、取得できなければnull</returns>
		public async Task<CloudInstanceMoonState> FetchInstanceStateAsync(string instanceId)
		{
			if (!isEnabled || string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(instanceId))
				return null;

			try
			{
				// インスタンスIDから短縮ID（5桁）を抽出
				string shortId = ExtractShortInstanceId(instanceId);
				if (string.IsNullOrEmpty(shortId))
				{
					Logger.Debug("CloudService", $"Could not extract short ID from: {instanceId}");
					return null;
				}

				var response = await httpClient.GetAsync($"{serverUrl.TrimEnd('/')}/api/v1/stats/instance/{shortId}");

				if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
				{
					// インスタンスがまだ記録されていない場合は正常
					Logger.Debug("CloudService", $"Instance not found on server: {shortId}");
					return null;
				}

				if (!response.IsSuccessStatusCode)
				{
					Logger.Warn("CloudService", $"Failed to fetch instance state: {response.StatusCode}");
					return null;
				}

				var responseBody = await response.Content.ReadAsStringAsync();
				var instanceDetail = JsonConvert.DeserializeObject<CloudInstanceDetailResponse>(responseBody);

				if (instanceDetail?.MoonState != null)
				{
					Logger.Info("CloudService", $"Fetched instance state: BloodMoon={instanceDetail.MoonState.BloodMoonUnlocked}, Twilight={instanceDetail.MoonState.TwilightUnlocked}, MysticMoon={instanceDetail.MoonState.MysticMoonUnlocked}, Solstice={instanceDetail.MoonState.SolsticeUnlocked}");
					Logger.Info("CloudService", $"Birds: BigBird={instanceDetail.MoonState.BigBirdEncountered}, Judgement={instanceDetail.MoonState.JudgementBirdEncountered}, Punishing={instanceDetail.MoonState.PunishingBirdEncountered}");
				}

				return instanceDetail?.MoonState;
			}
			catch (Exception ex)
			{
				Logger.Error("CloudService", $"Error fetching instance state: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// インスタンスIDから短縮ID（5桁）を抽出
		/// 例: wrld_xxx:12345~region(...) -> 12345
		/// </summary>
		private string ExtractShortInstanceId(string instanceId)
		{
			if (string.IsNullOrEmpty(instanceId))
				return null;

			// コロンで分割してインスタンス部分を取得
			var colonIndex = instanceId.IndexOf(':');
			if (colonIndex < 0)
				return null;

			var instancePart = instanceId.Substring(colonIndex + 1);

			// チルダで分割して短縮IDを取得
			var tildeIndex = instancePart.IndexOf('~');
			if (tildeIndex > 0)
			{
				return instancePart.Substring(0, tildeIndex);
			}

			// チルダがなければそのまま返す
			return instancePart;
		}

		public void Dispose()
		{
			httpClient?.Dispose();
		}
	}

	#region クラウド送信用データモデル

	/// <summary>
	/// ラウンド終了イベント（クラウド送信用）
	/// </summary>
	public class CloudRoundEndEvent
	{
		[JsonProperty("eventType")]
		public string EventType { get; set; } = "roundEnd";

		[JsonProperty("instanceId")]
		public string InstanceId { get; set; }

		[JsonProperty("timestamp")]
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;

		[JsonProperty("round")]
		public CloudRoundInfo Round { get; set; }

		[JsonProperty("instance")]
		public CloudInstanceInfo Instance { get; set; }

		[JsonProperty("player")]
		public CloudPlayerInfo Player { get; set; }
	}

	/// <summary>
	/// プレイヤー情報（クラウド送信用）
	/// 自分自身の情報のみ送信
	/// </summary>
	public class CloudPlayerInfo
	{
		[JsonProperty("vrchatName")]
		public string VRChatName { get; set; }

		[JsonProperty("vrchatId")]
		public string VRChatId { get; set; }

		[JsonProperty("survived")]
		public bool Survived { get; set; }

		[JsonProperty("items")]
		public string[] Items { get; set; }
	}

	/// <summary>
	/// ラウンド情報（クラウド送信用）
	/// </summary>
	public class CloudRoundInfo
	{
		[JsonProperty("type")]
		public string Type { get; set; }

		[JsonProperty("mapName")]
		public string MapName { get; set; }

		[JsonProperty("terrors")]
		public string[] Terrors { get; set; }
	}

	/// <summary>
	/// インスタンス情報（クラウド送信用）
	/// プレイヤー数のみ送信し、個人情報は含まない
	/// </summary>
	public class CloudInstanceInfo
	{
		[JsonProperty("playerCount")]
		public int PlayerCount { get; set; }

		[JsonProperty("survivorCount")]
		public int SurvivorCount { get; set; }
	}

	/// <summary>
	/// インスタンス状態更新イベント（クラウド送信用）
	/// </summary>
	public class CloudInstanceUpdateEvent
	{
		[JsonProperty("eventType")]
		public string EventType { get; set; } = "instanceUpdate";

		[JsonProperty("instanceId")]
		public string InstanceId { get; set; }

		[JsonProperty("timestamp")]
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;

		[JsonProperty("state")]
		public CloudInstanceState State { get; set; }
	}

	/// <summary>
	/// インスタンス状態（クラウド送信用）
	/// </summary>
	public class CloudInstanceState
	{
		[JsonProperty("playerCount")]
		public int PlayerCount { get; set; }

		[JsonProperty("normalRoundCount")]
		public int NormalRoundCount { get; set; }

		[JsonProperty("bloodMoonUnlocked")]
		public bool BloodMoonUnlocked { get; set; }

		[JsonProperty("twilightUnlocked")]
		public bool TwilightUnlocked { get; set; }

		[JsonProperty("mysticMoonUnlocked")]
		public bool MysticMoonUnlocked { get; set; }

		[JsonProperty("solsticeUnlocked")]
		public bool SolsticeUnlocked { get; set; }
	}

	#endregion

	#region クラウド取得用データモデル

	/// <summary>
	/// インスタンス詳細レスポンス（クラウド取得用）
	/// </summary>
	public class CloudInstanceDetailResponse
	{
		[JsonProperty("id")]
		public int Id { get; set; }

		[JsonProperty("instance_id")]
		public string InstanceId { get; set; }

		[JsonProperty("total_rounds")]
		public int TotalRounds { get; set; }

		[JsonProperty("moon_state")]
		public CloudInstanceMoonState MoonState { get; set; }
	}

	/// <summary>
	/// インスタンスのMoon/鳥状態（クラウド取得用）
	/// </summary>
	public class CloudInstanceMoonState
	{
		[JsonProperty("normal_round_count")]
		public int NormalRoundCount { get; set; }

		[JsonProperty("blood_moon_unlocked")]
		public bool BloodMoonUnlocked { get; set; }

		[JsonProperty("twilight_unlocked")]
		public bool TwilightUnlocked { get; set; }

		[JsonProperty("mystic_moon_unlocked")]
		public bool MysticMoonUnlocked { get; set; }

		[JsonProperty("solstice_unlocked")]
		public bool SolsticeUnlocked { get; set; }

		[JsonProperty("big_bird_encountered")]
		public bool BigBirdEncountered { get; set; }

		[JsonProperty("judgement_bird_encountered")]
		public bool JudgementBirdEncountered { get; set; }

		[JsonProperty("punishing_bird_encountered")]
		public bool PunishingBirdEncountered { get; set; }
	}

	#endregion
}
