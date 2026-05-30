using EnvContract.DAL.Database;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using System.Threading.Tasks;

namespace EnvContract.DAL.Repositories
{
    public class UserRepository : IUserRepository
    {
        public async Task<UserDTO> GetByUsernameAsync(string username)
        {
            var result = await SqlHelper.QuerySingleOrDefaultSpAsync<UserDTO>(
                "sp_AuthenticateUser", new { Username = username });
            return result!;
        }

        public async Task<UserDTO?> GetByUserIdAsync(string userId)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<UserDTO?>(
                "sp_GetUserById", new { UserID = userId });
        }

        public async Task<UserDTO?> GetByEmailAsync(string email)
        {
            return await SqlHelper.QuerySingleOrDefaultSpAsync<UserDTO?>(
                "sp_GetUserByEmail", new { Email = email });
        }

        public async Task<string> GetPasswordHashByUsernameAsync(string username)
        {
            // sp_AuthenticateUser trả về full user row — lấy PasswordHash
            var user = await SqlHelper.QuerySingleOrDefaultSpAsync<UserDTO>(
                "sp_AuthenticateUser", new { Username = username });
            return user?.PasswordHash ?? string.Empty;
        }

        public async Task UpdateFaceIDDataAsync(string userId, byte[] faceData)
        {
            // Dùng sp_RegisterFaceID thay vì UPDATE trực tiếp
            await SqlHelper.ExecuteSpAsync("sp_RegisterFaceID", new { UserID = userId, FaceData = faceData });
        }

        public async Task UpdateAvatarDataAsync(string userId, byte[] avatarData)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdateAvatar", new { UserID = userId, AvatarData = avatarData });
        }

        public async Task SetFaceIDRegisteredAsync(string userId, bool registered)
        {
            // sp_RegisterFaceID đã set IsFaceIDRegistered=1 khi đăng ký
            // Nếu cần unset, gọi SP riêng (hiện tại RegisterFaceID luôn set=1)
            await SqlHelper.ExecuteSpAsync("sp_RegisterFaceID", new { UserID = userId, FaceData = (byte[]?)null });
        }

        public async Task RegisterFaceIDAsync(string userId, byte[] faceData)
        {
            await SqlHelper.ExecuteSpAsync("sp_RegisterFaceID", new { UserID = userId, FaceData = faceData });
        }

        public async Task UpdatePasswordAsync(string username, string newPasswordHash)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdatePassword", new { Username = username, PasswordHash = newPasswordHash });
        }

        public async Task UpdatePasswordByEmailAsync(string email, string newPasswordHash)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdatePasswordByEmail", new { Email = email, PasswordHash = newPasswordHash });
        }

        public async Task UpdateProfileAsync(UserDTO user)
        {
            await SqlHelper.ExecuteSpAsync("sp_UpdateProfile", new
            {
                user.UserID, user.FullName, user.Email,
                user.Phone, user.Address, user.DateOfBirth, user.Department
            });
        }
    }
}
