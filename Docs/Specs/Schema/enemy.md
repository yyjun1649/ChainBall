# Schema — `SpecEnemy` (Spec class: `SpecEnemy`)

> Brick / Boss 등 **웨이브에서 스폰되는 적 유닛** 정의 테이블. Player / playable 캐릭터는 `SpecCharacter` 에서 따로 정의 (`Schema/character.md` 참고).
> Enemy 는 메타게임 진행상태(unlock / 풀 / 패시브) 가 없으므로 SpecCharacter 와 분리되어 있다.

| Sheet         | Key column    | Generated class                       | JSON                      |
|---------------|---------------|---------------------------------------|---------------------------|
| `SpecEnemy`   | `id` (string) | `SpecData.SpecEnemy` (`partial`)      | `Json/SpecEnemy.json`     |

---

## Columns

| Field          | Type     | Required | Description                                                                                  | Example                |
|----------------|----------|----------|----------------------------------------------------------------------------------------------|------------------------|
| `id`           | `string` | ✅       | 고유 키. Brick parser 는 `brick_{eBrickType}`, `brick_shield_{eElement}`, `boss_{bossId}` 형식을 lookup. | `brick_NORMAL`         |
| `nameKey`      | `string` | ✅       | 로컬라이즈 키.                                                                                | `enemy.brick_normal.name` |
| `descKey`      | `string` | ✅       | 로컬라이즈 키.                                                                                | `enemy.brick_normal.desc` |
| `controllerId` | `int`    | ✅       | `UnitController` 풀에서 `UnitController_{id}` Addressable 을 로드.                            | `1`                    |
| `viewId`       | `int`    | ✅       | `UnitView` 풀에서 `UnitView_{id}` Addressable 을 로드.                                        | `1`                    |
| `startHp`      | `int`    | ✅       | 시작 HP. Brick cell 에 `N(2)` 처럼 override 가 들어오면 그 값이 우선.                          | `1`                    |
| `iconKey`      | `string` | ⭕       | UI 아이콘 (Codex / 도감 등).                                                                   | `ui/enemy/brick_normal` |

---

## id 명명 규약 (UnitSpawnHandler 와 동기)

`UnitSpawnHandler.ResolveEnemyId(BrickPatternParser.ParsedCell)` 가 lookup 키를 생성한다:

| Cell 표기 | eBrickType | eElement | 생성되는 id           |
|-----------|------------|----------|------------------------|
| `N`       | NORMAL     | NONE     | `brick_NORMAL`         |
| `S(F)`    | SHIELD     | FIRE     | `brick_shield_FIRE`    |
| `S(I)`    | SHIELD     | ICE      | `brick_shield_ICE`     |
| `S(E)`    | SHIELD     | SHOCK    | `brick_shield_SHOCK`   |
| `E`       | EXPLOSIVE  | NONE     | `brick_EXPLOSIVE`      |
| `P`       | SPAWNER    | NONE     | `brick_SPAWNER`        |
| `R`       | REFLECTOR  | NONE     | `brick_REFLECTOR`      |
| `B[1]`    | BOSS       | —        | `boss_1`               |

> enum `ToString()` 결과를 그대로 쓰므로 대문자 보존 필수. 시트의 `id` 가 한 글자라도 다르면 spawn 시 missing 로그가 뜬다.

---

## Authoring checklist

```
[ ] id 가 위 명명 규약에 정확히 일치 (대문자 / underscore)
[ ] controllerId 가 등록된 Addressable "UnitController_{id}" 와 매칭
[ ] viewId 가 등록된 Addressable "UnitView_{id}" 와 매칭
[ ] startHp > 0
```
