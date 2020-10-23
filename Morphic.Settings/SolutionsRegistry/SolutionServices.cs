namespace Morphic.Settings.SolutionsRegistry
{
    using System;
    using DotNetWindowsRegistry;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    public static class SolutionServices
    {
        /// <summary>
        /// Adds the services for the solutions registry (all classes marked with the [SrService] attribute).
        /// </summary>
        public static IServiceCollection AddSolutionsRegistryServices(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IRegistry>(s => new WindowsRegistry());
            foreach ((Type? type, SrServiceAttribute? attr) in TypeResolver.GetSolutionServices())
            {
                Type serviceType = attr.ServiceType ?? type;
                switch (attr.Lifetime)
                {
                    case ServiceLifetime.Singleton:
                        serviceCollection.AddSingleton(serviceType);
                        break;
                    case ServiceLifetime.Scoped:
                        serviceCollection.AddScoped(serviceType);
                        break;
                    case ServiceLifetime.Transient:
                        serviceCollection.AddTransient(serviceType);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return serviceCollection;
        }

        public static IServiceCollection Replace<TService>(this IServiceCollection serviceCollection, Type implementationType, ServiceLifetime lifetime)
        {
            serviceCollection.Replace(new ServiceDescriptor(typeof(TService), implementationType, lifetime));
            return serviceCollection;
        }
        public static IServiceCollection Replace<TService>(this IServiceCollection serviceCollection, Func<IServiceProvider, object> factory, ServiceLifetime lifetime)
        {
            serviceCollection.Replace(new ServiceDescriptor(typeof(TService), factory, lifetime));
            return serviceCollection;
        }

        public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection serviceCollection, Func<IServiceProvider, object> factory)
        {
            serviceCollection.Replace<TService>(factory, ServiceLifetime.Singleton);
            return serviceCollection;
        }
        public static IServiceCollection ReplaceTransient<TService>(this IServiceCollection serviceCollection, Func<IServiceProvider, object> factory)
        {
            serviceCollection.Replace<TService>(factory, ServiceLifetime.Transient);
            return serviceCollection;
        }

        public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection serviceCollection, Type implementationType)
        {
            serviceCollection.Replace<TService>(implementationType, ServiceLifetime.Singleton);
            return serviceCollection;
        }
        public static IServiceCollection ReplaceTransient<TService>(this IServiceCollection serviceCollection, Type implementationType)
        {
            serviceCollection.Replace<TService>(implementationType, ServiceLifetime.Transient);
            return serviceCollection;
        }

    }
}
