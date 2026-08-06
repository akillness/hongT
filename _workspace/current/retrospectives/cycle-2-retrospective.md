# Cycle 2 Retrospective — run-id 20260805-dungeon-gimmicks

## 게이트 표 (측정값)

| 게이트 | 기준(표준 quality-gates.md — cycle-1의 재매핑 넘버링에서 복귀) | 측정 [OBSERVED] | 판정 |
|---|---|---|---|
| G7 draft | 루프 모델+구현 | core-loop.md 3종 + EditMode 183/183 (신규 기믹 행동 테스트 포함) | PASS |
| G1 draft | 세계관 정합 | worldview.md 신설, 신규 문자열 위반 0, 폰트 456글리프 FULL | PASS |
| G6-ops draft | 텔레메트리+리소스 매니페스트+빌드 | ops/telemetry-contract.md + resource-manifest.md + build 56.8MB errors 0 warnings 0 | PASS |
| G8 사전 | 빈도 ≤2/≥5 | current 1/11 · pylon 1/11 · wall 2/11 | 조건부 PASS(인상점수 잔여) |
| R1-R8 | 회귀 | dotnet pre/post 12행 동일 + Unity 골든 15행 고정 + 마스크/레거시/EmberRest/ReducedMotion 테스트 | PASS |

## 산출 (한 사이클)

- **신규 던전 3종**: cinder-sluice(재의 수문, W8) → ember-bastion(불씨 요새,
  W8) → ash-march(재의 행진, W9). 해금 체인 연장, 카탈로그 6→9, 마스크
  0x3F→파생 0x1FF(재발 불가 구조).
