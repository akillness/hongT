#!/usr/bin/env node
// Capture the NAN 2026 submission play video from a Unity WebGL build.
// Every frame is a frame the browser actually rendered while the game ran;
// input goes through the CDP input domain — the same path a physical
// keyboard/mouse takes. Nothing is composited or regenerated. The only post
// steps are a head-trim, an H.264 30 fps mezzanine, and a CompressO offline
// ffmpeg compression pass for the deliverable size.
//
//   node tools/video/capture-unity-play.mjs [--url <build>] [--seconds 55] [--out <path>]
//
// Route: lobby (live diorama) → Ember Gallery descent → (a) plain melee
// attack cadence → (b) hotkey skill rotation (Q Bolt / E Pulse / Shift Dash /
// F Aegis / R Nova) → (c) companion command console: guardian orders
// ('focus', 'defend') then skill casts ('shield' → Void Aegis, 'nova' → Ash
// Nova), each an ASCII alias that the local parser maps to a deterministic
// SimInput latch while the on-screen toast stays Korean. A returning-player
// save (prologueDone) is seeded via localStorage so stage 1 is unlocked — the
// same JSON shape CampaignStore.Save writes; gameplay itself is not touched.
//
// Headless note: CDP cannot compose Hangul (no IME) and Unity's WebGL input
// reads keyboard events, not DOM insertText — so console commands use the
// parser's documented ASCII aliases; feedback copy on screen stays Korean.
//
// Playwright is resolved from the sibling Abyssal-Surge checkout (dev-machine
// tool dependency, not a runtime dependency of this repo). Capture uses the
// installed Chrome channel because Unity WebGL exports AudioClips as AAC; the
// bundled open-source Chromium build has no AAC decoder.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createHash } from "node:crypto";
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const require = createRequire(import.meta.url);
const { chromium } = require(path.join(ROOT, "..", "..", "..", "Abyssal-Surge", "node_modules", "playwright"));

