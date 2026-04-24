using System;
using System.Collections.Generic;
using System.Threading;

namespace Library
{
    public abstract class DisposeObject<T> : IDisposable where T : DisposeObject<T>, new()
    {
        private static readonly Stack<T> Pool = new Stack<T>();
        private bool _isDisposed;

        public static T Get()
        {
            var instance = Pool.Count > 0 ? Pool.Pop() : new T();
            instance._isDisposed = false;
            return instance;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Reset();
            Pool.Push((T)this);
            GC.SuppressFinalize(this);
        }

        protected abstract void Reset();
    }
}