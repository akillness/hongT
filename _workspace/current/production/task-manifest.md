# Task Manifest — run-id 20260809-dungeon-fun-authorship

next_public_beat = "gh-pages 라이브 — 처음 들어온 사람이 외부 문서 없이 플레이한다"

전 사이클(20260808-lobby-icon-rail)은 cycle-8 회고로 마감, 사이클 전용
아티팩트는 `_workspace/archive/20260808-lobby-icon-rail/`로 `git mv` 완료.

**Stage 1 재진입 (컨셉 전환).** cycle-8 회고의 "Stage 2 retune" 지정을
디렉터가 뒤집었다 — 근거는 `intake/production-brief.md` §진입 결정 뒤집기.

**상태: Stage 1 Phase 1a·1b 완료 + 범위 확정. Phase 1c(협상) 대기 —
실행 승인 전.**

## 이번 사이클이 답하는 질문

> 결정론 던전은 무엇으로 반복 가능해지는가.

조사 답: **정보를 가격으로 팔고, 숙달을 채점하고, 저작을 런 안으로 옮긴다.**

**사용자 제보가 그 답의 우선순위를 뒤집었다** — *"현재 진행하는게 인게임내
수정하는거 맞아?"* → 화면 2종을 cycle-10으로 미루고 **인게임 4수**로 재편성.

## 범위 (deep-interview `20260809-dungeon-fun-execution`, 모호도 15%)

| 수 | 내용 | 심 | 골든 |
|---|---|---|---|
| **W1 겨냥 넉백** | 런처 피니셔 넉백 원점을 입력 방향 반대편으로 | 호출 인자 | **던전 12행** |
| **W2 기믹 처치 크레딧** | 벽·해류·분출구 처치 이벤트 + 연출 + 기름 환급 | 이벤트 1비트 | 같은 12행 |
| **W3 현장 각인** | 레벨업 오퍼 풀 확장 (기존 각인 훅 5개 재사용) | 옵트인 | 미옵트인 무이동 |
| **W4 적 스킬** | 잡몹 4아키타입 행동 분화 (해시+임계+천장) | 변경 | 같은 12행 |
| ~~M-A 집행 영장~~ | 설계 유지 | — | **cycle-10 이월** |
| ~~M-B 판결 등급~~ | 설계 유지 | — | **cycle-10 이월** |

## 완료 (Phase 1a — 조사)

| task | owner | stage.phase | artifact | gate | status |
|---|---|---|---|---|---|
| 재미 적자 베이스라인 실측 (B1–B12) | director | 1.a | intake/production-brief.md §측정 | — | done |
| 장르 서베이 5레인 (15타이틀 × 30메커니즘) | designer | 1.a | .survey/dungeon-entry-fun-factors/ | G8 | done |
| **세피리아 전투 서베이** (8,058리뷰 · 리뷰 전문 721 + namu 6문서) | designer | 1.a | .survey/sephiria-combat-system/ | G8 | done |
| 서베이 아티팩트 계약 검증 (양쪽) | designer | 1.a | validate_survey_artifacts.py PASS ×2 | — | done |
| 서베이 designer 레인 미러 (양쪽) | designer | 1.a | design/trend-survey/{dungeon-entry-fun-factors,sephiria-combat-system}.md | G8 | done |
| 캘리브레이션 밴드 F1–F7 + 충돌 판정 C5–C7 | qa | 1.a | qa/benchmark-notes.md v2.0 | G2·G7 | done |

## 완료 (Phase 1b — 설계 + 범위 확정)

| task | owner | stage.phase | artifact | gate | status |
|---|---|---|---|---|---|
| 인테이크 브리프 + 진입 결정 뒤집기 | director | 1.b | intake/production-brief.md | — | done |
| 재미 설계 v2.0 (M-A/M-B/M-C) | designer | 1.b | design/dungeon-entry-fun-spec.md | G7·G8 | done |
| **범위 재조정 인터뷰** (Round 0 + 8, 모호도 15%) | director | 1.b | .omc/specs/deep-interview-dungeon-fun-execution.md | — | done |
| **설계 v3.0 개정** (W1·W2·W4 신설, 화면 2종 이월) | designer | 1.b | design/dungeon-entry-fun-spec.md §−1, §11–16 | G7·G8 | done |
| **참신성 스코어카드 v4 개정** (후보 교체) | designer | 1.b | design/novelty-scorecard.md cycle-9 절 | G8 | done |
| 코어루프 재모델링 (저작 루프 A1·A2 + 선택 압력) | designer | 1.b | design/core-loop.md cycle-9 절 | G7 | done |
| 폰트 사전 검증 (제안 UI 문자열 전수, getBestCmap) | qa | 1.b | design/dungeon-entry-fun-spec.md §10.1 | G1 | done |
| W1 골든 비용 실측 | director | 1.b | 아래 §골든 비용 | G6 | done |

