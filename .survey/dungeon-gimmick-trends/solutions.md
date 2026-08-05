# Solution Landscape: Abyssal Lantern — Cinder Court, per-dungeon gimmick trends

Survey pool: 11 titles — Hades (HAD), Hades II (H2), Dead Cells (DC), Vampire Survivors (VS), Halls of Torment (HoT), Diablo IV Nightmare dungeons (D4), Path of Exile maps/leagues (PoE), Enter the Gungeon (ETG), Binding of Isaac: Repentance (BoI), Moonlighter (ML), Death Must Die (DMD). All per-title mechanics evidence: indexed snippet via search aggregation unless marked otherwise; cells marked (t) are thin evidence.

## Solutions
How each title manufactures per-stage mechanical variety (full detail in `context.md`):

## Solution List

| Name | Approach | Strengths | Weaknesses | Notes |
|------|----------|-----------|------------|-------|
| Hades | One signature spatial rule per biome (lava islands, respawn rule, trap tunnels) over shared trap/prop vocabulary | Traps are tools: luring enemies in is core tactics; permanent visible telegraphs | Lava biome reads as player-only punishment | indexed snippet |
| Hades II | Adds player-exploitable props (shock trees) and a structural gimmick (open fields replace rooms) | Prop-as-weapon widely praised; structure itself as gimmick | Open-field biome creates pacing complaints | indexed snippet |
| Dead Cells | Hazard + economy rule per biome (poison floors, cursed chests, timed doors) | Opt-in risk (curse) and speed pressure (doors) are deterministic-friendly | Hazards mostly player-only | indexed snippet |
| Vampire Survivors | Geometry constraint + fixed-clock events per stage; near-zero floor hazards | Purely deterministic schedule (26:00 rush, 30:00 Reaper) — closest to our no-RNG model | Little in-arena spatial play | indexed snippet |
| Halls of Torment | Stage shape + timeline sweeper (ghost walls shrink arena) + map-secret boss debuff chain | Shrink-wall is the stage's remembered identity; secret chain = deterministic objective | Player-only damage everywhere | indexed snippet |
| Diablo IV NM | Data-layered afflictions (periodic eruption, forced safe dome, chasing dazer) on any tileset | Cheap variety injection via data, exactly our data-driven model | Player-relative + interruption-heavy = "anti-chill" backlash | indexed snippet |
| Path of Exile | Placed opt-in encounter objects (Ritual circle, Blight lanes, Breach expansion, Delirium fog) + ground-effect map mods | Objects define local space rules; layout choice is player's difficulty lever | Complexity budget far beyond our scope | indexed snippet |
| Enter the Gungeon | Room-scale symmetric interactive sandbox (pits, tables, goop, water-barrel chains, minecarts) | Symmetry: enemies fall in pits, flip tables; hazards double as ammo-free kills | Chamber-signature movers (carts) costly to build | indexed snippet |
| Binding of Isaac | Chapter-signature traversal/reveal gimmicks (water+reflections, minecart+dark, HP-toll doors+teleporters) | Reveal mechanics (reflection, lightning flash) are unique in pool | RNG-soaked placement; needs determinizing | indexed snippet |
| Moonlighter | Per-dungeon hazard palette (toxic/fire/electric patches, stasis tasers) + overstay chaser (Wanderer) | Overstay timer = clean deterministic pressure valve | Palette-swap DoT patches, low mechanical spread | indexed snippet |
| Death Must Die | Variety via enemy composition per act, not placed gimmicks | Negative control: proves stages CAN differ without environment — but players describe acts, not arenas | Weakest environmental vocabulary in pool | indexed snippet; thin evidence for absence |

## Frequency Ranking
Gimmick-archetype frequency matrix. ✓ = present, — = absent/not established, (t) = thin evidence. Count = titles present out of 11. **NOVELTY = qualifies for G8 (<=2 of surveyed titles).**

