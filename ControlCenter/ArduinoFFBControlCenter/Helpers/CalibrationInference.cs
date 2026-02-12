using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Helpers;

public static class CalibrationInference
{
    public static CalibrationAssessment Assess(AxisSampleWindow window, double centerThreshold = 0.08, double extremeThreshold = 0.85)
    {
        var assessment = new CalibrationAssessment { IsSupported = window.Count > 0 };
        if (window.Count == 0)
        {
            assessment.NeedsCalibration = true;
            assessment.Reason = "No HID samples available.";
            return assessment;
        }

        var mean = window.Mean;
        var absMean = Math.Abs(mean);
        if (absMean >= extremeThreshold)
        {
            assessment.NeedsCalibration = true;
            assessment.Reason = "Axis is near an end stop at rest.";
            return assessment;
        }

        if (absMean >= centerThreshold)
        {
            assessment.NeedsCalibration = true;
            assessment.Reason = $"Center offset detected ({mean:+0.00;-0.00;0.00}).";
            return assessment;
        }

        assessment.NeedsCalibration = false;
        assessment.Reason = "Center looks stable.";
        return assessment;
    }
}
