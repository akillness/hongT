# QA Test Plan — Cycle 2: New Dungeons + Gimmicks (run-id 20260806-dungeon-gimmicks)

2026-08-05 · game-qa · Stage 1 Phase 1a. Companion: `qa/benchmark-notes.md` (calibration
bands), designer novelty survey (`design/trend-survey/`).

## Scope & invariants under test

- Cycle 2 adds NEW dungeons beyond the existing 6 catalog stages (cinder-span,
  ember-gallery, abyss-chancel, witness-well, echo-throne, ash-verdict), each with a
  distinct gimmick. All new mechanics: deterministic (NO RNG), data-driven placement
  (`HazardConfig[]` / `StageEntry.HazardOverride`), WebGL-safe.
- Frozen contract: SIM_SPEC.md + amendments stay intact. Same config + same input
  sequence ⇒ same `RunDigest` (Score, Wave, Kills, Relics, HealthRemaining, Reason).
- View additions (telegraph visuals, reduced-motion gating) must NEVER change sim
  outcomes.
- Test vehicle: EditMode NUnit against `CinderSim` directly (existing convention:
  `CampaignSimTests`, `HackSimTests`, `CinderSimTests`). No PlayMode dependency for
  gate numbers; capture/immersion checks run on the deployed WebGL build.

## A) Archetype rotation set (G3 requirement: ≥5 archetypes)

Each archetype is a pure deterministic bot `SimInput f(ISimSnapshot sim, int tick)` —
polled-state fields only, no RNG, no view access. Reference stat/equip grids below;
"reference ranks" = weapon/lantern/cloak 2/1/3 (existing test convention) and 5/5/5.

| # | Archetype | Concrete input pattern (SimInput fields) | What it stresses |
|---|---|---|---|
| 1 | **melee-rusher** | Every tick: `MoveX/MoveY` = normalized vector toward nearest living enemy (iso metric dy×1.42); `AttackQueued=true`; `DashQueued=true` when iso distance > 150 px (closes gap); never `WardQueued`/`NovaQueued`. | TTK ceiling, contact-damage exposure, whether new gimmicks punish melee uptime disproportionately (G3 viability). |
| 2 | **kiter** | Existing `CampaignSimTests.BotInput` pattern: retreat vector when nearest enemy < 260 px, approach when > 420 px, hold band otherwise; `AttackQueued=NovaQueued=WardQueued=true` every tick (queues no-op while on cooldown). | Baseline clear bot (already proven to clear stage 1 at 5/5/5); the control archetype for digest regressions. |
| 3 | **skill-spammer** | Hold anchor at combat-plane center (768,604): `MoveX/MoveY` only to re-center when pushed > 60 px; every tick `NovaQueued=BoltQueued=PulseQueued=WardQueued=true`; `AttackQueued` only while `Charge` is full. | Cooldown economy vs gimmick cadence (does a stationary caster get farmed by vents? altar synergy), oil-burst interaction (relic-altar +18 oil). |
| 4 | **companion-commander** | Config with `CompanionId="ember-cohort"`. Player runs kiter movement with wider band (retreat < 320 px). Every 300 ticks alternate `CompanionHoldQueued=true` (one tick, at current player position — near new-gimmick anchor when within 200 px) / `CompanionRecallQueued=true`. | Companion pathing vs blockers (pillar-class gimmicks), hold/recall determinism near hazard zones (HackSimTests §companion invariants extended to new gimmicks). |
| 5 | **pacifist-dodger** | Never `AttackQueued`/skills. Orbit the combat plane perimeter clockwise: waypoints (348,434)→(1188,434)→(1188,774)→(348,774), `MoveX/MoveY` toward next waypoint; `DashQueued=true` on the first tick any hazard with `telegraphing==true` covers the player position (iso metric). Run capped at 60×120 ticks; record survival time + hazard hits taken. | Telegraph fairness in isolation: with perfect information, hazard damage taken should be ≈0 when telegraph ≥ band (benchmark-notes band 1). Any unavoidable hit = S1 defect (undodgeable by design). |

Rotation discipline: every gate measurement below runs all 5 archetypes × reference
ranks on every NEW stage, plus kiter on all 6 existing stages (regression control).
Per-archetype results land in `qa/playtest-report.md` (per-archetype table).

## B) Per-gimmick determinism checks (blocking, run per new gimmick kind)

Convention: `AssertSameDigest` (all 6 RunDigest fields) + player X/Y within Tolerance —
same as `HackSimTests.AssertSameDigest`.

| Check | Method | Pass condition |
|---|---|---|
| D1 same-instance repeat | Same `CampaignConfig`/`HackConfig` + same scripted input (kiter, 1800–18000 ticks) run twice via fresh `new CinderSim(in config)` | Digests identical (extends `Regression_CampaignConstructor_StillProducesItsOwnDigest` to each new stage id) |
| D2 cross-archetype determinism | Each of the 5 archetype bots run twice per new stage | Digest identical per bot; digests DIFFER across bots (proves inputs actually reach the sim) |
| D3 phase/config sensitivity | Mutate one data field of the new gimmick (phase +0.6 s, or position +50 px) in a test-local config | Digest CHANGES (proves placement is data-driven and live, not baked); simultaneous-telegraph census over one LCM of all periods reports max concurrent `telegraphing==true` ≤ 3 (benchmark band 5) |
| D4 view-independence | Run sim headless vs through `GameView` tick adapter with `ViewPrefs.ReducedMotion` on and off | All four digests identical (reduced motion & view never touch outcomes) |
| D5 float stability | 1800-tick run digest compared against committed golden values per new stage (kiter @ 2/1/3) | Exact equality (int fields) / exact float equality (HealthRemaining — deterministic fixed-step, no tolerance) |

## C) Gate band checks (what QA must measure, and where the number lands)

Thresholds from `skill://game-studio-harness/references/quality-gates.md`; calibration
bands from `qa/benchmark-notes.md`. Every row = measured value + method + evidence path.

| Gate | Measurement | Band / threshold | Method | Evidence file |
|---|---|---|---|---|
| **G2** | Stage clear rate ("win-rate") per new stage, kiter + melee-rusher @ 2/1/3 and 5/5/5, 60×300-tick cap | 45–55% band interpreted for PvE as: reference-rank clear must succeed at 5/5/5, be contested at 2/1/3 (clears within cap on ≥1 archetype, fails on ≥1) | Scripted EditMode runs, deterministic — one run per cell IS the population | `qa/gate-measurements.md#g2` |
| **G2** | TTK: median wave-clear ticks per wave per new stage vs balance-sheet target | within ±15% of `design/balance-sheet.md` target | Tick census between `WaveStarted` events | `qa/gate-measurements.md#g2` |
| **G2** | Hazard damage share: (HP lost to new gimmick) / (total HP lost), kiter run | 10–35% (below 10% the gimmick is decorative, above 35% it dominates enemies) | Diff `Player.Health` on `HazardPulse`-family event ticks | `qa/gate-measurements.md#g2` |
| **G2** | Single-hit ceiling | No gimmick hit > 30% max HP (benchmark band 2) | Static audit of new `HazardConfig` damage vs 100+vit×8+cloak floor | `qa/gate-measurements.md#g2` |
| **G3** | Per-archetype viability: clear/survival per new stage, all 5 archetypes | ≥3 archetypes independently viable (clear at 5/5/5 with distinct strategies); no archetype's optimal clear dominates (fastest TTK ≤ 1.5× slowest viable); ≥5 tested | Full rotation matrix (§A) | `qa/playtest-report.md` (per-archetype table) + `qa/gate-measurements.md#g3` |
| **G3** | Pacifist telegraph fairness | 0 unavoidable hazard hits with perfect-information dodger | Archetype 5 run per new gimmick | `qa/gate-measurements.md#g3`, defects → `qa/defect-register.md` |
| **G7** | Loop period: cadence of gimmick-interaction loop (e.g. altar-class hold → buff → fight → return) | period 30–180 s, ≥3 actions/loop, ≥1 reward event/loop | Event-trace segmentation of kiter + skill-spammer runs (AltarBlessing/HazardPulse/EquipDropped timestamps) | `qa/gate-measurements.md#g7` (numeric model mirror: `design/core-loop.md`) |
| **G7** | Repeat-rate proxy ≥70% | testers voluntarily re-enter the loop (quick-retry usage / re-entry within session) | Structured playtest on deployed WebGL build, ≥5 sessions | `qa/playtest-report.md` + `qa/gate-measurements.md#g7` |
| **G8** | Impression score of each new gimmick | median ≥ 4/5 | Structured playtest scoring (same rubric as G4 immersion scoring), ≥5 scored sessions per gimmick | `qa/gate-measurements.md#g8` |
| **G8** | Novelty frequency cross-check | gimmick appears in ≤2 of ≥5 surveyed comparable titles | Designer frequency table (`design/trend-survey/solutions.md` §Frequency Ranking, 11-title pool ⊇ QA's 6); QA verifies the ≥5-title denominator and provenance labels before sign-off | `design/novelty-scorecard.md` + `qa/gate-measurements.md#g8` |

## D) Regression list (must-not-change; any diff = S1, blocks all gates)

| # | Surface | Check | Baseline source |
|---|---|---|---|
| R1 | Existing 6 catalog stages | Kiter golden digest per stage id (cinder-span, ember-gallery, abyss-chancel, witness-well, echo-throne, ash-verdict) @ 2/1/3, 1800 ticks — digests byte-identical to pre-cycle-2 goldens | Golden values recorded from HEAD before first cycle-2 sim change; committed in test source |
| R2 | Arena mode | `GameMode.Arena` frozen-run digest unchanged (existing `HackSimTests` arena regression, 1800 ticks) | Existing test, must stay green untouched |
| R3 | Prologue | Prologue pilot still clears wave 3 with `Digest.Reason == "prologue-clear"` | Existing `HackSimTests` prologue test |
| R4 | Existing gimmick constants | ember-vent {r90, 2.4 s, 0.8 s, dmg 8} · pillar {r40} · altar {r70, 1.2 s, +18, 6 s} and the 6 existing placement tables unchanged | SIM_SPEC_CAMPAIGN §Dungeon gimmicks + StageCatalog hazard tables |
| R5 | EditMode suite | Full suite green (cycle-1 close: 76/76; count only grows) | Unity EditMode run, results XML archived |
| R6 | Companion invariants | Hold/recall near new gimmicks: command sequences produce identical snapshots+digests; commands inert where spec says inert | Existing HackSimTests companion suite extended, not modified |
| R7 | Reduced-motion invariance | ReducedMotion on/off digest equality (D4) also run on one EXISTING stage (abyss-chancel) | New test, guards old content |
| R8 | Campaign progress key | localStorage key `abyssal-lantern:unity:campaign` schema unchanged (`cleared[]`, `equipment{}`) — new stage ids append to `cleared`, never rename | SIM_SPEC_CAMPAIGN §Page flow; renaming orphans every player's save |

## E) Evidence routing (single source: qa/gate-measurements.md)

- `qa/gate-measurements.md#g2` — clear-rate matrix, TTK table, hazard-damage share, single-hit audit (+ commands/tick counts per row)
- `qa/gate-measurements.md#g3` — viability verdict per archetype, dominance ratio, pacifist fairness
- `qa/gate-measurements.md#g7` — loop period/actions/reward measurements; repeat-rate proxy
- `qa/gate-measurements.md#g8` — impression scores; frequency-table verification note
- `qa/playtest-report.md` — per-archetype session table (G3/G7 raw)
- `qa/defect-register.md` — S1–S4 lifecycle; unavoidable-hit and digest-diff findings
- `qa/regression-matrix.md` — R1–R8 status per build
- `qa/exploit-register.md` — any gimmick-abuse loop found during rotation (broadcast to all agents with feedback request, per harness discipline)
- Unity EditMode results XML — archived beside the run (`test-results-*.xml`, same convention as cycle 1) and referenced by path from each measurement row
- Director verdicts consume ONLY these paths (`production/gate-reviews/{stage}-{gate}.md`)

## Open dependencies

- New stage ids + gimmick specs: blocked on designer Stage 1 output (`design/` lane); this
  plan's checks are parameterized by stage id and gimmick kind, so they bind the day specs land.
- `design/balance-sheet.md` TTK targets must exist before G2 TTK rows can be measured
  (sheet audit is itself a G2 requirement).
- Golden digests (R1) must be recorded BEFORE the first cycle-2 sim-touching commit —
  flag to programmer lane at Stage 1 close.

---

# QA Test Plan — 훈련·강화·돌발 (run-id 20260806-training-and-upgrade)

2026-08-06 · game-qa · Stage 1a. 동반 아티팩트: `qa/benchmark-notes.md` §훈련·돌발 벤치마크
(밴드 S1-S6 + 충돌 판정 C1-C4), designer 서베이 `.survey/roguelike-training-and-surge/`.

cycle-2 플랜(위)은 그대로 유효하다 — 이 섹션은 **추가분**이며 A)-E)의 아키타입/디지스트
관례를 상속한다. 신규 검증 대상은 세 가지: **훈련 모드가 본편을 안 움직인다** ·
**돌발이 결정론적이다** · **강화 신축이 아키타입 균형을 깨지 않는다**.

## 범위와 불변식 (이번 사이클 추가분)

- 훈련(프롤로그)에 기믹/보상/진행도가 붙는다면 **전부 옵트인 경로**여야 한다. 기본 경로는
  현재와 바이트 동일해야 한다(골든 15행 §T1).
- 돌발은 **난수가 아니다**. 허용 트리거는 benchmark-notes S5: 고정 시간표 · 웨이브 경계 ·
  처치 수 · 플레이어 옵트인. 체력 % 임계는 경계 조건 명시 시에만, 불가시 게이지는 금지.
- 현재 격리 4겹(재검증 대상): `Hazards.Count == 0` · 스킬/대시 입력 무시 · 각인 무효 ·
  메타스탯/장비 무효 [direct spec: `HackSimTests` §1, `SigilTests`].
- 시험 도구는 cycle-2와 동일: EditMode NUnit + `CinderSim` 직접 구동. 게이트 수치에
  PlayMode 의존 없음.

## T1) 훈련 모드가 본편 골든 15행을 움직이지 않음을 증명

골든 15행은 `qa/golden-rows-unity.md`에 있고 `DungeonGoldenDigestTests`가 집행한다.
**증명 전략: 훈련 경로 변경이 골든을 건드릴 수 있는 통로가 4개뿐이므로, 통로별로 하나씩 막는다.**

| # | 통로 | 검사 | 통과 조건 | 측정 방법 (명령/스크립트/세션) |
|---|---|---|---|---|
| T1.1 | 공유 상수 오염 (`SimConfig`/`HackSpec` 값을 훈련용으로 수정) | 골든 15행 전량 | 15행 **바이트 동일** | `Unity -runTests -testPlatform EditMode -testFilter "DungeonGoldenDigestTests"` → 결과 XML 보관. 행 텍스트를 `qa/golden-rows-unity.md`와 `diff` |
| T1.2 | 프롤로그 골든 자체 이동 | `Golden_Prologue_IsUnchanged` | `prologue\|1650\|2\|9\|1\|36\|(running)\|930.1258\|435.3988` 정확 일치 | 위와 동일 필터. **이 행이 움직이면 훈련 심 자체가 변한 것** — 옵트인 설계 실패의 1차 신호 |
| T1.3 | 모드 누출 (훈련 플래그가 던전/아레나 분기에 샘) | 신규: 훈련 신기능을 **켠 config**와 **끈 config**로 던전 6+3스테이지 구동 | 두 디지스트 집합이 서로 동일 **AND** 골든과 동일 | `SigilTests`의 무효 증명 패턴 재사용(아레나/프롤로그 각인 무효 테스트와 동형): 같은 입력열로 두 심을 병렬 tick, `Digest.HealthRemaining` 등 6필드 비교 |
| T1.4 | 영속화 스키마 파괴 | `localStorage` 키 `abyssal-lantern:unity:campaign` | 기존 필드 **미개명·미삭제**, 신규 필드는 append-only(파서의 "없는 키 = 0" 규칙 유지) | `StageCatalogTests` 왕복 테스트 확장(`CampaignStore.Save`/`Load` 후 필드별 비교). v0.1 레거시 블롭이 여전히 로드되는지 포함 |

**T1의 핵심 계약**: 훈련에 축을 열 때마다 **축당 회귀 1개**를 추가한다(benchmark-notes S6).
축을 안 열면(Warframe 기준선 유지: 보상 0·진행도 0·기록 분리) 추가 비용은 0이다.
열 축과 그 비용:

| 열 축 | 추가로 필요한 증명 | 이유 |
|---|---|---|
| 훈련에 기믹 등장 | 프롤로그 골든 **재핀 + AMENDMENT 문서** | `Hazards.Count == 0`이 깨진다 → T1.2 필연 적색 |
| 훈련에 유물/보상 | 경제 회귀: 훈련 반복 N회 후 유물 총액 검사 | 반복 파밍 경로가 생기면 §T5 밴드가 무의미해진다 |
| 훈련에 진행도 | `prologueDone` 외 신규 필드 왕복 + 해금 규칙 회귀 | `StageCatalog.IsUnlocked`가 `PrologueDone`을 게이트로 쓴다 |
| 훈련에 스킬/대시 허용 | 입력 무시 테스트 2건 갱신 + 프롤로그 골든 재핀 | `Prologue_IgnoresSkillAndDashInput`이 현재 이를 금지한다 |

## T2) 돌발 이벤트 결정론 고정 (같은 입력 → 같은 발동 틱)

cycle-2 §B의 D1-D5를 상속하되, 돌발은 **발동 틱 자체가 검증 대상**이라 항목을 추가한다.
관례: `AssertSameDigest`(6필드) + 플레이어 X/Y.

| # | 검사 | 통과 조건 | 측정 방법 |
|---|---|---|---|
| T2.1 | 발동 틱 재현성 | 같은 config + 같은 입력열 2회 → **발동 틱 번호 리스트가 정확히 동일** | 심 구동 중 매 틱 돌발 상태를 폴링해 전이 틱을 리스트로 수집, 두 런의 리스트를 `CollectionAssert.AreEqual`. 디지스트 동일만으로는 불충분 — 발동이 상쇄돼도 디지스트가 같을 수 있다 |
| T2.2 | 입력 독립성 (시간표 트리거인 경우) | 5아키타입 전원에서 **발동 틱 리스트 동일** | 아키타입별 런의 전이 틱 리스트를 상호 비교. 다르면 트리거가 시간표가 아니라 플레이어 상태에 의존 → 스펙과 불일치 |
| T2.3 | 조건 트리거 판정 (처치 수/웨이브 경계인 경우) | 발동 틱이 **조건 충족 틱과 같은 틱 또는 다음 틱** | `WaveStarted`/처치 이벤트 틱과 돌발 전이 틱의 차이를 세어 0 또는 1인지 검사 |
| T2.4 | 체력 임계 건너뛰기 (임계 트리거를 쓸 경우에만) | 임계를 **뛰어넘은 틱**에서도 발동이 정확히 1회 | 고피해 스크립트 입력으로 임계를 한 틱에 통과시킨 뒤 발동 횟수 카운트. benchmark-notes C-Elden Ring 행이 요구하는 검사 — 스킵도 중복도 결함 |
| T2.5 | 뷰 독립성 | 헤드리스 vs `GameView` 어댑터 × `ReducedMotion` on/off = **4 디지스트 동일 + 발동 틱 리스트 동일** | cycle-2 D4 확장. 돌발 연출이 심에 새는지 검사 |
| T2.6 | 데이터 주도 확인 | 돌발 데이터 1필드 변경(위상 +0.6s 등) → 디지스트 **변화** | cycle-2 D3과 동형. 안 변하면 값이 베이크된 것 |
| T2.7 | 골든 재확인 | 돌발 **비활성** 기본 경로에서 골든 15행 불변 | T1.1과 같은 명령. 돌발이 옵트인이라는 주장의 유일한 증거 |

## T3) 밴드 집행 — 동시 예고 센서스 (재현 절차 포함)

benchmark-notes C3의 여유가 **정확히 1**이므로 이 검사는 이번 사이클 최우선 차단기다.
cycle-2에 이미 `CampaignSimTests.Telegraph_PactCensusUnderBudget`가 존재하므로 **확장**한다.

| # | 검사 | 통과 조건 | 측정 방법 |
|---|---|---|---|
| T3.1 | 스테이지별 동시 예고 피크 | **≤3**, 동종 **≤2** | 모든 주기의 LCM 구간(현 상수로 **276초 = 16560틱**)을 틱 단위 순회하며 `Hazards` 중 `Telegraphing==true` 개수를 세고 최대값 기록. 예고 기믹은 3종뿐(분출구/해류/벽 — `CinderSim.cs` 3108-3127) |
| T3.2 | 돌발 활성 중 재센서스 | 동일 밴드 | 돌발을 켠 상태로 T3.1 반복. **ash-march / cinder-sluice는 기저 피크가 이미 2**라 여유 1 — 여기서 3 초과가 나오면 S4 위반 |
| T3.3 | 예고 점유율 회귀 | 기저값 대비 **증가 없음** (ash-march 71.0% · cinder-sluice 75.1% · span/throne 66.6% · chancel/bastion 33.3%) | 같은 순회에서 "최소 1종이 예고 중"인 틱 비율. 대비 실패(밴드 6) 조기 경보 |

기저값 산출 근거(QA 계산, 재현 가능): 위상표 = ash-march {wall 0.0, wall 11.5, vent 0.6,
vent 1.8} · cinder-sluice {current 0.0, current 3.0, vent 0.9, vent 2.1}. 판정식 =
vent: `fmod(t+φ, 2.4) ≥ 1.6` · current: `fmod(t+φ, 6.0) < 0.8` · wall:
`4.5 ≤ fmod(t+φ, 23.0) < 6.0` [direct spec: `CampaignSpec` 상수].

## T4) 노출 에피소드 누적 상한 (C1이 요구한 신규 검사)

per-hit ≤30% 감사는 **DoT를 못 잡는다**. 벽은 16.67 dmg/s이므로 1.80초 노출로 기본HP의
30%에 도달한다 [direct spec + 계산]. 돌발이 `WallHold`/`WallTickDamage`/밀기 겹침을
건드리면 per-hit을 한 번도 안 깨고 실효 상한을 넘는다.

| # | 검사 | 통과 조건 | 측정 방법 |
|---|---|---|---|
| T4.1 | 단일 히트 상한 (기존) | 어떤 기믹 히트도 **≤30% max HP** | 신규/변경 `HazardConfig` 피해값을 `100 + vit×8 + cloak×8` 하한(=100)에 대해 정적 감사 |
| T4.2 | **노출 에피소드 누적** (신규) | 연속 피해 구간 1회당 누적 **≤30% max HP** | 심 구동 중 매 틱 `Player.Health` 델타를 기록. 피해 틱이 **연속(간격 ≤ 최대 틱 주기 0.6s)** 인 구간을 하나의 에피소드로 묶고 구간 합의 최댓값을 보고 |
| T4.3 | 최악 케이스 봇 | pacifist-dodger가 **회피 불가로 30% 초과** 노출을 겪지 않음 | 아키타입 5(§A) 런에서 T4.2 계측. 완전정보 회피자가 못 피하면 S1 결함 |
| T4.4 | 저항 하한 (선행 서베이 규칙 1) | 강화 적용 후에도 기믹이 **행동을 바꾼다** | 해류 밀기 200 vs 이속 218 같은 관계를 강화 적용 후 재계산: 밀기 저항이 "서 있어도 됨"이 되면 면역 = 금지. 밀기 < 이속이면 통과이나 **역류 보행 속도 > 0**만으로는 불충분 — 이탈 요구가 남아야 함 |

## T5) 강화 경제 — 아키타입 5종으로 측정할 것 + PM 의뢰 접수분

아키타입 정의는 cycle-2 §A를 그대로 쓴다(melee-rusher / kiter / skill-spammer /
companion-commander / pacifist-dodger). **이번 사이클에 아키타입으로 측정할 대상은 4개다.**

| # | 측정 대상 | 아키타입별로 무엇을 보는가 | 통과/보고 조건 | 측정 방법 |
|---|---|---|---|---|
| T5.1 | 돌발 생존성 | 각 아키타입이 돌발을 **넘길 수 있는가** | ≥3 아키타입이 독립 생존(cycle-2 G3 관례). 특정 아키타입만 전멸하면 돌발이 스타일을 처벌 | 5아키타입 × 신규 경로 × 참조 랭크(2/1/3, 5/5/5), 60×300틱 캡. 생존 틱 + 돌발 중 HP 델타 기록 |
| T5.2 | 돌발 피해 점유율 | 돌발이 **장식인가 지배인가** | 10-35% (cycle-2 G2 밴드 계승) | (돌발에 잃은 HP)/(총 잃은 HP). `HazardPulse` 계열 이벤트 틱에서 `Player.Health` 차분 |
| T5.3 | 강화 신축의 아키타입 편향 | 신규 강화가 **한 아키타입에만 유리한가** | 최속 TTK ≤ 1.5× 최저속 유효 아키타입(cycle-2 G3 관례 유지) | 신규 강화 on/off × 5아키타입 TTK 행렬. 비율이 1.5 초과로 벌어지면 사이드그레이드 아님(선행 서베이 규칙 3 위반) |
| T5.4 | 대응 창 실측 | 예고를 보고 **실제로 대응 가능한가** | pacifist-dodger의 회피 불가 피격 **0** | 아키타입 5 전용. 이벤트 레벨 예고는 S1 밴드 ≥2.0초(120틱) 대비 실측 |

### T5.5 PM 의뢰 접수분 — 유물 수입 실측 (PMRevenueMap, 2026-08-06 IRC)

PM의 T5 도달 10-20세션 밴드를 계약값으로 만들기 위한 측정. **구매측은 실측 없이 이미
확정되므로 측정 대상이 아니다** — 아래는 수입측만이다.

구매측 확정값 [direct spec: `GameDirector.cs:412`, `LobbyView.cs:54`, `FirstClearRelicBonus` 598-607]:
슬롯당 `{2,4,7,11,16}` = 40, 3슬롯 **총 120유물**, 랭크 스텝 15개. 첫클리어 보너스
`sluice 6 / bastion 8 / march 10` = **+24 일회성**(기초 6스테이지는 0) → **순 구매 필요액 96유물**.

