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
