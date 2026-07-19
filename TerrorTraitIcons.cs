using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.RegularExpressions;

namespace ToNStatTool
{
	/// <summary>
	/// テラー特性のアイコンを管理するクラス（フラットバッジ版）
	/// 角丸の色付き背景 + 白抜きグリフ。要求サイズの4倍で描画してから縮小することで、
	/// 15px程度の小サイズでも輪郭が崩れないようにしている。
	/// バッジは背景色を自前で持つためテーマ非依存。
	/// </summary>
	public static class TerrorTraitIcons
	{
		private static readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();

		/// <summary>要求サイズに対する内部描画の倍率（スーパーサンプリング）</summary>
		private const int SuperSample = 4;

		#region バッジ背景色（ダーク/ライト両テーマで視認できる彩度）

		private static readonly Color BadgeRed = Color.FromArgb(229, 72, 77);       // 追跡・視界・停止・カウンター
		private static readonly Color BadgeDarkRed = Color.FromArgb(153, 27, 27);   // 即死
		private static readonly Color BadgeBlue = Color.FromArgb(62, 123, 192);     // 徘徊・複数
		private static readonly Color BadgeCyan = Color.FromArgb(0, 145, 178);      // テレポート
		private static readonly Color BadgeGreen = Color.FromArgb(48, 164, 108);    // 召喚
		private static readonly Color BadgePurple = Color.FromArgb(126, 91, 196);   // 壁貫通・変身
		private static readonly Color BadgeOrange = Color.FromArgb(232, 119, 46);   // デバフ・掴み
		private static readonly Color BadgeAmber = Color.FromArgb(245, 158, 11);    // 速度
		private static readonly Color BadgeGold = Color.FromArgb(202, 138, 4);      // スタン
		private static readonly Color BadgeGray = Color.FromArgb(107, 114, 128);    // 不明

		#endregion

		/// <summary>
		/// 特性タイプに応じたアイコンを取得
		/// </summary>
		public static Image GetTraitIcon(string traitType, int size = 16)
		{
			return GetTraitIcon(traitType, "", size);
		}

		/// <summary>
		/// 特性タイプと説明に応じたアイコンを取得（速度は説明から数値を抽出して表示）
		/// </summary>
		public static Image GetTraitIcon(string traitType, string description, int size = 16)
		{
			// バッジはテーマ非依存なのでキャッシュキーにテーマは含めない
			string cacheKey = $"{traitType}_{description}_{size}";

			if (iconCache.ContainsKey(cacheKey))
				return iconCache[cacheKey];

			var icon = CreateTraitIcon(traitType, description, size);
			iconCache[cacheKey] = icon;
			return icon;
		}

