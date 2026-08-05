# hongT / Cinder Court — Dungeon & Stage Code Map

Repo: `/Users/seokcmin/Desktop/hongT` · Unity WebGL top-down hack-and-slash · deterministic fixed-step sim (60 Hz).
Assemblies: `CinderCourt.Sim` (pure C#, `noEngineReferences: true`), `CinderCourt.View` (refs Sim + URP + InputSystem + UI), `CinderCourt.Tests.EditMode` (refs both). asmdefs: `Assets/Scripts/Sim/CinderCourt.Sim.asmdef`, `Assets/Scripts/View/CinderCourt.View.asmdef`, `Assets/Tests/EditMode/CinderCourt.Tests.EditMode.asmdef`.

---

## 1. Stage / dungeon data model

Two-tier design: **3 frozen sim anchors** (numeric combat truth) × **6 logical view stages** (presentation + hazard-placement overrides). Logical stages may share a sim anchor; only a non-null hazard override changes the anchor's configuration.

### Sim tier — `Assets/Scripts/Sim/CampaignTypes.cs` (FROZEN AMENDMENT)
| Symbol | Lines | Role |
|---|---|---|
| `enum HazardKind { EmberVent, ObsidianPillar, RelicAltar }` | 10 | The three gimmick archetypes |
| `struct HazardConfig` (`Kind, X, Y, Radius, Phase` + factories `Vent/Pillar/Altar`) | 16–49 | Deterministic gimmick placement record |
| `struct HazardState` (`CycleT, Telegraphing, CooldownT`) | 56–64 | Per-tick gimmick state published to view |
| `struct CampaignConfig` (`StageId, StageIndex, Waves, BossVisual, Hazards[], Weapon/Lantern/CloakRank` + derived-stat props) | 71–93 | Everything a stage feeds into the sim |
| `interface ICampaignSnapshot : ISimSnapshot` | 99–108 | Read seam: `StageId, BossAlive, StageCleared, Hazards, ranks` |
| `static class CampaignSpec` | 111–152 | All frozen campaign numerics (vent 90 px/2.4 s/0.8 s tel./8 dmg; pillar r40; altar r70/1.2 s hold/+18 oil/6 s cd; escorts `min(8, 3+2·stageIndex)`; shard drop `id%7==3`) |
| `static class CampaignStages` | 155–253 | **The 3 sim anchors**: `cinder-span` (5 waves, BossCommander, 2 vents), `abyss-chancel` (6 waves, BossCommander, 3 pillars + 1 vent), `echo-throne` (7 waves, BossMonarch, altar + 2 vents). Hazard tables at 163–182; `Build` switch at 223–252; `TryGet`/`ForIndex` at 201–221. |

A sim stage is described as: **wave count W** (waves 1..W follow the arena spawn formula, wave W+1 = boss + escorts), **boss visual**, **hazard array**, plus carried equipment ranks. No layout/geometry in sim — the arena is always the same 1536×1024 world with diamond clamp (`SimConfig.ArenaX/Y/HalfWidth/HalfHeight`, `SimTypes.cs:156–158`) and 8 fixed spawn points (`SimTypes.cs:205–210`).

### View tier — `Assets/Scripts/View/StageCatalog.cs`
| Symbol | Lines | Role |
|---|---|---|
| `struct BossPresentation` (`Visual, ResourceId, Tint, Scale, HudName`) | 9–25 | Per-stage boss skin |
| `struct StageEntry` | 28–66 | `CatalogIndex, Id, DisplayName, Kicker, Title, HazardIcon, SimAnchorId, HazardOverride[], PrereqId, TerrainId, AccentColor, Boss, StoryKey, CompanionReward` |
| `StageCatalog.AllEntries` — **6 logical stages** | 100–138 | `cinder-span`(anchor cinder-span), `ember-gallery`(anchor cinder-span + override), `abyss-chancel`(anchor abyss-chancel), `witness-well`(anchor abyss-chancel + override), `echo-throne`(anchor echo-throne), `ash-verdict`(anchor echo-throne + override). Linear prereq chain via `PrereqId`. |
| Hazard override tables | 76–98 | `EmberGalleryHazards`, `WitnessWellHazards`, `AshVerdictHazards` |
| `struct DressingPlacement` + per-stage dressing tables | 149–221 | View-only prop placement (see §3) |
| `DressingFor(stageId)` | 228–237 | Dressing lookup switch |
| `IsCleared / MarkCleared / IsUnlocked` | 255–273 | Progression predicates; `ValidClearMask = 0x3F` (line 74) hard-caps at 6 stages |

### Persistence — `Assets/Scripts/View/CampaignStore.cs`
`struct CampaignData` (lines 11–24): `ClearedMask` (**bits 0–5, six catalog stages**), equipment tiers, stats, points, relics, roster, active companion, `PrologueDone`. Key `"abyssal-lantern:unity:campaign"` (line 28). Hand-rolled fixed-shape JSON micro-parser (`Load` 32–70, `Save` 72–98) — **no external JSON dependency; adding fields means editing this parser** and its mirror `WebGLStorage.ReadCampaign`.

### Mode routing — `Assets/Scripts/Sim/HackTypes.cs` (FROZEN AMENDMENT #2)
`enum GameMode { Arena, Prologue, Dungeon }` (line 15). `struct HackConfig` (66–174): `Mode, StageId, MetaStats, EquipTiers, CompanionId, Hazards[], RosterMask, PreparationOffer`; factories `Arena()` (90), `Prologue()` (97), `TryDungeon(...)` (104–129, resolves stage via `CampaignStages.TryGet`); `ToCampaignConfig()` (136–155) — **`Hazards != null` overrides the anchor table here** (150–153).

---

## 2. Sim architecture

Single class: `Assets/Scripts/Sim/CinderSim.cs` — `sealed class CinderSim : ICinderSim, ICampaignSnapshot, IHackSnapshot, IRunPreparationSnapshot` (line 19, ~2,394 lines). No UnityEngine, no RNG, no LINQ, no per-tick allocation.

- **Constructors** = the three rule lanes: arena `CinderSim()` (196), campaign `CinderSim(in CampaignConfig)` (220), hack/dungeon `CinderSim(in HackConfig)` (249). Each lane is additive over the previous, arena numbers never move.
- **Tick entry**: `Tick(in SimInput)` (622–672). Fixed order: `CastSkills → UpdatePlayer → [Companion] → UpdateEnemies → [UpdateBossPhase (dungeon)] → [UpdateHazards (campaign)] → UpdateSkills → UpdatePickups → [UpdateExtraction (dungeon)] → UpdateWave → Publish`.
- **Enemy spawning**: `StartWave` (1887–1915) computes the queue — prologue via `HackSpec.PrologueSpawnCount`, campaign via `SpawnCountForStageWave` (474–481: waves 1..W use arena formula `min(20, 3+floor(w·1.2))` without arena's every-5th-wave boss; wave W+1 is `1 + min(8, 3+2·stageIndex)` escorts), arena via `SpawnCountForWave` (444). `UpdateWave` (1917–1953) drains the queue on a shrinking interval and flips to `WaveClear` intermission; `SpawnEnemy` (1955–2029) assigns id, spawn point `(waveSeed + id·3) % 8` (458), health curve (dungeon uses `86 + min(140,(w−1)·11)`, arena `58 + min(92,(w−1)·9)`), elite flag, boss visual (campaign: `_config.BossVisual`; arena: wave%10 rotation).
- **Win**: boss death in `DamageEnemy` (1781–1786) → `RaiseRank(StageIndex % 3)` (guaranteed slot drop) → `ClearStage()` (2195) → `ClearRun` (2198–2207): sets `_stageCleared`, `Digest.Reason = "stage-clear"`, `_mode = GameOver`, raises `SimEvents.StageCleared`, fades survivors. Prologue clear at `UpdateWave` 1945–1948 (`"prologue-clear"`).
- **Lose**: `DamagePlayer` (1490–1541) → health ≤ 0 → `_mode = GameOver`, reason `"overrun"`, `SimEvents.GameOver` (1536–1539).
- **What a stage feeds in**: exactly `CampaignConfig` (waves, boss visual, hazard array, equip ranks) + hack-lane extras from `HackConfig` (meta stats, companion id, roster mask, Ember Rest `PreparationOffer`). Nothing else — no per-stage geometry, spawn points, or enemy rosters.
- **Determinism seams**: `RunDigest` (`SimTypes.cs:107–112`), `Restart()` (485), pure static helpers (`SpawnCountForWave`, `IsBossWave`, `SpawnPointIndexFor`, `EscortCountForStage`) used directly by tests.

### FROZEN CONTRACT inventory (grep `FROZEN`)
| File | Marker | What it freezes |
|---|---|---|
| `Assets/Scripts/Sim/SimTypes.cs:1` | `// FROZEN CONTRACT` | The arena base contract: `SimMode, ActorAction, EnemyVisual, PickupKind, SimInput, PlayerState, EnemyState, PickupState, SimEvents, RunDigest, ISimSnapshot, ICinderSim, SimConfig` (all arena numerics). Edits require updating `docs/SIM_SPEC.md` "and both lanes". |
| `Assets/Scripts/Sim/CampaignTypes.cs:1` | `// FROZEN CONTRACT AMENDMENT` | Campaign amendment (hazards, `CampaignConfig`, `CampaignSpec`, the 3-stage table). **Additive only** — the default `CinderSim()` arena path must not change. Numeric truth: `docs/SIM_SPEC_CAMPAIGN.md`. That doc's §"SimTypes 증분" whitelists exactly which frozen symbols the amendment was allowed to touch. |
| `Assets/Scripts/Sim/HackTypes.cs:1` | `// FROZEN CONTRACT AMENDMENT #2` | Hack&slash amendment (GameMode, combo/dash/skills/elements/XP/elites/companion/boss-phase-2 constants in `HackSpec`, `HackConfig`, `IHackSnapshot`). Additive only over both prior lanes. Numeric truth: `docs/SIM_SPEC_HACKSLASH.md` §12. |
| `Assets/Scripts/Sim/CinderSim.cs:2` | reference | Not itself the contract, but implements it; header binds it to SimTypes (FROZEN) + docs. |
| `Assets/Tests/EditMode/CinderSimTests.cs:2` | reference | Tests pinned to the frozen contract. |

Regression locks: `HackSimTests.Regression_HackArenaConfig_ReproducesTheFrozenArenaRun` and `Regression_CampaignConstructor_StillProducesItsOwnDigest` (`Assets/Tests/EditMode/HackSimTests.cs:270, 290`) digest-compare the lanes. Also soft-frozen: `StoryCatalog` lines ("frozen — do not edit", `StoryCatalog.cs:24–25`).

**The established procedure for extending the sim is a new additive amendment**: new spec doc + new `*Types.cs` file + new `CinderSim` constructor/branch, older lanes digest-locked. (Precedent chain: SIM_SPEC → SIM_SPEC_CAMPAIGN → SIM_SPEC_HACKSLASH; `RunPreparationSnapshot.cs` shows the lighter pattern — a new additive read interface instead of amending `IHackSnapshot`.)

---

## 3. View side — how a stage is rendered

Orchestrator: `Assets/Scripts/View/GameDirector.cs` (single-scene state machine Lobby ↔ Prologue/Dungeon/Arena, line 1–9).

- **Stage start**: `StartDungeon(string stageId, PreparationOffer)` (244–276). Resolves `StageCatalog.TryGet` → builds `HackConfig.TryDungeon(entry.SimAnchorId, metaStats, equipTiers, companion, rosterMask)` → applies `entry.HazardOverride` (261–262) → `SetStageTerrain(entry.TerrainId)` (267) → `ApplyStageDressing(entry.Id)` (268) → input/camera profiles → story beat speech (274–275). Deep link: `?mode=campaign&stage=<id>` (75–79).
- **Terrain**: `SetStageTerrain` (105–122) instantiates `Resources/Terrain/terrain-<stageId>` at `ViewWorld.ToWorld(768, 512, 0)`; null returns to the base court plate; a stage without a prefab silently keeps the court (117). Existing prefabs: `Assets/Resources/Terrain/terrain-{cinder-span, abyss-chancel, echo-throne}.prefab`, produced by `Assets/Editor/TerrainImportPipeline.cs` from `Assets/Art/Terrain/*.fbx` (Blender `tools/blender/convert_terrain.py` → URP/Unlit, baked-light plates).
- **Per-stage dressing** (visual theming without new terrain): `ApplyStageDressing` (130–181) clones named children of the `cinder-span` library prefab (`StageCatalog.DressingLibraryTerrainId`, `StageCatalog.cs:167`) at static sim-space placements from `StageCatalog.DressingFor`. Constraints (StageCatalog.cs:140–177): placements must stay **outside the combat plane** x 248..1288 / y 334..874, clear every hazard by radius+50, deterministic, no RNG. Enforced by `StageDressingTests`.
- **Hazard rendering**: `VfxDirector.SyncHazards(IReadOnlyList<HazardState>)` (`Assets/Scripts/View/VfxDirector.cs:592+`), built once per run from the snapshot: vent = telegraph ring + imminence fill disc + eruption burst on `CycleT` wrap (608–643); pillar = unlit cylinder + base ring (697–708); altar = glow ring. `GameView` forwards hazards each frame (`GameView.cs:443–444`).
- **Per-stage theming hooks that already exist**: `StageEntry.AccentColor` (lobby accent light, `LobbyStaging.cs:88–89`), `BossPresentation` tint/scale/HUD name (`LobbyStaging.Show` 41–90, `GameView.BossNameFor` 209–212), `StageEntry.HazardIcon` (lobby card glyph), `StoryCatalog` per-stage beats (`stageStart/bossEntry/bossPhase2/completion`, `StoryCatalog.cs:10–13`), boss prefabs `Assets/Resources/Characters/{shadow-commander-boss, broken-court-monarch-boss}.prefab`.
- **Lobby**: `LobbyView` builds one card per `StageCatalog.Entries` (arrays sized by `Entries.Count`, `LobbyView.cs:75–77`; card layout `-174 - i*70`, line 395 — vertical budget is the practical cap on visible stage count). `LobbyStaging` shows the selected stage's boss diorama.
- **Clear/lose flow**: `GameDirector.OnRunEvents` (422+) persists clear via `StageCatalog.MarkCleared` (467–486: +2/+3 points, relics, companion reward), then either Ember Rest routing to the direct catalog successor (`HasDirectEmberRestSuccessor` 289–297 — assumes **linear** CatalogIndex+1 succession) or HUD ceremony (`GameView.cs:354–362` shows terminal panel only on the last catalog stage).

---

## 4. Data-driven surfaces

**There are none.** No JSON, no ScriptableObjects, no StreamingAssets (`grep ScriptableObject|StreamingAssets|\.json` over `Assets/Scripts` → zero hits; no `Assets/StreamingAssets/` directory). All stage content is compiled static C# tables (`CampaignStages`, `StageCatalog`, `StoryCatalog`) plus `Assets/Resources/` assets loaded by convention: `Terrain/terrain-<id>`, `Characters/<resourceId>`, `Icons/<hazardIcon>`. Adding a dungeon = code change + Resources asset + amendment doc, by design (determinism + WebGL size).

---

## 5. Existing gimmick-like mechanics (sim)

| Mechanic | Where | Notes |
|---|---|---|
| **EmberVent** — periodic AoE | `CinderSim.UpdateHazards` 2244–2258 | Cycle 2.4 s, 0.8 s telegraph, 8 dmg inside iso-radius 90; player-risk only |
| **ObsidianPillar** — movement blocker | `CinderSim.ApplyPillars` 2297–2327 | Push-out along iso normal, r40 + actor radius (player 26 / enemy 22); applied post-clamp to player, enemies, dash |
| **RelicAltar** — stand-to-bless | `UpdateHazards` 2261–2289 | 1.2 s continuous hold → +18 oil, 6 s cooldown, `SimEvents.AltarBlessing` |
| **Per-stage hazard override** | `StageEntry.HazardOverride` → `GameDirector.cs:261` → `HackConfig.ToCampaignConfig` 150–153 | Placement-level variation without touching frozen anchors — this is today's "per-dungeon gimmick" dial |
| **Elites** | `SpawnEnemy` 1971–1977, 1983–1986 | Every 7th dungeon spawn, ≤1/wave; ×3 HP, ×1.5 dmg, ×1.35 scale |
| **Elite corpse extraction** | `DropCorpse` 1005, `UpdateExtraction` 1025–1092, `CompleteExtraction` 1094–1108 | 10 s corpse, 2 s channel in r90 → new companion roster bit +8% dmg, or duplicate → +30 relics |
| **Boss phase 2** | `UpdateBossPhase` 1190–1225 | At 50% HP: ×1.25 speed/damage, `SimEvents.BossPhase2`; Monarch adds 3 escorts to the live spawn queue |
| **Boss escorts** | `SpawnCountForStageWave` 474–481, `EscortCountForStage` 465–468 | `min(8, 3 + 2·stageIndex)` |
| **Elemental cycle** | `HackSpec.ElementOf/Beats/Matchup` (`HackTypes.cs:366–399`) | ember>frost>veil>void; skills ±20%/−15%; per-visual enemy elements |
| **Ember Rest** (between-stage prep rooms) | `CinderSim.BeginEmberRest/TrySelectPreparation/DeferPreparation/EndEmberRest` 530–595, offers hashed at 597–620; `RunPreparationSnapshot.cs` (offer kinds Stat/SkillRune/GuardianResonance); director routing `GameDirector.cs:287–348` | Deterministic 3-offer choice carried into the **next** stage's `HackConfig.PreparationOffer` |
| **Boss visual variants** | `EnemyVisual` (`SimTypes.cs:15`, frozen), `BossPresentation` per logical stage | Same combat numbers, different skin/tint/scale/name |

---

## 6. Test layout

`Assets/Tests/EditMode/` — 16 files, **129 `[Test]` methods** total (EditMode only; no PlayMode folder).

| File | Tests | Stage/campaign relevance |
|---|---|---|
| `HackSimTests.cs` | 46 | Dungeon lane: combo/dash/skills/elements/XP/elites, prologue, **frozen-lane digest regressions** (270, 290) |
| `CinderSimTests.cs` | 20 | Frozen arena lane |
| `HudLayoutTests.cs` | 14 | Dungeon HUD, Ember Rest panel, retry modal |
| `CampaignSimTests.cs` | 11 | **Stage table vs amendment (28), boss wave composition (68), full-stage bot clear (108), shard drops (151), boss drop by stage index (170), vent/pillar/altar behaviour (191–317), campaign determinism (320)** |
| `StageCatalogTests.cs` | 8 | **Six ordered unique stages (36), anchor resolution (81), prereq chain (109), terrain resources exist (129), composite hazard placement contracts (147), legacy save migration (173–228)** |
| `KeyVaultTests.cs` | 8 | — |
| `StageDressingTests.cs` | 5 | **Dressing tables: distinct, valid library children, outside combat plane, hazard clearance, deterministic** |
| `EquipPropTests.cs` | 5 | Prop prefab budgets |
| `TerrainPartsTests.cs` | 2 | **terrain-abyss-chancel split parts, terrain-echo-throne slab-only** |
| `PresentationFeedbackTests.cs` | 2 | Boss intro / stage-clear ceremony |
| `LanternReaverPrefabTests.cs`, `CompanionCommandParserTests.cs` | 2+2 | — |
| `GameDirectorCampaignRouteTests.cs` | 1 | Lobby font glyph coverage |
| `WebGlTextureCapTests.cs`, `CharacterRosterAnimationTests.cs`, `BuildScriptWebGlPostprocessTests.cs` | 1 each | — |

---

## 7. Insertion points for new dungeons + gimmicks

### A. New logical stage on an existing sim anchor (cheapest; no frozen-file edits)
1. `StageCatalog.cs` — append a `StageEntry` to `AllEntries` (100–138) with `SimAnchorId` ∈ {cinder-span, abyss-chancel, echo-throne}, a `HazardConfig[]` override for placement variation, `PrereqId` for chain position, and **widen `ValidClearMask` (line 74) past `0x3F`**.
2. `CampaignStore.cs` — `ClearedMask` comment/contract says "bits 0–5" (line 13–14); >6 stages needs a persistence-version note (the mask int itself has headroom; the mask-cap in `MarkCleared` is what clamps).
3. `StoryCatalog.cs` — add the stage's four beats (new stage ids are additive; existing lines frozen).
4. Optional visuals: dressing table + `DressingFor` case (`StageCatalog.cs:228–237`), or a new terrain prefab (see C).
5. Tests to extend: `StageCatalogTests` (count/chain/terrain/hazard-contract assertions currently hardcode six), `StageDressingTests.DressedStages` list.
- **Constraint**: wave count, boss visual, escort scaling and enemy curves come from the *anchor* — a logical stage cannot change them. `EnemyVisual` (frozen `SimTypes.cs:15`) caps boss skins at Commander/Monarch unless amended.

### B. New sim anchor (new wave count / boss / hazard set) — frozen-amendment protocol
1. Update `docs/SIM_SPEC_CAMPAIGN.md` (or author a new amendment doc) — the file headers make this mandatory.
2. `CampaignTypes.cs` — add id to `CampaignStages.AllIds` (161) and a branch in `Build` (223–252). Escort count auto-scales via `StageIndex`.
3. `RaiseRank(_config.StageIndex % 3)` (`CinderSim.cs:1784`) — slot rotation extends automatically.
4. Digest-lock the older lanes stays intact as long as changes are constructor-gated (`_campaign`/`_dungeon` flags), mirroring how HackTypes layered on.
5. Add `CampaignSimTests` rows (`StageTable_MatchesAmendment`) + a bot-clear test.

### C. New terrain / environment dressing (view-only)
- Author FBX in `Assets/Art/Terrain/` → `TerrainImportPipeline` (`Assets/Editor/TerrainImportPipeline.cs`) → `Assets/Resources/Terrain/terrain-<id>.prefab` → point `StageEntry.TerrainId` at it. `SetStageTerrain` needs no change (convention-based `Resources.Load`, `GameDirector.cs:116`).
- Or reuse the dressing-library route: new `DressingPlacement[]` table honoring the combat-plane/hazard-clearance constants (`StageCatalog.cs:171–173`); `StageDressingTests` enforce the contract mechanically.

### D. New gimmick *kind* (the real "per-dungeon gimmick" seam) — frozen-amendment protocol
Touch list, in dependency order:
1. Amendment doc (numeric truth first — house style).
2. `CampaignTypes.cs`: extend `HazardKind` (10), add a `HazardConfig` factory (16–49), constants in `CampaignSpec` (111–152), any new per-tick fields on `HazardState` (56–64).
3. `CinderSim.cs`: behaviour branch in `UpdateHazards` (2235–2290) for timed/positional effects, or `ApplyPillars`-style movement hook (2297–2327) called from `UpdatePlayer`/`UpdateEnemy`/`UpdateDash`; new `SimEvents` flag if the view needs a one-shot (follow the `SimEvents` bit-allocation ledger in `docs/SIM_SPEC_CAMPAIGN.md` §"SimTypes 증분" — bits ≤13 are taken through the campaign amendment; hack amendment claimed more, check `SimTypes.cs:78–105`).
4. `VfxDirector.cs`: render branch in `SyncHazards` build/update switch (592–708) + optional `OnEvents` burst (203+).
5. `StageCatalog.cs` / `CampaignStages`: place it in hazard tables; `StageDressingTests` hazard-clearance helper and `StageCatalogTests.CompositeHazards_MatchPlacementAndClearanceContracts` (147) need the new radius constant.
6. `CampaignSimTests`: one behaviour test per gimmick (the vent/pillar/altar trio at 191–317 is the template) + rerun the determinism digest test.
- Note `UpdateHazards` is gated `_campaign && !GameOver` (`Tick`, 656–659) and hazards are **player-risk only** by doctrine (`CinderSim.cs:2255`) — enemy-affecting gimmicks would be a deliberate doctrine change to spec first.

### E. Campaign-structure seams to watch
- **Linear-succession assumption**: Ember Rest routes only to `CatalogIndex + 1` (`GameDirector.HasDirectEmberRestSuccessor`, 289–297); branching/nonlinear campaigns must generalize this.
- **Terminal-stage check**: clear ceremony keys off `CatalogIndex == Entries.Count - 1` (`GameView.cs:359–362`).
- **Ember Rest room index** is `entry.CatalogIndex` and `BeginEmberRest` validates `1..5` (`CinderSim.cs:532`) — more than 6 stages breaks this bound.
- **Lobby layout**: stage cards stack at fixed 70 px pitch (`LobbyView.cs:395`); >6 entries needs scroll/paging.
- **Deep link surface**: `?mode=campaign&stage=<id>` (`GameDirector.cs:75–79`) picks up new ids for free.
