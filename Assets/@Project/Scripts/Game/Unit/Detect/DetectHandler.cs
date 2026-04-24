using System.Collections;
using UnityEngine;

public class DetectHandler
{
    UnitController _controller;
    LayerMask _mask;
    float _radius;
    float _interval;
    Coroutine _routine;

    private WaitForFixedUpdate wof = new WaitForFixedUpdate();

    public bool isTargetNull = true;

    public DetectHandler(UnitController controller, LayerMask mask, float radius)
    {
        _controller = controller;
        _mask = mask;
        _radius = radius;
    }

    public void StartDetection(MonoBehaviour host)
    {
        StopDetection(host);

        _routine = host.StartCoroutine(Detect());
    }

    public void StopDetection(MonoBehaviour host)
    {
        if (_routine != null)
        {
            host.StopCoroutine(_routine);
        }
        
        _controller.Target = null;
        isTargetNull = true;
    }

    IEnumerator Detect()
    {
        var wait = new WaitForSeconds(_interval);
        float r2 = _radius * _radius;

        while (true)
        {
            FindTarget();

            yield return wait;
        }
        
        void FindTarget()
        {
            UnitController nearest = null;
            float bestSqr = float.MaxValue;
            var pos = _controller.transform.position;
        }
    }
}