# Release Notes

## GitHub Pages 배포 — 동료 명령 콘솔 + VFX 임팩트 패스 · 2026-08-04

### 변경 (소스 `7256cb5`, 교차 세션 레인)
- **동료 명령 콘솔** (던전, Enter): 한국어 우선 키워드 파서 → 닫힌 의도 집합
  (집중공격/방어·복귀/스킬 시전), 선택적 Gemini 자유문장 폴백 (키는 런타임
  전용, 빌드에 미포함). 입력 중 0.2x 슬로모. 분류 테스트 20종 포함.
- **AOE/스킬 VFX 임팩트 패스**: 노바 번 데칼 1.2 s, 펄스 필드 필 3 s,
  Aegis/Ward 시전 링 (전부 풀링, ClearTransient 커버).

### 게이트·배포
- EditMode **146/146 통과**
  (`_workspace/current/engineering/unity-logs/test-results-082947.xml`).
- Unity 6000.5.6f1 WebGL 빌드 성공
  (`_workspace/current/engineering/unity-logs/build-083019.log`), data
  26,558,801 B, wasm 9,140,333 B.
- gh-pages `6ddd724`, 캐시 버전 `18b0fc1a992f9312`. 라이브 index.html·4개
  리소스 모두 새 버전 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- 캠페인 1단계 강하 → Enter 콘솔 오픈 (명령 힌트·슬로모) → `nova` 제출 →
  **"잿불 노바 시전"** 피드백, 기름 100→55, 적 4→3, 점수 100, 노바 번 데칼
  렌더 (`_workspace/current/engineering/deployed-console-nova.png`).
- 로컬 빌드 사전 검증에서 동일 경로 + 콘솔 열림/닫힘/ESC 탈출 확인.
- 참고: headless CDP `keyboard.type()`은 한글 IME 조합이 없어 ASCII 별칭
  (`nova`)으로 실행 경로를 증명 — 한글 키워드는 파서 단위테스트 20종이 커버.

## GitHub Pages 배포 — WebGL 텍스처 상한 빌드 · 2026-08-04

### 배포
- gh-pages 커밋 `d4c7392` (`deploy: WebGL texture-cap verified build 2026-08-04`).
  data 26,549,778 bytes (이전 52,380,884 대비 −49.3 %), wasm 9,117,062 bytes,
  캐시 버전 `1bc1f4b712e762e5` → `61a0b09946ca5642`.
- 라이브 확인: `https://akillness.github.io/hongT/`가 새 캐시 버전 index.html을
  서빙하고 `Build/build-webgl.data.unityweb?v=61a0b09946ca5642`가 HTTP 200
  / content-length 26,549,778로 응답했다.

### 배포 후 스모크
- 데스크톱 1440×900: 로비 → 출정 → 전투 진입, WASD 이동·Space 타격 입력 후
  체력 86/웨이브 1/적 4·피격 비네트 확인, 런타임 오류·경고 배너 0
  (`_workspace/current/engineering/deployed-texcap-desktop-lobby.png`,
  `_workspace/current/engineering/deployed-texcap-desktop-combat.png`).
- 모바일 390×844 DPR 2: 로비 → 출정 → 전투 진입, 체력 100/웨이브 1/적 3,
  런타임 오류 0
  (`_workspace/current/engineering/deployed-texcap-mobile-lobby.png`,
  `_workspace/current/engineering/deployed-texcap-mobile-combat.png`).
- 아레나 `?mode=arena` 1440×900: 웨이브 전투 부팅, D 이동·Space 근접 교전
  (체력 44 정상 피격), Q/E 스킬바·적 체력바 렌더, 런타임 오류 0
  (`_workspace/current/engineering/deployed-texcap-arena-combat.png`).
- 캠페인 1단계 Cinder Span 1440×900: prologueDone 시드 후 강하 →
  "웨이브 1/5" 배너, 3타 콤보·대시·Q/E/R/F 스킬, Void Aegis 방패 40,
  기름 68 소모, 적 3→2, 분출구 텔레그래프, 런타임 오류 0
  (`_workspace/current/engineering/deployed-texcap-campaign-stage1*.png`).
  localStorage v2 스키마 시드가 로비 카드 게이팅(프롤로그 재훈련·1단계
  해금)에 정상 반영 — 영속 경로 확인.
- 종합: `_workspace/current/qa/deployed-release-verification.md`.

## 로컬 검증 — WebGL 텍스처 상한 보정 · 2026-08-04

