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

namespace Morphic.Settings
{
    internal struct ExtendedPInvoke
    {
        #region MinWinDef.h

        internal const int MAX_PATH = 260;

        #endregion MinWinDef.h


        #region WinUser.h

        internal const int COLOR_BACKGROUND = 1;
        internal const int COLOR_DESKTOP = COLOR_BACKGROUND;

        internal const uint SPI_GETAUDIODESCRIPTION = 0x0074;
        internal const uint SPI_SETAUDIODESCRIPTION = 0x0075;
        internal const uint SPI_GETMESSAGEDURATION = 0x2016;
        internal const uint SPI_SETMESSAGEDURATION = 0x2017;
        internal const uint SPI_GETMOUSEWHEELROUTING = 0x201C;
        internal const uint SPI_SETMOUSEWHEELROUTING = 0x201D;
        internal const uint SPI_GETWHEELSCROLLCHARS = 0x006C;
        internal const uint SPI_SETWHEELSCROLLCHARS = 0x006D;
        internal const uint SPI_GETWINARRANGING = 0x0082;
        internal const uint SPI_SETWINARRANGING = 0x0083;


        [StructLayout(LayoutKind.Sequential)]
        public struct AUDIODESCRIPTION
        {
            public uint cbSize { get; set; }
            public bool Enabled { get; set; }
            public uint Locale { get; set; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILTERKEYS
        {
            public uint cbSize { get; set; }
            public uint dwFlags { get; set; }
            public uint iWaitMSec { get; set; }
            public uint iDelayMSec { get; set; }
            public uint iRepeatMSec { get; set; }
            public uint iBounceMSec { get; set; }
        }

        internal const uint FKF_FILTERKEYSON = 0x00000001;
        //internal const uint FKF_AVAILABLE = 0x00000002;
        //internal const uint FKF_HOTKEYACTIVE = 0x00000004;
        //internal const uint FKF_CONFIRMHOTKEY = 0x00000008;
        //internal const uint FKF_HOTKEYSOUND = 0x00000010;
        //internal const uint FKF_INDICATOR = 0x00000020;
        //internal const uint FKF_CLICKON = 0x00000040;

        // https://docs.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-highcontrastw
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct HIGHCONTRAST
        {
            public uint cbSize;
            public HighContrastFlags dwFlags;
            public string? lpszDefaultScheme;

            public static HIGHCONTRAST CreateNew()
            {
                var result = new HIGHCONTRAST();
                result.cbSize = (uint)Marshal.SizeOf(typeof(HIGHCONTRAST));
                return result;
            }
        }

        // flags for HIGHCONTRAST.dwFlags
        public enum HighContrastFlags : uint
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
            HCF_OPTION_NOTHEMECHANGE = 0x00001000,
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEKEYS
        {
            public uint cbSize { get; set; }
            public uint dwFlags { get; set; }
            public uint iMaxSpeed { get; set; }
            public uint iTimeToMaxSpeed { get; set; }
            public uint iCtrlSpeed { get; set; }
            public uint dwReserved1 { get; set; }
            public uint dwReserved2 { get; set; }
        }

        internal const uint MKF_MOUSEKEYSON = 0x00000001;
        //internal const uint MKF_AVAILABLE = 0x00000002;
        //internal const uint MKF_HOTKEYACTIVE = 0x00000004;
        //internal const uint MKF_CONFIRMHOTKEY = 0x00000008;
        //internal const uint MKF_HOTKEYSOUND = 0x00000010;
        //internal const uint MKF_INDICATOR = 0x00000020;
        //internal const uint MKF_MODIFIERS = 0x00000040;
        //internal const uint MKF_REPLACENUMBERS = 0x00000080;
        //internal const uint MKF_LEFTBUTTONSEL = 0x10000000;
        //internal const uint MKF_RIGHTBUTTONSEL = 0x20000000;
        //internal const uint MKF_LEFTBUTTONDOWN = 0x01000000;
        //internal const uint MKF_RIGHTBUTTONDOWN = 0x02000000;
        //internal const uint MKF_MOUSEMODE = 0x80000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct STICKYKEYS
        {
            public uint cbSize { get; set; }
            public uint dwFlags { get; set; }
        }

        internal const uint SKF_STICKYKEYSON = 0x00000001;
        //internal const uint SKF_AVAILABLE = 0x00000002;
        //internal const uint SKF_HOTKEYACTIVE = 0x00000004;
        //internal const uint SKF_CONFIRMHOTKEY = 0x00000008;
        //internal const uint SKF_HOTKEYSOUND = 0x00000010;
        //internal const uint SKF_INDICATOR = 0x00000020;
        //internal const uint SKF_AUDIBLEFEEDBACK = 0x00000040;
        //internal const uint SKF_TRISTATE = 0x00000080;
        //internal const uint SKF_TWOKEYSOFF = 0x00000100;
        //internal const uint SKF_LALTLATCHED = 0x10000000;
        //internal const uint SKF_LCTLLATCHED = 0x04000000;
        //internal const uint SKF_LSHIFTLATCHED = 0x01000000;
        //internal const uint SKF_RALTLATCHED = 0x20000000;
        //internal const uint SKF_RCTLLATCHED = 0x08000000;
        //internal const uint SKF_RSHIFTLATCHED = 0x02000000;
        //internal const uint SKF_LWINLATCHED = 0x40000000;
        //internal const uint SKF_RWINLATCHED = 0x80000000;
        //internal const uint SKF_LALTLOCKED = 0x00100000;
        //internal const uint SKF_LCTLLOCKED = 0x00040000;
        //internal const uint SKF_LSHIFTLOCKED = 0x00010000;
        //internal const uint SKF_RALTLOCKED = 0x00200000;
        //internal const uint SKF_RCTLLOCKED = 0x00080000;
        //internal const uint SKF_RSHIFTLOCKED = 0x00020000;
        //internal const uint SKF_LWINLOCKED = 0x00400000;
        //internal const uint SKF_RWINLOCKED = 0x00800000;

        [StructLayout(LayoutKind.Sequential)]
        public struct TOGGLEKEYS
        {
            public uint cbSize { get; set; }
            public uint dwFlags { get; set; }
        }

        internal const uint TKF_TOGGLEKEYSON = 0x00000001;
        //internal const uint TKF_AVAILABLE = 0x00000002;
        //internal const uint TKF_HOTKEYACTIVE = 0x00000004;
        //internal const uint TKF_CONFIRMHOTKEY = 0x00000008;
        //internal const uint TKF_HOTKEYSOUND = 0x00000010;
        //internal const uint TKF_INDICATOR = 0x00000020;

        [DllImport("user32.dll")]
        internal static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        internal static extern uint GetSysColor(int nIndex);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetSysColorBrush(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetDoubleClickTime(uint unnamedParam1);

        [DllImport("user32.dll")]
        internal static extern bool SetSysColors(int cElements, int[] lpaElements, uint[] lpaRgbValues);

        #endregion WinUser.h

    }
}
