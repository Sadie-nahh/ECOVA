using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using Guna.UI2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EnvContract.Common;

namespace EnvContract.GUI.UserControls.Sales
{
    public partial class ContractListUC : UserControl
    {
        private readonly IContractService _contractService;
        private readonly ICustomerService _customerService;
        private readonly VoiceSearchService _voiceService;
        private Guna2DataGridView dgvContracts;
        private Guna2TextBox txtSearch;
        private Guna2Button btnCreateContract;
        private List<ContractDto> _allContracts = new List<ContractDto>();
        private Label _lblTitle;

        public ContractListUC(IContractService contractService, ICustomerService customerService)
        {
            _contractService = contractService;
            _customerService = customerService;
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>();
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Size = new Size(950, 650);
            this.BackColor = Color.White;
            this.Load += ContractListUC_Load;
            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            var LM = LanguageManager.Instance;
            _lblTitle = new Label
            {
                Text = LM.Get("contract_list_title"),
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = UIConstants.PrimaryColor,
                Location = new Point(30, 20),
                AutoSize = true
            };
            this.Controls.Add(_lblTitle);

            btnCreateContract = new Guna2Button
            {
                Text = LM.Get("contract_list_create"),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FillColor = UIConstants.PrimaryColor,
                ForeColor = Color.White,
                BorderRadius = 8,
                Size = new Size(150, 40),
                Location = new Point(30, 80),
                Cursor = Cursors.Hand
            };
            btnCreateContract.Click += BtnCreateContract_Click;
            this.Controls.Add(btnCreateContract);

            txtSearch = new Guna2TextBox
            {
                PlaceholderText = LM.Get("contract_list_search"),
                PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                Font = new Font("Segoe UI", 10),
                Size = new Size(300, 40),
                Location = new Point(this.Width - 330, 80),
                BorderRadius = 8,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            this.Controls.Add(txtSearch);

            // TextChanged: filter grid theo keyword (cho cả manual typing lẫn voice search)
            txtSearch.TextChanged += (s, e) =>
            {
                var q = txtSearch.Text.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(q))
                {
                    dgvContracts.DataSource = new List<ContractDto>(_allContracts);
                    return;
                }
                var filtered = _allContracts.Where(c =>
                    (c.ContractId    ?? "").ToLowerInvariant().Contains(q) ||
                    (c.IndustryType  ?? "").ToLowerInvariant().Contains(q) ||
                    (c.CreatedBy     ?? "").ToLowerInvariant().Contains(q))
                    .ToList();
                dgvContracts.DataSource = filtered;
            };

            VoiceSearchHelper.AttachVoiceButton(txtSearch, this, _voiceService,
                () => VoiceSearchHelper.ExtractGridContext(dgvContracts, "ContractId", "IndustryType"));

            dgvContracts = new Guna2DataGridView
            {
                Location = new Point(30, 140),
                Size = new Size(this.Width - 60, this.Height - 170),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White
            };

            // Style DataTable
            dgvContracts.ColumnHeadersDefaultCellStyle.BackColor = UIConstants.DarkGreenBackground;
            dgvContracts.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvContracts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            dgvContracts.ColumnHeadersHeight = 40;
            dgvContracts.DefaultCellStyle.Font = new Font("Segoe UI", 10);
            dgvContracts.DefaultCellStyle.SelectionBackColor = UIConstants.LightGreenAccent;
            dgvContracts.RowTemplate.Height = 35;

            this.Controls.Add(dgvContracts);

            LanguageManager.Instance.LanguageChanged += () =>
            {
                var lm = LanguageManager.Instance;
                if (_lblTitle != null) _lblTitle.Text = lm.Get("contract_list_title");
                if (btnCreateContract != null) btnCreateContract.Text = lm.Get("contract_list_create");
                if (txtSearch != null) txtSearch.PlaceholderText = lm.Get("contract_list_search");
            };
        }

        private async void ContractListUC_Load(object sender, EventArgs e)
        {
            try
            {
                _allContracts = await _contractService.GetAllContractsAsync() ?? new List<ContractDto>();
                dgvContracts.DataSource = new List<ContractDto>(_allContracts);
            }
            catch (Exception ex)
            {
                var LM = LanguageManager.Instance;
                MessageBox.Show(LM.Get("contract_list_err_load") + ex.Message, LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnCreateContract_Click(object sender, EventArgs e)
        {
            // Tạm thời truyền AdminUserId là "1" - Sẽ cải tiến UserSession tĩnh sau cùng
            using (var form = new EnvContract.GUI.Forms.Sales.ContractCreateForm(_contractService, _customerService, "1"))
            {
                form.ShowDialog(this.FindForm());
            }
            // Tải lại dữ liệu sau khi Form đóng
            try
            {
                _allContracts = await _contractService.GetAllContractsAsync() ?? new List<ContractDto>();
                dgvContracts.DataSource = new List<ContractDto>(_allContracts);
                txtSearch.Text = string.Empty;
            }
            catch (Exception ex)
            {
                var LM = LanguageManager.Instance;
                MessageBox.Show(LM.Get("contract_list_err_reload") + ex.Message, LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
