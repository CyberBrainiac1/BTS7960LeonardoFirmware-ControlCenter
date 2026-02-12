namespace ArduinoFFBControlCenter.Helpers;

public readonly record struct AxisRange(bool IsSigned, int Min, int Max)
{
    public double Center => (Min + Max) / 2.0;
    public double HalfRange => (Max - Min) / 2.0;
}

public static class AxisNormalization
{
    public static AxisRange DetectRange(IEnumerable<int> samples)
    {
        var list = samples as IList<int> ?? samples.ToList();
        var signed = list.Any(s => s < 0);
        if (signed)
        {
            return new AxisRange(true, -32768, 32767);
        }

        var max = list.Count > 0 ? list.Max() : 65535;
        var min = list.Count > 0 ? list.Min() : 0;
        if (max <= 4096 && min >= 0)
        {
            return new AxisRange(false, 0, 4095);
        }

        return new AxisRange(false, 0, 65535);
    }

    public static double Normalize(int raw, AxisRange range)
    {
        var half = range.HalfRange;
        if (half <= 0)
        {
            return 0;
        }
        return (raw - range.Center) / half;
    }
}
