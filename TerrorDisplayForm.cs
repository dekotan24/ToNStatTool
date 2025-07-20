using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ToNStatTool
{
	/// <summary>
	/// テラー表示専用のサブフォーム
	/// </summary>
	public partial class TerrorDisplayForm : Form
	{
		private FlowLayoutPanel terrorPanel;
		private readonly List<CompactTerrorControl> terrorControls = new List<CompactTerrorControl>();

		public TerrorDisplayForm()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.Text = "Terror Display - ToN Stat Tool";
			this.Size = new Size(450, 170);
			this.MinimumSize = new Size(450, 170);
			this.StartPosition = FormStartPosition.Manual;
			this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - this.Width, 0);
			this.FormBorderStyle = FormBorderStyle.Sizable;
			this.TopMost = true;

			// テラー表示パネル
			terrorPanel = new FlowLayoutPanel();
			terrorPanel.Location = new Point(0, 0);
			terrorPanel.Size = new Size(this.ClientSize.Width, this.ClientSize.Height);
			terrorPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			terrorPanel.FlowDirection = FlowDirection.LeftToRight;
			terrorPanel.WrapContents = false;
			terrorPanel.AutoScroll = true;
			terrorPanel.BorderStyle = BorderStyle.FixedSingle;
			this.Controls.Add(terrorPanel);

			// リサイズイベント
			this.Resize += (s, e) => {
				if (terrorPanel != null)
				{
					terrorPanel.Size = new Size(this.ClientSize.Width, this.ClientSize.Height);
				}
			};
		}

		public void UpdateTerrors(List<TerrorInfo> terrors)
		{
			// 既存のコントロールをクリア
			foreach (var control in terrorControls)
			{
				control.Dispose();
			}
			terrorControls.Clear();
			terrorPanel.Controls.Clear();

			// 新しいテラーコントロールを作成
			foreach (var terror in terrors)
			{
				var control = new CompactTerrorControl(terror);
				terrorControls.Add(control);
				terrorPanel.Controls.Add(control);
			}
		}
	}
}