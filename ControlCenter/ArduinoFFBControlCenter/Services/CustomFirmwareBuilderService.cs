using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

/// <summary>
/// Builds a Leonardo HEX from source by patching firmware pin defines from the wizard wiring config.
/// This is used when pinout differs from the stock precompiled HEX assumptions.
/// </summary>
public class CustomFirmwareBuilderService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/arduino/arduino-cli/releases/latest";
    private const string FallbackDownloadUrl = "https://downloads.arduino.cc/arduino-cli/arduino-cli_latest_Windows_64bit.zip";

    public async Task<BuildResult> BuildAsync(WiringConfig wiring, CancellationToken ct)
    {
        var result = new BuildResult();
        var log = new StringBuilder();
        log.AppendLine("=== Custom Firmware Build ===");

        var cli = await EnsureArduinoCliAsync(log, ct);
        if (cli == null)
        {
            result.Success = false;
            result.Message = "arduino-cli setup failed. Check internet access and retry, or install it manually.";
            result.OutputLog = log.ToString();
            return result;
        }

        var sourceRoot = FindSourceRoot();
        if (sourceRoot == null)
        {
            result.Success = false;
            result.Message = "Firmware source not found. Place brWheel_my next to the app or run from repo.";
            result.OutputLog = log.ToString();
            return result;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"ffb-custom-{DateTime.Now:yyyyMMddHHmmssfff}");
        Directory.CreateDirectory(tempRoot);
        var tempSrc = Path.Combine(tempRoot, "brWheel_my");
        CopyDirectory(sourceRoot, tempSrc);

        var configPath = Path.Combine(tempSrc, "Config.h");
        if (!File.Exists(configPath))
        {
            result.Success = false;
            result.Message = "Config.h was not found in firmware source.";
            result.OutputLog = log.ToString();
            return result;
        }

        var patchSummary = ApplyWiringToConfig(configPath, wiring);

        log.AppendLine($"Source: {sourceRoot}");
        log.AppendLine($"Temp: {tempSrc}");
        log.AppendLine("Applied pin mapping:");
        foreach (var line in patchSummary)
        {
            log.AppendLine("  " + line);
        }

        var coreReady = await EnsureAvrCoreInstalledAsync(cli, ct);
        log.AppendLine(coreReady.Log);
        if (!coreReady.Success)
        {
            result.Success = false;
            result.Message = "Arduino AVR core install failed.";
            result.OutputLog = log.ToString();
            return result;
        }

        var outputDir = Path.Combine(tempRoot, "build");
        Directory.CreateDirectory(outputDir);

        var inoPath = Path.Combine(tempSrc, "brWheel_my.ino");
        var args = $"compile --fqbn arduino:avr:leonardo \"{inoPath}\" --output-dir \"{outputDir}\"";
        var compile = await RunProcessAsync(cli, args, ct);
        log.AppendLine(compile.Output);

        if (compile.ExitCode != 0)
        {
            result.Success = false;
            result.Message = "Arduino CLI compile failed. See build log.";
            result.OutputPath = outputDir;
            result.OutputLog = log.ToString();
            return result;
        }

        var hex = Directory.GetFiles(outputDir, "*.hex", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (hex == null)
        {
            result.Success = false;
            result.Message = "Build completed but no HEX was generated.";
            result.OutputPath = outputDir;
            result.OutputLog = log.ToString();
            return result;
        }

        var buildsRoot = Path.Combine(AppPaths.AppDataRoot, "custom-builds");
        Directory.CreateDirectory(buildsRoot);
        var name = $"custom-{DateTime.Now:yyyyMMdd-HHmmss}.hex";
        var persistentHex = Path.Combine(buildsRoot, name);
        File.Copy(hex, persistentHex, true);

        result.Success = true;
        result.Message = "Custom HEX build complete (pinout-applied firmware).";
        result.HexPath = persistentHex;
        result.OutputPath = outputDir;
        result.OutputLog = log.ToString();
        return result;
    }

    private static string? FindSourceRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "brWheel_my"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "brWheel_my"),
            Path.Combine(Environment.CurrentDirectory, "brWheel_my")
        };

        foreach (var candidate in candidates.Select(Path.GetFullPath))
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "brWheel_my.ino")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static List<string> ApplyWiringToConfig(string configPath, WiringConfig wiring)
    {
        var lines = File.ReadAllLines(configPath).ToList();
        var summary = new List<string>();

        var rpwm = NormalizePinLiteral(wiring.RpwmPin);
        var lpwm = NormalizePinLiteral(wiring.LpwmPin);
        var encA = NormalizePinLiteral(wiring.EncoderAPin);
        var encB = NormalizePinLiteral(wiring.EncoderBPin);
        var accel = NormalizePinLiteral(wiring.ThrottlePin);
        var brake = NormalizePinLiteral(wiring.BrakePin);
        var clutch = NormalizePinLiteral(wiring.ClutchPin);

        // DIR behavior depends on output mode:
        // PWM+-: use enable pin if provided; PWM+DIR: use LPWM as direction line.
        var dirPin = wiring.PwmMode.Equals("PWM+DIR", StringComparison.OrdinalIgnoreCase)
            ? lpwm
            : NormalizePinLiteral(wiring.UseEnablePins ? wiring.REnPin : wiring.LEnPin);

        if (!string.IsNullOrWhiteSpace(rpwm))
        {
            ReplaceDefine(lines, "PWM_PIN_L", rpwm);
            summary.Add($"PWM_PIN_L <- {rpwm} (from {wiring.RpwmPin})");
        }

        if (!string.IsNullOrWhiteSpace(lpwm))
        {
            ReplaceDefine(lines, "PWM_PIN_R", lpwm);
            summary.Add($"PWM_PIN_R <- {lpwm} (from {wiring.LpwmPin})");
        }

        if (!string.IsNullOrWhiteSpace(dirPin))
        {
            ReplaceDefine(lines, "DIR_PIN", dirPin);
            summary.Add($"DIR_PIN <- {dirPin}");
        }

        if (!string.IsNullOrWhiteSpace(encA))
        {
            ReplaceDefine(lines, "QUAD_ENC_PIN_A", encA);
            summary.Add($"QUAD_ENC_PIN_A <- {encA} (from {wiring.EncoderAPin})");
        }

        if (!string.IsNullOrWhiteSpace(encB))
        {
            ReplaceDefine(lines, "QUAD_ENC_PIN_B", encB);
            summary.Add($"QUAD_ENC_PIN_B <- {encB} (from {wiring.EncoderBPin})");
        }

        if (wiring.HasPedals)
        {
            if (!string.IsNullOrWhiteSpace(accel))
            {
                ReplaceDefine(lines, "ACCEL_PIN", accel);
                summary.Add($"ACCEL_PIN <- {accel} (from {wiring.ThrottlePin})");
            }

            if (!string.IsNullOrWhiteSpace(brake))
            {
                ReplaceDefine(lines, "BRAKE_PIN", brake);
                summary.Add($"BRAKE_PIN <- {brake} (from {wiring.BrakePin})");
            }

            if (!string.IsNullOrWhiteSpace(clutch))
            {
                ReplaceDefine(lines, "CLUTCH_PIN", clutch);
                summary.Add($"CLUTCH_PIN <- {clutch} (from {wiring.ClutchPin})");
            }
        }

        File.WriteAllLines(configPath, lines);
        return summary;
    }

    private static void ReplaceDefine(IList<string> lines, string define, string value)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith($"#define {define} ", StringComparison.Ordinal))
            {
                continue;
            }

            var indentLength = lines[i].Length - trimmed.Length;
            var indent = indentLength > 0 ? lines[i][..indentLength] : string.Empty;
            var commentIndex = lines[i].IndexOf("//", StringComparison.Ordinal);
            var comment = commentIndex >= 0 ? " " + lines[i][commentIndex..].TrimStart() : string.Empty;
            lines[i] = $"{indent}#define {define} {value}{comment}";
        }
    }

    private static string NormalizePinLiteral(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return string.Empty;
        }

        var value = pin.Trim().ToUpperInvariant();
        if (value.StartsWith("D", StringComparison.Ordinal) &&
            int.TryParse(value[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d))
        {
            return d.ToString(CultureInfo.InvariantCulture);
        }

        if (value.StartsWith("A", StringComparison.Ordinal) &&
            int.TryParse(value[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a))
        {
            return $"A{a}";
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        return pin.Trim();
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

            var version = await RunProcessAsync(targetExe, "version", ct);
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
            // Fall back to static download URL.
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
        var list = await RunProcessAsync(cli, "core list", ct);
        log.AppendLine("arduino-cli core list:");
        log.AppendLine(list.Output);
        if (list.Output.Contains("arduino:avr", StringComparison.OrdinalIgnoreCase))
        {
            return (true, log.ToString());
        }

        var update = await RunProcessAsync(cli, "core update-index", ct);
        log.AppendLine("arduino-cli core update-index:");
        log.AppendLine(update.Output);

        var install = await RunProcessAsync(cli, "core install arduino:avr", ct);
        log.AppendLine("arduino-cli core install arduino:avr:");
        log.AppendLine(install.Output);
        return (install.ExitCode == 0, log.ToString());
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }

    private static async Task<ProcessRunResult> RunProcessAsync(string exe, string args, CancellationToken ct)
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

        var sb = new StringBuilder();
        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                sb.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                sb.AppendLine(e.Data);
            }
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
        return new ProcessRunResult(exitCode, sb.ToString());
    }

    private readonly record struct ProcessRunResult(int ExitCode, string Output);
}

public class BuildResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? HexPath { get; set; }
    public string? OutputPath { get; set; }
    public string? OutputLog { get; set; }
}
