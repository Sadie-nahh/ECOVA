using NAudio.Wave;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vosk;

namespace EnvContract.GUI.Services
{
    /// <summary>
    /// Nhận diện tiếng Việt offline dùng Vosk (Kaldi-based, vosk-model-vn-0.4).
    /// Pipeline: Ghi âm (NAudio 16kHz) → Calibrate noise → Streaming Vosk → FuzzyMatch → Text.
    ///
    /// Accuracy stack (4 lớp):
    ///   1. Adaptive noise calibration   — tự động đo ambient, đặt threshold phù hợp phòng
    ///   2. Vosk streaming Kaldi TDNN    — nhận dạng từng chunk 80ms, độ trễ thấp
    ///   3. Partial results (real-time)  — hiển thị text liên tục qua OnPartialResult event
    ///   4. Fuzzy Match (Levenshtein)    — sửa lỗi nhận dạng dựa trên dữ liệu DB thực
    /// </summary>
    public class VoiceSearchService : IDisposable
    {
        private Vosk.Model              _model;
        private CancellationTokenSource _recordCts;
        private bool                    _disposed;
        private bool                    _userCancelled;   // true CHỈ khi user click cancel
        private readonly SemaphoreSlim  _guard = new(1, 1);

        // WebSpeech — ưu tiên hơn Vosk khi đã khởi tạo
        private WebSpeechService        _webSpeech;
        private bool                    _webSpeechInitializing;

        public bool IsListening  { get; private set; }
        public bool IsProcessing { get; private set; }
        public bool IsBusy       => IsListening || IsProcessing;

        // IsReady: true nếu WebSpeech đã sẵn sàng, HOẶC Vosk model đã load
        public bool IsReady => (_webSpeech?.IsReady == true) || (_model != null && !_disposed);
        public bool UsingWebSpeech => _webSpeech?.IsReady == true;

        public VoiceSearchService() { }

        // =========================================================================
        // WEB SPEECH INIT  (gọi từ UI thread khi attach voice button)
        // =========================================================================

        /// <summary>
        /// Khởi tạo WebSpeech (Edge Web Speech API).
        /// Phải gọi từ UI thread vì tạo WebView2 control.
        /// Chỉ chạy một lần — các lần gọi sau là no-op.
        /// </summary>
        public async Task InitWebSpeechAsync(System.Windows.Forms.Control parent)
        {
            if (_webSpeech?.IsReady == true || _webSpeechInitializing || _disposed) return;
            _webSpeechInitializing = true;
            try
            {
                _webSpeech = new WebSpeechService();
                await _webSpeech.InitAsync(parent);
                if (_webSpeech.IsReady)
                    Log.Information("[VoiceSearch] Đã chuyển sang WebSpeech (Edge) — tiếng Việt có dấu!");
                else
                    Log.Warning("[VoiceSearch] WebSpeech khởi tạo thất bại — fallback Vosk");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[VoiceSearch] Không thể khởi tạo WebSpeech");
                _webSpeech = null;
            }
            finally { _webSpeechInitializing = false; }
        }

        // =========================================================================
        // LOAD MODEL
        // =========================================================================

        public void LoadModel()
        {
            if (_model != null) return;
            if (!VoiceModelManager.IsReady) return;

            try
            {
                Vosk.Vosk.SetLogLevel(-1); // tắt verbose Kaldi log
                _model = new Vosk.Model(VoiceModelManager.ModelDir);
                Log.Information("[Vosk] Model loaded: {D}", VoiceModelManager.ModelDir);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Vosk] Không thể load model từ {D}", VoiceModelManager.ModelDir);
            }
        }

        // =========================================================================
        // LISTEN  (main entry point — WebSpeech ưu tiên, Vosk fallback)
        // =========================================================================

