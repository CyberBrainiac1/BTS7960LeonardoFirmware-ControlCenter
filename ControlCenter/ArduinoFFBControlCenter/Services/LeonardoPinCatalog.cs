using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public static class LeonardoPinCatalog
{
    public static List<PinOption> GetAllPins()
    {
        var pins = new List<PinOption>();
        for (var i = 0; i <= 13; i++)
        {
            pins.Add(new PinOption
            {
                Name = $"D{i}",
                IsPwm = IsPwm(i),
                IsInterrupt = IsInterrupt(i),
                IsAnalog = false
            });
        }

        for (var i = 0; i <= 5; i++)
        {
            pins.Add(new PinOption
            {
                Name = $"A{i}",
                IsPwm = false,
                IsInterrupt = false,
                IsAnalog = true
            });
        }

        return pins;
    }

    public static List<PinOption> GetPwmPins() => GetAllPins().Where(p => p.IsPwm).ToList();
    public static List<PinOption> GetInterruptPins() => GetAllPins().Where(p => p.IsInterrupt).ToList();
    public static List<PinOption> GetAnalogPins() => GetAllPins().Where(p => p.IsAnalog).ToList();

    private static bool IsPwm(int pin) => pin is 3 or 5 or 6 or 9 or 10 or 11 or 13;
    private static bool IsInterrupt(int pin) => pin is 0 or 1 or 2 or 3 or 7;
}
