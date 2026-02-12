namespace ArduinoFFBControlCenter.Models;

public class WizardState
{
    public int LastStep { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? SelectedPort { get; set; }
    public string? SelectedFirmwarePath { get; set; }
    public bool UseCustomBuild { get; set; }
    public string? CustomHexPath { get; set; }
    public WiringConfig Wiring { get; set; } = new();
}
