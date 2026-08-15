# Asset Lane 4 — Presentation Image Gap Audit

`git tag -f pre-asset-lane4-20260808` (already present on repo before this
session started — see below). No files were modified or created by this
lane; no image generation was performed. Rationale is §5.

## 0. Method

Full sweep of `Assets/Scripts/View/**` for every `Resources.Load<Sprite>` /
`Resources.Load<Texture2D>` callsite (images only — materials, fonts, audio
clips, and GameObject/prefab loads are out of this lane's scope per the
brief). Each callsite's resource key(s) — literal or data-driven — were
cross-checked against the file that actually exists under `Assets/Resources/`
at the time of the audit (2026-08-08, mid-session; three other asset lanes
were active concurrently in `Assets/Resources/Icons/` and touched files as
recently as 16:44 today).

## 1. Deficit audit — result: zero dead references found [OBSERVED]

Every `Resources.Load` callsite for an image resolves to a file that exists
on disk right now. Table:

| Callsite | Key(s) | Resolves to | Status |
|---|---|---|---|
| `HudView.cs:1211-1213` `TryLoadOptionalSprite` | `Icons/{regenerated,generated,}/ui-ember-rest-bg` | `Icons/ui-ember-rest-bg.png` | present |
| `HudView.cs:2593` | `Icons/hud-combo-pip-gem` | present | present |
| `HudView.cs:3269` `ApplyFrameOverlay` | `hud-hp-bar-frame`, `hud-oil-bar-frame`, `hud-xp-bar-frame`, `hud-boss-bar-frame`, `hud-shield-readout-frame`, `hud-extraction-ring-frame`, `hud-skill-card-frame(-ready)` (all call sites, see `frameSpriteId=` grep) | all present | present |
| `HudView.cs:3435,3457,3523,3695,4240,4242` | `ui-button`, `ui-codex`, `ui-abandon`, joystick base/nub, skill-card frames | all present | present |
| `HudViewCodex.cs:565` | `stat-attack`, `stat-vitality`, `stat-swiftness`, `equip-lantern` | all present | present |
| `HudViewCodex.cs:689` `CodexChipIcon` | `GuidanceCatalog.GroupIcon` → `skill-nova`/`skill-dash`/`pickup-ember`/`skill-aegis`/`pickup-relic` | all present (reused player-skill/pickup icons — see §2.2) | present |
| `HudViewCodex.cs:716` | `ui-button-active` / `ui-button` | present | present |
| `CutsceneView.cs:54` | `Scenes/scene-intro`, `Scenes/scene-transition`, `Scenes/scene-boss-entry`, `Scenes/scene-stage-entry` (only 4 distinct literals exist in the codebase — see §2.1) | all present (+ `scene-ember-rest.png` unused by any current callsite, confirmed via `rg '"scene-'`) | present |
| `TerrainFlipbook.cs:399` | `Terrain/terrain-fx-{lava,ice,shift}-sheet` | all present | present |
| `EnvironmentBuilder.cs:492-493` | `Textures/Env/{stageId}-stone`, `{stageId}-floor` for all 9 stage ids | all 9×2 = 18 files present | present |
| `VfxDirector.cs:1910` `PickupIcons` | `pickup-ember`, `pickup-flask`, `pickup-relic`, `equip-weapon` | all present | present |
| `MetaScreenView.cs:640,696` | `ui-button-active`/`ui-button`, `stat-attack`/`stat-vitality`/`stat-swiftness`/`equip-lantern` | all present | present |
| `LobbyView.cs:475` `RailIconIds` | `ui-sanctum`, `ui-sortie`, `ui-map` | all present | present |
| `LobbyView.cs:1552` | `entry.HazardIcon` per `StageCatalog` entry → `skill-nova`/`skill-aegis`/`skill-pulse`/`skill-dash`/`skill-ward`/`skill-strike` (all 9 stages) | all present (reused player-skill icons — see §2.2) | present |
| `LobbyView.cs:2169,2217,2243` | `ui-button`, `ui-button-active`, dynamic `iconId` row icons | all present | present |
| `HudIconIntegration.cs:121-128` | all 27 keys in `IconThemeMap` | all present | present |

