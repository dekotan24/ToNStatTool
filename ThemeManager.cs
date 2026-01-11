using System;
using System.Drawing;
using System.Windows.Forms;

namespace ToNStatTool
{
	/// <summary>
	/// アプリケーションテーマの種類
	/// </summary>
	public enum AppTheme
	{
		Light,
		Dark
	}

	/// <summary>
	/// テーマの状態を一元管理する静的クラス
	/// </summary>
	public static class ThemeManager
	{
		// 現在のテーマ（デフォルトはライト）
		private static AppTheme currentTheme = AppTheme.Light;

		// テーマ変更時のイベント
		public static event EventHandler<AppTheme> ThemeChanged;

		/// <summary>
		/// 現在のテーマを取得
		/// </summary>
		public static AppTheme CurrentTheme => currentTheme;

		/// <summary>
		/// ダークテーマかどうか
		/// </summary>
		public static bool IsDark => currentTheme == AppTheme.Dark;

		// ===== ライトモード配色 =====
		public static class Light
		{
			public static readonly Color FormBackground = ColorTranslator.FromHtml("#F2F2F2");
			public static readonly Color Text = ColorTranslator.FromHtml("#202020");
			public static readonly Color CommonBackground = ColorTranslator.FromHtml("#F2F2F2");
			public static readonly Color GroupBoxBackground = ColorTranslator.FromHtml("#F2F2F2");
			public static readonly Color TextBoxBackground = ColorTranslator.FromHtml("#FFFFFF");
			public static readonly Color ButtonBackground = ColorTranslator.FromHtml("#E0E0E0");
			public static readonly Color ListViewBackground = ColorTranslator.FromHtml("#FFFFFF");
			
			// テラー表示ウィンドウ専用（病院・研究施設風の不穏な白）
			public static readonly Color TerrorFormBackground = ColorTranslator.FromHtml("#F6F6F6");
			public static readonly Color TerrorPanelBackground = ColorTranslator.FromHtml("#F6F6F6");
			public static readonly Color TerrorBottomPanel = ColorTranslator.FromHtml("#E8E8E8");
			public static readonly Color TerrorDragHandle = ColorTranslator.FromHtml("#D0D0D0");
			public static readonly Color TerrorDragHandleLine = Color.Gray;
			
			// テラー表示ウィンドウのテキスト色
			public static readonly Color TerrorPlayerCount = Color.Black;
			public static readonly Color TerrorPlayerCountWarning = Color.DarkRed;
			public static readonly Color TerrorElapsedTime = Color.DarkGreen;
			public static readonly Color TerrorCurrentRound = Color.SteelBlue;
			public static readonly Color TerrorNextRound = Color.DarkOrange;
			public static readonly Color TerrorNextRoundDisabled = Color.Gray;
			
			// プレイヤーリストの色
			public static readonly Color PlayerSelf = Color.Blue;
			public static readonly Color PlayerDead = Color.Red;
			public static readonly Color PlayerWarning = Color.DarkOrange;
			public static readonly Color PlayerCountLabel = ColorTranslator.FromHtml("#202020");
			
			// ラウンドログの色
			public static readonly Color RoundLogSurvived = Color.Green;
			public static readonly Color RoundLogDied = Color.Red;
			
			// 次ラウンド予測の色（ライト用）
			public static readonly Color PredictionBloodMoon = Color.DarkRed;
			public static readonly Color PredictionTwilight = Color.Goldenrod;
			public static readonly Color PredictionMysticMoon = Color.Teal;
			public static readonly Color PredictionSolstice = Color.DarkGreen;
			public static readonly Color PredictionNormal = Color.Green;
			public static readonly Color PredictionSpecial = Color.OrangeRed;  // DarkOrangeより濃い色
			public static readonly Color PredictionDisabled = Color.Gray;
			
			// テラー特性の色（ライト用 - 白背景で見やすい濃いめの色）
			public static readonly Color TraitMovement = Color.FromArgb(0, 100, 180);       // 濃い青
			public static readonly Color TraitAttack = Color.FromArgb(180, 0, 0);           // 濃い赤
			public static readonly Color TraitSpecial = Color.FromArgb(128, 0, 128);        // 紫
			public static readonly Color TraitSpeed = Color.FromArgb(180, 100, 0);          // 濃いオレンジ
			public static readonly Color TraitCounter = Color.FromArgb(150, 0, 50);         // 濃い赤紫
			public static readonly Color TraitDefault = Color.FromArgb(60, 60, 60);         // 濃いグレー
		}

