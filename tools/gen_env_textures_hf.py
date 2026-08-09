#!/usr/bin/env python3
"""Re-seam environment albedo textures via Higgsfield nano_banana_flash.

Why this exists beside tools/gen_env_textures.sh rather than replacing it:
the shell script's tool (god-tibo-imagen / codex-cli) still works for fresh
concepts, but it has two properties that matter here.

  1. It ignores --size and returns ~1254px, above the 1024 WebGL ceiling
     (EnvTextureImportPipeline clamps on import, so every texture ships
     downscaled from a size nobody asked for).
  2. It cannot take a reference image, so "regenerate THIS texture but make the
     edges wrap" is not expressible — the same limitation that got intro beat 6
     cut (docs/provenance/intro-video.json frames.provider_findings).

MEASURED PROBLEM. A tiling texture must match left<->right and top<->bottom
edges or the seam repeats across every wall — and EnvironmentBuilder tiles at
1 tile = 1.28 world units, so a dungeon wall shows many tiles at once. Mean
per-channel edge delta over the 18 shipped maps (256px sample):

    ember-gallery-stone  26.9      <- worst
    abyss-chancel-stone  22.5
    ash-verdict-floor    20.1
    cinder-sluice-floor  19.8
    ...the other 14      4.3-11.2  <- fine

Four are outliers by a factor of ~3 against the rest of their own set. Those
four are what this script regenerates; the other fourteen are left alone
(CLAUDE.md §4p: an incremental generator must not rewrite whole matter).

Prompts are READ from tools/gen_env_textures.sh rather than restated, so the
two generators cannot drift into describing different stages (§4i).

Usage:
    python3 tools/gen_env_textures_hf.py --list
    python3 tools/gen_env_textures_hf.py ember-gallery-stone
    python3 tools/gen_env_textures_hf.py --all-seamed
"""
import argparse
import json
import re
import subprocess
import time
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SHELL = REPO / "tools/gen_env_textures.sh"
OUT_DIR = REPO / "Assets/Resources/Textures/Env"
PROVENANCE = REPO / "docs/provenance/env-textures-reseam.json"
MODEL = "nano_banana_flash"

# Measured 2026-08-09; regenerate this list with the audit in the docstring.
SEAMED = {
    "ember-gallery-stone": 26.9,
    "abyss-chancel-stone": 22.5,
    "ash-verdict-floor": 20.1,
    "cinder-sluice-floor": 19.8,
}

# The shell script's shared clause already demands wrapping. It did not get it,
# so this adds an explicit, checkable instruction on top rather than repeating
# the same words louder.
WRAP_CLAUSE = (
    " CRITICAL: the image must tile seamlessly. The left edge must continue "
    "exactly into the right edge and the top edge exactly into the bottom edge, "
    "as if the image repeats infinitely in a grid. No feature may be cut off at "
    "a border, no vignette, no darkening or lightening toward any edge, "
    "perfectly uniform brightness corner to corner."
)


def prompts_from_shell():
    """Parse the STAGES table — the single source of truth for stage concepts."""
    text = SHELL.read_text(encoding="utf-8")
    common = re.search(r'COMMON="([^"]+)"', text).group(1)
    table = re.search(r"STAGES=\((.*?)\n\)", text, re.S).group(1)
    out = {}
    for line in table.strip().splitlines():
        line = line.strip().strip('"')
        if not line or "|" not in line:
            continue
        stage, stone, floor = line.split("|")
        out[f"{stage}-stone"] = f"{stone}, {common}"
        out[f"{stage}-floor"] = f"{floor}, {common}"
    return out


def hf(*args, retries=4):
    last = ""
    for attempt in range(retries):
        proc = subprocess.run(["higgsfield", *args, "--json"],
                              capture_output=True, text=True)
        out = proc.stdout.strip()
        if proc.returncode == 0:
            try:
                return json.loads(out)
            except json.JSONDecodeError:
                last = f"non-JSON: {out[:300]}"
        else:
            last = (proc.stderr or out).strip()
            if "no response received" not in last:
                raise SystemExit(f"FATAL: higgsfield {' '.join(args[:2])}\n{last}")
        if attempt < retries - 1:
            time.sleep(2 ** attempt)
    raise SystemExit(f"FATAL: higgsfield {' '.join(args[:2])} after {retries}\n{last}")


