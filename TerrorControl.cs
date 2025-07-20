using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ToNStatTool
{
	/// <summary>
	/// メインフォーム用のテラー表示コントロール
	/// </summary>
	public class TerrorControl : UserControl
	{
		private PictureBox iconPictureBox;
		private Label nameLabel;
		private PictureBox stunStatusIcon;
		private ToolTip toolTip;
		private FlowLayoutPanel traitsPanel;  // 特性表示パネル（メインフォーム用）
		private List<PictureBox> traitIcons = new List<PictureBox>();  // 特性アイコン（サブフォーム用）


		public TerrorInfo TerrorData { get; private set; }

		public TerrorControl(TerrorInfo terrorInfo)
		{
			TerrorData = terrorInfo;
			InitializeTerrorControl();
			UpdateDisplay();
		}

		private void InitializeTerrorControl()
		{
			this.Size = new Size(180, 200);
			this.BorderStyle = BorderStyle.FixedSingle;
			this.BackColor = Color.White;

			toolTip = new ToolTip();

			// テラーアイコン
			iconPictureBox = new PictureBox();
			iconPictureBox.Location = new Point(40, 10);
			iconPictureBox.Size = new Size(100, 100);
			iconPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
			iconPictureBox.BorderStyle = BorderStyle.FixedSingle;
			iconPictureBox.BackColor = Color.LightGray;
			this.Controls.Add(iconPictureBox);

			// テラー名
			nameLabel = new Label();
			nameLabel.Location = new Point(5, 115);
			nameLabel.Size = new Size(170, 20);
			nameLabel.TextAlign = ContentAlignment.MiddleCenter;
			nameLabel.Font = new Font("Meiryo UI", 9, FontStyle.Bold);
			this.Controls.Add(nameLabel);

			// スタン状態アイコン
			stunStatusIcon = new PictureBox();
			stunStatusIcon.Location = new Point(5, 10);
			stunStatusIcon.Size = new Size(20, 20);
			stunStatusIcon.SizeMode = PictureBoxSizeMode.CenterImage;
			this.Controls.Add(stunStatusIcon);

			// メインフォーム用の特性パネル（テキスト表示）
			traitsPanel = new FlowLayoutPanel();
			traitsPanel.Location = new Point(5, 135);
			traitsPanel.Size = new Size(170, 60);
			traitsPanel.FlowDirection = FlowDirection.TopDown;
			traitsPanel.AutoScroll = true;
			traitsPanel.BorderStyle = BorderStyle.None;
			this.Controls.Add(traitsPanel);
		}

		private void UpdateDisplay()
		{
			// 名前の設定とトランケート
			string displayName = TerrorData.DisplayName ?? TerrorData.Name;
			if (displayName.Length > 20)
			{
				nameLabel.Text = displayName.Substring(0, 17) + "...";
				toolTip.SetToolTip(nameLabel, displayName);
			}
			else
			{
				nameLabel.Text = displayName;
			}

			// 背景色の設定
			if (TerrorData.DisplayColor != 0)
			{
				var color = ColorFromUInt(TerrorData.DisplayColor);
				this.BackColor = Color.FromArgb(50, color.R, color.G, color.B);
			}

			// スタン状態アイコンの設定
			UpdateStunStatusIcon();

			// テラーアイコンの設定（TerrorImageManagerを使用）
			SetTerrorIcon();

			// 特性情報を表示
			UpdateTraitsDisplay();
		}

		private void UpdateStunStatusIcon()
		{
			Bitmap icon = new Bitmap(16, 16);
			using (Graphics g = Graphics.FromImage(icon))
			{
				switch (TerrorData.StunType)
				{
					case TerrorStunType.Safe:
						g.FillEllipse(Brushes.Green, 2, 2, 12, 12);
						g.DrawString("✓", new Font("Arial", 8, FontStyle.Bold), Brushes.White, 3, 1);
						toolTip.SetToolTip(stunStatusIcon, "スタン可能");
						break;
					case TerrorStunType.Caution:
						g.FillPolygon(Brushes.Orange, new Point[] { new Point(8, 2), new Point(14, 14), new Point(2, 14) });
						g.DrawString("!", new Font("Arial", 8, FontStyle.Bold), Brushes.White, 6, 4);
						toolTip.SetToolTip(stunStatusIcon, "注意が必要");
						break;
					case TerrorStunType.Forbidden:
						g.FillEllipse(Brushes.Red, 2, 2, 12, 12);
						g.DrawString("×", new Font("Arial", 8, FontStyle.Bold), Brushes.White, 4, 2);
						toolTip.SetToolTip(stunStatusIcon, "スタン厳禁");
						break;
					case TerrorStunType.Ineffective:
						g.FillRectangle(Brushes.Gray, 2, 7, 12, 2);
						toolTip.SetToolTip(stunStatusIcon, "スタン効果なし");
						break;
					case TerrorStunType.Unknown:
						g.FillEllipse(Brushes.Purple, 2, 2, 12, 12);
						g.DrawString("?", new Font("Arial", 8, FontStyle.Bold), Brushes.White, 5, 2);
						toolTip.SetToolTip(stunStatusIcon, "スタン可否不明");
						break;
				}
			}
			stunStatusIcon.Image = icon;
		}

		private void SetTerrorIcon()
		{
			// TerrorImageManagerを使用してテラー画像を取得
			try
			{
				var terrorImage = TerrorImageManager.GetTerrorImage(TerrorData.Name, 100, 100);
				if (terrorImage == null)
				{
					throw new Exception("テラー画像取得失敗");
				}
				iconPictureBox.Image = terrorImage;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"テラー画像の設定エラー: {TerrorData.Name} - {ex.Message}");

				// エラーの場合はプレースホルダーを生成
				Bitmap icon = new Bitmap(100, 100);
				using (Graphics g = Graphics.FromImage(icon))
				{
					g.FillRectangle(Brushes.DarkGray, 0, 0, 100, 100);
					string initial = string.IsNullOrEmpty(TerrorData.Name) ? "?" : TerrorData.Name.Substring(0, 1).ToUpper();
					using (Font font = new Font("Arial", 36, FontStyle.Bold))
					{
						var textSize = g.MeasureString(initial, font);
						g.DrawString(initial, font, Brushes.White,
							(100 - textSize.Width) / 2, (100 - textSize.Height) / 2);
					}
				}
				iconPictureBox.Image = icon;
			}
		}

		private Color ColorFromUInt(uint color)
		{
			return Color.FromArgb((int)(color >> 16) & 0xFF, (int)(color >> 8) & 0xFF, (int)color & 0xFF);
		}

		private void UpdateTraitsDisplay()
		{
			traitsPanel.Controls.Clear();

			// JSONから特性情報を取得
			var terrorDetail = TerrorJsonLoader.GetTerrorDetail(TerrorData.Name);

			if (terrorDetail.Traits.Count > 0)
			{
				int displayCount = Math.Min(terrorDetail.Traits.Count, 5);

				for (int i = 0; i < displayCount; i++)
				{
					var trait = terrorDetail.Traits[i];

					var traitLabel = new Label();
					traitLabel.Text = $"• {trait.TraitType}";
					traitLabel.Size = new Size(75, 18);
					traitLabel.Font = new Font("Meiryo UI", 8);
					traitLabel.ForeColor = GetTraitColor(trait.Category);
					toolTip.SetToolTip(traitLabel, trait.Description);

					traitsPanel.Controls.Add(traitLabel);
				}

				if (terrorDetail.Traits.Count > 5)
				{
					var moreLabel = new Label();
					moreLabel.Text = $"他 {terrorDetail.Traits.Count - 5} 個の特性";
					moreLabel.Size = new Size(75, 18);
					moreLabel.Font = new Font("Meiryo UI", 7, FontStyle.Italic);
					moreLabel.ForeColor = Color.Gray;

					// ツールチップに全特性を表示
					string allTraits = string.Join("\n", terrorDetail.Traits.Select(t => $"• {t.TraitType}: {t.Description}"));
					toolTip.SetToolTip(moreLabel, allTraits);

					traitsPanel.Controls.Add(moreLabel);
				}
			}
		}

		private Color GetTraitColor(TerrorTraitCategory category)
		{
			switch (category)
			{
				case TerrorTraitCategory.Movement:
					return Color.Blue;
				case TerrorTraitCategory.Attack:
					return Color.Red;
				case TerrorTraitCategory.Special:
					return Color.Purple;
				case TerrorTraitCategory.Speed:
					return Color.Orange;
				case TerrorTraitCategory.Counter:
					return Color.DarkRed;
				default:
					return Color.Black;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				toolTip?.Dispose();

				// 画像を明示的に解放
				if (iconPictureBox?.Image != null)
				{
					iconPictureBox.Image.Dispose();
					iconPictureBox.Image = null;
				}

				if (stunStatusIcon?.Image != null)
				{
					stunStatusIcon.Image.Dispose();
					stunStatusIcon.Image = null;
				}
			}
			base.Dispose(disposing);
		}
	}

	/// <summary>
	/// サブフォーム用のコンパクトなテラー表示コントロール
	/// </summary>
	public class CompactTerrorControl : UserControl
	{
		private PictureBox iconBox;
		private Label nameLabel;
		private PictureBox stunIcon;
		private ToolTip toolTip;
		private FlowLayoutPanel iconPanel;

		public CompactTerrorControl(TerrorInfo terror)
		{
			this.Size = new Size(130, 110);
			this.BorderStyle = BorderStyle.FixedSingle;
			this.BackColor = Color.White;
			this.Margin = new Padding(5);

			toolTip = new ToolTip();

			// スタン状態アイコン（右上）
			stunIcon = new PictureBox();
			stunIcon.Location = new Point(111, 3);
			stunIcon.Size = new Size(16, 16);
			stunIcon.SizeMode = PictureBoxSizeMode.CenterImage;
			SetStunIcon(terror.StunType);
			this.Controls.Add(stunIcon);

			// 特性アイコンパネル（左側、縦並び）
			iconPanel = new FlowLayoutPanel();
			iconPanel.Location = new Point(3, 3);
			iconPanel.Size = new Size(20, 100);
			iconPanel.FlowDirection = FlowDirection.TopDown;
			iconPanel.WrapContents = false;
			iconPanel.BorderStyle = BorderStyle.None;
			iconPanel.AutoScroll = false;
			this.Controls.Add(iconPanel);

			// テラーアイコン（中央）
			iconBox = new PictureBox();
			iconBox.Location = new Point(30, 8);
			iconBox.Size = new Size(70, 70);
			iconBox.SizeMode = PictureBoxSizeMode.StretchImage;
			iconBox.BorderStyle = BorderStyle.FixedSingle;
			iconBox.BackColor = Color.LightGray;
			SetTerrorIcon(terror.Name);
			this.Controls.Add(iconBox);

			// 名前ラベル（下部）
			nameLabel = new Label();
			nameLabel.Location = new Point(3, 82);
			nameLabel.Size = new Size(124, 45);
			nameLabel.TextAlign = ContentAlignment.TopCenter;
			nameLabel.Font = new Font("Meiryo UI", 8, FontStyle.Bold);
			string displayName = terror.DisplayName ?? terror.Name;
			if (displayName.Length > 15)
			{
				if (displayName.Length > 12)
				{
					nameLabel.Text = displayName.Substring(0, 12) + "\n...";
				}
				toolTip.SetToolTip(nameLabel, displayName);
			}
			else
			{
				nameLabel.Text = displayName;
			}
			this.Controls.Add(nameLabel);

			// 背景色設定
			if (terror.DisplayColor != 0)
			{
				var color = ColorFromUInt(terror.DisplayColor);
				this.BackColor = Color.FromArgb(30, color.R, color.G, color.B);
			}

			// 特性アイコンを追加
			AddTraitIcons(terror.Name);
		}

		private void SetStunIcon(TerrorStunType stunType)
		{
			Bitmap icon = new Bitmap(16, 16);
			using (Graphics g = Graphics.FromImage(icon))
			{
				switch (stunType)
				{
					case TerrorStunType.Safe:
						g.FillEllipse(Brushes.Green, 1, 1, 14, 14);
						g.DrawString("✓", new Font("Arial", 8, FontStyle.Bold), Brushes.White, 3, 2);
						toolTip.SetToolTip(stunIcon, "スタン可能");
						break;
					case TerrorStunType.Caution:
						g.FillPolygon(Brushes.Orange, new Point[] { new Point(8, 1), new Point(15, 15), new Point(1, 15) });
						g.DrawString("!", new Font("Arial", 8, FontStyle.Bold), Brushes.White, 6, 3);
						toolTip.SetToolTip(stunIcon, "注意が必要");
						break;
					case TerrorStunType.Forbidden:
						g.FillEllipse(Brushes.Red, 1, 1, 14, 14);
						g.DrawString("×", new Font("Arial", 8, FontStyle.Bold), Brushes.White, 3, 2);
						toolTip.SetToolTip(stunIcon, "スタン厳禁");
						break;
					case TerrorStunType.Ineffective:
						g.FillRectangle(Brushes.Gray, 1, 7, 14, 2);
						toolTip.SetToolTip(stunIcon, "スタン効果なし");
						break;
					case TerrorStunType.Unknown:
						g.FillEllipse(Brushes.Purple, 1, 1, 14, 14);
						g.DrawString("?", new Font("Arial", 8, FontStyle.Bold), Brushes.White, 5, 2);
						toolTip.SetToolTip(stunIcon, "スタン可否不明");
						break;
				}
			}
			stunIcon.Image = icon;
		}

		private void SetTerrorIcon(string name)
		{
			// TerrorImageManagerを使用してテラー画像を取得
			try
			{
				var terrorImage = TerrorImageManager.GetTerrorImage(name, 100, 100);
				if (terrorImage == null)
				{
					throw new Exception("テラー画像取得失敗");
				}
				iconBox.Image = terrorImage;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"コンパクトテラー画像の設定エラー: {name} - {ex.Message}");

				// エラーの場合はプレースホルダーを生成
				Bitmap icon = new Bitmap(100, 100);
				using (Graphics g = Graphics.FromImage(icon))
				{
					g.FillRectangle(Brushes.DarkGray, 0, 0, 70, 50);
					string initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpper();
					using (Font font = new Font("Arial", 20, FontStyle.Bold))
					{
						var textSize = g.MeasureString(initial, font);
						g.DrawString(initial, font, Brushes.White,
							(70 - textSize.Width) / 2, (50 - textSize.Height) / 2);
					}
				}
				iconBox.Image = icon;
			}
		}

		private Color ColorFromUInt(uint color)
		{
			return Color.FromArgb((int)(color >> 16) & 0xFF, (int)(color >> 8) & 0xFF, (int)color & 0xFF);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				toolTip?.Dispose();

				// 画像を明示的に解放
				if (iconBox?.Image != null)
				{
					iconBox.Image.Dispose();
					iconBox.Image = null;
				}

				if (stunIcon?.Image != null)
				{
					stunIcon.Image.Dispose();
					stunIcon.Image = null;
				}
			}
			base.Dispose(disposing);
		}

		private void AddTraitIcons(string terrorName)
		{
			var terrorDetail = TerrorJsonLoader.GetTerrorDetail(terrorName);

			// 最大5個のアイコンを縦に表示
			int iconCount = Math.Min(terrorDetail.Traits.Count, 5);

			for (int i = 0; i < iconCount; i++)
			{
				var trait = terrorDetail.Traits[i];

				var iconBox = new PictureBox();
				iconBox.Size = new Size(14, 14);
				iconBox.SizeMode = PictureBoxSizeMode.StretchImage;
				// 説明付きでアイコンを取得（速度の場合は数値が表示される）
				iconBox.Image = TerrorTraitIcons.GetTraitIcon(trait.TraitType, trait.Description, 16);
				iconBox.Margin = new Padding(0, 1, 0, 1); // 縦の間隔を調整

				// ツールチップに説明を設定
				toolTip.SetToolTip(iconBox, $"{trait.TraitType}: {trait.Description}");

				iconPanel.Controls.Add(iconBox);
			}
		}
	}
}