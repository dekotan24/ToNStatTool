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
		/// アイテムリマインダーを表示（8ページ/アンバウンド終了時）
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
		/// <param name="terrors">テラー情報リスト</param>
		/// <param name="unboundName">Unboundのアナウンス名（Unboundラウンド時のみ）</param>
		public void UpdateTerrors(List<TerrorInfo> terrors, string unboundName = null)
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
						var control = new CompactTerrorControl(terror);
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
			if (instanceState.MasterChanged)
			{
				labelNextRound.Text = "➡️ 次: 特殊(MC)";
				labelNextRound.ForeColor = ThemeManager.GetPredictionColor("special");
				return;
			}

			string prediction = "";
			Color color = ThemeManager.IsDark ? ThemeManager.Dark.TerrorNextRound : ThemeManager.Light.TerrorNextRound;

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
				if (instanceState.MasterChanged)
				{
					prediction = "特殊(MC)";
					color = ThemeManager.GetPredictionColor("special");
					labelNextRound.Text = $"➡️ 次: {prediction}";
					labelNextRound.ForeColor = color;
					return;
				}

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
						prediction = "通常 or 特殊";
						color = ThemeManager.GetPredictionColor("special");
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