- **신규 기믹 종류 3종** (AMENDMENT #5, docs/SIM_SPEC_DUNGEONS.md):
  tide-current(대칭 푸시 레인 — 기믹 최초로 적에게도 작용, 독트린 변경 명문화),
  ember-pylon(적 보호 파괴 오브젝트, 오라 −40%), ash-wall(시간표 침식 벽,
  대칭 틱 피해). 전부 무RNG 고정 타임테이블 — 서베이 key gap(학습 가능한
  해저드 안무 = 시장 공백)을 정체성으로 채택.
- 보상: 첫클리어 유물 +6/+8/+10(협상 서명), ash-march 동료 scout-echo.
- 테스트 166 → **183** (기믹 행동 7, 골든 6, 카탈로그/스토어 3, EmberRest 확장).
- 가시 검증: 로비 9카드 스크롤, 3스테이지 인게임 스모크 4캡처(qa/smoke-*).

## 환경 복구 (이 머신 최초 셋업 — cycle-3 필독)

이 사이클은 **미설정 머신**에서 시작했다. 복구한 로컬 의존성:
1. Unity 6000.5.6f1 + WebGL 모듈 (Hub 헤드리스 설치, ~/Applications 아님 —
   /Applications/Unity/Hub/Editor). unity_batch.sh는 UNITY_BIN 없이 동작.
2. git-lfs (`brew install git-lfs && git lfs pull`) — FBX/폰트가 포인터면
   휴머노이드/프롭/폰트 테스트 7건이 오해를 부르는 실패를 낸다.
3. ~/Library/Fonts/NanumBarunGothic.otf — gen_hud_font.sh의 미선언 의존성.
   **공식 zip**(hangeul.pstatic.net/hangeul_static/webfont/zips/
   nanum-barun-gothic.zip)만 U+2026 포함 — GitHub 아카이브 판은 글리프 부족.
4. dotnet 8 (스탠드얼론 심 검증용 — 선택).

**브라우저 스모크 레시피 (v1.4에서 발견 — 재발견 금지)**: 후반 콘텐츠를
육안 확인하려고 웨이브를 갈아 넣지 말 것. ① Unity WebGL 세이브는
**localStorage** 키 `abyssal-lantern:unity:campaign` (IndexedDB 아님) —
부팅 전 JSON을 심으면 진행도·스탯·장비를 임의 지정할 수 있다.
② `?mode=campaign&stage=<id>` 딥링크로 즉시 진입(잠긴 스테이지는 로비로
폴백하므로 ①이 선행돼야 한다). ③ **동료 활성화 필수** — 자율 DPS 없이는
블라인드 입력 봇이 웨이브 1에서 처치 0으로 죽는다(실측). 동료를 켜면
8웨이브 스테이지의 보스까지 도달한다.

## 배운 것 (다이제스트 런타임 벽)

**dotnet과 Unity 다이제스트는 비트 비교 불가** (float 하위비트, ~4 ULP,
FMA 추정 [INFERENCE]). 정수 필드는 동일. 용도 분리: 스탠드얼론 = 추가성
증명(pre/post 동일 런타임 비교), Unity 골든 = 배포 진실. qa/
golden-digests-cycle2.md 헤더에 명문화 — 하니스 재구축 금지.

## 교차 세션

- GUI Unity 에디터(Hub 경유) 세션이 프로젝트 락 점유 → 사용자 확인 후 종료.
  에디터가 재생성한 파일(.vscode/settings.json, HongT.slnx, Packages/
  manifest.json의 unity-mcp 항목, packages-lock, burst wasm)은 **이 세션
  산출물 아님** — 커밋 제외. 리베이스를 위해 일시 stash했다가 사이클 종료
  시점에 **워킹트리로 전부 복원 완료**(stash 비움, `git status`에 미커밋
  변경으로 남아 있음 — 소유 세션이 커밋 여부 결정).
- CharacterRosterAnimationTests.cs: 이번 사이클 LFS 복구 후 통과 — conflicts.md
  잔여 리스크 해소.
- **origin/main 19커밋 선행 발견**(타 세션: S8-a 보스 3페이즈, 위협 화살표,
  테스트 +40): `dungen` 브랜치를 origin/main(3e2e3a1)에 리베이스. 충돌은
  .meta GUID 3건뿐 — main GUID 채택(타 워크트리 Library 캐시 정합).
  리베이스 후 재게이트: **EditMode 223/223** (183+40, 양 세션 테스트 전부
  초록 — 골든 15행 생존, S8-a와 기믹 상호 간섭 없음), 빌드 57.05MB errors 0
  (warnings 2건은 main 세션 코드 소유 — LobbyView:125 CS0618,
  HudView:119 CS0414).
- push 차단: 이 머신 자격증명(leeseockmin)에 akillness/hongT 쓰기 권한 없음
  (403). SSH 키도 부재. **로컬 `dungen` 브랜치에 커밋 완료 — push는 권한
  있는 자격증명으로 수행 필요** (사람 판단 항목).

## 미해결 리스크 / 이월

1. G8 인상 점수(≥4/5)·G7 repeat-rate(≥70%): 배포 후 구조화 플레이테스트 필요.
2. ~~SpeechBubbleView.SpeakerColor 신규 보스 prefix 미분화~~ — **닫힘
   (2026-08-06, v1.4)**: prefix 매칭 자체를 제거하고 화자 분류를
   StoryCatalog(`VoiceOf`)로 이동 + 9스테이지 순회 가드 신설.
   `qa/gate-measurements.md` §v1.4.
3. Stage 3 미진행: G4(몰입 점수), G6 최종(소크/롤백 리허설), G2/G3/G5 최종
   측정(아키타입 로테이션 매트릭스 — 테스트 봇은 준비됨).
4. gh-pages 배포 미수행 — 빌드는 준비 완료(56.8MB), 배포는 사용자 확인 후.
5. 이월(one-mode-per-cycle 원칙): Ember Rest UI 심화, hold/recall §S 게이트
   패스, nan2026 제출 패키지.
6. P2 글로우·C1 트레일 라이브 육안(이월 2회째): 이번 스모크는 신규 스테이지
   중심 — 다음 배포 스모크에서 기존 스테이지 항목 포함할 것.
7. **신규 (v1.4 감사에서 발견)** — 로비 전 버튼이 44 CSS px 터치 하한 미달
   (390×844 portrait 실측: 강하/서약 41.0×13.7 · 탭 58.6×21.5 · 스탯 +
   25.4×21.5 · 재훈련 54.7×21.5). SIM_SPEC_HACKSLASH §9 위반이고 v1.3
   이전부터 존재. 카드 피치·스크롤·탭이 함께 움직이므로 designer+pm 협상
   안건. 현재는 `LobbyLayoutTests` 동결 래칫으로 회귀만 차단 중.
8. **ash-march 피날레 과열 — 라이브 증거 추가 (v1.4)**: v1.2가 "골든 hp 8
   생존"으로 플래그한 안건에 브라우저 실측이 붙었다. 풀장비(스탯 10/10/10,
   장비 5/5/5, 최대체력 220)로 진입해 **무입력 20초에 체력 220 → 16**.
   여전히 사람 플레이테스트가 판정 주체지만, 협상 테이블에 올릴 수치는
   이제 있다(`qa/gate-measurements.md` §v1.4).
   **머지 후 갱신(origin/main 3d8727b)**: 저쪽 hold-charge 수정이 봇을
   강하게 만들어 **골든 ash-march hp 8 → 52**로 올라갔다. 과열이 일부
   자연 해소된 셈이니 협상 전에 재측정할 것 — 위 20초 수치도 머지 전
   빌드 기준이다.
9. **골든 민감도 저하 (머지 관찰)** — 재고정 후 여러 행의 정수 4필드가
   3700|4|15|2로 수렴했다(체력·좌표는 여전히 갈림). 회귀 감지력이 예전만
   못하니 다음 사이클에서 골든 봇 스크립트 다양화를 검토할 것.
10. **entry 5 '평균' 조항 재개봉** — v1.4가 n=1 런 간 비율로 충족 처리한
   것은 과장이었다(머지에서 6.0×로 깨짐 — 배수는 여전히 2). 뷰가 통제하는
   배수만 게이트로 남기고, 평균 조항은 표본 연구 필요 항목으로 되돌렸다.

## 다음 사이클 진입 결정

**Stage 2 재진입 (retune)**: 콘텐츠 뼈대는 섰다. QA 아키타입 로테이션(봇 5종
준비 완료) → G2/G3/G5 측정 → 밸런스 리튠 → 배포+플레이테스트로 G8/G7 잔여
확정이 다음 beat. 신규 컨셉 추가보다 측정·조정 우선.
