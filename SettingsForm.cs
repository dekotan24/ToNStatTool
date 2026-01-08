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

		// テーマ設定コントロール
		private RadioButton radioThemeLight;
		private RadioButton radioThemeDark;

		// その他設定コントロール
		private CheckBox checkVerboseLog;

		// 音声再生用
		private IWavePlayer currentPlayer;
		private AudioFileReader currentAudioFile;

		public SettingsForm(SoundSettings settings)
		{
			soundSettings = settings;
			InitializeComponent();
			LoadSettings();
			ApplyTheme();
		}

		private void InitializeComponent()
		{
			this.Text = "設定";
			this.Size = new Size(480, 520);
			this.StartPosition = FormStartPosition.CenterParent;
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Icon = Properties.Resources.AppIcon;

			// タブコントロール（オーナー描画でダークモード対応）
			tabControl = new TabControl();
			tabControl.Location = new Point(10, 10);
			tabControl.Size = new Size(445, 420);
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
			buttonSave.Location = new Point(280, 440);
			buttonSave.Size = new Size(80, 30);
			buttonSave.Click += ButtonSave_Click;
			this.Controls.Add(buttonSave);

			var buttonCancel = new Button();
			buttonCancel.Text = "キャンセル";
			buttonCancel.Location = new Point(370, 440);
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
			labelWarningNote.ForeColor = Color.OrangeRed;
			groupWarning.Controls.Add(labelWarningNote);

			var labelWarningNote2 = new Label();
			labelWarningNote2.Text = "※ MP3またはWAVファイルを指定してください";
			labelWarningNote2.Location = new Point(75, 68);
			labelWarningNote2.Size = new Size(300, 20);
			labelWarningNote2.ForeColor = Color.Gray;
			groupWarning.Controls.Add(labelWarningNote2);
		}

		private void CreateReminderSettingsTab(TabPage tab)
		{
			// リマインダー設定グループ
			var groupReminder = new GroupBox();
			groupReminder.Text = "8ページ / アンバウンド終了時のリマインダー";
			groupReminder.Location = new Point(10, 10);
			groupReminder.Size = new Size(415, 200);
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
			labelDescription.Text = "8ページ・アンバウンド終了後、テラー表示フォームに\n「アイテムを持ち直してください」と表示します。";
			labelDescription.Location = new Point(15, 55);
			labelDescription.Size = new Size(380, 35);
			labelDescription.ForeColor = Color.Gray;
			groupReminder.Controls.Add(labelDescription);

			// サウンド設定
			checkReminderSoundEnabled = new CheckBox();
			checkReminderSoundEnabled.Text = "通知音を鳴らす";
			checkReminderSoundEnabled.Location = new Point(15, 95);
			checkReminderSoundEnabled.Size = new Size(120, 20);
			groupReminder.Controls.Add(checkReminderSoundEnabled);

			textReminderSoundPath = new TextBox();
			textReminderSoundPath.Location = new Point(140, 93);
			textReminderSoundPath.Size = new Size(150, 23);
			groupReminder.Controls.Add(textReminderSoundPath);

			var buttonReminderBrowse = new Button();
			buttonReminderBrowse.Text = "参照...";
			buttonReminderBrowse.Location = new Point(295, 92);
			buttonReminderBrowse.Size = new Size(55, 25);
			buttonReminderBrowse.Click += (s, e) => BrowseSoundFile(textReminderSoundPath);
			groupReminder.Controls.Add(buttonReminderBrowse);

			var buttonReminderTest = new Button();
			buttonReminderTest.Text = "▶";
			buttonReminderTest.Location = new Point(355, 92);
			buttonReminderTest.Size = new Size(40, 25);
			buttonReminderTest.Click += (s, e) => TestSound(textReminderSoundPath.Text, null);
			groupReminder.Controls.Add(buttonReminderTest);

			var labelSoundNote = new Label();
			labelSoundNote.Text = "※ 空の場合はシステム音を使用";
			labelSoundNote.Location = new Point(140, 118);
			labelSoundNote.Size = new Size(250, 20);
			labelSoundNote.ForeColor = Color.Gray;
			groupReminder.Controls.Add(labelSoundNote);

			// 表示時間
			var labelDuration = new Label();
			labelDuration.Text = "表示時間:";
			labelDuration.Location = new Point(15, 148);
			labelDuration.Size = new Size(60, 20);
			groupReminder.Controls.Add(labelDuration);

			numReminderDuration = new NumericUpDown();
			numReminderDuration.Location = new Point(80, 145);
			numReminderDuration.Size = new Size(60, 23);
			numReminderDuration.Minimum = 1;
			numReminderDuration.Maximum = 10;
			numReminderDuration.Value = 7;
			groupReminder.Controls.Add(numReminderDuration);

			var labelSeconds = new Label();
			labelSeconds.Text = "秒";
			labelSeconds.Location = new Point(145, 148);
			labelSeconds.Size = new Size(30, 20);
			groupReminder.Controls.Add(labelSeconds);
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
			groupLog.Size = new Size(415, 120);
			tab.Controls.Add(groupLog);

			// 詳細ログ
			checkVerboseLog = new CheckBox();
			checkVerboseLog.Text = "詳細ログを有効にする";
			checkVerboseLog.Location = new Point(15, 30);
			checkVerboseLog.Size = new Size(200, 20);
			groupLog.Controls.Add(checkVerboseLog);

			var labelVerboseNote = new Label();
			labelVerboseNote.Text = "※ デバッグ用の詳細なログを出力します";
			labelVerboseNote.Location = new Point(15, 55);
			labelVerboseNote.Size = new Size(300, 20);
			labelVerboseNote.ForeColor = Color.Gray;
			groupLog.Controls.Add(labelVerboseNote);

			// ログフォルダを開くボタン
			var buttonOpenLogFolder = new Button();
			buttonOpenLogFolder.Text = "ログフォルダを開く";
			buttonOpenLogFolder.Location = new Point(15, 80);
			buttonOpenLogFolder.Size = new Size(130, 28);
			buttonOpenLogFolder.Click += (s, e) => Logger.OpenLogFolder();
			groupLog.Controls.Add(buttonOpenLogFolder);
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

			// テーマ設定
			if (ThemeManager.IsDark)
			{
				radioThemeDark.Checked = true;
			}
			else
			{
				radioThemeLight.Checked = true;
			}

			// 詳細ログ設定
			checkVerboseLog.Checked = Logger.IsVerboseLoggingEnabled();

			UpdateReminderControlsState();
		}

		private void UpdateReminderControlsState()
		{
			bool enabled = checkReminderEnabled.Checked;
			checkReminderSoundEnabled.Enabled = enabled;
			textReminderSoundPath.Enabled = enabled;
			numReminderDuration.Enabled = enabled;
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

			// テーマ設定を更新
			ThemeChanged = (radioThemeDark.Checked && !ThemeManager.IsDark) || 
			               (radioThemeLight.Checked && ThemeManager.IsDark);
			NewThemeIsDark = radioThemeDark.Checked;

			// 詳細ログ設定を更新
			VerboseLogEnabled = checkVerboseLog.Checked;

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
