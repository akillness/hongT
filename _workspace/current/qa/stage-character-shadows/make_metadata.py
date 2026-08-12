#!/usr/bin/env python3
"""Emit the --metadata document for tools/deploy/release_provenance.py create.

Hand-writing this file is how a release ends up asserting things nobody
measured, so every field here is either read from Git, read from the evidence
that was actually produced, or a literal that the deploy procedure defines.

Run from the repository root, after all six evidence records and both
clean-status snapshots exist:

    python3 _workspace/current/qa/stage-character-shadows/make_metadata.py

Writes <evidence dir>/release-metadata.json.
"""

from __future__ import annotations

import datetime as dt
import hashlib
import json
import subprocess
import sys
from pathlib import Path

EVIDENCE_DIR = Path("_workspace/current/qa/stage-character-shadows")

# docs/release-deploy-procedure.md §5: the probe is the golden digest recording,
# because it is the one file sim arithmetic cannot change without touching.
GOLDEN_PROBE = "Assets/Tests/EditMode/DungeonGoldenDigestTests.cs"

# The source currently live on gh-pages ("deploy: exact source 94195cf").
RELEASE_BASE = "94195cf"

SOURCE_UPSTREAM = "origin/akillness/main"
UNITY_VERSION = "6000.5.6f1"

# release_provenance.py:47-54 REQUIRED_EVIDENCE_MODES, and the marker rule at
# :152-156 -- Source records describe the source and never saw a payload, so
# they must carry a null contentMarker.
EVIDENCE = [
    ("shadow-focused-editmode", "Source"),
    ("full-editmode", "Source"),
    ("release-build", "Source"),
    ("development-build", "Development"),
    ("browser-shadow-desktop", "Development"),
    ("browser-shadow-mobile", "Development"),
]

# Untracked/ignored paths OUTSIDE INPUT_ROOTS. They cannot alter what gets
# built -- that is precisely what puts them outside -- so the list exists to
# make the noise declared rather than invisible. Patterns are fnmatchcase, in
# which '*' also matches '/', and are deliberately category-shaped: the editor,
# the agent tooling and the knowledge hooks all create new files under these
# prefixes continuously, and a pattern that had to be re-enumerated per file
# would fail between freeze and deploy.
OUTSIDE_ALLOW_LIST = [
    ".*",                    # agent/editor dotfiles and dotdirs (.jeo, .serena, .env.*)
    "_workspace/*",          # lane evidence and reports, including this release's
    "docs/*.DS_Store",       # Finder droppings under docs/
    "graphify-out/*",        # knowledge-graph build output (CLAUDE.md §7 hooks)
    "llm-wiki/*",            # wiki state written by the same hooks
    "tools/*",               # tools/deploy is an INPUT_ROOT; the rest is not
    "Library",               # Unity import cache
    "Logs",
    "Temp",
    "obj",
    "UserSettings",
    "*.csproj",              # Unity-regenerated IDE projects
    "build-development*",    # the Development build this release measured
    "build-webgl*",          # the Release build this release ships
    "mono_crash.*.json",
    "node_modules",
    "skills",
    "skills-lock.json",
    "tmp",
]


def git(*args: str) -> str:
    return subprocess.run(
        ["git", *args], check=True, capture_output=True, text=True
    ).stdout.strip()


def probe_hash(sha: str) -> str:
    blob = subprocess.run(
        ["git", "show", f"{sha}:{GOLDEN_PROBE}"], check=True, capture_output=True
    ).stdout
    return hashlib.sha256(blob).hexdigest()


def main() -> int:
    candidate = git("rev-parse", "HEAD")
    release_base = git("rev-parse", f"{RELEASE_BASE}^{{commit}}")

    # The lineage check is `merge-base --is-ancestor`, which accepts equality
    # (release_common.py:314-324). The baseline probe is the deployed source
    # itself: this release is measured against what users are running now.
    baseline_probe = release_base

    missing = []
    for evidence_id, _ in EVIDENCE:
        path = EVIDENCE_DIR / f"evidence-{evidence_id}.json"
        if not path.is_file():
            missing.append(str(path))
            continue
        payload = json.loads(path.read_text())
        if payload.get("result") != "PASS":
            missing.append(f"{path} (result={payload.get('result')})")
        if payload.get("candidateSourceSha") != candidate:
            missing.append(
                f"{path} (candidate={payload.get('candidateSourceSha')} != {candidate})"
            )
    for snapshot in ("candidate-clean-pre.json", "candidate-clean-post.json"):
        if not (EVIDENCE_DIR / snapshot).is_file():
            missing.append(str(EVIDENCE_DIR / snapshot))
    if missing:
        print("refusing to emit metadata; unusable inputs:", file=sys.stderr)
        for item in missing:
            print(f"  {item}", file=sys.stderr)
        return 1

    metadata = {
        "releaseBaseSha": release_base,
        "baselineProbeSha": baseline_probe,
        "candidateSourceSha": candidate,
        "sourceUpstream": SOURCE_UPSTREAM,
        "unityVersion": UNITY_VERSION,
        "generatedAt": dt.datetime.now(dt.timezone.utc)
        .replace(microsecond=0)
        .isoformat()
        .replace("+00:00", "Z"),
        "probeHashes": {
            "baseline": probe_hash(baseline_probe),
            "candidate": probe_hash(candidate),
        },
        "commands": [
            "bash tools/unity_batch.sh import-only",
            "bash tools/deploy/run_release_gate.sh 1",
            "python3 tools/deploy/make_release_evidence.py editmode",
            "python3 tools/deploy/make_release_evidence.py build --kind development",
            # docs/release-deploy-procedure.md §5: the browser harness is pinned
            # by procedure, not by the seal, so its sha is recorded here.
            "node tools/qa/run_shadow_browser_evidence.mjs (harness e2a44f6)"
            " --viewport 1440x900 --evidence-id browser-shadow-desktop",
            "node tools/qa/run_shadow_browser_evidence.mjs (harness e2a44f6)"
            " --viewport 390x844 --evidence-id browser-shadow-mobile",
            "bash tools/deploy/run_release_gate.sh 2",
            "python3 tools/deploy/make_release_evidence.py build --kind release",
            "python3 tools/deploy/release_provenance.py snapshot-clean (pre, post)",
            "python3 tools/deploy/release_provenance.py create",
        ],
        "outsideInputAllowList": OUTSIDE_ALLOW_LIST,
        "cleanStatus": {
            "pre": str(EVIDENCE_DIR / "candidate-clean-pre.json"),
            "post": str(EVIDENCE_DIR / "candidate-clean-post.json"),
        },
        "evidence": [
            {
                "evidenceId": evidence_id,
                "path": str(EVIDENCE_DIR / f"evidence-{evidence_id}.json"),
                "buildMode": build_mode,
                "contentMarker": json.loads(
                    (EVIDENCE_DIR / f"evidence-{evidence_id}.json").read_text()
                ).get("contentMarker"),
            }
            for evidence_id, build_mode in EVIDENCE
        ],
    }

    output = EVIDENCE_DIR / "release-metadata.json"
    output.write_text(json.dumps(metadata, indent=2, sort_keys=True) + "\n")
    print(f"wrote {output}")
    print(f"  candidate      {candidate}")
    print(f"  releaseBase    {release_base}")
    print(f"  probe baseline {metadata['probeHashes']['baseline']}")
    print(f"  probe candidate{metadata['probeHashes']['candidate']}")
    same = metadata["probeHashes"]["baseline"] == metadata["probeHashes"]["candidate"]
    print(f"  golden moved:  {'no' if same else 'YES -- justify it'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
