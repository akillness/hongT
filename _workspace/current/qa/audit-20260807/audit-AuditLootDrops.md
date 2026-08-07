# 드롭/루트 경제 감사 — AuditLootDrops (2026-08-07)

대상: 픽업 3종 계약(SIM_SPEC.md §Pickups), 캠페인 장비 드롭(SIM_SPEC_CAMPAIGN.md §Item drops), 핵앤슬래시 §3 추출 보상·§6 장비 획득경로/유물 화폐, 로비 구매(CampaignStore/LobbyView/GameDirector).
판정 기준은 프로젝트 계약(docs/SIM_SPEC*.md)이며, 외부 레퍼런스 가이드는 일반론 참고자료로만 대조한다(프레이밍 가드 준수).

---

## 0. 결정론 조건표 — 어떤 조건에서 무엇이 확정 드롭되는가 (소스별) — Acceptance (a)

이 저장소의 드롭은 설계상 결정론이다: Sim 레이어에 `System.Random` 0건, 전 드롭 경로가 모듈러 산술뿐 [OBSERVED — CinderSim.cs 드롭 경로 전수]. 아래 표의 분수는 확률이 아니라 **결정적 모듈러 순환의 장기 빈도**이며, 각 개별 킬의 드롭은 조건식으로 100% 확정된다. 적 id는 1부터 순차 증가(`_nextEnemyId`, CinderSim.cs:2854,2917 [OBSERVED]).

결정 조건 (우선순위 순, CinderSim.cs:2705-2709):
1. `isBoss` → relic-mote **확정** (캠페인/던전 보스는 추가로 장비 랭크+1 직접 부여, 2673-2678).
2. 캠페인/던전이고 `enemyId % 7 == 3` → EquipShard **확정**.
3. 그 외 → `(PickupKind)(enemyId % 3)` **확정** (0 ember / 1 oil / 2 relic).

| 소스 | 모드 | ember-shard(+18HP) | oil-flask(+35유) | relic-mote(+1유물/+250점) | EquipShard(랭크+1) | 비고 |
|---|---|---|---|---|---|---|
| 일반 처치 | 아레나/프롤로그 | 1/3 | 1/3 | 1/3 | — | `kind=(PickupKind)(id%3)` CinderSim.cs:2709. 드롭률 100%(모든 킬 드롭) |
| 일반 처치 | 캠페인/던전 | 6/21 (2/7≈28.6%) | 6/21 | 6/21 | 3/21 (1/7≈14.3%) | `id%7==3` 우선 → EquipShard, 아니면 `id%3` (CinderSim.cs:2705-2709). lcm(3,7)=21 주기에서 shard 대상 id(3,10,17)가 %3 잔여류를 정확히 1개씩 잠식 → 3종 균등 유지 [OBSERVED 산술] |
| 정예 처치 | 던전 | 일반과 동일 테이블 | ← | ← | ← | 픽업 테이블 차등 없음. 추가 채널: 시체 마커 10s → 추출 시 신규 로스터+피해8% 또는 중복 유물+30 (CinderSim.cs:2654-2658, 1476; HackTypes.cs:537) |
| 보스 처치 | 아레나 | — | — | **100% 확정** | — | `isBoss → RelicMote` CinderSim.cs:2705-2706. SIM_SPEC.md:95-96 "드롭은 relic-mote 고정" 일치 |
| 보스 처치 | 캠페인/던전 | — | — | 스폰 100% but **회수 불가** (§2 G-1) | **확정 랭크+1** (픽업 아님, 직접 부여) | 슬롯 `stageIndex%3`, 캡 5 (CinderSim.cs:2673-2678, 3122-3138) |

공통 파라미터: 수명 12s(`PickupLifetime` SimTypes.cs:214), 자력 78 아이소 거리(`PickupMagnetRadius` SimTypes.cs:215, 적용 CinderSim.cs:2727-2729 — deltaY×IsoY 가중 후 반경² 비교), 회수 즉시 적용(CollectPickup CinderSim.cs:2743-2764).

유물(메타 화폐) 유입 채널 총괄: ① relic-mote 회수 +1 (캠페인 킬의 ~2/7), ② 중복 추출 +30, ③ 던전 종료 시 뱅킹(클리어 GameDirector.cs:541, 패배 GameDirector.cs:526). 실측 코호트: MomentumTests.cs:291-292 던전 스크립트 13킬 → relics 3 (2/7×13≈3.7의 회수분) [OBSERVED].
유출: 로비 구매 티어당 [2,4,7,11,16] (슬롯당 총 40, 3슬롯 만렙 120 유물).

