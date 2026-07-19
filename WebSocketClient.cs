using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NAudio.Wave;
using Newtonsoft.Json;
using ToNStatTool.Services;

namespace ToNStatTool
{
	/// <summary>
	/// WebSocket通信を管理するクライアントクラス
	/// </summary>
	public partial class WebSocketClient
	{
		private ClientWebSocket webSocket;
		private CancellationTokenSource cancellationTokenSource;
		private readonly object dataLock = new object();

		// Constants
		private const int MAX_EVENTS = 500;

		// Events
		public event Action<string> OnConnected;
		public event Action OnDisconnected;
		public event Action<string> OnMessageReceived;
		public event Action<string> OnError;
		public event Action OnTerrorUpdate;
		public event Action OnRoundEnd;
		public event Action<ToNRoundType> OnRoundStart;
		public event Action OnInstanceStateChanged; // インスタンス状態変更イベント
		public event Action OnPlayerCountChanged; // プレイヤー数変更イベント
		public event Action OnItemReminderRoundEnd; // 8ページ/パニッシュド終了時のリマインダーイベント
		public event Action OnMasterChanged; // マスター変更イベント
		public event Action<SaveCodeInfo> OnSaveCodeReceived; // セーブコード受信イベント
		public event Action<bool> OnOptedInChanged; // ゲーム参加状態変更イベント
		public event Action<bool> OnCloudSyncStateChanged; // クラウド同期状態変更イベント（true=同期中, false=完了）
		private HashSet<string> warningUsers = new HashSet<string>();
		private IWavePlayer waveOutDevice;
		private AudioFileReader audioFileReader;

		// Properties
		public bool IsConnected { get; private set; }
		public string LocalPlayerName { get; private set; } = "";
		public string LocalPlayerUserId { get; private set; } = "";

		// Game Data
		public List<TerrorInfo> CurrentTerrors { get; private set; } = new List<TerrorInfo>();
		public Dictionary<string, PlayerInfo> Players { get; private set; } = new Dictionary<string, PlayerInfo>();
		public List<GameEvent> RecentEvents { get; private set; } = new List<GameEvent>();
		public Dictionary<string, object> GameData { get; private set; } = new Dictionary<string, object>();
		public List<RoundLog> RoundLogs { get; private set; } = new List<RoundLog>();
		public bool HasFetchedCloudRoundLogs { get; private set; } = false;
		public List<SaveCodeInfo> SaveCodes { get; private set; } = new List<SaveCodeInfo>();
		public SessionStats SessionStats { get; private set; } = new SessionStats();
		public const int MaxSaveCodes = 5; // 保持するセーブコードの最大数
		public RoundStats RoundStats { get; private set; } = new RoundStats();
		public TerrorStats TerrorStats { get; private set; } = new TerrorStats();
		public InstanceState InstanceState { get; private set; } = new InstanceState();

		// Round tracking
		private RoundLog currentRound = null;
		private string lastFinishedRoundTerrorNames = ""; // セーブコード用に最後のラウンドのテラー名を保持
		private readonly List<string> currentRoundItems = new List<string>();
		public event Action<string> OnWarningUserJoined;
		public event Action<string, bool> OnPlayerJoinLeave; // プレイヤー名, join=true/leave=false
		private bool isRoundActive = false;
		private bool wasDeadDuringRound = false; // ラウンド中に死亡したかを追跡
		private bool wasSaboteurDuringRound = false; // ラウンド中にサボタージュキラー側になったかを追跡
		private bool pendingSaboteurFlag = false; // ラウンド開始前のサボタージュ状態を保持
		private bool isCurrentRoundFirstMoon = false; // 今回のラウンドが初回Moonかどうか
		
		// ダブルドラブル検出用
		private bool isDoubleTroubleActive = false; // ダブルドラブルラウンド中かどうか
		private DateTime doubleTroubleStartTime = DateTime.MinValue; // ダブルドラブル開始時刻
		private bool hasReceivedRoundAnnouncement = false; // 接続後に正式なラウンドアナウンスを受けたか
		private string pendingCloudFetchUrl = null; // バッファ処理完了後にフェッチするURL
		private bool isFetchingCloudData = false; // クラウドフェッチ実行中フラグ

		// Sound settings
		public SoundSettings SoundSettings { get; private set; } = new SoundSettings();
		private const string SOUND_SETTINGS_FILE = "sound_settings.json";

