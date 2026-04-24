using System;
using System.Collections.Generic;
using UnityEngine;

namespace Library
{
    public class PoolHandler : MonoBehaviour
    {
        private Dictionary<Type, IObjectPool> _objectPools = new Dictionary<Type, IObjectPool>();
        private Transform _poolRoot;

        private void Awake()
        {
            _poolRoot = new GameObject("ObjectPoolRoot").transform;
            _poolRoot.SetParent(transform);
        }

        public T Get<T>(int id) where T : PoolMonoBehaviour<T>
        {
            return GetObject<T>().Get(id);
        }

        public ObjectPoolBase<T> GetObject<T>() where T : PoolMonoBehaviour<T>
        {
            Type type = typeof(T);

            if (_objectPools.TryGetValue(type, out var objectPool))
            {
                return (ObjectPoolBase<T>)objectPool;
            }

            var newObjectPool = new ObjectPoolBase<T>(type.Name, _poolRoot);
            _objectPools.Add(type, newObjectPool);
            return newObjectPool;
        }

        public void Prewarm<T>(int id, int count = -1) where T : PoolMonoBehaviour<T>
        {
            GetObject<T>().Prewarm(id, count);
        }

        public void TrimAllPools()
        {
            foreach (var pool in _objectPools.Values)
            {
                pool.TrimAllPools();
            }
        }

        public void ReleaseAll<T>() where T : PoolMonoBehaviour<T>
        {
            if (_objectPools.TryGetValue(typeof(T), out var pool))
            {
                pool.ReleaseAll();
            }
        }

        public void ReleaseAllPools()
        {
            foreach (var pool in _objectPools.Values)
            {
                pool.ReleaseAll();
            }
        }

        public void DestroyPool<T>() where T : PoolMonoBehaviour<T>
        {
            Type type = typeof(T);
            if (_objectPools.TryGetValue(type, out var pool))
            {
                var poolBase = (ObjectPoolBase<T>)pool;
                if (poolBase.Root != null)
                {
                    Destroy(poolBase.Root.gameObject);
                }
                _objectPools.Remove(type);
            }
        }
    }
}
