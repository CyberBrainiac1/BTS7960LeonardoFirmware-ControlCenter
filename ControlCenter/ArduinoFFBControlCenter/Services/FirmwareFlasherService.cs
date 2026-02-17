using System.Diagnostics;
using System.IO.Compression;
using System.IO.Ports;
using System.Net.Http;
using System.Text.Json;
using System.Linq;
using System.Text;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

/// <summary>
/// Handles all firmware-flash operations for Leonardo boards using avrdude.
/// The flow matches the official Arduino bootloader behavior:
/// 1200-baud touch -> temporary bootloader COM port -> flash -> verify.
/// </summary>
public class FirmwareFlasherService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/arduino/arduino-cli/releases/latest";
    private const string FallbackDownloadUrl = "https://downloads.arduino.cc/arduino-cli/arduino-cli_latest_Windows_64bit.zip";

    private readonly LoggerService _logger;

    public FirmwareFlasherService(LoggerService logger)
    {
        _logger = logger;
    }

    public string ToolsRoot => Path.Combine(AppContext.BaseDirectory, "Assets", "Tools", "avrdude");

    public string AvrDudePath => Path.Combine(ToolsRoot, "avrdude.exe");
    public string AvrDudeConf => Path.Combine(ToolsRoot, "avrdude.conf");

    // Flashing cannot run without these bundled tools.
    public bool ToolsAvailable => File.Exists(AvrDudePath) && File.Exists(AvrDudeConf);

    /// <summary>
    /// Performs only the reset/bootloader step (no flashing).
    /// Useful when the user wants to re-enumerate the board quickly.
    /// </summary>
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

    /// <summary>
    /// Attempts flashing once, then retries exactly once for transient failures.
    /// Busy port / missing tools / missing COM are returned immediately.
    /// </summary>
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

    // Single flash attempt: validate environment, optionally reset, write, then verify.
    private async Task<FlashResult> FlashInternalAsync(string hexPath, string port, IProgress<string> progress, CancellationToken ct, bool skipReset)
    {
        var result = new FlashResult();
        var logBuffer = new StringBuilder();

        if (!File.Exists(hexPath))
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.FlashFailed;
            result.UserMessage = "HEX file not found.";
            result.SuggestedAction = "Re-select firmware and try again.";
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

        if (!ToolsAvailable)
        {
            progress.Report("Bundled avrdude not found. Falling back to arduino-cli uploader...");
            var cliFallback = await FlashWithArduinoCliAsync(hexPath, port, progress, logBuffer, ct);
            if (!cliFallback.Success && cliFallback.ErrorType == FlashErrorType.AvrDudeMissing)
            {
                cliFallback.SuggestedAction = "Install arduino-cli with 'winget install arduino.arduino-cli' or include avrdude under Assets/Tools/avrdude.";
            }
            TryWriteLastFlash(logBuffer);
            return cliFallback;
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

    private async Task<FlashResult> FlashWithArduinoCliAsync(string hexPath, string port, IProgress<string> progress, StringBuilder logBuffer, CancellationToken ct)
    {
        var result = new FlashResult();
        var cli = await EnsureArduinoCliAsync(logBuffer, ct);
        if (string.IsNullOrWhiteSpace(cli))
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.AvrDudeMissing;
            result.UserMessage = "avrdude not found and arduino-cli fallback setup failed.";
            result.SuggestedAction = "Install arduino-cli manually, then retry flashing.";
            result.RawOutput = logBuffer.ToString();
            return result;
        }

        var coreReady = await EnsureAvrCoreInstalledAsync(cli, ct);
        logBuffer.AppendLine(coreReady.Log);
        if (!coreReady.Success)
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.FlashFailed;
            result.UserMessage = "arduino:avr core install failed.";
            result.SuggestedAction = "Check internet access and retry.";
            result.RawOutput = logBuffer.ToString();
            return result;
        }

        var args = $"upload -p {port} --fqbn arduino:avr:leonardo --input-file \"{hexPath}\" --verify -v";
        progress.Report("Running arduino-cli upload...");
        var upload = await RunProcessAsync(cli, args, progress, logBuffer, ct);
        result.ExitCode = upload.ExitCode;
        result.RawOutput = logBuffer.ToString();

        if (upload.ExitCode != 0)
        {
            result.Success = false;
            result.ErrorType = FlashErrorType.FlashFailed;
            result.UserMessage = "Fallback uploader failed.";
            result.SuggestedAction = "Press reset twice quickly, then retry.";
            return result;
        }

        result.Success = true;
        result.ErrorType = FlashErrorType.None;
        result.UserMessage = "Flash complete (arduino-cli fallback).";
        progress.Report(result.UserMessage);
        return result;
    }

    // Leonardo bootloader detection:
    // 1) remember COM ports
    // 2) open+close selected port at 1200 baud
    // 3) watch for a new COM or temporary disappearance/reappearance
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

    // Executes avrdude and streams stdout/stderr back to the UI log.
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

    // Busy check is done before reset/flash to avoid collisions with IDE, serial monitors, legacy GUI, etc.
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

    // Converts raw avrdude output into actionable, beginner-friendly hints.
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

    // Keeps the last full flash log for diagnostics/support bundle export.
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

    private static async Task<string?> EnsureArduinoCliAsync(StringBuilder log, CancellationToken ct)
    {
        var existing = FindArduinoCli();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            log.AppendLine($"Using arduino-cli: {existing}");
            return existing;
        }

        log.AppendLine("arduino-cli not found. Attempting automatic setup...");
        var installed = await TryInstallPortableArduinoCliAsync(log, ct);
        if (installed)
        {
            var cli = FindArduinoCli();
            if (!string.IsNullOrWhiteSpace(cli))
            {
                log.AppendLine($"arduino-cli ready: {cli}");
                return cli;
            }
        }

        log.AppendLine("Automatic arduino-cli setup failed.");
        return null;
    }

    private static string? FindArduinoCli()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "Assets", "Tools", "arduino-cli", "arduino-cli.exe");
        if (File.Exists(local))
        {
            return local;
        }

        var appDataCli = Path.Combine(AppPaths.AppDataRoot, "tools", "arduino-cli", "arduino-cli.exe");
        if (File.Exists(appDataCli))
        {
            return appDataCli;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "arduino-cli",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd();
            proc?.WaitForExit(2000);
            var line = output?.Split(Environment.NewLine).FirstOrDefault(l => l.EndsWith("arduino-cli.exe", StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> TryInstallPortableArduinoCliAsync(StringBuilder log, CancellationToken ct)
    {
        try
        {
            var toolsRoot = Path.Combine(AppPaths.AppDataRoot, "tools");
            var cliRoot = Path.Combine(toolsRoot, "arduino-cli");
            Directory.CreateDirectory(cliRoot);

            var zipPath = Path.Combine(Path.GetTempPath(), $"arduino-cli-{Guid.NewGuid():N}.zip");
            string? downloadUrl = await GetLatestCliWindowsZipUrlAsync(ct);
            downloadUrl ??= FallbackDownloadUrl;

            log.AppendLine($"Downloading arduino-cli from: {downloadUrl}");
            using (var client = CreateHttpClient())
            using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                if (!response.IsSuccessStatusCode)
                {
                    log.AppendLine($"Download failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return false;
                }

                await using var input = await response.Content.ReadAsStreamAsync(ct);
                await using var output = File.Create(zipPath);
                await input.CopyToAsync(output, ct);
            }

            log.AppendLine("Extracting arduino-cli...");
            ZipFile.ExtractToDirectory(zipPath, cliRoot, true);

            var discovered = Directory.GetFiles(cliRoot, "arduino-cli.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(discovered))
            {
                log.AppendLine("Extracted archive did not contain arduino-cli.exe.");
                return false;
            }

            var targetExe = Path.Combine(cliRoot, "arduino-cli.exe");
            if (!string.Equals(discovered, targetExe, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(discovered, targetExe, true);
            }

            var version = await RunProcessAsync(targetExe, "version", null, log, ct);
            log.AppendLine("arduino-cli version check:");
            log.AppendLine(version.Output);
            return version.ExitCode == 0;
        }
        catch (Exception ex)
        {
            log.AppendLine($"Auto-install error: {ex.Message}");
            return false;
        }
    }

    private static async Task<string?> GetLatestCliWindowsZipUrlAsync(CancellationToken ct)
    {
        try
        {
            using var client = CreateHttpClient();
            using var response = await client.GetAsync(LatestReleaseApi, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameProp) ||
                    !asset.TryGetProperty("browser_download_url", out var urlProp))
                {
                    continue;
                }

                var name = nameProp.GetString() ?? string.Empty;
                if (!name.Contains("Windows_64bit.zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }
        catch
        {
            // fallback URL below
        }

        return null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ArduinoFFBControlCenter/1.0");
        return client;
    }

    private static async Task<(bool Success, string Log)> EnsureAvrCoreInstalledAsync(string cli, CancellationToken ct)
    {
        var log = new StringBuilder();
        var list = await RunProcessAsync(cli, "core list", null, log, ct);
        if (list.Output.Contains("arduino:avr", StringComparison.OrdinalIgnoreCase))
        {
            return (true, log.ToString());
        }

        await RunProcessAsync(cli, "core update-index", null, log, ct);
        var install = await RunProcessAsync(cli, "core install arduino:avr", null, log, ct);
        return (install.ExitCode == 0, log.ToString());
    }

    private static async Task<ProcessRunResult> RunProcessAsync(string exe, string args, IProgress<string>? progress, StringBuilder log, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }
            log.AppendLine(e.Data);
            progress?.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }
            log.AppendLine(e.Data);
            progress?.Report(e.Data);
        };
        process.Exited += (_, __) => tcs.TrySetResult(process.ExitCode);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });

        var exitCode = await tcs.Task;
        return new ProcessRunResult(exitCode, log.ToString());
    }

    private readonly record struct ProcessRunResult(int ExitCode, string Output);
}
