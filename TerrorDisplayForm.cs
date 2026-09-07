using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

		// リサイズ用（右下グリップ）
		private Panel resizeGrip;
		private bool isResizing = false;
		private Point resizeStartScreenPoint;
		private Size resizeStartSize;
		private const int RESIZE_GRIP_SIZE = 18;

		// クリックスルー状態
		private bool isClickThrough = false;

		// インスタンス状態への参照
		private InstanceState instanceState;

		// アイテムリマインダー用
		private System.Windows.Forms.Timer reminderTimer;
		private bool isShowingReminder = false;
		private string savedPlayerCountText;
		private string savedElapsedTimeText;
		private string savedCurrentRoundText;
		private Color savedPlayerCountColor;
		private Color savedElapsedTimeColor;
		private Color savedCurrentRoundColor;

		// Unbound表示用
		private ToolTip roundToolTip;
		private string currentUnboundName;

		private const int BOTTOM_PANEL_HEIGHT = 18;
		private const int TERROR_PANEL_HEIGHT = 140;  // 元のサイズに戻す

		/// <summary>
		/// スレッドセーフにUIを更新するヘルパーメソッド
		/// ハンドルが作成されていない場合やフォームが破棄中の場合は何もしない
		/// </summary>
		private void SafeInvoke(Action action)
		{
			try
			{
				if (this.IsDisposed || this.Disposing)
					return;
				
				// UIスレッドから呼ばれている場合は直接実行（ハンドル不要）
				if (!this.InvokeRequired)
				{
					action();
					return;
				}
				
				// 別スレッドからの場合はハンドルが必要
				if (!this.IsHandleCreated)
					return;
					
				this.BeginInvoke(action);
			}
			catch (ObjectDisposedException)
			{
				// フォームが破棄された場合は無視
			}
			catch (InvalidOperationException)
			{
				// ハンドルが無効な場合は無視
			}
		}

		public TerrorDisplayForm()
		{
			InitializeComponent();
			InitializeElapsedTimer();
			InitializeReminderTimer();
			InitializeToolTip();
			ApplyTheme(); // テーマを適用
		}

		private void InitializeToolTip()
		{
			roundToolTip = new ToolTip();
			roundToolTip.InitialDelay = 200;
			roundToolTip.ReshowDelay = 100;
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
			labelCurrentRound.Size = new Size(160, 16);
			labelCurrentRound.Text = "🎮 -";
			labelCurrentRound.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.TerrorCurrentRound : ThemeManager.Light.TerrorCurrentRound;
			labelCurrentRound.Font = new Font("Meiryo UI", 8);
			labelCurrentRound.TextAlign = ContentAlignment.MiddleLeft;
			bottomPanel.Controls.Add(labelCurrentRound);

			// 次のラウンド予測ラベル
			labelNextRound = new Label();
			labelNextRound.Location = new Point(300, 1);
			labelNextRound.Size = new Size(200, 16);
			labelNextRound.Text = "➡️ 次: -";
			labelNextRound.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.TerrorNextRound : ThemeManager.Light.TerrorNextRound;
			labelNextRound.Font = new Font("Meiryo UI", 8);
			labelNextRound.TextAlign = ContentAlignment.MiddleLeft;
			labelNextRound.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			bottomPanel.Controls.Add(labelNextRound);

			// リサイズグリップ（右下。FormBorderStyle.Noneなので枠でのリサイズができないため自前で用意する）
			resizeGrip = new Panel();
			resizeGrip.Size = new Size(RESIZE_GRIP_SIZE, BOTTOM_PANEL_HEIGHT);
			resizeGrip.Cursor = Cursors.SizeNWSE;
			resizeGrip.BackColor = Color.Transparent;
			resizeGrip.MouseDown += ResizeGrip_MouseDown;
			resizeGrip.MouseMove += ResizeGrip_MouseMove;
			resizeGrip.MouseUp += ResizeGrip_MouseUp;
			resizeGrip.Paint += (s, e) =>
			{
				// 斜めの三本線でグリップらしく見せる
				using (var pen = new Pen(ThemeManager.GetDragHandleLineColor(), 1))
				{
					int w = resizeGrip.Width;
					int h = resizeGrip.Height;
					for (int offset = 0; offset < 3; offset++)
					{
						int d = 4 + offset * 4;
						e.Graphics.DrawLine(pen, w - d, h - 2, w - 2, h - d);
					}
				}
			};
			bottomPanel.Controls.Add(resizeGrip);
			resizeGrip.BringToFront();

			LayoutBottomPanel();

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
				LayoutBottomPanel();
			};
		}

		/// <summary>
		/// 下部パネル内の可変幅コントロール（次ラウンド予測・リサイズグリップ）を配置し直す
		/// </summary>
		private void LayoutBottomPanel()
		{
			if (bottomPanel == null) return;

			int panelWidth = bottomPanel.ClientSize.Width;

			if (resizeGrip != null)
			{
				resizeGrip.Location = new Point(Math.Max(0, panelWidth - RESIZE_GRIP_SIZE), 0);
			}

			if (labelNextRound != null && !isShowingReminder)
			{
				// グリップに被らない範囲まで伸ばす
				int available = panelWidth - labelNextRound.Left - RESIZE_GRIP_SIZE - 2;
				labelNextRound.Size = new Size(Math.Max(60, available), 16);
			}
		}

		private void InitializeElapsedTimer()
		{
			elapsedTimer = new System.Windows.Forms.Timer();
			elapsedTimer.Interval = 1000;
			elapsedTimer.Tick += ElapsedTimer_Tick;
		}

		private void ElapsedTimer_Tick(object sender, EventArgs e)
		{
			if (isRoundActive && !isShowingReminder)
			{
				TimeSpan elapsed = DateTime.Now - roundStartTime;
				labelElapsedTime.Text = $"⏱️ {elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
			}
		}

		private void InitializeReminderTimer()
		{
			reminderTimer = new System.Windows.Forms.Timer();
			reminderTimer.Tick += ReminderTimer_Tick;
		}

		private void ReminderTimer_Tick(object sender, EventArgs e)
		{
			reminderTimer.Stop();
			HideItemReminder();
		}

		/// <summary>
		/// アイテムリマインダーを表示（8ページ/パニッシュ終了時）
		/// </summary>
		public void ShowItemReminder(int durationSeconds = 10)
		{
			SafeInvoke(() =>
			{
				if (isShowingReminder) return;

				// 現在の表示内容を保存
				savedPlayerCountText = labelPlayerCount.Text;
				savedElapsedTimeText = labelElapsedTime.Text;
				savedCurrentRoundText = labelCurrentRound.Text;
				savedPlayerCountColor = labelPlayerCount.ForeColor;
				savedElapsedTimeColor = labelElapsedTime.ForeColor;
				savedCurrentRoundColor = labelCurrentRound.ForeColor;

				isShowingReminder = true;

				// リマインダーメッセージを表示
				labelPlayerCount.Text = "⚠";
				labelPlayerCount.ForeColor = Color.Orange;
				labelElapsedTime.Text = "アイテムを持ち直してください。";
				labelElapsedTime.ForeColor = Color.Orange;
				labelElapsedTime.Size = new Size(180, 16);  // 幅を一時的に広げる
				labelCurrentRound.Text = "";

				// タイマーで元に戻す
				reminderTimer.Interval = durationSeconds * 1000;
				reminderTimer.Start();
			});
		}

		/// <summary>
		/// アイテムリマインダーを非表示にして元の表示に戻す
		/// </summary>
		private void HideItemReminder()
		{
			SafeInvoke(() =>
			{
				if (!isShowingReminder) return;

				isShowingReminder = false;

				// 元の表示内容に戻す
				labelPlayerCount.Text = savedPlayerCountText;
				labelPlayerCount.ForeColor = savedPlayerCountColor;
				labelElapsedTime.Text = savedElapsedTimeText;
				labelElapsedTime.ForeColor = savedElapsedTimeColor;
				labelElapsedTime.Size = new Size(58, 16);  // 元のサイズに戻す
				labelCurrentRound.Text = savedCurrentRoundText;
				labelCurrentRound.ForeColor = savedCurrentRoundColor;

				// リマインダー中は幅調整を止めているので、戻ったタイミングで再配置する
				LayoutBottomPanel();
			});
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

		/// <summary>
		/// ユーザー操作（ドラッグ・リサイズ）で位置やサイズが変わったときに発火する。
		/// 異常終了しても位置が残るよう、都度保存させるためのフック
		/// </summary>
		public event Action BoundsUserChanged;

		private void DragHandle_MouseUp(object sender, MouseEventArgs e)
		{
			if (!isDragging) return;

			isDragging = false;
			BoundsUserChanged?.Invoke();
		}

		private void ResizeGrip_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				isResizing = true;
				resizeStartScreenPoint = Control.MousePosition;
				resizeStartSize = this.Size;
			}
		}

		private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
		{
			if (!isResizing) return;

			// グリップ自身が動くのでクライアント座標だと不安定。スクリーン座標の差分で計算する
			Point current = Control.MousePosition;
			int newWidth = resizeStartSize.Width + (current.X - resizeStartScreenPoint.X);
			int newHeight = resizeStartSize.Height + (current.Y - resizeStartScreenPoint.Y);

			newWidth = Math.Max(this.MinimumSize.Width, newWidth);
			newHeight = Math.Max(this.MinimumSize.Height, newHeight);

			this.Size = new Size(newWidth, newHeight);
		}

		private void ResizeGrip_MouseUp(object sender, MouseEventArgs e)
		{
			if (!isResizing) return;

			isResizing = false;
			BoundsUserChanged?.Invoke();
		}

		/// <summary>
		/// 保存されていた位置とサイズを復元する。
		/// モニタ構成が変わって画面外になっている場合は既定位置に戻す
		/// </summary>
		public void ApplySavedBounds(AppSettings settings)
		{
			if (settings == null || !settings.RememberTerrorFormBounds) return;

			int width = settings.TerrorFormWidth > 0 ? settings.TerrorFormWidth : this.Width;
			int height = settings.TerrorFormHeight > 0 ? settings.TerrorFormHeight : this.Height;
			width = Math.Max(this.MinimumSize.Width, width);
			height = Math.Max(this.MinimumSize.Height, height);

			// 位置が未保存ならサイズだけ復元して既定位置のままにする
			if (settings.TerrorFormX == int.MinValue || settings.TerrorFormY == int.MinValue)
			{
				this.Size = new Size(width, height);
				return;
			}

			var bounds = new Rectangle(settings.TerrorFormX, settings.TerrorFormY, width, height);

			if (!Services.OverlayWindowHelper.IsBoundsVisible(bounds))
			{
				Logger.Warn("Overlay", $"保存位置({bounds})が画面外のため既定位置に戻します");
				this.Size = new Size(width, height);
				this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - width, 0);
				return;
			}

			this.Bounds = bounds;
		}

		/// <summary>
		/// 現在の位置とサイズを設定オブジェクトに書き出す（保存自体は呼び出し側で行う）
		/// </summary>
		public void SaveBoundsTo(AppSettings settings)
		{
			if (settings == null || this.IsDisposed) return;
			if (this.WindowState != FormWindowState.Normal) return;

			settings.TerrorFormX = this.Location.X;
			settings.TerrorFormY = this.Location.Y;
			settings.TerrorFormWidth = this.Width;
			settings.TerrorFormHeight = this.Height;
		}

		/// <summary>
		/// 位置とサイズを既定（画面右上・初期サイズ）に戻す
		/// </summary>
		public void ResetBoundsToDefault()
		{
			SafeInvoke(() =>
			{
				this.Size = new Size(450, TERROR_PANEL_HEIGHT + BOTTOM_PANEL_HEIGHT);
				this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - this.Width, 0);
			});
		}

		/// <summary>
		/// クリックスルー中かどうか
		/// </summary>
		public bool IsClickThrough => isClickThrough;

		/// <summary>
		/// クリックスルー（マウス操作をVRChat側に透過）を切り替える。
		/// 有効中はドラッグもリサイズもできなくなるので、ハンドルを隠して分かるようにする
		/// </summary>
		public void SetClickThrough(bool enabled)
		{
			SafeInvoke(() =>
			{
				isClickThrough = enabled;
				Services.OverlayWindowHelper.SetClickThrough(this, enabled);

				if (dragHandle != null) dragHandle.Visible = !enabled;
				if (resizeGrip != null) resizeGrip.Visible = !enabled;
			});
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
		/// <param name="terrors">テラー情報リスト</param>
		/// <param name="unboundName">Unboundのアナウンス名（Unboundラウンド時のみ）</param>
		/// <param name="mapName">現在のマップ名（HFA判定用）</param>
		public void UpdateTerrors(List<TerrorInfo> terrors, string unboundName = null, string mapName = null)
		{
			// スレッドセーフにリストをコピー
			List<TerrorInfo> terrorsCopy;
			try
			{
				terrorsCopy = terrors?.ToList() ?? new List<TerrorInfo>();
			}
			catch (InvalidOperationException)
			{
				return; // コレクションが変更中の場合はスキップ
			}

			// Unbound名を保持
			currentUnboundName = unboundName;

			SafeInvoke(() =>
			{
				try
				{
					foreach (var control in terrorControls)
					{
						control.Dispose();
					}
					terrorControls.Clear();
					terrorPanel.Controls.Clear();

					foreach (var terror in terrorsCopy)
					{
						var control = new CompactTerrorControl(terror, mapName);
						terrorControls.Add(control);
						terrorPanel.Controls.Add(control);
					}

					// Unbound名がある場合、ラウンド種別にツールチップを設定
					UpdateRoundToolTip();
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[TerrorDisplayForm] UpdateTerrors error: {ex.Message}");
				}
			});
		}

		/// <summary>
		/// ラウンド種別ラベルのツールチップを更新
		/// </summary>
		private void UpdateRoundToolTip()
		{
			if (labelCurrentRound == null || labelCurrentRound.IsDisposed || roundToolTip == null)
				return;

			if (!string.IsNullOrEmpty(currentUnboundName))
			{
				roundToolTip.SetToolTip(labelCurrentRound, currentUnboundName);
			}
			else
			{
				roundToolTip.SetToolTip(labelCurrentRound, null);
			}
		}

		/// <summary>
		/// プレイヤー数を更新
		/// </summary>
		public void UpdatePlayerCount(int alive, int total)
		{
			SafeInvoke(() =>
			{
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
			});
		}

		/// <summary>
		/// ラウンド開始時に呼び出す
		/// </summary>
		public void OnRoundStart(ToNRoundType roundType)
		{
			SafeInvoke(() =>
			{
				isRoundActive = true;
				roundStartTime = DateTime.Now;
				elapsedTimer.Start();

				Color roundColor = GetRoundTypeColor(roundType);
				labelCurrentRound.ForeColor = roundColor;
				
				// 上書きフラグをチェックして表示を変更
				string displayName = ToNRoundTypeHelper.GetDisplayName(roundType);
				if (instanceState?.IsCurrentRoundOverride == true)
				{
					displayName += " (上書き)";
				}
				labelCurrentRound.Text = $"🎮 {displayName}";

				// 次のラウンド予測を更新（現在のラウンド種別を考慮）
				UpdateNextRoundPredictionForCurrentRound(roundType);
			});
		}

		/// <summary>
		/// ラウンド情報を同期（途中でフォームを開いた時用）
		/// </summary>
		public void SyncRoundInfo(ToNRoundType roundType, DateTime startTime, bool isActive)
		{
			SafeInvoke(() =>
			{
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
					
					// 上書きフラグをチェックして表示を変更
					string displayName = ToNRoundTypeHelper.GetDisplayName(roundType);
					if (instanceState?.IsCurrentRoundOverride == true)
					{
						displayName += " (上書き)";
					}
					labelCurrentRound.Text = $"🎮 {displayName}";
					
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
			});
		}

		/// <summary>
		/// ラウンド終了時に呼び出す
		/// </summary>
		public void OnRoundEnd()
		{
			SafeInvoke(() =>
			{
				isRoundActive = false;
				elapsedTimer.Stop();

				// Unbound名をクリア
				currentUnboundName = null;
				UpdateRoundToolTip();

				// 予測を再更新
				UpdateNextRoundPrediction();
			});
		}

		/// <summary>
		/// 現在のラウンド種別を考慮して次のラウンド予測を更新
		/// </summary>
		private void UpdateNextRoundPredictionForCurrentRound(ToNRoundType currentRoundType)
		{
			if (instanceState == null)
			{
				labelNextRound.Text = "➡️ 次: -";
				labelNextRound.ForeColor = ThemeManager.GetPredictionColor("disabled");
				return;
			}

			// マスター変更時は特殊確定（ラウンド進行中でも即座に反映）
			if (instanceState.MasterChanged && instanceState.SpecialUnlocked)
			{
				labelNextRound.Text = "➡️ 次: 特殊(MC)";
				labelNextRound.ForeColor = ThemeManager.GetPredictionColor("special");
				return;
			}

			string prediction = "";
			Color color = ThemeManager.IsDark ? ThemeManager.Dark.TerrorNextRound : ThemeManager.Light.TerrorNextRound;

			// 特殊未解放（インスタンス全体で3回生存前）なら次は必ず通常
			if (!instanceState.SpecialUnlocked)
			{
				labelNextRound.Text = "➡️ 次: 通常";
				labelNextRound.ForeColor = ThemeManager.GetPredictionColor("disabled");
				return;
			}

			// 現在のラウンドが特殊なら次は通常
			if (ToNRoundTypeHelper.IsSpecialRound(currentRoundType))
			{
				prediction = "通常";
				color = ThemeManager.GetPredictionColor("normal");
			}
			// Moonラウンドの場合（2回目以降は特殊枚消費）
			else if (ToNRoundTypeHelper.IsMoonRound(currentRoundType))
			{
				if (instanceState.IsCurrentRoundFirstMoon)
				{
					// 初回MoonはOverride系と同じ動作
					// 特殊確定時（N>=2）なら特殊枠消費 → 次は通常確定
					int normalCountAtStart = instanceState.NormalRoundCountAtRoundStart;
					if (normalCountAtStart >= 2)
					{
						prediction = "通常";
						color = ThemeManager.GetPredictionColor("normal");
					}
					else
					{
						prediction = "通常 or 特殊";
						color = ThemeManager.GetPredictionColor("special");
					}
				}
				else
				{
					// 2回目以降Moonは特殊枚消費 → 次は通常
					prediction = "通常";
					color = ThemeManager.GetPredictionColor("normal");
				}
			}
			else if (ToNRoundTypeHelper.IsOverrideRound(currentRoundType))
			{
				// 特殊確定時（N>=2）にOverrideが出たら特殊枠消費 → 次は通常確定
				int normalCountAtStart = instanceState.NormalRoundCountAtRoundStart;
				if (normalCountAtStart >= 2)
				{
					prediction = "通常";
					color = ThemeManager.GetPredictionColor("normal");
				}
				else
				{
					prediction = "通常 or 特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
			}
			else
			{
				// 通常ラウンドの場合、カウントを考慮
				int normalCountAtStart = instanceState.NormalRoundCountAtRoundStart;
				int effectiveNormalCountAtStart = normalCountAtStart;

				// WasOverrideInUncertainState=trueの場合、前のOverrideが特殊枠を消費したことが確定
				// つまりこのNormalは実質N=0からの遷移として計算すべき
				if (instanceState.WasOverrideInUncertainState)
				{
					effectiveNormalCountAtStart = 0;
				}

				int normalCount = effectiveNormalCountAtStart + 1;
				if (normalCount >= 2)
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
			SafeInvoke(() =>
			{
				// ラウンドがアクティブな場合は、現在のラウンドを考慮した予測を使用
				if (isRoundActive && instanceState != null && instanceState.HasCurrentRound)
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

				// マスター変更時は特殊確定
				if (instanceState.MasterChanged && instanceState.SpecialUnlocked)
				{
					prediction = "特殊(MC)";
					color = ThemeManager.GetPredictionColor("special");
					labelNextRound.Text = $"➡️ 次: {prediction}";
					labelNextRound.ForeColor = color;
					return;
				}

				// Moon解禁直後フラグを最優先でチェック（メインフォームと同じ優先度）
				// 優先度: Twilight > Mystic Moon > Blood Moon
				if (instanceState.TwilightJustUnlocked)
				{
					prediction = "Twilight(解禁直後)";
					color = ThemeManager.GetPredictionColor("twilight");
				}
				else if (instanceState.MysticMoonJustUnlocked)
				{
					prediction = "Mystic Moon(解禁直後)";
					color = ThemeManager.GetPredictionColor("mystic");
				}
				else if (instanceState.BloodMoonJustUnlocked)
				{
					prediction = "Blood Moon(解禁直後)";
					color = ThemeManager.GetPredictionColor("blood");
				}
				// Moon解禁チェック（優先順位: Twilight > Mystic > Blood）
				else if (instanceState.AllBirdsMet && !instanceState.TwilightUnlocked)
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
					ToNRoundType lastRound = instanceState.LastRoundType;
					
					if (ToNRoundTypeHelper.IsSpecialRound(lastRound))
					{
						prediction = "通常";
						color = ThemeManager.GetPredictionColor("normal");
					}
					// Moonラウンド終了後の予測
					else if (ToNRoundTypeHelper.IsMoonRound(lastRound))
					{
						if (instanceState.IsCurrentRoundFirstMoon)
						{
							// 初回MoonはOverride系と同じ動作
							prediction = "通常 or 特殊";
							color = ThemeManager.GetPredictionColor("special");
						}
						else
						{
							// 2回目以降Moonは特殊枚消費 → 次は通常
							prediction = "通常";
							color = ThemeManager.GetPredictionColor("normal");
						}
					}
					else if (ToNRoundTypeHelper.IsOverrideRound(lastRound))
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
						// N=0（特殊直後など）は通常確定
						prediction = "通常";
						color = ThemeManager.GetPredictionColor("normal");
					}
				}

				labelNextRound.Text = $"➡️ 次: {prediction}";
				labelNextRound.ForeColor = color;
			});
		}

		/// <summary>
		/// ラウンドタイプに応じた色を取得
		/// </summary>
		private Color GetRoundTypeColor(ToNRoundType roundType)
		{
			bool isDark = ThemeManager.IsDark;

			switch (roundType)
			{
				case ToNRoundType.Classic:
				case ToNRoundType.RUN:
				case ToNRoundType.Alternate:
				case ToNRoundType.Eight_Pages:
					return isDark ? Color.White : Color.Black;
					
				case ToNRoundType.Punished:
					return Color.Yellow;
					
				case ToNRoundType.Cracked:
					return Color.Magenta;
					
				case ToNRoundType.Sabotage:
					return Color.Green;
					
				case ToNRoundType.Fog:
				case ToNRoundType.Fog_Alternate:
					return Color.Gray;
					
				case ToNRoundType.Bloodbath:
				case ToNRoundType.Double_Trouble:
				case ToNRoundType.EX:
					return Color.Red;
					
				case ToNRoundType.Midnight:
				case ToNRoundType.Blood_Moon:
					return Color.DarkRed;
					
				case ToNRoundType.Ghost:
				case ToNRoundType.Ghost_Alternate:
					return Color.DeepSkyBlue;
					
				case ToNRoundType.Unbound:
					return Color.Orange;
					
				case ToNRoundType.Mystic_Moon:
					return isDark ? Color.Cyan : Color.Teal;
					
				case ToNRoundType.Twilight:
					return Color.Gold;
					
				case ToNRoundType.Solstice:
					return Color.FromArgb(0, 255, 136);
					
				case ToNRoundType.GIGABYTE:
					return Color.Lime;
					
				case ToNRoundType.Cold_Night:
					return Color.LightBlue;
					
				default:
					return isDark ? Color.Cyan : Color.Teal;
			}
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

			// リサイズグリップ（背景は下部パネル、線色だけテーマ追従なので再描画のみ）
			if (resizeGrip != null)
			{
				resizeGrip.Invalidate();
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
			reminderTimer?.Stop();
			reminderTimer?.Dispose();
			base.OnFormClosing(e);
		}
	}
}
