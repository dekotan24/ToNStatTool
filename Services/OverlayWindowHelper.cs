using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ToNStatTool.Services
{
	/// <summary>
	/// オーバーレイウィンドウ（テラー表示ウィンドウ）まわりのWin32ヘルパー。
	/// クリックスルーの切替と、保存位置が画面内かどうかの判定を提供する。
	/// </summary>
	public static class OverlayWindowHelper
	{
		private const int GWL_EXSTYLE = -20;
		private const int WS_EX_LAYERED = 0x00080000;
		private const int WS_EX_TRANSPARENT = 0x00000020;

		[DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
		private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
		private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
		private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

		[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
		private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

		// AnyCPUで64bit実行される場合があるため、ポインタ幅で呼び分ける
		private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
		{
			return IntPtr.Size == 8
				? GetWindowLongPtr64(hWnd, nIndex)
				: new IntPtr(GetWindowLong32(hWnd, nIndex));
		}

		private static void SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value)
		{
			if (IntPtr.Size == 8)
			{
				SetWindowLongPtr64(hWnd, nIndex, value);
			}
			else
			{
				SetWindowLong32(hWnd, nIndex, value.ToInt32());
			}
		}

		/// <summary>
		/// 指定ウィンドウのクリックスルー（マウス操作の透過）を切り替える
		/// </summary>
		public static void SetClickThrough(IWin32Window window, bool enabled)
		{
			if (window == null) return;

			try
			{
				IntPtr handle = window.Handle;
				if (handle == IntPtr.Zero) return;

				long exStyle = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();

				if (enabled)
				{
					exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
				}
				else
				{
					// LAYEREDは透明度指定でも使われるため、TRANSPARENTだけを落とす
					exStyle &= ~(long)WS_EX_TRANSPARENT;
				}

				SetWindowLongPtr(handle, GWL_EXSTYLE, new IntPtr(exStyle));
				Logger.Debug("Overlay", $"クリックスルーを{(enabled ? "有効" : "無効")}化");
			}
			catch (Exception ex)
			{
				Logger.Error("Overlay", "クリックスルー切替エラー", ex);
			}
		}

		/// <summary>
		/// 指定した矩形がいずれかのディスプレイと十分に重なっているか判定する。
		/// モニタ構成が変わって画面外に取り残された位置を弾くために使う。
		/// </summary>
		public static bool IsBoundsVisible(Rectangle bounds)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return false;

			foreach (Screen screen in Screen.AllScreens)
			{
				Rectangle intersect = Rectangle.Intersect(screen.WorkingArea, bounds);

				// ドラッグして戻せる程度に見えていればOKとする
				if (intersect.Width >= 80 && intersect.Height >= 30)
				{
					return true;
				}
			}

			return false;
		}
	}
}
