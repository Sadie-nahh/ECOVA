using EnvContract.Common;
using FluentAssertions;
using Xunit;

namespace EnvContract.Tests.Common;

/// <summary>
/// Unit tests cho LanguageManager.
/// QUAN TRỌNG:
///  - SetLanguage() nhận AppLanguage enum (KHÔNG phải string)
///  - ToggleLanguage() chuyển đổi VI↔EN
///  - Get() trả về "[key]" nếu key không tìm thấy (KHÔNG phải key plain text)
///  - LanguageManager là Singleton — tests trong xUnit chạy parallel có thể interfere.
///    Dùng [Collection] để chạy sequential.
/// </summary>
[Collection("LanguageManagerTests")] // Sequential vì singleton mutable state
public class LanguageManagerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetVI() => LanguageManager.Instance.SetLanguage(AppLanguage.Vietnamese);
    private static void SetEN() => LanguageManager.Instance.SetLanguage(AppLanguage.English);

    // ── Test: SetLanguage & IsVietnamese ──────────────────────────────────────

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void SetLanguage_VI_SetsIsVietnameseTrue()
    {
        // Act
        SetVI();

        // Assert
        LanguageManager.Instance.IsVietnamese.Should().BeTrue("SetLanguage(VI) phải set IsVietnamese=true");
        LanguageManager.Instance.IsEnglish.Should().BeFalse("IsEnglish phải false khi đang VI");
    }

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void SetLanguage_EN_SetsIsVietnameseFalse()
    {
        // Act
        SetEN();

        // Teardown
        SetVI();

        // Assert (giá trị check TRƯỚC teardown — dùng captured value)
        // Cách đúng: capture trước teardown
        SetEN();
        bool isVI  = LanguageManager.Instance.IsVietnamese;
        bool isEN  = LanguageManager.Instance.IsEnglish;
        SetVI();

        isVI.Should().BeFalse("SetLanguage(EN) phải set IsVietnamese=false");
        isEN.Should().BeTrue("SetLanguage(EN) phải set IsEnglish=true");
    }

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void CurrentLanguage_AfterSetVI_EqualsVietnamese()
    {
        SetVI();
        LanguageManager.Instance.CurrentLanguage
            .Should().Be(AppLanguage.Vietnamese, "CurrentLanguage phải phản ánh đúng sau SetLanguage");
    }

    // ── Test: Get() giá trị tiếng Việt ───────────────────────────────────────

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void Get_VietnameseKey_ReturnsCorrectViValue()
    {
        // Arrange
        SetVI();

        // Act
        string error = LanguageManager.Instance.Get("error");
        string info  = LanguageManager.Instance.Get("info");

        // Assert: kiểm tra giá trị thực tế từ LanguageManager source code
        error.Should().Be("Lỗi",           "Key 'error' trong VI phải là 'Lỗi'");
        info.Should().Be("Thông báo",      "Key 'info' trong VI phải là 'Thông báo'");
    }

    // ── Test: Get() giá trị tiếng Anh ────────────────────────────────────────

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void Get_EnglishKey_ReturnsCorrectEnValue()
    {
        // Arrange
        SetEN();
        string error = LanguageManager.Instance.Get("error");
        string info  = LanguageManager.Instance.Get("info");
        SetVI();

        // Assert
        error.Should().Be("Error",         "Key 'error' trong EN phải là 'Error'");
        info.Should().Be("Information",    "Key 'info' trong EN phải là 'Information'");
    }

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void Get_SameKey_DifferentLanguages_ReturnsDifferentValues()
    {
        // Arrange
        SetVI();
        string viValue = LanguageManager.Instance.Get("save");

        SetEN();
        string enValue = LanguageManager.Instance.Get("save");
        SetVI();

        // Assert
        viValue.Should().NotBe(enValue,
            "Key 'save' phải có giá trị khác nhau giữa VI và EN");
    }

    // ── Test: Get() với key không tồn tại ────────────────────────────────────

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void Get_NonExistentKey_ReturnsBracketWrappedKey()
    {
        // Theo source code: dict.TryGetValue(key, out var val) ? val : $"[{key}]"
        SetVI();
        string nonExistentKey = "xyz_nonexistent_key_12345";
        string result = LanguageManager.Instance.Get(nonExistentKey);

        result.Should().Be($"[{nonExistentKey}]",
            "Key không tồn tại phải trả về '[key]' (bracketed fallback)");
    }

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void Get_NonExistentKey_DoesNotThrow()
    {
        // Verify không throw exception
        SetVI();
        string result = string.Empty;

        var act = () => { result = LanguageManager.Instance.Get("does_not_exist_999"); };
        act.Should().NotThrow("Get() với key không tồn tại không được throw exception");
    }

    // ── Test: ToggleLanguage ──────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void ToggleLanguage_FromVI_SwitchesToEN()
    {
        // Arrange
        SetVI();
        LanguageManager.Instance.IsVietnamese.Should().BeTrue("Pre-condition: phải đang ở VI");

        // Act
        LanguageManager.Instance.ToggleLanguage();
        bool isEN = LanguageManager.Instance.IsEnglish;

        // Teardown
        SetVI();

        // Assert
        isEN.Should().BeTrue("Toggle từ VI phải chuyển sang EN");
    }

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void ToggleLanguage_FromEN_SwitchesToVI()
    {
        // Arrange
        SetEN();

        // Act
        LanguageManager.Instance.ToggleLanguage();
        bool isVI = LanguageManager.Instance.IsVietnamese;

        // Teardown (đã về VI rồi, nhưng để an toàn)
        SetVI();

        // Assert
        isVI.Should().BeTrue("Toggle từ EN phải chuyển về VI");
    }

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void ToggleLabel_WhenVI_ReturnsEN()
    {
        // Khi đang ở VI, label nút toggle phải hiện "EN" (để user biết click sẽ chuyển sang EN)
        SetVI();
        LanguageManager.Instance.ToggleLabel.Should().Be("EN",
            "Khi đang VI, ToggleLabel phải là 'EN'");
    }

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void ToggleLabel_WhenEN_ReturnsVI()
    {
        // Khi đang ở EN, label nút toggle phải hiện "VI"
        SetEN();
        string label = LanguageManager.Instance.ToggleLabel;
        SetVI();

        label.Should().Be("VI",
            "Khi đang EN, ToggleLabel phải là 'VI'");
    }

    // ── Test: Smoke test — critical keys coverage ─────────────────────────────

    [Fact]
    [Trait("Category", "LanguageManager")]
    public void Smoke_CriticalKeys_RegisteredInBothLanguages()
    {
        // Danh sách key quan trọng nhất từ mỗi module
        string[] criticalKeys = {
            "error", "info", "warning",        // Common
            "yes", "no", "save", "cancel",     // Buttons
            "login_back", "login_submit",      // Auth
            "role_admin", "role_director",     // Roles
            "dashboard_header",                // Dashboard
        };

        // Kiểm tra trong VI
        SetVI();
        foreach (var key in criticalKeys)
        {
            string value = LanguageManager.Instance.Get(key);
            value.Should().NotBe($"[{key}]",
                $"Key '{key}' phải được registered trong VI dictionary");
        }

        // Kiểm tra trong EN
        SetEN();
        foreach (var key in criticalKeys)
        {
            string value = LanguageManager.Instance.Get(key);
            value.Should().NotBe($"[{key}]",
                $"Key '{key}' phải được registered trong EN dictionary");
        }

        // Teardown
        SetVI();
    }
}

/// <summary>Sequential collection cho LanguageManager tests (singleton state).</summary>
[CollectionDefinition("LanguageManagerTests")]
public class LanguageManagerTestCollection : ICollectionFixture<object> { }
