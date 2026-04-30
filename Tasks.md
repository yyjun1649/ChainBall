# Tasks — ChainBall

> Solo kanban. 우선순위 = 위→아래. **Active ≤ 3** 유지.
> 큰 그림은 `Docs/Roadmap.md`. 이 파일은 그 중 **지금 작업 중인 슬라이스**.
>
> **Current Phase**: Phase 6 — Effect 도메인 구현 (3~4일)

---

## Active

<!-- 지금 손대고 있는 것. 시작하면 Backlog → Active 로 이동. -->

- [ ] Phase 6 — `EffectFactory.Initialize` 골격 + `HEAL` kind 첫 등록. Stub `EffectFactory.Initialize()` 채우기 (`eEffectKind` → creator dispatch dict). `HealEffect : IEffect` 신규 — `SpecEffect.healAmount` 만큼 `Player.HP += amount`. `Schema/effect.md` 의 `HEAL` 행 권위. SROptions 에 `Trigger HEAL` 검증 버튼 (Player HP 직접 회복). 다음 kind (`DAMAGE_DIRECT`, `STATUS_BURN`, …) 는 후속 PR.

---

## Backlog

### Phase 6 — Effect 도메인 구현 (Roadmap §Phase 6)

<!-- 권장 PR 단위: EffectFactory creator 등록 + IEffect 구현 + schema 행 (Roadmap §작업 단위 권장) -->
<!-- 진행 순서: 의존성 적은 것부터. HEAL (Player.HP만) → GOLD_GAIN (RunState 의존, Phase 9 와이어링 후) → DAMAGE_DIRECT/AOE_DAMAGE/LINE_DAMAGE/CHAIN_DAMAGE (HitLauncher.FireProjectile + InstantHit + Shape) → STATUS_BURN (BurnEffect : IDurationEffect, ITickEffect) → STATUS_FREEZE (FreezeEffect, BrickField.ShiftAllDown 게이트) → SPAWN_HIT_INSTANCE → EXTRA_CAST → OVERKILL_TRANSFER / HALF_HP_REMOVE / GRAVITY_PULL / BURN_DETONATE / EMPOWER_NEXT. -->

- [ ] **(보류)** `UnitData` 마이그레이션 — SpecCharacter 시트화로 깨진 코드 복구. ChainBall Player 구조 결정 (UnitController 파생 여부, stat 사용 여부 등). Phase 8 에서 Player Unit 본격 도입 시 처리.
- [ ] `Effects.Tick(dt)` 흐름 in-scene 검증 — 화상 벽돌이 매 턴 데미지 받음 (Phase 2 TurnRunner.UPKEEP 와 연결).

### Phase 3 — SpellSequence (Roadmap §Phase 3)

<!-- 권장 PR 단위: 클래스 1개 + 단위 검증 디버그 코드 (Roadmap §작업 단위 권장) -->
<!-- 진행 순서: SnapshotPatch (스냅샷 변형, 의존성 0) → TriggerWatcher (HitInstance event 구독) → SpellSequence (위 둘 + HitLauncher 통합). -->

- [ ] **검증**: 씬에서 `SpellSequence` 직접 만들어 `Use` 호출 → 매직볼이 날아가서 벽돌이 깎임. Modifier (가속/강화탄) stat 반영, Trigger (온히트→폭발) 발화 — 단, `EffectFactory` Phase 6 미등록이므로 *발화 경고 로그* 만 확인.

### Phase 1 — Spec 데이터 기반

<!-- 시트명 컨벤션: Spec* (T* 폐기, 2026-04-26 결정) -->
<!-- 권장 PR 단위: 시트 1개 + Tables.cs 와이어링 1줄 = PR 1개 (Roadmap §작업 단위 권장) -->

<!-- 진행 순서: 의존 적은 것부터. SpecEffect는 Phase 6(EffectFactory 재구현)과 묶여 보류. -->

<!-- SpecEffect / SpecCharacter 마이그레이션 후속 작업 -->
- [ ] `SpecLocalize`에 모든 nameKey/descKey 등록
- [ ] **검증**: `Tools > SpecData > Rebuild All` 무에러 + `SpecDataValidator` Console 빨간 로그 0
- [ ] **검증**: 런타임에서 `SpecDataManager.SpecHitInstance.Get(...)` 호출 시 데이터 반환 확인

