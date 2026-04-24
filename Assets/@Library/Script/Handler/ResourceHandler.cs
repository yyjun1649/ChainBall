using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace Library
{
    public sealed partial class ResourceHandler : MonoBehaviour
    {
        private bool IsInitialize;

        public async UniTask InitializeAddressable(CancellationToken cancellationToken = default)
        {
            if (IsInitialize)
            {
                Debug.LogError("ResourceHandler의 어드레서블 리소스의 초기화는 이미 진행되었습니다.");
                return;
            }

            await LoadSpriteAtlasAsync(cancellationToken);

            IsInitialize = true;
        }
    }
}
