---
name: combat-hit
description: How to implement attack instances (melee swings, projectiles, auras, lasers) in RogueLikeTemplate through the unified HitInstance / HitShape / HitSnapshot / IHitBehavior system. Use whenever the user touches AttackModule, Projectile, Skill.Use, homing / piercing / splitting / bouncing bullets, aura damage, or anything that spawns a hit volume. Enforces the 4-axis composition model and HitSnapshot lock-at-fire-time rule from UnitCombatDesign.md §7.
paths: Assets/@Project/Scripts/Game/Projectile/**/*.cs, Assets/@Project/Scripts/Game/Unit/AttackModule/**/*.cs, Assets/@Project/Scripts/Game/Skill/**/*.cs, Assets/@Project/Scripts/Game/Hit/**/*.cs
---

# Combat — HitInstance (L5)

근접·원거리·오라·레이저를 **하나의 추상**으로 통합하는 공격 인스턴스 레이어. **Phase 4/5 적용됨** — 유지보수 가이드.

## Source of truth

- **설계 문서**: `UnitCombatDesign.md` §7 — §7.2 4축 모델, §7.6 HitSnapshot, §7.7 HitLauncher, §7.8 매핑 표, §7.10 금지 조합, §7.11 성능, §7.12 소환수 제외.
- **실제 소스**:
  - `Assets/@Project/Scripts/Game/Hit/HitInstance.cs` — 추상 베이스
  - `Assets/@Project/Scripts/Game/Hit/InstantHit.cs` — 근접 / 레이저 / 즉발
  - `Assets/@Project/Scripts/Game/Hit/AuraHit.cs` — 지속 영역
  - `Assets/@Project/Scripts/Game/Projectile/Projectile.cs` — MovingHit 역할(HitInstance 상속)
  - `Assets/@Project/Scripts/Game/Hit/HitShape.cs` + `Circle/Cone/Box/LineShape`
  - `Assets/@Project/Scripts/Game/Hit/IHitBehavior.cs` — Penetrate/Homing/Bounce/SplitOnDespawn
  - `Assets/@Project/Scripts/Game/Hit/HitSnapshot.cs`, `HitSnapshotBuilder.cs`
  - `Assets/@Project/Scripts/Game/Hit/HitLauncher.cs`
  - 발사 지점: `Unit/AttackModule/{MeleeSwing,Projectile,Spear,Collider}Module.cs`, `Skill/{Around,AreaDot,Projectile}Skill.cs`

## Non-negotiable rules

1. **`HitSnapshot`은 발사 시점 확정.** 착탄 시점에 공격자 Stat 재조회 금지. `HitSnapshotBuilder.Build`가 attacker.Stats 복사 + `OnFireHit` 훅 + 크리 판정 완료.
2. **`Snapshot.IsAttackerAlive()` 체크.** 착탄 시점에 `Snapshot.Attacker.Version != Snapshot.AttackerVersion`이면 조용히 drop. 풀 재사용 버그 방지.
3. **`HitLauncher.Launch`가 단일 진입점.** AttackModule/Skill은 HitLauncher 한 줄 호출로 끝난다. `Physics2D.OverlapCircleAll`을 AttackModule/Skill이 직접 호출 금지 — 전부 `HitShape.Query`로 흡수.
4. **공격 종류 = `HitInstance` 하위 타입.** `if (isMoving) ... else if (isAura) ...` 분기 금지. `InstantHit` / `MovingHit`(=Projectile) / `AuraHit` 중 선택.
5. **Behavior는 조합식.** Penetrate/Homing/Split/Bounce는 `IHitBehavior` 구현체로 만들고 `hit.AddBehavior(...)`. 상속 트리에 녹이지 않는다. Priority 순 자동 정렬.
6. **위치 덮어쓰기 Behavior ↔ Velocity Behavior 공존 금지.** Orbit/Falling vs Homing. 금지 조합 §7.10 참조 (아래 표).
7. **전부 풀링.** `HitInstance`(자체 Queue 또는 ObjectPoolBase), `HitSnapshot`(DisposeObject), `HitShape` 재사용. `HitInstance.Despawn`에서 Snapshot.Dispose + 리스너 정리 + `ReleaseToPool()` 하위 구현.
8. **소환수는 HitInstance가 아니다.** 독립 수명 + AI + HP면 유닛. `Skill.Use` 안에서 UnitSpawnHandler 경유.
9. **Infinite-loop Behavior는 InstantHit/AuraHit의 LifeTime으로만 종료.** Behavior 자체가 무한 루프면 Despawn 신호를 누구도 못 보낸다.
10. **RaiseHit 순서**: Pipeline 먼저 → OnHit invoke. 순서 반대면 Behavior가 Despawn 호출 시 Snapshot 참조가 무효화됨.

