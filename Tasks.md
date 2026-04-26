# Tasks — ChainBall

> Solo kanban. 우선순위 = 위→아래. **Active ≤ 3** 유지.
> 큰 그림은 `Docs/Roadmap.md`. 이 파일은 그 중 **지금 작업 중인 슬라이스**.
>
> **Current Phase**: Phase 1 — Spec 데이터 기반 (1~2일, 디자이너 협업)

---

## Active

<!-- 지금 손대고 있는 것. 시작하면 Backlog → Active 로 이동. -->

- [ ] (2026-04-26~) Phase 2 — Editor 작업 + 씬 와이어링
  - Brick prefab 만들기 (Brick + UnitFsmHandler + Collider2D + SpriteRenderer)
  - Addressable key `UnitController_1` 등록
  - GameScene 에 BrickField / TurnRunner / BrickFactory / Player 컴포넌트 배치 + 인스펙터 와이어링
  - 디버그 패턴으로 ENEMY 단계에서 줄 생성 호출 (현재 stub)

---

## Backlog

### Phase 1 — Spec 데이터 기반

<!-- 시트명 컨벤션: Spec* (T* 폐기, 2026-04-26 결정) -->
<!-- 권장 PR 단위: 시트 1개 + Tables.cs 와이어링 1줄 = PR 1개 (Roadmap §작업 단위 권장) -->

<!-- 진행 순서: 의존 적은 것부터. SpecEffect는 Phase 6(EffectFactory 재구현)과 묶여 보류. -->

<!-- SpecEffect / SpecCharacter 마이그레이션 후속 작업 -->
- [ ] **(Phase 6)** `EffectFactory` + `DamageActionBase` 마이그레이션 — SpecEffect 시트화로 깨진 코드 복구. `eEffectKind` 별 creator dispatch. `Schema/effect.md` 권위.
- [ ] **(Phase 8)** `UnitData` 마이그레이션 — SpecCharacter 시트화로 깨진 코드 복구. ChainBall Player 구조 결정 (UnitController 파생 여부, stat 사용 여부 등).
- [ ] `SpecLocalize`에 모든 nameKey/descKey 등록
- [ ] **검증**: `Tools > SpecData > Rebuild All` 무에러 + `SpecDataValidator` Console 빨간 로그 0
- [ ] **검증**: 런타임에서 `SpecDataManager.SpecHitInstance.Get(...)` 호출 시 데이터 반환 확인

### Phase 2 — Field & Brick & Turn skeleton (코드 완료, Editor 작업 남음)

- [x] `Brick : UnitController` 얇은 파생 (`eBrickType`, `eElement`, `gridPos`)
- [x] `BrickField` (8×15) — `AddRowFromPattern`, `ShiftAllDown`, `IsEmpty`, `GridToWorld`
- [x] `BrickPatternParser` — `./N/N(2)/S(F)/E/P/R/B[id]` 표기법
- [x] `TurnRunner` + `TurnPhase` (Idle/UPKEEP/CAST/ENEMY/DAMAGE/END), `OnTurnPhaseChanged`, UPKEEP 에서 `Effects.Tick(1f)`
- [x] `Player` 임시 컴포넌트 (HP, OnHpChanged, OnDefeat)
- [x] `BrickFactory` (`Handlers.Pool.Get<UnitController>` + Brick 캐스팅 + UnitData 조립)
- [x] 디버그 입력: 스페이스바 = 다음 턴 (TurnRunner.Update)
- [x] `UnitData.InitializeBare()` — SpecCharacter 없이도 동작 (Brick 용)
- [x] **결정**: Brick prefab 1종 + 시각 토글

---

## Done

<!-- 최근 완료. 누적되면 `Tasks/Archive/YYYY-MM.md`로 이주. -->

### 2026-04
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
