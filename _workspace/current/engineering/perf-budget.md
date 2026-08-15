# Performance Budget — Unity 6000.5/WebGL, cycles 9–13

## Verdict

- `[OBSERVED]` Active run-id: `20260808-achilles-quality`; public beat: **NAN 2026 final submission**.
- `[OBSERVED]` The latest retained EditMode XML is `engineering/unity-logs/test-results-185352.xml` with `total="808" passed="808" failed="0"`.
- `[OBSERVED]` The latest retained WebGL build log is `engineering/unity-logs/build-185425.log`; it records Unity `6000.5.6f1`, `result=Succeeded`, `size=104803048`, `errors=0`, `warnings=6`, and `time=00:01:19.6353650`.
- `[OBSERVED]` Cycle-8 retrospective records WebGL errors 0 and browser console errors 0.
- `[OBSERVED]` None of those artifacts measures active-run p95 frame time, long-frame percentage, input latency, or a 30-minute memory soak.
- `[TARGET]` **Current five-cycle G6 performance verdict: FAIL — required measurements are absent.** A passing test/build is not a performance measurement.

## Numeric gate table

| Metric | Required gate | Current measured value | Method required for a verdict | Evidence path | Status |
|---|---:|---:|---|---|---|
| p95 frame time | `[TARGET]` ≤16.7ms | `[OBSERVED]` absent | `[TARGET]` Capture real frame durations on the released WebGL player after warm-up; record sample count, route/device/browser, raw capture, and computed p95 without replacing it with `PostFxGate`'s flag window. | `[TARGET]` `engineering/tech-verification/cycle-9-unity-webgl.md#frame-time` initially; refreshed per changed cycle. | `[TARGET] FAIL until measured` |
| Long-frame rate | `[TARGET]` <0.5% | `[OBSERVED]` absent | `[TARGET]` From the same raw frame-duration capture, divide frames above 16.7ms by all valid measured frames; exclude loading/stall samples only when the exclusion rule and count are written before the verdict. | `[TARGET]` Same capture plus calculation row. | `[TARGET] FAIL until measured` |
| Long-session memory | `[TARGET]` stable over a 30-minute soak | `[OBSERVED]` absent | `[TARGET]` Run one uninterrupted released-WebGL route for 30 minutes after a documented warm-up; sample the same browser memory measure at fixed checkpoints; PASS only if the series settles to a bounded plateau with no sustained growth. Record the exact browser/tool because no memory-profiler package is evidenced in this repository. | `[TARGET]` `engineering/tech-verification/cycle-9-unity-webgl.md#memory-soak` plus raw capture. | `[TARGET] FAIL until measured` |
| Input-to-feedback latency | `[TARGET]` ≤100ms | `[OBSERVED]` absent | `[TARGET]` On released WebGL, timestamp the physical/key/touch action and first visible or audible game feedback using one declared capture method; report every trial and the worst observed value for attack, dash, one skill, one lobby purchase, and one touch action. | `[TARGET]` `engineering/tech-verification/cycle-9-unity-webgl.md#input-latency` plus capture. | `[TARGET] FAIL until measured` |

## Test routes

| Route | What must be exercised | Why it is load-bearing |
|---|---|---|
| Combat crowd | `[TARGET]` Dungeon wave with maximum observed live enemies, repeated light hits, kills, combo finisher, skill bursts, damage numbers, loot, and a wave warning. | `[OBSERVED]` `GameView.DispatchEvents` can fan one tick into impact, camera, VFX, audio, HUD, and digest work; VFX and numbers rely on fixed pools whose eviction behaviour is only visible under overlap. |
| Boss phases | `[TARGET]` Warden, Tactician, and Sovereign entrances plus every phase boundary and finish. | `[OBSERVED]` Archetype bosses instantiate unpooled once per run, phase boundaries reuse one transition event, and presentation touches HUD/camera/audio/VFX concurrently. |
| Lobby/meta churn | `[TARGET]` Open/close map/controls, enter Sanctum, switch growth/equipment/legion/sigil tabs, purchase/equip, refresh, return from a run, and reload. | `[OBSERVED]` Two current canvases use different scaler matches; `MetaScreenView.Build` does not currently tear down an old root on a second build. |
| Thirty-minute mixed soak | `[TARGET]` Repeat lobby → stage → reward → lobby without reloading the page, while exercising pickups, boss, and post-processing degradation. | `[TARGET]` This is the minimum route that can expose retained views/materials/canvases rather than a short arena-only plateau. |
| Support-floor input | `[TARGET]` 375×667 keyboard and touch paths with reduced motion both off and on. | `[OBSERVED]` G6 input and G4 feedback latency apply to the released WebGL interaction surface, not only desktop Editor controls. |

## Allocation and cost audit

