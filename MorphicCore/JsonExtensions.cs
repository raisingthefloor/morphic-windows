using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Collections.Generic;

namespace MorphicCore
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