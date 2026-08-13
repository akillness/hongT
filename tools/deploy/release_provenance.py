#!/usr/bin/env python3
"""Create and verify immutable exact-source WebGL release provenance.

The file produced here is deliberately upstream of the Pages payload manifest,
external seal, Git tree, and deployment reports. It never contains a hash or
identifier for any of those descendants.
"""

from __future__ import annotations

import argparse
import datetime as dt
import re
import sys
from pathlib import Path
from typing import Any

# BEFORE the release_common import below. tools/deploy/ is an INPUT_ROOT
# (release_common.INPUT_ROOTS), so importing a sibling writes
# tools/deploy/__pycache__/, and release_common.py:199 scopes the clean check to
# INPUT_ROOTS INCLUDING gitignored paths - the next `snapshot-clean` then
# refuses with "cannot freeze dirty source snapshot (ignoredInputs)" over a
# directory nobody knowingly created. run_release_gate.sh and deploy_pages.sh
# export PYTHONDONTWRITEBYTECODE, but a bare `python3 tools/deploy/...` inherits
# nothing - and a bare call is exactly what the propagation-lag recovery in
# docs/release-deploy-procedure.md instructs.
#
# This guard was already here and correct. seal_pages_payload.py was MISSING it,
# which is what actually wrote the .pyc that blocked a snapshot on 2026-08-13 -
# so the fix belonged there, and this one only needed the explanation.
sys.dont_write_bytecode = True

from release_common import (
    INPUT_ROOTS,
    ReleaseError,
    build_record,
    canonical_json_bytes,
    clean_status_record,
    committed_tree,
    forbid_downstream_identity_fields,
    load_json,
    normalize_relative_path,
    repo_relative,
    require_ancestor,
    require_sha,
    require_sha256,
    sha256_file,
    validate_exact_candidate,
    validate_outside_allow_list,
    verify_working_tree_path,
    write_new_file,
)


SCHEMA = "cinder-court.release-build-provenance"
CLEAN_SCHEMA = "cinder-court.clean-source-status"
BUILD_MODES = {"Development", "Source"}
DEFAULT_OUTPUT = "release-build-provenance.json"
SOURCE_UPSTREAM_RE = re.compile(r"^origin/[A-Za-z0-9][A-Za-z0-9._/-]*$")
REQUIRED_EVIDENCE_MODES = {
    "shadow-focused-editmode": "Source",
    "full-editmode": "Source",
    "development-build": "Development",
    "release-build": "Source",
    "browser-shadow-desktop": "Development",
    "browser-shadow-mobile": "Development",
}


def _require_mapping(value: Any, field: str) -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ReleaseError(f"{field} must be an object")
    return value


