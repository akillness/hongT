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

## 배운 것 (다이제스트 런타임 벽)

**dotnet과 Unity 다이제스트는 비트 비교 불가** (float 하위비트, ~4 ULP,
FMA 추정 [INFERENCE]). 정수 필드는 동일. 용도 분리: 스탠드얼론 = 추가성
증명(pre/post 동일 런타임 비교), Unity 골든 = 배포 진실. qa/
golden-digests-cycle2.md 헤더에 명문화 — 하니스 재구축 금지.

## 교차 세션

- GUI Unity 에디터(Hub 경유) 세션이 프로젝트 락 점유 → 사용자 확인 후 종료.
  에디터가 재생성한 파일(.vscode, slnx, manifest.json unity-mcp 추가,
  URP GlobalSettings)은 **이 세션 산출물 아님** — 커밋 제외, 소유 세션 존중.
- CharacterRosterAnimationTests.cs: 이번 사이클 LFS 복구 후 통과 — conflicts.md
  잔여 리스크 해소.

## 미해결 리스크 / 이월

1. G8 인상 점수(≥4/5)·G7 repeat-rate(≥70%): 배포 후 구조화 플레이테스트 필요.
2. SpeechBubbleView.SpeakerColor 신규 보스 prefix 미분화(1줄).
3. Stage 3 미진행: G4(몰입 점수), G6 최종(소크/롤백 리허설), G2/G3/G5 최종
   측정(아키타입 로테이션 매트릭스 — 테스트 봇은 준비됨).
4. gh-pages 배포 미수행 — 빌드는 준비 완료(56.8MB), 배포는 사용자 확인 후.
5. 이월(one-mode-per-cycle 원칙): Ember Rest UI 심화, hold/recall §S 게이트
   패스, nan2026 제출 패키지.
6. P2 글로우·C1 트레일 라이브 육안(이월 2회째): 이번 스모크는 신규 스테이지
   중심 — 다음 배포 스모크에서 기존 스테이지 항목 포함할 것.

## 다음 사이클 진입 결정

**Stage 2 재진입 (retune)**: 콘텐츠 뼈대는 섰다. QA 아키타입 로테이션(봇 5종
준비 완료) → G2/G3/G5 측정 → 밸런스 리튠 → 배포+플레이테스트로 G8/G7 잔여
확정이 다음 beat. 신규 컨셉 추가보다 측정·조정 우선.
