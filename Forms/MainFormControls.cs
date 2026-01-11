using System;
using System.Drawing;
using System.Windows.Forms;
using ToNStatTool.Controls;

namespace ToNStatTool
{
    // UI コントロール作成部分
    public partial class ToNStatTool
    {
        private void CreateConnectionControls()
        {
            // ToolTipオブジェクトを作成（このメソッド全体で共有）
            var mainToolTip = new ToolTip();
            mainToolTip.AutoPopDelay = 5000;
            mainToolTip.InitialDelay = 500;
            mainToolTip.ReshowDelay = 200;

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
            mainToolTip.SetToolTip(buttonConnect, "ToNSaveManagerに接続/切断");
            this.Controls.Add(buttonConnect);

            // ステータス表示
            labelStatus = new Label();
            labelStatus.Location = new Point(540, 15);
            labelStatus.Size = new Size(200, 23);
            labelStatus.Text = "未接続";
            labelStatus.ForeColor = Color.Red;
            this.Controls.Add(labelStatus);

            // 統計表示ボタン（テラー表示ウィンドウの左側）
            var buttonStats = new Button();
            buttonStats.Name = "buttonStats";
            buttonStats.Location = new Point(787, 10);
            buttonStats.Size = new Size(26, 26);
            buttonStats.Text = "📊";
            buttonStats.Font = new Font("Segoe UI Emoji", 9);
            buttonStats.FlatStyle = FlatStyle.Flat;
            buttonStats.FlatAppearance.BorderSize = 1;
            buttonStats.Click += ButtonStats_Click;
            mainToolTip.SetToolTip(buttonStats, "セッション統計を表示");
            this.Controls.Add(buttonStats);

            // インスタンスURLコピーボタン
            var buttonCopyInstanceUrl = new Button();
            buttonCopyInstanceUrl.Name = "buttonCopyInstanceUrl";
            buttonCopyInstanceUrl.Location = new Point(817, 10);
            buttonCopyInstanceUrl.Size = new Size(26, 26);
            buttonCopyInstanceUrl.Text = "🔗";
            buttonCopyInstanceUrl.Font = new Font("Segoe UI Emoji", 9);
            buttonCopyInstanceUrl.FlatStyle = FlatStyle.Flat;
            buttonCopyInstanceUrl.FlatAppearance.BorderSize = 1;
            buttonCopyInstanceUrl.Click += ButtonCopyInstanceUrl_Click;
            mainToolTip.SetToolTip(buttonCopyInstanceUrl, "インスタンスURLをクリップボードにコピー");
            this.Controls.Add(buttonCopyInstanceUrl);

            // セーブコードボタン
            var buttonSaveCodes = new Button();
            buttonSaveCodes.Name = "buttonSaveCodes";
            buttonSaveCodes.Location = new Point(847, 10);
            buttonSaveCodes.Size = new Size(26, 26);
            buttonSaveCodes.Text = "💾";
            buttonSaveCodes.Font = new Font("Segoe UI Emoji", 9);
            buttonSaveCodes.FlatStyle = FlatStyle.Flat;
            buttonSaveCodes.FlatAppearance.BorderSize = 1;
            buttonSaveCodes.Click += ButtonSaveCodes_Click;
            mainToolTip.SetToolTip(buttonSaveCodes, "最近のセーブコードを表示");
            this.Controls.Add(buttonSaveCodes);

            // テラー表示ウィンドウボタン（チェックボックススタイル）
            var buttonTerrorWindow = new CheckBox();
            buttonTerrorWindow.Name = "buttonTerrorWindow";
            buttonTerrorWindow.Location = new Point(887, 11);
            buttonTerrorWindow.Size = new Size(130, 25);
            buttonTerrorWindow.Text = "テラー表示ウィンドウ";
            buttonTerrorWindow.Appearance = Appearance.Button;
            buttonTerrorWindow.TextAlign = ContentAlignment.MiddleCenter;
            buttonTerrorWindow.CheckedChanged += ButtonTerrorWindow_CheckedChanged;
            mainToolTip.SetToolTip(buttonTerrorWindow, "テラー情報を別ウィンドウで表示");
            this.Controls.Add(buttonTerrorWindow);

            // 透明度ラベル
            var labelOpacity = new Label();
            labelOpacity.Text = "透明度:";
            labelOpacity.Location = new Point(1027, 15);
            labelOpacity.Size = new Size(50, 20);
            labelOpacity.Font = new Font("Meiryo UI", 9);
            this.Controls.Add(labelOpacity);

            // 透明度スライダー
            var trackBarOpacity = new TrackBar();
            trackBarOpacity.Name = "trackBarOpacity";
            trackBarOpacity.Location = new Point(1072, 8);
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
            mainToolTip.SetToolTip(trackBarOpacity, "テラー表示ウィンドウの透明度を調整 (10-100%)");
            this.Controls.Add(trackBarOpacity);

            // 設定ボタン
            var btnSettings = new Button();
            btnSettings.Name = "btnSettings";
            btnSettings.Location = new Point(1154, 10);
            btnSettings.Size = new Size(26, 26);
            btnSettings.Text = "🛠";
            btnSettings.Font = new Font("Segoe UI Emoji", 9);
            btnSettings.FlatStyle = FlatStyle.Flat;
            btnSettings.FlatAppearance.BorderSize = 1;
            btnSettings.Click += ButtonSoundSettings_Click;
            mainToolTip.SetToolTip(btnSettings, "設定を開く（テーマ・通知音など）");
            this.Controls.Add(btnSettings);
        }

