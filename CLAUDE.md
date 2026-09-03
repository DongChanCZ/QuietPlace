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
- LabHorror map (`Assets/LabBlockout/Prefabs/LabHorror_Blockout_Step7_3F_Floor.prefab`) is live-linked into `GameScene` as a connected prefab instance. `LabHorror_Blockout_Step7_3F_Floor.unity` is a preview/test scene for the same prefab — edits made there (doors, props, lighting, etc.) do NOT reach `GameScene` until they're applied back to the prefab. Always **Apply to Prefab** (or edit directly in Prefab Mode) before committing; saving only the scene leaves `GameScene` out of sync.
- All scenes live under `Assets/@Scenes/` (`MainMenu.unity`, `Intro_Cutscene.unity`, `GameScene.unity`, `QTE_Test.unity`, `SampleScene.unity`). `Assets/Scenes/` is now empty/unused — don't create new scenes there. Scene transitions load by name (`SceneManager.LoadScene("GameScene")`), never by path, so moving scene files around doesn't break references — only Build Settings' scene list needs to stay in sync.
- Exception: `LabHorror_Blockout_Step7_3F_Floor.unity` stays in `Assets/LabBlockout/Scenes/`, NOT `@Scenes`. It's the live prefab-preview workspace a teammate actively edits (see line above) — moving it risks a path-rename conflict against their in-progress local changes for no real benefit, since it's a preview/test scene rather than part of the MainMenu/Intro/Game flow.

## 5. No Per-Frame Allocation

- No allocation inside `Update()`/per-frame code, except when justified.

## 6. Planning Kickoff Prompt

With `CLAUDE.md` + `Plan.md` both provided, use as session-start prompt:

> We're starting Unity/C# game development now. The attached CLAUDE.md is our architecture and coding convention rules, and Plan.md is the design spec for the system to implement. Fully internalize both documents. Once you've understood them, don't write any code yet — summarize the core game loop from the design in exactly 3 lines.
