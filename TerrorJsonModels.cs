using System.Collections.Generic;
using Newtonsoft.Json;

namespace ToNStatTool
{
	/// <summary>
	/// JSONから読み込むテラー情報のルートクラス
	/// </summary>
	public class TerrorJsonData
	{
		// テラー名をキー、特性リストを値とする辞書
		[JsonExtensionData]
		public Dictionary<string, List<Dictionary<string, string>>> Terrors { get; set; } = new Dictionary<string, List<Dictionary<string, string>>>();
	}

	/// <summary>
	/// テラーの詳細情報を保持するクラス
	/// </summary>
	public class TerrorDetailInfo
	{
		public string Name { get; set; }
		public TerrorStunType StunType { get; set; }
		public List<TerrorTrait> Traits { get; set; } = new List<TerrorTrait>();
	}

	/// <summary>
	/// テラーの特性を表すクラス
	/// </summary>
	public class TerrorTrait
	{
		public string TraitType { get; set; }
		public string Description { get; set; }
		public TerrorTraitCategory Category { get; set; }
	}

	/// <summary>
	/// テラー特性のカテゴリー
	/// </summary>
	public enum TerrorTraitCategory
	{
		Movement,      // 移動関連（追跡型、徘徊型など）
		Attack,        // 攻撃関連（即死、デバフなど）
		Special,       // 特殊能力（テレポート、召喚など）
		Speed,         // 速度関連
		Counter,       // カウンター関連
		Other          // その他
	}
}