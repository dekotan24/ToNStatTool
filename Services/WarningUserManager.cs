using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ToNStatTool.Services
{
    /// <summary>
    /// 警告対象ユーザーを管理するクラス
    /// </summary>
    public class WarningUserManager
    {
        private HashSet<string> warningUsers = new HashSet<string>();
        private readonly string warningFilePath;

        /// <summary>
        /// 警告ユーザー数
        /// </summary>
        public int Count => warningUsers.Count;

        public WarningUserManager()
        {
            warningFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warn_user.txt");
            Load();
        }

        /// <summary>
        /// 警告対象ユーザーリストを読み込む
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(warningFilePath))
                {
                    var lines = File.ReadAllLines(warningFilePath);
                    warningUsers.Clear();

                    foreach (var line in lines)
                    {
                        var username = line.Trim();
                        if (!string.IsNullOrEmpty(username) && !username.StartsWith("#"))
                        {
                            warningUsers.Add(username.ToLowerInvariant());
                            System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー登録: {username}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー数: {warningUsers.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WARNING] warn_user.txtファイルが見つかりません");
                    File.WriteAllText(warningFilePath, "# 警告対象のユーザー名を1行1名で記入してください\n# #で始まる行はコメントです\n");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザーリスト読み込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 警告ユーザーリストを再読み込み
        /// </summary>
        public void Reload()
        {
            Load();
        }

        /// <summary>
        /// ユーザーが警告対象かチェック
        /// </summary>
        public bool IsWarningUser(string playerName)
        {
            if (string.IsNullOrEmpty(playerName) || warningUsers.Count == 0)
                return false;

            var normalizedName = playerName.ToLowerInvariant().Trim();
            return warningUsers.Contains(normalizedName);
        }

        /// <summary>
        /// 警告ユーザーを追加
        /// </summary>
        public bool AddWarningUser(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return false;

            var normalizedName = playerName.ToLowerInvariant().Trim();
            
            if (warningUsers.Contains(normalizedName))
                return false;

            try
            {
                warningUsers.Add(normalizedName);

                if (File.Exists(warningFilePath))
                {
                    File.AppendAllText(warningFilePath, Environment.NewLine + playerName);
                }
                else
                {
                    File.WriteAllText(warningFilePath, 
                        "# 警告対象のユーザー名を1行1名で記入してください\n# #で始まる行はコメントです\n" + playerName);
                }

                System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー追加: {playerName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー追加エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 警告ユーザーを削除
        /// </summary>
        public bool RemoveWarningUser(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return false;

            var normalizedName = playerName.ToLowerInvariant().Trim();

            if (!warningUsers.Contains(normalizedName))
                return false;

            try
            {
                warningUsers.Remove(normalizedName);

                if (File.Exists(warningFilePath))
                {
                    var lines = File.ReadAllLines(warningFilePath)
                        .Where(line => !line.Trim().Equals(playerName, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    File.WriteAllLines(warningFilePath, lines);
                }

                System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー削除: {playerName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー削除エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 警告ユーザーリストを取得
        /// </summary>
        public HashSet<string> GetWarningUsers()
        {
            return new HashSet<string>(warningUsers);
        }
    }
}
