using System;
using System.Collections;
using UnityEngine;
using SpecData;
using UnityEditor;

[RequireComponent(typeof(UnitController))]
public class BulletHellController : MonoBehaviour
{
    public enum BulletPatternType { Target, Circle, Spiral, Arc, Random, Homing }
    public enum BulletLoopType   { None, Incremental, Restart, Yoyo }

    [SerializeField] private UnitController _controller;
    [SerializeField] private int projectileId;
    [SerializeField] private BulletPatternType patternType = BulletPatternType.Target;
    [Serializable]
    public struct PatternStep
    {
        public BulletPatternType pattern;
        public float delay;
    }

    [SerializeField] private PatternStep[] patternSequence;
    [SerializeField] private BulletLoopType loopType       = BulletLoopType.None;
    [SerializeField] private int loopCount                  = 0;
    [SerializeField] private int bulletCount                = 1;
    [SerializeField] private float shootDelay;
    [SerializeField] private float interval                  = 1f;
    [SerializeField] private float spreadAngle               = 10f;
    [SerializeField] private float baseDamage                = 1f;
    [SerializeField] private eDamageType damageType         = eDamageType.Melee;
    [SerializeField] private eAttackType attackType         = eAttackType.Normal;
    [SerializeField] private float basePercent               = 1f;
    [SerializeField] private float homingSpeed               = 180f;

    [Header("Randomize Settings")]
    [SerializeField] private bool useRandomParameters = false;
    [SerializeField] private Vector2Int bulletCountRange = new Vector2Int(1, 1);
    [SerializeField] private Vector2 spreadAngleRange = Vector2.zero;
    [SerializeField] private Vector2 shootDelayRange = Vector2.zero;
    [SerializeField] private Vector2 intervalRange = Vector2.zero;

    private (int count, float shootDelay, float interval, float spread) GetParameters()
    {
        int c = bulletCount;
        float sd = shootDelay;
        float itv = interval;
        float sp = spreadAngle;
        if (useRandomParameters)
        {
            c += UnityEngine.Random.Range(bulletCountRange.x, bulletCountRange.y + 1);
            sd += UnityEngine.Random.Range(shootDelayRange.x, shootDelayRange.y);
            itv += UnityEngine.Random.Range(intervalRange.x, intervalRange.y);
            sp += UnityEngine.Random.Range(spreadAngleRange.x, spreadAngleRange.y);
        }
        return (c, sd, itv, sp);
    }

    public IEnumerator Fire()
    {
        if (projectileId > 0)
        {
            var specHitInstance = SpecHitInstance.GetDictionary()[projectileId];

            if (patternSequence != null && patternSequence.Length > 0)
            {
                yield return StartCoroutine(FireSequence(specHitInstance));
            }
            else
            {
                yield return StartCoroutine(ExecutePattern(specHitInstance, patternType));
            }
        }
    }

    private IEnumerator FireSequence(SpecHitInstance specHitInstance)
    {
        foreach (var step in patternSequence)
        {
            yield return ExecutePattern(specHitInstance, step.pattern);
            if (step.delay > 0f)
                yield return new WaitForSeconds(step.delay);
        }
    }

    private IEnumerator ExecutePattern(SpecHitInstance specHitInstance, BulletPatternType type)
    {
        var param = GetParameters();
        
        switch (type)
        {
            case BulletPatternType.Target:
                yield return FireToTarget2D(specHitInstance, param);
                break;
            case BulletPatternType.Circle:
                yield return FireCircle2D(specHitInstance, param);
                break;
            case BulletPatternType.Spiral:
                yield return FireSpiral2D(specHitInstance, param);
                break;
            case BulletPatternType.Arc:
                yield return FireArc2D(specHitInstance, param);
                break;
            case BulletPatternType.Random:
                yield return FireRandom2D(specHitInstance, param);
                break;
            case BulletPatternType.Homing:
                yield return FireHoming2D(specHitInstance, param);
                break;
        }
    }

    private IEnumerator FireToTarget2D(SpecHitInstance specHitInstance, (int count, float shootDelay, float interval, float spread) param)
    {
        Vector2 baseDir = Vector2.up;
        
        WaitForSeconds wos = new WaitForSeconds(param.shootDelay);

        for (int i = 0; i < param.count; i++)
        {
            param = GetParameters();
            
            if (_controller.Target != null)
            {
                Vector3 diff = _controller.Target.transform.position - transform.position;
                baseDir = new Vector2(diff.y, -diff.x).normalized;
            }
            
            SpawnProjectile2D(specHitInstance, baseDir);

            yield return (param.shootDelay > 0f) ? wos : null;
        }
    }

    private IEnumerator FireCircle2D(SpecHitInstance specHitInstance, (int count, float shootDelay, float interval, float spread) param)
    {
        WaitForSeconds shootWos = new WaitForSeconds(param.shootDelay);
        WaitForSeconds loopWos  = new WaitForSeconds(param.interval);
        
        float angleStep = param.spread;

        for (int loop = 0; loop < loopCount + 1; loop++)
        {
            param = GetParameters();
            
            float offset   = 0f;
            bool  reverse  = false;

            switch (loopType)
            {
                case BulletLoopType.Incremental:
                    offset = angleStep * param.count * loop;
                    break;
                case BulletLoopType.Restart:
                    offset = 0f;
                    break;
                case BulletLoopType.Yoyo:
                    offset  = 0f;
                    reverse = (loop % 2 == 1);
                    break;
            }


            for (int i = 0; i < param.count; i++)
            {
                int   idx   = reverse ? (param.count - 1 - i) : i;
                float angle = angleStep * idx + offset;
                Vector3 rot = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
                Vector2 dir = new Vector2(rot.x, rot.y).normalized;

                SpawnProjectile2D(specHitInstance, dir);
                yield return (param.shootDelay > 0f) ? shootWos : null;
            }


            if (param.interval > 0f && loop < loopCount)
                yield return loopWos;
        }
    }

