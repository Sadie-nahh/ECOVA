using EnvContract.Common.Helpers;
using Guna.UI2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnvContract.GUI.Forms.Admin
{
    /// <summary>
    /// Form cấu hình SMTP cho máy hiện tại.
    /// Tự động hiện khi DPAPI không giải mã được mật khẩu từ máy khác.
    /// Sau khi lưu, mã hóa lại bằng DPAPI của máy/user hiện tại → ghi appsettings.json.
    /// </summary>
    public class SmtpSetupForm : Form
    {
        // ── Palette ───────────────────────────────────────────────────────
        private static readonly Color DarkGreen  = Color.FromArgb(24,  46,  26);
        private static readonly Color MedGreen   = Color.FromArgb(49,  87,  44);
        private static readonly Color LightGreen = Color.FromArgb(145, 185, 110);
        private static readonly Color FieldBg    = Color.FromArgb(240, 246, 232);
        private static readonly Color BgColor    = Color.FromArgb(250, 253, 247);

        // ── Controls ──────────────────────────────────────────────────────
        private Guna2TextBox _txtServer;
        private Guna2TextBox _txtPort;
        private Guna2TextBox _txtUsername;
        private Guna2TextBox _txtPassword;
        private Guna2TextBox _txtTestEmail;
        private Guna2Button  _btnTest;
        private Guna2Button  _btnSave;
        private Guna2Button  _btnCancel;
        private Label        _lblStatus;

        private bool _isDpapiFailed;

        public SmtpSetupForm(bool isDpapiFailed = false)
        {
            _isDpapiFailed = isDpapiFailed;
            BuildUI();
            PreFillFromConfig();
            if (Program.AppIcon != null) this.Icon = Program.AppIcon;
        }

        // ═════════════════════════════════════════════════════════════════
        // BUILD UI
        // ═════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            const int W = 560, H = 540;
            this.Text            = LM.Get("smtp_title");
            this.Size            = new Size(W, H);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = BgColor;
            this.Font            = new Font("Segoe UI", 10);

            // ── Header ────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 68,
                BackColor = MedGreen
            };
            var lblTitle = new Label
            {
                Text      = "⚙️  " + LM.Get("smtp_head"),
                Font      = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(236, 243, 158),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(lblTitle);
            this.Controls.Add(pnlHeader);

            // ── Cảnh báo nếu DPAPI thất bại ─────────────────────────────
            if (_isDpapiFailed)
            {
                var pnlWarn = new Panel
                {
                    Dock      = DockStyle.Top,
                    Height    = 52,
                    BackColor = Color.FromArgb(255, 243, 205)
                };
                var lblWarn = new Label
                {
                    Text      = "⚠️  " + LM.Get("smtp_warn"),
                    Font      = new Font("Segoe UI", 9.5f),
                    ForeColor = Color.FromArgb(130, 80, 0),
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Padding   = new Padding(8, 0, 8, 0)
                };
                pnlWarn.Controls.Add(lblWarn);
                this.Controls.Add(pnlWarn);
            }

            // ── Fields ────────────────────────────────────────────────────
            int y = _isDpapiFailed ? 136 : 84;
            int x = 40, fw = W - 80;

            Label MakeLbl(string t) => new Label
            {
                Text = t, AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = DarkGreen, BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
            Guna2TextBox MakeTxt(bool pass = false) => new Guna2TextBox
            {
                Size            = new Size(fw, 42),
                Location        = new Point(x, y + 22),
                BorderRadius    = 9,
                BorderThickness = 1,
                BorderColor     = Color.FromArgb(170, 205, 140),
                FillColor       = FieldBg,
                ForeColor       = DarkGreen,
                Font            = new Font("Segoe UI", 10.5f),
                TextOffset      = new Point(8, 0),
                PasswordChar    = pass ? '●' : '\0'
            };

            // Server
            this.Controls.Add(MakeLbl("SMTP Server"));
            _txtServer = MakeTxt(); _txtServer.PlaceholderText = "smtp.gmail.com";
            this.Controls.Add(_txtServer);
            y += 74;

            // Port (smaller) + Username (same row)
            this.Controls.Add(new Label
            {
                Text = LM.Get("smtp_port"), AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = DarkGreen, BackColor = Color.Transparent,
                Location = new Point(x, y)
            });
            this.Controls.Add(new Label
            {
                Text = LM.Get("smtp_acc"), AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = DarkGreen, BackColor = Color.Transparent,
                Location = new Point(x + 130, y)
            });
            _txtPort = new Guna2TextBox
            {
                Size = new Size(110, 42), Location = new Point(x, y + 22),
                BorderRadius = 9, BorderThickness = 1,
                BorderColor = Color.FromArgb(170, 205, 140),
                FillColor = FieldBg, ForeColor = DarkGreen,
                Font = new Font("Segoe UI", 10.5f), TextOffset = new Point(8, 0),
                PlaceholderText = "587"
            };
            _txtUsername = new Guna2TextBox
            {
                Size = new Size(fw - 130, 42), Location = new Point(x + 130, y + 22),
                BorderRadius = 9, BorderThickness = 1,
                BorderColor = Color.FromArgb(170, 205, 140),
                FillColor = FieldBg, ForeColor = DarkGreen,
                Font = new Font("Segoe UI", 10.5f), TextOffset = new Point(8, 0),
                PlaceholderText = "your_email@gmail.com"
            };
            this.Controls.Add(_txtPort);
            this.Controls.Add(_txtUsername);
            y += 74;

            // Password
            this.Controls.Add(new Label
            {
                Text = LM.Get("smtp_pw"), AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = DarkGreen, BackColor = Color.Transparent,
                Location = new Point(x, y)
            });
            _txtPassword = MakeTxt(pass: true);
            _txtPassword.PlaceholderText = LM.Get("smtp_pw_ph");
            this.Controls.Add(_txtPassword);
            y += 74;

            // Hint link
            var lnkHint = new LinkLabel
            {
                Text      = "📖 " + LM.Get("smtp_guide"),
                Font      = new Font("Segoe UI", 8.5f),
                LinkColor = Color.FromArgb(30, 90, 160),
                Location  = new Point(x, y - 18),
                AutoSize  = true,
                BackColor = Color.Transparent
            };
            lnkHint.LinkClicked += (s, e) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://myaccount.google.com/apppasswords") { UseShellExecute = true });
            this.Controls.Add(lnkHint);

            // Test email row
            this.Controls.Add(new Label
            {
                Text = LM.Get("smtp_test"), AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = DarkGreen, BackColor = Color.Transparent,
                Location = new Point(x, y)
            });
            _txtTestEmail = new Guna2TextBox
            {
                Size = new Size(fw - 130, 42), Location = new Point(x, y + 22),
                BorderRadius = 9, BorderThickness = 1,
                BorderColor = Color.FromArgb(170, 205, 140),
                FillColor = FieldBg, ForeColor = DarkGreen,
                Font = new Font("Segoe UI", 10.5f), TextOffset = new Point(8, 0),
                PlaceholderText = "email_test@gmail.com"
            };
            this.Controls.Add(_txtTestEmail);

            _btnTest = new Guna2Button
            {
                Text = "🔌 " + LM.Get("smtp_btn_test"),
                Size = new Size(110, 42),
                Location = new Point(x + fw - 110, y + 22),
                BorderRadius = 9,
                FillColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BorderThickness = 0,
                Cursor = Cursors.Hand
            };
            _btnTest.HoverState.FillColor = Color.FromArgb(0, 95, 180);
            _btnTest.Click += BtnTest_Click;
            this.Controls.Add(_btnTest);
            y += 74;

            // Status label
            _lblStatus = new Label
            {
                Text      = string.Empty,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = DarkGreen,
                Location  = new Point(x, y),
                Size      = new Size(fw, 24),
                BackColor = Color.Transparent
            };
            this.Controls.Add(_lblStatus);

            // ── Footer buttons ─────────────────────────────────────────────
            var pnlFooter = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 68,
                BackColor = Color.FromArgb(220, 235, 205)
            };

            _btnCancel = new Guna2Button
            {
                Text = LM.Get("smtp_cancel"), Size = new Size(110, 42),
                Location = new Point(W - 290, 13),
                BorderRadius = 9, FillColor = Color.FromArgb(200, 210, 195),
                ForeColor = DarkGreen, Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BorderThickness = 0, Cursor = Cursors.Hand
            };
            _btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            pnlFooter.Controls.Add(_btnCancel);

            _btnSave = new Guna2Button
            {
                Text = "💾  " + LM.Get("smtp_save"), Size = new Size(165, 42),
                Location = new Point(W - 180, 13),
                BorderRadius = 9, FillColor = MedGreen,
                ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BorderThickness = 0, Cursor = Cursors.Hand
            };
            _btnSave.HoverState.FillColor = Color.FromArgb(65, 110, 55);
            _btnSave.Click += BtnSave_Click;
            pnlFooter.Controls.Add(_btnSave);

            this.Controls.Add(pnlFooter);
        }

        // ═════════════════════════════════════════════════════════════════
        // PRE-FILL
        // ═════════════════════════════════════════════════════════════════
        private void PreFillFromConfig()
        {
            _txtServer.Text   = "smtp.gmail.com";
            _txtPort.Text     = "587";
            _txtUsername.Text = Program.SmtpUsernameFromConfig;
            // Không pre-fill password — admin phải nhập lại trên máy mới
        }

        // ═════════════════════════════════════════════════════════════════
        // TEST CONNECTION
        // ═════════════════════════════════════════════════════════════════
        private async void BtnTest_Click(object sender, EventArgs e)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            if (!ValidateInputs()) return;
            if (string.IsNullOrWhiteSpace(_txtTestEmail.Text))
            {
                SetStatus("⚠️  " + LM.Get("smtp_hint_test"), Color.FromArgb(160, 80, 0));
                return;
            }

            _btnTest.Enabled = false;
            _btnSave.Enabled = false;
            SetStatus(LM.Get("smtp_testing"), Color.FromArgb(0, 100, 200));

            bool ok = await TestSmtpConnectionAsync(
                _txtServer.Text.Trim(),
                int.Parse(_txtPort.Text.Trim()),
                _txtUsername.Text.Trim(),
                _txtPassword.Text,
                _txtTestEmail.Text.Trim());

            _btnTest.Enabled = true;
            _btnSave.Enabled = true;

            if (ok)
                SetStatus("✅  " + LM.Get("smtp_test_ok"), Color.FromArgb(20, 120, 20));
            else
                SetStatus("❌  " + LM.Get("smtp_test_fail"), Color.FromArgb(180, 0, 0));
        }

        private static async Task<bool> TestSmtpConnectionAsync(
            string server, int port, string username, string password, string toEmail)
        {
            try
            {
                using var client = new SmtpClient(server, port)
                {
                    UseDefaultCredentials = false,
                    Credentials  = new NetworkCredential(username, password),
                    EnableSsl    = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout      = 15_000
                };
                using var msg = new MailMessage
                {
                    From       = new System.Net.Mail.MailAddress(username, "ECOVA System (Test)", System.Text.Encoding.UTF8),
                    Subject    = "[ECOVA] Kiểm tra kết nối SMTP",
                    Body       = "<p>Email này xác nhận cấu hình SMTP ECOVA hoạt động bình thường.</p>",
                    IsBodyHtml = true,
                    BodyEncoding    = System.Text.Encoding.UTF8,
                    SubjectEncoding = System.Text.Encoding.UTF8
                };
                msg.To.Add(toEmail);
                await client.SendMailAsync(msg);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SmtpSetupForm] Test connection thất bại");
                return false;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // SAVE
        // ═════════════════════════════════════════════════════════════════
        private void BtnSave_Click(object sender, EventArgs e)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            if (!ValidateInputs()) return;

            try
            {
                string server   = _txtServer.Text.Trim();
                int    port     = int.Parse(_txtPort.Text.Trim());
                string username = _txtUsername.Text.Trim();
                string password = _txtPassword.Text; // plaintext từ form

                // Mã hóa bằng AES (cross-machine) — không dùng DPAPI nữa
                string encryptedPassword = AesHelper.Encrypt(password);


                // Ghi lại appsettings.json
                SaveToAppSettings(server, port, username, encryptedPassword);

                // Áp dụng ngay (không cần khởi động lại)
                EmailSmtpHelper.Configure(
                    server:      server,
                    port:        port,
                    username:    username,
                    password:    password,
                    enableSsl:   true,
                    displayName: "ECOVA System (No Reply)");

                Log.Information("[SmtpSetupForm] Cấu hình SMTP đã lưu và áp dụng ngay. User={U} @ {S}:{P}",
                    username, server, port);

                MessageBox.Show(
                    "✅  Thành công!\n\n" + LM.Get("smtp_save_ok_msg"),
                    LM.Get("msg_success"), MessageBoxButtons.OK, MessageBoxIcon.Information);

                this.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(LM.Get("smtp_save_fail") + ex.Message, LM.Get("msg_error"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════
        private bool ValidateInputs()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            if (string.IsNullOrWhiteSpace(_txtServer.Text))
            { SetStatus("⚠️  " + LM.Get("smtp_err_server"), Color.FromArgb(160, 80, 0)); _txtServer.Focus(); return false; }

            if (!int.TryParse(_txtPort.Text.Trim(), out _) || int.Parse(_txtPort.Text.Trim()) <= 0)
            { SetStatus("⚠️  " + LM.Get("smtp_err_port"), Color.FromArgb(160, 80, 0)); _txtPort.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_txtUsername.Text))
            { SetStatus("⚠️  " + LM.Get("smtp_err_user"), Color.FromArgb(160, 80, 0)); _txtUsername.Focus(); return false; }

            if (string.IsNullOrWhiteSpace(_txtPassword.Text))
            { SetStatus("⚠️  " + LM.Get("smtp_err_pw"), Color.FromArgb(160, 80, 0)); _txtPassword.Focus(); return false; }

            return true;
        }

        private void SetStatus(string text, Color color)
        {
            _lblStatus.Text      = text;
            _lblStatus.ForeColor = color;
        }

        /// <summary>
        /// Ghi lại SmtpSettings vào appsettings.json (giữ nguyên các key khác).
        /// Dùng Newtonsoft.Json để parse/merge JSON an toàn.
        /// </summary>
        private static void SaveToAppSettings(string server, int port, string username, string encryptedPassword)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

            JObject root;
            if (File.Exists(path))
            {
                string raw = File.ReadAllText(path, System.Text.Encoding.UTF8);
                root = JObject.Parse(raw);
            }
            else
            {
                root = new JObject();
            }

            // Upsert SmtpSettings
            if (root["SmtpSettings"] is not JObject smtpSection)
            {
                smtpSection = new JObject();
                root["SmtpSettings"] = smtpSection;
            }

            smtpSection["Server"]            = server;
            smtpSection["Port"]              = port;
            smtpSection["Username"]          = username;
            smtpSection["Password"]          = encryptedPassword;
            smtpSection["PasswordEncrypted"] = true;
            smtpSection["EnableSsl"]         = true;
            smtpSection["DisplayName"]       = "ECOVA System (No Reply)";

            File.WriteAllText(path, root.ToString(Formatting.Indented), System.Text.Encoding.UTF8);
            Log.Information("[SmtpSetupForm] Đã cập nhật appsettings.json với cấu hình SMTP mới.");
        }
    }
}
