# 릴리스 배포 절차 — 동결 프로비넌스 게이트

작성 2026-08-12. 근거는 전부 코드 실측이며 각 항목에 파일:행을 붙인다.

이 문서가 존재하는 이유: `_workspace/current/ops/toon-deploy-handoff.md:22-23`이
"이 ID 집합과 probe 해시의 규격은 저장소·`docs/`·`specs/` 어디에도 절차 문서가
없습니다"라고 적었다. 절차는 실재했고 도구 안에 있었다 —
`tools/deploy/run_release_gate.sh`와 `tools/deploy/make_release_evidence.py`.
문서가 없어서 실행하지 못한 것이므로, 여기에 옮긴다.

## 0. 절대 하지 말 것

`build-webgl/release-build-provenance.json`을 손으로 쓰지 마라.
`release_provenance.py`가 파일마다 `evidenceId`·`candidateSourceSha`·
`buildMode`·`contentMarker`를 재검증하고(`:389-411`), `result != "PASS"`면
거부한다(`:170-171`, `:400-401`). 손으로 채우는 것은 동결 증거 없이
릴리스하지 못하게 막으려던 게이트를 무력화하는 것과 같다.

## 1. 요구되는 동결 증거 6종

`release_provenance.py:47-54` `REQUIRED_EVIDENCE_MODES`가 정확히 이 6개를
요구하고, 집합이 다르면 `metadata.evidence IDs must exactly match the
stage-shadow policy`로 거부한다(`:182-187`).

| evidenceId | buildMode | contentMarker | 생산자 |
|---|---|---|---|
| `shadow-focused-editmode` | Source | **null** | `make_release_evidence.py editmode` |
| `full-editmode` | Source | **null** | 같음 (한 번의 실행에서 둘 다 나온다) |
| `release-build` | Source | **null** | `make_release_evidence.py build --kind release` |
| `development-build` | Development | dev 빌드 마커 | `make_release_evidence.py build --kind development` |
| `browser-shadow-desktop` | Development | dev 빌드 마커 | `tools/qa/run_shadow_browser_evidence.mjs` |
| `browser-shadow-mobile` | Development | dev 빌드 마커 | 같음 (viewport만 다름) |

Source 레코드는 마커가 **null이어야 한다** — 소스를 서술할 뿐 페이로드를
보지 않았고, 마커를 주면 보지 않은 페이로드를 주장하게 된다
(`make_release_evidence.py:14-17`).

`shadow-focused-editmode`는 별도 실행이 아니다. 이 프로젝트에서 Unity
`-testFilter`는 쓸 수 없다 — 러너가 그것을 groupNames로 매핑하고
`FullNameFilter.Match`가 트리를 걷다 NRE를 던져 RunError(3)로 죽으며 결과
파일을 아예 쓰지 않는다. 전체 실행의 NUnit XML에서 full name으로 부분집합을
추출한다(`make_release_evidence.py:19-25`, `run_release_gate.sh:37-41`).

## 2. 실행 순서

Unity는 프로젝트 전역 락을 잡는다 — **어떤 단계도 겹쳐 실행하면 안 되고,
다른 세션이 Unity 배치를 돌리는 중이면 둘 다 죽는다**(`run_release_gate.sh:5-7`).