### 변경
- 임포터 파일 43개의 Default 또는 WebGL 항목 65개를 1024로 상한 조정했고, 아이콘
  Default 항목 20개는 256을 유지했다.

### 회귀 게이트
- 집중 텍스처-상한 테스트와 최종 111/111 테스트가 통과했다
  (`_workspace/current/engineering/unity-logs/test-results-071245.xml`). Unity
  6000.5.6f1 로컬 WebGL 빌드는
  `_workspace/current/engineering/unity-logs/build-071018.log`에서 성공했고,
  54,819,218 bytes, `errors=0`, `warnings=2`를 기록했다. 로컬 데스크톱·모바일에서
  로비에서 전투로 전환하는 스모크를 수행했고,
  `_workspace/current/engineering/post-cap-desktop-combat.png` 및
  `_workspace/current/engineering/post-cap-mobile-combat.png`에 화면을 보존했다.
  GitHub Pages는 배포하지 않았다.

## v0.2.0 — 심연 강하 (Hack & Slash Overhaul) · 2026-08-04

`index.html?mode=campaign&stage=cinder-span` 단일 페이지 흐름을 **로비 중심
단일 씬 상태머신**으로 전면 개편. 원작(Abyssal-Lantern)의 3D 인게임·로비
구성 리서치를 근거로 던전 전투를 핵앤슬래시로 재설계했다.

### 신규
- **로비 (단일 씬)** — 라이브 3D 디오라마 배경(워든/동료/보스 대치, 스테이지
  액센트 라이트, 슬로우 오빗), 성장/장비/군단 탭, 출정 카드.
- **프롤로그 "점화 훈련"** — 탑다운 오소그래픽 2D 디펜스 3웨이브로 장르 학습
  → 클리어 시 90°→55° 카메라 스윕(2.5D 전환 연출) → 캠페인 해금.
- **핵앤슬래시 전투 킷 (던전)** — 3타 콤보(87 마무리+넉백), 대시(무적
  0.22 s), 스킬 4종(균열 화살/묘지 파동/잿불 노바/공허 방패), 원소 상성
  사이클(ember>frost>veil>void, +20 %/−15 %).
- **인런 성장** — XP 곡선(원작 [30..310]+60), 레벨 캡 12, 레벨업 시
  피해 +4 %/HP +6/재생 +0.3.
- **정예 & 추출** — 7번째 스폰 정예(HP×3, 금색), 시체 채널 2 s로 동료화
  (`<visual>-echo`), 중복 시 유물 +30.
- **동료 동행** — 보스 첫 처치 보상 + 추출 로스터에서 1체 선택, 80 px 추종,
  플레이어 피해 60 % 자동 공격. 메시 재사용 + 틴트 변형(페이로드 0 증가).
- **보스전 개편** — 상단 보스 바, 2페이즈(50 %: 이속 +25 %, 접촉 ×1.25,
  도발 말풍선), Monarch 호위 3기 소환.
- **스토리 말풍선** — 원작 stage-story-catalog 대사 이식(스테이지 시작/보스
  등장/페이즈2/클리어), 월드공간 빌보드, 우선순위 큐.
- **메타 성장** — 스탯 포인트(클리어 +2, 첫 보스 +1), 장비 T0–T5 유물 구매
  ([2,4,7,11,16]), localStorage v2 (하위호환).
- **6단계 캠페인** — Cinder Span → Ember Gallery → Abyss Chancel → Witness Well →
  Echo Throne → Ash Verdict를 순서대로 해금. 마지막 Ash Verdict를 정화하면
  Ember Rest 없이 최종 결과 오버레이를 표시한다. 플레이어는 이 패널에서
  재도전하거나 명시적으로 로비 복귀를 선택한다.
- **Ember Rest** — 비최종 스테이지를 마친 뒤 결과 패널 없이 즉시 열리는
  결정론적 준비 제안 3개 중 하나를 선택하거나 건너뛴다. 선택은 다음 던전
  1회에만 적용되며 저장·재시도·이후 스테이지로 이월되지 않는다.
- **휴머노이드 런타임 애니메이션 게이트** — 재스키닝한 전 캐릭터 프리팹에
  유효한 Humanoid Avatar·공유 액션 컨트롤러·활성 Animator·SkinnedMeshRenderer와
  공격 시 오른손 모션을 요구한다.

### 변경
- `campaign.html` → `index.html` 즉시 리다이렉트 (로비가 게임 안으로 통합).
- 던전 키맵: Q/E/R/F = 스킬 4종, 재시작은 패널 버튼 전용 (아레나는 기존
  Q/E/R 유지).
