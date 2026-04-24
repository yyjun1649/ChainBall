---
name: combat-unit
description: How to work with Unit Controller / Data / View / FSM in RogueLikeTemplate. Use whenever the user touches UnitController, UnitData, UnitView, UnitHPBar, UnitFsmHandler, UnitState*, unit pooling, animation events, or anything under Assets/@Project/Scripts/Game/Unit/. Enforces the Controller/Data/View/FSM split from UnitCombatDesign.md §3.
paths: Assets/@Project/Scripts/Game/Unit/**/*.cs
---

# Combat — Unit (L1)

유닛의 Controller / Data / View / FSM 네 덩어리 + 풀 재사용 안전성 + 의미 기반 이벤트 규약. **Phase 0/6은 이미 적용됨** — 이 문서는 유지보수 가이드.

## Source of truth

- **설계 문서**: `Assets/@Project/Scripts/Game/UnitCombatDesign.md` §3 (Unit 구조), §3.5 (sibling View), §3.6 (Version), §3.7 (의미 이벤트), §3.8 (FSM 자식 조립).
- **실제 소스**:
  - `Assets/@Project/Scripts/Game/Unit/UnitController.cs`
  - `Assets/@Project/Scripts/Game/Unit/UnitData.cs`
  - `Assets/@Project/Scripts/Game/Unit/UnitDataModule.cs`
  - `Assets/@Project/Scripts/Game/Unit/UnitView.cs`
  - `Assets/@Project/Scripts/Game/Unit/UnitHPBar.cs`
  - `Assets/@Project/Scripts/Game/Unit/Fsm/UnitFsmHandler.cs`, `Fsm/UnitStateBase.cs`, `Fsm/State/Ai*State.cs`
  - `Assets/@Project/Scripts/Define/SpecEnums.cs` (`UnitAction` enum)

## Non-negotiable rules

1. **Controller는 얇은 퍼사드.** 전투 로직(계산, 판정)을 직접 담지 않는다. `DamagePipeline`/`HitInstance`가 처리. Controller는 상태 변경 + 이벤트 발행 + `ApplyDamageToHealth(ctx)` 수신만.
2. **`UnitController.Version` 필수.** `Initialize()` 진입마다 `Version++`. 풀에서 재사용되는 유닛 참조자(View, HPBar, HitSnapshot)는 발급 시 Version 기억 + 접근 시 비교 + 불일치면 조용히 drop.
3. **`UnitData`는 순수 C#.** `MonoBehaviour` 금지, Unity API 의존 금지. `PooledDisposable` 기반 풀링. `Stats` + `Effects` 허브.
4. **`UnitView`는 sibling 배치.** `transform.SetParent(controller.transform)` 금지. 형제 배치 + `LateUpdate` position 복사. `UnitHPBar`도 동일.
5. **`RegisterHandler` / `UnregisterHandler` 대칭.** 풀 반납 직전 Unregister. `OnDeath(force=true)`와 `ReleaseOnDeathAnimation()` 두 경로 모두.
6. **의미 기반 이벤트만.** Controller는 `UnitAction` enum + 방향 벡터만 발행. 문자열 Animator 파라미터(`"MoveX"`, `"Move"`, `"Death"`)를 Controller/State가 알면 안 됨. Animator 규약은 View 내부에서만.
7. **FSM State는 자식 오브젝트로 조립.** `UnitPrefab/StateMachine/` 자식 + `GetComponentsInChildren<UnitStateBase>()` 자동 등록.

## Entry points / 대표 API

### UnitController

```csharp
public class UnitController : MonoBehaviour
{
    public int Version { get; private set; }
    public UnitData Data { get; private set; }
    public float CurrentHp { get; private set; }
    public bool IsAlive { get; private set; }

    public Relay<UnitAction> OnActionChanged { get; }
    public Relay<Vector2>    OnMoveDirectionChanged { get; }
    public Relay<bool>       OnFlip { get; }

    public Relay<DamageInfo, UnitController> OnTakeDamage { get; }
    public Relay<DamageInfo, UnitController> OnDealDamage { get; }
    public Relay<bool>                       OnDeath { get; } // bool: isForce

    public void Initialize(UnitData data, LayerMask mine, LayerMask enemy)
    {
        Version++;
        // ...
    }

    // DamagePipeline이 호출 — 외부에서 CurrentHp 직접 조작 금지
    public void ApplyDamageToHealth(DamageInfo ctx);

    // 내부에서 DamagePipeline.Process 호출
    public float DealDamage(UnitController to, float value, eDamageType dmg, eAttackType atk, float percent = 1f);
}
```

### UnitView — sibling + LateUpdate

