# Deployed release verification — 2026-08-04 texture-cap → 2026-08-05 spec-complete builds

Target: <https://akillness.github.io/hongT/> · gh-pages `d4c7392` · cache version
`61a0b09946ca5642` · data 26,549,778 B (−49.3 % vs prior) · wasm 9,117,062 B.

All checks below ran against the LIVE GitHub Pages build in a real headless
Chromium tab (CDP input, no simulation). Runtime errors / warning banners were
captured on every route; **all routes: 0 errors, 0 warnings**.

| # | Route | Viewport | What was exercised | Observed | Evidence |
|---|---|---|---|---|---|
| 1 | 로비 → 프롤로그 출정 | 1440×900 | Sortie click, WASD move, Space strikes | 체력 86, 웨이브 1, 적 4, 피격 비네트, 적 체력바 | `engineering/deployed-texcap-desktop-lobby.png`, `…-desktop-combat.png` |
| 2 | 로비 → 프롤로그 (모바일) | 390×844 DPR 2 | Sortie tap, combat entry | 체력 100, 웨이브 1, 적 3, 모바일 HUD 레이아웃 | `…-mobile-lobby.png`, `…-mobile-combat.png` |
| 3 | `?mode=arena` | 1440×900 | D move + Space, melee trade | 체력 44 (근접 피격 정상), 적 4, Q/E 스킬바, 적 체력바 | `…-arena-combat.png` |
| 4 | `campaign.html` 리다이렉트 | — | HTTP probe | 200 | curl |
| 5 | 캠페인 1단계 Cinder Span | 1440×900 | prologueDone 시드 → 강하 → 콤보/대시/Q/E/R/F | "Cinder Span — 웨이브 1/5" 배너, 방패 40 (Void Aegis), 기름 68 소모, 적 3→2, 분출구 텔레그래프 2, 장비/Lv HUD | `…-campaign-stage1.png`, `…-campaign-stage1-combat.png` |
| 6 | localStorage 영속 | — | v2 스키마 시드 후 재로드 | 로비가 프롤로그 "재훈련 가능" + Cinder Span 해금으로 반영 | route 5 첫 스크린샷 |
| 7 | 동료 명령 콘솔 (콘솔+VFX 빌드 `18b0fc1a992f9312`) | 1440×900 | 강하 → Enter 콘솔 → `nova` 제출 | 명령 힌트·0.2x 슬로모, "잿불 노바 시전" 피드백, 기름 100→55, 적 4→3, 점수 100, 노바 번 데칼 | `…-console-nova.png` |
| 8 | 모바일 프롤로그 회귀 (콘솔+VFX 빌드) | 390×844 DPR 2 | 출정 탭, 전투 진입 | 체력 100, 웨이브 1, 적 3, 모바일 HUD 정상 | `…-console-mobile-lobby.png`, `…-console-mobile-combat.png` |
| 9 | 모바일 캠페인 1단계 (콘솔+VFX 빌드) | 390×844 DPR 2 | 시드 → 강하 | "Cinder Span — 웨이브 1/5", "가로 화면을 권장합니다" 토스트, Q/E/R/F+SHIFT 스킬바, 적 4 | `…-console-mobile-dungeon.png` |
| 10 | Ember Gallery 드레싱+V2 fill (스펙 빌드) | 1440×900 | 시드 → 강하 | T-a 드레싱 9종 렌더, 벤트 3기 상이 위상 fill | `…dressing-ember-gallery.png`, `deployed-v2-vent-fill.png` |
| 11 | 원소 파티클 V3 (스펙 빌드) | 1440×900 | R 노바 / F 에이기스 | 노바 링+엠버 파편, 에이기스 시안 플래시+방패 40 | `deployed-v3-nova-debris.png`, `deployed-v3-aegis-flash.png` |
| 12 | Lane K 키 등록 (글리프 수정 빌드) | 1440×900 | 콘솔 `key <dummy>` | "이 기기에만 난독화 저장" 토스트 전 글자 렌더 | `deployed-lanek-key-toast.png` |
| 13 | Lane P 프롭 (T5 시드) | 1440×900 | Ember Gallery 강하 | 엠버 블레이드+진홍 클록+시안 랜턴 3점 동시 | `deployed-lanep-props.png` |
| 14 | Lane T-b 분할 파츠 | 1440×900 | Abyss Chancel 강하 | 유적 밴드 48파츠 소스, slab/apron 불변 | `deployed-tb-abyss-parts.png` |
| 15 | Lane V1 시전 글로우 | 1440×900 | Q 볼트 / F 에이기스 | 보라/시안 손 글로우 (0.12s 윈도 캡처) | `/tmp` 검증 후 릴리즈 노트 기록 |
| 16 | Lane V4 포스트 성능 | 1440×900 | rAF 720+480프레임 실측 | 포스트 OFF p95 10.0ms → ON p95 10.0ms (예산 16.7) | 릴리즈 노트 수치 |
| 17 | 최종 빌드 Witness Well (`2442aaa76e15f544`) | 1440×900 | 전 스테이지 해금 시드 → 강하 | 드레싱 감시자 8종+아치, 제단·기둥 해저드, T4 프롭 | `finalqa-witness-well.png` |
| 18 | 최종 빌드 Ash Verdict | 1440×900 | 강하 | 재판정 매스+코너 기념물 드레싱, 제단+분출구 3, V1 글로우 가시 | `finalqa-ash-verdict.png` |
| 19 | 최종 빌드 아레나 회귀 | 1440×900 | D+Space 교전 | 원작 아레나 백드롭 불변, Q/E 스킬바 | `finalqa-arena.png` |
| 20 | 최종 빌드 모바일 회귀 | 390×844 DPR 2 | 출정 → 전투 | 프롤로그 정상, PostFxGate로 포스트 미적용 경로 | `finalqa-mobile-combat.png` |
| 21 | 세로 로어 겹침 수정 (`efb632aac6ccf3e5`) | 390×844 DPR 2 | Ember Gallery 강하 | 로어 라인이 콤보 핍·Q/E/R/F 행 위로 이동 — 겹침 해소, 위→아래 로어/핍/스킬행/SHIFT 순 정렬 | `portrait-lore-fixed.png` |

