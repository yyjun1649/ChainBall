# Systems — Weapon

> 무기(Weapon) 시스템: 슬롯 구조, 시전 정책, 빌드 / 스왑 흐름. 시전 *평가*는 Combat.md §2.
>
> v0.1의 무기는 모두 **지팡이 형태**(GDD §6) 지만, 향후 다른 캐릭터의 무기 카테고리가 들어올 수 있어
> 코드 / 스펙 식별자는 일반화된 `Weapon` 을 쓴다.

---

## 1. Weapon 구조

```
Weapon (런타임 인스턴스) {
    SpecWeapon spec        // SpecData에서 로드된 정의
    Spell?[]   slots       // 길이 = spec.slotCount. null 가능 (빈 슬롯)
    int        cooldownLeft  // 시전 후 spec.cooldownTurns 적용
}
```

- `spec` 은 `id` 만 저장하고 SpecDataManager에서 lookup.
- `slots` 의 인덱스는 `spec.slotShape[i]` 와 매칭되며, 각 슬롯은 **해당 카테고리의 스펠만** 받는다.

---

## 2. 슬롯 제약

`SpecWeapon.slotShape` 는 `eSlotKind[]` 형태 (Specs/Schema/weapon.md 참조). 슬롯에 스펠을 꽂을 때:

```
fun TryEquipSpell(weapon, slotIndex, spell) -> bool {
    var allowed = weapon.spec.slotShape[slotIndex]
    return allowed == ANY || allowed == spell.category
}
```

추가 제약:
- **첫 PROJECTILE 슬롯은 비어 있을 수 없다** (시전 불가). UI에서 시전 버튼 비활성화.
- Modifier / Effect / Trigger 슬롯은 비워둘 수 있다.

---

## 3. 시전 정책

### 3.1 castsPerTurn

`spec.castsPerTurn` 만큼 한 턴에 시전한다.

```
TurnPhase.CAST {
    if weapon.cooldownLeft > 0:
        skip cast (빈 턴)
        weapon.cooldownLeft--
        return

    repeat spec.castsPerTurn times:
        var aimAngle = inputProvider.NextAngle()   // multiAngle=true면 매번 새로 받음
        EvaluateAndFire(weapon, aimAngle)          // Combat.md §2 절차

    if spec.cooldownTurns > 0:
        weapon.cooldownLeft = spec.cooldownTurns
}
```

### 3.2 multiAngle

- `multiAngle = true` (예: 연발 지팡이) → 각 시전 전에 별도로 각도 입력. UI는 "1차 조준 → 발사 → 2차 조준 → 발사".
- `multiAngle = false` → 한 번 입력한 각도로 모두 발사.

### 3.3 cooldownTurns

대마도의 지팡이처럼 `cooldownTurns > 0` 이면 시전 후 N턴은 빈 턴이 된다 — 빈 턴에는 적 턴만 진행.
빈 턴 동안에도 `TurnPhase.UPKEEP` 의 상태이상 tick / Relic ON_TURN_END 는 정상 발화.

---

## 4. Weapon swap (런 중 교체)

상점 / 보상에서 새 무기를 획득하면:

```
1. 새 SpecWeapon 인스턴스화 (slots[] 전부 빈 상태)
2. 기존 weapon의 slots[] 를 새 weapon으로 가능한 만큼 이주
   - 카테고리 호환되는 슬롯에 매칭
   - 슬롯 수가 줄면 초과 스펠은 인벤토리로 이동 (또는 폐기 — 디자인 선택)
3. 기존 weapon은 인벤토리에 보관 (재교체 가능)
```

세부 UX는 메타 / UI 단계에서 확정.

---

## 5. Weapon list (v0.1, GDD §6.3)

| id                    | slotCount | castsPerTurn | cooldownTurns | slotShape                                          |
|-----------------------|-----------|--------------|---------------|----------------------------------------------------|
| `weapon_apprentice`   | 4         | 1            | 0             | TRIGGER / PROJECTILE / MODIFIER / EFFECT           |
| `weapon_repeater`     | 3         | 2            | 0             | PROJECTILE / MODIFIER / EFFECT                     |
| `weapon_archmage`     | 5         | 1            | 1             | TRIGGER / PROJECTILE / MODIFIER / MODIFIER / EFFECT |

