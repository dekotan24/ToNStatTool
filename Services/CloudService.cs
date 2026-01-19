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
}