| Gimmick archetype | HAD | H2 | DC | VS | HoT | D4 | PoE | ETG | BoI | ML | DMD | Count | G8 novelty |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Movement blocker / destructible obstacle | ✓ | ✓ | ✓ | ✓ | ✓(t) | ✓ | ✓ | ✓ | ✓ | ✓ | — | 10 | no |
| Static lingering hazard floor (lava/poison/thorns) | ✓ | ✓ | ✓ | — | ✓ | — | ✓ | ✓ | ✓ | ✓ | — | 8 | no |
| Moving hazard / chaser entity | ✓ | — | — | ✓ | ✓ | ✓ | — | ✓(t) | ✓ | ✓ | — | 7 | no |
| Cursed zone / risk-reward object (pay HP for reward) | ✓ | ✓(t) | ✓ | — | — | ✓ | ✓ | ✓ | ✓ | — | — | 7 | no |
| Spawner/enemy-rule alteration per stage | ✓ | — | — | ✓ | ✓(t) | — | ✓ | — | ✓(t) | ✓ | ✓ | 7 | no |
| Timed objective / fixed-clock event pressure | — | — | ✓ | ✓ | ✓ | ✓(t) | ✓ | — | — | ✓ | — | 6 | no |
| Shrine/altar zone buff (stand-in / interact) | — | — | — | — | — | ✓ | ✓ | ✓ | ✓ | ✓(t) | — | 5 | no |
| Darkness / vision limit | ✓ | — | ✓(t) | — | — | — | ✓ | — | ✓ | — | — | 4 | no |
| Player-exploitable strike prop (hit → hurts enemies) | ✓ | ✓ | — | — | — | — | — | ✓ | ✓ | — | — | 4 | no |
| Periodic scheduled AoE eruption (placed vent) | — | ✓(t) | — | — | — | ✓ | — | — | — | — | — | 2 | **yes** |
| Shrinking / encroaching safe zone | — | — | — | — | ✓ | — | ✓(t) | — | — | — | — | 2 | **yes** |
| Transport rail / mover vehicle (minecart) | — | — | — | — | — | — | — | ✓ | ✓ | — | — | 2 | **yes** |
| Teleporter / portal pair inside arena | — | — | — | — | — | — | — | ✓(t) | ✓ | — | — | 2 | **yes** |
| Conveyor / current / directional push field | — | — | — | — | — | — | — | ✓(t) | — | — | — | 1 | **yes** |
| Enemy-empowering aura object (destructible pylon) | — | — | — | — | — | — | ✓(t) | — | — | — | — | 1 | **yes** |

Reading of the ranking: the three archetypes Cinder Court already ships (ember-vent = periodic eruption, obsidian-pillar = blocker, relic-altar = shrine buff) sit at counts 2, 10 and 5 — the blocker and altar are saturated genre furniture, while the placed periodic vent is genuinely uncommon in placed-object form. New gimmicks should be drawn from the six novelty rows. (provenance: matrix cells compiled from indexed snippets listed in Curated Sources; (t) cells thin evidence)

## Categories
1. **Space-topology gimmicks** (blockers, portal pairs, shrink zones, push currents) — change WHERE the player can be. Least saturated subfamily: force/topology manipulation.
2. **Schedule gimmicks** (periodic vents, fixed-clock events, timed doors/objectives) — change WHEN space is safe. VS/HoT prove pure-schedule variety works with zero RNG.
3. **Object-interaction gimmicks** (strike props, altars, cursed objects, water-barrel chains) — give the arena verbs. Symmetric ones (Gungeon) are the best-loved in the pool.
4. **Enemy-rule gimmicks** (Elysium respawn, aura pylons, stage spawn tables) — change WHO you fight and how kills resolve. Enemy-side placed objects are near-empty design space.
5. **Perception gimmicks** (darkness, fog, reflections) — change what you can SEE. Moderately used; high theme fit for a lantern game but weak G8 novelty.

