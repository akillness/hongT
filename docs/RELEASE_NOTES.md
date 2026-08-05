# Release Notes

## GitHub Pages 배포 — §W 웨이브 등장 알림 · 2026-08-05

### 변경 (spec `combat-feel-boss-phase-spec` §W, 신규 스펙 첫 레인)
- **웨이브 도착 텔레그래프**: `WaveStarted` 시 다음 웨이브가 사용할 스폰
  지점에 0.6s 경고 링 4개. 심의 **public 결정론 함수**
  `CinderSim.SpawnPointIndexFor`를 그대로 읽어 배치하므로 View가 스폰 규칙을
  복제하지 않는다. 보스 웨이브는 적색·대형 링.
- **전용 풀 분리**: 기존 스코치 풀은 정확히 4슬롯(노바+펄스 기준)이라 웨이브
  링 4개를 같은 풀에 넣으면 **살아있는 스킬 데칼을 전부 축출**한다. 히트
  스파크를 버스트에서 분리한 선례대로 `_waveWarnings` 전용 풀 신설 +
  `SpawnScorchIn(pool, …)` 파라미터화.
- **셰이크 티어**: `WaveStarted` 0.05/0.15를 우선순위 **최하위**에 배치 —
  보스 웨이브는 `BossSpawned`와 `WaveStarted`를 동시에 올리므로, 순서를
  뒤집으면 0.35 보스 펀치가 약해진다.

### 게이트·배포
- EditMode **170/170 통과** — 신규 `WaveTelegraphTests` 3종(매핑 순수성·범위,
  웨이브 내 4지점 비충돌, 전 스폰 지점 아레나 내부)
  (`unity-logs/test-results-143001.xml`).
- gh-pages `906f25d`, 캐시 버전 `108d8370f2a300e3` 라이브 확인.

### 검증 상태 (정직 표기)
- **확인됨**: 컴파일·게이트 통과, 배포 및 라이브 캐시 갱신, 라이브 빌드가
  프롤로그·던전 양쪽에서 런타임 오류 0으로 구동.
- **미확인**: 경고 링의 **화면 렌더는 아직 라이브 캡처로 확인하지 못했다.**
  `WaveStarted`는 웨이브를 비워야 발생하는데, 헤드리스 무회피 드라이버가
  프롤로그 웨이브 1(처치 3/4, HP 9에서 사망)과 던전 웨이브 1을 끝내 클리어
  하지 못했다. 코드 경로와 스폰 매핑은 EditMode로 고정했으나, **시각 증거는
  다음 사이클의 미결 항목으로 남긴다.**

## GitHub Pages 배포 — 프롤로그 26° 측면 카메라 · 2026-08-05

### 변경 (교차 세션 레인 `0674535`)
- 훈련 화면이 90° 탑다운에서 **26° 측면 오쏘 뷰**로 전환 — 사용자 보고
  "평평해서 불편함" 해소. PrologueReveal 스윕을 26→55°로 재앵커하고 시작
  거리를 재계산(3.6 / tan21° = 9.4u)해 오쏘→퍼스펙티브 핸드오프에서 팝이
  생기지 않게 했다.
- 소스만 있고 배포되지 않은 상태였으므로 **소스/라이브 드리프트를 닫는**
  배포 사이클로 처리했다.

### 게이트·배포
- EditMode **167/167 통과** (`unity-logs/test-results-141542.xml`).
- gh-pages `043072c`, 캐시 버전 `2d44354d10a7fa7c` 라이브 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- 신규 프로필 → 점화 훈련 진입: 캐릭터가 탑다운 실루엣이 아니라 **전신
  측면 실루엣**으로 읽히는 레터박스 프레임 확인
  (`_workspace/current/engineering/deployed-prologue-side-camera.png`).
- 이동·타격 8사이클 동안 카메라 팝 없음, 함락 패널까지 프레임 일관.

## GitHub Pages 배포 — 세로 모드 로어 겹침 수정 + 폰트 커버리지 게이트 · 2026-08-05

### 변경 (회고 디자인 후보 소진)
- **세로 모드 로어 겹침 해소**: 폰 티어 던전에서 로어 라인(y=118)이 4카드
  스킬 행(y 54–146) 위에 그대로 얹혀 있었다 (모바일 QA 실측 발견). 던전
  활성 + 폰 티어일 때 로어를 컨트롤 스택 위(y=262+lift)로 이동 — 스피커
  라인(232, 스팬 ≈219–245)과도 비충돌. `SetCampaignSurfacesVisible`이
  `ApplyLayoutTier`를 재호출하도록 배선해 던전↔아레나 전환 시 앵커가
  낡지 않게 했다.
