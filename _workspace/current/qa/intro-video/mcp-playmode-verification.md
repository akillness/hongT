# Intro reel — in-editor play-mode verification via Unity MCP

Run: 2026-08-06 14:50–15:22 KST. Live Unity Editor 6000.5.6f1, project
`/Users/jangyoung/orca/workspaces/HongT/main`, scene `Assets/Scenes/CinderCourt.unity`.

Transport: the `com.ivanmurzak.unity.mcp` 0.87.0 editor plugin was already
connected to `Library/mcp-server/osx-arm64/gamedev-mcp-server` 9.2.5
(`port=29280 client-transport=streamableHttp`). Driven over streamable HTTP
JSON-RPC (`/tmp/mcpc.py`), tools used: `scene-open`, `console-clear-logs`,
`console-get-logs`, `editor-application-get-state`, `editor-application-set-state`,
`script-execute`, `screenshot-game-view`, `tests-run`.

## [OBSERVED] Two defects that only play mode could show

### 1. Legacy `UnityEngine.Input` threw every frame

First play-mode run, `console-get-logs` returned a repeating exception:

```
InvalidOperationException: You are trying to read Input using the UnityEngine.Input
class, but you have switched active Input handling to Input System package
  at CinderCourt.View.IntroVideoView.Update () (at Assets/Scripts/View/IntroVideoView.cs:106)
```

The throw happened *before* `Step(dt)`, so the state machine never advanced: the
opaque intro canvas stayed at `alpha=1` over the running game indefinitely. Every
EditMode test passed while this was true, because MonoBehaviour `Update` is never
pumped in EditMode.

Fix: `AnySkipPressedThisFrame()` reads `Keyboard.current` / `Mouse.current` /
`Touchscreen.current` (null-guarded, matching `InputAdapter` and `HudView`).

### 2. `!isPlaying` was a false end-of-clip signal

After the input fix, a re-`Play()` traced (`script-execute` coroutine sampling
every 0.5 s) showed the intro tearing itself down ~0.75 s in:

```
[TRACE] t=0.0 canvas=True  alpha=1.00 playing=False frame=-1/234
[TRACE] t=0.5 canvas=True  alpha=0.51 playing=False frame=-1/234
[TRACE] t=1.0 canvas=False alpha=0.00 playing=False frame=-1/0
```

`VideoPlayer.Play()` is asynchronous, so `isPlaying` is still false for the first
frames; the old `!_player.isPlaying && _phaseElapsed > 0.25f` read that as
"finished". A later trace showed the opposite failure at the other end — the
player parks on the last frame with `isPlaying` still true:

```
[RUN] t=5.6  playing=True frame=233/234
[RUN] t=11.5 playing=True frame=233/234   (would hold until the 20 s watchdog)
```

Fix: `_playbackObserved` latch (only trust `!isPlaying` after playback was seen),
`PlayStartTimeout = 2 s` if playback never begins, an explicit last-frame check,
and a `loopPointReached` subscription.

## [OBSERVED] Post-fix boot behaviour

Entering play mode pre-paused (`isPlaying:true, isPaused:true`) freezes the boot
frame, which proves the intro is triggered by the normal boot route rather than by
the probe:

```
[PAUSED] active=True canvas=True alpha=1.00
         url=.../Assets/StreamingAssets/Video/cinder-court-intro.mp4 prepared=False
```

Resuming, the full lifecycle now runs and ends cleanly:

```
[RUN] t=0.0 active=True  canvas=True  alpha=1.00 playing=False frame=-1/234
[RUN] t=0.4 active=True  canvas=True  alpha=1.00 playing=True  frame=8/234
[RUN] t=5.3 active=True  canvas=True  alpha=1.00 playing=True  frame=222/234
[RUN] t=5.8 active=True  canvas=True  alpha=0.67 playing=True  frame=233/234
[RUN] t=6.2 active=False canvas=False alpha=0.00 playing=False frame=-1/0
```

Console after the run: 0 Errors, 0 Exceptions, 0 Warnings.

(The elapsed times are Editor wall-clock: a backgrounded Editor throttles the
render loop, so sampled frame numbers jump. The clip itself is 234 frames / 7.8 s.)

## [OBSERVED] The game view really shows the clip

`screenshot-game-view` taken while paused mid-intro
(`mcp/paused-midintro.png`, probe reported `active=True frame=158`, i.e. t≈5.27 s)
was correlated against the source mp4 downsampled to 64×36 grayscale at 4 fps:

| video timestamp | correlation |
|---|---|
| **5.25 s** | **0.366** (peak) |
| 5.50 s | 0.361 |
| 5.00 s | 0.243 |
| 2.00 s | −0.158 (worst) |

The peak lands on the frame the VideoPlayer reported, independently confirming the
overlay is what the camera output shows.

## [OBSERVED] Tests

`tests-run {testMode: EditMode}` in the live project: **273/273 passed**, 0 failed
(9.19 s). The suite grew from 260 to 273 during the session because another session
added tests concurrently.

New regression test `IntroVideoViewTests.UpdateNeverReadsLegacyInput` invokes
`Update` by reflection and asserts it does not throw. Mutation-checked: temporarily
restoring `if (Input.anyKeyDown)` made exactly that test fail with the
`InvalidOperationException` above (259/260), and reverting restored a green suite.

## [OBSERVED] Still not verified

WebGL player behaviour. This run covers the Editor (Mono/Metal) path only; the
browser VideoPlayer path and the ≤120 MB build budget still need
`tools/unity_batch.sh webgl` plus a served run.

## [OBSERVED] Superseded — reel re-cut to 5 frames (2026-08-06)

The 234-frame / 7.8 s figure above measured the 6-frame cut. Beat 6
(`frames/frame06.png`) was rejected on review — the render read as a piece of
fruit rather than the lantern-bearing Warden — and was removed, so
`Assets/StreamingAssets/Video/cinder-court-intro.mp4` is now
`duration=6.600000, size=1432004, h264 1280x720 @ 30/1` (ffprobe).
Title-lockup rasterisation re-checked on the new final hold: near-white pixels
(r>200, g>195, b>180, saturation<40) inside the 1280x200 title band at y=225 are
7051 at t=5.5 s versus 1 at t=2.0 s. The Editor play-mode findings above are
otherwise unaffected (no code path changed; `IntroVideoView` only had its
duration comment corrected).