## Phase 1c — 선결 (일부 완료)

| task | owner | stage.phase | artifact | gate | status |
|---|---|---|---|---|---|
| **선택 압력 센서스 (9스테이지 전수)** | qa | 1.c | qa/selection-pressure-census.md | G7 | **done — 설계 주장 반증** |
| **W4 아키타입 4종 행동 설계** | designer | 1.c | design/enemy-archetype-spec.md | G8 | **done (draft)** |
| **W4 해시·천장 수치 검증** (2,160 표본) | designer | 1.c | 같은 문서 §4 | G7 | **done** |
| **경제 entry 17·18·19 상정** | designer | 1.c | pm/negotiation-record.md | G5 | **done** |
| 경제 협상 서명 (entry 17·18·19 **동시**) | pm | 1.c | pm/negotiation-record.md §서명 라운드 | G5 | **done** — 3인 서명 |
| `skill_gap_oil_net_max` 산출 | pm | 1.c | pm/reward-bands.md §도출 | G5 | **done** — 0.28 (초안 0.35 폐기) |
| G5 parity 밴드 `[BEFORE]` 측정 | qa | 1.c | qa/parity-before-census.md | G5 | **done** — 14.2 / 5.2 세션, 차단 해제(D-28) |
| S3 노출 곡선 재계산 | qa | 1.c | qa/benchmark-notes.md | G2 | **불요 판정** — T-X6가 증명 |
| W4 옵트인 여부 판정 | director | 1.c | production/decision-log.md D-23 | G6 | **done** — 기본 적용 |
| 선택 압력 목표 재정의 승인 | director | 1.c | production/decision-log.md D-24 | G7 | **done** — 진행 밴드 |
| 적용 범위 판정 (강하 전용) | director | 1.c | production/decision-log.md D-22 | G6 | **done** — 프롤로그·시련 제외 |

### Phase 1c가 뒤집은 것 2건

**① 선택 압력 ≥0.75 — 9/9 실패.**
센서스 결과 0.400~0.727. 최고치조차 미달이고, **압력이 가장 낮은 곳이 게임이
시작되는 곳**이다(초반 3스테이지 0.400 vs 후반 0.623). 원인은 §4.4의 해저드
필터 — 기믹 1종 스테이지는 각인 면이 2개뿐이라 풀이 5에서 막힌다.
필터가 상황 의존적 가치를 만드는 대가로 **풀 크기 상한도 정한다.**

→ designer 채택: 단일 임계 → **진행 밴드(초반 ≥0.70 / 중반 ≥0.80 / 후반
≥0.85)** + 크기 축 3단계 + 스탯 5종. 디렉터 승인 필요.
→ **구조 문제는 미해소**: 초반이 여전히 가장 낮다. 부채로 등재.

**② W2 환급 앵커가 측정 의존이었다.**
designer 초안 "제단 수급의 50%"에서 제단 수급이 스테이지 길이 T에 의존하고
T가 미측정이다 — 같은 문구가 T=180s에서 270, T=360s에서 540을 뜻한다.
**§4r 위반을 designer가 자기 규칙에 대해 저질렀다.**

→ 앵커를 **처치 기름**(웨이브 표에서 결정, 측정 불요)으로 교체.
환급 비율 = `p × r / 6`이라 **스테이지에 무관**하다 — 상수 하나로 9스테이지
전부 같은 비율. 재제안 r=4 (비율 6.7~13.3%) + 경성 상한 15%.

## 완료 (Phase 1c 후속 — 문서 레인 전수)

코드 무접촉. 사용자 지시 "코드 외 나머지 전부 진행"에 따라 cycle-9가 건드리는
모든 레인 문서를 v3.0 범위로 갱신했다.

