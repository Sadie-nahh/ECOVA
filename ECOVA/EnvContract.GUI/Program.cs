using Microsoft.Extensions.DependencyInjection;
using System.Text;
using Microsoft.Extensions.Configuration;
using EnvContract.BLL.Interfaces;
using EnvContract.BLL.Services;
using EnvContract.DAL.Interfaces;
using EnvContract.DAL.Repositories;
using EnvContract.GUI.Forms.Auth;
using EnvContract.Common.Helpers;
using Serilog;
using System.Windows.Forms;
using System;
using System.Linq;

namespace EnvContract.GUI
{
    internal static class Program
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        public static System.Drawing.Icon AppIcon { get; private set; }

        /// <summary>
        /// True nếu DPAPI decrypt mật khẩu SMTP thất bại (máy khác, user khác).
        /// SmtpSetupForm sẽ hiện lên khi admin mở chức năng gửi mail.
        /// </summary>
        public static bool SmtpDecryptFailed { get; private set; } = false;

        /// <summary>Username SMTP đọc từ appsettings (dùng để pre-fill SmtpSetupForm).</summary>
        public static string SmtpUsernameFromConfig { get; private set; } = string.Empty;

        [STAThread]
        static void Main()
        {
            // ── Cấu hình Serilog ──────────────────────────────────────────────
            // Ghi log ra: Console + file xoay vòng theo ngày (tối đa 10MB, giữ 7 file)
            string logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    path:              System.IO.Path.Combine(logDir, "ecova-.log"),
                    rollingInterval:   RollingInterval.Day,
                    fileSizeLimitBytes: 10 * 1024 * 1024,   // 10 MB
                    retainedFileCountLimit: 7,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Khởi tạo AppLogger wrapper (dùng khắp nơi trong app)
            AppLogger.Initialize(Log.Logger);
            AppLogger.Info("ECOVA khởi động.");

            // ── Global unhandled exception handlers ────────────────────────────
            // Catch exception trên UI thread → log + hiện MessageBox thân thiện
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                AppLogger.Fatal("[CRASH] Unhandled UI exception", e.Exception);
                var LM = EnvContract.Common.LanguageManager.Instance;
                MessageBox.Show(
                    string.Format(LM.Get("sys_unhandled_desc"), e.Exception.Message),
                    LM.Get("sys_startup_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            // Catch exception trên background thread (Task, Thread)
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                AppLogger.Fatal("[CRASH] Unhandled background exception", ex);
            };

            ApplicationConfiguration.Initialize();

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Đọc SMTP settings từ appsettings.json và cấu hình EmailSmtpHelper
            ConfigureEmailSettings();

            // Admin password được thiết lập đúng từ SeedData.sql với BCrypt hash.
            // KHÔNG auto-reset runtime — đây là security antipattern:
            //   - Reset về "admin" mỗi lần khởi động nếu hash không khớp.
            //   - Tấn công vật lý có thể khai thác cửa sổ giữa startup và login.
            // Nếu cần reset admin: dùng script SQL hoặc tool quản trị riêng.

            // ── Set Icon mặc định cho tất cả Form ──────────────────────────
            // Ưu tiên load từ output directory (được copy bởi csproj Content)
            string appIconPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, @"assets\images\Icon.ico");
            // Fallback: đường dẫn cũ (khi chạy debug từ IDE)
            if (!System.IO.File.Exists(appIconPath))
                appIconPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\assets\images\Icon.ico");
            if (System.IO.File.Exists(appIconPath))
                AppIcon = new System.Drawing.Icon(appIconPath, 128, 128);

            var loginForm = ServiceProvider.GetRequiredService<EnvContract.GUI.Forms.Auth.Login>();
            if (AppIcon != null) loginForm.Icon = AppIcon;

            Application.Run(loginForm);

            // Dọn dẹp tài nguyên trước khi tắt app
            EnvContract.Common.Helpers.EmailSmtpHelper.Shutdown();   // Dispose singleton SmtpClient
            AppLogger.Info("ECOVA đóng.");
            Log.CloseAndFlush();
        }