		// ===== ダークモード配色 =====
		public static class Dark
		{
			public static readonly Color FormBackground = ColorTranslator.FromHtml("#202020");
			public static readonly Color Text = Color.WhiteSmoke;
			public static readonly Color CommonBackground = ColorTranslator.FromHtml("#2A2A2A");
			public static readonly Color GroupBoxBackground = ColorTranslator.FromHtml("#2A2A2A");
			public static readonly Color TextBoxBackground = ColorTranslator.FromHtml("#333333");
			public static readonly Color ButtonBackground = ColorTranslator.FromHtml("#3A3A3A");
			public static readonly Color ListViewBackground = ColorTranslator.FromHtml("#2A2A2A");
			
			// テラー表示ウィンドウ専用（既存のかっこいいダークテーマ）
			public static readonly Color TerrorFormBackground = Color.FromArgb(30, 30, 30);
			public static readonly Color TerrorPanelBackground = Color.FromArgb(30, 30, 30);
			public static readonly Color TerrorBottomPanel = Color.FromArgb(45, 45, 45);
			public static readonly Color TerrorDragHandle = Color.FromArgb(70, 70, 70);
			public static readonly Color TerrorDragHandleLine = Color.LightGray;
			
			// テラー表示ウィンドウのテキスト色
			public static readonly Color TerrorPlayerCount = Color.White;
			public static readonly Color TerrorPlayerCountWarning = Color.Red;
			public static readonly Color TerrorElapsedTime = Color.LightGreen;
			public static readonly Color TerrorCurrentRound = Color.Cyan;
			public static readonly Color TerrorNextRound = Color.Yellow;
			public static readonly Color TerrorNextRoundDisabled = Color.Gray;
			
			// プレイヤーリストの色
			public static readonly Color PlayerSelf = Color.DeepSkyBlue;
			public static readonly Color PlayerDead = Color.Salmon;
			public static readonly Color PlayerWarning = Color.Orange;
			public static readonly Color PlayerCountLabel = Color.WhiteSmoke;
			
			// ラウンドログの色（ダーク背景で見やすい色）
			public static readonly Color RoundLogSurvived = Color.FromArgb(100, 255, 100);  // 明るい緑
			public static readonly Color RoundLogDied = Color.FromArgb(255, 120, 120);      // 明るい赤
			
			// 次ラウンド予測の色（ダーク用）
			public static readonly Color PredictionBloodMoon = Color.FromArgb(255, 100, 120);
			public static readonly Color PredictionTwilight = Color.Gold;
			public static readonly Color PredictionMysticMoon = Color.Cyan;
			public static readonly Color PredictionSolstice = Color.FromArgb(0, 255, 136);
			public static readonly Color PredictionNormal = Color.LightGreen;
			public static readonly Color PredictionSpecial = Color.Orange;
			public static readonly Color PredictionDisabled = Color.Gray;
			
			// テラー特性の色（ダーク用 - 暗い背景で見やすい明るめの色）
			public static readonly Color TraitMovement = Color.FromArgb(100, 180, 255);     // 明るい青
			public static readonly Color TraitAttack = Color.FromArgb(255, 150, 150);       // 明るいサーモンピンク
			public static readonly Color TraitSpecial = Color.FromArgb(220, 150, 255);      // 明るいラベンダー
			public static readonly Color TraitSpeed = Color.FromArgb(255, 200, 100);        // 明るいオレンジ
			public static readonly Color TraitCounter = Color.FromArgb(255, 180, 180);      // 明るいピンク
			public static readonly Color TraitDefault = Color.FromArgb(200, 200, 200);      // 明るいグレー
		}

		/// <summary>
		/// テーマを切り替える
		/// </summary>
		public static void ToggleTheme()
		{
			currentTheme = (currentTheme == AppTheme.Light) ? AppTheme.Dark : AppTheme.Light;
			ThemeChanged?.Invoke(null, currentTheme);
		}

		/// <summary>
		/// テーマを設定する
		/// </summary>
		public static void SetTheme(AppTheme theme)
		{
			if (currentTheme != theme)
			{
				currentTheme = theme;
				ThemeChanged?.Invoke(null, currentTheme);
			}
		}

		/// <summary>
		/// フォームにテーマを適用する（再帰的に全コントロールを走査）
		/// </summary>
		public static void Apply(Form form)
		{
			if (form == null) return;

			// フォーム自体の色を設定
			form.BackColor = IsDark ? Dark.FormBackground : Light.FormBackground;
			form.ForeColor = IsDark ? Dark.Text : Light.Text;

			// 全コントロールを再帰的に処理
			ApplyToControls(form.Controls);
		}

