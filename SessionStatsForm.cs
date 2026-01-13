using System;
using System.Drawing;
using System.Windows.Forms;

namespace ToNStatTool
{
	/// <summary>
	/// セッション統計を表示するフォーム（非モーダル）
	/// </summary>
	public partial class SessionStatsForm : Form
	{
		private SessionStats sessionStats;
		private Label labelSurvivals;
		private Label labelDeaths;
		private Label labelSurvivalRate;
		private Label labelTotalRounds;
		private Label labelStuns;
		private Label labelStunsAll;
		private Label labelTopStuns;
		private Label labelTopStunsAll;
		private Label labelDamageTaken;
		private Button buttonReset;
		private Button buttonClose;

		public SessionStatsForm(SessionStats stats)
		{
			sessionStats = stats;
			InitializeComponent();
			ApplyTheme();
			UpdateDisplay();
		}

		private void InitializeComponent()
		{
			this.Text = "セッション統計";
			this.Size = new Size(320, 400);
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.StartPosition = FormStartPosition.CenterParent;
			this.ShowInTaskbar = false;

			int y = 20;
			int labelWidth = 150;
			int valueWidth = 120;

			// ラウンド情報
			var labelRoundHeader = new Label();
			labelRoundHeader.Text = "【ラウンド情報】";
			labelRoundHeader.Location = new Point(20, y);
			labelRoundHeader.Size = new Size(260, 20);
			labelRoundHeader.Font = new Font(this.Font, FontStyle.Bold);
			this.Controls.Add(labelRoundHeader);
			y += 25;

			AddStatRow("生存:", ref labelSurvivals, ref y, labelWidth, valueWidth);
			AddStatRow("死亡:", ref labelDeaths, ref y, labelWidth, valueWidth);
			AddStatRow("合計ラウンド:", ref labelTotalRounds, ref y, labelWidth, valueWidth);
			AddStatRow("生存率:", ref labelSurvivalRate, ref y, labelWidth, valueWidth);

			y += 15;

			// スタン情報
			var labelStunHeader = new Label();
			labelStunHeader.Text = "【スタン情報】";
			labelStunHeader.Location = new Point(20, y);
			labelStunHeader.Size = new Size(260, 20);
			labelStunHeader.Font = new Font(this.Font, FontStyle.Bold);
			this.Controls.Add(labelStunHeader);
			y += 25;

			AddStatRow("自分のスタン:", ref labelStuns, ref y, labelWidth, valueWidth);
			AddStatRow("全員のスタン:", ref labelStunsAll, ref y, labelWidth, valueWidth);
			AddStatRow("最高記録(自分):", ref labelTopStuns, ref y, labelWidth, valueWidth);
			AddStatRow("最高記録(全員):", ref labelTopStunsAll, ref y, labelWidth, valueWidth);

			y += 15;

			// ダメージ情報
			var labelDamageHeader = new Label();
			labelDamageHeader.Text = "【ダメージ情報】";
			labelDamageHeader.Location = new Point(20, y);
			labelDamageHeader.Size = new Size(260, 20);
			labelDamageHeader.Font = new Font(this.Font, FontStyle.Bold);
			this.Controls.Add(labelDamageHeader);
			y += 25;

			AddStatRow("累計ダメージ:", ref labelDamageTaken, ref y, labelWidth, valueWidth);

			y += 30;

			// ボタン
			buttonReset = new Button();
			buttonReset.Text = "リセット";
			buttonReset.Location = new Point(40, y);
			buttonReset.Size = new Size(100, 30);
			buttonReset.Click += ButtonReset_Click;
			this.Controls.Add(buttonReset);

			buttonClose = new Button();
			buttonClose.Text = "閉じる";
			buttonClose.Location = new Point(160, y);
			buttonClose.Size = new Size(100, 30);
			buttonClose.Click += (s, e) => this.Hide();
			this.Controls.Add(buttonClose);
		}

		private void AddStatRow(string labelText, ref Label valueLabel, ref int y, int labelWidth, int valueWidth)
		{
			var label = new Label();
			label.Text = labelText;
			label.Location = new Point(30, y);
			label.Size = new Size(labelWidth, 20);
			this.Controls.Add(label);

			valueLabel = new Label();
			valueLabel.Location = new Point(30 + labelWidth, y);
			valueLabel.Size = new Size(valueWidth, 20);
			valueLabel.TextAlign = ContentAlignment.MiddleRight;
			this.Controls.Add(valueLabel);

			y += 25;
		}

		public void UpdateDisplay()
		{
			if (sessionStats == null) return;

			labelSurvivals.Text = sessionStats.Survivals.ToString();
			labelDeaths.Text = sessionStats.Deaths.ToString();
			labelTotalRounds.Text = sessionStats.TotalRounds.ToString();
			labelSurvivalRate.Text = $"{sessionStats.SurvivalRate:F1}%";
			labelStuns.Text = sessionStats.Stuns.ToString();
			labelStunsAll.Text = sessionStats.StunsAll.ToString();
			labelTopStuns.Text = sessionStats.TopStuns.ToString();
			labelTopStunsAll.Text = sessionStats.TopStunsAll.ToString();
			labelDamageTaken.Text = sessionStats.DamageTaken.ToString();
		}

		private void ButtonReset_Click(object sender, EventArgs e)
		{
			var result = MessageBox.Show(
				"セッション統計をリセットしますか？",
				"確認",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Question);

			if (result == DialogResult.Yes)
			{
				sessionStats.Reset();
				UpdateDisplay();
			}
		}

		public void ApplyTheme()
		{
			bool isDark = ThemeManager.IsDark;
			
			this.BackColor = isDark ? ThemeManager.Dark.FormBackground : ThemeManager.Light.FormBackground;
			this.ForeColor = isDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;

			foreach (Control control in this.Controls)
			{
				if (control is Label label)
				{
					label.ForeColor = isDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;
				}
				else if (control is Button button)
				{
					button.BackColor = isDark ? ThemeManager.Dark.ButtonBackground : ThemeManager.Light.ButtonBackground;
					button.ForeColor = isDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;
					button.FlatStyle = FlatStyle.Flat;
					button.FlatAppearance.BorderColor = isDark ? Color.Gray : Color.DarkGray;
				}
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			// 閉じる代わりに非表示にする
			if (e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				this.Hide();
			}
			base.OnFormClosing(e);
		}
	}
}
