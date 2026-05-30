using EnvContract.BLL.Services;
using EnvContract.DAL.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace EnvContract.Tests.Services;

/// <summary>
/// Unit tests cho AuthService.
/// OTP store là static Dictionary — cần dùng reflection để peek/reset.
/// Phase 2: _otpAttempts (rate limiting) cũng là static — reset trong Dispose().
/// Các tests được thiết kế độc lập: dùng unique email riêng mỗi test.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly AuthService _sut;
    private readonly Mock<IUserRepository> _userRepoMock;

    // Reflection — _otpStore (OTP codes)
    private static readonly FieldInfo OtpStoreField =
        typeof(AuthService).GetField(
            "_otpStore",
            BindingFlags.Static | BindingFlags.NonPublic)!;

    // Reflection — _otpAttempts (rate limiting, added Phase 2)
    private static readonly FieldInfo OtpAttemptsField =
        typeof(AuthService).GetField(
            "_otpAttempts",
            BindingFlags.Static | BindingFlags.NonPublic)!;

    public AuthServiceTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _sut = new AuthService(_userRepoMock.Object);
    }

    // Reset CẢ 2 static stores sau mỗi test — tránh interference
    public void Dispose()
    {
        (OtpStoreField.GetValue(null)   as System.Collections.IDictionary)?.Clear();
        (OtpAttemptsField.GetValue(null) as System.Collections.IDictionary)?.Clear();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Lấy OTP hiện tại của email từ static store (để test validation).</summary>
    private static string GetCurrentOtp(string email)
    {
        var store = OtpStoreField.GetValue(null)
            as Dictionary<string, (string Code, DateTime ExpiresAt)>;
        return store?.TryGetValue(email, out var entry) == true ? entry.Code : string.Empty;
    }

    /// <summary>Inject entry OTP đã hết hạn để test expiry.</summary>
    private static void InjectExpiredOtp(string email, string code)
    {
        var store = OtpStoreField.GetValue(null)
            as Dictionary<string, (string Code, DateTime ExpiresAt)>;
        store![email] = (code, DateTime.UtcNow.AddMinutes(-1)); // Quá khứ = đã hết hạn
    }

    // ── Test: GenerateOtp ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "AuthService")]
    public void GenerateOtp_ShouldReturn6DigitNumericString()
    {
        // Arrange
        string email = "test_format@ecova.vn";

        // Act
        string otp = _sut.GenerateOtp(email);

        // Assert
        otp.Should().NotBeNullOrEmpty("OTP không được rỗng");
        otp.Should().HaveLength(6, "OTP phải đúng 6 chữ số");
        otp.Should().MatchRegex("^[0-9]{6}$", "OTP chỉ chứa chữ số");
        // Verify range: 100000 ≤ code ≤ 999999
        int.Parse(otp).Should().BeInRange(100_000, 999_999, "OTP phải trong phạm vi 100000-999999");
    }

    [Fact]
    [Trait("Category", "AuthService")]
    public void GenerateOtp_SameEmail_OverwritesPreviousOtp()
    {
        // Arrange
        string email = "overwrite@ecova.vn";

        // Act
        string first  = _sut.GenerateOtp(email);
        string second = _sut.GenerateOtp(email);

        // Assert: OTP trong store phải là lần gọi thứ 2
        string stored = GetCurrentOtp(email);
        stored.Should().Be(second, "GenerateOtp phải ghi đè OTP cũ bằng OTP mới");
    }

    // ── Test: ValidateOtpAsync ───────────────────────────────────────────────

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ValidateOtp_CorrectCode_ReturnsTrue()
    {
        // Arrange
        string email = "validate_correct@ecova.vn";
        _sut.GenerateOtp(email);
        string code = GetCurrentOtp(email);
        code.Should().NotBeEmpty("Phải tạo được OTP trước khi validate");

        // Act
        bool result = await _sut.ValidateOtpAsync(email, code);

        // Assert
        result.Should().BeTrue("Nhập đúng OTP phải trả về true");
    }

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ValidateOtp_WrongCode_ReturnsFalse()
    {
        // Arrange
        string email = "validate_wrong@ecova.vn";
        _sut.GenerateOtp(email);
        string wrongCode = "000000"; // Chắc chắn sai (OTP range 100000-999999)

        // Act
        bool result = await _sut.ValidateOtpAsync(email, wrongCode);

        // Assert
        result.Should().BeFalse("Nhập sai OTP phải trả về false");
    }

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ValidateOtp_NoOtpGenerated_ReturnsFalse()
    {
        // Arrange: email chưa yêu cầu OTP bao giờ
        string email = "no_otp@ecova.vn";

        // Act
        bool result = await _sut.ValidateOtpAsync(email, "123456");

        // Assert
        result.Should().BeFalse("Email chưa generate OTP phải trả về false");
    }

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ValidateOtp_ExpiredOtp_ReturnsFalse()
    {
        // Arrange: inject OTP đã hết hạn
        string email = "expired@ecova.vn";
        InjectExpiredOtp(email, "654321");

        // Act
        bool result = await _sut.ValidateOtpAsync(email, "654321");

        // Assert
        result.Should().BeFalse("OTP hết hạn phải bị reject");
    }

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ValidateOtp_OneTimeUse_SecondCallReturnsFalse()
    {
        // Arrange
        string email = "onetime@ecova.vn";
        _sut.GenerateOtp(email);
        string code = GetCurrentOtp(email);

        // Act
        bool firstUse  = await _sut.ValidateOtpAsync(email, code);
        bool secondUse = await _sut.ValidateOtpAsync(email, code);

        // Assert
        firstUse.Should().BeTrue("Lần đầu đúng phải pass");
        secondUse.Should().BeFalse("OTP đã dùng phải bị reject (one-time use)");
    }

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ValidateOtp_CaseInsensitiveEmail_ReturnsTrue()
    {
        // Arrange: generate với email chữ HOA, validate với chữ thường
        string emailUpper = "CASEEMAIL@ECOVA.VN";
        string emailLower = "caseemail@ecova.vn";
        _sut.GenerateOtp(emailUpper);
        string code = GetCurrentOtp(emailLower); // Dictionary là OrdinalIgnoreCase

        // Act
        bool result = await _sut.ValidateOtpAsync(emailLower, code);

        // Assert
        result.Should().BeTrue("Email so sánh phải case-insensitive");
    }

    // ── Test: ChangePasswordByEmailAsync ────────────────────────────────────

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ChangePassword_ValidInput_ReturnsTrueAndCallsRepo()
    {
        // Arrange
        string email   = "change_pw@ecova.vn";
        string newPass = "NewPass@123";
        _userRepoMock
            .Setup(r => r.UpdatePasswordByEmailAsync(
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        bool result = await _sut.ChangePasswordByEmailAsync(email, newPass);

        // Assert
        result.Should().BeTrue("Đổi mật khẩu hợp lệ phải trả về true");
        _userRepoMock.Verify(
            r => r.UpdatePasswordByEmailAsync(
                email,
                It.Is<string>(h => h.StartsWith("$2"))), // BCrypt hash format
            Times.Once,
            "Phải gọi repo đúng 1 lần với BCrypt hash");
    }

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ChangePassword_EmptyEmail_ReturnsFalseWithoutCallingRepo()
    {
        // Act
        bool result = await _sut.ChangePasswordByEmailAsync("", "somepassword");

        // Assert
        result.Should().BeFalse("Email rỗng phải trả về false");
        _userRepoMock.Verify(
            r => r.UpdatePasswordByEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Không được gọi repo khi input invalid");
    }

    [Fact]
    [Trait("Category", "AuthService")]
    public async Task ChangePassword_EmptyPassword_ReturnsFalseWithoutCallingRepo()
    {
        // Act
        bool result = await _sut.ChangePasswordByEmailAsync("user@ecova.vn", "");

        // Assert
        result.Should().BeFalse("Mật khẩu rỗng phải trả về false");
        _userRepoMock.Verify(
            r => r.UpdatePasswordByEmailAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "Không được gọi repo khi mật khẩu rỗng");
    }

    // ── Test: Phase 2 Security — CSPRNG OTP ─────────────────────────────────

    [Fact]
    [Trait("Category", "AuthService_Security")]
    public void GenerateOtp_CSPRNG_Uniqueness_Across20Calls()
    {
        // CSPRNG phải tạo OTP không trùng lặp ngay cả khi gọi liên tiếp.
        var distinctOtps = new System.Collections.Generic.HashSet<string>();

        for (int i = 0; i < 20; i++)
        {
            string otp = _sut.GenerateOtp($"csprng_{i}@ecova.vn");
            distinctOtps.Add(otp);
            otp.Should().MatchRegex(@"^\d{6}$", $"Lần {i}: OTP phải là 6 chữ số");
            int.Parse(otp).Should().BeInRange(100_000, 999_999, $"Lần {i}: OTP phải trong range");
        }

        distinctOtps.Count.Should().BeGreaterThan(15,
            "CSPRNG phải tạo đủ nhiều giá trị khác nhau trong 20 lần gọi liên tiếp");
    }

    // ── Test: Phase 2 Security — OTP Rate Limiting ───────────────────────────

    [Fact]
    [Trait("Category", "AuthService_Security")]
    public async Task ValidateOtp_RateLimit_BlocksAfter5WrongAttempts()
    {
        // Arrange
        string email = "ratelimit_test@ecova.vn";
        _sut.GenerateOtp(email);
        string wrongCode = "000000"; // Chắc chắn sai (ngoài range 100000-999999)

        // Act: nhập sai 5 lần liên tiếp
        for (int i = 0; i < 5; i++)
        {
            bool r = await _sut.ValidateOtpAsync(email, wrongCode);
            r.Should().BeFalse($"Lần {i + 1}: OTP sai phải trả về false");
        }

        // Lần 6: phải bị rate-limited
        bool blocked = await _sut.ValidateOtpAsync(email, wrongCode);
        blocked.Should().BeFalse("Lần thứ 6 phải bị rate-limited (trả về false ngay lập tức)");
    }

    [Fact]
    [Trait("Category", "AuthService_Security")]
    public async Task GenerateOtp_ResetsRateLimitCounter_AllowsValidationAgain()
    {
        // Arrange: trigger rate limit
        string email = "reset_ratelimit@ecova.vn";
        _sut.GenerateOtp(email);
        for (int i = 0; i < 5; i++)
            await _sut.ValidateOtpAsync(email, "000000"); // Nhập sai 5 lần

        // Act: generate OTP mới → phải reset _otpAttempts counter
        _sut.GenerateOtp(email);
        string code = GetCurrentOtp(email);
        code.Should().NotBeEmpty("Phải có OTP mới sau GenerateOtp lần 2");

        // Assert: validate đúng phải thành công sau khi counter reset
        bool result = await _sut.ValidateOtpAsync(email, code);
        result.Should().BeTrue(
            "GenerateOtp mới phải reset rate limit counter — validate đúng phải pass");
    }
}