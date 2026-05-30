using Emgu.CV;
using Emgu.CV.Structure;
using Guna.UI2.WinForms;
using EnvContract.BLL.Interfaces;
using EnvContract.Common;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Threading.Tasks;
using Timer = System.Windows.Forms.Timer;

namespace EnvContract.GUI.Forms.Auth
{
    public partial class Login : Form
    {
        private readonly IUserBLL _userBLL;
        
        // EmguCV Camera objects
        private VideoCapture _capture;
        private Mat _currentFrame;
        private Timer _cameraTimer;
        
        // UI Controls
        private Guna2Panel cardPanel;
        private Guna2Panel pnlIntro; // Panel Introduction (Default Screen)
        private Guna2Panel pnlFooter; // Footer
        private Guna2TextBox txtUsername;
        private Guna2TextBox txtPassword;
        private Guna2Button btnLoginSubmit; // Rename to avoid conflict with "btnOpenLogin"
        private Guna2Button btnOpenLogin;  // Nút chữ nhật to ở Intro
        private Guna2Button btnFaceID; // Nút đăng nhập Face ID mới
        private Guna2Button btnBackToHome;
        private Guna2DragControl dragControl;

        private string _introBgPath;
        private string _loginBgPath;
        private Image _introBgImage = null;
        private Image _loginBgImage = null;

        // Cho chức năng tự vẽ (Paint) UI Kính mờ
        private string _loginStatusMessage = string.Empty;
        private Color _loginStatusColor = Color.Red;
        private Image _loginCardIconImg = null;
        private Image _loginCardTenPhanMemImg = null;
        private string _footerLogoPath = string.Empty;

        // Toggle show/hide password
        private Image _eyeOpenImg = null;
        private Image _eyeClosedImg = null;
        private bool _passwordVisible = false;

        // Hover state cho "Quên mật khẩu?"
        private bool _forgotHovered = false;
        private Rectangle _forgotRect = new Rectangle(50, 350, 150, 20);

        private void SetLoginStatus(string msg, Color clr)
        {
            _loginStatusMessage = msg;
            _loginStatusColor = clr;
            if (cardPanel != null) cardPanel.Invalidate();
        }

        public Login(IUserBLL userBLL)
        {
            _userBLL = userBLL;
            this.DoubleBuffered = true;
            InitializeComponentManual();
            
            this.Resize += (s, e) => CenterCard();
        }

