using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using EnvContract.Common;

namespace EnvContract.GUI.Forms.Auth
{
    // ─────────────────────────────────────────────────────────────────────────
    // Login.ForgotPassword.cs — Partial class quản lý luồng quên mật khẩu:
    //   Bước 1: Nhập email → gửi OTP qua SMTP
    //   Bước 2: Nhập OTP → xác minh
    //   Bước 3: Nhập mật khẩu mới → lưu DB
    // ─────────────────────────────────────────────────────────────────────────
    public partial class Login
    {
        // ── State ─────────────────────────────────────────────────────────────
        private int    _forgotStep      = 1;
        private string _sentOtp         = string.Empty;
        private Timer  _countdownTimer;
        private int    _remainingSeconds = 0;
        private string _countdownText   = string.Empty;
        private System.Drawing.Rectangle _resendRect = System.Drawing.Rectangle.Empty;

        // ── UI text (vẽ bằng Paint vì panel là glassmorphism) ────────────────
        private string _forgotTitleText    = "";
        private string _forgotSubtitleText = "";
        private string _forgotStatusMsg    = string.Empty;
        private Color  _forgotStatusColor  = Color.Transparent;

        // ── UI Controls ─────────────────────────────────────────────────────
        private Guna2Panel   pnlForgot;
        private Guna2TextBox txtForgotEmail;
        private Guna2TextBox txtForgotOtp;
        private Guna2TextBox txtNewPassword;
        private Guna2TextBox txtConfirmPassword;
        private Guna2Button  btnForgotAction;
        private PictureBox   pbEyeNewPw;
        private PictureBox   pbEyeConfirmPw;
        private bool         _newPwVisible     = false;
        private bool         _confirmPwVisible = false;

