using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Stt.Providers;

/// <summary>
/// STT provider using Google Cloud Speech-to-Text V1 REST API.
/// </summary>
public sealed class GoogleSttProvider : ISttProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GoogleSttProvider(HttpClient httpClient, string apiKey)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public string Name => "Google Cloud STT";
    public SttProviderType ProviderType => SttProviderType.Google;
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public async Task<SttResult> TranscribeAsync(byte[] audio, string language, CancellationToken ct = default)
    {
        string languageCode = language switch
        {
            "ru" => "ru-RU",
            "en" => "en-US",
            _ => language
        };

        var requestBody = new
        {
            config = new
            {
                encoding = "LINEAR16",
                sampleRateHertz = 16000,
                languageCode,
            },
            audio = new
            {
                content = Convert.ToBase64String(audio)
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(
                $"https://speech.googleapis.com/v1/speech:recognize?key={_apiKey}",
                httpContent,
                ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            throw new SttException(SttErrorKind.Timeout, "Google STT request timed out");
        }
        catch (HttpRequestException ex)
        {
            throw new SttException(SttErrorKind.Unknown, $"Google STT request failed: {ex.Message}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var kind = (int)response.StatusCode switch
            {
                403 => SttErrorKind.AuthError,
                400 => SttErrorKind.FormatError,
                _ => SttErrorKind.Unknown
            };
            throw new SttException(kind, $"Google STT API error: {response.StatusCode}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(responseJson);

        var text = "";
        if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
        {
            var alternatives = results[0].GetProperty("alternatives");
            if (alternatives.GetArrayLength() > 0)
            {
                text = alternatives[0].GetProperty("transcript").GetString() ?? "";
            }
        }

        return new SttResult(Text: text, Language: language, DurationMs: 0, Confidence: null);
    }
}
