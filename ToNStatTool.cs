using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ToNStatTool.Controls;
using ToNStatTool.Helpers;
using ToNStatTool.Services;

namespace ToNStatTool
{
    /// <summary>
    /// ToN Stat Tool のメインフォーム
    /// </summary>
    public partial class ToNStatTool : Form
    {
        private WebSocketClient webSocketClient;
        private TerrorDisplayForm terrorDisplayForm;
        private SessionStatsForm sessionStatsForm;
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

        // コントロールキャッシュ（FindControl高速化用）
        private readonly Dictionary<string, Control> controlCache = new Dictionary<string, Control>();

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
            
            // 保存された詳細ログ設定を復元
            if (appSettings.EnableVerboseLog)
            {
                Logger.EnableVerboseLogging();
            }

            // XSOverlay通知設定を反映
            XSOverlayNotifier.ApplySettings(appSettings);
            
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

            // フォーム表示後に自動接続を試行
            this.Shown += async (s, e) => await TryAutoConnectAsync();
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
            webSocketClient.OnItemReminderRoundEnd += OnItemReminderRoundEnd;
            webSocketClient.OnMasterChanged += OnMasterChanged;
            webSocketClient.OnSaveCodeReceived += OnSaveCodeReceived;
            webSocketClient.OnCloudSyncStateChanged += OnCloudSyncStateChanged;
        }

        private void InitializeTimer()
        {
            uiUpdateTimer = new System.Windows.Forms.Timer();
            uiUpdateTimer.Interval = 5000;
            uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            uiUpdateTimer.Start();

            elapsedTimeTimer = new System.Windows.Forms.Timer();
            elapsedTimeTimer.Interval = 1000;
            elapsedTimeTimer.Tick += ElapsedTimeTimer_Tick;
        }

        /// <summary>
        /// 設定ダイアログを表示
        /// </summary>
        private void ShowSoundSettingsDialog()
        {
            using (var settingsForm = new SettingsForm(webSocketClient.SoundSettings))
            {
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    webSocketClient.SaveSoundSettings();

                    appSettings = AppSettings.Load();

                    if (settingsForm.ThemeChanged)
                    {
                        var newTheme = settingsForm.NewThemeIsDark ? AppTheme.Dark : AppTheme.Light;
                        ThemeManager.SetTheme(newTheme);
                    }

                    if (settingsForm.VerboseLogEnabled)
                    {
                        Logger.EnableVerboseLogging();
                    }
                    else
                    {
                        Logger.DisableVerboseLogging();
                    }

                    // クラウド設定を反映
                    webSocketClient.UpdateCloudSettings(
                        settingsForm.CloudSyncEnabled,
                        settingsForm.CloudServerUrl,
                        settingsForm.CloudApiKey);

                    // XSOverlay通知設定を反映
                    XSOverlayNotifier.ApplySettings(appSettings);
                }
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
                dialog.Controls.Add(noteLabel);

                // ダイアログにテーマを適用
                ThemeManager.Apply(dialog);

                // 注釈ラベルはテーマ適用後に控えめな色に設定
                noteLabel.ForeColor = ThemeManager.IsDark ? Color.FromArgb(128, 128, 128) : Color.Gray;

                dialog.ShowDialog(this);
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
        /// アイテムリマインダー音を再生
        /// </summary>
        private void PlayItemReminderSound()
        {
            Task.Run(() =>
            {
                try
                {
                    string soundPath = webSocketClient.SoundSettings.ItemReminderSoundPath;
                    
                    if (string.IsNullOrEmpty(soundPath))
                    {
                        soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "item.mp3");
                    }
                    
                    if (File.Exists(soundPath))
                    {
                        using (var audioFile = new NAudio.Wave.AudioFileReader(soundPath))
                        using (var outputDevice = new NAudio.Wave.WaveOutEvent())
                        {
                            outputDevice.Init(audioFile);
                            outputDevice.Play();
                            while (outputDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                            {
                                Thread.Sleep(100);
                            }
                        }
                    }
                    else
                    {
                        System.Media.SystemSounds.Asterisk.Play();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[REMINDER] サウンド再生エラー: {ex.Message}");
                    try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                }
            });
        }

        /// <summary>
        /// 指定された名前のコントロールを検索（キャッシュ使用）
        /// </summary>
        private Control FindControl(string name)
        {
            // キャッシュにあればそれを返す
            if (controlCache.TryGetValue(name, out Control cached))
            {
                return cached;
            }

            // キャッシュにない場合は検索してキャッシュに追加
            var control = ControlFinder.FindControlRecursive(this, name);
            if (control != null)
            {
                controlCache[name] = control;
            }
            return control;
        }

        /// <summary>
        /// JSONを整形して表示
        /// </summary>
        private string FormatJson(string json)
        {
            return JsonHelper.FormatJson(json);
        }

        /// <summary>
        /// ゲームデータから値を取得
        /// </summary>
        private string GetGameDataValue(Dictionary<string, object> gameData, string key, string defaultValue)
        {
            if (gameData.ContainsKey(key))
            {
                return gameData[key]?.ToString() ?? defaultValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 設定を保存する
        /// </summary>
        private void SaveSettings()
        {
            try
            {
                var currentSettings = AppSettings.Load();
                
                currentSettings.SetTheme(ThemeManager.CurrentTheme);
                
                var trackBar = FindControl("trackBarOpacity") as TrackBar;
                if (trackBar != null)
                {
                    currentSettings.TerrorFormOpacity = trackBar.Value;
                }
                
                currentSettings.WebSocketUrl = textBoxUrl.Text;
                
                currentSettings.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// アプリ起動時にWebSocketサーバーへの自動接続を試行
        /// </summary>
        private async Task TryAutoConnectAsync()
        {
            string serverUrl = textBoxUrl.Text.Trim();
            if (string.IsNullOrEmpty(serverUrl) || webSocketClient.IsConnected)
                return;

            Logger.Info("AutoConnect", "自動接続を試行");
            labelStatus.Text = "自動接続中...";
            labelStatus.ForeColor = Color.Orange;
            buttonConnect.Enabled = false;

            await webSocketClient.ConnectAsync(serverUrl);

            // 接続失敗時はボタンを再有効化（OnError/OnDisconnectedで処理されるが念のため）
            if (!webSocketClient.IsConnected)
            {
                buttonConnect.Enabled = true;
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
            SaveSettings();
            
            webSocketClient?.DisconnectAsync().Wait();

            uiUpdateTimer?.Stop();
            uiUpdateTimer?.Dispose();

            elapsedTimeTimer?.Stop();
            elapsedTimeTimer?.Dispose();

            foreach (var control in terrorControls)
            {
                control.Dispose();
            }

            TerrorImageManager.ClearCache();

            if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
            {
                terrorDisplayForm.Close();
            }

            base.OnFormClosing(e);
        }
    }
}
