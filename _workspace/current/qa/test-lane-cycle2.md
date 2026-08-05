# Test Lane — Cycle 2 (dungeon expansion) EditMode additions

2026-08-05 · TestLane · run-id 20260805-dungeon-gimmicks.
Scope: `Assets/Tests/EditMode/` only (no Sim/View source edits, nothing staged).
Numeric truth: `docs/SIM_SPEC_DUNGEONS.md` (AMENDMENT #5) · goldens:
`qa/golden-digests-cycle2.md` · plan rows: `qa/test-plan.md`.

## Verification (this machine has no Unity editor)

All four touched test classes were compiled AND executed against the **real**
repo sources (`Assets/Scripts/Sim/*.cs`, `StageCatalog.cs`, `CampaignStore.cs`,
`WebGLStorage.cs`) with a standalone dotnet 8 + NUnitLite 3.14 harness plus
minimal UnityEngine stubs (PlayerPrefs = in-memory dict, Resources.Load =
non-null):

```
Overall result: Passed — Test Count: 82, Passed: 82, Failed: 0, Skipped: 0
(CampaignSimTests 19 · DungeonGoldenDigestTests 6 · HackSimTests 48 · StageCatalogTests 11)
```

The 15 golden rows reproduce **byte-identically** through the same harness
(shortest-round-trip "R" float formatting, InvariantCulture). Unity EditMode
re-confirmation stays on the G6 gate as planned. `StageDressingTests` edits are
compile-verified via the same file set used by Unity (they need real prefab
transforms, not runnable outside the editor); their table numerics were
re-checked arithmetically (plane + radius+50 clearance) against ViewLane's
committed tables — all clear.

## Added / changed tests → gate map

### CampaignSimTests.cs (7 new [Test]s, BotInput now `internal static` — shared, not forked)

| Test | Gate rows | What it pins |
|---|---|---|
| `StageTable_MatchesDungeonAmendment` | R4/G2 | 6 anchors, ids order, waves 5/6/7/8/8/9, boss C/C/M/C/C/M, full hazard tables (kind/x/y/phase/push/hp) per §배치 |
| `TideCurrent_PushesPlayerInsideActiveBand` | G2 | parked bot at (600,470): x strictly ↑ on active-interior ticks, zero drift on idle ticks; in-band enemy displaced +x across one active window (symmetric doctrine) |
| `TideCurrent_SymmetricAndClamped` | G2 | held +x through 2 active windows never crosses the L1 diamond bound; lane B (phase 3, push −140) strictly ↓ |
| `EmberPylon_CombatContract` | G2/G3 | (a) 5 swings → Hp 240→0, `PylonDown` exactly once, hp pinned 0; (b) aura ratio == 0.60 exactly (two lockstep sims, pylons present vs stripped); (c) nova leaves pylon hp untouched; (d) pylon-only combo finisher raises `ComboFinisher` (hack lane) |
| `AshWall_TimetableAndTicks` | G2 | FrontX 248/368/608/448 @ t=9/12/16/20 (±80/60 px); ≥3 exact-8 band drops in [10.5,18) riding the 0.6 s grid (whole-36-tick spacing — float stage-clock may shift the boundary 1 tick, cadence is the contract); bait bot (never attacks): kills 0 @10.5 s → >0, enemy dies inside band, score credit |
| `Telegraph_CensusUnderBudget` | D3 | max simultaneous telegraphing ≤3 and ≤2 same-kind over one LCM: sluice 6 s, bastion 3 s (vent 2.4), march 90 s (LCM 22.5, 2.4) — measured maxima 1/1/2 |
| `NewStages_SameConfigSameInputs_IdenticalDigests` | D1 | kiter 1800 ticks × 2 fresh sims per new stage: digests + player X/Y identical |
| `NewStages_MutatedPlacement_ChangesDigest` | D3 | one datum mutated via `HackConfig.Hazards` clone (sluice current phase +0.6 · bastion pylon x +50 · march vent phase +0.6): digest/position CHANGES. March uses a vent-anchored fighter — the centre script provably never observes the march mutation (verified: identical digests), so the bot choice is part of the test's evidence value |

### DungeonGoldenDigestTests.cs (NEW — 6 [Test]s, 15 rows)

| Test | Gate rows | Rows |
|---|---|---|
| `Golden_ArenaHackLane_IsUnchanged` | R2 | arena-hack |
| `Golden_FrozenArenaConstructor_IsUnchanged` | R2 | arena-frozen |
| `Golden_Prologue_IsUnchanged` | R3 | prologue |
| `Golden_SixLogicalStages_AreUnchanged` | R1 | 6 logical stages @2/1/3, `StageCatalog.HazardOverride` applied exactly like `GameDirector.StartDungeon` (binds goldens to the SHIPPING catalog tables) |
| `Golden_ClassicCampaignAnchors_AreUnchanged` | R1 | classic `CinderSim(in CampaignConfig)` lane, 3 anchors @2/1/3 |
| `Golden_NewDungeonStages_MatchFirstRecording` | D5 | cinder-sluice / ember-bastion / ash-march @2/1/3 (first recording) |

Kiter = `CampaignSimTests.BotInput` (single shared body, refactored to
`internal static`; goldens were recorded against this exact byte pattern).
Ints assert exactly; floats compare through their "R" round-trip string —
bit-exact. On mismatch the assert message prints the full actual row for cheap
re-pinning; any divergence found under Unity must be recorded in
`qa/gate-measurements.md` (comment in file header says the same).

### StageCatalogTests.cs (renamed 1, extended 2, +3 new [Test]s)

| Test | Gate rows | What changed |
|---|---|---|
| `Entries_AreNineOrderedUniqueLogicalStages` (was …Six…) | R8/G7 | 9 ids/anchors/prereqs/rewards — chain ash-verdict→cinder-sluice→ember-bastion→ash-march, rewards null/null/scout-echo |
| `Entries_ResolveFrozenAnchorWithoutChangingAnchorStageId` | R1 | reward indices now `{0,0,1,1,2,2,0,1,2}` |
| `Entries_ReferenceOnlyExistingSharedTerrainResources` | G8 | +3 terrain ids: sluice=abyss-chancel, bastion=cinder-span, march=echo-throne (reuse, no new prefabs) |
| `NewStageAnchors_CarryNoOverrideAndClearRadialHazards` (new) | R4/G2 | new entries are pure anchors (override null, own SimAnchorId); anchor tables obey non-overlap + pillar 2×push-radius clearance (band kinds excluded from radial checks) |
| `MarkCleared_Index8_SurvivesSaveLoadRoundTrip` (new) | R8 | the 0x3F bug class: bit 8 survives MarkCleared → Save → Load; bits ≥9 scrubbed |
| `Load_LegacySixBitMask_LoadsIdentically` (new) | R8 | legacy `clearedMask:63` loads bit-identical; new stages read uncleared; full legacy save already unlocks cinder-sluice |

### StageDressingTests.cs (extended, no new [Test])

`DressedStages` now includes the 3 new ids (ViewLane tables); `HazardsFor`
falls back to the frozen `CampaignStages` anchor table when `HazardOverride`
is null (cycle-2 anchors). All four existing dressing contracts (library
children, plane exclusion, radius+50 hazard clearance, determinism) now cover
the new tables. Gate: G8.

### HackSimTests.cs (1 test updated)

`EmberRest_ExtendedRoomsAreAvailableAndRepeatTheirOffersDeterministically`
(gate G7/D1): boundary rejection moved 6→9 (`BeginEmberRest(9,*)` false), room
loop widened 4..5 → 4..`CampaignSpec.MaxEmberRestRoomIndex` (=8) with the same
IsValid + seed-stable-across-reopen assertions per room.

## Notes / handoffs

- **Glyph coverage (flagged per constraint):** ViewLane's cycle-2 StoryCatalog/
  StageCatalog strings add **28 Hangul glyphs** not present pre-cycle-2:
  걸꺼든떠뚫락랐러록름막멈멎방벽살새숨역위죽집춘패허형흐흘
  The existing lobby font test (`LobbyMotionLabels_UseGlyphsPresentInShippedHudKoreanFont`)
  only covers 모션 labels — it does NOT gate StoryCatalog beats. If
  `Resources/Fonts/HudKorean` is a subset font, these need a subset refresh or
  a StoryCatalog-wide glyph test (ViewLane surface; pinged via irc).
- Parked-idle at (300,560) on ash-march **dies at t≈12.9** to melee saturation
  (measured) — that's why `AshWall_TimetableAndTicks` splits into kiter
  (timetable), killer-park (clean grid drops; one-shots arrivals so contact
  grace can't mask wall ticks), and bait (environmental kill credit) runs
  instead of one parked observer.
- Wall grid drops land on t1 % 36 == 1 after ~700 ticks (float `_stageTime`
  accumulation vs the tick index) — the tests therefore assert 36-tick
  *spacing*, not absolute modulus. Deterministic, same on every run.
- `TestResult.xml` of the standalone run: `/tmp/testlane-nunit/TestResult.xml`
  (82/82). Harness sources: `/tmp/testlane-nunit/` + `/tmp/testlane-harness/`
  (calibration probes incl. golden byte-reproduction).

## v1.1 retune pass

2026-08-05 · TestRetune · Stage-2 "gimmicks must be unmissable" re-pin.
Numeric truth: `docs/SIM_SPEC_DUNGEONS.md` REVISION v1.1 +
`design/gimmick-retune-spec.md`. Scope: `Assets/Tests/EditMode/` only
(CampaignSimTests, DungeonGoldenDigestTests). StageCatalogTests /
StageDressingTests / HackSimTests needed **no** edits (verified below).
Nothing staged, no sim/view sources touched.

### Verification (standalone dotnet 8 + NUnitLite 3.14, real repo sources)

```
Overall result: Failed — Test Count: 92, Passed: 87, Failed: 5
  CampaignSimTests          21/21
  DungeonGoldenDigestTests   1/6   (see below — the 5 fails are EXPECTED here)
  HackSimTests              54/54
  StageCatalogTests         11/11
```

The 5 "failures" are exactly the 5 golden tests whose literals are **Unity**
recordings (arena-hack, arena-frozen, prologue, six-logical-stages,
classic-anchors): under dotnet they diverge only in the trailing X/Y floats
(known ~4 ULP dotnet↔Unity drift, header-documented); **every int field is
byte-identical**, which re-proves the v1.1 retune left all 12 pre-existing
lanes untouched (R1-R3). `Golden_NewDungeonStages_MatchFirstRecording` (the
one test whose rows I re-pinned) passes 1/1 on this harness by construction.
Under Unity the polarity flips: the 5 pass, the new-stage row floats may fail
once — main lane re-pins them from the assert message (protocol in the file
header + test comment). `TestResult.xml`: `/tmp/testlane-nunit/`.
Calibration probes: `/tmp/simgate/RetuneProbe.cs` (+ `/tmp/simgate/retune/`).

### CampaignSimTests — v1.1 deltas (gate comments on every [Test])

| Test | Gate | v1.1 change |
|---|---|---|
| `StageTable_MatchesDungeonAmendment` | R4/G2 | full v1.1 tables: sluice 5 hazards (+2 corridor vents 500/1030,604 ph .9/2.1), bastion 6 (+pylon 768,430), march 5 (wall L0 + wall R11.5 **PushX ±1 edge encoding** + altar 768,604 + vents 560,760/.6 · 980,450/1.8) |
| `TideCurrent_PushesPlayerInsideActiveBand` | G2 | park (650,470) (halfH 110 lane, pinned outside both corridor-vent discs by iso-distance assert); push 200/active [0.8,4.0): strict-increase + zero-idle-drift preserved; park bot now casts nova+ward (no kinematic effect — probed byte-identical position, hp 28→105) |
| `TideCurrent_SymmetricAndClamped` | G2 | comment −140→−200; L1-diamond clamp + lane-B strict-decrease unchanged (probe: maxX==bound to 7 s.f.) |
| `EmberPylon_CombatContract` | G2/G3 | (a) hp 300, weapon-5: **exactly 4 swings** (75.4×3+73.8), monotone hp, PylonDown once, hp pinned 0; (b) aura ratio == **0.40** exactly; (c)(d) unchanged |
| `EmberPylon_AuraCoversSpawnConvergence` (NEW) | G2/R2 | THE retune pin: all 3 pylons at iso 247-256 from spawn (768,604) — >220 (v1.0 missed) AND ≤280 (v1.1 covers); behavioural: hex-ring of 6 synthetic pylons at iso-405 ⇒ struck enemy provably in (220,280] annulus (measured 270.9) still takes ×0.40 |
| `AshWall_TimetableAndTicks` | G2 | v1.1 cycle 23 s: L-front 248/328/808/488 @ t=4.5/7/13.5/20 AND R-front 728/928/1288/1088 (phase 11.5) same instants; exact-**10** drops, killer-park window [7.2,11.4) (36-tick cadence); bait credit leg vs synthetic single L-wall (full table's right wall kills the v1.0 bait mid-lure; placement pinned elsewhere) — bait dies post-credit by design |
| `AshWall_RightWallHoldAndCorridorInvariant` (NEW) | G2 | right hold (stage t∈[1.5,4.5)): runner @≈1100 takes ≥4 exact-10 grid drops (probe: 5), corridor control @600 takes ZERO; sweep 23 s: maxLeftFront==808±.5, minRightFront==728±.5 (WallCovers tests against FrontX ⇒ damage-band edges), dual-active gap ≥600−.01 (probe min 599.99994), bothActive 661 ticks |
| `Telegraph_CensusUnderBudget` | D3 | v1.1 LCMs: sluice 12 s kiter, bastion 3 s kiter, march **23 s sim cross-check + 276 s analytic mirror** (mirror asserted tick-for-tick == published `Telegraphing` over the sim window; same `+= FixedStep` float accumulation ⇒ bit-identical clock; no bot survives 4.6 min under v1.1 walls). Measured maxima: 2/1/2 total, 1 same-kind (≤3 / ≤2 PASS) |
| `NewStages_SameConfigSameInputs_IdenticalDigests` | D1 | unchanged (passes under v1.1) |
| `NewStages_MutatedPlacement_ChangesDigest` | D3 | re-probed recipes — v1.0 bastion/march recipes went BLIND under v1.1 (aura 280 covers the centre script's whole reach; both march vents sit in wall bands no anchored fighter survives): sluice current phase +0.6 (centre) · bastion **pillar x+50** (centre) · march **wall phase +0.3** (off-grid, corridor-mid bot — park position IS the front midpoint) — all three probed differs=True |

### DungeonGoldenDigestTests

- 12 pre-existing rows: **byte-untouched** (must stay green under Unity).
- 3 new-stage rows re-pinned from the v1.1 dotnet run:
  `cinder-sluice|1400|3|9|0|124|(running)|974.9843|630.34314` ·
  `ember-bastion|2500|3|11|2|112|(running)|883.6059|695.33936` ·
  `ash-march|3600|4|16|0|142|(running)|479.2219|639.42065`
  (v1.0: 4200/3150/4200 — big legible shifts = the gimmicks now bite the kiter;
  ash-march ≠ echo-throne any more, stale comment removed). Header + test
  comment carry the main-lane Unity float re-pin protocol.

### StageCatalogTests / StageDressingTests — no edits needed (checked)

- Catalog: new anchors carry no override; v1.1 radial pairs re-checked —
  min pair distance bastion pylon(768,430)↔pillar(900,560) 185.9 > 70,
  pillars 275.9 > 132 (2×push-radius rule); sluice/march all clear; band
  kinds (current/wall) stay excluded from radial checks. 11/11 green.
- Dressing (Unity-only test; arithmetic re-check): every placement clears
  radius+50 vs the **v1.1** tables — march worst case feature-004 (1350,400)
  vs altar(768,604) 618 ≥ 120 and vs wall x-anchors ≥ 50; sluice corridor
  vents (500/1030,604) nearest props ≥ 296 ≥ 140; bastion pylon(768,430)
  nearest feature-021 (770,215) 215 ≥ 80. march table already keeps SimX ≥ 658
  (left-band note in StageCatalog.cs); right wall x=1288 nearest placement
  (1350,400) 641 ≥ 50 — wall clearance is x-anchor-point based (radius 0) and
  all dressing sits outside the combat plane anyway.

### Handoffs

- **Main lane**: run Unity EditMode. Expect possible one-shot failure ONLY in
  `Golden_NewDungeonStages_MatchFirstRecording` float fields → paste actual
  rows from the assert message, record divergence in qa/gate-measurements.md.
  Everything else must be green as-is.
- **ViewRetune**: none of my asserts constrain view code; corridor-vent /
  spawn-shield geometry pins are sim-space only.

## v1.2 fun pass

2026-08-05 · TestFunPass · cycle-2 "campaign fun pass" — every logical stage
gets a dominant-gimmick identity. Numeric truth: `docs/SIM_SPEC_DUNGEONS.md`
REVISION v1.2 + `design/campaign-fun-pass-spec.md`. Scope:
`Assets/Tests/EditMode/` only (CampaignSimTests, DungeonGoldenDigestTests,
StageCatalogTests, StageDressingTests). Nothing staged, no sim/view sources
touched. StageCatalog v1.2 tables verified in-place after ViewFunPass landed
them (byte-equal to the spec factory calls; epithet ctor param touches no test
— no test constructs StageEntry directly, grep-verified).

### Verification (standalone dotnet 8 + NUnitLite 3.14, real repo sources incl. StageCatalog)

```
Overall result: Failed — Test Count: 108, Passed: 101, Failed: 7
  CampaignSimTests          26/26   (5 new v1.2 [Test]s)
  DungeonGoldenDigestTests   0/7    (ALL 7 fails = documented dotnet↔Unity float drift — see below)
  HackSimTests              54/54
  StageCatalogTests         11/11
  StageDressingTests         5/5    (NOW runs on the harness — Transform.Find stub added)
  V12RowsProbe               5/5    (harness-only per-row fixture, not in repo)
```

The 7 golden "failures" are all X/Y float-field drift with **byte-identical int
fields** — the exact polarity the file header documents (Unity literals fail
under dotnet; dotnet literals pass). Because each golden test stops at its
first failing row, the harness-only `V12RowsProbe` fixture
(`/tmp/testlane-nunit/V12RowsProbe.cs`) re-asserts every v1.2 row separately:
echo-throne / ash-verdict / ash-march dotnet rows byte-pass; gallery/well int
prefixes pass and their full dotnet rows are byte-identical to each other
(trajectory identity, see below). Under Unity the polarity flips: the 5
pre-existing golden tests + gallery/well literals must pass as-is; only the
dotnet-recorded rows (throne/verdict/march + sluice/bastion re-pin rows) may
one-shot fail on floats.

### Golden split (the v1.2 contract change, spec §잔여 불변)

- **MUST NOT MOVE** (invariant safety net): arena-hack · arena-frozen ·
  prologue · cinder-span · abyss-chancel · cinder-sluice · ember-bastion ·
  classic 3. Rows byte-untouched; now living in
  `Golden_InvariantLogicalStages_AreUnchanged` (cinder-span/abyss-chancel),
  `Golden_NewDungeonStages_MatchFirstRecording` (sluice/bastion) and the three
  unchanged single-row tests. File header documents: movement here is a REAL
  regression, never re-pin to green.
- **EXPECTED MOVERS** → new `Golden_FunPassStages_MatchV12Recording`:
  - `ember-gallery|3150|3|14|1|136|(running)|719.403564|831.701843` (Unity
    literal KEPT — dotnet v1.1 vs v1.2 rows byte-equal: the kiter never meets
    the ring vents, trajectory unchanged, so the proven Unity floats stand)
  - `witness-well|3150|3|14|1|136|(running)|719.403564|831.701843` (moved:
    3400/2-relic → 3150/1-relic — altars left the kiter's path; trajectory now
    byte-identical to gallery's, pinned with gallery's Unity floats)
  - `echo-throne|4100|4|16|2|142|(running)|1253.0039|604.5185` (dotnet v1.2 —
    the current sweeps the kiter east: 4200→4100, relics 4→2)
  - `ash-verdict|3400|3|14|2|136|(running)|587.2855|702.79987` (dotnet v1.2)
  - `ash-march|3450|4|15|1|8|(running)|938.5367|602.01154` (dotnet v1.2 —
    **FRAGILE: hp 8.** If Unity float drift kills the kiter before tick 1800
    the INT fields move too; re-pin the whole row — still the shipped v1.2
    product, not a regression. Protocol in the test comment.)

### CampaignSimTests — v1.2 deltas (gate comments on every [Test])

| Test | Gate | v1.2 change |
|---|---|---|
| `StageTable_MatchesDungeonAmendment` | R4/G2 | march anchor = 6 hazards: + pylon(768,520) hp/radius pinned at index 3, vents shifted to 4/5 |
| `AshWall_TimetableAndTicks` | G2 | leg-1 bot kiter→CorridorMidInput (v1.2 pylon shields the centre pack ×0.40; plain kiter dies t≈16.2 — probed 973). Wall clocks ride stage time ⇒ samples bot-independent; legs 2/3 unchanged |
| `Telegraph_CensusUnderBudget` | D3 | + 4 catalog stages via new `AssertTelegraphCensus(HackConfig,…)` overload (hack lane, override applied like GameDirector): gallery 3 s · well 3 s · throne LCM(6,2.4)=12 s · verdict 3 s. Probed maxima 2/1/2/1 total, same-kind ≤2 (gallery ring pairs overlap 0.2 s). Mirror cross-check mismatches: 0 |
| `EchoThrone_CurrentPreview_PushesAndInterruptsChannel` (NEW) | G2/D3 | throne current (768,604,+120,0.3) pins; idle park displaced ONLY on active-interior ticks (≥150; 0 outside — classifier mirrors the sim float clock, 1-tick latency honoured); push ejects from altar r70 at tick 49 < 72 (hold 1.2 s) ⇒ 0 blessings in cycle 1. Boundary-position interruption is flaky-by-geometry (bot walk 218 > push 120) — per assignment, displacement+interruption asserted instead, documented in the gate comment |
| `EchoThrone_AltarChannel_CompletesInRestWindow` (NEW) | G2 | out-of-band park (y 790) through the push window, walk-in at stage 3.7 s: blessing tick 346 ∈ (222,408], current inactive at bless, player inside r70 — the 2.8 s push-free window ≥ 1.2 s hold timing puzzle |
| `EmberGallery_VentRing_PulsesInPhaseOrder` (NEW) | G2/D3 | table pin (4 ring vents 0/0.6/1.2/1.8 + centre pillar); pulse order strictly 3→2→1→0 (phase 1.8 wraps first), one vent per tick, 8 pulses / 2 periods, gaps 35-37 ticks (float lattice), HazardPulse raised on every wrap |
| `AshVerdict_PylonAura_ShieldsAltarUntilPylonDown` (NEW) | G2 | geometry pin 212.4 ≤ 280; single-sim arc on the campaign lane (catalog table injected like the classic-successor route): gated strike at the altar = exactly 75.4×0.40=30.16 on a MEASURED in-aura enemy → walk out, 4 swings, PylonDown once → same gated strike lands >30.16+1 (kill-clamped ≤75.4). No lockstep twin needed |
| `AshMarch_FinalePylon_ShieldsAltarWithoutBlockingCorridor` (NEW) | G2 | (a) never-attack altar park: every wall tick on an in-aura enemy = exactly 4 (10×0.40), every out-of-aura = exactly 10 (probed 6/7 drops) — the wall-rhythm/shield-war/altar-risk braid; (b) player stands at (768,604±) under the pylon and walks straight THROUGH (768,520): body r30 never blocks (corridor invariant) |

`AshWall_RightWallHoldAndCorridorInvariant`, `NewStages_*`, stage-0/1 guards
(`CinderSpan_KitingBot_ClearsStage` — cinder-span anchor untouched, not
parameterized on changed stages) all pass unmodified.

### StageCatalogTests

- `CompositeHazards_MatchPlacementAndClearanceContracts`: v1.2 tables for all
  FOUR overrides (echo-throne override NULL→table — the null-assert lived in
  `NewStageAnchors…`, which never listed echo-throne; no other null pin
  existed). Field pins extended to PushX/PushY/HalfW/HalfH/Hp (the throne
  current's +120 push IS the stage identity).
- Radial clearance refactored into `AssertRadialClearance` (shared by
  composites + anchors) with two documented exemptions: band kinds
  (current/wall — the throne current is co-located with its altar BY DESIGN)
  and the v1.2 **guarded-altar pair** (altar↔pylon may overlap: verdict clears
  anyway at 202.4; march 768,520↔768,604 = 84 < 100 — pylon bodies never
  block, altars are pure channel discs, overlap mechanically inert).
- 11/11 green on the harness.

### StageDressingTests

- Code unchanged (HazardsFor already reads live overrides); header documents
  the v1.2 re-verification. Worst margins (Euclidean beyond radius+50):
  gallery +97.7 · well +117.1 · verdict +80.2 · march pylon +217.9 — matches
  ViewFunPass's independent computation, zero dressing rows moved.
- Now ALSO runs on the standalone harness (added `Transform.Find` stub +
  csproj row): 5/5 green against the LIVE v1.2 tables — the clearance
  contract is no longer Unity-only.

### Handoffs

- **Main lane (Unity EditMode)**: expect one-shot float fails ONLY in
  `Golden_FunPassStages_MatchV12Recording` (throne/verdict/march rows) and
  possibly `Golden_NewDungeonStages_MatchFirstRecording` (sluice/bastion rows
  were already Unity-re-pinned — should pass). Re-pin floats from assert
  messages, record in qa/gate-measurements.md. **ash-march row: if INT fields
  move (hp-8 fragility) re-pin the whole row and note it — it's the shipped
  v1.2 product.** Everything else must be green as-is; invariant-row movement
  is a real regression.
- **ViewFunPass**: dressing margins independently confirmed (+97.7/+117.1/
  +80.2/+217.9); zero moved rows verified on the harness. Epithet ctor param
  required no test change.
- Harness artifacts: `/tmp/testlane-nunit/` (TestResult.xml 108-test run,
  V12RowsProbe.cs) · `/tmp/funpass-probe/` (calibration probes A-K: kiter
  death t=973, corridor survival, throne interrupt tick 49 / bless tick 346,
  ring pulse order, census maxima, shield gradient 4-vs-10, verdict arc).
