// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
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
using System.Runtime.InteropServices;
using System.Text;

namespace Morphic.Windows.Native
{
    internal static class WindowsApi
    {
        // NOTE: SYSTEM_INFO Is used by GetSystemInfo and GetNativeSystemInfo
        // https://docs.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-system_info
        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_INFO
        {
            public SYSTEM_INFO__DUMMYUNIONNAME dummyUnion;
            public UInt32 dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public UInt32 dwNumberOfProcessors;
            public UInt32 dwProcessorType;
            public UInt32 dwAllocationGranularity;
            public UInt16 wProcessorLevel;
            public UInt16 wProcessorRevision;
        }
        //
        [StructLayout(LayoutKind.Explicit)]
        internal struct SYSTEM_INFO__DUMMYUNIONNAME
        {
            [FieldOffset(0)]
            public UInt32 dwOemId;
            //
            [FieldOffset(0)]
            public SYSTEM_INFO__DUMMYUNIONNAME__DUMMYSTRUCTNAME DUMMYSTRUCTNAME;

            [StructLayout(LayoutKind.Sequential)]
            public struct SYSTEM_INFO__DUMMYUNIONNAME__DUMMYSTRUCTNAME
            {
                public UInt16 wProcessorArchitecture;
                public UInt16 wReserved;
            }
        }

        // NOTE: ProcessArchitecture is used by SYSTEM_INFO
        internal enum ProcessorArchitecture : UInt16
        {
            ARM = 5,
            ARM64 = 12,
            // Neutral = 11,
            AMD64 = 9,
            IA32 = 0,
            // X86_ON_ARM64 = 14,
            UNKNOWN = 0xFFFF
        }

        // NOTE: SystemMetricIndex is used by GetSystemMetrics and GetSystemMetricsForDpi
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getsystemmetrics
        internal enum SystemMetricIndex : Int32
        {
            SM_CXSCREEN = 0,
            SM_CYSCREEN = 1,
            //SM_CXVSCROLL = 2,
            //SM_CYHSCROLL = 3,
            //SM_CYCAPTION = 4,
            //SM_CXBORDER = 5,
            //SM_CYBORDER = 6,
            //SM_CXDLGFRAME = 7,
            //SM_CXFIXEDFRAME = 7,
            //SM_CYDLGFRAME = 8,
            //SM_CYFIXEDFRAME = 8,
            //SM_CYVTHUMB = 9,
            //SM_CXHTHUMB = 10,
            //SM_CXICON = 11,
            //SM_CYICON = 12,
            //SM_CXCURSOR = 13,
            //SM_CYCURSOR = 14,
            //SM_CYMENU = 15,
            SM_CXFULLSCREEN = 16,
            SM_CYFULLSCREEN = 17,
            //SM_CYKANJIWINDOW = 18,
            SM_MOUSEPRESENT = 19,
            //SM_CYVSCROLL = 20,
            //SM_CXHSCROLL = 21,
            //SM_DEBUG = 22,
            SM_SWAPBUTTON = 23,
            //SM_CXMIN = 28,
            //SM_CYMIN = 29,
            //SM_CXSIZE = 30,
            //SM_CYSIZE = 31,
            //SM_CXFRAME = 32,
            //SM_CXSIZEFRAME = 32,
            //SM_CYFRAME = 33,
            //SM_CYSIZEFRAME = 33,
            //SM_CXMINTRACK = 34,
            //SM_CYMINTRACK = 35,
            //SM_CXDOUBLECLK = 36,
            //SM_CYDOUBLECLK = 37,
            //SM_CXICONSPACING = 38,
            //SM_CYICONSPACING = 39,
            //SM_MENUDROPALIGNMENT = 40,
            //SM_PENWINDOWS = 41,
            //SM_DBCSENABLED = 42,
            //SM_CMOUSEBUTTONS = 43,
            //SM_SECURE = 44,
            //SM_CXEDGE = 45,
            //SM_CYEDGE = 46,
            //SM_CXMINSPACING = 47,
            //SM_CYMINSPACING = 48,
            //SM_CXSMICON = 49,
            //SM_CYSMICON = 50,
            //SM_CYSMCAPTION = 51,
            //SM_CXSMSIZE = 52,
            //SM_CYSMSIZE = 53,
            //SM_CXMENUSIZE = 54,
            //SM_CYMENUSIZE = 55,
            //SM_ARRANGE = 56,
            //SM_CXMINIMIZED = 57,
            //SM_CYMINIMIZED = 58,
            //SM_CXMAXTRACK = 59,
            //SM_CYMAXTRACK = 60,
            //SM_CXMAXIMIZED = 61,
            //SM_CYMAXIMIZED = 62,
            //SM_NETWORK = 63,
            //SM_CLEANBOOT = 67,
            //SM_CXDRAG = 68,
            //SM_CYDRAG = 69,
            //SM_SHOWSOUNDS = 70,
            //SM_CXMENUCHECK = 71,
            //SM_CYMENUCHECK = 72,
            //SM_SLOWMACHINE = 73,
            //SM_MIDEASTENABLED = 74,
            //SM_MOUSEWHEELPRESENT = 75,
            SM_XVIRTUALSCREEN = 76,
            SM_YVIRTUALSCREEN = 77,
            SM_CXVIRTUALSCREEN = 78,
            SM_CYVIRTUALSCREEN = 79,
            SM_CMONITORS = 80,
            //SM_SAMEDISPLAYFORMAT = 81,
            //SM_IMMENABLED = 82,
            //SM_CXFOCUSBORDER = 83,
            //SM_CYFOCUSBORDER = 84,
            //SM_TABLETPC = 86,
            //SM_MEDIACENTER = 87,
            //SM_STARTER = 88,
            //SM_SERVERR2 = 89,
            //SM_MOUSEHORIZONTALWHEELPRESENT = 91,
            //SM_CXPADDEDBORDER = 92,
            //SM_DIGITIZER = 94,
            //SM_MAXIMUMTOUCHES = 95,
            //SM_REMOTESESSION = 0x1000,
            //SM_SHUTTINGDOWN = 0x2000,
            //SM_REMOTECONTROL = 0x2001,
            //SM_CONVERTIBLESLATEMODE = 0x2003,
            //SM_SYSTEMDOCKED = 0x2004,
        }

