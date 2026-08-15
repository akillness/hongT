#!/usr/bin/env node
// Shot-tracer browser proof: a save WITH an active companion, a dungeon entry,
// and a burst of frames dense enough to catch a 0.23 s comet in flight.
//
// SEPARATE from capture_dungeon_hud.mjs for the same reason that file is
// separate from smoke_first_timer.mjs: its seed has NO companion (fresh-save
// shape), and a fresh save renders zero of what this capture exists to see —
// SyncCompanionTracers no-ops at CompanionCount 0. One file per save contract.
//
// Detection is pixel-based, not screenshot-eyeballing: the companion comet is
// additive cyan (0.62, 0.95, 1.0) — we count pixels where B > 200, G > 180,
// R < B - 40 outside the HUD bands, sampled across N frames. The comet only
// lives 0.23 s, so any single frame can miss; the PASS is "seen in >= 1 of N".
//
// Usage: SMOKE_URL=http://127.0.0.1:8788/ node tools/qa/capture_companion_tracer.mjs
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..", "..");
const URL_ = process.env.SMOKE_URL || "http://127.0.0.1:8788/";
const OUT = path.join(ROOT, "_workspace/current/qa/shot-tracer");
const VIEW = { width: 1440, height: 900 };

// Prologue done, nothing cleared (one-button routes to stage 1), and — the
// point — scout-echo recruited AND in active slot 0.
const SEED = JSON.stringify({
  clearedMask: 0,
  equipment: { weapon: 0, lantern: 0, cloak: 0 },
  stats: { attack: 0, vitality: 0, swiftness: 0, points: 0 },
  relics: 0,
  roster: ["scout-echo"],
  active: "scout-echo",
  activeSlots: ["scout-echo"],
  prologueDone: true,
  // All 23 guidance bits pre-seen: the first-run card storm freezes the sim
  // (timeScale 0) on exactly the combat beats that fire comets, so a card-
  // heavy capture measures the pause system, not the tracer. A real player
  // sees the cards ONCE ever; this capture is every run after that.
  guidanceSeen: 8388607,
});

function chromium() {
  const require = createRequire(import.meta.url);
  for (const mod of ["playwright", "playwright-core"]) {
    try { return require(mod).chromium; } catch { /* next */ }
  }
  throw new Error("playwright not resolvable");
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

fs.mkdirSync(OUT, { recursive: true });
const browser = await chromium().launch();
const context = await browser.newContext({ viewport: VIEW, deviceScaleFactor: 1 });
await context.addInitScript((save) => {
  localStorage.setItem("abyssal-lantern:unity:campaign", save);
}, SEED);
const page = await context.newPage();
const pageErrors = [];
const fireLines = [];
const stateLines = [];
page.on("pageerror", (e) => pageErrors.push(String(e)));
// Unfiltered console hook: [TracerProbe] breadcrumbs are Debug.Log (type
// "log"), which the error-only hooks in every other harness drop. Fire lines
// separate "never fired" from "fired but undetected"; state lines answer the
// question BEFORE that one — is a companion even in the sim (count=0 means
// nothing downstream can ever fire).
page.on("console", (m) => {
  const text = m.text();
  if (text.includes("[TracerProbe] fire")) fireLines.push(text);
  else if (text.includes("[TracerProbe] state")) stateLines.push(text);
});

await page.goto(URL_, { waitUntil: "domcontentloaded" });
await page.waitForFunction(() => !!window.GameFlowAgentAPI?.observe, { timeout: 180000 });
await sleep(3500);
for (let i = 0; i < 12; i += 1) {
  const st = await page.evaluate(() => window.GameFlowAgentAPI?.observe?.()?.status?.reason);
  if (st && st !== "loading") break;
  await page.keyboard.press("Space");
  await sleep(1200);
}
await sleep(1500);

const probe = path.join(OUT, "probe.png");
await page.screenshot({ path: probe });
const { execFileSync } = await import("node:child_process");
const cta = JSON.parse(execFileSync("python3",
  [path.join(ROOT, "tools/qa/find_gold_button.py"), probe,
   String(VIEW.width), String(VIEW.height)], { encoding: "utf8" }));
if (!cta.found) { console.error("CTA not found"); process.exit(1); }
await page.mouse.move(cta.cx, cta.cy);
await sleep(120); await page.mouse.down(); await sleep(90); await page.mouse.up();
await sleep(6000);

// Dismiss guidance pause cards (they freeze the sim — no swings, no comets),
// then hold still near the fray: the companion fans out beside the player and
// swings on cadence 1.1 s. 24 frames over ~12 s spans ~10 swings.
const FRAMES = 24;
let cometFrames = 0;
let bestCount = 0;
for (let i = 0; i < FRAMES; i += 1) {
  await sleep(450);   // guidance pre-seen: no cards, the sim never freezes
  const shot = path.join(OUT, `frame-${String(i).padStart(2, "0")}.png`);
  await page.screenshot({ path: shot });
  const found = JSON.parse(execFileSync("python3",
    [path.join(ROOT, "tools/qa/count_tracer_pixels.py"), shot], { encoding: "utf8" }));
  if (found.count >= 30) cometFrames += 1;
  if (found.count > bestCount) {
    bestCount = found.count;
    fs.copyFileSync(shot, path.join(OUT, "best-comet-frame.png"));
  }
  // Keep every 6th frame unconditionally as a diagnostic anchor (is the
  // companion even on screen? is a pause card up?) — a 0/N run with zero
  // surviving frames is uninterpretable. The rest are noise on disk.
  if (found.count < 30 && i % 6 !== 0) fs.unlinkSync(shot);
}

fs.writeFileSync(path.join(OUT, "report.json"), JSON.stringify({
  cometFrames, framesSampled: FRAMES, bestCount,
  fires: fireLines.length, fireSample: fireLines.slice(0, 5),
  states: stateLines.slice(-6), pageErrors,
}, null, 2));
console.log(`comet frames ${cometFrames}/${FRAMES} bestCount=${bestCount} `
  + `fires=${fireLines.length} errors=${pageErrors.length}`);
console.log(`last state: ${stateLines[stateLines.length - 1] ?? "none"}`);
console.log(`artifacts: ${path.relative(ROOT, OUT)}`);
if (cometFrames === 0) {
  // The breadcrumbs split the two 0/N hypotheses — but ONLY on a Development
  // build. [TracerProbe] is #if DEVELOPMENT_BUILD, so a Release capture has
  // zero fire lines by construction; reading that as "FireTracer never ran"
  // would send the next person hunting an aim bug that does not exist. The
  // state line is the tell: it comes from the same #if block, so no state
  // lines = no instrumentation = the hypotheses cannot be split here.
  const instrumented = stateLines.length > 0;
  console.error(
    !instrumented
      ? "no breadcrumbs — Release build, cannot split the hypotheses. "
        + "Re-run against build-development to diagnose."
      : fireLines.length === 0
        ? "ZERO FIRES: FireTracer never ran — aim/edge path is the suspect"
        : `fired ${fireLines.length}x but never detected — geometry/detector is the suspect`);
  process.exit(1);
}
await browser.close();
