# 게임 유닛 / 전투 시스템 설계 가이드

## 0. 이 문서에 대하여

이 문서는 다수의 유닛과 다수의 투사체가 동시에 존재하는 액션/로그라이크 성향 게임에서, **유닛·능력·데미지·공격 인스턴스**를 어떻게 구성할지에 대한 설계 가이드다. Unity/C# 계열의 OOP 프로젝트를 전제로 하지만, 구조 자체는 엔진 중립적이다.

**다루는 것**
- 유닛의 Controller / Data / View 분리 원칙
- 스탯(Stat) 계산 레이어
- 효과(Effect) 시스템 (반응형/지속형)
- 데미지 파이프라인
- 근접·원거리·오라·레이저를 통합하는 HitInstance 시스템

**다루지 않는 것**
- DOTS / ECS 마이그레이션 (별도 판단 영역)
- 네트워크 동기화
- 저장/로드 직렬화
- AI 행동 트리 세부 (FSM 수준에서만 언급)

본문의 코드는 **시그니처와 흐름 중심의 의사 구현**이다. 복사해서 바로 컴파일되는 완성본은 아니고, 구조를 머릿속에 심기 위한 뼈대다.

---

## 1. 설계 원칙 (짧게)

1. **God Object 회피.** 유닛 베이스 클래스는 "조립자(composition root)"로만 얇게. 전투 로직은 각 모듈이 담당한다.
2. **상태와 행동을 분리.** 데이터 허브 클래스(`UnitData`)와 행동 퍼사드(`UnitController`)는 별개. 데이터는 순수 C# 객체여야 테스트가 쉽다.
3. **컴포지션 우선, 상속 최소.** 파생 클래스가 수십 개 쌓이는 구조(예: `Boss1`, `Boss2`, ..., `Boss99`)는 유지보수가 급속도로 불가능해진다.
4. **이벤트 기반 확장.** 새 기능은 기존 코드를 수정하는 게 아니라 새 파일을 하나 추가해서 이벤트에 구독하는 방식으로 붙인다(OCP).
5. **풀링은 기본값.** 유닛, 투사체, DamageContext, Modifier, Effect 인스턴스 등 자주 생성/소멸되는 모든 객체는 풀에서 꺼내 쓰고 돌려준다.
6. **조기 최적화를 경계.** DOTS/ECS는 프로파일러로 병목을 확인한 뒤에 선택. 기본은 OOP + Pool + 필요 시 Jobs/Burst + GPU Instancing으로 충분한 경우가 많다.
7. **리팩터링은 통증 기반으로.** 같은 버그 반복, 특정 파일을 여는 게 두려움, 테스트 불가능 — 이 중 하나 이상이 명확할 때만 손댄다. "맘에 안 든다"는 신호지만 그 자체가 리팩터링 사유가 되면 끝없이 뒤엎게 된다.

---

## 2. 전체 레이어 조감도

```
 ┌──────────────────────────────────────────────────────────┐
 │ L6. Ability / Augment / Item  (Effect 묶음의 출처 태그)   │
 │     - 로그라이크 업그레이드, 아이템, 증강 등이 여기에 위치 │
 └──────────────────────────┬───────────────────────────────┘
                            │ (Effect를 생성/제거)
                            ▼
 ┌──────────────────────────────────────────────────────────┐
 │ L3. Effect System  (IEffect + EffectHost)                │
 │     - Reactive / Status 효과                              │
 │     - 이벤트 훅 구독                                        │
 └─────┬────────────────────────────────────┬───────────────┘
       │ 읽기/쓰기                            │ 훅 구독
       ▼                                    ▼
 ┌──────────────────────┐    ┌──────────────────────────────┐
 │ L2. Stat System      │    │ L4. Damage Pipeline           │
 │     - StatContainer  │◄───┤     - DamageContext           │
 │     - Modifier       │    │     - 단계별 훅 (Before/After)│
 └──────────────────────┘    └──────────────┬───────────────┘
                                            │ (데미지 확정/적용)
                                            ▼
 ┌──────────────────────────────────────────────────────────┐
 │ L5. HitInstance System  (통합 공격 인스턴스)               │
 │     - InstantHit / MovingHit / AuraHit                    │
 │     - HitShape (Circle / Cone / Box / Line)               │
 │     - HitSnapshot (발사 시점 확정)                         │
 │     - IHitBehavior (관통 / 유도 / 분열 / 반사 / ...)       │
 └──────────────────────────┬───────────────────────────────┘
                            │ (발사/착탄)
                            ▼
 ┌──────────────────────────────────────────────────────────┐
 │ L1. Unit  (Controller + Data + View + FSM)                │
 │     - 공격자, 피격자 양쪽                                   │
 └──────────────────────────────────────────────────────────┘
```

**의존 방향 규칙**
- 위로 갈수록 "구체/가변", 아래로 갈수록 "기반/안정".
- 상위는 하위를 참조 가능. 하위가 상위를 직접 참조하지 않는다(필요하면 이벤트/콜백으로).
- Stat과 Damage Pipeline은 서로 읽고 쓸 수 있지만, 계산 방향은 항상 "Stat → Damage"로 단일.
- View는 Controller를 구독만 한다(단방향). Controller는 View의 존재 자체를 몰라도 동작해야 한다.

---

## 3. Unit 구조 — Controller / Data / View / FSM

### 3.1 역할 분담

| 구성요소 | 형태 | 책임 |
|---|---|---|
| `UnitController` | MonoBehaviour | 로직 퍼사드. 얇게. 풀의 진입점. 이벤트 발행. |
| `UnitData` | 순수 C# 객체 | 스탯·효과·아이템·증강 허브. 테스트 가능. |
| `UnitViewBase` | MonoBehaviour | 비주얼 전담. Controller 이벤트 구독자. 종류별(고블린/오우거 등)로 파생. |
| `UnitFsmHandler` | MonoBehaviour | 자식 오브젝트에 붙은 State들을 수집·전환. 에디터 조립. |

### 3.2 Controller 시그니처

