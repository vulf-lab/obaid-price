using System.IO;
using System.Text;

namespace CostWise.App.Services;

public static class AppLog
{
    private static readonly object Gate = new();

    public static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "logs");

    public static string CurrentLogPath =>
        Path.Combine(LogDirectory, $"costwise-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null)
    {
        var sb = new StringBuilder(message);
        for (var current = ex; current is not null; current = current.InnerException)
            sb.Append(" → ").Append(current.GetType().Name).Append(": ").Append(current.Message);
        Write("ERROR", sb.ToString());
    }

    public static string UserFacing(Exception ex) =>
        $"{ex.Message}\n\nDetails were written to:\n{CurrentLogPath}";

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (Gate)
                File.AppendAllText(CurrentLogPath, line);
        }
        catch
        {
            // never throw from logging
        }
    }
}
