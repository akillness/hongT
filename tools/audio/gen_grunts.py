#!/usr/bin/env python3
"""Player exertion grunts (기합) via the ElevenLabs sound-generation endpoint.

WHY A SEPARATE TOOL AND A SEPARATE CUE PREFIX. gen_sfx.py carries a signed rule at
its head: cue-* prompts forbid vocals, because a voice baked into a sound effect can
no longer be muted, ducked or translated on its own. A grunt IS a voice, so mixing it
into cue-hit would break that rule the moment it shipped. These are `voice-*` assets
and the View plays them through their own path.

NON-VERBAL ON PURPOSE. Every prompt ends in "no words". A wordless exhale needs no
localisation, which removes the third of those three concerns entirely — what remains
is muting and ducking, and a separate cue layer already answers both.

THE KEY IS SCOPED, NOT DEAD. CLAUDE.md §3 records ElevenLabs as HTTP 401 and the
pipeline as unrunnable. Measured 2026-08-11 with the key in .env.game-audio:

    /v1/user/subscription   401   (missing user_read)
    /v1/voices              401   (missing voices_read)
    /v1/sound-generation    200   <- the endpoint this tool uses
    /v1/text-to-speech      200   (with an explicit voice_id)

The blocker was a permission error read as an invalid key. A carried blocker is worth
re-testing against the tool rather than the target (CLAUDE.md §4z).

Usage:
    set -a; . ./.env.game-audio; set +a
    python3 tools/audio/gen_grunts.py            # only missing files
    python3 tools/audio/gen_grunts.py --force    # regenerate everything
"""
import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

ENDPOINT = "https://api.elevenlabs.io/v1/sound-generation"
OUT_DIR = Path("Assets/Resources/Audio")

# Style shared by every line so the variants read as ONE performer rather than a
# crowd. Close-mic and dry because the View's own reverb and the BGM bed sit on top;
# a baked tail fights both and cannot be removed later.
STYLE = ("single male voice, dry close-mic, no reverb tail, no music, "
         "no background, no words")

# (cue name, variants, seconds, prompt)
#
# VARIANT COUNTS ARE NOT UNIFORM, and that is the point. A dodge fires many times a
# minute and needs the most variety; a death fires once per run, so a third variant
# would cost credits and disk to be heard on the third death.
LINES = [
    ("voice-combo-finisher", 3, 1.0,
     "one short sharp martial kiai shout on a finishing blow, forceful, clipped"),
    ("voice-avoid", 3, 0.8,
     "one quick sharp inhale-hiss while dodging aside, breathy and brief"),
    ("voice-hurt", 3, 0.8,
     "one short pained grunt from taking a hit, restrained, not a scream"),
    ("voice-die", 2, 1.4,
     "one falling exhale as the body gives out, fading, no scream"),
]


def generate(prompt: str, seconds: float, key: str) -> bytes:
    request = urllib.request.Request(
        ENDPOINT,
        data=json.dumps({
            "text": f"{prompt}, {STYLE}",
            "duration_seconds": seconds,
            # High influence: the default lets the model drift toward generic
            # whooshes, and a whoosh is exactly what this layer must not be — the
            # cue-* set already covers impact noise.
            "prompt_influence": 0.85,
        }).encode(),
        headers={"xi-api-key": key, "Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=180) as response:
        return response.read()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--force", action="store_true",
                        help="regenerate files that already exist")
    parser.add_argument("--only", default=None, help="one cue name, e.g. voice-avoid")
    args = parser.parse_args()

    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        print("ELEVENLABS_API_KEY unset — `set -a; . ./.env.game-audio; set +a`")
        return 2

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    written = skipped = 0

    for name, variants, seconds, prompt in LINES:
        if args.only and name != args.only:
            continue
        for index in range(1, variants + 1):
            out = OUT_DIR / f"{name}-{index}.mp3"
            if out.exists() and not args.force:
                skipped += 1
                continue
            try:
                # Vary the prompt per index rather than relying on sampling noise:
                # the endpoint is deterministic enough that identical prompts can
                # return near-identical takes, and three copies of one grunt is the
                # repetition this layer exists to avoid.
                shade = ["", ", slightly lower pitch", ", slightly higher and tighter"][index - 1] \
                    if index <= 3 else ""
                audio = generate(prompt + shade, seconds, key)
            except urllib.error.HTTPError as error:
                print(f"{out.name}: HTTP {error.code} {error.read()[:120]!r}")
                return 1
            out.write_bytes(audio)
            print(f"{out.name}  {len(audio) / 1024:.0f} KB")
            written += 1

    print(f"\n{written} written, {skipped} skipped (already present)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