```csharp
public class UnitController : MonoBehaviour {
    // 풀 재사용 감지용
    public int Version { get; private set; }

    // 데이터/자원
    public UnitData Data { get; private set; }
    public float CurrentHp { get; private set; }
    public bool IsAlive { get; private set; }
    public LayerMask MyLayer { get; private set; }
    public LayerMask EnemyLayer { get; private set; }

    // 외부 구독용 이벤트 (의미 기반)
    public event Action<UnitAction> OnActionChanged;
    public event Action<Vector2> OnMoveDirectionChanged;
    public event Action<DamageContext> OnDamageTaken;
    public event Action<DamageContext> OnDamageDealt;
    public event Action<bool> OnDeath;         // bool: isForce

    public void Initialize(UnitData data, LayerMask mine, LayerMask enemy) {
        Version++;                     // 재사용마다 +1
        Data = data;
        MyLayer = mine; EnemyLayer = enemy;
        CurrentHp = Data.Stats.Get(StatType.MaxHp);
        IsAlive = true;
    }

    public void TakeDamage(DamageContext ctx) { /* DamagePipeline 호출 */ }
    public bool Death(bool isForce = false) { /* OnDeath → Release */ return true; }
    public void Release() { /* 풀 반납 */ }
}
```

**원칙**
- Controller는 전투 로직을 **직접 계산하지 않는다.** 외부(DamagePipeline, HitInstance 등)가 요청하면 상태 변경 + 이벤트 발행만 한다.
- View 참조 없음. View는 Controller를 구독할 뿐.

### 3.3 Data 시그니처

```csharp
public class UnitData : IPoolable {
    public SpecCharacter Spec { get; private set; }
    public StatContainer<StatType> Stats { get; private set; }
    public EffectHost Effects { get; private set; }
    public UnitItems Items { get; private set; }
    public UnitAugments Augments { get; private set; }

    public void Initialize(SpecCharacter spec) { /* ... */ }
    public void Recalculate() { /* 베이스 + 모든 출처 Modifier 합산 */ }
    public void Reset() { /* 풀 반납 시 */ }
}
```

**왜 Data를 분리하나**
- 테스트: MonoBehaviour 없이 데미지 계산 단위 테스트 가능.
- 재사용: 서버 시뮬레이션, 헤드리스 빌드, 미리보기 UI 등에서 그대로 사용 가능.
- 역할 명확: "이 필드는 비주얼인가 로직인가" 고민을 줄인다.

### 3.4 View 분리의 조건

**분리하는 게 이득인 경우**
- 하나의 AI/로직(Controller)에 여러 비주얼 베리에이션이 붙는다. (예: "기본 적 AI"에 고블린/오우거/스켈레톤 모델)
- 비주얼이 독립 수명을 가진다 (죽음 애니 재생 중에도 로직 리소스는 회수).
- 스킨/진화/코스튬 시스템이 있다.

**분리하지 않는 게 나은 경우**
- 1:1 고정 (이 로직에는 이 비주얼만).
- 소규모 프로젝트에서 추상화 비용이 이득보다 큼.

> 규칙: **교체 가능성이 있으면 분리, 없으면 합친다.** 게임에서 MVC 패턴에 대한 강박은 오히려 독이 될 수 있다. 교체 가능성 없는 View를 억지로 분리하면 빈 메서드들만 남는 "분리 흉내"가 된다.

### 3.5 SetParent 없는 View 동기화 (핵심 패턴)

**문제**
- Unity에서 `Transform.SetParent`는 hierarchy dirty propagation을 트리거한다. 천 단위 유닛에서 매 프레임·매 스폰마다 호출되면 체감 성능에 영향을 준다.
- View를 Controller의 자식으로 두면, View가 Controller의 Release 타이밍을 자기가 결정해야 하는 역참조가 발생한다.

**해법**
- Controller와 View를 **형제(sibling) 관계**로 두고, View가 `LateUpdate`에서 Controller의 position을 복사한다.
- 초기 스폰 시 position만 맞춰주고, SetParent 호출은 하지 않는다.

```csharp
public abstract class UnitViewBase : MonoBehaviour {
    protected UnitController _controller;
    protected Transform _controllerTr;
    protected int _controllerVersion;
    protected Vector3 _offset;
    protected bool _followTarget;

    public void Initialize(UnitController c, Vector3 offset = default) {
        _controller = c;
        _controllerTr = c.transform;
        _controllerVersion = c.Version;
        _offset = offset;
        _followTarget = true;

        transform.position = _controllerTr.position + _offset;
        RegisterHandlers();
        gameObject.SetActive(true);
    }

    void LateUpdate() {
        if (!_followTarget) return;
        if (_controller == null || _controller.Version != _controllerVersion) {
            _followTarget = false;
            return;
        }
        transform.position = _controllerTr.position + _offset;
    }

    protected virtual void RegisterHandlers() {
        _controller.OnActionChanged += OnActionChanged;
        _controller.OnMoveDirectionChanged += OnMoveDirectionChanged;
        _controller.OnDeath += OnControllerDeath;
    }

    protected virtual void UnregisterHandlers() {
        _controller.OnActionChanged -= OnActionChanged;
        _controller.OnMoveDirectionChanged -= OnMoveDirectionChanged;
        _controller.OnDeath -= OnControllerDeath;
    }

    protected abstract void OnActionChanged(UnitAction action);
    protected abstract void OnMoveDirectionChanged(Vector2 dir);
    protected abstract void PlayDeathAnimation();
    protected abstract void ReleaseSelf();

    protected virtual void OnControllerDeath(bool force) {
        UnregisterHandlers();
        _followTarget = false;
        if (force) { ReleaseSelf(); return; }
        PlayDeathAnimation();  // 끝나면 애니 이벤트가 ReleaseSelf 호출
    }

    // 애니메이션 이벤트에서 호출
    public void OnDeathAnimationEvent() => ReleaseSelf();
}
```

**파생된 설계 이득**
- Controller와 View가 독립적으로 풀에 반납될 수 있다.
- View → Controller 역호출(특히 풀 Release 호출)이 완전히 사라진다.
- 사망 플로우가 선형적이다: `Controller.Death() → OnDeath 이벤트 → View.OnControllerDeath → _followTarget=false → 사망 애니 → ReleaseSelf()`.
- Controller는 View보다 먼저 Release 가능 → 로직 리소스 즉시 회수.

### 3.6 Version / Generation 패턴

풀에서 유닛이 재사용될 때, 기존 구독자(View, HPBar, 투사체 Snapshot 등)가 "내가 참조하던 그 유닛이 맞나"를 판단할 수 있어야 한다.

- Controller에 `int Version` 필드. `Initialize`마다 +1.
- 참조자는 발급 시점의 Version을 기억해두고, 접근 시 비교.
- 버전이 다르면 "그 유닛은 이미 사라지고 다른 유닛으로 재사용됐다"고 판단.

이게 없으면 "죽은 공격자가 풀에서 적으로 재사용 → 내 화살이 착탄하면서 적이 경험치 획득" 같은 미묘한 버그가 발생한다.

