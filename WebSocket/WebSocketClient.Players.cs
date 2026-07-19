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
	/// プレイヤーの参加/退出/生死の管理
	/// </summary>
	public partial class WebSocketClient
	{
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

	}
}