---

## 1. 스펙 조항별 대조표

| # | 조항 (스펙 위치) | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|---|
| P1 | 드롭 종류 `enemyId%3` → ember/oil/relic (SIM_SPEC.md:87-88) | 0:+18HP / 1:+35oil / 2:+250점·relics+1 | CinderSim.cs:2709 (kind), 2745-2762 (효과); 상수 SimTypes.cs:216-218 (`RelicScore=250, EmberShardHeal=18, OilFlaskCharge=35`) | **구현됨** |
| P2 | 수명 12s (SIM_SPEC.md:89) | 12 s | SimTypes.cs:214 `PickupLifetime=12f`; 감쇠·만료 CinderSim.cs:2724, 2736-2739 | **구현됨** |
| P3 | 자력 반경 78, 아이소 거리 (SIM_SPEC.md:89) | 78, iso 가중 | SimTypes.cs:215; CinderSim.cs:2727-2729 (`(Δy×1.42)²` 포함 비교) | **구현됨** |
| P4 | 회수 즉시 적용 (SIM_SPEC.md:89) | 동일 tick 적용 | CinderSim.cs:2729-2733 (같은 tick Collect+Remove); 테스트가 same-tick 회수 검증 | **구현됨** |
| P5 | 아레나 보스 드롭 relic-mote 고정 (SIM_SPEC.md:95-96) | 100% relic | CinderSim.cs:2705-2706 `isBoss ? RelicMote` | **구현됨** |
| C1 | 캠페인 보스 확정 1드롭, 슬롯 `stageIndex%3`, 즉시 랭크+1, 캡 5 (SIM_SPEC_CAMPAIGN.md:41) | 확정, 모듈러 슬롯 | CinderSim.cs:2673-2678 `RaiseRank(_config.StageIndex % EquipSlotCount)`; 캡 CinderSim.cs:3126-3134 `Math.Min(MaxEquipRank,…)`; 상수 CampaignTypes.cs:127-128 | **구현됨** |
| C2 | 일반 처치 `enemyId%7==3` → EquipShard 스폰, 기존 3종 유지 (SIM_SPEC_CAMPAIGN.md:42-43) | 4번째 kind 추가 | SimTypes.cs:18 `EquipShard=3`; CinderSim.cs:2707-2708; 상수 CampaignTypes.cs:134-135 (`ShardDropModulus=7, Remainder=3`) | **구현됨** |
| C3 | EquipShard 회수 시 `킬수%3` 슬롯 랭크+1 (SIM_SPEC_CAMPAIGN.md:43) | kills%3 | CinderSim.cs:2753-2757 `RaiseRank(_kills % EquipSlotCount)` | **구현됨** (테스트 공백 — §2 T-2) |
| C4 | 드롭 규칙 결정적, RNG 금지 (SIM_SPEC_CAMPAIGN.md:40, 92) | 모듈러만 | 전 드롭 경로에 난수 없음(id/kills/stageIndex 모듈러만) [OBSERVED — CinderSim.cs 드롭 경로 전수] | **구현됨** |
| C5 | 클리어 시 장비 드롭 1개 확정 (SIM_SPEC_CAMPAIGN.md:31) | 보스킬=클리어 | C1과 동일 경로, `ClearStage()` 직전 부여 CinderSim.cs:2676-2677 | **구현됨** |
| C6 | 남은 잡몹 즉시 페이드 — 무득점·무드롭 (SIM_SPEC_CAMPAIGN.md:30) | 페이드만 | CinderSim.cs:3155-3164 `FadeRemainingEnemies` ("survivors fade without scoring or dropping") | **구현됨** |
| C7 | 캠페인 모드에서만 장비 지속 저장 (SIM_SPEC_CAMPAIGN.md:45) | 캠페인 키 한정 | GameDirector.cs:543-550 (클리어 시 max 병합), CampaignStore.cs:14-15 | **구현됨** (패배 시 처리 스펙 침묵 — §5 J-2) |
| H1 | 중복 추출 유물 +30 (SIM_SPEC_HACKSLASH.md:91) | +30 | HackTypes.cs:537 `ExtractionDuplicateRelics=30`; 적용 CinderSim.cs:1475-1477 | **구현됨** |
| H2 | 장비 획득경로 (a) 인런 드롭: 보스 확정 + id%7 파편 (SIM_SPEC_HACKSLASH.md:116) | 캠페인 규칙 상속 | 던전은 `_campaign=_dungeon` (CinderSim.cs:305-307)으로 C1-C3 경로 그대로 상속 | **구현됨** |
| H3 | 획득경로 (b) 로비 구매 `[2,4,7,11,16]` 유물/티어 (SIM_SPEC_HACKSLASH.md:117) | 비용 사다리 | GameDirector.cs:426 `EquipCosts={2,4,7,11,16}`, 428-448 (tier≥5 거부, 잔액 검사, 차감); LobbyView.cs:51 동일 테이블 + 185-188 (버튼 게이팅) | **구현됨** (중복 정의 — §2 G-2; 테스트 공백 — §2 T-3) |
| H4 | 유물 = 메타 화폐, 런 종료 시 누적 저장 (SIM_SPEC_HACKSLASH.md:118) | 클리어+패배 모두 | 클리어 GameDirector.cs:541, 패배 GameDirector.cs:518-529 (`_data.Relics += sim.Relics`) | **구현됨** (프롤로그 런 제외 — §5 J-1) |
| H5 | 보스 처치 시 기존 장비 드롭 + 동료 해금 + StageCleared (SIM_SPEC_HACKSLASH.md:144) | 3종 동시 | CinderSim.cs:2673-2678 + GameDirector.cs:552-560 (첫 클리어 동료) + 3149 (StageCleared) | **구현됨** |
| H6 | 지속성 v2 스키마 `relics` 필드 (SIM_SPEC_HACKSLASH.md:189-192) | int 누적 | CampaignStore.cs:20 `public int Relics`; 라운드트립 테스트 StageCatalogTests.cs:199-220 | **구현됨** |

