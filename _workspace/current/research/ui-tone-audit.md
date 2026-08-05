# UI Tone Audit — Cinder Court

Read-only survey. HEAD `5383577`. No code edited, no images generated, no Unity launched.

Every claim below is tagged `[OBSERVED]` (read from the repo or measured from a file) or
`[INFERENCE]` (reasoned from observed facts). Paths are repo-relative with line numbers.

**Headline:** the UI is **not** untextured — 15 authored PNGs are already mounted across ~47
`Image` instances. The problem is the opposite of "no art": the art that exists was drawn in a
**glossy, high-saturation mobile-gacha idiom** that is measurably 3× more saturated than the
approved concept art, and the one button plate that does exist is a **neon coral outline** whose
9-slice borders mathematically **cannot fit** two of the button sizes it is mounted on.

---

## 1. Authoritative palette and visual language

### 1.1 Where tone is actually defined

| Source | What it contributes | Status |
|---|---|---|
| `_workspace/current/design/deep-interview-cinder-court-dungeon-revival.md` L71 | The single normative sentence on visual grammar | `[OBSERVED]` |
| `_workspace/current/design/deep-interview-cinder-court-dungeon-revival.md` L48 | Mood statement | `[OBSERVED]` |
| `docs/provenance/cinder-court-link-preview.json` | Generation prompt + palette words for the key art | `[OBSERVED]` |
| `Assets/Scripts/View/LobbyView.cs` L6-8, L32-40 | The **only** place hex values are written down | `[OBSERVED]` |
| `_workspace/current/design/achilles-visual-overhaul-spec.md` §U (L113-115) | UI *structure* principles — contains **zero** colour values | `[OBSERVED]` |
| `docs/provenance/audio.json`, `docs/provenance/lantern-reaver-reskin.json` | Audio + character mesh provenance — **no UI palette** | `[OBSERVED]` |

`[OBSERVED]` `.omc/specs/` is **empty** (`ls` returns no files). The deep-interview spec named in
the brief lives at `_workspace/current/design/deep-interview-cinder-court-dungeon-revival.md`.

`[OBSERVED]` No design document in the repo states a UI hex value. The de-facto palette source of
truth is a **code comment** at `LobbyView.cs` L6-8.

### 1.2 The authoritative palette

Normative statement, `deep-interview-cinder-court-dungeon-revival.md` L71 `[OBSERVED]`:

> **Visual grammar:** dark stone, low blue-black fill, ember-orange hazard/fire, spectral-cyan
> memory/guardian signals. Every hostile telegraph contrasts against the floor and has a
> reduced-motion-safe static indicator.

Mood, same file L48 `[OBSERVED]`:

> The intended mood is **dark-souls-like dark fantasy**, not a reproduction of Dark Souls or
> Achilles.

| Token | Hex | RGB | Declared at | Role | Source |
|---|---|---|---|---|---|
| Panel fill | `#050409` @ 72% | 5, 4, 9 | `LobbyView.cs` L32 | "low blue-black fill" | `[OBSERVED]` |
| Border line | `#80D8FF` @ 34% | 128, 216, 255 | `LobbyView.cs` L33 | Cold rim on panel edges | `[OBSERVED]` |
| Cyan | `#2CADD6` | 44, 173, 214 | `LobbyView.cs` L34 | "spectral-cyan memory/guardian" | `[OBSERVED]` |
| Ember | `#F3592C` | 243, 89, 44 | `LobbyView.cs` L35 | "ember-orange hazard/fire" | `[OBSERVED]` |
| Gold | `#DDC869` | 221, 200, 105 | `LobbyView.cs` L36 | Reward / earned state | `[OBSERVED]` |
| InkDim | `#9EA8CC` | 158, 168, 204 | `LobbyView.cs` L37 | Secondary text | `[OBSERVED]` |
| Lock | `#6B7394` | 107, 115, 148 | `LobbyView.cs` L38 | Disabled state | `[OBSERVED]` |
| ButtonBack | `#29213D` @ 90% | 41, 33, 61 | `LobbyView.cs` L39 | Idle button fill | `[OBSERVED]` |
| ButtonActive | `#524729` @ 95% | 82, 71, 41 | `LobbyView.cs` L40 | Selected tab / roster | `[OBSERVED]` |

Additional tokens used in `HudView.cs` but **never named as constants** `[OBSERVED]`:

| Value | Hex | Used for | Line |
|---|---|---|---|
| `(0.05,0.04,0.09,0.55)` | `#0D0A17` | Every HUD panel fill | L220, L231, L603, L617 |
| `(1,0.83,0.45)` | `#FFD473` | Oil bar, keycaps, boss intro, stage banner | L225, L612, L786, L1407, L1748 |
| `(0.95,0.42,0.3)` | `#F26B4D` | Health bar fill | L223 |
| `(0.56,0.91,1)` | `#8FE8FF` | XP fill, level, stage-clear title | L1331, L1345, L633 |
| `(0.62,0.95,0.88)` | `#9EF2E0` | Extraction ring, toasts | L1435, L1057 |
| `(0.16,0.13,0.24,0.9)` | `#29213D` | `TextButton` fallback fill (matches ButtonBack) | L1687 |
| `(0.92,0.94,1)` | `#EBF0FF` | Default label ink | L1670 |
| `StageClearColor` | `#2BADD6` | Stage-clear banner/flash | L122 |

`[INFERENCE]` `HudView` duplicates LobbyView's palette numerically but shares no constants — the
two views drifted independently. `#29213D` appears in both, once named (`ButtonBack`) and once as
a raw literal (`HudView.cs` L1687, L1717, L764, L848).

### 1.3 Measured tone of the approved concept art

`[OBSERVED]` Measured by decoding the PNGs (downsampled via `sips`, PNG decoded in-process; no
Unity, no PIL). Luma is Rec.709 on 0-255; saturation is HSV S on 0-1.

| Asset | Dominant buckets | luma p05 / p50 / p95 | sat p50 |
|---|---|---|---|
| `docs/branding/cinder-court-link-preview.png` (1200×630) | `#181818` 19%, `#303030` 16%, `#000000` 16%, `#183030` 13% | 7 / **45** / 93 | **0.25** |
| `Assets/Art/Textures/cinder-court-backdrop.png` (1536×1024) | `#000000` 30%, `#000018` 21%, `#181818` 17%, `#001818` 17% | 8 / **22** / 41 | 0.50 |