```bash
# 후보는 현재 HEAD여야 하고, 이미 푸시돼 있어야 한다.
#   release_common.py:302-310  HEAD != candidate면 거부
#   deploy_pages.sh:90-100     원격 소스 브랜치 tip == candidate 여야 함
git rev-parse HEAD

# pre 스냅샷 (깨끗한 상태여야 한다)
python3 tools/deploy/release_provenance.py snapshot-clean \
  --output _workspace/current/qa/stage-character-shadows/candidate-clean-pre.json

# 1단계 — import 게이트 + 전체 EditMode + 페이로드 유닛테스트 + Development 빌드
bash tools/deploy/run_release_gate.sh 1

# EditMode 증거 2종 (한 XML에서 둘 다 나온다)
python3 tools/deploy/make_release_evidence.py editmode \
  --results  <_workspace/current/engineering/unity-logs/test-results-*.xml> \
  --candidate <sha> --out _workspace/current/qa/stage-character-shadows

# Development 빌드 증거 + 마커
python3 tools/deploy/make_release_evidence.py build --kind development \
  --build-dir build-development --log <build-development-*.log> \
  --candidate <sha> --out _workspace/current/qa/stage-character-shadows
DEV_MARKER=$(python3 tools/deploy/make_release_evidence.py marker \
  --build-dir build-development)

# 브라우저 증거 2종 — Development 빌드를 서빙한 뒤 각각 실행
#   토글 API는 `#if DEVELOPMENT_BUILD || UNITY_EDITOR` 뒤에 있어 Release에는 없다
node tools/qa/run_shadow_browser_evidence.mjs \
  --url http://127.0.0.1:8783/ --viewport 1440x900 \
  --evidence-id browser-shadow-desktop \
  --candidate <sha> --content-marker "$DEV_MARKER" \
  --out _workspace/current/qa/stage-character-shadows
#   mobile은 --viewport 375x667 --evidence-id browser-shadow-mobile

# 2단계 — Release 빌드 (build-webgl을 지우고 다시 만든다)
bash tools/deploy/run_release_gate.sh 2

python3 tools/deploy/make_release_evidence.py build --kind release \
  --build-dir build-webgl --log <build-*.log> \
  --candidate <sha> --out _workspace/current/qa/stage-character-shadows

# post 스냅샷 — 빌드 전후 둘 다 깨끗해야 한다 (release_provenance.py:248-251)
python3 tools/deploy/release_provenance.py snapshot-clean \
  --output _workspace/current/qa/stage-character-shadows/candidate-clean-post.json

# 프로비넌스 동결 (한 번만 가능 — 이미 있으면 거부, :269-270)
python3 tools/deploy/release_provenance.py create \
  --metadata <metadata.json> --development-build build-development \
  --release-build build-webgl

