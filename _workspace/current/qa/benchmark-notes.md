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
