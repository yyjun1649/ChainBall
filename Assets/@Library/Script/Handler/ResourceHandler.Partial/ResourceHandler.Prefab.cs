using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Library
{
    public sealed partial class ResourceHandler
    {
        private readonly DictionaryCache<string, GameObject> _cachePrefabs = new();

        // 동기 메서드 - 일반 사용 (캐시 우선, 없으면 동기 로드)
        public GameObject GetPrefab(string prefabName)
        {
            return _cachePrefabs.TryGetValue(prefabName, () => LoadPrefabSync(prefabName));
        }

        // Component 캐싱 안하니 자주 호출하는것이라면 다르게 구현
        public T GetPrefab<T>(string prefabName)
        {
            var prefab = _cachePrefabs.TryGetValue(prefabName, () => LoadPrefabSync(prefabName));
            return prefab.GetComponent<T>();
        }

        // 비동기 메서드 - 명시적 프리로딩용 (프레임 드롭 방지)
        public async UniTask<GameObject> GetPrefabAsync(string prefabName)
        {
            if (_cachePrefabs.Cache.TryGetValue(prefabName, out var cached))
            {
                return cached;
            }

            var prefab = await LoadPrefabAsync(prefabName);
            _cachePrefabs.Cache.Add(prefabName, prefab);
            return prefab;
        }

        public async UniTask<T> GetPrefabAsync<T>(string prefabName)
        {
            var prefab = await GetPrefabAsync(prefabName);
            return prefab.GetComponent<T>();
        }

        public void ClearCache()
        {
            foreach (var preafab in _cachePrefabs.Cache)
            {
                Addressables.Release(preafab.Value);
            }

            _cachePrefabs.Clear();
        }

        public void ClearPrefab(string prefabName, GameObject gameObject)
        {
            if (_cachePrefabs.Cache.ContainsKey(prefabName))
            {
                _cachePrefabs.Cache.Remove(prefabName);
            }

            Addressables.Release(gameObject);
            DestroyImmediate(gameObject);
        }

        // 동기 로드 - 캐시 미스 시에만 사용됨 (첫 로딩 시 약간의 프레임 드롭 가능)
        private GameObject LoadPrefabSync(string name)
        {
            var prefab = Addressables.LoadAssetAsync<GameObject>(name).WaitForCompletion();
            return prefab;
        }

        // 비동기 로드 - 프리로딩에 사용
        private async UniTask<GameObject> LoadPrefabAsync(string name)
        {
            var prefab = await Addressables.LoadAssetAsync<GameObject>(name).ToUniTask();
            return prefab;
        }
    }
}