// Copyright 2020-2024 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/main/LICENSE
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
using System;
using System.Diagnostics;

namespace Morphic.WindowsNative.SystemRestore;

public static class SystemRestore
{
    public interface IRestorePointEventType
    {
        public record BeginSystemChange : IRestorePointEventType;
        public record EndSystemChange(long SequenceNumber) : IRestorePointEventType;
        //
        public record BeginNestedSystemChange : IRestorePointEventType;
        public record EndNestedSystemChange(long SequenceNumber) : IRestorePointEventType;
    }

    public interface IRestorePointType
    {
        public record ApplicationInstall(string description) : IRestorePointType;
        public record ApplicationUninstall(string description) : IRestorePointType;
        public record CancelledOperation : IRestorePointType;
        public record DeviceDriverInstall(string description) : IRestorePointType;
        public record ModifySettings(string description) : IRestorePointType;
    }

    private delegate Windows.Win32.Foundation.BOOL SetRestorePointDelegate(in Windows.Win32.System.Restore.RESTOREPOINTINFOW pRestorePtSpec, out Windows.Win32.System.Restore.STATEMGRSTATUS pSMgrStatus);

    public interface ICreateRestorePointError
    {
        public record AccessDenied : ICreateRestorePointError; // generally, this means that the current user account does not have permission to create a system restore point
        public record SystemRestoreIsNotSupported : ICreateRestorePointError; // system restore isn't available on the system (such as with some headless/server/miniature systems)
        public record SystemRestoreServiceIsDisabled : ICreateRestorePointError;
        public record OperationFailedWithWin32Error(uint Win32Error) : ICreateRestorePointError;
    }

