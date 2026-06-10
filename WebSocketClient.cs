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
	public class WebSocketClient
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
		public event Action OnItemReminderRoundEnd; // 8ページ/アンバウンド終了時のリマインダーイベント
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

		/// <summary>
		/// 警告対象ユーザーリストを読み込む
		/// </summary>
		private void LoadWarningUsers()
		{
			try
			{
				string warningFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warn_user.txt");

				if (File.Exists(warningFilePath))
				{
					var lines = File.ReadAllLines(warningFilePath);
					warningUsers.Clear();

					foreach (var line in lines)
					{
						var username = line.Trim();
						if (!string.IsNullOrEmpty(username) && !username.StartsWith("#")) // #で始まる行はコメント扱い
						{
							warningUsers.Add(username.ToLowerInvariant());
							System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー登録: {username}");
						}
					}

					System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー数: {warningUsers.Count}");
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("[WARNING] warn_user.txtファイルが見つかりません");
					// ファイルが存在しない場合は空のファイルを作成
					File.WriteAllText(warningFilePath, "# 警告対象のユーザー名を1行1名で記入してください\n# #で始まる行はコメントです\n");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザーリスト読み込みエラー: {ex.Message}");
			}
		}

		/// <summary>
		/// 警告音を初期化
		/// </summary>
		private void InitializeWarningSound()
		{
			try
			{
				string soundFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warning.mp3");

				if (File.Exists(soundFilePath))
				{
					System.Diagnostics.Debug.WriteLine($"[WARNING] 警告音ファイルを確認: {soundFilePath}");
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("[WARNING] warning.mp3ファイルが見つかりません");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告音初期化エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// 警告音を再生（キュー使用）
		/// </summary>
		private void PlayWarningSound()
		{
			// サウンドが無効の場合は何もしない
			if (!SoundSettings.EnableWarningUserSound)
			{
				return;
			}

			try
			{
				// 設定からサウンドパスを取得
				string soundFilePath = SoundSettings.WarningUserSoundPath;
				
				// 設定にパスがない場合はデフォルトのwarning.mp3を使用
				if (string.IsNullOrEmpty(soundFilePath))
				{
					soundFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warning.mp3");
				}

				if (File.Exists(soundFilePath))
				{
					QueueSound(soundFilePath);
					System.Diagnostics.Debug.WriteLine($"[WARNING] 警告音をキュー: {soundFilePath}");
				}
				else
				{
					// ファイルがない場合はシステム音を使用
					System.Media.SystemSounds.Exclamation.Play();
					System.Diagnostics.Debug.WriteLine("[WARNING] サウンドファイルが見つからないためシステム音を使用");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告音再生エラー: {ex.Message}");
				// エラー時はシステム音にフォールバック
				System.Media.SystemSounds.Exclamation.Play();
			}
		}

		/// <summary>
		/// カスタムサウンドを再生（パスが空の場合はデフォルトのwarning.mp3を使用、キュー使用）
		/// </summary>
		public void PlayCustomSound(string soundPath, string defaultFileName = "warning.mp3")
		{
			try
			{
				string soundFilePath = soundPath;
				
				// パスが空の場合はデフォルトのファイルを使用
				if (string.IsNullOrEmpty(soundFilePath))
				{
					soundFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, defaultFileName);
				}

				if (File.Exists(soundFilePath))
				{
					QueueSound(soundFilePath);
					System.Diagnostics.Debug.WriteLine($"[SOUND] カスタムサウンドをキュー: {soundFilePath}");
				}
				else
				{
					System.Media.SystemSounds.Exclamation.Play();
					System.Diagnostics.Debug.WriteLine("[SOUND] サウンドファイルが見つからないためシステム音を使用");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[SOUND] サウンド再生エラー: {ex.Message}");
				System.Media.SystemSounds.Exclamation.Play();
			}
		}

		/// <summary>
		/// インスタンス移動中（サウンドミュート期間中）かどうかを判定
		/// </summary>
		private bool IsInInstanceTransition()
		{
			if (!isInstanceTransitioning)
				return false;
			
			// 指定秒数経過していたらフラグを解除
			if ((DateTime.Now - instanceTransitionStartTime).TotalSeconds > INSTANCE_TRANSITION_MUTE_SECONDS)
			{
				isInstanceTransitioning = false;
				Logger.Info("Instance", $"インスタンス移動ミュート期間終了（{INSTANCE_TRANSITION_MUTE_SECONDS}秒経過）");
				return false;
			}
			
			return true;
		}

		/// <summary>
		/// インスタンス参加直後の初期プレイヤーリスト受信中かどうかを判定
		/// インスタンス移動後、最初のPLAYER_JOINから一定時間内はtrue
		/// </summary>
		private bool IsReceivingInitialPlayerList()
		{
			// インスタンス移動中でない場合は対象外
			if (!isInstanceTransitioning && !isReceivingInitialPlayerList)
				return false;

			var now = DateTime.Now;

			// インスタンス移動中に最初のPLAYER_JOINが来た場合、初期リスト受信を開始
			if (isInstanceTransitioning && !isReceivingInitialPlayerList)
			{
				isReceivingInitialPlayerList = true;
				initialPlayerListStartTime = now;
				isInstanceTransitioning = false; // 移動中フラグはここで解除
				Logger.Info("Instance", "初期プレイヤーリスト受信開始");
				return false; // 最初の1件は通過させる
			}

			// 初期リスト受信中の場合、ウィンドウ時間内かチェック
			if (isReceivingInitialPlayerList)
			{
				var elapsed = (now - initialPlayerListStartTime).TotalMilliseconds;
				if (elapsed > INITIAL_PLAYER_LIST_WINDOW_MS)
				{
					// ウィンドウ時間を超えたので終了
					isReceivingInitialPlayerList = false;
					Logger.Info("Instance", $"初期プレイヤーリスト受信終了（{INITIAL_PLAYER_LIST_WINDOW_MS}ms経過）");
					return false;
				}

				// ウィンドウ時間内なのでスキップ
				System.Diagnostics.Debug.WriteLine($"[PLAYER_EVENT] 初期リスト受信中のためスキップ: {elapsed:F0}ms経過");
				return true;
			}

			return false;
		}

		/// <summary>
		/// 通知サウンドをミュートすべきかどうかを判定（パブリック）
		/// バッファイベント処理中またはインスタンス移動中の場合はtrueを返す
		/// </summary>
		public bool ShouldMuteNotificationSounds()
		{
			return isProcessingBufferedEvents || IsInInstanceTransition();
		}

		/// <summary>
		/// NAudioを使用してMP3ファイルを再生
		/// </summary>
		private void PlayMp3File(string filePath)
		{
			lock (audioLock)
			{
				try
				{
					// 既に再生中の場合は停止
					StopCurrentPlaybackInternal();

					// NAudioを使用してMP3を再生
					var newAudioReader = new AudioFileReader(filePath);
					var newWaveOut = new WaveOutEvent();
					
					newWaveOut.Init(newAudioReader);
					
					// フィールドに設定
					audioFileReader = newAudioReader;
					waveOutDevice = newWaveOut;

					// 再生完了時のイベントハンドラ
					waveOutDevice.PlaybackStopped += (sender, e) =>
					{
						Task.Run(() => StopCurrentPlayback());
					};

					waveOutDevice.Play();
					System.Diagnostics.Debug.WriteLine($"[SOUND] MP3再生開始: {filePath}");
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[SOUND] NAudio MP3再生エラー: {ex.Message}");

					// NAudioで失敗した場合はシステム音にフォールバック
					try
					{
						System.Media.SystemSounds.Exclamation.Play();
					}
					catch { }

					// リソースをクリーンアップ
					StopCurrentPlaybackInternal();
				}
			}
		}
		
		/// <summary>
		/// 現在の再生を停止してリソースを解放（ロックあり）
		/// </summary>
		private void StopCurrentPlayback()
		{
			lock (audioLock)
			{
				StopCurrentPlaybackInternal();
			}
		}

		/// <summary>
		/// 現在の再生を停止してリソースを解放（内部用、ロックなし）
		/// </summary>
		private void StopCurrentPlaybackInternal()
		{
			var device = waveOutDevice;
			var reader = audioFileReader;
			
			waveOutDevice = null;
			audioFileReader = null;

			// デバイスの停止
			if (device != null)
			{
				try
				{
					device.Stop();
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[SOUND] デバイス停止エラー: {ex.Message}");
				}

				// デバイスがreaderを解放する時間を確保
				Thread.Sleep(50);

				try
				{
					device.Dispose();
				}
				catch (Exception ex)
				{
					// RCW解放エラーは無視（別スレッドで使用中の可能性）
					System.Diagnostics.Debug.WriteLine($"[SOUND] デバイス解放エラー（無視）: {ex.Message}");
				}
			}

			// リーダーの解放
			if (reader != null)
			{
				try
				{
					reader.Dispose();
				}
				catch (Exception ex)
				{
					// RCW解放エラーは無視（別スレッドで使用中の可能性）
					System.Diagnostics.Debug.WriteLine($"[SOUND] リーダー解放エラー（無視）: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// ユーザーが警告対象かチェック
		/// </summary>
		public bool IsWarningUser(string playerName)
		{
			if (string.IsNullOrEmpty(playerName) || warningUsers.Count == 0)
				return false;

			var normalizedName = playerName.ToLowerInvariant().Trim();
			return warningUsers.Contains(normalizedName);
		}

		/// <summary>
		/// 警告ユーザーリストを再読み込み
		/// </summary>
		public void ReloadWarningUsers()
		{
			LoadWarningUsers();
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

		private void ProcessGameData(JObject jsonData)
		{
			try
			{
				string eventType = jsonData["Type"]?.ToString() ?? jsonData["TYPE"]?.ToString() ?? "";

				// イベントタイプをログに記録
				Logger.Debug("GameData", $"イベント処理開始: {eventType}");

				switch (eventType.ToUpper())
				{
					case "CONNECTED":
						ProcessConnectedEvent(jsonData);
						break;
					case "TERRORS":
						ProcessTerrorEvent(jsonData);
						break;
					case "ROUND_TYPE":
						ProcessRoundTypeEvent(jsonData);
						break;
					case "LOCATION":
						ProcessLocationEvent(jsonData);
						break;
					case "ROUND_ACTIVE":
						ProcessRoundActiveEvent(jsonData);
						break;
					case "ALIVE":
						ProcessAliveEvent(jsonData);
						break;
					case "IS_SABOTEUR":
						ProcessSaboteurEvent(jsonData);
						break;
					case "PAGE_COUNT":
						ProcessPageCountEvent(jsonData);
						break;
					case "ITEM":
						ProcessItemEvent(jsonData);
						break;
					case "PLAYER_JOIN":
						ProcessPlayerJoinEvent(jsonData);
						break;
					case "PLAYER_LEAVE":
						ProcessPlayerLeaveEvent(jsonData);
						break;
					case "DEATH":
						ProcessDeathEvent(jsonData);
						break;
					// 新しいイベント処理を追加
					case "INSTANCE":
						ProcessInstanceEvent(jsonData);
						break;
					case "STATS":
						ProcessStatsEvent(jsonData);
						break;
					case "TRACKER":
						ProcessTrackerEvent(jsonData);
						break;
					case "MASTER_CHANGE":
						ProcessMasterChangeEvent(jsonData);
						break;
					case "SAVED":
						ProcessSavedEvent(jsonData);
						break;
					case "OPTED_IN":
						ProcessOptedInEvent(jsonData);
						break;
					default:
						Logger.Warn("GameData", $"未処理のイベント: {eventType}");
						break;
				}

				// イベントログに追加
				AddGameEvent(eventType, jsonData);
			}
			catch (Exception ex)
			{
				Logger.Error("GameData", $"ゲームデータ処理エラー", ex);
				AddGameEvent("ERROR", null, $"データ処理エラー: {ex.Message}");
			}
		}

		private void ProcessConnectedEvent(JObject jsonData)
		{
			try
			{
				LocalPlayerName = SanitizePlayerName(jsonData["DisplayName"]?.ToString() ?? "Unknown");
				LocalPlayerUserId = jsonData["UserID"]?.ToString() ?? "";

				// 空の名前の場合の処理
				if (string.IsNullOrWhiteSpace(LocalPlayerName))
				{
					LocalPlayerName = $"You_{LocalPlayerUserId.Substring(0, Math.Min(8, LocalPlayerUserId.Length))}";
				}

				System.Diagnostics.Debug.WriteLine($"[CONNECTED] ローカルプレイヤー: '{LocalPlayerName}', ID: '{LocalPlayerUserId}'");

				// ラウンド統計、ラウンドログをリセット（接続時にリプレイデータが送られてくるため）
				ResetRoundStats();
				hasReceivedRoundAnnouncement = false;
				
				// 推定生存回数をリセット（他のインスタンス状態設定値はそのまま）
				InstanceState.EstimatedSurvivalCount = 0;
				System.Diagnostics.Debug.WriteLine("[CONNECTED] ラウンド統計、ラウンドログ、推定生存回数をリセットしました");

				// 既存のプレイヤーデータをクリア（接続時にリセット）
				Players.Clear();

				Players[LocalPlayerUserId] = new PlayerInfo
				{
					Name = LocalPlayerName,
					UserId = LocalPlayerUserId,
					IsLocal = true,
					IsAlive = true,
					LastSeen = DateTime.Now
				};

				OnConnected?.Invoke(LocalPlayerName);

				// バッファされたイベントを処理（サウンドを鳴らさない）
				if (jsonData["Args"] is JArray args)
				{
					Logger.Info("Connected", $"バッファイベント数: {args.Count}");

					// バッファ内のINSTANCEイベントを確認
					var instanceEvents = args.OfType<JObject>()
						.Where(a => (a["Type"]?.ToString() ?? a["TYPE"]?.ToString() ?? "").ToUpper() == "INSTANCE")
						.ToList();
					if (instanceEvents.Count > 0)
					{
						foreach (var ie in instanceEvents)
						{
							string instanceValue = ie["Value"]?.ToString() ?? "(null)";
							Logger.Info("Connected", $"バッファ内INSTANCEイベント: Value='{instanceValue}'");
						}
					}
					else
					{
						Logger.Warn("Connected", $"バッファ内にINSTANCEイベントがありません (現在のInstanceUrl={InstanceState.InstanceUrl})");
					}

					// バッファイベント処理中フラグを立てる（サウンドを鳴らさない）
					isProcessingBufferedEvents = true;
					try
					{
						// PLAYER_JOINイベントを先に処理
						foreach (var arg in args)
						{
							if (arg is JObject argObj)
							{
								string eventType = argObj["Type"]?.ToString() ?? argObj["TYPE"]?.ToString() ?? "";
								if (eventType.ToUpper() == "PLAYER_JOIN")
								{
									ProcessGameData(argObj);
								}
							}
						}

						// その後、他のイベントを処理
						foreach (var arg in args)
						{
							if (arg is JObject argObj)
							{
								string eventType = argObj["Type"]?.ToString() ?? argObj["TYPE"]?.ToString() ?? "";
								if (eventType.ToUpper() != "PLAYER_JOIN")
								{
									ProcessGameData(argObj);
								}
							}
						}
					}
					finally
					{
						isProcessingBufferedEvents = false;

						// リプレイ終了後、最終的なテラー情報を反映するために1回だけ更新イベントを発火
						OnTerrorUpdate?.Invoke();

						// バッファ処理中に遅延されたクラウドフェッチを実行
						if (!string.IsNullOrEmpty(pendingCloudFetchUrl))
						{
							string url = pendingCloudFetchUrl;
							pendingCloudFetchUrl = null;
							Logger.Info("Cloud", "バッファ処理完了、遅延クラウドフェッチを実行");
							_ = FetchInstanceStateFromCloudAsync(url);
						}
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[CONNECTED] エラー: {ex.Message}");
				AddGameEvent("ERROR", null, $"接続処理エラー: {ex.Message}");
			}
		}

		private void ProcessTerrorEvent(JObject jsonData)
		{
			int command = jsonData["Command"]?.ToObject<int>() ?? 0;

			if (command == 255) // Reset
			{
				CurrentTerrors.Clear();
				return;
			}

			if (command == 0 || command == 1) // Set or Revealed
			{
				CurrentTerrors.Clear();

				var names = jsonData["Names"] as JArray;
				if (names != null)
				{
					foreach (var nameToken in names)
					{
						string terrorName = nameToken.ToString();
						AddTerrorFromName(terrorName, jsonData);
					}
				}
				else
				{
					string displayName = jsonData["DisplayName"]?.ToString();
					if (!string.IsNullOrEmpty(displayName))
					{
						AddTerrorFromName(displayName, jsonData);
					}
				}

				// 鳥遭遇チェック（テラー表示時に即時チェック）
				CheckBirdEncounters();
			}

			// テラー更新イベントを発火（リプレイ中はスキップ）
			if (!isProcessingBufferedEvents)
			{
				OnTerrorUpdate?.Invoke();
			}
		}

		private void ProcessRoundTypeEvent(JObject jsonData)
		{
			int command = jsonData["Command"]?.ToObject<int>() ?? 0;
			string roundName = jsonData["Name"]?.ToString() ?? jsonData["DisplayName"]?.ToString() ?? "Unknown";
			int roundValue = jsonData["Value"]?.ToObject<int>() ?? -1;

			// ラウンドタイプをEnumに変換（Valueがあればそれを優先、なければ名前から変換）
			ToNRoundType roundType;
			if (roundValue >= 0)
			{
				roundType = ToNRoundTypeHelper.FromInt(roundValue);
			}
			else
			{
				roundType = ToNRoundTypeHelper.Parse(roundName);
			}

			// ラウンドタイプイベントの詳細をログに記録
			Logger.Info("RoundType", $"ROUND_TYPEイベント受信: Command={command}, Name='{roundName}', Value={roundValue}, Enum={roundType}");
			Logger.Debug("RoundType", $"生データ: {jsonData.ToString(Newtonsoft.Json.Formatting.None)}");

			if (command == 1) // Started
			{
				Logger.Info("RoundType", $"ラウンド開始処理: {roundType} ({roundName})");
				GameData["roundType"] = $"{ToNRoundTypeHelper.GetDisplayName(roundType)} (開始)";

				// 正式なラウンドアナウンスを受信したことを記録（バッファ処理中以外）
				if (!isProcessingBufferedEvents)
				{
					hasReceivedRoundAnnouncement = true;
				}
				
				// ダブルドラブルがアクティブなら終了させる
				if (isDoubleTroubleActive)
				{
					Logger.Info("RoundType", "ダブルドラブルがアクティブのため終了処理を実行");
					FinishDoubleTroubleRound();
				}
				
				// インスタンス移動ミュート期間を終了
				if (isInstanceTransitioning)
				{
					isInstanceTransitioning = false;
					Logger.Info("RoundType", "ラウンド開始によりインスタンス移動ミュート期間を終了");
				}
				
				// 現在のラウンド種別を記録（次ラウンド予測用）
				InstanceState.CurrentRoundType = roundType;
				
				// ラウンド開始時のNormalRoundCountを保存（予測計算用）
				InstanceState.NormalRoundCountAtRoundStart = InstanceState.NormalRoundCount;
				Logger.Debug("RoundType", $"ラウンド開始時のNormalRoundCount保存: {InstanceState.NormalRoundCountAtRoundStart}");
				
				// Moonラウンド開始時に即座に解禁フラグを立てる
				CheckMoonUnlockOnRoundStart(roundType);
				
				StartNewRound(roundType);
				Logger.Info("RoundType", $"ラウンド開始イベントを発火: {roundType}");
				
				// インスタンス状態変更を通知（次ラウンド予測更新用）
				OnInstanceStateChanged?.Invoke();
			}
			else if (command == 0) // Ended
			{
				Logger.Info("RoundType", $"ラウンド終了処理: {roundType} ({roundName})");
				GameData["roundType"] = $"{ToNRoundTypeHelper.GetDisplayName(roundType)} (終了)";
				
				// アイテムリマインダーチェック用に終了前のラウンドタイプを保存
				// (FinishCurrentRound等で値が変わる可能性があるため)
				var finishedRoundType = InstanceState.CurrentRoundType;
				
				FinishCurrentRound();

				// クラウドにラウンド情報を送信（ResetAllPlayersAliveの前に実行すること）
				SendRoundEndToCloud(finishedRoundType);

				ResetAllPlayersAlive();
				GameData["saboteur"] = "いいえ";

				// 上書きフラグをリセット
				InstanceState.IsCurrentRoundOverride = false;

				// ラウンド終了イベントを発火
				OnRoundEnd?.Invoke();
				Logger.Info("RoundType", $"ラウンド終了イベントを発火: {roundType}");

				// アイテムリマインダー対象ラウンドかチェック（Punished/8Pages）
				// 注意: 受信したroundType(Intermission)ではなく、終了前のラウンドタイプを使用
				bool isItemReminderRound = ToNRoundTypeHelper.IsItemReminderRound(finishedRoundType);
				
				// サボタージュでキラー側になった場合もアイテムリマインダー対象
				bool shouldRemindItem = isItemReminderRound || wasSaboteurDuringRound;
				
				Logger.Info("RoundType", $"アイテムリマインダーチェック: finishedRoundType={finishedRoundType}, IsItemReminderRound={isItemReminderRound}, wasSaboteur={wasSaboteurDuringRound}");
				System.Diagnostics.Debug.WriteLine($"[ITEM_REMINDER] finishedRoundType={finishedRoundType}, IsItemReminderRound={isItemReminderRound}, wasSaboteur={wasSaboteurDuringRound}");
				
				if (shouldRemindItem)
				{
					string reason = wasSaboteurDuringRound ? "サボタージュキラー" : finishedRoundType.ToString();
					Logger.Info("RoundType", $"アイテムリマインダーイベントを発火: {reason}");
					System.Diagnostics.Debug.WriteLine($"[ITEM_REMINDER] イベント発火: {reason}");
					OnItemReminderRoundEnd?.Invoke();
				}
			}
			else
			{
				Logger.Warn("RoundType", $"不明なCommand値: {command}, Name='{roundName}'");
			}
		}

		private void StartNewRound(ToNRoundType roundType)
		{
			string displayName = ToNRoundTypeHelper.GetDisplayName(roundType);
			Logger.Info("Round", $"StartNewRound呼び出し: roundType={roundType} ({displayName})");
			
			currentRoundItems.Clear();
			wasDeadDuringRound = false; // ラウンド開始時に死亡フラグをリセット

			// 全プレイヤーのラウンド内死亡フラグをリセット
			// （前ラウンド終了後〜開始までの間に届いた遅延DEATH/ALIVE=falseの影響を持ち越さない）
			foreach (var player in Players.Values)
			{
				player.DiedThisRound = false;
			}

			// サボタージュフラグの処理
			// Sabotageラウンドの場合、ラウンド開始前にIS_SABOTEUR=Trueが来ている可能性があるので
			// pendingSaboteurFlagを引き継ぐ
			if (roundType == ToNRoundType.Sabotage && pendingSaboteurFlag)
			{
				wasSaboteurDuringRound = true;
				Logger.Info("Round", "Sabotageラウンド開始: pendingSaboteurFlagからwasSaboteurDuringRoundをセット");
			}
			else
			{
				wasSaboteurDuringRound = false;
			}
			pendingSaboteurFlag = false; // pendingフラグは常にリセット
			
			// 上書きフラグを設定（通常確定時にOverrideラウンドが出た場合）
			// ただしMasterChanged（MC）による特殊の場合は上書きではない
			InstanceState.IsCurrentRoundOverride = false;
			if (InstanceState.NormalRoundCount == 0 && !InstanceState.MasterChanged)
			{
				if (ToNRoundTypeHelper.IsOverrideRound(roundType))
				{
					// Ghost, Unbound, 8Pagesはフルムーン状態問わず通常枠を上書きできる
					InstanceState.IsCurrentRoundOverride = true;
					Logger.Info("Round", $"通常確定時に{roundType}が上書き（NormalRoundCount={InstanceState.NormalRoundCount}）");
				}
				else if (roundType == ToNRoundType.Alternate && InstanceState.SolsticeUnlocked)
				{
					// Alternateはフルムーン（Solstice解禁）時のみ通常枠を上書きできる
					InstanceState.IsCurrentRoundOverride = true;
					Logger.Info("Round", $"フルムーン時にAlternateが通常枠を上書き（NormalRoundCount={InstanceState.NormalRoundCount}）");
				}
			}
			
			// アイテムリセット処理（ラウンドタイプによって異なる）
			if (roundType == ToNRoundType.Eight_Pages)
			{
				// 8ページ: Midn（ミッドレーダー）は持ち込み可能、それ以外はリセット
				if (InstanceState.CurrentItem != "Midn")
				{
					InstanceState.CurrentItem = "";
					Logger.Debug("Round", "8ページラウンドのためアイテムをリセット（Midn以外）");
				}
				else
				{
					Logger.Debug("Round", "8ページラウンドだがMidnを所持しているため保持");
				}
			}
			else if (roundType == ToNRoundType.Punished)
			{
				// パニッシュド: アイテムが没収されるためリセット
				InstanceState.CurrentItem = "";
				Logger.Debug("Round", "パニッシュドラウンドのためアイテムをリセット");
			}
			
			// マスター変更フラグをリセット（ラウンド開始で消費）
			InstanceState.MasterChanged = false;
			
			string mapName = GetGameDataValue("location", "Unknown").Split('(')[0].Trim();
			Logger.Debug("Round", $"マップ名: {mapName}");
			
			// ラウンド開始時の所持アイテムを取得
			string startingItem = InstanceState.CurrentItem ?? "";
			
			currentRound = new RoundLog
			{
				Timestamp = DateTime.Now,
				RoundType = roundType,
				MapName = mapName,
				TerrorNames = "",
				Items = string.IsNullOrEmpty(startingItem) ? "なし" : startingItem,
				Survived = false,
				WasOptedIn = InstanceState.IsOptedIn,  // ラウンド開始時の参加状態を記録
				IsReplay = isProcessingBufferedEvents  // リプレイ（バッファ処理中）かどうか
			};

			// ラウンド開始イベントを発火
			OnRoundStart?.Invoke(roundType);

			Logger.Info("Round", $"新しいラウンド開始: {displayName}, マップ: {mapName}, 参加状態: {InstanceState.IsOptedIn}");
		}

		private void FinishCurrentRound()
		{
			Logger.Info("Round", "FinishCurrentRound呼び出し");
			
			if (currentRound == null)
			{
				Logger.Warn("Round", "currentRoundがnullです。ラウンドが開始されていない可能性があります。");
				return;
			}

			try
			{
				// テラー名を設定
				if (CurrentTerrors.Count > 0)
				{
					currentRound.TerrorNames = string.Join(", ", CurrentTerrors.Select(t => t.Name));
				}
				else
				{
					currentRound.TerrorNames = "Unknown";
				}

				// アイテムはラウンド開始時に設定済み（InstanceState.CurrentItem）
				// currentRoundItemsはラウンド中の取得アイテム追跡用なので、Itemsは上書きしない

				// 生存状態を確認（ラウンド中に一度でも死亡していれば死亡として記録）
				bool survived = !wasDeadDuringRound;

				// フラグが設定されていない場合のフォールバック（従来のロジック）
				if (!wasDeadDuringRound)
				{
					// プレイヤー情報からも確認
					if (!string.IsNullOrEmpty(LocalPlayerUserId) && Players.ContainsKey(LocalPlayerUserId))
					{
						var localPlayer = Players[LocalPlayerUserId];
						survived = localPlayer.IsAlive && !localPlayer.DiedThisRound;
						System.Diagnostics.Debug.WriteLine($"プレイヤー情報から生存状態を取得: {survived}");
					}
					// GameDataからも確認
					else
					{
						string aliveStatus = GetGameDataValue("alive", "");
						if (!string.IsNullOrEmpty(aliveStatus) && aliveStatus != "-")
						{
							survived = aliveStatus == "生存";
							System.Diagnostics.Debug.WriteLine($"GameDataから生存状態を取得: {aliveStatus}");
						}
					}
				}
				else
				{
					System.Diagnostics.Debug.WriteLine("ラウンド中に死亡したため、死亡として記録します");
				}

				// 全員死亡チェック: 全プレイヤーが死亡している場合は生存をfalseに修正
				// （TPKシナリオでALIVE=falseがROUND_TYPE終了より後に届く場合の安全弁）
				int roundAliveCount = CountRoundSurvivors();
				int roundTotalCount = Players.Count;
				if (survived && roundAliveCount == 0 && roundTotalCount > 0)
				{
					survived = false;
					System.Diagnostics.Debug.WriteLine($"全プレイヤーが死亡しているため、生存をfalseに修正 (aliveCount=0/{roundTotalCount})");
				}

				currentRound.Survived = survived;
				currentRound.AliveCount = roundAliveCount;
				currentRound.TotalPlayerCount = roundTotalCount;

				// ログに追加
				RoundLogs.Add(currentRound);
				Logger.Info("Round", $"ラウンドログに記録: {currentRound.RoundTypeDisplayName} - {(survived ? "生存" : "死亡")} - テラー: {currentRound.TerrorNames}");

				// 統計を更新（クラウドマージ時の再集計と同じく、参加していたラウンドの生存のみカウント）
				RoundStats.TotalRounds++;
				if (currentRound.WasOptedIn && survived)
				{
					RoundStats.SurvivedRounds++;
				}

				// ラウンド種別の統計も更新（Enumベース）
				RoundStats.IncrementCount(currentRound.RoundType);

				// プレイヤーごとの経過ラウンド数・生存数を更新（RP時はスキップ）
				if (!isProcessingBufferedEvents)
				{
					foreach (var player in Players.Values)
					{
						player.RoundCount++;
						if (player.IsAlive && !player.DiedThisRound)
						{
							player.SurvivalCount++;
						}
					}
				}

				// テラー統計更新
				string roundTerrorKey = currentRound.TerrorNames;
				var splitNames = roundTerrorKey.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string terror in splitNames)
				{
					if (TerrorStats.TerrorTypeCounts.ContainsKey(terror))
					{
						TerrorStats.TerrorTypeCounts[terror]++;
					}
					else
					{
						TerrorStats.TerrorTypeCounts[terror] = 1;
					}
				}

				// InstanceState更新（ラウンド予測用）
				UpdateInstanceState(currentRound.RoundType, survived, splitNames);

				// ラウンドログを最大件数に制限
				while (RoundLogs.Count > MAX_ROUND_LOGS)
				{
					RoundLogs.RemoveAt(0);
				}
				
				Logger.Info("Round", "FinishCurrentRound完了");
			}
			catch (Exception ex)
			{
				Logger.Error("Round", "ラウンド記録エラー", ex);
			}
			finally
			{
				// セーブコード用にテラー名を保存（currentRoundをnullにする前に）
				if (currentRound != null)
				{
					lastFinishedRoundTerrorNames = currentRound.TerrorNames ?? "";
				}
				currentRound = null;
			}
		}

		/// <summary>
		/// ダブルドラブルラウンドを開始する（Intermission中にDEATHイベントが来た場合）
		/// </summary>
		private void StartDoubleTroubleRound()
		{
			Logger.Info("DoubleTrouble", "StartDoubleTroubleRound呼び出し");
			
			isDoubleTroubleActive = true;
			doubleTroubleStartTime = DateTime.Now;
			
			// ダブルドラブル用のラウンドログを作成
			string mapName = GetGameDataValue("location", "Unknown").Split('(')[0].Trim();
			string startingItem = InstanceState.CurrentItem ?? "";
			
			currentRound = new RoundLog
			{
				Timestamp = DateTime.Now,
				RoundType = ToNRoundType.Double_Trouble,
				MapName = mapName,
				TerrorNames = "???", // ダブルドラブルはテラー名がアナウンスされない
				Items = string.IsNullOrEmpty(startingItem) ? "なし" : startingItem,
				Survived = true, // 通常と同じく、初期値は生存
				WasOptedIn = InstanceState.IsOptedIn,
				IsReplay = isProcessingBufferedEvents  // リプレイ（バッファ処理中）かどうか
			};
			
			// フラグ設定（通常のラウンドと同じ）
			currentRoundItems.Clear();
			CurrentTerrors.Clear(); // 前ラウンドのテラー情報をクリア
			wasDeadDuringRound = false; // 通常と同じく、初期値は生存
			wasSaboteurDuringRound = false;
			pendingSaboteurFlag = false;

			// 全プレイヤーのラウンド内死亡フラグをリセット
			// （この直後に検出契機となったDEATHイベントの本処理でフラグが立つ）
			foreach (var player in Players.Values)
			{
				player.DiedThisRound = false;
			}
			
			// 現在のラウンド種別を記録
			InstanceState.CurrentRoundType = ToNRoundType.Double_Trouble;
			InstanceState.NormalRoundCountAtRoundStart = InstanceState.NormalRoundCount;
			
			// UI表示用のGameDataを更新（メインフォームに反映）
			GameData["roundType"] = "Double Trouble";
			GameData["roundActive"] = "アクティブ";
			
			// ラウンド開始イベントを発火
			OnRoundStart?.Invoke(ToNRoundType.Double_Trouble);
			
			// インスタンス状態変更を通知（UI更新用）
			OnInstanceStateChanged?.Invoke();
			
			Logger.Info("DoubleTrouble", $"ダブルドラブルラウンド開始: マップ={mapName}");
		}
		
		/// <summary>
		/// ダブルドラブルラウンドを終了する（次のラウンド開始時に呼び出される）
		/// </summary>
		private void FinishDoubleTroubleRound()
		{
			if (!isDoubleTroubleActive)
			{
				return;
			}
			
			Logger.Info("DoubleTrouble", "FinishDoubleTroubleRound呼び出し");
			
			isDoubleTroubleActive = false;
			
			if (currentRound != null)
			{
				// テラー名を設定（利用可能な場合）
				if (CurrentTerrors.Count > 0)
				{
					currentRound.TerrorNames = string.Join(", ", CurrentTerrors.Select(t => t.Name));
				}
				else
				{
					currentRound.TerrorNames = "Unknown (Double Trouble)";
				}
				
				// 通常のラウンドと同じ生死判定
				// wasDeadDuringRoundフラグで判定（ALIVE=falseが来たかどうか）
				bool survived = !wasDeadDuringRound && !wasSaboteurDuringRound;

				// 全員死亡チェック
				int dtAliveCount = CountRoundSurvivors();
				int dtTotalCount = Players.Count;
				if (survived && dtAliveCount == 0 && dtTotalCount > 0)
				{
					survived = false;
					System.Diagnostics.Debug.WriteLine($"[DoubleTrouble] 全プレイヤーが死亡しているため、生存をfalseに修正");
				}

				currentRound.Survived = survived;
				currentRound.AliveCount = dtAliveCount;
				currentRound.TotalPlayerCount = dtTotalCount;

				// ログに追加
				RoundLogs.Add(currentRound);
				Logger.Info("DoubleTrouble", $"ラウンドログに記録: {currentRound.RoundTypeDisplayName} - {(survived ? "生存" : "死亡")} - テラー: {currentRound.TerrorNames}");

				// 統計を更新（クラウドマージ時の再集計と同じく、参加していたラウンドの生存のみカウント）
				RoundStats.TotalRounds++;
				if (currentRound.WasOptedIn && survived)
				{
					RoundStats.SurvivedRounds++;
				}

				// プレイヤーごとの経過ラウンド数・生存数を更新（RP時はスキップ）
				if (!isProcessingBufferedEvents)
				{
					foreach (var player in Players.Values)
					{
						player.RoundCount++;
						if (player.IsAlive && !player.DiedThisRound)
						{
							player.SurvivalCount++;
						}
					}
				}
				
				// ラウンド種別の統計も更新
				RoundStats.IncrementCount(currentRound.RoundType);
				
				// テラー統計更新（Unknown (Double Trouble)の場合はスキップ）
				if (currentRound.TerrorNames != "Unknown (Double Trouble)")
				{
					string roundTerrorKey = currentRound.TerrorNames;
					var splitNames = roundTerrorKey.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
					foreach (string terror in splitNames)
					{
						if (TerrorStats.TerrorTypeCounts.ContainsKey(terror))
						{
							TerrorStats.TerrorTypeCounts[terror]++;
						}
						else
						{
							TerrorStats.TerrorTypeCounts[terror] = 1;
						}
					}
					
					// InstanceState更新（Double_Troubleは特殊ラウンド扱い）
					UpdateInstanceState(currentRound.RoundType, survived, splitNames);
				}
				else
				{
					// テラー名がUnknown (Double Trouble)の場合はテラーなしでInstanceState更新
					UpdateInstanceState(currentRound.RoundType, survived, new string[0]);
				}
				
				// ラウンドログを最大件数に制限
				while (RoundLogs.Count > MAX_ROUND_LOGS)
				{
					RoundLogs.RemoveAt(0);
				}
				
				// セーブコード用にテラー名を保存
				lastFinishedRoundTerrorNames = currentRound.TerrorNames ?? "";
				
				Logger.Info("DoubleTrouble", "FinishDoubleTroubleRound完了");
			}
			
			// クラウドにラウンド情報を送信（ResetAllPlayersAliveの前に実行すること）
			// 接続後に正式なラウンドアナウンスを受けていない場合はスキップ
			// （接続直後の未アナウンスラウンドをダブトラと誤判定する可能性があるため）
			if (hasReceivedRoundAnnouncement && !isProcessingBufferedEvents)
			{
				SendRoundEndToCloud(ToNRoundType.Double_Trouble);
			}
			else
			{
				Logger.Debug("DoubleTrouble", "クラウド送信スキップ: ラウンドアナウンス未受信またはバッファ処理中");
			}

			// ラウンド終了イベントを発火
			OnRoundEnd?.Invoke();

			// プレイヤーの生存状態をリセット
			ResetAllPlayersAlive();
			GameData["saboteur"] = "いいえ";

			currentRound = null;
		}

		/// <summary>
		/// Moonラウンド開始時に解禁フラグを立てる
		/// </summary>
		private void CheckMoonUnlockOnRoundStart(ToNRoundType roundType)
		{
			bool stateChanged = false;

			// 初回Moonフラグをリセット
			isCurrentRoundFirstMoon = false;
			InstanceState.IsCurrentRoundFirstMoon = false;

			// ※JustUnlockedフラグは初回判定に使用するため、チェック後にリセットする

			// ※Midnightは開始時には解禁しない（ラウンド終了時に生存者がいる場合のみBlood Moon解禁）

			if (roundType == ToNRoundType.Blood_Moon)
			{
				// BloodMoonJustUnlocked=true（ミッドナイト生存直後）の場合も初回扱い
				if (!InstanceState.BloodMoonUnlocked || InstanceState.BloodMoonJustUnlocked)
				{
					isCurrentRoundFirstMoon = true; // 初回Blood Moon
					InstanceState.IsCurrentRoundFirstMoon = true;
					InstanceState.BloodMoonUnlocked = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Blood Moon解禁（初回、ラウンド開始時）");
				}
			}
			if (roundType == ToNRoundType.Twilight)
			{
				if (!InstanceState.TwilightUnlocked)
				{
					isCurrentRoundFirstMoon = true; // 初回Twilight
					InstanceState.IsCurrentRoundFirstMoon = true;
					InstanceState.TwilightUnlocked = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Twilight解禁（初回、ラウンド開始時）");
				}
			}
			if (roundType == ToNRoundType.Mystic_Moon)
			{
				if (!InstanceState.MysticMoonUnlocked)
				{
					isCurrentRoundFirstMoon = true; // 初回Mystic Moon
					InstanceState.IsCurrentRoundFirstMoon = true;
					InstanceState.MysticMoonUnlocked = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Mystic Moon解禁（初回、ラウンド開始時）");
				}
			}
			if (roundType == ToNRoundType.Solstice)
			{
				if (!InstanceState.SolsticeUnlocked)
				{
					isCurrentRoundFirstMoon = true; // 初回Solstice
					InstanceState.IsCurrentRoundFirstMoon = true;
					InstanceState.SolsticeUnlocked = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Solstice解禁（初回、ラウンド開始時）");
				}
			}

			// JustUnlockedフラグをリセット（次のラウンド予測に影響しないように）
			// ※初回判定で使用した後にリセットする
			InstanceState.BloodMoonJustUnlocked = false;
			InstanceState.TwilightJustUnlocked = false;
			InstanceState.MysticMoonJustUnlocked = false;

			// 状態が変化した場合はイベントを発火（チェックボックス更新用）
			if (stateChanged)
			{
				OnInstanceStateChanged?.Invoke();
			}
		}

		/// <summary>
		/// 現在のテラーから鳥遭遇をチェック（即時更新）
		/// </summary>
		private void CheckBirdEncounters()
		{
			bool stateChanged = false;

			foreach (var terror in CurrentTerrors)
			{
				string terrorLower = terror.Name.ToLower();
				if (terrorLower.Contains("big bird") && !InstanceState.MetBigBird)
				{
					InstanceState.MetBigBird = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Big Bird遭遇（即時）");
				}
				if (terrorLower.Contains("judgement bird") && !InstanceState.MetJudgementBird)
				{
					InstanceState.MetJudgementBird = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Judgement Bird遭遇（即時）");
				}
				if (terrorLower.Contains("punishing bird") && !InstanceState.MetPunishingBird)
				{
					InstanceState.MetPunishingBird = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Punishing Bird遭遇（即時）");
				}
			}

			// 状態が変化した場合はイベントを発火
			if (stateChanged)
			{
				OnInstanceStateChanged?.Invoke();
			}
		}

		/// <summary>
		/// インスタンス状態を更新（ラウンド予測用）
		/// </summary>
		private void UpdateInstanceState(ToNRoundType roundType, bool survived, string[] terrorNames)
		{
			// ラウンド終了時の状態をログ出力
			Logger.Info("InstanceState", $"UpdateInstanceState呼び出し: roundType={roundType}, isCurrentRoundFirstMoon={isCurrentRoundFirstMoon}");
			Logger.Info("InstanceState", $"更新前: NormalRoundCount={InstanceState.NormalRoundCount}, InstanceState.WasOverrideInUncertainState={InstanceState.WasOverrideInUncertainState}");
			
			// インスタンス内の誰かが生存しているかチェック（推定生存回数用）
			// 全滅(TPK)時にリスポーンのALIVE/TRACKER更新が先に届いてもカウントしないよう、
			// ラウンド内死亡フラグを考慮した集計を使用する
			int aliveCount = CountRoundSurvivors();
			bool anyoneSurvived = aliveCount > 0;
			System.Diagnostics.Debug.WriteLine($"[InstanceState] ラウンド終了時の生存状況: 自分={survived}, インスタンス内生存者={aliveCount}人, anyoneSurvived={anyoneSurvived}");

			// インスタンス内で誰かが生存していればカウントアップ
			if (anyoneSurvived)
			{
				InstanceState.EstimatedSurvivalCount++;

				// 特殊解放チェック（3回生存）
				if (InstanceState.EstimatedSurvivalCount >= 3 && !InstanceState.SpecialUnlocked)
				{
					InstanceState.SpecialUnlocked = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] 特殊ラウンド解放");
				}

				// Mystic Moon解禁チェック（15回生存）
				if (InstanceState.EstimatedSurvivalCount >= 15 && !InstanceState.MysticMoonUnlocked)
				{
					// 次のラウンドでMystic Moonが来る可能性
					System.Diagnostics.Debug.WriteLine("[InstanceState] Mystic Moon解禁条件達成");
				}
			}

			// Midnightラウンド終了時のチェック（誰かが生存していればBlood Moon解禁）
			if (roundType == ToNRoundType.Midnight)
			{
				// インスタンス内の誰かが生存しているかチェック（aliveCountは既に上で計算済み）
				int totalCount = Players.Count;
				System.Diagnostics.Debug.WriteLine($"[InstanceState] Midnight終了時チェック: 生存{aliveCount}/{totalCount}人, BloodMoon解禁={InstanceState.BloodMoonUnlocked}");
				
				// 生存者が1人でもいればBlood Moon解禁
				if (aliveCount > 0 && !InstanceState.BloodMoonUnlocked)
				{
					InstanceState.MidnightSurvived = true;
					InstanceState.BloodMoonUnlocked = true;
					InstanceState.BloodMoonJustUnlocked = true; // 解禁直後フラグをセット（次ラウンドがBlood Moonの可能性が高い）
					System.Diagnostics.Debug.WriteLine("[InstanceState] Midnight生存者あり → Blood Moon解禁 (JustUnlocked=true)");
					OnInstanceStateChanged?.Invoke();
				}
			}

			// 鳥遭遇チェック（生存に関係なく遭遇でカウント）
			foreach (var terror in terrorNames)
			{
				string terrorLower = terror.ToLower();
				if (terrorLower.Contains("big bird") && !InstanceState.MetBigBird)
				{
					InstanceState.MetBigBird = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Big Bird遭遇");
				}
				if (terrorLower.Contains("judgement bird") && !InstanceState.MetJudgementBird)
				{
					InstanceState.MetJudgementBird = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Judgement Bird遭遇");
				}
				if (terrorLower.Contains("punishing bird") && !InstanceState.MetPunishingBird)
				{
					InstanceState.MetPunishingBird = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Punishing Bird遭遇");
				}
			}

			// Moonラウンド解禁チェック（ラウンド終了時）
			if (roundType == ToNRoundType.Blood_Moon)
			{
				InstanceState.BloodMoonUnlocked = true;
				System.Diagnostics.Debug.WriteLine("[InstanceState] Blood Moon解禁");
			}
			if (roundType == ToNRoundType.Twilight)
			{
				InstanceState.TwilightUnlocked = true;
				System.Diagnostics.Debug.WriteLine("[InstanceState] Twilight解禁");
			}
			if (roundType == ToNRoundType.Mystic_Moon)
			{
				InstanceState.MysticMoonUnlocked = true;
				System.Diagnostics.Debug.WriteLine("[InstanceState] Mystic Moon解禁");
			}
			if (roundType == ToNRoundType.Solstice)
			{
				InstanceState.SolsticeUnlocked = true;
				System.Diagnostics.Debug.WriteLine("[InstanceState] Solstice解禁");
			}

			// ラウンド周期の更新
			// N=0: 通常枠確定, N=1: 通常/特殊どちらか, N=2: 特殊枠確定
			if (ToNRoundTypeHelper.IsNormalRound(roundType))
			{
				// Normal系: 純粋な通常ラウンド（Classic, RUN）
				if (InstanceState.WasOverrideInUncertainState)
				{
					// N=1でOverride後にNormalが来た → 前のOverrideが特殊枠を食ったことが確定
					// なのでNormalはN=0からの遷移として扱う → N=1
					InstanceState.NormalRoundCount = 1;
					InstanceState.WasOverrideInUncertainState = false;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Normal(前のOverrideが特殊枠消費確定): NormalRoundCount=1");
				}
				else if (InstanceState.NormalRoundCount >= 2)
				{
					// N=2（特殊枠確定）でNormalが出た → 特殊未解放時
					// 特殊枠は消費されたが特殊が出せないのでNormalが代わりに出た
					InstanceState.NormalRoundCount = 0;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Normal(特殊未解放時): 特殊枠消費 → NormalRoundCount=0");
				}
				else
				{
					// N=0 → N=1, N=1 → N=2
					InstanceState.NormalRoundCount++;
					System.Diagnostics.Debug.WriteLine($"[InstanceState] Normal: NormalRoundCount={InstanceState.NormalRoundCount}");
				}
				
				// 通常が3回連続 → インスタンス作成者確定、特殊未解放
				if (InstanceState.NormalRoundCount >= 3 && !InstanceState.IsInstanceOwner)
				{
					InstanceState.IsInstanceOwner = true;
					InstanceState.SpecialUnlocked = false;
					InstanceState.EstimatedSurvivalCount = RoundStats.SurvivedRounds;
					System.Diagnostics.Debug.WriteLine("[InstanceState] インスタンス作成者と判定");
				}
			}
			else if (ToNRoundTypeHelper.IsMoonRound(roundType))
			{
				// Moonラウンド（Blood Moon/Twilight/Mystic Moon/Solstice）
				// 初回: Classicを上書きして出現 → Override系と同じ挙動
				// 2回目以降: 特殊ラウンドの1/20で選出 → 特殊枠を消費
				InstanceState.WasOverrideInUncertainState = false; // Moonが出たらフラグリセット
				
				if (isCurrentRoundFirstMoon)
				{
					// 初回Moon → Override系と同じ挙動
					if (InstanceState.NormalRoundCount == 0)
					{
						InstanceState.NormalRoundCount = 1;
						System.Diagnostics.Debug.WriteLine("[InstanceState] 初回Moon(通常枠確定): NormalRoundCount=1");
					}
					else if (InstanceState.NormalRoundCount == 1)
					{
						// N=1で初回Moon → N=1維持（通常枠を上書きした可能性）
						InstanceState.WasOverrideInUncertainState = true; // 初回MoonもOverride系と同様にフラグを立てる
						System.Diagnostics.Debug.WriteLine("[InstanceState] 初回Moon(不明): NormalRoundCount=1維持");
					}
					else if (InstanceState.NormalRoundCount >= 2)
					{
						InstanceState.NormalRoundCount = 0;
						System.Diagnostics.Debug.WriteLine("[InstanceState] 初回Moon(特殊枠消費): NormalRoundCount=0");
					}
				}
				else
				{
					// 2回目以降Moon → 特殊ラウンドとして扱う（特殊枠を消費）
					InstanceState.NormalRoundCount = 0;
					System.Diagnostics.Debug.WriteLine("[InstanceState] 2回目以降Moon(特殊枠消費): NormalRoundCount=0");
				}
			}
			else if (ToNRoundTypeHelper.IsOverrideRound(roundType))
			{
				// Run/Ghost/Unbound/8Pages: 通常枠でも特殊枠でも出現可能
				if (InstanceState.NormalRoundCount == 0)
				{
					// N=0（通常枠確定） → N=1
					InstanceState.NormalRoundCount = 1;
					InstanceState.WasOverrideInUncertainState = false;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Override系(通常枠確定): NormalRoundCount=1");
				}
				else if (InstanceState.NormalRoundCount == 1)
				{
					// N=1（通常/特殊どちらか） → N=1維持（どちらで出たか不明）
					InstanceState.WasOverrideInUncertainState = true; // 不確定フラグを立てる
					System.Diagnostics.Debug.WriteLine("[InstanceState] Override系(不明): NormalRoundCount=1維持, 不確定フラグON");
				}
				else if (InstanceState.NormalRoundCount >= 2)
				{
					// N=2（特殊枠確定） → N=0（特殊枠消費）
					InstanceState.NormalRoundCount = 0;
					InstanceState.WasOverrideInUncertainState = false;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Override系(特殊枠消費): NormalRoundCount=0");
				}
			}
			else if (ToNRoundTypeHelper.IsSpecialRound(roundType))
			{
				// 特殊ラウンド
				if (InstanceState.IsCurrentRoundOverride)
				{
					// 上書きで出た特殊（MCなしで通常確定時に出現）→ 通常枠を消費したのでN=1
					InstanceState.NormalRoundCount = 1;
					InstanceState.WasOverrideInUncertainState = false;
					System.Diagnostics.Debug.WriteLine("[InstanceState] 特殊ラウンド(上書き): NormalRoundCount=1");
				}
				else
				{
					// 通常の特殊ラウンド（MCまたは通常枠2消費後）→ 特殊枠消費でN=0
					InstanceState.NormalRoundCount = 0;
					InstanceState.WasOverrideInUncertainState = false;
					System.Diagnostics.Debug.WriteLine("[InstanceState] 特殊ラウンド: NormalRoundCount=0");
				}
			}

			InstanceState.LastRoundType = roundType;

			// 状態が変化したのでUIに通知（推定生存回数等の更新用）
			OnInstanceStateChanged?.Invoke();
		}

		/// <summary>
		/// インスタンス状態をリセット
		/// </summary>
		public void ResetInstanceState()
		{
			InstanceState.Reset();
			
			// ダブルドラブルフラグもリセット
			if (isDoubleTroubleActive)
			{
				Logger.Info("DoubleTrouble", "インスタンスリセットによりダブルドラブルを終了");
				isDoubleTroubleActive = false;
				currentRound = null;
			}
			
			System.Diagnostics.Debug.WriteLine("[InstanceState] リセット");
		}

		/// <summary>
		/// 現在のラウンドを生き残ったプレイヤー数をカウント
		/// 全滅(TPK)時はラウンド終了通知より先にリスポーンのALIVE/TRACKER更新が届いて
		/// IsAliveがtrueに戻ることがあるため、IsAliveだけでなくDiedThisRoundも参照する
		/// </summary>
		private int CountRoundSurvivors()
		{
			return Players.Values.Count(p => p.IsAlive && !p.DiedThisRound);
		}

		/// <summary>
		/// プレイヤー名からプレイヤーを検索する
		/// 似た名前のプレイヤー（例: "Ken" と "Kenta"）への誤マッチを防ぐため、
		/// 完全一致 → 正規化一致 → 部分一致 の優先順位で評価する
		/// 見つからない場合はKeyがnullのペアを返す
		/// </summary>
		private KeyValuePair<string, PlayerInfo> FindPlayerByName(string playerName)
		{
			if (string.IsNullOrEmpty(playerName))
				return default(KeyValuePair<string, PlayerInfo>);

			// 1. 完全一致（名前またはID）
			var match = Players.FirstOrDefault(p => p.Value.Name == playerName || p.Key == playerName);
			if (match.Key != null)
				return match;

			// 2. 正規化一致（空白・記号・大文字小文字の違いを無視）
			string normalized = NormalizePlayerName(playerName);
			if (!string.IsNullOrEmpty(normalized))
			{
				match = Players.FirstOrDefault(p => NormalizePlayerName(p.Value.Name) == normalized);
				if (match.Key != null)
					return match;
			}

			// 3. 部分一致（最後の手段: 名前の切り詰め等の対策。最も長く一致する候補を優先）
			return Players
				.Where(p => p.Value.Name.Contains(playerName) || playerName.Contains(p.Value.Name))
				.OrderByDescending(p => Math.Min(p.Value.Name.Length, playerName.Length))
				.FirstOrDefault();
		}

		private void ResetAllPlayersAlive()
		{
			System.Diagnostics.Debug.WriteLine("[RESET] 全プレイヤーを生存状態にリセット");

			foreach (var player in Players.Values)
			{
				if (!player.IsAlive)
				{
					System.Diagnostics.Debug.WriteLine($"  - {player.Name}: 死亡 → 生存");
				}
				player.IsAlive = true;
				player.DiedThisRound = false;
				player.LastSeen = DateTime.Now;
			}

			// プレイヤー数変更イベントを発火
			OnPlayerCountChanged?.Invoke();
		}

		private void ProcessLocationEvent(JObject jsonData)
		{
			int command = jsonData["Command"]?.ToObject<int>() ?? 0;
			if (command == 1) // Set
			{
				string mapName = jsonData["Name"]?.ToString() ?? "Unknown";
				string creator = jsonData["Creator"]?.ToString() ?? "";
				string origin = jsonData["Origin"]?.ToString() ?? "";

				string locationInfo = mapName;
				if (!string.IsNullOrEmpty(creator))
					locationInfo += $" (作者: {creator})";
				if (!string.IsNullOrEmpty(origin))
					locationInfo += $" [{origin}]";

				GameData["location"] = locationInfo;
				
				// ラウンド中でcurrentRoundのマップ名が空または"-"の場合は更新
				// （サボタージュキラー側ではROUND_TYPEがLOCATIONより先に来る場合がある）
				if (isRoundActive && currentRound != null && 
				    (string.IsNullOrEmpty(currentRound.MapName) || currentRound.MapName == "-"))
				{
					currentRound.MapName = mapName;
					Logger.Info("Location", $"currentRoundのマップ名を後から更新: {mapName}");
				}
			}
			else if (command == 0) // Reset
			{
				GameData["location"] = "-";
			}
		}

		private void ProcessRoundActiveEvent(JObject jsonData)
		{
			bool isActive = jsonData["Value"]?.ToObject<bool>() ?? false;
			
			Logger.Info("RoundActive", $"ROUND_ACTIVEイベント受信: Value={isActive}, 前の状態={isRoundActive}");
			Logger.Debug("RoundActive", $"生データ: {jsonData.ToString(Newtonsoft.Json.Formatting.None)}");
			
			GameData["roundActive"] = isActive ? "アクティブ" : "非アクティブ";
			isRoundActive = isActive;
			
			Logger.Info("RoundActive", $"ラウンドアクティブ状態を更新: {(isActive ? "アクティブ" : "非アクティブ")}");
			
			// ROUND_ACTIVE=Falseが来た時、ダブルドラブルがアクティブなら終了する
			// （ダブルドラブルはROUND_TYPE Intermissionが来ないため、ここで終了処理を行う）
			if (!isActive && isDoubleTroubleActive)
			{
				Logger.Info("RoundActive", "ROUND_ACTIVE=Falseによりダブルドラブル終了処理を実行");
				FinishDoubleTroubleRound();
			}
		}

		private void ProcessAliveEvent(JObject jsonData)
		{
			bool isAlive = jsonData["Value"]?.ToObject<bool>() ?? false;
			GameData["alive"] = isAlive ? "生存" : "死亡";

			// ラウンド中に死亡した場合はフラグを立てる
			if (!isAlive && isRoundActive)
			{
				wasDeadDuringRound = true;
				System.Diagnostics.Debug.WriteLine("[ALIVE] ラウンド中に死亡しました");
			}

			if (!string.IsNullOrEmpty(LocalPlayerUserId) && Players.ContainsKey(LocalPlayerUserId))
			{
				var localPlayer = Players[LocalPlayerUserId];
				localPlayer.IsAlive = isAlive;
				if (!isAlive)
				{
					localPlayer.DiedThisRound = true;
				}
				localPlayer.LastSeen = DateTime.Now;

				// プレイヤー数変更イベントを発火
				OnPlayerCountChanged?.Invoke();
			}
		}

		private void ProcessSaboteurEvent(JObject jsonData)
		{
			bool isSaboteur = jsonData["Value"]?.ToObject<bool>() ?? false;
			
			Logger.Info("Saboteur", $"IS_SABOTEURイベント受信: Value={isSaboteur}");
			
			// サボタージュ状態を常に更新
			GameData["saboteur"] = isSaboteur ? "はい" : "いいえ";
			
			// サボタージュでキラー側になった場合
			if (isSaboteur)
			{
				// バッファイベント処理中でなければフラグをセット
				if (!isProcessingBufferedEvents)
				{
					// ラウンド開始前のイベントはpendingSaboteurFlagに保持
					// ラウンド開始後のイベントはwasSaboteurDuringRoundに直接セット
					if (isRoundActive)
					{
						Logger.Info("Saboteur", "サボタージュでキラー側になりました（ラウンド中）");
						wasSaboteurDuringRound = true;
					}
					else
					{
						Logger.Info("Saboteur", "サボタージュでキラー側になりました（ラウンド開始前、pending）");
						pendingSaboteurFlag = true;
					}
				}
			}
			else
			{
				// サボタージュ解除時
				pendingSaboteurFlag = false;
				// ラウンドがアクティブ中は wasSaboteurDuringRound を保持
				// （ラウンド終了後のアイテムリマインダーで必要なため）
				// ラウンド終了後は StartNewRound でリセットされる
				if (!isRoundActive)
				{
					wasSaboteurDuringRound = false;
					Logger.Info("Saboteur", "サボタージュ解除（サバイバー側）: フラグをクリア");
				}
				else
				{
					Logger.Info("Saboteur", "サボタージュ解除（サバイバー側）: ラウンドアクティブ中のためフラグを保持");
				}
			}
			
			// UI更新のためにイベントを発火
			OnInstanceStateChanged?.Invoke();
		}

		private void ProcessPageCountEvent(JObject jsonData)
		{
			int pageCount = jsonData["Value"]?.ToObject<int>() ?? 0;
			// ページ数は0ベースで来ているようなので、表示用に+1する
			if (pageCount == 0)
			{
				GameData["pageCount"] = $"-";
			}
			else
			{
				GameData["pageCount"] = $"{pageCount + 1} / 8";
			}
		}

		private void ProcessInstanceEvent(JObject jsonData)
		{
			try
			{
				// インスタンス情報の処理
				string instanceUrl = jsonData["Value"]?.ToString() ?? "";

				// 空のURLが来た場合は警告ログを出力
				if (string.IsNullOrEmpty(instanceUrl))
				{
					Logger.Warn("Instance", $"空のINSTANCEイベントを受信 (現在のInstanceUrl={InstanceState.InstanceUrl}, lastInstanceUrl={lastInstanceUrl}, バッファ処理中={isProcessingBufferedEvents})");
				}

				if (!string.IsNullOrEmpty(instanceUrl))
				{
					// インスタンスURLが変わった場合（インスタンス移動）
					if (!string.IsNullOrEmpty(lastInstanceUrl) && lastInstanceUrl != instanceUrl)
					{
						// インスタンス移動を検知 - サウンドミュート期間開始
						isInstanceTransitioning = true;
						instanceTransitionStartTime = DateTime.Now;
						Logger.Info("Instance", $"インスタンス移動を検知 - サウンドミュート開始（{INSTANCE_TRANSITION_MUTE_SECONDS}秒間）");
						System.Diagnostics.Debug.WriteLine($"[INSTANCE] インスタンス移動検知: {lastInstanceUrl} → {instanceUrl}");
						
						// ダブルドラブルがアクティブなら終了（ログに残さず破棄）
						if (isDoubleTroubleActive)
						{
							Logger.Info("DoubleTrouble", "インスタンス移動によりダブルドラブルを破棄");
							isDoubleTroubleActive = false;
							currentRound = null;
						}
						
						// プレイヤーリストをクリア（新しいインスタンスのプレイヤーリストを受け取るため）
						// ローカルプレイヤーは保持
						var localPlayer = Players.Values.FirstOrDefault(p => p.IsLocal);
						Players.Clear();
						if (localPlayer != null)
						{
							Players[localPlayer.UserId] = localPlayer;
						}
						Logger.Debug("Instance", "インスタンス移動のためプレイヤーリストをクリア（ローカルプレイヤーは保持）");
					}
					
					lastInstanceUrl = instanceUrl;
					InstanceState.InstanceUrl = instanceUrl;
					Logger.Info("Instance", $"インスタンスURL更新: {instanceUrl}");

					// インスタンス変更時は状態をリセット
					InstanceState.MasterChanged = false;

					// リスポーン追跡用フラグをリセット（新しいインスタンスでは初めからやり直し）
					InstanceState.WasOptedInThisInstance = false;
					InstanceState.HadRespawnedInRound = false;
					InstanceState.IsRespawnSaveCode = false;

					// クラウドからインスタンス状態を取得
					// バッファ処理中は遅延（処理完了後にフェッチする）
					if (isProcessingBufferedEvents)
					{
						pendingCloudFetchUrl = instanceUrl;
						Logger.Debug("Cloud", "バッファ処理中のためクラウドフェッチを遅延");
					}
					else
					{
						_ = FetchInstanceStateFromCloudAsync(instanceUrl);
					}

					OnInstanceStateChanged?.Invoke();
				}
				
				System.Diagnostics.Debug.WriteLine($"[INSTANCE] インスタンス情報を受信: {instanceUrl}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[INSTANCE] エラー: {ex.Message}");
			}
		}

		private void ProcessStatsEvent(JObject jsonData)
		{
			try
			{
				string statName = jsonData["Name"]?.ToString() ?? "";
				
				// 値の取得（型に応じて処理）
				var valueToken = jsonData["Value"];
				if (valueToken == null) return;
				
				switch (statName)
				{
					case "Survivals":
						SessionStats.Survivals = valueToken.ToObject<int>();
						break;
					case "Deaths":
						SessionStats.Deaths = valueToken.ToObject<int>();
						break;
					case "Stuns":
						SessionStats.Stuns = valueToken.ToObject<int>();
						break;
					case "StunsAll":
						SessionStats.StunsAll = valueToken.ToObject<int>();
						break;
					case "TopStuns":
						SessionStats.TopStuns = valueToken.ToObject<int>();
						break;
					case "TopStunsAll":
						SessionStats.TopStunsAll = valueToken.ToObject<int>();
						break;
					case "DamageTaken":
						SessionStats.DamageTaken = valueToken.ToObject<int>();
						break;
					case "LobbySurvivals":
						// ロビー生存数が15以上ならMystic Moon解禁
						int lobbySurvivals = valueToken.ToObject<int?>() ?? 0;
						if (lobbySurvivals >= 15 && !InstanceState.MysticMoonUnlocked)
						{
							InstanceState.MysticMoonUnlocked = true;
							Logger.Info("Stats", $"LobbySurvivalsが15以上({lobbySurvivals})のためMystic Moon解禁");
							System.Diagnostics.Debug.WriteLine($"[InstanceState] LobbySurvivals={lobbySurvivals} → Mystic Moon解禁");
							OnInstanceStateChanged?.Invoke();
						}
						// EstimatedSurvivalCountも更新（接続時の初期値として）
						if (lobbySurvivals > InstanceState.EstimatedSurvivalCount)
						{
							InstanceState.EstimatedSurvivalCount = lobbySurvivals;
							Logger.Debug("Stats", $"EstimatedSurvivalCountを{lobbySurvivals}に更新");
						}
						break;
				}
				
				Logger.Debug("Stats", $"統計更新: {statName} = {valueToken}");
				System.Diagnostics.Debug.WriteLine($"[STATS] 統計情報を受信: {statName} = {valueToken}");
			}
			catch (Exception ex)
			{
				Logger.Error("Stats", "統計情報処理エラー", ex);
				System.Diagnostics.Debug.WriteLine($"[STATS] エラー: {ex.Message}");
			}
		}

		private void ProcessTrackerEvent(JObject jsonData)
		{
			try
			{
				// eventプロパティをチェック（item_pickup等）
				string trackerEvent = jsonData["event"]?.ToString() ?? "";
				
				if (!string.IsNullOrEmpty(trackerEvent))
				{
					Logger.Debug("Tracker", $"TRACKERイベント受信: event='{trackerEvent}'");
					
					switch (trackerEvent.ToLower())
					{
						case "item_pickup":
							ProcessItemPickupEvent(jsonData);
							return;
						case "enemy_enraged":
							ProcessEnemyEnragedEvent(jsonData);
							return;
					}
				}
				
				// プレイヤートラッキング情報の処理（これが重要！）
				var playersData = jsonData["Value"] as JArray;
				if (playersData != null)
				{
					System.Diagnostics.Debug.WriteLine($"[TRACKER] プレイヤー追跡情報を受信: {playersData.Count}人");

					// 既存プレイヤーのSurvivalCount/JoinedAtを保持するためにバックアップ
					var existingPlayers = new Dictionary<string, PlayerInfo>(Players);
					Players.Clear();

					foreach (var playerData in playersData)
					{
						try
						{
							string playerName = playerData["Name"]?.ToString() ?? "Unknown";
							string userId = playerData["UserId"]?.ToString() ?? Guid.NewGuid().ToString();
							bool isAlive = playerData["IsAlive"]?.ToObject<bool>() ?? true;

							playerName = SanitizePlayerName(playerName);

							// 既存プレイヤーの値を引き継ぐ
							int prevRoundCount = 0;
							int prevSurvivalCount = 0;
							DateTime prevJoinedAt = DateTime.Now;
							bool prevDiedThisRound = false;
							bool wasAlreadyWarning = false;
							if (existingPlayers.TryGetValue(userId, out var existing))
							{
								prevRoundCount = existing.RoundCount;
								prevSurvivalCount = existing.SurvivalCount;
								prevJoinedAt = existing.JoinedAt;
								prevDiedThisRound = existing.DiedThisRound;
								wasAlreadyWarning = existing.IsWarningUser;
							}

							var player = new PlayerInfo
							{
								Name = playerName,
								UserId = userId,
								IsLocal = userId == LocalPlayerUserId,
								IsAlive = isAlive,
								LastSeen = DateTime.Now,
								JoinedAt = prevJoinedAt,
								RoundCount = prevRoundCount,
								SurvivalCount = prevSurvivalCount,
								// ラウンド内死亡フラグは引き継ぐ（IsAliveのみ最新値で上書き）
								DiedThisRound = prevDiedThisRound || !isAlive
							};

							Players[userId] = player;

							// 警告対象ユーザーかチェック
							if (IsWarningUser(playerName))
							{
								player.IsWarningUser = true;
								// 警告音・通知は新規検出時のみ（TRACKER更新のたびに鳴らさない）
								if (!wasAlreadyWarning && !isProcessingBufferedEvents)
								{
									PlayWarningSound();
									OnWarningUserJoined?.Invoke(playerName);
								}
								System.Diagnostics.Debug.WriteLine($"[WARNING] 警告対象ユーザーを検出: {playerName}");
							}

							System.Diagnostics.Debug.WriteLine($"[TRACKER] プレイヤー追加: {playerName} ({(isAlive ? "生存" : "死亡")})");
						}
						catch (Exception playerEx)
						{
							System.Diagnostics.Debug.WriteLine($"[TRACKER] プレイヤー処理エラー: {playerEx.Message}");
						}
					}

					// プレイヤー数変更イベントを発火
					OnPlayerCountChanged?.Invoke();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[TRACKER] エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// TRACKERイベントのitem_pickupを処理
		/// </summary>
		private void ProcessItemPickupEvent(JObject jsonData)
		{
			try
			{
				var args = jsonData["args"] as JArray;
				if (args != null && args.Count > 0)
				{
					string itemName = args[0]?.ToString() ?? "";
					
					if (!string.IsNullOrEmpty(itemName))
					{
						// ラウンド中の取得アイテムに追加（死亡後は記録しない）
						if (!wasDeadDuringRound && !currentRoundItems.Contains(itemName))
						{
							currentRoundItems.Add(itemName);

							// ラウンドログのアイテムが未設定なら、最初に持ったアイテムを記録
							if (currentRound != null && (string.IsNullOrEmpty(currentRound.Items) || currentRound.Items == "なし"))
							{
								currentRound.Items = itemName;
							}
						}

						// 現在所持アイテムを更新
						string previousItem = InstanceState.CurrentItem;
						InstanceState.CurrentItem = itemName;
						
						Logger.Info("ItemPickup", $"アイテム取得(TRACKER): '{previousItem}' → '{itemName}'");
						System.Diagnostics.Debug.WriteLine($"[ITEM_PICKUP] アイテム取得: '{previousItem}' → '{itemName}'");
						
						// UI更新のためにイベントを発火
						OnInstanceStateChanged?.Invoke();
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error("ItemPickup", "アイテム取得処理エラー", ex);
				System.Diagnostics.Debug.WriteLine($"[ITEM_PICKUP] エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// TRACKERイベントのenemy_enragedを処理（ダブルドラブル時のテラー名取得用）
		/// </summary>
		private void ProcessEnemyEnragedEvent(JObject jsonData)
		{
			try
			{
				var args = jsonData["args"] as JArray;
				if (args != null && args.Count > 0)
				{
					string terrorName = args[0]?.ToString() ?? "";
					
					if (!string.IsNullOrEmpty(terrorName))
					{
						Logger.Debug("EnemyEnraged", $"enemy_enraged受信: テラー名='{terrorName}'");
						
						// ダブルドラブル中の場合、テラー名を収集
						if (isDoubleTroubleActive && currentRound != null)
						{
							// 既存のテラー名リストに追加（重複チェック）
							if (string.IsNullOrEmpty(currentRound.TerrorNames))
							{
								currentRound.TerrorNames = terrorName;
							}
							else if (!currentRound.TerrorNames.Contains(terrorName))
							{
								currentRound.TerrorNames += ", " + terrorName;
							}
							
							// CurrentTerrorsリストにも追加
							if (!CurrentTerrors.Any(t => t.Name == terrorName))
							{
								CurrentTerrors.Add(new TerrorInfo
								{
									Name = terrorName,
									DisplayName = terrorName,
									DisplayColor = 0,
									StunType = TerrorConfiguration.GetTerrorStunType(terrorName)
								});
							}
							
							Logger.Info("DoubleTrouble", $"テラー名を追加: '{terrorName}' → 現在のテラー: '{currentRound.TerrorNames}'");
							
							// テラー更新イベントを発火（リプレイ中はスキップ）
							if (!isProcessingBufferedEvents)
							{
								OnTerrorUpdate?.Invoke();
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error("EnemyEnraged", "enemy_enraged処理エラー", ex);
			}
		}

		private void ProcessMasterChangeEvent(JObject jsonData)
		{
			try
			{
				Logger.Info("MasterChange", "マスター変更を検出");
				
				// マスター変更フラグを立てる（次ラウンドが特殊確定）
				InstanceState.MasterChanged = true;
				
				// イベント発火
				OnMasterChanged?.Invoke();
				OnInstanceStateChanged?.Invoke();
				
				System.Diagnostics.Debug.WriteLine($"[MASTER_CHANGE] マスター変更を検出 - 次ラウンド特殊確定");
			}
			catch (Exception ex)
			{
				Logger.Error("MasterChange", "マスター変更処理エラー", ex);
				System.Diagnostics.Debug.WriteLine($"[MASTER_CHANGE] エラー: {ex.Message}");
			}
		}

		private void ProcessSavedEvent(JObject jsonData)
		{
			try
			{
				string saveCode = jsonData["Value"]?.ToString() ?? "";
				
				if (!string.IsNullOrEmpty(saveCode))
				{
					string roundTypeName;
					string terrorNames;
					
					// リスポーン時のセーブコードかどうかをチェック
					if (InstanceState.IsRespawnSaveCode)
					{
						// リスポーン時のセーブコード
						roundTypeName = "リスポーン";
						terrorNames = "";  // テラー名は空
						InstanceState.IsRespawnSaveCode = false;  // フラグをリセット
						Logger.Info("SaveCode", "リスポーン用セーブコードとして処理");
					}
					else
					{
						// 通常のセーブコード: 直前のラウンドタイプを取得
						roundTypeName = ToNRoundTypeHelper.GetDisplayName(InstanceState.LastRoundType);
						if (InstanceState.LastRoundType == ToNRoundType.Intermission)
						{
							roundTypeName = ToNRoundTypeHelper.GetDisplayName(InstanceState.CurrentRoundType);
						}
						
						// テラー名を取得（優先順位: currentRound > lastFinishedRoundTerrorNames > CurrentTerrors）
						terrorNames = "";
						if (currentRound != null && !string.IsNullOrEmpty(currentRound.TerrorNames))
						{
							terrorNames = currentRound.TerrorNames;
						}
						else if (!string.IsNullOrEmpty(lastFinishedRoundTerrorNames))
						{
							terrorNames = lastFinishedRoundTerrorNames;
						}
						else if (CurrentTerrors.Count > 0)
						{
							terrorNames = string.Join(", ", CurrentTerrors.Select(t => t.Name));
						}
					}
					
					var saveCodeInfo = new SaveCodeInfo
					{
						Code = saveCode,
						RoundTypeName = roundTypeName,
						TerrorNames = terrorNames,
						Timestamp = DateTime.Now
					};
					
					// リストの先頭に追加
					SaveCodes.Insert(0, saveCodeInfo);
					
					// 最大数を超えたら古いものを削除
					while (SaveCodes.Count > MaxSaveCodes)
					{
						SaveCodes.RemoveAt(SaveCodes.Count - 1);
					}
					
					Logger.Info("SaveCode", $"セーブコード受信: {saveCode} ({roundTypeName}) - テラー: {terrorNames}");
					
					// イベント発火
					OnSaveCodeReceived?.Invoke(saveCodeInfo);
				}
				
				System.Diagnostics.Debug.WriteLine($"[SAVED] セーブコード受信: {saveCode}");
			}
			catch (Exception ex)
			{
				Logger.Error("SaveCode", "セーブコード処理エラー", ex);
				System.Diagnostics.Debug.WriteLine($"[SAVED] エラー: {ex.Message}");
			}
		}

		private void ProcessOptedInEvent(JObject jsonData)
		{
			try
			{
				bool isOptedIn = jsonData["Value"]?.ToObject<bool>() ?? true;
				
				// リスポーン検出: 一度opted_inしていた状態からopted_outになった場合
				if (!isOptedIn && InstanceState.WasOptedInThisInstance)
				{
					// これはリスポーン（死亡してリスポーン地点へ）
					InstanceState.HadRespawnedInRound = true;
					InstanceState.IsRespawnSaveCode = true;
					Logger.Info("OptedIn", "リスポーン検出: opted_out");
				}
				else if (isOptedIn)
				{
					// opted_inになった
					if (InstanceState.HadRespawnedInRound)
					{
						// リスポーン後の再参加 → 設定が有効ならアイテムリマインダーを発行
						if (SoundSettings.EnableRespawnReminder)
						{
							Logger.Info("OptedIn", "リスポーン後の再参加検出: アイテムリマインダーを発行");
							System.Diagnostics.Debug.WriteLine("[OPTED_IN] リスポーン後の再参加 → アイテムリマインダー発行");
							
							// ミュート期間中でなければリマインダーを発行
							if (!ShouldMuteNotificationSounds())
							{
								OnItemReminderRoundEnd?.Invoke();
							}
						}
						else
						{
							Logger.Info("OptedIn", "リスポーン後の再参加検出: リマインダー設定が無効のためスキップ");
						}
						
						InstanceState.HadRespawnedInRound = false;
					}
					
					// このインスタンスでopted_inしたことを記録
					InstanceState.WasOptedInThisInstance = true;
				}
				
				InstanceState.IsOptedIn = isOptedIn;
				
				Logger.Info("OptedIn", $"ゲーム参加状態変更: {(isOptedIn ? "参加中" : "未参加")}");
				
				// イベント発火
				OnOptedInChanged?.Invoke(isOptedIn);
				OnInstanceStateChanged?.Invoke();
				
				System.Diagnostics.Debug.WriteLine($"[OPTED_IN] ゲーム参加状態: {isOptedIn}");
			}
			catch (Exception ex)
			{
				Logger.Error("OptedIn", "ゲーム参加状態処理エラー", ex);
				System.Diagnostics.Debug.WriteLine($"[OPTED_IN] エラー: {ex.Message}");
			}
		}

		private void ProcessItemEvent(JObject jsonData)
		{
			int command = jsonData["Command"]?.ToObject<int>() ?? 0;
			string itemName = jsonData["Name"]?.ToString() ?? "Unknown Item";
			int itemId = jsonData["ID"]?.ToObject<int>() ?? -1;

			// ITEMイベント受信を必ずログ出力（デバッグ用）
			Logger.Info("Item", $"ITEMイベント受信: Command={command}, Name='{itemName}', ID={itemId}");
			System.Diagnostics.Debug.WriteLine($"[ITEM] Command={command}, Name='{itemName}', ID={itemId}");

			if (command == 1) // Grab
			{
				if (!currentRoundItems.Contains(itemName))
				{
					currentRoundItems.Add(itemName);
				}
				// 現在所持アイテムを更新
				string previousItem = InstanceState.CurrentItem;
				InstanceState.CurrentItem = itemName;
				Logger.Info("Item", $"アイテム取得: '{previousItem}' → '{itemName}' (ID: {itemId})");
				System.Diagnostics.Debug.WriteLine($"[ITEM] アイテム取得: '{previousItem}' → '{itemName}'");
				
				// UI更新のためにイベントを発火
				OnInstanceStateChanged?.Invoke();
			}
			else if (command == 0) // Drop
			{
				// ドロップ時は現在アイテムをクリア
				string previousItem = InstanceState.CurrentItem;
				if (InstanceState.CurrentItem == itemName)
				{
					InstanceState.CurrentItem = "";
				}
				Logger.Info("Item", $"アイテムドロップ: '{previousItem}' → '' (ドロップ: {itemName})");
				System.Diagnostics.Debug.WriteLine($"[ITEM] アイテムドロップ: '{previousItem}' → ''");
				
				// UI更新のためにイベントを発火
				OnInstanceStateChanged?.Invoke();
			}
		}

		private void ProcessPlayerJoinEvent(JObject jsonData)
		{
			try
			{
				string playerName = jsonData["Value"]?.ToString() ?? "Unknown";
				string playerId = jsonData["ID"]?.ToString() ?? playerName;

				// プレイヤー名のサニタイズと検証
				playerName = SanitizePlayerName(playerName);

				// 空の名前やnullの場合の処理
				if (string.IsNullOrWhiteSpace(playerName))
				{
					playerName = $"Player_{playerId.Substring(0, Math.Min(8, playerId.Length))}";
				}

				System.Diagnostics.Debug.WriteLine($"[PLAYER_JOIN] 名前: '{playerName}', ID: '{playerId}'");

				// サウンドをスキップするかどうかの判定（初期プレイヤーリスト受信中を含む）
				bool isReceivingInitialList = IsReceivingInitialPlayerList();
				bool shouldSkipSound = isProcessingBufferedEvents || isReceivingInitialList;
				if (shouldSkipSound)
				{
					System.Diagnostics.Debug.WriteLine($"[PLAYER_JOIN] サウンドスキップ: バッファ処理中={isProcessingBufferedEvents}, 初期リスト受信中={isReceivingInitialList}");
				}

				// 警告ユーザーチェック（サウンドスキップ条件でない場合のみ）
				if (IsWarningUser(playerName) && !shouldSkipSound)
				{
					System.Diagnostics.Debug.WriteLine($"[WARNING] 警告対象ユーザーが参加: {playerName}");
					PlayWarningSound();
					OnWarningUserJoined?.Invoke(playerName);

					// イベントログにも記録
					AddGameEvent("WARNING", jsonData, $"警告: {playerName} が参加しました");
				}

				// 既に存在するプレイヤーの場合はLastSeenを更新するだけ
				if (Players.ContainsKey(playerId))
				{
					Players[playerId].LastSeen = DateTime.Now;
					Players[playerId].Name = playerName; // 名前も更新
					System.Diagnostics.Debug.WriteLine($"プレイヤー更新: {playerName}");
					// temp_エントリが残っていたら掃除（DEATH自動追加で作られた仮エントリ）
					var tempEntries = Players
						.Where(p => p.Key != playerId && p.Key.StartsWith("temp_") && p.Value.Name == playerName)
						.Select(p => p.Key)
						.ToList();
					foreach (var tempKey in tempEntries)
					{
						Players.Remove(tempKey);
						System.Diagnostics.Debug.WriteLine($"[PLAYER_JOIN] temp重複エントリを削除: {tempKey}");
					}
					return;
				}

				// 新規追加前にtemp_エントリを掃除（同名プレイヤーの仮エントリ）
				var existingTempEntries = Players
					.Where(p => p.Key.StartsWith("temp_") && p.Value.Name == playerName)
					.Select(p => p.Key)
					.ToList();
				foreach (var tempKey in existingTempEntries)
				{
					Players.Remove(tempKey);
					System.Diagnostics.Debug.WriteLine($"[PLAYER_JOIN] temp重複エントリを削除（新規追加前）: {tempKey}");
				}

				bool initialAliveState = !isRoundActive;

				Players[playerId] = new PlayerInfo
				{
					Name = playerName,
					UserId = playerId,
					IsLocal = false,
					IsAlive = initialAliveState,
					LastSeen = DateTime.Now,
					JoinedAt = DateTime.Now
				};

				// サウンドスキップ条件でない場合のみJoinサウンドを再生
				if (!shouldSkipSound)
				{
					PlayJoinLeaveSound(true);
					// イベントを発火
					OnPlayerJoinLeave?.Invoke(playerName, true);
				}
				
				// プレイヤー数変更イベントは常に発火（UIは更新する）
				OnPlayerCountChanged?.Invoke();

				System.Diagnostics.Debug.WriteLine($"プレイヤー参加: {playerName} - ラウンド中: {isRoundActive} - 初期状態: {(initialAliveState ? "生存" : "死亡")}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[PLAYER_JOIN] エラー: {ex.Message}");
				AddGameEvent("ERROR", null, $"プレイヤー参加処理エラー: {ex.Message}");
			}
		}

		private void ProcessPlayerLeaveEvent(JObject jsonData)
		{
			try
			{
				string playerName = jsonData["Value"]?.ToString() ?? "Unknown";
				playerName = SanitizePlayerName(playerName);

				System.Diagnostics.Debug.WriteLine($"[PLAYER_LEAVE] 名前: '{playerName}'");

				// サウンドをスキップするかどうかの判定
				// LEAVEについてはインスタンス移動中（プレイヤーリストクリア済み）または初期リスト受信中はスキップ
				bool isReceivingInitialList = isReceivingInitialPlayerList;
				bool shouldSkipSound = isProcessingBufferedEvents || isInstanceTransitioning || isReceivingInitialList;
				if (shouldSkipSound)
				{
					System.Diagnostics.Debug.WriteLine($"[PLAYER_LEAVE] サウンドスキップ: バッファ処理中={isProcessingBufferedEvents}, インスタンス移動中={isInstanceTransitioning}, 初期リスト受信中={isReceivingInitialList}");
				}

				// 名前またはIDで検索（完全一致 → 正規化一致 → 部分一致）
				var playerToRemove = FindPlayerByName(playerName);

				if (playerToRemove.Key != null)
				{
					string removedPlayerName = Players[playerToRemove.Key].Name;
					System.Diagnostics.Debug.WriteLine($"プレイヤー退出: {removedPlayerName}");
					Players.Remove(playerToRemove.Key);

					// サウンドスキップ条件でない場合のみLeaveサウンドを再生
					if (!shouldSkipSound)
					{
						PlayJoinLeaveSound(false);
						// イベントを発火
						OnPlayerJoinLeave?.Invoke(removedPlayerName, false);
					}
					
					// プレイヤー数変更イベントは常に発火（UIは更新する）
					OnPlayerCountChanged?.Invoke();
				}
				else
				{
					System.Diagnostics.Debug.WriteLine($"退出プレイヤーが見つかりません: '{playerName}'");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[PLAYER_LEAVE] エラー: {ex.Message}");
				AddGameEvent("ERROR", null, $"プレイヤー退出処理エラー: {ex.Message}");
			}
		}

		private void ProcessDeathEvent(JObject jsonData)
		{
			try
			{
				string playerName = jsonData["Name"]?.ToString() ?? "Unknown";
				string message = jsonData["Message"]?.ToString() ?? "";

				playerName = SanitizePlayerName(playerName);

				System.Diagnostics.Debug.WriteLine($"[DEATH] 名前: '{playerName}', メッセージ: '{message}'");
				
				// ダブルドラブル検出: currentRoundがnullなのにDEATHイベントが来た場合
				// （ROUND_TYPEが来ていないのにラウンドが進行している = ダブルドラブル）
				// かつゲーム参加中（IsOptedIn=true）の場合
				if (currentRound == null && InstanceState.IsOptedIn && !isDoubleTroubleActive && !isProcessingBufferedEvents)
				{
					Logger.Info("DoubleTrouble", "currentRoundがnullの状態でDEATHイベント検出 - ダブルドラブル開始");
					StartDoubleTroubleRound();
				}

				// 名前で検索（完全一致 → 正規化一致 → 部分一致）
				var player = FindPlayerByName(playerName).Value;

				if (player != null)
				{
					player.IsAlive = false;
					player.DiedThisRound = true;
					player.LastSeen = DateTime.Now;
					System.Diagnostics.Debug.WriteLine($"[DEATH] プレイヤー死亡: {player.Name} - メッセージ: {message}");
					
					// プレイヤー数変更イベントを発火
					OnPlayerCountChanged?.Invoke();
				}
				else
				{
					// プレイヤーが見つからない場合、自動追加する（TSMからのPLAYER_JOIN漏れ対策）
					System.Diagnostics.Debug.WriteLine($"[DEATH] 警告: プレイヤー '{playerName}' が見つかりません - 自動追加します");
					Logger.Info("Death", $"プレイヤー '{playerName}' がPLAYER_JOINなしでDEATH受信 - 自動追加");
					
					// 仮のIDを生成（実際のUserIDが不明なため）
					string tempId = $"temp_{playerName}_{DateTime.Now.Ticks}";
					
					Players[tempId] = new PlayerInfo
					{
						Name = playerName,
						UserId = tempId,
						IsLocal = false,
						IsAlive = false, // 死亡状態で追加
						DiedThisRound = true,
						LastSeen = DateTime.Now,
						JoinedAt = DateTime.Now
					};
					
					System.Diagnostics.Debug.WriteLine($"[DEATH] プレイヤー自動追加完了: {playerName} (ID: {tempId})");
					
					// 警告ユーザーチェック
					if (IsWarningUser(playerName))
					{
						System.Diagnostics.Debug.WriteLine($"[WARNING] 警告対象ユーザーが自動追加されました: {playerName}");
						OnWarningUserJoined?.Invoke(playerName);
					}
					
					// プレイヤー数変更イベントを発火
					OnPlayerCountChanged?.Invoke();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[DEATH] エラー: {ex.Message}");
				AddGameEvent("ERROR", null, $"死亡処理エラー: {ex.Message}");
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

		/// <summary>
		/// プレイヤー名をサニタイズする
		/// </summary>
		private string SanitizePlayerName(string playerName)
		{
			if (string.IsNullOrEmpty(playerName))
				return "Unknown";

			try
			{
				// 制御文字を除去
				var sanitized = new StringBuilder();
				foreach (char c in playerName)
				{
					// 印刷可能文字、日本語、中国語、韓国語、絵文字などを許可
					if (char.IsControl(c))
					{
						continue; // 制御文字はスキップ
					}

					sanitized.Append(c);
				}

				string result = sanitized.ToString().Trim();

				// 空になった場合は"Unknown"を返す
				if (string.IsNullOrWhiteSpace(result))
				{
					return "Unknown";
				}

				// 長すぎる名前は切り詰める
				if (result.Length > 50)
				{
					result = result.Substring(0, 47) + "...";
				}

				return result;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"プレイヤー名サニタイズエラー: {ex.Message}");
				return "Unknown";
			}
		}

		/// <summary>
		/// プレイヤー名を正規化する（比較用）
		/// </summary>
		private string NormalizePlayerName(string playerName)
		{
			if (string.IsNullOrEmpty(playerName))
				return "";

			try
			{
				return playerName
					.Trim()
					.ToLowerInvariant()
					.Replace(" ", "")
					.Replace("_", "")
					.Replace("-", "");
			}
			catch
			{
				return playerName ?? "";
			}
		}

		/// <summary>
		/// テラー名からTerrorInfoを追加する
		/// </summary>
		private void AddTerrorFromName(string terrorName, JObject jsonData)
		{
			// 除外リストに含まれるテラーは分割しない
			if (TerrorConfiguration.AmpersandSplitExclusions.Contains(terrorName))
			{
				var terrorInfo = new TerrorInfo
				{
					Name = terrorName,
					DisplayName = terrorName,
					DisplayColor = jsonData["DisplayColor"]?.ToObject<uint>() ?? 0,
					StunType = TerrorConfiguration.GetTerrorStunType(terrorName)
				};
				CurrentTerrors.Add(terrorInfo);
			}
			else
			{
				// その他のテラーは " & " で分割
				var splitNames = terrorName.Split(new[] { " & " }, StringSplitOptions.RemoveEmptyEntries);

				foreach (var individualName in splitNames)
				{
					var terrorInfo = new TerrorInfo
					{
						Name = individualName.Trim(),
						DisplayName = individualName.Trim(),
						DisplayColor = jsonData["DisplayColor"]?.ToObject<uint>() ?? 0,
						StunType = TerrorConfiguration.GetTerrorStunType(individualName.Trim())
					};
					CurrentTerrors.Add(terrorInfo);
				}
			}
		}

		/// <summary>
		/// 現在ロードしている警告対象ユーザーリストを取得
		/// </summary>
		public HashSet<string> GetWarningUsers()
		{
			return new HashSet<string>(warningUsers);
		}

		public Dictionary<string, int> GetTerrorStats()
		{
			lock (dataLock)
			{
				return new Dictionary<string, int>(RoundStats.TerrorCounts);
			}
		}

		/// <summary>
		/// ラウンド統計とテラー統計をリセットする
		/// </summary>
		public void ResetRoundStats()
		{
			lock (dataLock)
			{
				// ラウンド統計をリセット（空のリストにする）
				RoundStats = new RoundStats();

				// テラー統計をリセット（空のリストにする）
				TerrorStats = new TerrorStats();

				// ラウンドログをクリア
				RoundLogs.Clear();
				HasFetchedCloudRoundLogs = false;
				isFetchingCloudData = false;

				System.Diagnostics.Debug.WriteLine("[リセット] ラウンド統計、テラー統計、ラウンドログをリセットしました");
			}
		}

		/// <summary>
		/// クラウドにラウンド終了情報を送信する
		/// </summary>
		private void SendRoundEndToCloud(ToNRoundType roundType)
		{
			if (cloudService == null)
				return;

			// TSMからの履歴データ（バッファイベント）処理中はスキップ
			if (isProcessingBufferedEvents)
			{
				Logger.Debug("Cloud", "バッファイベント処理中のためクラウド送信をスキップ");
				return;
			}

			// インスタンスURLを取得（InstanceState.InstanceUrlが空の場合はlastInstanceUrlをフォールバック）
			string instanceUrl = InstanceState.InstanceUrl;
			if (string.IsNullOrEmpty(instanceUrl))
			{
				// lastInstanceUrlが有効ならそれを使用
				if (!string.IsNullOrEmpty(lastInstanceUrl))
				{
					Logger.Warn("Cloud", $"InstanceState.InstanceUrlが空のためlastInstanceUrlを使用: {lastInstanceUrl}");
					instanceUrl = lastInstanceUrl;
					// InstanceState.InstanceUrlも復元
					InstanceState.InstanceUrl = lastInstanceUrl;
				}
				else
				{
					Logger.Debug("Cloud", $"インスタンスURLが空のためクラウド送信をスキップ (lastInstanceUrl={lastInstanceUrl}, IsOptedIn={InstanceState.IsOptedIn})");
					return;
				}
			}

			try
			{
				// 現在のラウンド情報を取得
				string mapName = GetGameDataValue("location", "Unknown").Split('(')[0].Trim();
				var terrors = CurrentTerrors.Select(t => t.Name).ToArray();

				// 生存プレイヤー数をカウント
				int aliveCount = 0;
				int totalPlayerCount = 0;
				lock (dataLock)
				{
					aliveCount = CountRoundSurvivors();
					totalPlayerCount = Players.Count;
				}

				// プレイヤーのアイテムリストを作成（開始時アイテム + ラウンド中取得アイテム）
				var playerItems = new List<string>();
				if (!string.IsNullOrEmpty(InstanceState.CurrentItem) && InstanceState.CurrentItem != "なし")
				{
					playerItems.Add(InstanceState.CurrentItem);
				}
				playerItems.AddRange(currentRoundItems.Where(i => !playerItems.Contains(i)));

				// プレイヤーの生存状態を判定
				bool playerSurvived = !wasDeadDuringRound && !wasSaboteurDuringRound;

				var roundEvent = new CloudRoundEndEvent
				{
					InstanceId = instanceUrl,
					Timestamp = DateTime.UtcNow,
					Round = new CloudRoundInfo
					{
						Type = ToNRoundTypeHelper.GetDisplayName(roundType),
						MapName = mapName,
						Terrors = terrors
					},
					Instance = new CloudInstanceInfo
					{
						PlayerCount = totalPlayerCount,
						SurvivorCount = aliveCount
					},
					Player = new CloudPlayerInfo
					{
						VRChatName = LocalPlayerName,
						VRChatId = LocalPlayerUserId,
						Survived = playerSurvived,
						Items = playerItems.ToArray()
					}
				};

				// 非同期で送信（結果を待たない）
				_ = cloudService.SendRoundEndAsync(roundEvent);
				Logger.Debug("Cloud", $"ラウンド情報をクラウドに送信: {roundType}, Players={totalPlayerCount}, Survivors={aliveCount}, PlayerSurvived={playerSurvived}, Items={string.Join(",", playerItems)}");
			}
			catch (Exception ex)
			{
				Logger.Warn("Cloud", $"クラウド送信エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// クラウド設定を更新する
		/// </summary>
		public void UpdateCloudSettings(bool enabled, string serverUrl, string apiKey = null)
		{
			if (cloudService != null)
			{
				cloudService.SetEnabled(enabled);
				cloudService.SetServerUrl(serverUrl);
				if (apiKey != null)
				{
					cloudService.SetApiKey(apiKey);
				}
				Logger.Info("Cloud", $"クラウド設定を更新: Enabled={enabled}, URL={serverUrl}");
			}
		}

		/// <summary>
		/// クラウドからインスタンス状態を取得してInstanceStateに反映する
		/// </summary>
		private async Task FetchInstanceStateFromCloudAsync(string instanceUrl)
		{
			if (cloudService == null)
				return;

			// 既にフェッチ中または取得済みなら重複フェッチしない
			if (isFetchingCloudData || HasFetchedCloudRoundLogs)
			{
				Logger.Debug("Cloud", $"クラウドフェッチスキップ (fetching={isFetchingCloudData}, fetched={HasFetchedCloudRoundLogs})");
				return;
			}

			isFetchingCloudData = true;
			OnCloudSyncStateChanged?.Invoke(true);
			try
			{
				var instanceDetail = await cloudService.FetchInstanceDetailAsync(instanceUrl);
				if (instanceDetail == null)
					return;

				// Moon/鳥状態の反映
				var moonState = instanceDetail.MoonState;
				if (moonState != null)
				{
					// 取得した状態をInstanceStateに反映（既存の状態とORで結合）
					if (moonState.BloodMoonUnlocked)
						InstanceState.BloodMoonUnlocked = true;
					if (moonState.TwilightUnlocked)
						InstanceState.TwilightUnlocked = true;
					if (moonState.MysticMoonUnlocked)
						InstanceState.MysticMoonUnlocked = true;
					if (moonState.SolsticeUnlocked)
						InstanceState.SolsticeUnlocked = true;

					// 鳥遭遇状態
					if (moonState.BigBirdEncountered)
						InstanceState.MetBigBird = true;
					if (moonState.JudgementBirdEncountered)
						InstanceState.MetJudgementBird = true;
					if (moonState.PunishingBirdEncountered)
						InstanceState.MetPunishingBird = true;

					// Twilight/Solstice解禁なら全鳥遭遇済み
					if (InstanceState.TwilightUnlocked || InstanceState.SolsticeUnlocked)
					{
						InstanceState.MetBigBird = true;
						InstanceState.MetJudgementBird = true;
						InstanceState.MetPunishingBird = true;
					}

					Logger.Info("Cloud", $"インスタンス状態をクラウドから復元: Moon(B={InstanceState.BloodMoonUnlocked},T={InstanceState.TwilightUnlocked},M={InstanceState.MysticMoonUnlocked},S={InstanceState.SolsticeUnlocked}) Birds({InstanceState.MetBigBird},{InstanceState.MetJudgementBird},{InstanceState.MetPunishingBird})");
				}

				// クラウドのラウンドログをローカルにマージ
				if (instanceDetail.Rounds != null && instanceDetail.Rounds.Length > 0)
				{
					// 50件制限に達している場合は全件取得を試みる
					if (instanceDetail.TotalRounds > instanceDetail.Rounds.Length)
					{
						Logger.Info("Cloud", $"ラウンドログが50件制限（全{instanceDetail.TotalRounds}件）のため全件取得を試行");
						var allRounds = await cloudService.FetchAllRoundLogsAsync(instanceUrl);
						if (allRounds != null && allRounds.Length > 0)
						{
							MergeCloudRoundLogs(allRounds);
						}
						else
						{
							// 全件取得に失敗した場合は50件分を使用
							MergeCloudRoundLogs(instanceDetail.Rounds);
						}
					}
					else
					{
						MergeCloudRoundLogs(instanceDetail.Rounds);
					}
				}

				// UIを更新
				OnInstanceStateChanged?.Invoke();
				OnRoundEnd?.Invoke(); // ラウンドログ表示を更新
			}
			catch (Exception ex)
			{
				Logger.Warn("Cloud", $"クラウドからインスタンス状態取得エラー: {ex.Message}");
			}
			finally
			{
				isFetchingCloudData = false;
				OnCloudSyncStateChanged?.Invoke(false);
			}
		}

		/// <summary>
		/// クラウドから取得したラウンドログをローカルのRoundLogsにマージする
		/// リプレイログはクラウドデータで置き換え、統計も更新する
		/// </summary>
		private void MergeCloudRoundLogs(CloudRoundDetail[] cloudRounds)
		{
			// クラウドのラウンドキー一覧を先に作成（リプレイ置き換え判定用）
			var cloudKeys = new System.Collections.Generic.HashSet<string>();
			foreach (var cr in cloudRounds)
			{
				string t = cr.Terrors != null ? string.Join(", ", cr.Terrors) : "";
				cloudKeys.Add($"{cr.RoundType}|{t}");
			}

			// クラウドに対応するリプレイログのみ削除（クラウドにないリプレイは残す）
			int removedReplayCount = RoundLogs.RemoveAll(log => log.IsReplay && cloudKeys.Contains($"{log.RoundTypeDisplayName}|{log.TerrorNames}"));
			if (removedReplayCount > 0)
			{
				// 統計をリセットして残ったログから再構築
				RoundStats = new RoundStats();
				TerrorStats = new TerrorStats();

				foreach (var log in RoundLogs)
				{
					RoundStats.TotalRounds++;
					if (log.WasOptedIn && log.Survived)
						RoundStats.SurvivedRounds++;
					RoundStats.IncrementCount(log.RoundType);

					if (!string.IsNullOrEmpty(log.TerrorNames))
					{
						var names = log.TerrorNames.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
						foreach (string t in names)
						{
							if (TerrorStats.TerrorTypeCounts.ContainsKey(t))
								TerrorStats.TerrorTypeCounts[t]++;
							else
								TerrorStats.TerrorTypeCounts[t] = 1;
						}
					}
				}

				Logger.Info("Cloud", $"リプレイログ {removedReplayCount} 件をクラウドデータで置き換えます（統計リセット済み）");
			}

			// クラウドラウンドID（サーバー側で一意）で重複排除
			var processedCloudIds = new System.Collections.Generic.HashSet<int>();

			int addedCount = 0;

			// クラウドのラウンドを古い順に処理（時系列順にリストに追加するため）
			var sortedRounds = cloudRounds.OrderBy(r => r.StartedAt).ToArray();

			foreach (var cloudRound in sortedRounds)
			{
				// クラウドのround IDで重複排除（LEFT JOINで同一ラウンドが複数行返る場合の対策）
				if (processedCloudIds.Contains(cloudRound.Id))
					continue;
				processedCloudIds.Add(cloudRound.Id);

				string terrorNames = cloudRound.Terrors != null ? string.Join(", ", cloudRound.Terrors) : "";

				// クラウドのタイムスタンプはUTCなのでローカル時間に変換
				var localTime = cloudRound.StartedAt.ToLocalTime();

				// RoundLogに変換してマージ
				var roundType = ToNRoundTypeHelper.Parse(cloudRound.RoundType);
				var roundLog = new RoundLog
				{
					Timestamp = localTime,
					RoundType = roundType,
					MapName = cloudRound.MapName ?? "",
					TerrorNames = terrorNames,
					AliveCount = cloudRound.SurvivorCount,
					TotalPlayerCount = cloudRound.PlayerCount,
					IsReplay = false,
				};

				// 自分の参加情報があれば反映
				if (cloudRound.MySurvived.HasValue)
				{
					roundLog.Survived = cloudRound.MySurvived.Value;
					roundLog.Items = cloudRound.MyItems != null ? string.Join(", ", cloudRound.MyItems) : "";
					roundLog.WasOptedIn = true;
				}
				else
				{
					roundLog.Survived = false;
					roundLog.Items = "";
					roundLog.WasOptedIn = false;
				}

				RoundLogs.Add(roundLog);
				addedCount++;

				// ラウンド統計を更新
				RoundStats.TotalRounds++;
				if (roundLog.WasOptedIn && roundLog.Survived)
				{
					RoundStats.SurvivedRounds++;
				}
				RoundStats.IncrementCount(roundType);

				// テラー統計を更新
				if (!string.IsNullOrEmpty(terrorNames))
				{
					var splitNames = terrorNames.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
					foreach (string terror in splitNames)
					{
						if (TerrorStats.TerrorTypeCounts.ContainsKey(terror))
						{
							TerrorStats.TerrorTypeCounts[terror]++;
						}
						else
						{
							TerrorStats.TerrorTypeCounts[terror] = 1;
						}
					}
				}
			}

			// タイムスタンプ順にソート
			if (addedCount > 0 || removedReplayCount > 0)
			{
				RoundLogs.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

				// 最大件数を超えたら古いものを削除
				while (RoundLogs.Count > MAX_ROUND_LOGS)
				{
					RoundLogs.RemoveAt(0);
				}

				HasFetchedCloudRoundLogs = true;
				Logger.Info("Cloud", $"クラウドからラウンドログ {addedCount} 件をマージしました（合計 {RoundLogs.Count} 件）");
			}
		}

		/// <summary>
		/// サウンド設定を読み込む
		/// </summary>
		private void LoadSoundSettings()
		{
			try
			{
				string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SOUND_SETTINGS_FILE);
				if (File.Exists(settingsPath))
				{
					string json = File.ReadAllText(settingsPath);
					SoundSettings = JsonConvert.DeserializeObject<SoundSettings>(json) ?? new SoundSettings();
					System.Diagnostics.Debug.WriteLine("[SOUND] サウンド設定を読み込みました");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[SOUND] サウンド設定読み込みエラー: {ex.Message}");
				SoundSettings = new SoundSettings();
			}
		}

		/// <summary>
		/// サウンド設定を保存する
		/// </summary>
		public void SaveSoundSettings()
		{
			try
			{
				string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SOUND_SETTINGS_FILE);
				string json = JsonConvert.SerializeObject(SoundSettings, Formatting.Indented);
				File.WriteAllText(settingsPath, json);
				System.Diagnostics.Debug.WriteLine("[SOUND] サウンド設定を保存しました");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[SOUND] サウンド設定保存エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// サウンド設定を更新する
		/// </summary>
		public void UpdateSoundSettings(SoundSettings settings)
		{
			SoundSettings = settings;
			SaveSoundSettings();
		}

		// 音声再生用のキュー（競合回避）
		private readonly Queue<string> soundQueue = new Queue<string>();
		private bool isSoundPlaying = false;
		private readonly object soundQueueLock = new object();

		/// <summary>
		/// サウンドをキューに追加して順番に再生
		/// </summary>
		private void QueueSound(string soundPath)
		{
			if (string.IsNullOrEmpty(soundPath) || !File.Exists(soundPath))
				return;

			lock (soundQueueLock)
			{
				soundQueue.Enqueue(soundPath);
				if (!isSoundPlaying)
				{
					isSoundPlaying = true;
					Task.Run(() => ProcessSoundQueue());
				}
			}
		}

		/// <summary>
		/// サウンドキューを処理
		/// </summary>
		private void ProcessSoundQueue()
		{
			while (true)
			{
				string nextSound;
				lock (soundQueueLock)
				{
					if (soundQueue.Count == 0)
					{
						isSoundPlaying = false;
						return;
					}
					nextSound = soundQueue.Dequeue();
				}

				try
				{
					PlayMp3FileSync(nextSound);
					// 次の音まで少し間隔を空ける
					Thread.Sleep(100);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[SOUND_QUEUE] 再生エラー: {ex.Message}");
				}
			}
		}

		/// <summary>
		/// MP3ファイルを同期的に再生（完了まで待機）
		/// </summary>
		private void PlayMp3FileSync(string filePath)
		{
			try
			{
				using (var audioReader = new AudioFileReader(filePath))
				using (var waveOut = new WaveOutEvent())
				{
					waveOut.Init(audioReader);
					waveOut.Play();
					
					// 再生完了まで待機
					while (waveOut.PlaybackState == PlaybackState.Playing)
					{
						Thread.Sleep(50);
					}
					
					Thread.Sleep(50); // デバイス解放前に少し待機
				}
				System.Diagnostics.Debug.WriteLine($"[SOUND_SYNC] 再生完了: {filePath}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[SOUND_SYNC] 再生エラー: {ex.Message}");
				try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
			}
		}

		/// <summary>
		/// Join/Leaveサウンドを再生（キュー使用）
		/// </summary>
		private void PlayJoinLeaveSound(bool isJoin)
		{
			try
			{
				bool isEnabled = isJoin ? SoundSettings.EnableJoinSound : SoundSettings.EnableLeaveSound;
				if (!isEnabled)
					return;

				string soundPath = isJoin ? SoundSettings.JoinSoundPath : SoundSettings.LeaveSoundPath;
				string defaultFileName = isJoin ? "player_join.mp3" : "player_leave.mp3";

				// カスタムパスが空または存在しない場合はデフォルトファイルを使用
				if (string.IsNullOrEmpty(soundPath) || !File.Exists(soundPath))
				{
					soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, defaultFileName);
				}

				if (!File.Exists(soundPath))
					return;

				QueueSound(soundPath);
				System.Diagnostics.Debug.WriteLine($"[SOUND] {(isJoin ? "Join" : "Leave")}サウンドをキュー: {soundPath}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[SOUND] サウンド再生エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// プレイヤーを一覧から手動で削除する（leave通知漏れ対策）
		/// </summary>
		public bool RemovePlayerManually(string playerName)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(playerName))
					return false;

				// プレイヤーを検索（完全一致 → 正規化一致 → 部分一致）
				var playerEntry = FindPlayerByName(playerName);

				if (playerEntry.Key != null)
				{
					Players.Remove(playerEntry.Key);
					System.Diagnostics.Debug.WriteLine($"[PLAYER] プレイヤーを手動削除: {playerName} (ID: {playerEntry.Key})");
					Logger.Info("Player", $"プレイヤーを手動削除: {playerName}");
					
					// プレイヤー数変更イベントを発火
					OnPlayerCountChanged?.Invoke();
					return true;
				}
				else
				{
					System.Diagnostics.Debug.WriteLine($"[PLAYER] 削除対象プレイヤーが見つかりません: {playerName}");
					return false;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[PLAYER] プレイヤー削除エラー: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// 警告ユーザーを追加する
		/// </summary>
		public bool AddWarningUser(string playerName)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(playerName))
					return false;

				string normalizedName = playerName.ToLowerInvariant().Trim();
				
				// 既に登録済みの場合
				if (warningUsers.Contains(normalizedName))
					return false;

				// メモリに追加
				warningUsers.Add(normalizedName);

				// ファイルに追記
				string warningFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warn_user.txt");
				File.AppendAllText(warningFilePath, $"\n{playerName}");

				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザーを追加: {playerName}");
				return true;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー追加エラー: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// 警告ユーザーを削除する
		/// </summary>
		public bool RemoveWarningUser(string playerName)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(playerName))
					return false;

				string normalizedName = playerName.ToLowerInvariant().Trim();
				
				if (!warningUsers.Contains(normalizedName))
					return false;

				// メモリから削除
				warningUsers.Remove(normalizedName);

				// ファイルを更新
				string warningFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warn_user.txt");
				if (File.Exists(warningFilePath))
				{
					var lines = File.ReadAllLines(warningFilePath)
						.Where(line => {
							var trimmed = line.Trim();
							if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
								return true; // コメントや空行は保持
							return trimmed.ToLowerInvariant() != normalizedName;
						})
						.ToArray();
					File.WriteAllLines(warningFilePath, lines);
				}

				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザーを削除: {playerName}");
				return true;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー削除エラー: {ex.Message}");
				return false;
			}
		}

	}
}