- **폰트 커버리지 게이트 신설** (`FontCoverageTests`): `HudKorean.otf`는
  생성된 **서브셋**이라 새 한글 문자열이 글리프 없이 배포되면 WebGL에서
  OS 폴백 없이 글자가 사라진다 (Lane K "난독화" 토스트가 라이브에서 실제로
  당함). View 소스에서 한글을 재수확해 폰트가 전부 커버하는지 검사하고,
  실패 시 `bash tools/gen_hud_font.sh` 안내를 낸다 — 함정을 게이트로 고정.

### 게이트·배포
- EditMode **167/167 통과** (`unity-logs/test-results-113158.xml`).
- gh-pages `0d3fba5`, 캐시 버전 `efb632aac6ccf3e5` 라이브 확인.

### 배포 후 스모크 (라이브, 390×844 DPR 2, 오류 0)
- Ember Gallery 세로 강하: 위→아래 **로어 / 콤보 핍 / Q·E·R·F 행 / SHIFT**
  순으로 정렬, 겹침 없음 (`_workspace/current/engineering/portrait-lore-fixed.png`).

## GitHub Pages 배포 — Lane V1 시전 동기화 + Lane V4 URP 포스트 (스펙 전 레인 완료) · 2026-08-05

### V1 — 시전 동기화 손 글로우 (`a9bd7ff`, 캐시 `30f826ca74f49b95`)
- `ActorView.FlashCastGlow`: RightHand 본에 원소색 수렴 글로우 0.12s
  (0.16→0.055wu 수축 + 안쪽으로 증휘) — 볼트 보라 / 파동 녹색 / 노바 엠버 /
  에이기스·워드 시안. 심은 즉발 시전이라 글로우는 시전 이벤트에서 시작해
  "방출 직후 잔광"으로 읽힘. 판정 불변(SimEvents 소비 전용), 풀 리셋 정리,
  비휴머노이드 리그 무표시.
- 라이브 스모크: Q 볼트 보라 글로우+스트릭, F 에이기스 시안 글로우+링
  (0.12s 윈도 내 캡처, 오류 0).

### V4 — URP 포스트 (블룸+비네트) (`7669414`, 캐시 `2442aaa76e15f544`)
- **게이트 실측 선행** (스펙 요구): 라이브 빌드 전투 중 rAF 720프레임 —
  p50 8.3 / **p95 10.0 / p99 10.2 ms** (예산 16.7 ms, 여유 ~6.7 ms) → 적용.
- `CinderPostProfile.asset` (직렬화 자산 — WebGL 셰이더 변형 보존): Bloom
  intensity 0.55·threshold 1.05 (진짜 발광체만 블룸)·scatter 0.6, Vignette
  0.22/0.45 다크 네이비. SceneBuilder가 글로벌 볼륨+카메라 포스트 배선.
- **PostFxGate**: `Application.isMobilePlatform`이면 카메라 포스트 플래그
  OFF — 모바일 티어는 이 하니스에서 미실측이므로 스펙 규칙(강등, 방치 금지)
  적용. 데스크톱 전용 적용.
- 포스트 ON 재실측: p50 8.3 / p95 10.0 / p99 10.3 ms — **포스트 비용이 노이즈
  이내**, 게이트 통과 확정. 라이브 스모크 오류 0
  (`deployed-v4-post-lobby.png`, `deployed-v4-post-combat.png`).

### 스펙 현황
- `deep-interview-vfx-terrain-command-hardening` **전 레인 배포 완료**:
  T-a 드레싱 → V2 벤트 fill → V3 원소 파티클 → K 키 난독화 → P 본 소켓
  프롭 → T-b 터레인 분할 → V1 시전 동기화 → V4 URP 포스트.

## GitHub Pages 배포 — Lane T-b 융합 터레인 연결성 분할 · 2026-08-05

### 변경 (spec `deep-interview-vfx-terrain-command-hardening` §Lane T-b)
- **abyss-chancel 융합 GLB 분할**: retained `textured-cleaned.glb`(1노드
  1메시)를 `convert_terrain.py --parts` 신설 경로로 연결성 기반 362개 섬
  분리 → 결정론 정렬(트라이 수 내림차순, 위치 타이브레이크) → ≥150 tri
  상위 48개 유지·명명(`terrain-abyss-chancel-part-NNN`) → 독립 등록(자체
  bbox, X 스팬 17 적합) + 섬별 접지(min-z→0) → 기존 TerrainImportPipeline
  경로로 임포트. **저작 시점 분리**(§3 계약), delete 자산 불사용.
