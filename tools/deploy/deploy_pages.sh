#!/usr/bin/env bash
# Deploy build-webgl/ to the gh-pages branch of origin (akillness/hongT).
# Pages serves https://akillness.github.io/hongT/ — path casing is
# case-SENSITIVE on github.io, so the repo name must stay "hongT".
# Usage: bash tools/deploy/deploy_pages.sh [commit-message]
set -euo pipefail
cd "$(dirname "$0")/../.."

BUILD_DIR="build-webgl"
WORKTREE=".gh-pages-worktree"
STAGING=".gh-pages-stage"
MESSAGE="${1:-deploy: WebGL build $(date +%Y-%m-%dT%H:%M:%S)}"
WORKTREE_CREATED=0
STAGING_CREATED=0

cleanup() {
  if [ "$STAGING_CREATED" -eq 1 ]; then
    rm -rf "$STAGING"
  fi
  if [ "$WORKTREE_CREATED" -eq 1 ] && [ -d "$WORKTREE" ]; then
    git worktree remove --force "$WORKTREE" || true
  fi
}
trap cleanup EXIT

[ -f "$BUILD_DIR/index.html" ] || { echo "FATAL: $BUILD_DIR/index.html missing — build first"; exit 1; }

git fetch origin
if git ls-remote --exit-code --heads origin gh-pages >/dev/null 2>&1; then
  git worktree add --detach "$WORKTREE" origin/gh-pages
  WORKTREE_CREATED=1
else
  git worktree add --detach "$WORKTREE"
  WORKTREE_CREATED=1
  git -C "$WORKTREE" checkout --orphan gh-pages
  git -C "$WORKTREE" rm -rf . >/dev/null 2>&1 || true
fi

WORKTREE_ROOT="$(git -C "$WORKTREE" rev-parse --show-toplevel)"
EXPECTED_ROOT="$(cd "$WORKTREE" && pwd -P)"
[ "$WORKTREE_ROOT" = "$EXPECTED_ROOT" ] ||
  { echo "FATAL: Pages worktree did not initialize at $WORKTREE"; exit 1; }

# Build an exact candidate tree before synchronizing the isolated Pages
# worktree. This keeps arbitrary web/ additions/removals in the deployment
# while never copying its .git metadata.
[ ! -e "$STAGING" ] ||
  { echo "FATAL: deployment staging path already exists: $STAGING"; exit 1; }
mkdir -p "$STAGING"
STAGING_CREATED=1
rsync -a --exclude "*_BurstDebugInformation_DoNotShip" "$BUILD_DIR/" "$STAGING/"
[ -d web ] && rsync -a web/ "$STAGING/"
touch "$STAGING/.nojekyll"   # Pages: serve Build/ files verbatim, no Jekyll
rsync -a --delete --exclude ".git" "$STAGING/" "$WORKTREE/"

# Stage tracked changes in one operation so deleted transient files cannot
# disappear between discovery and staging. The isolated worktree contains only
# the exact deployment candidate, so this cannot capture root checkout work.
git -C "$WORKTREE" add -u -- .

# Stage each untracked candidate path explicitly.
stage_paths() {
  while IFS= read -r -d '' path; do
    git -C "$WORKTREE" add -- "$path"
  done
}
stage_paths < <(git -C "$WORKTREE" ls-files --others --exclude-standard -z)
if git -C "$WORKTREE" diff --cached --quiet; then
  echo "No changes to deploy."
else
  git -C "$WORKTREE" commit -m "$MESSAGE"
  git -C "$WORKTREE" push origin HEAD:gh-pages
fi
echo "Deployed. Verify: https://akillness.github.io/hongT/"
