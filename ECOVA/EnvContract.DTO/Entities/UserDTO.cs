using System;

namespace EnvContract.DTO.Entities
{
    public class UserDTO
    {
        public string UserID { get; set; }       // DB: UserId
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public byte[] AvatarData { get; set; }   // Ảnh đại diện hiển thị
        public byte[] FaceIDData { get; set; }    // Dữ liệu nhận diện khuôn mặt
        public bool IsFaceIDRegistered { get; set; }   // true = đã đăng ký FaceID
        public string RoleID { get; set; }       // DB: RoleId
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }        // DB: Phone
        public DateTime? DateOfBirth { get; set; } // DB: DateOfBirth
        public string Department { get; set; }   // DB: Department
        public string EmployeeCode { get; set; } // DB: EmployeeCode
    }
}