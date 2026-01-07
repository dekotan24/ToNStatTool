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
		/// WebSocket URL
		/// </summary>
		public string WebSocketUrl { get; set; } = "ws://localhost:11398";

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
