# QA Benchmark Notes — Cycle 2 Gimmick Calibration (run-id 20260806-dungeon-gimmicks)

2026-08-05 · game-qa (Benchmark Calibration Researcher) · Stage 1 Phase 1a survey artifact.
Purpose: calibration benchmarks for NEW dungeon gimmicks (deterministic, data-driven,
WebGL-safe) added on top of the existing three (ember-vent / obsidian-pillar / relic-altar).
Companion artifact: novelty/frequency survey is the designer lane
(`design/trend-survey/`, superset 11-title pool); THIS file owns the numbers
(telegraph seconds, damage % HP, ramp, readability, reduced motion). Agreed via IRC
with DesignerTrendSurvey 2026-08-05 — both lanes share the 6 titles below so G8
frequency rows stay comparable.

## Provenance legend

- **[direct spec]** — read from this repo's frozen contract (`docs/SIM_SPEC*.md`, code).
- **[indexed snippet]** — web-search-grounded summary over wiki/community/press pages
  (fandom, steamcommunity, reddit, access-ability.uk, diablowiki.net, gungeongod.com,
  aaronbsmith.com/gamepressure D4 settings lists). Retrieved 2026-08-05; the search layer
  returned excerpts, not full pages — treat as secondary.
- **[INFERENCE]** — convention observed in play/analysis but not publicly documented as
  an exact number. Stated honestly as an estimate; do not cite as fact in gate verdicts.

## Our own anchors [direct spec]

| Anchor | Value | Source |
|---|---|---|
| Sim tick | 60 Hz fixed step, deterministic, NO RNG | `SimConfig.FixedStep`, SIM_SPEC_CAMPAIGN §Determinism |
| Player HP | 100 base; dungeon max = 100 + vit×8 + cloak(+8/rank, ≤5) → ~100–188 | SIM_SPEC_CAMPAIGN §Item drops, cycle2-spec §B0 |
| ember-vent | radius 90, period 2.4 s, **telegraph 0.8 s**, damage 8 (= 8% of base HP; Ward negates) | SIM_SPEC_CAMPAIGN §ember-vent |
| obsidian-pillar | radius 40 blocker, push-out, iso metric (dy×1.42) | SIM_SPEC_CAMPAIGN §obsidian-pillar |
| relic-altar | radius 70, hold 1.2 s → oil +18, cooldown 6 s | SIM_SPEC_CAMPAIGN §relic-altar |
| Reduced motion | ViewPrefs.ReducedMotion: hit-stop/slow-mo off, shake/flash ×0.4; hazards are sim-authoritative so the toggle NEVER changes outcomes | cycle2-spec §A5/§B4 |

## Per-title calibration table (6 titles)

| Title | (a) Telegraph duration & grammar | (b) Hazard damage as % player HP | (c) Stage N+1 ramp convention | (d) Simultaneous-telegraph readability | (e) Accessibility / reduced motion |
|---|---|---|---|---|---|
| **Hades** | No instant attacks; every major attack has telegraph → windup → recovery; standardized ground circles for AoE [indexed snippet]. Exact windups undocumented; observed ~0.5–1.2 s for regular AoE, longer for arena-wide boss slams [INFERENCE] | Regular hits ~5–15% of a 50–150 HP pool; boss heavies ~20–30%; almost nothing one-shots [INFERENCE — wiki lists flat damage, HP varies by Mirror] | Per-biome NEW mechanic (Asphodel lava floor, Elysium respawning shades) + more simultaneous attackers; mechanics-first, stat inflation second [indexed snippet + INFERENCE] | Boss + adds keep distinct telegraph channels (ground circle vs projectile vs lunge line); rarely >2–3 distinct AoE circles live at once; failure is attributed to greed, not unreadability [indexed snippet] | God Mode: +20% dmg reduction, +2%/death to 80%, toggle anytime; screen-shake toggle [indexed snippet] |
| **Dead Cells** | Retracting spikes shine/pulse before extending; flails have rhythmic learnable cycles [indexed snippet]. Pulse-to-damage window subsecond, ~0.5–1.0 s [INFERENCE] | Trap damage scales per biome tier but is **capped ≈30% max HP per hit** [indexed snippet — fandom wiki]. That cap is the genre's clearest "heavy ceiling" number | Biome tier raises enemy level AND trap damage together; boss-cell (BC) meta-ramp adds systemic pressure (Malaise soft-timer at 5BC: passive play punished) [indexed snippet] | Hazards are mostly static/rhythmic, learnable geometry rather than stacking dynamic telegraphs; density rises, cycle grammar stays fixed [indexed snippet + INFERENCE] | Assist Mode: independent trap damage AND trap speed sliders, parry window, enemy HP/dmg; progression not disabled [indexed snippet] |
| **Vampire Survivors** | Essentially NO formal hazard telegraphs — threat is body-contact crowd geometry; danger is communicated by density and approach vectors [indexed snippet] | Per-touch contact damage small (single digits vs ~100+ HP, i.e. ~1–5%/tick); lethality via stacking DPS not single hits [INFERENCE — wiki lists per-enemy power, builds vary] | Time-keyed waves: fixed-minute density jumps, elite/swarm events at scheduled timestamps; ramp = spawn table swap per minute, fully data-driven [indexed snippet + INFERENCE] | Deliberately accepts visual overload as fantasy; readability delegated to player position + white damage flashes. NOT a model for telegraphed AoE — negative benchmark for our lane [indexed snippet] | Flashing-VFX disable toggle; damage-number toggle to cut clutter; players still report overload at high intensity [indexed snippet] |
| **Halls of Torment** | Explicit Diablo-style ground circles/cones/lines before boss/elite attacks land [indexed snippet]. Fill-then-fire grammar (outline appears, zone fills, then damage) with ~1–2 s lead on heavy hits [INFERENCE] | Telegraphed boss/elite AoE is high — big chunk to one-shot territory (≥40%; some kill outright), which is WHY the telegraph is long and explicit [indexed snippet + INFERENCE] | Later stages: more elites with more overlapping telegraph patterns + agony modifiers; ramp = pattern count and overlap, not just numbers [INFERENCE] | Layers several zones at once but keeps shapes distinct (circle vs cone vs line); documented player complaint: telegraphs can blend into floor art — contrast is the failure mode, not count [indexed snippet] | Limited dedicated suite; relies on readable telegraphs themselves [thin evidence — no authoritative settings list retrieved] |
| **Enter the Gungeon** | Floor traps (flame jets, spike panels) run fixed visible cycles or arm on proximity with a short pop-up animation [indexed snippet]. Trap arm-to-fire ~0.3–0.6 s — tightest in this pool, paired with the lowest per-hit cost [INFERENCE] | Cleanest quantized grammar in pool: standard trap/contact = **half heart**; jammed = 1 full heart. On a typical 3–5 heart run: half heart ≈ 10–17% HP [indexed snippet — gungeongod/fandom] | Floor N+1: denser bullet patterns, faster projectiles, more trap-filled rooms; curse raises jammed (double-damage) frequency — damage quantum stays fixed, frequency/speed ramp [indexed snippet + INFERENCE] | Bullet-hell readability solved by uniform projectile color grammar (pink/red) + static learnable traps; hazards NEVER strobe; dodge-roll i-frames are the pressure valve [indexed snippet + INFERENCE] | Few dedicated toggles (screen-shake option present) [thin evidence]; readability carried by art grammar |
| **Diablo 3 / 4** | Ground affixes form-then-burn: Desecrator swirl expands before pool is "fully formed" and damaging; Plagued has a brewing/bubbling phase; Arcane Sentry has long audible+visual setup then slow-sweeping beam [indexed snippet — diablowiki]. Form time ~0.5–1 s; sentry setup ~1–2 s [INFERENCE]. Pools persist ~12 s once live [indexed snippet] | Ground effects tuned as DoT: individually ~5–15% HP/tick at on-level torment, lethal only if you stand in them or stack pools; one-shot ground damage is treated as a tuning failure by the community [INFERENCE] | Difficulty tier raises %HP/%dmg AND affix count per elite pack (more simultaneous ground-effect types); GR/Pit = pure multiplier ladder on the same telegraph grammar [indexed snippet + INFERENCE] | Multiple pools + beams routinely coexist; readability via strong color coding (orange/green/purple) per family; known failure: pool color vs floor tileset contrast [indexed snippet] | D4 has the fullest suite in pool: screen-shake off, combat hit-flash off, dedicated **Reduce Strobing** toggle, Highlight Player [indexed snippet — settings lists] |

## Derived calibration bands (QA recommendation to designer/PM — inputs to G2 band table)

These are the recommendations this survey exists to produce. Each is traceable to rows above.