**요약: 대조 17개 조항 전부 "구현됨". 스펙 문면과의 직접 상충 0건.** 아래 갭은 스펙이 침묵하는 영역 또는 품질/커버리지 결함이다.

---

## 2. 갭/상충 목록 — Acceptance (b)

### G-1 (Med) 캠페인/던전 보스 relic-mote가 스폰되지만 구조적으로 회수 불가
- [OBSERVED] 보스 킬 tick: `SpawnPickup(...)` (CinderSim.cs:2670) → 직후 `_campaign && boss` 분기에서 `ClearStage()` (2677) → `_mode=GameOver` (3148). 픽업 갱신은 `if (_mode != SimMode.GameOver)` 게이트 안에 있어 (825는 822 게이트 내부) 그 tick부터 `UpdatePickups`가 다시 실행되지 않는다 → 보스 모트는 영원히 회수·만료되지 않는 유령 드롭.
- 스펙 대조: SIM_SPEC.md:95-96(보스 relic 고정)은 **아레나 확장 절**이고, 캠페인 절(SIM_SPEC_CAMPAIGN.md:30-31)은 보스 킬 보상을 "진행도+장비 1확정"으로만 정의 — 문면 상충은 아니다. 그러나 스폰 자체는 낭비이고, 결과 오버레이 동안 뷰에 모트가 잔존 표시될 수 있으며, 던전 보스 킬이 유물 +1을 영구히 못 주는 것이 의도인지 스펙이 답하지 않는다. → §5 J-3.

### G-2 (Low) `EquipCosts` 테이블 이중 정의
- [OBSERVED] LobbyView.cs:51과 GameDirector.cs:426에 `{2,4,7,11,16}`이 각각 하드코딩. 값은 현재 일치하나 단일 소스가 아니어서 드리프트 위험. 스펙 §6이 계약 수치로 명시한 테이블인데 Sim 레이어 상수(HackSpec/CampaignSpec)에 없다.

### T-1 (Med) EmberShard(+18HP)/OilFlask(+35oil) 회수 효과 무테스트
- [OBSERVED] `EmberShardHeal|OilFlaskCharge` grep 결과 Assets/Tests 매치 0건. 테스트는 relic-mote 효과(CinderSimTests.cs:508-564)와 종류 회전/자력(438-505)/만료(566-601)만 커버. +18/+35 수치는 상수 선언만 있고 행동 검증이 없다 — 회수 분기(CinderSim.cs:2745-2752)가 회귀해도 잡을 테스트가 없음.

### T-2 (Med) EquipShard 회수 시 `kills%3` 슬롯 매핑 무테스트
- [OBSERVED] CampaignSimTests.cs:151-167은 **스폰만** 검증(`sawEquipShard`), 170-186은 보스 드롭 슬롯(`stageIndex%3`)만 검증. 회수 시점 `_kills%3` 슬롯 부여(CinderSim.cs:2756)는 어떤 테스트도 단언하지 않는다. `kills % 3` grep 매치 0건.

