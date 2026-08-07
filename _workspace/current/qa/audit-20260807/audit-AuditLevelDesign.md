# 감사 리포트 — 레벨·웨이브·캠페인 구조 (AuditLevelDesign)

날짜: 2026-08-07 · 대상 커밋: 워크스페이스 현재 상태 (읽기 전용 감사)
판정 기준: docs/SIM_SPEC*.md + _workspace/current/design/*.md (프로젝트 계약).
외부 레퍼런스 가이드는 일반론 참고자료이며 판정 기준이 아니다 (§3 프레이밍 참조).

---

## 1. 스펙 조항별 대조표

### 1.1 프롤로그 (SIM_SPEC_HACKSLASH §1)

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| 웨이브 3개, 4/6/8기 | `docs/SIM_SPEC_HACKSLASH.md:30` | [OBSERVED] `HackTypes.cs:388-399` (`PrologueWaves=3`, `PrologueSpawns={4,6,8}`), 소비: `CinderSim.cs:2785` | 구현됨 |
| 기믹 없음 | 동 §1 | [OBSERVED] `HackTypes.cs:153-157` (`HackConfig.Prologue()` — StageId만, Hazards 없음), 테스트 `HackSimTests.cs:314` | 구현됨 |
| 보스 없음 | 동 §1 | [OBSERVED] `CinderSim.cs:2783-2787` (`_prologue → _pendingBoss=false`), 테스트 `HackSimTests.cs:341` | 구현됨 |
| 아레나 수치 계약 그대로, 스킬/대시/콤보 비활성 | `docs/SIM_SPEC_HACKSLASH.md:28-29` | [OBSERVED] `CinderSim.cs:2861-2862` (프롤로그=아레나 HP 곡선 58+min(92,(w-1)*9)), 테스트 `HackSimTests.cs:346-377` (스킬/대시 무시), `HackSimTests.cs:1097-1104` (58 HP) | 구현됨 |
| 3웨이브 클리어 → prologue-clear 종료 | 동 §1 | [OBSERVED] `CinderSim.cs:2836-2841` (`_wave >= PrologueWaves → ClearRun("prologue-clear")`), 테스트 `HackSimTests.cs:381-406` | 구현됨 |
| 클리어 → prologueDone=true 저장 | `docs/SIM_SPEC_HACKSLASH.md:33-34` | [OBSERVED] `GameDirector.cs:497-514` (StageCleared & State.Prologue → `_data.PrologueDone=true` → `CampaignStore.Save`), 직렬화 `CampaignStore.cs:74,125` | 구현됨 |

### 1.2 캠페인 스테이지 구성 (SIM_SPEC_CAMPAIGN §Stages)

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| 심 앵커 3종: cinder-span W=5 / abyss-chancel W=6 / echo-throne W=7 | `docs/SIM_SPEC_CAMPAIGN.md:21-25` | [OBSERVED] `CampaignTypes.cs:232-249` (Waves 5/6/7, BossVisual Commander/Commander/Monarch) | 구현됨 |
| 웨이브 1..W는 아레나 규칙 그대로 | `docs/SIM_SPEC_CAMPAIGN.md:27` | [OBSERVED] `CinderSim.cs:630-637` (`SpawnCountForStageWave`: 아레나 공식 `3+floor(w*1.2)` 유지, **단 아레나의 5웨이브마다 보스 슬롯은 제외**), `CinderSim.cs:2790-2793` | 구현됨(해석 분기 — §6-1) |
| 웨이브 W+1 = 보스 1기 + 호위 min(8, 3+idx*2) | `docs/SIM_SPEC_CAMPAIGN.md:28` | [OBSERVED] `CinderSim.cs:620-623` (`EscortCountForStage`), `CinderSim.cs:632-635`, 상수 `CampaignTypes.cs:138-140` | 구현됨 |
| 보스 처치 → StageCleared, 잔존 잡몹 즉시 페이드 | `docs/SIM_SPEC_CAMPAIGN.md:30` | [OBSERVED] `CinderSim.cs:2674-2678` (보스킬 → `RaiseRank` + `ClearStage`), `CinderSim.cs:3141-3153` (`ClearRun` → `FadeRemainingEnemies`, Reason "stage-clear") | 구현됨 |
| 기믹 배치 테이블 (vent/pillar/altar 좌표) | `docs/SIM_SPEC_CAMPAIGN.md:69-75` | [OBSERVED] `CampaignTypes.cs:163-182` (좌표·위상 스펙과 1:1 일치) | 구현됨 |
| 플레이어 사망 = GameOver(재도전) | `docs/SIM_SPEC_CAMPAIGN.md:32` | [OBSERVED] `GameDirector.cs:518-530` (사망 시 유물만 적립, 클리어 미기록) + cycle2 B2 퀵리트라이 | 구현됨 |

### 1.3 6스테이지 체인 (integrated-campaign-level-spec §3 — achilles §T)

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| 체인 `1 → 1+2(보스) → 2 → 2+3(보스) → 3 → 1+3(최종)` | `integrated-campaign-level-spec.md:47-51` | [OBSERVED] `StageCatalog.cs:109-154` — cinder-span → ember-gallery → abyss-chancel → witness-well → echo-throne → ash-verdict. 합성 스테이지는 앵커 재사용 + 해저드 오버라이드로 구역 혼합: ember-gallery(앵커 zone1 + chancel vent/pillar, :85-91), witness-well(앵커 zone2 + throne altar, :93-99), ash-verdict(앵커 zone3 + cinder vents, :101-107) | 구현됨(부분 — 적 조합 미분화, §2-G2) |
| 각 노드는 (룸 구성, 적 조합, 보스 유무) 결정적 참조 | `integrated-campaign-level-spec.md:51` | [OBSERVED] 룸구성=해저드/터레인/드레싱은 노드별 분화(`StageCatalog.cs:195-237`), **적 조합은 전 노드 동일** — `CinderSim.cs:2896` visual=(wave+spawnIndex)%4 고정 로테이션, 스테이지 파라미터 없음 | 부분 |
| §T1 하드코딩 8곳 데이터화, 체인 외 ID 로비 리다이렉트 | `integrated-campaign-level-spec.md:53-55` | [OBSERVED] 카탈로그 테이블 단일 소스(`StageCatalog.cs:109`), 체인 외 ID: `GameDirector.cs:270-274` (TryGet 실패 → EnterLobby), 부팅 딥링크: `GameDirector.cs:95-96` (잠금 실패 → EnterLobby) | 구현됨 |
| §T2 ClearedMask 마이그레이션 (구 3-bool → 비트 0/2/4) | `integrated-campaign-level-spec.md:111` | [OBSERVED] 테스트 `StageCatalogTests.cs:173-185` (legacy → bits 0/2/4), `:227-240` (소급 잠금 없음) | 구현됨 |
| §T5 검증 (6엔트리 무결성·해저드 비중첩·기둥 통로 ≥r합+52·보상 {0,0,1,1,2,2}) | `integrated-campaign-level-spec.md:113` | [OBSERVED] `StageCatalogTests.cs:36-105` (6엔트리·앵커·prereq), `:242-274` (비중첩 + 기둥 2×26 통로), `:105` (보상 분포) | 구현됨 |
| 3룸 + Ember Rest (§1.2) | `integrated-campaign-level-spec.md:29-32` | [OBSERVED] 룸=단일 아레나의 웨이브 그룹으로 재해석. Ember Rest: `GameDirector.cs:325-385` (클리어 → 직접 후속 노드 존재 시 BeginEmberRest → 준비 오퍼 → 다음 스테이지 연쇄 진입) | 부분(재해석 — §6-2) |
| 구역 3 최종 보스 "2페이즈 + 소환" | `integrated-campaign-level-spec.md:31` | [OBSERVED] 심 진실은 AMENDMENT #4 **3페이즈** + Monarch P2 소환 3기(`CinderSim.cs:1886-1891`, `HackTypes.cs:771`) | 문서 불일치(§2-G5) |

### 1.4 웨이브·난이도 수식 (스펙 ↔ 구현 상수)

| 수식 | 스펙 | 구현 | 판정 |
|---|---|---|---|
| 던전 적 HP `86+min(140,(wave-1)*11)` | `docs/SIM_SPEC_HACKSLASH.md:51` | [OBSERVED] `HackTypes.cs:412-414`, 소비 `CinderSim.cs:2858-2860` | 구현됨 |
| 아레나/프롤로그 HP `58+min(92,(w-1)*9)` | SIM_SPEC | [OBSERVED] `CinderSim.cs:23-24, 2861-2862` | 구현됨 |
| 웨이브 스폰수 `min(20, 3+floor(w*1.2))` | SIM_SPEC | [OBSERVED] `CinderSim.cs:37-38, 600-608` | 구현됨 |
| 스폰 간격 `max(0.28, 0.62-w*0.018)` | SIM_SPEC | [OBSERVED] `CinderSim.cs:39-41, 2830` | 구현됨 |
| 접촉 피해 `min(18, 7+floor(w*0.8))` | SIM_SPEC | [OBSERVED] `CinderSim.cs:31-33, 2567` | 구현됨 |
| 적 이속 `min(128, 78+w*3.2+(id%3)*2.5)` | SIM_SPEC | [OBSERVED] `CinderSim.cs:27-30, 2616-2617` | 구현됨 |
| 정예: 던전 7번째 스폰마다, 웨이브당 1, HP×3/접촉×1.5 | `docs/SIM_SPEC_HACKSLASH.md:85-87` | [OBSERVED] `CinderSim.cs:2863-2869` (ordinal 누적·`%7==0`·웨이브당 1), `HackTypes.cs:529-532` | 구현됨 |
| 던전 보스 HP = 웨이브HP × BossHealthMul(6) × DungeonBossHealthMul(6) | `docs/SIM_SPEC_HACKSLASH.md:138-141` | [OBSERVED] `CinderSim.cs:2871-2884` | 구현됨 |
| 보스 웨이브 아레나 클램프 15% 축소 + 텔레그래프 1.5 s | `docs/SIM_SPEC_HACKSLASH.md:180-181` (§10) | [OBSERVED] `ClampToArena`는 고정 마진(`CinderSim.cs:3295-3297`), 뷰에도 보스웨이브 축소 없음. §W 링은 0.9 s(`VfxDirector.cs:427`) | **미구현** (§2-G1) |
| 페이즈별 공격간격/텔레그래프/스킬쿨 (1.37/1.16/0.99 등) | `docs/SIM_SPEC_HACKSLASH.md:124-128` | [OBSERVED] 상수 선언만(`HackTypes.cs:758-760`) — 소비 없음. 실제 공격 쿨은 전 적 공통 `1.22+min(0.38,w*0.025)`(`CinderSim.cs:2469-2470`). 속도/범위/접촉 벡터는 소비됨(`:2624, :2562, :2582-84`) | 부분 — 주 판정은 AuditPresentation/AuditBossPlayer 소유 |

---

## 2. 갭/상충 목록

| # | 심각도 | 내용 | 근거 |
|---|---|---|---|
| G1 | **High** | §10 "보스 웨이브 시작 시 아레나 클램프 15% 축소(deformation) + 텔레그래프 1.5 s" 미구현. 심 클램프는 고정 마진, 뷰에도 축소 없음. §W 경고 링(0.9 s)은 별도 신규 스펙(combat-feel §W, 원 스펙 0.6 s → 릴리즈 0.9 s)이며 §10 항목의 대체가 아님. 보스전 공간 압박 부재 = 레벨디자인상 보스 웨이브의 '방' 감각 상실 | [OBSERVED] `CinderSim.cs:3295-3297`, `VfxDirector.cs:427`, `docs/RELEASE_NOTES.md:98-105`. AuditPresentation 교차 확인 동일 판정(판정 소유: 본 리포트) |
| G2 | **Med** | integrated §3.1 "각 노드는 (룸 구성, **적 조합**, 보스 유무)를 결정적으로 참조" — 적 조합이 전 6노드 동일: visual=(wave+spawnIndex)%4 균등 로테이션, 스테이지별 적 구성 파라미터가 스키마에 없음(`CampaignConfig`에 적 조합 필드 부재). 노드 간 차이는 해저드/터레인/드레싱/보스비주얼/웨이브수뿐 | [OBSERVED] `CinderSim.cs:2896`, `CampaignTypes.cs:71-93` |
| G3 | **Med** | 스테이지 간 난이도 곡선이 웨이브 번호에만 결박 — 모든 스테이지가 웨이브 1을 동일 값(4기, 86 HP)에서 시작. 후반 스테이지 난이도 상승분은 +1웨이브·+2호위·보스HP +8%p·해저드 1개뿐인데, 메타 성장(장비 T5 +30% 공격, 스탯 10pt, 동료 3슬롯)이 이를 크게 앞지름 | [OBSERVED] 수식 §4 표 / [INFERENCE] 후반 스테이지가 체감상 더 쉬워질 개연성 — 실플레이 검증 필요(§7) |
| G4 | **Low** | §W 링 지속: 설계 문서 0.6 s(`combat-feel-boss-phase-spec.md:28`) vs 구현 0.9 s(`VfxDirector.cs:427`) vs 릴리즈 노트 0.9 s. 문서-구현 간 수치 표류(릴리즈 노트가 사실상 개정 기록) | [OBSERVED] 상기 3파일 |
| G5 | **Low** | integrated §1.2 "구역 3은 최종 보스(**2페이즈** + 소환)" vs 심 진실 AMENDMENT #4 **3페이즈** + Monarch P2 소환 3기. 문서 B(8-05)가 v0.1 시점 서술을 잔존시킴 — 구현이 아닌 문서 결함 | [OBSERVED] `CinderSim.cs:1886-1891`, `docs/SIM_SPEC_HACKSLASH.md:122-137` |
| G6 | **Low** | SIM_SPEC_CAMPAIGN §Page flow "campaign.html 정적 허브 + 스테이지 카드 3장"은 v0.2.0 HACKSLASH §0(campaign.html → index.html 리다이렉트) + §9(로비 카드) + 6스테이지 체인으로 대체됨. CAMPAIGN 문서에 supersede 표기 없음 | [OBSERVED] `docs/SIM_SPEC_CAMPAIGN.md:7-17` vs `docs/SIM_SPEC_HACKSLASH.md:18-20`, `StageCatalog.cs:109-154` |
| G7 | **Low** | 보스 웨이브 호위도 정예가 될 수 있음(ordinal 누적이 보스 웨이브를 관통: abyss W7 호위 ordinal 42, echo W8 호위 56 → 정예 발생). 스펙은 금지도 명시도 안 함 — 보스+정예 동시 존재는 위협 스파이크(호위 1기 HP 3배) | [OBSERVED] `CinderSim.cs:2863-2869` 게이트가 `_dungeon && !boss`뿐 / [INFERENCE] 의도 여부 불명 — §7 |

---

## 3. 외부 레퍼런스 대비 갭 분석

프레이밍: 레퍼런스(`llm-wiki/raw/sources/2026-08-07-hackslash-design-guide-reference.md`)는 일반론이다.
아래 각 항목을 **[OBSERVED] 스펙 자체의 갭** vs **[INFERENCE] 의도적으로 채택 안 한 레퍼런스 아이디어**로만 분류한다.
가이드의 유니티 구현 관점(AttackData SO, OnTriggerEnter, Animator, PostProcessing)은 CLAUDE.md §1 순수 C# 결정론 심 경계와 충돌하므로 수정 제안 대상이 아니다.

| 레퍼런스 개념 | 프로젝트 상태 | 분류 |
|---|---|---|
| 포인트 기반 웨이브 생성 (적 타입별 코스트, baseWavePoints·growthFactor) | 부재. 웨이브는 개수 공식(`3+floor(w*1.2)`) + 균등 비주얼 로테이션. 적 타입별 코스트 차등의 전제인 '행동이 다른 적 타입' 자체가 없음 | [INFERENCE] 의도적 미채택 — RNG 금지·결정론 계약(SIM_SPEC_CAMPAIGN §Determinism) 하에서 개수 공식이 포인트 예산을 대체. 단, **적 타입 다양성 부재는 스펙 차원의 갭**(아래 빌드 체크 항목) |
| 난이도 곡선 — 학습 구간(낮은 증가율·단일 타입) → 복합 구간 → 클라이맥스 스파이크 | 구조적으로 충족: 프롤로그(스킬 봉인·기믹 없음·4/6/8) = 학습 구간, 웨이브 1은 정예 불가능(ordinal<7), 보스 웨이브 = 총 HP 3.8~3.9× 스파이크(§4 표) + "소수 강유닛+지원" 조언과 일치(보스1+호위3~7). 구간별 증가율 튜닝 노브는 없음(전 구간 선형 고정) | 곡선 존재 [OBSERVED]. 구간별 성장률 파라미터 부재는 [INFERENCE] 의도적 단순화(동결 수치 계약 문화) — 현 스테이지 길이(≤8웨이브)에서 실익 낮음 |
| DDA (동적 난이도 조정, rubber-banding) | 부재 (스펙·구현 모두) | [INFERENCE] 의도적 미채택 — "동일 입력 → 동일 Digest" 결정론 계약과 정면 충돌. 프로젝트의 난이도 조절 수단은 플레이어 주도 메타(장비/스탯/동료/Ember Rest 오퍼)와 퀵 리트라이(cycle2 B2)로 대체 |
| 던전 리듬: 전투방–루팅–챌린지–보스 | 부분: 매크로 리듬은 전투 웨이브 → 보스 → **Ember Rest**(회복·준비 교체) → 다음 노드로 존재(`GameDirector.cs:335-377`). 루팅은 전투 중 인터리브(픽업·파편), 전용 루팅/탐험 방·중간 챌린지 방은 없음. integrated §1.2의 '3룸' 개념이 구현에서 단일 아레나+웨이브 그룹으로 수렴 | 스펙 차원: 룸 개념은 **있으나**(integrated §1.2) 구현이 재해석 [OBSERVED]. 탐험/루팅 전용 공간은 스펙에도 없음 — [INFERENCE] 단일 씬·단일 아레나 아키텍처(HACKSLASH §0)의 의도적 결과 |
| 리스크–리워드: 선택 루트(안전 vs 위험), 타임어택/노데스 보너스 | 프리미티브는 존재: 정예 추출(전투 중 2 s 정지 채널 = 리스크 → 로스터/유물), 유물 제단(1.2 s 체류 → 기름), 분출구(순수 리스크). **선택형 경로·추가 조건 보상은 스펙·구현 모두 부재** — 체인은 단선(strict chain, `StageCatalogTests.cs:109-126`) | 스펙 차원의 갭 [OBSERVED] — 단선 체인은 명시적 설계(integrated §3.1). 루트 분기 도입은 새 기획 결정 필요(§7) |
| 빌드 체크 포인트 (몰려오는 소형몹·방어무시·원거리 등 적 유형) | 부재. 전 적이 동일 근접 추적 AI — 차이는 스탯 지터(id%3)·원소(스킬 상성 방어측)·정예 배수뿐. 원거리/실드/힐러 등 행동 타입 없음(동료 아키타입에는 원거리 존재 — `HackTypes.cs:585-589` — 적에는 없음) | **스펙 차원의 갭 [OBSERVED]** — SIM_SPEC 어디에도 적 행동 타입 분화 조항이 없음. 레퍼런스가 지적하는 '빌드 과강/과약 점검' 기능이 현 레벨디자인에 없다는 점은 실질적 설계 공백 |
| 희귀도 테이블 (Common/Rare/Epic/Legendary 가중치) | 코드에 없음 — 결함 아님. 의도적 설계 분기: 결정적 모듈러 드롭(`enemyId%7==3`, 보스 확정) + T0–T5 랭크(`HackTypes.cs` EquipTiers)가 확률 희귀도를 대체 | [INFERENCE] 의도적 미채택 (RNG 금지 계약). 상세 판정은 AuditLootDrops 소유 |
| 텔레그래프 공정성 (범위 사전 표시) | 충족: vent 0.8 s 텔레그래프(심+뷰), §W 웨이브 도착 링, 보스 텔레그래프 0.8 s 고정 선언(단 소비는 부분 — §1.4 마지막 행) | [OBSERVED] 대체로 구현됨 |

---

## 4. 수식 기반 난이도 곡선 표 (구현 수식 기준, 던전 모드)

수식([OBSERVED] `CinderSim.cs`/`HackTypes.cs`): 몹수 `min(20,3+floor(1.2w))` (보스웨이브는 `1+min(8,3+idx*2)`),
HP `86+min(140,11(w-1))`, 정예 HP×3(ordinal%7==0, 웨이브당 1, 스테이지 누적), 접촉 `min(18,7+floor(0.8w))`(보스×2, P2×1.25/P3×1.45, 정예×1.5),
간격 `max(0.28,0.62-0.018w)`, 공격쿨 `1.22+min(0.38,0.025w)`, 이속 `min(128,78+3.2w+(id%3)2.5)`, 보스 HP=웨이브HP×36.

### cinder-span (W=5, 보스 웨이브 6, 호위 3)
| 웨이브 | 개체 | 몹 HP | 정예 HP | 접촉 | 스폰간격 | 이속 | 웨이브 총 HP(누적 위협) |
|---|---|---|---|---|---|---|---|
| 1 | 4 | 86 | — | 7 | 0.602s | 81–86 | 344 |
| 2 | 5 | 97 | 291(1기) | 8 | 0.584s | 84–89 | 679 |
| 3 | 6 | 108 | 324 | 9 | 0.566s | 88–93 | 864 |
| 4 | 7 | 119 | 357 | 10 | 0.548s | 91–96 | 1071 |
| 5 | 9 | 130 | 390 | 11 | 0.530s | 94–99 | 1430 |
| **6(보스)** | 3+보스 | 141 | — | 11×2(P2 ×1.25/P3 ×1.45) | 0.512s | 97–102 | **5499** (보스 5076) |

### abyss-chancel (W=6, 보스 웨이브 7, 호위 5)
| 웨이브 | 개체 | 몹 HP | 정예 HP | 접촉 | 간격 | 총 HP |
|---|---|---|---|---|---|---|
| 1–5 | cinder와 동일 | 86–130 | w2부터 매웨이브 1기 | 7–11 | 0.602–0.530s | 344–1430 |
| 6 | 10 | 141 | 423 | 11 | 0.512s | 1692 |
| **7(보스)** | 5+보스 | 152 | 456(호위 중 1기, ordinal 42) | 12×2 | 0.494s | **6536** (보스 5472) |

### echo-throne (W=7, 보스 웨이브 8, 호위 7 / ash-verdict 동일 앵커)
| 웨이브 | 개체 | 몹 HP | 정예 HP | 접촉 | 간격 | 총 HP |
|---|---|---|---|---|---|---|
| 1–6 | 위와 동일 | 86–141 | 매웨이브 1기 | 7–11 | 0.602–0.512s | 344–1692 |
| 7 | 11 | 152 | 456 | 12 | 0.494s | 1976 |
| **8(보스)** | 7+보스 | 163 | 489(호위 중 1기, ordinal 56) | 13×2 | 0.476s | **7335** (보스 5868) + Monarch P2 소환 3기(+489) |

관찰 (전부 [OBSERVED] 수식 유도):
- 웨이브 1..W 구간은 순선형(HP +11/w, 개체 +1~2/w, 간격 −0.018 s/w). 캡(HP 226@w14, 개체 20@w15, 간격 0.28@w19, 접촉 18@w13.75)은 **캠페인 범위(≤8) 밖** — 무한 아레나 전용.
- 보스 웨이브는 직전 웨이브 대비 총 HP **3.8~3.9× 스텝** — 레퍼런스의 '클라이맥스 스파이크' 문법과 정합.
- 세 앵커의 1..5 웨이브는 완전 동일 — 스테이지 간 차이는 꼬리 웨이브(+1~2), 호위(+2/스테이지), 보스 HP(+7.8%/스테이지), 해저드 구성뿐 (§2-G3).
- 프롤로그(58/67/76 HP, 무기믹)→던전 w1(86 HP+기믹+정예 예정)의 +48% HP 점프는 콤보 1.5× 피니셔·대시·스킬 4종 해금이 상쇄 — 학습 곡선으로 정상 [INFERENCE].
- 페이즈별 공격간격 열(1.37/1.16/0.99)은 스펙 선언 대비 **미소비**(전 적 공통 쿨) — 주 판정 AuditPresentation/AuditBossPlayer 소유.

---

## 5. 프롤로그→캠페인 해금 흐름 판정

체인: 심 `ClearRun("prologue-clear")`(`CinderSim.cs:2836-2841`) → `SimEvents.StageCleared`(`:3149`) →
`GameDirector.OnRunEvents`(`GameDirector.cs:497-507`: State.Prologue → `_data.PrologueDone=true` → 2.2 s 리빌 카메라) →
`CampaignStore.Save`(`:514`, 직렬화 `CampaignStore.cs:125`) →
`StageCatalog.IsUnlocked`(`StageCatalog.cs:293`: **`!data.PrologueDone → false` 전 스테이지 공통 게이트**) + prereq 단선 체인(`:295-297`) →
로비 표기(`LobbyView.cs:148-153`: 미클리어 시 "필수 훈련"+엠버 펄스, 클리어 후 "재훈련") →
딥링크 게이트(`GameDirector.cs:93-96`: `?mode=campaign`은 `IsStageUnlocked` 통과 필수, 실패 시 로비; `?mode=prologue`는 상시 허용 — 프롤로그엔 잠금이 없으므로 스펙 §0 "잠금 검사"와 모순 없음).
사망 시 PrologueDone 미기록(StageCleared 미발화) — 정확.

**판정: 구현됨.** 스펙 §1(`SIM_SPEC_HACKSLASH.md:33-34`) "캠페인 스테이지 해금은 프롤로그 클리어가 선행 조건" 충족.
테스트: `StageCatalogTests.cs:109-126`(PrologueDone=true 전제 하 단선 체인), `HackSimTests.cs:381-406`(클리어 경로). 195/195 통과(`unity-logs/test-results-175105.xml`).

---

## 6. 사람 판단 필요 항목

1. **체인 표기 vs 전 노드 보스**: integrated §3.1 체인은 노드 2/4/6에만 "(보스)" 표기, §1.2는 "구역 1·2는 보스 페이즈로 종결". 구현은 **전 6노드가 보스 웨이브로 종결**(앵커 공유의 필연). 표기가 '구역 경계 보스'만 강조한 것인지, 비보스 노드 의도가 있었는지는 문서로 판정 불가 — 기획 의도 확인 필요.
2. **3룸 재해석의 승인 여부**: '전투 룸 → 보스 룸 → Ember Rest'가 '웨이브 그룹 → 보스 웨이브 → Ember Rest 오버레이'로 수렴한 것이 승인된 축소인지. RoomObjective 텍스트(`StageCatalog.cs:50`)가 룸 개념의 잔존 표현.
3. **§3.3 플레이타임 25–30분(구역당 8–10분)**: 정적 감사로 검증 불가 — 실측 필요. 웨이브 수 기준으로는 노드당 6–8웨이브 × (스폰 꼬리+전투+2.15 s 인터미션)이며 TTK는 빌드 의존.
4. **G3 메타 성장 대비 스테이지 곡선 평탄**: 후반 스테이지 체감 난이도 역전 여부는 플레이테스트 판단.
5. **G7 보스 웨이브 정예 호위**: 의도 여부 스펙 침묵.

## 7. 이월 증거 vs 신규 증거

- **이월(재검증됨)**: `gjc-campaign-lane.md`의 구현 범위 서술(보스 웨이브 조성·클리어 경로·테스트 요구 1–8) — 본 감사에서 전 항목 현재 코드 라인으로 재확인. 레인 리포트 파일(`gjc-campaign-lane-report.md`)은 **부재** — 이월 증거로 쓸 수 없어 코드 직접 대조로 대체.
- **이월(재검증 안 됨)**: `unity-logs/test-results-175105.xml` 195/195 — 실행 재현 금지 제약상 결과 파일 신뢰(2026-08-05 산출 추정). 상수·로직은 현재 소스에서 독립 재검증.
- **신규**: §1 대조표 전 라인 인용, §4 곡선 표(수식 유도 계산), G1~G7, §3 레퍼런스 갭 분류, AuditPresentation 교차 확인(클램프 15%·§W 0.9 s·보스 페이즈 벡터 미소비).
- **함정 회피 확인**: Amendment #7 동료 자율성 '앵커 오프바이원'은 본 감사 범위 밖이며 재보고하지 않음(`llm-wiki/wiki/hongt-companion-autonomy-tick-order-trap.md` 준수).
