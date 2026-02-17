using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

/// <summary>
/// ViewModel for the in-app Ollama side panel.
/// Supports local chat plus optional screenshot context per prompt.
/// </summary>
public partial class OllamaViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly OllamaService _ollama;
    private readonly ScreenCaptureService _screenCapture;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly SetupWizardViewModel _setupWizard;

    public ObservableCollection<string> Models { get; } = new();
    public ObservableCollection<OllamaChatEntry> Messages { get; } = new();

    [ObservableProperty] private string endpoint = "http://localhost:11434";
    [ObservableProperty] private string selectedModel = string.Empty;
    [ObservableProperty] private string prompt = string.Empty;
    [ObservableProperty] private bool includeScreen = true;
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string userEmail = string.Empty;
    [ObservableProperty] private string apiKey = string.Empty;
    [ObservableProperty] private string aiProvider = "Ollama";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string status = "Connect Ollama and load models.";
    [ObservableProperty] private ImageSource? lastCapturePreview;
    [ObservableProperty] private string lastCaptureInfo = "No screenshot captured yet.";
    public bool NeedsUserIdentity => string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(UserEmail);
    public bool IsApiKeyMode => string.Equals(AiProvider, "ApiKey", StringComparison.OrdinalIgnoreCase);
    public bool IsOllamaMode => !IsApiKeyMode;
    public string SettingsFilePath => AppPaths.SettingsFile;

    public OllamaViewModel(
        LoggerService logger,
        OllamaService ollama,
        ScreenCaptureService screenCapture,
        SettingsService settingsService,
        AppSettings settings,
        SetupWizardViewModel setupWizard)
    {
        _logger = logger;
        _ollama = ollama;
        _screenCapture = screenCapture;
        _settingsService = settingsService;
        _settings = settings;
        _setupWizard = setupWizard;

        Endpoint = !string.IsNullOrWhiteSpace(_settings.AiEndpoint)
            ? _settings.AiEndpoint!
            : string.IsNullOrWhiteSpace(_settings.OllamaEndpoint) ? "http://localhost:11434" : _settings.OllamaEndpoint!;
        SelectedModel = _settings.AiModel ?? _settings.OllamaModel ?? string.Empty;
        IncludeScreen = _settings.OllamaIncludeScreenCapture;
        UserName = _settings.AiUserName ?? string.Empty;
        UserEmail = _settings.AiUserEmail ?? string.Empty;
        ApiKey = _settings.AiApiKey ?? string.Empty;
        AiProvider = string.IsNullOrWhiteSpace(_settings.AiProvider) ? "Ollama" : _settings.AiProvider;
    }

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        // Query locally available models from Ollama.
        IsBusy = true;
        Status = IsApiKeyMode ? "Loading API models..." : "Loading Ollama models...";
        var models = IsApiKeyMode
            ? await _ollama.ListOpenAiModelsAsync(Endpoint, ApiKey, CancellationToken.None)
            : await _ollama.ListModelsAsync(Endpoint, CancellationToken.None);

        Models.Clear();
        foreach (var model in models)
        {
            Models.Add(model);
        }

        if (Models.Count == 0)
        {
            Status = IsApiKeyMode
                ? "No models returned by API. You can still type a model name manually."
                : "No models found. Check if Ollama is running and model is pulled.";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(SelectedModel) || !Models.Contains(SelectedModel))
            {
                SelectedModel = Models.FirstOrDefault() ?? string.Empty;
            }
            Status = $"Loaded {Models.Count} model(s).";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private void CaptureScreen()
    {
        // Manual capture lets users verify what context will be sent.
        try
        {
            var png = _screenCapture.CaptureVirtualScreenPng();
            LastCapturePreview = ToImageSource(png);
            LastCaptureInfo = $"Captured {DateTime.Now:HH:mm:ss} ({Math.Round(png.Length / 1024.0, 1)} KB)";
            Status = "Screenshot captured.";
        }
        catch (Exception ex)
        {
            Status = $"Capture failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AskAsync()
    {
        var text = Prompt?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            Status = "Enter a prompt first.";
            return;
        }
        if (string.IsNullOrWhiteSpace(SelectedModel))
        {
            Status = "Select an Ollama model first.";
            return;
        }

        // Keep a trimmed history so requests stay fast and deterministic.
        IsBusy = true;
        var history = Messages.ToList();
        Messages.Add(new OllamaChatEntry
        {
            Role = "user",
            Content = text,
            TimestampLocal = DateTime.Now
        });
        Prompt = string.Empty;

        // Local automation: apply setup/pin mapping commands directly from prompt.
        var localAutomation = TryApplyWizardChanges(text);
        if (localAutomation.Applied)
        {
            Messages.Add(new OllamaChatEntry
            {
                Role = "assistant",
                Content = localAutomation.Message,
                TimestampLocal = DateTime.Now
            });
            Status = "Applied setup changes from your prompt.";
            IsBusy = false;
            return;
        }

        // Include screenshot only for Ollama local mode.
        byte[]? screenshot = null;
        if (IncludeScreen && !IsApiKeyMode)
        {
            try
            {
                screenshot = _screenCapture.CaptureVirtualScreenPng();
                LastCapturePreview = ToImageSource(screenshot);
                LastCaptureInfo = $"Captured {DateTime.Now:HH:mm:ss} ({Math.Round(screenshot.Length / 1024.0, 1)} KB)";
            }
            catch (Exception ex)
            {
                _logger.Warn($"Screen capture failed: {ex.Message}");
            }
        }

        Status = IncludeScreen && screenshot != null
            ? "Asking Ollama with screen context..."
            : "Asking Ollama...";

        (bool Success, string Content, string Error) result;
        if (IsApiKeyMode)
        {
            Status = "Asking AI endpoint...";
            result = await _ollama.AskOpenAiCompatAsync(
                Endpoint,
                ApiKey,
                SelectedModel,
                history,
                text,
                UserName,
                UserEmail,
                CancellationToken.None);
        }
        else
        {
            result = await _ollama.AskAsync(Endpoint, SelectedModel, history, text, screenshot, CancellationToken.None);
        }

        if (!IsApiKeyMode && !result.Success && screenshot != null)
        {
            // Fallback for non-vision models.
            result = await _ollama.AskAsync(Endpoint, SelectedModel, history, text, null, CancellationToken.None);
            if (result.Success)
            {
                result = (true, $"{result.Content}\n\n[Note: model did not accept image input; response is text-only.]", string.Empty);
            }
        }

        if (result.Success)
        {
            Messages.Add(new OllamaChatEntry
            {
                Role = "assistant",
                Content = result.Content,
                TimestampLocal = DateTime.Now
            });
            Status = "Response received.";
        }
        else
        {
            Messages.Add(new OllamaChatEntry
            {
                Role = "assistant",
                Content = $"[Error] {result.Error}",
                TimestampLocal = DateTime.Now
            });
            Status = "Request failed.";
        }

        IsBusy = false;
    }

    private (bool Applied, string Message) TryApplyWizardChanges(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, string.Empty);
        }

        // Trigger on likely setup intent only.
        if (!Regex.IsMatch(text, @"\b(pin|pins|wiring|rpwm|lpwm|encoder|e-?stop|button|shifter|throttle|brake|clutch|common ground|logic voltage|pwm mode)\b", RegexOptions.IgnoreCase))
        {
            return (false, string.Empty);
        }

        var result = _setupWizard.ApplyNaturalLanguageConfig(text);
        if (!result.Applied)
        {
            return (false, string.Empty);
        }

        return (true, $"{result.Summary}\n\nI updated the Setup Wizard fields in-app.");
    }

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        Status = "Chat cleared.";
    }

    [RelayCommand]
    private async Task TestApiKeyAsync()
    {
        if (!IsApiKeyMode)
        {
            Status = "API key test is only available when provider is ApiKey (change in Settings).";
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Status = "Enter an API key first.";
            return;
        }

        IsBusy = true;
        Status = "Testing API key...";
        var result = await _ollama.TestOpenAiApiKeyAsync(Endpoint, ApiKey, CancellationToken.None);
        Status = result.Success ? $"API key valid. {result.Details}" : $"API key test failed: {result.Error}";
        IsBusy = false;
    }

    [RelayCommand]
    private void SaveAiSidebar()
    {
        PersistSettings();
        Status = "AI sidebar settings saved.";
    }

    partial void OnEndpointChanged(string value) => PersistSettings();
    partial void OnSelectedModelChanged(string value) => PersistSettings();
    partial void OnIncludeScreenChanged(bool value) => PersistSettings();
    partial void OnUserNameChanged(string value)
    {
        OnPropertyChanged(nameof(NeedsUserIdentity));
        PersistSettings();
    }
    partial void OnUserEmailChanged(string value)
    {
        OnPropertyChanged(nameof(NeedsUserIdentity));
        PersistSettings();
    }
    partial void OnApiKeyChanged(string value)
    {
        PersistSettings();
    }
    partial void OnAiProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsApiKeyMode));
        OnPropertyChanged(nameof(IsOllamaMode));
        PersistSettings();
    }

    private void PersistSettings()
    {
        _settings.AiEndpoint = Endpoint;
        _settings.AiModel = SelectedModel;
        _settings.AiApiKey = ApiKey;
        _settings.AiProvider = AiProvider;
        _settings.AiUserName = UserName;
        _settings.AiUserEmail = UserEmail;
        // Keep legacy fields in sync for backward compatibility.
        _settings.OllamaEndpoint = Endpoint;
        _settings.OllamaModel = SelectedModel;
        _settings.OllamaIncludeScreenCapture = IncludeScreen;
        _settingsService.Save(_settings);
    }

    private static ImageSource ToImageSource(byte[] png)
    {
        using var ms = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
