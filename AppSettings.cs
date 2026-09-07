using System;
using System.IO;
using Newtonsoft.Json;

namespace ToNStatTool
{
	/// <summary>
	/// アプリケーション設定を管理するクラス
	/// </summary>
	public class AppSettings
	{
		private static readonly string SettingsFilePath = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory, "settings.json");

		/// <summary>
		/// テーマ設定（"Light" or "Dark"）
		/// </summary>
		public string Theme { get; set; } = "Light";

		/// <summary>
		/// テラー表示フォームの透明度（10-100）
		/// </summary>
		public int TerrorFormOpacity { get; set; } = 100;

		/// <summary>
		/// テラー表示フォームの位置とサイズを記憶するか
		/// </summary>
		public bool RememberTerrorFormBounds { get; set; } = true;

		/// <summary>
		/// テラー表示フォームのX座標（int.MinValue = 未保存）
		/// </summary>
		public int TerrorFormX { get; set; } = int.MinValue;

		/// <summary>
		/// テラー表示フォームのY座標（int.MinValue = 未保存）
		/// </summary>
		public int TerrorFormY { get; set; } = int.MinValue;

		/// <summary>
		/// テラー表示フォームの幅（0以下 = 未保存）
		/// </summary>
		public int TerrorFormWidth { get; set; } = 0;

		/// <summary>
		/// テラー表示フォームの高さ（0以下 = 未保存）
		/// </summary>
		public int TerrorFormHeight { get; set; } = 0;

		/// <summary>
		/// テラー表示フォームをクリックスルー（マウス操作を透過）するか
		/// </summary>
		public bool TerrorFormClickThrough { get; set; } = false;

		/// <summary>
		/// グローバルホットキーを有効にするか
		/// </summary>
		public bool OverlayHotkeyEnabled { get; set; } = false;

		/// <summary>
		/// オーバーレイ表示切替のホットキー（例: "Ctrl+Shift+T"、空文字で無効）
		/// </summary>
		public string OverlayToggleHotkey { get; set; } = "Ctrl+Shift+T";

		/// <summary>
		/// クリックスルー切替のホットキー（例: "Ctrl+Shift+C"、空文字で無効）
		/// </summary>
		public string ClickThroughHotkey { get; set; } = "Ctrl+Shift+C";

		/// <summary>
		/// WebSocket URL
		/// </summary>
		public string WebSocketUrl { get; set; } = "ws://localhost:11398";

		/// <summary>
		/// 詳細ログを有効にするか
		/// </summary>
		public bool EnableVerboseLog { get; set; } = false;

		/// <summary>
		/// クラウドにラウンド情報を送信するか（デフォルトはオフ）
		/// </summary>
		public bool EnableCloudSync { get; set; } = false;

		/// <summary>
		/// クラウドサーバーのURL
		/// </summary>
		public string CloudServerUrl { get; set; } = "https://ton.fanet.work";

		/// <summary>
		/// クラウドAPIキー（Webで発行したキー）
		/// </summary>
		public string CloudApiKey { get; set; } = "";

		/// <summary>
		/// XSOverlayへのVR通知を有効にするか（デフォルトはオフ）
		/// </summary>
		public bool EnableXSOverlayNotify { get; set; } = false;

		/// <summary>
		/// XSOverlay Notification APIのUDPポート
		/// </summary>
		public int XSOverlayPort { get; set; } = 42069;

		/// <summary>
		/// 次ラウンド予測をXSOverlayに通知するか
		/// </summary>
		public bool XSOverlayNotifyPrediction { get; set; } = true;

		/// <summary>
		/// 警告ユーザー参加をXSOverlayに通知するか
		/// </summary>
		public bool XSOverlayNotifyWarningUser { get; set; } = true;

		/// <summary>
		/// アイテムリマインダーをXSOverlayに通知するか
		/// </summary>
		public bool XSOverlayNotifyItemReminder { get; set; } = true;

		/// <summary>
		/// テラー情報をXSOverlayに通知するか
		/// </summary>
		public bool XSOverlayNotifyTerror { get; set; } = true;

		/// <summary>
		/// 設定をファイルから読み込む
		/// </summary>
		public static AppSettings Load()
		{
			try
			{
				if (File.Exists(SettingsFilePath))
				{
					string json = File.ReadAllText(SettingsFilePath);
					var settings = JsonConvert.DeserializeObject<AppSettings>(json);
					return settings ?? new AppSettings();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"設定読み込みエラー: {ex.Message}");
			}

			return new AppSettings();
		}

		/// <summary>
		/// 設定をファイルに保存する
		/// </summary>
		public void Save()
		{
			try
			{
				string json = JsonConvert.SerializeObject(this, Formatting.Indented);
				File.WriteAllText(SettingsFilePath, json);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"設定保存エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// 現在のテーマをAppTheme列挙型で取得
		/// </summary>
		public AppTheme GetAppTheme()
		{
			return Theme.ToLower() == "dark" ? AppTheme.Dark : AppTheme.Light;
		}

		/// <summary>
		/// テーマを設定
		/// </summary>
		public void SetTheme(AppTheme theme)
		{
			Theme = theme == AppTheme.Dark ? "Dark" : "Light";
		}
	}
}
