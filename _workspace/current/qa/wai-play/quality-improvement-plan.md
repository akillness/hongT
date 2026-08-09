# WAI Play presentation improvement plan

## Evidence-locked baseline

- WAI natural-flow run reached wave 1, 2 kills, score 450, then died after 15 actions.
- Overall score is intentionally withheld because observable coverage was only 76%.
- Retained screenshots show the low-health vignette obscuring most of the combat field and actor silhouettes reading too small at the gameplay camera distance.
- Action traces show 154-395 ms bridge round trips. The bridge currently polls in `LateUpdate`, after `GameView.Update`, so browser actions wait an avoidable render frame before the real `InputAdapter` path consumes them.

## Minimal behavior-preserving changes

1. Keep all deterministic simulation and frozen contracts untouched.
2. Reduce damage-vignette opacity while preserving a visible damage and low-health warning.
3. Restore the global actor presentation scale from 0.80 to the authored 1.00 while preserving all relative player/enemy/boss scale ratios. A first 0.90 pass remained visually indistinguishable from baseline, so it was rejected after retained screenshot review.
4. Poll WAI actions before the normal `GameView.Update` and publish the post-simulation observation in `LateUpdate`.

## Regression locks

- Pin damage-punch and low-health vignette bounds with EditMode tests.
- Keep the existing proportional actor-scale test and update its explicit presentation target.
- Pin the bridge execution order so a later refactor cannot silently restore the extra-frame latency.

## Verification and stop condition

- Unity import succeeds with no new compile errors.
- Full EditMode suite passes apart from the existing documented skip.
- Fresh WebGL build succeeds.
- Browser API smoke check has no page/console errors and real movement/reset still work.
- A same-route WAI replay and retained screenshots show whether latency and visual legibility improved; report unobserved key nodes as gaps rather than inventing a score.
