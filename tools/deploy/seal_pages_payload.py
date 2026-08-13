#!/usr/bin/env python3
"""Canonically merge, seal, reproduce, and remotely verify a Pages payload."""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import os
import secrets
import shutil
import stat
import sys
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Any

sys.dont_write_bytecode = True

from release_common import (
    MAX_RELEASE_BYTES,
    ReleaseError,
    canonical_json_bytes,
    committed_blob,
    git_mode,
    git_output,
    git_tree_for_stage,
    load_json,
    normalize_relative_path,
    repo_relative,
    require_sha,
    require_sha256,
    run_git,
    sha256_bytes,
    sha256_file,
    validate_exact_candidate,
    verify_working_tree_path,
    write_new_file,
)
from release_provenance import verify_provenance


MANIFEST_NAME = "deployment-payload-manifest.json"
MANIFEST_SCHEMA = "cinder-court.deployment-payload-manifest"
SEAL_SCHEMA = "cinder-court.deployment-payload-seal"
COLLISION_SCHEMA = "cinder-court.deployment-merge-collisions"
REMOTE_REPORT_SCHEMA = "cinder-court.remote-pages-payload-verification"
MERGE_POLICY = "release-web-overlay-v1"
BURST_EXCLUDE = "*_BurstDebugInformation_DoNotShip"
PROVENANCE_NAME = "release-build-provenance.json"
TOOLS = (
    "tools/deploy/deploy_pages.sh",
    "tools/deploy/seal_pages_payload.py",
    "tools/deploy/release_provenance.py",
    "tools/deploy/release_common.py",
)
RESERVED_COMPONENTS = {
    ".git",
    ".nojekyll",
    MANIFEST_NAME,
    "deployment-payload-seal.json",
    "deployment-merge-collisions.json",
    "deployment-precopy-recheck.json",
    "remote-served-file-hashes.json",
    "gh-pages-tree-comparison.json",
}


@dataclass(frozen=True)
class SourceEntry:
    path: str
    origin: str
    source_path: str
    file_path: Path
    mode: str


@dataclass(frozen=True)
class SourceTree:
    files: dict[str, SourceEntry]
    nodes: dict[str, str]


def _inside(path: Path, parent: Path) -> bool:
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def _require_external_artifact(path: Path, *forbidden_roots: Path) -> None:
    for root in forbidden_roots:
        if _inside(path, root):
            raise ReleaseError(f"external evidence artifact cannot be inside {root}: {path}")


def _is_burst_excluded(relative: str) -> bool:
    return any(fnmatch.fnmatchcase(component, BURST_EXCLUDE) for component in relative.split("/"))


def _validate_source_path(relative: str, origin: str) -> None:
    normalize_relative_path(relative, f"{origin} path")
    components = relative.split("/")
    if any(component in RESERVED_COMPONENTS for component in components):
        raise ReleaseError(f"reserved path in {origin}: {relative}")
    if PROVENANCE_NAME in components:
        if origin != "release" or relative != PROVENANCE_NAME:
            raise ReleaseError(f"release provenance has an invalid origin/path: {origin}:{relative}")