        public async Task<string> ListenAsync(
            int maxDurationMs  = 10_000,
            int silenceMs      = 2_000,
            string contextHint = null)
        {
            // ★ Log path để trace
            Log.Information("[VoiceSearch] ListenAsync — WebSpeech={W}, VoskModel={V}",
                _webSpeech?.IsReady == true, _model != null);

            // ★ WebSpeech path: Edge Web Speech API — tiếng Việt có dấu, chính xác cao
            if (_webSpeech?.IsReady == true)
            {
                // Chờ tối đa 1500ms cho guard được release (warm-up có thể đang chạy từ UC khác)
                if (!await _guard.WaitAsync(1500))
                {
                    Log.Warning("[WebSpeech] Timeout chờ guard — bỏ qua");
                    return string.Empty;
                }
                try
                {
                    IsListening    = true;
                    _userCancelled = false;
                    _recordCts     = new CancellationTokenSource(maxDurationMs);

                    var rawResult = await _webSpeech.ListenAsync(
                        maxDurationMs, _recordCts.Token);

                    Log.Information("[WebSpeech] Raw: \"{R}\"", rawResult);

                    if (_userCancelled) return string.Empty;

                    // Nếu WebSpeech trả về kết quả hợp lệ
                    if (!string.IsNullOrWhiteSpace(rawResult))
                    {
                        IsProcessing = true;
                        return FuzzyMatchContext(rawResult, contextHint);
                    }

                    // WebSpeech thất bại (không có internet, mic bị block...)
                    // Tự động fallback sang Vosk nếu có sẵn model
                    if (_model != null)
                    {
                        Log.Warning("[VoiceSearch] WebSpeech không có kết quả → fallback Vosk");
                        IsListening = true; // giữ trạng thái recording
                        _recordCts  = new CancellationTokenSource(maxDurationMs);

                        using var recognizer = new VoskRecognizer(_model, 16000f);
                        recognizer.SetWords(false);
                        var voskResult = await RecordAndRecognizeAsync(
                            recognizer, silenceMs, _recordCts.Token);

                        if (!string.IsNullOrWhiteSpace(voskResult))
                        {
                            IsProcessing = true;
                            return FuzzyMatchContext(voskResult, contextHint);
                        }
                    }

                    return string.Empty;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[WebSpeech] Lỗi");
                    return string.Empty;
                }
                finally
                {
                    IsListening  = false;
                    IsProcessing = false;
                    _recordCts?.Dispose();
                    _recordCts = null;
                    _guard.Release();
                }
            }

            // ☆ Vosk fallback: khi không có internet hoặc WebSpeech chưa sẵn sàng
            if (_model == null)
            {
                Log.Warning("[VoiceSearch] Cả WebSpeech và Vosk đều chưa sẵn sàng");
                return string.Empty;
            }
            if (!await _guard.WaitAsync(1500))
            {
                Log.Warning("[Vosk] Timeout chờ guard");
                return string.Empty;
            }

            try
            {
                IsListening    = true;
                _userCancelled = false;
                _recordCts     = new CancellationTokenSource(maxDurationMs);

                using var recognizer = new VoskRecognizer(_model, 16000f);
                recognizer.SetWords(false);

                var rawResult = await RecordAndRecognizeAsync(
                    recognizer, silenceMs, _recordCts.Token);

                IsListening = false;

                if (_userCancelled) return string.Empty;
                if (string.IsNullOrWhiteSpace(rawResult)) return string.Empty;

                IsProcessing = true;
                return FuzzyMatchContext(rawResult, contextHint);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                    Log.Error(ex, "[Vosk] Lỗi trong ListenAsync");
                return string.Empty;
            }
            finally
            {
                IsListening  = false;
                IsProcessing = false;
                _recordCts?.Dispose();
                _recordCts = null;
                _guard.Release();
            }   // end finally
        }       // end ListenAsync

        // =========================================================================
        // RECORD + RECOGNIZE
        // - Thu thập audio (batch) → xử lý Vosk sau → tốt hơn streaming
        // - Calibration 400ms đầu để đo ambient noise → threshold động
        // - Collect TẤT CẢ Vosk segments (cả trung gian + final)
        // =========================================================================

