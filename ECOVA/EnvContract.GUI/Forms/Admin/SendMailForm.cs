using EnvContract.BLL.Interfaces;
using EnvContract.DTO.Entities;
using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnvContract.GUI.Forms.Admin
{
    /// <summary>
    /// Dialog Admin gửi email hàng loạt đến người dùng hệ thống.
    /// Tích hợp trong EmployeeManagementUC — mở qua nút "📧 Gửi Mail".
    /// </summary>
    public class SendMailForm : Form
    {
        // ── Services ────────────────────────────────────────────────────────
        private readonly INotificationService _notificationService;
        private readonly IEmployeeService     _employeeService;

        // ── Controls ────────────────────────────────────────────────────────
        private CheckedListBox  _clbRecipients;
        private CheckBox        _chkSelectAll;
        private Label           _lblCount;
        private Guna2TextBox    _txtSubject;
        private RichTextBox     _rtbBody;
        private Guna2Button     _btnSend;
        private Guna2Button     _btnCancel;
        private ProgressBar     _progressBar;
        private Label           _lblProgress;   // "Đang gửi... 3/5"
        private Label           _lblResult;     // Kết quả cuối cùng

        // ── State ────────────────────────────────────────────────────────────
        private List<UserDTO> _employees = new();
        private bool _isSending = false;
        private CancellationTokenSource _cts = null;

        public SendMailForm(INotificationService notificationService, IEmployeeService employeeService)
        {
            _notificationService = notificationService;
            _employeeService     = employeeService;
            BuildUI();
            this.Load += async (s, e) => await LoadRecipientsAsync();
            this.FormClosing += SendMailForm_FormClosing;
        }

        // ════════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            
            // ── Form settings ──────────────────────────────────────────────
            this.Text            = LM.Get("mail_sys_title") + " — ECOVA";
            this.Size            = new Size(820, 620);
            this.StartPosition   = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = Color.FromArgb(245, 248, 242);
            this.Font            = new Font("Segoe UI", 10);

            // ── Header bar ────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 60,
                BackColor = Color.FromArgb(49, 87, 44)  // #31572C
            };

            var lblTitle = new Label
            {
                Text      = "📧  " + LM.Get("mail_sys_title"),
                ForeColor = Color.FromArgb(236, 243, 158),  // #ECF39E
                Font      = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(24, 16)
            };
            pnlHeader.Controls.Add(lblTitle);
            this.Controls.Add(pnlHeader);

            // ── Main layout: left list + right compose ─────────────────────
            int listW   = 270;
            int pad     = 20;
            int headerH = 60;
            int footerH = 80;
            int innerH  = this.ClientSize.Height - headerH - footerH;

            // ── LEFT: Recipient list ───────────────────────────────────────
            var lblRecip = new Label
            {
                Text      = LM.Get("mail_recipient"),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(49, 87, 44),
                Location  = new Point(pad, headerH + pad),
                AutoSize  = true
            };
            this.Controls.Add(lblRecip);

            _chkSelectAll = new CheckBox
            {
                Text      = LM.IsVietnamese ? "Chọn tất cả" : "Select All",
                Font      = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location  = new Point(pad, headerH + pad + 26),
                AutoSize  = true
            };
            _chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;
            this.Controls.Add(_chkSelectAll);

            _clbRecipients = new CheckedListBox
            {
                Location       = new Point(pad, headerH + pad + 54),
                Size           = new Size(listW, innerH - 54 - pad),
                BorderStyle    = BorderStyle.FixedSingle,
                Font           = new Font("Segoe UI", 10),
                CheckOnClick   = true,
                BackColor      = Color.FromArgb(240, 246, 232),
                ForeColor      = Color.FromArgb(40, 40, 40),
                IntegralHeight = false
            };
            _clbRecipients.ItemCheck += (s, e) =>
                this.BeginInvoke((Action)UpdateCountLabel);
            this.Controls.Add(_clbRecipients);

            _lblCount = new Label
            {
                Text      = LM.Get("mail_selected") + " 0",
                Font      = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                Location  = new Point(pad, headerH + innerH + 2),
                AutoSize  = true
            };
            this.Controls.Add(_lblCount);

            // ── RIGHT: Compose area ───────────────────────────────────────
            int rightX = pad + listW + pad;
            int rightW = this.ClientSize.Width - rightX - pad;

            var lblSubject = new Label
            {
                Text      = LM.Get("mail_subject"),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(49, 87, 44),
                Location  = new Point(rightX, headerH + pad),
                AutoSize  = true
            };
            this.Controls.Add(lblSubject);

            _txtSubject = new Guna2TextBox
            {
                Location        = new Point(rightX, headerH + pad + 26),
                Size            = new Size(rightW, 42),
                PlaceholderText = LM.Get("mail_ph_subject"),
                Font            = new Font("Segoe UI", 11),
                BorderRadius    = 8,
                BorderThickness = 1,
                BorderColor     = Color.FromArgb(161, 185, 114),
                FillColor       = Color.FromArgb(240, 246, 232),
                ForeColor       = Color.FromArgb(30, 30, 30),
                TextOffset      = new Point(8, 0)
            };
            this.Controls.Add(_txtSubject);

            var lblBody = new Label
            {
                Text      = LM.Get("mail_body"),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(49, 87, 44),
                Location  = new Point(rightX, headerH + pad + 80),
                AutoSize  = true
            };
            this.Controls.Add(lblBody);

            _rtbBody = new RichTextBox
            {
                Location    = new Point(rightX, headerH + pad + 106),
                Size        = new Size(rightW, innerH - 106 - pad),
                Font        = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.FromArgb(240, 246, 232),
                ForeColor   = Color.FromArgb(30, 30, 30),
                AcceptsTab  = false,
                WordWrap    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical
            };
            this.Controls.Add(_rtbBody);

            // ── FOOTER: progress + buttons ─────────────────────────────────
            var pnlFooter = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = footerH,
                BackColor = Color.FromArgb(220, 235, 205)
            };

            // Progress bar — chiếm hết chiều ngang trái, để chỗ cho 2 button bên phải
            _progressBar = new ProgressBar
            {
                Location  = new Point(pad, 12),
                Size      = new Size(this.ClientSize.Width - 340, 10),
                Style     = ProgressBarStyle.Continuous,
                Minimum   = 0,
                Value     = 0,
                Visible   = false,
                BackColor = Color.FromArgb(200, 220, 180),
                ForeColor = Color.FromArgb(49, 87, 44)
            };
            pnlFooter.Controls.Add(_progressBar);

            // Label tiến trình: "Đang gửi... 3/5"
            _lblProgress = new Label
            {
                Text      = string.Empty,
                Font      = new Font("Segoe UI", 9, FontStyle.Italic),
                ForeColor = Color.FromArgb(60, 90, 50),
                Location  = new Point(pad, 28),
                Size      = new Size(this.ClientSize.Width - 340, 20),
                Visible   = false
            };
            pnlFooter.Controls.Add(_lblProgress);

            // Label kết quả cuối cùng
            _lblResult = new Label
            {
                Text      = string.Empty,
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(49, 87, 44),
                Location  = new Point(pad, 30),
                AutoSize  = true
            };
            pnlFooter.Controls.Add(_lblResult);

            _btnCancel = new Guna2Button
            {
                Text            = LM.Get("mail_close_btn"),
                Size            = new Size(110, 42),
                BorderRadius    = 8,
                FillColor       = Color.FromArgb(200, 200, 200),
                ForeColor       = Color.FromArgb(60, 60, 60),
                Font            = new Font("Segoe UI", 11, FontStyle.Bold),
                BorderThickness = 0
            };
            _btnCancel.Location = new Point(pnlFooter.Width - 130 - pad, 19);
            _btnCancel.Anchor   = AnchorStyles.Right | AnchorStyles.Top;
            _btnCancel.Click   += (s, e) => this.Close();
            pnlFooter.Controls.Add(_btnCancel);

            _btnSend = new Guna2Button
            {
                Text            = "📤  " + LM.Get("mail_send_btn"),
                Size            = new Size(155, 42),
                BorderRadius    = 8,
                FillColor       = Color.FromArgb(49, 87, 44),
                ForeColor       = Color.FromArgb(236, 243, 158),
                Font            = new Font("Segoe UI", 11, FontStyle.Bold),
                BorderThickness = 0
            };
            _btnSend.Location = new Point(pnlFooter.Width - 130 - pad - 165, 19);
            _btnSend.Anchor   = AnchorStyles.Right | AnchorStyles.Top;
            _btnSend.Click   += BtnSend_Click;
            pnlFooter.Controls.Add(_btnSend);

            this.Controls.Add(pnlFooter);
        }

        // ════════════════════════════════════════════════════════════════════
        // LOGIC
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadRecipientsAsync()
        {
            try
            {
                _employees = await _employeeService.GetAllEmployeesAsync();

                _clbRecipients.BeginUpdate();
                _clbRecipients.Items.Clear();
                foreach (var emp in _employees)
                {
                    string display = string.IsNullOrWhiteSpace(emp.Email)
                        ? $"{emp.FullName}  (chưa có email)"
                        : $"{emp.FullName}  —  {emp.Email}";
                    // Mặc định chưa chọn ai — admin tự chọn thủ công
                    _clbRecipients.Items.Add(display, false);
                }
                _clbRecipients.EndUpdate();
                UpdateCountLabel();
            }
            catch (Exception ex)
            {
                var LM = EnvContract.Common.LanguageManager.Instance;
                MessageBox.Show(LM.Get("mail_err_load") + ex.Message,
                    LM.Get("msg_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ChkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            bool check = _chkSelectAll.Checked;
            for (int i = 0; i < _clbRecipients.Items.Count; i++)
            {
                // Chỉ check những người có email hợp lệ
                if (_employees[i].IsActive && !string.IsNullOrWhiteSpace(_employees[i].Email))
                    _clbRecipients.SetItemChecked(i, check);
            }
            UpdateCountLabel();
        }

        private void UpdateCountLabel()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            int n = _clbRecipients.CheckedIndices.Count;
            _lblCount.Text = LM.Get("mail_selected") + $" {n}";
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            // ── Validate ──────────────────────────────────────────────────
            var checkedEmails = _clbRecipients.CheckedIndices
                .Cast<int>()
                .Select(i => _employees[i].Email)
                .Where(email => !string.IsNullOrWhiteSpace(email))
                .ToList();

            if (checkedEmails.Count == 0)
            {
                MessageBox.Show(LM.Get("mail_err_no_recip"),
                    LM.Get("msg_warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtSubject.Text))
            {
                MessageBox.Show(LM.Get("mail_err_no_subj"), LM.Get("msg_warning"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtSubject.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(_rtbBody.Text))
            {
                MessageBox.Show(LM.Get("mail_err_no_body"), LM.Get("msg_warning"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _rtbBody.Focus();
                return;
            }

            // ── Confirm ───────────────────────────────────────────────────
            var confirm = MessageBox.Show(
                string.Format(LM.Get("mail_confirm_msg"), checkedEmails.Count),
                LM.Get("mail_confirm_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            // ── Khởi tạo trạng thái gửi ───────────────────────────────────
            _cts = new CancellationTokenSource();
            SetSendingState(true, checkedEmails.Count);

            try
            {
                var (sent, failed, failedEmails) = await _notificationService.SendBroadcastEmailAsync(
                    recipientEmails: checkedEmails,
                    subject:         _txtSubject.Text.Trim(),
                    plainTextBody:   _rtbBody.Text.Trim(),
                    onProgress: (current, total) =>
                    {
                        if (!this.IsHandleCreated) return;
                        this.BeginInvoke((Action)(() =>
                        {
                            var LM = EnvContract.Common.LanguageManager.Instance;
                            _progressBar.Value  = current;
                            _lblProgress.Text   = string.Format(LM.Get("mail_sending"), current, total);
                        }));
                    });

                // ── Hiện kết quả HOÀN THÀNH ──────────────────────────────
                ShowCompletionResult(sent, failed, failedEmails, checkedEmails.Count);
            }
            catch (Exception ex)
            {
                ShowErrorResult(ex.Message);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                // Unlock UI nhưng GIỮ progress bar đầy & label kết quả
                SetSendingState(false, keepProgress: true);
            }
        }

        private void ShowCompletionResult(int sent, int failed, List<string> failedEmails, int total)
        {
            if (!this.IsHandleCreated) return;
            var LM = EnvContract.Common.LanguageManager.Instance;

            // Đẩy progress về 100%
            _progressBar.Value = total;
            _lblProgress.Visible = false;

            if (failed == 0)
            {
                _lblResult.Text      = "✅  " + string.Format(LM.Get("mail_done"), sent, total);
                _lblResult.ForeColor = Color.FromArgb(20, 120, 20);
                _lblResult.Visible   = true;

                // Đổi nút Gửi Mail thành màu xanh nhạt báo "đã xong"
                _btnSend.Text      = "✓ " + LM.Get("mail_btn_done");
                _btnSend.FillColor = Color.FromArgb(76, 153, 0);
            }
            else
            {
                _lblResult.Text      = "⚠  " + string.Format(LM.Get("mail_done_detail"), sent, failed);
                _lblResult.ForeColor = Color.FromArgb(180, 80, 0);
                _lblResult.Visible   = true;

                MessageBox.Show(
                    $"[{failed}]\n{string.Join("\n", failedEmails)}",
                    LM.Get("mail_fail_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Đổi nút Hủy → "Đóng" rõ ràng
            _btnCancel.Text = LM.Get("mail_close_btn");
        }

        private void ShowErrorResult(string message)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            _lblProgress.Visible = false;
            _lblResult.Text      = "❌  " + LM.Get("mail_err_sending");
            _lblResult.ForeColor = Color.FromArgb(180, 0, 0);
            _lblResult.Visible   = true;

            MessageBox.Show(message, LM.Get("msg_error"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void SetSendingState(bool isSending, int total = 0, bool keepProgress = false)
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            _isSending = isSending;

            _btnSend.Enabled       = !isSending;
            _btnCancel.Enabled     = !isSending;
            _txtSubject.Enabled    = !isSending;
            _rtbBody.Enabled       = !isSending;
            _clbRecipients.Enabled = !isSending;
            _chkSelectAll.Enabled  = !isSending;

            if (isSending)
            {
                // Bắt đầu gửi: hiện progress, ẩn label kết quả cũ
                _progressBar.Maximum = total > 0 ? total : 1;
                _progressBar.Value   = 0;
                _progressBar.Visible = true;
                _lblProgress.Visible = true;
                _lblProgress.Text    = string.Format(LM.Get("mail_sending"), 0, total);
                _lblResult.Visible   = false;
                _lblResult.Text      = string.Empty;

                _btnSend.Text      = LM.Get("msg_loading");
                _btnSend.FillColor = Color.FromArgb(100, 130, 80);
                _btnCancel.Text    = LM.Get("mail_btn_cancel_send");
            }
            else if (!keepProgress)
            {
                // Reset hoàn toàn (dùng cho trường hợp cancel thủ công)
                _progressBar.Value   = 0;
                _progressBar.Visible = false;
                _lblProgress.Visible = false;
                _btnSend.Text        = "📤  " + LM.Get("mail_send_btn");
                _btnSend.FillColor   = Color.FromArgb(49, 87, 44);
            }
        }

        /// <summary>Ngăn đóng form khi đang gửi email.</summary>
        private void SendMailForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isSending)
            {
                e.Cancel = true;
                MessageBox.Show(
                    "Đang gửi email, không thể đóng ngay lúc này.\nVui lòng chờ quá trình hoàn tất.",
                    "Đang xử lý", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
