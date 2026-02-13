using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class OllamaViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly OllamaService _ollama;
    private readonly ScreenCaptureService _screenCapture;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    public ObservableCollection<string> Models { get; } = new();
    public ObservableCollection<OllamaChatEntry> Messages { get; } = new();

    [ObservableProperty] private string endpoint = "http://localhost:11434";
    [ObservableProperty] private string selectedModel = string.Empty;
    [ObservableProperty] private string prompt = string.Empty;
    [ObservableProperty] private bool includeScreen = true;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string status = "Connect Ollama and load models.";
    [ObservableProperty] private ImageSource? lastCapturePreview;
    [ObservableProperty] private string lastCaptureInfo = "No screenshot captured yet.";

    public OllamaViewModel(
        LoggerService logger,
        OllamaService ollama,
        ScreenCaptureService screenCapture,
        SettingsService settingsService,
        AppSettings settings)
    {
        _logger = logger;
        _ollama = ollama;
        _screenCapture = screenCapture;
        _settingsService = settingsService;
        _settings = settings;

        Endpoint = string.IsNullOrWhiteSpace(_settings.OllamaEndpoint) ? "http://localhost:11434" : _settings.OllamaEndpoint!;
        SelectedModel = _settings.OllamaModel ?? string.Empty;
        IncludeScreen = _settings.OllamaIncludeScreenCapture;
    }

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        IsBusy = true;
        Status = "Loading Ollama models...";
        var models = await _ollama.ListModelsAsync(Endpoint, CancellationToken.None);

        Models.Clear();
        foreach (var model in models)
        {
            Models.Add(model);
        }

        if (Models.Count == 0)
        {
            Status = "No models found. Check if Ollama is running and model is pulled.";
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

        IsBusy = true;
        var history = Messages.ToList();
        Messages.Add(new OllamaChatEntry
        {
            Role = "user",
            Content = text,
            TimestampLocal = DateTime.Now
        });
        Prompt = string.Empty;

        byte[]? screenshot = null;
        if (IncludeScreen)
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

        var result = await _ollama.AskAsync(Endpoint, SelectedModel, history, text, screenshot, CancellationToken.None);
        if (!result.Success && screenshot != null)
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

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        Status = "Chat cleared.";
    }

    partial void OnEndpointChanged(string value) => PersistSettings();
    partial void OnSelectedModelChanged(string value) => PersistSettings();
    partial void OnIncludeScreenChanged(bool value) => PersistSettings();

    private void PersistSettings()
    {
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
