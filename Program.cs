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
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			TerrorJsonLoader.LoadTerrorData();
			Application.Run(new ToNStatTool());
			MediaFoundationApi.Startup();
		}
	}
}