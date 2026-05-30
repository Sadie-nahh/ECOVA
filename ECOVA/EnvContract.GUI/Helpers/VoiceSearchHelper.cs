using EnvContract.GUI.Forms;
using EnvContract.GUI.Services;
using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace EnvContract.GUI.Helpers
{
    /// <summary>
    /// Gắn nút micro overlay góc phải Guna2TextBox.
    /// 3 trạng thái:
    ///   🟢 Idle      — sẵn sàng
    ///   🔴 Recording — đang ghi âm (nhấn để dừng sớm)
    ///   🟡 Processing — Vosk đang xử lý (không thể tương tác)
    /// </summary>
    public static class VoiceSearchHelper
    {
        private static readonly Color ColorIdle       = Color.FromArgb(161, 185, 114); // olive-green
        private static readonly Color ColorRecording  = Color.FromArgb(220, 53, 69);   // red
        private static readonly Color ColorProcessing = Color.FromArgb(255, 165, 0);   // orange

        private static Image _cachedIcon;
        private static readonly object _iconLock = new();

        // ── State Machine ──────────────────────────────────────────────────

        /// <summary>
        /// 3 trạng thái rõ ràng của nút micro:
        ///   Idle       — sẵn sàng (olive-green)
        ///   Recording  — đang ghi âm (đỏ, pulse)
        ///   Processing — Vosk/WebSpeech đang xử lý (vàng, spinner)
        /// Thay thế bool isRunning (đơn tầng) bằng enum rõ ràng hơn.
        /// </summary>
        private enum VoiceButtonState { Idle, Recording, Processing }

        // ── Icon ────────────────────────────────────────────────────────────

        private static Image LoadIcon()
        {
            lock (_iconLock)
            {
                if (_cachedIcon != null) return _cachedIcon;
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (dir != null)
                {
                    foreach (var sub in new[] { "assets", "Assets" })
                    {
                        var p = Path.Combine(dir.FullName, sub, "images", "micro.png");
                        if (File.Exists(p))
                        {
                            try
                            {
                                // Dùng MemoryStream để không lock file trên disk.
                                // Image.FromFile() giữ file handle mở suốt vòng đời Image,
                                // gây lỗi khi file bị xóa/cập nhật trong quá trình chạy.
                                byte[] bytes = File.ReadAllBytes(p);
                                _cachedIcon = Image.FromStream(new MemoryStream(bytes));
                                return _cachedIcon;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[VoiceSearch] LoadIcon lỗi tại '{p}': {ex.Message}");
                            }
                        }
                    }
                    dir = dir.Parent;
                }
                return null;
            }
        }

        private static Image ResizeIcon(Image src, int size)
        {
            var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.SmoothingMode    = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, new Rectangle(0, 0, size, size));
            return bmp;
        }

        // ── PUBLIC API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Gắn nút voice search vào Guna2TextBox.
        /// contextProvider: trả về danh sách từ vựng phòng ban hiện tại
        /// (tên KH, mã HĐ...) để Vosk FuzzyMatch nhận đúng hơn. Null = không dùng.
        /// </summary>
        public static Guna2Button AttachVoiceButton(
            Guna2TextBox targetTextBox,
            Control parentContainer,
            VoiceSearchService voiceService,
            Func<string> contextProvider = null)
        {
            if (targetTextBox == null || parentContainer == null) return null;
            return AttachCore(targetTextBox, parentContainer, voiceService, contextProvider);
        }

        public static Guna2Button AttachVoiceButtonInPanel(
            Guna2TextBox targetTextBox,
            Panel containerPanel,
            VoiceSearchService voiceService,
            Func<string> contextProvider = null)
        {
            return AttachVoiceButton(targetTextBox, containerPanel, voiceService, contextProvider);
        }

        // ── CORE ────────────────────────────────────────────────────────────────

        private static Guna2Button AttachCore(
            Guna2TextBox txt,
            Control parent,
            VoiceSearchService voiceService,
            Func<string> contextProvider)
        {
            var icon = LoadIcon();

            // Kích thước ban đầu tạm — sẽ được tính lại sau khi layout xong
            const int DefaultBtnH = 32;
            int iconSz = DefaultBtnH - 8;

            var btn = new Guna2Button
            {
                BorderThickness          = 0,
                Size                     = new Size(DefaultBtnH, DefaultBtnH),
                BorderRadius             = DefaultBtnH / 2,
                FillColor                = ColorIdle,
                ForeColor                = Color.White,
                Text                     = (icon == null) ? "🎤" : string.Empty,
                Font                     = new Font("Segoe UI Emoji", 12),
                Cursor                   = Cursors.Hand,
                Animated                 = false,
                Tag                      = "idle",
                Visible                  = false,   // Ẩn cho đến khi layout đã done
                BackColor                = txt.FillColor,
                UseTransparentBackground = false
            };

            if (icon != null)
            {
                btn.Image      = ResizeIcon(icon, iconSz);
                btn.ImageSize  = new Size(iconSz, iconSz);
                btn.ImageAlign = HorizontalAlignment.Center;
            }

            // ── Overlay positioning ──────────────────────────────────────────
            // Dùng PointToScreen/PointToClient để xử lý đúng khi txt có Dock=Fill
            // hoặc nằm trong nested panel bất kỳ.
            void Reposition()
            {
                try
                {
                    if (txt.IsDisposed || btn.IsDisposed) return;
                    if (!txt.IsHandleCreated || !parent.IsHandleCreated) return;
                    if (txt.Width <= 0 || txt.Height <= 0) return;

                    // Tính btnH theo chiều cao thực của TextBox
                    int newBtnH = Math.Max(Math.Min(txt.Height - 8, 36), 24);
                    if (btn.Size.Width != newBtnH)
                    {
                        btn.Size         = new Size(newBtnH, newBtnH);
                        btn.BorderRadius = newBtnH / 2;
                        int newIconSz    = newBtnH - 8;
                        if (icon != null)
                        {
                            btn.Image     = ResizeIcon(icon, newIconSz);
                            btn.ImageSize = new Size(newIconSz, newIconSz);
                        }
                        // Cập nhật right-padding của TextBox
                        txt.Padding = new Padding(txt.Padding.Left, txt.Padding.Top,
                                                  newBtnH + 8, txt.Padding.Bottom);
                    }

                    // Toạ độ txt trong hệ toạ độ parent (bất kể txt nằm ở đâu)
                    var txtInParent = parent.PointToClient(txt.PointToScreen(Point.Empty));
                    int x           = txtInParent.X + txt.Width  - newBtnH - 6;
                    int y           = txtInParent.Y + (txt.Height - newBtnH) / 2;
                    var pt          = new Point(x, y);

                    if (btn.Location != pt) btn.Location = pt;
                    if (!btn.Visible)       btn.Visible  = true;
                }
                catch (ObjectDisposedException) { /* Control đã bị dispose trong layout — bỏ qua an toàn */ }
            }

            // Padding sơ bộ ngay lúc attach để tránh text bị đè
            txt.Padding = new Padding(txt.Padding.Left, txt.Padding.Top,
                                      DefaultBtnH + 8, txt.Padding.Bottom);

            // Subscribe các event để reposition khi layout thay đổi
            parent.Layout        += (_, _) => Reposition();
            parent.Resize        += (_, _) => Reposition();
            parent.VisibleChanged += (_, _) => { if (parent.Visible) Reposition(); };
            txt.SizeChanged      += (_, _) => Reposition();
            txt.LocationChanged  += (_, _) => Reposition();
            txt.VisibleChanged   += (_, _) => { if (txt.Visible) Reposition(); };

            // Lần đầu: defer đến sau khi handle được tạo và layout done
            void TryFirstReposition(object s, EventArgs e) => parent.BeginInvoke(new Action(Reposition));
            if (parent.IsHandleCreated)
                parent.BeginInvoke(new Action(Reposition));
            else
                parent.HandleCreated += TryFirstReposition;

            // ── Pulse timer khi đang ghi ──────────────────────────────────────
            var pulseTimer = new System.Windows.Forms.Timer { Interval = 400 };
            bool pulseState = false;
            pulseTimer.Tick += (_, _) =>
            {
                if (btn.Tag?.ToString() != "recording") return;
                pulseState    = !pulseState;
                btn.FillColor = pulseState ? Color.FromArgb(180, 30, 45) : ColorRecording;
            };
            btn.Disposed += (_, _) => pulseTimer.Dispose();

            // ── Processing spinner ────────────────────────────────────────────
            var spinTimer  = new System.Windows.Forms.Timer { Interval = 200 };
            string[] dots  = { "⏳", "⌛" };
            int spinIdx    = 0;
            spinTimer.Tick += (_, _) =>
            {
                if (btn.Tag?.ToString() != "processing") return;
                btn.Text  = dots[spinIdx % dots.Length];
                btn.Image = null;
                spinIdx++;
            };
            btn.Disposed += (_, _) => spinTimer.Dispose();

            // ── Click ─────────────────────────────────────────────────────────
            // Phase 5: Thay `bool isRunning` bằng `VoiceButtonState _state`.
            // 3 trạng thái rõ ràng: Idle, Recording, Processing.
            VoiceButtonState _state = VoiceButtonState.Idle;

            btn.Click += async (_, _) =>
            {
                // ★ Re-entrancy guard — CRITICAL
                // WinForms async click handler cho phép re-entrant: khi handler A đang await,
                // user click lại → handler B chạy song song → Cancel recording A → start mới
                // → handler A finally SetIdle → user thấy idle → click → Cancel B → vòng lặp vô tận
                if (_state != VoiceButtonState.Idle)
                {
                    // User click trong khi Recording hoặc Processing → cancel gracefully
                    Serilog.Log.Information("[VoiceSearch] Click phụ (_state={S}) → Cancel", _state);
                    voiceService?.Cancel();
                    return;
                }

                // === Idle → Bắt đầu ===
                if (voiceService == null) return;
                _state = VoiceButtonState.Recording; // Guard ON — chặn mọi click handler song song

                Serilog.Log.Information("[VoiceSearch] Click: UsingWebSpeech={W}, VoskReady={V}",
                    voiceService.UsingWebSpeech, voiceService.IsReady);

                // ★ Bước 1: Thử khởi tạo WebSpeech (Edge Web Speech API)
                // wasFirstInit đã được loại bỏ — điều kiện thực sự là voiceService.UsingWebSpeech
                // sau khi InitWebSpeechAsync hoàn thành thành công.
                if (!voiceService.UsingWebSpeech)
                {
                    Serilog.Log.Information("[VoiceSearch] Bắt đầu InitWebSpeechAsync...");
                    SetProcessing(btn, pulseTimer, spinTimer);
                    await voiceService.InitWebSpeechAsync(
                        parent.FindForm() ?? parent);
                    SetIdle(btn, pulseTimer, spinTimer, icon,
                        Math.Max(btn.Size.Width - 8, 16));
                    Serilog.Log.Information("[VoiceSearch] Sau init: UsingWebSpeech={W}",
                        voiceService.UsingWebSpeech);

                    // WebSpeech khởi tạo thành công lần đầu:
                    // Hiện tooltip gợi ý + bắt đầu background warm-up để kết nối MS Speech Server.
                    // Khi user click lần 2 → cancel warm-up (kết nối đã warm) → record ngay.
                    if (voiceService.UsingWebSpeech)
                    {
                        var LM = EnvContract.Common.LanguageManager.Instance;
                        var initHint = new ToolTip
                        {
                            InitialDelay = 0,
                            IsBalloon    = true,
                            ToolTipIcon  = ToolTipIcon.Info,
                            ToolTipTitle = LM.Get("voz_mic_ready")
                        };
                        initHint.Show(
                            LM.Get("voz_hint_click_again"),
                            btn, 0, -(btn.Height + 8), 3500);

                        // Background warm-up (fire-and-forget): thiết lập kết nối MS Speech Server.
                        // Khi user click lần 2 → cancel warm-up (đã warm) → record ngay.
                        Serilog.Log.Information("[VoiceSearch] Background warm-up bắt đầu...");
                        _ = voiceService.ListenAsync(
                            maxDurationMs: AppConfig.VoiceSearch.WarmupDurationMs,
                            silenceMs:     AppConfig.VoiceSearch.WarmupSilenceMs,
                            contextHint:   null);

                        _state = VoiceButtonState.Idle; // Cho phép click lần 2
                        return;
                    }
                }

                // Nếu warm-up background đang chạy (guard bị giữ), cancel để nhường lại.
                // VoiceSearchService.ListenAsync sẽ tự chờ guard (WaitAsync 1500ms).
                if (voiceService.IsBusy)
                {
                    Serilog.Log.Information("[VoiceSearch] Warm-up đang chạy → Cancel, chờ mic release...");
                    voiceService.Cancel();
                    // ★ CRITICAL: Chrome audio stream cần ~300-500ms để release mic
                    // sau recognition.stop(). Nếu start() ngay → mic chưa sẵn sàng → no-speech.
                    await Task.Delay(AppConfig.VoiceSearch.MicReleaseDelayMs);
                }


                // ☆ Bước 2: Vosk fallback chỉ khi WebSpeech chưa sẵn sàng
                if (!voiceService.UsingWebSpeech)
                {
                    if (!VoiceModelManager.IsReady)
                    {
                        using var dlg = new VoiceDownloadForm();
                        if (dlg.ShowDialog(parent.FindForm()) != DialogResult.OK)
                        {
                            // BUG FIX: Reset _state trước khi return để button không bị kẹt Recording.
                            _state = VoiceButtonState.Idle;
                            return;
                        }
                    }

                    if (!voiceService.IsReady)
                    {
                        SetProcessing(btn, pulseTimer, spinTimer);
                        _state = VoiceButtonState.Processing; // Phản ánh đúng trạng thái đang load
                        await Task.Run(() => voiceService.LoadModel());
                        SetIdle(btn, pulseTimer, spinTimer, icon,
                            Math.Max(btn.Size.Width - 8, 16));
                        _state = VoiceButtonState.Recording; // Load xong → chuyển lại Recording

                        if (!voiceService.IsReady)
                        {
                            // BUG FIX: Reset _state trước khi return để button không bị kẹt Recording.
                            _state = VoiceButtonState.Idle;
                            var LM = EnvContract.Common.LanguageManager.Instance;
                            MessageBox.Show(
                                LM.Get("voz_error_desc2") + VoiceModelManager.ModelDir,
                                LM.Get("voz_error_lbl"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }
                }

                // Lấy iconSz hiện tại (sau khi Reposition đã chạy)
                int curIconSz = Math.Max(btn.Size.Width - 8, 16);

                SetRecording(btn, pulseTimer, spinTimer, icon, curIconSz);
                string result = string.Empty;

                try
                {
                    string ctx = null;
                    try { ctx = contextProvider?.Invoke(); } catch { }

                    // Giới hạn thời gian ghi âm tối đa (từ AppConfig)
                    var listenTask = voiceService.ListenAsync(
                        maxDurationMs: AppConfig.VoiceSearch.RecordingTimeoutMs,
                        silenceMs:     AppConfig.VoiceSearch.RecordingSilenceMs,
                        contextHint:   ctx);

                    while (!listenTask.IsCompleted)
                    {
                        if (voiceService.IsProcessing && btn.Tag?.ToString() == "recording")
                            SetProcessing(btn, pulseTimer, spinTimer);
                        await Task.Delay(AppConfig.VoiceSearch.PollingIntervalMs);
                    }

                    result = await listenTask;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "[VoiceSearch] Lỗi");
                }
                finally
                {
                    int finalIconSz = Math.Max(btn.Size.Width - 8, 16);
                    SetIdle(btn, pulseTimer, spinTimer, icon, finalIconSz);
                    _state = VoiceButtonState.Idle; // Guard OFF — cho phép click tiếp theo
                }

                // Chỉ hiện text sau khi xử lý hoàn tất (không dùng partial)
                if (!string.IsNullOrWhiteSpace(result))
                {
                    void ApplyResult()
                    {
                        txt.Text = result;
                        txt.Focus();
                        txt.SelectAll();
                    }
                    if (txt.InvokeRequired) txt.Invoke(ApplyResult);
                    else ApplyResult();
                }
            };

            // Tooltip
            var tip = new ToolTip { InitialDelay = 300 };
            void UpdateTooltip()
            {
                if (btn.IsDisposed) return;
                var lm = EnvContract.Common.LanguageManager.Instance;
                tip.SetToolTip(btn,
                    lm.Get("voz_tip_title") + "\n" +
                    lm.Get("voz_tip_instr") + "\n" +
                    lm.Get("voz_tip_engine"));
            }
            UpdateTooltip();
            EnvContract.Common.LanguageManager.Instance.LanguageChanged += UpdateTooltip;
            btn.Disposed += (s, e) => {
                EnvContract.Common.LanguageManager.Instance.LanguageChanged -= UpdateTooltip;
                tip.Dispose();
            };

            parent.Controls.Add(btn);
            btn.BringToFront();
            return btn;
        }

        // ── State helpers (thread-safe — gọi từ async cần Invoke về UI) ─────────
        // Tất cả 3 hàm đều dùng btn.Invoke() để đảm bảo chạy trên UI thread.
        // Thiếu Invoke → SetIdle từ finally block không update UI → button kẹt vàng.

        private static void SetRecording(Guna2Button btn,
            System.Windows.Forms.Timer pulse,
            System.Windows.Forms.Timer spin,
            Image icon, int iconSz)
        {
            void Apply()
            {
                if (btn.IsDisposed) return;
                spin.Stop();
                btn.Tag       = "recording";
                btn.FillColor = ColorRecording;
                btn.Text      = (icon == null) ? "■" : string.Empty;
                if (icon != null)
                {
                    btn.Image     = ResizeIcon(icon, iconSz);
                    btn.ImageSize = new Size(iconSz, iconSz);
                }
                pulse.Start();
            }
            if (btn.InvokeRequired) btn.Invoke(Apply); else Apply();
        }

        private static void SetProcessing(Guna2Button btn,
            System.Windows.Forms.Timer pulse,
            System.Windows.Forms.Timer spin)
        {
            void Apply()
            {
                if (btn.IsDisposed) return;
                pulse.Stop();
                btn.Tag       = "processing";
                btn.FillColor = ColorProcessing;
                btn.Image     = null;
                btn.Text      = "⏳";
                spin.Start();
            }
            if (btn.InvokeRequired) btn.Invoke(Apply); else Apply();
        }

        private static void SetIdle(Guna2Button btn,
            System.Windows.Forms.Timer pulse,
            System.Windows.Forms.Timer spin,
            Image icon, int iconSz)
        {
            void Apply()
            {
                if (btn.IsDisposed) return;
                pulse.Stop();
                spin.Stop();
                btn.Tag       = "idle";
                btn.FillColor = ColorIdle;
                btn.Text      = (icon == null) ? "🎤" : string.Empty;
                if (icon != null)
                {
                    btn.Image     = ResizeIcon(icon, iconSz);
                    btn.ImageSize = new Size(iconSz, iconSz);
                }
            }
            if (btn.InvokeRequired) btn.Invoke(Apply); else Apply();
        }

        // ── Context provider utility ────────────────────────────────────────────

        /// <summary>
        /// Trích unique text từ các cột chỉ định trong DataGridView.
        /// Dùng làm prompt Whisper — giúp nhận đúng tên riêng (Trung Nguyên, Vinamilk...).
        /// <para>Ví dụ: ExtractGridContext(dgv, "colCompanyName", "colContractId")</para>
        /// </summary>
        public static string ExtractGridContext(DataGridView dgv, params string[] columnNames)
        {
            if (dgv == null || dgv.Rows.Count == 0 || columnNames.Length == 0)
                return null;

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                foreach (var col in columnNames)
                {
                    if (!dgv.Columns.Contains(col)) continue;
                    var val = row.Cells[col].Value?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && val.Length > 1)
                        unique.Add(val);
                }
            }

            if (unique.Count == 0) return null;

            // Nối tối đa 2000 ký tự — đủ cho ~50 tên công ty/mã HĐ
            var sb = new StringBuilder();
            foreach (var term in unique.Take(60))  // max 60 terms
            {
                if (sb.Length + term.Length + 2 > 2000) break;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(term);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Trích context từ FlowLayoutPanel card-based (card.Tag chứa DTO).
        /// Dùng cho các UC không có DataGridView (Director, SampleConfig, LabResult...).
        /// </summary>
        public static string ExtractCardContext(FlowLayoutPanel flp, params string[] propertyNames)
        {
            if (flp == null || flp.Controls.Count == 0 || propertyNames.Length == 0)
                return null;

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Control ctrl in flp.Controls)
            {
                var tag = ctrl.Tag;
                if (tag == null) continue;
                var type = tag.GetType();

                foreach (var propName in propertyNames)
                {
                    var prop = type.GetProperty(propName);
                    if (prop == null) continue;
                    var val = prop.GetValue(tag)?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(val) && val.Length > 1)
                        unique.Add(val);
                }
            }

            if (unique.Count == 0) return null;

            var sb = new StringBuilder();
            foreach (var term in unique.Take(60))  // max 60 terms
            {
                if (sb.Length + term.Length + 2 > 2000) break;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(term);
            }
            return sb.ToString();
        }
    }
}