---

## Done

<!-- 최근 완료. 누적되면 `Tasks/Archive/YYYY-MM.md`로 이주. -->

### 2026-04
- [x] (2026-04-30) **Phase 5 종료** — Modifier behavior 보강 완료. HomingBehavior / LightningChainBehavior / BonusDamageBehavior / SpawnOnHitBehavior 4종 + UnitSpawnHandler 레지스트리 최적화 (`GetNearestEnemy/GetNearestEnemies` Top-N stackalloc) + Orbit/Falling position-override 카테고리 폐기. → Phase 6 진입.
- [x] (2026-04-30) Phase 5 — `SpawnOnHitBehavior` 신규 구현 (OnHit 구독 패턴). 검증 완료. count + hitInstanceId + spreadAngleDeg, 부채꼴 fan-out, ChildDamageSpec 어댑터 + parent Snapshot fallback, BounceMovement.CopyBoundsFrom 으로 자식 wall reflect 정합, immediate re-hit 방지 (skinDistance 0.4 + IgnoreColliderForFrames 5).
- [x] (2026-04-30) Phase 5 — `BonusDamageBehavior` 신규 구현 (OnHit 구독 패턴). 검증 완료. OnHit + OnTickFrame 펄스 큐, target 고정 + Version 게이트, 매 OnHit 누적, count=3/damage=2/interval=0.3 SROptions 하드코딩.
- [x] (2026-04-30) Dev — SRDebugger Spawn 패널 추가 (BrickType / Column / FilledRow). FieldHandler ShiftAllDown + AddRowFromPattern 우회.
- [x] (2026-04-30) Phase 5 — `LightningChainBehavior` 신규 구현 (OnHit 구독 패턴). 검증 완료. `GetNearestEnemies` stackalloc Top-N + `DealDamage` 6-layer 진입점, SROptions `Cast: LightningChain` 버튼.
- [x] (2026-04-30) Phase 5 — `OrbitBehavior` / position-override 구조 폐기 결정. ChainBall 에 부적합 (Orbit/Falling 모두). 관련 코드/marker/가드 제거 (combat-specialist 동시 진행).
- [x] (2026-04-30) Phase 5 — `HomingBehavior` 보강 완료. Velocity 회전 실구현 (단순 turnRate, U-turn 부스트 없음) + 타깃 사망 시 retarget + bounce pause 0f. `FindObjectsByType` 제거하고 `UnitSpawnHandler.GetNearestEnemy` 레지스트리 경로로 최적화. turnRate=360f 는 SROptions 하드코딩 (정식 SpecData 화는 Phase 11 Content/Balance 시점). SROptions Homing 토글 + 타깃 디버그로 in-scene 검증 통과.
- [x] (2026-04-30) Phase 3+4 in-scene 검증 — GameScene 진입 → SRDebugger Spell 카테고리 → `CastDirect` 버튼으로 매직볼 발사 동작 확인. Addressable `HitInstance_1` 키 등록 완료. Phase 3 (SpellSequence/SnapshotPatch/TriggerWatcher) + Phase 4 (Weapon) 런타임 라이브 검증 통과. → Phase 5 진입.
- [x] (2026-04-30) Phase 3+4 zero-alloc 패스 — 사용자 컨벤션 강제: ZString / ZLinq / Pool. `SnapshotPatch` / `TriggerWatcher` / `SpellSequence` / `HitInstanceDamageSpec` 4종을 `Library.DisposeObject<T>` 로 전환 (HitSnapshot/DamageInfo 와 동일 패턴): 정적 `Get()` + `Dispose()` 사이클 + `protected override Reset()`. 모든 `$"..."` 인터폴레이션을 `ZString.Format/Concat` 로 교체 (Cysharp.Text). `SpellSequence.Reset` 은 `_activeWatchers.Clear()` 만 — watcher 들은 자기 HitInstance.OnDespawn 으로 자가-Dispose 하므로 sequence 풀링과 독립. ZLinq 미적용 (LINQ 미사용). ListPool 미적용 (TriggerWatcher 가 effects List 를 hit 수명 동안 보유 → aliasing 위험). Weapon 본체는 long-lived (per-Player 1개) 라 풀 불필요.
- [x] (2026-04-30) Phase 3+4 코드리뷰 + 최적화 — 3-agent 병렬 리뷰 (reuse / quality / efficiency) 후 픽스: ① `HitSnapshotKeys.HitWidth/Element` 상수화 (SnapshotPatch + TriggerWatcher 의 stringly-typed `Extra` 키 제거) ② TriggerWatcher 에 `_effectIdPrefix` 캐시 — `FireEffects` 의 `$"trig_{id}_{spec.id}"` 매-발화 string alloc 제거 (HIGH) ③ SpellSequence.Use 가 `HitInstanceDamageSpec` 를 캐스트당 1회 hoist (이전: 발사체당 alloc) ④ `UtilCode.AngleToVector` 재사용 — Weapon.FireOnce / SROptions.Spell 의 inline `cos/sin/Deg2Rad` 중복 제거 ⑤ CONSECUTIVE_HIT vs NTH_BRICK_HIT 시맨틱 차이 주석 추가. 스킵: `_hasBounds` 캐시 (hot path 정당), `ApplyPierce` is-check (honest dispatch), List per-Cast (TriggerWatcher 수명 보유).
- [x] (2026-04-30) ChainBall pierce 시맨틱 구현 (Phase 5 일부 선행). `BounceMovement` 에 `_pierceLeft` + `SetPierceCount(int)` 추가. `TickBounce` 가 `Physics2D.CircleCastNonAlloc` (8-buffer, 거리 정렬) 로 한 step 안의 모든 hit 처리 — 유닛 hit 시 pierce 활성이면 데미지 + decrement + 반사 스킵 + 다음 hit 으로 진행 (벽은 항상 반사). `PierceMark[4]` 무시 리스트 (3프레임 TTL) 로 brick 가 폭이 step 보다 클 때 같은 charge 재소비 방지. `SpellSequence.ApplyPierce` 가 BounceMovement 면 `SetPierceCount`, 아니면 기존 `PenetrateBehavior(N)` 폴백. `PIERCE_ON_HIT` 모디파이어도 동일 분기. SROptions 의 PierceCount/ModInfinitePierce 가 의도대로 "벽돌 통과" 로 작동.
- [x] (2026-04-30) SROptions 벽 반사 지원 — `SpellSequence.Use` / `Weapon.Cast` 에 `Action<MovingHit> onFired` 옵셔널 콜백 추가 (스폰 직후 호출). SROptions 에 `BounceWalls` 토글 + `BoundsXMin/XMax/YMax/KillLine` 노브 (디폴트 CastPhase 값 매칭: -4/4/12/0). `MaybeAttachBounceContext` 가 콜백으로 전달되어 BounceMovement.AttachContext 호출 → 벽 반사 정상 동작.
- [x] (2026-04-30) Bug — `BounceMovement` degenerate-bounds 가드. SROptions CastDirect 호출 시 공이 위로 가다 즉시 아래로 반사된 원인: `AttachContext` 미호출 → `_bounds=default(Rect)` → ceiling y=0 즉시 반사. `_hasBounds` 플래그 도입 (`width>0 && height>0` 체크), `Initialize` 에서 `_bounds/_session/_killLineReported/_mode` 명시적 리셋 (풀 재사용 시 이전 cast 의 bounds 가 누설되지 않도록). 기존 `BallSession` 경로는 AttachContext 가 valid bounds 로 호출되어 `_hasBounds=true` → 동작 동일.
- [x] (2026-04-30) Phase 3+4 검증 패널 — `Assets/@Project/Scripts/Game/Spell/Debug/SROptions.Spell.cs`. `public partial class SROptions` (no namespace) 추가. Spell 카테고리: HitInstanceId/BaseDamage/MultiShot/Spread/Bounce/Pierce/Aim/MoveSpeed/LifeTime 노브 + Heavy/Light/DmgUp/InfinitePierce 모디파이어 토글 + OnHit 트리거 토글 + Weapon castsPerTurn/cooldownTurns. 버튼: `CastDirect` (SpellSequence 직접), `CastViaWeapon` (Weapon.Cast — 쿨다운 다음 Cast 시 자동 skip 검증), `ResetSpellOptions`. SpecHitInstance/SpecModifier/SpecTrigger/SpecEffect 인스턴스를 코드에서 즉석으로 생성 — SpecData 시트 채움 미완 상태에서도 동작. 사용자가 SRDebugger (StompyRobot) 설치 (2026-04-30, source 형태로 `Assets/StompyRobot/`).
- [x] (2026-04-30) Phase 4 — `Spell` + `Weapon` 런타임. `Assets/@Project/Scripts/Game/Spell/Spell.cs` + `Weapon.cs`. `Spell` discriminated union (Projectile/Modifier/Trigger/Effect 4종 factory + `FitsSlot`). `Weapon` (`SpecWeapon spec`, `Spell?[] slots`, `int cooldownLeft`): `TryEquipSpell` (slotShape 호환 + null 슬롯 클리어), `Cast(from, origin, IReadOnlyList<float> angles)` — `castsPerTurn` 만큼 SpellSequence 빌드 + `cooldownTurns` 적용 + 쿨다운 자동 decrement, `SwapTo(newSpec, overflow)` first-fit migration, `CanCast` (첫 PROJECTILE 슬롯 검사). `ExtractSlots` 매 캐스트당 새 List — TriggerWatcher 가 effects 리스트를 hit 수명 동안 보유하므로 정적 버퍼 alias 위험 회피.
- [x] (2026-04-30) Phase 3 — `SnapshotPatch` 구현. `Assets/@Project/Scripts/Game/Spell/SnapshotPatch.cs` (Spell.md §3 / §7.1 권위표 그대로). `Apply(SpecModifier)` 누적 + `ApplyTo(HitSnapshot, SpecHitInstance)` 적용. `Mul`이 0이면 identity로 처리 (xlsx empty cell 방어). Editor verifier 추가 (`Tools > ChainBall > Spell > Verify SnapshotPatch`) — 강화탄+경량탄+데미지업 → damage=3 / bounce=7.
- [x] (2026-04-30) Phase 3 — `SpellSequence` 통합. `Assets/@Project/Scripts/Game/Spell/SpellSequence.cs`. `Initialize` + `Use(from, origin, dir)`: SnapshotPatch 빌드 → multiShot (+ CLONE_AT_FIRE) cap 64 → spread 분배 → `Handlers.Pool.Get<MovingHit>` + `HitSnapshotBuilder.Build` + `patch.ApplyTo` + `Initialize` → bounce/pierce delta 반영 + PIERCE_ON_HIT → TriggerWatcher 부착. `Dispose` 시 watcher 일괄 정리. `HitInstanceDamageSpec` adapter (SpecHitInstance → IDamageSpec, damageType/attackType `default`). TriggerWatcher 는 OnDespawn 끝에서 self-Dispose 추가 — 풀 재활용 시 핸들러 leak 방지.
- [x] (2026-04-30) Phase 3 — `TriggerWatcher` 구현. `Assets/@Project/Scripts/Game/Spell/TriggerWatcher.cs`. HitInstance.OnHit/OnDespawn 구독 → `eTriggerEvent` 매칭 (BRICK_HIT/BRICK_KILL/PROJECTILE_DESPAWN/NTH_BRICK_HIT/ELEMENT_MATCH/CONSECUTIVE_HIT 6종) → `EffectFactory.Create` + `host.Data.Effects.Add`. cooldownTurn / maxFiresPerCast 가드. WALL_BOUNCE/LINE_CLEAR/DANGER_PROXIMITY/FULL_BOUNCE 는 Phase 7 ChainBall event bus 도입 후.
- [x] (2026-04-29) Phase 2 마무리 — Editor 작업 (Brick prefab + Addressable `UnitController_1` 등록 + GameScene 와이어링 + 디버그 줄 생성) 완료. 코드 측은 `TurnRunner` 단일 클래스 모델에서 **GameHandler 분리 모델**로 진화: `FieldHandler` / `PhaseHandler` / `UnitSpawnHandler` 3종 + `UpKeep/Cast/Damage/Enemy/End/Idle` Phase 코루틴 5종. `ARCHITECTURE.md §2.6` 신규 섹션으로 패턴 문서화.
- [x] (2026-04-26) `Brick : UnitController` 얇은 파생 (`eBrickType`, `eElement`, `gridPos`)
- [x] (2026-04-26) `BrickField` (8×15) — `AddRowFromPattern`, `ShiftAllDown`, `IsEmpty`, `GridToWorld`
- [x] (2026-04-26) `BrickPatternParser` — `./N/N(2)/S(F)/E/P/R/B[id]` 표기법
- [x] (2026-04-26) `TurnRunner` + `TurnPhase` (Idle/UPKEEP/CAST/ENEMY/DAMAGE/END), `OnTurnPhaseChanged`, UPKEEP 에서 `Effects.Tick(1f)`
- [x] (2026-04-26) `Player` 임시 컴포넌트 (HP, OnHpChanged, OnDefeat)
- [x] (2026-04-26) `BrickFactory` (`Handlers.Pool.Get<UnitController>` + Brick 캐스팅 + UnitData 조립)
- [x] (2026-04-26) 디버그 입력: 스페이스바 = 다음 턴 (TurnRunner.Update)
- [x] (2026-04-26) `UnitData.InitializeBare()` — SpecCharacter 없이도 동작 (Brick 용)
- [x] (2026-04-26) **결정**: Brick prefab 1종 + 시각 토글
- [x] (2026-04-26) `Tasks.md` 도입 — 마크다운 칸반 시작
- [x] (2026-04-26) `Docs/Roadmap.md` 작성 — Phase 0~12 구현 순서 확정
- [x] (2026-04-26) `Docs/Specs/Schema/_enums.md` — 13종 enum 권위 문서 (정수값 확정 + 디자이너 절차)
- [x] (2026-04-26) `#enum` 시트 13종 enum 추가 — `Generated/Enums.g.cs` 에 `eHitInstanceKind ~ eBossPattern` 13개 모두 생성 확인
- [x] (2026-04-26) Spec 시트명 컨벤션 변경 — `T*` 폐기, `Spec*` 로 통일. `Docs/Specs/README.md` + 8개 schema md 일괄 정정. Code-side 변경 없음 (CodeGenerator 자동 호환).
- [x] (2026-04-26) `SpecHitInstance` 시트 작성 — 25컬럼, `Generated/SpecHitInstance.g.cs` + `Json/SpecHitInstance.json` + `SpecDataManager.Tables.g.cs` 와이어링 검증 통과 (jsonOutDir = `Assets/@Project/Scripts/SpecData/Json` 으로 정정). 데이터 행은 디자이너 채움 단계.
- [x] (2026-04-26) `Schema/effect.md` 키 타입 정정 — `string` → `int` (`SpecHitInstance.effects: int[]` 및 코드 더미와 일치).
- [x] (2026-04-26) `SpecModifier` 시트 작성 — 17컬럼, `Generated/SpecModifier.g.cs` + `Json/SpecModifier.json` + `Tables.g.cs` 와이어링 검증. (시트명 혼선으로 한 차례 SpecEffect 더미가 덮어씌워졌다가 git checkout으로 복원.)
- [x] (2026-04-26) `SpecTrigger` 시트 작성 — 10컬럼, 컬럼명 `event` → `eventType` 정정 (C# 예약어 회피). `Schema/trigger.md` 동기 갱신.
- [x] (2026-04-26) `SchemaParser` C# 예약어 가드 추가 — 잘못된 컬럼명은 `Tools > SpecData > Rebuild All` 단계에서 빨간 에러 + 컬럼 스킵 (컴파일 단계까지 가지 않음). Contextual keyword (`var/dynamic/partial/where`)는 의도적으로 제외.
- [x] (2026-04-26) `SpecWeapon` 시트 작성 — 10컬럼, `eSlotKind[] slotShape` 배열 정상 생성, `Tables.g.cs` 와이어링 검증.
- [x] (2026-04-26) `SpecWave` 시트 작성 — 6컬럼, `string[] pattern` + `eBossPattern` 정상, key=int 와이어링.
- [x] (2026-04-26) `SpecRelic` 시트 작성 — 19컬럼, 3개 enum + `triggerEffectId` 타입 `string`→`int` 정정 (SpecEffect.id 동기).
- [x] (2026-04-26) `SpecCharacter` 시트 작업 Phase 8로 보류 결정 — `UnitData.cs` 의 `GetDictionary()` + 11 stat 필드 의존이 ChainBall Player 구조와 함께 마이그레이션 필요.
- [x] (2026-04-26) `SpecEffect` 시트 작성 — 18컬럼, 기존 더미 덮어쓰기. `Generated/SpecEffect.g.cs` 갱신 + `Tables.g.cs` 와이어링.
- [x] (2026-04-26) `SpecCharacter` 시트 작성 — 15컬럼, 기존 더미 덮어쓰기. `Generated/SpecCharacter.g.cs` 갱신 + `Tables.g.cs` 와이어링.
- [x] (2026-04-26) 컴파일 stub 처리 — 3개 파일 임시 복구:
  - `EffectFactory.cs`: dispatch key `eEffectType` → `eEffectKind`. Initialize 비움. Create 미등록 시 `null` + 경고.
  - `DamageActionBase.cs`: `SpecEffect.GetDictionary()/SetParam()` → `SpecDataManager.Instance.SpecEffect.TryGet()`. null 가드.
  - `UnitData.cs`: `SpecCharacter.GetDictionary()` → `SpecDataManager.Instance.SpecCharacter.Get()`. RegisterBaseStats 11 stat 라인 → `startHp`만 `Health` 등록 (TODO Phase 8).
- [x] (2026-04-26) `UnitController` slim down — RogueLikeTemplate 자율 모듈 제거. `ARCHITECTURE.md §3.5` 신규 섹션 추가.
  - 삭제: `Detect/`, `BulletHellController.cs` (사용자 Editor)
  - 슬림화: `UnitController` (Move/Heal/Detect/Target/AttackRange/OnAction*+OnFlip/OnHeal* 제거 + `Release()` 무한재귀 버그 → `OnRelease()` virtual 로 정정), `UnitView` (Action/Move/Flip/Heal 핸들러 제거), `UnitHPBar` (OnTakeHeal 제거)
- [x] (2026-04-26) Phase 2 코드 skeleton 완료 — Brick / BrickField / BrickPatternParser / BrickFactory / BrickView / TurnRunner / Player / UnitData.InitializeBare. 폴더 위치는 `Game/Unit/Brick/` + `Game/Turn/` + `Game/Player/`.
- [x] (2026-04-26) BrickFactory View 와이어링 — Controller 1개 + View N개 (type별) 모델. Inspector 매핑 (`_viewIdByType`, `_bossViewMap`). View 는 `UnitView` 그대로 (이벤트 구독형이라 brick-specific 클래스 불필요).
- [x] (2026-04-26) Unit Data 구조 결정 — Active Record + Module 패턴 채택 (Phase 6 까지). `UnitData` 가 generic module dict 보유 (`GetModule<T>` / `AddModule`), brick-specific 메타는 `BrickMeta : UnitDataModule` 로. Controller / Data 책임 본격 split (BaseStats vs RuntimeStats 등) 은 Phase 6 Effect 도메인 구현 시 재평가.
- [x] Phase 0 — 설계 잔존 결정 5종 확정 (상태이상 모델, 시간 감속, TWave schema, Trigger 카운터, 보스 패턴)

### 사전 작업 (날짜 미상)
- [x] 6-레이어 컨트랙트 — `Unit / Stat / Effect / Damage / HitInstance` (UnitCombatDesign §1~7)
- [x] `Handlers.Pool` 통합 — `MovingHit`, `InstantHit`, `AuraHit` 모두 `PoolMonoBehaviour<T>` 위 동작
- [x] SpecData 파이프라인 — xlsx → `*.g.cs` + `*.json` → `SpecDataManager`
- [x] `Skill : DamageActionBase<SpecSkill>` + `SkillFactory` + `HitLauncher.FireProjectile`
