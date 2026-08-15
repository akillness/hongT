# Stage Hazard Remaster Baseline

Captured: 2026-08-11 Asia/Seoul

## Fresh focused Unity evidence

`CinderCourt.Tests.DungeonFramingAndMoodTests` was run in Unity 6000.5.6f1
EditMode batch mode before implementation.

- Result: 12/12 passed, 0 failed, 0 skipped.
- XML: `_workspace/current/engineering/unity-logs/test-results-dungeon-mood-191634.xml`
- Log: `_workspace/current/engineering/unity-logs/tests-dungeon-mood-191634.log`
- Reported XML duration: 0.3772718 seconds.

Latest pre-feature full EditMode evidence:

- XML: `_workspace/current/engineering/unity-logs/test-results-185654.xml`
- Result: 907 total, 905 passed, 0 failed, 2 skipped.
- Duration: 35.5640925 seconds.

## WebGL baseline

Latest inspected build log:
`_workspace/current/engineering/unity-logs/build-181136.log`.

- Unity result: Succeeded.
- Reported output size: 110,989,836 bytes.
- Errors: 0; warnings: 6; build time: 38.775531 seconds.
- Current `build-webgl` directory: 89,625,820 bytes (85.47 MiB).
- Largest compressed data file: 69.17 MiB.
- Largest compressed wasm file: 10.12 MiB.

## Existing browser/capture baseline

`_workspace/current/qa/amendment17c-smoke/full-browser-report.json` contains all
nine stages at 1440x900 with 15 normal screenshots each, automation pass true, and
zero page errors. Stage reports live below:

```text
_workspace/current/qa/amendment17c-smoke/stages-normal/01-cinder-span/
_workspace/current/qa/amendment17c-smoke/stages-normal/02-ember-gallery/
_workspace/current/qa/amendment17c-smoke/stages-normal/03-abyss-chancel/
_workspace/current/qa/amendment17c-smoke/stages-normal/04-witness-well/
_workspace/current/qa/amendment17c-smoke/stages-normal/05-echo-throne/
_workspace/current/qa/amendment17c-smoke/stages-normal/06-ash-verdict/
_workspace/current/qa/amendment17c-smoke/stages-normal/07-cinder-sluice/
_workspace/current/qa/amendment17c-smoke/stages-normal/08-ember-bastion/
_workspace/current/qa/amendment17c-smoke/stages-normal/09-ash-march/
```

Reduced-motion representatives exist for `ember-gallery`, `echo-throne`, and
`ash-march`; `_workspace/current/qa/amendment17c-smoke/reduced-motion-metrics.json`
passes with a representative ember-gallery mean delta of 0.3559%.

## Pinned frame metric

The prior artifacts do not contain comparable rAF frame intervals. Before rebuilding,
the new committed matrix harness will run against this untouched `build-webgl` and
write `_workspace/current/engineering/hazard-texture-gen/perf-baseline.json`: 660
intervals on `echo-throne` at 1280x720, discard 60, retain 600, record median, p95,
and ratio above 33.3ms under Chromium/SwiftShader. The final build must use the same
command and browser configuration.