`[OBSERVED]` Visual reading of `cinder-court-link-preview.png`: near-black charcoal ground, a
desaturated blue-grey fog gradient occupying the whole left half, ember light appearing **only** as
small emissive points (lantern, floor runes, a distant gate) and never as a large field, and a
single spectral-cyan figure. The bright accents cover a tiny fraction of frame area.

`[INFERENCE]` The concept tone is therefore: **very low key** (median luma 45/255 = 18%),
**low chroma overall** (median sat 0.25), with saturation reserved for **small emissive accents**
against a desaturated charcoal/blue-grey field. Ember and cyan are *light sources in darkness*,
not surface colours.

---

## 2. Complete UI surface inventory

Both view files are pure code-generated uGUI. Neither uses a prefab, a `.uxml`, or a scene-authored
canvas. `[OBSERVED]` `LobbyView.cs` L1-2 states this explicitly: "Pure code-generated uGUI over the
live 3D backdrop — no asset dependencies, factory style cloned from HudView."

`[OBSERVED]` No `RawImage`, no `Outline`, no `Shadow` component anywhere in `Assets/Scripts`
(grep returned no matches). Every UI pixel is either an `Image` or a legacy `Text`.

### 2.0 The four ways a surface gets its pixels

| Mechanism | What it produces | Where defined |
|---|---|---|
| **A. Flat tint** | Unity's built-in 1×1 white sprite multiplied by `Image.color` — a solid rounded-nothing rectangle | `HudView.Panel` L1617-1636; `LobbyView.Panel` L588-602 |
| **B. Procedural texture** | `MakeRadialTexture()` — the **only** generator in the codebase | `HudView.cs` L1592-1614 |
| **C. Authored sprite** | `Resources.Load<Sprite>("Icons/…")` | 6 call sites, listed in §3.2 |
| **D. Text glyphs** | `Resources.Load<Font>("Fonts/HudKorean")` OTF | `HudView.cs` L143; `LobbyView.cs` L108 |

**The complete procedural-texture inventory is one function.** `[OBSERVED]`
`MakeRadialTexture()` (`HudView.cs` L1592-1614) produces a **128×128 RGBA32** texture, no mipmaps.
For each pixel it computes normalised distance from centre `edge = clamp01(√(dx²+dy²))`, remaps with
`InverseLerp(0.45, 1, edge)` so the inner 45% radius is fully transparent, then applies
`SmoothStep` and scales by `0.85`. RGB is **constant white (255,255,255)**; only alpha varies —
0 at the centre ramping smoothly to 217/255 at the corners. It is a **white vignette mask**, tinted
at runtime by `Image.color`. It carries no texture, grain, or edge detail.

`[OBSERVED]` It is instantiated **once** (L171) and shared by three `Image`s via `Overlay()`
(L172-174): `Vignette`, `CastFlash`, `StageClearFlash`. There is no `MakeCircleTexture`,
`MakeGradientTexture`, or any other generator — grep for `Make\w+Texture` and `Texture2D(` returns
only this one.

### 2.1 HudView surfaces

Mount = `Assets/Scripts/View/HudView.cs`.

#### Full-screen overlays (built in `Build()`)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| 1 | Vignette | L172 | **B** radial 128px | `#F23821` @0.6·MotionScale on damage (L2022) |
| 2 | CastFlash | L173 | **B** radial 128px | per-skill: nova `#F25A2B`@0.28, ward `#2BADD6`@0.24, bolt `#9E6BF2`@0.20, altar `#DEC769`@0.26 (L2010-2016) |
| 3 | StageClearFlash | L174-179 | **B** radial 128px, `Filled`/`Radial360` | `#2BADD6` @0.38 (L699) |
| 4 | Letterbox top | L190 | **A** flat | `Color.black` (L1582) |
| 5 | Letterbox bottom | L191 | **A** flat | `Color.black` (L1582) |

#### Persistent HUD

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| 6 | Boss intro plate (text) | L192-198 | **D** text | `#FFD473`, bold, α animated |
| 7 | Speaker subtitle (text) | L203-208 | **D** text | `#EBE0CC`, α animated |
| 8 | Stage-clear banner (text) | L210-216 | **D** text | `#2BADD6`, bold |
| 9 | Meters panel | L219-220 | **A** flat | `#0D0A17` @0.55 |
| 10 | Health bar back | L222 → L1641 | **A** flat | black @0.55 |
| 11 | **Health bar fill** | L222-223 | **A** flat, `Filled/Horizontal` | `#F26B4D` |
| 12 | Oil bar back | L224 → L1641 | **A** flat | black @0.55 |
| 13 | **Oil bar fill** | L224-225 | **A** flat, `Filled/Horizontal` | `#FFD473`; flickers to `#F26B4D` below 20 charge (L2139-2146) |
| 14 | Stats panel | L230-231 | **A** flat | `#0D0A17` @0.55 |
| 15-18 | Wave / score / relic / enemy labels | L233-236 | **D** text | `#EBF0FF` |
| 19 | **Mute toggle button** | L239-241 | **C** `ui-button` 9-slice | `Color.white` over `#29213D` |
| 20-21 | **Arena skill cards Q, E** | L247-252 | **A** card + **C** icon | card `#1A142D9`; icons `skill-nova`, `skill-ward` @α0.55 |
| 22 | Lore line | L257-262 | **D** text | `#BFD1FF` @0.85 |
| 23 | Game-over panel | L265-269 | **A** flat, raycast **ON** (modal) | `#08050F` @0.92 |
| 24 | Game-over title | L270-271 | **D** text | `#FF8C66` |
| 25 | Final-score text | L272 | **D** text | `#EBF0FF` |
| 26 | **Game-over retry button** | L273-274 | **C** `ui-button` | white |
| 27 | Wave banner | L281-287 | **D** text | `#F25A2B`, boss variant `#FF4033` (L2002) |
| 28 | Level toast | L289-294 | **D** text | `#8FE8FF` |

#### Ember Rest modal (`BuildEmberRestPanel`, L773-812)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| 29 | Blocker | L775-778 | **A** flat, raycast ON | black @0.32 |
| 30 | Panel | L779-783 | **A** flat, raycast ON | `#050D0F` @0.96 |
| 31 | Title | L784-786 | **D** text | `#FFD473` |
| 32 | Room text | L787-789 | **D** text | `#8FE8FF` |
| 33-35 | **Offer cards ×3** | L791-799 | **C** `ui-button` | white; recoloured on select to `#478575`@0.96 / idle `#29213D`@0.9 (L846-848) — **note:** the recolour multiplies the plate sprite |
| 36 | **Defer button** | L801-803 | **C** `ui-button` | white |
| 37 | **Continue button** | L804-807 | **C** `ui-button` | white |
| 38 | Decision text | L808-810 | **D** text | `#EBF0FF` |

