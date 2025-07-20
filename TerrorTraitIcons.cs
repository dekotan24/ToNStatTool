using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;

namespace ToNStatTool
{
	/// <summary>
	/// テラー特性のアイコンを管理するクラス
	/// </summary>
	public static class TerrorTraitIcons
	{
		private static readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();

		/// <summary>
		/// 特性タイプに応じたアイコンを取得
		/// </summary>
		public static Image GetTraitIcon(string traitType, int size = 16)
		{
			string cacheKey = $"{traitType}_{size}";

			if (iconCache.ContainsKey(cacheKey))
				return iconCache[cacheKey];

			var icon = CreateTraitIcon(traitType, size);
			iconCache[cacheKey] = icon;
			return icon;
		}

		/// <summary>
		/// 特性タイプと説明に応じたアイコンを取得（説明付きバージョン）
		/// </summary>
		public static Image GetTraitIcon(string traitType, string description, int size = 16)
		{
			string cacheKey = $"{traitType}_{description}_{size}";

			if (iconCache.ContainsKey(cacheKey))
				return iconCache[cacheKey];

			var icon = CreateTraitIcon(traitType, description, size);
			iconCache[cacheKey] = icon;
			return icon;
		}

		/// <summary>
		/// 特性カテゴリーに応じたアイコンを作成
		/// </summary>
		private static Image CreateTraitIcon(string traitType, int size)
		{
			return CreateTraitIcon(traitType, "", size);
		}

		/// <summary>
		/// 特性カテゴリーに応じたアイコンを作成（説明付きバージョン）
		/// </summary>
		private static Image CreateTraitIcon(string traitType, string description, int size)
		{
			var bitmap = new Bitmap(size, size);
			using (var g = Graphics.FromImage(bitmap))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;

				string lowerType = traitType.ToLower();

				// 移動関連
				if (lowerType.Contains("追跡"))
				{
					DrawChaseIcon(g, size);
				}
				else if (lowerType.Contains("徘徊"))
				{
					DrawWanderIcon(g, size);
				}
				else if (lowerType.Contains("壁貫通"))
				{
					DrawWallPassIcon(g, size);
				}
				// 攻撃関連
				else if (lowerType.Contains("即死"))
				{
					DrawInstantKillIcon(g, size);
				}
				else if (lowerType.Contains("デバフ"))
				{
					DrawDebuffIcon(g, size);
				}
				else if (lowerType.Contains("掴み"))
				{
					DrawGrabIcon(g, size);
				}
				else if (lowerType.Contains("視界ダメージ") || lowerType.Contains("視認"))
				{
					DrawEyeIcon(g, size);
				}
				// 特殊能力
				else if (lowerType.Contains("テレポート"))
				{
					DrawTeleportIcon(g, size);
				}
				else if (lowerType.Contains("召喚"))
				{
					DrawSummonIcon(g, size);
				}
				else if (lowerType.Contains("複数"))
				{
					DrawMultipleIcon(g, size);
				}
				else if (lowerType.Contains("変身") || lowerType.Contains("形態"))
				{
					DrawTransformIcon(g, size);
				}
				else if (lowerType.Contains("停止"))
				{
					DrawStopIcon(g, size);
				}
				// 速度関連
				else if (lowerType.Contains("速度") || lowerType.Contains("加速"))
				{
					// 説明から最大速度を抽出して表示
					string maxSpeed = ExtractMaxSpeed(description);
					DrawSpeedIcon(g, size, maxSpeed);
				}
				// カウンター
				else if (lowerType.Contains("カウンター"))
				{
					DrawCounterIcon(g, size);
				}
				// スタン関連
				else if (lowerType.Contains("スタン"))
				{
					DrawStunIcon(g, size);
				}
				// その他
				else
				{
					DrawDefaultIcon(g, size);
				}
			}

			return bitmap;
		}

