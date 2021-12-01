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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native.Devices
{
    // NOTE: the Disk class refers to the underlying disk device (e.g. a physical USB drive, not the drive volume)
    public class Disk
    {
        private Device _device;
        internal ExtendedPInvoke.STORAGE_DEVICE_NUMBER StorageDeviceNumber { get; private set; }

        private Disk(Device device, ExtendedPInvoke.STORAGE_DEVICE_NUMBER storageDeviceNumber)
        {
            _device = device;
            this.StorageDeviceNumber = storageDeviceNumber;
        }

        public record GetDisksError : MorphicAssociatedValueEnum<GetDisksError.Values>
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
            public static GetDisksError ConfigManagerError(uint configManagerErrorCode) => new GetDisksError(Values.ConfigManagerError) { ConfigManagerErrorCode = configManagerErrorCode };
            public static GetDisksError CouldNotEnumerateViaWin32Api => new GetDisksError(Values.CouldNotEnumerateViaWin32Api);
            public static GetDisksError CouldNotGetDeviceCapabilities => new GetDisksError(Values.CouldNotGetDeviceCapabilities);
            public static GetDisksError CouldNotGetDeviceInstanceId => new GetDisksError(Values.CouldNotGetDeviceInstanceId);
            public static GetDisksError CouldNotRetrieveStorageDeviceNumbers => new GetDisksError(Values.CouldNotRetrieveStorageDeviceNumbers);
            public static GetDisksError Win32Error(int win32ErrorCode) => new GetDisksError(Values.Win32Error) { Win32ErrorCode = win32ErrorCode };

            // associated values
            public uint? ConfigManagerErrorCode { get; private set; }
            public int? Win32ErrorCode { get; private set; }

            // verbatim required constructor implementation for MorphicAssociatedValueEnums
            private GetDisksError(Values value) : base(value) { }
        }
        //
        public static async Task<MorphicResult<List<Disk>, GetDisksError>> GetAllDisksAsync()
        {
            var allDisks = new List<Disk>();

            // NOTE: GUID_DEVINTERFACE_DISK will only capture physical disks and will filter out non-disks such as CD-ROMs
            var allDiskDevicesResult = Morphic.Windows.Native.Devices.Device.GetDevicesForClassGuid(ExtendedPInvoke.GUID_DEVINTERFACE_DISK);
            if (allDiskDevicesResult.IsError == true)
            {
                switch (allDiskDevicesResult.Error!.Value)
                {
                    case Device.GetDevicesForClassGuidError.Values.ConfigManagerError:
                        return MorphicResult.ErrorResult(GetDisksError.ConfigManagerError(allDiskDevicesResult.Error!.ConfigManagerErrorCode!.Value));
                    case Device.GetDevicesForClassGuidError.Values.CouldNotEnumerateViaWin32Api:
                        return MorphicResult.ErrorResult(GetDisksError.CouldNotEnumerateViaWin32Api);
                    case Device.GetDevicesForClassGuidError.Values.CouldNotGetDeviceCapabilities:
                        return MorphicResult.ErrorResult(GetDisksError.CouldNotGetDeviceCapabilities);
                    case Device.GetDevicesForClassGuidError.Values.CouldNotGetDeviceInstanceId:
                        return MorphicResult.ErrorResult(GetDisksError.CouldNotGetDeviceInstanceId);
                    case Device.GetDevicesForClassGuidError.Values.Win32Error:
                        return MorphicResult.ErrorResult(GetDisksError.Win32Error(allDiskDevicesResult.Error!.Win32ErrorCode!.Value));
                }
            }

            // create a drive object for each disk
            foreach (var diskDevice in allDiskDevicesResult.Value!)
            {
                // capture the storage device number for each disk
                var diskStorageDeviceNumberResult = await StorageDeviceUtils.GetStorageDeviceNumberAsync(diskDevice.DevicePath!);
                if (diskStorageDeviceNumberResult.IsError == true)
                {
                    switch (diskStorageDeviceNumberResult.Error!.Value)
                    {
                        case StorageDeviceNumberError.Values.CouldNotRetrieveStorageDeviceNumbers:
                            return MorphicResult.ErrorResult(GetDisksError.CouldNotRetrieveStorageDeviceNumbers);
                        case StorageDeviceNumberError.Values.Win32Error:
                            return MorphicResult.ErrorResult(GetDisksError.Win32Error(allDiskDevicesResult.Error!.Win32ErrorCode!.Value));
                    }
                }
                var storageDeviceNumber = diskStorageDeviceNumberResult.Value!;

                var disk = new Disk(diskDevice, storageDeviceNumber);
                allDisks.Add(disk);
            }

            return MorphicResult.OkResult(allDisks);
        }

        // NOTE: for a Disk, "IsRemovable" means that the hardware can be removed (i.e. a USB drive can be removed; we're not talking about physically ejecting media out of a drive)
        //       [in other words, this will return true for USB drives, but false for USB CD-ROMs]
        public MorphicResult<bool, Device.GetParentOrChildError> GetIsRemovable()
        {
            return _device.GetIsDeviceOrAncestorsRemovable();
        }

        public MorphicResult<MorphicUnit, Device.SafelyRemoveDeviceError> SafelyRemoveDevice()
        {
            return _device.SafelyRemoveDevice();
        }

        public async Task<MorphicResult<IEnumerable<Drive>, Drive.GetDrivesError>> GetDrivesAsync()
        {
            // get a list of all of the drives (so we can compare these against our storage device number)
            var getAllDrivesResult = await Morphic.Windows.Native.Devices.Drive.GetAllDrivesAsync();
            if (getAllDrivesResult.IsError == true)
            {
                return MorphicResult.ErrorResult(getAllDrivesResult.Error!);
            }
            var allDrives = getAllDrivesResult.Value!;

            // filter out the drives which don't have the same storage device number as our disk
            var drivesOfThisDisk = allDrives.Where(drive => ((this.StorageDeviceNumber.DeviceType == drive.StorageDeviceNumber.DeviceType) && (this.StorageDeviceNumber.DeviceNumber == drive.StorageDeviceNumber.DeviceNumber)));
            return MorphicResult.OkResult(drivesOfThisDisk!);
        }
    }
}
