using System;
using UnityEngine;

namespace Library
{
    public abstract class BaseScene : MonoBehaviour
    {
        private void Start()
        {
            Handlers.Scene.RegisterScene(this);
            
            OnSceneLoaded();
        }

        private void OnDestroy()
        {
            OnSceneDestroy();
        }
        
        protected abstract void OnSceneLoaded();
        protected abstract void OnSceneDestroy();
    }
}