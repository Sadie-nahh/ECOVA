using System;
using System.Collections.Generic;
using System.IO;

namespace EnvContract.Common
{
    public enum AppLanguage { Vietnamese, English }

    /// <summary>
    /// Singleton quản lý ngôn ngữ ứng dụng (VI/EN).
    /// Persist lựa chọn vào file cấu hình.
    /// </summary>
    public sealed class LanguageManager
    {
        private static readonly Lazy<LanguageManager> _lazy =
            new Lazy<LanguageManager>(() => new LanguageManager());

        public static LanguageManager Instance => _lazy.Value;

        private AppLanguage _currentLanguage;

        /// <summary>Sự kiện khi ngôn ngữ thay đổi — UI subscribe để cập nhật text.</summary>
        public event Action? LanguageChanged;


        public AppLanguage CurrentLanguage
        {
            get => _currentLanguage;
            private set => _currentLanguage = value;
        }

        public bool IsVietnamese => _currentLanguage == AppLanguage.Vietnamese;
        public bool IsEnglish => _currentLanguage == AppLanguage.English;

        private readonly Dictionary<string, string> _vi = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _en = new Dictionary<string, string>();

        private static string SettingsFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ecova_lang.cfg");

        private LanguageManager()
        {
            BuildDictionaries();
            _currentLanguage = LoadPersistedLanguage();
        }

        /// <summary>Chuyển đổi VI ↔ EN, lưu persist, phát sự kiện.</summary>
        public void ToggleLanguage()
        {
            _currentLanguage = _currentLanguage == AppLanguage.Vietnamese
                ? AppLanguage.English
                : AppLanguage.Vietnamese;
            PersistLanguage(_currentLanguage);
            LanguageChanged?.Invoke();
        }

        /// <summary>Đặt ngôn ngữ cụ thể.</summary>
        public void SetLanguage(AppLanguage lang)
        {
            if (_currentLanguage == lang) return;
            _currentLanguage = lang;
            PersistLanguage(_currentLanguage);
            LanguageChanged?.Invoke();
        }

        /// <summary>Tra cứu text theo key + ngôn ngữ hiện tại.</summary>
        public string Get(string key)
        {
            var dict = _currentLanguage == AppLanguage.Vietnamese ? _vi : _en;
            return dict.TryGetValue(key, out var val) ? val : $"[{key}]";
        }

        /// <summary>Label hiển thị trên nút toggle (ngôn ngữ ĐÍCH khi click).</summary>
        public string ToggleLabel => _currentLanguage == AppLanguage.Vietnamese ? "EN" : "VI";

        // ══════════════════════════════════════════════════════════════════
        // PERSIST
        // ══════════════════════════════════════════════════════════════════

