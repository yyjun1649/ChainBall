using Sigtrap.Relays;

namespace Library
{
    using System;
    using System.Collections;
    using UnityEngine;

    public sealed class TimeHandler : MonoBehaviour
    {
        #region Coroutine

        private Coroutine _tickerCoroutine;
        private Coroutine _minuteCoroutine;

        private bool _isPaused;
        private bool _isActivate;

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                _isPaused = true;
                StopTimeManager();
            }
            else
            {
                if (_isPaused)
                {
                    _isPaused = false;
                    StartTimeCoroutine();
                }
            }
        }

        public void StartTimeCoroutine()
        {
            if (!_isActivate)
            {
                return;
            }

            _tickerCoroutine = StartCoroutine(TickCoroutine());
            _minuteCoroutine = StartCoroutine(MinuteCoroutine());
        }


        private void StopTimeManager()
        {
            if (_tickerCoroutine != null)
            {
                StopCoroutine(_tickerCoroutine);
            }

            if (_minuteCoroutine != null)
            {
                StopCoroutine(_minuteCoroutine);
            }

            StopAllCoroutines();
        }

        #endregion

        #region Date

        public DateTime DateTimeToday => DateTimeNow.Date;

        public DateTime DateTimeNow
        {
            get
            {
                var dateTime = UtilLibrary.UnixTimeToDateTime(ServerUnixTime);

                return dateTime;
            }
        }

        public long ServerUnixTime => _serverUnixTime + ((long)Time.realtimeSinceStartup - _refreshTime);

        private long _serverUnixTime;
        private long _refreshTime;

        public void SetServerTime(long unixTime)
        {
            if (unixTime <= 0)
            {
                return;
            }

            _serverUnixTime = unixTime;
            _refreshTime = (long)Time.realtimeSinceStartup;
        }


        public DayOfWeek GetServerDayOfWeek()
        {
            return DateTimeNow.DayOfWeek;
        }

        #endregion


        public void SetTimeScale(float scale)
        {
            Time.timeScale = scale;
        }


        #region delegate

        private IEnumerator TickCoroutine()
        {
            var second = new WaitForSecondsRealtime(1f);

            while (true)
            {
                yield return second;

                try
                {
                    _onTick?.Dispatch();
                }
                catch (Exception e)
                {

                }
            }
        }

        private void Update()
        {
            if (!_isActivate || _isPaused)
            {
                return;
            }
            
            _onUpdate?.Dispatch(Time.deltaTime);
        }


        private IEnumerator MinuteCoroutine()
        {
#if __DEV || UNITY_EDITOR
            var second = new WaitForSecondsRealtime(10);
#else
        var second = new WaitForSecondsRealtime(60);
#endif

            while (true)
            {
                _onMinute?.Dispatch();

                yield return second;
            }
        }


        private Relay _onTick = new Relay();
        private Relay _onMinute = new Relay();
        private Relay _onNextDay = new Relay();
        private Relay<float> _onUpdate = new Relay<float>();
        
        public void AddOnUpdate(Action<float> callback)
        {
            _onUpdate.AddListener(callback);

            callback.Invoke(Time.deltaTime);
        }
        
        public void RemoveOnUpdate(Action<float> callback)
        {
            _onUpdate.RemoveListener(callback);
        }


        public void AddOnTickCallback(Action callback)
        {
            
            _onTick.AddListener(callback);

            callback.Invoke();
        }


        public void RemoveOnTickCallback(Action callback)
        {
            _onTick.RemoveListener(callback);
        }


        public void AddOnMinuteCallback(Action callback)
        {
            _onMinute.AddListener(callback);
        }


        public void RemoveOnMinuteCallback(Action callback)
        {
            _onMinute.RemoveListener(callback);
        }


        public void AddOnNextDay(Action callback)
        {
            _onNextDay.AddListener(callback);
        }


        public void RemoveOnNextDay(Action callback)
        {
            _onNextDay.RemoveListener(callback);
        }

        public void SetActivate()
        {
            _isActivate = true;

            StartTimeCoroutine();
        }

        public void ResetActivate()
        {
            _onTick.RemoveAll();
            _onNextDay.RemoveAll();
            _onMinute.RemoveAll();
            _onUpdate.RemoveAll();

            _isActivate = false;

            StopTimeManager();
        }

        #endregion
    }
}
