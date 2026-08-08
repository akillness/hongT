---
title: "Worktree: /Users/jangyoung/orca/workspaces/HongT/main (Unity project). You own…"
created_at: "2026-08-04T16:36:51.157430+00:00"
section: "queries"
status: "submitted"
session_id: "019fcda2-50be-79d0-a055-b988fb408204"
raw_prompt: "`raw/sources/prompts/2026/08/04/163651-019fcda2-50b-worktree-users-jangyoung-orca-workspaces-hongt-m.md`"
source_summary: "[[wiki/sources/2026-08-04-163651-worktree-users-jangyoung-orca-workspaces-hongt-m]]"
---

# Worktree: /Users/jangyoung/orca/workspaces/HongT/main (Unity project). You own…

## Question

Worktree: /Users/jangyoung/orca/workspaces/HongT/main (Unity project). You own ONLY these three files: Assets/Scripts/View/HudView.cs, Assets/Scripts/View/GameView.cs, Assets/Scripts/View/GameDirector.cs. Do NOT touch CameraRig/LobbyView/ViewPrefs files, sim, tests, docs, config, or assets - those are owned by parallel workers. Do NOT run git commands. Do NOT run broad tests or a formatter.

FIRST STEP - MANDATORY: Read the full current content of all three target files before writing anything. A prior agent (and a live sibling) have already made partial/legitimate edits to GameView.cs (e.g. an EquipDropped block, Rig-related code). Another peer just confirmed GameView.cs currently compiles cleanly (structurally valid, 74/74 EditMode tests pass) as of this message. Preserve every existing addition and existing behavior in these files; you are COMPLETING a partial Cycle2 presentation contract, not rewriting these files. Do not remove or restructure code that isn't directly in conflict with the tasks below. Reuse existing patterns/utilities/factories already present (Sync loop, OnEvents subscription pattern, HUD element factories, existing damage-number pool, existing stage-complete screen, existing game-over screen) rather than inventing new ones.

GOAL: Implement Cycle2 A1-A5 UI/presentation contract for a boss-battle/dungeon-crawler View layer (Unity uGUI, no MonoBehaviour Update() polling beyond what already exists - hook off existing event dispatch / Sync tick pattern used in the file). Concretely:

A1 - Boss intro: On a BossSpawned event, show for exactly 0.45 seconds: (1) an HUD letterbox effect (top/bottom bars sliding in or fading in, covering media), (2) a centered boss nameplate label with the boss name, (3) trigger a boss CameraRig focus pull (call into whatever existing CameraRig focus API already exists in the codebase - grep for it first; if no such hook exists yet, add the smallest possible public method call, do not build a new camera system). After 0.45s everything must cleanly hide. Critically: the letterbox/nameplate timer state and any "already showing" guard flag MUST fully reset when a new run starts (look for existing run-reset / retry entry points in GameDirector.cs) and must not leak/re-trigger stale state across a retry or new run - i.e. if a retry happens mid-fade, the flag and timer must be zeroed, not left mid-count.

A2 - StageCleared: On a StageCleared event, show for exactly 0.5 seconds a blue radial pulse effect (radial-fill Image or similar existing UI primitive, animated via Sync-driven property mutation, not a coroutine-per-event / not per-frame Instantiate) plus a centered "district clear" banner. Do NOT spawn any new persistent/non-stop UI panel - only reuse the existing stage-completion screen that must already exist in these files; the pulse+banner are transient overlays that hide after 0.5s.

A3 - Death: On player death, show ONLY health-delta damage numbers (i.e., reuse the existing damage-number popup mechanism to show damage taken, not a special death number type) with a 1.18x "punch" scale animation (scale bounces to 1.18x then settles to 1x - use existing punch/scale-tween utility if one exists in these files, otherwise a minimal Sync-driven lerp). Do NOT trigger a second persistent game-over UI artifact - there must be exactly ONE persistent game-over screen shown (find the existing one and make sure death doesn't also spawn a second competing overlay/panel).

A4 - Damage numbers: All normal damage numbers must use an "enemy palette" color (find/reuse existing enemy color constant or palette already referenced in the file; if only a placeholder exists, use the existing convention). Damage numbers originating from a ComboFinisher event/hit must use gold color instead. The existing damage-number object pool's cleanup/return-to-pool path must fully clear stale state (reset color, scale, alpha, position, active text) so a reused pooled instance never shows leftover data from a previous popup.

A5, B0, B2, B5: These reference other partial event display/persistence behavior already present in the files from prior work. Do NOT regress any of it - if you find existing code handling these areas, leave it working as-is unless it directly conflicts with A1-A4 changes above.

HARD CONSTRAINTS:
- Any new passive/decorative UI element (letterbox bars, banners, radial pulse, nameplate) MUST have raycastTarget = false on its Graphic/Image/Text component. Only an explicit full-screen modal/backdrop or an actual interactive button may have raycastTarget = true.
- Reuse the existing uGUI factory/builder methods already in these files to construct any new UI elements (do not hand-roll new GameObject/Instantiate boilerplate if a factory already covers similar elements - extend a factory minimally only if genuinely missing).
- Preserve the static hierarchy - do not create/destroy GameObjects per event/per frame; toggle activeSelf and mutate existing component properties instead.
- Zero per-frame heap allocations in any Sync/Update-equivalent hot path you touch or add (no new Lists/arrays/string concatenation/boxing per frame; use pooled/pre-allocated fields).
- All active-state flags and timers you add or touch (boss intro guard, stage-clear pulse timer, etc.) must be explicitly reset to their default/inactive state both (a) when a new run starts, and (b) whenever ShowGameOver (or equivalent existing game-over trigger method) is invoked - locate the exact existing reset/retry method(s) in GameDirector.cs and GameView.cs and add resets there rather than inventing a new reset pathway.

VERIFICATION - MANDATORY LAST STEP: After edits, run exactly this one command from the worktree root and report its full output: bash tools/unity_batch.sh import-only
That command must return success (Unity import completes without compile errors). Do not run any other test, git, or formatter command.

REPORT: In your final summary, list the exact changed symbols/methods per file (HudView.cs, GameView.cs, GameDirector.cs), confirm no other files were touched, confirm which existing behaviors/factories you reused vs. minimally extended, and paste the exact result of the unity_batch.sh import-only command.

## Answer

- [ ] Fill this after the answer becomes worth keeping

## Evidence and Citations

- [[wiki/sources/2026-08-04-163651-worktree-users-jangyoung-orca-workspaces-hongt-m]]
- `raw/sources/prompts/2026/08/04/163651-019fcda2-50b-worktree-users-jangyoung-orca-workspaces-hongt-m.md`
