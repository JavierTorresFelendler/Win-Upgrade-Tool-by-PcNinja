using System.Runtime.InteropServices;

namespace PcNinja.Win11UpgradeAssistant;

internal static class UserDownloads
{
    internal static string GetPath() => Resolve(ReadKnownFolder,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    internal static string Resolve(Func<string?> readKnownFolder, string fallback)
    {
        try { return readKnownFolder() is { Length: > 0 } path ? path : fallback; }
        catch (Exception ex) when (ex is COMException or DllNotFoundException or EntryPointNotFoundException)
        { return fallback; }
    }

    private static string? ReadKnownFolder()
    {
        var id = new Guid("374DE290-123F-4565-9164-39C4925E467B"); // FOLDERID_Downloads
        IntPtr pointer = IntPtr.Zero;
        try
        {
            // Resolve the current user's redirected folder without creating it,
            // probing a network path or changing the process working directory.
            Marshal.ThrowExceptionForHR(SHGetKnownFolderPath(ref id, 0x4000, IntPtr.Zero, out pointer));
            return Marshal.PtrToStringUni(pointer);
        }
        finally { if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer); }
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(ref Guid folder, uint flags, IntPtr token, out IntPtr path);
}
