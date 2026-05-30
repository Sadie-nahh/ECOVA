using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.GUI.Helpers;
using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EnvContract.GUI.Forms.Admin
{
    /// <summary>
    /// </summary>
    public class EmployeeAddEditForm : Form
    {
        private readonly IEmployeeService _employeeService;
        private readonly string _employeeId;
        private UserDTO _currentEmployee;

        private Guna2TextBox txtFullName, txtPhone, txtEmployeeCode, txtEmail, txtAddress;
        private Guna2DateTimePicker dtpBirthDate;
        private Guna2ComboBox cboDepartment;
        private Guna2CirclePictureBox picAvatar;
        private Guna2Button btnSave, btnClose;
        private string _selectedAvatarPath;

        // Lưu trữ dữ liệu tự sinh cho nhân viên mới
        private string _generatedUserID;
        private string _generatedUsername;

        // Thứ tự phải khớp chính xác với thứ tự Items trong cboDepartment
        private static readonly string[] _roleIds = { "R01", "R02", "R03", "R04", "R05", "R06", "R07" };

        public EmployeeAddEditForm(IEmployeeService employeeService, string employeeId = null)
        {
            _employeeService = employeeService;
            _employeeId = employeeId;
            InitializeComponent();
            SetupUI();
            if (Program.AppIcon != null) this.Icon = Program.AppIcon;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(580, 620);
            this.BackColor = Color.White;
            this.ShowInTaskbar = false;
            this.Load += EmployeeAddEditForm_Load;

            var shadowForm = new Guna2ShadowForm(this) { TargetForm = this };
            var elipse = new Guna2Elipse { TargetControl = this, BorderRadius = UIConstants.BorderRadiusLarge };
            var dragControl = new Guna2DragControl { TargetControl = this };

            this.ResumeLayout(false);
        }

        private void SetupUI()
        {
            bool isEdit = !string.IsNullOrEmpty(_employeeId);

            var LM = EnvContract.Common.LanguageManager.Instance;
            var lblTitle = new Label
            {
                Text = isEdit ? LM.Get("admin_emp_title_edit") : LM.Get("admin_emp_title_add"),
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize = true,
                Location = new Point(0, 20),
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblTitle);
            lblTitle.Location = new Point((this.Width - lblTitle.PreferredWidth) / 2, 20);

            // Close button (X)
            btnClose = new Guna2Button
            {
                Text = "✕",
                Size = new Size(40, 40),
                Location = new Point(this.Width - 45, 15),
                FillColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BorderThickness = 0
            };
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);

            // Avatar section (centered, below title)
            picAvatar = new Guna2CirclePictureBox
            {
                Size = new Size(70, 70),
                Location = new Point((this.Width - 70) / 2, 85),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                FillColor = Color.FromArgb(240, 240, 240), 
                Cursor = Cursors.Hand
            };

            try
            {
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "assets", "images", "circle-user-solid.png");
                if (!System.IO.File.Exists(path)) path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "images", "circle-user-solid.png");
                if (System.IO.File.Exists(path))
                {
                    Image original = Image.FromFile(path);
                    Bitmap grayIcon = new Bitmap(original.Width, original.Height);
                    using (Graphics g = Graphics.FromImage(grayIcon))
                    {
                        var cm = new System.Drawing.Imaging.ColorMatrix(new float[][] {
                            new float[] { 0, 0, 0, 0, 0 },
                            new float[] { 0, 0, 0, 0, 0 },
                            new float[] { 0, 0, 0, 0, 0 },
                            new float[] { 0, 0, 0, 1, 0 },
                            new float[] { 0.5f, 0.5f, 0.5f, 0, 1 } 
                        });
                        var ia = new System.Drawing.Imaging.ImageAttributes();
                        ia.SetColorMatrix(cm);
                        
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height), 0, 0, original.Width, original.Height, GraphicsUnit.Pixel, ia);
                    }
                    picAvatar.Image = grayIcon;
                }
            } catch { }

            picAvatar.Click += PicAvatar_Click;
            this.Controls.Add(picAvatar);

            var lblAvatarHint = new Label
            {
                Text = LM.Get("admin_emp_ava"),
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            this.Controls.Add(lblAvatarHint);
            lblAvatarHint.Location = new Point((this.Width - lblAvatarHint.PreferredWidth) / 2, 158);

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
                    BackColor = Color.Transparent
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

            // Row 1: Họ và tên | Phòng ban
            int y1 = 180;
            CreateLabel(LM.Get("admin_emp_name"), leftCol, y1);
            txtFullName = CreateTextBox(LM.Get("admin_emp_name_ph"), leftCol, y1 + 28);

            CreateLabel(LM.Get("admin_emp_dept"), rightCol, y1);
            cboDepartment = new Guna2ComboBox
            {
                Font = new Font("Segoe UI", 10),
                Size = new Size(fieldWidth, 42),
                Location = new Point(rightCol, y1 + 28),
                BorderRadius = 8,
                FillColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Black,
                BorderColor = Color.FromArgb(220, 220, 220),
                BorderThickness = 1
            };
            cboDepartment.Items.AddRange(new object[] {
                LM.Get("role_system_admin"),
                LM.Get("role_director"),
                LM.Get("role_sales"),
                LM.Get("role_field"),
                LM.Get("role_lab"),
                LM.Get("role_planning"),
                LM.Get("role_result")
            });
            cboDepartment.SelectedIndex = 0;
            // Khi thay đổi phòng ban ở mode Add → cập nhật username tự sinh
            cboDepartment.SelectedIndexChanged += CboDepartment_SelectedIndexChanged;
            this.Controls.Add(cboDepartment);

            // Row 2: Ngày sinh | Điện thoại
            int y2 = 265;
            CreateLabel(LM.Get("admin_emp_dob"), leftCol, y2);
            dtpBirthDate = new Guna2DateTimePicker
            {
                CustomFormat = "dd/MM/yyyy",
                Format = DateTimePickerFormat.Custom,
                Font = new Font("Segoe UI", 10),
                Size = new Size(fieldWidth, 42),
                Location = new Point(leftCol, y2 + 28),
                BorderRadius = 8,
                FillColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.Black,
                BorderColor = Color.FromArgb(220, 220, 220),
                BorderThickness = 1,
                Value = new DateTime(2000, 1, 1)
            };
            this.Controls.Add(dtpBirthDate);

            CreateLabel(LM.Get("admin_emp_phone"), rightCol, y2);
            txtPhone = CreateTextBox(LM.Get("admin_emp_phone_ph"), rightCol, y2 + 28);

            // Row 3: ID Nhân viên (ReadOnly, tự sinh) | Gmail
            int y3 = 350;
            CreateLabel(LM.Get("admin_emp_id"), leftCol, y3);
            txtEmployeeCode = CreateTextBox(LM.Get("admin_emp_id_ph"), leftCol, y3 + 28);
            txtEmployeeCode.ReadOnly = true;
            txtEmployeeCode.FillColor = Color.FromArgb(230, 230, 230);

            CreateLabel(LM.Get("admin_emp_email"), rightCol, y3);
            txtEmail = CreateTextBox(LM.Get("admin_emp_email_ph"), rightCol, y3 + 28);

            // Row 4: Địa chỉ (full width)
            int y4 = 435;
            CreateLabel(LM.Get("admin_emp_addr"), leftCol, y4);
            txtAddress = CreateTextBox(LM.Get("admin_emp_addr_ph"), leftCol, y4 + 28, this.Width - 60);

            // Save button
            btnSave = new Guna2Button
            {
                Text = LM.Get("msg_save"),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                FillColor = UIConstants.SuccessColor,
                ForeColor = Color.White,
                Size = new Size(120, 45),
                Location = new Point(this.Width - 155, this.Height - 60),
                BorderRadius = 10,
                Cursor = Cursors.Hand
            };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
        }

        private void PicAvatar_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                var LM = EnvContract.Common.LanguageManager.Instance;
                ofd.Title = LM.Get("admin_emp_ava_title");
                ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _selectedAvatarPath = ofd.FileName;
                    picAvatar.Image = Image.FromFile(_selectedAvatarPath);
                }
            }
        }

        private async void EmployeeAddEditForm_Load(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_employeeId))
            {
                // === EDIT MODE ===
                try
                {
                    _currentEmployee = await _employeeService.GetEmployeeByIdAsync(_employeeId);
                    if (_currentEmployee != null)
                    {
                        txtFullName.Text = _currentEmployee.FullName;
                        int roleIdx = Array.IndexOf(_roleIds, _currentEmployee.RoleID ?? "R01");
                        if (roleIdx >= 0) cboDepartment.SelectedIndex = roleIdx;
                        dtpBirthDate.Value = _currentEmployee.DateOfBirth ?? new DateTime(2000, 1, 1);
                        txtPhone.Text = _currentEmployee.Phone;
                        txtEmployeeCode.Text = _currentEmployee.EmployeeCode ?? _currentEmployee.UserID;
                        txtEmail.Text = _currentEmployee.Email;
                        txtAddress.Text = _currentEmployee.Address;

                        txtEmployeeCode.ReadOnly = true;

                        if (_currentEmployee.AvatarData != null && _currentEmployee.AvatarData.Length > 0)
                        {
                            try
                            {
                                using (var ms = new MemoryStream(_currentEmployee.AvatarData))
                                {
                                    picAvatar.Image = Image.FromStream(ms);
                                }
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EmployeeForm] Avatar load error (corrupted data): {ex.Message}"); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var LM = EnvContract.Common.LanguageManager.Instance;
                    MessageBox.Show(LM.Get("admin_emp_err_load") + ex.Message, LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // === ADD MODE — Tự sinh EmployeeCode, UserID, Username ===
                await GenerateEmployeeDataForCurrentRole();
            }
        }

        /// <summary>
        /// Khi thay đổi phòng ban ở mode Add → tự sinh lại Username (EmployeeCode giữ nguyên)
        /// </summary>
        private async void CboDepartment_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_employeeId)) // Chỉ ở mode Add
            {
                await GenerateEmployeeDataForCurrentRole();
            }
        }

        /// <summary>
        /// Gọi service để sinh EmployeeCode, UserID, Username và điền vào form.
        /// </summary>
        private async System.Threading.Tasks.Task GenerateEmployeeDataForCurrentRole()
        {
            try
            {
                string roleId = DisplayToRoleId(cboDepartment.SelectedItem?.ToString() ?? "");
                var data = await _employeeService.GenerateNewEmployeeDataAsync(roleId);

                txtEmployeeCode.Text = data.EmployeeCode;
                _generatedUserID = data.UserID;
                _generatedUsername = data.Username;
            }
            catch (Exception ex)
            {
                // Fallback nếu lỗi
                var LM = EnvContract.Common.LanguageManager.Instance;
                txtEmployeeCode.Text = "EMP???";
                _generatedUserID = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                _generatedUsername = "user01";
                System.Diagnostics.Debug.WriteLine(LM.Get("admin_emp_err_gen") + ex.Message);
            }
        }

        private async void BtnSave_Click(object sender, EventArgs e)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            try
            {
                if (string.IsNullOrWhiteSpace(txtFullName.Text))
                {
                    MessageBox.Show(LM.Get("admin_emp_req_name"), LM.Get("msg_warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(_employeeId) && string.IsNullOrWhiteSpace(txtEmail.Text))
                {
                    MessageBox.Show(LM.Get("admin_emp_req_email"), LM.Get("msg_warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                btnSave.Enabled = false;

                // Read date of birth from DateTimePicker
                DateTime? dateOfBirth = dtpBirthDate.Value.Date;

                // Read avatar to byte[]
                byte[] avatarBytes = null;
                if (!string.IsNullOrEmpty(_selectedAvatarPath) && File.Exists(_selectedAvatarPath))
                {
                    avatarBytes = File.ReadAllBytes(_selectedAvatarPath);
                }

                // Map selected department to RoleID
                string roleId = DisplayToRoleId(cboDepartment.SelectedItem?.ToString() ?? "");
                string department = cboDepartment.SelectedItem?.ToString() ?? "";

                if (string.IsNullOrEmpty(_employeeId))
                {
                    // === ADD NEW EMPLOYEE ===
                    var employee = new UserDTO
                    {
                        UserID = _generatedUserID,
                        FullName = txtFullName.Text.Trim(),
                        Username = _generatedUsername,
                        // PasswordHash sẽ được set trong Service
                        Department = department,
                        Phone = txtPhone.Text.Trim(),
                        EmployeeCode = txtEmployeeCode.Text.Trim(),
                        Email = txtEmail.Text.Trim(),
                        Address = txtAddress.Text.Trim(),
                        IsActive = true,
                        RoleID = roleId,
                        DateOfBirth = dateOfBirth,
                        AvatarData = avatarBytes,
                        CreatedDate = DateTime.Now
                    };

                    string plainPassword = await _employeeService.AddEmployeeAsync(employee);

                    // Hiển thị thông báo thành công kèm thông tin tài khoản
                    string msg = string.Format(LM.Get("admin_emp_add_succ"),
                                               _generatedUsername,
                                               plainPassword,
                                               txtEmployeeCode.Text,
                                               txtEmail.Text.Trim());

                    MessageBox.Show(msg, LM.Get("msg_success"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    // === UPDATE EXISTING EMPLOYEE ===
                    _currentEmployee.FullName = txtFullName.Text.Trim();
                    _currentEmployee.Department = department;
                    _currentEmployee.Phone = txtPhone.Text.Trim();
                    _currentEmployee.Email = txtEmail.Text.Trim();
                    _currentEmployee.Address = txtAddress.Text.Trim();
                    _currentEmployee.RoleID = roleId;
                    _currentEmployee.DateOfBirth = dateOfBirth ?? _currentEmployee.DateOfBirth;

                    if (avatarBytes != null)
                        _currentEmployee.AvatarData = avatarBytes;

                    await _employeeService.UpdateEmployeeAsync(_currentEmployee);
                    MessageBox.Show(LM.Get("admin_emp_upd_succ"), LM.Get("msg_info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (InvalidOperationException ex)
            {
                // Lỗi nghiệp vụ (email trùng, dữ liệu không hợp lệ...)
                MessageBox.Show(ex.Message, LM.Get("msg_warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnSave.Enabled = true;
            }
            catch (Exception ex)
            {
                // Bắt lỗi SQL duplicate key và hiển thị thông báo thân thiện
                string errorMsg;
                if (ex.Message.Contains("UNIQUE KEY") || ex.Message.Contains("duplicate key"))
                {
                    errorMsg = LM.Get("admin_emp_err_dup");
                }
                else
                {
                    errorMsg = LM.Get("admin_emp_err_save") + ex.Message;
                }
                MessageBox.Show(errorMsg, LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnSave.Enabled = true;
            }
        }

        // === Role mapping (theo index — không phụ thuộc ngôn ngữ) ===
        private string DisplayToRoleId(string display)
        {
            // Dùng SelectedIndex để ánh xạ — bất biến theo ngôn ngữ
            int idx = cboDepartment.SelectedIndex;
            return (idx >= 0 && idx < _roleIds.Length) ? _roleIds[idx] : "R01";
        }

        private string RoleIdToDisplay(string roleId)
        {
            // Không cần dùng display trong edit mode — chỉ cần index để set SelectedIndex
            int idx = Array.IndexOf(_roleIds, roleId ?? "R01");
            return idx >= 0 ? cboDepartment.Items[idx]?.ToString() ?? "" : "";
        }
    }
}