- 프리팹 실측: slab 4 + apron 1 불변, part 48 신규. 분할 산출 알베도의
  DefaultTexturePlatform 2048 유입은 텍스처 상한 계약(≤1024)으로 즉시 보정
  — 상한 게이트가 실제로 회귀를 잡음.
- **echo-throne은 의도적 미분할**: 후보 자산이 전부 2D 빌보드(8×8 평면
  1섬)라 55° 카메라에서 종잇장 — §S4 비목표 확정, 테스트로 고정.
- `git tag -f pre-terrain-split-20260805` 사전 태깅(파괴적 자산 작업 계약).

### 게이트·배포
- EditMode **166/166 통과** — 신규 `TerrainPartsTests` 2종(48파츠+바닥
  불변, echo-throne 빌보드 비목표) (`unity-logs/test-results-104008.xml`).
- gh-pages `ad40851`, 캐시 버전 `d8d55ea7d9b6df7c` 라이브 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- Abyss Chancel 강하: 분할 파츠 유적 밴드(질감 있는 붕괴 콜로네이드)가
  상단 비전투 지대에 렌더, 기둥 해저드 3종·전투 판정 불변
  (`_workspace/current/engineering/deployed-tb-abyss-parts.png`).

## GitHub Pages 배포 — Lane P 본 소켓 장비 프롭 · 2026-08-05

### 변경 (spec `deep-interview-vfx-terrain-command-hardening` §Lane P)
- **랭크 티어 본 소켓 프롭**: 무기(RightHand)/랜턴(LeftHand)/클록(Chest) 3슬롯
  × 2밴드 — T0-1 없음 / T2-3 basic / T4-5 fine. `ActorView.AttachEquipProps`
  가 밴드별 멱등 갱신(런 중 랭크업 즉시 반영), `ResetForPool`에서 정리.
  비휴머노이드 리그는 무프롭 — §P2 전신 틴트가 하한.
- **자산 파이프라인**: retained 원작 프롭 2종(블레이드 .03/렐릭 .05, 원작
  런타임의 PROP_BLADE/RELIC_MESH와 동일 소스) + 절차 저작 클록 →
  `tools/blender/convert_equip_props.py` (소켓 공간 정규화, ≤800 tri 강제,
  총 3,832 tri) → `PropImportPipeline` (URP Lit 명시 머티리얼: FBX 임포트가
  emission을 드랍하고 차콜이 바닥에 묻히는 문제를 밴드 코딩 발광으로 해소
  — basic 미광 / fine 강발광: 무기 엠버·랜턴 시안·클록 진홍).
- delete 마킹 소스 불사용(§Non-Goals), 신규 EquipPropTests 5종(존재·렌더러·
  트라이 예산·착용 가능 월드 크기·URP 셰이더·휴머노이드 소켓 본).

### 게이트·배포
- EditMode **164/164 통과** (`unity-logs/test-results-102032.xml`).
- gh-pages `04e64c6`, 캐시 버전 `a64367c75e1720b4` 라이브 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- T5 시드 → Ember Gallery 강하: 우수 엠버 발광 블레이드 + 배면 진홍 클록 +
  좌수 시안 랜턴 글로우 3점 동시 렌더
  (`_workspace/current/engineering/deployed-lanep-props.png`).
- 로컬 검증: basic 밴드(차콜, T2) 근접 캡처와 T5 fine 밴드 대비 확인
  (`lanep-props-basic-closeup.png`, `lanep-props-fine.png`).
- 주의(실측): localStorage 시드는 **오리진 스코프** — localhost에서 시드 후
  github.io로 이동하면 미적용. 라이브 검증 시 라이브 오리진에서 재시드.

## GitHub Pages 배포 — V2 벤트 fill · V3 원소 파티클 · Lane K 키 난독화 · 2026-08-05

### 변경 (spec `deep-interview-vfx-terrain-command-hardening` §V2/§V3/§K)
- **V2 벤트 임박도 fill** (교차 세션 레인 `3a15a87`+`de34dc3`): 텔레그래프 링
  내부 디스크가 CycleT/VentPeriod에 비례해 0→반경으로 차오름 — "언제
  터지는가"가 한눈에 읽힘. 벤트당 1회 생성, 프레임당 할당 0.