| task | owner | stage.phase | artifact | gate | status |
|---|---|---|---|---|---|
| 결정 로그 D-14~D-21 (범위 2회 변경 궤적 포함) | director | 1.c | production/decision-log.md | — | done |
| QA 테스트 계획 v3.0 (T-V/W/X/Y/Z 5군) | qa | 1.c | qa/test-plan.md | 전 게이트 | done |
| 게이트 측정 cycle-9 절 (측정 3건 + 미측정 명시) | qa | 1.c | qa/gate-measurements.md | 전 게이트 | done |
| 텔레메트리 v3.0 — **신규 필드 0개** 논증 | programmer·qa | 1.c | ops/telemetry-contract.md | G6 | done |
| 리워드 밴드 v3.0 (기름 축 신설 + 경고 3건) | pm | 1.c | pm/reward-bands.md | G5 | done |
| 수익 지도 v3.0 (간접 경로 + 17↔19 순효과) | pm | 1.c | pm/revenue-map.md | G5 | done |
| 세계관 — 적 아키타입 4종 서사 + 별칭 확정 | designer | 1.c | design/worldview.md | G1 | done |
| 인용 경로 감사 + 정정 2건 | qa | 1.c | (benchmark-notes, trend-survey) | — | done |

### 이 문서 레인이 새로 만든 판정 3건

**① 텔레메트리 신규 필드 0개 — 결정론이 텔레메트리를 대체한다.**
`SkillRoll(enemyId, wave, attackOrdinal)`처럼 모든 신규 측정 대상의 입력이
재현 가능하다. "무슨 일이 일어났는가"를 기록할 필요 없이 **재현하면 된다.**
무RNG의 운영상 이점을 이 사이클이 처음 명시적으로 쓴다.
예외는 사람이 만드는 값 둘 — G8 인상 점수, 오퍼 **수용** 여부(플레이어
입력이라 재현하려면 입력 로그가 필요하고 그건 스키마 변경이다). 둘 다 세션
관찰로 대체하고 신규 필드를 만들지 않는다.

**② parity 재측정이 지금 마지막으로 분리 가능한 시점이다.**
cycle-8 D-9가 지정하고 3사이클째 이월인데, v3.0이 이유를 악화시켰다 —
환급이 스킬 사용을 늘리면 클리어율이 오르고 유물 수입이 늘어 T5가 빨라진다.
**환급을 켜고 나면 로비 도달률 개선(cycle-8)과 기름 환급(cycle-9) 중
무엇이 T5를 당겼는지 영원히 분리할 수 없다.**

**③ G8 인상 점수가 6사이클째 미측정이고, 측정 대상이 달라졌다.**
cycle-2·3·4·5·8이 전부 여기서 멈췄다. 초판 대상은 정적 화면(스크린샷으로
측정 가능)이었으나 v3.0 대상은 **전투 중 체감**이라 플레이 세션이 필수다.
T-Z3(4종이 다르게 느껴지는가)과 T-Z4(무엇이 다른지 아는가)를 분리했다 —
전자만 통과하면 차이가 **노이즈로 읽히는 것**이지 행동 분화가 아니다.

## 계획 (Phase 1d — 구현, 승인 후)

### 심 레인

| task | owner | artifact | gate |
|---|---|---|---|
| W1 겨냥 넉백 — `KnockbackFrom` 원점 인자 변경 (런처 분기만) | programmer | Sim/CinderSim.cs | G7 |
| W2 기믹 처치 이벤트 1비트 + 기름 환급 | programmer | Sim/ (SimEvents 확장) | G7 |
| W3 `GrowthChoiceKind` 확장 + 오퍼 해시 (플레이어 항 포함) + 스테이지 필터 | programmer | Sim/ (옵트인 뒤) | G7 |
| W4 `SkillRoll` + 아키타입 임계 표 + 천장 | programmer | Sim/ | G7·G8 |
| `EquipNames` → `ProgressionGuide` 이동 | programmer | View/ | G6 (이월 2) |

### 뷰 레인

| task | owner | artifact | gate |
|---|---|---|---|
| W2 기믹별 처치 연출 + 사운드 | programmer | View/VfxDirector.cs, AudioDirector.cs | G4 |
| W4 적 행동 시각 구분 (4아키타입) | programmer | View/ActorView.cs | G4·G8 |
| W1 겨냥 피드백 (방향 표시) | programmer | View/ | G4 |
| 폰트 재생성 + 동반 커밋 (신규 문자열 발생 시) | programmer | tools/gen_hud_font.sh, Assets/Resources/Fonts/HudKorean.otf | G1 |

### 검증 레인 (디렉터 직렬 실행 — 배치모드 락)