        private async Task<string> RecordAndRecognizeAsync(
            VoskRecognizer recognizer,
            int silenceMs,
            CancellationToken ct)
        {
            // Log audio devices một lần để xác nhận micro đúng
            int devCount = WaveInEvent.DeviceCount;
            Log.Information("[Vosk] Audio input devices: {N}", devCount);
            for (int d = 0; d < devCount; d++)
            {
                var cap = WaveInEvent.GetCapabilities(d);
                Log.Information("[Vosk] Device[{I}]: {Name}", d, cap.ProductName);
            }

            var chunks = new List<(byte[] data, int len)>();
            var done   = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fmt    = new WaveFormat(16000, 16, 1);       // 16kHz 16-bit mono
            using var mic = new WaveInEvent { WaveFormat = fmt, BufferMilliseconds = 80 };

            // ── Calibration + silence detection state ──────────────────────────────────
            const int   CalibBufs = 5;      // 5 × 80ms = 400ms đo ambient
            const float RmsFloor  = 0.002f; // -54dB baseline (nhạy hơn)
            const float RmsScale  = 2.0f;   // threshold = ambient × 2.0 (dễ khởi động hơn)
            const float ThreshMax = 0.020f; // cap thấp hơn: giọng yếu vẫn được nhận
            float ambientSum  = 0f;
            int   calibCount  = 0;
            float threshold   = 0.006f;     // giá trị mặc định trước calibration
            int   silThreshold  = (int)Math.Ceiling(silenceMs / 80.0);
            int   silentCount   = 0;  // số buffer im lặng LIÊN TỤC
            bool  speechDetected = false;

            mic.DataAvailable += (_, e) =>
            {
                if (done.Task.IsCompleted) return;

                float rms = CalcRms(e.Buffer, e.BytesRecorded);

                // Log 5 chunk đầu để biết mic có tín hiệu thực không
                if (chunks.Count < 5)
                    Log.Debug("[Vosk] Chunk#{N} RMS={R:F6}", chunks.Count + 1, rms);

                // Thu thập TOÀN BỘ audio (kể cả trong calibration) cho batch
                var buf = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, buf, 0, e.BytesRecorded);
                chunks.Add((buf, e.BytesRecorded));

                // ── Calibration phase (400ms đầu tiên) ──────────────────────────
                if (calibCount < CalibBufs)
                {
                    ambientSum += rms;
                    calibCount++;
                    if (calibCount == CalibBufs)
                    {
                        float avg = ambientSum / CalibBufs;
                        threshold = Math.Min(ThreshMax, Math.Max(RmsFloor, avg * RmsScale));
                        Log.Information("[Vosk] Calib: ambient={A:F4} → threshold={T:F4}", avg, threshold);
                    }
                    return; // Không chạy silence detection trong calibration
                }

                // ── Silence detection (sau calibration) ────────────────────────
                // Không gate bằng hasSpeech — dừng sau silenceMs im lặng bất kể
                // Nếu RMS > threshold → phát hiện giọng, reset đếm im lặng
                // Nếu RMS ≤ threshold → đếm im lặng, dừng khi đủ silenceMs
                if (rms > threshold)
                {
                    speechDetected = true;
                    silentCount    = 0;
                }
                else if (++silentCount >= silThreshold)
                {
                    Log.Information("[Vosk] Im lặng {S}ms (speech={D}) → dừng ghi âm",
                        silenceMs, speechDetected);
                    done.TrySetResult(speechDetected);
                }
            };