| # | 측정 항목 | 왜 필요한가 | 측정 방법 |
|---|---|---|---|
| T5.5a | **스테이지별 런당 유물 수입 (기초 6종 포함, 풀런)** | 현재 실측은 신규 3스테이지의 만개 런(18/17/18, r=17.7)뿐. 기초 6스테이지는 **0점**이다. cinder-span의 7은 웨이브6 전사 **부분런**이라 풀런 수입이 아니다 [playtest-report:12] | 9스테이지 × 참조 랭크(0/0/0 · 2/1/3 · 5/5/5) 봇 풀런(클리어 또는 사망까지, 캡 60×300틱). `Digest.Relics` 기록. 랭크별 3열이 필요한 이유는 T5.5c |
| T5.5b | **T5 도달까지 실제 구매 랭크 수 + 대체 티어 분포** | 인런 드롭이 줄여주는 양은 **개수로 환산 불가**. 대체 스텝 위치에 따라 2~16유물, **8배 스프레드**(T0→T1 대체=2 절약, T4→T5=16). 개수만 보고하면 밴드가 안 닫힌다 | 런 종료 시 `ICampaignSnapshot`의 랭크와 진입 시 랭크를 비교해 인런 상승 스텝을 **티어 위치까지** 기록. 보고 형식: `{슬롯, from→to, 절약 유물}` 리스트 |
| T5.5c | **수입-랭크 결합도** | 밴드가 상수 분모로 안 닫히는 진짜 이유. 실측 2점이 밴드를 **양쪽에서 협살**한다: r=17.7 → 96/17.7 = **5.4런(하한 10 아래)**, r=7.0 → **13.7런(밴드 내)** | T5.5a의 랭크별 3열로 수입 곡선을 만든다. 부트스트랩 모델(수입이 mean rank에 비례) 결과: 인런 드롭 0/런 → **10런**, 0.25/런 → 8런, 0.5/런 → 8런, 1.0/런 → 6런 |

**QA 판정 1 (현 근거로 확정 가능)**: 밴드가 상수 분모로 닫히지 않는다. 실측 2점이 밴드를
양쪽에서 협살하므로 수입은 랭크에 비례하는 **부트스트랩 곡선**으로 모델링해야 한다.
PM이 지목한 두 반대 효과 중 **인런 드롭이 이긴다** — 기초 6스테이지의 낮은 수입은
부트스트랩 초기 구간으로 곡선에 흡수된다.

**QA 판정 2 (각인 포함 시 — PM 2026-08-06 2차 IRC 반영)**: 각인가는
`LobbyView.SigilCost = 12` [direct spec: `LobbyView.cs:60`, `GameDirector.cs:453`]이고
5종 = 60유물이다. 총 필요액 = 96 + 60 = **156유물**.

| 모델 | c=12 결과 | 하한 10 대비 |
|---|---|---|
| 상수-r (r=17.7) | 156/17.7 = **8.81런** | 깬다 (헤드룸 **−1.19런** = −21유물) |
| 부트스트랩, 드롭 0/런 | **12.87런** | 지킨다 |
| 부트스트랩, 드롭 0.25/런 | **11.33런** | 지킨다 |
| 부트스트랩, 드롭 0.50/런 | **10.28런** | 지킨다 (여유 0.28) |
| 부트스트랩, 드롭 0.75/런 | **9.48런** | 깬다 |
| 부트스트랩, 드롭 1.00/런 | **9.22런** | 깬다 |

상수-r에서 하한을 지키는 최소 정수 각인가는 c=12가 아니라 **c=17**(→10.23런)이다.

**→ 임계값(entry 6 서명을 푸는 단일 숫자):**

> **인런 드롭율 > 0.714 스텝/런이면 c=12는 하한 10을 깬다.**
> (= 60런당 42.9스텝 = 1.40런마다 1드롭. 이분법으로 부트스트랩 연속 모델의 교차점 산출)

서명 조건이 "드롭율을 실측하라"는 열린 요구가 아니라 **"드롭율이 0.714를 넘는가"라는
이진 판정**으로 좁혀진다. §T5.5b가 이 값을 측정한다.

**보고 규약 2건**:
- **연속(분수 런) 모델을 계약값으로 쓴다.** 이산(정수 런) 모델은 마지막 런의 잉여 수입을
  버려 낙관 편향이 있다(드롭 1.0에서 이산 10런 vs 연속 9.22런 — 이산은 하한에 걸치고
  연속은 깬다). 차이는 반올림이며 이견이 아니다.
- **헤드룸 정의를 고정하고 쓴다.** PM 2차 IRC의 −2.07은 r=19.67을, 이전 +0.33은 r=17.42를
  함의하며 **둘 다 r=17.7이 아니다**(같은 총액에 서로 다른 분모). QA 재계산값은
  **−1.19런 / −21유물**이다. 정의 미고정 상태로 협상 문서에 들어가면 부호가 뒤집힌 채
  계약된다 — 이건 결함은 아니되 계약 오류 위험이다.

→ **권고: 세션 수로 계약하지 말고 순 구매액 96유물(확정) + 인런 드롭율 상한 0.714로 계약.**
세션 수는 드롭율의 종속변수이므로 계약 대상이 아니다. reward-bands.md 개정은 director
중재 안건(PM이 Stage 1c에 상정).

## T6) 회귀 목록 (추가분 — 임의 변화 시 S1, 전 게이트 차단)

cycle-2 R1-R8을 상속하고 다음을 추가한다.

| # | 표면 | 검사 | 기준선 |
|---|---|---|---|
| R9 | 골든 15행 | 훈련/돌발 **비활성** 기본 경로에서 전량 바이트 동일 | `qa/golden-rows-unity.md` (15행, 현 HEAD) |
| R10 | 프롤로그 격리 4겹 | `Hazards.Count == 0` · 스킬/대시 무시 · 각인 무효 · 메타/장비 무효 — **네 테스트 모두 무수정 통과** | `HackSimTests` §1 3건 + `SigilTests` 각인 무효 1건 |
| R11 | 기존 기믹 상수 | 분출구 {r90, 2.4s, 0.8s, dmg8} · 해류 {6s, 0.8s, 3.2s, push200} · 벽 {23s, rest4.5, tel1.5, tick10/0.6s} · 제단 {r70, 1.2s, +18, 6s} · 기둥 {r40} · 방벽주 {HP300, r280, ×0.40} 불변 | `CampaignSpec` [direct spec] |
| R12 | 동시 예고 기저 피크 | ash-march 2 · cinder-sluice 2 · span/throne 1 · chancel/bastion 1 (동종 전부 1) | T3.1 센서스, 현 HEAD |
| R13 | 경제 상수 | `EquipCosts {2,4,7,11,16}` · 첫클리어 `6/8/10` · `PactRelicMultiplier 2` 불변 | `GameDirector.cs` / `LobbyView.cs` [direct spec] |
| R14 | 해류/벽 대칭성 | 적에게도 적용되는 성질 유지(기믹이 플레이어의 도구라는 정체성) | `SIM_SPEC` Amendment #5 + 기존 테스트 |

## T7) 증거 라우팅 (추가분)

- `qa/gate-measurements.md#g2` — T4 단일히트/노출 에피소드 감사, T5.2 돌발 피해 점유율
- `qa/gate-measurements.md#g3` — T5.1 아키타입 생존, T5.3 편향 비율, T5.4 대응 창
- `qa/gate-measurements.md#g7` — 돌발이 루프에 기여하는 주기/보상 이벤트
- `qa/gate-measurements.md#g8` — 돌발 인상 점수 + designer 빈도표 분모 검증
- `qa/regression-matrix.md` — R9-R14 빌드별 상태
- `qa/golden-rows-unity.md` — R9 기준선(재핀은 AMENDMENT 동반 시에만)
- `pm/revenue-map.md` — T5.5 결과가 교체할 대상(현재 r=17.7 [INFERENCE])
- Unity EditMode 결과 XML — 실행마다 보관, 각 측정 행이 경로로 참조

## 미해결 의존성

- T1의 "열 축" 표는 designer/PM이 훈련에 무엇을 붙일지 결정해야 확정된다. 축을 안 열면
  추가 증명 비용 0 — 이 선택지가 가장 저렴하다는 사실을 Stage 1 협상에 올린다.
- T2의 트리거 종류(시간표/웨이브 경계/처치 수/옵트인)가 정해지면 T2.2-T2.4 중 해당 행만
  활성된다. 체력 임계를 고르면 T2.4가 **필수**가 된다.
- T3.2는 돌발이 예고 기믹을 추가하지 않는다는 전제에서 값싸다. 추가하는 설계라면
  AMENDMENT + 스테이지별 재센서스가 선행이며 ash-march/cinder-sluice는 제외 대상이다.
- T5.5는 EditMode 봇 풀런 하니스가 필요하다(현재 골든 하니스는 1800틱 절단런). Stage 1d 이후.
- benchmark-notes S3(노출 에피소드 상한)은 **밴드 문구 확장 제안**이다 — Stage 2에서
  designer/PM 서명이 없으면 T4.2는 보고 항목이고 차단기가 아니다.

## v1.7 — 진행 네비게이션 시험 계획 (run-id 20260807-progression-navigation)

2026-08-07 · game-qa. Stage 1a 산출물. **계획만이고 테스트 코드는 아직 안 쓴다.**

**용어**: 이 절은 "다음에 할 것을 화면이 한 곳 집어주는 것"을 **지목**이라 부른다.
최종 명칭은 designer 레인 소관이므로 잠정어다. 여기서 고정하는 건 이름이 아니라
**판정 수치**다.

### 왜 3층으로 쪼개는가

네비게이션은 **뷰 레인**이다. cycle-3 회고 §4b가 이 레인의 구조적 사각을 이미 기록했다:
EditMode 319/319 초록·골든 무이동·빌드 errors 0인 상태에서 게임을 열었더니 브라우저가
7건을 더 잡았고, **7건 전부 뷰 레인이며 전부 사람이 화면을 봐야만 보이는 것**이었다.
그 7건의 유형이 그대로 이 기능의 위험 목록이다 — 안 그려짐, 부모가 캔버스 밖, 라벨
겹침, 문자열 조합 오류, 심은 옳고 뷰만 틀림, 20px 통로. 그래서 "EditMode로 잡히는
것"과 "안 잡히는 것"을 **처음부터 나눠서** 계획하고, 후자는 브라우저 스모크를 게이트
항목으로 올린다(회고 §4b 교훈의 집행).

---

### T-A) EditMode로 잡히는 것 — 순수 함수·rect·글리프

지목/사유/배지는 전부 `CampaignData` 하나에서 나오는 **순수 함수**로 설계 가능하다.
순수 함수인 한 EditMode가 전수로 돌릴 수 있다. 이 층의 판정은 전부 정수 비교다.

**구현이 착지했다 — 이 층은 이제 대상이 실재한다 [2026-08-07 12:04].**
`Assets/Scripts/View/ProgressionGuide.cs`가 들어왔고 표면이 전부 `in CampaignData`
읽기 전용 순수 함수다. T-A는 가정이 아니라 **이 API에 직접 붙는다**:

| 판정 | 대상 심볼 |
|---|---|
| T-A1 | `NextTarget(in CampaignData) → GuideTarget{Kind,Index}`, `GroupOfTarget` |
| T-A2 | `LockReasonFor(in,in) → LockReason{None,PrologueIncomplete,PrerequisiteUncleared}`, `PrerequisiteTitle`, `StageSubLine(in,in,string)` |
| T-A3 | `Badges(in) → SanctumBadges{Growth,Equip,Legion,Sigil}` |
| T-A4 | `Badges` + `SigilLiveInTarget(in,SigilKind,in GuideTarget)` |
| T-A5 | `NextTarget` 폴백 + `MasteredTrials` |
| 보조 | `ClearedTotal`, `ClearedInAct`, `ActOf` |

`LockReason`이 **enum 3값**으로 나온 게 T-A2에 유리하다 — 문자열을 파싱해 사유를
역분류할 필요 없이 enum을 직접 비교하면 된다. 내 초판은 문자열 분류를 가정했는데,
구현이 더 검사하기 좋은 형태로 왔다. `LockReasonFor`는 `IsUnlocked`를 **재유도하지 않고
분기만 명명**하므로(`:176-181`) 심과 뷰가 갈라질 구조적 여지도 없다.

**T-A1) 지목 슬롯은 정확히 1개**
`PrologueDone`(2) × `ClearedMask`(2⁹=512) = **1024 상태 전수**를 돌린다.
- 판정: 모든 1024 상태에서 지목 슬롯 수 == 1. **0개 또는 2개 이상이면 FAIL.**
- 세부 기대값:
  - `PrologueDone == false` (512 상태): 지목 == 프롤로그, 1건. 그 외 지목 0건.
  - `PrologueDone == true`, 미클리어 해금 스테이지 존재: 지목 == **최소 인덱스**의
    미클리어 해금 스테이지 1건. (동률 없음 — 인덱스가 전순서라 유일)
  - `PrologueDone == true`, 9스테이지 전부 클리어 + 숙달 미완: 지목은 시련으로 폴백, 1건.
  - **전부 완료 시 `GuideTarget.Nothing`** (`:156`). 이 상태만 슬롯 0이 정상이다 —
    T-A1의 "정확히 1개"는 **`Nothing`을 제외한 전 상태**에 대한 판정으로 읽는다.
    `Nothing`이 아닌데 0이면 FAIL, `Nothing`인데 뭔가 가리키면 FAIL.
    이 폴백이 `mastery_surface_rows_visible`의 집행처다. **전제 2개 적용**(T-A9):
    `PrologueDone == true`인 세이브에서만 판정하고, 행은 `interactable == true`인
    것만 센다. `PrologueDone == false`의 alpha 0.45 잠김 시련 행을 "보이는 행"으로
    세면 FAIL — 밴드 우회다.
- 근거: `StageCatalog.IsUnlocked`(`StageCatalog.cs:526`)는 `PrologueDone`과
  `PrereqId` 클리어 여부만 본다. 손상 세이브는 도달 불가 마스크도 만들 수 있으므로
  **도달 가능 상태만 돌리면 안 되고 1024 전수여야 한다.**

**T-A2) 잠금 사유 문자열은 사유당 1종, 빈 문자열 금지**
`IsUnlocked == false`의 원인은 코드상 **정확히 2가지**뿐이다 — (i) `!PrologueDone`,
(ii) `PrereqId` 스테이지 미클리어.
- 판정: 잠긴 스테이지마다 사유 문자열 길이 > 0. **0이면 FAIL.**
- 판정: 사유 문자열이 위 2사유 중 **정확히 하나**에 대응. 분류 불가 문자열 1건이면 FAIL.
- 판정: (ii)인 경우 사유 문자열이 **그 선행 스테이지의 표시명을 포함**. 미포함이면 FAIL.
- 판정: 서로 다른 선행 조건에서 나온 사유 문자열이 **서로 달라야** 한다. 9스테이지 중
  잠긴 것들의 사유 문자열 집합 크기 == 서로 다른 사유의 개수. 작으면 FAIL(사유가 뭉개짐).

**T-A3) SANCTUM 배지 조건은 탭별 구매 가능성과 100% 일치**
각 탭의 "지금 살 수 있음"은 이미 코드에 있다 [OBSERVED]:

| 탭 | 조건 | 출처 |
|---|---|---|
| 성장 | `Points > 0 && 어느 스탯 < 10` | `LobbyView.cs:282` |
| 장비 | `어느 i: tier<5 && Relics >= EquipCosts[tier]` | `:301`, 비용 {2,4,7,11,16} |
| 각인 | `Relics >= 12 && 미보유 각인 존재` | `:342`, `SigilCost=12` |
| 군단 | `보유 동료 중 비활성 존재` (무료) | `:312-313` |

- 판정: 4탭 각각 경계 3점(`비용-1`, `비용`, `비용+1`)에서 배지 상태 == 위 조건.
  장비는 5티어 × 3점 = 15케이스, 각인 3, 성장 3, 군단 3 → **총 24케이스 전부 일치.**
  1건이라도 어긋나면 FAIL.
- 판정: 배지가 켜졌는데 그 탭에서 `interactable == true`인 버튼이 0개면 FAIL(거짓 지목).

**T-A4) 배지 오도 — 통화별로 분리해서 계산 (PM `badge_misdirect_relics` 대응)**
로비 통화는 **2종 + 무료 1종**이다: 성장=Points, 장비/각인=Relics, 군단=무료.
섞으면 지표가 무의미해지므로 **Relics 표시 구매(장비 5티어 + 각인)로만 스코프**한다.
IRC로 PMNavRevenueMap에 회신 완료(2026-08-07).
- 계산: `|지목 비용 − 그 시점 Relics 구매 가능 최저비용|`.
- 판정: `Relics` 0–20 × 장비 티어 조합 전수에서 값이 **정의됨**(NaN·음수 0건). 음수 1건
  이면 FAIL — 지목이 최저보다 싸다는 건 모순이다.
- 판정: Relics 구매 가능 항목이 0개인 상태에서 이 지표는 **계산하지 않는다**(정의역 밖).
  그 상태에서 값을 뱉으면 FAIL.
- 밴드 자체(차이 상한)는 PM 소관. 여기서는 **계산 가능성과 정의역**만 잠근다.
- **후속 판정 2건이 이 절 아래쪽에 있다** (착지 후 추가되어 순서가 밀렸다):
  「T-A4 착지 후 전수 검증」(421,632 상태, 최악 오도 0) 과 **「T-A4b 변이 검정」**
  (`SigilCost` 7값 스윕 — 규칙이 가격 상수에 안 묶였음을 기계로 증명). T-A9 바로 앞이다.

**T-A5) 미수령 숙달이 있으면 지목이 그걸 가리킨다 (PM `trial_visit_rate` 폴백)**
진입"률"은 이번 사이클 측정 불가다 — 저장에 `TrialTiers`(5×2비트)와
`TrainingMasteryClaimed`뿐이고 텔레메트리 백엔드가 없다. 세이브에서 나오는 건 이진값
이지 모집단 비율이 아니다. PM에 회신 완료. 대신 **구조적 이진 조건**은 순수 함수다:
- 판정: `!TrainingMasteryClaimed && !CampaignStore.MasteryComplete(data)`인 상태에서
  지목 슬롯이 그 미수령 숙달(또는 그에 필요한 시련)을 가리키는 케이스 수 ≥ 1.
  0이면 FAIL.
- 근거: `CampaignStore.MasteryComplete`(`CampaignStore.cs:167`)는 5시련 전부
  `BestTier >= 판결`인지만 본다. 순수 함수.

**T-A6) 잠금 사유 텍스트 색 대비 ≥ 4.5:1**
- 판정: 사유 문자열에 쓰이는 전경색과 숯 바탕 `rgb(5,4,9)`의 WCAG 상대휘도 대비 ≥ 4.5.
  미만이면 FAIL.
- **잠금 회색 `(0.42,0.45,0.58)`은 4.37:1로 미달**이다 [OBSERVED — 계산,
  benchmark-notes v1.7]. 즉 **사유 텍스트에 잠금 회색을 쓰면 이 테스트가 즉시 빨강**이다.
  통과 색: 골드 12.20 / 시안 7.84 / 엠버 6.11.
- 이 검사는 색 상수에 대한 산술이라 EditMode 소관이다. **화면에서 실제로 그 색이
  나오는지**는 T-B5가 따로 본다.
- **판정 추가 — CanvasGroup alpha를 합성한 뒤에도 ≥ 4.5.** 잠긴 행은 `alpha = 0.45`로
  흐려진다(`LobbyView.cs:244` 스테이지, `:852` 시련). 원색이 아니라 **합성색**으로
  재야 한다. 잠금 회색 합성 결과는 **1.71:1**로 AA 큰글씨 3.0에도 미달이다
  [OBSERVED — 계산]. 합성 대비 < 4.5면 FAIL.
- 따라서 사유 텍스트를 잠김 행 안에 둘 거면 **색 교체만으로는 부족하다** — 그 텍스트를
  alpha 감쇠 대상에서 빼거나(별도 CanvasGroup / 감쇠 밖 부모), 행 alpha 자체를 올려야
  한다. 어느 쪽이든 designer 결정이고, 여기서는 **"합성 후 4.5 미만이면 FAIL"**만 잠근다.
- **스윕 범위 확정 (PMNavRevenueMap 2026-08-07 지적 반영).** 시련 행만 재면 성소 탭
  케이스가 안 잡힌다. alpha 0.45를 거는 CanvasGroup은 로비에 **4종**이다:

  | CanvasGroup | 감쇠 대상 | 감쇠되는 색 | 합성 대비(숯 위) | 판정 |
  |---|---|---|---|---|
  | `_stageGroups` (`:244`) | 잠긴 스테이지 카드 | 잠금 회색 | **1.71:1** | FAIL(<3.0) |
  | `_trialGroups` (`:852`) | 잠긴 시련 행 | 잠금 회색 | **1.71:1** | FAIL(<3.0) |
  | `_equipGroups` (`:303`) | 구매불가 **구매 버튼** | 기본 잉크 `(0.92,0.94,1.0)` | **4.03:1** | FAIL(<4.5) |
  | `_statGroups` (`:284`) | 포인트 없을 때 **`+` 버튼** | 기본 잉크 | **4.03:1** | FAIL(<4.5) |

  **감쇠색 귀속 정정**: PM은 장비 케이스를 "골드가 0.45로 감쇠 → 3.07:1"로 보고했으나,
  `_equipGroups[i]`는 **구매 버튼에 붙는다**(`:992` `buy.AddComponent<CanvasGroup>()`).
  골드인 `_equipDerived[i]`(`:980`)는 **버튼이 아니라 row의 형제**라 이 CanvasGroup
  하위가 아니다. 실제로 감쇠되는 건 `TextButton`이 만드는 라벨의 **기본 잉크**
  (`Label()` `:1181`)다. 3.07은 산술로는 맞지만(골드 a=0.45 = 3.07:1) **그 색이 거기
  없다**. 실측 4.03:1 — 여전히 AA 4.5 미달이라 **스윕에 넣어야 한다는 결론은 동일**하다.
- 판정: 위 4종 전부에 대해 합성 대비를 잰다. 하나라도 < 4.5면 FAIL. 현행은 **4/4 미달**.
- **각인 탭은 기전이 다르다**: `_sigilBuyLabels[i]`는 CanvasGroup 없이 색만 Gold→Lock으로
  바꾼다(`:344-345`, alpha 1.0 유지) → **4.37:1**. 같은 "못 산다"를 두 싱크가 다른
  기전으로 말하고 있고 **둘 다 미달**이다. 사유 텍스트 설계 시 이 불일치를 물려받지 말 것.
- **판 합성 모델 정정 — 내 3.93이 틀렸고 PM의 4.11이 맞다.** 나는 `ButtonBack`(a=0.9)
  판을 감쇠시키지 않고 그 위에 감쇠된 잉크만 얹었는데, **CanvasGroup alpha는 하위
  그래픽 전체에 곱해지므로 버튼 Image(판)도 같이 0.45로 흐려진다**. `_equipGroups`는
  버튼 GameObject에 붙고(`:992`) 판 Image는 그 자신이므로 감쇠 대상이다. 판까지 감쇠한
  올바른 합성은 **4.11:1** — 배경이 더 어두워져 숯 직상 4.03보다 오히려 **높다**.
  즉 판은 대비를 낮추는 게 아니라 살짝 올린다. 내 "상한" 표기는 방향이 반대였다.
  판정에는 영향 없다(4.11 < 4.5, 여전히 FAIL). **문서 값은 4.11로 통일**한다 —
  미해소 병기 대신 옳은 모델 하나만 남긴다.

**T-A7) 레이아웃 rect 감사 — 신규 요소가 기존 것을 덮지 않는다**
cycle-3에서 "라벨이 버튼에 가림"과 "통로 20px"를 테스트가 놓친 이유는 **값만 보고
겹침을 안 봤기 때문**이다. 그 구멍을 여기서 막는다.
- 판정: 신규 지목 마커·배지 rect와 기존 인터랙티브 rect의 겹침 면적 > 1u²인 쌍의 수 == 0.
  1쌍이라도 있으면 FAIL. (`OverlapEpsilon = 1f` 관례 유지)
- 판정: 모든 신규 rect의 `width > 0 && height > 0`. 0이면 FAIL(레이아웃 미해결).
- 판정: 신규 rect가 부모 뷰포트 경계를 벗어난 건수 == 0. cycle-3의 "부모가 캔버스 밖"
  재발 방지.

**T-A8) 폰트 글리프 커버리지 — 누락 0**
- 절차: 신규 한국어 문자열 추가 후 `bash tools/gen_hud_font.sh` 재실행.
- 판정: 스크립트의 커버리지 검사가 `coverage: FULL`. **누락 문자 ≥ 1이면 FAIL**
  (스크립트가 `SystemExit(1)`로 이미 차단한다).
- 판정: 글리프 수가 변했으면 **`Assets/Resources/Fonts/HudKorean.otf` 재생성 커밋이
  동반**되어야 한다. 문자열만 늘고 폰트가 그대로면 FAIL — 화면에 두부가 뜬다.
  이건 T-B4가 육안으로 재확인한다.

**T-A8 실행 결과 — 라이브 결함 3건 [OBSERVED, 2026-08-07]**
DesignerNavSurvey 제보를 받아 실제로 돌려서 **재현했다**. 이건 계획이 아니라 현재 상태다:

```
폰트 cmap 글리프 수 = 498
소스 스캔 글리프 수 = 499
폰트에 없는 문자 = [('·', '0xb7')]
```

**`·` U+00B7 MIDDLE DOT이 View 소스에 있는데 배포 폰트에 없다.** 즉 **지금 빌드에서
이미 두부가 뜨고 있다.** 사용자 가시 문자열 4건(`HudView.cs:1138` 성장 옵션 /
`:1196` 콘솔 플레이스홀더 / `:1246` 콘솔 토스트 / `:1698` 레벨업 토스트).
`LobbyView.cs`·`CampaignStore.cs`·`GameDirector.cs`의 `·`는 주석이라 화면과 무관하다.
**단 "가시 4건"과 "스캐너가 잡는 건수"는 다른 수다** — 스크립트의 전체 파일 정규식이
실제로 잡는 `·` 문자열은 3건이고 그 중 2건이 주석 안 인용구다(아래 결함 2건째 참조).

**결함 3건째 — `간` U+AC04. 조사 중에 구현이 착지하면서 발생했고, `FontCoverageTests`가
지금 RED다. [OBSERVED, 2026-08-07 12:04]**

조사 도중 `Assets/Scripts/View/ProgressionGuide.cs`(신규, untracked)와
`LobbyView.cs`/`GameDirector.cs` 수정이 착지했다. 재스캔 결과:

| 스캔 | charset | 폰트(498) 대비 누락 |
|---|---|---|
| 전체 파일(생성기 실동작) | 500자 | `·`, **`간`** |
| 줄 단위 | 500자 | `·`, `−`, **`간`** |
| **`FontCoverageTests` 규칙(한글만)** | 395자 | **`간`** ← **기존 테스트가 RED** |

`간`의 출처는 `ProgressionGuide.cs:105`/`:115`의 `/// worldview.md §공간 계보` 주석이다.
화면 문자열이 아니지만 **생성기와 테스트 둘 다 주석의 한글을 걷는다**(의도된 싸구려
상위집합). 즉 **주석 한 줄이 EditMode 게이트를 빨갛게 만든다** — 설계대로 동작한 것이지
테스트 결함이 아니다. 조치는 `bash tools/gen_hud_font.sh` 재실행 한 번이다.

**이 사건이 T-A8의 승격 근거를 확정한다.** 나는 앞서 "T-A8을 매 뷰 편집 묶음마다 도는
항목으로 승격한다"고 적었는데, 그 문장을 쓴 직후 **정확히 그 실패가 실시간으로 재현됐다**.
뷰 파일 3개가 들어오자마자 글리프 게이트가 깨졌고, 기존 테스트는 그중 `간` 하나만
잡는다(`·`·`−`는 여전히 못 잡는다 — 아래 참조).

**앞선 서술 정정**: 나는 "기존 `FontCoverageTests`는 현재 초록"이라고 적었다. 그건
ProgressionGuide.cs 착지 **이전** 스캔 기준이었고 지금은 **RED**다. 초록이었다는 사실
자체는 유효하지만(그래서 `·`·`−`를 놓쳤다), 현재 상태로는 틀린 서술이라 정정한다.

- **컨텍스트의 "497 글리프"는 낡은 수치다.** 실측 폰트 498 / 소스 499. 문서·계획에서
  497을 기준선으로 인용하지 말 것.
- 원인: 문자열은 늘었는데 `tools/gen_hud_font.sh` 재실행이 빠졌다 — T-A8이 막으려던
  실패 유형 그 자체가 이미 통과해 나갔다. **이 회귀 항목이 필요하다는 증거**다.
- 조치: 네비게이션 작업과 **무관하게** 선행 수정 대상이다. 이번 사이클은 조사만이므로
  고치지 않고 기록만 남긴다. `·`를 쓸 거면 폰트 재생성, 안 쓸 거면 `•`(이미 포함)로 교체.
- 회귀 형태: T-A8을 "신규 문자열 추가 시"가 아니라 **매 뷰 편집 묶음마다** 도는
  항목으로 승격한다. 지금 결함이 그 공백에서 나왔다.

**결함 2건째 — `−` U+2212. 이건 폰트 재생성으로도 안 고쳐진다. [OBSERVED, 2026-08-07]**

DesignerNavSurvey가 "스크립트 정규식이 파일 전체를 한 번에 스캔한다"고 지적해서 두 모드를
비교하다 **더 나쁜 결함**이 나왔다.

| 스캔 모드 | charset | 폰트(498) 대비 누락 |
|---|---|---|
| **전체 파일** (스크립트 실동작) | 499자 | `·` 1건 |
| **줄 단위** (직관) | 499자 | `·`, **`−`** 2건 |

같은 499자인데 **집합이 다르다**: 줄 단위에만 `−`(U+2212), 전체 스캔에만 `§`(U+00A7)가
있다. 전체 스캔이 놓치는 한국어 문자열이 **34건**이다(따옴표 짝짓기가 줄 경계를 넘어
엇물린 결과). `§`는 폰트에 있으니 무해하지만 **`−`는 없다**.

`−`의 출처는 주석이 아니다: `HudView.cs:884`
`$"Companion cadence −{10 * magnitude}% (min 0.5 s)"` — `EmberRestEffectLabel`이
반환하는 **레벨업 제안 라벨, 사용자 가시 문자열**이다.

**두 결함의 등급이 다르다:**

| | `·` U+00B7 | `−` U+2212 |
|---|---|---|
| 스크립트가 보는가 | **예** (전체 스캔이 잡음) | **아니오** (짝짓기에 먹힘) |
| 원인 | 폰트 재생성 누락 | **도구 결함** |
| `gen_hud_font.sh` 재실행하면 | 해소됨 | **여전히 누락** — charset에 안 들어가니 서브셋에도 안 들어감 |
| 스크립트 판정 | `SystemExit(1)`로 차단됨 | **`coverage: FULL` 초록** |

`−`는 **스크립트가 초록을 주면서 화면에는 두부가 뜨는** 상태다. 위양성이라 `·`보다 나쁘다.

**기존 게이트 `FontCoverageTests.cs`가 생성기의 진부분집합이다 (PMNavRevenueMap 제보,
독립 재현 완료).** T-A8은 신규 검사가 아니라 **기존 검사의 확장**이다. 실제 코드를 읽었다:

| | 절 ① 따옴표 리터럴 | 절 ② `[가-힣]` | 절 ③ punctuation·ascii·`•▲▼◀▶—` 주입 |
|---|---|---|---|
| `gen_hud_font.sh` | 있음 | 있음 | 있음 |
| `FontCoverageTests.cs:34` | **없음** | 있음 | **없음** |

`FontCoverageTests.cs:31-33`의 주석은 *"Same harvest rule as tools/gen_hud_font.sh …
the generator harvests identically"*라고 적혀 있는데 **거짓이다**. 생성기는 절이 3개고
테스트는 ②만 본다. 그래서 charset이 500 대 395로 갈리고, **비한글 누락은 구조적으로
못 잡는다** — `·`와 `−`가 정확히 그 구멍으로 빠져나갔다.

PM 검산도 같은 결론이다: 그 한글-전용 규칙으로 `다음 재판 − 해류` 같은 문자열을 검사하면
**통과로 나오고 화면엔 두부가 뜬다**. 후보 문자열들이 지금 안전한 건 비한글이 `:`·공백뿐이라
`string.punctuation` 주입에 우연히 걸려서지, 규칙이 막아준 게 아니다.

따라서 T-A8의 형태는 **`FontCoverageTests`를 대체하는 게 아니라 절 ①·③을 추가하는 것**이다.
그 테스트의 거짓 주석도 같이 고쳐야 한다 — 안 고치면 다음 사람이 다시 "생성기와 동일"로 읽는다.

**T-A8의 진짜 근거 — 새 가드를 만드는 게 아니라 원래 있던 가드가 왜 안 울렸는지를 닫는다
(designer 프레이밍, 검증 완료).**
`gen_hud_font.sh` 27-37행은 **정확히 이 경우를 위해** 이미 짜여 있다:
`MISSING (source font lacks these — replace in code)` + `SystemExit(1)`. 스크립트가
처음부터 "치환하라"고 처방까지 문자열로 적어놨다. 즉 결함은 가드 부재가 아니라 **가드가
우회된 채 배포된 것**이다.

그런데 우회 방식이 **두 가지로 갈린다**. 착지 전 charset(스크립트 실동작 규칙, 499자)을
복원해 가드 발화 조건("charset에 있는데 소스 폰트에 없음")을 돌려봤다:

| 문자 | charset 진입 | 소스 폰트 | 가드가 울렸을까 | 우회 경로 |
|---|---|---|---|---|
| `·` U+00B7 | **예** | 없음 | **울렸을 것** (발화 대상 목록에 있음) | **스크립트를 안 돌렸다** |
| `−` U+2212 | **아니오** | 없음 | **울릴 수 없음** | **스캐너 사각** — 가드가 볼 기회조차 없음 |

같은 결함 등급으로 묶으면 안 된다. `·`는 **절차 누락**(가드는 정상, 안 돌렸을 뿐)이고
`−`는 **가드 무력화**(돌려도 안 울린다)다. 처방도 갈린다:

- `·`류 → T-A8을 **매 뷰 편집 묶음마다** 돌리면 닫힌다(가드가 이미 옳으므로).
- `−`류 → 돌리는 빈도를 아무리 올려도 안 닫힌다. **정규식 자체를 고쳐야** 하고, 그게
  Main이 추가한 이스케이프 인식 규칙이다.

이 구분이 T-A8이 **두 판정을 따로 두는 이유**다. 빈도 승격만으로도, 정규식 교체만으로도
각각 절반만 닫힌다.

**T-A8 처방 정정 — 검사기는 스크립트와 같은 정규식을 써야 한다.**
designer 지적대로, "사용자 가시 문자열만 스캔"하는 별도 검사기를 만들면 `gen_hud_font.sh`의
실제 charset과 어긋나 **통과했는데 폰트에 없는** 상태가 재발한다. 동시에 **스크립트 정규식만
믿어도** `−`처럼 스캐너 사각에 있는 문자를 영원히 못 잡는다. 따라서 T-A8은 **두 모드를 모두**
돌리고 합집합으로 판정한다:

- 판정 (0) `FontCoverageTests` 규칙(한글만) charset − 폰트 cmap == ∅. **기존 게이트.**
- 판정 (1) 전체 파일 정규식 charset − 폰트 cmap == ∅. 아니면 FAIL (폰트가 낡음).
- 판정 (2) 줄 단위 정규식 charset − 폰트 cmap == ∅. 아니면 FAIL (스캐너 사각).
- 판정 (3) 두 charset의 대칭차 — **크기가 아니라 `대칭차 ∩ 폰트 누락`으로 판정**한다.
  착지 전 `{−}`(FAIL), 착지 후 **∅**(PASS). 원시 대칭차 크기는 정규식 건전성의 **이진
  지표**로만 읽는다(생성기 실동작 기준 구정규식 2, 신정규식 0 — 아래 「정정의 정정」 참조).
- **착지 전 현행: (0) FAIL(`간`) · (1) FAIL(`·`,`간`) · (2) FAIL(`·`,`−`,`간`). 3/3 실패.**
  **착지 후: 4판정 전부 PASS**(아래 「해소 완료」).

- **주석을 화면 무관으로 치우면 안 되는 이유**: 전체 스캔이 실제로 잡는 `·` 문자열 3건 중
  **2건이 주석 안 인용구**다(`HudView.cs:1634`, `:2205`). 즉 코드에서 `·`를 전부 걷어내도
  주석에 남으면 charset에 다시 들어온다. 반대로 주석까지 지우면 charset에서 빠지는데
  **사용자 가시 3건은 그대로 남아** 두부가 된다. 문자 제거로 닫으려면 주석·코드를 **동시에**
  훑어야 하고, 그게 싫으면 폰트 재생성이 유일한 경로다.
- **대체 문자는 `•`(U+2022)가 안전하다** — 스크립트 11행 `chars.update('•▲▼◀▶—')`가
  소스와 무관하게 **무조건 주입**하므로 항상 폰트에 있다.
- **해소 경로가 결함마다 다르다 — 정정 후 확정판 (Main 실측 제보 + 독립 재현).**
  내 초판 표에서 **`·` 행이 틀렸다**: "재생성으로 해소 = 예"라고 적었는데 **아니다**.
  소스 폰트 `~/Library/Fonts/NanumBarunGothic.otf`(cmap 18,665자)를 직접 열어 확인했다:

  | 결함 | 소스 폰트에 있나 | 재생성으로 해소? | 치환 경로 | 치환 시 재생성 |
  |---|---|---|---|---|
  | `간` U+AC04 | **있음** | **예 — 유일 조치** | 불가(정당한 주석 한글) | — |
  | `≥` U+2265 | **있음** | **예** (Main이 4번째로 발견) | 불필요 | — |
  | `·` U+00B7 | **없음** | **아니오** ← 내 초판 오류 | `•` U+2022 | 불필요 |
  | `−` U+2212 | **없음** | **아니오** (charset 미진입 + 소스 부재, 사유 2중) | `-` U+002D | 불필요 |

  즉 `·`가 못 들어간 이유는 **charset 문제가 아니라 소스 폰트에 글리프 자체가 없어서**다.
  `gen_hud_font.sh`의 커버리지 검사가 `MISSING (source font lacks these — replace in code)`로
  exit 1 하는 경로가 정확히 이것이고, 스크립트가 처방까지 문자열로 적어놨다("replace in code").
  **재생성 가능/불가를 가르는 건 charset 진입 여부가 아니라 소스 폰트 cmap이다** — 내 초판은
  이 축을 안 봤다.

  **PM이 제안한 세 번째 경로("수확 규칙 수정으로 `·`가 charset에서 빠졌다")는 성립하지
  않는다 — 반사실 검정으로 기각했다.** `git show HEAD:...HudView.cs`로 착지 전 파일을
  꺼내 정규식만 바꿔 돌렸다:

  | | 구정규식 | 신정규식 |
  |---|---|---|
  | 착지 **전** 소스 | `·` 인용문 3건 | **6건** |
  | 착지 **후** 소스 | 0건 | 0건 |

  정규식만 고쳤다면 `·`는 charset에서 빠지기는커녕 **3건 → 6건으로 늘었다**. 신정규식이
  이스케이프를 인식해 **더 많이** 걷기 때문이다. `·`가 charset에서 사라진 원인은
  `HudView.cs`의 `·` 10개가 전부 `•`로 **치환**된 것이다(diff로 확인).
  즉 해소 경로는 여전히 **2가지**다 — 재생성(`간`·`≥`) / 소스 치환(`·`·`−`).
  수확 규칙 수정은 **미래의 재발을 막는 조치**이지 이번 `·`를 닫은 조치가 아니다.
  두 효과를 같은 칸에 적으면 다음에 `·`류가 또 들어왔을 때 "정규식이 알아서 뺀다"고
  오독한다.

**T-A6 착지 확인 — 배치로 풀렸고, 내 계산이 그 선택의 근거였다 [OBSERVED].**
사유 라벨은 `CanvasGroup{alpha=1, ignoreParentGroups=true}`로 감쇠 **밖**에 놓였고 색은
InkDim이다. 합성 후 **8.71:1**(Main 보고 8.69, 반올림 차 0.02 — 둘 다 AA 4.5를 크게 상회).
이게 유일한 해법이었음을 팔레트 전수로 확인했다 — **alpha 0.45 뒤에서는 어떤 색도 통과 못 한다**:

| 색 | a=0.45 합성 | AA 4.5 |
|---|---|---|
| 잉크 `(0.92,0.94,1.0)` | 4.03 | 미달 (최선) |
| 골드 | 3.07 | 미달 |
| InkDim | 2.48 | 미달 |
| 시안 | 2.31 | 미달 |
| 엠버 | 1.98 | 미달 |
| 잠금 회색 | 1.71 | 미달 |

내가 T-A6에 적었던 **"색 교체만으로는 부족하다 — alpha 감쇠에서 빼거나 행 alpha를 올려야
한다"**가 그대로 채택됐다. 판정을 수치로 잠근 것이 설계 선택을 강제한 사례다.
(사소한 정정: 나는 최선을 "골드 3.07"로 적었는데 실제 최선은 **잉크 4.03**이다. 둘 다
미달이라 결론은 안 바뀐다.)

**T-A4 착지 후 전수 검증 — 밴드 통과 [OBSERVED].**
`ProgressionGuide.Badges`(`:297-324`)를 장비 6³ × 각인 소유 2⁵ × 유물 0–60 =
**421,632 상태**에 대입해 독립 검산했다(PM은 유물 경쟁이 나는 부분집합 73,039개로 더
좁게 쟀다 — 범위가 다르고 결론은 같다):

```
최악 오도 = 0 유물        (밴드 max 2 → PASS)
밴드 위반 상태 = 0개
유물 탭 동시 점등 = 0회   (negotiation entry 10 금지 조항)
기준 사례 T0/T0/T0 + 유물 12 → equip=True, sigil=False, cheapest=2, 오도 |2−2| = 0
역방향 T5/T5/T5 + 유물 12 → 각인 단독 점등 (규칙이 각인을 영구 배제하지 않음)
```

**착지 전 이 사례의 오도가 10이었다**(밴드의 5배). 지금 0이다. 결정적인 건 `:320`이
가격을 **런타임에 읽어 비교**한다는 것이다 — 오늘의 2/12를 인라인하지 않았으므로
각인 가격이 12→17로 가도 규칙이 안 바뀐다. entry 6 미서명 상태에서 그 비의존성이
기계로 고정됐다.

**T-A4b) 변이 검정 — 규칙이 가격 상수에 의존하지 않음을 기계로 증명한다 [신규 판정]**

"런타임에 읽는다"는 코드 읽기로 주장할 수도 있지만, **변이로 증명하는 게 더 싸고 강하다**.
`SigilCost`를 바꿔가며 전수 스윕을 재실행했다:

| `SigilCost` | 2 | 7 | 12(현행) | 16 | 17 | 25 | 40 |
|---|---|---|---|---|---|---|---|
| 최악 오도 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 밴드 위반 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 동시 점등 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| 각인 점등 가능 | 예 | 예 | 예 | 예 | 예 | 예 | 예 |

**7개 값 전부 PASS.** 규칙에 가격 상수가 없다는 것의 기계적 증명이다.

- 판정: `SigilCost ∈ {2,7,12,16,17,25,40}` 각각에서 최악 오도 ≤ 2 · 위반 0 · 동시 점등 0.
  **하나라도 실패하면 규칙이 오늘의 가격표에 묶인 것이므로 FAIL.**
- 판정: 모든 변이값에서 각인이 점등 가능해야 한다. 어떤 값에서 **영구 배제**되면 FAIL.

**게이트 경로로 전환하면 이 판정이 반드시 필요하다 (PM §6.5 인계).**
designer의 N-A 결과가 "장르의 답은 배지가 아니라 **가용성 게이트**"였고, PM이 그 경로의
경제 리스크를 넘겼다. 가상 게이트(문턱을 상수 72로 박음)를 같은 스윕에 넣어봤다:

| `SigilCost` | 12 | 16 | 17 |
|---|---|---|---|
| 최악 오도 | **10** | **14** | **15** |
| 밴드 위반 상태 | 182,063 | 186,992 | 186,992 |

**전부 FAIL이고 `SigilCost`가 오를수록 악화된다.** 상수 문턱은 가격이 움직이는 순간
깨진다. 따라서 게이트로 가면 문턱은 **런타임 산출**(`min(남은 장비 스텝) > SigilCost`)
이어야 하고, T-A4b가 "문턱이 상수인가"를 잡는 판정이 된다.

**착지한 배지 경로는 이 리스크가 구조적으로 없다** — 규칙에 문턱이라는 개념 자체가 없고
`:320`이 두 가격을 비교할 뿐이다. 위 7값 전수 통과가 그 사실의 증거다.
**T-A4b의 사각 — 게이트 변이 두 종류 중 하나만 잡는다 (PM 지적, 재현 완료).**
내가 잰 게이트는 "문턱 이후 각인 우선"(게이트A)이었는데, PM의 §6.5 시나리오는
**"문턱까지 각인 구매 자체 차단"(게이트B)**으로 다른 변이였다. 셋을 같은 스윕에 넣었다:

| 모델 | c=12 | c=16 | c=17 | T-A4b 판정 |
|---|---|---|---|---|
| 착지 배지 규칙 | 오도 0 | 0 | 0 | PASS |
| 게이트A (문턱 이후 각인 우선) | **10** | **14** | **15** | **FAIL** |
| 게이트B (문턱까지 구매 차단) | 0 | 0 | 0 | **PASS ← 못 잡는다** |

**게이트B가 T-A4b를 통과하는 이유**: 각인이 구매 불가면 배지가 최저비용 장비를 가리키고
그게 실제 최저이므로 **오도가 정의상 0**이다. 실패 모드가 오유도가 아니라 **싱크 소멸**이라
오도 지표에 안 잡힌다. 역으로 PM의 포화 산술은 게이트A를 못 잡는다(구매 가능하니 싱크는
실현된다). 즉 두 게이트는 **상보**다:

| 게이트 | 잡는 변이 | 놓치는 변이 |
|---|---|---|
| T-A4b 변이 검정 | 배지 우선순위 오염 | 구매 차단형 싱크 소멸 |
| PM §6.5 포화 산술 | 구매 차단형 싱크 소멸 | 배지 우선순위 오염 |

**"두 근거는 독립"이라고 적은 내 처리는 유지되지만 이유가 바뀐다** — 같은 사실을 다른
경로로 확인하는 게 아니라 **서로 다른 실패 모드를 덮는다**. 어느 하나로 다른 하나를
대체할 수 없다.

**PM 제안 ②(각인 싱크 도달 가능성)는 전제가 하나 필요하다.** 게이트B에서 "각인이 한 번
이라도 점등 가능한가"를 유물 상한별로 재봤다:

| 유물 상한 | 40 | 60 | 71 | 72 | 100 |
|---|---|---|---|---|---|
| 도달 가능 | 아니오 | 아니오 | 아니오 | 예 | 예 |

같은 규칙·같은 문턱인데 **유물 상한을 무엇으로 두느냐에 따라 판정이 뒤집힌다**. 상한은
`CampaignData`의 함수가 아니라 **유물 수입 곡선**의 함수다. 즉 ②는 T-A4b와 달리 순수
EditMode 판정이 아니다.

**정정 — "서명이 필요하다"까지가 내 결론이었는데, PM이 이미 서명돼 있음을 보였다.**
나는 "상한이 상수로 서명돼야 기계 판정이 된다"고 적었다. PM 지적: **상한의 하한은 이미
계약돼 있다.** `pm/reward-bands.md`의 `steady.parity_sessions_band: [10,20]`(계약)에
revenue-map §0의 실측 앵커를 곱하면 생애 예산이 나온다. 독립 검산했다:

| 랭크 | r [OBSERVED] | 하한 10런 예산 | 문턱 72 |
|---|---|---|---|
| 만개 | 17.67 | 176.7 | 통과 |
| **무성장** | **7.00** | **70.0** | **미달 (2 부족)** |

즉 **새 서명 없이 지금 판정 가능**하고, 그 판정은 **게이트B 문턱 72 탈락**이다.
내 전제는 형식적으로 옳았지만 이미 충족돼 있었다 — 나는 밴드 문서를 안 봤다.

**그리고 더 강한 결과: 상수 문턱은 어떤 값도 안 된다.** 최저비용 우선 사다리 누적을
직접 계산해 도달성 상한 70과 대조했다:

| 티어 | 누적 | 하한 파일럿 |
|---|---|---|
| T(4,4,3) | **61** | 도달 가능 — 70 이하 **최대** |
| **T(4,4,4)** | **72** | **도달 불가 — 2유물 초과** |

§N-A의 스텝 효율 임계는 72(3슬롯 전부 T4)인데 도달성 상한은 70이다. **2유물 차이로
배타적**이다 — 문턱 72면 하한 파일럿이 영구 미도달(싱크 소멸), 문턱 61이면 각인이 T4
완성 전에 열려 61→72 구간 보호가 깨진다. 부등호를 `EquipCosts`와 실측 r이 정하므로
**선호가 아니라 산술**이고 협상으로 못 뒤집는다(밴드 하한을 11로 올리면 11×7.00=77로
해소되나 그건 `parity_sessions_band` 개정 = director 안건).

따라서 게이트 전환 협상 안건이 3개로 확정됐다: ① T-A4b 변이 전체 통과 · ② 도달성
(지금 산술 판정 가능, 새 서명 불요) · **③ 문턱 상수 금지**. ③이 실질 산출이다.

**지금은 ②를 T-A4b에 넣지 않는다** — 배지 경로에서는 항상 참이라 판정력이 0이다
(위 7값 스윕의 "각인 점등 가능" 행이 그 사실이다). PM 판단과 동일.

**착지한 배지 경로는 두 리스크가 다 구조적으로 없다** — 규칙에 문턱이라는 개념 자체가
없고 `:320`이 두 가격을 비교할 뿐이다. PM의 경제 산술(포화 8.83런 → 5.43런)은 내 범위
밖이라 검산하지 않았다.

위반 상태 수는 PM 2,205/4,050/3,960 vs 내 182,063/186,992로 갈리는데, 각인 소유를
개수 6으로 축약했느냐 비트마스크 2⁵로 폈느냐의 차이다. **최악 오도와 단조 증가 방향이
일치**하므로 판정에 영향 없다.

**해소 완료 [OBSERVED, 2026-08-07, Main 조치 후 재검증]**

```
(0) FontCoverageTests 한글만  395자  누락 0
(1) 전체파일 구정규식          500자  누락 0
(1') 전체파일 신정규식         498자  누락 0
(2) 줄단위 구·신정규식         498자  누락 0
    합집합                    500자  누락 0
```

**4판정 전부 통과.** 배포 폰트 cmap 501자(charset 500 + 서브셋터가 넣는 `≤` U+2264 1자 —
스크립트가 찍는 500과 cmap 501은 세는 대상이 달라 둘 다 맞다).

조치 내역: `−`→`-`(HudView.cs:889) · `·`→`•` 5곳(가시 4 + 주석 2) · `간`·`≥` 재생성.
주석 2건은 **코드가 이미 `•`를 조립하는데 주석만 `·`로 인용**하던 케이스였다 — 문서가 화면과
어긋난 것이라 화면 쪽에 맞췄다. 소스에 남은 `·`·`−`는 전부 주석이고 charset 누락 0이다.

**생성기 자체도 수정됐다 — 근본 원인이 내 진단보다 정확하다.**
나는 "따옴표 짝짓기가 줄 경계를 넘어 엇물린다"까지 봤는데, Main이 한 겹 더 파고들었다:
`"([^"\\]*)"`는 **백슬래시를 포함한 리터럴을 아예 안 먹는다**. 그런 리터럴이 연달아 나오면
짝짓기가 어긋나 **그 사이 문자열이 통째로 매치 틈에 빠진다**. 그 charset으로 폰트를
검증하니 초록 — **자기 사각지대를 기준으로 스스로를 검증하는 검사기**였다.
이스케이프 인식 규칙 `"((?:[^"\\\n]|\\.)*)"`가 union으로 추가됐다.

**T-A8 판정 형태는 유지한다.** 신정규식이 구정규식의 상위집합이 아니기 때문이다 —
실측 charset이 500(구) vs 498(신)으로 **양쪽이 서로 다른 것을 잡는다**. 어느 하나로
갈아타지 말고 **합집합 판정**을 유지해야 한다. 이번에 넷 다 0이 된 건 결과이지 근거가 아니다.