| task | owner | artifact | gate |
|---|---|---|---|
| **골든 분해 1단계** — W1만 켜고 재고정, 행별 변화 기록 | director | qa/golden-decomposition-cycle9.md | **G6** |
| **골든 분해 2단계** — +W2, 추가 변화분 | director | 동일 | **G6** |
| **골든 분해 3단계** — +W4, 추가 변화분 | director | 동일 | **G6** |
| 불변 3행 바이트 동일 (모든 단계) | qa | Tests/EditMode/DungeonGoldenDigestTests.cs | G6 |
| W3 미옵트인 경로 바이트 동일 | qa | 동일 | G6 |
| 예고 예산 LCM 센서스 재실행 | qa | qa/gate-measurements.md | G2 |
| W4 결정론 (동일 입력 2회 동일, 다른 kills 다른 결과) | qa | Tests/EditMode/ | G7 |
| W4 천장 (연속 미발동 상한) | qa | Tests/EditMode/ | G7 |
| 변이 스윕 — **합의 항목부터** (§4q, 3항목 지정) | director | qa/mutation-sweep-cycle9.json | G6 |
| **G8 인상 점수 측정 — 플레이 세션에서** | qa | qa/gate-measurements.md#g8 | **G8** |
| 브라우저 스모크 — 2모드 + **적 4종 육안 구분** | director | qa/dungeon-weaponize-smoke/ | G4 |
| 게이트 판정 | director | production/gate-reviews/stage1-gates-v22.md | 전 게이트 |

## 골든 비용 `[OBSERVED]`

```
CampaignSimTests.BotInput:146-147  →  카이팅하며 MoveX/MoveY를 계속 채움
CinderSim.cs:2496                  →  ResolveFinisherVariant가 3타 시점의 그 값을 읽음
CinderSim.cs:2496 (_dungeon 게이트) →  _comboVariant는 던전에서만 산출
```

**골든 파일럿은 이미 방향 피니셔를 발동시키고 있다.**

| 행 | W1 영향 |
|---|---|
| arena-hack · arena-frozen · prologue (3행) | **불변** |
| 던전 12행 | **이동** |

선례: AMENDMENT #9가 던전 다이제스트를 의도적으로 움직이고
`Momentum_DungeonDigestSitsAtTheAmendedValue`로 재고정했다.

## 실행 편성 (확정)

**병렬 집필 + 직렬 검증.** 심·뷰 레인은 동시에 쓰되 **Unity 배치모드를 쓰는
검증은 디렉터가 한 줄로 세워 돌린다.**

근거: §4 — *"Unity 배치모드는 프로젝트를 잠근다. 병렬 세션은 실행 순서를
조율하고, 락 충돌은 XML 없이 EXIT=1로 조용히 끝나므로 결과 파일 존재를 먼저
확인한다."* 레인을 늘려도 검증은 직렬화되므로 "N레인 = N배"는 거짓이다.

| 레인 | 값싼 확인 | 비싼 확인 |
|---|---|---|
| 심 | dotnet 1.7초 | 배치모드 (디렉터) |
| 뷰 | `import-only` ~15초 | 배치모드 + 브라우저 (디렉터) |
| QA | — | 배치모드 (디렉터) |

## 설계가 스스로 부과한 제약 (구현 레인 구속)

| # | 제약 | 근거 |
|---|---|---|
| 1 | **W4는 예고 기믹을 추가하지 않는다** | cycle-3 S4. ash-march·cinder-sluice 예고 점유율 71–75%, 여유 정확히 1 |
| 2 | W4 방어 계열이 잡몹 피해를 늘리면 **S3 노출 곡선 재계산 후 승인** | cycle-3 C1이 "per-hit을 안 어기고 실효 상한을 넘는" 구멍을 찾았다 |
| 3 | 해시 입력은 **플레이어에게 보이는 값만** (enemyId·wave·ordinal·kills·hitsTaken) | cycle-3 S5 불가시 게이지 금지 |
| 4 | W3 미옵트인 경로 골든 바이트 동일 | `DungeonProgressionSpec.cs` 선례 |
| 5 | `IHackSnapshot` 무접촉 — `IGrowthChoiceSnapshot` 재사용 | CLAUDE.md §1 동결 |
| 6 | 레벨업 정지 전환 **안 함** — 오퍼 수용률 측정 먼저 | C7. §4q "만장일치 결정" 신규 생성 방지 |
| 7 | W1은 **런처 분기만** 바꾼다 | 넷 다 바꾸면 후퇴 베기(포위 탈출)의 의미가 사라진다 |
| 8 | W4 행동은 **속성(Ember/Frost/Veil/Void)과 정합** | `ElementOf(visual)`이 이미 4종을 가른다 — 행동이 속성을 배신하면 안 된다 |

