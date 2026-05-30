using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Drawing;

namespace EnvContract.GUI.Forms.Auth
{
    /// <summary>
    /// Static utility class chứa toàn bộ logic xử lý ảnh khuôn mặt cho tính năng Face ID:
    ///   - Load DNN model (SSD ResNet-10) một lần duy nhất (lazy singleton)
    ///   - Phát hiện khuôn mặt qua DNN (+ 15% bbox padding) hoặc center-crop fallback
    ///   - Chuẩn hóa ảnh (128×128 grayscale, EqualizeHist, PNG lossless)
    ///   - So sánh hai ảnh bằng multi-metric histogram (robust, không dùng MAE)
    /// 
    /// === THUẬT TOÁN SO SÁNH v3 (production-stable) ===
    /// 
    /// Pipeline so sánh:
    ///   1. Decode PNG → 128×128 (ĐÃ EqualizeHist — KHÔNG equalize lần 2)
    ///   2. GaussianBlur(7) → làm mờ nhẹ để histogram ổn định khi bbox dịch chuyển
    ///   3. Score = 25% Global Correlation
    ///           + 50% Grid 2×2 Correlation (cell 64×64, 64 bins)
    ///           + 25% Bhattacharyya similarity
    ///
    /// Tại sao KHÔNG dùng MAE?
    ///   → DNN bbox dao động 5-15px mỗi frame → pixel ở (x,y) khác nhau
    ///   → MAE tăng cao dù cùng 1 người → false negative
    ///
    /// Tại sao Grid 2×2 thay vì 3×3?
    ///   → Cell 64×64 lớn hơn → ít nhạy với bbox shift
    ///   → 64 bins / 4096 pixels = 64 pixels/bin → tín hiệu tốt
    ///   → Vẫn capture cấu trúc không gian (trán/mắt vs mũi/miệng)
    ///
    /// Tại sao GaussianBlur?
    ///   → Làm mượt histogram, giảm ảnh hưởng của pixel lệch vị trí
    ///   → Cùng người: score ổn định hơn qua các frame
    /// </summary>
    internal static class FaceIdManager
    {
        // ── DNN Model (lazy singleton) ────────────────────────────────────────
        private static readonly Lazy<Emgu.CV.Dnn.Net> _faceNet = new(() =>
        {
            string baseDir    = AppDomain.CurrentDomain.BaseDirectory;
            string prototxt   = System.IO.Path.Combine(baseDir, "deploy.prototxt");
            string caffemodel = System.IO.Path.Combine(baseDir, "res10_300x300_ssd_iter_140000.caffemodel");

            if (System.IO.File.Exists(prototxt) && System.IO.File.Exists(caffemodel))
            {
                try
                {
                    var net = Emgu.CV.Dnn.DnnInvoke.ReadNetFromCaffe(prototxt, caffemodel);
                    AppLogger.Info("FaceID: DNN model loaded thành công.");
                    return net;
                }
                catch (Exception ex)
                {
                    AppLogger.Error("FaceID: Không thể load DNN model.", ex);
                    return null;
                }
            }

            AppLogger.Warning("FaceID: Không tìm thấy model files — dùng center crop fallback.");
            return null;
        });

        // ── Face guide proportions (khớp với oval hiển thị trên camera) ────────
        private const double GUIDE_W_RATIO = 0.18;
        private const double GUIDE_H_RATIO = 0.28;

