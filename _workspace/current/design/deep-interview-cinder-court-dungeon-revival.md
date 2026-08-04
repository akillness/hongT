# Deep Interview Spec: Cinder Court — Dungeon Revival

## Metadata
- Interview ID: `cinder-court-dungeon-revival-20260804`
- Rounds: 6
- Final Ambiguity Score: 6.7%
- Type: brownfield
- Generated: 2026-08-04
- Threshold: 0.20
- Threshold Source: default
- Initial Context Summarized: no
- Status: PASSED — pending execution approval

## Clarity Breakdown

| Dimension | Score | Weight | Weighted |
|---|---:|---:|---:|
| Goal Clarity | 0.96 | 0.35 | 0.336 |
| Constraint Clarity | 0.93 | 0.25 | 0.233 |
| Success Criteria Clarity | 0.92 | 0.25 | 0.230 |
| Context Clarity | 0.90 | 0.15 | 0.135 |
| **Total Clarity** | | | **0.934** |
| **Ambiguity** | | | **0.066** |

## Topology

| Component | Status | Description | Coverage / deferral note |
|---|---|---|---|
| Reference analysis | active | Translate supplied Achilles short and screenshots into original, non-copying design principles. | Equipment readability, centred boss staging, human silhouette, subtitle timing, and room-scale framing are reference inputs only. |
| Core play | active | Human `Lantern Reaver` clears three rooms using hybrid summon/deployment. | Guardian follows by default, can be ordered to hold a target zone temporarily, then returns to follow. |
| Story and dungeon | active | Recover sealed memories by restoring guardian echoes through a compact three-room route. | Each recovered echo reveals the betrayal behind the final corrupt enforcer. |
| Presentation | active | Improve camera, URP shaders, combat VFX, audio cues, and HUD as one dark-fantasy language. | Use charcoal, ember orange, and spectral cyan; cinematic but readable in WebGL. |
| External assets | active | Adopt only source assets retained by the external manifest and compatible with Unity/WebGL budgets. | Source repo remains read-only; non-runtime OBJ paths are not approval to copy. |
| Link preview | active | Generate original social-preview key art through `god-tibo-imagen`. | `gti --dry-run` passed; actual generation and provenance happen during approved execution. |

## Goal

Ship one original, playable Cinder Court campaign vertical slice by recomposing the current `cinder-span → abyss-chancel → echo-throne` progression into one contiguous route: a human **Lantern Reaver** crosses its three dark-fantasy rooms to recover sealed memories, reclaims guardian echoes through hybrid summon-and-deploy combat, and confronts a two-phase corrupt enforcer in Tribunal Arena. The slice must unify dungeon layout, equipment/companion UI, story beats, camera language, URP materials/VFX, approved source assets, and a generated social link preview.

## Constraints

- Preserve the 60 Hz deterministic simulation boundary: `Assets/Scripts/Sim/` remains pure C# and does not reference UnityEngine; `Assets/Scripts/View/` only reads simulation state.
- Do not modify files marked `FROZEN CONTRACT`. Extend behavior only through non-frozen seams.
- Target Unity 6000.5.6f1, URP 17.5.0, and WebGL. Do not use compute shaders or threads; textures must be at most 1024 px, characters at most 25k triangles, and total WebGL build at most 120 MB.
- Treat `../../../Abyssal-Surge` as a read-only source. Runtime adoption follows `defense-asset-manifest.json`: retained replacement assets are candidates; `delete` / `runtimeReference:false` entries, including most OBJ exports, are not candidates.
- Use `assets/mesh/character/lantern-reaver-character/glb/base_basic_pbr.glb` as the player source. It is retained and runtime-referenced in the source manifest. Rebind and retarget through the repository's documented Blender-to-Unity humanoid pipeline; do not reuse the source project's procedural skinning.
- Use `god-tibo-imagen` for the link-preview key art; run `gti --dry-run` before final generation and record prompt, reference sources, output path, and tool under `docs/provenance/`.
- The intended mood is dark-souls-like dark fantasy, not a reproduction of Dark Souls or Achilles. Do not copy named characters, UI layouts, lore, boss anatomy, prompts, screenshots, or protected designs.
- All deployment URLs remain relative to support GitHub Pages at `https://akillness.github.io/hongT`.

## Non-Goals

- Do not port the source project's Three.js/DOM implementation or apply its renderer guidance to Unity.
- Do not import every raw OBJ solely because it exists; manifest-disallowed files must remain untouched.
- Do not replace the existing infinite arena mode or change its frozen numeric contract.
- Do not add unsupported runtime dependencies, remote asset loading, compute shaders, or unbounded particle systems.
- Do not implement additional campaigns beyond the requested three-room vertical slice.

