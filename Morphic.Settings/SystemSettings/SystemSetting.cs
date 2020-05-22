// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Win32;
using System.Threading;

namespace Morphic.Settings.SystemSettings
{
    /// <summary>
    /// Implementation of Window System Settings
    /// </summary>
    /// <remarks>
    /// Information about System Settings can be found in the Windows Registry under
    /// 
    /// HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\SystemSettings\SettingId\SomeSettingId
    /// 
    /// Each subkey has an value for DllPath, which contains a string of the absolute path to a DLL
    /// that in turn contains a GetSetting() function.
    /// 
    /// The result of calling GetSetting("SomeSettingId") is an object that has GetValue() and SetValue() methods,
    /// which read and write the setting, respectively.
    /// </remarks>
    class SystemSetting: ISystemSetting
    {

        public string Id { get; private set; }

        public SettingType SettingType
        {
            get
            {
                return settingItem?.Type ?? SettingType.Custom;
            }
        }

        /// <summary>
        /// The registry key name for this setting's information
        /// </summary>
        private string registryKeyName;

        /// <summary>
        /// The item that can read and write the setting
        /// </summary>
        private ISettingItem? settingItem;

        private SettingNotFoundException? settingLoadException;

        /// <summary>
        /// Create a new system settings handler with the given description and logger
        /// </summary>
        /// <param name="description"></param>
        /// <param name="logger"></param>
        public SystemSetting(string id, ILogger<SystemSetting> logger)
        {
            Id = id;
            registryKeyName = $"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\SystemSettings\\SettingId\\{Id}";
            this.logger = logger;
            try
            {
                settingItem = LoadSettingItem();
            }
            catch (SettingNotFoundException e)
            {
                settingLoadException = e;
            }
        }

        /// <summary>
        /// The logger to use
        /// </summary>
        private readonly ILogger<SystemSetting> logger;

        /// <summary>
        /// A global cache of pointers to loaded dll's, so each dll only gets loaded once and then resued
        /// </summary>
        private static Dictionary<string, IntPtr> loadedLibraries = new Dictionary<string, IntPtr>();

        /// <summary>
        /// Load the setting item by inspecting the DLL referenced in the registry key for this setting
        /// </summary>
        /// <returns></returns>
        private ISettingItem? LoadSettingItem()
        {
            var dll = DllPath;
            if (dll == null)
            {
                throw new SettingNotFoundException(String.Format("Failed to get value for DllPath for {0}", Id));
            }

            if (!loadedLibraries.TryGetValue(dll, out var libraryPointer))
            {
                libraryPointer = LoadLibrary(dll);
                if (libraryPointer == IntPtr.Zero)
                {
                    throw new SettingNotFoundException(String.Format("Failed to load DLL for {0}", Id));
                }

                loadedLibraries.Add(dll, libraryPointer);
            }

            var functionPointer = GetProcAddress(libraryPointer, "GetSetting");
            if (functionPointer == IntPtr.Zero)
            {
                throw new SettingNotFoundException(String.Format("Failed to location GetSetting function in library for {0}", Id));
            }

            var function = Marshal.GetDelegateForFunctionPointer<GetSetting>(functionPointer);

            if (function(Id, out var item, IntPtr.Zero) != IntPtr.Zero)
            {
                throw new SettingNotFoundException(String.Format("GetSetting failed for {0}", Id));
            }

            return item;
        }

        /// <summary>
        /// Apply the value to the setting
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task SetValue(object value)
        {
            if (settingItem is ISettingItem item)
            {
                var result = item.SetValue("Value", value);
                if (result != 0)
                {
                    throw new Exception(String.Format("SetValue returned non-0 for {0}", Id));
                }
                var updateResult = await item.WaitForUpdate(30);
                logger.LogInformation("setting took {0}ms to apply for {1}", updateResult.Item2, Id);
                if (!updateResult.Item1)
                {
                    throw new Exception(String.Format("SetValue timed out waiting for update for {0}", Id));

                }
            }
            else
            {
                throw settingLoadException!;
            }
        }

        public Task<object?> GetValue()
        {
            if (settingItem is ISettingItem item)
            {
                var value = item.GetValue("Value");
                return Task.FromResult<object?>(value);
            }
            else
            {
                throw settingLoadException!;
            }
        }

        /// <summary>
        /// The value of the DllPath registry value for this setting
        /// </summary>
        private string? DllPath
        {
            get
            {
                return GetRegistryString("DllPath");
            }
        }

        /// <summary>
        /// Get a registry value for this setting as a string
        /// </summary>
        /// <param name="valueName"></param>
        /// <returns></returns>
        private string? GetRegistryString(string valueName)
        {
            try
            {
                var value = Microsoft.Win32.Registry.GetValue(registryKeyName, valueName, RegistryValueKind.String);
                return value as string;
            }
            catch (Exception)
            {
                throw new SettingNotFoundException(String.Format("Failed to read registry value for {0}.{1}", Id, valueName));
            }
        }

