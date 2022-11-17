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

using Morphic.Core;
using Morphic.WindowsNative;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        // NOTE: in our Ghidra analysis, the third parameter (IntPtr n) does not appear in the function signature; we should test this function without the third parameter in the declaration
        // NOTE: in .NET 5+, we cannot pass settingId as an HString via P/Invoke; instead, we would need to pass it as an IntPtr and create an HSTRING class to create the string pointer beforehand,
        //       call the function in a try block and then delete the HSTRING in the try...finally constructs's finally block; see: https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate IntPtr GetSettingFunction([MarshalAs(UnmanagedType.HString)] string settingId, out ISettingItem settingItem, IntPtr n);

        public ISettingItem? GetSetting(string id)
        {
            // get the path to the setting DLL associated with this setting id
            var getSettingDllPathResult = SettingsDatabase.GetSettingDllPath(id);
            if (getSettingDllPathResult.IsError == true)
            {
                switch (getSettingDllPathResult.Error!.Value)
                {
                    case GetSettingDllPathError.Values.RegistryValueIsInvalid:
                        return null;
                    case GetSettingDllPathError.Values.Win32Error:
						// NOTE: we are unsure whether the WinRT class throws exceptions; it might be more appropriate to simply return null here
                        throw new System.ComponentModel.Win32Exception((int)getSettingDllPathResult.Error!.Win32ErrorCode!);
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var settingDllPath = getSettingDllPathResult.Value;

            // if the setting doesn't exist, return null
            if (settingDllPath is null)
            {
                return null;
            }

            // get a reference to the WinRT ISettingItem instance used to manage the setting
            var getSettingItemResult = SettingsDatabase.GetSettingItemWinrtObject(settingDllPath!, id);
            if (getSettingItemResult.IsError == true)
            {
				// NOTE: we are unsure whether the WinRT class throws exceptions; it might be more appropriate to simply return null here
                // NOTE: in the future, we may want to throw more specific returns to clarify the exception
                throw new Exception("Setting found, but an instance could not be instantiated.");
            }
            var settingItem = getSettingItemResult.Value!;

            return settingItem;
        }

        //

        private record GetSettingDllPathError : MorphicAssociatedValueEnum<GetSettingDllPathError.Values>
        {
            // enum members
            public enum Values
            {
                RegistryValueIsInvalid,
                Win32Error/*(int win32ErrorCode)*/
            }

            // functions to create member instances
            public static GetSettingDllPathError RegistryValueIsInvalid => new(Values.RegistryValueIsInvalid);
            public static GetSettingDllPathError Win32Error(uint win32ErrorCode) => new(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public uint? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private GetSettingDllPathError(Values value) : base(value) { }
        }
        //
        private static MorphicResult<string?, GetSettingDllPathError> GetSettingDllPath(string settingId)
        {
            if (settingId == String.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(settingId));
            }

            // open the base setting id registry key
            var openBaseRegistryKeyResult = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\SystemSettings\SettingId");
            if (openBaseRegistryKeyResult.IsError == true)
            {
                switch (openBaseRegistryKeyResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        return MorphicResult.ErrorResult(GetSettingDllPathError.Win32Error(openBaseRegistryKeyResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var baseRegistryKey = openBaseRegistryKeyResult.Value!;
            //
            // determine if the subkey exists; if it does not then return a null string (as a success condition)
            var getSettingIdSubKeyExistsResult = baseRegistryKey.SubKeyExists(settingId);
            if (getSettingIdSubKeyExistsResult.IsError == true)
            {
                switch (openBaseRegistryKeyResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        return MorphicResult.ErrorResult(GetSettingDllPathError.Win32Error(openBaseRegistryKeyResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var settingIdSubKeyExists = getSettingIdSubKeyExistsResult.Value!;
            if (settingIdSubKeyExists == false)
            {
                return MorphicResult.OkResult<string?>(null);
            }
            //
            // open the specific setting id's subkey
            var openSettingIdKeyResult = baseRegistryKey.OpenSubKey(settingId);
            if (openSettingIdKeyResult.IsError == true)
            {
                switch (openSettingIdKeyResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        return MorphicResult.ErrorResult(GetSettingDllPathError.Win32Error(openBaseRegistryKeyResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var settingIdRegistrySubKey = openSettingIdKeyResult.Value!;
            //
            // get the dll path for the specified setting id from its "DllPath" value's string data
            var getDllPathValueDataResult = settingIdRegistrySubKey.GetValueData<string>("DllPath");
            if (getDllPathValueDataResult.IsError == true)
            {
                switch (getDllPathValueDataResult.Error!.Value)
                {
                    case Registry.RegistryKey.RegistryGetValueError.Values.Win32Error:
                        return MorphicResult.ErrorResult(GetSettingDllPathError.Win32Error(openBaseRegistryKeyResult.Error!.Win32ErrorCode!.Value));
                    case Registry.RegistryKey.RegistryGetValueError.Values.ValueDoesNotExist:
                        // if the setting's DLL entry does not exist, return null
                        return MorphicResult.OkResult<string?>(null);
                    case Registry.RegistryKey.RegistryGetValueError.Values.TypeMismatch:
                        return MorphicResult.ErrorResult(GetSettingDllPathError.RegistryValueIsInvalid);
                    case Registry.RegistryKey.RegistryGetValueError.Values.UnsupportedType:
                    default:
                        throw new MorphicUnhandledErrorException();
                }
            }
            var dllPath = getDllPathValueDataResult.Value!;

            return MorphicResult.OkResult<string?>(dllPath);
        }

        private static MorphicResult<ISettingItem, MorphicUnit> GetSettingItemWinrtObject(string dllPath, string settingId)
        {
            // sanity-check: only allow dllPaths that are located in %SYSTEM32%
            var directoryName = System.IO.Path.GetDirectoryName(dllPath);
            if (String.Equals(directoryName, Environment.SystemDirectory, StringComparison.InvariantCultureIgnoreCase) == false)
            {
                Debug.Assert(false, "dllPath points to dll outside of %SYSTEM32%; this may be safe, but we have blocked it by default out of an abundance of caution.");
                return MorphicResult.ErrorResult();
            }

            // NOTE: we do not use the PInvoke.Kernel32 NuGet package's LoadLibrary and GetProcAddress functions as they led to AppEngine faults; instead we declare the P/Invokes ourselves
            var dllHandle = Morphic.WindowsNative.ExtendedPInvoke.LoadLibrary(dllPath);
            if (dllHandle == IntPtr.Zero)
            {
                return MorphicResult.ErrorResult();
            }

            // get the address of the "GetSetting" function inside the DLL; this function should exist in each System Setting library
            // NOTE: we do not use the PInvoke.Kernel32 NuGet package's LoadLibrary and GetProcAddress functions as they led to AppEngine faults; instead we declare the P/Invokes ourselves
            var pointerToGetSettingFunction = Morphic.WindowsNative.ExtendedPInvoke.GetProcAddress(dllHandle, "GetSetting");
            if (pointerToGetSettingFunction == IntPtr.Zero)
            {
                return MorphicResult.ErrorResult();
            }

            // create a managed delegate using the unmanaged GetSetting function pointer
            // NOTE: in .NET 5+, we cannot use a RCW for a WinRT object like we do here; instead, we either need to:
            //       1. Modify the GetSettingFunction delegate to return an IntPtr instead of an ISettingItem, and then modify the ABI.SystemSettings.DataModel.ISettingItemMethods methods to take
            //          the IntPtr instead of an object (or cast the IntPtr to a WinRT IObjectReference)--which we would then call directly, passing in the ISettingItem instance/pointer
            //       or
            //       2. Change the function delegate signature to indicate that the resulting item should be marshalled as a WinRT object (if that's an option); otherwise, we'd just get back a
            //          COM object representation and the .NET CLR will want to skip the custom ABI/vtable code generated by CsWinRT (as seen when testing under .NET 6.0) and the function call
            //          order, types, lack of HSTRING marshalling, and lack of IInspectable support in .NET 5+ would trip us up.
            //       or
            //       3. Use the SettingsDatabase WinRT class to obtain the ISettingItem instances directly (bypassing all of the registry and DLL/function load logic we use here)
            //       [.NET 5+ have removed the IterfaceIsIInspectable ComInterfaceType, HSTRING interop, etc.]
            //       see: https://learn.microsoft.com/en-us/dotnet/core/compatibility/interop/5.0/casting-rcw-to-inspectable-interface-throws-exception
            GetSettingFunction? getSettingFunction = Marshal.GetDelegateForFunctionPointer<GetSettingFunction>(pointerToGetSettingFunction);
            if (getSettingFunction is null)
            {
                return MorphicResult.ErrorResult();
            }

            // call the GetSetting function, supplying it with the setting ID, to get the WinRT interface used for the specific system setting; this will be represented via an ISettingItem interface instance
            SystemSettings.DataModel.ISettingItem? settingItem = null;

            var getSettingResult = getSettingFunction!(settingId, out settingItem, IntPtr.Zero /* unknown extra parameter */);
            if (getSettingResult != IntPtr.Zero /* assume this is success */)
            {
                Debug.Assert(false, "Loaded DLL for system setting, but unable to instantiate COM object for setting");
                return MorphicResult.ErrorResult();
            }
            if (settingItem is null)
            {
                // sanity check (which we are doing because the function is undocumented): make sure that we actually got back a result
                Debug.Assert(false, "Loaded DLL for system setting and got the COM object for setting, but the COM object reference is null");
                return MorphicResult.ErrorResult();
            }

            return MorphicResult.OkResult(settingItem!);
        }
    }
}
