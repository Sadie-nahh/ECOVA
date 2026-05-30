using EnvContract.Common.Helpers;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace EnvContract.Tests.Helpers;

/// <summary>
/// Unit tests cho SecurityHelper (BCrypt + CSPRNG password generation).
/// SecurityHelper đã dùng RandomNumberGenerator (CSPRNG) — không phải System.Random.
/// HashPassword là BCrypt — mỗi lần hash sẽ có salt khác nhau.
/// </summary>
public class SecurityHelperTests
{
    // ── Test: GenerateRandomPassword ──────────────────────────────────────────

    [Theory]
    [Trait("Category", "SecurityHelper")]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(20)]
    public void GenerateRandomPassword_ShouldReturnCorrectLength(int length)
    {
        // Act
        string password = SecurityHelper.GenerateRandomPassword(length);

        // Assert
        password.Should().NotBeNullOrEmpty("Mật khẩu không được rỗng");
        password.Should().HaveLength(length, $"Mật khẩu phải đúng {length} ký tự");
    }

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void GenerateRandomPassword_DefaultLength_Returns12Characters()
    {
        // Act
        string password = SecurityHelper.GenerateRandomPassword(); // default = 12

        // Assert
        password.Should().HaveLength(12, "Mật khẩu mặc định phải là 12 ký tự");
    }

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void GenerateRandomPassword_TwoCalls_ShouldProduceDifferentResults()
    {
        // Act — CSPRNG nên cực kỳ hiếm khi trùng
        var passwords = new HashSet<string>();
        for (int i = 0; i < 5; i++)
            passwords.Add(SecurityHelper.GenerateRandomPassword());

        // Assert
        passwords.Count.Should().BeGreaterThan(1,
            "5 lần gọi CSPRNG phải tạo ra ít nhất 2 mật khẩu khác nhau");
    }

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void GenerateRandomPassword_ShouldOnlyContainAllowedCharacters()
    {
        // Arrange — charset từ SecurityHelper source: ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789@#$!
        const string allowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789@#$!";

        // Act
        string password = SecurityHelper.GenerateRandomPassword(20);

        // Assert
        foreach (char c in password)
        {
            allowedChars.Should().Contain(c.ToString(),
                $"Ký tự '{c}' không nằm trong bộ ký tự cho phép");
        }
    }

    // ── Test: HashPassword ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void HashPassword_ShouldProduceBCryptFormatHash()
    {
        // Act
        string hash = SecurityHelper.HashPassword("myTestPassword123");

        // Assert
        hash.Should().NotBeNullOrEmpty("Hash không được rỗng");
        hash.Should().StartWith("$2", "BCrypt hash phải bắt đầu bằng $2a, $2b hoặc $2y");
        hash.Length.Should().BeGreaterThan(50, "BCrypt hash thường dài 60 ký tự");
    }

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void HashPassword_SamePlaintext_ProducesDifferentHashes()
    {
        // BCrypt dùng random salt — mỗi lần hash khác nhau
        string hash1 = SecurityHelper.HashPassword("samePassword");
        string hash2 = SecurityHelper.HashPassword("samePassword");

        hash1.Should().NotBe(hash2,
            "BCrypt dùng random salt nên 2 lần hash cùng plaintext phải khác nhau");
    }

    // ── Test: VerifyPassword ──────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        string plaintext = "CorrectPassword@2025";
        string hash = SecurityHelper.HashPassword(plaintext);

        // Act
        bool result = SecurityHelper.VerifyPassword(plaintext, hash);

        // Assert
        result.Should().BeTrue("Mật khẩu đúng phải verify thành công");
    }

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        // Arrange
        string hash = SecurityHelper.HashPassword("CorrectPassword");

        // Act
        bool result = SecurityHelper.VerifyPassword("WrongPassword", hash);

        // Assert
        result.Should().BeFalse("Mật khẩu sai phải verify thất bại");
    }

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void VerifyPassword_EmptyHash_ReturnsFalse()
    {
        // Act — SecurityHelper.VerifyPassword check IsNullOrEmpty trước
        bool result = SecurityHelper.VerifyPassword("anypassword", "");

        // Assert
        result.Should().BeFalse("Hash rỗng phải trả về false (không throw exception)");
    }

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void VerifyPassword_NullHash_ReturnsFalse()
    {
        // Act — test null safety
        bool result = SecurityHelper.VerifyPassword("anypassword", null!);

        // Assert
        result.Should().BeFalse("Hash null phải trả về false (không throw NullReferenceException)");
    }

    [Fact]
    [Trait("Category", "SecurityHelper")]
    public void VerifyPassword_CaseSensitive_ReturnsFalse()
    {
        // BCrypt verify là case-sensitive
        string hash = SecurityHelper.HashPassword("MyPassword");

        bool result = SecurityHelper.VerifyPassword("mypassword", hash); // lowercase

        result.Should().BeFalse("BCrypt verify phải phân biệt chữ hoa/thường");
    }
}
