#!/usr/bin/env node
// Stage hazard texture QA matrix for the nine campaign stages.
//
// Uses the proven AMENDMENT #17c WebGL route: seed a returning campaign,
// open Sortie, select the logical stage card, and only accept frames after the
// GameFlowAgentAPI reports a live dungeon wave. Screenshots are raw browser
// captures; the HUD red-bar check samples the Unity canvas pixels in-browser.

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..", "..");
const DEFAULT_URL = "http://127.0.0.1:8766/";
const DEFAULT_OUT = path.join(ROOT, "_workspace/current/qa/stage-hazard-matrix");
const BASE_W = 1440;
const BASE_H = 900;
const NAV_VIEWPORT = { width: BASE_W, height: BASE_H, label: `${BASE_W}x${BASE_H}` };
const HUD_RED_BAR_CROP = { x: 100, y: 28, w: 320, h: 24 };
const UI_REFERENCE_W = 1280;
const UI_REFERENCE_H = 720;
const HUD_SCREENSHOT_REGRESSION_REPORT = path.join(
  ROOT,
  "_workspace/current/qa/stage-hazard-remaster/final-512/mobile-375/375x667/09-ash-march/browser-report.json");
const HUD_SCREENSHOT_REGRESSION_FILE = "03-close-boundary.png";
const ACT_HEADER_Y = [315, 369, 423];
const STAGE_ACTION_Y = [
  [402, 524, 646],
  [456, 579, 701],
  [381, 503, 624],
];
const SEED = JSON.stringify({
  clearedMask: 511,
  equipment: { weapon: 4, lantern: 4, cloak: 4 },
  stats: { attack: 0, vitality: 0, swiftness: 0, points: 0 },
  relics: 0,
  roster: [],
  active: "",
  prologueDone: true,
});

const STAGES = [
  { id: "cinder-span", title: "재의 다리", act: 0, slot: 0 },
  { id: "ember-gallery", title: "불씨 회랑", act: 0, slot: 1 },
  { id: "abyss-chancel", title: "서약의 성당", act: 0, slot: 2 },
  { id: "witness-well", title: "증언의 우물", act: 1, slot: 0 },
  { id: "echo-throne", title: "메아리 왕좌", act: 1, slot: 1 },
  { id: "ash-verdict", title: "재의 판결", act: 1, slot: 2 },
  { id: "cinder-sluice", title: "재의 수문", act: 2, slot: 0 },
  { id: "ember-bastion", title: "불씨 요새", act: 2, slot: 1 },
  { id: "ash-march", title: "재의 행진", act: 2, slot: 2 },
];

function resolveChromium() {
  const require = createRequire(import.meta.url);
  try {
    return require("playwright").chromium;
  } catch {
    try {
      return require(path.join(ROOT, "..", "..", "..", "Abyssal-Surge/node_modules/playwright")).chromium;
    } catch {
      try {
        const globalRoot = execFileSync("npm", ["root", "-g"], { encoding: "utf8" }).trim();
        return createRequire(globalRoot + "/")(path.join(globalRoot, "playwright")).chromium;
      } catch (error) {
        throw new Error(
          "Playwright is required. Install it locally or globally before running this QA harness.",
          { cause: error });
      }
    }
  }
}

function optionValue(argv, index, option) {
  const value = argv[index + 1];
  if (!value || value.startsWith("--"))
    throw new Error(`${option} requires a value`);
  return value;
}

function parseViewport(value) {
  const match = /^(\d+)x(\d+)$/i.exec(value);
  if (!match) throw new Error(`Invalid viewport '${value}', expected WIDTHxHEIGHT`);
  return { width: Number(match[1]), height: Number(match[2]), label: `${match[1]}x${match[2]}` };
}

function parseViewportList(value) {
  return value.split(",").map((part) => parseViewport(part.trim())).filter(Boolean);
}