        private AppLanguage LoadPersistedLanguage()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var val = File.ReadAllText(SettingsFilePath).Trim();
                    if (val == "EN") return AppLanguage.English;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[i18n] LoadPersistedLanguage error: {ex.Message}");
                // Fallback to Vietnamese — non-critical
            }
            return AppLanguage.Vietnamese;
        }

        private void PersistLanguage(AppLanguage lang)
        {
            try
            {
                File.WriteAllText(SettingsFilePath, lang == AppLanguage.English ? "EN" : "VI");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[i18n] PersistLanguage error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // DICTIONARY
        // ══════════════════════════════════════════════════════════════════

        private void BuildDictionaries()
        {
            // ── Login ─────────────────────────────────────────────────────
            Add("form_title",             "ECOVA - Hệ Thống Quản Lý Quan Trắc",           "ECOVA - Environmental Monitoring System");
            Add("login_title",            "ĐĂNG NHẬP",                                     "LOGIN");
            Add("login_subtitle",         "Quản lý thông minh cho môi trường bền vững",    "Smart management for sustainable environment");
            Add("login_username_placeholder", "Nhập tên tài khoản...",                     "Enter username...");
            Add("login_password_placeholder", "Nhập mật khẩu...",                          "Enter password...");
            Add("login_submit",           "Đăng nhập",                                     "Login");
            Add("login_faceid",           "Đăng nhập bằng Face ID",                        "Login with Face ID");
            Add("login_forgot",           "Quên mật khẩu?",                                "Forgot password?");
            Add("login_or",              "HOẶC",                                            "OR");
            Add("login_locked",          "Tài khoản bị tạm khóa. Thử lại sau",             "Account temporarily locked. Try again after");
            Add("login_intro_btn",       "Đăng nhập",                         "Login to the System");

            // ── MainForm / Sidebar ────────────────────────────────────────
            Add("sidebar_dashboard",     "Tổng Quan",                                      "Dashboard");
            Add("sidebar_employee",      "Quản Lý Nhân Viên",                              "Employee Management");
            Add("sidebar_customer",      "Phòng Kinh Doanh",                               "Sales Department");
            Add("sidebar_planning",      "Phòng Kế Hoạch",                                 "Planning Department");
            Add("sidebar_field",         "Phòng Hiện Trường",                              "Field Department");
            Add("sidebar_lab",           "Phòng Thí Nghiệm",                               "Lab Department");
            Add("sidebar_result",        "Phòng Kết Quả",                                  "Results Department");
            Add("sidebar_notification",  "Thông Báo",                                      "Notifications");
            Add("sidebar_logout",        "Đăng Xuất",                                      "Logout");
            Add("sidebar_font_size",     "11.25",                                           "10.5");
            Add("logout_confirm",        "Bạn có chắc chắn muốn đăng xuất?",               "Are you sure you want to logout?");
            Add("confirm",              "Xác nhận",                                         "Confirm");

            // ── Common System Messages ────────────────────────────────────
            Add("msg_cancel",            "Hủy",                                             "Cancel");
            Add("msg_error",             "Lỗi",                                             "Error");
            Add("msg_warning",           "Cảnh báo",                                        "Warning");
            Add("msg_success",           "Thành công",                                      "Success");
            Add("msg_info",              "Thông báo",                                       "Information");
            Add("msg_loading",           "Đang xử lý...",                                   "Processing...");
            Add("ui_error_title",        "Lỗi UI",                                          "UI Error");
            Add("ui_error_desc",         "Lỗi hiển thị màn hình mờ: ",                       "Overlay error: ");

            // ── System Startup & App State ────────────────────────────────
            Add("sys_startup_error",     "Lỗi Hệ Thống",                                    "System Error");
            Add("sys_unhandled_desc",    "Đã xảy ra lỗi không xác định:\n{0}\n\nDữ liệu đã được ghi log. Vui lòng liên hệ quản trị viên.", "An unknown error occurred:\n{0}\n\nData has been logged. Please contact administrator.");
            Add("main_timeout_title",    "Hết Phiên",                                       "Session Expired");
            Add("main_timeout_msg",      "Không có hoạt động nào trong {0} phút. Hệ thống tự động đăng xuất để bảo mật.", "No activity for {0} minutes. Auto logged out for security.");

            // ── Voice Download & Speech ───────────────────────────────────
            Add("voz_title",             "ECOVA — Cài đặt Voice Search (Vosk AI)",          "ECOVA — Install Voice Search (Vosk AI)");
            Add("voz_header",            "🎤  Cài đặt nhận diện giọng nói tiếng Việt",      "🎤  Install Vietnamese Voice Recognition");
            Add("voz_desc",              "Đang tải Vosk AI model tiếng Việt (~78 MB) — Nhỏ gọn, phản hồi nhanh.\nChỉ tải 1 lần duy nhất. Sau đó hoạt động hoàn toàn OFFLINE.", "Downloading Vosk AI Vietnamese model (~78 MB) — Compact, fast response.\nDownload only once. Works completely OFFLINE.");
            Add("voz_status_connect",    "Đang kết nối...",                                 "Connecting...");
            Add("voz_status_down",       "Đang tải model Vosk tiếng Việt...",               "Downloading Vietnamese Vosk model...");
            Add("voz_status_partial",    "{0} MB đã tải",                                   "{0} MB downloaded");
            Add("voz_status_done",       "Hoàn tất! Đang khởi động nhận diện...",           "Completed! Starting recognition...");
            Add("voz_error_desc",        "Lỗi tải model Vosk:\n{0}\n\nVui lòng kiểm tra kết nối internet và thử lại.", "Error downloading Vosk model:\n{0}\n\nPlease check your internet connection and try again.");
            Add("voz_error_title",       "Lỗi tải Voice Model",                             "Voice Model Download Error");
            Add("voz_mic_ready",         "🎤 Micro đã sẵn sàng!",                           "🎤 Voice Search ready!");
            Add("voz_error_lbl",         "Lỗi Voice Search",                                "Voice Search Error");
            Add("voz_error_desc2",       "Kiểm tra kết nối internet (Edge) hoặc: ",         "Check internet connection (Edge) or: ");
            
            Add("voz_tip_title",         "🎙️ Tìm kiếm bằng giọng nói tiếng Việt",           "🎙️ Vietnamese Voice Search");
            Add("voz_tip_instr",         "Nhấn để bắt đầu ghi âm • Im lặng để tự dừng",    "Click to start recording • Silence to auto-stop");
            Add("voz_tip_engine",        "[Edge Web Speech API — chuyển giọng nói thành văn bản tiếng Việt có dấu]", "[Edge Web Speech API — Vietnamese Speech-to-Text]");
            Add("voz_hint_click_again",  "Nhấn nút Mic một lần nữa\nrồi nói to tên cần tìm kiếm.", "Click Mic button again\nand speak the search term clearly.");

            // ── Phase 2: Admin - EmployeeAddEditForm ──────────────────────
            Add("admin_emp_title_add",   "THÊM NHÂN VIÊN",                                  "ADD EMPLOYEE");
            Add("admin_emp_title_edit",  "SỬA NHÂN VIÊN",                                   "EDIT EMPLOYEE");
            Add("admin_emp_ava",         "Nhấn để chọn ảnh đại diện",                       "Click to choose avatar");
            Add("admin_emp_ava_title",   "Chọn ảnh đại diện",                               "Choose avatar image");
            Add("admin_emp_name",        "Họ và tên",                                       "Full Name");
            Add("admin_emp_name_ph",     "Nhập tên nhân viên....",                          "Enter employee name...");
            Add("admin_emp_dept",        "Phòng ban",                                       "Department");
            Add("admin_emp_dob",         "Ngày sinh",                                       "Date of Birth");
            Add("admin_emp_phone",       "Điện thoại",                                      "Phone");
            Add("admin_emp_phone_ph",    "Nhập số điện thoại...",                           "Enter phone number...");
            Add("admin_emp_id",          "ID Nhân viên",                                    "Employee ID");
            Add("admin_emp_id_ph",       "Đang tạo mã...",                                  "Generating code...");
            Add("admin_emp_email",       "Gmail",                                           "Email");
            Add("admin_emp_email_ph",    "Nhập địa chỉ mail....",                           "Enter email address...");
            Add("admin_emp_addr",        "Địa chỉ",                                         "Address");
            Add("admin_emp_addr_ph",     "Nhập địa chỉ....",                                "Enter address...");
            Add("msg_save",              "Lưu",                                             "Save");
            Add("admin_emp_err_load",    "Lỗi tải thông tin nhân viên: ",                   "Error loading employee info: ");
            Add("admin_emp_err_gen",     "Lỗi sinh mã nhân viên: ",                         "Error generating employee code: ");
            Add("admin_emp_req_name",    "Vui lòng nhập Họ và tên!",                        "Please enter Full Name!");
            Add("admin_emp_req_email",   "Vui lòng nhập Gmail để gửi thông tin tài khoản!", "Please enter email to send account info!");
            Add("admin_emp_add_succ",    "Thêm nhân viên thành công!\n\n═══ Thông tin tài khoản ═══\nUsername: {0}\nMật khẩu: {1}\nMã NV: {2}\n\n📧 Thông tin đã được gửi tới: {3}", "Employee added successfully!\n\n═══ Account Info ═══\nUsername: {0}\nPassword: {1}\nEmp Code: {2}\n\n📧 Info sent to: {3}");
            Add("admin_emp_upd_succ",    "Cập nhật nhân viên thành công!",                  "Employee updated successfully!");
            Add("admin_emp_err_dup",     "Email hoặc tên đăng nhập đã tồn tại trong hệ thống.\nVui lòng kiểm tra lại thông tin nhân viên.", "Email or username already exists.\nPlease check employee info.");
            Add("admin_emp_err_save",    "Lỗi lưu dữ liệu: ",                               "Error saving data: ");

            // ── Phase 2: Admin - SendMailForm & SmtpSetupForm ─────────────
            Add("mail_sys_title",        "Hệ thống Gửi Email",                              "Email System");
            Add("mail_recipient",        "Người nhận",                                      "Recipients");
            Add("mail_selected",         "Đã chọn:",                                        "Selected:");
            Add("mail_subject",          "Tiêu đề",                                         "Subject");
            Add("mail_ph_subject",       "Nhập tiêu đề email...",                           "Enter email subject...");
            Add("mail_body",             "Nội dung",                                        "Body");
            Add("mail_close_btn",        "Đóng",                                            "Close");
            Add("mail_send_btn",         "Gửi Mail",                                        "Send Mail");
            Add("mail_btn_cancel_send",  "Dừng",                                            "Stop");
            Add("mail_err_no_email",     "(chưa có email)",                                 "(no email)");
            Add("mail_err_load",         "Không thể tải danh sách nhân viên: ",             "Cannot load employee list: ");
            Add("mail_err_no_recip",     "Vui lòng chọn ít nhất 1 người nhận có email hợp lệ.", "Please select at least 1 valid recipient.");
            Add("mail_err_no_subj",      "Vui lòng nhập tiêu đề email.",                    "Please enter email subject.");
            Add("mail_err_no_body",      "Vui lòng nhập nội dung email.",                   "Please enter email body.");
            Add("mail_confirm_title",    "Xác nhận gửi",                                    "Confirm sending");
            Add("mail_confirm_msg",      "Bạn chuẩn bị gửi email đến {0} người nhận.\nTiếp tục?", "You are about to send email to {0} recipients.\nContinue?");
            Add("mail_sending",          "Đang gửi... {0}/{1}",                             "Sending... {0}/{1}");
            Add("mail_done",             "Hoàn thành! Đã gửi thành công {0}/{1} email.",    "Completed! Successfully sent {0}/{1} emails.");
            Add("mail_btn_done",         "Đã gửi xong",                                     "Sent successfully");
            Add("mail_done_detail",      "Xong: {0} thành công, {1} thất bại.",             "Done: {0} success, {1} failed.");
            Add("mail_fail_title",       "Email thất bại",                                  "Email Failed");
            Add("mail_err_sending",      "Có lỗi xảy ra khi gửi.",                          "Error occurred while sending.");
            
            Add("smtp_title",            "Cấu hình SMTP — ECOVA",                           "SMTP Configuration — ECOVA");
            Add("smtp_head",             "Cấu hình Email SMTP",                             "SMTP Email Configuration");
            Add("smtp_warn",             "Mật khẩu SMTP được mã hóa trên máy khác. Vui lòng nhập lại để dùng trên máy này.", "SMTP password was encrypted on another machine. Please re-enter to use it here.");
            Add("smtp_port",             "Cổng (Port)",                                     "Port");
            Add("smtp_acc",              "Tài khoản email (From)",                          "Email Account (From)");
            Add("smtp_pw",               "Mật khẩu ứng dụng (App Password)",                "App Password");
            Add("smtp_pw_ph",            "Nhập App Password Gmail...",                      "Enter Gmail App Password...");
            Add("smtp_guide",            "Cách lấy App Password Gmail (Google Account → Security → App Passwords)", "How to get Gmail App Password (Google Account → Security → App Passwords)");
            Add("smtp_test",             "Email kiểm tra kết nối (tuỳ chọn):",              "Test email address (optional):");
            Add("smtp_btn_test",         "Test",                                            "Test");
            Add("smtp_save",             "Lưu & Áp dụng",                                   "Save & Apply");
            Add("smtp_hint_test",        "Nhập email kiểm tra vào ô bên cạnh.",             "Enter test email in the adjacent box.");
            Add("smtp_testing",          "Đang kiểm tra kết nối SMTP...",                   "Checking SMTP connection...");
            Add("smtp_test_ok",          "Kết nối thành công! Email kiểm tra đã được gửi.", "Connection successful! Test email sent.");
            Add("smtp_test_fail",        "Kết nối thất bại. Kiểm tra lại Server/Port/Username/App Password.", "Connection failed. Check Server/Port/Username/App Password.");
            Add("smtp_save_ok_msg",      "Tính năng gửi email sẽ hoạt động ngay từ bây giờ.", "Email feature will be active immediately.");
            Add("smtp_save_fail",        "Lỗi khi lưu cấu hình: ",                          "Error saving configuration: ");
            Add("smtp_err_server",       "Nhập SMTP Server.",                               "Enter SMTP Server.");
            Add("smtp_err_port",         "Port không hợp lệ (VD: 587).",                    "Invalid Port (e.g. 587).");
            Add("smtp_err_user",         "Nhập tài khoản email.",                           "Enter email account.");
            Add("smtp_err_pw",           "Nhập mật khẩu ứng dụng.",                         "Enter app password.");

            // ── Phase 3: Sales - CustomerEditForm ─────────────────────────
            Add("sales_cus_title_add",   "Thêm mới Khách Hàng",                             "Add New Customer");
            Add("sales_cus_title_edit",  "Sửa thông tin Khách Hàng",                        "Edit Customer Info");
            Add("sales_cus_tax",         "Mã số thuế (* Bắt buộc)",                         "Tax Code (* Required)");
            Add("sales_cus_name",        "Tên Công ty / Khách hàng (* Bắt buộc)",           "Company / Customer Name (* Required)");
            Add("sales_cus_addr",        "Địa chỉ",                                         "Address");
            Add("sales_cus_rep",         "Người đại diện",                                  "Representative");
            Add("sales_cus_email",       "Email liên hệ",                                   "Contact Email");
            Add("sales_cus_phone",       "Số điện thoại",                                   "Phone Number");

            Add("msg_save_info",         "Lưu thông tin",                                   "Save Info");
            Add("sales_cus_err_req",     "Vui lòng nhập Mã số thuế và Tên công ty!",        "Please enter Tax Code and Company Name!");
            Add("sales_cus_err_dup_tax", "Mã số thuế này đã tồn tại trong hệ thống!",       "This Tax Code already exists in the system!");
            Add("sales_cus_err_save",    "Lỗi lưu dữ liệu: ",                               "Error saving data: ");

            Add("sales_ct_add_title",    "TẠO HỢP ĐỒNG MỚI",                                "CREATE NEW CONTRACT");
            Add("sales_ct_comp",         "Tên doanh nghiệp",                                "Company Name");
            Add("sales_ct_sym",          "Ký hiệu",                                         "Symbol");
            Add("sales_ct_ph_comp",      "Nhập tên doanh nghiệp...",                        "Enter company name...");
            Add("sales_ct_ph_sym",       "Nhập ký hiệu...",                                 "Enter symbol...");
            Add("sales_ct_rep",          "Người đại diện",                                  "Representative");
            Add("sales_ct_ph_rep",       "Nhập tên người đại diện....",                     "Enter representative name....");
            Add("sales_ct_phone",        "Số điện thoại",                                   "Phone Number");
            Add("sales_ct_ph_phone",     "Nhập số điện thoại...",                           "Enter phone number...");
            Add("sales_ct_addr",         "Địa chỉ doanh nghiệp",                            "Company Address");
            Add("sales_ct_ph_addr",      "Nhập địa chỉ....",                                "Enter address....");
            Add("sales_ct_sign_date",    "Ngày ký kết hợp đồng",                            "Signed Date");
            Add("sales_ct_exp_date",     "Ngày dự kiến trả kết quả",                        "Expected Result Date");
            Add("sales_ct_err_comp",     "Vui lòng nhập Tên doanh nghiệp!",                 "Please enter Company Name!");
            Add("sales_ct_msg_success",  "Tạo Hợp đồng thành công!",                        "Contract created successfully!");
            Add("sales_ct_edit_title",   "SỬA HỢP ĐỒNG",                                    "EDIT CONTRACT");
            Add("msg_update",            "Cập nhật",                                        "Update");
            Add("sales_ct_err_load",     "Lỗi tải dữ liệu hợp đồng: ",                      "Error loading contract data: ");
            Add("sales_ct_msg_update_success", "Cập nhật hợp đồng thành công!",             "Contract updated successfully!");


            // ── Phase 4: Field, Lab & Planning ────────────────────────────
            Add("plan_add_area_title",   "Thêm khu vực lấy mẫu",                            "Add Sampling Area");
            Add("plan_add_area_ph",      "VD: Khu vực đầu dự án - KK1",                     "Ex: Project starting area - KK1");
            Add("plan_add_area_err_req", "Vui lòng nhập tên khu vực!",                      "Please enter an area name!");
            Add("msg_confirm",           "Xác nhận",                                        "Confirm");

            Add("plan_add_param_title",  "Chọn thông số",                                   "Select Parameter");
            Add("plan_add_param_header", "Chọn thông số cần thêm",                          "Select parameter to add");
            Add("plan_add_param_search", "Tìm kiếm thông số...",                            "Search parameter...");
            Add("plan_add_param_col_select", "Chọn",                                        "Select");
            Add("plan_add_param_col_name", "Tên thông số",                                  "Parameter Name");
            Add("plan_add_param_col_unit", "Đơn vị",                                        "Unit");
            Add("plan_add_param_col_dept", "Phòng ban",                                     "Department");
            Add("plan_add_param_col_limit", "Ngưỡng QCVN",                                  "Limit (QCVN)");
            Add("plan_add_param_btn_add","＋ Thêm mới",                                     "＋ Add New");
            Add("plan_add_param_new_title", "Thêm thông số mới",                            "Add New Parameter");
            Add("plan_add_param_new_name", "Tên thông số:",                                 "Parameter Name:");
            Add("plan_add_param_new_unit", "Đơn vị:",                                       "Unit:");
            Add("plan_add_param_new_dept", "Phòng ban:",                                    "Department:");
            Add("plan_add_param_new_limit", "Ngưỡng QCVN:",                                 "Limit (QCVN):");
            Add("plan_add_param_dept_field", "Hiện trường",                                 "Field");
            Add("plan_add_param_dept_lab", "Thí nghiệm",                                    "Laboratory");
            Add("msg_add",               "Thêm",                                            "Add");
            Add("plan_add_param_err_req","Vui lòng nhập tên thông số!",                     "Please enter parameter name!");

            Add("plan_barcode_title",    "In Tem Dán Mẫu Quan Trắc",                        "Print Monitoring Sample Tag");
            Add("plan_barcode_info",     "Mẫu ID: {0}\nBarcode: {1}",                       "Sample ID: {0}\nBarcode: {1}");
            Add("plan_barcode_btn_print","Xác nhận IN",                                     "Confirm PRINT");
            Add("plan_barcode_msg_success", "Đã gửi lệnh In tới máy in nhiệt Barcode thành công!", "Print command sent to Barcode thermal printer successfully!");

            Add("plan_sp_env_type",      "Nền mẫu:",                                        "Environment:");
            Add("plan_sp_all",           "Tất cả",                                          "All");
            Add("plan_sp_air",           "Không khí",                                       "Air");
            Add("plan_sp_water",         "Nước thải",                                       "Wastewater");
            Add("plan_sp_soil",          "Đất",                                             "Soil");
            Add("plan_sp_assign",        "Phân công:",                                      "Assignment:");
            Add("plan_sp_field",         "HT",                                              "Field");
            Add("plan_sp_lab",           "PTN",                                             "Lab");
            Add("plan_sp_btn_add_env",   "Thêm nền mẫu",                                    "Add Env Type");
            Add("plan_sp_msg_all_env_added", "Tất cả nền mẫu đã được thêm cho hợp đồng này.", "All environment types are added to this contract.");
            Add("plan_sp_msg_env_added", "\"{0}\" đã được thêm cho hợp đồng này",           "\"{0}\" is already added to this contract");
            Add("plan_sp_msg_add_env",   "Thêm nền mẫu {0} vào hợp đồng",                   "Add environment type {0} to contract");
            Add("plan_sp_err_add_env",   "Lỗi khi thêm nền mẫu '{0}': {1}",                 "Error adding environment type '{0}': {1}");
            Add("plan_sp_area_1",        "Khu vực 1",                                       "Area 1");
            Add("plan_sp_area_count",    "{0} khu vực",                                     "{0} area(s)");
            Add("plan_sp_btn_add_area",  "+ Thêm khu vực",                                  "+ Add Area");
            Add("plan_sp_msg_err_add_area", "Lỗi khi tạo khu vực: {0}",                     "Error creating area: {0}");
            Add("plan_sp_btn_add_param", "Thêm thông số",                                   "Add Parameter");
            Add("plan_sp_msg_del_param", "Xóa thông số '{0}' khỏi khu vực?",                "Delete parameter '{0}' from area?");
            Add("plan_sp_msg_del_area",  "Bạn có chắc muốn xóa {0}?",                       "Are you sure you want to delete {0}?");
            Add("plan_sp_btn_del",       "✕",                                               "✕");
            Add("plan_sp_col_name",      "Tên thông số",                                    "Parameter Name");
            Add("plan_sp_col_unit",      "Đơn vị",                                          "Unit");
            Add("plan_sp_col_dept",      "Phòng ban",                                       "Department");
            Add("plan_sp_col_limit",     "Ngưỡng QCVN",                                     "Limit (QCVN)");
            Add("plan_sp_col_qcvn_short","QCVN",                                            "QCVN");
            Add("plan_sp_col_actions",   "Thao tác",                                        "Actions");
            Add("msg_del",               "Xóa",                                             "Delete");
            Add("msg_confirm_del",       "Xác nhận xóa",                                    "Confirm Deletion");
            Add("plan_sp_range",         "Khoảng",                                          "Range");
            Add("plan_sp_val",           "Giá trị",                                         "Value");
            Add("plan_sp_max",           "Max",                                             "Max");
            Add("plan_sp_min",           "Min",                                             "Min");

            Add("plan_param_selecting",  "Đang chọn: {0}",                                  "Selecting: {0}");
            Add("plan_param_grp_field",  "Thông số Hiện trường",                            "Field Parameters");
            Add("plan_param_grp_lab_water", "Thông số Phòng Thí Nghiệm (Nước)",             "Laboratory Parameters (Water)");
            Add("plan_param_grp_heavy_metal", "Kim loại nặng",                              "Heavy Metals");

            Add("director_exporting",    "Đang xuất...",                                    "Exporting...");
            Add("director_area_format",  "K{0}: {1}",                                       "A{0}: {1}");
            Add("director_col_result",   "Kết quả",                                         "Result");
            Add("director_msg_select_to_export", "Vui lòng chọn 1 Đơn hàng trên bảng để xuất File!", "Please select 1 order from the table to export!");
            Add("director_msg_export_success", "Đã xuất File PDF thành công!\nLưu tại: {0}", "PDF exported successfully!\nSaved at: {0}");
            Add("director_msg_export_fail", "Lỗi xuất PDF: {0}",                            "Error exporting PDF: {0}");

            Add("role_unassigned",       "Chưa phân công",                                  "Unassigned");

            // ── Dashboard ─────────────────────────────────────────────────
            Add("dashboard_header",       "TỔNG QUAN",                                     "DASHBOARD");
            Add("dashboard_search",       "Tìm kiếm dữ liệu quan trắc....",               "Search monitoring data....");
            Add("dashboard_risk",         "Nguy cơ ô nhiễm",                               "Pollution Risk");
            Add("dashboard_today",        "Hôm nay",                                       "Today");
            Add("dashboard_activity",     "Hoạt động quan trắc",                            "Monitoring Activity");
            Add("dashboard_ai_title",     "Dự báo AI",                                     "AI Forecast");
            Add("dashboard_ai_sub",       "Khả năng tái ký",                                "Renewal Probability");
            Add("dashboard_chart_title",  "Tổng quan số lượng hợp đồng",                   "Contract Overview");
            Add("dashboard_new_contract", "Hợp đồng mới",                                  "New Contracts");
            Add("dashboard_completed",    "Hoàn thành",                                    "Completed");
            Add("dashboard_extended",     "Gia hạn",                                        "Extended");
            Add("dashboard_expired",      "Hết hạn",                                        "Expired");
            Add("dashboard_sub_header",   "Danh mục & dữ liệu quan trắc",                 "Categories & monitoring data");
            

            Add("dashboard_ai_analyzing", "Đang phân tích...",                              "Analyzing...");
            Add("dashboard_ai_high",      "Khả năng tái ký cao",                            "High Renewal Probability");
            Add("dashboard_ai_low",       "Nguy cơ mất khách hàng",                         "Risk of Churn");
            Add("dashboard_ai_summary",   "Phân tích tổng thể",                             "Overall Analysis");
            Add("dashboard_ai_avg_high",  "TB tái ký",                                      "Avg Renewal");
            Add("dashboard_ai_avg_low",   "TB rủi ro",                                      "Avg Risk");
            Add("dashboard_ai_high_detail", "Dữ liệu lịch sử cho thấy tỷ lệ gắn bó ổn định.", "Historical data indicates a stable retention rate.");
            Add("dashboard_ai_mid_detail",  "Lưu ý chăm sóc thêm để củng cố gia hạn.",       "Additional care required to secure renewal.");
            Add("dashboard_ai_low_detail",  "Nguy cơ rời rạc cao, cần ưu đãi đặc biệt.",    "High risk of churn, special incentives needed.");
            
            Add("dashboard_tip_risk",     "Nguy cơ ô nhiễm: {0}%",                         "Pollution risk: {0}%");
            Add("dashboard_tip_pending",  "Đợt q.trắc chưa ht: {0}/{1}",                   "Pending monitoring: {0}/{1}");
            Add("dashboard_tip_eval",     "Mức đánh giá: {0}",                             "Evaluation: {0}");
            Add("dashboard_tip_low",      "Thấp",                                          "Low");
            Add("dashboard_tip_med",      "Trung bình",                                    "Medium");
            Add("dashboard_tip_high",     "Cao",                                           "High");
            Add("dashboard_tip_count_suffix", "đợt",                                        "sessions");
            Add("dashboard_tip_activity", "Đợt quan trắc: {0} {1}",                        "Monitoring Activity: {0} {1}");
            Add("dashboard_phase",        "Giai đoạn",                                     "Phase");

            // ── Landing & Auth ────────────────────────────────────────────
            Add("login_hero1",            "QUẢN LÝ HỢP ĐỒNG ĐƠN HÀNG",                     "CONTRACT & ORDER MANAGEMENT");
            Add("login_hero2",            "TRONG QUAN TRẮC MÔI TRƯỜNG",                   "FOR ENVIRONMENTAL MONITORING");
            Add("login_feat1_title1",     "Quản lý hợp đồng",                              "Smart contract");
            Add("login_feat1_title2",     "thông minh",                                    "management");
            Add("login_feat1_desc",       "“Quản lý toàn bộ quy trình thực hiện quan trắc môi trường từ tiếp nhận yêu cầu, lập kế hoạch, lấy mẫu, phân tích đến tổng hợp và báo cáo kết quả.”", "“Manage the entire environmental monitoring process from receiving requests, planning, sampling, testing to synthesizing and reporting the results.”");
            Add("login_feat2_title1",     "Theo dõi đơn hàng &",                           "Order tracking &");
            Add("login_feat2_title2",     "Điều tiết các phòng ban",                       "Department coordination");
            Add("login_feat2_desc",       "“Hỗ trợ phối hợp công việc giữa phòng Kinh doanh, Kế hoạch, Hiện trường, Phòng Thí nghiệm và Phòng Kết quả, đảm bảo dữ liệu được luân chuyển chính xác và thống nhất.”", "“Support coordination between Sales, Planning, Field, Lab, and Results departments, ensuring data is transferred accurately and consistently.”");
            Add("login_feat3_title1",     "Kiểm soát kết quả &",                           "Result control &");
            Add("login_feat3_title2",     "Cảnh báo",                                      "Warnings");
            Add("login_feat3_desc",       "“Lưu trữ, quản lý và tổng hợp dữ liệu quan trắc môi trường, hỗ trợ lập báo cáo kết quả theo quy định và phục vụ công tác theo dõi, đánh giá.”", "“Store, manage, and synthesize environmental monitoring data, support reporting according to regulations, and serve monitoring and evaluation.”");
            Add("login_footer_contact",   "Địa chỉ: số 19 Nguyễn Hữu Thọ, phường Tân Hưng, Quận 7, TP. Hồ Chí Minh\r\nHotline: 028 3775 5052\r\nEmail: ecova.tdtu@gmail.com", "Address: 19 Nguyen Huu Tho, Tan Hung Ward, District 7, Ho Chi Minh City\r\nHotline: +84 28 3775 5052\r\nEmail: ecova.tdtu@gmail.com");
            Add("login_back",             "Quay Lại",                                      "Back");
            Add("login_authenticating",   "Đang xác thực...",                              "Authenticating...");
            Add("login_success",          "Đăng nhập thành công! Chào",                    "Login successful! Welcome");
            Add("login_fail",             "Sai tài khoản hoặc mật khẩu!",                 "Wrong username or password!");
            Add("login_error_empty",      "Vui lòng nhập tài khoản và mật khẩu!",          "Please enter username and password!");
            Add("login_remaining_attempts", " (còn {0} lần thử)",                          " ({0} attempt(s) left)");
            Add("login_account_locked_temp", " — Tài khoản bị khóa tạm thời!",                " — Account temporarily locked!");
            Add("login_locked_out",       "Tài khoản bị tạm khóa. Thử lại sau {0}.",        "Account temporarily locked. Try again in {0}.");
            Add("login_minutes",          "{0} phút {1} giây",                             "{0} minutes {1} seconds");
            Add("login_seconds",          "{0} giây",                                       "{0} seconds");
            Add("login_server_error",     "Lỗi kết nối máy chủ!",                          "Server connection error!");
            
            // Forget Password texts
            Add("forget_title",           "QUÊN MẬT KHẨU",                                 "FORGOT PASSWORD");
            Add("forget_sub",             "Nhập email để nhận mã xác minh OTP",             "Enter email to receive OTP");
            Add("forget_email_ph",        "Nhập địa chỉ email...",                          "Enter email address...");
            Add("forget_otp_ph",          "Nhập mã xác thực OTP...",                        "Enter OTP code...");
            Add("forget_new_pw_ph",       "Mật khẩu mới (tối thiểu 6 ký tự)",                "New password (min 6 chars)");
            Add("forget_confirm_pw_ph",   "Xác nhận mật khẩu mới",                          "Confirm new password");
            Add("forget_continue",        "Tiếp tục",                                      "Continue");
            Add("forget_back",            "Quay Lại",                                      "Back");
            Add("forget_email_label",     "Email",                                         "Email");
            Add("forget_otp_label",       "Mã OTP",                                        "OTP Code");
            Add("forget_resend",          "Gửi lại mã OTP",                                "Resend OTP");
            Add("forget_new_pw_label",    "Mật khẩu mới",                                  "New Password");
            Add("forget_confirm_pw_label","Xác nhận mật khẩu",                             "Confirm Password");
            Add("forget_step2_success",   "Xác minh OTP thành công!",                      "OTP verified successfully!");
            Add("forget_processing",      "Đang xử lý...",                                 "Processing...");
            Add("forget_otp_expired",     "Mã đã hết hạn",                                 "Code expired");
            Add("forget_otp_valid",       "Mã có hiệu lực trong {0}:{1}",                   "Code valid for {0}:{1}");
            
            // Forget Password - Messages/Status
            Add("forget_err_email_empty", "Vui lòng nhập địa chỉ Email!",                  "Please enter your Email address!");
            Add("forget_err_email_not_exist", "Email không tồn tại trong hệ thống!",        "Email does not exist in our system!");
            Add("forget_err_email_send",  "Lỗi gửi Email. Vui lòng thử lại!",              "Error sending Email. Please try again!");
            Add("forget_err_otp_empty",   "Vui lòng nhập mã OTP!",                        "Please enter the OTP code!");
            Add("forget_err_otp_invalid", "Mã xác thực OTP không hợp lệ!",                "Invalid OTP code!");
            Add("forget_err_pw_min",      "Mật khẩu mới cần tối thiểu 6 ký tự!",           "New password must be at least 6 characters!");
            Add("forget_err_pw_mismatch", "Mật khẩu xác nhận không khớp!",                 "Confirm password does not match!");
            Add("forget_err_session",     "Phiên làm việc hết hạn. Vui lòng thực hiện lại từ đầu.", "Session expired. Please start over.");
            Add("forget_success",         "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.", "Password changed successfully! Please login again.");
            Add("forget_confirm",         "Xác nhận",                                      "Confirm");

            Add("faceid_prompt_continue", "Nhập Username để tiếp tục.",                    "Enter username to continue.");
            Add("faceid_btn_continue",    "Tiếp tục →",                                    "Continue →");
            Add("faceid_btn_back",        "Quay lại",                                      "Back");

            // Face ID texts
            Add("faceid_register_title",  "ĐĂNG KÝ FACE ID",                               "FACE ID REGISTRATION");
            Add("faceid_login_title",     "ĐĂNG NHẬP FACE ID",                             "FACE ID LOGIN");
            Add("login_faceid_title",     "FACE ID",                                       "FACE ID");
            Add("login_faceid_un",        "Username",                                      "Username");
            Add("login_faceid_un_ph",     "Nhập username của bạn...",                       "Enter your username...");
            Add("login_faceid_pw",        "Mật khẩu xác nhận",                             "Confirm password");
            Add("login_faceid_pw_ph",     "Nhập mật khẩu...",                              "Enter password...");
            Add("login_faceid_back",      "Quay lại",                                      "Back");
            Add("login_faceid_continue",  "Tiếp tục",                                      "Continue");
            Add("login_faceid_btn_reg",   "Đăng ký Face ID",                               "Register Face ID");
            Add("login_faceid_btn_update","Cập nhật Face ID",                              "Update Face ID");
            Add("login_faceid_btn_manual","Đăng nhập bằng mật khẩu",                       "Login with password");
            Add("login_faceid_btn_retry", "Thử lại",                                       "Retry");
            
            Add("faceid_status_input",    "Vui lòng nhập tên tài khoản!",                  "Please enter your username!");
            Add("faceid_checking_acc",    "Đang kiểm tra tài khoản...",                    "Checking account...");
            Add("faceid_err_not_exist",   "Tài khoản không tồn tại hoặc đã bị khoá!",      "Account does not exist or is locked!");
            Add("faceid_unregistered",    "Tài khoản chưa đăng ký Face ID. Hãy đăng ký ngay!", "Account not registered for Face ID. Register now!");
            Add("faceid_look_camera",     "Vui lòng nhìn vào camera để đăng nhập.",        "Please look at the camera to login.");
            Add("faceid_cam_not_ready",   "Camera chưa sẵn sàng!",                         "Camera not ready!");
            Add("faceid_err_pw_empty",    "Vui lòng nhập mật khẩu xác nhận!",              "Please enter confirm password!");
            Add("faceid_countdown",       "📸 Chụp sau {0} giây... Hãy giữ mặt trong khung!", "📸 Capturing in {0} seconds... Keep your face in frame!");
            Add("faceid_capturing",       "📸 Đang chụp...",                               "📸 Capturing...");
            Add("faceid_cam_not_ready_retry", "Camera chưa sẵn sàng! Thử lại.",            "Camera not ready! Retry.");
            Add("faceid_no_face_detect",  "Không phát hiện khuôn mặt! Hãy nhìn thẳng vào camera.", "No face detected! Look straight at the camera.");
            Add("faceid_reg_success",     "✅ Đăng ký Face ID thành công! Bạn có thể đăng nhập ngay.", "✅ Face ID registered successfully! You can login now.");
            Add("faceid_pw_wrong",        "Mật khẩu không đúng hoặc tài khoản bị khoá!",   "Incorrect password or account is locked!");
            Add("faceid_auto_scanning",   "🔍  Đang quét khuôn mặt tự động...",            "🔍 Auto scanning face...");
            Add("faceid_look_camera_login", "Nhìn vào camera để đăng nhập...",             "Look at the camera to login...");
            Add("faceid_look_straight",   "Hãy nhìn thẳng vào camera...",                  "Please look straight at the camera...");
            Add("faceid_auth_success",    "✅ Nhận diện thành công!",                      "✅ Recognized successfully!");
            Add("faceid_welcome",         "✅ Chào mừng {0}!",                             "✅ Welcome {0}!");
            Add("faceid_acc_locked",      "Tài khoản bị khoá!",                            "Account is locked!");
            Add("faceid_max_attempts",    "❌ Không nhận diện được sau {0} lần thử.",      "❌ Recognition failed after {0} attempts.");
            Add("faceid_scanning_progress", "Đang quét... (còn {0} lần thử)",                  "Scanning... ({0} attempt(s) left)");
            Add("faceid_fail_msg",        "Không thể nhận diện khuôn mặt.\nVui lòng chọn một trong các tuỳ chọn bên dưới:", "Face not recognized.\nPlease select one of the options below:");
            Add("faceid_update_prompt",   "Hãy nhìn vào camera và nhập mật khẩu để cập nhật.", "Look at the camera and enter your password to update.");
            Add("faceid_cam_ready",       "Camera sẵn sàng. Vui lòng nhìn vào ống kính.",  "Camera ready. Please look at the lens.");
            Add("faceid_cam_err",         "Lỗi khởi động camera: {0}",                      "Camera start error: {0}");
            Add("login_faceid_status_input", "Nhập Username để tiếp tục.",               "Enter username to continue.");


            Add("dashboard_env_wind",     "Gió",                                           "Wind");
            Add("dashboard_env_humidity", "Độ ẩm",                                         "Humidity");
            Add("dashboard_env_pressure", "Áp suất",                                       "Pressure");

            // ── Customer Management (Sales) ───────────────────────────────
            Add("sales_title",            "PHÒNG KINH DOANH",                              "SALES DEPARTMENT");
            Add("sales_subtitle",         "Quản lý khách hàng & Dự đoán tái ký",           "Customer Management & Renewal Prediction");
            Add("sales_search",           "Tìm kiếm doanh nghiệp, mã đơn....",            "Search company, contract ID....");
            Add("sales_add_contract",     "Thêm hợp đồng",                                "Add Contract");
            Add("sales_col_contract_id",  "Mã HĐ",                                        "Contract ID");
            Add("sales_col_company",      "Doanh nghiệp",                                 "Company");
            Add("sales_col_representative","Đại diện",                                     "Representative");
            Add("sales_col_contact",      "Liên hệ",                                      "Contact");
            Add("sales_col_signed",       "Ngày ký",                                       "Signed Date");
            Add("sales_col_valid_to",     "Ngày trả KQ",                                   "Result Date");
            Add("sales_col_address",      "Địa điểm",                                     "Location");
            Add("sales_col_renewal",      "Tái ký (%)",                                    "Renewal (%)");
            Add("sales_col_action",       "Thao tác",                                      "Actions");
            Add("sales_btn_update",       "Cập nhật",                                      "Update");
            Add("sales_btn_analyze",      "Phân tích",                                     "Analyze");
            Add("sales_anlz_title",       "CHI TIẾT PHÂN TÍCH",                            "ANALYSIS DETAILS");
            Add("sales_anlz_comp",        "CÔNG TY / ĐỐI TÁC",                            "COMPANY / PARTNER");
            Add("sales_anlz_rep",         "Đại diện:",                                     "Representative:");
            Add("sales_anlz_contact",     "Liên hệ:",                                      "Contact:");
            Add("sales_anlz_addr",        "ĐỊA CHỈ",                                       "ADDRESS");
            Add("sales_anlz_dur",         "Thời hạn: {0} - {1}",                            "Duration: {0} - {1}");
            Add("sales_anlz_st_active",   "Đang hoạt động",                                 "Active");
            Add("sales_anlz_st_pend",     "Chờ xử lý",                                     "Pending");
            Add("sales_anlz_predict",     "DỰ ĐOÁN KHẢ NĂNG TÁI KÝ",                     "RENEWAL PREDICTION");
            Add("sales_anlz_btn_ok",      "XÁC NHẬN",                                     "CONFIRM");
            Add("sales_error_load",       "Lỗi tải dữ liệu",                              "Error loading data");

            // ── Contract List ─────────────────────────────────────────────
            Add("contract_list_title",    "Danh sách Hợp Đồng",                            "Contract List");
            Add("contract_list_create",   "+ Soạn Hợp Đồng",                               "+ Create Contract");
            Add("contract_list_search",   "Tìm mã hợp đồng...",                            "Search contract ID...");
            Add("contract_list_error",    "Lỗi tải danh sách Hợp đồng: ",                  "Error loading contract list: ");
            Add("contract_list_reload_error", "Lỗi tải lại danh sách Hợp đồng: ",          "Error reloading contract list: ");

            // ── Employee Management (Admin) ───────────────────────────────
            Add("employee_title",         "QUẢN LÝ NHÂN VIÊN",                             "EMPLOYEE MANAGEMENT");
            Add("employee_subtitle",      "Danh mục nhân viên & quản lý",                 "Employee directory & management");
            Add("employee_search",        "Tìm kiếm nhân viên",                            "Search employees");
            Add("employee_all_dept",      "Tất cả phòng ban",                               "All departments");
            Add("employee_add",           "Thêm nhân viên",                                "Add Employee");
            Add("employee_send_mail",     "📧  Gửi Mail",                                   "📧  Send Mail");
            Add("employee_col_name",      "Họ và tên",                                     "Full Name");
            Add("employee_col_code",      "Mã NV",                                         "Emp. ID");
            Add("employee_col_dept",      "Bộ phận",                                        "Department");
            Add("employee_col_address",   "Địa chỉ",                                       "Address");
            Add("employee_col_contact",   "Thông tin liên lạc",                             "Contact Info");
            Add("employee_col_action",    "Thao tác",                                      "Actions");
            Add("employee_btn_edit",      "Sửa",                                            "Edit");
            Add("employee_btn_lock",      "Khóa",                                           "Lock");
            Add("employee_btn_unlock",    "Mở",                                             "Open");
            Add("employee_btn_delete",    "Xóa",                                            "Del");
            Add("employee_cant_lock_self","Bạn không thể khóa tài khoản của chính mình!",   "You cannot lock your own account!");
            Add("employee_cant_delete_self","Bạn không thể xóa tài khoản của chính mình!",   "You cannot delete your own account!");
            Add("employee_not_allowed",   "Không được phép",                                "Not Allowed");
            Add("employee_confirm_toggle","Bạn có chắc chắn muốn {0} tài khoản của '{1}'?","Are you sure you want to {0} account of '{1}'?");
            Add("employee_lock_word",     "khóa",                                           "lock");
            Add("employee_unlock_word",   "mở khóa",                                        "unlock");
            Add("employee_toggle_success","Đã {0} tài khoản thành công!",                   "Account {0} successfully!");
            Add("employee_delete_confirm","CẢNH BÁO: BẠN ĐANG XÓA VĨNH VIỄN nhân viên '{0}' khỏi hệ thống.\nHành động này không thể hoàn tác. Bạn có thật sự muốn xóa không?",
                                          "WARNING: You are PERMANENTLY DELETING employee '{0}' from the system.\nThis action cannot be undone. Do you really want to delete?");
            Add("employee_delete_title",  "Xóa vĩnh viễn",                                 "Permanent Delete");
            Add("employee_delete_success","Đã xóa vĩnh viễn nhân viên thành công!",         "Employee permanently deleted successfully!");
            Add("employee_dept_sales",    "Kinh doanh",                                     "Sales");
            Add("employee_dept_planning", "Kế hoạch",                                       "Planning");
            Add("employee_dept_field",    "Hiện trường",                                    "Field");
            Add("employee_dept_lab",      "Thí nghiệm",                                    "Lab");
            Add("employee_dept_result",   "Kết quả",                                        "Results");

            // ── Planning (SampleConfigUC) ─────────────────────────────────
            Add("planning_title",         "PHÒNG KẾ HOẠCH",                                "PLANNING DEPARTMENT");
            Add("planning_search",        "Chọn hợp đồng...",                               "Select contract...");
            Add("planning_filter_sample", "Nền mẫu:",                                       "Sample type:");
            Add("planning_filter_assign", "Phân công:",                                      "Assignment:");
            Add("planning_filter_all",    "Tất cả",                                          "All");
            Add("planning_env_air",       "Không khí",                                       "Air");
            Add("planning_env_water",     "Nước thải",                                       "Wastewater");
            Add("planning_env_soil",      "Đất",                                             "Soil");
            Add("planning_add_env",       "Thêm nền mẫu",                                   "Add sample type");
            Add("planning_add_area",      "+ Thêm khu vực",                                  "+ Add area");
            Add("planning_add_param",     "Thêm thông số",                                   "Add parameter");
            Add("planning_areas_count",   "{0} khu vực",                                     "{0} areas");
            Add("planning_area_default",  "Khu vực",                                         "Area");
            Add("planning_all_env_added", "Tất cả nền mẫu đã được thêm cho hợp đồng này.", "All sample types have been added for this contract.");
            Add("planning_error_create_area", "Lỗi khi tạo khu vực",                        "Error creating area");
            Add("planning_error_add_env",     "Lỗi khi thêm nền mẫu",                       "Error adding sample type");

            // ── Field (EnterResultUC) ─────────────────────────────────────
            Add("field_title",            "PHÒNG HIỆN TRƯỜNG",                              "FIELD DEPARTMENT");
            Add("field_filter_display",   "Hiển thị:",                                      "Display:");
            Add("field_filter_not_entered","Chưa nhập",                                     "Not Entered");
            Add("field_filter_entered",   "Đã nhập",                                        "Entered");
            Add("field_qcvn_warning_title","Cảnh báo QCVN",                                 "QCVN Warning");
            Add("field_qcvn_warning_msg", "⚠ CẢNH BÁO VƯỢT NGƯỠNG QCVN\n\nThông số: {0}\nGiá trị đo: {1}\nNgưỡng QCVN: {2}\n\nKết quả vẫn được lưu vào hệ thống để ghi vết.",
                                          "⚠ QCVN THRESHOLD EXCEEDED\n\nParameter: {0}\nMeasured value: {1}\nQCVN Limit: {2}\n\nResult will be saved for audit trail.");
            Add("field_autosave_error",   "Lỗi tự lưu: ",                                   "Auto-save error: ");
            Add("field_reason_title",     "Lý do sửa đổi kết quả",                          "Edit Result Reason");
            Add("field_reason_label",     "Nhập lý do sửa đổi kết quả cho thông số {0}:",   "Enter reason for editing result of {0}:");
            Add("field_reason_ph",        "Nhập lý do...",                                   "Enter reason...");

            // ── Lab (LabResultUC) ─────────────────────────────────────────
            Add("lab_title",              "PHÒNG THÍ NGHIỆM",                              "LAB DEPARTMENT");

            // ── Director / QA (DirectorApprovalUC) ────────────────────────
            Add("director_title",         "PHÒNG KẾT QUẢ",                                 "RESULTS DEPARTMENT");
            Add("director_filter_param",  "Thông số:",                                     "Parameter:");
            Add("director_filter_location","Vị trí:",                                       "Location:");
            Add("director_export",        "Xuất file",                                      "Export");
            Add("director_no_orders",     "Không có đơn hàng nào cho hợp đồng này.",        "No orders found for this contract.");
            Add("director_error_load",    "Lỗi tải dữ liệu khu vực",                       "Error loading area data");
            Add("director_export_select_title", "Chọn nền mẫu xuất PDF",                   "Select Sample Types to Export");
            Add("director_export_select_desc",  "Chọn nền mẫu cần xuất file PDF:",          "Select sample types to export as PDF:");
            Add("director_export_select_none",  "Vui lòng chọn ít nhất 1 nền mẫu!",         "Please select at least 1 sample type!");

            // ── Results Grid Columns (Field / Lab) ────────────────────────
            Add("grid_col_param_name",    "Tên thông số",                                  "Parameter Name");
            Add("grid_col_unit",          "Đơn vị",                                        "Unit");
            Add("grid_col_result",        "Kết quả",                                       "Result");

            Add("director_filter_area",   "Khu vực {0}",                                   "Area {0}");

            // ── Notification ──────────────────────────────────────────────
            Add("notification_title",     "TRUNG TÂM THÔNG BÁO",                           "NOTIFICATION CENTER");
            Add("notification_search",    "Tìm kiếm đơn hàng....",                         "Search orders....");
            Add("notification_overdue",   "⚠  Quá hạn {0} ngày",                            "⚠  {0} days overdue");
            Add("notification_remaining", "⏰  Còn {0} ngày",                               "⏰  {0} days remaining");
            Add("notification_code",      "Mã HĐ: {0}",                                    "Contract: {0}");
            Add("notification_signed",    "Ngày ký:        {0}",                           "Signed:         {0}");
            Add("notification_expected",  "Ngày dự kiến: {0}",                             "Expected:       {0}");
            Add("notification_contracts_title", "📋 DANH SÁCH HỢP ĐỒNG",                     "📋 CONTRACT LIST");

            // ── Parameter Config ──────────────────────────────────────────
            Add("paramconfig_title",      "PHÒNG KẾ HOẠCH",                                "PLANNING DEPARTMENT");
            Add("paramconfig_subtitle",   "Cấu hình Thông số phân tích cho Mẫu",           "Configure Analysis Parameters for Samples");
            Add("paramconfig_sample_list","Danh sách Mẫu",                                  "Sample List");
            Add("paramconfig_search",     "Tìm kiếm mã mẫu...",                             "Search sample ID...");
            Add("paramconfig_selected",   "Đang chọn:",                                     "Selected:");
            Add("paramconfig_guide",      "Tích chọn các thông số cần thiết cho mẫu này",   "Select required parameters for this sample");
            Add("paramconfig_save",       "Lưu cấu hình",                                   "Save Config");
            Add("paramconfig_saved",      "Đã lưu chỉ tiêu thành công!",                    "Parameters saved successfully!");
            Add("paramconfig_group_field", "Thông số Hiện trường",                          "Field Parameters");
            Add("paramconfig_group_lab",   "Thông số Phòng Thí Nghiệm (Nước)",             "Lab Parameters (Water)");
            Add("paramconfig_group_metal", "Kim loại nặng",                                 "Heavy Metals");

            // ── Common / Shared ───────────────────────────────────────────
            Add("error",                  "Lỗi",                                            "Error");
            Add("info",                   "Thông báo",                                       "Information");
            Add("warning",               "Cảnh báo",                                         "Warning");
            Add("yes",                   "Có",                                               "Yes");
            Add("no",                    "Không",                                             "No");
            Add("save",                  "Lưu",                                              "Save");
            Add("cancel",               "Hủy",                                               "Cancel");
            Add("close",                "Đóng",                                               "Close");
            Add("loading",              "Đang tải...",                                        "Loading...");

            // ── Profile / Edit Profile ────────────────────────────────────
            Add("profile_title",          "HỒ SƠ CÁ NHÂN",                                 "PERSONAL PROFILE");
            Add("profile_tab_info",       "Thông tin cá nhân",                             "Personal Info");
            Add("profile_tab_pwd",        "Đổi mật khẩu",                                 "Change Password");
            Add("profile_btn_change_avatar", "Đổi ảnh đại diện",                             "Change Avatar");
            Add("profile_lbl_name",       "Họ và tên",                                     "Full Name");
            Add("profile_lbl_dept",       "Phòng ban",                                     "Department");
            Add("profile_lbl_birth",      "Năm sinh",                                      "Birth Year");
            Add("profile_lbl_phone",      "Điện thoại",                                    "Phone Number");
            Add("profile_lbl_id",         "ID Nhân viên",                                  "Employee ID");
            Add("profile_lbl_email",      "Email",                                         "Email");
            Add("profile_lbl_addr",       "Địa chỉ",                                       "Address");
            Add("profile_ph_name",        "Nhập họ tên...",                                "Enter full name...");
            Add("profile_ph_phone",       "Số điện thoại...",                              "Phone number...");
            Add("profile_ph_email",       "Email...",                                      "Email...");
            Add("profile_ph_addr",        "Địa chỉ...",                                    "Address...");
            Add("profile_ph_birth",       "VD: 1998",                                      "Ex: 1998");
            Add("profile_btn_update",     "Cập nhật hồ sơ",                                "Update Profile");
            Add("profile_msg_saving",     "Đang lưu...",                                   "Saving...");
            Add("profile_msg_success",    "Cập nhật hồ sơ thành công!",                    "Profile updated successfully!");
            Add("profile_err_name_req",   "Vui lòng nhập họ và tên.",                      "Please enter full name.");
            Add("profile_err_birth_invalid", "Năm sinh không hợp lệ (VD: 1990).",           "Invalid birth year (Ex: 1990).");
            
            Add("profile_pwd_title",      "THAY ĐỔI MẬT KHẨU",                             "CHANGE PASSWORD");
            Add("profile_pwd_sub",        "Mật khẩu mới phải có ít nhất 6 ký tự",          "New password must be at least 6 characters");
            Add("profile_pwd_lbl_old",    "Mật khẩu hiện tại",                             "Current Password");
            Add("profile_pwd_lbl_new",    "Mật khẩu mới",                                  "New Password");
            Add("profile_pwd_lbl_cfm",    "Xác nhận mật khẩu mới",                         "Confirm New Password");
            Add("profile_pwd_ph_old",    "Nhập mật khẩu đang dùng...",                    "Enter current password...");
            Add("profile_pwd_ph_new",    "Ít nhất 6 ký tự...",                             "At least 6 characters...");
            Add("profile_pwd_ph_cfm",    "Nhập lại mật khẩu mới...",                      "Re-enter new password...");
            Add("profile_pwd_hint",       "Sau khi đổi, bạn sẽ cần đăng nhập lại bằng mật khẩu mới.", "After changing, you will need to login again with the new password.");
            Add("profile_pwd_success",    "Đổi mật khẩu thành công!",                      "Password changed successfully!");
            // Roles Localization
            Add("role_admin",            "Admin",                                         "Admin");
            Add("role_director",         "Giám đốc",                                      "Director");
            Add("role_sales",            "Phòng Kinh doanh",                              "Sales Department");
            Add("role_field",            "Phòng Hiện trường",                             "Field Department");
            Add("role_lab",              "Phòng Thí nghiệm",                             "Lab Department");
            Add("role_planning",         "Phòng Kế hoạch",                                "Planning Department");
            Add("role_result",           "Phòng Kết quả",                                 "Results Department");
            Add("role_employee",         "Nhân viên",                                       "Employee");
            Add("role_system_admin",     "Quản trị hệ thống",                               "System Admin");
        }

        private void Add(string key, string vi, string en)
        {
#if DEBUG
            // Guard chống duplicate key khi phát triển.
            // Nếu thêm key đã tồn tại → throw ngay tại startup (Debug only).
            // Release build: check này bị compile away — zero overhead.
            if (_vi.ContainsKey(key))
                throw new InvalidOperationException(
                    $"[LanguageManager] Duplicate key detected: \"{key}\"\n" +
                    $"  Existing VI = \"{_vi[key]}\"\n" +
                    $"  New VI      = \"{vi}\"");
#endif
            _vi[key] = vi;
            _en[key] = en;
        }
    }
}
