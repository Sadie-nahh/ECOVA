using EnvContract.Common;
using EnvContract.GUI.Helpers;

namespace EnvContract.GUI.Forms.Main
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        // UI Components
        private Guna.UI2.WinForms.Guna2Panel pnlSidebar;
        private System.Windows.Forms.Panel pnlContent; // Changed from Guna2Panel to standard Panel for reliable Dock.Fill
        // Buttons
        private Guna.UI2.WinForms.Guna2Button btnDashboard;
        private Guna.UI2.WinForms.Guna2Button btnCustomer;
        private Guna.UI2.WinForms.Guna2Button btnContract;
        private Guna.UI2.WinForms.Guna2Button btnEmployee;
        private Guna.UI2.WinForms.Guna2Button btnPlanning;
        private Guna.UI2.WinForms.Guna2Button btnTesting;
        private Guna.UI2.WinForms.Guna2Button btnApproval;
        private Guna.UI2.WinForms.Guna2Button btnNotification; // Thêm nút Thông báo
        private Guna.UI2.WinForms.Guna2Button btnLogout;
        // User Info & Logo
        private Guna.UI2.WinForms.Guna2PictureBox pbLogo;
        private System.Windows.Forms.Label lblLang;
        private Guna.UI2.WinForms.Guna2PictureBox pbLang;
        private Guna.UI2.WinForms.Guna2Panel pnlUserInfo;
        private Guna.UI2.WinForms.Guna2CirclePictureBox pbAvatar;
        private System.Windows.Forms.Label lblUserName;
        private System.Windows.Forms.Label lblUserRole;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlSidebar = new Guna.UI2.WinForms.Guna2Panel();
            this.pnlContent = new System.Windows.Forms.Panel(); // Use standard Panel to guarantee Dock.Fill support
            this.btnDashboard = new Guna.UI2.WinForms.Guna2Button();
            this.btnCustomer = new Guna.UI2.WinForms.Guna2Button();
            this.btnContract = new Guna.UI2.WinForms.Guna2Button();
            this.btnEmployee = new Guna.UI2.WinForms.Guna2Button();
            this.btnPlanning = new Guna.UI2.WinForms.Guna2Button();
            this.btnTesting = new Guna.UI2.WinForms.Guna2Button();
            this.btnApproval = new Guna.UI2.WinForms.Guna2Button();
            this.btnNotification = new Guna.UI2.WinForms.Guna2Button();
            this.btnLogout = new Guna.UI2.WinForms.Guna2Button();
            this.pbLogo = new Guna.UI2.WinForms.Guna2PictureBox();
            this.lblLang = new System.Windows.Forms.Label();
            this.pbLang = new Guna.UI2.WinForms.Guna2PictureBox();
            this.pnlUserInfo = new Guna.UI2.WinForms.Guna2Panel();
            this.pbAvatar = new Guna.UI2.WinForms.Guna2CirclePictureBox();
            this.lblUserName = new System.Windows.Forms.Label();
            this.lblUserRole = new System.Windows.Forms.Label();
            
            this.pnlSidebar.SuspendLayout();
            this.pnlUserInfo.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbLogo)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbLang)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbAvatar)).BeginInit();
            this.SuspendLayout();

            // pnlSidebar
            // 
            this.pnlSidebar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(46)))), ((int)(((byte)(26))))); // Màu xanh đậm Figma
            this.pnlSidebar.Controls.Add(this.btnLogout);
            this.pnlSidebar.Controls.Add(this.pnlUserInfo);
            this.pnlSidebar.Controls.Add(this.btnNotification);
            this.pnlSidebar.Controls.Add(this.btnApproval);
            this.pnlSidebar.Controls.Add(this.btnTesting);
            this.pnlSidebar.Controls.Add(this.btnPlanning);
            this.pnlSidebar.Controls.Add(this.btnContract);
            this.pnlSidebar.Controls.Add(this.btnEmployee);
            this.pnlSidebar.Controls.Add(this.btnCustomer);
            this.pnlSidebar.Controls.Add(this.btnDashboard);
            this.pnlSidebar.Controls.Add(this.lblLang);
            this.pnlSidebar.Controls.Add(this.pbLang);
            this.pnlSidebar.Controls.Add(this.pbLogo);
            this.pnlSidebar.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnlSidebar.Location = new System.Drawing.Point(0, 0);
            this.pnlSidebar.Name = "pnlSidebar";
            this.pnlSidebar.Size = new System.Drawing.Size(260, 750);
            this.pnlSidebar.TabIndex = 0;

            // 
            // pbLogo
            // 
            string assetsBasePath = System.IO.Path.Combine(Application.StartupPath, "assets");
            if (!System.IO.Directory.Exists(assetsBasePath))
                assetsBasePath = System.IO.Path.Combine(Application.StartupPath, @"..\..\..\..\assets");
            string logoPath = System.IO.Path.Combine(assetsBasePath, "images", "TenPhanMem2.png");
            if (System.IO.File.Exists(logoPath)) this.pbLogo.Image = System.Drawing.Image.FromFile(logoPath);
            this.pbLogo.Location = new System.Drawing.Point(50, 20);
            this.pbLogo.Name = "pbLogo";
            this.pbLogo.Size = new System.Drawing.Size(160, 50);
            this.pbLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pbLogo.TabIndex = 0;
            this.pbLogo.TabStop = false;

            // 
            // lblLang
            // 
            this.lblLang.AutoSize = true;
            this.lblLang.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblLang.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(243)))), ((int)(((byte)(158))))); // Vàng nhạt
            this.lblLang.Location = new System.Drawing.Point(215, 6);
            this.lblLang.Name = "lblLang";
            this.lblLang.Size = new System.Drawing.Size(25, 15);
            this.lblLang.TabIndex = 1;
            this.lblLang.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblLang.Text = LanguageManager.Instance.ToggleLabel;
            this.lblLang.Cursor = System.Windows.Forms.Cursors.Hand;
            this.lblLang.Click += (s, e) => LanguageManager.Instance.ToggleLanguage();

            this.pbLang.Cursor = System.Windows.Forms.Cursors.Hand;
            this.pbLang.Click += (s, e) => LanguageManager.Instance.ToggleLanguage();

            // 
            // pbLang
            // 
            string langIconPath = System.IO.Path.Combine(assetsBasePath, "images", "NgonNgu.png");
            if (System.IO.File.Exists(langIconPath)) this.pbLang.Image = System.Drawing.Image.FromFile(langIconPath);
            this.pbLang.Location = new System.Drawing.Point(195, 6);
            this.pbLang.Name = "pbLang";
            this.pbLang.Size = new System.Drawing.Size(15, 15);
            this.pbLang.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pbLang.TabIndex = 2;
            this.pbLang.TabStop = false;

            // --- Định nghĩa hàm Helper để cấu hình Guna2Button ---
            // Đọc font size từ LanguageManager để tự động phù hợp với ngôn ngữ hiện tại (VI=11.25, EN=9.0)
            float sidebarInitFontSize = 11.25F;
            if (float.TryParse(LanguageManager.Instance.Get("sidebar_font_size"),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsedSize))
                sidebarInitFontSize = parsedSize;

            void SetupSidebarButton(Guna.UI2.WinForms.Guna2Button btn, string text, int yPos)
            {
                btn.BorderRadius = 15;
                btn.Cursor = System.Windows.Forms.Cursors.Hand;
                btn.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(49)))), ((int)(((byte)(87)))), ((int)(((byte)(44))))); // Xanh rêu nhạt hơn
                btn.Font = new System.Drawing.Font("Segoe UI", sidebarInitFontSize, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
                btn.ForeColor = System.Drawing.Color.White;
                btn.HoverState.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(90)))), ((int)(((byte)(118)))), ((int)(((byte)(73))))); // Hover sáng hơn
                btn.Location = new System.Drawing.Point(20, yPos);
                btn.Name = "btn" + text.Replace(" ", "");
                btn.Size = new System.Drawing.Size(220, 45);
                btn.TabIndex = yPos;
                btn.Text = text;
                btn.TextOffset = new System.Drawing.Point(0, 0);
            }

            int startY = 100;
            int stepY = 60;
            
            // 
            // Buttons
            // 
            var LM = LanguageManager.Instance;
            SetupSidebarButton(this.btnDashboard, LM.Get("sidebar_dashboard"), startY);
            SetupSidebarButton(this.btnEmployee, LM.Get("sidebar_employee"), startY + stepY * 1);
            SetupSidebarButton(this.btnCustomer, LM.Get("sidebar_customer"), startY + stepY * 2);
            SetupSidebarButton(this.btnPlanning, LM.Get("sidebar_planning"), startY + stepY * 3);
            SetupSidebarButton(this.btnContract, LM.Get("sidebar_field"), startY + stepY * 4);
            SetupSidebarButton(this.btnTesting, LM.Get("sidebar_lab"), startY + stepY * 5);
            SetupSidebarButton(this.btnApproval, LM.Get("sidebar_result"), startY + stepY * 6);
            SetupSidebarButton(this.btnNotification, LM.Get("sidebar_notification"), startY + stepY * 7);

            int lastMenuButtonBottom = startY + stepY * 7 + 45; 
            int minUserInfoY = lastMenuButtonBottom + 20; 

            // 
            // pnlUserInfo
            // 
            this.pnlUserInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.pnlUserInfo.Controls.Add(this.lblUserRole);
            this.pnlUserInfo.Controls.Add(this.lblUserName);
            this.pnlUserInfo.Controls.Add(this.pbAvatar);
            this.pnlUserInfo.Location = new System.Drawing.Point(20, 595);
            this.pnlUserInfo.Name = "pnlUserInfo";
            this.pnlUserInfo.Size = new System.Drawing.Size(220, 65);
            this.pnlUserInfo.TabIndex = 11;

            // 
            // pbAvatar
            // 
            this.pbAvatar.FillColor = System.Drawing.Color.LightGray; // Placeholder color
            this.pbAvatar.Location = new System.Drawing.Point(0, 5);
            this.pbAvatar.Name = "pbAvatar";
            this.pbAvatar.ShadowDecoration.Mode = Guna.UI2.WinForms.Enums.ShadowMode.Circle;
            this.pbAvatar.Size = new System.Drawing.Size(50, 50);
            this.pbAvatar.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pbAvatar.TabIndex = 0;
            this.pbAvatar.TabStop = false;

            // 
            // lblUserName
            // 
            this.lblUserName.AutoSize = false;
            this.lblUserName.AutoEllipsis = true;
            this.lblUserName.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblUserName.ForeColor = System.Drawing.Color.White;
            this.lblUserName.Location = new System.Drawing.Point(55, 10);
            this.lblUserName.Name = "lblUserName";
            this.lblUserName.Size = new System.Drawing.Size(160, 24);
            this.lblUserName.TabIndex = 1;
            this.lblUserName.Text = "Admin";
            this.lblUserName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // 
            // lblUserRole
            // 
            this.lblUserRole.AutoSize = false;
            this.lblUserRole.AutoEllipsis = true;
            this.lblUserRole.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblUserRole.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(243)))), ((int)(((byte)(158)))));
            this.lblUserRole.Location = new System.Drawing.Point(55, 34);
            this.lblUserRole.Name = "lblUserRole";
            this.lblUserRole.Size = new System.Drawing.Size(160, 20);
            this.lblUserRole.TabIndex = 2;
            this.lblUserRole.Text = "Admin";

            // 
            // btnLogout
            // 
            this.btnLogout.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnLogout.BorderRadius = 10;
            this.btnLogout.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLogout.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(49)))), ((int)(((byte)(87)))), ((int)(((byte)(44))))); // Xanh rêu
            this.btnLogout.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnLogout.ForeColor = System.Drawing.Color.White;
            this.btnLogout.Location = new System.Drawing.Point(20, this.pnlSidebar.Height - 80);
            this.btnLogout.Name = "btnLogout";
            this.btnLogout.Size = new System.Drawing.Size(220, 45);
            this.btnLogout.TabIndex = 12;
            this.btnLogout.Text = LM.Get("sidebar_logout");

            // 
            // pnlContent
            // 
            this.pnlContent.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(240))))); // Màu xám nhạt nền chính
            this.pnlContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContent.Location = new System.Drawing.Point(260, 0);
            this.pnlContent.Name = "pnlContent";
            this.pnlContent.Size = new System.Drawing.Size(1090, 750);
            this.pnlContent.TabIndex = 1;
            
            // Xử lý Resize của Sidebar để UserInfo & Logout luôn ở dưới cùng và KHÔNG đè lên menu
            this.pnlSidebar.Resize += (s, e) => {
                // Tính vị trí lý tưởng (neo dưới cùng)
                int idealUserInfoY = this.pnlSidebar.Height - 155;
                int idealLogoutY = this.pnlSidebar.Height - 70;

                // Đảm bảo không bao giờ đè lên nút menu cuối cùng
                this.pnlUserInfo.Top = Math.Max(minUserInfoY, idealUserInfoY);
                this.btnLogout.Top = Math.Max(minUserInfoY + 75, idealLogoutY);
            };

            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(245, 245, 240); // Màu nền content để không lộ trắng
            this.ClientSize = new System.Drawing.Size(1350, 750);
            // WinForms Dock Rule: Process order is REVERSE of Controls collection index
            // So to get: Sidebar=Left, Content=Fill -> Add Content FIRST, then Sidebar SECOND
            this.Controls.Add(this.pnlContent);  // index 0 → processed last → Fill remaining space
            this.Controls.Add(this.pnlSidebar);  // index 1 → processed first → takes Left allocation
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.MinimizeBox = false;
            this.MaximizeBox = true;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = LM.Get("form_title");

            // Load Icon cho Form — dùng AppIcon đã load tập trung ở Program.cs
            if (Program.AppIcon != null)
                this.Icon = Program.AppIcon;
            
            this.pnlUserInfo.ResumeLayout(false);
            this.pnlUserInfo.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbLogo)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbLang)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbAvatar)).EndInit();
            this.pnlSidebar.ResumeLayout(false);
            this.pnlSidebar.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