        private void CreateTerrorDisplay()
        {
            // テラー表示グループ
            groupBoxTerrors = new GroupBox();
            groupBoxTerrors.Text = "現在のテラー";
            groupBoxTerrors.Location = new Point(10, 50);
            groupBoxTerrors.Size = new Size(600, 240);
            this.Controls.Add(groupBoxTerrors);

            terrorDisplayPanel = new FlowLayoutPanel();
            terrorDisplayPanel.Location = new Point(10, 25);
            terrorDisplayPanel.Size = new Size(580, 220);
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

            // 4行目: 次ラウンド予測 + 所持アイテム
            var labelNextRound = new Label();
            labelNextRound.Text = "次ラウンド:";
            labelNextRound.Location = new Point(10, 97);
            labelNextRound.Size = new Size(55, 20);
            groupBoxRoundInfo.Controls.Add(labelNextRound);

            var textBoxNextRound = new TextBox();
            textBoxNextRound.Name = "textBox_nextRound";
            textBoxNextRound.Location = new Point(65, 95);
            textBoxNextRound.Size = new Size(250, 23);
            textBoxNextRound.ReadOnly = true;
            textBoxNextRound.Text = "-";
            groupBoxRoundInfo.Controls.Add(textBoxNextRound);

            var labelCurrentItem = new Label();
            labelCurrentItem.Text = "所持アイテム:";
            labelCurrentItem.Location = new Point(325, 97);
            labelCurrentItem.Size = new Size(70, 20);
            groupBoxRoundInfo.Controls.Add(labelCurrentItem);

            var textBoxCurrentItem = new TextBox();
            textBoxCurrentItem.Name = "textBox_currentItem";
            textBoxCurrentItem.Location = new Point(395, 95);
            textBoxCurrentItem.Size = new Size(150, 23);
            textBoxCurrentItem.ReadOnly = true;
            textBoxCurrentItem.Text = "-";
            groupBoxRoundInfo.Controls.Add(textBoxCurrentItem);

            // インスタンス状態設定グループを作成
            CreateInstanceStateControls();
        }

        private void CreateInstanceStateControls()
        {
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
            checkTwilight.CheckedChanged += CheckTwilight_CheckedChanged;
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
            checkSolstice.CheckedChanged += CheckSolstice_CheckedChanged;
            groupBoxInstanceState.Controls.Add(checkSolstice);

            // 生存回数
            var labelSurvivalCount = new Label();
            labelSurvivalCount.Text = "推定生存数:";
            labelSurvivalCount.Location = new Point(10, 73);
            labelSurvivalCount.Size = new Size(75, 20);
            groupBoxInstanceState.Controls.Add(labelSurvivalCount);

            var labelSurvivalValue = new Label();
            labelSurvivalValue.Name = "labelSurvivalValue";
            labelSurvivalValue.Text = "0";
            labelSurvivalValue.Location = new Point(90, 71);
            labelSurvivalValue.Size = new Size(40, 20);
            labelSurvivalValue.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
            labelSurvivalValue.TextAlign = ContentAlignment.MiddleRight;
            groupBoxInstanceState.Controls.Add(labelSurvivalValue);

            var buttonEditSurvival = new Button();
            buttonEditSurvival.Name = "buttonEditSurvival";
            buttonEditSurvival.Text = "編集";
            buttonEditSurvival.Location = new Point(135, 70);
            buttonEditSurvival.Size = new Size(45, 25);
            buttonEditSurvival.Click += ButtonEditSurvival_Click;
            groupBoxInstanceState.Controls.Add(buttonEditSurvival);

            var buttonResetInstanceState = new Button();
            buttonResetInstanceState.Text = "リセット";
            buttonResetInstanceState.Location = new Point(480, 70);
            buttonResetInstanceState.Size = new Size(65, 25);
            buttonResetInstanceState.Click += ButtonResetInstanceState_Click;
            groupBoxInstanceState.Controls.Add(buttonResetInstanceState);

            // インスタンス状態設定のToolTip
            var instanceStateToolTip = new ToolTip();
            instanceStateToolTip.SetToolTip(buttonEditSurvival, "推定生存回数を手動で編集");
            instanceStateToolTip.SetToolTip(buttonResetInstanceState, "インスタンス状態をすべてリセット");
        }

