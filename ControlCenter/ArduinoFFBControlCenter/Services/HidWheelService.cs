using SharpDX.DirectInput;

namespace ArduinoFFBControlCenter.Services;

public class HidWheelService
{
    private readonly LoggerService _logger;
    private DirectInput? _directInput;
    private Joystick? _joystick;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public double WheelAngle { get; private set; }
    public int AxisY { get; private set; }
    public int AxisZ { get; private set; }
    public int AxisRx { get; private set; }
    public int AxisRy { get; private set; }
    public int AxisRz { get; private set; }
    public int Slider0 { get; private set; }
    public int Slider1 { get; private set; }
    public bool[] Buttons { get; private set; } = new bool[32];
    public event Action? StateUpdated;
    public bool IsAttached => _joystick != null;

    public HidWheelService(LoggerService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DeviceInstance> EnumerateDevices()
    {
        _directInput ??= new DirectInput();
        return _directInput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AttachedOnly)
            .Concat(_directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AttachedOnly))
            .ToList();
    }

    public bool Attach(Guid instanceGuid)
    {
        try
        {
            _directInput ??= new DirectInput();
            _joystick?.Dispose();
            _joystick = new Joystick(_directInput, instanceGuid);
            _joystick.Properties.BufferSize = 128;
            _joystick.Acquire();
            _logger.Info($"HID attached: {_joystick.Information.ProductName}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warn($"HID attach failed: {ex.Message}");
            return false;
        }
    }

    public void Start()
    {
        if (_joystick == null)
        {
            return;
        }
        _cts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _pollTask = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        if (_joystick == null)
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                _joystick.Poll();
                var state = _joystick.GetCurrentState();
                WheelAngle = state.X;
                AxisY = state.Y;
                AxisZ = state.Z;
                AxisRx = state.RotationX;
                AxisRy = state.RotationY;
                AxisRz = state.RotationZ;
                var sliders = state.Sliders;
                if (sliders != null && sliders.Length > 0)
                {
                    Slider0 = sliders[0];
                    Slider1 = sliders.Length > 1 ? sliders[1] : 0;
                }
                else
                {
                    Slider0 = 0;
                    Slider1 = 0;
                }
                var btns = state.Buttons;
                var count = Math.Min(btns.Length, Buttons.Length);
                for (int i = 0; i < count; i++)
                {
                    Buttons[i] = btns[i];
                }
                StateUpdated?.Invoke();
            }
            catch
            {
                // ignore polling errors
            }

            await Task.Delay(5, ct);
        }
    }
}
