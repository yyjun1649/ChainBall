using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Library
{
    public sealed partial class ResourceHandler
    {
        private readonly DictionaryCache<string, ScriptableObject> _cacheScriptable = new();

        public T GetScriptableObject<T>(string scriptableName) where T : ScriptableObject
        {
            return _cacheScriptable.TryGetValue(scriptableName, () => LoadScriptable(scriptableName)) as T;
        }

        public void ClearCacheScriptable()
        {
            foreach (var scriptable in _cacheScriptable.Cache)
            {
                Addressables.Release(scriptable.Value);
            }

            _cacheScriptable.Clear();
        }

        private ScriptableObject LoadScriptable(string name)
        {
            var prefab = Addressables.LoadAssetAsync<ScriptableObject>(name).WaitForCompletion();

            return prefab;
        }
    }
}