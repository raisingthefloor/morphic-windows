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
    public class IniFile: IIniFile
    {

        private string path;

        private bool needsConfigurationOpen;
        private Configuration? configuration;

        /// <summary>
        /// Create an ini file from the given path
        /// </summary>
        /// <param name="path"></param>
        public IniFile(string path)
        {
            this.path = path;
            needsConfigurationOpen = true;
        }

        public async Task<string?> GetValue(string section, string key)
        {
            if (needsConfigurationOpen)
            {
                configuration = await Configuration.Open(path);
                needsConfigurationOpen = false;
            }
            if (configuration != null)
            {
                return configuration.Get(section, key);
            }
            return null;
        }

        public async Task<bool> SetValue(string section, string key, string value)
        {
            if (needsConfigurationOpen)
            {
                configuration = await Configuration.Open(path);
                needsConfigurationOpen = false;
            }
            if (configuration != null)
            {
                configuration.Set(section, key, value);
                return true;
            }
            return false;
        }

        public bool NeedsWrite
        {
            get
            {
                return !needsConfigurationOpen && (configuration?.IsChanged ?? false);
            }
        }

        public async Task Write()
        {
            if (configuration != null)
            {
                await configuration.Write(path);
            }
        }
    }
}
