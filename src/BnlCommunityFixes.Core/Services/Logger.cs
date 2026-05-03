namespace BnlCommunityFixes.Core.Services;

public sealed class Logger
{
    private readonly string path;
    private readonly object sync = new();

    public Logger(string path)
    {
        this.path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public void Info(string message) => Write("Info", message);

    public void Warning(string message) => Write("Warning", message);

    public void Error(string message) => Write("Error", message);

    public void Exception(Exception exception, string context)
    {
        Write("Error", $"{context}: {exception}");
    }

    private void Write(string level, string message)
    {
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (sync)
        {
            File.AppendAllLines(path, [line]);
        }
    }
}
