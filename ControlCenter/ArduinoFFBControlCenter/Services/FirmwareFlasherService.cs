using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class FirmwareFlasherService
{
    private readonly LoggerService _logger;

    public FirmwareFlasherService(LoggerService logger)
    {
        _logger = logger;
    }

    public string ToolsRoot => Path.Combine(AppContext.BaseDirectory, "Assets", "Tools", "avrdude");

    public string AvrDudePath => Path.Combine(ToolsRoot, "avrdude.exe");
    public string AvrDudeConf => Path.Combine(ToolsRoot, "avrdude.conf");

    public bool ToolsAvailable => File.Exists(AvrDudePath) && File.Exists(AvrDudeConf);

    public async Task<FlashResult> ResetBoardAsync(string port, IProgress<string> progress, CancellationToken ct)
    {
        var result = new FlashResult();
        if (!SerialPort.GetPortNames().Any(p => string.Equals(p, port, StringComparison.OrdinalIgnoreCase)))
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.PortNotFound;
            result.UserMessage = $"Port {port} not found.";
            result.SuggestedAction = "Reconnect the device or rescan ports.";
            return result;
        }

        if (IsPortBusy(port, out var busyReason))
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.PortBusy;
            result.UserMessage = "COM port is busy.";
            result.SuggestedAction = busyReason;
            return result;
        }

        var bootPort = await EnterBootloaderAsync(port, progress, ct);
        if (bootPort == null)
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.BootloaderNotDetected;
            result.UserMessage = "Reset command sent, but bootloader port was not detected.";
            result.SuggestedAction = "Press reset twice quickly and try again.";
            return result;
        }

        result.Success = true;
        result.ErrorType = FlashErrorType.None;
        result.BootloaderPort = bootPort;
        result.UserMessage = "Board reset complete.";
        result.SuggestedAction = "Wait a few seconds for the normal COM port to return.";
        return result;
    }

    public async Task<FlashResult> FlashWithRetryAsync(string hexPath, string port, IProgress<string> progress, CancellationToken ct, bool skipReset = false)
    {
        var first = await FlashInternalAsync(hexPath, port, progress, ct, skipReset);
        if (first.Success || first.ErrorType == FlashErrorType.PortBusy || first.ErrorType == FlashErrorType.AvrDudeMissing || first.ErrorType == FlashErrorType.PortNotFound)
        {
            return first;
        }

        progress.Report("Retrying flash once...");
        var second = await FlashInternalAsync(hexPath, port, progress, ct, skipReset);
        second.RetryCount = 1;
        return second;
    }

    private async Task<FlashResult> FlashInternalAsync(string hexPath, string port, IProgress<string> progress, CancellationToken ct, bool skipReset)
    {
        var result = new FlashResult();
        if (!ToolsAvailable)
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.AvrDudeMissing;
            result.UserMessage = "avrdude not found.";
            result.SuggestedAction = "Place avrdude.exe and avrdude.conf in Assets/Tools/avrdude.";
            return result;
        }

        if (!SerialPort.GetPortNames().Any(p => string.Equals(p, port, StringComparison.OrdinalIgnoreCase)))
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.PortNotFound;
            result.UserMessage = $"Port {port} not found.";
            result.SuggestedAction = "Reconnect the device or rescan ports.";
            return result;
        }

        if (IsPortBusy(port, out var busyReason))
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.PortBusy;
            result.UserMessage = "COM port is busy.";
            result.SuggestedAction = busyReason;
            return result;
        }

        string? targetPort = port;
        if (!skipReset)
        {
            var bootPort = await EnterBootloaderAsync(port, progress, ct);
            if (bootPort == null)
            {
                result.Success = false;
                result.ErrorType = FlashErrorType.BootloaderNotDetected;
                result.UserMessage = "Bootloader port not detected.";
                result.SuggestedAction = "Press the reset button twice quickly, then use Manual Recovery.";
                return result;
            }
            targetPort = bootPort;
            result.BootloaderPort = bootPort;
        }

        var logBuffer = new StringBuilder();
        var flashArgs = $"-C \"{AvrDudeConf}\" -v -p atmega32u4 -c avr109 -P {targetPort} -b 57600 -D -U flash:w:\"{hexPath}\":i";
        progress.Report($"Running avrdude on {targetPort}...");
        var flashExit = await RunAvrDudeAsync(flashArgs, progress, logBuffer, ct);
        result.ExitCode = flashExit;
        result.RawOutput = logBuffer.ToString();
        if (flashExit != 0)
        {
            ApplyFriendlyError(result);
            TryWriteLastFlash(logBuffer);
            return result;
        }

        var verifyArgs = $"-C \"{AvrDudeConf}\" -v -p atmega32u4 -c avr109 -P {targetPort} -b 57600 -D -U flash:v:\"{hexPath}\":i";
        progress.Report("Verifying flash...");
        var verifyExit = await RunAvrDudeAsync(verifyArgs, progress, logBuffer, ct);
        result.ExitCode = verifyExit;
        result.RawOutput = logBuffer.ToString();
        if (verifyExit != 0)
        {
            result.ErrorType = FlashErrorType.VerifyFailed;
            result.UserMessage = "Flash verify failed.";
            result.SuggestedAction = "Retry the flash or use a different USB cable.";
            TryWriteLastFlash(logBuffer);
            return result;
        }

        result.Success = true;
        result.ErrorType = FlashErrorType.None;
        result.UserMessage = "Flash complete.";
        progress.Report("Flash complete.");
        TryWriteLastFlash(logBuffer);
        return result;
    }

    private async Task<string?> EnterBootloaderAsync(string port, IProgress<string> progress, CancellationToken ct)
    {
        try
        {
            var before = SerialPort.GetPortNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var sawDisappear = false;

            using (var sp = new SerialPort(port, 1200))
            {
                sp.DtrEnable = false;
                sp.Open();
                sp.Close();
            }

            progress.Report("Bootloader reset triggered (1200 baud). Waiting for boot port...");
            var timeout = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < timeout && !ct.IsCancellationRequested)
            {
                await Task.Delay(200, ct);
                var now = SerialPort.GetPortNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
                var diff = now.Except(before).ToList();
                if (diff.Count > 0)
                {
                    var newPort = diff.First();
                    progress.Report($"Bootloader port detected: {newPort}");
                    return newPort;
                }
                if (!now.Contains(port))
                {
                    sawDisappear = true;
                }
                if (sawDisappear && now.Contains(port))
                {
                    progress.Report($"Bootloader reappeared on {port}");
                    return port;
                }
            }
        }
        catch (Exception ex)
        {
            progress.Report($"Bootloader reset failed: {ex.Message}");
        }

        progress.Report("Bootloader port not detected.");
        return null;
    }

    private async Task<int> RunAvrDudeAsync(string args, IProgress<string> progress, StringBuilder logBuffer, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = AvrDudePath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = ToolsRoot
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<int>();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                progress.Report(e.Data);
                logBuffer.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                progress.Report(e.Data);
                logBuffer.AppendLine(e.Data);
            }
        };
        process.Exited += (_, __) => tcs.TrySetResult(process.ExitCode);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        });

        return await tcs.Task;
    }

    private bool IsPortBusy(string port, out string reason)
    {
        reason = "Close other serial tools (Arduino IDE, legacy GUI) and disconnect in the app.";
        try
        {
            using var sp = new SerialPort(port, 115200);
            sp.Open();
            sp.Close();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch
        {
            return true;
        }
    }

    private void ApplyFriendlyError(FlashResult result)
    {
        var output = result.RawOutput ?? string.Empty;
        if (output.Contains("ser_open", StringComparison.OrdinalIgnoreCase) && output.Contains("access", StringComparison.OrdinalIgnoreCase))
        {
            result.ErrorType = FlashErrorType.PortBusy;
            result.UserMessage = "COM port is busy.";
            result.SuggestedAction = "Close Arduino IDE/legacy GUI and disconnect in the app.";
            return;
        }

        if (output.Contains("not in sync", StringComparison.OrdinalIgnoreCase) || output.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            result.ErrorType = FlashErrorType.BootloaderNotDetected;
            result.UserMessage = "Bootloader did not respond.";
            result.SuggestedAction = "Press reset twice quickly, then try Manual Recovery.";
            return;
        }

        if (output.Contains("device signature", StringComparison.OrdinalIgnoreCase))
        {
            result.ErrorType = FlashErrorType.SignatureMismatch;
            result.UserMessage = "Device signature mismatch.";
            result.SuggestedAction = "Verify the board is Leonardo (ATmega32U4) and select the correct HEX.";
            return;
        }

        result.ErrorType = FlashErrorType.FlashFailed;
        result.UserMessage = "Flash failed.";
        result.SuggestedAction = "Retry the flash or use a different USB cable.";
    }

    private void TryWriteLastFlash(StringBuilder logBuffer)
    {
        try
        {
            File.WriteAllText(Path.Combine(AppPaths.AppDataRoot, "last_flash.txt"), logBuffer.ToString());
        }
        catch
        {
        }
    }
}
