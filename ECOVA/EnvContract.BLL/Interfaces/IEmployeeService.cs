using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Interfaces
{
    public interface IEmployeeService
    {
        /// <summary>
        /// Adds a new employee and returns the plain-text password (for email notification).
        /// </summary>
        Task<string> AddEmployeeAsync(UserDTO employee);
        Task UpdateEmployeeAsync(UserDTO employee);
        Task ToggleActiveAsync(string userId, bool isActive);
        Task DeleteEmployeeAsync(string employeeId);
        Task<UserDTO?> GetEmployeeByIdAsync(string employeeId);
        Task<List<UserDTO>> GetAllEmployeesAsync();
        Task<List<UserDTO>> SearchEmployeesAsync(string keyword);

        /// <summary>
        /// Generates EmployeeCode, UserID, and Username for a new employee based on roleId.
        /// </summary>
        Task<(string EmployeeCode, string UserID, string Username)> GenerateNewEmployeeDataAsync(string roleId);
    }
}
