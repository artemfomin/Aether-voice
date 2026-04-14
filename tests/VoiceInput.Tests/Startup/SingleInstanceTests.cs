using FluentAssertions;
using VoiceInput.App.Startup;
using Xunit;

namespace VoiceInput.Tests.Startup;

/// <summary>
/// Tests for <see cref="SingleInstanceGuard"/> — verifies mutex acquisition,
/// second-instance detection, and proper release on disposal.
/// </summary>
public sealed class SingleInstanceTests
{
    /// <summary>
    /// Generates a unique mutex name per test to prevent cross-test interference.
    /// </summary>
    private static string UniqueMutexName() =>
        $@"Local\VoiceInputTest_{Guid.NewGuid():N}";

    [Fact]
    public void FirstGuard_IsFirstInstance_ReturnsTrue()
    {
        // Arrange & Act
        using var guard = new SingleInstanceGuard(UniqueMutexName());

        // Assert
        guard.IsFirstInstance.Should().BeTrue(
            "the first process to acquire the mutex must be recognised as the first instance");
    }

    [Fact]
    public void SecondGuard_WithSameMutexName_IsFirstInstanceReturnsFalse()
    {
        // Arrange
        var mutexName = UniqueMutexName();

        using var firstGuard = new SingleInstanceGuard(mutexName);
        firstGuard.IsFirstInstance.Should().BeTrue("pre-condition: first guard must own the mutex");

        // Act
        using var secondGuard = new SingleInstanceGuard(mutexName);

        // Assert
        secondGuard.IsFirstInstance.Should().BeFalse(
            "a second guard with the same mutex name must detect the existing instance");
    }

    [Fact]
    public void NewGuard_AfterFirstGuardDisposed_AcquiresMutex()
    {
        // Arrange
        var mutexName = UniqueMutexName();

        var firstGuard = new SingleInstanceGuard(mutexName);
        firstGuard.IsFirstInstance.Should().BeTrue("pre-condition: first guard must own the mutex");

        // Act — release the mutex
        firstGuard.Dispose();

        // Assert — a new guard can now acquire it
        using var newGuard = new SingleInstanceGuard(mutexName);
        newGuard.IsFirstInstance.Should().BeTrue(
            "after the first guard is disposed the mutex should be available for a new guard");
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var guard = new SingleInstanceGuard(UniqueMutexName());

        // Act & Assert — double-dispose must be safe
        var act = () =>
        {
            guard.Dispose();
            guard.Dispose();
        };

        act.Should().NotThrow("IDisposable implementations must be idempotent");
    }
}
