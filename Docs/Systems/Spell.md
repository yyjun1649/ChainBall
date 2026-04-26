# Systems — Spell

> 4 카테고리(Projectile / Modifier / Trigger / Effect)의 *역할 분리*와 *합성 규칙*.
> 개별 스펠 정의는 `Docs/Specs/Schema/{projectile,modifier,trigger,effect}.md`.

---

## 1. Category responsibilities

| Category    | Owns                                                        | Cannot do                                |
|-------------|-------------------------------------------------------------|------------------------------------------|
| Projectile  | 발사체의 motion, 베이스 stat (damage / bounce / pierce 등). | Trigger 발화, Effect 실행                 |
| Modifier    | 직전 Projectile의 stat / behavior 변형.                     | 새 Projectile 생성 (단, behavior=SPLIT 예외), Effect 실행 |
| Trigger     | "언제 Effect가 발화하는가" 조건.                             | 데미지 / 회복 등 결과를 직접 만들지 않음 |
| Effect      | 실제 결과 (데미지, 상태이상, 회복, 추가 발사체 spawn).       | 자체적으로 Trigger 평가 안 함            |

이 책임 분리가 깨지면 시퀀스 평가가 비결정적이 된다 — 새 스펠을 추가할 때 카테고리를 정확히 고르는 것이 중요.

---

## 2. Slot order semantics (참조)

표준 시퀀스:

```
[Trigger] [Projectile] [Modifier] [Modifier] [Effect]
```

- 슬롯 인덱스 오름차순으로 평가 (Combat.md §2.2).
- Modifier는 **앞쪽 인덱스의 Projectile**에 적용. v0.1 지팡이는 모두 Projectile 1개라 모호하지 않음.
- Effect는 슬롯 시퀀스 *말단*에 위치. Trigger 발화 시 또는 시퀀스 종료 시 실행.

---

## 3. Modifier 합성 (수식 정리)

본 절은 Combat.md §2.3을 그대로 옮긴 것이다 — 디자이너가 Modifier만 보고 의도를 잡을 수 있도록 별도 게시.

```
final.damage   = max( damageMin_max,  ( base.damage + Σ damageDelta ) * Π damageMul )
final.bounce   = base.bounce + Σ bounceDelta
final.pierce   = base.pierce + Σ pierceDelta
final.hitWidth = base.hitWidth * Π hitWidthMul
final.speed    = base.speed   * Π speedMul
final.element  = last non-NONE element among {base.element, mod[0].element, mod[1].element, …}
final.behavior = [base behaviors] + [mod[i].behavior for i where mod[i].behavior != NONE]
```

- `damageMin_max` = `max(mod[i].damageMin)` — 가장 보수적인 하한 채택.
- 합산 → 곱셈 순서 고정.
- `Π damageMul` 가 0이 되면 `damageMin_max` 가 작동.

### 예시: 강화탄 + 경량탄 + 데미지 업

base.damage = 1, bounce = 5
- 강화탄: damageDelta=+2, bounceDelta=-2, damageMin=1
- 경량탄: damageDelta=-1, bounceDelta=+4, damageMin=1
- 데미지 업: damageDelta=+1, damageMin=1

→ damage = max(1, (1 + 2 - 1 + 1) * 1) = **3**
→ bounce = 5 - 2 + 4 = **7**

---

## 4. Trigger 사용 패턴

### 4.1 Trigger 없을 때

시퀀스 종료(=모든 발사체 소멸) 시점에 Effect들이 1회 발화.

### 4.2 Trigger 있을 때

해당 Trigger의 `event` 가 만족된 시점에 Effect들이 발화. `cooldownTurn` / `maxFiresPerCast` 가 발화 빈도 제한.

```
시퀀스: [온 히트] [매직볼] [데미지 업] [폭발]
→ 매직볼이 벽돌에 충돌할 때마다 → 폭발 효과 (반경 1, dmg 1)
→ 매직볼이 5바운스 동안 5번 충돌하면 5번 폭발
```

발화 시 EffectContext (Combat.md §2.5)는 Trigger 발생 시점의 충돌 위치 / 대상으로 채워진다.

### 4.3 다중 Effect

Effect 슬롯이 여러 개면 슬롯 순서대로 모두 실행:

```
시퀀스: [온 킬] [매직볼] [폭발] [흡수]
→ 벽돌 파괴 시 → 폭발 → 흡수 (HP +1)
```

---

## 5. 카테고리 borderline cases

| Question                                                                  | Answer                                                                 |
|---------------------------------------------------------------------------|------------------------------------------------------------------------|
| Modifier가 새 발사체를 생성하는 게 가능?                                    | `behavior=SPLIT` / `CLONE_AT_FIRE` 만 예외. 그 외에는 Effect `SPAWN_HIT_INSTANCE` 사용. |
| Effect가 자기 안에서 새 Trigger를 발화시키는 게 가능?                       | 불가능. 순수 결과만. 단, Effect가 spawn한 발사체는 자신의 시퀀스 / Trigger를 가질 수 있음. |
| Trigger 슬롯 비워두고 Effect만 두면?                                        | "always" 모드 — 시퀀스 종료 시 Effect 발화.                              |
| Projectile 없이 Effect만 가능?                                              | 불가능. PROJECTILE 슬롯 필수 (Weapon schema가 강제).                     |

