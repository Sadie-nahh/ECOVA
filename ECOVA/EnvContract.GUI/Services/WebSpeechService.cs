using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnvContract.GUI.Services
{
    /// <summary>
    /// Voice recognition dùng Microsoft Edge WebView2 + Web Speech API (vi-VN).
    ///
    /// Các fix đã áp dụng:
    ///  1. Visible=true  → Chromium không coi là background page → SpeechRecognition hoạt động
    ///  2. sessionId     → Khi recognition.abort() được gọi, handler cũ (stale) sẽ bị skip
    ///                     → tránh ERROR:aborted làm resolve TCS sớm
    ///  3. no-speech retry → Tự retry 1 lần sau 500ms nếu user chưa kịp nói
    ///  4. Punctuation strip → Xoá dấu . , ! ? ; : thừa khỏi kết quả
    /// </summary>
    public class WebSpeechService : IDisposable
    {
        // ─────────────────────────────────────────────────────────────────────────
        // HTML + JavaScript (dùng string concat thay vì @"" để tránh lỗi quote)
        // ─────────────────────────────────────────────────────────────────────────
        private static readonly string SpeechHtml = BuildHtml();

        private static string BuildHtml()
        {
            // Viết HTML ra temp string rồi embed vào C# constant
            return
"<!DOCTYPE html><html><head><meta charset='UTF-8'></head><body><script>\n" +
"'use strict';\n" +
"var recognition    = null;\n" +
"var currentSession = 0;\n" +
"\n" +
"document.addEventListener('DOMContentLoaded', function() {\n" +
"    if (!window.chrome || !window.chrome.webview) return;\n" +
"    window.chrome.webview.postMessage('VIS:' + document.visibilityState);\n" +
"    function sendReady() {\n" +
"        var S = window.SpeechRecognition || window.webkitSpeechRecognition;\n" +
"        window.chrome.webview.postMessage(S ? 'READY:available' : 'READY:not_supported');\n" +
"    }\n" +
"    if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {\n" +
"        navigator.mediaDevices.getUserMedia({ audio: true })\n" +
"            .then(function(s) {\n" +
"                s.getTracks().forEach(function(t) { t.stop(); });\n" +
"                window.chrome.webview.postMessage('MIC_READY');\n" +
"                sendReady();\n" +
"            })\n" +
"            .catch(function(err) {\n" +
"                window.chrome.webview.postMessage('MIC_ERROR:' + err.name);\n" +
"                sendReady();\n" +
"            });\n" +
"    } else {\n" +
"        sendReady();\n" +
"    }\n" +
"});\n" +

"\n" +
"function startListening(retry) {\n" +
"    retry = retry || 0;\n" +
"\n" +
"    // FIX: sessionId — mỗi lần start là 1 session mới\n" +
"    // Handler cũ (từ recognition bị abort) kiểm tra sessionId trước khi làm gì\n" +
"    var mySession  = ++currentSession;\n" +
"    var isRetrying = false;\n" +
"\n" +
"    try {\n" +
"        var SR = window.SpeechRecognition || window.webkitSpeechRecognition;\n" +
"        if (!SR) { window.chrome.webview.postMessage('ERROR:not_supported'); return; }\n" +
"\n" +
        // Dừng recognition cũ — handler cũ sẽ thấy mySession !== currentSession → skip
"        if (recognition) { try { recognition.abort(); } catch(e) {} recognition = null; }\n" +

"\n" +
"        recognition = new SR();\n" +
"        recognition.lang            = 'vi-VN';\n" +
"        recognition.continuous      = true;\n" +
"        recognition.interimResults  = true;\n" +
"        recognition.maxAlternatives = 3;\n" +
"\n" +
"        recognition.onstart = function() {\n" +
"            if (mySession !== currentSession) return;\n" +
"            window.chrome.webview.postMessage('STARTED');\n" +
"        };\n" +
"\n" +
"        recognition.onresult = function(e) {\n" +
"            if (mySession !== currentSession) return;\n" +
"            try {\n" +
"                var t = '';\n" +
"                for (var i = e.resultIndex; i < e.results.length; i++) {\n" +
"                    if (e.results[i].isFinal) { t = e.results[i][0].transcript.trim(); break; }\n" +
"                }\n" +
"                // Xoá dấu câu thừa (artifact của speech recognition)\n" +
"                t = t.replace(/[.,!?;:\\u2026]+/g, ' ').replace(/\\s+/g, ' ').trim();\n" +
"                if (t) {\n" +
"                    window.chrome.webview.postMessage('RESULT:' + t);\n" +
"                    // continuous=true → cần stop thủ công sau khi có final result\n" +
"                    try { recognition.stop(); } catch(e2) {}\n" +
"                }\n" +
"            } catch(ex) {\n" +
"                window.chrome.webview.postMessage('ERROR:parse_' + ex.message);\n" +
"            }\n" +
"        };\n" +
"\n" +
"        recognition.onerror = function(e) {\n" +
"            // FIX: Bỏ qua hoàn toàn nếu session này đã bị thay thế (stale handler)\n" +
"            if (mySession !== currentSession) return;\n" +
"            // FIX: 'aborted' là do chúng ta tự gọi abort() — không phải lỗi thật\n" +
"            if (e.error === 'aborted') return;\n" +
"\n" +
"            // 'not-allowed': permission chưa kịp grant — retry sau 700ms (tối đa 2 lần)\n" +
"            if (e.error === 'not-allowed' && retry < 2) {\n" +
"                isRetrying = true;\n" +
"                window.chrome.webview.postMessage('RETRY:not_allowed_' + retry);\n" +
"                setTimeout(function() { startListening(retry + 1); }, 700);\n" +
"                return;\n" +
"            }\n" +
"            // 'no-speech': mic có thể chưa warm lại sau warm-up — retry 2 lần\n" +
"            if (e.error === 'no-speech' && retry < 2) {\n" +
"                isRetrying = true;\n" +
"                window.chrome.webview.postMessage('RETRY:no_speech');\n" +
"                setTimeout(function() { startListening(retry + 1); }, 500);\n" +
"                return;\n" +
"            }\n" +
"\n" +
"            window.chrome.webview.postMessage('ERROR:' + e.error);\n" +
"        };\n" +
"\n" +
"        recognition.onend = function() {\n" +
"            // FIX: Bỏ qua nếu session đã thay đổi hoặc đang retry\n" +
"            if (mySession !== currentSession) return;\n" +
"            if (!isRetrying) window.chrome.webview.postMessage('END');\n" +
"        };\n" +
"\n" +
"        recognition.onnomatch = function() {\n" +
"            if (mySession !== currentSession) return;\n" +
"            window.chrome.webview.postMessage('NOMATCH');\n" +
"        };\n" +
"\n" +
"        recognition.start();\n" +
"    } catch(ex) {\n" +
"        window.chrome.webview.postMessage('ERROR:ex_' + ex.message);\n" +
"    }\n" +
"}\n" +
"\n" +
"function stopListening() {\n" +
"    currentSession++; // Vô hiệu hoá mọi handler đang chờ\n" +
"    try { if (recognition) { recognition.stop(); recognition = null; } } catch(e) {}\n" +
"}\n" +
"</script></body></html>";
        }

        // ── State ─────────────────────────────────────────────────────────────────
        private WebView2   _webView;
        private bool       _initialized;
        private bool       _pageReady;
        private bool       _disposed;
        private bool       _gotResult;
        private string     _resultBuffer;

        private readonly SemaphoreSlim        _lock    = new SemaphoreSlim(1, 1);
        private TaskCompletionSource<bool>    _initTcs;
        private TaskCompletionSource<string>  _currentTcs;

        public bool IsReady => _initialized && _pageReady && !_disposed;

        // ── INIT ──────────────────────────────────────────────────────────────────
        public async Task InitAsync(Control parent)
        {
            if (_initialized || _disposed) return;
            Log.Information("[WebSpeech] ── Khởi tạo WebView2 ─────────────────────────");
            try
            {
                // Visible=true BẮT BUỘC: Chromium block SpeechRecognition trên background page
                _webView = new WebView2
                {
                    Visible  = true,
                    Size     = new System.Drawing.Size(1, 1),
                    Location = new System.Drawing.Point(0, 0)
                };
                parent.Controls.Add(_webView);
                _webView.BringToFront();
                Log.Information("[WebSpeech] [1/5] WebView2 control thêm vào {P}", parent.GetType().Name);

                var dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ECOVA", "WebView2Data");
                var htmlFolder = Path.Combine(dataFolder, "speech");
                Directory.CreateDirectory(htmlFolder);
                File.WriteAllText(Path.Combine(htmlFolder, "speech.html"), SpeechHtml, System.Text.Encoding.UTF8);
                Log.Information("[WebSpeech] [2/5] HTML ghi → speech.html ({L} chars)", SpeechHtml.Length);

                Log.Information("[WebSpeech] [3/5] CoreWebView2Environment.CreateAsync...");
                var env = await CoreWebView2Environment.CreateAsync(null, dataFolder);
                await _webView.EnsureCoreWebView2Async(env);
                Log.Information("[WebSpeech] [3/5] CoreWebView2 OK. Edge={V}",
                    _webView.CoreWebView2.Environment.BrowserVersionString);

                // Permission handler
                _webView.CoreWebView2.PermissionRequested += (_, e) =>
                {
                    Log.Information("[WebSpeech] PermissionRequested: {K} → Allow", e.PermissionKind);
                    e.State = CoreWebView2PermissionState.Allow;
                };

                _webView.CoreWebView2.NavigationCompleted += (_, e) =>
                {
                    if (e.IsSuccess) Log.Information("[WebSpeech] Navigation OK");
                    else Log.Error("[WebSpeech] Navigation FAILED: {E}", e.WebErrorStatus);
                };

                _webView.CoreWebView2.WebMessageReceived += OnWebMessage;

                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "ecova-voice.local", htmlFolder, CoreWebView2HostResourceAccessKind.Allow);
                Log.Information("[WebSpeech] [4/5] VirtualHost mapped: ecova-voice.local");

                _initTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Log.Information("[WebSpeech] [5/5] Navigate → https://ecova-voice.local/speech.html");
                _webView.CoreWebView2.Navigate("https://ecova-voice.local/speech.html");

                var winner = await Task.WhenAny(_initTcs.Task, Task.Delay(6000));
                _pageReady   = winner == _initTcs.Task && _initTcs.Task.Result;
                _initialized = true;

                if (_pageReady)
                    Log.Information("[WebSpeech] ✓ Edge Web Speech API (vi-VN) sẵn sàng!");
                else
                    Log.Warning("[WebSpeech] ✗ SpeechRecognition không khả dụng hoặc timeout 6s");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WebSpeech] ✗ Lỗi khởi tạo: {T}", ex.GetType().Name);
                _initialized = false;
                _pageReady   = false;
            }
            Log.Information("[WebSpeech] ── Kết thúc khởi tạo: IsReady={R}", IsReady);
        }

        // ── WEB MESSAGE HANDLER ───────────────────────────────────────────────────
        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var msg = e.TryGetWebMessageAsString();
            Log.Debug("[WebSpeech] JS→C#: [{M}]", msg);   // Debug để không spam log

            if (msg.StartsWith("READY:"))    { _initTcs?.TrySetResult(msg == "READY:available"); return; }
            if (msg == "MIC_READY")          { Log.Information("[WebSpeech] Mic pre-warmed ✓"); return; }
            if (msg.StartsWith("MIC_ERROR:"))
            {
                Log.Warning("[WebSpeech] Mic pre-warm lỗi: {E}", msg.Substring(10));
                return;
            }
            if (msg.StartsWith("VIS:"))
            {
                Log.Information("[WebSpeech] document.visibilityState={V}", msg.Substring(4));
                return;
            }
            if (msg.StartsWith("RETRY:"))
            {
                Log.Information("[WebSpeech] Auto-retry ({M})", msg);
                return; // Không resolve TCS — JS sẽ gọi lại startListening
            }
            if (msg.StartsWith("RESULT:"))
            {
                _gotResult    = true;
                _resultBuffer = msg.Substring(7).Trim();
                Log.Information("[WebSpeech] ✓ Kết quả: \"{T}\"", _resultBuffer);
                return;
            }
            if (msg == "END" || msg == "NOMATCH")
            {
                if (msg == "NOMATCH") Log.Warning("[WebSpeech] no-match");
                _currentTcs?.TrySetResult(_gotResult ? _resultBuffer : string.Empty);
                return;
            }
            if (msg.StartsWith("ERROR:"))
            {
                Log.Warning("[WebSpeech] Lỗi JS: [{E}]", msg.Substring(6));
                _currentTcs?.TrySetResult(string.Empty);
                return;
            }
            if (msg == "STARTED")
                Log.Information("[WebSpeech] 🔴 Đang lắng nghe...");
        }

        // ── LISTEN ────────────────────────────────────────────────────────────────
        public async Task<string> ListenAsync(int timeoutMs = 10_000, CancellationToken ct = default)
        {
            if (!IsReady)
            {
                Log.Warning("[WebSpeech] Chưa ready (init={I}, page={P})", _initialized, _pageReady);
                return string.Empty;
            }
            if (!await _lock.WaitAsync(0))
            {
                Log.Warning("[WebSpeech] Đang bận (concurrent call)");
                return string.Empty;
            }
            try
            {
                _gotResult    = false;
                _resultBuffer = string.Empty;
                _currentTcs   = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

                using var reg = ct.Register(() =>
                {
                    Log.Information("[WebSpeech] Hủy bởi CancellationToken");
                    _ = StopAsync();
                    _currentTcs?.TrySetResult(string.Empty);
                });

                Log.Information("[WebSpeech] Gọi startListening()...");
                await _webView.ExecuteScriptAsync("startListening()");

                // Chờ kết quả (bao gồm cả thời gian retry trong JS)
                var winner = await Task.WhenAny(_currentTcs.Task, Task.Delay(timeoutMs));
                if (winner != _currentTcs.Task)
                {
                    await StopAsync();
                    Log.Warning("[WebSpeech] Timeout {T}ms — dừng lắng nghe", timeoutMs);
                    await Task.Delay(400);
                    return _currentTcs.Task.IsCompleted ? _currentTcs.Task.Result : string.Empty;
                }

                var result = _currentTcs.Task.Result;
                Log.Information("[WebSpeech] ListenAsync trả về: \"{R}\"", result);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WebSpeech] Lỗi ListenAsync");
                return string.Empty;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task StopAsync()
        {
            try
            {
                if (_webView != null && !_disposed)
                    await _webView.ExecuteScriptAsync("stopListening()");
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WebSpeech] StopAsync error: {ex.Message}"); }
        }

        // ── CLEANUP ───────────────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed    = true;
            _initialized = false;
            _pageReady   = false;
            try { _webView?.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WebSpeech] StopAsync error: {ex.Message}"); }
            try { _lock?.Dispose(); }    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WebSpeech] StopAsync error: {ex.Message}"); }
        }
    }
}
