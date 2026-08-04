#!/usr/bin/env bash
# Deploy build-webgl/ to the gh-pages branch of origin (akillness/hongT).
# Pages serves https://akillness.github.io/hongT/ — path casing is
# case-SENSITIVE on github.io, so the repo name must stay "hongT".
# Usage: bash tools/deploy/deploy_pages.sh [commit-message]
set -euo pipefail
cd "$(dirname "$0")/../.."

BUILD_DIR="build-webgl"
WORKTREE=".gh-pages-worktree"
MESSAGE="${1:-deploy: WebGL build $(date +%Y-%m-%dT%H:%M:%S)}"

[ -f "$BUILD_DIR/index.html" ] || { echo "FATAL: $BUILD_DIR/index.html missing — build first"; exit 1; }

git fetch origin
if git ls-remote --exit-code --heads origin gh-pages >/dev/null 2>&1; then
  git worktree add "$WORKTREE" gh-pages 2>/dev/null || true
else
  git worktree add --detach "$WORKTREE" 2>/dev/null || true
  git -C "$WORKTREE" checkout --orphan gh-pages
  git -C "$WORKTREE" rm -rf . >/dev/null 2>&1 || true
fi

# Sync build output (delete removed files, keep .git), then overlay static
# pages from web/ (campaign hub etc.) — NO --delete on the overlay so Unity
# output and static pages coexist.
rsync -a --delete --exclude ".git" \
  --exclude "*_BurstDebugInformation_DoNotShip" \
  "$BUILD_DIR/" "$WORKTREE/"
[ -d web ] && rsync -a web/ "$WORKTREE/"
touch "$WORKTREE/.nojekyll"   # Pages: serve Build/ files verbatim, no Jekyll

git -C "$WORKTREE" add -A
if git -C "$WORKTREE" diff --cached --quiet; then
  echo "No changes to deploy."
else
  git -C "$WORKTREE" commit -m "$MESSAGE"
  git -C "$WORKTREE" push origin gh-pages
fi
git worktree remove --force "$WORKTREE"
echo "Deployed. Verify: https://akillness.github.io/hongT/"