		// Cloud service
		private CloudService cloudService;
		private const int MAX_ROUND_LOGS = 2000; // ラウンドログの最大保持数
		private bool isProcessingBufferedEvents = false; // バッファイベント処理中フラグ
		private readonly object audioLock = new object(); // 音声再生の排他制御用
		
		// インスタンス移動時のサウンドミュート用
		private bool isInstanceTransitioning = false; // インスタンス移動中フラグ
		private DateTime instanceTransitionStartTime = DateTime.MinValue; // 移動開始時刻
		private const int INSTANCE_TRANSITION_MUTE_SECONDS = 10; // ミュートする秒数
		private string lastInstanceUrl = ""; // 前回のインスタンスURL

		// インスタンス参加時の初期プレイヤーリスト受信時のバースト検出用
		private bool isReceivingInitialPlayerList = false; // 初期プレイヤーリスト受信中フラグ
		private DateTime initialPlayerListStartTime = DateTime.MinValue; // 初期リスト受信開始時刻
		private const int INITIAL_PLAYER_LIST_WINDOW_MS = 3000; // 初期リスト受信ウィンドウ（3秒）

		public WebSocketClient()
		{
			LoadWarningUsers();
			InitializeWarningSound();
			LoadSoundSettings();
			cloudService = new CloudService();
		}

		public async Task ConnectAsync(string url)
		{
			try
			{
				webSocket = new ClientWebSocket();
				cancellationTokenSource = new CancellationTokenSource();

				await webSocket.ConnectAsync(new Uri(url), cancellationTokenSource.Token);
				IsConnected = true;

				// データ受信を開始
				_ = Task.Run(async () => await ReceiveMessages());
			}
			catch (Exception ex)
			{
				IsConnected = false;
				OnError?.Invoke($"接続エラー: {ex.Message}");
			}
		}

		public async Task DisconnectAsync()
		{
			try
			{
				// 音声リソースを先に解放
				StopCurrentPlayback();

				if (webSocket != null)
				{
					// CancellationTokenSourceを先にキャンセル
					cancellationTokenSource?.Cancel();

					// WebSocketの状態をチェックしてから切断処理を実行
					if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.Connecting)
					{
						try
						{
							await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnected", CancellationToken.None);
						}
						catch (WebSocketException wsEx)
						{
							// WebSocketの状態エラーは無視（すでに切断されている可能性）
							System.Diagnostics.Debug.WriteLine($"WebSocket切断時の警告: {wsEx.Message}");
						}
					}

					webSocket.Dispose();
					webSocket = null;
				}

				// CancellationTokenSourceも破棄
				cancellationTokenSource?.Dispose();
				cancellationTokenSource = null;

				IsConnected = false;
				OnDisconnected?.Invoke();
			}
			catch (Exception ex)
			{
				OnError?.Invoke($"切断エラー: {ex.Message}");
			}
		}

