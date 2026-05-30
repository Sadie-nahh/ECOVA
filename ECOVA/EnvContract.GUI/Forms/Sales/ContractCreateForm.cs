using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.DTO.Requests;
using EnvContract.GUI.Helpers;
using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace EnvContract.GUI.Forms.Sales
{
    /// <summary>
    /// Form tạo hợp đồng mới khớp UI mockup "TẠO HỢP ĐỒNG MỚI".
    /// Fields: Tên doanh nghiệp, Ký hiệu, Người đại diện, SĐT, Địa chỉ, Ngày ký, Ngày trả KQ.
    /// </summary>
    public partial class ContractCreateForm : Form
    {
        private readonly IContractService _contractService;
        private readonly ICustomerService _customerService;
        private string _currentUserId;

        // UI Controls
        private Guna2TextBox txtCompanyName, txtSymbol, txtRepresentative, txtPhone, txtAddress;
        private Guna2DateTimePicker dtpSignedDate, dtpExpectedDate;
        private Guna2Button btnSave;
        private Label btnClose;

        public ContractCreateForm(IContractService contractService, ICustomerService customerService, string currentUserId)
        {
            _contractService = contractService;
            _customerService = customerService;
            _currentUserId = currentUserId;

            InitializeComponent();
            SetupUI();
            if (Program.AppIcon != null) this.Icon = Program.AppIcon;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(580, 520);
            this.BackColor = Color.White;
            this.ShowInTaskbar = false;

            var shadowForm = new Guna2ShadowForm(this) { TargetForm = this };
            var elipse = new Guna2Elipse { TargetControl = this, BorderRadius = UIConstants.BorderRadiusLarge };
            var dragControl = new Guna2DragControl { TargetControl = this };

            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            // Title
            var lblTitle = new Label
            {
                Text = LM.Get("sales_ct_add_title"),
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(0, 25),
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblTitle);
            lblTitle.Location = new Point((this.Width - lblTitle.PreferredWidth) / 2, 25);

            // Close button (X) - dùng Label để tránh lỗi Guna2Button Transparent không render text
            btnClose = new Label
            {
                Text = "✕",
                Size = new Size(32, 32),
                Location = new Point(this.Width - 55, 18),
                BackColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => this.Close();
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.FromArgb(180, 0, 0);
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.Black;
            this.Controls.Add(btnClose);

            int leftCol = 30;
            int rightCol = 305;
            int fieldWidth = 240;

            Label CreateLabel(string text, int x, int y)
            {
                var lbl = new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.Black,
                    AutoSize = true,
                    Location = new Point(x, y),
                    BackColor = Color.White  // Dùng White thay Transparent để không bị vẽ đè lên control bên dưới
                };
                this.Controls.Add(lbl);
                return lbl;
            }

            Guna2TextBox CreateTextBox(string placeholder, int x, int y, int width = 0)
            {
                var txt = new Guna2TextBox
                {
                    PlaceholderText = placeholder,
                    PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                    Font = new Font("Segoe UI", 10),
                    Size = new Size(width > 0 ? width : fieldWidth, 42),
                    Location = new Point(x, y),
                    BorderRadius = 8,
                    FillColor = Color.FromArgb(240, 240, 240),
                    ForeColor = Color.Black,
                    BorderColor = Color.FromArgb(220, 220, 220),
                    BorderThickness = 1
                };
                this.Controls.Add(txt);
                return txt;
            }

            // Khoảng cách label: 26px, textbox cao: 42px → mỗi row cao 26+42=68px, thêm gap 16px → rowHeight = 84px
            int labelOffsetY = 0;   // label bắt đầu từ y của row
            int inputOffsetY = 26;  // textbox bắt đầu 26px sau label
            int rowHeight = 84;     // khoảng cách giữa 2 row

            // Row 1: Tên doanh nghiệp | Ký hiệu
            int y1 = 80;
            CreateLabel(LM.Get("sales_ct_comp"), leftCol, y1 + labelOffsetY);
            txtCompanyName = CreateTextBox(LM.Get("sales_ct_ph_comp"), leftCol, y1 + inputOffsetY);

            CreateLabel(LM.Get("sales_ct_sym"), rightCol, y1 + labelOffsetY);
            txtSymbol = CreateTextBox(LM.Get("sales_ct_ph_sym"), rightCol, y1 + inputOffsetY);

            // Row 2: Người đại diện | Số điện thoại
            int y2 = y1 + rowHeight;
            CreateLabel(LM.Get("sales_ct_rep"), leftCol, y2 + labelOffsetY);
            txtRepresentative = CreateTextBox(LM.Get("sales_ct_ph_rep"), leftCol, y2 + inputOffsetY);

            CreateLabel(LM.Get("sales_ct_phone"), rightCol, y2 + labelOffsetY);
            txtPhone = CreateTextBox(LM.Get("sales_ct_ph_phone"), rightCol, y2 + inputOffsetY);

            // Row 3: Địa chỉ doanh nghiệp (full width)
            int y3 = y2 + rowHeight;
            CreateLabel(LM.Get("sales_ct_addr"), leftCol, y3 + labelOffsetY);
            txtAddress = CreateTextBox(LM.Get("sales_ct_ph_addr"), leftCol, y3 + inputOffsetY, this.Width - 60);

            // Row 4: Ngày ký kết hợp đồng | Ngày dự kiến trả kết quả
            int y4 = y3 + rowHeight;
            CreateLabel(LM.Get("sales_ct_sign_date"), leftCol, y4 + labelOffsetY);
            dtpSignedDate = new Guna2DateTimePicker
            {
                CustomFormat = "dd/MM/yyyy",
                Format = DateTimePickerFormat.Custom,
                Font = new Font("Segoe UI", 10),
                Size = new Size(fieldWidth, 42),
                Location = new Point(leftCol, y4 + inputOffsetY),
                BorderRadius = 8,
                FillColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Black,
                BorderColor = Color.FromArgb(220, 220, 220),
                BorderThickness = 1,
                Value = DateTime.Now
            };
            this.Controls.Add(dtpSignedDate);

            CreateLabel(LM.Get("sales_ct_exp_date"), rightCol, y4 + labelOffsetY);
            dtpExpectedDate = new Guna2DateTimePicker
            {
                CustomFormat = "dd/MM/yyyy",
                Format = DateTimePickerFormat.Custom,
                Font = new Font("Segoe UI", 10),
                Size = new Size(fieldWidth, 42),
                Location = new Point(rightCol, y4 + inputOffsetY),
                BorderRadius = 8,
                FillColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Black,
                BorderColor = Color.FromArgb(220, 220, 220),
                BorderThickness = 1,
                Value = DateTime.Now.AddMonths(3)
            };
            this.Controls.Add(dtpExpectedDate);

            // Điều chỉnh chiều cao form để chứa đủ 4 row + nút Lưu
            int formHeight = y4 + inputOffsetY + 42 + 80; // y4 + input + textbox height + bottom padding
            this.Size = new Size(580, formHeight);

            // Save button
            btnSave = new Guna2Button
            {
                Text = LM.Get("msg_save"),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                FillColor = UIConstants.SuccessColor,
                ForeColor = Color.White,
                Size = new Size(120, 45),
                Location = new Point(this.Width - 155, this.Height - 65),
                BorderRadius = 10,
                Cursor = Cursors.Hand
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            try
            {
                if (string.IsNullOrWhiteSpace(txtCompanyName.Text))
                {
                    MessageBox.Show(LM.Get("sales_ct_err_comp"), LM.Get("msg_warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                btnSave.Enabled = false;

                // Read dates directly from DateTimePicker
                DateTime signedDate = dtpSignedDate.Value.Date;
                DateTime expectedDate = dtpExpectedDate.Value.Date;

                // Create customer first
                string customerId = Guid.NewGuid().ToString();
                var customer = new CustomerDto
                {
                    CustomerId = customerId,
                    CompanyName = txtCompanyName.Text.Trim(),
                    Representative = txtRepresentative.Text.Trim(),
                    PhoneNumber = txtPhone.Text.Trim(),
                    Address = txtAddress.Text.Trim(),
                    TaxCode = txtSymbol.Text.Trim(),
                    ContactEmail = ""
                };
                await _customerService.AddCustomerAsync(customer);

                // Generate contract ID
                string contractId = $"ECOVA-{DateTime.Now:yyyy}-{Guid.NewGuid().ToString().Substring(0, 3).ToUpper()}";

                var request = new CreateContractRequest
                {
                    Contract = new ContractDto
                    {
                        ContractId = txtSymbol.Text.Trim().Length > 0 ? txtSymbol.Text.Trim() : contractId,
                        CustomerId = customerId,
                        SignedDate = signedDate,
                        ValidFrom = signedDate,
                        ValidTo = expectedDate,
                        CreatedBy = _currentUserId
                    },
                    SourcePdfPath = ""
                };

                await _contractService.CreateContractAsync(request);

                MessageBox.Show(LM.Get("sales_ct_msg_success"), LM.Get("msg_info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(LM.Get("msg_error") + ": " + ex.Message, LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = true;
            }
        }
    }
}