            mic.RecordingStopped += (_, _) => done.TrySetResult(true);
            ct.Register(() =>
            {
                try   { mic.StopRecording(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Vosk] CancelToken StopRecording error: {ex.Message}"); done.TrySetResult(false); }
            });

            mic.StartRecording();
            Log.Information("[Vosk] Bắt đầu ghi âm (silence={S}ms)", silenceMs);
            await done.Task;

            try { mic.StopRecording(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Vosk] StopRecording cleanup: {ex.Message}"); }
            await Task.Delay(200); // Flush buffers cuối NAudio

            Log.Information("[Vosk] Thu thập {N} chunks ({Ms}ms) — batch Vosk...",
                chunks.Count, chunks.Count * 80);

            if (chunks.Count == 0)
            {
                Log.Warning("[Vosk] Không có audio — kiểm tra microphone");
                return string.Empty;
            }

            // ── Batch processing: feed TẤT CẢ audio → collect ALL segments ────
            // QUAN TRỌNG: khi AcceptWaveform trả true, Vosk đã commit 1 phrase.
            // Phải gọi Result() ngay — KHÔNG bỏ qua — nếu không mất text đó.
            var sb = new StringBuilder();
            foreach (var (data, len) in chunks)
            {
                // Normalize volume trước khi feed Vosk
                // (mic yếu → audio quá nhỏ → Vosk bỏ sót từ)
                var normalized = NormalizeAudio(data, len);
                if (recognizer.AcceptWaveform(normalized, len))
                {
                    var seg = ParseVoskText(recognizer.Result());
                    if (!string.IsNullOrWhiteSpace(seg))
                    {
                        if (sb.Length > 0) sb.Append(' ');
                        sb.Append(seg);
                    }
                }
            }
            // Phần audio chưa được Vosk finalize
            var tail = ParseVoskText(recognizer.FinalResult());
            if (!string.IsNullOrWhiteSpace(tail))
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(tail);
            }

            var raw = sb.ToString().Trim().TrimEnd('.', ',', '!', '?', ';', ':');
            Log.Information("[Vosk] Raw result: \"{R}\"", raw);
            return raw;
        }

        // =========================================================================
        // AUDIO PRE-PROCESSING
        // =========================================================================

        /// <summary>
        /// Normalize âm lượng audio + pre-emphasis filter.
        /// - Normalization: tăng gain để đưa về mức 40% amplitude
        ///   → Vosk nhận dạng tốt hơn với mic yếu
        /// - Pre-emphasis: boost tần số cao (phụ âm ch, kh, th, nh...)
        ///   → Vosk phân biệt phụ âm tiếng Việt tốt hơn
        /// </summary>
        private static byte[] NormalizeAudio(byte[] buffer, int byteCount)
        {
            if (byteCount < 2) return buffer;

            // Bước 1: Tìm RMS thực tế của chunk
            float sumSq = 0f;
            int   n     = byteCount / 2;
            for (int i = 0; i < byteCount - 1; i += 2)
            {
                float s = BitConverter.ToInt16(buffer, i) / 32768f;
                sumSq += s * s;
            }
            float rms = n > 0 ? MathF.Sqrt(sumSq / n) : 0f;

            // Bỏ qua chỉ khi chính xác bằng 0 (tất cả mẫu = 0x00) hoặc đã đủ lớn (> 0.35)
            if (rms <= 0f || rms > 0.35f) return buffer;

            // Gain target RMS = 0.35 (35% amplitude — vùng tốt nhất cho Vosk)
            float gain = Math.Min(0.35f / rms, 6.0f); // max 6x gain tránh distortion

            // Bước 2: Apply gain + pre-emphasis filter (coefficient 0.97)
            // pre-emphasis: y[n] = x[n] - 0.97 * x[n-1]
            // Boost phụ âm cao tần: ch, kh, ph, th, tr...
            var    output    = new byte[byteCount];
            float  prevSample = 0f;
            const float PreEmph = 0.95f;

            for (int i = 0; i < byteCount - 1; i += 2)
            {
                float cur    = BitConverter.ToInt16(buffer, i) / 32768f;
                float gained = (cur - PreEmph * prevSample) * gain;
                prevSample   = cur;

                // Hard clip -1..1
                gained = Math.Max(-1f, Math.Min(1f, gained));

                short s16   = (short)(gained * 32767f);
                output[i]   = (byte)(s16 & 0xFF);
                output[i+1] = (byte)((s16 >> 8) & 0xFF);
            }
            return output;
        }


        // =========================================================================
        // JSON PARSERS  (Vosk trả về JSON string)
        // =========================================================================