1. **Telegraph duration scales with damage tier.** Genre-wide pattern: cheap hits get short
   warnings, expensive hits get long explicit ones (Gungeon 0.3–0.6 s @ ~12% HP …
   Halls of Torment 1–2 s @ one-shot). Recommended bands for new gimmicks at 60 Hz:
   - **light** (≤10% base HP, our vent's 8 dmg class): telegraph ≥ 0.8 s (48 ticks) — current vent is exactly at floor. [direct spec + INFERENCE]
   - **medium** (10–25% base HP): telegraph ≥ 1.2 s (72 ticks).
   - **heavy** (25–30% base HP): telegraph ≥ 1.8 s (108 ticks) + distinct audio cue (`HazardPulse`-family event).
2. **Damage ceiling.** No single gimmick hit above **30% of max HP** — Dead Cells' cap is the
   only published hard number in the pool and the genre treats one-shot environment damage
   as a fairness failure. [indexed snippet + INFERENCE]
3. **Warning visual grammar.** Convention = zone-shaped ground marker (ring/cone/line) that
   exists BEFORE damage, distinct per hazard family by color+shape, never flash-only.
   Under ReducedMotion the persistent zone marker must remain; only the blink/pulse
   modulates (matches our A5 contract: flash ×0.4, marker stays). [indexed snippet + direct spec]
4. **Ramp convention for stage N+1.** All 6 titles ramp by ADDING pattern/mechanic count and
   overlap before inflating numbers. Our existing catalog already follows it (cinder-span
   2 vents → ember-gallery 3 vents+pillar → witness-well altar+2 pillars+vent). New stages
   should introduce exactly ONE new gimmick kind per stage, then remix it with existing
   kinds in the following stage. [direct spec: StageCatalog hazard tables + INFERENCE]
5. **Simultaneous-telegraph budget.** Readable convention is **≤3 concurrently telegraphing
   hazards** in the combat plane, and ≤2 of the same kind phase-aligned. Because our phases
   are deterministic, this is mechanically checkable: max simultaneous `telegraphing==true`
   over one LCM of all periods (see test-plan.md §D3 — this is a QA gate check, not a vibe).
   [INFERENCE from Hades/HoT/D3 rows + direct spec determinism]
6. **Contrast is the real readability risk**, not count: both HoT and D3 rows show the failure
   mode is marker-vs-floor blending. New terrain (dressing lane) must keep hazard rings on a
   reserved accent channel; QA smoke includes a screenshot check per new stage terrain.
7. **Reduced-motion parity.** Pool norm: shake/flash toggles (Hades, VS, D4), damage-model
   toggles kept separate (Dead Cells Assist, Hades God Mode). Our split matches: ViewPrefs
   is view-only; sim outcomes NEVER differ with ReducedMotion on/off — that invariance is a
   regression test, not a promise. [direct spec]

## Curated sources

- docs/SIM_SPEC_CAMPAIGN.md, docs/SIM_SPEC_HACKSLASH.md, Assets/Scripts/View/StageCatalog.cs — [direct spec]
- Dead Cells wiki (fandom): trap damage biome scaling + ~30% cap — [indexed snippet]
- Enter the Gungeon wiki (fandom) + gungeongod.com: half-heart convention, jammed 1-heart — [indexed snippet]
- diablowiki.net: Desecrator/Plagued/Arcane formation delay, ~12 s pool persistence — [indexed snippet]
- access-ability.uk, steamcommunity: Hades God Mode numbers, VS flashing-VFX toggle — [indexed snippet]
- aaronbsmith.com / gamepressure D4 settings inventories: Reduce Strobing, hit-flash, shake, Highlight Player — [indexed snippet]
- Hades/HoT design analyses (medium, minimap.net, steamcommunity threads): telegraph grammar, readability framing — [indexed snippet]
- All exact seconds not published by developers are marked [INFERENCE] above and MUST NOT be
  cited as measured values in `qa/gate-measurements.md`.

---

## 훈련·돌발 벤치마크 (run-id 20260806-training-and-upgrade)

2026-08-06 · game-qa (Benchmark Calibration Researcher) · Stage 1a 서베이 아티팩트.
목적: **"얼마가 공정한가"**. 돌발(surge) 이벤트의 발동 빈도·예고·위협량·대응 창,
그리고 훈련 모드가 본편 밸런스를 오염시키지 않게 하는 검증 관례를 수치로 고정한다.
designer 레인은 "무엇을 만들까"(빈도표·신규성)를 소유한다 — 이 파일은 숫자만 소유하며
두 레인은 아래 7타이틀 풀을 공유한다.

cycle-2 섹션(위)은 **정적 기믹**의 예고/피해 밴드를 세웠다. 이 섹션은 그 밴드가
**동적 이벤트**에도 그대로 성립하는지 판정하고, 성립하지 않는 지점을 명시적으로 경고한다.

### Provenance legend (이 섹션 추가분)

- **[direct spec]** — 이 리포의 동결 계약(`Assets/Scripts/Sim/*.cs`, `docs/SIM_SPEC*.md`)에서 읽음.
- **[direct spec + 계산]** — 위 상수로 QA가 직접 계산/센서스한 값. 재현 절차는 test-plan §T3에 있음.
- **[indexed snippet]** — 웹 검색 계층이 반환한 wiki/커뮤니티/공식 문서 **발췌**. 2026-08-06 조회.
  전체 페이지가 아니라 발췌였다 — 2차 자료로 취급한다.
- **[INFERENCE]** — 플레이/분석에서 관측되나 개발사가 수치로 공표하지 않은 관례. 추정임을 명시.
  게이트 판정문(`qa/gate-measurements.md`)에 **측정값으로 인용 금지**.

### 우리 앵커 — 돌발 관련 (cycle-2 표에 추가)

| 앵커 | 값 | 출처 |
|---|---|---|
| 예고하는 기믹 종류 | 3종만: 분출구(0.8s) · 해류(0.8s) · 벽(1.5s). 제단/기둥/방벽주는 `Telegraphing`을 채우지 않는다 | `CinderSim.cs` §HazardView 3108-3127 [direct spec] |
| 분출구 | 주기 2.4s · 예고 0.8s · 피해 8 (기본HP의 8.0% / 만개 220HP의 3.6%) | `CampaignSpec.Vent*` [direct spec + 계산] |
| 해류 | 주기 6s · 예고 0.8s · 활성 3.2s(듀티 53%) · 밀기 200 vs 이속 218 → 역류 보행 가능(≈18px/s) | `CampaignSpec.Current*` [direct spec] |
| 벽 | 주기 23s(rest 4.5 + 예고 1.5 + 전진 7 + 유지 3 + 후퇴 7) · 틱 10 / 0.6s = **16.67 dmg/s** | `CampaignSpec.Wall*` [direct spec + 계산] |
| 벽 DoT 노출 곡선 | 기본 100HP 기준 10% = 0.60s(35틱) · 20% = 1.20s(71틱) · **30% = 1.80s(107틱)** · 사망 6.0s | [direct spec + 계산] |
| 웨이브 간 인터미션 | **2.15s = 129틱** — 심에 이미 존재하는 유일한 "준비" 창 | `SimConfig.WaveIntermission` [direct spec] |
| 현재 동시 예고 피크 | ash-march **2** (동종 1) · cinder-sluice **2** (동종 1) · 나머지 4스테이지 **1** | 276s LCM 센서스 [direct spec + 계산] |
| 예고 점유율 | ash-march 71.0% · cinder-sluice 75.1% · span/throne 66.6% · chancel/bastion 33.3% (최소 1종이 예고 중인 시간 비율) | 동일 센서스 [direct spec + 계산] |
| 훈련(프롤로그) 현황 | 3웨이브 4/6/8 · `Hazards.Count == 0` · 보스 없음 · 스킬/대시 입력 무시 · 각인 무효 · 메타스탯/장비 무효 · 골든 `prologue\|1650\|2\|9\|1\|36\|(running)\|930.1258\|435.3988` | `HackSimTests` §1, `SigilTests`, `DungeonGoldenDigestTests` [direct spec] |

### 돌발 이벤트 공정성 캘리브레이션 (7타이틀)

| 타이틀 | (a) 발동 빈도·조건 | (b) 예고 길이·문법 | (c) 한 번에 위협하는 체력 % | (d) 대응 시간 창 | (e) 결정론 — 우리 무RNG 제약 적합성 |
|---|---|---|---|---|---|
| **Deep Rock Galactic** (스웜) | 미션 스크립트 + 체류 시간. Point Extraction은 타이머 없이 체류 길이에 비례해 밀도 상승, 종국엔 거의 연속 스웜 [indexed snippet] | **약 20초** — Mission Control 음성 경고 후 벌레가 땅에서 올라옴. 풀 중 **가장 긴 예고** [indexed snippet] | 스웜 자체는 단일 히트가 아니라 **누적 압박**. 개별 접촉 피해는 작고 위협은 밀도에서 나옴 [INFERENCE] | 20초 — 방어 진형 구축·집결·장비 준비까지 가능한 **행동 계획 창** [indexed snippet] | 조건부 발동(스크립트/체류)이라 우리 고정 시간표로 이식 가능. **예고 길이 최고 참조점** |
| **Left 4 Dead 1/2** (패닉 이벤트) | AI Director 상태기계. 패닉은 맵 로직(문 개방/알람)이 강제 진입시키는 **스크립트 오버라이드** [indexed snippet] | 진입 자체는 즉발(트리거=문/알람이 곧 예고). 사이 회복 구간 **Relax 목표 30–45초** [indexed snippet] | 개별 커먼 피해는 작음. 위협은 군집. Director가 커먼 상한을 준수하므로 실효 강도는 플레이어 처리 속도에 종속 [indexed snippet] | 종료 조건이 **시간이 아니라 처치 수 또는 목표 도달** — 대응 창은 플레이어가 만든다 [indexed snippet] | Relax 30–45초는 **이벤트 간 최소 간격**의 좋은 하한. 트리거가 플레이어 행동이라 결정론적 |
| **Risk of Rain 2** (텔레포터 이벤트) | **플레이어가 켠다(옵트인)**. 난이도 계수는 런 타이머로 연속 상승, 스테이지 전환마다 점프 [indexed snippet] | 이벤트 예고 없음 — 플레이어가 시작 시점을 고름. 대신 난이도 바 **노치**가 적 레벨업 시점을 미리 보여줌 [indexed snippet] | 보스 편성이 Director 크레딧에 따라 1보스 → 다보스 → 엘리트보스 → Horde of Many로 승격 [indexed snippet] | 무제한 — 준비가 끝날 때까지 미룰 수 있음. 커뮤니티 관례 "3분/3레벨/3아이템 중 2" [indexed snippet] | **핵심 교훈: 위기를 옵트인으로 만들면 예고 문제가 사라진다.** 우리 제단/준비 오퍼와 같은 문법 |
| **Vampire Survivors** (리퍼/시간표) | **완전 고정 시간표**. 스테이지 제한시간(보통 15 또는 30분) 도달 시 Red Death 스폰, 이후 **매 1분마다 추가** [indexed snippet] | 시간표 자체가 예고 — 시계가 항상 보임. 별도 시각 예고 없음 [indexed snippet] | Red Death는 사실상 처형(런 종료용) — 상한 개념이 아니라 **의도된 종결자** [indexed snippet] | 분 단위로 예측 가능 → 대응 창 = 남은 시간 전체 [indexed snippet] | **우리와 가장 가까운 구조.** Hurry는 시간표를 ½로, Endless는 종결자를 끔 — **모드 플래그가 시간표를 바꾸는 데이터 주도 설계** |
| **Monster Hunter** (분노) | **불가시 분노 게이지** — 누적 피해량으로 채워짐. 체력 % 임계도 아니고 타이머도 아님 [indexed snippet] | 진입 시 포효·발색·연기 등 **상태 표식**. 사전 예고가 아니라 **진입 통지** [indexed snippet] | 분노 중 공격 속도·이동 속도·전용 공격 빈도 상승 — 단일 히트 상한이 아니라 **DPS/압박 상승** [indexed snippet] | 사전 창 없음. 대응은 "표식을 보고 즉시 거리 재설정" [INFERENCE] | **부적합 — 부정 벤치마크.** 불가시 게이지는 예측 불가. 선행 서베이 규칙 2(무작위/불가시 발동 금지)와 정면 충돌 |
| **Elden Ring** (보스 2페이즈) | 체력 % 임계. **50%는 관례지만 하드코딩 아님** — 60%대·40%대 사례 혼재 [indexed snippet] | 컷신형(명확한 분리) / 무브셋 추가형("히든 페이즈", 예고 없음)로 갈림 [indexed snippet] | 페이즈 전환 자체는 피해가 아니라 **위협 문법 교체** [indexed snippet] | 컷신형은 사실상 무한(연출 중 무피해). 히든형은 0 [indexed snippet] | **체력 임계 트리거의 경고 사례**: 애니메이션 락과 버스트 피해가 겹치면 임계를 **건너뛴다**. 우리가 체력 임계를 쓰면 같은 경계 조건을 명시해야 함 |
| **Hades** (Extreme Measures / Pact) | **플레이어가 켠다(옵트인)** — Pact of Punishment 조건. 켜지 않으면 발동하지 않음 [indexed snippet] | 보스 무브셋 자체는 cycle-2에서 기록한 예고→발동→회복 문법 유지(즉발 공격 없음) [indexed snippet] | cycle-2 기록: 일반 5–15%, 보스 헤비 20–30%, 원샷 거의 없음 [INFERENCE, cycle-2 표 재인용] | 상시 예고 문법이 유지되므로 난이도가 올라도 대응 창은 존재 [indexed snippet] | **난이도 상승을 옵트인 + 예고 유지로 처리한 표준.** 골든 불변과 양립하는 유일한 패턴 |

### 훈련/연습 모드 격리 관례 (5타이틀 — "본편을 오염시키지 않는 방법")

| 타이틀 | 격리 수단 | 보상 처리 | 진행도 처리 | 통계/기록 | 우리에게 주는 검증 항목 |
|---|---|---|---|---|---|
| **Warframe** (Simulacrum) | 전용 공간. "파밍 장소가 아니라 통제된 시험 환경"으로 명시 설계 [indexed snippet] | **Affinity(XP) 0, 루트 미지속.** 안에서 떨어진 것은 안에 남는다 [indexed snippet] | 미션 목표·Focus·계정 단위 진행에 **일절 미반영** [indexed snippet] | 별도 — 스타차트 기록에 안 들어감 [INFERENCE] | **가장 강한 관례.** 훈련은 "보상 0 + 진행도 0"이 기본값 |
| **Hades** (Skelly) | 로비 내 훈련 인형 | **인형 타격에 대한 보상 자체가 없다.** 피해 임계 보상 없음 [indexed snippet] | 없음. Skelly 관련 보상은 전부 **런/Heat 조건**(8/16/32 Heat 상 = 순수 장식) [indexed snippet] | 없음 | 연습에 보상을 안 붙여도 연습은 쓰인다 — 우리 "재훈련" 무보상 현황이 장르 이상값이 아님 |
| **Street Fighter 6** (Training) | 별 모드. Frame Meter·더미 녹화·리버설 설정은 **훈련 모드 전용** [indexed snippet] | 경쟁 모드에선 도구 자체가 비활성 — "모든 플레이어에게 표준화된 경험" 보장 목적 [indexed snippet] | 랭크 포인트 미반영 [indexed snippet] | 랭크 기록과 분리 [indexed snippet] | **뷰/도구 격리 = 우리 ViewPrefs 계약과 동형.** 훈련 전용 표시가 본편 판정에 새면 안 됨 |
| **Vampire Survivors** (Hurry/Endless) | 같은 스테이지에 **모드 플래그** — Hurry는 시간표 ½, Endless는 종결자 비활성 [indexed snippet] | 모드별로 규칙이 다르되 데이터 주도 [indexed snippet] | 스테이지 클리어 판정이 모드에 종속 [indexed snippet] | 모드별 분리 [INFERENCE] | **훈련을 "별 심"이 아니라 "같은 심 + 플래그"로 만드는 근거.** 우리 `GameMode.Prologue`가 이미 이 형태 |
| **Deep Rock Galactic** (튜토리얼 미션) | 별 미션 타입 | 최초 완료에 **1회성** 보상 — 반복 파밍 불가 [INFERENCE — 검색으로 확증 못 함] | 1회성이라 반복 진행 기여 없음 [INFERENCE] | 미확인 | "1회성 보상"은 반복 훈련에 보상을 붙이는 **유일한 안전 패턴** — 단 근거 약함, 게이트 인용 금지 |

### 우리 기존 밴드와의 충돌 판정 (명시적)

cycle-2가 세운 밴드 3개를 돌발 이벤트에 대입한 결과. **판정은 "조건부 유지" 2건 +
"깨질 수 있음 — 경고" 2건이다.**

#### C1. 단일 히트 ≤ 30% max HP → **조건부 유지. 단 DoT 경로에 구멍이 있다 ⚠**

돌발이 **새 히트를 추가**하는 방식(분출구 추가 발동 등)이면 밴드는 유지된다. 현재 최대
단일 히트는 벽 틱 10(기본HP 10.0% / 만개 4.5%)이고 30%까지 여유가 크다.

**그러나 벽은 이미 DoT다.** 16.67 dmg/s이므로 기본 100HP 기준 **1.80초 노출로 정확히
30%**에 도달하고 6.0초면 사망한다 [direct spec + 계산]. 즉 돌발이

- 벽의 `WallHold`(3s)를 늘리거나,
- `WallTickDamage`(10)를 올리거나,
- 벽 활성 중 플레이어를 벽 쪽으로 미는 해류를 겹치면

**per-hit 규칙을 한 번도 위반하지 않고 실효 상한을 넘긴다.** 이건 밴드 문구의 구멍이다.

→ **QA 권고: 밴드 2를 "단일 히트 ≤30%" **에서** "단일 히트 ≤30% **AND** 단일 노출
에피소드(연속 피해 구간) 누적 ≤30%"로 확장**하라. 측정 가능하다 — test-plan §T4가
`HazardPulse` 사이 연속 구간의 HP 델타를 세는 방식으로 검사한다. 이 확장 없이 돌발을
벽/해류에 붙이면 게이트 G2의 단일 히트 감사는 **통과하면서 실제로는 불공정**해진다.

#### C2. 예고 ≥ 0.8초 → **유지. 단 티어가 하나 부족하다 ⚠**

기믹 레벨 예고(0.8/0.8/1.5s)는 그대로 성립한다. 문제는 **이벤트 레벨 예고가 밴드에
아예 없다**는 것이다. 풀 전체가 두 층을 구분한다:

- **기믹 레벨** = "이 타일이 곧 터진다" → 0.3–1.8초 (cycle-2 표 전체)
- **이벤트 레벨** = "상황이 곧 바뀐다" → DRG **20초**, L4D Relax **30–45초** 간격

DRG의 20초를 우리 스케일로 비례 환산하면(DRG 미션 300–600초 → 경고는 미션의 3.3–6.7%)
우리 웨이브 10–30초에 대해 **0.33–2.01초**가 나온다 [direct spec + 계산]. 상단이
**이미 존재하는 웨이브 인터미션 2.15초(129틱)와 거의 정확히 일치한다.**

→ **QA 권고: 이벤트 레벨 예고 밴드를 ≥ 2.0초(120틱)로 신설**하고, 돌발을 **웨이브
경계에 정렬**하라. 그러면 (1) 비례 환산 상단을 만족하고, (2) 인터미션 안에 들어가므로
새 정지 구간을 만들지 않고, (3) 골든 15행의 웨이브 타이밍을 건드리지 않는다.
런 중간 임의 시점 발동은 이 세 이점을 전부 잃는다.

#### C3. 동시 예고 ≤3 · 동종 ≤2 → **여유가 정확히 1이다. 2종 추가 시 깨진다 🔴**

276초 LCM 센서스 결과(재현: test-plan §T3):

| 스테이지 | 동시 예고 피크 | 동종 피크 | 예고 점유율 | 밴드 여유 |
|---|---|---|---|---|
| ash-march | **2** | 1 | 71.0% | 동시 **+1** / 동종 +1 |
| cinder-sluice | **2** | 1 | 75.1% | 동시 **+1** / 동종 +1 |
| cinder-span · echo-throne | 1 | 1 | 66.6% | +2 |
| abyss-chancel · ember-bastion | 1 | 1 | 33.3% | +2 |

**판정: 돌발이 "예고하는 기믹"을 1종 추가하면 ash-march/cinder-sluice는 정확히 3/2 —
상한에 붙는다. 2종 추가하면 밴드가 깨진다.** 그리고 이 두 스테이지는 예고 점유율이
이미 71–75%다. 예고가 시간의 3/4를 덮은 화면에 예고를 더 얹는 것은 대비(cycle-2 밴드 6)
실패로 직행한다.

**장르가 이 문제를 푸는 방법은 우리가 쓸 수 있다:** DRG 스웜은 **벌레**를 보내고 새 바닥
함정을 놓지 않는다. L4D 패닉은 **군집**을 보낸다. RoR2 텔레포터는 **적 편성**을 승격시킨다.
7타이틀 중 **돌발로 새 예고 기믹을 추가하는 사례가 0건이다** — 전부 적 밀도·압박·편성을
올린다.

→ **QA 권고(구속력 있음): 돌발은 예고 기믹 수를 늘리지 말고 적 밀도/편성/기존 기믹의
위상을 바꾸는 방식으로 설계하라.** 기존 기믹의 **위상 정렬**(예: 돌발 중 분출구 2기를
동위상으로) 은 동종 피크를 1→2로 올리므로 밴드 안에서 합법이고, 실제 압박은 크게 오른다.
정말 새 예고 기믹을 돌발에 붙이려면 **AMENDMENT 문서 + 스테이지별 재센서스**가
선행 조건이며, ash-march/cinder-sluice는 대상에서 제외해야 한다.

#### C4. 훈련 모드 → **밴드 충돌 없음. 격리 계약이 없다는 것이 위험 🔴**

훈련은 밴드를 깨지 않는다(기믹 0). 위험은 반대편이다: **현재 프롤로그가 "본편에 안 새는
것"을 보장하는 근거가 테스트 4건에 흩어져 있고, 계약 문서가 없다.** 현재 실측 격리:
`Hazards.Count == 0` · 스킬/대시 입력 무시 · 각인 무효 · 메타스탯/장비 무효 ·
`prologueDone` 불리언 1개만 기록 [direct spec].

훈련에 기믹·보상·진행도를 붙이는 순간 이 4겹이 전부 재검증 대상이 된다. Warframe이
"보상 0 · 진행도 0 · 기록 0"으로 이 문제를 통째로 없앤 것이 풀 전체의 최강 관례다
[indexed snippet].

→ **QA 권고: 훈련에 보상을 붙이려면 Warframe 기준선에서 벗어나는 **각 축마다** 골든
회귀를 하나씩 추가하라.** 어느 축을 열지는 designer/PM 결정이고, 축당 증명 비용은
test-plan §T1이 명시한다. 축을 안 열면 증명 비용은 0이다.

### 이 섹션이 만든 밴드 (게이트 입력)

| # | 밴드 | 값 | 근거 |
|---|---|---|---|
| S1 | 이벤트 레벨 예고 | **≥ 2.0s (120틱)**, 웨이브 경계 정렬 | DRG 비례 환산 상단 2.01s ≈ 인터미션 2.15s [indexed snippet + 계산] |
| S2 | 이벤트 간 최소 간격 | **≥ 30s** | L4D Relax 목표 30–45s 하한 [indexed snippet] |
| S3 | 노출 에피소드 누적 상한 | **≤ 30% max HP** (per-hit 상한과 별도로 신설) | 벽 DoT가 1.80s에 30% 도달 [direct spec + 계산] |
| S4 | 돌발의 동시 예고 기여 | **+0** (예고 기믹 추가 금지, 위상 정렬만 허용) | 7타이틀 중 0건이 예고 기믹을 추가 [indexed snippet] + LCM 센서스 여유 1 [계산] |
| S5 | 돌발 트리거 종류 | 고정 시간표 · 웨이브 경계 · 처치 수 · **플레이어 옵트인**. 체력 % 임계는 경계 조건 문서화 필수, 불가시 게이지 **금지** | RoR2/Hades 옵트인 [indexed snippet] · Elden Ring 임계 건너뛰기 [indexed snippet] · MH 불가시 게이지 부정 사례 [indexed snippet] |
| S6 | 훈련 격리 기본값 | 보상 0 · 진행도 0 · 기록 분리. 축을 열 때마다 골든 회귀 1개 추가 | Warframe Simulacrum [indexed snippet] + 현재 4겹 격리 [direct spec] |

### 이 섹션이 인용한 선행 결론 (재조사 안 함)

`.survey/meta-upgrade-gimmick-interaction/solutions.md`의 설계 규칙 3개는 이번에도 유효한
제약이며, 위 판정과 충돌하지 않는다: **면역 금지(저항까지)** · **무작위 발동 금지** ·
**사이드그레이드 우선**. 특히 두 번째가 MH 분노(불가시 게이지)를 부정 벤치마크로 만든
근거이고, S5의 "불가시 게이지 금지"가 그것의 돌발 버전이다.

### 큐레이션 출처 (이 섹션 추가분)

- `Assets/Scripts/Sim/CampaignTypes.cs`(CampaignSpec 상수 · CampaignStages 배치표),
  `SimTypes.cs`(`WaveIntermission` 2.15 · `PlayerMaxHealth` 100), `CinderSim.cs`(§HazardView
  3108-3127 예고 판정), `HackTypes.cs`(`HackSpec.Prologue*`),
  `Assets/Tests/EditMode/{HackSimTests,SigilTests,DungeonGoldenDigestTests}.cs` — [direct spec]
- DRG wiki(fandom) + reddit/steamcommunity: 스웜 경고 ~20초 3단 구조, Point Extraction 예외 — [indexed snippet]
- L4D AI Director 분석(scribd/valvesoftware developer wiki/steamcommunity): Relax 30–45초,
  패닉=스크립트 오버라이드, 종료=처치 수/도달 — [indexed snippet]
- RoR2 wiki(fandom) + reddit/steamcommunity: 난이도 계수·Director 크레딧·노치, 옵트인 텔레포터 — [indexed snippet]
- Vampire Survivors wiki + steamcommunity/reddit: 스테이지 제한시간 리퍼, 이후 매 1분, Hurry ½/Endless — [indexed snippet]
- Monster Hunter(reddit/gamespot/fandom): 불가시 분노 게이지, 피해 누적 트리거, 스태미나 연동 종료 — [indexed snippet]
- Elden Ring(커뮤니티 코퍼스): 50% 비하드코딩, 애니메이션 락에 의한 임계 건너뛰기 — [indexed snippet]
- Hades wiki(fandom)/gamerant/gamespot: Skelly 인형 무보상, Pact 조건 상(8/16/32 Heat) — [indexed snippet]
- Warframe wiki(fandom)/warframe.com/steamcommunity: Simulacrum Affinity 0·루트 미지속·진행도 미반영 — [indexed snippet]
- Street Fighter 6(thegamer/reddit/ultimateframedata): Frame Meter 훈련 전용, 경쟁 모드 비활성 — [indexed snippet]
- 위 [INFERENCE] 표기 값(특히 DRG 튜토리얼 1회성 보상, MH 대응 창 0, 모드별 기록 분리)은
  개발사 공표 수치가 아니다. `qa/gate-measurements.md`에 **측정값으로 인용 금지**.

## v1.7 — 진행 네비게이션 벤치마크 (run-id 20260807-progression-navigation)

2026-08-07 · game-qa (네비게이션 계측 가능성 담당) · Stage 1a 조사 산출물.

**레인 분담**: designer 레인(`design/trend-survey/`)이 "무엇을 채택할까"(빈도·신규성)를
조사한다. 이 절은 **"그것을 어떻게 측정할까"**만 담당한다. 두 레인이 같은 6타이틀 풀을
쓰기로 IRC 합의(2026-08-07, DesignerNavSurvey) — cycle-2 선례와 동일하게, 빈도표와
수치표가 같은 N 위에서 나란히 비교되게 하기 위해서다. designer 풀의 Hollow Knight는
로비형 메타 진행 화면이 없어 제외했고, 대신 Hades II를 넣어 **같은 스튜디오의 4년 뒤
개정판**을 A/B로 세웠다. 축 매핑: (a)↔designer N2, (c)↔N3, (d)↔N1.

### 계측 축 정의 (재현 가능하게)

형용사는 게이트를 못 넘으므로 네 축 전부 **개수**로 정의한다.

| 축 | 정의 | 세는 단위 | 세지 않는 것 |
|---|---|---|---|
| **(a) 다음 행동까지 조작 수** | 허브 진입 직후부터, 화면이 지목한 단일 다음 행동을 **커밋**하기까지의 입력 수 | 클릭·키 입력·스틱 이동 1회 = 1 | 눈으로 훑는 시간(스캔 비용). 지목이 없어 스캔이 필요하면 그 사실을 별도 표기 |
| **(b) 전체 진행 파악 화면 전환 수** | 진행에 관계된 **모든** 상태(진행 라인 + 강화 라인)를 최소 1회씩 화면에 띄우는 데 필요한 전환 수 | 스크롤 1페이지 = 1, 탭 전환 = 1, 별도 메뉴 진입 = 1 | 같은 화면 안의 시선 이동 |
| **(c) 잠금 사유 확인 조작 수** | 잠긴 항목 1개를 골라 **왜 잠겼는지**를 게임 화면만으로 알아내는 데 필요한 조작 수 | 위와 동일 | 위키·커뮤니티 조회. 게임 안에 정보가 없으면 **∞** |
| **(d) 동시 가시율** | 한 화면에 동시에 보이는 항목 수 ÷ 그 목록의 전체 항목 수 | 부분 가시 카드는 가시 높이 비율로 안 세고, **완전 가시만** 센다 | — |

**(c)의 ∞ 표기가 이 표의 핵심**이다. "조작을 많이 해야 안다"와 "게임 안에서는 영원히
모른다"는 정도 차이가 아니라 종류 차이라서, 큰 수가 아니라 ∞로 적는다.

### 우리 현재값 [OBSERVED — 코드 실측, `Assets/Scripts/View/LobbyView.cs`]

스크롤 기하는 계산으로 검증했다. **상수 정정(2026-08-07, PMNavRevenueMap 교차검증)**:
카드 y는 `Card(content, -6 - row*70, 68)`이다(`LobbyView.cs:684` 스테이지 / `:784`
등급 / `:808` 시련, 세 곳 동일 공식). 즉 **상단 인셋 6u · 피치 70u · 카드 높이 68u**.
콘텐츠 높이의 `+8`(`:669-670`)은 **말미 패드**이지 카드 y에 안 들어간다. 내 초판은
이 둘을 섞어 `8 + 70*row`로 계산했고 그건 틀렸다. 아래는 정정값이다.

| 행 | 카드 | [top, bottom] | 뷰포트(434u) 내 가시 | 가시 |
|---|---|---|---|---|
| 0–5 | 스테이지 1–6 | [6,74] … [356,424] | 68u | 100% |
| 6 | 스테이지 7 | [426,494] | 8u | **11.8%** |
| 7 | 스테이지 8 | [496,564] | 0u | **0%** |
| 8 | 스테이지 9 | [566,634] | 0u | **0%** |
| 9 | 등급선택 | [636,704] | 0u | 0% |
| 10–14 | 시련 1–5 | [706,774] … [986,1054] | 0u | 0% |

따라서 **완전 가시 카드는 6장/15장 = 40.0%**다. 픽셀 가시율 41.0%(434/1058)와 구분해
적는다 — 6.2장은 피치 나눗셈이고, 플레이어가 **읽을 수 있는** 카드는 6장이다.
정정 전후로 이 두 수(40.0% / 41.0%)와 아래 33.3%는 **바뀌지 않았다**. 바뀐 건 스테이지 7의
가시 비율(9% → 11.8%)과 첫 시련 카드 top(708 → **706**)뿐이다.

파생 사실 하나가 여기서 나온다: 스테이지 7·8·9는 가시 0~11.8%다. 즉 **cleared가
6·7·8일 때 "다음 스테이지"는 완전 가시가 아니다**. 프롤로그 완료 후 클리어 진행 상태
9가지 중 3가지(33.3%)에서 다음 목표가 스크롤 없이는 온전히 보이지 않는다.

### 타이틀별 계측표 (6타이틀 + 우리)

출처 등급은 이 파일 상단 §Provenance legend를 그대로 따른다. **(a)(b)(c)의 벤치마크
값은 전부 [indexed snippet + INFERENCE]**다 — 나는 이 타이틀들을 직접 조작해 클릭을
세지 않았다. 세어 본 척하지 않으려고, 각 칸에 **무엇을 근거로 그 수가 나오는지**를
같이 적는다. 우리 행만 [OBSERVED]다.

| 타이틀 | (a) 다음 행동 조작 수 | (b) 전체 파악 전환 수 | (c) 잠금 사유 조작 수 | (d) 동시 가시율 | 근거 |
|---|---|---|---|---|---|
| **Slay the Spire** | **≈1** — 맵이 기본 화면(또는 `M` 1회), 인접 노드 클릭 1회로 커밋 | **0** — 액트 전체가 한 화면 | **0** — 잠금이 문자열이 아니라 **간선 부재**로 표현. 갈 수 없는 노드는 선이 없어서, 사유가 곧 그림 | **100%** (액트 내) | 맵은 액트 시작 시 전부 생성·전부 표시, 진행해도 추가 공개 없음 [indexed snippet] |
| **Dead Cells** | **≈1** — 바이옴 맵에서 다음 문 선택 | **≥2** — 맵과 룬 목록이 다른 층(룬은 메인 메뉴 세이브 슬롯) | **∞** — 맵이 연결은 그리지만 **어느 룬이 필요한지 안 적는다**. 커뮤니티 위키가 사실상의 UI | **100%** (그래프 전체, 미방문은 회색) | 맵은 방문=컬러/미방문=회색으로 전체를 보여주지만 차단 사유는 미표기; 위키 의존이 표준 [indexed snippet] |
| **Rogue Legacy 2** | **1–2** — 저택 트리에서 노드 선택 후 구매 | **≥2** — 트리가 커서 패닝 필요 | **0–1** — 선행 조건이 **트리 간선**으로 구조 표현되고, 저택 레벨 게이트는 노드에 **수치로** 표기(예: Lv.12) | **<100%** — 패닝 필요 | 노드는 선행 업그레이드 구매 전까지 잠김/은닉, 일부는 저택 레벨 요구 [indexed snippet] |
| **Hades** | **1–2** — 거울 앞으로 이동 후 행 선택 | **4–5** — 거울/계약자/운명의 목록/무기대/코덱스가 **물리적으로 다른 스테이션** | **혼합: 0 또는 ∞** — 거울 행 잠금은 케런의 열쇠 비용이 화면에 표기(0). 반면 초록 면 해금(다크니스 300 + Nyx 대화)과 다수 예언의 선행 조건은 **화면에 안 나온다**(∞) | 거울 12행은 한 화면 [indexed snippet] | 거울은 12행/24탈렌트 단일 화면, 행 해금은 열쇠, 초록 면은 300 다크니스+대화 [indexed snippet]. 예언은 대부분 이진 표시이고 "5/10" 같은 카운터를 안 준다 [indexed snippet] |
| **Hades II** | **1–2** — 동일 구조 | **4–5** — 동일 구조(교차로) | **0 (재료 축에 한해)** — 가마솥 주문 화면이 **필요 재료와 보유량을 인라인 표기**하고 **부족분을 빨강으로** 칠한다. 전작의 ∞를 색으로 닫았다 | 주문 목록은 스크롤 | 부족 재료 빨강 표기 [indexed snippet]. 단 운명의 목록 자체는 여전히 이진이고 수치 카운터가 없다 [indexed snippet] |
| **Vampire Survivors** | **≈1** — 캐릭터 선택 | **≥3** — 컬렉션 / 언락 / 시크릿이 **서로 다른 메뉴** | **1–2** — 언락 메뉴로 전환 후 해당 항목까지 스크롤. **컬렉션에는 사유가 없고 언락 메뉴에만 있다** | **낮음** — 긴 스크롤 그리드 | 컬렉션은 보유 기록일 뿐 해금 안내가 아니며, 요구 조건은 별도 "Unlocks" 메뉴 소관 [indexed snippet] |
| **Cinder Court (현재)** | **0회 지목 없음** — 화면이 다음 행동을 **지목하지 않는다**. 조작 수가 아니라 **스캔 비용**이 든다: 6장을 읽고 상태 문자열 3종을 비교해 스스로 고른다. cleared 6·7·8이면 그 카드가 화면 밖이라 스크롤 1회가 **선행** | **5** = SORTIE 스크롤 2 + SANCTUM 탭 전환 3 | **∞** — `StageEntry.PrereqId`가 데이터에 있는데 화면에 없다. 상태 문자열은 `"잠김"` 하나뿐 | **40.0%** (완전 가시 6/15). SANCTUM은 **25%** (4탭 중 1탭) | [OBSERVED] `LobbyView.cs:241` 상태 3종, `StageCatalog.cs:526` 해금 판정, ScrollRect Scrollbar 미할당 |
| **Cinder Court (착지 후)** | **1** — 지목이 있는 그룹이 자동 펼쳐지고(`RefreshGroupHeaders` → `GroupOfTarget`) 그 카드가 100% 가시. 스캔 비용 → 조작 1 | **3–4** = 막 그룹 전환 0–1(자동 펼침) + SANCTUM 탭 3 | **0** — `LockReasonFor` enum 3값 + `StageSubLine`이 카드에 인라인(`• 점화 훈련 필요` / `• 선행: {스테이지명}`) | **100%** (막 그룹, 완전가시 7/7 · 콘텐츠 416u < 뷰포트 434u, 여유 18u) · 훈련장은 70.0%(7/10, 픽셀비 69.3%) | [OBSERVED] 접이식 기하 재측정(designer 교차검증), `ProgressionGuide.cs` |

### 이 표에서 나오는 사실 3개

**1. (c)=∞는 우리만의 결함이 아니다 — 그러나 (c)와 (d)가 동시에 나쁜 건 우리뿐이다.**
Dead Cells도 (c)=∞다. Hades도 절반은 ∞다. 즉 "잠금 사유를 안 적는다"는 이 장르에서
드문 일이 아니다. 그런데 풀을 두 축으로 교차하면 패턴이 하나 뿐이다:

| | (d) 높음 (≥100% 또는 전체 그래프) | (d) 낮음 |
|---|---|---|
| **(c) 낮음 (0~2)** | Slay the Spire, Rogue Legacy 2 | Hades II, Vampire Survivors |
| **(c) = ∞** | Dead Cells, Hades(절반) | **Cinder Court (현재) ← 풀에서 유일** |

(c)=∞인 타이틀은 **전체 지도를 보여주는 것으로 갚는다**. 사유를 안 적는 대신 구조를
통째로 보여줘서, 플레이어가 위상으로 추론하게 한다. 우리는 사유도 없고 60%가 화면
밖이다. **둘 중 하나는 닫아야 벤치마크 풀 안으로 들어온다** — 어느 쪽을 닫을지는
designer 레인의 채택 결정이고, 여기서는 "현재 조합이 풀 밖"이라는 사실만 남긴다.


**착지 후 위치 갱신**: 접이식이 (d)를 40.0% → **100%**(막 그룹)로 올렸고 `LockReasonFor`
enum + `StageSubLine` 인라인이 (c)를 ∞ → **0**으로 닫았다. 두 축을 **동시에** 닫아서
표의 좌상단 칸(StS·RL2와 같은 자리)으로 이동했다. "둘 중 하나는 닫아야 풀 안으로
들어온다"고 적었는데 **둘 다 닫혔다** — 그리고 designer 규칙 순서(스크롤 제거가 선행)가
그걸 가능하게 했다. 순서를 뒤집었으면 사유 문자열이 1058u를 더 늘려 (d)가 40% 아래로
떨어졌을 것이다.

**남은 이탈은 (a)의 기전 하나**다. 우리는 여전히 **추천(지목)** 으로 (a)=1을 만들고 풀의
다섯 타이틀은 **공개**로 만든다. 다만 §1d가 예고한 실패 모드(비용이 (b)로 이동)는
발생하지 않았다 — (b)도 5 → **3–4**로 내려갔다. 접이식이 공개의 역할을 겸했기 때문이고,
즉 우리는 **추천과 공개를 동시에** 했다. 풀에 그 조합의 선례가 없다는 사실은 그대로이나,
"추천만 하고 공개를 안 한" VS형 실패는 아니다.

**선례 부재의 원인을 구분해야 한다 — 내 "이탈 3개" 읽기가 거칠었다.**
나는 N2 0/6 · N11 0/17 · N-A UI 0건을 "관례 이탈 3개가 한 화면에 겹친다"로 묶어 위험
신호로 읽었다. designer가 마감에서 구분을 세웠고 내 측정이 그걸 지지한다:

| 선례 0건의 원인 | 읽는 법 | 사례 |
|---|---|---|
| **나쁜 조합이라 아무도 안 함** | 진짜 위험 신호 | 이번 풀에서 확인된 사례 없음 |
| **그 조합을 할 지면이 없었음** | 위험 신호 아님 | **N2+N8 동시 채택** — 추천과 공개를 같이 하려면 둘 다 놓을 공간이 필요한데 평면 스크롤 리스트엔 없다 |
| **구조가 없어서 가질 수 없었음** | 위험 신호 아님 | N11 (designer가 이미 이 구분을 함) |

**첫 칸이 비어 있다는 게 이 표의 결론이다.** designer가 0/N을 네 개 냈는데
(**N2 0/6 · N11 0/17 · N-A UI 0건 · N5 1/6**) **"시도됐다가 버려졌다"는 증거는
하나도 없다.** 즉 우리가 본 모든 0은 2·3번 칸이었고, 1번 칸은 표본에서 관측되지 않았다.
그런데 나는 §1c에서 그 0들을 "관례 이탈이 한 화면에 겹친다"로 묶어 위험으로 읽었다.
**0의 원인을 안 묻고 위험으로 환산한 것**이 오류였다.

**실무 규칙으로 닫는다** (designer와 합의):
**0/N을 인용할 때는 원인 칸을 같이 적는다. 원인을 모르면 "선례 없음"까지만 쓰고
위험 판정은 하지 않는다.**

이게 두 레인의 경계를 정확히 그린다 — **빈도표는 방향을 주고, 원인은 코드·산술·측정이
준다.** designer의 198셀은 전자를 했고, 후자는 내 계측(접이식 416u가 지면을 만들었다는
실측)과 PM 산술(도달성 70 < 임계 72)이 했다. 어느 한쪽만으로는 0을 해석할 수 없다.

**우리 측정이 두 번째 칸의 사례를 만들었다.** 접이식이 416u를 만들어 지면이 생겼고,
그래서 추천(지목)과 공개(막 헤더가 위치 + 다음 재판이 어느 막인지 동시 표시)를 같이 할
수 있었다. §1d가 예고한 VS형 실패가 안 난 이유가 이거다.

**계측 레인의 교훈**: 빈도표는 "몇 개가 채택했나"를 세지만 **"왜 안 했나"는 못 센다**.
선례 0건을 위험으로 환산하려면 그 0의 원인을 따로 물어야 하고 그건 빈도 데이터 밖이다.
이번엔 내 (a)(b)(d) 실측이 그 밖을 짚었다 — **선례 부재가 실패를 뜻하지 않는다는 걸
측정이 보여준 사례**다.

**1b. designer 빈도표와 합치면 (d)의 해법이 뒤집힌다.**
공유 풀을 쓴 이유가 이것이다. DesignerNavSurvey 빈도표(2026-08-07 IRC, 전문
`.survey/progression-navigation/`)를 내 축에 붙이면:

| 내 축 | designer 축 | 채택률 | QA가 읽는 함의 |
|---|---|---|---|
| (d) 동시 가시율 | N1 위치 지시자 | **11/11 = 100%** | 만장일치. 그런데 **11/11 전부 스크롤바가 아니라 공간 유계 배치**(그리드/트리/맵)로 풀었다. **표시 없는 스크롤 리스트는 18타이틀 중 0건** |
| (a) 다음 행동 조작 수 | N2 단일 지목 | **0/6 = 0%** | 아무도 안 한다. Hades는 "목표 마커·퀘스트 로그 없음"을 **의도적 설계로 명시**. 변형 1건(Hades II Forget-Me-Not)도 **시스템 추천이 아니라 플레이어 수동 핀** |
| (c) 잠금 사유 조작 수 | N3 잠금 사유 | **4/15 = 27%** (변형 포함 9/15 = 60%) | **미채택 6건 중 5건이 실패 사례**. 유일한 성공적 미채택은 Hades인데 NPC 대사·`!` 마커라는 **대체 채널**이 있다 |

세 줄이 각각 방향을 바꾼다:

- **(d): 해법은 스크롤바 추가가 아니라 스크롤 제거다.** 내 T-B9는 "스크롤 가능함이
  화면에 표시되는가"를 묻는데, 빈도표는 **아무도 그 문제를 스크롤바로 풀지 않았다**고
  답한다. 우리 41.0%는 장르 표준 미달이고 표준적 처방은 15장을 스크롤 밖으로 빼는
  게 아니라 **한 화면에 들어오는 배치로 바꾸는 것**이다. T-B9는 그대로 두되(지시자
  0개는 여전히 결함), 이 항목이 통과해도 (d)가 해결되는 건 아님을 기록한다.
- **(a): 0/6은 "하지 마라"가 아니라 "가드 없이 하지 마라"다.** 채택률 0%인 기능을
  넣는다는 건 장르 관례를 벗어난다는 뜻이므로, 내 T-A1(지목 슬롯 정확히 1개)이
  **관례 이탈을 감당할 만큼 엄격한지**가 더 중요해진다. 0개도 2개도 안 되는 이유가
  여기 있다 — 아무도 안 하는 걸 하면서 흔들리기까지 하면 근거가 없다.
- **(c): 우리는 Hades의 예외 조건을 못 만족한다.** Hades가 사유를 안 적고도 버티는 건
  대체 채널이 있어서다. **우리 로비의 대체 채널은 0개**다(NPC 없음, 마커 없음, 툴팁
  없음). 즉 우리에게 (c)=∞는 Hades형 "의도된 생략"이 아니라 **미채택 6건 중 실패 5건
  쪽**이다.

designer 측 계측 가능 실측 1건도 받았다: **3부작 접이식 헤더는 그룹 3개면 ≤51.3u라
44u 탭이 가능하지만, 그룹 4개면 ≤38.5u로 래칫 위반**이다. 3부작이 정확히 3그룹이라
아슬하게 성립한다 — 그룹을 하나라도 늘리면 T-C1이 막는다. 이건 designer 설계 자유도의
상한이므로 협상에 올릴 값이다.

**1c. (a) 축의 숨은 변수 — "배지로 안내"와 "게이트로 축소"는 같은 조작 수가 아니다.**
PM이 "각인 vs 장비 중 무엇을 먼저 사라고 UI가 추천하는 사례가 있나"(N-A)를 물었고
designer가 표본을 그 질문으로 다시 훑었다. 결과: **UI 추천 사례 0건**, 대신 비UI 기전 3종:

| 기전 | 사례 | 무엇을 조작하나 |
|---|---|---|
| 강제 순서 | Hades Mirror(2개씩 배치 해금, 건너뛰기 불가) | 가용성 |
| 티어 게이트 | Cult of the Lamb(N개 구매→다음 티어) · CotDG(Blood Emblem 1·3·5) · WoL(절반 구매) | 가용성 |
| 강제 소비 넛지 | Dead Cells 파란 문 | 가용성 |

셋 다 **배지가 아니라 가용성을 조작한다**. 이게 내 (a) 축에 직접 걸린다: 게이트로 선택지를
줄인 화면은 조작 수가 낮게 나오지만 그건 **안내가 좋아서가 아니라 고를 게 없어서**다.
두 경로를 같은 수로 적으면 축이 거짓말을 한다. 그래서 (a)를 쓸 때는 **동시 선택 가능
항목 수를 같이 적어야** 비교가 성립한다 — 조작 수 단독은 게이트 설계에 유리하게 편향된다.

우리 SANCTUM은 게이트가 없고 4탭이 항상 열려 있으므로 **배지 경로**다. 즉 위 3종 어느
것과도 같은 종류가 아니고, 벤치마크에 배지 선례가 0건이라는 뜻이다 — N2(단일 지목) 0/6과
같은 성격의 관례 이탈이다. T-A3·T-A4가 그 이탈을 감당할 유일한 근거라는 점이 다시 확인된다.

**1d. (a)와 (d)는 독립 축이 아니다 — 풀 전체에서 (a)를 낮춘 기전이 전부 (d)다.**
designer의 마감 정리("장르는 '다음 무엇'에 **추천이 아니라 공개**로 답한다")를 내 계측표에
역으로 대입해봤다. 내 네 축은 독립적으로 설계했는데, 데이터가 그렇지 않다고 말한다:

| 타이틀 | (a) | (d) | (a)를 낮춘 기전 |
|---|---|---|---|
| Slay the Spire | ≈1 | 100% | 공개 — 액트 전체 한 화면 |
| Dead Cells | ≈1 | 100% | 공개 — 그래프 전체(미방문 회색) |
| Rogue Legacy 2 | 1–2 | <100% 패닝 | 공개 — 트리 위상 |
| Hades | 1–2 | 거울 12행 한 화면 | 공개 (단 스테이션 분산) |
| Hades II | 1–2 | 주문 목록 스크롤 | 공개 + 색(부족분 빨강) |
| Vampire Survivors | ≈1 | 낮음 | **공개 아님** — 메뉴 분산 |

**6타이틀 중 추천으로 (a)를 낮춘 사례가 0건**이다(designer N2 0/6과 독립적으로 같은 값).
다섯은 전부 **(d)를 올려서 (a)를 낮췄다**. 즉 내 두 축은 직교하지 않고 **한쪽이 다른 쪽의
수단**이다.

**Vampire Survivors가 반례처럼 보이지만 오히려 증거다.** (a)≈1인데 (d)가 낮은 유일한
타이틀인데, 대신 **(b)=≥3으로 풀 최악**이다. 공개 없이 (a)만 낮추면 비용이 사라지는 게
아니라 **(b)로 이동한다**. 세 축을 같이 보면 총비용은 보존된다.

**우리에게 적용하면**: 우리는 (d)=40.0%를 유지한 채 **추천(배지·지목)으로 (a)를 낮추는**
경로다. 풀에서 아무도 안 한 조합이고, VS 사례가 예고하는 실패 모드는 "(a)는 낮아지는데
(b)가 커진다"이다. 우리 (b)는 이미 5로 풀 최악급이다.

이건 설계 판단이 아니라 **계측 귀결**이다 — 채택 결정은 designer 소관이고, 여기서는
"(a)만 낮추는 개선은 (b)에서 되돌아온다"는 축 간 관계만 기록한다. T-B3(자동 스크롤)과
T-B9(스크롤 지시자)가 (d)/(b)를 건드리는 유일한 판정이라는 점이 이 관계에서 다시 중요해진다.

**2. (c)를 닫는 방법은 두 가지고, 비용이 다르다.**
- **구조로 닫기**(StS·RL2): 잠금을 문자열이 아니라 **간선/트리 위상**으로 표현. 문자열
  0개 추가 → 폰트 글리프 위험 0, 문자열 잘림 위험 0. 대신 레이아웃 변경이 크다.
- **문자열로 닫기**(Hades II·VS): 사유·비용·보유량을 **인라인 텍스트+색**으로. 레이아웃
  변경 작음. 대신 **신규 한국어 문자열 → 폰트 서브셋 재생성 필요**(실측 폰트 498 /
  소스 499 — 컨텍스트의 "497"은 낡은 수치다), 그리고 카드 폭 안에서 잘릴 위험이 생긴다.
  두 위험 다 T-A/T-B에 검사 항목으로 넣었고, **서브셋 재생성 누락은 이미 라이브 결함으로
  발생해 있다**(`·` U+00B7, test-plan T-A8 실행 결과).

**3. Hades → Hades II A/B가 알려주는 것: 부족분을 색으로 칠하는 건 값싸고 효과가 크다.**
같은 스튜디오가 4년 뒤 바꾼 지점이 정확히 "필요량 대비 보유량을 인라인 표기 + 부족은
빨강"이다 [indexed snippet]. 이건 **우리 SANCTUM 배지 부재와 정확히 같은 문제**다.
우리 로비는 이미 `data.Relics >= cost`를 탭 안에서 계산해 버튼 `interactable`을 끄고
있다(`LobbyView.cs:301`, `:342`) — **판정은 이미 있고 탭 밖으로 안 나올 뿐**이다.
배지는 새 계산이 아니라 기존 불리언의 재배치다. [OBSERVED]

### 색 대비 실측 [OBSERVED — 계산]

worldview 팔레트를 숯 바탕 `rgb(5,4,9)` 위에서 WCAG 2.x 상대휘도로 계산했다:

| 전경 | 대비비 | AA 본문(4.5) | AA 큰글씨(3.0) |
|---|---|---|---|
| 골드 `#DDC869` | 12.20:1 | 통과 | 통과 |
| 시안 `#2CADD6` | 7.84:1 | 통과 | 통과 |
| 엠버 `#F3592C` | 6.11:1 | 통과 | 통과 |
| **잠금 회색 `(0.42,0.45,0.58)`** | **4.37:1** | **미달** | 통과 |

**잠금 회색이 AA 본문 기준에 0.13 모자란다.** 지금은 `"잠김"` 두 글자(11pt)에만 쓰여서
영향이 작지만, **잠금 사유 문자열을 이 색으로 넣으면 읽어야 할 본문이 미달 색으로
들어간다**. 사유 텍스트는 잠금 회색을 쓰면 안 된다. 이건 designer 레인에 넘기는 제약
이지 내 결정이 아니다 — 다만 **수치는 확정**이고 T-A6이 이걸 게이트로 잡는다.
숯 바탕의 알파 0.72는 뒤에 깔린 색에 따라 실효 대비를 **더 낮추기만** 하므로, 4.37은
상한이다. 즉 실제 화면에서는 이보다 나쁘다.

**추가 실측 — 잠김 행의 CanvasGroup alpha가 대비를 한 번 더 깎는다.**
잠긴 스테이지·시련 행은 `alpha = 0.45`로 흐려진다(`LobbyView.cs:244` 스테이지,
`:852` 시련 — 시련은 `open = data.PrologueDone`). 이 알파를 숯 위 합성에 반영하면:

| 상태 | 합성 후 대비 | AA 본문 4.5 |
|---|---|---|
| 해금 행 (alpha 1.00) | 4.37:1 | 미달 |
| **잠김 행 (alpha 0.45)** | **1.71:1** | **크게 미달** |

즉 **가장 읽혀야 할 대상(잠긴 것의 사유)이 가장 안 읽히는 색으로 간다.** 1.71:1은
AA 큰글씨 3.0에도 못 미친다. 사유 텍스트를 잠김 행 안에 넣을 거라면 색만 바꾸는 걸로는
부족하고 **그 텍스트를 alpha 감쇠에서 빼야** 한다. 이 사실은 PMNavRevenueMap이
`mastery_surface_rows_visible` 밴드 결함을 잡는 근거가 됐다(아래).

**밴드 상호작용 — PM 밴드가 다른 게이트를 깨는 경로가 있었다.**
초판 `mastery_surface_rows_visible`에는 `PrologueDone` 전제와 `interactable` 조건이
없었다. 그래서 **프롤로그 미완료 세이브에서 alpha 0.45 잠김 시련 행이 "보이는 행"으로
계수되어 밴드를 충족시키면서, 동시에 1.71:1로 T-A6을 깨는** 경로가 열려 있었다.
PM이 metric에 두 전제를 추가해 닫았다. QA 계측이 PM 밴드의 결함을 잡은 사례라
기록해 둔다 — 밴드가 다른 게이트를 깨는 방향으로 만족될 수 있으면 그건 밴드 결함이다.

### 출처

- Slay the Spire 맵 전체 공개 — steamcommunity / untapped.gg / fandom [indexed snippet]
- Dead Cells 바이옴 맵·룬 미표기, 위키 의존 — deadcells.wiki.gg / fandom / reddit [indexed snippet]
- Rogue Legacy 2 저택 트리 선행·저택 레벨 게이트 — ign / gamepur / fandom [indexed snippet]
- Hades 거울 12행·열쇠·다크니스 300+Nyx — fandom / 커뮤니티 가이드 [indexed snippet]
- Hades / Hades II 운명의 목록 이진 표시, 수치 카운터 부재 — 커뮤니티 Q&A [indexed snippet]
- Hades II 가마솥 부족 재료 빨강 표기 — shacknews / gamespot / fextralife [indexed snippet]
- Vampire Survivors 컬렉션 ≠ 언락 메뉴 — steamcommunity / thegamer / wikihow [indexed snippet]
- 우리 값 — `Assets/Scripts/View/LobbyView.cs`, `Assets/Scripts/View/StageCatalog.cs` [OBSERVED]

**인용 금지 경고**: 위 (a)(b)(c) 벤치마크 수치는 개발사 공표값이 아니라 문서화된 UI
구조에서 유도한 **추정 조작 수**다. `qa/gate-measurements.md`에 **측정값으로 인용 금지**.
우리 행(40.0%, 5회, ∞, 4.37:1)만 측정값이다.

## v1.8 — 인게임 안내 벤치마크 (run-id 20260807-ingame-guidance)

2026-08-07 · game-qa (안내 계측 가능성 담당) · Stage 1a 조사 산출물. AMENDMENT #9 예정.

**레인 분담**: designer 레인(`GuidanceSurvey` → `.survey/ingame-guidance/`)이 "무엇을
채택할까"(빈도·신규성)를 조사한다. 이 절은 **"그것을 어떻게 측정할까"**만 담당한다.
v1.7 선례대로 IRC로 표본 풀을 합의했다(2026-08-07).

| 구분 | 타이틀 | 근거 |
|---|---|---|
| **공유 5** | Hades · Dead Cells · Risk of Rain 2 · Vampire Survivors · Slay the Spire | 양 레인 풀 교집합. 빈도표와 수치표가 같은 N 위에서 비교되게 |
| **QA 단독 1** | Deep Rock Galactic: Survivor | designer 19풀에 없음. "튜토리얼이 거의 없는" 극단값 확보용 |
| **designer 풀에서 당김 2** | Into the Breach · Returnal | **정지를 쓰는 계열의 상한을 재기 위해**. 공유 5는 전부 무정지라 우리 정지 예산 8의 비교군이 안 된다 |

총 **8타이틀**. ItB/Returnal을 당긴 판단은 결과적으로 절반만 맞았다 — §3에 적는다.

### 계측 축 정의 (재현 가능하게)

v1.7과 같은 원칙: 형용사는 게이트를 못 넘으므로 다섯 축 전부 **개수 또는 비율**로 정의한다.

| 축 | 정의 | 세는 단위 | 세지 않는 것 |
|---|---|---|---|
| **(G1) 첫 플레이 N분까지 정지 횟수** | 첫 런 시작부터 진행이 **플레이어 입력을 기다리며 멈추는** 횟수 | 모달·전면 오버레이·강제 확인 1회 = 1 | 무정지 토스트. 플레이어가 **자기 의지로** 연 메뉴 |
| **(G2) 안내 1건당 단어 수** | 정지 1건이 화면에 띄우는 본문 단어 수(제목 제외) | 공백 분리 토큰. 한국어는 어절 | 도감 상세 본문(요청해야 나옴) |
| **(G3) 도감 열람 조작 수** | 임의 항목 1개의 설명에 도달하는 입력 수. **런 중 / 로비를 따로 잰다** | 키·클릭·스틱 1회 = 1 | 게임 안에 없으면 **∞** |
| **(G4) 런 중 이탈 조작 수** | 전투 중 로비/메뉴로 **의도적으로** 나가기까지의 입력 수 | 위와 동일 | 사망·클리어(비자발). 경로 없으면 **∞** |
| **(G5) 커버리지율** | 게임이 **게임 안에서** 설명하는 메커니즘 수 ÷ 플레이어가 첫 런에 만나는 전체 메커니즘 수 | 설명 = 이름 외 효과·조건 1줄 이상 | 별칭만 있는 것(우리 `해류 숙달`이 이 사례) |

**∞ 표기 규칙은 v1.7과 동일**하다. "조작이 많다"와 "게임 안에서는 영원히 불가"는
정도 차이가 아니라 종류 차이라서 큰 수가 아니라 ∞로 적는다. G3·G4에 적용된다.

**G5의 분모 주의**: 타이틀마다 메커니즘 총수가 다르므로 절대수 비교는 무의미하다.
비율만 비교하고, 분모는 각 행에 명시한다.

### 우리 현재값 [OBSERVED — 코드 실측]

| 축 | 값 | 근거 |
|---|---|---|
| **G1 정지 횟수** | **0** | 정지 기전 자체가 없다. `HudView.cs:958-972` 4단계는 무정지 토스트이고 프롤로그 전용 |
| **G2 단어 수** | **해당 없음** (정지 0건) | 참고: 기존 토스트 4건 평균 **5.5어절** (예: `이동 — W A S D 또는 방향키` 6어절) |
| **G3 도감 (런 중/로비)** | **∞ / ∞** | 도감이 존재하지 않는다. 양쪽 다 부재 |
| **G4 이탈 조작 수** | **∞** | D1. 전투 HUD에 이탈 컨트롤 0. 승리·사망만이 출구 |
| **G5 커버리지율** | **2/23 = 8.7%** | 이동·타격만. 조작 7 + 아이템 4 + 기믹 6 + 승패 2 + 돌발 2 = 21종 설명 0곳 |

**우리 행은 5축 중 3축이 ∞ 또는 최하위다.** v1.7에서는 ∞가 1축(잠금 사유)이었는데
이번엔 3축이다. 안내 레인이 진행 레인보다 결함이 깊다.

### 타이틀별 계측표 (8타이틀 + 우리)

출처 등급은 이 파일 상단 §Provenance legend를 따른다. **G1은 designer 레인 G1축
실측을 인용**하고(귀속: `GuidanceSurvey`, `.survey/ingame-guidance/solutions.md`
§정지 빈도 정량화), G2–G5는 문서화된 UI 구조에서 유도한 **[indexed snippet +
INFERENCE]**다. 나는 이 타이틀들을 직접 조작해 클릭을 세지 않았다. 우리 행만 [OBSERVED].

| 타이틀 | G1 첫런 정지 | G2 1건당 단어 | G3 도감 (런중/로비) | G4 이탈 조작 | G5 커버리지율 | 근거 |
|---|---|---|---|---|---|---|
| **Hades** | **0** | — | **1 / 1** — `C` 키 1회로 코덱스. 런 중 열람 가능 | **2–3** — 일시정지 → 포기/타르타로스 복귀 | **높음** — 코덱스가 신·적·지역을 항목별로 기술. 단 **3회 탈출 실패 후 아킬레우스에게 받아야** 열린다 | 코덱스 기본 `C`, 첫 구역·NPC 근접·일부 전투 시작 시 열람 제한 [indexed snippet] |
| **Dead Cells** | **0** | — | **1 / 1** — 인벤토리에서 아이템 설명 인라인 | **2–3** — 메뉴 → 종료(런 소실) | **중간** — 아이템 설명은 있으나 룬 요구조건 미표기(v1.7 (c)=∞와 동일 구멍) | 위키 의존이 표준 [indexed snippet] |
| **Risk of Rain 2** | **0** | — | **∞ / 1** — **로그북은 메인 메뉴 전용. 런 중 접근 불가** | **2–3** — ESC → 종료 | **낮음–중간** — 아이템 툴팁은 `Tab`에 있으나 로그북 상세는 런 밖. BetterUI 모드가 사실상 표준 | 일시정지 메뉴에 로그북 진입로 없음 [indexed snippet] |
| **Vampire Survivors** | **0** | — | **∞ / 1–2** — 컬렉션은 메인 메뉴. 런 중 열람 경로 없음 | **3** — 일시정지 → 옵션 → 종료. **진행 보존**(몰수 없음) | **낮음** — 무기 진화 조건이 게임 안에 거의 없음 | 종료는 안전 이탈이고 골드·언락 유지 [indexed snippet] |
| **Slay the Spire** | **0** | — | **1–2 / 1–2** — 카드·유물 마우스오버 인라인 | **3 + 확인** — ESC → Abandon Run → **확인 모달**. 오조작 제보 다수 | **높음** — 카드·유물 텍스트가 규칙 전문을 담는다(장르 최상위) | Abandon Run에 확인 다이얼로그 존재, `Save and Quit`과 인접해 오조작 보고 [indexed snippet] |
| **DRG: Survivor** | **0** | — | **1–2 / 1–2** — 일시정지 중 무기 호버로 상세 스탯 | **2–3** — 일시정지 → 종료 | **낮음** — 정식 튜토리얼 부재. 스탯 버킷·태그 상호작용은 위키 소관. 서브클래스 선택창 팝업이 거의 유일한 인게임 설명 | 단계별 튜토리얼 없음, 커뮤니티 위키가 1차 학습 경로 [indexed snippet] |
| **Into the Breach** | **1** | **다수(약 2분 전체)** | 1–2 / 1–2 | 2–3 | 높음 | designer G1 실측. **거절 가능** [GuidanceSurvey] |
| **Returnal** | **0** (정지 아님) | 오버레이 다수 | 1 / 1 | 2–3 | 중간 | designer G1 실측. **팝업은 많으나 전투를 안 멈춤. 끌 수 없다는 게 최대 불만** [GuidanceSurvey] |
| **Cinder Court (현재)** | **0** | — | **∞ / ∞** | **∞** | **8.7%** (2/23) | [OBSERVED] `HudView.cs:958-972`, `InputAdapter.cs:5-7`, `SimTypes.cs:18` |
| **Cinder Court (설계안)** | **8** | 미정 — §3이 상한을 준다 | 1–2 / 1–2 (도감 양쪽) | 2 + 확인 모달 | 100% (23/23) | 확정 스펙 |

### 이 표에서 나오는 사실 4개

**1. G3(도감)에서 우리 설계안은 풀 최상위로 간다 — 그런데 그 자리는 비어 있지 않다.**
런 중 도감 접근을 두 축으로 교차하면:

| | 런 중 열람 가능 | 런 중 열람 불가(∞) |
|---|---|---|
| **로비 열람 가능** | Hades, Dead Cells, StS, DRG:S, Returnal, ItB | **RoR2, Vampire Survivors** |
| **로비 열람 불가(∞)** | (없음) | **Cinder Court (현재) ← 풀에서 유일** |

**8타이틀 중 어느 쪽도 못 여는 건 우리뿐이다.** 그리고 v1.7의 (c)=∞ 때와 달리
이번엔 "구조로 갚는" 대안이 없다 — 도감이 없다는 건 대체 채널이 없다는 뜻이고,
RoR2·VS도 최소한 **로비에서는** 연다. 확정 스펙의 "로비 + 인게임 양쪽"은 이 표에서
**좌상단 6타이틀 그룹으로의 합류**이지 관례 이탈이 아니다. **G3에 한해 설계안은 안전하다.**

**2. G4(이탈)에서 확인 모달은 관례이고, 몰수는 관례가 아니다 — 그러나 몰수 판단은 유지된다.**

| 타이틀 | 확인 모달 | 이탈 시 보상 |
|---|---|---|
| Slay the Spire | **있음** | 런 소실(패배 처리) |
| Vampire Survivors | 없음(옵션 경유가 사실상 지연) | **전액 보존** |
| Dead Cells | — | 런 소실 |
| RoR2 / Hades / DRG:S | — | 런 소실 |
| **Cinder Court (설계안)** | **있음** | **몰수** |

확인 모달은 StS 선례가 있고 **그 선례가 "없으면 사고 난다"는 증거까지 같이 있다** —
StS는 모달이 있는데도 `Save and Quit`과 인접해서 오조작 제보가 나온다 [indexed snippet].
즉 모달은 필요조건이지 충분조건이 아니고, **배치**가 나머지를 결정한다. 이게 §4의
D2 감사와 직접 이어진다: 우리 사망 패널은 이미 두 버튼이 26u 겹쳐 있다.

몰수 쪽은 선례가 얇지만(대부분 "런 소실"이라 몰수와 구분이 안 됨) **VS의 전액 보존이
반례로 존재**한다. 다만 VS는 사망해도 보존이라 이탈만 특별대우하는 게 아니다. 우리는
`GameDirector.cs:614-626`이 **패배 시 유물을 적립**하므로 이탈만 몰수하면 두 종료 경로의
보상이 갈린다 — 이건 벤치마크가 아니라 익스플로잇 산술이 정하는 문제이고, 확정 스펙의
판단(파밍 경로 차단)이 이 표로 뒤집히지 않는다. **관례 이탈이되 근거가 장르 밖에 있다.**

**3. G1은 designer 실측이 우리 8을 대역 밖으로 판정했다 — 그리고 판정 축은 횟수가 아니다.**
designer G1축 19타이틀 실측(2026-08-07 IRC 인용):

| 지표 | 값 |
|---|---|
| 진행을 막는 안내를 쓰는 타이틀 | **5 / 19 = 26%** |
| 첫런 정지 횟수 중앙값 | **0** |
| 최빈값 | **0** |
| 확인된 최댓값 | **1** |
| **우리 설계안** | **8** |

**8은 관측된 최댓값의 8배다.** 그러나 designer가 같은 메시지에서 더 중요한 걸 줬다:
**혹평의 축은 횟수가 아니라 총 소요 시간**이다. Enter the Gungeon(1회, 5–10분)과
Cult of the Lamb(1회, 10–15분)은 **정지 1회인데도 과잉으로 욕먹었다**. 반대로
Into the Breach(1회, 약 2분, 거절 가능)는 불만이 없다.

이게 내 G2 축을 판정 변수로 승격시킨다. **횟수 × 1건당 길이 = 총량**이고, 총량이
판정된다. ItB의 2분을 상한 기준선으로 잡으면:

| 항목 | 값 | 근거 |
|---|---|---|
| 상한 기준선 | **120초** | ItB 전체 튜토리얼, 불만 0 [GuidanceSurvey] |
| 우리 정지 건수 | 8 | 확정 스펙 |
| 1건당 허용 시간 | **≤ 15초** | 120 ÷ 8 |
| 한국어 읽기 속도 가정 | **200어절/분 = 3.3어절/초** | UI 스캔 읽기 보수값 [INFERENCE] |
| 읽기에 쓸 수 있는 시간 | **≤ 10초** (나머지 5초는 카드 등장·해제·복귀 조작) | [INFERENCE] |
| **→ G2 상한** | **1건당 ≤ 33어절** | 10초 × 3.3 |
| 8건 총 예산 | **≤ 264어절** | |

**이 33어절이 이번 조사가 설계에 주는 유일한 정량 상한이다.** 기존 토스트 평균이
5.5어절이므로 6배 여유가 있다 — 즉 **8회 정지는 문안이 짧으면 ItB 대역 안에 들어간다.**
횟수만 보면 대역 밖(8 vs 최대 1)이고, 총량으로 보면 대역 안일 수 있다. 두 판정이
갈리므로 **어느 축으로 볼지가 설계 결정**이고 그건 designer 소관이다. 여기서는
**"33어절을 넘으면 총량으로도 대역 밖"**이라는 경계만 확정한다.

**ItB/Returnal을 당긴 판단은 절반만 맞았다.** 나는 "정지를 쓰는 타이틀의 상한"을
재려고 둘을 넣었는데, 실측 결과 ItB는 **상한이 아니라 하한 쪽 증거**였고(2분·거절
가능) Returnal은 **애초에 정지 계열이 아니었다**(무정지 오버레이). 즉 내 표본 선택
가설("이 둘이 정지를 많이 쓸 것")은 틀렸다. 그런데 **틀린 덕분에 더 나은 걸 얻었다** —
Returnal은 "무정지인데도 과잉으로 욕먹은" 사례라서, **무정지 15종 쪽에도 상한이
있다**는 증거가 됐다. 정지 예산만 관리하고 토스트를 무제한으로 늘리면 Returnal형
실패로 간다. 확정 스펙에 토스트 상한이 없다는 점을 §4b에 위험으로 기록한다.

**4. G5(커버리지)는 이 표에서 유일하게 우리가 명확히 이기는 축이다 — 그래서 의심해야 한다.**
설계안 100%는 8타이틀 중 최고다. StS(카드 텍스트가 규칙 전문)조차 100%는 아니다.
**아무도 100%를 안 한다는 사실은 §1과 같은 질문을 부른다: 못 해서인가, 안 해서인가.**
v1.7의 실무 규칙("0/N을 인용할 때는 원인 칸을 같이 적는다")을 여기 대칭으로 적용하면:

| 100% 미달의 원인 | 읽는 법 | 사례 |
|---|---|---|
| **메커니즘 수가 너무 많아 물리적으로 불가** | 우리에겐 해당 없음 | VS(무기×진화 조합 폭발), RoR2(아이템 200+) |
| **의도적 발견 재미** | 진짜 반대 근거 | Dead Cells 룬, StS 일부 시너지 |
| **그냥 안 함** | 우리와 무관 | DRG:S |

**우리 분모는 23이다.** VS·RoR2의 분모는 수백이다. 즉 100%가 가능한 이유는 우리가
뛰어나서가 아니라 **분모가 작아서**이고, 그래서 이 축의 승리는 자랑거리가 아니라
**당연히 닫아야 할 구멍**이다. 8.7%는 분모 23에서 나온 값이라 변명이 안 된다.

반대 방향의 위험 하나: 분모가 작다는 건 **23이 늘면 커버리지가 즉시 깨진다**는 뜻이다.
기믹 1종·아이템 1종만 추가돼도 23→24가 되고 비트마스크(23비트)와 전수 테스트가 같이
깨진다. test-plan T-A1이 이걸 잡는다.

### 출처

- Hades 코덱스 `C` 키·아킬레우스 획득·구역 제한 — 커뮤니티 가이드 / fandom [indexed snippet]
- Risk of Rain 2 로그북 메인 메뉴 전용, 일시정지 진입로 부재 — riskofrain2.wiki.gg / reddit [indexed snippet]
- Vampire Survivors 일시정지 → 옵션 → 종료, 진행 보존 — steamcommunity / reddit [indexed snippet]
- Slay the Spire Abandon Run 확인 모달 + 오조작 제보 — steamcommunity / reddit [indexed snippet]
- DRG: Survivor 정식 튜토리얼 부재, 일시정지 중 무기 스탯 — thegamer / steamcommunity / reddit [indexed snippet]
- Into the Breach · Returnal · 19타이틀 G1 실측 — **designer 레인 귀속**,
  `GuidanceSurvey` IRC 2026-08-07, 전문 `.survey/ingame-guidance/solutions.md` §정지 빈도 정량화
- 우리 값 — `Assets/Scripts/View/HudView.cs`, `Assets/Scripts/Input/InputAdapter.cs`,
  `Assets/Scripts/Sim/SimTypes.cs`, `Assets/Scripts/View/GameDirector.cs` [OBSERVED]

**인용 금지 경고**: G2–G5 벤치마크 값은 개발사 공표값이 아니라 문서화된 UI 구조에서
유도한 **추정 조작 수·추정 비율**이다. `qa/gate-measurements.md`에 **측정값으로 인용
금지**. 우리 행(0회, ∞/∞, ∞, 8.7%)과 §4 rect 좌표만 측정값이다.
**G2 상한 33어절은 [INFERENCE]**다 — 읽기 속도 200어절/분 가정에서 유도했고, 그
가정 자체는 측정하지 않았다. 밴드로 승격하려면 designer/PM 서명이 필요하다.

### v1.8 추가 — G1 판정 재검정 (designer 헤드라인 반전에 대한 계측 회신)

2026-08-07 후반. `GuidanceSurvey`가 내 33어절 계량을 받아 헤드라인을 **"대역 밖"
→ "횟수 축 대역 밖 / 총량 축 대역 안, 권고는 총량 축"**으로 고쳤다. 근거는
*"제 표본에서 횟수의 판별력은 0"*이다. **그 주장을 검정했고 절반만 맞다.**
수치가 내 손에서 나왔으므로 내가 검정할 의무가 있다.

**검정 1: "횟수의 판별력 0"은 층 안에서만 참이다.**

| 층 | N | 불만 |
|---|---|---|
| 정지 0회 | **14** | **0건** |
| 정지 1회 | 4 | 2건 불만 · 1건 무불만 · 1건 미확인 |
| 정지 다수 | 1 (DD2) | **1건 — 혹평 최댓값** |

designer가 든 2×2는 **`count == 1` 층 내부**다. 층 안에서 횟수는 상수이므로
판별력 0은 **동어반복적으로 참**이고 데이터가 준 발견이 아니다. 층을 걷어내면
횟수는 단조 증가한다(0회 → 불만 0 / 1회 → 혼재 / 다수 → 최댓값).

**즉 횟수는 "판별력 0"이 아니라 총 시간과 교락(confounded)돼 있다.** 두 진술은
다르다. 전자는 "무시해도 된다"이고 후자는 "단독으로 해석하면 안 된다"이다.
**전자로 읽으면 설계 제약 하나를 근거 없이 버리게 된다.**

**검정 2: 우리 설계안이 들어가는 칸이 표본에 없다.**

| | 1회 · 짧음(<120s) | 1회 · 김(≥120s) | 다수 |
|---|---|---|---|
| **무불만** | (표본 0건) | Into the Breach 120s | (표본 0건) |
| **불만** | (표본 0건) | Gungeon 450s · CotL 750s | **DD2** |
| **미확인** | | Skul ~180s | |

우리 설계안 = **8회 × 15초 = 총 120초**. ItB와 **총량이 정확히 같다**(120초).
그래서 "총량 축 대역 안"이 나온다. 그런데 그건 **총량만이 변수일 때** 성립한다.

**표본에서 분산 배치(횟수 > 1) 선례는 DD2 하나뿐이고 그것이 혹평 최댓값이다.**
그리고 designer 자신이 첫 메시지에서 정확히 그렇게 적었다 — *"분산 배치 형태의
선례는 표본에서 DD2 하나뿐이고 그게 온보딩 혹평 최댓값입니다."* **반전 과정에서
자기 증거를 떨어뜨렸다.**

**따라서 정직한 판정은 "총량 축 대역 안"이 아니라 "미검증 칸"이다.**

**이건 v1.7 실무 규칙의 재적용이다 — 이번엔 부호가 반대다.**
v1.7에서 나는 0/N을 위험으로 환산했다가 틀렸고, 규칙을 세웠다:
*"0/N을 인용할 때는 원인 칸을 같이 적는다. 원인을 모르면 '선례 없음'까지만 쓰고
위험 판정은 하지 않는다."*

이번 (8회, 짧음) 칸도 **선례 0건**이다. 규칙을 그대로 적용하면 **"안전 판정도
하면 안 된다."** v1.7에서 내 오류는 0을 위험으로 읽은 것이었고, 여기서 0을
안전으로 읽으면 **같은 오류의 거울상**이다. 0은 방향을 안 준다.

| 0/N을 읽는 법 | v1.7의 내 오류 | 이번의 대칭 오류 |
|---|---|---|
| 0 → 위험 | **범함** (N2 0/6을 관례 이탈 위험으로) | — |
| 0 → 안전 | — | **여기서 범할 뻔함** ((8,짧음) 0건을 대역 안으로) |
| 0 → 판정 보류 | 규칙이 요구하는 것 | 규칙이 요구하는 것 |

**합의 가능한 결론 — designer 권고는 채택하되 근거를 바꾼다.**
designer의 실무 권고(*"'정지 8종' 옆에 '1건당 ≤33어절'을 나란히 수용 기준으로"*)는
**옳고 채택한다.** 다만 근거가 "총량 축이면 안전해서"가 아니라 **"어느 축으로도
안전이 증명되지 않아서 둘 다 잠근다"**이다. 결론은 같고 이유가 다르며, **이유가
다르면 다음 사이클에 한쪽을 푸는 판단이 갈린다.**

| 항목 | 값 | 근거 등급 |
|---|---|---|
| 정지 종수 상한 | **8** | 확정 스펙 |
| **1건당 단어 상한** | **≤ 33어절** | [INFERENCE] — ItB 120초 ÷ 8 ÷ 3.3어절/초 |
| **총 정지 시간 상한** | **≤ 120초** | ItB 실측 [GuidanceSurvey] — 이게 실제 구속 조건 |
| 판정 | **미검증 칸** (표본 0건) | 위험도 안전도 아님 |

**셋을 동시에 잠근다.** 8종만 박으면 카드가 길어지는 경로가 열려 있고(Gungeon·CotL
함정), 33어절만 박으면 종수가 늘어나는 경로가 열려 있고(DD2 함정), 총 120초만 박으면
분배가 자유로워진다. **세 축이 서로의 우회로를 막는다.**

**계측 레인의 교훈(v1.7 대칭)**: v1.7에서는 designer 빈도표가 내 위험 오독을 고쳤고,
이번에는 내 계층화가 designer의 안전 오독을 고쳤다. **같은 실무 규칙이 양방향으로
작동했다** — 0/N은 방향을 주지 않으며, 그걸 잊는 순간 두 레인 다 같은 함정에 빠진다.
내 33어절이 반전의 방아쇠였으므로 검정 의무도 내게 있었다.

### v1.8 추가 2 — 내 절편 오류 정정, 그리고 그 정정이 드러낸 네 번째 채널

2026-08-07 후반. `GuidanceSurvey`가 내 §G1 재검정의 **수치 1건을 반증**했다. 수용한다.

**정정: "0회 → 불만 0건(14타이틀)"은 거짓이다.**

내 근거는 designer 1차 표의 0회 행 불만칸이 `—`였다는 것이고, 나는 그걸 **0건으로
독해**했다. 실제 의미는 **해당없음/미측정**이었다(정지 관련 컬럼이므로).

**이 오류는 내 문서 내부에서 모순이다.** 나는 같은 v1.8의 §3과 T-A11에서
*"Returnal은 무정지(0회)인데 과잉 혹평"*을 **토스트 상한의 핵심 근거로 썼다.**
즉 0회 층에 과잉 불만이 존재한다는 걸 **내가 이미 알고 있었고, 그걸 근거로 판정까지
내려놓고, 두 절 뒤에서 0건이라고 적었다.** 외부 반증을 기다릴 필요도 없었다 —
내 문서를 다시 읽었으면 잡혔다.

정정판 [GuidanceSurvey 2차 개정]:

| 차단 정지 횟수 | N | 과잉 불만 | 비율 |
|---|---|---|---|
| 0회 | 14 | 2 (Returnal · StS 약) | **14%** |
| 1회 | 4 | 2 (Gungeon · CotL) | **50%** |
| 다수 | 1 | 1 (DD2) | **100%** |

**단조 증가는 유지된다 — 방향은 내가 맞았고 절편이 틀렸다.** 결론("횟수는 판별력
0이 아니라 총 시간과 교락")은 살아남지만, **"0회는 무조건 안전"이 아니라 "0회도
14%는 욕먹는다"**로 바뀐다. 이 차이가 아래를 만든다.

**정정이 드러낸 것: 우리 판정은 "미검증"보다 한 단계 나쁘다.**

designer가 불만 어휘를 분해했고 그게 결정적이다:

| 불만 어휘 | 타이틀 | 차단 총시간 |
|---|---|---|
| *"흐름을 끊음 · 보스전에 뜸 · 화면 가림"* | **Returnal**, DD2 | **0분**, ? |
| *"너무 김 · 반복 · 강제"* | Gungeon, CotL | 7.5분, 12.5분 |

**Returnal은 차단 0회 · 차단 총시간 0분인데 흐름 불만이 있다.** 즉 **빈도 채널은
차단 여부와도 총시간과도 독립으로 작동하고, 그 채널은 미검증이 아니라 입증돼 있다.**

따라서 내 이전 판정("미검증 칸")을 **강화한다**:

| | 이전 (내 v1.8 추가) | 현재 |
|---|---|---|
| 우리 칸 선례 | 0건 | 0건 (동일) |
| 빈도 채널 | 미검증 | **입증됨 — 우리는 거기 노출(8회 = 최댓값의 8배)** |
| 판정 | 미검증 칸 | **미검증 칸 + 입증된 채널에 노출** |

총량 동률(120초)은 **길이 채널만 닫는다.** 빈도 채널은 총량과 독립이므로 안 닫힌다.

**삼중 잠금의 구멍 — 종수 8 잠금은 빈도 리스크를 완화하지 않는다.**

| 채널 | 증거 | T-A10이 닫는가 |
|---|---|---|
| 길이 | Gungeon 450s·CotL 750s 불만 / ItB 120s 무불만 | **닫힘** — 1건당 33어절 |
| 총량 | ItB 120s 무불만 | **닫힘** — 총 264어절(120초) |
| **빈도** | **Returnal 0회인데 불만** · DD2 | **안 닫힘** — 8은 잠금이 아니라 노출 그 자체 |

`종수 == 8`은 **9로 늘어나는 걸** 막지 8이 안전하다고 말하지 않는다. 악화 방지이지
완화가 아니다.

**그럼 빈도 채널을 무엇이 완화하는가 — 표본에 후보가 하나 남는다.**

불만 5건을 세 후보로 갈라봤다:

| 분리 후보 | 참일 때 | 거짓일 때 | 완전 분리? |
|---|---|---|---|
| 정지 1회 이하 | 불만 3/4 | 불만 1/1 | **아니오** |
| 1건당 < 120초 | 불만 1/1 | 불만 3/4 | **아니오** |
| **회피 가능(스킵·해제)** | **불만 0/1** | **불만 4/4** | **예 — 유일** |

횟수도 길이도 완전 분리를 못 한다(Returnal이 둘 다 깨뜨린다). **회피 가능성만이
5/5를 가른다.** ItB는 *거절 가능*이라 무불만이고, Gungeon(스킵 불가)·CotL(클리어
후에만 스킵)·DD2(전역 OFF 없음)·Returnal(끌 수 없음)은 전부 불만이다.
**Returnal의 불만 내용이 문자 그대로 *"끌 수 없다"*라는 점이 이 축의 직접 증거다.**

**과신 경고 — 이 발견의 증거력을 정확히 적는다.**
내가 designer의 과신을 지적한 직후이므로 같은 잣대를 내게 적용한다:

| 방향 | 증거 | 강도 |
|---|---|---|
| 회피 **불가** → 불만 | **4/4** | 시사적. N=4는 작지만 예외 0 |
| 회피 **가능** → 무불만 | **1/1** (ItB 단독) | **약함. 단일 사례** |

**"회피 가능성이 안전을 보장한다"고 말할 수 없다.** 말할 수 있는 것은
**"표본에서 반증되지 않은 유일한 후보"**이고, 나머지 둘은 반증됐다는 것뿐이다.
ItB 하나로 충분조건을 주장하면 내가 방금 비판한 오류를 반복한다.

**확정 스펙에 스킵·해제 조항이 없다.**
*"최초 조우 자동 + 정지"*와 *"본 것은 다시 정지 안 함"*만 있다. 후자는 **2회차
이후**를 다루지 **첫 강하의 8회**를 안 다룬다 — 그리고 첫 강하가 빈도 노출이
최대인 지점이다. **이건 스펙 공백이지 스펙 위반이 아니므로** designer 결정 사항으로
올린다. 여기서는 **"빈도 채널이 열려 있고, 표본에서 그걸 닫은 유일한 수단이
스펙에 없다"**는 사실만 남긴다.

**계측 레인 교훈 3연속**: v1.7 — designer가 내 위험 오독을 고침. v1.8 — 내 계층화가
designer의 안전 오독을 고침. v1.8 추가2 — **designer가 내 절편 오류를 고쳤고, 그
정정이 양쪽 다 못 본 네 번째 채널을 드러냈다.** 세 번 다 **한쪽 레인만으로는 못
나온 결론**이다. 그리고 이번 건은 **내 문서 내부 모순**이었다는 점에서 앞의 둘과
다르다 — 교차검증이 없었으면 내 오류가 그대로 남았을 것이다.

### v1.8 추가 3 — 회피 축 입도 정정, 그리고 감사 범위 확장

2026-08-07 후반. `GuidanceSurvey`가 §추가2의 "회피 가능성" 축을 **두 입도로 쪼개
내 결론을 정정**했다. 재현했고 수용한다.

| 입도 | 가능&불만 | 가능&무불만 | 불가&불만 | 완전분리 |
|---|---|---|---|---|
| **개별 해제**(카드 닫기) | **2** (DD2 · Returnal) | 1 (ItB) | 2 | **아니오** |
| **범주 옵트아웃**(아예 안 보기) | 0 | 1 (ItB) | **4** | **예** |

**DD2 모달은 개별로 닫히고 Returnal 팝업은 자동 소멸한다. 둘 다 과잉 혹평이다.**
개별 해제는 충분조건으로 **반증**(반례 2/2). 범주 옵트아웃만 남는다.

**내 오류의 형태**: 나는 Returnal의 *"끌 수 없다"*를 근거로 인용해놓고 판정은
**개별 입도로 썼다.** 인용문이 가리키는 축과 판정이 재는 축이 달랐다. §추가2의
5/5 완전분리 주장은 **범주 입도에서만 참**이고, 내가 T-A12에 쓴 개별 입도에서는
3/5로 무너진다. 근거와 판정 사이에서 축이 미끄러진 것이다.

**증거력은 양쪽 다 유보 유지**: 반증 쪽(개별 해제 → 불만, 2/2)은 견고, 긍정 쪽
(범주 옵트아웃 → 무불만, **ItB 단독 1/1**)은 약함. designer도 같은 강도로 달았다.
**"안전 보장"이 아니라 "반증되지 않은 유일한 후보"까지만** 쓴다.

**부수 산출 — §4a 구멍이 겹침보다 넓었다 [OBSERVED — 산술]**

designer 기각 사유(터치 래칫) 철회를 검증하느라 하한을 실측하다가
기존 모달 버튼을 같이 쟀고, **네 개가 하한 미달**이었다.
cssPerUnit = 390/798.7 = 0.48829, 44 CSS px = **90.1u**.

| 패널 | 버튼 세로(u) | CSS px | `AssertTouchFloor` 실행 |
|---|---|---|---|
| 게임오버 ×2 | 44 · 40 | **21.5 · 19.5** | **안 함** |
| 스테이지클리어 ×2 | 44 · 44 | **21.5** | **안 함** |
| 엠버레스트 ×5 | 128 · 92 | 62.5 · 44.9 | **함** |

**측정되는 패널은 준수, 안 되는 패널은 미달. 예외 0건.** 원인은 §4a 구멍 2번과
동일하다(`ArrangePhone`이 모달을 안 켜고 `InteractiveRects`가 비활성 제외).
**같은 구멍 하나가 겹침과 터치 하한 두 결함 유형을 동시에 숨기고 있었다** —
§4a는 겹침만 다뤘으므로 그 절의 범위가 좁았다. `qa/test-plan.md` T-A13으로 판정화했다.

**감사 총계 갱신**: 겹침 확정 8건(+조건부 2) **+ 터치 하한 미달 4건** = 기존
테스트가 못 잡는 라이브 결함 **12건**. 전부 §4a 세 구멍에서 나왔다.

---

## Cycle 9 — Achilles action-concept calibration (run-id 20260808-achilles-quality)

2026-08-08 · game-qa · **Stage 2 Phase 2a re-entry**, not a new Stage 1 concept pass.
Cycle 9 preserves the current Cinder Court concept. This section calibrates QA senses for
cycles 10–13; it does not authorize Greek mythology, Achilles names/story, copied art,
layouts, assets, moves, stamina values, or online co-op.

### Source and boundary

| Claim | Status | Source |
|---|---|---|
| Achilles positions itself as punishing souls-like combat plus ARPG progression. | `[OBSERVED]` | `design/trend-survey/achilles-steam-source.md:8-16` |
| Its stated verbs include timed dodge, block, strike, perfect counter, divine abilities, special attacks and grenades. | `[OBSERVED]` | same source `:17-23` |
| Its presentation emphasizes readable silhouettes, impact frames, named confrontations and location identity. | `[OBSERVED]` | same source `:25-29` |
| HongT may borrow committed timing, readable response, build identity, coordinated pressure, named phases, environmental tactics and finish emphasis only as play concepts. | `[TARGET]` | same source `:31-35` |
| HongT remains a deterministic 60 Hz Cinder Court game with its current courtroom/lantern worldview and generated-asset provenance. | `[TARGET]` | `docs/SIM_SPEC*.md`; adaptation boundary above |

**QA anti-copy check**: every cycle 10–13 test charter names the HongT-owned system under
test. If a proposed criterion can only be described with an Achilles proper noun, Greek
story beat, copied encounter layout, asset, camera shot or exact tuning value, it is rejected
before playtest. The benchmark is a sensitivity reference, never source content.

### Calibration axes: observed HongT anchor → measurable question

| Axis | HongT anchor | Cycle-9 status | QA measurement for cycles 10–13 |
|---|---|---|---|
| Committed strike | Attack active window 0.167–0.333 s; PlayMode frames exist at normalized 0.22/0.53/0.78. | `[OBSERVED]` pose/timing anchor; shipped input-to-motion latency `UNKNOWN`. | 30 trials/mode: input timestamp → readable motion; sim hit → VFX/audio. Every G4 spot-check ≤100 ms. |
| Evade response | Dungeon dash is 0.22 s, invulnerable throughout, cooldown 1.6 s, oil 8; Arena/Prologue dash is inert. | `[OBSERVED]` contract. | Boundary probes at one tick before/on/after contact; 30 trials each. Outcome and feedback timestamps, no adjective verdict. |
| Guard/counter | Achilles advertises block/perfect counter; HongT’s current public control table has no guard/counter verb. | `[OBSERVED]` benchmark/current mismatch. | No cycle-9 defect. If design adopts it in cycle 10+, first require a deterministic input/event contract and a one-tick boundary matrix; do not infer one exists now. |
| Build authorship | HongT has equipment, stats, sigils, companions, skills and four difficulty tiers. | `[OBSERVED]`; current per-build win rates/EV `UNKNOWN`. | ≥5 archetypes, distinct loadout policies, 20 distinct deterministic scripts per evaluated pairing; 45–55% band and pair EV ≤1.3× median. |
| Coordinated pressure | Hard/Nightmare group AI and attack-token caps exist; isolated tier metrics exist. | `[OBSERVED]`; real clear rates/fun `UNKNOWN`. | Measure surround occupancy, simultaneous swings, telegraph recognition, damage source and clear rate per tier/archetype. |
| Named duel | Three named boss families and three phases already exist. | `[OBSERVED]`; phase recognition/immersion median `UNKNOWN`. | Per phase: telegraph recognition, TTK, avoidability and 1–5 rubric; ≥5 scored sessions/scene. |
| Environmental tactics | Current/pylon/wall/vent/pillar/altar are deterministic and symmetric where specified. | `[OBSERVED]`; exploit census incomplete. | Safe-spot, enemy-lure, pylon-first, wall hold, current clamp and simultaneous-telegraph probes; record ticks/damage/TTK. |
| Climactic finish | Boss intros, focus pulse, hit-stop tiers and finish presentation exist. | `[OBSERVED]`; cross-mode latency/readability `UNKNOWN`. | Record event→camera/VFX/audio latency, reduced-motion equivalent signal and duplicate/missing terminal panel count. |

### Benchmark-derived exploit probes

These are new QA patterns, not feature requests.

1. **Commit-cancel boundary** `[TARGET]`: issue Space, Shift and Q/E/R/F at −1/0/+1
   simulation tick around every active-window edge. Record accepted action, damage, oil,
   cooldown and pose. Duplicate damage, free cast, or state/action disagreement is a defect.
2. **Input-spam economic leak** `[TARGET]`: 30 Hz synthetic attack/skill/dash input for
   60 s. Charges and cooldowns must equal accepted simulation events exactly; no UI event
   may debit twice or cast free.
3. **Group-AI bait loop** `[TARGET]`: rotate player around the attack ring for 1,800 ticks
   on Hard/Nightmare. Record bodies in range, simultaneous swings, rear attacks and idle
   slots. Compare with the declared token caps and dominance bands.
4. **Environmental outsourcing** `[TARGET]`: win using only enemy lures into symmetric
   hazards where possible. Record player damage, hazard damage, reward credit and TTK.
   A zero-risk route or >1.3× median EV pair enters `qa/exploit-register.md`.
5. **Boss phase skip** `[TARGET]`: burst exactly across 50% and 20% HP boundaries at
   −1/0/+1 tick. Each phase event, plate and presentation occurs once; no reward duplication.
6. **Mode leakage** `[TARGET]`: send Dungeon-only dash/skills/companion commands in
   Prologue, Training and Arena. Inert commands must leave digest, economy and persistence
   unchanged.
7. **Growth doorway fairness** `[TARGET]`: fresh save → T5 under seven declared spending
   policies. Report first-T5 session per slot; UI reachability is measured separately from
   affordability.
8. **Reduced-motion equivalence** `[TARGET]`: disable time stop/camera impulse while retaining
   an alternative readable hit/finish signal. Count missed outcomes and response latency.

### Action-feel and immersion scoring anchors

Each scored scene produces five integer fields, not one free-text impression:

| Score | Timing/readability anchor |
|---|---|
| 1 | Required event is missed or misread; outcome cannot be attributed to an input/threat. |
| 2 | Event is eventually identifiable but after the decision window, or feedback conflicts. |
| 3 | Event and outcome are identifiable inside the decision window, with one ambiguity/recheck. |
| 4 | Event, response and result are correctly identified on first viewing without external text. |
| 5 | Score-4 clarity plus the tester can name the timing/counter window and encounter identity after the scene. |

Dimensions: `input response`, `threat readability`, `hit/outcome clarity`, `scene identity`,
`audio/visual cohesion`. G4 uses the scene medians and aggregate median; all must be ≥4.0/5,
effect feedback latency must be ≤100 ms, and unresolved S1/S2 readability complaints must be 0.
G8 uses a separate prompt — “which single element would you describe unprompted?” — but the
same 1–5 anchors. A frequency PASS never substitutes for an impression median ≥4/5.

### NAN 2026 truth audit

| Public claim | Current evidence | QA status |
|---|---|---|
| `EditMode 166/166` in README and overview. | Cycle 8 records 808/808, 0 failed. | `[OBSERVED]` stale. Public-beat blocker until Markdown and regenerated PDFs agree with accepted evidence. |
| Six logical stages / “sixth and final” / `StageCatalog.cs` six-stage role. | Contract and test source describe nine ordered logical stages; cycle-2 browser run reached mask 511. | `[OBSERVED]` stale. Public-beat blocker. |
| Overview scope omits later training, difficulty, guidance and broader campaign additions. | Current contracts/tests and workspace evidence contain those systems. | `[INFERENCE]` incomplete public description; every added claim still needs current measured proof before publication. |

Reproduction:

```bash
grep -R "166/166\|6개 논리 스테이지\|6번째이자 마지막\|6단계 캠페인" docs/nan2026
```

Comparison evidence: `qa/gate-measurements.md:138-145`,
`docs/SIM_SPEC_CAMPAIGN.md:16-18`, `Assets/Tests/EditMode/StageCatalogTests.cs:35-39`,
and `qa/playtest-report.md:14-18`.

### Cycle-9 blocking conclusion

| Blocker | Measured status | Exit evidence |
|---|---|---|
| G8 impression | `[OBSERVED]` every submitted novelty candidate remains `미측정`; cycle 8 made this a Stage-2 entry condition after five carry-overs. | ≥5 raw scored sessions for one submitted candidate, median ≥4/5, plus frequency ≤2 of ≥5. |
| G5 parity | `[OBSERVED]` spend reachability changed 0%→100%; sessions-to-T5 was not rerun. | Fresh-save session ledger; every claimed free path reaches parity in 10–20 sessions. |
| G5 replay/signature | `[OBSERVED]` the current contract requires five pilots × routes A–E, but negotiation entry 17 remains `escalated`, `pending`, `signed: []`; joined session telemetry is not yet captured. | Exact per-route `N_T5` replay, comeback pairs, paid-path absence audit, joined evidence rows and designer/PM signatures. |
| Evidence contract | `[OBSERVED at audit start]` `qa/exploit-register.md`, `qa/defect-register.md`, and `qa/regression-matrix.md` were absent. | Required paths exist with build/session identity; missing evidence cannot pass a gate. |

Achilles does not unblock any of these. Cycle 9 fixes the current evidence first; the
benchmark becomes PRD calibration only in cycles 10–13.
