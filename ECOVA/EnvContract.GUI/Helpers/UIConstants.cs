using System.Drawing;

namespace EnvContract.GUI.Helpers
{
    public static class UIConstants
    {
        // 1. Màu sắc chủ đạo (Figma Rừng / Glassmorphism)
        public static readonly Color PrimaryColor = Color.FromArgb(70, 153, 91); // Xanh lá cây sẫm (Active / Nút chính)
        public static readonly Color DarkGreenBackground = Color.FromArgb(24, 46, 26); // Xanh rừng rậm dùng cho Sidebar (#182E1A)
        public static readonly Color LightGreenAccent = Color.FromArgb(161, 185, 114); // Xanh mạ non / olive (#A1B972)
        public static readonly Color VeryLightGreen = Color.FromArgb(217, 232, 198); // Nền box nhạt (#D9E8C6)
        public static readonly Color SoftLightGreen = Color.FromArgb(193, 219, 153); // Nền box sáng (#C1DB99)
        
        // 2. Màu nền và text (Sáng/Tối)
        public static readonly Color WhiteBackground = Color.FromArgb(245, 245, 240); // Màu nền mờ nhạt (#F5F5F0)
        public static readonly Color FormWhite = Color.White; // Nền thẻ bo góc (Card form)
        public static readonly Color FormDarkGlass = Color.FromArgb(180, 20, 30, 25); // Kính mờ đen dùng cho Modal Popup Overlay
        
        // Text
        public static readonly Color TextDark = Color.FromArgb(40, 40, 40); // Chữ thường đen xám
        public static readonly Color TextLight = Color.White; // Chữ trắng dùng trên nền xanh thẫm
        public static readonly Color TextMuted = Color.Gray; // Chữ mờ cho Helper/Placeholder

        // 3. Màu cảnh báo và hệ thống
        public static readonly Color DangerColor = Color.FromArgb(220, 53, 69); // Đỏ cờ báo lỗi QCVN hoặc Text ngày quá hạn
        public static readonly Color WarningColor = Color.FromArgb(255, 193, 7); // Vàng cảnh báo
        public static readonly Color SuccessColor = Color.FromArgb(49, 87, 44); // Xanh thành công đậm (#31572C)
        public static readonly Color BorderColor = Color.FromArgb(220, 225, 230); // Viền xám nhạt

        // 4. Kích thước & Font chữ chuẩn UI (Dùng để config Guna2 Control)
        public static readonly string PrimaryFontName = "Segoe UI";
        public static readonly int BorderRadiusLarge = 15; // Cho form ThemMoi popup, các Card chính lớn.
        public static readonly int BorderRadiusMedium = 8; // Cho Input Box, Button
        public static readonly int BorderRadiusSmall = 5; // Cho Label Status

        // 5. Cài đặt các Panel
        public static readonly Size SidebarSize = new Size(250, 720); // Chiều ngang mặc định sidebar
    }
}