- 던전 적 HP `86 + min(140, (wave−1)×11)` (콤보 DPS 보정).
- 노멀맵 활성화 (`_BumpMap` + NormalMap 임포터 타입).
- **잿불 분출구** — 활성 Lantern Ward는 분출구 펄스 피해를 무효화하며, 기존 피해 유예는 보존.
- **세로 WebGL 기동 안정화** — Unity 6의 자동 캔버스-백킹스토어 동기화가
  390×844 전체화면에서 WASM 호출 스택을 재귀적으로 소진하던 경로를 끄고,
  CSS로 렌더된 캔버스 사각형과 DPR 상한 2를 사용해 로더 전과
  resize·orientation·visualViewport 변경 뒤 백킹스토어를 명시적으로 동기화한다.
  기동 실패는 브라우저 `alert` 대신 게임 경고 배너에 표시해 로딩 대기와 실제
  오류를 구분한다.
- **WebGL 한글 로비 글리프 게이트** — 모든 View 문자열로 `HudKorean` 서브셋을 재생성하고, 라이브 모션 버튼이 같은 리소스를 사용한 채 `모션: 보통`과 `모션: 약함` 두 상태의 모든 글리프를 보유하는지 EditMode에서 검증한다.
- **동료 명령 캐치업 안전성** — 고정 스텝 캐치업 배치에서 동료 대기/회수
  명령도 첫 틱에서만 소비해 반복 재적용되지 않는다.

### 회귀 게이트
- 최종 EditMode 전체 회귀 110/110 통과, 실패 0
  (`_workspace/current/engineering/unity-logs/test-results-065139.xml`). WebGL 셸
  회귀는 자동 동기화 해제, DPR≤2, 초기/이후 viewport 동기화, 세로·데스크톱 CSS
  계약, 기동 오류 배너, 멱등 postprocess를 검증한다.
- 최종 Unity 6000.5.6f1 캐시-버스트 WebGL 빌드 통과
  (`_workspace/current/engineering/unity-logs/build-055336.log`,
  `Build Finished, Result: Success`, `errors=0`, `warnings=2`, 80,731,744 bytes).
- GitHub Pages(<https://akillness.github.io/hongT/>)의 iPhone UA/DPR 3 에뮬레이션
  390×844→844×390 회전에서 100 % 로드, `unity-mobile` 분기, 2× DPR 상한
  백킹스토어(780×1688→1688×780), 로딩 바 숨김·경고/런타임 오류 0을 확인했다.
  데스크톱 1280×720→1440×900 확대에서도 CSS/백킹스토어가
  1080×720→1280×853으로 함께 갱신되고 오류가 없었다.
- 최종 Pages iPhone UA/DPR 3 수동 스모크에서 로비 → 프롤로그 진입 → 전투 HUD →
  패배 패널 → `다시 도전` 후 HUD 재초기화까지 동작했고, 전투 HUD의 드래그
  조이스틱과 `타격` 터치 컨트롤이 표시됐다. 이 스모크는 전체 웨이브 클리어
  검증을 대신하지 않는다.
- 동료 대기·회수 원샷 명령의 캐치업 회귀 2/2 통과, 실패 0
  (`_workspace/current/engineering/unity-logs/test-results-companion-one-shot.xml`).
- 한글 로비 글리프 회귀 1/1 통과, 실패 0
  (`_workspace/current/engineering/unity-logs/test-results-lobby-motion-label-font.xml`).
  Unity 6000.5.6f1 WebGL 보정 빌드는 `build-063448.log`에서 성공했다.
  GitHub Pages 모바일 스모크의 실제 토글 전·후 상태는
  `_workspace/current/engineering/deployed-mobile-font-normal.png`와
  `_workspace/current/engineering/deployed-mobile-font-weak.png`에 각각 보존했다.

---

## v0.1.0 — Unity 재구현 초판 · 2026-08-04

- 원작 Cinder Court(Canvas 2.5D)의 수치 계약을 보존한 Unity 6/URP/WebGL 재구현.
- 결정론 60 Hz 순수 C# 심과 Unity 6/URP/WebGL 재구현의 기반을 도입했다.
- 3D 캐릭터 8종에 Blender 본히트 재스키닝과 Unity Humanoid 리타겟 경로를
  적용했다.
- 캠페인 초기 기반: 보스 웨이브, 장비 파편, 던전 기믹.
- ElevenLabs SFX 8종 + 로어 앰비언트 + BGM 루프.
- GitHub Pages 배포: <https://akillness.github.io/hongT/>
