using System;
using UnityEngine;

namespace Library
{
    public abstract class BaseScene : MonoBehaviour
    {
        private void Awake()
        {
            Handlers.Scene.RegisterScene(this);
        }

        private void Start()
        {
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