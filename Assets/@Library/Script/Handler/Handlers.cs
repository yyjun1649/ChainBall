using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;

namespace Library
{
    public class Handlers : SingletonBehaviour<Handlers>
    {
        public static ResourceHandler Resource => Instance._resourceHandler;
        public static SoundHandler Sound => Instance._soundHandler;
        public static UIHandler UI => Instance._uiHandler;
        public static EventHandler Event => Instance._eventHandler;

        public static SceneHandler Scene => Instance._sceneHandler;
        public static TimeHandler Time => Instance._timeHandler;
        public static PoolHandler Pool => Instance._poolHandler;
        public static FeelHandler Feel => Instance._feelHandler;

        private ResourceHandler _resourceHandler;
        private SoundHandler _soundHandler;
        private UIHandler _uiHandler;
        private EventHandler _eventHandler;
        private SceneHandler _sceneHandler;
        private TimeHandler _timeHandler;
        private PoolHandler _poolHandler;
        private FeelHandler _feelHandler;

        protected override void Awake()
        {
            base.Awake();

            _resourceHandler = GetComponentInChildren<ResourceHandler>();
            _soundHandler = GetComponentInChildren<SoundHandler>();
            _uiHandler = GetComponentInChildren<UIHandler>();
            _eventHandler = GetComponentInChildren<EventHandler>();
            _sceneHandler = GetComponentInChildren<SceneHandler>();
            _timeHandler = GetComponentInChildren<TimeHandler>();
            _poolHandler = GetComponentInChildren<PoolHandler>();
            _feelHandler = GetComponentInChildren<FeelHandler>();
        }

        public async UniTask Initialize(CancellationToken cancellationToken = default)
        {
            Screen.fullScreen = true; //풀스크린
            QualitySettings.vSyncCount = 0; //VSync 비활성화
            Application.targetFrameRate = 60; //프레임레이드 60
            Screen.sleepTimeout = SleepTimeout.NeverSleep; //슬립 없도록
            GarbageCollector.GCMode = GarbageCollector.Mode.Enabled; //가비지 콜렉팅 활성화

#if UNITY_EDITOR
            Application.runInBackground = true;
#else
        Application.runInBackground = false;
#endif
            
            #if __SRD
            SRDebug.Init();
            #endif

            await _resourceHandler.InitializeAddressable(cancellationToken);

            _timeHandler.StartTimeCoroutine();
        }
    }
}