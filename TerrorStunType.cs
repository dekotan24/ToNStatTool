namespace ToNStatTool
{
	/// <summary>
	/// テラーのスタン可否状態を表す列挙型
	/// </summary>
	public enum TerrorStunType
	{
		/// <summary>
		/// 緑 - スタン可能
		/// </summary>
		Safe,

		/// <summary>
		/// 黄 - 制限あり
		/// </summary>
		Caution,

		/// <summary>
		/// 赤 - スタン厳禁
		/// </summary>
		Forbidden,

		/// <summary>
		/// 灰 - スタン効果なし
		/// </summary>
		Ineffective,

		/// <summary>
		/// 紫 - スタン可否不明
		/// </summary>
		Unknown
	}
}