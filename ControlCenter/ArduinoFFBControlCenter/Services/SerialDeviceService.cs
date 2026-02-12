using System.IO.Ports;
using System.Text;

namespace ArduinoFFBControlCenter.Services;

public class SerialDeviceService
{
    private readonly LoggerService _logger;
    private SerialPort? _port;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private TaskCompletionSource<string>? _pendingResponse;
    private Func<string, bool>? _pendingMatch;

    public bool IsConnected => _port?.IsOpen == true;
    public string? PortName => _port?.PortName;
    public bool TelemetryEnabled { get; set; }

    public event Action<string>? LineReceived;
    public event Action<int>? TelemetryLineReceived;
    public event Action? Disconnected;

    public SerialDeviceService(LoggerService logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string portName)
    {
        Disconnect();

        _port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
        {
            NewLine = "\r\n",
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            DtrEnable = true,
            RtsEnable = true
        };
        _port.Open();

        _cts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
        _logger.Info($"Serial connected: {portName}");

        await Task.Delay(150);
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
            _port?.Close();
        }
        catch
        {
        }
        finally
        {
            _cts = null;
            _port = null;
        }
    }

    public async Task<string> SendCommandAsync(string command, Func<string, bool>? responseMatch = null, int timeoutMs = 1200)
    {
        if (_port == null || !_port.IsOpen)
        {
            throw new InvalidOperationException("Serial port not connected.");
        }

        await _commandLock.WaitAsync();
        try
        {
            _pendingResponse = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingMatch = responseMatch;

            _port.Write(command + "\r");

            using var cts = new CancellationTokenSource(timeoutMs);
            await using var reg = cts.Token.Register(() => _pendingResponse.TrySetCanceled());

            var line = await _pendingResponse.Task;
            return line;
        }
        finally
        {
            _pendingResponse = null;
            _pendingMatch = null;
            _commandLock.Release();
        }
    }

    public void SendCommandNoWait(string command)
    {
        if (_port == null || !_port.IsOpen)
        {
            return;
        }
        _port.Write(command + "\r");
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_port == null)
        {
            return;
        }
        var stream = _port.BaseStream;
        var buffer = new byte[256];
        var sb = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            }
            catch
            {
                break;
            }

            if (read <= 0)
            {
                continue;
            }

            for (int i = 0; i < read; i++)
            {
                char c = (char)buffer[i];
                if (c == '\n')
                {
                    var line = sb.ToString().Trim('\r');
                    sb.Clear();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        HandleLine(line.Trim());
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (!ct.IsCancellationRequested)
        {
            _logger.Warn("Serial connection lost.");
            Disconnected?.Invoke();
        }
    }

    private void HandleLine(string line)
    {
        if (_pendingResponse != null)
        {
            if (_pendingMatch == null || _pendingMatch(line))
            {
                _pendingResponse.TrySetResult(line);
                return;
            }
        }

        if (TelemetryEnabled && int.TryParse(line, out var torque))
        {
            TelemetryLineReceived?.Invoke(torque);
            return;
        }

        LineReceived?.Invoke(line);
        _logger.Info($"Serial: {line}");
    }
}
