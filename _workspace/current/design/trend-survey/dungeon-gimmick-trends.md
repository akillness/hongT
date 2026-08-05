# Trend Survey Summary: Per-Dungeon Environmental Gimmicks (Cycle 2, Stage 1 Phase 1a)

Full artifacts: `.survey/dungeon-gimmick-trends/{triage.md,context.md,solutions.md}`. Pool: 11 titles (Hades, Hades II, Dead Cells, Vampire Survivors, Halls of Torment, Diablo IV NM dungeons, Path of Exile, Enter the Gungeon, Binding of Isaac: Repentance, Moonlighter, Death Must Die). Evidence: indexed snippets via web search aggregation unless noted; (t) = thin evidence. Calibration numbers (telegraph seconds, damage % HP) are owned by the parallel QA lane: `_workspace/current/qa/benchmark-notes.md`.

## Frequency Table (G8 input)

✓ present, — absent, (t) thin. Novelty gate: archetype qualifies if present in <=2 of surveyed titles.

| Gimmick archetype | HAD | H2 | DC | VS | HoT | D4 | PoE | ETG | BoI | ML | DMD | Count | G8 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Movement blocker / destructible obstacle | ✓ | ✓ | ✓ | ✓ | ✓(t) | ✓ | ✓ | ✓ | ✓ | ✓ | — | 10 | no |
| Static lingering hazard floor | ✓ | ✓ | ✓ | — | ✓ | — | ✓ | ✓ | ✓ | ✓ | — | 8 | no |
| Moving hazard / chaser entity | ✓ | — | — | ✓ | ✓ | ✓ | — | ✓(t) | ✓ | ✓ | — | 7 | no |
| Cursed zone / risk-reward object | ✓ | ✓(t) | ✓ | — | — | ✓ | ✓ | ✓ | ✓ | — | — | 7 | no |
| Spawner/enemy-rule alteration per stage | ✓ | — | — | ✓ | ✓(t) | — | ✓ | — | ✓(t) | ✓ | ✓ | 7 | no |
| Timed objective / fixed-clock event | — | — | ✓ | ✓ | ✓ | ✓(t) | ✓ | — | — | ✓ | — | 6 | no |
| Shrine/altar zone buff | — | — | — | — | — | ✓ | ✓ | ✓ | ✓ | ✓(t) | — | 5 | no |
| Darkness / vision limit | ✓ | — | ✓(t) | — | — | — | ✓ | — | ✓ | — | — | 4 | no |
| Player-exploitable strike prop | ✓ | ✓ | — | — | — | — | — | ✓ | ✓ | — | — | 4 | no |
| Periodic scheduled AoE eruption (placed vent) | — | ✓(t) | — | — | — | ✓ | — | — | — | — | — | 2 | **yes** |
| Shrinking / encroaching safe zone | — | — | — | — | ✓ | — | ✓(t) | — | — | — | — | 2 | **yes** |
| Transport rail / mover (minecart) | — | — | — | — | — | — | — | ✓ | ✓ | — | — | 2 | **yes** |
| Teleporter / portal pair in arena | — | — | — | — | — | — | — | ✓(t) | ✓ | — | — | 2 | **yes** |
| Conveyor / current / push field | — | — | — | — | — | — | — | ✓(t) | — | — | — | 1 | **yes** |
| Enemy-empowering aura object (pylon) | — | — | — | — | — | — | ✓(t) | — | — | — | — | 1 | **yes** |

Existing Cinder Court trio maps to: ember-vent = periodic eruption (2/11 — actually uncommon in placed form), obsidian-pillar = blocker (10/11, saturated), relic-altar = shrine buff (5/11, common). New signatures must come from the six **yes** rows.

## Key Insight
Stage identity = ONE signature spatial/scheduling rule per stage over shared saturated furniture (blockers, hazard floors). The unsaturated space is force/topology manipulation (push, shrink, portals) and enemy-side objects. Fairness is bought with symmetry (Gungeon pits/tables — loved) or predictability (HoT ghost wall, VS clock — loved); player-relative ambush gimmicks (D4 afflictions) draw sustained "anti-chill" complaints. (provenance: indexed snippet synthesis)

## Key Gap
No surveyed title runs hazards on a fixed, learnable per-arena timeline — everything is RNG-placed, player-relative, or one global timer. A deterministic multi-gimmick hazard rotation (learnable like a boss pattern) is unoccupied design space, and it is exactly what the no-RNG 60Hz sim does natively. Determinism should be marketed as the identity, not treated as a constraint.

## Ranked Candidates (fit = novelty × sim cost × WebGL cost × fairness)
1. **Tide-current channels** — scheduled directional push lanes, symmetric, AABB+velocity-add sim, scrolling-UV view. 1/11, G8 pass.
2. **Encroaching ash wall** — wall advances/recedes on fixed timetable, symmetric crush. 2/11, G8 pass.
3. **Ember-shield pylon** — destructible object granting nearby enemies visible damage reduction; enemy-side mirror of relic-altar; zero motion, cheapest. 1/11 (t), G8 pass.
4. **Resonant strike pillar** — hit → radial shockwave damaging enemies, deterministic cooldown. Placed-emitter form ≈1/11; needs G8 ruling that scoring is placed-form, not strike-prop family (4/11).
5. **Paired ember-gates** — fixed portal pads; reshapes routing on a flat plane; enemy usage is a scope decision (pathfinding cost). 2/11, G8 pass.
6. **Pendulum crusher lane** — phase-locked fixed-path sweeper, symmetric; reserve only (moving-hazard family saturated 7/11).
7. **Warden's sconce dark** — vignette-mask vision limit lit by ignitable sconces; best lore fit, G8 fail on frequency (4/11) — pair with a passing gimmick only.
8. **Rune-circuit gate** — ordered rune activation under scheduled pressure; variety lever, not a signature (6/11).

Recommended G8 submission: 1–3 as the three new dungeon signatures; 5 first alternate; 4 second alternate pending scoring ruling.

## Implementation frame (all candidates)
Data-driven placement rows: `{type, x, y, radius/extent, period_ticks, phase_ticks, damage_rule: playerOnly|symmetric, path?: segment}`. Telegraph decals drawn in plane space, y-scaled 1.42 for iso; follow windup → indicator → execution convention with decal == hitbox exactly (indexed snippet, telegraph design corpus). No dynamic lights, pooled VFX, reduced-motion fallback = static decal without pulse.