        private void CreatePlayerListControls()
        {
            groupBoxPlayerList = new GroupBox();
            groupBoxPlayerList.Text = "プレイヤー一覧";
            groupBoxPlayerList.Location = new Point(10, 300);
            groupBoxPlayerList.Size = new Size(400, 415);
            this.Controls.Add(groupBoxPlayerList);

            var labelPlayerCount = new Label();
            labelPlayerCount.Name = "labelPlayerCount";
            labelPlayerCount.Location = new Point(10, 25);
            labelPlayerCount.Size = new Size(200, 20);
            labelPlayerCount.Text = "総人数: 0人 | 生存: 0人";
            labelPlayerCount.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
            labelPlayerCount.TextAlign = ContentAlignment.MiddleLeft;
            groupBoxPlayerList.Controls.Add(labelPlayerCount);

            var buttonShowWarningUsers = new Button();
            buttonShowWarningUsers.Name = "buttonShowWarningUsers";
            buttonShowWarningUsers.Location = new Point(320, 20);
            buttonShowWarningUsers.Size = new Size(30, 25);
            buttonShowWarningUsers.Text = "👤";
            buttonShowWarningUsers.Font = new Font("Segoe UI Emoji", 9);
            buttonShowWarningUsers.UseVisualStyleBackColor = true;
            buttonShowWarningUsers.Click += ButtonShowWarningUsers_Click;
            groupBoxPlayerList.Controls.Add(buttonShowWarningUsers);

            var buttonReloadWarningUsers = new Button();
            buttonReloadWarningUsers.Name = "buttonReloadWarningUsers";
            buttonReloadWarningUsers.Location = new Point(355, 20);
            buttonReloadWarningUsers.Size = new Size(30, 25);
            buttonReloadWarningUsers.Text = "🔄";
            buttonReloadWarningUsers.Font = new Font("Segoe UI Emoji", 9);
            buttonReloadWarningUsers.UseVisualStyleBackColor = true;
            buttonReloadWarningUsers.Click += ButtonReloadWarningUsers_Click;
            groupBoxPlayerList.Controls.Add(buttonReloadWarningUsers);

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

            var toolTip = new ToolTip();
            toolTip.SetToolTip(buttonShowWarningUsers, "警告対象ユーザー一覧を表示");
            toolTip.SetToolTip(buttonReloadWarningUsers, "警告対象ユーザーリストを再読み込み");
        }

