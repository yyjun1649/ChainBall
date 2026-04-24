using Cysharp.Threading.Tasks;
using EnhancedUI.EnhancedScroller;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Library
{
    public sealed partial class ResourceHandler
    {
        private readonly DictionaryCache<string, EnhancedScrollerCellView> _cahcedSlots = new();

        // 동기 메서드 - 일반 사용
        public EnhancedScrollerCellView GetSlot(string prefabName)
        {
            return _cahcedSlots.TryGetValue(prefabName, () => LoadSlotSync(prefabName));
        }

        // 비동기 메서드 - 프리로딩용
        public async UniTask<EnhancedScrollerCellView> GetSlotAsync(string prefabName)
        {
            if (_cahcedSlots.Cache.TryGetValue(prefabName, out var cached))
            {
                return cached;
            }

            var cellView = await LoadSlotAsync(prefabName);
            _cahcedSlots.Cache.Add(prefabName, cellView);
            return cellView;
        }

        public void ClearSlotCache()
        {
            foreach (var preafab in _cahcedSlots.Cache)
            {
                Addressables.Release(preafab.Value);
            }

            _cahcedSlots.Clear();
        }

        // 동기 로드
        private EnhancedScrollerCellView LoadSlotSync(string prefabName)
        {
            var prefab = Addressables.LoadAssetAsync<GameObject>(prefabName).WaitForCompletion();
            return prefab.GetComponent<EnhancedScrollerCellView>();
        }

        // 비동기 로드
        private async UniTask<EnhancedScrollerCellView> LoadSlotAsync(string prefabName)
        {
            var prefab = await Addressables.LoadAssetAsync<GameObject>(prefabName).ToUniTask();
            return prefab.GetComponent<EnhancedScrollerCellView>();
        }
    }
}