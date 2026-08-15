// Post-deploy smoke against the LIVE Pages URL.
//
// The provenance gate proves the deployed bytes are the bytes we built. It
// cannot prove the game runs: every check upstream of here compares hashes,
// and a WebGL build that boots to a black canvas hashes exactly as well as one
// that plays (CLAUDE.md §4c -- a view lane is only verified once a screen has
// been opened). This script opens the screen.
//
// It deliberately runs as a first-time visitor: no seeded localStorage, no
// skipped intro. That is the path a real visitor takes and the one no other
// harness covers, because the shadow evidence seeds a campaign to reach the
// dungeon quickly.
//
// Usage:
//   node tools/qa/smoke_live_deploy.mjs [--url https://akillness.github.io/hongT/]
//                                       [--out <dir>] [--expect-candidate <sha>]

import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { execFileSync } from "node:child_process";

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..", "..");

const DEFAULT_URL = "https://akillness.github.io/hongT/";
const LOAD_TIMEOUT_MS = 240_000;
// Unity's loading bar hides when the player is up; the frames after that are
// still the first-frame flash, so settle before judging anything visual.
const SETTLE_MS = 6_000;
// A canvas that never leaves one flat colour is the failure this script exists
// to catch, so "did anything render" is measured on the decoded image. An
// earlier version bucketed the raw PNG *file* bytes, which are compressed and
// therefore always spread across every bucket -- it reported success on any
// input whatsoever. Luma standard deviation over the decoded pixels is the
// smallest honest version of the question: a solid fill scores 0.
const MIN_LUMA_STDEV = 8;


function parseArgs(argv) {
  const args = {
    url: DEFAULT_URL,
    out: "_workspace/current/qa/stage-character-shadows",
    expectCandidate: null,
  };
  for (let i = 2; i < argv.length; i += 1) {
    const key = argv[i];
    const value = argv[i + 1];
    if (key === "--url") { args.url = value; i += 1; }
    else if (key === "--out") { args.out = value; i += 1; }
    else if (key === "--expect-candidate") { args.expectCandidate = value; i += 1; }
    else throw new Error(`unknown argument: ${key}`);
  }
  return args;
}

