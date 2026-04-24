using System;

namespace Library
{
    using UnityEngine;

    public class SingletonBehaviour<T> : MonoBehaviour, ISingleton where T : Component
    {
        protected static T _instance = null;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    // FindAnyObjectByType is faster than FindObjectOfType in Unity 2023+
                    _instance = FindAnyObjectByType<T>();

                    if (_instance == null)
                    {
                        DebugUtil.LogWarning($"[SingletonBehaviour] No instance of {typeof(T).Name} found in scene. Creating new instance. Consider adding it to the scene manually for better performance.");
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<T>();
                        obj.name = typeof(T).Name;
                    }
                }

                return _instance;
            }
        }

        /// <summary>
        /// 싱글톤 인스턴스가 존재하는지 확인 (인스턴스를 생성하지 않음)
        /// </summary>
        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_instance == null)
            {
                _instance = this as T;
            }
            else
            {
                if (this != _instance)
                {
                    DebugUtil.LogError("[X] 싱글톤(" + transform.name + ") : " + typeof(T).Name + "의 중복 생성을 시도하고 있습니다.");

                    Destroy(this.gameObject);
                }
            }
        }

        private void OnDestroy()
        {
            _instance = null;
        }
    }
}