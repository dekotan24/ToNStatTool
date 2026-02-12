using System.Windows.Forms;

namespace ToNStatTool.Controls
{
	/// <summary>
	/// ダブルバッファリングを有効にしたListView
	/// ちらつきを軽減するためのカスタムコントロール
	/// </summary>
	public class DoubleBufferedListView : ListView
	{
		public DoubleBufferedListView()
		{
			// ダブルバッファリングを有効化
			this.DoubleBuffered = true;
			
			// 追加の最適化フラグ
			this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
			this.UpdateStyles();
		}
	}
}
