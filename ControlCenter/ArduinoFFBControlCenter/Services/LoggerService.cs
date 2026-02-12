using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public class LoggerService
{
    private readonly object _lock = new();
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public LoggerService()
    {
        Directory.CreateDirectory(AppPaths.LogsPath);
    }

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);
    public void Error(string message) => Log("ERROR", message);

    public void Log(string level, string message)
    {
        var entry = new LogEntry { Level = level, Message = message, Timestamp = DateTime.Now };
        lock (_lock)
        {
            Entries.Insert(0, entry);
            if (Entries.Count > 1000)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }

            var logFile = Path.Combine(AppPaths.LogsPath, $"log-{DateTime.Now:yyyyMMdd}.txt");
            File.AppendAllText(logFile, $"{entry.Timestamp.ToString("o", CultureInfo.InvariantCulture)} [{level}] {message}\n");
        }
    }
}
