using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace EnvContract.GUI.Helpers
{
    /// <summary>
    /// Loading overlay bán trong suốt phủ lên control cha khi đang chạy async.
    /// Hiển thị spinner animation + message — chặn user tương tác cho đến khi xong.
    ///
    /// Cách dùng:
    ///   using (LoadingOverlay.Show(parentControl, "Đang tải..."))
    ///   {
    ///       await SomeLongRunningTask();
    ///   }
    ///
    /// Hoặc:
    ///   var overlay = LoadingOverlay.Show(pnlContent, "Đang xử lý...");
    ///   try { await DoWork(); }
    ///   finally { overlay.Dispose(); }
    /// </summary>
    public class LoadingOverlay : IDisposable
    {
        private Panel _overlay;
        private Timer _animTimer;
        private int _angle = 0;
        private bool _disposed = false;

        private LoadingOverlay() { }

        /// <summary>
        /// Hiển thị loading overlay phủ lên parentControl.
        /// Trả về IDisposable — gọi Dispose() hoặc dùng using để ẩn.
        /// </summary>
        public static LoadingOverlay Show(Control parentControl, string message = null)
        {
            message ??= EnvContract.Common.LanguageManager.Instance.Get("msg_loading");
            var instance = new LoadingOverlay();
            instance.CreateOverlay(parentControl, message);
            return instance;
        }

        private void CreateOverlay(Control parent, string message)
        {
            _overlay = new Panel
            {
                Size = parent.ClientSize,
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(120, 0, 0, 0), // semi-transparent dark
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };

            // Custom paint: spinner + text
            _overlay.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                int centerX = _overlay.Width / 2;
                int centerY = _overlay.Height / 2;

                // ── Spinner (arc) ──────────────────────────────────────
                int spinnerSize = 40;
                var spinnerRect = new Rectangle(
                    centerX - spinnerSize / 2,
                    centerY - spinnerSize / 2 - 20,
                    spinnerSize, spinnerSize);

                using var pen = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, spinnerRect, _angle, 270);

                // ── Text ──────────────────────────────────────────────
                using var font = new Font("Segoe UI", 12f, FontStyle.Regular);
                using var brush = new SolidBrush(Color.White);
                var textSize = g.MeasureString(message, font);
                g.DrawString(message, font, brush,
                    centerX - textSize.Width / 2,
                    centerY + spinnerSize / 2);
            };

            parent.Controls.Add(_overlay);
            _overlay.BringToFront();

            // Animation timer: xoay spinner 30fps
            _animTimer = new Timer { Interval = 33 };
            _animTimer.Tick += (s, e) =>
            {
                _angle = (_angle + 10) % 360;
                if (!_disposed) _overlay?.Invalidate();
            };
            _animTimer.Start();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _animTimer?.Stop();
            _animTimer?.Dispose();

            if (_overlay != null)
            {
                var parent = _overlay.Parent;
                parent?.Controls.Remove(_overlay);
                _overlay.Dispose();
                _overlay = null;
            }
        }
    }
}