## Entry points / 대표 API

### 4축 조합

```
HitInstance      × HitShape         × HitSnapshot     × IHitBehavior[]
  InstantHit        Circle               Attacker+Ver      Penetrate
  MovingHit         Cone                 BaseDamage        Homing
  AuraHit           Box                  IsCritical        Bounce
                    Line                 LifeTime          SplitOnDespawn
                                         HitCount
```

### HitLauncher

```csharp
public static class HitLauncher
{
    public static HitInstance Launch(
        UnitController attacker,
        IDamageSpec damageSpec,           // SpecAttack / SpecSkill 공통
        SpecProjectile projectileSpec,    // null 허용 (InstantHit/AuraHit)
        HitInstance instance,             // 호출자가 준비
        HitShape shape,                   // null 허용 (Projectile은 Collider 기반)
        Vector3 origin,
        Vector3 direction,
        UnitController target = null,
        IReadOnlyList<IHitBehavior> behaviors = null,
        float lifeTimeOverride = -1f);
}
```

### HitSnapshot

```csharp
public class HitSnapshot : DisposeObject<HitSnapshot>
{
    public UnitController Attacker; public int AttackerVersion;
    public UnitController Target;

    public float BaseDamage;
    public float Percent;
    public eDamageType DamageType;
    public eAttackType AttackType;

    public float CritChance;
    public float CritMultiplier;
    public bool  IsCritical;

    public float Speed;
    public float LifeTime;
    public int   HitCount;

    public Vector3 Origin;
    public Vector3 Direction;

    public readonly Dictionary<string, object> Extra;

    public bool IsAttackerAlive();
}
```

### 공격 종류별 매핑 (§7.8 기준)

| 공격 | HitInstance | Shape | 주요 Behavior |
|---|---|---|---|
| 근접 스윙 | InstantHit (LifeTime=0) | Cone | (선택) Penetrate |
| 근접 찌르기 (Spear) | InstantHit | Box (forward 길이) | - |
| 레이저 | InstantHit (LifeTime>0) | Line | - |
| 투사체 | MovingHit (Projectile) | (Collider) | Penetrate(HitCount 자동), Homing, Split, Bounce |
| 오라 / 장판 | AuraHit (LifeTime>0 + TickInterval) | Circle | - |
| 범위 스킬 즉발 | InstantHit | Circle | - |
| 파이어볼 | MovingHit → InstantHit | Circle → Circle(큰) | SplitOnDespawn |

### Behavior 카탈로그 & 금지 조합 (§7.10)

| Behavior | Priority | 동작 |
|---|---|---|
| PenetrateBehavior | 10 | N번 히트 후 Despawn. Projectile.OnSpawn이 HitCount 기반 자동 장착 |
| HomingBehavior | 20 | 매 프레임 Velocity를 타겟 방향으로 회전 (구체 구현은 Movement 모듈 협업) |
| BounceBehavior | 50 | 벽 감지 + Reflect (Movement가 collision 이벤트 발행 필요) |
| SplitOnDespawnBehavior | 60 | Despawn 시 자식 N개 스폰 (콜백으로 생성 함수 주입) |

**금지 조합:**
- ❌ `HomingBehavior` + 위치 덮어쓰기 계열 (Orbit/Falling) 동시 부착. Velocity vs Position 충돌.
- ❌ `BounceBehavior` + `Homing` 동시 — Reflect 후 Homing이 다시 방향 돌려서 발사자 쪽으로 날림.

## 사용 패턴

### 근접 스윙 (Cone)

```csharp
var shape = new ConeShape { Radius = Range, HalfAngleDegrees = _spec.arcAngle * 0.5f, Direction = dir };
var instance = InstantHit.Create(origin);
HitLauncher.Launch(from, _spec, null, instance, shape, origin, dir, to);
```

### 투사체 (MovingHit)

```csharp
var projectile = ObjectPoolManager.Projectile.Get(specProjectile.prefab);
HitLauncher.Launch(from, _spec, specProjectile, projectile, null, origin, dir, to);
// PenetrateBehavior는 Projectile.OnSpawn이 SpecProjectile.hitCount로 자동 장착
```

### 오라 (AuraHit + TickInterval + LifeTime)

```csharp
var shape = new CircleShape { Radius = _spec.range };
var instance = AuraHit.Create(origin);
instance.TickInterval = _spec.arg0;
HitLauncher.Launch(from, _spec, null, instance, shape, origin, Vector3.right, target, lifeTimeOverride: duration);
```

## Anti-patterns — 절대 다시 도입 금지

