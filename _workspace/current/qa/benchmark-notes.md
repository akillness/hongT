# QA Benchmark Notes — Cycle 2 Gimmick Calibration (run-id 20260806-dungeon-gimmicks)

2026-08-05 · game-qa (Benchmark Calibration Researcher) · Stage 1 Phase 1a survey artifact.
Purpose: calibration benchmarks for NEW dungeon gimmicks (deterministic, data-driven,
WebGL-safe) added on top of the existing three (ember-vent / obsidian-pillar / relic-altar).
Companion artifact: novelty/frequency survey is the designer lane
(`design/trend-survey/`, superset 11-title pool); THIS file owns the numbers
(telegraph seconds, damage % HP, ramp, readability, reduced motion). Agreed via IRC
with DesignerTrendSurvey 2026-08-05 — both lanes share the 6 titles below so G8
frequency rows stay comparable.

## Provenance legend

- **[direct spec]** — read from this repo's frozen contract (`docs/SIM_SPEC*.md`, code).
- **[indexed snippet]** — web-search-grounded summary over wiki/community/press pages
  (fandom, steamcommunity, reddit, access-ability.uk, diablowiki.net, gungeongod.com,
  aaronbsmith.com/gamepressure D4 settings lists). Retrieved 2026-08-05; the search layer
  returned excerpts, not full pages — treat as secondary.
- **[INFERENCE]** — convention observed in play/analysis but not publicly documented as
  an exact number. Stated honestly as an estimate; do not cite as fact in gate verdicts.

## Our own anchors [direct spec]

| Anchor | Value | Source |
|---|---|---|
| Sim tick | 60 Hz fixed step, deterministic, NO RNG | `SimConfig.FixedStep`, SIM_SPEC_CAMPAIGN §Determinism |
| Player HP | 100 base; dungeon max = 100 + vit×8 + cloak(+8/rank, ≤5) → ~100–188 | SIM_SPEC_CAMPAIGN §Item drops, cycle2-spec §B0 |
| ember-vent | radius 90, period 2.4 s, **telegraph 0.8 s**, damage 8 (= 8% of base HP; Ward negates) | SIM_SPEC_CAMPAIGN §ember-vent |
| obsidian-pillar | radius 40 blocker, push-out, iso metric (dy×1.42) | SIM_SPEC_CAMPAIGN §obsidian-pillar |
| relic-altar | radius 70, hold 1.2 s → oil +18, cooldown 6 s | SIM_SPEC_CAMPAIGN §relic-altar |
| Reduced motion | ViewPrefs.ReducedMotion: hit-stop/slow-mo off, shake/flash ×0.4; hazards are sim-authoritative so the toggle NEVER changes outcomes | cycle2-spec §A5/§B4 |

## Per-title calibration table (6 titles)

