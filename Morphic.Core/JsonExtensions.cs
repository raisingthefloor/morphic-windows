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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Collections.Generic;

namespace Morphic.Core
{

#nullable disable

    /// <summary>Convert <code>JSONElement</code>s into standard types like <code>long, string, object[], or Dictionary&lt;string, object></code></summary>
    /// <remarks>
    /// For cases such as <code>Preferences</code> where we allow arbirary JSON and can't indicate the correct types up-front in a class definition.
    /// System.Text.Json doesn't do this automatically, so we need to write our own.
    /// This implementation simply leverages the <code>JsonElement.GetObject</code> extension found below.
    /// </remarks>
    public class JsonElementInferredTypeConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.GetObject();
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            throw new InvalidOperationException("For JSON deserilization only");
        }
    }

    public static class JsonElementExtensions
    {

        /// <summary>Return an appropriate primitive type for a Jsonelement</summary>
        public static object GetObject(this JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long n))
                    {
                        return n;
                    }
                    return element.GetDouble();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Array:
                    {
                        var array = new object[element.GetArrayLength()];
                        var i = 0;
                        foreach (var child in element.EnumerateArray())
                        {
                            array[i++] = child.GetObject();
                        }
                        return array;
                    }
                case JsonValueKind.Object:
                    {
                        var dict = new Dictionary<string, object>();
                        foreach (var property in element.EnumerateObject())
                        {
                            dict[property.Name] = property.Value.GetObject();
                        }
                        return dict;
                    }
                default:
                    throw new Exception(String.Format("Unknown JsonValueKind: {0}", element.ValueKind.ToString()));
            }
        }
    }

#nullable enable

}