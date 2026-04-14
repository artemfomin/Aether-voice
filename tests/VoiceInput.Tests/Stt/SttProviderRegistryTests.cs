using FluentAssertions;
using VoiceInput.Core.Config;
using VoiceInput.Core.Stt;
using Xunit;

namespace VoiceInput.Tests.Stt;

public sealed class SttProviderRegistryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ISttProvider MakeProvider(
        SttProviderType type,
        string name = "Mock",
        bool isAvailable = true)
        => new MockSttProvider(type, name, isAvailable);

    // ── Register + Resolve ────────────────────────────────────────────────────

    [Fact]
    public void Register_ThenResolve_ReturnsSameProvider()
    {
        var registry = new SttProviderRegistry();
        var provider = MakeProvider(SttProviderType.OpenAI, "OpenAI Mock");

        registry.Register(provider);
        var resolved = registry.Resolve(SttProviderType.OpenAI);

        resolved.Should().BeSameAs(provider);
    }

    [Fact]
    public void Register_SameTypeTwice_ReplacesExistingProvider()
    {
        var registry = new SttProviderRegistry();
        var first = MakeProvider(SttProviderType.Google, "First");
        var second = MakeProvider(SttProviderType.Google, "Second");

        registry.Register(first);
        registry.Register(second);

        registry.Resolve(SttProviderType.Google).Should().BeSameAs(second);
    }

    [Fact]
    public void Register_NullProvider_ThrowsArgumentNullException()
    {
        var registry = new SttProviderRegistry();

        var act = () => registry.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Resolve unregistered ──────────────────────────────────────────────────

    [Fact]
    public void Resolve_UnregisteredType_ThrowsSttExceptionWithUnavailableKind()
    {
        var registry = new SttProviderRegistry();

        var act = () => registry.Resolve(SttProviderType.FasterWhisper);

        act.Should()
            .Throw<SttException>()
            .Which.Kind.Should().Be(SttErrorKind.Unavailable);
    }

    [Fact]
    public void Resolve_UnregisteredType_ExceptionMessageContainsTypeName()
    {
        var registry = new SttProviderRegistry();

        var act = () => registry.Resolve(SttProviderType.Ollama);

        act.Should()
            .Throw<SttException>()
            .WithMessage("*Ollama*");
    }

    // ── TryResolve ────────────────────────────────────────────────────────────

    [Fact]
    public void TryResolve_RegisteredType_ReturnsTrueAndProvider()
    {
        var registry = new SttProviderRegistry();
        var provider = MakeProvider(SttProviderType.LMStudio);
        registry.Register(provider);

        var found = registry.TryResolve(SttProviderType.LMStudio, out var resolved);

        found.Should().BeTrue();
        resolved.Should().BeSameAs(provider);
    }

    [Fact]
    public void TryResolve_UnregisteredType_ReturnsFalseAndNullProvider()
    {
        var registry = new SttProviderRegistry();

        var found = registry.TryResolve(SttProviderType.OpenAI, out var resolved);

        found.Should().BeFalse();
        resolved.Should().BeNull();
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public void GetAll_WhenEmpty_ReturnsEmptyList()
    {
        var registry = new SttProviderRegistry();

        registry.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void GetAll_AfterRegisteringMultiple_ReturnsAllProviders()
    {
        var registry = new SttProviderRegistry();
        var openAi = MakeProvider(SttProviderType.OpenAI);
        var google = MakeProvider(SttProviderType.Google);
        var whisper = MakeProvider(SttProviderType.FasterWhisper);

        registry.Register(openAi);
        registry.Register(google);
        registry.Register(whisper);

        var all = registry.GetAll();

        all.Should().HaveCount(3);
        all.Should().Contain(openAi);
        all.Should().Contain(google);
        all.Should().Contain(whisper);
    }

    [Fact]
    public void GetAll_ReturnsReadOnlySnapshot_NotLiveCollection()
    {
        var registry = new SttProviderRegistry();
        registry.Register(MakeProvider(SttProviderType.OpenAI));

        var snapshot = registry.GetAll();
        registry.Register(MakeProvider(SttProviderType.Google));

        // Snapshot taken before second registration must not reflect the new entry.
        snapshot.Should().HaveCount(1);
    }

    // ── SttResult record ──────────────────────────────────────────────────────

    [Fact]
    public void SttResult_Properties_AreAccessible()
    {
        var result = new SttResult("Hello world", "en", 1500, 0.95);

        result.Text.Should().Be("Hello world");
        result.Language.Should().Be("en");
        result.DurationMs.Should().Be(1500);
        result.Confidence.Should().BeApproximately(0.95, 0.001);
    }

    [Fact]
    public void SttResult_OptionalFields_CanBeNull()
    {
        var result = new SttResult("Привет", null, 800, null);

        result.Language.Should().BeNull();
        result.Confidence.Should().BeNull();
    }

    // ── SttException ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SttErrorKind.Timeout)]
    [InlineData(SttErrorKind.AuthError)]
    [InlineData(SttErrorKind.FormatError)]
    [InlineData(SttErrorKind.Unavailable)]
    [InlineData(SttErrorKind.Unknown)]
    public void SttException_Kind_IsPreserved(SttErrorKind kind)
    {
        var ex = new SttException(kind, "test message");

        ex.Kind.Should().Be(kind);
        ex.Message.Should().Be("test message");
    }

    [Fact]
    public void SttException_WithInnerException_PreservesInner()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new SttException(SttErrorKind.Unknown, "wrapper", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }

    // ── Private mock ──────────────────────────────────────────────────────────

    private sealed class MockSttProvider : ISttProvider
    {
        public MockSttProvider(SttProviderType type, string name, bool isAvailable)
        {
            ProviderType = type;
            Name = name;
            IsAvailable = isAvailable;
        }

        public string Name { get; }
        public SttProviderType ProviderType { get; }
        public bool IsAvailable { get; }

        public Task<SttResult> TranscribeAsync(
            byte[] audio,
            string language,
            CancellationToken ct = default)
            => Task.FromResult(new SttResult("mock transcription", language, 0, null));
    }
}
