using System;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.UserControls.Dashboards;
using Microsoft.Extensions.DependencyInjection;
using EnvContract.Common;
using EnvContract.GUI.Forms.Profile;
using EnvContract.GUI.Services;

namespace EnvContract.GUI.Forms.Main
{
    public partial class MainForm : Form
    {
        private Guna2Button currentBtn;

        public MainForm()
        {
            InitializeComponent();
            this.MinimumSize = new Size(1200, 720);
            AssignSidebarEvents();

            // Cập nhật thông tin User sau khi Login
            if (AppState.Instance.CurrentUser != null)
            {
                var user = AppState.Instance.CurrentUser;
                lblUserName.Text = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
                
                string roleText = GetRoleText(user.RoleID);
                lblUserRole.Text = roleText;

                // Load avatar từ AvatarData đã lưu trong DB
                if (user.AvatarData != null && user.AvatarData.Length > 0)
                {
                    try
                    {
                        using var ms = new System.IO.MemoryStream(user.AvatarData);
                        pbAvatar.Image = Image.FromStream(ms);
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainForm] Avatar load error: {ex.Message}"); }
                }

                // Phân quyền hiển thị menu sidebar theo Role
                ApplyRoleBasedAccess(user.RoleID);
            }

            // Xử lý sự kiện đăng xuất
            btnLogout.Click += async (s, e) => {
                var LM = LanguageManager.Instance;
                var res = MessageBox.Show(LM.Get("logout_confirm"), LM.Get("confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    EnvContract.Common.AppState.Instance.CurrentUser = null;
                    this.IsLoggingOut = true;
                    var loginForm = Application.OpenForms.OfType<Auth.Login>().FirstOrDefault();
                    if (loginForm != null)
                    {
                        loginForm.ShowIntroPanel();
                        await Helpers.FormTransitionHelper.TransitionAsync(this, loginForm, closeFrom: true);
                    }
                }
            };

            // Mở popup Sửa Hồ Sơ khi click vào pnlUserInfo (avatar/tên/role)
            void OpenEditProfile()
            {
                var form = new EditProfileForm();
                form.ProfileUpdated += () =>
                {
                    var u = AppState.Instance.CurrentUser;
                    if (u == null) return;

                    // Refresh tên trên sidebar
                    lblUserName.Text = string.IsNullOrWhiteSpace(u.FullName) ? u.Username : u.FullName;

                    // Refresh avatar trên sidebar
                    if (u.AvatarData != null && u.AvatarData.Length > 0)
                    {
                        try
                        {
                            using var ms = new System.IO.MemoryStream(u.AvatarData);
                            pbAvatar.Image = Image.FromStream(ms);
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainForm] Avatar refresh error: {ex.Message}"); }
                    }
                };
                form.ShowDialog(this);
            }

            pnlUserInfo.Cursor  = Cursors.Hand;
            pbAvatar.Cursor     = Cursors.Hand;
            lblUserName.Cursor  = Cursors.Hand;
            lblUserRole.Cursor  = Cursors.Hand;

            pnlUserInfo.Click  += (s, e) => OpenEditProfile();
            pbAvatar.Click     += (s, e) => OpenEditProfile();
            lblUserName.Click  += (s, e) => OpenEditProfile();
            lblUserRole.Click  += (s, e) => OpenEditProfile();

            // Subscribe to language change
            LanguageManager.Instance.LanguageChanged += () =>
            {
                ApplyLanguage();
            };
        }

        private void AssignSidebarEvents()
        {
            btnDashboard.Click += (s, e) => {
                ActivateButton(s);
                LoadUserControl(new DashboardUC());
            };
            btnCustomer.Click += (s, e) => {
                ActivateButton(s);
                LoadUserControl(Program.ServiceProvider.GetRequiredService<EnvContract.GUI.UserControls.Sales.CustomerManagementUC>());
            };
            btnContract.Click += (s, e) => {
                ActivateButton(s);
                LoadUserControl(Program.ServiceProvider.GetRequiredService<EnvContract.GUI.UserControls.FieldAndLab.EnterResultUC>());
            };
            btnEmployee.Click += (s, e) => {
                ActivateButton(s);
                LoadUserControl(Program.ServiceProvider.GetRequiredService<EnvContract.GUI.UserControls.Admin.EmployeeManagementUC>());
            };
            btnPlanning.Click += (s, e) => {
                ActivateButton(s);
                LoadUserControl(Program.ServiceProvider.GetRequiredService<EnvContract.GUI.UserControls.Planning.SampleConfigUC>());
            };
            btnTesting.Click += (s, e) => {
                ActivateButton(s);
                LoadUserControl(Program.ServiceProvider.GetRequiredService<EnvContract.GUI.UserControls.FieldAndLab.LabResultUC>());
            };
            btnApproval.Click += (s, e) => {
                ActivateButton(s);
                LoadUserControl(Program.ServiceProvider.GetRequiredService<EnvContract.GUI.UserControls.QAAndDirector.DirectorApprovalUC>());
            };
            btnNotification.Click += (s, e) => {
                ActivateButton(s);
                LoadUserControl(Program.ServiceProvider.GetRequiredService<EnvContract.GUI.UserControls.Notification.NotificationUC>());
            };
        }

        /// <summary>
        /// Tất cả role đều thấy tất cả menu (xem nội dung),
        /// nhưng chỉ được chỉnh sửa module của mình.
        /// </summary>
        private void ApplyRoleBasedAccess(string roleId)
        {
            // Tất cả nút luôn hiển thị — mọi user đều có thể xem tất cả phòng ban
            btnEmployee.Visible = true;
            btnCustomer.Visible = true;
            btnPlanning.Visible = true;
            btnContract.Visible = true;
            btnTesting.Visible = true;
            btnApproval.Visible = true;
            btnDashboard.Visible = true;
            btnNotification.Visible = true;

            // Sắp xếp lại (đầy đủ nên không cần reposition, nhưng gọi để đảm bảo)
            RepositionSidebarButtons();
        }

        /// <summary>
        /// Kiểm tra xem roleId hiện tại có phải chế độ read-only cho module chỉ định không.
        /// Dùng bởi các UserControl để disable nút Save/Edit khi không có quyền.
        /// </summary>
        /// <param name="moduleOwnerRoles">Danh sách RoleID được phép chỉnh sửa module này</param>
        public static bool IsReadOnlyForRole(params string[] moduleOwnerRoles)
        {
            var currentRole = EnvContract.Common.AppState.Instance.CurrentUser?.RoleID ?? "";
            // Admin (R01) luôn có full quyền
            if (currentRole == "R01") return false;
            return !moduleOwnerRoles.Contains(currentRole);
        }

        /// <summary>
        /// Sắp xếp lại vị trí Y của các nút sidebar hiện còn Visible,
        /// để không có khoảng trống giữa các nút khi ẩn bớt.
        /// </summary>
        private void RepositionSidebarButtons()
        {
            var orderedButtons = new[] 
            { 
                btnDashboard, btnEmployee, btnCustomer, btnPlanning,
                btnContract, btnTesting, btnApproval, btnNotification 
            };

            int startY = 100;
            int stepY = 60;
            int index = 0;

            foreach (var btn in orderedButtons)
            {
                if (btn.Visible)
                {
                    btn.Location = new Point(btn.Location.X, startY + stepY * index);
                    index++;
                }
            }
        }

        public bool IsLoggingOut = false;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            btnDashboard.PerformClick();

            // ── Session timeout: tự logout khi không dùng trong N phút ─────────
            int timeout = AppConfig.Session.TimeoutMinutes;
            SessionTimeoutService.Start(timeout, async () =>
            {
                AppLogger.Warning($"Session: Không hoạt động sau {timeout} phút → tự đăng xuất");
                var LM = EnvContract.Common.LanguageManager.Instance;
                MessageBox.Show(
                    string.Format(LM.Get("main_timeout_msg"), timeout),
                    LM.Get("main_timeout_title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                EnvContract.Common.AppState.Instance.CurrentUser = null;
                this.IsLoggingOut = true;
                this.Invoke(new Action(async () => {
                    var loginForm = Application.OpenForms.OfType<Auth.Login>().FirstOrDefault();
                    if (loginForm != null)
                    {
                        loginForm.ShowIntroPanel();
                        await Helpers.FormTransitionHelper.TransitionAsync(this, loginForm, closeFrom: true);
                    }
                }));
            });
        }

        // Reset timer khi có mouse move
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SessionTimeoutService.Reset();
        }

        // Reset timer khi có phím bất kỳ
        protected override bool ProcessKeyPreview(ref Message m)
        {
            SessionTimeoutService.Reset();
            return base.ProcessKeyPreview(ref m);
        }

        // Dừng timer khi đóng form
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SessionTimeoutService.Stop();
            base.OnFormClosed(e);
        }

