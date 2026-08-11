// Browser gate for the three act cinematics.
//
// This does not play the files in a stand-alone <video>: it exposes the actual
// Unity WebGL instance, calls IntroVideoView.Play(path) through SendMessage,
// and records the HTMLVideoElement events created by Unity's VideoPlayer
// backend. The same script is used against localhost and GitHub Pages.
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

const acts = [
  { id: "act1", clip: "Video/cinder-court-act1.mp4" },
  { id: "act2", clip: "Video/cinder-court-act2.mp4" },
  { id: "act3", clip: "Video/cinder-court-act3.mp4" },
];
const outDir = path.resolve(process.argv[2] ??
  "_workspace/current/qa/amendment17c-smoke/cutscene-local");
const requestedUrl = process.argv[3] ?? "http://127.0.0.1:8766/";
const targetUrl = new URL(requestedUrl);
targetUrl.searchParams.set("intro", "off");
fs.mkdirSync(outDir, { recursive: true });

const chromium = resolveChromium();
const browser = await chromium.launch({
  headless: true,
  channel: "chrome",
  args: [
    "--autoplay-policy=no-user-gesture-required",
    "--use-gl=swiftshader",
    "--enable-unsafe-swiftshader",
    "--ignore-gpu-blocklist",
    "--enable-webgl",
  ],
});
const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  serviceWorkers: "block",
});
const page = await context.newPage();
const pageErrors = [];
const consoleErrors = [];
const requestFailures = [];
page.on("pageerror", (error) => pageErrors.push(String(error)));
page.on("console", (message) => {
  if (message.type() === "error") consoleErrors.push(message.text());
});
page.on("requestfailed", (request) => requestFailures.push({
  url: request.url(),
  error: request.failure()?.errorText ?? "unknown",
}));

await page.addInitScript(() => {
  const probe = window.__hongtVideoProbe = {
    events: [],
    records: [],
    videos: [],
    nextId: 1,
  };
  const mediaEvents = [
    "loadstart", "loadedmetadata", "canplay", "playing", "waiting",
    "stalled", "timeupdate", "pause", "ended", "error", "abort", "emptied",
  ];
  const originalCreateElement = Document.prototype.createElement;
  Document.prototype.createElement = function (tagName, options) {
    const element = originalCreateElement.call(this, tagName, options);
    if (String(tagName).toLowerCase() !== "video") return element;

    const record = { id: probe.nextId++, createdAt: performance.now() };
    probe.records.push(record);
    probe.videos.push(element);
    for (const type of mediaEvents) {
      element.addEventListener(type, () => {
        probe.events.push({
          id: record.id,
          type,
          at: performance.now(),
          src: element.currentSrc || element.src || "",
          currentTime: Number.isFinite(element.currentTime) ? element.currentTime : null,
          duration: Number.isFinite(element.duration) ? element.duration : null,
          readyState: element.readyState,
          networkState: element.networkState,
          paused: element.paused,
          ended: element.ended,
          error: element.error ? {
            code: element.error.code,
            message: element.error.message || "",
          } : null,
          videoWidth: element.videoWidth,
          videoHeight: element.videoHeight,
        });
      });
    }
    return element;
  };

  // The template installs script.onload before appending the Unity loader.
  // A capture-phase load listener runs first and wraps createUnityInstance so
  // the instance remains observable without changing the production template.
  document.addEventListener("load", (event) => {
    const script = event.target;
    if (!(script instanceof HTMLScriptElement) ||
        !script.src.includes(".loader.js") ||
        typeof window.createUnityInstance !== "function") return;
    const original = window.createUnityInstance;
    if (original.__hongtWrapped) return;
    const wrapped = (...args) => original(...args).then((instance) => {
      window.__hongtUnityInstance = instance;
      return instance;
    });
    wrapped.__hongtWrapped = true;
    window.createUnityInstance = wrapped;
  }, true);
});

await page.goto(targetUrl.href, { waitUntil: "domcontentloaded", timeout: 30_000 });
await page.waitForFunction(() => window.GameFlowAgentAPI, null, { timeout: 90_000 });
await page.waitForFunction(() => window.__hongtUnityInstance, null, { timeout: 90_000 });

const results = [];
for (const act of acts) {
  const eventStart = await page.evaluate(() => window.__hongtVideoProbe.events.length);
  await page.evaluate((clip) => {
    window.__hongtUnityInstance.SendMessage("IntroVideo", "PlayClip", clip);
  }, act.clip);

  const filename = path.basename(act.clip);
  await page.waitForFunction(({ start, filename: expected }) =>
    window.__hongtVideoProbe.events.slice(start).some((event) =>
      event.type === "playing" && event.src.includes(expected)),
    { start: eventStart, filename }, { timeout: 30_000 });

  await page.waitForTimeout(900);
  await page.screenshot({
    path: path.join(outDir, `${act.id}-playing.png`),
    fullPage: true,
  });

  await page.waitForFunction(({ start, filename: expected }) =>
    window.__hongtVideoProbe.events.slice(start).some((event) =>
      event.type === "ended" && event.src.includes(expected)),
    { start: eventStart, filename }, { timeout: 20_000 });
  await page.waitForTimeout(900);

  const events = await page.evaluate(({ start, filename: expected }) =>
    window.__hongtVideoProbe.events.slice(start)
      .filter((event) => event.src.includes(expected)),
    { start: eventStart, filename });
  const types = new Set(events.map((event) => event.type));
  const progressed = events.some((event) =>
    event.type === "timeupdate" && (event.currentTime ?? 0) >= 0.25);
  const mediaErrors = events.filter((event) => event.type === "error");
  results.push({
    ...act,
    ok: types.has("loadedmetadata") && types.has("playing") && progressed &&
      types.has("ended") && mediaErrors.length === 0,
    eventTypes: [...types],
    duration: events.find((event) => event.duration)?.duration ?? null,
    dimensions: events.find((event) => event.videoWidth > 0)
      ? {
          width: events.find((event) => event.videoWidth > 0).videoWidth,
          height: events.find((event) => event.videoWidth > 0).videoHeight,
        }
      : null,
    maxCurrentTime: Math.max(0, ...events.map((event) => event.currentTime ?? 0)),
    mediaErrors,
    events,
  });
}

const report = {
  url: targetUrl.href,
  generatedAt: new Date().toISOString(),
  ok: results.every((result) => result.ok) && pageErrors.length === 0 &&
    consoleErrors.length === 0 && requestFailures.length === 0,
  results,
  pageErrors,
  consoleErrors,
  requestFailures,
};
fs.writeFileSync(path.join(outDir, "act-video-report.json"),
  JSON.stringify(report, null, 2) + "\n");
await browser.close();

if (!report.ok) {
  process.stderr.write(JSON.stringify(report, null, 2) + "\n");
  process.exit(1);
}
process.stdout.write(JSON.stringify({
  url: report.url,
  ok: report.ok,
  acts: report.results.map(({ id, duration, dimensions, maxCurrentTime }) =>
    ({ id, duration, dimensions, maxCurrentTime })),
}, null, 2) + "\n");
