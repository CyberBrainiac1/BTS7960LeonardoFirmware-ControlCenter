using System;

namespace ArduinoFFBControlCenter.Models;

public class WiringConfig
{
    public string ProfileName { get; set; } = "Default";

    // BTS7960 control pins
    public string? RpwmPin { get; set; }
    public string? LpwmPin { get; set; }
    public string? REnPin { get; set; }
    public string? LEnPin { get; set; }

    // Encoder
    public string? EncoderAPin { get; set; }
    public string? EncoderBPin { get; set; }

    // Optional controls
    public string? EStopPin { get; set; }
    public string? Button1Pin { get; set; }
    public string? Button2Pin { get; set; }
    public string? ShifterXPin { get; set; }
    public string? ShifterYPin { get; set; }

    // Pedals (analog)
    public string? ThrottlePin { get; set; }
    public string? BrakePin { get; set; }
    public string? ClutchPin { get; set; }

    // Wiring confirmations
    public string MotorPlusTerminal { get; set; } = "M+";
    public string MotorMinusTerminal { get; set; } = "M-";
    public string LogicVoltage { get; set; } = "5V";
    public string PwmMode { get; set; } = "PWM+-";
    public bool CommonGround { get; set; } = true;
    public bool UseEnablePins { get; set; } = true;
    public bool LogicVccConnected { get; set; } = true;
    public bool LogicGndConnected { get; set; } = true;
    public bool HasPedals { get; set; } = true;

    public bool IsDefaultLeonardo()
    {
        return string.Equals(RpwmPin, "D9", StringComparison.OrdinalIgnoreCase)
               && string.Equals(LpwmPin, "D10", StringComparison.OrdinalIgnoreCase)
               && string.Equals(REnPin, "D7", StringComparison.OrdinalIgnoreCase)
               && string.Equals(LEnPin, "D8", StringComparison.OrdinalIgnoreCase)
               && string.Equals(EncoderAPin, "D2", StringComparison.OrdinalIgnoreCase)
               && string.Equals(EncoderBPin, "D3", StringComparison.OrdinalIgnoreCase);
    }
}
