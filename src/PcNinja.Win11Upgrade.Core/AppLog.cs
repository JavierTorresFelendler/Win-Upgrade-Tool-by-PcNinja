using System.Text;

namespace PcNinja.Win11Upgrade.Core;

public static class AppLog
{
    private static readonly object Sync = new();
    public static string Folder { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "PcNinja", "Win11UpgradeAssistant");
    public static string Path { get; } = System.IO.Path.Combine(Folder, "Upgrade-Assistant.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        lock (Sync)
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(Path,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
    }
}
