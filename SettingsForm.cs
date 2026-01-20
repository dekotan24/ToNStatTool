using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NAudio.Wave;

namespace ToNStatTool
{
	/// <summary>
	/// 設定フォーム
	/// </summary>
	public class SettingsForm : Form
	{
		private SoundSettings soundSettings;
		private AppSettings appSettings;
		private TabControl tabControl;

		// サウンド設定コントロール
		private CheckBox checkJoinEnabled;
		private TextBox textJoinPath;
		private CheckBox checkLeaveEnabled;
		private TextBox textLeavePath;
		private CheckBox checkWarningEnabled;
		private TextBox textWarningPath;

		// アイテムリマインダー設定コントロール
		private CheckBox checkReminderEnabled;
		private CheckBox checkReminderSoundEnabled;
		private TextBox textReminderSoundPath;
		private NumericUpDown numReminderDuration;
		// リスポーン後リマインダー設定コントロール
		private CheckBox checkRespawnReminderEnabled;

		// テーマ設定コントロール
		private RadioButton radioThemeLight;
		private RadioButton radioThemeDark;

		// その他設定コントロール
		private CheckBox checkVerboseLog;
		private CheckBox checkMasterChangeEnabled;
		private TextBox textMasterChangePath;

		// クラウド設定コントロール
		private CheckBox checkCloudSyncEnabled;
		private TextBox textCloudServerUrl;
		private TextBox textCloudApiKey;

		// 音声再生用
		private IWavePlayer currentPlayer;
		private AudioFileReader currentAudioFile;

		public SettingsForm(SoundSettings settings)
		{
			soundSettings = settings;
			appSettings = AppSettings.Load();
			InitializeComponent();
			LoadSettings();
			ApplyTheme();
		}

		private void InitializeComponent()
		{
			this.Text = "設定";
			this.Size = new Size(480, 620);
			this.StartPosition = FormStartPosition.CenterParent;
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Icon = Properties.Resources.AppIcon;

			// タブコントロール（オーナー描画でダークモード対応）
			tabControl = new TabControl();
			tabControl.Location = new Point(10, 10);
			tabControl.Size = new Size(445, 520);
			tabControl.DrawMode = TabDrawMode.Normal;
			tabControl.DrawItem += TabControl_DrawItem;
			this.Controls.Add(tabControl);

			// サウンド設定タブ
			var tabSound = new TabPage("サウンド設定");
			tabControl.TabPages.Add(tabSound);
			CreateSoundSettingsTab(tabSound);

			// アイテムリマインダータブ
			var tabReminder = new TabPage("アイテムリマインダー");
			tabControl.TabPages.Add(tabReminder);
			CreateReminderSettingsTab(tabReminder);

			// テーマ設定タブ
			var tabTheme = new TabPage("テーマ");
			tabControl.TabPages.Add(tabTheme);
			CreateThemeSettingsTab(tabTheme);

			// その他設定タブ
			var tabOther = new TabPage("その他");
			tabControl.TabPages.Add(tabOther);
			CreateOtherSettingsTab(tabOther);

			// ボタン
			var buttonSave = new Button();
			buttonSave.Text = "保存";
			buttonSave.Location = new Point(280, 540);
			buttonSave.Size = new Size(80, 30);
			buttonSave.Click += ButtonSave_Click;
			this.Controls.Add(buttonSave);

			var buttonCancel = new Button();
			buttonCancel.Text = "キャンセル";
			buttonCancel.Location = new Point(370, 540);
			buttonCancel.Size = new Size(80, 30);
			buttonCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
			this.Controls.Add(buttonCancel);
		}