## Original Reference Direction

### Observed reference inputs

- The supplied 60-second Achilles: Legends Untold short has no subtitle transcript available. Its attached capture shows a high-contrast equipment view with a full-height human warrior silhouette, large item slots, readable level/stat block, and restrained dark background.
- The second supplied capture shows a dungeon boss framed centrally in a ruined chamber with environmental fog, backlight, and dialogue subtitles.

### Original translation

- **Human readability:** keep the Lantern Reaver readable at gameplay camera distance through a lantern silhouette, ember rim light, and a compact equipment pane rather than copying the referenced character screen.
- **Room dramaturgy:** rooms shift from a low-pressure memory corridor, to a guardian-placement choke, to a circular tribunal boss arena. Each presents a specific tactical reason to issue a hold/follow guardian order.
- **Boss direction:** the final corrupt enforcer is an original humanoid court executioner using ash-and-echo telegraphs, not a serpent or any reference boss. The second phase starts with an original memory fracture and camera lock, followed by a recoverable player-control return.
- **Visual grammar:** dark stone, low blue-black fill, ember-orange hazard/fire, spectral-cyan memory/guardian signals. Every hostile telegraph contrasts against the floor and has a reduced-motion-safe static indicator.

## Dungeon and Storyline

**Premise:** Lantern Reaver was once the Cinder Court's memory hunter. A betrayed pact sealed their memories inside guardian echoes. Reclaiming each echo restores a tactical guardian command and exposes the corrupt enforcer who turned the court into a memory prison.

| Room | Tactical purpose | Story beat | Completion / reward |
|---|---|---|---|
| 1. Cinder Span — Ash Archive | Teach follow and recall while avoiding a simple ember vent. | First echo identifies the Reaver as the court's former hunter. | Ember Rest, unlock hold-command tutorial, and first memory fragment. |
| 2. Abyss Chancel — Witness Gallery | Ask the player to deploy the guardian on a capture rune while they intercept enemies. | Second echo reveals that the enforcer rewrote the court record. | Ember Rest, permanent companion resonance reward, and equipment shard choice. |
| 3. Echo Throne — Verdict Well | Combine hazards, placement, and elite timing before the arena opens. | Third echo restores the Reaver's sentence and names the enforcer. | Ember Rest, boss gate, full guardian charge, and final equipment comparison. |
| 4. Tribunal Arena | Boss phase 1 pressure/positioning; phase 2 memory-fracture telegraph and counter-window. | The Reaver chooses to preserve the recovered memories rather than use them as fuel. | Campaign completion, relic reward, and replay-ready result screen. |

## Inter-Stage Preparation Contract

- Completing a room enters a short **Ember Rest** interlude instead of cutting from a top-down lantern action directly to the next map.
- Ember Rest bridges Cinder Span → Abyss Chancel → Echo Throne without returning to the lobby or resetting the active run. Tribunal Arena opens after the third rest.
- The camera shifts to a 3/4 over-shoulder composition of the Lantern Reaver beside a floor-level lantern. The ignition uses the character's hand/lantern silhouette, ember reflections, and a forward-facing next-door reveal; it must not use a top-down close-up.
- Ember Rest preserves campaign meta, shows earned memory/relic rewards, and presents a small, readable choice set covering stat tuning, skill-rune/loadout tuning, and guardian resonance.
- The player may inspect equipment and companion status before confirming **Continue**. Choices must be explicit, may be deferred, must not alter frozen arena numbers, and must not discard earned rewards.
- Selected rest effects live in a dedicated deterministic **run-scoped preparation state**; skill runes/loadouts do not silently become permanent `CampaignStore` meta. Permanent rewards use the existing single-writer persistence path; a future resume checkpoint requires an explicit schema change.
- Continuing applies the selected run-state adjustment, restores the dungeon camera, and opens the next room with a bounded reveal. The panel is touch-safe, keyboard-accessible, and cannot soft-lock progression.

## Core Interaction Contract

