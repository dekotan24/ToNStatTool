using System;
using System.Windows.Forms;
using ToNStatTool.Services;

namespace ToNStatTool
{
	// テラー表示ウィンドウ（オーバーレイ）まわりの制御
	// ・クリックスルー
	// ・グローバルホットキー
	// ・位置とサイズの永続化
	public partial class ToNStatTool
	{
		private const int HOTKEY_ID_TOGGLE_OVERLAY = 0xA001;
		private const int HOTKEY_ID_TOGGLE_CLICKTHROUGH = 0xA002;

		private HotkeyManager hotkeyManager;

		/// <summary>
		/// オーバーレイ関連機能を初期化する（コンストラクタから呼ぶ）
		/// </summary>
		private void InitializeOverlayFeatures()
		{
			hotkeyManager = new HotkeyManager(this);
			ApplyOverlaySettings();
		}

		/// <summary>
		/// 設定内容をオーバーレイ機能に反映する（起動時・設定保存後に呼ぶ）
		/// </summary>
		/// <param name="notifyHotkeyFailure">ホットキー登録に失敗したときダイアログで知らせるか</param>
		private void ApplyOverlaySettings(bool notifyHotkeyFailure = false)
		{
			if (appSettings == null) return;

			if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
			{
				terrorDisplayForm.SetClickThrough(appSettings.TerrorFormClickThrough);
			}

			var failed = ApplyHotkeySettings();

			// 起動時は黙ってログに残すだけ。設定を保存した直後だけは気づけるように伝える
			if (notifyHotkeyFailure && failed.Count > 0)
			{
				MessageBox.Show(
					"次のホットキーを登録できませんでした。\n他のアプリが同じキーを使用している可能性があります。\n\n"
						+ string.Join("\n", failed),
					"ホットキー登録エラー",
					MessageBoxButtons.OK,
					MessageBoxIcon.Warning);
			}
		}

		/// <summary>
		/// ホットキーを登録し直す。登録できなかったホットキーの一覧を返す
		/// </summary>
		private System.Collections.Generic.List<string> ApplyHotkeySettings()
		{
			var failed = new System.Collections.Generic.List<string>();
			if (hotkeyManager == null) return failed;

			hotkeyManager.UnregisterAll();

			if (!appSettings.OverlayHotkeyEnabled) return failed;

			// 登録に失敗しても他アプリと競合しているだけなので、記録して続行する
			if (!string.IsNullOrWhiteSpace(appSettings.OverlayToggleHotkey))
			{
				if (!hotkeyManager.Register(HOTKEY_ID_TOGGLE_OVERLAY, appSettings.OverlayToggleHotkey, ToggleOverlayVisible))
				{
					failed.Add($"表示ON/OFF: {appSettings.OverlayToggleHotkey}");
				}
			}

			if (!string.IsNullOrWhiteSpace(appSettings.ClickThroughHotkey))
			{
				if (!hotkeyManager.Register(HOTKEY_ID_TOGGLE_CLICKTHROUGH, appSettings.ClickThroughHotkey, ToggleOverlayClickThrough))
				{
					failed.Add($"クリックスルー: {appSettings.ClickThroughHotkey}");
				}
			}

			return failed;
		}

		/// <summary>
		/// オーバーレイの表示/非表示を切り替える（ホットキー用）
		/// </summary>
		private void ToggleOverlayVisible()
		{
			var checkBox = FindControl("buttonTerrorWindow") as CheckBox;
			if (checkBox == null || checkBox.IsDisposed) return;

			checkBox.Checked = !checkBox.Checked;
			Logger.Info("Overlay", $"ホットキーでオーバーレイを{(checkBox.Checked ? "表示" : "非表示")}");
		}

		/// <summary>
		/// クリックスルーを切り替える（ホットキー用）。設定にも反映して次回起動に引き継ぐ
		/// </summary>
		private void ToggleOverlayClickThrough()
		{
			if (appSettings == null) return;

			appSettings.TerrorFormClickThrough = !appSettings.TerrorFormClickThrough;

			if (terrorDisplayForm != null && !terrorDisplayForm.IsDisposed)
			{
				terrorDisplayForm.SetClickThrough(appSettings.TerrorFormClickThrough);
			}

			PersistOverlaySettings(settings =>
			{
				settings.TerrorFormClickThrough = appSettings.TerrorFormClickThrough;
			});

			Logger.Info("Overlay", $"ホットキーでクリックスルーを{(appSettings.TerrorFormClickThrough ? "有効" : "無効")}化");
		}

		/// <summary>
		/// オーバーレイの現在位置・サイズを保存する（ドラッグ/リサイズ確定時と閉じるとき）
		/// </summary>
		private void SaveOverlayBounds()
		{
			if (terrorDisplayForm == null || terrorDisplayForm.IsDisposed) return;
			if (appSettings == null || !appSettings.RememberTerrorFormBounds) return;

			// メモリ上の設定にも反映しておく（次に開き直したときに同じ位置で出す）
			terrorDisplayForm.SaveBoundsTo(appSettings);

			PersistOverlaySettings(settings => terrorDisplayForm.SaveBoundsTo(settings));
		}

		/// <summary>
		/// 設定ファイルを読み直して一部だけ書き換えて保存する。
		/// 他の設定（設定画面で変更された値など）を巻き戻さないための共通処理
		/// </summary>
		private void PersistOverlaySettings(Action<AppSettings> mutate)
		{
			try
			{
				var settings = AppSettings.Load();
				mutate(settings);
				settings.Save();
			}
			catch (Exception ex)
			{
				Logger.Error("Overlay", "オーバーレイ設定の保存に失敗", ex);
			}
		}

		/// <summary>
		/// オーバーレイ生成直後に、保存済みの位置とイベントを結び付ける（Show前に呼ぶ）
		/// </summary>
		private void SetupOverlayWindow(TerrorDisplayForm form)
		{
			if (form == null) return;

			form.ApplySavedBounds(appSettings);
			form.BoundsUserChanged += SaveOverlayBounds;
			form.FormClosing += (s, e) => SaveOverlayBounds();
		}

		/// <summary>
		/// オーバーレイ表示直後（ハンドル作成後）に適用するウィンドウスタイル
		/// </summary>
		private void ApplyOverlayWindowStyles(TerrorDisplayForm form)
		{
			if (form == null || form.IsDisposed || appSettings == null) return;

			form.SetClickThrough(appSettings.TerrorFormClickThrough);
		}

		protected override void WndProc(ref Message m)
		{
			// グローバルホットキーはここで拾う
			if (hotkeyManager != null && hotkeyManager.TryHandleMessage(ref m))
			{
				return;
			}

			base.WndProc(ref m);
		}

		/// <summary>
		/// オーバーレイ関連リソースを解放する（フォーム終了時）
		/// </summary>
		private void DisposeOverlayFeatures()
		{
			hotkeyManager?.Dispose();
			hotkeyManager = null;
		}
	}
}