		/// <summary>
		/// 説明文から最大速度を抽出する（小数点対応）
		/// </summary>
		private static string ExtractMaxSpeed(string description)
		{
			if (string.IsNullOrEmpty(description))
				return "";

			// +数字.数字のパターンを探す（例: "+3.5", "+2.8"）
			var decimalMatches = Regex.Matches(description, @"\+(\d+\.\d+)");
			if (decimalMatches.Count > 0)
			{
				double maxSpeed = 0;
				foreach (Match match in decimalMatches)
				{
					if (double.TryParse(match.Groups[1].Value, out double speed))
					{
						maxSpeed = Math.Max(maxSpeed, speed);
					}
				}

				if (maxSpeed > 0)
				{
					// 小数点以下がある場合は「+」記号で表示
					if (maxSpeed % 1 != 0)
					{
						return ((int)maxSpeed) + "+";
					}
					else
					{
						return ((int)maxSpeed).ToString();
					}
				}
			}

			// +数字のパターンを探す（例: "+3", "+8以上", "+9程度"）
			var matches = Regex.Matches(description, @"\+(\d+)");
			if (matches.Count > 0)
			{
				int maxSpeed = 0;
				foreach (Match match in matches)
				{
					if (int.TryParse(match.Groups[1].Value, out int speed))
					{
						maxSpeed = Math.Max(maxSpeed, speed);
					}
				}

				if (maxSpeed > 0)
				{
					return maxSpeed.ToString();
				}
			}

			// 特定のキーワードを数値に変換
			if (description.Contains("超高速") || description.Contains("非常に速い"))
				return "9+";
			else if (description.Contains("高速"))
				return "6";
			else if (description.Contains("速い"))
				return "3";
			else if (description.Contains("遅い") || description.Contains("素手"))
				return "0";

			return "";
		}

