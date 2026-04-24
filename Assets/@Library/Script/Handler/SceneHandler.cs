using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Library
{
    public class SceneHandler : MonoBehaviour
    {
        public BaseScene CurrentScene { get; private set; }

        [SerializeField] private Canvas _loadingCanvas;

        private bool _isLoading = false;

        public async UniTask ChangeSceneAsync(
            string sceneName,
            bool useLoading = false,
            float minDuration = 0f,
            CancellationToken cancellationToken = default)
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            try
            {
                if (Handlers.Feel != null)
                {
                    Handlers.Feel.StopAll();
                }

                if (useLoading && _loadingCanvas != null)
                {
                    _loadingCanvas.gameObject.SetActive(true);
                }

                float startTime = Time.realtimeSinceStartup;

                await SceneManager.LoadSceneAsync(sceneName).ToUniTask(cancellationToken: cancellationToken);

                if (useLoading && minDuration > 0f)
                {
                    float elapsed = Time.realtimeSinceStartup - startTime;
                    float remain = minDuration - elapsed;
                    if (remain > 0f)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(remain), cancellationToken: cancellationToken);
                    }
                }
            }
            finally
            {
                if (_loadingCanvas != null)
                {
                    _loadingCanvas.gameObject.SetActive(false);
                }
                _isLoading = false;
            }
        }

        public void RegisterScene(BaseScene scene)
        {
            CurrentScene = scene;
        }
    }
}
