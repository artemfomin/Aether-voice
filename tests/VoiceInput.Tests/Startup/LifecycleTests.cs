using FluentAssertions;
using VoiceInput.Core;
using Xunit;

namespace VoiceInput.Tests.Startup;

/// <summary>
/// Tests for the <see cref="IApplicationLifecycle"/> contract.
/// Uses a minimal in-test implementation to verify the expected behaviour
/// without requiring a full application host.
/// </summary>
public sealed class LifecycleTests
{
    // ── Contract tests ────────────────────────────────────────────────────────

    [Fact]
    public void IApplicationLifecycle_HasStartAsync_Method()
    {
        var type = typeof(IApplicationLifecycle);

        var method = type.GetMethod(nameof(IApplicationLifecycle.StartAsync));

        method.Should().NotBeNull(
            "IApplicationLifecycle must declare a StartAsync method");
        method!.ReturnType.Should().Be(typeof(Task),
            "StartAsync must return Task");
    }

    [Fact]
    public void IApplicationLifecycle_HasStopAsync_Method()
    {
        var type = typeof(IApplicationLifecycle);

        var method = type.GetMethod(nameof(IApplicationLifecycle.StopAsync));

        method.Should().NotBeNull(
            "IApplicationLifecycle must declare a StopAsync method");
        method!.ReturnType.Should().Be(typeof(Task),
            "StopAsync must return Task");
    }

    [Fact]
    public void IApplicationLifecycle_HasIsRunning_Property()
    {
        var type = typeof(IApplicationLifecycle);

        var property = type.GetProperty(nameof(IApplicationLifecycle.IsRunning));

        property.Should().NotBeNull(
            "IApplicationLifecycle must declare an IsRunning property");
        property!.PropertyType.Should().Be(typeof(bool),
            "IsRunning must be of type bool");
        property.CanRead.Should().BeTrue(
            "IsRunning must be readable");
    }

    // ── Behaviour tests via stub implementation ───────────────────────────────

    [Fact]
    public async Task StartAsync_SetsIsRunning_ToTrue()
    {
        // Arrange
        IApplicationLifecycle lifecycle = new StubLifecycle();

        // Act
        await lifecycle.StartAsync();

        // Assert
        lifecycle.IsRunning.Should().BeTrue(
            "IsRunning must be true after StartAsync completes");
    }

    [Fact]
    public async Task StopAsync_AfterStart_SetsIsRunning_ToFalse()
    {
        // Arrange
        IApplicationLifecycle lifecycle = new StubLifecycle();
        await lifecycle.StartAsync();
        lifecycle.IsRunning.Should().BeTrue("pre-condition: must be running before stop");

        // Act
        await lifecycle.StopAsync();

        // Assert
        lifecycle.IsRunning.Should().BeFalse(
            "IsRunning must be false after StopAsync completes");
    }

    [Fact]
    public void IsRunning_BeforeStart_IsFalse()
    {
        // Arrange
        IApplicationLifecycle lifecycle = new StubLifecycle();

        // Assert
        lifecycle.IsRunning.Should().BeFalse(
            "IsRunning must be false before StartAsync is called");
    }

    // ── Minimal stub ─────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal in-test implementation of <see cref="IApplicationLifecycle"/>
    /// used to verify the interface contract without external dependencies.
    /// </summary>
    private sealed class StubLifecycle : IApplicationLifecycle
    {
        public bool IsRunning { get; private set; }

        public Task StartAsync(CancellationToken ct = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            IsRunning = false;
            return Task.CompletedTask;
        }
    }
}
