using System.Collections;
using System.Collections.Generic;
using SpecData;
using UnityEngine;

public class CastPhase : PhaseBase
{
    [Header("Refs")]
    [SerializeField] private AimController   _aim;
    [SerializeField] private Player          _player;
    [SerializeField] private PlayerProxyUnit _attacker;

    [Header("Round")]
    [SerializeField] private int   _ballCount      = 8;
    [SerializeField] private float _launchInterval = 0.1f;
    [SerializeField] private int   _hitInstanceId  = 1;
    [SerializeField] private float _ballDamage     = 1f;

    [Header("Field")]
    [SerializeField] private Rect  _fieldBounds = new Rect(-4f, 0f, 8f, 12f);
    [SerializeField] private float _killLineY   = 0f;

    public override IEnumerator Execute()
    {
        if (_aim == null || _player == null || _attacker == null)
        {
            Debug.LogError("[CastPhase] missing refs (aim/player/attacker)");
            yield break;
        }

        Vector2 dir = Vector2.up;
        Vector3 origin = _player.transform.position;
        yield return _aim.WaitForFire(origin, d => dir = d);

        var spec = new PrototypeBallSpec(_hitInstanceId, _ballDamage);
        var session = new BallSession(_attacker, _player, spec, _fieldBounds, _killLineY, _launchInterval);
        yield return session.Run(origin, dir, _ballCount);
    }

    private sealed class PrototypeBallSpec : IDamageSpec
    {
        private static readonly List<int> _empty = new List<int>();

        public PrototypeBallSpec(int hitInstanceId, float baseDamage)
        {
            hitInstance = hitInstanceId;
            this.baseDamage = baseDamage;
        }

        public float       range        => 0f;
        public int         hitInstance  { get; }
        public eDamageType damageType   => default;
        public eAttackType attackType   => default;
        public float       baseDamage   { get; }
        public float       basePercent  => 1f;
        public List<int>   effects      => _empty;
    }
}
