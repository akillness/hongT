#!/usr/bin/env node
// Render the NAN 2026 pre-task markdown deliverables to submission PDFs.
//
//   node tools/docs/build-nan2026-pdf.mjs [--only 02]
//
// Requires pandoc, a XeLaTeX toolchain, and rsvg-convert.
//
// Two things the naive `pandoc -o out.pdf` invocation gets wrong here:
//   1. Menlo carries no Hangul, so every Korean glyph inside a code block or
//      inline code silently disappears. D2Coding is a Korean-capable monospace
//      face, so code blocks keep their text.
//   2. XeLaTeX cannot include an SVG. Each diagram is converted to a vector PDF
//      first and the image references are rewritten in a scratch copy of the
//      markdown, so the figures stay resolution independent.

import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const SRC_DIR = path.join(ROOT, "docs", "nan2026");
const ASSET_DIR = path.join(SRC_DIR, "assets");
const OUT_DIR = path.join(SRC_DIR, "pdf");
const BUILD_DIR = path.join(SRC_DIR, ".build");

const DOCS = [
  { id: "01", file: "01-game-overview.md" },
  { id: "02", file: "02-ai-tech.md" },
  { id: "03", file: "03-team-roles.md" },
];

// Apple SD Gothic Neo ships with macOS; D2Coding is the Korean coding face that
// keeps Hangul legible inside fixed-width blocks.
const MAIN_FONT = process.env.NAN_PDF_FONT || "Apple SD Gothic Neo";
const MONO_FONT = process.env.NAN_PDF_MONO || "D2Coding";

function have(binary) {
  try {
    execFileSync("which", [binary], { stdio: "pipe" });
    return true;
  } catch {
    return false;
  }
}

function fontPresent(family) {
  try {
    const out = execFileSync("fc-list", [family, "family"], { encoding: "utf8" });
    return out.trim().length > 0;
  } catch {
    return false;
  }
}

// XeLaTeX has no SVG reader. Convert once per build into vector PDF.
function convertDiagrams() {
  if (!fs.existsSync(ASSET_DIR)) return [];
  const converted = [];
  for (const name of fs.readdirSync(ASSET_DIR)) {
    if (!name.endsWith(".svg")) continue;
    const svg = path.join(ASSET_DIR, name);
    const pdf = path.join(BUILD_DIR, name.replace(/\.svg$/, ".pdf"));
    execFileSync("rsvg-convert", ["-f", "pdf", "-o", pdf, svg], { stdio: "pipe" });
    converted.push(path.basename(pdf));
  }
  return converted;
}

function build({ id, file }) {
  const src = path.join(SRC_DIR, file);
  if (!fs.existsSync(src)) throw new Error(`missing source: ${file}`);

  // Point the figures at the converted vector PDFs without touching the
  // markdown sources, which keep the SVGs so they render on GitHub.
  const scratch = path.join(BUILD_DIR, file);
  const body = fs.readFileSync(src, "utf8").replace(/assets\/([\w-]+)\.svg/g, "$1.pdf");
  fs.writeFileSync(scratch, body);

  const out = path.join(OUT_DIR, file.replace(/\.md$/, ".pdf"));
  execFileSync("pandoc", [
    scratch,
    "-o", out,
    "--pdf-engine=xelatex",
    "--from=markdown+yaml_metadata_block+pipe_tables+backtick_code_blocks+implicit_figures",
    "--resource-path", BUILD_DIR,
    "-V", `mainfont=${MAIN_FONT}`,
    "-V", `sansfont=${MAIN_FONT}`,
    "-V", `monofont=${MONO_FONT}`,
    "-V", "monofontoptions=Scale=0.86",
    "-V", "geometry:a4paper",
    "-V", "geometry:margin=20mm",
    "-V", "fontsize=10pt",
    "-V", "linkcolor=NavyBlue",
    "-V", "urlcolor=NavyBlue",
    "-V", "colorlinks=true",
    "-V", "graphics=true",
    "--highlight-style=tango",
    "--toc",
    "--toc-depth=2",
  ], { stdio: "inherit", cwd: ROOT });

  return { id, out: path.relative(ROOT, out), bytes: fs.statSync(out).size };
}

function main() {
  for (const binary of ["pandoc", "xelatex", "rsvg-convert"]) {
    if (!have(binary)) {
      console.error(`required tool not found: ${binary}`);
      process.exit(1);
    }
  }
  if (!fontPresent(MONO_FONT)) {
    console.error(
      `monospace font not installed: ${MONO_FONT}\n` +
      "  install it with: brew install --cask font-d2coding\n" +
      "  or override with NAN_PDF_MONO=<family>",
    );
    process.exit(1);
  }

  const onlyIndex = process.argv.indexOf("--only");
  const only = onlyIndex === -1 ? null : process.argv[onlyIndex + 1];

  fs.rmSync(BUILD_DIR, { recursive: true, force: true });
  fs.mkdirSync(BUILD_DIR, { recursive: true });
  fs.mkdirSync(OUT_DIR, { recursive: true });

  const diagrams = convertDiagrams();
  const built = [];
  for (const doc of DOCS) {
    if (only && doc.id !== only) continue;
    built.push(build(doc));
  }
  fs.rmSync(BUILD_DIR, { recursive: true, force: true });

  console.log(JSON.stringify({
    mainFont: MAIN_FONT,
    monoFont: MONO_FONT,
    diagrams,
    built,
  }, null, 2));
}

main();