function parseArgs(argv) {
  const args = {
    url: DEFAULT_URL,
    out: DEFAULT_OUT,
    viewports: [
      parseViewport("1440x900"),
      parseViewport("1280x720"),
      parseViewport("375x667"),
    ],
    phase: "full",
    stage: "all",
    frames: 660,
    headless: true,
  };
  for (let i = 0; i < argv.length; i += 1) {
    switch (argv[i]) {
      case "--url":
        args.url = optionValue(argv, i, "--url");
        i += 1;
        break;
      case "--out":
        args.out = optionValue(argv, i, "--out");
        i += 1;
        break;
      case "--viewports":
        args.viewports = parseViewportList(optionValue(argv, i, "--viewports"));
        i += 1;
        break;
      case "--viewport":
        args.viewports = [parseViewport(optionValue(argv, i, "--viewport"))];
        i += 1;
        break;
      case "--phase":
        args.phase = optionValue(argv, i, "--phase");
        if (!["full", "perf"].includes(args.phase))
          throw new Error(`Unknown phase '${args.phase}', expected full or perf`);
        i += 1;
        break;
      case "--stage":
        args.stage = optionValue(argv, i, "--stage");
        i += 1;
        break;
      case "--frames":
        args.frames = Number(optionValue(argv, i, "--frames"));
        if (!Number.isInteger(args.frames) || args.frames < 120)
          throw new Error("--frames must be an integer >= 120");
        i += 1;
        break;
      case "--headed":
        args.headless = false;
        break;
      case "--self-check":
        args.selfCheck = true;
        break;
      default:
        throw new Error(`Unknown option: ${argv[i]}`);
    }
  }
  if (args.viewports.length === 0) throw new Error("At least one viewport is required");
  return args;
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const scale = (value, current, base) => Math.round(value * current / base);
const median = (values) => percentile(values, 0.5);
const percentile = (values, p) => {
  if (values.length === 0) return null;
  const sorted = [...values].sort((a, b) => a - b);
  const index = Math.min(sorted.length - 1, Math.max(0, Math.ceil(p * sorted.length) - 1));
  return Number(sorted[index].toFixed(3));
};

function canvasScalerScale(width, height) {
  const match = width < height ? 0.35 : 0.5;
  const logWidth = Math.log2(width / UI_REFERENCE_W);
  const logHeight = Math.log2(height / UI_REFERENCE_H);
  return 2 ** (logWidth * (1 - match) + logHeight * match);
}

function projectedHudCrop(width, height, rect = HUD_RED_BAR_CROP) {
  const factor = canvasScalerScale(width, height) / canvasScalerScale(BASE_W, BASE_H);
  return {
    x: rect.x * factor,
    y: rect.y * factor,
    w: rect.w * factor,
    h: rect.h * factor,
  };
}

function hpFillRatio(observation) {
  const hp = Number(observation?.player?.hp);
  const maxHp = Number(observation?.player?.max_hp);
  if (!Number.isFinite(hp) || !Number.isFinite(maxHp) || maxHp <= 0)
    return 1;
  return Math.max(0, Math.min(1, hp / maxHp));
}

function redBarSampleCrop(observation, rect = HUD_RED_BAR_CROP) {
  return {
    ...rect,
    w: Math.max(1, Math.round(rect.w * hpFillRatio(observation))),
  };
}

function expectedViewportRedBarCrop(viewport, observation) {
  const fullCrop = roundViewportCrop(
    { left: 0, top: 0 },
    projectedHudCrop(viewport.width, viewport.height, HUD_RED_BAR_CROP));
  return {
    ...fullCrop,
    w: Math.max(1, Math.round(fullCrop.w * hpFillRatio(observation))),
  };
}

function roundViewportCrop(bounds, crop) {
  return {
    x: Math.round(bounds.left + crop.x),
    y: Math.round(bounds.top + crop.y),
    w: Math.max(1, Math.round(crop.w)),
    h: Math.max(1, Math.round(crop.h)),
  };
}

function cropToCanvasPixels(bounds, canvas, crop) {
  const sx = canvas.width / bounds.width;
  const sy = canvas.height / bounds.height;
  const x = Math.max(0, Math.floor(crop.x * sx));
  const y = Math.max(0, Math.floor(crop.y * sy));
  return {
    x,
    y,
    w: Math.max(1, Math.min(canvas.width - x, Math.round(crop.w * sx))),
    h: Math.max(1, Math.min(canvas.height - y, Math.round(crop.h * sy))),
  };
}

function assertCrop(label, actual, expected) {
  for (const key of ["x", "y", "w", "h"]) {
    if (actual[key] !== expected[key])
      throw new Error(`${label}.${key}: expected ${expected[key]}, got ${actual[key]}`);
  }
}

function runSelfCheck() {
  assertCrop(
    "desktop 1440x900 HUD crop",
    roundViewportCrop(
      { left: 0, top: 0 },
      projectedHudCrop(1440, 900, redBarSampleCrop({ player: { hp: 100, max_hp: 100 } }))),
    HUD_RED_BAR_CROP);
  assertCrop(
    "mobile 375x667 HUD crop",
    roundViewportCrop(
      { left: 0, top: 0 },
      projectedHudCrop(375, 667, redBarSampleCrop({ player: { hp: 100, max_hp: 100 } }))),
    { x: 37, y: 10, w: 118, h: 9 });
  assertCrop(
    "mobile 375x667 low-HP HUD crop",
    expectedViewportRedBarCrop(parseViewport("375x667"), { player: { hp: 57, max_hp: 138 } }),
    { x: 37, y: 10, w: 49, h: 9 });
  runScreenshotRegression();
  console.log("STAGE_HAZARD_MATRIX_SELF_CHECK_OK");
}

function runScreenshotRegression() {
  if (!fs.existsSync(HUD_SCREENSHOT_REGRESSION_REPORT)) return;
  const report = JSON.parse(fs.readFileSync(HUD_SCREENSHOT_REGRESSION_REPORT, "utf8"));
  const checkpoint = report.checkpoints?.find((item) => item.file === HUD_SCREENSHOT_REGRESSION_FILE);
  if (!checkpoint)
    throw new Error(`${HUD_SCREENSHOT_REGRESSION_REPORT}: ${HUD_SCREENSHOT_REGRESSION_FILE} checkpoint missing`);
  const screenshotPath = path.join(path.dirname(HUD_SCREENSHOT_REGRESSION_REPORT), HUD_SCREENSHOT_REGRESSION_FILE);
  if (!fs.existsSync(screenshotPath))
    throw new Error(`${screenshotPath}: regression screenshot missing`);
  const referenceCrop = {
    ...redBarSampleCrop(checkpoint.observation),
    baseW: BASE_W,
    baseH: BASE_H,
    viewport: report.viewport,
    hpFillRatio: Number(hpFillRatio(checkpoint.observation).toFixed(4)),
  };
  const viewport = parseViewport(report.viewport);
  const viewportCrop = roundViewportCrop(
    { left: 0, top: 0 },
    projectedHudCrop(viewport.width, viewport.height, referenceCrop));
  const expectedCrop = expectedViewportRedBarCrop(viewport, checkpoint.observation);
  assertCrop("ash-march 03 low-HP regression crop", viewportCrop, expectedCrop);
  const fullCrop = expectedViewportRedBarCrop(viewport, { player: { hp: 1, max_hp: 1 } });
  if (viewportCrop.w < 1 || viewportCrop.w > fullCrop.w)
    throw new Error(
      `ash-march 03 low-HP regression crop.w outside filled-bar bounds: ` +
      `got ${viewportCrop.w}, expected 1..${fullCrop.w}`);
  const hud = imageMeanRedBar(screenshotPath, viewportCrop, {
    available: true,
    crop: referenceCrop,
    viewportCrop,
    canvasCrop: null,
  });
  if (!hud.available || hud.redMinusGreen <= 20)
    throw new Error(`ash-march 03 low-HP regression failed: ${JSON.stringify(hud)}`);
  console.log(`STAGE_HAZARD_MATRIX_SCREENSHOT_REGRESSION_OK redMinusGreen=${hud.redMinusGreen}`);
}

async function press(page, viewport, x, y) {
  const sx = scale(x, viewport.width, BASE_W);
  const sy = scale(y, viewport.height, BASE_H);
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

async function openSortie(browser, args, viewport) {
  const context = await browser.newContext({
    viewport: { width: NAV_VIEWPORT.width, height: NAV_VIEWPORT.height },
    deviceScaleFactor: 1,
    reducedMotion: "no-preference",
  });
  const page = await context.newPage();
  const errors = [];
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
  await page.waitForSelector("#unity-loading-bar", { state: "hidden", timeout: 120000 });
  await sleep(4000);
  await press(page, NAV_VIEWPORT, 760, 760);
  await sleep(700);
  await press(page, NAV_VIEWPORT, 153, 258);
  await sleep(2200);
  return { context, page, errors };
}

async function enterStage(browser, args, viewport, stage) {
  const resources = await openSortie(browser, args, viewport);
  const { page } = resources;
  await press(page, NAV_VIEWPORT, 435, ACT_HEADER_Y[stage.act]);
  await sleep(1200);
  if (stage.act === 2) {
    await page.mouse.move(435, 700);
    await page.mouse.wheel(0, 350);
    await sleep(1200);
  }
  await press(page, NAV_VIEWPORT, 573, STAGE_ACTION_Y[stage.act][stage.slot]);
  await sleep(7000);
  for (let i = 0; i < 5; i += 1) {
    await hold(page, ["Space"], 100);
    await sleep(650);
  }
  await assertActiveDungeon(page, resources.errors, stage, "entry");
  if (viewport.width !== NAV_VIEWPORT.width || viewport.height !== NAV_VIEWPORT.height) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    await sleep(1500);
  }
  return resources;
}

async function observe(page) {
  return page.evaluate(() => window.GameFlowAgentAPI?.observe?.() ?? null);
}

async function hudRedBarMean(page, viewport, observation) {
  const rect = {
    ...redBarSampleCrop(observation),
    baseW: BASE_W,
    baseH: BASE_H,
    viewport: viewport.label,
    hpFillRatio: Number(hpFillRatio(observation).toFixed(4)),
  };
  const metrics = await page.evaluate(() => {
    const canvas = document.querySelector("#unity-canvas, canvas");
    if (!canvas) return { available: false, reason: "unity canvas missing" };
    const bounds = canvas.getBoundingClientRect();
    return {
      available: true,
      bounds: {
        left: bounds.left,
        top: bounds.top,
        width: bounds.width,
        height: bounds.height,
      },
      canvas: {
        width: canvas.width,
        height: canvas.height,
      },
    };
  });
  if (!metrics.available) return { ...metrics, crop: rect };
  const cssCrop = projectedHudCrop(metrics.bounds.width, metrics.bounds.height, rect);
  const viewportCrop = roundViewportCrop(metrics.bounds, cssCrop);
  const local = cropToCanvasPixels(metrics.bounds, metrics.canvas, cssCrop);
  return page.evaluate(({ rect, local, viewportCrop }) => {
    const canvas = document.querySelector("#unity-canvas, canvas");
    if (!canvas) return { available: false, reason: "unity canvas missing", crop: rect };
    const scratch = document.createElement("canvas");
    scratch.width = canvas.width;
    scratch.height = canvas.height;
    const ctx = scratch.getContext("2d", { willReadFrequently: true });
    try {
      ctx.drawImage(canvas, 0, 0);
      const data = ctx.getImageData(local.x, local.y, local.w, local.h).data;
      let r = 0, g = 0, b = 0;
      const n = data.length / 4;
      for (let i = 0; i < data.length; i += 4) {
        r += data[i];
        g += data[i + 1];
        b += data[i + 2];
      }
      return {
        available: true,
        crop: rect,
        viewportCrop,
        canvasCrop: local,
        mean: {
          r: Number((r / n).toFixed(2)),
          g: Number((g / n).toFixed(2)),
          b: Number((b / n).toFixed(2)),
        },
        redMinusGreen: Number(((r - g) / n).toFixed(2)),
      };
    } catch (error) {
      return { available: false, reason: String(error), crop: rect, canvasCrop: local };
    }
  }, { rect, local, viewportCrop });
}

function commandPath(command) {
  try {
    return execFileSync("/bin/sh", ["-lc", `command -v ${command}`], { encoding: "utf8" }).trim() || null;
  } catch {
    return null;
  }
}

function imageMeanRedBar(screenshotPath, viewportCrop, canvasFallback) {
  const magick = commandPath("magick") ?? commandPath("convert");
  if (!magick) return canvasFallback;
  const crop = `${viewportCrop.w}x${viewportCrop.h}+${viewportCrop.x}+${viewportCrop.y}`;
  try {
    const output = execFileSync(
      magick,
      [screenshotPath, "-crop", crop, "-format", "%[fx:mean.r],%[fx:mean.g],%[fx:mean.b]", "info:"],
      { encoding: "utf8" }).trim();
    const [r, g, b] = output.split(",").map((value) => Number(value) * 255);
    return {
      available: true,
      source: path.basename(magick),
      crop: canvasFallback.crop,
      viewportCrop,
      canvasCrop: canvasFallback.canvasCrop,
      mean: {
        r: Number(r.toFixed(2)),
        g: Number(g.toFixed(2)),
        b: Number(b.toFixed(2)),
      },
      redMinusGreen: Number((r - g).toFixed(2)),
    };
  } catch (error) {
    return { ...canvasFallback, imageFallbackError: String(error) };
  }
}

async function assertActiveDungeon(page, errors, stage, label) {
  const state = await observe(page);
  const pass = Boolean(
    state &&
    state.world?.current_phase !== "loading" &&
    state.world?.wave >= 1 &&
    state.player?.max_hp > 0);
  if (!pass) {
    errors.push(
      `assertion: ${label} lobby/loading frame for ${stage.id}: ` +
      JSON.stringify(state));
  }
  return { pass, state };
}

async function screenshotCheckpoint(page, viewport, stage, stageDir, errors, name) {
  const active = await assertActiveDungeon(page, errors, stage, name);
  const file = `${name}.png`;
  const screenshotPath = path.join(stageDir, file);
  const observation = active.state ?? await observe(page);
  const canvasHud = await hudRedBarMean(page, viewport, observation);
  await page.screenshot({ path: screenshotPath });
  const hud = canvasHud.viewportCrop
    ? imageMeanRedBar(screenshotPath, canvasHud.viewportCrop, canvasHud)
    : canvasHud;
  if (!hud.available || hud.redMinusGreen <= 20) {
    errors.push(
      `assertion: HUD red bar mean failed at ${name} for ${stage.id}: ` +
      JSON.stringify(hud));
  }
  return { file, hud, observation };
}

async function captureStage(browser, args, viewport, stage, index) {
  const stageDir = path.join(
    args.out,
    viewport.label,
    `${String(index + 1).padStart(2, "0")}-${stage.id}`);
  fs.mkdirSync(stageDir, { recursive: true });
  const { context, page, errors } = await enterStage(browser, args, viewport, stage);
  const checkpoints = [];
  try {
    checkpoints.push(await screenshotCheckpoint(page, viewport, stage, stageDir, errors, "00-entry"));
    await hold(page, ["KeyW", "KeyD"], 1200);
    checkpoints.push(await screenshotCheckpoint(page, viewport, stage, stageDir, errors, "01-combat"));
    await page.keyboard.down("KeyE");
    await sleep(120);
    await page.keyboard.up("KeyE");
    await sleep(700);
    for (let i = 0; i < 3; i += 1) {
      await hold(page, ["Space"], 120);
      await sleep(480);
    }
    checkpoints.push(await screenshotCheckpoint(page, viewport, stage, stageDir, errors, "02-active-hazard-or-boss"));
    await hold(page, ["KeyW", "KeyA"], 2100);
    checkpoints.push(await screenshotCheckpoint(page, viewport, stage, stageDir, errors, "03-close-boundary"));
  } finally {
    await context.close();
  }
  const report = {
    stageId: stage.id,
    title: stage.title,
    act: stage.act + 1,
    viewport: viewport.label,
    url: args.url,
    checkpoints,
    pageErrors: errors,
    automationPass: errors.length === 0,
  };
  fs.writeFileSync(path.join(stageDir, "browser-report.json"), JSON.stringify(report, null, 2) + "\n");
  console.log(
    `HAZARD_MATRIX ${viewport.label} ${index + 1}/9 ${stage.id} ` +
    `screenshots=${checkpoints.length} errors=${errors.length}`);
  return report;
}

async function captureFull(browser, args) {
  const selected = args.stage === "all"
    ? STAGES
    : STAGES.filter((stage) => stage.id === args.stage);
  if (selected.length === 0)
    throw new Error(`Unknown stage '${args.stage}'. Expected one of: ${STAGES.map((stage) => stage.id).join(", ")}`);

  const results = [];
  for (const viewport of args.viewports) {
    for (const stage of selected) {
      const index = STAGES.findIndex((candidate) => candidate.id === stage.id);
      results.push(await captureStage(browser, args, viewport, stage, index));
    }
  }
  const errors = results.flatMap((result) => result.pageErrors ?? []);
  const report = {
    phase: "full",
    url: args.url,
    viewports: args.viewports.map((viewport) => viewport.label),
    stages: selected.map((stage) => stage.id),
    results,
    automationPass: errors.length === 0,
    pageErrors: errors,
  };
  fs.writeFileSync(path.join(args.out, "full-browser-report.json"), JSON.stringify(report, null, 2) + "\n");
  return report;
}

async function capturePerf(browser, args) {
  const viewport = parseViewport("1280x720");
  const stage = STAGES.find((candidate) => candidate.id === (args.stage === "all" ? "echo-throne" : args.stage));
  if (!stage) throw new Error(`Unknown perf stage '${args.stage}'`);
  const { context, page, errors } = await enterStage(browser, args, viewport, stage);
  let samples;
  try {
    await hold(page, ["KeyW", "KeyD"], 1200);
    samples = await page.evaluate(async (frames) => {
      const intervals = [];
      let previous = await new Promise((resolve) => requestAnimationFrame(resolve));
      while (intervals.length < frames) {
        const current = await new Promise((resolve) => requestAnimationFrame(resolve));
        intervals.push(current - previous);
        previous = current;
      }
      return intervals;
    }, args.frames);
  } finally {
    await context.close();
  }
  const measured = samples.slice(60);
  const report = {
    phase: "perf",
    stageId: stage.id,
    viewport: viewport.label,
    url: args.url,
    frames: args.frames,
    discardedFrames: 60,
    measuredFrames: measured.length,
    medianMs: median(measured),
    p95Ms: percentile(measured, 0.95),
    over33_3msRatio: Number((measured.filter((value) => value > 33.3).length / measured.length).toFixed(4)),
    maxMs: Number(Math.max(...measured).toFixed(3)),
    pageErrors: errors,
    automationPass: errors.length === 0,
  };
  fs.mkdirSync(args.out, { recursive: true });
  fs.writeFileSync(path.join(args.out, "perf-baseline.json"), JSON.stringify(report, null, 2) + "\n");
  return report;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  if (args.selfCheck) {
    runSelfCheck();
    return;
  }
  fs.mkdirSync(args.out, { recursive: true });
  const chromium = resolveChromium();
  const browser = await chromium.launch({
    headless: args.headless,
    channel: "chrome",
    args: [
      "--use-gl=swiftshader",
      "--enable-unsafe-swiftshader",
      "--ignore-gpu-blocklist",
      "--enable-webgl",
    ],
  });
  try {
    const report = args.phase === "perf"
      ? await capturePerf(browser, args)
      : await captureFull(browser, args);
    const errors = report.pageErrors ?? [];
    if (errors.length > 0) {
      console.error(JSON.stringify({ errors }, null, 2));
      process.exitCode = 1;
    } else {
      console.log(`STAGE_HAZARD_MATRIX_OK phase=${args.phase} errors=0`);
    }
  } finally {
    await browser.close();
  }
}

if (import.meta.url === `file://${process.argv[1]}`) {
  main().catch((error) => {
    console.error(error);
    process.exitCode = 1;
  });
}
