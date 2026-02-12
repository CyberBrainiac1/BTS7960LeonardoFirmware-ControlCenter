using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;
using Xunit;

namespace ArduinoFFBControlCenter.Tests;

public class CalibrationInferenceTests
{
    [Fact]
    public void Assess_FlagsCalibration_WhenCenterOffsetLarge()
    {
        var window = new AxisSampleWindow(0.2, 0.1, 0.3, 0.05, 100);
        var assessment = CalibrationInference.Assess(window, centerThreshold: 0.08, extremeThreshold: 0.85);

        Assert.True(assessment.NeedsCalibration);
    }

    [Fact]
    public void Assess_Passes_WhenCenterStable()
    {
        var window = new AxisSampleWindow(0.02, -0.05, 0.05, 0.02, 100);
        var assessment = CalibrationInference.Assess(window, centerThreshold: 0.08, extremeThreshold: 0.85);

        Assert.False(assessment.NeedsCalibration);
    }
}
