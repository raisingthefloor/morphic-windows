using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native.Windowing
{
    public class SafeNativeWindowHandle : SafeHandle
    {
        // NOTE: we capture the context in which a handle was created (or passed to us) so that we destroy the handle on the same thread
        private readonly TaskScheduler TaskScheduler;

        public SafeNativeWindowHandle(IntPtr preexistingHandle, bool ownsHandle) : base(IntPtr.Zero, ownsHandle)
        {
            base.handle = preexistingHandle;

            this.TaskScheduler = System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext();
        }

        protected override bool ReleaseHandle()
        {
            // NOTE: according to Microsfot's documentation, we must obey all rules for constrained execution regions in this block of code.
            // see: https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.safehandle?view=net-6.0

            // NOTE: we pass along the result to our caller, but we don't throw an exception here; as Microsoft notes in their sample, most code could not recover from an exception thrown here
            // NOTE: there is some risk of thread contention here (i.e. if running this code on the UI thread doesn't return); to gracefully degrade, we set a timeout and return false after an initial waiting period if the call didn't complete quickly enough
            // NOTE: the following degradation mechanism has not been tested at the time of writing; it is theoretically possible that DestroyWindow might block the call indefinitely (in which case we need to develop another strategy)
            var syncTask = new Task<bool>(() => {
                return PInvoke.User32.DestroyWindow(base.handle);
            });
            try
            {
                var waitTask = new Task<bool>(() =>
                {
                    syncTask.RunSynchronously(this.TaskScheduler);
                    return syncTask.Result;
                });
                // wait up to 100ms for the call to complete
                var waitTimeout = new TimeSpan(0, 0, 0, 0, 100);
                var waitResult = waitTask.Wait(waitTimeout);
                if (waitResult == false)
                {
                    // timeout
                    return false;
                }
                else
                {
                    // DestroyHandle call completed within the timeout period
                    return waitTask.Result;
                }
            }
            catch
            {
                // swallow any exceptions; just return false (failure) instead
                return false;
            }
        }

        public override bool IsInvalid => base.handle == IntPtr.Zero;
    }
}
