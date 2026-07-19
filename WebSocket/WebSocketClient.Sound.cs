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
	/// 通知サウンドの再生・キュー・ミュート判定
	/// </summary>
	public partial class WebSocketClient
	{
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

	}
}