### 3.7 의미 기반 이벤트 vs 문자열 기반 이벤트

**안티패턴**
```csharp
// Controller가 View의 내부 파라미터 이름을 알고 있다
OnAnimationSetInt.Dispatch("MoveX", animX);
OnAnimationSetFloat.Dispatch("Speed", 0.5f);
```
→ 모든 View가 같은 Animator 파라미터 규약을 강제당한다. View 교체의 의미가 사라진다.

**지향**
```csharp
// Controller는 "의미"만 발행. View가 자기 애니에 매핑.
OnActionChanged?.Invoke(UnitAction.Move);
OnMoveDirectionChanged?.Invoke(direction);
```
```csharp
// 고블린 View
protected override void OnActionChanged(UnitAction a) {
    switch (a) {
        case UnitAction.Idle:   animator.SetBool("IsWalking", false); break;
        case UnitAction.Move:   animator.SetBool("IsWalking", true);  break;
        case UnitAction.Death:  animator.SetTrigger("Die");           break;
    }
}

// 오우거 View는 완전히 다른 Animator 구조를 써도 된다
protected override void OnActionChanged(UnitAction a) {
    switch (a) {
        case UnitAction.Idle:   animator.Play("Ogre_Idle");        break;
        case UnitAction.Move:   animator.Play("Ogre_Walk");        break;
        case UnitAction.Death:  animator.Play("Ogre_Death_Heavy"); break;
    }
}
```

### 3.8 FSM은 자식 오브젝트로 조립

```
UnitPrefab
 ├─ UnitController, Collider, ...
 ├─ StateMachine (빈 오브젝트)
 │   ├─ IdleState
 │   ├─ ChaseState
 │   └─ AttackState
 └─ (Abilities 컨테이너, 선택)
```

- `UnitFsmHandler.Initialize`에서 `GetComponentsInChildren<UnitStateBase>`로 자동 등록.
- State 추가/제거가 에디터에서 오브젝트 드래그만으로 가능.
- 유닛마다 다른 AI 조합을 프리팹 단위로 만들 수 있다.

---

## 4. Stat 시스템 (값 계산 레이어)

### 4.1 개념
- Stat은 순수한 "값 계산기"다. 같은 Modifier 집합 → 같은 결과가 나와야 한다.
- Reactive 로직, 이벤트 구독은 여기 있으면 안 된다. (그건 Effect 레이어의 일)

### 4.2 Modifier 종류

| 종류 | 의미 | 예 |
|---|---|---|
| `Flat` | 덧셈 | +10 공격력 |
| `PercentAdd` | 같은 그룹에서 합산 후 곱 | +30% (다른 +20%와 합산하여 총 +50%) |
| `PercentMul` | 독립적으로 곱 | ×1.5 (따로따로 곱셈) |

### 4.3 계산 규약 (권장)

```
final = (baseValue + Σ Flat) × (1 + Σ PercentAdd) × Π (1 + PercentMul)
```

- PercentAdd는 "버프의 합산"용 (예: 여러 증강이 더해져 공격력 +40%).
- PercentMul은 "독립 승수"용 (예: 크리티컬 배율, 속성 배율).
- 프로젝트마다 규약이 다르므로 한 번 정하면 문서화.

### 4.4 핵심 시그니처

```csharp
public enum ModifierType { Flat, PercentAdd, PercentMul }

public class StatModifier : IPoolable {
    public string Id { get; private set; }         // 중복 방지 키
    public object Source { get; private set; }     // 출처 (Augment, Item 등)
    public ModifierType Type { get; private set; }
    public float Value { get; set; }

    public void Set(string id, object src, ModifierType type, float value) { /* ... */ }
    public void Reset() { /* 풀 반납 */ }
}

public class Stat {
    public float BaseValue { get; set; }
    readonly List<StatModifier> _mods = new();
    float _cached;
    bool _dirty = true;

    public float Final {
        get { if (_dirty) Recalculate(); return _cached; }
    }

    public void Add(StatModifier m)    { _mods.Add(m); _dirty = true; }
    public void Remove(StatModifier m) { _mods.Remove(m); _dirty = true; }
    public void RemoveBySource(object src) {
        _mods.RemoveAll(m => m.Source == src);
        _dirty = true;
    }
    void Recalculate() { /* 위 공식 */ }
}

public class StatContainer<TKey> {
    readonly Dictionary<TKey, Stat> _stats = new();
    public float Get(TKey key) => _stats[key].Final;
    public void AddModifier(TKey key, StatModifier m) => _stats[key].Add(m);
    public void RemoveBySource(object src) {
        foreach (var s in _stats.Values) s.RemoveBySource(src);
    }
}
```

### 4.5 주의

- Modifier를 풀링하지 않으면 초당 수백 개 생성/GC로 성능 하락.
- `Source` 참조는 "한 증강이 주는 여러 스탯 모디파이어를 한 번에 제거"하는 데 필수.
- `Final` 접근 시 lazy 재계산 — 매 프레임 N번 호출돼도 값이 변하지 않았다면 cache 반환.

---

## 5. Effect 시스템 (반응/상태 레이어)

### 5.1 세 종류를 구분하라

| 이름 | 본질 | 어디에 |
|---|---|---|
| **Stat Modifier** | 값 계산 | §4 Stat 레이어 |
| **Reactive Effect** | 이벤트에 반응한 행동 | §5 Effect 레이어 |
| **Status Effect** | 지속 시간 + 중첩 규칙 | §5 Effect 레이어 (Duration 파생) |

"맞으면 순간이동", "공격 시 회복", "죽일 때 스택" 같은 건 Reactive. "5초간 빙결", "독 DoT 10초" 같은 건 Status. **이 셋을 한 서랍에 넣으면 God Object가 된다.**

### 5.2 IEffect 라이프사이클

```csharp
public interface IEffect {
    string Id { get; }                     // 중복 식별
    void OnAttach(UnitData unit);          // 이벤트 구독만
    void OnDetach(UnitData unit);          // 이벤트 해제만
}

public interface ITickEffect : IEffect {
    void OnTick(float deltaTime);          // 매 프레임 필요한 경우
}

public interface IDurationEffect : IEffect {
    float RemainingTime { get; }
    bool IsExpired { get; }                // EffectHost가 자동 제거
}

public interface IStackableEffect : IEffect {
    int Stacks { get; }
    int MaxStacks { get; }
    void OnStack();                        // 중복 Add 시
}
```

**애매한 `Apply/Remove/ClearMemory` 같은 메서드를 두지 말 것.** 구현자마다 "Apply가 뭐지?"를 다르게 해석하면 시스템이 무너진다. `OnAttach/OnDetach` 두 개로 계약을 명확히 고정.

