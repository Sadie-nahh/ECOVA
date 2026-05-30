using System;
using System.Drawing;
using System.Windows.Forms;

namespace EnvContract.GUI.Helpers
{
    public static class PopupOverlayHelper
    {
        /// <summary>
        /// Hiển thị một Form Popup chèn lên trên một Background đen mờ (làm mờ Form phía dưới)
        /// Sử dụng kỹ thuật tạo Form giả trong suốt màu DarkGlass.
        /// </summary>
        /// <param name="mainForm">Form cha mong muốn làm mờ</param>
        /// <param name="popupForm">Form popup chuẩn bị mở lên</param>
        public static void ShowAsOverlay(Form mainForm, Form popupForm)
        {
            Form backgroundOverlay = new Form();
            try
            {
                using (backgroundOverlay)
                {
                    // Cấu hình Form nền Overlay làm mờ
                    backgroundOverlay.StartPosition = FormStartPosition.Manual;
                    backgroundOverlay.FormBorderStyle = FormBorderStyle.None;
                    backgroundOverlay.Opacity = 0.50d; // Độ mờ 50%
                    backgroundOverlay.BackColor = Color.Black; 
                    backgroundOverlay.WindowState = FormWindowState.Normal;
                    backgroundOverlay.TopMost = true;

                    // Canh đúng tọa độ và size của MainForm
                    backgroundOverlay.Location = mainForm.PointToScreen(Point.Empty);
                    backgroundOverlay.ClientSize = mainForm.ClientSize;

                    // Hiện background
                    backgroundOverlay.Show();
                    
                    // Thiết lập popup Form ở ngay trên lớp BlackOverlay 
                    popupForm.Owner = backgroundOverlay;
                    
                    // Center Screen tương đối so với màn hình
                    popupForm.StartPosition = FormStartPosition.CenterScreen;

                    // Mở popup Form (Code sẽ dừng ở đây cho đến khi tắt Form con)
                    popupForm.ShowDialog();
                    
                    // Dọn dẹp Overlay
                    backgroundOverlay.Dispose();
                }
            }
            catch (Exception ex)
            {
                var LM = EnvContract.Common.LanguageManager.Instance;
                MessageBox.Show(LM.Get("ui_error_desc") + ex.Message, LM.Get("ui_error_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Fallback nếu crash mờ -> hiện bình thường
                popupForm.ShowDialog();
            }
        }
    }
}
