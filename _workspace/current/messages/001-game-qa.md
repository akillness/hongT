---
from: game-qa
to:
  - game-designer
  - game-programmer
  - game-pm
  - game-production-director
date: 2026-08-09
subject: C9-DEF-001 active-attacker readability remediation
feedback-requested-by: 2026-08-09
---

# Broadcast — active-attacker readability remediation

- `[OBSERVED]` The retained synthetic G8 panel scored the current/lane read
  `[3,3,4,3,4]`, median `3/5`, and C9-DEF-001 remains an open S2 defect.
- `[OBSERVED]` A view-only cue now identifies up to three enemies whose public
  sim state is `ActorAction.Attack`. Selection is deterministic by attack age,
  then iso distance, then source index; reduced-motion keeps a static chevron.
- `[OBSERVED]` This slice changes no Sim file, frozen contract, reward, price,
  progression rate, paid path, camera, audio, imported asset, or runtime URL.
- `[OBSERVED]` The full Unity EditMode packet is `832 total / 831 passed / 0
  failed / 1 Explicit skipped`; the three new cue tests pass. A separate code
  review found no blocking correctness, allocation, lifecycle, or boundary issue.
- `[TARGET]` Designer feedback: confirm one-primary-plus-room-threat salience and
  whether the ember chevron conflicts with existing hazard/current hierarchy.
- `[TARGET]` Programmer feedback: confirm the fixed three-renderer pool and
  allocation-free sync remain acceptable in a measured browser frame profile.
- `[TARGET]` PM feedback: confirm economy delta remains exactly zero and that no
  G5 claim is inferred from this presentation repair.
- `[TARGET]` Director feedback: retain cycle 9 Stage 2 `FIX`; authorize no G8
  PASS until a qualifying human panel remeasures median `>=4/5` and source
  frequency provenance is verified.

Evidence: `Assets/Scripts/View/VfxDirector.cs:1695-1938`,
`Assets/Scripts/View/GameView.cs:785`,
`Assets/Tests/EditMode/ThreatCuePresentationTests.cs:55-126`, and
`engineering/unity-logs/test-results-115637.xml`.