        // ─────────────────────────────────────────────────────────────────────
        // Entry point — gọi từ Login.cs khi click "Quên mật khẩu"
        // ─────────────────────────────────────────────────────────────────────
        private void ShowForgotCard()
        {
            if (pnlForgot == null) CreateForgotPanel();

            _forgotStep        = 1;
            var lm = LanguageManager.Instance;
            _forgotTitleText   = lm.Get("forget_title");
            _forgotSubtitleText = lm.Get("forget_sub");
            txtForgotEmail.Text    = ""; txtForgotEmail.Enabled = true; txtForgotEmail.Visible = true;
            txtForgotOtp.Text      = ""; txtForgotOtp.Enabled   = false;
            txtForgotOtp.FillColor = Color.FromArgb(40, 65, 40); txtForgotOtp.Visible = true;
            txtNewPassword.Text    = ""; txtNewPassword.Visible      = false;
            txtConfirmPassword.Text = ""; txtConfirmPassword.Visible = false;
            btnForgotAction.Text   = lm.Get("forget_continue");
            if (pbEyeNewPw != null)     pbEyeNewPw.Visible = false;
            if (pbEyeConfirmPw != null) pbEyeConfirmPw.Visible = false;
            SetForgotStatus("", Color.Transparent);

            ApplyLanguageForgot(); // Cập nhật toàn bộ nhãn và nút theo ngôn ngữ hiện tại

            pnlForgot.Visible = true;
            pnlForgot.BringToFront();
            btnBackToHome.Visible = false;
            AppLogger.Info("ForgotPassword: Mở màn hình quên mật khẩu");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Countdown timer (OTP 5 phút)
        // ─────────────────────────────────────────────────────────────────────
        private void StartForgotCountdown()
        {
            StopForgotCountdown();
            _remainingSeconds = 5 * 60;
            UpdateForgotCountdown();
            _countdownTimer = new Timer { Interval = 1000 };
            _countdownTimer.Tick += (s, e) =>
            {
                _remainingSeconds--;
                UpdateForgotCountdown();
                pnlForgot?.Invalidate();
                if (_remainingSeconds <= 0) StopForgotCountdown();
            };
            _countdownTimer.Start();
        }

        private void StopForgotCountdown()
        {
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();
            _countdownTimer   = null;
            _countdownText    = string.Empty;
            _remainingSeconds = 0;
        }

        private void UpdateForgotCountdown()
        {
            int m = _remainingSeconds / 60, s = _remainingSeconds % 60;
            _countdownText = _remainingSeconds > 0
                ? string.Format(LanguageManager.Instance.Get("forget_otp_valid"), m.ToString("D2"), s.ToString("D2"))
                : LanguageManager.Instance.Get("forget_otp_expired");
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI Panel builder
        // ─────────────────────────────────────────────────────────────────────
        private void CreateForgotPanel()
        {
            pnlForgot = new Guna2Panel
            {
                Size         = new Size(500, 530),
                FillColor    = Color.FromArgb(210, 206, 219, 192),
                BorderRadius = 25,
                BackColor    = Color.Transparent,
                ShadowDecoration = { Enabled = false }
            };
            this.Controls.Add(pnlForgot);
            pnlForgot.Left = (this.ClientSize.Width  - pnlForgot.Width)  / 2;
            pnlForgot.Top  = (this.ClientSize.Height - pnlForgot.Height) / 2;

            // ── Helper tạo textbox ────────────────────────────────────────────
            Guna2TextBox MakeTxt(string placeholder, int y, bool disabled = false)
            {
                var t = new Guna2TextBox
                {
                    PlaceholderText      = placeholder,
                    PlaceholderForeColor = Color.FromArgb(160, 180, 150),
                    BorderRadius         = 15,
                    Size                 = new Size(390, 52),
                    Location             = new Point(50, y),
                    Font                 = new Font("Segoe UI", 11),
                    FillColor            = disabled ? Color.FromArgb(40, 65, 40) : Color.FromArgb(64, 88, 60),
                    ForeColor            = Color.White,
                    BorderThickness      = 0,
                    BackColor            = Color.Transparent,
                    Enabled              = !disabled
                };
                pnlForgot.Controls.Add(t);
                return t;
            }

            var lm = LanguageManager.Instance;
            txtForgotEmail      = MakeTxt(lm.Get("forget_email_ph"), 195);
            txtForgotOtp        = MakeTxt(lm.Get("forget_otp_ph"), 320, disabled: true);
            txtNewPassword      = MakeTxt(lm.Get("forget_new_pw_ph"), 175);
            txtNewPassword.PasswordChar      = '●';
            txtNewPassword.Visible           = false;
            txtConfirmPassword  = MakeTxt(lm.Get("forget_confirm_pw_ph"), 278);
            txtConfirmPassword.PasswordChar  = '●';
            txtConfirmPassword.Visible       = false;

            // ── Eye toggle cho txtNewPassword ──────────────────────────────
            string eyeOpenPath   = System.IO.Path.Combine(GetAssetsPath(), "images", "eye-solid-full.png");
            string eyeClosedPath = System.IO.Path.Combine(GetAssetsPath(), "images", "eye-slash-solid (1).png");

            pbEyeNewPw = new PictureBox
            {
                Size      = new Size(24, 24),
                Location  = new Point(txtNewPassword.Left + txtNewPassword.Width - 38,
                                      txtNewPassword.Top  + (txtNewPassword.Height - 24) / 2),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(64, 88, 60),
                Cursor    = Cursors.Hand,
                Visible   = false
            };
            if (System.IO.File.Exists(eyeClosedPath))
                pbEyeNewPw.Image = TintImage(Image.FromFile(eyeClosedPath), Color.FromArgb(180, 200, 170));
            pbEyeNewPw.MouseEnter += (s, ev) =>
            {
                string p = _newPwVisible ? eyeOpenPath : eyeClosedPath;
                if (System.IO.File.Exists(p))
                    pbEyeNewPw.Image = TintImage(Image.FromFile(p), Color.White);
                pbEyeNewPw.BackColor = Color.FromArgb(80, 108, 74);
            };
            pbEyeNewPw.MouseLeave += (s, ev) =>
            {
                string p = _newPwVisible ? eyeOpenPath : eyeClosedPath;
                if (System.IO.File.Exists(p))
                    pbEyeNewPw.Image = TintImage(Image.FromFile(p), Color.FromArgb(180, 200, 170));
                pbEyeNewPw.BackColor = Color.FromArgb(64, 88, 60);
            };
            pbEyeNewPw.Click += (s, ev) =>
            {
                _newPwVisible = !_newPwVisible;
                txtNewPassword.PasswordChar = _newPwVisible ? '\0' : '●';
                string p = _newPwVisible ? eyeOpenPath : eyeClosedPath;
                if (System.IO.File.Exists(p))
                    pbEyeNewPw.Image = TintImage(Image.FromFile(p), Color.White);
            };
            pnlForgot.Controls.Add(pbEyeNewPw);
            pbEyeNewPw.BringToFront();

            // ── Eye toggle cho txtConfirmPassword ───────────────────────
            pbEyeConfirmPw = new PictureBox
            {
                Size      = new Size(24, 24),
                Location  = new Point(txtConfirmPassword.Left + txtConfirmPassword.Width - 38,
                                      txtConfirmPassword.Top  + (txtConfirmPassword.Height - 24) / 2),
                SizeMode  = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(64, 88, 60),
                Cursor    = Cursors.Hand,
                Visible   = false
            };
            if (System.IO.File.Exists(eyeClosedPath))
                pbEyeConfirmPw.Image = TintImage(Image.FromFile(eyeClosedPath), Color.FromArgb(180, 200, 170));
            pbEyeConfirmPw.MouseEnter += (s, ev) =>
            {
                string p = _confirmPwVisible ? eyeOpenPath : eyeClosedPath;
                if (System.IO.File.Exists(p))
                    pbEyeConfirmPw.Image = TintImage(Image.FromFile(p), Color.White);
                pbEyeConfirmPw.BackColor = Color.FromArgb(80, 108, 74);
            };
            pbEyeConfirmPw.MouseLeave += (s, ev) =>
            {
                string p = _confirmPwVisible ? eyeOpenPath : eyeClosedPath;
                if (System.IO.File.Exists(p))
                    pbEyeConfirmPw.Image = TintImage(Image.FromFile(p), Color.FromArgb(180, 200, 170));
                pbEyeConfirmPw.BackColor = Color.FromArgb(64, 88, 60);
            };
            pbEyeConfirmPw.Click += (s, ev) =>
            {
                _confirmPwVisible = !_confirmPwVisible;
                txtConfirmPassword.PasswordChar = _confirmPwVisible ? '\0' : '●';
                string p = _confirmPwVisible ? eyeOpenPath : eyeClosedPath;
                if (System.IO.File.Exists(p))
                    pbEyeConfirmPw.Image = TintImage(Image.FromFile(p), Color.White);
            };
            pnlForgot.Controls.Add(pbEyeConfirmPw);
            pbEyeConfirmPw.BringToFront();

            // ── Nút Quay Lại ──────────────────────────────────────────────────
            var btnBackF = new Guna2Button
            {
                Name         = "btnForgotBack",
                Text         = lm.Get("forget_back"),
                Size         = new Size(188, 55),
                Location     = new Point(50, 430),
                BorderRadius = 15,
                FillColor    = Color.FromArgb(145, 175, 100),
                Font         = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor    = Color.FromArgb(24, 46, 26),
                HoverState   = { FillColor = Color.FromArgb(130, 160, 80) },
                Cursor       = Cursors.Hand,
                BackColor    = Color.Transparent
            };
            btnBackF.Click += async (s, e) =>
            {
                StopForgotCountdown();
                await Helpers.FormTransitionHelper.PanelSwitchAsync(this, () =>
                {
                    pnlForgot.Visible = false;
                    ShowLoginCard();
                });
            };
            pnlForgot.Controls.Add(btnBackF);

            // ── Nút hành động chính ───────────────────────────────────────────
            btnForgotAction = new Guna2Button
            {
                Text         = "Tiếp tục",
                Size         = new Size(188, 55),
                Location     = new Point(262, 430),
                BorderRadius = 15,
                FillColor    = Color.FromArgb(145, 175, 100),
                Font         = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor    = Color.FromArgb(24, 46, 26),
                HoverState   = { FillColor = Color.FromArgb(130, 160, 80) },
                Cursor       = Cursors.Hand,
                BackColor    = Color.Transparent
            };
            btnForgotAction.Click += BtnForgotAction_Click;
            pnlForgot.Controls.Add(btnForgotAction);

            // ── Click "Gửi lại OTP" (vùng clickable trên Paint) ──────────────
            pnlForgot.MouseClick += (s, e) =>
            {
                if (_forgotStep == 2 && _resendRect != System.Drawing.Rectangle.Empty
                    && _resendRect.Contains(e.Location))
                {
                    _forgotStep = 1;
                    BtnForgotAction_Click(s, EventArgs.Empty);
                }
            };
            pnlForgot.MouseMove += (s, e) =>
            {
                bool over = _forgotStep == 2 && _resendRect != System.Drawing.Rectangle.Empty
                            && _resendRect.Contains(e.Location);
                pnlForgot.Cursor = over ? Cursors.Hand : Cursors.Default;
            };

            // ── Paint handler (glassmorphism custom draw) ─────────────────────
            pnlForgot.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                using (Font fT = new Font("Segoe UI", 26, FontStyle.Bold))
                using (Brush bT = new SolidBrush(Color.FromArgb(24, 46, 26)))
                {
                    var sz = g.MeasureString(_forgotTitleText, fT);
                    g.DrawString(_forgotTitleText, fT, bT, (pnlForgot.Width - sz.Width) / 2, 40);
                }
                using (Font fS = new Font("Segoe UI", 11, FontStyle.Italic))
                using (Brush bS = new SolidBrush(Color.FromArgb(40, 60, 40)))
                {
                    var sz = g.MeasureString(_forgotSubtitleText, fS);
                    g.DrawString(_forgotSubtitleText, fS, bS, (pnlForgot.Width - sz.Width) / 2, 100);
                }

                if (_forgotStep <= 2)
                {
                    var LM = LanguageManager.Instance;
                    using (Font fL = new Font("Segoe UI", 12, FontStyle.Bold))
                    using (Brush bL = new SolidBrush(Color.FromArgb(24, 46, 26)))
                    {
                        g.DrawString(LM.Get("forget_email_label"),  fL, bL, 50, 165);
                        g.DrawString(LM.Get("forget_otp_label"),   fL, bL, 50, 290);
                    }
                    if (!string.IsNullOrEmpty(_countdownText))
                    {
                        using Font fCd = new Font("Segoe UI", 9);
                        using Brush bCd = new SolidBrush(Color.FromArgb(80, 100, 70));
                        float w = g.MeasureString(LM.Get("forget_otp_label"), new Font("Segoe UI", 12, FontStyle.Bold)).Width;
                        g.DrawString(_countdownText, fCd, bCd, 50 + w + 8, 296);
                    }
                    using (Font fR = new Font("Segoe UI", 9, FontStyle.Underline))
                    using (Brush bR = new SolidBrush(Color.FromArgb(74, 92, 72)))
                    {
                        string resendTxt = LM.Get("forget_resend");
                        g.DrawString(resendTxt, fR, bR, 50, 382);
                        var sz = g.MeasureString(resendTxt, fR);
                        _resendRect = _forgotStep == 2
                            ? new System.Drawing.Rectangle(50, 382, (int)sz.Width, 18)
                            : System.Drawing.Rectangle.Empty;
                    }
                }
                else
                {
                    var LM = LanguageManager.Instance;
                    using (Font fL = new Font("Segoe UI", 12, FontStyle.Bold))
                    using (Brush bL = new SolidBrush(Color.FromArgb(24, 46, 26)))
                    {
                        g.DrawString(LM.Get("forget_new_pw_label"),      fL, bL, 50, 148);
                        g.DrawString(LM.Get("forget_confirm_pw_label"),  fL, bL, 50, 251);
                    }
                }

                if (!string.IsNullOrEmpty(_forgotStatusMsg))
                {
                    using Font fErr = new Font("Segoe UI", 10, FontStyle.Italic);
                    using Brush bErr = new SolidBrush(_forgotStatusColor);
                    var sz = g.MeasureString(_forgotStatusMsg, fErr);
                    int statusY = _forgotStep <= 2 ? 400 : 345;
                    g.DrawString(_forgotStatusMsg, fErr, bErr, (pnlForgot.Width - sz.Width) / 2, statusY);
                }

                if (_loginCardIconImg != null)        g.DrawImage(_loginCardIconImg, 15, 488, 35, 35);
                if (_loginCardTenPhanMemImg != null)  g.DrawImage(_loginCardTenPhanMemImg, 55, 488, 100, 35);
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Main action handler (3 bước)
        // ─────────────────────────────────────────────────────────────────────
        private async void BtnForgotAction_Click(object sender, EventArgs e)
        {
            var LM = LanguageManager.Instance;
            SetForgotStatus(LM.Get("forget_processing"), Color.FromArgb(0, 100, 200));
            btnForgotAction.Enabled = false;

            try
            {
                // ── Bước 1: Gửi OTP ──────────────────────────────────────────
                if (_forgotStep == 1)
                {
                    if (string.IsNullOrWhiteSpace(txtForgotEmail.Text))
                        throw new Exception(LM.Get("forget_err_email_empty"));

                    string email = txtForgotEmail.Text.Trim();

                    // ── Validate email tồn tại trước khi gửi OTP ─────────────
                    AppLogger.Info($"ForgotPassword: Kiểm tra email '{email}' trong DB");
                    bool emailExists = await _userBLL.CheckEmailExistsAsync(email);
                    if (!emailExists)
                    {
                        AppLogger.Warning($"ForgotPassword: Email '{email}' không tồn tại trong hệ thống");
                        throw new Exception(LM.Get("forget_err_email_not_exist"));
                    }

                    // Sinh OTP 6 chữ số dùng CSPRNG (an toàn hơn new Random)
                    int otpNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
                    _sentOtp = otpNumber.ToString();

                    AppLogger.Info($"ForgotPassword: Gửi OTP đến email '{email}'");

                    string subject = "[ECOVA] Mã xác minh đổi mật khẩu";
                    string body    = BuildOtpEmailBody(_sentOtp);

                    bool sent = await EnvContract.Common.Helpers.EmailSmtpHelper.SendEmailAsync(email, subject, body);
                    if (sent)
                    {
                        AppLogger.Info("ForgotPassword: Gửi OTP thành công");
                        _forgotStep = 2;
                        txtForgotEmail.Tag     = email; // lưu email để bước 3 dùng
                        txtForgotEmail.Enabled = false;
                        txtForgotOtp.Enabled   = true;
                        txtForgotOtp.FillColor = Color.FromArgb(64, 88, 60);
                        btnForgotAction.Text   = LM.Get("forget_confirm");
                        SetForgotStatus("", Color.Transparent);
                        StartForgotCountdown();
                        pnlForgot.Invalidate();
                    }
                    else throw new Exception(LM.Get("forget_err_email_send"));
                }
                // ── Bước 2: Xác minh OTP ─────────────────────────────────────
                else if (_forgotStep == 2)
                {
                    if (string.IsNullOrWhiteSpace(txtForgotOtp.Text))
                        throw new Exception(LM.Get("forget_err_otp_empty"));
                    if (txtForgotOtp.Text.Trim() != _sentOtp)
                    {
                        AppLogger.Warning("ForgotPassword: OTP không hợp lệ");
                        throw new Exception(LM.Get("forget_err_otp_invalid"));
                    }

                    AppLogger.Info("ForgotPassword: OTP hợp lệ → chuyển sang đổi mật khẩu");
                    StopForgotCountdown();
                    _forgotStep = 3;
                    txtForgotEmail.Visible     = false;
                    txtForgotOtp.Visible       = false;
                    txtNewPassword.Visible     = true;
                    txtConfirmPassword.Visible = true;
                    // Reset và hiện eye icons
                    _newPwVisible = false;
                    _confirmPwVisible = false;
                    txtNewPassword.PasswordChar     = '●';
                    txtConfirmPassword.PasswordChar = '●';
                    if (pbEyeNewPw    != null) pbEyeNewPw.Visible    = true;
                    if (pbEyeConfirmPw != null) pbEyeConfirmPw.Visible = true;
                    _forgotTitleText    = LM.Get("forget_title");
                    _forgotSubtitleText = LM.Get("forget_sub");
                    btnForgotAction.Text = LM.Get("msg_save");
                    SetForgotStatus(LM.Get("forget_step2_success"), Color.Green);
                }
                // ── Bước 3: Lưu mật khẩu mới ─────────────────────────────────
                else if (_forgotStep == 3)
                {
                    if (string.IsNullOrWhiteSpace(txtNewPassword.Text) || txtNewPassword.Text.Length < 6)
                        throw new Exception(LM.Get("forget_err_pw_min"));
                    if (txtNewPassword.Text != txtConfirmPassword.Text)
                        throw new Exception(LM.Get("forget_err_pw_mismatch"));

                    // Lấy email đã lưu từ bước 1 (Tag của txtForgotEmail)
                    string email = txtForgotEmail.Tag as string ?? txtForgotEmail.Text.Trim();
                    if (string.IsNullOrEmpty(email))
                        throw new Exception(LM.Get("forget_err_session"));

                    await _userBLL.ResetPasswordByEmailAsync(email, txtNewPassword.Text);

                    AppLogger.Info($"ForgotPassword: Đổi mật khẩu thành công cho email '{email}'");

                    SetForgotStatus(LM.Get("forget_success"), Color.Green);
                    await Task.Delay(1500);

                    StopForgotCountdown();
                    await Helpers.FormTransitionHelper.PanelSwitchAsync(this, () =>
                    {
                        pnlForgot.Visible = false;
                        ShowLoginCard();
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"ForgotPassword: {ex.Message}");
                SetForgotStatus(ex.Message, Color.Red);
            }
            finally
            {
                btnForgotAction.Enabled = true;
            }
        }

        private void ApplyLanguageForgot()
        {
            var LM = LanguageManager.Instance;
            if (pnlForgot == null) return;

            if (_forgotStep == 3)
            {
                _forgotTitleText = LM.Get("forget_title");
                _forgotSubtitleText = LM.Get("forget_sub");
                btnForgotAction.Text = LM.Get("msg_save");
            }
            else if (_forgotStep == 2)
            {
                _forgotTitleText = LM.Get("forget_title");
                _forgotSubtitleText = LM.Get("forget_sub");
                btnForgotAction.Text = LM.Get("forget_confirm");
            }
            else
            {
                _forgotTitleText = LM.Get("forget_title");
                _forgotSubtitleText = LM.Get("forget_sub");
                btnForgotAction.Text = LM.Get("forget_continue");
            }

            if (txtForgotEmail != null)     txtForgotEmail.PlaceholderText = LM.Get("forget_email_ph");
            if (txtForgotOtp != null)       txtForgotOtp.PlaceholderText = LM.Get("forget_otp_ph");
            if (txtNewPassword != null)     txtNewPassword.PlaceholderText = LM.Get("forget_new_pw_ph");
            if (txtConfirmPassword != null)  txtConfirmPassword.PlaceholderText = LM.Get("forget_confirm_pw_ph");
            
            // Tìm nút Quay lại bằng Name
            var btnBack = pnlForgot.Controls.Find("btnForgotBack", true).FirstOrDefault() as Guna2Button;
            if (btnBack != null) btnBack.Text = LM.Get("forget_back");

            pnlForgot.Invalidate();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────
        private void SetForgotStatus(string msg, Color clr)
        {
            _forgotStatusMsg   = msg;
            _forgotStatusColor = clr;
            pnlForgot?.Invalidate();
        }

        /// <summary>
        /// Template HTML chuyên nghiệp cho email OTP đổi mật khẩu.
        /// </summary>
        private static string BuildOtpEmailBody(string otp)
        {
            return $@"<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='UTF-8'><meta name='viewport' content='width=device-width,initial-scale=1'></head>
<body style='margin:0;padding:0;background:#f4f6f4;font-family:Segoe UI,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f6f4;padding:32px 0;'>
    <tr><td align='center'>
      <table width='520' cellpadding='0' cellspacing='0'
             style='background:#ffffff;border-radius:12px;border:1px solid #dde8d9;'>
        <!-- Header -->
        <tr>
          <td style='background:#31572C;border-radius:12px 12px 0 0;padding:24px;text-align:center;'>
            <h1 style='margin:0;color:#ECF39E;font-size:22px;letter-spacing:2px;'>ECOVA</h1>
            <p style='margin:4px 0 0;color:#c8e6a0;font-size:12px;'>Hệ thống quản lý quan trắc môi trường</p>
          </td>
        </tr>
        <!-- Body -->
        <tr>
          <td style='padding:32px 36px;'>
            <p style='margin:0 0 16px;font-size:15px;color:#333;'>Xin chào,</p>
            <p style='margin:0 0 24px;font-size:15px;color:#333;'>
              Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản ECOVA của bạn.
              Vui lòng sử dụng mã xác minh (OTP) dưới đây:
            </p>
            <!-- OTP Box -->
            <table width='100%' cellpadding='0' cellspacing='0' style='margin:0 0 24px;'>
              <tr><td align='center'>
                <div style='display:inline-block;background:#f0f7ec;border:2px solid #31572C;
                            border-radius:10px;padding:18px 40px;'>
                  <span style='font-size:36px;font-weight:bold;color:#31572C;
                               letter-spacing:8px;font-family:Courier New,monospace;'>{otp}</span>
                </div>
              </td></tr>
            </table>
            <p style='margin:0 0 8px;font-size:13px;color:#555;'>
              ⏱️ Mã có hiệu lực trong <strong>5 phút</strong>.
            </p>
            <p style='margin:0 0 24px;font-size:13px;color:#d32f2f;'>
              ⚠️ Không chia sẻ mã này với bất kỳ ai, kể cả nhân viên ECOVA.
            </p>
            <p style='margin:0;font-size:13px;color:#777;'>
              Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này —
              tài khoản của bạn vẫn an toàn.
            </p>
          </td>
        </tr>
        <!-- Footer -->
        <tr>
          <td style='background:#f8f9f6;border-radius:0 0 12px 12px;padding:16px 36px;
                     border-top:1px solid #eee;text-align:center;'>
            <p style='margin:0;font-size:11px;color:#aaa;'>
              Email này được gửi tự động từ hệ thống ECOVA. Vui lòng không trả lời.
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
        }
    }
}