    private IEnumerator FireSpiral2D(SpecHitInstance specHitInstance, (int count, float shootDelay, float interval, float spread) param)
    {
        WaitForSeconds wos = new WaitForSeconds(param.shootDelay);
        float angle = 0f;
        int total = param.count * (loopCount + 1);
        
        for (int i = 0; i < total; i++)
        {
            Vector3 rot = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
            Vector2 dir = new Vector2(rot.x, rot.y).normalized;
            SpawnProjectile2D(specHitInstance, dir);
            angle += param.spread;
            yield return (param.shootDelay > 0f) ? wos : null;
        }
    }

    private IEnumerator FireArc2D(SpecHitInstance specHitInstance, (int count, float shootDelay, float interval, float spread) param)
    {
        WaitForSeconds shootWos = new WaitForSeconds(param.shootDelay);
        WaitForSeconds loopWos = new WaitForSeconds(param.interval);
        Vector2 baseDir = Vector2.up;
        if (_controller.Target != null)
        {
            Vector3 diff = _controller.Target.transform.position - transform.position;
            baseDir = new Vector2(diff.y, -diff.x).normalized;
        }
        float angleStep = (param.count > 1) ? param.spread / (param.count - 1) : 0f;
        float startAngle = -param.spread * 0.5f;
        for (int loop = 0; loop < loopCount + 1; loop++)
        {
            param = GetParameters();
            
            for (int i = 0; i < param.count; i++)
            {
                float angle = startAngle + angleStep * i;
                Vector3 rot = Quaternion.Euler(0f, 0f, angle) * new Vector3(baseDir.x, baseDir.y, 0f);
                Vector2 dir = new Vector2(rot.x, rot.y).normalized;
                SpawnProjectile2D(specHitInstance, dir);
                yield return (param.shootDelay > 0f) ? shootWos : null;
            }
            if (param.interval > 0f && loop < loopCount)
                yield return loopWos;
        }
    }

    private IEnumerator FireRandom2D(SpecHitInstance specHitInstance, (int count, float shootDelay, float interval, float spread) param)
    {
        WaitForSeconds shootWos = new WaitForSeconds(param.shootDelay);
        WaitForSeconds loopWos = new WaitForSeconds(param.interval);
        Vector2 baseDir = Vector2.up;
        if (_controller.Target != null)
        {
            Vector3 diff = _controller.Target.transform.position - transform.position;
            baseDir = new Vector2(diff.y, -diff.x).normalized;
        }
        for (int loop = 0; loop < loopCount + 1; loop++)
        {
            param = GetParameters();
            
            for (int i = 0; i < param.count; i++)
            {
                float angle = UnityEngine.Random.Range(-param.spread * 0.5f, param.spread * 0.5f);
                Vector3 rot = Quaternion.Euler(0f, 0f, angle) * new Vector3(baseDir.x, baseDir.y, 0f);
                Vector2 dir = new Vector2(rot.x, rot.y).normalized;
                SpawnProjectile2D(specHitInstance, dir);
                yield return (param.shootDelay > 0f) ? shootWos : null;
            }
            if (param.interval > 0f && loop < loopCount)
                yield return loopWos;
        }
    }

    private IEnumerator FireHoming2D(SpecHitInstance specHitInstance, (int count, float shootDelay, float interval, float spread) param)
    {
        Vector2 baseDir = Vector2.up;
        for (int i = 0; i < param.count; i++)
        {
            if (_controller.Target != null)
            {
                Vector3 diff = _controller.Target.transform.position - transform.position;
                baseDir = new Vector2(diff.y, -diff.x).normalized;
            }
            WaitForSeconds wos = new WaitForSeconds(param.shootDelay);
            
            SpawnProjectile2D(specHitInstance, baseDir, true, _controller.Target?.transform);
            yield return (param.shootDelay > 0f) ? wos : null;
        }
    }


    // TODO: rewrite against HitLauncher.FireProjectile + IDamageSpec. Current body is
    // orphaned from the pre-merge Projectile API (legacy Initialize signature). Inputs
    // here are raw fields (baseDamage / damageType / etc.), not a SpecAttack/SpecSkill,
    // so either synthesize an IDamageSpec adapter or expose a low-level HitLauncher
    // overload that takes individual numbers.
    private void SpawnProjectile2D(SpecHitInstance specHitInstance, Vector2 dir, bool homing = false, Transform target = null)
    {
        var movingHit = Library.Handlers.Pool.Get<MovingHit>(specHitInstance.id);

        movingHit.transform.position = transform.position;

        // Legacy call — needs rewrite (see TODO above).
        // movingHit.Initialize(_controller, specHitInstance.id, dir, baseDamage, damageType, attackType, basePercent);

        if (homing)
        {
            var move = movingHit.GetComponent<StraightMovement>();
            move?.Setup(specHitInstance.moveSpeed, specHitInstance.lifeTime, target, homingSpeed);
        }
    }
}