#### Campaign surfaces (`EnableCampaignUi`, L596-646)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| 39 | Stage banner panel | L602-603 | **A** flat | `#0D0A17` @0.62 |
| 40 | Stage banner text | L606-612 | **D** text | `#FFD473` |
| 41 | Equipment strip panel | L616-617 | **A** flat | `#0D0A17` @0.55 |
| 42 | Equipment text | L620 | **D** text | `#EBF0FF` |
| 43 | Stage-clear panel | L628-631 | **A** flat, raycast ON | `#050D0F` @0.94 |
| 44 | Stage-clear title | L632-633 | **D** text | `#8FE8FF` |
| 45 | Stage-clear score text | L634 | **D** text | `#EBF0FF` |
| 46 | **Stage-clear "캠페인으로"** | L635-636 | **C** `ui-button` | white |
| 47 | **Stage-clear retry** | L637-638 | **C** `ui-button` | white |
| 48 | **Game-over "캠페인으로"** | L643-644 | **C** `ui-button` | white |

#### Prologue toast + command console

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| 49 | Prologue toast panel | L1048-1050 | **A** flat | `#050D0F` @0.85 |
| 50 | Prologue toast text | L1051-1057 | **D** text | `#9EF2E0` |
| 51 | Console root | L1086-1089 | **A** flat, raycast ON | `#080A17` @0.92 |
| 52 | Console placeholder | L1114 | **D** text | `#A6ADC7` @0.55 |
| 53 | Console toast | L1126-1127 | **D** text | `#9EF2E0` |

#### Dungeon HUD (`EnableDungeonUi`, L1307-1450)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| 54 | XP bar back | L1324-1325 | **A** flat | black @0.6 |
| 55 | **XP fill** | L1328-1334 | **A** flat, `Filled/Horizontal` | `#8FE8FF`, flashes to `#DEC769` (L2185-2186) |
| 56 | Level text | L1340-1345 | **D** text | `#8FE8FF` |
| 57-59 | **Combo pips ×3** | L1349-1357 | **A** flat | idle white@0.14, lit `#FFD473`@0.95, finisher `#FFF59E` (L1487-1488, L2299-2302) |
| 60 | **Dash card (SHIFT)** | L1362-1366 | **A** card + **C** `skill-dash` | card `#1A142D9` |
| 61-64 | **Dungeon skill cards Q/E/R/F** | L1376-1382 | **A** card + **C** icons | `skill-bolt`, `skill-pulse`, `skill-nova`, `skill-aegis` @α0.55 |
| 65 | **Companion hold button (G)** | L1384-1386 | **C** `ui-button` | white |
| 66 | **Companion recall button (H)** | L1387-1389 | **C** `ui-button` | white |
| 67 | Shield readout | L1392-1397 | **D** text | `#8FD9FF` |
| 68 | Boss bar panel | L1400-1401 | **A** flat | `#0D0508` @0.8 |
| 69 | Boss name | L1404-1405 | **D** text | `#FF8C66` |
| 70 | Boss phase pip | L1406-1407 | **D** text | `#FFD473` |
| 71 | Boss bar back | L1408-1409 | **A** flat | black @0.65 |
| 72 | **Boss HP fill** | L1411-1417 | **A** flat, `Filled/Horizontal` | P1 `#FF8C42`, P2 `#F24D52`, P3 `#FF3D8C` (L1537-1541) |
| 73 | Extraction root | L1426-1427 | **A** flat | `#050D0F` @0.8 |
| 74 | Extraction label | L1428 | **D** text | `#EBF0FF` |
| 75 | Extraction back | L1429-1430 | **A** flat | black @0.6 |
| 76 | **Extraction fill** | L1432-1438 | **A** flat, `Filled/Horizontal` | `#9EF2E0` |

#### Touch controls (`BuildTouchControls`, L1796-1857) — mobile / touch-only devices

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| 77 | Joystick catch panel | L1813-1816 | **A** flat, fully transparent, raycast ON | `(0,0,0,0)` |
| 78 | **Joystick base** | L1819-1820 → L1861 | **C** `ui-joystick-base` | white @0.4 |
| 79 | **Joystick nub** | L1821-1822 → L1861 | **C** `ui-joystick-nub` | white @0.75 |
| 80 | **Strike button** | L1830-1832 | **A flat tint — NO sprite** | `#CC6640` @0.5 |
| 81 | Strike label | L1837 | **D** text | `#EBF0FF` |
| 82 | **Dash touch button** | L1842-1845 | **A flat tint — NO sprite** | `#2BADD6` @0.4 |
| 83 | Dash label | L1850 | **D** text | `#EBF0FF` |

`[OBSERVED]` Cooldown overlays: every `SkillCard` adds a full-stretch `Image` at
`(0,0,0,0.65)`, `Filled/Vertical` from top (L1750-1757). Seven instances.

### 2.2 LobbyView surfaces

Mount = `Assets/Scripts/View/LobbyView.cs`.

#### Top bar (`BuildTopBar`, L325-356)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| L1 | Top bar panel | L327-332 | **A** flat | `PanelColor` `#050409`@0.72 |
| L2 | Underline | L333 → L696 | **A** flat 1px line | `BorderColor` `#80D8FF`@0.34 |
| L3 | Title | L335-337 | **D** text | `Gold #DDC869` |
| L4 | Kicker | L338-340 | **D** text | `InkDim` |
| L5 | Relic counter | L342-344 | **D** text | `Gold` |
| L6 | Point counter | L345-347 | **D** text | `Cyan` |
| L7 | Version badge panel | L349-352 | **A** flat | `Ember` @0.22 |
| L8 | Version badge text | L353-355 | **D** text | `Ember` |