| Title | (a) Telegraph duration & grammar | (b) Hazard damage as % player HP | (c) Stage N+1 ramp convention | (d) Simultaneous-telegraph readability | (e) Accessibility / reduced motion |
|---|---|---|---|---|---|
| **Hades** | No instant attacks; every major attack has telegraph → windup → recovery; standardized ground circles for AoE [indexed snippet]. Exact windups undocumented; observed ~0.5–1.2 s for regular AoE, longer for arena-wide boss slams [INFERENCE] | Regular hits ~5–15% of a 50–150 HP pool; boss heavies ~20–30%; almost nothing one-shots [INFERENCE — wiki lists flat damage, HP varies by Mirror] | Per-biome NEW mechanic (Asphodel lava floor, Elysium respawning shades) + more simultaneous attackers; mechanics-first, stat inflation second [indexed snippet + INFERENCE] | Boss + adds keep distinct telegraph channels (ground circle vs projectile vs lunge line); rarely >2–3 distinct AoE circles live at once; failure is attributed to greed, not unreadability [indexed snippet] | God Mode: +20% dmg reduction, +2%/death to 80%, toggle anytime; screen-shake toggle [indexed snippet] |
| **Dead Cells** | Retracting spikes shine/pulse before extending; flails have rhythmic learnable cycles [indexed snippet]. Pulse-to-damage window subsecond, ~0.5–1.0 s [INFERENCE] | Trap damage scales per biome tier but is **capped ≈30% max HP per hit** [indexed snippet — fandom wiki]. That cap is the genre's clearest "heavy ceiling" number | Biome tier raises enemy level AND trap damage together; boss-cell (BC) meta-ramp adds systemic pressure (Malaise soft-timer at 5BC: passive play punished) [indexed snippet] | Hazards are mostly static/rhythmic, learnable geometry rather than stacking dynamic telegraphs; density rises, cycle grammar stays fixed [indexed snippet + INFERENCE] | Assist Mode: independent trap damage AND trap speed sliders, parry window, enemy HP/dmg; progression not disabled [indexed snippet] |
| **Vampire Survivors** | Essentially NO formal hazard telegraphs — threat is body-contact crowd geometry; danger is communicated by density and approach vectors [indexed snippet] | Per-touch contact damage small (single digits vs ~100+ HP, i.e. ~1–5%/tick); lethality via stacking DPS not single hits [INFERENCE — wiki lists per-enemy power, builds vary] | Time-keyed waves: fixed-minute density jumps, elite/swarm events at scheduled timestamps; ramp = spawn table swap per minute, fully data-driven [indexed snippet + INFERENCE] | Deliberately accepts visual overload as fantasy; readability delegated to player position + white damage flashes. NOT a model for telegraphed AoE — negative benchmark for our lane [indexed snippet] | Flashing-VFX disable toggle; damage-number toggle to cut clutter; players still report overload at high intensity [indexed snippet] |
| **Halls of Torment** | Explicit Diablo-style ground circles/cones/lines before boss/elite attacks land [indexed snippet]. Fill-then-fire grammar (outline appears, zone fills, then damage) with ~1–2 s lead on heavy hits [INFERENCE] | Telegraphed boss/elite AoE is high — big chunk to one-shot territory (≥40%; some kill outright), which is WHY the telegraph is long and explicit [indexed snippet + INFERENCE] | Later stages: more elites with more overlapping telegraph patterns + agony modifiers; ramp = pattern count and overlap, not just numbers [INFERENCE] | Layers several zones at once but keeps shapes distinct (circle vs cone vs line); documented player complaint: telegraphs can blend into floor art — contrast is the failure mode, not count [indexed snippet] | Limited dedicated suite; relies on readable telegraphs themselves [thin evidence — no authoritative settings list retrieved] |
| **Enter the Gungeon** | Floor traps (flame jets, spike panels) run fixed visible cycles or arm on proximity with a short pop-up animation [indexed snippet]. Trap arm-to-fire ~0.3–0.6 s — tightest in this pool, paired with the lowest per-hit cost [INFERENCE] | Cleanest quantized grammar in pool: standard trap/contact = **half heart**; jammed = 1 full heart. On a typical 3–5 heart run: half heart ≈ 10–17% HP [indexed snippet — gungeongod/fandom] | Floor N+1: denser bullet patterns, faster projectiles, more trap-filled rooms; curse raises jammed (double-damage) frequency — damage quantum stays fixed, frequency/speed ramp [indexed snippet + INFERENCE] | Bullet-hell readability solved by uniform projectile color grammar (pink/red) + static learnable traps; hazards NEVER strobe; dodge-roll i-frames are the pressure valve [indexed snippet + INFERENCE] | Few dedicated toggles (screen-shake option present) [thin evidence]; readability carried by art grammar |
| **Diablo 3 / 4** | Ground affixes form-then-burn: Desecrator swirl expands before pool is "fully formed" and damaging; Plagued has a brewing/bubbling phase; Arcane Sentry has long audible+visual setup then slow-sweeping beam [indexed snippet — diablowiki]. Form time ~0.5–1 s; sentry setup ~1–2 s [INFERENCE]. Pools persist ~12 s once live [indexed snippet] | Ground effects tuned as DoT: individually ~5–15% HP/tick at on-level torment, lethal only if you stand in them or stack pools; one-shot ground damage is treated as a tuning failure by the community [INFERENCE] | Difficulty tier raises %HP/%dmg AND affix count per elite pack (more simultaneous ground-effect types); GR/Pit = pure multiplier ladder on the same telegraph grammar [indexed snippet + INFERENCE] | Multiple pools + beams routinely coexist; readability via strong color coding (orange/green/purple) per family; known failure: pool color vs floor tileset contrast [indexed snippet] | D4 has the fullest suite in pool: screen-shake off, combat hit-flash off, dedicated **Reduce Strobing** toggle, Highlight Player [indexed snippet — settings lists] |

