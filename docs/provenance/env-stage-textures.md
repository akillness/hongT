# Provenance — stage environment albedo textures

`[OBSERVED]` unless marked otherwise.

## What

Per-stage tiling albedo maps for the dungeon gimmick geometry built by
`Assets/Scripts/View/EnvironmentBuilder.cs` (walls / pillars / gates / galleries
/ bridges = `env-stone`, Zone A accent panels = `env-floor`).

- Output: `Assets/Resources/Textures/Env/<stageId>-{stone,floor}.png`
- Consumer: `EnvironmentBuilder.ApplyStageTextures(stageId)`, which rebinds
  `_BaseMap` on the two shared materials at stage entry (one stage is live at a
  time, so the §E7 4-material environment budget is unchanged).
- Tiling density is unchanged: the existing per-piece `_BaseMap_ST` MPB
  (`TilesPerWorldUnit = 1/1.28`) already sized UVs for a real texture.

## Tool (CLAUDE.md §3)

god-tibo-imagen, via `tools/gen_env_textures.sh`:

```
gti --provider codex-cli --prompt "<stage concept>, <common tiling clause>" \
    --output Assets/Resources/Textures/Env/<stageId>-<class>.png
```

- `[OBSERVED]` The default `private-codex` provider returns **HTTP 429** for
  every call on this machine (single call, and after a 45 s idle gap), so the
  script pins `--provider codex-cli`, which succeeds.
- `[OBSERVED]` A 4-way parallel burst 429s instantly; the script is strictly
  serial with exponential backoff and an 8 s courtesy gap.
- `[OBSERVED]` `codex-cli` ignores `--size` and returns ~1254×1254 PNG.
  `Assets/Editor/EnvTextureImportPipeline.cs` clamps `maxTextureSize` to 1024
  (WebGL ceiling, CLAUDE.md §1) and forces `wrapMode = Repeat` + mipmaps.

## Prompts

The per-stage concept clauses live in the `STAGES` table of
`tools/gen_env_textures.sh` (source of truth — not duplicated here so the two
cannot drift). Every prompt is suffixed with the shared clause:

> seamless tileable square texture, flat even lighting with no baked shadows or
> highlights, orthographic flat albedo map for a game engine, edges wrap
> perfectly, no text, no logo, no border, no vignette

## Re-run

```
bash tools/gen_env_textures.sh          # idempotent: skips non-empty outputs
```

Gate: `DungeonFramingAndMoodTests.StageTextures_*` (EditMode) fails when a map
is missing, non-Repeat, or above the 1024 ceiling.
