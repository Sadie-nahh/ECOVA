using EnvContract.DTO.Entities;
using System.Threading.Tasks;

namespace EnvContract.DAL.Interfaces
{
    public interface IUserRepository
    {
        Task<UserDTO> GetByUsernameAsync(string username);
        Task<UserDTO?> GetByUserIdAsync(string userId);
        Task<UserDTO?> GetByEmailAsync(string email);
        Task<string> GetPasswordHashByUsernameAsync(string username);
        Task UpdateFaceIDDataAsync(string userId, byte[] faceData);
        Task UpdateAvatarDataAsync(string userId, byte[] avatarData);
        Task SetFaceIDRegisteredAsync(string userId, bool registered);
        /// <summary>
        /// Atomic: lưu FaceIDData và set IsFaceIDRegistered=1 trong cùng 1 transaction.
        /// Đảm bảo nếu step 2 fail, step 1 sẽ bị rollback — tránh data inconsistency.
        /// </summary>
        Task RegisterFaceIDAsync(string userId, byte[] faceData);
        Task UpdatePasswordAsync(string username, string newPasswordHash);
        Task UpdatePasswordByEmailAsync(string email, string newPasswordHash);
        Task UpdateProfileAsync(UserDTO user);
    }
}