def _scan_source(root: Path, origin: str, repo: Path, exclude_burst: bool) -> SourceTree:
    if not root.is_dir() or root.is_symlink():
        raise ReleaseError(f"{origin} root is missing or not a real directory: {root}")
    files: dict[str, SourceEntry] = {}
    nodes: dict[str, str] = {}
    folded: dict[str, str] = {}

    def register(relative: str, kind: str) -> None:
        normalized = normalize_relative_path(relative, f"{origin} path")
        folded_key = unicodedata.normalize("NFC", normalized).casefold()
        prior = folded.get(folded_key)
        if prior is not None and prior != normalized:
            raise ReleaseError(f"Unicode/case-fold collision in {origin}: {prior!r} vs {normalized!r}")
        folded[folded_key] = normalized
        prior_kind = nodes.get(normalized)
        if prior_kind is not None and prior_kind != kind:
            raise ReleaseError(f"file/directory collision in {origin}: {normalized}")
        nodes[normalized] = kind

    for walk_root, dirs, names in os.walk(root, topdown=True, followlinks=False):
        walk_path = Path(walk_root)
        dirs.sort()
        names.sort()
        kept_dirs: list[str] = []
        for name in dirs:
            path = walk_path / name
            relative = path.relative_to(root).as_posix()
            if exclude_burst and _is_burst_excluded(relative):
                continue
            if path.is_symlink():
                raise ReleaseError(f"symlink is forbidden in {origin}: {relative}")
            _validate_source_path(relative, origin)
            register(relative, "directory")
            kept_dirs.append(name)
        dirs[:] = kept_dirs
        for name in names:
            path = walk_path / name
            relative = path.relative_to(root).as_posix()
            if exclude_burst and _is_burst_excluded(relative):
                continue
            _validate_source_path(relative, origin)
            try:
                mode = path.lstat().st_mode
            except OSError as exc:
                raise ReleaseError(f"cannot inspect {origin} entry {relative}: {exc}") from exc
            if stat.S_ISLNK(mode):
                raise ReleaseError(f"symlink is forbidden in {origin}: {relative}")
            if not stat.S_ISREG(mode):
                raise ReleaseError(f"non-regular entry is forbidden in {origin}: {relative}")
            register(relative, "file")
            files[relative] = SourceEntry(
                path=relative,
                origin=origin,
                source_path=repo_relative(repo, path, f"{origin} source path"),
                file_path=path,
                mode=git_mode(path),
            )
    return SourceTree(files=files, nodes=nodes)


def _validate_cross_source_nodes(release: SourceTree, web: SourceTree) -> None:
    folded: dict[str, tuple[str, str]] = {}
    for origin, tree in (("release", release), ("web", web)):
        for path, kind in tree.nodes.items():
            key = path.casefold()
            prior = folded.get(key)
            if prior is not None:
                prior_path, prior_kind = prior
                if prior_path != path:
                    raise ReleaseError(
                        f"cross-source Unicode/case-fold collision: {prior_path!r} vs {path!r}"
                    )
                if prior_kind != kind:
                    raise ReleaseError(f"cross-source file/directory collision: {path}")
            else:
                folded[key] = (path, kind)


def _copy_entry(entry: SourceEntry, stage: Path) -> None:
    destination = stage / entry.path
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(entry.file_path, destination)
    destination.chmod(0o755 if entry.mode == "100755" else 0o644)


def _collision_record(previous: SourceEntry, winner: SourceEntry) -> dict[str, Any]:
    return {
        "path": winner.path,
        "policy": "web-overrides-release",
        "losingOrigin": previous.origin,
        "losingSourcePath": previous.source_path,
        "losingByteLength": previous.file_path.stat().st_size,
        "losingSha256": sha256_file(previous.file_path),
        "winningOrigin": winner.origin,
        "winningSourcePath": winner.source_path,
        "winningByteLength": winner.file_path.stat().st_size,
        "winningSha256": sha256_file(winner.file_path),
        "identicalBytes": (
            previous.file_path.stat().st_size == winner.file_path.stat().st_size
            and sha256_file(previous.file_path) == sha256_file(winner.file_path)
        ),
    }


def _entry_record(entry: SourceEntry, stage: Path) -> dict[str, Any]:
    path = stage / entry.path
    return {
        "path": entry.path,
        "origin": entry.origin,
        "sourcePath": entry.source_path,
        "gitMode": git_mode(path),
        "byteLength": path.stat().st_size,
        "sha256": sha256_file(path),
    }


