using EnvContract.BLL.Interfaces;
using EnvContract.Common;
using EnvContract.DTO.Entities;
using Guna.UI2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace EnvContract.GUI.Forms.Profile
{
    /// <summary>
    /// Popup "Hồ Sơ Cá Nhân" — 2 tab: Thông tin cá nhân & Đổi mật khẩu.
    /// Avatar căn giữa, thiết kế premium theo theme ECOVA.
    /// </summary>
    public class EditProfileForm : Form
    {
        // ── Palette ───────────────────────────────────────────────────────
        private static readonly Color DarkGreen    = Color.FromArgb(24,  46,  26);
        private static readonly Color MedGreen     = Color.FromArgb(49,  87,  44);
        private static readonly Color LightGreen   = Color.FromArgb(145, 185, 110);
        private static readonly Color FieldBg      = Color.FromArgb(240, 246, 232);
        private static readonly Color ROFieldBg    = Color.FromArgb(224, 230, 218);
        private static readonly Color BgColor      = Color.FromArgb(250, 253, 247);
        // Tab colors — active=DarkGreen (không lẫn body trắng), inactive=lắt
        private static readonly Color TabActiveBg  = Color.FromArgb(24,  46,  26);   // dark green = nổi bật khon body trắng
        private static readonly Color TabInactBg   = Color.FromArgb(195, 220, 170);  // xanh nhạt
        private static readonly Color TabHoverBg   = Color.FromArgb(155, 195, 125);  // hover rõ hơn inactive
        private static readonly Color TabActiveFg  = Color.White;
        private static readonly Color TabInactFg   = Color.FromArgb(30, 60, 28);

        // ── State ─────────────────────────────────────────────────────────
        private int    _activeTab = 0;
        private byte[] _newAvatarData;
        private readonly IUserBLL _userBLL;
        public event Action ProfileUpdated;

        // ── Controls — Tab 0 ─────────────────────────────────────────────
        private Guna2CirclePictureBox picAvatar;
        private Label                 lblAvatarName;
        private Label                 lblAvatarRole;
        private Guna2Button           btnChangeAvatar;
        private Guna2TextBox          txtFullName;
        private Guna2TextBox          txtDepartment;
        private Guna2TextBox          txtBirthYear;
        private Guna2TextBox          txtPhone;
        private Guna2TextBox          txtEmployeeId;
        private Guna2TextBox          txtEmail;
        private Guna2TextBox          txtAddress;
        private Guna2Button           btnUpdate;

        // ── Controls — Tab 1 ─────────────────────────────────────────────
        private Guna2TextBox          txtOldPassword;
        private Guna2TextBox          txtNewPassword;
        private Guna2TextBox          txtConfirmPassword;
        private Guna2Button           btnChangePassword;

        // ── Tab strip & panels ────────────────────────────────────────────
        private Panel    pnlTabStrip;
        private Panel    pnlTab0;
        private Panel    pnlTab1;
        private Label    lblHeaderTitle;
        private Label    lblTab0;
        private Label    lblTab1;
        private Panel    pnlContent0;
        private Panel    pnlContent1;

        // Labels / Buttons needed for localization
        private Label        lblLFullName, lblLDepartment, lblLBirthYear, lblLPhone, lblLEmployeeId, lblLEmail, lblLAddress;
        private Guna2Button  btnCancelInfo, btnCancelPwd;
        private Label        lblPwdTitle, lblPwdSub, lblPwdLOld, lblPwdLNew, lblPwdLCfm, lblPwdHint;

        // ─────────────────────────────────────────────────────────────────
        public EditProfileForm()
        {
            _userBLL = Program.ServiceProvider.GetRequiredService<IUserBLL>();
            BuildUI();
            ApplyLanguage();
            PopulateFields();
            if (Program.AppIcon != null) this.Icon = Program.AppIcon;

            LanguageManager.Instance.LanguageChanged += ApplyLanguage;
            this.FormClosed += (s, e) => LanguageManager.Instance.LanguageChanged -= ApplyLanguage;
        }

        // ═════════════════════════════════════════════════════════════════
        //  BUILD UI
        // ═════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            const int W = 700;
            const int H = 700;

            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.BackColor       = BgColor;
            this.Size            = new Size(W, H);
            this.KeyPreview      = true;
            this.KeyDown        += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };

            // Rounded corners — Region đủ tạo hiệu ứng, KHÔNG vẽ border thủ công thêm (sẽ tạo double border)
            this.Load += (s, e) =>
            {
                const int r = 8; // Giảm bo tròn để tránh vỡ hình ở góc
                var gp = new GraphicsPath();
                gp.AddArc(0,     0,     r*2, r*2, 180, 90);
                gp.AddArc(W-r*2, 0,     r*2, r*2, 270, 90);
                gp.AddArc(W-r*2, H-r*2, r*2, r*2,   0, 90);
                gp.AddArc(0,     H-r*2, r*2, r*2,  90, 90);
                gp.CloseFigure();
                this.Region = new Region(gp);
            };

            // Shadow
            var shadowForm = new Guna2ShadowForm(this) { TargetForm = this };

            // ── HEADER ────────────────────────────────────────────────────
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = MedGreen };

            lblHeaderTitle = new Label
            {
                Text      = "H\u1ed2 S\u01a0 C\u00c1 NH\u00c2N",
                Font      = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = Color.White,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(lblHeaderTitle);

            // Close button — đặt trong pnlHeader, giống EmployeeAddEditForm
            var btnClose = new Guna2Button
            {
                Text            = "✕",
                Size            = new Size(40, 40),
                Location        = new Point(W - 48, 9),
                FillColor       = Color.Transparent,
                ForeColor       = Color.White,
                Font            = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor          = Cursors.Hand,
                BorderThickness = 0
            };
            btnClose.Click += (s, e) => this.Close();

            pnlHeader.Controls.Add(btnClose);
            btnClose.BringToFront();


            // ── TAB STRIP — nền nhạt, 2 tab nổi bật ───────────────
            pnlTabStrip = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.FromArgb(225, 238, 210) };

            // Tab 0
            pnlTab0  = new Panel { Location = new Point(0, 0), Size = new Size(W/2, 46), Cursor = Cursors.Hand, BackColor = TabActiveBg };
            lblTab0  = new Label { Text = "Th\u00f4ng tin c\u00e1 nh\u00e2n", Dock = DockStyle.Fill,
                                   TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand,
                                   Font = new Font("Segoe UI", 10, FontStyle.Bold),
                                   ForeColor = TabActiveFg, BackColor = Color.Transparent };
            pnlTab0.Controls.Add(lblTab0);

            // Tab 1
            pnlTab1  = new Panel { Location = new Point(W/2, 0), Size = new Size(W/2, 46), Cursor = Cursors.Hand, BackColor = TabInactBg };
            lblTab1  = new Label { Text = "\u0110\u1ed5i m\u1eadt kh\u1ea9u", Dock = DockStyle.Fill,
                                   TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand,
                                   Font = new Font("Segoe UI", 10, FontStyle.Bold),
                                   ForeColor = TabInactFg, BackColor = Color.Transparent };
            pnlTab1.Controls.Add(lblTab1);

            // Wire hover + click for both tabs
            WireTab(pnlTab0, lblTab0, 0);
            WireTab(pnlTab1, lblTab1, 1);

            pnlTabStrip.Controls.AddRange(new Control[] { pnlTab0, pnlTab1 });

            // ── CONTENT PANELS ────────────────────────────────────────────
            var pnlBody = new Panel { Dock = DockStyle.Fill, BackColor = BgColor };

            pnlContent0 = new Panel { Dock = DockStyle.Fill, BackColor = BgColor, Visible = true };
            pnlContent1 = new Panel { Dock = DockStyle.Fill, BackColor = BgColor, Visible = false };

            BuildInfoTab(pnlContent0, W);
            BuildPasswordTab(pnlContent1, W);

            pnlBody.Controls.Add(pnlContent1);
            pnlBody.Controls.Add(pnlContent0);

            // ── ASSEMBLE ──────────────────────────────────────────────────
            this.Controls.Add(pnlBody);
            this.Controls.Add(pnlTabStrip);
            this.Controls.Add(pnlHeader);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Tab hover wiring (Panel không có HoverState → dùng events)
        // ─────────────────────────────────────────────────────────────────
        private void WireTab(Panel pnl, Label lbl, int idx)
        {
            EventHandler click = (s, e) => SetActiveTab(idx);
            pnl.Click += click;
            lbl.Click += click;

            EventHandler enter = (s, e) =>
            {
                if (_activeTab != idx)
                {
                    pnl.BackColor = TabHoverBg;
                    lbl.ForeColor = DarkGreen;
                }
            };
            EventHandler leave = (s, e) =>
            {
                if (_activeTab != idx)
                {
                    pnl.BackColor = TabInactBg;
                    lbl.ForeColor = TabInactFg;
                }
            };
            pnl.MouseEnter += enter; lbl.MouseEnter += enter;
            pnl.MouseLeave += leave; lbl.MouseLeave += leave;
        }

        private void SetActiveTab(int idx)
        {
            _activeTab = idx;

            pnlTab0.BackColor = idx == 0 ? TabActiveBg : TabInactBg;
            pnlTab1.BackColor = idx == 1 ? TabActiveBg : TabInactBg;
            lblTab0.ForeColor = idx == 0 ? TabActiveFg : TabInactFg;
            lblTab1.ForeColor = idx == 1 ? TabActiveFg : TabInactFg;

            pnlContent0.Visible = idx == 0;
            pnlContent1.Visible = idx == 1;
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAB 0 — THÔNG TIN CÁ NHÂN
        // ═════════════════════════════════════════════════════════════════
        private void BuildInfoTab(Panel parent, int W)
        {
            Label MakeLbl(string t) => new Label
            {
                Text      = t,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.Black,
                AutoSize  = true,
                BackColor = Color.Transparent
            };

            Guna2TextBox MakeTxt(string ph, bool ro = false) => new Guna2TextBox
            {
                PlaceholderText      = ph,
                PlaceholderForeColor = Color.FromArgb(155, 170, 148),
                Size                 = new Size(314, 40), // Tăng từ 274 lên 314 để căn lề phải đồng đều
                BorderRadius         = 9,
                BorderThickness      = 1,
                BorderColor          = Color.FromArgb(175, 205, 155),
                FillColor            = ro ? ROFieldBg : FieldBg,
                ForeColor            = DarkGreen,
                Font                 = new Font("Segoe UI", 10f),
                TextOffset           = new Point(8, 0),
                ReadOnly             = ro,
                Cursor               = ro ? Cursors.Default : Cursors.IBeam
            };

            // ── AVATAR (căn giữa) ─────────────────────────────────────────
            picAvatar          = new Guna2CirclePictureBox();
            picAvatar.Size     = new Size(96, 96);
            picAvatar.SizeMode = PictureBoxSizeMode.Zoom;
            picAvatar.FillColor = Color.FromArgb(170, 210, 145);
            picAvatar.Cursor   = Cursors.Hand;
            picAvatar.Location = new Point((W - 96) / 2, 16);
            picAvatar.Click   += PickAvatar;

            lblAvatarName = new Label
            {
                AutoSize  = false, Size = new Size(W - 40, 22),
                Location  = new Point(20, 118),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = DarkGreen, BackColor = Color.Transparent
            };
            lblAvatarRole = new Label
            {
                AutoSize  = false, Size = new Size(W - 40, 18),
                Location  = new Point(20, 140),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 135, 85), BackColor = Color.Transparent
            };

            btnChangeAvatar          = new Guna2Button();
            btnChangeAvatar.Text     = "Doi anh dai dien";
            btnChangeAvatar.Text     = "\u0110\u1ed5i \u1ea3nh \u0111\u1ea1i di\u1ec7n";
            btnChangeAvatar.Size     = new Size(156, 32);
            btnChangeAvatar.Location = new Point((W - 156) / 2, 163);
            btnChangeAvatar.BorderRadius    = 8;
            btnChangeAvatar.FillColor       = Color.FromArgb(218, 238, 198);
            btnChangeAvatar.ForeColor       = DarkGreen;
            btnChangeAvatar.Font            = new Font("Segoe UI", 9, FontStyle.Bold);
            btnChangeAvatar.BorderThickness = 1;
            btnChangeAvatar.BorderColor     = Color.FromArgb(155, 195, 120);
            btnChangeAvatar.Cursor          = Cursors.Hand;
            btnChangeAvatar.HoverState.FillColor = Color.FromArgb(185, 220, 155);
            btnChangeAvatar.Click          += PickAvatar;

            // Divider
            var sep = new Panel { Location = new Point(24, 204), Size = new Size(W - 48, 1), BackColor = Color.FromArgb(195, 215, 175) };

            // ── FIELDS (2 cột) ────────────────────────────────────────────
            int fY = 216;
            int x1 = 24, x2 = W/2 + 12;

            lblLFullName   = MakeLbl("H\u1ecd v\u00e0 t\u00ean");  lblLFullName.Location   = new Point(x1, fY);
            lblLDepartment = MakeLbl("Ph\u00f2ng ban"); lblLDepartment.Location = new Point(x2, fY);
            txtFullName   = MakeTxt("Nh\u1eadp h\u1ecd t\u00ean...");   txtFullName.Location   = new Point(x1, fY + 26);
            txtDepartment = MakeTxt("---", ro: true); txtDepartment.Location = new Point(x2, fY + 26);

            int fY2 = fY + 75;
            lblLBirthYear = MakeLbl("N\u0103m sinh");   lblLBirthYear.Location = new Point(x1, fY2);
            lblLPhone = MakeLbl("\u0110i\u1ec7n tho\u1ea1i"); lblLPhone.Location = new Point(x2, fY2);
            txtBirthYear = MakeTxt("VD: 1998"); txtBirthYear.Location = new Point(x1, fY2 + 26);
            txtPhone     = MakeTxt("S\u1ed1 \u0111i\u1ec7n tho\u1ea1i..."); txtPhone.Location = new Point(x2, fY2 + 26);

            int fY3 = fY2 + 75;
            lblLEmployeeId = MakeLbl("ID Nh\u00e2n vi\u00ean"); lblLEmployeeId.Location = new Point(x1, fY3);
            lblLEmail = MakeLbl("Email");          lblLEmail.Location = new Point(x2, fY3);
            txtEmployeeId = MakeTxt("ID...", ro: true); txtEmployeeId.Location = new Point(x1, fY3 + 26);
            txtEmail      = MakeTxt("Email...");  txtEmail.Location      = new Point(x2, fY3 + 26);

            int fY4 = fY3 + 75;
            lblLAddress = MakeLbl("\u0110\u1ecba ch\u1ec9"); lblLAddress.Location = new Point(x1, fY4);
            txtAddress          = MakeTxt("\u0110\u1ecba ch\u1ec9...");
            txtAddress.Size     = new Size(W - 48, 40);
            txtAddress.Location = new Point(x1, fY4 + 26);

            // Buttons row
            int fYBtn = fY4 + 72;

            btnCancelInfo          = new Guna2Button();
            btnCancelInfo.Text         = "H\u1ee7y";
            btnCancelInfo.Size         = new Size(110, 40);
            btnCancelInfo.Location     = new Point(x1, fYBtn);
            btnCancelInfo.BorderRadius = 9;
            btnCancelInfo.FillColor    = Color.FromArgb(220, 228, 214);
            btnCancelInfo.ForeColor    = Color.FromArgb(60, 80, 55);
            btnCancelInfo.Font         = new Font("Segoe UI", 10, FontStyle.Bold);
            btnCancelInfo.BorderThickness = 1;
            btnCancelInfo.BorderColor  = Color.FromArgb(170, 195, 150);
            btnCancelInfo.Cursor       = Cursors.Hand;
            btnCancelInfo.HoverState.FillColor = Color.FromArgb(195, 210, 182);
            btnCancelInfo.Click       += (s, e) => this.Close();

            btnUpdate          = new Guna2Button();
            btnUpdate.Text     = "C\u1eadp nh\u1eadt h\u1ed3 s\u01a1";
            btnUpdate.Size     = new Size(170, 40);
            btnUpdate.Location = new Point(W - 24 - 170, fYBtn);
            btnUpdate.BorderRadius    = 9;
            btnUpdate.FillColor       = MedGreen;
            btnUpdate.ForeColor       = Color.White;
            btnUpdate.Font            = new Font("Segoe UI", 10, FontStyle.Bold);
            btnUpdate.Cursor          = Cursors.Hand;
            btnUpdate.HoverState.FillColor = Color.FromArgb(65, 110, 55);
            btnUpdate.Click           += BtnUpdate_Click;

            parent.Controls.AddRange(new Control[]
            {
                picAvatar, lblAvatarName, lblAvatarRole, btnChangeAvatar, sep,
                lblLFullName, txtFullName, lblLDepartment, txtDepartment,
                lblLBirthYear, txtBirthYear, lblLPhone, txtPhone,
                lblLEmployeeId, txtEmployeeId, lblLEmail, txtEmail,
                lblLAddress, txtAddress,
                btnCancelInfo, btnUpdate
            });
        }

        // ═════════════════════════════════════════════════════════════════
        //  TAB 1 — ĐỔI MẬT KHẨU
        // ═════════════════════════════════════════════════════════════════
        private void BuildPasswordTab(Panel parent, int W)
        {
            Label MakeLbl(string t) => new Label
            {
                Text = t, Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.Black, AutoSize = true, BackColor = Color.Transparent
            };

            Guna2TextBox MakePwdTxt(string ph) => new Guna2TextBox
            {
                PlaceholderText      = ph,
                PlaceholderForeColor = Color.FromArgb(155, 170, 148),
                Size                 = new Size(380, 46),
                BorderRadius         = 10,
                BorderThickness      = 1,
                BorderColor          = Color.FromArgb(175, 205, 155),
                FillColor            = FieldBg,
                ForeColor            = DarkGreen,
                Font                 = new Font("Segoe UI", 10.5f),
                TextOffset           = new Point(10, 0),
                PasswordChar         = '\u25cf'
            };

            // Bỏ vòng tròn * — chỉ giữ tiêu đề và subtitle
            // (không thêm pnlIconBg)

            // Tiêu đề căn giữa, gần top như tab info
            lblPwdTitle = new Label
            {
                Text = "THAY \u0110\u1ed4I M\u1eacT KH\u1ea8U",
                Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = DarkGreen,
                AutoSize = false, Size = new Size(W, 52), Location = new Point(0, 28),
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
            };
            lblPwdSub = new Label
            {
                Text = "M\u1eadt kh\u1ea9u m\u1edbi ph\u1ea3i c\u00f3 \u00edt nh\u1ea5t 6 k\u00fd t\u1ef1",
                Font = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(120, 145, 110),
                AutoSize = false, Size = new Size(W, 20), Location = new Point(0, 80),
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
            };

            // Divider giống tab info
            var sepPwd = new Panel { Location = new Point(24, 112), Size = new Size(W - 48, 1), BackColor = Color.FromArgb(195, 215, 175) };

            // Fields — căn trái px1=24, full-width, khoảng cách đều giữa các nhóm
            int px1 = 24;
            int fW  = W - 48; // full-width cho mỗi field (1 cột)

            // Giảm khoảng cách giữa các hàng từ 118 xuống 90 để bớt trống trải
            const int fieldRowH = 90; 

            // Khoảng cách label→textbox = 26px (đồng bộ với tab thông tin cá nhân)
            const int lblOffset = 26;

            lblPwdLOld  = MakeLbl("M\u1eadt kh\u1ea9u hi\u1ec7n t\u1ea1i"); lblPwdLOld.Location  = new Point(px1, 132);
            txtOldPassword = MakePwdTxt("Nh\u1eadp m\u1eadt kh\u1ea9u \u0111ang d\u00f9ng...");
            txtOldPassword.Size     = new Size(fW, 40);
            txtOldPassword.Location = new Point(px1, 132 + lblOffset);

            lblPwdLNew  = MakeLbl("M\u1eadt kh\u1ea9u m\u1edbi"); lblPwdLNew.Location = new Point(px1, 132 + fieldRowH);
            txtNewPassword = MakePwdTxt("\u00cdt nh\u1ea5t 6 k\u00fd t\u1ef1...");
            txtNewPassword.Size     = new Size(fW, 40);
            txtNewPassword.Location = new Point(px1, 132 + fieldRowH + lblOffset);

            lblPwdLCfm  = MakeLbl("X\u00e1c nh\u1eadn m\u1eadt kh\u1ea9u m\u1edbi"); lblPwdLCfm.Location = new Point(px1, 132 + fieldRowH * 2);
            txtConfirmPassword = MakePwdTxt("Nh\u1eadp l\u1ea1i m\u1eadt kh\u1ea9u m\u1edbi...");
            txtConfirmPassword.Size     = new Size(fW, 40);
            txtConfirmPassword.Location = new Point(px1, 132 + fieldRowH * 2 + lblOffset);

            // Buttons — cùng hàng, cách txt cuối ~40px
            int txtCfmBottom = 132 + fieldRowH * 2 + lblOffset + 40; // bottom của txtConfirmPassword
            int btnY = txtCfmBottom + 40;
            btnCancelPwd          = new Guna2Button();
            btnCancelPwd.Text         = "H\u1ee7y";
            btnCancelPwd.Size         = new Size(110, 40);
            btnCancelPwd.Location     = new Point(px1, btnY);
            btnCancelPwd.BorderRadius = 9;
            btnCancelPwd.FillColor    = Color.FromArgb(220, 228, 214);
            btnCancelPwd.ForeColor    = Color.FromArgb(60, 80, 55);
            btnCancelPwd.Font         = new Font("Segoe UI", 10, FontStyle.Bold);
            btnCancelPwd.BorderThickness = 1;
            btnCancelPwd.BorderColor  = Color.FromArgb(170, 195, 150);
            btnCancelPwd.Cursor       = Cursors.Hand;
            btnCancelPwd.HoverState.FillColor = Color.FromArgb(195, 210, 182);
            btnCancelPwd.Click += (s, e) =>
            {
                txtOldPassword.Clear(); txtNewPassword.Clear(); txtConfirmPassword.Clear();
            };

            btnChangePassword          = new Guna2Button();
            btnChangePassword.Text     = "\u0110\u1ed5i m\u1eadt kh\u1ea9u";
            btnChangePassword.Size     = new Size(170, 40);
            btnChangePassword.Location = new Point(W - 24 - 170, btnY); // cùng Y với btnCancelPwd
            btnChangePassword.BorderRadius    = 9;
            btnChangePassword.FillColor       = MedGreen;
            btnChangePassword.ForeColor       = Color.White;
            btnChangePassword.Font            = new Font("Segoe UI", 10, FontStyle.Bold);
            btnChangePassword.Cursor          = Cursors.Hand;
            btnChangePassword.HoverState.FillColor = Color.FromArgb(65, 110, 55);
            btnChangePassword.Click          += BtnChangePassword_Click;

            lblPwdHint = new Label
            {
                Text      = "Sau khi \u0111\u1ed5i, b\u1ea1n s\u1ebd c\u1ea7n \u0111\u0103ng nh\u1eadp l\u1ea1i b\u1eb1ng m\u1eadt kh\u1ea9u m\u1edbi.",
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.FromArgb(145, 165, 135),
                AutoSize  = false, Size = new Size(W, 20), Location = new Point(0, btnY + 52),
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
            };

            parent.Controls.AddRange(new Control[]
            {
                lblPwdTitle, lblPwdSub, sepPwd,
                lblPwdLOld, txtOldPassword,
                lblPwdLNew, txtNewPassword,
                lblPwdLCfm, txtConfirmPassword,
                btnCancelPwd, btnChangePassword,
                lblPwdHint
            });
        }

        private void ApplyLanguage()
        {
            var LM = LanguageManager.Instance;

            lblHeaderTitle.Text = LM.Get("profile_title");
            lblTab0.Text        = LM.Get("profile_tab_info");
            lblTab1.Text        = LM.Get("profile_tab_pwd");

            // Tab 0
            btnChangeAvatar.Text = LM.Get("profile_btn_change_avatar");
            lblLFullName.Text    = LM.Get("profile_lbl_name");
            lblLDepartment.Text  = LM.Get("profile_lbl_dept");
            lblLBirthYear.Text   = LM.Get("profile_lbl_birth");
            lblLPhone.Text       = LM.Get("profile_lbl_phone");
            lblLEmployeeId.Text  = LM.Get("profile_lbl_id");
            lblLEmail.Text       = LM.Get("profile_lbl_email");
            lblLAddress.Text     = LM.Get("profile_lbl_addr");

            txtFullName.PlaceholderText  = LM.Get("profile_ph_name");
            txtPhone.PlaceholderText     = LM.Get("profile_ph_phone");
            txtEmail.PlaceholderText     = LM.Get("profile_ph_email");
            txtAddress.PlaceholderText   = LM.Get("profile_ph_addr");
            txtBirthYear.PlaceholderText = LM.Get("profile_ph_birth");

            btnCancelInfo.Text = LM.Get("cancel");
            btnUpdate.Text     = LM.Get("profile_btn_update");

            // Tab 1
            lblPwdTitle.Text   = LM.Get("profile_pwd_title");
            lblPwdSub.Text     = LM.Get("profile_pwd_sub");
            lblPwdLOld.Text    = LM.Get("profile_pwd_lbl_old");
            lblPwdLNew.Text    = LM.Get("profile_pwd_lbl_new");
            lblPwdLCfm.Text    = LM.Get("profile_pwd_lbl_cfm");
            lblPwdHint.Text    = LM.Get("profile_pwd_hint");

            txtOldPassword.PlaceholderText     = LM.Get("profile_pwd_ph_old");
            txtNewPassword.PlaceholderText     = LM.Get("profile_pwd_ph_new");
            txtConfirmPassword.PlaceholderText = LM.Get("profile_pwd_ph_cfm");
            btnCancelPwd.Text                  = LM.Get("cancel");
            btnChangePassword.Text             = LM.Get("profile_tab_pwd");

            // Update role-based labels if user loaded
            PopulateFields();
        }

        // ═════════════════════════════════════════════════════════════════
        //  POPULATE
        // ═════════════════════════════════════════════════════════════════
        private void PopulateFields()
        {
            var user = AppState.Instance.CurrentUser;
            if (user == null) return;

            var LM = LanguageManager.Instance;

            txtFullName.Text   = user.FullName ?? "";
            txtPhone.Text      = user.Phone    ?? "";
            txtEmail.Text      = user.Email    ?? "";
            txtAddress.Text    = user.Address  ?? "";
            txtEmployeeId.Text = user.UserID   ?? "";
            txtBirthYear.Text  = user.DateOfBirth.HasValue
                ? user.DateOfBirth.Value.Year.ToString() : "";

            string roleName = user.RoleID switch
            {
                "R01" => LM.Get("role_admin"),
                "R02" => LM.Get("role_director"),
                "R03" => LM.Get("role_sales"),
                "R04" => LM.Get("role_field"),
                "R05" => LM.Get("role_lab"),
                "R06" => LM.Get("role_planning"),
                "R07" => LM.Get("role_result"),
                _     => user.RoleID ?? "---"
            };
            txtDepartment.Text = roleName;
            lblAvatarName.Text = user.FullName ?? "";
            lblAvatarRole.Text = roleName;

            if (user.AvatarData != null && user.AvatarData.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(user.AvatarData);
                    picAvatar.Image = Image.FromStream(ms);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Profile] Avatar display error: {ex.Message}"); }
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  AVATAR PICKER
        // ═════════════════════════════════════════════════════════════════
        private void PickAvatar(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title  = "Ch\u1ecdn \u1ea3nh \u0111\u1ea1i di\u1ec7n",
                Filter = "\u1ea2nh|*.jpg;*.jpeg;*.png;*.bmp"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;
            try
            {
                var bmp = new Bitmap(200, 200, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(Image.FromFile(ofd.FileName), 0, 0, 200, 200);
                }
                picAvatar.Image = bmp;
                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Jpeg);
                _newAvatarData = ms.ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.Instance.Get("faceid_cam_err") + ": " + ex.Message, LanguageManager.Instance.Get("error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  SAVE PROFILE
        // ═════════════════════════════════════════════════════════════════
        private async void BtnUpdate_Click(object sender, EventArgs e)
        {
            var user = AppState.Instance.CurrentUser;
            if (user == null) return;

            if (string.IsNullOrWhiteSpace(txtFullName.Text))
            {
                MessageBox.Show(LanguageManager.Instance.Get("profile_err_name_req"), LanguageManager.Instance.Get("info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtFullName.Focus(); return;
            }

            int? birthYear = null;
            if (!string.IsNullOrWhiteSpace(txtBirthYear.Text))
            {
                if (int.TryParse(txtBirthYear.Text.Trim(), out int by) && by >= 1900 && by <= DateTime.Now.Year)
                    birthYear = by;
                else
                {
                    MessageBox.Show(LanguageManager.Instance.Get("profile_err_birth_invalid"), LanguageManager.Instance.Get("info"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtBirthYear.Focus(); return;
                }
            }

            user.FullName    = txtFullName.Text.Trim();
            user.Phone       = txtPhone.Text.Trim();
            user.Email       = txtEmail.Text.Trim();
            user.Address     = txtAddress.Text.Trim();
            user.DateOfBirth = birthYear.HasValue ? new DateTime(birthYear.Value, 1, 1) : (DateTime?)null;

            try
            {
                btnUpdate.Enabled = false;
                btnUpdate.Text    = LanguageManager.Instance.Get("profile_msg_saving");

                await _userBLL.UpdateProfileAsync(user);

                if (_newAvatarData != null)
                    await _userBLL.UpdateAvatarAsync(user.UserID, _newAvatarData);

                AppState.Instance.CurrentUser = user;
                lblAvatarName.Text = user.FullName;

                MessageBox.Show(LanguageManager.Instance.Get("profile_msg_success"), LanguageManager.Instance.Get("info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                ProfileUpdated?.Invoke();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.Instance.Get("error") + ": " + ex.Message, LanguageManager.Instance.Get("error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnUpdate.Enabled = true;
                btnUpdate.Text    = LanguageManager.Instance.Get("profile_btn_update");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  CHANGE PASSWORD
        // ═════════════════════════════════════════════════════════════════
        private async void BtnChangePassword_Click(object sender, EventArgs e)
        {
            string oldPwd     = txtOldPassword.Text;
            string newPwd     = txtNewPassword.Text;
            string confirmPwd = txtConfirmPassword.Text;

            if (string.IsNullOrWhiteSpace(oldPwd))
            {
                MessageBox.Show(LanguageManager.Instance.Get("login_faceid_pw_ph"), LanguageManager.Instance.Get("info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOldPassword.Focus(); return;
            }
            if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 6)
            {
                MessageBox.Show(LanguageManager.Instance.Get("profile_pwd_sub"), LanguageManager.Instance.Get("info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPassword.Focus(); return;
            }
            if (newPwd != confirmPwd)
            {
                MessageBox.Show(LanguageManager.Instance.Get("forget_err_pw_mismatch"), LanguageManager.Instance.Get("info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtConfirmPassword.Focus(); return;
            }

            var user = AppState.Instance.CurrentUser;
            bool ok  = await _userBLL.LoginAsync(user.Username, oldPwd);
            AppState.Instance.CurrentUser = user; // Restore sau LoginAsync

            if (!ok)
            {
                MessageBox.Show(LanguageManager.Instance.Get("login_fail"), LanguageManager.Instance.Get("info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOldPassword.Clear();
                txtOldPassword.Focus();
                return;
            }

            try
            {
                btnChangePassword.Enabled = false;
                btnChangePassword.Text    = LanguageManager.Instance.Get("profile_msg_saving");

                await _userBLL.ResetPasswordAsync(user.Username, newPwd);

                txtOldPassword.Clear();
                txtNewPassword.Clear();
                txtConfirmPassword.Clear();

                MessageBox.Show(LanguageManager.Instance.Get("profile_pwd_success"), LanguageManager.Instance.Get("info"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(LanguageManager.Instance.Get("error") + ": " + ex.Message, LanguageManager.Instance.Get("error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnChangePassword.Enabled = true;
                btnChangePassword.Text    = LanguageManager.Instance.Get("profile_tab_pwd");
            }
        }

        // ── Graphics helper ────────────────────────────────────────────────
        private static void DrawRoundRect(Graphics g, Pen pen, Rectangle r, int radius)
        {
            int d = radius * 2;
            using var path = new GraphicsPath();
            path.AddArc(r.X,         r.Y,          d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            path.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }
    }
}
