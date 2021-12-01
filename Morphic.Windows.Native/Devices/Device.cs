// Copyright 2021 Raising the Floor - International
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
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Morphic.Windows.Native.Devices
{
    public class Device
    {
        private PInvoke.SetupApi.SafeDeviceInfoSetHandle DeviceInfoSetHandle;
        public string DeviceInstanceId { get; private init; }
        // NOTE: in the future, if we want to save the devicePaths for all nodes (including parents/children), we should change DevicePath to be non-optional
        public string? DevicePath { get; private init; }
        internal PInvoke.SetupApi.SP_DEVINFO_DATA DeviceInfoData { get; private init; }
        public bool IsRemovable => (this.Capabilities & ExtendedPInvoke.CmDeviceCapabilitiesFlags.Removable) != 0;
        //
        private ExtendedPInvoke.CmDeviceCapabilitiesFlags Capabilities;

        private Device(PInvoke.SetupApi.SafeDeviceInfoSetHandle deviceInfoSetHandle, PInvoke.SetupApi.SP_DEVINFO_DATA deviceInfoData, string? devicePath, string deviceInstanceId, ExtendedPInvoke.CmDeviceCapabilitiesFlags capabilities)
        {
            // NOTE: deviceInfoSetHandle is a SafeHandle variant; it is shared widely and will automatically be cleaned up by .NET once the final device gets deallocated
            this.DeviceInfoSetHandle = deviceInfoSetHandle;
            this.DeviceInfoData = deviceInfoData;
            this.DevicePath = devicePath;
            this.DeviceInstanceId = deviceInstanceId;
            this.Capabilities = capabilities;
        }

        public record GetParentOrChildError : MorphicAssociatedValueEnum<GetParentOrChildError.Values>
        {
            // enum members
            public enum Values
            {
                ConfigManagerError/*(uint configManagerErrorCode)*/,
                CouldNotGetDeviceCapabilities,
                Win32Error/*(int win32ErrorCode)*/
            }

            // functions to create member instances
            public static GetParentOrChildError ConfigManagerError(uint configManagerErrorCode) => new GetParentOrChildError(Values.ConfigManagerError) { ConfigManagerErrorCode = configManagerErrorCode };
            public static GetParentOrChildError CouldNotGetDeviceCapabilities => new GetParentOrChildError(Values.CouldNotGetDeviceCapabilities);
            public static GetParentOrChildError Win32Error(int win32ErrorCode) => new GetParentOrChildError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public uint? ConfigManagerErrorCode { get; private set; }
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private GetParentOrChildError(Values value) : base(value) { }
        }
        //
        public MorphicResult<Device?, GetParentOrChildError> GetParent()
        {
            // see: https://docs.microsoft.com/en-us/windows-hardware/drivers/install/obtaining-the-parent-of-a-device-in-the-device-tree

            // get the parent DevInst
            uint parentDevInst;
            var getParentResult = ExtendedPInvoke.CM_Get_Parent(out parentDevInst, this.DeviceInfoData.DevInst, 0 /* must be zero */);
            if (getParentResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
            {
                switch (getParentResult)
                {
                    case (uint)ExtendedPInvoke.CR_RESULT.CR_NO_SUCH_DEVNODE:
                        // device doesn't have a parent
                        return MorphicResult.OkResult<Device?>(null);
                    default:
                        return MorphicResult.ErrorResult(GetParentOrChildError.ConfigManagerError(getParentResult));
                }
            }

            // use the parent DevInst to get a device instance id
            int deviceInstanceIdLength;
            var getDeviceIdSizeResult = ExtendedPInvoke.CM_Get_Device_ID_Size(out deviceInstanceIdLength, parentDevInst, 0 /* must be zero */);
            if (getDeviceIdSizeResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
            {
                return MorphicResult.ErrorResult(GetParentOrChildError.ConfigManagerError(getDeviceIdSizeResult));
            }
            if (deviceInstanceIdLength == 0)
            {
                // device does not exist
                return MorphicResult.OkResult<Device?>(null);
            }
            //
            // add one to the length (because the previous function returns the length _without_ the null-terminator character)
            deviceInstanceIdLength += 1; // +1 char for null-terminator character
            // NOTE: the length is in chars, so when we allocate the actual memory to receive the instance id we need to multiply that by the size of a character (e.g. x2)
            var pointerToParentDeviceInstanceId = Marshal.AllocHGlobal(deviceInstanceIdLength * sizeof(char));
            string parentDeviceInstanceId;
            try
            {
                var getDeviceIdResult = ExtendedPInvoke.CM_Get_Device_ID(parentDevInst, pointerToParentDeviceInstanceId, deviceInstanceIdLength, 0);
                if (getDeviceIdResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
                {
                    return MorphicResult.ErrorResult(GetParentOrChildError.ConfigManagerError(getDeviceIdResult));
                }

                parentDeviceInstanceId = Marshal.PtrToStringUni(pointerToParentDeviceInstanceId)!;
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToParentDeviceInstanceId);
            }

            // get the devinfo data of the parent
            var parentDeviceInfoData = PInvoke.SetupApi.SP_DEVINFO_DATA.Create();
            var pointerToParentDeviceInfoData = Marshal.AllocHGlobal(Marshal.SizeOf<PInvoke.SetupApi.SP_DEVINFO_DATA>());
            Marshal.StructureToPtr(parentDeviceInfoData, pointerToParentDeviceInfoData, false);
            try
            {
                var setupDiOpenDeviceInfoSuccess = PInvoke.SetupApi.SetupDiOpenDeviceInfo(this.DeviceInfoSetHandle, parentDeviceInstanceId, IntPtr.Zero, PInvoke.SetupApi.SetupDiOpenDeviceInfoFlags.None, pointerToParentDeviceInfoData);
                if (setupDiOpenDeviceInfoSuccess == false)
                {
                    var win32ErrorCode = Marshal.GetLastWin32Error();
                    return MorphicResult.ErrorResult(GetParentOrChildError.Win32Error(win32ErrorCode));
                }

                parentDeviceInfoData = Marshal.PtrToStructure<PInvoke.SetupApi.SP_DEVINFO_DATA>(pointerToParentDeviceInfoData);
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToParentDeviceInfoData);
            }

            // get the capabilities of the device
            var getPropertyResult = GetDevicePlugAndPlayProperty<uint>(this.DeviceInfoSetHandle, parentDeviceInfoData, ExtendedPInvoke.SPDRP_CAPABILITIES);
            if (getPropertyResult.IsError == true)
            {
                switch (getPropertyResult.Error!.Value)
                {
                    case GetDevicePlugAndPlayPropertyError.Values.PropertyDoesNotExistOrIsInvalid:
                        return MorphicResult.ErrorResult(GetParentOrChildError.CouldNotGetDeviceCapabilities);
                    case GetDevicePlugAndPlayPropertyError.Values.SizeOfReturnTypeDoesNotMatchPropertyValue:
                        return MorphicResult.ErrorResult(GetParentOrChildError.CouldNotGetDeviceCapabilities);
                    case GetDevicePlugAndPlayPropertyError.Values.Win32Error:
                        return MorphicResult.ErrorResult(GetParentOrChildError.Win32Error(getPropertyResult.Error.Win32ErrorCode!.Value));
                }
            }
            ExtendedPInvoke.CmDeviceCapabilitiesFlags parentDeviceCapabilities = (ExtendedPInvoke.CmDeviceCapabilitiesFlags)getPropertyResult.Value!;

            // NOTE: in the future, if we want to save the devicePaths for all nodes (including parents/children), we should change DevicePath to be non-optional (and we should enumerate and capture that path here)
            Device parentDevice = new Device(this.DeviceInfoSetHandle, parentDeviceInfoData, null, parentDeviceInstanceId, parentDeviceCapabilities);
            return MorphicResult.OkResult<Device?>(parentDevice);
        }

        public MorphicResult<List<Device>, GetParentOrChildError> GetChildren()
        {
            // see: https://docs.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_child

            var listOfChildDevices = new List<Device>();

            // NOTE: we first query the child; if the child is populated, we then query the nex tsibling using that child's node (and then continue querying siblings based on the previous sibling's node)
            // start by seeking the first child, using our devinst
            var seekingFirstChild = true;
            var previousDevInst = this.DeviceInfoData.DevInst;

            while (true)
            {
                // get the child's DevInst
                uint childDevInst;
                uint getChildOrSiblingResult;
                if (seekingFirstChild == true)
                {
                    getChildOrSiblingResult = ExtendedPInvoke.CM_Get_Child(out childDevInst, previousDevInst, 0 /* must be zero */);
                }
                else
                {
                    getChildOrSiblingResult = ExtendedPInvoke.CM_Get_Sibling(out childDevInst, previousDevInst, 0 /* must be zero */);
                }
                if (getChildOrSiblingResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
                {
                    if (getChildOrSiblingResult == (uint)ExtendedPInvoke.CR_RESULT.CR_NO_SUCH_DEVNODE)
                    {
                        // device doesn't have any/another child; exit this loop
                        break;
                    }

                    return MorphicResult.ErrorResult(GetParentOrChildError.ConfigManagerError(getChildOrSiblingResult));
                }
                // update our previousDevInst (so that the next iteration uses it instead)
                previousDevInst = childDevInst;
                // set our seekingFirstChild flag to false; all further iterations should be seeking siblings instead
                seekingFirstChild = false;

                // use the child's DevInst to get a device instance id
                int deviceInstanceIdLength;
                var getDeviceIdSizeResult = ExtendedPInvoke.CM_Get_Device_ID_Size(out deviceInstanceIdLength, childDevInst, 0 /* must be zero */);
                if (getDeviceIdSizeResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
                {
                    return MorphicResult.ErrorResult(GetParentOrChildError.ConfigManagerError(getDeviceIdSizeResult));
                }
                if (deviceInstanceIdLength == 0)
                {
                    // device does not exist
                    return MorphicResult.OkResult(new List<Device>());
                }
                //
                // add one to the length (because the previous function returns the length _without_ the null-terminator character)
                deviceInstanceIdLength += 1; // +1 char for null-terminator character
                                             // NOTE: the length is in chars, so when we allocate the actual memory to receive the instance id we need to multiply that by the size of a character (e.g. x2)
                var pointerToChildDeviceInstanceId = Marshal.AllocHGlobal(deviceInstanceIdLength * sizeof(char));
                string childDeviceInstanceId;
                try
                {
                    var getDeviceIdResult = ExtendedPInvoke.CM_Get_Device_ID(childDevInst, pointerToChildDeviceInstanceId, deviceInstanceIdLength, 0);
                    if (getDeviceIdResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
                    {
                        return MorphicResult.ErrorResult(GetParentOrChildError.ConfigManagerError(getDeviceIdResult));
                    }

                    childDeviceInstanceId = Marshal.PtrToStringUni(pointerToChildDeviceInstanceId)!;
                }
                finally
                {
                    Marshal.FreeHGlobal(pointerToChildDeviceInstanceId);
                }

                // get the devinfo data of the child
                var childDeviceInfoData = PInvoke.SetupApi.SP_DEVINFO_DATA.Create();
                var pointerToChildDeviceInfoData = Marshal.AllocHGlobal(Marshal.SizeOf<PInvoke.SetupApi.SP_DEVINFO_DATA>());
                Marshal.StructureToPtr(childDeviceInfoData, pointerToChildDeviceInfoData, false);
                try
                {
                    var setupDiOpenDeviceInfoSuccess = PInvoke.SetupApi.SetupDiOpenDeviceInfo(this.DeviceInfoSetHandle, childDeviceInstanceId, IntPtr.Zero, PInvoke.SetupApi.SetupDiOpenDeviceInfoFlags.None, pointerToChildDeviceInfoData);
                    if (setupDiOpenDeviceInfoSuccess == false)
                    {
                        var win32ErrorCode = Marshal.GetLastWin32Error();
                        return MorphicResult.ErrorResult(GetParentOrChildError.Win32Error(win32ErrorCode));
                    }

                    childDeviceInfoData = Marshal.PtrToStructure<PInvoke.SetupApi.SP_DEVINFO_DATA>(pointerToChildDeviceInfoData);
                }
                finally
                {
                    Marshal.FreeHGlobal(pointerToChildDeviceInfoData);
                }

                // get the capabilities of the device
                var getPropertyResult = GetDevicePlugAndPlayProperty<uint>(this.DeviceInfoSetHandle, childDeviceInfoData, ExtendedPInvoke.SPDRP_CAPABILITIES);
                if (getPropertyResult.IsError == true)
                {
                    switch (getPropertyResult.Error!.Value)
                    {
                        case GetDevicePlugAndPlayPropertyError.Values.PropertyDoesNotExistOrIsInvalid:
                            return MorphicResult.ErrorResult(GetParentOrChildError.CouldNotGetDeviceCapabilities);
                        case GetDevicePlugAndPlayPropertyError.Values.SizeOfReturnTypeDoesNotMatchPropertyValue:
                            return MorphicResult.ErrorResult(GetParentOrChildError.CouldNotGetDeviceCapabilities);
                        case GetDevicePlugAndPlayPropertyError.Values.Win32Error:
                            return MorphicResult.ErrorResult(GetParentOrChildError.Win32Error(getPropertyResult.Error.Win32ErrorCode!.Value));
                    }
                }
                ExtendedPInvoke.CmDeviceCapabilitiesFlags childDeviceCapabilities = (ExtendedPInvoke.CmDeviceCapabilitiesFlags)getPropertyResult.Value!;

                // NOTE: in the future, if we want to save the devicePaths for all nodes (including parents/children), we should change DevicePath to be non-optional (and we should enumerate and capture that path here)
                Device childDevice = new Device(this.DeviceInfoSetHandle, childDeviceInfoData, null, childDeviceInstanceId, childDeviceCapabilities);
                listOfChildDevices.Add(childDevice);
            }
            
            return MorphicResult.OkResult(listOfChildDevices);
        }

        public record GetDevicesForClassGuidError : MorphicAssociatedValueEnum<GetDevicesForClassGuidError.Values>
        {
            // enum members
            public enum Values
            {
                ConfigManagerError/*(uint configManagerErrorCode)*/,
                CouldNotEnumerateViaWin32Api,
                CouldNotGetDeviceCapabilities,
                CouldNotGetDeviceInstanceId,
                Win32Error/*(int win32ErrorCode)*/
            }

            // functions to create member instances
            public static GetDevicesForClassGuidError ConfigManagerError(uint configManagerErrorCode) => new GetDevicesForClassGuidError(Values.ConfigManagerError) { ConfigManagerErrorCode = configManagerErrorCode };
            public static GetDevicesForClassGuidError CouldNotEnumerateViaWin32Api => new GetDevicesForClassGuidError(Values.CouldNotEnumerateViaWin32Api);
            public static GetDevicesForClassGuidError CouldNotGetDeviceCapabilities => new GetDevicesForClassGuidError(Values.CouldNotGetDeviceCapabilities);
            public static GetDevicesForClassGuidError CouldNotGetDeviceInstanceId => new GetDevicesForClassGuidError(Values.CouldNotGetDeviceInstanceId);
            public static GetDevicesForClassGuidError Win32Error(int win32ErrorCode) => new GetDevicesForClassGuidError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public uint? ConfigManagerErrorCode { get; private set; }
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private GetDevicesForClassGuidError(Values value) : base(value) { }
        }
        //
        public static MorphicResult<List<Device>, GetDevicesForClassGuidError> GetDevicesForClassGuid(Guid classGuid)
        {
            var listOfDevices = new List<Device>();

            // get a DeviceInfoSet as a SafeHandle variant (which we'll share with each of our devices, so that they can further enumerate their parents/children)
            PInvoke.SetupApi.SafeDeviceInfoSetHandle? deviceInfoSetHandle;
            deviceInfoSetHandle = PInvoke.SetupApi.SetupDiGetClassDevs(classGuid, null, IntPtr.Zero, PInvoke.SetupApi.GetClassDevsFlags.DIGCF_PRESENT | PInvoke.SetupApi.GetClassDevsFlags.DIGCF_DEVICEINTERFACE);
            if (deviceInfoSetHandle is null)
            {
                return MorphicResult.ErrorResult(GetDevicesForClassGuidError.CouldNotEnumerateViaWin32Api);
            }
            // when we reach here, we have a device information set (which is a list of attached and enumerated disks)

            // get the device path and DeviceInfoData for each enumerated member in our list
            var deviceInterfaceData = PInvoke.SetupApi.SP_DEVICE_INTERFACE_DATA.Create();
            int memberIndex = 0;
            while (true)
            {
                string devicePath;

                var success = PInvoke.SetupApi.SetupDiEnumDeviceInterfaces(deviceInfoSetHandle, IntPtr.Zero, ref classGuid, memberIndex, ref deviceInterfaceData);
                if (success == false)
                {
                    var win32ErrorCode = Marshal.GetLastWin32Error();
                    if (win32ErrorCode == (int)PInvoke.Win32ErrorCode.ERROR_NO_MORE_ITEMS)
                    {
                        // we have successfully enumerated all items; break out of our loop
                        break;
                    }
                    else
                    {
                        // for any other win32 error, fail
                        return MorphicResult.ErrorResult(GetDevicesForClassGuidError.Win32Error(win32ErrorCode));
                    }
                }

                // we captured the device's interface data; use this to capture the rest of the handles/data
                var deviceInterfaceDetailData = new PInvoke.SetupApi.SP_DEVICE_INTERFACE_DETAIL_DATA();
                //
                // first, send a null SP_DEVICE_INTERFACE_DETAIL_DATA with a size of zero so that we get the actual required size needed for the struct
                int requiredSize = 0;
                var pointerToRequiredSize = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
                Marshal.StructureToPtr(requiredSize, pointerToRequiredSize, false);
                try
                {
                    success = PInvoke.SetupApi.SetupDiGetDeviceInterfaceDetail(deviceInfoSetHandle, ref deviceInterfaceData, IntPtr.Zero, 0, pointerToRequiredSize, IntPtr.Zero);
                    if (success == false)
                    {
                        var win32ErrorCode = Marshal.GetLastWin32Error();
                        if (win32ErrorCode == (int)PInvoke.Win32ErrorCode.ERROR_INSUFFICIENT_BUFFER)
                        {
                            // we have successfully received the size we need
                        }
                        else
                        {
                            // for any other win32 error, fail
                            return MorphicResult.ErrorResult(GetDevicesForClassGuidError.Win32Error(win32ErrorCode));
                        }
                    }

                    requiredSize = Marshal.PtrToStructure<int>(pointerToRequiredSize);
                }
                finally
                {
                    Marshal.FreeHGlobal(pointerToRequiredSize);
                }
                //
                // setup our SP_DEVINFO_DATA instance (so our devices can retrieve properties later)
                var deviceInfoData = PInvoke.SetupApi.SP_DEVINFO_DATA.Create();
                var pointerToDeviceInfoData = Marshal.AllocHGlobal(Marshal.SizeOf<PInvoke.SetupApi.SP_DEVINFO_DATA>());
                Marshal.StructureToPtr(deviceInfoData, pointerToDeviceInfoData, false);
                //
                // setup our SP_DEVICE_INTERFACE_DETAIL_DATA instance (using the size we just got from the first call)
                var pointerToDeviceInterfaceDetailData = Marshal.AllocHGlobal(requiredSize);
                deviceInterfaceDetailData.cbSize = PInvoke.SetupApi.SP_DEVICE_INTERFACE_DETAIL_DATA.ReportableStructSize;
                Marshal.StructureToPtr(deviceInterfaceDetailData, pointerToDeviceInterfaceDetailData, false);
                try
                {
                    success = PInvoke.SetupApi.SetupDiGetDeviceInterfaceDetail(deviceInfoSetHandle, ref deviceInterfaceData, pointerToDeviceInterfaceDetailData, requiredSize, IntPtr.Zero, pointerToDeviceInfoData);
                    if (success == false)
                    {
                        var win32ErrorCode = Marshal.GetLastWin32Error();
                        return MorphicResult.ErrorResult(GetDevicesForClassGuidError.Win32Error(win32ErrorCode));
                    }

                    // capture the device's path; it's already stored in deviceInterfaceData.DevicePath, but that's as a char*; we want to return it as a string instead
                    var pointerToDevicePath = new IntPtr(pointerToDeviceInterfaceDetailData.ToInt64() + Marshal.SizeOf(deviceInterfaceDetailData.cbSize));
                    devicePath = Marshal.PtrToStringAuto(pointerToDevicePath)!;
                    //
                    deviceInfoData = Marshal.PtrToStructure<PInvoke.SetupApi.SP_DEVINFO_DATA>(pointerToDeviceInfoData);

                    // use the device's DevInst to get a device instance id
                    int deviceInstanceIdLength;
                    var getDeviceIdSizeResult = ExtendedPInvoke.CM_Get_Device_ID_Size(out deviceInstanceIdLength, deviceInfoData.DevInst, 0 /* must be zero */);
                    if (getDeviceIdSizeResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
                    {
                        return MorphicResult.ErrorResult(GetDevicesForClassGuidError.ConfigManagerError(getDeviceIdSizeResult));
                    }
                    if (deviceInstanceIdLength == 0)
                    {
                        // device does not exist
                        return MorphicResult.ErrorResult(GetDevicesForClassGuidError.CouldNotGetDeviceInstanceId);
                    }
                    //
                    // add one to the length (because the previous function returns the length _without_ the null-terminator character)
                    deviceInstanceIdLength += 1; // +1 char for null-terminator character
                                                 // NOTE: the length is in chars, so when we allocate the actual memory to receive the instance id we need to multiply that by the size of a character (e.g. x2)
                    var pointerToDeviceInstanceId = Marshal.AllocHGlobal(deviceInstanceIdLength * sizeof(char));
                    string deviceInstanceId;
                    try
                    {
                        var getDeviceIdResult = ExtendedPInvoke.CM_Get_Device_ID(deviceInfoData.DevInst, pointerToDeviceInstanceId, deviceInstanceIdLength, 0);
                        if (getDeviceIdResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
                        {
                            return MorphicResult.ErrorResult(GetDevicesForClassGuidError.ConfigManagerError(getDeviceIdResult));
                        }

                        deviceInstanceId = Marshal.PtrToStringUni(pointerToDeviceInstanceId)!;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pointerToDeviceInstanceId);
                    }

                    // get the capabilities of the device
                    var getPropertyResult = GetDevicePlugAndPlayProperty<uint>(deviceInfoSetHandle, deviceInfoData, ExtendedPInvoke.SPDRP_CAPABILITIES);
                    if (getPropertyResult.IsError == true)
                    {
                        switch (getPropertyResult.Error!.Value)
                        {
                            case GetDevicePlugAndPlayPropertyError.Values.PropertyDoesNotExistOrIsInvalid:
                                return MorphicResult.ErrorResult(GetDevicesForClassGuidError.CouldNotGetDeviceCapabilities);
                            case GetDevicePlugAndPlayPropertyError.Values.SizeOfReturnTypeDoesNotMatchPropertyValue:
                                return MorphicResult.ErrorResult(GetDevicesForClassGuidError.CouldNotGetDeviceCapabilities);
                            case GetDevicePlugAndPlayPropertyError.Values.Win32Error:
                                return MorphicResult.ErrorResult(GetDevicesForClassGuidError.Win32Error(getPropertyResult.Error.Win32ErrorCode!.Value));
                        }
                    }
                    ExtendedPInvoke.CmDeviceCapabilitiesFlags deviceCapabilities = (ExtendedPInvoke.CmDeviceCapabilitiesFlags)getPropertyResult.Value!;

                    var device = new Device(deviceInfoSetHandle, deviceInfoData, devicePath, deviceInstanceId, deviceCapabilities);
                    listOfDevices.Add(device);
                }
                finally
                {
                    Marshal.FreeHGlobal(pointerToDeviceInterfaceDetailData);
                    Marshal.FreeHGlobal(pointerToDeviceInfoData);
                }

                memberIndex += 1;
            }

            return MorphicResult.OkResult(listOfDevices);
        }

        public record SafeEjectError : MorphicAssociatedValueEnum<SafeEjectError.Values>
        {
            // enum members
            public enum Values
            {
                ConfigManagerError/*(uint configManagerErrorCode)*/,
                DeviceInUse,
                DeviceWasAlreadyRemoved,
                SafeEjectVetoed/*(int vetoType, string vetoName)*/
            }

            // functions to create member instances
            public static SafeEjectError ConfigManagerError(uint configManagerErrorCode) => new SafeEjectError(Values.ConfigManagerError) { ConfigManagerErrorCode = configManagerErrorCode };
            public static SafeEjectError DeviceInUse => new SafeEjectError(Values.DeviceInUse);
            public static SafeEjectError DeviceWasAlreadyRemoved => new SafeEjectError(Values.DeviceWasAlreadyRemoved);
            public static SafeEjectError SafeEjectVetoed(int vetoType, string vetoName) => new SafeEjectError(Values.SafeEjectVetoed) { VetoType = vetoType, VetoName = vetoName };

            // associated values
            public uint? ConfigManagerErrorCode { get; private set; }
            public int? VetoType { get; private set; }
            public string? VetoName { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private SafeEjectError(Values value) : base(value) { }
        }
        //
        public MorphicResult<MorphicUnit, SafeEjectError> SafeEject()
        {
            // see: https://docs.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_request_device_ejectw
            // see: https://docs.microsoft.com/en-us/windows/win32/api/cfg/ne-cfg-pnp_veto_type

            int vetoType;
            string vetoName;
            var pointerToVetoName = Marshal.AllocHGlobal(1024 /*ExtendedPInvoke.MAX_PATH * sizeof(char)*/);
            try
            {
                var deviceEjectResult = ExtendedPInvoke.CM_Request_Device_Eject(this.DeviceInfoData.DevInst, out vetoType, pointerToVetoName, 1024 /*ExtendedPInvoke.MAX_PATH*/, 0 /* not used */);
                vetoName = Marshal.PtrToStringUni(pointerToVetoName)!;
                if (deviceEjectResult != (uint)ExtendedPInvoke.CR_RESULT.CR_SUCCESS)
                {
                    switch (deviceEjectResult)
                    {
                        case (uint)ExtendedPInvoke.CR_RESULT.CR_REMOVE_VETOED:
                            switch (vetoType)
                            {
                                case (int)ExtendedPInvoke.PNP_VETO_TYPE.PNP_VetoPendingClose:
                                case (int)ExtendedPInvoke.PNP_VETO_TYPE.PNP_VetoWindowsApp:
                                case (int)ExtendedPInvoke.PNP_VETO_TYPE.PNP_VetoWindowsService:
                                case (int)ExtendedPInvoke.PNP_VETO_TYPE.PNP_VetoOutstandingOpen:
                                    return MorphicResult.ErrorResult(SafeEjectError.DeviceInUse);
                                case (int)ExtendedPInvoke.PNP_VETO_TYPE.PNP_VetoAlreadyRemoved:
                                    return MorphicResult.ErrorResult(SafeEjectError.DeviceWasAlreadyRemoved);
                                default:
                                    return MorphicResult.ErrorResult(SafeEjectError.SafeEjectVetoed(vetoType, vetoName));
                            }
                        default:
                            return MorphicResult.ErrorResult(SafeEjectError.ConfigManagerError(deviceEjectResult));
                    }
                }

            }
            finally
            {
                Marshal.FreeHGlobal(pointerToVetoName);
            }

            return MorphicResult.OkResult();
        }

        //

        private record GetDevicePlugAndPlayPropertyError : MorphicAssociatedValueEnum<GetDevicePlugAndPlayPropertyError.Values>
        {
            // enum members
            public enum Values
            {
                PropertyDoesNotExistOrIsInvalid,
                SizeOfReturnTypeDoesNotMatchPropertyValue,
                Win32Error/*(int win32ErrorCode)*/
            }

            // functions to create member instances
            public static GetDevicePlugAndPlayPropertyError PropertyDoesNotExistOrIsInvalid => new GetDevicePlugAndPlayPropertyError(Values.PropertyDoesNotExistOrIsInvalid);
            public static GetDevicePlugAndPlayPropertyError SizeOfReturnTypeDoesNotMatchPropertyValue => new GetDevicePlugAndPlayPropertyError(Values.SizeOfReturnTypeDoesNotMatchPropertyValue);
            public static GetDevicePlugAndPlayPropertyError Win32Error(int win32ErrorCode) => new GetDevicePlugAndPlayPropertyError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private GetDevicePlugAndPlayPropertyError(Values value) : base(value) { }
        }
        //
        private static MorphicResult<TResult, GetDevicePlugAndPlayPropertyError> GetDevicePlugAndPlayProperty<TResult>(PInvoke.SetupApi.SafeDeviceInfoSetHandle deviceInfoSetHandle, PInvoke.SetupApi.SP_DEVINFO_DATA devinfoData, uint property)
        {
            // step #1: get the required size of the property
            uint propertyRegDataType;
            int requiredSize;
            var pointerToRequiredSize = Marshal.AllocHGlobal(Marshal.SizeOf<uint>());
            try
            {
                var success = ExtendedPInvoke.SetupDiGetDeviceRegistryProperty(deviceInfoSetHandle, ref devinfoData, property, out propertyRegDataType, IntPtr.Zero, 0, pointerToRequiredSize);
                if (success == false)
                {
                    var win32ErrorCode = Marshal.GetLastWin32Error();
                    switch (win32ErrorCode)
                    {
                        case (int)PInvoke.Win32ErrorCode.ERROR_INSUFFICIENT_BUFFER:
                            // we have successfully received the size we need
                            break;
                        case (int)PInvoke.Win32ErrorCode.ERROR_INVALID_DATA:
                            return MorphicResult.ErrorResult(GetDevicePlugAndPlayPropertyError.PropertyDoesNotExistOrIsInvalid);
                        default:
                            return MorphicResult.ErrorResult(GetDevicePlugAndPlayPropertyError.Win32Error(win32ErrorCode));
                    }
                }

                requiredSize = Marshal.PtrToStructure<int>(pointerToRequiredSize);
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToRequiredSize);
            }

            // step #1: get the property
            var pointerToPropertyData = Marshal.AllocHGlobal(requiredSize);
            try
            {
                var success = ExtendedPInvoke.SetupDiGetDeviceRegistryProperty(deviceInfoSetHandle, ref devinfoData, property, out propertyRegDataType, pointerToPropertyData, requiredSize, IntPtr.Zero);
                if (success == false)
                {
                    var win32ErrorCode = Marshal.GetLastWin32Error();
                    switch (win32ErrorCode)
                    {
                        case (int)PInvoke.Win32ErrorCode.ERROR_INVALID_DATA:
                            return MorphicResult.ErrorResult(GetDevicePlugAndPlayPropertyError.PropertyDoesNotExistOrIsInvalid);
                        default:
                            return MorphicResult.ErrorResult(GetDevicePlugAndPlayPropertyError.Win32Error(win32ErrorCode));
                    }
                }

                // now, convert to type TResult
                if (typeof(TResult) == typeof(byte[]))
                {
                    var propertyDataAsByteArray = new byte[requiredSize];
                    Marshal.Copy(pointerToPropertyData, propertyDataAsByteArray, 0, requiredSize);
                    return MorphicResult.OkResult((TResult)(object)propertyDataAsByteArray);
                }
                else if (typeof(TResult) == typeof(int))
                {
                    if (requiredSize != sizeof(int)) { return MorphicResult.ErrorResult(GetDevicePlugAndPlayPropertyError.SizeOfReturnTypeDoesNotMatchPropertyValue); }
                    var result = Marshal.PtrToStructure<int>(pointerToPropertyData);
                    return MorphicResult.OkResult((TResult)(object)result!);
                }
                else if (typeof(TResult) == typeof(uint))
                {
                    if (requiredSize != sizeof(uint)) { return MorphicResult.ErrorResult(GetDevicePlugAndPlayPropertyError.SizeOfReturnTypeDoesNotMatchPropertyValue); }
                    var result = Marshal.PtrToStructure<uint>(pointerToPropertyData);
                    return MorphicResult.OkResult((TResult)(object)result!);
                }
                else
                {
                    throw new ArgumentOutOfRangeException("TResult");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToPropertyData);
            }
        }

        public record SafelyRemoveDeviceError : MorphicAssociatedValueEnum<SafelyRemoveDeviceError.Values>
        {
            // enum members
            public enum Values
            {
                ConfigManagerError/*(uint configManagerErrorCode)*/,
                CouldNotGetDeviceCapabilities,
                DeviceIsNotRemovable,
                DeviceInUse,
                DeviceWasAlreadyRemoved,
                SafeEjectVetoed/*(int vetoType, string vetoName)*/,
                Win32Error/*(int win32ErrorCode)*/

            }

            // functions to create member instances
            public static SafelyRemoveDeviceError ConfigManagerError(uint configManagerErrorCode) => new SafelyRemoveDeviceError(Values.ConfigManagerError) { ConfigManagerErrorCode = configManagerErrorCode };
            public static SafelyRemoveDeviceError CouldNotGetDeviceCapabilities => new SafelyRemoveDeviceError(Values.CouldNotGetDeviceCapabilities);
            public static SafelyRemoveDeviceError DeviceInUse => new SafelyRemoveDeviceError(Values.DeviceInUse);
            public static SafelyRemoveDeviceError DeviceIsNotRemovable => new SafelyRemoveDeviceError(Values.DeviceIsNotRemovable);
            public static SafelyRemoveDeviceError DeviceWasAlreadyRemoved => new SafelyRemoveDeviceError(Values.DeviceWasAlreadyRemoved);
            public static SafelyRemoveDeviceError SafeEjectVetoed(int vetoType, string vetoName) => new SafelyRemoveDeviceError(Values.SafeEjectVetoed) { VetoType = vetoType, VetoName = vetoName };
            public static SafelyRemoveDeviceError Win32Error(int win32ErrorCode) => new SafelyRemoveDeviceError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public uint? ConfigManagerErrorCode { get; private set; }
            public int? VetoType { get; private set; }
            public string? VetoName { get; private set; }
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private SafelyRemoveDeviceError(Values value) : base(value) { }
        }
        //
        // NOTE: this will detach the PnP device from the system
        public MorphicResult<MorphicUnit, SafelyRemoveDeviceError> SafelyRemoveDevice()
        {
            Device targetRemovableDevice;

            if (this.IsRemovable == true)
            {
                targetRemovableDevice = this;
            }
            else
            {
                var getFirstRemovableAncestorResult = this.GetFirstRemovableAncestor();
                if (getFirstRemovableAncestorResult.IsError == true)
                {
                    switch (getFirstRemovableAncestorResult.Error!.Value)
                    {
                        case Device.GetParentOrChildError.Values.ConfigManagerError:
                            return MorphicResult.ErrorResult(SafelyRemoveDeviceError.ConfigManagerError(getFirstRemovableAncestorResult.Error!.ConfigManagerErrorCode!.Value));
                        case Device.GetParentOrChildError.Values.CouldNotGetDeviceCapabilities:
                            return MorphicResult.ErrorResult(SafelyRemoveDeviceError.CouldNotGetDeviceCapabilities);
                        case Device.GetParentOrChildError.Values.Win32Error:
                            return MorphicResult.ErrorResult(SafelyRemoveDeviceError.Win32Error(getFirstRemovableAncestorResult.Error!.Win32ErrorCode!.Value));
                    }
                }

                if (getFirstRemovableAncestorResult.Value is null)
                {
                    // neither this device nor its ancestors are ejectable
                    return MorphicResult.ErrorResult(SafelyRemoveDeviceError.DeviceIsNotRemovable);
                }

                targetRemovableDevice = getFirstRemovableAncestorResult.Value!;
            }

            var safeEjectResult = targetRemovableDevice.SafeEject();
            if (safeEjectResult.IsError == true)
            {
                switch (safeEjectResult.Error!.Value)
                {
                    case Device.SafeEjectError.Values.ConfigManagerError:
                        return MorphicResult.ErrorResult(SafelyRemoveDeviceError.ConfigManagerError(safeEjectResult.Error!.ConfigManagerErrorCode!.Value));
                    case Device.SafeEjectError.Values.DeviceInUse:
                        return MorphicResult.ErrorResult(SafelyRemoveDeviceError.DeviceInUse);
                    case Device.SafeEjectError.Values.DeviceWasAlreadyRemoved:
                        return MorphicResult.ErrorResult(SafelyRemoveDeviceError.DeviceWasAlreadyRemoved);
                    case Device.SafeEjectError.Values.SafeEjectVetoed:
                        return MorphicResult.ErrorResult(SafelyRemoveDeviceError.SafeEjectVetoed(safeEjectResult.Error!.VetoType!.Value, safeEjectResult.Error!.VetoName!));
                }
            }

            // we successfully ejected
            return MorphicResult.OkResult();
        }

        internal MorphicResult<Device?, Device.GetParentOrChildError> GetFirstRemovableAncestor()
        {
            var ancestorDeviceResult = this.GetParent();
            if (ancestorDeviceResult.IsError == true)
            {
                return MorphicResult.ErrorResult(ancestorDeviceResult.Error!);
            }
            var ancestorDevice = ancestorDeviceResult.Value;

            while (ancestorDevice is not null)
            {
                // if this ancestor is removable, return it
                if (ancestorDevice.IsRemovable == true)
                {
                    return MorphicResult.OkResult<Device?>(ancestorDevice);
                }

                // if this ancestor was not removable, search out the parent of the current ancestor (i.e. work our way toward root)
                ancestorDeviceResult = ancestorDevice.GetParent();
                if (ancestorDeviceResult.IsError == true)
                {
                    return MorphicResult.ErrorResult(ancestorDeviceResult.Error!);
                }
                ancestorDevice = ancestorDeviceResult.Value;
            }

            // if we have run out of ancestors, return null
            return MorphicResult.OkResult<Device?>(null);
        }

        internal MorphicResult<bool, Device.GetParentOrChildError> GetIsDeviceOrAncestorsRemovable()
        {
            // determine if this drive or any of its ancestors are removable
            if (this.IsRemovable == true)
            {
                return MorphicResult.OkResult(true);
            }
            else
            {
                var getFirstRemovableAncestorResult = this.GetFirstRemovableAncestor();
                if (getFirstRemovableAncestorResult.IsError == true)
                {
                    return MorphicResult.ErrorResult(getFirstRemovableAncestorResult.Error!);
                }

                // if one of our ancestors was removable, our drive is removable
                var isRemovable = (getFirstRemovableAncestorResult.Value! is not null);
                return MorphicResult.OkResult(isRemovable);
            }
        }
    }
}
