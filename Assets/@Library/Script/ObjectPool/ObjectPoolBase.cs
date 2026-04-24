using System.Collections.Generic;
using Cysharp.Text;
using Library;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 풀 통계 정보
/// </summary>
public struct PoolStatistics
{
    public string PoolName;
    public int PooledCount;  // 풀에 대기 중인 개수
    public int ActiveCount;  // 현재 사용 중인 개수
    public int TotalCount;   // 전체 개수
}

/// <summary>
/// 오브젝트 풀 공통 인터페이스 (Reflection 방지용)
/// </summary>
public interface IObjectPool
{
    void TrimAllPools();
    void ReleaseAll();
    System.Collections.Generic.Dictionary<int, PoolStatistics> GetAllStatistics();
}

public class ObjectPoolBase<T> : IObjectPool where T : PoolMonoBehaviour<T>
{
    protected Dictionary<int, Queue<T>> _objectPools = new Dictionary<int, Queue<T>>();
    private Dictionary<int, List<T>> _activeObjects = new Dictionary<int, List<T>>();
    protected GameObject _poolRoot;

    // 메모리 관리 설정
    private int _maxPoolSize = 100; // 각 풀의 최대 크기
    private int _prewarmCount = 5; // 사전 생성 개수

    private string _poolName;

    public Transform Root => _poolRoot.transform;

    public int MaxPoolSize
    {
        get => _maxPoolSize;
        set => _maxPoolSize = Mathf.Max(1, value);
    }

    public int PrewarmCount
    {
        get => _prewarmCount;
        set => _prewarmCount = Mathf.Max(0, value);
    }

    public ObjectPoolBase(string poolName, Transform parent)
    {
        _poolName = poolName;
        _poolRoot = new GameObject(poolName);
        _poolRoot.transform.SetParent(parent);
    }

    /// <summary>
    /// 특정 오브젝트를 미리 생성
    /// </summary>
    public void Prewarm(int id, int count = -1)
    {
        int prewarmCount = count > 0 ? count : _prewarmCount;
        GetOrAddQueue(id, out var queue);

        for (int i = 0; i < prewarmCount; i++)
        {
            InstantiateObject(id);
        }
    }

