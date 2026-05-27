// TEMPORARY DEBUG LOGGING - The log goes to
// %LocalAppData%\Morphic\rm-trace.log (e.g., C:\Users\<user>\AppData\Local\Morphic\rm-trace.log).
// Tail it during install to see how far the shutdown signal makes it through
// SubclassWndProc -> dispatched Shutdown -> App.Shutdown -> Exit.

namespace Morphic;

internal static class RmTraceLog
{
    private static readonly object s_lockObject = new();

    private static readonly string s_logPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
        "Morphic",
        "rm-trace.log");

    public static void Log(string message)
    {
        try
        {
            lock (s_lockObject)
            {
                var directory = System.IO.Path.GetDirectoryName(s_logPath);
                if (directory is not null && System.IO.Directory.Exists(directory) == false)
                {
                    _ = System.IO.Directory.CreateDirectory(directory);
                }
                var line = $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [tid={System.Environment.CurrentManagedThreadId}] {message}{System.Environment.NewLine}";
                System.IO.File.AppendAllText(s_logPath, line);
            }
        }
        catch
        {
            // Logging must never break shutdown.
        }
    }
}
