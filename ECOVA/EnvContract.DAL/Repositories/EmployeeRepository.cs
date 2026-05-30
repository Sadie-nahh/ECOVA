using Dapper;
using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class EmployeeRepository : IEmployeeRepository
    {
        public async Task<string> UpsertEmployeeAsync(UserDTO employee)
        {
            var p = new DynamicParameters();
            p.Add("@UserID",       employee.UserID);
            p.Add("@Username",     string.IsNullOrEmpty(employee.Username) ? employee.Email : employee.Username);
            p.Add("@PasswordHash", employee.PasswordHash);
            p.Add("@FullName",     employee.FullName);
            p.Add("@Email",        employee.Email);
            p.Add("@RoleID",       employee.RoleID);
            p.Add("@Phone",        employee.Phone,      DbType.String);
            p.Add("@Address",      employee.Address,    DbType.String);
            p.Add("@Department",   employee.Department, DbType.String);
            p.Add("@DateOfBirth",  employee.DateOfBirth,DbType.DateTime);
            p.Add("@EmployeeCode", employee.EmployeeCode);
            p.Add("@AvatarData",   employee.AvatarData, DbType.Binary);
            p.Add("@Action",       dbType: DbType.String, direction: ParameterDirection.Output, size: 10);

            await SqlHelper.ExecuteSpWithOutputAsync("sp_UpsertEmployee", p);
            return p.Get<string>("@Action");
        }

        public async Task UpdateEmployeeAsync(UserDTO employee)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdateEmployee", new
            {
                employee.UserID,
                Username = string.IsNullOrEmpty(employee.Username) ? employee.Email : employee.Username,
                employee.PasswordHash, employee.FullName, employee.Email,
                employee.RoleID, employee.IsActive, employee.Phone, employee.Address,
                employee.Department, employee.DateOfBirth, employee.EmployeeCode, employee.AvatarData
            });
        }

        public async Task ToggleActiveAsync(string userId, bool isActive)
        {
            await SqlHelper.ExecuteSpAsync("sp_ToggleEmployeeActive", new
            {
                UserID   = userId,
                IsActive = isActive ? 1 : 0
            });
        }

        public async Task DeleteEmployeeAsync(string employeeId)
        {
            await SqlHelper.ExecuteSpAsync("sp_DeleteEmployee", new { UserID = employeeId });
        }

        public async Task<List<UserDTO>> GetAllEmployeesAsync()
        {
            var result = await SqlHelper.QuerySpAsync<UserDTO>("sp_GetEmployeeList");
            return result.ToList();
        }

        public async Task<UserDTO?> GetEmployeeByIdAsync(string employeeId)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<UserDTO>(
                "sp_GetEmployeeById", new { UserID = employeeId });
        }

        public async Task<List<UserDTO>> SearchEmployeesAsync(string keyword)
        {
            var result = await SqlHelper.QuerySpAsync<UserDTO>(
                "sp_GetEmployeeList", new { Keyword = keyword });
            return result.ToList();
        }

        public async Task<UserDTO?> GetEmployeeByEmailAsync(string email)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<UserDTO>(
                "sp_GetEmployeeByEmail", new { Email = email });
        }

        public async Task<(string UserID, string EmployeeCode, string Username)> GenerateEmployeeCodeAsync(string roleId)
        {
            var p = new DynamicParameters();
            p.Add("@RoleID",       roleId);
            p.Add("@UserID",       dbType: DbType.String, direction: ParameterDirection.Output, size: 10);
            p.Add("@EmployeeCode", dbType: DbType.String, direction: ParameterDirection.Output, size: 20);
            p.Add("@Username",     dbType: DbType.String, direction: ParameterDirection.Output, size: 50);

            await SqlHelper.ExecuteSpWithOutputAsync("sp_GenerateEmployeeCode", p);

            return (
                p.Get<string>("@UserID"),
                p.Get<string>("@EmployeeCode"),
                p.Get<string>("@Username")
            );
        }
    }
}