def seam_delta(path):
    """Mean per-channel edge mismatch, the metric that selected these four."""
    raw = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-vf",
         "scale=256:256,format=rgb24", "-f", "rawvideo", "-"],
        capture_output=True).stdout
    w = h = 256
    px = lambda x, y: raw[(y * w + x) * 3:(y * w + x) * 3 + 3]
    lr = sum(sum(abs(a - b) for a, b in zip(px(0, y), px(w - 1, y)))
             for y in range(h)) / (h * 3)
    tb = sum(sum(abs(a - b) for a, b in zip(px(x, 0), px(x, h - 1)))
             for x in range(w)) / (w * 3)
    return max(lr, tb)


def generate(name, prompt):
    created = hf("generate", "create", MODEL,
                 "--prompt", prompt + WRAP_CLAUSE,
                 "--aspect-ratio", "1:1", "--resolution", "1k")
    job_id = created[0] if isinstance(created, list) else created.get("id")
    if not job_id:
        raise SystemExit(f"FATAL: no job id for {name}: {created}")
    for _ in range(60):
        done = hf("generate", "wait", job_id)
        if done.get("status") == "completed" and done.get("result_url"):
            return job_id, done["result_url"]
        if done.get("status") in ("failed", "cancelled"):
            raise SystemExit(f"FATAL: job {job_id} {done.get('status')}")
        time.sleep(3)
    raise SystemExit(f"FATAL: job {job_id} never completed")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*")
    ap.add_argument("--all-seamed", action="store_true")
    ap.add_argument("--list", action="store_true")
    args = ap.parse_args()
    prompts = prompts_from_shell()

    if args.list:
        for name, before in SEAMED.items():
            p = OUT_DIR / f"{name}.png"
            now = seam_delta(p) if p.exists() else float("nan")
            print(f"{name:24} recorded {before:5.1f}  now {now:5.1f}")
        return

    wanted = list(SEAMED) if args.all_seamed else args.names
    if not wanted:
        raise SystemExit("nothing to do: pass names, --all-seamed, or --list")
    unknown = [n for n in wanted if n not in prompts]
    if unknown:
        raise SystemExit(f"FATAL: no prompt for {unknown}")

    records = []
    for name in wanted:
        out = OUT_DIR / f"{name}.png"
        before = seam_delta(out) if out.exists() else None
        print(f"=== {name}  (seam {before:.1f} -> ?)" if before else f"=== {name}")
        job_id, url = generate(name, prompts[name])
        with urllib.request.urlopen(url) as resp:
            data = resp.read()
        # Write to a temp path and KEEP the original until the new one is
        # measured: a regeneration that seams worse must not silently ship.
        tmp = out.with_suffix(".hf.png")
        tmp.write_bytes(data)
        after = seam_delta(tmp)
        if before is not None and after >= before:
            tmp.unlink()
            print(f"    REJECTED: seam {after:.1f} is not better than {before:.1f} — kept original")
            records.append({"texture": name, "jobId": job_id, "accepted": False,
                            "seamBefore": round(before, 1), "seamAfter": round(after, 1)})
            continue
        tmp.replace(out)
        print(f"    accepted: seam {before:.1f} -> {after:.1f}  ({len(data)} bytes)")
        records.append({"texture": name, "jobId": job_id, "accepted": True,
                        "seamBefore": round(before, 1) if before else None,
                        "seamAfter": round(after, 1), "bytes": len(data)})

    PROVENANCE.write_text(json.dumps({
        "asset": "environment albedo re-seam",
        "date": "2026-08-09",
        "why": ("Four of the 18 shipped maps had an edge mismatch ~3x the rest "
                "of their own set, and EnvironmentBuilder tiles at 1 tile = 1.28 "
                "world units, so the seam repeats across every wall."),
        "metric": ("mean per-channel delta between opposing edges of a 256px "
                   "sample; max(left-right, top-bottom). The 14 healthy maps "
                   "measure 4.3-11.2."),
        "tool": f"Higgsfield CLI, model {MODEL}",
        "whyNotGti": ("god-tibo-imagen/codex-cli ignores --size (returns ~1254px "
                      "against a 1024 ceiling) and rejects image input, so "
                      "'regenerate this one but wrap the edges' is not "
                      "expressible on that path."),
        "promptSource": "tools/gen_env_textures.sh STAGES table, read at runtime",
        "acceptanceRule": ("a regeneration is only kept when it measures STRICTLY "
                           "better than the texture it replaces; otherwise the "
                           "original stays"),
        "outputs": records,
    }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"provenance -> {PROVENANCE.relative_to(REPO)}")


if __name__ == "__main__":
    main()
