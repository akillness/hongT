# Archive disposition — `20260808-achilles-quality`

- recorded: `2026-08-08`
- decision: **no archive move is authorized**
- reason: the harness permits `_workspace/current/` → `_workspace/archive/{run-id}/` moves only at a valid cycle close. Cycle 9 remains Stage-2 `FIX`; cycles 10–13 are explicitly `not-validly-entered` prework. Moving their artifacts now would falsely encode a closed run.

## Current retention

Keep the active production manifest, decision log, gate reviews, cycle 8–13 retrospectives, design/PM/QA/engineering/ops source-of-truth files, cited Unity logs, and current video/document verification packets under `_workspace/current/` until cycle 9 receives a valid director gate decision.

## Existing archive

`_workspace/archive/20260808-achilles-quality/submission/video/` contains retained older media only. It is frozen: no addition, deletion, or claim that it represents a valid closed production cycle.

## Next archive trigger

After a valid cycle close, archive superseded lane material with `git mv`, keep only the next cycle's carried-forward truth in `_workspace/current/`, and record the transition in `production/task-manifest.md` and the closing retrospective. Until then, the correct archive action is no action.
