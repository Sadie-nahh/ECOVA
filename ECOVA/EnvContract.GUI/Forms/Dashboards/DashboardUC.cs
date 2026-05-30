using System;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using EnvContract.GUI.Helpers;

namespace EnvContract.GUI.Forms.Dashboards
{
    public partial class DashboardUC : UserControl
    {
        public DashboardUC()
        {
            InitializeComponent();
            SetupCardsColor();
        }

        private void SetupCardsColor()
        {
            lblTotalContractsValue.ForeColor = UIConstants.PrimaryColor;
            lblRevenueValue.ForeColor = UIConstants.SuccessColor;
            lblPendingTasksValue.ForeColor = UIConstants.WarningColor;
        }

        private void DashboardUC_Load(object sender, EventArgs e)
        {
            lblTotalContractsValue.Text = "2,456";
            lblRevenueValue.Text = "$124K";
            lblPendingTasksValue.Text = "12";
        }
    }
}