		/// <summary>
		/// 特性カテゴリーに応じたバッジアイコンを作成
		/// </summary>
		private static Image CreateTraitIcon(string traitType, string description, int size)
		{
			// 高解像度で描画（最低48px確保）
			int ss = Math.Max(size * SuperSample, 48);
			float s = ss;

			var big = new Bitmap(ss, ss);
			using (var g = Graphics.FromImage(big))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.TextRenderingHint = TextRenderingHint.AntiAlias;

				string lowerType = (traitType ?? "").ToLower();

				// 移動関連
				if (lowerType.Contains("追跡"))
				{
					DrawBadge(g, s, BadgeRed, DrawChaseGlyph);
				}
				else if (lowerType.Contains("徘徊"))
				{
					DrawBadge(g, s, BadgeBlue, DrawWanderGlyph);
				}
				else if (lowerType.Contains("壁貫通"))
				{
					DrawBadge(g, s, BadgePurple, DrawWallPassGlyph);
				}
				// 攻撃関連
				else if (lowerType.Contains("即死"))
				{
					DrawBadge(g, s, BadgeDarkRed, DrawInstantKillGlyph);
				}
				else if (lowerType.Contains("デバフ"))
				{
					DrawBadge(g, s, BadgeOrange, DrawDebuffGlyph);
				}
				else if (lowerType.Contains("掴み"))
				{
					DrawBadge(g, s, BadgeOrange, DrawGrabGlyph);
				}
				else if (lowerType.Contains("視界ダメージ") || lowerType.Contains("視認"))
				{
					DrawBadge(g, s, BadgeRed, DrawEyeGlyph);
				}
				// 特殊能力
				else if (lowerType.Contains("テレポート"))
				{
					DrawBadge(g, s, BadgeCyan, DrawTeleportGlyph);
				}
				else if (lowerType.Contains("召喚"))
				{
					DrawBadge(g, s, BadgeGreen, DrawSummonGlyph);
				}
				else if (lowerType.Contains("複数"))
				{
					DrawBadge(g, s, BadgeBlue, DrawMultipleGlyph);
				}
				else if (lowerType.Contains("変身") || lowerType.Contains("形態"))
				{
					DrawBadge(g, s, BadgePurple, DrawTransformGlyph);
				}
				else if (lowerType.Contains("停止"))
				{
					DrawBadge(g, s, BadgeRed, DrawStopGlyph);
				}
				// 速度関連
				else if (lowerType.Contains("速度") || lowerType.Contains("加速"))
				{
					string maxSpeed = ExtractMaxSpeed(description);
					DrawBadge(g, s, BadgeAmber, (gg, sz) => DrawSpeedGlyph(gg, sz, maxSpeed));
				}
				// カウンター
				else if (lowerType.Contains("カウンター"))
				{
					DrawBadge(g, s, BadgeRed, DrawCounterGlyph);
				}
				// スタン関連
				else if (lowerType.Contains("スタン"))
				{
					DrawBadge(g, s, BadgeGold, DrawStunGlyph);
				}
				// その他
				else
				{
					DrawBadge(g, s, BadgeGray, DrawDefaultGlyph);
				}
			}

			// 要求サイズへ高品質縮小
			var bmp = new Bitmap(size, size);
			using (var g = Graphics.FromImage(bmp))
			{
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.PixelOffsetMode = PixelOffsetMode.HighQuality;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.DrawImage(big, new Rectangle(0, 0, size, size));
			}
			big.Dispose();
			return bmp;
		}

		/// <summary>
		/// 角丸バッジ背景を描いてからグリフを描画する
		/// </summary>
		private static void DrawBadge(Graphics g, float s, Color bg, Action<Graphics, float> glyph)
		{
			using (var path = RoundedRect(new RectangleF(s * 0.01f, s * 0.01f, s * 0.98f, s * 0.98f), s * 0.22f))
			using (var brush = new SolidBrush(bg))
			{
				g.FillPath(brush, path);
			}
			glyph(g, s);
		}

		private static GraphicsPath RoundedRect(RectangleF r, float radius)
		{
			var path = new GraphicsPath();
			float d = radius * 2;
			path.AddArc(r.X, r.Y, d, d, 180, 90);
			path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
			path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
			path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
			path.CloseFigure();
			return path;
		}

