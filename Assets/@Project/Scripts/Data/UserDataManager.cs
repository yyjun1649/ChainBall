using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Library;
using UnityEngine;

public class UserDataManager : Singleton<UserDataManager>
{
    private Dictionary<Type, UserData> dataDict = new Dictionary<Type, UserData>();
    private bool _isLoaded;
    private bool _isInitialized;

    public bool IsLoaded => _isLoaded;
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// ES3 load + EnsureData/캐시 빌드까지 한 번에 수행.
    /// </summary>
    public async UniTask LoadAllAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return;

        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        LoadAll();
        _isLoaded = true;

        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        InitializeAllData();

        DebugUtil.Log("[UserDataManager] 데이터 로드/초기화 완료");
    }

    /// <summary>
    /// 모든 데이터 초기화 (EnsureData, 캐시 빌드 등)
    /// DataSync 이후에 호출해야 함
    /// </summary>
    public void InitializeAllData()
    {
        if (_isInitialized) return;

        foreach (var data in dataDict.Values)
        {
            data.Initialize();
        }

        _isInitialized = true;
        DebugUtil.Log("[UserDataManager] 데이터 초기화 완료");
    }
    
    
    private void RegisterAndLoad<T>(T data) where T : UserData
    {
        Type type = typeof(T);
        if (dataDict.ContainsKey(type))
        {
            return;
        }

        dataDict[type] = data;
        data.Load();
    }
    
    public void UnregisterData<T>() where T : UserData
    {
        Type type = typeof(T);
        if (dataDict.ContainsKey(type))
        {
            dataDict.Remove(type);
        }
    }

    public T GetData<T>() where T : UserData
    {
        Type type = typeof(T);
        if (dataDict.TryGetValue(type, out UserData data))
        {
            return data as T;
        }

        return null;
    }

    public IEnumerable<UserData> GetAllData()
    {
        return dataDict.Values;
    }

    public UserData GetDataByType(string dataType)
    {
        foreach (var data in dataDict.Values)
        {
            if (data.DataType == dataType)
            {
                return data;
            }
        }
        return null;
    }

    public void SaveData<T>() where T : UserData
    {
        Type type = typeof(T);
        if (dataDict.TryGetValue(type, out UserData data))
        {
            data.Save();
        }
    }

    public void LoadData<T>() where T : UserData
    {
        Type type = typeof(T);
        if (dataDict.TryGetValue(type, out UserData data))
        {
            data.Load();
        }
    }

    public void DeleteData<T>() where T : UserData
    {
        Type type = typeof(T);
        if (dataDict.TryGetValue(type, out UserData data))
        {
            data.Delete();
        }
    }

    public void SaveAll()
    {
        foreach (var data in dataDict.Values)
        {
            data.Save();
        }
    }

    public void LoadAll()
    {
        foreach (var data in dataDict.Values)
        {
            data.Load();
        }
    }

    public void DeleteAll()
    {
        foreach (var data in dataDict.Values)
        {
            data.Delete();
        }
    }

    private void OnApplicationQuit()
    {
        SaveAll();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveAll();
        }
    }
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReloadDomain()
    {
        Instance.dataDict.Clear();
        Instance._isLoaded = false;
        Instance._isInitialized = false;
    }
}
