# Implementation Roadmap — ChainBall

> 코드 작성 *순서*. 각 Phase는 의존성(앞 Phase의 결과)을 전제하며, 끝날 때마다 **검증 가능한 게임 상태**가 남는다.
> Phase 1~9 = Prototype (GDD §11.1 — 1액트 + 보스, 10~15분 런).
> Phase 10~12 = Alpha 진입 준비.

---

## 출발점 (이미 완료된 것)

GDD 도메인을 *얹기 위한* 6-레이어 컨트랙트는 코드에 이미 존재한다:

- `Unit / Stat / Effect / Damage / HitInstance` 5개 레이어 (UnitCombatDesign §1~7).
- `Handlers.Pool` 통합 풀 (`MovingHit`, `InstantHit`, `AuraHit` 모두 `PoolMonoBehaviour<T>` 위에서 동작).
- SpecData 파이프라인 (`xlsx → *.g.cs + *.json → SpecDataManager`).
- `Skill : DamageActionBase<SpecSkill>` + `SkillFactory` + `HitLauncher.FireProjectile` 단축 진입점.

GDD에서 *추가로* 필요한 것:
- 도메인 데이터 (스펠/유물/캐릭터/무기 시트).
- 슬롯 시퀀스 평가기 (`SpellSequence`, `SnapshotPatch`, `TriggerWatcher`).
- Brick / Field / Turn lifecycle.
- Player의 Run / Meta / Node.
- ChainBall 전용 IEffect / IHitBehavior 구현체.

---

## Phase 0 — 설계 잔존 결정 (확정 ✅)

| 질문 | 결정 | 반영 위치 |
|---|---|---|
| 상태이상(BURN/FREEZE) 데이터 모델 | **기존 `IEffect` / `EffectHost` 시스템 재사용**. `BurnEffect : IDurationEffect, ITickEffect`, `FreezeEffect : IDurationEffect`. `brick.Data.Effects.Add(effect)` 로 적용. Brick은 `UnitController` 파생. | Brick.md §1 |
| 시간 감속(`speedMul=0.5`) 구현 | **HitSnapshot 단순 곱셈 패치** (`snap.Speed *= 0.5`). 글로벌 슬로우모션은 v0.1 미사용 — 향후 필요 시 `Handlers.Time.SetTimeScale` 도입. | Combat.md §7 |
| 줄 생성 데이터 schema | **`SpecWave` (테이블명 `TWave`)** — 컬럼: `id, waveId, lineIndex, pattern (string[8]), bossLine, bossPattern (eBossPattern enum)`. | Specs/Schema/wave.md (신규) |
| Trigger 카운터 castsPerTurn=2 시 동작 | **각 시전마다 리셋**. | Combat.md §7 |
| 보스 줄 생성 패턴 정의 | **`eBossPattern` enum + 코드 (`BossPatternRunner.BossXX.*`)**. SpecData (`TWave`)는 enum 값만 시퀀싱. | Meta.md §6 |

---

## Phase 1 — Spec 데이터 기반 (1~2일, 디자이너 협업)

**목표**: 모든 도메인 spec 시트를 xlsx에 작성하고 codegen이 통과하는 상태.

### 산출물
- [ ] `Spec.xlsx` 에 시트 추가 (시트명 컨벤션: `Spec*`, 2026-04-26 결정):
  - `SpecHitInstance` (kind enum 컬럼 + ChainBall 도메인 필드, v0.1은 `damageType/attackType` 제외 — Option A)
  - `SpecModifier`, `SpecTrigger`, `SpecEffect`, `SpecRelic`, `SpecWeapon`, `SpecCharacter`, `SpecWave`
