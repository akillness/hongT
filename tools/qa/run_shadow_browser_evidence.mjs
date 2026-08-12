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
// Method: A/B/A on one frozen frame.
//   A  receiver ON   -> floor receives the character shadow
//   B  receiver OFF  -> same frame, shadow removed from the floor
//   A2 receiver ON   -> back to the first state
// A-vs-B is the shadow. A-vs-A2 is whatever moved on its own (temporal drift).
// The run only passes when the toggle effect dominates the drift, which is what
// makes "the pixels changed" mean "the shadow changed" rather than "time passed".
// A single ON capture cannot separate those two, and a frozen clock is not
// enough on its own: SHADOW_CAPTURE_FREEZE sets Time.timeScale = 0, and
// anything driven by unscaled wall-clock time keeps running underneath it.
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
const MIN_CHANGED_RATIO = 0.005;
const DRIFT_DOMINANCE = 2.0;
const LUMA_DELTA_EPSILON = 0.05;

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

// Reads the live Unity canvas. Returns raw RGBA plus the luminance histogram
// inputs the caller compares; doing the arithmetic in-page avoids shipping
// several megabytes of pixels across the CDP boundary three times.
async function sampleCanvas(page) {
  return page.evaluate(() => {
    const canvas = document.querySelector("#unity-canvas, canvas");
    if (!canvas) return { available: false, reason: "unity canvas missing" };
    const scratch = document.createElement("canvas");
    scratch.width = canvas.width;
    scratch.height = canvas.height;
    const ctx = scratch.getContext("2d", { willReadFrequently: true });
    ctx.drawImage(canvas, 0, 0);
    const data = ctx.getImageData(0, 0, scratch.width, scratch.height).data;
    const luma = new Float32Array(data.length / 4);
    let sum = 0;
    let min = 255;
    let max = 0;
    for (let i = 0, p = 0; i < data.length; i += 4, p += 1) {
      const value = 0.2126 * data[i] + 0.7152 * data[i + 1] + 0.0722 * data[i + 2];
      luma[p] = value;
      sum += value;
      if (value < min) min = value;
      if (value > max) max = value;
    }
    return {
      available: true,
      width: scratch.width,
      height: scratch.height,
      pixels: Array.from(luma),
      meanLuma: sum / luma.length,
      minLuma: min,
      maxLuma: max,
    };
  });
}

function compare(a, b) {
  if (a.width !== b.width || a.height !== b.height)
    throw new Error("canvas geometry changed between captures");
  let changed = 0;
  let signedSum = 0;
  for (let i = 0; i < a.pixels.length; i += 1) {
    const delta = a.pixels[i] - b.pixels[i];
    signedSum += delta;
    if (Math.abs(delta) > 6) changed += 1;
  }
  return {
    changedPixels: changed,
    changedRatio: changed / a.pixels.length,
    meanSignedLumaDelta: signedSum / a.pixels.length,
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

async function main() {
  const args = parseArgs(process.argv);
  fs.mkdirSync(args.out, { recursive: true });
  const chromium = resolveChromium();
  const browser = await chromium.launch({
    args: ["--use-gl=swiftshader", "--enable-unsafe-swiftshader", "--disable-lcd-text"],
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

    await toggleReceiver(page, true);
    const onA = await sampleCanvas(page);
    if (!onA.available) throw new Error(`capture failed: ${onA.reason}`);
    await page.screenshot({ path: path.join(args.out, `${args.evidenceId}-receiver-on.png`) });

    await toggleReceiver(page, false);
    const off = await sampleCanvas(page);
    if (!off.available) throw new Error(`capture failed: ${off.reason}`);
    await page.screenshot({ path: path.join(args.out, `${args.evidenceId}-receiver-off.png`) });

    await toggleReceiver(page, true);
    const onB = await sampleCanvas(page);
    if (!onB.available) throw new Error(`capture failed: ${onB.reason}`);

    // A frame that is uniformly one colour means the readback failed, not that
    // the game is dark. Passing on that would certify a blank canvas.
    if (onA.maxLuma - onA.minLuma < 1)
      throw new Error("receiver-ON capture is a flat frame — canvas readback did not work");

    const shadow = compare(onA, off);
    const drift = compare(onA, onB);
    const dominates = shadow.changedRatio >= drift.changedRatio * DRIFT_DOMINANCE;
    const visible = shadow.changedRatio >= MIN_CHANGED_RATIO;
    const darker = shadow.meanSignedLumaDelta <= -LUMA_DELTA_EPSILON;

    measurements = {
      viewport: args.viewport.label,
      canvas: { width: onA.width, height: onA.height },
      apiShape,
      stage: args.stage.id,
      wave: observation?.wave ?? null,
      receiverOnMeanLuma: Number(onA.meanLuma.toFixed(4)),
      receiverOffMeanLuma: Number(off.meanLuma.toFixed(4)),
      shadowToggle: {
        changedPixels: shadow.changedPixels,
        changedRatio: Number(shadow.changedRatio.toFixed(6)),
        meanSignedLumaDelta: Number(shadow.meanSignedLumaDelta.toFixed(4)),
      },
      temporalDrift: {
        changedPixels: drift.changedPixels,
        changedRatio: Number(drift.changedRatio.toFixed(6)),
      },
      gates: {
        shadowVisible: visible,
        darkerWithReceiver: darker,
        toggleDominatesDrift: dominates,
        pageErrorFree: errors.length === 0,
      },
    };
    if (!visible) notes.push(`changedRatio ${shadow.changedRatio} < ${MIN_CHANGED_RATIO}`);
    if (!darker) notes.push(`receiver ON was not darker (${shadow.meanSignedLumaDelta})`);
    if (!dominates) notes.push(`temporal drift ${drift.changedRatio} not dominated`);
    verdict = visible && darker && dominates && errors.length === 0 ? "PASS" : "FAIL";

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
    console.log(`  changedRatio=${measurements.shadowToggle.changedRatio} ` +
      `drift=${measurements.temporalDrift.changedRatio} ` +
      `lumaDelta=${measurements.shadowToggle.meanSignedLumaDelta}`);
  }
  for (const note of notes) console.log(`  note: ${note}`);
  for (const error of errors) console.log(`  error: ${error}`);
  process.exitCode = verdict === "PASS" ? 0 : 1;
}

main();