### 5.3 EffectHost — 유닛 안의 효과 컨테이너

```csharp
public class EffectHost {
    UnitData _owner;
    readonly List<IEffect> _active = new();

    // 이벤트 훅 (파이프라인 단계에 대응)
    public event Action<DamageContext> OnBeforeDealDamage;
    public event Action<DamageContext> OnAfterDealDamage;
    public event Action<DamageContext> OnBeforeTakeDamage;
    public event Action<DamageContext> OnAfterTakeDamage;
    public event Action<HitSnapshot>   OnFireHit;
    public event Action<UnitController, UnitController> OnKill;
    public event Action OnDeath;

    public void Initialize(UnitData owner) { _owner = owner; }

    public void Add(IEffect effect) {
        var existing = _active.Find(e => e.Id == effect.Id);
        if (existing != null) {
            if (existing is IStackableEffect s) s.OnStack();
            return;  // 중복 Attach 방지
        }
        effect.OnAttach(_owner);
        _active.Add(effect);
    }

    public void Remove(IEffect effect) {
        if (!_active.Remove(effect)) return;
        effect.OnDetach(_owner);
    }

    public void RemoveBySource(object source) {
        for (int i = _active.Count - 1; i >= 0; i--) {
            if (_active[i] is ISourcedEffect se && se.Source == source) {
                Remove(_active[i]);
            }
        }
    }

    public void Tick(float dt) {
        for (int i = _active.Count - 1; i >= 0; i--) {
            var e = _active[i];
            if (e is ITickEffect t)     t.OnTick(dt);
            if (e is IDurationEffect d && d.IsExpired) Remove(e);
        }
    }
}
```

### 5.4 Ability / Augment / Item는 "Effect의 출처"

로그라이크 업그레이드, 아이템, 증강은 각자 전용 로직 클래스가 아니라 **Effect 묶음을 발행하는 출처 태그**로 설계한다.

```csharp
public class Augment {
    public SpecAugment Spec { get; }
    readonly List<IEffect> _issued = new();

    public void Apply(UnitData unit) {
        foreach (var effectSpec in Spec.Effects) {
            var e = EffectFactory.Create(effectSpec, source: this);
            _issued.Add(e);
            unit.Effects.Add(e);
        }
    }

    public void Revoke(UnitData unit) {
        foreach (var e in _issued) unit.Effects.Remove(e);
        _issued.Clear();
    }
}
```

**새 능력 추가 = 새 `IEffect` 구현 클래스 1개 + Factory 등록 1줄.** 기존 유닛/컨트롤러 코드는 건드리지 않는다.

### 5.5 샘플: 반응형 "맞으면 순간이동"

```csharp
public class TeleportOnHitEffect : IEffect {
    readonly float _distance;
    readonly float _cooldown;
    float _nextAvailableTime;
    UnitData _unit;
    UnitController _unitCtrl;

    public string Id => $"Teleport_{_distance}";

    public TeleportOnHitEffect(float distance, float cooldown) {
        _distance = distance; _cooldown = cooldown;
    }

    public void OnAttach(UnitData unit) {
        _unit = unit;
        _unitCtrl = unit.Controller;
        unit.Effects.OnBeforeTakeDamage += OnBeforeTakeDamage;
    }

    public void OnDetach(UnitData unit) {
        unit.Effects.OnBeforeTakeDamage -= OnBeforeTakeDamage;
    }

    void OnBeforeTakeDamage(DamageContext ctx) {
        if (Time.time < _nextAvailableTime) return;
        if (ctx.Attacker == null) return;
        var away = (_unitCtrl.transform.position - ctx.Attacker.transform.position).normalized;
        _unitCtrl.transform.position += away * _distance;
        _nextAvailableTime = Time.time + _cooldown;
        // ctx.Canceled = true;  // 완전 회피로 만들고 싶으면
    }
}
```

### 5.6 샘플: 상태형 "빙결" (Stat + Status의 조합)

```csharp
public class FrozenEffect : IDurationEffect, ITickEffect {
    public string Id => "Frozen";
    public float RemainingTime { get; private set; }
    public bool IsExpired => RemainingTime <= 0f;

    UnitData _unit;
    StatModifier _speedMod;

    public FrozenEffect(float duration) { RemainingTime = duration; }

    public void OnAttach(UnitData unit) {
        _unit = unit;
        _speedMod = StatModifierPool.Get("Frozen", this, ModifierType.PercentAdd, -1f);
        unit.Stats.AddModifier(StatType.MoveSpeed, _speedMod);
        unit.Stats.AddModifier(StatType.AttackSpeed, _speedMod);
    }

    public void OnDetach(UnitData unit) {
        unit.Stats.RemoveBySource(this);
        _speedMod.Reset();  // 풀 반납
    }

    public void OnTick(float dt) { RemainingTime -= dt; }
}
```

**관찰**: Status Effect가 내부적으로 Stat Modifier를 조립한다. 빙결 자체는 "값 계산"이 아니지만 "값 계산"을 도구로 쓴다. 레이어가 분리됐기 때문에 자연스럽게 협업한다.

---

## 6. Damage Pipeline (데미지 파이프라인)

### 6.1 안티패턴 직시

```csharp
// 한 메서드에 모든 것이 뒤섞인 경우
public void DealDamage(UnitController target, float value, ...) {
    float dmg = value + Stats.Get(MeleeDamage);
    if (damageType == Melee && attackType == Normal) {
        if (Random(CritChance)) dmg *= (1 + CritMul);   // 근접 Normal만 크리?
    }
    _effects.TriggerDealDamage(info, this, target);
    OnDealDamage.Dispatch(info, target);
    DamageText.Show(info, target.transform.position);
    if (target.TakeDamage(info, this)) { /* kill */ }
}
```

문제:
- 크리티컬 조건이 특정 공격 타입에만 하드코딩 → 원거리 크리 빌드 불가.
- 효과 훅/이벤트 발행/UI/실제 HP 차감이 한 메서드에.
- 원거리 공격자의 "발사 시점 버프"가 착탄 시점에 풀리면 반영 안 됨.

### 6.2 DamageContext

파이프라인을 따라 흐르며 단계별로 수정되는 가변 컨테이너. 풀링된다.

