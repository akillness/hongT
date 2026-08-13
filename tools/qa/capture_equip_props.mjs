#!/usr/bin/env node
// Equip props in a real dungeon frame, at both bands, for the toon conversion.
//
// WHY A DEDICATED CAPTURE. Everything else in this change is measured on assets:
// UV spread, characteristic thickness, sheet luminance and contrast, material
// properties on disk. None of that looks at a rendered pixel, and the actual
// deliverable is "the props read as textured, cel-shaded, and rank-legible".
// The nearest failure mode has already happened once in this repo — a sheet that
// measured a correct mean while carrying five usable levels of pattern is
// arithmetically fine and visually the flat tint it replaced.
//
// WHY TWO SAVES AND NOT ONE. Band is the whole point of the emission term, and a
// single frame cannot show a difference. The two runs are identical except for
// equipment tier: T2 -> basic, T4 -> fine (ActorView.AttachEquipProps maps
// tier->band, EquipPropTests pins the mapping). Anything that differs between
// the two frames is the band, because nothing else moved.
//
// WHY NOT THE PROLOGUE. Props attach from the campaign save's equipment tiers,
// and the prologue runs before equipment exists — a prologue frame would show a
// bare character and read as "the props are missing".
//
// Usage:
//   SMOKE_URL=http://127.0.0.1:8788/ node tools/qa/capture_equip_props.mjs
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..", "..");
const URL_ = process.env.SMOKE_URL || "http://127.0.0.1:8788/";
const OUT = path.join(ROOT, "_workspace/current/qa/equip-props");
const VIEW = { width: 1440, height: 900 };

// Tier 2 -> basic band, tier 4 -> fine band. Everything else is held equal so
// the only difference between the two captures is the band.
const save = (tier) => JSON.stringify({
  clearedMask: 0,
  equipment: { weapon: tier, lantern: tier, cloak: tier },
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

async function captureBand(browser, tier, label) {
  // deviceScaleFactor 3: Unity WebGL renders at devicePixelRatio, so this is
  // the one knob that resolves a 0.42 m dagger without moving the shipped
  // camera. Framing, FOV and distance stay exactly as a player sees them —
  // only the pixel count under that framing changes.
  const context = await browser.newContext({ viewport: VIEW, deviceScaleFactor: 3 });
  await context.addInitScript((s) => {
    localStorage.setItem("abyssal-lantern:unity:campaign", s);
  }, save(tier));
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

  const probe = path.join(OUT, `probe-${label}.png`);
  await page.screenshot({ path: probe });
  const { execFileSync } = await import("node:child_process");
  const cta = JSON.parse(execFileSync("python3",
    [path.join(ROOT, "tools/qa/find_gold_button.py"), probe,
     String(VIEW.width), String(VIEW.height)], { encoding: "utf8" }));
  if (!cta.found) throw new Error(`${label}: CTA not found — cannot reach a dungeon`);

  await page.mouse.move(cta.cx, cta.cy);
  await sleep(120); await page.mouse.down(); await sleep(90); await page.mouse.up();
  // Past the wave banner before touching anything: the opening seconds are the
  // banner's.
  await sleep(7000);

  // Clear the first-run guidance cards. They pause at timeScale 0 and sit over
  // the middle of the screen — exactly where the character wearing the props
  // is — so a frame taken before they are dismissed shows a card, not a prop.
  // Space is also the strike key, so the surplus presses land as attacks once
  // the cards are gone; that helps, since a swing carries the weapon away from
  // the body where it is easiest to see.
  for (let i = 0; i < 12; i += 1) {
    await page.keyboard.press("Space");
    await sleep(600);
  }
  await sleep(1000);
  await page.screenshot({ path: path.join(OUT, `${label}-dungeon.png`) });

  // Does the canvas actually gain pixels from deviceScaleFactor? Unity's WebGL
  // template may size its drawing buffer from devicePixelRatio or pin it to the
  // CSS size. If it pins, a "closeup" is an upscale carrying no new detail, and
  // a flat-looking prop would be indistinguishable from a prop rendered at too
  // few pixels to judge. Record it so the frame can be read honestly.
  const canvas = await page.evaluate(() => {
    const c = document.querySelector("canvas");
    return c ? { backing: c.width, css: c.clientWidth, dpr: window.devicePixelRatio } : null;
  });

  // The shipped camera frames the whole arena, so a 0.42 m dagger lands at a
  // few pixels — an honest viewing distance, and useless for deciding whether
  // the sheet reads as a texture. Crop the player band at full device scale:
  // the game's framing, FOV and distance are untouched, only the pixel count
  // under that framing changes. No `scale: "css"` — that would opt out of the
  // device scaling and hand back the same pixels in a smaller file.
  //
  // Sampled across a swing rather than once: an attack carries the weapon away
  // from the body where it is easiest to see, but a single blind shot lands at
  // an arbitrary phase and can catch the blade edge-on or mid-blur.
  const clip = { x: VIEW.width * 0.30, y: VIEW.height * 0.34,
                 width: VIEW.width * 0.40, height: VIEW.height * 0.42 };
  for (let shot = 0; shot < 5; shot += 1) {
    await page.keyboard.press("Space");
    await sleep(200);
    await page.screenshot({
      path: path.join(OUT, `${label}-closeup-${shot}.png`),
      clip,
    });
  }
  const status = (await observe(page))?.status ?? null;
  await context.close();
  return { label, tier, status, canvas, pageErrors };
}

fs.mkdirSync(OUT, { recursive: true });
const browser = await chromium().launch();
const results = [];
for (const [tier, label] of [[2, "basic"], [4, "fine"]]) {
  results.push(await captureBand(browser, tier, label));
}
await browser.close();
fs.writeFileSync(path.join(OUT, "report.json"), JSON.stringify(results, null, 2));
for (const r of results) {
  console.log(`${r.label} (tier ${r.tier}) status=${JSON.stringify(r.status?.reason)} `
    + `errors=${r.pageErrors.length}`);
}
console.log(`artifacts: ${path.relative(ROOT, OUT)}`);
