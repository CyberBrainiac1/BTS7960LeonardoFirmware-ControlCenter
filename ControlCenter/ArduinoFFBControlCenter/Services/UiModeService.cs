namespace ArduinoFFBControlCenter.Services;

public class UiModeService
{
    public bool IsBeginnerMode { get; private set; } = true;
    public bool IsKidMode { get; private set; }

    public event Action<bool>? ModeChanged;
    public event Action<bool>? KidModeChanged;

    public void SetBeginnerMode(bool value)
    {
        if (IsBeginnerMode == value)
        {
            return;
        }

        IsBeginnerMode = value;
        ModeChanged?.Invoke(value);
    }

    public void SetKidMode(bool value)
    {
        if (IsKidMode == value)
        {
            return;
        }

        IsKidMode = value;
        KidModeChanged?.Invoke(value);
    }
}
