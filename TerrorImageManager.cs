using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ToNStatTool
{
	/// <summary>
	/// テラー画像を管理するクラス
	/// 外部の images フォルダから画像を読み込む
	/// </summary>
	public static class TerrorImageManager
	{
		private static readonly Dictionary<string, Image> imageCache = new Dictionary<string, Image>();
		private static string _imagesFolder = null;

		/// <summary>
		/// 画像フォルダのパスを取得
		/// </summary>
		private static string ImagesFolder
		{
			get
			{
				if (_imagesFolder == null)
				{
					string exeDir = AppDomain.CurrentDomain.BaseDirectory;
					_imagesFolder = Path.Combine(exeDir, "images");
					Logger.Info("TerrorImage", $"画像フォルダパスを初期化: {_imagesFolder}");
					Logger.Debug("TerrorImage", $"BaseDirectory: {exeDir}");
				}
				return _imagesFolder;
			}
		}

		/// <summary>
		/// 画像フォルダが存在するかどうか
		/// </summary>
		public static bool ImagesAvailable => Directory.Exists(ImagesFolder);

		/// <summary>
		/// テラー名に対応する画像を取得する（新しいインスタンスを返す）
		/// </summary>
		/// <param name="terrorName">テラー名</param>
		/// <param name="width">画像の幅</param>
		/// <param name="height">画像の高さ</param>
		/// <returns>テラー画像（見つからない場合はプレースホルダー）</returns>
		public static Image GetTerrorImage(string terrorName, int width, int height)
		{
			Logger.Debug("TerrorImage", $"画像取得リクエスト: テラー名='{terrorName}', サイズ={width}x{height}");
			
			if (string.IsNullOrEmpty(terrorName))
			{
				Logger.Debug("TerrorImage", "テラー名が空のため、プレースホルダーを返します");
				return CreatePlaceholderImage("?", width, height);
			}

			// キャッシュをチェック
			string cacheKey = $"{terrorName}_{width}x{height}";
			if (imageCache.ContainsKey(cacheKey))
			{
				Logger.Debug("TerrorImage", $"キャッシュヒット: {terrorName}");
				// キャッシュされた画像のコピーを作成して返す
				return CloneImage(imageCache[cacheKey]);
			}

			Image image = null;

			// 外部ファイルから画像を取得
			try
			{
				image = LoadImageFromFile(terrorName, width, height);
				if (image != null)
				{
					Logger.Info("TerrorImage", $"画像読み込み成功: {terrorName}");
					imageCache[cacheKey] = CloneImage(image);
					return image;
				}
				else
				{
					Logger.Warn("TerrorImage", $"画像ファイルが見つかりませんでした: {terrorName}");
				}
			}
			catch (Exception ex)
			{
				Logger.Error("TerrorImage", $"画像読み込みエラー: {terrorName} - {ex.Message}");
				Logger.Debug("TerrorImage", $"スタックトレース: {ex.StackTrace}");
			}

			// 画像が見つからない場合はプレースホルダーを生成
			Logger.Info("TerrorImage", $"プレースホルダー画像を作成: {terrorName}");
			image = CreatePlaceholderImage(terrorName, width, height);
			imageCache[cacheKey] = CloneImage(image); // キャッシュにはコピーを保存
			return image;
		}

		// 画像フォルダ内のファイル一覧キャッシュ
		private static Dictionary<string, string> _fileNameCache = null;
		private static DateTime _fileCacheTime = DateTime.MinValue;
		private static readonly TimeSpan FileCacheExpiry = TimeSpan.FromSeconds(30);

		/// <summary>
		/// 外部ファイルから画像を読み込む
		/// </summary>
		private static Image LoadImageFromFile(string terrorName, int width, int height)
		{
			Logger.Debug("TerrorImage", $"LoadImageFromFile開始: {terrorName}");
			Logger.Debug("TerrorImage", $"ImagesFolder: {ImagesFolder}");
			
			if (!Directory.Exists(ImagesFolder))
			{
				Logger.Error("TerrorImage", $"画像フォルダが存在しません: {ImagesFolder}");
				return null;
			}

			// ファイル一覧をキャッシュから取得（または更新）
			var fileCache = GetFileNameCache();
			if (fileCache == null || fileCache.Count == 0)
			{
				Logger.Error("TerrorImage", "ファイルキャッシュが空です");
				return null;
			}
			
			Logger.Debug("TerrorImage", $"ファイルキャッシュに{fileCache.Count}個のファイルが登録されています");

			// ファイル名の候補を生成（拡張子なし、小文字）
			var candidates = GetFileNameCandidates(terrorName).ToList();
			Logger.Debug("TerrorImage", $"ファイル名候補: {string.Join(", ", candidates)}");

			foreach (var candidate in candidates)
			{
				// 大文字小文字を無視してマッチング
				string lowerCandidate = candidate.ToLowerInvariant();
				Logger.Debug("TerrorImage", $"候補'{candidate}'（小文字: '{lowerCandidate}'）を検索中...");
				
				if (fileCache.TryGetValue(lowerCandidate, out string actualFilePath))
				{
					Logger.Info("TerrorImage", $"ファイル見つかりました: {actualFilePath}");
					try
					{
						// ファイルをメモリに読み込んでからImageを作成（ファイルロックを防ぐ）
						using (var stream = new MemoryStream(File.ReadAllBytes(actualFilePath)))
						{
							using (var originalImage = Image.FromStream(stream))
							{
								Logger.Debug("TerrorImage", "画像ファイルの読み込みとリサイズに成功しました");
								return ResizeImage(originalImage, width, height);
							}
						}
					}
					catch (Exception ex)
					{
						Logger.Error("TerrorImage", $"画像ファイル読み込みエラー: {actualFilePath} - {ex.Message}");
					}
				}
				else
				{
					Logger.Debug("TerrorImage", $"候補'{lowerCandidate}'はキャッシュに見つかりませんでした");
				}
			}

			Logger.Warn("TerrorImage", $"すべての候補で画像が見つかりませんでした: {terrorName}");
			return null;
		}

		/// <summary>
		/// 画像フォルダ内のファイル一覧をキャッシュとして取得
		/// キーは拡張子なしファイル名（小文字）、値は実際のフルパス
		/// </summary>
		private static Dictionary<string, string> GetFileNameCache()
		{
			// キャッシュが有効期限内ならそのまま返す
			if (_fileNameCache != null && DateTime.Now - _fileCacheTime < FileCacheExpiry)
			{
				Logger.Debug("TerrorImage", $"ファイルキャッシュを再利用します ({_fileNameCache.Count}個のファイル)");
				return _fileNameCache;
			}

			Logger.Info("TerrorImage", $"画像フォルダをスキャン中: {ImagesFolder}");
			
			try
			{
				_fileNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
				{
					".png", ".jpg", ".jpeg", ".gif", ".bmp"
				};

				var allFiles = Directory.GetFiles(ImagesFolder);
				Logger.Debug("TerrorImage", $"フォルダ内の全ファイル数: {allFiles.Length}");
				
				foreach (var filePath in allFiles)
				{
					string ext = Path.GetExtension(filePath);
					if (supportedExtensions.Contains(ext))
					{
						// 拡張子なしのファイル名を小文字でキーにする
						string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
						
						// 重複がある場合は最初に見つかったものを優先
						if (!_fileNameCache.ContainsKey(fileNameWithoutExt))
						{
							_fileNameCache[fileNameWithoutExt] = filePath;
						}
					}
				}

				Logger.Info("TerrorImage", $"ファイルキャッシュに{_fileNameCache.Count}個の画像ファイルを登録しました");
				
				// 最初の10個をログに表示（デバッグ用）
				if (_fileNameCache.Count > 0)
				{
					var firstTen = _fileNameCache.Take(10).Select(kvp => kvp.Key).ToList();
					Logger.Debug("TerrorImage", $"登録されたファイル名（最初の10個）: {string.Join(", ", firstTen)}");
				}
				
				_fileCacheTime = DateTime.Now;
			}
			catch (Exception ex)
			{
				Logger.Error("TerrorImage", $"画像フォルダのスキャンエラー: {ex.Message}");
				Logger.Debug("TerrorImage", $"スタックトレース: {ex.StackTrace}");
				_fileNameCache = new Dictionary<string, string>();
			}

			return _fileNameCache;
		}

		/// <summary>
		/// テラー名からファイル名の候補を生成（拡張子なし）
		/// </summary>
		private static IEnumerable<string> GetFileNameCandidates(string terrorName)
		{
			// オリジナルのテラー名
			yield return terrorName;

			// & を and に置換したバージョン
			string withAnd = terrorName.Replace(" & ", "_and_").Replace("&", "_and_");
			if (withAnd != terrorName)
			{
				yield return withAnd;
				// さらにスペースをアンダースコアに変換
				string withAndUnderscored = withAnd.Replace(" ", "_");
				if (withAndUnderscored != withAnd)
				{
					yield return withAndUnderscored;
				}
			}

			// アンダースコア変換（スペース→アンダースコア）
			string underscored = ConvertToFileName(terrorName);
			if (underscored != terrorName)
			{
				yield return underscored;
			}

			// スペースなし
			string noSpace = terrorName.Replace(" ", "");
			if (noSpace != terrorName && noSpace != underscored)
			{
				yield return noSpace;
			}

			// ハイフン変換（アンダースコアの代わりにハイフン）
			string hyphenated = terrorName.Replace(" ", "-");
			if (hyphenated != terrorName)
			{
				yield return hyphenated;
			}
		}

		/// <summary>
		/// テラー名をファイル名に変換する
		/// a-zA-Z0-9_ 以外の文字をアンダースコアに置換
		/// </summary>
		private static string ConvertToFileName(string terrorName)
		{
			// a-zA-Z0-9_ 以外の文字を _ に置換
			return System.Text.RegularExpressions.Regex.Replace(terrorName, @"[^a-zA-Z0-9_]", "_");
		}

		/// <summary>
		/// ファイル名キャッシュをクリアする（画像フォルダの内容が変更された場合に呼び出す）
		/// </summary>
		public static void RefreshFileCache()
		{
			_fileNameCache = null;
			_fileCacheTime = DateTime.MinValue;
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
