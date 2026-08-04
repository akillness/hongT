# Cycle 1 Retrospective — run-id 20260805-visible-impact

## 게이트 표 (측정값)

| 게이트 | 기준 | 측정 [OBSERVED] | 판정 |
|---|---|---|---|
| G1 코어 루프 안정 | EditMode 전체 초록 | 76/76 (73→74 외부 로스터 테스트 편입→76 신규 겹침 테스트 2) | PASS |
| G4 성능 예산 | 빌드 ≤120 MB, errors 0 | `result=Succeeded size=80581397 errors=0 warnings=2` (경고는 기존 CS0618 2건) | PASS |
| G6 ops | 배포 diff 발생 + 라이브 갱신 | gh-pages 3커밋(0eae3ce→28ccc72→dca28f8), 각 배포마다 build 해시 변경 | PASS |
| G7 컨셉/연출 일치 | 스펙 백로그 항목의 코드화 | M1·L1·P2·C1·C3·G1·U1(핍/컴팩션)·캡처§5 자막 — 8항목 라이브 | PASS |
| G2/G3/G5/G8 밸런스·수익 | 수치 계약 | 심 무변경 — 해당 없음(이번 사이클 범위 밖) | N/A |

## 웨이브별 산출 (커밋)

- W1 `1909d89`+`bd0d7eb`: 16방향 표시 요(공격 프레임은 심 ±1 스냅 유지), 로비 보스 Idle(이종 릭 Show 클립 회피), 장비 랭크 글로우(BaseColor MPB — _EMISSION 키워드는 MPB로 불가 확인), 콤보 트레일 티어(1x/1.5x/2x, 피니셔 골드), 장비 획득 골드 플래시(0.4s 전용 분모)
- W2 `1d4ea8c`: 스킬 카드 컴팩션(라벨 제거·아이콘 승격·데스크톱 96×76·행 스팬 574→500u), **실측 버그 수정** — 콤보 핍(y=52)이 대시 카드 rect(18..106) 내부에 3건 충돌 → y=102 이동, 리드아웃×카드 겹침 테스트 2티어 신설(InteractiveRects 사각지대 봉쇄), CS0618 1건
- W3 `0d30f5c`: 히트 스파크 전용 12풀(공유 풀 축출 방지, 프레임 예산 6), 소환수 전투 응시(16방향 게이즈, M1 델타 요 오버라이드, NaN 센티널)+휴식 Idle, 보스 화자 자막(티어 인지 배치)

## 테스트가 일한 순간

신설 겹침 테스트가 **개발 중 실제 결함을 잡았다**: 화자 자막 고정 y=128이 Phone 티어 lift(+120)에서 스킬 카드 rect 안에 완전히 매몰 — `PhoneDungeon_SkillRow_DoesNotCoverReadouts` 실패(75/76) → 티어 인지 배치로 수정 → 76/76. 사각지대 봉쇄의 즉각적 회수.

## 교차 세션 충돌 (conflicts.md)

`CharacterRosterAnimationTests.cs`(타 세션, untracked)가 `CinderCourt.EditorTools` 정적 참조로 전체 컴파일 차단 — 컴파일 에러는 testFilter보다 선행하므로 우회 불가. 최소 침습(리플렉션 타입 조회)으로 로컬 수정, **파일은 커밋하지 않음**(소유 세션 결정 존중). 이후 mtime 확인 결과 소유 세션이 계속 편집 중.

## 미해결 리스크

- P2 글로우·C1 트레일은 라이브 육안 검증 미수행(배포 diff와 테스트로만 확인) — 다음 사이클 QA 스모크 대상.
- 소환수 게이즈는 심 타게팅과 같은 iso 가중 메트릭이지만 심의 `NearestEnemyIndex`와 미세하게 다른 순회(뷰는 첫 최근접) — 순수 장식이라 무해, 어긋나 보이면 조정.
- 다른 세션이 damage-number 경로를 병행 확장 중 — GameView 충돌 주의.

## 다음 사이클 진입 결정

**Stage 1 재진입** (컨셉 확장): 이월 백로그 = T1/T2 StageCatalog·CampaignStore 마이그레이션(6스테이지 전제), Ember Rest UI(심 시임 완비·View 소비자 0), 가디언 hold/recall(§S 게이트), nan2026 제출 패키지(HTML/PDF/플레이 영상 — compresso 스킬 요청 잔여분).
