using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;
using QRCoder;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class PhoneDashboardViewModel : ViewModelBase
{
    private readonly LoggerService _logger;
    private readonly DashboardHostService _host;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;

    [ObservableProperty] private bool isEnabled;
    [ObservableProperty] private int port;
    [ObservableProperty] private bool requirePin;
    [ObservableProperty] private string pin = string.Empty;
    [ObservableProperty] private bool advancedRemote;
    [ObservableProperty] private string url = "-";
    [ObservableProperty] private string lanIp = "-";
    [ObservableProperty] private ImageSource? qrCode;
    [ObservableProperty] private string statusText = "Dashboard stopped";

    public PhoneDashboardViewModel(LoggerService logger, DashboardHostService host, SettingsService settingsService, AppSettings settings)
    {
        _logger = logger;
        _host = host;
        _settingsService = settingsService;
        _settings = settings;

        IsEnabled = _settings.DashboardEnabled;
        Port = _settings.DashboardPort;
        RequirePin = _settings.DashboardRequirePin;
        Pin = string.IsNullOrWhiteSpace(_settings.DashboardPin) ? CreatePin() : _settings.DashboardPin!;
        AdvancedRemote = _settings.DashboardAdvancedRemote;

        _host.StateChanged += OnHostStateChanged;
        UpdateUrl(_host.State);
        StatusText = _host.State.IsRunning ? "Dashboard running" : "Dashboard stopped";
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        _settings.DashboardEnabled = IsEnabled;
        _settings.DashboardPort = Port;
        _settings.DashboardRequirePin = RequirePin;
        _settings.DashboardPin = Pin;
        _settings.DashboardAdvancedRemote = AdvancedRemote;
        _settingsService.Save(_settings);

        try
        {
            var wasRunning = _host.State.IsRunning;
            if (IsEnabled)
            {
                if (wasRunning)
                {
                    await _host.StopAsync();
                }
                await _host.StartAsync();
                StatusText = "Dashboard running";
            }
            else if (wasRunning)
            {
                await _host.StopAsync();
                StatusText = "Dashboard stopped";
            }
            else
            {
                StatusText = "Dashboard stopped";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Dashboard error: {ex.Message}";
            _logger.Error(StatusText);
        }

        UpdateUrl(_host.State);
    }

    [RelayCommand]
    private void GeneratePin()
    {
        Pin = CreatePin();
        _settings.DashboardPin = Pin;
        _settingsService.Save(_settings);
        UpdateUrl(_host.State);
    }

    private string CreatePin()
    {
        var rng = new Random();
        return rng.Next(100000, 999999).ToString();
    }

    private void OnHostStateChanged(DashboardHostState state)
    {
        UpdateUrl(state);
    }

    private void UpdateUrl(DashboardHostState state)
    {
        if (!state.IsRunning || string.IsNullOrWhiteSpace(state.PrimaryAddress))
        {
            Url = "-";
            LanIp = "-";
            QrCode = null;
            StatusText = "Dashboard stopped";
            return;
        }

        LanIp = state.PrimaryAddress;
        Url = $"http://{state.PrimaryAddress}:{state.Port}";
        QrCode = BuildQr(Url);
        StatusText = "Dashboard running";
    }

    private ImageSource? BuildQr(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var code = new PngByteQRCode(data);
        var bytes = code.GetGraphic(4);

        using var ms = new MemoryStream(bytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = ms;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