# 배포
bash tools/deploy/deploy_pages.sh "deploy: <메시지>"
```

## 3. `--metadata` 필수 필드

`create_provenance`(`release_provenance.py:233-252`, `:283-297`)가 요구한다.
**세 sha가 빠지면 여기서 하드 실패한다** — 이 표만 보고 `metadata.json`을
만들다 막히는 것이, 이 문서가 끝내려는 바로 그 상태다.

| 필드 | 규격 |
|---|---|
| `releaseBaseSha` | 소문자 40-hex Git SHA (`:233`, `release_common.py:143-146`) |
| `baselineProbeSha` | 같음 (`:234`) |
| `candidateSourceSha` | 같음. 현재 HEAD여야 한다 (`:235-236`) |
| `generatedAt` | ISO-8601, **타임존 필수** (`:205-213`) |
| `sourceUpstream` | `origin/<branch>` 형태의 원격 추적 ref (`:216-220`) |
| `unityVersion` | 비어 있지 않은 문자열 |
| `commands` | 비어 있지 않은 문자열 리스트 (실행한 명령들) |
| `probeHashes.baseline` / `.candidate` | 각각 sha256 (`:197-202`). **§5 참조 — 유래 미문서화** |
| `cleanStatus.pre` / `.post` | §2의 두 스냅샷 경로 |
| `outsideInputAllowList` | 리스트 (빈 리스트 허용) |
| `evidence` | §1의 6개 레코드 (경로 + evidenceId + buildMode) |

### 계보 제약 (순서가 강제된다)

`releaseBaseSha` → `baselineProbeSha` → `candidateSourceSha`가 조상 관계여야
하고, 아니면 `required ancestry does not hold`로 거부된다(`:237-238`).
검사는 `git merge-base --is-ancestor`이므로(`release_common.py:314-324`)
**동일 sha도 통과한다** — 실제로 `tools/tests/test_release_payload.py:166-168`이
세 값을 같은 sha로 넣는다. 단일 커밋을 배포할 때는 세 값 모두 HEAD로 두면 된다.

## 4. 게이트가 잡는 것들 (실측)

- `INPUT_ROOTS = ("Assets","Packages","ProjectSettings","web","tools/deploy")`
  (`release_common.py:19`). `_workspace/`는 **밖에 있다** — 워크스페이스 문서를
  고쳐도 게이트에 걸리지 않는다. 반대로 이 5개 루트는 전 과정 동안 건드리면
  안 된다(pre/post 둘 다 깨끗해야 하므로).
- Development와 Release의 `contentMarker`가 같으면 거부(`:277-278`).
- Release 빌드 경로와 Development 경로가 같으면 거부(`:271-272`).
- 프로비넌스 출력 경로는 반드시 `<release-build>/release-build-provenance.json`
  (`:266-268`), 파일 모드 0444로 동결된다(`:300`).
- `deploy_pages.sh`는 `seal_pages_payload.py`가 만든 매니페스트·충돌로그·
  분리 씰도 함께 요구한다(`:69-71`).

## 5. 닫힌 공백

### probeHashes — 규격 확정 (2026-08-12)

인계 문서의 불만 두 개 중 ID 집합은 §1이 닫았고, **probe 해시는 이 절이 닫는다.**

관측은 그대로다: 저장소에 `probeHashes`를 **생산하는 도구가 없고**,
`_validate_probe_hashes`(`release_provenance.py:197-202`)는 두 값이 소문자
64-hex sha256인지만 본다. 코드는 "무엇의 해시인가"에 답하지 않는다. 그래서
그 질문에 답하는 것은 도구가 아니라 이 계약이어야 한다.

**규격**: `Assets/Tests/EditMode/DungeonGoldenDigestTests.cs`의 **커밋된 블롭
sha256**. baseline은 `baselineProbeSha` 시점, candidate는 `candidateSourceSha`
시점에서 읽는다.

```bash
P=Assets/Tests/EditMode/DungeonGoldenDigestTests.cs
git show "$BASELINE_PROBE_SHA:$P" | shasum -a 256 | cut -d' ' -f1   # .baseline
git show "$CANDIDATE_SOURCE_SHA:$P" | shasum -a 256 | cut -d' ' -f1 # .candidate
```

왜 하필 이 파일인가. 스키마는 이 두 값을 `releaseBaseSha` → `baselineProbeSha`
→ `candidateSourceSha` 계보 **바로 옆에** 놓는다(`:284-288`). 계보는 "어떤
소스에서 왔는가"를 말하고, probe는 그 옆에서 **"그 사이에 판정 기준이
움직였는가"**를 말해야 자리값이 산다. 이 저장소에서 판정 기준은 골든 다이제스트
녹화본 하나다 — 시뮬레이션 산술이 바뀌면 반드시 이 파일이 바뀌고, 바뀌지
않았다면 심 거동은 바뀌지 않았다(CLAUDE.md §4: 배포 진실은 Unity 골든).

따라서 **두 값이 같다 = 이 릴리스는 심 거동을 바꾸지 않았다**는 서명이고,
다르면 골든이 의도적으로 이동했다는 서명이다. 어느 쪽이든 사람이 읽고 판정할
수 있는 명제다. 빌드 산출물 해시를 넣었다면 이 값은 `builds`가 이미 말하는
것을 되풀이했을 뿐이고(§0이 금지하는 하위 정체성 중복에 가깝다), 소스 트리
전체 해시를 넣었다면 주석 한 줄에도 움직여 아무 명제도 만들지 못했을 것이다.

[OBSERVED] 2026-08-12 릴리스: baseline(94195cf)·candidate 모두
`9c6f5d705f5710552f329a21981135d78504d42baffb61bf1555cc00e4245322` — 동일.
이번 배포는 툰 셰이딩·텍스처·그림자 뷰 작업이며 심 산술을 건드리지 않았다.

이 규격은 추측이 아니라 **선언**이다. 다음 레인은 이 정의를 따르거나, 바꾸려면
여기를 고치고 이유를 남긴다. 검증기가 형식만 보는 필드는 그 의미를 계약이
들고 있어야 한다 — 아무도 들지 않으면 필드는 형식만 맞는 난수가 된다.

### 하네스 핀

`seal_pages_payload.py:55-60`이 핀하는 도구는 `tools/deploy/` 4개뿐이라
`tools/qa/run_shadow_browser_evidence.mjs`는 게이트 밖에 있다 — **커밋되지 않은
하네스가 게이트를 통과하는 증거를 만들 수 있다.**

닫는 방법은 절차다: 브라우저 증거를 생산하기 전에 하네스를 커밋하고 그 sha를
`metadata.commands`에 남긴다. 2026-08-12 릴리스의 하네스 sha는 `e2a44f6`이며,
그 커밋이 뷰포트 의존 임계값 3개를 제거해 모바일 증거의 위양성 FAIL을 없앴다.

### 전파 지연을 배포 실패로 오독하지 마라 (2026-08-13)

`deploy_pages.sh`는 **푸시한 뒤에** 서빙 바이트를 검증한다. Pages 재빌드가
재시도 창을 넘기면 배포가 성공했는데 FATAL이 찍힌다. 실제로 그랬다 —
gh-pages는 `b7961e65`로 이동을 마쳤고 오류만 남았다.

오류 문자열이 판정에 필요한 전부다 (`seal_pages_payload.py:548-551`):

```
remote byte mismatch ... sha={actual}/{expected}
```

**actual이 서빙된 바이트, expected가 씰이다.** 세 값을 비교하면 원인이 갈린다:

```bash
git show origin/gh-pages:<file> | shasum -a 256   # 커밋된 것
shasum -a 256 <sealed copy>                        # 씰이 기대하는 것
curl -s "<url>?x=$RANDOM" | shasum -a 256          # 지금 서빙되는 것
```

- 커밋 == 씰 != 서빙 -> **전파 지연**. 기다렸다가 `verify-remote`만 다시 돌린다.
- 커밋 != 씰 -> 페이로드 불일치. 이건 진짜 결함이다.

2026-08-13은 전자였고 (셋 다 `635d4a95`로 수렴, actual만 직전 배포본),
45초 뒤 standalone `verify-remote`가 그대로 통과했다.

**`deploy_pages.sh`를 다시 돌리지 마라.** 이미 푸시했으므로 재실행은 스테이징
경로 충돌로 죽고, 성공한 배포 위에 실패를 하나 더 얹을 뿐이다. 복구는:

```bash
python3 tools/deploy/seal_pages_payload.py verify-remote \
  --repo-root . --release-build build-webgl \
  --manifest <manifest> --seal <seal> \
  --remote-commit $(git rev-parse origin/gh-pages) \
  --base-url https://akillness.github.io/hongT/ \
  --report <OUT>/remote-served-file-hashes.json
