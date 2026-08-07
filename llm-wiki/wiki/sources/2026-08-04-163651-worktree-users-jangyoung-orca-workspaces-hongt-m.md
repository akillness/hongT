---
type: "source-summary"
captured_at: "2026-08-04T16:36:51.157430+00:00"
raw_path: "raw/sources/prompts/2026/08/04/163651-019fcda2-50b-worktree-users-jangyoung-orca-workspaces-hongt-m.md"
session_id: "019fcda2-50be-79d0-a055-b988fb408204"
---

# Worktree: /Users/jangyoung/orca/workspaces/HongT/main (Unity project). You own…

- Raw capture: `raw/sources/prompts/2026/08/04/163651-019fcda2-50b-worktree-users-jangyoung-orca-workspaces-hongt-m.md`
- Filed query: [[wiki/queries/2026-08-04-163651-worktree-users-jangyoung-orca-workspaces-hongt-m]]

## Prompt Excerpt

```text
Worktree: /Users/jangyoung/orca/workspaces/HongT/main (Unity project). You own ONLY these three files: Assets/Scripts/View/HudView.cs, Assets/Scripts/View/GameView.cs, Assets/Scripts/View/GameDirector.cs. Do NOT touch CameraRig/LobbyView/ViewPrefs files, sim, tests, docs, config, or assets - those are owned by parallel workers. Do NOT run git commands. Do NOT run broad tests or a formatter.

FIRST STEP - MANDATORY: Read the full current content of all three target files before writing anything. A prior agent (and a live sibling) have already made partial/legitimate edits to GameView.cs (e.g. an EquipDropped block, Rig-related code). Another peer just confirmed GameView.cs currently compiles cleanly (structurally valid, 74/74 EditMode tests pass) as of this message. Preserve every existing addition and existing behavior in these files; you are COMPLETING a partial Cycle2 presentation contract, not rewriting these files. Do not remove or restructure code that isn't directly in conflict with the tasks below. Reuse existing patterns/utilities/factories already present (Sync loop, OnEvents subscription pattern, HUD element factories, existing damage-number pool, existing stage-complete screen, existing game-over screen) rather than inventing new ones.

GOAL: Implement Cycle2 A1-A5 UI/presentation contract for a boss-battle/dungeon-crawler View layer (Unity uGUI, no MonoBehaviour Update() polling beyond what already exists - hook off existing event dispatch / Sync tick pattern used in the file). Concretely:

A1 - Boss intro: On a BossSpawned event, show for exactly 0.45 seconds: (1) an HUD letterbox effect (top/bottom bars sliding in or fading in, covering media), (2) a centered boss nameplate label with the boss name, (3) trigger a boss CameraRig focus pull (call into whatever existing CameraRig focus API already exists in the codebase - grep for it first; if no such hook exists yet, add the smallest possible public method call, do not build a new camera system). After 0
```
