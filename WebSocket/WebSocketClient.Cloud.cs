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
	/// クラウド同期（ラウンドログ送信・取得・マージ）
	/// </summary>
	public partial class WebSocketClient
	{
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

	}
}
