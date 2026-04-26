# ChainBall — Design & Spec Docs

> Source of truth for **design intent (Systems)** and **data contracts (Specs)**.
> Numbers themselves live in `Assets/@Project/Scripts/SpecData/Xlsx/Spec.xlsx` — not in this folder.

---

## Layout

```
Docs/
├── GDD.md                    # Game design document (human-facing). Cleaned-up source from SpellBreaker_GDD_v0.1.docx.
├── Roadmap.md                # Phase별 구현 순서. 무엇을 언제 만들지.
├── Systems/                  # Rules, not numbers. Touching these may require /arch-update.
│   ├── Combat.md             # Turn lifecycle, cast sequence evaluation, damage pipeline
│   ├── Brick.md              # Brick types, line generation, danger line
│   ├── Weapon.md             # Slot layout, cast policy
│   ├── Spell.md              # 4 spell categories + slot order semantics
│   ├── Relic.md              # Passive timing, hook points
│   └── Meta.md               # Acts, nodes, rewards
└── Specs/                    # Spec table contracts (column dictionaries).
    ├── README.md             # Pipeline guide (xlsx → JSON + .g.cs → SpecDataManager)
    └── Schema/               # One file per Spec table. Defines columns, types, allowed values.
        ├── hit_instance.md   # SpecHitInstance (kind=MOVING / INSTANT / AURA 통합)
        ├── modifier.md
        ├── trigger.md
        ├── effect.md
        ├── relic.md
        ├── weapon.md
        └── character.md
```

## How to read this

| Question | Look here |
|---|---|
| "What's the design vision?" | `GDD.md` |
| "What do we build next, and in what order?" | `Roadmap.md` |
| "How does turn evaluation work?" | `Systems/Combat.md` |
| "What columns must the HitInstance xlsx sheet have?" | `Specs/Schema/hit_instance.md` |
| "What's the value of `magic_ball.baseDamage`?" | The xlsx — never duplicated here |

## How to change

- **Numbers** — edit `Assets/@Project/Scripts/SpecData/Xlsx/Spec.xlsx`, run `Tools > SpecData > Rebuild All`.
- **Schema (columns)** — update both the xlsx and the corresponding `Specs/Schema/*.md` in the same change.
- **System rules** — `/arch-update` flow: doc first, then code. See `ARCHITECTURE.md` §11.
