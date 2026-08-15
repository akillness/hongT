#!/usr/bin/env bash
# Assemble the god-tibo-imagen brand frames into the boot intro reel.
#
# Output contract (consumed by Assets/Scripts/View/IntroVideoView.cs):
#   Assets/StreamingAssets/Video/cinder-court-intro.mp4
#   H.264 High/yuv420p, 1280x720, 30 fps, no audio, faststart.
#
# Frames arrive at mixed aspect ratios (1536x1024, 1672x941), so each is
# scaled-to-cover and centre-cropped to 16:9 before the Ken-Burns push, then
# cross-faded. The Korean title lockup is burned in over the final hold using the
# same subset font the HUD ships (Resources/Fonts/HudKorean.otf).
#
# Beat 6 ("brand hold") history: the original god-tibo-imagen render
# (1254x1254) was rejected on review — the subject read as a piece of fruit
# rather than the lantern-bearing Warden — and the beat was cut to 5 frames.
# 2026-08-09: regenerated with Higgsfield nano_banana_flash using frame03 +
# frame05 as IMAGE REFERENCES (the capability the codex-cli provider lacked,
# which is why consistency had to ride a text-only STYLE suffix before). The
# new frame06.png (1264x848) reads as a full-body hooded Warden with the
# lantern raised and a clean dark top third, so the beat is back in and the
# title lockup lands over it. Edit FRAMES to change the reel; every loop below
# derives its bounds from it.
set -euo pipefail
cd "$(dirname "$0")"
ROOT=../../../..

FONT="$ROOT/Assets/Resources/Fonts/HudKorean.otf"
OUT_DIR="$ROOT/Assets/StreamingAssets/Video"
OUT="$OUT_DIR/cinder-court-intro.mp4"
DOCS_OUT="$ROOT/docs/nan2026/assets/video/cinder-court-intro.mp4"

FRAMES=(
  frames/frame01.png
  frames/frame02.png
  frames/frame03.png
  frames/frame04.png
  frames/frame05.png
  frames/frame06.png
)
N=${#FRAMES[@]}

SEG=1.8          # seconds each frame is on screen before the transition
XF=0.6           # cross-fade duration
FPS=30
D=$(python3 -c "print(int($SEG*$FPS))")

[ -f "$FONT" ] || { echo "missing font: $FONT" >&2; exit 1; }
for f in "${FRAMES[@]}"; do
  [ -s "$f" ] || { echo "missing $f" >&2; exit 1; }
done
mkdir -p "$OUT_DIR" "$(dirname "$DOCS_OUT")"

# Per-frame Ken-Burns: odd frames push in, even frames pull out.
# NOTE: inputs are SINGLE still frames (no -loop), so zoompan's d= is the only
# thing that sets segment length. Feeding a looped stream instead makes zoompan
# emit d frames per input frame and the reel balloons (observed: 87 s).
filter=""
for ((i = 0; i < N; i++)); do
  if [ $((i % 2)) -eq 0 ]; then
    Z="z='min(zoom+0.0009,1.16)'"
  else
    Z="z='if(lte(zoom,1.0),1.16,max(1.001,zoom-0.0009))'"
  fi
  filter+="[${i}:v]scale=1920:1080:force_original_aspect_ratio=increase,"
  filter+="crop=1920:1080,zoompan=${Z}:d=${D}:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':"
  filter+="s=1280x720:fps=${FPS},setsar=1,format=yuv420p[v${i}];"
done

# Cross-fade chain: each transition starts XF before the current tail.
prev="v0"
acc=$SEG
for ((i = 1; i < N; i++)); do
  off=$(python3 -c "print(round($acc-$XF,3))")
  filter+="[${prev}][v${i}]xfade=transition=fade:duration=${XF}:offset=${off}[x${i}];"
  prev="x${i}"
  acc=$(python3 -c "print(round($acc+$SEG-$XF,3))")
done
TOTAL=$acc

# Title lockup over the final hold + fade to black on the very last beat.
# y=h*0.18 is MEASURED, not eyeballed: beat 6 puts the lantern flare at the old
# h*0.34 band, and the gold Korean sub-line sat right on it (mean luma 56.8 in
# the 30px sub band). Sampling the cropped 1280x720 luma of frame06 per
# candidate y gave title 35.5->17.4 and sub 56.8->28.2 at h*0.18, which is the
# clean dark top third the beat was regenerated to provide. Re-measure if the
# final beat ever changes (script: ffmpeg -vf ...,format=gray -f rawvideo).
T_IN=$(python3 -c "print(round($TOTAL-2.4,3))")
FADE_OUT=$(python3 -c "print(round($TOTAL-0.7,3))")
filter+="[${prev}]drawtext=fontfile='${FONT}':text='ABYSSAL LANTERN':"
filter+="fontcolor=0xF2EFE6:fontsize=64:x=(w-text_w)/2:y=h*0.18:"
filter+="alpha='if(lt(t,${T_IN}),0,min(1,(t-${T_IN})/0.6))':"
filter+="shadowcolor=0x000000@0.85:shadowx=0:shadowy=3,"
filter+="drawtext=fontfile='${FONT}':text='잿불의 법정을 지켜라':"
filter+="fontcolor=0xDEC76A:fontsize=30:x=(w-text_w)/2:y=h*0.18+86:"
filter+="alpha='if(lt(t,${T_IN}+0.35),0,min(1,(t-${T_IN}-0.35)/0.6))':"
filter+="shadowcolor=0x000000@0.85:shadowx=0:shadowy=2,"
filter+="fade=t=in:st=0:d=0.5,fade=t=out:st=${FADE_OUT}:d=0.7[vout]"

inputs=()
for f in "${FRAMES[@]}"; do inputs+=(-i "$f"); done

ffmpeg -y -hide_banner -loglevel error \
  "${inputs[@]}" \
  -filter_complex "$filter" -map "[vout]" \
  -c:v libx264 -profile:v high -pix_fmt yuv420p -crf 21 -preset slow \
  -movflags +faststart -r $FPS -an "$OUT"

cp "$OUT" "$DOCS_OUT"
echo "wrote $OUT (${N} frames, expected duration ${TOTAL}s)"
ffprobe -hide_banner -v error -show_entries \
  format=duration,size:stream=codec_name,width,height,avg_frame_rate \
  -of default=noprint_wrappers=1 "$OUT"
