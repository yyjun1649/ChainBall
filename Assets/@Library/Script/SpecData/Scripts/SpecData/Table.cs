// Assets/Scripts/SpecData/Table.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace SpecData
{
    /// <summary>
    /// 스펙 테이블 런타임 컨테이너.
    /// TKey 로 O(1) 조회 + 전체 순회 지원.
    /// </summary>
    public sealed class Table<TKey, TValue>
    {
        readonly Dictionary<TKey, TValue> _map;
        public IReadOnlyList<TValue> All { get; }
        public int Count => _map.Count;

        Table(List<TValue> list, Func<TValue, TKey> keySelector)
        {
            All = list;
            _map = new Dictionary<TKey, TValue>(list.Count);
            foreach (var v in list)
            {
                var k = keySelector(v);
                if (_map.ContainsKey(k))
                {
                    Debug.LogWarning(
                        $"[SpecData] duplicate key '{k}' in {typeof(TValue).Name}, later row ignored.");
                    continue;
                }
                _map[k] = v;
            }
        }

        public TValue Get(TKey key)
        {
            if (_map.TryGetValue(key, out var v)) return v;
            throw new KeyNotFoundException($"[{typeof(TValue).Name}] key '{key}' not found.");
        }

        public bool TryGet(TKey key, out TValue value) => _map.TryGetValue(key, out value);
        public bool Contains(TKey key) => _map.ContainsKey(key);
        
        /// <summary>
        /// Addressable TextAsset 에서 동기 로드. 예) "SpecData/TTower".
        /// Handlers.Resource.GetTextAsset (WaitForCompletion) 을 통과하므로 부트 타임용.
        /// </summary>
        public static Table<TKey, TValue> LoadAddressable(string address, Func<TValue, TKey> keySelector)
        {
            var ta = Library.Handlers.Resource.GetTextAsset(address);
            if (ta == null)
                throw new InvalidOperationException($"[SpecData] addressable not found: {address}");

            var list = JsonConvert.DeserializeObject<List<TValue>>(ta.text, Settings);
            if (list == null)
                throw new InvalidOperationException($"[SpecData] failed to deserialize {address}");

            return new Table<TKey, TValue>(list, keySelector);
        }

        /// <summary>
        /// Addressable TextAsset 비동기 로드. 로딩 화면·프레임 예산 필요한 경우.
        /// </summary>
        public static async Cysharp.Threading.Tasks.UniTask<Table<TKey, TValue>> LoadAddressableAsync(
            string address, Func<TValue, TKey> keySelector)
        {
            var ta = await Library.Handlers.Resource.GetTextAssetAsync(address);
            if (ta == null)
                throw new InvalidOperationException($"[SpecData] addressable not found: {address}");

            var list = JsonConvert.DeserializeObject<List<TValue>>(ta.text, Settings);
            if (list == null)
                throw new InvalidOperationException($"[SpecData] failed to deserialize {address}");

            return new Table<TKey, TValue>(list, keySelector);
        }

        public static Table<TKey, TValue> LoadJson(string json, Func<TValue, TKey> keySelector)
        {
            var list = JsonConvert.DeserializeObject<List<TValue>>(json, Settings);
            return new Table<TKey, TValue>(list, keySelector);
        }

        public static readonly JsonSerializerSettings Settings = new()
        {
            Converters = { new StringEnumConverter() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };
    }
}
