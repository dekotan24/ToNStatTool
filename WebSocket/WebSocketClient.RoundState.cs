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
	/// ラウンド進行・インスタンス状態・Moon/鳥解禁の管理
	/// </summary>
	public partial class WebSocketClient
	{
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

	}
}
