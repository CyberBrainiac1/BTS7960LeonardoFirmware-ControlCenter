using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

/// <summary>
/// Minimal Ollama HTTP client for local model listing and chat calls.
/// Uses non-streaming responses to keep integration simple inside WPF.
/// </summary>
public class OllamaService
{
    private readonly HttpClient _http;
    private readonly LoggerService _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaService(LoggerService logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    // Reads local model registry from Ollama (/api/tags).
    public async Task<IReadOnlyList<string>> ListModelsAsync(string endpoint, CancellationToken ct)
    {
        try
        {
            var url = BuildUrl(endpoint, "/api/tags");
            var response = await _http.GetFromJsonAsync<OllamaTagsResponse>(url, JsonOptions, ct);
            var models = response?.Models?
                .Select(m => m.Name ?? string.Empty)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n)
                .ToList() ?? new List<string>();

            return models;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Ollama model list failed: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    // Reads models from OpenAI-compatible endpoint (/v1/models) using bearer API key.
    public async Task<IReadOnlyList<string>> ListOpenAiModelsAsync(string endpoint, string apiKey, CancellationToken ct)
    {
        try
        {
            var url = BuildOpenAiUrl(endpoint, "/models");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.Warn($"OpenAI-compatible model list failed: HTTP {(int)response.StatusCode}: {body}");
                return Array.Empty<string>();
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            var models = new List<string>();
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idElement))
                {
                    var id = idElement.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        models.Add(id);
                    }
                }
            }