function resolveChromium() {
  const require = createRequire(path.join(ROOT, "package.json"));
  try {
    return require("playwright").chromium;
  } catch {
    const globalRoot = execFileSync("npm", ["root", "-g"], { encoding: "utf8" }).trim();
    return createRequire(globalRoot + "/")(path.join(globalRoot, "playwright")).chromium;
  }
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

// ImageMagick is already the pixel-measurement dependency for the shadow
// harness (tools/qa/run_shadow_browser_evidence.mjs:188-209); this follows the
// same convention rather than adding a second image stack.
function magickBinary() {
  for (const candidate of ["magick", "convert"]) {
    try {
      execFileSync("command", ["-v", candidate], { encoding: "utf8", shell: true });
      return candidate;
    } catch {
      continue;
    }
  }
  throw new Error("ImageMagick (magick/convert) is required to judge the capture");
}

function lumaStdev(file) {
  const output = execFileSync(
    magickBinary(),
    [file, "-colorspace", "Gray", "-format", "%[fx:standard_deviation]", "info:"],
    { encoding: "utf8" }
  ).trim();
  return Number(output) * 255;
}


async function main() {
  const args = parseArgs(process.argv);
  fs.mkdirSync(args.out, { recursive: true });

  const failures = [];
  const notes = [];

  if (args.expectCandidate) {
    const url = new URL("release-build-provenance.json", args.url).toString();
    const response = await fetch(`${url}?cb=${Date.now()}`);
    if (!response.ok) {
      failures.push(`provenance fetch ${response.status}`);
    } else {
      const provenance = await response.json();
      if (provenance.candidateSourceSha !== args.expectCandidate) {
        failures.push(
          `live candidate ${provenance.candidateSourceSha} != ${args.expectCandidate}`
        );
      } else {
        notes.push(`live provenance candidate ${provenance.candidateSourceSha}`);
      }
    }
  }

  const chromium = resolveChromium();
  // Same GPU requirement as the shadow harness: under swiftshader the frame
  // rate collapses and StageShadowPolicy walks its own quality tier down, so a
  // software run reports a degraded game rather than the deployed one.
  const browser = await chromium.launch({
    headless: !process.env.SMOKE_HEADED,
    args: ["--use-angle=metal", "--enable-gpu", "--ignore-gpu-blocklist"],
  });

  try {
    const context = await browser.newContext({
      viewport: { width: 1440, height: 900 },
      deviceScaleFactor: 1,
    });
    const page = await context.newPage();
    const errors = [];
    page.on("pageerror", (error) => errors.push(`pageerror: ${error}`));
    page.on("console", (message) => {
      if (message.type() === "error") errors.push(`console: ${message.text()}`);
    });
    const failedRequests = [];
    // net::ERR_ABORTED is what a still-streaming <video> reports when the page
    // is torn down, so it fires for the intro reel on every clean run. Treating
    // it as a defect made this script fail a deployment whose video served 200
    // with correct bytes. Silencing it would have been the other mistake, so
    // aborted URLs are re-checked directly below and only kept if they really
    // do not resolve.
    const abortedUrls = new Set();
    page.on("requestfailed", (request) => {
      const reason = request.failure()?.errorText ?? "unknown";
      if (reason.includes("ERR_ABORTED")) abortedUrls.add(request.url());
      else failedRequests.push(`${request.url()} ${reason}`);
    });
    page.on("response", (response) => {
      if (response.status() >= 400) {
        failedRequests.push(`${response.url()} HTTP ${response.status()}`);
      }
    });


    const started = Date.now();
    await page.goto(args.url, { waitUntil: "domcontentloaded", timeout: 60_000 });
    await page.waitForSelector("#unity-loading-bar", {
      state: "hidden",
      timeout: LOAD_TIMEOUT_MS,
    });
    const loadSeconds = ((Date.now() - started) / 1000).toFixed(1);
    notes.push(`player up in ${loadSeconds}s`);
    await sleep(SETTLE_MS);

    const shot = path.join(args.out, "live-deploy-smoke.png");
    await page.screenshot({ path: shot });
    const stdev = lumaStdev(shot);
    notes.push(`screenshot ${shot} (luma stdev ${stdev.toFixed(2)})`);
    if (stdev < MIN_LUMA_STDEV) {
      failures.push(`canvas looks blank: luma stdev ${stdev.toFixed(2)} < ${MIN_LUMA_STDEV}`);
    }


    const canvas = await page.evaluate(() => {
      const element = document.querySelector("#unity-canvas") || document.querySelector("canvas");
      if (!element) return null;
      const rect = element.getBoundingClientRect();
      return { width: Math.round(rect.width), height: Math.round(rect.height) };
    });
    if (!canvas || canvas.width < 100 || canvas.height < 100) {
      failures.push(`canvas missing or degenerate: ${JSON.stringify(canvas)}`);
    } else {
      notes.push(`canvas ${canvas.width}x${canvas.height}`);
    }

    // Re-check every aborted URL out-of-band. A torn-down media stream still
    // resolves; a missing asset does not, and this is where that distinction
    // gets measured instead of guessed.
    for (const url of abortedUrls) {
      const response = await fetch(`${url}${url.includes("?") ? "&" : "?"}cb=${Date.now()}`, {
        method: "GET",
        headers: { Range: "bytes=0-0" },
      }).catch((error) => ({ ok: false, status: `fetch failed: ${error}` }));
      if (response.ok) notes.push(`aborted mid-stream but resolves: ${url}`);
      else failures.push(`aborted and does not resolve: ${url} (${response.status})`);
    }

    if (errors.length) failures.push(...errors.slice(0, 10));
    if (failedRequests.length) failures.push(...failedRequests.slice(0, 10));

  } finally {
    await browser.close();
  }

  for (const note of notes) console.log(`  ${note}`);
  if (failures.length) {
    console.log(`FAIL live-deploy-smoke ${args.url}`);
    for (const failure of failures) console.log(`  ! ${failure}`);
    return 1;
  }
  console.log(`PASS live-deploy-smoke ${args.url}`);
  return 0;
}

main().then(
  (code) => process.exit(code),
  (error) => {
    console.error(error);
    process.exit(1);
  }
);
