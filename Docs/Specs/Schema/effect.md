# Schema — `TEffect` (Spec class: `SpecEffect`)

> Effect는 Trigger 발동 시, 또는 시퀀스 말단에서 실행되는 결과다.
> "무엇이 일어나는가" — 데미지, 상태이상, 회복, 추가 발사체 생성 등.

| Sheet | Key column | Generated class                     | JSON                                |
|-------|------------|-------------------------------------|-------------------------------------|
| `TEffect` | `id` (string) | `SpecData.SpecEffect` (`partial`) | `Json/TEffect.json`                  |

> 주의: 이미 `Generated/SpecEffect.g.cs` 가 존재한다 (현 더미). 본 schema는 ChainBall 도메인을 반영하는
> 새 정의이며, 기존 `SpecEffect` 와의 정합화는 구현 단계에서 결정한다 — 이 문서 단계에서는 도메인 형태에 집중.

---

## Columns

| Field            | Type             | Required | Description                                                                | Example      |
|------------------|------------------|----------|----------------------------------------------------------------------------|--------------|
| `id`             | `string`         | ✅       | 고유 키.                                                                   | `explosion`  |
| `nameKey`        | `string`         | ✅       | 로컬라이즈.                                                                | `eff.explosion.name` |
| `descKey`        | `string`         | ✅       | 로컬라이즈.                                                                | `eff.explosion.desc` |
| `rarity`         | `enum:eRarity`   | ✅       | `COMMON / UNCOMMON / RARE`                                                 | `COMMON`     |
| `kind`           | `enum:eEffectKind` | ✅     | 효과 종류. 아래 enum 참조.                                                  | `AOE_DAMAGE` |
| `damage`         | `int`            | ✅       | 효과 데미지. AoE / 직접 / 전이 등.                                          | `1`          |
| `radius`         | `int`            | ✅       | 영향 반경 (칸). AoE = 주변 N칸. `0` = 적용 안 함.                          | `1`          |
| `targetCount`    | `int`            | ✅       | 영향 대상 수. 연쇄 번개 = `3`, 단일 = `1`.                                 | `1`          |
| `duration`       | `int`            | ✅       | 상태이상 지속 턴. 화상 / 동결 = `2`. 즉발은 `0`.                           | `0`          |
| `tickDamage`     | `int`            | ✅       | 상태이상 턴당 데미지 (DOT). 화상 = `1`.                                    | `0`          |
| `element`        | `enum:eElement`  | ✅       | 효과 자체의 속성. 보통 `NONE` (Modifier에서 부여한 element가 우선).        | `NONE`       |
| `healAmount`     | `int`            | ✅       | 플레이어 HP 회복량. 흡수 = `1`. `0` = 회복 없음.                           | `0`          |
| `goldAmount`     | `int`            | ✅       | 추가 골드. 골드 러시 = `1`. `0` = 없음.                                    | `0`          |
| `spawnHitInstance`| `int`           | ✅       | 효과로 HitInstance 추가 생성 시 그 인스턴스 `THitInstance.id`. 빙결 파편 등. `0` = 생성 없음. | `1500`       |
| `spawnCount`     | `int`            | ✅       | `spawnHitInstance > 0` 시 생성 개수. 빙결 파편 = `4`.                       | `0`          |
| `extraCast`      | `int`            | ✅       | 다음 턴 추가 시전 횟수 (탄환 환수 = `1`, 속사 = `1` 조건부).                | `0`          |
| `vfxKey`         | `string`         | ⭕       | Resource Handler 키. 비어있으면 기본.                                       | `vfx/eff/explosion` |
| `sfxKey`         | `string`         | ⭕       | Sound Handler 키. 비어있으면 기본.                                          | `sfx/eff/explosion` |

---

## `eEffectKind` (enum)

| Value              | Meaning                                                                  |
|--------------------|--------------------------------------------------------------------------|
| `DAMAGE_DIRECT`    | 단일 대상 직접 데미지.                                                    |
| `AOE_DAMAGE`       | 영역 데미지 (`radius` 사용).                                              |
| `LINE_DAMAGE`      | 1줄 전체 데미지 (충격파).                                                 |
| `CHAIN_DAMAGE`     | 인접 벽돌 `targetCount` 개로 전이 (연쇄 번개).                             |
| `STATUS_BURN`      | 화상 부여 (`duration`, `tickDamage`).                                     |
| `STATUS_FREEZE`    | 동결 (`duration`). 하강 정지.                                             |
| `HEAL`             | 플레이어 HP 회복 (`healAmount`).                                          |
| `GOLD_GAIN`        | 골드 추가 (`goldAmount`).                                                 |
| `SPAWN_HIT_INSTANCE` | HitInstance 추가 생성 (`spawnHitInstance`, `spawnCount`). kind는 참조 행에 따름 (MOVING / INSTANT / AURA 모두 허용). |
| `EXTRA_CAST`       | 다음 턴 시전 +N (`extraCast`).                                            |
| `OVERKILL_TRANSFER`| 초과 데미지를 인접 벽돌에 전이 (오버킬).                                  |
| `HALF_HP_REMOVE`   | 적중 벽돌 HP 절반 제거 (파쇄).                                            |
| `GRAVITY_PULL`     | 주변 N칸 벽돌을 1칸 중앙으로 당김 (중력장).                                |
| `BURN_DETONATE`    | 화상 상태 벽돌 적중 시 화상 소모 + `damage` (연소 폭발).                  |
| `EMPOWER_NEXT`     | 다음 턴 첫 발사체 데미지 +N (과부하). `damage` = N의 베이스값.             |

---

## Trigger ↔ Effect 관계

- Trigger가 없는 시퀀스 → 시퀀스 끝에 Effect들이 발화 (시전 1회당 1발화).
- Trigger가 있는 시퀀스 → Trigger 발동 컨텍스트(충돌 위치, 대상 벽돌 등)를 Effect가 사용.
- 다중 Effect → 슬롯 순서대로 모두 실행.
- Effect 실행 결과로 새 발사체 / 추가 시전이 생성될 수 있다 (재귀 가능). 무한 재귀 방지는
  `Docs/Systems/Combat.md` 의 cap 규칙으로.

---

## Authoring checklist

```
[ ] id 는 snake_case 고유키
[ ] kind 가 STATUS_* 면 duration / tickDamage 채울 것
[ ] kind 가 SPAWN_HIT_INSTANCE 이면 spawnHitInstance id가 THitInstance 에 존재해야 함
[ ] kind 가 AOE_DAMAGE 면 radius > 0
[ ] healAmount / goldAmount / damage 등 숫자 컬럼은 0 = 미적용 의미를 유지
```