- **V3 원소별 파티클 임팩트** (동 레인): 사전 생성 풀링 ParticleSystem 4종
  (볼트 보라 잔광 / 파동 녹색 리플 / 노바 엠버 파편 / 에이기스 시안 흡수),
  `Emit(count)` 전용, maxParticles 96, reduced-motion 시 count 절반. 검증된
  MakeUnlit 시드 경로 사용(URP Particles 셰이더는 빌드 내 참조 0으로 변형
  스트리핑 — per-particle 그라디언트는 사양 수정으로 면제). 링/스파크 문법
  증강, 대체 아님. 초기 미검증 URP Particles 시드 자산은 정리(`849dcbc`).
- **Lane K 키 저장 난독화** (`f017d3e`): `KeyVault` — 기기 파생 키(AES-CBC,
  `deviceUniqueIdentifier`+salt SHA-256) 위 `enc1:` 마킹 저장. 레거시 평문은
  로드 시 제자리 마이그레이션, 복호 실패(기기/브라우저 변경·변조)는 자동
  삭제 후 재입력 안내 — 기능 잠김 없음. UI 문구는 정직 계약대로
  "이 기기에만 난독화 저장" (암호화/안전 표현 금지). KeyVaultTests 8종.
- **HudKorean 글리프 갱신**: 새 토스트 글자(난독화 등)가 서브셋 폰트에
  없어 라이브에서 탈락 — `tools/gen_hud_font.sh` 재생성(436 glyphs, FULL
  coverage) 후 재배포로 해소.

### 게이트·배포
- EditMode **159/159 통과** (`unity-logs/test-results-095439.xml`).
- gh-pages `6d83ad8` → 글리프 수정 `b7431d0`, 최종 캐시 버전
  `e6ab57862f88d16b` 라이브 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- V2: Ember Gallery 벤트 3개가 서로 다른 위상의 fill 상태로 렌더
  (`deployed-v2-vent-fill.png`).
- V3: R 노바 직후 링+파편, F 에이기스 시전 시 시안 흡수 플래시 + 방패 40
  (`deployed-v3-nova-debris.png`, `deployed-v3-aegis-flash.png`).
- Lane K: 콘솔 `key <dummy>` 등록 → "Gemini 키 저장됨 (이 기기에만 난독화
  저장) — 자유 문장 명령 활성화" 토스트 전체 글자 정상 렌더
  (`deployed-lanek-key-toast.png`).

## GitHub Pages 배포 — 조합 스테이지 드레싱 (Lane T-a) · 2026-08-05

### 변경
- **드레싱 테이블 시스템** (spec `deep-interview-vfx-terrain-command-hardening`
  §Lane T-a): cinder-span 프리팹의 feature/prop 90종을 공용 드레싱
  라이브러리로 재사용, `StageCatalog.DressingPlacement` 정적 테이블(무 RNG)로
  조합 스테이지 3종에 스테이지별 드레싱 부여 — Ember Gallery(상단 능선 암괴
  4 + 좌하 포켓 + 하단 소품), Witness Well(좌우 대칭 감시자 4 + 상단 소품 열
  + 하단 아치 기념물), Ash Verdict(상단 재판정 매스 3 + 코너 기념물 + 하단
  소품). 배치는 전투 평면(248..1288 × 334..874) 밖 + 모든 해저드 반경+50
  클리어런스 준수. slab/apron은 불변.
- `GameDirector.ApplyStageDressing`: 스테이지 전환당 1회 실행(프레임당 0),
  라이브러리 자식의 **베이크드 피벗**(로컬 0, 메시에 위치 베이크)을 라이브
  렌더러 바운즈로 측정해 피벗 앵커 하위로 중심 정렬 — yaw/스케일이 메시
  중심 기준으로 작동.
- 라이브러리 원본이 밀리미터급 마이크로 데칼(바운즈 0.05–0.12 world unit)
  이라 테이블 스케일은 ×11–22 대역. Ash Verdict 측면 거석 2점은 시각 침범
  피드백으로 축소·후퇴 튜닝.

### 게이트·배포
- EditMode **151/151 통과** — 신규 `StageDressingTests` 5종 포함(테이블
  무결성: 라이브러리 자식 실존·feature/prop 접두사 강제·전투 평면 밖·해저드
  클리어런스·결정론) (`unity-logs/test-results-092856.xml`).
- gh-pages `ead692d`, 캐시 버전 `de0be8ac3e61a30f` — 라이브 데이터 리소스
  새 버전 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- Ember Gallery 강하: 드레싱 9/9 렌더
  (`_workspace/current/engineering/deployed-dressing-ember-gallery.png`).
- 로컬 최종 빌드에서 Witness Well·Ash Verdict 드레싱 확인
  (`dressing-witness-well.png`, `dressing-ash-verdict.png`).

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
