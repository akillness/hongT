#!/usr/bin/env python3
"""Focused non-Unity tests for exact-source Pages release tooling."""

from __future__ import annotations

import functools
import hashlib
import http.server
import json
import os
import shutil
import subprocess
import sys
import tempfile
import threading
import unittest
from unittest import mock
from pathlib import Path

sys.dont_write_bytecode = True


SOURCE_ROOT = Path(__file__).resolve().parents[2]
DEPLOY_TOOLS = SOURCE_ROOT / "tools" / "deploy"
sys.path.insert(0, str(DEPLOY_TOOLS))

from release_common import (  # noqa: E402
    ReleaseError,
    WEBGL_RESOURCES,
    canonical_json_bytes,
    clean_status_record,
    git_output,
    load_json,
    sha256_bytes,
    validate_exact_candidate,
)
from release_provenance import (  # noqa: E402
    REQUIRED_EVIDENCE_MODES,
    create_clean_snapshot,
    create_provenance,
    verify_provenance,
)
import seal_pages_payload  # noqa: E402
from seal_pages_payload import (  # noqa: E402
    MANIFEST_NAME,
    create_merged_stage,
    seal_payload,
    verify_remote_payload,
    verify_sealed_payload,
)


def git(repo: Path, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        check=check,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def load_json_bytes(raw: bytes) -> dict[str, object]:
    value = json.loads(raw.decode("utf-8"))
    if not isinstance(value, dict):
        raise AssertionError("expected a top-level JSON object")
    return value


class ReleaseFixture:
    def __init__(self, root: Path) -> None:
        self.repo = root / "repo"
        self.repo.mkdir()
        git(self.repo, "init", "-q")
        git(self.repo, "config", "user.email", "release-tests@example.invalid")
        git(self.repo, "config", "user.name", "Release Tests")
        for directory in ("Assets", "Packages", "ProjectSettings", "web", "tools/deploy"):
            path = self.repo / directory
            path.mkdir(parents=True, exist_ok=True)
            (path / ".keep").write_text("tracked\n", encoding="utf-8")
        for name in (
            "deploy_pages.sh",
            "release_common.py",
            "release_provenance.py",
            "seal_pages_payload.py",
        ):
            shutil.copy2(DEPLOY_TOOLS / name, self.repo / "tools/deploy" / name)
        (self.repo / "web/shared.txt").write_text("committed web winner\n", encoding="utf-8")
        (self.repo / ".gitignore").write_text(
            "build-development/\nbuild-webgl/\nevidence/\nstage-*/\n",
            encoding="utf-8",
        )
        git(self.repo, "add", "--", ".gitignore", "Assets", "Packages", "ProjectSettings", "web", "tools/deploy")
        git(self.repo, "commit", "-q", "-m", "fixture candidate")
        self.candidate = git_output(self.repo, "rev-parse", "HEAD")
        self.development = self.repo / "build-development"
        self.release = self.repo / "build-webgl"
        self.development_marker = self._write_build(self.development, b"development")
        self.release_marker = self._write_build(self.release, b"release")
        (self.release / "shared.txt").write_text("release loser\n", encoding="utf-8")
        burst = self.release / "Build/build-webgl_BurstDebugInformation_DoNotShip"
        burst.mkdir()
        (burst / "debug.txt").write_text("must not ship\n", encoding="utf-8")
        self.evidence_dir = self.repo / "evidence"
        self.evidence_dir.mkdir()
        self.pre = self.evidence_dir / "candidate-clean-pre.json"
        self.post = self.evidence_dir / "candidate-clean-post.json"
        create_clean_snapshot(self.repo, self.pre)
        create_clean_snapshot(self.repo, self.post)
        self.evidence_records: list[dict[str, object]] = []
        for evidence_id, build_mode in REQUIRED_EVIDENCE_MODES.items():
            marker = self.development_marker if build_mode == "Development" else None
            path = self.evidence_dir / f"{evidence_id}.json"
            path.write_bytes(
                canonical_json_bytes(
                    {
                        "evidenceId": evidence_id,
                        "candidateSourceSha": self.candidate,
                        "buildMode": build_mode,
                        "contentMarker": marker,
                        "result": "PASS",
                    }
                )
            )
            self.evidence_records.append(
                {
                    "evidenceId": evidence_id,
                    "path": f"evidence/{path.name}",
                    "buildMode": build_mode,
                    "contentMarker": marker,
                }
            )
        self.evidence = self.evidence_dir / "shadow-focused-editmode.json"
        self.metadata = self.evidence_dir / "provenance-metadata.json"
        self._write_metadata()
        self.provenance_path = self.release / "release-build-provenance.json"
        create_provenance(
            self.repo,
            self.metadata,
            self.development,
            self.release,
            self.provenance_path,
        )

    def _write_build(self, root: Path, seed: bytes) -> str:
        build = root / "Build"
        build.mkdir(parents=True)
        resource_hashes = bytearray()
        for index, name in enumerate(WEBGL_RESOURCES):
            payload = seed + b":" + str(index).encode("ascii") + b":" + name.encode("ascii")
            (build / name).write_bytes(payload)
            resource_hashes.extend(hashlib.sha256(payload).digest())
        marker = hashlib.sha256(bytes(resource_hashes)).hexdigest()[:16]
        references = "\n".join(f"Build/{name}?v={marker}" for name in WEBGL_RESOURCES)
        (root / "index.html").write_text(
            f"/* CinderCourt WebGL build cache version: {marker} */\n{references}\n",
            encoding="utf-8",
        )
        return marker

    def _write_metadata(self) -> None:
        self.metadata.write_bytes(
            canonical_json_bytes(
                {
                    "releaseBaseSha": self.candidate,
                    "baselineProbeSha": self.candidate,
                    "candidateSourceSha": self.candidate,
                    "sourceUpstream": "origin/main",
                    "probeHashes": {"baseline": "0" * 64, "candidate": "1" * 64},
                    "unityVersion": "6000.5.6f1",
                    "outsideInputAllowList": [
                        "build-development",
                        "build-webgl",
                        "evidence",
                        "stage-*",
                    ],
                    "cleanStatus": {
                        "pre": "evidence/candidate-clean-pre.json",
                        "post": "evidence/candidate-clean-post.json",
                    },
                    "commands": ["fixture Development build", "fixture Release build"],
                    "generatedAt": "2026-08-12T00:00:00Z",
                    "evidence": self.evidence_records,
                }
            )
        )


class ReleasePayloadTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory(prefix="hongt-release-test-")
        self.root = Path(self.temp.name)

    def tearDown(self) -> None:
        self.temp.cleanup()

    def test_provenance_is_exact_immutable_and_recomputed(self) -> None:
        fixture = ReleaseFixture(self.root)
        provenance = verify_provenance(fixture.repo, fixture.release)
        self.assertEqual(provenance["candidateSourceSha"], fixture.candidate)
        self.assertEqual(provenance["committedWebTree"], git_output(fixture.repo, "rev-parse", "HEAD:web"))
        with self.assertRaisesRegex(ReleaseError, "already frozen"):
            create_provenance(
                fixture.repo,
                fixture.metadata,
                fixture.development,
                fixture.release,
                fixture.provenance_path,
            )
        fixture.provenance_path.chmod(0o644)
        fixture.provenance_path.write_bytes(fixture.provenance_path.read_bytes() + b" ")
        with self.assertRaises(ReleaseError):
            verify_provenance(fixture.repo, fixture.release)

    def test_full_merge_manifest_seal_and_reproduction(self) -> None:
        fixture = ReleaseFixture(self.root)
        stage_one = fixture.repo / "stage-one"
        manifest = fixture.evidence_dir / "deployment-payload-manifest.json"
        collisions = fixture.evidence_dir / "deployment-merge-collisions.json"
        seal_path = fixture.evidence_dir / "deployment-payload-seal.json"
        seal = seal_payload(
            fixture.repo,
            fixture.release,
            fixture.repo / "web",
            stage_one,
            manifest,
            collisions,
            seal_path,
        )
        self.assertEqual((stage_one / "shared.txt").read_text(), "committed web winner\n")
        self.assertFalse(any("BurstDebugInformation_DoNotShip" in str(path) for path in stage_one.rglob("*")))
        self.assertEqual((stage_one / ".nojekyll").read_bytes(), b"")
        payload = load_json(manifest)
        paths = [entry["path"] for entry in payload["entries"]]
        self.assertIn(".nojekyll", paths)
        self.assertIn("release-build-provenance.json", paths)
        self.assertNotIn(MANIFEST_NAME, paths)
        self.assertTrue((stage_one / MANIFEST_NAME).is_file())
        collision_payload = load_json(collisions)
        self.assertEqual(collision_payload["collisions"][0]["policy"], "web-overrides-release")
        self.assertEqual(collision_payload["collisions"][0]["path"], "shared.txt")
        stage_two = fixture.repo / "stage-two"
        report = fixture.evidence_dir / "deployment-precopy-recheck.json"
        verified = verify_sealed_payload(
            fixture.repo,
            fixture.release,
            fixture.repo / "web",
            stage_two,
            manifest,
            collisions,
            seal_path,
            report,
        )
        self.assertEqual(verified["expectedGitTreeId"], seal["expectedGitTreeId"])
        self.assertEqual(
            sorted(path.relative_to(stage_one) for path in stage_one.rglob("*") if path.is_file()),
            sorted(path.relative_to(stage_two) for path in stage_two.rglob("*") if path.is_file()),
        )

    def test_final_payload_budget_includes_the_deployed_manifest(self) -> None:
        fixture = ReleaseFixture(self.root)
        probe_stage = fixture.repo / "stage-budget-probe"
        manifest_bytes, _ = create_merged_stage(
            fixture.repo,
            fixture.release,
            fixture.repo / "web",
            probe_stage,
            fixture.candidate,
            fixture.release_marker,
            False,
        )
        entry_bytes = load_json_bytes(manifest_bytes)["payloadBytes"]
        shutil.rmtree(probe_stage)

        # Entries alone remain below this limit. Only the canonical manifest
        # pushes the actual deployable tree over it.
        limit = entry_bytes + len(manifest_bytes) - 1
        with mock.patch.object(seal_pages_payload, "MAX_RELEASE_BYTES", limit):
            with self.assertRaisesRegex(ReleaseError, "merged Pages payload exceeds"):
                create_merged_stage(
                    fixture.repo,
                    fixture.release,
                    fixture.repo / "web",
                    fixture.repo / "stage-budget-fail",
                    fixture.candidate,
                    fixture.release_marker,
                    False,
                )

    def test_clean_gate_rejects_untracked_and_ignored_inputs(self) -> None:
        fixture = ReleaseFixture(self.root)
        candidate = fixture.candidate
        untracked = fixture.repo / "Assets/untracked.asset"
        untracked.write_text("untracked\n", encoding="utf-8")
        with self.assertRaisesRegex(ReleaseError, "untrackedInputs"):
            validate_exact_candidate(fixture.repo, candidate)
        untracked.unlink()
        ignored = fixture.repo / "Assets/ignored.asset"
        gitignore = fixture.repo / ".git/info/exclude"
        with gitignore.open("a", encoding="utf-8") as handle:
            handle.write("Assets/ignored.asset\n")
        ignored.write_text("ignored\n", encoding="utf-8")
        with self.assertRaisesRegex(ReleaseError, "ignoredInputs"):
            validate_exact_candidate(fixture.repo, candidate)
        second = fixture.repo / "ProjectSettings/ignored-two.asset"
        with gitignore.open("a", encoding="utf-8") as handle:
            handle.write("ProjectSettings/ignored-two.asset\n")
        second.write_text("ignored two\n", encoding="utf-8")
        record = clean_status_record(fixture.repo)
        self.assertEqual(
            record["ignoredInputs"],
            ["Assets/ignored.asset", "ProjectSettings/ignored-two.asset"],
        )

    def test_merge_rejects_reserved_symlink_and_casefold_paths(self) -> None:
        repo = self.root / "sources"
        repo.mkdir()
        release = repo / "release"
        web = repo / "web"
        release.mkdir()
        web.mkdir()
        (release / "release-build-provenance.json").write_text("{}\n", encoding="utf-8")
        (release / "A.txt").write_text("a\n", encoding="utf-8")
        (web / "a.txt").write_text("b\n", encoding="utf-8")
        with self.assertRaisesRegex(ReleaseError, "case-fold collision"):
            create_merged_stage(repo, release, web, repo / "stage-case", "a" * 40, "0" * 16, False)
        (web / "a.txt").unlink()
        (web / ".nojekyll").write_text("bad\n", encoding="utf-8")
        with self.assertRaisesRegex(ReleaseError, "reserved path"):
            create_merged_stage(repo, release, web, repo / "stage-reserved", "a" * 40, "0" * 16, False)
        (web / ".nojekyll").unlink()
        os.symlink("A.txt", release / "link.txt")
        with self.assertRaisesRegex(ReleaseError, "symlink"):
            create_merged_stage(repo, release, web, repo / "stage-link", "a" * 40, "0" * 16, False)

    def test_remote_git_and_http_bytes_match_complete_tree(self) -> None:
        fixture = ReleaseFixture(self.root)
        stage = fixture.repo / "stage-remote"
        manifest = fixture.evidence_dir / "deployment-payload-manifest.json"
        collisions = fixture.evidence_dir / "deployment-merge-collisions.json"
        seal_path = fixture.evidence_dir / "deployment-payload-seal.json"
        seal = seal_payload(
            fixture.repo,
            fixture.release,
            fixture.repo / "web",
            stage,
            manifest,
            collisions,
            seal_path,
        )
        commit = git(
            fixture.repo,
            "commit-tree",
            seal["expectedGitTreeId"],
            "-m",
            "sealed pages fixture",
        ).stdout.strip()
        handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=str(stage))
        server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            report_path = fixture.evidence_dir / "remote-served-file-hashes.json"
            report = verify_remote_payload(
                fixture.repo,
                fixture.release,
                manifest,
                seal_path,
                commit,
                f"http://127.0.0.1:{server.server_port}/",
                report_path,
            )
        finally:
            server.shutdown()
            thread.join(timeout=5)
            server.server_close()
        self.assertEqual(report["remoteGitTreeId"], seal["expectedGitTreeId"])
        self.assertEqual(report["result"], "PASS")
        manifest_payload = load_json(manifest)
        served_entries = [item for item in manifest_payload["entries"] if item["path"] != ".nojekyll"]
        self.assertEqual(len(report["httpEntries"]), len(served_entries) + 1)
        self.assertEqual(report["nojekyllGitProof"]["byteLength"], 0)

    def test_downstream_identity_is_rejected_from_upstream_evidence(self) -> None:
        fixture = ReleaseFixture(self.root)
        fixture.provenance_path.chmod(0o644)
        fixture.provenance_path.unlink()
        evidence = json.loads(fixture.evidence.read_text(encoding="utf-8"))
        evidence["expectedGitTreeId"] = "a" * 40
        fixture.evidence.write_bytes(canonical_json_bytes(evidence))
        with self.assertRaisesRegex(ReleaseError, "downstream identity field"):
            create_provenance(
                fixture.repo,
                fixture.metadata,
                fixture.development,
                fixture.release,
                fixture.provenance_path,
            )

    def test_provenance_rejects_failed_evidence_same_build_and_fake_upstream(self) -> None:
        fixture = ReleaseFixture(self.root)
        fixture.provenance_path.chmod(0o644)
        fixture.provenance_path.unlink()

        payload = json.loads(fixture.evidence.read_text(encoding="utf-8"))
        payload["result"] = "FAIL"
        fixture.evidence.write_bytes(canonical_json_bytes(payload))
        with self.assertRaisesRegex(ReleaseError, "did not PASS"):
            create_provenance(
                fixture.repo,
                fixture.metadata,
                fixture.development,
                fixture.release,
                fixture.provenance_path,
            )

        payload["result"] = "PASS"
        fixture.evidence.write_bytes(canonical_json_bytes(payload))
        with self.assertRaisesRegex(ReleaseError, "paths must be distinct"):
            create_provenance(
                fixture.repo,
                fixture.metadata,
                fixture.release,
                fixture.release,
                fixture.provenance_path,
            )

        metadata = json.loads(fixture.metadata.read_text(encoding="utf-8"))
        metadata["sourceUpstream"] = "HEAD"
        fixture.metadata.write_bytes(canonical_json_bytes(metadata))
        with self.assertRaisesRegex(ReleaseError, "origin/<branch>"):
            create_provenance(
                fixture.repo,
                fixture.metadata,
                fixture.development,
                fixture.release,
                fixture.provenance_path,
            )

    def test_cli_import_does_not_create_ignored_bytecode_inputs(self) -> None:
        fixture = ReleaseFixture(self.root)
        bytecode = fixture.repo / "tools/deploy/__pycache__"
        self.assertFalse(bytecode.exists())
        environment = os.environ.copy()
        environment.pop("PYTHONDONTWRITEBYTECODE", None)
        subprocess.run(
            [sys.executable, str(fixture.repo / "tools/deploy/release_provenance.py"), "--help"],
            cwd=fixture.repo,
            env=environment,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        self.assertFalse(bytecode.exists())


if __name__ == "__main__":
    unittest.main()
