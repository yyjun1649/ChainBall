# Tasks — ChainBall

> Solo kanban. 우선순위 = 위→아래. **Active ≤ 3** 유지.
> 큰 그림은 `Docs/Roadmap.md`. 이 파일은 그 중 **지금 작업 중인 슬라이스**.
>
> **Current Phase**: Phase 1 — Spec 데이터 기반 (1~2일, 디자이너 협업)

---

## Active

<!-- 지금 손대고 있는 것. 시작하면 Backlog → Active 로 이동. -->

- [ ] _(없음 — 시작할 때 Backlog 최상단에서 가져오기)_

---

## Backlog

### Phase 1 — Spec 데이터 기반

<!-- 권장 PR 단위: 시트 1개 + Tables.cs 와이어링 1줄 = PR 1개 (Roadmap §작업 단위 권장) -->

- [ ] `#enum` 시트 12종 enum 추가 — `eHitInstanceKind, eProjectileMotion, eRarity, eElement, eModifierBehavior, eTriggerEvent, eEffectKind, eRelicCategory, eRelicHook, eRelicCondition, eSlotKind, eUnlockCondition, eBossPattern`
- [ ] `THitInstance` 시트 작성 — kind enum 컬럼 + ChainBall 도메인 필드 (`Docs/Specs/Schema/hit_instance.md` 동기 갱신)
- [ ] `TModifier` 시트 + 와이어링
- [ ] `TTrigger` 시트 + 와이어링
- [ ] `TEffect` 시트 + 와이어링
- [ ] `TRelic` 시트 + 와이어링
- [ ] `TWeapon` 시트 + 와이어링
- [ ] `TCharacter` 시트 + 와이어링
- [ ] `TWave` 시트 + 와이어링 (`Docs/Specs/Schema/wave.md` **신규** 작성)
- [ ] `SpecLocalize`에 모든 nameKey/descKey 등록
- [ ] **검증**: `Tools > SpecData > Rebuild All` 무에러 + `SpecDataValidator` Console 빨간 로그 0
- [ ] **검증**: 런타임에서 `SpecDataManager.SpecHitInstance.Get(...)` 호출 시 데이터 반환 확인

### Phase 2 미리보기 — Field & Brick & Turn skeleton

<!-- Phase 1 완료 직후 진입. 디자이너 시트 진행 중일 때 임시 하드코딩으로 병행 가능. -->

- [ ] `Brick : UnitController` 얇은 파생 (`eBrickType`, `eElement`, EnemyLayer)
- [ ] `BrickField` (8×15 그리드 매니저) — `AddRowFromPattern`, `ShiftAllDown`, `IsEmpty`
- [ ] `BrickPatternParser` — `string[8]` (`./N/N(2)/S(F)/...`) → spawn
- [ ] `TurnRunner` — `TurnPhase` 진행 + `OnTurnPhaseChanged` + `UPKEEP`에서 `Effects.Tick(1f)`
- [ ] `Player` 임시 컴포넌트 (HP만)
- [ ] `BrickFactory` / `BrickPool` (Editor: Brick prefab 등록 필요)
- [ ] 디버그 입력: 스페이스바 = 다음 턴
- [ ] **결정 필요**: Brick prefab 1종 vs type별 5종 (권장: 1종 + 시각 토글)

---

## Done

<!-- 최근 완료. 누적되면 `Tasks/Archive/YYYY-MM.md`로 이주. -->

### 2026-04
- [x] (2026-04-26) `Tasks.md` 도입 — 마크다운 칸반 시작
- [x] (2026-04-26) `Docs/Roadmap.md` 작성 — Phase 0~12 구현 순서 확정
- [x] Phase 0 — 설계 잔존 결정 5종 확정 (상태이상 모델, 시간 감속, TWave schema, Trigger 카운터, 보스 패턴)

### 사전 작업 (날짜 미상)
- [x] 6-레이어 컨트랙트 — `Unit / Stat / Effect / Damage / HitInstance` (UnitCombatDesign §1~7)
- [x] `Handlers.Pool` 통합 — `MovingHit`, `InstantHit`, `AuraHit` 모두 `PoolMonoBehaviour<T>` 위 동작
- [x] SpecData 파이프라인 — xlsx → `*.g.cs` + `*.json` → `SpecDataManager`
- [x] `Skill : DamageActionBase<SpecSkill>` + `SkillFactory` + `HitLauncher.FireProjectile`
