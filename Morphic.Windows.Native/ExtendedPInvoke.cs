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

        internal static uint FSCTL_IS_VOLUME_MOUNTED => ExtendedPInvoke.CTL_CODE((uint)FILE_DEVICE_TYPE.FILE_DEVICE_FILE_SYSTEM, 10, METHOD_BUFFERED, FILE_ANY_ACCESS);
        internal static uint FSCTL_LOCK_VOLUME => ExtendedPInvoke.CTL_CODE((uint)FILE_DEVICE_TYPE.FILE_DEVICE_FILE_SYSTEM, 6, METHOD_BUFFERED, FILE_ANY_ACCESS);
        internal static uint FSCTL_UNLOCK_VOLUME => ExtendedPInvoke.CTL_CODE((uint)FILE_DEVICE_TYPE.FILE_DEVICE_FILE_SYSTEM, 7, METHOD_BUFFERED, FILE_ANY_ACCESS);

        internal static Guid GUID_DEVINTERFACE_DISK => new Guid(0x53f56307, 0xb6bf, 0x11d0, 0x94, 0xf2, 0x00, 0xa0, 0xc9, 0x1e, 0xfb, 0x8b);
        internal static Guid GUID_DEVINTERFACE_VOLUME => new Guid(0x53f5630d, 0xb6bf, 0x11d0, 0x94, 0xf2, 0x00, 0xa0, 0xc9, 0x1e, 0xfb, 0x8b);

        const uint IOCTL_STORAGE_BASE = FILE_DEVICE_MASS_STORAGE;

        const uint METHOD_BUFFERED = 0;

        internal static uint IOCTL_STORAGE_GET_DEVICE_NUMBER => ExtendedPInvoke.CTL_CODE(IOCTL_STORAGE_BASE, 0x0420, METHOD_BUFFERED, FILE_ANY_ACCESS);
        internal static uint IOCTL_STORAGE_EJECT_MEDIA => ExtendedPInvoke.CTL_CODE(IOCTL_STORAGE_BASE, 0x0202, METHOD_BUFFERED, FILE_READ_ACCESS);

        internal enum FILE_DEVICE_TYPE: uint
        {
            //FILE_DEVICE_BEEP = 0x00000001,
            FILE_DEVICE_CD_ROM = 0x00000002,
            //FILE_DEVICE_CD_ROM_FILE_SYSTEM = 0x00000003,
            //FILE_DEVICE_CONTROLLER = 0x00000004,
            //FILE_DEVICE_DATALINK = 0x00000005,
            //FILE_DEVICE_DFS = 0x00000006,
            FILE_DEVICE_DISK = 0x00000007,
            //FILE_DEVICE_DISK_FILE_SYSTEM = 0x00000008,
            FILE_DEVICE_FILE_SYSTEM = 0x00000009,
            //FILE_DEVICE_INPORT_PORT = 0x0000000a,
            //FILE_DEVICE_KEYBOARD = 0x0000000b,
            //FILE_DEVICE_MAILSLOT = 0x0000000c,
            //FILE_DEVICE_MIDI_IN = 0x0000000d,
            //FILE_DEVICE_MIDI_OUT = 0x0000000e,
            //FILE_DEVICE_MOUSE = 0x0000000f,
            //FILE_DEVICE_MULTI_UNC_PROVIDER = 0x00000010,
            //FILE_DEVICE_NAMED_PIPE = 0x00000011,
            //FILE_DEVICE_NETWORK = 0x00000012,
            //FILE_DEVICE_NETWORK_BROWSER = 0x00000013,
            //FILE_DEVICE_NETWORK_FILE_SYSTEM = 0x00000014,
            //FILE_DEVICE_NULL = 0x00000015,
            //FILE_DEVICE_PARALLEL_PORT = 0x00000016,
            //FILE_DEVICE_PHYSICAL_NETCARD = 0x00000017,
            //FILE_DEVICE_PRINTER = 0x00000018,
            //FILE_DEVICE_SCANNER = 0x00000019,
            //FILE_DEVICE_SERIAL_MOUSE_PORT = 0x0000001a,
            //FILE_DEVICE_SERIAL_PORT = 0x0000001b,
            //FILE_DEVICE_SCREEN = 0x0000001c,
            //FILE_DEVICE_SOUND = 0x0000001d,
            //FILE_DEVICE_STREAMS = 0x0000001e,
            //FILE_DEVICE_TAPE = 0x0000001f,
            //FILE_DEVICE_TAPE_FILE_SYSTEM = 0x00000020,
            //FILE_DEVICE_TRANSPORT = 0x00000021,
            //FILE_DEVICE_UNKNOWN = 0x00000022,
            //FILE_DEVICE_VIDEO = 0x00000023,
            //FILE_DEVICE_VIRTUAL_DISK = 0x00000024,
            //FILE_DEVICE_WAVE_IN = 0x00000025,
            //FILE_DEVICE_WAVE_OUT = 0x00000026,
            //FILE_DEVICE_8042_PORT = 0x00000027,
            //FILE_DEVICE_NETWORK_REDIRECTOR = 0x00000028,
            //FILE_DEVICE_BATTERY = 0x00000029,
            //FILE_DEVICE_BUS_EXTENDER = 0x0000002a,
            //FILE_DEVICE_MODEM = 0x0000002b,
            //FILE_DEVICE_VDM = 0x0000002c,
            //FILE_DEVICE_MASS_STORAGE = 0x0000002d,
            //FILE_DEVICE_SMB = 0x0000002e,
            //FILE_DEVICE_KS = 0x0000002f,
            //FILE_DEVICE_CHANGER = 0x00000030,
            //FILE_DEVICE_SMARTCARD = 0x00000031,
            //FILE_DEVICE_ACPI = 0x00000032,
            //FILE_DEVICE_DVD = 0x00000033,
            //FILE_DEVICE_FULLSCREEN_VIDEO = 0x00000034,
            //FILE_DEVICE_DFS_FILE_SYSTEM = 0x00000035,
            //FILE_DEVICE_DFS_VOLUME = 0x00000036,
            //FILE_DEVICE_SERENUM = 0x00000037,
            //FILE_DEVICE_TERMSRV = 0x00000038,
            //FILE_DEVICE_KSEC = 0x00000039,
            //FILE_DEVICE_FIPS = 0x0000003A,
            //FILE_DEVICE_INFINIBAND = 0x0000003B,
            //FILE_DEVICE_VMBUS = 0x0000003E,
            //FILE_DEVICE_CRYPT_PROVIDER = 0x0000003F,
            //FILE_DEVICE_WPD = 0x00000040,
            //FILE_DEVICE_BLUETOOTH = 0x00000041,
            //FILE_DEVICE_MT_COMPOSITE = 0x00000042,
            //FILE_DEVICE_MT_TRANSPORT = 0x00000043,
            //FILE_DEVICE_BIOMETRIC = 0x00000044,
            //FILE_DEVICE_PMI = 0x00000045,
            //FILE_DEVICE_EHSTOR = 0x00000046,
            //FILE_DEVICE_DEVAPI = 0x00000047,
            //FILE_DEVICE_GPIO = 0x00000048,
            //FILE_DEVICE_USBEX = 0x00000049,
            //FILE_DEVICE_CONSOLE = 0x00000050,
            //FILE_DEVICE_NFP = 0x00000051,
            //FILE_DEVICE_SYSENV = 0x00000052,
            //FILE_DEVICE_VIRTUAL_BLOCK = 0x00000053,
            //FILE_DEVICE_POINT_OF_SERVICE = 0x00000054,
            //FILE_DEVICE_STORAGE_REPLICATION = 0x00000055,
            //FILE_DEVICE_TRUST_ENV = 0x00000056,
            //FILE_DEVICE_UCM = 0x00000057,
            //FILE_DEVICE_UCMTCPCI = 0x00000058,
            //FILE_DEVICE_PERSISTENT_MEMORY = 0x00000059,
            //FILE_DEVICE_NVDIMM = 0x0000005a,
            //FILE_DEVICE_HOLOGRAPHIC = 0x0000005b,
            //FILE_DEVICE_SDFXHCI = 0x0000005c,
            //FILE_DEVICE_UCMUCSI = 0x0000005d,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct STORAGE_DEVICE_NUMBER
        {
            public FILE_DEVICE_TYPE DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        }

        #endregion winioctl.h
    }
}