```csharp
public class DamageContext : IPoolable {
    // 참조 (재사용 감지용 Version 포함)
    public UnitController Attacker;
    public int AttackerVersion;
    public UnitController Target;
    public int TargetVersion;

    // 수치
    public float BaseDamage;         // 스탯 반영 전/후 베이스
    public float PreMitigation;      // 방어 적용 전
    public float Final;              // 최종

    // 분류
    public DamageType DamageType;
    public AttackType AttackType;
    public ElementType Element;

    // 판정 결과
    public bool IsCritical;
    public float CritMultiplier;
    public bool IsDodged;
    public bool IsBlocked;
    public bool Canceled;            // 완전 취소 (순간이동 회피 등)

    // 진단/로그
    public readonly List<string> AppliedEffects = new();

    public void Reset() { /* ... */ }
}
```

### 6.3 단계 정의

```csharp
public static class DamagePipeline {
    public static void Process(DamageContext ctx) {
        // 1. 기본 수치 결정
        Stage_CalculateBase(ctx);

        // 2. 공격자 측 훅 (속성 부여, 확정 크리, 추가 배율 등)
        ctx.Attacker?.Data.Effects.RaiseBeforeDealDamage(ctx);

        if (ctx.Canceled) return;

        // 3. 회피 판정
        Stage_RollDodge(ctx);
        if (ctx.IsDodged) { Finalize(ctx); return; }

        // 4. 크리티컬 판정
        Stage_RollCritical(ctx);

        // 5. 방어/저항 적용
        Stage_ApplyResistance(ctx);

        // 6. 피격자 측 훅 (실드, 반사, 데미지 수정)
        ctx.Target.Data.Effects.RaiseBeforeTakeDamage(ctx);

        if (ctx.Canceled) { Finalize(ctx); return; }

        // 7. 실제 HP 차감
        Stage_ApplyToHealth(ctx);

        // 8. 사후 훅 (흡혈, 가시, 킬 연쇄 등)
        ctx.Attacker?.Data.Effects.RaiseAfterDealDamage(ctx);
        ctx.Target.Data.Effects.RaiseAfterTakeDamage(ctx);

        Finalize(ctx);
    }

    static void Finalize(DamageContext ctx) {
        // 연출: DamageText, 카메라 쉐이크 등 이벤트 발행
        // ctx.Dispose() 호출 지점은 상위 레이어(HitInstance)가 결정
    }
}
```

**장점**
- 새 효과(흡혈/실드/반사)는 "해당 훅에 구독하는 `IEffect` 1개"로 끝.
- 공격 타입 무관하게 크리/회피 공식이 한 자리에.
- 순서가 명시적 → "왜 방어 무시가 안 먹지?" 같은 버그가 드러난다.

### 6.4 근접 vs 원거리 재사용

- **근접**: 공격자 → `DamageContext` 즉시 생성 → `DamagePipeline.Process(ctx)` 호출.
- **원거리**: 공격자 → **HitSnapshot**으로 발사 시점 확정 → 투사체가 날아감 → 착탄 시 Snapshot으로 `DamageContext` 구성 → `DamagePipeline.Process(ctx)`.

**같은 파이프라인을 두 경로가 공유한다.** 근접/원거리가 데미지 규칙 일치.

---

## 7. HitInstance 통합 시스템

### 7.1 개념

"공격"은 근접과 원거리가 본질적으로 다르지 않다. 모두:

> **어떤 공간 × 시간 범위 안에서 조건에 맞는 대상에게 데미지 이벤트를 발생시키는 행위**

차이는 "공간/시간 범위의 패턴"뿐이다.

| 공격 | 공간 | 시간 |
|---|---|---|
| 근접 스윙 | 내 전방 부채꼴 | 0.1~0.2초 |
| 근접 찌르기 | 앞 직선 | 순간 |
| 레이저 | 직선 | 순간~지속 |
| 투사체 | 움직이는 원 | 수명 동안 |
| 오라 | 내 주변 원 | 지속 (주기적 판정) |
| 체인 번개 | 타겟에서 타겟으로 | 순차 |
| 운석 낙하 | 착지점 원 | 예고 후 순간 |

→ 이걸 **`HitInstance`** 라는 공통 추상으로 통합한다.

### 7.2 4축 조합 모델

| 축 | 의미 | 예 |
|---|---|---|
| **HitInstance 종류** | 수명/판정 패턴 | `InstantHit`, `MovingHit`, `AuraHit` |
| **HitShape** | 판정 모양 | `Circle`, `Cone`, `Box`, `Line` |
| **HitSnapshot** | 발사 시점 확정 데이터 | Damage, CritMul, Penetrate 등 |
| **IHitBehavior** | 런타임 조합 가능한 행동 | Penetrate, Homing, Split, Bounce, ... |

### 7.3 HitInstance 베이스

```csharp
public abstract class HitInstance : MonoBehaviour {
    public UnitController Attacker { get; protected set; }
    public HitSnapshot Snapshot { get; protected set; }
    public HitShape Shape { get; set; }
    public float Age { get; protected set; }
    public bool IsAlive { get; protected set; }

    public event Action<HitInstance, UnitController> OnHit;
    public event Action<HitInstance>                 OnDespawn;
    public event Action<HitInstance, float>          OnTick;

    readonly List<IHitBehavior> _behaviors = new();

    public virtual void Initialize(UnitController attacker, HitSnapshot snap) {
        Attacker = attacker; Snapshot = snap;
        Age = 0; IsAlive = true;
        OnSpawn();
    }

    public void AddBehavior(IHitBehavior b) {
        _behaviors.Add(b);
        if (IsAlive) b.OnAttach(this);
    }

    protected abstract void OnSpawn();
    protected abstract void Tick(float dt);

    void Update() {
        if (!IsAlive) return;
        float dt = Time.deltaTime;
        Age += dt;
        Tick(dt);
        OnTick?.Invoke(this, dt);
    }

    protected void RaiseHit(UnitController target) {
        OnHit?.Invoke(this, target);
        if (Snapshot != null && !TargetIsValid(target)) return;
        var ctx = BuildDamageContext(target);
        DamagePipeline.Process(ctx);
        ctx.Dispose();
    }

    public void Despawn() {
        if (!IsAlive) return;
        IsAlive = false;
        OnDespawn?.Invoke(this);
        foreach (var b in _behaviors) b.OnDetach(this);
        _behaviors.Clear();
        OnHit = null; OnDespawn = null; OnTick = null;
        ReleaseToPool();
    }

    protected abstract void ReleaseToPool();
}
```

### 7.4 종류별 하위 클래스

