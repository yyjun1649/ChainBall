using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Library
{
    public sealed partial class ResourceHandler
    {
        private readonly Dictionary<string, PopupBase> _cachePopups = new();
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _popupHandles = new();

        // 동기 메서드 - 일반 사용
        public PopupBase GetPopup(string popupName, out bool isNew)
        {
            isNew = false;

            if (!_cachePopups.TryGetValue(popupName, out var popup))
            {
                isNew = true;
                popup = LoadAndInstantiateSync(popupName);
                _cachePopups.Add(popupName, popup);
            }

            return popup;
        }

        // 비동기 메서드 - 프리로딩용
        public async UniTask<(PopupBase popup, bool isNew)> GetPopupAsync(string popupName)
        {
            bool isNew = false;

            if (!_cachePopups.TryGetValue(popupName, out var popup))
            {
                isNew = true;
                popup = await LoadAndInstantiateAsync(popupName);
                _cachePopups.Add(popupName, popup);
            }

            return (popup, isNew);
        }

        public void ClearPopupCache()
        {
            foreach (var kvp in _cachePopups)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }

            foreach (var handle in _popupHandles.Values)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _cachePopups.Clear();
            _popupHandles.Clear();
        }

        private PopupBase LoadAndInstantiateSync(string popupName)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(popupName);
            var prefab = handle.WaitForCompletion();
            _popupHandles[popupName] = handle;
            return Instantiate(prefab).GetComponent<PopupBase>();
        }

        private async UniTask<PopupBase> LoadAndInstantiateAsync(string popupName)
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(popupName);
            var prefab = await handle.ToUniTask();
            _popupHandles[popupName] = handle;
            return Instantiate(prefab).GetComponent<PopupBase>();
        }
    }
}
