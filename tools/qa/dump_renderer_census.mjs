#!/usr/bin/env node
// Runtime renderer census — the probe pale-ring-investigation.md prescribes.
//
// The ring renders through ToonLit yet every .mat asset and MPB site disclaims
// it; the remaining hypothesis is a material that never exists as a .mat
// (FBX-embedded / runtime-made), which only the PLAYING object graph contains.
// So: reach a real dungeon (returning-save seed, same as capture_dungeon_hud),
// call the DEVELOPMENT_BUILD-only GameDirector.DumpRendererCensus via
// SendMessage, and recover the [RingProbe] console lines to a file.
//
// Console hook captures ALL types on purpose: Debug.Log lands as type "log",
// which every existing harness filters out (they keep only "error").
//
// Usage: SMOKE_URL=http://127.0.0.1:8783/ node tools/qa/dump_renderer_census.mjs
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..", "..");
const URL_ = process.env.SMOKE_URL || "http://127.0.0.1:8783/";
const OUT = path.join(ROOT, "_workspace/current/engineering/renderer-census");
const VIEW = { width: 1440, height: 900 };

const SEED = JSON.stringify({
  clearedMask: 0,
  equipment: { weapon: 0, lantern: 0, cloak: 0 },
  stats: { attack: 0, vitality: 0, swiftness: 0, points: 0 },
  relics: 0, roster: [], active: "", prologueDone: true,
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
const consoleLines = [];
page.on("console", (m) => consoleLines.push(m.text()));
page.on("pageerror", (e) => consoleLines.push("PAGEERROR " + String(e)));

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

// One-button route to the dungeon (find_gold_button, same as capture_dungeon_hud).
const probe = path.join(OUT, "probe.png");
await page.screenshot({ path: probe });
const { execFileSync } = await import("node:child_process");
const cta = JSON.parse(execFileSync("python3",
  [path.join(ROOT, "tools/qa/find_gold_button.py"), probe,
   String(VIEW.width), String(VIEW.height)], { encoding: "utf8" }));
if (!cta.found) { console.error("CTA not found"); process.exit(1); }
await page.mouse.move(cta.cx, cta.cy);
await sleep(120); await page.mouse.down(); await sleep(90); await page.mouse.up();
await sleep(8000);   // dungeon settled (wave banner passed)

// DEVELOPMENT_BUILD-only bridge action (gameflow_agent.jslib -> RENDERER_CENSUS
// -> GameDirector.DumpRendererCensus). The GameFlowAgentAPI handle is the one
// JS surface the loader actually exposes — `unityInstance` never escapes its
// .then() callback, so a SendMessage route would throw here.
await page.evaluate(() => window.GameFlowAgentAPI._debugRendererCensus());
await sleep(2500);   // let the log flush
await page.screenshot({ path: path.join(OUT, "dungeon-frame.png") });

const lines = consoleLines.filter((l) => l.includes("[RingProbe]"));
fs.writeFileSync(path.join(OUT, "census.log"), lines.join("\n") + "\n");
const began = lines.some((l) => l.includes("BEGIN"));
const ended = lines.some((l) => l.includes("END"));
console.log(`census lines=${lines.length} begin=${began} end=${ended}`);
console.log(`artifacts: ${path.relative(ROOT, OUT)}`);
// A zero/short dump must read as a capture failure, not as "no renderers".
if (!began || !ended || lines.length < 10) {
  console.error("census incomplete — do not interpret");
  fs.writeFileSync(path.join(OUT, "console-full.log"), consoleLines.join("\n"));
  process.exit(1);
}
await browser.close();
