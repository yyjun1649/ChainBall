using System;
using System.Collections;
using UnityEngine;

public class AimController : MonoBehaviour
{
    [SerializeField] private LineRenderer _preview;
    [SerializeField] private float _previewLength = 12f;
    [SerializeField] private float _minPitchDeg   = 10f;

    private Camera _camera;

    private void Awake()
    {
        _camera = Camera.main;
    }

    public IEnumerator WaitForFire(Vector3 origin, Action<Vector2> onResult)
    {
        while (!Input.GetMouseButtonDown(0)) yield return null;

        if (_preview != null)
        {
            _preview.enabled = true;
            _preview.positionCount = 2;
        }

        Vector2 dir = Vector2.up;

        while (Input.GetMouseButton(0))
        {
            dir = ResolveDirection(origin);
            UpdatePreview(origin, dir);
            yield return null;
        }

        if (_preview != null) _preview.enabled = false;

        onResult?.Invoke(dir);
    }

    private Vector2 ResolveDirection(Vector3 origin)
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return Vector2.up;

        Vector3 mouse = _camera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 raw = (Vector2)(mouse - origin);
        if (raw.sqrMagnitude < 0.0001f) return Vector2.up;

        Vector2 dir = raw.normalized;

        // Clamp to upper hemisphere with minimum pitch.
        float minY = Mathf.Sin(_minPitchDeg * Mathf.Deg2Rad);
        if (dir.y < minY)
        {
            dir.y = minY;
            dir = dir.normalized;
        }
        return dir;
    }

    private void UpdatePreview(Vector3 origin, Vector2 dir)
    {
        if (_preview == null) return;
        _preview.SetPosition(0, origin);
        _preview.SetPosition(1, origin + (Vector3)(dir * _previewLength));
    }
}
