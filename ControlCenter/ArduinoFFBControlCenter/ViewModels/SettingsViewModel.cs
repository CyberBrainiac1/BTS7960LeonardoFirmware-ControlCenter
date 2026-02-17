using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly ThemeService _themeService;

    [ObservableProperty] private bool sendAsInoMode;
    [ObservableProperty] private bool forcePinoutBuild;
    [ObservableProperty] private bool autoConnect;
    [ObservableProperty] private bool autoApplyLastProfile;
    [ObservableProperty] private bool beginnerMode;
    [ObservableProperty] private bool demoMode;
    [ObservableProperty] private bool aiChatEnabled = true;
    [ObservableProperty] private string themeMode = "SoftDark";
    [ObservableProperty] private string aiProvider = "Ollama";
    [ObservableProperty] private string aiEndpoint = "http://localhost:11434";
    [ObservableProperty] private string aiModel = string.Empty;
    [ObservableProperty] private string aiApiKey = string.Empty;
    [ObservableProperty] private string saveStatus = "Not saved";

    public IReadOnlyList<string> ThemeModes { get; } = new[] { "SoftDark", "Light" };
    public IReadOnlyList<string> AiProviders { get; } = new[] { "Ollama", "ApiKey" };
    public bool IsApiKeyProvider => string.Equals(AiProvider, "ApiKey", StringComparison.OrdinalIgnoreCase);

    public SettingsViewModel(SettingsService settingsService, AppSettings settings, ThemeService themeService)
    {
        _settingsService = settingsService;
        _settings = settings;
        _themeService = themeService;

        SendAsInoMode = _settings.SendAsInoMode;
        ForcePinoutBuild = _settings.ForcePinoutBuild;
        AutoConnect = _settings.AutoConnect;
        AutoApplyLastProfile = _settings.AutoApplyLastProfile;
        BeginnerMode = _settings.BeginnerMode;
        DemoMode = _settings.DemoMode;
        AiChatEnabled = _settings.AiChatEnabled;
        ThemeMode = string.IsNullOrWhiteSpace(_settings.ThemeMode) ? "SoftDark" : _settings.ThemeMode;
        AiProvider = string.IsNullOrWhiteSpace(_settings.AiProvider) ? "Ollama" : _settings.AiProvider;
        AiEndpoint = string.IsNullOrWhiteSpace(_settings.AiEndpoint) ? "http://localhost:11434" : _settings.AiEndpoint!;
        AiModel = _settings.AiModel ?? string.Empty;
        AiApiKey = _settings.AiApiKey ?? string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.SendAsInoMode = SendAsInoMode;
        _settings.ForcePinoutBuild = ForcePinoutBuild;
        _settings.AutoConnect = AutoConnect;
        _settings.AutoApplyLastProfile = AutoApplyLastProfile;
        _settings.BeginnerMode = BeginnerMode;
        _settings.DemoMode = DemoMode;
        _settings.AiChatEnabled = AiChatEnabled;
        _settings.ThemeMode = ThemeMode;
        _settings.AiProvider = AiProvider;
        _settings.AiEndpoint = AiEndpoint;
        _settings.AiModel = AiModel;
        _settings.AiApiKey = AiApiKey;
        _themeService.ApplyTheme(ThemeMode);
        _settingsService.Save(_settings);
        SaveStatus = $"Saved at {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    private void ResetBuildSettings()
    {
        SendAsInoMode = false;
        ForcePinoutBuild = false;
        Save();
    }

    partial void OnAiProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsApiKeyProvider));
    }
}
