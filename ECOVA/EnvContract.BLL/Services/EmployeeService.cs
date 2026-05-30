using EnvContract.BLL.Interfaces;
using EnvContract.Common.Helpers;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using Microsoft.Data.SqlClient;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EnvContract.BLL.Services
{
    public class EmployeeService : IEmployeeService
    {
        private readonly IEmployeeRepository _employeeRepository;

        public EmployeeService(IEmployeeRepository employeeRepository)
        {
            _employeeRepository = employeeRepository;
        }

        /// <summary>
        /// Sinh EmployeeCode, UserID, Username theo RoleID.
        /// Gọi sp_GenerateEmployeeCode — atomic, tránh race condition.
        /// </summary>
        public async Task<(string EmployeeCode, string UserID, string Username)> GenerateNewEmployeeDataAsync(string roleId)
        {
            var result = await _employeeRepository.GenerateEmployeeCodeAsync(roleId);
            return (result.EmployeeCode, result.UserID, result.Username);
        }

        /// <summary>
        /// Thêm nhân viên mới:
        ///   1. Hash password ở application-layer (BCrypt — không bao giờ đưa xuống DB).
        ///   2. Gọi sp_UpsertEmployee — DB xử lý logic check email + INSERT/UPDATE.
        ///   3. Gửi email credentials (fire-and-forget).
        /// Trả về plain-text password để GUI hiển thị.
        /// </summary>
        public async Task<string> AddEmployeeAsync(UserDTO employee)
        {
            // Hash password — application-level (BCrypt không thể làm trong SQL)
            string plainPassword = SecurityHelper.GenerateRandomPassword(8);
            employee.PasswordHash = SecurityHelper.HashPassword(plainPassword);
            employee.IsActive = true;

            try
            {
                string action = await _employeeRepository.UpsertEmployeeAsync(employee);
                Log.Information("[Employee] {Action} nhân viên {Email}", action, employee.Email);
            }
            catch (SqlException ex) when (ex.Number == 50002)
            {
                throw new InvalidOperationException(
                    $"Email '{employee.Email}' đã được sử dụng bởi nhân viên đang hoạt động.", ex);
            }

            // Gửi email credentials (fire-and-forget — không block luồng chính)
            _ = Task.Run(async () =>
            {
                try
                {
                    string subject = "ECOVA — Thông tin tài khoản nhân viên mới";
                    string body    = BuildAccountEmailBody(employee.FullName, employee.Username, plainPassword);
                    bool sent      = await EmailSmtpHelper.SendEmailAsync(employee.Email, subject, body);
                    if (sent)
                        Log.Information("[Employee] Đã gửi email credentials tới {Email}", employee.Email);
                    else
                        Log.Warning("[Employee] Gửi email credentials thất bại tới {Email}", employee.Email);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[Employee] Lỗi gửi email credentials tới {Email}", employee.Email);
                }
            });

            return plainPassword;
        }

        public async Task DeleteEmployeeAsync(string employeeId)
        {
            await _employeeRepository.DeleteEmployeeAsync(employeeId);
        }

        public async Task<List<UserDTO>> GetAllEmployeesAsync()
        {
            return await _employeeRepository.GetAllEmployeesAsync();
        }

        public async Task<UserDTO?> GetEmployeeByIdAsync(string employeeId)
        {
            return await _employeeRepository.GetEmployeeByIdAsync(employeeId);
        }

        public async Task<List<UserDTO>> SearchEmployeesAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return await GetAllEmployeesAsync();
            return await _employeeRepository.SearchEmployeesAsync(keyword);
        }

        public async Task UpdateEmployeeAsync(UserDTO employee)
        {
            await _employeeRepository.UpdateEmployeeAsync(employee);
        }

        public async Task ToggleActiveAsync(string userId, bool isActive)
        {
            await _employeeRepository.ToggleActiveAsync(userId, isActive);
            Log.Information("[Employee] Đã {Action} tài khoản UserID={UserID}",
                isActive ? "mở khóa" : "khóa", userId);
        }

        // ── Email template ──────────────────────────────────────────────────
        private static string BuildAccountEmailBody(string fullName, string username, string password)
        {
            return $@"
<div style='font-family: Segoe UI, Arial, sans-serif; max-width: 520px; margin: auto; padding: 32px; border: 1px solid #e0e0e0; border-radius: 12px;'>
    <div style='text-align: center; margin-bottom: 24px;'>
        <h2 style='color: #31572C; margin: 0;'>ECOVA</h2>
        <p style='color: #888; font-size: 13px; margin: 4px 0 0;'>Hệ thống quản lý quan trắc môi trường</p>
    </div>
    <hr style='border: none; border-top: 1px solid #e0e0e0; margin: 16px 0;'/>
    <p>Xin chào <b>{fullName}</b>,</p>
    <p>Tài khoản ECOVA của bạn đã được tạo thành công. Dưới đây là thông tin đăng nhập:</p>
    <table style='width: 100%; border-collapse: collapse; margin: 16px 0;'>
        <tr>
            <td style='padding: 10px 14px; background: #f5f5f5; border-radius: 6px 0 0 0; font-weight: bold; width: 120px;'>Username</td>
            <td style='padding: 10px 14px; background: #f5f5f5; border-radius: 0 6px 0 0;'><code style='font-size: 15px; color: #31572C;'>{username}</code></td>
        </tr>
        <tr>
            <td style='padding: 10px 14px; background: #fafafa; border-radius: 0 0 0 6px; font-weight: bold;'>Mật khẩu</td>
            <td style='padding: 10px 14px; background: #fafafa; border-radius: 0 0 6px 0;'><code style='font-size: 15px; color: #d32f2f;'>{password}</code></td>
        </tr>
    </table>
    <p style='color: #d32f2f; font-size: 13px;'>⚠️ Vui lòng đổi mật khẩu sau khi đăng nhập lần đầu.</p>
    <hr style='border: none; border-top: 1px solid #e0e0e0; margin: 16px 0;'/>
    <p style='color: #888; font-size: 12px; text-align: center;'>Email này được gửi tự động từ hệ thống ECOVA. Vui lòng không trả lời.</p>
</div>";
        }
    }
}
