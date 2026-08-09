#!/usr/bin/env python3
"""Story VO for the Cinder Court beats, via the Higgsfield CLI.

Directive: user 2026-08-09 asked for 연출용 영상·음성·사운드. This AMENDS the
2026-08-04 "SFX + BGM only, NO voice narration" rule for STORY NARRATION only —
cue-* sound effects still forbid vocals (see gen_sfx.py), because a voice baked
into a combat one-shot cannot be muted, ducked or translated separately.

Why Higgsfield and not ElevenLabs (which every other audio asset here uses):
the committed ElevenLabs key returns HTTP 401 as of 2026-08-09 — the SFX/BGM
pipeline is dead until it is rotated. Higgsfield is the operator's newly
provisioned tool and its qwen_audio_tts model lists `ko` as a first-class
language. When the ElevenLabs key comes back, SFX/BGM stay there; VO can stay
here. Recorded as a §3 table row.

Line source: LINES below are transcribed from Assets/Scripts/View/StoryCatalog.cs
(frozen catalog). Speaker->voice mapping mirrors StoryCatalog.VoiceOf so a boss
taunt never comes out in the narrator's voice.

Output: Assets/Resources/Audio/vo-<storyKey>-<beatKind>.mp3, which is exactly
the key AudioDirector.PlayVoice builds (GameDirector.VoiceKey). A beat with no
file stays silent — the line is already on screen as text, so VO is
reinforcement and never the only carrier of a beat.

Usage:
    python3 tools/audio/gen_voice.py --list
    python3 tools/audio/gen_voice.py cinder-span-bossEntry [more keys...]
    python3 tools/audio/gen_voice.py --all        # every line in LINES

Incremental by default (CLAUDE.md §4p): an existing non-empty output is
SKIPPED. Nothing here re-generates a file that already shipped.
"""
import argparse
import json
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT_DIR = REPO / "Assets/Resources/Audio"
PROVENANCE = REPO / "docs/provenance/voice.json"

MODEL = "inworld_text_to_speech"

# Voice per speaker CLASS, mirroring StoryCatalog.VoiceOf.
#
# Model choice is forced, not preferred. qwen_audio_tts advertises `ko` and
# takes a voice_id from `higgsfield voices list`, but every id in that list is
# rejected at generate time with "Voice preset is not available for Qwen Audio"
# [OBSERVED 2026-08-09] — the 114-entry catalog and the model's voice_id
# parameter are two different namespaces. inworld_text_to_speech carries its
# own enum instead, and it holds exactly four Korean voices:
#   Hyunwoo (ko), Seojun (ko)  — male
#   Minji (ko),   Yoona (ko)   — female
# Casting inside that set: the boss gets the heavier male read, the Dusk Warden
# the younger male so the two never sound like one person, and the watcher a
# female voice so narration is instantly separable from anyone in the fight.
VOICES = {
    "Ambient": ("Yoona (ko)",
                "watcher narration — detached, outside the fight"),
    "Boss":    ("Hyunwoo (ko)",
                "boss taunts — the heavier male read"),
    "Warden":  ("Seojun (ko)",
                "Dusk Warden retrospectives — separable from the boss"),
}

# inworld exposes ONLY prompt + voice: no rate, pitch or style knobs (verified
# against `higgsfield model get inworld_text_to_speech`). Delivery therefore
# has to live in the text itself, which is why nothing here rewrites a line —
# the frozen StoryCatalog string is what gets spoken.
# Flagship beats only: the arc's first stage, its two escalations, and the
# campaign's final boss. Adding a row here is additive; nothing else changes.
LINES = [
    # The watcher's stage opening — the one Ambient beat, and the most
    # "연출용" moment in the game: it plays over the loading cutscene before
    # any combat. Wired from GameDirector.EnterStage, not DispatchStory.
    ("cinder-span", "stageStart", "Ambient",
     "서쪽 불씨를 버티고 사슬의 진실을 확인하세요."),
    ("cinder-span", "bossEntry", "Boss",
     "등불을 내려라. 네가 찾는 길은 내 사슬 아래서 끝난다."),
    ("cinder-span", "bossPhase3", "Boss",
     "사슬이 끊긴다면… 다리도, 너도, 나와 함께 재가 된다!"),
    ("cinder-span", "completion", "Warden",
     "그는 문을 지킨 게 아니었다. 문이 올라오지 못하게 묶고 있었다."),
    ("abyss-chancel", "bossEntry", "Boss",
     "또 같은 등불, 또 같은 서약."),
    ("echo-throne", "bossEntry", "Boss",
     "마침내 내가 놓았던 등불을 네가 들고 왔다."),
    ("echo-throne", "bossPhase3", "Boss",
     "왕좌가 무너져도 마지막 명령은 남는다 — 꿇어라!"),
    ("echo-throne", "completion", "Warden",
     "왕좌는 비었다. 그런데 명령은 내 등불 안에서 계속된다."),
]


