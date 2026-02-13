using System.Net.Http;
using System.Net.Http.Json;
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
