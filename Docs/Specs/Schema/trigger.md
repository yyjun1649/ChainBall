# Schema — `TTrigger` (Spec class: `SpecTrigger`)

> Trigger는 조건 충족 시 **뒤에 배치된 스펠**(Effect 또는 추가 Projectile)을 발동시킨다.
> 슬롯 시퀀스의 시작 부분에 위치하며, "어떤 이벤트가 일어날 때 효과가 발화하는가"를 정의한다.

| Sheet | Key column | Generated class                      | JSON                                |
|-------|------------|--------------------------------------|-------------------------------------|
| `TTrigger` | `id` (string) | `SpecData.SpecTrigger` (`partial`) | `Json/TTrigger.json`                 |

---

## Columns

| Field            | Type             | Required | Description                                                                | Example         |
|------------------|------------------|----------|----------------------------------------------------------------------------|-----------------|
| `id`             | `string`         | ✅       | 고유 키.                                                                   | `on_hit`        |
| `nameKey`        | `string`         | ✅       | 로컬라이즈.                                                                | `trig.on_hit.name` |
| `descKey`        | `string`         | ✅       | 로컬라이즈.                                                                | `trig.on_hit.desc` |
| `rarity`         | `enum:eRarity`   | ✅       | `COMMON / UNCOMMON / RARE`                                                 | `COMMON`        |
| `event`          | `enum:eTriggerEvent` | ✅   | 발동 이벤트. 아래 enum 참조.                                               | `BRICK_HIT`     |
| `nthCount`       | `int`            | ✅       | `event=NTH_BRICK_HIT` 일 때 N번째. 그 외에는 `0`.                          | `0`             |
| `elementMatch`   | `enum:eElement`  | ✅       | `event=ELEMENT_MATCH` 일 때 매칭할 속성. `NONE` = 사용 안 함.              | `NONE`          |
| `proximityRow`   | `int`            | ✅       | `event=DANGER_PROXIMITY` 일 때 위험 라인으로부터 N줄 이내. 그 외 `0`.      | `0`             |
| `cooldownTurn`   | `int`            | ✅       | 발동 후 N턴 동안 재발동 금지 (한 시퀀스 내 다발동 방지). 보통 `0`.         | `0`             |
| `maxFiresPerCast`| `int`            | ✅       | 한 시전(turn)당 최대 발동 횟수. `0` = 무제한.                              | `0`             |

---

## `eTriggerEvent` (enum)

| Value              | Description                                                              |
|--------------------|--------------------------------------------------------------------------|
| `BRICK_HIT`        | 발사체가 벽돌에 충돌할 때마다.                                            |
| `BRICK_KILL`       | 발사체가 벽돌을 파괴할 때마다.                                            |
| `WALL_BOUNCE`      | 좌우 / 상단 벽 반사 시.                                                   |
| `PROJECTILE_DESPAWN` | 발사체 소멸 시 (바운스 소진 또는 하단 이탈).                            |
| `NTH_BRICK_HIT`    | `nthCount`번째 벽돌 충돌 시에만 (그 외 충돌은 무시).                       |
| `ELEMENT_MATCH`    | 발사체 속성과 충돌 벽돌 속성이 모두 `elementMatch` 일 때.                 |
| `LINE_CLEAR`       | 한 줄(가로 행)의 벽돌이 모두 파괴되었을 때.                                |
| `DANGER_PROXIMITY` | 벽돌이 위험 라인으로부터 `proximityRow` 줄 이내에 존재할 때 (시전 시점). |
| `CONSECUTIVE_HIT`  | 바운스 없이 벽돌 N개 연속 적중 시 (`nthCount` 사용, Gunslinger 연속 명중). |
| `FULL_BOUNCE`      | 발사체가 최대 바운스 횟수 전부 소진했을 때.                                |

---

## Trigger ↔ slot 관계

지팡이 슬롯 시퀀스는 다음 형태:

```
[Trigger] [Projectile] [Modifier]* [Effect]*
```

- 슬롯 시퀀스에 Trigger가 없으면 → **항상 발동** (시퀀스 종료 시점에 Effect들이 발화).
- Trigger가 있으면 → Trigger의 `event` 가 만족된 시점에 **그 뒤 Effect들이 1회 발화**.
- `cooldownTurn` / `maxFiresPerCast` 는 폭주 방지 가드 (예: BRICK_HIT 트리거가 한 번에 50발화하는 것 방지).
- 평가 순서, 다중 트리거 처리, 발화 컨텍스트 (어느 위치, 어느 벽돌)는 `Docs/Systems/Combat.md` 참조.

---

## Authoring checklist

```
[ ] id 는 snake_case 고유키
[ ] event 가 NTH_BRICK_HIT 면 nthCount 채울 것
[ ] event 가 ELEMENT_MATCH 면 elementMatch 채울 것
[ ] event 가 DANGER_PROXIMITY 면 proximityRow 채울 것
[ ] BRICK_HIT 같은 고빈도 트리거는 maxFiresPerCast 로 상한 걸기 검토
```
