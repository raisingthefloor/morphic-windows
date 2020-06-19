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

namespace Morphic.Settings
{

    using Spi;
    using Process;

    /// <summary>
    /// Base class for describing the handler for a setting
    /// </summary>
    public class SettingFinalizerDescription
    {

        /// <summary>
        /// The possible kinds of handlers
        /// </summary>
        public enum FinalizerKind
        {
            /// <summary>
            /// A Windows System Parameter Info call
            /// </summary>
            SystemParametersInfo,

            /// <summary>
            /// A Process restart
            /// </summary>
            Process,

            /// <summary>
            /// An unknown handler is used for any unrecogized or invalid handler JSON
            /// </summary>
            Unknown
        }

        /// <summary>
        /// The kind of this handler
        /// </summary>
        public FinalizerKind Kind { get; set; }

        /// <summary>
        /// Create a new handler for the given kind
        /// </summary>
        /// <param name="kind"></param>
        public SettingFinalizerDescription(FinalizerKind kind)
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
                            return new SystemParametersInfoSettingFinalizerDescription(element);
                        case "com.microsoft.windows.process":
                            return new ProcessSettingFinalizerDescription(element);
                    }
                }
                catch
                {
                }
                return new SettingFinalizerDescription(FinalizerKind.Unknown);
            }

            public override void Write(Utf8JsonWriter writer, SettingFinalizerDescription value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
    }
}