- The player character is the human Lantern Reaver model, imported as a Unity Humanoid avatar with the existing action contract: `idle`, `move`, `run`, `hit`, `bighit`, `attack`, `critical`, `avoid`, `defence`, `die`, `show`.
- One guardian is active. Its default state follows behind the player and automatically attacks near targets.
- When the guardian acquires an attack target, simulation publishes its target-facing direction and the View rotates the companion to face that target before the attack presentation. Follow/idle facing remains stable and deterministic.
- The player can issue a **hold** command to a valid target location. During hold, the guardian defends/captures that zone for a bounded duration or until recalled; the command has a visible cooldown and clear failure state.
- The player can recall the guardian at any time. Recall cancels hold, returns the guardian to follow behavior, and restores the normal combat formation.
- Existing companion selection stays meaningful: the active companion controls visual identity and minor presentation tint, while the simulation owns all state changes deterministically.

## Presentation Contract

### UI

- Preserve current responsive uGUI and Korean-first support.
- Add a compact dungeon equipment/companion readout: weapon, lantern, cloak, active echo, hold state, hold duration/cooldown, and a simple room objective.
- Do not bury boss health, extraction, guardian order, or player skill availability in an inventory panel. These remain at the combat edge of the HUD.
- Equipment inspection must present the Lantern Reaver silhouette, chosen item, stat delta, and a single equipped action, but remain visually original.

### Camera

- Keep the established mode-aware camera. The dungeon profile leads with readable 55-degree perspective rather than a scripted camera that steals control.
- Ember ignition and inter-stage preparation use a fixed 3/4 over-shoulder shot that shows the Lantern Reaver and the destination door at ground level; no top-down ignition close-up is permitted.
- Use only bounded beats: short room-entry reveal, a boss-gate push-in, a 0.5-second boss-phase fracture beat, then immediate restoration to the playable camera.
- Camera motion must not hide enemy telegraphs, objective runes, or guardian placement targets.

### Epic Dungeon Scale and Hack-and-Slash Level Design

- Keep the frozen simulation arena and hazard coordinates intact. Grand scale is a **visual envelope**, not a hidden expansion of walkable collision: distant architecture, elevated ruins, side galleries, broken statuary, hanging chains, bridge silhouettes, and sky/fog layers remain non-walkable dressing outside the combat plane.
- Each existing stage becomes a readable three-beat combat space: a landmarked approach, a dense but dodgeable combat pocket with two or three threat lanes, and a reward/gate exit. Player routes, dodge corridors, telegraphs, pickups, and exits stay visible before commitment.
- Cinder Span emphasizes a monumental approach and flanking lanes; Abyss Chancel emphasizes pillars and guardian hold points; Echo Throne emphasizes a broad tribunal forecourt that previews the final arena.
- Rewards use a deterministic run-scoped reward deck rather than nondeterministic RNG. At each Ember Rest, the player receives a varied but reproducible small selection whose seed and chosen effect are simulation-owned and testable.
- More props must serve navigation, enemy readability, cover silhouette, reward framing, or a motivated light source. Decoration must not create impassable routes, hide target zones, or imply vertical traversal.
- Use full-screen presentation only for short, bounded beats: room-gate ignition, guardian materialization, elite defeat, and boss phase fracture. Keep combat UI and hostile telegraphs legible; provide reduced-motion and low-quality fallbacks.
- Treat 16.67 ms/frame as the target. Pool effects; cap active VFX by quality tier; avoid compute, screen-filling blur, unbounded overdraw, per-frame material allocation, or persistent letterboxing during live combat.

### Shader and VFX

- Use URP-compatible material/shader paths only. Prioritize lit basalt/metal materials, Fresnel rim highlight, low-cost dissolve for memory echoes, and emissive pulse for enemy telegraphs.
- Reuse pooled VFX patterns. No runtime effect may require compute, threads, per-frame material cloning, or uncontrolled particle allocation.
- Guardian summon: cyan-gold circular sigil, 0.35-second silhouette bloom, then a fixed, readable origin marker.
- Hold order: projected rune with contrast-safe edge and a countdown ring. Recall: lantern filament trails toward the Reaver.
- Boss phase: ash cracks and cyan memory shards with screen-space-safe bounds, reinforced by existing audio cues and subtitles.

## Approved External Asset Plan

| Role | Candidate source | Manifest status | Unity use |
|---|---|---|---|
| Player | `assets/mesh/character/lantern-reaver-character/glb/base_basic_pbr.glb` | retain / runtime-reference | Rebind to standard humanoid skeleton, retarget existing action library, build a player prefab. |
| Boss candidates | `assets/mesh/boss/s1-cinder-warden/glb/base_basic_pbr.glb`; `s2-veil-tactician`; `s3-gate-sovereign` | retain / runtime-reference | Evaluate triangle count and skeleton, select one as the original corrupt-enforcer base, then create Unity prefab/material variant. |
| Existing enemy roles | Existing Unity prefabs under `Assets/Resources/Characters/` | already imported | Keep current enemy visual mappings; only upgrade materials/encounter usage where compatible. |
| Dungeon terrain | Existing Unity prefabs under `Assets/Resources/Terrain/` | already imported | Compose the three rooms from deterministic stage terrain and safe, lightweight props. |

