using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MiniHttpServer
{
    public enum ServiceLifetime
    {
        Transient,
        Singleton
    }

    public class ServiceDescriptor
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public ServiceLifetime Lifetime { get; }
        public object ImplementationInstance { get; set; }

        public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
        }
    }

    public class ServiceCollection
    {
        private readonly List<ServiceDescriptor> _descriptors = new();

        public void AddTransient<TService, TImplementation>() where TImplementation : TService
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Transient));
        }

        public void AddSingleton<TService, TImplementation>(TImplementation instance) where TImplementation : TService
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton)
            {
                ImplementationInstance = instance
            });
        }

        public void AddSingleton<TService, TImplementation>() where TImplementation : TService
        {
            _descriptors.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), ServiceLifetime.Singleton));
        }

        public ServiceProvider BuildServiceProvider()
        {
            return new ServiceProvider(_descriptors);
        }
    }

    public class ServiceProvider
    {
        private readonly Dictionary<Type, ServiceDescriptor> _services;
        private readonly Dictionary<Type, object> _singletons = new();

        public ServiceProvider(IEnumerable<ServiceDescriptor> descriptors)
        {
            _services = descriptors.ToDictionary(d => d.ServiceType);
        }

        public T GetService<T>() => (T)GetService(typeof(T));

        public object GetService(Type serviceType)
        {
            if (!_services.TryGetValue(serviceType, out var descriptor))
            {
                if (!serviceType.IsAbstract && !serviceType.IsInterface)
                {
                    return CreateInstance(serviceType);
                }
                throw new InvalidOperationException($"Сервис типа {serviceType.Name} не зарегистрирован.");
            }

            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                if (descriptor.ImplementationInstance != null)
                    return descriptor.ImplementationInstance;

                lock (_singletons)
                {
                    if (_singletons.TryGetValue(serviceType, out var instance))
                        return instance;

                    instance = CreateInstance(descriptor.ImplementationType);
                    _singletons[serviceType] = instance;
                    return instance;
                }
            }

            return CreateInstance(descriptor.ImplementationType);
        }

        private object CreateInstance(Type implementationType)
        {
            var constructors = implementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                return Activator.CreateInstance(implementationType);
            }

            var ctor = constructors[0];
            var parameters = ctor.GetParameters();
            var parameterValues = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterValues[i] = GetService(parameters[i].ParameterType);
            }

            return ctor.Invoke(parameterValues);
        }
    }
}