		/// <summary>
		/// タブのオーナー描画（ダークモード対応）
		/// </summary>
		private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
		{
			TabPage page = tabControl.TabPages[e.Index];
			Rectangle tabBounds = tabControl.GetTabRect(e.Index);

			// 背景色を決定
			Color backColor;
			Color textColor;

			if (ThemeManager.IsDark)
			{
				if (e.Index == tabControl.SelectedIndex)
				{
					backColor = ThemeManager.Dark.FormBackground;
					textColor = ThemeManager.Dark.Text;
				}
				else
				{
					backColor = Color.FromArgb(50, 50, 50);
					textColor = Color.LightGray;
				}
			}
			else
			{
				// ライトモードはデフォルトに近い色
				if (e.Index == tabControl.SelectedIndex)
				{
					backColor = ThemeManager.Light.FormBackground;
					textColor = ThemeManager.Light.Text;
				}
				else
				{
					backColor = Color.FromArgb(240, 240, 240);
					textColor = ThemeManager.Light.Text;
				}
			}

			// 背景を描画
			using (SolidBrush brush = new SolidBrush(backColor))
			{
				e.Graphics.FillRectangle(brush, tabBounds);
			}

			// テキストを描画
			TextRenderer.DrawText(e.Graphics, page.Text, tabControl.Font, tabBounds, textColor,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}

		private void CreateSoundSettingsTab(TabPage tab)
		{
			// Joinサウンド設定
			var groupJoin = new GroupBox();
			groupJoin.Text = "プレイヤー参加時のサウンド";
			groupJoin.Location = new Point(10, 10);
			groupJoin.Size = new Size(415, 100);
			tab.Controls.Add(groupJoin);

			checkJoinEnabled = new CheckBox();
			checkJoinEnabled.Text = "有効";
			checkJoinEnabled.Location = new Point(10, 25);
			checkJoinEnabled.Size = new Size(60, 20);
			groupJoin.Controls.Add(checkJoinEnabled);

			textJoinPath = new TextBox();
			textJoinPath.Location = new Point(75, 23);
			textJoinPath.Size = new Size(220, 23);
			groupJoin.Controls.Add(textJoinPath);

			var buttonJoinBrowse = new Button();
			buttonJoinBrowse.Text = "参照...";
			buttonJoinBrowse.Location = new Point(300, 22);
			buttonJoinBrowse.Size = new Size(55, 25);
			buttonJoinBrowse.Click += (s, e) => BrowseSoundFile(textJoinPath);
			groupJoin.Controls.Add(buttonJoinBrowse);

			var buttonJoinTest = new Button();
			buttonJoinTest.Text = "▶";
			buttonJoinTest.Location = new Point(360, 22);
			buttonJoinTest.Size = new Size(40, 25);
			buttonJoinTest.Click += (s, e) => TestSound(textJoinPath.Text, "player_join.mp3");
			groupJoin.Controls.Add(buttonJoinTest);

			var labelJoinNote = new Label();
			labelJoinNote.Text = "※ 空の場合はplayer_join.mp3を使用";
			labelJoinNote.Location = new Point(75, 50);
			labelJoinNote.Size = new Size(300, 20);
			labelJoinNote.ForeColor = Color.Gray;
			groupJoin.Controls.Add(labelJoinNote);

			var labelJoinNote2 = new Label();
			labelJoinNote2.Text = "※ MP3またはWAVファイルを指定してください";
			labelJoinNote2.Location = new Point(75, 68);
			labelJoinNote2.Size = new Size(300, 20);
			labelJoinNote2.ForeColor = Color.Gray;
			groupJoin.Controls.Add(labelJoinNote2);

			// Leaveサウンド設定
			var groupLeave = new GroupBox();
			groupLeave.Text = "プレイヤー退出時のサウンド";
			groupLeave.Location = new Point(10, 120);
			groupLeave.Size = new Size(415, 100);
			tab.Controls.Add(groupLeave);

			checkLeaveEnabled = new CheckBox();
			checkLeaveEnabled.Text = "有効";
			checkLeaveEnabled.Location = new Point(10, 25);
			checkLeaveEnabled.Size = new Size(60, 20);
			groupLeave.Controls.Add(checkLeaveEnabled);

			textLeavePath = new TextBox();
			textLeavePath.Location = new Point(75, 23);
			textLeavePath.Size = new Size(220, 23);
			groupLeave.Controls.Add(textLeavePath);

			var buttonLeaveBrowse = new Button();
			buttonLeaveBrowse.Text = "参照...";
			buttonLeaveBrowse.Location = new Point(300, 22);
			buttonLeaveBrowse.Size = new Size(55, 25);
			buttonLeaveBrowse.Click += (s, e) => BrowseSoundFile(textLeavePath);
			groupLeave.Controls.Add(buttonLeaveBrowse);

			var buttonLeaveTest = new Button();
			buttonLeaveTest.Text = "▶";
			buttonLeaveTest.Location = new Point(360, 22);
			buttonLeaveTest.Size = new Size(40, 25);
			buttonLeaveTest.Click += (s, e) => TestSound(textLeavePath.Text, "player_leave.mp3");
			groupLeave.Controls.Add(buttonLeaveTest);

			var labelLeaveNote = new Label();
			labelLeaveNote.Text = "※ 空の場合はplayer_leave.mp3を使用";
			labelLeaveNote.Location = new Point(75, 50);
			labelLeaveNote.Size = new Size(300, 20);
			labelLeaveNote.ForeColor = Color.Gray;
			groupLeave.Controls.Add(labelLeaveNote);

			var labelLeaveNote2 = new Label();
			labelLeaveNote2.Text = "※ MP3またはWAVファイルを指定してください";
			labelLeaveNote2.Location = new Point(75, 68);
			labelLeaveNote2.Size = new Size(300, 20);
			labelLeaveNote2.ForeColor = Color.Gray;
			groupLeave.Controls.Add(labelLeaveNote2);

			// 警告ユーザー参加時サウンド設定
			var groupWarning = new GroupBox();
			groupWarning.Text = "⚠ 警告ユーザー参加時のサウンド";
			groupWarning.Location = new Point(10, 230);
			groupWarning.Size = new Size(415, 100);
			tab.Controls.Add(groupWarning);

			checkWarningEnabled = new CheckBox();
			checkWarningEnabled.Text = "有効";
			checkWarningEnabled.Location = new Point(10, 25);
			checkWarningEnabled.Size = new Size(60, 20);
			groupWarning.Controls.Add(checkWarningEnabled);

			textWarningPath = new TextBox();
			textWarningPath.Location = new Point(75, 23);
			textWarningPath.Size = new Size(220, 23);
			groupWarning.Controls.Add(textWarningPath);

			var buttonWarningBrowse = new Button();
			buttonWarningBrowse.Text = "参照...";
			buttonWarningBrowse.Location = new Point(300, 22);
			buttonWarningBrowse.Size = new Size(55, 25);
			buttonWarningBrowse.Click += (s, e) => BrowseSoundFile(textWarningPath);
			groupWarning.Controls.Add(buttonWarningBrowse);

			var buttonWarningTest = new Button();
			buttonWarningTest.Text = "▶";
			buttonWarningTest.Location = new Point(360, 22);
			buttonWarningTest.Size = new Size(40, 25);
			buttonWarningTest.Click += (s, e) => TestSound(textWarningPath.Text, "warning.mp3");
			groupWarning.Controls.Add(buttonWarningTest);

			var labelWarningNote = new Label();
			labelWarningNote.Text = "※ 空の場合はwarning.mp3またはシステム音を使用";
			labelWarningNote.Location = new Point(75, 50);
			labelWarningNote.Size = new Size(330, 20);
			labelWarningNote.ForeColor = Color.Gray;
			groupWarning.Controls.Add(labelWarningNote);

			var labelWarningNote2 = new Label();
			labelWarningNote2.Text = "※ MP3またはWAVファイルを指定してください";
			labelWarningNote2.Location = new Point(75, 68);
			labelWarningNote2.Size = new Size(300, 20);
			labelWarningNote2.ForeColor = Color.Gray;
			groupWarning.Controls.Add(labelWarningNote2);

			// マスター変更音設定
			var groupMasterChange = new GroupBox();
			groupMasterChange.Text = "マスター変更時のサウンド";
			groupMasterChange.Location = new Point(10, 340);
			groupMasterChange.Size = new Size(415, 100);
			tab.Controls.Add(groupMasterChange);

			checkMasterChangeEnabled = new CheckBox();
			checkMasterChangeEnabled.Text = "有効";
			checkMasterChangeEnabled.Location = new Point(10, 25);
			checkMasterChangeEnabled.Size = new Size(60, 20);
			groupMasterChange.Controls.Add(checkMasterChangeEnabled);

			textMasterChangePath = new TextBox();
			textMasterChangePath.Location = new Point(75, 23);
			textMasterChangePath.Size = new Size(220, 23);
			groupMasterChange.Controls.Add(textMasterChangePath);

			var buttonMasterChangeBrowse = new Button();
			buttonMasterChangeBrowse.Text = "参照...";
			buttonMasterChangeBrowse.Location = new Point(300, 22);
			buttonMasterChangeBrowse.Size = new Size(55, 25);
			buttonMasterChangeBrowse.Click += (s, e) => BrowseSoundFile(textMasterChangePath);
			groupMasterChange.Controls.Add(buttonMasterChangeBrowse);

			var buttonMasterChangeTest = new Button();
			buttonMasterChangeTest.Text = "▶";
			buttonMasterChangeTest.Location = new Point(360, 22);
			buttonMasterChangeTest.Size = new Size(40, 25);
			buttonMasterChangeTest.Click += (s, e) => TestSound(textMasterChangePath.Text, "masterchange.mp3");
			groupMasterChange.Controls.Add(buttonMasterChangeTest);

			var labelMasterChangeNote = new Label();
			labelMasterChangeNote.Text = "※ 空の場合はmasterchange.mp3を使用";
			labelMasterChangeNote.Location = new Point(75, 50);
			labelMasterChangeNote.Size = new Size(300, 20);
			labelMasterChangeNote.ForeColor = Color.Gray;
			groupMasterChange.Controls.Add(labelMasterChangeNote);

			var labelMasterChangeNote2 = new Label();
			labelMasterChangeNote2.Text = "※ MP3またはWAVファイルを指定してください";
			labelMasterChangeNote2.Location = new Point(75, 68);
			labelMasterChangeNote2.Size = new Size(300, 20);
			labelMasterChangeNote2.ForeColor = Color.Gray;
			groupMasterChange.Controls.Add(labelMasterChangeNote2);
		}

		private void CreateReminderSettingsTab(TabPage tab)
		{
			// リマインダー設定グループ
			var groupReminder = new GroupBox();
			groupReminder.Text = "8ページ / アンバウンド終了時のリマインダー";
			groupReminder.Location = new Point(10, 10);
			groupReminder.Size = new Size(415, 220);
			tab.Controls.Add(groupReminder);

			// 有効/無効
			checkReminderEnabled = new CheckBox();
			checkReminderEnabled.Text = "リマインダーを有効にする";
			checkReminderEnabled.Location = new Point(15, 30);
			checkReminderEnabled.Size = new Size(200, 20);
			checkReminderEnabled.CheckedChanged += (s, e) => UpdateReminderControlsState();
			groupReminder.Controls.Add(checkReminderEnabled);

			// 説明ラベル
			var labelDescription = new Label();
			labelDescription.Text = "8ページ・アンバウンド終了後、テラー表示ウィンドウに\n「アイテムを持ち直してください」と表示します。\nサボタージュでキラー側になった時も通知します。";
			labelDescription.Location = new Point(15, 55);
			labelDescription.Size = new Size(380, 50);
			labelDescription.ForeColor = Color.Gray;
			groupReminder.Controls.Add(labelDescription);

			// サウンド設定
			checkReminderSoundEnabled = new CheckBox();
			checkReminderSoundEnabled.Text = "通知音を鳴らす";
			checkReminderSoundEnabled.Location = new Point(15, 110);
			checkReminderSoundEnabled.Size = new Size(120, 20);
			groupReminder.Controls.Add(checkReminderSoundEnabled);

			textReminderSoundPath = new TextBox();
			textReminderSoundPath.Location = new Point(140, 108);
			textReminderSoundPath.Size = new Size(150, 23);
			groupReminder.Controls.Add(textReminderSoundPath);

			var buttonReminderBrowse = new Button();
			buttonReminderBrowse.Text = "参照...";
			buttonReminderBrowse.Location = new Point(295, 107);
			buttonReminderBrowse.Size = new Size(55, 25);
			buttonReminderBrowse.Click += (s, e) => BrowseSoundFile(textReminderSoundPath);
			groupReminder.Controls.Add(buttonReminderBrowse);

			var buttonReminderTest = new Button();
			buttonReminderTest.Text = "▶";
			buttonReminderTest.Location = new Point(355, 107);
			buttonReminderTest.Size = new Size(40, 25);
			buttonReminderTest.Click += (s, e) => TestSound(textReminderSoundPath.Text, "item.mp3");
			groupReminder.Controls.Add(buttonReminderTest);

			var labelSoundNote = new Label();
			labelSoundNote.Text = "※ 空の場合はitem.mp3を使用";
			labelSoundNote.Location = new Point(140, 133);
			labelSoundNote.Size = new Size(250, 20);
			labelSoundNote.ForeColor = Color.Gray;
			groupReminder.Controls.Add(labelSoundNote);

			// 表示時間
			var labelDuration = new Label();
			labelDuration.Text = "表示時間:";
			labelDuration.Location = new Point(15, 163);
			labelDuration.Size = new Size(60, 20);
			groupReminder.Controls.Add(labelDuration);

			numReminderDuration = new NumericUpDown();
			numReminderDuration.Location = new Point(80, 160);
			numReminderDuration.Size = new Size(60, 23);
			numReminderDuration.Minimum = 1;
			numReminderDuration.Maximum = 10;
			numReminderDuration.Value = 10;
			groupReminder.Controls.Add(numReminderDuration);

			var labelSeconds = new Label();
			labelSeconds.Text = "秒";
			labelSeconds.Location = new Point(145, 163);
			labelSeconds.Size = new Size(30, 20);
			groupReminder.Controls.Add(labelSeconds);

			// リスポーン後リマインダー設定グループ
			var groupRespawn = new GroupBox();
			groupRespawn.Text = "リスポーン後の再参加時のリマインダー";
			groupRespawn.Location = new Point(10, 240);
			groupRespawn.Size = new Size(415, 100);
			tab.Controls.Add(groupRespawn);

			// 有効/無効
			checkRespawnReminderEnabled = new CheckBox();
			checkRespawnReminderEnabled.Text = "リマインダーを有効にする";
			checkRespawnReminderEnabled.Location = new Point(15, 30);
			checkRespawnReminderEnabled.Size = new Size(200, 20);
			groupRespawn.Controls.Add(checkRespawnReminderEnabled);

			// 説明ラベル
			var labelRespawnDescription = new Label();
			labelRespawnDescription.Text = "リスポーン後にゲームへ再参加した際に\n「アイテムを持ち直してください」と表示します。\n※ 通知音は上記の設定を共有します";
			labelRespawnDescription.Location = new Point(15, 55);
			labelRespawnDescription.Size = new Size(380, 40);
			labelRespawnDescription.ForeColor = Color.Gray;
			groupRespawn.Controls.Add(labelRespawnDescription);
		}

		private void CreateThemeSettingsTab(TabPage tab)
		{
			// テーマ設定グループ
			var groupTheme = new GroupBox();
			groupTheme.Text = "外観テーマ";
			groupTheme.Location = new Point(10, 10);
			groupTheme.Size = new Size(415, 120);
			tab.Controls.Add(groupTheme);

			// ライトモード
			radioThemeLight = new RadioButton();
			radioThemeLight.Text = "☀ ライトモード";
			radioThemeLight.Location = new Point(20, 35);
			radioThemeLight.Size = new Size(150, 25);
			radioThemeLight.Font = new Font("Meiryo UI", 10);
			groupTheme.Controls.Add(radioThemeLight);

			// ダークモード
			radioThemeDark = new RadioButton();
			radioThemeDark.Text = "🌙 ダークモード";
			radioThemeDark.Location = new Point(20, 70);
			radioThemeDark.Size = new Size(150, 25);
			radioThemeDark.Font = new Font("Meiryo UI", 10);
			groupTheme.Controls.Add(radioThemeDark);

			// 説明ラベル
			var labelThemeNote = new Label();
			labelThemeNote.Text = "※ テーマ変更は保存後に反映されます";
			labelThemeNote.Location = new Point(180, 50);
			labelThemeNote.Size = new Size(220, 20);
			labelThemeNote.ForeColor = Color.Gray;
			groupTheme.Controls.Add(labelThemeNote);
		}

		private void CreateOtherSettingsTab(TabPage tab)
		{
			// ログ設定グループ
			var groupLog = new GroupBox();
			groupLog.Text = "ログ設定";
			groupLog.Location = new Point(10, 10);
			groupLog.Size = new Size(415, 90);
			tab.Controls.Add(groupLog);

			// 詳細ログ
			checkVerboseLog = new CheckBox();
			checkVerboseLog.Text = "詳細ログを有効にする";
			checkVerboseLog.Location = new Point(15, 25);
			checkVerboseLog.Size = new Size(200, 20);
			groupLog.Controls.Add(checkVerboseLog);

			// ログフォルダを開くボタン
			var buttonOpenLogFolder = new Button();
			buttonOpenLogFolder.Text = "ログフォルダを開く";
			buttonOpenLogFolder.Location = new Point(15, 52);
			buttonOpenLogFolder.Size = new Size(130, 28);
			buttonOpenLogFolder.Click += (s, e) => Logger.OpenLogFolder();
			groupLog.Controls.Add(buttonOpenLogFolder);

			// クラウド同期設定グループ
			var groupCloud = new GroupBox();
			groupCloud.Text = "クラウド同期";
			groupCloud.Location = new Point(10, 110);
			groupCloud.Size = new Size(415, 180);
			tab.Controls.Add(groupCloud);

			// クラウド同期有効/無効
			checkCloudSyncEnabled = new CheckBox();
			checkCloudSyncEnabled.Text = "ラウンド情報をクラウドに送信する";
			checkCloudSyncEnabled.Location = new Point(15, 25);
			checkCloudSyncEnabled.Size = new Size(250, 20);
			checkCloudSyncEnabled.CheckedChanged += (s, e) => UpdateCloudControlsState();
			groupCloud.Controls.Add(checkCloudSyncEnabled);

			// 説明ラベル
			var labelCloudDescription = new Label();
			labelCloudDescription.Text = "ラウンド終了時にテラー・マップ・生存人数などの情報を\nクラウドサーバーに送信します。\n使用にはAPIキーを発行する必要があります。";
			labelCloudDescription.Location = new Point(15, 50);
			labelCloudDescription.Size = new Size(380, 50);
			labelCloudDescription.ForeColor = Color.Gray;
			groupCloud.Controls.Add(labelCloudDescription);

			// サーバーURL
			var labelServerUrl = new Label();
			labelServerUrl.Text = "サーバーURL:";
			labelServerUrl.Location = new Point(15, 108);
			labelServerUrl.Size = new Size(80, 20);
			groupCloud.Controls.Add(labelServerUrl);

			textCloudServerUrl = new TextBox();
			textCloudServerUrl.Location = new Point(100, 105);
			textCloudServerUrl.Size = new Size(295, 23);
			groupCloud.Controls.Add(textCloudServerUrl);

			// APIキー
			var labelApiKey = new Label();
			labelApiKey.Text = "APIキー:";
			labelApiKey.Location = new Point(15, 138);
			labelApiKey.Size = new Size(80, 20);
			groupCloud.Controls.Add(labelApiKey);

			textCloudApiKey = new TextBox();
			textCloudApiKey.Location = new Point(100, 135);
			textCloudApiKey.Size = new Size(295, 23);
			textCloudApiKey.UseSystemPasswordChar = true;  // APIキーを隠す
			groupCloud.Controls.Add(textCloudApiKey);
		}

		private void BrowseSoundFile(TextBox targetTextBox)
		{
			using (var ofd = new OpenFileDialog())
			{
				ofd.Filter = "音声ファイル|*.mp3;*.wav|MP3ファイル|*.mp3|WAVファイル|*.wav|すべてのファイル|*.*";
				if (ofd.ShowDialog() == DialogResult.OK)
				{
					targetTextBox.Text = ofd.FileName;
				}
			}
		}

		/// <summary>
		/// サウンドをテスト再生
		/// </summary>
		private void TestSound(string customPath, string defaultFileName)
		{
			try
			{
				// 既存の再生を停止
				StopCurrentPlayback();

				string soundPath = customPath;

				// カスタムパスが空または存在しない場合はデフォルトファイルを使用
				if (string.IsNullOrEmpty(soundPath) || !File.Exists(soundPath))
				{
					if (!string.IsNullOrEmpty(defaultFileName))
					{
						soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, defaultFileName);
					}
				}

				if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
				{
					currentAudioFile = new AudioFileReader(soundPath);
					currentPlayer = new WaveOutEvent();
					currentPlayer.Init(currentAudioFile);
					currentPlayer.PlaybackStopped += (s, e) => StopCurrentPlayback();
					currentPlayer.Play();
				}
				else
				{
					// ファイルがない場合はシステム音
					System.Media.SystemSounds.Asterisk.Play();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"サウンド再生エラー: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		/// <summary>
		/// 現在の再生を停止
		/// </summary>
		private void StopCurrentPlayback()
		{
			try
			{
				if (currentPlayer != null)
				{
					currentPlayer.Stop();
					currentPlayer.Dispose();
					currentPlayer = null;
				}
				if (currentAudioFile != null)
				{
					currentAudioFile.Dispose();
					currentAudioFile = null;
				}
			}
			catch { }
		}

		private void LoadSettings()
		{
			// サウンド設定
			checkJoinEnabled.Checked = soundSettings.EnableJoinSound;
			textJoinPath.Text = soundSettings.JoinSoundPath;
			checkLeaveEnabled.Checked = soundSettings.EnableLeaveSound;
			textLeavePath.Text = soundSettings.LeaveSoundPath;
			checkWarningEnabled.Checked = soundSettings.EnableWarningUserSound;
			textWarningPath.Text = soundSettings.WarningUserSoundPath;

			// リマインダー設定
			checkReminderEnabled.Checked = soundSettings.EnableItemReminder;
			checkReminderSoundEnabled.Checked = soundSettings.EnableItemReminderSound;
			textReminderSoundPath.Text = soundSettings.ItemReminderSoundPath;
			numReminderDuration.Value = Math.Max(1, Math.Min(10, soundSettings.ItemReminderDurationSeconds));
			
			// リスポーン後リマインダー設定
			checkRespawnReminderEnabled.Checked = soundSettings.EnableRespawnReminder;

			// マスター変更音設定（SoundSettingsから読み込み）
			checkMasterChangeEnabled.Checked = soundSettings.EnableMasterChangeSound;
			textMasterChangePath.Text = soundSettings.MasterChangeSoundPath;

			// テーマ設定
			if (ThemeManager.IsDark)
			{
				radioThemeDark.Checked = true;
			}
			else
			{
				radioThemeLight.Checked = true;
			}

			// 詳細ログ設定（AppSettingsから読み込み）
			checkVerboseLog.Checked = appSettings.EnableVerboseLog;

			// クラウド設定（AppSettingsから読み込み）
			checkCloudSyncEnabled.Checked = appSettings.EnableCloudSync;
			textCloudServerUrl.Text = appSettings.CloudServerUrl;
			textCloudApiKey.Text = appSettings.CloudApiKey;

			UpdateReminderControlsState();
			UpdateCloudControlsState();
		}

		private void UpdateReminderControlsState()
		{
			bool enabled = checkReminderEnabled.Checked;
			checkReminderSoundEnabled.Enabled = enabled;
			textReminderSoundPath.Enabled = enabled;
			numReminderDuration.Enabled = enabled;
		}

		private void UpdateCloudControlsState()
		{
			bool enabled = checkCloudSyncEnabled.Checked;
			textCloudServerUrl.Enabled = enabled;
			textCloudApiKey.Enabled = enabled;
		}

		private void ButtonSave_Click(object sender, EventArgs e)
		{
			// サウンド設定を更新
			soundSettings.EnableJoinSound = checkJoinEnabled.Checked;
			soundSettings.JoinSoundPath = textJoinPath.Text;
			soundSettings.EnableLeaveSound = checkLeaveEnabled.Checked;
			soundSettings.LeaveSoundPath = textLeavePath.Text;
			soundSettings.EnableWarningUserSound = checkWarningEnabled.Checked;
			soundSettings.WarningUserSoundPath = textWarningPath.Text;

			// リマインダー設定を更新
			soundSettings.EnableItemReminder = checkReminderEnabled.Checked;
			soundSettings.EnableItemReminderSound = checkReminderSoundEnabled.Checked;
			soundSettings.ItemReminderSoundPath = textReminderSoundPath.Text;
			soundSettings.ItemReminderDurationSeconds = (int)numReminderDuration.Value;
			
			// リスポーン後リマインダー設定を更新
			soundSettings.EnableRespawnReminder = checkRespawnReminderEnabled.Checked;

			// マスター変更音設定を更新（SoundSettingsに保存）
			soundSettings.EnableMasterChangeSound = checkMasterChangeEnabled.Checked;
			soundSettings.MasterChangeSoundPath = textMasterChangePath.Text;

			// テーマ設定を更新
			ThemeChanged = (radioThemeDark.Checked && !ThemeManager.IsDark) || 
			               (radioThemeLight.Checked && ThemeManager.IsDark);
			NewThemeIsDark = radioThemeDark.Checked;

			// 詳細ログ設定を更新（AppSettingsに保存）
			appSettings.EnableVerboseLog = checkVerboseLog.Checked;

			// クラウド設定を更新（AppSettingsに保存）
			appSettings.EnableCloudSync = checkCloudSyncEnabled.Checked;
			appSettings.CloudServerUrl = textCloudServerUrl.Text;
			appSettings.CloudApiKey = textCloudApiKey.Text;

			appSettings.Save();
			VerboseLogEnabled = checkVerboseLog.Checked;
			CloudSyncEnabled = checkCloudSyncEnabled.Checked;
			CloudServerUrl = textCloudServerUrl.Text;
			CloudApiKey = textCloudApiKey.Text;

			// 再生中のサウンドを停止
			StopCurrentPlayback();

			this.DialogResult = DialogResult.OK;
		}

		/// <summary>
		/// テーマが変更されたかどうか
		/// </summary>
		public bool ThemeChanged { get; private set; } = false;

		/// <summary>
		/// 新しいテーマがダークかどうか
		/// </summary>
		public bool NewThemeIsDark { get; private set; } = false;

		/// <summary>
		/// 詳細ログが有効かどうか
		/// </summary>
		public bool VerboseLogEnabled { get; private set; } = false;

		/// <summary>
		/// クラウド同期が有効かどうか
		/// </summary>
		public bool CloudSyncEnabled { get; private set; } = false;

		/// <summary>
		/// クラウドサーバーのURL
		/// </summary>
		public string CloudServerUrl { get; private set; } = "";

		/// <summary>
		/// クラウドAPIキー
		/// </summary>
		public string CloudApiKey { get; private set; } = "";

		/// <summary>
		/// フォームを閉じる際に再生を停止
		/// </summary>
		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			StopCurrentPlayback();
			base.OnFormClosing(e);
		}

		/// <summary>
		/// テーマを適用
		/// </summary>
		public void ApplyTheme()
		{
			this.BackColor = ThemeManager.IsDark 
				? ThemeManager.Dark.FormBackground 
				: ThemeManager.Light.FormBackground;

			this.ForeColor = ThemeManager.IsDark
				? ThemeManager.Dark.Text
				: ThemeManager.Light.Text;

			// タブコントロールとその子要素にテーマを適用
			ApplyThemeToControls(this.Controls);
		}

		private void ApplyThemeToControls(Control.ControlCollection controls)
		{
			foreach (Control control in controls)
			{
				if (control is GroupBox groupBox)
				{
					groupBox.ForeColor = ThemeManager.IsDark
						? ThemeManager.Dark.Text
						: ThemeManager.Light.Text;
				}
				else if (control is TextBox textBox)
				{
					textBox.BackColor = ThemeManager.IsDark
						? ThemeManager.Dark.TextBoxBackground
						: SystemColors.Window;
					textBox.ForeColor = ThemeManager.IsDark
						? ThemeManager.Dark.Text
						: ThemeManager.Light.Text;
				}
				else if (control is Button button)
				{
					button.BackColor = ThemeManager.IsDark
						? ThemeManager.Dark.ButtonBackground
						: SystemColors.Control;
					button.ForeColor = ThemeManager.IsDark
						? ThemeManager.Dark.Text
						: ThemeManager.Light.Text;
					button.FlatStyle = ThemeManager.IsDark ? FlatStyle.Flat : FlatStyle.Standard;
				}
				else if (control is TabControl tabControl)
				{
					foreach (TabPage page in tabControl.TabPages)
					{
						page.BackColor = ThemeManager.IsDark
							? ThemeManager.Dark.FormBackground
							: ThemeManager.Light.FormBackground;
						ApplyThemeToControls(page.Controls);
					}
				}
				else if (control is NumericUpDown numericUpDown)
				{
					numericUpDown.BackColor = ThemeManager.IsDark
						? ThemeManager.Dark.TextBoxBackground
						: SystemColors.Window;
					numericUpDown.ForeColor = ThemeManager.IsDark
						? ThemeManager.Dark.Text
						: ThemeManager.Light.Text;
				}

				// 子コントロールにも適用
				if (control.Controls.Count > 0)
				{
					ApplyThemeToControls(control.Controls);
				}
			}
		}
	}
}
