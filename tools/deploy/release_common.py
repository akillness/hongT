#!/usr/bin/env python3
"""Shared, dependency-free helpers for exact-source Pages releases."""

from __future__ import annotations

import hashlib
import fnmatch
import json
import os
import re
import stat
import subprocess
import tempfile
import unicodedata
from pathlib import Path
from typing import Any, Iterable


INPUT_ROOTS = ("Assets", "Packages", "ProjectSettings", "web", "tools/deploy")
WEBGL_RESOURCES = (
    "build-webgl.loader.js",
    "build-webgl.data.unityweb",
    "build-webgl.framework.js.unityweb",
    "build-webgl.wasm.unityweb",
)
MARKER_RE = re.compile(
    rb"/\* CinderCourt WebGL build cache version: ([0-9a-f]{16}) \*/"
)
SHA_RE = re.compile(r"^[0-9a-f]{40}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
MAX_RELEASE_BYTES = 120_000_000


class ReleaseError(RuntimeError):
    """A fail-closed release validation error."""


def canonical_json_bytes(value: Any) -> bytes:
    """Return the repository's canonical JSON representation."""

    return (
        json.dumps(
            value,
            ensure_ascii=False,
            allow_nan=False,
            separators=(",", ":"),
            sort_keys=False,
        )
        + "\n"
    ).encode("utf-8")


def _reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ReleaseError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(path: Path) -> Any:
    try:
        raw = path.read_bytes()
    except OSError as exc:
        raise ReleaseError(f"cannot read JSON {path}: {exc}") from exc
    if raw.startswith(b"\xef\xbb\xbf"):
        raise ReleaseError(f"JSON must be UTF-8 without BOM: {path}")
    try:
        return json.loads(raw.decode("utf-8"), object_pairs_hook=_reject_duplicate_keys)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ReleaseError(f"invalid JSON {path}: {exc}") from exc


def write_new_file(path: Path, data: bytes, mode: int = 0o644) -> None:
    """Create a file once. Upstream release artifacts are never rewritten."""

    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, mode)
    except FileExistsError as exc:
        raise ReleaseError(f"refusing to overwrite frozen artifact: {path}") from exc
    try:
        with os.fdopen(fd, "wb") as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
    except Exception:
        try:
            path.unlink()
        except OSError:
            pass
        raise


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as handle:
            while True:
                chunk = handle.read(1024 * 1024)
                if not chunk:
                    break
                digest.update(chunk)
    except OSError as exc:
        raise ReleaseError(f"cannot hash {path}: {exc}") from exc
    return digest.hexdigest()


def run_git(
    repo: Path,
    *args: str,
    input_bytes: bytes | None = None,
    env: dict[str, str] | None = None,
    check: bool = True,
) -> subprocess.CompletedProcess[bytes]:
    command = ["git", "-C", str(repo), *args]
    merged_env = os.environ.copy()
    if env:
        merged_env.update(env)
    process = subprocess.run(
        command,
        input=input_bytes,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=merged_env,
        check=False,
    )
    if check and process.returncode != 0:
        stderr = process.stderr.decode("utf-8", "replace").strip()
        raise ReleaseError(f"git command failed ({' '.join(command)}): {stderr}")
    return process


def git_output(repo: Path, *args: str) -> str:
    return run_git(repo, *args).stdout.decode("utf-8", "strict").strip()


def require_sha(value: Any, field: str) -> str:
    if not isinstance(value, str) or not SHA_RE.fullmatch(value):
        raise ReleaseError(f"{field} must be a lowercase 40-hex Git SHA")
    return value


def require_sha256(value: Any, field: str) -> str:
    if not isinstance(value, str) or not SHA256_RE.fullmatch(value):
        raise ReleaseError(f"{field} must be a lowercase 64-hex SHA-256")
    return value


