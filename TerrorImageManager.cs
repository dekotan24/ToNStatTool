using System;
using System.Collections.Generic;
using System.Drawing;
using System.Resources;

namespace ToNStatTool
{
	/// <summary>
	/// テラー画像を管理するクラス
	/// </summary>
	public static class TerrorImageManager
	{
		private static readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
		private static readonly ResourceManager resourceManager = Properties.Resources.ResourceManager;

		/// <summary>
		/// テラー名に対応する画像を取得する（新しいインスタンスを返す）
		/// </summary>
		/// <param name="terrorName">テラー名</param>
		/// <param name="width">画像の幅</param>
		/// <param name="height">画像の高さ</param>
		/// <returns>テラー画像（見つからない場合はプレースホルダー）</returns>
		public static Image GetTerrorImage(string terrorName, int width, int height)
		{
			if (string.IsNullOrEmpty(terrorName))
			{
				return CreatePlaceholderImage("?", width, height);
			}

			// キャッシュをチェック
			string cacheKey = $"{terrorName}_{width}x{height}";
			if (imageCache.ContainsKey(cacheKey))
			{
				// キャッシュされた画像のコピーを作成して返す
				return CloneImage(imageCache[cacheKey]);
			}

			Image image = null;

			// リソースから画像を取得
			try
			{
				// テラー名をリソース名に変換
				string resourceName = ConvertToResourceName(terrorName);
				var resourceImage = resourceManager.GetObject(resourceName) as Image;

				if (resourceImage != null)
				{
					// サイズを調整してキャッシュに保存
					image = ResizeImage(resourceImage, width, height);
					imageCache[cacheKey] = CloneImage(image); // キャッシュにはコピーを保存
					return image;
				}

				// 別名でも試す（例: "The Painter" と "ThePainter"）
				string alternativeName = terrorName.Replace(" ", "");
				resourceImage = resourceManager.GetObject(alternativeName) as Image;

				if (resourceImage != null)
				{
					image = ResizeImage(resourceImage, width, height);
					imageCache[cacheKey] = CloneImage(image); // キャッシュにはコピーを保存
					return image;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"テラー画像の読み込みエラー: {terrorName} - {ex.Message}");
			}

			// 画像が見つからない場合はプレースホルダーを生成
			image = CreatePlaceholderImage(terrorName, width, height);
			imageCache[cacheKey] = CloneImage(image); // キャッシュにはコピーを保存
			return image;
		}

		/// <summary>
		/// 画像のクローンを作成する
		/// </summary>
		private static Image CloneImage(Image originalImage)
		{
			if (originalImage == null) return null;

			var clonedImage = new Bitmap(originalImage.Width, originalImage.Height);
			using (var graphics = Graphics.FromImage(clonedImage))
			{
				graphics.DrawImage(originalImage, 0, 0);
			}
			return clonedImage;
		}

		/// <summary>
		/// テラー名をリソース名に変換する
		/// </summary>
		private static string ConvertToResourceName(string terrorName)
		{
			return terrorName
				.Replace(" ", "_")
				.Replace("'", "")
				.Replace(".", "")
				.Replace("&", "and")
				.Replace("-", "_")
				.Replace("!", "")
				.Replace("?", "")
				.Replace(":", "");
		}

		/// <summary>
		/// 画像をリサイズする
		/// </summary>
		private static Image ResizeImage(Image originalImage, int width, int height)
		{
			var resizedImage = new Bitmap(width, height);
			using (var graphics = Graphics.FromImage(resizedImage))
			{
				graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
				graphics.DrawImage(originalImage, 0, 0, width, height);
			}
			return resizedImage;
		}

		/// <summary>
		/// プレースホルダー画像を生成する
		/// </summary>
		private static Image CreatePlaceholderImage(string terrorName, int width, int height)
		{
			var bitmap = new Bitmap(width, height);
			using (var graphics = Graphics.FromImage(bitmap))
			{
				graphics.FillRectangle(Brushes.DarkGray, 0, 0, width, height);

				// テラー名の最初の文字を表示
				string initial = string.IsNullOrEmpty(terrorName) ? "?" : terrorName.Substring(0, 1).ToUpper();

				// フォントサイズを画像サイズに合わせて調整
				int fontSize = Math.Min(width, height) / 2;
				using (var font = new Font("Arial", fontSize, FontStyle.Bold))
				{
					var textSize = graphics.MeasureString(initial, font);
					graphics.DrawString(initial, font, Brushes.White,
						(width - textSize.Width) / 2, (height - textSize.Height) / 2);
				}
			}
			return bitmap;
		}

		/// <summary>
		/// キャッシュをクリアする
		/// </summary>
		public static void ClearCache()
		{
			foreach (var image in imageCache.Values)
			{
				image?.Dispose();
			}
			imageCache.Clear();
		}
	}
}