- [x] `#enum` 시트 확장 — 13종: `eHitInstanceKind, eProjectileMotion, eRarity, eElement, eModifierBehavior, eTriggerEvent, eEffectKind, eRelicCategory, eRelicHook, eRelicCondition, eSlotKind, eUnlockCondition, eBossPattern` (2026-04-26 완료, `Docs/Specs/Schema/_enums.md` 권위 문서).
- [ ] `Tools > SpecData > Rebuild All` 무에러 통과 — `Generated/SpecHitInstance.g.cs` 등이 schema 컬럼대로 생성됨.
- [ ] `SpecDataManager.Tables.g.cs` 자동 와이어링 검증 (CodeGenerator.WriteManagerTables 가 8개 테이블 모두 프로퍼티 + LoadAddressable 생성).
- [ ] `SpecLocalize` 에 모든 nameKey/descKey 등록.

### 검증
- 런타임에서 `SpecDataManager.SpecHitInstance.Get(1001)` 등 호출이 데이터 반환.
- `SpecDataValidator` 가 enum 참조를 모두 통과 (Console 빨간 로그 없음).

### Blocker
- Phase 0 의 결정이 schema 컬럼에 반영되어야 함.
- 디자이너가 Phase 0 스펠/유물 24종 + 캐릭터 1 + 무기 3 데이터 채움.

---

## Phase 2 — Field & Brick & Turn skeleton (3~4일)

**목표**: 빈 화면에서 벽돌이 매 턴 1줄 하강하고, 위험 라인 도달 시 플레이어 HP가 깎이는 상태. *공격은 아직 없음.*

### 산출물
- [ ] `Brick : UnitController` (얇은 파생) — `Data.Stats` 가 HP, `Data.Effects` 가 상태이상 host. `eBrickType` 필드, `eElement` 필드. Layer = `EnemyLayer`.
- [ ] `BrickField` (씬 매니저) — 8×15 그리드, `Brick[,] _cells`. `AddRowFromPattern(string[] pattern)`, `ShiftAllDown()` (FREEZE 검사 포함), `IsEmpty()`.
- [ ] `BrickPatternParser` — `string[8] pattern` (`./N/N(2)/S(F)/...`) → `Brick` 인스턴스 spawn. wave.md 표기법 권위.
- [ ] `TurnRunner` — `TurnPhase` enum 진행 (CAST는 아직 빈 구현). `OnTurnPhaseChanged` 이벤트. `UPKEEP` 에서 모든 Brick의 `Effects.Tick(1f)` 호출 (턴 단위 dt).
- [ ] `Player` 임시 컴포넌트 — HP만. 위험 라인 도달 시 `DealDamage` 또는 직접 `Player.HP -= dangerDamage * count`.
- [ ] `BrickFactory` / `BrickPool` — `Handlers.Pool` 사용 (Brick prefab 등록 필요 — Editor 작업).
- [ ] 디버그 입력: 스페이스바 = 다음 턴.

### 검증
- 플레이가능한 데모: 매 스페이스바마다 한 줄 새로 생성 + 모두 한 줄 하강 + 14행 도달 시 Player HP 감소 + HP 0 시 패배 화면.
- Brick은 UnitController라 향후 DamagePipeline 자연 통합.

### Depends on
- Phase 1 (Spec 없으면 BrickPattern 데이터 못 만듦. 단, 임시 하드코딩 가능 — 디자이너 시트 진행 중일 때).

### 결정 필요
- Brick prefab 1종 vs type별 5종 — 권장: 1종 + 컴포넌트가 type을 보고 시각 요소 토글. 풀 키 단순화.

---

## Phase 3 — SpellSequence + SnapshotPatch + TriggerWatcher (3~5일)

**목표**: 슬롯 4종(Projectile + Modifier + Trigger + Effect)을 받아 *코드로* 구성된 시퀀스가 실제로 발사체를 쏘고 벽돌 데미지가 들어가는 상태.

### 산출물
- [ ] `SnapshotPatch` (struct or class):
  - 누적 메서드: `Apply(SpecModifier)` — stat-delta 합산, behavior 리스트 추가.
  - 출력 메서드: `BuildSnapshot(HitSnapshot baseSnap) → HitSnapshot` — 합산/곱셈 순서 고정 (Spell.md §3 / §7).