---

## 6. Authoring rules

```
새 스펠 추가 시:
[ ] 책임이 어느 카테고리인지 명확한가? (위 §1 표)
[ ] Modifier라면 stat-delta로 표현 가능한가? 안 되면 behavior enum에 추가.
[ ] Trigger / Effect라면 단일 책임을 지키는가? (Trigger가 데미지 주거나, Effect가 조건 검사하지 않는다)
[ ] cooldownTurn / maxFiresPerCast 등 폭주 방지 가드가 필요한 빈도인가?
[ ] 로컬라이즈 키 등록 (SpecLocalize)
```

---

## 7. Modifier ↔ 코드 매핑 (HitSnapshot patch / IHitBehavior)

각 Modifier 컬럼이 실제 코드에서 *어떻게* 적용되는지의 권위 표. SpellSequence 평가기가 이 표를 따른다.

### 7.1 Stat-delta → HitSnapshot patch

| Modifier 컬럼      | HitSnapshot 필드 적용                                                  |
|--------------------|------------------------------------------------------------------------|
| `damageDelta`      | `snap.BaseDamage += Σ damageDelta`                                     |
| `damageMul`        | `snap.Percent *= Π damageMul` (또는 `BaseDamage *=`, 합산→곱셈 순서 고정) |
| `damageMin`        | 최종 `BaseDamage * Percent` 결과를 `max(damageMin_max, …)`              |
| `bounceDelta`      | (런타임) `BounceBehavior._remaining += Σ` — Behavior 부착 시점에 가산  |
| `pierceDelta`      | (런타임) `PenetrateBehavior._remaining += Σ`                           |
| `hitWidthMul`      | `snap.Extra["hitWidth"] *= Π hitWidthMul` (Movement 측 충돌 판정에서 사용) |
| `speedMul`         | `snap.Speed *= Π speedMul`                                             |
| `element`          | `snap.Extra["element"] = lastNonNoneElement`                           |

### 7.2 `eModifierBehavior` → IHitBehavior

| Modifier behavior  | 부착되는 IHitBehavior                  | 비고 |
|--------------------|----------------------------------------|------|
| `NONE`             | (없음)                                 | stat-delta만 적용 |
| `SPLIT`            | `SplitOnDespawnBehavior(child=behaviorParam1, spread=behaviorParam2)` | OnDespawn 시 자식 spawn |
| `PIERCE_ON_HIT`    | `PenetrateBehavior(int.MaxValue)`      | "무한 관통" 모드 |
| `CHAIN`            | `ChainBehavior(maxJumps=behaviorParam1)` | 코드에 신규 IHitBehavior 추가 필요 |
| `CLONE_AT_FIRE`    | (Behavior 아님 — SpellSequence가 spawn 단계에서 multiShot+1 처리) | |
| `FREEZE_ROW`       | (Effect로 우회 — `STATUS_FREEZE` Effect를 시퀀스에 삽입하는 게 권장) | |

> 코드에 아직 없는 Behavior(`HomingMovement`, `OrbitBehavior`, `FallingBehavior`, `ChainBehavior`)는
> 구현 단계에서 추가. UnitCombatDesign §7.10의 Behavior 충돌 규칙을 따른다.

---

## 8. Trigger ↔ 코드 매핑 (HitInstance event)

`SpecTrigger.event` 가 코드 측 어느 이벤트에 구독되는지의 권위 표. `TriggerWatcher` 가 이 매핑에 따라
`HitInstance` 또는 `Handlers.Event` 의 채널에 구독한다.

| `eTriggerEvent`      | 구독 대상                                      | 발화 컨텍스트 |
|----------------------|------------------------------------------------|----------------|
| `BRICK_HIT`          | `HitInstance.OnHit`                            | target = 충돌 Brick |
| `BRICK_KILL`         | `HitInstance.OnHit` + Brick.HP=0 후처리 검사    | target = 파괴된 Brick |
| `WALL_BOUNCE`        | `Handlers.Event` `WallBounced` 채널            | 반사 위치, 벽 방향 |
| `PROJECTILE_DESPAWN` | `HitInstance.OnDespawn`                        | 마지막 위치 |
| `NTH_BRICK_HIT`      | `HitInstance.OnHit` + 카운터                   | `nthCount` 만족 시점 |
| `ELEMENT_MATCH`      | `HitInstance.OnHit` + `snap.Extra["element"] == brick.element` | 충돌 시 |
| `LINE_CLEAR`         | `Handlers.Event` `LineCleared` 채널            | 비워진 행 인덱스 |
| `DANGER_PROXIMITY`   | (Step A 시점에 1회 평가)                        | 위험 라인 거리 |
| `CONSECUTIVE_HIT`    | `HitInstance.OnHit` + 자체 카운터 (벽 반사 시 리셋) | `nthCount` 만족 시점 |
| `FULL_BOUNCE`        | `BounceBehavior` 가 0 도달 시 emit (커스텀 이벤트) | despawn 직전 |

> Trigger는 **`HitInstance` 단위 이벤트** 를 본다. Relic의 `eRelicHook` (`OnBeforeDealDamage` 등)
> 은 **`UnitController` 단위 이벤트 = `EffectHost` 채널** 을 본다. 이 둘을 섞지 말 것.