        /// <summary>
        /// Parse "text" field từ Result() hoặc FinalResult().
        /// JSON format: {"text": "xin chào công ty"}
        /// </summary>
        private static string ParseVoskText(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            try
            {
                return JObject.Parse(json)["text"]?.ToString()?.Trim() ?? string.Empty;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Vosk] JSON parse error: {ex.Message}"); return string.Empty; }
        }

        /// <summary>
        /// Parse "partial" field từ PartialResult().
        /// JSON format: {"partial": "xin chào"}
        /// </summary>
        private static string ParseVoskPartial(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            try
            {
                return JObject.Parse(json)["partial"]?.ToString()?.Trim() ?? string.Empty;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Vosk] JSON parse error: {ex.Message}"); return string.Empty; }
        }

        // =========================================================================
        // CALCULATE RMS
        // =========================================================================

        private static float CalcRms(byte[] buf, int count)
        {
            float sum = 0f;
            int   n   = count / 2;
            for (int i = 0; i < count - 1; i += 2)
            {
                float s = BitConverter.ToInt16(buf, i) / 32768f;
                sum += s * s;
            }
            return n > 0 ? MathF.Sqrt(sum / n) : 0f;
        }

        // =========================================================================
        // FUZZY MATCH — Đối chiếu Vosk output với dữ liệu grid/card
        //
        // Thuật toán 3 lớp cho tiếng Việt:
        //   1. Exact / Contains     — khớp chính xác hoặc chứa
        //   2. Syllable Jaccard     — so sánh từng âm tiết (bỏ dấu thanh)
        //   3. Levenshtein          — so sánh ký tự (bỏ dấu thanh)
        //   → Dùng score MAX(Jaccard, Levenshtein) ≥ 55% → xuất dữ liệu grid
        // =========================================================================

        /// <summary>
        /// Đối chiếu kết quả Vosk với danh sách từ vựng phòng ban.
        /// Nếu vần/âm tiết giống data grid ≥ 55% → xuất CHÍNH XÁC dữ liệu grid.
        /// Chỉ khi quá khác mới xuất text Vosk nguyên bản.
        /// </summary>
        private static string FuzzyMatchContext(string rawResult, string contextHint)
        {
            if (string.IsNullOrWhiteSpace(rawResult)) return rawResult;

            // Không có context → áp dụng Vietnamese Title Case tối thiểu
            // "nguyễn trung nguyên" → "Nguyễn Trung Nguyên"
            if (string.IsNullOrWhiteSpace(contextHint))
            {
                var titled = ApplyVietnameseTitleCase(rawResult);
                Log.Information("[VoiceSearch] No context → TitleCase: '{R}' → '{T}'", rawResult, titled);
                return titled;
            }

            var terms = contextHint.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0) return rawResult;

            string input = rawResult.Trim();