            return models.OrderBy(n => n).ToList();
        }
        catch (Exception ex)
        {
            _logger.Warn($"OpenAI-compatible model list failed: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    // Sends chat history + current user prompt, optionally with screenshot image bytes.
    public async Task<(bool Success, string Content, string Error)> AskAsync(
        string endpoint,
        string model,
        IReadOnlyList<OllamaChatEntry> history,
        string userPrompt,
        byte[]? screenshotPng,
        CancellationToken ct)
    {
        try
        {
            var messages = new List<OllamaChatRequestMessage>
            {
                new()
                {
                    Role = "system",
                    Content = "You are a concise assistant inside a DIY wheel control center app. Be direct and practical."
                }
            };

            foreach (var item in history.TakeLast(20))
            {
                if (string.IsNullOrWhiteSpace(item.Content))
                {
                    continue;
                }

                var role = item.IsUser ? "user" : "assistant";
                messages.Add(new OllamaChatRequestMessage
                {
                    Role = role,
                    Content = item.Content
                });
            }

            var askMessage = new OllamaChatRequestMessage
            {
                Role = "user",
                Content = userPrompt
            };
            if (screenshotPng != null && screenshotPng.Length > 0)
            {
                askMessage.Images = new[] { Convert.ToBase64String(screenshotPng) };
            }
            messages.Add(askMessage);

            var request = new OllamaChatRequest
            {
                Model = model,
                Stream = false,
                Messages = messages
            };

            var url = BuildUrl(endpoint, "/api/chat");
            using var response = await _http.PostAsJsonAsync(url, request, JsonOptions, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return (false, string.Empty, $"HTTP {(int)response.StatusCode}: {body}");
            }

            var parsed = JsonSerializer.Deserialize<OllamaChatResponse>(body, JsonOptions);
            var content = parsed?.Message?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, string.Empty, "Ollama returned an empty response.");
            }

            return (true, content, string.Empty);
        }
        catch (TaskCanceledException)
        {
            return (false, string.Empty, "Ollama request timed out.");
        }
        catch (Exception ex)
        {
            return (false, string.Empty, ex.Message);
        }
    }

    // Sends chat request to OpenAI-compatible endpoint (/v1/chat/completions).
    public async Task<(bool Success, string Content, string Error)> AskOpenAiCompatAsync(
        string endpoint,
        string apiKey,
        string model,
        IReadOnlyList<OllamaChatEntry> history,
        string userPrompt,
        string? userName,
        string? userEmail,
        CancellationToken ct)
    {
        try
        {
            var messages = new List<object>
            {
                new
                {
                    role = "system",
                    content = "You are a concise assistant inside a DIY wheel control center app. Be direct and practical."
                }
            };

            if (!string.IsNullOrWhiteSpace(userName) || !string.IsNullOrWhiteSpace(userEmail))
            {
                messages.Add(new
                {
                    role = "system",
                    content = $"User profile: name={userName ?? "unknown"}, email={userEmail ?? "unknown"}."
                });
            }

            foreach (var item in history.TakeLast(20))
            {
                if (string.IsNullOrWhiteSpace(item.Content))
                {
                    continue;
                }

                messages.Add(new
                {
                    role = item.IsUser ? "user" : "assistant",
                    content = item.Content
                });
            }

            messages.Add(new { role = "user", content = userPrompt });

            var payload = new
            {
                model,
                temperature = 0.2,
                messages
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildOpenAiUrl(endpoint, "/chat/completions"))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return (false, string.Empty, $"HTTP {(int)response.StatusCode}: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return (false, string.Empty, "AI endpoint returned no choices.");
            }

            var first = choices[0];
            if (!first.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var contentElement))
            {
                return (false, string.Empty, "AI endpoint returned invalid response.");
            }

            string? content = contentElement.ValueKind switch
            {
                JsonValueKind.String => contentElement.GetString(),
                JsonValueKind.Array => string.Join(
                    "\n",
                    contentElement.EnumerateArray()
                        .Select(x => x.TryGetProperty("text", out var text) ? text.GetString() : string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x))),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(content))
            {
                return (false, string.Empty, "AI endpoint returned empty content.");
            }

            return (true, content.Trim(), string.Empty);
        }
        catch (TaskCanceledException)
        {
            return (false, string.Empty, "AI request timed out.");
        }
        catch (Exception ex)
        {
            return (false, string.Empty, ex.Message);
        }
    }

    public async Task<(bool Success, string Details, string Error)> TestOpenAiApiKeyAsync(
        string endpoint,
        string apiKey,
        CancellationToken ct)
    {
        var models = await ListOpenAiModelsAsync(endpoint, apiKey, ct);
        if (models.Count == 0)
        {
            return (false, string.Empty, "No models returned. Check endpoint/key.");
        }

        return (true, $"{models.Count} models available", string.Empty);
    }

    // Normalizes endpoints like localhost:11434 -> http://localhost:11434.
    private static string BuildUrl(string endpoint, string path)
    {
        var trimmed = string.IsNullOrWhiteSpace(endpoint) ? "http://localhost:11434" : endpoint.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"http://{trimmed}";
        }

        return $"{trimmed.TrimEnd('/')}{path}";
    }

    // Normalizes OpenAI-compatible endpoints.
    // Examples:
    //  - https://api.openai.com -> https://api.openai.com/v1
    //  - https://ai.hackclub.com/proxy/v1 -> unchanged
    private static string BuildOpenAiUrl(string endpoint, string path)
    {
        var trimmed = string.IsNullOrWhiteSpace(endpoint) ? "https://api.openai.com/v1" : endpoint.Trim();
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        trimmed = trimmed.TrimEnd('/');
        if (!trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"{trimmed}/v1";
        }

        return $"{trimmed}{path}";
    }

    private sealed class OllamaTagsResponse
    {
        public List<OllamaTagItem>? Models { get; set; }
    }

    private sealed class OllamaTagItem
    {
        public string? Name { get; set; }
    }

    private sealed class OllamaChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public bool Stream { get; set; }
        public List<OllamaChatRequestMessage> Messages { get; set; } = new();
    }

    private sealed class OllamaChatRequestMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
        public string[]? Images { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        public OllamaChatResponseMessage? Message { get; set; }
    }

    private sealed class OllamaChatResponseMessage
    {
        public string? Content { get; set; }
    }
}
