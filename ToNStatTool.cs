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
using ToNStatTool.Controls;

namespace ToNStatTool
{
	/// <summary>
	/// ToN Stat Tool のメインフォーム
	/// </summary>
	public partial class ToNStatTool : Form
	{
		private WebSocketClient webSocketClient;
		private TerrorDisplayForm terrorDisplayForm;
		private System.Windows.Forms.Timer elapsedTimeTimer;
		private DateTime mainFormRoundStartTime;
		private bool mainFormRoundActive = false;

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

		// アプリケーション設定
		private AppSettings appSettings;

		public ToNStatTool()
		{
			// 設定を読み込み
			appSettings = AppSettings.Load();
			
			InitializeComponent();
			InitializeWebSocketClient();
			InitializeTimer();
			
			// 保存されたテーマを適用
			ThemeManager.SetTheme(appSettings.GetAppTheme());
			ThemeManager.ThemeChanged += OnThemeChanged;
			ThemeManager.Apply(this);
			
			// 保存されたURLを復元
			if (!string.IsNullOrEmpty(appSettings.WebSocketUrl))
			{
				textBoxUrl.Text = appSettings.WebSocketUrl;
			}
			
			// 保存された透明度を復元
			var trackBar = FindControl("trackBarOpacity") as TrackBar;
			if (trackBar != null)
			{
				trackBar.Value = Math.Max(trackBar.Minimum, Math.Min(trackBar.Maximum, appSettings.TerrorFormOpacity));
			}
			
			// フォームクローズ時に設定を保存
			this.FormClosing += ToNStatTool_FormClosing;
		}

		private void InitializeComponent()
		{
			this.SuspendLayout();

			// Form設定
			this.Text = "ToN Stat Tool - Terror of Nowhere Statistics Tool";
			this.Size = new Size(1205, 760);
			this.StartPosition = FormStartPosition.CenterScreen;
			this.FormBorderStyle = FormBorderStyle.Sizable;
			this.Icon = Properties.Resources.AppIcon;

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
			webSocketClient.OnRoundStart += OnRoundStart;
			webSocketClient.OnWarningUserJoined += OnWarningUserJoined;
			webSocketClient.OnInstanceStateChanged += OnInstanceStateChanged;
			webSocketClient.OnPlayerCountChanged += OnPlayerCountChanged;
		}

