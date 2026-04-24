    using System;
    using System.Linq;
    using Cysharp.Text;
    using Library;
    using Sigtrap.Relays;
    using UnityEngine;
    // Semantic events: controllers emit UnitAction + direction, views decide Animator mapping.

    public class UnitController : PoolMonoBehaviour<UnitController>
    {
        protected internal override string AddressFormat => ZString.Format("UnitController_{0}", poolObjectId);
        
        [SerializeField] private UnitFsmHandler _unitFsmHandler;
        [SerializeField] private Collider2D _collider;
        
        private UnitData _unitData;

        public int Version { get; private set; }
        public UnitData Data => _unitData;
        public float MaxHp { get; private set; }
        public float CurrentHp { get; private set; }
        public LayerMask EnemyLayer { get; set;}
        public LayerMask MyLayer { get; set;}
        public bool IsAlive { get; private set; }
        
        public float AttackRange { get; set; }
        
        public DetectHandler DectectHandler;
        public UnitFsmHandler UnitFsmHandler => _unitFsmHandler;

        public UnitController Target;

        #region Action

        public Relay<DamageInfo, UnitController> OnTakeDamage { get; private set; } = new();
        public Relay<DamageInfo, UnitController> OnDealDamage { get; private set; } = new();
        public Relay<DamageInfo, UnitController> OnTakeHeal { get; private set; } = new();
        public Relay<DamageInfo, UnitController> OnHeal { get; private set; } = new();
        public Relay<bool> OnDeath { get; private set; } = new();
        public Relay<UnitAction> OnActionChanged { get; private set; } = new();
        public Relay<Vector2> OnMoveDirectionChanged { get; private set; } = new();
        public Relay<bool> OnFlip { get; private set; } = new();

        public void ClearAction()
        {
            OnTakeDamage.RemoveAll();
            OnDealDamage.RemoveAll();
            OnTakeHeal.RemoveAll();
            OnHeal.RemoveAll();
            OnDeath.RemoveAll();

            OnActionChanged.RemoveAll();
            OnMoveDirectionChanged.RemoveAll();
            OnFlip.RemoveAll();
        }

        #endregion

        public void Initialize(UnitData unitData, LayerMask myLayer, LayerMask enemyLayer)
        {
            Version++;
            _unitData = unitData;

            SetLayer(enemyLayer, myLayer);
            
            MaxHp = _unitData.Stats.GetStatValue(eStatType.Health);
            
            CurrentHp = MaxHp;

            ClearAction();

            MappingHelperManager.Instance.Unit.Register(_collider,this);
            
            gameObject.SetActive(true);

            SetCollider(true);
            
            _unitFsmHandler.Initialize(this);

            SetDetect();

            IsAlive = true;
        }

        public void StartGame()
        {
            SetDetect();
            
            _unitFsmHandler.Change(eStateType.Idle);
        }

        public void SetPosition(Vector3 pos)
        {
            transform.position = pos;
        }

        public float DealDamage(UnitController @to, float value, eDamageType damageType, eAttackType attackType, float percent = 1f)
        {
            if (to == null) return 0f;

            using (var ctx = DamageInfo.Get())
            {
                ctx.Attacker = this;
                ctx.AttackerVersion = Version;
                ctx.Target = to;
                ctx.TargetVersion = to.Version;
                ctx.BaseDamage = value;
                ctx.Percent = percent;
                ctx.DamageType = damageType;
                ctx.AttackType = attackType;

                DamagePipeline.Process(ctx);

                if (ctx.Canceled || ctx.IsDodged) return 0f;

                OnDealDamage.Dispatch(ctx, to);

                if (!to.IsAlive)
                {
                    _unitData.Effects.RaiseKill(this, to);
                }

                return ctx.Final;
            }
        }

        public void ApplyDamageToHealth(DamageInfo ctx)
        {
            if (!IsAlive) return;

            CurrentHp -= ctx.Final;

            OnTakeDamage.Dispatch(ctx, ctx.Attacker);

            if (CurrentHp <= 0)
            {
                Death();
            }
        }
        
        public void Heal(UnitController @to, float value, eDamageType damageType, eAttackType attackType, float percent = 1f)
        {
            using (var healInfo = DamageInfo.Get())
            {
                var healValue = value + _unitData.Stats.GetStatValue(eStatType.HealRating) * percent;
                var criticalType = eCriticalType.Normal;

                healInfo.Set(damageType, criticalType, attackType, healValue);
                
                _unitData.Effects.RaiseBeforeHeal(healInfo,this,@to);

                if (healInfo.Value < 0)
                {
                    return;
                }
                
                to.TakeHeal(healInfo, this);
                
                OnHeal.Dispatch(healInfo,to);
            }
        }

        public void TakeHeal(DamageInfo damageInfo, UnitController @from)
        {
            if (damageInfo.Value < 0)
            {
                return;
            }

            var damageValue = damageInfo.Value;
            
            _unitData.Effects.RaiseBeforeTakeHeal(damageInfo,from,this);

            CurrentHp += damageValue;
            
            OnTakeHeal.Dispatch(damageInfo,from);
        }

        public bool Death(bool isForce = false)
        {
            if (!IsAlive) return false;

            if (!isForce && CurrentHp > 0)
            {
                return false;
            }
            
            _unitFsmHandler.Change(eStateType.Death);

            SetCollider(false);
            
            OnDeath.Dispatch(isForce);

            IsAlive = false;
            
            if (isForce)
            {
                Release();
            }
            
            return true;
        }


        public void Release()
        {
            Release();
            
            MappingHelperManager.Instance.Unit.Unregister(_collider);
        }

        public void Move(Vector2 dir, float moveSpeed = -1)
        {
            if (!IsAlive) return;

            if (moveSpeed < 0)
            {
                moveSpeed = _unitData.Stats.GetStatValue(eStatType.MoveSpeed);
            }

            if (dir.sqrMagnitude > 0.01f)
            {
                var normalized = dir.normalized;
                transform.position += (Vector3)normalized * (moveSpeed * Time.deltaTime);

                OnFlip.Dispatch(normalized.x >= 0f);
                OnMoveDirectionChanged.Dispatch(normalized);
                OnActionChanged.Dispatch(UnitAction.Move);
            }
            else
            {
                OnActionChanged.Dispatch(UnitAction.Idle);
            }
        }
        
        private void SetDetect()
        {
            var userLayer = LayerMask.NameToLayer("User");

            if (EnemyLayer == userLayer)
            {
                return;
            }

            DectectHandler = new DetectHandler(this, EnemyLayer, 10f);
            DectectHandler.StartDetection(this);
        }

        private void SetLayer(LayerMask enemyLayer, LayerMask myLayer)
        {
            EnemyLayer = enemyLayer;
            MyLayer = myLayer;
            gameObject.layer = myLayer;
        }

        private void SetCollider(bool isOn)
        {
            if (isOn)
            {
                MappingHelperManager.Instance.Unit.Register(_collider,this);
            }
            else
            {
                MappingHelperManager.Instance.Unit.Unregister(_collider);
            }

            _collider.enabled = isOn;
        }

        private void Update()
        {

        }
    }
