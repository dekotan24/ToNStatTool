using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ToNStatTool
{
    // イベントハンドラ部分
    public partial class ToNStatTool
    {
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

        private void OnWebSocketConnected(string playerName)
        {
            SafeInvoke(() =>
            {
                labelStatus.Text = $"接続済み - プレイヤー: {playerName}";
                labelStatus.ForeColor = Color.Green;
                buttonConnect.Text = "切断";
                buttonConnect.Enabled = true;
                textBoxUrl.ReadOnly = true;

                // 接続時にUIをリセット（ラウンド統計、ラウンドログ、推定生存回数がWebSocket側でリセットされているのでUIに反映）
                UpdateBirdCheckboxes();
                UpdateStatsDisplay();
                UpdateRoundLogDisplay();
            });
        }

        private void OnCloudSyncStateChanged(bool isSyncing)
        {
            SafeInvoke(() =>
            {
                string playerName = webSocketClient.LocalPlayerName;
                if (isSyncing)
                {
                    labelStatus.Text = $"接続済み（クラウド同期中） - プレイヤー: {playerName}";
                    labelStatus.ForeColor = Color.FromArgb(0, 180, 220); // 水色
                }
                else
                {
                    labelStatus.Text = $"接続済み - プレイヤー: {playerName}";
                    labelStatus.ForeColor = Color.Green;
                }
            });
        }

        private void OnWebSocketDisconnected()
        {
            SafeInvoke(() =>
            {
                labelStatus.Text = "切断済み";
                labelStatus.ForeColor = Color.Red;
                buttonConnect.Text = "接続";
                buttonConnect.Enabled = true;
                textBoxUrl.ReadOnly = false;
            });
        }

        private void OnWebSocketMessageReceived(string message)
        {
            SafeInvoke(() =>
            {
                var shortMessage = message.Length > 500 ? message.Substring(0, 500) + "..." : message;
                textBoxRawData.Text = FormatJson(shortMessage);
                textBoxRawData.SelectionStart = 0;
                textBoxRawData.ScrollToCaret();

                ScheduleUIUpdate();
            });
        }

        private void OnWebSocketError(string error)
        {
            SafeInvoke(() =>
            {
                MessageBox.Show($"エラー: {error}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                labelStatus.Text = "接続失敗";
                labelStatus.ForeColor = Color.Red;
                buttonConnect.Text = "接続";
                buttonConnect.Enabled = true;
                textBoxUrl.ReadOnly = false;
            });
        }

        private void OnTerrorUpdate()
        {
            SafeInvoke(() =>
            {
                UpdateTerrorDisplay();
            });
        }

        private void OnRoundEnd()
        {
            SafeInvoke(() =>
            {
                UpdateStatsDisplay();
                UpdateRoundLogDisplay();
                
                mainFormRoundActive = false;
                elapsedTimeTimer.Stop();
                
                UpdateNextRoundPrediction();
                
                if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
                {
                    terrorDisplayForm.OnRoundEnd();
                }
            });
        }

        private void OnRoundStart(ToNRoundType roundType)
        {
            SafeInvoke(() =>
            {
                mainFormRoundStartTime = DateTime.Now;
                mainFormRoundActive = true;
                elapsedTimeTimer.Start();
                
                UpdateNextRoundPrediction();
                
                if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
                {
                    terrorDisplayForm.OnRoundStart(roundType);
                }
            });
        }

        private void OnInstanceStateChanged()
        {
            SafeInvoke(() =>
            {
                string currentItem = webSocketClient?.InstanceState?.CurrentItem ?? "";
                System.Diagnostics.Debug.WriteLine($"[INSTANCE_STATE] OnInstanceStateChanged呼び出し: CurrentItem='{currentItem}'");
                
                UpdateBirdCheckboxes();
                UpdateNextRoundPrediction();
                UpdateCurrentItemDisplay();
                
                if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
                {
                    terrorDisplayForm.UpdateNextRoundPrediction();
                }
            });
        }

        private void OnPlayerCountChanged()
        {
            SafeInvoke(() =>
            {
                if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
                {
                    // スレッドセーフにコレクションをコピー
                    List<PlayerInfo> playerList;
                    try
                    {
                        playerList = webSocketClient.Players.Values.ToList();
                    }
                    catch (InvalidOperationException)
                    {
                        return;
                    }
                    
                    int aliveCount = playerList.Count(p => p.IsAlive);
                    int totalCount = playerList.Count;
                    terrorDisplayForm.UpdatePlayerCount(aliveCount, totalCount);
                }
            });
        }

        private void OnItemReminderRoundEnd()
        {
            Logger.Info("ItemReminder", $"アイテムリマインダーイベント受信: EnableItemReminder={webSocketClient.SoundSettings.EnableItemReminder}");
            System.Diagnostics.Debug.WriteLine($"[ITEM_REMINDER_UI] イベント受信: EnableItemReminder={webSocketClient.SoundSettings.EnableItemReminder}");
            
            // バッファ処理中またはインスタンス移動中はスキップ
            if (webSocketClient.ShouldMuteNotificationSounds())
            {
                Logger.Info("ItemReminder", "サウンドミュート期間中のためスキップ");
                System.Diagnostics.Debug.WriteLine("[ITEM_REMINDER_UI] サウンドミュート期間中のためスキップ");
                return;
            }
            
            if (!webSocketClient.SoundSettings.EnableItemReminder)
            {
                Logger.Info("ItemReminder", "リマインダーが無効のためスキップ");
                System.Diagnostics.Debug.WriteLine("[ITEM_REMINDER_UI] リマインダーが無効のためスキップ");
                return;
            }

            this.BeginInvoke(new Action(() =>
            {
                Logger.Info("ItemReminder", $"リマインダー処理開始: terrorDisplayForm={terrorDisplayForm != null}, IsDisposed={terrorDisplayForm?.IsDisposed ?? true}");
                System.Diagnostics.Debug.WriteLine($"[ITEM_REMINDER_UI] 処理開始: terrorDisplayForm={terrorDisplayForm != null}");
                
                if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
                {
                    int duration = webSocketClient.SoundSettings.ItemReminderDurationSeconds;
                    Logger.Info("ItemReminder", $"テラー表示ウィンドウにリマインダー表示: duration={duration}秒");
                    System.Diagnostics.Debug.WriteLine($"[ITEM_REMINDER_UI] ShowItemReminder呼び出し: duration={duration}");
                    terrorDisplayForm.ShowItemReminder(duration);
                }
                else
                {
                    Logger.Info("ItemReminder", "テラー表示ウィンドウが表示されていないためリマインダーをスキップ");
                    System.Diagnostics.Debug.WriteLine("[ITEM_REMINDER_UI] terrorDisplayFormがないためスキップ");
                }

                if (webSocketClient.SoundSettings.EnableItemReminderSound)
                {
                    Logger.Info("ItemReminder", "リマインダー音を再生");
                    System.Diagnostics.Debug.WriteLine("[ITEM_REMINDER_UI] サウンド再生");
                    PlayItemReminderSound();
                }
            }));
        }

        private void OnMasterChanged()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(OnMasterChanged));
                return;
            }
            
            // バッファ処理中またはインスタンス移動中はサウンドをスキップ（UIは更新）
            bool shouldMute = webSocketClient.ShouldMuteNotificationSounds();
            if (shouldMute)
            {
                Logger.Info("MasterChange", "サウンドミュート期間中のためサウンドスキップ");
            }
            
            if (webSocketClient.SoundSettings.EnableMasterChangeSound && !shouldMute)
            {
                webSocketClient.PlayCustomSound(webSocketClient.SoundSettings.MasterChangeSoundPath, "masterchange.mp3");
            }
            
            UpdateNextRoundPrediction();
            
            if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
            {
                terrorDisplayForm.UpdateNextRoundPrediction();
            }
            
            Logger.Info("MasterChange", "マスター変更を検出 - 次ラウンドは特殊確定");
        }

        private void OnSaveCodeReceived(SaveCodeInfo saveCode)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<SaveCodeInfo>(OnSaveCodeReceived), saveCode);
                return;
            }
            
            Logger.Info("SaveCode", $"セーブコード受信: {saveCode.Code} ({saveCode.RoundTypeName})");
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

        private void OnThemeChanged(object sender, AppTheme newTheme)
        {
            ThemeManager.Apply(this);

            var btnSettings = FindControl("btnSettings") as Button;
            if (btnSettings != null)
            {
                btnSettings.BackColor = ThemeManager.IsDark
                    ? ThemeManager.Dark.ButtonBackground
                    : SystemColors.Control;
                btnSettings.ForeColor = ThemeManager.IsDark
                    ? ThemeManager.Dark.Text
                    : ThemeManager.Light.Text;
            }

            if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
            {
                terrorDisplayForm.ApplyTheme();
            }

            // メインフォームのテラーコントロールのテーマを更新
            foreach (var terrorControl in terrorControls)
            {
                terrorControl.ApplyTheme();
            }

            UpdatePlayerList();
            UpdateNextRoundPrediction();
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

        private void ButtonReloadWarningUsers_Click(object sender, EventArgs e)
        {
            try
            {
                webSocketClient.ReloadWarningUsers();
                var warningUsers = webSocketClient.GetWarningUsers();

                MessageBox.Show($"警告対象ユーザーリストを再読み込みしました。\n現在の登録数: {warningUsers.Count}人", "リスト再読み込み", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
                var result = MessageBox.Show(
                    "ラウンド統計、テラー統計、ラウンドログをすべてリセットします。\n\nこの操作は取り消せません。よろしいですか？",
                    "統計リセットの確認",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    webSocketClient.ResetRoundStats();
                    UpdateStatsDisplay();
                    UpdateRoundLogDisplay();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"統計のリセットでエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonStats_Click(object sender, EventArgs e)
        {
            try
            {
                if (sessionStatsForm == null || sessionStatsForm.IsDisposed)
                {
                    sessionStatsForm = new SessionStatsForm(webSocketClient.SessionStats);
                }
                
                sessionStatsForm.UpdateDisplay();
                sessionStatsForm.ApplyTheme();
                
                if (!sessionStatsForm.Visible)
                {
                    sessionStatsForm.Show(this);
                }
                else
                {
                    sessionStatsForm.BringToFront();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"統計フォームの表示でエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonCopyInstanceUrl_Click(object sender, EventArgs e)
        {
            try
            {
                string instanceUrl = webSocketClient.InstanceState.InstanceUrl;
                
                if (string.IsNullOrEmpty(instanceUrl))
                {
                    MessageBox.Show("インスタンスURLがまだ取得されていません。\nゲームに接続してから再試行してください。", 
                        "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string[] parts = instanceUrl.Split(new[] { ':' }, 2);
                string worldId = parts[0];
                string instanceId = parts[1];
                string launchUrl = $"https://vrchat.com/home/launch?worldId={worldId}&instanceId={instanceId}";
                
                Clipboard.SetText(launchUrl);
                
                var button = sender as Button;
                if (button != null)
                {
                    var originalText = button.Text;
                    button.Text = "✓";
                    var timer = new System.Windows.Forms.Timer();
                    timer.Interval = 1000;
                    timer.Tick += (s, args) =>
                    {
                        button.Text = originalText;
                        timer.Stop();
                        timer.Dispose();
                    };
                    timer.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"URLのコピーでエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonSaveCodes_Click(object sender, EventArgs e)
        {
            try
            {
                List<SaveCodeInfo> saveCodes;
                try { saveCodes = webSocketClient.SaveCodes.ToList(); }
                catch { return; }

                if (saveCodes.Count == 0)
                {
                    MessageBox.Show("セーブコードがまだありません。\nラウンドをクリアするとセーブコードが生成されます。", 
                        "情報", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                var contextMenu = new ContextMenuStrip();
                
                foreach (var saveCode in saveCodes)
                {
                    // テラー名も表示（長すぎる場合は省略）
                    string terrorDisplay = saveCode.TerrorNames;
                    if (string.IsNullOrEmpty(terrorDisplay))
                    {
                        terrorDisplay = "-";
                    }
                    else if (terrorDisplay.Length > 30)
                    {
                        terrorDisplay = terrorDisplay.Substring(0, 27) + "...";
                    }
                    
                    var item = new ToolStripMenuItem($"{saveCode.Timestamp:HH:mm:ss} - {saveCode.RoundTypeName} - {terrorDisplay}");
                    item.Tag = saveCode.Code;
                    item.Click += (s, args) =>
                    {
                        var code = (s as ToolStripMenuItem)?.Tag?.ToString();
                        if (!string.IsNullOrEmpty(code))
                        {
                            Clipboard.SetText(code);
                            MessageBox.Show($"セーブコードをコピーしました。\n\n{code}", 
                                "コピー完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    };
                    contextMenu.Items.Add(item);
                }
                
                var button = sender as Button;
                if (button != null)
                {
                    contextMenu.Show(button, new Point(0, button.Height));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"セーブコード一覧の表示でエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    
                    // スレッドセーフにコレクションをコピー
                    List<TerrorInfo> terrorsCopy;
                    List<PlayerInfo> playerList;
                    try
                    {
                        terrorsCopy = webSocketClient.CurrentTerrors?.ToList() ?? new List<TerrorInfo>();
                        playerList = webSocketClient.Players?.Values.ToList() ?? new List<PlayerInfo>();
                    }
                    catch (InvalidOperationException)
                    {
                        terrorsCopy = new List<TerrorInfo>();
                        playerList = new List<PlayerInfo>();
                    }

                    // Unboundラウンドの場合、内訳テラーに展開
                    string unboundName = null;
                    var instanceState = webSocketClient?.InstanceState;
                    if (instanceState?.CurrentRoundType == ToNRoundType.Unbound && terrorsCopy.Count > 0)
                    {
                        var expandedTerrors = new List<TerrorInfo>();
                        foreach (var terror in terrorsCopy)
                        {
                            var unboundTerrors = UnboundJsonLoader.GetUnboundTerrors(terror.Name);
                            if (unboundTerrors.Count > 0)
                            {
                                unboundName = terror.Name;
                                foreach (var innerTerrorName in unboundTerrors)
                                {
                                    expandedTerrors.Add(CreateTerrorInfoFromName(innerTerrorName));
                                }
                            }
                            else
                            {
                                expandedTerrors.Add(terror);
                            }
                        }
                        terrorsCopy = expandedTerrors;
                    }

                    // マップ名を取得（HFA判定用）
                    string mapName = null;
                    if (webSocketClient?.GameData != null && webSocketClient.GameData.ContainsKey("location"))
                    {
                        mapName = webSocketClient.GameData["location"]?.ToString();
                    }

                    terrorDisplayForm.UpdateTerrors(terrorsCopy, unboundName, mapName);

                    int aliveCount = playerList.Count(p => p.IsAlive);
                    int totalCount = playerList.Count;
                    terrorDisplayForm.UpdatePlayerCount(aliveCount, totalCount);
                    
                    if (mainFormRoundActive && webSocketClient.InstanceState.HasCurrentRound)
                    {
                        terrorDisplayForm.SyncRoundInfo(webSocketClient.InstanceState.CurrentRoundType, mainFormRoundStartTime, mainFormRoundActive);
                    }
                    else
                    {
                        terrorDisplayForm.UpdateNextRoundPrediction();
                    }
                    
                    var trackBar = FindControl("trackBarOpacity") as TrackBar;
                    if (trackBar != null)
                    {
                        terrorDisplayForm.SetOpacity(trackBar.Value / 100.0);
                    }
                    
                    terrorDisplayForm.FormClosed += (s, args) =>
                    {
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

        private void ButtonSoundSettings_Click(object sender, EventArgs e)
        {
            try
            {
                ShowSoundSettingsDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定の表示でエラーが発生しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ListViewPlayers_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                var listView = sender as ListView;
                if (listView?.SelectedItems.Count > 0)
                {
                    string playerName = listView.SelectedItems[0].Text;
                    
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
                                UpdatePlayerList();
                            }
                        }
                    }
                    else
                    {
                        var result = MessageBox.Show(
                            $"{playerName} を警告対象ユーザーに追加しますか？",
                            "警告ユーザー追加",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Yes)
                        {
                            if (webSocketClient.AddWarningUser(playerName))
                            {
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

        // 右クリックメニュー表示時の処理
        private void ContextMenuPlayers_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var contextMenu = sender as ContextMenuStrip;
            var listView = contextMenu?.SourceControl as ListView;
            
            if (listView?.SelectedItems.Count == 0)
            {
                // アイテムが選択されていない場合はメニューを表示しない
                e.Cancel = true;
                return;
            }
            
            string playerName = listView.SelectedItems[0].Text;
            
            // 警告ユーザーのメニューテキストを動的に更新
            var menuItemWarning = contextMenu.Items["menuItemToggleWarning"] as ToolStripMenuItem;
            if (menuItemWarning != null)
            {
                if (webSocketClient.IsWarningUser(playerName))
                {
                    menuItemWarning.Text = "警告ユーザーから削除";
                }
                else
                {
                    menuItemWarning.Text = "警告ユーザーに追加";
                }
            }
        }

        // 警告ユーザー追加/削除メニュークリック
        private void MenuItemToggleWarning_Click(object sender, EventArgs e)
        {
            try
            {
                var menuItem = sender as ToolStripMenuItem;
                var contextMenu = menuItem?.Owner as ContextMenuStrip;
                var listView = contextMenu?.SourceControl as ListView;
                
                if (listView?.SelectedItems.Count > 0)
                {
                    string playerName = listView.SelectedItems[0].Text;
                    
                    if (webSocketClient.IsWarningUser(playerName))
                    {
                        if (webSocketClient.RemoveWarningUser(playerName))
                        {
                            UpdatePlayerList();
                        }
                    }
                    else
                    {
                        if (webSocketClient.AddWarningUser(playerName))
                        {
                            UpdatePlayerList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エラー: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // プレイヤー削除メニュークリック
        private void MenuItemRemovePlayer_Click(object sender, EventArgs e)
        {
            try
            {
                var menuItem = sender as ToolStripMenuItem;
                var contextMenu = menuItem?.Owner as ContextMenuStrip;
                var listView = contextMenu?.SourceControl as ListView;
                
                if (listView?.SelectedItems.Count > 0)
                {
                    string playerName = listView.SelectedItems[0].Text;
                    
                    var result = MessageBox.Show(
                        $"{playerName} をプレイヤー一覧から削除しますか？\n\n※この操作は、leave通知が来なかった場合に手動で\nプレイヤーを削除するためのものです。",
                        "プレイヤー削除確認",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Yes)
                    {
                        if (webSocketClient.RemovePlayerManually(playerName))
                        {
                            UpdatePlayerList();
                        }
                        else
                        {
                            MessageBox.Show($"プレイヤー '{playerName}' の削除に失敗しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エラー: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            webSocketClient.CleanupOldData();
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
    }
}
