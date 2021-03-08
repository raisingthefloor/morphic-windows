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
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Morphic.Windows.Native.Devices.Utils
{
    internal record StorageDeviceNumberError : MorphicAssociatedValueEnum<StorageDeviceNumberError.Values>
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
    internal struct StorageDeviceUtils {
        internal static async Task<IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>> GetStorageDeviceNumberAsync(string devicePath)
        {
            var deviceHandle = PInvoke.Kernel32.CreateFile(devicePath, (PInvoke.Kernel32.ACCESS_MASK)0, PInvoke.Kernel32.FileShare.FILE_SHARE_READ, IntPtr.Zero, PInvoke.Kernel32.CreationDisposition.OPEN_EXISTING, (PInvoke.Kernel32.CreateFileFlags)0, PInvoke.Kernel32.SafeObjectHandle.Null);
            if (deviceHandle.IsInvalid == true)
            {
                var win32ErrorCode = Marshal.GetLastWin32Error();
                return IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error(win32ErrorCode));
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
                    return IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.Win32Error((int)ex.NativeErrorCode));
                }

                var storageDeviceNumberAsArray = storageDeviceNumberMemoryObject.ToArray();
                if (storageDeviceNumberAsArray.Length != 1)
                {
                    return IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>.ErrorResult(StorageDeviceNumberError.CouldNotRetrieveStorageDeviceNumbers);
                }
                storageDeviceNumber = storageDeviceNumberAsArray[0];
            }
            finally
            {
                deviceHandle.Close();
            }

            return IMorphicResult<ExtendedPInvoke.STORAGE_DEVICE_NUMBER, StorageDeviceNumberError>.SuccessResult(storageDeviceNumber);
        }
    }
}