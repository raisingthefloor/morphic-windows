using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Morphic;
using System;
using System.IO;
using System.Threading;

namespace Morphic;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        try
        {
            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
        catch (Exception ex)
        {
            File.WriteAllText(@"C:\temp\startup-crash.log", ex.ToString());
            throw;
        }
    }
}