- [ ] `TriggerWatcher : IDisposable` — 한 시전 단위, HitInstance에 OnHit/OnDespawn/OnTickFrame 구독. `SpecTrigger.event` 매칭 시 `EffectFactory.Create(SpecEffect)` 로 Effect 발화.
- [ ] `SpellSequence : PooledDisposable`
  - `Initialize(SpecHitInstance proj, List<SpecModifier> mods, SpecTrigger trig, List<SpecEffect> effs)`.
  - `Use(UnitController from, Vector3 origin, Vector3 direction)`:
    1. `SnapshotPatch` 빌드.
    2. `multiShot` 만큼 `HitLauncher.FireProjectile` 호출.
    3. 각 HitInstance에 `IHitBehavior` 부착 + `TriggerWatcher` 부착.
- [ ] `MAX_PROJECTILES_PER_CAST = 64` cap 가드.

### 검증
- 디버그 콘솔에서 `SpellSequence` 인스턴스 직접 만들고 `Use` 호출 → 실제로 매직볼이 날아가서 벽돌을 깎는다.
- Modifier (가속, 강화탄) 가 stat에 반영됨.
- Trigger (온히트 → 폭발 Effect) 가 발화함.

### Depends on
- Phase 1 (모든 spec 데이터).
- Phase 2 (벽돌이 있어야 발사체가 무언가 맞춤).

---

## Phase 4 — Weapon runtime + 슬롯 평가 (1~2일)

**목표**: `Weapon` 인스턴스가 슬롯에서 SpellSequence를 빌드해 시전. `castsPerTurn`, `cooldownTurns`, `multiAngle` 정책 적용.

### 산출물
- [ ] `Weapon` 런타임 클래스 — `SpecWeapon spec`, `Spell?[] slots`, `int cooldownLeft`.
- [ ] `Weapon.TryEquipSpell(slotIndex, spell)` — slotShape 카테고리 검증.
- [ ] `Weapon.Cast(angle)`:
  - `cooldownLeft > 0` 면 빈 턴.
  - `castsPerTurn` 만큼 → 슬롯에서 (proj, mods, trig, effs) 추출 → `SpellSequence.Use`.
  - 시전 끝나면 `cooldownLeft = spec.cooldownTurns`.
- [ ] `Weapon.SwapTo(SpecWeapon newSpec)` — 슬롯 가능한 만큼 이주.
- [ ] `TurnRunner` 의 `TurnPhase.CAST` 가 `Weapon.Cast(playerInputAngle)` 호출하도록 통합.

### 검증
- 디버그 UI에 슬롯 시각화 + 스펠 드래그앤드롭.
- 각 무기 (수습/연발/대마도) 가 `castsPerTurn / cooldownTurns` 차이를 보여줌.
- 슬롯이 빈 상태 / 첫 PROJECTILE 슬롯 비어 있음 검증 (시전 버튼 비활성화).

### Depends on
- Phase 3 (SpellSequence).

---

## Phase 5 — Modifier behavior 보강 (2일)

**목표**: GDD의 Modifier behavior 모두 작동.

### 산출물
- [ ] 신규 `IHitBehavior` 구현:
  - `ChainBehavior(int maxJumps)` — OnHit 시 인접 벽돌로 새 InstantHit/MovingHit spawn.
  - ~~`OrbitBehavior` (위치 덮어쓰기 — Homing과 금지 조합).~~ **폐기 (2026-04-30)** — position-override 구조 자체가 ChainBall 에 부적합.
  - ~~`FallingBehavior` (위치 덮어쓰기).~~ **폐기 (2026-04-30)** — 동상.
- [ ] 기존 scaffold 보강:
  - `HomingBehavior` — Velocity 회전 실제 구현 (Movement 모듈과 협업, S/combat-hit §IHitBehavior 카탈로그 §7.10).
  - `BounceBehavior` — `TryConsume()` 가 Movement collision callback에서 호출되도록 ProjectileMovement와 연결.
