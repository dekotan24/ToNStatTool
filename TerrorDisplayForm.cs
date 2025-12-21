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
		private Panel dragHandle; // ドラッグハンドル
		private readonly List<CompactTerrorControl> terrorControls = new List<CompactTerrorControl>();

		// ドラッグ用の変数
		private bool isDragging = false;
		private Point dragStartPoint;

		public TerrorDisplayForm()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.Text = "Terror Display - ToN Stat Tool";
			this.Size = new Size(450, 140);
			this.MinimumSize = new Size(450, 140);
			this.StartPosition = FormStartPosition.Manual;
			this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - this.Width, 0);
			this.FormBorderStyle = FormBorderStyle.None;
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

			// ドラッグハンドル（左下の小さい■）を作成
			dragHandle = new Panel();
			dragHandle.Size = new Size(15, 15); // ハンドルのサイズ
			dragHandle.BackColor = Color.DarkGray; // ハンドルの色
			dragHandle.Cursor = Cursors.SizeAll; // カーソルを移動アイコンに
			dragHandle.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			dragHandle.Location = new Point(0, this.ClientSize.Height - dragHandle.Height);

			// ドラッグハンドルのマウスイベント
			dragHandle.MouseDown += DragHandle_MouseDown;
			dragHandle.MouseMove += DragHandle_MouseMove;
			dragHandle.MouseUp += DragHandle_MouseUp;

			this.Controls.Add(dragHandle);
			dragHandle.BringToFront(); // 最前面に表示

			// リサイズイベント
			this.Resize += (s, e) =>
			{
				if (terrorPanel != null)
				{
					terrorPanel.Size = new Size(this.ClientSize.Width, this.ClientSize.Height);
				}
				if (dragHandle != null)
				{
					dragHandle.Location = new Point(0, this.ClientSize.Height - dragHandle.Height);
				}
			};
		}

		private void DragHandle_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				isDragging = true;
				dragStartPoint = e.Location;
			}
		}

		private void DragHandle_MouseMove(object sender, MouseEventArgs e)
		{
			if (isDragging)
			{
				Point newLocation = this.Location;
				newLocation.X += e.X - dragStartPoint.X;
				newLocation.Y += e.Y - dragStartPoint.Y;
				this.Location = newLocation;
			}
		}

		private void DragHandle_MouseUp(object sender, MouseEventArgs e)
		{
			isDragging = false;
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