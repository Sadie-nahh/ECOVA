using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.GUI.Helpers;
using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Windows.Forms;
using Serilog;

namespace EnvContract.GUI.Forms.Sales
{
    /// <summary>
    /// Form sửa hợp đồng khớp UI mockup "SỬA HỢP ĐỒNG".
    /// Load contract + customer data theo contractId, cho phép cập nhật.
    /// </summary>
    public class ContractEditForm : Form
    {
        private readonly IContractService _contractService;
        private readonly ICustomerService _customerService;
        private readonly string _contractId;
        private ContractCardDTO _currentCard;

        // UI Controls
        private Guna2TextBox txtCompanyName, txtSymbol, txtRepresentative, txtPhone, txtAddress;
        private Guna2DateTimePicker dtpSignedDate, dtpExpectedDate;
        private Guna2Button btnSave;
        private Label btnClose;

        public ContractEditForm(IContractService contractService, ICustomerService customerService, string contractId)
        {
            _contractService = contractService;
            _customerService = customerService;
            _contractId = contractId;

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
            this.Load += ContractEditForm_Load;

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
                Text = LM.Get("sales_ct_edit_title"),
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

            // Row 4: Ngày ký kết | Ngày dự kiến trả kết quả
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

            // Điều chỉnh chiều cao form để chứa đủ 4 row + nút Cập nhật
            int formHeight = y4 + inputOffsetY + 42 + 80; // y4 + input + textbox height + bottom padding
            this.Size = new Size(580, formHeight);

            // Update button (instead of "Lưu")
            btnSave = new Guna2Button
            {
                Text = LM.Get("msg_update"),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                FillColor = UIConstants.SuccessColor,
                ForeColor = Color.White,
                Size = new Size(140, 45),
                Location = new Point(this.Width - 175, this.Height - 65),
                BorderRadius = 10,
                Cursor = Cursors.Hand
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
        }

        private async void ContractEditForm_Load(object sender, EventArgs e)
        {
            try
            {
                _currentCard = await _contractService.GetContractCardByIdAsync(_contractId);
                if (_currentCard != null)
                {
                    txtCompanyName.Text = _currentCard.CompanyName;
                    txtSymbol.Text = _currentCard.ContractId;
                    txtSymbol.ReadOnly = true; // Don't allow changing contract ID
                    txtRepresentative.Text = _currentCard.Representative;
                    txtPhone.Text = _currentCard.PhoneNumber;
                    txtAddress.Text = _currentCard.Address;
                    dtpSignedDate.Value = _currentCard.SignedDate;
                    dtpExpectedDate.Value = _currentCard.ValidTo;
                }
            }
            catch (Exception ex)
            {
                var LM = EnvContract.Common.LanguageManager.Instance;
                MessageBox.Show(LM.Get("sales_ct_err_load") + ex.Message, LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

                // Update customer info
                if (!string.IsNullOrEmpty(_currentCard?.CustomerId))
                {
                    try
                    {
                        var customer = await _customerService.GetCustomerByIdAsync(_currentCard.CustomerId);
                        if (customer != null)
                        {
                            customer.CompanyName = txtCompanyName.Text.Trim();
                            customer.Representative = txtRepresentative.Text.Trim();
                            customer.PhoneNumber = txtPhone.Text.Trim();
                            customer.Address = txtAddress.Text.Trim();
                            await _customerService.UpdateCustomerAsync(customer);
                        }
                    }
                    catch (Exception ex) { Log.Warning(ex, "[ContractEdit] Customer update failed - continuing with contract save"); }
                }

                // Update contract
                var contract = new ContractDto
                {
                    ContractId = _contractId,
                    CustomerId = _currentCard?.CustomerId,
                    SignedDate = signedDate,
                    ValidFrom = signedDate,
                    ValidTo = expectedDate,
                    Status = _currentCard?.Status ?? 0
                };

                await _contractService.UpdateContractAsync(contract);

                MessageBox.Show(LM.Get("sales_ct_msg_update_success"), LM.Get("msg_info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
