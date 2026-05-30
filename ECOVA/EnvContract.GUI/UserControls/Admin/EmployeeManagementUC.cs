using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using EnvContract.GUI.Forms.Main;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using EnvContract.Common;

namespace EnvContract.GUI.UserControls.Admin
{
    public class EmployeeManagementUC : UserControl
    {
        private IEmployeeService _employeeService;
        private VoiceSearchService _voiceService;
        
        private Guna2DataGridView dgvEmployees;
        private Guna2TextBox txtSearch;
        private Guna2ComboBox cboDepartment;
        private Guna2Button btnAdd;
        private Guna2Button _btnSendMail;
        private bool _isLoadingData = false;
        private Action _langHandler; // Named handler so we can unsubscribe on Dispose
        
        private Image _watermarkImage;
        private Bitmap _cachedWatermark;
        private Image _defaultAvatarIcon;
        
        public EmployeeManagementUC()
        {
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>();
            InitializeComponent();
            
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            try
            {
                string logoPath = FindAssetPath("Icon.png");
                if (!string.IsNullOrEmpty(logoPath))
                    _watermarkImage = Image.FromFile(logoPath);

                string userIcon = FindAssetPath("circle-user-solid.png");
                if (!string.IsNullOrEmpty(userIcon))
                    _defaultAvatarIcon = Image.FromFile(userIcon);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EmployeeUC] Error: {ex.Message}"); }

            if (!this.DesignMode)
            {
                _employeeService = Program.ServiceProvider.GetRequiredService<IEmployeeService>();
                this.Load += async (s, e) =>
                {
                    await LoadDataAsync();
                    // Phân quyền: chỉ R01 Admin được chỉnh sửa nhân viên
                    if (MainForm.IsReadOnlyForRole())  // không role nào khác được edit
                    {
                        btnAdd.Enabled = false;
                        btnAdd.FillColor = System.Drawing.Color.FromArgb(180, 180, 180);
                        _btnSendMail.Visible = false;   // Ẩn nút gửi mail nếu không phải Admin
                        if (dgvEmployees.Columns.Contains("colAction"))
                            dgvEmployees.Columns["colAction"].Visible = false;
                    }
                };
            }
        }

        private string FindAssetPath(string filename)
        {
            string[] candidates = {
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "assets", "images", filename),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "images", filename)
            };
            foreach (var c in candidates)
                if (System.IO.File.Exists(c)) return System.IO.Path.GetFullPath(c);