		// 追跡アイコン（人型と矢印）
		private static void DrawChaseIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Red, 2))
			{
				// 人型の簡易シルエット
				g.DrawEllipse(pen, size / 4, 2, size / 6, size / 6); // 頭
				g.DrawLine(pen, size / 3, size / 3, size / 3, size * 2 / 3); // 体
																			 // 矢印
				g.DrawLine(pen, size / 2, size / 2, size - 3, size / 2);
				g.DrawLine(pen, size - 6, size / 2 - 3, size - 3, size / 2);
				g.DrawLine(pen, size - 6, size / 2 + 3, size - 3, size / 2);
			}
		}

		// 徘徊アイコン（ランダムな軌跡）
		private static void DrawWanderIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Blue, 1.5f))
			{
				Point[] points = {
					new Point(2, size / 2),
					new Point(size / 4, size / 4),
					new Point(size / 2, size * 3 / 4),
					new Point(size * 3 / 4, size / 3),
					new Point(size - 2, size * 2 / 3)
				};
				g.DrawCurve(pen, points);
			}
		}

		// 壁貫通アイコン（壁と通り抜ける矢印）
		private static void DrawWallPassIcon(Graphics g, int size)
		{
			using (var penWall = new Pen(Color.Gray, 3))
			using (var penArrow = new Pen(Color.Purple, 2))
			{
				// 壁
				g.DrawLine(penWall, size / 2, 2, size / 2, size - 2);
				// 貫通矢印
				g.DrawLine(penArrow, 2, size / 2, size - 2, size / 2);
				g.DrawLine(penArrow, size - 5, size / 2 - 3, size - 2, size / 2);
				g.DrawLine(penArrow, size - 5, size / 2 + 3, size - 2, size / 2);
			}
		}

		// 即死アイコン（ドクロ改良版）
		private static void DrawInstantKillIcon(Graphics g, int size)
		{
			using (var brush = new SolidBrush(Color.DarkRed))
			using (var whiteBrush = new SolidBrush(Color.White))
			{
				// ドクロの形
				g.FillEllipse(brush, size / 4, size / 4, size / 2, size / 3);
				// 目の穴
				g.FillEllipse(whiteBrush, size / 3, size / 3, size / 8, size / 8);
				g.FillEllipse(whiteBrush, size / 2, size / 3, size / 8, size / 8);
				// 鼻
				g.FillPolygon(whiteBrush, new Point[] {
					new Point(size * 7 / 16, size * 5 / 12),
					new Point(size * 9 / 16, size * 5 / 12),
					new Point(size / 2, size / 2)
				});
			}
		}

		// デバフアイコン（下向き矢印に波模様）
		private static void DrawDebuffIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Orange, 2))
			{
				// 下向き矢印
				g.DrawLine(pen, size / 2, 2, size / 2, size - 4);
				g.DrawLine(pen, size / 2 - 3, size - 7, size / 2, size - 4);
				g.DrawLine(pen, size / 2 + 3, size - 7, size / 2, size - 4);

				// 波模様（デバフ効果を表現）
				using (var thinPen = new Pen(Color.Orange, 1))
				{
					for (int i = 0; i < 3; i++)
					{
						int y = size / 4 + i * size / 8;
						g.DrawArc(thinPen, size / 2 + 2, y, size / 8, size / 16, 0, 180);
					}
				}
			}
		}

		// 掴みアイコン（手の形）
		private static void DrawGrabIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.DarkOrange, 2))
			using (var brush = new SolidBrush(Color.DarkOrange))
			{
				// 手のひら
				g.FillEllipse(brush, size / 3, size / 3, size / 3, size / 2);
				// 指
				for (int i = 0; i < 4; i++)
				{
					int x = size / 3 + i * size / 12;
					g.DrawLine(pen, x, size / 3, x, size / 6);
				}
			}
		}

		// 視界ダメージアイコン（目に波線）
		private static void DrawEyeIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Red, 1.5f))
			using (var brush = new SolidBrush(Color.White))
			{
				// 目の形
				g.FillEllipse(brush, size / 4, size / 3, size / 2, size / 4);
				g.DrawEllipse(pen, size / 4, size / 3, size / 2, size / 4);
				// 瞳
				g.FillEllipse(Brushes.Black, size * 5 / 12, size * 5 / 12, size / 6, size / 8);
				// ダメージ波線
				using (var redPen = new Pen(Color.Red, 1))
				{
					for (int i = 0; i < 2; i++)
					{
						int y = size / 2 + i * size / 8;
						g.DrawArc(redPen, size / 4, y, size / 2, size / 8, 0, 180);
					}
				}
			}
		}

		// テレポートアイコン（稲妻改良版）
		private static void DrawTeleportIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Cyan, 2))
			using (var brush = new SolidBrush(Color.Cyan))
			{
				// ジグザグの稲妻
				Point[] lightning = {
					new Point(size * 3 / 4, 2),
					new Point(size / 2, size / 3),
					new Point(size * 2 / 3, size / 2),
					new Point(size / 4, size - 2)
				};
				g.DrawLines(pen, lightning);

				// エフェクト（点々）
				for (int i = 0; i < 3; i++)
				{
					g.FillEllipse(brush, 2 + i * 3, size / 4 + i * 2, 2, 2);
					g.FillEllipse(brush, size - 8 + i * 2, size * 2 / 3 - i * 2, 2, 2);
				}
			}
		}

		// 召喚アイコン（魔法陣風）
		private static void DrawSummonIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Green, 2))
			using (var thinPen = new Pen(Color.Green, 1))
			{
				// 外側の円
				g.DrawEllipse(thinPen, 2, 2, size - 4, size - 4);
				// 十字
				g.DrawLine(pen, size / 2, 3, size / 2, size - 3);
				g.DrawLine(pen, 3, size / 2, size - 3, size / 2);
				// 対角線
				g.DrawLine(thinPen, size / 4, size / 4, size * 3 / 4, size * 3 / 4);
				g.DrawLine(thinPen, size * 3 / 4, size / 4, size / 4, size * 3 / 4);
			}
		}

		// 複数アイコン（3つの人影）
		private static void DrawMultipleIcon(Graphics g, int size)
		{
			using (var brush = new SolidBrush(Color.Blue))
			{
				int figureWidth = size / 6;
				int figureHeight = size / 3;

				// 3つの人影
				for (int i = 0; i < 3; i++)
				{
					int x = size / 6 + i * size / 4;
					int y = size / 3;

					// 頭
					g.FillEllipse(brush, x, y, figureWidth, figureWidth);
					// 体
					g.FillRectangle(brush, x, y + figureWidth, figureWidth, figureHeight);
				}
			}
		}

		// 変身アイコン（変化する形）
		private static void DrawTransformIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Purple, 2))
			{
				// 変化前（四角）
				g.DrawRectangle(pen, 2, size / 4, size / 3, size / 3);
				// 矢印
				g.DrawLine(pen, size / 3 + 2, size / 2, size * 2 / 3 - 2, size / 2);
				g.DrawLine(pen, size * 2 / 3 - 5, size / 2 - 2, size * 2 / 3 - 2, size / 2);
				g.DrawLine(pen, size * 2 / 3 - 5, size / 2 + 2, size * 2 / 3 - 2, size / 2);
				// 変化後（円）
				g.DrawEllipse(pen, size * 2 / 3, size / 4, size / 3 - 2, size / 3);
			}
		}

		// 停止アイコン（停止標識）
		private static void DrawStopIcon(Graphics g, int size)
		{
			using (var brush = new SolidBrush(Color.Red))
			using (var whiteBrush = new SolidBrush(Color.White))
			{
				// 八角形の停止標識
				g.FillEllipse(brush, 2, 2, size - 4, size - 4);
				// 白い「止」の文字風
				g.FillRectangle(whiteBrush, size / 3, size / 4, size / 3, size / 8);
				g.FillRectangle(whiteBrush, size * 5 / 12, size / 3, size / 6, size / 2);
			}
		}

		// 速度アイコン（>>と数値、改良版）
		private static void DrawSpeedIcon(Graphics g, int size, string speedText = "")
		{
			using (var brush = new SolidBrush(Color.Yellow))
			{
				// 背景の四角形
				g.FillRectangle(brush, 0, 0, size, size);
			}

			using (var pen = new Pen(Color.Black, 1))
			{
				// 境界線
				g.DrawRectangle(pen, 0, 0, size - 1, size - 1);
			}

			// 速度数値を表示
			if (!string.IsNullOrEmpty(speedText))
			{
				using (var font = new Font("Arial", size * 0.35f, FontStyle.Bold))
				using (var brush = new SolidBrush(Color.Black))
				{
					var textSize = g.MeasureString(speedText, font);
					float x = (size - textSize.Width) / 2;
					float y = (size - textSize.Height) / 2;
					g.DrawString(speedText, font, brush, x, y);
				}
			}
			else
			{
				// 数値がない場合は従来の>>アイコン
				using (var pen = new Pen(Color.Black, 1.5f))
				{
					// 二重矢印
					g.DrawLine(pen, size / 6, size / 2 - size / 8, size / 2, size / 2);
					g.DrawLine(pen, size / 6, size / 2 + size / 8, size / 2, size / 2);
					g.DrawLine(pen, size / 2, size / 2 - size / 8, size * 5 / 6, size / 2);
					g.DrawLine(pen, size / 2, size / 2 + size / 8, size * 5 / 6, size / 2);
				}
			}
		}

		// カウンターアイコン（回転矢印改良版）
		private static void DrawCounterIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Red, 2))
			{
				// 反撃を表現する回転矢印
				g.DrawArc(pen, 3, 3, size - 6, size - 6, 45, 270);
				// 矢印の先端
				g.DrawLine(pen, size - 4, size / 2 + 2, size - 2, size / 2);
				g.DrawLine(pen, size - 4, size / 2 - 2, size - 2, size / 2);

				// 中央に「!」マーク
				using (var brush = new SolidBrush(Color.Red))
				using (var font = new Font("Arial", size * 0.4f, FontStyle.Bold))
				{
					g.DrawString("!", font, brush, size / 2 - size / 8, size / 2 - size / 6);
				}
			}
		}

		// スタンアイコン（稲妻とスタン効果）
		private static void DrawStunIcon(Graphics g, int size)
		{
			using (var pen = new Pen(Color.Gold, 2))
			using (var brush = new SolidBrush(Color.Gold))
			{
				// スタン効果の星
				for (int i = 0; i < 4; i++)
				{
					double angle = i * Math.PI / 2;
					int x1 = (int)(size / 2 + Math.Cos(angle) * size / 4);
					int y1 = (int)(size / 2 + Math.Sin(angle) * size / 4);
					int x2 = (int)(size / 2 + Math.Cos(angle) * size / 6);
					int y2 = (int)(size / 2 + Math.Sin(angle) * size / 6);
					g.DrawLine(pen, x2, y2, x1, y1);
				}

				// 中央の円
				g.FillEllipse(brush, size / 2 - size / 8, size / 2 - size / 8, size / 4, size / 4);
			}
		}

		// デフォルトアイコン（？マーク改良版）
		private static void DrawDefaultIcon(Graphics g, int size)
		{
			using (var brush = new SolidBrush(Color.Gray))
			using (var whiteBrush = new SolidBrush(Color.White))
			using (var font = new Font("Arial", size * 0.5f, FontStyle.Bold))
			{
				// 背景円
				g.FillEllipse(brush, 1, 1, size - 2, size - 2);
				// 白い「？」
				var textSize = g.MeasureString("?", font);
				g.DrawString("?", font, whiteBrush,
					(size - textSize.Width) / 2, (size - textSize.Height) / 2);
			}
		}

		/// <summary>
		/// キャッシュをクリア
		/// </summary>
		public static void ClearCache()
		{
			foreach (var icon in iconCache.Values)
			{
				icon?.Dispose();
			}
			iconCache.Clear();
		}
	}
}