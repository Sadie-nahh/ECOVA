using Serilog;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EnvContract.GUI.Services
{
    /// <summary>
    /// Quản lý Vosk Vietnamese model cho nhận diện tiếng Việt offline.
    ///
    /// Model: vosk-model-vn-0.4 (~78MB zip, ~200MB unzipped)
    /// Lưu tại: %APPDATA%\ECOVA\vosk-model-vn\
    /// Chỉ download 1 lần. Sau đó hoàn toàn offline.
    ///
    /// IsReady: kiểm tra am/final.mdl tồn tại (cấu trúc chuẩn Vosk model).
    /// </summary>
    public static class VoiceModelManager
    {
        private const string ModelUrl =
            "https://alphacephei.com/vosk/models/vosk-model-vn-0.4.zip";

        /// <summary>Thư mục chứa Vosk model đã giải nén.</summary>
        public static readonly string ModelDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ECOVA", "vosk-model-vn");

        /// <summary>File xác nhận model hợp lệ (acoustic model).</summary>
        private static string FinalMdlPath => Path.Combine(ModelDir, "am", "final.mdl");

        /// <summary>Kích thước ZIP tối thiểu hợp lệ: 50 MB.</summary>
        private const long MinZipBytes = 50_000_000L;

        /// <summary>true nếu model đã giải nén và hợp lệ (am/final.mdl tồn tại).</summary>
        public static bool IsReady => File.Exists(FinalMdlPath);

        /// <summary>
        /// Đảm bảo model đã sẵn sàng. Download + giải nén nếu chưa có.
        /// onProgress(bytesReceived, totalBytes) — totalBytes = -1 nếu server không gửi Content-Length.
        /// onStatus(message) — thông báo trạng thái (download / giải nén).
        /// </summary>
        public static async Task EnsureReadyAsync(
            Action<long, long> onProgress = null,
            Action<string> onStatus = null,
            CancellationToken ct = default)
        {
            if (IsReady)
            {
                Log.Information("[VoiceModel] Vosk model đã sẵn sàng: {D}", ModelDir);
                return;
            }

            // Tạo thư mục cha nếu chưa có
            Directory.CreateDirectory(Path.GetDirectoryName(ModelDir)!);

            Log.Information("[VoiceModel] Bắt đầu download Vosk VN model (~78MB) từ AlphaCephei...");

            string tempZip = ModelDir + ".zip.downloading";
            string tempExt = ModelDir + ".zip.extracting";

            try
            {
                // ── Bước 1: Download ZIP ─────────────────────────────────
                onStatus?.Invoke("Đang tải model Vosk tiếng Việt...");
                await DownloadAsync(ModelUrl, tempZip, onProgress, ct);

                // Validate ZIP size
                long downloaded = new FileInfo(tempZip).Length;
                if (downloaded < MinZipBytes)
                    throw new IOException(
                        $"File ZIP không đủ kích thước: {downloaded / 1_048_576.0:F1} MB < 50 MB. " +
                        "Có thể bị gián đoạn mạng.");

                Log.Information("[VoiceModel] Download xong ({S:F1} MB) — bắt đầu giải nén...",
                    downloaded / 1_048_576.0);

                // ── Bước 2: Giải nén ─────────────────────────────────────
                onStatus?.Invoke("Đang giải nén model...");

                // Xóa thư mục giải nén cũ nếu có
                if (Directory.Exists(tempExt)) Directory.Delete(tempExt, recursive: true);
                if (Directory.Exists(ModelDir)) Directory.Delete(ModelDir, recursive: true);

                await Task.Run(() =>
                    ZipFile.ExtractToDirectory(tempZip, tempExt),
                    ct);

                // ZIP giải nén vào thư mục con: tempExt/vosk-model-vn-0.4/
                // Tìm thư mục con đầu tiên và move thành ModelDir
                var subDirs = Directory.GetDirectories(tempExt);
                if (subDirs.Length == 0)
                    throw new IOException("ZIP không chứa thư mục model hợp lệ.");

                Directory.Move(subDirs[0], ModelDir);

                // Validate sau giải nén
                if (!IsReady)
                    throw new IOException(
                        "Giải nén thành công nhưng không tìm thấy am/final.mdl. " +
                        "Model ZIP có thể bị hỏng.");

                Log.Information("[VoiceModel] ✓ Model sẵn sàng: {D}", ModelDir);
            }
            catch (OperationCanceledException)
            {
                TryCleanup(tempZip, tempExt);
                Log.Warning("[VoiceModel] Download bị hủy bởi người dùng.");
                throw;
            }
            catch (Exception ex)
            {
                TryCleanup(tempZip, tempExt);
                Log.Error(ex, "[VoiceModel] Lỗi download/giải nén model");
                throw;
            }
            finally
            {
                // Luôn xóa file ZIP tạm (kể cả khi thành công)
                TryDelete(tempZip);
                if (Directory.Exists(tempExt))
                    try { Directory.Delete(tempExt, recursive: true); } catch { }
            }
        }

        // ── Download stream với buffer 80KB ──────────────────────────────────────

        private static async Task DownloadAsync(
            string url, string destPath,
            Action<long, long> onProgress,
            CancellationToken ct)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.Add("User-Agent", "ECOVA-VoiceSearch/3.0");

            using var response = await client.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long total    = response.Content.Headers.ContentLength ?? -1;
            long received = 0;
            const int BufSize = 81920; // 80 KB chunks

            using var netStream  = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(destPath,
                FileMode.Create, FileAccess.Write, FileShare.None,
                BufSize, useAsync: true);

            var buffer = new byte[BufSize];
            int read;
            while ((read = await netStream.ReadAsync(buffer.AsMemory(0, BufSize), ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;
                onProgress?.Invoke(received, total);
            }

            await fileStream.FlushAsync(ct);
        }

        private static void TryCleanup(string zipPath, string extractDir)
        {
            TryDelete(zipPath);
            if (Directory.Exists(extractDir))
                try { Directory.Delete(extractDir, recursive: true); } catch { }
            if (Directory.Exists(ModelDir))
                try { Directory.Delete(ModelDir, recursive: true); } catch { }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
