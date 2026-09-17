using System;
using System.Collections.Generic;

namespace Escape.Core
{
    /// <summary>
    /// Typed event bus. Commands mutate state; events report what happened.
    /// UI listens here and never owns state.
    /// </summary>
    public interface IGameEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : IGameEvent;
        void Unsubscribe<T>(Action<T> handler) where T : IGameEvent;
        void Publish<T>(T evt) where T : IGameEvent;
        void Clear();
    }

    public sealed class GameEventBus : IGameEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (_handlers.TryGetValue(typeof(T), out var list)) list.Remove(handler);
        }

        public void Publish<T>(T evt) where T : IGameEvent
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;
            // Copy so handlers can unsubscribe during publish.
            var snapshot = list.ToArray();
            foreach (var d in snapshot) ((Action<T>)d)(evt);
        }

        public void Clear() => _handlers.Clear();
    }
}
