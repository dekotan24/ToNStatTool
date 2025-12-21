using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ToNStatTool
{
	/// <summary>
	/// ToN Stat Tool のメインフォーム
	/// </summary>
	public partial class ToNStatTool : Form
	{
		private WebSocketClient webSocketClient;
		private TerrorDisplayForm terrorDisplayForm;

		// UI Controls
		private TextBox textBoxUrl;
		private Button buttonConnect;
		private Label labelStatus;
		private GroupBox groupBoxTerrors;
		private GroupBox groupBoxRoundInfo;
		private GroupBox groupBoxPlayerList;
		private GroupBox groupBoxStats;
		private TabControl tabControlStats;
		private TabPage tabPageRounds;
		private TabPage tabPageTerrors;
		private GroupBox groupBoxRoundLog;
		private TextBox textBoxRawData;
		private ListBox listBoxEvents;
		private System.Windows.Forms.Timer uiUpdateTimer;

		// Terror Display Controls
		private FlowLayoutPanel terrorDisplayPanel;
		private readonly List<TerrorControl> terrorControls = new List<TerrorControl>();

		// UI更新制御用
		private DateTime lastUIUpdate = DateTime.MinValue;
		private readonly TimeSpan minUIUpdateInterval = TimeSpan.FromMilliseconds(100);
		private bool isUpdatingEvents = false;
		private bool isUpdatingPlayers = false;
		private DateTime lastSaboteurUpdate = DateTime.MinValue;

		public ToNStatTool()
		{
			InitializeComponent();
			InitializeWebSocketClient();
			InitializeTimer();
		}

		private void InitializeComponent()
		{
			this.SuspendLayout();

			// Form設定
			this.Text = "ToN Stat Tool - Terror of Nowhere Statistics Tool";
			this.Size = new Size(1205, 760);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.FormBorderStyle = FormBorderStyle.Sizable;

			CreateConnectionControls();
			CreateTerrorDisplay();
			CreateGameInfoControls();
			CreatePlayerListControls();
			CreateStatsControls();
			CreateRoundLogControls();
			CreateEventControls();

			this.ResumeLayout(false);
		}

		private void InitializeWebSocketClient()
		{
			webSocketClient = new WebSocketClient();
			webSocketClient.OnConnected += OnWebSocketConnected;
			webSocketClient.OnDisconnected += OnWebSocketDisconnected;
			webSocketClient.OnMessageReceived += OnWebSocketMessageReceived;
			webSocketClient.OnError += OnWebSocketError;
			webSocketClient.OnTerrorUpdate += OnTerrorUpdate;
			webSocketClient.OnRoundEnd += OnRoundEnd;
			webSocketClient.OnWarningUserJoined += OnWarningUserJoined;
		}

		private void InitializeTimer()
		{
			uiUpdateTimer = new System.Windows.Forms.Timer();
			uiUpdateTimer.Interval = 5000; // 5秒間隔（主に古いデータのクリーンアップ用）
			uiUpdateTimer.Tick += UiUpdateTimer_Tick;
			uiUpdateTimer.Start();
		}

		private void CreateConnectionControls()
		{
			// URL入力
			var labelUrl = new Label();
			labelUrl.Text = "WebSocket URL:";
			labelUrl.Location = new Point(10, 15);
			labelUrl.Size = new Size(100, 23);
			this.Controls.Add(labelUrl);

			textBoxUrl = new TextBox();
			textBoxUrl.Location = new Point(120, 12);
			textBoxUrl.Size = new Size(300, 23);
			textBoxUrl.Text = "ws://localhost:11398";
			this.Controls.Add(textBoxUrl);

			// 接続ボタン
			buttonConnect = new Button();
			buttonConnect.Location = new Point(430, 11);
			buttonConnect.Size = new Size(100, 25);
			buttonConnect.Text = "接続";
			buttonConnect.Click += ButtonConnect_Click;
			this.Controls.Add(buttonConnect);

			// ステータス表示
			labelStatus = new Label();
			labelStatus.Location = new Point(540, 15);
			labelStatus.Size = new Size(400, 23);
			labelStatus.Text = "未接続";
			labelStatus.ForeColor = Color.Red;
			this.Controls.Add(labelStatus);

			// テラー表示ウィンドウボタン
			var buttonTerrorWindow = new Button();
			buttonTerrorWindow.Location = new Point(950, 11);
			buttonTerrorWindow.Size = new Size(150, 25);
			buttonTerrorWindow.Text = "テラー表示ウィンドウ";
			buttonTerrorWindow.Click += ButtonTerrorWindow_Click;
			this.Controls.Add(buttonTerrorWindow);
		}

		private void CreateTerrorDisplay()
		{
			// テラー表示グループ
			groupBoxTerrors = new GroupBox();
			groupBoxTerrors.Text = "現在のテラー";
			groupBoxTerrors.Location = new Point(10, 50);
			groupBoxTerrors.Size = new Size(600, 240);//600 180
			this.Controls.Add(groupBoxTerrors);

			terrorDisplayPanel = new FlowLayoutPanel();
			terrorDisplayPanel.Location = new Point(10, 25);
			terrorDisplayPanel.Size = new Size(580, 220);// 580 145
			terrorDisplayPanel.FlowDirection = FlowDirection.LeftToRight;
			terrorDisplayPanel.WrapContents = false;
			terrorDisplayPanel.AutoScroll = false;
			groupBoxTerrors.Controls.Add(terrorDisplayPanel);
		}

		private void CreateGameInfoControls()
		{
			// ラウンド情報グループ
			groupBoxRoundInfo = new GroupBox();
			groupBoxRoundInfo.Text = "ラウンド情報";
			groupBoxRoundInfo.Location = new Point(620, 50);
			groupBoxRoundInfo.Size = new Size(560, 180);
			this.Controls.Add(groupBoxRoundInfo);

			var infoControls = new[]
			{
				new { Label = "ラウンド:", Key = "roundType", Y = 25 },
				new { Label = "マップ:", Key = "location", Y = 50 },
				new { Label = "ラウンド状態:", Key = "roundActive", Y = 75 },
				new { Label = "生存状態:", Key = "alive", Y = 100 },
				new { Label = "サボタージュ:", Key = "saboteur", Y = 125 },
				new { Label = "ページ数:", Key = "pageCount", Y = 150 }
			};

			foreach (var control in infoControls)
			{
				var label = new Label();
				label.Text = control.Label;
				label.Location = new Point(10, control.Y);
				label.Size = new Size(80, 20);
				groupBoxRoundInfo.Controls.Add(label);

				var textBox = new TextBox();
				textBox.Name = $"textBox_{control.Key}";
				textBox.Location = new Point(95, control.Y - 2);
				textBox.Size = new Size(450, 23);
				textBox.ReadOnly = true;
				textBox.Text = "-";
				groupBoxRoundInfo.Controls.Add(textBox);
			}
		}

		private void CreatePlayerListControls()
		{
			// プレイヤーリストグループ
			groupBoxPlayerList = new GroupBox();
			groupBoxPlayerList.Text = "プレイヤー一覧";
			groupBoxPlayerList.Location = new Point(10, 300);
			groupBoxPlayerList.Size = new Size(400, 415);
			this.Controls.Add(groupBoxPlayerList);

			// プレイヤー数表示
			var labelPlayerCount = new Label();
			labelPlayerCount.Name = "labelPlayerCount";
			labelPlayerCount.Location = new Point(10, 25);
			labelPlayerCount.Size = new Size(300, 20); // 幅を狭めてボタン用スペースを確保
			labelPlayerCount.Text = "総人数: 0人 | 生存: 0人";
			labelPlayerCount.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
			labelPlayerCount.TextAlign = ContentAlignment.MiddleLeft; // 左寄せに変更
			groupBoxPlayerList.Controls.Add(labelPlayerCount);

			// 警告対象ユーザー表示ボタン（右上端にアイコンで配置）
			var buttonShowWarningUsers = new Button();
			buttonShowWarningUsers.Name = "buttonShowWarningUsers";
			buttonShowWarningUsers.Location = new Point(325, 20);
			buttonShowWarningUsers.Size = new Size(30, 25);
			buttonShowWarningUsers.Text = "👤";
			buttonShowWarningUsers.Font = new Font("Arial", 12);
			buttonShowWarningUsers.UseVisualStyleBackColor = true;
			buttonShowWarningUsers.Click += ButtonShowWarningUsers_Click;
			groupBoxPlayerList.Controls.Add(buttonShowWarningUsers);

			// 警告ユーザーリスト再読み込みボタン（右上端にアイコンで配置）
			var buttonReloadWarningUsers = new Button();
			buttonReloadWarningUsers.Name = "buttonReloadWarningUsers";
			buttonReloadWarningUsers.Location = new Point(360, 20);
			buttonReloadWarningUsers.Size = new Size(30, 25);
			buttonReloadWarningUsers.Text = "🔄";
			buttonReloadWarningUsers.Font = new Font("Arial", 12);
			buttonReloadWarningUsers.UseVisualStyleBackColor = true;
			buttonReloadWarningUsers.Click += ButtonReloadWarningUsers_Click;
			groupBoxPlayerList.Controls.Add(buttonReloadWarningUsers);

			// リストビュー
			var listViewPlayers = new ListView();
			listViewPlayers.Name = "listViewPlayers";
			listViewPlayers.Location = new Point(10, 50);
			listViewPlayers.Size = new Size(375, 350);
			listViewPlayers.View = View.Details;
			listViewPlayers.FullRowSelect = true;
			listViewPlayers.GridLines = true;

			listViewPlayers.Columns.Add("プレイヤー名", 200);
			listViewPlayers.Columns.Add("状態", 80);
			listViewPlayers.Columns.Add("種別", 80);

			groupBoxPlayerList.Controls.Add(listViewPlayers);

			// ツールチップの設定
			var toolTip = new ToolTip();
			toolTip.SetToolTip(buttonShowWarningUsers, "警告対象ユーザー一覧を表示");
			toolTip.SetToolTip(buttonReloadWarningUsers, "警告対象ユーザーリストを再読み込み");
		}

		private void CreateStatsControls()
		{
			// 統計情報グループ
			groupBoxStats = new GroupBox();
			groupBoxStats.Text = "ラウンド統計";
			groupBoxStats.Location = new Point(420, 300);
			groupBoxStats.Size = new Size(300, 415);
			this.Controls.Add(groupBoxStats);

			tabControlStats = new TabControl();
			tabControlStats.Text = "ラウンド統計";
			tabControlStats.Location = new Point(10, 20);
			tabControlStats.Size = new Size(280, 380);
			groupBoxStats.Controls.Add(tabControlStats);

			tabPageRounds = new TabPage();
			tabPageRounds.Text = "ラウンド";
			tabPageRounds.Location = new Point(0, 0);
			tabPageRounds.Size = new Size(300, 415);
			tabControlStats.Controls.Add(tabPageRounds);

			tabPageTerrors = new TabPage();
			tabPageTerrors.Text = "テラー";
			tabPageTerrors.Location = new Point(0, 0);
			tabPageTerrors.Size = new Size(300, 415);
			tabControlStats.Controls.Add(tabPageTerrors);

			// 総ラウンド数表示ラベル
			var labelTotalRounds = new Label();
			labelTotalRounds.Name = "labelTotalRounds";
			labelTotalRounds.Text = "総ラウンド数: 0";
			labelTotalRounds.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
			labelTotalRounds.Location = new Point(5, 5);
			labelTotalRounds.Size = new Size(260, 20);
			tabPageRounds.Controls.Add(labelTotalRounds);

			// ラウンド統計ListView
			var listViewStats = new ListView();
			listViewStats.Name = "listViewStats";
			listViewStats.Location = new Point(5, 30);
			listViewStats.Size = new Size(260, 320);
			listViewStats.View = View.Details;
			listViewStats.FullRowSelect = true;
			listViewStats.GridLines = true;
			listViewStats.Columns.Add("ラウンド種別", 130);
			listViewStats.Columns.Add("回数", 50);
			listViewStats.Columns.Add("確率(%)", 60);
			tabPageRounds.Controls.Add(listViewStats);

			// テラー統計ListView
			var listViewStatsTerrors = new ListView();
			listViewStatsTerrors.Name = "listViewStatsTerrors";
			listViewStatsTerrors.Dock = DockStyle.Fill;
			listViewStatsTerrors.View = View.Details;
			listViewStatsTerrors.FullRowSelect = true;
			listViewStatsTerrors.GridLines = true;
			listViewStatsTerrors.Columns.Add("テラー名", 170);
			listViewStatsTerrors.Columns.Add("遭遇回数", 70);
			tabPageTerrors.Controls.Add(listViewStatsTerrors);
		}

		private void CreateRoundLogControls()
		{
			// ラウンドロググループ
			groupBoxRoundLog = new GroupBox();
			groupBoxRoundLog.Text = "ラウンドログ";
			groupBoxRoundLog.Location = new Point(730, 300);
			groupBoxRoundLog.Size = new Size(450, 415);
			this.Controls.Add(groupBoxRoundLog);

			var listViewRoundLog = new ListView();
			listViewRoundLog.Name = "listViewRoundLog";
			listViewRoundLog.Location = new Point(10, 25);
			listViewRoundLog.Size = new Size(430, 375);
			listViewRoundLog.View = View.Details;
			listViewRoundLog.FullRowSelect = true;
			listViewRoundLog.GridLines = true;

			listViewRoundLog.Columns.Add("時刻", 45);
			listViewRoundLog.Columns.Add("ラウンド", 90);
			listViewRoundLog.Columns.Add("マップ", 120);
			listViewRoundLog.Columns.Add("テラー", 150);

			groupBoxRoundLog.Controls.Add(listViewRoundLog);
		}

		private void CreateEventControls()
		{
			// イベントリストグループ
			var groupBoxEvents = new GroupBox();
			groupBoxEvents.Text = "最新イベント";
			groupBoxEvents.Location = new Point(10, 725);
			groupBoxEvents.Size = new Size(710, 200);
			this.Controls.Add(groupBoxEvents);

			listBoxEvents = new ListBox();
			listBoxEvents.Name = "listBoxEvents";
			listBoxEvents.Location = new Point(10, 25);
			listBoxEvents.Size = new Size(685, 165);
			listBoxEvents.Font = new Font("Consolas", 8);
			groupBoxEvents.Controls.Add(listBoxEvents);

			// 生データ表示
			var labelRawData = new GroupBox();
			labelRawData.Text = "受信データ (JSON):";
			labelRawData.Location = new Point(730, 725);
			labelRawData.Size = new Size(450, 200);
			this.Controls.Add(labelRawData);

			textBoxRawData = new TextBox();
			textBoxRawData.Location = new Point(10, 25);
			textBoxRawData.Size = new Size(430, 165);
			textBoxRawData.Multiline = true;
			textBoxRawData.ScrollBars = ScrollBars.Both;
			textBoxRawData.ReadOnly = true;
			textBoxRawData.Font = new Font("Consolas", 8);
			labelRawData.Controls.Add(textBoxRawData);
		}

		private void UiUpdateTimer_Tick(object sender, EventArgs e)
		{
			webSocketClient.CleanupOldData();
		}

		private async void ButtonConnect_Click(object sender, EventArgs e)
		{
			if (!webSocketClient.IsConnected)
			{
				string serverUrl = textBoxUrl.Text.Trim();
				if (string.IsNullOrEmpty(serverUrl))
				{
					MessageBox.Show("WebSocket URLを入力してください。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}

				labelStatus.Text = "接続中...";
				labelStatus.ForeColor = Color.Orange;
				buttonConnect.Enabled = false;

				await webSocketClient.ConnectAsync(serverUrl);
			}
			else
			{
				await webSocketClient.DisconnectAsync();
			}
		}

		private void ButtonShowWarningUsers_Click(object sender, EventArgs e)
		{
			try
			{
				var warningUsers = webSocketClient.GetWarningUsers();

				if (warningUsers.Count == 0)
				{
					MessageBox.Show("現在、警告対象ユーザーは登録されていません。", "警告対象ユーザー", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}

				StringBuilder sb = new StringBuilder();
				sb.AppendLine($"現在ロードしている警告対象ユーザー ({warningUsers.Count}人):");
				sb.AppendLine();

				foreach (var user in warningUsers.OrderBy(u => u))
				{
					sb.AppendLine($"• {user}");
				}

				sb.AppendLine();
				sb.AppendLine("※ warn_user.txt ファイルから読み込まれています");

				MessageBox.Show(sb.ToString(), "警告対象ユーザー一覧", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"警告対象ユーザーの表示でエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void ButtonReloadWarningUsers_Click(object sender, EventArgs e)
		{
			try
			{
				webSocketClient.ReloadWarningUsers();
				var warningUsers = webSocketClient.GetWarningUsers();

				MessageBox.Show($"警告対象ユーザーリストを再読み込みしました。\n現在の登録数: {warningUsers.Count}人", "リスト再読み込み", MessageBoxButtons.OK, MessageBoxIcon.Information);

				// プレイヤーリストも更新
				UpdatePlayerList();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"警告対象ユーザーリストの再読み込みでエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void ButtonTerrorWindow_Click(object sender, EventArgs e)
		{
			if (terrorDisplayForm == null || terrorDisplayForm.IsDisposed)
			{
				terrorDisplayForm = new TerrorDisplayForm();
				terrorDisplayForm.UpdateTerrors(webSocketClient.CurrentTerrors);
				terrorDisplayForm.Show();
			}
			else
			{
				terrorDisplayForm.Close();
			}
		}

		private void OnWebSocketConnected(string playerName)
		{
			this.Invoke(new Action(() =>
			{
				labelStatus.Text = $"接続済み - プレイヤー: {playerName}";
				labelStatus.ForeColor = Color.Green;
				buttonConnect.Text = "切断";
				buttonConnect.Enabled = true;
			}));
		}

		private void OnWebSocketDisconnected()
		{
			this.Invoke(new Action(() =>
			{
				labelStatus.Text = "切断済み";
				labelStatus.ForeColor = Color.Red;
				buttonConnect.Text = "接続";
				buttonConnect.Enabled = true;
			}));
		}

		private void OnWebSocketMessageReceived(string message)
		{
			this.Invoke(new Action(() =>
			{
				// 生データを表示
				var shortMessage = message.Length > 500 ? message.Substring(0, 500) + "..." : message;
				textBoxRawData.Text = FormatJson(shortMessage);
				textBoxRawData.SelectionStart = 0;
				textBoxRawData.ScrollToCaret();

				// UI更新をスケジュール
				ScheduleUIUpdate();
			}));
		}

		private void OnWebSocketError(string error)
		{
			this.Invoke(new Action(() =>
			{
				MessageBox.Show($"エラー: {error}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
				labelStatus.Text = "接続失敗";
				labelStatus.ForeColor = Color.Red;
				buttonConnect.Text = "接続";
				buttonConnect.Enabled = true;
			}));
		}

		private void OnTerrorUpdate()
		{
			this.Invoke(new Action(() =>
			{
				UpdateTerrorDisplay();
			}));
		}

		private void OnRoundEnd()
		{
			this.Invoke(new Action(() =>
			{
				UpdateStatsDisplay();
				UpdateRoundLogDisplay();
			}));
		}

		private void ScheduleUIUpdate()
		{
			// 最小更新間隔をチェック
			if (DateTime.Now - lastUIUpdate < minUIUpdateInterval)
			{
				return;
			}

			UpdateUI();
			lastUIUpdate = DateTime.Now;
		}

		private void UpdateUI()
		{
			try
			{
				UpdateGameDataDisplay();
				UpdatePlayerList();
				UpdateEventList();
				UpdateStatsDisplay();
				UpdateRoundLogDisplay();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"UI更新エラー: {ex.Message}");
			}
		}

		private void UpdateGameDataDisplay()
		{
			UpdateGameInfo();
		}

		private void OnWarningUserJoined(string userName)
		{
			try
			{
				string warningMessage = $"⚠️ 注意: {userName} が参加しました";

				string originalTitle = this.Text;
				this.Text = $"【警告】{warningMessage} - {originalTitle}";

				var timer = new System.Windows.Forms.Timer();
				timer.Interval = 5000;
				timer.Tick += (s, e) =>
				{
					this.Text = originalTitle;
					timer.Stop();
					timer.Dispose();
				};
				timer.Start();

				System.Diagnostics.Debug.WriteLine($"[WARNING_UI] {warningMessage}");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING_UI] エラー: {ex.Message}");
			}
		}

		private void UpdateGameInfo()
		{
			var gameData = webSocketClient.GameData;

			UpdateTextBoxWithColor("roundType", GetGameDataValue(gameData, "roundType", "-"));
			UpdateTextBox("location", GetGameDataValue(gameData, "location", "-"));
			UpdateTextBox("roundActive", GetGameDataValue(gameData, "roundActive", "-"));
			UpdateTextBox("alive", GetGameDataValue(gameData, "alive", "-"));

			// サボタージュ状態の更新を制限
			if (DateTime.Now - lastSaboteurUpdate > TimeSpan.FromSeconds(2))
			{
				UpdateTextBox("saboteur", GetGameDataValue(gameData, "saboteur", "-"));
				lastSaboteurUpdate = DateTime.Now;
			}

			UpdateTextBox("pageCount", GetGameDataValue(gameData, "pageCount", "-"));
		}

		private void UpdateTerrorDisplay()
		{
			var currentTerrors = webSocketClient.CurrentTerrors;

			if (currentTerrors.Count != terrorControls.Count)
			{
				// テラー数が変更された場合、コントロールを再生成
				foreach (var control in terrorControls)
				{
					control.Dispose();
				}
				terrorControls.Clear();
				terrorDisplayPanel.Controls.Clear();

				// 新しいテラーコントロールを作成
				foreach (var terror in currentTerrors)
				{
					var terrorControl = new TerrorControl(terror);
					terrorControls.Add(terrorControl);
					terrorDisplayPanel.Controls.Add(terrorControl);
				}

				// レイアウトを調整
				AdjustTerrorLayout();

				// サブフォームにも更新を反映
				if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
				{
					terrorDisplayForm.UpdateTerrors(currentTerrors);
				}
			}
		}

		private void AdjustTerrorLayout()
		{
			if (terrorControls.Count == 0) return;

			int totalWidth = terrorControls.Count * 185;
			int panelWidth = terrorDisplayPanel.Width;
			int startX = Math.Max(0, (panelWidth - totalWidth) / 2);

			for (int i = 0; i < terrorControls.Count; i++)
			{
				terrorControls[i].Location = new Point(startX + i * 185, 5);
			}
		}

		private void UpdatePlayerList()
		{
			if (isUpdatingPlayers) return;
			isUpdatingPlayers = true;

			try
			{
				var listView = FindControl("listViewPlayers") as ListView;
				var labelPlayerCount = FindControl("labelPlayerCount") as Label;
				if (listView == null || labelPlayerCount == null) return;

				var players = webSocketClient.Players;
				var localPlayerUserId = webSocketClient.LocalPlayerUserId;

				// 現在の選択を保存
				var selectedIndices = new List<int>();
				foreach (int index in listView.SelectedIndices)
				{
					selectedIndices.Add(index);
				}

				listView.BeginUpdate();
				listView.Items.Clear();

				int totalPlayers = players.Count;
				int alivePlayers = 0;
				int warningPlayers = 0; // 警告対象プレイヤー数

				System.Diagnostics.Debug.WriteLine($"[UI] プレイヤー一覧更新 - 総数: {totalPlayers}");

				foreach (var player in players.Values.OrderBy(p => p.Name))
				{
					try
					{
						// プレイヤー名の表示用処理
						string displayName = GetDisplayPlayerName(player.Name);
						bool isWarningUser = webSocketClient.IsWarningUser(player.Name);

						var item = new ListViewItem(displayName);
						item.SubItems.Add(player.IsAlive ? "生存" : "死亡");

						// 種別列に警告マークを追加
						string playerType = player.UserId == localPlayerUserId ? "自分" : "他人";
						if (isWarningUser)
						{
							playerType = "⚠️注意";
							warningPlayers++;
						}
						item.SubItems.Add(playerType);

						// ツールチップに元の名前を設定（表示名が切り詰められた場合）
						if (displayName != player.Name)
						{
							item.ToolTipText = $"元の名前: {player.Name}";
						}
						if (isWarningUser && string.IsNullOrEmpty(item.ToolTipText))
						{
							item.ToolTipText = "警告対象ユーザーです";
						}

						if (player.IsAlive)
							alivePlayers++;

						// 色分け（優先順位: 警告 > 死亡 > 自分）
						if (isWarningUser)
						{
							item.ForeColor = Color.DarkOrange; // 警告ユーザーはオレンジ色
							item.Font = new Font(listView.Font, FontStyle.Bold); // 太字で強調
						}
						else if (!player.IsAlive)
						{
							item.ForeColor = Color.Red; // 死亡は赤
						}
						else if (player.UserId == localPlayerUserId)
						{
							item.ForeColor = Color.Blue; // 自分は青
						}

						listView.Items.Add(item);

						string warningFlag = isWarningUser ? " [警告]" : "";
						System.Diagnostics.Debug.WriteLine($"[UI] プレイヤー追加: '{displayName}'{warningFlag} - {(player.IsAlive ? "生存" : "死亡")}");
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"[UI] プレイヤー表示エラー: {player.Name} - {ex.Message}");

						// エラーが発生した場合でもリストに追加
						var errorItem = new ListViewItem($"[表示エラー] {player.UserId}");
						errorItem.SubItems.Add(player.IsAlive ? "生存" : "死亡");
						errorItem.SubItems.Add(player.UserId == localPlayerUserId ? "自分" : "他人");
						errorItem.ForeColor = Color.Orange;
						listView.Items.Add(errorItem);
					}
				}

				// プレイヤー数表示を更新（警告ユーザー数も表示）
				string countText = $"総人数: {totalPlayers}人 | 生存: {alivePlayers}人";
				if (warningPlayers > 0)
				{
					countText += $" | ⚠️警告: {warningPlayers}人";
					labelPlayerCount.ForeColor = Color.DarkOrange; // 警告がある場合は色を変える
				}
				else
				{
					labelPlayerCount.ForeColor = SystemColors.ControlText; // 通常の色に戻す
				}
				labelPlayerCount.Text = countText;

				// 選択状態を復元
				foreach (int index in selectedIndices)
				{
					if (index < listView.Items.Count)
						listView.Items[index].Selected = true;
				}

				listView.EndUpdate();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[UI] プレイヤーリスト更新エラー: {ex.Message}");
			}
			finally
			{
				isUpdatingPlayers = false;
			}
		}

		/// <summary>
		/// プレイヤー名を表示用に調整する
		/// </summary>
		private string GetDisplayPlayerName(string playerName)
		{
			if (string.IsNullOrEmpty(playerName))
				return "Unknown";

			try
			{
				// ListView での表示に適した長さに調整
				if (playerName.Length > 25)
				{
					return playerName.Substring(0, 22) + "...";
				}

				return playerName;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"プレイヤー名表示処理エラー: {ex.Message}");
				return "Unknown";
			}
		}

		private void UpdateEventList()
		{
			if (isUpdatingEvents) return;
			isUpdatingEvents = true;

			try
			{
				var recentEvents = webSocketClient.RecentEvents;

				int topIndex = listBoxEvents.TopIndex;
				bool wasAtBottom = (topIndex + listBoxEvents.ClientSize.Height / listBoxEvents.ItemHeight) >= listBoxEvents.Items.Count - 1;

				listBoxEvents.BeginUpdate();
				listBoxEvents.Items.Clear();

				foreach (var evt in recentEvents.OrderByDescending(e => e.Timestamp).Take(50))
				{
					string timeStr = evt.Timestamp.ToString("HH:mm:ss");
					listBoxEvents.Items.Add($"[{timeStr}] {evt.Type}: {evt.Description}");
				}

				if (wasAtBottom && listBoxEvents.Items.Count > 0)
				{
					listBoxEvents.TopIndex = Math.Max(0, listBoxEvents.Items.Count - 1);
				}
				else if (topIndex < listBoxEvents.Items.Count)
				{
					listBoxEvents.TopIndex = topIndex;
				}

				listBoxEvents.EndUpdate();
			}
			finally
			{
				isUpdatingEvents = false;
			}
		}

		private void UpdateStatsDisplay()
		{
			// ラウンド統計
			var labelTotalRounds = FindControl("labelTotalRounds") as Label;
			var listView = FindControl("listViewStats") as ListView;

			if (labelTotalRounds != null && listView != null)
			{
				var roundStats = webSocketClient.RoundStats;

				// 総ラウンド数表示
				labelTotalRounds.Text = $"総ラウンド数: {roundStats.TotalRounds}";

				// ListView更新
				listView.BeginUpdate();
				listView.Items.Clear();

				if (roundStats.RoundTypeCounts.Count > 0)
				{
					foreach (var kvp in roundStats.RoundTypeCounts.OrderByDescending(x => x.Value))
					{
						double percentage = (double)kvp.Value / roundStats.TotalRounds * 100;
						var item = new ListViewItem(kvp.Key);
						item.SubItems.Add(kvp.Value.ToString());
						item.SubItems.Add(percentage.ToString("F1"));
						listView.Items.Add(item);
					}
				}

				listView.EndUpdate();
			}

			// テラー統計
			var listView2 = FindControl("listViewStatsTerrors") as ListView;
			if (listView2 != null)
			{
				var terrorStats = webSocketClient.TerrorStats;

				listView2.BeginUpdate();
				listView2.Items.Clear();

				if (terrorStats.TerrorTypeCounts.Count > 0)
				{
					foreach (var kvp in terrorStats.TerrorTypeCounts.OrderByDescending(x => x.Value))
					{
						var item = new ListViewItem(kvp.Key);
						item.SubItems.Add(kvp.Value.ToString());
						listView2.Items.Add(item);
					}
				}

				listView2.EndUpdate();
			}
		}

		private void UpdateRoundLogDisplay()
		{
			var listView = FindControl("listViewRoundLog") as ListView;
			if (listView == null) return;

			var roundLogs = webSocketClient.RoundLogs;

			listView.BeginUpdate();
			listView.Items.Clear();

			foreach (var log in roundLogs.OrderByDescending(l => l.Timestamp).Take(1000))
			{
				var item = new ListViewItem(log.Timestamp.ToString("HH:mm"));
				item.SubItems.Add(log.RoundType);
				item.SubItems.Add(log.MapName);
				item.SubItems.Add(log.TerrorNames);

				if (log.Survived)
					item.ForeColor = Color.Green;
				else
					item.ForeColor = Color.Red;

				listView.Items.Add(item);
			}

			listView.EndUpdate();
		}

		private string GetGameDataValue(Dictionary<string, object> gameData, string key, string defaultValue)
		{
			if (gameData.ContainsKey(key))
			{
				return gameData[key]?.ToString() ?? defaultValue;
			}
			return defaultValue;
		}

		private void UpdateTextBoxWithColor(string key, string value)
		{
			var textBox = FindControl($"textBox_{key}") as TextBox;
			if (textBox != null && textBox.Text != value)
			{
				textBox.Text = value;

				if (key == "roundType")
				{
					if (value.Contains("Classic"))
						textBox.BackColor = Color.LightBlue;
					else if (value.Contains("Alternate"))
						textBox.BackColor = Color.LightGreen;
					else if (value.Contains("Sabotage"))
						textBox.BackColor = Color.LightCoral;
					else if (value.Contains("Blood"))
						textBox.BackColor = Color.LightPink;
					else if (value.Contains("Midnight"))
						textBox.BackColor = Color.DarkSlateBlue;
					else if (value.Contains("Cracked"))
						textBox.BackColor = Color.LightYellow;
					else if (value.Contains("Mystic"))
						textBox.BackColor = Color.Lavender;
					else
						textBox.BackColor = SystemColors.Window;
				}
			}
		}

		private void UpdateTextBox(string key, string value)
		{
			var textBox = FindControl($"textBox_{key}") as TextBox;
			if (textBox != null && textBox.Text != value)
			{
				textBox.Text = value;
			}
		}

		private Control FindControl(string name)
		{
			return FindControlRecursive(this, name);
		}

		private Control FindControlRecursive(Control parent, string name)
		{
			foreach (Control control in parent.Controls)
			{
				if (control.Name == name)
					return control;
				var found = FindControlRecursive(control, name);
				if (found != null)
					return found;
			}
			return null;
		}

		private string FormatJson(string json)
		{
			try
			{
				var jsonObject = JObject.Parse(json);
				return jsonObject.ToString(Formatting.Indented);
			}
			catch
			{
				return json;
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			webSocketClient?.DisconnectAsync().Wait();

			uiUpdateTimer?.Stop();
			uiUpdateTimer?.Dispose();

			// テラーコントロールのリソースを解放
			foreach (var control in terrorControls)
			{
				control.Dispose();
			}

			// TerrorImageManagerのキャッシュをクリア
			TerrorImageManager.ClearCache();

			if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
			{
				terrorDisplayForm.Close();
			}

			base.OnFormClosing(e);
		}
	}
}