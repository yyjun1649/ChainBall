
    using System.Collections;
    using UnityEngine;

    public abstract class CameraMove : MonoBehaviour
    {
        public eCameraPositionType type;

        private Coroutine _coroutine;

        protected WaitForFixedUpdate wof = new WaitForFixedUpdate();
        
        public virtual void Move()
        {
            _coroutine = StartCoroutine(CoMove());
        }

        public void Stop()
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }
        }

        public abstract IEnumerator CoMove();
    }
