using NAudio.MediaFoundation;
using System;
using System.Windows.Forms;

namespace ToNStatTool
{
	/// <summary>
	/// プログラムのエントリーポイント
	/// </summary>
	static class Program
	{
		/// <summary>
		/// アプリケーションのメイン エントリ ポイントです。
		/// </summary>
		[STAThread]
		static void Main()
		{
			// ロガーを初期化
			Logger.Initialize();

			try
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);
				TerrorJsonLoader.LoadTerrorData();
				MediaFoundationApi.Startup();
				Application.Run(new ToNStatTool());
			}
			finally
			{
				// ロガーをシャットダウン
				Logger.Shutdown();
			}
		}
	}
}