        private void InitializeComponentManual()
        {
            // 1. Form Setup
            this.Text = LanguageManager.Instance.Get("form_title");
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable; 
            this.MinimizeBox = false;
            this.MaximizeBox = true;
            this.MinimumSize = new Size(1150, 720);
            // Background setup
            string assetsPath = GetAssetsPath();
            // Set Form Icon — dùng AppIcon đã load tập trung ở Program.cs
            if (Program.AppIcon != null)
                this.Icon = Program.AppIcon;

            _introBgPath = Path.Combine(assetsPath, "images", "Giới thiệu.jpg");
            _loginBgPath = Path.Combine(assetsPath, "images", "BACKGROUND-LOGIN.jpg"); // Fixed path with "images" folder

            if (File.Exists(_introBgPath))
            {
                _introBgImage = Image.FromFile(_introBgPath);
                this.BackgroundImage = _introBgImage;
                this.BackgroundImageLayout = ImageLayout.Stretch;
            }
            else
            {
                this.BackColor = Color.FromArgb(240, 245, 240); // Fallback color
            }

            if (File.Exists(_loginBgPath))
            {
                _loginBgImage = Image.FromFile(_loginBgPath);
            }

            // Nút Back to Home (Nằm ngoài card, góc trên trái)
            btnBackToHome = new Guna2Button
            {
                Text = LanguageManager.Instance.Get("login_back"),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                FillColor = Color.Transparent, // Đặt nền trong suốt
                ForeColor = Color.White,
                Size = new Size(120, 45),
                Location = new Point(20, 20),
                Cursor = Cursors.Hand,
                UseTransparentBackground = true,
                Visible = false
            };
            btnBackToHome.Click += async (s, e) => {
                await Helpers.FormTransitionHelper.PanelSwitchAsync(this, () => {
                    cardPanel.Visible = false;
                    btnBackToHome.Visible = false;
                    pnlIntro.Visible = true;
                    pnlFooter.Visible = true;
                    if (_introBgImage != null) this.BackgroundImage = _introBgImage;
                });
            };
            this.Controls.Add(btnBackToHome);
            
            // Xây dựng Panel chứa Language Switcher dùng chung đặt ở góc phải Form
            var pnlLangShared = new FlowLayoutPanel
            {
                Size = new Size(110, 36),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0),
                Location = new Point(this.ClientSize.Width - 140, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            
            string ngonNguPath = Path.Combine(assetsPath, "images", "NgonNgu.png");
            var pbNgonNgu = new Guna2PictureBox
            {
                Size = new Size(30, 30),
                Margin = new Padding(0, 4, 4, 0),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            if (File.Exists(ngonNguPath)) pbNgonNgu.Image = Image.FromFile(ngonNguPath);
            pnlLangShared.Controls.Add(pbNgonNgu);

            var lblLang = new Label
            {
                Text = LanguageManager.Instance.ToggleLabel,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(236, 243, 158),
                BackColor = Color.Transparent,
                Margin = new Padding(0, 2, 0, 0),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            pnlLangShared.Controls.Add(lblLang);
            this.Controls.Add(pnlLangShared);
            pnlLangShared.BringToFront();

            // Language toggle click handlers
            lblLang.Click += (s, e) => { LanguageManager.Instance.ToggleLanguage(); };
            pbNgonNgu.Click += (s, e) => { LanguageManager.Instance.ToggleLanguage(); };
            pnlLangShared.Click += (s, e) => { LanguageManager.Instance.ToggleLanguage(); };
            pbNgonNgu.Cursor = Cursors.Hand;
            pnlLangShared.Cursor = Cursors.Hand;

            // Subscribe to language change event
            LanguageManager.Instance.LanguageChanged += () =>
            {
                lblLang.Text = LanguageManager.Instance.ToggleLabel;
                ApplyLanguage();
            };

            // 3. Setup Intro Panel (Mockup 1)
            SetupIntroPanel(assetsPath);

            // 4. Setup Footer Panel
            SetupFooterPanel();

            // 5. Glassmorphism Login Card Panel (Hidden default)
            cardPanel = new Guna2Panel
            {
                Size = new Size(500, 650),
                FillColor = Color.FromArgb(210, 206, 219, 192),
                BorderRadius = 25,
                BackColor = Color.Transparent,
                // Removed UseTransparentBackground = true; because it causes the 4 corner gray pixels bug when parent background changes.
                ShadowDecoration = { Enabled = false },
                Visible = false
            };
            this.Controls.Add(cardPanel);
            cardPanel.BringToFront();

            // 5. Title, Subtitle, Text, Forget Password, Divider And Logos are PAINTED manually for true Glassmorphism transparency.
            string iconCardPath = Path.Combine(assetsPath, "images", "Icon.png");
            string tenPmCardPath = Path.Combine(assetsPath, "images", "TenPhanMem.png");
            if (File.Exists(iconCardPath)) _loginCardIconImg = Image.FromFile(iconCardPath);
            if (File.Exists(tenPmCardPath)) _loginCardTenPhanMemImg = Image.FromFile(tenPmCardPath);

            cardPanel.Paint += CardPanel_Paint;
            cardPanel.MouseClick += CardPanel_MouseClick;
            cardPanel.MouseMove += (s, e) => {
                bool wasHovered = _forgotHovered;
                _forgotHovered = _forgotRect.Contains(e.Location);
                cardPanel.Cursor = _forgotHovered ? Cursors.Hand : Cursors.Default;
                if (wasHovered != _forgotHovered) cardPanel.Invalidate(_forgotRect);
            };
            cardPanel.MouseLeave += (s, e) => {
                if (_forgotHovered)
                {
                    _forgotHovered = false;
                    cardPanel.Invalidate(_forgotRect);
                }
            };

            // 6. Username Input
            txtUsername = new Guna2TextBox
            {
                PlaceholderText = LanguageManager.Instance.Get("login_username_placeholder"),
                PlaceholderForeColor = Color.FromArgb(160, 180, 150),
                BorderRadius = 15,
                Size = new Size(400, 50),
                Location = new Point(50, 190),
                Font = new Font("Segoe UI", 11),
                FillColor = Color.FromArgb(64, 88, 60), // Nền xanh đậm
                ForeColor = Color.White,
                BorderThickness = 0,
                BackColor = Color.Transparent // Loại bỏ nền thừa ở 4 góc bo tròn
            };
            cardPanel.Controls.Add(txtUsername);

            // 7. Password Input
            txtPassword = new Guna2TextBox
            {
                PlaceholderText = LanguageManager.Instance.Get("login_password_placeholder"),
                PlaceholderForeColor = Color.FromArgb(160, 180, 150),
                BorderRadius = 15,
                Size = new Size(400, 50),
                Location = new Point(50, 290),
                Font = new Font("Segoe UI", 11),
                FillColor = Color.FromArgb(64, 88, 60),
                ForeColor = Color.White,
                PasswordChar = '●',
                BorderThickness = 0,
                BackColor = Color.Transparent // Loại bỏ nền thừa ở 4 góc bo tròn
            };
            cardPanel.Controls.Add(txtPassword);

            // 7b. Toggle show/hide password icon
            string eyeOpenPath = Path.Combine(assetsPath, "images", "eye-solid-full.png");
            string eyeClosedPath = Path.Combine(assetsPath, "images", "eye-slash-solid (1).png");
            if (File.Exists(eyeOpenPath)) _eyeOpenImg = TintImage(Image.FromFile(eyeOpenPath), Color.FromArgb(180, 200, 170));
            if (File.Exists(eyeClosedPath)) _eyeClosedImg = TintImage(Image.FromFile(eyeClosedPath), Color.FromArgb(180, 200, 170));

            // Đặt PictureBox toggle trên cardPanel, overlay lên góc phải của txtPassword
            var pbTogglePassword = new PictureBox
            {
                Size = new Size(24, 24),
                Location = new Point(
                    txtPassword.Left + txtPassword.Width - 38,
                    txtPassword.Top + (txtPassword.Height - 24) / 2),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(64, 88, 60),
                Cursor = Cursors.Hand,
                Image = _eyeClosedImg
            };
   
            pbTogglePassword.MouseEnter += (s, ev) =>
            {
                pbTogglePassword.Image = _passwordVisible
                    ? TintImage(Image.FromFile(eyeOpenPath), Color.White)
                    : TintImage(Image.FromFile(eyeClosedPath), Color.White);
                pbTogglePassword.BackColor = Color.FromArgb(80, 108, 74);
            };
            pbTogglePassword.MouseLeave += (s, ev) =>
            {
                pbTogglePassword.Image = _passwordVisible ? _eyeOpenImg : _eyeClosedImg;
                pbTogglePassword.BackColor = Color.FromArgb(64, 88, 60);
            };
            pbTogglePassword.Click += (s, ev) =>
            {
                _passwordVisible = !_passwordVisible;
                if (_passwordVisible)
                {
                    txtPassword.PasswordChar = '\0';
                    pbTogglePassword.Image = TintImage(Image.FromFile(eyeOpenPath), Color.White);
                }
                else
                {
                    txtPassword.PasswordChar = '●';
                    pbTogglePassword.Image = TintImage(Image.FromFile(eyeClosedPath), Color.White);
                }
            };
            cardPanel.Controls.Add(pbTogglePassword);
            pbTogglePassword.BringToFront();

            // 8. Login Submit Button
            btnLoginSubmit = new Guna2Button
            {
                Text = LanguageManager.Instance.Get("login_submit"),
                BorderRadius = 15,
                Size = new Size(400, 55),
                Location = new Point(50, 400),
                FillColor = Color.FromArgb(145, 175, 100),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 46, 26),
                HoverState = { FillColor = Color.FromArgb(130, 160, 80) },
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnLoginSubmit.Click += BtnLoginSubmit_Click;
            cardPanel.Controls.Add(btnLoginSubmit);

            // 9. FaceID Button
            btnFaceID = new Guna2Button
            {
                Text = LanguageManager.Instance.Get("login_faceid"),
                BorderRadius = 15,
                Size = new Size(400, 55),
                Location = new Point(50, 520),
                FillColor = Color.FromArgb(145, 175, 100),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 46, 26),
                HoverState = { FillColor = Color.FromArgb(130, 160, 80) },
                Cursor = Cursors.Hand,
                BackColor = Color.Transparent
            };
            btnFaceID.Click += BtnFaceID_Click;
            cardPanel.Controls.Add(btnFaceID);

            // 10. Dragging functionality
            dragControl = new Guna2DragControl
            {
                TargetControl = this,
                TransparentWhileDrag = true,
                UseTransparentDrag = true
            };

            CenterCard();

            // Cho phép nhấn Enter để đăng nhập
            txtUsername.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BtnLoginSubmit_Click(this, EventArgs.Empty); } };
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BtnLoginSubmit_Click(this, EventArgs.Empty); } };
        }

        private async void CardPanel_MouseClick(object sender, MouseEventArgs e)
        {
            Rectangle forgotRect = new Rectangle(50, 350, 150, 20);
            if (forgotRect.Contains(e.Location))
            {
                await Helpers.FormTransitionHelper.PanelSwitchAsync(this, () => {
                    cardPanel.Visible = false;
                    ShowForgotCard();
                });
            }
        }

        private void CardPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            var LM = LanguageManager.Instance;

            // Title
            using (Font fontTitle = new Font("Segoe UI", 24, FontStyle.Bold))
            using (Brush brushTitle = new SolidBrush(Color.FromArgb(24, 46, 26)))
            {
                string titleText = LM.Get("login_title");
                var size = e.Graphics.MeasureString(titleText, fontTitle);
                e.Graphics.DrawString(titleText, fontTitle, brushTitle, (cardPanel.Width - size.Width) / 2, 50);
            }

            // Subtitle
            using (Font fontSub = new Font("Segoe UI", 11, FontStyle.Italic))
            using (Brush brushSub = new SolidBrush(Color.FromArgb(40, 60, 40)))
            {
                string text = $"\"{ LM.Get("login_subtitle")}\"";
                var size = e.Graphics.MeasureString(text, fontSub);
                e.Graphics.DrawString(text, fontSub, brushSub, (cardPanel.Width - size.Width) / 2, 100);
            }

            // Labels for Inputs
            using (Font fontLbl = new Font("Segoe UI", 12, FontStyle.Bold))
            using (Brush brushLbl = new SolidBrush(Color.FromArgb(24, 46, 26)))
            {
                string accLabel = LM.IsVietnamese ? "Tài khoản" : "Account";
                string pwdLabel = LM.IsVietnamese ? "Mật khẩu" : "Password";
                e.Graphics.DrawString(accLabel, fontLbl, brushLbl, 50, 160);
                e.Graphics.DrawString(pwdLabel, fontLbl, brushLbl, 50, 260);
            }

            var forgotStyle = _forgotHovered ? FontStyle.Underline : FontStyle.Regular;
            var forgotColor = _forgotHovered ? Color.FromArgb(145, 175, 100) : Color.FromArgb(74, 92, 72);
            using (Font fontForgot = new Font("Segoe UI", 9, forgotStyle))
            using (Brush brushForgot = new SolidBrush(forgotColor))
            {
                e.Graphics.DrawString(LM.Get("login_forgot"), fontForgot, brushForgot, 50, 350);
            }

            using (Pen penDiv = new Pen(Color.FromArgb(100, 120, 100), 1))
            {
                e.Graphics.DrawLine(penDiv, 50, 480, 200, 480);
                e.Graphics.DrawLine(penDiv, 300, 480, 450, 480);
            }
            using (Font fontOr = new Font("Segoe UI", 9))
            using (Brush brushOr = new SolidBrush(Color.FromArgb(40, 60, 40)))
            {
                e.Graphics.DrawString(LM.Get("login_or"), fontOr, brushOr, 225, 470);
            }

            if (!string.IsNullOrEmpty(_loginStatusMessage))
            {
                using (Font fontErr = new Font("Segoe UI", 9, FontStyle.Italic))
                using (Brush brushErr = new SolidBrush(_loginStatusColor))
                {
                    var size = e.Graphics.MeasureString(_loginStatusMessage, fontErr);
                    e.Graphics.DrawString(_loginStatusMessage, fontErr, brushErr, (cardPanel.Width - size.Width) / 2, 375);
                }
            }

            if (_loginCardIconImg != null)
            {
                e.Graphics.DrawImage(_loginCardIconImg, 15, 600, 35, 35);
            }
            if (_loginCardTenPhanMemImg != null)
            {
                e.Graphics.DrawImage(_loginCardTenPhanMemImg, 55, 600, 100, 35);
            }
        }

        private void SetupIntroPanel(string assetsPath)
        {
            pnlIntro = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            this.Controls.Add(pnlIntro);

            string introImagesPath = Path.Combine(assetsPath, "images");

            string iconPath = Path.Combine(introImagesPath, "Icon.png");
            string tenPhanMemPath = Path.Combine(introImagesPath, "TenPhanMem.png");
            
            var pnlLogo = new FlowLayoutPanel
            {
                Location = new Point(30, 25),
                Size = new Size(150, 44),
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(0),
                AutoSize = false
            };

            var pbIcon = new Guna2PictureBox
            {
                Size = new Size(40, 40),
                Margin = new Padding(0, 0, 2, 0),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            if (File.Exists(iconPath)) pbIcon.Image = Image.FromFile(iconPath);
            pnlLogo.Controls.Add(pbIcon);

            var pbTenPhanMem = new Guna2PictureBox
            {
                Size = new Size(100, 40),
                Margin = new Padding(0),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            if (File.Exists(tenPhanMemPath)) pbTenPhanMem.Image = Image.FromFile(tenPhanMemPath);
            pnlLogo.Controls.Add(pbTenPhanMem);
            pnlIntro.Controls.Add(pnlLogo);

            string logoTrangPath = Path.Combine(introImagesPath, "TenPhanMem2.png");
            _footerLogoPath = logoTrangPath;

            var pnlCenter = new Guna2Panel
            {
                Size = new Size(1100, 580),
                Anchor = AnchorStyles.None,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            pnlCenter.Location = new Point((pnlIntro.Width - pnlCenter.Width) / 2, (pnlIntro.Height - pnlCenter.Height) / 2 - 20);
            pnlIntro.Controls.Add(pnlCenter);

            var lblHero1 = new Label
            {
                Name = "lblHero1",
                Text = LanguageManager.Instance.Get("login_hero1"),
                Font = new Font("Segoe UI", 28, FontStyle.Bold),
                ForeColor = Color.FromArgb(145, 175, 100), //xanh lá nhạt 
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = pnlCenter.Width,
                Height = 60,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 0)
            };
            
            var lblHero2 = new Label
            {
                Name = "lblHero2",
                Text = LanguageManager.Instance.Get("login_hero2"),
                Font = new Font("Segoe UI", 32, FontStyle.Bold),
                ForeColor = Color.FromArgb(49, 87, 44),
                BackColor = Color.Transparent,
                AutoSize = false,
                Width = pnlCenter.Width,
                Height = 70,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 60)
            };

            pnlCenter.Controls.Add(lblHero1);
            pnlCenter.Controls.Add(lblHero2);

            btnOpenLogin = new Guna2Button
            {
                Text = LanguageManager.Instance.Get("login_intro_btn"),
                BorderRadius = 25,
                Size = new Size(300, 60),
                FillColor = Color.FromArgb(236, 243, 158),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.Black,
                Cursor = Cursors.Hand
            };
            btnOpenLogin.Location = new Point((pnlCenter.Width - btnOpenLogin.Width) / 2, 160);
            btnOpenLogin.Click += async (s, e) => { 
                await Helpers.FormTransitionHelper.PanelSwitchAsync(this, () => {
                    pnlIntro.Visible = false; 
                    pnlFooter.Visible = false;
                    
                    // Set Background form màn hình Login mới
                    if (_loginBgImage != null) this.BackgroundImage = _loginBgImage;
                    
                    btnBackToHome.Visible = true;
                    btnBackToHome.BringToFront();

                    // Dọn dẹp trạng thái lỗi cũ (nếu có)
                    SetLoginStatus("", Color.Transparent);
                    if (txtUsername != null) txtUsername.Text = "";
                    if (txtPassword != null) txtPassword.Text = "";

                    cardPanel.Visible = true; 
                    cardPanel.BringToFront(); 
                });
            };
            pnlCenter.Controls.Add(btnOpenLogin);

            // Setup 3 Features Cards
            int cardW = 340; int cardH = 350; int spacing = 20; // Tăng chiều cao thẻ thêm 40px
            int totalW = (cardW * 3) + (spacing * 2);
            int startX = (pnlCenter.Width - totalW) / 2;
            int cardY = 260; // Nằm cách button 40px

            Guna2Panel card1 = CreateFeatureCard("login_feat1_title1", "login_feat1_title2", "login_feat1_desc", "approve-folder-29.png", cardW, cardH, introImagesPath);
            card1.Location = new Point(startX, cardY);
            pnlCenter.Controls.Add(card1);

            Guna2Panel card2 = CreateFeatureCard("login_feat2_title1", "login_feat2_title2", "login_feat2_desc", "document-history-30.png", cardW, cardH, introImagesPath);
            card2.Location = new Point(startX + cardW + spacing, cardY);
            pnlCenter.Controls.Add(card2);

            Guna2Panel card3 = CreateFeatureCard("login_feat3_title1", "login_feat3_title2", "login_feat3_desc", "analytics-22.png", cardW, cardH, introImagesPath);
            card3.Location = new Point(startX + (cardW + spacing) * 2, cardY);
            pnlCenter.Controls.Add(card3);
        }

        private Guna2Panel CreateFeatureCard(string title1, string title2, string desc, string iconFilename, int width, int height, string assetsPath)
        {
            var pnl = new Guna2Panel
            {
                Size = new Size(width, height),
                FillColor = Color.FromArgb(178, 144, 169, 85), // ~70% alpha green
                BorderRadius = 15,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };

            // Icon: increased to 110x110
            var pbIcon = new Guna2PictureBox
            {
                Size = new Size(110, 110),
                Location = new Point(22, 15),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            string iconPath = Path.Combine(assetsPath, iconFilename);
            if (File.Exists(iconPath)) pbIcon.Image = Image.FromFile(iconPath);
            pnl.Controls.Add(pbIcon);

            // Draw text via Paint to avoid WinForms label transparent background issues.
            pnl.Paint += (s, e) =>
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                int padX = 22;
                int textW = width - padX * 2;

                var LM = LanguageManager.Instance;
                // Title 1
                using (Font fontTitle = new Font("Segoe UI", 14, FontStyle.Bold))
                using (Brush brushTitle = new SolidBrush(Color.FromArgb(15, 15, 15)))
                {
                    var sf = new System.Drawing.StringFormat { FormatFlags = 0 };
                    e.Graphics.DrawString(LM.Get(title1), fontTitle, brushTitle, new RectangleF(padX, 135, textW, 32), sf);
                }

                // Title 2
                using (Font fontTitle2 = new Font("Segoe UI", 14, FontStyle.Bold))
                using (Brush brushTitle2 = new SolidBrush(Color.FromArgb(236, 243, 158)))
                {
                    var sf = new System.Drawing.StringFormat { FormatFlags = 0 };
                    e.Graphics.DrawString(LM.Get(title2), fontTitle2, brushTitle2, new RectangleF(padX, 165, textW, 32), sf);
                }

                // Description
                using (Font fontDesc = new Font("Segoe UI", 9.5f)) 
                using (Brush brushDesc = new SolidBrush(Color.FromArgb(15, 15, 15)))
                {
                    var sf = new System.Drawing.StringFormat { Trimming = StringTrimming.EllipsisWord };
                    e.Graphics.DrawString(LM.Get(desc), fontDesc, brushDesc, new RectangleF(padX, 205, textW, height - 215), sf);
                }
            };

            return pnl;
        }

        private void SetupFooterPanel()
        {
            // Footer height = 110px to accommodate: logo (60px) + copyright (20px) + padding
            const int footerH = 110;
            pnlFooter = new Guna2Panel
            {
                Dock = DockStyle.Bottom,
                Height = footerH,
                FillColor = Color.FromArgb(19, 42, 19)
            };
            this.Controls.Add(pnlFooter);

            // ── LEFT SIDE: Logo + copyright stacked ──────────────────────────────
            var pbFooterLogo = new Guna2PictureBox
            {
                Size = new Size(130, 55),
                Location = new Point(24, 12),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            if (!string.IsNullOrEmpty(_footerLogoPath) && File.Exists(_footerLogoPath))
                pbFooterLogo.Image = Image.FromFile(_footerLogoPath);
            pnlFooter.Controls.Add(pbFooterLogo);

            var lblCopy = new Label
            {
                Text = "copyright @2026 EcovaGroup",
                ForeColor = Color.FromArgb(236, 243, 158),
                Font = new Font("Segoe UI", 8),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            pnlFooter.Controls.Add(lblCopy);
            // Place below the logo with a small gap
            lblCopy.Location = new Point(24, pbFooterLogo.Bottom + 4);

            // ── RIGHT SIDE: Contact info, vertically centered ──────────────────
            var lblContact = new Label
            {
                Name = "lblContact",
                Text = LanguageManager.Instance.Get("login_footer_contact"),
                ForeColor = Color.FromArgb(236, 243, 158),
                Font = new Font("Segoe UI", 9),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            pnlFooter.Controls.Add(lblContact);
            lblContact.Location = new Point(this.ClientSize.Width - lblContact.PreferredWidth - 30,
                                           (footerH - lblContact.PreferredHeight) / 2);

            this.Resize += (s, e) =>
            {
                if (lblContact != null)
                    lblContact.Left = this.ClientSize.Width - lblContact.Width - 30;
            };
        }

        private void CenterCard()
        {
            if (cardPanel != null)
            {
                cardPanel.Left = (this.ClientSize.Width - cardPanel.Width) / 2;
                cardPanel.Top = (this.ClientSize.Height - cardPanel.Height) / 2;
            }
            if (pnlFaceID != null)
            {
                pnlFaceID.Left = (this.ClientSize.Width - pnlFaceID.Width) / 2;
                pnlFaceID.Top = (this.ClientSize.Height - pnlFaceID.Height) / 2;
            }
            if (pnlForgot != null)
            {
                pnlForgot.Left = (this.ClientSize.Width - pnlForgot.Width) / 2;
                pnlForgot.Top = (this.ClientSize.Height - pnlForgot.Height) / 2;
            }
        }

        private string GetAssetsPath()
        {
            string baseDir = Application.StartupPath;

            // 1. Khi đã cài đặt (published) — assets nằm ngay trong thư mục ứng dụng
            string installedPath = Path.Combine(baseDir, "assets");
            if (Directory.Exists(installedPath))
                return installedPath;

            // 2. Khi chạy từ IDE (debug) — assets nằm ở thư mục gốc Solution
            return Path.Combine(baseDir, @"..\..\..\..\assets");
        }

        public void ShowLoginCard()
        {
            pnlIntro.Visible = false;
            pnlFooter.Visible = false;
            if (_loginBgImage != null) this.BackgroundImage = _loginBgImage;
            btnBackToHome.Visible = true;
            btnBackToHome.BringToFront();
            cardPanel.Visible = true;
            cardPanel.BringToFront();
            SetLoginStatus("", Color.Transparent);
            txtUsername.Text = "";
            txtPassword.Text = "";
            if (btnLoginSubmit != null) btnLoginSubmit.Enabled = true;
            if (btnFaceID != null) btnFaceID.Enabled = true;
        }

        public void ShowIntroPanel()
        {
            cardPanel.Visible = false;
            btnBackToHome.Visible = false;
            pnlIntro.Visible = true;
            pnlFooter.Visible = true;
            if (_introBgImage != null) this.BackgroundImage = _introBgImage;

            SetLoginStatus("", Color.Transparent);
            txtUsername.Text = "";
            txtPassword.Text = "";
            if (btnLoginSubmit != null) btnLoginSubmit.Enabled = true;
            if (btnFaceID != null) btnFaceID.Enabled = true;
        }

        private async void BtnFaceID_Click(object sender, EventArgs e)
        {
            await Helpers.FormTransitionHelper.PanelSwitchAsync(this, () =>
            {
                cardPanel.Visible = false;
                ShowFacePanel(); // Login.FaceID.cs
            });
        }


        // FaceID fields, panel, camera → Login.FaceID.cs (partial class)
        // ForgotPassword fields, panel, flow → Login.ForgotPassword.cs (partial class)
        // DNN image processing (ExtractNormalizedFace, CompareHistogram) → FaceIdManager.cs

        // ── Tất cả FaceID + ForgotPassword + DNN logic đã được tách sang partial files ──
        // Login.FaceID.cs         → camera, panel, register, auto-scan, failed options
        // Login.ForgotPassword.cs → OTP email, countdown, 3-step reset flow
        // FaceIdManager.cs        → DNN detect, ExtractNormalizedFace, CompareHistogram



        private async void BtnLoginSubmit_Click(object sender, EventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetLoginStatus(LanguageManager.Instance.Get("login_error_empty"), Color.Red);
                return;
            }

            // ── Brute-force protection ────────────────────────────────────────
            if (Services.LoginThrottleService.IsLockedOut(username, out var remaining))
            {
                var LM = LanguageManager.Instance;
                string time = remaining.TotalMinutes >= 1
                    ? string.Format(LM.Get("login_minutes"), (int)remaining.TotalMinutes, remaining.Seconds)
                    : string.Format(LM.Get("login_seconds"), remaining.Seconds);

                SetLoginStatus(string.Format(LM.Get("login_locked_out"), time), Color.Red);
                AppLogger.Warning($"Login: Tài khoản '{username}' đang bị lockout, còn {time}");
                return;
            }

            AppLogger.Info($"Login: Đang đăng nhập với tài khoản '{username}'");
            btnLoginSubmit.Enabled = false;
            SetLoginStatus(LanguageManager.Instance.Get("login_authenticating"), Color.FromArgb(0, 123, 255));

            try
            {
                bool success = await _userBLL.LoginAsync(username, password);

                if (success)
                {
                    Services.LoginThrottleService.RecordSuccess(username);
                    AppLogger.Info($"Login: Đăng nhập thành công — user='{username}' name='{AppState.Instance.CurrentUser?.FullName}'");
                    SetLoginStatus($"{LanguageManager.Instance.Get("login_success")} {AppState.Instance.CurrentUser?.FullName}", Color.FromArgb(40, 167, 69));
                    await System.Threading.Tasks.Task.Delay(1000);
                    var mainForm = Program.ServiceProvider.GetRequiredService<Forms.Main.MainForm>();
                    mainForm.FormClosed += (s, args) => { if (!mainForm.IsLoggingOut) this.Close(); };
                    await Helpers.FormTransitionHelper.TransitionAsync(this, mainForm, closeFrom: false);
                }
                else
                {
                    var LM = LanguageManager.Instance;
                    Services.LoginThrottleService.RecordFailure(username);
                    int remaining2 = Services.LoginThrottleService.RemainingAttempts(username);
                    AppLogger.Warning($"Login: Sai tài khoản/mật khẩu cho '{username}' — còn {remaining2} lần thử");
                    
                    string hint = remaining2 > 0
                        ? string.Format(LM.Get("login_remaining_attempts"), remaining2)
                        : LM.Get("login_account_locked_temp");

                    SetLoginStatus($"{LM.Get("login_fail")}{hint}", Color.Red);
                    btnLoginSubmit.Enabled = true;
                }
            }
            catch (System.Exception ex)
            {
                AppLogger.Error("Login: Lỗi kết nối máy chủ", ex);
                SetLoginStatus(LanguageManager.Instance.Get("login_server_error"), Color.Red);
                btnLoginSubmit.Enabled = true;
            }
        }


        /// <summary>
        /// Tint ảnh: đổi tất cả pixel không trong suốt sang màu chỉ định, giữ nguyên alpha.
        /// Dùng để đổi icon đen thành trắng/sáng cho phù hợp nền tối.
        /// </summary>
        private static Image TintImage(Image source, Color tint)
        {
            var bmp = new Bitmap(source.Width, source.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                // Vẽ ảnh gốc
                g.DrawImage(source, 0, 0, source.Width, source.Height);
            }
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    var px = bmp.GetPixel(x, y);
                    if (px.A > 0) // Chỉ tô màu pixel không trong suốt
                    {
                        bmp.SetPixel(x, y, Color.FromArgb(px.A, tint.R, tint.G, tint.B));
                    }
                }
            }
            return bmp;
        }

        /// <summary>Cập nhật toàn bộ text khi chuyển ngôn ngữ.</summary>
        private void ApplyLanguage()
        {
            var LM = LanguageManager.Instance;
            this.Text = LM.Get("form_title");
            if (btnBackToHome != null) btnBackToHome.Text = LM.Get("login_back");
            if (txtUsername != null) txtUsername.PlaceholderText = LM.Get("login_username_placeholder");
            if (txtPassword != null) txtPassword.PlaceholderText = LM.Get("login_password_placeholder");
            if (btnLoginSubmit != null) btnLoginSubmit.Text = LM.Get("login_submit");
            if (btnFaceID != null) btnFaceID.Text = LM.Get("login_faceid");
            if (btnOpenLogin != null) btnOpenLogin.Text = LM.Get("login_intro_btn");
            if (cardPanel != null) cardPanel.Invalidate(); // Repaint custom-drawn text

            if (pnlIntro != null)
            {
                var h1 = pnlIntro.Controls.Find("lblHero1", true).FirstOrDefault() as Label;
                if (h1 != null) h1.Text = LM.Get("login_hero1");
                var h2 = pnlIntro.Controls.Find("lblHero2", true).FirstOrDefault() as Label;
                if (h2 != null) h2.Text = LM.Get("login_hero2");
                pnlIntro.Invalidate(true);
            }
            if (pnlFooter != null)
            {
                var lblC = pnlFooter.Controls.Find("lblContact", true).FirstOrDefault() as Label;
                if (lblC != null) lblC.Text = LM.Get("login_footer_contact");
            }
            if (pnlFaceID != null)
            {
                // lblFaceTitle might change between steps, so only update if it matches the generic faceid title
                if (lblFaceTitle?.Text == "FACE ID" || lblFaceTitle?.Text == "FACE ID X" || lblFaceTitle?.Text == "ĐĂNG NHẬP FACE ID" || lblFaceTitle?.Text == "FACE ID LOGIN" || lblFaceTitle?.Text == "ĐĂNG KÝ FACE ID" || lblFaceTitle?.Text == "FACE ID REGISTRATION")
                {
                    // leave it, or we could update it but we don't know the state perfectly.
                    // Actually, the user can just re-enter FaceID. But I can update lblFaceUN and txtFaceUsername.
                }
                var lblUN = pnlFaceID.Controls.Find("lblFaceUN", true).FirstOrDefault() as Label;
                if (lblUN != null) lblUN.Text = LM.Get("login_faceid_un");
                if (txtFaceUsername != null) txtFaceUsername.PlaceholderText = LM.Get("login_faceid_un_ph");

                if (lblFaceStatus != null && (lblFaceStatus.Text == "Nhập Username để tiếp tục tiếp tục." || lblFaceStatus.Text == "Enter username to continue." || lblFaceStatus.Text == "Nhập Username để tiếp tục."))
                {
                    lblFaceStatus.Text = LM.Get("login_faceid_status_input");
                }

                var btnFaceBack = pnlFaceID.Controls.OfType<Guna2Button>().FirstOrDefault(b => b.Text == "Quay lại" || b.Text == "Back");
                if (btnFaceBack != null) btnFaceBack.Text = LM.Get("faceid_btn_back");
            }
            if (pnlFaceDynamic != null)
            {
                var btnFaceCont = pnlFaceDynamic.Controls.Find("btnFaceContinue", true).FirstOrDefault() as Guna2Button;
                if (btnFaceCont != null) btnFaceCont.Text = LM.Get("faceid_btn_continue");
            }
            ApplyLanguageForgot();
        }

    }
}
