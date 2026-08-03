namespace DirectorySorter.Core;

public enum LogLevel { Info, Warn, Error }

/// <summary>Minimal console + rolling file logger. No external dependencies.</summary>
public sealed class Logger
{
    private readonly string? _logFile;
    private readonly object _lock = new();

    public Logger(string? logFile = null)
    {
        _logFile = logFile;
        if (_logFile is not null)
        {
            var dir = Path.GetDirectoryName(_logFile);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }
    }

    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warn, message);
    public void Error(string message) => Write(LogLevel.Error, message);

    private void Write(LogLevel level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

        lock (_lock)
        {
            var prevColor = Console.ForegroundColor;
            Console.ForegroundColor = level switch
            {
                LogLevel.Warn => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => prevColor
            };
            Console.WriteLine(line);
            Console.ForegroundColor = prevColor;

            if (_logFile is not null)
                File.AppendAllText(_logFile, line + Environment.NewLine);
        }
    }
}
