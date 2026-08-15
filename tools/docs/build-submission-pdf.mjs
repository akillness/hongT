#!/usr/bin/env node
// Render a submission markdown set to PDFs.
//
//   node tools/docs/build-submission-pdf.mjs                 # docs/submission-nhn, 01..03
//   node tools/docs/build-submission-pdf.mjs --only 02
//   node tools/docs/build-submission-pdf.mjs --all           # include 00/04 too
//   node tools/docs/build-submission-pdf.mjs --dir docs/other
//
// Generalized from tools/docs/build-nan2026-pdf.mjs, which stays as-is because
// it is the reproducer for an already-submitted deliverable. The two things a
// naive `pandoc -o out.pdf` gets wrong here are inherited from it:
//   1. Menlo carries no Hangul, so Korean inside code blocks silently vanishes.
//      D2Coding is a Korean-capable monospace face.
//   2. XeLaTeX cannot include an SVG, so diagrams are converted to vector PDF
//      and the references rewritten in a scratch copy — the markdown keeps its
//      SVG links so it still renders on GitHub.
//
// Which files are SUBMITTED is a property of the set, not of this script: only
// the numbered deliverables (01..03) build by default. 00 (index) and 04 (video
// shot list) are working documents and need --all.

import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");

function arg(flag, fallback = null) {
  const i = process.argv.indexOf(flag);
  return i === -1 ? fallback : process.argv[i + 1];
}

const SRC_DIR = path.join(ROOT, arg("--dir", "docs/submission-nhn"));
const ASSET_DIR = path.join(SRC_DIR, "assets");
// The shared LaTeX header lives with the first submission set; a set may
// override it by dropping its own assets/pdf-header.tex.
const FALLBACK_HEADER = path.join(ROOT, "docs", "nan2026", "assets", "pdf-header.tex");
const OUT_DIR = path.join(SRC_DIR, "pdf");
const BUILD_DIR = path.join(SRC_DIR, ".build");

const SUBMITTED = new Set(["01", "02", "03"]);

const MAIN_FONT = process.env.SUBMISSION_PDF_FONT || "Apple SD Gothic Neo";
const MONO_FONT = process.env.SUBMISSION_PDF_MONO || "D2Coding";

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
    return execFileSync("fc-list", [family, "family"], { encoding: "utf8" }).trim().length > 0;
  } catch {
    return false;
  }
}

function discover() {
  return fs.readdirSync(SRC_DIR)
    .filter((n) => /^\d\d-.*\.md$/.test(n))
    .sort()
    .map((file) => ({ id: file.slice(0, 2), file }));
}

function convertDiagrams() {
  if (!fs.existsSync(ASSET_DIR)) return [];
  const converted = [];
  for (const name of fs.readdirSync(ASSET_DIR)) {
    if (!name.endsWith(".svg")) continue;
    const pdf = path.join(BUILD_DIR, name.replace(/\.svg$/, ".pdf"));
    execFileSync("rsvg-convert", ["-f", "pdf", "-o", pdf, path.join(ASSET_DIR, name)], { stdio: "pipe" });
    converted.push(path.basename(pdf));
  }
  return converted;
}

function build({ id, file }) {
  const src = path.join(SRC_DIR, file);
  const scratch = path.join(BUILD_DIR, file);
  fs.writeFileSync(scratch, fs.readFileSync(src, "utf8").replace(/assets\/([\w-]+)\.svg/g, "$1.pdf"));

  const out = path.join(OUT_DIR, file.replace(/\.md$/, ".pdf"));
  const header = fs.existsSync(path.join(ASSET_DIR, "pdf-header.tex"))
    ? path.join(ASSET_DIR, "pdf-header.tex")
    : FALLBACK_HEADER;
  const args = [
    scratch,
    "-o", out,
    "--pdf-engine=xelatex",
    "--from=markdown+yaml_metadata_block+pipe_tables+backtick_code_blocks+implicit_figures",
    "--resource-path", [BUILD_DIR, SRC_DIR].join(path.delimiter),
  ];
  if (fs.existsSync(header)) args.push("-H", header);
  args.push(
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
  );
  execFileSync("pandoc", args, { stdio: "inherit", cwd: ROOT });
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
      "  or override with SUBMISSION_PDF_MONO=<family>",
    );
    process.exit(1);
  }
  if (!fs.existsSync(SRC_DIR)) {
    console.error(`source directory not found: ${path.relative(ROOT, SRC_DIR)}`);
    process.exit(1);
  }

  const only = arg("--only");
  const all = process.argv.includes("--all");

  fs.rmSync(BUILD_DIR, { recursive: true, force: true });
  fs.mkdirSync(BUILD_DIR, { recursive: true });
  fs.mkdirSync(OUT_DIR, { recursive: true });

  const diagrams = convertDiagrams();
  const built = [];
  const skipped = [];
  for (const doc of discover()) {
    if (only ? doc.id !== only : !all && !SUBMITTED.has(doc.id)) {
      skipped.push(doc.file);
      continue;
    }
    built.push(build(doc));
  }
  fs.rmSync(BUILD_DIR, { recursive: true, force: true });

  console.log(JSON.stringify({
    dir: path.relative(ROOT, SRC_DIR),
    mainFont: MAIN_FONT,
    monoFont: MONO_FONT,
    diagrams,
    built,
    skipped,
  }, null, 2));
}

main();
