using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Library
{
    public sealed partial class ResourceHandler
    {
        private readonly DictionaryCache<string, TextAsset> _cacheTextAsset = new();

        // 동기 메서드 - 부트 타임 스펙 데이터 로드에 사용 (WaitForCompletion)
        public TextAsset GetTextAsset(string address)
        {
            return _cacheTextAsset.TryGetValue(address, () => LoadTextAssetSync(address));
        }

        // 비동기 메서드 - 프리로딩/프레임 예산 있는 상황용
        public async UniTask<TextAsset> GetTextAssetAsync(string address)
        {
            if (_cacheTextAsset.Cache.TryGetValue(address, out var cached))
            {
                return cached;
            }

            var ta = await Addressables.LoadAssetAsync<TextAsset>(address).ToUniTask();
            _cacheTextAsset.Cache.Add(address, ta);
            return ta;
        }

        public void ClearCacheTextAsset()
        {
            foreach (var kv in _cacheTextAsset.Cache)
            {
                Addressables.Release(kv.Value);
            }

            _cacheTextAsset.Clear();
        }

        private TextAsset LoadTextAssetSync(string address)
        {
            return Addressables.LoadAssetAsync<TextAsset>(address).WaitForCompletion();
        }
    }
}
