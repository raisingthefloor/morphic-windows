namespace Morphic.Settings.SolutionsRegistry
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using SettingsHandlers;

    public class TypeResolver : ISerializationBinder
    {
        private static readonly Dictionary<string, Type> SrTypes = new Dictionary<string, Type>();

        private static readonly Dictionary<Type, SrServiceAttribute> SrServices =
            new Dictionary<Type, SrServiceAttribute>();

        static TypeResolver()
        {
            LoadTypes();
        }

        private static void LoadTypes()
        {
            foreach (Type type in typeof(TypeResolver).Assembly.GetTypes())
            {
                foreach (SrTypeAttribute attr in type.GetCustomAttributes<SrTypeAttribute>(false))
                {
                    SrTypes.Add(attr.TypeName, type);
                }

                foreach (SrServiceAttribute attr in type.GetCustomAttributes<SrServiceAttribute>())
                {
                    SrServices.Add(type, attr);
                }

                foreach (SettingsHandlerTypeAttribute attr in type.GetCustomAttributes<SettingsHandlerTypeAttribute>())
                {
                    SrTypes.Add(attr.Name, type);
                    Solutions.AddSettingsHandler(attr.SettingsHandlerType, type);
                }
            }
        }

        public static bool IsSolutionService(Type type)
        {
            return SrServices.ContainsKey(type);
        }

        public static Dictionary<Type, SrServiceAttribute> GetSolutionServices()
        {
            return SrServices;
        }

        public static Type? ResolveType(string typeName)
        {
            return SrTypes.TryGetValue(typeName, out Type? type) ? type : null;
        }

        public Type BindToType(string? assemblyName, string typeName)
        {
            Type? type = ResolveType(typeName);
            if (type == null)
            {
                Console.WriteLine($"unknown type: {typeName}");
                return typeof(object);
                Environment.Exit(0);
            }

            return type!;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            throw new NotImplementedException();
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    sealed class SrTypeAttribute : Attribute
    {
        public string TypeName { get; }

        public SrTypeAttribute(string typeName)
        {
            this.TypeName = typeName;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class SrServiceAttribute : Attribute
    {
        public Type? ServiceType { get; }
        public ServiceLifetime Lifetime { get; }

        public SrServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Transient, Type? serviceType = null)
        {
            this.ServiceType = serviceType;
            this.Lifetime = lifetime;
        }
    }
}