## 미해결 (2026-08-09 승인 라운드 이후)

**승인 완료 5건 — 전부 처리됨**

| # | 항목 | 판정 | 기록 |
|---|---|---|---|
| 1 | 적용 범위 | **강하(던전) 전용** — 프롤로그·시련 제외 | D-22 |
| 2 | W4 옵트인 | **기본 적용** (항상 분화) | D-23 |
| 3 | 선택 압력 목표 | **진행 밴드** 0.70/0.80/0.85 | D-24 |
| 4 | entry 17×19 순효과 | **의도** — 숙련 보상 축 + `skill_gap_oil_net_max` 0.28 신설 | D-25 |
| 5 | entry 17·18·19 서명 | **3인 서명 완료** | 협상록 §서명 라운드 |

**착지 차단 — 없음** (D-28로 해제)

parity `[BEFORE]` 측정 완료(`qa/parity-before-census.md`) — 카이팅 14.2 세션,
수거 5.2 세션. `[AFTER]`와의 차이가 entry 17에 귀속된다.

**구현 착수 가능.** 남은 것은 전부 병행 항목이다.

**designer 잔여 3건** (`design/enemy-archetype-spec.md` §9) — 구현과 병행 가능

1. 기질 상수의 정확한 값 (넉백 저항 ×0.75/×1.40 등은 제안이고 미검증)
2. Shade 불규칙 정지의 주기 — **예고 없이 읽기 어렵게 만드는 것**과
   **그냥 짜증나는 것**의 경계가 여기 있다. QA 인상 점수 대상
3. par 시간 — M-B와 함께 cycle-10 이월

**parity가 만든 신규 안건 2건**

6. **밴드 형식 개정** — director, cycle-10. `parity_sessions_band: [10,20]`이
   단일 값을 가정하는데 실측은 분포다(행동이 분산의 85%). 제안:
   `parity_greedy_floor: 8` + `parity_measurement_protocol: dual-bot`.
   **환급 착지의 조건이 아니다** — 위반은 환급 이전에 이미 있었다.
7. **entry 6 재개시** — pm, cycle-10. 3사이클째 블로커였던 "봇 0/6 클리어"가
   해소됐다(카이팅 5/6, 수거 T5 6/6). 단 수입 곡선의 축이 랭크가 아니라
   행동이므로 **전제부터 다시 세워야 한다**.

**QA 신규 측정 요구 2건** (승인이 만든 것)

4. 기름 순수지를 **아키타입 회피율별로** 분리 측정 (단일 평균 금지) — D-25
5. **Possessed 사망 전 접촉 횟수** 기록 — `skill_gap_oil_net_max` 0.28이
   `hits=2` 위에 서 있다. 4회면 설계값 자체가 상한에 닿는다(0.274 vs 0.28).
   2가 아니면 상한 재산출

## 이월 승계

| # | 항목 | 출처 | 이번 처리 |
|---|---|---|---|
| 1 | G5 parity 밴드 재측정 | D-9 | **Phase 1c 편입** — entry 17·18·19와 같은 계산 |
| 2 | 장비·각인 2중 구현 | D-6 | **재서술 + 한 줄 수정.** 실측 결과 데이터 중복은 `EquipNames` 한 줄뿐이고 어휘·가격·상한은 이미 단일 소유자가 있다. 진짜 부채는 **LobbyView/MetaScreenView 화면 2개 통합 미결**이며 이번 범위와 접점 0 → 만료 연장 |
| 3 | 강하 액션 92u, 지원 플로어 375에서 40.3px | D-11 | 병렬 레인 유지 |
| 4 | 레일 아이콘 103.3u, 범위 밖 320 CSS 미달 | — | 범위 밖, 무기한 |

## 캐리포워드 (건드리지 않음)

`.vscode/settings.json`, `Data/Plugins/lib_burst_generated.wasm`, `HongT.slnx`,
`Packages/*`, `ProjectSettings/PackageManagerSettings.asset` — 다른 세션 작업.
`_workspace/current/qa/lobby-rail-smoke/*.png`는 다른 세션이 17:01에 파일명을
바꿔 재촬영했다 — §5에 따라 무접촉.
스테이징은 명시 pathspec만.
