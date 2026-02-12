namespace ArduinoFFBControlCenter.Models;

public enum FfbCurveType
{
    Linear,
    Progressive,
    Regressive,
    Custom
}

public class FfbCurveSettings
{
    public FfbCurveType CurveType { get; set; } = FfbCurveType.Linear;
    public double Bias { get; set; } = 0.5;
    public double CustomP1 { get; set; } = 0.25;
    public double CustomP2 { get; set; } = 0.5;
    public double CustomP3 { get; set; } = 0.75;
}