        private const Int32 CCHDEVICENAME = 32;
        private const Int32 CCHFORMNAME = 32;

        // DEVMODEW is used by EnumDisplaySettingsEx and other functions
        // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-devmodew
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DEVMODEW
        {
            private const Int32 privateDriverDataLength = 0;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCHDEVICENAME)]
            public Char[] dmDeviceName;
            public UInt16 dmSpecVersion;
            public UInt16 dmDriverVersion;
            public UInt16 dmSize;
            public UInt16 dmDriverExtra;
            public DM_FieldSelectionBit dmFields;
            public DEVMODEW__DUMMYUNIONNAME DUMMYUNIONNAME;
            public Int16 dmColor;
            public Int16 dmDuplex;
            public Int16 dmYResolution;
            public Int16 dmTTOption;
            public Int16 dmCollate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCHFORMNAME)]
            public Char[] dmFormName;
            public UInt16 dmLogPixels;
            public UInt32 dmBitsPerPel;
            public UInt32 dmPelsWidth;
            public UInt32 dmPelsHeight;
            public DEVMODEW__DUMMYUNIONNAME2 DUMMYUNIONNAME2;
            public UInt32 dmDisplayFrequency;
            public UInt32 dmICMMethod;
            public UInt32 dmICMIntent;
            public UInt32 dmMediaType;
            public UInt32 dmDitherType;
            public UInt32 dmReserved1;
            public UInt32 dmReserved2;
            public UInt32 dmPanningWidth;
            public UInt32 dmPanningHeight;
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = privateDriverDataLength)]
            //public Byte[] privateDriverData;

            public void Init()
            {
                this.dmDeviceName = new Char[CCHDEVICENAME];
                this.dmFormName = new Char[CCHFORMNAME];
                //this.privateDriverData = new byte[privateDriverDataLength];
                this.dmDriverExtra = privateDriverDataLength;
                this.dmSize = (UInt16)Marshal.SizeOf(typeof(DEVMODEW));
            }
        }
        //
        [StructLayout(LayoutKind.Explicit)]
        internal struct DEVMODEW__DUMMYUNIONNAME
        {
            [FieldOffset(0)]
            public DEVMODEW__DUMMYUNIONNAME__DUMMYSTRUCTNAME DUMMYSTRUCTNAME;
            //
            [FieldOffset(0)]
            public POINTL dmPosition;
            //
            [FieldOffset(0)]
            public DEVMODEW__DUMMYUNIONNAME__DUMMYSTRUCTNAME2 DUMMYSTRUCTNAME2;

            [StructLayout(LayoutKind.Sequential)]
            public struct DEVMODEW__DUMMYUNIONNAME__DUMMYSTRUCTNAME
            {
                public Int16 dmOrientation;
                public Int16 dmPaperSize;
                public Int16 dmPaperLength;
                public Int16 dmPaperWidth;
                public Int16 dmScale;
                public Int16 dmCopies;
                public Int16 dmDefaultSource;
                public Int16 dmPrintQuality;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct DEVMODEW__DUMMYUNIONNAME__DUMMYSTRUCTNAME2
            {
                public POINTL dmPosition;
                public UInt32 dmDisplayOrientation;
                public UInt32 dmDisplayFixedOutput;
            }
        }
        //
        [StructLayout(LayoutKind.Explicit)]
        internal struct DEVMODEW__DUMMYUNIONNAME2
        {
            [FieldOffset(0)]
            public UInt32 dmDisplayFlags;
            //
            [FieldOffset(0)]
            public UInt32 dmNup;
        }

        // https://docs.microsoft.com/en-us/windows/win32/api/windef/ns-windef-pointl
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINTL
        {
            public Int32 x;
            public Int32 y;
        }

        // wingdi.h (Windows 10 SDK v10.0.18632)
        internal enum DM_FieldSelectionBit : UInt32
        {
            DM_ORIENTATION = 0x0000_0001,
            DM_PAPERSIZE = 0x0000_0002,
            DM_PAPERLENGTH = 0x0000_0004,
            DM_PAPERWIDTH = 0x0000_0008,
            DM_SCALE = 0x0000_0010,
            DM_POSITION = 0x0000_0020,
            DM_NUP = 0x0000_0040,
            DM_DISPLAYORIENTATION = 0x0000_0080,
            DM_COPIES = 0x0000_0100,
            DM_DEFAULTSOURCE = 0x0000_0200,
            DM_PRINTQUALITY = 0x0000_0400,
            DM_COLOR = 0x0000_0800,
            DM_DUPLEX = 0x0000_1000,
            DM_YRESOLUTION = 0x0000_2000,
            DM_TTOPTION = 0x0000_4000,
            DM_COLLATE = 0x0000_8000,
            DM_FORMNAME = 0x0001_0000,
            DM_LOGPIXELS = 0x0002_0000,
            DM_BITSPERPEL = 0x0004_0000,
            DM_PELSWIDTH = 0x0008_0000,
            DM_PELSHEIGHT = 0x0010_0000,
            DM_DISPLAYFLAGS = 0x0020_0000,
            DM_DISPLAYFREQUENCY = 0x0040_0000,
            DM_ICMMETHOD = 0x0080_0000,
            DM_ICMINTENT = 0x0100_0000,
            DM_MEDIATYPE = 0x0200_0000,
            DM_DITHERTYPE = 0x0400_0000,
            DM_PANNINGWIDTH = 0x0800_0000,
            DM_PANNINGHEIGHT = 0x1000_0000,
            DM_DISPLAYFIXEDOUTPUT = 0x2000_0000,
        }

        // WinUser.h (Windows 10 SDK v10.0.18632)
        internal static readonly UInt32 ENUM_CURRENT_SETTINGS = BitConverter.ToUInt32(BitConverter.GetBytes((Int32)(-1)));
        //internal static readonly UInt32 ENUM_REGISTRY_SETTINGS = BitConverter.ToUInt32(BitConverter.GetBytes((Int32)(-2)));

        // WinUser.h (Windows 10 SDK v10.0.18632)
        internal enum DISP_CHANGE_RESULT: Int32
        {
            DISP_CHANGE_BADDUALVIEW = -6,
            DISP_CHANGE_BADPARAM = -5,
            DISP_CHANGE_BADFLAGS = -4,
            DISP_CHANGE_NOTUPDATED = -3,
            DISP_CHANGE_BADMODE = -2,
            DISP_CHANGE_FAILED = -1,
            DISP_CHANGE_SUCCESSFUL = 0,
            DISP_CHANGE_RESTART = 1
        }

        internal const UInt32 EDD_GET_DEVICE_INTERFACE_NAME = 0x00000001;

        // DISPLAY_DEVICEW is used by EnumDisplayDevices
        // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-display_devicew
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DISPLAY_DEVICEW
        {
            public UInt32 cb;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public Char[] DeviceName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public Char[] DeviceString;
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public Char[] DeviceID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public Char[] DeviceKey;

            public void Init()
            {
                this.DeviceName = new char[32];
                this.DeviceString = new char[128];
                this.StateFlags = 0;
                this.DeviceID = new char[128];
                this.DeviceKey = new char[128];
                this.cb = (UInt32)Marshal.SizeOf(typeof(DISPLAY_DEVICEW));
            }
        }

        // https://docs.microsoft.com/en-us/windows/win32/etw/systemconfig-video
        internal enum DisplayDeviceStateFlags : UInt32
        {
            DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1,
            DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4,
            DISPLAY_DEVICE_MIRRORING_DRIVER = 0x8,
            //DISPLAY_DEVICE_VGA_COMPATIBLE = 0x10,
            //DISPLAY_DEVICE_REMOVABLE = 0x20,
            //DISPLAY_DEVICE_MODESPRUNED = 0x8000000
        }
        // windgi.h (Windows 10 SDK v10.0.18632)
        internal enum ChildDisplayDeviceStateFlags: UInt32
        {
            DISPLAY_DEVICE_ACTIVE = 0x1,
            DISPLAY_DEVICE_ATTACHED = 0x2
        }

        internal const UInt32 MONITORINFOF_PRIMARY = 1;

        // NOTE: MONITORINFOEX is used by the GetMonitorInfo function
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfoexa
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MONITORINFOEXA
        {
            public UInt32 cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public UInt32 dwFlags;
            // NOTE: szDevice must be marshalled as a ByValArray instead of a ByValTString so that Marshal.SizeOf can calculate a value
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCHDEVICENAME)]
            public Char[] szDevice;

            public void Init()
            {
                this.rcMonitor = new RECT();
                this.rcWork = new RECT();
                this.dwFlags = 0;
                this.szDevice = new Char[CCHDEVICENAME];
                this.cbSize = (UInt32)Marshal.SizeOf(typeof(MONITORINFOEXA));
            }
        }

        // NOTE: RECT is used by multiple functions including EnumDisplayMonitor's callbacks
        // https://docs.microsoft.com/en-us/windows/win32/api/windef/ns-windef-rect
        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public Int32 left;
            public Int32 top;
            public Int32 right;
            public Int32 bottom;
        }

        // NOTE: this delegate is used as a callback by EnumDisplayMonitors
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-monitorenumproc
        internal delegate Boolean MonitorEnumProcDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        /* kernel32 */
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-getsysteminfo
        [DllImport("kernel32.dll")]
        internal static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        internal enum ChangeDisplaySettingsFlags: UInt32
        {
            //CDS_UPDATEREGISTRY = 0x00000001,
            CDS_TEST = 0x00000002,
            //CDS_FULLSCREEN = 0x00000004,
            //CDS_GLOBAL = 0x00000008,
            //CDS_SET_PRIMARY = 0x00000010,
            //CDS_VIDEOPARAMETERS = 0x00000020,
            //CDS_ENABLE_UNSAFE_MODES = 0x00000100,
            //CDS_DISABLE_UNSAFE_MODES = 0x00000200,
            //CDS_RESET = 0x40000000,
            //CDS_RESET_EX = 0x20000000,
            //CDS_NORESET = 0x10000000,
        }

        /* user32 */
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern DISP_CHANGE_RESULT ChangeDisplaySettingsEx(Char[] lpszDeviceName, ref DEVMODEW lpDevMode, IntPtr hwnd, UInt32 dwflags, IntPtr lParam);
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaydevicesw
        [DllImport("user32.dll", EntryPoint = "EnumDisplayDevices", CharSet = CharSet.Unicode)]
        internal static extern Boolean EnumDisplayDevices_DisplayAdapter(IntPtr lpDeviceAsZeroPtr, UInt32 iDevNum, ref DISPLAY_DEVICEW lpDisplayDevice, UInt32 dwFlags);
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaydevicesw
        [DllImport("user32.dll", EntryPoint = "EnumDisplayDevices", CharSet = CharSet.Unicode)]
        internal static extern Boolean EnumDisplayDevices_Monitor(Char[] lpDevice, UInt32 iDevNum, ref DISPLAY_DEVICEW lpDisplayDevice, UInt32 dwFlags);
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern Boolean EnumDisplaySettingsEx(Char[] lpszDeviceName, UInt32 iModeNum, ref DEVMODEW lpDevMode, UInt32 dwFlags);
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors
        [DllImport("user32.dll")]
        internal static extern Boolean EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProcDelegate lpfnEnum, IntPtr dwData);
        // 
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow
        [DllImport("user32.dll")]
        internal static extern UInt32 GetDpiForWindow(IntPtr hwnd);
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmonitorinfoa
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern Boolean GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEXA lpmi);
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getsystemmetrics
        [DllImport("user32.dll")] 
        internal static extern Int32 GetSystemMetrics(SystemMetricIndex smIndex);
        //
        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getsystemmetricsfordpi
        [DllImport("user32.dll")]
        internal static extern Int32 GetSystemMetricsForDpi(SystemMetricIndex nIndex, UInt32 dpi);

    }
}