        /// <summary>
        /// Xử lý Animation và Color Hover khi Click vào 1 Tab Menu bên Sidebar (Chuẩn theo Figma)
        /// </summary>
        private void ActivateButton(object senderBtn)
        {
            if (senderBtn != null)
            {
                // Tắt trạng thái Button cũ
                DisableButton();
                
                // Active Button mới
                currentBtn = (Guna2Button)senderBtn;
                currentBtn.FillColor = System.Drawing.Color.FromArgb(236, 243, 158); // Xanh nõn chuối sáng (Figma)
                currentBtn.ForeColor = System.Drawing.Color.FromArgb(24, 46, 26); // Chữ màu xanh đen
                float activeFontSize = GetSidebarFontSize() + 0.25F;
                currentBtn.Font = new Font("Segoe UI", activeFontSize, FontStyle.Bold, GraphicsUnit.Point);
            }
        }

        private void DisableButton()
        {
            if (currentBtn != null)
            {
                currentBtn.FillColor = System.Drawing.Color.FromArgb(49, 87, 44); // Trả lại nền xanh rêu
                currentBtn.ForeColor = Color.White;
                currentBtn.Font = new Font("Segoe UI", GetSidebarFontSize(), FontStyle.Bold, GraphicsUnit.Point);
            }
        }