- ❌ Projectile이 `UnitController _owner` 필드로 공격자 직접 참조. Snapshot + Version만.
- ❌ `from.DealDamage(unit, _spec.baseDamage, ...)` 직접 호출 (AttackModule/Skill 내부). HitLauncher 경유.
- ❌ `Physics2D.OverlapCircleAll` / `OverlapBoxAll`를 AttackModule/Skill이 직접 호출. `HitShape.Query`로만.
- ❌ 이동 방식을 상속으로 표현 (`StraightMovement`, `HomingMovement`, `BounceMovement` 별도 클래스). Behavior 조합으로 대체.
- ❌ `HitSnapshot` 필드를 착탄 시점에 `Attacker.Stats.GetStatValue(...)`로 재조회.
- ❌ `hit.OnHit += ...` 구독 후 `OnDetach`에서 `-=` 미대응.
- ❌ `AddBehavior`를 Despawn 이후 호출 (IsAlive 체크 필요).
- ❌ 소환수(독립 AI + HP)를 HitInstance로 표현.

## Checklist — 편집 전 확인

- [ ] 새 공격을 4축에 매핑했는가? (HitInstance 종류 / Shape / Snapshot 필드 / Behavior 목록)
- [ ] Snapshot이 발사 시점에 Attacker Stat을 복사하는가? 착탄 시 재조회가 없는가?
- [ ] 착탄 시 `Snapshot.IsAttackerAlive()` 체크 있는가?
- [ ] `HitLauncher.Launch` 경유 호출인가? 아니면 Physics/DealDamage 직접 호출?
- [ ] 관통/유도/분열을 새 파생 클래스로 만들고 있지 않은가? `IHitBehavior` 가능한가?
- [ ] Homing과 Orbit/Falling 동시 장착 안 하는가?
- [ ] Bounce와 Homing 동시 장착 안 하는가?
- [ ] 소환수인가 HitInstance인가?
- [ ] 새 Behavior가 `OnDetach`에서 이벤트 `-=` 대칭 수행?
- [ ] HitLauncher가 다루지 않는 것(`lifeTimeOverride`, Shape) 필요하면 명시했는가?

## Sample usage — 새 공격 "체인 번개"

- HitInstance: `InstantHit` (한 대상 치고 사라짐) + `ChainBehavior`(신설) 가 hit 시 근처 다른 대상에 새 InstantHit 스폰
- Shape: `CircleShape` (반경 내 단일 타겟 pick)
- 새 Behavior 필요 → `IHitBehavior` 구현. OnAttach에서 `hit.OnHit` 구독, 내부에서 다음 타겟 탐색 + `HitLauncher.Launch` 재호출.

```csharp
public class ChainBehavior : IHitBehavior
{
    public int Priority => 40;
    private readonly int _jumps;
    private int _remaining;

    public ChainBehavior(int jumps) { _jumps = jumps; _remaining = jumps; }

    public void OnAttach(HitInstance hit) { hit.OnHit += OnHitTarget; }
    public void OnDetach(HitInstance hit) { hit.OnHit -= OnHitTarget; }

    private void OnHitTarget(HitInstance hit, UnitController target)
    {
        if (_remaining <= 0) { hit.Despawn(); return; }
        _remaining--;
        // 근처 다음 타겟 탐색 + HitLauncher.Launch(새 InstantHit)
    }
}
```

## 관련 Skill 경계

- DamageContext 구성, 8단계 파이프라인 → `combat-damage`
- 발사 시점 Effect 훅 (`OnFireHit(snap)`) → `combat-effect`
- Attacker Stat 읽기 (HitSnapshotBuilder) → `combat-stat`
- 공격자 Version 체크 → `combat-unit`
- Augment/Item 계층 (Behavior/Effect 공급) → `combat-ability` (현재 제거됨)

## Known stopgap — 풀링

`InstantHit`, `AuraHit`는 프리팹 없이 런타임 `new GameObject + AddComponent`를 쓰며 **자체 `Queue<T>` 풀**이 임시 적용됨 (파일 상단 TODO 주석). 장기적으로 두 타입의 빈 프리팹 + Addressable 등록 후 `ObjectPoolBase<T>`(프리팹 기반)로 이관 예정. 이관 시:
1. `InstantHit.prefab`, `AuraHit.prefab` 에디터 생성 (빈 GameObject + Component)
2. Addressable 키 등록
3. `InstantHit : PoolMonoBehaviour<InstantHit>` 상속 전환 + `AddressFormat = "InstantHit_{0}"`
4. `Create(pos)` → `ObjectPoolManager.InstantHit.Get(0)` 호출로 교체
