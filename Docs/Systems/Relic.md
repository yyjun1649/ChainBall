# Systems — Relic

> 유물(패시브)의 hook 발화 시점, 조건 평가, stat 합성. 개별 유물 정의는 `Specs/Schema/relic.md`.

---

## 1. Relic 적용 모델

```
Relic 인스턴스 {
    SpecRelic spec
    int       usesLeftThisRun  // spec.usesPerRun > 0 일 때 카운터
}

RunState {
    List<Relic> relics
    // …
}
```

- 유물은 한 런 동안 영구 보관. 런 종료 시 폐기.
- `usesPerRun > 0` 인 유물(예: 최후의 발악)은 `usesLeftThisRun` 가 0이 되면 비활성.
- 같은 유물 중복 보유는 v0.1에서 **금지** (한 종류는 1개). 향후 stack 검토.

---

## 2. Hook 발화

`SpecRelic.hook` 의 enum 값이 발화 시점을 결정. 코드 측 두 채널이 발화 소스:

- **`EffectHost` (UnitController에 1:1)** — DamagePipeline 단계별 훅. UnitCombatDesign §5.3 참조.
- **`Handlers.Event` (CombatEvent 채널)** — 전투 단위 이벤트 (TurnEnded, LineCleared 등).

본 표가 권위:

| `eRelicHook`               | 코드 구독 채널 / 이벤트                                                           | 비고 |
|----------------------------|------------------------------------------------------------------------------------|------|
| `PASSIVE_GLOBAL`           | (no event) — `HitSnapshotBuilder.Build` 안에서 등록된 PASSIVE_GLOBAL 유물의 stat-delta를 매번 누적 | 시전마다 |
| `ON_HIT`                   | `EffectHost.OnAfterDealDamage` (Player의 EffectHost)                              | DamagePipeline 후. HitInstance.OnHit 직후 |
| `ON_KILL`                  | `EffectHost.OnKill` (UnitCombatDesign §5.3)                                       | 대상 사망 확정 후 |
| `ON_BOUNCE`                | `Handlers.Event` `WallBounced` 채널                                                | 벽 반사 |
| `ON_BOUNCE_EXHAUSTED`      | `Handlers.Event` `ProjectileDespawned` (사유 = `BOUNCE_EXHAUSTED`)                 | despawn 사유 enum 필요 |
| `ON_PROJECTILE_DESPAWN`    | `Handlers.Event` `ProjectileDespawned` (모든 사유)                                 |  |
| `ON_TURN_END`              | `Handlers.Event` `TurnEnded`                                                       | TurnPhase.UPKEEP 끝 |
| `ON_BATTLE_END`            | `Handlers.Event` `BattleEnded`                                                     | 승리 / 패배 직후 |
| `ON_BATTLE_START`          | `Handlers.Event` `BattleStarted`                                                   |  |
| `ON_DAMAGE_TAKEN`          | `EffectHost.OnAfterTakeDamage` (Player의 EffectHost)                              | TurnPhase.DAMAGE 적용 후 |
| `ON_FATAL`                 | `EffectHost.OnBeforeTakeDamage` 안에서 HP 차감 시뮬레이션 후 0 이하 예측 시         | 차감 *직전* 가로채기 |
| `ON_BRICK_DESTROY`         | `EffectHost.OnKill` (별칭 — 확률 분기용)                                           | 동일 채널, 동작만 다름 |
| `ON_SHOP_OPEN`             | `Handlers.Event` `ShopOpened`                                                      | UI 진입 시 |

**구현 패턴**: 각 SpecRelic은 `EffectFactory.Create` 로 `IEffect` 인스턴스를 만들어 Player의 `EffectHost`
에 `Add` 한다. 그 IEffect 가 `OnAttach` 에서 위 채널에 구독, `OnDetach` 에서 해제. UnitCombatDesign §5.4의
Augment 패턴 그대로.

`ON_KILL` 과 `ON_BRICK_DESTROY` 의 구분: 의미 충돌이 있다면 v0.1은 동일 이벤트(`BrickKilled`)에 발화.
효과 동작 방식만 다름 (전자: 직접 효과, 후자: 확률로 부수효과 spawn). schema에서 분리한 이유는 디자인
가독성. 코드는 같은 이벤트 핸들러에서 처리해도 무방.

