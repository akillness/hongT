# Retrospective — 2026-08-04 release cycles (texture-cap → console/VFX)

Scope: two Pages deploys (`d4c7392` texture-cap, `6ddd724` console+VFX), full
deployed-route QA (9 routes, 0 errors), submission-doc refresh (test counts,
PDFs, AI-claim amendment), showcase video recapture, evidence commits
`ecc18a4..e280a1d`.

## What worked

- **Gate → build → deploy → live-smoke as one unbroken chain.** Every deploy
  cycle ran EditMode first (111/111, then 146/146), built headless, deployed
  via the worktree script, then smoked the LIVE Pages URL in a real browser
  before claiming anything. Zero "works locally" claims shipped.
- **Seeded-save fixtures.** Seeding `CampaignStore` v2 JSON via localStorage
  unlocked campaign routes for QA/video without playing through the prologue
  each time — and doubled as the persistence read-path check.
- **ASCII parser aliases as the headless console probe.** Proving
  parser→intent→SimInput→cast on the live build without Hangul IME.

## What bit us

- **Pages CDN staleness.** First post-deploy smoke fetched the PREVIOUS build
  (old cache version) and nearly produced false evidence. Fix that stuck:
  poll `curl … | grep 'cache version'` until the new hash serves, then smoke.
- **Frozen defeat panel in video attempt 1.** The fight driver had no retry
  key, so a mid-video death froze 27 s of tail on a static panel. Fix: R in
  the fight cadence (alive = extra Ash Nova, dead = instant 재강하) and cast
  shield early while HP is high.
- **Unity WebGL ignores composed text.** CDP `Input.insertText` never reaches
  the TextField (keyboard-event path only) — headless Hangul input is
  impossible. Documented; Korean coverage lives in the 20 parser unit tests.
- **Doc drift under parallel lanes.** The console deploy made 02-ai-tech's
  "runtime zero-AI" claim false until amended, and test counts went stale
  twice (102→111→146). Submission docs need a re-read after every deploy,
  not just after doc-lane commits.
- **Duplicate deploys.** Two lanes deployed the same source minutes apart
  (`daef754`, then mine). Harmless but wasted work — a deploy claim in the
  task manifest / messages lane would have prevented it.

## Standing observations (design candidates, not defects)

- Cinder Span spawn sits adjacent to vent tiles; a T0 warden standing still
  through the first pulse can die in ~2 s ("위험 지대" defeat copy). Same sim
  as prior builds — spawn-vs-vent phase deserves a design look.
- Mobile portrait dungeon: lore line overlaps the skill bar; the build
  already recommends landscape via toast, so noted as polish, not a blocker.

## Ledger

- Deploys: gh-pages `d4c7392` (cache `61a0b09946ca5642`), `6ddd724`
  (cache `18b0fc1a992f9312`), both live-verified.
- QA: `_workspace/current/qa/deployed-release-verification.md` — 9 routes,
  0 runtime errors/warnings.
- Docs: RELEASE_NOTES two deploy sections; nan2026 README/01/02 + 3 PDFs;
  02-ai-tech "zero-AI by default, opt-in Gemini exception" amendment.
- Video: 55.0 s 1440×900 showcase (lobby → Cinder Span → console shield/nova
  → wave 2 → retry), harness `tools/video/capture-unity-play.mjs`.
- Human-only remainder: YouTube upload + application form.