## Acceptance Criteria

- [ ] The current `cinder-span → abyss-chancel → echo-throne` campaign becomes one contiguous route, bridged by Ember Rest and ending in Tribunal Arena, without breaking the arena entry flow or corrupting campaign unlock data.
- [ ] The player is rendered from the imported Lantern Reaver source through a Unity Humanoid avatar and can play the existing required action states without mesh tearing.
- [ ] The player can issue hold and recall orders to exactly one guardian; follow, hold, recall, invalid placement, expiry, and boss-phase interaction all have deterministic simulation tests.
- [ ] Guardian targeting deterministically publishes the attack-facing direction. In play, the companion turns toward the actual target before its attack; follow/idle orientation does not jitter or use View-owned targeting.
- [ ] Each of the three rooms has a distinct objective, encounter rule, story beat, permanent reward persisted through `CampaignStore`, and a separate run-scoped preparation outcome.
- [ ] The boss has a clear two-phase encounter, bounded camera beat, subtitles, contrast-safe telegraphs, and a return to player-controlled combat after the phase transition.
- [ ] The HUD exposes health/resources, boss phase, current room objective, guardian mode, placement indicator, command cooldown, companion/equipment state, and responsive touch-safe layout.
- [ ] Clearing each non-boss room opens Ember Rest without a scene cut. The player can inspect earned rewards, choose or defer an explicit stat, skill-rune/loadout, or guardian-resonance adjustment, and then continue to the next room without losing campaign state or incorrectly persisting run-scoped choices.
- [ ] Ember Rest lantern ignition is readable from a 3/4 ground-level camera, while gameplay returns to the existing dungeon profile before player input resumes.
- [ ] Every room has a readable approach, dodgeable combat pocket, and reward/gate exit; its grand visual envelope uses non-walkable dressing and does not change frozen simulation bounds or hazards.
- [ ] Ember Rest reward selections come from a simulation-owned deterministic deck; the same run seed/room index reproduces the same choices, while different permitted seeds can produce variety without runtime RNG.
- [ ] Full-screen camera/VFX beats are bounded, pooled, reduced-motion-safe, and preserve combat telegraphs and HUD readability. Target performance remains 16.67 ms/frame until a measured target-device profile supersedes it.
- [ ] All imported source assets are manifest-approved, within WebGL budgets, and leave `../../../Abyssal-Surge` unchanged.
- [ ] `gti --dry-run` succeeds; a generated original 1200x630-class preview image is saved at the chosen web/deployment surface and its full provenance is recorded under `docs/provenance/`.
- [ ] EditMode tests covering new simulation logic pass; Unity batchmode WebGL build succeeds; deployed preview paths are relative.

## Technical Context

- `Assets/Scripts/Sim/SimTypes.cs` is frozen and provides the pure simulation contracts. `CinderSim` already supports `GameMode.Dungeon`, single active companion data, boss HP/phase, hazards, extraction, and deterministic fixed-step update.
- `Assets/Scripts/Sim/HackTypes.cs` exposes the existing 1-slot companion through `HackConfig.CompanionId`; `CinderSim.UpdateCompanion` currently follows and auto-attacks. The hybrid guardian contract extends this at the sim seam rather than letting `View` own gameplay decisions.
- `Assets/Scripts/View/GameDirector.cs` routes lobby, prologue, campaign dungeon, terrain swapping, and active companion persistence.
- The new route orchestration belongs in non-frozen view/campaign seams: it must carry stage identity and run-scoped preparation across the three existing stage terrain swaps, rather than treating every stage clear as a return-to-lobby boundary.
- `Assets/Scripts/View/GameView.cs` already creates/synchronizes the companion, syncs dungeon HUD values, controls a boss-phase slow-motion beat, and forwards VFX events.
- `Assets/Scripts/View/HudView.cs` owns responsive uGUI Dungeon HUD; `CameraRig.cs` already has a dungeon camera profile; `VfxDirector.cs` owns pooled/generated effects; `StoryCatalog.cs` owns stage/boss/phase/completion text hooks.
- The external source manifest marks most raw OBJ exports as deletion candidates. The retained GLBs are the authoritative source candidates.

