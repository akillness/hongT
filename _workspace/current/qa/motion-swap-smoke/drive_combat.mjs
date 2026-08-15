// Phase-2 smoke: enter Ember Gallery, dismiss overlays, fight ~25 s.
// Screenshots every 2 s + a full video for frame-level motion review.
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
  const globalRoot = execSync("npm root -g").toString().trim();
  return createRequire(globalRoot + "/")(globalRoot + "/playwright").chromium;
}
const chromium = resolveChromium();

const OUT = process.argv[2];
const URL = process.argv[3] || "http://localhost:8765/";
fs.mkdirSync(OUT, { recursive: true });

const W = 1440, H = 900;
const SEED = JSON.stringify({
  clearedMask: 1,
  equipment: { weapon: 4, lantern: 4, cloak: 4 },
  stats: { attack: 0, vitality: 0, swiftness: 0, points: 0 },
  relics: 0, roster: [], active: "", prologueDone: true,
});
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const browser = await chromium.launch({ headless: true, channel: "chrome" });
const ctx = await browser.newContext({
  viewport: { width: W, height: H },
  recordVideo: { dir: OUT, size: { width: W, height: H } },
});
const page = await ctx.newPage();
page.on("console", (m) => {
  if (m.type() === "error") console.log("CONSOLE-ERROR:", m.text().slice(0, 200));
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
await page.mouse.click(153, 258);
await sleep(2000);
await page.mouse.click(574, 517);
await sleep(7000); // stage-entry cutscene
// dismiss cutscene + guidance cards: several discrete presses
for (let i = 0; i < 5; i += 1) {
  await page.keyboard.press("Space");
  await sleep(700);
}
let shotIdx = 0;
const shot = async () => {
  shotIdx += 1;
  await page.screenshot({ path: path.join(OUT, `c${String(shotIdx).padStart(2, "0")}.png`) });
};
// Idle-loop isolation FIRST: the Combat Idle loop is 6.0 s, so the seam is
// only observable in >6 s of continuous standstill — do it before enemies
// close in (spawns start at arena edge).
console.log("IDLE-OBSERVE start 9s");
for (let i = 0; i < 3; i += 1) {
  await sleep(3000);
  await shot();
}
console.log("IDLE-OBSERVE end");
// approach enemies then melee loop with facing flips
await page.keyboard.down("KeyW"); await page.keyboard.down("KeyD");
await sleep(1000);
await page.keyboard.up("KeyW"); await page.keyboard.up("KeyD");
let flip = false;
const stop = Date.now() + 25000;
let lastFace = 0, lastSpace = 0, lastShot = 0;
while (Date.now() < stop) {
  const now = Date.now();
  if (now - lastFace > 1100) {
    const key = flip ? "KeyA" : "KeyD";
    flip = !flip;
    await page.keyboard.down(key);
    await sleep(130);
    await page.keyboard.up(key);
    lastFace = Date.now();
  }
  if (now - lastSpace > 380) {
    await page.keyboard.down("Space");
    await sleep(140);
    await page.keyboard.up("Space");
    lastSpace = Date.now();
  }
  if (now - lastShot > 2000) {
    await shot();
    lastShot = Date.now();
  }
  await sleep(45);
}
// stand still to observe Combat Idle loop
await sleep(4000);
await shot();
const video = page.video();
await ctx.close();
const vp = await video.path();
fs.copyFileSync(vp, path.join(OUT, "combat.webm"));
await browser.close();
console.log("DONE", shotIdx, "shots");
