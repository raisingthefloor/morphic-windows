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
        var line = $"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [tid={System.Environment.CurrentManagedThreadId}] {message}";

        // Echo to the debugger output (VS Output window) so trace points are easy to see in the dev
        // environment, then append to the on-disk log (which also captures non-debugger runs).
        System.Diagnostics.Debug.WriteLine(line);

        try
        {
            lock (s_lockObject)
            {
                var directory = System.IO.Path.GetDirectoryName(s_logPath);
                if (directory is not null && System.IO.Directory.Exists(directory) == false)
                {
                    _ = System.IO.Directory.CreateDirectory(directory);
                }
                System.IO.File.AppendAllText(s_logPath, line + System.Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never break shutdown.
        }
    }
}