## Derived calibration bands (QA recommendation to designer/PM — inputs to G2 band table)

These are the recommendations this survey exists to produce. Each is traceable to rows above.

1. **Telegraph duration scales with damage tier.** Genre-wide pattern: cheap hits get short
   warnings, expensive hits get long explicit ones (Gungeon 0.3–0.6 s @ ~12% HP …
   Halls of Torment 1–2 s @ one-shot). Recommended bands for new gimmicks at 60 Hz:
   - **light** (≤10% base HP, our vent's 8 dmg class): telegraph ≥ 0.8 s (48 ticks) — current vent is exactly at floor. [direct spec + INFERENCE]
   - **medium** (10–25% base HP): telegraph ≥ 1.2 s (72 ticks).
   - **heavy** (25–30% base HP): telegraph ≥ 1.8 s (108 ticks) + distinct audio cue (`HazardPulse`-family event).
2. **Damage ceiling.** No single gimmick hit above **30% of max HP** — Dead Cells' cap is the
   only published hard number in the pool and the genre treats one-shot environment damage
   as a fairness failure. [indexed snippet + INFERENCE]
3. **Warning visual grammar.** Convention = zone-shaped ground marker (ring/cone/line) that
   exists BEFORE damage, distinct per hazard family by color+shape, never flash-only.
   Under ReducedMotion the persistent zone marker must remain; only the blink/pulse
   modulates (matches our A5 contract: flash ×0.4, marker stays). [indexed snippet + direct spec]
4. **Ramp convention for stage N+1.** All 6 titles ramp by ADDING pattern/mechanic count and
   overlap before inflating numbers. Our existing catalog already follows it (cinder-span
   2 vents → ember-gallery 3 vents+pillar → witness-well altar+2 pillars+vent). New stages
   should introduce exactly ONE new gimmick kind per stage, then remix it with existing
   kinds in the following stage. [direct spec: StageCatalog hazard tables + INFERENCE]
5. **Simultaneous-telegraph budget.** Readable convention is **≤3 concurrently telegraphing
   hazards** in the combat plane, and ≤2 of the same kind phase-aligned. Because our phases
   are deterministic, this is mechanically checkable: max simultaneous `telegraphing==true`
   over one LCM of all periods (see test-plan.md §D3 — this is a QA gate check, not a vibe).
   [INFERENCE from Hades/HoT/D3 rows + direct spec determinism]
6. **Contrast is the real readability risk**, not count: both HoT and D3 rows show the failure
   mode is marker-vs-floor blending. New terrain (dressing lane) must keep hazard rings on a
   reserved accent channel; QA smoke includes a screenshot check per new stage terrain.
7. **Reduced-motion parity.** Pool norm: shake/flash toggles (Hades, VS, D4), damage-model
   toggles kept separate (Dead Cells Assist, Hades God Mode). Our split matches: ViewPrefs
   is view-only; sim outcomes NEVER differ with ReducedMotion on/off — that invariance is a
   regression test, not a promise. [direct spec]

## Curated sources

- docs/SIM_SPEC_CAMPAIGN.md, docs/SIM_SPEC_HACKSLASH.md, Assets/Scripts/View/StageCatalog.cs — [direct spec]
- Dead Cells wiki (fandom): trap damage biome scaling + ~30% cap — [indexed snippet]
- Enter the Gungeon wiki (fandom) + gungeongod.com: half-heart convention, jammed 1-heart — [indexed snippet]
- diablowiki.net: Desecrator/Plagued/Arcane formation delay, ~12 s pool persistence — [indexed snippet]
- access-ability.uk, steamcommunity: Hades God Mode numbers, VS flashing-VFX toggle — [indexed snippet]
- aaronbsmith.com / gamepressure D4 settings inventories: Reduce Strobing, hit-flash, shake, Highlight Player — [indexed snippet]
- Hades/HoT design analyses (medium, minimap.net, steamcommunity threads): telegraph grammar, readability framing — [indexed snippet]
- All exact seconds not published by developers are marked [INFERENCE] above and MUST NOT be
  cited as measured values in `qa/gate-measurements.md`.