def create_merged_stage(
    repo: Path,
    release_build: Path,
    web_root: Path,
    stage: Path,
    candidate_sha: str,
    content_marker: str,
    install_manifest: bool,
) -> tuple[bytes, bytes]:
    if stage.exists() or stage.is_symlink():
        raise ReleaseError(f"deployment staging path must not exist: {stage}")
    stage.mkdir(parents=True)
    release = _scan_source(release_build, "release", repo, exclude_burst=True)
    web = _scan_source(web_root, "web", repo, exclude_burst=False)
    if PROVENANCE_NAME not in release.files:
        raise ReleaseError("Release output must contain release-build-provenance.json")
    _validate_cross_source_nodes(release, web)
    winners: dict[str, SourceEntry] = {}
    for path in sorted(release.files):
        entry = release.files[path]
        _copy_entry(entry, stage)
        winners[path] = entry
    collisions: list[dict[str, Any]] = []
    for path in sorted(web.files):
        entry = web.files[path]
        previous = winners.get(path)
        if previous is not None:
            collisions.append(_collision_record(previous, entry))
        _copy_entry(entry, stage)
        winners[path] = entry
    nojekyll = stage / ".nojekyll"
    nojekyll.write_bytes(b"")
    nojekyll.chmod(0o644)
    entries = [_entry_record(winners[path], stage) for path in sorted(winners)]
    entries.append(
        {
            "path": ".nojekyll",
            "origin": "generated",
            "sourcePath": None,
            "gitMode": "100644",
            "byteLength": 0,
            "sha256": sha256_bytes(b""),
        }
    )
    entries.sort(key=lambda item: item["path"])
    payload_bytes = sum(item["byteLength"] for item in entries)
    entry_root = sha256_bytes(canonical_json_bytes({"entries": entries}))
    manifest = {
        "schema": MANIFEST_SCHEMA,
        "version": 1,
        "mergePolicyVersion": MERGE_POLICY,
        "candidateSourceSha": candidate_sha,
        "releaseContentMarker": content_marker,
        "entryRootSha256": entry_root,
        "payloadBytes": payload_bytes,
        "entries": entries,
    }
    collision_log = {
        "schema": COLLISION_SCHEMA,
        "version": 1,
        "mergePolicyVersion": MERGE_POLICY,
        "candidateSourceSha": candidate_sha,
        "collisions": collisions,
    }
    manifest_bytes = canonical_json_bytes(manifest)
    collision_bytes = canonical_json_bytes(collision_log)
    # The manifest excludes itself to avoid a recursive hash/length contract,
    # but it is still a deployed file and therefore counts toward the hard
    # Pages payload budget. Provenance is already present in ``entries``.
    deployment_bytes = payload_bytes + len(manifest_bytes)
    if deployment_bytes > MAX_RELEASE_BYTES:
        raise ReleaseError(
            "merged Pages payload exceeds "
            f"{MAX_RELEASE_BYTES} bytes: {deployment_bytes} "
            f"(entries={payload_bytes}, manifest={len(manifest_bytes)})"
        )
    if install_manifest:
        (stage / MANIFEST_NAME).write_bytes(manifest_bytes)
        (stage / MANIFEST_NAME).chmod(0o644)
    return manifest_bytes, collision_bytes


def _tool_blobs(repo: Path) -> dict[str, str]:
    return {path: committed_blob(repo, path) for path in TOOLS}


def _unsigned_seal(
    candidate: str,
    marker: str,
    provenance_sha: str,
    manifest_sha: str,
    collision_sha: str,
    entry_root: str,
    expected_tree: str,
    tool_blobs: dict[str, str],
) -> dict[str, Any]:
    return {
        "schema": SEAL_SCHEMA,
        "version": 1,
        "mergePolicyVersion": MERGE_POLICY,
        "candidateSourceSha": candidate,
        "releaseContentMarker": marker,
        "releaseProvenanceSha256": provenance_sha,
        "payloadManifestSha256": manifest_sha,
        "collisionLogSha256": collision_sha,
        "entryRootSha256": entry_root,
        "expectedGitTreeId": expected_tree,
        "toolGitBlobs": tool_blobs,
    }


def _complete_seal(unsigned: dict[str, Any]) -> dict[str, Any]:
    digest = sha256_bytes(canonical_json_bytes(unsigned))
    return {
        **unsigned,
        "verifierAttestation": {
            "kind": "sha256-content-digest",
            "canonicalUnsignedSealSha256": digest,
            "cryptographicSignature": False,
        },
    }


def _load_seal(path: Path) -> dict[str, Any]:
    raw = path.read_bytes()
    seal = load_json(path)
    if not isinstance(seal, dict) or seal.get("schema") != SEAL_SCHEMA or seal.get("version") != 1:
        raise ReleaseError("unsupported deployment payload seal schema")
    if raw != canonical_json_bytes(seal):
        raise ReleaseError("deployment payload seal is not canonical JSON")
    attestation = seal.get("verifierAttestation")
    if not isinstance(attestation, dict):
        raise ReleaseError("deployment payload seal lacks verifier attestation")
    if attestation.get("kind") != "sha256-content-digest" or attestation.get(
        "cryptographicSignature"
    ) is not False:
        raise ReleaseError("unsupported or misleading seal attestation")
    unsigned = {key: value for key, value in seal.items() if key != "verifierAttestation"}
    expected = sha256_bytes(canonical_json_bytes(unsigned))
    if attestation.get("canonicalUnsignedSealSha256") != expected:
        raise ReleaseError("deployment payload seal attestation mismatch")
    return seal


