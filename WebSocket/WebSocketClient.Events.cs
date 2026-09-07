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
	/// WebSocketイベントメッセージの解析・振り分け処理
	/// </summary>
	public partial class WebSocketClient
	{
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

						// ラウンド周期・特殊/Moon解禁状態は前のインスタンスのものなので破棄する
						// （引き継ぐと新インスタンスで予測が最初からずれる）
						InstanceState.ResetForNewInstance();
						Logger.Info("Instance", "インスタンス移動のためラウンド予測状態をリセット");
					}
					
					lastInstanceUrl = instanceUrl;
					InstanceState.InstanceUrl = instanceUrl;
					UpdateInstanceVisit(instanceUrl);
					Logger.Info("Instance", $"インスタンスURL更新: {instanceUrl} [{InstanceState.InstanceInfo.ShortDescription}]");

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


		/// <summary>
		/// インスタンス滞在履歴を更新する。
		/// 別インスタンスに移ったら直前の記録を閉じて、新しい記録を開始する
		/// </summary>
		private void UpdateInstanceVisit(string instanceUrl)
		{
			if (string.IsNullOrEmpty(instanceUrl)) return;

			try
			{
				var current = InstanceVisits.LastOrDefault(v => v.IsCurrent);

				// 同じインスタンスのイベントが再送された場合は何もしない
				if (current != null && current.InstanceUrl == instanceUrl) return;

				if (current != null)
				{
					current.LeftAt = DateTime.Now;
					Logger.Info("Instance", $"インスタンス滞在を記録: {current.Info.ShortDescription} / 滞在 {current.Duration:hh\\:mm\\:ss}");
				}

				InstanceVisits.Add(new InstanceVisit
				{
					InstanceUrl = instanceUrl,
					Info = VRChatInstanceParser.Parse(instanceUrl),
					JoinedAt = DateTime.Now
				});
			}
			catch (Exception ex)
			{
				Logger.Error("Instance", "インスタンス滞在履歴の更新エラー", ex);
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
					case "LobbyDeaths":
						SessionStats.LobbyDeaths = valueToken.ToObject<int?>() ?? 0;
						break;
					case "LobbyStuns":
						SessionStats.LobbyStuns = valueToken.ToObject<int?>() ?? 0;
						break;
					case "LobbyStunsAll":
						SessionStats.LobbyStunsAll = valueToken.ToObject<int?>() ?? 0;
						break;
					case "LobbyTopStuns":
						SessionStats.LobbyTopStuns = valueToken.ToObject<int?>() ?? 0;
						break;
					case "LobbyTopStunsAll":
						SessionStats.LobbyTopStunsAll = valueToken.ToObject<int?>() ?? 0;
						break;
					case "LobbyDamageTaken":
						SessionStats.LobbyDamageTaken = valueToken.ToObject<int?>() ?? 0;
						break;
					case "LobbySurvivals":
						// ロビー生存数が15以上ならMystic Moon解禁
						int lobbySurvivals = valueToken.ToObject<int?>() ?? 0;
						SessionStats.LobbySurvivals = lobbySurvivals;
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

	}
}
