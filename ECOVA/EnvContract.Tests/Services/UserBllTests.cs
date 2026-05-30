using EnvContract.BLL.Services;
using EnvContract.DAL.Interfaces;
using EnvContract.DTO.Entities;
using FluentAssertions;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace EnvContract.Tests.Services;

/// <summary>
/// Unit tests cho UserBLL.
/// QUAN TRỌNG: LoginAsync trả về bool (không phải UserDTO).
/// Dùng GetByUsernameAsync để mock — UserBLL dùng single query pattern.
///
/// Lưu ý: LoginAsync cũng gọi SystemAuditHelper.LogAsync (fire-and-forget).
/// Trong test, DB connection sẽ fail nhẹ nhàng vì không có SQL Server — không ảnh hưởng kết quả.
/// </summary>
public class UserBllTests
{
    private readonly UserBLL _sut;
    private readonly Mock<IUserRepository> _userRepoMock;

    public UserBllTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _sut = new UserBLL(_userRepoMock.Object);
    }

    // ── Test: LoginAsync (trả về bool) ───────────────────────────────────────

    [Fact]
    [Trait("Category", "UserBLL")]
    public async Task LoginAsync_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        string username  = "testuser_correct";
        string password  = "CorrectPass@123";
        string hashedPw  = BCrypt.Net.BCrypt.HashPassword(password);

        // UserBLL dùng GetByUsernameAsync (single-query pattern, bao gồm cả PasswordHash)
        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(username))
            .ReturnsAsync(new UserDTO
            {
                UserID       = "NV001",
                Username     = username,
                PasswordHash = hashedPw,
                IsActive     = true,
                RoleID       = "ROL001"
            });

        // Act
        bool result = await _sut.LoginAsync(username, password);

        // Assert
        result.Should().BeTrue("Đăng nhập đúng mật khẩu phải trả về true");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    public async Task LoginAsync_WrongPassword_ReturnsFalse()
    {
        // Arrange
        string username  = "testuser_wrong";
        string correctPw = "CorrectPass@123";
        string wrongPw   = "WrongPass@999";
        string hashedPw  = BCrypt.Net.BCrypt.HashPassword(correctPw);

        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(username))
            .ReturnsAsync(new UserDTO
            {
                UserID       = "NV002",
                Username     = username,
                PasswordHash = hashedPw,
                IsActive     = true
            });

        // Act
        bool result = await _sut.LoginAsync(username, wrongPw);

        // Assert
        result.Should().BeFalse("Sai mật khẩu phải trả về false");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    public async Task LoginAsync_NullUserFromRepo_ReturnsFalse()
    {
        // Arrange: UserBLL kiểm tra user != null VÀ PasswordHash không rỗng.
        // Vì GetByUsernameAsync trả về Task<UserDTO> (non-nullable),
        // simulate "not found" bằng cách trả về UserDTO với PasswordHash = null.
        string username = "ghost_user";
        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(It.IsAny<string>()))
            .ReturnsAsync(new UserDTO
            {
                UserID       = string.Empty,
                Username     = username,
                PasswordHash = null!, // Simulate: không tìm thấy → PasswordHash null/empty
                IsActive     = false
            });

        // Act
        bool result = await _sut.LoginAsync(username, "anypassword");

        // Assert: !string.IsNullOrEmpty(user.PasswordHash) → false → login fail
        result.Should().BeFalse("User với PasswordHash null coi như không tồn tại → login fail");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    public async Task LoginAsync_EmptyUsername_ReturnsFalseWithoutCallingRepo()
    {
        // Guard check trong UserBLL: IsNullOrEmpty → trả về false ngay
        bool result = await _sut.LoginAsync("", "somepassword");

        // Assert
        result.Should().BeFalse("Username rỗng phải trả về false");
        _userRepoMock.Verify(
            r => r.GetByUsernameAsync(It.IsAny<string>()),
            Times.Never,
            "Không được gọi DB khi username rỗng (guard pattern)");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    public async Task LoginAsync_EmptyPassword_ReturnsFalseWithoutCallingRepo()
    {
        // Guard check: password rỗng → trả về false ngay
        bool result = await _sut.LoginAsync("validuser", "");

        result.Should().BeFalse("Mật khẩu rỗng phải trả về false");
        _userRepoMock.Verify(
            r => r.GetByUsernameAsync(It.IsAny<string>()),
            Times.Never,
            "Không được gọi DB khi mật khẩu rỗng (guard pattern)");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    public async Task LoginAsync_PasswordHashIsEmpty_ReturnsFalse()
    {
        // Arrange: user tồn tại nhưng PasswordHash rỗng (data inconsistency)
        string username = "broken_user";
        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(username))
            .ReturnsAsync(new UserDTO
            {
                UserID       = "NV003",
                Username     = username,
                PasswordHash = string.Empty, // Hash rỗng
                IsActive     = true
            });

        // Act
        bool result = await _sut.LoginAsync(username, "anypassword");

        // Assert: !string.IsNullOrEmpty(user.PasswordHash) sẽ false
        result.Should().BeFalse("User có PasswordHash rỗng không được authenticate");
    }

    // ── Test: CheckEmailExistsAsync ───────────────────────────────────────────

    [Fact]
    [Trait("Category", "UserBLL")]
    public async Task CheckEmailExists_ExistingEmail_ReturnsTrue()
    {
        // Arrange
        string email = "existing@ecova.vn";
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(email))
            .ReturnsAsync(new UserDTO { UserID = "NV001", Email = email });

        // Act
        bool exists = await _sut.CheckEmailExistsAsync(email);

        // Assert
        exists.Should().BeTrue("Email tồn tại trong DB phải trả về true");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    public async Task CheckEmailExists_NonExistentEmail_ReturnsFalse()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((UserDTO?)null);

        // Act
        bool exists = await _sut.CheckEmailExistsAsync("ghost@ecova.vn");

        // Assert
        exists.Should().BeFalse("Email không tồn tại phải trả về false");
    }

    // ── Tests Phase 6: FaceID Encryption (RegisterFaceIDAsync / GetFaceIDDataAsync) ──

    [Fact]
    [Trait("Category", "UserBLL")]
    [Trait("Category", "Phase6")]
    public async Task RegisterFaceIDAsync_ValidData_StoresEncryptedBytes()
    {
        // Arrange: raw JPEG bytes (simulated face image header)
        string username  = "faceid_user";
        string password  = "TestPass@123";
        string hashedPw  = BCrypt.Net.BCrypt.HashPassword(password);
        byte[] rawFace   = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5 }; // JPEG header

        _userRepoMock
            .Setup(r => r.GetPasswordHashByUsernameAsync(username))
            .ReturnsAsync(hashedPw);
        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(username))
            .ReturnsAsync(new UserDTO { UserID = "NV010", Username = username, IsActive = true });

        // Capture bytes that were actually written to the repository
        byte[]? storedBytes = null;
        _userRepoMock
            .Setup(r => r.RegisterFaceIDAsync(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Callback<string, byte[]>((_, b) => storedBytes = b)
            .Returns(Task.CompletedTask);

        // Act
        bool result = await _sut.RegisterFaceIDAsync(username, password, rawFace);

        // Assert – registration succeeded
        result.Should().BeTrue("RegisterFaceIDAsync với dữ liệu hợp lệ phải trả về true");

        // Assert – bytes stored to DB are NOT raw JPEG (they have been AES-encrypted)
        storedBytes.Should().NotBeNull("Repository phải được gọi với encrypted bytes");
        storedBytes!.Should().NotBeEquivalentTo(rawFace,
            "Bytes lưu DB phải là AES-256 ciphertext — không phải raw JPEG");

        // Assert – decrypt stored bytes should yield original
        string base64 = System.Text.Encoding.UTF8.GetString(storedBytes!);
        byte[]? decrypted = EnvContract.Common.Helpers.AesHelper.DecryptBytes(base64);
        decrypted.Should().NotBeNull("AES decrypt của stored bytes phải thành công");
        decrypted!.Should().BeEquivalentTo(rawFace,
            "Decrypt(EncryptBytes(rawFace)) == rawFace — roundtrip qua BLL phải đúng");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    [Trait("Category", "Phase6")]
    public async Task RegisterFaceIDAsync_EmptyFaceData_ReturnsFalse()
    {
        // Guard: faceData null/empty không được lưu vào DB
        bool result = await _sut.RegisterFaceIDAsync("user", "pass", Array.Empty<byte>());

        result.Should().BeFalse("faceData rỗng phải trả về false (guard)");
        _userRepoMock.Verify(
            r => r.RegisterFaceIDAsync(It.IsAny<string>(), It.IsAny<byte[]>()),
            Times.Never,
            "Không được gọi RegisterFaceIDAsync khi faceData rỗng");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    [Trait("Category", "Phase6")]
    public async Task GetFaceIDDataAsync_NotRegistered_ReturnsEmptyArray()
    {
        // User tồn tại nhưng IsFaceIDRegistered = false
        string username = "user_no_face";
        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(username))
            .ReturnsAsync(new UserDTO
            {
                UserID = "NV011",
                Username = username,
                IsActive = true,
                IsFaceIDRegistered = false,
                FaceIDData = null
            });

        byte[]? result = await _sut.GetFaceIDDataAsync(username);

        result.Should().NotBeNull("Không được trả về null — phân biệt 'không có' vs 'lỗi'");
        result.Should().BeEmpty("User chưa đăng ký FaceID phải trả về byte[0]");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    [Trait("Category", "Phase6")]
    public async Task GetFaceIDDataAsync_InactiveUser_ReturnsNull()
    {
        // User bị khoá → trả về null
        string username = "locked_user";
        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(username))
            .ReturnsAsync(new UserDTO
            {
                UserID = "NV012",
                Username = username,
                IsActive = false
            });

        byte[]? result = await _sut.GetFaceIDDataAsync(username);

        result.Should().BeNull("User bị khoá (IsActive=false) phải trả về null");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    [Trait("Category", "Phase6")]
    public async Task GetFaceIDDataAsync_EncryptedData_ReturnsDecryptedBytes()
    {
        // Phase 6: DB chứa AES-encrypted bytes → GetFaceIDDataAsync phải giải mã trước khi trả về
        string username = "face_encrypted_user";
        byte[] originalFace = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 10, 20, 30 }; // fake JPEG
        string base64 = EnvContract.Common.Helpers.AesHelper.EncryptBytes(originalFace);
        byte[] encryptedInDb = System.Text.Encoding.UTF8.GetBytes(base64);

        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(username))
            .ReturnsAsync(new UserDTO
            {
                UserID = "NV013",
                Username = username,
                IsActive = true,
                IsFaceIDRegistered = true,
                FaceIDData = encryptedInDb
            });

        byte[]? result = await _sut.GetFaceIDDataAsync(username);

        result.Should().NotBeNull("GetFaceIDDataAsync phải trả về dữ liệu đã giải mã");
        result!.Should().BeEquivalentTo(originalFace,
            "Giải mã AES phải khôi phục đúng JPEG bytes gốc — không trả về ciphertext");
    }

    [Fact]
    [Trait("Category", "UserBLL")]
    [Trait("Category", "Phase6")]
    public async Task GetFaceIDDataAsync_UnencryptedLegacyData_ReturnsFallbackRawBytes()
    {
        // Backward compat: row cũ lưu JPEG thuần (chưa mã hóa)
        // → DecryptBytes trả null → fallback trả raw bytes để không vỡ logic cũ
        string username = "legacy_face_user";
        byte[] rawJpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 99, 88, 77 }; // unencrypted JPEG

        _userRepoMock
            .Setup(r => r.GetByUsernameAsync(username))
            .ReturnsAsync(new UserDTO
            {
                UserID = "NV014",
                Username = username,
                IsActive = true,
                IsFaceIDRegistered = true,
                FaceIDData = rawJpeg // raw JPEG — NOT AES encrypted
            });

        byte[]? result = await _sut.GetFaceIDDataAsync(username);

        result.Should().NotBeNull("Fallback phải hoạt động — không được trả null khi có data cũ");
        result!.Should().BeEquivalentTo(rawJpeg,
            "Dữ liệu cũ chưa mã hóa phải được trả về nguyên vẹn (backward compat fallback)");
    }
}
