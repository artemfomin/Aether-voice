using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Stt.Providers;

/// <summary>
/// STT provider using OpenAI Whisper API (/v1/audio/transcriptions).
/// </summary>
public sealed class OpenAiSttProvider : ISttProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiSttProvider(HttpClient httpClient, string baseUrl, string apiKey, string model = "whisper-1")
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _model = model;
    }

    public string Name => "OpenAI Whisper";
    public SttProviderType ProviderType => SttProviderType.OpenAI;
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public async Task<SttResult> TranscribeAsync(byte[] audio, string language, CancellationToken ct = default)
    {
        var wavBytes = WavHelper.CreateWavBytes(audio);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", "audio.wav");
        content.Add(new StringContent(_model), "model");
        content.Add(new StringContent(language), "language");
        content.Add(new StringContent("json"), "response_format");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/audio/transcriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = content;

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            throw new SttException(SttErrorKind.Timeout, "OpenAI request timed out");
        }
        catch (HttpRequestException ex)
        {
            throw new SttException(SttErrorKind.Unknown, $"OpenAI request failed: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var kind = (int)response.StatusCode switch
            {
                401 => SttErrorKind.AuthError,
                413 => SttErrorKind.FormatError,
                429 => SttErrorKind.Timeout,
                _ => SttErrorKind.Unknown
            };
            throw new SttException(kind, $"OpenAI API error: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.GetProperty("text").GetString() ?? "";

        return new SttResult(Text: text, Language: language, DurationMs: 0, Confidence: null);
    }
}
