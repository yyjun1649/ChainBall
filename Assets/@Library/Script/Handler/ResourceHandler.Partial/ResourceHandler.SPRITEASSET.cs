using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

namespace Library
{
    public sealed partial class ResourceHandler
    {
        private readonly DictionaryCache<string, SpriteLibraryAsset> _spriteLibraryCache = new();

        public SpriteLibraryAsset GetSpriteLibrary(string libName)
        {
            return _spriteLibraryCache.TryGetValue(libName, () => LoadSpriteLibrary(libName));
        }

        public void ClearSpriteLibraryCache()
        {
            foreach (var lib in _spriteLibraryCache.Cache)
            {
                Addressables.Release(lib.Value);
            }

            _spriteLibraryCache.Clear();
        }

        private SpriteLibraryAsset LoadSpriteLibrary(string libName)
        {
            var lib = Addressables.LoadAssetAsync<SpriteLibraryAsset>(libName).WaitForCompletion();

            return lib;
        }
    }
}