		/// <summary>
		/// コントロールコレクションに再帰的にテーマを適用
		/// </summary>
		private static void ApplyToControls(Control.ControlCollection controls)
		{
			foreach (Control control in controls)
			{
				ApplyToControl(control);

				// 子コントロールがあれば再帰処理
				if (control.HasChildren)
				{
					ApplyToControls(control.Controls);
				}
			}
		}

		/// <summary>
		/// 個別のコントロールにテーマを適用
		/// </summary>
		private static void ApplyToControl(Control control)
		{
			// コントロールの種類に応じて適切な色を設定
			switch (control)
			{
				case GroupBox groupBox:
					groupBox.ForeColor = IsDark ? Dark.Text : Light.Text;
					groupBox.BackColor = IsDark ? Dark.GroupBoxBackground : Light.GroupBoxBackground;
					break;

				case TextBox textBox:
					textBox.BackColor = IsDark ? Dark.TextBoxBackground : Light.TextBoxBackground;
					textBox.ForeColor = IsDark ? Dark.Text : Light.Text;
					break;

				case Button button:
					button.BackColor = IsDark ? Dark.ButtonBackground : Light.ButtonBackground;
					button.ForeColor = IsDark ? Dark.Text : Light.Text;
					button.FlatStyle = FlatStyle.Flat;
					button.FlatAppearance.BorderColor = IsDark ? Color.FromArgb(80, 80, 80) : Color.FromArgb(180, 180, 180);
					break;

				case CheckBox checkBox:
					// CheckBox（Appearance.Button含む）
					checkBox.ForeColor = IsDark ? Dark.Text : Light.Text;
					if (checkBox.Appearance == Appearance.Button)
					{
						checkBox.BackColor = IsDark ? Dark.ButtonBackground : Light.ButtonBackground;
						checkBox.FlatStyle = FlatStyle.Flat;
						checkBox.FlatAppearance.BorderColor = IsDark ? Color.FromArgb(80, 80, 80) : Color.FromArgb(180, 180, 180);
					}
					break;

				case Label label:
					// 特定の名前を持つラベルは色を保持（例：ステータスラベル）
					if (!label.Name.Contains("Status") && label.ForeColor != Color.Red && label.ForeColor != Color.Green)
					{
						label.ForeColor = IsDark ? Dark.Text : Light.Text;
					}
					break;

				case ListView listView:
					listView.BackColor = IsDark ? Dark.ListViewBackground : Light.ListViewBackground;
					listView.ForeColor = IsDark ? Dark.Text : Light.Text;
					break;

				case DataGridView dataGridView:
					dataGridView.BackgroundColor = IsDark ? Dark.CommonBackground : Light.CommonBackground;
					dataGridView.DefaultCellStyle.BackColor = IsDark ? Dark.ListViewBackground : Light.ListViewBackground;
					dataGridView.DefaultCellStyle.ForeColor = IsDark ? Dark.Text : Light.Text;
					dataGridView.ColumnHeadersDefaultCellStyle.BackColor = IsDark ? Dark.ButtonBackground : Light.ButtonBackground;
					dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = IsDark ? Dark.Text : Light.Text;
					dataGridView.EnableHeadersVisualStyles = false;
					dataGridView.GridColor = IsDark ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
					break;

				case TabControl tabControl:
					tabControl.BackColor = IsDark ? Dark.CommonBackground : Light.CommonBackground;
					break;

				case TabPage tabPage:
					tabPage.BackColor = IsDark ? Dark.CommonBackground : Light.CommonBackground;
					tabPage.ForeColor = IsDark ? Dark.Text : Light.Text;
					break;

				case NumericUpDown numericUpDown:
					numericUpDown.BackColor = IsDark ? Dark.TextBoxBackground : Light.TextBoxBackground;
					numericUpDown.ForeColor = IsDark ? Dark.Text : Light.Text;
					break;

				case ComboBox comboBox:
					comboBox.BackColor = IsDark ? Dark.TextBoxBackground : Light.TextBoxBackground;
					comboBox.ForeColor = IsDark ? Dark.Text : Light.Text;
					comboBox.FlatStyle = FlatStyle.Flat;
					break;

				case TrackBar trackBar:
					trackBar.BackColor = IsDark ? Dark.FormBackground : Light.FormBackground;
					break;

				case Panel panel:
					// 特定のパネルは除外（テラー表示ウィンドウのパネルなど）
					if (!panel.Name.Contains("terror") && !panel.Name.Contains("Terror"))
					{
						panel.BackColor = IsDark ? Dark.CommonBackground : Light.CommonBackground;
					}
					break;

				default:
					// その他のコントロールは基本色のみ設定
					control.ForeColor = IsDark ? Dark.Text : Light.Text;
					break;
			}
		}

