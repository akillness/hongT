#!/usr/bin/env bash
# Generate the brand-intro frames with god-tibo-imagen.
#
# NOTE: frame 6 (the "brand hold") was cut after review — the render read as a
# piece of fruit, not the Warden. This script still knows how to produce it, but
# assemble.sh only consumes frames 1-5; re-adding frame06.png does NOT put it
# back in the reel unless you also add it to FRAMES in assemble.sh.
#
# Provider notes (both observed, not assumed):
#  - default `private-codex` backend returns HTTP 429 (image quota exhausted
#    on this account), so we drive the `codex-cli` provider instead.
#  - `codex-cli` does NOT support image input ("The codex-cli provider does
#    not support image input"), so cross-frame consistency is carried by a
#    shared STYLE suffix appended to every prompt rather than by a reference
#    image.
set -u
cd "$(dirname "$0")"

STYLE="Consistent series style: painterly dark-fantasy game concept art, moody deep-teal shadows with warm amber ember glow, wet black obsidian architecture, heavy volumetric fog, drifting cinders, cinematic widescreen composition, high detail, no text, no letters, no logo, no watermark."

declare -a PROMPTS
PROMPTS[1]="Frame 1 of a 6-frame brand intro for the game Abyssal Lantern: a single glowing ember spark igniting alone in pitch-black darkness, faint reflection on a wet black obsidian floor, tiny drifting embers, almost entirely dark frame. ${STYLE}"
PROMPTS[2]="Frame 2 of a 6-frame brand intro for the game Abyssal Lantern: the ember spark has grown into a glowing ember lantern held aloft by an unseen hand, warm amber light pushing back the dark, sparks drifting upward from the lantern, black obsidian floor below. ${STYLE}"
PROMPTS[3]="Frame 3 of a 6-frame brand intro for the game Abyssal Lantern: a lone hooded Dusk Warden silhouette rises holding the glowing ember lantern, cloak billowing, lantern light revealing the edge of a vast abyssal court, dramatic amber rim light against teal darkness. ${STYLE}"
PROMPTS[4]="Frame 4 of a 6-frame brand intro for the game Abyssal Lantern: wide establishing reveal of a vast abyssal court of towering black obsidian pillars receding into fog, the tiny lantern-bearing Dusk Warden at the center, overwhelming sense of scale and dread. ${STYLE}"
PROMPTS[5]="Frame 5 of a 6-frame brand intro for the game Abyssal Lantern: molten cinders and embers surge upward through the abyssal court, the obsidian pillars veined with glowing ember light, the Dusk Warden bracing with the lantern raised against the storm of sparks, dynamic energy. ${STYLE}"
PROMPTS[6]="Frame 6 of a 6-frame brand intro for the game Abyssal Lantern, heroic brand hold shot: the Dusk Warden stands triumphant at the center holding the blazing ember lantern in the abyssal cinder court, large empty dark negative space across the top third reserved for a title lockup, epic symmetrical composition. ${STYLE}"

for i in 1 2 3 4 5 6; do
  OUT="frames/frame0${i}.png"
  if [ -f "$OUT" ] && [ -s "$OUT" ]; then echo "SKIP $OUT (exists)"; continue; fi
  attempt=0
  while [ $attempt -lt 4 ]; do
    attempt=$((attempt+1))
    echo "=== frame $i attempt $attempt ==="
    gti --provider codex-cli --prompt "${PROMPTS[$i]}" --output "$OUT" 2>&1 | tail -3
    if [ -f "$OUT" ] && [ -s "$OUT" ]; then
      echo "OK $OUT ($(wc -c < "$OUT") bytes)"; break
    fi
    echo "retry in 15s"; sleep 15
  done
  [ -f "$OUT" ] || echo "FAILED frame $i"
done
echo "=== DONE ==="; ls -la frames/
