// Three-act WebGL smoke for AMENDMENT #17c.
//
// Unity samples pointer input per frame, so an ordinary Playwright click can
// disappear between frames. Every canvas action uses the measured
// move -> settle -> nudge -> hold -> release sequence, and the first press is
// deliberately spent on empty ground to focus the WebGL canvas.
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

const stages = [
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

// Coordinates measured from the layout screenshots emitted by this script at
// the fixed 1440x900 viewport. Acts I/II fit without scrolling. Act III uses a
// single measured wheel step so all three action plates are fully visible.
const actHeaderY = [315, 369, 423];
const stageActionY = [
  [402, 524, 646],
  [456, 579, 701],
  [381, 503, 624],
];

const chromium = resolveChromium();
const out = process.argv[2] ??
  "/Users/jangyoung/orca/workspaces/HongT/main/_workspace/current/qa/amendment17c-smoke";
const url = process.argv[3] ?? "http://127.0.0.1:8766/";
const phase = process.argv[4] ?? "layout";
const width = 1440;
const height = 900;
const reducedMotion = phase === "reduced";
const seed = JSON.stringify({
  clearedMask: 511,
  equipment: { weapon: 4, lantern: 4, cloak: 4 },
  stats: { attack: 0, vitality: 0, swiftness: 0, points: 0 },
  relics: 0,
  roster: [],
  active: "",
  prologueDone: true,
});
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

fs.mkdirSync(out, { recursive: true });
const browser = await chromium.launch({
  headless: true,
  channel: "chrome",
  args: [
    "--use-gl=swiftshader",
    "--enable-unsafe-swiftshader",
    "--ignore-gpu-blocklist",
    "--enable-webgl",
  ],
});

async function press(page, x, y) {
  await page.mouse.move(x, y);
  await sleep(320);
  await page.mouse.move(x, y + 2);
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

async function openSortie(modeReduced = false) {
  const context = await browser.newContext({
    viewport: { width, height },
    reducedMotion: modeReduced ? "reduce" : "no-preference",
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
  }, seed);
  await page.goto(url, { waitUntil: "domcontentloaded" });
  await page.waitForSelector("#unity-loading-bar", { state: "hidden", timeout: 120000 });
  await sleep(4000);
  await press(page, 760, 760); // first canvas press is consumed as focus
  await sleep(700);
  await press(page, 153, 258); // 출정
  await sleep(2200);
  return { context, page, errors };
}

async function captureLayout() {
  const { context, page, errors } = await openSortie();
  const screenshots = [];
  const shot = async (name) => {
    const file = `${name}.png`;
    await page.screenshot({ path: path.join(out, file) });
    screenshots.push(file);
  };

  await shot("layout-00-default-training");
  await press(page, 435, 315); // 제1부 기록
  await sleep(1200);
  await shot("layout-01-act-record");
  await press(page, 435, 735); // 제2부 증언 after Act I expands
  await sleep(1200);
  await shot("layout-02-act-testimony");
  await page.mouse.move(435, 700);
  await page.mouse.wheel(0, 700);
  await sleep(1200);
  await shot("layout-02b-act-testimony-scrolled");
  await press(page, 435, 658); // 제3부 집행 after measured scroll
  await sleep(1200);
  await shot("layout-03-act-enforcement");
  await page.mouse.move(435, 700);
  await page.mouse.wheel(0, 350);
  await sleep(1200);
  await shot("layout-03b-act-enforcement-scrolled");

  fs.writeFileSync(path.join(out, "layout-browser-report.json"), JSON.stringify({
    url,
    viewport: `${width}x${height}`,
    phase,
    screenshots,
    pageErrors: errors,
  }, null, 2) + "\n");
  await context.close();
  return { stage: "layout", automationPass: errors.length === 0, pageErrors: errors };
}

async function runStage(stage, index, modeReduced) {
  const mode = modeReduced ? "reduced" : "normal";
  const stageDir = path.join(
    out,
    modeReduced ? "stages-reduced" : "stages-normal",
    `${String(index + 1).padStart(2, "0")}-${stage.id}`);
  fs.mkdirSync(stageDir, { recursive: true });

  const { context, page, errors } = await openSortie(modeReduced);
  const screenshots = [];
  const shot = async (name) => {
    const file = `${name}.png`;
    await page.screenshot({ path: path.join(stageDir, file) });
    screenshots.push(file);
  };
  const captureCast = async (key, name, captureDelayMs) => {
    await page.keyboard.down(key);
    await sleep(captureDelayMs);
    await shot(name);
    await sleep(Math.max(30, 150 - captureDelayMs));
    await page.keyboard.up(key);
    await sleep(850);
  };

  await shot("00-sortie");
  await press(page, 435, actHeaderY[stage.act]);
  await sleep(1200);
  if (stage.act === 2) {
    await page.mouse.move(435, 700);
    await page.mouse.wheel(0, 350);
    await sleep(1200);
  }
  await shot("01-stage-card");
  await press(page, 573, stageActionY[stage.act][stage.slot]);
  await sleep(7000);

  // Intro/loading cards consume Space. Five presses are harmless after the
  // cards are gone and guarantee the run reaches an interactive frame.
  for (let i = 0; i < 5; i += 1) {
    await hold(page, ["Space"], 100);
    await sleep(650);
  }
  await shot("02-entered");
  const entryObservation = await page.evaluate(() =>
    window.GameFlowAgentAPI?.observe?.() ?? null);
  const entryPass = Boolean(
    entryObservation &&
    entryObservation.world?.current_phase !== "loading" &&
    entryObservation.world?.wave >= 1 &&
    entryObservation.player?.max_hp > 0);
  if (!entryPass) {
    errors.push(
      `assertion: dungeon entry not observed for ${stage.id}: ` +
      JSON.stringify(entryObservation));
  }

  let movementObservation = null;
  let movementPass = null;

  if (modeReduced) {
    await sleep(300);
    await shot("03-reduced-telegraph-a");
    await sleep(120);
    await shot("04-reduced-telegraph-b");
  } else {
    await hold(page, ["KeyW", "KeyD"], 1300);
    await shot("03-moved-ne");
    movementObservation = await page.evaluate(() =>
      window.GameFlowAgentAPI?.observe?.() ?? null);
    const dx = (movementObservation?.player?.position?.x ?? 0) -
      (entryObservation?.player?.position?.x ?? 0);
    const dy = (movementObservation?.player?.position?.y ?? 0) -
      (entryObservation?.player?.position?.y ?? 0);
    movementPass = Math.hypot(dx, dy) > 1;
    if (!movementPass) {
      errors.push(
        `assertion: movement delta not observed for ${stage.id}: ` +
        JSON.stringify({ dx, dy, entryObservation, movementObservation }));
    }
    await hold(page, ["KeyS", "KeyA"], 1300);
    await shot("04-moved-sw");
    await captureCast("Space", "05-melee", 80);
    await captureCast("KeyQ", "06-v3-rift-q", 80);
    await captureCast("KeyE", "07-v2-eruption-e", 120);
    await captureCast("ShiftLeft", "08-v4-shard-shift", 35);
    await captureCast("KeyF", "09-v2-aegis-f", 100);
    await captureCast("KeyR", "10-v3-nova-r", 100);
    for (let i = 0; i < 3; i += 1) {
      await sleep(550);
      await shot(`11-vent-wave-${i + 1}`);
    }
    await shot("12-post-vfx");
  }

  const report = {
    index,
    stageId: stage.id,
    title: stage.title,
    act: stage.act + 1,
    mode,
    url,
    viewport: `${width}x${height}`,
    screenshots,
    gameFlow: {
      entryPass,
      movementPass,
      entryObservation,
      movementObservation,
    },
    automationPass: errors.length === 0,
    pageErrors: errors,
  };
  fs.writeFileSync(
    path.join(stageDir, "browser-report.json"),
    JSON.stringify(report, null, 2) + "\n");
  await context.close();
  console.log(
    `STAGE_SMOKE ${index + 1}/9 ${stage.id} mode=${mode} ` +
    `screenshots=${screenshots.length} pageErrors=${errors.length}`);
  return report;
}

let results;
if (phase === "layout") {
  results = [await captureLayout()];
} else if (phase === "full") {
  results = [];
  for (let i = 0; i < stages.length; i += 1)
    results.push(await runStage(stages[i], i, false));
} else if (phase === "reduced") {
  // One vent-bearing representative per act. The normal pass still covers all
  // nine stages; these three runs verify the fixed-frame accessibility path
  // survives each act's environment and hazard composition.
  results = [];
  for (const index of [1, 4, 8])
    results.push(await runStage(stages[index], index, true));
} else if (phase === "act3") {
  // Targeted recovery pass after visual review of the 3x3 contact sheets.
  // Reuses the same stage directories so stale lobby-only captures are
  // overwritten by evidence from the corrected action-plate centres.
  results = [];
  for (let index = 6; index < stages.length; index += 1)
    results.push(await runStage(stages[index], index, false));
} else if (phase === "probe") {
  results = [await runStage(stages[0], 0, false)];
} else {
  throw new Error(
    `Unknown phase: ${phase}. Expected layout, full, act3, probe, or reduced.`);
}

const errors = results.flatMap((result) => result.pageErrors ?? []);
fs.writeFileSync(path.join(out, `${phase}-browser-report.json`), JSON.stringify({
  phase,
  url,
  viewport: `${width}x${height}`,
  results,
  automationPass: errors.length === 0,
  pageErrors: errors,
}, null, 2) + "\n");

if (phase === "act3" || phase === "probe") {
  const normalResults = stages.map((stage, index) => {
    const reportPath = path.join(
      out,
      "stages-normal",
      `${String(index + 1).padStart(2, "0")}-${stage.id}`,
      "browser-report.json");
    return JSON.parse(fs.readFileSync(reportPath, "utf8"));
  });
  const normalErrors = normalResults.flatMap((result) => result.pageErrors ?? []);
  fs.writeFileSync(path.join(out, "full-browser-report.json"), JSON.stringify({
    phase: "full",
    url,
    viewport: `${width}x${height}`,
    results: normalResults,
    automationPass: normalErrors.length === 0,
    pageErrors: normalErrors,
    rebuiltAfterTargetedAct3Recovery: true,
  }, null, 2) + "\n");
}
await browser.close();

if (errors.length > 0) {
  console.error(JSON.stringify({ errors }, null, 2));
  process.exitCode = 1;
} else {
  console.log(
    `THREE_ACT_SMOKE_OK phase=${phase} runs=${results.length} pageErrors=0`);
}