            // === Bước 1: Exact match (case-insensitive) ===
            foreach (var t in terms)
            {
                if (t.Equals(input, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("[VoiceSearch] Exact match: '{R}' = '{M}'", input, t);
                    return t;
                }
            }

            // === Bước 2: Contains match (cả 2 chiều, có và không có dấu) ===
            // input ⊂ term: "Vinamilk" → "Công ty Vinamilk"
            // Normalize dấu trước khi so sánh: "trung" match "Trung Nguyên Cà Phê" ✓
            string inputNFD  = RemoveVietnameseDiacritics(input.ToLowerInvariant());
            string bestContains = null;
            int    bestLen      = 0;
            foreach (var t in terms)
            {
                if (t.Length <= 1) continue;
                string tNFD = RemoveVietnameseDiacritics(t.ToLowerInvariant());
                // So sánh có dấu (OIC) VÀ không dấu
                bool match =
                    t.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    input.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tNFD.IndexOf(inputNFD, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    inputNFD.IndexOf(tNFD, StringComparison.OrdinalIgnoreCase) >= 0;
                if (match && t.Length > bestLen)
                {
                    bestLen      = t.Length;
                    bestContains = t;
                }
            }
            if (bestContains != null)
            {
                Log.Information("[VoiceSearch] Contains match: '{R}' → '{M}'", input, bestContains);
                return bestContains;
            }

            // === Bước 2b: Word-overlap match ===
            // "cong ty vinamilk" vs "Công ty cổ phần Vinamilk":
            // Substring fails do "cổ phần" ở giữa — nhưng từng từ đều khớp:
            // "cong"✓ "ty"✓ "vinamilk"✓ → 3/3 = 100% → match!
            var inputWords = inputNFD.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                     .Where(w => w.Length >= 2).ToArray();
            if (inputWords.Length >= 1)
            {
                string bestWordMatch = null;
                double bestWordScore = 0;
                int    bestWordLen   = 0;

                foreach (var t in terms)
                {
                    if (t.Length <= 1) continue;
                    string tNFD = RemoveVietnameseDiacritics(t.ToLowerInvariant());
                    int matched = inputWords.Count(w => tNFD.Contains(w));
                    double ratio = (double)matched / inputWords.Length;

                    // Ưu tiên term dài hơn khi cùng score
                    if (ratio > bestWordScore || (ratio == bestWordScore && t.Length > bestWordLen))
                    {
                        bestWordScore = ratio;
                        bestWordMatch = t;
                        bestWordLen   = t.Length;
                    }
                }

                // Ngưỡng: ≥ 60% từ khớp (ít nhất 1 từ dài ≥ 2 ký tự)
                if (bestWordScore >= 0.60 && bestWordMatch != null)
                {
                    Log.Information("[VoiceSearch] WordOverlap: '{R}' → '{M}' ({S:P0})",
                        input, bestWordMatch, bestWordScore);
                    return bestWordMatch;
                }
            }

            // === Bước 3: Vietnamese-aware fuzzy matching ===
            // Bỏ dấu thanh trước khi so sánh: "Nguyễn" → "nguyen", "Trưởng" → "truong"
            string inputNorm  = RemoveVietnameseDiacritics(input.ToLowerInvariant());
            string bestMatch  = null;
            double bestScore  = 0;
            string bestMethod = "";

            foreach (var term in terms)
            {
                if (term.Length <= 1) continue;
                string termNorm = RemoveVietnameseDiacritics(term.ToLowerInvariant());

                double levSim = LevenshteinSimilarity(inputNorm, termNorm);
                double jacSim = SyllableJaccardSimilarity(inputNorm, termNorm);
                double score  = Math.Max(levSim, jacSim);
                string method = jacSim > levSim ? "Syllable" : "Levenshtein";

                if (score > bestScore)
                {
                    bestScore  = score;
                    bestMatch  = term;
                    bestMethod = method;
                    if (bestScore >= 0.90) break; // quá rõ ràng → dừng sớm
                }
            }

            // ★ Ngưỡng 55%: score tối thiểu để tránh false match
            // Ví dụ: "không" (1 âm tiết) sẽ không vô tình match "Không Khí Sạch VN" (40%)
            if (bestScore >= 0.55 && bestMatch != null)
            {
                Log.Information("[VoiceSearch] {M}: '{R}' → '{B}' ({S:P0})",
                    bestMethod, input, bestMatch, bestScore);
                return bestMatch;
            }

            // Sai lệch quá lớn → dùng TitleCase thay vì raw text
            var fallback = ApplyVietnameseTitleCase(rawResult);
            Log.Warning("[VoiceSearch] Không khớp grid (best='{M}', {S:P0}) → TitleCase: '{F}'",
                bestMatch ?? "(none)", bestScore, fallback);
            return fallback;
        }

        /// <summary>
        /// Title Case cho tiếng Việt: viết hoa chữ đầu mỗi từ.
        /// Web Speech API trả lowercase nên cần chuẩn hóa trước hiển thị.
        /// Ví dụ: "hồ thị tuyết hân" → "Hồ Thị Tuyết Hân"
        /// </summary>
        private static string ApplyVietnameseTitleCase(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var w = parts[i];
                if (w.Length == 0) continue;
                parts[i] = char.ToUpperInvariant(w[0]) + w.Substring(1);
            }
            return string.Join(" ", parts);
        }

