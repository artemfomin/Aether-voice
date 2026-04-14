using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VoiceInput.Core;
using VoiceInput.Core.Config;
using Xunit;

namespace VoiceInput.Tests.Startup;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions.AddVoiceInputCore"/> —
/// verifies that the DI container correctly registers and resolves Core services.
/// </summary>
public sealed class DependencyInjectionTests
{
    private static ServiceProvider BuildCoreProvider()
    {
        var services = new ServiceCollection();
        services.AddVoiceInputCore();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddVoiceInputCore_ResolvesIConfigStore_WithoutException()
    {
        // Arrange
        using var provider = BuildCoreProvider();

        // Act
        var configStore = provider.GetService<IConfigStore>();

        // Assert
        configStore.Should().NotBeNull(
            "AddVoiceInputCore must register IConfigStore");
    }

    [Fact]
    public void AddVoiceInputCore_IConfigStore_IsSingleton_SameInstanceResolvedTwice()
    {
        // Arrange
        using var provider = BuildCoreProvider();

        // Act
        var first = provider.GetRequiredService<IConfigStore>();
        var second = provider.GetRequiredService<IConfigStore>();

        // Assert
        first.Should().BeSameAs(second,
            "IConfigStore must be registered as a singleton — same instance on every resolution");
    }

    [Fact]
    public void AddVoiceInputCore_ReturnsServiceCollection_ForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var returned = services.AddVoiceInputCore();

        // Assert
        returned.Should().BeSameAs(services,
            "extension method must return the same IServiceCollection to support fluent chaining");
    }
}