        /// <summary>Lấy font size phù hợp với ngôn ngữ hiện tại — VI=11.25, EN=9.75.</summary>
        private static float GetSidebarFontSize()
        {
            var raw = LanguageManager.Instance.Get("sidebar_font_size");
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float size) ? size : 11.25F;
        }

        /// <summary>
        /// Kỹ thuật Single Page Application - Load UserControl vào Panel Content 
        /// Không Refresh/Tạo form mới, giúp chuyển trang cực mượt.
        /// </summary>
        public void LoadUserControl(UserControl userControl)
        {
            // Cải thiện Memory Leaks (Giai đoạn 5): Dispose Control cũ trước khi nạp Control mới
            foreach (Control ctrl in pnlContent.Controls)
            {
                ctrl.Dispose();
            }
            pnlContent.Controls.Clear();

            userControl.Dock = DockStyle.Fill;
            pnlContent.Controls.Add(userControl);
            userControl.BringToFront();
            pnlContent.PerformLayout(); // Trigger layout engine to size pnlContent → UC → DoLayout fires with final size
        }

        private string GetRoleText(string roleId)
        {
            var LM = LanguageManager.Instance;
            return roleId switch
            {
                "R01" => LM.Get("role_admin"),
                "R02" => LM.Get("role_director"),
                "R03" => LM.Get("role_sales"),
                "R04" => LM.Get("role_field"),
                "R05" => LM.Get("role_lab"),
                "R06" => LM.Get("role_planning"),
                "R07" => LM.Get("role_result"),
                _ => LM.Get("role_employee")
            };
        }

        private void ApplyLanguage()
        {
            var LM = LanguageManager.Instance;
            this.Text = LM.Get("form_title");
            lblLang.Text = LM.ToggleLabel;

            // Sidebar buttons
            btnDashboard.Text = LM.Get("sidebar_dashboard");
            btnEmployee.Text = LM.Get("sidebar_employee");
            btnCustomer.Text = LM.Get("sidebar_customer");
            btnPlanning.Text = LM.Get("sidebar_planning");
            btnContract.Text = LM.Get("sidebar_field");
            btnTesting.Text = LM.Get("sidebar_lab");
            btnApproval.Text = LM.Get("sidebar_result");
            btnNotification.Text = LM.Get("sidebar_notification");
            btnLogout.Text = LM.Get("sidebar_logout");

            // Tự động điều chỉnh font size để tránh text bị cắt khi EN dài hơn VI
            float sidebarFontSize = float.Parse(LM.Get("sidebar_font_size"),
                System.Globalization.CultureInfo.InvariantCulture);
            var sidebarFont = new Font("Segoe UI", sidebarFontSize, FontStyle.Bold, GraphicsUnit.Point);

            var sidebarButtons = new[] { btnDashboard, btnEmployee, btnCustomer, btnPlanning,
                                         btnContract, btnTesting, btnApproval, btnNotification, btnLogout };
            foreach (var btn in sidebarButtons)
            {
                // Giữ nguyên font của button đang active (màu nõn chuối), chỉ đổi font inactive
                if (btn != currentBtn)
                    btn.Font = sidebarFont;
            }
            // Cập nhật font active button nếu có
            if (currentBtn != null)
                currentBtn.Font = new Font("Segoe UI", sidebarFontSize + 0.25F, FontStyle.Bold, GraphicsUnit.Point);

            // Role text
            if (AppState.Instance.CurrentUser != null)
                lblUserRole.Text = GetRoleText(AppState.Instance.CurrentUser.RoleID);
        }
    }
}