---

## 3. Condition 평가

`SpecRelic.condition` 이 `NONE` 이 아니면, hook 발화 시 컨텍스트를 검사:

```
fun EvaluateCondition(relic, ctx) -> bool {
    return when relic.spec.condition {
        NONE                       -> true
        BRICK_HP_EQ                -> ctx.targetBrick?.hp == relic.spec.condParam1
        BRICK_HP_LT_PCT            -> ctx.targetBrick?.hpRatio < relic.spec.condParam1 / 100f
        WITHIN_DANGER_ROW          -> dangerLineDist(ctx.targetBrick) <= relic.spec.condParam1
        WALL_BOUNCE_LR             -> ctx.bounceWall == LEFT || ctx.bounceWall == RIGHT
        SAME_BRICK_HITS_GE         -> ctx.sameBrickHitsThisCast >= relic.spec.condParam1
        PLAYER_HP_LT_PCT           -> playerHp / playerHpMax * 100 < relic.spec.condParam1
        BRICKS_KILLED_THIS_CAST_GE -> ctx.bricksKilledThisCast >= relic.spec.condParam1
        NO_DAMAGE_TAKEN_PREV_BATTLE-> runState.lastBattleNoDamage
        IN_ELITE_BATTLE            -> currentNode.kind == ELITE
    }
}
```

조건 충족 시:
- `chance < 1.0` 이면 RNG 굴림.
- 통과하면 effect 적용 (다음 §4).

---

## 4. Effect 적용

조건 통과한 유물의 효과는 두 경로로 분기:

### 4.1 Stat-delta 경로 (가장 흔함)

`damageDelta` / `damageMul` / `bounceDelta` / `extraCast` / `extraCastDamageDelta` 가 0 또는 1.0 이외면
컨텍스트에 가산:

```
hook=ON_HIT, condition=BRICK_HP_EQ(1):
    → damage += 2  (약점 포착)
hook=PASSIVE_GLOBAL:
    → 모든 발사체 baseDamage 계산 시 + damageDelta
```

### 4.2 Effect 위임 (`triggerEffectId`)

복잡한 효과는 `TEffect` 의 한 행을 호출하는 형태로 위임:

```
hook=ON_KILL, condition=NONE:
    → SpecData.SpecEffect[triggerEffectId] 를 EffectContext와 함께 실행
```

이렇게 하면 유물과 Effect의 동작 정의를 한 군데(TEffect)에서 관리할 수 있어 중복이 줄어든다.

---

## 5. 적용 순서 (deterministic)

같은 hook에 여러 유물이 매칭하면 **유물 획득 순서**대로 적용.

```
PASSIVE_GLOBAL: relicA(damage+1), relicB(damage+1)
→ damage = base + 1 + 1 = base + 2
```

`damageMul` 이 둘 이상이면 곱셈 누적. 합산 → 곱셈 순서는 Modifier와 동일하게 고정 (Spell.md §3).

---

## 6. 조합 hook 처리

`hook=ON_HIT` 의 유물은 `BrickHit` 이벤트마다 발화. 발사체 1개가 5번 충돌하면 5번 발화.
`maxFiresPerCast` / `cooldownTurn` 같은 가드는 schema에 없다 (Trigger와 다름) — 유물의 효과 자체가
폭주를 일으킬 정도면 hook을 ON_KILL이나 ON_TURN_END로 옮기는 게 정상 설계.

예외: `chance < 1.0` 으로 빈도 자연 감소 (파편화 30% 등).

---

## 7. Authoring rules

```
새 유물 추가 시:
[ ] 어느 hook이 가장 자연스러운가?
[ ] condition 으로 표현 가능한가? 안 되면 condition enum 확장 (본 문서 + relic.md 갱신).
[ ] stat-delta vs triggerEffectId — 단순하면 stat-delta, 복잡하면 Effect 위임.
[ ] usesPerRun = 0 이 기본. 강력한 효과만 1회 제한.
[ ] synergyAxis 라벨이 GDD 8.7 표와 일치 / 또는 새 라벨이면 GDD에 추가.
[ ] 로컬라이즈 키 등록.
```