def hf(*args, retries=4):
    """Run the Higgsfield CLI and return parsed JSON stdout.

    Retries transient transport failures. OBSERVED 2026-08-09: the API
    intermittently answers `request failed (no response received)` and the CLI
    appends `Hint: Run: hf auth login`, which is misleading — the session is
    fine and the next call succeeds. Dying on the first blip would abandon a
    job that is already generating and already paid for.
    """
    last = ""
    for attempt in range(retries):
        proc = subprocess.run(
            ["higgsfield", *args, "--json"], capture_output=True, text=True)
        out = proc.stdout.strip()
        if proc.returncode == 0:
            try:
                return json.loads(out)
            except json.JSONDecodeError:
                last = f"non-JSON: {out[:300]}"
        else:
            last = (proc.stderr or out).strip()
            if "no response received" not in last:
                raise SystemExit(
                    f"FATAL: higgsfield {' '.join(args[:2])} failed\n{last}")
        if attempt < retries - 1:
            delay = 2 ** attempt
            print(f"    transient CLI failure, retrying in {delay}s: {last[:90]}")
            time.sleep(delay)
    raise SystemExit(f"FATAL: higgsfield {' '.join(args[:2])} failed after {retries} tries\n{last}")


def generate(story_key, beat, speaker_class, text):
    voice, _ = VOICES[speaker_class]
    created = hf(
        "generate", "create", MODEL,
        "--prompt", text,
        "--voice", voice,
    )
    job_id = created[0] if isinstance(created, list) else created.get("id")
    if not job_id:
        raise SystemExit(f"FATAL: no job id for {story_key}-{beat}: {created}")

    # Poll rather than trusting one wait: the CLI's wait returns the record,
    # but a queued job can come back without a url on the first call.
    for _ in range(60):
        done = hf("generate", "wait", job_id)
        if done.get("status") == "completed" and done.get("result_url"):
            return job_id, done["result_url"], voice
        if done.get("status") in ("failed", "cancelled"):
            raise SystemExit(f"FATAL: job {job_id} {done.get('status')}")
        time.sleep(3)
    raise SystemExit(f"FATAL: job {job_id} never completed")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("keys", nargs="*", help="<storyKey>-<beatKind> to generate")
    ap.add_argument("--all", action="store_true", help="generate every line")
    ap.add_argument("--list", action="store_true", help="print the line table")
    ap.add_argument("--force", action="store_true",
                    help="regenerate even when the output exists")
    args = ap.parse_args()

    table = {f"{s}-{b}": (s, b, c, t) for s, b, c, t in LINES}

    if args.list:
        for key, (_, _, cls, text) in table.items():
            exists = (OUT_DIR / f"vo-{key}.mp3").exists()
            print(f"{'[x]' if exists else '[ ]'} {key:32} {cls:8} {text}")
        return

    wanted = list(table) if args.all else args.keys
    if not wanted:
        raise SystemExit("nothing to do: pass keys, --all, or --list")
    unknown = [k for k in wanted if k not in table]
    if unknown:
        raise SystemExit(f"FATAL: unknown keys {unknown}\nknown: {list(table)}")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    records = []
    for key in wanted:
        story_key, beat, cls, text = table[key]
        out = OUT_DIR / f"vo-{key}.mp3"
        if out.exists() and out.stat().st_size > 0 and not args.force:
            print(f"SKIP {out.relative_to(REPO)} (exists)")
            continue
        print(f"=== {key}  [{cls}] {text}")
        job_id, url, voice_name = generate(story_key, beat, cls, text)
        with urllib.request.urlopen(url) as resp:
            data = resp.read()
        if len(data) < 2000:
            raise SystemExit(f"FATAL: {key} came back {len(data)} bytes — too small to be speech")
        out.write_bytes(data)
        print(f"    wrote {out.relative_to(REPO)} ({len(data)} bytes, voice {voice_name})")
        records.append({
            "file": str(out.relative_to(REPO)),
            "storyKey": story_key,
            "beat": beat,
            "speakerClass": cls,
            "text": text,
            "voice": voice_name,
            "model": MODEL,
            "jobId": job_id,
            "bytes": len(data),
        })

    if records:
        existing = {}
        if PROVENANCE.exists():
            existing = json.loads(PROVENANCE.read_text(encoding="utf-8"))
        outputs = {o["file"]: o for o in existing.get("outputs", [])}
        for rec in records:
            outputs[rec["file"]] = rec
        doc = {
            "asset": "story VO (narration for StoryCatalog beats)",
            "date": "2026-08-09",
            "directive": (
                "User 2026-08-09 asked for 연출용 영상·음성·사운드, amending the "
                "2026-08-04 'SFX + BGM only, NO voice narration' rule. The "
                "amendment covers STORY NARRATION only; cue-* sound effects "
                "still forbid vocals."
            ),
            "tool": "Higgsfield CLI @higgsfield/cli 1.1.23, model qwen_audio_tts",
            "whyNotElevenLabs": (
                "The committed ELEVENLABS_API_KEY returned HTTP 401 on "
                "2026-08-09, so the existing SFX/BGM pipeline could not be "
                "used. Higgsfield's qwen_audio_tts lists ko as a supported "
                "language and was provisioned by the operator the same day."
            ),
            "generator": "tools/audio/gen_voice.py (incremental; existing files are skipped)",
            "lineSource": "Assets/Scripts/View/StoryCatalog.cs (frozen catalog — transcribed, never rewritten)",
            "speakerMapping": {
                cls: {"voice": voice, "role": role}
                for cls, (voice, role) in VOICES.items()
            },
            "consumer": (
                "AudioDirector.PlayVoice(key) loads Audio/vo-{key}; "
                "GameDirector.DispatchStory builds key = storyKey + '-' + beatKind "
                "beside the _speech.Show that renders the same line as text."
            ),
            "outputs": list(outputs.values()),
        }
        PROVENANCE.write_text(
            json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"provenance -> {PROVENANCE.relative_to(REPO)}")


if __name__ == "__main__":
    main()