**주입 검정 — 게이트가 장식이 아님을 실증했다 [OBSERVED].**
"결함이 닫혔으니 게이트를 내려도 되나"에 답하려고, 현재 소스에 U+2212를 담은 새 문자열
(`"정예 −20% 방어"`)을 주입하고 두 정규식을 돌렸다:

| 정규식 | 주입 문자열 탐지 |
|---|---|
| 구정규식 `"([^"\\]*)"` | **0건** — 여전히 못 본다 |
| 신정규식 `"((?:[^"\\\n]|\\.)*)"` | 1건 |

**원인 구조는 그대로다.** 닫힌 건 결함 4건이지 사각 자체가 아니다. 누가 새 문자열에
`−`를 쓰면 구정규식 경로로는 다시 탐지 없이 두부가 된다. 두 모드 합집합 게이트가
그 재발을 막는 유일한 장치이므로 **통과했다고 내리지 않는다**(designer와 동일 결론).

**부수 관측 — 대칭차는 문자 목록이 아니라 이진 지표로 써야 한다.**
**[정정의 정정 — 내 1차 정정이 틀렸고 designer의 원래 값이 옳았다]**

나는 designer의 `{§, ≥}`를 "검산 없이 인용했다"며 원시 대칭차로 정정했다(그때 31/30을
오갔는데, 공백 포함 여부만 다른 같은 오류값이다).
**그 정정이 틀렸다.** 내 재계산 모델이 생성기와 달랐다 — `[가-힣]` **한글 스윕을
빠뜨렸다**. 생성기는 한글을 **스캔 모드와 무관하게 파일 전체에서** 걷으므로
(`gen_hud_font.sh` 절 ②) 두 모드 집합에 동일하게 들어가고, 그래서 한글은 대칭차에
기여하지 않는다. 내 30자에 섞인 `괴 급 듬 링 밍 밖 …`이 전부 그 누락의 산물이었다.

생성기를 완전 재현(base 주입 + 인용문 + 한글 전체 스윕)해서 다시 쟀다:

| 기준 | 착지 전 | 착지 후 |
|---|---|---|
| **생성기 실동작** (base+인용문+한글) | \|W\|=499 \|L\|=499, 대칭차 **2** `{§, −}` | \|W\|=500 \|L\|=498, 대칭차 **2** `{§, ≥}` |
| base 주입, 한글 제외 | 대칭차 14 | 대칭차 24 |
| 원시 (base·한글 다 제외) | 대칭차 20 | 대칭차 30 ← **내 오류값** |
| **대칭차 ∩ 폰트 누락** | `{−}` | **∅** |

**designer의 `{§, ≥}`가 정확히 재현된다.** 착지 후 생성기 실동작 기준이고, 내 30은
생성기가 실제로 하는 일을 모델링하지 못한 값이었다. 남의 수를 검산 없이 옮긴 걸
지적하면서 **내 검산 모델이 틀렸던** 것이라 더 나쁘다 — 정정한다.

**결론은 세 기준 모두 동일하다**: `대칭차 ∩ 폰트 누락`이 착지 전 `{−}`, 착지 후 ∅.
판정에 쓸 값은 대칭차 크기가 아니라 이 교집합이다.

designer가 "대칭차를 보고 항목에서 **진단 신호**로 승격하자"고 제안한 것은 **영/비영으로만
쓰면 성립한다**: 생성기 실동작 기준으로 구정규식은 착지 전후 2→2로 **0이 된 적이 없고**,
신정규식은 0이다. 즉 대칭차 ≠ ∅ 은 "짝짓기가 줄 경계를 넘어 어긋난다"는 정규식 건전성의
이진 지표이고, 어느 문자가 위험한지는 교집합이 말한다. 두 용도를 섞으면 틀린 수가
전파된다 — **이번 사이클에 우리 둘 사이에서 실제로 두 번 일어났다**(내가 designer 값을
검산 없이 인용 → 내가 틀린 모델로 그걸 "정정" → designer가 기준선을 제시해 해소).

그럼에도 **구정규식을 판정에서 빼지 않는다**: 생성기가 실제로 그 규칙으로 서브셋을 만드는
한, 그 charset과 폰트의 일치는 별도로 지켜져야 하는 계약이다.

**T-A9) PM 확정 밴드 결선 (2026-08-07, `pm/revenue-map.md` §v1.7 §2)**

PM이 밴드 3개를 확정했다. 이름·값을 그대로 박아 두 문서가 어긋나지 않게 한다.

| 밴드 | 값 | 어느 판정이 집행하나 | 현행 실측 |
|---|---|---|---|
| `badge_misdirect_relics` | `max: 2` (스코프: 장비 5티어 + 각인 12. Points/무료 제외) | T-A4 | **10** — 아래 검증 |
| `mastery_surface_rows_visible` | `min: 1` + **전제 2개**(아래) | T-A1 폴백 + T-B8 | **0** |
| `mastery_pointer_coverage` | `required: true` (이진, 순수 함수) | T-A5 | 미구현 |

**밴드 위반 실측 재현 [OBSERVED — 계산]**
`T0/T0/T0 + Relics=12`에서 Relics 스코프 구매 가능 집합은 `{장비×3 = 2, 각인 = 12}`,
최저 = 2. 각인을 지목하면 `|12 − 2| = 10` = **밴드 max의 5배**. 최저비용을 지목하면 0.
PM 산정과 정확히 일치 확인.

**`mastery_surface_rows_visible` 기하 — 2u 불일치 해소, 706이 권위값.**
내 초판 708u는 **상수 혼합 오류**였다. 카드 y는 `Card(content, -6 - row*70, 68)`
(`LobbyView.cs:684`/`:784`/`:808` 동일 공식)이고, 콘텐츠 높이의 `+8`(`:669-670`)은
**말미 패드**라 카드 y에 들어가지 않는다. 두 상수를 섞어 `8 + 70*10`으로 계산한 게
708이었다. 정정: **row10 top = `6 + 70*10` = 706u**. PM 값이 맞고 내 값이 틀렸다.
뷰포트 434u를 272u 초과 → 가시 행 0. 코드 재확인으로 검증 완료.
판정은 "가시 행 ≥ 1" 정수 조건을 유지한다 — 2u가 판정을 못 바꾸는 형태라 이 정정이
게이트 설계를 바꾸지는 않는다. 다만 문서에는 **706만 권위값**으로 두고 708은 이 해소
주석 안에만 남긴다.

**밴드 metric 정정 (PM 2026-08-07) — 전제 2개가 추가됐다.**
초판 metric에는 `PrologueDone` 전제와 `interactable` 조건이 없었다. 그래서 프롤로그
미완료 세이브에서 **alpha 0.45 잠김 시련 행이 "보이는 행"으로 계수되어 밴드를
충족시키면서 동시에 T-A6을 깨는** 경로가 열려 있었다(잠김 행 합성 대비 **1.71:1**,
AA 큰글씨 3.0에도 미달 — benchmark-notes v1.7 §색 대비). 밴드가 다른 게이트를 깨는
방향으로 만족될 수 있으면 밴드 결함이다. 확정 metric:

```yaml
band: mastery_surface_rows_visible
metric: "`PrologueDone == true` 인 세이브에서, 기본 스크롤 위치에 보이는
  '미수령 숙달 경로' 행 수 (시련 5행 + 등급선택 1행 중).
  행은 `interactable == true` 여야 계수한다 — alpha 0.45 잠김 행은 0으로 센다"
min: 1
observed_now: 0
```

**metric 문구 정정 — 실행에서 2 vs 3으로 갈렸다. 내 문구가 모호했다. [2026-08-07]**
`qa/gate-measurements.md` g7이 이 밴드를 **2**로 보고했는데(`06-training-fold.png`),
같은 줄의 산문은 "등급 1행 + 시련 2행"으로 **3**을 기술한다. 기하를 재보면 훈련장 펼침
시 완전 가시 본문 행은 **등급선택·시련1·시련2 = 3개**다(시련3은 [414,482]로 뷰포트
434를 넘는다).

갈린 원인은 내 문구다 — **"'미수령 숙달 경로' 행 수 (시련 5행 + 등급선택 1행 중)"**에서
괄호는 등급선택을 스코프에 넣는데, "숙달 경로"라는 말은 시련만 가리키는 것처럼 읽힌다.
**밴드 min 1은 어느 해석으로도 PASS**라 이번엔 판정이 안 갈렸지만, 하한을 2 이상으로
올리는 순간 해석 차가 판정을 뒤집는다.

**확정 해석: 등급선택을 포함해 센다(정의상 3).** 근거 — 등급선택은 시련 진입의 전제
(티어를 골라야 시련을 돈다)이므로 "숙달 경로"의 일부다. `interactable` 조건도 시련과
동일하게 `open = data.PrologueDone`이 결정한다(`:857-858`). 문구를 이렇게 읽는다:

> "`PrologueDone == true` 세이브에서, 기본 스크롤 위치에 **완전 가시**인 훈련장 그룹
> 본문 행 수. 대상은 등급선택 1행 + 시련 5행 = 총 6행이며, `interactable == true`인
> 행만 센다. 현행 실측 **3**(등급선택·시련1·시련2)."

`observed_now: 0`은 **착지 전** 값이다(평면 1058u에서 훈련장 행 전부 비가시). 착지 후는
3이므로 밴드는 충족됐다. 두 값을 구분해 적지 않으면 다음 사람이 0을 현재값으로 읽는다.

**T-A1 폴백과 T-B8은 이 전제 2개를 반영한다**: (i) `PrologueDone == true`인 세이브에서만
이 밴드를 판정한다. (ii) 행 계수는 `interactable == true`인 행만 센다. 잠김 행이 화면에
보인다는 이유로 계수하면 FAIL 처리 — 그건 밴드를 우회한 것이다.
근거 코드: 시련 행의 `open = data.PrologueDone`이 `_trialButtons[i].interactable`과
`_trialGroups[i].alpha`를 동시에 결정한다(`LobbyView.cs:844`, `:852-853`).

**`trial_visit_rate`는 게이트에 올리지 않는다.** PM이 계약하지 않았고, [TARGET] 0.90은
기준선 없는 희망치로 명문화됐다. 이 사이클에서 이 수를 게이트·측정값 어느 쪽으로도
인용 금지.

**NB2 실패 시 보고 문구 교정 (PM 지적 반영)**: 숙달은 일회성 +2유물이라 도달률 0%여도
손실은 2유물(브래킷 2.8%)로 **해상도 아래**다. 즉 경제 산술이 NB2를 지탱하지 못한다.
T-A5 / `mastery_pointer_coverage`가 실패하면 **"경제 위반"이 아니라 "negotiation
entry 7 계약 조항 도달 불가"로 보고**한다. 근거가 경제가 아니라 계약 무결성이다.

---

### T-B) EditMode로 안 잡히는 것 — 브라우저 스모크 체크리스트

**이 층은 게이트다, 참고가 아니다.** cycle-3 §4b 교훈: "뷰 변경이 있는 사이클은
브라우저 스모크를 게이트 항목으로 승격." 네비게이션은 100% 뷰 변경이다.

공통 절차: 세이브를 주입해 로비를 열고, 항목별 기대 관측과 대조한다. 각 항목은
**스크린샷 1장을 증거로 남긴다**(`qa/` 아래 `.webp`, 기존 관례).

| # | 세이브 상태 | 조작 | 기대 관측 (수치 판정) |
|---|---|---|---|
| **T-B1** | 신규(`PrologueDone=false`) | 로비 진입 | 지목 마커가 화면에 **정확히 1개** 보인다. 0개면 FAIL(안 그려짐 — cycle-3 최빈 결함). 2개 이상이면 FAIL |
| **T-B2** | 위와 동일 | 로비 진입 | 지목 마커가 카드의 스테이지명·상태 문자열을 **한 글자도 가리지 않는다**. 1글자라도 가리면 FAIL. (T-A7이 rect로 잡지만, 렌더 결과는 사람이 본다 — cycle-3 "숙달 라벨이 버튼에 가림"이 정확히 이 유형) |
| **T-B3** | `cleared=7` (다음 목표가 스크롤 밖) | 로비 진입 | 지목 카드가 **뷰포트 안에 완전히** 보인다. 부분 가시(<100%)거나 화면 밖이면 FAIL. 스테이지 7·8·9는 가시 0~11.8%라 자동 스크롤이 없으면 반드시 실패한다 [OBSERVED] |
| **T-B4** | 잠긴 스테이지 존재 | 잠긴 카드 관찰 | 사유 문자열이 렌더된다. 두부(□) **0개**. 1개라도 있으면 FAIL(글리프 미포함) |
| **T-B5** | 위와 동일 | 잠긴 카드 관찰 | 사유 문자열이 **잠금 회색이 아니다**(4.37:1 미달). 회색으로 렌더되면 FAIL |
| **T-B6** | 위와 동일 | 잠긴 카드 관찰 | 사유 문자열이 카드 폭 안에서 **말줄임 없이 완결**되거나, 설계된 축약형이다. 단어 중간 잘림 1건이면 FAIL |
| **T-B7** | `Relics=12`, 장비 T0/T0/T0, `Points=0`, 미보유 각인 존재 | 로비 진입 | SANCTUM 배지가 **장비·각인 2개**에만 보인다. 성장·군단에 배지가 보이면 FAIL. 개수가 2가 아니면 FAIL |
| **T-B8** | 전부 클리어 + 숙달 미수령 (`PrologueDone=true`) | 로비 진입 | 지목이 시련/숙달을 가리키고, **"다음이 없다"로 보이지 않는다**. 지목 0개면 FAIL(완주자가 막힌다). **`interactable == true`인 숙달 경로 행이 기본 스크롤 위치에 ≥1개** 보여야 한다 — alpha 0.45 잠김 행은 0으로 센다(T-A9 전제 2개) |
| **T-B9** | 임의 | 스크롤 위치 관찰 | 스크롤 가능함이 화면에 **표시된다**(현재는 Scrollbar 미할당 = 지시 픽셀 0). 지시자가 0개면 FAIL |
| **T-B10** | 임의 | 390×844 세로 | 지목 마커·배지가 다른 요소와 **겹치지 않는다**. 겹침 1건이면 FAIL |
| **T-B11** | 임의 | 콘솔 관찰 | 로비 진입~탭 4개 순회 동안 콘솔 에러·예외 **0건**. 1건이면 FAIL |
| **T-B12** | 진행 중 상태 | 문자열 조합 관찰 | 지목/사유 문자열에 자리표시자 잔재(`{0}`, `null`, `—  —`, 빈 괄호) **0건**. 1건이면 FAIL. cycle-3의 `— 웨이브 0/0`이 이 유형 |

**T-B의 커버리지 한계를 정직하게 적는다**: 위 12항목은 cycle-3에서 **실제로 발생한
7건의 결함 유형**을 역으로 매핑한 것이다. 새 유형은 여전히 못 잡는다. 이 목록은
"충분하다"가 아니라 "지난번에 우리를 문 것들은 최소한 막는다"이다.

---

### T-C) 계약 위반 후보 — 하나라도 걸리면 S1, 전 게이트 차단

**T-C1) 터치 하한 래칫에 신규 등급 추가**
`Assets/Tests/EditMode/LobbyLayoutTests.cs`의 동결표는 현재 라벨 12종 / 컨트롤
**26개**다(강하 9, 서약 1, 성장 1, 장비 1, 군단 1, 각인 1, `+` 3, 재훈련 1, 견습 1,
숙련 1, 판결 1, 수련 5).
- 판정: 지목 마커·배지가 **버튼이 아니면** 이 표는 안 움직인다. 표 불변이면 PASS.
- 판정: 버튼이면 — 기존 등급(41.0×13.7 또는 그 이상)과 **동일 크기면 허용**,
  **더 좁은 신규 등급이면 FAIL**. v1.6 선례: 8개 컨트롤을 41.0×13.7로 붙여 신규 등급
  0으로 통과. v1.5 반례: 28.3×13.7 티어 버튼 15개가 신규 등급이라 차단됐고, 해결책은
  표 확장이 아니라 **공유 티어 행**이었다(negotiation entry 10).
- 판정: 라벨 키가 12종을 넘으면 **negotiation 기록 없이는 FAIL**.

**T-C1b) 접이식 여유 래칫 — 막 그룹은 스크롤 없이 들어와야 한다 [신규, 착지 후]**

접이식이 (d)를 100%로 만든 근거는 **콘텐츠 416u < 뷰포트 434u** 딱 하나다. 여유가
**18u**뿐이라 이건 성질상 터치 래칫과 같다 — 조용히 넘으면 N1이 되돌아간다.
민감도를 전수로 쟀다(막 그룹 펼침 기준):

| 변경 | 콘텐츠 | 판정 |
|---|---|---|
| 기준 (막 3행 · `CardPitch` 70 · `HeaderPitch` 48) | 416u | OK (여유 18u) |
| **막 그룹 3행 → 4행** | **486u** | **파손** |
| `CardPitch` 76 | 434u | 경계 (딱 맞음) |
| **`CardPitch` ≥ 77** | 437u+ | **파손** — 여유 6u |
| `HeaderPitch` 52 | 432u | 경계 |
| **`HeaderPitch` ≥ 53** | 436u+ | **파손** — 여유 4u |

- 판정: 막 그룹 펼침 시 `_stageContentRect.sizeDelta.y ≤ 434`. 초과면 FAIL.
- 판정: `StagesPerAct`가 3에서 변하면 **자동 FAIL**(4행이면 486u).
- 판정: `CardPitch`·`HeaderPitch` 상향은 각각 76·52가 상한이다. 그 위는 FAIL.
- **cycle-2 선례와 같은 복리**다: 확장이 (d)를 51.2%→41.0%로 떨어뜨린 적이 있는데,
  이번엔 여유가 18u라 **한 번에 깨진다**. 점진적 악화가 아니라 절벽이다.
- 스테이지 추가는 **막을 늘리는 방향으로도 해결되지 않는다** — 아래 전수 결과가 근거다.

**10스테이지 불가 — 막 분할 전수 [OBSERVED, designer 교차검증]**

`그룹 수 = 막 수 + 1`(훈련장이 항상 하나 더 붙는다)을 넣고 막 1~10개 × 균등 분할을
전수로 돌렸다. 펼친 막이 최대 행수를 가진 최악 케이스 기준:

| 막 수 | 최대 행 | 콘텐츠 | 판정 |
|---|---|---|---|
| 3 | 4 | 486u | 파손 |
| 4 | 3 | 466u | 파손 |
| **5** | **2** | **446u** | **파손 — 전 분할 중 최소** |
| 6 | 2 | 496u | 파손 |
| 10 | 1 | 626u | 파손 |

**434u를 만족하는 분할이 0개다.** 최소값이 막 5개일 때 446u로 **12u 초과**다.
내 초판은 "그룹 +1 = 466u"까지만 봤는데 그건 막 4개 케이스 하나였다 — 전수를 돌려야
"배치로 피할 수 없다"가 나온다. designer가 처음에 5그룹 2행 396u로 반례를 잡았다가
**훈련장 그룹 누락**을 스스로 찾아 철회했다.

**초과분이 12u뿐이라는 게 이걸 협상 가능하게 만든다.** 불가 판정으로 끝내지 말고
레버를 같이 올린다 — 뷰포트 예산은 `620(패널) − 174(프롤로그 밴드) − 12(하단 패드) = 434`다
(`:616`/`:693` 패널, `:766` offsetMax, `:765` offsetMin):

| 레버 | 필요값 | 비고 |
|---|---|---|
| **뷰포트 +12u** | 434 → 446 | 프롤로그 밴드 174u를 162u로 줄이면 정확히 충족. **가장 값싼 안** |
| `CardPitch` ↓ | 70 → 64 | 카드 68 + 44 CSS px 터치 하한과 충돌 검토 필요 (T-C1) |
| `HeaderPitch` ↓ | 48 → 46 | 헤더 44u 터치 하한에 2u 남는다 — 여유 없음 |
| 패널 높이 ↑ | 620 → 632 | 로비 세로 예산 재협상 |

**9스테이지를 유지하면 막 3개 3행 = 416u로 여유 18u다.** 즉 현재 구성은 성립하고,
**10번째 스테이지가 임계**다. 콘텐츠 확장 논의가 열리면 이 표를 먼저 올린다.

**하류 반영 확인 [OBSERVED, 2026-08-07].** Main이 세 문서에 반영했다고 통보했고
직접 대조했다 — 전부 일치한다:

| 문서 | 내용 | 확인 |
|---|---|---|
| `qa/gate-measurements.md` g7 | `착지 전 0 → 착지 후 3`, 행별 좌표 명시, 등급선택 포함 근거 | 일치 |
| `design/progression-navigation-spec.md` §2.2 | `4×50 + 3×70 + 6 = 416u` / 훈련장 626u / 현행 1058u | 일치 (spec의 `50`은 `HeaderPitch 48 + GroupGap 2`를 묶은 표기, 산술 동일) |
| `production/gate-reviews/stage1-gates-v17.md` | T-C1b 별도 절 — 416u/18u/446u/12u 초과, 파손점 3종 | 일치 |

**표기가 다르지만 산술이 같은 경우를 "불일치"로 올리지 않았다** — spec의 `4×50`은 내
`4×(HP+GAP)`와 같은 값이다. 이번 사이클에 표기 차이를 오류로 오인한 사례가 두 번 있었으므로
(대칭차 기준선, 70.0% vs 69.3%) 대조 시 **값을 먼저 맞추고 표기는 그다음**으로 봤다.

**T-C2) 골든 이동**
- 판정: `qa/golden-rows-unity.md`의 15행 다이제스트 **완전 무변경**. 1행이라도 움직이면
  FAIL·S1. 네비게이션은 **관측만** 하므로 심 결과가 바뀔 경로가 없다 — 바뀌었다면
  네비게이션이 심을 건드린 것이다.

**T-C3) 심 쓰기**
- 판정: 네비게이션 코드가 `CampaignData`를 **쓰지 않는다**. `ref CampaignData` 인자나
  필드 대입 **1건이면 FAIL**. 지목 계산은 `in CampaignData` 읽기 전용이어야 한다.
- 근거: cycle-3의 "심은 옳고 뷰만 틀렸다"의 대칭 위험. 뷰가 심을 고치기 시작하면
  골든이 조용히 움직인다.

**T-C4) 심 수치 참조**
- 판정: 네비게이션이 `HackConfig`/`HackSpec` 상수를 **재계산하거나 복제하지 않는다**.
  복제 상수 1건이면 FAIL. 기존 관례대로 프로브 프로퍼티를 통해서만 읽는다
  (`LobbyView.cs:258-265` 주석의 "미러 드리프트 구조적 불가" 원칙).

**T-C5) WebGL 예산**
- 판정: 빌드 ≤ 120MB. 현재 57.5MB. 폰트 서브셋 증가분 포함해도 여유가 크므로 이 항목은
  **보고용**이며, 초과 시에만 차단.
- 판정: compute shader / threads 사용 0건. 1건이면 FAIL.

---

### 아키타입 6종과 각각의 실패 모드

G3 요구(≥5)를 만족한다. 각 행의 "무엇이 깨지면 막히나"는 **위 판정 항목과 1:1로 연결**된다.

| # | 아키타입 | 세이브 상태 | 이 기능에서 무엇이 깨지면 막히나 | 연결 판정 |
|---|---|---|---|---|
| **A1** | **신규 (첫 진입)** | `PrologueDone=false`, cleared=0 | **지목이 프롤로그를 1장으로 안 집으면**, 9장 잠긴 카드 앞에서 무엇을 눌러야 할지 모른 채 멈춘다 — 현재 화면은 `"잠김"` 세 글자 외에 아무 안내도 안 준다 | T-A1, T-B1 |
| **A2** | **복귀 (2주 만)** | cleared=4, `Points>0`, `Relics≥2` | **지목이 스테이지만 보고 강화를 안 보면**, 안 쓴 포인트와 유물을 손에 쥔 채 다음 스테이지로 들어가 준비 안 된 상태로 죽는다 | T-A3, T-A4, T-B7 |
| **A3** | **완주자 (9스테이지 클리어)** | cleared=511, 시련 미완 | **지목이 시련/숙달로 폴백 안 하면** 화면이 "끝났다"로 보인다. 시련 5장은 스크롤 최하단(가시 0%)이라 존재 자체가 안 보인다 | T-A1 폴백, T-A5, T-B8 |
| **A4** | **수집가 (각인 전부)** | `Relics` 다량, 장비 혼합 티어, 각인 일부 | **배지가 통화를 구분 못 하면** Points 항목과 Relics 항목이 섞여 지목되고, 오도량이 커지면 배지 자체를 무시하기 시작한다 — 한번 신뢰를 잃으면 켜도 안 본다 | T-A3, T-A4 |
| **A5** | **모바일 세로 (390×844)** | 임의 | **신규 마커/배지가 44 CSS px 미만 신규 등급을 만들면** 래칫이 차단한다. 통과해도 **사유 문자열이 카드 폭을 넘으면** 사유는 있는데 못 읽는 상태가 된다 — ∞를 문자열로 닫았다가 잘림으로 다시 여는 셈 | T-C1, T-B6, T-B10 |
| **A6** | **부분 진행 이탈자** | cleared=6 또는 7 | **자동 스크롤이 없으면** 다음 목표 카드가 완전 가시가 아니다. cleared 6·7·8은 가시 0~11.8%로 [OBSERVED] 확정 — 진행 상태 9가지 중 3가지(33.3%)가 이 함정에 빠진다 | T-B3, T-B9 |

**A6이 가장 위험하다.** 다른 5종은 "설계를 잘못하면" 깨지지만, A6은 **아무것도 안 하면
이미 깨져 있다** — 현재 스크롤 기하가 그 상태고, 지목 기능을 붙여도 자동 스크롤을
같이 안 붙이면 "화면 밖을 가리키는 지목"이 되어 오히려 더 나빠진다. 지목만 있고
스크롤이 없으면 A6은 개선이 아니라 회귀다.

**A6 착지 후 재측정 — 구조적으로 닫혔다 [OBSERVED, 2026-08-07].**
접이식(3막 + 훈련장 4그룹)이 착지했고, `RefreshGroupHeaders`가 `_groupPinned`가 아닐 때
`SelectGroup(GroupOfTarget(target))`로 **지목이 있는 그룹을 자동 펼친다**(`:1020`).
`_groupPinned`는 로비를 나가면 해제된다(`:556`) — 플레이어가 직접 탭한 방문에서만 고정된다.

기하를 다시 쟀다(헤더 피치 48 · 그룹 간격 2 · 카드 피치 70 · 카드 68 · 뷰포트 434):