		private async Task ReceiveMessages()
		{
			var buffer = new byte[4096];
			var messageBuilder = new StringBuilder();

			try
			{
				while (webSocket.State == WebSocketState.Open && !cancellationTokenSource.Token.IsCancellationRequested)
				{
					var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);

					if (result.MessageType == WebSocketMessageType.Text)
					{
						var messageFragment = Encoding.UTF8.GetString(buffer, 0, result.Count);
						messageBuilder.Append(messageFragment);

						if (result.EndOfMessage)
						{
							var completeMessage = messageBuilder.ToString();
							messageBuilder.Clear();

							ProcessReceivedMessage(completeMessage);
							OnMessageReceived?.Invoke(completeMessage);
						}
					}
					else if (result.MessageType == WebSocketMessageType.Close)
					{
						break;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// 正常な切断
			}
			catch (Exception ex)
			{
				OnError?.Invoke($"受信エラー: {ex.Message}");
			}
			finally
			{
				OnDisconnected?.Invoke();
			}
		}

		private void ProcessReceivedMessage(string message)
		{
			try
			{
				// WebSocket生メッセージをログに記録
				Logger.LogWebSocketMessage("RECV", message);

				var jsonData = JObject.Parse(message);
				lock (dataLock)
				{
					ProcessGameData(jsonData);
				}
			}
			catch (JsonReaderException jsonEx)
			{
				Logger.Error("WebSocket", $"JSON解析エラー: {jsonEx.Message}");
				Logger.Error("WebSocket", $"エラーメッセージ: {message.Substring(0, Math.Min(200, message.Length))}...");
			}
			catch (Exception ex)
			{
				Logger.Error("WebSocket", $"メッセージ処理エラー: {ex.Message}");
			}
		}

		private void AddGameEvent(string eventType, JObject rawData, string customDescription = null)
		{
			string description = customDescription ?? GetEventDescription(eventType, rawData);

			var gameEvent = new GameEvent
			{
				Type = eventType,
				Timestamp = DateTime.Now,
				Description = description,
				RawData = rawData
			};

			RecentEvents.Add(gameEvent);
		}

		private string GetEventDescription(string eventType, JObject rawData)
		{
			switch (eventType.ToUpper())
			{
				case "CONNECTED":
					return "WebSocketに接続しました";
				case "TERRORS":
					var terrorNames = rawData?["Names"] as JArray;
					if (terrorNames != null && terrorNames.Count > 0)
					{
						return $"テラー: {string.Join(", ", terrorNames)}";
					}
					return "テラーがリセットされました";
				case "ROUND_TYPE":
					var command = rawData?["Command"]?.ToObject<int>() ?? 0;
					var roundName = rawData?["Name"]?.ToString() ?? "Unknown";
					return command == 1 ? $"ラウンド開始: {roundName}" : $"ラウンド終了: {roundName}";
				case "LOCATION":
					var locCommand = rawData?["Command"]?.ToObject<int>() ?? 0;
					if (locCommand == 1)
					{
						var mapName = rawData?["Name"]?.ToString() ?? "Unknown";
						return $"マップ変更: {mapName}";
					}
					return "マップがリセットされました";
				case "PLAYER_JOIN":
					var joinName = rawData?["Value"]?.ToString() ?? "Unknown";
					return $"プレイヤー参加: {joinName}";
				case "PLAYER_LEAVE":
					var leaveName = rawData?["Value"]?.ToString() ?? "Unknown";
					return $"プレイヤー退出: {leaveName}";
				case "DEATH":
					var deathName = rawData?["Name"]?.ToString() ?? "Unknown";
					var deathMessage = rawData?["Message"]?.ToString() ?? "";
					return $"死亡: {deathName} - {deathMessage}";
				case "ALIVE":
					var isAlive = rawData?["Value"]?.ToObject<bool>() ?? false;
					return isAlive ? "復活しました" : "死亡しました";
				case "IS_SABOTEUR":
					var isSaboteur = rawData?["Value"]?.ToObject<bool>() ?? false;
					return isSaboteur ? "サボタージュ開始" : "サボタージュ終了";
				case "PAGE_COUNT":
					var pageCount = rawData?["Value"]?.ToObject<int>() ?? 0;
					return $"ページ収集: {pageCount}/8";
				case "ROUND_ACTIVE":
					var roundActive = rawData?["Value"]?.ToObject<bool>() ?? false;
					return roundActive ? "ラウンドがアクティブになりました" : "ラウンドが非アクティブになりました";
				case "ITEM":
					var itemCommand = rawData?["Command"]?.ToObject<int>() ?? 0;
					var itemName = rawData?["Name"]?.ToString() ?? "Unknown";
					return itemCommand == 1 ? $"アイテム取得: {itemName}" : $"アイテム放棄: {itemName}";
				default:
					return eventType;
			}
		}

		public string GetGameDataValue(string key, string defaultValue)
		{
			if (GameData.ContainsKey(key))
			{
				return GameData[key]?.ToString() ?? defaultValue;
			}
			return defaultValue;
		}

		public void CleanupOldData()
		{
			lock (dataLock)
			{
				// 古いイベントを削除
				if (RecentEvents.Count > MAX_EVENTS)
				{
					RecentEvents.RemoveRange(0, RecentEvents.Count - MAX_EVENTS);
				}

				// 古いプレイヤー情報を削除（60分以上見えていない）
				// ただし、自分自身は削除しない
				var cutoffTime = DateTime.Now.AddMinutes(-60);
				var playersToRemove = Players
					.Where(p => p.Value.LastSeen < cutoffTime && p.Key != LocalPlayerUserId)
					.Select(p => p.Key)
					.ToList();

				foreach (var playerId in playersToRemove)
				{
					System.Diagnostics.Debug.WriteLine($"古いプレイヤーを削除: {Players[playerId].Name}");
					Players.Remove(playerId);
				}
			}
		}

		public Dictionary<string, int> GetTerrorStats()
		{
			lock (dataLock)
			{
				return new Dictionary<string, int>(RoundStats.TerrorCounts);
			}
		}


	}
}
