using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceInput.Core.Stt;
using VoiceInput.Core.Stt.Providers;
using Xunit;

namespace VoiceInput.Tests.Stt.Providers;

public class SttProviderTests
{
    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new MockHttpHandler(statusCode, responseBody);
        return new HttpClient(handler);
    }

    // --- OpenAI ---

    [Fact]
    public async Task OpenAi_TranscribeAsync_ParsesResponse()
    {
        var client = CreateMockHttpClient(HttpStatusCode.OK, """{"text":"Hello world"}""");
        var provider = new OpenAiSttProvider(client, "https://api.openai.com", "test-key");

        var result = await provider.TranscribeAsync([1, 2, 3, 4], "en");

        result.Text.Should().Be("Hello world");
        result.Language.Should().Be("en");
    }

    [Fact]
    public async Task OpenAi_401_ThrowsAuthError()
    {
        var client = CreateMockHttpClient(HttpStatusCode.Unauthorized, "");
        var provider = new OpenAiSttProvider(client, "https://api.openai.com", "bad-key");

        var act = () => provider.TranscribeAsync([1, 2], "en");

        (await act.Should().ThrowAsync<SttException>())
            .Which.Kind.Should().Be(SttErrorKind.AuthError);
    }

    [Fact]
    public void OpenAi_Properties()
    {
        var provider = new OpenAiSttProvider(new HttpClient(), "https://api.openai.com", "key");
        provider.Name.Should().Be("OpenAI Whisper");
        provider.ProviderType.Should().Be(VoiceInput.Core.Config.SttProviderType.OpenAI);
        provider.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void OpenAi_NoApiKey_NotAvailable()
    {
        var provider = new OpenAiSttProvider(new HttpClient(), "https://api.openai.com", "");
        provider.IsAvailable.Should().BeFalse();
    }

    // --- Google ---

    [Fact]
    public async Task Google_TranscribeAsync_ParsesResponse()
    {
        var responseJson = """{"results":[{"alternatives":[{"transcript":"Привет мир"}]}]}""";
        var client = CreateMockHttpClient(HttpStatusCode.OK, responseJson);
        var provider = new GoogleSttProvider(client, "test-key");

        var result = await provider.TranscribeAsync([1, 2, 3, 4], "ru");

        result.Text.Should().Be("Привет мир");
    }

    [Fact]
    public async Task Google_403_ThrowsAuthError()
    {
        var client = CreateMockHttpClient(HttpStatusCode.Forbidden, "");
        var provider = new GoogleSttProvider(client, "bad-key");

        var act = () => provider.TranscribeAsync([1, 2], "en");

        (await act.Should().ThrowAsync<SttException>())
            .Which.Kind.Should().Be(SttErrorKind.AuthError);
    }

    [Fact]
    public async Task Google_EmptyResults_ReturnsEmptyText()
    {
        var client = CreateMockHttpClient(HttpStatusCode.OK, """{"results":[]}""");
        var provider = new GoogleSttProvider(client, "key");

        var result = await provider.TranscribeAsync([1, 2], "en");
        result.Text.Should().BeEmpty();
    }

    // --- Ollama Stub ---

    [Fact]
    public void Ollama_IsNotAvailable()
    {
        var provider = new OllamaSttProvider();
        provider.IsAvailable.Should().BeFalse();
        provider.Name.Should().Be("Ollama");
    }

    [Fact]
    public async Task Ollama_Transcribe_ThrowsUnavailable()
    {
        var provider = new OllamaSttProvider();

        var act = () => provider.TranscribeAsync([1, 2], "en");

        var ex = (await act.Should().ThrowAsync<SttException>()).Which;
        ex.Kind.Should().Be(SttErrorKind.Unavailable);
        ex.Message.Should().Contain("github.com/ollama/ollama/pull/15243");
    }

    // --- LM Studio Stub ---

    [Fact]
    public void LmStudio_IsNotAvailable()
    {
        var provider = new LmStudioSttProvider();
        provider.IsAvailable.Should().BeFalse();
        provider.Name.Should().Be("LM Studio");
    }

    [Fact]
    public async Task LmStudio_Transcribe_ThrowsUnavailable()
    {
        var provider = new LmStudioSttProvider();

        var act = () => provider.TranscribeAsync([1, 2], "en");

        var ex = (await act.Should().ThrowAsync<SttException>()).Which;
        ex.Kind.Should().Be(SttErrorKind.Unavailable);
        ex.Message.Should().Contain("lmstudio.ai/transcribe");
    }

    // --- WavHelper ---

    [Fact]
    public void WavHelper_CreatesValidWavHeader()
    {
        var pcm = new byte[] { 1, 2, 3, 4 };
        var wav = WavHelper.CreateWavBytes(pcm);

        wav.Length.Should().Be(44 + 4); // 44 byte header + 4 byte data
        // Check RIFF magic
        wav[0].Should().Be((byte)'R');
        wav[1].Should().Be((byte)'I');
        wav[2].Should().Be((byte)'F');
        wav[3].Should().Be((byte)'F');
    }

    // --- Mock Handler ---

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public MockHttpHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            });
        }
    }
}
