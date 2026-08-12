#!/usr/bin/env node
// One dungeon HUD frame, for looking at the bottom control stack.
//
// SEPARATE FROM smoke_first_timer.mjs ON PURPOSE. That harness asserts a cold
// profile and must never seed, because the seed is exactly what hides the defect it
// exists to catch. This one has the opposite job — it needs a returning save so it
// can reach a real dungeon stage, where the skill row and the strike legend exist at
// all. Putting both jobs in one file would mean one of them silently loses its
// precondition the first time someone edits the other.
//
// WHY A DUNGEON AND NOT THE PROLOGUE. The prologue seals skills (CLAUDE.md §4v), so
// the whole bottom stack is absent there — including the strike legend. A capture of
// the prologue cannot show whether the legend renders, and reading one as if it
// could is how three frames got misread in an earlier cycle.
//
// Usage:
//   node tools/qa/capture_dungeon_hud.mjs
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..", "..");
const URL_ = process.env.SMOKE_URL || "http://127.0.0.1:8766/";
const OUT = path.join(ROOT, "_workspace/current/qa/dungeon-hud");
const VIEW = { width: 1440, height: 900 };

// A save that has finished the prologue and cleared nothing, so the one-button route
// lands on stage 1 — the earliest dungeon, which is the one a real player reaches
// first and therefore the one whose HUD matters most for this feedback.
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
  throw new Error("playwright not resolvable — npm i -D playwright");
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const observe = (page) =>
  page.evaluate(() => window.GameFlowAgentAPI?.observe?.() ?? null);

fs.mkdirSync(OUT, { recursive: true });
const browser = await chromium().launch();
const context = await browser.newContext({ viewport: VIEW, deviceScaleFactor: 1 });
await context.addInitScript((save) => {
  localStorage.setItem("abyssal-lantern:unity:campaign", save);
}, SEED);
const page = await context.newPage();
const pageErrors = [];
page.on("pageerror", (e) => pageErrors.push(String(e)));

await page.goto(URL_, { waitUntil: "domcontentloaded" });
await page.waitForFunction(() => !!window.GameFlowAgentAPI?.observe, { timeout: 180000 });
await sleep(3500);
for (let i = 0; i < 12; i += 1) {
  const st = (await observe(page))?.status?.reason;
  if (st && st !== "loading") break;
  await page.keyboard.press("Space");
  await sleep(1200);
}
await sleep(1500);
await page.screenshot({ path: path.join(OUT, "01-lobby.png") });

const probe = path.join(OUT, "probe.png");
await page.screenshot({ path: probe });
const { execFileSync } = await import("node:child_process");
const cta = JSON.parse(execFileSync("python3",
  [path.join(ROOT, "tools/qa/find_gold_button.py"), probe,
   String(VIEW.width), String(VIEW.height)], { encoding: "utf8" }));
if (!cta.found) { console.error("CTA not found — cannot reach a dungeon"); process.exit(1); }

await page.mouse.move(cta.cx, cta.cy);
await sleep(120); await page.mouse.down(); await sleep(90); await page.mouse.up();
await sleep(6000);
await page.screenshot({ path: path.join(OUT, "02-dungeon-hud.png") });

// Let the wave settle, then take a second frame: the first seconds are the wave
// banner's, and the control stack is what this capture is for.
await sleep(6000);
await page.screenshot({ path: path.join(OUT, "03-dungeon-settled.png") });
const after = await observe(page);

fs.writeFileSync(path.join(OUT, "report.json"),
  JSON.stringify({ status: after?.status ?? null, pageErrors }, null, 2));
console.log(`status=${JSON.stringify(after?.status)} errors=${pageErrors.length}`);
console.log(`artifacts: ${path.relative(ROOT, OUT)}`);
await browser.close();