| Source/symbol | Current behaviour | Allocation/performance risk | Recommendation |
|---|---|---|---|
| `CinderSim._mayAttack`, `PlanEnemyGroup` | `[OBSERVED]` `_mayAttack` starts at `SimConfig.EnemyCap`; planning is fixed scans and id tie-breaks, with a resize guard only if the backing enemy array is larger. | `[INFERENCE]` Steady-state group planning is allocation-free; increasing enemy storage beyond the cap could trigger a one-time resize and raises $O(tokens\times enemies)$ scan cost. | `[TARGET]` Preserve the cap and scan shape; any coordination retune changes table values before algorithm shape. |
| `GameView` enemy pools | `[OBSERVED]` Ordinary actors use per-visual stacks; archetype bosses are intentionally unpooled and destroyed at run end. | `[INFERENCE]` Ordinary wave churn is bounded; boss instantiate/destroy can hitch once per run but avoids wrong-mesh pool reuse. | `[TARGET]` DEFER boss pooling unless spawn capture or soak proves a defect. |
| `DamageNumberPool` | `[OBSERVED]` Sixteen prebuilt `TextMesh` slots use oldest-slot eviction and memoized integer strings. | `[INFERENCE]` Steady-state numbers are allocation-free; bursts above 16 trade visibility for bounded memory. | `[TARGET]` Reuse unchanged; do not increase the cap without crowd-route evidence. |
| `VfxDirector` | `[OBSERVED]` Uses fixed arrays for four scorches, eight bursts, twelve sparks, four wave warnings, and eighteen shards; pickup/hazard objects are cleared on reset. | `[INFERENCE]` Burst feedback is bounded, but pickup/hazard destroy paths and flying-pickup lists need soak observation across repeated stages. | `[TARGET]` Keep current pools and verify reset plateau; fix only a measured retained object/material. |
| `ActorView` afterimages/equipment props | `[OBSERVED]` Afterimages reuse fixed mesh/transform/material arrays and destroy those resources in `OnDestroy`; equipment prop changes instantiate/destroy only when bands change. | `[INFERENCE]` Dash afterimages are bounded; repeated equipment changes can create managed/native churn outside combat. | `[TARGET]` Exercise purchases in the soak; do not move equipment instantiation into per-frame refresh. |
| `PostFxGate` | `[OBSERVED]` Uses a 120-frame over-budget flag ring, excludes stalls, allocates nothing after `Awake`, and degrades bloom/vignette one-way. | `[OBSERVED]` Its own source says the flag window is not a real p95 and cannot award G6. | `[TARGET]` Keep it as player protection; report independent p95/long-frame measurements. |
| `MetaScreenView.Build` | `[OBSERVED]` Clears `_contentFrames`, then creates a new `MetaScreen` root without removing a prior `_root`. | `[INFERENCE]` A repeated build can retain a canvas and its UI graph. | `[TARGET] FIX in cycle 9` with owner consolidation and idempotent build; include repeated build/open/close in the soak. |
| `WebGLStorage`/`CampaignStore` | `[OBSERVED]` Serialize on run-end/campaign-save transitions using shared builders, not every frame. | `[INFERENCE]` Current persistence is off the combat hot path; joined evidence rows could become expensive if emitted per tick. | `[TARGET]` Produce cycle-9 G5 rows by deterministic test/session export; if runtime emission is later required, buffer event-level rows and write only at declared transitions. |

## Per-cycle change budget

| Cycle | Performance rule | Allocation rule | Measurement rule |
|---|---|---|---|
| 9 | `[TARGET]` UI owner cutover may reduce duplicate UI; no mechanic retune. | `[TARGET]` Repeated `Build` leaves exactly one meta canvas; no new collections. | `[TARGET]` Establish all four numeric baselines and the 30-minute route before combat changes. |
| 10 | `[TARGET]` Impact/animation changes must remain inside existing event fan-out. | `[TARGET]` No per-hit object creation; reuse VFX, audio voices, damage numbers, and impact structs. | `[TARGET]` Re-run crowd frame/input probes and G8 impression/contact timing. |
| 11 | `[TARGET]` Coordination changes preserve deterministic fixed scans unless profiling proves the scan itself is the bottleneck. | `[TARGET]` No LINQ, delegates, temporary lists, or RNG in `Tick`/`PlanEnemyGroup`. | `[TARGET]` Re-run deterministic digest, maximum-enemy frame capture, and 30-minute mixed route if storage shape changes. |
| 12 | `[TARGET]` Boss presentation reuses current assets, MPB/event fan-out, and one-boss-per-run lifetime. | `[TARGET]` No per-frame material clone or renderer discovery; cache at rent/phase transition. | `[TARGET]` Re-run three archetype entrances/phases/finish, fallback, reduced motion, frame/input/memory routes. |
| 13 | `[TARGET]` Only measured bottlenecks are changed; optional runtime evidence emission stays outside the hot path. | `[TARGET]` Stable combat and soak loops allocate no unbounded collections. | `[TARGET]` Final release capture must make every gate row green with measured value + method + evidence path. |

## Unity and browser evidence packet

- `[TARGET]` Unity evidence names exact Editor version, focused tests, deterministic digest result, warnings/errors, and build size/result.
- `[TARGET]` Browser evidence names deployed URL/build identity, browser/device/viewport, reduced-motion state, route, raw performance capture, page errors, console errors, and interaction result.
- `[TARGET]` Every performance capture records warm-up, sample interval/count, exclusions, and raw artifact path before calculating a verdict.
- `[TARGET]` A missing p95, long-frame percentage, 30-minute series, or input trial leaves G6 FAIL even when `PostFxGate.DebugLine` is green.