### T-3 (Med) 로비 구매 경제 무테스트
- [OBSERVED] `EquipCosts|BuyEquip|구매` grep 결과 Assets/Tests 매치 0건. 비용 사다리, 잔액 부족 거부, tier 5 상한 거부, 유물 차감(GameDirector.cs:428-448) 전부 무커버. 유물 뱅킹(클리어/패배 경로, GameDirector.cs:518-541)도 직접 테스트 없음(GameDirectorCampaignRouteTests는 다이제스트 동등성만 비교, :342-345).

### G-3 (Low) 패배 시 인런 장비 랭크 폐기가 코드 주석으로만 계약화됨
- [OBSERVED] GameDirector.cs:520-521 주석 "equipment keeps the pre-run baseline (spec §3/§6 contract)" — 그러나 SIM_SPEC_HACKSLASH §6(112-118)과 SIM_SPEC_CAMPAIGN §Item drops(35-45) 어디에도 패배 시 장비 처리 조항이 없다. 구현 선택 자체는 합리적이나 스펙에 없는 계약을 인용하는 주석. → §5 J-2.

---

## 3. 외부 레퍼런스 대비 갭 — Acceptance (c)

기준: llm-wiki/raw/sources/2026-08-07-hackslash-design-guide-reference.md:104-128 (일반론 참고자료 — 판정 기준 아님).

| 가이드 개념 (라인) | 현 설계 상태 | 분류 |
|---|---|---|
| 희귀도 등급 테이블 Common/Rare/Epic/Legendary, 가중치 100/20/5/1 (:108-110, 123-128) | 스펙·코드 모두 부재. 대신 결정론적 랭크 시스템 T0-T5(`EquipTiers` HackTypes.cs:97-101 + `RaiseRank` 캡 5)와 고정 픽업 3종+파편이 파밍 축을 대체 | [INFERENCE] **아키텍처상 비양립** — "미구현 갭" 아님. 가중치 롤은 심에 RNG 도입을 요구하는데, RNG 금지·결정론 고정스텝 계약(SIM_SPEC_CAMPAIGN.md:40,92 + CLAUDE.md §1)과 채택 시 결정론(동일 입력→동일 Digest)과 EditMode 결정론 테스트(CampaignSimTests.cs:326-329 등)를 파괴한다. 랭크 누적이 의도적 대체 설계 |
| 소스별 테이블: 정예는 Rare/Epic 확률 상향 (:112-114) | 정예의 **픽업 테이블 차등 없음** — 일반과 동일 id 모듈러(CinderSim.cs:2705-2709는 elite를 구분하지 않음). 정예 차등 보상은 추출 채널(로스터/유물+30)로만 존재 | [OBSERVED] **스펙 자체의 갭** — SIM_SPEC_HACKSLASH §3은 추출만 정의하고 정예 픽업 차등을 요구하지 않으므로 구현 결함이 아니라 스펙이 채택하지 않은 개념 |
| 보스 최소 1개 보장 drop guarantee (:115) | **채택됨**: 아레나 보스 relic 100%(CinderSim.cs:2705-2706), 캠페인 보스 장비 랭크+1 확정(2676) | 개념 일치 |
| bad-luck protection: 특정 슬롯 무성장 시 가중치 상향 (:117-118) | 스펙·코드 모두 부재. 단, 결정론 모듈러가 구조적으로 상한 보장: 파편 간 최대 간격 7 id, 슬롯 배정은 `kills%3` 순환이라 슬롯 기아가 산술적으로 유계 — 확률적 불운 자체가 존재하지 않는 시스템 | [INFERENCE] **아키텍처상 비양립 + 불필요** — 보호 대상인 "불운"이 가중치 롤의 산물인데 이 심에는 롤이 없다(위 행과 동일한 결정론 계약). 결정론 모듈러가 pity timer의 강한 형태를 이미 내장하므로 별도 로직은 개념적으로도 무의미 |
| 동적 드롭 조정: 난이도/웨이브에 따른 재화 증가 (:119) | 부재. 드롭 테이블은 웨이브/난이도 무관 고정(스코어만 `×wave` 스케일, CinderSim.cs:2667) | [OBSERVED] **스펙 자체의 갭** — 유물 수입이 런 길이에 선형이라 후반 스테이지의 시간당 유물 효율이 정체. 채택 여부는 기획 판단(§5 J-4) |
| 위험 루트/조건부 보너스 드롭 (:96-97) | 부재 (단일 아레나 구조라 경로 선택 자체가 없음) | [INFERENCE] 의도적 스코프 컷 — v0.2.0 구조와 무관한 개념 |
| LootTable/DropManager 클래스 구조 (:148-150) | 해당 없음 — 순수 C# 결정론 심 경계(프레이밍 가드)상 수정 제안 대상 아님 | 대조 제외 |

