# Cycle 9 G8 synthetic blind-panel ballots

- run-id: `20260808-achilles-quality`
- capture source: deployed GitHub Pages build, Playwriter session `cycle9-g8-entry-raw.mp4`
- capture set: anonymized crops of live-combat frames; the stage title and objective banner were removed before rating because they named the current mechanic.
- rater type: five independent LLM reviewer subagents. This is a synthetic directional panel, not a human playtest and not eligible to close the human/real-combat impression requirement by itself.
- rubric: 1 no distinctive event; 2 visible but generic; 3 recognizable event with response/outcome unclear; 4 event and response legible; 5 counter/timing and outcome memorable.

| Rater | Randomized capture order | Unprompted recalled element | Score | Evidence |
|---|---|---|---:|---|
| 1 | `anon-a`, `anon-c`, `anon-b`, `anon-d` | giant translucent cyan current/knockback lane, left chevrons, orange impact circles | 3 | Event is recognizable; counter/timing and success/failure are not unambiguous. |
| 2 | `anon-d`, `anon-b`, `anon-a`, `anon-c` | giant cyan conveyor lane, white chevrons, orange blast circles | 3 | Characters move across the lane, but hit/dodge timing and consequence are unresolved. |
| 3 | `anon-c`, `anon-a`, `anon-d`, `anon-b` | orange circular danger zones and characters repositioning around them | 4 | Telegraph motion and evasive response are legible; final outcome is not unmistakable. |
| 4 | `anon-b`, `anon-d`, `anon-c`, `anon-a` | orange circular hazard telegraphs on the blue combat lane | 3 | Hazards remain recognizable, but counter timing and outcome are unclear. |
| 5 | `anon-a`, `anon-b`, `anon-d`, `anon-c` | companion-assisted push through the blue lane and orange circles | 4 | Lane and advance are legible; no precise counter-timing cue is apparent. |

Scores: `[3, 3, 4, 3, 4]`; median: **3/5**.

First-recall frequency: current/lane family `3/5`; orange telegraph family `2/5`. The current/lane pair exceeds the `≤2/5` blind-pair concentration target used by the current QA protocol. The median is below the required `≥4/5` in all cases.

Capture files:

- `_workspace/current/qa/cycle9-g8-anon-a.png`
- `_workspace/current/qa/cycle9-g8-anon-b.png`
- `_workspace/current/qa/cycle9-g8-anon-c.png`
- `_workspace/current/qa/cycle9-g8-anon-d.png`
- `_workspace/current/qa/cycle9-g8-entry-raw.mp4`

Verdict: **FAIL / human-blocked**. The synthetic panel exposes a specific presentation defect—event identification succeeds, but counter/timing and outcome readability do not. The frequency leg remains **UNVERIFIED** because the only matching survey cell is thin evidence (`ETG(t)`, `1/11`) and QA has not revalidated the denominator/source labels for this run.
