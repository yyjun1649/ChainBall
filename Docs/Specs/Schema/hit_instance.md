# Schema — `SpecHitInstance` (Spec class: `SpecHitInstance`)

> 공격 인스턴스의 정의. **MovingHit (투사체) / InstantHit (근접·범위 즉발) / AuraHit (지속 영역)** 세 종류가
> 한 테이블을 공유한다. `kind` 컬럼이 어느 코드 풀(MovingHit/InstantHit/AuraHit)에서 인스턴스를 꺼낼지 결정한다.
>
> ChainBall의 **Weapon 슬롯 카테고리 "Projectile"** 은 `kind=MOVING` 인 SpecHitInstance 행만 받는다.
> Effect의 `SPAWN_HIT_INSTANCE` (구 `SPAWN_PROJECTILE`)는 임의의 kind를 받을 수 있다.

| Sheet             | Key column | Generated class                        | JSON                              |
|-------------------|------------|----------------------------------------|-----------------------------------|
| `SpecHitInstance` | `id` (int) | `SpecData.SpecHitInstance` (`partial`) | `Json/SpecHitInstance.json`       |

---

## Columns

| Field             | Type             | Required | Description                                                                  | Example       |
|-------------------|------------------|----------|------------------------------------------------------------------------------|---------------|
| `id`              | `int`            | ✅       | 고유 키 (정수). MovingHit / InstantHit / AuraHit 풀 모두 같은 키 공간을 공유. | `1001`        |
| `kind`            | `enum:eHitInstanceKind` | ✅ | `MOVING / INSTANT / AURA`. 코드 풀 분기.                                    | `MOVING`      |
| `nameKey`         | `string`         | ✅       | 로컬라이즈 (UI에서 사용 시).                                                  | `proj.magic_ball.name` |
| `descKey`         | `string`         | ✅       | 로컬라이즈.                                                                   | `proj.magic_ball.desc` |
| `rarity`          | `enum:eRarity`   | ✅       | `COMMON / UNCOMMON / RARE`. Weapon 슬롯에 직접 꽂히는 경우(`kind=MOVING`)에 의미. | `COMMON`     |
| `motion`          | `enum:eProjectileMotion` | ✅ | `kind=MOVING` 일 때만 의미. 그 외엔 `STRAIGHT` (무시).                       | `REFLECT`     |
| `baseDamage`      | `float`          | ✅       | 충돌당 데미지. `0` = 데미지 없음 (예: 바운스볼).                              | `1`           |
| `basePercent`     | `float`          | ✅       | 데미지 배수 베이스. 보통 `1.0`.                                               | `1.0`         |
| `effects`         | `int[]`          | ✅       | 충돌 시 적용할 SpecEffect.id 리스트. 배열 구분자 `/`. 비어있으면 `[]`.        | `1001/1002`   |
| `range`           | `float`          | ✅       | 코드 IDamageSpec 호환. `kind=INSTANT/AURA` 일 때 Shape 반경, `MOVING` 일 때 0 가능. | `1.5`   |
| `moveSpeed`       | `float`          | ✅       | `kind=MOVING` 시 비행 속도. 그 외 `0`.                                       | `12.0`        |
| `lifeTime`        | `float`          | ✅       | 발사체 / Aura 수명(초). `kind=INSTANT` + `Duration=0` 형태면 `0`.            | `4.0`         |
| `hitCount`        | `int`            | ✅       | 동일 인스턴스가 같은 대상을 N번까지 적중 가능한지. 보통 `1`.                  | `1`           |
| `bounceCount`     | `int`            | ✅       | 최대 벽/벽돌 반사. `kind=MOVING` 전용. 그 외 `0`.                            | `5`           |
| `pierceCount`     | `int`            | ✅       | 관통 가능 벽돌 수. `kind=MOVING` 전용.                                        | `0`           |
| `passThrough`     | `int`            | ✅       | 데미지 없이 통과 가능한 벽돌 수. 유령볼 등.                                   | `0`           |
| `homing`          | `bool`           | ✅       | 가장 가까운 대상으로 유도. `kind=MOVING` 시 `HomingBehavior` 자동 부착.       | `false`       |
| `randomAngle`     | `float`          | ✅       | 발사각 ±N° 랜덤. `0` = 정확.                                                  | `0`           |
| `hitWidth`        | `int`            | ✅       | 충돌 판정 가로 칸 수. 캐논볼 = `2`. `kind=MOVING` 의미. 그 외엔 Shape 폭.    | `1`           |
| `multiShot`       | `int`            | ✅       | 시전 시 동시 발사되는 발사체 수. 더블샷 = `2`.                                | `1`           |
| `spreadAngle`     | `float`          | ✅       | `multiShot > 1` 일 때 부채꼴 총 각도. 좁은 부채꼴 = `10`.                     | `0`           |
| `staticAfterFire` | `bool`           | ✅       | 발사 후 고정. 플라즈마 코어 = `true`. `kind=MOVING` 의미.                    | `false`       |
| `tickInterval`    | `float`          | ✅       | `kind=AURA` 시 데미지 재적용 주기(초). 그 외 `0`.                            | `0.5`         |
| `vfxKey`          | `string`         | ⭕       | Resource Handler 키.                                                         | `vfx/proj/spark` |
| `sfxKey`          | `string`         | ⭕       | Sound Handler 키.                                                            | `sfx/cast/spark` |

