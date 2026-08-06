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