## Ontology (Key Entities)

| Entity | Type | Fields | Relationships |
|---|---|---|---|
| Lantern Reaver | core human protagonist | humanoid avatar, lantern, equipment, recovered memories | commands one Guardian Echo; challenges corrupt enforcer |
| Guardian Echo | tactical companion | follow/hold/recall state, position, duration, cooldown | is recovered per room; obeys Lantern Reaver |
| Dungeon Room | progression space | objective, hazard, encounter, story beat, reward | feeds a memory to the Lantern Reaver and gates Tribunal Arena |
| Corrupt Enforcer | final boss | HP, phase, telegraphs, dialogue | imprisoned the guardian memories; opposes Lantern Reaver |
| Equipment | progression | weapon, lantern, cloak, shard rank | changes campaign/readout and appears in equipment inspection |
| Link Preview | release asset | original key art, output path, provenance | represents Cinder Court on GitHub Pages/social links |

## Ontology Convergence

| Round | Entity Count | New | Changed | Stable | Stability Ratio |
|---|---:|---:|---:|---:|---:|
| 1 | 6 | 6 | - | - | - |
| 2 | 6 | 1 | 0 | 5 | 83% |
| 3 | 6 | 0 | 1 | 5 | 100% |

## Assumptions Exposed and Resolved

| Assumption | Challenge | Resolution |
|---|---|---|
| Summoning meant multiple units or free placement. | Existing sim supports one following companion only. | Use one guardian with bounded follow/hold/recall states. |
| Every raw OBJ is usable. | Source manifest labels most OBJ exports `delete` and non-runtime. | Use retained/replacement assets only; source remains read-only. |
| Reference atmosphere requires copied content. | Screenshots are style reference, not implementation source. | Translate only broad legibility and staging principles into original dark fantasy. |
| The player model was undecided. | Asked for exact protagonist asset. | Use retained `lantern-reaver-character` GLB through the documented humanoid pipeline. |
| First delivery could be any UI or combat fragment. | Asked for a testable playable proof. | Build a three-room route plus two-phase boss as the vertical-slice gate. |
| Story was atmospheric decoration. | Asked why the protagonist crosses the dungeon. | Recover sealed memories through guardian echoes and expose the corrupt enforcer's betrayal. |
| Inter-stage choices would safely fit campaign persistence. | `CampaignStore` holds permanent meta only and has no skill-loadout field. | Ember Rest tuning is a deterministic run-scoped state; only existing permanent rewards use `CampaignStore`. |
| The three existing campaign stages had to stay isolated or be replaced by a new campaign. | `GameDirector` currently unlocks and starts them separately. | Recompose Cinder Span, Abyss Chancel, and Echo Throne as sequential rooms in one Lantern Reaver run, with Ember Rest transitions and Tribunal Arena as the final boss. |

## Interview Transcript

### Round 0
**Q:** Confirm the six-component project topology.

**A:** All six components proceed.

### Round 1
**Q:** Choose the guardian deployment interaction.

**A:** Mixed: guardian follows by default and can be assigned to defend a target location for a duration.

**Ambiguity after round:** 34.5%.

### Round 2
**Q:** Choose the first playable proof.

**A:** Three rooms plus a boss fight.

**Ambiguity after round:** 26.5%.

### Round 3
**Q:** Define the Lantern Reaver's dungeon purpose.

**A:** Recover sealed memories; each guardian echo restores memories and reveals the boss betrayal.

**Additional resolved constraint:** The player model is `lantern-reaver-character`.

**Ambiguity after round:** 14.5%.

### Round 4
**Q:** Improve the awkward top-down lantern ignition and make inter-stage preparation meaningful.

**A:** Ember ignition must use a non-top-down presentation. Before continuing, the player needs stat and skill maintenance plus meaningful choices.

**Resolution:** Use a fixed 3/4 ground-level Ember Rest composition. Its stat, skill-rune/loadout, and guardian-resonance choices are explicit and run-scoped; existing permanent rewards continue through `CampaignStore`.

**Ambiguity after round:** 11.2%.

### Round 5
**Q:** Should Ember Rest be a hard tactical choice or free rebuilding?

**A:** Mixed: stats and equipment remain freely adjustable, while one rune or guardian trait is selected and locked for the next room.

**Ambiguity after round:** 8.9%.

### Round 6
**Q:** Should the three rooms be new content or the current campaign's progression?

**A:** Recompose the existing three stages into one continuous dungeon, then end with a boss encounter.

**Ambiguity after round:** 6.7%.
