using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NAudio.Wave;
using Newtonsoft.Json;
using ToNStatTool.Services;

namespace ToNStatTool
{
	/// <summary>
	/// 警告対象ユーザーリストの管理
	/// </summary>
	public partial class WebSocketClient
	{
		/// <summary>
		/// 警告対象ユーザーリストを読み込む
		/// </summary>
		private void LoadWarningUsers()
		{
			try
			{
				string warningFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warn_user.txt");

				if (File.Exists(warningFilePath))
				{
					var lines = File.ReadAllLines(warningFilePath);
					warningUsers.Clear();

					foreach (var line in lines)
					{
						var username = line.Trim();
						if (!string.IsNullOrEmpty(username) && !username.StartsWith("#")) // #で始まる行はコメント扱い
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
					// ファイルが存在しない場合は空のファイルを作成
					File.WriteAllText(warningFilePath, "# 警告対象のユーザー名を1行1名で記入してください\n# #で始まる行はコメントです\n");
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザーリスト読み込みエラー: {ex.Message}");
			}
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
		/// 警告ユーザーリストを再読み込み
		/// </summary>
		public void ReloadWarningUsers()
		{
			LoadWarningUsers();
		}


		/// <summary>
		/// 現在ロードしている警告対象ユーザーリストを取得
		/// </summary>
		public HashSet<string> GetWarningUsers()
		{
			return new HashSet<string>(warningUsers);
		}


		/// <summary>
		/// 警告ユーザーを追加する
		/// </summary>
		public bool AddWarningUser(string playerName)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(playerName))
					return false;

				string normalizedName = playerName.ToLowerInvariant().Trim();
				
				// 既に登録済みの場合
				if (warningUsers.Contains(normalizedName))
					return false;

				// メモリに追加
				warningUsers.Add(normalizedName);

				// ファイルに追記
				string warningFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warn_user.txt");
				File.AppendAllText(warningFilePath, $"\n{playerName}");

				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザーを追加: {playerName}");
				return true;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー追加エラー: {ex.Message}");
				return false;
			}
		}


		/// <summary>
		/// 警告ユーザーを削除する
		/// </summary>
		public bool RemoveWarningUser(string playerName)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(playerName))
					return false;

				string normalizedName = playerName.ToLowerInvariant().Trim();
				
				if (!warningUsers.Contains(normalizedName))
					return false;

				// メモリから削除
				warningUsers.Remove(normalizedName);

				// ファイルを更新
				string warningFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "warn_user.txt");
				if (File.Exists(warningFilePath))
				{
					var lines = File.ReadAllLines(warningFilePath)
						.Where(line => {
							var trimmed = line.Trim();
							if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
								return true; // コメントや空行は保持
							return trimmed.ToLowerInvariant() != normalizedName;
						})
						.ToArray();
					File.WriteAllLines(warningFilePath, lines);
				}

				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザーを削除: {playerName}");
				return true;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[WARNING] 警告ユーザー削除エラー: {ex.Message}");
				return false;
			}
		}
	}
}