- [ ] Modifier behavior → IHitBehavior 매핑 표 (Spell.md §7.2) 를 코드 `ModifierBehaviorResolver` 에 반영.
- [ ] ~~금지 조합 검증 — `SpellSequence.Use` 안에서 동시 부착 시 디버그 경고 + 후자 무시.~~ **폐기 (2026-04-30)** — position-override 카테고리 제거로 Velocity-rotate (Homing) vs Position-override 충돌 자체가 사라짐.

### 검증
- "Pinball Machine" 빌드 (스파크 + 가속 + 바운스 마스터 + 리코셰) 동작.
- "Bullet Hell" 빌드 (더블샷 + 분열) 동작.

### Depends on
- Phase 3 (SpellSequence가 Behavior 부착 가능).

---

## Phase 6 — Effect 도메인 구현 (3~4일)

**목표**: GDD §7.4 Effect 카테고리 + §11 Brick / Player 효과 모두 작동.

### 산출물
- [ ] `SpecEffect.g.cs` 더미 → ChainBall 도메인으로 확장 (Phase 1에서 schema는 이미 작성, 여기서는 *동작 코드* 추가).
- [ ] `EffectFactory.Initialize` 에 도메인 `eEffectKind` 별 creator 등록:
  - `DAMAGE_DIRECT`, `AOE_DAMAGE`, `LINE_DAMAGE`, `CHAIN_DAMAGE` — 모두 `InstantHit + Shape` 으로 구현 (HitLauncher 호출).
  - `STATUS_BURN` → `BurnEffect : IDurationEffect, ITickEffect`. `OnTick` 에서 `brick.DealDamage(self, tickDamage, ...)` 또는 `DamagePipeline.Process`. `brick.Data.Effects.Add(effect)` 로 적용.
  - `STATUS_FREEZE` → `FreezeEffect : IDurationEffect`. `BrickField.ShiftAllDown` 가 `Effects.HasEffect<FreezeEffect>()` 검사 후 해당 벽돌 건너뜀.
  - `HEAL` (Player.HP), `GOLD_GAIN` (RunState.gold).
  - `SPAWN_HIT_INSTANCE` — `HitLauncher.FireProjectile` 또는 `Launch`.
  - `EXTRA_CAST` — 다음 턴 `Weapon.castsThisTurnBonus`.
  - `OVERKILL_TRANSFER`, `HALF_HP_REMOVE` — DamagePipeline 후 처리 hook.
  - `GRAVITY_PULL`, `BURN_DETONATE`, `EMPOWER_NEXT` — 개별 IEffect 구현.
- [ ] `Effects.Tick(dt)` 흐름 검증 — 화상 벽돌이 매 턴 데미지 받음 (Phase 2의 TurnRunner.UPKEEP 와 연결).

### 검증
- 화염 모디파이어 + 화상 Effect → 적용 벽돌이 2턴간 1 데미지씩 받음.
- 폭발 Effect → 인접 8칸 1 데미지.
- 연쇄 번개 → 인접 3개로 전이.

### Depends on
- Phase 3 (Trigger가 Effect를 발화시키는 경로).

---

## Phase 7 — Relic 시스템 (2~3일)

**목표**: 24종 유물이 EffectHost 구독으로 동작.

### 산출물
- [ ] Player가 `UnitController/UnitData` 인스턴스를 가짐 (없다면 신규) — `Player.Data.Effects` (UnitEffects) 사용.
- [ ] `RelicRegistry` (런타임) — RunState에 보관된 유물 → IEffect 묶음으로 풀어 EffectHost에 Add.
- [ ] `eRelicHook` → 코드 채널 매핑 표 (Relic.md §2) 를 IEffect 구현체로 표현:
  - 각 hook당 1개 IEffect 구현체 (예: `OnHitRelicEffect`, `OnKillRelicEffect`, `PassiveGlobalRelicEffect` 등).
  - Condition 평가 (`eRelicCondition`) 헬퍼 함수.
  - `chance < 1.0` RNG 분기.
  - `usesPerRun` 카운터.
