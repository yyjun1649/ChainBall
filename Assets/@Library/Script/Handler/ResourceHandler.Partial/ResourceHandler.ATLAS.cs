using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;

namespace Library
{
    public sealed partial class ResourceHandler
    {
        #region public

        /// <summary>
        /// 런타임 SpriteAtlas에 캐싱된 Sprite 꺼내오기.
        /// </summary>
        /// <param name="spriteName"> 스프라이트(이미지) 이름 </param>
        /// <returns> [Nullable] Sprite 객체 </returns>
        public Sprite GetSprite(string spriteName)
        {
            if (_sprites.TryGetValue(spriteName, out Sprite sprite))
            {
                return sprite;
            }

            foreach (SpriteAtlas atlas in _atlases)
            {
                sprite = atlas.GetSprite(spriteName);
                if (sprite == null)
                {
                    continue;
                }

                _sprites.TryAdd(spriteName, sprite);
                return sprite;
            }

            Debug.LogError($"Sprite Not Found : {spriteName}");

            return null;
        }

        #endregion

        #region private

        /// <summary>
        /// 아틀라스 캐싱
        /// </summary>
        private readonly List<SpriteAtlas> _atlases = new();

        /// <summary>
        /// 아틀라스에서 불러온 Sprite 캐싱
        /// </summary>
        private readonly Dictionary<string, Sprite> _sprites = new();

        /// <summary>
        /// Sprite Atlas 에셋 로드
        /// </summary>
        private async UniTask LoadSpriteAtlasAsync(CancellationToken cancellationToken)
        {
            _atlases.Clear();
            IList<IResourceLocation> list = await Addressables
                .LoadResourceLocationsAsync(eAddressableLabel.ATLAS.ToString())
                .ToUniTask(cancellationToken: cancellationToken);

            for (var index = 0; index < list.Count; index++)
            {
                if (list[index].ResourceType != typeof(SpriteAtlas)) continue;

                IResourceLocation location = list[index];
                var atlas = await Addressables.LoadAssetAsync<SpriteAtlas>(location)
                    .ToUniTask(cancellationToken: cancellationToken);
                if (atlas != null)
                {
                    _atlases.Add(atlas);
                }
            }
        }

        #endregion
    }
}
