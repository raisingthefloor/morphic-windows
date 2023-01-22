// Copyright 2020-2022 Raising the Floor - US, Inc.
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

namespace Morphic.WindowsNative;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public struct ExtendedPInvoke
{
    #region appmodel.h

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, [MarshalAs(UnmanagedType.LPWStr)] string packageFullName);

    #endregion appmodel.h


    #region devicetopology.h

    internal static uint E_NOTFOUND = HRESULT_FROM_WIN32((uint)PInvoke.Win32ErrorCode.ERROR_NOT_FOUND);

    #endregion devicetopology.h


    #region winerror.h

    internal const nint S_OK = 0;
    internal const nint S_FALSE = 1;

    // facility codes
    private const uint FACILITY_WIN32 = 7;

    // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/0c0bcf55-277e-4120-b5dc-f6115fc8dc38
    private static uint HRESULT_FROM_WIN32(uint x)
    {
        return unchecked((uint)(x) <= 0 ? ((uint)(x)) : ((uint)(((x) & 0x0000FFFF) | (FACILITY_WIN32 << 16) | 0x80000000)));
    }

    #endregion winerror.h


    #region wingdi.h

    private const int CCHFORMNAME = 32;

    [Flags]
    internal enum QueryDisplayConfigFlags : uint
    {
        QDC_ALL_PATHS = 0x00000001,
        QDC_ONLY_ACTIVE_PATHS = 0x00000002,
        QDC_DATABASE_CURRENT = 0x00000004
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_device_info_header
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
        public uint size;
        public PInvoke.User32.LUID adapterId;
        public uint id;
    }

    [Flags]
    internal enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
    {
        // NOTE: the GET_DPI and SET_DPI values are undocumented and were reverse engineered as part of the Morphic Classic project
        DISPLAYCONFIG_DEVICE_INFO_SET_DPI = unchecked((uint)-4),
        DISPLAYCONFIG_DEVICE_INFO_GET_DPI = unchecked((uint)-3),
        //
        // NOTE: the remaining entries are publicly documented
        DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1,
        DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2,
        DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE = 3,
        DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME = 4,
        DISPLAYCONFIG_DEVICE_INFO_SET_TARGET_PERSISTENCE = 5,
        DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_BASE_TYPE = 6,
        DISPLAYCONFIG_DEVICE_INFO_GET_SUPPORT_VIRTUAL_RESOLUTION = 7,
        DISPLAYCONFIG_DEVICE_INFO_SET_SUPPORT_VIRTUAL_RESOLUTION = 8,
        DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9,
        DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10,
        DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL = 11,
        DISPLAYCONFIG_DEVICE_INFO_FORCE_UINT32 = 0xFFFFFFFF
    }

    // Reverse-engineered DPI scaling code, utilizing the CCD APIs
    // https://docs.microsoft.com/en-us/windows-hardware/drivers/display/ccd-apis
    //
    // NOTE: this structure is undocumented and was reverse engineered as part of the Morphic Classic project
    // NOTE: all offsets are indices (relative to the OS's recommended DPI scaling value; the recommended DPI scaling value is always zero)
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_GET_DPI
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        public int minimumDpiOffset;
        public int currentDpiOffset;
        public int maximumDpiOffset;

        public static DISPLAYCONFIG_GET_DPI InitializeNew()
        {
            var result = new DISPLAYCONFIG_GET_DPI()
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER()
                {
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_DPI>()
                }
            };
            return result;
        }
    }
    //
    // NOTE: this structure is undocumented and was reverse engineered as part of the Morphic Classic project
    // NOTE: all offsets are indices (relative to the OS's recommended DPI scaling value; the recommended DPI scaling value is always zero)
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_SET_DPI
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        public int dpiOffset;

        public static DISPLAYCONFIG_SET_DPI InitializeNew()
        {
            var result = new DISPLAYCONFIG_SET_DPI()
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER()
                {
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_DPI>()
                }
            };
            return result;
        }
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_mode_info
    [StructLayout(LayoutKind.Explicit)]
    internal struct DISPLAYCONFIG_MODE_INFO
    {
        [FieldOffset(0)]
        public PInvoke.User32.DISPLAYCONFIG_MODE_INFO_TYPE infoType;
        [FieldOffset(4)]
        public uint id;
        [FieldOffset(8)]
        public PInvoke.User32.LUID adapterId;

        // union
        [FieldOffset(16)]
        public DISPLAYCONFIG_TARGET_MODE targetMode;
        [FieldOffset(16)]
        public PInvoke.User32.DISPLAYCONFIG_SOURCE_MODE sourceMode;
        [FieldOffset(16)]
        public PInvoke.User32.DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_info
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [Flags]
    internal enum DisplayConfigSourceInfoStatus : uint
    {
        DISPLAYCONFIG_TARGET_IN_USE = 0x00000001,
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_source_info
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public PInvoke.User32.LUID adapterId;
        public uint id;
        public uint modeInfoIdx; // union with cloneGroupId:16 and sourceModeInfoIdx:16
        public DisplayConfigSourceInfoStatus statusFlags;
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_target_info
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public PInvoke.User32.LUID adapterId;
        public uint id;
        public uint modeInfoIdx; // union with desktopModeInfoIdx:16 and targetModeInfoIdx:16
        public PInvoke.User32.DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
        public PInvoke.User32.DISPLAYCONFIG_ROTATION rotation;
        public PInvoke.User32.DISPLAYCONFIG_SCALING scaling;
        public PInvoke.User32.DISPLAYCONFIG_RATIONAL refreshRate;
        public PInvoke.User32.DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
        public bool targetAvailable;
        public PInvoke.User32.DISPLAYCONFIG_PATH_TARGET_INFOFlags statusFlags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCHDEVICENAME)]
        public Char[] viewGdiDeviceName;

        public static DISPLAYCONFIG_SOURCE_DEVICE_NAME InitializeNew()
        {
            var result = new DISPLAYCONFIG_SOURCE_DEVICE_NAME()
            {
                viewGdiDeviceName = new char[CCHDEVICENAME],
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER()
                {
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>()
                }
            };
            return result;
        }
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_target_mode
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_TARGET_MODE
    {
        public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_video_signal_info
    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public PInvoke.User32.DISPLAYCONFIG_RATIONAL hSyncFreq;
        public PInvoke.User32.DISPLAYCONFIG_RATIONAL vSyncFreq;
        public PInvoke.User32.DISPLAYCONFIG_2DREGION activeSize;
        public PInvoke.User32.DISPLAYCONFIG_2DREGION totalSize;
        public _D3DKMDT_VIDEO_SIGNAL_STANDARD videoStandard; // union: AdditionalSignalInfo(videoStandard:16, vSyncFreqDivider:6, reserved:10)
                                                             // Scan line ordering (e.g. progressive, interlaced).
        public PInvoke.User32.DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering;
    }

    // https://docs.microsoft.com/en-us/windows-hardware/drivers/ddi/d3dkmdt/ne-d3dkmdt-_d3dkmdt_video_signal_standard
    // see: d3dkmdt.h (MSVC v142 - VS 2019 C++ x64/x86 build tools (14.25) [and Spectre-migrated libs] 
    [Flags]
    internal enum _D3DKMDT_VIDEO_SIGNAL_STANDARD : uint
    {
        D3DKMDT_VSS_UNINITIALIZED = 0,
        //
        D3DKMDT_VSS_VESA_DMT = 1,
        D3DKMDT_VSS_VESA_GTF = 2,
        D3DKMDT_VSS_VESA_CVT = 3,
        //
        D3DKMDT_VSS_IBM = 4,
        D3DKMDT_VSS_APPLE = 5,
        //
        D3DKMDT_VSS_NTSC_M = 6,
        D3DKMDT_VSS_NTSC_J = 7,
        D3DKMDT_VSS_NTSC_443 = 8,
        D3DKMDT_VSS_PAL_B = 9,
        D3DKMDT_VSS_PAL_B1 = 10,
        D3DKMDT_VSS_PAL_G = 11,
        D3DKMDT_VSS_PAL_H = 12,
        D3DKMDT_VSS_PAL_I = 13,
        D3DKMDT_VSS_PAL_D = 14,
        D3DKMDT_VSS_PAL_N = 15,
        D3DKMDT_VSS_PAL_NC = 16,
        D3DKMDT_VSS_SECAM_B = 17,
        D3DKMDT_VSS_SECAM_D = 18,
        D3DKMDT_VSS_SECAM_G = 19,
        D3DKMDT_VSS_SECAM_H = 20,
        D3DKMDT_VSS_SECAM_K = 21,
        D3DKMDT_VSS_SECAM_K1 = 22,
        D3DKMDT_VSS_SECAM_L = 23,
        D3DKMDT_VSS_SECAM_L1 = 24,
        //
        D3DKMDT_VSS_EIA_861 = 25,
        D3DKMDT_VSS_EIA_861A = 26,
        D3DKMDT_VSS_EIA_861B = 27,
        //
        D3DKMDT_VSS_PAL_K = 28,
        D3DKMDT_VSS_PAL_K1 = 29,
        D3DKMDT_VSS_PAL_L = 30,
        D3DKMDT_VSS_PAL_M = 31,
        //
        D3DKMDT_VSS_OTHER = 255
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig
    [DllImport("user32.dll")]
    internal static extern PInvoke.Win32ErrorCode QueryDisplayConfig(QueryDisplayConfigFlags flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray,
        ref uint modeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

    //

    // DEVMODEW is used by EnumDisplaySettingsEx and other functions
    // https://docs.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-devmodew
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DEVMODEW
    {
        private const int privateDriverDataLength = 0;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCHDEVICENAME)]
        public char[] dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public DM_FieldSelectionBit dmFields;
        public DEVMODEW__DUMMYUNIONNAME DUMMYUNIONNAME;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCHFORMNAME)]
        public char[] dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public DEVMODEW__DUMMYUNIONNAME2 DUMMYUNIONNAME2;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = privateDriverDataLength)]
        //public Byte[] privateDriverData;

        public static DEVMODEW InitializeNew()
        {
            var result = new DEVMODEW()
            {
                dmDeviceName = new Char[CCHDEVICENAME],
                dmFormName = new Char[CCHFORMNAME],
                // privateDriverData = new byte[privateDriverDataLength],
                dmDriverExtra = privateDriverDataLength,
                dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODEW)),
            };

            return result;
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
        public DEVMODEW__DUMMYUNIONNAME__DUMMYSTRUCTNAME2 DUMMYSTRUCTNAME2;

        /* printer only fields */
        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODEW__DUMMYUNIONNAME__DUMMYSTRUCTNAME
        {
            public short dmOrientation;
            public short dmPaperSize;
            public short dmPaperLength;
            public short dmPaperWidth;
            public short dmScale;
            public short dmCopies;
            public short dmDefaultSource;
            public short dmPrintQuality;
        }

        /* display only fields */
        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODEW__DUMMYUNIONNAME__DUMMYSTRUCTNAME2
        {
            public POINTL dmPosition;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
        }
    }
    //
    [StructLayout(LayoutKind.Explicit)]
    internal struct DEVMODEW__DUMMYUNIONNAME2
    {
        [FieldOffset(0)]
        public uint dmDisplayFlags;
        //
        [FieldOffset(0)]
        public uint dmNup;
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/windef/ns-windef-pointl
    [StructLayout(LayoutKind.Sequential)]
    internal struct POINTL
    {
        public int x;
        public int y;
    }

    internal enum DM_FieldSelectionBit : uint
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

    #endregion wingdi.h


    #region user32.dll (reverse engineered, not WinUser.h)

    // see: https://stackoverflow.com/questions/32724187/how-do-you-set-the-glass-blend-colour-on-windows-10
    // see also: https://gist.github.com/ysc3839/b08d2bff1c7dacde529bed1d37e85ccf (this GIST corroborated the values we pulled form kernel32legacylib.etc, fileextd.lib, etc. -- and also appears to have a few newer attributes which weren't present in the 1809 SDK).

    // NOTE: these attributes were painstakingly observed and captured by hand from "C:\Program Files (x86)\Windows Kits\10\Lib\10.0.17763.0\um\x64\kernel32legacylib.lib" (Windows 10 1809 SDK) using direct hex view; they appear to be undocumented
    // NOTE: the enum name, ACCENT_STATE, was observed in "C:\Program Files (x86)\Windows Kits\10\Lib\10.0.17763.0\um\x64\kernel32legacylib.lib"; it appears to be undocumented
    internal enum ACCENT_STATE: uint
    {
        ACCENT_DISABLED = 0x00,
        ACCENT_ENABLE_GRADIENT = 0x01,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 0x02,
        ACCENT_ENABLE_BLURBEHIND = 0x03,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 0x04,
        ACCENT_ENABLE_HOSTBACKDROP = 0x05,
        ACCENT_INVALID_STATE = 0x06,
    }

    // NOTE: the struct name, ACCENT_POLICY, was observed in "C:\Program Files (x86)\Windows Kits\10\Lib\10.0.17763.0\um\x64\kernel32legacylib.lib"; it appears to be undocumented
    // NOTE: this structure is used with WCA_ACCENT_POLICY; other structures would need to be used with other WINDOWCOMPOSITIONATTRIB attribute values
    [StructLayout(LayoutKind.Sequential)]
    internal struct ACCENT_POLICY
    {
        public ACCENT_STATE AccentState;
        public uint AccentFlags;
        public uint GradientColor;
        public uint AnimationId;
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct HIGHCONTRAST
    {
        public uint cbSize;
        public HighContrastFlags dwFlags;
        public String? lpszDefaultScheme;

        public static HIGHCONTRAST InitializeNew()
        {
            var result = new HIGHCONTRAST()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(HIGHCONTRAST)),
            };

            return result;
        }

    }

    // flags for HIGHCONTRAST.dwFlags
    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw
    internal enum HighContrastFlags : uint
    {
        HCF_HIGHCONTRASTON = 0x00000001,
        HCF_AVAILABLE = 0x00000002,
        HCF_HOTKEYACTIVE = 0x00000004,
        HCF_CONFIRMHOTKEY = 0x00000008,
        HCF_HOTKEYSOUND = 0x00000010,
        HCF_INDICATOR = 0x00000020,
        HCF_HOTKEYAVAILABLE = 0x00000040,
        HCF_LOGONDESKTOP = 0x00000100,
        HCF_DEFAULTDESKTOP = 0x00000200,
        // NOTE: HCF_OPTION_NOTHEMECHANGE is new (or newly documented), as of Windows 10 2004 (build 19041)
        HCF_OPTION_NOTHEMECHANGE = 0x00001000,
    }

    // NOTE: these attributes were painstakingly observed and captured by hand from "C:\Program Files (x86)\Windows Kits\10\Lib\10.0.17763.0\um\x64\fileextd.lib" (Windows 10 1809 SDK) using Ghidra; they appear to be undocumented
    // NOTE: these attributes were corroborated at: http://www.brandonfa.lk/win8/win8_devrel_head_x86/webcamui.h
    // NOTE: the enum name, WINDOWCOMPOSITIONATTRIB, is assumed (based on the string immediately following the WCA_ values in fileextd.lib); these are grouped together for convenience (and may technically just be a list of consts)
    internal enum WINDOWCOMPOSITIONATTRIB : uint
    {
        WCA_UNDEFINED = 0x00,
        WCA_NCRENDERING_ENABLED = 0x01,
        WCA_NCRENDERING_POLICY = 0x02,
        WCA_TRANSITIONS_FORCEDISABLED = 0x03,
        WCA_ALLOW_NCPAINT = 0x04,
        WCA_CAPTION_BUTTON_BOUNDS = 0x05,
        WCA_NONCLIENT_RTL_LAYOUT = 0x06,
        WCA_FORCE_ICONIC_REPRESENTATION = 0x07,
        WCA_EXTENDED_FRAME_BOUNDS = 0x08,
        WCA_HAS_ICONIC_BITMAP = 0x09,
        WCA_THEME_ATTRIBUTES = 0x0A,
        WCA_NCRENDERING_EXILED = 0x0B,
        WCA_NCADORNMENTINFO = 0x0C,
        WCA_EXCLUDED_FROM_LIVEPREVIEW = 0x0D,
        WCA_VIDEO_OVERLAY_ACTIVE = 0x0E,
        WCA_FORCE_ACTIVEWINDOW_APPEARANCE = 0x0F,
        WCA_DISALLOW_PEEK = 0x10,
        WCA_CLOAK = 0x11,
        WCA_CLOAKED = 0x12,
        WCA_ACCENT_POLICY = 0x13,
        WCA_FREEZE_REPRESENTATION = 0x14,
        WCA_EVER_UNCLOAKED = 0x15,
        WCA_VISUAL_OWNER = 0x16,
        WCA_HOLOGRAPHIC = 0x17,
        WCA_EXCLUDED_FROM_DDA = 0x18,
        WCA_PASSIVEUPDATEMODE = 0x19,
        WCA_LAST = 0x1A,
    }

    // NOTE; the struct name, WINDOWCOMPOSITIONATTRIBDATA, was observed in "C:\Program Files (x86)\Windows Kits\10\Lib\10.0.17763.0\um\x64\kernel32legacylib.lib"; it appears to be undocumented
    // NOTE: this undocumented struct was documented at http://undoc.airesoft.co.uk/user32.dll/GetWindowCompositionAttribute.php
    // NOTE: the field names noted at https://gist.github.com/ysc3839/b08d2bff1c7dacde529bed1d37e85ccf are used here; cbData might be alternatively renamed to DataSize for clarity
    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWCOMPOSITIONATTRIBDATA
    {
        public WINDOWCOMPOSITIONATTRIB attribute;  // specify the attribute to set
        public IntPtr pvData;                      // specify the data/value of the requested attribute
        public uint cbData;                        // set to the size of pvData
    };

    // NOTE: this function is located in user32.dll (as exported by dumpbin and observed in Ghidra); it appears to be undocumented
    // NOTE: this undocumented function was documented at http://undoc.airesoft.co.uk/user32.dll/SetWindowCompositionAttribute.php
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA pAttrData);

    #endregion user32.dll (reverse engineered, not WinUser.h)


    #region winnt.h

    internal const uint SYNCHRONIZE = 0x00100000;

    // registry-specific access rights

    internal const uint KEY_QUERY_VALUE         = 0x0001;
    internal const uint KEY_SET_VALUE           = 0x0002;
    internal const uint KEY_CREATE_SUB_KEY      = 0x0004;
    internal const uint KEY_ENUMERATE_SUB_KEYS  = 0x0008;
    internal const uint KEY_NOTIFY              = 0x0010;
    internal const uint KEY_CREATE_LINK         = 0x0020;
    internal const uint KEY_WOW64_32KEY         = 0x0200;
    internal const uint KEY_WOW64_64KEY         = 0x0100;
    internal const uint KEY_WOW64_RES           = 0x0300;

    internal const uint KEY_READ = ((uint)PInvoke.Kernel32.ACCESS_MASK.StandardRight.STANDARD_RIGHTS_READ |
                                    KEY_QUERY_VALUE |
                                    KEY_ENUMERATE_SUB_KEYS |
                                    KEY_NOTIFY) 
                                    & ~SYNCHRONIZE;
    internal const uint KEY_WRITE = ((uint)PInvoke.Kernel32.ACCESS_MASK.StandardRight.STANDARD_RIGHTS_WRITE | 
                                    KEY_SET_VALUE | 
                                    KEY_CREATE_SUB_KEY)
                                    & ~SYNCHRONIZE;

    internal const uint KEY_EXECUTE = KEY_READ
                                      & ~SYNCHRONIZE;

    internal const uint KEY_ALL_ACCESS = ((uint)PInvoke.Kernel32.ACCESS_MASK.StandardRight.STANDARD_RIGHTS_ALL |
                                         KEY_QUERY_VALUE |
                                         KEY_SET_VALUE |
                                         KEY_CREATE_SUB_KEY |
                                         KEY_ENUMERATE_SUB_KEYS |
                                         KEY_NOTIFY |
                                         KEY_CREATE_LINK)
                                      & ~SYNCHRONIZE;

    //

    internal enum RegistryValueType : uint
    {
        REG_NONE = 0,
        REG_SZ = 1,
        REG_EXPAND_SZ = 2,
        REG_BINARY = 3,
        REG_DWORD = 4,
        REG_DWORD_LITTLE_ENDIAN = REG_DWORD,
        REG_DWORD_BIG_ENDIAN = 5,
        REG_LINK = 6,
        REG_MULTI_SZ = 7,
        REG_RESOURCE_LIST = 8,
        REG_FULL_RESOURCE_DESCRIPTOR = 9,
        REG_RESOURCE_REQUIREMENTS_LIST = 10,
        REG_QWORD = 11,
        REG_QWORD_LITTLE_ENDIAN = REG_QWORD
    }

    #endregion winnt.h


    #region winreg.h

    // https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regdeletevaluew
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    internal static extern PInvoke.Win32ErrorCode RegDeleteValue(UIntPtr hKey, [MarshalAs(UnmanagedType.LPWStr)] string? lpValueName);

    // https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regenumkeyw
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    internal static extern PInvoke.Win32ErrorCode RegEnumKeyEx(UIntPtr hKey, uint dwIndex, StringBuilder lpName, ref uint lpcchName, IntPtr lpReserved, IntPtr lpClass, IntPtr lpcchClass, IntPtr lpftLastWriteTime);

    // https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regenumvaluew
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    //internal static extern PInvoke.Win32ErrorCode RegEnumValue(UIntPtr hKey, uint dwIndex, StringBuilder lpValueName, ref uint lpcchValueName, IntPtr lpReserved, out RegistryValueType lpType, IntPtr lpData, ref uint lpcbData);
    internal static extern PInvoke.Win32ErrorCode RegEnumValue(UIntPtr hKey, uint dwIndex, StringBuilder lpValueName, ref uint lpcchValueName, IntPtr lpReserved, IntPtr lpType, IntPtr lpData, IntPtr lpcbData);

    // https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regqueryvalueexw
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    internal static extern PInvoke.Win32ErrorCode RegQueryValueEx(UIntPtr hKey, [MarshalAs(UnmanagedType.LPWStr)] string? lpValueName, IntPtr lpReserved, out RegistryValueType lpType, IntPtr lpData, ref uint lpcbData);

    // https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regsetvalueexw
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    internal static extern PInvoke.Win32ErrorCode RegSetValueEx(UIntPtr hKey, [MarshalAs(UnmanagedType.LPWStr)] string? lpValueName, uint reserved, RegistryValueType dwType, IntPtr lpData, uint cbData);

    #endregion winreg.h


    #region WinUser.h

    private const int CCHDEVICENAME = 32;

    internal const uint CDS_UPDATEREGISTRY = 0x00000001;

    internal const ushort FAPPCOMMAND_KEY = 0;

    // return values for ChangeDisplaySettings/ChangeDisplaySettingsEx
    internal const int DISP_CHANGE_SUCCESSFUL = 0;
    //internal const int DISP_CHANGE_RESTART = 1;
    //internal const int DISP_CHANGE_FAILED = -1;
    //internal const int DISP_CHANGE_BADMODE = -2;
    //internal const int DISP_CHANGE_NOTUPDATED = -3;
    //internal const int DISP_CHANGE_BADFLAGS = -4;
    //internal const int DISP_CHANGE_BADPARAM = -5;
    //internal const int DISP_CHANGE_BADDUALVIEW = -6;

    // WinUser.h (Windows 10 SDK v10.0.18632)
    internal static readonly uint ENUM_CURRENT_SETTINGS = BitConverter.ToUInt32(BitConverter.GetBytes((int)(-1)));
    //internal static readonly uint ENUM_REGISTRY_SETTINGS = BitConverter.ToUInt32(BitConverter.GetBytes((int)(-2)));

    // NOTE: MONITORINFOEX is used by the GetMonitorInfo function
    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfoexa
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFOEXW
    {
        public uint cbSize;
        public PInvoke.RECT rcMonitor;
        public PInvoke.RECT rcWork;
        public uint dwFlags;
        // NOTE: szDevice must be marshalled as a ByValArray instead of a ByValTString so that Marshal.SizeOf can calculate a value
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CCHDEVICENAME)]
        public char[] szDevice;

        public static MONITORINFOEXW InitializeNew()
        {
            var result = new MONITORINFOEXW()
            {
                cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>()
            };
            return result;
        }
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODEW lpDevMode, IntPtr hwnd, uint dwFlags, IntPtr lParam);

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdisplayconfigbuffersizes
    [DllImport("user32.dll")]
    internal static extern PInvoke.Win32ErrorCode GetDisplayConfigBufferSizes(QueryDisplayConfigFlags flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern PInvoke.Win32ErrorCode DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);
    //
    [DllImport("user32.dll")]
    internal static extern PInvoke.Win32ErrorCode DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_GET_DPI requestPacket);

    [DllImport("user32.dll")]
    internal static extern PInvoke.Win32ErrorCode DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SET_DPI requestPacket);

    // NOTE: this delegate is used as a callback by EnumDisplayMonitors
    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nc-winuser-monitorenumproc
    internal delegate Boolean MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref PInvoke.RECT lpRect, IntPtr lParam);

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors
    [DllImport("user32.dll")]
    internal static extern Boolean EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern Boolean EnumDisplaySettingsEx(string? lpszDeviceName, uint iModeNum, ref DEVMODEW lpDevMode, uint dwFlags);

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmonitorinfow
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEXW lpmi);

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createwindowexw
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowEx(
        WindowStylesEx dwExStyle,
        IntPtr lpClassName,
        string? lpWindowName,
        WindowStyles dwStyle,
        int X,
        int Y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    // helper declaration (for passing the class name as a ushort)
    internal static IntPtr CreateWindowEx(ExtendedPInvoke.WindowStylesEx dwExStyle, ushort lpClassName, string? lpWindowName, ExtendedPInvoke.WindowStyles dwStyle, int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam)
    {
        return ExtendedPInvoke.CreateWindowEx(dwExStyle, new IntPtr(lpClassName), lpWindowName, dwStyle, X, Y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
    }

    // helper declaration (for passing the class name as a string)
    internal static IntPtr CreateWindowEx(ExtendedPInvoke.WindowStylesEx dwExStyle, string lpClassName, string? lpWindowName, ExtendedPInvoke.WindowStyles dwStyle, int X, int Y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam)
    {
        var pointerToClassName = Marshal.StringToHGlobalUni(lpClassName);
        try
        {
            return ExtendedPInvoke.CreateWindowEx(dwExStyle, pointerToClassName, lpWindowName, dwStyle, X, Y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
        }
        finally
        {
            Marshal.FreeHGlobal(pointerToClassName);
        }
    }

    //

    // https://docs.microsoft.com/en-us/windows/win32/winmsg/window-styles
    [Flags]
    internal enum WindowStyles : uint
    {
        WS_BORDER = 0x00800000,
        WS_CAPTION = 0x00C00000,
        WS_CHILD = 0x40000000,
        WS_CHILDWINDOW = 0x40000000,
        WS_CLIPCHILDREN = 0x02000000,
        WS_CLIPSIBLINGS = 0x04000000,
        WS_DISABLED = 0x08000000,
        WS_DLGFRAME = 0x00400000,
        WS_GROUP = 0x00020000,
        WS_HSCROLL = 0x00100000,
        WS_ICONIC = 0x20000000,
        WS_MAXIMIZE = 0x01000000,
        WS_MAXIMIZEBOX = 0x00010000,
        WS_MINIMIZE = 0x20000000,
        WS_MINIMIZEBOX = 0x00020000,
        WS_OVERLAPPED = 0x00000000,
        WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
        WS_POPUP = 0x80000000,
        WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
        WS_SIZEBOX = 0x00040000,
        WS_SYSMENU = 0x00080000,
        WS_TABSTOP = 0x00010000,
        WS_THICKFRAME = 0x00040000,
        WS_TILED = 0x00000000,
        WS_TILEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
        WS_VISIBLE = 0x10000000,
        WS_VSCROLL = 0x00200000
    }

    // https://docs.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles
    [Flags]
    internal enum WindowStylesEx : uint
    {
        WS_EX_ACCEPTFILES = 0x00000010,
        WS_EX_APPWINDOW = 0x00040000,
        WS_EX_CLIENTEDGE = 0x00000200,
        WS_EX_COMPOSITED = 0x02000000,
        WS_EX_CONTEXTHELP = 0x00000400,
        WS_EX_CONTROLPARENT = 0x00010000,
        WS_EX_DLGMODALFRAME = 0x00000001,
        WS_EX_LAYERED = 0x00080000,
        WS_EX_LAYOUTRTL = 0x00400000,
        WS_EX_LEFT = 0x00000000,
        WS_EX_LEFTSCROLLBAR = 0x00004000,
        WS_EX_LTRREADING = 0x00000000,
        WS_EX_MDICHILD = 0x00000040,
        WS_EX_NOACTIVATE = 0x08000000,
        WS_EX_NOINHERITLAYOUT = 0x00100000,
        WS_EX_NOPARENTNOTIFY = 0x00000004,
        WS_EX_NOREDIRECTIONBITMAP = 0x00200000,
        WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE,
        WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
        WS_EX_RIGHT = 0x00001000,
        WS_EX_RIGHTSCROLLBAR = 0x00000000,
        WS_EX_RTLREADING = 0x00002000,
        WS_EX_STATICEDGE = 0x00020000,
        WS_EX_TOOLWINDOW = 0x00000080,
        WS_EX_TOPMOST = 0x00000008,
        WS_EX_TRANSPARENT = 0x00000020,
        WS_EX_WINDOWEDGE = 0x00000100
    }

    //

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-defwindowprocw
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongw
    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLongPtr_32bit(IntPtr hWnd, int nIndex);
    //
    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlongptrw
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr_64bit(IntPtr hWnd, int nIndex);
    //
    internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        // NOTE: 32-bit windows does not have a DLL entrypoint labeled GetWindowLongPtr, so we alias the functions here (just like the C header files do)
        if (Marshal.SizeOf<IntPtr>() == 4) {
            return (IntPtr)ExtendedPInvoke.GetWindowLongPtr_32bit(hWnd, nIndex);
        }
        else
        {
            return ExtendedPInvoke.GetWindowLongPtr_64bit(hWnd, nIndex);
        }
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-loadcursorw
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
    //
    internal enum Cursors
    {
        IDC_APPSTARTING = 32650,
        IDC_ARROW = 32512,
        IDC_CROSS = 32515,
        IDC_HAND = 32649,
        IDC_HELP = 32651,
        IDC_IBEAM = 32513,
        IDC_ICON = 32641,
        IDC_NO = 32648,
        IDC_SIZE = 32640,
        IDC_SIZEALL = 32646,
        IDC_SIZENESW = 32643,
        IDC_SIZENS = 32645,
        IDC_SIZENWSE = 32642,
        IDC_SIZEWE = 32644,
        IDC_UPARROW = 32516,
        IDC_WAIT = 32514,
    }

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerclassexw
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpWndClass);

    // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-wndclassexw
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public WNDPROC lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }
    // see: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms633573(v=vs.85)
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate IntPtr WNDPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion WinUser.h
}
