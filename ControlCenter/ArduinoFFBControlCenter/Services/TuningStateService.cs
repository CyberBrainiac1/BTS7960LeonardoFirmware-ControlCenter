using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class TuningStateService
{
    public FfbConfig? CurrentConfig { get; private set; }
    public FfbCurveSettings CurrentCurve { get; private set; } = new();
    public AdvancedTuningSettings CurrentAdvanced { get; private set; } = new();

    public event Action? StateChanged;

    public void UpdateConfig(FfbConfig config)
    {
        CurrentConfig = config;
        StateChanged?.Invoke();
    }

    public void UpdateCurve(FfbCurveSettings curve)
    {
        CurrentCurve = curve;
        StateChanged?.Invoke();
    }

    public void UpdateAdvanced(AdvancedTuningSettings advanced)
    {
        CurrentAdvanced = advanced;
        StateChanged?.Invoke();
    }
}