def repo_relative(repo: Path, path: Path, field: str) -> str:
    repo = repo.resolve()
    path = path.resolve()
    try:
        relative = path.relative_to(repo)
    except ValueError as exc:
        raise ReleaseError(f"{field} must be inside the candidate worktree: {path}") from exc
    return normalize_relative_path(relative.as_posix(), field)


def normalize_relative_path(value: str, field: str = "path") -> str:
    if not isinstance(value, str) or not value:
        raise ReleaseError(f"{field} must be a non-empty relative path")
    if "\x00" in value or "\\" in value or value.startswith("/"):
        raise ReleaseError(f"unsafe {field}: {value!r}")
    parts = value.split("/")
    if any(part in ("", ".", "..") for part in parts):
        raise ReleaseError(f"unsafe {field}: {value!r}")
    normalized = unicodedata.normalize("NFC", value)
    if normalized != value:
        raise ReleaseError(f"{field} must already be NFC-normalized: {value!r}")
    return normalized


def _list_git_paths(repo: Path, *args: str) -> list[str]:
    command = list(args)
    if "--" in command:
        command.insert(command.index("--"), "-z")
    else:
        command.append("-z")
    raw = run_git(repo, *command).stdout
    if not raw:
        return []
    return [item.decode("utf-8", "surrogateescape") for item in raw.split(b"\0") if item]


def clean_status_record(
    repo: Path,
    input_roots: Iterable[str] = INPUT_ROOTS,
) -> dict[str, Any]:
    repo = repo.resolve()
    head = git_output(repo, "rev-parse", "HEAD")
    require_sha(head, "HEAD")
    tracked = _list_git_paths(
        repo,
        "status",
        "--porcelain=v1",
        "--untracked-files=no",
    )
    roots = tuple(input_roots)
    untracked = _list_git_paths(
        repo,
        "ls-files",
        "--others",
        "--exclude-standard",
        "--",
        *roots,
    )
    ignored = _list_git_paths(
        repo,
        "ls-files",
        "--others",
        "--ignored",
        "--exclude-standard",
        "--",
        *roots,
    )
    outside = outside_status_paths(repo, roots)
    projection = {
        "candidateSourceSha": head,
        "inputRoots": list(roots),
        "trackedChanges": sorted(tracked),
        "untrackedInputs": sorted(untracked),
        "ignoredInputs": sorted(ignored),
        "outsideCandidates": outside,
    }
    return {
        "schema": "cinder-court.clean-source-status",
        "version": 2,
        **projection,
        "statusDigest": sha256_bytes(canonical_json_bytes(projection)),
    }


def outside_status_paths(repo: Path, input_roots: Iterable[str] = INPUT_ROOTS) -> list[str]:
    raw = run_git(
        repo,
        "status",
        "--porcelain=v1",
        "-z",
        "--untracked-files=normal",
        "--ignored=matching",
    ).stdout
    roots = tuple(input_roots)
    paths: set[str] = set()
    for encoded in raw.split(b"\0"):
        if not encoded or encoded[:2] not in (b"??", b"!!"):
            continue
        path = encoded[3:].decode("utf-8", "surrogateescape").rstrip("/")
        if not path or any(path == root or path.startswith(root + "/") for root in roots):
            continue
        paths.add(normalize_relative_path(path, "outside candidate"))
    return sorted(paths)


def validate_outside_allow_list(
    candidates: Iterable[str], allow_patterns: Iterable[str]
) -> list[str]:
    patterns = list(allow_patterns)
    if len(patterns) != len(set(patterns)):
        raise ReleaseError("outsideInputAllowList contains duplicate patterns")
    for pattern in patterns:
        if not isinstance(pattern, str) or not pattern or pattern.startswith("/") or ".." in pattern.split("/"):
            raise ReleaseError(f"unsafe outsideInputAllowList pattern: {pattern!r}")
    normalized_candidates = sorted(set(candidates))
    rejected = [
        path
        for path in normalized_candidates
        if not any(fnmatch.fnmatchcase(path, pattern) for pattern in patterns)
    ]
    if rejected:
        raise ReleaseError(f"outside candidate is not allow-listed: {rejected}")
    return normalized_candidates


