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
# AMENDMENT #8: two quote rules, unioned.
#
# The original rule alone shipped a live defect. `"([^"\\]*)"` refuses to match
# any literal containing a backslash, so a run of such literals leaves the
# alternation mis-paired and whole strings fall into the gaps between matches.
# HudView's "Companion cadence −{...}%" landed in one of those gaps: its U+2212
# never reached the charset, the subset never carried the glyph, and the
# coverage check below compared the font against the same short charset and
# printed FULL. A checker that validates against its own blind spot is not a
# checker.
#
# The escape-aware rule closes the gap. Both are kept: neither is a superset of
# the other on real C# (verbatim strings, char literals, interpolation braces),
# and the union is free — an over-harvested char costs a few bytes of subset.
QUOTED = (r'"([^"\\]*)"', r'"((?:[^"\\\n]|\\.)*)"')
for path in glob.glob('Assets/Scripts/View/*.cs'):
    source = open(path, encoding='utf-8').read()
    for rule in QUOTED:
        for quoted in re.findall(rule, source):
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
