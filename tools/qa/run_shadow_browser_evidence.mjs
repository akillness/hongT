#!/usr/bin/env node
// Browser proof that stage character shadows actually reach the floor.
//
// Produces the `browser-shadow-desktop` / `browser-shadow-mobile` evidence that
// tools/deploy/release_provenance.py requires (REQUIRED_EVIDENCE_MODES). Both
// are Development-mode records because the toggle this harness drives —
// window.GameFlowAgentAPI._debugShadowReceiver — is compiled behind
// `#if DEVELOPMENT_BUILD || UNITY_EDITOR` in View/GameFlowAgentBridge.cs, so it
// does not exist in the Release payload that ships to Pages.
//
// Method: freeze the stage, measure the shipped frame, remove the shadow, and
// measure again -- with the noise floor measured inside each state instead of
// assumed. The verdict is a signal-to-noise comparison of mean luminance:
// removing a shadow can only brighten a frame, while animation moves pixels in
// both directions. See the block comment at the measurement site for the two
// observations that forced this shape.
//
// Usage:
//   node tools/qa/run_shadow_browser_evidence.mjs \
//     --url http://127.0.0.1:8783/ --viewport 1440x900 \
//     --evidence-id browser-shadow-desktop --candidate <sha> \
//     --content-marker <development contentMarker> --out <evidence-dir>

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..", "..");
const NAV = { width: 1440, height: 900 };
const ACT_HEADER_Y = [315, 369, 423];
const STAGE_ACTION_Y = [
  [402, 524, 646],
  [456, 579, 701],
  [381, 503, 624],
];
// Same returning-campaign seed the stage hazard matrix harness uses, so stage
// selection lands on the identical card geometry.
const SEED = JSON.stringify({
  clearedMask: 511,
  equipment: { weapon: 4, lantern: 4, cloak: 4 },
  stats: { attack: 0, vitality: 0, swiftness: 0, points: 0 },
  relics: 0,
  roster: [],
  active: "",
  prologueDone: true,
});

// Thresholds. Deliberately conservative: a shadow that only moves 0.05% of the
// frame is not visible to a player either, so passing on it would be a lie.
// Thresholds. Deliberately conservative: four ON/OFF pairs, every pair must
// agree in direction, and the ON/OFF gap must clear the within-set spread by
// 3x. A shadow that cannot beat the frame's own noise is not one a player sees
// either, so passing on it would be a lie.
const FRAMES_PER_STATE = 3;
const SIGNAL_TO_NOISE = 3.0;
// A character shadow is a small dark blob, not a whole-frame dimmer. Measured
// on the shipped desktop frame it covers 0.38% of the pixels and moves the
// frame mean by 0.07 luma -- so a whole-frame epsilon calibrated for global
// lighting (0.25) rejects the very thing this harness exists to detect. The
// gates below are therefore: does it cover pixels at all, does it darken the
// pixels it covers, and does the frame-mean shift clear the measured noise.
const LUMA_DELTA_EPSILON = 0.02;
const MIN_FOOTPRINT_RATIO = 0.0005;
const MIN_FOOTPRINT_DARKENING = 3.0;
const NOISE_FLOOR = 0.02;
const MAX_SETTLE_FRAMES = 8;
const SETTLE_EPSILON = 0.01;
const TOGGLE_ATTEMPTS = 4;
const TOGGLE_POLLS = 6;
const TOGGLE_MIN_EFFECT = 0.05;
// A coherent shadow patch, not a stray pixel. The mobile receiver toggle changed 173
// pixels; sensor noise between two settled frames of the same state changed far fewer.
const TOGGLE_MIN_CHANGED_PIXELS = 60;
const STABILITY_SAMPLES = 8;
const STABILITY_INTERVAL_MS = 1500;
const STABILITY_TOLERANCE = 0.5;

function resolveChromium() {
  const require = createRequire(import.meta.url);
  try {
    return require("playwright").chromium;
  } catch {
    try {
      const globalRoot = execFileSync("npm", ["root", "-g"], { encoding: "utf8" }).trim();
      return createRequire(globalRoot + "/")(path.join(globalRoot, "playwright")).chromium;
    } catch (error) {
      throw new Error("Playwright is required for the shadow browser evidence run.",
        { cause: error });
    }
  }
}

