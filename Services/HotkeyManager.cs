using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ToNStatTool.Services
{
	/// <summary>
	/// グローバルホットキー（他アプリにフォーカスがあっても効くキー）を管理する。
	/// 登録先ウィンドウのWndProcで<see cref="TryHandleMessage"/>を呼ぶことで発火する。
	/// </summary>
	public sealed class HotkeyManager : IDisposable
	{
		private const int WM_HOTKEY = 0x0312;

		private const uint MOD_ALT = 0x0001;
		private const uint MOD_CONTROL = 0x0002;
		private const uint MOD_SHIFT = 0x0004;
		private const uint MOD_WIN = 0x0008;
		private const uint MOD_NOREPEAT = 0x4000;

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		private readonly IWin32Window owner;
		private readonly Dictionary<int, Action> handlers = new Dictionary<int, Action>();
		private bool disposed;

		public HotkeyManager(IWin32Window owner)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
		}

		/// <summary>
		/// ホットキーを登録する。登録できた場合のみtrueを返す
		/// （他アプリが同じキーを押さえている場合はfalse）
		/// </summary>
		/// <param name="id">1〜0xBFFFの範囲で一意なID</param>
		/// <param name="hotkeyText">"Ctrl+Shift+T" 形式の文字列</param>
		/// <param name="handler">押下時に実行する処理</param>
		public bool Register(int id, string hotkeyText, Action handler)
		{
			if (disposed) return false;
			if (handler == null) return false;

			if (!TryParse(hotkeyText, out uint modifiers, out Keys key))
			{
				Logger.Warn("Hotkey", $"ホットキーの書式を解釈できません: '{hotkeyText}'");
				return false;
			}

			// 同じIDが残っていると登録に失敗するため、先に解除しておく
			Unregister(id);

			if (!RegisterHotKey(owner.Handle, id, modifiers | MOD_NOREPEAT, (uint)key))
			{
				int error = Marshal.GetLastWin32Error();
				Logger.Warn("Hotkey", $"ホットキー登録に失敗: '{hotkeyText}' (Win32 error={error}。他アプリが使用中の可能性)");
				return false;
			}

			handlers[id] = handler;
			Logger.Info("Hotkey", $"ホットキーを登録: '{hotkeyText}' (id={id})");
			return true;
		}

		/// <summary>
		/// 指定IDのホットキーを解除する
		/// </summary>
		public void Unregister(int id)
		{
			if (handlers.Remove(id))
			{
				Logger.Debug("Hotkey", $"ホットキーを解除 (id={id})");
			}

			try
			{
				UnregisterHotKey(owner.Handle, id);
			}
			catch (Exception ex)
			{
				Logger.Debug("Hotkey", $"ホットキー解除エラー: {ex.Message}");
			}
		}

		/// <summary>
		/// 登録済みのホットキーをすべて解除する
		/// </summary>
		public void UnregisterAll()
		{
			foreach (int id in handlers.Keys.ToList())
			{
				Unregister(id);
			}
			handlers.Clear();
		}

		/// <summary>
		/// WndProcから呼ぶ。ホットキーメッセージを処理した場合はtrueを返す
		/// </summary>
		public bool TryHandleMessage(ref Message m)
		{
			if (disposed || m.Msg != WM_HOTKEY) return false;

			int id = m.WParam.ToInt32();
			if (!handlers.TryGetValue(id, out Action handler)) return false;

			try
			{
				handler();
			}
			catch (Exception ex)
			{
				Logger.Error("Hotkey", $"ホットキー処理エラー (id={id})", ex);
			}

			return true;
		}

		/// <summary>
		/// "Ctrl+Shift+T" のような文字列を修飾キーとキーコードに分解する
		/// </summary>
		public static bool TryParse(string hotkeyText, out uint modifiers, out Keys key)
		{
			modifiers = 0;
			key = Keys.None;

			if (string.IsNullOrWhiteSpace(hotkeyText)) return false;

			foreach (string rawPart in hotkeyText.Split('+'))
			{
				string part = rawPart.Trim();
				if (part.Length == 0) continue;

				switch (part.ToLowerInvariant())
				{
					case "ctrl":
					case "control":
						modifiers |= MOD_CONTROL;
						continue;
					case "shift":
						modifiers |= MOD_SHIFT;
						continue;
					case "alt":
						modifiers |= MOD_ALT;
						continue;
					case "win":
					case "windows":
						modifiers |= MOD_WIN;
						continue;
				}

				// 修飾キー以外は本体キーとして解釈する（最後に出てきたものを採用）
				if (Enum.TryParse(part, true, out Keys parsed) && parsed != Keys.None)
				{
					key = parsed;
				}
				else if (part.Length == 1 && char.IsDigit(part[0]))
				{
					// "1" のような数字は Keys.D1 として扱う
					key = (Keys)Enum.Parse(typeof(Keys), "D" + part);
				}
				else
				{
					return false;
				}
			}

			// 修飾キーなしのホットキーは誤爆しやすいので許可しない
			return key != Keys.None && modifiers != 0;
		}

		/// <summary>
		/// ホットキー文字列として妥当かどうか
		/// </summary>
		public static bool IsValid(string hotkeyText)
		{
			return TryParse(hotkeyText, out _, out _);
		}

		public void Dispose()
		{
			if (disposed) return;

			UnregisterAll();
			disposed = true;
		}
	}
}
