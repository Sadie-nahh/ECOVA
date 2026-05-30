using EnvContract.Common.Helpers;
using FluentAssertions;
using System;
using Xunit;

namespace EnvContract.Tests.Helpers;

/// <summary>
/// Unit tests cho AesHelper (AES-256 CBC mã hóa/giải mã chuỗi).
/// AesHelper dùng key dẫn xuất SHA-256 và IV fixed MD5 — key nhúng trong binary.
/// Tests xác minh tính đúng đắn của roundtrip và error handling.
/// </summary>
public class AesHelperTests
{
    // ── Test: Encrypt ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Encrypt_ShouldProduceAesPrefixedResult()
    {
        // Act
        string ciphertext = AesHelper.Encrypt("my-secret-password");

        // Assert
        ciphertext.Should().NotBeNullOrEmpty("Ciphertext không được rỗng");
        ciphertext.Should().StartWith(AesHelper.Prefix,
            $"Kết quả phải bắt đầu bằng prefix '{AesHelper.Prefix}'");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Encrypt_EmptyString_ShouldReturnEmptyString()
    {
        // AesHelper.Encrypt có guard: if (IsNullOrEmpty) return plaintext
        string result = AesHelper.Encrypt("");

        result.Should().BeEmpty("Encrypt chuỗi rỗng phải trả về chuỗi rỗng");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Encrypt_SameInput_ShouldProduceSameCiphertext()
    {
        // AES với fixed key và fixed IV → deterministic
        string plaintext = "ecova-smtp-password-2025";
        string cipher1 = AesHelper.Encrypt(plaintext);
        string cipher2 = AesHelper.Encrypt(plaintext);

        cipher1.Should().Be(cipher2,
            "AES với fixed key+IV phải deterministic (mã hóa cùng plaintext → cùng ciphertext)");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Encrypt_DifferentInputs_ShouldProduceDifferentCiphertexts()
    {
        string cipher1 = AesHelper.Encrypt("password1");
        string cipher2 = AesHelper.Encrypt("password2");

        cipher1.Should().NotBe(cipher2,
            "Hai plaintext khác nhau phải cho ciphertext khác nhau");
    }

    // ── Test: Decrypt ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Decrypt_ValidCiphertext_ShouldReturnOriginalPlaintext()
    {
        // Arrange
        string original   = "smtp-password-ecova@gmail.com";
        string encrypted  = AesHelper.Encrypt(original);

        // Act
        string decrypted = AesHelper.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(original,
            "Decrypt(Encrypt(x)) == x (roundtrip phải đúng)");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Decrypt_WithoutPrefix_ShouldStillDecryptIfValidBase64()
    {
        // AesHelper.Decrypt tự bỏ prefix nếu có, thử decode base64 nếu không có prefix
        string original  = "test-value";
        string encrypted = AesHelper.Encrypt(original);

        // Bỏ prefix thủ công
        string withoutPrefix = encrypted.Substring(AesHelper.Prefix.Length);
        string decrypted = AesHelper.Decrypt(withoutPrefix);

        decrypted.Should().Be(original,
            "Decrypt không cần prefix — vẫn decode được base64 hợp lệ");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Decrypt_InvalidBase64_ShouldReturnNull()
    {
        // Garbage input — không phải base64 hợp lệ
        string result = AesHelper.Decrypt("AES:!!!not-valid-base64!!!");

        result.Should().BeNull(
            "Decrypt chuỗi corrupt phải trả về null (không throw exception)");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Decrypt_EmptyString_ShouldReturnEmptyString()
    {
        // Guard trong Decrypt: if (IsNullOrEmpty) return cipherText
        string result = AesHelper.Decrypt("");

        result.Should().BeEmpty("Decrypt chuỗi rỗng phải trả về chuỗi rỗng");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void Decrypt_RandomBase64_ShouldReturnNull()
    {
        // Valid base64 nhưng không phải AES ciphertext của key này
        string randomBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        string result = AesHelper.Decrypt(randomBase64);

        result.Should().BeNull("Base64 ngẫu nhiên không decrypt được phải trả về null");
    }

    // ── Test: IsAesCipherText ─────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "AesHelper")]
    public void IsAesCipherText_WithAesPrefix_ReturnsTrue()
    {
        bool result = AesHelper.IsAesCipherText("AES:someBase64Data==");

        result.Should().BeTrue("Chuỗi bắt đầu bằng 'AES:' phải được nhận diện là ciphertext");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void IsAesCipherText_RealEncryptedValue_ReturnsTrue()
    {
        string encrypted = AesHelper.Encrypt("test-password");

        AesHelper.IsAesCipherText(encrypted).Should().BeTrue(
            "Kết quả của Encrypt() phải qua được IsAesCipherText");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void IsAesCipherText_PlainText_ReturnsFalse()
    {
        bool result = AesHelper.IsAesCipherText("plaintext-password");

        result.Should().BeFalse("Plaintext không có prefix phải trả về false");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void IsAesCipherText_EmptyString_ReturnsFalse()
    {
        bool result = AesHelper.IsAesCipherText("");

        result.Should().BeFalse("Chuỗi rỗng phải trả về false");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    public void IsAesCipherText_NullString_ReturnsFalse()
    {
        bool result = AesHelper.IsAesCipherText(null!);

        result.Should().BeFalse("Null phải trả về false (không throw)");
    }

    // ── Tests Phase 6: EncryptBytes / DecryptBytes (FaceID biometric at rest) ──

    [Fact]
    [Trait("Category", "AesHelper")]
    [Trait("Category", "Phase6")]
    public void EncryptBytes_Roundtrip_ShouldReturnOriginalBytes()
    {
        // Arrange: giả lập JPEG header bytes
        byte[] original = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        string base64 = AesHelper.EncryptBytes(original);
        byte[]? decrypted = AesHelper.DecryptBytes(base64);

        // Assert
        decrypted.Should().NotBeNull("DecryptBytes phải trả về kết quả hợp lệ");
        decrypted!.Should().BeEquivalentTo(original,
            "Roundtrip EncryptBytes → DecryptBytes phải khôi phục data gốc chính xác từng byte");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    [Trait("Category", "Phase6")]
    public void EncryptBytes_NullInput_ShouldReturnEmptyString()
    {
        string result = AesHelper.EncryptBytes(null!);

        result.Should().BeEmpty("EncryptBytes(null) phải trả về string.Empty (guard)");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    [Trait("Category", "Phase6")]
    public void EncryptBytes_EmptyArray_ShouldReturnEmptyString()
    {
        string result = AesHelper.EncryptBytes(Array.Empty<byte>());

        result.Should().BeEmpty("EncryptBytes(byte[0]) phải trả về string.Empty (guard)");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    [Trait("Category", "Phase6")]
    public void EncryptBytes_SameInput_ShouldProduceSameBase64()
    {
        // AES với fixed key+IV → deterministic
        byte[] data = new byte[] { 10, 20, 30, 40, 50 };
        string base64_1 = AesHelper.EncryptBytes(data);
        string base64_2 = AesHelper.EncryptBytes(data);

        base64_1.Should().Be(base64_2,
            "AES fixed key+IV → cùng input phải ra cùng ciphertext (deterministic)");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    [Trait("Category", "Phase6")]
    public void EncryptBytes_DifferentInputs_ShouldProduceDifferentBase64()
    {
        byte[] data1 = new byte[] { 1, 2, 3 };
        byte[] data2 = new byte[] { 4, 5, 6 };

        string base64_1 = AesHelper.EncryptBytes(data1);
        string base64_2 = AesHelper.EncryptBytes(data2);

        base64_1.Should().NotBe(base64_2,
            "Hai input khác nhau phải cho ciphertext khác nhau");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    [Trait("Category", "Phase6")]
    public void DecryptBytes_InvalidBase64_ShouldReturnNull()
    {
        // Data cũ (chưa mã hóa) là JPEG thuần — DecryptBytes sẽ fail, trả null → caller fallback
        byte[]? result = AesHelper.DecryptBytes("!!!invalid-base64-not-aes!!!");

        result.Should().BeNull(
            "DecryptBytes với chuỗi không phải AES ciphertext phải trả về null (không throw) — " +
            "để caller có thể dùng raw bytes làm fallback (backward compat)");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    [Trait("Category", "Phase6")]
    public void DecryptBytes_NullOrEmptyBase64_ShouldReturnNull()
    {
        AesHelper.DecryptBytes(null!).Should().BeNull("null input → null (guard)");
        AesHelper.DecryptBytes("").Should().BeNull("empty input → null (guard)");
    }

    [Fact]
    [Trait("Category", "AesHelper")]
    [Trait("Category", "Phase6")]
    public void EncryptBytes_LargeFaceData_ShouldRoundtripCorrectly()
    {
        // Giả lập JPEG data kích thước thực tế (~50KB face image)
        var rng = new Random(42);
        byte[] fakeJpeg = new byte[50_000];
        rng.NextBytes(fakeJpeg);

        string encrypted = AesHelper.EncryptBytes(fakeJpeg);
        byte[]? decrypted = AesHelper.DecryptBytes(encrypted);

        decrypted.Should().NotBeNull("Dữ liệu lớn vẫn phải roundtrip thành công");
        decrypted!.Length.Should().Be(fakeJpeg.Length,
            "Độ dài byte sau decrypt phải khớp với data gốc");
        decrypted.Should().BeEquivalentTo(fakeJpeg,
            "Nội dung 50KB face data phải khôi phục chính xác 100% sau roundtrip AES-256");
    }
}
