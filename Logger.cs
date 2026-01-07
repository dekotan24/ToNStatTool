using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ToNStatTool
{
	/// <summary>
	/// ログレベル
	/// </summary>
	public enum LogLevel
	{
		Debug = 0,
		Info = 1,
		Warn = 2,
		Error = 3,
		None = 4
	}

	/// <summary>
	/// セッションベースのログ管理クラス（非同期バッファリング対応）
	/// </summary>
	public static class Logger
	{
		private static string currentLogFilePath;
		private static bool isInitialized = false;
		private static bool isShuttingDown = false;

		// 非同期書き込み用
		private static readonly ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
		private static readonly AutoResetEvent logEvent = new AutoResetEvent(false);
		private static Thread writerThread;

		// 設定
		private const string LOG_FOLDER_NAME = "logs";
		private const int MAX_LOG_FILES = 10;
		private const string LOG_FILE_PREFIX = "ToNStatTool_";
		private const string LOG_FILE_EXTENSION = ".log";
		private const int FLUSH_INTERVAL_MS = 1000;

		/// <summary>
		/// 現在のログレベル（これより低いレベルのログは出力されない）
		/// </summary>
		public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info;

		/// <summary>
		/// WebSocketメッセージのログを有効にするか
		/// </summary>
		public static bool EnableWebSocketLogging { get; set; } = false;

		/// <summary>
		/// デバッグ出力（Visual Studioの出力ウィンドウ）を有効にするか
		/// </summary>
		public static bool EnableDebugOutput { get; set; } = false;

		/// <summary>
		/// ログディレクトリのパス
		/// </summary>
		public static string LogDirectory { get; private set; }

		/// <summary>
		/// 現在のログファイルパス
		/// </summary>
		public static string CurrentLogFilePath => currentLogFilePath;

		/// <summary>
		/// ロガーを初期化する（アプリケーション起動時に呼び出す）
		/// </summary>
		public static void Initialize()
		{
			if (isInitialized)
				return;

			try
			{
				// ログディレクトリを設定
				LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LOG_FOLDER_NAME);

				// ログディレクトリが存在しない場合は作成
				if (!Directory.Exists(LogDirectory))
				{
					Directory.CreateDirectory(LogDirectory);
				}

				// 新しいセッション用のログファイルを作成
				string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
				string logFileName = $"{LOG_FILE_PREFIX}{timestamp}{LOG_FILE_EXTENSION}";
				currentLogFilePath = Path.Combine(LogDirectory, logFileName);

				// 古いログファイルを削除
				CleanupOldLogFiles();

				// 非同期書き込みスレッドを開始
				isShuttingDown = false;
				writerThread = new Thread(LogWriterLoop)
				{
					IsBackground = true,
					Name = "LogWriter"
				};
				writerThread.Start();

				isInitialized = true;

				// 初期化ログを記録（これはログレベルに関係なく出力）
				LogInternal("INFO", "Logger", $"ログシステムを初期化しました: {currentLogFilePath}");
				LogInternal("INFO", "Logger", $"アプリケーションバージョン: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
				LogInternal("INFO", "Logger", $"OS: {Environment.OSVersion}");
				LogInternal("INFO", "Logger", $".NET Version: {Environment.Version}");
				LogInternal("INFO", "Logger", $"ログレベル: {CurrentLogLevel}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"ロガー初期化エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// ログ書き込みループ（バックグラウンドスレッド）
		/// </summary>
		private static void LogWriterLoop()
		{
			var buffer = new StringBuilder();
			DateTime lastFlush = DateTime.Now;

			while (!isShuttingDown || !logQueue.IsEmpty)
			{
				// キューからログエントリを取得
				while (logQueue.TryDequeue(out string entry))
				{
					buffer.AppendLine(entry);
				}

				// バッファにデータがあり、一定時間経過したらフラッシュ
				if (buffer.Length > 0)
				{
					bool shouldFlush = (DateTime.Now - lastFlush).TotalMilliseconds >= FLUSH_INTERVAL_MS
						|| buffer.Length > 10000
						|| isShuttingDown;

					if (shouldFlush)
					{
						try
						{
							File.AppendAllText(currentLogFilePath, buffer.ToString(), Encoding.UTF8);
							buffer.Clear();
							lastFlush = DateTime.Now;
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"ログファイル書き込みエラー: {ex.Message}");
						}
					}
				}

				// シャットダウン中でなければ待機
				if (!isShuttingDown)
				{
					logEvent.WaitOne(100);
				}
			}

			// 残りのバッファをフラッシュ
			if (buffer.Length > 0)
			{
				try
				{
					File.AppendAllText(currentLogFilePath, buffer.ToString(), Encoding.UTF8);
				}
				catch { }
			}
		}

		/// <summary>
		/// 内部ログ記録（ログレベルチェックなし）
		/// </summary>
		private static void LogInternal(string level, string category, string message)
		{
			try
			{
				string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
				string logEntry = $"[{timestamp}] [{level}] [{category}] {message}";

				// キューに追加（ノンブロッキング）
				logQueue.Enqueue(logEntry);
				logEvent.Set();

				// デバッグ出力（有効な場合のみ）
				if (EnableDebugOutput)
				{
					System.Diagnostics.Debug.WriteLine(logEntry);
				}
			}
			catch { }
		}

		/// <summary>
		/// ログを記録する
		/// </summary>
		public static void Log(string level, string category, string message)
		{
			if (!isInitialized)
			{
				Initialize();
			}

			LogInternal(level, category, message);
		}

		/// <summary>
		/// INFOレベルのログを記録
		/// </summary>
		public static void Info(string category, string message)
		{
			if (CurrentLogLevel <= LogLevel.Info)
			{
				Log("INFO", category, message);
			}
		}

		/// <summary>
		/// DEBUGレベルのログを記録
		/// </summary>
		public static void Debug(string category, string message)
		{
			if (CurrentLogLevel <= LogLevel.Debug)
			{
				Log("DEBUG", category, message);
			}
		}

		/// <summary>
		/// WARNレベルのログを記録
		/// </summary>
		public static void Warn(string category, string message)
		{
			if (CurrentLogLevel <= LogLevel.Warn)
			{
				Log("WARN", category, message);
			}
		}

		/// <summary>
		/// ERRORレベルのログを記録
		/// </summary>
		public static void Error(string category, string message)
		{
			if (CurrentLogLevel <= LogLevel.Error)
			{
				Log("ERROR", category, message);
			}
		}

		/// <summary>
		/// 例外情報付きのERRORログを記録
		/// </summary>
		public static void Error(string category, string message, Exception ex)
		{
			if (CurrentLogLevel <= LogLevel.Error)
			{
				Log("ERROR", category, $"{message}: {ex.Message}");
				Log("ERROR", category, $"StackTrace: {ex.StackTrace}");
			}
		}

		/// <summary>
		/// WebSocketの生メッセージをログに記録（デバッグ用）
		/// </summary>
		public static void LogWebSocketMessage(string direction, string message)
		{
			// WebSocketログが無効の場合は何もしない
			if (!EnableWebSocketLogging)
				return;

			// メッセージが長い場合は切り詰める
			const int MAX_MESSAGE_LENGTH = 2000;
			string logMessage = message.Length > MAX_MESSAGE_LENGTH
				? message.Substring(0, MAX_MESSAGE_LENGTH) + "... (truncated)"
				: message;

			Log("WS", direction, logMessage);
		}

		/// <summary>
		/// 古いログファイルを削除する
		/// </summary>
		private static void CleanupOldLogFiles()
		{
			try
			{
				var logFiles = Directory.GetFiles(LogDirectory, $"{LOG_FILE_PREFIX}*{LOG_FILE_EXTENSION}")
					.Select(f => new FileInfo(f))
					.OrderByDescending(f => f.CreationTime)
					.ToList();

				if (logFiles.Count > MAX_LOG_FILES)
				{
					var filesToDelete = logFiles.Skip(MAX_LOG_FILES).ToList();
					foreach (var file in filesToDelete)
					{
						try
						{
							file.Delete();
						}
						catch { }
					}
				}
			}
			catch { }
		}

		/// <summary>
		/// 現在のログファイルを開く
		/// </summary>
		public static void OpenCurrentLogFile()
		{
			try
			{
				if (File.Exists(currentLogFilePath))
				{
					System.Diagnostics.Process.Start(currentLogFilePath);
				}
			}
			catch { }
		}

		/// <summary>
		/// ログフォルダを開く
		/// </summary>
		public static void OpenLogFolder()
		{
			try
			{
				if (Directory.Exists(LogDirectory))
				{
					System.Diagnostics.Process.Start("explorer.exe", LogDirectory);
				}
			}
			catch { }
		}

		/// <summary>
		/// セッション終了時のクリーンアップ
		/// </summary>
		public static void Shutdown()
		{
			if (isInitialized)
			{
				LogInternal("INFO", "Logger", "アプリケーションを終了します");
				
				isShuttingDown = true;
				logEvent.Set();

				if (writerThread != null && writerThread.IsAlive)
				{
					writerThread.Join(3000);
				}

				isInitialized = false;
			}
		}

		/// <summary>
		/// デバッグログを有効化（トラブルシューティング用）
		/// </summary>
		public static void EnableVerboseLogging()
		{
			CurrentLogLevel = LogLevel.Debug;
			EnableWebSocketLogging = true;
			EnableDebugOutput = true;
			Info("Logger", "詳細ログモードを有効化しました");
		}

		/// <summary>
		/// 通常ログモードに戻す
		/// </summary>
		public static void DisableVerboseLogging()
		{
			CurrentLogLevel = LogLevel.Info;
			EnableWebSocketLogging = false;
			EnableDebugOutput = false;
			Info("Logger", "通常ログモードに戻しました");
		}
	}
}