            var dir = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var path = System.IO.Path.Combine(dir.FullName, "assets", "images", filename);
                if (System.IO.File.Exists(path)) return path;
                dir = dir.Parent;
            }
            return null;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Dock = DockStyle.Fill;
            this.BackColor = UIConstants.WhiteBackground;

            var LM = LanguageManager.Instance;
            // 1. Header
            var lblTitle = new Label { Text = LM.Get("employee_title"), Font = new Font("Segoe UI", 24, FontStyle.Bold), Location = new Point(30, 25), AutoSize = true, ForeColor = Color.Black };
            var lblSubTitle = new Label { Text = LM.Get("employee_subtitle"), Font = new Font("Segoe UI", 12), Location = new Point(35, 73), AutoSize = true, ForeColor = UIConstants.TextDark };
            
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblSubTitle);

            // 2. Toolbar
            int toolY = 120;
            txtSearch = new Guna2TextBox 
            { 
                PlaceholderText = LM.Get("employee_search"), 
                PlaceholderForeColor = Color.FromArgb(64, 64, 64),
                Size = new Size(350, 45), 
                Location = new Point(30, toolY), 
                BorderRadius = 10,
                BorderThickness = 1,
                BorderColor = UIConstants.DarkGreenBackground,
                Font = new Font("Segoe UI", 11),
                FillColor = Color.FromArgb(226, 232, 219),
                ForeColor = UIConstants.TextDark,
                TextOffset = new Point(10, 0),
                IconRight = null 
            };
            txtSearch.TextChanged += async (s, e) => {
                if(txtSearch.Text.Length > 2 || txtSearch.Text.Length == 0)
                    await LoadDataAsync(txtSearch.Text, cboDepartment.SelectedItem?.ToString());
            };
            this.Controls.Add(txtSearch);
            VoiceSearchHelper.AttachVoiceButton(txtSearch, this, _voiceService,
                () => VoiceSearchHelper.ExtractGridContext(dgvEmployees, "FullName", "UserName"));

            cboDepartment = new Guna2ComboBox
            {
                Location = new Point(400, toolY),
                Size = new Size(260, 45),
                ItemHeight = 39,
                BorderRadius = 10,
                BorderThickness = 1,
                BorderColor = UIConstants.DarkGreenBackground,
                FillColor = Color.FromArgb(226, 232, 219),
                Font = new Font("Segoe UI", 11),
                ForeColor = UIConstants.TextDark
            };
            cboDepartment.Items.AddRange(new object[] { LM.Get("employee_all_dept"), LM.Get("employee_dept_sales"), LM.Get("employee_dept_planning"), LM.Get("employee_dept_field"), LM.Get("employee_dept_lab"), LM.Get("employee_dept_result") });
            cboDepartment.SelectedIndex = 0; 
            this.Controls.Add(cboDepartment);

            btnAdd = new Guna2Button 
            { 
                Text = LM.Get("employee_add"), 
                Size = new Size(220, 45), 
                BorderRadius = 10, 
                FillColor = Color.FromArgb(226, 232, 219), 
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BorderThickness = 1,
                BorderColor = UIConstants.DarkGreenBackground
            };
            btnAdd.Click += BtnAdd_Click;
            this.Controls.Add(btnAdd);

            // Nút Gửi Mail — ở trái nút Thêm nhân viên
            _btnSendMail = new Guna2Button
            {
                Text            = LM.Get("employee_send_mail"),
                Size            = new Size(155, 45),
                BorderRadius    = 10,
                FillColor       = Color.FromArgb(49, 87, 44),
                ForeColor       = Color.FromArgb(236, 243, 158),
                Font            = new Font("Segoe UI", 11, FontStyle.Bold),
                BorderThickness = 0,
                Animated        = false
            };
            _btnSendMail.Click += BtnSendMail_Click;
            this.Controls.Add(_btnSendMail);

            // 3. Grid
            dgvEmployees = new Guna2DataGridView
            {
                Location = new Point(30, 190),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeColumns = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = UIConstants.WhiteBackground, // Note: Cannot be Transparent for DataGridView
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                RowHeadersVisible = false,
                RowTemplate = { Height = 60 },
                AutoGenerateColumns = false, 
                AlternatingRowsDefaultCellStyle = { BackColor = UIConstants.VeryLightGreen }
            };
            
            // Header Styling
            dgvEmployees.ColumnHeadersHeight = 50;
            dgvEmployees.ColumnHeadersDefaultCellStyle.BackColor = UIConstants.SuccessColor; // #31572C
            dgvEmployees.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvEmployees.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            dgvEmployees.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvEmployees.ColumnHeadersDefaultCellStyle.SelectionBackColor = UIConstants.SuccessColor; // Khóa màu vùng chọn (ngăn bị đổi sang màu xanh blue)
            
            // Default Cell Styling
            dgvEmployees.DefaultCellStyle.BackColor = UIConstants.SoftLightGreen; // #C1DB99
            dgvEmployees.DefaultCellStyle.SelectionBackColor = UIConstants.LightGreenAccent;
            dgvEmployees.DefaultCellStyle.SelectionForeColor = Color.Black;
            dgvEmployees.DefaultCellStyle.Font = new Font("Segoe UI", 10);
            dgvEmployees.DefaultCellStyle.ForeColor = Color.Black;
            dgvEmployees.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            
            // Setup explicit columns
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFullName", DataPropertyName = "FullName", HeaderText = LM.Get("employee_col_name"), Width = 250, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "colID", DataPropertyName = "EmployeeCode", HeaderText = LM.Get("employee_col_code"), Width = 100 });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDepartment", DataPropertyName = "RoleID", HeaderText = LM.Get("employee_col_dept"), Width = 180, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAddress", DataPropertyName = "Address", HeaderText = LM.Get("employee_col_address"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
            dgvEmployees.Columns.Add(new DataGridViewTextBoxColumn { Name = "colContact", DataPropertyName = "Phone", HeaderText = LM.Get("employee_col_contact"), Width = 200, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleLeft } });
            dgvEmployees.Columns.Add(new DataGridViewButtonColumn { Name = "colAction", HeaderText = LM.Get("employee_col_action"), Width = 180, UseColumnTextForButtonValue = false });

            foreach (DataGridViewColumn col in dgvEmployees.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            dgvEmployees.CellPainting += DgvEmployees_CellPainting;
            dgvEmployees.CellFormatting += DgvEmployees_CellFormatting;
            dgvEmployees.CellClick += DgvEmployees_CellClick;
            dgvEmployees.CellMouseMove += (s, e) => { if (e.RowIndex >= 0 && dgvEmployees.Columns[e.ColumnIndex].Name == "colAction") dgvEmployees.Cursor = Cursors.Hand; else dgvEmployees.Cursor = Cursors.Default; };

            cboDepartment.SelectedIndexChanged += CboDepartment_SelectedIndexChangedHandler;

            this.Controls.Add(dgvEmployees);

            this.Resize += (s, e) => DoLayout();

            _langHandler = () =>
            {
                if (this.IsDisposed) return;
                if (dgvEmployees == null || dgvEmployees.IsDisposed) return;

                var lm = LanguageManager.Instance;
                if (lblTitle    != null) lblTitle.Text    = lm.Get("employee_title");
                if (lblSubTitle != null) lblSubTitle.Text = lm.Get("employee_subtitle");
                if (txtSearch   != null) txtSearch.PlaceholderText = lm.Get("employee_search");
                if (btnAdd      != null) btnAdd.Text      = lm.Get("employee_add");
                if (_btnSendMail != null) _btnSendMail.Text = lm.Get("employee_send_mail");

                // Safe column header update
                void SetHeader(string colName, string key)
                {
                    if (dgvEmployees.Columns[colName] != null)
                        dgvEmployees.Columns[colName].HeaderText = lm.Get(key);
                }
                SetHeader("colFullName",  "employee_col_name");
                SetHeader("colID",        "employee_col_code");
                SetHeader("colDepartment","employee_col_dept");
                SetHeader("colAddress",   "employee_col_address");
                SetHeader("colContact",   "employee_col_contact");
                SetHeader("colAction",    "employee_col_action");

                // Re-populate cboDepartment items — giữ SelectedIndex hiện tại
                if (cboDepartment != null)
                {
                    int selIdx = cboDepartment.SelectedIndex;
                    cboDepartment.SelectedIndexChanged -= CboDepartment_SelectedIndexChangedHandler;
                    cboDepartment.Items.Clear();
                    cboDepartment.Items.AddRange(new object[]
                    {
                        lm.Get("employee_all_dept"),
                        lm.Get("employee_dept_sales"),
                        lm.Get("employee_dept_planning"),
                        lm.Get("employee_dept_field"),
                        lm.Get("employee_dept_lab"),
                        lm.Get("employee_dept_result")
                    });
                    cboDepartment.SelectedIndex = (selIdx >= 0 && selIdx < cboDepartment.Items.Count) ? selIdx : 0;
                    cboDepartment.SelectedIndexChanged += CboDepartment_SelectedIndexChangedHandler;
                }

                dgvEmployees.Invalidate();
            };
            LanguageManager.Instance.LanguageChanged += _langHandler;
            _langHandler(); // Apply immediately

            this.ResumeLayout(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _langHandler != null)
            {
                LanguageManager.Instance.LanguageChanged -= _langHandler;
                _langHandler = null;
            }
            base.Dispose(disposing);
        }

        private void DoLayout()

        {
            if (this.Width == 0 || this.Height == 0) return;

            int minW = 1000;
            int minH = 700;
            int w = Math.Max(this.Width, minW);
            int h = Math.Max(this.Height, minH);

            int pad = 30;
            
            // Toolbar layout
            int toolY = 120;
            txtSearch.Location     = new Point(pad, toolY);
            cboDepartment.Location = new Point(pad + txtSearch.Width + 20, toolY);
            btnAdd.Location        = new Point(w - pad - btnAdd.Width, toolY);
            _btnSendMail.Location  = new Point(w - pad - btnAdd.Width - 10 - _btnSendMail.Width, toolY);
            
            // Grid layout
            int gridY = 190;
            dgvEmployees.SetBounds(pad, gridY, w - pad * 2, h - gridY - pad);

        }

        private void BtnSendMail_Click(object sender, EventArgs e)
        {
            // ── Kiểm tra SMTP đã được cấu hình chưa ─────────────────────────────
            // Nếu DPAPI thất bại (máy khác) hoặc chưa config → hiện SmtpSetupForm trước
            bool smtpReady = EnvContract.Common.Helpers.EmailSmtpHelper.IsConfigured;

            if (!smtpReady || Program.SmtpDecryptFailed)
            {
                string msg = Program.SmtpDecryptFailed
                    ? "⚠️  Mật khẩu SMTP được mã hóa trên máy khác, không dùng được trên máy này.\n\n" +
                      "Vui lòng cấu hình lại SMTP cho máy này (chỉ cần làm 1 lần)."
                    : "⚠️  SMTP chưa được cấu hình.\n\nVui lòng nhập thông tin SMTP để bật tính năng gửi email.";

                var ask = MessageBox.Show(
                    msg + "\n\nMở cửa sổ cấu hình SMTP ngay?",
                    "Cần cấu hình SMTP", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);

                if (ask != DialogResult.Yes) return;

                using var setupDlg = new Forms.Admin.SmtpSetupForm(isDpapiFailed: Program.SmtpDecryptFailed);
                var result = setupDlg.ShowDialog(this.FindForm());
                if (result != DialogResult.OK) return; // Admin hủy → không mở SendMailForm
            }

            // ── Mở form gửi mail ─────────────────────────────────────────────────
            var notifService = Program.ServiceProvider
                .GetRequiredService<EnvContract.BLL.Interfaces.INotificationService>();
            using var dlg = new Forms.Admin.SendMailForm(notifService, _employeeService);
            dlg.ShowDialog(this.FindForm());
        }


        private static string RoleToDisplayName(string roleId)
        {
            var LM = LanguageManager.Instance;
            return roleId switch
            {
                "R01" => LM.Get("role_system_admin"),
                "R02" => LM.Get("role_director"),
                "R03" => LM.Get("role_sales"),
                "R04" => LM.Get("role_field"),
                "R05" => LM.Get("role_lab"),
                "R06" => LM.Get("role_planning"),
                "R07" => LM.Get("role_result"),
                _     => roleId ?? LM.Get("role_unassigned")
            };
        }


        private void DgvEmployees_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var dataItem = dgvEmployees.Rows[e.RowIndex].DataBoundItem as UserDTO;
            if (dataItem == null) return;

            // Đổi mã RoleID -> tên phòng ban tiếng Việt
            if (dgvEmployees.Columns[e.ColumnIndex].Name == "colDepartment")
            {
                e.Value = RoleToDisplayName(dataItem.RoleID);
                e.FormattingApplied = true;
            }

            if (!dataItem.IsActive)
            {
                e.CellStyle.BackColor = Color.FromArgb(235, 235, 235);
                e.CellStyle.ForeColor = Color.DimGray;
                e.CellStyle.SelectionBackColor = Color.FromArgb(220, 220, 220);
                e.CellStyle.SelectionForeColor = Color.DimGray;
            }
        }

        private void DgvEmployees_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var grid = (DataGridView)sender;
            var colName = grid.Columns[e.ColumnIndex].Name;
            var row = grid.Rows[e.RowIndex];
            var dataItem = row.DataBoundItem as UserDTO;

            if (colName == "colFullName")
            {
                e.PaintBackground(e.ClipBounds, true);

                // Avatar
                int avatarSize = 40;
                int padX = e.CellBounds.Left + 10;
                int padY = e.CellBounds.Top + (e.CellBounds.Height - avatarSize) / 2;
                Rectangle avatarRect = new Rectangle(padX, padY, avatarSize, avatarSize);

                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(avatarRect);
                    e.Graphics.SetClip(path);
                    if (dataItem?.AvatarData != null && dataItem.AvatarData.Length > 0)
                    {
                        try
                        {
                            using (var ms = new System.IO.MemoryStream(dataItem.AvatarData))
                            {
                                var img = Image.FromStream(ms);
                                if (!dataItem.IsActive)
                                {
                                    var cm = new System.Drawing.Imaging.ColorMatrix(new float[][] {
                                        new float[] {0.3f, 0.3f, 0.3f, 0, 0},
                                        new float[] {0.59f, 0.59f, 0.59f, 0, 0},
                                        new float[] {0.11f, 0.11f, 0.11f, 0, 0},
                                        new float[] {0, 0, 0, 1, 0},
                                        new float[] {0, 0, 0, 0, 1}
                                    });
                                    var ia = new System.Drawing.Imaging.ImageAttributes();
                                    ia.SetColorMatrix(cm);
                                    e.Graphics.DrawImage(img, avatarRect, 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
                                }
                                else
                                {
                                    e.Graphics.DrawImage(img, avatarRect);
                                }
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[EmployeeUC] Avatar paint error: {ex.Message}"); e.Graphics.FillEllipse(Brushes.Gray, avatarRect); }
                    }
                    else
                    {
                        if (_defaultAvatarIcon != null)
                        {
                            var cm = new System.Drawing.Imaging.ColorMatrix(new float[][] {
                                new float[] { 0, 0, 0, 0, 0 },
                                new float[] { 0, 0, 0, 0, 0 },
                                new float[] { 0, 0, 0, 0, 0 },
                                new float[] { 0, 0, 0, 1, 0 },
                                new float[] { 0.7f, 0.7f, 0.7f, 0, 1 }
                            });
                            var ia = new System.Drawing.Imaging.ImageAttributes();
                            ia.SetColorMatrix(cm);
                            
                            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            e.Graphics.DrawImage(_defaultAvatarIcon, avatarRect, 0, 0, _defaultAvatarIcon.Width, _defaultAvatarIcon.Height, GraphicsUnit.Pixel, ia);
                        }
                        else 
                        {
                            e.Graphics.FillEllipse(Brushes.Gray, avatarRect);
                        }
                    }
                    e.Graphics.ResetClip();
                }

                // Text
                if (dataItem != null)
                {
                    string nameText = dataItem.FullName ?? "N/A";

                    var nameFont = new Font("Segoe UI", 10, FontStyle.Bold);
                    var textBrush = dataItem.IsActive ? Brushes.Black : Brushes.DimGray;

                    int textX = avatarRect.Right + 10;
                    
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(nameText, nameFont, textBrush, new RectangleF(textX, e.CellBounds.Top, e.CellBounds.Width - textX, e.CellBounds.Height), sf);
                }

                e.Handled = true;
            }
            else if (colName == "colContact")
            {
                e.PaintBackground(e.ClipBounds, true);
                if (dataItem != null)
                {
                    string phoneText = string.IsNullOrEmpty(dataItem.Phone) ? "0123456787" : dataItem.Phone;
                    string emailText = string.IsNullOrEmpty(dataItem.Email) ? "pna@gmail.com" : dataItem.Email;

                    var font = new Font("Segoe UI", 10, FontStyle.Regular);
                    var textBrush = dataItem.IsActive ? Brushes.Black : Brushes.DimGray;

                    e.Graphics.DrawString(phoneText, font, textBrush, new PointF(e.CellBounds.Left + 35, e.CellBounds.Top + 10));
                    e.Graphics.DrawString(emailText, font, textBrush, new PointF(e.CellBounds.Left + 35, e.CellBounds.Top + 30));
                }
                e.Handled = true;
            }
            else if (colName == "colAction")
            {
                e.PaintBackground(e.ClipBounds, true);

                int btnWidth = 50;
                int btnHeight = 30;
                int space = 5;

                // Center the 3 buttons horizontally
                int totalWidth = btnWidth * 3 + space * 2;
                int startX = e.CellBounds.Left + (e.CellBounds.Width - totalWidth) / 2;
                int startY = e.CellBounds.Top + (e.CellBounds.Height - btnHeight) / 2;

                Rectangle rectEdit = new Rectangle(startX, startY, btnWidth, btnHeight);
                Rectangle rectToggle = new Rectangle(startX + btnWidth + space, startY, btnWidth, btnHeight);
                Rectangle rectDelete = new Rectangle(startX + btnWidth * 2 + space * 2, startY, btnWidth, btnHeight);

                using (var pathEdit = GetRoundedRect(rectEdit, 8))
                using (var pathToggle = GetRoundedRect(rectToggle, 8))
                using (var pathDelete = GetRoundedRect(rectDelete, 8))
                {
                    // "Sửa" Button (Dark Green)
                    e.Graphics.FillPath(new SolidBrush(UIConstants.SuccessColor), pathEdit);
                    // "Ẩn/Hiện" Button — xám nếu là chính mình (không được tự khóa)
                    bool isSelf = dataItem != null &&
                        dataItem.UserID == EnvContract.Common.AppState.Instance.CurrentUser?.UserID;
                    if (isSelf) {
                        e.Graphics.FillPath(new SolidBrush(Color.FromArgb(180, 180, 180)), pathToggle); // Xám = disabled
                    } else if (dataItem != null && !dataItem.IsActive) {
                        e.Graphics.FillPath(new SolidBrush(UIConstants.PrimaryColor), pathToggle); // Xanh lá
                    } else {
                        e.Graphics.FillPath(new SolidBrush(Color.FromArgb(215, 160, 70)), pathToggle); // Vàng đất
                    }

                    // "Xóa" Button 
                    e.Graphics.FillPath(new SolidBrush(Color.FromArgb(89, 135, 59)), pathDelete); // Xanh lá cây

                    var lm = LanguageManager.Instance;
                    string toggleText = (dataItem != null && !dataItem.IsActive)
                        ? lm.Get("employee_btn_unlock")
                        : lm.Get("employee_btn_lock");

                    var btnFont = new Font("Segoe UI", 9, FontStyle.Bold);
                    var btnSf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(lm.Get("employee_btn_edit"), btnFont, Brushes.White, rectEdit, btnSf);
                    e.Graphics.DrawString(toggleText, btnFont, Brushes.White, rectToggle, btnSf);
                    e.Graphics.DrawString(lm.Get("employee_btn_delete"), btnFont, Brushes.White, rectDelete, btnSf);
                }

                e.Handled = true;
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(bounds.Location, size);
            GraphicsPath path = new GraphicsPath();

            if (radius == 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);

            if (_watermarkImage == null) return;

            int logoSize = Math.Min(this.Width, this.Height) * 2 / 3;
            if (logoSize < 200) logoSize = 200;

            if (_cachedWatermark == null || _cachedWatermark.Width != logoSize)
            {
                _cachedWatermark?.Dispose();
                _cachedWatermark = new Bitmap(logoSize, logoSize, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(_cachedWatermark))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    var cm = new ColorMatrix { Matrix33 = 0.08f }; // Opacity
                    var ia = new ImageAttributes();
                    ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                    g.DrawImage(_watermarkImage,
                        new Rectangle(0, 0, logoSize, logoSize),
                        0, 0, _watermarkImage.Width, _watermarkImage.Height,
                        GraphicsUnit.Pixel, ia);
                    ia.Dispose();
                }
            }

            int x = (this.Width - logoSize) / 2 + 60;
            int y = (this.Height - logoSize) / 2 + 40;
            e.Graphics.DrawImage(_cachedWatermark, x, y);
        }

        private async void CboDepartment_SelectedIndexChangedHandler(object sender, EventArgs e)
        {
            await LoadDataAsync(txtSearch.Text, cboDepartment.SelectedItem?.ToString());
        }

        private async Task LoadDataAsync(string keyword = "", string department = null)
        {
            if (_isLoadingData) return; // Tránh gọi đồng thời
            _isLoadingData = true;
            try {
                var data = await _employeeService.SearchEmployeesAsync(keyword);
                // Bỏ lọc IsActive để hiển thị cả những NV đã bị ẩn
                if (!string.IsNullOrEmpty(department) && department != LanguageManager.Instance.Get("employee_all_dept"))
                {
                    data = data.FindAll(emp =>
                        (RoleToDisplayName(emp.RoleID).IndexOf(department, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        ((emp.Department ?? "").IndexOf(department, StringComparison.OrdinalIgnoreCase) >= 0));
                }
                dgvEmployees.DataSource = null;
                dgvEmployees.DataSource = data;
                dgvEmployees.Refresh();
            } catch {
                // Ignore errors if DB is not attached during styling test
            } finally {
                _isLoadingData = false;
            }
        }

        private async void BtnAdd_Click(object sender, EventArgs e)
        {
            using (var form = new EnvContract.GUI.Forms.Admin.EmployeeAddEditForm(_employeeService))
            {
                form.ShowDialog(this.FindForm());
            }
            // Reset filter to "Tất cả phòng ban" nhưng tạm ngắt event để tránh reload 2 lần
            cboDepartment.SelectedIndexChanged -= CboDepartment_SelectedIndexChangedHandler;
            cboDepartment.SelectedIndex = 0;
            cboDepartment.SelectedIndexChanged += CboDepartment_SelectedIndexChangedHandler;
            await LoadDataAsync(txtSearch.Text, cboDepartment.SelectedItem?.ToString());
        }

        private async void DgvEmployees_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvEmployees.Columns[e.ColumnIndex].Name != "colAction") return;

            var dataItem = dgvEmployees.Rows[e.RowIndex].DataBoundItem as UserDTO;
            if (dataItem == null) return;

            var cellBounds = dgvEmployees.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
            var mousePos = dgvEmployees.PointToClient(Cursor.Position);

            int btnWidth = 50;
            int space = 5;
            int totalWidth = btnWidth * 3 + space * 2;
            int startX = cellBounds.Left + (cellBounds.Width - totalWidth) / 2;

            int midX1 = startX + btnWidth + space / 2;
            int midX2 = startX + btnWidth * 2 + space + space / 2;

            if (mousePos.X < midX1)
            {
                // "Sửa" button clicked
                using (var form = new EnvContract.GUI.Forms.Admin.EmployeeAddEditForm(_employeeService, dataItem.UserID))
                {
                    form.ShowDialog(this.FindForm());
                }
                await LoadDataAsync(txtSearch.Text, cboDepartment.SelectedItem?.ToString());
            }
            else if (mousePos.X < midX2)
            {
                // "Ẩn/Hiện" button clicked — chặn tự khóa tài khoản chính mình
                if (dataItem.UserID == EnvContract.Common.AppState.Instance.CurrentUser?.UserID)
                {
                    var lmSelf = LanguageManager.Instance;
                    MessageBox.Show(
                        lmSelf.Get("employee_cant_lock_self"),
                        lmSelf.Get("employee_not_allowed"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var lmToggle = LanguageManager.Instance;
                string actionWord = dataItem.IsActive
                    ? lmToggle.Get("employee_lock_word")
                    : lmToggle.Get("employee_unlock_word");
                var result = MessageBox.Show(
                    string.Format(lmToggle.Get("employee_confirm_toggle"), actionWord, dataItem.FullName),
                    lmToggle.Get("msg_confirm"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    try
                    {
                        bool newActiveState = !dataItem.IsActive;
                        await _employeeService.ToggleActiveAsync(dataItem.UserID, newActiveState);
                        MessageBox.Show(
                            string.Format(lmToggle.Get("employee_toggle_success"), actionWord),
                            lmToggle.Get("msg_info"),
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await LoadDataAsync(txtSearch.Text, cboDepartment.SelectedItem?.ToString());
                    }
                    catch (Exception ex)
                    {
                        var lmErr = LanguageManager.Instance;
                        MessageBox.Show(lmErr.Get("msg_error") + ": " + ex.Message, lmErr.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            else
            {
                // "Xóa vĩnh viễn" button clicked — chặn tự xóa chính mình
                if (dataItem.UserID == EnvContract.Common.AppState.Instance.CurrentUser?.UserID)
                {
                    var lmSelf2 = LanguageManager.Instance;
                    MessageBox.Show(
                        lmSelf2.Get("employee_cant_delete_self"),
                        lmSelf2.Get("employee_not_allowed"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var lmDel = LanguageManager.Instance;
                var resultDel = MessageBox.Show(
                    string.Format(lmDel.Get("employee_delete_confirm"), dataItem.FullName),
                    lmDel.Get("employee_delete_title"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);

                if (resultDel == DialogResult.Yes)
                {
                    try
                    {
                        await _employeeService.DeleteEmployeeAsync(dataItem.UserID);
                        MessageBox.Show(lmDel.Get("employee_delete_success"), lmDel.Get("msg_info"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        await LoadDataAsync(txtSearch.Text, cboDepartment.SelectedItem?.ToString());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(lmDel.Get("msg_error") + ": " + ex.Message, lmDel.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}

