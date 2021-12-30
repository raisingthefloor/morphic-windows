namespace Morphic.Settings.SolutionsRegistry
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    internal class SolutionsRegistryContractResolver : DefaultContractResolver
    {
        private readonly IServiceProvider serviceProvider;

        public SolutionsRegistryContractResolver(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }
        protected override JsonContract CreateContract(Type objectType)
        {
            JsonContract jsonContract = base.CreateContract(objectType);

            if (TypeResolver.IsSolutionService(objectType))
            {
                jsonContract.DefaultCreator = () => this.serviceProvider.GetService(objectType);
            }

            return jsonContract;
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty jsonProperty = base.CreateProperty(member, memberSerialization);

            // Allow private members to be deserialised.
            if (!jsonProperty.Writable && member is PropertyInfo propertyInfo)
            {
                jsonProperty.Writable = propertyInfo.GetSetMethod(true) is not null;
            }

            return jsonProperty;
        }
    }

    internal class SolutionsTextReader : JsonTextReader
    {
        public SolutionsTextReader(TextReader reader) : base(reader)
        {
        }

        public override object? Value
        {
            get
            {
                // Tweak the input fields "type" to be the special "$type" identifier.
                if (this.TokenType == JsonToken.PropertyName && base.Value is string value)
                {
                    if (value == "type")
                    {
                        return "$type";
                    }
                    else if (value.StartsWith('$'))
                    {
                        return "_" + value;
                    }
                }

                return base.Value;
            }
        }
    }

    /// <summary>
    /// Allow an array field to be a single item, which gets converted into a single item array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ArrayConverter<T> : JsonConverter
    {
        public override bool CanWrite => false;

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
            JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            return token.Type == JTokenType.Array
                ? token.ToObject<List<T>>()
                : new List<T> {token.ToObject<T>()!};
        }

        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(List<T>));
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }

}