#### Sortie panel (`BuildSortiePanel`, L359-429)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| L9 | Panel | L361-362 | **A** flat | `PanelColor` |
| L10-13 | Panel border ×4 | L365 → L694-731 | **A** flat 1px lines | `BorderColor` |
| L14 | SORTIE eyebrow (2 texts) | L367 → L684 | **D** text | kicker `Cyan`@0.8, title `#EBF0FF` |
| L15 | Prologue card | L370 → L671 | **A** flat | white @0.03 |
| L16-19 | Prologue card border ×4 | L679 | **A** flat lines | `BorderColor`; **pulses `Ember` α0.35→0.9** until cleared (L238-246) |
| L20 | Prologue eyebrow | L379 | **D** text | — |
| L21 | Prologue sub | L380-382 | **D** text | `InkDim` |
| L22 | Prologue status | L383-384 | **D** text | `Gold` if done else `Ember` (L150) |
| L23 | **Prologue sortie button** | L385-387 | **C** `ui-button` | white |
| L24-29 | Stage cards ×6 | L395 → L671 | **A** flat | white @0.03 |
| L30-53 | Stage card borders ×24 | L679 | **A** flat lines | `BorderColor` |
| L54-59 | Stage eyebrows ×6 | L396 | **D** text | — |
| L60-65 | **Stage hazard glyphs ×6** | L397-412 | **C** `Icons/{HazardIcon}` 24×24u | untinted white |
| L66-71 | Stage reward subs ×6 | L416-418 | **D** text | `Gold` |
| L72-77 | Stage status ×6 | L419-420 | **D** text | `Gold`/`Cyan`/`Lock` (L161) |
| L78-83 | **Stage sortie buttons ×6 (강하, 84×28u)** | L422-424 | **C** `ui-button` | white |

`[OBSERVED]` `HazardIcon` values, `StageCatalog.cs` L102-136: `skill-nova` (cinder-span,
ember-gallery), `skill-aegis` (abyss-chancel, witness-well), `skill-pulse` (echo-throne,
ash-verdict). Six card slots, **three distinct glyphs**, each reused twice.

#### Sanctum panel (`BuildSanctumPanel`, L432-457) and tabs

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| L84 | Panel | L434-435 | **A** flat | `PanelColor` |
| L85-88 | Panel border ×4 | L437 | **A** flat lines | `BorderColor` |
| L89 | SANCTUM eyebrow | L439 | **D** text | — |
| L90-92 | **Tab buttons ×3** | L448-450 | **A flat — `plated: false`** | `ButtonActive` selected / `ButtonBack` idle (L582) |

**Growth tab** (`BuildGrowthTab`, L471-512)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| L93 | Points-left text | L474-475 | **D** text | `Cyan` |
| L94-96 | Stat rows ×3 | L479-480 | **A** flat | white @0.04 |
| L97-99 | **Stat row icons ×3** | L481 → L605 | **C** `stat-attack`/`stat-vitality`/`stat-swiftness` 36×36u | untinted |
| L100-105 | Stat name + effect ×3 | L482-484 | **D** text | `#EBF0FF` / `InkDim` |
| L106-108 | Stat values ×3 | L485-486 | **D** text | `Cyan` |
| L109-111 | **Stat "+" buttons ×3 (52×44u)** | L489-491 | **C** `ui-button` | white |
| L112 | Hint | L498-500 | **D** text | `InkDim` |
| L113 | **Motion toggle button** | L502-508 | **C** `ui-button` | white |

**Equip tab** (`BuildEquipTab`, L514-546)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| L114 | Hint | L517-518 | **D** text | `InkDim` |
| L115-117 | Equip rows ×3 | L522-523 | **A** flat | white @0.04 |
| L118-120 | **Equip row icons ×3** | L524 → L605 | **C** `equip-weapon`/`equip-lantern`/`equip-cloak` | untinted |
| L121-126 | Equip name + effect ×3 | L525-527 | **D** text | `#EBF0FF` / `InkDim` |
| L127-129 | Equip values ×3 | L528-529 | **D** text | `Cyan` |
| L130-132 | **Equip buy buttons ×3** | L532-534 | **C** `ui-button` | white |
| L133 | Cost line | L542-544 | **D** text | `InkDim` |

**Legion tab** (`BuildLegionTab`, L548-575)

| # | Surface | Line | Draw mechanism | Colour |
|---|---|---|---|---|
| L134 | Hint | L551-552 | **D** text | `InkDim` |
| L135-140 | **Roster buttons ×6** | L562-565 | **A flat — `plated: false`** | `ButtonActive`/`ButtonBack` (L194, L201) |
| L141 | Note | L571-573 | **D** text | `InkDim` |

`[OBSERVED]` `LobbyView.Panel` (L588-602) does **not** set `raycastTarget = false`, unlike
`HudView.Panel` (L1628). Every lobby panel, card, border line and row is therefore a raycast
target. `[INFERENCE]` This is a divergence from the stated HUD discipline, not a deliberate
lobby-specific choice — the lobby has no joystick to protect, so it has never mattered visually.

### 2.3 Interactive-surface totals

`[OBSERVED]` Counting runtime instances (loops expanded):

| | HudView | LobbyView | Total |
|---|---|---|---|
| Interactive surfaces | 22 | 23 | **45** |
| …backed by `ui-button.png` 9-slice | 12 | 14 | **26** |
| …flat tint, no sprite | 3 (strike, dash-touch, joystick catch) | 9 (3 tabs + 6 roster) | **12** |
| …`SkillCard` (flat card + separate icon child) | 7 | 0 | **7** |

---

## 3. Does any image asset back the UI? — definitive count

### 3.1 What exists on disk

`[OBSERVED]`

- `Assets/Textures/` — **does not exist**.
- `Assets/Art/Icons/` — **does not exist**.
- `Assets/Art/Textures/` — contains exactly **one** PNG, `cinder-court-backdrop.png` (1536×1024,
  2.76 MB). It is bound to `Assets/Art/Materials/CourtBackdrop.mat` and is **not referenced from
  any C# file** (grep for `cinder-court-backdrop` / `CourtBackdrop` across `Assets/Scripts`
  returns nothing) — it is a **world/scene** backdrop, not a UI surface.
- `Assets/Resources/Icons/` — **20 PNGs, 1.3 MB total**. This is the entire UI image library.
- `_workspace/current/engineering/icons/raw/` — **20 source PNGs, 28 MB** (~1.0-2.0 MB each,
  outside `Assets/`, not shipped) plus `contact-sheet.png` (236 KB).

`[OBSERVED]` `Assets/Resources/Icons/` manifest — every file is 256×256 except the button plate:

| File | px | bytes | | File | px | bytes |
|---|---|---|---|---|---|---|
| `app-lantern.png` | 256×256 | 64,371 | | `skill-nova.png` | 256×256 | 93,743 |
| `equip-cloak.png` | 256×256 | 56,909 | | `skill-pulse.png` | 256×256 | 89,588 |
| `equip-lantern.png` | 256×256 | 56,852 | | `skill-strike.png` | 256×256 | 76,352 |
| `equip-weapon.png` | 256×256 | 44,505 | | `skill-ward.png` | 256×256 | 80,743 |
| `pickup-ember.png` | 256×256 | 48,161 | | `stat-attack.png` | 256×256 | 61,318 |
| `pickup-flask.png` | 256×256 | 50,453 | | `stat-swiftness.png` | 256×256 | 65,785 |
| `pickup-relic.png` | 256×256 | 56,881 | | `stat-vitality.png` | 256×256 | 78,013 |
| `skill-aegis.png` | 256×256 | 69,609 | | **`ui-button.png`** | **256×106** | 23,420 |
| `skill-bolt.png` | 256×256 | 43,252 | | `ui-joystick-base.png` | 256×256 | 75,547 |
| `skill-dash.png` | 256×256 | 43,448 | | `ui-joystick-nub.png` | 256×256 | 87,606 |

