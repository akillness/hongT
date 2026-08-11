# Quickstart: Generate, Map, and Verify Stage Hazard Textures

Run from the repository root. Do not reset or broadly stage the shared worktree.

## 1. Freeze current evidence

```bash
git rev-parse HEAD
git status --short -- Assets/Scripts/View/GameView.cs Assets/Scripts/View/VfxDirector.cs Assets/Scripts/View/EnvironmentBuilder.cs Assets/Scripts/View/GameDirector.cs Assets/Scripts/Sim Assets/Resources/Fx Assets/Resources/Textures/Env
git diff -- Assets/Scripts/Sim Assets/Resources/Fx Assets/Resources/Textures/Env
```

Record hashes of protected/target paths in the engineering evidence directory.

## 2. Probe god-tibo-imagen

```bash
command -v gti
node --version
test -s "$HOME/.codex/auth.json"
gti --help
```

Use repeatable `--image` references. First run the exact representative request with
`--dry-run`, then one real output with sanitized debug artifacts. Do not start the
batch unless the real smoke succeeds and consumes the reference images.

## 3. Generate serially

```bash
bash tools/gen_hazard_textures.sh --dry-run
bash tools/gen_hazard_textures.sh --smoke
bash tools/gen_hazard_textures.sh --generate
```

The script must issue one request at a time, retry transient failures with bounded
backoff, skip already accepted/hash-matching outputs, and update provenance without
overwriting the 18 Env images or existing Fx sheets.

## 4. Import and run targeted gates

```bash
bash tools/unity_batch.sh import-only
bash tools/unity_batch.sh tests
```

Read Unity output; do not infer success from exit code alone. Targeted tests must
cover the source-derived binding matrix, importer roles, opaque pixels, safe fallback,
context reset, dressing caps, and existing #17c resource/state behavior.

## 5. Full WebGL verification

```bash
bash tools/unity_batch.sh build
```

Measure the fresh build (<=120 MB), serve it through the existing WebGL smoke
harness, then run:

```bash
node tools/qa/capture_stage_hazard_matrix.mjs \
  --url http://127.0.0.1:8766/ \
  --out _workspace/current/qa/stage-hazard-remaster \
  --viewports 1920x1080,1280x720,375x667 \
  --phase full
node tools/qa/capture_stage_hazard_matrix.mjs \
  --url http://127.0.0.1:8766/ \
  --out _workspace/current/qa/stage-hazard-remaster/perf-final \
  --viewport 1280x720 --phase perf --stage echo-throne --frames 660
```

Collect entry, combat, active-hazard/boss, and close-boundary evidence for all nine.
The harness must fail unless `GameFlowAgentAPI` reports an active wave and the HUD
health-bar crop reports mean `R-G > 20`; this prevents lobby screenshots from
passing. Compare the 600 post-warmup rAF intervals with the same-command baseline:
median and p95 <= baseline * 1.10, and >33.3ms ratio <= baseline + 0.05.

## 6. Final integrity check

```bash
git diff -- Assets/Scripts/Sim Assets/Resources/Fx Assets/Resources/Textures/Env Assets/Scripts/View/GameDirector.cs
git status --short -- Assets/Scripts/View/GameView.cs Assets/Scripts/View/VfxDirector.cs Assets/Scripts/View/EnvironmentBuilder.cs Assets/Editor/HazardTextureImportPipeline.cs Assets/Scripts/View/StageHazardVisualCatalog.cs Assets/Scripts/View/StageHazardTextureResolver.cs Assets/Resources/Textures/Hazards docs/provenance/stage-hazard-textures.json tools/gen_hazard_textures.sh
```

Completion requires fresh automated results plus visual evidence for every acceptance
criterion. A safe runtime fallback does not waive a missing required texture.
