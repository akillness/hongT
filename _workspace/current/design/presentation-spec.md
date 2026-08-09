# Cinder Court Presentation Specification — Cycles 9–13

- `[OBSERVED]` Active program run-id: `20260808-achilles-quality`; public beat: **NAN 2026 final submission**.
- `[OBSERVED]` Cycle 9 is Stage 2/Phase 2a and preserves the current concept; this spec first documents the shipped presentation, then defines future targets for cycles 10–13.
- `[TARGET]` Presentation makes the court’s deterministic state readable. It never invents hidden timing, moves an actor for spectacle, or changes a simulation outcome.

Status convention: each row/paragraph begins with `[OBSERVED]`, `[INFERENCE]`, or `[TARGET]`; status-labeled section headings apply to the full subsection.

## `[TARGET]` Presentation sentence

**Charcoal establishes the court, ember names danger, spectral cyan reveals memory and guardians, and gold confirms judgment; shape and timing carry the same message when motion or color is unavailable.**

See [worldview.md](worldview.md) for lore/color authority, [core-loop.md](core-loop.md) for timing/action authority, and [concept.md](concept.md) for cycle scope.

## `[OBSERVED]` Shipped presentation foundation

| Status | Beat/system | Current fact | Evidence owner |
|---|---|---|---|
| `[OBSERVED]` | Combat impact | Kill/finisher hit-stop, shake tiers, death pop, hit flash, damage-number pool, skill rings, and attack trail are implemented/spec-governed View feedback around sim events. | `integrated-combat-vfx-spec.md`, View code/tests. |
| `[OBSERVED]` | Boss entry | A 1.2 s letterbox/name intro is input-safe; dungeon camera focus returns to arena center. | `PresentationFeedbackTests`, `HudView`, `CameraRig`. |
| `[OBSERVED]` | Boss phases | Phase state and variable telegraph durations are exposed by the deterministic boss profile; HUD phase pips and transition feedback consume the state. | `DungeonProgressionSpec`, boss tests. |
| `[OBSERVED]` | Clear | Stage clear immediately starts a visible ceremony, suppresses premature terminal input, and cannot duplicate its terminal panel. | `PresentationFeedbackTests`. |
| `[OBSERVED]` | Story | Watcher, boss role, and warden/guardian speaker classes resolve to distinct palette classes across every stage catalog beat. | `StoryCatalog`, `PresentationFeedbackTests`. |
| `[OBSERVED]` | Reduced motion | A player toggle/OS hint feeds `ViewPrefs`; reduced motion sets motion amplitude to 0.4 and disables hit-stop/slow-motion time effects. | `ViewPrefs.cs`. |
| `[OBSERVED]` | WebGL performance | Dungeon post-processing is guarded by a live 120-frame p95-equivalent watchdog and degrades one-way after a sustained breach. | `PostFxGate.cs`. |
| `[OBSERVED]` | Support floor | Cycle 8 names 375×667 CSS px as the minimum viewport condition and 44 CSS px as the interactive control floor. | `../retrospectives/cycle-8-retrospective.md`. |

`[INFERENCE]` The foundation already has strong event feedback but does not by itself guarantee that a player knows which enemy owns the next attack, which action answer is intended, or why a room rule changed the outcome.

## `[TARGET]` Salience ladder

| Status | Priority | Signal | Maximum simultaneous high-salience count |
|---|---:|---|---:|
| `[TARGET]` | 1 | Immediate lethal/unguardable threat and active attacker | 1 primary + 1 overlapping room threat |
| `[TARGET]` | 2 | Boss phase/punish boundary, pylon protection break, guardian command result | 1 |
| `[TARGET]` | 3 | Guardable attack, current/wall/vent telegraph, altar hold | Existing total telegraph cap across the frame: 3 |
| `[TARGET]` | 4 | Damage, XP, pickup, combo, build explanation | May queue; must not cover priority 1–3 |
| `[TARGET]` | 5 | Atmosphere, dust, bloom, idle camera motion | First to reduce or disable |

- `[TARGET]` Only the **Acting Bailiff** (the current attack-token owner) receives the full ember threat chevron. Witness Line enemies remain 40–55% lower luminance and never flash like an active attacker.
- `[TARGET]` Flank threat uses a side-origin wedge plus direction, not a second full-screen flash.
- `[TARGET]` Pylon protection uses an aura boundary tethered to the pylon body; `PylonDown` removes both in the same presented frame, target feedback latency ≤100 ms.

## `[TARGET]` Action-answer readability

