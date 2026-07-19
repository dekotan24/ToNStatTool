using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ToNStatTool.Services
{
	/// <summary>
	/// XSOverlayのNotification API（UDP、既定ポート42069）へ通知を送るサービス。
	/// VRヘッドセット内にプッシュ通知を表示する。送信先はlocalhost固定。
	/// </summary>
	public static class XSOverlayNotifier
	{
		/// <summary>XSOverlay通知全体の有効/無効</summary>
		public static bool Enabled { get; set; } = false;

		/// <summary>送信先UDPポート（XSOverlay既定: 42069）</summary>
		public static int Port { get; set; } = 42069;

		/// <summary>次ラウンド予測を通知するか</summary>
		public static bool NotifyPrediction { get; set; } = true;

		/// <summary>警告ユーザー参加を通知するか</summary>
		public static bool NotifyWarningUser { get; set; } = true;

		/// <summary>アイテムリマインダーを通知するか</summary>
		public static bool NotifyItemReminder { get; set; } = true;

		/// <summary>テラー情報を通知するか</summary>
		public static bool NotifyTerror { get; set; } = true;

		/// <summary>
		/// AppSettingsの内容を反映する（起動時・設定保存時に呼ぶ）
		/// </summary>
		public static void ApplySettings(AppSettings settings)
		{
			if (settings == null) return;
			Enabled = settings.EnableXSOverlayNotify;
			Port = settings.XSOverlayPort;
			NotifyPrediction = settings.XSOverlayNotifyPrediction;
			NotifyWarningUser = settings.XSOverlayNotifyWarningUser;
			NotifyItemReminder = settings.XSOverlayNotifyItemReminder;
			NotifyTerror = settings.XSOverlayNotifyTerror;
		}

		/// <summary>
		/// 通知を送信する（Enabledでない場合は何もしない）
		/// </summary>
		public static void Send(string title, string content, float timeout = 4f, float volume = 0.7f)
		{
			if (!Enabled) return;
			SendRaw(Port, title, content, timeout, volume);
		}

		/// <summary>
		/// 設定に関わらず指定ポートへ通知を送信する（設定画面のテスト送信用）
		/// </summary>
		public static void SendRaw(int port, string title, string content, float timeout = 4f, float volume = 0.7f)
		{
			// 内容の行数に応じて通知の高さを調整
			int lineCount = string.IsNullOrEmpty(content) ? 0 : content.Split('\n').Length;
			int height = 100 + Math.Max(0, lineCount - 1) * 30;

			var message = new
			{
				messageType = 1,        // 1 = Notification
				index = 0,
				timeout = timeout,
				height = height,
				opacity = 1f,
				volume = volume,
				audioPath = "default",
				title = title,
				content = content,
				useBase64Icon = false,
				icon = "default",
				sourceApp = "ToNStatTool"
			};

			string json = JsonConvert.SerializeObject(message);
			byte[] data = Encoding.UTF8.GetBytes(json);

			// UI/受信スレッドをブロックしないよう非同期送信。失敗してもアプリ動作には影響させない
			Task.Run(() =>
			{
				try
				{
					using (var client = new UdpClient())
					{
						client.Send(data, data.Length, "127.0.0.1", port);
					}
					Logger.Info("XSOverlay", $"通知送信: {title} - {content.Replace("\n", " / ")}");
				}
				catch (Exception ex)
				{
					// XSOverlay未起動でも失敗しうるため、詳細ログのみに記録
					Logger.Debug("XSOverlay", $"通知送信エラー: {ex.Message}");
				}
			});
		}
	}
}