---

## 4. 이월 증거 vs 신규 증거

**이월(레인 리포트·기존 테스트 결과) — 재검증됨:**
- gjc-campaign-lane.md:28-31 (보스 확정 슬롯 stageIndex%3, id%7==3 파편, kills%3 슬롯, 적용식) → 현 코드 CinderSim.cs:2676/2707/2756, CampaignTypes.cs:127-135에서 전부 재확인.
- gjc-sim-lane-report.md:108-110 (id%3 회전·자력 78·relic +1/+250·12s 만료 테스트 존재 주장) → 현 CinderSimTests.cs:438-601에서 실재 확인.
- unity-logs/test-results-*.xml (2026-08-04, 8회 런): `EquipShard_DropsFromEnemyIdModulo`, `Pickup_*` 3종, `Extraction_DuplicateVisualPaysThirtyRelics`, `RelicAltar_BlessesAfterHold_WithCooldown` 전부 Passed — **읽기로만 활용, 이번 감사에서 재실행 안 함**(배치모드 금지 제약).

**신규 증거(이번 감사에서 처음 확인):**
- G-1 보스 모트 회수 불가 tick-순서 분석 (CinderSim.cs:790, 822-825, 2670-2677, 3144-3153).
- 캠페인 드롭 분포의 21-주기 산술(파편이 3종 잔여류를 1개씩 균등 잠식 → 2/7·2/7·2/7·1/7).
- G-2 EquipCosts 이중 정의, T-1~T-3 테스트 공백, G-3 패배 시 장비 처리의 스펙 침묵.
- 프롤로그 유물 폐기 경로 (GameDirector.cs:497-508 — Prologue 분기는 `PrologueDone`만 기록, 뱅킹 없음).
- MomentumTests.cs:291-292의 던전 13킬→relics 3이 재구성 확률표(2/7)와 정합함을 교차 확인.

---

## 5. 사람 판단 필요 항목

- **J-1** 프롤로그 런에서 획득한 relic-mote 유물이 메타에 저장되지 않음 [OBSERVED GameDirector.cs:497-508]. 스펙 §6 "런 종료 시 relics 누적 저장"(SIM_SPEC_HACKSLASH.md:118)의 "런"에 프롤로그가 포함되는지 문면이 침묵. 튜토리얼 제외가 자연스러우나 명문화 권장.
- **J-2** 패배 시 인런 장비 랭크 폐기(클리어 시만 max 병합, GameDirector.cs:543-550 vs 518-529). 스펙에 조항 없음 — 현 구현을 스펙에 명문화할지, 패배에도 보존할지 기획 결정 필요.
- **J-3** 캠페인/던전 보스 relic-mote 유령 스폰(G-1). 옵션: (i) 캠페인 보스는 모트 스폰 생략(`isBoss && !_campaign` 조건), (ii) ClearStage 전 즉시 자동 회수로 유물 +1 부여, (iii) 현상 유지 + 스펙 명문화. 던전 보스 킬의 유물 보상 유무는 경제 수치에 직접 영향(120 유물 만렙 대비 스테이지당 +1).
- **J-4** 시간당 유물 효율이 스테이지 난이도와 무관(레퍼런스의 "동적 조정" 미채택). 로비 만렙 비용 120 유물 대비 런당 수입 ~3-10(킬 수·추출 의존)이라 파밍 루프 길이가 긴 편 — 의도인지 확인 필요.
- **J-5** 테스트 공백 3건(T-1~T-3)의 우선순위. 드롭 경제는 메타 진행의 화폐 축이므로 최소 T-3(구매 경제)과 T-2(회수 슬롯 매핑)는 행동 테스트 추가 가치가 높다 — 단, 이번 감사는 읽기 전용이라 제안만 기록.

## 부록: 아레나 동결 계약 무결성
`_campaign` 게이트(CinderSim.cs:2707) 덕에 기본 생성자 경로의 드롭은 문자 그대로 `id%3` — SIM_SPEC.md 동결 계약(P1-P5) 위반 없음. `PickupKind.EquipShard=3`은 SIM_SPEC_CAMPAIGN.md:81이 명시적으로 허가한 증분(SimTypes.cs:17-18 주석으로 출처 표기됨).
