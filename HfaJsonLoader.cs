using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace ToNStatTool
{
	/// <summary>
	/// Homefield Advantage（HFA）情報をJSONファイルから読み込むクラス
	/// </summary>
	public static class HfaJsonLoader
	{
		// マップ名 → テラー名リスト
		private static Dictionary<string, List<string>> hfaByMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		// テラー名 → マップ名リスト（逆引き用）
		private static Dictionary<string, List<string>> hfaByTerror = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		private static bool isLoaded = false;

		/// <summary>
		/// JSONファイルからHFA情報を読み込む
		/// </summary>
		public static void LoadHfaData()
		{
			if (isLoaded) return;

			try
			{
				string executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				string jsonPath = Path.Combine(executablePath, "hfaInfo.json");

				if (!File.Exists(jsonPath))
				{
					System.Diagnostics.Debug.WriteLine($"HFA JSONファイルが見つかりません: {jsonPath}");
					isLoaded = true;
					return;
				}

				string jsonContent = File.ReadAllText(jsonPath);
				var data = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonContent)
					?? new Dictionary<string, List<string>>();

				// 大文字小文字を区別しない辞書に変換
				hfaByMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
				hfaByTerror = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

				foreach (var kvp in data)
				{
					string mapName = kvp.Key;
					hfaByMap[mapName] = kvp.Value;

					// 逆引き辞書を構築
					foreach (var terrorName in kvp.Value)
					{
						if (!hfaByTerror.ContainsKey(terrorName))
						{
							hfaByTerror[terrorName] = new List<string>();
						}
						hfaByTerror[terrorName].Add(mapName);
					}
				}

				isLoaded = true;
				System.Diagnostics.Debug.WriteLine($"HFA情報を読み込みました: {hfaByMap.Count}マップ, {hfaByTerror.Count}テラー");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"HFA JSON読み込みエラー: {ex.Message}");
				isLoaded = true;
			}
		}

		/// <summary>
		/// 指定されたテラーが指定されたマップでHFAを持つかどうかを判定
		/// </summary>
		/// <param name="terrorName">テラー名</param>
		/// <param name="mapName">マップ名</param>
		/// <returns>HFAを持つ場合true</returns>
		public static bool HasHfa(string terrorName, string mapName)
		{
			if (!isLoaded)
				LoadHfaData();

			if (string.IsNullOrEmpty(terrorName) || string.IsNullOrEmpty(mapName))
				return false;

			string cleanMapName = CleanMapName(mapName);

			if (hfaByMap.TryGetValue(cleanMapName, out List<string> terrors))
			{
				return terrors.Any(t => string.Equals(t, terrorName, StringComparison.OrdinalIgnoreCase));
			}

			return false;
		}

		/// <summary>
		/// 指定されたテラーがHFAを持つマップのリストを取得
		/// </summary>
		/// <param name="terrorName">テラー名</param>
		/// <returns>HFAを持つマップ名のリスト</returns>
		public static List<string> GetHfaMaps(string terrorName)
		{
			if (!isLoaded)
				LoadHfaData();

			if (string.IsNullOrEmpty(terrorName))
				return new List<string>();

			if (hfaByTerror.TryGetValue(terrorName, out List<string> maps))
			{
				return new List<string>(maps);
			}

			return new List<string>();
		}

		/// <summary>
		/// 指定されたマップでHFAを持つテラーのリストを取得
		/// </summary>
		/// <param name="mapName">マップ名</param>
		/// <returns>HFAを持つテラー名のリスト</returns>
		public static List<string> GetHfaTerrors(string mapName)
		{
			if (!isLoaded)
				LoadHfaData();

			if (string.IsNullOrEmpty(mapName))
				return new List<string>();

			string cleanMapName = CleanMapName(mapName);

			if (hfaByMap.TryGetValue(cleanMapName, out List<string> terrors))
			{
				return new List<string>(terrors);
			}

			return new List<string>();
		}

		/// <summary>
		/// マップ名からサフィックス（角括弧、丸括弧）を除去
		/// </summary>
		private static string CleanMapName(string mapName)
		{
			if (string.IsNullOrEmpty(mapName))
				return mapName;

			string result = mapName;

			// 角括弧を先に処理（元ネタゲーム名）
			int bracketIndex = result.IndexOf('[');
			if (bracketIndex > 0)
				result = result.Substring(0, bracketIndex).Trim();

			// 丸括弧を処理（作者名など）
			int parenIndex = result.IndexOf('(');
			if (parenIndex > 0)
				result = result.Substring(0, parenIndex).Trim();

			return result;
		}
	}
}
