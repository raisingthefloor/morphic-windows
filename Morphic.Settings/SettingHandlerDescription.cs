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

namespace Morphic.Settings
{

    /// <summary>
    /// Base class for describing the handler for a setting
    /// </summary>
    public class SettingHandlerDescription
    {

        /// <summary>
        /// The possible kinds of handlers
        /// </summary>
        public enum HandlerKind
        {
            /// <summary>
            /// A client handler is custom code provided by the client
            /// </summary>
            Client,

            /// <summary>
            /// A registry handler sets a specific value in the windows registry
            /// </summary>
            Registry,

            /// <summary>
            /// A registry handler sets a specific SystemSetting
            /// </summary>
            System,

            /// <summary>
            /// An ini handler sets a specific value in an ini file
            /// </summary>
            Ini,

            /// <summary>
            /// A Windows System Parameter Info call
            /// </summary>
            SystemParametersInfo,

            /// <summary>
            /// An unknown handler is used for any unrecogized or invalid handler JSON
            /// </summary>
            Unknown
        }

        /// <summary>
        /// The kind of this handler
        /// </summary>
        public HandlerKind Kind { get; set; }

        /// <summary>
        /// Create a new handler for the given kind
        /// </summary>
        /// <param name="kind"></param>
        public SettingHandlerDescription(HandlerKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// A custom JSON converter that creates the correct subclass based on the type property
        /// </summary>
        public class JsonConverter : System.Text.Json.Serialization.JsonConverter<SettingHandlerDescription>
        {
            public override SettingHandlerDescription Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                try
                {
                    var document = JsonDocument.ParseValue(ref reader);
                    var element = document.RootElement;
                    var type = element.GetProperty("type").GetString();
                    switch (type)
                    {
                        case "org.raisingthefloor.morphic.client":
                            {
                                var solution = element.GetProperty("solution").GetString();
                                var preference = element.GetProperty("preference").GetString();
                                var key = new Preferences.Key(solution, preference);
                                return new ClientSettingHandlerDescription(key);
                            }
                        case "com.microsoft.windows.registry":
                            {
                                var keyName = element.GetProperty("key_name").GetString();
                                var valueName = element.GetProperty("value_name").GetString();
                                var valueType = element.GetProperty("value_type").GetString();
                                var valueKind = Enum.Parse<RegistryValueKind>(valueType, ignoreCase: true);
                                return new RegistrySettingHandlerDescription(keyName, valueName, valueKind);
                            }
                        case "com.microsoft.windows.ini":
                            {
                                var filename = element.GetProperty("filename").GetString();
                                var section = element.GetProperty("section").GetString();
                                var key = element.GetProperty("key").GetString();
                                return new IniSettingHandlerDescription(filename, section, key);
                            }
                        case "com.microsoft.windows.system":
                            {
                                var settingId = element.GetProperty("setting_id").GetString();
                                var valueType = element.GetProperty("value_type").GetString();
                                var valueKind = Enum.Parse<SystemValueKind>(valueType, ignoreCase: true);
                                return new SystemSettingHandlerDescription(settingId, valueKind);
                            }
                    }
                }
                catch
                {
                }
                return new SettingHandlerDescription(HandlerKind.Unknown);
            }

            public override void Write(Utf8JsonWriter writer, SettingHandlerDescription value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class ClientSettingHandlerDescription : SettingHandlerDescription
    {

        public Preferences.Key Key;

        public ClientSettingHandlerDescription(Preferences.Key key) : base(HandlerKind.Client)
        {
            Key = key;
        }

        public override bool Equals(object? obj)
        {
            if (obj is ClientSettingHandlerDescription other)
            {
                return other.Key.Equals(Key);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }

    /// <summary>
    /// A registry handler description with registry key names
    /// </summary>
    public class RegistrySettingHandlerDescription : SettingHandlerDescription
    {

        /// <summary>
        /// The full key name in the registry
        /// </summary>
        public string KeyName;

        /// <summary>
        /// The value name within the key
        /// </summary>
        public string ValueName;

        /// <summary>
        /// The expected value kind
        /// </summary>
        public RegistryValueKind ValueKind;

        /// <summary>
        /// Create a new registry handler description
        /// </summary>
        /// <param name="keyName"></param>
        /// <param name="valueName"></param>
        /// <param name="valueKind"></param>
        public RegistrySettingHandlerDescription(string keyName, string valueName, RegistryValueKind valueKind) : base(HandlerKind.Registry)
        {
            KeyName = keyName;
            ValueName = valueName;
            ValueKind = valueKind;
        }

        public override bool Equals(object? obj)
        {
            if (obj is RegistrySettingHandlerDescription other)
            {
                return other.KeyName == KeyName && other.ValueName == ValueName;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return KeyName.GetHashCode() ^ ValueName.GetHashCode();
        }
    }

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

    /// <summary>
    /// An ini handler description that specifies which part of the ini file should be udpated
    /// </summary>
    public class SystemSettingHandlerDescription : SettingHandlerDescription
    {

        /// <summary>
        /// The filename of the ini file, possibly including environmental variables
        /// </summary>
        public string SettingId;

        /// <summary>
        /// The kind of value expected for this setting
        /// </summary>
        public SystemValueKind ValueKind;

        /// <summary>
        /// Create a new ini file handler
        /// </summary>
        /// <param name="settingId"></param>
        public SystemSettingHandlerDescription(string settingId, SystemValueKind valueKind) : base(HandlerKind.System)
        {
            SettingId = settingId;
            ValueKind = valueKind;
        }

        public override bool Equals(object? obj)
        {
            if (obj is SystemSettingHandlerDescription other)
            {
                return other.SettingId == SettingId;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return SettingId.GetHashCode();
        }
    }
}