        public class SettingNotFoundException: Exception
        {
            public SettingNotFoundException(string message): base(message)
            {
            }
        }


        /// <summary>Points to a GetSetting export.</summary>
        /// <param name="settingId">Setting ID</param>
        /// <param name="settingItem">Returns the instance.</param>
        /// <param name="n">Unknown.</param>
        /// <returns>Zero on success.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr GetSetting([MarshalAs(UnmanagedType.HString)] string settingId, out ISettingItem settingItem, IntPtr n);

        /// <see cref="https://msdn.microsoft.com/library/ms684175.aspx"/>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        /// <see cref="https://msdn.microsoft.com/library/ms683212.aspx"/>
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    }

    /// <summary>
    /// Interface for the settings classes, instantiated by the GetSettings exports.
    /// </summary>
    /// <remarks>
    /// Most of the information was taken from the debug symbols (PDB) for the relevent DLLs. The symbols
    /// don't describe the interface, just the classes that implement it (the "vtable"). This contains the
    /// method names (and order), and vague information on the parameters (no names, and, er, de-macro'd types).
    ///
    /// Visual Studio was used to obtain the names by first creating a method with any name, then stepping into the
    /// native code from the call with the debugger where the function name will be displayed in the disassembled code.
    ///
    /// The binding of methods isn't by name, but by order, which is why the "unknown" methods must remain.
    /// Not all methods work for some type of setting.
    /// </remarks>
    [ComImport, Guid("40C037CC-D8BF-489E-8697-D66BAA3221BF"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    public interface ISettingItem
    {
        int Id { get; }
        SettingType Type { get; }
        bool IsSetByGroupPolicy { get; }
        bool IsEnabled { get; }
        bool IsApplicable { get; }

        // Not always available, sometimes looks like a resource ID
        string Description
        {
            [return: MarshalAs(UnmanagedType.HString)]
            get;
        }

        // Unknown
        bool IsUpdating { get; }

        // For Type = Boolean, List, Range, String
        [return: MarshalAs(UnmanagedType.IInspectable)]
        object GetValue(
            // Normally "Value"
            [MarshalAs(UnmanagedType.HString)] string name);

        int SetValue(
            // Normally "Value"
            [MarshalAs(UnmanagedType.HString)] string name,
            [MarshalAs(UnmanagedType.IInspectable)] object value);

        // Unknown usage
        int GetProperty(string name);
        int SetProperty(string name, object value);

        // For Type = Action - performs the action.
        IntPtr Invoke(IntPtr a, Rect b);

        // SettingChanged event
        event EventHandler<string> SettingChanged;

        // Unknown - setter for IsUpdating
        bool IsUpdating2 { set; }

        // Unknown
        int GetInitializationResult();
        int DoGenericAsyncWork();
        int StartGenericAsyncWork();
        int SetSkipConcurrentOperations(bool flag);

        // These appear to be base implementations overridden by the above.
        bool GetValue2 { get; }
        IntPtr unknown_SetValue1();
        IntPtr unknown_SetValue2();
        IntPtr unknown_SetValue3();

        // Unknown usage
        IntPtr GetNamedValue(
            [MarshalAs(UnmanagedType.HString)] string name
            //[MarshalAs(UnmanagedType.IInspectable)] object unknown
            );

        IntPtr SetNullValue();

        // For Type=List:
        IntPtr GetPossibleValues(out IList<object> value);

        // There are more unknown methods.
    }

    public static class ISettingItemExtensions
    {
        /// <summary>
        /// Wait for <code>IsUpdating</code> to be false
        /// </summary>
        /// <param name="item"></param>
        /// <param name="timeoutInSeconds">The longeset time to wait for</param>
        /// <returns>Whether the item completed updated within the allotted time</returns>
        public static Task<(bool, long)> WaitForUpdate(this ISettingItem item, int timeoutInSeconds)
        {
            var task = new Task<(bool, long)>(() =>
            {
                var timeoutInMilliseconds = timeoutInSeconds * 1000;
                var watch = new Stopwatch();
                watch.Start();
                while (item.IsUpdating && watch.ElapsedMilliseconds < timeoutInMilliseconds)
                {
                    // Busy wait for the first 50ms and then sleep for 50ms intervals
                    if (watch.ElapsedMilliseconds > 50)
                    {
                        Thread.Sleep(50);
                    }
                }
                return (!item.IsUpdating, watch.ElapsedMilliseconds);
            });
            task.Start();
            return task;
        }
    }

    /// <summary>The type of setting.</summary>
    public enum SettingType
    {
        // Needs investigating
        Custom = 0,

        // Read-only
        DisplayString = 1,
        LabeledString = 2,

        // Values (use GetValue/SetValue)
        Boolean = 3,
        Range = 4,
        String = 5,
        List = 6,

        // Performs an action
        Action = 7,

        // Needs investigating
        SettingCollection = 8,
    }

    /// <summary>
    /// Used by ISettingsItem.Invoke (reason unknown).
    /// </summary>
    public struct Rect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
    }
}
