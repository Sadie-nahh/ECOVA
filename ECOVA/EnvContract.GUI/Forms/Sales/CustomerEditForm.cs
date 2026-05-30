using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.GUI.Helpers;
using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace EnvContract.GUI.Forms.Sales
{
    public partial class CustomerEditForm : Form
    {
        private readonly ICustomerService _customerService;
        private readonly string _customerId;
        private CustomerDto _currentCustomer;

        // UI Controls
        private Label lblTitle;
        private Guna2TextBox txtTaxCode, txtCompanyName, txtAddress, txtRepresentative, txtEmail, txtPhone;
        private Guna2Button btnSave, btnCancel;

        public CustomerEditForm(ICustomerService customerService, string customerId = null)
        {
            _customerService = customerService;
            _customerId = customerId;
            
            InitializeComponent();
            SetupUI();
            if (Program.AppIcon != null) this.Icon = Program.AppIcon;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(500, 600);
            this.BackColor = Color.White; // Make background transparent if needed, or white with shadow
            this.ShowInTaskbar = false;
            this.Load += CustomerEditForm_Load;
            
            // Guna2 Shadow Form
            var shadowForm = new Guna2ShadowForm(this);
            shadowForm.TargetForm = this;
            
            // Guna2 Elipse for rounded corners
            var elipse = new Guna2Elipse { TargetControl = this, BorderRadius = UIConstants.BorderRadiusLarge };
            
            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            lblTitle = new Label
            {
                Text = string.IsNullOrEmpty(_customerId) ? LM.Get("sales_cus_title_add") : LM.Get("sales_cus_title_edit"),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = UIConstants.PrimaryColor,
                Location = new Point(30, 20),
                AutoSize = true
            };
            this.Controls.Add(lblTitle);

            // Helper function to create TextBoxes
            Guna2TextBox CreateTextBox(string placeholder, int yPos)
            {
                var txt = new Guna2TextBox
                {
                    PlaceholderText = placeholder,
                    PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                    Font = new Font("Segoe UI", 10),
                    Size = new Size(440, 45),
                    Location = new Point(30, yPos),
                    BorderRadius = UIConstants.BorderRadiusMedium,
                    CustomizableEdges = new Guna.UI2.WinForms.Suite.CustomizableEdges { BottomLeft = true, BottomRight = true, TopLeft = true, TopRight = true }
                };
                this.Controls.Add(txt);
                return txt;
            }

            txtTaxCode = CreateTextBox(LM.Get("sales_cus_tax"), 80);
            txtCompanyName = CreateTextBox(LM.Get("sales_cus_name"), 140);
            txtAddress = CreateTextBox(LM.Get("sales_cus_addr"), 200);
            txtRepresentative = CreateTextBox(LM.Get("sales_cus_rep"), 260);
            txtEmail = CreateTextBox(LM.Get("sales_cus_email"), 320);
            txtPhone = CreateTextBox(LM.Get("sales_cus_phone"), 380);

            btnCancel = new Guna2Button
            {
                Text = LM.Get("msg_cancel"),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FillColor = UIConstants.BorderColor,
                ForeColor = UIConstants.TextDark,
                BorderRadius = UIConstants.BorderRadiusMedium,
                Size = new Size(120, 45),
                Location = new Point(220, 460),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);

            btnSave = new Guna2Button
            {
                Text = LM.Get("msg_save_info"),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FillColor = UIConstants.PrimaryColor,
                ForeColor = Color.White,
                BorderRadius = UIConstants.BorderRadiusMedium,
                Size = new Size(150, 45),
                Location = new Point(350, 460),
                Cursor = Cursors.Hand
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
            
            // Allow form dragging
            var dragControl = new Guna2DragControl { TargetControl = this };
        }

        private async void CustomerEditForm_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_customerId))
            {
                _currentCustomer = await _customerService.GetCustomerByIdAsync(_customerId);
                if (_currentCustomer != null)
                {
                    txtTaxCode.Text = _currentCustomer.TaxCode;
                    txtTaxCode.ReadOnly = true; // Thường không cho sửa MST
                    txtCompanyName.Text = _currentCustomer.CompanyName;
                    txtAddress.Text = _currentCustomer.Address;
                    txtRepresentative.Text = _currentCustomer.Representative;
                    txtEmail.Text = _currentCustomer.ContactEmail;
                    txtPhone.Text = _currentCustomer.PhoneNumber;
                }
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            try
            {
                if (string.IsNullOrWhiteSpace(txtTaxCode.Text) || string.IsNullOrWhiteSpace(txtCompanyName.Text))
                {
                    MessageBox.Show(LM.Get("sales_cus_err_req"), LM.Get("msg_warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                btnSave.Enabled = false;

                if (string.IsNullOrEmpty(_customerId))
                {
                    // Create new
                    var customer = new CustomerDto
                    {
                        CustomerId = Guid.NewGuid().ToString(),
                        TaxCode = txtTaxCode.Text.Trim(),
                        CompanyName = txtCompanyName.Text.Trim(),
                        Address = txtAddress.Text.Trim(),
                        Representative = txtRepresentative.Text.Trim(),
                        ContactEmail = txtEmail.Text.Trim(),
                        PhoneNumber = txtPhone.Text.Trim()
                    };

                    bool exists = await _customerService.CheckTaxCodeExistsAsync(customer.TaxCode);
                    if (exists)
                    {
                        MessageBox.Show(LM.Get("sales_cus_err_dup_tax"), LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        btnSave.Enabled = true;
                        return;
                    }

                    await _customerService.AddCustomerAsync(customer);
                }
                else
                {
                    // Update
                    _currentCustomer.CompanyName = txtCompanyName.Text.Trim();
                    _currentCustomer.Address = txtAddress.Text.Trim();
                    _currentCustomer.Representative = txtRepresentative.Text.Trim();
                    _currentCustomer.ContactEmail = txtEmail.Text.Trim();
                    _currentCustomer.PhoneNumber = txtPhone.Text.Trim();

                    await _customerService.UpdateCustomerAsync(_currentCustomer);
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(LM.Get("sales_cus_err_save") + ex.Message, LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = true;
            }
        }
    }
}