        /// <summary>Padding 15% quanh bbox DNN cho crop ổn định.</summary>
        private const double BBOX_PADDING = 0.15;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Vẽ face guide oval kiểu banking-app lên frame camera.
        /// </summary>
        public static void DrawFaceGuide(Mat frame)
        {
            if (frame == null || frame.IsEmpty) return;

            int w = frame.Width, h = frame.Height;
            var center = new Point(w / 2, h / 2);
            var axes   = new Size((int)(w * GUIDE_W_RATIO), (int)(h * GUIDE_H_RATIO));

            using var overlay = new Mat();
            frame.CopyTo(overlay);
            using var dark = new Mat(frame.Size, frame.Depth, frame.NumberOfChannels);
            dark.SetTo(new MCvScalar(0, 0, 0));
            CvInvoke.AddWeighted(overlay, 0.35, dark, 0.65, 0, overlay);

            using var mask = new Mat(frame.Size, DepthType.Cv8U, 1);
            mask.SetTo(new MCvScalar(0));
            CvInvoke.Ellipse(mask, center, axes, 0, 0, 360, new MCvScalar(255), -1);

            frame.CopyTo(overlay, mask);

            CvInvoke.Ellipse(overlay, center, axes, 0, 0, 360,
                new MCvScalar(100, 255, 100), 3);

            int cross = 8;
            CvInvoke.Line(overlay, new Point(w / 2 - cross, h / 2),
                new Point(w / 2 + cross, h / 2), new MCvScalar(100, 255, 100), 1);
            CvInvoke.Line(overlay, new Point(w / 2, h / 2 - cross),
                new Point(w / 2, h / 2 + cross), new MCvScalar(100, 255, 100), 1);

            overlay.CopyTo(frame);
        }

        /// <summary>
        /// Trích xuất khuôn mặt chuẩn hóa 128×128 grayscale PNG.
        /// DNN + 15% padding → (nếu requireDnn=false) fallback center crop → resize 128×128 → EqualizeHist → PNG.
        /// 
        /// requireDnn = true  → dùng khi ĐĂNG NHẬP:
        ///   • Nếu DNN load được VÀ không phát hiện mặt → return null (người dùng không nhìn thẳng).
        ///   • Nếu DNN KHÔNG load được (môi trường lỗi) → cho phép center-crop fallback với threshold cao hơn.
        /// requireDnn = false → dùng khi ĐĂNG KÝ: luôn cho phép center-crop fallback.
        /// </summary>
        public static byte[] ExtractNormalizedFace(Mat frame, bool requireDnn = false)
        {
            try
            {
                if (frame == null || frame.IsEmpty) return null;

                var net = _faceNet.Value;

                if (net != null)
                {
                    try
                    {
                        int W = frame.Width, H = frame.Height;

                        using var blob = Emgu.CV.Dnn.DnnInvoke.BlobFromImage(
                            frame, 1.0, new Size(300, 300),
                            new MCvScalar(104, 177, 123), false, false);

                        net.SetInput(blob, "data");
                        using var detections = net.Forward("detection_out");

                        System.Array data = detections.GetData();
                        float bestConf = 0f;
                        Rectangle bestRect = Rectangle.Empty;

                        int numDet = detections.SizeOfDimension[2];
                        for (int i = 0; i < numDet; i++)
                        {
                            float conf = (float)data.GetValue(0, 0, i, 2);
                            if (conf < 0.50f) continue;
                            if (conf > bestConf)
                            {
                                bestConf = conf;
                                float x1 = (float)data.GetValue(0, 0, i, 3);
                                float y1 = (float)data.GetValue(0, 0, i, 4);
                                float x2 = (float)data.GetValue(0, 0, i, 5);
                                float y2 = (float)data.GetValue(0, 0, i, 6);

                                // Padding 15% quanh bbox → crop ổn định hơn khi bbox drift
                                float bw = x2 - x1, bh = y2 - y1;
                                float padW = bw * (float)BBOX_PADDING;
                                float padH = bh * (float)BBOX_PADDING;

                                int rx  = (int)Math.Max(0, (x1 - padW) * W);
                                int ry  = (int)Math.Max(0, (y1 - padH) * H);
                                int rx2 = (int)Math.Min(W, (x2 + padW) * W);
                                int ry2 = (int)Math.Min(H, (y2 + padH) * H);

                                if (rx2 - rx > 0 && ry2 - ry > 0)
                                    bestRect = new Rectangle(rx, ry, rx2 - rx, ry2 - ry);
                            }
                        }

                        if (!bestRect.IsEmpty)
                        {
                            AppLogger.Debug($"FaceID: DNN detected conf={bestConf:F2} rect={bestRect}");
                            return CropAndNormalize(frame, bestRect);
                        }

                        AppLogger.Debug($"FaceID: DNN no face (best conf={bestConf:F2})");

                        // DNN load được nhưng KHÔNG detect mặt → người dùng không nhìn thẳng
                        // Trả null để Login hiện thông báo "nhìn thẳng" (không đếm attempt)
                        if (requireDnn)
                        {
                            AppLogger.Warning("FaceID: DNN không phát hiện mặt người — yêu cầu nhìn thẳng vào camera.");
                            return null;
                        }
                    }
                    catch (Exception dnnEx)
                    {
                        AppLogger.Warning($"FaceID: DNN error, using center crop: {dnnEx.Message}");
                        // DNN lỗi runtime → fall through to center-crop
                    }
                }
                else
                {
                    // net == null: DNN model không load được (thiếu thư viện, runtime issue)
                    // → KHÔNG chặn, dùng center-crop + threshold cao (0.80) vẫn đủ bảo mật
                    AppLogger.Warning("FaceID: DNN model unavailable — using center-crop fallback (threshold=0.80 active).");
                }

                // Center crop fallback → dùng cho cả đăng ký và khi DNN không khả dụng
                int fw = (int)(frame.Width  * GUIDE_W_RATIO * 2);
                int fh = (int)(frame.Height * GUIDE_H_RATIO * 2);
                var centerRect = new Rectangle(
                    (frame.Width  - fw) / 2,
                    (frame.Height - fh) / 2, fw, fh);

                AppLogger.Debug($"FaceID: Center crop fallback {centerRect}");
                return CropAndNormalize(frame, centerRect);
            }
            catch (Exception ex)
            {
                AppLogger.Error("FaceID: ExtractNormalizedFace error", ex);
                return null;
            }
        }


