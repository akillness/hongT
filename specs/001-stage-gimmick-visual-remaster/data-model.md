# Data Model: Stage Gimmick Visual Remaster

## StageToneProfile

Immutable View data for one of the nine campaign stages.

| Field | Type | Rule |
|---|---|---|
| `StageId` | string | Exact `StageCatalog` ID; unique and campaign-only |
| `Act` | int | 1..3 |
| `Concept` | string | Frozen tone description used by prompts/review |
| `SurfaceTint` | Color | Multiplies physical surfaces only |
| `SecondaryGlow` | Color | Optional hazard/VFX secondary accent |
| `BedValue` | float | Dark, restrained floor integration range |
| `DressingDensity` | float | Presentation-only density multiplier |

Validation: exactly nine unique IDs, three per act, no Prologue/Arena/Training ID.

## HazardSurfaceBinding

One required mapping for an actual stage/hazard combination.

| Field | Type | Rule |
|---|---|---|
| `StageId` | string | References `StageToneProfile` |
| `Kind` | `HazardKind` | Existing enum; never serialized back into Sim |
| `PrimaryRole` | string | `underlay`, `body`, `bed`, or `band` role |
| `ResourcePath` | string | Extensionless `Resources/Textures/Hazards/...` |
| `OpacityClass` | enum | `Opaque`, `FeatheredOpaque`, or `AlphaTrim` |
| `WrapMode` | enum | Clamp except body/declared tiled band |
| `UvScale` | Vector2 | Positive; derived from physical footprint |
| `TintMode` | enum | Stage surface or neutral shared mask |
| `Fallback` | enum | Existing #17c material/primitive; never error color |

Validation: the effective catalog-derived set has exactly 33 unique pairs and one
required primary binding for each. Extra unused mappings fail validation.

## HazardTextureAsset

Imported resource metadata derived from filename and manifest.

| Field | Rule |
|---|---|
| `FileName` | `<stageId>-<hazard>-<role>.png` |
| `Width`, `Height` | Each <=1024; normally square |
| `ColorSpace` | sRGB for physical color, linear/alpha-preserving for masks as declared |
| `AlphaPolicy` | Primary surface interior alpha >= 0.98; trims preserve source alpha |
| `Wrap` | Clamp except body/current/ashwall roles declared tileable |
| `Mipmap` | On for physical surfaces; role contract controls masks |
| `Compression` | Platform-supported compressed texture |

## GenerationProvenance

One record per accepted generated or deterministic derivative asset.

Required fields: `stage_id`, `act`, `hazard_kind`, `role`, `tool`, `provider`,
`model`, `prompt`, `negative_prompt`, `input_refs`, `dry_run_artifact`,
`smoke_artifact`, `generation_attempts`, `source_output`, `output_path`,
`postprocess`, `sha256`, `dimensions`, `mode`, `import_settings`,
`runtime_consumer`, `mapping_scale`, `acceptance_metrics`, `validation_artifacts`,
and `decision`.

## VisualEvidence

| Field | Rule |
|---|---|
| `StageId` | One of nine campaign IDs |
| `Viewport` | 1920x1080, 1280x720, or 375x667 |
| `Moment` | entry, combat, active-hazard/boss, close-boundary |
| `Artifact` | Repository-relative screenshot/report path |
| `LeakCount` | 0 required |
| `UvDefects` | 0 required |
| `Readability` | pass/fail live and grayscale |
| `ToneMatch` | pass/fail against frozen matrix |
| `BrowserErrors` | 0 required |

## Runtime State

```text
Unscoped
  -> SetStageContext(campaign stage)
StageResolved
  -> first SyncHazards / BuildHazardView
TexturesBound
  -> EndRun, ClearTransient, disable, or non-campaign Begin
Unscoped
```

Missing optional trims retain existing #17c rendering. A missing required primary is
safe at runtime but fails tests/release evidence; it never produces white or magenta.