        /// <summary>
        /// Đọc cài đặt SMTP từ appsettings.json và cấu hình EmailSmtpHelper.
        /// Ưu tiên: AES (mọi máy) → DPAPI (auto-migrate sang AES) → Plaintext.
        /// </summary>
        private static void ConfigureEmailSettings()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .Build();

                var smtp       = config.GetSection("SmtpSettings");
                string rawPwd  = smtp["Password"]    ?? "";
                bool isEnc     = bool.TryParse(smtp["PasswordEncrypted"], out var enc) && enc;
                string username = smtp["Username"]   ?? "";
                string server   = smtp["Server"]     ?? "smtp.gmail.com";
                int    port     = int.TryParse(smtp["Port"], out var p) ? p : 587;
                bool   ssl      = bool.TryParse(smtp["EnableSsl"], out var s) ? s : true;
                string dispName = smtp["DisplayName"] ?? "ECOVA System (No Reply)";

                SmtpUsernameFromConfig = username;

                string password = ResolveSmtpPassword(rawPwd, isEnc);

                EmailSmtpHelper.Configure(
                    server:      server,
                    port:        port,
                    username:    username,
                    password:    password,
                    enableSsl:   ssl,
                    displayName: dispName);

                if (EmailSmtpHelper.IsConfigured)
                    AppLogger.Info($"Startup: SMTP configured — {username} @ {server}:{port}");
                else if (!SmtpDecryptFailed)
                    AppLogger.Warning("Startup: SMTP not configured — Username/Password empty.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Startup: Cannot read SMTP settings", ex);
            }
        }

        /// <summary>
        /// Giải mã mật khẩu SMTP theo thứ tự ưu tiên:
        ///   1. AES  (prefix "AES:") — cross-machine, hoạt động mọi máy tự động.
        ///   2. DPAPI legacy         — machine-specific, tự động migrate sang AES.
        ///   3. Plaintext            — dùng thẳng.
        /// </summary>
        private static string ResolveSmtpPassword(string rawPwd, bool isEncrypted)
        {
            if (!isEncrypted || string.IsNullOrEmpty(rawPwd))
                return rawPwd;

            // ── 1. AES (cross-machine) ────────────────────────────────────────
            if (AesHelper.IsAesCipherText(rawPwd))
            {
                string result = AesHelper.Decrypt(rawPwd);
                if (result != null)
                {
                    SmtpDecryptFailed = false;
                    AppLogger.Info("Startup: SMTP — AES decrypt OK (cross-machine, no config needed).");
                    return result;
                }
                SmtpDecryptFailed = true;
                AppLogger.Warning("Startup: SMTP — AES decrypt failed (data corrupt?).");
                return string.Empty;
            }

            // ── 2. DPAPI legacy → auto-migrate to AES ────────────────────────
            string dpapi = DpapiHelper.Decrypt(rawPwd);
            if (dpapi != null)
            {
                AppLogger.Info("Startup: SMTP — DPAPI OK. Auto-migrating to AES (1-time)...");
                AutoMigrateToAes(dpapi);
                SmtpDecryptFailed = false;
                return dpapi;
            }

            // ── 3. DPAPI failed — different machine (show SmtpSetupForm) ─────
            SmtpDecryptFailed = true;
            AppLogger.Warning("Startup: SMTP — DPAPI failed (different machine). " +
                              "Admin: click 'Gui Mail' -> 'Cai dat SMTP' to configure once.");
            return string.Empty;
        }

        /// <summary>
        /// Ghi lại appsettings.json với mật khẩu AES thay vì DPAPI.
        /// Chỉ chạy 1 lần trên máy gốc. Sau đó file dùng được trên MỌI máy.
        /// </summary>
        private static void AutoMigrateToAes(string plainPassword)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!System.IO.File.Exists(path)) return;

                string json = System.IO.File.ReadAllText(path, Encoding.UTF8);
                var root    = Newtonsoft.Json.Linq.JObject.Parse(json);
                if (root["SmtpSettings"] is not Newtonsoft.Json.Linq.JObject smtpSec) return;

                smtpSec["Password"]          = AesHelper.Encrypt(plainPassword);
                smtpSec["PasswordEncrypted"] = true;

                System.IO.File.WriteAllText(
                    path,
                    root.ToString(Newtonsoft.Json.Formatting.Indented),
                    Encoding.UTF8);

                AppLogger.Info("Startup: SMTP — Migrated DPAPI → AES. " +
                               "appsettings.json now works on ALL machines automatically.");
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Startup: Auto-migrate failed (non-critical): {ex.Message}");
            }
        }


        private static void ConfigureServices(IServiceCollection services)
        {
            // Logger (singleton — inject qua IAppLogger trong form/service mới)
            services.AddSingleton<IAppLogger>(new AppLogger());

            // Repositories
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<IContractRepository, ContractRepository>();
            services.AddTransient<ICustomerRepository, CustomerRepository>();
            services.AddTransient<IOrderRepository, OrderRepository>();
            services.AddTransient<IEmployeeRepository, EmployeeRepository>();
            services.AddTransient<ISampleRepository, SampleRepository>();
            services.AddTransient<ITestResultRepository, TestResultRepository>();
            services.AddTransient<IAuditLogRepository, AuditLogRepository>();
            services.AddTransient<IStandardParameterRepository, StandardParameterRepository>();
            services.AddTransient<ISamplingPlanRepository, SamplingPlanRepository>();
            
            // Services & Validators
            services.AddSingleton<EnvContract.BLL.Services.AiIntegrationService>();
            services.AddTransient<IUserBLL, UserBLL>();
            services.AddTransient<IContractService, ContractService>();
            services.AddTransient<ICustomerService, CustomerService>();
            services.AddTransient<IEmployeeService, EmployeeService>();
            services.AddTransient<IPlanningService, PlanningService>();
            services.AddTransient<ITestingService, TestingService>();
            services.AddTransient<EnvContract.BLL.Validators.ContractValidator>();
            services.AddTransient<EnvContract.BLL.Validators.ResultValidator>();

            services.AddTransient<IExportService, ExportService>();
            services.AddTransient<INotificationService, NotificationService>();
            services.AddTransient<IApprovalService, ApprovalService>();
            services.AddTransient<IAuthService, AuthService>();
            services.AddSingleton<EnvContract.GUI.Services.VoiceSearchService>();

            // Forms
            services.AddTransient<Login>();
            services.AddTransient<Forms.Main.MainForm>();
            
            // UserControls (Sales SPA)
            services.AddTransient<EnvContract.GUI.UserControls.Sales.CustomerManagementUC>();
            services.AddTransient<EnvContract.GUI.UserControls.Sales.ContractListUC>();
            
            // UserControls (Phase 3 & 4 & 5 SPA)
            services.AddTransient<EnvContract.GUI.UserControls.Dashboards.DashboardUC>();
            services.AddTransient<EnvContract.GUI.UserControls.Admin.EmployeeManagementUC>();
            services.AddTransient<EnvContract.GUI.UserControls.Planning.SampleConfigUC>();
            services.AddTransient<EnvContract.GUI.UserControls.Planning.ParameterConfigUC>();
            services.AddTransient<EnvContract.GUI.UserControls.FieldAndLab.EnterResultUC>();
            services.AddTransient<EnvContract.GUI.UserControls.FieldAndLab.LabResultUC>();
            services.AddTransient<EnvContract.GUI.UserControls.QAAndDirector.DirectorApprovalUC>();
            services.AddTransient<EnvContract.GUI.UserControls.Notification.NotificationUC>();
        }
    }
}
