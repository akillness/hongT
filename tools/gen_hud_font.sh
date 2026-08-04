#!/usr/bin/env bash
# Regenerate Assets/Resources/Fonts/HudKorean.otf from ALL Korean text in the
# View assembly (HUD labels, lore beats, story dialogue, lobby panels...).
# Run after adding any user-visible Korean string.
set -euo pipefail
cd "$(dirname "$0")/.."

python3 - <<'EOF'
import glob, re, string
chars = set(string.ascii_letters + string.digits + string.punctuation + ' ')
chars.update('•▲▼◀▶—')
for path in glob.glob('Assets/Scripts/View/*.cs'):
    source = open(path, encoding='utf-8').read()
    for quoted in re.findall(r'"([^"\\]*)"', source):
        chars.update(quoted)
    chars.update(re.findall(r'[가-힣]', source))
text = ''.join(sorted(c for c in chars if c.isprintable()))
open('/tmp/hud-charset.txt', 'w', encoding='utf-8').write(text)
print(f'{len(text)} glyphs from View assembly')
EOF

python3 -m fontTools.subset ~/Library/Fonts/NanumBarunGothic.otf \
  --text-file=/tmp/hud-charset.txt \
  --output-file=Assets/Resources/Fonts/HudKorean.otf \
  --no-hinting --desubroutinize 2>&1 | grep -v FFTM || true

python3 - <<'EOF'
from fontTools.ttLib import TTFont
import os
cmap = TTFont('Assets/Resources/Fonts/HudKorean.otf').getBestCmap()
charset = open('/tmp/hud-charset.txt', encoding='utf-8').read()
missing = [c for c in charset if ord(c) not in cmap and not c.isspace()]
print('bytes:', os.path.getsize('Assets/Resources/Fonts/HudKorean.otf'))
if missing:
    print('MISSING (source font lacks these — replace in code):', missing)
    raise SystemExit(1)
print('coverage: FULL')
EOF