def seal_payload(
    repo: Path,
    release_build: Path,
    web_root: Path,
    stage: Path,
    manifest_copy: Path,
    collision_log: Path,
    seal_path: Path,
) -> dict[str, Any]:
    repo = repo.resolve()
    release_build = release_build.resolve()
    web_root = web_root.resolve()
    stage = stage.resolve()
    for artifact in (manifest_copy, collision_log, seal_path):
        _require_external_artifact(artifact.resolve(), release_build, web_root, stage)
    provenance = verify_provenance(repo, release_build)
    candidate = provenance["candidateSourceSha"]
    validate_exact_candidate(repo, candidate)
    if provenance["committedWebTree"] != verify_working_tree_path(repo, "web"):
        raise ReleaseError("committed web tree changed before sealing")
    marker = provenance["builds"]["release"]["contentMarker"]
    manifest_bytes, collision_bytes = create_merged_stage(
        repo,
        release_build,
        web_root,
        stage,
        candidate,
        marker,
        install_manifest=True,
    )
    manifest = load_json(stage / MANIFEST_NAME)
    write_new_file(manifest_copy, manifest_bytes)
    write_new_file(collision_log, collision_bytes)
    expected_tree = git_tree_for_stage(repo, stage)
    unsigned = _unsigned_seal(
        candidate,
        marker,
        sha256_file(release_build / PROVENANCE_NAME),
        sha256_bytes(manifest_bytes),
        sha256_bytes(collision_bytes),
        manifest["entryRootSha256"],
        expected_tree,
        _tool_blobs(repo),
    )
    seal = _complete_seal(unsigned)
    write_new_file(seal_path, canonical_json_bytes(seal))
    return seal


def verify_sealed_payload(
    repo: Path,
    release_build: Path,
    web_root: Path,
    stage: Path,
    manifest_copy: Path,
    collision_log: Path,
    seal_path: Path,
    report_path: Path | None = None,
) -> dict[str, Any]:
    repo = repo.resolve()
    release_build = release_build.resolve()
    web_root = web_root.resolve()
    stage = stage.resolve()
    seal = _load_seal(seal_path)
    provenance = verify_provenance(repo, release_build)
    candidate = provenance["candidateSourceSha"]
    validate_exact_candidate(repo, candidate)
    if seal.get("candidateSourceSha") != candidate:
        raise ReleaseError("seal candidateSourceSha mismatch")
    marker = provenance["builds"]["release"]["contentMarker"]
    if seal.get("releaseContentMarker") != marker:
        raise ReleaseError("seal Release content marker mismatch")
    if seal.get("releaseProvenanceSha256") != sha256_file(release_build / PROVENANCE_NAME):
        raise ReleaseError("seal provenance hash mismatch")
    if seal.get("toolGitBlobs") != _tool_blobs(repo):
        raise ReleaseError("seal tool blob IDs mismatch")
    if provenance["committedWebTree"] != verify_working_tree_path(repo, "web"):
        raise ReleaseError("committed web tree changed before deployment")
    sealed_manifest_bytes = manifest_copy.read_bytes()
    if sealed_manifest_bytes != canonical_json_bytes(load_json(manifest_copy)):
        raise ReleaseError("sealed payload manifest is not canonical JSON")
    if sha256_bytes(sealed_manifest_bytes) != seal.get("payloadManifestSha256"):
        raise ReleaseError("sealed payload manifest hash mismatch")
    sealed_collision_bytes = collision_log.read_bytes()
    if sealed_collision_bytes != canonical_json_bytes(load_json(collision_log)):
        raise ReleaseError("sealed collision log is not canonical JSON")
    if sha256_bytes(sealed_collision_bytes) != seal.get("collisionLogSha256"):
        raise ReleaseError("sealed collision log hash mismatch")
    candidate_manifest, candidate_collisions = create_merged_stage(
        repo,
        release_build,
        web_root,
        stage,
        candidate,
        marker,
        install_manifest=False,
    )
    if candidate_manifest != sealed_manifest_bytes:
        raise ReleaseError("recreated payload manifest differs from sealed bytes")
    if candidate_collisions != sealed_collision_bytes:
        raise ReleaseError("recreated merge collision log differs from sealed bytes")
    manifest = load_json(manifest_copy)
    if manifest.get("entryRootSha256") != seal.get("entryRootSha256"):
        raise ReleaseError("seal entry-root hash mismatch")
    (stage / MANIFEST_NAME).write_bytes(sealed_manifest_bytes)
    (stage / MANIFEST_NAME).chmod(0o644)
    tree = git_tree_for_stage(repo, stage)
    if tree != seal.get("expectedGitTreeId"):
        raise ReleaseError(f"recreated Git tree mismatch: {tree} != {seal.get('expectedGitTreeId')}")
    report = {
        "schema": "cinder-court.deployment-precopy-recheck",
        "version": 1,
        "candidateSourceSha": candidate,
        "releaseContentMarker": marker,
        "releaseProvenanceSha256": seal["releaseProvenanceSha256"],
        "payloadManifestSha256": seal["payloadManifestSha256"],
        "entryRootSha256": seal["entryRootSha256"],
        "expectedGitTreeId": tree,
        "toolGitBlobs": seal["toolGitBlobs"],
        "result": "PASS",
    }
    if report_path is not None:
        _require_external_artifact(report_path.resolve(), release_build, web_root, stage)
        write_new_file(report_path, canonical_json_bytes(report))
    return report


