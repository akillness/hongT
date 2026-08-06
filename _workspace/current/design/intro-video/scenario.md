# Intro / Brand Loading Video — Scenario (webtoon-harness method)

Game: **Abyssal Lantern — Hold the Cinder Court** (Unity 6000.5.6f1 / URP / WebGL)
Team: Hong팀
Purpose: Replace the plain "Unity loading" screen with a branded game-concept intro
that plays at boot (reuses the `CutsceneView` overlay grammar as a frame-sequence).

## Method note
`webtoon-harness` native 27-agent runtime is unavailable here
(`CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` unset). We follow its *pipeline*
(research → beat sheet → reference-consistent frame prompts → validate) manually,
generating stills with `god-tibo-imagen` (`gti`) referencing the existing
`Assets/Resources/Scenes/scene-intro.png` for palette/character consistency,
then assemble with `ffmpeg` (Ken-Burns + crossfade) into an H.264 mp4.

## Grounding (docs/nan2026/01-game-overview.md)
- Dusk Warden holds the last lantern; defends the abyssal Cinder Court.
- Core tension: Lantern-oil budget between two competing powers.
- 6-stage abyssal-court campaign; obsidian pillars, ember vents, molten cinders.
- Palette: moody teal shadow + ember/amber glow, volumetric fog, painterly key art.

## Beat sheet (5 frames, 1.8s each with 0.6s cross-fades = 6.6s video)
1. **Ember spark** — a single ember ignites in pitch black; faint obsidian floor.
2. **Lantern reveal** — the ember becomes a glowing lantern held aloft; sparks drift.
3. **Warden rises** — Dusk Warden silhouette stands, lantern lighting the court.
4. **Abyssal court** — wide reveal of obsidian pillars and the vast cinder court.
5. **Cinders surge** — molten cinders swirl upward, court alive with ember light.
6. ~~**Brand hold** — heroic composition, negative space top-center for title lockup.~~
   **CUT.** The generated render (`frames/frame06.png`, 1254x1024-ish square) read as a
   piece of fruit rather than the lantern-bearing Warden, so the beat was dropped
   instead of regenerated. The burned-in title lockup now lands over beat 5.

All frames: 1536x1024 (frame05 came back 1672x941), no baked text in the stills —
ffmpeg burns the title lockup in, and Unity draws the credits overlay.
