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
		public event Action<string> OnRoundStart;
		public event Action OnInstanceStateChanged; // インスタンス状態変更イベント
		public event Action OnPlayerCountChanged; // プレイヤー数変更イベント
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
		public RoundStats RoundStats { get; private set; } = new RoundStats();
		public TerrorStats TerrorStats { get; private set; } = new TerrorStats();
		public InstanceState InstanceState { get; private set; } = new InstanceState();

		// Round tracking
		private RoundLog currentRound = null;
		private readonly List<string> currentRoundItems = new List<string>();
		public event Action<string> OnWarningUserJoined;
		public event Action<string, bool> OnPlayerJoinLeave; // プレイヤー名, join=true/leave=false
		private bool isRoundActive = false;
		private bool wasDeadDuringRound = false; // ラウンド中に死亡したかを追跡
		private bool isCurrentRoundFirstMoon = false; // 今回のラウンドが初回Moonかどうか

		// Sound settings
		public SoundSettings SoundSettings { get; private set; } = new SoundSettings();
		private const string SOUND_SETTINGS_FILE = "sound_settings.json";
		private const int MAX_ROUND_LOGS = 2000; // ラウンドログの最大保持数
		private bool isProcessingBufferedEvents = false; // バッファイベント処理中フラグ
		private readonly object audioLock = new object(); // 音声再生の排他制御用

		public WebSocketClient()
		{
			LoadWarningUsers();
			InitializeWarningSound();
			LoadSoundSettings();
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
		/// 警告音を再生
		/// </summary>
		private void PlayWarningSound()
		{
			// サウンドが無効の場合は何もしない
			if (!SoundSettings.EnableWarningUserSound)
			{
				return;
			}

			Task.Run(() =>
			{
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
						PlayMp3File(soundFilePath);
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
			});
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
					System.Diagnostics.Debug.WriteLine($"[CONNECTED] バッファされたイベント数: {args.Count}");

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

			// テラー更新イベントを発火
			OnTerrorUpdate?.Invoke();
		}

		private void ProcessRoundTypeEvent(JObject jsonData)
		{
			int command = jsonData["Command"]?.ToObject<int>() ?? 0;
			string roundName = jsonData["Name"]?.ToString() ?? jsonData["DisplayName"]?.ToString() ?? "Unknown";

			// ラウンドタイプイベントの詳細をログに記録
			Logger.Info("RoundType", $"ROUND_TYPEイベント受信: Command={command}, Name='{roundName}'");
			Logger.Debug("RoundType", $"生データ: {jsonData.ToString(Newtonsoft.Json.Formatting.None)}");

			if (command == 1) // Started
			{
				Logger.Info("RoundType", $"ラウンド開始処理: {roundName}");
				GameData["roundType"] = $"{roundName} (開始)";
				
				// 現在のラウンド種別を記録（次ラウンド予測用）
				InstanceState.CurrentRoundType = roundName;
				
				// Moonラウンド開始時に即座に解禁フラグを立てる
				CheckMoonUnlockOnRoundStart(roundName);
				
				StartNewRound(roundName);
				Logger.Info("RoundType", $"ラウンド開始イベントを発火: {roundName}");
				
				// インスタンス状態変更を通知（次ラウンド予測更新用）
				OnInstanceStateChanged?.Invoke();
			}
			else if (command == 0) // Ended
			{
				Logger.Info("RoundType", $"ラウンド終了処理: {roundName}");
				GameData["roundType"] = $"{roundName} (終了)";
				FinishCurrentRound();
				ResetAllPlayersAlive();
				GameData["saboteur"] = "いいえ";

				// ラウンド終了イベントを発火
				OnRoundEnd?.Invoke();
				Logger.Info("RoundType", $"ラウンド終了イベントを発火: {roundName}");
			}
			else
			{
				Logger.Warn("RoundType", $"不明なCommand値: {command}, Name='{roundName}'");
			}
		}

		private void StartNewRound(string roundType)
		{
			Logger.Info("Round", $"StartNewRound呼び出し: roundType='{roundType}'");
			
			currentRoundItems.Clear();
			wasDeadDuringRound = false; // ラウンド開始時に死亡フラグをリセット
			
			string mapName = GetGameDataValue("location", "Unknown").Split('(')[0].Trim();
			Logger.Debug("Round", $"マップ名: {mapName}");
			
			currentRound = new RoundLog
			{
				Timestamp = DateTime.Now,
				RoundType = roundType,
				MapName = mapName,
				TerrorNames = "",
				Items = "",
				Survived = false
			};

			// ラウンド開始イベントを発火
			OnRoundStart?.Invoke(roundType);

			Logger.Info("Round", $"新しいラウンド開始: {roundType}, マップ: {mapName}");
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

				// アイテムを設定
				currentRound.Items = currentRoundItems.Count > 0 ? string.Join(", ", currentRoundItems) : "なし";

				// 生存状態を確認（ラウンド中に一度でも死亡していれば死亡として記録）
				bool survived = !wasDeadDuringRound;

				// フラグが設定されていない場合のフォールバック（従来のロジック）
				if (!wasDeadDuringRound)
				{
					// プレイヤー情報からも確認
					if (!string.IsNullOrEmpty(LocalPlayerUserId) && Players.ContainsKey(LocalPlayerUserId))
					{
						survived = Players[LocalPlayerUserId].IsAlive;
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

				currentRound.Survived = survived;

				// ログに追加
				RoundLogs.Add(currentRound);
				Logger.Info("Round", $"ラウンドログに記録: {currentRound.RoundType} - {(survived ? "生存" : "死亡")} - テラー: {currentRound.TerrorNames}");

				// 統計を更新
				RoundStats.TotalRounds++;
				if (survived)
				{
					RoundStats.SurvivedRounds++;
				}

				// ラウンド種別の統計も更新
				string roundTypeKey = currentRound.RoundType;
				if (RoundStats.RoundTypeCounts.ContainsKey(roundTypeKey))
				{
					RoundStats.RoundTypeCounts[roundTypeKey]++;
				}
				else
				{
					RoundStats.RoundTypeCounts[roundTypeKey] = 1;
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
				currentRound = null;
			}
		}

		/// <summary>
		/// Moonラウンド開始時に解禁フラグを立てる
		/// </summary>
		private void CheckMoonUnlockOnRoundStart(string roundName)
		{
			string lower = roundName.ToLower();
			bool stateChanged = false;
			
			// 初回Moonフラグをリセット
			isCurrentRoundFirstMoon = false;

			// ※Midnightは開始時には解禁しない（ラウンド終了時に生存者がいる場合のみBlood Moon解禁）

			if (lower.Contains("blood moon") || lower.Contains("blood_moon") || lower.Contains("ブラッドムーン"))
			{
				if (!InstanceState.BloodMoonUnlocked)
				{
					isCurrentRoundFirstMoon = true; // 初回Blood Moon
					InstanceState.BloodMoonUnlocked = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Blood Moon解禁（初回、ラウンド開始時）");
				}
			}
			if (lower.Contains("twilight") || lower.Contains("トワイライト"))
			{
				if (!InstanceState.TwilightUnlocked)
				{
					isCurrentRoundFirstMoon = true; // 初回Twilight
					InstanceState.TwilightUnlocked = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Twilight解禁（初回、ラウンド開始時）");
				}
			}
			if (lower.Contains("mystic moon") || lower.Contains("mystic_moon") || lower.Contains("ミスティックムーン"))
			{
				if (!InstanceState.MysticMoonUnlocked)
				{
					isCurrentRoundFirstMoon = true; // 初回Mystic Moon
					InstanceState.MysticMoonUnlocked = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Mystic Moon解禁（初回、ラウンド開始時）");
				}
			}
			if (lower.Contains("solstice") || lower.Contains("ソルスティス"))
			{
				if (!InstanceState.SolsticeUnlocked)
				{
					isCurrentRoundFirstMoon = true; // 初回Solstice
					InstanceState.SolsticeUnlocked = true;
					stateChanged = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Solstice解禁（初回、ラウンド開始時）");
				}
			}

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
		private void UpdateInstanceState(string roundType, bool survived, string[] terrorNames)
		{
			string lower = roundType.ToLower();

			// 生存時の処理
			if (survived)
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
			if (lower.Contains("midnight") || lower.Contains("ミッドナイト"))
			{
				// インスタンス内の誰かが生存しているかチェック
				int aliveCount = Players.Values.Count(p => p.IsAlive);
				int totalCount = Players.Count;
				System.Diagnostics.Debug.WriteLine($"[InstanceState] Midnight終了時チェック: 生存{aliveCount}/{totalCount}人, BloodMoon解禁={InstanceState.BloodMoonUnlocked}");
				
				// 生存者が1人でもいればBlood Moon解禁
				if (aliveCount > 0 && !InstanceState.BloodMoonUnlocked)
				{
					InstanceState.MidnightSurvived = true;
					InstanceState.BloodMoonUnlocked = true;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Midnight生存者あり → Blood Moon解禁");
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
			if (lower.Contains("blood moon") || lower.Contains("blood_moon") || lower.Contains("ブラッドムーン"))
			{
				InstanceState.BloodMoonUnlocked = true;
				System.Diagnostics.Debug.WriteLine("[InstanceState] Blood Moon解禁");
			}
			if (lower.Contains("twilight") || lower.Contains("トワイライト"))
			{
				InstanceState.TwilightUnlocked = true;
				System.Diagnostics.Debug.WriteLine("[InstanceState] Twilight解禁");
			}
			if (lower.Contains("mystic moon") || lower.Contains("mystic_moon") || lower.Contains("ミスティックムーン"))
			{
				InstanceState.MysticMoonUnlocked = true;
				System.Diagnostics.Debug.WriteLine("[InstanceState] Mystic Moon解禁");
			}
			if (lower.Contains("solstice") || lower.Contains("ソルスティス"))
			{
				InstanceState.SolsticeUnlocked = true;
				System.Diagnostics.Debug.WriteLine("[InstanceState] Solstice解禁");
			}

			// ラウンド周期の更新
			// N=0: 通常枠確定, N=1: 通常/特殊どちらか, N=2: 特殊枠確定
			if (IsClassicRoundType(lower))
			{
				// Classic: 純粋な通常ラウンド（通常枠のみ出現）
				if (InstanceState.NormalRoundCount >= 2)
				{
					// N=2（特殊枠確定）でClassicが出た → 特殊未解放時
					// 特殊枠は消費されたが特殊が出せないのでClassicが代わりに出た
					InstanceState.NormalRoundCount = 0;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Classic(特殊未解放時): 特殊枠消費 → NormalRoundCount=0");
				}
				else
				{
					// N=0 → N=1, N=1 → N=2
					InstanceState.NormalRoundCount++;
					System.Diagnostics.Debug.WriteLine($"[InstanceState] Classic: NormalRoundCount={InstanceState.NormalRoundCount}");
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
			else if (IsMoonRoundType(lower))
			{
				// Moonラウンド（Blood Moon/Twilight/Mystic Moon/Solstice）
				// 初回: Classicを上書きして出現 → Override系と同じ挙動
				// 2回目以降: 特殊ラウンドの1/20で選出 → 特殊枠を消費
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
			else if (IsOverrideRoundType(lower))
			{
				// Run/Ghost/Unbound/8Pages: 通常枠でも特殊枠でも出現可能
				if (InstanceState.NormalRoundCount == 0)
				{
					// N=0（通常枠確定） → N=1
					InstanceState.NormalRoundCount = 1;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Override系(通常枠確定): NormalRoundCount=1");
				}
				else if (InstanceState.NormalRoundCount == 1)
				{
					// N=1（通常/特殊どちらか） → N=1維持（どちらで出たか不明）
					System.Diagnostics.Debug.WriteLine("[InstanceState] Override系(不明): NormalRoundCount=1維持");
				}
				else if (InstanceState.NormalRoundCount >= 2)
				{
					// N=2（特殊枠確定） → N=0（特殊枠消費）
					InstanceState.NormalRoundCount = 0;
					System.Diagnostics.Debug.WriteLine("[InstanceState] Override系(特殊枠消費): NormalRoundCount=0");
				}
			}
			else if (IsSpecialRoundType(lower))
			{
				// 特殊ラウンド → N=0
				InstanceState.NormalRoundCount = 0;
				System.Diagnostics.Debug.WriteLine("[InstanceState] 特殊ラウンド: NormalRoundCount=0");
			}

			InstanceState.LastRoundType = roundType;
		}

		/// <summary>
		/// Classicラウンド判定（純粋な通常ラウンド、通常枠でのみ出現）
		/// </summary>
		private bool IsClassicRoundType(string roundType)
		{
			return roundType.Contains("classic") || roundType.Contains("クラシック");
		}

		/// <summary>
		/// 特殊ラウンド判定（Override系を除く）
		/// </summary>
		private bool IsSpecialRoundType(string roundType)
		{
			string[] specialRounds = {
				"alternate", "オルタネイト",
				"punished", "パニッシュ",
				"cracked", "狂気",
				"sabotage", "サボタージュ",
				"fog", "霧",
				"bloodbath", "ブラッドバス",
				"double trouble", "ダブルトラブル",
				"midnight", "ミッドナイト",
				"blood moon", "ブラッドムーン",
				"mystic moon", "ミスティックムーン",
				"twilight", "トワイライト",
				"solstice", "ソルスティス"
				// GhostはOverride系なので含めない
			};

			foreach (var special in specialRounds)
			{
				if (roundType.Contains(special))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Override系ラウンド判定（Run/Ghost/8Pages/Unbound：通常枠でも特殊枠でも出現可能）
		/// </summary>
		private bool IsOverrideRoundType(string roundType)
		{
			return roundType.Contains("run") || 
			       roundType.Contains("走れ") || 
			       roundType.Contains("ghost") ||
			       roundType.Contains("ゴースト") ||
			       roundType.Contains("8 pages") || 
			       roundType.Contains("8pages") || 
			       roundType.Contains("8ページ") ||
			       roundType.Contains("unbound") ||
			       roundType.Contains("アンバウンド");
		}

		/// <summary>
		/// Moonラウンド判定（Blood Moon/Twilight/Mystic Moon/Solstice）
		/// ※MidnightはMoonラウンドではなく通常の特殊ラウンド
		/// </summary>
		private bool IsMoonRoundType(string roundType)
		{
			return roundType.Contains("blood moon") || roundType.Contains("blood_moon") || roundType.Contains("ブラッドムーン") ||
			       roundType.Contains("twilight") || roundType.Contains("トワイライト") ||
			       roundType.Contains("mystic moon") || roundType.Contains("mystic_moon") || roundType.Contains("ミスティックムーン") ||
			       roundType.Contains("solstice") || roundType.Contains("ソルスティス");
		}

		/// <summary>
		/// インスタンス状態をリセット
		/// </summary>
		public void ResetInstanceState()
		{
			InstanceState.Reset();
			System.Diagnostics.Debug.WriteLine("[InstanceState] リセット");
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
				Players[LocalPlayerUserId].IsAlive = isAlive;
				Players[LocalPlayerUserId].LastSeen = DateTime.Now;
				
				// プレイヤー数変更イベントを発火
				OnPlayerCountChanged?.Invoke();
			}
		}

		private void ProcessSaboteurEvent(JObject jsonData)
		{
			bool isSaboteur = jsonData["Value"]?.ToObject<bool>() ?? false;
			string roundActive = GetGameDataValue("roundActive", "");
			if (roundActive == "非アクティブ" && isSaboteur)
			{
				return;
			}
			GameData["saboteur"] = isSaboteur ? "はい" : "いいえ";
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
				System.Diagnostics.Debug.WriteLine($"[INSTANCE] インスタンス情報を受信");
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
				// 統計情報の処理
				System.Diagnostics.Debug.WriteLine($"[STATS] 統計情報を受信");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[STATS] エラー: {ex.Message}");
			}
		}

		private void ProcessTrackerEvent(JObject jsonData)
		{
			try
			{
				// プレイヤートラッキング情報の処理（これが重要！）
				var playersData = jsonData["Value"] as JArray;
				if (playersData != null)
				{
					System.Diagnostics.Debug.WriteLine($"[TRACKER] プレイヤー追跡情報を受信: {playersData.Count}人");

					// 既存のプレイヤー情報をクリア
					Players.Clear();

					foreach (var playerData in playersData)
					{
						try
						{
							string playerName = playerData["Name"]?.ToString() ?? "Unknown";
							string userId = playerData["UserId"]?.ToString() ?? Guid.NewGuid().ToString();
							bool isAlive = playerData["IsAlive"]?.ToObject<bool>() ?? true;

							playerName = SanitizePlayerName(playerName);

							var player = new PlayerInfo
							{
								Name = playerName,
								UserId = userId,
								IsLocal = userId == LocalPlayerUserId,
								IsAlive = isAlive,
								LastSeen = DateTime.Now
							};

							Players[userId] = player;

							// 警告対象ユーザーかチェック
							if (IsWarningUser(playerName))
							{
								player.IsWarningUser = true;
								PlayWarningSound();
								OnWarningUserJoined?.Invoke(playerName);
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

		private void ProcessItemEvent(JObject jsonData)
		{
			int command = jsonData["Command"]?.ToObject<int>() ?? 0;
			string itemName = jsonData["Name"]?.ToString() ?? "Unknown Item";

			if (command == 1) // Grab
			{
				if (!currentRoundItems.Contains(itemName))
				{
					currentRoundItems.Add(itemName);
				}
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

				// 警告ユーザーチェック（バッファイベント処理中でない場合のみ）
				if (IsWarningUser(playerName) && !isProcessingBufferedEvents)
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
					return;
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

				// バッファイベント処理中でない場合のみJoinサウンドを再生
				if (!isProcessingBufferedEvents)
				{
					PlayJoinLeaveSound(true);
					// イベントを発火
					OnPlayerJoinLeave?.Invoke(playerName, true);
					// プレイヤー数変更イベントを発火
					OnPlayerCountChanged?.Invoke();
				}

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

				// 名前またはIDで検索
				var playerToRemove = Players.FirstOrDefault(p =>
					p.Value.Name == playerName ||
					p.Key == playerName ||
					p.Value.Name.Contains(playerName) ||
					playerName.Contains(p.Value.Name));

				if (playerToRemove.Key != null)
				{
					string removedPlayerName = Players[playerToRemove.Key].Name;
					System.Diagnostics.Debug.WriteLine($"プレイヤー退出: {removedPlayerName}");
					Players.Remove(playerToRemove.Key);

					// バッファイベント処理中でない場合のみLeaveサウンドを再生
					if (!isProcessingBufferedEvents)
					{
						PlayJoinLeaveSound(false);
						// イベントを発火
						OnPlayerJoinLeave?.Invoke(removedPlayerName, false);
						// プレイヤー数変更イベントを発火
						OnPlayerCountChanged?.Invoke();
					}
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

				// より柔軟な検索
				var player = Players.Values.FirstOrDefault(p =>
					p.Name == playerName ||
					p.Name.Contains(playerName) ||
					playerName.Contains(p.Name) ||
					NormalizePlayerName(p.Name) == NormalizePlayerName(playerName));

				if (player != null)
				{
					player.IsAlive = false;
					player.LastSeen = DateTime.Now;
					System.Diagnostics.Debug.WriteLine($"[DEATH] プレイヤー死亡: {player.Name} - メッセージ: {message}");
					
					// プレイヤー数変更イベントを発火
					OnPlayerCountChanged?.Invoke();
				}
				else
				{
					System.Diagnostics.Debug.WriteLine($"[DEATH] 警告: プレイヤー '{playerName}' が見つかりません");
					System.Diagnostics.Debug.WriteLine($"[DEATH] 現在のプレイヤー一覧:");
					foreach (var p in Players.Values)
					{
						System.Diagnostics.Debug.WriteLine($"  - '{p.Name}' (ID: {p.UserId})");
					}
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
		/// テラー名からTerrorInfoを追加する（Mona & The Mountain例外処理付き）
		/// </summary>
		private void AddTerrorFromName(string terrorName, JObject jsonData)
		{
			// Mona & The Mountainは分割しない
			if (terrorName == "Mona & The Mountain")
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

				System.Diagnostics.Debug.WriteLine("[リセット] ラウンド統計、テラー統計、ラウンドログをリセットしました");
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

		/// <summary>
		/// Join/Leaveサウンドを再生
		/// </summary>
		private void PlayJoinLeaveSound(bool isJoin)
		{
			Task.Run(() =>
			{
				try
				{
					string soundPath = isJoin ? SoundSettings.JoinSoundPath : SoundSettings.LeaveSoundPath;
					bool isEnabled = isJoin ? SoundSettings.EnableJoinSound : SoundSettings.EnableLeaveSound;

					if (!isEnabled || string.IsNullOrEmpty(soundPath) || !File.Exists(soundPath))
						return;

					PlayMp3File(soundPath);
					System.Diagnostics.Debug.WriteLine($"[SOUND] {(isJoin ? "Join" : "Leave")}サウンドを再生: {soundPath}");
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[SOUND] サウンド再生エラー: {ex.Message}");
				}
			});
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