```csharp
// 순간 판정 (근접/레이저/운석 착지 등)
public class InstantHit : HitInstance {
    public float Duration { get; set; }     // 0이면 1프레임
    protected override void OnSpawn() {
        DoQuery();
        if (Duration <= 0) Despawn();
    }
    protected override void Tick(float dt) {
        if (Duration > 0) DoQuery();        // 지속 판정
        if (Age >= Duration) Despawn();
    }
    void DoQuery() {
        var targets = Shape.Query(transform.position, Attacker.EnemyLayer);
        foreach (var t in targets) RaiseHit(t);
    }
}

// 이동 판정 (투사체)
public class MovingHit : HitInstance {
    public Vector3 Velocity { get; set; }
    protected override void OnSpawn() { /* collider setup */ }
    protected override void Tick(float dt) {
        transform.position += Velocity * dt;
        if (Age >= Snapshot.LifeTime) Despawn();
    }
    void OnTriggerEnter2D(Collider2D other) {
        if (TryGetTarget(other, out var t)) RaiseHit(t);
    }
}

// 지속 영역 (오라/장판)
public class AuraHit : HitInstance {
    public float TickInterval { get; set; }
    public Transform FollowTarget { get; set; }
    float _tickTimer;
    protected override void OnSpawn() { _tickTimer = 0; }
    protected override void Tick(float dt) {
        if (FollowTarget != null) transform.position = FollowTarget.position;
        _tickTimer += dt;
        if (_tickTimer >= TickInterval) {
            _tickTimer = 0;
            foreach (var t in Shape.Query(transform.position, Attacker.EnemyLayer))
                RaiseHit(t);
        }
        if (Age >= Snapshot.LifeTime) Despawn();
    }
}
```

### 7.5 HitShape

```csharp
public abstract class HitShape {
    public abstract List<UnitController> Query(Vector3 origin, LayerMask layer);
}

public class CircleShape : HitShape {
    public float Radius;
    public override List<UnitController> Query(Vector3 origin, LayerMask layer) {
        // Physics2D.OverlapCircleAll → UnitController 추출
        return default;
    }
}

public class ConeShape : HitShape {
    public float Radius;
    public float AngleDegrees;
    public Vector3 Direction;
    public override List<UnitController> Query(Vector3 origin, LayerMask layer) {
        // 원 판정 → 각도 필터
        return default;
    }
}
// BoxShape, LineShape도 동일 패턴
```

### 7.6 HitSnapshot

```csharp
public class HitSnapshot : IPoolable {
    public UnitController Attacker;
    public int AttackerVersion;

    // 발사 시점 확정 수치
    public float Damage;
    public float CritChance;
    public float CritMultiplier;
    public bool  IsCritical;           // 발사 시 확정되면 여기 찍음
    public DamageType DamageType;
    public AttackType AttackType;
    public ElementType Element;

    // 거동
    public float Speed;
    public float LifeTime;
    public int   Penetrate;
    public int   Split;
    public int   Bounce;
    public float Knockback;

    // 타겟/연쇄
    public UnitController Target;
    public Vector3 Origin;
    public Vector3 Direction;

    // 확장 (특정 스킬만 쓰는 데이터)
    public readonly Dictionary<string, object> Extra = new();

    public HitSnapshot Clone() { /* 자식 투사체용 */ return default; }
    public void Reset() { /* 풀 반납 */ }
}
```

**왜 스냅샷인가**
- 발사 시점의 공격자 상태를 **확정**시켜 투사체에 동행시킨다.
- 공격자가 죽거나 풀에서 재사용되어도 데미지/크리가 정확.
- 착탄 시점에 공격자를 재조회하는 방식은 "버프 풀림 버그", "공격자 재사용 버그"를 낳는다.

### 7.7 발사 런처 (AttackModule / Skill 통합 진입점)

```csharp
public static class HitLauncher {
    public static HitInstance Launch(
        UnitController attacker,
        IDamageSpec damageSpec,           // SpecAttack 또는 SpecSkill 공통 인터페이스
        HitSpawnConfig spawnConfig,       // InstantHit/MovingHit/AuraHit, Shape, 수명 등
        Vector3 origin,
        Vector3 direction,
        UnitController target = null
    ) {
        var snap = HitSnapshotBuilder.Build(attacker, damageSpec, spawnConfig,
                                            origin, direction, target);

        // Effect 훅: 공격자의 "발사 시" 효과들이 Snapshot 수정 (화염속성 부여 등)
        attacker.Data.Effects.RaiseOnFireHit(snap);

        // 크리 판정 (발사 시 확정)
        snap.IsCritical = ShouldCrit(snap);
        if (snap.IsCritical) snap.Damage *= snap.CritMultiplier;

        var hit = HitSpawner.Spawn(spawnConfig);
        hit.Shape = ShapeFactory.Create(spawnConfig);
        hit.Initialize(attacker, snap);

        foreach (var bCfg in spawnConfig.Behaviors)
            hit.AddBehavior(HitBehaviorFactory.Create(bCfg));

        return hit;
    }
}
```

AttackModule/Skill은 Launch를 **한 줄 호출**로 끝난다. 차이는 `spawnConfig`(데이터)에만.

### 7.8 공격 종류별 매핑 표

| 공격 | HitInstance | Shape | 특징 Behavior |
|---|---|---|---|
| 근접 스윙 | InstantHit (Duration≈0.1s) | Cone | Penetrate |
| 근접 찌르기 | InstantHit | Line (짧은) | - |
| 레이저 | InstantHit (Duration>0) | Line (긴) | - |
| 투사체 | MovingHit | Circle | Penetrate, Homing, Split, Bounce |
| 오라 | AuraHit | Circle | - |
| 궤도 무기 | MovingHit | Circle | Orbit |
| 체인 번개 | InstantHit 연쇄 | Line | Chain |
| 운석 낙하 | 예고 후 InstantHit | Circle | ExplodeOnDespawn |
| 파이어볼 | MovingHit → InstantHit | Circle → Circle(큰) | ExplodeOnDespawn |

### 7.9 IHitBehavior

```csharp
public interface IHitBehavior {
    int Priority { get; }              // 낮을수록 먼저 처리 (Velocity 변조 등)
    void OnAttach(HitInstance hit);
    void OnDetach(HitInstance hit);
}
```

샘플 Behavior 카탈로그:
- `PenetrateBehavior` — 관통 카운트 소진 시까지 계속 비행
- `HomingBehavior` — 매 프레임 Velocity를 타겟 방향으로 회전
- `SpinBehavior` — transform.Rotate
- `OrbitBehavior` — 중심축 기준 회전 궤도 (위치 덮어쓰기)
- `FallingBehavior` — 중력 낙하 (위치 덮어쓰기)
- `BounceBehavior` — 벽 충돌 시 Vector3.Reflect
- `SplitOnDespawnBehavior` — 소멸 시 자식 HitInstance N개 스폰
- `ExplodeOnDespawnBehavior` — 소멸 시 InstantHit(AoE) 스폰
- `ChainBehavior` — Hit 시 근처 다른 대상으로 새 HitInstance 연쇄

