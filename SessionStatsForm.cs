using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ToNStatTool
{
	/// <summary>
	/// セッション統計を表示するフォーム（非モーダル）。
	/// 「累計」と「このインスタンス」を並べて表示し、下部にインスタンスの滞在履歴を出す
	/// </summary>
	public partial class SessionStatsForm : Form
	{
		private readonly SessionStats sessionStats;
		private readonly WebSocketClient webSocketClient;

		// 累計列
		private Label labelSurvivals;
		private Label labelDeaths;
		private Label labelSurvivalRate;
		private Label labelTotalRounds;
		private Label labelStuns;
		private Label labelStunsAll;
		private Label labelTopStuns;
		private Label labelTopStunsAll;
		private Label labelDamageTaken;

		// インスタンス列
		private Label labelLobbySurvivals;
		private Label labelLobbyDeaths;
		private Label labelLobbySurvivalRate;
		private Label labelLobbyTotalRounds;
		private Label labelLobbyStuns;
		private Label labelLobbyStunsAll;
		private Label labelLobbyTopStuns;
		private Label labelLobbyTopStunsAll;
		private Label labelLobbyDamageTaken;

		// 現在のインスタンス
		private Label labelCurrentInstance;
		private Label labelCurrentInstanceDuration;

		private ListView listViewVisits;
		private Button buttonReset;
		private Button buttonClose;

		// 見出しラベル（テーマ適用後に色を上書きしないよう別管理）
		private readonly List<Label> headerLabels = new List<Label>();

		private const int LABEL_X = 25;
		private const int LABEL_WIDTH = 150;
		private const int COL1_X = 185;
		private const int COL2_X = 295;
		private const int COL_WIDTH = 100;

		public SessionStatsForm(SessionStats stats, WebSocketClient client = null)
		{
			sessionStats = stats;
			webSocketClient = client;
			InitializeComponent();
			ApplyTheme();
			UpdateDisplay();
		}

		private void InitializeComponent()
		{
			this.Text = "セッション統計";
			this.Size = new Size(430, 700);
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.StartPosition = FormStartPosition.CenterParent;
			this.ShowInTaskbar = false;

			int y = 15;

			// 列見出し
			AddColumnHeader("累計", COL1_X, y);
			AddColumnHeader("このインスタンス", COL2_X, y);
			y += 22;

			AddSectionHeader("【ラウンド情報】", ref y);
			AddStatRow("生存:", ref labelSurvivals, ref labelLobbySurvivals, ref y);
			AddStatRow("死亡:", ref labelDeaths, ref labelLobbyDeaths, ref y);
			AddStatRow("合計ラウンド:", ref labelTotalRounds, ref labelLobbyTotalRounds, ref y);
			AddStatRow("生存率:", ref labelSurvivalRate, ref labelLobbySurvivalRate, ref y);

			y += 12;
			AddSectionHeader("【スタン情報】", ref y);
			AddStatRow("自分のスタン:", ref labelStuns, ref labelLobbyStuns, ref y);
			AddStatRow("全員のスタン:", ref labelStunsAll, ref labelLobbyStunsAll, ref y);
			AddStatRow("最高記録(自分):", ref labelTopStuns, ref labelLobbyTopStuns, ref y);
			AddStatRow("最高記録(全員):", ref labelTopStunsAll, ref labelLobbyTopStunsAll, ref y);

			y += 12;
			AddSectionHeader("【ダメージ情報】", ref y);
			AddStatRow("累計ダメージ:", ref labelDamageTaken, ref labelLobbyDamageTaken, ref y);

			y += 12;
			AddSectionHeader("【現在のインスタンス】", ref y);

			labelCurrentInstance = new Label();
			labelCurrentInstance.Location = new Point(LABEL_X + 5, y);
			labelCurrentInstance.Size = new Size(250, 20);
			labelCurrentInstance.Text = "-";
			this.Controls.Add(labelCurrentInstance);

			labelCurrentInstanceDuration = new Label();
			labelCurrentInstanceDuration.Location = new Point(COL2_X, y);
			labelCurrentInstanceDuration.Size = new Size(COL_WIDTH, 20);
			labelCurrentInstanceDuration.TextAlign = ContentAlignment.MiddleRight;
			labelCurrentInstanceDuration.Text = "-";
			this.Controls.Add(labelCurrentInstanceDuration);
			y += 27;

			AddSectionHeader("【インスタンス履歴】", ref y);

			listViewVisits = new ListView();
			listViewVisits.Location = new Point(LABEL_X, y);
			listViewVisits.Size = new Size(375, 150);
			listViewVisits.View = View.Details;
			listViewVisits.FullRowSelect = true;
			listViewVisits.GridLines = false;
			listViewVisits.MultiSelect = false;
			listViewVisits.HideSelection = true;
			listViewVisits.Columns.Add("種別", 105);
			listViewVisits.Columns.Add("入室", 55);
			listViewVisits.Columns.Add("滞在", 70);
			listViewVisits.Columns.Add("R", 35);
			listViewVisits.Columns.Add("生存率", 100);
			this.Controls.Add(listViewVisits);
			y += 160;

			buttonReset = new Button();
			buttonReset.Text = "リセット";
			buttonReset.Location = new Point(LABEL_X + 60, y);
			buttonReset.Size = new Size(100, 30);
			buttonReset.Click += ButtonReset_Click;
			this.Controls.Add(buttonReset);

			buttonClose = new Button();
			buttonClose.Text = "閉じる";
			buttonClose.Location = new Point(LABEL_X + 180, y);
			buttonClose.Size = new Size(100, 30);
			buttonClose.Click += (s, e) => this.Hide();
			this.Controls.Add(buttonClose);
		}

		private void AddColumnHeader(string text, int x, int y)
		{
			var label = new Label();
			label.Text = text;
			label.Location = new Point(x, y);
			label.Size = new Size(COL_WIDTH, 20);
			label.TextAlign = ContentAlignment.MiddleRight;
			label.Font = new Font(this.Font, FontStyle.Bold);
			this.Controls.Add(label);
			headerLabels.Add(label);
		}

		private void AddSectionHeader(string text, ref int y)
		{
			var label = new Label();
			label.Text = text;
			label.Location = new Point(15, y);
			label.Size = new Size(260, 20);
			label.Font = new Font(this.Font, FontStyle.Bold);
			this.Controls.Add(label);
			headerLabels.Add(label);
			y += 24;
		}

		private void AddStatRow(string labelText, ref Label totalLabel, ref Label lobbyLabel, ref int y)
		{
			var label = new Label();
			label.Text = labelText;
			label.Location = new Point(LABEL_X, y);
			label.Size = new Size(LABEL_WIDTH, 20);
			this.Controls.Add(label);

			totalLabel = new Label();
			totalLabel.Location = new Point(COL1_X, y);
			totalLabel.Size = new Size(COL_WIDTH, 20);
			totalLabel.TextAlign = ContentAlignment.MiddleRight;
			this.Controls.Add(totalLabel);

			lobbyLabel = new Label();
			lobbyLabel.Location = new Point(COL2_X, y);
			lobbyLabel.Size = new Size(COL_WIDTH, 20);
			lobbyLabel.TextAlign = ContentAlignment.MiddleRight;
			this.Controls.Add(lobbyLabel);

			y += 24;
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

			labelLobbySurvivals.Text = sessionStats.LobbySurvivals.ToString();
			labelLobbyDeaths.Text = sessionStats.LobbyDeaths.ToString();
			labelLobbyTotalRounds.Text = sessionStats.LobbyTotalRounds.ToString();
			labelLobbySurvivalRate.Text = $"{sessionStats.LobbySurvivalRate:F1}%";
			labelLobbyStuns.Text = sessionStats.LobbyStuns.ToString();
			labelLobbyStunsAll.Text = sessionStats.LobbyStunsAll.ToString();
			labelLobbyTopStuns.Text = sessionStats.LobbyTopStuns.ToString();
			labelLobbyTopStunsAll.Text = sessionStats.LobbyTopStunsAll.ToString();
			labelLobbyDamageTaken.Text = sessionStats.LobbyDamageTaken.ToString();

			UpdateInstanceSection();
		}

		/// <summary>
		/// 現在のインスタンス表示と滞在履歴を更新する
		/// </summary>
		private void UpdateInstanceSection()
		{
			var visits = webSocketClient?.InstanceVisits;

			if (visits == null || visits.Count == 0)
			{
				labelCurrentInstance.Text = "-";
				labelCurrentInstanceDuration.Text = "-";
				listViewVisits.Items.Clear();
				return;
			}

			// ラウンドログから滞在ごとの成績を集計する。
			// 接続時のリプレイやクラウドから取得した過去ログは入室時刻より古いタイムスタンプを持つため、
			// 基本はインスタンスURLだけで突き合わせる。
			// 同じインスタンスに複数回入った場合のみ、二重計上を避けるため時刻でも切り分ける。
			var logs = webSocketClient?.RoundLogs?.ToList() ?? new List<RoundLog>();
			foreach (var visit in visits)
			{
				var sameInstance = logs.Where(l => l.InstanceUrl == visit.InstanceUrl).ToList();

				bool revisited = visits.Count(v => v.InstanceUrl == visit.InstanceUrl) > 1;
				var related = revisited
					? sameInstance.Where(l =>
						l.Timestamp >= visit.JoinedAt &&
						(visit.LeftAt == null || l.Timestamp <= visit.LeftAt.Value)).ToList()
					: sameInstance;

				visit.Rounds = related.Count;
				visit.Survived = related.Count(l => l.WasOptedIn && l.Survived);
			}

			var current = visits.LastOrDefault(v => v.IsCurrent);
			if (current != null)
			{
				labelCurrentInstance.Text = current.Info.ShortDescription;
				labelCurrentInstance.ForeColor = current.Info.GetTypeColor(ThemeManager.IsDark);
				labelCurrentInstanceDuration.Text = FormatDuration(current.Duration);
			}
			else
			{
				labelCurrentInstance.Text = "-";
				labelCurrentInstance.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;
				labelCurrentInstanceDuration.Text = "-";
			}

			// 新しい滞在が上に来るように並べる
			listViewVisits.BeginUpdate();
			try
			{
				listViewVisits.Items.Clear();

				foreach (var visit in Enumerable.Reverse(visits))
				{
					var item = new ListViewItem(visit.Info.ShortDescription);
					item.SubItems.Add(visit.JoinedAt.ToString("HH:mm"));
					item.SubItems.Add(FormatDuration(visit.Duration));
					item.SubItems.Add(visit.Rounds.ToString());
					item.SubItems.Add(visit.Rounds > 0 ? $"{visit.SurvivalRate:F0}% ({visit.Survived})" : "-");
					item.ForeColor = visit.Info.GetTypeColor(ThemeManager.IsDark);

					if (visit.IsCurrent)
					{
						item.Font = new Font(listViewVisits.Font, FontStyle.Bold);
					}

					listViewVisits.Items.Add(item);
				}
			}
			finally
			{
				listViewVisits.EndUpdate();
			}
		}

		private static string FormatDuration(TimeSpan duration)
		{
			if (duration.TotalHours >= 1)
			{
				return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";
			}
			return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
		}

		private void ButtonReset_Click(object sender, EventArgs e)
		{
			var result = MessageBox.Show(
				"表示中の統計をリセットしますか？\n（ToNSaveManager側の記録は変更されません。再接続すると元の値に戻ります）",
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
				else if (control is ListView listView)
				{
					listView.BackColor = isDark ? ThemeManager.Dark.ListViewBackground : ThemeManager.Light.ListViewBackground;
					listView.ForeColor = isDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;
					listView.BorderStyle = isDark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
				}
			}

			// インスタンス種別の色はテーマで変わるので、行の色も含めて作り直す
			UpdateInstanceSection();
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