    // NOTE: recommended descriptions (per Microsoft): (see: https://learn.microsoft.com/en-us/windows/win32/sr/restore-point-description-text)
    //   RestorePointType.ApplicationInstall => "Installed AppName"
    //   RestorePointType.ApplicationUninstall => "Removed AppName"
    //   RestorePointType.CancelledOperation => ???
    //   RestorePointType.ModifySettings => "Configured AppName"
    //   RestorePointType.DeviceDriverInstall => "Installed DriverName"
    //
    // NOTE: this implementation of CreateRestorePoint uses the Win32 API instead of the Windows Management Interface API)
    //
    // NOTE: this function requires COM security to already be initialized properly for the creation of restore points; see https://learn.microsoft.com/en-us/windows/win32/sr/using-system-restore
    //
    // NOTE: this function returns the sequence number as its result
    public static MorphicResult<long, ICreateRestorePointError> CreateRestorePoint(IRestorePointEventType eventType, IRestorePointType restorePointType)
    {
        // validate inputs
        //
        // eventType
        var dwEventType = eventType switch
        {
            IRestorePointEventType.BeginSystemChange => Windows.Win32.System.Restore.RESTOREPOINTINFO_EVENT_TYPE.BEGIN_SYSTEM_CHANGE,
            IRestorePointEventType.EndSystemChange => Windows.Win32.System.Restore.RESTOREPOINTINFO_EVENT_TYPE.END_SYSTEM_CHANGE,
            IRestorePointEventType.BeginNestedSystemChange => Windows.Win32.System.Restore.RESTOREPOINTINFO_EVENT_TYPE.BEGIN_NESTED_SYSTEM_CHANGE,
            IRestorePointEventType.EndNestedSystemChange => Windows.Win32.System.Restore.RESTOREPOINTINFO_EVENT_TYPE.END_NESTED_SYSTEM_CHANGE,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType)),
        };
        //
        // restorePointType
        var dwRestorePointType = restorePointType switch {
            IRestorePointType.ApplicationInstall => Windows.Win32.System.Restore.RESTOREPOINTINFO_TYPE.APPLICATION_INSTALL,
            IRestorePointType.ApplicationUninstall => Windows.Win32.System.Restore.RESTOREPOINTINFO_TYPE.APPLICATION_UNINSTALL,
            IRestorePointType.CancelledOperation => Windows.Win32.System.Restore.RESTOREPOINTINFO_TYPE.CANCELLED_OPERATION,
            IRestorePointType.DeviceDriverInstall=> Windows.Win32.System.Restore.RESTOREPOINTINFO_TYPE.DEVICE_DRIVER_INSTALL,
            IRestorePointType.ModifySettings => Windows.Win32.System.Restore.RESTOREPOINTINFO_TYPE.MODIFY_SETTINGS,
            _ => throw new ArgumentOutOfRangeException(nameof(restorePointType)),
        };
        //
        // sequenceNumber
        long llSequenceNumber = eventType switch
        {
            IRestorePointEventType.BeginNestedSystemChange => 0,
            IRestorePointEventType.BeginSystemChange => 0,
            //
            IRestorePointEventType.EndSystemChange(var sequenceNumber) => sequenceNumber,
            IRestorePointEventType.EndNestedSystemChange(var sequenceNumber) => sequenceNumber,
            //
            _ => throw new ArgumentOutOfRangeException(nameof(eventType)),
        };
        //
        // description
        string? descriptionAsOptionalString = restorePointType switch
        {
            IRestorePointType.ApplicationInstall(string description) => description,
            IRestorePointType.ApplicationUninstall(string description) => description,
            IRestorePointType.CancelledOperation => null,
            IRestorePointType.DeviceDriverInstall(string description) => description,
            IRestorePointType.ModifySettings(string description) => description,
            _ => throw new ArgumentOutOfRangeException(nameof(restorePointType)),
        };

        var restorePointInfo = new Windows.Win32.System.Restore.RESTOREPOINTINFOW()
        {
            dwEventType = dwEventType,
            dwRestorePtType = Windows.Win32.System.Restore.RESTOREPOINTINFO_TYPE.APPLICATION_INSTALL,
            llSequenceNumber = llSequenceNumber,
        };
        //
        // sanity-check: make sure that the szDescription field is the expected size (i.e. that our string will not overflow and also that we are not blocking strings that are not long enough)
        if (descriptionAsOptionalString is not null)
        {
            Debug.Assert(restorePointInfo.szDescription.GetType() == typeof(Windows.Win32.__char_256), "RESTOREPOINTINFOW does not match the previously-documented (expected) size");
            if (descriptionAsOptionalString is not null && descriptionAsOptionalString!.ToCharArray().Length > PInvokeExtensions.MAX_DESC_W /* 256 */ - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(restorePointType), "Description field cannot be greater than (MAX_DESC_W - 1) chars in length");
            }

            // now that we've made sure that the structure and description match the expected size, set the descirption of restorePointInfo
            restorePointInfo.szDescription = descriptionAsOptionalString;
        }

        // NOTE: per the Microsoft documentation, we must dynamically load the SRSetRestorePoint function (i.e. not statically link to it)
        //   "Applications should not call System Restore functions using load-time dynamic linking. Instead, use the LoadLibrary function to load SrClient.dll and GetProcAddress to call the function."
        //   see: https://learn.microsoft.com/en-us/windows/win32/api/srrestoreptapi/nf-srrestoreptapi-srsetrestorepointw
        //
        var loadLibraryFunctionResult = Morphic.WindowsNative.DynamicLibrary.DynamicLibrary.LoadFunction<SetRestorePointDelegate>("srclient.dll", "SRSetRestorePointW");
        if (loadLibraryFunctionResult.IsError == true)
        {
            switch (loadLibraryFunctionResult.Error!)
            {
                case DynamicLibrary.DynamicLibrary.LoadFunctionError.LibraryNotFound:
                    // could not find system restore library
                    return MorphicResult.ErrorResult<ICreateRestorePointError>(new ICreateRestorePointError.SystemRestoreIsNotSupported());
                case DynamicLibrary.DynamicLibrary.LoadFunctionError.FunctionNotFound:
                    // system restore library does not include SRSetRestorePointW function
                    Debug.Assert(false, "Found system restore library, but the SRSetRestorePointW function is missing");
                    return MorphicResult.ErrorResult<ICreateRestorePointError>(new ICreateRestorePointError.SystemRestoreIsNotSupported());
                default:
                    throw new Morphic.Core.MorphicUnhandledErrorException();
            }
        }
        var setRestorePointFunc = loadLibraryFunctionResult.Value!;

        // call SRSetRestorePoint
        var setRestorePointResult = setRestorePointFunc(restorePointInfo, out var stateMgrStatus);
        if (setRestorePointResult.Value == 0)
        {
            // could not set restore point; see the pSMgrStatus.nStatus variable for the actual error
			// NOTE: we may want to add additional ICreateRestorePointError members, as appropriate
            var win32Error = stateMgrStatus.nStatus;
            switch (win32Error)
            {
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_ACCESS_DENIED:
                    return MorphicResult.ErrorResult<ICreateRestorePointError>(new ICreateRestorePointError.AccessDenied());
                case Windows.Win32.Foundation.WIN32_ERROR.ERROR_SERVICE_DISABLED:
                    return MorphicResult.ErrorResult<ICreateRestorePointError>(new ICreateRestorePointError.SystemRestoreServiceIsDisabled());
                default:
                    var win32ErrorAsUInt = (uint)win32Error;
                    return MorphicResult.ErrorResult<ICreateRestorePointError>(new ICreateRestorePointError.OperationFailedWithWin32Error(win32ErrorAsUInt));
            }
        }

        long sequenceNumberResult = stateMgrStatus.llSequenceNumber;
        return MorphicResult.OkResult(sequenceNumberResult);
    }

    //

    public static MorphicResult<uint?, MorphicUnit> GetRestorePointCreationFrequency()
    {
        var openRegistryKeyResult = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", false);
        if (openRegistryKeyResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var registryKey = openRegistryKeyResult.Value!;

        var getValueResult = registryKey.GetValueDataOrNull<uint>("SystemRestorePointCreationFrequency");
        if (getValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        uint? systemRestorePointCreationFrequency = getValueResult.Value;

        return MorphicResult.OkResult(systemRestorePointCreationFrequency);
    }

    public static MorphicResult<MorphicUnit, MorphicUnit> SetRestorePointCreationFrequency(uint restorePointCreationFrequency)
    {
        var openRegistryKeyResult = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", false);
        if (openRegistryKeyResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var registryKey = openRegistryKeyResult.Value!;

        var setValueResult = registryKey.SetValue<uint>("SystemRestorePointCreationFrequency", restorePointCreationFrequency);
        if (setValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }

    public static MorphicResult<MorphicUnit, MorphicUnit> ClearRestorePointCreationFrequency(uint restorePointCreationFrequency)
    {
        var openRegistryKeyResult = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", false);
        if (openRegistryKeyResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }
        var registryKey = openRegistryKeyResult.Value!;

        var deleteValueResult = registryKey.DeleteValue("SystemRestorePointCreationFrequency");
        if (deleteValueResult.IsError == true)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult();
    }
}
