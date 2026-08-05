#!/usr/bin/env node
// Capture the NAN 2026 submission play video from the DEPLOYED Unity WebGL
// build (GitHub Pages). Every frame is a frame the browser actually rendered
// while the game ran; input goes through the CDP input domain — the same path
// a physical keyboard/mouse takes. Nothing is composited or regenerated; the
// only post steps are a head-trim (loading splash) and H.264 transcode.
//
//   node tools/video/capture-unity-play.mjs [--seconds 55] [--out <path>]
//
// Route: lobby (live diorama) → Cinder Span descent → melee/skills → companion
// command console (Enter, ASCII alias 'nova' → Korean feedback toast) → more
// combat → console 'shield' (Void Aegis) → fight to credits. A returning-player
// save (prologueDone) is seeded via localStorage so stage 1 is unlocked — the
// same JSON shape CampaignStore.Save writes; gameplay itself is not touched.
//
// Headless note: CDP cannot compose Hangul (no IME) and Unity's WebGL input
// reads keyboard events, not DOM insertText — so console commands use the
// parser's documented ASCII aliases; feedback copy on screen stays Korean.
//
// Playwright is resolved from the sibling Abyssal-Surge checkout (dev-machine
// tool dependency, not a runtime dependency of this repo).

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const require = createRequire(import.meta.url);
const { chromium } = require(path.join(ROOT, "..", "..", "..", "Abyssal-Surge", "node_modules", "playwright"));

const URL = "https://akillness.github.io/hongT/";
const W = 1440, H = 900;
// Ember Gallery card (stage 2): dressed combo stage — colonnade ridge, three
// vents with imminence fill, pillar. Coordinate verified by deployed smokes.
const EMBER_GALLERY = { x: 1271, y: 398 };
// Returning veteran seed: cinder-span cleared (unlocks ember-gallery) and
// T4 equipment — the fine-band socket props (ember blade, cyan lantern,
// crimson cloak) and their bloom read on camera.
const SEED = JSON.stringify({
  clearedMask: 1,
  equipment: { weapon: 4, lantern: 4, cloak: 4 },
  stats: { attack: 0, vitality: 0, swiftness: 0, points: 0 },
  relics: 0, roster: [], active: "", prologueDone: true,
});

function parseArgs(argv) {
  const args = { seconds: 55, out: "docs/nan2026/assets/video/nan2026-cinder-court-unity-play.mp4" };
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--seconds") args.seconds = Number(argv[i + 1]);
    if (argv[i] === "--out") args.out = argv[i + 1];
  }
  return args;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const videoDir = fs.mkdtempSync(path.join("/tmp", "unity-capture-"));

  const browser = await chromium.launch();
  const captureStart = Date.now();
  const context = await browser.newContext({
    viewport: { width: W, height: H },
    deviceScaleFactor: 1,
    recordVideo: { dir: videoDir, size: { width: W, height: H } },
  });
  const page = await context.newPage();
  const pageErrors = [];
  page.on("pageerror", (e) => pageErrors.push(String(e)));

  // Returning-player save so stage 1 is unlocked (prologue already cleared).
  await page.goto(URL, { waitUntil: "domcontentloaded" });
  await page.evaluate((seed) => localStorage.setItem("abyssal-lantern:unity:campaign", seed), SEED);
  await page.goto(URL, { waitUntil: "domcontentloaded" });
  await page.waitForSelector("#unity-loading-bar", { state: "hidden", timeout: 120000 });
  await page.waitForTimeout(2200); // splash still plays ~2s after the bar hides
  const loadOffset = (Date.now() - captureStart) / 1000;

  const log = { beats: [], attacks: 0 };
  const started = Date.now();
  const at = () => Number(((Date.now() - started) / 1000).toFixed(1));
  const beat = (name) => log.beats.push({ t: at(), beat: name });

  // 1) Lobby beauty shot — live 3D diorama, sanctum/sortie panels.
  await page.waitForTimeout(5200);
  beat("lobby-done");

  // 2) Descend into Ember Gallery (dressed stage: T-a set dressing, V2 vent
  //    fill, split-terrain grammar; T4 props on the warden; V4 bloom).
  await page.mouse.click(EMBER_GALLERY.x, EMBER_GALLERY.y);
  beat("ember-gallery");
  await page.waitForTimeout(1500);
  // step off the spawn vents immediately
  await page.keyboard.down("KeyS"); await page.keyboard.down("KeyD");
  await page.waitForTimeout(650);
  await page.keyboard.up("KeyS"); await page.keyboard.up("KeyD");

  // Combat helpers -----------------------------------------------------------
  let flip = false;
  const fight = async (ms) => {
    const stop = Date.now() + ms;
    let lastFace = 0, lastSpace = 0, lastR = 0;
    while (Date.now() < stop) {
      const now = Date.now();
      if (now - lastFace > 1100) {
        const key = flip ? "KeyA" : "KeyD";
        flip = !flip;
        await page.keyboard.down(key);
        await page.waitForTimeout(130);
        await page.keyboard.up(key);
        lastFace = Date.now();
      }
      if (now - lastSpace > 270) {
        await page.keyboard.press("Space");
        log.attacks += 1;
        lastSpace = Date.now();
      }
      // Alive: casts Ash Nova when oil allows (harmless extra flair).
      // Dead: the defeat panel binds R = 재강하, so a bad run self-recovers
      // instead of freezing the tail of the video on a static panel.
      if (now - lastR > 6500) {
        await page.keyboard.press("KeyR");
        lastR = Date.now();
      }
      await page.waitForTimeout(45);
    }
  };
  const consoleCommand = async (text) => {
    await page.keyboard.press("Enter");        // open console (0.2x slow-mo)
    await page.waitForTimeout(650);            // let the hint line read
    await page.keyboard.type(text, { delay: 70 });
    await page.waitForTimeout(350);
    await page.keyboard.press("Enter");        // submit -> intent -> SimInput
    beat(`console:${text}`);
    await page.waitForTimeout(900);            // feedback toast + cast visual
  };

  // 3) Fight, showcasing the command console twice — shield early while HP
  //    is high (Void Aegis buys survival), nova once a pack has gathered.
  const end = started + args.seconds * 1000;
  await fight(4500);
  await consoleCommand("shield");  // -> Void Aegis ring + 방패 HUD
  await fight(8000);
  await consoleCommand("nova");    // -> "잿불 노바 시전", AOE burn decal
  while (Date.now() < end) await fight(Math.min(2000, Math.max(250, end - Date.now())));

  await page.waitForTimeout(600);
  const video = page.video();
  await context.close();
  await browser.close();

  const raw = await video.path();
  const outPath = path.join(ROOT, args.out);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  execFileSync("ffmpeg", [
    "-y", "-loglevel", "error",
    "-ss", String(Math.max(0, loadOffset - 0.4)),
    "-i", raw,
    "-t", String(args.seconds),
    "-r", "30", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20",
    outPath,
  ]);
  fs.rmSync(videoDir, { recursive: true, force: true });

  const probe = execFileSync("ffprobe", [
    "-v", "error", "-select_streams", "v:0",
    "-show_entries", "stream=width,height,r_frame_rate,duration",
    "-of", "csv=p=0", outPath,
  ], { encoding: "utf8" }).trim();

  console.log(JSON.stringify({ output: args.out, loadOffset, probe, pageErrors, log }, null, 2));
}

main().catch((error) => { console.error(error); process.exit(1); });
