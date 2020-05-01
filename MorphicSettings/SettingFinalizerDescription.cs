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
using MorphicCore;
using Microsoft.Win32;

namespace MorphicSettings
{

    /// <summary>
    /// Base class for describing the handler for a setting
    /// </summary>
    public class SettingFinalizerDescription
    {

        /// <summary>
        /// The possible kinds of handlers
        /// </summary>
        public enum HandlerKind
        {
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
        public SettingFinalizerDescription(HandlerKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// A custom JSON converter that creates the correct subclass based on the type property
        /// </summary>
        public class JsonConverter : System.Text.Json.Serialization.JsonConverter<SettingFinalizerDescription>
        {
            public override SettingFinalizerDescription Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                try
                {
                    var document = JsonDocument.ParseValue(ref reader);
                    var element = document.RootElement;
                    var type = element.GetProperty("type").GetString();
                    switch (type)
                    {
                        case "com.microsoft.windows.systemParametersInfo":
                            {
                                var actionString = element.GetProperty("action").GetString();
                                var action = Enum.Parse<SystemParametersInfo.Action>(actionString, ignoreCase: true);
                                var description = new SystemParametersInfoSettingFinalizerDescription(action);
                                try
                                {
                                    description.SendChange = element.GetProperty("send_change").GetBoolean();
                                }
                                catch
                                {
                                }
                                try
                                {
                                    description.UpdateUserProfile = element.GetProperty("update_user_profile").GetBoolean();
                                }
                                catch
                                {
                                }
                                return description;
                            }
                    }
                }
                catch
                {
                }
                return new SettingFinalizerDescription(HandlerKind.Unknown);
            }

            public override void Write(Utf8JsonWriter writer, SettingFinalizerDescription value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
    }

    /// <summary>
    /// An ini handler description that specifies which part of the ini file should be udpated
    /// </summary>
    public class SystemParametersInfoSettingFinalizerDescription : SettingFinalizerDescription
    {

        /// <summary>
        /// The filename of the ini file, possibly including environmental variables
        /// </summary>
        public SystemParametersInfo.Action Action;

        public int Parameter1 = 0;

        public object? Parameter2;

        /// <summary>
        /// Should a change notification be sent to the system
        /// </summary>
        public bool SendChange = false;

        /// <summary>
        /// Should the user's profile be updated
        /// </summary>
        public bool UpdateUserProfile = false;

        /// <summary>
        /// Create a new ini file handler
        /// </summary>
        /// <param name="settingId"></param>
        public SystemParametersInfoSettingFinalizerDescription(SystemParametersInfo.Action action) : base(HandlerKind.SystemParametersInfo)
        {
            Action = action;
        }

        public override bool Equals(object? obj)
        {
            if (obj is SystemParametersInfoSettingFinalizerDescription other)
            {
                return other.Action == Action && other.Parameter1 == Parameter1 && other.Parameter2 == Parameter2 && other.SendChange == SendChange && other.UpdateUserProfile == UpdateUserProfile;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Action.GetHashCode() ^ Parameter1 ^ (Parameter2?.GetHashCode() ?? 0) ^ SendChange.GetHashCode() ^ UpdateUserProfile.GetHashCode();
        }
    }
}
