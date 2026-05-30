using EnvContract.DTO.Entities;

namespace EnvContract.Common
{
    /// <summary>
    /// Singleton class to manage the global state of the application.
    /// Thread-safe: CurrentUser uses lock to prevent race conditions
    /// from background tasks (AI predictions, notification timers, etc.)
    /// </summary>
    public class AppState
    {
        private static AppState? _instance;
        private static readonly object _lock = new object();

        private UserDTO? _currentUser;

        /// <summary>
        /// Người dùng hiện tại đang đăng nhập.
        /// Thread-safe: get/set đều dùng lock để tránh race condition.
        /// </summary>
        public UserDTO? CurrentUser
        {
            get { lock (_lock) { return _currentUser; } }
            set { lock (_lock) { _currentUser = value; } }
        }

        private AppState() { }

        public static AppState Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new AppState();
                    }
                    return _instance;
                }
            }
        }

        public bool IsLoggedIn => CurrentUser != null;

        public void Logout()
        {
            CurrentUser = null;
        }
    }
}
