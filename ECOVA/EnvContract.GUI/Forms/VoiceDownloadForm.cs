using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace EnvContract.GUI.Forms
{
    /// <summary>
    /// Hiển thị progress khi download + giải nén Vosk VN model (~78MB) lần đầu.
    /// Tự đóng khi hoàn tất. User có thể hủy.
    /// </summary>
    public class VoiceDownloadForm : Form
    {
        private readonly ProgressBar             _bar;
        private readonly Label                   _lblStatus;
        private readonly Label                   _lblPercent;
        private readonly CancellationTokenSource _cts = new();
        private bool _success;

        public bool Success => _success;

        public VoiceDownloadForm()
        {
            var LM = EnvContract.Common.LanguageManager.Instance;
            Text            = LM.Get("voz_title");
            Size            = new Size(520, 240);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = UIConstants.WhiteBackground;
            Font            = new Font("Segoe UI", 10);

            var lblTitle = new Label
            {
                Text      = LM.Get("voz_header"),
                Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = UIConstants.DarkGreenBackground,
                Location  = new Point(25, 20),
                AutoSize  = true
            };

            var lblDesc = new Label
            {
                Text      = LM.Get("voz_desc"),
                Location  = new Point(25, 58),
                Size      = new Size(470, 44),
                ForeColor = UIConstants.TextDark
            };

            _bar = new ProgressBar
            {
                Location  = new Point(25, 112),
                Size      = new Size(466, 22),
                Minimum   = 0,
                Maximum   = 100,
                Style     = ProgressBarStyle.Continuous
            };

            _lblPercent = new Label
            {
                Text      = "0%",
                Location  = new Point(25, 142),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = UIConstants.PrimaryColor
            };

            _lblStatus = new Label
            {
                Text      = LM.Get("voz_status_connect"),
                Location  = new Point(75, 142),
                AutoSize  = true,
                ForeColor = UIConstants.TextMuted
            };

            var btnCancel = new Button
            {
                Text      = LM.Get("msg_cancel"),
                Location  = new Point(406, 168),
                Size      = new Size(85, 32),
                FlatStyle = FlatStyle.Flat,
                ForeColor = UIConstants.TextDark,
                Cursor    = Cursors.Hand
            };
            btnCancel.Click += (_, _) =>
            {
                _cts.Cancel();
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.AddRange(new Control[]
                { lblTitle, lblDesc, _bar, _lblPercent, _lblStatus, btnCancel });
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await RunDownloadAsync();
        }

        private async System.Threading.Tasks.Task RunDownloadAsync()
        {
            try
            {
                var LM = EnvContract.Common.LanguageManager.Instance;
                _lblStatus.Text = LM.Get("voz_status_down");

                await VoiceModelManager.EnsureReadyAsync(
                    onProgress: (recv, total) =>
                    {
                        if (!IsHandleCreated || IsDisposed) return;
                        Invoke(() =>
                        {
                            if (total > 0)
                            {
                                int pct          = (int)(recv * 100L / total);
                                _bar.Value       = Math.Min(pct, 100);
                                _lblPercent.Text = $"{pct}%";
                                _lblStatus.Text  = string.Format(LM.Get("voz_status_partial"), (recv / 1_048_576.0).ToString("F1")) + $" / {(total / 1_048_576.0):F1} MB";
                            }
                            else
                            {
                                _bar.Style       = ProgressBarStyle.Marquee;
                                _lblPercent.Text = "...";
                                _lblStatus.Text  = string.Format(LM.Get("voz_status_partial"), (recv / 1_048_576.0).ToString("F1"));
                            }
                        });
                    },
                    onStatus: msg =>
                    {
                        // Callback khi chuyển sang bước giải nén
                        if (!IsHandleCreated || IsDisposed) return;
                        Invoke(() =>
                        {
                            _bar.Style                 = ProgressBarStyle.Marquee;
                            _bar.MarqueeAnimationSpeed = 30;
                            _lblPercent.Text           = "...";
                            _lblStatus.Text            = msg;
                        });
                    },
                    ct: _cts.Token);

                _success = true;
                if (IsHandleCreated && !IsDisposed)
                {
                    Invoke(() =>
                    {
                        _bar.Style       = ProgressBarStyle.Continuous;
                        _bar.Value       = 100;
                        _lblPercent.Text = "100%";
                        _lblStatus.Text  = LM.Get("voz_status_done");
                        DialogResult     = DialogResult.OK;
                        Close();
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // User hủy — form đã đóng
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[VoiceDownload] Lỗi download/giải nén");
                if (IsHandleCreated && !IsDisposed)
                {
                    Invoke(() =>
                    {
                        var LM = EnvContract.Common.LanguageManager.Instance;
                        MessageBox.Show(
                            string.Format(LM.Get("voz_error_desc"), ex.Message),
                            LM.Get("voz_error_title"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.Abort;
                        Close();
                    });
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts.Cancel();
            base.OnFormClosing(e);
        }
    }
}
