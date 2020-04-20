//
// IniFileReaderWriter.cs
// Morphic support library for Windows
//
// Copyright © 2020 Raising the Floor -- US Inc. All rights reserved.
//
// The R&D leading to these results received funding from the
// Department of Education - Grant H421A150005 (GPII-APCP). However,
// these results do not necessarily represent the policy of the
// Department of Education, and you should not assume endorsement by the
// Federal Government.

using System;
using System.Runtime.InteropServices;

// TODO: This code is a draft, pending QA

namespace Morphic.Windows.Native
{
    public class IniFileReaderWriter
    {
        private String _path;

        public IniFileReaderWriter(String path)
        {
            _path = path;
        }

        public String ReadValue(String key, String section)
        {
            // NOTE: we are using a maximum value length of 255 characters + null; this is somewhat arbitrary, so enlarge maxValueLength if necessary
            var maxValueLength = 256;
            var value = new String(' ', maxValueLength);

            var actualLength = WindowsApi.GetPrivateProfileString(section, key, "", out value, (UInt32)maxValueLength, _path);

            var lastWin32Error = Marshal.GetLastWin32Error();
            if (lastWin32Error == 0x2) /* file not found*/
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                throw Marshal.GetExceptionForHR(hresult);
            }

            var result = value.Substring(0, (Int32)actualLength);
            return result;
        }

        public void WriteValue(String value, String key, String section)
        {
            var success = WindowsApi.WritePrivateProfileString(section, key, value, _path);
            if (success == false)
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                throw Marshal.GetExceptionForHR(hresult);
            }
        }

        public void DeleteKey(String key, String section)
        {
            var success = WindowsApi.WritePrivateProfileString(section, key, null, _path);
            if (success == false)
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                throw Marshal.GetExceptionForHR(hresult);
            }
        }

        public void ClearSection(String section)
        {
            var success = WindowsApi.WritePrivateProfileSection(section, "", _path);
            if (success == false)
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                throw Marshal.GetExceptionForHR(hresult);
            }
        }
    }
}
