using System;
using System.Collections.Generic;

namespace SocialUniverse.Core
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
                _handlers[type] = list = new List<Delegate>();
            list.Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public static void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;
            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
                ((Action<T>)handler)(evt);
        }

        public static void Clear() => _handlers.Clear();
    }
}
