# Cinder Court SFX via ElevenLabs sound-generation API.
# User directive 2026-08-04: SFX must come from the ElevenLabs API.
#
# Key resolution order: $ELEVENLABS_API_KEY, then ../Abyssal-Surge/.env.game-audio
# (read-only; never copied into this repo). Output: Assets/Art/Audio/cue-<name>.mp3
# plus docs/provenance/audio.json recording prompt + endpoint per cue.
#
# Fallback: if the API is unreachable / quota-blocked the script fails loudly.
# It does NOT silently substitute procedural audio (report the blocker instead).
import json
import os
import sys
import time
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT_DIR = REPO / "Assets" / "Resources" / "Audio"
PROV = REPO / "docs" / "provenance" / "audio.json"
ENDPOINT = "https://api.elevenlabs.io/v1/sound-generation"


def _find_env_fallback():
    """Walk up from the repo root looking for Abyssal-Surge/.env.game-audio."""
    for ancestor in [REPO, *REPO.parents]:
        candidate = ancestor / "Abyssal-Surge" / ".env.game-audio"
        if candidate.exists():
            return candidate
    return None


ENV_FALLBACK = _find_env_fallback()

# Prompt table mirrors docs/SIM_SPEC.md audio semantics.
# User directive 2026-08-04: SFX + BGM only — NO voice narration anywhere.
# The lore beat at wave start gets an ambient TEXTURE cue, not speech.
CUES = {
    "strike": (0.8, "Short crisp fantasy sword swing whoosh, dark metallic swipe, "
               "tight low arcade hit, no tail reverb, game SFX one-shot"),
    "hit": (0.8, "Quick gritty impact thud on armor, low sawtooth-like crunch, "
            "arcade enemy damage tick, dry, game SFX one-shot"),
    "kill": (1.0, "Ember burst enemy defeat pop, falling triangle chime with soft "
             "ash crackle, short dark fantasy kill confirm, game SFX one-shot"),
    "nova": (1.6, "Massive fiery shockwave nova burst radiating outward, deep "
             "descending roar with ember sizzle tail, arena AoE blast, game SFX"),
    "ward": (1.2, "Rising magical shield seal, warm glassy sine sweep upward, "
             "protective lantern barrier engage shimmer, game SFX one-shot"),
    "pickup": (0.7, "Bright tiny relic pickup sparkle, quick ascending glass chime, "
               "cheerful arcade collect blip, game SFX one-shot"),
    # W12: UI/traversal SFX (deep-interview-seed-ui-vfx-flow.md D7/W12).
    "click": (0.5, "Short crisp UI button click, tight dark-fantasy interface tap, "
              "low wooden-stone knock with subtle metallic edge, dry, no tail, "
              "game menu SFX one-shot"),
    "footstep": (0.5, "Single soft dungeon footstep on stone, muffled leather boot "
                 "fall, low thud with faint grit scrape, dry, no reverb tail, "
                 "game SFX one-shot"),
    # Loot rarity pickups (ui-lane3 loot popup contract): richer sibling of
    # "pickup" for Fine-tier drops, and a distinct gold/resonant sting for
    # Epic-tier drops. Both stay in the dark-fantasy charcoal/ember/spectral-cyan
    # palette established by the cue set above.
    "loot-fine": (0.6, "Fine-tier relic pickup shimmer, richer layered sparkle "
                  "than a plain collect blip, spectral cyan crystal resonance "
                  "with a quick ascending glass chime, dark fantasy arcade "
                  "collect confirm, dry, no long tail, game SFX one-shot"),
    "loot-epic": (1.0, "Epic-tier relic pickup fanfare, short bright gold chime "
                  "struck once with a low warm resonant drone layer underneath, "
                  "unmistakably precious and rare, dark fantasy arcade reward "
                  "confirm, tight tail, game SFX one-shot"),
    # UI toast popup: very short soft slide/pop, midrange and up only so it
    # layers cleanly over loot cues without muddying the low end.
    # NOTE: 0.5s is the ElevenLabs sound-generation API's hard floor for
    # duration_seconds (0.3 was rejected with HTTP 400 invalid_generation_settings);
    # the prompt still asks for the tightest possible transient within that floor.
    "toast": (0.5, "Extremely short soft UI toast popup sound, gentle upward "
              "slide into a light airy pop that decays almost instantly, "
              "midrange and high frequencies only, no bass, no low end, no "
              "reverb tail, subtle dark-fantasy interface notification, dry, "
              "game SFX one-shot"),
    "wave": (1.4, "Ominous war horn signaling a new enemy wave, rising dark brass "
             "swell with distant ember hiss, short stinger, game SFX"),
    "gameover": (2.2, "Lantern extinguishing defeat sting, deep descending sine "
                 "drone fading to cold silence, somber game over, game SFX"),
    # Ambient texture under the wave-start lore line — explicitly NO voice.
    "lore": (4.0, "Ethereal abyssal ambience swell, ghostly airy texture with "
             "faint ash-wind and deep sub rumble, mysterious ancient reliquary "
             "atmosphere, instrumental sound design only, absolutely no voice, "
             "no whispering words, no speech, no vocals"),
    # Looping background bed for the whole run (Unity AudioSource loop=true).
    "bgm": (22.0, "Dark fantasy arena ambient music loop, low ember drone bed, "
            "distant deep choir-like synth pads, slow ominous two-note pulse, "
            "smoldering coals crackle sparsely, seamless loop, instrumental "
            "only, no melody spikes, no percussion breaks, no vocals"),
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


def generate(key, name, duration, prompt, influence=0.55, retries=3):
    body = {
        "text": prompt,
        "duration_seconds": duration,   # API hard cap: 22 s
        "prompt_influence": influence,
    }
    if name == "bgm":
        body["loop"] = True             # seamless loop hint for the bed track
    payload = json.dumps(body).encode("utf-8")
    request = urllib.request.Request(
        ENDPOINT, data=payload, method="POST",
        headers={"xi-api-key": key, "Content-Type": "application/json"})
    for attempt in range(1, retries + 1):
        try:
            with urllib.request.urlopen(request, timeout=120) as response:
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
    only = sys.argv[1:] or list(CUES)
    key = resolve_key()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    PROV.parent.mkdir(parents=True, exist_ok=True)
    provenance = {"tool": "elevenlabs sound-generation", "endpoint": ENDPOINT,
                  "generatedAt": time.strftime("%Y-%m-%dT%H:%M:%S%z"), "cues": {}}
    if PROV.exists():
        try:
            provenance["cues"] = json.loads(PROV.read_text())["cues"]
        except Exception:  # noqa: BLE001
            pass
    for name in only:
        duration, prompt = CUES[name]
        out = OUT_DIR / f"cue-{name}.mp3"
        print(f"GEN {name} ({duration}s) ...")
        # BGM: lower prompt_influence for musicality; SFX: tighter adherence.
        influence = 0.3 if name == "bgm" else 0.55
        audio = generate(key, name, duration, prompt, influence=influence)
        out.write_bytes(audio)
        provenance["cues"][name] = {
            "file": f"Assets/Resources/Audio/{out.name}", "bytes": len(audio),
            "durationSeconds": duration, "prompt": prompt,
            "promptInfluence": influence,
        }
        print(f"  wrote {out.name} ({len(audio)} bytes)")
    PROV.write_text(json.dumps(provenance, indent=2, ensure_ascii=False))
    print(f"PROVENANCE {PROV}")


if __name__ == "__main__":
    main()
