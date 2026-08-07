# Cinder Court BGM via ElevenLabs Music API (v1/music).
# D7 decision (_workspace/current/intake/deep-interview-seed-ui-vfx-flow.md §5):
# the sound-generation endpoint (see gen_sfx.py) hard-caps at 22 s, unsuitable
# for BGM loops. Music API allows 3000-600000 ms and is purpose-built for
# scored/instrumental beds, so BGM gets its own script and its own endpoint.
#
# Key resolution order: $ELEVENLABS_API_KEY, then ../Abyssal-Surge/.env.game-audio
# (read-only; never copied into this repo). Output: Assets/Resources/Audio/bgm-<name>.mp3
# plus docs/provenance/bgm.json recording prompt + endpoint per track.
#
# Fallback: if the API is unreachable / quota-blocked / non-2xx, the script
# fails loudly. It does NOT silently substitute procedural audio; per D7 the
# fallback is a manual audit of ../Abyssal-Surge/audio/elevenlabs/loops/
# candidates (Abyssal-Surge is read-only — never copy without checking that
# repo's own retain/manifest status first).
import json
import os
import sys
import time
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT_DIR = REPO / "Assets" / "Resources" / "Audio"
PROV = REPO / "docs" / "provenance" / "bgm.json"
ENDPOINT = "https://api.elevenlabs.io/v1/music"


def _find_env_fallback():
    """Walk up from the repo root looking for Abyssal-Surge/.env.game-audio."""
    for ancestor in [REPO, *REPO.parents]:
        candidate = ancestor / "Abyssal-Surge" / ".env.game-audio"
        if candidate.exists():
            return candidate
    return None


ENV_FALLBACK = _find_env_fallback()

# Prompt table: dark-fantasy tone (charcoal/ember/spectral-cyan world, per
# CLAUDE.md §3 world tone and docs/SIM_SPEC.md). Flow order per D7/W12:
# intro (boot reel bed) -> lobby (campaign map) -> loading (stage transition)
# -> stage (dungeon/arena combat bed). Durations sized per screen dwell time,
# not just the Music API 3-600 s range: loading is a quick stinger-length
# loop, lobby/stage are longer beds meant to actually loop under play.
TRACKS = {
    "intro": (20000, "Dark fantasy boot-reel theme, slow ember-lantern ignition "
              "swell, low brooding string drone rising into a distant choir "
              "pad, restrained cyan-tinged shimmer on the final bar, cinematic "
              "instrumental only, no percussion hits, no vocals"),
    "lobby": (45000, "Dark fantasy command-hall ambient loop, slow charcoal-toned "
              "string bed under a warm ember drone, sparse low harp plucks, "
              "unhurried and contemplative, seamless loop, instrumental only, "
              "no percussion breaks, no vocals"),
    "loading": (15000, "Short tense dark fantasy transition sting loop, rising "
                "sub bass pulse with a cold spectral-cyan synth swell, restrained "
                "and anticipatory, seamless loop, instrumental only, no melody "
                "hooks, no vocals"),
    "stage": (60000, "Dark fantasy dungeon combat ambient bed, driving low ember "
              "drone with a slow ominous two-note pulse, distant deep choir-like "
              "synth pads, smoldering coals crackle texture sparsely, seamless "
              "loop, instrumental only, no melody spikes, no percussion breaks, "
              "no vocals"),
}


def resolve_key():
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if key:
        return key
    if ENV_FALLBACK is not None and ENV_FALLBACK.exists():
        for line in ENV_FALLBACK.read_text(encoding="utf-8").splitlines():
            if line.startswith("ELEVENLABS_API_KEY="):
                return line.split("=", 1)[1].strip().strip('"').strip("'")
    raise SystemExit("FATAL: no ELEVENLABS_API_KEY in env or .env.game-audio")


def generate(key, name, length_ms, prompt, retries=3):
    body = {
        "prompt": prompt,
        "music_length_ms": length_ms,
        "force_instrumental": True,   # user directive 2026-08-04: no vocals
    }
    payload = json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        ENDPOINT, data=payload, method="POST",
        headers={"xi-api-key": key, "Content-Type": "application/json"})
    for attempt in range(1, retries + 1):
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            detail = ""
            try:
                detail = error.read().decode("utf-8", "replace")[:300]
            except Exception:  # noqa: BLE001
                pass
            print(f"  HTTP {error.code} for {name}: {detail}")
            if error.code < 500:
                raise  # 4xx never succeeds on retry (bad key/field/quota shape)
            if attempt == retries:
                raise
            time.sleep(2.0 * attempt)
        except Exception as error:  # noqa: BLE001 — network/timeout: retry
            print(f"  attempt {attempt}/{retries} failed for {name}: {error}")
            if attempt == retries:
                raise
            time.sleep(2.0 * attempt)
    return None


def main():
    only = sys.argv[1:] or list(TRACKS)
    key = resolve_key()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    PROV.parent.mkdir(parents=True, exist_ok=True)
    provenance = {"tool": "elevenlabs music (v1/music)", "endpoint": ENDPOINT,
                  "generatedAt": time.strftime("%Y-%m-%dT%H:%M:%S%z"), "tracks": {}}
    if PROV.exists():
        try:
            provenance["tracks"] = json.loads(PROV.read_text())["tracks"]
        except Exception:  # noqa: BLE001
            pass
    for name in only:
        length_ms, prompt = TRACKS[name]
        out = OUT_DIR / f"bgm-{name}.mp3"
        print(f"GEN {name} ({length_ms}ms) ...")
        audio = generate(key, name, length_ms, prompt)
        out.write_bytes(audio)
        provenance["tracks"][name] = {
            "file": f"Assets/Resources/Audio/{out.name}", "bytes": len(audio),
            "musicLengthMs": length_ms, "prompt": prompt,
            "forceInstrumental": True,
        }
        print(f"  wrote {out.name} ({len(audio)} bytes)")
    PROV.write_text(json.dumps(provenance, indent=2, ensure_ascii=False))
    print(f"PROVENANCE {PROV}")


if __name__ == "__main__":
    main()
