# HongT WebGL shell centering fix (2026-08-09)

- tags: hongt, webgl, shell, centering, responsive, unity
- status: durable report

## Summary

`[OBSERVED]` The pre-fix desktop WebGL shell overflowed vertically. At
1366×768, the canvas measured approximately `1152.4375×767.9844`, while the
container also included a 38px footer and an inline-canvas baseline gap. The
container started at `y=-21.4921875`; left and right margins were equal at
`106.78125`. The defect was vertical clipping, not horizontal centering, and
did not require a scrollbar to reproduce.

`[OBSERVED]` The fix preserves the load-bearing `1280:853` aspect ratio
(approximately 3:2), reserves footer height before deriving desktop canvas
width, and uses full-viewport fill for narrow or low-landscape screens.

## Root cause

`[OBSERVED]` `Assets/Editor/BuildScript.cs` emitted a desktop canvas width
bounded by `calc(100vh * 1280 / 853)`. On a height-bound viewport, that made the
canvas itself consume essentially all viewport height. Unity's stock template
then centered the composite container containing:

- the canvas;
- a footer rendered below it;
- an inline-canvas baseline gap.

`[INFERENCE]` The composite became taller than the viewport. Stock
`translate(-50%, -50%)` centered that oversized box, clipping the canvas above
the viewport and the footer below it. Horizontal margins stayed symmetric.

## Decision

`[TARGET]` Keep `1280:853` unchanged because lobby containment behavior depends
on that aspect. Change only shell sizing and responsive membership:

1. Lock `html, body` to the viewport and disable document overflow.
2. Make `#unity-canvas` block-level to remove its inline baseline gap.
3. Give `#unity-footer` an explicit 38px height.
4. Derive desktop canvas width from the height remaining after the footer:
   `calc((100vh - 38px) * 1280 / 853)`, enhanced to `100svh` when supported.
5. Preserve `height:auto` and `aspect-ratio:1280 / 853` on desktop.
6. For width at most 500px, or landscape height at most 500px, make the
   container `100vw×100vh` with a `100dvh` enhancement, make the canvas
   `width:100%; height:100%; aspect-ratio:auto`, and hide the footer.
7. Apply four-edge safe-area padding with `border-box` to both the desktop-UA
   mobile fallback and Unity's `.unity-mobile` path.

The source of truth is `Assets/Editor/BuildScript.cs`; generated
`build-webgl/index.html` must not be patched independently.

## Verification

### Automated evidence

`[OBSERVED]` A filtered Unity EditMode run executed exactly:

`CinderCourt.Tests.BuildScriptWebGlPostprocessTests.PolishIndexHtml_ResyncsResponsiveBackingStoreBeforeLoader_AndIsIdempotent`

The artifact `/tmp/hongt-resolution-tests.xml` reported
`total="1" passed="1" failed="0"`. The full 808-test EditMode suite was not run.

`[OBSERVED]` `bash tools/unity_batch.sh build` completed with `EXIT=0` from a
clean detached worktree at implementation commit `3f12938`.

### Browser evidence

`[OBSERVED]` Before the fix at 1366×768:

- container: `x=106.78125`, `y=-21.4921875`,
  `w=1152.4375`, `h=810.984375`;
- canvas: `x=106.78125`, `y=-21.4921875`,
  `w=1152.4375`, `h=767.984375`;
- left/right canvas margins were equal; the vertical shell exceeded the
  viewport.

`[OBSERVED]` Local and deployed measurements after the fix:

| Viewport | Result |
|---|---|
| 1366×768 | composite shell top/bottom `0.0078125`, left/right `135.2890625`, footer 38px, no scroll |
| 390×844 | container and canvas exactly 390×844, footer hidden, no scroll |
| 844×390 | container and canvas exactly 844×390, footer hidden, no scroll |

At 1366×768 the canvas starts at the shell's top edge and intentionally leaves
38px below it for the footer; `0.0078125` is the composite shell margin, not the
canvas's bottom margin.

`[OBSERVED]` The deployed page loaded without a Unity error banner and contained
the `100svh` footer-subtraction rule.

`[INFERENCE]` Headless Chromium has zero notch inset, so safe-area ownership and
box sizing were verified structurally. A physical notched-device check remains
separate evidence.

Relevant guards:

- `Assets/Tests/EditMode/BuildScriptWebGlPostprocessTests.cs`
- `Assets/Tests/EditMode/LobbyContainmentTests.cs`
- responsive band definitions in `CLAUDE.md` §4r

## Release

`[OBSERVED]`

- implementation commit: `3f12938`
- responsive-band documentation and source snapshot: `d123a00`
- pushed branch: `origin/akillness/main`
- GitHub Pages commit: `3bd950c`
- live URL: https://akillness.github.io/hongT/

The build and deployment were produced from the same clean isolated worktree,
so unrelated uncommitted workspace changes were not included.

## Reusable rule

`[TARGET]`

1. Size a WebGL shell as the complete composite: canvas plus persistent footer
   or other chrome. Subtract sibling chrome before deriving a height-bound
   canvas width.
2. Preserve load-bearing aspect ratios unless game-layout tests and camera
   behavior are intentionally revised together.
3. Remove inline canvas baseline space with `display:block` on the canvas.
4. Verify both responsive bands and low-landscape membership with measured
   composite-shell margins, overflow, CSS size, and backing-store size.
5. On mobile fill paths, verify the actual contract: 100% canvas width and
   height, automatic aspect, hidden footer, and safe-area padding on every UA
   branch.
6. Keep generated HTML and `Assets/Editor/BuildScript.cs` atomic; the build
   postprocessor and its structural test must change together.
7. Build and deploy from the same clean commit. `deploy_pages.sh` replaces the
   Pages tree, so deploying from a dirty or different checkout can publish
   content that was never pushed.
8. GitHub Pages branch updates and CDN publication are asynchronous. Verify the
   `gh-pages` source first, then wait for the live HTML marker rather than
   re-deploying an identical tree.
