using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ToNStatTool
{
	/// <summary>
	/// テラー表示専用のサブフォーム
	/// </summary>
	public partial class TerrorDisplayForm : Form
	{
		private FlowLayoutPanel terrorPanel;
		private Panel bottomPanel;
		private Panel dragHandle;
		private Label labelPlayerCount;
		private Label labelElapsedTime;
		private Label labelCurrentRound;
		private Label labelNextRound;
		private readonly List<CompactTerrorControl> terrorControls = new List<CompactTerrorControl>();
		private System.Windows.Forms.Timer elapsedTimer;
		private DateTime roundStartTime;
		private bool isRoundActive = false;

		// ドラッグ用の変数
		private bool isDragging = false;
		private Point dragStartPoint;

		// インスタンス状態への参照
		private InstanceState instanceState;

		private const int BOTTOM_PANEL_HEIGHT = 18;
		private const int TERROR_PANEL_HEIGHT = 140;  // 元のサイズに戻す

		public TerrorDisplayForm()
		{
			InitializeComponent();
			InitializeElapsedTimer();
			ApplyTheme(); // テーマを適用
		}

		private void InitializeComponent()
		{
			this.Text = "Terror Display - ToN Stat Tool";
			this.Size = new Size(450, TERROR_PANEL_HEIGHT + BOTTOM_PANEL_HEIGHT);  // 元の幅に戻す
			this.MinimumSize = new Size(450, TERROR_PANEL_HEIGHT + BOTTOM_PANEL_HEIGHT);
			this.StartPosition = FormStartPosition.Manual;
			this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - this.Width, 0);
			this.FormBorderStyle = FormBorderStyle.None;
			this.TopMost = true;
			this.BackColor = Color.FromArgb(30, 30, 30);
			this.Icon = Properties.Resources.AppIcon;

			// テラー表示パネル（上部、140px維持）
			terrorPanel = new FlowLayoutPanel();
			terrorPanel.Location = new Point(0, 0);
			terrorPanel.Size = new Size(this.ClientSize.Width, TERROR_PANEL_HEIGHT);
			terrorPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
			terrorPanel.FlowDirection = FlowDirection.LeftToRight;
			terrorPanel.WrapContents = false;
			terrorPanel.AutoScroll = true;
			terrorPanel.BorderStyle = BorderStyle.FixedSingle;
			terrorPanel.BackColor = Color.FromArgb(30, 30, 30);
			this.Controls.Add(terrorPanel);

			// 下部パネル（ドラッグハンドル + 情報表示）
			bottomPanel = new Panel();
			bottomPanel.Location = new Point(0, TERROR_PANEL_HEIGHT);
			bottomPanel.Size = new Size(this.ClientSize.Width, BOTTOM_PANEL_HEIGHT);
			bottomPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			bottomPanel.BackColor = Color.FromArgb(45, 45, 45);
			this.Controls.Add(bottomPanel);

			// ドラッグハンドル（左端）
			dragHandle = new Panel();
			dragHandle.Size = new Size(18, BOTTOM_PANEL_HEIGHT);
			dragHandle.Location = new Point(0, 0);
			dragHandle.BackColor = Color.FromArgb(70, 70, 70);
			dragHandle.Cursor = Cursors.SizeAll;
			dragHandle.MouseDown += DragHandle_MouseDown;
			dragHandle.MouseMove += DragHandle_MouseMove;
			dragHandle.MouseUp += DragHandle_MouseUp;
			bottomPanel.Controls.Add(dragHandle);

			// ドラッグハンドルに≡マークを描画
			dragHandle.Paint += (s, e) =>
			{
				using (var pen = new Pen(ThemeManager.GetDragHandleLineColor(), 1))
				{
					int y1 = 5, y2 = 9, y3 = 13;
					e.Graphics.DrawLine(pen, 3, y1, 15, y1);
					e.Graphics.DrawLine(pen, 3, y2, 15, y2);
					e.Graphics.DrawLine(pen, 3, y3, 15, y3);
				}
			};

			// 生存人数/総人数ラベル
			labelPlayerCount = new Label();
			labelPlayerCount.Location = new Point(22, 1);
			labelPlayerCount.Size = new Size(60, 16);  // 幅を広げる
			labelPlayerCount.Text = "👥 0/0";
			labelPlayerCount.ForeColor = ThemeManager.IsDark ? Color.White : Color.Black;
			labelPlayerCount.Font = new Font("Meiryo UI", 8);
			labelPlayerCount.TextAlign = ContentAlignment.MiddleLeft;
			bottomPanel.Controls.Add(labelPlayerCount);

			// 経過時間ラベル
			labelElapsedTime = new Label();
			labelElapsedTime.Location = new Point(82, 1);  // 位置調整
			labelElapsedTime.Size = new Size(58, 16);
			labelElapsedTime.Text = "⏱️ 00:00";
			labelElapsedTime.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.TerrorElapsedTime : ThemeManager.Light.TerrorElapsedTime;
			labelElapsedTime.Font = new Font("Meiryo UI", 8);
			labelElapsedTime.TextAlign = ContentAlignment.MiddleLeft;
			bottomPanel.Controls.Add(labelElapsedTime);

			// 現在のラウンドラベル
			labelCurrentRound = new Label();
			labelCurrentRound.Location = new Point(140, 1);  // 位置調整
			labelCurrentRound.Size = new Size(115, 16);
			labelCurrentRound.Text = "🎮 -";
			labelCurrentRound.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.TerrorCurrentRound : ThemeManager.Light.TerrorCurrentRound;
			labelCurrentRound.Font = new Font("Meiryo UI", 8);
			labelCurrentRound.TextAlign = ContentAlignment.MiddleLeft;
			bottomPanel.Controls.Add(labelCurrentRound);

			// 次のラウンド予測ラベル
			labelNextRound = new Label();
			labelNextRound.Location = new Point(255, 1);  // 位置調整
			labelNextRound.Size = new Size(200, 16);
			labelNextRound.Text = "➡️ 次: -";
			labelNextRound.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.TerrorNextRound : ThemeManager.Light.TerrorNextRound;
			labelNextRound.Font = new Font("Meiryo UI", 8);
			labelNextRound.TextAlign = ContentAlignment.MiddleLeft;
			labelNextRound.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			bottomPanel.Controls.Add(labelNextRound);

			// リサイズイベント
			this.Resize += (s, e) =>
			{
				if (terrorPanel != null)
				{
					terrorPanel.Size = new Size(this.ClientSize.Width, this.ClientSize.Height - BOTTOM_PANEL_HEIGHT);
				}
				if (bottomPanel != null)
				{
					bottomPanel.Location = new Point(0, this.ClientSize.Height - BOTTOM_PANEL_HEIGHT);
					bottomPanel.Size = new Size(this.ClientSize.Width, BOTTOM_PANEL_HEIGHT);
				}
			};
		}

		private void InitializeElapsedTimer()
		{
			elapsedTimer = new System.Windows.Forms.Timer();
			elapsedTimer.Interval = 1000;
			elapsedTimer.Tick += ElapsedTimer_Tick;
		}

		private void ElapsedTimer_Tick(object sender, EventArgs e)
		{
			if (isRoundActive)
			{
				TimeSpan elapsed = DateTime.Now - roundStartTime;
				labelElapsedTime.Text = $"⏱️ {elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
			}
		}

		private void DragHandle_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				isDragging = true;
				dragStartPoint = e.Location;
			}
		}

		private void DragHandle_MouseMove(object sender, MouseEventArgs e)
		{
			if (isDragging)
			{
				Point newLocation = this.Location;
				newLocation.X += e.X - dragStartPoint.X;
				newLocation.Y += e.Y - dragStartPoint.Y;

				// 画面端スナップ（10px以内で吸い付く）
				const int SNAP_DISTANCE = 10;
				Rectangle workingArea = Screen.FromControl(this).WorkingArea;

				// 左端スナップ
				if (Math.Abs(newLocation.X - workingArea.Left) < SNAP_DISTANCE)
				{
					newLocation.X = workingArea.Left;
				}
				// 右端スナップ
				if (Math.Abs(newLocation.X + this.Width - workingArea.Right) < SNAP_DISTANCE)
				{
					newLocation.X = workingArea.Right - this.Width;
				}
				// 上端スナップ
				if (Math.Abs(newLocation.Y - workingArea.Top) < SNAP_DISTANCE)
				{
					newLocation.Y = workingArea.Top;
				}
				// 下端スナップ
				if (Math.Abs(newLocation.Y + this.Height - workingArea.Bottom) < SNAP_DISTANCE)
				{
					newLocation.Y = workingArea.Bottom - this.Height;
				}

				this.Location = newLocation;
			}
		}

		private void DragHandle_MouseUp(object sender, MouseEventArgs e)
		{
			isDragging = false;
		}

		/// <summary>
		/// インスタンス状態を設定
		/// </summary>
		public void SetInstanceState(InstanceState state)
		{
			instanceState = state;
		}

		/// <summary>
		/// テラー情報を更新
		/// </summary>
		public void UpdateTerrors(List<TerrorInfo> terrors)
		{
			foreach (var control in terrorControls)
			{
				control.Dispose();
			}
			terrorControls.Clear();
			terrorPanel.Controls.Clear();

			foreach (var terror in terrors)
			{
				var control = new CompactTerrorControl(terror);
				terrorControls.Add(control);
				terrorPanel.Controls.Add(control);
			}
		}

		/// <summary>
		/// プレイヤー数を更新
		/// </summary>
		public void UpdatePlayerCount(int alive, int total)
		{
			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action(() => UpdatePlayerCount(alive, total)));
				return;
			}

			if (labelPlayerCount != null && !labelPlayerCount.IsDisposed)
			{
				labelPlayerCount.Text = $"👥 {alive}/{total}";
				if (total > 0 && alive <= total / 3)
				{
					labelPlayerCount.ForeColor = ThemeManager.IsDark ? Color.Red : Color.DarkRed;
				}
				else
				{
					labelPlayerCount.ForeColor = ThemeManager.IsDark ? Color.White : Color.Black;
				}
			}
		}

		/// <summary>
		/// ラウンド開始時に呼び出す
		/// </summary>
		public void OnRoundStart(string roundType)
		{
			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action(() => OnRoundStart(roundType)));
				return;
			}

			isRoundActive = true;
			roundStartTime = DateTime.Now;
			elapsedTimer.Start();

			Color roundColor = GetRoundTypeColor(roundType);
			labelCurrentRound.ForeColor = roundColor;
			labelCurrentRound.Text = $"🎮 {roundType}";

			// 次のラウンド予測を更新（現在のラウンド種別を考慮）
			UpdateNextRoundPredictionForCurrentRound(roundType);
		}

		/// <summary>
		/// ラウンド情報を同期（途中でフォームを開いた時用）
		/// </summary>
		public void SyncRoundInfo(string roundType, DateTime startTime, bool isActive)
		{
			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action(() => SyncRoundInfo(roundType, startTime, isActive)));
				return;
			}

			isRoundActive = isActive;
			roundStartTime = startTime;

			if (isActive)
			{
				elapsedTimer.Start();
				// 経過時間を即座に更新
				TimeSpan elapsed = DateTime.Now - roundStartTime;
				labelElapsedTime.Text = $"⏱️ {elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
				
				Color roundColor = GetRoundTypeColor(roundType);
				labelCurrentRound.ForeColor = roundColor;
				labelCurrentRound.Text = $"🎮 {roundType}";
				
				// 次ラウンド予測を更新
				UpdateNextRoundPredictionForCurrentRound(roundType);
			}
			else
			{
				elapsedTimer.Stop();
				labelElapsedTime.Text = "⏱️ 00:00";
				labelCurrentRound.Text = "🎮 -";
				UpdateNextRoundPrediction();
			}
		}

		/// <summary>
		/// ラウンド終了時に呼び出す
		/// </summary>
		public void OnRoundEnd()
		{
			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action(() => OnRoundEnd()));
				return;
			}

			isRoundActive = false;
			elapsedTimer.Stop();
			
			// 予測を再更新
			UpdateNextRoundPrediction();
		}

		/// <summary>
		/// 現在のラウンド種別を考慮して次のラウンド予測を更新
		/// </summary>
		private void UpdateNextRoundPredictionForCurrentRound(string currentRoundType)
		{
			if (instanceState == null)
			{
				labelNextRound.Text = "➡️ 次: -";
				labelNextRound.ForeColor = ThemeManager.GetPredictionColor("disabled");
				return;
			}

			string prediction = "";
			Color color = ThemeManager.IsDark ? ThemeManager.Dark.TerrorNextRound : ThemeManager.Light.TerrorNextRound;

			// 現在のラウンドが特殊なら次は通常
			if (IsSpecialRound(currentRoundType))
			{
				prediction = "通常";
				color = ThemeManager.GetPredictionColor("normal");
			}
			else if (IsOverrideRound(currentRoundType))
			{
				prediction = "通常 or 特殊";
				color = ThemeManager.GetPredictionColor("special");
			}
			else
			{
				// 通常ラウンドの場合、カウントを考慮
				int normalCount = instanceState.NormalRoundCount + 1; // 現在のラウンドも含む
				if (normalCount >= 3)
				{
					prediction = "特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
				else if (normalCount == 2)
				{
					prediction = "特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
				else
				{
					prediction = "通常 or 特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
			}

			labelNextRound.Text = $"➡️ 次: {prediction}";
			labelNextRound.ForeColor = color;
		}

		/// <summary>
		/// 次のラウンド予測を更新
		/// </summary>
		public void UpdateNextRoundPrediction()
		{
			// ラウンドがアクティブな場合は、現在のラウンドを考慮した予測を使用
			if (isRoundActive && instanceState != null && !string.IsNullOrEmpty(instanceState.CurrentRoundType))
			{
				UpdateNextRoundPredictionForCurrentRound(instanceState.CurrentRoundType);
				return;
			}

			if (instanceState == null)
			{
				labelNextRound.Text = "➡️ 次: -";
				labelNextRound.ForeColor = ThemeManager.GetPredictionColor("disabled");
				return;
			}

			string prediction = "";
			Color color = ThemeManager.IsDark ? ThemeManager.Dark.TerrorNextRound : ThemeManager.Light.TerrorNextRound;

			// Moon解禁チェック（優先順位: Twilight > Mystic > Blood）
			if (instanceState.AllBirdsMet && !instanceState.TwilightUnlocked)
			{
				prediction = "Twilight";
				color = ThemeManager.GetPredictionColor("twilight");
			}
			else if (instanceState.EstimatedSurvivalCount >= 15 && !instanceState.MysticMoonUnlocked)
			{
				prediction = "Mystic Moon";
				color = ThemeManager.GetPredictionColor("mystic");
			}
			else if (instanceState.AllMoonsUnlocked && !instanceState.SolsticeUnlocked)
			{
				prediction = "Solstice";
				color = ThemeManager.GetPredictionColor("solstice");
			}
			else if (!instanceState.SpecialUnlocked)
			{
				prediction = "通常";
				color = ThemeManager.GetPredictionColor("disabled");
			}
			else
			{
				// 通常の周期予測
				string lastRound = instanceState.LastRoundType.ToLower();
				
				if (IsSpecialRound(lastRound))
				{
					prediction = "通常";
					color = ThemeManager.GetPredictionColor("normal");
				}
				else if (IsOverrideRound(lastRound))
				{
					prediction = "通常 or 特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
				else if (instanceState.NormalRoundCount >= 2)
				{
					prediction = "特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
				else if (instanceState.NormalRoundCount == 1)
				{
					prediction = "通常 or 特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
				else
				{
					prediction = "通常 or 特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
			}

			labelNextRound.Text = $"➡️ 次: {prediction}";
			labelNextRound.ForeColor = color;
		}

		/// <summary>
		/// 特殊ラウンドかどうかを判定
		/// </summary>
		private bool IsSpecialRound(string roundType)
		{
			string lower = roundType.ToLower();
			string[] specialRounds = {
				"alternate", "オルタネイト",
				"punished", "パニッシュ",
				"cracked", "狂気",
				"sabotage", "サボタージュ",
				"fog", "霧",
				"bloodbath", "ブラッドバス",
				"double trouble", "ダブルトラブル",
				"midnight", "ミッドナイト",
				"blood moon", "ブラッドムーン",
				"mystic moon", "ミスティックムーン",
				"twilight", "トワイライト",
				"solstice", "ソルスティス"
			};

			foreach (var special in specialRounds)
			{
				if (lower.Contains(special))
					return true;
			}
			return false;
		}

		/// <summary>
		/// 上書きラウンドかどうかを判定
		/// </summary>
		private bool IsOverrideRound(string roundType)
		{
			string lower = roundType.ToLower();
			return lower.Contains("ghost") || lower.Contains("ゴースト") ||
				   lower.Contains("8 pages") || lower.Contains("8ページ") ||
				   lower.Contains("unbound") || lower.Contains("アンバウンド");
		}

		/// <summary>
		/// ラウンドタイプに応じた色を取得
		/// </summary>
		private Color GetRoundTypeColor(string roundType)
		{
			string lower = roundType.ToLower();
			bool isDark = ThemeManager.IsDark;

			if (lower.Contains("classic") || lower == "run" || lower.Contains("走れ"))
				return isDark ? Color.White : Color.Black;
			if (lower.Contains("alternate") || lower.Contains("オルタネイト"))
				return isDark ? Color.White : Color.Black;
			if (lower.Contains("punished") || lower.Contains("パニッシュ"))
				return Color.Yellow;
			if (lower.Contains("cracked") || lower.Contains("狂気"))
				return Color.Magenta;
			if (lower.Contains("sabotage") || lower.Contains("サボタージュ"))
				return Color.Green;
			if (lower.Contains("fog") || lower.Contains("霧"))
				return Color.Gray;
			if (lower.Contains("bloodbath") || lower.Contains("ブラッドバス"))
				return Color.Red;
			if (lower.Contains("midnight") || lower.Contains("ミッドナイト"))
				return Color.DarkRed;
			if (lower.Contains("ghost") || lower.Contains("ゴースト"))
				return Color.DeepSkyBlue;
			if (lower.Contains("8 pages") || lower.Contains("8ページ"))
				return isDark ? Color.White : Color.Black;
			if (lower.Contains("unbound") || lower.Contains("アンバウンド"))
				return Color.Orange;
			if (lower.Contains("blood moon") || lower.Contains("ブラッドムーン"))
				return Color.DarkRed;
			if (lower.Contains("mystic moon") || lower.Contains("ミスティックムーン"))
				return isDark ? Color.Cyan : Color.Teal;
			if (lower.Contains("twilight") || lower.Contains("トワイライト"))
				return Color.Gold;
			if (lower.Contains("solstice") || lower.Contains("ソルスティス"))
				return Color.FromArgb(0, 255, 136);

			return isDark ? Color.Cyan : Color.Teal;
		}

		/// <summary>
		/// 透明度を設定
		/// </summary>
		public void SetOpacity(double opacity)
		{
			this.Opacity = Math.Max(0.1, Math.Min(1.0, opacity));
		}

		/// <summary>
		/// テーマを適用する
		/// </summary>
		public void ApplyTheme()
		{
			// フォーム背景
			this.BackColor = ThemeManager.IsDark 
				? ThemeManager.Dark.TerrorFormBackground 
				: ThemeManager.Light.TerrorFormBackground;

			// テラーパネル
			if (terrorPanel != null)
				terrorPanel.BackColor = ThemeManager.IsDark 
					? ThemeManager.Dark.TerrorPanelBackground 
					: ThemeManager.Light.TerrorPanelBackground;

			// 下部パネル
			if (bottomPanel != null)
				bottomPanel.BackColor = ThemeManager.IsDark 
					? ThemeManager.Dark.TerrorBottomPanel 
					: ThemeManager.Light.TerrorBottomPanel;

			// ドラッグハンドル
			if (dragHandle != null)
			{
				dragHandle.BackColor = ThemeManager.IsDark 
					? ThemeManager.Dark.TerrorDragHandle 
					: ThemeManager.Light.TerrorDragHandle;
				dragHandle.Invalidate(); // 再描画を要求
			}

			// ラベル色
			if (labelPlayerCount != null)
				labelPlayerCount.ForeColor = ThemeManager.IsDark ? Color.White : Color.Black;

			if (labelElapsedTime != null)
				labelElapsedTime.ForeColor = ThemeManager.IsDark 
					? ThemeManager.Dark.TerrorElapsedTime 
					: ThemeManager.Light.TerrorElapsedTime;

			if (labelCurrentRound != null)
				labelCurrentRound.ForeColor = ThemeManager.IsDark 
					? ThemeManager.Dark.TerrorCurrentRound 
					: ThemeManager.Light.TerrorCurrentRound;

			if (labelNextRound != null)
				labelNextRound.ForeColor = ThemeManager.IsDark 
					? ThemeManager.Dark.TerrorNextRound 
					: ThemeManager.Light.TerrorNextRound;

			// 次ラウンド予測を再計算（色を更新）
			UpdateNextRoundPrediction();

			// テラーコントロールの色を更新
			foreach (var control in terrorControls)
			{
				control.ApplyTheme();
			}
		}

		/// <summary>
		/// フォームを閉じる際にタイマーを停止
		/// </summary>
		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			elapsedTimer?.Stop();
			elapsedTimer?.Dispose();
			base.OnFormClosing(e);
		}
	}
}
