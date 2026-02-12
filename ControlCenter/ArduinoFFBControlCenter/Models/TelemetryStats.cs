namespace ArduinoFFBControlCenter.Models;

public class TelemetryStats
{
    public double SampleRateHz { get; set; }
    public double TorqueLineRateHz { get; set; }
    public double ClippingPercent { get; set; }
    public double AvgLoopDtMs { get; set; }
    public double PacketLossPercent { get; set; }
}