        private void CreateStatsControls()
        {
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

            var labelTotalRounds = new Label();
            labelTotalRounds.Name = "labelTotalRounds";
            labelTotalRounds.Text = "総ラウンド数: 0";
            labelTotalRounds.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
            labelTotalRounds.Location = new Point(5, 5);
            labelTotalRounds.Size = new Size(180, 20);
            tabPageRounds.Controls.Add(labelTotalRounds);

            var buttonResetStats = new Button();
            buttonResetStats.Name = "buttonResetStats";
            buttonResetStats.Text = "リセット";
            buttonResetStats.Location = new Point(190, 3);
            buttonResetStats.Size = new Size(70, 22);
            buttonResetStats.Click += ButtonResetStats_Click;
            tabPageRounds.Controls.Add(buttonResetStats);

            // 統計リセットボタンのToolTip
            var statsToolTip = new ToolTip();
            statsToolTip.SetToolTip(buttonResetStats, "ラウンド統計をリセット");

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
            groupBoxRoundLog = new GroupBox();
            groupBoxRoundLog.Text = "ラウンドログ";
            groupBoxRoundLog.Location = new Point(730, 300);
            groupBoxRoundLog.Size = new Size(450, 415);
            this.Controls.Add(groupBoxRoundLog);

            // フィルターパネル
            var filterPanel = new Panel();
            filterPanel.Location = new Point(10, 20);
            filterPanel.Size = new Size(430, 30);
            groupBoxRoundLog.Controls.Add(filterPanel);

            // ラウンド種別フィルター
            var labelRoundFilter = new Label();
            labelRoundFilter.Text = "種別:";
            labelRoundFilter.Location = new Point(0, 5);
            labelRoundFilter.Size = new Size(35, 20);
            filterPanel.Controls.Add(labelRoundFilter);

            var comboRoundFilter = new ComboBox();
            comboRoundFilter.Name = "comboRoundFilter";
            comboRoundFilter.Location = new Point(35, 2);
            comboRoundFilter.Size = new Size(120, 23);
            comboRoundFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            comboRoundFilter.Items.Add("(すべて)");
            // ラウンド種別を追加
            foreach (ToNRoundType roundType in Enum.GetValues(typeof(ToNRoundType)))
            {
                if (roundType != ToNRoundType.Intermission)
                {
                    comboRoundFilter.Items.Add(ToNRoundTypeHelper.GetDisplayName(roundType));
                }
            }
            comboRoundFilter.SelectedIndex = 0;
            comboRoundFilter.SelectedIndexChanged += ComboRoundFilter_SelectedIndexChanged;
            filterPanel.Controls.Add(comboRoundFilter);

            // テラー名フィルター
            var labelTerrorFilter = new Label();
            labelTerrorFilter.Text = "テラー:";
            labelTerrorFilter.Location = new Point(165, 5);
            labelTerrorFilter.Size = new Size(45, 20);
            filterPanel.Controls.Add(labelTerrorFilter);

            var textTerrorFilter = new TextBox();
            textTerrorFilter.Name = "textTerrorFilter";
            textTerrorFilter.Location = new Point(210, 2);
            textTerrorFilter.Size = new Size(140, 23);
            textTerrorFilter.TextChanged += TextTerrorFilter_TextChanged;
            filterPanel.Controls.Add(textTerrorFilter);

            // フィルタークリアボタン
            var buttonClearFilter = new Button();
            buttonClearFilter.Name = "buttonClearFilter";
            buttonClearFilter.Text = "×";
            buttonClearFilter.Location = new Point(355, 1);
            buttonClearFilter.Size = new Size(25, 25);
            buttonClearFilter.Click += ButtonClearFilter_Click;
            filterPanel.Controls.Add(buttonClearFilter);

            // フィルター件数表示
            var labelFilterCount = new Label();
            labelFilterCount.Name = "labelFilterCount";
            labelFilterCount.Text = "";
            labelFilterCount.Location = new Point(385, 5);
            labelFilterCount.Size = new Size(45, 20);
            labelFilterCount.TextAlign = ContentAlignment.MiddleRight;
            filterPanel.Controls.Add(labelFilterCount);

            var listViewRoundLog = new DoubleBufferedListView();
            listViewRoundLog.Name = "listViewRoundLog";
            listViewRoundLog.Location = new Point(10, 55);
            listViewRoundLog.Size = new Size(430, 345);
            listViewRoundLog.View = View.Details;
            listViewRoundLog.FullRowSelect = true;
            listViewRoundLog.GridLines = true;
            listViewRoundLog.Columns.Add("時刻", 45);
            listViewRoundLog.Columns.Add("ラウンド", 80);
            listViewRoundLog.Columns.Add("マップ", 100);
            listViewRoundLog.Columns.Add("テラー", 120);
            listViewRoundLog.Columns.Add("アイテム", 60);
            groupBoxRoundLog.Controls.Add(listViewRoundLog);

            // ツールチップ設定
            var toolTipRoundLog = new ToolTip();
            toolTipRoundLog.SetToolTip(comboRoundFilter, "ラウンド種別でフィルタリング");
            toolTipRoundLog.SetToolTip(textTerrorFilter, "テラー名で部分一致検索");
            toolTipRoundLog.SetToolTip(buttonClearFilter, "フィルターをクリア");
        }

        private void ComboRoundFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRoundLogDisplay();
        }

        private void TextTerrorFilter_TextChanged(object sender, EventArgs e)
        {
            UpdateRoundLogDisplay();
        }

        private void ButtonClearFilter_Click(object sender, EventArgs e)
        {
            var comboRoundFilter = FindControl("comboRoundFilter") as ComboBox;
            var textTerrorFilter = FindControl("textTerrorFilter") as TextBox;

            if (comboRoundFilter != null) comboRoundFilter.SelectedIndex = 0;
            if (textTerrorFilter != null) textTerrorFilter.Text = "";
        }

