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
using Morphic.Windows.Native.Devices.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native.Devices
{
    // NOTE: the Drive class refers to the logical volume (e.g. a USB volume or partition on a hard drive or CD media, not the physical disk)
    public class Drive
    {
        private Device _device;
        internal ExtendedPInvoke.STORAGE_DEVICE_NUMBER StorageDeviceNumber { get; private set; }

        private Drive(Device device, ExtendedPInvoke.STORAGE_DEVICE_NUMBER storageDeviceNumber)
        {
            _device = device;
            this.StorageDeviceNumber = storageDeviceNumber;
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
                CouldNotRetrieveStorageDeviceNumbers,
                Win32Error/*(int win32ErrorCode)*/
            }

            // functions to create member instances
            public static GetDrivesError ConfigManagerError(uint configManagerErrorCode) => new GetDrivesError(Values.ConfigManagerError) { ConfigManagerErrorCode = configManagerErrorCode };
            public static GetDrivesError CouldNotEnumerateViaWin32Api => new GetDrivesError(Values.CouldNotEnumerateViaWin32Api);
            public static GetDrivesError CouldNotGetDeviceCapabilities => new GetDrivesError(Values.CouldNotGetDeviceCapabilities);
            public static GetDrivesError CouldNotGetDeviceInstanceId => new GetDrivesError(Values.CouldNotGetDeviceInstanceId);
            public static GetDrivesError CouldNotRetrieveStorageDeviceNumbers => new GetDrivesError(Values.CouldNotRetrieveStorageDeviceNumbers);
            public static GetDrivesError Win32Error(int win32ErrorCode) => new GetDrivesError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public uint? ConfigManagerErrorCode { get; private set; }
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private GetDrivesError(Values value) : base(value) { }
        }
        //
        public static async Task<IMorphicResult<List<Drive>, GetDrivesError>> GetAllDrivesAsync()
        {
            var allDrives = new List<Drive>();

            // NOTE: GUID_DEVINTERFACE_VOLUME will capture all volumes (including ones that don't have a drive letter assigned to them)
            var allDiskDevicesResult = Morphic.Windows.Native.Devices.Device.GetDevicesForClassGuid(ExtendedPInvoke.GUID_DEVINTERFACE_VOLUME);
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
                // capture the storage device number for each drive
                var diskStorageDeviceNumberResult = await StorageDeviceUtils.GetStorageDeviceNumberAsync(diskDevice.DevicePath!);
                if (diskStorageDeviceNumberResult.IsError == true)
                {
                    switch (diskStorageDeviceNumberResult.Error!.Value)
                    {
                        case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                            return IMorphicResult<List<Drive>, GetDrivesError>.ErrorResult(GetDrivesError.CouldNotRetrieveStorageDeviceNumbers);
                        case StorageDeviceNumberError.Values.Win32Error:
                            return IMorphicResult<List<Drive>, GetDrivesError>.ErrorResult(GetDrivesError.Win32Error(allDiskDevicesResult.Error!.Win32ErrorCode!.Value));
                    }
                }
                var storageDeviceNumber = diskStorageDeviceNumberResult.Value!;

                var drive = new Drive(diskDevice, storageDeviceNumber);
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
            var convertDriveLetterResult = await Drive.TryConvertDevicePathToDosDriveLetterAsync(_device.DevicePath!);
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

        // NOTE: for a Drive, "IsRemovable" means that the media can be ejected (i.e. a CD can be ejected from a drive; a USB drive's volume cannot be ejected from the physical USB drive)
        //       [in other words, this will return true for CD-ROMs, but false for USB drives]
        public IMorphicResult<bool, Device.GetParentOrChildError> GetIsRemovable()
        {
            return _device.GetIsDeviceOrAncestorsRemovable();
        }

        private static async Task<IMorphicResult<char?, StorageDeviceNumberError>> TryConvertDevicePathToDosDriveLetterAsync(string devicePath)
        {
            // get the storage device number for this devicePath
            var deviceStorageDeviceNumberResult = await StorageDeviceUtils.GetStorageDeviceNumberAsync(devicePath);
            if (deviceStorageDeviceNumberResult.IsError == true)
            {
                switch (deviceStorageDeviceNumberResult.Error!.Value)
                {
                    case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                        // START TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                        throw new Exception("StorageDeviceUtils.GetStorageDeviceNumberAsync ERROR CouldNotRetrieveStorageDeviceNumbers; devicePath: " + devicePath);
                        // END TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                        return IMorphicResult<char?, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.CouldNotRetrieveStorageDeviceNumbers);
                    case StorageDeviceNumberError.Values.Win32Error:
                        // START TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                        throw new Exception("StorageDeviceUtils.GetStorageDeviceNumberAsync ERROR Win32Error; devicePath: " + devicePath + "; win32Error: " + deviceStorageDeviceNumberResult.Error!.Win32ErrorCode!.Value);
                        // END TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
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
                    case Win32ApiError.Values.Win32Error:
                        // START TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                        throw new Exception("StorageDeviceUtils.GetStorageDeviceNumbersForAllDosDrivesAsync ERROR Win32Error; win32Error: " + dosDrivesAndStorageNumbersResult.Error!.Win32ErrorCode!.Value);
                        // END TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                        return IMorphicResult<char?, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(dosDrivesAndStorageNumbersResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new Exception("invalid code path");
                }
            }

            StorageDeviceNumberError? singleDosDeviceErrorResult = null;
            foreach (var (driveLetter, storageDeviceNumberResult) in dosDrivesAndStorageNumbersResult.Value!)
            {
                if (storageDeviceNumberResult.IsError == true)
                {
                    singleDosDeviceErrorResult = storageDeviceNumberResult.Error!;
                    // skip to the next drive letter; if we find our drive letter (even though another drive had an error), then we are still successful in conversion and can return success
                    continue;
                }
                var storageDeviceNumber = storageDeviceNumberResult.Value!;
                //
                if (storageDeviceNumber.Equals(deviceStorageDeviceNumberResult.Value!) == true)
                {
                    // we have found the drive letter
                    return IMorphicResult<char?, StorageDeviceNumberError>.SuccessResult(driveLetter);
                }
            }

            // if we could not find a mapping AND we found an error with one or more of the drive letters we tried to find a storage number for, bubble up that error to our caller
            if (singleDosDeviceErrorResult != null)
            {
                // NOTE for future: we might want to return an array of errors (along with the drives that they were caused on) in the future, if that's necessary
                switch (singleDosDeviceErrorResult.Value)
                {
                    case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                        return IMorphicResult<char?, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.CouldNotRetrieveStorageDeviceNumbers);
                    case StorageDeviceNumberError.Values.Win32Error:
                        return IMorphicResult<char?, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(dosDrivesAndStorageNumbersResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new Exception("invalid code path");
                }
            }

            // if we could not find the mapping and there was no error, return null; this simply means we couldn't find a drive letter for the device path (e.g. recovery partitions, etc.)
            return IMorphicResult<char?, StorageDeviceNumberError>.SuccessResult(null);
        }

        // NOTE: this function will return the full list of drive letters, complete with error conditions for each drive letter (since some drive letters will not let us get their info); it
        //       ALSO returns a general error if it cannot get any results; if this ever seems too complicated, we could split it into two functions
        private static async Task<IMorphicResult<Dictionary<char, IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>>, Win32ApiError>> GetStorageDeviceNumbersForAllDosDrivesAsync()
        {
            // get all dos drive letters
            var allDosDriveLettersResult = Drive.GetAllDosDriveLetters();
            if (allDosDriveLettersResult.IsError == true)
            {
                switch (allDosDriveLettersResult.Error!.Value)
                {
                    case Win32ApiError.Values.Win32Error:
                        // START TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                        throw new Exception("Drive.GetAllDosDriveLetters ERROR Win32Error; win32Error: " + allDosDriveLettersResult.Error!.Win32ErrorCode!.Value);
                        // END TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                        return IMorphicResult<Dictionary<char, IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>>, Win32ApiError>.ErrorResult(Win32ApiError.Win32Error(allDosDriveLettersResult.Error!.Win32ErrorCode!.Value));
                    default:
                        throw new Exception("invalid code path");
                }
            }

            var result = new Dictionary<char, IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>>();

            // get the storage device numbers for each drive letter
            foreach (char dosDriveLetter in allDosDriveLettersResult.Value!)
            {
                IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>? errorResult = null;

                // get the storage device number for this devicePath
                var deviceStorageDeviceNumberResult = await StorageDeviceUtils.GetStorageDeviceNumberAsync(@"\\.\" + dosDriveLetter + ":");
                if (deviceStorageDeviceNumberResult.IsError)
                {
                    switch (deviceStorageDeviceNumberResult.Error!.Value)
                    {
                        case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                            //// START TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                            //throw new Exception("StorageDeviceUtils.GetStorageDeviceNumberAsync(\"\\\\.\\" + dosDriveLetter + ":\") ERROR CouldNotRetrieveStorageDeviceNumbers");
                            //// END TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                            errorResult = IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.CouldNotRetrieveStorageDeviceNumbers);
                            break;
                        case StorageDeviceNumberError.Values.Win32Error:
                            //// START TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                            //throw new Exception("StorageDeviceUtils.GetStorageDeviceNumberAsync(\"\\\\.\\" + dosDriveLetter + ":\") ERROR Win32Error; win32Error: " + allDosDriveLettersResult.Error!.Win32ErrorCode!.Value);
                            //// END TEMPORARY CODE TO DIAGNOSE BETA TESTER'S OPEN USB ISSUE
                            errorResult = IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(deviceStorageDeviceNumberResult.Error!.Win32ErrorCode!.Value));
                            break;
                        default:
                            throw new Exception("invalid code path");
                    }
                }

                if (errorResult == null)
                {
                    var successResult = IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>.SuccessResult(deviceStorageDeviceNumberResult.Value!);
                    result.Add(dosDriveLetter, successResult);
                }
                else
                {
                    result.Add(dosDriveLetter, errorResult);
                }
            }

            return IMorphicResult<Dictionary<char, IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>>, Win32ApiError>.SuccessResult(result);
        }

        public static IMorphicResult<List<char>, Win32ApiError> GetAllDosDriveLetters()
        {
            var listOfDosDriveLetters = new List<char>();

            var listOfDosDeviceNamesResult = Drive.GetAllDosDeviceNames();
            if (listOfDosDeviceNamesResult.IsError == true)
            {
                return IMorphicResult<List<char>, Win32ApiError>.ErrorResult(listOfDosDeviceNamesResult.Error!);
            }

            foreach (var dosDeviceName in listOfDosDeviceNamesResult.Value!)
            {
                if ((dosDeviceName.Length == 2) && (dosDeviceName[1] == ':'))
                {
                    listOfDosDriveLetters.Add(dosDeviceName[0]);
                }
            }

            return IMorphicResult<List<char>, Win32ApiError>.SuccessResult(listOfDosDriveLetters);
        }

        private static IMorphicResult<List<string>, Win32ApiError> GetAllDosDeviceNames()
        {
            var listOfDosDeviceNames = new List<string>();

            var maxSize = UInt16.MaxValue;
            var pointerToListOfDosDeviceNames = Marshal.AllocHGlobal(maxSize);
            try
            {
                var numberOfChars = ExtendedPInvoke.QueryDosDevice(null, pointerToListOfDosDeviceNames, (uint)maxSize / sizeof(char));
                if (numberOfChars == 0)
                {
                    var win32ErrorCode = Marshal.GetLastWin32Error();
                    return IMorphicResult<List<string>, Win32ApiError>.ErrorResult(Win32ApiError.Win32Error(win32ErrorCode));
                }

                var dosDevicesAsCharArray = new char[numberOfChars];
                Marshal.Copy(pointerToListOfDosDeviceNames, dosDevicesAsCharArray, 0, (int)numberOfChars);
                int startIndex = 0;
                for (var index = 0; index < dosDevicesAsCharArray.Length; index += 1)
                {
                    if (dosDevicesAsCharArray[index] == '\0')
                    {
                        var dosDeviceName = new string(dosDevicesAsCharArray, startIndex, index - startIndex);
                        listOfDosDeviceNames.Add(dosDeviceName);

                        // move the startIndex marker to the index after this null pointer
                        startIndex = index + 1;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pointerToListOfDosDeviceNames);
            }

            return IMorphicResult<List<string>, Win32ApiError>.SuccessResult(listOfDosDeviceNames);
        }

        public IMorphicResult<MorphicUnit, Device.SafelyRemoveDeviceError> SafelyRemoveDevice()
        {
            return _device.SafelyRemoveDevice();
        }

        public record EjectDriveMediaError : MorphicAssociatedValueEnum<EjectDriveMediaError.Values>
        {
            // enum members
            public enum Values
            {
                CouldNotRetrieveStorageDeviceNumbers,
                DiskInUse,
                DriveHasNoDriveLetter,
                Win32Error/*(int win32ErrorCode)*/
            }

            // functions to create member instances
            public static EjectDriveMediaError CouldNotRetrieveStorageDeviceNumbers => new EjectDriveMediaError(Values.CouldNotRetrieveStorageDeviceNumbers);
            public static EjectDriveMediaError DiskInUse => new EjectDriveMediaError(Values.DiskInUse);
            public static EjectDriveMediaError DriveHasNoDriveLetter => new EjectDriveMediaError(Values.DriveHasNoDriveLetter);
            public static EjectDriveMediaError Win32Error(int win32ErrorCode) => new EjectDriveMediaError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private EjectDriveMediaError(Values value) : base(value) { }
        }
        //
        public async Task<IMorphicResult<MorphicUnit, EjectDriveMediaError>> EjectDriveMediaAsync()
        {
            // NOTE: CD-ROMs cannot eject unless we switch from using the drive's device path to the drive letter 
            // NOTE: In all of our testing, 100% of drives that could be ejected with their device letter could also be ejected with their drive letter (if they had a drive letter),
            //       so we have standardized on this mechanism.  If it turns out that some disks cannot be ejected by their drive letter but COULD be ejected by their device path,
            //       we can modify our logic accordingly.

            var convertToDriveLetterResult = await Drive.TryConvertDevicePathToDosDriveLetterAsync(_device.DevicePath!);
            if (convertToDriveLetterResult.IsError == true)
            {
                switch (convertToDriveLetterResult.Error!.Value)
                {
                    case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                        return IMorphicResult<MorphicUnit, EjectDriveMediaError>.ErrorResult(EjectDriveMediaError.CouldNotRetrieveStorageDeviceNumbers);
                    case StorageDeviceNumberError.Values.Win32Error:
                        return IMorphicResult<MorphicUnit, EjectDriveMediaError>.ErrorResult(EjectDriveMediaError.Win32Error(convertToDriveLetterResult.Error!.Win32ErrorCode!.Value));
                }
            }

            string pathToEject;

            var driveLetter = convertToDriveLetterResult.Value;
            if (driveLetter != null)
            {
                // convert the drive letter to a path (e.g. 'E' => '\\.\E:')
                pathToEject = Drive.ConvertDriveLetterToDriveLetterPath(driveLetter.Value);
            }
            else
            {
                pathToEject = _device.DevicePath!;
            }

            return await Drive.EjectDriveMediaAsync(pathToEject);
        }
        //
        // NOTE: this function ejects the media from the drive (i.e. for CD-ROM drives...but this can also work to "eject" USB drives without unmounting their drive letters, exhibiting the same behavior as when right-clicking 'eject drive' on a USB drive in Windows Explorer)
        private static async Task<IMorphicResult<MorphicUnit, EjectDriveMediaError>> EjectDriveMediaAsync(string path)
        {
            // when we clean up, we will need to unlock the volume; set up that delegate now
            Func<PInvoke.Kernel32.SafeObjectHandle, Task<EjectDriveMediaError?>> unlockVolumeAsync = async delegate(PInvoke.Kernel32.SafeObjectHandle deviceHandle)
            {
                try
                {
                    // attempt to lock the storage device; if this fails, it means that the disk is in use
                    _ = await PInvoke.Kernel32.DeviceIoControlAsync<byte /* for null */, byte /* for null */>(
                        hDevice: deviceHandle,
                        dwIoControlCode: ExtendedPInvoke.FSCTL_UNLOCK_VOLUME,
                        inBuffer: null,
                        outBuffer: null,
                        cancellationToken: System.Threading.CancellationToken.None);

                }
                catch (PInvoke.Win32Exception ex)
                {
                    // NOTE: in our testing, we got an exception with NativeErrorCode 1 (ERROR_INVALID_FUNCTION) when trying to eject a CD-ROM which was in use;
                    //       this seems like an odd error to get back, so we're just passing it through for now rather than returning an error of DiskInUse.
                    return EjectDriveMediaError.Win32Error((int)ex.NativeErrorCode);
                }

                return null;
            };

            // NOTE: IOCTL_STORAGE_EJECT_MEDIA does not specify which access or sharing is required, so we have just assumed some defaults here (and then tested to make sure they worked in our tests)
            // see: https://docs.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-ioctl_storage_eject_media
            var deviceHandle = PInvoke.Kernel32.CreateFile(path, PInvoke.Kernel32.ACCESS_MASK.GenericRight.GENERIC_READ | PInvoke.Kernel32.ACCESS_MASK.GenericRight.GENERIC_WRITE, PInvoke.Kernel32.FileShare.FILE_SHARE_READ | PInvoke.Kernel32.FileShare.FILE_SHARE_WRITE, IntPtr.Zero, PInvoke.Kernel32.CreationDisposition.OPEN_EXISTING, (PInvoke.Kernel32.CreateFileFlags)0, PInvoke.Kernel32.SafeObjectHandle.Null);
            if (deviceHandle.IsInvalid == true)
            {
                var win32ErrorCode = Marshal.GetLastWin32Error();
                switch (win32ErrorCode)
                {
                    case (int)PInvoke.Win32ErrorCode.ERROR_SHARING_VIOLATION:
                        return IMorphicResult<MorphicUnit, EjectDriveMediaError>.ErrorResult(EjectDriveMediaError.DiskInUse);
                    default:
                        return IMorphicResult<MorphicUnit, EjectDriveMediaError>.ErrorResult(EjectDriveMediaError.Win32Error(win32ErrorCode));
                }
            }
            //
            try
            {
                try
                {
                    // attempt to lock the storage device; if this fails, it means that the disk is in use
                    _ = await PInvoke.Kernel32.DeviceIoControlAsync<byte /* for null */, byte /* for null */>(
                        hDevice: deviceHandle,
                        dwIoControlCode: ExtendedPInvoke.FSCTL_LOCK_VOLUME,
                        inBuffer: null,
                        outBuffer: null,
                        cancellationToken: System.Threading.CancellationToken.None);

                }
                catch (PInvoke.Win32Exception ex)
                {
                    switch (ex.NativeErrorCode)
                    {
                        case PInvoke.Win32ErrorCode.ERROR_ACCESS_DENIED:
                            return IMorphicResult<MorphicUnit, EjectDriveMediaError>.ErrorResult(EjectDriveMediaError.DiskInUse);
                        default:
                            return IMorphicResult<MorphicUnit, EjectDriveMediaError>.ErrorResult(EjectDriveMediaError.Win32Error((int)ex.NativeErrorCode));
                    }
                }
                var requiresAttemptToUnlock = true;

                try
                {
                    try
                    {
                        // eject the media for this storage device
                        // NOTE: we use IOCTL_STORAGE_EJECT_MEDIA instead of FSCTL_DISMOUNT_VOLUME because FSCTL_DISMOUNT_VOLUME will attempt a dismount even if the media is currently in use
                        _ = await PInvoke.Kernel32.DeviceIoControlAsync<byte /* for null */, byte /* for null */>(
                            hDevice: deviceHandle,
                            dwIoControlCode: ExtendedPInvoke.IOCTL_STORAGE_EJECT_MEDIA,
                            inBuffer: null,
                            outBuffer: null,
                            cancellationToken: System.Threading.CancellationToken.None);
                    }
                    catch (PInvoke.Win32Exception ex)
                    {
                        // NOTE: in our testing, we got an exception with NativeErrorCode 1 (ERROR_INVALID_FUNCTION) when trying to eject a CD-ROM which was in use;
                        //       this seems like an odd error to get back, so we're just passing it through for now rather than returning an error of DiskInUse.
                        return IMorphicResult<MorphicUnit, EjectDriveMediaError>.ErrorResult(EjectDriveMediaError.Win32Error((int)ex.NativeErrorCode));
                    }

                    // unlock the storage device
                    var unlockError = await unlockVolumeAsync(deviceHandle);
                    requiresAttemptToUnlock = false;
                    if (unlockError != null)
                    {
                        return IMorphicResult<MorphicUnit, EjectDriveMediaError>.ErrorResult(unlockError);
                    }
                }
                finally
                {
                    // if we aborted before we could unlock the storage device, try to unlock it now
                    if (requiresAttemptToUnlock == true)
                    {
                        // NOTE: we swallow any errors, as we're in a finally block and if we're executing this code it means that the function already terminated early
                        _ = await unlockVolumeAsync(deviceHandle);
                        requiresAttemptToUnlock = false;
                    }
                }
            }
            finally
            {
                deviceHandle.Close();
            }

            return IMorphicResult<MorphicUnit, EjectDriveMediaError>.SuccessResult(new MorphicUnit());
        }

        public async Task<IMorphicResult<string?, TryGetDriveLetterError>> TryGetDriveRootPathAsync()
        {
            var tryGetDriveLetterResult = await this.TryGetDriveLetterAsync();
            if (tryGetDriveLetterResult.IsError == true) {
                return IMorphicResult<string?, TryGetDriveLetterError>.ErrorResult(tryGetDriveLetterResult.Error!);
            }
            var driveLetter = tryGetDriveLetterResult.Value;

            if (driveLetter == null)
            {
                return IMorphicResult<string?, TryGetDriveLetterError>.SuccessResult(null);
            }
            else
            {
                var driveRootPath = driveLetter + @":\";
                return IMorphicResult<string?, TryGetDriveLetterError>.SuccessResult(driveRootPath);
            }
        }

        private static string ConvertDriveLetterToDriveLetterPath(char driveLetter)
        {
            return @"\\.\" + driveLetter + ":";
        }

        // NOTE: some drives (like CD-ROM drives) are volumes which don't have a corresponding "disk", so this function can return a successful result of null
        public async Task<IMorphicResult<Disk?, Disk.GetDisksError>> GetDiskAsync()
        {
            // get a list of all of the disks (so we can compare these against our storage device number)
            var getAllDisksResult = await Morphic.Windows.Native.Devices.Disk.GetAllDisksAsync();
            if (getAllDisksResult.IsError == true)
            {
                return IMorphicResult<Disk?, Disk.GetDisksError>.ErrorResult(getAllDisksResult.Error!);
            }
            var allDisks = getAllDisksResult.Value!;

            // filter out the drives which don't have the same storage device number as our disk
            var disksForThisDrive = allDisks.Where(disk => ((this.StorageDeviceNumber.DeviceType == disk.StorageDeviceNumber.DeviceType) && (this.StorageDeviceNumber.DeviceNumber == disk.StorageDeviceNumber.DeviceNumber)));
            var numberOfDisksForThisDrive = disksForThisDrive.Count();
            switch (numberOfDisksForThisDrive)
            {
                case 0:
                    // this drive has no corresponding disk (e.g. CD-ROMs)
                    return IMorphicResult<Disk?, Disk.GetDisksError>.SuccessResult(null);
                case 1:
                    // this drive has exactly one disk (the normal case)
                    return IMorphicResult<Disk?, Disk.GetDisksError>.SuccessResult(disksForThisDrive.First());
                default:
                    // this is an unexpected error condition
                    Debug.Assert(false, "Drive has multiple disks; this should not be possible.");
                    // fail gracefully, returning success but indicating the the drive has no corresponding disk
                    return IMorphicResult<Disk?, Disk.GetDisksError>.SuccessResult(null);
            }
        }

        public async Task<IMorphicResult<bool, Win32ApiError>> GetIsMountedAsync()
        {
            var deviceHandle = PInvoke.Kernel32.CreateFile(_device.DevicePath, PInvoke.Kernel32.ACCESS_MASK.GenericRight.GENERIC_READ | PInvoke.Kernel32.ACCESS_MASK.GenericRight.GENERIC_WRITE, PInvoke.Kernel32.FileShare.FILE_SHARE_READ | PInvoke.Kernel32.FileShare.FILE_SHARE_WRITE, IntPtr.Zero, PInvoke.Kernel32.CreationDisposition.OPEN_EXISTING, (PInvoke.Kernel32.CreateFileFlags)0, PInvoke.Kernel32.SafeObjectHandle.Null);
            if (deviceHandle.IsInvalid == true)
            {
                var win32ErrorCode = Marshal.GetLastWin32Error();
                return IMorphicResult<bool, Win32ApiError>.ErrorResult(Win32ApiError.Win32Error(win32ErrorCode));
            }
            try
            {
                try
                {
                    // attempt to see if the volume is mounted; this should only work on volumes which are mounted (and not locked)
                    _ = await PInvoke.Kernel32.DeviceIoControlAsync<byte /* for null */, byte /* for null */>(
                        hDevice: deviceHandle,
                        dwIoControlCode: ExtendedPInvoke.FSCTL_IS_VOLUME_MOUNTED,
                        inBuffer: null,
                        outBuffer: null,
                        cancellationToken: System.Threading.CancellationToken.None);

                    // if the above call passed successfully, the volume is mounted
                    return IMorphicResult<bool, Win32ApiError>.SuccessResult(true);
                }
                catch (PInvoke.Win32Exception ex)
                {
                    switch (ex.NativeErrorCode)
                    {
                        // based on our testing, an unmounted volume will return ERROR_INVALID_PARAMETER
                        case PInvoke.Win32ErrorCode.ERROR_INVALID_PARAMETER:
                            return IMorphicResult<bool, Win32ApiError>.SuccessResult(false);
                        default:
                            return IMorphicResult<bool, Win32ApiError>.ErrorResult(Win32ApiError.Win32Error((int)ex.NativeErrorCode));
                    }
                }
            }
            finally
            {
                deviceHandle.Close();
            }
        }
    }
}