| 펼친 그룹 | 완전 가시 / 전체 | 완전가시율 | 픽셀 가시율 | 콘텐츠 높이 |
|---|---|---|---|---|
| 1부 재판정 | 7/7 | **100%** | 100% | 416u |
| 2부 증언 | 7/7 | **100%** | 100% | 416u |
| 3부 집행부 | 7/7 | **100%** | 100% | 416u |
| 훈련장 | 7/10 | 70.0% | 69.3% | 626u |
| **착지 전(평면 15행)** | 6/15 | **40.0%** | 41.0% | 1058u |

**콘텐츠 높이 정정 (designer 제보, 재현 확인)**: 내 초판은 414/624였는데 **마지막 그룹
뒤의 `GroupGap`을 빼먹었다**. `cursor += height + GroupGap`이 루프 안이라 4번 더해지고
(`:992`), `:994`가 그 `cursor`를 그대로 `sizeDelta`에 넣는다. **416/626이 맞다.**
결론은 안 바뀐다(416 < 434, 626 > 434).

**70.0% vs designer 69.3%는 오류가 아니라 다른 지표다** — 내 건 **완전 가시 항목 수**
(7/10), designer 건 **픽셀 비**(434/626). 나는 §우리 현재값에서 이미 같은 구분을
했다(40.0% 완전가시 vs 41.0% 픽셀). 여기도 같은 구분이 적용되므로 두 값을 병기한다 —
한쪽을 정정하면 안 된다.

**막 그룹은 어느 것을 펼쳐도 스크롤이 사라진다**(콘텐츠 416u < 뷰포트 434u, 여유 18u).
A6의 cleared 6·7·8은 전부 3부 집행부이고, 그 그룹이 자동으로 펼쳐지며 카드 3장이 100%
가시다. **"화면 밖을 가리키는 지목"이 구조적으로 불가능해졌다** — 내가 A6을 최악으로
꼽은 근거가 해소됐다.

**여유 18u가 얇다는 점은 기록해 둔다.** 막 그룹 본문에 행이 하나라도 늘면(3장 → 4장)
416 + 70 = 486u로 스크롤이 되살아난다. 카드 높이를 68에서 올려도 마찬가지다.
**T-C1의 터치 래칫과 같은 성격의 상한**이고, 협상 안건에 올릴 값이다.

**단 훈련장 그룹은 완전가시 70.0%로 남는다**(6행 626u > 434u, 시련 4·5·6 안 보임). A3 완주자가
거기 해당한다. T-B3은 **훈련장 펼침 상태에서 여전히 유효한 판정**이고, 막 그룹에서는
자동 통과다. 판정을 내리지 말고 대상 그룹을 명시해야 한다.

**designer의 규칙 순서("스크롤 제거가 잠금 사유·공개의 선행 조건")가 여기서 확인된다.**
평면 1058u에 사유 문자열을 먼저 넣었으면 콘텐츠가 더 길어져 (d)가 40% 아래로 떨어졌을
것이다. 접이식이 먼저 들어가 414u를 만들었기 때문에 사유 텍스트가 들어갈 지면이 생겼다.
순서를 뒤집었으면 두 개선이 서로를 상쇄했다.

### 증거 라우팅 (v1.7 추가분)

- `qa/benchmark-notes.md#v17` — 계측 축 정의, 6타이틀 표, 우리 실측(40.0% / 5회 / ∞),
  색 대비 4.37:1
- `qa/gate-measurements.md` — T-A 수치 결과가 들어갈 자리. **벤치마크 (a)(b)(c) 값은
  추정치이므로 이 파일에 측정값으로 인용 금지**
- `qa/golden-rows-unity.md` — T-C2 기준선
- `pm/revenue-map.md` §v1.7 §2 — 확정 밴드 3개의 집행처. `badge_misdirect_relics`
  (max 2) ← T-A4 · `mastery_surface_rows_visible` (min 1) ← T-A1 폴백+T-B8 ·
  `mastery_pointer_coverage` (required) ← T-A5. 결선표는 T-A9
- Unity EditMode 결과 XML — T-A 실행마다 보관
- 브라우저 스모크 `.webp` — T-B 12항목, 항목당 1장

### 미해결 의존성 (v1.7)

- T-A2의 사유 문자열 **문안**은 designer 소관이다. 여기서 잠근 건 "존재할 것, 2사유를
  구분할 것, 선행 스테이지명을 포함할 것"이고 문장 자체가 아니다.
- T-A6은 designer가 사유 텍스트 색을 정해야 확정된다. **잠금 회색을 고르면 자동 FAIL**
  이므로 이 제약을 Stage 2 협상에 먼저 올린다.
- T-C1은 지목 마커가 **버튼인지 장식인지**에 따라 비용이 갈린다. 장식이면 래칫 비용 0.
  버튼이면 41.0×13.7 이상을 강제해야 하고, 그보다 좁으면 v1.5 티어 버튼처럼 설계를
  되돌려야 한다. **가장 값싼 선택지는 "마커는 비인터랙티브"**이며 이 사실을 협상에 올린다.
- T-B3의 자동 스크롤은 designer가 채택해야 존재한다. 미채택이면 T-B3은 **항상 FAIL**
  이므로, 미채택 결정 시 A6을 "알려진 미해결"로 명시 기록해야 한다 — 조용히 빼면 안 된다.
- T-A5는 PM이 `trial_visit_rate`를 구조적 이진 조건으로 낮춰 쓰기로 한 전제에 의존한다.
  진짜 진입률을 원하면 텔레메트리가 선행이고 이번 사이클 범위 밖이다.

### v1.7 레인 종료 — 교차검증이 실제로 잡은 것

세 레인(QA / designer / PM)이 서로의 수를 재검산했고, **결론이 아니라 근거 구조에서**
오류가 나왔다. 다음 사이클에 재사용할 유형 분류다.

**내 자체 정정 10건**

| 유형 | 건수 | 사례 |
|---|---|---|
| 상수·좌표 미확인 | 3 | 기하 708→706(인셋 6과 말미 패드 8 혼동) · `·` 재생성 해소 가능 오판(원본 폰트 미확인) · alpha 0.45 최선색 골드→잉크 |
| 모델을 실동작과 다르게 세움 | 2 | 판 합성(CanvasGroup이 판까지 감쇠) · **대칭차 재계산에서 한글 스윕 누락** |
| 남의 수를 검산 없이 인용 | 1 | designer의 `{§,≥}` |
| 시점 의존 서술을 무시간으로 씀 | 2 | "기존 테스트 초록"(착지 전 기준) · "497 글리프"(낡은 기준선) |
| 단위·집계 혼동 | 2 | "가시 4건" vs "스캐너 3건" · 위반 상태 수 범위 차이 |

**세 레인이 공유한 실패형 하나 더 — "둘 다 맞다"는 성급한 화해.**
designer가 자기 것으로 지적해 왔고, 유형으로 남길 값어치가 있다. 대칭차 건의 전체 경위는
**3단계가 아니라 4단계**였다:

1. 내가 designer의 `{§,≥}`를 검산 없이 인용 (무검산 인용)
2. 내가 틀린 모델로 그걸 "정정" — 한글 스윕 누락 (모델 오류)
3. designer가 "둘 다 맞고 기준선이 달랐다"로 화해 (**성급한 화해**)
4. 내가 한 번 더 파서 진짜 원인 규명 — 재현으로 확정

3단계가 특히 고약하다. **불일치를 마주쳤을 때 "둘 다 맞을 것"이 가장 편한 가설이라
검산 동기를 죽인다.** 그리고 비용이 거기서 끝나지 않는다 — designer 지적:
**화해 가설은 이미 화면에 있는 반증까지 노이즈로 재분류한다.** 이번이 정확히 그랬다.
한글이 구조적으로 대칭차에 기여할 수 없다는 건 생성기 코드만 보면 즉시 나오는데,
내 값에 **한글 22자가 섞여 있는 것**을 양쪽 다 보고도 "기준선이 다른가 보다"로 넘겼다.
그 22자가 모델 오류의 직접 증거였고 표를 만들기 전부터 화면에 있었다.
실제로 4변형을 다 돌려보면 화해가 성립하지 않는다:

| 변형 | \|W\| | \|L\| | 대칭차 | 그중 한글 | 유효 기준선인가 |
|---|---|---|---|---|---|
| 생성기 완전 재현 (base+인용문+한글) | 500 | 498 | **2** | 0 | **예** — designer 값 |
| base 미주입 | 491 | 483 | 8 | 0 | 예 — 다른 기준선이라 부를 수 있음 |
| 한글 스윕 누락 | 470 | 488 | 24 | 22 | 아니오 |
| base+한글 둘 다 누락 | 461 | 473 | **30** | 22 | **아니오** — 내 오류값 |

`base 미주입`은 실제로 다른 기준선이다(대칭차 8, 한글 0). 그러나 **내 30은 base와 한글이
둘 다 빠진 조합이라 어느 정의로도 생성기를 재현하지 않는다.** 한글이 대칭차에 기여하는
것 자체가 불가능한데(모드 무관 전체 스윕) 내 값에는 22자가 섞여 있었다 — 그게 모델이
틀렸다는 지표였고, "기준선 차이"로는 설명되지 않는다.

**이 유형은 이번 사이클 축의 3인칭 버전이다.** PM의 "같은 방향을 가리키는 것 ≠ 같은 것을
증명하는 것", designer의 "빈도표는 방향만 산출하고 증명은 못 한다"와 같은 계열인데,
화해는 그걸 **관찰자 위치에서** 저지른다 — 두 값이 같은 대상을 가리킨다고 가정하고
차이를 기준선 탓으로 돌린다. 셋 다 **차이의 원인을 재현으로 확정하지 않은 것**이 공통이다.

처방은 하나다: **불일치는 화해시키지 말고 재현한다.** 변형을 전수로 돌리면 어느 조합이
실동작인지 기계가 답한다. 이번엔 4변형 중 2개만이 유효 기준선이었고, 그 사실은 표를
만들기 전에는 아무도 몰랐다.

**내가 잡은 것**: PM 4건(색 귀속 / 밴드 alpha 우회 / 제3 해소경로 / 상보관계 오약) ·
designer 3건(재생성 유일경로 오류 / 처방 절반 / **성급한 화해**) · 기존 테스트 거짓 주석 1건.
**남이 잡은 내 것**: PM 3건(판 합성 / T-A4b 사각 / 상한 서명 전제) · designer 2건
(정규식 전체파일 스캔 / **내 "정정"이 틀렸음**).

대칭차 한 건에서만 **양쪽이 번갈아 3번 틀렸다**(내 무검산 인용 → 내 모델 오류 → designer
화해). 결론은 처음부터 끝까지 같았다(`대칭차 ∩ 폰트 누락 = ∅`). **결론이 안정적이라는
것이 근거가 건전하다는 뜻이 아니라는 사례**로 이게 이번 사이클 최고의 표본이다.

**가장 위험했던 유형은 "같은 방향"을 "같은 증명"으로 요약한 것**이다(PM 2건, 나 1건).
셋 다 결론은 맞았는데 근거 구조가 틀렸고, 그러면 **다른 조건에서 같은 결론을 재사용할 때
깨진다**. 구체적으로:

- 내 T-A4b(변이 검정)와 PM 포화 산술은 "같은 방향"이 아니라 **상보**다 — 각각 게이트A/
  게이트B만 잡고 서로를 대체 못 한다. 보강 관계로 적었으면 하나만 돌리고 안심했을 것이다.
- designer의 빈도표는 **방향만 산출하고 증명은 못 한다**(N2 0/6이 "하지 마라"가 아니라
  "가드 없이 하지 마라"인 이유). PM의 산술은 **부호를 주지만 크기를 못 준다**.
  내 계측은 **크기를 주지만 무엇을 채택할지는 못 정한다**. 세 레인이 각자 이 축에 부딪쳤다.

**마지막 두 라운드가 둘 다 "형식은 맞는데 사실 확인을 안 한" 유형이었다.** 나는 "상한이
서명돼야 판정 가능"이라고 형식적으로 옳게 판단하고 `pm/reward-bands.md`를 안 열었고,
PM은 두 근거가 같은 결론을 낸다고 형식적으로 옳게 보고 두 실패 모드를 대조하지 않았다.
둘 다 **파일 한 번 열기 또는 스윕 한 번**으로 잡히는 것이었다.

**Stage 1b 이후로 넘기는 것**

- T-A/T-B/T-C 테스트 **코드 작성은 별도 레인**(Main 지시). 이 문서는 판정 정의까지다.
- 게이트 전환 협상 안건 3개(①변이 통과 ②도달성 ③문턱 상수 금지) — 배지 경로 확정으로
  당장은 비활성이나, 전환 논의가 재개되면 ③이 선결이다.
- T-B 12항목은 **뷰 변경이 있는 모든 사이클에서 게이트**다(cycle-3 §4b 교훈의 집행).
  이번에 T-A6·T-A4가 착지 코드에서 통과한 것은 T-B를 면제하지 않는다 — 렌더·겹침·잘림은
  여전히 사람이 화면을 봐야 보인다.

## v1.8 — 인게임 안내 시험 계획 (run-id 20260807-ingame-guidance)

2026-08-07 · game-qa. Stage 1a 산출물. **계획만이고 테스트 코드는 아직 안 쓴다.**

**용어**: 이 절은 확정 스펙의 어휘를 그대로 쓴다 — **정지 카드**(8종), **토스트**(15종),
**도감**, **이탈 모달**, **조우 비트**(23비트). 최종 명칭은 designer 소관이고 여기서
고정하는 건 이름이 아니라 **판정 수치**다.

### 왜 또 3층인가 — 이번엔 근거가 하나 더 있다

안내는 **뷰 레인**이다. cycle-3 회고 §4b: EditMode 319/319 초록·골든 무이동·빌드
errors 0 상태에서 브라우저가 7건을 더 잡았고 **전부 뷰 레인**이었다.

이번 사이클은 그 위에 증거가 하나 더 쌓였다. **§4 전수 감사에서 D2와 같은 유형의
겹침을 8개 더 찾았고, 그중 어느 것도 현재 테스트가 못 잡는다.** 즉 "EditMode가 못
잡는다"는 이제 회고 인용이 아니라 **이번 조사의 실측**이다. T-A를 설계할 때 이
8건이 통과하는 T-A는 실패한 T-A라는 기준을 쓴다.

---

### T-A) EditMode로 잡히는 것 — 순수 함수·rect·글리프·산술

안내 항목표·조우 비트·정지 판정은 전부 데이터와 순수 함수로 설계 가능하다. 순수
함수인 한 EditMode가 전수로 돌린다. 이 층의 판정은 전부 **정수 비교 또는 부동소수 비교**다.

**T-A1) 커버리지 전수 — 23종 누락 0**
- 판정: `GuidanceEntry` 인스턴스 수 **== 23**. 22 이하 또는 24 이상이면 FAIL.
- 판정: 23개 id 집합이 실제 열거형과 **정확히 일치**. 대응 없는 id 1건이면 FAIL.
  - 조작 9: `InputAdapter` 표면(이동·Space·Shift·Q·E·R·F·G·H)
  - 아이템 4: `PickupKind` 전 값 (EmberShard/OilFlask/RelicMote/EquipShard)
  - 기믹 6 / 승패 2 / 돌발 2
- 판정: 각 열거형을 **리플렉션 또는 전 값 순회**로 돌려 항목 없는 값 == 0.
  **하드코딩된 23 리스트와 비교하면 안 된다** — 그러면 열거형이 늘어도 초록이다.
  벤치마크 §4가 지적한 "분모 23은 작아서 쉽게 깨진다"가 이 판정의 존재 이유다.
- 판정: 신규 열거값 추가 시 이 테스트가 **자동으로 빨강**이 되어야 한다. 회귀 검사:
  테스트 코드에 `23`이라는 리터럴이 **분모로 쓰이면 FAIL**(정의상 자기충족).

**T-A2) 중복 0 — 같은 항목이 두 번 정지시키지 않는다**
- 판정: 23비트 마스크에서 비트 i가 1이면 항목 i의 정지 트리거가 **false**를 반환.
  전 23비트 × {0,1} = **46 케이스 전수**. 1건이라도 true면 FAIL.
- 판정: 정지 발생 시 해당 비트가 **정확히 1개** 세워진다. 0개 또는 2개 이상이면 FAIL.
- 판정: **비트마스크 라운드트립** — 임의 마스크 m(0 ≤ m < 2²³)에 대해
  `Decode(Encode(m)) == m`. 2²³ = 8,388,608 전수는 과하므로
  **경계 + 무작위 표본**으로 한다: {0, 1, 2²², 2²³−1} ∪ {고정 시드 무작위 10,000}.
  1건이라도 불일치면 FAIL.
- 판정: 23비트가 **int(32비트) 범위 안**. `m < (1 << 23)` 위반 0건.
  24번째 비트가 세워지면 FAIL — 항목이 늘었는데 저장 폭을 안 늘린 상태다.

**T-A3) 정지 대상은 정확히 8종**
- 판정: `tier == pause`인 항목 수 **== 8**. 7 이하 또는 9 이상이면 FAIL.
- 판정: 그 8종의 id 집합 == {기믹 6} ∪ {승리, 패배}. 집합 불일치 1건이면 FAIL.
- 판정: `tier == toast`인 항목 수 **== 15**. 8 + 15 == 23 항등 검사.

**T-A4) 한 스테이지 정지 상한 ≤ 4회**
- 판정: 9스테이지 각각에 대해, **조우 비트 전부 0**인 최악 상태에서 그 스테이지가
  띄울 수 있는 정지 수 ≤ **4**. 5 이상이면 FAIL.
- 계산 방법: 스테이지의 기믹 배치(`CampaignStages`)에서 기믹 종류 수 + 승패 2.
  승리·패배는 배타적이므로 **둘 다 세지 않고 1로 센다** — 한 번의 스테이지 진입에서
  이기면서 동시에 지지 않는다. 즉 상한 = 그 스테이지 고유 기믹 수 + 1.
  **기믹 4종 이상 배치된 스테이지가 있으면 즉시 FAIL**이고, 그건 안내 결함이 아니라
  스테이지 구성이 예산을 넘긴 것이다.
- 판정: 9스테이지 전부 통과. 1개라도 초과면 FAIL.

**T-A5) 이탈 정산 — 유물 증가 0 · ClearedMask 불변**
- 판정: 이탈 경로 실행 전후로 `CampaignData.Relics` 차이 **== 0**. 양수면 FAIL.
- 판정: `ClearedMask` 비트 단위 **불변**. 1비트라도 바뀌면 FAIL.
- 판정: 조우 비트는 **바뀌어도 된다**(본 것은 본 것). 단 **감소는 FAIL** —
  단조 증가만 허용.
- 대조군: 패배 경로는 `GameDirector.cs:614-626`대로 **유물이 증가한다**.
  같은 테스트에서 패배 경로를 돌려 **증가 > 0**임을 확인. 증가가 0이면 FAIL —
  이탈 구현이 패배 경로까지 망가뜨린 것이다. 이 대조군이 없으면 "둘 다 0"인
  회귀가 초록으로 통과한다.

**T-A6) rect 겹침 0 — §4가 이 판정의 사양이다**
- 판정: 신규 안내 rect(정지 카드·토스트·도감·이탈 모달)와 **기존 활성 rect**의
  겹침 면적 > 1u²인 쌍의 수 **== 0**. 1쌍이면 FAIL. (`OverlapEpsilon = 1f` 관례 유지)
- **판정 확장 — 이게 §4의 집행이다.** 기존 `HudLayoutTests.AssertNoPairwiseOverlap`은
  **인터랙티브 × 인터랙티브만** 본다. 안내 rect는 다음 **4조합 전부**를 검사한다:

  | 조합 | 기존 테스트가 보는가 | 왜 필요한가 |
  |---|---|---|
  | 인터랙티브 × 인터랙티브 | **예** | — |
  | 인터랙티브 × 텍스트 | **아니오** | **D2가 정확히 이것** (버튼이 사망 사유 밑) |
  | 텍스트 × 텍스트 | **아니오** | §4 신규 D (surge × wave 배너) |
  | 불투명 판 × 임의 rect | **아니오** | §4 신규 A·B (성장 판이 토스트·스킬행 매장) |

- 판정: 겹침 검사에 **알파 ≥ 0.5인 Image를 불투명 판으로 취급**하고 그 아래 rect를
  위반으로 센다. 성장 판 alpha 0.88이 이 기준에 걸린다.
- 판정: 모든 신규 rect의 `width > 0 && height > 0`. 0이면 FAIL(레이아웃 미해결).
- 판정: 신규 rect가 **부모 판 경계를 벗어난 면적 == 0**. §4 신규 E(성장 제목이 자기
  판 밖으로 16u)가 이 판정의 사례다. 1u² 넘으면 FAIL.
- 판정: **4조합 × 4구성**(Phone/Full × touch on/off)에서 전부 0. §4가 보여주듯
  구성마다 결과가 다르다 — 한 구성만 재면 놓친다.

**T-A7) 글리프 누락 0**
- 절차: 신규 한국어 문자열 추가 후 `bash tools/gen_hud_font.sh` 재실행.
- 판정: `coverage: FULL`. 누락 문자 ≥ 1이면 FAIL.
- 판정: **`·`(U+00B7) 사용 0건, `−`(U+2212) 사용 0건.** 1건이면 FAIL.
  둘 다 소스 폰트에 없고 `−`는 **스캐너가 못 잡는다**(v1.7 T-A8 실측: 따옴표 짝짓기에
  먹혀 `coverage: FULL` 초록인데 화면엔 두부). 안내 문안은 23종 × 2(데스크톱/터치)
  = **최대 46개 신규 문자열**이라 기존 최대 유입원이 된다. `•`(U+2022)와 ASCII `-`만 쓴다.
- 판정: 글리프 수가 변했으면 `HudKorean.otf` 재생성이 **동반**. 문자열만 늘고 폰트가
  그대로면 FAIL.

**T-A8) 대비 ≥ 4.5:1 (합성 후)**
- 판정: 안내 본문 전경색과 실제 배경의 WCAG 상대휘도 대비 **≥ 4.5**. 미만이면 FAIL.
- 통과 색 [OBSERVED, benchmark v1.7]: 골드 12.20 / 시안 7.84 / 엠버 6.11 / InkDim 8.69.
  **잠금 회색 4.38은 미달** — 안내 본문에 쓰면 즉시 빨강.
- 판정: **`CanvasGroup.alpha` 합성 후**로 잰다. **alpha 0.45 뒤에서는 어떤 색도
  4.5를 못 넘는다**(골드 3.07이 최선) [OBSERVED]. 따라서 안내 텍스트를
  **alpha 감쇠 대상에 넣으면 색과 무관하게 FAIL**이다. 이건 색 선택이 아니라
  **구조 제약**이므로 designer에게 "감쇠 밖에 두라"로 전달한다.
- 판정: 정지 카드가 배경을 어둡게 깔면(블로커) **그 위에서 재계산**. 블로커가
  대비를 올리는 방향이면 통과, 내리면 FAIL.

**T-A9) 터치 패리티 — 조작 안내 문안이 양쪽 존재**
- 판정: 조작 9종 각각 `body`와 `touchBody`가 **둘 다 비어 있지 않다**. 1건이라도
  길이 0이면 FAIL.
- 판정: `body != touchBody`. 같으면 FAIL — 터치 문안을 안 쓰고 복사한 것이다.
  (선례: `DesktopPrologueSteps`는 `W A S D`, `TouchPrologueSteps`는 `왼쪽 조이스틱 드래그`)
- 판정: 아이템·기믹·승패·돌발 13종은 입력과 무관하므로 **`touchBody`가 비어도 통과**.
  단 비었으면 표시 시 `body`로 폴백하는지 확인 — 폴백이 빈 문자열을 그리면 FAIL.

---

### T-B) EditMode가 못 잡는 것 — 브라우저 스모크 체크리스트

cycle-3 §4b 7건의 유형이 그대로 이 기능의 위험 목록이다. **항목별 기대 관측을 수치로
명시**한다 — "잘 보인다"는 판정이 아니다. **12항목.**

| # | 항목 | 절차 | 기대 관측 (수치) | 실패 신호 |
|---|---|---|---|---|
| **T-B1** | 정지 카드가 실제로 그려지는가 | 첫 강하, 기믹 최초 조우 | 카드가 화면에 **1개** 보인다. 0개면 FAIL | 안 그려짐 / 캔버스 밖 (cycle-3 유형 1·2) |
| **T-B2** | 정지가 정말 멈추는가 | 정지 중 5초 대기 | 적 위치·체력·기름 수치 **변화 0**. 1픽셀이라도 움직이면 FAIL | `timeScale=0` 미적용 |
| **T-B3** | 정지 해제 후 복귀 | 카드 닫고 3초 관찰 | 적이 다시 움직이고 `timeScale == 1`. 느리면(0.2 등) FAIL | **T-C5와 같은 결함** |
| **T-B4** | 문자열 잘림 | 23종 카드·토스트 전부 표시 | 말줄임·잘림 **0건**. 카드 폭 밖으로 나간 글자 0 | `HorizontalWrapMode.Overflow`라 **잘리는 대신 삐져나온다** — 육안 필수 |
| **T-B5** | 두부(글리프 누락) | 위와 동일 | `□` 문자 **0개**. 1개면 FAIL | T-A7이 `−`를 못 잡으므로 육안이 최종 게이트 |
| **T-B6** | 터치 모드 문안 전환 | 터치 기기(또는 강제 터치)로 조작 9종 열람 | 9종 전부 터치 문안. `W A S D`가 1건이라도 보이면 FAIL | 심은 옳고 뷰만 틀림 (cycle-3 유형 5) |
| **T-B7** | 도감 스크롤 | 로비 성소·인게임 정지 양쪽에서 도감 열고 끝까지 스크롤 | 23항목 전부 도달. 마지막 항목이 안 나오면 FAIL. **스크롤 가능함이 화면에 표시되는가**도 기록 | v1.7 (d) 축과 같은 구멍 |
| **T-B8** | 이탈 모달 상호작용 | 전투 중 이탈 → 취소 → 다시 이탈 → 확인 | 취소 시 전투 **계속**, 확인 시 로비 도착. 취소가 나가버리면 FAIL | 확인 모달의 존재 이유 |
| **T-B9** | 이탈 후 유물 화면 | 이탈 확인 후 로비 유물 수 확인 | 이탈 **전과 동일한 수**. 늘면 FAIL(T-A5의 육안 확인) | 심/뷰 불일치 |
| **T-B10** | **레벨업 동시 표시** | 던전에서 레벨업 발생시키고 5초 관찰 | 성장 선택 문구와 레벨업 토스트가 **둘 다 읽힌다**. 하나가 안 보이면 FAIL | **§4 신규 A — 현재 이미 FAIL이다** |
| **T-B11** | **위기/기세 + 웨이브 동시** | 체력 35% 미만에서 웨이브 클리어 | `위기 N.N`과 `웨이브 N`이 **둘 다 읽힌다**. 겹쳐서 하나가 뭉개지면 FAIL | **§4 신규 D — 현재 이미 FAIL이다** |
| **T-B12** | **성장 패널 × 스킬행** | 터치 가로(권장 구성)에서 레벨업 | 스킬 카드 4장 + 대시 카드의 **아이콘·키캡이 가려지지 않는다** | **§4 신규 B1 — 현재 이미 FAIL이다** |