`[OBSERVED]` Import contract, `Assets/Editor/IconImportPipeline.cs` L24-41: everything under
`Assets/Resources/Icons/` is forced to `Sprite`/`Single`, `alphaIsTransparency = true`,
`mipmapEnabled = false`, `filterMode = Bilinear`, `maxTextureSize = 256`,
`textureCompression = Uncompressed`. `ui-button.png` additionally gets
`spriteBorder = (30, 14, 30, 14)` (L39). `ImportAll()` hard-fails the lane if zero icons are found
(L62-67).

### 3.2 Every sprite-load call site

`[OBSERVED]` Exactly **six** `Resources.Load<Sprite>` call sites exist in the whole repo:

| Call site | Loads | Mounts on |
|---|---|---|
| `HudView.cs` L1691 | `Icons/ui-button` | Every `TextButton` (12 HUD instances) |
| `HudView.cs` L1728 | `Icons/{iconId}` | `SkillCard` icon child (7 instances) |
| `HudView.cs` L1867 | `Icons/{iconId}` | `JoystickSprite` base + nub (2 instances) |
| `LobbyView.cs` L397 | `Icons/{entry.HazardIcon}` | Stage card glyph (6 instances) |
| `LobbyView.cs` L607 | `Icons/{iconId}` | `RowIcon` stat/equip rows (6 instances) |
| `LobbyView.cs` L655 | `Icons/ui-button` | Every plated `TextButton` (14 instances) |
| *(non-UI)* `VfxDirector.cs` L1027 | `Icons/{PickupIcons[kind]}` | **World-space** pickup quads |

`[OBSERVED]` `VfxDirector.cs` L934-935: `PickupIcons = { "pickup-ember", "pickup-flask",
"pickup-relic", "equip-weapon" }`. These render in the 3D world, not on the UI canvas.

### 3.3 The definitive count

`[OBSERVED]` Distinct authored images mounted on a **UI canvas**: **15 of 20**.

`ui-button`, `skill-nova`, `skill-ward`, `skill-dash`, `skill-bolt`, `skill-pulse`, `skill-aegis`,
`stat-attack`, `stat-vitality`, `stat-swiftness`, `equip-weapon`, `equip-lantern`, `equip-cloak`,
`ui-joystick-base`, `ui-joystick-nub`.

`[OBSERVED]` Mounted **world-space only** (not UI): `pickup-ember`, `pickup-flask`,
`pickup-relic` (3).

`[OBSERVED]` **Orphaned — zero references anywhere in `Assets/`, `build-webgl/`, or `docs/`:**

- **`app-lantern.png`** — the only other hit repo-wide is an unrelated copy at
  `tools/video/brand/public/app-lantern.png`. It ships in the WebGL data file for nothing.
- **`skill-strike.png`** — see §4 gap G2; the strike button uses a text label instead.

`[OBSERVED]` **Runtime `Image` instances on the UI canvas, by pixel source:**

| Source | Instances | Share |
|---|---|---|
| **C — authored sprite** | **47** | **~30%** |
| **B — procedural (`MakeRadialTexture`)** | **3** | ~2% |
| **A — flat tint (Unity 1×1 white)** | **~107** | ~68% |
| **Total** | **~157** | |

Breakdown of the 47 authored-sprite instances: 26 × `ui-button` plate, 7 × skill-card icons,
6 × lobby row icons, 6 × stage hazard glyphs, 2 × joystick parts.

> **Definitive answer:** yes, image assets back the UI today — **15 distinct PNGs across 47 `Image`
> instances, roughly 30% of all UI images**. The remaining ~68% are flat colour rectangles and ~2%
> are one white radial gradient. Critically, **one file (`ui-button.png`) carries 26 of the 47
> instances**, so the *entire* button language of the game is a single 256×106 sprite.

`[OBSERVED]` **Budget headroom.** Current WebGL build is **37 MB** total
(`build-webgl.data.unityweb` 28.1 MB + `.wasm` 9.5 MB + framework/loader 124 KB) against a 120 MB
cap — **83 MB free**. All 20 icons together are 1.3 MB on disk. Uncompressed 256² RGBA32 is 256 KB
of VRAM each; the current set costs ~5.0 MB VRAM. `[INFERENCE]` Asset budget is a non-constraint
here; the binding limits are the ≤1024 px per-texture rule and VRAM, not build size.

---

## 4. Gaps between current UI and concept tone — prioritised

### G1 — The button plate is a neon coral outline, and it is 26 of 45 buttons `[OBSERVED]`

Sampled pixels of `Assets/Resources/Icons/ui-button.png`:

| Sample point | Value |
|---|---|
| Centre fill | `#151037` (indigo-violet) |
| Bottom border | `#FF5105` |
| Left border | `#A8361B` |
| Right border | `#802D20` |
| Brightest opaque pixel | `#FF7273` (coral pink) |

The plate is a **hard 1-2 px neon outline** — a saturated coral-to-orange rim around a flat
indigo-violet field, with a soft outer glow. It reads as sci-fi HUD / arcade, not as a physical
object.

Against the concept: the key art's dominant buckets are `#181818`, `#303030`, `#183030` — charcoal
and desaturated blue-grey. `#151037` (indigo-violet) **does not appear in the concept palette at
all**, and `#FF7273` coral-pink is nowhere in a dark-fantasy ember ramp. The declared `Ember` token
is `#F3592C`; the plate's brightest pixel is both **brighter and pinker** than the token it is
supposed to express.

**Should be:** a pitted dark-stone or blackened-iron plate — charcoal `#181818`-`#303030` body,
soft ember rim-light only on the top/lit edge, visible material grain, no uniform glowing outline.
Ember should look like *light falling on metal*, not *a stroke applied in a vector editor*.

**Why it breaks:** this is the single highest-leverage surface in the project. 26 of 45 interactive
surfaces are this one file. Every menu press, every modal, every lobby action is stamped with it.
Fixing this one asset moves more of the screen than the next five items combined.