**Penetrate 샘플**
```csharp
public class PenetrateBehavior : IHitBehavior {
    public int Priority => 10;
    int _left;
    public PenetrateBehavior(int count) { _left = count; }

    public void OnAttach(HitInstance h) { h.OnHit += OnHit; }
    public void OnDetach(HitInstance h) { h.OnHit -= OnHit; }

    void OnHit(HitInstance h, UnitController target) {
        if (_left > 0) _left--;
        else h.Despawn();
    }
}
```

### 7.10 Behavior 간 충돌 규칙

- **위치를 덮어쓰는 Behavior**(Orbit, Falling)는 Velocity 기반 Behavior(Homing)와 동시에 쓰지 않는다. 데이터 스펙에서 금지 조합으로 선언하거나, Priority로 후자가 덮어쓰도록 명시.
- **명시적 금지 > 암묵적 공존**. 프로젝트 초기에 "이 조합은 안 됨" 리스트를 관리.

### 7.11 성능 가이드

- 자주 쓰는 Behavior(관통/기본 이동/유도)는 `HitSnapshot`의 필드나 비트플래그로 처리하고, 드문 Behavior만 인터페이스로. 인기 경로의 hot-loop 할당을 줄인다.
- HitInstance, HitShape, HitBehavior, HitSnapshot, DamageContext 모두 **풀링 필수**.
- `Update()`가 호출되는 인스턴스가 수백~천 단위면, 개별 MonoBehaviour `Update` 대신 중앙 관리자(`HitInstanceManager`)가 `List<HitInstance>`를 tick하는 패턴이 체감 성능 이득.

### 7.12 소환수는 HitInstance가 아니다

- 소환수는 독립 수명 + AI + HP를 가진 **유닛**이다.
- 소환 행위는 "유닛 스폰"이지 "히트박스 발생"이 아니다.
- `Skill.Use` 안에서 `UnitSpawnHandler.Spawn(...)`을 호출하는 형태로 다룬다.
- 소환수가 공격할 때는 그 소환수의 AttackModule이 같은 `HitLauncher.Launch`를 호출 → 결국 같은 시스템으로 귀결.

---

## 8. End-to-End 시나리오

**상황**: "관통+유도 화살을 쏘는 플레이어가 빙결된 적을 맞힌다."

```
[플레이어 측]
1. AttackModule.TryAttack(target)
2. HitLauncher.Launch(attacker=player, damageSpec, spawnConfig, origin, dir, target)
   ├─ HitSnapshotBuilder.Build
   │   ├─ attacker.Stats에서 Damage, CritChance 반영
   │   └─ attacker.Effects.RaiseOnFireHit(snap)
   │       └─ 속성부여 Effect가 snap.Element = Fire 설정
   ├─ ShouldCrit(snap) → snap.IsCritical = true, snap.Damage ×= CritMul
   ├─ MovingHit 인스턴스 풀에서 꺼냄
   ├─ HitShape = CircleShape(radius=0.3)
   ├─ hit.Initialize(player, snap)
   └─ PenetrateBehavior, HomingBehavior 어태치

[비행 단계]
3. MovingHit.Update 매 프레임
   ├─ HomingBehavior.OnTick: Velocity를 target 방향으로 회전
   └─ transform.position += Velocity * dt

[착탄 단계]
4. OnTriggerEnter2D → RaiseHit(enemy)
5. hit.OnHit 이벤트 → PenetrateBehavior: _left-- 체크
6. DamageContext 구성 (snap에서 복사)
7. DamagePipeline.Process(ctx)
   ├─ Stage_CalculateBase
   ├─ player.Effects.RaiseBeforeDealDamage
   ├─ Stage_RollDodge (실패)
   ├─ Stage_RollCritical (이미 snap에서 확정)
   ├─ Stage_ApplyResistance (방어/저항)
   ├─ enemy.Effects.RaiseBeforeTakeDamage
   │   └─ "빙결 상태면 데미지 +50%" Effect가 ctx.Final 수정
   ├─ Stage_ApplyToHealth → enemy.CurrentHp -= ctx.Final
   └─ After 훅 (흡혈/가시/킬 체크)

[후처리]
8. PenetrateBehavior의 _left > 0 → hit.Despawn() 호출 안 함, 계속 비행
9. 수명 만료 또는 관통 소진 시 hit.Despawn()
   ├─ OnDespawn 이벤트 → (만약 ExplodeOnDespawnBehavior 있으면 InstantHit 스폰)
   ├─ 모든 Behavior.OnDetach
   ├─ 이벤트 구독자 정리
   └─ HitInstance와 HitSnapshot 풀 반납
```

**관찰**: 근접/원거리, 단순/복합 공격 모두 이 흐름의 변형으로 표현된다. 새 공격은 새 `HitSpawnConfig` + 필요 시 Behavior 1~2개만 추가하면 된다.

---

## 9. 구현 순서 로드맵

각 단계 말미에 게임이 플레이 가능한 상태를 유지한다.

**1단계. 기반 뼈대**
- `StatContainer` + `StatModifier` + 풀링
- `DamageContext` + `DamagePipeline` 최소 버전 (근접만)
- 유닛의 `TakeDamage`가 Pipeline을 타도록

**2단계. Effect 시스템**
- `IEffect` / `ITickEffect` / `IDurationEffect` / `IStackableEffect` 계약 확정
- `EffectHost` 구현 (중복/스택/만료)
- 샘플 Effect 3개: 스탯 버프, 흡혈, 순간이동 또는 빙결

**3단계. 데미지 파이프라인 확장**
- 모든 공격 타입에 크리/회피 공식 일원화
- 공격자/피격자 훅 완성
- 기존 근접 공격이 파이프라인 위에서 동작 확인

**4단계. HitSnapshot + MovingHit**
- `HitSnapshot` 도입 (기존 Projectile 데이터를 스냅샷으로 이관)
- `MovingHit` 구현 (기존 Projectile의 대체)
- `HitLauncher.Launch`로 AttackModule/Skill 진입점 통일

**5단계. InstantHit + AuraHit**
- 근접 공격을 `InstantHit`로 이관 (Shape: Cone/Line/Box)
- 오라/지속 영역을 `AuraHit`로 이관
- `HitShape` 추상 정리