## What People Actually Use
- The universal baseline is category 1+3 furniture: every title except DMD ships blockers, and 8/11 ship static hazard floors. This vocabulary is table stakes, not identity. (provenance: matrix above, indexed snippet)
- Stage IDENTITY is almost always exactly one signature rule per stage layered on that baseline: Elysium = respawn rule, Viaduct = ghost wall, Gallo Tower = vertical shaft, Black Powder Mine = minecarts, Downpour = water+reflections, Oceanus = aquatic traps. No surveyed title gives a stage two signature gimmicks at once. (provenance: indexed snippet synthesis)
- Deterministic scheduling already exists commercially: VS fixed-clock events and HoT wall timelines are fully deterministic and are the mechanics players describe when naming those stages — evidence that no-RNG gimmicks can carry stage identity. (provenance: indexed snippet)
- Symmetric hazards are used as player tools (Gungeon pit-kills, Hades trap-luring, H2 shock trees) and are consistently described positively in community sources, whereas player-only harassment (D4 afflictions) draws sustained complaints. (provenance: indexed snippet, incl. user voices in context.md)

## Curated Sources
- Hades biome hazards: fandom.com Hades wiki, techraptor.net, steelseries.com, gamepressure.com, reddit.com r/HadesTheGame (indexed snippet)
- Hades II regions: search-aggregated guides w/ citations (indexed snippet)
- Dead Cells biomes/timed doors: deadcells wiki + community guides (indexed snippet)
- Vampire Survivors stages: gamerant.com, thegamer.com, screenrant.com, namu.wiki (indexed snippet)
- Halls of Torment stages: search-aggregated community guides (indexed snippet)
- Diablo IV afflictions: news.blizzard.com, maxroll.gg, mythicdrop.com, diablo4.gg, reddit.com r/diablo4 (indexed snippet)
- PoE maps/leagues: search-aggregated wiki/guide corpus (indexed snippet)
- Enter the Gungeon hazards: search-aggregated wiki corpus (indexed snippet)
- Binding of Isaac Repentance alt-path: search-aggregated wiki corpus (indexed snippet)
- Moonlighter dungeons: search-aggregated wiki/guide corpus (indexed snippet)
- Death Must Die acts: escapistmagazine.com, steamcommunity.com, reddit.com (indexed snippet)
- Telegraph conventions (windup → indicator → execution, decal=hitbox, diegetic skins, off-screen cues): search-aggregated design-practice corpus (indexed snippet)
- Project constraints: `_workspace/current/design/deep-interview-cinder-court-dungeon-revival.md` (direct page retrieval)

## Key Gaps
- **No surveyed title choreographs hazards on a fixed learnable timeline per arena.** Everything is RNG-placed (BoI, PoE, ETG rooms), player-relative (D4 afflictions), or a single global timer (VS, HoT). A deterministic multi-gimmick "hazard rotation" the player can learn like a boss pattern is unoccupied space and is EXACTLY what a no-RNG 60Hz sim is uniquely good at.
- Enemy-side placed objects (aura pylons, enemy-buffing zones) are essentially absent as environmental gimmicks (1/11, thin) despite being the natural mirror of the ubiquitous player altar.
- Force/topology gimmicks (push currents, portal pairs, shrinking zones) are all in the <=2 band — rare not because they read badly but because they're harder to retrofit into RNG room generators; fixed-plane data-driven placement removes that barrier for us.

## Contradictions
- D4's player-relative gimmicks are heavily complained about ("anti-chill") while HoT's ghost wall — also player-punishing — is celebrated. The difference is predictability: the wall runs on a fixed visible schedule, the afflictions ambush. Fairness can be bought with EITHER symmetry (Gungeon) or determinism (HoT/VS); we can afford both. (provenance: indexed snippet)
- Placed periodic AoE vents scored "novel" (2/11) in placed-object form, yet the archetype FEELS common because monster-cast ground AoE is everywhere; G8 reviewers should score against placed environmental objects, not monster abilities, or the table misleads.
- Isaac/Gungeon minecarts show movers create beloved stage identity, but both implementations depend on pit-riddled layouts we don't have on a flat 1536x1024 plane — novelty rank high, fit rank low.