**T-B10·11·12는 신규 기능 검사가 아니라 현행 결함 재현 절차다.** §4가 산술로 찾았고
브라우저가 확인해야 등급이 확정된다. 안내 기능이 같은 좌표대에 카드·토스트를 얹으므로
**고치지 않으면 안내가 그 위에 또 겹친다.**

**T-B는 뷰 변경이 있는 모든 사이클에서 게이트다** (cycle-3 §4b 교훈의 집행).
T-A 전부 초록이어도 T-B를 면제하지 않는다.

---

### T-C) 계약 위반 후보 — 하나라도 걸리면 착지 불가

| # | 계약 | 판정 | 현재 여유 |
|---|---|---|---|
| **T-C1** | **골든 7스위트 무이동** | 7스위트 다이제스트 **완전 일치**. 1비트라도 다르면 FAIL | 정지는 `timeScale`만 건드리므로 안전 — 단 T-C2가 전제 |
| **T-C2** | **심 쓰기 0** | 안내 코드가 `CinderSim` 상태를 쓰는 호출 **0건**. 1건이면 FAIL | `HudView`/`GameView` 내부로 한정. 조우 비트는 `CampaignData`(세이브)이지 심이 아니다 |
| **T-C3** | **터치 래칫 신규 등급 0** | `LobbyLayoutTests` 동결표에 **새 최소 크기 등급 추가 0**. 최협 25.4 / 최단 13.7 유지 | 정지 카드 버튼은 **≥ 90u**(44 CSS px ÷ 0.488)여야 새 등급을 안 만든다 |
| **T-C4** | **WebGL ≤ 120MB** | 빌드 크기 ≤ 120MB | 현재 38MB. 여유 82MB. 폰트 서브셋 증가분만 실질 위험 — **낮음** |
| **T-C5** | **정지 중 timeScale 복원 누락** | 아래 5판정 전부 통과 | **최고 위험. 아래 상술** |

#### T-C5 상술 — 이게 제일 위험하고, 선례가 그걸 증명한다

**구조 [OBSERVED]**: `Time.timeScale`은 `GameView`가 **독점 소유**한다.
`HudView`는 `CommandConsoleOpen`(bool)만 노출하고 `GameView.ApplyTimeScale()`이
매 프레임 읽어 반영한다(`GameView.cs:298`, `:303`, `:307`). **안내도 같은 심을
써야 한다 — 정지 카드가 `Time.timeScale`을 직접 쓰면 그 자체로 FAIL이다.**

**함정 [OBSERVED]**: `ApplyTimeScale()`은 매 프레임 `target`을 **재계산**한다.
`_hitStopTimer`·`_slowMoTimer`는 GameOver에서 0으로 지워지지만(`:354-355`)
**`consoleOpen`은 안 지워진다.** 그래서 `GameOver`가 `Time.timeScale = 1f`로
복원해도(`:356`) **다음 프레임에 `ApplyTimeScale()`이 0.2로 되돌린다.**

이 함정은 실재했고 이미 막혀 있다 — `HudView.cs:904-907`:
> *"Trap guard: a run can end while the console is open (death/clear).
> Without this, CommandConsoleOpen pins timeScale at 0.2..."*

`ResetRunUi()`가 `CloseCommandConsole(submit: false)`를 부르는 게 유일한 방어다.

**정지 카드는 같은 함정에 더 나쁘게 빠진다:**

| | 콘솔 (기존) | 정지 카드 (설계안) |
|---|---|---|
| 고정되는 값 | **0.2** | **0** |
| 결과 | 느리지만 진행됨. 재강하 버튼 도달 가능 | **완전 정지. 조작 불가** |
| `Time.deltaTime` | 0.2배 | **0** |
| 해제 애니메이션 | 느리게 진행 | **영원히 진행 안 함** |
| 복구 가능성 | 있음 | **없음 — 새로고침만** |

**0.2는 느리기라도 하지만 0은 하드 프리즈다.** 그리고 `Time.deltaTime == 0`이면
`_levelToastTimer`·`_waveBannerTimer`·`_bossRevealTimer` 같은 **스케일드 타이머가
전부 멈춘다**(`HudView.cs:2410`, `:2426`, `:2577`). 정지 카드의 해제가 스케일드
타이머에 의존하면 **자기 자신을 못 닫는다.**

**판정 5개:**
- **T-C5a** 정지 카드가 `Time.timeScale`에 **직접 대입 0건**. 1건이면 FAIL.
  `GameView.ApplyTimeScale()`을 유일 기록자로 유지한다.
- **T-C5b** `ResetRunUi()`가 **열린 정지 카드를 닫는다**. 런 종료(사망/클리어) 후
  카드 플래그 == false. true면 FAIL. (`CloseCommandConsole` 선례와 같은 자리)
- **T-C5c** 런 종료 후 `Time.timeScale == 1f`를 **다음 프레임에도** 유지.
  1프레임만 재고 끝내면 이 결함을 놓친다 — **최소 3프레임 관측**.
- **T-C5d** `GameView.OnDisable()` 경로에서 정지 카드가 열려 있어도 `timeScale == 1f`
  (`GameView.cs:243` 선례). 아니면 FAIL.
- **T-C5e** 정지 카드의 해제 타이머·애니메이션이 **`Time.unscaledDeltaTime`을 쓴다**.
  `Time.deltaTime`을 쓰면 `timeScale == 0`에서 영원히 안 끝나므로 FAIL.
  선례: `ApplyTimeScale`의 회복 감쇠가 이미 `unscaledDeltaTime`을 쓴다(`:306`).

**판정: T-C5는 실현 가능하되 방어가 필수다.** 콘솔이 같은 함정을 이미 밟았고
가드 한 줄로 막혀 있다는 사실이 양방향 증거다 — **막을 수 있다는 증거이자,
안 막으면 반드시 발생한다는 증거**다. T-C5b가 없으면 사망 시 하드 프리즈다.

---

### 아키타입 — 안내는 유형마다 다르게 깨진다

**6유형.** 각각 **이 기능에서 무엇이 깨지면 그 유형이 막히는지** 1줄.

| # | 아키타입 | 이 기능에서 막히는 지점 |
|---|---|---|
| **A1** | **완전 신규** (로그라이트 첫 경험) | 커버리지가 100% 미만이면 설명 안 된 메커니즘에서 죽고 이유를 모른다 — **T-A1이 유일한 방어**이고, 23종 중 1종만 빠져도 그게 하필 자기가 만난 것일 수 있다 |
| **A2** | **다른 로그라이트 경험자** (안내가 귀찮음) | 정지 8회가 전부 길면 총량이 ItB 2분을 넘어 이탈한다 — **G2 33어절 상한**이 방어선이고, 스킵/거절 경로가 없으면 Gungeon·CotL형 혹평으로 간다 |
| **A3** | **복귀자** (예전에 봤는데 잊음) | 조우 비트가 이미 1이라 정지가 안 뜨는데 **도감 없이는 다시 볼 방법이 0** — T-B7(도감 양쪽)이 깨지면 이 유형만 정확히 막힌다. 최초 1회 설계의 유일한 출구다 |
| **A4** | **모바일 세로** (390×844, Phone tier) | 정지 카드 버튼이 90u 미만이면 못 누른다(T-C3) — 그리고 **§4 신규 B2가 이미 이 구성에서 성장 판이 스킬 4장을 42u 덮고 있다**. 안내 카드가 같은 대역에 오면 누적된다 |
| **A5** | **모바일 가로** (게임이 권장하는 구성) | `ShowRotateHintIfPortrait`가 이쪽으로 유도하는데 **§4 신규 B1이 가장 심한 구성이 정확히 여기다**(스킬 4 + 대시가 62u씩 매장). 안내를 얹기 전에 이미 깨져 있다 |
| **A6** | **실수 이탈자** | 이탈 모달이 없거나 버튼이 겹치면 유물 몰수가 오조작으로 발생 — **StS도 모달이 있는데 인접 배치 때문에 오조작 제보가 난다** [indexed snippet]. 모달 존재(T-B8)와 **버튼 겹침 0**(T-A6)이 둘 다 필요하고, D2가 정확히 후자의 실패다 |

**A4·A5·A6이 §4와 직접 연결된다.** 아키타입 6개 중 3개가 이번 겹침 감사에서
나온 좌표에 걸린다 — 안내를 얹기 전에 그 대역이 이미 오염돼 있다는 뜻이다.

---

### §4 특별 조사 — D2 겹침이 왜 통과했는가, 그리고 같은 유형이 8개 더 있다

#### 4a. 왜 못 잡았는가 — 구조적 이유 3개

`Assets/Tests/EditMode/HudLayoutTests.cs`를 읽었다. D2가 통과한 이유는 버그가
아니라 **테스트 계약의 정의역**이다. 독립적인 구멍이 **3개** 있고, D2는 **3개 전부**를
통과한다 — 하나만 막아도 안 잡힌다.

**구멍 ①: 겹침 그래프에 텍스트가 없다.**
`InteractiveRects()`(`:121-130`)는 `IPointerDownHandler`를 구현한 컴포넌트만 모은다.
`Label()`은 `raycastTarget = false`(`HudView.cs:1907`)이고 포인터 핸들러가 없으므로
**집합에 애초에 안 들어간다.** `AssertNoPairwiseOverlap`(`:146-167`)은 그 집합의
**내부 쌍만** 순회한다. 즉 검사되는 건 **인터랙티브 × 인터랙티브**뿐이고,
D2는 **인터랙티브 × 텍스트**다. 정의역 밖이라 초록이다.

**구멍 ②: 사망 패널이 어떤 겹침 테스트에서도 활성이 아니다.**
겹침을 보는 테스트는 `PhoneDungeon_InteractiveSurfaces_DoNotOverlap`(`:285`)과
`PhoneArena_...`(`:303`)인데 둘 다 `ArrangePhone()`만 부르고 **`GameOver`를 안 쏜다**.
`_gameOverPanel`은 `SetActive(false)`(`HudView.cs:283`)로 만들어지고
`InteractiveRects`는 `GetComponentsInChildren<IPointerDownHandler>(**false**)` —
**비활성 제외**다. 사망 패널의 버튼 2개는 rect 목록에 **한 번도 들어간 적이 없다.**

반대로 `GameOver`를 쏘는 테스트(`:412` `RetryModalVisible_...`, `:432`
`GameOver_HidesCombatTouchTargets_...`)는 **활성 상태·입력 초기화만 보고 rect를 안 잰다.**
**겹침을 보는 테스트는 패널을 안 켜고, 패널을 켜는 테스트는 겹침을 안 본다.**

**구멍 ③: `AssertSkillRowClearOfReadouts`는 일반 규칙이 아니라 특례다.**
구멍 ①을 아는 사람이 있었다 — `:317-321` 주석이 정확히 그렇게 적혀 있다:
*"InteractiveRects() only collects pointer handlers, so the skill row could bury
non-interactive readouts ... while every existing test stayed green"*.
그런데 그 대응이 **범용 검사가 아니라 스킬행 × 리드아웃 한 쌍의 전용 검사**다.
`CollectDungeonReadoutRectsForTest`(`HudView.cs:2012-2020`)의 멤버는 **5종 고정**
(xp바·레벨·콤보핍·실드·스피커). **사망 사유 텍스트도, 레벨 토스트도, 성장 패널도,
배너도 이 목록에 없다.**

**즉 §U1에서 같은 유형을 한 번 겪고도 일반화하지 않고 그 인스턴스만 막았다.**
이번에 8건이 더 나온 이유가 이거다.

#### 4b. 전수 감사 결과 — **추가 겹침 8건 확정 + 2건 조건부**

소스에서 rect 좌표를 읽어 **산술로** 계산했다. 모델은 Unity uGUI 규칙 그대로:
`Panel(anchorMin==anchorMax=A, size S, anchored P)` → `pivot = A`(`HudView.cs:1867`);
`Label(parent,x,y,w,h)` → `anchor = pivot = (0,1)`, 부모 좌상단 기준(`:1909-1913`).

**모델 검증**: 이 모델로 D2를 계산하면 `finalText y ∈ [-20,40]` × `캠페인으로 y ∈ [-34,6]`
→ 세로 겹침 **26u**. 확정 스펙의 실측 26u와 **정확히 일치**한다. 모델이 맞다는 뜻이므로
아래 값들도 같은 신뢰도다. [OBSERVED — 산술]

| # | 겹치는 쌍 | 발생 상황 | 교차 (u) | 면적 (u²) | 등급 |
|---|---|---|---|---|---|
| **D2** | `finalText`(279) × `캠페인으로`(655) | 사망 | 200 × 26 | 5,200 | 기존 |
| **A** | `levelToast`(296) × `growthPlate`(1136) | 레벨업 | 440 × 34 | **14,960** | **신규** |
| **B1** | `growthPlate` × 스킬4 + 대시 | 레벨업 · **Full+touch** | 96 × 62 ×5 | **27,528** | **신규** |
| **B2** | `growthPlate` × 스킬4 | 레벨업 · Phone, touch off | 92 × 42 ×4 | 15,456 | **신규** |
| **B3** | `growthPlate` × 대시 | 레벨업 · Phone+touch | 96 × 62 | 5,952 | **신규** |
| **C** | `levelToast` × 스킬행 | 레벨업 · Full+touch | 96 × 34 ×5 | 15,776 | **신규** |
| **D** | `surgeBanner`(1670) × `waveBanner`(288) | 위기·기세 + 웨이브 | 300 × 30 | 9,000 | **신규** |
| **E** | `growthTitle`(1139)이 자기 판 이탈 | 레벨업 | 440 × 16 | 7,040 | **신규** |
| **F** | `chargeGauge`(1098) × 스킬행 | 던전 · Phone, touch off | 92 × 4 ×2 | 880 | **신규**(경미) |
| **G** | `consoleRoot`(1179) × 스킬행 | 콘솔 열림 · Phone+touch | 92 × 12 ×4 | 4,212 | 조건부 |
| **H** | `prologueToast`(1070) × `trialBanner`(1681) | 회전힌트 + 훈련 세로 | 300 × 14 | 4,200 | 조건부 |

**확정 8건(D2 제외) · 조건부 2건. 확정 교차 면적 합계 101,792 u² — D2의 19.6배다.**

**깨끗한 것도 기록한다**: **스테이지 클리어 패널 0건**, **엠버 레스트 패널 0건**.
이 둘이 깨끗한 게 원인 규명의 열쇠다(§4c).

#### 4c. 가장 심각한 3건 상술

**A — 레벨업 토스트가 100% 매장된다. D2보다 나쁘다.**
```
levelToast   x=[-240,240]  y=[170,204]   (480×34, alpha 애니메이션, 배경 없음)
growthPlate  x=[-220,220]  y=[150,212]   (440×62, alpha 0.88 불투명)
```
토스트의 **세로 34u 전부가** 판의 150–212 안에 있다. 가로도 텍스트가
`MiddleCenter` 정렬이라 실제 글자는 x≈0 근처 — 판 안이다.
**렌더 순서**: `_levelToast`는 `Build()`(`:296`)에서, `_growthPanel`은
`SyncGrowthOffer()`(`:1136`)에서 **첫 레벨업 때 지연 생성**된다. 같은 `_safeRoot`
아래 **나중 형제**라서 위에 그려진다. 즉 **불투명 판이 토스트를 완전히 덮는다.**

**동시 표시 확정** [OBSERVED]: `CinderSim.GainXp()`가 같은 프레임에
`_events |= SimEvents.LevelUp`(`:1215`)과 `_growthOfferOpen = true`(`:1228`)를 세운다.
토스트 1.4초(`HudView.cs:1704`), 성장 오퍼 5초(`HackSpec.GrowthOfferSeconds`).
**토스트의 생애 1.4초가 오퍼 5초 안에 완전히 포함된다.** 우연히 겹치는 게 아니라
**항상 겹친다.**

잃는 정보: `"레벨 업! 피해 +4% • 최대 체력 +6"` — **레벨업이 무엇을 줬는지를 말하는
유일한 문자열**이다. 남는 건 `"레벨 업 — 강화 선택 (5)"`뿐이라 **자동으로 받은 보상은
영영 안 보인다.** D2는 26u/60u(43%) 가림이고 이건 **100%**다.

**B1 — 게임이 권장하는 구성에서 스킬행이 매장된다.**
`ShowRotateHintIfPortrait`(`:570-583`)가 `"가로 화면을 권장합니다"`로 유도하는 구성 =
**터치 + 가로 = Full tier + touch on**(`TierThresholds` 테스트가 확인: 폰 가로는 Full).
그 구성에서 `lift = 120`이라 스킬 카드가 `y ∈ [138,214]`로 올라오고,
성장 판 `y ∈ [150,212]`가 **카드 높이 76u 중 62u(81.6%)를 덮는다.** 4장 전부 + 대시.

**판은 `raycastTarget = false`**(`Panel()` 기본, `:1863`)라 **탭은 통과한다.**
즉 **버튼은 작동하는데 안 보인다** — D2와 **정확히 같은 서명**이다.
레벨업 5초 동안 주 전투 컨트롤이 시각적으로 사라진다.

**D — 위기/기세 배너가 웨이브 배너에 100% 포함된다.**
```
surgeBanner  x=[-150,150]  y=[top-180, top-150]   (300×30, 22pt Bold)
waveBanner   x=[-300,300]  y=[top-200, top-140]   (600×60, 34pt Bold, 등장 시 1.4배 확대)
```
**surge rect가 wave rect에 가로·세로 모두 완전히 포함된다.** 둘 다 배경 없는
굵은 중앙 정렬 텍스트라 **글자 위에 글자**다. 34pt가 1.4배로 punch-in 하는 동안
22pt는 읽을 수 없다.

`위기`/`기세`는 **우리 23종 중 돌발 2종**이다. 즉 **안내 대상 항목이 이미 다른
HUD 요소에 파묻혀 있다** — 안내를 추가하기 전에 이미 그렇다.

#### 4d. 원인은 하나다 — "나중에 꽂는 코드는 앞 rect를 못 본다"

깨끗한 패널과 더러운 패널을 비교하면 규칙이 하나로 떨어진다:

| 패널 | 본체·라벨·버튼을 만드는 곳 | 겹침 |
|---|---|---|
| 스테이지 클리어 | `EnableCampaignUi` **한 곳**(635–652) | **0건** |
| 엠버 레스트 | `BuildEmberRestPanel` **한 곳**(790–828) | **0건** |
| **사망 패널** | 본체·라벨 `Build()`(271–283) + 버튼 `EnableCampaignUi`(**655**) | **D2** |
| **레벨업 대역** | 토스트 `Build()`(296) + 판 `SyncGrowthOffer()`(**1136**, 지연) | **A·B·C·E** |
| **상단 배너 대역** | wave `Build()`(288) + surge `EnureSurgeBanners()`(**1664**, 지연) | **D** |

**한 함수 안에서 만든 패널은 전부 깨끗하고, 두 곳 이상에서 만든 대역은 전부 겹쳤다.
예외 0건.** 좌표를 고르는 사람이 이웃 좌표를 **소스에서 볼 수 없을 때** 겹친다.

이건 부주의가 아니라 **구조**다. `EnableCampaignUi`에서 `캠페인으로`를 y=76에 놓은
코드는 384줄 위의 `_finalText` y=-70을 보고 있지 않았다. `SyncGrowthOffer`에서
판을 y=150에 놓은 코드는 840줄 위의 토스트 y=170을 보고 있지 않았다.

**설계 제약으로 승격한다** (designer에 IRC 전달 완료, 기각 사유로 인용하기로 합의):
> **정지 카드·토스트·도감·이탈 모달은 본체·라벨·버튼을 단일 빌더에서 한 번에 만든다.
> 나중에 다른 함수에서 자식을 꽂는 구조를 금지한다.**

`EnableCampaignUi`가 사망 패널에 버튼을 덧붙이듯 안내 카드에 "닫기/도감" 버튼을
나중에 꽂으면 **D2가 그대로 재발한다.** 이 제약은 조사에서 나온 게 아니라
**계측(산술)에서 나왔다.**

#### 4e. 이 감사가 T-A6에 주는 사양

§4a의 구멍 3개를 그대로 뒤집으면 T-A6의 요구사항이 된다:

| 구멍 | T-A6이 닫는 방법 |
|---|---|
| ① 텍스트가 그래프에 없음 | 텍스트·불투명판을 **rect 집합에 포함**. 4조합 전부 검사 |
| ② 모달이 활성이 아님 | **각 모달을 켠 상태로** 겹침을 잰다. 사망·클리어·레벨업·정지 카드·이탈 모달 |
| ③ 특례가 일반화 안 됨 | 고정 5종 목록이 아니라 **캔버스의 모든 활성 rect를 순회**. 화이트리스트가 아니라 블랙리스트 0 |

**검증 기준**: 새 T-A6이 §4b의 **확정 8건을 전부 빨강으로 만들어야 한다.**
8건 중 하나라도 초록이면 그 T-A6은 D2를 놓친 것과 같은 구멍을 남긴 것이다.
**이게 T-A6의 수용 시험이다** — 테스트의 테스트.

---

### 미해결 의존성 (v1.8)

- **§4b 확정 8건의 수정 범위는 이 레인 소관이 아니다.** 조사는 좌표와 등급까지다.
  다만 **A·B1·D는 안내 기능과 같은 좌표대**라서 안 고치면 안내가 그 위에 또 겹친다 —
  선행 수정 대상으로 올린다.
- **G2 33어절 상한**은 [INFERENCE]다(읽기 속도 200어절/분 가정). designer/PM 서명이
  없으면 보고 항목이고 차단기가 아니다.
- **T-A4 스테이지 정지 상한**은 `CampaignStages` 기믹 배치표에 의존한다. 배치가
  바뀌면 상한도 다시 계산해야 한다.
- **토스트 15종에는 상한이 없다.** 확정 스펙이 정지 8만 예산화했는데 Returnal 사례가
  **무정지 과잉도 혹평 사유**임을 보여준다 [GuidanceSurvey]. 토스트 동시 표시 수·
  지속 시간 상한이 필요한지는 designer 판단이고, 여기서는 **선례가 위험을 예고한다는
  사실만** 남긴다.
- **테스트 코드 작성은 별도 레인.** 이 문서는 판정 정의까지다.

### v1.8 추가 — 레인 교차 후 확정된 판정 3건

2026-08-07 후반. `GuidanceSurvey`와의 교차 결과 판정 3건이 추가·변경됐다.
근거 상술은 `qa/benchmark-notes.md` §v1.8 추가(G1 재검정).

**T-A10) 정지 총량 — 세 축을 동시에 잠근다 (신규)**

정지 예산을 종수 하나로만 잠그면 우회로가 열린다. 표본이 그 우회로 두 개가 실제로
혹평을 받은 걸 보여준다(길이 우회 = Gungeon·CotL, 종수 우회 = DD2).

| 축 | 판정 | 초과 시 |
|---|---|---|
| 정지 **종수** | `tier == pause` 항목 수 **== 8** | FAIL (T-A3와 동일) |
| 정지 **1건당 단어** | 각 정지 카드 본문 어절 수 **≤ 33** | FAIL |
| 정지 **총 어절** | 8건 본문 어절 합 **≤ 264** | FAIL |

- 판정: 세 축 **전부** 통과해야 초록. 하나라도 초과면 FAIL.
- 어절 = 공백 분리 토큰, **제목 제외 본문만**. 도감 상세 본문은 세지 않는다
  (요청해야 나오므로 정지 시간에 안 들어간다).
- **33·264는 [INFERENCE]**다(읽기 속도 200어절/분 가정). designer/PM 서명 전까지
  **보고 항목이고 차단기가 아니다.** 서명되면 차단기로 승격한다.
- 근거: (8회, 짧음) 칸은 **표본 0건 = 미검증**이다. 안전이 증명돼서 잠그는 게
  아니라 **어느 축으로도 안전이 증명되지 않아서** 셋 다 잠근다.

**T-A11) 토스트 상한 — 무정지도 예산이 있다 (신규, 역제보 채택)**

확정 스펙은 정지 8만 예산화하고 토스트 15는 열어뒀다. Returnal이 **정지 0회인데
과잉 혹평**이고 불만 내용이 *"화면을 가린다 / 끌 수 없다"*이지 *"멈춘다"*가 아니다
[GuidanceSurvey]. 표본 과잉 4건 중 1건이 정지 0회라는 사실이 **"무정지 = 예산 면제"를
반증한다.** designer가 `solutions.md §Key Gaps`에 신설하고 **충돌 4**로 올렸다.

- 판정: **동시 표시 토스트 수 ≤ 1**. 2개 이상이 동시에 보이면 FAIL.
- 판정: **토스트 rect 상호 겹침 면적 > 1u²인 쌍 == 0**. 1쌍이면 FAIL.
- 판정: 토스트가 큐잉된다면 큐 길이 상한이 존재하고, 상한 초과분은 **드롭 또는 병합**.
  무한 큐면 FAIL(첫 강하에 15종이 몰릴 수 있다).
- 둘 다 **심 쓰기 0**이라 T-C2를 안 건드린다.

**T-A1 판정 강화) 커버리지는 "항목 존재"가 아니라 "읽힘"이다**

§4b 신규 D가 이 판정을 바꾼다. **위기·기세는 우리 23종 중 돌발 2종인데, 그 배너가
웨이브 배너 rect에 100% 포함돼 있다.** 즉 안내 대상이 **안내 채널에 파묻혀 있다.**
항목표에 23개가 다 있어도 화면에서 2개가 안 읽히면 실질 커버리지는 21/23이다.
designer도 같은 결론으로 제약 3번에 승격했다.

