#!/usr/bin/env python3
"""Build the six frozen evidence records release_provenance.py requires.

Each record answers one question, in the shape the provenance verifier checks
(evidenceId / candidateSourceSha / buildMode / contentMarker / result):

  shadow-focused-editmode  Source       does the shadow contract itself hold?
  full-editmode            Source       does the whole EditMode suite hold?
  release-build            Source       did the Release WebGL build succeed?
  development-build        Development  did the Development WebGL build succeed?
  browser-shadow-desktop   Development  written by tools/qa/run_shadow_browser_evidence.mjs
  browser-shadow-mobile    Development  written by tools/qa/run_shadow_browser_evidence.mjs

Development-mode records must carry the Development build's contentMarker, which
is what ties a browser observation to the exact bytes that were in the browser.
Source-mode records must carry a null marker: they describe the source, not a
payload, and giving them a marker would silently claim a payload they never saw.

Note on the shadow-focused record. Unity's `-testFilter` cannot be used in this
project: the runner maps it to groupNames and FullNameFilter.Match throws an
NRE while walking the tree, so the run dies with RunError (code 3) and writes no
results at all. The shadow subset is therefore extracted from the full run's
NUnit XML by full name. Same executions, same assertions, one run instead of two
-- and stated here rather than left to look like a second run that never
happened.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))

from release_common import build_record  # noqa: E402

SHADOW_PATTERN = re.compile(r"shadow", re.IGNORECASE)
BUILD_RESULT_RE = re.compile(
    r"\[BuildWebGL\] result=(?P<result>\w+) size=(?P<size>\d+) "
    r"errors=(?P<errors>\d+) warnings=(?P<warnings>\d+)"
)


def _test_cases(results_path: Path) -> list[dict[str, object]]:
    root = ET.parse(results_path).getroot()
    cases = []
    for case in root.iter("test-case"):
        cases.append(
            {
                "fullname": case.attrib.get("fullname", ""),
                "result": case.attrib.get("result", ""),
            }
        )
    if not cases:
        raise SystemExit(f"no test-case elements in {results_path}")
    return cases


def _suite_totals(results_path: Path) -> dict[str, object]:
    root = ET.parse(results_path).getroot()
    return {
        "total": int(root.attrib.get("total", 0)),
        "passed": int(root.attrib.get("passed", 0)),
        "failed": int(root.attrib.get("failed", 0)),
        "skipped": int(root.attrib.get("skipped", 0)),
        "inconclusive": int(root.attrib.get("inconclusive", 0)),
    }


def _write(out_dir: Path, evidence: dict[str, object]) -> Path:
    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / f"evidence-{evidence['evidenceId']}.json"
    path.write_text(json.dumps(evidence, indent=2, ensure_ascii=False) + "\n")
    print(f"{evidence['result']} {evidence['evidenceId']} -> {path}")
    return path


def editmode(args: argparse.Namespace) -> int:
    results = Path(args.results)
    totals = _suite_totals(results)
    cases = _test_cases(results)
    failed = [c for c in cases if c["result"] not in ("Passed", "Skipped")]

    shadow_cases = [c for c in cases if SHADOW_PATTERN.search(str(c["fullname"]))]
    shadow_failed = [c for c in shadow_cases if c["result"] not in ("Passed", "Skipped")]
    # An empty subset must never read as success: it would certify the shadow
    # contract by having tested nothing.
    shadow_ok = bool(shadow_cases) and not shadow_failed

    _write(
        Path(args.out),
        {
            "evidenceId": "shadow-focused-editmode",
            "candidateSourceSha": args.candidate,
            "buildMode": "Source",
            "contentMarker": None,
            "result": "PASS" if shadow_ok else "FAIL",
            "method": "shadow subset extracted by full name from the full EditMode NUnit results",
            "resultsFile": str(results),
            "shadowTestCount": len(shadow_cases),
            "shadowFailures": [c["fullname"] for c in shadow_failed],
            "shadowTests": sorted(str(c["fullname"]) for c in shadow_cases),
        },
    )
    _write(
        Path(args.out),
        {
            "evidenceId": "full-editmode",
            "candidateSourceSha": args.candidate,
            "buildMode": "Source",
            "contentMarker": None,
            "result": "PASS" if not failed else "FAIL",
            "method": "bash tools/unity_batch.sh tests",
            "resultsFile": str(results),
            "totals": totals,
            "failures": [c["fullname"] for c in failed],
        },
    )
    return 0 if (shadow_ok and not failed) else 1


def build(args: argparse.Namespace) -> int:
    repo = Path(args.repo_root).resolve()
    build_dir = Path(args.build_dir).resolve()
    record = build_record(repo, build_dir, exclude_provenance=True)
    log_text = Path(args.log).read_text(errors="replace")
    match = None
    for match in BUILD_RESULT_RE.finditer(log_text):
        pass
    if match is None:
        raise SystemExit(f"no [BuildWebGL] result line in {args.log}")
    succeeded = match.group("result") == "Succeeded" and match.group("errors") == "0"

    development = args.kind == "development"
    evidence = {
        "evidenceId": f"{args.kind}-build",
        "candidateSourceSha": args.candidate,
        "buildMode": "Development" if development else "Source",
        "contentMarker": record["contentMarker"] if development else None,
        "result": "PASS" if succeeded else "FAIL",
        "method": f"bash tools/unity_batch.sh {'build-development' if development else 'build'}",
        "buildLog": str(args.log),
        "unityReported": {
            "result": match.group("result"),
            "sizeBytes": int(match.group("size")),
            "errors": int(match.group("errors")),
            "warnings": int(match.group("warnings")),
        },
        "measuredPayload": {
            "path": record["path"],
            "contentMarker": record["contentMarker"],
            "fileCount": record["payloadFileCountExcludingProvenance"],
            "byteLength": record["payloadBytesExcludingProvenance"],
        },
    }
    _write(Path(args.out), evidence)
    return 0 if succeeded else 1


def marker(args: argparse.Namespace) -> int:
    repo = Path(args.repo_root).resolve()
    record = build_record(repo, Path(args.build_dir).resolve(), exclude_provenance=True)
    print(record["contentMarker"])
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    tests = sub.add_parser("editmode", help="shadow-focused + full EditMode evidence")
    tests.add_argument("--results", required=True)
    tests.add_argument("--candidate", required=True)
    tests.add_argument("--out", required=True)
    tests.set_defaults(func=editmode)

    builds = sub.add_parser("build", help="development or release build evidence")
    builds.add_argument("--kind", required=True, choices=("development", "release"))
    builds.add_argument("--repo-root", default=".")
    builds.add_argument("--build-dir", required=True)
    builds.add_argument("--log", required=True)
    builds.add_argument("--candidate", required=True)
    builds.add_argument("--out", required=True)
    builds.set_defaults(func=build)

    show = sub.add_parser("marker", help="print a build's contentMarker")
    show.add_argument("--repo-root", default=".")
    show.add_argument("--build-dir", required=True)
    show.set_defaults(func=marker)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
