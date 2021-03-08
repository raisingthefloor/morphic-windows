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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Windows.Native
{
    internal struct ExtendedPInvoke
    {
        #region cfgmgr32.h

        [Flags]
        internal enum CmDeviceCapabilitiesFlags: uint
        {
            LockSupported = 0x00000001,
            EjectSupported = 0x00000002,
            Removable = 0x00000004,
            DocDevice = 0x00000008,
            UniqueID = 0x00000010,
            SilentInstall = 0x00000020,
            RawDeviceOK = 0x00000040,
            SurpriseRemovalOk = 0x00000080,
            HardwareDisabled = 0x00000100, // NOTE: DEVICE_CAPABILITIES has this shifted << 4 (i.e. 0x10000)
            NonDynamic = 0x00000200, // NOTE: DEVICE_CAPABILITIES has this shifted << 4 (i.e. 0x2000)
            SecureDevice = 0x00000400 // NOTE: DEVICE_CAPABILITIES has this shifted << 4 (i.e. 0x4000)
        }

        // config manager success/error codes
        internal enum CR_RESULT: uint
        {
            CR_SUCCESS = 0x00000000,
            //CR_DEFAULT = 0x00000001,
            //CR_OUT_OF_MEMORY = 0x00000002,
            //CR_INVALID_POINTER = 0x00000003,
            //CR_INVALID_FLAG = 0x00000004,
            CR_INVALID_DEVNODE = 0x00000005,
            //CR_INVALID_DEVINST = CR_INVALID_DEVNODE,
            //CR_INVALID_RES_DES = 0x00000006,
            //CR_INVALID_LOG_CONF = 0x00000007,
            //CR_INVALID_ARBITRATOR = 0x00000008,
            //CR_INVALID_NODELIST = 0x00000009,
            //CR_DEVNODE_HAS_REQS = 0x0000000A,
            //CR_DEVINST_HAS_REQS = CR_DEVNODE_HAS_REQS,
            //CR_INVALID_RESOURCEID = 0x0000000B,
            //CR_DLVXD_NOT_FOUND = 0x0000000C,
            CR_NO_SUCH_DEVNODE = 0x0000000D,
            //CR_NO_SUCH_DEVINST = CR_NO_SUCH_DEVNODE,
            //CR_NO_MORE_LOG_CONF = 0x0000000E,
            //CR_NO_MORE_RES_DES = 0x0000000F,
            //CR_ALREADY_SUCH_DEVNODE = 0x00000010,
            //CR_ALREADY_SUCH_DEVINST = CR_ALREADY_SUCH_DEVNODE,
            //CR_INVALID_RANGE_LIST = 0x00000011,
            //CR_INVALID_RANGE = 0x00000012,
            //CR_FAILURE = 0x00000013,
            //CR_NO_SUCH_LOGICAL_DEV = 0x00000014,
            //CR_CREATE_BLOCKED = 0x00000015,
            //CR_NOT_SYSTEM_VM = 0x00000016,
            CR_REMOVE_VETOED = 0x00000017,
            //CR_APM_VETOED = 0x00000018,
            //CR_INVALID_LOAD_TYPE = 0x00000019,
            //CR_BUFFER_SMALL = 0x0000001A,
            //CR_NO_ARBITRATOR = 0x0000001B,
            //CR_NO_REGISTRY_HANDLE = 0x0000001C,
            //CR_REGISTRY_ERROR = 0x0000001D,
            //CR_INVALID_DEVICE_ID = 0x0000001E,
            //CR_INVALID_DATA = 0x0000001F,
            //CR_INVALID_API = 0x00000020,
            //CR_DEVLOADER_NOT_READY = 0x00000021,
            //CR_NEED_RESTART = 0x00000022,
            //CR_NO_MORE_HW_PROFILES = 0x00000023,
            //CR_DEVICE_NOT_THERE = 0x00000024,
            //CR_NO_SUCH_VALUE = 0x00000025,
            //CR_WRONG_TYPE = 0x00000026,
            //CR_INVALID_PRIORITY = 0x00000027,
            //CR_NOT_DISABLEABLE = 0x00000028,
            //CR_FREE_RESOURCES = 0x00000029,
            //CR_QUERY_VETOED = 0x0000002A,
            //CR_CANT_SHARE_IRQ = 0x0000002B,
            //CR_NO_DEPENDENT = 0x0000002C,
            //CR_SAME_RESOURCES = 0x0000002D,
            //CR_NO_SUCH_REGISTRY_KEY = 0x0000002E,
            //CR_INVALID_MACHINENAME = 0x0000002F,
            //CR_REMOTE_COMM_FAILURE = 0x00000030,
            //CR_MACHINE_UNAVAILABLE = 0x00000031,
            //CR_NO_CM_SERVICES = 0x00000032,
            //CR_ACCESS_DENIED = 0x00000033,
            //CR_CALL_NOT_IMPLEMENTED = 0x00000034,
            //CR_INVALID_PROPERTY = 0x00000035,
            //CR_DEVICE_INTERFACE_ACTIVE = 0x00000036,
            //CR_NO_SUCH_DEVICE_INTERFACE = 0x00000037,
            //CR_INVALID_REFERENCE_STRING = 0x00000038,
            //CR_INVALID_CONFLICT_LIST = 0x00000039,
            //CR_INVALID_INDEX = 0x0000003A,
            //CR_INVALID_STRUCTURE_SIZE = 0x0000003B
        }

        internal enum PNP_VETO_TYPE: int
        {
            PNP_VetoTypeUnknown = 1,
            PNP_VetoLegacyDevice,
            PNP_VetoPendingClose,
            PNP_VetoWindowsApp,
            PNP_VetoWindowsService,
            PNP_VetoOutstandingOpen,
            PNP_VetoDevice,
            PNP_VetoDriver,
            PNP_VetoIllegalDeviceRequest,
            PNP_VetoInsufficientPower,
            PNP_VetoNonDisableable,
            PNP_VetoLegacyDriver,
            PNP_VetoInsufficientRights,
            PNP_VetoAlreadyRemoved
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern uint CM_Get_Child(out uint pdnDevInst, uint dnDevInst, int uFlags);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern uint CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, int ulFlags);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern uint CM_Get_Sibling(out uint pdnDevInst, uint dnDevInst, int uFlags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint CM_Get_Device_ID(uint dnDevInst, IntPtr buffer, int bufferLen, int ulFlags);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern uint CM_Get_Device_ID_Size(out int pulLen, uint dnDevInst, int ulFlags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint CM_Request_Device_Eject(uint dnDevInst, out int pVetoType, IntPtr pszVetoName, int ulNameLength, int ulFlags);

        #endregion cfgmgr32.h


        #region fileapi.h

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint QueryDosDevice(string? lpDeviceName, IntPtr lpTargetPath, uint ucchMax);

        #endregion fileapi.h


        #region minwindef.h

        internal const int MAX_PATH = 260;

        #endregion minwindef.h


        #region SetupApi.h

        internal const uint SPDRP_CAPABILITIES = 0x0000000F;

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SetupDiGetDeviceRegistryProperty(SafeHandle deviceInfoSet, ref PInvoke.SetupApi.SP_DEVINFO_DATA deviceInfoData, uint property, out UInt32 propertyRegDataType, IntPtr propertyBuffer, int propertyBufferSize, IntPtr requiredSize);

        #endregion SetupApi.h


        #region winioctl.h

        static uint CTL_CODE(uint deviceType, uint function, uint method, ushort access)
        {
            uint result = 0;
            result |= deviceType << 16;
            result |= (uint)access << 14;
            result |= function << 2;
            result |= method;

            return result;
        }

        const uint FILE_DEVICE_MASS_STORAGE = 0x0000002d;

        const ushort FILE_ANY_ACCESS = 0x0000;
        //const ushort FILE_SPECIAL_ACCESS = FILE_ANY_ACCESS;
        const ushort FILE_READ_ACCESS = 0x0001;
        //const ushort FILE_WRITE_ACCESS = 0x0002;

        internal static Guid GUID_DEVINTERFACE_DISK => new Guid(0x53f56307, 0xb6bf, 0x11d0, 0x94, 0xf2, 0x00, 0xa0, 0xc9, 0x1e, 0xfb, 0x8b);
        internal static Guid GUID_DEVINTERFACE_VOLUME => new Guid(0x53f5630d, 0xb6bf, 0x11d0, 0x94, 0xf2, 0x00, 0xa0, 0xc9, 0x1e, 0xfb, 0x8b);

        const uint IOCTL_STORAGE_BASE = FILE_DEVICE_MASS_STORAGE;

        const uint METHOD_BUFFERED = 0;

        internal static uint IOCTL_STORAGE_GET_DEVICE_NUMBER => ExtendedPInvoke.CTL_CODE(IOCTL_STORAGE_BASE, 0x0420, METHOD_BUFFERED, FILE_ANY_ACCESS);
        internal static uint IOCTL_STORAGE_EJECT_MEDIA => ExtendedPInvoke.CTL_CODE(IOCTL_STORAGE_BASE, 0x0202, METHOD_BUFFERED, FILE_READ_ACCESS);

        [StructLayout(LayoutKind.Sequential)]
        internal struct STORAGE_DEVICE_NUMBER
        {
            public uint DeviceType;
            public uint DeviceNumber;
            public uint PartitionNumber;
        }

        #endregion winioctl.h
    }
}
