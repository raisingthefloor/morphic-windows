using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Morphic.Windows.Native.WindowsCom
{
    public static class Appx
    {
        /// <summary>
        /// Starts a Windows Store app.
        /// </summary>
        /// <param name="appUserModelId">
        /// Application User Model ID of the app.
        /// For example, `Microsoft.WindowsCalculator_8wekyb3d8bbwe!App`
        /// </param>
        /// <returns>The pid (or -1 on failure)</returns>
        public static int Start(string appUserModelId)
        {
            IntPtr result =
                new ApplicationActivationManager().ActivateApplication(appUserModelId, null, 0, out uint pid);
            if (result != IntPtr.Zero)
            {
                return -1;
            }

            return (int) pid;
        }
    }

    #region COM

    // The COM interface and class for IApplicationActivationManager
    // See https://docs.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateapplication

    /// <summary>
    /// Provides methods which activate Windows Store apps for the Launch, File, and Protocol extensions. You will
    /// normally use this interface in debuggers and design tools.
    /// https://docs.microsoft.com/en-gb/windows/win32/api/shobjidl_core/nn-shobjidl_core-iapplicationactivationmanager
    /// </summary>
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IApplicationActivationManager
    {
        /// <summary>
        /// Activates the specified Windows Store app for the generic launch contract (Windows.Launch) in the current session.
        /// https://docs.microsoft.com/en-gb/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateapplication
        /// </summary>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        IntPtr ActivateApplication(string appUserModelId, string arguments, uint options, out uint processId);

        /// <summary>
        /// Activates the specified Windows Store app for the file contract (Windows.File).
        /// https://docs.microsoft.com/en-gb/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateforfile
        /// </summary>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        IntPtr ActivateForFile(string appUserModelId, IntPtr itemArray, string verb, out uint processId);

        /// <summary>
        /// Activates the specified Windows Store app for the protocol contract (Windows.Protocol).
        /// https://docs.microsoft.com/en-gb/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateforprotocol
        /// </summary>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        IntPtr ActivateForProtocol(string appUserModelId, IntPtr itemArray, out uint processId);
    }

    /// <summary>
    /// The implementation of <see cref="IApplicationActivationManager"/>.
    /// https://docs.microsoft.com/en-gb/windows/win32/api/shobjidl_core/nn-shobjidl_core-iapplicationactivationmanager
    /// </summary>
    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    internal class ApplicationActivationManager : IApplicationActivationManager
    {
        /// <summary>
        /// Activates the specified Windows Store app for the generic launch contract (Windows.Launch) in the current session.
        /// https://docs.microsoft.com/en-gb/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateapplication
        /// </summary>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern IntPtr ActivateApplication(string appUserModelId, string? arguments, uint options,
            out uint processId);

        /// <summary>
        /// Activates the specified Windows Store app for the file contract (Windows.File).
        /// https://docs.microsoft.com/en-gb/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateforfile
        /// </summary>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern IntPtr ActivateForFile(string appUserModelId, IntPtr itemArray, string verb, out uint processId);

        /// <summary>
        /// Activates the specified Windows Store app for the protocol contract (Windows.Protocol).
        /// https://docs.microsoft.com/en-gb/windows/win32/api/shobjidl_core/nf-shobjidl_core-iapplicationactivationmanager-activateforprotocol
        /// </summary>
        /// <returns>If this method succeeds, it returns S_OK. Otherwise, it returns an HRESULT error code.</returns>
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern IntPtr ActivateForProtocol(string appUserModelId, IntPtr itemArray, out uint processId);
    }

    #endregion

}