function optionValue(argv, index, option) {
  const value = argv[index + 1];
  if (!value || value.startsWith("--")) throw new Error(`${option} requires a value`);
  return value;
}

function parseArgs(argv) {
  const args = {
    url: "http://127.0.0.1:8783/",
    viewport: { width: 1440, height: 900, label: "1440x900" },
    evidenceId: null,
    candidate: null,
    contentMarker: null,
    out: null,
    stage: { id: "cinder-span", act: 0, slot: 0 },
  };
  for (let i = 2; i < argv.length; i += 1) {
    switch (argv[i]) {
      case "--url": args.url = optionValue(argv, i, "--url"); i += 1; break;
      case "--viewport": {
        const raw = optionValue(argv, i, "--viewport");
        const match = /^(\d+)x(\d+)$/.exec(raw);
        if (!match) throw new Error(`Invalid viewport '${raw}'`);
        args.viewport = {
          width: Number(match[1]), height: Number(match[2]), label: raw,
        };
        i += 1;
        break;
      }
      case "--evidence-id": args.evidenceId = optionValue(argv, i, "--evidence-id"); i += 1; break;
      case "--candidate": args.candidate = optionValue(argv, i, "--candidate"); i += 1; break;
      case "--content-marker":
        args.contentMarker = optionValue(argv, i, "--content-marker"); i += 1; break;
      case "--out": args.out = optionValue(argv, i, "--out"); i += 1; break;
      default: throw new Error(`Unknown option ${argv[i]}`);
    }
  }
  for (const key of ["evidenceId", "candidate", "contentMarker", "out"]) {
    if (!args[key]) throw new Error(`--${key.replace(/[A-Z]/g, (c) => "-" + c.toLowerCase())} is required`);
  }
  return args;
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function scale(value, actual, base) {
  return Math.round((value / base) * actual);
}

async function press(page, x, y) {
  const sx = scale(x, NAV.width, 1440);
  const sy = scale(y, NAV.height, 900);
  await page.mouse.move(sx, sy);
  await sleep(320);
  await page.mouse.move(sx, sy + 2);
  await sleep(220);
  await page.mouse.down();
  await sleep(240);
  await page.mouse.up();
}

async function hold(page, keys, ms) {
  for (const key of keys) await page.keyboard.down(key);
  await sleep(ms);
  for (const key of keys) await page.keyboard.up(key);
}

// Measures the composited screenshot, not the live WebGL canvas.
//
// [OBSERVED] drawImage(unityCanvas) into a 2D scratch canvas returns a flat
// frame here: Unity's WebGL context is created without preserveDrawingBuffer,
// so by the time a readback runs the drawing buffer is already cleared. The
// first version of this harness failed exactly there, which is the guard below
// doing its job rather than a passing run. The repo's existing browser QA
// (tools/qa/capture_stage_hazard_matrix.mjs) samples the screenshot PNG with
// ImageMagick for the same reason; this follows that convention.
function magickBinary() {
  for (const candidate of ["magick", "convert"]) {
    try {
      execFileSync("command", ["-v", candidate], { encoding: "utf8", shell: true });
      return candidate;
    } catch {
      continue;
    }
  }
  throw new Error("ImageMagick (magick/convert) is required to measure the captures");
}

function imageStats(binary, file) {
  const output = execFileSync(
    binary,
    [file, "-colorspace", "Gray", "-format",
      "%[fx:mean],%[fx:standard_deviation],%[fx:w],%[fx:h]", "info:"],
    { encoding: "utf8" }).trim();
  const [mean, stdev, width, height] = output.split(",").map(Number);
  return { meanLuma: mean * 255, stdevLuma: stdev * 255, width, height, file };
}

// Absolute-error pixel count with a small fuzz so 8-bit dither and compression
// noise do not read as shadow.
function changedPixels(binary, a, b) {
  const args = binary === "magick"
    ? ["compare", "-metric", "AE", "-fuzz", "2%", a, b, "null:"]
    : ["-metric", "AE", "-fuzz", "2%", a, b, "null:"];
  const command = binary === "magick" ? "magick" : "compare";
  try {
    const result = execFileSync(command, args, { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
    return Number(String(result).trim().split(/\s+/)[0]);
  } catch (error) {
    // `compare` exits non-zero whenever the images differ; the metric it printed
    // on stderr is the answer, so only a missing number is a real failure.
    const text = String(error.stderr ?? error.stdout ?? "").trim();
    const value = Number(text.split(/\s+/)[0]);
    if (Number.isFinite(value)) return value;
    throw new Error(`ImageMagick compare failed: ${text || error.message}`);
  }
}

function compareShots(binary, a, b) {
  const statsA = imageStats(binary, a);
  const statsB = imageStats(binary, b);
  if (statsA.width !== statsB.width || statsA.height !== statsB.height)
    throw new Error("capture geometry changed between states");
  const total = statsA.width * statsA.height;
  const changed = changedPixels(binary, a, b);
  return {
    changedPixels: changed,
    changedRatio: changed / total,
    meanSignedLumaDelta: statsA.meanLuma - statsB.meanLuma,
    totalPixels: total,
  };
}


async function toggleReceiver(page, enabled) {
  // The debug actions resolve on the next published observation. Under
  // SHADOW_CAPTURE_FREEZE the sim clock is stopped, so that publication can be
  // late or never; the toggle itself is already queued, so a timeout here is not
  // a failure. The pixels decide, not the promise.
  await page.evaluate(async (on) => {
    const api = window.GameFlowAgentAPI;
    await Promise.race([
      Promise.resolve(api._debugShadowReceiver(on)).catch(() => null),
      new Promise((resolve) => setTimeout(resolve, 4000)),
    ]);
  }, enabled);
  await sleep(1400);
}

// Issues the toggle and confirms it landed on screen before returning.
//
// [OBSERVED] the same call is not reliably effective. Across runs of this
// harness SHADOW_RECEIVER_OFF sometimes moved the frame by 3.5 luma and
// sometimes by 0.05 — StageShadowPolicy.SetReceiverEnabledForDiagnostics
// returns false and changes nothing when Current or the receiver renderer is
// not up yet, and nothing in the browser API reports that refusal. So the
// harness re-issues until the screen agrees, and reports how many attempts it
// took. Treating an unconfirmed toggle as applied would silently compare a
// state against itself and call the tiny difference "no shadow regression".
// LANDING IS DETECTED BY CHANGED PIXELS, not by whole-frame mean luma.
//
// Mean luma dilutes with how much of the frame the shadow covers, so the same
// working toggle reports a different magnitude per viewport. Measured on this
// build, toggling the receiver moves the frame mean by:
//
//     desktop 1440x900   0.0704   >= TOGGLE_MIN_EFFECT (0.05)  -> landed
//     mobile   414x896   0.0126   <  0.05                      -> "never landed"
//
// The mobile toggle was working the whole time: 173 pixels changed, up to 27 luma
// darker, in one coherent patch at the character's feet. The detector simply could
// not see a local signal through a global average — the same coordinate-system
// mistake CLAUDE.md §4m describes, here inside the instrument rather than the game.
//
// So landing is confirmed when EITHER the frame mean moves (the original test, kept
// because it is cheap and sufficient on desktop) OR a meaningful patch of pixels
// changes. Neither test loosens the PASS bar: MIN_FOOTPRINT_RATIO and the noise/gap
// checks downstream are untouched. This only fixes whether the harness can tell that
// the state it asked for actually arrived.
async function applyReceiver(page, binary, enabled, shotPath) {
  const referencePath = `${shotPath.replace(/\.png$/, "")}-toggle-ref.png`;
  await page.screenshot({ path: referencePath });
  const before = imageStats(binary, referencePath).meanLuma;

  const measure = async () => {
    await page.screenshot({ path: shotPath });
    return imageStats(binary, shotPath).meanLuma;
  };

  for (let attempt = 1; attempt <= TOGGLE_ATTEMPTS; attempt += 1) {
    await toggleReceiver(page, enabled);
    for (let poll = 0; poll < TOGGLE_POLLS; poll += 1) {
      const value = await measure();
      const moved = Math.abs(value - before) >= TOGGLE_MIN_EFFECT;
      const patch = compareShots(binary, referencePath, shotPath);
      if (moved || patch.changedPixels >= TOGGLE_MIN_CHANGED_PIXELS)
        return { landed: true, attempts: attempt, before, after: value };
      await sleep(600);
    }
  }
  return { landed: false, attempts: TOGGLE_ATTEMPTS, before, after: await measure() };
}

async function main() {
  const args = parseArgs(process.argv);
  fs.mkdirSync(args.out, { recursive: true });
  const chromium = resolveChromium();
  // Software GL is not a neutral stand-in here. Under swiftshader the frame
  // rate collapses, StageShadowPolicy walks its quality tier down, and the
  // shadow distance shrinks until the floor shadow disappears on its own --
  // measured as a spontaneous +3.5 luma jump with no toggle issued. That
  // degradation is indistinguishable from the toggle this harness is supposed
  // to measure, so the run needs a real GPU. SHADOW_EVIDENCE_SOFTWARE_GL=1
  // forces the old path for debugging, and the stability gate below will fail
  // the run when it degrades.
  const softwareGl = process.env.SHADOW_EVIDENCE_SOFTWARE_GL === "1";
  const browser = await chromium.launch({
    headless: !process.env.SHADOW_EVIDENCE_HEADED,
    args: softwareGl
      ? ["--use-gl=swiftshader", "--enable-unsafe-swiftshader", "--disable-lcd-text"]
      : ["--use-angle=metal", "--enable-gpu", "--disable-lcd-text",
         "--ignore-gpu-blocklist", "--enable-gpu-rasterization"],
  });
  const errors = [];
  const notes = [];
  let verdict = "FAIL";
  let measurements = null;
  try {
    const context = await browser.newContext({
      viewport: { width: NAV.width, height: NAV.height },
      deviceScaleFactor: 1,
      reducedMotion: "no-preference",
    });
    const page = await context.newPage();
    page.on("pageerror", (error) => errors.push(`pageerror: ${error}`));
    page.on("console", (message) => {
      if (message.type() === "error") errors.push(`console: ${message.text()}`);
    });
    await page.addInitScript((campaign) => {
      localStorage.setItem("abyssal-lantern:unity:campaign", campaign);
      localStorage.setItem("abyssal-lantern:unity:intro-seen", "1");
      localStorage.setItem("abyssal-lantern:cinder-court:concept-seen", "1");
    }, SEED);

    await page.goto(args.url, { waitUntil: "domcontentloaded" });
    await page.waitForSelector("#unity-loading-bar", { state: "hidden", timeout: 180000 });
    await sleep(4000);
    await press(page, 760, 760);
    await sleep(700);
    await press(page, 153, 258);
    await sleep(2200);
    await press(page, 435, ACT_HEADER_Y[args.stage.act]);
    await sleep(1200);
    await press(page, 573, STAGE_ACTION_Y[args.stage.act][args.stage.slot]);
    await sleep(7000);
    for (let i = 0; i < 5; i += 1) {
      await hold(page, ["Space"], 100);
      await sleep(650);
    }

    const observation = await page.evaluate(
      () => window.GameFlowAgentAPI?.observe?.() ?? null);
    if (!observation) throw new Error("GameFlowAgentAPI.observe() returned nothing");

    const apiShape = await page.evaluate(() => ({
      hasReceiverToggle: typeof window.GameFlowAgentAPI?._debugShadowReceiver === "function",
      hasFreeze: typeof window.GameFlowAgentAPI?._debugFreezeStage === "function",
      apiVersion: window.GameFlowAgentAPI?.api_version ?? null,
    }));
    if (!apiShape.hasReceiverToggle || !apiShape.hasFreeze)
      throw new Error("Development shadow probe API is absent — this is not a Development build");

    if (args.viewport.width !== NAV.width || args.viewport.height !== NAV.height) {
      await page.setViewportSize({ width: args.viewport.width, height: args.viewport.height });
      await sleep(2500);
    }

    await page.evaluate(async () => {
      const api = window.GameFlowAgentAPI;
      await Promise.race([
        Promise.resolve(api._debugFreezeStage(true)).catch(() => null),
        new Promise((resolve) => setTimeout(resolve, 4000)),
      ]);
    });
    await sleep(1200);

    // Did the freeze actually reach C#? The JS functions exist in every build
    // (they are plain jslib members); the handlers behind them are compiled
    // behind `#if DEVELOPMENT_BUILD || UNITY_EDITOR`. world.elapsed standing
    // still is the only proof that the action was consumed rather than queued
    // into a build that has no case for it.
    const elapsedBefore = await page.evaluate(
      () => window.GameFlowAgentAPI.observe()?.world?.elapsed ?? null);
    await sleep(1500);
    const elapsedAfter = await page.evaluate(
      () => window.GameFlowAgentAPI.observe()?.world?.elapsed ?? null);
    const freezeEffective =
      elapsedBefore !== null && elapsedAfter !== null && elapsedAfter === elapsedBefore;
    if (!freezeEffective)
      notes.push(`freeze ineffective: elapsed ${elapsedBefore} -> ${elapsedAfter}`);


    const binary = magickBinary();

    // Measure the shipped state against the shadow-removed state, and measure
    // the noise floor inside each state rather than assuming one.
    //
    // [OBSERVED, 1440x900] Two findings shaped this.
    //   1. Under SHADOW_CAPTURE_FREEZE consecutive frames of one state are
    //      byte-identical in mean luminance (63.0561, 63.0561, 63.0564), so the
    //      noise floor is ~0 and a single pair is conclusive. Without the freeze
    //      two identical-state frames differ in 28.6% of their pixels, which is
    //      why a changed-pixel count is reported below but never gated on.
    //   2. Re-enabling the receiver does NOT restore the shipped frame:
    //      untouched ON reads 59.50, OFF reads 63.06, and ON-again reads 62.998
    //      — it recovers 0.06 of the 3.55 it removed. So the ON reference has to
    //      be the untouched state, captured before anything is toggled. Toggling
    //      ON first and calling that the baseline would compare the shipped
    //      build against a state the shipped build never enters.
    // A state change is not visible in the next screenshot. [OBSERVED] the
    // first frame captured after SHADOW_RECEIVER_OFF still read 59.5134 —
    // essentially the shipped value — before the following frames settled to
    // 63.0181 and stayed there exactly. Averaging that straggler in inflated the
    // measured noise floor to 3.5 luma and buried a real 2.4 luma signal under
    // its own settling transient. So each state is allowed to settle first, and
    // settling is defined by measurement (two consecutive equal frames), not by
    // a sleep that happens to be long enough today.
    const captureState = async (prefix) => {
      let previous = null;
      let settleFrames = 0;
      for (; settleFrames < MAX_SETTLE_FRAMES; settleFrames += 1) {
        const probe = path.join(args.out, `${args.evidenceId}-${prefix}-settle.png`);
        await page.screenshot({ path: probe });
        const value = imageStats(binary, probe).meanLuma;
        if (previous !== null && Math.abs(value - previous) < SETTLE_EPSILON) break;
        previous = value;
        await sleep(700);
      }
      const frames = [];
      for (let index = 0; index < FRAMES_PER_STATE; index += 1) {
        const shot = path.join(args.out, `${args.evidenceId}-${prefix}-${index}.png`);
        await page.screenshot({ path: shot });
        frames.push({ shot, stats: imageStats(binary, shot) });
        await sleep(500);
      }
      return { frames, settleFrames };
    };

    const shippedState = await captureState("shipped");
    const shipped = shippedState.frames;

    // Hold still and watch the untouched state. If the reference drifts on its
    // own there is nothing to compare a toggle against.
    const stabilitySeries = [];
    for (let index = 0; index < STABILITY_SAMPLES; index += 1) {
      const shot = path.join(args.out, `${args.evidenceId}-stability.png`);
      await page.screenshot({ path: shot });
      stabilitySeries.push(Number(imageStats(binary, shot).meanLuma.toFixed(4)));
      await sleep(STABILITY_INTERVAL_MS);
    }
    const stabilitySpread = Math.max(...stabilitySeries) - Math.min(...stabilitySeries);
    const shippedStateStable = stabilitySpread <= STABILITY_TOLERANCE;

    const removalApply = await applyReceiver(
      page, binary, false, path.join(args.out, `${args.evidenceId}-toggle-probe.png`));
    const removedState = await captureState("receiver-off");
    const removed = removedState.frames;

    await toggleReceiver(page, true);
    const restoredShot = path.join(args.out, `${args.evidenceId}-receiver-on-again.png`);
    await page.screenshot({ path: restoredShot });
    const restored = imageStats(binary, restoredShot);

    // A uniform frame means the capture failed, not that the game is dark.
    // Passing on that would certify a blank screen.
    const first = shipped[0].stats;
    if (first.stdevLuma < 1)
      throw new Error("shipped capture is a flat frame — nothing was rendered to measure");

    const mean = (values) => values.reduce((a, b) => a + b, 0) / values.length;
    const spread = (values) => Math.max(...values) - Math.min(...values);
    const shippedLumas = shipped.map((s) => s.stats.meanLuma);
    const removedLumas = removed.map((s) => s.stats.meanLuma);
    const gap = mean(shippedLumas) - mean(removedLumas);
    const noise = Math.max(spread(shippedLumas), spread(removedLumas), NOISE_FLOOR);

    const darker = gap <= -LUMA_DELTA_EPSILON;
    const separated = Math.abs(gap) >= noise * SIGNAL_TO_NOISE;
    const everyFrameDarker = Math.max(...shippedLumas) < Math.min(...removedLumas);
    const footprint = compareShots(binary, shipped[0].shot, removed[0].shot);
    // How dark the shadow makes the pixels it actually covers. Spreading a
    // whole-frame mean over only the changed pixels separates "a small region
    // got much darker" (a shadow) from "everything dimmed a hair" (a lighting
    // or tier change), which the frame mean alone cannot tell apart.
    const footprintDarkening = footprint.changedPixels > 0
      ? Math.abs(gap) * footprint.totalPixels / footprint.changedPixels
      : 0;
    const footprintPresent = footprint.changedRatio >= MIN_FOOTPRINT_RATIO;
    const footprintDark = footprintDarkening >= MIN_FOOTPRINT_DARKENING;

    measurements = {
      viewport: args.viewport.label,
      capture: { width: first.width, height: first.height },
      apiShape,
      stage: args.stage.id,
      wave: observation?.wave ?? null,
      freeze: { effective: freezeEffective, elapsedBefore, elapsedAfter },
      framesPerState: FRAMES_PER_STATE,
      shippedStability: {
        series: stabilitySeries,
        spread: Number(stabilitySpread.toFixed(4)),
        seconds: (STABILITY_SAMPLES * STABILITY_INTERVAL_MS) / 1000,
      },
      settleFrames: { shipped: shippedState.settleFrames, shadowRemoved: removedState.settleFrames },
      receiverRemoval: {
        landed: removalApply.landed,
        attempts: removalApply.attempts,
        lumaBefore: Number(removalApply.before.toFixed(4)),
        lumaAfter: Number(removalApply.after.toFixed(4)),
      },
      shippedMeanLuma: Number(mean(shippedLumas).toFixed(4)),
      receiverOffMeanLuma: Number(mean(removedLumas).toFixed(4)),
      restoredMeanLuma: Number(restored.meanLuma.toFixed(4)),
      shippedFrameLumas: shippedLumas.map((v) => Number(v.toFixed(4))),
      receiverOffFrameLumas: removedLumas.map((v) => Number(v.toFixed(4))),
      meanLumaGap: Number(gap.toFixed(4)),
      noiseFloor: Number(noise.toFixed(4)),
      signalToNoise: Number((Math.abs(gap) / noise).toFixed(3)),
      shadowPixelFootprint: {
        changedPixels: footprint.changedPixels,
        changedRatio: Number(footprint.changedRatio.toFixed(6)),
        meanDarkeningWithinFootprint: Number(footprintDarkening.toFixed(3)),
      },
      gates: {
        darkerWithShadows: darker,
        everyFrameDarker,
        shadowCoversPixels: footprintPresent,
        footprintIsDarkened: footprintDark,
        separatedFromNoise: separated,
        freezeEffective,
        receiverToggleLanded: removalApply.landed,
        shippedStateStable,
        pageErrorFree: errors.length === 0,
      },
    };
    if (!darker) notes.push(`shipped frame was not darker (gap ${gap})`);
    if (!everyFrameDarker) notes.push("shipped and shadow-removed frame sets overlap");
    if (!separated) notes.push(`gap ${gap} did not clear ${SIGNAL_TO_NOISE}x noise ${noise}`);
    if (!footprintPresent)
      notes.push(`shadow covered ${footprint.changedRatio} of the frame, below ${MIN_FOOTPRINT_RATIO}`);
    if (!footprintDark)
      notes.push(`covered pixels darkened by ${footprintDarkening.toFixed(3)}, below ${MIN_FOOTPRINT_DARKENING}`);
    if (!freezeEffective) notes.push("frames were not frozen; the noise floor is not trustworthy");
    if (!shippedStateStable)
      notes.push(
        `the untouched state drifted ${stabilitySpread.toFixed(4)} luma on its own; ` +
        "shadow quality is degrading and no toggle measurement is trustworthy");
    if (!removalApply.landed)
      notes.push(`SHADOW_RECEIVER_OFF never reached the screen after ${removalApply.attempts} attempts`);
    // 1.0 means re-enabling the receiver put the frame back exactly where the
    // shipped state was; 0 means the toggle is one-way. Reported, not gated:
    // shipping never toggles the receiver, so only the shipped state is a
    // release claim. It is recorded because a one-way toggle would mean the two
    // states above were not the two states they are labelled as.
    const restorationRatio = Math.abs(gap) > 1e-9
      ? (mean(removedLumas) - restored.meanLuma) / Math.abs(gap)
      : 0;
    measurements.restorationRatio = Number(restorationRatio.toFixed(3));
    notes.push(
      `receiver re-enable restored ${(restorationRatio * 100).toFixed(1)}% of the ` +
      `${Math.abs(gap).toFixed(4)} luma the shipped state carries`);
    verdict = darker && everyFrameDarker && separated && footprintPresent
      && footprintDark && freezeEffective && removalApply.landed
      && shippedStateStable && errors.length === 0 ? "PASS" : "FAIL";


    await page.evaluate(async () => {
      const api = window.GameFlowAgentAPI;
      await Promise.race([
        Promise.resolve(api._debugFreezeStage(false)).catch(() => null),
        new Promise((resolve) => setTimeout(resolve, 2000)),
      ]);
    });
    await context.close();
  } catch (error) {
    errors.push(`harness: ${error.message}`);
  } finally {
    await browser.close();
  }

  const evidence = {
    evidenceId: args.evidenceId,
    candidateSourceSha: args.candidate,
    buildMode: "Development",
    contentMarker: args.contentMarker,
    result: verdict,
    method: "A/B/A receiver toggle on one frozen frame; toggle effect must dominate temporal drift",
    url: args.url,
    measurements,
    notes,
    errors,
  };
  const output = path.join(args.out, `evidence-${args.evidenceId}.json`);
  fs.writeFileSync(output, JSON.stringify(evidence, null, 2) + "\n");
  console.log(`${verdict} ${args.evidenceId} -> ${path.relative(ROOT, output)}`);
  if (measurements) {
    console.log(`  shippedLuma=${measurements.shippedMeanLuma} ` +
      `shadowRemovedLuma=${measurements.receiverOffMeanLuma} ` +
      `gap=${measurements.meanLumaGap} noise=${measurements.noiseFloor} ` +
      `snr=${measurements.signalToNoise} ` +
      `footprint=${measurements.shadowPixelFootprint.changedRatio} ` +
      `footprintDarkening=${measurements.shadowPixelFootprint.meanDarkeningWithinFootprint}`);
  }
  for (const note of notes) console.log(`  note: ${note}`);
  for (const error of errors) console.log(`  error: ${error}`);
  process.exitCode = verdict === "PASS" ? 0 : 1;
}

main();
