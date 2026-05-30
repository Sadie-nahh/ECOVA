using EnvContract.DTO.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface IEmployeeRepository
    {
        /// <summary>
        /// UPSERT nhân viên qua sp_UpsertEmployee.
        /// Trả về 'INSERT' hoặc 'UPDATE'. Ném SqlException(50002) nếu email trùng active.
        /// </summary>
        Task<string> UpsertEmployeeAsync(UserDTO employee);

        Task UpdateEmployeeAsync(UserDTO employee);
        Task ToggleActiveAsync(string userId, bool isActive);
        Task DeleteEmployeeAsync(string employeeId);
        Task<UserDTO?> GetEmployeeByIdAsync(string employeeId);
        Task<List<UserDTO>> GetAllEmployeesAsync();
        Task<List<UserDTO>> SearchEmployeesAsync(string keyword);
        Task<UserDTO?> GetEmployeeByEmailAsync(string email);

        /// <summary>
        /// Sinh UserID, EmployeeCode, Username theo RoleID qua sp_GenerateEmployeeCode (atomic).
        /// </summary>
        Task<(string UserID, string EmployeeCode, string Username)> GenerateEmployeeCodeAsync(string roleId);
    }
}
