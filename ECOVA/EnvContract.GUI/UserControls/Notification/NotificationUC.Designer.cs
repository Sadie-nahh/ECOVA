namespace EnvContract.GUI.UserControls.Notification
{
    partial class NotificationUC
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlHeader = new Guna.UI2.WinForms.Guna2Panel();
            this.txtSearch = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblTitle = new Guna.UI2.WinForms.Guna2HtmlLabel();
            this.flpNotifications = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 
            this.pnlHeader.BackColor = System.Drawing.Color.Transparent;
            this.pnlHeader.Controls.Add(this.txtSearch);
            this.pnlHeader.Controls.Add(this.lblTitle);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.Location = new System.Drawing.Point(0, 0);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Size = new System.Drawing.Size(1000, 80);
            this.pnlHeader.TabIndex = 0;
            // 
            // txtSearch
            // 
            this.txtSearch.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSearch.BorderColor = System.Drawing.Color.Black;
            this.txtSearch.BorderRadius = 15;
            this.txtSearch.BorderThickness = 1;
            this.txtSearch.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtSearch.DefaultText = "";
            this.txtSearch.DisabledState.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(208)))), ((int)(((byte)(208)))), ((int)(((byte)(208)))));
            this.txtSearch.DisabledState.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(226)))), ((int)(((byte)(226)))));
            this.txtSearch.DisabledState.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(138)))), ((int)(((byte)(138)))), ((int)(((byte)(138)))));
            this.txtSearch.DisabledState.PlaceholderForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(138)))), ((int)(((byte)(138)))), ((int)(((byte)(138)))));
            this.txtSearch.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(233)))), ((int)(((byte)(238)))), ((int)(((byte)(217)))));
            this.txtSearch.FocusedState.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(94)))), ((int)(((byte)(148)))), ((int)(((byte)(255)))));
            this.txtSearch.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtSearch.HoverState.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(94)))), ((int)(((byte)(148)))), ((int)(((byte)(255)))));
            this.txtSearch.Location = new System.Drawing.Point(544, 20);
            this.txtSearch.Margin = new System.Windows.Forms.Padding(4);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.PasswordChar = '\0';
            this.txtSearch.PlaceholderForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.txtSearch.PlaceholderText = "Tìm kiếm đơn hàng....";
            this.txtSearch.SelectedText = "";
            this.txtSearch.Size = new System.Drawing.Size(430, 40);
            this.txtSearch.TabIndex = 1;
            // 
            // lblTitle
            // 
            this.lblTitle.BackColor = System.Drawing.Color.Transparent;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblTitle.Location = new System.Drawing.Point(30, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(387, 47);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = EnvContract.Common.LanguageManager.Instance.Get("notification_title");
            // 
            // flpNotifications — 2-column grid layout
            // 
            this.flpNotifications.AutoScroll = true;
            this.flpNotifications.BackColor = System.Drawing.Color.Transparent;
            this.flpNotifications.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpNotifications.Location = new System.Drawing.Point(0, 80);
            this.flpNotifications.Name = "flpNotifications";
            this.flpNotifications.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.flpNotifications.WrapContents = true;
            this.flpNotifications.Padding = new System.Windows.Forms.Padding(25, 10, 25, 30);
            this.flpNotifications.Size = new System.Drawing.Size(1000, 620);
            this.flpNotifications.TabIndex = 1;
            this.flpNotifications.Resize += new System.EventHandler(this.flpNotifications_Resize);
            // 
            // NotificationUC
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(244)))), ((int)(((byte)(236)))));
            this.Controls.Add(this.flpNotifications);
            this.Controls.Add(this.pnlHeader);
            this.Name = "NotificationUC";
            this.Size = new System.Drawing.Size(1000, 700);
            this.Load += new System.EventHandler(this.NotificationUC_Load);
            this.pnlHeader.ResumeLayout(false);
            this.pnlHeader.PerformLayout();
            this.ResumeLayout(false);

            EnvContract.Common.LanguageManager.Instance.LanguageChanged += () =>
            {
                this.lblTitle.Text = EnvContract.Common.LanguageManager.Instance.Get("notification_title");
            };
        }

        private Guna.UI2.WinForms.Guna2Panel pnlHeader;
        private Guna.UI2.WinForms.Guna2HtmlLabel lblTitle;
        private Guna.UI2.WinForms.Guna2TextBox txtSearch;
        private System.Windows.Forms.FlowLayoutPanel flpNotifications;
    }
}
