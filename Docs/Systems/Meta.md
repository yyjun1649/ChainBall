# Systems — Meta Progression

> 액트 / 노드맵 / 보상 흐름. Slay the Spire 구조를 차용. 본 문서는 *규칙*만 — 노드 수치, 보상 확률은 SpecData에.

---

## 1. Run lifecycle

```
TitleScene (캐릭터 / 시작 지팡이 선택)
   │
   ▼
GameScene 진입
   │
   ▼
Act 1 → Act 2 → Act 3 → Final Boss
   │
   ▼
승리 화면 / 패배 화면 → TitleScene
```

- 한 런 단위는 `RunState` 객체. 캐릭터, 지팡이, 슬롯 스펠, 유물, HP, 골드, 클리어한 노드 등을 담는다.
- 패배 시 RunState는 폐기 (Slay the Spire와 동일).

---

## 2. Act / Map 구조

각 Act는 **노드맵**으로 구성된다.

```
Act = {
    int    actIndex   (0..2 + boss)
    Node[] nodes
    Edge[] edges      // 노드 간 연결
    NodeId start
    NodeId end (= 보스 노드)
}
```

- 노드 수: 액트당 12~15 (참고; 실제 값은 stage 데이터에서).
- 시각적으로는 StS 식 트리/그래프 — 한 노드에서 다음 행으로 1~3개 분기.
- 플레이어는 한 노드를 클리어하면 `edges` 로 연결된 다음 노드 중 하나를 선택.

세부 맵 생성 알고리즘 (랜덤 vs pre-baked)은 별도 문서 — 본 v0.1은 **고정 맵 권장** (검증 단순화).

---

## 3. Node types

| Node kind        | Behavior on entry                                              |
|------------------|----------------------------------------------------------------|
| `BATTLE_NORMAL`  | 일반 전투. 5~7턴 예상.                                         |
| `BATTLE_ELITE`   | 엘리트 전투. 8~12턴. 강한 벽돌 / 특수 조건.                     |
| `BATTLE_BOSS`    | 보스 전투. 12~15턴. 액트 종결.                                  |
| `EVENT`          | 선택지 기반 이벤트 (조건부 유물 / 스펠 / HP 변동).              |
| `SHOP`           | 스펠 / 유물 구매, 슬롯 추가, 스펠 제거, HP 회복.                |
| `REST`           | HP 회복 또는 스펠 업그레이드 택 1.                              |

각 node kind는 `eNodeKind` enum + 별도 `TNode*` 데이터 시트(권장).

---

## 4. Reward flow

```
전투 클리어 후:
1. 골드 지급 (Battle 종류에 따라 차등)
2. 카드 / 유물 보상 선택 UI
3. 선택 후 다음 노드로
```

| Node kind        | Reward                                                          |
|------------------|-----------------------------------------------------------------|
| `BATTLE_NORMAL`  | 골드 + 스펠 3개 중 1개 선택                                      |
| `BATTLE_ELITE`   | 레어 스펠 + 유물 2개 중 1개 선택                                 |
| `BATTLE_BOSS`    | 골드 + 유물 3개 중 1개 선택                                      |
| `EVENT`          | 이벤트 결과에 따름                                              |
| `SHOP`           | 골드 소비 — 보상 형태 아님                                       |
| `REST`           | HP +N 또는 스펠 업그레이드 (택 1)                               |

보상의 **rarity 분포**는 RunState 의 액트 진행도에 따라 동적 — Act 1은 Common 비율 높고, Act 3는 Rare 등장률 증가. 구체 곡선은 `TRewardWeight` 시트에 둔다 (별도 schema 후속).

---

## 5. Run-state immutable interactions

`RunState` 의 변경은 다음 시점에서만 일어난다:

| Event                   | RunState changes                                  |
|-------------------------|---------------------------------------------------|
| `BattleStart`           | 전투 시작 (변경 없음, 컨텍스트만 push)             |
| `BattleEnd(victory)`    | gold += reward, ON_BATTLE_END relic hook          |
| `RewardSelected`        | spells / relics 추가                              |
| `ShopPurchase`          | gold -= cost, item 추가                           |
| `EventChoice`           | 이벤트 정의에 따라 임의 변동                       |
| `BattleEnd(defeat)`     | RunState 폐기 (런 종료)                           |

전투 안에서의 임시 상태 (이번 시전 BricksKilledCount 등)는 RunState 가 아니라 BattleState (전투 단위
ephemeral object) 에 저장.

---

## 6. Boss 패턴 — enum + 코드 (확정)

보스의 줄 생성은 코드로 결정한다. 데이터(`TWave`)에는 **언제 어느 패턴을 호출할지**만 시퀀싱.

```
eBossPattern (enum) — Specs/Schema/wave.md 의 #enum 시트에 등록
  NONE
  BOSS_01_OPENING
  BOSS_01_TELEGRAPH
  BOSS_01_RAGE
  ...

코드:
  static class BossPatternRunner {
      public static class Boss01 {
          public static void Opening(BrickField field)   { /* 줄 생성 + 보스 등장 */ }
          public static void Telegraph(BrickField field) { /* 예고 */ }
          public static void Rage(BrickField field)      { /* 공격 패턴 */ }
      }
  }

  void Run(eBossPattern pattern, BrickField field) {
      switch (pattern) {
          case eBossPattern.BOSS_01_OPENING:   BossPatternRunner.Boss01.Opening(field);   break;
          case eBossPattern.BOSS_01_TELEGRAPH: BossPatternRunner.Boss01.Telegraph(field); break;
          case eBossPattern.BOSS_01_RAGE:      BossPatternRunner.Boss01.Rage(field);      break;
          ...
      }
  }
```

새 보스 추가:
1. `eBossPattern` 에 enum 값 추가 (예: `BOSS_02_OPENING`).
2. `BossPatternRunner.Boss02.*` 메서드 작성.
3. `Run` 의 switch에 분기 추가.
4. `TWave` 에 `act2_boss_01` 같은 waveId + `bossLine=true` + `bossPattern=BOSS_02_OPENING` 행 추가.

---

## 7. Open questions

1. 노드맵 생성 방식 (절차 생성 vs 고정맵). v0.1은 고정맵 권장.
2. 이벤트(EVENT 노드) 정의 schema — `TEvent` 시트로 분리 예정.