### G2 — The most-pressed button in the game has no art at all `[OBSERVED]`

`HudView.cs` L1830-1837: the mobile **strike** button is
`Panel(..., new Color(0.8f, 0.4f, 0.25f, 0.5f))` — a **flat 50%-alpha orange rectangle** with the
text "타격" centred on it. No sprite, no border, no shape. Directly below, L1842-1850: the **dash**
button is `new Color(0.17f, 0.68f, 0.84f, 0.4f)` — a **flat 40%-alpha cyan rectangle** labelled
"질주".

Meanwhile `Assets/Resources/Icons/skill-strike.png` (76 KB, 256×256) exists and is
**referenced by nothing** (§3.3).

**Should be:** a circular ember-iron action button with a struck-blade glyph; dash a matching
spectral-cyan disc. Both should be round — the joystick already is (`ui-joystick-base.png`), so a
square strike button beside a round joystick is internally inconsistent.

**Why it breaks:** on touch this is the button the player presses more than all others combined,
and it is a translucent orange square. It is also the most visible surface in any mobile
screenshot. `[INFERENCE]` The unused `skill-strike.png` suggests this was intended and never wired.

### G3 — The 9-slice borders mathematically cannot fit two button sizes `[OBSERVED]`

`ui-button.png` is 256×106 with `spriteBorder = (30, 14, 30, 14)` (`IconImportPipeline.cs` L39).
The importer sets no `spritePixelsPerUnit`, so Unity's default 100 applies and border pixels map
1:1 to canvas units. Under `Image.Type.Sliced` the centre region must be ≥ 0 in both axes:

| Button | Size (u) | Centre region after borders | Verdict |
|---|---|---|---|
| Lobby stat **"+"** (`LobbyView.cs` L489-490) | 52 × 44 | **−8** × 16 | **NEGATIVE — left and right borders overlap** |
| Lobby stage **"강하"** ×6 (`LobbyView.cs` L422-423) | 84 × 28 | 24 × **0** | **Degenerate — zero vertical centre** |
| Mute toggle (`HudView.cs` L239-240) | 240 × 34 | 180 × 6 | Marginal |
| Game-over retry (`HudView.cs` L273-274) | 200 × 44 | 140 × 16 | OK |
| Companion hold/recall (`HudView.cs` L1384-1389) | 154 × 92 | 94 × 64 | OK |

The comment at `IconImportPipeline.cs` L36-38 says "Buttons are 34-48px tall, so the vertical
border must stay small (14+14 < 34)" — but the six 강하 buttons are **28 u** tall, below the range
the border was designed for, and the "+" button is **52 u** wide against 60 u of horizontal border.

**Should be:** a plate whose border inset survives the smallest real button. `[INFERENCE]` Either
a smaller border (≈12/8) on a plate drawn for it, or a dedicated small-button variant, or resizing
the 강하 buttons to ≥ 44 u tall — which is independently desirable, since 28 u is under the touch
floor the HUD spec enforces elsewhere (`HudView.cs` L1806-1812 documents a 44-CSS-px minimum).

**Why it breaks:** 7 of 26 plated buttons (27%) render a crushed or distorted plate. This is a
correctness defect, not only a taste one.

### G4 — Icons are 3× the concept's saturation and read as mobile-gacha `[OBSERVED]`

Measured across all 20 icons (opaque pixels only, alpha > 32):

| | luma p50 | luma p95 | sat p50 |
|---|---|---|---|
| **Icon set mean** | 47 | **135** | **0.75** |
| Concept key art | 45 | 93 | **0.25** |
| Arena backdrop | 22 | 41 | 0.50 |

Worst offenders by saturation: `skill-ward` 0.96, `pickup-ember` 0.95, `skill-pulse` 0.95,
`stat-vitality` 0.95, `skill-nova` 0.91. Worst by highlight blow-out: `skill-strike` p95 = 253,
`skill-pulse` 219, `skill-bolt` 214, `pickup-relic` 234.

`[OBSERVED]` Visual reading of `_workspace/current/engineering/icons/contact-sheet.png`: glossy
bevelled emblems with thick black outlines, chrome specular highlights, uniform outer glow, and a
faceted ruby heart — the idiom of a free-to-play mobile RPG inventory, rendered in pure
orange/cyan complementary pairs.

**Should be:** desaturated to roughly sat 0.35-0.45 with highlights capped near luma 180; painted
as aged metal, bone, and ash with ember or cyan appearing only as small emissive accents, matching
the concept's "small bright points in a desaturated field" structure.

**Why it breaks:** the icons are the only detailed art on the HUD, so they set the perceived genre.
At 3× the concept's chroma they read as a different game from the backdrop they sit on. They also
undercut the "reduced-motion-safe static indicator" contrast rule (spec L71) by being uniformly
loud — nothing can stand out when everything glows.

### G5 — Every panel, card and modal is an untextured rectangle `[OBSERVED]`

All 5 HUD panels, 3 modals, 9 lobby panels/cards and 6 rows are `Image` with the default white
1×1 sprite and a colour multiply (§2.0 mechanism A). There is no frame, no corner, no material.
The only structure is `LobbyView.Border()` (L694-731), which draws **1-pixel `sizeDelta` lines** at
`#80D8FF` @ 34%.

**Should be:** stone-and-iron framing — a 9-slice panel frame with a dark carved-stone field, a
subtle inner shadow, and corner detail; ember or cyan appearing only as inlay, not as a uniform
1 px stroke.

**Why it breaks:** the concept art's entire language is *material* — carved stone, hanging chain,
weathered iron. Flat rectangles with hairline strokes are the visual opposite. `[INFERENCE]` This
is more surface area than G1 but lower priority per unit of work, because panels sit behind content
and a single 9-slice frame fixes all of them at once.

### G6 — Two stateful button groups deliberately cannot take the plate `[OBSERVED]`

`LobbyView.cs` L652-655 comments: "Stateful groups (tabs, roster) keep the flat fill because
Refresh/SelectTab drive `Image.color` as the state signal — a sprite would multiply-tint." So the
3 sanctum tabs (L448-450) and 6 roster slots (L562-565) pass `plated: false` and render as flat
`#29213D` / `#524729` rectangles.

The same conflict exists un-commented in `HudView`: the 3 Ember Rest offer cards **do** take the
plate (L794-796) and are **then** recoloured to `#478575`@0.96 / `#29213D`@0.9 on selection
(L846-848) — so the plate sprite is multiply-tinted teal, exactly the failure the lobby comment
avoids.

