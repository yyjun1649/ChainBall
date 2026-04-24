using System;
using UnityEngine;

[Serializable]
public abstract class UserData
{
    public abstract string DataType { get; protected set; }
    
    public virtual void Initialize()
    {
        EnsureData();
     
        
        Save();
    }
    
    protected virtual void EnsureData()
    {

    }

    public void Save()
    {
        ES3.Save(DataType, this);
    }

    public void Load()
    {
        if (ES3.KeyExists(DataType))
        {
            ES3.LoadInto(DataType, this);
        }
        
        OnAfterLoad();
    }

    protected virtual void OnAfterLoad()
    {
    }

    public void Delete()
    {
        if (ES3.KeyExists(DataType))
        {
            ES3.DeleteKey(DataType); 
        }
    }
}
