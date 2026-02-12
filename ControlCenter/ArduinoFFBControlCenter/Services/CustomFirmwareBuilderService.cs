using System.Diagnostics;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class CustomFirmwareBuilderService
{
    public async Task<BuildResult> BuildAsync(WiringConfig wiring, CancellationToken ct)
    {
        var result = new BuildResult();
        var cli = FindArduinoCli();
        if (cli == null)
        {
            result.Success = false;
            result.Message = "arduino-cli not found. Install Arduino CLI or add it to PATH.";
            return result;
        }

        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "brWheel_my");
        if (!Directory.Exists(sourceRoot))
        {
            result.Success = false;
            result.Message = "Firmware source not found in app package. Use the developer build or place brWheel_my next to the app.";
            return result;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"ffb-custom-{DateTime.Now:yyyyMMddHHmmss}");
        Directory.CreateDirectory(tempRoot);
        var tempSrc = Path.Combine(tempRoot, "brWheel_my");
        CopyDirectory(sourceRoot, tempSrc);

        var configPath = Path.Combine(tempSrc, "Config.h");
        if (File.Exists(configPath))
        {
            var config = File.ReadAllText(configPath);
            config = ReplaceDefine(config, "PWM_PIN_L", wiring.RpwmPin);
            config = ReplaceDefine(config, "PWM_PIN_R", wiring.LpwmPin);
            config = ReplaceDefine(config, "DIR_PIN", wiring.LpwmPin);
            config = ReplaceDefine(config, "QUAD_ENC_PIN_A", wiring.EncoderAPin);
            config = ReplaceDefine(config, "QUAD_ENC_PIN_B", wiring.EncoderBPin);
            config = ReplaceDefine(config, "ACCEL_PIN", wiring.ThrottlePin);
            config = ReplaceDefine(config, "BRAKE_PIN", wiring.BrakePin);
            config = ReplaceDefine(config, "CLUTCH_PIN", wiring.ClutchPin);
            File.WriteAllText(configPath, config);
        }

        var outputDir = Path.Combine(tempRoot, "build");
        Directory.CreateDirectory(outputDir);

        var args = $"compile --fqbn arduino:avr:leonardo \"{tempSrc}\\brWheel_my.ino\" --output-dir \"{outputDir}\"";
        var exit = await RunProcessAsync(cli, args, ct);
        if (exit != 0)
        {
            result.Success = false;
            result.Message = "Arduino CLI compile failed. Check CLI output.";
            result.OutputPath = outputDir;
            return result;
        }

        var hex = Directory.GetFiles(outputDir, "*.hex").FirstOrDefault();
        if (hex == null)
        {
            result.Success = false;
            result.Message = "Build completed but no HEX found.";
            return result;
        }

        result.Success = true;
        result.Message = "Custom HEX build complete.";
        result.HexPath = hex;
        return result;
    }

    private static string? FindArduinoCli()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "Assets", "Tools", "arduino-cli", "arduino-cli.exe");
        if (File.Exists(local))
        {
            return local;
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

    private static string ReplaceDefine(string content, string define, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return content;
        }
        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith($"#define {define}", StringComparison.Ordinal))
            {
                lines[i] = $"#define {define} {value}";
                break;
            }
        }
        return string.Join('\n', lines);
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

    private static async Task<int> RunProcessAsync(string exe, string args, CancellationToken ct)
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
        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync(ct);
        return process.ExitCode;
    }
}

public class BuildResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? HexPath { get; set; }
    public string? OutputPath { get; set; }
}
