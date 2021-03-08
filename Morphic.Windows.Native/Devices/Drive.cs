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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native.Devices
{
    public class Drive
    {
        private Device _device;

        private Drive(Device device)
        {
            _device = device;
        }

        public record GetDrivesError : MorphicAssociatedValueEnum<GetDrivesError.Values>
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
            public static GetDrivesError ConfigManagerError(uint configManagerErrorCode) => new GetDrivesError(Values.ConfigManagerError) { ConfigManagerErrorCode = configManagerErrorCode };
            public static GetDrivesError CouldNotEnumerateViaWin32Api => new GetDrivesError(Values.CouldNotEnumerateViaWin32Api);
            public static GetDrivesError CouldNotGetDeviceCapabilities => new GetDrivesError(Values.CouldNotGetDeviceCapabilities);
            public static GetDrivesError CouldNotGetDeviceInstanceId => new GetDrivesError(Values.CouldNotGetDeviceInstanceId);
            public static GetDrivesError Win32Error(int win32ErrorCode) => new GetDrivesError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public uint? ConfigManagerErrorCode { get; private set; }
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private GetDrivesError(Values value) : base(value) { }
        }
        //
        public static IMorphicResult<List<Drive>, GetDrivesError> GetAllDrives()
        {
            var allDrives = new List<Drive>();

            var allDiskDevicesResult = Morphic.Windows.Native.Devices.Device.GetDevicesForClassGuid(ExtendedPInvoke.GUID_DEVINTERFACE_DISK);
            if (allDiskDevicesResult.IsError == true)
            {
                switch (allDiskDevicesResult.Error!.Value)
                {
                    case Device.GetDevicesForClassGuidError.Values.ConfigManagerError:
                        return IMorphicResult<List<Drive>, GetDrivesError>.ErrorResult(GetDrivesError.ConfigManagerError(allDiskDevicesResult.Error!.ConfigManagerErrorCode!.Value));
                    case Device.GetDevicesForClassGuidError.Values.CouldNotEnumerateViaWin32Api:
                        return IMorphicResult<List<Drive>, GetDrivesError>.ErrorResult(GetDrivesError.CouldNotEnumerateViaWin32Api);
                    case Device.GetDevicesForClassGuidError.Values.CouldNotGetDeviceCapabilities:
                        return IMorphicResult<List<Drive>, GetDrivesError>.ErrorResult(GetDrivesError.CouldNotGetDeviceCapabilities);
                    case Device.GetDevicesForClassGuidError.Values.CouldNotGetDeviceInstanceId:
                        return IMorphicResult<List<Drive>, GetDrivesError>.ErrorResult(GetDrivesError.CouldNotGetDeviceInstanceId);
                    case Device.GetDevicesForClassGuidError.Values.Win32Error:
                        return IMorphicResult<List<Drive>, GetDrivesError>.ErrorResult(GetDrivesError.Win32Error(allDiskDevicesResult.Error!.Win32ErrorCode!.Value));
                }
            }

            // create a drive object for each disk
            foreach (var diskDevice in allDiskDevicesResult.Value!)
            {
                var drive = new Drive(diskDevice);
                allDrives.Add(drive);
            }

            return IMorphicResult<List<Drive>, GetDrivesError>.SuccessResult(allDrives);
        }

        public record TryGetDriveLetterError : MorphicAssociatedValueEnum<TryGetDriveLetterError.Values>
        {
            // enum members
            public enum Values
            {
                CouldNotRetrieveStorageDeviceNumbers,
                Win32Error/*(int win32ErrorCode)*/
            }

            // functions to create member instances
            public static TryGetDriveLetterError CouldNotRetrieveStorageDeviceNumbers => new TryGetDriveLetterError(Values.CouldNotRetrieveStorageDeviceNumbers);
            public static TryGetDriveLetterError Win32Error(int win32ErrorCode) => new TryGetDriveLetterError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private TryGetDriveLetterError(Values value) : base(value) { }
        }
        //
        public async Task<IMorphicResult<char?, TryGetDriveLetterError>> TryGetDriveLetterAsync()
        {
            // get the disk letter for the drive
            var convertDriveLetterResult = await Drive.TryConvertDevicePathToDosDriveLetterAsync(_device.DevicePath);
            if (convertDriveLetterResult.IsError == true)
            {
                switch (convertDriveLetterResult.Error!.Value)
                {
                    case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                        return IMorphicResult<char?, TryGetDriveLetterError>.ErrorResult(TryGetDriveLetterError.CouldNotRetrieveStorageDeviceNumbers);
                    case StorageDeviceNumberError.Values.Win32Error:
                        return IMorphicResult<char?, TryGetDriveLetterError>.ErrorResult(TryGetDriveLetterError.Win32Error(convertDriveLetterResult.Error!.Win32ErrorCode!.Value));
                }
            }

            // NOTE: convertDiskLetterResult.Value will be null if this drive has no drive letter; note that Windows allows drive letters to be assigned and unassigned on-the-fly to mounted volumes
            var diskLetter = convertDriveLetterResult.Value;
            return IMorphicResult<char?, TryGetDriveLetterError>.SuccessResult(diskLetter);
        }

        public IMorphicResult<bool, Device.GetParentOrChildError> GetIsRemovable()
        {
            // determine if this drive or any of its ancestors are removable
            if (_device.IsRemovable == true)
            {
                return IMorphicResult<bool, Device.GetParentOrChildError>.SuccessResult(true);
            }
            else
            {
                var getFirstRemovableAncestorResult = this.GetFirstRemovableAncestor();
                if (getFirstRemovableAncestorResult.IsError == true)
                {
                    return IMorphicResult<bool, Device.GetParentOrChildError>.ErrorResult(getFirstRemovableAncestorResult.Error!);
                }

                // if one of our ancestors was removable, our drive is removable
                var isRemovable = (getFirstRemovableAncestorResult.Value! != null);
                return IMorphicResult<bool, Device.GetParentOrChildError>.SuccessResult(isRemovable);
            }
        }

        public record SafeEjectError : MorphicAssociatedValueEnum<SafeEjectError.Values>
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
            public static SafeEjectError ConfigManagerError(uint configManagerErrorCode) => new SafeEjectError(Values.ConfigManagerError) { ConfigManagerErrorCode = configManagerErrorCode };
            public static SafeEjectError CouldNotGetDeviceCapabilities => new SafeEjectError(Values.CouldNotGetDeviceCapabilities);
            public static SafeEjectError DeviceInUse => new SafeEjectError(Values.DeviceInUse);
            public static SafeEjectError DeviceIsNotRemovable => new SafeEjectError(Values.DeviceIsNotRemovable);
            public static SafeEjectError DeviceWasAlreadyRemoved => new SafeEjectError(Values.DeviceWasAlreadyRemoved);
            public static SafeEjectError SafeEjectVetoed(int vetoType, string vetoName) => new SafeEjectError(Values.SafeEjectVetoed) { VetoType = vetoType, VetoName = vetoName };
            public static SafeEjectError Win32Error(int win32ErrorCode) => new SafeEjectError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public uint? ConfigManagerErrorCode { get; private set; }
            public int? VetoType { get; private set; }
            public string? VetoName { get; private set; }
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private SafeEjectError(Values value) : base(value) { }
        }
        //
        public IMorphicResult<MorphicUnit, SafeEjectError> SafeEject()
        {
            Device targetRemovableDevice;

            if (_device.IsRemovable == true)
            {
                targetRemovableDevice = _device;
            }
            else
            {
                var getFirstRemovableAncestorResult = this.GetFirstRemovableAncestor();
                if (getFirstRemovableAncestorResult.IsError == true)
                {
                    switch (getFirstRemovableAncestorResult.Error!.Value)
                    {
                        case Device.GetParentOrChildError.Values.ConfigManagerError:
                            return IMorphicResult<MorphicUnit, SafeEjectError>.ErrorResult(SafeEjectError.ConfigManagerError(getFirstRemovableAncestorResult.Error!.ConfigManagerErrorCode!.Value));
                        case Device.GetParentOrChildError.Values.CouldNotGetDeviceCapabilities:
                            return IMorphicResult<MorphicUnit, SafeEjectError>.ErrorResult(SafeEjectError.CouldNotGetDeviceCapabilities);
                        case Device.GetParentOrChildError.Values.Win32Error:
                            return IMorphicResult<MorphicUnit, SafeEjectError>.ErrorResult(SafeEjectError.Win32Error(getFirstRemovableAncestorResult.Error!.Win32ErrorCode!.Value));
                    }
                }

                if (getFirstRemovableAncestorResult.Value == null)
                {
                    // not ejectable!
                    return IMorphicResult<MorphicUnit,SafeEjectError>.ErrorResult(SafeEjectError.DeviceIsNotRemovable);
                }

                targetRemovableDevice = getFirstRemovableAncestorResult.Value!;
            }

            var safeEjectResult = targetRemovableDevice.SafeEject();
            if (safeEjectResult.IsError == true)
            {
                switch (safeEjectResult.Error!.Value)
                {
                    case Device.SafeEjectError.Values.ConfigManagerError:
                        return IMorphicResult<MorphicUnit, SafeEjectError>.ErrorResult(SafeEjectError.ConfigManagerError(safeEjectResult.Error!.ConfigManagerErrorCode!.Value));
                    case Device.SafeEjectError.Values.DeviceInUse:
                        return IMorphicResult<MorphicUnit, SafeEjectError>.ErrorResult(SafeEjectError.DeviceInUse);
                    case Device.SafeEjectError.Values.DeviceWasAlreadyRemoved:
                        return IMorphicResult<MorphicUnit, SafeEjectError>.ErrorResult(SafeEjectError.DeviceWasAlreadyRemoved);
                    case Device.SafeEjectError.Values.SafeEjectVetoed:
                        return IMorphicResult<MorphicUnit, SafeEjectError>.ErrorResult(SafeEjectError.SafeEjectVetoed(safeEjectResult.Error!.VetoType!.Value, safeEjectResult.Error!.VetoName!));
                }
            }

            // we successfully ejected
            return IMorphicResult<MorphicUnit, SafeEjectError>.SuccessResult(new MorphicUnit());
        }

        //

        private IMorphicResult<Device?, Device.GetParentOrChildError> GetFirstRemovableAncestor()
        {
            var ancestorDeviceResult = _device.GetParent();
            if (ancestorDeviceResult.IsError == true)
            {
                return IMorphicResult<Device?, Device.GetParentOrChildError>.ErrorResult(ancestorDeviceResult.Error!);
            }
            var ancestorDevice = ancestorDeviceResult.Value;

            while (ancestorDevice != null)
            {
                // if this ancestor is removable, return it
                if (ancestorDevice.IsRemovable == true)
                {
                    return IMorphicResult<Device?, Device.GetParentOrChildError>.SuccessResult(ancestorDevice);
                }

                // if this ancestor was not removable, search out the parent of the current ancestor (i.e. work our way toward root)
                ancestorDeviceResult = ancestorDevice.GetParent();
                if (ancestorDeviceResult.IsError == true)
                {
                    return IMorphicResult<Device?, Device.GetParentOrChildError>.ErrorResult(ancestorDeviceResult.Error!);
                }
                ancestorDevice = ancestorDeviceResult.Value;
            }

            // if we have run out of ancestors, return null
            return IMorphicResult<Device?, Device.GetParentOrChildError>.SuccessResult(null);
        }

        private record StorageDeviceNumberError : MorphicAssociatedValueEnum<StorageDeviceNumberError.Values>
        {
            // enum members
            public enum Values
            {
                CouldNotRetrieveStorageDeviceNumbers,
                Win32Error/*(int win32ErrorCode)*/
            }

            // functions to create member instances
            public static StorageDeviceNumberError CouldNotRetrieveStorageDeviceNumbers = new StorageDeviceNumberError(Values.CouldNotRetrieveStorageDeviceNumbers);
            public static StorageDeviceNumberError Win32Error(int win32ErrorCode) => new StorageDeviceNumberError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private StorageDeviceNumberError(Values value) : base(value) { }
        }
        //
        private static async Task<IMorphicResult<char?, StorageDeviceNumberError>> TryConvertDevicePathToDosDriveLetterAsync(string devicePath)
        {
            // get the storage device number for this devicePath
            var deviceStorageDeviceNumberResult = await Drive.GetStorageDeviceNumberAsync(devicePath);
            if (deviceStorageDeviceNumberResult.IsError == true)
            {
                switch (deviceStorageDeviceNumberResult.Error!.Value)
                {
                    case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                        return IMorphicResult<char?, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.CouldNotRetrieveStorageDeviceNumbers);
                    case StorageDeviceNumberError.Values.Win32Error:
                        return IMorphicResult<char?, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(deviceStorageDeviceNumberResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new Exception("invalid code path");
                }
            }

            // now get the storage device numbers for every drive letter
            var dosDrivesAndStorageNumbersResult = await GetStorageDeviceNumbersForAllDosDrivesAsync();
            if (dosDrivesAndStorageNumbersResult.IsError == true)
            {
                switch (dosDrivesAndStorageNumbersResult.Error!.Value)
                {
                    case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                        return IMorphicResult<char?, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.CouldNotRetrieveStorageDeviceNumbers);
                    case StorageDeviceNumberError.Values.Win32Error:
                        return IMorphicResult<char?, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(dosDrivesAndStorageNumbersResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new Exception("invalid code path");
                }
            }

            foreach (var (driveLetter, storageDeviceNumber) in dosDrivesAndStorageNumbersResult.Value!)
            {
                if (storageDeviceNumber == deviceStorageDeviceNumberResult.Value!)
                {
                    // we have found the drive letter
                    return IMorphicResult<char?, StorageDeviceNumberError>.SuccessResult(driveLetter);
                }
            }

            // if we could not find the mapping, return null; this means we didn't encounter any errors, but we also didn't find the result
            return IMorphicResult<char?, StorageDeviceNumberError>.SuccessResult(null);
        }

        private static async Task<IMorphicResult<Dictionary<char, uint>, StorageDeviceNumberError>> GetStorageDeviceNumbersForAllDosDrivesAsync()
        {
            // get all dos drive letters
            var allDosDriveLettersResult = ExtraCode_FileSystem.GetAllDosDriveLetters();
            if (allDosDriveLettersResult.IsError == true)
            {
                switch (allDosDriveLettersResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        return IMorphicResult<Dictionary<char, uint>, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(allDosDriveLettersResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new Exception("invalid code path");
                }
            }

            var result = new Dictionary<char, uint>();

            // get the storage device numbers for each drive letter
            foreach (char dosDriveLetter in allDosDriveLettersResult.Value!)
            {
                // get the storage device number for this devicePath
                var deviceStorageDeviceNumberResult = await Drive.GetStorageDeviceNumberAsync(@"\\.\" + dosDriveLetter + ":");
                if (deviceStorageDeviceNumberResult.IsError)
                {
                    switch (deviceStorageDeviceNumberResult.Error!.Value)
                    {
                        case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                            return IMorphicResult<Dictionary<char, uint>, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.CouldNotRetrieveStorageDeviceNumbers);
                        case StorageDeviceNumberError.Values.Win32Error:
                            return IMorphicResult<Dictionary<char, uint>, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(deviceStorageDeviceNumberResult.Error!.Win32ErrorCode!.Value));
                        default:
                            throw new Exception("invalid code path");
                    }
                }

                result.Add(dosDriveLetter, deviceStorageDeviceNumberResult.Value!);
            }

            return IMorphicResult<Dictionary<char, uint>, StorageDeviceNumberError>.SuccessResult(result);
        }

        private static async Task<IMorphicResult<uint, StorageDeviceNumberError>> GetStorageDeviceNumberAsync(string devicePath)
        {
            var deviceHandle = PInvoke.Kernel32.CreateFile(devicePath, (PInvoke.Kernel32.ACCESS_MASK)0, PInvoke.Kernel32.FileShare.FILE_SHARE_READ, IntPtr.Zero, PInvoke.Kernel32.CreationDisposition.OPEN_EXISTING, (PInvoke.Kernel32.CreateFileFlags)0, PInvoke.Kernel32.SafeObjectHandle.Null);
            if (deviceHandle.IsInvalid == true)
            {
                var win32ErrorCode = Marshal.GetLastWin32Error();
                return IMorphicResult<uint, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(win32ErrorCode));
            }
            //
            // get the device number for this storage device
            var storageDeviceNumber = new ExtendedPInvoke.STORAGE_DEVICE_NUMBER();
            var storageDeviceNumberMemoryObject = new Memory<ExtendedPInvoke.STORAGE_DEVICE_NUMBER>(new ExtendedPInvoke.STORAGE_DEVICE_NUMBER[] { storageDeviceNumber });
            //
            try
            {
                uint numberOfBytes;
                try
                {
                    numberOfBytes = await PInvoke.Kernel32.DeviceIoControlAsync<byte /* for null */, ExtendedPInvoke.STORAGE_DEVICE_NUMBER>(
                        hDevice: deviceHandle,
                        dwIoControlCode: ExtendedPInvoke.IOCTL_STORAGE_GET_DEVICE_NUMBER,
                        inBuffer: null,
                        outBuffer: storageDeviceNumberMemoryObject,
                        cancellationToken: System.Threading.CancellationToken.None);
                }
                catch (PInvoke.Win32Exception ex)
                {
                    return IMorphicResult<uint, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error((int)ex.NativeErrorCode));
                }

                var storageDeviceNumberAsArray = storageDeviceNumberMemoryObject.ToArray();
                if (storageDeviceNumberAsArray.Length != 1)
                {
                    return IMorphicResult<uint, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.CouldNotRetrieveStorageDeviceNumbers);
                }
                storageDeviceNumber = storageDeviceNumberAsArray[0];
            }
            finally
            {
                deviceHandle.Close();
            }

            return IMorphicResult<uint, StorageDeviceNumberError>.SuccessResult(storageDeviceNumber.DeviceNumber);
        }




    }
}
