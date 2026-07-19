using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ToNStatTool
{
	/// <summary>
	/// テラーのスタン可否アイコンを管理するクラス（フラットバッジ版）
	/// TerrorTraitIconsと同じトーン（高解像度描画→縮小、白抜きグリフ）で、
	/// 従来の形の意味論（丸=可否、三角=注意）は維持する。
	/// 通常表示・コンパクト表示の両方から共有される。
	/// </summary>
	public static class TerrorStunIcons
	{
		private static readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>();

		/// <summary>要求サイズに対する内部描画の倍率（スーパーサンプリング）</summary>
		private const int SuperSample = 4;

		// 配色（TerrorTraitIconsのバッジパレットと統一）
		private static readonly Color SafeGreen = Color.FromArgb(48, 164, 108);
		private static readonly Color CautionAmber = Color.FromArgb(245, 158, 11);
		private static readonly Color ForbiddenRed = Color.FromArgb(229, 72, 77);
		private static readonly Color IneffectiveGray = Color.FromArgb(107, 114, 128);
		private static readonly Color UnknownPurple = Color.FromArgb(126, 91, 196);

		/// <summary>
		/// スタン可否に応じたアイコンを取得（キャッシュ付き）
		/// </summary>
		public static Image GetIcon(TerrorStunType stunType, int size = 16)
		{
			string cacheKey = $"{stunType}_{size}";

			if (iconCache.ContainsKey(cacheKey))
				return iconCache[cacheKey];

			var icon = CreateIcon(stunType, size);
			iconCache[cacheKey] = icon;
			return icon;
		}

		/// <summary>
		/// スタン可否のツールチップ文言を取得
		/// </summary>
		public static string GetToolTipText(TerrorStunType stunType)
		{
			switch (stunType)
			{
				case TerrorStunType.Safe: return "スタン可能";
				case TerrorStunType.Caution: return "注意が必要";
				case TerrorStunType.Forbidden: return "スタン厳禁";
				case TerrorStunType.Ineffective: return "スタン効果なし";
				default: return "スタン可否不明";
			}
		}

		private static Image CreateIcon(TerrorStunType stunType, int size)
		{
			// 高解像度で描画（最低48px確保）
			int ss = Math.Max(size * SuperSample, 48);
			float s = ss;

			var big = new Bitmap(ss, ss);
			using (var g = Graphics.FromImage(big))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.TextRenderingHint = TextRenderingHint.AntiAlias;

				switch (stunType)
				{
					case TerrorStunType.Safe:
						DrawCircle(g, s, SafeGreen);
						using (var pen = WhitePen(s, 0.12f))
						{
							g.DrawLine(pen, s * 0.26f, s * 0.52f, s * 0.44f, s * 0.70f);
							g.DrawLine(pen, s * 0.44f, s * 0.70f, s * 0.76f, s * 0.32f);
						}
						break;

					case TerrorStunType.Caution:
						// 角丸三角形 + 「!」（角はLineJoin.Roundの縁取りで丸める）
						using (var path = RoundedTriangle(s))
						using (var brush = new SolidBrush(CautionAmber))
						using (var edge = new Pen(CautionAmber, s * 0.10f) { LineJoin = LineJoin.Round })
						{
							g.FillPath(brush, path);
							g.DrawPath(edge, path);
						}
						using (var pen = WhitePen(s, 0.11f))
						using (var wb = new SolidBrush(Color.White))
						{
							g.DrawLine(pen, s * 0.50f, s * 0.38f, s * 0.50f, s * 0.62f);
							g.FillEllipse(wb, s * 0.44f, s * 0.70f, s * 0.12f, s * 0.12f);
						}
						break;

					case TerrorStunType.Forbidden:
						DrawCircle(g, s, ForbiddenRed);
						using (var pen = WhitePen(s, 0.12f))
						{
							g.DrawLine(pen, s * 0.32f, s * 0.32f, s * 0.68f, s * 0.68f);
							g.DrawLine(pen, s * 0.68f, s * 0.32f, s * 0.32f, s * 0.68f);
						}
						break;

					case TerrorStunType.Ineffective:
						DrawCircle(g, s, IneffectiveGray);
						using (var pen = WhitePen(s, 0.12f))
						{
							g.DrawLine(pen, s * 0.28f, s * 0.50f, s * 0.72f, s * 0.50f);
						}
						break;

					default: // Unknown
						DrawCircle(g, s, UnknownPurple);
						using (var font = new Font("Segoe UI", s * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
						using (var wb = new SolidBrush(Color.White))
						{
							var sf = new StringFormat
							{
								Alignment = StringAlignment.Center,
								LineAlignment = StringAlignment.Center
							};
							g.DrawString("?", font, wb, new RectangleF(0, s * -0.02f, s, s), sf);
						}
						break;
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

		private static void DrawCircle(Graphics g, float s, Color color)
		{
			using (var brush = new SolidBrush(color))
			{
				g.FillEllipse(brush, s * 0.02f, s * 0.02f, s * 0.96f, s * 0.96f);
			}
		}

		/// <summary>注意アイコン用の三角形パスを作成（縁取り分だけ内側に寄せてある）</summary>
		private static GraphicsPath RoundedTriangle(float s)
		{
			var path = new GraphicsPath();
			PointF top = new PointF(s * 0.50f, s * 0.10f);
			PointF right = new PointF(s * 0.92f, s * 0.86f);
			PointF left = new PointF(s * 0.08f, s * 0.86f);
			path.AddLine(top, right);
			path.AddLine(right, left);
			path.AddLine(left, top);
			path.CloseFigure();
			return path;
		}

		private static Pen WhitePen(float s, float widthRatio)
		{
			var pen = new Pen(Color.White, s * widthRatio);
			pen.StartCap = LineCap.Round;
			pen.EndCap = LineCap.Round;
			pen.LineJoin = LineJoin.Round;
			return pen;
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
