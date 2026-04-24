---
description: Update ARCHITECTURE.md BEFORE implementing a design change; get approval before code.
argument-hint: <short description of the proposed change>
---

The user is requesting a design change: **$ARGUMENTS**

## Workflow (strict)

1. **Read `ARCHITECTURE.md`** fully. Identify the section(s) the change affects.
2. **Summarize the impact** in 3–6 bullets:
   - What rule / section changes?
   - Which files / handlers / data types are affected?
   - What existing callers break? How are they migrated?
   - Risks (perf, lifecycle, asset references)?
3. **Draft the doc patch** — an explicit diff to `ARCHITECTURE.md`. Do not propose code changes yet.
4. **Pause and wait for user approval** on the doc patch.
5. Only after approval, proceed to implementation in a **separate** step.

## Guardrails

- Never edit `ARCHITECTURE.md` and production code in the same pass.
- If the change violates a hard rule in `CLAUDE.md` (e.g., adding a new singleton), call it out and
  ask the user to reconsider before continuing.
- If the change implies `ProjectSettings`, `manifest.json`, or new `asmdef` work, flag it.

## Output

- The impact analysis.
- The proposed `ARCHITECTURE.md` diff.
- A direct question: "Approve this architecture change? (yes / revise)"
