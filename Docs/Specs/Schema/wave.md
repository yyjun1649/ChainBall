# Schema — `TWave` (Spec class: `SpecWave`)

> 웨이브 / 스테이지의 줄 생성 패턴. 매 `TurnPhase.ENEMY` 마다 1행 생성에 사용된다.
> 한 웨이브는 *복수 행* 을 가지며, `lineIndex` 오름차순으로 enqueue.

| Sheet | Key column          | Generated class                    | JSON                            |
|-------|---------------------|------------------------------------|---------------------------------|
| `TWave` | (`waveId`, `lineIndex`) 복합 — 실제로는 `id`(int) 또는 `string` 단일 키 | `SpecData.SpecWave` (`partial`) | `Json/TWave.json` |

> 단일 키 시트라 `id` 컬럼을 별도로 두고 `(waveId, lineIndex)` 는 데이터 컬럼으로 — 검색은
> `SpecDataManager.SpecWave.All.Where(...)` 사용 (Phase 2 / Phase 9에서 헬퍼 partial 추가 권장).

---

## Columns

| Field         | Type        | Required | Description                                                                  | Example              |
|---------------|-------------|----------|------------------------------------------------------------------------------|----------------------|
| `id`          | `int`       | ✅       | 행의 고유 키.                                                                | `10101`              |
| `waveId`      | `string`    | ✅       | 웨이브 식별자. 액트 / 노드 / 보스별로 고유.                                   | `act1_normal_01`     |
| `lineIndex`   | `int`       | ✅       | 해당 웨이브 내 N번째 줄 (0-based). 매 `TurnPhase.ENEMY` 마다 +1 진행.        | `0`                  |
| `pattern`     | `string[]`  | ✅       | 가로 8칸. 각 셀에 `brickTypeId` 또는 `"."`(빈 칸). 배열 구분자 `/`.            | `./N/N/S(F)/N/./E/.` |
| `bossLine`    | `bool`      | ✅       | 보스 패턴이 코드로 결정하는 줄인지 표시. `true` 면 `pattern` 무시 + 코드 호출. | `false`              |
| `bossPattern` | `enum:eBossPattern` | ✅ | `bossLine=true` 시 어느 패턴 메서드를 호출할지. 그 외 `NONE`.               | `NONE`               |

### `pattern` 셀 표기법

| 표기      | 의미                                          |
|-----------|-----------------------------------------------|
| `.`       | 빈 칸                                          |
| `N`       | NORMAL 벽돌 (HP 1)                             |
| `N(2)`    | NORMAL 벽돌 (HP 2)                             |
| `S(F)`    | SHIELD 벽돌 (element=FIRE)                     |
| `S(I)`    | SHIELD 벽돌 (element=ICE)                      |
| `S(E)`    | SHIELD 벽돌 (element=SHOCK / electricity)      |
| `E`       | EXPLOSIVE 벽돌                                 |
| `P`       | SPAWNER 벽돌                                   |
| `R`       | REFLECTOR 벽돌                                 |
| `B[id]`   | BOSS 벽돌 (id로 보스 정의 참조)                 |

권장: 표기법은 `BrickPatternParser` 한 곳에 캡슐화. 새 표기 추가 시 본 표 갱신 + 파서 갱신.

---

## `eBossPattern` (enum)

보스 줄 생성은 데이터로 표현하기 어려운 동적 동작이 많아, `bossLine=true` 시 코드 메서드로 분기.

| Value     | 코드 진입점 (예시)                              |
|-----------|------------------------------------------------|
| `NONE`    | (보스 줄 아님)                                  |
| `BOSS_01_OPENING`     | `BossPatternRunner.Boss01.Opening(field)`        |
| `BOSS_01_RAGE`        | `BossPatternRunner.Boss01.Rage(field)`           |
| `BOSS_01_TELEGRAPH`   | `BossPatternRunner.Boss01.Telegraph(field)`      |
| ...                    | (보스마다 enum 값 추가, 코드에서 매핑 메서드 작성) |

새 보스 패턴 추가:
1. `eBossPattern` 에 enum 값 추가.
2. `BossPatternRunner` (또는 보스별 클래스) 에 메서드 추가.
3. `TWave` 시트에 `bossLine=true` 행 추가하고 `bossPattern` 컬럼에 enum 값 지정.

---

## 사용 흐름

### 일반 웨이브
```
TurnRunner.TurnPhase.ENEMY 진입:
  var line = SpecDataManager.SpecWave.All
              .Where(w => w.waveId == currentWaveId)
              .OrderBy(w => w.lineIndex)
              .ElementAtOrDefault(currentLineIndex);
  if (line == null) → 웨이브 종료, 잔여 벽돌 처리만
  else if (line.bossLine) → BossPatternRunner.Run(line.bossPattern, field)
  else → BrickPatternParser.SpawnRow(line.pattern, field)
  currentLineIndex++
```

### 보스 웨이브
보스의 시그니처는 *코드*에 있고, `TWave` 는 *언제 어느 패턴을 호출할지*만 시퀀싱. 보스 웨이브 한 행 예시:

| id     | waveId          | lineIndex | pattern | bossLine | bossPattern         |
|--------|-----------------|-----------|---------|----------|---------------------|
| 90001  | `act1_boss_01`  | 0         | (무시)  | `true`   | `BOSS_01_OPENING`   |
| 90002  | `act1_boss_01`  | 1         | (무시)  | `true`   | `BOSS_01_TELEGRAPH` |
| 90003  | `act1_boss_01`  | 2         | (무시)  | `true`   | `BOSS_01_RAGE`      |

---

## Cross-references

- `SpecBrick` (선택, 신규 schema 후속) — 표기 `N(2)` / `S(F)` 같은 변형이 늘어나면 별도 시트로 분리 가능.
- 줄 생성 흐름 / SPAWNER 처리 / 위험 라인 도달은 `Docs/Systems/Brick.md` §4.
- 보스 패턴 코드는 `Docs/Systems/Meta.md` §6.

---

## Authoring checklist

```
[ ] id 는 정수 고유키
[ ] waveId 는 액트/노드/보스를 명확히 식별 (예: act1_elite_03, act1_boss_01)
[ ] lineIndex 는 0부터 연속 (gap 금지)
[ ] pattern 의 셀 수가 정확히 8 (BrickPatternParser가 길이 검증)
[ ] bossLine=true 면 bossPattern 이 NONE 이 아님
[ ] bossPattern 의 enum 값이 BossPatternRunner 에 매핑됨
```
