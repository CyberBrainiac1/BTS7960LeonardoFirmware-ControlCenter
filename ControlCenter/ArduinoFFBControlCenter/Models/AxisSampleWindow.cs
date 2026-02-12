namespace ArduinoFFBControlCenter.Models;

public class AxisSampleWindow
{
    public AxisSampleWindow(double mean, double min, double max, double stdDev, int count)
    {
        Mean = mean;
        Min = min;
        Max = max;
        StdDev = stdDev;
        Count = count;
    }

    public double Mean { get; }
    public double Min { get; }
    public double Max { get; }
    public double StdDev { get; }
    public int Count { get; }
}