const DEFAULT_URL = "https://akillness.github.io/hongT/";
const W = 1440, H = 900;
// Cycle-13 lobby coordinates at the fixed 1440x900 capture viewport.
const SORTIE_TAB = { x: 153, y: 258 };
const EMBER_GALLERY = { x: 574, y: 517 };
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
  const args = {
    url: DEFAULT_URL,
    seconds: 55,
    out: "docs/nan2026/assets/video/nan2026-cinder-court-unity-play.mp4",
  };
  for (let i = 0; i < argv.length; i += 1) {
    if (argv[i] === "--url") args.url = argv[i + 1];
    if (argv[i] === "--seconds") args.seconds = Number(argv[i + 1]);
    if (argv[i] === "--out") args.out = argv[i + 1];
  }
  return args;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const videoDir = fs.mkdtempSync(path.join("/tmp", "unity-capture-"));

  const browser = await chromium.launch({ channel: "chrome" });
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
  await page.goto(args.url, { waitUntil: "domcontentloaded" });
  await page.evaluate((seed) => localStorage.setItem("abyssal-lantern:unity:campaign", seed), SEED);
  await page.goto(args.url, { waitUntil: "domcontentloaded" });
  await page.waitForSelector("#unity-loading-bar", { state: "hidden", timeout: 120000 });
  await page.waitForTimeout(2200); // splash still plays ~2s after the bar hides
  const loadOffset = (Date.now() - captureStart) / 1000;

  const log = { beats: [], attacks: 0 };
  const started = Date.now();
  const at = () => Number(((Date.now() - started) / 1000).toFixed(1));
  const beat = (name) => log.beats.push({ t: at(), beat: name });
  const tap = async (key, holdMs = 120) => {
    await page.keyboard.down(key);
    await page.waitForTimeout(holdMs);
    await page.keyboard.up(key);
  };

  // 1) Lobby beauty shot — live 3D diorama and the cycle-13 sortie rail.
  await page.waitForTimeout(3200);
  await page.mouse.click(SORTIE_TAB.x, SORTIE_TAB.y);
  beat("sortie-open");
  await page.waitForTimeout(1800);

  // 2) Descend into Ember Gallery.
  await page.mouse.click(EMBER_GALLERY.x, EMBER_GALLERY.y);
  beat("ember-gallery");
  await page.waitForTimeout(1500);
  // Move up-right toward the first threat; held input crosses multiple Unity
  // frames, unlike an instantaneous down/up pair that InputSystem can miss.
  await page.keyboard.down("KeyW"); await page.keyboard.down("KeyD");
  await page.waitForTimeout(1000);
  await page.keyboard.up("KeyW"); await page.keyboard.up("KeyD");

  // Combat helpers -----------------------------------------------------------
  let flip = false;
  // Plain melee: WASD facing flips + Space combo, NO skills. Shows the
  // baseline attack cadence (sim cooldown / combo-link window owns the rate).
  const meleeOnly = async (ms) => {
    const stop = Date.now() + ms;
    let lastFace = 0, lastSpace = 0;
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
      if (now - lastSpace > 360) {
        await tap("Space", 140);
        log.attacks += 1;
        lastSpace = Date.now();
      }
      await page.waitForTimeout(45);
    }
  };
  // Hotkey skill rotation: the dungeon skill row is Q=Rift Bolt, E=Grave
  // Pulse, R=Ash Nova, F=Void Aegis, Shift=Dash. Fire them on their own so
  // the four skill overlays + dash light up on camera, interleaved with melee.
  const skillHotkeys = async () => {
    for (const key of ["KeyQ", "KeyE", "ShiftLeft", "KeyF", "KeyR"]) {
      await tap("Space", 140);   // keep pressure on
      log.attacks += 1;
      await tap(key, 140);
      log.skills = (log.skills || 0) + 1;
      beat(`hotkey:${key}`);
      await page.waitForTimeout(950);
    }
  };
  // Death-safety loop used only between scripted beats: presses R which is
  // Nova mid-run, but the defeat panel rebinds R = 재강하 so a bad run recovers.
  const fight = async (ms) => {
    const stop = Date.now() + ms;
    let lastFace = 0, lastSpace = 0;
    const lastSkill = { KeyQ: 0, KeyE: 0, KeyF: 0, KeyR: 0 };
    const skillCadence = { KeyQ: 2800, KeyE: 4200, KeyF: 8000, KeyR: 6500 };
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
      if (now - lastSpace > 360) {
        await tap("Space", 140);
        log.attacks += 1;
        lastSpace = Date.now();
      }
      for (const [key, cadence] of Object.entries(skillCadence)) {
        if (now - lastSkill[key] <= cadence) continue;
        await tap(key, 140);
        lastSkill[key] = Date.now();
        log.skills = (log.skills || 0) + 1;
      }
      await page.waitForTimeout(45);
    }
  };
  const consoleCommand = async (text) => {
    await tap("Enter", 100);                   // open console (0.2x slow-mo)
    await page.waitForTimeout(650);            // let the hint line read
    await page.keyboard.type(text, { delay: 70 });
    await page.waitForTimeout(350);
    await tap("Enter", 100);                   // submit -> intent -> SimInput
    beat(`console:${text}`);
    await page.waitForTimeout(1100);           // feedback toast + cast visual
  };

  // 3) Three demonstrated input patterns, in order:
  //    (a) plain melee attack cadence, (b) direct hotkey skill rotation,
  //    (c) text command console — a guardian order AND skill casts. Every
  //    console command uses the parser's documented ASCII alias because CDP
  //    has no Hangul IME; the on-screen feedback copy stays Korean.
  const end = started + args.seconds * 1000;
  await meleeOnly(6000);                        // (a) 일반 공격 패턴
  beat("melee-demo-done");
  await skillHotkeys();                          // (b) Q/E/Shift/F/R 스킬 패턴
  beat("skill-demo-done");
  await fight(2500);
  await consoleCommand("focus");   // 수호자 집중공격 order (companion latch)
  await fight(2500);
  await consoleCommand("defend");  // 수호자 방어태세 order
  await fight(2500);
  await consoleCommand("shield");  // -> Void Aegis ring + 공허 방패 시전
  await fight(4000);
  await consoleCommand("nova");    // -> "잿불 노바 시전", AOE burn decal
  while (Date.now() < end) await fight(Math.min(2000, Math.max(250, end - Date.now())));

  await page.waitForTimeout(600);
  const buildIdentity = (await page.content())
    .match(/CinderCourt WebGL build cache version:\s*([0-9a-f]+)/)?.[1] ?? null;
  const video = page.video();
  await context.close();
  await browser.close();

  const raw = await video.path();
  const outPath = path.join(ROOT, args.out);
  fs.mkdirSync(path.dirname(outPath), { recursive: true });

  // Transcode the raw Playwright capture to a mezzanine H.264 (head-trim +
  // 30 fps), then run the CompressO offline ffmpeg pass for the deliverable
  // size. CompressO ships its own static ffmpeg; fall back to PATH ffmpeg if
  // the app is not installed. Playwright's video recorder has no audio track,
  // so both deterministic transcode stages remain explicitly video-only.
  const COMPRESSO_FFMPEG =
    "/Applications/CompressO.app/Contents/MacOS/compresso_ffmpeg";
  const ffmpegBin = fs.existsSync(COMPRESSO_FFMPEG) ? COMPRESSO_FFMPEG : "ffmpeg";
  const mezz = path.join(videoDir, "mezzanine.mp4");
  execFileSync(ffmpegBin, [
    "-y", "-loglevel", "error",
    "-ss", String(Math.max(0, loadOffset - 0.4)),
    "-i", raw,
    "-t", String(args.seconds),
    "-r", "30", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "20",
    "-an",
    mezz,
  ]);
  // CompressO compression pass (crf 28, slow preset, faststart web streaming).
  execFileSync(ffmpegBin, [
    "-y", "-loglevel", "error",
    "-i", mezz,
    "-c:v", "libx264", "-preset", "slow", "-crf", "28",
    "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-an",
    outPath,
  ]);
  fs.rmSync(videoDir, { recursive: true, force: true });


  const probe = execFileSync("ffprobe", [
    "-v", "error", "-select_streams", "v:0",
    "-show_entries", "stream=width,height,r_frame_rate,duration",
    "-of", "csv=p=0", outPath,
  ], { encoding: "utf8" }).trim();

  const pageErrorCounts = {};
  for (const error of pageErrors) pageErrorCounts[error] = (pageErrorCounts[error] ?? 0) + 1;
  const sha256 = createHash("sha256").update(fs.readFileSync(outPath)).digest("hex");
  const report = {
    output: args.out,
    captureUrl: args.url,
    requestedSeconds: args.seconds,
    compressionTool: ffmpegBin === COMPRESSO_FFMPEG ? "CompressO compresso_ffmpeg" : "PATH ffmpeg fallback",
    captureCompletedAt: new Date().toISOString(),
    buildIdentity,
    bytes: fs.statSync(outPath).size,
    sha256,
    loadOffset,
    probe,
    pageErrors: {
      count: pageErrors.length,
      unique: pageErrorCounts,
    },
    log,
  };
  const reportPath = `${outPath}.json`;
  fs.writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
  console.log(JSON.stringify({ ...report, report: path.relative(ROOT, reportPath) }, null, 2));
}

main().catch((error) => { console.error(error); process.exit(1); });
