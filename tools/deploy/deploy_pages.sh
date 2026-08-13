#!/usr/bin/env bash
# Deploy the exact sealed build-webgl/ + committed web/ payload to gh-pages.
# Pages serves https://akillness.github.io/hongT/ — path casing is
# case-SENSITIVE on github.io, so the repo name must stay "hongT".
# Usage: bash tools/deploy/deploy_pages.sh [commit-message]
set -euo pipefail
cd "$(dirname "$0")/../.."
export PYTHONDONTWRITEBYTECODE=1

BUILD_DIR="${PAGES_RELEASE_BUILD_DIR:-build-webgl}"
WORKTREE="${PAGES_WORKTREE_DIR:-.gh-pages-worktree}"
STAGING="${PAGES_STAGING_DIR:-.gh-pages-stage}"
EVIDENCE_DIR="${PAGES_EVIDENCE_DIR:-_workspace/current/qa/stage-character-shadows}"
MANIFEST="${PAGES_PAYLOAD_MANIFEST:-$EVIDENCE_DIR/deployment-payload-manifest.json}"
COLLISION_LOG="${PAGES_COLLISION_LOG:-$EVIDENCE_DIR/deployment-merge-collisions.json}"
SEAL="${PAGES_PAYLOAD_SEAL:-$EVIDENCE_DIR/deployment-payload-seal.json}"
PRECOPY_REPORT="${PAGES_PRECOPY_REPORT:-$EVIDENCE_DIR/deployment-precopy-recheck.json}"
REMOTE_REPORT="${PAGES_REMOTE_REPORT:-$EVIDENCE_DIR/remote-served-file-hashes.json}"
BASE_URL="${PAGES_BASE_URL:-https://akillness.github.io/hongT/}"
PYTHON_BIN="${PYTHON_BIN:-python3}"
SEALER="tools/deploy/seal_pages_payload.py"
MESSAGE="${1:-deploy: WebGL build $(date +%Y-%m-%dT%H:%M:%S)}"
# 12x5s = 60s was under the observed Pages rebuild time: the 2026-08-13 release
# exhausted every attempt, printed FATAL, and had ALREADY pushed gh-pages
# successfully - the served bytes caught up ~45 s later and a standalone
# verify-remote passed unchanged. A FATAL beside a working deploy is the most
# expensive shape of failure here, because the obvious response is to re-run
# deploy_pages.sh, which then dies on an existing staging path. 3 minutes.
REMOTE_ATTEMPTS="${PAGES_REMOTE_ATTEMPTS:-36}"
REMOTE_RETRY_SECONDS="${PAGES_REMOTE_RETRY_SECONDS:-5}"
WORKTREE_CREATED=0
STAGING_CREATED=0
REPORT_TMP_DIR="$(mktemp -d)"

# Framework Python installations on macOS may not inherit the system keychain.
# Prefer certifi's maintained CA bundle when the caller has not selected one.
if [ -z "${SSL_CERT_FILE:-}" ]; then
  CERTIFI_CA="$($PYTHON_BIN -c 'import certifi; print(certifi.where())' 2>/dev/null || true)"
  if [ -n "$CERTIFI_CA" ] && [ -f "$CERTIFI_CA" ]; then
    export SSL_CERT_FILE="$CERTIFI_CA"
  fi
fi

cleanup() {
  if [ "$STAGING_CREATED" -eq 1 ] && [ -d "$STAGING" ]; then
    rm -rf -- "$STAGING"
  fi
  if [ "$WORKTREE_CREATED" -eq 1 ] && [ -d "$WORKTREE" ]; then
    git worktree remove --force "$WORKTREE" || true
  fi
  if [ -d "$REPORT_TMP_DIR" ]; then
    rm -rf -- "$REPORT_TMP_DIR"
  fi
}
trap cleanup EXIT

freeze_report() {
  local source_path="$1"
  local frozen_path="$2"
  mkdir -p "$(dirname "$frozen_path")"
  if [ -e "$frozen_path" ]; then
    cmp -s -- "$source_path" "$frozen_path" || {
      echo "FATAL: existing frozen report differs: $frozen_path"
      exit 1
    }
    rm -f -- "$source_path"
  else
    mv -- "$source_path" "$frozen_path"
  fi
}

