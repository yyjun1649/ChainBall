
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UI;

    public class DynamicSlider : MonoBehaviour
    {
        [SerializeField] private Slider _currentSlider;
        [SerializeField] private Slider _backGroundSlider;
        
        private float _lastValue;
        private float _lastBackValue;

        private Coroutine _currentCoroutine;
        private Coroutine _backGroundCoroutine;

        private WaitForFixedUpdate wof1 = new WaitForFixedUpdate();
        private WaitForFixedUpdate wof2 = new WaitForFixedUpdate();
        private WaitForSeconds wos = new WaitForSeconds(0.3f);

        public void Initialize(float v, float maxValue)
        {
            _lastValue = v / maxValue;
            _lastBackValue = v / maxValue;

            _currentSlider.value = _lastValue;
            _backGroundSlider.value = _lastBackValue;
        }
        
        public void SetValue(float newValue)
        {
            if (_currentCoroutine != null)
            {
                StopCoroutine(_currentCoroutine);
            }

            _currentCoroutine = StartCoroutine(CoSlideCurrentValue(newValue , 0.3f));
            
            if (_backGroundCoroutine != null)
            {
                StopCoroutine(_backGroundCoroutine);
            }

            _currentCoroutine = StartCoroutine(CoSlideBackValue(newValue , 1f));
        }

        private IEnumerator CoSlideCurrentValue(float endValue , float time)
        {
            var currentValue = _lastValue;

            var elaspedTime = 0f;

            while (elaspedTime < time)
            {
                _lastValue = Mathf.Lerp(currentValue, endValue, elaspedTime / time);

                _currentSlider.value = _lastValue / 1f;
                
                elaspedTime += Time.deltaTime;
                
                yield return wof1;
            }

            _currentSlider.value = endValue;
        }
        
        private IEnumerator CoSlideBackValue(float endValue , float time)
        {
            var currentValue = _lastBackValue;

            var elaspedTime = 0f;

            yield return wos;

            while (elaspedTime < time)
            {
                _lastBackValue = Mathf.Lerp(currentValue, endValue, elaspedTime / time);

                _backGroundSlider.value = _lastBackValue;
                
                elaspedTime += Time.deltaTime;
                
                yield return wof2;
            }

            _backGroundSlider.value = endValue;
        }
    }