def validate_exact_candidate(
    repo: Path,
    candidate_sha: str,
    input_roots: Iterable[str] = INPUT_ROOTS,
) -> dict[str, Any]:
    candidate_sha = require_sha(candidate_sha, "candidateSourceSha")
    record = clean_status_record(repo, input_roots)
    if record["candidateSourceSha"] != candidate_sha:
        raise ReleaseError(
            f"candidate HEAD mismatch: {record['candidateSourceSha']} != {candidate_sha}"
        )
    for field in ("trackedChanges", "untrackedInputs", "ignoredInputs"):
        if record[field]:
            raise ReleaseError(f"candidate source is not clean ({field}): {record[field]}")
    return record


def require_ancestor(repo: Path, ancestor: str, descendant: str, label: str) -> None:
    process = run_git(
        repo,
        "merge-base",
        "--is-ancestor",
        ancestor,
        descendant,
        check=False,
    )
    if process.returncode != 0:
        raise ReleaseError(f"required ancestry does not hold: {label}")


def committed_tree(repo: Path, relative_path: str) -> str:
    relative_path = normalize_relative_path(relative_path, "Git tree path")
    value = git_output(repo, "rev-parse", f"HEAD:{relative_path}")
    if not SHA_RE.fullmatch(value):
        raise ReleaseError(f"HEAD:{relative_path} is not a Git tree")
    kind = git_output(repo, "cat-file", "-t", value)
    if kind != "tree":
        raise ReleaseError(f"HEAD:{relative_path} is not a tree")
    return value


def committed_blob(repo: Path, relative_path: str) -> str:
    relative_path = normalize_relative_path(relative_path, "tool path")
    blob = git_output(repo, "rev-parse", f"HEAD:{relative_path}")
    if not SHA_RE.fullmatch(blob) or git_output(repo, "cat-file", "-t", blob) != "blob":
        raise ReleaseError(f"HEAD:{relative_path} is not a committed blob")
    working = repo / relative_path
    if not working.is_file() or working.is_symlink():
        raise ReleaseError(f"committed tool is missing or not a regular file: {relative_path}")
    working_blob = git_output(repo, "hash-object", "--", relative_path)
    if working_blob != blob:
        raise ReleaseError(f"working tool bytes differ from HEAD: {relative_path}")
    return blob


def verify_working_tree_path(repo: Path, relative_root: str) -> str:
    """Require a working directory to match its committed Git tree byte-for-byte."""

    relative_root = normalize_relative_path(relative_root, "working tree root")
    tree = committed_tree(repo, relative_root)
    raw = run_git(repo, "ls-tree", "-r", "-z", "HEAD", "--", relative_root).stdout
    committed: dict[str, tuple[str, str]] = {}
    for item in raw.split(b"\0"):
        if not item:
            continue
        metadata, encoded_path = item.split(b"\t", 1)
        mode, kind, blob = metadata.decode("ascii").split(" ")
        path = encoded_path.decode("utf-8", "strict")
        if kind != "blob" or mode not in ("100644", "100755"):
            raise ReleaseError(f"unsupported committed entry in {relative_root}: {path}")
        committed[path] = (mode, blob)
    actual: dict[str, tuple[str, str]] = {}
    root = repo / relative_root
    if not root.is_dir() or root.is_symlink():
        raise ReleaseError(f"working tree root is missing: {relative_root}")
    for walk_root, dirs, names in os.walk(root, topdown=True, followlinks=False):
        walk_path = Path(walk_root)
        for directory in dirs:
            if (walk_path / directory).is_symlink():
                raise ReleaseError(f"symlink is forbidden in {relative_root}: {walk_path / directory}")
        for name in names:
            path = walk_path / name
            if path.is_symlink() or not path.is_file():
                raise ReleaseError(f"non-regular entry in {relative_root}: {path}")
            relative = path.relative_to(repo).as_posix()
            actual[relative] = (git_mode(path), git_output(repo, "hash-object", "--", relative))
    if actual != committed:
        missing = sorted(set(committed) - set(actual))
        extra = sorted(set(actual) - set(committed))
        changed = sorted(
            path for path in set(actual) & set(committed) if actual[path] != committed[path]
        )
        raise ReleaseError(
            f"working {relative_root} differs from HEAD; missing={missing}, extra={extra}, changed={changed}"
        )
    return tree