[ -f "$BUILD_DIR/index.html" ] || { echo "FATAL: $BUILD_DIR/index.html missing — build first"; exit 1; }
[ -f "$BUILD_DIR/release-build-provenance.json" ] ||
  { echo "FATAL: frozen Release provenance is missing"; exit 1; }
[ -f "$MANIFEST" ] || { echo "FATAL: sealed payload manifest is missing: $MANIFEST"; exit 1; }
[ -f "$COLLISION_LOG" ] || { echo "FATAL: sealed collision log is missing: $COLLISION_LOG"; exit 1; }
[ -f "$SEAL" ] || { echo "FATAL: detached payload seal is missing: $SEAL"; exit 1; }
[ ! -e "$STAGING" ] || { echo "FATAL: deployment staging path already exists: $STAGING"; exit 1; }
[ ! -e "$WORKTREE" ] || { echo "FATAL: Pages worktree path already exists: $WORKTREE"; exit 1; }
case "$REMOTE_ATTEMPTS" in (*[!0-9]*|'') echo "FATAL: PAGES_REMOTE_ATTEMPTS must be a positive integer"; exit 1;; esac
[ "$REMOTE_ATTEMPTS" -gt 0 ] || { echo "FATAL: PAGES_REMOTE_ATTEMPTS must be positive"; exit 1; }