        /// <summary>
        /// So sánh 2 ảnh khuôn mặt chuẩn hóa (128×128 grayscale PNG).
        ///
        /// Score = 25% Global Correlation + 50% Grid 2×2 Correlation + 25% Bhattacharyya
        ///
        /// Cùng người (webcam): ~0.80–0.95
        /// Người khác:          ~0.45–0.65
        /// Threshold mặc định:  0.72
        /// </summary>
        public static double CompareHistogram(byte[] faceA, byte[] faceB)
        {
            try
            {
                using var matA = new Mat();
                using var matB = new Mat();
                CvInvoke.Imdecode(faceA, ImreadModes.Grayscale, matA);
                CvInvoke.Imdecode(faceB, ImreadModes.Grayscale, matB);

                if (matA.IsEmpty || matB.IsEmpty) return 0.0;

                // ★ KHÔNG resize (cả hai đã là 128×128)
                // ★ KHÔNG EqualizeHist lần 2 (đã làm trong CropAndNormalize)
                // ★ GaussianBlur(7) để histogram ổn định khi bbox dịch nhẹ
                using var blurA = new Mat();
                using var blurB = new Mat();
                CvInvoke.GaussianBlur(matA, blurA, new Size(7, 7), 0);
                CvInvoke.GaussianBlur(matB, blurB, new Size(7, 7), 0);

                // ── 1. Global histogram Correlation (25%) ────────────────────────
                double globalScore = CalcHistCorrelation(blurA, blurB,
                    new Rectangle(0, 0, blurA.Width, blurA.Height), 256);
                AppLogger.Debug($"FaceID: Global={globalScore:F3}");

                // ── 2. Grid 2×2 histogram Correlation (50%) ─────────────────────
                // Cell 64×64 = 4096 pixels, 64 bins → ~64 px/bin → tín hiệu mạnh
                // 2×2 đủ lớn để tolerance bbox shift, vẫn capture cấu trúc mặt:
                //   [trán+mắt_trái | trán+mắt_phải]
                //   [mũi+miệng_trái | mũi+miệng_phải]
                const int grid = 2;
                int cellW = blurA.Width  / grid;  // 64
                int cellH = blurA.Height / grid;  // 64
                double gridTotal = 0;

                for (int gy = 0; gy < grid; gy++)
                for (int gx = 0; gx < grid; gx++)
                {
                    var cellRect = new Rectangle(gx * cellW, gy * cellH, cellW, cellH);
                    double cellScore = CalcHistCorrelation(blurA, blurB, cellRect, 64);
                    gridTotal += cellScore;
                    AppLogger.Debug($"FaceID: Grid[{gy},{gx}]={cellScore:F3}");
                }
                double gridScore = gridTotal / (grid * grid);

                // ── 3. Bhattacharyya similarity (25%) ────────────────────────────
                double bhattScore = CalcBhattacharyyaSimilarity(blurA, blurB);
                AppLogger.Debug($"FaceID: Bhatt={bhattScore:F3}");

                // ── Weighted final score ─────────────────────────────────────────
                double finalScore = globalScore * 0.25
                                  + gridScore   * 0.50
                                  + bhattScore  * 0.25;

                AppLogger.Info($"FaceID: SCORE = {globalScore:F3}×0.25 + {gridScore:F3}×0.50 + {bhattScore:F3}×0.25 = {finalScore:F3}");

                return Math.Clamp(finalScore, 0.0, 1.0);
            }
            catch (Exception ex)
            {
                AppLogger.Error("FaceID: CompareHistogram error", ex);
                return 0.0;
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Histogram Correlation giữa 2 vùng ảnh.
        /// Normalize histogram trước khi compare → ổn định hơn.
        /// </summary>
        private static double CalcHistCorrelation(Mat a, Mat b, Rectangle region, int bins)
        {
            using var cellA = new Mat(a, region);
            using var cellB = new Mat(b, region);
            using var imgA  = cellA.ToImage<Gray, byte>();
            using var imgB  = cellB.ToImage<Gray, byte>();
            using var hA    = new Emgu.CV.DenseHistogram(bins, new RangeF(0, 256));
            using var hB    = new Emgu.CV.DenseHistogram(bins, new RangeF(0, 256));
            hA.Calculate(new[] { imgA }, true, null);
            hB.Calculate(new[] { imgB }, true, null);
            CvInvoke.Normalize(hA, hA);
            CvInvoke.Normalize(hB, hB);
            return Math.Max(0, CvInvoke.CompareHist(hA, hB, HistogramCompMethod.Correl));
        }

        /// <summary>
        /// Bhattacharyya distance → similarity (1 - distance).
        /// </summary>
        private static double CalcBhattacharyyaSimilarity(Mat a, Mat b)
        {
            using var imgA = a.ToImage<Gray, byte>();
            using var imgB = b.ToImage<Gray, byte>();
            using var hA   = new Emgu.CV.DenseHistogram(256, new RangeF(0, 256));
            using var hB   = new Emgu.CV.DenseHistogram(256, new RangeF(0, 256));
            hA.Calculate(new[] { imgA }, true, null);
            hB.Calculate(new[] { imgB }, true, null);
            CvInvoke.Normalize(hA, hA);
            CvInvoke.Normalize(hB, hB);
            double distance = CvInvoke.CompareHist(hA, hB, HistogramCompMethod.Bhattacharyya);
            return Math.Max(0, 1.0 - distance);
        }

        /// <summary>
        /// Crop → grayscale → resize 128×128 → EqualizeHist → PNG lossless.
        /// </summary>
        private static byte[] CropAndNormalize(Mat frame, Rectangle rect)
        {
            rect = Rectangle.Intersect(rect, new Rectangle(0, 0, frame.Width, frame.Height));
            if (rect.Width <= 0 || rect.Height <= 0) return null;

            using var gray = new Mat();
            if (frame.NumberOfChannels > 1)
                CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);
            else
                frame.CopyTo(gray);

            using var face       = new Mat(gray, rect);
            using var normalized = new Mat();
            CvInvoke.Resize(face, normalized, new Size(128, 128));
            CvInvoke.EqualizeHist(normalized, normalized);  // Chỉ 1 lần duy nhất ở đây

            using var vec = new VectorOfByte();
            CvInvoke.Imencode(".png", normalized, vec);
            return vec.ToArray();
        }
    }
}
