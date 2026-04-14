using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VoiceInput.Core.Config;
using VoiceInput.Core.Llm;
using Xunit;

namespace VoiceInput.Tests.Llm;

public class LlmProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ReturnsPolishedText()
    {
        var client = CreateMockClient(HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"Hello, world!"}}]}""");
        var processor = new OpenAiCompatLlmProcessor(client, "http://localhost", "", "gpt-4");

        var result = await processor.ProcessAsync("hello world", LlmPostProcessingMode.GrammarFix);

        result.Should().Be("Hello, world!");
    }

    [Fact]
    public async Task ProcessAsync_OnFailure_ReturnsRawText()
    {
        var client = CreateMockClient(HttpStatusCode.InternalServerError, "");
        var processor = new OpenAiCompatLlmProcessor(client, "http://localhost", "", "gpt-4");

        var result = await processor.ProcessAsync("raw text", LlmPostProcessingMode.GrammarFix);

        result.Should().Be("raw text");
    }

    [Fact]
    public async Task ProcessAsync_EmptyInput_ReturnsEmpty()
    {
        var client = CreateMockClient(HttpStatusCode.OK, """{"choices":[]}""");
        var processor = new OpenAiCompatLlmProcessor(client, "http://localhost", "", "gpt-4");

        var result = await processor.ProcessAsync("", LlmPostProcessingMode.GrammarFix);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_NetworkError_ReturnsRawText()
    {
        var handler = new FailingHandler();
        var client = new HttpClient(handler);
        var processor = new OpenAiCompatLlmProcessor(client, "http://localhost", "", "gpt-4");

        var result = await processor.ProcessAsync("some text", LlmPostProcessingMode.RemoveFillers);

        result.Should().Be("some text");
    }

    [Fact]
    public void ILlmProcessor_InterfaceContract()
    {
        typeof(ILlmProcessor).GetMethod("ProcessAsync").Should().NotBeNull();
    }

    private static HttpClient CreateMockClient(HttpStatusCode code, string body)
    {
        return new HttpClient(new MockHandler(code, body));
    }

    private sealed class MockHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _body;

        public MockHandler(HttpStatusCode code, string body) { _code = code; _body = body; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(_code) { Content = new StringContent(_body) });
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => throw new HttpRequestException("Network error");
    }
}
