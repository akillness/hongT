#!/usr/bin/env bash
# Deploy build-webgl/ to the gh-pages branch of origin (akillness/HongT).
# Pages then serves https://akillness.github.io/HongT/ (user-facing casing
# "hongT" resolves to the same path case-insensitively on github.io hosts).
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

# Sync build output (delete removed files, keep .git).
rsync -a --delete --exclude ".git" \
  --exclude "*_BurstDebugInformation_DoNotShip" \
  "$BUILD_DIR/" "$WORKTREE/"
touch "$WORKTREE/.nojekyll"   # Pages: serve Build/ files verbatim, no Jekyll

git -C "$WORKTREE" add -A
if git -C "$WORKTREE" diff --cached --quiet; then
  echo "No changes to deploy."
else
  git -C "$WORKTREE" commit -m "$MESSAGE"
  git -C "$WORKTREE" push origin gh-pages
fi
git worktree remove --force "$WORKTREE"
echo "Deployed. Verify: https://akillness.github.io/HongT/"
