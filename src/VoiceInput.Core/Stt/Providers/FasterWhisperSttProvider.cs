using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Stt.Providers;

/// <summary>
/// STT provider for local faster-whisper via OpenAI-compatible API (speaches server).
/// </summary>
public sealed class FasterWhisperSttProvider : ISttProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public FasterWhisperSttProvider(HttpClient httpClient, string baseUrl = "http://localhost:8000", string model = "Systran/faster-distil-whisper-large-v3")
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    public string Name => "faster-whisper (local)";
    public SttProviderType ProviderType => SttProviderType.FasterWhisper;
    public bool IsAvailable => CheckAvailability();

    public async Task<SttResult> TranscribeAsync(byte[] audio, string language, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            throw new SttException(SttErrorKind.Unavailable, $"faster-whisper server not reachable at {_baseUrl}");
        }

        var wavBytes = WavHelper.CreateWavBytes(audio);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", "audio.wav");
        content.Add(new StringContent(_model), "model");
        content.Add(new StringContent(language), "language");
        content.Add(new StringContent("json"), "response_format");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/audio/transcriptions") { Content = content },
                ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            throw new SttException(SttErrorKind.Timeout, "faster-whisper request timed out");
        }
        catch (HttpRequestException ex)
        {
            throw new SttException(SttErrorKind.Unknown, $"faster-whisper request failed: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new SttException(SttErrorKind.Unknown, $"faster-whisper API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("text").GetString() ?? "";

        return new SttResult(Text: text, Language: language, DurationMs: 0, Confidence: null);
    }

    private bool CheckAvailability()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            // Use Send with synchronous completionOption to avoid VSTHRD002
            using var request = new HttpRequestMessage(HttpMethod.Head, $"{_baseUrl}/health");
            using var response = _httpClient.Send(request, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