def git_mode(path: Path) -> str:
    mode = path.stat().st_mode
    if not stat.S_ISREG(mode):
        raise ReleaseError(f"unsupported non-regular file: {path}")
    return "100755" if mode & 0o111 else "100644"


def webgl_resource_names(build_dir: Path) -> tuple[str, str, str, str]:
    build_root = build_dir / "Build"
    try:
        names = [path.name for path in build_root.iterdir() if path.is_file()]
    except OSError as exc:
        raise ReleaseError(f"cannot inspect WebGL Build directory {build_root}: {exc}") from exc
    resolved: list[str] = []
    for suffix in (".loader.js", ".data", ".framework.js", ".wasm"):
        matches = sorted(
            name
            for name in names
            if name.endswith(suffix) or name.endswith(suffix + ".unityweb")
        )
        if len(matches) != 1:
            raise ReleaseError(
                f"expected one WebGL resource ending {suffix} in {build_root}, found {matches}"
            )
        resolved.append(matches[0])
    return tuple(resolved)  # type: ignore[return-value]


def compute_build_marker(build_dir: Path) -> tuple[str, list[dict[str, Any]]]:
    resource_records: list[dict[str, Any]] = []
    combined = bytearray()
    resource_names = webgl_resource_names(build_dir)
    for name in resource_names:
        path = build_dir / "Build" / name
        if not path.is_file() or path.is_symlink():
            raise ReleaseError(f"required WebGL resource is missing: {path}")
        digest = bytes.fromhex(sha256_file(path))
        combined.extend(digest)
        resource_records.append(
            {
                "path": f"Build/{name}",
                "byteLength": path.stat().st_size,
                "sha256": digest.hex(),
            }
        )
    marker = hashlib.sha256(bytes(combined)).hexdigest()[:16]
    index_path = build_dir / "index.html"
    if not index_path.is_file() or index_path.is_symlink():
        raise ReleaseError(f"WebGL index is missing: {index_path}")
    index = index_path.read_bytes()
    markers = MARKER_RE.findall(index)
    if markers != [marker.encode("ascii")]:
        raise ReleaseError(
            f"index content marker mismatch: found {[m.decode() for m in markers]}, expected {marker}"
        )
    for name in resource_names:
        token = name.encode("ascii") + b"?v=" + marker.encode("ascii")
        if index.count(token) != 1:
            raise ReleaseError(f"index must reference {name}?v={marker} exactly once")
    return marker, resource_records


