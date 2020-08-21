// BarJson.cs: Bar deserialisation.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public interface IDeserializable
    {
        public void Deserialized();
    }

    public static class BarJson
    {
        /// <summary>
        /// Loads some json data.
        /// </summary>
        /// <param name="reader">The input json.</param>
        /// <param name="existingBar">An existing bar to populate.</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T Load<T>(TextReader reader, T? existingBar = null)
            where T : class, IDeserializable
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                Error = (sender, args) =>
                {
                    args.ToString();
                }
            };
            
            JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
            BarJsonTextReader barJsonTextReader = new BarJsonTextReader(reader, "win");

            T? bar;
            if (existingBar == null)
            {
                bar = jsonSerializer.Deserialize<T>(barJsonTextReader);
            }
            else
            {
                bar = existingBar;
                jsonSerializer.Populate(barJsonTextReader, bar);
            }

            bar?.Deserialized();

            return bar!;
        }

        /// <summary>
        /// Customised JSON reader which handles platform specific fields. The platform for which a field is used,
        /// is identified by a '$id' suffix. A field with a platform identifier of the current platform will be
        /// used instead of one without. 
        ///
        /// For example:
        /// 
        /// "value": "default value",
        /// "value$win": "windows-specific value",
        /// "value$mac": "macOS-specific value
        /// 
        /// </summary>
        public class BarJsonTextReader : JsonTextReader
        {
            /// <summary>
            /// Field paths visited which have the platform identifier.
            /// </summary>
            private readonly HashSet<string> overridden = new HashSet<string>();

            public BarJsonTextReader(TextReader reader) : base(reader)
            {
            }

            public BarJsonTextReader(TextReader reader, string platformId) : this(reader)
            {
                this.PlatformId = platformId;
            }

            public string PlatformId { get; } = "win";

            public override object? Value
            {
                get
                {
                    if (this.TokenType == JsonToken.PropertyName)
                    {
                        string name = base.Value?.ToString() ?? string.Empty;
                        string platformId = string.Empty;
                        string path = this.Path;

                        // Take the platform identifier from the name.
                        if (name.Contains('$'))
                        {
                            string[]? parts = name.Split("$", 2);
                            if (parts.Length == 2)
                            {
                                name = parts[0];
                                platformId = parts[1].ToLowerInvariant();
                                path = path.Substring(0, path.Length - platformId.Length - 1);
                            }
                        }

                        if (platformId == this.PlatformId)
                        {
                            // It's for this platform - use this field, and mark as over-ridden so it takes
                            // precedence over subsequent fields with no platform ID.
                            this.overridden.Add(path);
                        }
                        else if (platformId == string.Empty)
                        {
                            // No platform ID on this field name - use it only if there hasn't already been a
                            // field with a platform ID.
                            if (this.overridden.Contains(path))
                            {
                                // Rename it so it's not used.
                                name = "_overridden:" + base.Value;
                            }
                        }
                        else
                        {
                            // Not for this platform - ignore this field.
                            name = "_ignored:" + base.Value;
                        }

                        return name;
                    }
                    else
                    {
                        return base.Value;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Used by a class to specify, by name, the type of item it supports.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class JsonTypeNameAttribute : Attribute
    {
        public JsonTypeNameAttribute(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
    
    /// <summary>
    /// Provides support for a polymorphic json object, while also allowing properties to deserialise with values
    /// from a child object.
    ///
    /// The base class identifies the JSON field which specifies the type name via the 2nd parameter of the
    /// JsonConverter attribute.
    ///
    /// The subclass specifies the type name which is supports via the JsonTypeName attribute. 
    /// </summary>
    public class TypedJsonConverter : JsonConverter
    {
        private readonly string typeFieldName;
        private readonly string defaultValue;

        public TypedJsonConverter(string typeFieldName, string defaultValue)
        {
            this.typeFieldName = typeFieldName;
            this.defaultValue = defaultValue;
        }

        /// <summary>
        /// Creates an instance of the class inheriting <c>baseType</c> which has the JsonTypeName attribute
        /// with the specified name.
        /// </summary>
        /// <param name="baseType">The base type.</param>
        /// <param name="name">The name of the type.</param>
        /// <returns>A class which inherits baseType.</returns>
        private object CreateInstance(Type baseType, string name)
        {
            // Find the class which has the JsonTypeName attribute with the given name.
            Type? type = GetJsonType(baseType, name);

            if (type == null)
            {
                if (baseType.GetCustomAttributes<JsonTypeNameAttribute>().Any())
                {
                    // The type has already been resolved at the property.
                    type = baseType;
                }
                else
                {
                    throw new JsonSerializationException(
                        $"Unable to get type of {baseType.Name} from '{this.typeFieldName} = ${name}'.");
                }
            }

            object? instance = Activator.CreateInstance(type);
            if (instance == null)
            {
                throw new JsonSerializationException(
                    $"Unable to instantiate ${type.Name} from '${this.typeFieldName} = ${name}'.");
            }
            return instance;
        }

        /// <summary>
        /// Finds a type which is a subclass of baseType, having a JsonTypeName attribute with the specified name. 
        /// </summary>
        /// <param name="baseType">The base class.</param>
        /// <param name="name">The name in the JsonTypeName attribute.</param>
        /// <returns>The type.</returns>
        private static Type? GetJsonType(Type baseType, string name)
        {
            return baseType.Assembly.GetTypes()
                .Where(t => !t.IsAbstract && t.IsSubclassOf(baseType))
                .FirstOrDefault(t => t.GetCustomAttribute<JsonTypeNameAttribute>()?.Name == name);
        }

        /// <summary>
        /// Gets the JSON field name for a given property, from either the JsonProperty attribute or the
        /// name of the property.
        /// </summary>
        /// <param name="property"></param>
        /// <returns></returns>
        private static string GetFieldName(MemberInfo property)
        {
            JsonPropertyAttribute? attribute = property.GetCustomAttributes<JsonPropertyAttribute>(true)
                .FirstOrDefault();
            return attribute?.PropertyName ?? property.Name;
        }

        /// <summary>
        /// Instantiates the correct subclass of objectType, as identified by the type field.
        /// 
        /// Also, makes the JsonProperty attribute allow a path into child objects.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
            JsonSerializer serializer)
        {
            JObject jo = JObject.Load(reader);

            // Get the type of item.
            string kindName = jo[this.typeFieldName]?.ToString() ?? this.defaultValue;
            
            // Create the class for the type.
            object? target = this.CreateInstance(objectType, kindName);

            // For each property, get the value using a path rather than just the field name.
            // (inspired by https://stackoverflow.com/a/33094930/67586)
            foreach (PropertyInfo property in target.GetType().GetProperties()
                .Where(p => p.CanRead && p.CanWrite))
            {
                // Get the value, using the path in the field name attribute.
                string jsonPath = GetFieldName(property);
                JToken? token = jo.SelectToken(jsonPath);

                if (token != null && token.Type != JTokenType.Null)
                {
                    Type? newType = this.GetNewType(jo, property);
                    object? value = newType == null
                        ? token.ToObject(property.PropertyType, serializer)
                        : token.ToObject(newType);
                    // Set the property value.
                    property.SetValue(target, value, null);
                }
            }

            return target;
        }

        /// <summary>
        /// Gets the actual type to use, from the property.
        /// </summary>
        /// <param name="jo">The current json object to look at.</param>
        /// <param name="property">The property.</param>
        /// <returns>The type to use, or null to use the property's own type</returns>
        private Type? GetNewType(JObject jo, PropertyInfo property)
        {
            JsonConverterAttribute? converter = property.GetCustomAttribute<JsonConverterAttribute>()
                                                ?? property.PropertyType.GetCustomAttribute<JsonConverterAttribute>();
            if (converter?.ConverterParameters == null || converter.ConverterType != this.GetType())
            {
                return null;
            }

            string nameField = (string) converter.ConverterParameters[0];
            string defaultValue = (string) converter.ConverterParameters[1];
            string name = jo[nameField]?.ToString() ?? defaultValue;
            return TypedJsonConverter.GetJsonType(property.PropertyType, name);

        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            // This isn't worth writing - the client only consumes JSON.
            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }

}
