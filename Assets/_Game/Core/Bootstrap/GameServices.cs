using System;
using System.Collections.Generic;

namespace Escape.Core
{
    /// <summary>
    /// Lightweight service registry owned by GameRoot. Deliberately minimal —
    /// no DI framework.
    /// </summary>
    public sealed class GameServices
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public void Unregister<T>(T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var existing) && ReferenceEquals(existing, service))
                _services.Remove(typeof(T));
        }

        public T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s) && s != null) return (T)s;
            throw new InvalidOperationException($"Service not registered: {typeof(T).Name}");
        }

        public bool TryGet<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s) && s != null)
            {
                service = (T)s;
                return true;
            }
            service = null;
            return false;
        }
    }
}