Second deploy cycle (gh-pages `6ddd724`): source `7256cb5` (companion command
console + AOE/skill VFX pass), EditMode 146/146
(`unity-logs/test-results-082947.xml`), build `unity-logs/build-083019.log`
(data 26,558,801 B, wasm 9,140,333 B). Route 7 above ran against that live
build; routes 1–6 ran against `61a0b09946ca5642`.

Console-input caveat: headless CDP `keyboard.type()` cannot compose Hangul
(no IME), so the live-path proof used the parser's ASCII alias (`nova`).
Korean keywords are covered by the 20 CompanionCommandParser EditMode tests.

Fixture note: route 5 used a seeded v2 save
(`{"clearedMask":0,…,"prologueDone":true}`) to unlock stage 1 without a full
manual prologue clear — the same shape `CampaignStore.Save` writes; the lobby
parsed it and gated cards correctly, which is itself the persistence check.

Capture-environment caveat: Playwright's bundled headless Chromium lacks
proprietary audio codecs and logs `EncodingError: Unable to decode audio data`
during video capture. Real-browser smokes on the same URL log zero errors —
environment artifact, not a game defect.
## 2026-08-06 배포 사이클 — 걷기/휘두르기 모션 수정

[OBSERVED] 소스 `7343cd0` (내 `57f8afd` 모션 수정 + 형제 세션의
`b3ad28d` 인트로 릴 5프레임, `7343cd0` HUD 아틀라스 배선), gh-pages
`ce76295`, 캐시 버전 `a78283f49ff7e483`.

빌드: `bash tools/unity_batch.sh build` — `result=Succeeded errors=0
warnings=8 size=70,601,672 time=00:01:14`. 배포 산출물 47.2 MB
(`data` 36,196,342 B · `wasm` 10,472,622 B · `framework` 79,052 B ·
`loader` 48,106 B) — 계약 상한 120 MB 대비 39 %. 편집기가 프로젝트
잠금을 쥐고 있어 `/tmp/hongt-build` 클론(Library 포함 APFS 클론)에서
배치모드로 빌드했고, `Assets/ ProjectSettings/ Packages/ docs/ web/`를
`rsync -a --delete`로 `7343cd0` 작업트리와 일치시킨 뒤 빌드했다
(`Assets` 최종 동기화 차이 0건). 로그
`_workspace/current/engineering/unity-logs/build-170501.log`.

배포: `bash tools/deploy/deploy_pages.sh`.

[OBSERVED] 실브라우저 검증 2회 — 로컬(`python3 -m http.server` 상당,
`Content-Encoding` 없음 = Pages와 동일한 폴백 경로)과 라이브
<https://akillness.github.io/hongT/> 양쪽에서 Chromium(swiftshader)
헤드리스로 부팅:

| 항목 | 로컬 `a78283f49ff7e483` | 라이브 `a78283f49ff7e483` |
|---|---|---|
| 요청 실패(≥400) | 0 | 0 |
| 콘솔 에러 / 페이지 예외 | 0 / 0 | 0 / 0 |
| 로더 진행률 | 100 % | 100 % |
| WebGL 컨텍스트 | webgl2 | webgl2 |
| 캔버스 CSS / 백킹스토어 | 1280×853 / 1280×853 | 1280×853 / 1280×853 |
| 경고 배너 | 없음 | 없음 |
| 인트로 릴 | `Video/cinder-court-intro.mp4` 200 | 동일 파일 206 range 2건 |
| 렌더 픽셀 | 84,283색 | 46,146색 (배경 #050812 포함) |

`loader.js`는 Pages가 `gzip`으로, `.unityweb` 3종은 무압축으로 서빙되며
빌드의 `decompressionFallback=true` 경로로 정상 해제됐다. `index.html`에
루트 절대 URL 0건, 캐시 버스트 `?v=a78283f49ff7e483`가 4개 리소스에
정확히 1회씩. 증거: `qa/swing-motion/deployed-pages-boot.png`.

[OBSERVED] 배포 직후 라이브 `index.html`을 20초 간격으로 폴링해
`321e5e336d66b53d`(같은 사이클의 선행 배포) → `a78283f49ff7e483` 전환을
17:11:22에 확인했다. Pages 반영 지연 약 3분.

[INFERENCE] 형제 세션이 동일 소스(`7343cd0`)로 gh-pages `3ecc425`를
같은 시각에 배포했고 내 `ce76295`가 그 위에 올라갔다. 두 배포의 소스
커밋이 같으므로 유실된 변경은 없다.