| Status | Answer | Before commitment | During commitment | Recovery/punish signal |
|---|---|---|---|---|
| `[TARGET]` | Committed Strike | Thin forward ember arc; target/pylon silhouette remains visible. | Trail follows the actual active window; no false early impact. | Arc closes and weapon ember falls; punish glow appears only on a verified opening. |
| `[TARGET]` | Lantern Dodge | Threat lane remains visible; intended escape side gets no automatic recommendation. | 0.22 s travel follows actual simulation position; cyan/ember line may trail but cannot extend collision meaning. | Oil/cooldown state is visible within 100 ms. |
| `[TARGET]` | Witness Guard (future) | Guardable claim = closed ring; unguardable claim = broken ring; both persist ≥0.30 s. | 0.16 s counter portion uses a tightening cyan ring; 0.34 s tail uses a stable half-ring. | Success opens 0.60 s gold punish marker; whiff shows 0.35 s dimmed lantern recovery. |
| `[TARGET]` | Environment | Telegraph shows exact affected shape and phase direction. | Current/wall/pylon/altar state remains anchored to simulation geometry. | Displacement, protection removal, oil burst, or kill receives one bounded confirmation. |
| `[TARGET]` | Guardian | Hold/recall target and path are cyan; invalid command is crossed, not silently ignored. | Active guardian angle/target uses a thin tether, below threat salience. | Command/skill cooldown and result update within 100 ms. |

`[TARGET]` Cycle 9 does not display Witness Guard because the mechanic is not implemented. Its visual language is a future contract that activates only after the cycle-10 mechanic gate in [concept.md](concept.md).

## `[TARGET]` Presentation beats

### Beat P1 — Chamber entry: “The court states its rule”

```yaml
beat: chamber-entry
budget_s: [2.2, 5.2]
input_blocked: false
required: [stage_title, court_function_line, hazard_shape_preview, safe_player_silhouette]
```

- `[OBSERVED]` Story bubble hold time already derives from text length and is clamped to 2.2–5.2 s.
- `[TARGET]` Within the first 1.0 s, show the stage epithet and one hazard-function glyph; by beat end, the player must see both Reaver and first navigable lane.
- `[TARGET]` Success: ≥80% of first-time testers can name the room rule before first damage. Failure: cinematic framing hides the actionable floor or requires external instructions.

### Beat P2 — Wave order: “Who acts, who waits”

```yaml
beat: wave-order
banner_hold_s: 1.2
threat_signal_latency_ms_max: 100
simultaneous_high_salience_max: 3
```

- `[OBSERVED]` `WaveStarted` already has a banner/event seam; Hard/Nightmare have deterministic attack tokens, ring slots, and flank bias.
- `[TARGET]` Show active-attacker ownership after spawn warning and before contact. A token handoff gets ≤0.20 s chevron transfer and one short non-verbal audio tick.
- `[TARGET]` Success: ≥80% of incoming damage is attributed by testers to a remembered cue; no high-salience frame exceeds 3 cues.

### Beat P3 — Court turn: “The room changes the answer”

```yaml
beat: court-turn
confirmation_ms_max: 100
confirmation_hold_s: [0.35, 0.80]
```

- `[TARGET]` `PylonDown`, enemy wall/current displacement, altar blessing, and guardian hold/recall each receive one distinct confirmation using existing court colors and shapes.
- `[TARGET]` Do not stack a screen flash, camera shake, large text, and audio sting for a routine event. Use at most two channels for priority-3 feedback; reserve four-channel emphasis for boss phase/clear.
- `[TARGET]` Success: ≥40% of successful G7 loops contain one recognized tactical environment event.

### Beat P4 — Boss entry: “The office enters”

```yaml
beat: boss-entry
intro_s: 1.2
letterbox_height_screen_fraction: 0.09
input_blocked: false
required: [role_title, phase_count, room_rule, boss_silhouette]
```

- `[OBSERVED]` The input-safe 1.2 s letterbox/name intro and focus pulse exist.
- `[TARGET]` Keep the court role title and stage-specific room rule visible; never use a foreign heroic title, mythological name, or copied pose.
- `[TARGET]` At 375×667, boss name, phase pips, player silhouette, and first threat lane must remain visible without control overlap.

### Beat P5 — Phase verdict: “The office changes procedure”

```yaml
beat: boss-phase
normal_time_effect_s_max: 0.50
reduced_motion_time_effect_s: 0
static_phase_hold_s_min: 0.50
phase_floor_s: 2.17
```

- `[OBSERVED]` Boss profiles expose two/three phases and live telegraph duration; current reduced-motion preference disables time effects.
- `[TARGET]` A phase change updates pip, cue grammar, and one sentence-form role line. Normal mode may use the current bounded time effect; reduced motion uses a static gold/cyan edge and phase label for ≥0.50 s without changing simulation time.
- `[TARGET]` Success: phase presentation begins within 100 ms and ≥70% of testers can name the new answer after the fight.

### Beat P6 — Final judgment: “Result before menu”

```yaml
beat: final-judgment
ceremony_s: 1.0
terminal_input_during_ceremony: false
reduced_motion_equivalent: static_flash_and_label
```

- `[OBSERVED]` Stage clear already starts a 1.0 s ceremony before enabling terminal input and guards against duplicate terminal panels.
- `[TARGET]` Preserve the Reaver/lantern silhouette, court-role defeat, gold verdict glyph, and one reward reveal. No external finisher pose, camera path, or prolonged slow motion is required.
- `[TARGET]` Reduced motion replaces zoom/shake/time effects with a stable verdict frame and immediate audio/label; information and reward timing remain equal.

