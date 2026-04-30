using System.Collections;
using SpecData;
using UnityEngine;

public class BallSession
{
    private readonly PlayerProxyUnit _attacker;
    private readonly Player          _player;
    private readonly IDamageSpec     _damageSpec;
    private readonly Rect            _fieldBounds;
    private readonly float           _killLineY;
    private readonly float           _launchInterval;

    private int   _liveCount;
    private bool  _firstLanded;
    private float _anchorX;

    public BallSession(
        PlayerProxyUnit attacker,
        Player          player,
        IDamageSpec     damageSpec,
        Rect            fieldBounds,
        float           killLineY,
        float           launchInterval)
    {
        _attacker       = attacker;
        _player         = player;
        _damageSpec     = damageSpec;
        _fieldBounds    = fieldBounds;
        _killLineY      = killLineY;
        _launchInterval = launchInterval;
    }

    public IEnumerator Run(Vector3 origin, Vector2 direction, int ballCount)
    {
        var wait = new WaitForSeconds(_launchInterval);

        for (int i = 0; i < ballCount; i++)
        {
            var hit = HitLauncher.FireProjectile(_attacker, _damageSpec, origin, direction);
            if (hit == null) continue;

            if (hit.Movement is BounceMovement bounce)
            {
                bounce.AttachContext(_fieldBounds, _killLineY, this);
            }

            _liveCount++;
            hit.OnDespawn += OnHitDespawn;

            yield return wait;
        }

        while (_liveCount > 0) yield return null;

        _player.SetPosition(new Vector3(_anchorX, _killLineY, _player.transform.position.z));
    }

    internal void OnBallReachedKillLine(MovingHit hit, Vector2 hitPos)
    {
        if (!_firstLanded)
        {
            _firstLanded = true;
            _anchorX = hitPos.x;
            _player.SetPosition(new Vector3(_anchorX, _killLineY, _player.transform.position.z));
            hit.Despawn();
            return;
        }

        if (hit.Movement is BounceMovement bounce)
        {
            bounce.EnterAbsorbMode(new Vector2(_anchorX, _killLineY));
        }
        else
        {
            hit.Despawn();
        }
    }

    private void OnHitDespawn(IHitInstance _)
    {
        _liveCount--;
    }
}