		/// <summary>白の丸キャップペンを作成（呼び出し側でDispose）</summary>
		private static Pen WhitePen(float s, float widthRatio = 0.10f)
		{
			var pen = new Pen(Color.White, s * widthRatio);
			pen.StartCap = LineCap.Round;
			pen.EndCap = LineCap.Round;
			pen.LineJoin = LineJoin.Round;
			return pen;
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

		#region グリフ描画（すべて白、座標はサイズ比）

		// 追跡: 走る人型 + 右向き矢印
		private static void DrawChaseGlyph(Graphics g, float s)
		{
			using (var wb = new SolidBrush(Color.White))
			using (var pen = WhitePen(s))
			{
				g.FillEllipse(wb, s * 0.20f, s * 0.14f, s * 0.20f, s * 0.20f);      // 頭
				g.DrawLine(pen, s * 0.30f, s * 0.38f, s * 0.38f, s * 0.62f);        // 体（前傾）
				g.DrawLine(pen, s * 0.38f, s * 0.62f, s * 0.24f, s * 0.84f);        // 後ろ脚
				g.DrawLine(pen, s * 0.38f, s * 0.62f, s * 0.54f, s * 0.80f);        // 前脚
				g.DrawLine(pen, s * 0.52f, s * 0.40f, s * 0.84f, s * 0.40f);        // 矢印軸
				g.DrawLine(pen, s * 0.70f, s * 0.27f, s * 0.84f, s * 0.40f);        // 矢頭上
				g.DrawLine(pen, s * 0.70f, s * 0.53f, s * 0.84f, s * 0.40f);        // 矢頭下
			}
		}

		// 徘徊: 蛇行する軌跡 + 矢頭
		private static void DrawWanderGlyph(Graphics g, float s)
		{
			using (var pen = WhitePen(s))
			{
				PointF[] points = {
					new PointF(s * 0.16f, s * 0.66f),
					new PointF(s * 0.32f, s * 0.32f),
					new PointF(s * 0.50f, s * 0.66f),
					new PointF(s * 0.70f, s * 0.36f)
				};
				g.DrawCurve(pen, points, 0.6f);
				g.DrawLine(pen, s * 0.60f, s * 0.32f, s * 0.70f, s * 0.36f);        // 矢頭
				g.DrawLine(pen, s * 0.72f, s * 0.50f, s * 0.70f, s * 0.36f);
			}
		}

		// 壁貫通: 半透明の壁 + 貫通する矢印
		private static void DrawWallPassGlyph(Graphics g, float s)
		{
			using (var wallPen = new Pen(Color.FromArgb(150, Color.White), s * 0.10f))
			using (var pen = WhitePen(s))
			{
				wallPen.StartCap = LineCap.Round;
				wallPen.EndCap = LineCap.Round;
				g.DrawLine(wallPen, s * 0.50f, s * 0.16f, s * 0.50f, s * 0.84f);    // 壁
				g.DrawLine(pen, s * 0.14f, s * 0.50f, s * 0.84f, s * 0.50f);        // 矢印軸
				g.DrawLine(pen, s * 0.70f, s * 0.37f, s * 0.84f, s * 0.50f);        // 矢頭
				g.DrawLine(pen, s * 0.70f, s * 0.63f, s * 0.84f, s * 0.50f);
			}
		}

		// 即死: ドクロ
		private static void DrawInstantKillGlyph(Graphics g, float s)
		{
			using (var wb = new SolidBrush(Color.White))
			using (var bb = new SolidBrush(BadgeDarkRed))
			{
				g.FillEllipse(wb, s * 0.24f, s * 0.14f, s * 0.52f, s * 0.46f);      // 頭蓋
				g.FillRectangle(wb, s * 0.32f, s * 0.48f, s * 0.36f, s * 0.22f);    // 顎
				g.FillEllipse(bb, s * 0.33f, s * 0.30f, s * 0.13f, s * 0.15f);      // 左目
				g.FillEllipse(bb, s * 0.54f, s * 0.30f, s * 0.13f, s * 0.15f);      // 右目
				g.FillRectangle(bb, s * 0.415f, s * 0.56f, s * 0.045f, s * 0.14f);  // 歯の隙間
				g.FillRectangle(bb, s * 0.54f, s * 0.56f, s * 0.045f, s * 0.14f);
			}
		}

		// デバフ: 塗りつぶしの下向き矢印
		private static void DrawDebuffGlyph(Graphics g, float s)
		{
			using (var wb = new SolidBrush(Color.White))
			{
				PointF[] arrow = {
					new PointF(s * 0.40f, s * 0.16f),
					new PointF(s * 0.60f, s * 0.16f),
					new PointF(s * 0.60f, s * 0.52f),
					new PointF(s * 0.76f, s * 0.52f),
					new PointF(s * 0.50f, s * 0.84f),
					new PointF(s * 0.24f, s * 0.52f),
					new PointF(s * 0.40f, s * 0.52f)
				};
				g.FillPolygon(wb, arrow);
			}
		}

		// 掴み: 手のひら
		private static void DrawGrabGlyph(Graphics g, float s)
		{
			using (var wb = new SolidBrush(Color.White))
			using (var pen = WhitePen(s, 0.085f))
			{
				g.FillEllipse(wb, s * 0.28f, s * 0.52f, s * 0.44f, s * 0.32f);      // 手のひら
				for (int i = 0; i < 4; i++)
				{
					float x = s * (0.32f + i * 0.12f);
					float topY = (i == 1 || i == 2) ? s * 0.16f : s * 0.24f;        // 中指・薬指を長く
					g.DrawLine(pen, x, s * 0.56f, x, topY);
				}
			}
		}

		// 視界ダメージ: 目
		private static void DrawEyeGlyph(Graphics g, float s)
		{
			using (var wb = new SolidBrush(Color.White))
			using (var bb = new SolidBrush(BadgeRed))
			{
				// アーモンド形の目（上下まぶたをベジェ曲線で描いて両端を尖らせる）
				var eye = new GraphicsPath();
				eye.AddBezier(
					s * 0.10f, s * 0.50f,
					s * 0.30f, s * 0.16f, s * 0.70f, s * 0.16f,
					s * 0.90f, s * 0.50f);
				eye.AddBezier(
					s * 0.90f, s * 0.50f,
					s * 0.70f, s * 0.84f, s * 0.30f, s * 0.84f,
					s * 0.10f, s * 0.50f);
				eye.CloseFigure();
				g.FillPath(wb, eye);
				eye.Dispose();
				g.FillEllipse(bb, s * 0.39f, s * 0.36f, s * 0.22f, s * 0.28f);      // 瞳
			}
		}

		// テレポート: 稲妻
		private static void DrawTeleportGlyph(Graphics g, float s)
		{
			using (var wb = new SolidBrush(Color.White))
			{
				PointF[] bolt = {
					new PointF(s * 0.60f, s * 0.10f),
					new PointF(s * 0.30f, s * 0.52f),
					new PointF(s * 0.47f, s * 0.52f),
					new PointF(s * 0.40f, s * 0.90f),
					new PointF(s * 0.72f, s * 0.44f),
					new PointF(s * 0.53f, s * 0.44f)
				};
				g.FillPolygon(wb, bolt);
			}
		}

		// 召喚: 円 + 十字（魔法陣）
		private static void DrawSummonGlyph(Graphics g, float s)
		{
			using (var pen = WhitePen(s))
			{
				g.DrawEllipse(pen, s * 0.18f, s * 0.18f, s * 0.64f, s * 0.64f);
				g.DrawLine(pen, s * 0.50f, s * 0.32f, s * 0.50f, s * 0.68f);
				g.DrawLine(pen, s * 0.32f, s * 0.50f, s * 0.68f, s * 0.50f);
			}
		}

		// 複数: 3つの人影
		private static void DrawMultipleGlyph(Graphics g, float s)
		{
			using (var wb = new SolidBrush(Color.White))
			{
				// 左右（少し下げて小さめ）
				for (int i = 0; i < 2; i++)
				{
					float cx = (i == 0) ? s * 0.24f : s * 0.76f;
					g.FillEllipse(wb, cx - s * 0.07f, s * 0.30f, s * 0.14f, s * 0.14f);
					g.FillEllipse(wb, cx - s * 0.10f, s * 0.48f, s * 0.20f, s * 0.28f);
				}
				// 中央（大きめ・手前）
				g.FillEllipse(wb, s * 0.41f, s * 0.20f, s * 0.18f, s * 0.18f);
				g.FillEllipse(wb, s * 0.37f, s * 0.42f, s * 0.26f, s * 0.38f);
			}
		}

		// 変身: 循環する2つの矢印
		private static void DrawTransformGlyph(Graphics g, float s)
		{
			using (var pen = WhitePen(s))
			{
				g.DrawArc(pen, s * 0.20f, s * 0.20f, s * 0.60f, s * 0.60f, 200, 130);   // 上弧
				g.DrawArc(pen, s * 0.20f, s * 0.20f, s * 0.60f, s * 0.60f, 20, 130);    // 下弧
				// 上弧の矢頭（右上向き）
				g.DrawLine(pen, s * 0.68f, s * 0.16f, s * 0.78f, s * 0.30f);
				g.DrawLine(pen, s * 0.62f, s * 0.34f, s * 0.78f, s * 0.30f);
				// 下弧の矢頭（左下向き）
				g.DrawLine(pen, s * 0.32f, s * 0.84f, s * 0.22f, s * 0.70f);
				g.DrawLine(pen, s * 0.38f, s * 0.66f, s * 0.22f, s * 0.70f);
			}
		}

		// 停止: 一時停止バー
		private static void DrawStopGlyph(Graphics g, float s)
		{
			using (var pen = WhitePen(s, 0.15f))
			{
				g.DrawLine(pen, s * 0.38f, s * 0.28f, s * 0.38f, s * 0.72f);
				g.DrawLine(pen, s * 0.62f, s * 0.28f, s * 0.62f, s * 0.72f);
			}
		}

		// 速度: 数値、なければ二重シェブロン
		private static void DrawSpeedGlyph(Graphics g, float s, string speedText)
		{
			if (!string.IsNullOrEmpty(speedText))
			{
				float fontSize = s * (speedText.Length >= 2 ? 0.44f : 0.56f);
				using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
				using (var wb = new SolidBrush(Color.White))
				{
					var sf = new StringFormat
					{
						Alignment = StringAlignment.Center,
						LineAlignment = StringAlignment.Center
					};
					g.DrawString(speedText, font, wb, new RectangleF(0, s * -0.01f, s, s), sf);
				}
			}
			else
			{
				using (var pen = WhitePen(s, 0.12f))
				{
					g.DrawLine(pen, s * 0.20f, s * 0.26f, s * 0.46f, s * 0.50f);
					g.DrawLine(pen, s * 0.20f, s * 0.74f, s * 0.46f, s * 0.50f);
					g.DrawLine(pen, s * 0.54f, s * 0.26f, s * 0.80f, s * 0.50f);
					g.DrawLine(pen, s * 0.54f, s * 0.74f, s * 0.80f, s * 0.50f);
				}
			}
		}

		// カウンター: 回転矢印 + 中央の「!」
		private static void DrawCounterGlyph(Graphics g, float s)
		{
			using (var pen = WhitePen(s, 0.09f))
			using (var wb = new SolidBrush(Color.White))
			{
				g.DrawArc(pen, s * 0.16f, s * 0.16f, s * 0.68f, s * 0.68f, -60, 270);
				// 矢頭（円弧の終端付近）
				g.DrawLine(pen, s * 0.86f, s * 0.28f, s * 0.80f, s * 0.44f);
				g.DrawLine(pen, s * 0.66f, s * 0.32f, s * 0.80f, s * 0.44f);
				// 中央の「!」
				using (var exPen = WhitePen(s, 0.11f))
				{
					g.DrawLine(exPen, s * 0.50f, s * 0.34f, s * 0.50f, s * 0.54f);
				}
				g.FillEllipse(wb, s * 0.44f, s * 0.62f, s * 0.12f, s * 0.12f);
			}
		}

		// スタン: バースト（8方向の光）
		private static void DrawStunGlyph(Graphics g, float s)
		{
			using (var pen = WhitePen(s))
			using (var wb = new SolidBrush(Color.White))
			{
				for (int i = 0; i < 8; i++)
				{
					double a = i * Math.PI / 4;
					float len = (i % 2 == 0) ? s * 0.36f : s * 0.27f;
					g.DrawLine(pen,
						(float)(s * 0.5 + Math.Cos(a) * s * 0.14), (float)(s * 0.5 + Math.Sin(a) * s * 0.14),
						(float)(s * 0.5 + Math.Cos(a) * len), (float)(s * 0.5 + Math.Sin(a) * len));
				}
				g.FillEllipse(wb, s * 0.42f, s * 0.42f, s * 0.16f, s * 0.16f);
			}
		}

		// 不明: 「?」
		private static void DrawDefaultGlyph(Graphics g, float s)
		{
			using (var font = new Font("Segoe UI", s * 0.58f, FontStyle.Bold, GraphicsUnit.Pixel))
			using (var wb = new SolidBrush(Color.White))
			{
				var sf = new StringFormat
				{
					Alignment = StringAlignment.Center,
					LineAlignment = StringAlignment.Center
				};
				g.DrawString("?", font, wb, new RectangleF(0, s * -0.02f, s, s), sf);
			}
		}

		#endregion

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

		/// <summary>
		/// テーマ変更時の処理（バッジはテーマ非依存なので何もしない）
		/// </summary>
		public static void OnThemeChanged()
		{
			// フラットバッジは自前の背景色を持つためテーマの影響を受けない
		}
	}
}
