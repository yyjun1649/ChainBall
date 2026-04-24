using System;
using System.Collections.Generic;
using UnityEngine;

namespace Library
{
    public static class DictionaryCacheHandler
    {
        private static List<IClearable> _clearables;

        public static void AddClearable(IClearable clearable)
        {
            if (_clearables == null)
            {
                _clearables = new List<IClearable>();
            }

            _clearables.Add(clearable);
        }

        public static void ClearAll()
        {
            if (_clearables == null)
            {
                return;
            }

            foreach (var clearable in _clearables)
            {
                clearable.Clear();
            }
        }
    }

    public class DictionaryCache<K, T> : IClearable
    {
        private Dictionary<K, T> _cache = new Dictionary<K, T>();
        public Dictionary<K, T> Cache => _cache;

        private bool _isInited;

        public virtual T TryGetValue(K key, Func<T> cacheFunc)
        {
            if (!_isInited)
            {
                DictionaryCacheHandler.AddClearable(this);
                _isInited = true;
            }

            if (!_cache.TryGetValue(key, out T value))
            {
                value = cacheFunc();

                if (value == null)
                {
                    Debug.LogError($"Cache Fail : return null. Key: {key} Type : {nameof(T)}");
                    return default(T);
                }

                _cache.Add(key, value);
            }

            return value;
        }

        public void Clear()
        {
            _cache.Clear();
            _isInited = false;
        }
    }

    public interface IClearable
    {
        public void Clear();
    }
}

