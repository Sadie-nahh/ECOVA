using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EnvContract.GUI.Helpers
{
    /// <summary>
    /// Helper tĩnh cung cấp hiệu ứng Fade mượt mà giữa các Form / Panel.
    /// </summary>
    public static class FormTransitionHelper
    {
        private const int Steps = 15;
        private const int DelayMs = 10; // ~100fps for smoother transitions

        /// <summary>Làm mờ dần Form từ opacity hiện tại xuống 0.</summary>
        public static async Task FadeOutAsync(Form form)
        {
            double start = form.Opacity;
            for (int i = Steps; i >= 0; i--)
            {
                if (form.IsDisposed) return;
                form.Opacity = start * i / Steps;
                await Task.Delay(DelayMs);
            }
            form.Opacity = 0;
        }

        /// <summary>Fade Form từ 0 lên 1.</summary>
        public static async Task FadeInAsync(Form form)
        {
            form.Opacity = 0;
            for (int i = 0; i <= Steps; i++)
            {
                if (form.IsDisposed) return;
                form.Opacity = (double)i / Steps;
                await Task.Delay(DelayMs);
            }
            form.Opacity = 1.0;
        }

        /// <summary>
        /// Mượt mà chuyển đổi 2 form bằng cách cho form mới đè lên đúng tọa độ form cũ.
        /// </summary>
        public static async Task TransitionAsync(Form from, Form to, bool closeFrom = false)
        {
            // Bước 1: Chuẩn bị form 'to'
            to.StartPosition = FormStartPosition.Manual;
            to.Bounds = from.Bounds;
            to.WindowState = from.WindowState;

            // Bước 2: Hiển thị form 'to' lên trên nhưng trong suốt
            to.Opacity = 0;
            to.Show();
            
            // Nếu không dùng Application.DoEvents(), UI form mới có thể vẽ không kịp.
            // Thay vào đó ta cứ set opacity nhanh dần đều
            for (int i = 0; i <= Steps; i++)
            {
                if (to.IsDisposed) break;
                to.Opacity = (double)i / Steps;
                await Task.Delay(DelayMs);
            }
            to.Opacity = 1.0;
            to.BringToFront();

            // Bước 3: Đóng/Ẩn form cũ
            if (closeFrom) from.Close();
            else from.Hide();
            
            if (!closeFrom && !from.IsDisposed)
            {
                from.Opacity = 1.0;
            }
        }

        /// <summary>
        /// Fade bên trong cùng một Form (dùng cho Panel switch).
        /// Để tránh lỗi biến mất form (Opacity = 0), thực hiện đổi panel trực tiếp và Refresh form.
        /// </summary>
        public static async Task PanelSwitchAsync(Form host, Action switchAction)
        {
            // Tạm thời dừng Layout để tránh nhấp nháy UI
            host.SuspendLayout();
            
            switchAction();
            
            host.ResumeLayout(true);
            host.Refresh();

            await Task.CompletedTask;
        }
    }
}
