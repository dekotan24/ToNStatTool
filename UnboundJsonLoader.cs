using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace ToNStatTool
{
	/// <summary>
	/// Unboundテラー情報をJSONファイルから読み込むクラス
	/// </summary>
	public static class UnboundJsonLoader
	{
		private static Dictionary<string, List<string>> unboundData = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		private static bool isLoaded = false;

		/// <summary>
		/// JSONファイルからUnboundテラー情報を読み込む
		/// </summary>
		public static void LoadUnboundData()
		{
			if (isLoaded) return;

			try
			{
				// 実行ファイルと同じディレクトリにあるJSONファイルを読み込む
				string executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				string jsonPath = Path.Combine(executablePath, "unboundInfo.json");

				if (!File.Exists(jsonPath))
				{
					System.Diagnostics.Debug.WriteLine($"Unbound JSONファイルが見つかりません: {jsonPath}");
					isLoaded = true; // 再度読み込みを試みないようにする
					return;
				}

				string jsonContent = File.ReadAllText(jsonPath);
				unboundData = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonContent)
					?? new Dictionary<string, List<string>>();

				// 大文字小文字を区別しない辞書に変換
				var caseInsensitiveData = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
				foreach (var kvp in unboundData)
				{
					caseInsensitiveData[kvp.Key] = kvp.Value;
				}
				unboundData = caseInsensitiveData;

				isLoaded = true;
				System.Diagnostics.Debug.WriteLine($"Unboundテラー情報を読み込みました: {unboundData.Count}件");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Unbound JSON読み込みエラー: {ex.Message}");
			}
		}

		/// <summary>
		/// Unbound名から内訳テラーリストを取得
		/// </summary>
		/// <param name="unboundName">Unboundのアナウンス名</param>
		/// <returns>内訳テラーのリスト（見つからない場合は空のリスト）</returns>
		public static List<string> GetUnboundTerrors(string unboundName)
		{
			if (!isLoaded)
				LoadUnboundData();

			if (string.IsNullOrEmpty(unboundName))
				return new List<string>();

			if (unboundData.TryGetValue(unboundName, out List<string> terrors))
			{
				return new List<string>(terrors); // コピーを返す
			}

			return new List<string>();
		}

		/// <summary>
		/// 指定された名前がUnboundテラーかどうかを判定
		/// </summary>
		/// <param name="terrorName">テラー名</param>
		/// <returns>Unboundテラーの場合true</returns>
		public static bool IsUnboundTerror(string terrorName)
		{
			if (!isLoaded)
				LoadUnboundData();

			if (string.IsNullOrEmpty(terrorName))
				return false;

			return unboundData.ContainsKey(terrorName);
		}

		/// <summary>
		/// すべてのUnboundテラー情報を取得
		/// </summary>
		public static Dictionary<string, List<string>> GetAllUnboundData()
		{
			if (!isLoaded)
				LoadUnboundData();

			return new Dictionary<string, List<string>>(unboundData);
		}

		/// <summary>
		/// 内訳テラーをユニークなテラー名と出現回数のペアで取得
		/// </summary>
		/// <param name="unboundName">Unboundのアナウンス名</param>
		/// <returns>テラー名と出現回数のディクショナリ</returns>
		public static Dictionary<string, int> GetUnboundTerrorsGrouped(string unboundName)
		{
			var terrors = GetUnboundTerrors(unboundName);
			return terrors.GroupBy(t => t)
				.ToDictionary(g => g.Key, g => g.Count());
		}
	}
}
