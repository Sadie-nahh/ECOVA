namespace EnvContract.GUI.Forms.Dashboards
{
    partial class DashboardUC
    {
        private System.ComponentModel.IContainer components = null;

        private Guna.UI2.WinForms.Guna2Panel cardContracts;
        private Guna.UI2.WinForms.Guna2Panel cardRevenue;
        private Guna.UI2.WinForms.Guna2Panel cardTasks;
        
        private System.Windows.Forms.Label lblTotalContractsTitle;
        private System.Windows.Forms.Label lblTotalContractsValue;

        private System.Windows.Forms.Label lblRevenueTitle;
        private System.Windows.Forms.Label lblRevenueValue;

        private System.Windows.Forms.Label lblPendingTasksTitle;
        private System.Windows.Forms.Label lblPendingTasksValue;

        private System.Windows.Forms.Label lblHeaderTitle;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            
            // Header
            this.lblHeaderTitle = new System.Windows.Forms.Label();
            this.lblHeaderTitle.AutoSize = true;
            this.lblHeaderTitle.Font = new System.Drawing.Font("Segoe UI", 20F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblHeaderTitle.Location = new System.Drawing.Point(40, 30);
            this.lblHeaderTitle.Text = "Tổng quan khu vực";

            // 1. Card Contracts
            this.cardContracts = new Guna.UI2.WinForms.Guna2Panel();
            this.lblTotalContractsTitle = new System.Windows.Forms.Label();
            this.lblTotalContractsValue = new System.Windows.Forms.Label();
            
            this.cardContracts.BorderRadius = 15;
            this.cardContracts.FillColor = System.Drawing.Color.White;
            this.cardContracts.Location = new System.Drawing.Point(40, 100);
            this.cardContracts.Size = new System.Drawing.Size(280, 140);
            // Shadow effect via panel paint or Guna ShadowPanel (simplified here)

            this.lblTotalContractsTitle.AutoSize = true;
            this.lblTotalContractsTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblTotalContractsTitle.ForeColor = System.Drawing.Color.Gray;
            this.lblTotalContractsTitle.Location = new System.Drawing.Point(20, 20);
            this.lblTotalContractsTitle.Text = "Tổng Hợp Đồng";
            this.lblTotalContractsTitle.BackColor = System.Drawing.Color.Transparent;

            this.lblTotalContractsValue.AutoSize = true;
            this.lblTotalContractsValue.Font = new System.Drawing.Font("Segoe UI", 28F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblTotalContractsValue.Location = new System.Drawing.Point(20, 60);
            this.lblTotalContractsValue.Text = "0";
            this.lblTotalContractsValue.BackColor = System.Drawing.Color.Transparent;

            this.cardContracts.Controls.Add(this.lblTotalContractsTitle);
            this.cardContracts.Controls.Add(this.lblTotalContractsValue);

            // 2. Card Revenue
            this.cardRevenue = new Guna.UI2.WinForms.Guna2Panel();
            this.lblRevenueTitle = new System.Windows.Forms.Label();
            this.lblRevenueValue = new System.Windows.Forms.Label();

            this.cardRevenue.BorderRadius = 15;
            this.cardRevenue.FillColor = System.Drawing.Color.White;
            this.cardRevenue.Location = new System.Drawing.Point(350, 100);
            this.cardRevenue.Size = new System.Drawing.Size(280, 140);

            this.lblRevenueTitle.AutoSize = true;
            this.lblRevenueTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblRevenueTitle.ForeColor = System.Drawing.Color.Gray;
            this.lblRevenueTitle.Location = new System.Drawing.Point(20, 20);
            this.lblRevenueTitle.Text = "Doanh thu ước tính";
            this.lblRevenueTitle.BackColor = System.Drawing.Color.Transparent;

            this.lblRevenueValue.AutoSize = true;
            this.lblRevenueValue.Font = new System.Drawing.Font("Segoe UI", 28F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblRevenueValue.Location = new System.Drawing.Point(20, 60);
            this.lblRevenueValue.Text = "$0";
            this.lblRevenueValue.BackColor = System.Drawing.Color.Transparent;

            this.cardRevenue.Controls.Add(this.lblRevenueTitle);
            this.cardRevenue.Controls.Add(this.lblRevenueValue);

            // 3. Card Pending
            this.cardTasks = new Guna.UI2.WinForms.Guna2Panel();
            this.lblPendingTasksTitle = new System.Windows.Forms.Label();
            this.lblPendingTasksValue = new System.Windows.Forms.Label();

            this.cardTasks.BorderRadius = 15;
            this.cardTasks.FillColor = System.Drawing.Color.White;
            this.cardTasks.Location = new System.Drawing.Point(660, 100);
            this.cardTasks.Size = new System.Drawing.Size(280, 140);

            this.lblPendingTasksTitle.AutoSize = true;
            this.lblPendingTasksTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblPendingTasksTitle.ForeColor = System.Drawing.Color.Gray;
            this.lblPendingTasksTitle.Location = new System.Drawing.Point(20, 20);
            this.lblPendingTasksTitle.Text = "Mẫu Test tới hạn";
            this.lblPendingTasksTitle.BackColor = System.Drawing.Color.Transparent;

            this.lblPendingTasksValue.AutoSize = true;
            this.lblPendingTasksValue.Font = new System.Drawing.Font("Segoe UI", 28F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblPendingTasksValue.Location = new System.Drawing.Point(20, 60);
            this.lblPendingTasksValue.Text = "0";
            this.lblPendingTasksValue.BackColor = System.Drawing.Color.Transparent;

            this.cardTasks.Controls.Add(this.lblPendingTasksTitle);
            this.cardTasks.Controls.Add(this.lblPendingTasksValue);

            // UserControl Container
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250); // WhiteBackground
            this.Controls.Add(this.lblHeaderTitle);
            this.Controls.Add(this.cardContracts);
            this.Controls.Add(this.cardRevenue);
            this.Controls.Add(this.cardTasks);
            this.Name = "DashboardUC";
            this.Size = new System.Drawing.Size(1030, 720);
            this.Load += new System.EventHandler(this.DashboardUC_Load);
            
            this.cardContracts.ResumeLayout(false);
            this.cardContracts.PerformLayout();
            this.cardRevenue.ResumeLayout(false);
            this.cardRevenue.PerformLayout();
            this.cardTasks.ResumeLayout(false);
            this.cardTasks.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
