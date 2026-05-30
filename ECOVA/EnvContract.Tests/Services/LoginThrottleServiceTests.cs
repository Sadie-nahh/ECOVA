using EnvContract.GUI.Services;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Xunit;

namespace EnvContract.Tests.Services;

/// <summary>
/// Unit tests cho LoginThrottleService.
///
/// LoginThrottleService là static class với static Dictionary — tests có thể
/// interfere nhau nếu chạy parallel. Dùng [Collection] để chạy sequential.
/// IDisposable reset _attempts sau mỗi test.
///
/// LoginThrottleService đọc MaxLoginAttempts từ AppConfig (appsettings.json).
/// Trong test environment không có appsettings.json → dùng default = 5.
/// </summary>
[Collection("LoginThrottleTests")] // Sequential collection để tránh race condition
public class LoginThrottleServiceTests : IDisposable
{
    // Truy cập static _attempts dictionary qua reflection để reset giữa các tests
    private static readonly FieldInfo AttemptsField =
        typeof(LoginThrottleService).GetField(
            "_attempts",
            BindingFlags.Static | BindingFlags.NonPublic)!;

    public LoginThrottleServiceTests()
    {
        // Clear trước mỗi test để đảm bảo clean state
        ClearAttempts();
    }

    public void Dispose()
    {
        // Clear sau mỗi test
        ClearAttempts();
    }

    private static void ClearAttempts()
    {
        var dict = AttemptsField.GetValue(null)
            as Dictionary<string, (int Count, DateTime? LockedUntil)>;
        dict?.Clear();
    }

    // ── Test: IsLockedOut ────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void IsLockedOut_NoAttempts_ReturnsFalse()
    {
        // Arrange: username chưa có attempt nào
        string username = "fresh_user";

        // Act
        bool locked = LoginThrottleService.IsLockedOut(username, out TimeSpan remaining);

        // Assert
        locked.Should().BeFalse("User mới chưa attempt không bị lockout");
        remaining.Should().Be(TimeSpan.Zero, "Remaining time phải là 0 khi không bị khóa");
    }

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void IsLockedOut_AfterMaxAttempts_ReturnsTrue()
    {
        // Arrange: record đủ failures để trigger lockout
        // Default MaxLoginAttempts = 5 (từ appsettings.json hoặc fallback)
        string username = "brute_force_user";
        for (int i = 0; i < 5; i++)
            LoginThrottleService.RecordFailure(username);

        // Act
        bool locked = LoginThrottleService.IsLockedOut(username, out TimeSpan remaining);

        // Assert
        locked.Should().BeTrue("Sau 5 lần sai phải bị lockout");
        remaining.Should().BeGreaterThan(TimeSpan.Zero,
            "Remaining time phải > 0 khi đang bị lockout");
    }

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void IsLockedOut_Below_MaxAttempts_ReturnsFalse()
    {
        // Arrange: 4 lần sai (chưa đủ max = 5)
        string username = "almost_locked_user";
        for (int i = 0; i < 4; i++)
            LoginThrottleService.RecordFailure(username);

        // Act
        bool locked = LoginThrottleService.IsLockedOut(username, out _);

        // Assert
        locked.Should().BeFalse("4 lần sai (< max 5) chưa bị lockout");
    }

    // ── Test: RecordSuccess ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void RecordSuccess_ClearsAttemptHistory()
    {
        // Arrange: record một số failures
        string username = "recover_user";
        LoginThrottleService.RecordFailure(username);
        LoginThrottleService.RecordFailure(username);

        // Act: đăng nhập thành công → reset
        LoginThrottleService.RecordSuccess(username);

        // Assert: không còn bị lockout
        bool locked = LoginThrottleService.IsLockedOut(username, out _);
        locked.Should().BeFalse("Sau RecordSuccess phải xóa lịch sử attempts");
    }

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void RecordSuccess_AfterLockout_ClearsLockout()
    {
        // Arrange: trigger lockout đầy đủ
        string username = "locked_then_success";
        for (int i = 0; i < 5; i++)
            LoginThrottleService.RecordFailure(username);

        // Confirm đang bị lock
        LoginThrottleService.IsLockedOut(username, out _).Should().BeTrue();

        // Act: đăng nhập thành công (ví dụ admin reset password)
        LoginThrottleService.RecordSuccess(username);

        // Assert
        bool stillLocked = LoginThrottleService.IsLockedOut(username, out _);
        stillLocked.Should().BeFalse("RecordSuccess phải giải phóng lockout");
    }

    // ── Test: RemainingAttempts ───────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void RemainingAttempts_NoFailures_ReturnsMaxAttempts()
    {
        // Arrange
        string username = "fresh_attempts_user";

        // Act
        int remaining = LoginThrottleService.RemainingAttempts(username);

        // Assert: MaxLoginAttempts default = 5
        remaining.Should().BeGreaterThan(0,
            "User mới phải có remaining attempts > 0");
        remaining.Should().BeLessOrEqualTo(10,
            "MaxLoginAttempts không nên quá 10 (sanity check)");
    }

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void RemainingAttempts_DecreasesWithEachFailure()
    {
        // Arrange
        string username = "decrementing_user";
        int initial = LoginThrottleService.RemainingAttempts(username);

        // Act: record 2 failures
        LoginThrottleService.RecordFailure(username);
        LoginThrottleService.RecordFailure(username);

        int after2Failures = LoginThrottleService.RemainingAttempts(username);

        // Assert
        after2Failures.Should().Be(initial - 2,
            "Mỗi lần RecordFailure phải giảm RemainingAttempts đi 1");
    }

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void RemainingAttempts_AfterLockout_ReturnsZero()
    {
        // Arrange: max out failures
        string username = "zero_remaining_user";
        for (int i = 0; i < 5; i++)
            LoginThrottleService.RecordFailure(username);

        // Act
        int remaining = LoginThrottleService.RemainingAttempts(username);

        // Assert
        remaining.Should().Be(0,
            "Sau khi lockout, RemainingAttempts phải = 0");
    }

    // ── Test: Thread Safety ───────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "LoginThrottle")]
    public void RecordFailure_ConcurrentCalls_ShouldNotThrow()
    {
        // Arrange: test thread safety với lock {}
        string username = "concurrent_user";
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act: 20 threads đồng thời record failure
        var threads = new Thread[20];
        for (int i = 0; i < 20; i++)
        {
            threads[i] = new Thread(() =>
            {
                try { LoginThrottleService.RecordFailure(username); }
                catch (Exception ex) { exceptions.Add(ex); }
            });
        }
        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // Assert: không có exception nào
        exceptions.Should().BeEmpty(
            "RecordFailure phải thread-safe, không throw exception khi concurrent");
    }
}

/// <summary>
/// xUnit collection để đảm bảo LoginThrottleServiceTests chạy sequential.
/// </summary>
[CollectionDefinition("LoginThrottleTests")]
public class LoginThrottleTestCollection : ICollectionFixture<object> { }