    public void AddCanvas(RenderMode renderMode)
    {
        var canvas = _poolRoot.AddComponent<Canvas>();
        var scaler = _poolRoot.AddComponent<CanvasScaler>();
        _poolRoot.AddComponent<GraphicRaycaster>();

        canvas.renderMode = renderMode;

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            canvas.transform.position = Vector3.zero;
            canvas.transform.localScale = Vector3.one * 0.01f;
            canvas.worldCamera = MainCamera.Camera;
            canvas.sortingLayerName = "VFX";
        }
        
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100;
    }

    public T Get(int id)
    {
        GetOrAddQueue(id, out var queue);
        GetOrAddActiveList(id, out var activeList);

        var obj = GetOrCreateObject(id);
        activeList.Add(obj);
        obj.OnGet();

        return obj;
    }

    public void Release(int id, T obj)
    {
        if (obj == null) return;

        GetOrAddQueue(id, out var queue);
        GetOrAddActiveList(id, out var activeList);

        if (queue.Contains(obj))
        {
            return;
        }
        
        obj.OnRelease();

        activeList.Remove(obj);

        // 최대 풀 크기를 초과하면 파괴
        if (queue.Count >= _maxPoolSize)
        {
            GameObject.Destroy(obj.gameObject);
            return;
        }

        queue.Enqueue(obj);
    }

    public void UnloadUnusedAssets(int id)
    {
        if (!_activeObjects.TryGetValue(id, out var activeList))
            return;

        // Active 오브젝트 파괴
        foreach (var obj in activeList)
        {
            if (obj != null)
                GameObject.Destroy(obj.gameObject);
        }
        activeList.Clear();

        // 풀의 오브젝트도 파괴
        if (_objectPools.TryGetValue(id, out var queue))
        {
            while (queue.Count > 0)
            {
                var obj = queue.Dequeue();
                if (obj != null)
                    GameObject.Destroy(obj.gameObject);
            }
        }
    }

    public void ReleaseAll()
    {
        foreach (var kvp in _activeObjects)
        {
            var key = kvp.Key;
            var activeList = kvp.Value;

            if (!_objectPools.TryGetValue(key, out var queue))
                continue;

            foreach (var obj in activeList)
            {
                if (obj == null) continue;

                obj.OnRelease();

                // 최대 크기 체크
                if (queue.Count < _maxPoolSize)
                {
                    queue.Enqueue(obj);
                }
                else
                {
                    GameObject.Destroy(obj.gameObject);
                }
            }
            activeList.Clear();
        }
    }

    /// <summary>
    /// 특정 풀의 사용되지 않는 오브젝트 정리 (절반만 유지)
    /// </summary>
    public void TrimPool(int id, int targetCount = -1)
    {
        if (!_objectPools.TryGetValue(id, out var queue))
            return;

        int keepCount = targetCount > 0 ? targetCount : Mathf.Max(queue.Count / 2, _prewarmCount);

        while (queue.Count > keepCount)
        {
            var obj = queue.Dequeue();
            if (obj != null)
                GameObject.Destroy(obj.gameObject);
        }
    }

    /// <summary>
    /// 모든 풀 정리
    /// </summary>
    public void TrimAllPools()
    {
        foreach (var key in _objectPools.Keys)
        {
            TrimPool(key);
        }
    }

    /// <summary>
    /// 통계 정보 얻기
    /// </summary>
    public PoolStatistics GetStatistics(int id)
    {
        int pooledCount = _objectPools.TryGetValue(id, out var queue) ? queue.Count : 0;
        int activeCount = _activeObjects.TryGetValue(id, out var list) ? list.Count : 0;

        return new PoolStatistics
        {
            PoolName = id.ToString(),
            PooledCount = pooledCount,
            ActiveCount = activeCount,
            TotalCount = pooledCount + activeCount
        };
    }

    /// <summary>
    /// 모든 풀의 통계 정보
    /// </summary>
    public Dictionary<int, PoolStatistics> GetAllStatistics()
    {
        var stats = new Dictionary<int, PoolStatistics>();
        var allKeys = new HashSet<int>(_objectPools.Keys);

        foreach (var key in _activeObjects.Keys)
        {
            allKeys.Add(key);
        }

        foreach (var key in allKeys)
        {
            stats[key] = GetStatistics(key);
        }

        return stats;
    }

    private T GetOrCreateObject(int id)
    {
        if (_objectPools[id].Count == 0)
        {
            InstantiateObject(id);
        }

        return _objectPools[id].Dequeue();
    }

    private string _addressFormatCache;

    private void EnsureAddressFormat()
    {
        if (_addressFormatCache != null) return;

        var tempGO = new GameObject("__pool_format_reader__");
        tempGO.SetActive(false);
        tempGO.hideFlags = HideFlags.HideAndDontSave;
        var prototype = tempGO.AddComponent<T>();
        var format = prototype.AddressFormat;
        GameObject.Destroy(tempGO);

        if (string.IsNullOrEmpty(format) || !format.Contains("{0}"))
        {
            throw new System.InvalidOperationException(
                $"{typeof(T).Name}.AddressFormat must contain '{{0}}' placeholder. Got: '{format}'");
        }

        _addressFormatCache = format;
    }

    protected virtual void InstantiateObject(int id)
    {
        EnsureAddressFormat();

        var prefab = Handlers.Resource.GetPrefab(ZString.Format(_addressFormatCache, id));
        var created = GameObject.Instantiate(prefab, _poolRoot.transform);
        var component = created.GetComponent<T>();
        component.poolObjectId = id;
        component.SourcePool = this;
        _objectPools[id].Enqueue(component);
    }

    private void GetOrAddActiveList(int id, out List<T> activeList)
    {
        if (!_activeObjects.TryGetValue(id, out var list))
        {
            list = new List<T>();
            _activeObjects.Add(id, list);
        }

        activeList = list;
    }

    private void GetOrAddQueue(int id, out Queue<T> queue)
    {
        if (!_objectPools.TryGetValue(id, out var q))
        {
            q = new Queue<T>();
            _objectPools.Add(id, q);
        }

        queue = q;
    }
}
