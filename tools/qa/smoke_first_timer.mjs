#!/usr/bin/env node
// The first-timer path: open the game with NO save and press exactly one button.
//
// WHY THIS EXISTS. Playtest feedback (2026-08-12) was not about difficulty or feel —
// it was "there is no tutorial and I cannot tell what the UI is, so I cannot start".
// The lobby's entry point was three unlabelled glyphs opening a list of nine cards
// that were all locked. Every EditMode assertion about that lobby passed, because
// EditMode can measure what a control SAYS and where it IS but not whether a person
// who has never seen it can find it (CLAUDE.md §4c).
//
// So this harness asserts the one property the feedback is about, and it is a
// property no unit test can hold: after ONE press on a cold profile, is the game
// running? Everything else here — the frame captures, the HUD probes — exists to
// make a failure legible, not to be the claim.
//
// THE COLD PROFILE IS THE POINT. The other capture harnesses in this directory seed
// localStorage with clearedMask 511 so they can reach a specific stage. That seed is
// exactly what hides this defect: a returning player has unlocked cards, so the
// lobby's four decisions all have obvious answers. This script must never seed. If a
// future edit adds a seed here to make something convenient, it has deleted the test.
//
// Usage:
//   python3 -m http.server 8766 -d build-webgl &
//   node tools/qa/smoke_first_timer.mjs
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..", "..");
const URL_ = process.env.SMOKE_URL || "http://127.0.0.1:8766/";
const OUT = path.join(ROOT, "_workspace/current/qa/first-timer");
const VIEWPORTS = [
  { width: 1440, height: 900, label: "desktop-1440x900" },
  { width: 390, height: 844, label: "mobile-390x844" },
];

function chromium() {
  const require = createRequire(import.meta.url);
  for (const mod of ["playwright", "playwright-core"]) {
    try { return require(mod).chromium; } catch { /* next */ }
  }
  throw new Error("playwright not resolvable — npm i -D playwright");
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// The press recipe, not page.click(). Unity's WebGL input reads the browser's raw
// pointer stream; a synthesised click lands as a down+up in the same frame and the
// canvas never sees a held button. Measured in an earlier cycle: 44 scripted
// "attacks" produced zero sim-side actions. move -> settle -> down -> hold -> up is
// the sequence that registers, and the FIRST press of a session is eaten by focus.
async function press(page, x, y) {
  await page.mouse.move(x, y);
  await sleep(120);
  await page.mouse.down();
  await sleep(90);
  await page.mouse.up();
  await sleep(400);
}

async function observe(page) {
  return page.evaluate(() => window.GameFlowAgentAPI?.observe?.() ?? null);
}

// Locate the CTA by measuring a captured FRAME, not by reading the canvas in-page.
// The in-page route (drawImage + getImageData) returns black: Unity's WebGL context
// has no preserveDrawingBuffer, so the backbuffer is unreadable between frames even
// though Chrome still composites it for a screenshot. That probe cannot tell a
// missing button from an unreadable buffer — the coordinate system where right and
// wrong coincide (CLAUDE.md §4m). tools/qa/find_gold_button.py carries the detection
// and documents what it keys on.
async function findPlayNow(page, dir, viewport) {
  const frame = path.join(dir, "probe-cta.png");
  await page.screenshot({ path: frame });
  const out = execFileSync("python3",
    [path.join(ROOT, "tools/qa/find_gold_button.py"), frame,
     String(viewport.width), String(viewport.height)],
    { encoding: "utf8" });
  return JSON.parse(out);
}

async function runViewport(browser, viewport) {
  const dir = path.join(OUT, viewport.label);
  fs.mkdirSync(dir, { recursive: true });
  // A FRESH context per viewport, so "no save" is a fact rather than a hope.
  const context = await browser.newContext({ viewport, deviceScaleFactor: 1 });
  const page = await context.newPage();
  const pageErrors = [];
  page.on("pageerror", (e) => pageErrors.push(String(e)));
  page.on("console", (m) => { if (m.type() === "error") pageErrors.push(m.text()); });

  await page.goto(URL_, { waitUntil: "domcontentloaded" });
  // Wait for the bridge rather than a fixed sleep: the wasm load time is the one
  // number on this path that varies with the machine.
  await page.waitForFunction(() => !!window.GameFlowAgentAPI?.observe, { timeout: 180000 });
  await sleep(3500);

  const storage = await page.evaluate(() =>
    localStorage.getItem("abyssal-lantern:unity:campaign"));
  await page.screenshot({ path: path.join(dir, "01-cold-first-frame.png") });

  // The intro reel plays BEFORE the lobby and holds the screen until a key. It is
  // part of the first-timer path whether or not it was meant to be — a person who
  // has just opened the page is looking at it, not at the lobby — so the harness
  // skips it the way a player would rather than reaching past it through the API.
  // Two presses: the first is eaten by canvas focus, as always here.
  for (let i = 0; i < 12; i += 1) {
    const st = (await observe(page))?.status?.reason;
    if (st && st !== "loading") break;
    await page.keyboard.press("Space");
    await sleep(1200);
  }
  await sleep(1500);
  await page.screenshot({ path: path.join(dir, "01b-cold-lobby.png") });

  const cta = await findPlayNow(page, dir, viewport);
  const before = await observe(page);

  let after = null;
  if (cta?.found) {
    await press(page, cta.cx, cta.cy);
    await sleep(5000);
    await page.screenshot({ path: path.join(dir, "02-after-one-press.png") });
    after = await observe(page);
  }

  const report = {
    viewport: viewport.label,
    coldProfile: storage === null,
    savePresent: storage,
    ctaFound: !!cta?.found,
    ctaPixels: cta?.pixels ?? 0,
    ctaCssSize: cta?.found ? { w: +cta.wCss.toFixed(1), h: +cta.hCss.toFixed(1) } : null,
    // 44 CSS px is this repo's accessibility floor (LobbyLayoutTests). Measured HERE
    // in the shipped frame, not in the layout fixture — that is the whole point of
    // a browser gate.
    ctaClearsTouchFloor: cta?.found ? (cta.wCss >= 44 && cta.hCss >= 44) : false,
    before: before?.status ?? null,
    after: after?.status ?? null,
    pageErrors,
  };
  fs.writeFileSync(path.join(dir, "report.json"), JSON.stringify(report, null, 2));
  await context.close();
  return report;
}

const browser = await chromium().launch();
const reports = [];
for (const v of VIEWPORTS) reports.push(await runViewport(browser, v));
await browser.close();

fs.writeFileSync(path.join(OUT, "summary.json"), JSON.stringify(reports, null, 2));
let bad = 0;
for (const r of reports) {
  const ok = r.coldProfile && r.ctaFound && r.ctaClearsTouchFloor && r.pageErrors.length === 0;
  if (!ok) bad += 1;
  console.log(`${ok ? "PASS" : "FAIL"}  ${r.viewport}  cold=${r.coldProfile} `
    + `cta=${r.ctaFound} size=${JSON.stringify(r.ctaCssSize)} `
    + `floor=${r.ctaClearsTouchFloor} errors=${r.pageErrors.length}`);
  console.log(`      before=${JSON.stringify(r.before)}`);
  console.log(`      after =${JSON.stringify(r.after)}`);
}
console.log(`\nartifacts: ${path.relative(ROOT, OUT)}`);
process.exit(bad ? 1 : 0);