        private void CreateEventControls()
        {
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

        // イベントハンドラ（インスタンス状態関連）
        private void CheckTwilight_CheckedChanged(object sender, EventArgs e)
        {
            var checkTwilight = sender as CheckBox;
            if (checkTwilight == null) return;

            if (webSocketClient?.InstanceState != null)
                webSocketClient.InstanceState.TwilightUnlocked = checkTwilight.Checked;
            
            if (checkTwilight.Checked)
            {
                var chkBigBird = FindControl("checkBigBird") as CheckBox;
                var chkJudgementBird = FindControl("checkJudgementBird") as CheckBox;
                var chkPunishingBird = FindControl("checkPunishingBird") as CheckBox;
                
                if (chkBigBird != null) chkBigBird.Checked = true;
                if (chkJudgementBird != null) chkJudgementBird.Checked = true;
                if (chkPunishingBird != null) chkPunishingBird.Checked = true;
            }
        }

        private void CheckSolstice_CheckedChanged(object sender, EventArgs e)
        {
            var checkSolstice = sender as CheckBox;
            if (checkSolstice == null) return;

            if (webSocketClient?.InstanceState != null)
                webSocketClient.InstanceState.SolsticeUnlocked = checkSolstice.Checked;
            
            if (checkSolstice.Checked)
            {
                var chkBloodMoon = FindControl("checkBloodMoon") as CheckBox;
                var chkTwilight = FindControl("checkTwilight") as CheckBox;
                var chkMysticMoon = FindControl("checkMysticMoon") as CheckBox;
                
                if (chkBloodMoon != null) chkBloodMoon.Checked = true;
                if (chkTwilight != null) chkTwilight.Checked = true;
                if (chkMysticMoon != null) chkMysticMoon.Checked = true;
            }
        }

        private void ButtonEditSurvival_Click(object sender, EventArgs e)
        {
            try
            {
                int currentValue = webSocketClient?.InstanceState?.EstimatedSurvivalCount ?? 0;
                string input = ShowInputDialog("推定生存回数", "推定生存回数を入力してください (0-9999):", currentValue.ToString());
                if (input != null && int.TryParse(input, out int newValue))
                {
                    newValue = Math.Max(0, Math.Min(9999, newValue));
                    if (webSocketClient?.InstanceState != null)
                    {
                        webSocketClient.InstanceState.EstimatedSurvivalCount = newValue;
                        
                        // 15以上ならMystic Moonを自動解禁
                        if (newValue >= 15 && !webSocketClient.InstanceState.MysticMoonUnlocked)
                        {
                            webSocketClient.InstanceState.MysticMoonUnlocked = true;
                            var checkMysticMoon = FindControl("checkMysticMoon") as CheckBox;
                            if (checkMysticMoon != null)
                            {
                                checkMysticMoon.Checked = true;
                            }
                            System.Diagnostics.Debug.WriteLine("[InstanceState] Mystic Moon解禁（手動設定で15以上）");
                        }
                        
                        var labelSurvivalValue = FindControl("labelSurvivalValue") as Label;
                        if (labelSurvivalValue != null)
                        {
                            labelSurvivalValue.Text = newValue.ToString();
                        }
                        
                        // 次ラウンド予測を更新
                        UpdateNextRoundPrediction();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"推定生存回数の編集エラー: {ex.Message}");
            }
        }

        private void ButtonResetInstanceState_Click(object sender, EventArgs e)
        {
            if (webSocketClient != null) webSocketClient.ResetInstanceState();
            
            var checkBigBird = FindControl("checkBigBird") as CheckBox;
            var checkJudgementBird = FindControl("checkJudgementBird") as CheckBox;
            var checkPunishingBird = FindControl("checkPunishingBird") as CheckBox;
            var checkBloodMoon = FindControl("checkBloodMoon") as CheckBox;
            var checkTwilight = FindControl("checkTwilight") as CheckBox;
            var checkMysticMoon = FindControl("checkMysticMoon") as CheckBox;
            var checkSolstice = FindControl("checkSolstice") as CheckBox;
            var labelSurvivalValue = FindControl("labelSurvivalValue") as Label;

            if (checkBigBird != null) checkBigBird.Checked = false;
            if (checkJudgementBird != null) checkJudgementBird.Checked = false;
            if (checkPunishingBird != null) checkPunishingBird.Checked = false;
            if (checkBloodMoon != null) checkBloodMoon.Checked = false;
            if (checkTwilight != null) checkTwilight.Checked = false;
            if (checkMysticMoon != null) checkMysticMoon.Checked = false;
            if (checkSolstice != null) checkSolstice.Checked = false;
            if (labelSurvivalValue != null) labelSurvivalValue.Text = "0";
        }
    }
}
