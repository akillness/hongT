# HongT WAI Play presentation-quality report

Date: 2026-08-09 (Asia/Seoul)

## Delivery status

- [OBSERVED] `git pull --ff-only` completed with the branch already up to date. Existing unrelated worktree changes were preserved.
- [OBSERVED] The requested WAI-compatible natural-flow bridge, presentation changes, browser evidence, tests, WebGL build, and independent code review are complete.
- [INFERENCE] The delivered slice materially improves desktop combat readability and automated playtestability, but it is not evidence for unconditional PRD readiness: upgrade choice, resource collection, boss flow, mobile presentation, and human performance remain unobserved.

## Implemented quality changes

### WAI / GameFlow testability

- Added `GameFlowAgentBridge` in the View layer. It reads simulation snapshots and routes actions through the real `InputAdapter`; it never mutates deterministic simulation state.
- Added the WebGL `window.GameFlowIntegration` / `window.GameFlowAgentAPI` v2 bridge.
- Supported natural-flow actions: eight-direction movement, attack, upgrade choices 1-3, collect, reset, and wait.
- Published structured player, world, combat, resource, upgrade, boss, and terminal state.
- Kept unsupported scenario jump/repair explicit: `SCENARIO_LOADER_NOT_IMPLEMENTED` and `REPAIRER_NOT_IMPLEMENTED`.
- Polls actions before normal `GameView.Update` and publishes the post-simulation observation in `LateUpdate`.

### Presentation

- Restored actor presentation to authored scale `1.00`, preserving relative player/enemy/boss ratios.
- Reduced hit vignette peak to `0.36` and constrained sustained low-health pulse to `0.06-0.18`.
- Added regression locks for bridge execution order, action routing, observation shape, terminal-state semantics, authored actor scale, and vignette bounds.

## WAI evidence

| Evidence | Baseline | Post-change |
| --- | ---: | ---: |
| Strict overall / five-star score | withheld | withheld |
| Observable scoring coverage | 76% | 76% |
| Capability coverage | 62% | 62% |
| Task flow | unscored | unscored |
| Gameplay | 60 | 60 |
| UI quality | 65 | 65 |
| Feedback | 53 | 48 |
| Technical quality | 58 | 54 |
| Evidence quality | 82 / high | 65 / medium |

- [OBSERVED] WAI correctly withheld the overall score because core-flow evidence was incomplete. Both natural-flow attempts ended in `overrun`; operator failure is excluded from the product score.
- [OBSERVED] The post report's 2818 ms latency card measures the recorded orchestration/evidence pipeline, not raw game response. Direct adapter probes measured 138 ms movement and 160 ms attack without recording; a 16-action recorded probe measured 182-411 ms (242.5 ms average) with zero page or console errors.
- [INFERENCE] The lower post feedback/technical dimensions are not credible regression evidence. Synchronous screenshots, post-step waits, recording, and clip padding contaminate the WAI wall-clock field, while the same bridge passes direct action checks.
- [OBSERVED] API functional checks detect the standard interface and classify it as `natural_flow`, `full_test_ready=false`, `scenario_test_ready=false`, `recommended_test_mode=limited_natural_flow`.

## Visual comparison

- [OBSERVED] At 1440x900, final actor silhouettes are approximately 10-20% larger than the retained baseline. Player height increased from roughly 135-145 px to 155-165 px; a representative enemy increased from roughly 80-90 px to 95-105 px.
- [OBSERVED] Indicative red-excess sampling fell from about 56.3 full-frame / 60.3 playfield in the heaviest baseline low-health frame to 25.3 / 27.1 in the final frame.
- [OBSERVED] No actor-HUD collision or new HUD overlap was found in the inspected frames.
- [INFERENCE] Larger actors improve pose, facing, and class-color recognition. The remaining trade-off is denser silhouette overlap in close melee, where health bars and damage numbers still carry hit attribution.

Retained evidence:

- Baseline: `_workspace/current/qa/wai-play/baseline/evidence/game_test_20260809_151514_c7d6cfdd/`
- Scored post run: `_workspace/current/qa/wai-play/post/evidence/game_test_20260809_152738_ff6f6d98/`
- Final authored-scale run: `_workspace/current/qa/wai-play/final-visual/evidence/game_test_20260809_154233_42c5d3aa/`

## Verification

- [OBSERVED] WAI static checker: 0 blockers, 0 warnings.
- [OBSERVED] JavaScript syntax check: passed.
- [OBSERVED] Focused EditMode tests: 25/25 passed (`test-results-wai-final-focused.xml`).
- [OBSERVED] Current full EditMode run: 850 total, 845 passed, 4 failed, 1 explicitly skipped (`test-results-154702.xml`).
- [OBSERVED] All four full-suite failures are outside this slice and follow the concurrently modified `CinderSim` deterministic digest: two frozen companion digest tests, one dungeon golden, and one momentum golden. This report does not modify or bless those external/frozen values.
- [OBSERVED] WebGL build: succeeded, 105,223,486 bytes, 0 errors, 6 warnings (`build-153625.log`).
- [OBSERVED] Browser smoke: movement, attack, and reset use real input; final 16-action run reached 4 kills with 41 HP; page errors 0, console errors 0.
- [OBSERVED] Independent code review: 0 critical/high/medium/low findings in the WAI/presentation scope.
- [OBSERVED] `git diff --check`: passed for all reviewed files.

## Remaining PRD gates

- [TARGET] Add honest scenario loading/repairing before claiming `full_test_ready`.
- [TARGET] Retain evidence for resource collection, an upgrade decision, boss entry/phase/result, and a successful end-to-end completion.
- [TARGET] Run 375x667 mobile viewport and real-device performance/accessibility checks.
- [TARGET] Resolve or explicitly rebaseline the four deterministic golden failures in the owner lane for the concurrent `CinderSim` change.
- [TARGET] Validate crowded-melee readability with a short human playtest; consider spacing/telegraph work only if the overlap is reproducibly harmful.

