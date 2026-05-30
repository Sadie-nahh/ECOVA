using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using EnvContract.BLL.Interfaces;
using EnvContract.DTO;
using EnvContract.GUI.Helpers;
using EnvContract.GUI.Services;
using Guna.UI2.WinForms;
using Microsoft.Extensions.DependencyInjection;
using Timer = System.Windows.Forms.Timer;

namespace EnvContract.GUI.UserControls.Notification
{
    public partial class NotificationUC : UserControl
    {
        private readonly INotificationService _notificationService;
        private readonly VoiceSearchService _voiceService;
        private Image _watermarkImage;
        private Bitmap _cachedWatermark;
        private List<NotificationDTO> _cachedNotifications;
        private Action _langHandler;
        private Timer _searchTimer;

        public NotificationUC(INotificationService notificationService)
        {
            InitializeComponent();
            _notificationService = notificationService;
            _voiceService = Program.ServiceProvider?.GetService<VoiceSearchService>();

            // Gắn nút micro vào txtSearch (được tạo bởi Designer)
            this.Load += (s, e) => VoiceSearchHelper.AttachVoiceButton(txtSearch, pnlHeader, _voiceService,
                () =>
                {
                    if (flpNotifications == null || flpNotifications.Controls.Count == 0) return null;
                    var sb = new System.Text.StringBuilder();
                    foreach (Control card in flpNotifications.Controls)
                    {
                        // card là Guna2Panel → Label đầu tiên = Title, Label thứ 2 = Code
                        foreach (Control child in card.Controls)
                        {
                            if (child is Label lbl && !string.IsNullOrWhiteSpace(lbl.Text) && lbl.Text.Length > 2)
                            {
                                if (sb.Length + lbl.Text.Length + 2 > 200) break;
                                if (sb.Length > 0) sb.Append(", ");
                                sb.Append(lbl.Text);
                                break; // chỉ lấy Title (Label đầu tiên)
                            }
                        }
                        if (sb.Length >= 200) break;
                    }
                    return sb.Length > 0 ? sb.ToString() : null;
                });

            // Search filter with debounce
            _searchTimer = new Timer { Interval = 300 };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                PerformSearch();
            };

            txtSearch.TextChanged += (s, e) =>
            {
                _searchTimer.Stop();
                _searchTimer.Start();
            };

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            try
            {
                string logoPath = FindAssetPath("Icon.png");
                if (!string.IsNullOrEmpty(logoPath))
                    _watermarkImage = Image.FromFile(logoPath);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[NotificationUC] Error: {ex.Message}"); }

            _langHandler = () =>
            {
                if (this.IsDisposed) return;
                PerformSearch();
                var LM = EnvContract.Common.LanguageManager.Instance;
                txtSearch.PlaceholderText = LM.Get("notification_search");
            };
            EnvContract.Common.LanguageManager.Instance.LanguageChanged += _langHandler;
            this.Disposed += (s, e) => { EnvContract.Common.LanguageManager.Instance.LanguageChanged -= _langHandler; };

            // Áp dụng ngôn ngữ hiện tại ngay khi khởi tạo (không đợi LanguageChanged)
            txtSearch.PlaceholderText = EnvContract.Common.LanguageManager.Instance.Get("notification_search");
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

        private async void NotificationUC_Load(object sender, EventArgs e)
        {
            try
            {
                // Tải danh sách thông báo
                _cachedNotifications = await _notificationService.GetNotifications();
                RenderNotifications(_cachedNotifications);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Có lỗi xảy ra: " + ex.Message);
            }
        }

        /// <summary>
        /// Tính chiều rộng 1 card sao cho 2 cards vừa 1 hàng.
        /// </summary>
        private int GetCardWidth()
        {
            int available = flpNotifications.ClientSize.Width
                            - flpNotifications.Padding.Left
                            - flpNotifications.Padding.Right
                            - SystemInformation.VerticalScrollBarWidth;
            int cardMarginH = 12; // Margin left + right per card
            int cardWidth = (available - cardMarginH * 2) / 2;
            return Math.Max(cardWidth, 300);
        }

        private void PerformSearch()
        {
            if (_cachedNotifications == null) return;
            var query = txtSearch.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query))
            {
                RenderNotifications(_cachedNotifications);
            }
            else
            {
                var filtered = _cachedNotifications.Where(n =>
                {
                    // Tạo bản dịch ảo sang TA để có thể tìm theo cả 2 ngôn ngữ
                    string enTitle = n.Title.Replace("Hợp đồng trễ hạn:", "Overdue contract:")
                                            .Replace("Hợp đồng sắp hết hạn:", "Expiring contract:");
                    
                    return n.Title.ToLower().Contains(query) ||
                           enTitle.ToLower().Contains(query) ||
                           n.Code.ToLower().Contains(query);
                }).ToList();
                RenderNotifications(filtered);
            }
        }

