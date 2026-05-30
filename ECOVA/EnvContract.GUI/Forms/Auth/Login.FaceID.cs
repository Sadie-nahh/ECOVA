using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Guna.UI2.WinForms;
using EnvContract.BLL.Interfaces;
using EnvContract.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace EnvContract.GUI.Forms.Auth
{
    // ─────────────────────────────────────────────────────────────────────────
    // Login.FaceID.cs — Partial class quản lý toàn bộ luồng FaceID:
    //   Bước 1: Nhập username → kiểm tra DB
    //   Bước 2a: Đăng ký FaceID (camera + password x/n)
    //   Bước 2b: Đăng nhập FaceID (auto-scan với timer)
    //   Fallback: Hiển thị options sau khi quét thất bại 5 lần
    //
    // Logic xử lý ảnh (DNN detect, compare) → FaceIdManager.cs
    // ─────────────────────────────────────────────────────────────────────────
    public partial class Login
    {
        // ── FaceID UI fields ─────────────────────────────────────────────────
        private Guna2Panel   pnlFaceID;
        private Panel        pnlFaceDynamic;
        private Label        lblFaceTitle;
        private Label        lblFaceStatus;
        private Guna2TextBox txtFaceUsername;
        private Guna2PictureBox picCamera;

        // ── FaceID login state ───────────────────────────────────────────────
        private Timer  _faceAutoScanTimer;
        private int    _faceAttemptCount;
        private bool   _faceScanning;
        private string _faceLoginUsername;
        private byte[] _faceLoginStoredData;

        // ─────────────────────────────────────────────────────────────────────
        // Panel setup (được gọi từ InitializeComponentManual trong Login.cs)
        // ─────────────────────────────────────────────────────────────────────
        private void CreateFaceIDPanel()
        {
            pnlFaceID = new Guna2Panel
            {
                Size        = new Size(500, 700),
                FillColor   = Color.FromArgb(210, 206, 219, 192),
                BorderRadius = 25,
                BackColor   = Color.Transparent,
                Visible     = false,
                ShadowDecoration = { Enabled = false }
            };
            this.Controls.Add(pnlFaceID);
            pnlFaceID.Left = (this.ClientSize.Width  - pnlFaceID.Width)  / 2;
            pnlFaceID.Top  = (this.ClientSize.Height - pnlFaceID.Height) / 2;

            lblFaceTitle = new Label
            {
                Text      = LanguageManager.Instance.Get("login_faceid_title"),
                Font      = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 46, 26),
                AutoSize  = true,
                BackColor = Color.Transparent
            };
            lblFaceTitle.Location = new Point((pnlFaceID.Width - lblFaceTitle.PreferredSize.Width) / 2, 20);
            pnlFaceID.Controls.Add(lblFaceTitle);

            var lblUN = new Label
            {
                Text      = LanguageManager.Instance.Get("login_faceid_un"),
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 46, 26),
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(50, 65)
            };
            pnlFaceID.Controls.Add(lblUN);

            txtFaceUsername = new Guna2TextBox
            {
                PlaceholderText      = LanguageManager.Instance.Get("login_faceid_un_ph"),
                PlaceholderForeColor = Color.FromArgb(160, 180, 150),
                BorderRadius  = 12,
                Size          = new Size(400, 46),
                Location      = new Point(50, 96),
                Font          = new Font("Segoe UI", 11),
                FillColor     = Color.FromArgb(64, 88, 60),
                ForeColor     = Color.White,
                BorderThickness = 0,
                BackColor     = Color.Transparent
            };
            pnlFaceID.Controls.Add(txtFaceUsername);

            lblFaceStatus = new Label
            {
                Text      = "",
                Location  = new Point(50, 158),
                Size      = new Size(400, 36),
                Font      = new Font("Segoe UI", 10, FontStyle.Italic),
                ForeColor = Color.FromArgb(40, 60, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                AutoSize  = false
            };
            pnlFaceID.Controls.Add(lblFaceStatus);

            pnlFaceDynamic = new Panel
            {
                Location  = new Point(50, 200),
                Size      = new Size(400, 390),
                BackColor = Color.Transparent
            };
            pnlFaceID.Controls.Add(pnlFaceDynamic);

            var btnBackF = new Guna2Button
            {
                Text            = LanguageManager.Instance.Get("login_faceid_back"),
                Size            = new Size(400, 46),
                BorderRadius    = 15,
                FillColor       = Color.Transparent,
                BorderThickness = 2,
                BorderColor     = Color.FromArgb(145, 175, 100),
                Font            = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor       = Color.FromArgb(24, 46, 26),
                Cursor          = Cursors.Hand,
                BackColor       = Color.Transparent
            };
            btnBackF.Location = new Point(50, 624);
            btnBackF.Click += async (s, e) =>
            {
                StopCamera();
                await Helpers.FormTransitionHelper.PanelSwitchAsync(this, () =>
                {
                    pnlFaceID.Visible = false;
                    ShowLoginCard();
                });
            };
            pnlFaceID.Controls.Add(btnBackF);
        }

        private void ShowFacePanel()
        {
            if (pnlFaceID == null) CreateFaceIDPanel();
            pnlFaceID.Visible = true;
            pnlFaceID.BringToFront();
            pnlFaceID.Left = (this.ClientSize.Width  - pnlFaceID.Width)  / 2;
            pnlFaceID.Top  = (this.ClientSize.Height - pnlFaceID.Height) / 2;
            btnBackToHome.Visible = false;
            ShowFaceStep1();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Bước 1: Nhập username → kiểm tra DB
        // ─────────────────────────────────────────────────────────────────────
        private void ShowFaceStep1()
        {
            StopCamera();
            txtFaceUsername.Enabled = true;
            txtFaceUsername.Text    = "";
            SetFaceStatus(LanguageManager.Instance.Get("login_faceid_status_input"), Color.FromArgb(40, 60, 40));

            pnlFaceDynamic.Controls.Clear();
            var btnContinue = MakeFaceBtn(LanguageManager.Instance.Get("faceid_btn_continue"), Color.FromArgb(145, 175, 100), 0);
            btnContinue.Name = "btnFaceContinue";
            btnContinue.Click += async (s, e) => await OnFaceStep1Continue();
            pnlFaceDynamic.Controls.Add(btnContinue);
        }

        private async Task OnFaceStep1Continue()
        {
            string username = txtFaceUsername.Text.Trim();
            var LM = LanguageManager.Instance;
            if (string.IsNullOrEmpty(username))
            {
                SetFaceStatus(LM.Get("faceid_status_input"), Color.Red);
                return;
            }

            SetFaceStatus(LM.Get("faceid_checking_acc"), Color.FromArgb(0, 100, 200));
            txtFaceUsername.Enabled = false;

            AppLogger.Info($"FaceID: Kiểm tra tài khoản '{username}'");
            byte[] storedFace = await _userBLL.GetFaceIDDataAsync(username);

            if (storedFace == null)
            {
                AppLogger.Warning($"FaceID: Tài khoản '{username}' không tồn tại hoặc bị khóa");
                SetFaceStatus(LM.Get("faceid_err_not_exist"), Color.Red);
                txtFaceUsername.Enabled = true;
                return;
            }

            if (storedFace.Length == 0)
            {
                AppLogger.Info($"FaceID: Tài khoản '{username}' chưa đăng ký FaceID");
                SetFaceStatus(LM.Get("faceid_unregistered"), Color.Orange);
                ShowFaceStepRegister(username);
            }
            else
            {
                AppLogger.Info($"FaceID: Tài khoản '{username}' đã có FaceID → vào chế độ đăng nhập");
                SetFaceStatus(LM.Get("faceid_look_camera"), Color.FromArgb(40, 60, 40));
                ShowFaceStepLogin(username, storedFace);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Bước 2a: Đăng ký FaceID
        // ─────────────────────────────────────────────────────────────────────
        private void ShowFaceStepRegister(string username)
        {
            lblFaceTitle.Text = LanguageManager.Instance.Get("faceid_register_title");
            lblFaceTitle.Location = new Point((pnlFaceID.Width - lblFaceTitle.PreferredSize.Width) / 2, 20);
            pnlFaceDynamic.Controls.Clear();

            picCamera = MakeCameraBox(0);
            pnlFaceDynamic.Controls.Add(picCamera);

            var lblPw = new Label
            {
                Text      = LanguageManager.Instance.Get("login_faceid_pw"),
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 46, 26),
                BackColor = Color.Transparent,
                AutoSize  = true,
                Location  = new Point(0, 222)
            };
            pnlFaceDynamic.Controls.Add(lblPw);

            var txtPw = new Guna2TextBox
            {
                PlaceholderText      = LanguageManager.Instance.Get("login_faceid_pw_ph"),
                PlaceholderForeColor = Color.FromArgb(160, 180, 150),
                BorderRadius    = 12,
                Size            = new Size(400, 44),
                Location        = new Point(0, 244),
                Font            = new Font("Segoe UI", 11),
                FillColor       = Color.FromArgb(64, 88, 60),
                ForeColor       = Color.White,
                BorderThickness = 0,
                BackColor       = Color.Transparent,
                PasswordChar    = '●'
            };
            pnlFaceDynamic.Controls.Add(txtPw);

            var btnReg = MakeFaceBtn(LanguageManager.Instance.Get("login_faceid_btn_reg"), Color.FromArgb(49, 87, 44), 302);
            btnReg.ForeColor = Color.White;
            btnReg.Click += async (s, e) => await OnFaceRegister(username, txtPw);
            pnlFaceDynamic.Controls.Add(btnReg);

            StartCamera();
        }

        private async Task OnFaceRegister(string username, Guna2TextBox txtPw)
        {
            var LM = LanguageManager.Instance;
            if (_currentFrame == null || _currentFrame.IsEmpty)
            {
                SetFaceStatus(LM.Get("faceid_cam_not_ready"), Color.Red); return;
            }
            string password = txtPw.Text;
            if (string.IsNullOrEmpty(password))
            {
                SetFaceStatus(LM.Get("faceid_err_pw_empty"), Color.Red); return;
            }

            // Đếm ngược 3 giây để user căn mặt vào oval
            for (int i = 3; i >= 1; i--)
            {
                SetFaceStatus(string.Format(LM.Get("faceid_countdown"), i), Color.FromArgb(0, 100, 200));
                await Task.Delay(1000);
            }

            SetFaceStatus(LM.Get("faceid_capturing"), Color.FromArgb(0, 100, 200));
            AppLogger.Info($"FaceID: Đăng ký mặt cho tài khoản '{username}'");

            // ★ FIX: Clone frame để tránh race condition với camera timer
            Mat frameSnapshot;
            lock (this)
            {
                if (_currentFrame == null || _currentFrame.IsEmpty)
                {
                    SetFaceStatus(LM.Get("faceid_cam_not_ready_retry"), Color.Red);
                    return;
                }
                frameSnapshot = _currentFrame.Clone();
            }

            // FaceIdManager xử lý extract
            byte[] faceData;
            using (frameSnapshot)
                faceData = FaceIdManager.ExtractNormalizedFace(frameSnapshot);

            if (faceData == null || faceData.Length == 0)
            {
                AppLogger.Warning("FaceID: Không phát hiện được khuôn mặt khi đăng ký");
                SetFaceStatus(LM.Get("faceid_no_face_detect"), Color.Red);
                return;
            }

            bool ok = await _userBLL.RegisterFaceIDAsync(username, password, faceData);
            if (ok)
            {
                AppLogger.Info($"FaceID: Đăng ký thành công cho '{username}'");
                SetFaceStatus(LM.Get("faceid_reg_success"), Color.Green);
                lblFaceTitle.Text     = LanguageManager.Instance.Get("login_faceid_title");
                lblFaceTitle.Location = new Point((pnlFaceID.Width - lblFaceTitle.PreferredSize.Width) / 2, 20);
                await Task.Delay(1500);
                SetFaceStatus(LM.Get("faceid_look_camera_login"), Color.FromArgb(40, 60, 40));
                ShowFaceStepLogin(username, faceData);
            }
            else
            {
                AppLogger.Warning($"FaceID: Đăng ký thất bại cho '{username}' — sai mật khẩu hoặc bị khóa");
                SetFaceStatus(LM.Get("faceid_pw_wrong"), Color.Red);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Bước 2b: Đăng nhập FaceID — Auto-scan (iPhone-style)
        // ─────────────────────────────────────────────────────────────────────
        private void ShowFaceStepLogin(string username, byte[] storedFace)
        {
            _faceLoginUsername   = username;
            _faceLoginStoredData = storedFace;
            _faceAttemptCount    = 0;
            _faceScanning        = false;

            lblFaceTitle.Text     = LanguageManager.Instance.Get("faceid_login_title");
            lblFaceTitle.Location = new Point((pnlFaceID.Width - lblFaceTitle.PreferredSize.Width) / 2, 20);
            pnlFaceDynamic.Controls.Clear();

            picCamera = MakeCameraBox(0);
            picCamera.Size = new Size(400, 260);
            pnlFaceDynamic.Controls.Add(picCamera);

            var lblScanHint = new Label
            {
                Text      = LanguageManager.Instance.Get("faceid_auto_scanning"),
                Font      = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 200),
                BackColor = Color.Transparent,
                AutoSize  = false,
                Size      = new Size(400, 35),
                Location  = new Point(0, 270),
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlFaceDynamic.Controls.Add(lblScanHint);

            StartCamera();
            SetFaceStatus(LanguageManager.Instance.Get("faceid_look_camera_login"), Color.FromArgb(0, 120, 200));
            StartFaceAutoScan();
        }

        private void StartFaceAutoScan()
        {
            StopFaceAutoScan();
            _faceAutoScanTimer = new Timer { Interval = AppConfig.FaceId.ScanIntervalMs };
            _faceAutoScanTimer.Tick += async (s, e) => await AutoScanTick();
            _faceAutoScanTimer.Start();
        }

        private void StopFaceAutoScan()
        {
            _faceAutoScanTimer?.Stop();
            _faceAutoScanTimer?.Dispose();
            _faceAutoScanTimer = null;
        }

        private async Task AutoScanTick()
        {
            if (_faceScanning) return;
            if (_currentFrame == null || _currentFrame.IsEmpty) return;

            _faceScanning = true;
            try
            {
                // ★ FIX: Clone frame để tránh race condition — camera tick có thể
                // Dispose _currentFrame trong khi chúng ta đang xử lý
                Mat frameToProcess;
                lock (this)
                {
                    if (_currentFrame == null || _currentFrame.IsEmpty) return;
                    frameToProcess = _currentFrame.Clone();
                }

                using (frameToProcess)
                {
                    // requireDnn:true → bắt buộc DNN phát hiện mặt người thật sự.
                    // Nếu null: không có mặt hoặc DNN lỗi → KHÔNG đếm attempt (tránh bị khóa oan).
                    byte[] cameraFace = FaceIdManager.ExtractNormalizedFace(frameToProcess, requireDnn: true);
                    if (cameraFace == null || cameraFace.Length == 0)
                    {
                        SetFaceStatus(LanguageManager.Instance.Get("faceid_look_straight"), Color.FromArgb(200, 150, 0));
                        return;  // Không tăng _faceAttemptCount
                    }

                    double similarity = FaceIdManager.CompareHistogram(cameraFace, _faceLoginStoredData);
                    _faceAttemptCount++;
                    AppLogger.Info($"FaceID: Auto-scan #{_faceAttemptCount} user='{_faceLoginUsername}' similarity={similarity:F3} (threshold={AppConfig.FaceId.Threshold:F2})");

                    if (similarity >= AppConfig.FaceId.Threshold)
                    {
                        StopFaceAutoScan();
                        SetFaceStatus(LanguageManager.Instance.Get("faceid_auth_success"), Color.LightGreen);

                        bool ok = await _userBLL.LoginWithFaceIDAsync(_faceLoginUsername);
                        if (ok)
                        {
                            AppLogger.Info($"FaceID: Đăng nhập thành công user='{_faceLoginUsername}'");
                            SetFaceStatus(string.Format(LanguageManager.Instance.Get("faceid_welcome"), AppState.Instance.CurrentUser?.FullName), Color.LightGreen);
                            await Task.Delay(800);
                            StopCamera();
                            var mainForm = Program.ServiceProvider.GetRequiredService<Forms.Main.MainForm>();
                            mainForm.FormClosed += (s2, args) => this.Close();
                            await Helpers.FormTransitionHelper.TransitionAsync(this, mainForm, closeFrom: false);
                        }
                        else
                        {
                            AppLogger.Warning($"FaceID: Tài khoản '{_faceLoginUsername}' bị khóa");
                            SetFaceStatus(LanguageManager.Instance.Get("faceid_acc_locked"), Color.Red);
                            ShowFaceFailedOptions();
                        }
                        return;
                    }

                    if (_faceAttemptCount >= AppConfig.FaceId.MaxAttempts)
                    {
                        AppLogger.Warning($"FaceID: Thất bại sau {AppConfig.FaceId.MaxAttempts} lần thử cho '{_faceLoginUsername}'");
                        StopFaceAutoScan();
                        SetFaceStatus(string.Format(LanguageManager.Instance.Get("faceid_max_attempts"), AppConfig.FaceId.MaxAttempts), Color.Red);
                        ShowFaceFailedOptions();
                    }
                    else
                    {
                        int remaining = AppConfig.FaceId.MaxAttempts - _faceAttemptCount;
                        SetFaceStatus(string.Format(LanguageManager.Instance.Get("faceid_scanning_progress"), remaining), Color.FromArgb(200, 150, 0));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("FaceID: AutoScanTick lỗi", ex);
            }
            finally
            {
                _faceScanning = false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fallback: Options sau khi quét thất bại
        // ─────────────────────────────────────────────────────────────────────
        private void ShowFaceFailedOptions()
        {
            StopCamera();
            pnlFaceDynamic.Controls.Clear();

            var lblFail = new Label
            {
                Text      = "😔",
                Font      = new Font("Segoe UI", 48),
                AutoSize  = false,
                Size      = new Size(400, 80),
                Location  = new Point(0, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlFaceDynamic.Controls.Add(lblFail);

            var lblMsg = new Label
            {
                Text      = LanguageManager.Instance.Get("faceid_fail_msg"),
                Font      = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize  = false,
                Size      = new Size(400, 60),
                Location  = new Point(0, 100),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlFaceDynamic.Controls.Add(lblMsg);

            var btnReRegister = MakeFaceBtn(LanguageManager.Instance.Get("login_faceid_btn_update"), Color.FromArgb(49, 87, 44), 180);
            btnReRegister.ForeColor = Color.White;
            btnReRegister.Click += (s, e) =>
            {
                SetFaceStatus(LanguageManager.Instance.Get("faceid_update_prompt"), Color.Orange);
                ShowFaceStepRegister(_faceLoginUsername);
            };
            pnlFaceDynamic.Controls.Add(btnReRegister);

            var btnManual = MakeFaceBtn(LanguageManager.Instance.Get("login_faceid_btn_manual"), Color.FromArgb(100, 120, 140), 245);
            btnManual.ForeColor = Color.White;
            btnManual.Click += async (s, e) =>
            {
                StopCamera();
                await Helpers.FormTransitionHelper.PanelSwitchAsync(this, () =>
                {
                    pnlFaceID.Visible = false;
                    ShowLoginCard();
                });
            };
            pnlFaceDynamic.Controls.Add(btnManual);

            var btnRetry = MakeFaceBtn(LanguageManager.Instance.Get("login_faceid_btn_retry"), Color.FromArgb(145, 175, 100), 310);
            btnRetry.Click += (s, e) => ShowFaceStepLogin(_faceLoginUsername, _faceLoginStoredData);
            pnlFaceDynamic.Controls.Add(btnRetry);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Camera management
        // ─────────────────────────────────────────────────────────────────────
        private void StartCamera()
        {
            StopCamera();
            try
            {
                AppLogger.Info("FaceID: Khởi động camera");
                _capture      = new VideoCapture(0);
                _cameraTimer  = new Timer { Interval = 30 };
                _cameraTimer.Tick += (s, e) =>
                {
                    using var frame = _capture.QueryFrame();
                    if (frame != null && !frame.IsEmpty)
                    {
                        CvInvoke.Flip(frame, frame, FlipType.Horizontal);

                        // ★ FIX: lock khi ghi _currentFrame — đồng bộ với AutoScanTick
                        Mat oldFrame;
                        lock (this)
                        {
                            oldFrame = _currentFrame;
                            _currentFrame = frame.Clone();
                        }
                        oldFrame?.Dispose();

                        if (picCamera != null && !picCamera.IsDisposed)
                        {
                            // Vẽ face guide overlay lên bản hiển thị (không ảnh hưởng _currentFrame)
                            using var displayFrame = frame.Clone();
                            FaceIdManager.DrawFaceGuide(displayFrame);

                            using var image = displayFrame.ToImage<Bgr, byte>();
                            var bmp    = image.ToBitmap();
                            var oldImg = picCamera.Image;
                            picCamera.Image = bmp;
                            oldImg?.Dispose();
                        }
                    }
                };
                _cameraTimer.Start();
                SetFaceStatus(LanguageManager.Instance.Get("faceid_cam_ready"), Color.FromArgb(40, 60, 40));
            }
            catch (Exception ex)
            {
                AppLogger.Error("FaceID: Lỗi khởi động camera", ex);
                SetFaceStatus(string.Format(LanguageManager.Instance.Get("faceid_cam_err"), ex.Message), Color.Red);
            }
        }

        private void StopCamera()
        {
            StopFaceAutoScan();
            _cameraTimer?.Stop();
            _cameraTimer?.Dispose();
            _capture?.Dispose();
            _cameraTimer = null;
            _capture     = null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI Helpers
        // ─────────────────────────────────────────────────────────────────────
        private void SetFaceStatus(string msg, Color color)
        {
            lblFaceStatus.Text      = msg;
            lblFaceStatus.ForeColor = color;
        }

        private static Guna2Button MakeFaceBtn(string text, Color fill, int y) => new Guna2Button
        {
            Text         = text,
            Size         = new Size(400, 50),
            Location     = new Point(0, y),
            BorderRadius = 15,
            FillColor    = fill,
            Font         = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor    = Color.FromArgb(24, 46, 26),
            HoverState   = { FillColor = Color.FromArgb(130, 160, 80) },
            Cursor       = Cursors.Hand,
            BackColor    = Color.Transparent
        };

        private static Guna2PictureBox MakeCameraBox(int y) => new Guna2PictureBox
        {
            Size         = new Size(400, 210),
            Location     = new Point(0, y),
            BorderRadius = 15,
            FillColor    = Color.Black,
            SizeMode     = PictureBoxSizeMode.Zoom,
            BackColor    = Color.Transparent
        };
    }
}
