namespace ArduinoFFBControlCenter.Models;

public enum SnapshotKind
{
    Flash,
    SaveToWheel,
    ApplyProfile,
    Calibration,
    SelfTest,
    Revert,
    Manual
}

public class SnapshotEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public SnapshotKind Kind { get; set; } = SnapshotKind.Manual;
    public string? Label { get; set; }
    public string? DeviceId { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? FirmwareHexPath { get; set; }
    public string? ProfileName { get; set; }
    public FfbConfig? Config { get; set; }
    public FfbCurveSettings? Curve { get; set; }
    public AdvancedTuningSettings? Advanced { get; set; }
    public CalibrationRecord? Calibration { get; set; }
    public WiringConfig? Wiring { get; set; }
    public string? Notes { get; set; }
}