## Key Insight
Stage identity in this genre = ONE signature spatial-or-scheduling rule per stage on top of shared saturated furniture; the unsaturated design space is force/topology manipulation (push, shrink, portals) and enemy-side objects, and the single biggest unclaimed differentiator is a fully deterministic, visibly scheduled hazard choreography — which our no-RNG constraint turns from a limitation into the product identity.

---

## Gimmick Candidates for Cycle 2 (ranked by fit: G8 novelty × deterministic-sim cost × WebGL cost × fairness readability)

Constraint frame for all rows: fixed 1536x1024 plane, iso distance dy*1.42 (telegraph decals must be drawn in plane-space and scaled 1.42x on y to read as circles), sim-owned state, data-driven placement (position/period/phase/radius fields), player-only or symmetric damage rules, pooled VFX, no dynamic lights.

1. **Tide-current channels** (conveyor/push field; 1/11, G8 pass) — Fixed lanes on the plane apply a constant velocity offset to ALL entities inside during scheduled phases (e.g. 4s on / 4s off, per-lane phase offset in data). Symmetric: pushes enemies into ember-vents; player learns to surf. Sim cost: one AABB test + velocity add. View cost: scrolling UV quad. Best overall fit.
2. **Encroaching ash wall** (shrinking safe zone; 2/11, G8 pass) — A wall segment advances along a fixed path on a fixed timetable, crushing/damaging BOTH sides against arena bounds, then recedes. Differentiates from HoT by symmetry + full determinism. Sim cost: 1D position function of tick. View: animated mesh strip. Natural "final room" pressure gimmick.
3. **Ember-shield pylon** (enemy-empowering aura object; 1/11 thin, G8 pass) — Destructible pylon granting enemies in radius a visible damage-reduction tint; creates deterministic target-priority puzzles and mirrors relic-altar on the enemy side. Sim cost: radius check + HP object. View: tint + beam. Zero motion — cheapest candidate.
4. **Resonant strike pillar** (player-exploitable prop; strike-prop family 4/11, but placed shockwave-emitter form ≈1/11 [H2], borderline G8 — argue placed-form scoring) — Hitting it emits a fixed-radius shockwave damaging enemies, then a visible deterministic cooldown. Gives arenas a verb; pairs with guardian hold zones. Sim cost: cooldown timer + radial test.
5. **Paired ember-gates** (portal pair; 2/11, G8 pass) — Two fixed pads teleport any entity standing 0.5s on one to its partner (symmetric; enemies use it only if pathfinding cost is accepted — otherwise player+projectiles only, flag as scope decision). Reshapes routing on a flat plane where no pits exist. Sim cost: pad timer + position swap; deterministic by construction.
6. **Pendulum crusher lanes** (moving geometric sweeper; moving-hazard family saturated 7/11 — G8 fail as family, pass only if scored as fixed-path phase-locked sweeper distinct from chasers) — Sweeper oscillates on a data-defined segment/phase; symmetric damage. Keep as reserve: readable and cheap, but weakest novelty claim.
7. **Warden's sconce dark** (vision limit; 4/11, G8 fail on frequency — carried by theme) — Arena dims outside lit sconce radii; player ignites sconces in any order (deterministic state machine). Highest lore fit for a lantern game; implement as plane-space vignette mask overlay, never dynamic lights. Recommend only if paired with a passing gimmick, not as a signature.
8. **Rune-circuit gate** (timed objective; 6/11, G8 fail — variety lever only) — Activate 3 runes in fixed order under scheduled enemy pressure to open the exit. Cheap encounter-shape variety for mid-dungeon rooms; not a signature gimmick.

Recommended G8 submissions: candidates 1–3 as the three new dungeon signatures (all <=2/11, all symmetric-or-static, all trivially deterministic), candidate 5 as first alternate, 4 as second alternate pending placed-form scoring ruling.
