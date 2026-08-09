#!/usr/bin/env python3
"""The nine dungeon-kit SFX cues, via Higgsfield mirelo_text_to_audio.

Until now these nine events had no sound of their own. AudioDirector.OnEvents
voiced them by replaying one of the base eight clips at a different volume and
called it an "interim contract" in a code comment. The worst of them was
BossPhase2 = cue-gameover at 0.35: a DEFEAT sting on the boss's escalation,
telling the player they were losing at the moment the fight got harder.

Why not gen_sfx.py (ElevenLabs), which produced every other cue: that key
returns HTTP 401 as of 2026-08-09 [OBSERVED]. This script is the same shape —
same incremental skip rule, same "no voice inside an effect" prompt discipline,
same provenance write — pointed at the tool that works.

Timbre note, stated up front because it is the real risk: these nine come from
a different vendor than the eight they sit beside. mirelo is a text-to-audio
model, not the ElevenLabs sound-generation endpoint, so the prompts below lean
hard on the SAME vocabulary the originals used (dry, one-shot, no tail, dark
fantasy, arcade confirm) to keep the family recognisable. Any cue that lands
wrong is a one-line delete away from falling back to its interim mapping,
because AudioDirector.PlayOrFallback keeps the old pairing alive.

Usage:
    python3 tools/audio/gen_dungeon_cues.py --list
    python3 tools/audio/gen_dungeon_cues.py dash bolt
    python3 tools/audio/gen_dungeon_cues.py --all

Incremental (CLAUDE.md §4p): an existing non-empty output is SKIPPED.
"""
import argparse
import json
import subprocess
import time
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT_DIR = REPO / "Assets/Resources/Audio"
PROVENANCE = REPO / "docs/provenance/dungeon-cues.json"
MODEL = "mirelo_text_to_audio"

# name: (seconds, prompt, event it voices, interim fallback it replaces)
CUES = {
    "dash": (0.6,
             "Short sharp cloth-and-air dash whoosh, quick lateral movement "
             "swipe with a faint ember trail hiss, dry, no reverb tail, dark "
             "fantasy arcade one-shot, no voice, no music",
             "SimEvents.DashUsed", "cue-strike @ 0.5"),
    "bolt": (0.7,
             "Quick violet arcane bolt release, tight electric snap into a "
             "short crackling whistle, piercing but small, dry, no tail, dark "
             "fantasy spell one-shot, no voice, no music",
             "SimEvents.BoltCast", "cue-nova @ 0.45"),
    "pulse": (1.1,
             "Low green resonance pulse spreading across stone ground, soft "
             "sub thump followed by a ringing harmonic wash, grave field "
             "resonance, dark fantasy spell, no voice, no music",
             "SimEvents.PulseCast", "cue-ward @ 0.6"),
    "levelup": (1.3,
                "Warm ascending level-up chime, three quick rising bell tones "
                "over a soft golden swell, triumphant but brief, dark fantasy "
                "arcade progression confirm, no voice, no music",
                "SimEvents.LevelUp", "cue-pickup + cue-wave @ 0.4"),
    "elite-down": (1.2,
                   "Heavy elite enemy defeat impact, deep armored collapse "
                   "thud with a metallic ring and a short ash crackle tail, "
                   "weightier than a common kill, dark fantasy, no voice, no music",
                   "SimEvents.EliteDown", "cue-kill @ 1.0"),
    "extraction": (1.5,
                   "Spectral extraction complete, rising cyan shimmer drawn "
                   "upward into a soft glassy resolve, essence siphon finish, "
                   "dark fantasy magic, no voice, no music",
                   "SimEvents.ExtractionComplete", "cue-ward @ 0.9"),
    "boss-phase2": (2.0,
                    "Boss escalation sting, low brass-like growl swelling "
                    "upward into a hard dark hit, threatening and rising, the "
                    "sound of a fight getting WORSE not ending, dark fantasy, "
                    "no voice, no music",
                    "SimEvents.BossPhase2", "cue-gameover @ 0.35 (a DEFEAT sound)"),
    "combo-finisher": (0.9,
                       "Gold-tinged finishing blow, crisp heavy impact with a "
                       "bright metallic ring on top, decisive combo ender, "
                       "dry, tight tail, dark fantasy arcade, no voice, no music",
                       "SimEvents.ComboFinisher", "cue-kill @ 0.7"),
    "boss-spawned": (2.2,
                     "Boss arrival stinger, deep descending horn blast with a "
                     "stone-grinding rumble underneath, huge and ominous, "
                     "announces something large entering the arena, dark "
                     "fantasy, no voice, no music",
                     "SimEvents.BossSpawned", "cue-wave @ 0.9"),
}


