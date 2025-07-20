using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace ToNStatTool
{
	/// <summary>
	/// テラー情報をJSONファイルから読み込むクラス
	/// </summary>
	public static class TerrorJsonLoader
	{
		private static Dictionary<string, TerrorDetailInfo> terrorDetails = new Dictionary<string, TerrorDetailInfo>();
		private static bool isLoaded = false;

		/// <summary>
		/// JSONファイルからテラー情報を読み込む
		/// </summary>
		public static void LoadTerrorData()
		{
			if (isLoaded) return;

			try
			{
				// 実行ファイルと同じディレクトリにあるJSONファイルを読み込む
				string executablePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				string jsonPath = Path.Combine(executablePath, "terrorsInfo.json");

				if (!File.Exists(jsonPath))
				{
					System.Diagnostics.Debug.WriteLine($"JSONファイルが見つかりません: {jsonPath}");
					return;
				}

				string jsonContent = File.ReadAllText(jsonPath);
				var jsonData = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, string>>>>(jsonContent);

				ProcessJsonData(jsonData);
				isLoaded = true;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"JSON読み込みエラー: {ex.Message}");
			}
		}

		/// <summary>
		/// JSONデータを処理してTerrorDetailInfoに変換
		/// </summary>
		private static void ProcessJsonData(Dictionary<string, List<Dictionary<string, string>>> jsonData)
		{
			foreach (var terrorEntry in jsonData)
			{
				var terrorDetail = new TerrorDetailInfo
				{
					Name = terrorEntry.Key,
					StunType = TerrorStunType.Unknown,
					Traits = new List<TerrorTrait>()
				};

				foreach (var traitDict in terrorEntry.Value)
				{
					foreach (var trait in traitDict)
					{
						// スタン情報の処理（大文字小文字を区別しない）
						if (string.Equals(trait.Key, "スタン", StringComparison.OrdinalIgnoreCase))
						{
							terrorDetail.StunType = ParseStunType(trait.Value);
						}
						else
						{
							// その他の特性の処理
							var terrorTrait = new TerrorTrait
							{
								TraitType = trait.Key,
								Description = trait.Value,
								Category = CategorizeTraitType(trait.Key)
							};
							terrorDetail.Traits.Add(terrorTrait);
						}
					}
				}

				// テラー名の分割処理（Mona & The Mountainは例外）
				if (terrorEntry.Key == "Mona & The Mountain")
				{
					// このテラーは分割しない
					terrorDetails[terrorDetail.Name] = terrorDetail;
				}
				else
				{
					// 他のテラーは " & " で分割
					var splitNames = terrorEntry.Key.Split(new[] { " & " }, StringSplitOptions.RemoveEmptyEntries);

					foreach (var individualName in splitNames)
					{
						var individualTerror = new TerrorDetailInfo
						{
							Name = individualName.Trim(),
							StunType = terrorDetail.StunType,
							Traits = new List<TerrorTrait>(terrorDetail.Traits) // 特性のコピーを作成
						};
						terrorDetails[individualTerror.Name] = individualTerror;
					}
				}
			}
		}

		/// <summary>
		/// スタン文字列をTerrorStunTypeに変換（大文字小文字を区別しない）
		/// </summary>
		private static TerrorStunType ParseStunType(string stunText)
		{
			if (string.IsNullOrEmpty(stunText))
				return TerrorStunType.Unknown;

			stunText = stunText.ToLower();

			if (stunText.Contains("無効") || stunText.Contains("不可"))
				return TerrorStunType.Ineffective;
			else if (stunText.Contains("厳禁") || stunText.Contains("禁止") || stunText.Contains("非推奨"))
				return TerrorStunType.Forbidden;
			else if (stunText.Contains("注意") || stunText.Contains("カウンター") || stunText.Contains("条件"))
				return TerrorStunType.Caution;
			else if (stunText.Contains("有効") || stunText.Contains("推奨"))
				return TerrorStunType.Safe;
			else
				return TerrorStunType.Unknown;
		}

		/// <summary>
		/// 特性タイプをカテゴリーに分類
		/// </summary>
		private static TerrorTraitCategory CategorizeTraitType(string traitType)
		{
			traitType = traitType.ToLower();

			if (traitType.Contains("追跡") || traitType.Contains("徘徊") || traitType.Contains("壁貫通") || traitType.Contains("停止"))
				return TerrorTraitCategory.Movement;
			else if (traitType.Contains("即死") || traitType.Contains("デバフ") || traitType.Contains("ダメージ") || traitType.Contains("掴み"))
				return TerrorTraitCategory.Attack;
			else if (traitType.Contains("テレポート") || traitType.Contains("召喚") || traitType.Contains("変身") || traitType.Contains("複数"))
				return TerrorTraitCategory.Special;
			else if (traitType.Contains("速度") || traitType.Contains("加速"))
				return TerrorTraitCategory.Speed;
			else if (traitType.Contains("カウンター"))
				return TerrorTraitCategory.Counter;
			else
				return TerrorTraitCategory.Other;
		}

		/// <summary>
		/// テラー名から詳細情報を取得（大文字小文字を区別しない）
		/// </summary>
		public static TerrorDetailInfo GetTerrorDetail(string terrorName)
		{
			if (!isLoaded)
				LoadTerrorData();

			// 完全一致で検索
			if (terrorDetails.ContainsKey(terrorName))
				return terrorDetails[terrorName];

			// 大文字小文字を区別しない検索
			var foundEntry = terrorDetails.FirstOrDefault(kvp =>
				string.Equals(kvp.Key, terrorName, StringComparison.OrdinalIgnoreCase));

			if (!string.IsNullOrEmpty(foundEntry.Key))
			{
				return foundEntry.Value;
			}

			// 見つからない場合は空の情報を返す（スタン情報はTerrorConfigurationで別途処理）
			return new TerrorDetailInfo
			{
				Name = terrorName,
				StunType = TerrorStunType.Unknown, // GetTerrorStunTypeで後から設定される
				Traits = new List<TerrorTrait>()
			};
		}

		/// <summary>
		/// すべてのテラー詳細情報を取得
		/// </summary>
		public static Dictionary<string, TerrorDetailInfo> GetAllTerrorDetails()
		{
			if (!isLoaded)
				LoadTerrorData();

			return new Dictionary<string, TerrorDetailInfo>(terrorDetails);
		}
	}
}