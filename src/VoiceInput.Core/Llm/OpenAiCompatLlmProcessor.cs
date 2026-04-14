using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoiceInput.Core.Config;

namespace VoiceInput.Core.Llm;

/// <summary>
/// LLM post-processor using OpenAI-compatible chat completions API.
/// Works with OpenAI, Ollama, LM Studio — any /v1/chat/completions endpoint.
/// On failure, returns the original raw text (graceful degradation).
/// </summary>
public sealed class OpenAiCompatLlmProcessor : ILlmProcessor
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;

    private static readonly string GrammarFixPrompt =
        "Fix grammar, punctuation, and capitalization. Keep the original meaning. Return only the corrected text, nothing else.";

    private static readonly string RemoveFillersPrompt =
        "Remove filler words (uh, um, like, you know, ну, типа, это, вот, как бы). Fix punctuation. Return only the cleaned text.";

    public OpenAiCompatLlmProcessor(HttpClient httpClient, string baseUrl, string apiKey, string model)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<string> ProcessAsync(string rawText, LlmPostProcessingMode mode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return rawText;

        try
        {
            string systemPrompt = mode == LlmPostProcessingMode.GrammarFix ? GrammarFixPrompt : RemoveFillersPrompt;

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = rawText }
                },
                temperature = 0.1,
                max_tokens = 2000
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return rawText;

            var responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(responseJson);

            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var content = choices[0].GetProperty("message").GetProperty("content").GetString();
                return string.IsNullOrWhiteSpace(content) ? rawText : content.Trim();
            }

            return rawText;
        }
        catch
        {
            // Graceful degradation: return raw text on any failure
            return rawText;
        }
    }
}