**6단계. Behavior 시스템**
- `IHitBehavior` 도입
- 우선순위 Penetrate → Homing → Split/Bounce 순으로 구현
- 로그라이크 업그레이드가 런타임에 Behavior 주입할 수 있게

**7단계. View 분리 정리**
- 교체 가능한 View에 대해 LateUpdate 동기화 패턴 적용
- `UnitViewBase` 추상 + 종류별 파생
- Version 패턴으로 재사용 안전성 확보

**8단계. 상호작용/밸런싱**
- Behavior 금지 조합 정의
- 풀 warmup, 통계/디버그 오버레이
- 로드 테스트 (최대 동시 유닛/투사체 수)

---

## 10. 자주 밟는 함정

1. **조기 최적화로 DOTS부터 도입** — 러닝 커브·생태계 호환성 때문에 출시 일정이 무너지는 흔한 사유. 프로파일러로 병목을 확인한 뒤 결정.
2. **Effect 라이프사이클 모호** — `Apply/Remove/ClearMemory` 같은 애매한 메서드 이름은 구현자를 혼란시킨다. 새 Effect 만들기가 두려워 주석 처리된 채 방치되는 이유.
3. **스냅샷 없이 원거리 데미지 계산** — 발사 시점 버프가 착탄 시점에 풀리면 반영 안 됨. "원거리가 근거리보다 약함" 현상의 대표 원인.
4. **공격자 재사용 감지 실패** — 풀에서 재사용된 유닛이 공격자로 남아 "적이 아군에게 흡혈" 같은 버그.
5. **매 프레임 SetParent** — hierarchy dirty 재계산 폭발. 형제 + LateUpdate 복사로 해결.
6. **View → Controller 역참조** — 분리의 의미가 사라짐. 풀 반납 로직이 View에 있으면 특히 위험.
7. **문자열 애니 파라미터 하드코딩** — 모든 View가 같은 Animator 규약을 강제당함. 의미 기반 이벤트로 변환.
8. **HitInstance를 단일 클래스로 통합 시도** — `if (isMoving) ... else if (isAura) ...` 지옥. 수명/판정 패턴이 다른 것은 하위 클래스로 분리 유지.
9. **Behavior로 공격 종류 자체를 표현** — "근접이냐 원거리냐"는 HitInstance 종류의 문제지 Behavior의 문제가 아님.
10. **Modifier와 Effect를 같은 서랍에** — 값 계산과 이벤트 반응이 섞이면 God Object. 레이어를 유지.
11. **풀링 누락** — DamageContext, StatModifier, HitSnapshot, Effect를 new로 만들면 초당 수백 GC. 기본값을 풀링으로.
12. **이벤트 해제 누락** — Effect/Behavior의 `OnDetach`에서 이벤트 구독 해제 안 하면, 유닛 재사용 시 유령 구독자가 남는다.

---

## 11. 명명 규약 권장

- `On*Changed` — 상태 변화 알림 이벤트 (예: `OnActionChanged`)
- `OnBefore* / OnAfter*` — 파이프라인 단계 훅
- `*Snapshot` — 특정 시점에 확정되는 불변 또는 준불변 데이터 (`HitSnapshot`)
- `*Context` — 파이프라인을 따라 흐르는 가변 컨테이너 (`DamageContext`)
- `*Host` — 컴포넌트 보유 컨테이너 (`EffectHost`)
- `*Spawner / *Launcher` — 생성 팩토리 역할
- `I*Behavior` — 조합 가능한 행동 인터페이스 (`IHitBehavior`)
- `*Base` — 추상 기반 클래스 (`UnitViewBase`)

---

## 12. 이 설계가 수용하는 것 / 거부하는 것

**수용 (Design Goals)**
- 로그라이크 업그레이드 — Effect/Behavior 동적 주입
- 다양한 공격 형태 — Shape + Behavior 조합
- View 교체 — 하나의 AI에 여러 비주얼
- 버프/디버프 중첩 — 카운터/스택 규칙
- 데이터 주도 설계 — Spec 시트만으로 새 스킬
- 대량 인스턴스 — 풀링 전제

**거부 또는 별도 레이어에 맡김 (Out of Scope)**
- DOTS/ECS 마이그레이션
- 네트워크 동기화 (별도 직렬화 레이어 필요)
- 저장/로드 (영속성은 Data 레이어에 어댑터로 붙임)
- AI 세부 행동 트리 (FSM 수준까지만)

---

## 부록 A. 인터페이스 요약표

| 레이어 | 핵심 타입 | 역할 |
|---|---|---|
| Unit | `UnitController`, `UnitData`, `UnitViewBase`, `UnitFsmHandler` | 조립자/데이터/뷰/상태 |
| Stat | `Stat`, `StatModifier`, `StatContainer<TKey>` | 값 계산 |
| Effect | `IEffect`, `ITickEffect`, `IDurationEffect`, `IStackableEffect`, `EffectHost` | 반응/상태 |
| Damage | `DamageContext`, `DamagePipeline` | 데미지 흐름 |
| HitInstance | `HitInstance`, `InstantHit`, `MovingHit`, `AuraHit`, `HitShape`, `HitSnapshot`, `IHitBehavior`, `HitLauncher`, `HitSpawner` | 공격 인스턴스 |
| 출처 | `Augment`, `Item`, `Ability` (Effect 묶음) | 로그라이크 요소 |

---

## 부록 B. 새 기능 추가 체크리스트

"공격 시 확률로 얼음 화살 하나 추가 발사" 같은 기능을 추가한다고 할 때:

- [ ] **이게 어느 레이어인가?** → Effect (반응형).
- [ ] 기존에 이미 있는 `IEffect` 구현체로 표현 가능한가, 새 클래스 필요한가? → 새 클래스.
- [ ] 구독할 훅은? → `OnFireHit` 또는 `OnAfterDealDamage`.
- [ ] 출처는? → Augment 또는 Item.
- [ ] 기존 모듈(UnitController, HitInstance, DamagePipeline) **수정 필요한가?** → 필요 없다면 설계가 잘 된 것.
- [ ] 풀링 적용? → 새 Effect/Projectile이면 풀에 등록.
- [ ] OnDetach에서 이벤트 해제 누락 없는가?
- [ ] 유닛 재사용(Version) 대응 필요한가? → HitSnapshot/Effect 참조가 있다면 Yes.

이 체크리스트를 한 번 통과하면 설계 내에서의 확장이고, 막히면 레이어 설계 자체를 재검토할 신호다.
