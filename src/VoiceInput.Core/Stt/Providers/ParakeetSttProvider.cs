using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Stt.Providers;

/// <summary>
/// STT provider for NVIDIA NeMo Parakeet ASR API.
/// Simple multipart POST /transcribe with a "file" field → JSON { "text": "..." }.
/// </summary>
public sealed class ParakeetSttProvider : ISttProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ParakeetSttProvider(HttpClient httpClient, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public string Name => "Parakeet (NeMo)";
    public SttProviderType ProviderType => SttProviderType.Parakeet;
    public bool IsAvailable => CheckAvailability();

    public async Task<SttResult> TranscribeAsync(byte[] audio, string language, CancellationToken ct = default)
    {
        var wavBytes = WavHelper.CreateWavBytes(audio);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", "audio.wav");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/transcribe") { Content = content },
                ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            throw new SttException(SttErrorKind.Timeout, "Parakeet request timed out");
        }
        catch (HttpRequestException ex)
        {
            throw new SttException(SttErrorKind.Unknown, $"Parakeet request to {_baseUrl}/transcribe failed: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new SttException(SttErrorKind.Unknown, $"Parakeet API error {response.StatusCode}: {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        // Response format: { "text": "transcribed text" } or { "transcription": "..." }
        var text = "";
        if (doc.RootElement.TryGetProperty("text", out var textProp))
        {
            text = textProp.GetString() ?? "";
        }
        else if (doc.RootElement.TryGetProperty("transcription", out var transProp))
        {
            text = transProp.GetString() ?? "";
        }

        return new SttResult(Text: text, Language: language, DurationMs: 0, Confidence: null);
    }

    private bool CheckAvailability()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/health");
            using var response = _httpClient.Send(request, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
