---
description: Sync Tasks.md against the current project state (Roadmap, git history, scope drift). Propose a diff; do not edit until approved.
argument-hint: <optional focus, e.g. "phase 1 only" / "done section">
---

The user wants to refresh `Tasks.md` so it reflects current reality. Focus: **$ARGUMENTS** (empty = full sync).

**Delegate this to the `pm` agent.** Use the Agent tool with `subagent_type: pm` and the brief below. PM agent owns `Tasks.md` editing — do not edit it from the main thread.

## Brief to pass the pm agent

```
Sync Tasks.md against current project state. Do NOT edit the file yet — produce a proposal first.

Focus area (from user): $ARGUMENTS

Steps:
1. Read Tasks.md (current state).
2. Read Docs/Roadmap.md (strategic phase + deliverables).
3. `git log --oneline -30` and `git status` — find work that landed but isn't reflected in Done.
4. For each section, identify drift:
   - **Active**: items that look completed (matching commits / files exist) → propose move to Done.
   - **Active**: items with no recent commits (>14 days) → flag as stale, suggest demote or split.
   - **Backlog**: missing items from current Phase's Roadmap deliverables → propose adds.
   - **Backlog**: items that no longer fit any Phase → flag as orphan.
   - **Done**: items missing date stamps → propose backfill (use commit date if findable, else mark as "(date unknown)").
5. Verify current Phase header in Tasks.md matches Roadmap progress (e.g., if all Phase 1 deliverables are Done, current phase should advance).

Output (in Korean):
- 5~10 line summary of drift found.
- Concrete diff proposal for Tasks.md (show before/after for each change block).
- One-line recommendation: which change is most important to apply first.
- End with: "이대로 적용할까요? (yes / 일부만 / revise)"

Do NOT apply edits until the user confirms in the next turn.
```

## After the agent returns

- Relay the agent's diff and recommendation to the user.
- Wait for explicit approval.
- On approval, ask the pm agent to apply the diff (or the subset the user picked).

## Guardrails

- Never edit `Docs/Roadmap.md` from this command — strategy changes go through `/arch-update`.
- Never bundle Tasks.md edits with code edits in the same pass.
- If sync reveals a Roadmap-level drift (Phase deliverable wrong, Phase order needs change), surface it as a `/arch-update` recommendation — don't silently realign.