Confirms the earlier hypothesis in the brief ("9 stages sharing 3 cutscene
frames") is real, but it is the **only** duplication-type gap, and every
single sprite/texture key the game actually asks for at runtime already has
a file backing it. The Icons/generated, Icons/regenerated, ui-map/sanctum/
sortie/codex/abandon, and stage stone/floor textures were evidently closed
out by concurrent lanes very recently (several `Icons/*.png` timestamps are
today 16:44, i.e. mid-session).

## 2. Genuine presentation debt found (all blocked by the no-script-edit constraint)

None of these are "null → silent failure" bugs (nothing crashes or renders a
white quad); they are content-reuse limits baked into hardcoded strings in
`Assets/Scripts/**`, which this lane is explicitly forbidden to touch.
Recorded here as **코드 연동 필요**, not attempted:

### 2.1 Cutscene frames: 9 stages + 3 boss archetypes → 3 shared images
`GameDirector.cs:513-519`:
```csharp
var cutsceneSprite = preparation.IsValid
    ? "scene-transition"
    : entry.Boss.Visual == EnemyVisual.BossMonarch
        ? "scene-boss-entry"
        : "scene-stage-entry";
```
Every one of the 9 `StageCatalog` entries collapses onto exactly one of 3
literal sprite names — none of the per-stage identity (accent color, terrain
id, boss archetype name: Cinder Warden / Veil Tactician / Gate Sovereign /
Sluice Keeper / Bastion Sentinel / Ash Magistrate) reaches the loading-screen
art. To ship distinct stage-entry/boss-entry frames, `GameDirector.cs:513-519`
would need to build a stage- or archetype-qualified sprite name (e.g.
`"scene-stage-entry-" + entry.Id`) and `CutsceneView.Show` would need no
change (it already takes an arbitrary `spriteName` and degrades gracefully
on a miss). **This is the single highest-value fix if code edits are ever
authorized for this lane**, since it's a 3-line change plus N new frames.

### 2.2 Hazard-family glyphs reuse the player's own skill icons
`StageCatalog.cs` (`HazardIcon` field per `StageEntry`, e.g. lines 164, 172,
180, 188, 196, 204, 215, 223, 231) and `GuidanceCatalog.cs:263-267`
(`GroupIcon`) both point the lobby stage-card glyph and the in-run codex
chip icon at `skill-nova` / `skill-aegis` / `skill-pulse` / `skill-dash` /
`skill-ward` / `skill-strike` / `pickup-ember` / `pickup-relic` — the exact
same files the HUD uses for the player's own Nova/Aegis/Pulse/Dash/Ward/
Strike skill cards. There is no dedicated icon set for the 6 actual hazard
kinds (`Vent`, `Pillar`, `Altar`, `Current`, `Pylon`, `Wall`). Repainting the
shared `skill-*.png` files would corrupt the player's HUD skill cards (same
file, same identity), so this cannot be fixed by dropping new art under the
existing names — it needs new hazard-specific icon files **and** a literal
string change in `StageCatalog.cs`/`GuidanceCatalog.cs` to point at them.

### 2.3 Companion skills and roster have no icon/portrait at all
`CompanionSkillId` (`Volley`/`Hex`/`Quake`/`Flare`, `Sim/HackTypes.cs:26`) is
only ever turned into a **color** (`VfxDirector.cs:697-701`) and a borrowed
enemy silhouette (`VfxDirector.cs:723-727`) — no `Resources.Load<Sprite>`
call exists for a companion-skill icon anywhere in the codebase, so there is
no hook to attach new art to without adding a new loader call. Likewise
`LobbyView`'s roster rows (`CompanionIds`/`CompanionNames`/`CompanionEpithets`,
`LobbyView.cs:113-119`) render name + epithet text only, no portrait.

## 3. Generated files

None. See §5.

## 4. Commands run (audit only, no writes)

```
git status --short
git tag -f pre-asset-lane4-20260808
rg -n 'Resources\.Load<Sprite>|Resources\.Load<Texture' Assets/Scripts
rg -n '"scene-' Assets/Scripts
rg -n '"Icons/' Assets/Scripts/View/*.cs -o
grep -rn 'CompanionSkillId' Assets/Scripts/
grep -n 'HazardIcon' Assets/Scripts/View/StageCatalog.cs
grep -n 'GroupIcon' -A8 Assets/Scripts/View/GuidanceCatalog.cs
(+ targeted Read of GameDirector.cs, CutsceneView.cs, StageCatalog.cs,
  HudView.cs, HudViewCodex.cs, LobbyView.cs, MetaScreenView.cs,
  VfxDirector.cs, HudIconIntegration.cs, TerrainFlipbook.cs,
  EnvironmentBuilder.cs, GameBootstrap.cs, LobbyStaging.cs)
ls Assets/Resources/{Icons,Icons/generated,Icons/regenerated,Scenes,
  Textures/Env,Materials,Terrain,Characters,Props}
```

## 5. Why nothing was generated this lane

Per §1, an exhaustive sweep of every `Resources.Load<Sprite>` /
`Resources.Load<Texture2D>` callsite in `Assets/Scripts/View/**` found no
resource key that fails to resolve to an existing file — the concurrent
lanes running in this same session had already closed every literal gap
(icon regen batches, `ui-map`/`ui-sanctum`/`ui-sortie`/`ui-codex`/
`ui-abandon`, `ui-ember-rest-bg`, all 9 stage stone/floor texture pairs).

The three remaining presentation-content limits found (§2) are real but are
all blocked by the hard constraint on this lane: none of them can be closed
by dropping new files under names the existing loaders already consume —
each requires a literal-string change inside `Assets/Scripts/View/**`
(`GameDirector.cs`, `StageCatalog.cs`, `GuidanceCatalog.cs`, or a new
`Resources.Load` call for companion portraits), which this lane is
explicitly forbidden to touch. Generating speculative art with no code path
to load it would sit unused in `Assets/Resources/` and risk colliding with
the other three asset lanes' concurrent work in the same directories, so no
files were written.

**Recommendation for the team lead**: §2.1 (cutscene frames) is the
highest-value item — a 3-line `GameDirector.cs` change (stage/archetype-
qualified sprite name, with `CutsceneView`'s existing null-degrades-gracefully
behavior as the safety net) would let a follow-up asset lane ship up to 9
stage-entry + 3 boss-archetype-entry frames without any other code change.
If that edit is authorized (by the team lead or a lane with
`Assets/Scripts/View/**` write access), re-run this lane to generate the art.

## 6. Blockers

None tool/API-side. The only blocker is scope: the constraint against
editing `Assets/Scripts/**` means the identified gaps (§2) cannot be
exploited by this lane without a code change from elsewhere.