        // ── Vietnamese Diacritics Normalization ─────────────────────────────────
        // Bỏ tất cả dấu thanh + dấu phụ tiếng Việt để so sánh "vần gốc".
        // "Nguyễn" → "nguyen", "Trùng" → "trung", "Phước" → "phuoc"
        // NFC trước để xử lý cả 2 dạng Unicode: Precomposed và Decomposed.

        private static readonly (string From, string To)[] _vnMap =
        {
            ("àáạảãâầấậẩẫăằắặẳẵÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴ", "a"),
            ("èéẹẻẽêềếệểễÈÉẸẺẼÊỀẾỆỂỄ", "e"),
            ("ìíịỉĩÌÍỊỈĨ", "i"),
            ("òóọỏõôồốộổỗơờớợởỡÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠ", "o"),
            ("ùúụủũưừứựửữÙÚỤỦŨƯỪỨỰỬỮ", "u"),
            ("ỳýỵỷỹỲÝỴỶỸ", "y"),
            ("đĐ", "d"),
        };

        private static string RemoveVietnameseDiacritics(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            input = input.Normalize(System.Text.NormalizationForm.FormC);
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                bool replaced = false;
                foreach (var (from, to) in _vnMap)
                {
                    if (from.IndexOf(c) >= 0)
                    {
                        sb.Append(to);
                        replaced = true;
                        break;
                    }
                }
                if (!replaced) sb.Append(c);
            }
            return sb.ToString();
        }

        // ── Syllable Jaccard Similarity ──────────────────────────────────────────
        // Tiếng Việt: mỗi từ = 1 âm tiết, cách nhau bởi dấu cách.
        // "trung nguyen" vs "chung nguyen" → syllables overlap = 1/3 = 33%
        // Mỗi âm tiết khớp nếu Levenshtein ≥ 70% → đếm matched / total.

        private static double SyllableJaccardSimilarity(string a, string b)
        {
            var sylA = a.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sylB = b.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (sylA.Length == 0 || sylB.Length == 0) return 0;

            int matchedA = 0;
            var usedB    = new bool[sylB.Length];

            foreach (var sa in sylA)
            {
                double bestSylSim = 0;
                int    bestIdx    = -1;
                for (int j = 0; j < sylB.Length; j++)
                {
                    if (usedB[j]) continue;
                    double sim = LevenshteinSimilarity(sa, sylB[j]);
                    if (sim > bestSylSim) { bestSylSim = sim; bestIdx = j; }
                }
                if (bestSylSim >= 0.70 && bestIdx >= 0)
                {
                    matchedA++;
                    usedB[bestIdx] = true;
                }
            }

            // Jaccard = matched / union (không đếm trùng)
            int union = sylA.Length + sylB.Length - matchedA;
            return union > 0 ? (double)matchedA / union : 0;
        }

        // ── Levenshtein Similarity ───────────────────────────────────────────────

        /// <summary>
        /// Levenshtein distance chuẩn hóa → similarity [0..1].
        /// 1.0 = giống hoàn toàn, 0.0 = khác hoàn toàn.
        /// </summary>
        private static double LevenshteinSimilarity(string a, string b)
        {
            if (a == b) return 1.0;
            if (a.Length == 0 || b.Length == 0) return 0.0;

            int la = a.Length, lb = b.Length;
            var d  = new int[la + 1, lb + 1];

            for (int i = 0; i <= la; i++) d[i, 0] = i;
            for (int j = 0; j <= lb; j++) d[0, j] = j;

            for (int i = 1; i <= la; i++)
            for (int j = 1; j <= lb; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }

            return 1.0 - (double)d[la, lb] / Math.Max(la, lb);
        }

        // =========================================================================
        // CANCEL / DISPOSE
        // =========================================================================

        public void Cancel()
        {
            _userCancelled = true;
            try { _recordCts?.Cancel(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Cancel();
            _webSpeech?.Dispose();
            _webSpeech = null;
            _model?.Dispose();
            _model = null;
            _guard.Dispose();
        }
    }
}
