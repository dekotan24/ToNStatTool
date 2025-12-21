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

		// Round tracking
		private RoundLog currentRound = null;
		private readonly List<string> currentRoundItems = new List<string>();
		public event Action<string> OnWarningUserJoined;
		private bool isRoundActive = false;

		public WebSocketClient()
		{
			LoadWarningUsers();
			InitializeWarningSound();
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
			Task.Run(() =>
			{
				try
				{
					string soundFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warning.mp3");

					if (File.Exists(soundFilePath))
					{
						PlayMp3File(soundFilePath);
					}
					else
					{
						// ファイルがない場合はシステム音を使用
						System.Media.SystemSounds.Exclamation.Play();
						System.Diagnostics.Debug.WriteLine("[WARNING] warning.mp3が見つからないためシステム音を使用");
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
			try
			{
				// 既に再生中の場合は停止
				StopCurrentPlayback();

				// NAudioを使用してMP3を再生
				audioFileReader = new AudioFileReader(filePath);
				waveOutDevice = new WaveOutEvent();
				waveOutDevice.Init(audioFileReader);

				// 再生完了時のイベントハンドラ
				waveOutDevice.PlaybackStopped += (sender, e) =>
				{
					StopCurrentPlayback();
				};

				waveOutDevice.Play();
				System.Diagnostics.Debug.WriteLine($"[WARNING] MP3再生開始: {filePath}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] NAudio MP3再生エラー: {ex.Message}");

				// NAudioで失敗した場合はシステム音にフォールバック
				System.Media.SystemSounds.Exclamation.Play();

				// リソースをクリーンアップ
				StopCurrentPlayback();
			}
		}
		
		/// <summary>
		 /// 現在の再生を停止してリソースを解放
		 /// </summary>
		private void StopCurrentPlayback()
		{
			try
			{
				waveOutDevice?.Stop();
				waveOutDevice?.Dispose();
				waveOutDevice = null;

				audioFileReader?.Dispose();
				audioFileReader = null;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 再生停止エラー: {ex.Message}");
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
				var jsonData = JObject.Parse(message);
				lock (dataLock)
				{
					ProcessGameData(jsonData);
				}
			}
			catch (JsonReaderException jsonEx)
			{
				System.Diagnostics.Debug.WriteLine($"JSON解析エラー: {jsonEx.Message}");
				System.Diagnostics.Debug.WriteLine($"エラーメッセージ: {message.Substring(0, Math.Min(200, message.Length))}...");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"メッセージ処理エラー: {ex.Message}");
			}
		}

		private void ProcessGameData(JObject jsonData)
		{
			try
			{
				string eventType = jsonData["Type"]?.ToString() ?? jsonData["TYPE"]?.ToString() ?? "";

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
						System.Diagnostics.Debug.WriteLine($"未処理のイベント: {eventType}");
						break;
				}

				// イベントログに追加
				AddGameEvent(eventType, jsonData);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"ゲームデータ処理エラー: {ex.Message}");
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

				// バッファされたイベントを処理
				if (jsonData["Args"] is JArray args)
				{
					System.Diagnostics.Debug.WriteLine($"[CONNECTED] バッファされたイベント数: {args.Count}");

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
			}

			// テラー更新イベントを発火
			OnTerrorUpdate?.Invoke();
		}

		private void ProcessRoundTypeEvent(JObject jsonData)
		{
			int command = jsonData["Command"]?.ToObject<int>() ?? 0;
			string roundName = jsonData["Name"]?.ToString() ?? jsonData["DisplayName"]?.ToString() ?? "Unknown";

			if (command == 1) // Started
			{
				GameData["roundType"] = $"{roundName} (開始)";
				StartNewRound(roundName);
			}
			else if (command == 0) // Ended
			{
				GameData["roundType"] = $"{roundName} (終了)";
				FinishCurrentRound();
				ResetAllPlayersAlive();
				GameData["saboteur"] = "いいえ";

				// ラウンド終了イベントを発火
				OnRoundEnd?.Invoke();
			}
		}

		private void StartNewRound(string roundType)
		{
			currentRoundItems.Clear();
			currentRound = new RoundLog
			{
				Timestamp = DateTime.Now,
				RoundType = roundType,
				MapName = GetGameDataValue("location", "Unknown").Split('(')[0].Trim(),
				TerrorNames = "",
				Items = "",
				Survived = false
			};

			System.Diagnostics.Debug.WriteLine($"新しいラウンド開始: {roundType}");
		}

		private void FinishCurrentRound()
		{
			if (currentRound == null)
			{
				System.Diagnostics.Debug.WriteLine("警告: currentRoundがnullです。");
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

				// 生存状態を確認
				bool survived = false;

				// まず自分のプレイヤー情報から生存状態を確認
				if (!string.IsNullOrEmpty(LocalPlayerUserId) && Players.ContainsKey(LocalPlayerUserId))
				{
					survived = Players[LocalPlayerUserId].IsAlive;
					System.Diagnostics.Debug.WriteLine($"プレイヤー情報から生存状態を取得: {survived}");
				}
				// プレイヤー情報がない場合はGameDataから確認
				else
				{
					string aliveStatus = GetGameDataValue("alive", "");
					if (!string.IsNullOrEmpty(aliveStatus) && aliveStatus != "-")
					{
						survived = aliveStatus == "生存";
						System.Diagnostics.Debug.WriteLine($"ALIVEイベントから生存状態を取得: {aliveStatus}");
					}
					else
					{
						// デフォルトは死亡とみなす
						survived = false;
						System.Diagnostics.Debug.WriteLine("生存状態が不明のため、死亡とみなします");
					}
				}

				currentRound.Survived = survived;

				// ログに追加
				RoundLogs.Add(currentRound);
				System.Diagnostics.Debug.WriteLine($"ラウンドログに記録: {currentRound.RoundType} - {(survived ? "生存" : "死亡")} - テラー: {currentRound.TerrorNames}");

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

				// ラウンドログを最大999件に制限
				if (RoundLogs.Count > 999)
				{
					RoundLogs.RemoveAt(0);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"ラウンド記録エラー: {ex.Message}");
			}
			finally
			{
				currentRound = null;
			}
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
			GameData["roundActive"] = isActive ? "アクティブ" : "非アクティブ";
			isRoundActive = isActive;
		}

		private void ProcessAliveEvent(JObject jsonData)
		{
			bool isAlive = jsonData["Value"]?.ToObject<bool>() ?? false;
			GameData["alive"] = isAlive ? "生存" : "死亡";

			if (!string.IsNullOrEmpty(LocalPlayerUserId) && Players.ContainsKey(LocalPlayerUserId))
			{
				Players[LocalPlayerUserId].IsAlive = isAlive;
				Players[LocalPlayerUserId].LastSeen = DateTime.Now;
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

				// 警告ユーザーチェック
				if (IsWarningUser(playerName))
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
					LastSeen = DateTime.Now
				};

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
					System.Diagnostics.Debug.WriteLine($"プレイヤー退出: {Players[playerToRemove.Key].Name}");
					Players.Remove(playerToRemove.Key);
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
	}
}