## `[TARGET]` Reduced-motion and readability rules

```yaml
support_floor_css_px: [375, 667]
interactive_target_min_css_px: 44
feedback_latency_ms_max: 100
simultaneous_high_salience_max: 3
critical_text_contrast_min: 4.5
large_text_contrast_min: 3.0
critical_shape_cue_required: true
```

- `[OBSERVED]` Current reduced motion uses amplitude scale `0.4` and disables hit-stop/slow motion.
- `[TARGET]` Preserve that behavior. Decorative particles are capped at 50% of normal count; attack trails, camera shake, zoom pulses, and continuous idle orbit may be disabled. Static floor shapes, active-attacker ownership, phase pips, command state, health/oil/cooldown, and reward labels may never be removed.
- `[TARGET]` No flash exceeds two full-screen pulses per second; no necessary cue exists for <0.30 s unless it is continuously represented by a persistent state marker.
- `[TARGET]` Every hostile telegraph pairs color with geometry: circle/arc/band/wall/wedge/broken ring. Every friendly/memory signal pairs cyan with tether, lantern, label, or progress ring.
- `[TARGET]` Text does not ride on bloom. Essential Korean/English glyphs use the HUD font, remain inside safe area, and are tested at 375×667 and desktop landscape.
- `[TARGET]` Post-processing is optional decoration. The watchdog may disable it without changing cue contrast below the listed targets.

## `[TARGET]` Audio rules

- `[OBSERVED]` Current event audio reuses a bounded cue set and includes non-verbal combat, wave, pickup, and lore/BGM surfaces; narration voice is prohibited by the existing generation contract.
- `[TARGET]` Attack-token handoff, guardable/unguardable distinction, pylon down, phase verdict, and clear must have separable spectral/temporal shapes; do not communicate a mandatory distinction by pitch alone.
- `[TARGET]` Repeated host hits require pooled/round-robin playback or bounded pitch variation only after current deterministic simulation events are preserved; audio randomness must never enter the sim or digest.
- `[TARGET]` A reduced-motion player receives identical state information through audio/label/static shape; audio is complementary, not required for play.

## `[TARGET]` Scene scorecard and evidence

| Status | Scene | Primary question | Quantitative target | Evidence path |
|---|---|---|---|---|
| `[TARGET]` | Chamber entry | What does this room do? | ≥80% rule identification before first damage | `../qa/gate-measurements.md#g4` + session notes |
| `[TARGET]` | Coordinated wave | Who attacks next? | ≥80% damage attribution; cues ≤3 | QA playtest/event trace |
| `[TARGET]` | Court turn | Did my tactic change the state? | ≤100 ms feedback; ≥40% successful loops use tactic | G7 event trace |
| `[TARGET]` | Boss entry/phase | What office and answer changed? | ≥70% answer identification; phase ≥2.17 s | boss session trace |
| `[TARGET]` | Final judgment | What ended and what was earned? | 0 premature input; 100% reward label visibility | clear ceremony test/browser capture |
| `[TARGET]` | All scored scenes | Is the presentation immersive and readable? | median immersion ≥4.0/5; 0 unresolved S1/S2 readability complaints | `../qa/gate-measurements.md#g4` |

## `[TARGET]` Five-cycle presentation evolution

| Status | Cycle | Presentation scope | Exit proof |
|---|---|---|---|
| `[TARGET]` | 9 | Measure current experience; no benchmark-driven restyle. | G8 impression ≥4/5 now; current UI/effect reachability preserved. |
| `[TARGET]` | 10 | Prototype action-answer shapes in fixtures; Witness Guard remains gated. | ≥0.30 s guardable cue; reduced-motion equivalent; ≥2 valid answers in ≥80% threats. |
| `[TARGET]` | 11 | Surface attack-token order, flank direction, guardian angle, and room turns. | ≥80% damage attribution; handoff ≤0.20 s; cue cap ≤3. |
| `[TARGET]` | 12 | Differentiate court-role boss entries/phases and build-result explanation. | ≥70% answer identification and boss impression ≥4/5. |
| `[TARGET]` | 13 | Freeze content, run G4/G6/G1 at support floor, capture NAN submission evidence. | G4 median ≥4/5; ≤100 ms feedback; G6 green; 0 G1 violations/readability S1/S2. |

## `[TARGET]` Originality and asset boundary

- `[TARGET]` External sources may calibrate clarity, commitment, build expression, coordination, duel stakes, and environment use. They do not supply names, mythology, prose, slogans, UI layouts, camera reproductions, character/boss anatomy, art, prompts, code, or assets.
- `[TARGET]` All visible work must trace to [worldview.md](worldview.md), generated-asset provenance, and current HongT catalogs/specs. The cited calibration is isolated in [benchmark analysis](trend-survey/achilles-analysis.md).