def _git_file(repo: Path, commit: str, path: str) -> tuple[str, bytes]:
    listing = run_git(repo, "ls-tree", commit, "--", path).stdout.decode("utf-8", "strict").strip()
    if not listing:
        raise ReleaseError(f"remote Git tree lacks {path}")
    metadata, listed_path = listing.split("\t", 1)
    mode, kind, _blob = metadata.split(" ")
    if listed_path != path or kind != "blob" or mode not in ("100644", "100755"):
        raise ReleaseError(f"unexpected remote Git entry for {path}: {listing}")
    data = run_git(repo, "show", f"{commit}:{path}").stdout
    return mode, data


def _http_hash(url: str, expected_length: int, expected_sha: str) -> dict[str, Any]:
    request = urllib.request.Request(
        url,
        headers={"Accept-Encoding": "identity", "Cache-Control": "no-cache"},
        method="GET",
    )
    try:
        response = urllib.request.urlopen(request, timeout=60)
    except urllib.error.HTTPError as exc:
        raise ReleaseError(f"HTTP {exc.code} for {url}") from exc
    with response:
        final_url = response.geturl()
        requested_parts = urllib.parse.urlsplit(url)
        final_parts = urllib.parse.urlsplit(final_url)
        if (final_parts.scheme, final_parts.netloc) != (
            requested_parts.scheme,
            requested_parts.netloc,
        ):
            raise ReleaseError(f"cross-origin redirect: {url} -> {final_url}")
        if final_parts.path != requested_parts.path:
            raise ReleaseError(f"unexpected final URL path: {url} -> {final_url}")
        encoding = response.headers.get("Content-Encoding")
        if encoding not in (None, "", "identity"):
            raise ReleaseError(f"unexpected content transform for {url}: {encoding}")
        digest = hashlib.sha256()
        length = 0
        while True:
            chunk = response.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
            length += len(chunk)
    actual_sha = digest.hexdigest()
    if length != expected_length or actual_sha != expected_sha:
        raise ReleaseError(
            f"remote byte mismatch for {url}: length={length}/{expected_length}, sha={actual_sha}/{expected_sha}"
        )
    return {
        "url": url,
        "finalUrl": final_url,
        "status": 200,
        "byteLength": length,
        "sha256": actual_sha,
    }


def _cache_busted_url(base_url: str, path: str, nonce: str, index: int) -> str:
    if not base_url.endswith("/"):
        base_url += "/"
    encoded = urllib.parse.quote(path, safe="/")
    return urllib.parse.urljoin(base_url, encoded) + f"?__hongt_release={nonce}-{index}"


