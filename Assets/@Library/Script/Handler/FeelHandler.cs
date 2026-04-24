using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Library
{
    public class FeelHandler : MonoBehaviour
    {
        [SerializeField] private FeelPresetTable _presets;

        private Dictionary<string, MMF_Player> _presetByKey;
        private readonly Dictionary<MMF_Player, Queue<MMF_Player>> _pool =
            new Dictionary<MMF_Player, Queue<MMF_Player>>();
        private readonly Dictionary<MMF_Player, MMF_Player> _instanceToSource =
            new Dictionary<MMF_Player, MMF_Player>();
        private readonly HashSet<MMF_Player> _registered = new HashSet<MMF_Player>();

        private Transform _poolRoot;

        private void Awake()
        {
            var rootGO = new GameObject("FeelPoolRoot");
            rootGO.transform.SetParent(transform);
            rootGO.SetActive(false);
            _poolRoot = rootGO.transform;

            BuildPresetCache();
        }

        private void BuildPresetCache()
        {
            _presetByKey = new Dictionary<string, MMF_Player>();
            if (_presets == null) return;

            var entries = _presets.Entries;
            if (entries == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.key) || entry.prefab == null) continue;

                if (_presetByKey.ContainsKey(entry.key))
                {
                    Debug.LogError($"[FeelHandler] Duplicate preset key: {entry.key}");
                    continue;
                }
                _presetByKey.Add(entry.key, entry.prefab);
            }
        }

        public MMF_Player Play(string key, Transform target = null, Vector3? worldPos = null)
        {
            if (!_presetByKey.TryGetValue(key, out var prefab))
            {
                Debug.LogError($"[FeelHandler] Unknown preset key: {key}");
                return null;
            }

            var instance = Acquire(prefab);
            var tr = instance.transform;

            if (target != null)
            {
                tr.SetParent(target, worldPositionStays: false);
                tr.localPosition = Vector3.zero;
            }
            else
            {
                tr.SetParent(null);
            }

            if (worldPos.HasValue)
            {
                tr.position = worldPos.Value;
            }

            instance.gameObject.SetActive(true);
            _registered.Add(instance);
            instance.PlayFeedbacks();
            return instance;
        }

        public void Prewarm(string key, int count)
        {
            if (!_presetByKey.TryGetValue(key, out var prefab))
            {
                Debug.LogError($"[FeelHandler] Prewarm unknown key: {key}");
                return;
            }

            GetOrCreateQueue(prefab, out var queue);
            for (int i = 0; i < count; i++)
            {
                queue.Enqueue(CreateInstance(prefab));
            }
        }

        public void Register(MMF_Player player)
        {
            if (player != null) _registered.Add(player);
        }

        public void Unregister(MMF_Player player)
        {
            if (player != null) _registered.Remove(player);
        }

        public void PauseAll()
        {
            foreach (var p in _registered)
            {
                if (p != null) p.PauseFeedbacks();
            }
        }

        public void ResumeAll()
        {
            foreach (var p in _registered)
            {
                if (p != null) p.ResumeFeedbacks();
            }
        }

        public void StopAll()
        {
            foreach (var p in _registered)
            {
                if (p != null) p.StopFeedbacks();
            }
        }

        // Reserved. Feel exposes no single uniform global time-scale knob across all feedbacks;
        // state is tracked here for future wiring through per-player timing overrides.
        public void SetTimeScale(float scale)
        {
            _timeScale = scale;
        }

        public float TimeScale => _timeScale;
        private float _timeScale = 1f;

        private MMF_Player Acquire(MMF_Player prefab)
        {
            GetOrCreateQueue(prefab, out var queue);
            return queue.Count > 0 ? queue.Dequeue() : CreateInstance(prefab);
        }

        private MMF_Player CreateInstance(MMF_Player prefab)
        {
            var go = Instantiate(prefab.gameObject, _poolRoot);
            go.SetActive(false);
            var instance = go.GetComponent<MMF_Player>();
            _instanceToSource[instance] = prefab;

            if (instance.Events != null && instance.Events.OnComplete != null)
            {
                instance.Events.OnComplete.AddListener(() => OnPlayComplete(instance));
            }
            else
            {
                Debug.LogError($"[FeelHandler] {prefab.name}.Events.OnComplete is null; pooled release is disabled for this preset.");
            }

            return instance;
        }

        private void OnPlayComplete(MMF_Player instance)
        {
            if (instance == null) return;
            if (!_instanceToSource.TryGetValue(instance, out var source)) return;

            _registered.Remove(instance);
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_poolRoot, worldPositionStays: false);

            GetOrCreateQueue(source, out var queue);
            queue.Enqueue(instance);
        }

        private void GetOrCreateQueue(MMF_Player prefab, out Queue<MMF_Player> queue)
        {
            if (!_pool.TryGetValue(prefab, out queue))
            {
                queue = new Queue<MMF_Player>();
                _pool.Add(prefab, queue);
            }
        }
    }
}