```

재시도 기본값은 이 사이클에서 12->36회(3분)로 올렸다. 60초는 관측된 재빌드
시간보다 짧았다.

### snapshot-clean 전에 `tools/deploy/__pycache__`를 쓸어라 (2026-08-13)

`tools/deploy`는 INPUT_ROOT이고 `release_common.py:199`는 **gitignore된 경로까지
포함해** 검사한다. `.gitignore:118`에 `tools/**/__pycache__/`가 있어도 소용없다:

```
FATAL: cannot freeze dirty source snapshot (ignoredInputs):
  ['tools/deploy/__pycache__/release_common.cpython-310.pyc', ...]
```

네 진입 스크립트는 전부 `release_common` import 전에
`sys.dont_write_bytecode = True`를 갖고 있다. **그런데도 막혔다.** .pyc가 두
인터프리터(310/314)로 찍힌 게 단서다 - 이 저장소 도구는 한 파이썬으로 도니까,
`release_common`을 **직접 import한 외부 프로세스**가 만든 것이다. 진입점 가드는
자기 프로세스만 지킬 수 있고, 외부 import는 그 밖에 있다.

그러므로 스크립트로는 닫히지 않는다. 절차로 닫는다:

```bash
rm -rf tools/deploy/__pycache__
python3 tools/deploy/release_provenance.py snapshot-clean --output ...
```

pre·post 두 스냅샷 모두 앞에 둔다. 두 인터프리터 태그가 보이면 원인은 이
저장소 도구가 아니라 외부에서 온 import다.