- 판정 추가: 23종 각각에 대해 **표시 시점의 rect가 다른 rect에 90% 이상 덮이지
  않는다**. 1건이라도 90% 초과 피복이면 FAIL.
- 90%를 쓰는 이유: 부분 겹침은 판독 가능할 수 있으나 §4b의 A(100%)·D(100%)·
  B1(81.6%)급은 판독 불가다. **B1이 임계 아래라는 점이 이 수치의 약점**이므로
  T-B12가 육안으로 보완한다.
- **커버리지 전수를 주장하려면 §4b의 A·D부터 풀어야 한다.** 안내 기능이 착지해도
  그 둘이 남아 있으면 T-A1 강화 판정에서 빨강이다.

**미해결 의존성 갱신**

- v1.8 §미해결 의존성의 *"토스트 15종에는 상한이 없다"* 항목은 **T-A11로 닫혔다.**
  designer가 수용 기준 2줄을 권고안에 올렸으므로 더 이상 보고 항목이 아니다.
- **새 의존성**: T-A10의 33·264 어절 상한은 designer/PM 서명 전까지 차단기가
  아니다. 서명 주체는 Stage 2 협상이다.

### v1.8 추가 2 — T-A12 회피 가능성 (빈도 채널)

2026-08-07 후반. 근거: `qa/benchmark-notes.md` §v1.8 추가 2.
`GuidanceSurvey`가 내 절편 오류("0회 → 불만 0건")를 반증했고, 정정 결과
**빈도 채널이 미검증이 아니라 입증된 리스크**로 올라갔다.

**왜 T-A10으로 부족한가**

| 채널 | T-A10이 닫는가 |
|---|---|
| 길이 (1건당 33어절) | 닫힘 |
| 총량 (264어절 = 120초) | 닫힘 |
| **빈도 (8회)** | **안 닫힘** — `종수 == 8`은 9로 늘어나는 걸 막을 뿐, 8이 안전하다고 말하지 않는다 |

Returnal이 **차단 0회 · 차단 총시간 0분인데 흐름 불만**이다. 빈도 채널은 차단
여부와도 총시간과도 독립으로 작동한다. 우리는 8회 = 표본 최댓값의 8배에 노출된다.

**T-A12) 회피 경로가 존재한다 (신규)**

표본 5건에서 불만을 **완전히 가르는 유일한 축**이 회피 가능성이다
(회피 불가 4/4 불만 · 회피 가능 0/1 불만). 횟수·길이는 둘 다 Returnal에 반증된다.

- 판정: 정지 카드 8종 각각에 **해제 입력이 최소 1개** 존재. 0개면 FAIL.
- 판정: 해제 입력이 **첫 프레임부터 유효**하다(강제 대기 시간 0). 잠금 지연이
  있으면 그 값을 기록하고 **> 1.0초면 FAIL** — CotL의 "클리어 후에만 스킵"이
  이 유형의 실패다.
- 판정: 해제가 **`Time.unscaledDeltaTime` 기반**(T-C5e와 동일 근거). `timeScale == 0`
  에서 스케일드 타이머는 안 돈다.
- 판정: 해제 후 **같은 항목이 같은 런에서 다시 정지시키지 않는다**(조우 비트가
  해제 시점에 세워진다). 다시 뜨면 FAIL — 해제가 연기로 동작하면 회피가 아니다.
- **보고 항목**: 8종 전체를 한 번에 끄는 **전역 OFF가 있는가**. 없으면 FAIL은
  아니지만 DD2(전역 OFF 없음 = 혹평 최댓값)와 같은 형태임을 기록한다.

**증거력 표기 — 이 판정은 [INFERENCE]다.**

| 방향 | 증거 | 강도 |
|---|---|---|
| 회피 불가 → 불만 | 4/4, 예외 0 | 시사적 |
| 회피 가능 → 무불만 | **1/1 (ItB 단독)** | **약함** |

**"회피 가능성이 안전을 보장한다"는 주장은 하지 않는다.** 표본에서 **반증되지 않은
유일한 후보**일 뿐이고 나머지 둘(횟수·길이)은 반증됐다. 단일 사례로 충분조건을
주장하면 이 문서가 §v1.8 추가에서 비판한 오류를 반복한다.
따라서 T-A12는 **서명 전까지 보고 항목이고 차단기가 아니다** — T-A10과 같은 등급.

**스펙 공백 — designer 결정 필요**

확정 스펙에 스킵·해제 조항이 **없다**. *"본 것은 다시 정지 안 함"*은 **2회차 이후**를
다루지 **첫 강하의 8회**를 안 다루고, 첫 강하가 빈도 노출 최대 지점이다.
**스펙 위반이 아니라 스펙 공백**이므로 판정을 강제하지 않고 designer에 올린다.

**미해결 의존성 갱신**

- v1.8 §미해결 의존성의 *"조건부 2건은 브라우저 확인 필요"*는 유지.
- **새 의존성**: T-A12의 존재 여부 자체가 designer 결정이다. 채택되면 판정 4개가
  활성화되고, 기각되면 **빈도 채널이 열린 채로 착지한다는 사실을 gate-review에
  명시**해야 한다 — 완화자 없이 노출되는 유일한 채널이기 때문이다.

### v1.8 추가 3 — T-A12 정정(입도 오류) + T-A13 신설

2026-08-07 후반. `GuidanceSurvey`가 **T-A12의 입도 오류**를 지적했다. 재현했고 수용한다.

**정정: T-A12 초판은 통과해도 위험이 그대로인 테스트였다.**

"회피 가능성"을 두 입도로 쪼개면 분리력이 갈린다:

| 입도 | 가능&불만 | 가능&무불만 | 불가&불만 | 완전분리 |
|---|---|---|---|---|
| **개별 해제**(카드 닫기) | **2** (DD2 · Returnal) | 1 (ItB) | 2 | **아니오** |
| **범주 옵트아웃**(아예 안 보기) | 0 | 1 (ItB) | **4** | **예** |

**DD2 모달은 개별로 닫히고 Returnal 팝업은 자동 소멸한다. 둘 다 과잉 혹평이다.**
즉 개별 해제는 충분조건으로 **반증**됐다(반례 2/2).

내가 근거로 든 Returnal의 *"끌 수 없다"* 문구가 정확히 **범주 축**을 가리킨다 —
개별로는 닫히는데 범주로 못 끄는 게 불만의 내용이다. 나는 그 문구를 인용하면서
판정은 개별 입도로 썼다.

**T-A12 초판의 실패 양상**: `8종 각각 해제 입력 ≥ 1`은 정지 카드에 `닫기` 버튼만
달면 자동 통과한다. 그리고 그 상태가 **정확히 DD2 칸**이다. **초록인데 리스크가
안 줄어드는 테스트**이므로 계측으로서 무가치하다.

**T-A12 개정) 범주 옵트아웃이 존재한다**
- 판정: 정지 카드 계열 전체를 **한 번에 끄는 경로가 1개 이상** 존재. 0개면 FAIL.
- 판정: 그 경로가 **첫 정지 카드에서 도달 가능**하다. 설정 메뉴 깊숙이만 있으면
  FAIL — ItB의 "시작 전 거절"과 같은 접근성이어야 한다.
- 판정: 옵트아웃 후 **남은 정지 카드가 0회 발생**. 1회라도 뜨면 FAIL.
- 판정: 옵트아웃해도 **도감에서는 23종 전부 열람 가능**. 정보 자체가 사라지면
  커버리지(T-A1)와 충돌하므로 FAIL.
- **개별 `닫기`는 여전히 필요하다**(T-A12 초판 4판정 유지) — 다만 그건
  **필요조건이지 빈도 채널 완화자가 아니다.** 두 층을 분리해서 기록한다.

**증거력**: 반증 쪽(개별 해제 → 불만, 2/2)은 견고. 긍정 쪽(범주 옵트아웃 → 무불만,
**ItB 단독 1/1**)은 약함. designer도 같은 강도로 유보를 달았다. **"안전 보장"이
아니라 "반증되지 않은 유일한 후보"까지만** 쓴다. 서명 전까지 보고 항목.

**designer 기각 사유 철회 확인 [OBSERVED — 재현]**: 초판 기각 근거였던 "터치 래칫"은
**잘못된 테스트 인용**이었다. 정지 카드는 `HudView` 소속이라 `LobbyLayoutTests`
동결표가 아니라 **`HudLayoutTests` 관할**이고, 후자는 동결표 없는 하드 어서트
(`Assert.That(violations, Is.Empty)`)다. 하한 실측 **44 ÷ 0.48829 = 90.1u**,
사망 패널 460×220u에 90u 버튼 2개가 들어간다(가로 180 ≤ 460, 세로 잔여 130u).
**제약이 막지 않는다.** 철회 타당.

---

**T-A13) 모달 버튼 터치 하한 — §4a 구멍 2번이 겹침 말고 하나 더 숨기고 있었다 (신규)**

위 90.1u를 재느라 기존 모달 버튼을 같이 쟀는데 **전부 하한 미달이다.**
[OBSERVED — 산술, cssPerUnit = 390/798.7 = 0.48829]

| 버튼 | 크기(u) | CSS px | 판정 | 측정되는가 |
|---|---|---|---|---|
| 게임오버 재강하 (`:280`) | 200×44 | 97.7×**21.5** | **미달** | ArrangePhone에서 비활성 |
| 게임오버 캠페인으로 (`:655`) | 200×40 | 97.7×**19.5** | **미달** | 〃 |
| 스테이지클리어 캠페인으로 (`:647`) | 190×44 | 92.8×**21.5** | **미달** | 〃 |
| 스테이지클리어 재강하 (`:649`) | 190×44 | 92.8×**21.5** | **미달** | 〃 |
| 엠버레스트 오퍼 ×3 (`:811`) | 188×128 | 91.8×62.5 | 통과 | **`PhoneEmberRest`가 실행** |
| 엠버레스트 보류/계속 (`:818`,`:821`) | 196×92 | 95.7×44.9 | 통과 | **〃** |

**규칙이 정확히 갈린다: `AssertTouchFloor`가 도는 패널은 준수, 안 도는 패널은 미달.**
예외 0건. 엠버레스트만 `PhoneEmberRest_...` 테스트에서 `AssertTouchFloor(ButtonRects(actions))`
를 호출하고(`HudLayoutTests.cs:511`), 게임오버·스테이지클리어는 어떤 터치 하한
테스트에도 안 들어간다.

**원인은 §4a 구멍 2번과 같다** — `ArrangePhone()`이 모달을 안 켜고
`InteractiveRects()`가 `GetComponentsInChildren<IPointerDownHandler>(**false**)`라
비활성을 제외한다. **같은 구멍이 겹침(D2)과 터치 하한 두 가지를 동시에 숨기고 있었다.**
§4a는 겹침만 다뤘는데 실제로는 더 넓다.

- 판정: 안내 UI 신규 버튼(정지 카드 닫기·옵트아웃·도감 항목·이탈 모달 확인/취소)
  전부 **가로·세로 ≥ 90.1u**. 1개라도 미달이면 FAIL.
- 판정: **모달을 활성화한 상태로** 잰다. 비활성 상태로 재면 이 판정은 공허하다.
- **위험 경고**: 사망 패널 44u·스테이지클리어 44u가 **HUD 모달 버튼의 사실상 관례**다.
  정지 카드가 이 선례를 복사하면 **21.5 CSS px 버튼이 되고 아무 테스트도 안 잡는다.**
  엠버레스트 92u가 따라야 할 선례다.
- 기존 4버튼 수정은 이 레인 소관 밖 — **§4b 확정 8건과 같이 선행 수정 목록에 올린다.**

**미해결 의존성 갱신**
- T-A12는 **개정판(범주 입도)** 기준이다. 초판(개별 입도)은 무효.
- T-A13 기존 4버튼 미달은 신규 결함이 아니라 **기존 라이브 상태**다. 안내 착지와
  무관하게 존재하나, 정지 카드가 선례를 복사하면 재생산된다.

### v1.8 추가 4 — T-A6과 T-A13은 독립이 아니다 (T-A14 신설)

2026-08-07 후반. `GuidanceSurvey`가 자기 철회의 **세로 검산 누락**을 자진 보고했다.
재현했고 타당하다. 그리고 검산을 이어가니 **내 판정 두 개의 결합 문제**가 나왔다.

**1) 세로 예산 검증 [OBSERVED — 산술]** — designer 보고 타당

사망 패널 460×220, 제목 34 + 본문 60 = 94u 고정:

| 배치 | 필요 | 잔여 | 판정 |
|---|---|---|---|
| **스택 2행** (현행) | 94 + 90.1×2 = **274.2u** | **−54.2u** | **불가** |
| **나란히 1행** | 94 + 90.1 = **184.1u** | +35.9u | 가능 |

현행 사망 패널이 **정확히 스택 2행**이다(재강하 y∈[-84,-40], 캠페인으로 y∈[-34,6]).
즉 D2 해소와 터치 하한을 동시에 만족하려면 **위치 조정이 아니라 나란히 재배열
또는 패널 확대**가 필요하다.

**2) 그런데 나란히로 바꿔도 안 끝난다 — 두 수정이 결합돼 있다 [OBSERVED — 산술]**

"스테이지클리어는 이미 나란히니 높이만 올리면 되지 않나"를 검산했더니 아니다:

| 패널 | 버튼 높이 | 버튼 y | 본문 y | 결과 |
|---|---|---|---|---|
| 스테이지클리어 480×240 | 44u (현행) | [-96, -52] | [-14, 46] | 겹침 없음 |
| 〃 | **90.1u** | [-96, **-5.9**] | [-14, 46] | **겹침 8.1u — 신규 D2 유형** |
| 사망 460×220 (나란히) | **90.1u** | [-86, **4.1**] | [-20, 40] | **겹침 24.1u — 신규** |

**버튼은 하단 인셋에 고정돼 있으므로 높이를 올리면 위로 자란다. 그 방향에 본문이 있다.**
즉 **T-A13(터치 하한)을 만족시키면 T-A6(겹침 0)이 깨진다.** 두 판정은 독립으로
쓰였는데 실제로는 같은 세로 예산을 두고 경쟁한다.

**내 오류**: T-A6과 T-A13을 각각 정의하면서 **결합 실현 가능성을 검산하지 않았다.**
각 판정은 개별로 만족 가능하고, 둘 다 통과하는 배치가 존재하는지는 안 물었다.
designer가 명명한 유형과 같다 — **검산 범위가 주장 범위보다 좁음.** 내 경우는
"각 판정 검산 ⊂ 판정 집합 전체"였다.

**3) 결합 해소 최소 치수 [OBSERVED — 산술]**

스테이지클리어 기준, 본문을 위로 **9.1u** 밀어야 한다:

| 방안 | 결과 | 가능 |
|---|---|---|
| (a) 본문 이동 | y=[-5,55], 제목(y=[66,102])과 잔여 10.9u | **가능** |
| (b) 패널 확대 | 240 → 249u | 가능 |
| (c) 하단 인셋 축소 | 24 → 14.9u | 가능 |

**셋 다 가능하므로 막다른 길은 아니다.** 다만 **어느 것도 "버튼 크기만 고치면
된다"가 아니다** — 수정 범위가 버튼 밖으로 나간다.

**T-A14) 판정 결합 실현 가능성 (신규)**

- 판정: 안내 UI의 모든 모달에 대해 **T-A6(겹침 0) ∧ T-A13(≥90.1u) ∧ 콘텐츠 보존**을
  동시에 만족하는 배치가 **존재한다**. 존재하지 않으면 FAIL — 패널 치수를 늘려야 한다.
- 계산: `필요 세로 = 제목 + 본문 + 버튼행수 × 90.1 + 인셋×2 + 행간`.
  **`필요 세로 > 패널 높이`면 그 패널은 설계 단계에서 이미 FAIL이다.**
- 판정: 버튼 행 수는 **1행을 기본**으로 한다. 2행이 필요하면 패널 높이가
  `94 + 180.2 + 인셋` ≥ **약 290u** 이상이어야 한다(사망 패널 220u로는 불가).
- 판정: 이 계산을 **정지 카드 치수 확정 전에** 돌린다. 사후 검증이면 재작업이다.

**정지 카드 설계에 주는 하한**: 닫기 + 범주 옵트아웃 **2버튼을 나란히** 놓고
본문을 유지하려면 카드 세로 ≥ **약 184u + 인셋**, 가로 ≥ **약 190u**
(90.1×2 + 간격). 스택하면 ≥ 290u다. **엠버레스트 620×420이 유일한 통과 선례**이고
사망 패널 460×220은 2버튼 스택으로는 구조적으로 불가하다.

**미해결 의존성 갱신**
- 선행 수정 목록의 "터치 하한 4버튼"은 **버튼 크기 수정만으로 안 끝난다.**
  본문 이동 또는 패널 확대가 동반되며, 그 자체가 §4b 겹침 재계산을 요구한다.
- T-A14는 **설계 입력**이지 사후 검사가 아니다. 정지 카드 치수 확정 시점에 필요하다.

### v1.8 추가 5 — T-A10 × T-A13 결합: 어절 상한은 치수 제약이다

2026-08-07 후반. `GuidanceSurvey`가 T-A14를 적용해 **자기 권고 치수를 재검산**했고,
그 과정에서 **T-A10(33어절)과 T-A13(90.1u)이 결합**한다는 걸 찾았다. 재현·확장했다.

**1) 33어절 = 3행 확인 [OBSERVED — 실측 캘리브레이션]**

designer의 3행 가정을 소스 문자열 9건으로 검산했다:

| 항목 | 값 |
|---|---|
| 표본 | 실제 View 문자열 9건, 57어절 / 105자 |
| 어절당 글자 | **1.84자** |
| 전각 비율 | **66%** (한글 69 / 반각 36) |
| 33어절 추정 폭 (17pt) | **1,128u** |
| 행수 | 460u → **3행** · 480u → **3행** · 620u → **2행** |

**3행 가정 타당.** 15/17/18pt 전부 460·480에서 3행이다 — 폰트 크기에 둔감하다.

**2) 그런데 "480×240 통과" 결론은 인셋 가정에 민감하다 [OBSERVED]**

| 인셋 모델 | 사망 460×220 | 스테이지클리어 480×240 |
|---|---|---|
| designer (제목34+본문66+버튼90.1, 인셋 최소) | 226.1u → **FAIL** (−6.1) | 226.1u → 통과 (+13.9) |
| 내 모델 (인셋 18×2 + 행간 12×2 추가) | 245.3u → **FAIL** (−25.3) | 245.3u → **FAIL** (−5.3) |

**사망은 두 모델 다 FAIL로 일치한다. 스테이지클리어는 갈린다 — ±20u 밴드 안이다.**
즉 **480×240은 통과가 아니라 경계**이고, 인셋·행간을 어떻게 잡느냐로 부호가 바뀐다.
**경계값을 최소 치수로 권고하면 안 된다** — 구현이 인셋을 조금만 넉넉히 잡으면 FAIL이다.

**3) 더 유용한 형태 — 패널별 어절 수용량 역산 [OBSERVED, 17pt·내 인셋 모델]**

| 패널 | 본문 가용 세로 | 행수 | **최대 어절** |
|---|---|---|---|
| 사망 460×220 | 35.9u | 1행 | **약 7어절** |
| 스테이지클리어 480×240 | 55.9u | 2행 | **약 26어절** |
| 엠버레스트 620×420 | 235.9u | 11행 | 약 252어절 |

**33어절은 기존 소형 패널 두 곳 어디에도 안 들어간다.** 사망 패널 형태는 7어절,
스테이지클리어 형태는 26어절이 한계다. 33을 쓰려면 **엠버레스트급이 필요하다.**

**T-A10 재해석**: 33어절은 **문안 품질 기준인 줄 알았는데 치수 제약이기도 하다.**
본문이 길어지면 행수 → 세로 → 버튼 공간을 잠식하고 T-A13이 깨진다. 두 판정이
같은 예산을 나눠 쓴다 — **T-A14가 예고한 결합의 구체 사례이고, 이번엔 내 판정
두 개 사이에서 났다.**

**4) 결정 규칙 (T-A14 적용형)**

정지 카드 치수는 **셋 중 하나를 고르는 문제**이고 자유 조합이 아니다:

| 선택 | 카드 치수 | 본문 상한 | 비고 |
|---|---|---|---|
| (i) 문안을 줄인다 | 480×240급 | **≤ 26어절** | 33 → 26 하향. 총 예산 264 → 208어절 |
| (ii) 카드를 키운다 | **620×420급** | ≤ 33어절 | 엠버레스트 선례. 통과 확실 |
| (iii) 버튼을 줄인다 | 460×220 유지 | ≤ 7어절 | **T-A13 위반** — 선택지 아님 |

**(iii)은 배제된다**(90.1u 하한은 하드 어서트). (i)과 (ii) 중 선택은 designer
소관이고, 여기서는 **"33어절과 460×220은 양립 불가"**만 확정한다.

- 판정: 정지 카드 본문 어절 수 ≤ **그 카드 치수의 역산 수용량**. 초과면 FAIL.
  고정 상수 33이 아니라 **치수에서 유도된 값**을 쓴다 — 카드가 작아지면 상한도 내려간다.
- 판정: 역산 시 **인셋·행간을 명시**하고, 값이 ±20u 밴드 안이면 **경계로 보고**한다.
  경계값을 통과로 처리하면 FAIL.

**미해결 의존성 갱신**
- T-A10의 33·264어절은 **480×240급 카드에서는 26·208로 내려간다.** 어절 상한을
  치수와 분리해서 인용하면 안 된다.
- 위 수용량은 17pt·전각 66%·어절당 1.84자 기준이다. **폰트 크기나 문안 성격이
  바뀌면 재계산**해야 한다(행수는 15~18pt에서 둔감하나 수용량은 아니다).

### v1.8 추가 6 — 어절 수용량 계산 오류 정정

2026-08-07 후반. `GuidanceSurvey`가 §추가5 수용량 표의 **두 칸을 재현하지 못한다**고
보고했다(사망 7, 엠버 252). 재검산했고 **내 식에 버그가 있다.**

**버그**: 나는 수용량을
`cap = (행수 × 폭 − 32 × 공백) / (어절당글자수 × 전진폭)`
으로 계산했다. **33어절분 공백 32개를 전체 행 예산에서 한 번만 빼고, 어절마다는
안 물렸다.** 올바른 식은 어절 단가에 공백을 포함해야 한다:
`cap = 행수 × 폭 / (어절당글자폭 + 공백)`.

**오차 부호가 행수에 따라 뒤집힌다** — 그래서 designer가 한 칸만 재현했다:

| 패널 | 행 | 내 값 | 정정 | 오차 | 원인 |
|---|---|---|---|---|---|
| 사망 460×220 | 1행 | 7 | **13** | −6 **과소** | 공백 272u를 460u 한 행에서 통째로 뺌 |
| 스테이지클리어 480×240 | 2행 | 26 | **26–27** | −1 | **우연히 근사 → 재현된 유일한 칸** |
| 엠버레스트 620×420 | 11행 | 252 | **187–197** | +55 **과대** | 6,820u 예산에 공백을 272u만 물림 |

**1행에서 과소, 11행에서 과대, 2행에서 상쇄.** designer가 "사망 7과 엠버 252는
어떤 조합으로도 못 만들었다"고 한 게 정확하다 — 재현 불가가 아니라 **내 식이
행수에 따라 다른 방향으로 틀렸다.**

**정정 수용량 [OBSERVED — 17pt·전각 66%·어절당 1.84자·행높이 20.4u]**

| 패널 | 본문 가용 | 행 | 행당 어절 | **수용량** | 33어절 |
|---|---|---|---|---|---|
| 사망 460×220 | 35.9u | 1 | 13 | **13** | **불가** |
| 스테이지클리어 480×240 | 55.9u | 2 | 12–13 | **24–27** | **불가** |
| 엠버레스트 620×420 | 235.9u | 11 | 17 | **187–197** | 수용 |

**본문 가용 세로(35.9 / 55.9 / 235.9)는 정확히 일치했다** — 세로 예산 계산은
맞았고 폭→어절 환산만 틀렸다. 그래서 §추가5의 인셋 감도 분석과 (i)/(ii) 선택지
구조는 **영향받지 않는다.**

**결론 불변**: 세 모델(내 정정 · designer floor · round) 전부에서 **33어절은
460×220에도 480×240에도 안 들어간다.** 판정 방향은 유지된다.

**수치 갱신**: §추가5 (i)의 본문 상한을 **26 → 24–27 밴드**로 고친다.
행당 어절이 `floor(480/34.46)=13` vs 보수적 `12`로 갈리므로 점추정을 쓰지 않는다.
**보수값 24를 권고**한다(designer 제안과 일치) — 경계값을 상한으로 쓰지 않는다는
§추가5 규칙을 내 숫자에도 적용한다.

- 판정 갱신: T-A10의 치수 유도 상한은 **`행수 × floor(폭 / (어절당글자폭 + 공백))`**
  으로 계산한다. 공백을 어절 단가에 포함하지 않으면 행수에 따라 부호가 뒤집힌 오차가
  난다.
- 판정: 수용량 계산 시 **행당 어절을 먼저 구하고 행수를 곱한다.** 총 폭 예산을
  한 번에 나누면 행 경계에서 잘리는 어절을 무시하게 되어 과대 추정된다.

### v1.8 추가 7 — 행높이는 미측정 파라미터다 (밴드 확정)

2026-08-07 후반. `GuidanceSurvey`가 §추가6의 **엠버 11행**을 반박했다(10행이 맞다).
검산 결과 **식 오류가 아니라 파라미터 차이**이고, designer 진단이 정확하다.

| 행높이 가정 | 배수 | 엠버 행수 |
|---|---|---|
| 내 값 20.40u | 17pt × 1.20 | 11행 |
| **임계 21.45u** | × **1.262** | 경계 |
| designer 22.00u | 17pt × 1.294 | 10행 |

**임계 배수 1.262는 한글 OTF의 통상 범위 1.15~1.35 한가운데다.** 즉 어느 쪽도
선험적으로 옳지 않고, **둘 다 `NanumBarunGothic`의 실제 메트릭을 안 읽었다.**
`font.lineHeight × lineSpacing`이 참값이고 우리는 그걸 측정하지 않았다.

**그런데 이 불확실성은 판정에 영향이 없다** — 결정에 쓰이는 두 칸이 밴드 전체에서
안정적이기 때문이다:

| 패널 | 배수 1.15 | 배수 1.35 | 행수 안정 |
|---|---|---|---|
| 사망 460×220 | 1행 | 1행 | **안정** |
| 스테이지클리어 480×240 | 2행 | 2행 | **안정** |
| 엠버레스트 620×420 | 12행 | 10행 | 변동 — 그러나 **어느 쪽이든 33어절 수용** |

**소형 패널 두 곳은 파라미터 전 범위에서 행수가 안 바뀐다.** 33어절 불가 판정은
행높이 가정과 무관하다. 변동하는 건 엠버뿐이고 엠버는 어느 쪽이든 통과한다.

**최종 수용량 밴드 (캘리브레이션 N=9 → ±10%, 행수 floor 고정)**

| 패널 | 본문 가용 | 행 | 어절/행 | **최대 어절** | 33 수용 |
|---|---|---|---|---|---|
| 사망 460×220 | 35.9u | 1 | 11~13 | **11~13** | **불가** |
| 스테이지클리어 480×240 | 55.9u | 2 | 11~14 | **22~28** | **불가** |
| 엠버레스트 620×420 | 235.9u | 10~11 | 15~18 | 150~198 | 수용 |

내 정정값(13 / 27)과 designer floor 값(12 / 24)이 **둘 다 밴드 안**이다 —
충돌이 아니라 같은 밴드의 다른 지점. **(i) 상한은 밴드 하단 24 유지.**

- 판정 갱신: 행수는 **floor 고정**. 부분 행은 렌더되지 않으므로 ceil은 과대 추정이다.
- 판정 갱신: 수용량은 **점추정 금지, 밴드로 보고**한다. 밴드가 판정 임계(33)를
  걸치면 **경계로 보고**하고 통과 처리하지 않는다(§추가5 ±20u 규칙과 동일 정신).
- **미측정 입력 기록**: `NanumBarunGothic`의 `font.lineHeight`를 읽지 않았다.
  엠버급 패널에 긴 본문을 넣을 계획이 생기면 **그때는 측정이 선행**돼야 한다.
  현재 판정 범위(소형 패널 33어절 불가)에는 불필요하다.

**검증 규칙 3종 확정** — 이번 사이클에서 자가 오류 5건·designer 오류 3건이 각각
다른 방법으로 잡혔다. 세 규칙이 서로 다른 실패 유형을 담당한다:

| 규칙 | 잡는 실패 | 이번 사이클 사례 |
|---|---|---|
| **요약 ↔ 표 대조** | 자기 산출물을 안 읽음 | 내 "0회 → 불만 0건", 추가4 본문 60u 고정 |
| **다중 입력 검산** | 우연히 맞음 | 내 수용량 식(2행에서만 상쇄, 1·11행에서 부호 반전) |
| **독립 이중 경로** | 일관되게 틀림 | designer ceil 오류(세로 250.1u FAIL vs 폭 42어절 통과가 모순) |

세 번째가 이번 라운드의 산출이다. **다중 입력은 ceil 오류를 못 잡는다** —
입력을 몇 개 넣어도 전부 같은 방향으로 부풀려지기 때문이다. 오직 **같은 결론에
이르는 독립 경로 두 개**(세로 필요량 vs 폭 수용량)를 만들어 어긋남을 보는 것만이
잡았다. 그리고 이 §추가7 자체가 그 규칙의 적용 사례다 — 두 사람이 다른 행높이로
같은 표를 만들었고, **불일치가 미측정 파라미터의 존재를 드러냈다.**

### v1.8 추가 8 — 행높이 실측 확정 (§추가7 "미측정" 해소)

2026-08-07 후반. `GuidanceSurvey`가 **가정하는 대신 폰트를 읽었다.** 독립 재현했고
**전 필드 일치**한다. §추가7의 "미측정 입력" 항목은 이것으로 **해소**된다.

**`Assets/Resources/Fonts/HudKorean.otf` 실측 [OBSERVED — 바이너리 직접 파싱]**

| 테이블 | 필드 | 값 |
|---|---|---|
| `head` | unitsPerEm | **1000** |
| `hhea` | ascender / descender / lineGap | **850 / −300 / 0** |
| `OS/2` v3 | typoAsc / typoDesc / typoGap | 850 / −300 / 0 |
| `OS/2` v3 | winAsc / winDesc | 850 / 299 |

세 경로가 교차 확인된다: `hhea` **1.1500** · `typo` **1.1500** · `win` **1.1490**.
**행높이 배수 = 1.15, 17pt → 19.55u.**

**우리 둘 다 틀렸고, 참값은 양쪽 바깥에 있었다.**

| 출처 | 배수 | 17pt | 엠버 행수 |
|---|---|---|---|
| 내 가정 | 1.200 | 20.40u | 11 |
| designer 가정 | 1.294 | 22.00u | 10 |
| §추가7 임계 | 1.262 | 21.45u | 경계 |
| **실측** | **1.150** | **19.55u** | **12** |

임계 1.262가 **두 가정 모두의 위**에 있었다 — 우리는 임계를 사이에 두고 갈린 게
아니라 **둘 다 임계 아래에서 서로 다르게 틀렸다.** §추가7에서 "임계를 사이에 두고
갈린다"고 적은 내 서술도 실측 앞에서 부정확했다.

**§추가7의 불변 주장은 실측값에서도 성립한다** [OBSERVED — 재현]:

| 패널 | ×1.15(실측) | ×1.20 | ×1.262 | ×1.294 | ×1.35 |
|---|---|---|---|---|---|
| 사망 460×220 | **1** | 1 | 1 | 1 | 1 |
| 스테이지클리어 480×240 | **2** | 2 | 2 | 2 | 2 |
| 엠버레스트 620×420 | 12 | 11 | 10 | 10 | 10 |

**결정에 쓰이는 두 칸은 실측 포함 전 범위에서 불변.** 33어절 불가 판정은 확정이다.

**감도표 갱신**: 19.55u 적용 시 필요 세로가 행당 −0.85u씩 낮아진다
(3행 기준 −2.55u, designer 계산 −7.35u는 4행 기준). **판정 패턴은 불변** —
내 모델 250.1u → **242.8u**로 내려가도 240u를 여전히 초과해 스테이지클리어는 FAIL.
사망은 어느 모델에서도 FAIL.

**측정 경제 — 내 판단이 틀렸다.**
나는 §추가7에서 *"현재 판정 범위에는 불필요, 엠버급에 긴 본문 계획이 생기면 그때
측정"*이라고 적었다. **결론은 맞았지만 경제 판단이 틀렸다** — 실측은 5분이었고,
미측정 파라미터를 문서에 남기고 그 불확실성을 서술하는 비용이 **측정 비용보다 컸다.**
판정에 영향이 없다는 것과 측정을 미룰 이유가 있다는 것은 다른 명제다.

**검증 규칙 3 보강 (designer 제안 채택)**

§추가7의 3종 표에서 규칙 3에 한 줄을 붙인다:

> **독립 이중 경로** — 일관되게 틀림을 잡는다.
> **불일치 시 두 파생값을 비교하지 말고 원천 입력을 측정한다.**

근거가 이번 건 자체다. 두 값이 갈렸을 때 옳은 반응은 서로의 값을 견주는 게 아니라
**원본을 읽는 것**이었고, 그 결과가 **셋 다 아닌 네 번째 값**이었다.
불일치가 드러낸 건 "누가 틀렸나"가 아니라 **"아무도 안 읽었다"**이다.
§추가7에서 내가 "불일치가 미측정 파라미터의 존재를 드러냈다"고 쓰고도 측정으로
가지 않은 것이 이 보강이 필요한 이유다 — **존재를 드러내는 데서 멈추면 절반이다.**

- 판정 갱신: 수용량·세로 계산의 행높이는 **19.55u(17pt) 고정**. 가정값 사용 금지.
- 판정: 폰트 교체 시 이 값을 **재측정**한다. `head.unitsPerEm`과 `hhea` 3필드를
  읽으면 되고 파싱 5줄이다.

**QA 레인 v1.8 종료.** 서명 대기: T-A10 · T-A12 · T-A13 · T-A14.
브라우저 확인 대기: 조건부 겹침 2건(G 콘솔×스킬행, H 회전힌트×훈련배너).
선행 수정 권고: 겹침 A·B1·D + 터치 하한 4버튼.

### v1.8 추가 9 — §추가8의 자기정정을 철회한다 (과잉 정정)

2026-08-07 후반. `GuidanceSurvey`가 **§추가8에서 내가 한 자기정정이 참인 명제를
거짓으로 바꿨다**고 지적했다. 검정했고 타당하다. **§추가7 원문이 맞다.**

**검정 [OBSERVED — 산술]**

임계 행높이 = 235.9 / 11 = **21.445u = 17pt × 1.2615**

| 출처 | 배수 | 행높이 | 행수 | 임계 대비 |
|---|---|---|---|---|
| QA 가정 | 1.2000 | 20.40u | 11 | **아래** |
| designer 가정 | 1.2940 | 22.00u | 10 | **위** |
| 실측 | 1.1500 | 19.55u | 12 | 아래 |

| 명제 | 검정 | 판정 |
|---|---|---|
| **S1** "두 가정이 임계를 사이에 두고 갈린다" (§추가7 원문) | 20.40 < 21.445 < 22.00 | **참** |
| **S2** "실측 1.15는 두 가정보다 작다" (§추가8) | 19.55 < 20.40 ∧ 19.55 < 22.00 | **참** |
| **S3** "둘 다 임계 아래에서 서로 다르게 틀렸다" (§추가8 정정문) | 22.00 < 21.445 이 거짓 | **거짓** |

**S1과 S2는 동시 성립한다 — 서로 배타적이지 않다.** 나는 S2가 참이라는 이유로
S1을 철회하고 S3으로 대체했는데, **S1은 애초에 틀리지 않았고 S3은 틀렸다.**

**§추가7 원문 복원**: *"두 가정이 임계를 사이에 두고 갈린다"* 가 정확한 서술이다.
실측 1.15가 두 가정보다 작다는 §추가8의 사실은 그대로 유효하며, 그 사실이 S1을
반박하지 않는다. §추가8의 해당 문단("임계가 두 가정 모두의 위에 있었다 / 둘 다
임계 아래에서 서로 다르게 틀렸다")은 **무효**다. 나머지 §추가8 내용(폰트 실측값,
행수표, 감도 갱신, 측정 경제 반성, 규칙 3 보강)은 영향 없다.

**검증 규칙 4종으로 확장 — 네 번째 유형: 과잉 정정**

앞의 셋은 전부 **"틀린 걸 못 잡음"**인데 이건 **"맞은 걸 틀렸다고 잡음"**이다.
검출 방향이 반대라 앞의 세 규칙으로는 안 걸린다.

| 규칙 | 잡는 실패 | 사례 |
|---|---|---|
| 요약 ↔ 표 대조 | 안 읽음 | 내 "0회 → 불만 0건", 추가4 본문 60u |
| 다중 입력 검산 | 우연히 맞음 | 내 수용량 식(2행에서만 상쇄) |
| 독립 이중 경로 (+원천 측정) | 일관되게 틀림 | designer ceil(세로 FAIL vs 폭 통과 모순) |
| **철회 대상 재검정** | **과잉 정정** | **§추가8에서 내가 S1을 철회한 것** |

- 규칙: **정정할 때 철회 대상 명제를 원래 근거로 다시 한 번 검정한다.**
  인접 명제가 틀렸다는 것은 그 명제가 틀렸다는 근거가 아니다.
- 발생 조건: 판정을 여러 번 뒤집는 긴 교차검증에서 위험이 커진다. 이번 사이클은
  8라운드에 걸쳐 정지 예산 판정을 4회, 어절 상한을 3회 뒤집었다 — **정정 횟수가
  늘수록 과잉 정정 확률이 오른다.**

이 유형이 마지막에 나온 게 우연이 아니다. 앞의 일곱 라운드에서 오류를 계속
잡아내다 보니 **"내가 쓴 것은 틀렸을 것"이라는 사전확률이 과도해졌고**, 실측이라는
새 증거를 받자 인접한 참인 문장까지 같이 버렸다. 자기교정이 과열되면 정확도가
다시 내려간다.

**QA 레인 v1.8 최종 종료.** 서명 대기 T-A10·A12·A13·A14, 브라우저 확인 2건,
선행 수정 권고 겹침 A·B1·D + 터치 하한 4버튼.

**다음 사이클 독자에게** — 이 v1.8 절은 **9회 개정**을 거쳤고 그 과정에서 판정을
여러 번 뒤집었다(정지 예산 4회, 어절 상한 3회, 회피 입도 1회, 행높이 2회).
§추가9가 보여주듯 **철회 중 최소 1건은 철회 자체가 틀렸다.** 따라서 이 절을 읽을
때는 살아남은 판정뿐 아니라 **철회된 판정이 정말 틀렸는지도 한 번 의심하는 편이
좋다.** 특히 "새 사실이 나와서 이전 서술을 고쳤다"는 형태의 정정은 새 사실과 옛
서술이 **배타적인지**를 먼저 확인해야 한다 — 배타적이지 않으면 둘 다 참일 수 있다.
(designer 레인도 같은 문구를 미러 §판정 이력에 남겼다. 양 레인 공통 주의사항이다.)

---

## v3.0 기믹 무기화 (run-id 20260809-dungeon-fun-authorship)

근거: `.omc/specs/deep-interview-dungeon-fun-execution.md` (Round 0+8, 15%),
`design/dungeon-entry-fun-spec.md` v3.0, `design/enemy-archetype-spec.md`,
`qa/selection-pressure-census.md`, `production/decision-log.md` D-13~D-21.

**이 절이 다루는 것**: W1 겨냥 넉백 · W2 기믹 처치 크레딧 · W3 현장 각인 ·
W4 적 스킬. 그리고 **골든 안전망이 이 사이클만 꺼진다**는 사실.

### 이 사이클의 검증이 평소와 다른 이유

던전 골든 12행이 **의도적으로 움직인다**(D-16). 골든은 "움직이면 회귀"라는
안전망인데, 그 안전망이 이번 한 사이클 동안 무효다.

selected 대체물은 **변경원별 분해 재고정**이고, 그것이 이 절의 척추다.
분해를 안 하면 세 변경이 12행을 뒤섞은 뒤 "이게 맞나?"를 물을 근거가 없다.

### T-W) 골든 분해 — 이 사이클의 최우선

| # | 항목 | 절차 | 실패 조건 |
|---|---|---|---|
| **T-W1** | 1단계 재고정 | W1만 활성. 15행 전부 기록 | 아레나·프롤로그 3행이 **바이트 동일이 아니면 즉시 중단** |
| **T-W2** | 2단계 재고정 | +W2. 1단계 대비 **델타만** 기록 | 3행 이동 |
| **T-W3** | 3단계 재고정 | +W4. 2단계 대비 **델타만** 기록 | 3행 이동 |
| **T-W4** | 분해 합산 검증 | Σ(단계별 델타) == 최종값 − 원본값 | 불일치 = 어딘가에 미기록 변경원이 있다 |
| **T-W5** | W3 옵트인 격리 | W3 미옵트인 경로가 3단계 결과와 **바이트 동일** | 이동 시 옵트인이 새는 것 |

**T-W4가 이 표의 핵심 어서션이다.** 세 델타의 합이 총 변화와 다르면
"변경원이 셋"이라는 전제가 거짓이다 — 넷째가 숨어 있다.

기록 형식(`qa/golden-decomposition-cycle9.md`):

```
행 | 원본 | 1단계(W1) | Δ1 | 2단계(+W2) | Δ2 | 3단계(+W4) | Δ3 | Σ Δ | 검산
```

### T-X) W4 적 아키타입

`design/enemy-archetype-spec.md` §8의 E1~E9를 QA 절차로 전개한다.

| # | 항목 | 방법 | 판정 |
|---|---|---|---|
| **T-X1** | 4종이 실제로 다르게 움직인다 | 동일 스크립트로 4종 각각 심 구동 → 위치·타이밍 궤적 비교 | 임의 두 종의 궤적이 **모든 틱에서 동일**하면 FAIL |
| **T-X2** | 해시 결정론 | 동일 `(id, wave, ordinal)` 2회 | 값 상이 = FAIL |
| **T-X3** | 해시 균등성 | 2,160 표본(웨이브 1–9 × id 1–20 × 공격 12) | 발동률이 기대치(100−임계) ±5%p 밖 |
| **T-X4** | 천장 동작 | 연속 무발동 최댓값 | `PityLimit` 초과 시 FAIL |
| **T-X5** | 천장이 주 경로가 아님 | 천장 기여 비율 | **>12%면 FAIL** — 임계를 낮추거나 천장을 올린다 |
| **T-X6** | 피해 불변 | 4종 전부 `ContactDamage` 산출 경로 | 수치 변화 = S3 재계산 발동 |
| **T-X7** | 예고 예산 무변경 | LCM 센서스 재실행 | 동시 >3 또는 동종 >2 |
| **T-X8** | 속성 정합 | 프로파일 인덱스 == `ElementOf(visual)` enum | 인덱스 어긋남 |

**T-X5가 왜 게이트인가**: 초안이 천장을 6으로 통일했더니 Possessed에서
천장 기여가 **23%**였다 — 천장이 보험이 아니라 주 경로가 됐다는 뜻이고,
그러면 "확률"이 사실상 "6번에 한 번 고정"이 된다. 아키타입별 천장
(6/5/8/9)으로 전부 ≤12%로 내렸다. **이 수치가 회귀하면 설계 의도가
조용히 죽는다.**

### T-Y) 선택 압력 — 밴드로 재정의됨

**초안의 단일 임계 ≥0.75는 폐기됐다**(D-17). 9/9 실패했고 근거는
`qa/selection-pressure-census.md`.

| # | 항목 | 목표 | 방법 |
|---|---|---|---|
| **T-Y1** | 초반 압력 (stage 0–2) | **≥0.70** | 풀 크기 센서스 |
| **T-Y2** | 중반 압력 (stage 3–7) | **≥0.80** | 동일 |
| **T-Y3** | 후반 압력 (stage 8) | **≥0.85** | 동일 |
| **T-Y4** | 죽은 선택지 0 | 제시 3개가 전부 그 스테이지 해저드에 유효 | 9스테이지 전수 |
| **T-Y5** | 오퍼 해시가 플레이어 항에 반응 | 동일 스테이지·레벨, `kills` 다른 두 심 → 오퍼 집합 상이 | 동일하면 FAIL |

**T-Y5가 이 스펙의 핵심 어서션이다**(스펙 V4). 통과하지 못하면 §0의 진단
— *결정론이 플레이어와 연결돼 있지 않다* — 을 고치지 못한 것이다.

센서스 재현 조건(§4r — 조건 없는 수치 인용 금지):

```
제시 수 = 3
풀 = 스탯 N + 스테이지 해저드로 필터링된 각인 면 × 크기 축
유효 해저드 표 = HazardOverride ?? anchor  (서약 테이블 제외)
```

**이 세 조건이 바뀌면 모든 압력 값이 바뀐다.**

### T-Z) G8 인상 점수 — 절차가 바뀌었다

**5사이클 연속 이월된 항목이고, 이번에 측정 대상 자체가 달라졌다.**

초판(v2.0)은 **화면**(집행 영장·판결 등급)을 재려 했다. 정적 UI라
스크린샷으로 점수를 매길 수 있었다. v3.0은 **전투 중 체감**을 잰다 —
스크린샷으로 못 잰다.

| # | 항목 | 절차 | 임계 |
|---|---|---|---|
| **T-Z1** | W1 겨냥 인상 | 플레이 세션. "적을 원하는 곳으로 보냈다"가 성립하는가 | 중앙 ≥4/5 |
| **T-Z2** | W2 크레딧 인상 | 기믹 처치가 **인지되는가**(그전엔 조용했다) | 중앙 ≥4/5 |
| **T-Z3** | W4 4종 구분 | **아키타입 이름을 모르는 채로** 4종이 다르다고 느끼는가 | 중앙 ≥4/5 |
| **T-Z4** | W4 4종 식별 | 어느 적이 어떤 성격인지 **말로 설명할 수 있는가** | ≥3/4 종 |

**T-Z3과 T-Z4를 나눈 이유**: "다르게 느낀다"와 "무엇이 다른지 안다"는 다른
명제다(§4m 계열). 전자만 통과하면 **차이가 노이즈로 읽히는 것**이고, 그건
행동 분화가 아니라 불규칙이다.

**세션 요건**: ≥5 세션, 배포 빌드(`build-webgl` 로컬 서빙), 세션당 최소
2스테이지. 채점자는 아키타입 표를 **보지 않은 상태**로 T-Z3을 먼저 하고,
그 뒤에 표를 보고 T-Z4를 한다 — 순서를 바꾸면 T-Z3이 오염된다.

### T-V) 브라우저 스모크 — 편중 금지 (§4l)

cycle-5의 스모크 13항목이 **전부 던전 경로**여서 두 결함을 동시에 숨겼다.
이번 스모크는 그 실수를 반복하지 않는다.

| # | 모드 | 확인 |
|---|---|---|
| **T-V1** | 던전 | 적 4종이 **육안으로 다르게 보인다** |
| **T-V2** | 던전 | W1 겨냥이 실제로 벽·분출구로 적을 보낸다 |
| **T-V3** | 던전 | W2 처치 연출이 발화한다 |
| **T-V4** | 아레나 | **무변경 확인** — 4종 행동 분화가 새지 않았는가 |
| **T-V5** | 프롤로그 | 동일 |
| **T-V6** | 훈련 | 기믹 시련에서 W1이 동작하는가(스킬·대시 비활성 모드) |

**T-V4·T-V5가 골든 3행의 육안 대응물이다.** 골든은 다이제스트만 보고
화면은 안 본다 — §4c가 cycle-3에서 EditMode 319/319 초록 상태로 7건을
찾은 이유다.

### T-U) 변이 스윕 — 합의 항목부터 (§4q)

cycle-8이 확인했다: **무방비 3건이 전부 만장일치 결정이었고, 논쟁이 있었던
결정 중 무방비는 0건**이었다.

이 사이클에서 아무도 반대하지 않을 항목:

| # | 합의 항목 | 되돌리는 변이 | 잡혀야 하는 테스트 |
|---|---|---|---|
| **T-U1** | "W1은 아레나를 안 건드린다" | `_dungeon` 게이트 제거 | 골든 아레나 2행 |
| **T-U2** | "W4 해시는 결정론적이다" | 해시에 프레임 카운터 혼입 | T-X2 |
| **T-U3** | "W2는 기존 점수를 안 바꾼다" | 처치 크레딧 경로에 점수 가산 | `CampaignSimTests:1002-1003` |
| **T-U4** | "W4는 피해를 안 올린다" | Possessed 흡수를 피해로 변경 | T-X6 |
| **T-U5** | "천장은 보험이다" | 천장을 3으로 낮춤 | T-X5 |

**T-U5가 새 형태다** — 지금까지 변이는 "기능이 꺼지는가"를 봤는데, 이건
**기능이 과하게 켜지는가**를 본다. 천장이 낮으면 확률이 사라지고 주기가
된다. 그것도 회귀다.

### 미해결 의존성 (v3.0)

| # | 항목 | 무엇을 막는가 |
|---|---|---|
| 1 | entry 17·18·19 서명 | T-Z 인상 점수는 돌릴 수 있으나 **밸런스 판정 불가** |
| 2 | 기질 상수 값 | T-X1 궤적 테스트가 값을 되먹인다 — 값이 없으면 테스트가 무엇을 어서션할지 모른다 |
| 3 | Shade 정지 주기 | T-Z3/T-Z4에 직접 영향. **"읽기 어렵게"와 "짜증나게"의 경계** |
| 4 | W4 옵트인 여부 | D-18이 기본 적용으로 판정 — 되돌릴 스위치가 없으므로 T-W3 기록이 유일한 되돌림 근거 |
| 5 | par 시간 | cycle-10 이월. 이번 사이클 비차단 |

### QA가 이 사이클에 하지 않는 것

- **par 시간 측정** — M-B가 이월되며 함께 빠졌다.
- **T9 전체** — 선택 압력 센서스만 선행했고 나머지(길이·보스 비중·보상/분·
  수용률)는 병행이다. 이번 사이클을 차단하지 않는다.
- **S3 노출 곡선 재계산** — W4가 피해 수치를 0개 올리므로 불요.
  **단 T-X6이 그것을 증명해야 한다** — "안 올렸다"는 주장이지 아직 측정이
  아니다.

### v3.0 추가 — 범위 게이트 검증 (D-22 확정 후 신설)

D-22가 "강하 전용"을 확정했다. **주장이지 아직 측정이 아니다.** 아래가 그
주장을 반증하려는 시도다.

| # | 대상 | 방법 | 통과 조건 |
|---|---|---|---|
| **T-G1** | 프롤로그 골든 | W1·W2·W4 착지 후 재실행 | **바이트 동일** |
| **T-G2** | 아레나 골든 2행 | 동일 | **바이트 동일** |
| **T-G3** | 시련 골든 | 동일 | **바이트 동일** |
| **T-G4** | 게이트 제거 변이 | `_dungeon` → `true` 강제 | **프롤로그 골든이 깨져야 한다** |

**T-G4가 이 묶음의 핵심이다.** T-G1~G3은 "안 움직였다"를 보이지만, 기능이
애초에 아무 데도 안 붙었어도 같은 결과가 나온다. §4q(만장일치 결정에 테스트가
없다)가 지목한 형태다 — **게이트를 없앴을 때 깨지지 않으면 게이트가 아니라
빈 코드다.**

측정 도구: `/tmp/simprobe` (Sim 어셈블리 직접 컴파일, Unity 배치모드 불요).
이번 사이클이 이 경로로 4개 모드의 적·기믹·강화·스킬을 전수 측정했고
EditMode보다 빨랐다 — 재사용할 것.

#### 프롤로그 부채 감시 (구현 후 상시)

D-22가 남긴 부채: 튜토리얼이 적 4종을 소개하면서 그 넷이 동일하다는 것도
가르친다. **W4가 던전에서 성공할수록 이 역전이 커진다.**

| 관측 | 신호 |
|---|---|
| G8 T-Z4(4종 식별) 통과율이 높다 | 던전 분화가 잘 읽힌다 = 프롤로그와의 낙차가 크다 = 부채 이자 증가 |
| 신규 플레이어가 던전 첫 진입에서 사망률 급증 | 프롤로그가 가르친 모델이 틀렸다는 증거 |

두 번째는 이번 사이클에 측정 수단이 없다(세션 관찰 ≥5로는 신규/기존 구분 불가).
**다음 사이클 진입 조건으로 넘긴다.**