- [ ] `Handlers.Event` 에 ChainBall 채널 추가 — `WallBounced`, `ProjectileDespawned`, `LineCleared`, `TurnEnded`, `BattleStarted`, `BattleEnded`, `ShopOpened`.
- [ ] `eRelicCategory` 24종 모두 동작.

### 검증
- "철갑탄 장전" (PASSIVE_GLOBAL +1 damage) → 모든 발사체 데미지 +1.
- "처형자" (HP 25% 이하 즉사) → 조건 충족 시 발화.
- "최후의 발악" (런 1회) → 카운터 작동.

### Depends on
- Phase 6 (도메인 IEffect 패턴).
- Phase 4 (Weapon/시전 흐름).

---

## Phase 8 — Player Unit + 위험라인 + 승패 (1~2일)

**목표**: 한 전투를 끝까지 플레이 가능. 승리/패배 화면.

### 산출물
- [ ] `Player : UnitController` (또는 Player 컴포넌트) — HP/방어막 적용 (Survival relic 작동).
- [ ] `TurnRunner.TurnPhase.DAMAGE` 가 위험 라인 도달 벽돌을 Player에 `DealDamage`.
- [ ] 승리 판정 — 모든 웨이브 끝 + 잔여 벽돌 0 → `BattleEnded(victory)` 발화.
- [ ] 패배 판정 — Player.HP <= 0 → `BattleEnded(defeat)` 발화.
- [ ] 디버그 UI: HP 바, 골드, 보유 유물.

### 검증
- 한 전투 처음부터 끝까지 가능. 승/패 모두 정상 종료.

### Depends on
- Phase 2 (Field, TurnRunner), Phase 7 (방어막 같은 Survival relic).

---

## Phase 9 — Meta / Run / Node (3일)

**목표**: 1액트 13~15노드 진행 가능. 보상 선택 / 상점 / 휴식 / 보스.

### 산출물
- [ ] `RunState` 객체 — gold, hp, hpMax, weapon, slots, relics, currentNode, defeatedNodes.
- [ ] `Map` (Act 1 고정맵 권장) — `Node[]` + `Edge[]` + `eNodeKind`.
- [ ] 노드 진입 핸들러 — Battle / Elite / Event / Shop / Rest / Boss.
- [ ] 보상 시스템 — `RewardRoller` (액트 진행도에 따라 rarity 분포 동적).
- [ ] 상점 — 스펠/유물 구매 + 가격 (감정사 relic 적용).
- [ ] 휴식 — HP +N or 스펠 업그레이드 택1.
- [ ] `BossPatternRunner.Boss01.*` (Opening / Telegraph / Rage 등) 코드 — `eBossPattern` enum 분기.
- [ ] Boss 웨이브의 `TWave` 행 작성 (`bossLine=true` + `bossPattern=BOSS_01_*`).
- [ ] Act 1 → 보스 진행 로직.

### 검증
- 한 런: 시작 → 13노드 클리어 → 보스 → 승리 화면.
- 보상으로 받은 스펠/유물이 다음 전투에 적용됨.

### Depends on
- Phase 8 (전투가 완성됨).

---

## Phase 10 — UI / Scene 통합 (3~4일)

**목표**: Title → Lobby → Game 흐름 + 인게임 HUD + 보상/상점 popup.

### 산출물
- [ ] `TitleScene : BaseScene` — `Handlers.Initialize()` + UserData 로드 → LobbyScene.
- [ ] `LobbyScene : BaseScene` — 캐릭터 선택, 시작 버튼.
- [ ] `GameScene : BaseScene` — `BrickField`, `Player`, `Weapon`, `TurnRunner` 인스턴스화.
- [ ] HUD: HP / Gold / Wave / Turn count / Weapon slots / Relic 목록.
- [ ] Popup (모두 `PopupBase` 상속, `Handlers.UI.Show<T>`):
  - `RewardSelectionPopup`, `ShopPopup`, `RestPopup`, `EventPopup`, `MapPopup`.
  - `VictoryPopup`, `DefeatPopup`.
