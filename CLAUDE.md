# HongT — Cinder Court (Unity) repository operating rules

Unity 재구현 프로젝트의 저장소 계약. `AGENTS.md`는 이 파일을 가리킨다. 계약은
하나다. 소스 프로젝트(`~/orca/Abyssal-Surge`)의 규칙을 이식하되, 엔진 관점은
이 저장소에 맞게 **Unity + WebGL**로 대체한다.

---

## 0. Lineage

- 원작: Abyssal Lantern — Hold the Cinder Court (Three.js/Canvas 2.5D,
  NAN 2026 제출본). 소스 저장소는 **읽기 전용 자산·규칙 소스**다.
- 이 저장소: 동일 게임의 Unity 6000.5.6f1 / URP / WebGL 재구현.
- 배포 계약: <https://akillness.github.io/hongT> (GitHub Pages, gh-pages 브랜치).
  모든 런타임 URL은 상대 경로만 사용한다. 루트 절대 경로(`/Build`, `/assets`)
  금지.

## 1. Engine perspective: Unity + WebGL only

- Unity 6000.5.6f1, URP 17.5, WebGL 타깃. 에디터 자동화는 배치모드
  (`Unity -batchmode -executeMethod ...`)로만 수행하고 명령을 기록한다.
- **Three.js/DOM 가이드는 여기 적용하지 않는다.** 원작 코드는 시맨틱(수치,
  규칙) 추출용 참조다.
- 시뮬레이션은 결정론적 고정스텝 60 Hz 순수 C# (`CinderCourt.Sim`,
  UnityEngine 참조 금지). 프레젠테이션(`CinderCourt.View`)은 심 상태를 읽기만
  하고 되쓰지 않는다. 이 경계는 원작의 하드 인바리언트를 계승한다.
- WebGL 제약: compute/threads 금지, 텍스처 ≤1024, 캐릭터 ≤25k tri,
  총 빌드 ≤120 MB. gzip + decompression fallback.

## 2. Numeric contract (제출문서 §2.3 이식)

고정스텝 `1/60`. 아레나 1536×1024, 중심 (768,604), 반경 520×270.
워든 HP 100 / 이동 218 u·s⁻¹ / 공격 58 / 사거리 160 / 쿨 0.48 s.
적 HP 58 / 사거리 76 / 쿨 1.22 s. 동시 상한 20.
기름 최대 100, +7/s, +6/처치. Nova 45 기름·6.5 s·반경 250·피해 96.
Ward 30 기름·9 s·지속 3 s. 드롭 shard +18 HP / flask +35 기름 /
relic +250 점수, 수명 12 s, 자력 78. 아이소 거리 `hypot(dx, dy*1.42)`,
전방 판정 `dx*facing ≥ -18`. **숫자는 게이트다. 형용사는 게이트를 못 넘는다.**

## 3. Asset generation: fixed tool per asset class

| Asset class | Tool | Invocation |
|---|---|---|
| 컨셉/텍스처/아틀라스 | god-tibo-imagen | `gti --prompt "..." --input <ref> --output <path>` (`--dry-run` 먼저) |
| 2D 스프라이트/시트 | perfectpixel | `ppgen -provider god-tibo-imagen -desc "..." -json` |
| 3D 리스킨/애니메이션 | Blender 5.x headless | `blender -b -P tools/blender/<script>.py -- ...` |
| SFX | ElevenLabs sound-generation API | `python3 tools/audio/gen_sfx.py` (키: env `ELEVENLABS_API_KEY`, 커밋 금지) |

- 캐릭터 스키닝 계약: 소스 메시(Abyssal-Surge 모션 라이브러리)를
  **mixamo 표준 휴머노이드 스켈레톤에 자동 웨이트로 재바인딩**하고 FBX로
  내보낸 뒤 Unity Humanoid 아바타로 리타겟한다. 원작의 절차적 영역분할
  스키닝(메시 파손 원인)은 재사용 금지.
- 생성 산출물은 `docs/provenance/`에 프롬프트·소스·도구를 기록한다.
- 액션 라이브러리(11종): `idle move run hit bighit attack critical avoid
  defence die show`. idle/move/run 루프, 나머지 원샷.

## 4. Workspace and evidence

- Unity 소스(`Assets/`, `Packages/`, `ProjectSettings/`, `tools/`, `docs/`)는
  일반 소스 트리로 편집한다. `_workspace/current/`는 **증거·레인 아티팩트
  전용**(intake/design/engineering/qa/ops — 리포트, 측정, 생성 계획)이며,
  이전 사이클은 `_workspace/archive/<run-id>/`로 `git mv` 후 읽기 전용.
- `[OBSERVED]` / `[INFERENCE]` / `[TARGET]` 표기를 유지한다. 측정 없는 주장
  금지, 정확한 저장소 상대 경로 인용.
- 검증 명령: EditMode 테스트 `Unity -batchmode -runTests -testPlatform
  EditMode`, WebGL 빌드 `-executeMethod CinderCourt.EditorTools.BuildScript.
  BuildWebGL`, 로컬 서빙 `python3 -m http.server`.

## 5. Concurrent-session Git safety (원작 이식, 무수정)

- 다른 세션의 동시 편집을 가정한다. 편집 전과 커밋 직전 `git status --short`.
- 명시적 pathspec으로만 스테이징. `git add -A`/`git add .` 금지.
- 다른 세션의 변경 복원·폐기·강제 덮어쓰기 금지. 충돌 시 중단·기록·명시 해결.
- push 전 upstream fetch 후 `@{upstream}..HEAD` 전체 검사. force-push 금지.
- 파괴적 자산 작업 전 `git tag -f pre-<op>-<date>`.

## 6. Reporting

실제 확인한 것만 보고한다: 정확한 명령/아티팩트 경로와 관측 결과.
이월 증거·신규 증거·미해결 블로커·사람 판단 항목을 구분한다.