def _require_string(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value:
        raise ReleaseError(f"{field} must be a non-empty string")
    return value


def _require_string_list(value: Any, field: str, allow_empty: bool = False) -> list[str]:
    if not isinstance(value, list) or (not value and not allow_empty):
        raise ReleaseError(f"{field} must be a {'possibly empty ' if allow_empty else ''}list")
    result: list[str] = []
    for index, item in enumerate(value):
        result.append(_require_string(item, f"{field}[{index}]") )
    return result


def _resolve_repo_path(repo: Path, value: Any, field: str) -> tuple[Path, str]:
    relative = normalize_relative_path(_require_string(value, field), field)
    path = (repo / relative).resolve()
    repo_relative(repo, path, field)
    return path, relative


def _validate_clean_snapshot(
    repo: Path,
    value: Any,
    field: str,
    candidate_sha: str,
) -> dict[str, Any]:
    path, relative = _resolve_repo_path(repo, value, field)
    record = _require_mapping(load_json(path), field)
    if record.get("schema") != CLEAN_SCHEMA or record.get("version") != 2:
        raise ReleaseError(f"invalid clean-status schema: {relative}")
    if record.get("candidateSourceSha") != candidate_sha:
        raise ReleaseError(f"clean-status candidate mismatch: {relative}")
    if record.get("inputRoots") != list(INPUT_ROOTS):
        raise ReleaseError(f"clean-status input roots mismatch: {relative}")
    for key in ("trackedChanges", "untrackedInputs", "ignoredInputs"):
        if record.get(key) != []:
            raise ReleaseError(f"clean-status record is not clean ({key}): {relative}")
    projection = {
        "candidateSourceSha": record["candidateSourceSha"],
        "inputRoots": record["inputRoots"],
        "trackedChanges": record["trackedChanges"],
        "untrackedInputs": record["untrackedInputs"],
        "ignoredInputs": record["ignoredInputs"],
        "outsideCandidates": record.get("outsideCandidates"),
    }
    from release_common import sha256_bytes

    expected_digest = sha256_bytes(canonical_json_bytes(projection))
    if record.get("statusDigest") != expected_digest:
        raise ReleaseError(f"clean-status digest mismatch: {relative}")
    return {
        "path": relative,
        "byteLength": path.stat().st_size,
        "sha256": sha256_file(path),
        "statusDigest": expected_digest,
        "outsideCandidates": record.get("outsideCandidates"),
    }


def _evidence_records(
    repo: Path,
    metadata: dict[str, Any],
    candidate_sha: str,
    development_marker: str,
) -> list[dict[str, Any]]:
    raw_records = metadata.get("evidence")
    if not isinstance(raw_records, list) or not raw_records:
        raise ReleaseError("metadata.evidence must contain at least one frozen artifact")
    seen_ids: set[str] = set()
    seen_paths: set[str] = set()
    result: list[dict[str, Any]] = []
    for index, raw in enumerate(raw_records):
        item = _require_mapping(raw, f"metadata.evidence[{index}]")
        evidence_id = _require_string(item.get("evidenceId"), f"evidence[{index}].evidenceId")
        if evidence_id in seen_ids:
            raise ReleaseError(f"duplicate evidenceId: {evidence_id}")
        seen_ids.add(evidence_id)
        path, relative = _resolve_repo_path(repo, item.get("path"), f"evidence[{index}].path")
        if relative in seen_paths:
            raise ReleaseError(f"duplicate evidence path: {relative}")
        seen_paths.add(relative)
        if not path.is_file() or path.is_symlink():
            raise ReleaseError(f"evidence must be a regular file: {relative}")
        build_mode = _require_string(item.get("buildMode"), f"evidence[{index}].buildMode")
        if build_mode not in BUILD_MODES:
            raise ReleaseError(f"unsupported evidence buildMode: {build_mode}")
        marker = item.get("contentMarker")
        if build_mode == "Development":
            if marker != development_marker:
                raise ReleaseError(f"Development evidence marker mismatch: {relative}")
        elif marker is not None:
            raise ReleaseError(f"Source evidence must use a null contentMarker: {relative}")
        if path.suffix.lower() == ".json":
            payload = load_json(path)
            if not isinstance(payload, dict):
                raise ReleaseError(f"JSON evidence must be a top-level object: {relative}")
            forbid_downstream_identity_fields(payload, f"evidence {relative}")
            if payload.get("evidenceId") != evidence_id:
                raise ReleaseError(f"JSON evidenceId mismatch: {relative}")
            if payload.get("candidateSourceSha") != candidate_sha:
                raise ReleaseError(f"JSON evidence candidate mismatch: {relative}")
            if payload.get("buildMode") != build_mode:
                raise ReleaseError(f"JSON evidence buildMode mismatch: {relative}")
            if payload.get("contentMarker") != marker:
                raise ReleaseError(f"JSON evidence contentMarker mismatch: {relative}")
            if payload.get("result") != "PASS":
                raise ReleaseError(f"JSON evidence did not PASS: {relative}")
        result.append(
            {
                "evidenceId": evidence_id,
                "path": relative,
                "buildMode": build_mode,
                "contentMarker": marker,
                "byteLength": path.stat().st_size,
                "sha256": sha256_file(path),
            }
        )
    required_ids = set(REQUIRED_EVIDENCE_MODES)
    if seen_ids != required_ids:
        raise ReleaseError(
            "metadata.evidence IDs must exactly match the stage-shadow policy; "
            f"missing={sorted(required_ids - seen_ids)}, extra={sorted(seen_ids - required_ids)}"
        )
    for record in result:
        expected_mode = REQUIRED_EVIDENCE_MODES[record["evidenceId"]]
        if record["buildMode"] != expected_mode:
            raise ReleaseError(
                f"evidence {record['evidenceId']} must use buildMode={expected_mode}"
            )
    return sorted(result, key=lambda item: (item["evidenceId"], item["path"]))


def _validate_probe_hashes(value: Any) -> dict[str, str]:
    probes = _require_mapping(value, "metadata.probeHashes")
    return {
        "baseline": require_sha256(probes.get("baseline"), "probeHashes.baseline"),
        "candidate": require_sha256(probes.get("candidate"), "probeHashes.candidate"),
    }


def _validate_timestamp(value: Any) -> str:
    timestamp = _require_string(value, "metadata.generatedAt")
    try:
        parsed = dt.datetime.fromisoformat(timestamp.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ReleaseError("metadata.generatedAt must be an ISO-8601 timestamp") from exc
    if parsed.tzinfo is None:
        raise ReleaseError("metadata.generatedAt must include a timezone")
    return timestamp


def _validate_source_upstream(value: Any) -> str:
    upstream = _require_string(value, "sourceUpstream")
    if not SOURCE_UPSTREAM_RE.fullmatch(upstream) or ".." in upstream.split("/"):
        raise ReleaseError("sourceUpstream must be an origin/<branch> remote-tracking ref")
    return upstream


def create_provenance(
    repo: Path,
    metadata_path: Path,
    development_build: Path,
    release_build: Path,
    output: Path,
) -> dict[str, Any]:
    repo = repo.resolve()
    metadata = _require_mapping(load_json(metadata_path), "metadata")
    forbid_downstream_identity_fields(metadata, "provenance metadata")
    release_base = require_sha(metadata.get("releaseBaseSha"), "releaseBaseSha")
    baseline_probe = require_sha(metadata.get("baselineProbeSha"), "baselineProbeSha")
    candidate = require_sha(metadata.get("candidateSourceSha"), "candidateSourceSha")
    current_status = validate_exact_candidate(repo, candidate)
    require_ancestor(repo, release_base, baseline_probe, "releaseBaseSha -> baselineProbeSha")
    require_ancestor(repo, baseline_probe, candidate, "baselineProbeSha -> candidateSourceSha")
    web_tree = verify_working_tree_path(repo, "web")
    source_upstream = _validate_source_upstream(metadata.get("sourceUpstream"))
    unity_version = _require_string(metadata.get("unityVersion"), "unityVersion")
    commands = _require_string_list(metadata.get("commands"), "metadata.commands")
    allow_list = _require_string_list(
        metadata.get("outsideInputAllowList", []),
        "metadata.outsideInputAllowList",
        allow_empty=True,
    )
    clean = _require_mapping(metadata.get("cleanStatus"), "metadata.cleanStatus")
    clean_records = {
        "pre": _validate_clean_snapshot(repo, clean.get("pre"), "cleanStatus.pre", candidate),
        "post": _validate_clean_snapshot(repo, clean.get("post"), "cleanStatus.post", candidate),
    }
    outside_state = {
        "current": validate_outside_allow_list(
            current_status.get("outsideCandidates", []), allow_list
        ),
        "pre": validate_outside_allow_list(
            clean_records["pre"].get("outsideCandidates", []), allow_list
        ),
        "post": validate_outside_allow_list(
            clean_records["post"].get("outsideCandidates", []), allow_list
        ),
    }
    release_relative = repo_relative(repo, release_build, "release build")
    output_relative = repo_relative(repo, output, "provenance output")
    expected_output = f"{release_relative}/{DEFAULT_OUTPUT}"
    if output_relative != expected_output:
        raise ReleaseError(f"provenance output must be {expected_output}")
    if output.exists() or output.is_symlink():
        raise ReleaseError(f"provenance is already frozen: {output_relative}")
    if development_build.resolve() == release_build.resolve():
        raise ReleaseError("Development and Release build paths must be distinct")
    development = build_record(repo, development_build, exclude_provenance=True)
    release = build_record(
        repo, release_build, exclude_provenance=True, enforce_release_size=True
    )
    if development["contentMarker"] == release["contentMarker"]:
        raise ReleaseError("Development and Release content markers must be distinct")
    evidence = _evidence_records(repo, metadata, candidate, development["contentMarker"])
    provenance = {
        "schema": SCHEMA,
        "version": 1,
        "generatedAt": _validate_timestamp(metadata.get("generatedAt")),
        "releaseBaseSha": release_base,
        "baselineProbeSha": baseline_probe,
        "candidateSourceSha": candidate,
        "sourceUpstream": source_upstream,
        "probeHashes": _validate_probe_hashes(metadata.get("probeHashes")),
        "unityVersion": unity_version,
        "inputRoots": list(INPUT_ROOTS),
        "outsideInputAllowList": allow_list,
        "outsideInputState": outside_state,
        "cleanStatus": clean_records,
        "committedWebTree": web_tree,
        "commands": commands,
        "builds": {"development": development, "release": release},
        "evidence": evidence,
    }
    forbid_downstream_identity_fields(provenance, "release provenance")
    write_new_file(output, canonical_json_bytes(provenance), mode=0o444)
    return provenance


def _verify_file_record(repo: Path, record: Any, field: str) -> None:
    item = _require_mapping(record, field)
    path, relative = _resolve_repo_path(repo, item.get("path"), f"{field}.path")
    if not path.is_file() or path.is_symlink():
        raise ReleaseError(f"frozen file is missing: {relative}")
    if item.get("byteLength") != path.stat().st_size or item.get("sha256") != sha256_file(path):
        raise ReleaseError(f"frozen file changed: {relative}")


def verify_provenance(
    repo: Path,
    release_build: Path,
    *,
    require_live_candidate: bool = True,
) -> dict[str, Any]:
    """Validate a frozen release provenance.

    `require_live_candidate` gates the three assertions that describe the LOCAL
    WORKING TREE rather than the frozen artifact: HEAD == candidate, working
    web/ == HEAD:web, and the current outside-input allow list. Callers that are
    about to act on the local tree (create, seal, deploy) need them. Remote
    verification does not: its entire chain is anchored in the frozen seal and
    the remote (seal -> payloadManifestSha256 -> 28 manifest entries -> remote
    git blobs -> served HTTP bytes, plus seal.expectedGitTreeId -> remote tree),
    and it calls this function for exactly ONE field, candidateSourceSha at the
    single call site in verify_remote_payload.

    Why this flag exists (2026-08-13, proven by an isolated subprocess run with
    only these gates stubbed: RESULT=PASS, every remote assertion still real).
    verify-remote normally runs inline in deploy_pages.sh right after the push,
    where HEAD == candidate holds by construction, so the coupling is free and
    invisible. It only misfires on the STANDALONE recovery path — and that path
    is exactly what docs/release-deploy-procedure.md now prescribes when Pages
    propagation outlasts the retry window. Deploy evidence is committed AFTER a
    deploy, so HEAD is always ahead by then and the documented recovery could
    never run. The commit that wrote the recovery down is the one that broke it.

    The web/ coupling is the subtler half and would have survived a fix aimed
    only at the candidate check: it passed on 2026-08-13 solely because web/
    happened not to move (tree ff46d3a2 at both commits). web/ does move in this
    repo. It is also redundant for remote purposes — the three web-origin payload
    entries are already pinned by sha256 in the sealed manifest and re-checked
    against the remote blobs.
    """

    repo = repo.resolve()
    release_build = release_build.resolve()
    path = release_build / DEFAULT_OUTPUT
    if not path.is_file() or path.is_symlink():
        raise ReleaseError(f"release provenance is missing: {path}")
    raw_provenance = path.read_bytes()
    provenance = _require_mapping(load_json(path), "release provenance")
    if raw_provenance != canonical_json_bytes(provenance):
        raise ReleaseError("release provenance is not canonical immutable JSON")
    if provenance.get("schema") != SCHEMA or provenance.get("version") != 1:
        raise ReleaseError("unsupported release provenance schema")
    forbid_downstream_identity_fields(provenance, "release provenance")
    candidate = require_sha(provenance.get("candidateSourceSha"), "candidateSourceSha")
    release_base = require_sha(provenance.get("releaseBaseSha"), "releaseBaseSha")
    baseline_probe = require_sha(provenance.get("baselineProbeSha"), "baselineProbeSha")
    current_status = (
        validate_exact_candidate(repo, candidate) if require_live_candidate else None
    )
    require_ancestor(repo, release_base, baseline_probe, "releaseBaseSha -> baselineProbeSha")
    require_ancestor(repo, baseline_probe, candidate, "baselineProbeSha -> candidateSourceSha")
    if provenance.get("inputRoots") != list(INPUT_ROOTS):
        raise ReleaseError("provenance input roots mismatch")
    if require_live_candidate and provenance.get("committedWebTree") != verify_working_tree_path(
        repo, "web"
    ):
        raise ReleaseError("committed web tree mismatch")
    builds = _require_mapping(provenance.get("builds"), "builds")
    development_stored = _require_mapping(builds.get("development"), "builds.development")
    release_stored = _require_mapping(builds.get("release"), "builds.release")
    development_path, _ = _resolve_repo_path(
        repo, development_stored.get("path"), "builds.development.path"
    )
    if development_path.resolve() == release_build.resolve():
        raise ReleaseError("Development and Release build paths are not distinct")
    if build_record(repo, development_path, exclude_provenance=True) != development_stored:
        raise ReleaseError("Development build bytes no longer match provenance")
    if build_record(
        repo, release_build, exclude_provenance=True, enforce_release_size=True
    ) != release_stored:
        raise ReleaseError("Release build bytes no longer match provenance")
    if development_stored.get("contentMarker") == release_stored.get("contentMarker"):
        raise ReleaseError("Development and Release markers are not distinct")
    clean = _require_mapping(provenance.get("cleanStatus"), "cleanStatus")
    allow_list = _require_string_list(
        provenance.get("outsideInputAllowList", []),
        "outsideInputAllowList",
        allow_empty=True,
    )
    stored_outside = _require_mapping(
        provenance.get("outsideInputState"), "outsideInputState"
    )
    # Third liveness assertion: `current_status` describes TODAY's tree, so it
    # is None when the caller asked not to require a live candidate. The pre/post
    # allow-list checks below read the FROZEN snapshots instead and stay
    # unconditional - those are part of the artifact. (require_ancestor above is
    # also unconditional and correctly so: it walks committed history, which is
    # HEAD-independent, and a sealed lineage should verify forever.)
    if require_live_candidate:
        validate_outside_allow_list(current_status.get("outsideCandidates", []), allow_list)
    for label in ("pre", "post"):
        record = _require_mapping(clean.get(label), f"cleanStatus.{label}")
        _verify_file_record(repo, record, f"cleanStatus.{label}")
        snapshot_path = repo / record["path"]
        snapshot = _require_mapping(load_json(snapshot_path), f"cleanStatus.{label}")
        if snapshot.get("statusDigest") != record.get("statusDigest"):
            raise ReleaseError(f"clean-status digest changed: {label}")
        if snapshot.get("candidateSourceSha") != candidate:
            raise ReleaseError(f"clean-status candidate changed: {label}")
        for key in ("trackedChanges", "untrackedInputs", "ignoredInputs"):
            if snapshot.get(key) != []:
                raise ReleaseError(f"clean-status is not empty: {label}.{key}")
        outside = validate_outside_allow_list(
            snapshot.get("outsideCandidates", []), allow_list
        )
        if stored_outside.get(label) != outside:
            raise ReleaseError(f"outside-input state changed: {label}")
    evidence = provenance.get("evidence")
    if not isinstance(evidence, list) or not evidence:
        raise ReleaseError("release provenance has no frozen evidence")
    seen_ids: set[str] = set()
    development_marker = development_stored.get("contentMarker")
    for index, record in enumerate(evidence):
        item = _require_mapping(record, f"evidence[{index}]")
        evidence_id = _require_string(item.get("evidenceId"), f"evidence[{index}].evidenceId")
        if evidence_id in seen_ids:
            raise ReleaseError(f"duplicate frozen evidenceId: {evidence_id}")
        seen_ids.add(evidence_id)
        expected_mode = REQUIRED_EVIDENCE_MODES.get(evidence_id)
        if expected_mode is None or item.get("buildMode") != expected_mode:
            raise ReleaseError(f"frozen evidence policy mismatch: {evidence_id}")
        expected_marker = development_marker if expected_mode == "Development" else None
        if item.get("contentMarker") != expected_marker:
            raise ReleaseError(f"frozen evidence marker mismatch: {evidence_id}")
        _verify_file_record(repo, item, f"evidence[{index}]")
        evidence_path = repo / item["path"]
        if evidence_path.suffix.lower() == ".json":
            payload = load_json(evidence_path)
            if not isinstance(payload, dict) or payload.get("result") != "PASS":
                raise ReleaseError(f"frozen JSON evidence did not PASS: {item['path']}")
            if payload.get("evidenceId") != evidence_id:
                raise ReleaseError(f"frozen JSON evidenceId mismatch: {item['path']}")
            if payload.get("candidateSourceSha") != candidate:
                raise ReleaseError(f"frozen JSON evidence candidate mismatch: {item['path']}")
            if payload.get("buildMode") != expected_mode:
                raise ReleaseError(f"frozen JSON evidence mode mismatch: {item['path']}")
            if payload.get("contentMarker") != expected_marker:
                raise ReleaseError(f"frozen JSON evidence marker mismatch: {item['path']}")
            forbid_downstream_identity_fields(payload, f"evidence {item['path']}")
    if seen_ids != set(REQUIRED_EVIDENCE_MODES):
        raise ReleaseError("frozen evidence set does not match stage-shadow policy")
    _validate_probe_hashes(provenance.get("probeHashes"))
    _validate_timestamp(provenance.get("generatedAt"))
    _validate_source_upstream(provenance.get("sourceUpstream"))
    _require_string(provenance.get("unityVersion"), "unityVersion")
    _require_string_list(provenance.get("commands"), "commands")
    return provenance


def create_clean_snapshot(repo: Path, output: Path) -> dict[str, Any]:
    record = clean_status_record(repo)
    for field in ("trackedChanges", "untrackedInputs", "ignoredInputs"):
        if record[field]:
            raise ReleaseError(f"cannot freeze dirty source snapshot ({field}): {record[field]}")
    write_new_file(output, canonical_json_bytes(record))
    return record


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    snapshot = subparsers.add_parser("snapshot-clean", help="freeze a clean exact-SHA status record")
    snapshot.add_argument("--repo-root", default=".")
    snapshot.add_argument("--output", required=True)

    create = subparsers.add_parser("create", help="create immutable release provenance")
    create.add_argument("--repo-root", default=".")
    create.add_argument("--metadata", required=True)
    create.add_argument("--development-build", required=True)
    create.add_argument("--release-build", default="build-webgl")
    create.add_argument("--output")

    verify = subparsers.add_parser("verify", help="recompute and verify release provenance")
    verify.add_argument("--repo-root", default=".")
    verify.add_argument("--release-build", default="build-webgl")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        repo = Path(args.repo_root).resolve()
        if args.command == "snapshot-clean":
            record = create_clean_snapshot(repo, Path(args.output).resolve())
            print(record["statusDigest"])
        elif args.command == "create":
            release_build = (repo / args.release_build).resolve()
            output = Path(args.output).resolve() if args.output else release_build / DEFAULT_OUTPUT
            provenance = create_provenance(
                repo,
                Path(args.metadata).resolve(),
                (repo / args.development_build).resolve(),
                release_build,
                output,
            )
            print(provenance["candidateSourceSha"])
        else:
            provenance = verify_provenance(repo, (repo / args.release_build).resolve())
            print(provenance["candidateSourceSha"])
    except ReleaseError as exc:
        print(f"FATAL: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
