#!/usr/bin/env node
// Capture the NAN 2026 submission play video from the DEPLOYED Unity WebGL
// build (GitHub Pages). Every frame is a frame the browser actually rendered
// while the game ran; input goes through the CDP input domain — the same path
// a physical keyboard/mouse takes. Nothing is composited or regenerated; the
// only post step is a head-trim (loading splash) and H.264 transcode.
//
//   node tools/video/capture-unity-play.mjs [--seconds 55] [--out <path>]
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
const SORTIE = { x: 1255, y: 220 }; // lobby primary action (verified by smoke)

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

  await page.goto(URL, { waitUntil: "domcontentloaded" });
  await page.waitForSelector("#unity-loading-bar", { state: "hidden", timeout: 120000 });
  // Splash still plays ~2s after the loading bar hides.
  await page.waitForTimeout(2200);
  const loadOffset = (Date.now() - captureStart) / 1000;

  const log = { beats: [], attacks: 0, skills: 0 };
  const started = Date.now();
  const at = () => Number(((Date.now() - started) / 1000).toFixed(1));

  // 1) Lobby beauty shot — live 3D diorama with slow orbit.
  await page.waitForTimeout(5600);
  log.beats.push({ t: at(), beat: "lobby-done" });

  // 2) Sortie into the prologue fight.
  await page.mouse.click(SORTIE.x, SORTIE.y);
  log.beats.push({ t: at(), beat: "sortie" });
  await page.waitForTimeout(1700);

  // 3) Melee loop: stay central so cohorts walk into the 160-range band,
  //    flip facing (A/D) to strike both sides, keep Space cadence, brief
  //    W/S taps for spacing, periodic Q/E/R (skill kit where bound; the
  //    defeat panel also binds R = retry, so a bad run self-recovers).
  const end = started + args.seconds * 1000;
  let flip = false;
  let lastFace = 0, lastSpace = 0, lastSpacing = 0, lastQ = started + 6500, lastE = started + 2500, lastR = started + 9000;

  while (Date.now() < end) {
    const now = Date.now();
    if (now - lastFace > 1100) {
      const key = flip ? "KeyA" : "KeyD";
      flip = !flip;
      await page.keyboard.down(key);
      await page.waitForTimeout(130);
      await page.keyboard.up(key);
      lastFace = Date.now();
    }
    if (now - lastSpacing > 6800) {
      const key = flip ? "KeyW" : "KeyS";
      await page.keyboard.down(key);
      await page.waitForTimeout(190);
      await page.keyboard.up(key);
      lastSpacing = Date.now();
    }
    if (now - lastSpace > 260) {
      await page.keyboard.press("Space");
      log.attacks += 1;
      lastSpace = Date.now();
    }
    if (now - lastQ > 9000) {
      await page.keyboard.press("KeyQ");
      log.skills += 1;
      log.beats.push({ t: at(), beat: "Q" });
      lastQ = Date.now();
    }
    if (now - lastE > 11000) {
      await page.keyboard.press("KeyE");
      log.skills += 1;
      log.beats.push({ t: at(), beat: "E" });
      lastE = Date.now();
    }
    if (now - lastR > 15000) {
      await page.keyboard.press("KeyR");
      log.beats.push({ t: at(), beat: "R" });
      lastR = Date.now();
      await page.waitForTimeout(400);
    }
    await page.waitForTimeout(45);
  }

  await page.waitForTimeout(600);
  const video = page.video();
  await context.close();
  await browser.close();

  const raw = await video.path();
  const outPath = path.join(ROOT, args.out);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  // Head-trim the load splash; H.264 MP4 @30fps for submission parity.
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