        private void RenderNotifications(List<NotificationDTO> notifications)
        {
            flpNotifications.SuspendLayout();
            flpNotifications.Controls.Clear();

            var LM = EnvContract.Common.LanguageManager.Instance;
            int cardWidth = GetCardWidth();

            foreach (var dto in notifications)
            {
                bool isOverdue = dto.OverdueDays > 0;

                var card = new Guna2Panel
                {
                    Width        = cardWidth,
                    Height       = 110,
                    BorderRadius = 12,
                    FillColor    = Color.FromArgb(232, 243, 214),
                    Margin       = new Padding(6, 0, 6, 12)
                };

                // ── Cột TRÁI trong card: tiêu đề + mã HĐ ────────────────
                int leftColWidth = (int)(cardWidth * 0.52);

                var lblTitle = new Label
                {
                    Text        = LM.IsEnglish 
                                    ? dto.Title.Replace("Hợp đồng trễ hạn:", "Overdue contract:")
                                               .Replace("Hợp đồng sắp hết hạn:", "Expiring contract:")
                                    : dto.Title,
                    Font        = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    ForeColor   = Color.FromArgb(30, 70, 30),
                    Location    = new Point(16, 12),
                    AutoSize    = true,
                    BackColor   = Color.Transparent,
                    MaximumSize = new Size(leftColWidth - 20, 0)
                };

                var lblCode = new Label
                {
                    Text      = string.Format(LM.Get("notification_code"), dto.Code),
                    Font      = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(80, 80, 80),
                    Location  = new Point(16, 70),
                    AutoSize  = true,
                    BackColor = Color.Transparent
                };

                // ── Cột PHẢI trong card: ngày ký + ngày dự kiến + badge ──
                int rightX = leftColWidth + 10;

                var lblSignedDate = new Label
                {
                    Text      = string.Format(LM.Get("notification_signed"), dto.SignedDate.ToString("dd/MM/yyyy")),
                    Font      = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(60, 60, 60),
                    Location  = new Point(rightX, 10),
                    AutoSize  = true,
                    BackColor = Color.Transparent
                };

                var lblExpectedDate = new Label
                {
                    Text      = string.Format(LM.Get("notification_expected"), dto.ExpectedDate.ToString("dd/MM/yyyy")),
                    Font      = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                    ForeColor = Color.FromArgb(60, 60, 60),
                    Location  = new Point(rightX, 34),
                    AutoSize  = true,
                    BackColor = Color.Transparent
                };

                var lblBadge = new Label
                {
                    Text      = isOverdue
                                    ? string.Format(LM.Get("notification_overdue"), dto.OverdueDays)
                                    : string.Format(LM.Get("notification_remaining"), Math.Max(0, (int)(dto.ExpectedDate - DateTime.Now).TotalDays)),
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = isOverdue ? Color.FromArgb(180, 0, 0) : Color.FromArgb(20, 110, 20),
                    BackColor = isOverdue ? Color.FromArgb(255, 220, 220) : Color.FromArgb(195, 235, 165),
                    Location  = new Point(rightX, 68),
                    AutoSize  = true,
                    Padding   = new Padding(6, 3, 6, 3)
                };

                card.Controls.Add(lblTitle);
                card.Controls.Add(lblCode);
                card.Controls.Add(lblSignedDate);
                card.Controls.Add(lblExpectedDate);
                card.Controls.Add(lblBadge);

                // Đường kẻ dọc ngăn cách 2 cột trong card
                int lineX = leftColWidth;
                card.Paint += (s, pe) =>
                {
                    using var pen = new Pen(Color.FromArgb(80, 100, 140, 80), 1);
                    pe.Graphics.DrawLine(pen, lineX, 10, lineX, 97);
                };

                flpNotifications.Controls.Add(card);
            }

            flpNotifications.ResumeLayout(true);
        }

        private void flpNotifications_Resize(object sender, EventArgs e)
        {
            int cardWidth = GetCardWidth();
            foreach (Control ctrl in flpNotifications.Controls)
            {
                if (ctrl is Guna2Panel panel)
                {
                    panel.Width = cardWidth;
                    // Cập nhật vị trí cột phải trong card
                    int leftColWidth = (int)(cardWidth * 0.52);
                    int rightX = leftColWidth + 10;
                    int labelIdx = 0;
                    foreach (Control child in panel.Controls)
                    {
                        if (child is Label lbl)
                        {
                            // Labels 0,1 = cột trái (title, code) → giữ nguyên
                            // Labels 2,3,4 = cột phải (signed, expected, badge) → di chuyển
                            if (labelIdx >= 2)
                                lbl.Location = new Point(rightX, lbl.Location.Y);
                            if (labelIdx == 0) // Title
                                lbl.MaximumSize = new Size(leftColWidth - 20, 0);
                            labelIdx++;
                        }
                    }
                    panel.Invalidate(); // Vẽ lại đường kẻ dọc
                }
            }
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

                    var cm = new ColorMatrix { Matrix33 = 0.08f };
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
    }
}
