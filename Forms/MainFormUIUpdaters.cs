using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ToNStatTool
{
    // UI更新処理部分
    public partial class ToNStatTool
    {
        // プレイヤーリスト差分更新用
        private string lastPlayerListHash = "";
        
        // イベントリスト差分更新用
        private int lastEventCount = 0;
        private DateTime lastEventTimestamp = DateTime.MinValue;
        
        // ラウンドログ差分更新用
        private int lastRoundLogCount = 0;
        private string lastRoundLogFilter = "";
        
        private void ScheduleUIUpdate()
        {
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

        private void UpdateGameInfo()
        {
            var gameData = webSocketClient.GameData;

            UpdateTextBoxWithColor("roundType", GetGameDataValue(gameData, "roundType", "-"));
            UpdateTextBox("location", GetGameDataValue(gameData, "location", "-"));
            UpdateTextBox("roundActive", GetGameDataValue(gameData, "roundActive", "-"));
            UpdateTextBox("alive", GetGameDataValue(gameData, "alive", "-"));
            UpdateTextBox("saboteur", GetGameDataValue(gameData, "saboteur", "-"));
            UpdateTextBox("pageCount", GetGameDataValue(gameData, "pageCount", "-"));
        }

        private void UpdateTerrorDisplay()
        {
            // スレッドセーフにリストをコピー
            List<TerrorInfo> currentTerrors;
            try
            {
                currentTerrors = webSocketClient.CurrentTerrors?.ToList() ?? new List<TerrorInfo>();
            }
            catch (InvalidOperationException)
            {
                return; // コレクションが変更中の場合はスキップ
            }

            // Unboundラウンドの場合、内訳テラーに展開
            string unboundAnnounceName = null;
            var instanceState = webSocketClient?.InstanceState;
            if (instanceState?.CurrentRoundType == ToNRoundType.Unbound && currentTerrors.Count > 0)
            {
                var expandedTerrors = new List<TerrorInfo>();
                foreach (var terror in currentTerrors)
                {
                    var unboundTerrors = UnboundJsonLoader.GetUnboundTerrors(terror.Name);
                    if (unboundTerrors.Count > 0)
                    {
                        // Unbound名を保持（ツールチップ表示用）
                        unboundAnnounceName = terror.Name;
                        // 内訳テラーに展開
                        foreach (var innerTerrorName in unboundTerrors)
                        {
                            var terrorInfo = CreateTerrorInfoFromName(innerTerrorName);
                            expandedTerrors.Add(terrorInfo);
                        }
                    }
                    else
                    {
                        // Unbound情報がない場合はそのまま追加
                        expandedTerrors.Add(terror);
                    }
                }
                currentTerrors = expandedTerrors;
            }

            // テラーの数が変わったか、または内容が変わったかをチェック
            bool needsUpdate = currentTerrors.Count != terrorControls.Count;

			/*
            // この部分の処理は不要なのではないかと思い、検証のためコメントアウト
            // これで問題が無ければ将来的に削除予定
            if (!needsUpdate && currentTerrors.Count > 0)
            {
                // 数が同じでも、テラー名が変わっていれば更新が必要
                var currentNames = currentTerrors.Select(t => t.Name).ToList();
                var displayedNames = terrorControls.Select(c => c.TerrorData?.Name ?? "").ToList();
                needsUpdate = !currentNames.SequenceEqual(displayedNames);
            }
            */

			if (needsUpdate)
            {
                foreach (var control in terrorControls)
                {
                    control.Dispose();
                }
                terrorControls.Clear();
                terrorDisplayPanel.Controls.Clear();

                foreach (var terror in currentTerrors)
                {
                    var terrorControl = new TerrorControl(terror);
                    terrorControls.Add(terrorControl);
                    terrorDisplayPanel.Controls.Add(terrorControl);
                }

                AdjustTerrorLayout();

                if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
                {
                    // マップ名を取得（HFA判定用）
                    string mapName = null;
                    if (webSocketClient?.GameData != null && webSocketClient.GameData.ContainsKey("location"))
                    {
                        mapName = webSocketClient.GameData["location"]?.ToString();
                    }

                    terrorDisplayForm.UpdateTerrors(currentTerrors, unboundAnnounceName, mapName);

                    // スレッドセーフにプレイヤーリストをコピー
                    List<PlayerInfo> playerList;
                    try
                    {
                        playerList = webSocketClient.Players?.Values.ToList() ?? new List<PlayerInfo>();
                    }
                    catch (InvalidOperationException)
                    {
                        return;
                    }
                    
                    int aliveCount = playerList.Count(p => p.IsAlive);
                    int totalCount = playerList.Count;
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

                // スレッドセーフにコレクションをコピー
                var players = webSocketClient.Players;
                if (players == null) return;
                
                List<PlayerInfo> playerList;
                try
                {
                    playerList = players.Values.ToList();
                }
                catch (InvalidOperationException)
                {
                    // コレクションが変更中の場合はスキップ
                    return;
                }
                
                var localPlayerUserId = webSocketClient.LocalPlayerUserId;

                // 変更検出用ハッシュを計算（テーマも含める）
                var hashBuilder = new System.Text.StringBuilder();
                hashBuilder.Append(ThemeManager.IsDark ? "D" : "L");
                foreach (var player in playerList.OrderBy(p => p.Name))
                {
                    hashBuilder.Append($"|{player.Name}:{player.IsAlive}:{player.UserId == localPlayerUserId}:{webSocketClient.IsWarningUser(player.Name)}:{player.RoundCount}:{player.SurvivalCount}");
                }
                string currentHash = hashBuilder.ToString();

                // ハッシュが同じなら更新をスキップ
                if (currentHash == lastPlayerListHash)
                {
                    return;
                }
                lastPlayerListHash = currentHash;

                var selectedIndices = new List<int>();
                foreach (int index in listView.SelectedIndices)
                {
                    selectedIndices.Add(index);
                }

                listView.BeginUpdate();
                listView.Items.Clear();

                int totalPlayers = playerList.Count;
                int alivePlayers = 0;
                int warningPlayers = 0;

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[UI] プレイヤー一覧更新 - 総数: {totalPlayers}");
#endif

                foreach (var player in playerList.OrderBy(p => p.Name))
                {
                    try
                    {
                        string displayName = GetDisplayPlayerName(player.Name);
                        bool isWarningUser = webSocketClient.IsWarningUser(player.Name);

                        var item = new ListViewItem(displayName);
                        item.SubItems.Add(player.IsAlive ? "生存" : "死亡");

                        string playerType = player.UserId == localPlayerUserId ? "自分" : "他人";
                        if (isWarningUser)
                        {
                            playerType = "⚠️注意";
                            warningPlayers++;
                        }
                        item.SubItems.Add(playerType);
                        item.SubItems.Add(player.RoundCount.ToString());
                        item.SubItems.Add(player.SurvivalCount.ToString());

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

                        if (isWarningUser)
                        {
                            item.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.PlayerWarning : ThemeManager.Light.PlayerWarning;
                            item.Font = new Font(listView.Font, FontStyle.Bold);
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

#if DEBUG
                        string warningFlag = isWarningUser ? " [警告]" : "";
                        System.Diagnostics.Debug.WriteLine($"[UI] プレイヤー追加: '{displayName}'{warningFlag} - {(player.IsAlive ? "生存" : "死亡")}");
#endif
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"[UI] プレイヤー表示エラー: {player.Name} - {ex.Message}");
#endif

                        var errorItem = new ListViewItem($"[表示エラー] {player.UserId}");
                        errorItem.SubItems.Add(player.IsAlive ? "生存" : "死亡");
                        errorItem.SubItems.Add(player.UserId == localPlayerUserId ? "自分" : "他人");
                        errorItem.SubItems.Add(player.RoundCount.ToString());
                        errorItem.SubItems.Add(player.SurvivalCount.ToString());
                        errorItem.ForeColor = Color.Orange;
                        listView.Items.Add(errorItem);
                    }
                }

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

                foreach (int index in selectedIndices)
                {
                    if (index < listView.Items.Count)
                        listView.Items[index].Selected = true;
                }

                listView.EndUpdate();
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[UI] プレイヤーリスト更新エラー: {ex.Message}");
#endif
            }
            finally
            {
                isUpdatingPlayers = false;
            }
        }

        private string GetDisplayPlayerName(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return "Unknown";

            try
            {
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

                // 変更検出：イベント数とタイムスタンプで判定
                int currentEventCount = recentEvents.Count;
                DateTime currentLastTimestamp = recentEvents.Count > 0 
                    ? recentEvents.Max(e => e.Timestamp) 
                    : DateTime.MinValue;

                if (currentEventCount == lastEventCount && currentLastTimestamp == lastEventTimestamp)
                {
                    return;
                }
                lastEventCount = currentEventCount;
                lastEventTimestamp = currentLastTimestamp;

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
            var labelTotalRounds = FindControl("labelTotalRounds") as Label;
            var listView = FindControl("listViewStats") as ListView;

            if (labelTotalRounds != null && listView != null)
            {
                var roundStats = webSocketClient.RoundStats;

                labelTotalRounds.Text = $"総ラウンド数: {roundStats.TotalRounds}";

                listView.BeginUpdate();
                listView.Items.Clear();

                List<KeyValuePair<ToNRoundType, int>> roundTypeCounts;
                try { roundTypeCounts = roundStats.RoundTypeCounts.ToList(); }
                catch { listView.EndUpdate(); return; }
                if (roundTypeCounts.Count > 0)
                {
                    foreach (var kvp in roundTypeCounts.OrderByDescending(x => x.Value).ThenBy(x => ToNRoundTypeHelper.GetDisplayName(x.Key)))
                    {
                        double percentage = roundStats.TotalRounds > 0 ? (double)kvp.Value / roundStats.TotalRounds * 100 : 0;
                        var item = new ListViewItem(ToNRoundTypeHelper.GetDisplayName(kvp.Key));
                        item.SubItems.Add(kvp.Value.ToString());
                        item.SubItems.Add(percentage.ToString("F1"));
                        listView.Items.Add(item);
                    }
                }

                listView.EndUpdate();
            }

            var listView2 = FindControl("listViewStatsTerrors") as ListView;
            if (listView2 != null)
            {
                var terrorStats = webSocketClient.TerrorStats;

                listView2.BeginUpdate();
                listView2.Items.Clear();

                List<KeyValuePair<string, int>> terrorTypeCounts;
                try { terrorTypeCounts = terrorStats.TerrorTypeCounts.ToList(); }
                catch { listView2.EndUpdate(); return; }
                if (terrorTypeCounts.Count > 0)
                {
                    foreach (var kvp in terrorTypeCounts.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
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

            // スナップショットを取得（クラウドマージ中のコレクション変更によるクラッシュ防止）
            List<RoundLog> roundLogs;
            try
            {
                roundLogs = webSocketClient.RoundLogs.ToList();
            }
            catch
            {
                return; // コレクション変更中の場合はスキップ（次回更新で再描画される）
            }

            // フィルター条件を取得
            var comboRoundFilter = FindControl("comboRoundFilter") as ComboBox;
            var textTerrorFilter = FindControl("textTerrorFilter") as TextBox;
            var labelFilterCount = FindControl("labelFilterCount") as Label;

            string roundTypeFilter = comboRoundFilter?.SelectedIndex > 0 
                ? comboRoundFilter.SelectedItem?.ToString() 
                : null;
            string terrorFilter = !string.IsNullOrWhiteSpace(textTerrorFilter?.Text) 
                ? textTerrorFilter.Text.Trim().ToLower() 
                : null;

            // 変更検出用ハッシュを計算（テーマ + ログ数 + フィルター条件）
            string currentFilter = $"{(ThemeManager.IsDark ? "D" : "L")}|{roundLogs.Count}|{roundTypeFilter ?? ""}|{terrorFilter ?? ""}";
            if (currentFilter == lastRoundLogFilter && roundLogs.Count == lastRoundLogCount)
            {
                return;
            }
            lastRoundLogCount = roundLogs.Count;
            lastRoundLogFilter = currentFilter;

            listView.BeginUpdate();
            listView.Items.Clear();

            int totalCount = 0;
            int filteredCount = 0;

            foreach (var log in roundLogs.OrderByDescending(l => l.Timestamp).Take(1000))
            {
                totalCount++;

                // ラウンド種別フィルター
                if (roundTypeFilter != null && log.RoundTypeDisplayName != roundTypeFilter)
                {
                    continue;
                }

                // テラー名フィルター（部分一致）
                if (terrorFilter != null && 
                    (string.IsNullOrEmpty(log.TerrorNames) || 
                     !log.TerrorNames.ToLower().Contains(terrorFilter)))
                {
                    continue;
                }

                filteredCount++;

                string timeDisplay = log.IsReplay ? "RP" : log.Timestamp.ToString("HH:mm");
                var item = new ListViewItem(timeDisplay);
                item.SubItems.Add(log.RoundTypeDisplayName);
                item.SubItems.Add(log.MapName);
                item.SubItems.Add(log.TerrorNames);
                item.SubItems.Add(string.IsNullOrEmpty(log.Items) || log.Items == "なし" ? "-" : log.Items);
                item.SubItems.Add(log.IsReplay ? "-" : (log.TotalPlayerCount > 0 ? $"{log.AliveCount}/{log.TotalPlayerCount}" : "-"));

                if (!log.WasOptedIn)
                {
                    item.ForeColor = ThemeManager.IsDark ? Color.FromArgb(128, 128, 128) : Color.Gray;
                }
                else if (log.Survived)
                {
                    item.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.RoundLogSurvived : ThemeManager.Light.RoundLogSurvived;
                }
                else
                {
                    item.ForeColor = ThemeManager.IsDark ? ThemeManager.Dark.RoundLogDied : ThemeManager.Light.RoundLogDied;
                }

                listView.Items.Add(item);
            }

            listView.EndUpdate();

            // フィルター件数を更新
            if (labelFilterCount != null)
            {
                if (roundTypeFilter != null || terrorFilter != null)
                {
                    labelFilterCount.Text = $"{filteredCount}/{totalCount}";
                    labelFilterCount.ForeColor = ThemeManager.IsDark ? Color.LightBlue : Color.Blue;
                }
                else
                {
                    labelFilterCount.Text = "";
                }
            }
        }

        private void UpdateBirdCheckboxes()
        {
            var instanceState = webSocketClient?.InstanceState;
            if (instanceState == null) return;

            var checkBigBird = FindControl("checkBigBird") as CheckBox;
            var checkJudgementBird = FindControl("checkJudgementBird") as CheckBox;
            var checkPunishingBird = FindControl("checkPunishingBird") as CheckBox;

            if (checkBigBird != null && checkBigBird.Checked != instanceState.MetBigBird)
                checkBigBird.Checked = instanceState.MetBigBird;
            if (checkJudgementBird != null && checkJudgementBird.Checked != instanceState.MetJudgementBird)
                checkJudgementBird.Checked = instanceState.MetJudgementBird;
            if (checkPunishingBird != null && checkPunishingBird.Checked != instanceState.MetPunishingBird)
                checkPunishingBird.Checked = instanceState.MetPunishingBird;

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

            var labelSurvivalValue = FindControl("labelSurvivalValue") as Label;
            if (labelSurvivalValue != null)
            {
                int targetValue = Math.Max(0, Math.Min(9999, instanceState.EstimatedSurvivalCount));
                labelSurvivalValue.Text = targetValue.ToString();
            }
        }

        private void UpdateCurrentItemDisplay()
        {
            var textBoxCurrentItem = FindControl("textBox_currentItem") as TextBox;
            if (textBoxCurrentItem == null)
            {
                System.Diagnostics.Debug.WriteLine("[ITEM_DISPLAY] textBox_currentItemが見つかりません");
                return;
            }
            
            var instanceState = webSocketClient?.InstanceState;
            string currentItem = instanceState?.CurrentItem ?? "";
            string displayText = string.IsNullOrEmpty(currentItem) ? "-" : currentItem;
            
            System.Diagnostics.Debug.WriteLine($"[ITEM_DISPLAY] UpdateCurrentItemDisplay: CurrentItem='{currentItem}' → Display='{displayText}'");
            
            textBoxCurrentItem.Text = displayText;
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

            if (mainFormRoundActive && instanceState.HasCurrentRound)
            {
                UpdateNextRoundPredictionForCurrentRound(instanceState.CurrentRoundType);
                return;
            }

            string prediction = "";
            Color color = ThemeManager.IsDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;

            if (instanceState.MasterChanged)
            {
                prediction = "特殊 (マスター変更)";
                color = ThemeManager.GetPredictionColor("special");
                textBoxNextRound.Text = prediction;
                textBoxNextRound.ForeColor = color;
                return;
            }

            // JustUnlockedフラグを最優先でチェック（解禁直後のMoonを予測）
            // これによりAllMoonsUnlockedによるSolstice予測が上書きされることを防ぐ
            // 複数同時解禁時の優先度: Twilight > Mystic Moon > Blood Moon
            // （Wiki情報: BloodMoonとMysticMoonならMysticMoon、TwilightとMysticMoonならTwilight）
            if (instanceState.TwilightJustUnlocked)
            {
                prediction = "Twilight (解禁直後)";
                color = ThemeManager.GetPredictionColor("twilight");
            }
            else if (instanceState.MysticMoonJustUnlocked)
            {
                prediction = "Mystic Moon (解禁直後)";
                color = ThemeManager.GetPredictionColor("mystic");
            }
            else if (instanceState.BloodMoonJustUnlocked)
            {
                prediction = "Blood Moon (解禁直後)";
                color = ThemeManager.GetPredictionColor("blood");
            }
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
                prediction = "通常 (特殊未解放)";
                color = ThemeManager.GetPredictionColor("disabled");
            }
            else
            {
                if (instanceState.NormalRoundCount == 0)
                {
                    prediction = "通常";
                    color = ThemeManager.GetPredictionColor("normal");
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

        private void UpdateNextRoundPredictionForCurrentRound(ToNRoundType currentRoundType)
        {
            var textBoxNextRound = FindControl("textBox_nextRound") as TextBox;
            if (textBoxNextRound == null) return;

            var instanceState = webSocketClient?.InstanceState;
            if (instanceState == null)
            {
                textBoxNextRound.Text = "-";
                return;
            }

            // マスター変更時は特殊確定（ラウンド進行中でも即座に反映）
            if (instanceState.MasterChanged)
            {
                textBoxNextRound.Text = "特殊(MC)";
                textBoxNextRound.ForeColor = ThemeManager.GetPredictionColor("special");
                return;
            }

            string prediction = "";
            Color color = ThemeManager.IsDark ? ThemeManager.Dark.Text : ThemeManager.Light.Text;

            int normalCountAtStart = instanceState.NormalRoundCountAtRoundStart;

            if (ToNRoundTypeHelper.IsSpecialRound(currentRoundType))
            {
                prediction = "通常";
                color = ThemeManager.GetPredictionColor("normal");
            }
            else if (ToNRoundTypeHelper.IsMoonRound(currentRoundType))
            {
                if (instanceState.IsCurrentRoundFirstMoon)
                {
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
                    prediction = "通常";
                    color = ThemeManager.GetPredictionColor("normal");
                }
            }
            else if (ToNRoundTypeHelper.IsOverrideRound(currentRoundType))
            {
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
                // Normal系（Classic, RUN）
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

            textBoxNextRound.Text = prediction;
            textBoxNextRound.ForeColor = color;
        }

        private void UpdateTextBoxWithColor(string key, string value)
        {
            var textBox = FindControl($"textBox_{key}") as TextBox;
            if (textBox != null && textBox.Text != value)
            {
                // ラウンドタイプの場合、上書きフラグをチェックして表示を変更
                string displayValue = value;
                if (key == "roundType" && webSocketClient?.InstanceState?.IsCurrentRoundOverride == true)
                {
                    // 「(開始)」や「(終了)」がある場合はその前に「(上書き)」を挿入
                    if (displayValue.Contains("(開始)"))
                    {
                        displayValue = displayValue.Replace("(開始)", "(上書き・開始)");
                    }
                    else if (displayValue.Contains("(終了)"))
                    {
                        displayValue = displayValue.Replace("(終了)", "(上書き・終了)");
                    }
                    else if (!displayValue.Contains("(上書き)"))
                    {
                        displayValue = displayValue + " (上書き)";
                    }
                }
                
                textBox.Text = displayValue;

                if (key == "roundType")
                {
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

        /// <summary>
        /// テラー名からTerrorInfoを生成（Unbound内訳テラー用）
        /// </summary>
        private TerrorInfo CreateTerrorInfoFromName(string terrorName)
        {
            var detail = TerrorJsonLoader.GetTerrorDetail(terrorName);
            var stunType = detail?.StunType ?? TerrorStunType.Unknown;

            // JSONから取得できなかった場合はTerrorConfigurationからスタンタイプを取得
            if (stunType == TerrorStunType.Unknown)
            {
                stunType = TerrorConfiguration.GetTerrorStunType(terrorName);
            }

            return new TerrorInfo
            {
                Name = terrorName,
                DisplayName = terrorName,
                StunType = stunType,
                DisplayColor = 0
            };
        }
    }
}
