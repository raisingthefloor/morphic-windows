// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SystemSettings.DataModel
{
    public interface ISettingsDatabase
    {
        ISettingItem? GetSetting(string id);
    }

    public sealed class SettingsDatabase : ISettingsDatabase
    {
        public ISettingItem? GetSetting(string id)
        {
            string? dllPath = this.GetSettingDll(id);
            if (dllPath is null)
            {
                return null;
            }

            var settingItem = this.GetSettingItem(id, dllPath);
            return settingItem;
        }

        //

        /// <summary>Location of the setting definitions in the registry.</summary>
        internal const string RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\SystemSettings\SettingId";

        /// <summary>The name of the GetSetting export.</summary>
        private const string GetSettingExport = "GetSetting";

        /// <see cref="https://msdn.microsoft.com/library/ms684175.aspx"/>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        /// <see cref="https://msdn.microsoft.com/library/ms683212.aspx"/>
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        /// <summary>Points to a GetSetting export.</summary>
        /// <param name="settingId">Setting ID</param>
        /// <param name="settingItem">Returns the instance.</param>
        /// <param name="n">Unknown.</param>
        /// <returns>Zero on success.</returns>
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr GetSettingFunc(
            [MarshalAs(UnmanagedType.HString)] string settingId,
            out ISettingItem settingItem,
            IntPtr n);

        //

        /// <summary>Gets the DLL file that contains the class for the setting.</summary>
        /// <param name="settingId">The setting ID.</param>
        /// <returns>The path of the DLL file containing the setting class, null if the setting doesn't exist.</returns>
        private string? GetSettingDll(string settingId)
        {
            object? value = null;
            if (!string.IsNullOrEmpty(settingId))
            {
                string path = Path.Combine(RegistryPath, settingId);
                value = Microsoft.Win32.Registry.GetValue(path, "DllPath", null);
            }

            return value is null ? null : value.ToString();
        }

        /// <summary>Get an instance of ISettingItem for the given setting.</summary>
        /// <param name="settingId">The setting.</param>
        /// <param name="dllPath">The dll containing the class.</param>
        /// <returns>An ISettingItem instance for the setting.</returns>
        private ISettingItem? GetSettingItem(string settingId, string dllPath)
        {
            // Load the dll.
            IntPtr lib = LoadLibrary(dllPath);
            if (lib == IntPtr.Zero)
            {
                return null;
                //throw new SettingFailedException("Unable to load library " + dllPath, true);
            }

            // Get the address of the function within the dll.
            IntPtr proc = GetProcAddress(lib, GetSettingExport);
            if (proc == IntPtr.Zero)
            {
                return null;
                //throw new SettingFailedException(
                //    string.Format("Unable get address of {0}!{1}", dllPath, GetSettingExport), true);
            }

            // Create a function from the address.
            GetSettingFunc getSetting = Marshal.GetDelegateForFunctionPointer<GetSettingFunc>(proc);

            // Call it.
            ISettingItem item;
            IntPtr result = getSetting(settingId, out item, IntPtr.Zero);
            if (result != IntPtr.Zero || item is null)
            {
                return null;
                //throw new SettingFailedException("Unable to instantiate setting class", true);
            }
            //item.SettingChanged += this.SettingItem_SettingChanged;

            return item;
        }


    }
}