def hf(*args, retries=4):
    """Higgsfield CLI -> parsed JSON, retrying transient transport failures.

    OBSERVED 2026-08-09: the API intermittently answers 'request failed (no
    response received)' with a misleading 'hf auth login' hint; the next call
    succeeds. Dying on the first blip abandons a job already paid for.
    """
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
            delay = 2 ** attempt
            print(f"    transient CLI failure, retrying in {delay}s")
            time.sleep(delay)
    raise SystemExit(f"FATAL: higgsfield {' '.join(args[:2])} after {retries} tries\n{last}")


def generate(name, seconds, prompt):
    created = hf("generate", "create", MODEL,
                 "--prompt", prompt, "--duration", str(seconds))
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
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()

    if args.list:
        for name, (sec, _, event, fallback) in CUES.items():
            exists = (OUT_DIR / f"cue-{name}.mp3").exists()
            print(f"{'[x]' if exists else '[ ]'} cue-{name:16} {sec:4}s  "
                  f"{event:32} was: {fallback}")
        return

    wanted = list(CUES) if args.all else args.names
    if not wanted:
        raise SystemExit("nothing to do: pass names, --all, or --list")
    unknown = [n for n in wanted if n not in CUES]
    if unknown:
        raise SystemExit(f"FATAL: unknown cues {unknown}\nknown: {list(CUES)}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    records = []
    for name in wanted:
        seconds, prompt, event, fallback = CUES[name]
        out = OUT_DIR / f"cue-{name}.mp3"
        if out.exists() and out.stat().st_size > 0 and not args.force:
            print(f"SKIP {out.relative_to(REPO)} (exists)")
            continue
        print(f"=== cue-{name} ({seconds}s)  voices {event}")
        job_id, url = generate(name, seconds, prompt)
        with urllib.request.urlopen(url) as resp:
            data = resp.read()
        if len(data) < 1000:
            raise SystemExit(f"FATAL: cue-{name} came back {len(data)} bytes")
        out.write_bytes(data)
        print(f"    wrote {out.relative_to(REPO)} ({len(data)} bytes)")
        records.append({
            "file": str(out.relative_to(REPO)), "cue": f"cue-{name}",
            "seconds": seconds, "prompt": prompt, "event": event,
            "replacedInterim": fallback, "model": MODEL, "jobId": job_id,
            "bytes": len(data),
        })

    if records:
        existing = json.loads(PROVENANCE.read_text(encoding="utf-8")) \
            if PROVENANCE.exists() else {}
        outputs = {o["file"]: o for o in existing.get("outputs", [])}
        for rec in records:
            outputs[rec["file"]] = rec
        PROVENANCE.write_text(json.dumps({
            "asset": "dungeon-kit SFX cues",
            "date": "2026-08-09",
            "why": ("Nine SimEvents had no dedicated sound and were voiced by "
                    "replaying a base clip at another volume — self-labelled an "
                    "'interim contract' in AudioDirector.OnEvents. BossPhase2 "
                    "was the worst: cue-gameover at 0.35, a defeat sting on the "
                    "boss's escalation."),
            "tool": "Higgsfield CLI @higgsfield/cli 1.1.23, model mirelo_text_to_audio",
            "whyNotElevenLabs": ("The committed ELEVENLABS_API_KEY returns HTTP "
                                 "401 as of 2026-08-09, so gen_sfx.py cannot run."),
            "timbreRisk": ("These nine come from a different vendor than the "
                           "eight beside them. Prompts reuse the original "
                           "vocabulary to keep the family recognisable, and "
                           "AudioDirector.PlayOrFallback keeps every interim "
                           "mapping alive, so deleting a bad cue restores the "
                           "previous behaviour with no code change."),
            "generator": "tools/audio/gen_dungeon_cues.py (incremental)",
            "outputs": list(outputs.values()),
        }, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"provenance -> {PROVENANCE.relative_to(REPO)}")


if __name__ == "__main__":
    main()