- [ ] 슬롯 편집 UI (드래그앤드롭 또는 클릭).
- [ ] LoadingCanvas 확인 (이미 SceneHandler 소유).

### 검증
- 콜드 스타트부터 1런 완주 가능. UI 흐름 매끄러움.

### Depends on
- Phase 9.

---

## Phase 11 — Content + Balance (지속)

**목표**: GDD §11.1 프로토타입 골 충족.

### 산출물
- [ ] Phase 0 스펠 8종 + 공용 풀 ~25종 데이터 완성.
- [ ] 유물 24종 데이터 완성.
- [ ] 액트 1 + 보스 — 줄 패턴 / 노드 배치 / 보상 분포.
- [ ] 처음~끝 5회 플레이테스트 + 밸런스 패스.

### 검증
- "코어 루프의 재미 검증" — 30~60분 런이 의도대로 흐름. 빌드 다양성 체감.

### Depends on
- Phase 7~10 (시스템 완성).

---

## Phase 12 — Polish (Alpha 진입 전)

**목표**: 알려진 stopgap 정리 + Feel 통합 + 모바일 성능 검증.

### 산출물
- [ ] `FeelHandler` 도입 (ARC §10) — `MMF_Player` 풀, 도메인 FX 키 (`fx/hit_crit`, `fx/explosion`, ...).
- [ ] VFX/SFX 통합 — `SpecHitInstance.vfxKey/sfxKey`, `SpecEffect.vfxKey/sfxKey`.
- [ ] `Handlers.Initialize()` 의 `InitializeAddressable` 중복 호출 제거 (S/handler-resource Known rough edges).
- [ ] `LoadBigTextureAsync` 활성화 (필요시).
- [ ] `/review-mobile` 통과.
- [ ] 풀 warmup (`Handlers.Pool.Prewarm<T>` 호출 지점) — 핫 발사체 사전 로드.

### 검증
- 모바일 디바이스 체감 60fps. GC 스파이크 없음.

### Depends on
- Phase 11.

---

## 의존성 그래프 (요약)

```
Phase 0 (결정)
   │
   ▼
Phase 1 (Spec 데이터)  ←── Phase 11 (Content)
   │
   ▼
Phase 2 (Field/Brick/Turn skeleton)
   │
   ▼
Phase 3 (SpellSequence/Patch/Watcher)
   │       │
   ▼       └──┐
Phase 4    Phase 5 (Modifier behaviors)
(Weapon)      │
   │          │
   └──────────┴──▶ Phase 6 (Effects 도메인)
                       │
                       ▼
                  Phase 7 (Relic)
                       │
                       ▼
                  Phase 8 (Player + 승패)
                       │
                       ▼
                  Phase 9 (Meta/Run/Node)
                       │
                       ▼
                  Phase 10 (UI/Scene)
                       │
                       ▼
                  Phase 11 (Content/Balance)
                       │
                       ▼
                  Phase 12 (Polish/Feel)
```

---

## 작업 단위 권장

| 작업 카테고리 | 권장 PR 단위 |
|---|---|
| Spec 시트 추가 | 시트 1개 + Tables.cs 와이어링 1줄 (PR 1개) |
| 신규 클래스 (예: SpellSequence) | 클래스 + 단위 검증 디버그 코드 (PR 1개) |
| IHitBehavior 추가 | Behavior 1개 + 사용 예시 (PR 1개) |
| Effect kind 추가 | EffectFactory creator 등록 + IEffect 구현 + Phase 1에 schema 행 (PR 1개) |
| Relic hook | hook당 IEffect 구현 1개 (PR 1개) |
| 신규 Popup | `PopupBase` 상속 + Addressable 등록 + 사용 지점 (PR 1개) |

---

## 변경 절차

본 Roadmap을 변경(Phase 추가/병합/순서 변경)하려면 `/arch-update` 흐름을 거친다 — 의존성이 흔들리면 후행 Phase의 산출물 정의가 깨진다.
