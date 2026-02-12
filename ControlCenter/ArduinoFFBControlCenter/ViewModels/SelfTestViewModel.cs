using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ArduinoFFBControlCenter.Models;
using ArduinoFFBControlCenter.Services;

namespace ArduinoFFBControlCenter.ViewModels;

public partial class SelfTestViewModel : ViewModelBase
{
    private readonly SelfTestService _selfTest;
    private readonly SnapshotService _snapshots;
    private readonly TelemetryService _telemetry;
    private readonly DeviceStateService _deviceState;
    private readonly LoggerService _logger;

    public ObservableCollection<string> ReportLines { get; } = new();

    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string status = "Ready";

    public SelfTestViewModel(LoggerService logger, SelfTestService selfTest, SnapshotService snapshots, TelemetryService telemetry, DeviceStateService deviceState)
    {
        _logger = logger;
        _selfTest = selfTest;
        _snapshots = snapshots;
        _telemetry = telemetry;
        _deviceState = deviceState;
    }

    [RelayCommand]
    private async Task RunSelfTestAsync()
    {
        IsRunning = true;
        Status = "Running self-test...";
        ReportLines.Clear();

        var report = await _selfTest.RunAsync(CancellationToken.None);
        ReportLines.Add($"Encoder direction: {(report.EncoderDirectionOk ? "PASS" : "FAIL")}");
        ReportLines.Add($"Buttons: {(report.ButtonsOk ? "PASS" : "FAIL")}");
        var motorStatus = report.MotorDirectionTested ? (report.MotorDirectionOk ? "PASS" : "FAIL") : "SKIPPED";
        var endstopStatus = report.EndstopTested ? (report.EndstopOk ? "PASS" : "FAIL") : "SKIPPED";
        ReportLines.Add($"Motor direction: {motorStatus}");
        ReportLines.Add($"Endstop: {endstopStatus}");
        if (!string.IsNullOrWhiteSpace(report.Notes))
        {
            ReportLines.Add(report.Notes);
        }

        _snapshots.CreateSnapshot(new SnapshotEntry
        {
            Kind = SnapshotKind.SelfTest,
            Label = "Self-test report",
            FirmwareVersion = _deviceState.CurrentDevice?.FirmwareVersion,
            Notes = report.Notes
        }, _telemetry.GetSamplesSnapshot());

        Status = "Self-test complete.";
        IsRunning = false;
    }
}