**Should be:** separate idle / hover / selected / disabled plate variants, selected by swapping
`Image.sprite` instead of multiplying `Image.color`. That unblocks 9 flat lobby buttons and fixes
the 3 Ember Rest cards.

**Why it breaks:** 12 buttons are excluded from the button language by an art limitation. It also
means "selected" reads as *a different colour* rather than *a different material state*.

### G7 — Selection, hover and disabled states are alpha and tint only `[OBSERVED]`

Disabled state is `CanvasGroup.alpha = 0.45` (`LobbyView.cs` L163, L174, L188). Locked stages use
text colour `Lock #6B7394` (L161). No `Button.spriteState` is configured anywhere — grep finds no
`SpriteState` in either file, so buttons use uGUI's default `ColorTint` transition.

**Should be:** ember-glow-on-press plate variants and a visibly cold/unlit disabled plate.

**Why it breaks:** in a dark-fantasy UI where everything is already dim, a 45% alpha "disabled"
state is nearly indistinguishable from an enabled one on a dark backdrop.

### G8 — Two shipped icons are dead weight `[OBSERVED]`

`app-lantern.png` (64 KB) and `skill-strike.png` (76 KB) are referenced by nothing and are inside
`Assets/Resources/`, which forces them into the WebGL data file unconditionally.

**Why it breaks:** minor (140 KB), but `skill-strike` being orphaned is the direct evidence for G2.

### G9 — Vignette, cast-flash and stage-clear flash share one white radial `[OBSERVED]`

All three full-screen overlays reuse the same 128 px smooth-step gradient (`HudView.cs` L171-174),
distinguished only by tint. A damage vignette, a spell cast and a victory bloom all have identical
falloff geometry.

**Should be:** the damage vignette in particular should carry ash/ember grain at the edge rather
than a mathematically clean falloff.

**Why it breaks:** lowest priority. `[INFERENCE]` It is nearly free to fix later (one texture
swap, no code shape change) and is only visible for a fraction of a second at a time.

---

## 5. Generation plan

Ordered by **visual impact per unit of work**. Every entry names the exact mount point.

**Global constraints honoured throughout:**
- Max dimension **1024 px** — the largest proposal here is 512 px; the importer clamps to 256
  anyway (`IconImportPipeline.cs` L31), so any asset intended to ship above 256 px **requires that
  clamp to be raised for its path**. Flagged per item below.
- Total build ≤ 120 MB — current 37 MB, headroom 83 MB. Full plan adds **≈1.5 MB** on disk.
- `raycastTarget = false` discipline — **every asset below is decorative**. All mount into `Image`
  components that either already set `raycastTarget = false`
  (`HudView.Panel` L1628, icon children L1736, joystick L1877) or are the button's own hit surface
  where `raycastTarget = true` is already correct and unchanged (L1688, L1718). **No new raycast
  target is introduced by any item in this plan.**

### Tier 1 — highest impact

| # | Asset | Size | Depicts | Mount point |
|---|---|---|---|---|
| **1** | `ui-button.png` **(replace)** | 256×96 | Blackened-iron plate, pitted charcoal `#1C1A20` field, warm ember rim-light on the top edge only, faint ash grain, **no** uniform outer glow. Ember accent ≤ `#F3592C`, never brighter. | `HudView.cs` L1691 and `LobbyView.cs` L655. **26 instances.** Redraw with border ≈ `(12, 8, 12, 8)` and update `IconImportPipeline.cs` L39 to match — this fixes G3's negative and zero centre regions (52 u wide clears 24 u of border; 28 u tall clears 16 u). |
| **2** | `ui-button-active.png` | 256×96 | Same plate, ember inlay lit, warmer body. | New sprite swap for selected state. Unblocks the 3 tabs (`LobbyView.cs` L448-450) and 6 roster slots (L562-565) currently forced to `plated: false` (G6), and replaces the multiply-tint on the 3 Ember Rest cards (`HudView.cs` L846-848). |
| **3** | `ui-button-disabled.png` | 256×96 | Cold, unlit, ember extinguished, desaturated toward `#14141A`. | Same swap path; replaces `CanvasGroup.alpha = 0.45` as the primary disabled signal (`LobbyView.cs` L163, L174, L188). Fixes G7. |
| **4** | `ui-strike.png` | 256×256 | Round ember-iron action disc, struck-blade glyph, heavier bottom weight. | `HudView.cs` L1830-1832 — replaces the flat `#CC6640`@0.5 rectangle. Fixes G2, the most-pressed button in the game. |
| **5** | `ui-dash.png` | 256×256 | Matching round disc, spectral-cyan chevron, lighter body. | `HudView.cs` L1842-1845 — replaces the flat `#2BADD6`@0.4 rectangle. |

`[INFERENCE]` Items 1-5 are five files and change the appearance of 28 of 45 interactive surfaces.
Item 1 alone touches 26.

### Tier 2 — panel and card material

| # | Asset | Size | Depicts | Mount point |
|---|---|---|---|---|
| **6** | `ui-panel.png` | 256×256, 9-slice ≈ `(24,24,24,24)` | Carved dark-stone panel, inner shadow, subtle corner bracket, cyan hairline inlay at the rim (replacing the current 1 px `#80D8FF` line). | `HudView.Panel` L1617-1636 and `LobbyView.Panel` L588-602 — add an optional sprite parameter. Applies to HUD meters (L219), stats (L230), stage banner (L602), equip strip (L616), boss bar (L1400), lobby top bar (L327), sortie (L361), sanctum (L434). Fixes G5. |
| **7** | `ui-modal.png` | 512×512, 9-slice ≈ `(48,48,48,48)` | Heavier tribunal-stone frame with iron corner studs, for full-attention surfaces. **Needs `maxTextureSize` raised to 512 for this path** (`IconImportPipeline.cs` L31). | Game-over panel `HudView.cs` L265, stage-clear `L628`, Ember Rest `L779`. |
| **8** | `ui-card.png` | 256×192, 9-slice ≈ `(16,16,16,16)` | Lighter inset stone slab for list items. | `LobbyView.Card` L671-681 (7 instances: prologue + 6 stages) and `HudView.SkillCard` card fill L1716-1717 (7 instances). Retires 28 border-line `Image`s in the lobby. |
| **9** | `ui-bar-frame.png` | 256×32, 9-slice ≈ `(12,8,12,8)` | Iron gutter with end caps, for meters. | `HudView.Bar` back panel L1641-1642 (health, oil), XP back L1324, boss back L1408, extraction back L1429. |

### Tier 3 — icon retone

