using System.Collections.Generic;
using Library;
using Sigtrap.Relays;
using UnityEngine;

public class MainCamera : SingletonBehaviour<MainCamera>
{
    [SerializeField] private Camera _camera;

    private Dictionary<eCameraPositionType, CameraMove> _cameraMoves = new();

    public static Camera Camera => Instance._camera;

    private Relay OnMoveStop = new Relay();

    protected override void Awake()
    {
        base.Awake();
        
        var moves = GetComponents<CameraMove>();

        foreach (var move in moves)
        {
            _cameraMoves.Add(move.type,move);

            OnMoveStop.AddListener(move.Stop);
        }
    }

    public void SetCamera(eCameraPositionType positionType)
    {
        if (_cameraMoves.TryGetValue(positionType, out var move))
        {
            Stop();
            
            move.Move();
        }
    }
    
    public void Stop()
    {
        OnMoveStop?.Dispatch();
    }
}

public enum eCameraPositionType
{
    PlayerFollow,
    Center,
}
