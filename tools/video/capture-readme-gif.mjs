// Clean README capture on the CURRENT build: a veteran save with every
// guidance lesson already seen, so no cards interrupt the frame.
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { execSync } from "node:child_process";

const toolRequire = createRequire(
  "/Users/jangyoung/orca/workspaces/HongT/main/tools/video/capture-unity-play.mjs");
function resolveChromium() {
  try { return toolRequire("playwright").chromium; } catch {}
  try {
    return toolRequire(
      "/Users/jangyoung/orca/Abyssal-Surge/node_modules/playwright").chromium;
  } catch {}
  const root = execSync("npm root -g").toString().trim();
  return createRequire(root + "/")(root + "/playwright").chromium;
}
const chromium = resolveChromium();

const OUT = process.argv[2];
const URL = process.argv[3] || "http://localhost:8765/";
fs.mkdirSync(OUT, { recursive: true });
const W = 1440, H = 900;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// guidanceSeen: 23 lessons, all shown. Bit ceiling is 31 (sign bit would
// zero the whole field on reload), so 2^23-1 is well inside it.
const SEED = JSON.stringify({
  clearedMask: 3,
  equipment: { weapon: 4, lantern: 4, cloak: 4 },
  stats: { attack: 6, vitality: 6, swiftness: 4, points: 0 },
  relics: 0, roster: [], active: "", activeSlots: [],
  prologueDone: true,
  sigilsOwned: 0, sigilFaces: 0, sigilSlot0: -1, sigilSlot1: -1,
  trialTiers: 0, trainingMastery: 0,
  guidanceSeen: (1 << 23) - 1,
});

const browser = await chromium.launch({ headless: true, channel: "chrome" });
const ctx = await browser.newContext({
  viewport: { width: W, height: H },
  recordVideo: { dir: OUT, size: { width: W, height: H } },
});
const page = await ctx.newPage();
page.on("console", (m) => {
  if (m.type() === "error") console.log("CONSOLE-ERROR:", m.text().slice(0, 160));
});
await page.addInitScript((seed) => {
  window.localStorage.setItem("abyssal-lantern:unity:campaign", seed);
  window.localStorage.setItem("abyssal-lantern:unity:intro-seen", "1");
}, SEED);
await page.goto(URL);
await sleep(30000);
await page.mouse.click(W / 2, H / 2);
await sleep(1200);
await page.keyboard.press("Escape");
await sleep(1200);
await page.mouse.click(153, 258);      // sortie tab
await sleep(2000);
await page.mouse.click(574, 517);      // Ember Gallery
await sleep(7000);                     // stage-entry cutscene
for (let i = 0; i < 6; i += 1) { await page.keyboard.press("Space"); await sleep(600); }

const tap = async (key, ms) => {
  await page.keyboard.down(key); await sleep(ms); await page.keyboard.up(key);
};
// close in, then a continuous skill-and-combo rotation with facing flips
await page.keyboard.down("KeyW"); await page.keyboard.down("KeyD");
await sleep(900);
await page.keyboard.up("KeyW"); await page.keyboard.up("KeyD");
console.log("CAPTURE-START", Date.now());

const skills = ["KeyQ", "KeyE", "KeyR", "KeyF"];
let flip = false, si = 0;
const stop = Date.now() + 26000;
let lastFace = 0, lastSpace = 0, lastSkill = 0, lastDash = 0;
while (Date.now() < stop) {
  const now = Date.now();
  if (now - lastFace > 1500) {
    const k = flip ? "KeyA" : "KeyD"; flip = !flip;
    await tap(k, 150); lastFace = Date.now();
  }
  if (now - lastSpace > 380) { await tap("Space", 130); lastSpace = Date.now(); }
  if (now - lastSkill > 2600) {
    await tap(skills[si % skills.length], 90); si += 1; lastSkill = Date.now();
  }
  if (now - lastDash > 5200) { await tap("ShiftLeft", 90); lastDash = Date.now(); }
  await sleep(45);
}
const video = page.video();
await ctx.close();
fs.copyFileSync(await video.path(), path.join(OUT, "capture.webm"));
await browser.close();
console.log("DONE");