def build_record(
    repo: Path,
    build_dir: Path,
    exclude_provenance: bool,
    enforce_release_size: bool = False,
) -> dict[str, Any]:
    relative = repo_relative(repo, build_dir, "build path")
    marker, resources = compute_build_marker(build_dir)
    total = 0
    files = 0
    entries: list[dict[str, Any]] = []
    for root, dirs, names in os.walk(build_dir, topdown=True, followlinks=False):
        root_path = Path(root)
        for directory in dirs:
            candidate = root_path / directory
            if candidate.is_symlink():
                raise ReleaseError(f"symlink is forbidden in build output: {candidate}")
        for name in names:
            candidate = root_path / name
            if candidate.is_symlink() or not candidate.is_file():
                raise ReleaseError(f"non-regular build entry: {candidate}")
            if exclude_provenance and candidate == build_dir / "release-build-provenance.json":
                continue
            relative_path = normalize_relative_path(
                candidate.relative_to(build_dir).as_posix(), "build entry"
            )
            size = candidate.stat().st_size
            total += size
            files += 1
            entries.append(
                {
                    "path": relative_path,
                    "gitMode": git_mode(candidate),
                    "byteLength": size,
                    "sha256": sha256_file(candidate),
                }
            )
    entries.sort(key=lambda item: item["path"])
    if enforce_release_size and total > MAX_RELEASE_BYTES:
        raise ReleaseError(
            f"Release build exceeds {MAX_RELEASE_BYTES} bytes: {total} ({build_dir})"
        )
    return {
        "path": relative,
        "contentMarker": marker,
        "payloadFileCountExcludingProvenance": files,
        "payloadBytesExcludingProvenance": total,
        "payloadRootSha256": sha256_bytes(canonical_json_bytes({"entries": entries})),
        "payloadEntries": entries,
        "indexSha256": sha256_file(build_dir / "index.html"),
        "resources": resources,
    }


def git_tree_for_stage(repo: Path, stage_dir: Path) -> str:
    """Hash a complete stage with a fresh isolated Git index."""

    stage_dir = stage_dir.resolve()
    entries: list[tuple[str, str, str]] = []
    for root, dirs, names in os.walk(stage_dir, topdown=True, followlinks=False):
        root_path = Path(root)
        for directory in dirs:
            candidate = root_path / directory
            if candidate.is_symlink():
                raise ReleaseError(f"symlink is forbidden in deployment stage: {candidate}")
        for name in names:
            candidate = root_path / name
            if candidate.is_symlink() or not candidate.is_file():
                raise ReleaseError(f"non-regular deployment entry: {candidate}")
            relative = normalize_relative_path(
                candidate.relative_to(stage_dir).as_posix(), "staged path"
            )
            # Deployment bytes are already canonical and must be hashed
            # literally. Repository clean filters (notably Git LFS) would turn
            # copied MP4/PNG payloads back into pointer blobs and make the
            # sealed tree differ from what gh-pages actually stages.
            blob = git_output(
                repo, "hash-object", "-w", "--no-filters", "--", str(candidate)
            )
            entries.append((relative, git_mode(candidate), blob))
    entries.sort(key=lambda item: item[0])
    with tempfile.NamedTemporaryFile(prefix="hongt-pages-index-", delete=True) as index:
        env = {"GIT_INDEX_FILE": index.name}
        run_git(repo, "read-tree", "--empty", env=env)
        for relative, mode, blob in entries:
            run_git(
                repo,
                "update-index",
                "--add",
                "--cacheinfo",
                f"{mode},{blob},{relative}",
                env=env,
            )
        tree = run_git(repo, "write-tree", env=env).stdout.decode("ascii").strip()
    if not SHA_RE.fullmatch(tree):
        raise ReleaseError("failed to compute expected Git tree")
    return tree


def forbid_downstream_identity_fields(value: Any, context: str) -> None:
    forbidden_fragments = (
        "releaseprovenance",
        "payloadmanifest",
        "deploymentmanifest",
        "payloadseal",
        "deploymentseal",
        "expectedgittree",
        "stagedgittree",
        "localgittree",
        "remotegittree",
        "remotecommit",
        "ghpages",
        "deployedidentity",
        "deploymentidentity",
        "sealdigest",
        "manifesthash",
    )

    def walk(node: Any, path: str) -> None:
        if isinstance(node, dict):
            for key, child in node.items():
                normalized = re.sub(r"[^a-z0-9]", "", str(key).lower())
                if any(fragment in normalized for fragment in forbidden_fragments):
                    raise ReleaseError(f"downstream identity field in {context}: {path}.{key}")
                walk(child, f"{path}.{key}")
        elif isinstance(node, list):
            for index, child in enumerate(node):
                walk(child, f"{path}[{index}]")

    walk(value, "$")
