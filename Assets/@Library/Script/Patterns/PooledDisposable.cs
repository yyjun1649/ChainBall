using System;
using System.Collections.Generic;

namespace Library
{
    public abstract class PooledDisposable : IDisposable
    {
        private static readonly Dictionary<Type, Stack<PooledDisposable>> Pools
            = new Dictionary<Type, Stack<PooledDisposable>>();
        private bool _disposed;

        public static T Get<T>() where T : PooledDisposable, new()
        {
            var type = typeof(T);
            if (!Pools.TryGetValue(type, out var pool))
            {
                pool = new Stack<PooledDisposable>();
                Pools[type] = pool;
            }

            T inst = pool.Count > 0
                ? (T)pool.Pop()
                : new T();

            inst._disposed = false;
            return inst;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Reset();
            Pools[GetType()].Push(this);
            GC.SuppressFinalize(this);
        }

        protected abstract void Reset();
    }
}