```csharp
public class UnitView : MonoBehaviour
{
    private UnitController _unitController;
    private Transform _controllerTr;
    private int _controllerVersion;
    private bool _followTarget;

    public void Initialize(UnitController c)
    {
        _unitController = c;
        _controllerTr = c.transform;
        _controllerVersion = c.Version;
        _followTarget = true;
        transform.position = _controllerTr.position + _offset; // SetParent 없음
        RegisterHandler();
    }

    private void LateUpdate()
    {
        if (!_followTarget) return;
        if (_unitController == null || _unitController.Version != _controllerVersion)
        {
            _followTarget = false;
            return;
        }
        transform.position = _controllerTr.position + _offset;
    }

    private void OnActionChanged(UnitAction action)
    {
        // Animator 규약은 여기서만 매핑 — 외부는 몰라야 함
        switch (action)
        {
            case UnitAction.Move:  _animator.SetBool("Move", true);  break;
            case UnitAction.Idle:  _animator.SetBool("Move", false); break;
            case UnitAction.Death: _animator.Play("Death"); break;
        }
    }
}
```

### UnitData — 순수 C# 허브 (증분 CalculateStat)

```csharp
public class UnitData : PooledDisposable
{
    public SpecCharacter SpecCharacter { get; private set; }
    public TStatContainer<eStatType> Stats { get; private set; }
    public UnitEffects Effects { get; private set; }

    public void Initialize(string name)
    {
        // Spec 로드 + 모듈 Initialize + RegisterBaseStats() 1회 + CalculateStat
    }

    public void CalculateStat()
    {
        Stats.CalculateStat();
        if (!unitName.Contains("Monster")) HandlerManager.Event.Event(eEventType.Stat);
    }

    protected override void Reset()
    {
        Effects?.Clear();
        Stats?.Initialize();
    }
}
```

## Anti-patterns — 절대 다시 도입 금지

- ❌ `transform.SetParent(controller.transform)` — View/HPBar/기타 follow 컴포넌트.
- ❌ `Controller.OnAnimationSetInt.Dispatch("MoveX", ...)` 같은 문자열 애니 이벤트. `OnActionChanged`/`OnMoveDirectionChanged`만.
- ❌ `UnitController.TakeDamage(DamageInfo, UnitController)` — 제거된 메서드. HP 차감은 `ApplyDamageToHealth`만.
- ❌ `CurrentHp -= value`를 Controller 외부 또는 `ApplyDamageToHealth` 외부에서.
- ❌ `UnitData`에 MonoBehaviour 상속 / `GetComponent<>` / Unity Editor 의존.
- ❌ `UnitData.CalculateStat` 안에서 `Effects.Clear()` + Base stat 재등록 같은 "전체 리빌드" 패턴 부활. Base는 `Initialize()` 1회.
- ❌ FSM State를 코드에서 `new`로 수동 생성.
- ❌ `RegisterHandler` 추가 시 대응 `UnregisterHandler` 미구현.

## Checklist — 편집 전 확인

- [ ] Controller `Initialize()`에 `Version++`가 유지되는가?
- [ ] 새 View 구현체가 sibling 배치 + LateUpdate + `_controllerVersion` 비교를 가지는가?
- [ ] 새 `RegisterHandler` 라인수만큼 `UnregisterHandler`가 있는가?
- [ ] Controller 신규 이벤트에 문자열 Animator 파라미터가 섞이지 않았는가?
- [ ] 새 `UnitAction` 값 추가 시 기본 `UnitView`에 Animator 매핑했는가?
- [ ] `UnitData` 변경이 Unity 의존성을 도입하지 않는가?
- [ ] HP 차감 경로가 `ApplyDamageToHealth`로만 통하는가?

## Sample usage — 새 UnitAction 값 추가

Boss에 "포효(Roar)" 상태 추가 가정:

```diff
 // Define/SpecEnums.cs
 public enum UnitAction { Idle, Move, Attack, Hit, Death,
+    Roar,
 }
```

```diff
 // BossIdleState.cs 등 조건 만족 시
+_owner.OnActionChanged.Dispatch(UnitAction.Roar);
```

```diff
 // 해당 Boss View 파생에서 매핑
 switch (action)
 {
+    case UnitAction.Roar: _animator.Play("Roar"); break;
 }
```

Controller/State는 **의미**, View가 Animator **문자열**.

## 관련 Skill 경계

- 스탯 모디파이어 / StatContainer / Base stat → `combat-stat`
- Effect 추가/제거, 반응형/상태형 → `combat-effect`
- 데미지 계산, 크리, HP 차감, 데미지 텍스트 → `combat-damage`
- 공격 발사, 근접/원거리/오라 → `combat-hit`
- Augment/Item/Ability (**현재 제거됨** — 재도입 시) → `combat-ability`
