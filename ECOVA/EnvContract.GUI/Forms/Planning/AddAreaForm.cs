using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EnvContract.GUI.Forms.Planning
{
    /// <summary>
    /// Dialog nhập tên khu vực lấy mẫu mới.
    /// </summary>
    public class AddAreaForm : Form
    {
        public string AreaName { get; private set; } = string.Empty;

        private static readonly Color DarkGreen2 = Color.FromArgb(49, 87, 44);
        private static readonly Color PageBg = Color.FromArgb(240, 244, 235);
        private static readonly Color InputBg = Color.FromArgb(226, 231, 220);
        private static readonly Color YellowGreen = Color.FromArgb(236, 243, 158);

        public AddAreaForm()
        {
            InitializeComponent();
            if (Program.AppIcon != null) this.Icon = Program.AppIcon;
        }

        private void InitializeComponent()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            this.Text = LM.Get("plan_add_area_title");
            this.Size = new Size(450, 220);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = PageBg;
            this.ShowInTaskbar = false;

            // Rounded form using region
            this.Paint += (s, e) =>
            {
                using (var path = new GraphicsPath())
                {
                    int r = 20;
                    path.AddArc(0, 0, r, r, 180, 90);
                    path.AddArc(Width - r, 0, r, r, 270, 90);
                    path.AddArc(Width - r, Height - r, r, r, 0, 90);
                    path.AddArc(0, Height - r, r, r, 90, 90);
                    path.CloseFigure();
                    this.Region = new Region(path);

                    using (var pen = new Pen(DarkGreen2, 2))
                        e.Graphics.DrawPath(pen, path);
                }
            };

            var lblTitle = new Label
            {
                Text = LM.Get("plan_add_area_title"),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = DarkGreen2,
                AutoSize = true,
                Location = new Point(30, 25),
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblTitle);

            var txtName = new Guna2TextBox
            {
                PlaceholderText = LM.Get("plan_add_area_ph"),
                PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                Font = new Font("Segoe UI", 12),
                Size = new Size(390, 45),
                Location = new Point(30, 70),
                BorderRadius = 10,
                FillColor = InputBg,
                BorderColor = DarkGreen2,
                BorderThickness = 1,
                ForeColor = Color.Black
            };
            this.Controls.Add(txtName);

            var btnConfirm = new Guna2Button
            {
                Text = LM.Get("msg_confirm"),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = YellowGreen,
                FillColor = DarkGreen2,
                Size = new Size(130, 42),
                Location = new Point(160, 135),
                BorderRadius = 12
            };
            btnConfirm.Click += (s, e) =>
            {
                var LM_Btn = EnvContract.Common.LanguageManager.Instance;
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show(LM_Btn.Get("plan_add_area_err_req"), LM_Btn.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                AreaName = txtName.Text.Trim();
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(btnConfirm);

            var btnCancel = new Guna2Button
            {
                Text = LM.Get("msg_cancel"),
                Font = new Font("Segoe UI", 11),
                ForeColor = DarkGreen2,
                FillColor = Color.Transparent,
                BorderColor = DarkGreen2,
                BorderThickness = 1,
                Size = new Size(100, 42),
                Location = new Point(310, 135),
                BorderRadius = 12
            };
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(btnCancel);

            // Allow dragging
            bool dragging = false;
            Point dragStart = Point.Empty;
            this.MouseDown += (s, e) => { dragging = true; dragStart = e.Location; };
            this.MouseMove += (s, e) => { if (dragging) { this.Left += e.X - dragStart.X; this.Top += e.Y - dragStart.Y; } };
            this.MouseUp += (s, e) => { dragging = false; };
        }
    }
}
