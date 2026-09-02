# CLAUDE.md

Unity/C# architecture and coding rules for this repo. Respond in Korean (한국어로 답변).

## 1. Architecture & Managers

- Common systems (state, sound, UI) → dedicated Manager classes.
- Singleton only on Managers (`GameManager.Instance`). Never on regular objects.
- One script, one job (e.g. split Player into `PlayerMovement`, `PlayerHealth`, `PlayerInventory`).

## 2. Object Communication

- No `GameObject.Find()` — use Inspector drag-and-drop or Manager-cached refs.
- Prefer C# `event`/`Action` over direct calls (e.g. Player fires "damaged"; UI subscribes and updates itself).

## 3. Object References

- Prefer `GetComponent` over Inspector assignment, except when justified.
- No `Find`-family calls (`GameObject.Find`, `transform.Find`, ...) — runtime cost.
- No string comparison for tags/names — use enums/IDs/direct refs.

## 4. Resources & Assets

- Dynamic/repeated objects must be prefabs. Edit only the source in Prefab Mode, never scene instances.
- Prefix naming: `P_` prefab, `T_` texture, `M_` material.
- Prefabs under `@Resources`, one subfolder per category.

## 5. No Per-Frame Allocation

- No allocation inside `Update()`/per-frame code, except when justified.

## 6. Planning Kickoff Prompt

With `CLAUDE.md` + `Plan.md` both provided, use as session-start prompt:

> We're starting Unity/C# game development now. The attached CLAUDE.md is our architecture and coding convention rules, and Plan.md is the design spec for the system to implement. Fully internalize both documents. Once you've understood them, don't write any code yet — summarize the core game loop from the design in exactly 3 lines.