def verify_remote_payload(
    repo: Path,
    release_build: Path,
    manifest_copy: Path,
    seal_path: Path,
    remote_commit: str,
    base_url: str,
    report_path: Path,
) -> dict[str, Any]:
    repo = repo.resolve()
    seal = _load_seal(seal_path)
    # Non-strict: every assertion below is anchored in the frozen seal and the
    # REMOTE (seal -> payloadManifestSha256 -> manifest entries -> remote git
    # blobs -> served bytes, plus seal.expectedGitTreeId -> remote tree). None
    # of it reads the local working tree, and this call exists for exactly one
    # field - candidateSourceSha, cross-checked on the next line. Requiring a
    # live candidate here made the standalone recovery path in
    # docs/release-deploy-procedure.md unrunnable, because deploy evidence is
    # always committed AFTER the deploy that produced it.
    provenance = verify_provenance(
        repo, release_build.resolve(), require_live_candidate=False
    )
    if seal.get("candidateSourceSha") != provenance.get("candidateSourceSha"):
        raise ReleaseError("remote verification candidate mismatch")
    remote_commit = require_sha(
        git_output(repo, "rev-parse", remote_commit), "remote gh-pages commit"
    )
    remote_tree = git_output(repo, "rev-parse", f"{remote_commit}^{{tree}}")
    if remote_tree != seal.get("expectedGitTreeId"):
        raise ReleaseError(
            f"remote gh-pages tree mismatch: {remote_tree} != {seal.get('expectedGitTreeId')}"
        )
    manifest_bytes = manifest_copy.read_bytes()
    if sha256_bytes(manifest_bytes) != seal.get("payloadManifestSha256"):
        raise ReleaseError("remote verification manifest hash mismatch")
    manifest = load_json(manifest_copy)
    if manifest.get("schema") != MANIFEST_SCHEMA or manifest.get("version") != 1:
        raise ReleaseError("unsupported payload manifest schema")
    git_manifest_mode, git_manifest = _git_file(repo, remote_commit, MANIFEST_NAME)
    if git_manifest_mode != "100644" or git_manifest != manifest_bytes:
        raise ReleaseError("remote Git manifest blob differs from sealed bytes")
    nojekyll_mode, nojekyll_bytes = _git_file(repo, remote_commit, ".nojekyll")
    if nojekyll_mode != "100644" or nojekyll_bytes != b"":
        raise ReleaseError("remote Git .nojekyll must be an empty mode-100644 blob")
    entries = manifest.get("entries")
    if not isinstance(entries, list):
        raise ReleaseError("payload manifest entries must be a list")
    nonce = secrets.token_hex(12)
    http_results: list[dict[str, Any]] = []
    http_results.append(
        {
            "path": MANIFEST_NAME,
            **_http_hash(
                _cache_busted_url(base_url, MANIFEST_NAME, nonce, 0),
                len(manifest_bytes),
                sha256_bytes(manifest_bytes),
            ),
        }
    )
    nojekyll_http: dict[str, Any] = {"path": ".nojekyll", "status": "not-served"}
    for index, raw_entry in enumerate(entries, start=1):
        if not isinstance(raw_entry, dict):
            raise ReleaseError("payload manifest entry must be an object")
        path = normalize_relative_path(raw_entry.get("path"), "manifest entry path")
        expected_length = raw_entry.get("byteLength")
        expected_sha = require_sha256(raw_entry.get("sha256"), f"entry {path} sha256")
        if not isinstance(expected_length, int) or expected_length < 0:
            raise ReleaseError(f"invalid byte length for {path}")
        mode, git_bytes = _git_file(repo, remote_commit, path)
        if mode != raw_entry.get("gitMode"):
            raise ReleaseError(f"remote Git mode mismatch for {path}")
        if len(git_bytes) != expected_length or sha256_bytes(git_bytes) != expected_sha:
            raise ReleaseError(f"remote Git blob mismatch for {path}")
        url = _cache_busted_url(base_url, path, nonce, index)
        if path == ".nojekyll":
            try:
                nojekyll_http = {"path": path, **_http_hash(url, expected_length, expected_sha)}
            except ReleaseError as exc:
                if "HTTP 404" not in str(exc):
                    raise
            continue
        http_results.append(
            {
                "path": path,
                **_http_hash(url, expected_length, expected_sha),
            }
        )
    report = {
        "schema": REMOTE_REPORT_SCHEMA,
        "version": 1,
        "candidateSourceSha": provenance["candidateSourceSha"],
        "remoteCommitSha": remote_commit,
        "expectedGitTreeId": seal["expectedGitTreeId"],
        "remoteGitTreeId": remote_tree,
        "payloadManifestSha256": seal["payloadManifestSha256"],
        "releaseProvenanceSha256": seal["releaseProvenanceSha256"],
        "baseUrl": base_url,
        "httpEntries": http_results,
        "nojekyllGitProof": {
            "mode": nojekyll_mode,
            "byteLength": len(nojekyll_bytes),
            "sha256": sha256_bytes(nojekyll_bytes),
        },
        "nojekyllHttpProof": nojekyll_http,
        "result": "PASS",
    }
    write_new_file(report_path, canonical_json_bytes(report))
    return report


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    def common_payload(subparser: argparse.ArgumentParser) -> None:
        subparser.add_argument("--repo-root", default=".")
        subparser.add_argument("--release-build", default="build-webgl")
        subparser.add_argument("--web-root", default="web")
        subparser.add_argument("--stage-dir", required=True)
        subparser.add_argument("--manifest", required=True)
        subparser.add_argument("--collision-log", required=True)
        subparser.add_argument("--seal", required=True)

    seal = subparsers.add_parser("seal", help="create a canonical stage and detached seal")
    common_payload(seal)

    verify = subparsers.add_parser("verify", help="recreate and verify a sealed stage")
    common_payload(verify)
    verify.add_argument("--report")

    remote = subparsers.add_parser("verify-remote", help="verify remote Git and served bytes")
    remote.add_argument("--repo-root", default=".")
    remote.add_argument("--release-build", default="build-webgl")
    remote.add_argument("--manifest", required=True)
    remote.add_argument("--seal", required=True)
    remote.add_argument("--remote-commit", required=True)
    remote.add_argument("--base-url", required=True)
    remote.add_argument("--report", required=True)

    show = subparsers.add_parser("show", help="print one validated seal/provenance field")
    show.add_argument("--repo-root", default=".")
    source = show.add_mutually_exclusive_group(required=True)
    source.add_argument("--seal")
    source.add_argument("--release-build")
    show.add_argument("--field", required=True)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = _parser().parse_args(argv)
    try:
        repo = Path(args.repo_root).resolve()
        if args.command in ("seal", "verify"):
            release_build = (repo / args.release_build).resolve()
            web_root = (repo / args.web_root).resolve()
            stage = Path(args.stage_dir).resolve()
            manifest = Path(args.manifest).resolve()
            collisions = Path(args.collision_log).resolve()
            seal_path = Path(args.seal).resolve()
            if args.command == "seal":
                result = seal_payload(
                    repo, release_build, web_root, stage, manifest, collisions, seal_path
                )
                print(result["expectedGitTreeId"])
            else:
                result = verify_sealed_payload(
                    repo,
                    release_build,
                    web_root,
                    stage,
                    manifest,
                    collisions,
                    seal_path,
                    Path(args.report).resolve() if args.report else None,
                )
                print(result["expectedGitTreeId"])
        elif args.command == "verify-remote":
            result = verify_remote_payload(
                repo,
                (repo / args.release_build).resolve(),
                Path(args.manifest).resolve(),
                Path(args.seal).resolve(),
                args.remote_commit,
                args.base_url,
                Path(args.report).resolve(),
            )
            print(result["remoteGitTreeId"])
        else:
            if args.seal:
                source = _load_seal(Path(args.seal).resolve())
            else:
                # `show` prints ONE scalar from a frozen file; demanding a live
                # tree would make a historical provenance unreadable the moment
                # HEAD moves. deploy_pages.sh calls it inline where HEAD ==
                # candidate holds anyway, so strictness bought nothing there.
                source = verify_provenance(
                    repo,
                    (repo / args.release_build).resolve(),
                    require_live_candidate=False,
                )
            if args.field not in source or isinstance(source[args.field], (dict, list)):
                raise ReleaseError(f"field is missing or not scalar: {args.field}")
            print(source[args.field])
    except (OSError, ReleaseError) as exc:
        print(f"FATAL: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