`⭕` = 빈 값 허용.

---

## `eHitInstanceKind` (enum)

`SpellSequence` (Weapon 시전) / `Skill` (자율 시전) 이 어느 코드 풀에서 인스턴스를 꺼낼지 결정.

| Value     | Pool          | 용도                                              |
|-----------|---------------|---------------------------------------------------|
| `MOVING`  | `MovingHit`   | 투사체. `motion`, `bounceCount`, `pierceCount` 등. |
| `INSTANT` | `InstantHit`  | 근접 / 즉발 범위. `range` = Shape 반경.            |
| `AURA`    | `AuraHit`     | 지속 영역. `tickInterval` 사용.                    |

---

## `eProjectileMotion` (enum, `kind=MOVING` 전용)

| Value      | Behavior                                                          |
|------------|-------------------------------------------------------------------|
| `REFLECT`  | 표준 반사. `bounceCount` 만큼 튕긴다.                              |
| `STRAIGHT` | 직선. 반사 없음. 화면 이탈 시 소멸.                                |
| `HOMING`   | 가장 가까운 벽돌로 유도 (`homing=true` 와 함께).                   |
| `CURVE`    | 발사 후 1회 꺾임.                                                  |
| `STATIC`   | 발사 위치에 고정. `staticAfterFire=true` 와 함께.                  |
| `FALLING`  | 충돌 시 정지·낙하 (헤비볼).                                        |

---

## 필드 그룹 — kind 별 의미

| Field             | MOVING | INSTANT | AURA |
|-------------------|--------|---------|------|
| `motion`          | ✅     | —       | —    |
| `moveSpeed`       | ✅     | —       | —    |
| `bounceCount`     | ✅     | —       | —    |
| `pierceCount`     | ✅     | —       | —    |
| `passThrough`     | ✅     | —       | —    |
| `homing`          | ✅     | —       | —    |
| `randomAngle`     | ✅     | —       | —    |
| `hitWidth`        | ✅     | —       | —    |
| `multiShot`       | ✅     | —       | —    |
| `spreadAngle`     | ✅     | —       | —    |
| `staticAfterFire` | ✅     | —       | —    |
| `range`           | (작은 hit radius) | ✅ Shape 반경 | ✅ Shape 반경 |
| `lifeTime`        | ✅ 비행 수명 | ✅ Duration (0=1프레임) | ✅ 지속 시간 |
| `tickInterval`    | —      | (지속형 InstantHit 시 재쿼리 간격) | ✅ |
| `hitCount`        | ✅     | ✅      | ✅   |
| `effects`         | ✅     | ✅      | ✅   |

---

## Cross-references

- 코드 측 `SpecHitInstance.g.cs` 의 더미 필드(`id`, `moveSpeed`, `lifeTime`, `hitCount`)는 본 schema로 확장된다.
- Weapon 슬롯에서 Projectile 카테고리는 `kind=MOVING` 만 허용.
- `SpecEffect.kind=SPAWN_HIT_INSTANCE` 가 임의 kind를 spawn (자세한 Effect 동작은 Schema/effect.md).
- 시전 평가 흐름은 `Docs/Systems/Combat.md` §2.
- 코드 측 인스턴스 풀 키 형식: `HitInstance_{id}` (`[PoolAddress("HitInstance_{0}")]` on `HitInstance<T>`, see `Game/Hit/HitInstance.cs`).

---

## v0.1 결정 — `damageType` / `attackType` 컬럼 제외 (Option A, 2026-04-26)

`Define.cs` 의 기존 `eDamageType` (`Melee/Magic/Heal`) / `eAttackType` (`Normal`) 와 본 schema 상의
대문자 스네이크 (`MELEE/RANGED/MAGIC`, `NORMAL/SKILL/DOT`) 가 충돌. v0.1 에서는 두 컬럼을 시트에서
**제외** 하고, `SpecHitInstance → DamageInfo` 어댑터에서 다음 고정값을 사용한다:

```csharp
damageType = eDamageType.Magic;   // ChainBall 발사체는 v0.1 모두 마법 계열로 처리
attackType = eAttackType.Normal;  // 코드 enum의 유일 값
```

향후 캐릭터 다양화 / DOT Effect 추가 시점에 두 enum을 `#enum` 시트로 이전하고 `Define.cs` enum
정리(또는 alias) 후 본 schema에 두 컬럼을 다시 추가한다 — 그 때 별도 `/arch-update`.

---

## Authoring checklist

```
[ ] id 는 정수 고유키 (다른 kind와도 키 공간 공유 — 중복 금지)
[ ] kind 가 MOVING 이면 motion / moveSpeed / bounceCount 등 채울 것
[ ] kind 가 INSTANT 면 range (= Shape 반경) + lifeTime (Duration) 채울 것
[ ] kind 가 AURA 면 range + lifeTime + tickInterval 채울 것
[ ] effects 의 모든 id가 SpecEffect 에 존재
[ ] damageType / attackType 이 enum 정의에 있는 값
[ ] vfxKey / sfxKey 의 Resource 키가 Addressable 에 등록
```