CANDIDATE_SHA="$($PYTHON_BIN "$SEALER" show --repo-root . --release-build "$BUILD_DIR" --field candidateSourceSha)"
SOURCE_UPSTREAM="$($PYTHON_BIN "$SEALER" show --repo-root . --release-build "$BUILD_DIR" --field sourceUpstream)"
EXPECTED_TREE="$($PYTHON_BIN "$SEALER" show --repo-root . --seal "$SEAL" --field expectedGitTreeId)"
case "$SOURCE_UPSTREAM" in
  origin/*) SOURCE_BRANCH="${SOURCE_UPSTREAM#origin/}" ;;
  *) echo "FATAL: sourceUpstream must be origin/<branch>"; exit 1 ;;
esac
case "$SOURCE_BRANCH" in
  ''|*'..'*|/*) echo "FATAL: unsafe source branch: $SOURCE_BRANCH"; exit 1 ;;
esac

# External writes are allowed only after the normal source push has made the
# recorded candidate the exact remote source branch tip.
git fetch origin
REMOTE_SOURCE_SHA="$(git ls-remote --heads origin "refs/heads/$SOURCE_BRANCH" | awk 'NR == 1 { print $1 }')"
[ -n "$REMOTE_SOURCE_SHA" ] || { echo "FATAL: remote source branch missing: $SOURCE_BRANCH"; exit 1; }
[ "$(git rev-parse "$SOURCE_UPSTREAM^{commit}")" = "$REMOTE_SOURCE_SHA" ] || {
  echo "FATAL: fetched $SOURCE_UPSTREAM does not match ls-remote"
  exit 1
}
[ "$REMOTE_SOURCE_SHA" = "$CANDIDATE_SHA" ] || {
  echo "FATAL: remote source $SOURCE_UPSTREAM is $REMOTE_SOURCE_SHA, expected $CANDIDATE_SHA"
  exit 1
}

# Recreate the full sealed merge in a fresh stage. The verifier rechecks HEAD,
# clean compiled/copied roots, committed web bytes/tree, tool blobs, provenance,
# the canonical self-excluding manifest, and the expected complete Git tree.
STAGING_CREATED=1
PRECOPY_TMP="$REPORT_TMP_DIR/deployment-precopy-recheck.json"
$PYTHON_BIN "$SEALER" verify \
  --repo-root . \
  --release-build "$BUILD_DIR" \
  --web-root web \
  --stage-dir "$STAGING" \
  --manifest "$MANIFEST" \
  --collision-log "$COLLISION_LOG" \
  --seal "$SEAL" \
  --report "$PRECOPY_TMP" >/dev/null
freeze_report "$PRECOPY_TMP" "$PRECOPY_REPORT"

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

# No old gh-pages file survives. The stage already contains the exact Release
# copy, Burst-debug exclusion, logged web overlay, deterministic .nojekyll, and
# the canonical manifest; the detached seal is intentionally not deployed.
rsync -a --delete --exclude ".git" "$STAGING/" "$WORKTREE/"

# Stage tracked changes atomically, then add each untracked candidate path with
# an explicit pathspec. Never stage from the shared/source checkout.
git -C "$WORKTREE" add -u -- .
stage_paths() {
  while IFS= read -r -d '' path; do
    git -C "$WORKTREE" add -- "$path"
  done
}
stage_paths < <(git -C "$WORKTREE" ls-files --others --exclude-standard -z)

STAGED_TREE="$(git -C "$WORKTREE" write-tree)"
[ "$STAGED_TREE" = "$EXPECTED_TREE" ] || {
  echo "FATAL: staged Pages tree $STAGED_TREE does not match sealed tree $EXPECTED_TREE"
  exit 1
}

if git -C "$WORKTREE" diff --cached --quiet; then
  echo "No Pages byte changes; verifying the existing commit and deployment."
else
  git -C "$WORKTREE" commit -m "$MESSAGE"
  git -C "$WORKTREE" push origin HEAD:gh-pages
fi

LOCAL_COMMIT="$(git -C "$WORKTREE" rev-parse HEAD)"
LOCAL_TREE="$(git -C "$WORKTREE" rev-parse 'HEAD^{tree}')"
[ "$LOCAL_TREE" = "$EXPECTED_TREE" ] || {
  echo "FATAL: local gh-pages commit tree $LOCAL_TREE does not match $EXPECTED_TREE"
  exit 1
}

# verify-remote revalidates the exact source checkout and therefore must not
# see this script's own transient staging/worktree directories as outside
# inputs. Their commit/tree identities are frozen above, so remove them before
# the source-clean and HTTP byte checks.
rm -rf -- "$STAGING"
STAGING_CREATED=0
git worktree remove --force "$WORKTREE"
WORKTREE_CREATED=0

git fetch origin gh-pages:refs/remotes/origin/gh-pages
REMOTE_COMMIT="$(git rev-parse origin/gh-pages)"
REMOTE_TREE="$(git rev-parse 'origin/gh-pages^{tree}')"
[ "$REMOTE_TREE" = "$EXPECTED_TREE" ] || {
  echo "FATAL: remote gh-pages tree $REMOTE_TREE does not match $EXPECTED_TREE"
  exit 1
}

# GitHub Pages can lag the branch briefly. Every attempt uses fresh cache keys;
# a report is written only after the remote Git tree and every served byte pass.
remote_verified=0
for ((attempt = 1; attempt <= REMOTE_ATTEMPTS; attempt += 1)); do
  REMOTE_TMP="$REPORT_TMP_DIR/remote-served-file-hashes-$attempt.json"
  if $PYTHON_BIN "$SEALER" verify-remote \
      --repo-root . \
      --release-build "$BUILD_DIR" \
      --manifest "$MANIFEST" \
      --seal "$SEAL" \
      --remote-commit "$REMOTE_COMMIT" \
      --base-url "$BASE_URL" \
      --report "$REMOTE_TMP"; then
    freeze_report "$REMOTE_TMP" "$REMOTE_REPORT"
    remote_verified=1
    break
  fi
  if [ "$attempt" -lt "$REMOTE_ATTEMPTS" ]; then
    sleep "$REMOTE_RETRY_SECONDS"
  fi
done
[ "$remote_verified" -eq 1 ] || { echo "FATAL: remote Pages bytes never matched the seal"; exit 1; }

echo "Deployed candidate $CANDIDATE_SHA as gh-pages $REMOTE_COMMIT (tree $REMOTE_TREE)."
echo "Verified: $BASE_URL"