(실제 값은 `TWeapon.json` 에서 권위. 본 표는 도메인 도큐의 참조 사본.)

---

## 6. SpellSequence 통합 (코드 매핑, B-3 결정)

Weapon.Cast 의 결과는 코드 측 `SpellSequence` 인스턴스 한 개로 빌드되어 평가된다.
`Skill` (기존 `DamageActionBase<SpecSkill>`) 은 **자율 시전** (캐릭터 패시브 / 자동 발화 어빌리티) 용도로
별도 유지되며, Weapon 시전과 *분리* 된다.

### 6.1 SpellSequence 책임

```csharp
// Conceptual signature (실제 구현은 코드 작업 단계에서 확정)
public class SpellSequence : PooledDisposable {
    SpecHitInstance _projectile;            // PROJECTILE 슬롯 (kind=MOVING)
    List<SpecModifier> _modifiers;          // MODIFIER 슬롯들 (순서대로)
    SpecTrigger _trigger;                   // TRIGGER 슬롯 (선택)
    List<SpecEffect> _effects;              // EFFECT 슬롯들 (순서대로)

    public void Initialize(
        SpecHitInstance proj,
        List<SpecModifier> mods,
        SpecTrigger trig,
        List<SpecEffect> effs);

    public void Use(UnitController from, Vector3 origin, Vector3 direction);
}
```

- `Use(...)` 는 다음 순서를 한 시전 단위로 실행:
  1. SnapshotPatch 누적 (Modifier stat-delta → HitSnapshot 변형)
  2. IHitBehavior 리스트 빌드 (Modifier behavior + projectile 자체 속성에서 유도)
  3. multiShot 만큼 `HitLauncher.FireProjectile(...)` 호출
  4. 각 HitInstance에 TriggerWatcher 부착 (Trigger + Effects 컨텍스트)
- SpellSequence 는 풀링된다. 한 시전이 끝나면(모든 발사체가 소멸하면) 풀에 반납.

### 6.2 Skill ↔ SpellSequence 분리

| 사용 사례                                | 사용 클래스        | 비고 |
|------------------------------------------|--------------------|------|
| Weapon 슬롯 시전 (플레이어 매 턴)         | `SpellSequence`    | 슬롯 4종 합성 |
| 캐릭터 패시브의 자동 발화 (예: Gunslinger 정밀 사격) | `Skill` 또는 IEffect | 단일 SpecSkill 또는 Relic 패턴 |
| 보스의 패턴 공격                          | `Skill`            | 단일 SpecSkill (자율 발화) |
| 적의 평타                                  | `AttackModule` + SpecAttack (현재 미사용) | v0.1 미사용 |

`Skill` 추상 클래스 (`DamageActionBase<SpecSkill>`)는 그대로 유지. 차이:
- `Skill.Use(from, target)` 는 1개 SpecSkill 기반 → 1+ HitInstance.
- `SpellSequence.Use(from, origin, dir)` 는 4종 spec 조합 기반 → 1+ HitInstance + Trigger 컨텍스트.

### 6.3 Weapon — Skill 호출 안 함

Weapon은 직접 `SpellSequence` 만 사용한다. `Skill` 경유로 우회하지 않는다 — Skill은 Weapon과 무관한
"한 spec = 한 시전" 모델이라 Wand 슬롯 시퀀스를 표현할 수 없다.

---

## 7. Open questions

1. 슬롯 시퀀스가 GDD 6.2 표준 외 형태(예: PROJECTILE 2개)를 가질 때 평가 모델 — Combat.md §2.1
   재정의 필요.
2. Modifier가 PROJECTILE 슬롯과 PROJECTILE 슬롯 사이에 있으면 어느 쪽에 적용되는지 — v0.1은
   "**바로 앞의 PROJECTILE**" 단순 규칙. 다중 PROJECTILE 무기 도입 시 재검토.
3. SpellSequence pooling — 시전당 1개 인스턴스라 pool size는 작아도 됨. 발사체 풀과는 무관.
