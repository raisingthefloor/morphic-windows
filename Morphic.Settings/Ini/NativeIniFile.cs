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

using Morphic.Windows.Native;
using System.IO;
using System.Threading.Tasks;

namespace Morphic.Settings.Ini
{
    /// <summary>
    /// Implementation of <code>IIniFile</code> that uses <code>IniFileReaderWriter</code>
    /// </summary>
    public class NativeIniFile: IIniFile
    {

        private IniFileReaderWriter iniReaderWriter;
        private bool exists;

        /// <summary>
        /// Create an ini file from the given path
        /// </summary>
        /// <param name="path"></param>
        public NativeIniFile(string path)
        {
            iniReaderWriter = new IniFileReaderWriter(path);
            exists = File.Exists(path);
        }

        public Task<string?> GetValue(string section, string key)
        {
            string? result = null;
            if (exists)
            {
                result = iniReaderWriter.ReadValue(key, section);
            }
            return Task.FromResult(result);
        }

        public Task<bool> SetValue(string section, string key, string value)
        {
            if (exists)
            {
                iniReaderWriter.WriteValue(value, key, section);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

    }
}