		/// <summary>
		/// テラー表示ウィンドウ専用のテーマ適用
		/// </summary>
		public static void ApplyToTerrorDisplayForm(Form form, Panel terrorPanel, Panel bottomPanel, Label dragHandle,
			Label labelPlayerCount, Label labelElapsedTime, Label labelCurrentRound, Label labelNextRound)
		{
			if (form == null) return;

			// フォーム背景
			form.BackColor = IsDark ? Dark.TerrorFormBackground : Light.TerrorFormBackground;

			// テラーパネル
			if (terrorPanel != null)
				terrorPanel.BackColor = IsDark ? Dark.TerrorPanelBackground : Light.TerrorPanelBackground;

			// 下部パネル
			if (bottomPanel != null)
				bottomPanel.BackColor = IsDark ? Dark.TerrorBottomPanel : Light.TerrorBottomPanel;

			// ドラッグハンドル
			if (dragHandle != null)
				dragHandle.BackColor = IsDark ? Dark.TerrorDragHandle : Light.TerrorDragHandle;

			// ラベル色
			if (labelPlayerCount != null)
				labelPlayerCount.ForeColor = IsDark ? Dark.TerrorPlayerCount : Light.TerrorPlayerCount;

			if (labelElapsedTime != null)
				labelElapsedTime.ForeColor = IsDark ? Dark.TerrorElapsedTime : Light.TerrorElapsedTime;

			if (labelCurrentRound != null)
				labelCurrentRound.ForeColor = IsDark ? Dark.TerrorCurrentRound : Light.TerrorCurrentRound;

			if (labelNextRound != null)
				labelNextRound.ForeColor = IsDark ? Dark.TerrorNextRound : Light.TerrorNextRound;
		}

		/// <summary>
		/// ドラッグハンドルの線の色を取得
		/// </summary>
		public static Color GetDragHandleLineColor()
		{
			return IsDark ? Dark.TerrorDragHandleLine : Light.TerrorDragHandleLine;
		}

		/// <summary>
		/// プレイヤーカウント警告色を取得
		/// </summary>
		public static Color GetPlayerCountWarningColor()
		{
			return IsDark ? Dark.TerrorPlayerCountWarning : Light.TerrorPlayerCountWarning;
		}

		/// <summary>
		/// プレイヤーカウント通常色を取得
		/// </summary>
		public static Color GetPlayerCountNormalColor()
		{
			return IsDark ? Dark.TerrorPlayerCount : Light.TerrorPlayerCount;
		}

		/// <summary>
		/// 次ラウンド予測の色を取得
		/// </summary>
		public static Color GetPredictionColor(string predictionType)
		{
			switch (predictionType.ToLower())
			{
				case "blood":
				case "blood moon":
					return IsDark ? Dark.PredictionBloodMoon : Light.PredictionBloodMoon;
				case "twilight":
					return IsDark ? Dark.PredictionTwilight : Light.PredictionTwilight;
				case "mystic":
				case "mystic moon":
					return IsDark ? Dark.PredictionMysticMoon : Light.PredictionMysticMoon;
				case "solstice":
					return IsDark ? Dark.PredictionSolstice : Light.PredictionSolstice;
				case "normal":
					return IsDark ? Dark.PredictionNormal : Light.PredictionNormal;
				case "special":
					return IsDark ? Dark.PredictionSpecial : Light.PredictionSpecial;
				case "disabled":
				default:
					return IsDark ? Dark.PredictionDisabled : Light.PredictionDisabled;
			}
		}

		/// <summary>
		/// テーマ切替ボタンのテキストを取得
		/// </summary>
		public static string GetThemeButtonText()
		{
			return IsDark ? "☀" : "🌙";
		}

		/// <summary>
		/// テラー特性の色を取得
		/// </summary>
		public static Color GetTraitColor(TerrorTraitCategory category)
		{
			switch (category)
			{
				case TerrorTraitCategory.Movement:
					return IsDark ? Dark.TraitMovement : Light.TraitMovement;
				case TerrorTraitCategory.Attack:
					return IsDark ? Dark.TraitAttack : Light.TraitAttack;
				case TerrorTraitCategory.Special:
					return IsDark ? Dark.TraitSpecial : Light.TraitSpecial;
				case TerrorTraitCategory.Speed:
					return IsDark ? Dark.TraitSpeed : Light.TraitSpeed;
				case TerrorTraitCategory.Counter:
					return IsDark ? Dark.TraitCounter : Light.TraitCounter;
				default:
					return IsDark ? Dark.TraitDefault : Light.TraitDefault;
			}
		}
	}
}
