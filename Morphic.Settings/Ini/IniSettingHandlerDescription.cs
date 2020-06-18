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
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Morphic.Core;
using Microsoft.Win32;
using Morphic.Settings.SystemSettings;
using System.Linq;
using Morphic.Settings.Files;

namespace Morphic.Settings
{

    /// <summary>
    /// An ini handler description that specifies which part of the ini file should be udpated
    /// </summary>
    public class IniSettingHandlerDescription : SettingHandlerDescription
    {

        /// <summary>
        /// The filename of the ini file, possibly including environmental variables
        /// </summary>
        public string Filename;

        /// <summary>
        /// The section within the ini file
        /// </summary>
        public string Section;

        /// <summary>
        /// The key within the section
        /// </summary>
        public string Key;

        /// <summary>
        /// Create a new ini file handler
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="section"></param>
        /// <param name="key"></param>
        public IniSettingHandlerDescription(string filename, string section, string key) : base(HandlerKind.Ini)
        {
            Filename = filename;
            Section = section;
            Key = key;
        }

        public IniSettingHandlerDescription(JsonElement element) : base(HandlerKind.Ini)
        {
            Filename = element.GetProperty("filename").GetString();
            Section = element.GetProperty("section").GetString();
            Key = element.GetProperty("key").GetString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is IniSettingHandlerDescription other)
            {
                return other.Filename == Filename && other.Section == Section && other.Key == Key;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Filename.GetHashCode() ^ Section.GetHashCode() ^ Key.GetHashCode();
        }
    }

}