| # | Asset | Size | Depicts | Mount point |
|---|---|---|---|---|
| **10** | `skill-nova`, `skill-ward`, `skill-dash`, `skill-bolt`, `skill-pulse`, `skill-aegis` **(regenerate ×6)** | 256×256 | Same silhouettes, retoned to sat ≈ 0.40 and highlight luma ≤ 180. Aged metal / bone / ash bodies; ember and cyan only as small emissive accents. | `HudView.cs` L1728 (`SkillCard` icons, 7 instances) and `LobbyView.cs` L397 (stage hazard glyphs, 6 instances — `skill-nova`/`skill-aegis`/`skill-pulse` per `StageCatalog.cs` L102-136). Fixes G4. |
| **11** | `stat-attack`, `stat-vitality`, `stat-swiftness` **(regenerate ×3)** | 256×256 | Same retone. Drop the faceted-ruby heart on `stat-vitality` (sat 0.95) — it is the furthest from tone in the set. | `LobbyView.cs` L481 → L605 (`RowIcon`). |
| **12** | `equip-weapon`, `equip-lantern`, `equip-cloak` **(regenerate ×3)** | 256×256 | Same retone; these should read closest to the Lantern Reaver's actual gear. | `LobbyView.cs` L524 → L605, plus `equip-weapon` doubles as the world pickup for `EquipShard` (`VfxDirector.cs` L935). |
| **13** | `pickup-ember`, `pickup-flask`, `pickup-relic` **(regenerate ×3)** | 256×256 | Retone, but keep these the **brightest** things in the set — they are world-space and must read at gameplay camera distance. Target sat ≈ 0.55. | `VfxDirector.cs` L1027. Not a UI surface; listed for palette coherence. Note L1029-1030 states they are deliberately untinted. |
| **14** | `ui-joystick-base`, `ui-joystick-nub` **(regenerate ×2)** | 256×256 | Current base is navy `#1E2440` with orange tick marks — closer to tone than most, but reads as clean tech. Retone to worn stone ring with faint ember cardinal marks. | `HudView.cs` L1819-1822 → L1861-1884. |

### Tier 4 — polish

| # | Asset | Size | Depicts | Mount point |
|---|---|---|---|---|
| **15** | `ui-vignette-ash.png` | 512×512 | Ash-grained radial falloff replacing the mathematically clean `SmoothStep`. **Needs the 512 clamp exception**, or ship at 256. | Replaces `MakeRadialTexture()` (`HudView.cs` L1592-1614) for the `Vignette` overlay (L172) only; `CastFlash` and `StageClearFlash` (L173-174) keep the clean procedural gradient. Fixes G9. |
| **16** | `ui-cooldown-sweep.png` | 128×128 | Faint radial tick ring behind the cooldown wipe. | `HudView.cs` L1750-1757 (`SkillCard` cooldown overlay, 7 instances). |

### Deletions

`[OBSERVED]` `app-lantern.png` (64 KB) and `skill-strike.png` (76 KB) are unreferenced (§3.3).
`skill-strike.png` should be **superseded by** `ui-strike.png` (item 4) rather than deleted
blindly — it is the intended source for that glyph. `app-lantern.png` is a web brand asset that
duplicates `tools/video/brand/public/app-lantern.png` and can leave `Assets/Resources/`.

### Budget check `[OBSERVED]` / `[INFERENCE]`

| | Count | On-disk estimate | VRAM (uncompressed RGBA32) |
|---|---|---|---|
| New (items 1-9, 15-16) | 11 files | ≈ 0.7 MB | ≈ 3.1 MB |
| Regenerated (items 10-14) | 17 files | ≈ 1.1 MB (replaces 1.1 MB) | unchanged, ≈ 4.3 MB |
| Removed | 1-2 files | −0.14 MB | −0.5 MB |
| **Net build delta** | | **≈ +0.6 MB** | **≈ +2.6 MB** |

Against 83 MB of headroom this is negligible. `[INFERENCE]` The real constraint is the
`maxTextureSize = 256` clamp at `IconImportPipeline.cs` L31: items 7 and 15 are specified at 512 px
and will be silently downsampled unless that path is given an exception. Everything else fits the
existing clamp unchanged.

### Sequencing note

`[INFERENCE]` Items 1-3 must land together. Item 1 changes `spriteBorder`, which requires the
matching edit at `IconImportPipeline.cs` L39; shipping the new plate against the old
`(30,14,30,14)` border would preserve the G3 defect. Items 2-3 are what make the border change
worth doing, because they retire the multiply-tint pattern that currently forces 12 buttons out of
the plate language entirely.

---

## Verification

- `[OBSERVED]` Palette hex values read directly from `LobbyView.cs` L32-40 and `HudView.cs`
  colour literals (`new Color(` occurs 79 times in `HudView.cs` and 8 in `LobbyView.cs`, 87
  combined; all HUD values in §1.2 transcribed from those lines).
- `[OBSERVED]` Concept-art and icon tone measured by decoding the PNGs in-process — downsampled
  with `sips -Z`, PNG chunks parsed and unfiltered manually (no PIL available in the kernel).
  Luma is Rec.709, saturation is HSV S, computed over alpha > 32 pixels only.
- `[OBSERVED]` Surface inventory built by reading `HudView.cs` L129-316, L579-666, L739-833,
  L1044-1130, L1289-1450, L1552-1903 and `LobbyView.cs` L1-750 in full, cross-checked against a
  mechanical count of `Panel(`/`Label(`/`TextButton(`/`Bar(`/`SkillCard(`/`Border(` call sites.
- `[OBSERVED]` Sprite-load call sites found by grepping `Resources.Load` across `Assets/Scripts`,
  `Assets/Editor`, and `build-webgl`; six UI sites plus one world-space site, all listed in §3.2.
- `[OBSERVED]` Orphan status of `app-lantern` and `skill-strike` confirmed by a repo-wide grep
  across `*.cs`, `*.html`, `*.json`, `*.md`, `*.js` excluding `Library/`; the only hits are a
  graphify manifest index and an unrelated `tools/video/brand/public/` copy.
- `[OBSERVED]` 9-slice arithmetic in G3 computed from `spriteBorder = (30,14,30,14)`
  (`IconImportPipeline.cs` L39) at the default 100 pixels-per-unit against button sizes read from
  their construction lines.
- `[OBSERVED]` Build size from `du` on `build-webgl/` — 37 MB total.
- **Not verified:** nothing was rendered. All appearance claims are from decoding the source PNGs
  and reading construction code, not from a running Unity player. The user's Editor holds the
  project lock, so no batchmode run was attempted, per the brief.
- No code edited, no images generated, no formatter/linter/test run.