		private void InitializeTimer()
		{
			uiUpdateTimer = new System.Windows.Forms.Timer();
			uiUpdateTimer.Interval = 5000; // 5秒間隔（主に古いデータのクリーンアップ用）
			uiUpdateTimer.Tick += UiUpdateTimer_Tick;
			uiUpdateTimer.Start();

			// 経過時間更新タイマー（1秒間隔）
			elapsedTimeTimer = new System.Windows.Forms.Timer();
			elapsedTimeTimer.Interval = 1000;
			elapsedTimeTimer.Tick += ElapsedTimeTimer_Tick;
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
			labelStatus.Size = new Size(200, 23);
			labelStatus.Text = "未接続";
			labelStatus.ForeColor = Color.Red;
			this.Controls.Add(labelStatus);

			// ログフォルダを開くボタン
			var buttonOpenLog = new Button();
			buttonOpenLog.Name = "buttonOpenLog";
			buttonOpenLog.Location = new Point(750, 11);
			buttonOpenLog.Size = new Size(55, 25);
			buttonOpenLog.Text = "ログ";
			buttonOpenLog.Click += (s, e) => Logger.OpenLogFolder();
			this.Controls.Add(buttonOpenLog);

			// 詳細ログトグルボタン
			var buttonVerboseLog = new CheckBox();
			buttonVerboseLog.Name = "buttonVerboseLog";
			buttonVerboseLog.Location = new Point(810, 11);
			buttonVerboseLog.Size = new Size(60, 25);
			buttonVerboseLog.Text = "詳細ログ";
			buttonVerboseLog.Appearance = Appearance.Button;
			buttonVerboseLog.TextAlign = ContentAlignment.MiddleCenter;
			buttonVerboseLog.CheckedChanged += (s, e) =>
			{
				if (buttonVerboseLog.Checked)
				{
					Logger.EnableVerboseLogging();
				}
				else
				{
					Logger.DisableVerboseLogging();
				}
			};
			this.Controls.Add(buttonVerboseLog);

			// テラー表示ウィンドウボタン（チェックボックススタイル）
			var buttonTerrorWindow = new CheckBox();
			buttonTerrorWindow.Name = "buttonTerrorWindow";
			buttonTerrorWindow.Location = new Point(880, 11);
			buttonTerrorWindow.Size = new Size(130, 25);
			buttonTerrorWindow.Text = "テラー表示ウィンドウ";
			buttonTerrorWindow.Appearance = Appearance.Button;
			buttonTerrorWindow.TextAlign = ContentAlignment.MiddleCenter;
			buttonTerrorWindow.CheckedChanged += ButtonTerrorWindow_CheckedChanged;
			this.Controls.Add(buttonTerrorWindow);

			// 透明度ラベル
			var labelOpacity = new Label();
			labelOpacity.Text = "透明度:";
			labelOpacity.Location = new Point(1025, 15);
			labelOpacity.Size = new Size(50, 20);
			labelOpacity.Font = new Font("Meiryo UI", 9);
			this.Controls.Add(labelOpacity);

			// 透明度スライダー（ラウンド情報グループ右端に合わせる）
			var trackBarOpacity = new TrackBar();
			trackBarOpacity.Name = "trackBarOpacity";
			trackBarOpacity.Location = new Point(1070, 8);
			trackBarOpacity.Size = new Size(80, 30);
			trackBarOpacity.Minimum = 10;
			trackBarOpacity.Maximum = 100;
			trackBarOpacity.Value = 100;
			trackBarOpacity.TickFrequency = 10;
			trackBarOpacity.SmallChange = 5;
			trackBarOpacity.LargeChange = 10;
			trackBarOpacity.ValueChanged += (s, e) =>
			{
				if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
				{
					terrorDisplayForm.SetOpacity(trackBarOpacity.Value / 100.0);
				}
			};
			this.Controls.Add(trackBarOpacity);

			// テーマ切替ボタン（オーナー描画）
			var btnThemeToggle = new Button();
			btnThemeToggle.Name = "btnThemeToggle";
			btnThemeToggle.Location = new Point(1152, 10);
			btnThemeToggle.Size = new Size(26, 26);
			btnThemeToggle.FlatStyle = FlatStyle.Flat;
			btnThemeToggle.FlatAppearance.BorderSize = 1;
			btnThemeToggle.Click += BtnThemeToggle_Click;
			btnThemeToggle.Paint += BtnThemeToggle_Paint;
			this.Controls.Add(btnThemeToggle);
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
			groupBoxRoundInfo.Size = new Size(560, 130);
			this.Controls.Add(groupBoxRoundInfo);

			// 1行目: ラウンド（7割） + 経過時間（3割）
			var labelRound = new Label();
			labelRound.Text = "ラウンド:";
			labelRound.Location = new Point(10, 22);
			labelRound.Size = new Size(55, 20);
			groupBoxRoundInfo.Controls.Add(labelRound);

			var textBoxRound = new TextBox();
			textBoxRound.Name = "textBox_roundType";
			textBoxRound.Location = new Point(65, 20);
			textBoxRound.Size = new Size(310, 23);
			textBoxRound.ReadOnly = true;
			textBoxRound.Text = "-";
			groupBoxRoundInfo.Controls.Add(textBoxRound);

			var labelElapsedTime = new Label();
			labelElapsedTime.Text = "経過:";
			labelElapsedTime.Location = new Point(385, 22);
			labelElapsedTime.Size = new Size(35, 20);
			groupBoxRoundInfo.Controls.Add(labelElapsedTime);

			var textBoxElapsedTime = new TextBox();
			textBoxElapsedTime.Name = "textBox_elapsedTime";
			textBoxElapsedTime.Location = new Point(420, 20);
			textBoxElapsedTime.Size = new Size(125, 23);
			textBoxElapsedTime.ReadOnly = true;
			textBoxElapsedTime.Text = "-";
			groupBoxRoundInfo.Controls.Add(textBoxElapsedTime);

			// 2行目: マップ（全幅）
			var labelMap = new Label();
			labelMap.Text = "マップ:";
			labelMap.Location = new Point(10, 47);
			labelMap.Size = new Size(55, 20);
			groupBoxRoundInfo.Controls.Add(labelMap);

			var textBoxMap = new TextBox();
			textBoxMap.Name = "textBox_location";
			textBoxMap.Location = new Point(65, 45);
			textBoxMap.Size = new Size(480, 23);
			textBoxMap.ReadOnly = true;
			textBoxMap.Text = "-";
			groupBoxRoundInfo.Controls.Add(textBoxMap);

			// 3行目: 状態 | 生存 | サボ | ページ（4分割）
			var labelRoundActive = new Label();
			labelRoundActive.Text = "状態:";
			labelRoundActive.Location = new Point(10, 72);
			labelRoundActive.Size = new Size(55, 20);
			groupBoxRoundInfo.Controls.Add(labelRoundActive);

			var textBoxRoundActive = new TextBox();
			textBoxRoundActive.Name = "textBox_roundActive";
			textBoxRoundActive.Location = new Point(65, 70);
			textBoxRoundActive.Size = new Size(80, 23);
			textBoxRoundActive.ReadOnly = true;
			textBoxRoundActive.Text = "-";
			groupBoxRoundInfo.Controls.Add(textBoxRoundActive);

			var labelAlive = new Label();
			labelAlive.Text = "生存:";
			labelAlive.Location = new Point(155, 72);
			labelAlive.Size = new Size(35, 20);
			groupBoxRoundInfo.Controls.Add(labelAlive);

			var textBoxAlive = new TextBox();
			textBoxAlive.Name = "textBox_alive";
			textBoxAlive.Location = new Point(190, 70);
			textBoxAlive.Size = new Size(80, 23);
			textBoxAlive.ReadOnly = true;
			textBoxAlive.Text = "-";
			groupBoxRoundInfo.Controls.Add(textBoxAlive);

			var labelSaboteur = new Label();
			labelSaboteur.Text = "サボ:";
			labelSaboteur.Location = new Point(280, 72);
			labelSaboteur.Size = new Size(35, 20);
			groupBoxRoundInfo.Controls.Add(labelSaboteur);

			var textBoxSaboteur = new TextBox();
			textBoxSaboteur.Name = "textBox_saboteur";
			textBoxSaboteur.Location = new Point(315, 70);
			textBoxSaboteur.Size = new Size(65, 23);
			textBoxSaboteur.ReadOnly = true;
			textBoxSaboteur.Text = "-";
			groupBoxRoundInfo.Controls.Add(textBoxSaboteur);

			var labelPageCount = new Label();
			labelPageCount.Text = "ページ:";
			labelPageCount.Location = new Point(390, 72);
			labelPageCount.Size = new Size(45, 20);
			groupBoxRoundInfo.Controls.Add(labelPageCount);

			var textBoxPageCount = new TextBox();
			textBoxPageCount.Name = "textBox_pageCount";
			textBoxPageCount.Location = new Point(435, 70);
			textBoxPageCount.Size = new Size(110, 23);
			textBoxPageCount.ReadOnly = true;
			textBoxPageCount.Text = "-";
			groupBoxRoundInfo.Controls.Add(textBoxPageCount);

			// 4行目: 次ラウンド予測（全幅）
			var labelNextRound = new Label();
			labelNextRound.Text = "次ラウンド:";
			labelNextRound.Location = new Point(10, 97);
			labelNextRound.Size = new Size(55, 20);
			groupBoxRoundInfo.Controls.Add(labelNextRound);

			var textBoxNextRound = new TextBox();
			textBoxNextRound.Name = "textBox_nextRound";
			textBoxNextRound.Location = new Point(65, 95);
			textBoxNextRound.Size = new Size(480, 23);
			textBoxNextRound.ReadOnly = true;
			textBoxNextRound.Text = "-";
			groupBoxRoundInfo.Controls.Add(textBoxNextRound);

			// インスタンス状態設定グループ（鳥/Moon設定）
			var groupBoxInstanceState = new GroupBox();
			groupBoxInstanceState.Text = "インスタンス状態設定";
			groupBoxInstanceState.Location = new Point(620, 185);
			groupBoxInstanceState.Size = new Size(560, 105);
			this.Controls.Add(groupBoxInstanceState);

			// 鳥遭遇チェックボックス
			var labelBirds = new Label();
			labelBirds.Text = "鳥遭遇:";
			labelBirds.Location = new Point(10, 22);
			labelBirds.Size = new Size(50, 20);
			groupBoxInstanceState.Controls.Add(labelBirds);

			var checkBigBird = new CheckBox();
			checkBigBird.Name = "checkBigBird";
			checkBigBird.Text = "Big Bird";
			checkBigBird.Location = new Point(65, 20);
			checkBigBird.Size = new Size(80, 20);
			checkBigBird.CheckedChanged += (s, e) => { if (webSocketClient?.InstanceState != null) webSocketClient.InstanceState.MetBigBird = checkBigBird.Checked; };
			groupBoxInstanceState.Controls.Add(checkBigBird);

			var checkJudgementBird = new CheckBox();
			checkJudgementBird.Name = "checkJudgementBird";
			checkJudgementBird.Text = "Judgement Bird";
			checkJudgementBird.Location = new Point(150, 20);
			checkJudgementBird.Size = new Size(105, 20);
			checkJudgementBird.CheckedChanged += (s, e) => { if (webSocketClient?.InstanceState != null) webSocketClient.InstanceState.MetJudgementBird = checkJudgementBird.Checked; };
			groupBoxInstanceState.Controls.Add(checkJudgementBird);

			var checkPunishingBird = new CheckBox();
			checkPunishingBird.Name = "checkPunishingBird";
			checkPunishingBird.Text = "Punishing Bird";
			checkPunishingBird.Location = new Point(260, 20);
			checkPunishingBird.Size = new Size(105, 20);
			checkPunishingBird.CheckedChanged += (s, e) => { if (webSocketClient?.InstanceState != null) webSocketClient.InstanceState.MetPunishingBird = checkPunishingBird.Checked; };
			groupBoxInstanceState.Controls.Add(checkPunishingBird);

			// Moon解禁チェックボックス
			var labelMoon = new Label();
			labelMoon.Text = "Moon:";
			labelMoon.Location = new Point(10, 47);
			labelMoon.Size = new Size(50, 20);
			groupBoxInstanceState.Controls.Add(labelMoon);

			var checkBloodMoon = new CheckBox();
			checkBloodMoon.Name = "checkBloodMoon";
			checkBloodMoon.Text = "Blood Moon";
			checkBloodMoon.Location = new Point(65, 45);
			checkBloodMoon.Size = new Size(90, 20);
			checkBloodMoon.ForeColor = Color.DarkRed;
			checkBloodMoon.CheckedChanged += (s, e) => { if (webSocketClient?.InstanceState != null) webSocketClient.InstanceState.BloodMoonUnlocked = checkBloodMoon.Checked; };
			groupBoxInstanceState.Controls.Add(checkBloodMoon);

			var checkTwilight = new CheckBox();
			checkTwilight.Name = "checkTwilight";
			checkTwilight.Text = "Twilight";
			checkTwilight.Location = new Point(160, 45);
			checkTwilight.Size = new Size(70, 20);
			checkTwilight.ForeColor = Color.Goldenrod;
			checkTwilight.CheckedChanged += (s, e) => {
				if (webSocketClient?.InstanceState != null)
					webSocketClient.InstanceState.TwilightUnlocked = checkTwilight.Checked;
				
				// Twilightがチェックされたら鳥も全部チェック
				if (checkTwilight.Checked)
				{
					var chkBigBird = FindControl("checkBigBird") as CheckBox;
					var chkJudgementBird = FindControl("checkJudgementBird") as CheckBox;
					var chkPunishingBird = FindControl("checkPunishingBird") as CheckBox;
					
					if (chkBigBird != null) chkBigBird.Checked = true;
					if (chkJudgementBird != null) chkJudgementBird.Checked = true;
					if (chkPunishingBird != null) chkPunishingBird.Checked = true;
				}
			};
			groupBoxInstanceState.Controls.Add(checkTwilight);

			var checkMysticMoon = new CheckBox();
			checkMysticMoon.Name = "checkMysticMoon";
			checkMysticMoon.Text = "Mystic Moon";
			checkMysticMoon.Location = new Point(235, 45);
			checkMysticMoon.Size = new Size(95, 20);
			checkMysticMoon.ForeColor = Color.DarkCyan;
			checkMysticMoon.CheckedChanged += (s, e) => { if (webSocketClient?.InstanceState != null) webSocketClient.InstanceState.MysticMoonUnlocked = checkMysticMoon.Checked; };
			groupBoxInstanceState.Controls.Add(checkMysticMoon);

			var checkSolstice = new CheckBox();
			checkSolstice.Name = "checkSolstice";
			checkSolstice.Text = "Solstice";
			checkSolstice.Location = new Point(335, 45);
			checkSolstice.Size = new Size(70, 20);
			checkSolstice.ForeColor = Color.Green;
			checkSolstice.CheckedChanged += (s, e) => { if (webSocketClient?.InstanceState != null) webSocketClient.InstanceState.SolsticeUnlocked = checkSolstice.Checked; };
			groupBoxInstanceState.Controls.Add(checkSolstice);

			// 生存回数
			var labelSurvivalCount = new Label();
			labelSurvivalCount.Text = "推定生存数:";
			labelSurvivalCount.Location = new Point(10, 75);
			labelSurvivalCount.Size = new Size(75, 20);
			groupBoxInstanceState.Controls.Add(labelSurvivalCount);

			// 生存回数表示ラベル
			var labelSurvivalValue = new Label();
			labelSurvivalValue.Name = "labelSurvivalValue";
			labelSurvivalValue.Text = "0";
			labelSurvivalValue.Location = new Point(90, 75);
			labelSurvivalValue.Size = new Size(40, 20);
			labelSurvivalValue.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
			labelSurvivalValue.TextAlign = ContentAlignment.MiddleRight;
			groupBoxInstanceState.Controls.Add(labelSurvivalValue);

			// 編集ボタン
			var buttonEditSurvival = new Button();
			buttonEditSurvival.Name = "buttonEditSurvival";
			buttonEditSurvival.Text = "編集";
			buttonEditSurvival.Location = new Point(135, 70);
			buttonEditSurvival.Size = new Size(45, 25);
			buttonEditSurvival.Click += (s, e) => {
				try
				{
					int currentValue = webSocketClient?.InstanceState?.EstimatedSurvivalCount ?? 0;
					string input = ShowInputDialog("推定生存回数", "推定生存回数を入力してください (0-999):", currentValue.ToString());
					if (input != null && int.TryParse(input, out int newValue))
					{
						newValue = Math.Max(0, Math.Min(999, newValue));
						if (webSocketClient?.InstanceState != null)
						{
							webSocketClient.InstanceState.EstimatedSurvivalCount = newValue;
							labelSurvivalValue.Text = newValue.ToString();
						}
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"推定生存回数の編集エラー: {ex.Message}");
				}
			};
			groupBoxInstanceState.Controls.Add(buttonEditSurvival);

			// リセットボタン
			var buttonResetInstanceState = new Button();
			buttonResetInstanceState.Text = "リセット";
			buttonResetInstanceState.Location = new Point(480, 70);
			buttonResetInstanceState.Size = new Size(65, 25);
			buttonResetInstanceState.Click += (s, e) =>
			{
				if (webSocketClient != null) webSocketClient.ResetInstanceState();
				checkBigBird.Checked = false;
				checkJudgementBird.Checked = false;
				checkPunishingBird.Checked = false;
				checkBloodMoon.Checked = false;
				checkTwilight.Checked = false;
				checkMysticMoon.Checked = false;
				checkSolstice.Checked = false;
				labelSurvivalValue.Text = "0";
			};
			groupBoxInstanceState.Controls.Add(buttonResetInstanceState);
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
			labelPlayerCount.Size = new Size(280, 20);  // 幅を広げて警告ユーザー数も表示できるように
			labelPlayerCount.Text = "総人数: 0人 | 生存: 0人";
			labelPlayerCount.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
			labelPlayerCount.TextAlign = ContentAlignment.MiddleLeft;
			groupBoxPlayerList.Controls.Add(labelPlayerCount);

			// サウンド設定ボタン（右上端にアイコンで配置）
			var buttonSoundSettings = new Button();
			buttonSoundSettings.Name = "buttonSoundSettings";
			buttonSoundSettings.Location = new Point(295, 20);
			buttonSoundSettings.Size = new Size(30, 25);
			buttonSoundSettings.Text = "🔊";
			buttonSoundSettings.Font = new Font("Segoe UI Emoji", 9);
			buttonSoundSettings.UseVisualStyleBackColor = true;
			buttonSoundSettings.Click += ButtonSoundSettings_Click;
			groupBoxPlayerList.Controls.Add(buttonSoundSettings);

			// 警告対象ユーザー表示ボタン（右上端にアイコンで配置）
			var buttonShowWarningUsers = new Button();
			buttonShowWarningUsers.Name = "buttonShowWarningUsers";
			buttonShowWarningUsers.Location = new Point(330, 20);
			buttonShowWarningUsers.Size = new Size(30, 25);
			buttonShowWarningUsers.Text = "👤";
			buttonShowWarningUsers.Font = new Font("Segoe UI Emoji", 9);
			buttonShowWarningUsers.UseVisualStyleBackColor = true;
			buttonShowWarningUsers.Click += ButtonShowWarningUsers_Click;
			groupBoxPlayerList.Controls.Add(buttonShowWarningUsers);

			// 警告ユーザーリスト再読み込みボタン（右上端にアイコンで配置）
			var buttonReloadWarningUsers = new Button();
			buttonReloadWarningUsers.Name = "buttonReloadWarningUsers";
			buttonReloadWarningUsers.Location = new Point(365, 20);
			buttonReloadWarningUsers.Size = new Size(30, 25);
			buttonReloadWarningUsers.Text = "🔄";
			buttonReloadWarningUsers.Font = new Font("Segoe UI Emoji", 9);
			buttonReloadWarningUsers.UseVisualStyleBackColor = true;
			buttonReloadWarningUsers.Click += ButtonReloadWarningUsers_Click;
			groupBoxPlayerList.Controls.Add(buttonReloadWarningUsers);

			// リストビュー（ダブルバッファリング有効）
			var listViewPlayers = new DoubleBufferedListView();
			listViewPlayers.Name = "listViewPlayers";
			listViewPlayers.Location = new Point(10, 50);
			listViewPlayers.Size = new Size(375, 350);
			listViewPlayers.View = View.Details;
			listViewPlayers.FullRowSelect = true;
			listViewPlayers.GridLines = true;

			listViewPlayers.Columns.Add("プレイヤー名", 180);
			listViewPlayers.Columns.Add("状態", 60);
			listViewPlayers.Columns.Add("種別", 70);
			listViewPlayers.DoubleClick += ListViewPlayers_DoubleClick;

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
			labelTotalRounds.Size = new Size(180, 20);
			tabPageRounds.Controls.Add(labelTotalRounds);

			// リセットボタン
			var buttonResetStats = new Button();
			buttonResetStats.Name = "buttonResetStats";
			buttonResetStats.Text = "リセット";
			buttonResetStats.Location = new Point(190, 3);
			buttonResetStats.Size = new Size(70, 22);
			buttonResetStats.Click += ButtonResetStats_Click;
			tabPageRounds.Controls.Add(buttonResetStats);

			// ラウンド統計ListView（ダブルバッファリング有効）
			var listViewStats = new DoubleBufferedListView();
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

			// テラー統計ListView（ダブルバッファリング有効）
			var listViewStatsTerrors = new DoubleBufferedListView();
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

			// ダブルバッファリング有効
			var listViewRoundLog = new DoubleBufferedListView();
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
				ShowWarningUsersDialog(warningUsers);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"警告対象ユーザーの表示でエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// 警告対象ユーザー一覧ダイアログを表示
		/// </summary>
		private void ShowWarningUsersDialog(HashSet<string> warningUsers)
		{
			using (var dialog = new Form())
			{
				dialog.Text = "警告対象ユーザー一覧";
				dialog.Size = new Size(350, 450);
				dialog.StartPosition = FormStartPosition.CenterParent;
				dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
				dialog.MaximizeBox = false;
				dialog.MinimizeBox = false;

				var label = new Label();
				label.Text = $"現在ロードしている警告対象ユーザー ({warningUsers.Count}人):";
				label.Location = new Point(10, 10);
				label.Size = new Size(320, 20);
				dialog.Controls.Add(label);

				var listBox = new ListBox();
				listBox.Location = new Point(10, 35);
				listBox.Size = new Size(315, 320);
				listBox.Font = new Font("Meiryo UI", 9);
				
				foreach (var user in warningUsers.OrderBy(u => u))
				{
					listBox.Items.Add(user);
				}
				dialog.Controls.Add(listBox);

				var buttonRemove = new Button();
				buttonRemove.Text = "選択したユーザーを削除";
				buttonRemove.Location = new Point(10, 365);
				buttonRemove.Size = new Size(150, 30);
				buttonRemove.Click += (s, args) =>
				{
					if (listBox.SelectedItem != null)
					{
						string selectedUser = listBox.SelectedItem.ToString();
						if (webSocketClient.RemoveWarningUser(selectedUser))
						{
							listBox.Items.Remove(selectedUser);
							label.Text = $"現在ロードしている警告対象ユーザー ({listBox.Items.Count}人):";
							UpdatePlayerList();
						}
					}
				};
				dialog.Controls.Add(buttonRemove);

				var buttonClose = new Button();
				buttonClose.Text = "閉じる";
				buttonClose.Location = new Point(235, 365);
				buttonClose.Size = new Size(90, 30);
				buttonClose.Click += (s, args) => dialog.Close();
				dialog.Controls.Add(buttonClose);

				var noteLabel = new Label();
				noteLabel.Text = "※ warn_user.txt ファイルから読み込まれています";
				noteLabel.Location = new Point(10, 400);
				noteLabel.Size = new Size(320, 20);
				noteLabel.ForeColor = Color.Gray;
				dialog.Controls.Add(noteLabel);

				dialog.ShowDialog(this);
			}
		}

		/// <summary>
		/// プレイヤーリストをダブルクリックした時のイベントハンドラ
		/// </summary>
		private void ListViewPlayers_DoubleClick(object sender, EventArgs e)
		{
			try
			{
				var listView = sender as ListView;
				if (listView?.SelectedItems.Count > 0)
				{
					string playerName = listView.SelectedItems[0].Text;
					
					// 既に警告ユーザーの場合は削除を確認
					if (webSocketClient.IsWarningUser(playerName))
					{
						var result = MessageBox.Show(
							$"{playerName} は既に警告対象ユーザーです。\n警告リストから削除しますか？",
							"警告ユーザー削除",
							MessageBoxButtons.YesNo,
							MessageBoxIcon.Question);

						if (result == DialogResult.Yes)
						{
							if (webSocketClient.RemoveWarningUser(playerName))
							{
								MessageBox.Show($"{playerName} を警告リストから削除しました。", "削除完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
								UpdatePlayerList();
							}
						}
					}
					else
					{
						// 警告ユーザーに追加
						var result = MessageBox.Show(
							$"{playerName} を警告対象ユーザーに追加しますか？",
							"警告ユーザー追加",
							MessageBoxButtons.YesNo,
							MessageBoxIcon.Question);

						if (result == DialogResult.Yes)
						{
							if (webSocketClient.AddWarningUser(playerName))
							{
								MessageBox.Show($"{playerName} を警告リストに追加しました。", "追加完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
								UpdatePlayerList();
							}
							else
							{
								MessageBox.Show($"{playerName} は既に警告リストに登録されています。", "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"エラー: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// サウンド設定ボタンのクリックイベント
		/// </summary>
		private void ButtonSoundSettings_Click(object sender, EventArgs e)
		{
			try
			{
				ShowSoundSettingsDialog();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"サウンド設定の表示でエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// サウンド設定ダイアログを表示
		/// </summary>
		private void ShowSoundSettingsDialog()
		{
			using (var dialog = new Form())
			{
				dialog.Text = "サウンド設定";
				dialog.Size = new Size(450, 380);
				dialog.StartPosition = FormStartPosition.CenterParent;
				dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
				dialog.MaximizeBox = false;
				dialog.MinimizeBox = false;

				var settings = webSocketClient.SoundSettings;

				// Joinサウンド設定
				var groupJoin = new GroupBox();
				groupJoin.Text = "プレイヤー参加時のサウンド";
				groupJoin.Location = new Point(10, 10);
				groupJoin.Size = new Size(415, 80);
				dialog.Controls.Add(groupJoin);

				var checkJoinEnabled = new CheckBox();
				checkJoinEnabled.Text = "有効";
				checkJoinEnabled.Location = new Point(10, 25);
				checkJoinEnabled.Size = new Size(60, 20);
				checkJoinEnabled.Checked = settings.EnableJoinSound;
				groupJoin.Controls.Add(checkJoinEnabled);

				var textJoinPath = new TextBox();
				textJoinPath.Location = new Point(75, 23);
				textJoinPath.Size = new Size(250, 23);
				textJoinPath.Text = settings.JoinSoundPath;
				groupJoin.Controls.Add(textJoinPath);

				var buttonJoinBrowse = new Button();
				buttonJoinBrowse.Text = "参照...";
				buttonJoinBrowse.Location = new Point(330, 22);
				buttonJoinBrowse.Size = new Size(70, 25);
				buttonJoinBrowse.Click += (s, args) =>
				{
					using (var ofd = new OpenFileDialog())
					{
						ofd.Filter = "音声ファイル|*.mp3;*.wav|MP3ファイル|*.mp3|WAVファイル|*.wav|すべてのファイル|*.*";
						if (ofd.ShowDialog() == DialogResult.OK)
						{
							textJoinPath.Text = ofd.FileName;
						}
					}
				};
				groupJoin.Controls.Add(buttonJoinBrowse);

				var labelJoinNote = new Label();
				labelJoinNote.Text = "※ MP3またはWAVファイルを指定してください";
				labelJoinNote.Location = new Point(75, 50);
				labelJoinNote.Size = new Size(300, 20);
				labelJoinNote.ForeColor = Color.Gray;
				groupJoin.Controls.Add(labelJoinNote);

				// Leaveサウンド設定
				var groupLeave = new GroupBox();
				groupLeave.Text = "プレイヤー退出時のサウンド";
				groupLeave.Location = new Point(10, 100);
				groupLeave.Size = new Size(415, 80);
				dialog.Controls.Add(groupLeave);

				var checkLeaveEnabled = new CheckBox();
				checkLeaveEnabled.Text = "有効";
				checkLeaveEnabled.Location = new Point(10, 25);
				checkLeaveEnabled.Size = new Size(60, 20);
				checkLeaveEnabled.Checked = settings.EnableLeaveSound;
				groupLeave.Controls.Add(checkLeaveEnabled);

				var textLeavePath = new TextBox();
				textLeavePath.Location = new Point(75, 23);
				textLeavePath.Size = new Size(250, 23);
				textLeavePath.Text = settings.LeaveSoundPath;
				groupLeave.Controls.Add(textLeavePath);

				var buttonLeaveBrowse = new Button();
				buttonLeaveBrowse.Text = "参照...";
				buttonLeaveBrowse.Location = new Point(330, 22);
				buttonLeaveBrowse.Size = new Size(70, 25);
				buttonLeaveBrowse.Click += (s, args) =>
				{
					using (var ofd = new OpenFileDialog())
					{
						ofd.Filter = "音声ファイル|*.mp3;*.wav|MP3ファイル|*.mp3|WAVファイル|*.wav|すべてのファイル|*.*";
						if (ofd.ShowDialog() == DialogResult.OK)
						{
							textLeavePath.Text = ofd.FileName;
						}
					}
				};
				groupLeave.Controls.Add(buttonLeaveBrowse);

				var labelLeaveNote = new Label();
				labelLeaveNote.Text = "※ MP3またはWAVファイルを指定してください";
				labelLeaveNote.Location = new Point(75, 50);
				labelLeaveNote.Size = new Size(300, 20);
				labelLeaveNote.ForeColor = Color.Gray;
				groupLeave.Controls.Add(labelLeaveNote);

				// 警告ユーザー参加時サウンド設定
				var groupWarning = new GroupBox();
				groupWarning.Text = "⚠️ 警告ユーザー参加時のサウンド";
				groupWarning.Location = new Point(10, 190);
				groupWarning.Size = new Size(415, 80);
				dialog.Controls.Add(groupWarning);

				var checkWarningEnabled = new CheckBox();
				checkWarningEnabled.Text = "有効";
				checkWarningEnabled.Location = new Point(10, 25);
				checkWarningEnabled.Size = new Size(60, 20);
				checkWarningEnabled.Checked = settings.EnableWarningUserSound;
				groupWarning.Controls.Add(checkWarningEnabled);

				var textWarningPath = new TextBox();
				textWarningPath.Location = new Point(75, 23);
				textWarningPath.Size = new Size(250, 23);
				textWarningPath.Text = settings.WarningUserSoundPath;
				groupWarning.Controls.Add(textWarningPath);

				var buttonWarningBrowse = new Button();
				buttonWarningBrowse.Text = "参照...";
				buttonWarningBrowse.Location = new Point(330, 22);
				buttonWarningBrowse.Size = new Size(70, 25);
				buttonWarningBrowse.Click += (s, args) =>
				{
					using (var ofd = new OpenFileDialog())
					{
						ofd.Filter = "音声ファイル|*.mp3;*.wav|MP3ファイル|*.mp3|WAVファイル|*.wav|すべてのファイル|*.*";
						if (ofd.ShowDialog() == DialogResult.OK)
						{
							textWarningPath.Text = ofd.FileName;
						}
					}
				};
				groupWarning.Controls.Add(buttonWarningBrowse);

				var labelWarningNote = new Label();
				labelWarningNote.Text = "※ 空の場合はwarning.mp3またはシステム音を使用";
				labelWarningNote.Location = new Point(75, 50);
				labelWarningNote.Size = new Size(330, 20);
				labelWarningNote.ForeColor = Color.OrangeRed;
				groupWarning.Controls.Add(labelWarningNote);

				// ボタン
				var buttonSave = new Button();
				buttonSave.Text = "保存";
				buttonSave.Location = new Point(260, 290);
				buttonSave.Size = new Size(80, 30);
				buttonSave.Click += (s, args) =>
				{
					var newSettings = new SoundSettings
					{
						EnableJoinSound = checkJoinEnabled.Checked,
						JoinSoundPath = textJoinPath.Text,
						EnableLeaveSound = checkLeaveEnabled.Checked,
						LeaveSoundPath = textLeavePath.Text,
						EnableWarningUserSound = checkWarningEnabled.Checked,
						WarningUserSoundPath = textWarningPath.Text
					};
					webSocketClient.UpdateSoundSettings(newSettings);
					MessageBox.Show("サウンド設定を保存しました。", "保存完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
					dialog.Close();
				};
				dialog.Controls.Add(buttonSave);

				var buttonCancel = new Button();
				buttonCancel.Text = "キャンセル";
				buttonCancel.Location = new Point(345, 290);
				buttonCancel.Size = new Size(80, 30);
				buttonCancel.Click += (s, args) => dialog.Close();
				dialog.Controls.Add(buttonCancel);

				dialog.ShowDialog(this);
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

		private void ButtonResetStats_Click(object sender, EventArgs e)
		{
			try
			{
				// 確認ダイアログを表示
				var result = MessageBox.Show(
					"ラウンド統計、テラー統計、ラウンドログをすべてリセットします。\n\nこの操作は取り消せません。よろしいですか？",
					"統計リセットの確認",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Warning);

				if (result == DialogResult.Yes)
				{
					webSocketClient.ResetRoundStats();

					// UIを更新
					UpdateStatsDisplay();
					UpdateRoundLogDisplay();

					MessageBox.Show("統計をリセットしました。", "リセット完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"統計のリセットでエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void ButtonTerrorWindow_CheckedChanged(object sender, EventArgs e)
		{
			var checkBox = sender as CheckBox;
			if (checkBox == null) return;

			if (checkBox.Checked)
			{
				if (terrorDisplayForm == null || terrorDisplayForm.IsDisposed)
				{
					terrorDisplayForm = new TerrorDisplayForm();
					terrorDisplayForm.SetInstanceState(webSocketClient.InstanceState);
					terrorDisplayForm.UpdateTerrors(webSocketClient.CurrentTerrors);
					
					// 現在のプレイヤー数を同期
					int aliveCount = webSocketClient.Players.Values.Count(p => p.IsAlive);
					int totalCount = webSocketClient.Players.Count;
					terrorDisplayForm.UpdatePlayerCount(aliveCount, totalCount);
					
					// 現在のラウンド状態を同期
					var gameData = webSocketClient.GameData;
					if (mainFormRoundActive)
					{
						string roundType = gameData.ContainsKey("roundType") ? gameData["roundType"]?.ToString() ?? "-" : "-";
						// (開始)などのサフィックスを除去
						if (roundType.Contains(" ("))
							roundType = roundType.Substring(0, roundType.IndexOf(" ("));
						
						// 経過時間を含めて同期
						terrorDisplayForm.SyncRoundInfo(roundType, mainFormRoundStartTime, mainFormRoundActive);
					}
					else
					{
						// ラウンド非アクティブ時は次ラウンド予測のみ更新
						terrorDisplayForm.UpdateNextRoundPrediction();
					}
					
					// 透明度スライダーの値を適用
					var trackBar = FindControl("trackBarOpacity") as TrackBar;
					if (trackBar != null)
					{
						terrorDisplayForm.SetOpacity(trackBar.Value / 100.0);
					}
					
					terrorDisplayForm.FormClosed += (s, args) =>
					{
						// フォームが閉じられたらチェックボックスを解除
						if (!checkBox.IsDisposed)
						{
							checkBox.Checked = false;
						}
					};
					terrorDisplayForm.Show();
				}
			}
			else
			{
				if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
				{
					terrorDisplayForm.Close();
				}
			}
		}

		private void BtnThemeToggle_Click(object sender, EventArgs e)
		{
			ThemeManager.ToggleTheme();
		}

		private void BtnThemeToggle_Paint(object sender, PaintEventArgs e)
		{
			var btn = sender as Button;
			if (btn == null) return;

			// 背景を描画
			e.Graphics.Clear(btn.BackColor);

			// アイコンを描画
			string icon = ThemeManager.IsDark ? "☀" : "🌙";
			using (var font = new Font("Segoe UI Emoji", 11))
			{
				var textSize = e.Graphics.MeasureString(icon, font);
				float x = (btn.Width - textSize.Width) / 2;
				float y = (btn.Height - textSize.Height) / 2;
				e.Graphics.DrawString(icon, font, new SolidBrush(btn.ForeColor), x, y);
			}
		}

		private void OnThemeChanged(object sender, AppTheme newTheme)
		{
			// メインフォームにテーマを適用
			ThemeManager.Apply(this);

			// テーマ切替ボタンを再描画
			var btnThemeToggle = FindControl("btnThemeToggle") as Button;
			btnThemeToggle?.Invalidate();

			// テラー表示フォームが開いていればテーマを適用
			if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
			{
				terrorDisplayForm.ApplyTheme();
			}

			// プレイヤーリストを更新（色をテーマ対応に）
			UpdatePlayerList();

			// 次ラウンド予測の色を更新
			UpdateNextRoundPrediction();
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
				
				// 経過時間タイマー停止
				mainFormRoundActive = false;
				elapsedTimeTimer.Stop();
				
				// 次ラウンド予測を更新
				UpdateNextRoundPrediction();
				
				// テラー表示フォームに通知
				if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
				{
					terrorDisplayForm.OnRoundEnd();
				}
			}));
		}

		/// <summary>
		/// インスタンス状態変更時（鳥遭遇、ラウンド開始時など）
		/// </summary>
		private void OnInstanceStateChanged()
		{
			this.Invoke(new Action(() =>
			{
				// メインフォームの鳥チェックボックスを更新
				UpdateBirdCheckboxes();
				
				// 次ラウンド予測を更新
				UpdateNextRoundPrediction();
				
				// テラー表示フォームにも通知
				if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
				{
					terrorDisplayForm.UpdateNextRoundPrediction();
				}
			}));
		}

		/// <summary>
		/// プレイヤー数変更時（死亡、参加、退出時）
		/// </summary>
		private void OnPlayerCountChanged()
		{
			this.Invoke(new Action(() =>
			{
				// テラー表示フォームのプレイヤー数を更新
				if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
				{
					int aliveCount = webSocketClient.Players.Values.Count(p => p.IsAlive);
					int totalCount = webSocketClient.Players.Count;
					terrorDisplayForm.UpdatePlayerCount(aliveCount, totalCount);
				}
			}));
		}

		/// <summary>
		/// 鳥チェックボックス、Moonチェックボックス、推定生存回数を更新
		/// </summary>
		private void UpdateBirdCheckboxes()
		{
			var instanceState = webSocketClient?.InstanceState;
			if (instanceState == null) return;

			// 鳥チェックボックス
			var checkBigBird = FindControl("checkBigBird") as CheckBox;
			var checkJudgementBird = FindControl("checkJudgementBird") as CheckBox;
			var checkPunishingBird = FindControl("checkPunishingBird") as CheckBox;

			if (checkBigBird != null && checkBigBird.Checked != instanceState.MetBigBird)
				checkBigBird.Checked = instanceState.MetBigBird;
			if (checkJudgementBird != null && checkJudgementBird.Checked != instanceState.MetJudgementBird)
				checkJudgementBird.Checked = instanceState.MetJudgementBird;
			if (checkPunishingBird != null && checkPunishingBird.Checked != instanceState.MetPunishingBird)
				checkPunishingBird.Checked = instanceState.MetPunishingBird;

			// Moonチェックボックス
			var checkBloodMoon = FindControl("checkBloodMoon") as CheckBox;
			var checkTwilight = FindControl("checkTwilight") as CheckBox;
			var checkMysticMoon = FindControl("checkMysticMoon") as CheckBox;
			var checkSolstice = FindControl("checkSolstice") as CheckBox;

			if (checkBloodMoon != null && checkBloodMoon.Checked != instanceState.BloodMoonUnlocked)
				checkBloodMoon.Checked = instanceState.BloodMoonUnlocked;
			if (checkTwilight != null && checkTwilight.Checked != instanceState.TwilightUnlocked)
				checkTwilight.Checked = instanceState.TwilightUnlocked;
			if (checkMysticMoon != null && checkMysticMoon.Checked != instanceState.MysticMoonUnlocked)
				checkMysticMoon.Checked = instanceState.MysticMoonUnlocked;
			if (checkSolstice != null && checkSolstice.Checked != instanceState.SolsticeUnlocked)
				checkSolstice.Checked = instanceState.SolsticeUnlocked;

			// 推定生存回数を更新
			var labelSurvivalValue = FindControl("labelSurvivalValue") as Label;
			if (labelSurvivalValue != null)
			{
				int targetValue = Math.Max(0, Math.Min(999, instanceState.EstimatedSurvivalCount));
				labelSurvivalValue.Text = targetValue.ToString();
			}
		}

		private void OnRoundStart(string roundType)
		{
			this.Invoke(new Action(() =>
			{
				// 経過時間タイマー開始
				mainFormRoundStartTime = DateTime.Now;
				mainFormRoundActive = true;
				elapsedTimeTimer.Start();
				
				// 次ラウンド予測を更新
				UpdateNextRoundPrediction();
				
				// テラー表示フォームに通知
				if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
				{
					terrorDisplayForm.OnRoundStart(roundType);
				}
			}));
		}

		private void ElapsedTimeTimer_Tick(object sender, EventArgs e)
		{
			if (mainFormRoundActive)
			{
				TimeSpan elapsed = DateTime.Now - mainFormRoundStartTime;
				var textBoxElapsedTime = FindControl("textBox_elapsedTime") as TextBox;
				if (textBoxElapsedTime != null)
				{
					textBoxElapsedTime.Text = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
				}
			}
		}

		private void UpdateNextRoundPrediction()
		{
			var textBoxNextRound = FindControl("textBox_nextRound") as TextBox;
			if (textBoxNextRound == null) return;

			var instanceState = webSocketClient?.InstanceState;
			if (instanceState == null)
			{
				textBoxNextRound.Text = "-";
				textBoxNextRound.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;
				return;
			}

			// ラウンドがアクティブな場合は、現在のラウンドを考慮した予測を使用
			if (mainFormRoundActive && !string.IsNullOrEmpty(instanceState.CurrentRoundType))
			{
				UpdateNextRoundPredictionForCurrentRound(instanceState.CurrentRoundType);
				return;
			}

			string prediction = "";
			Color color = ThemeManager.IsDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;

			// Moon解禁チェック
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
				prediction = "通常 (特殊未解放)";
				color = ThemeManager.GetPredictionColor("disabled");
			}
			else
			{
				string lastRound = instanceState.LastRoundType?.ToLower() ?? "";
				
				if (IsSpecialRoundType(lastRound))
				{
					prediction = "通常";
					color = ThemeManager.GetPredictionColor("normal");
				}
				else if (IsOverrideRoundType(lastRound))
				{
					prediction = "通常 or 特殊";
					color = ThemeManager.GetPredictionColor("special");
				}
				else if (instanceState.NormalRoundCount >= 2)
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

			textBoxNextRound.Text = prediction;
			textBoxNextRound.ForeColor = color;
		}

		private bool IsSpecialRoundType(string roundType)
		{
			string lower = roundType.ToLower();
			string[] specialRounds = {
				"alternate", "punished", "cracked", "sabotage", "fog",
				"bloodbath", "double trouble", "midnight",
				"blood moon", "mystic moon", "twilight", "solstice"
			};
			foreach (var special in specialRounds)
			{
				if (lower.Contains(special)) return true;
			}
			return false;
		}

		private bool IsOverrideRoundType(string roundType)
		{
			string lower = roundType.ToLower();
			return lower.Contains("ghost") || lower.Contains("8 pages") || lower.Contains("unbound");
		}

		/// <summary>
		/// 現在のラウンドを考慮した次ラウンド予測を更新
		/// </summary>
		private void UpdateNextRoundPredictionForCurrentRound(string currentRoundType)
		{
			var textBoxNextRound = FindControl("textBox_nextRound") as TextBox;
			if (textBoxNextRound == null) return;

			var instanceState = webSocketClient?.InstanceState;
			if (instanceState == null)
			{
				textBoxNextRound.Text = "-";
				return;
			}

			string prediction = "";
			Color color = ThemeManager.IsDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;

			// 現在のラウンドが特殊なら次は通常
			if (IsSpecialRoundType(currentRoundType.ToLower()))
			{
				prediction = "通常";
				color = ThemeManager.GetPredictionColor("normal");
			}
			else if (IsOverrideRoundType(currentRoundType.ToLower()))
			{
				prediction = "通常 or 特殊";
				color = ThemeManager.GetPredictionColor("special");
			}
			else
			{
				// 通常ラウンドの場合、カウントを考慮
				int normalCount = instanceState.NormalRoundCount + 1; // 現在のラウンドも含む
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

			textBoxNextRound.Text = prediction;
			textBoxNextRound.ForeColor = color;
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
					
					// プレイヤー数を更新
					int aliveCount = webSocketClient.Players.Values.Count(p => p.IsAlive);
					int totalCount = webSocketClient.Players.Count;
					terrorDisplayForm.UpdatePlayerCount(aliveCount, totalCount);
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

						// 色分け（優先順位: 警告 > 死亡 > 自分）- テーマ対応
						if (isWarningUser)
						{
							item.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.PlayerWarning : ThemeManager.Light.PlayerWarning;
							item.Font = new Font(listView.Font, FontStyle.Bold); // 太字で強調
						}
						else if (!player.IsAlive)
						{
							item.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.PlayerDead : ThemeManager.Light.PlayerDead;
						}
						else if (player.UserId == localPlayerUserId)
						{
							item.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.PlayerSelf : ThemeManager.Light.PlayerSelf;
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

				// プレイヤー数表示を更新（警告ユーザー数も表示）- テーマ対応
				string countText = $"総人数: {totalPlayers}人 | 生存: {alivePlayers}人";
				if (warningPlayers > 0)
				{
					countText += $" | ⚠️警告: {warningPlayers}人";
					labelPlayerCount.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.PlayerWarning : ThemeManager.Light.PlayerWarning;
				}
				else
				{
					labelPlayerCount.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.PlayerCountLabel : ThemeManager.Light.PlayerCountLabel;
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
					item.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.RoundLogSurvived : ThemeManager.Light.RoundLogSurvived;
				else
					item.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.RoundLogDied : ThemeManager.Light.RoundLogDied;

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
					// デフォルトの文字色を設定
					textBox.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;
					
					if (value.Contains("Classic"))
					{
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(40, 80, 120) : Color.LightBlue;
					}
					else if (value.Contains("Alternate"))
					{
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(40, 100, 40) : Color.LightGreen;
					}
					else if (value.Contains("Sabotage"))
					{
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(100, 60, 60) : Color.LightCoral;
					}
					else if (value.Contains("Bloodbath"))
					{
						// ブラッドバスは濃い赤背景に白文字
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(139, 0, 0) : Color.DarkRed;
						textBox.ForeColor = Color.White;
					}
					else if (value.Contains("Blood"))
					{
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(100, 50, 70) : Color.LightPink;
					}
					else if (value.Contains("Midnight"))
					{
						textBox.BackColor = Color.DarkSlateBlue;
						textBox.ForeColor = Color.White;
					}
					else if (value.Contains("Cracked"))
					{
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(100, 100, 40) : Color.LightYellow;
					}
					else if (value.Contains("Mystic"))
					{
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(60, 80, 100) : Color.Lavender;
					}
					else if (value.Contains("Twilight"))
					{
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(120, 100, 40) : Color.Gold;
						textBox.ForeColor = ThemeManager.IsDark ? Color.White : Color.Black;
					}
					else if (value.Contains("Solstice"))
					{
						textBox.BackColor = ThemeManager.IsDark ? Color.FromArgb(0, 100, 50) : Color.FromArgb(0, 200, 100);
						textBox.ForeColor = Color.White;
					}
					else
					{
						textBox.BackColor = ThemeManager.IsDark ? ThemeManager.Dark.TextBoxBackground : SystemColors.Window;
					}
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

		/// <summary>
		/// 入力ダイアログを表示
		/// </summary>
		private string ShowInputDialog(string title, string prompt, string defaultValue = "")
		{
			using (var dialog = new Form())
			{
				dialog.Text = title;
				dialog.Size = new Size(300, 150);
				dialog.StartPosition = FormStartPosition.CenterParent;
				dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
				dialog.MaximizeBox = false;
				dialog.MinimizeBox = false;

				var label = new Label();
				label.Text = prompt;
				label.Location = new Point(10, 15);
				label.Size = new Size(270, 20);
				dialog.Controls.Add(label);

				var textBox = new TextBox();
				textBox.Text = defaultValue;
				textBox.Location = new Point(10, 40);
				textBox.Size = new Size(260, 23);
				textBox.SelectAll();
				dialog.Controls.Add(textBox);

				var buttonOk = new Button();
				buttonOk.Text = "OK";
				buttonOk.DialogResult = DialogResult.OK;
				buttonOk.Location = new Point(110, 75);
				buttonOk.Size = new Size(75, 25);
				dialog.Controls.Add(buttonOk);

				var buttonCancel = new Button();
				buttonCancel.Text = "キャンセル";
				buttonCancel.DialogResult = DialogResult.Cancel;
				buttonCancel.Location = new Point(195, 75);
				buttonCancel.Size = new Size(75, 25);
				dialog.Controls.Add(buttonCancel);

				dialog.AcceptButton = buttonOk;
				dialog.CancelButton = buttonCancel;

				if (dialog.ShowDialog(this) == DialogResult.OK)
				{
					return textBox.Text;
				}
				return null;
			}
		}

		/// <summary>
		/// 設定を保存する
		/// </summary>
		private void SaveSettings()
		{
			try
			{
				// テーマを保存
				appSettings.SetTheme(ThemeManager.CurrentTheme);
				
				// 透明度を保存
				var trackBar = FindControl("trackBarOpacity") as TrackBar;
				if (trackBar != null)
				{
					appSettings.TerrorFormOpacity = trackBar.Value;
				}
				
				// URLを保存
				appSettings.WebSocketUrl = textBoxUrl.Text;
				
				appSettings.Save();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"設定保存エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// フォームクローズ時のイベントハンドラ
		/// </summary>
		private void ToNStatTool_FormClosing(object sender, FormClosingEventArgs e)
		{
			// OnFormClosingで保存するので、ここでは何もしない
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			// 設定を保存
			SaveSettings();
			
			webSocketClient?.DisconnectAsync().Wait();

			uiUpdateTimer?.Stop();
			uiUpdateTimer?.Dispose();

			elapsedTimeTimer?.Stop();
			elapsedTimeTimer?.Dispose();

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