# Balance Sheet — cycle 9 G2 runtime-authority snapshot

작성: game-designer. 기준일: 2026-08-08. 이 문서는 G2의 `100% mechanics in balance
sheet` 인벤토리다. 런타임 수치를 바꾸는 문서가 아니며, 아래 `[OBSERVED-RUNTIME]`
행은 코드에서 읽은 현재 shipping truth를 기록한다.

## 읽는 법과 권한

- `[OBSERVED-RUNTIME]`: Unity가 실제 소비하는 코드 상수/테이블. 각 행의 `code authority`
  열이 단일 진실이다.
- `[DERIVED]`: 같은 행에 적힌 코드 권한의 산술 결과. 별도 튜닝 상수가 아니다.
- `[TARGET]`: QA 측정 기준. 런타임은 이 값을 소비하지 않으며 코드 권한이 없다고
  명시한다.
- `[UNCONTRACTED]`: 현재 목표값이 없다. 특히 보스 TTK에 숫자를 발명하지 않는다.
- 과거 수치는 마지막의 `HISTORICAL — NOT ACTIVE` 표에만 남긴다. 그 표는 현재값
  조회에 사용하면 안 된다.

## 현재 shipping 경로

| status | surface | current value | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | Dungeon progression feature gate | `AdaptiveWaves=true`, `GradedLoot=true`, expanded bounds active, `BossVariety=true` | `Assets/Scripts/View/GameView.cs::DungeonProgression = DungeonProgressionConfig.Everything`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::DungeonProgressionConfig.Everything` |
| `[OBSERVED-RUNTIME]` | Dungeon-only routing | Dungeon은 위 config로 `CinderSim` 생성; Arena/Prologue/Training은 progression config 미사용 | `Assets/Scripts/View/GameView.cs::Begin`; `Assets/Scripts/Sim/CinderSim.cs::CinderSim(in HackConfig, DungeonProgressionConfig)` |
| `[OBSERVED-RUNTIME]` | fixed simulation step | `1/60 s`; frame catch-up `5` steps; max frame delta `0.25 s` | `Assets/Scripts/Sim/SimTypes.cs::SimConfig.FixedStep`, `MaxCatchUpSteps`, `MaxFrameDelta` |

## 1. 전투·빌드 수치

### 1.1 플레이어와 기본 공격

| status | mechanic | current value | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | base chassis | HP `100`; speed `218`; damage `58`; range `160`; attack cooldown `0.48 s`; hit grace `0.38 s` | `Assets/Scripts/Sim/SimTypes.cs::SimConfig.PlayerMaxHealth`, `PlayerSpeed`, `PlayerDamage`, `PlayerAttackRange`, `PlayerAttackCooldown`, `PlayerHitGrace` |
| `[OBSERVED-RUNTIME]` | movement during combat | attack move scale `0.42`; Y input scale `0.68`; player clamp margin `34` | `Assets/Scripts/Sim/SimTypes.cs::SimConfig.AttackMoveScale`, `YMoveScale`, `PlayerMarginClamp` |
| `[OBSERVED-RUNTIME]` | 3-hit combo | damage scales `{1,1,87/58}`; swing `{0.30,0.30,0.42}s`; active windows `{0.10–0.22,0.10–0.22,0.14–0.30}s`; link `0.9 s` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.ComboDamageScale`, `ComboSwing`, `ComboActiveFrom`, `ComboActiveTo`, `ComboLinkWindow` |
| `[DERIVED]` | combo raw damage | `{58,58,87}`; one full chain `203` before build/element/momentum modifiers | `Assets/Scripts/Sim/SimTypes.cs::SimConfig.PlayerDamage`; `Assets/Scripts/Sim/HackTypes.cs::HackSpec.ComboDamageScale` |
| `[OBSERVED-RUNTIME]` | combo displacement | finisher knockback `120` over `0.18 s`; variant multipliers `{neutral 1.00, launcher 1.60, retreat 0.70, spin 1.00}`; spin reach `×1.35`; retreat step `74` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.ComboKnockbackDistance`, `ComboKnockbackTime`, `FinisherKnockbackMul`, `SpinReachMul`, `RetreatStepDistance` |
| `[OBSERVED-RUNTIME]` | charged attack | ready after `0.45 s`; damage `×1.8`; knockback `×2.0`; movement `×0.45` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.ChargeReadySeconds`, `ChargeDamageMul`, `ChargeKnockbackMul`, `ChargeMoveScale` |
| `[OBSERVED-RUNTIME]` | dash | distance `190`; duration `0.22 s`; cooldown `1.6 s`; oil cost `8` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.DashDistance`, `DashTime`, `DashCooldownSeconds`, `DashCost` |

### 1.2 액티브 스킬과 원소

| status | skill | current value | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | Rift Bolt | range `420`; hit `145`; splash radius `115` at `×0.6`; cooldown `6.5 s`; oil `25`; Void | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.BoltRange`, `BoltDamage`, `BoltSplashRadius`, `BoltSplashScale`, `BoltCooldown`, `BoltCost`, `BoltElement` |
| `[OBSERVED-RUNTIME]` | Grave Pulse | radius `190`; duration `3 s`; tick every `0.5 s` for `26`; cooldown `4 s`; oil `30`; Ember | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.PulseRadius`, `PulseDuration`, `PulseTickInterval`, `PulseTickDamage`, `PulseCooldown`, `PulseCost`, `PulseElement` |
| `[OBSERVED-RUNTIME]` | Ash Nova | radius `230`; damage `110`; knockback `120`; cooldown `8 s`; oil `45`; Ember | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.AshNovaRadius`, `AshNovaDamage`, `AshNovaKnockback`, `AshNovaCooldown`, `AshNovaCost`, `AshNovaElement` |
| `[OBSERVED-RUNTIME]` | Void Aegis | shield `40`; duration `8 s`; cast invulnerability `0.2 s`; cooldown `12 s`; oil `30`; Frost | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.AegisShield`, `AegisDuration`, `AegisCastInvuln`, `AegisCooldown`, `AegisCost`, `AegisElement` |
| `[OBSERVED-RUNTIME]` | element matchup | advantage `×1.20`; disadvantage `×0.85`; otherwise `×1.00` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.ElementAdvantage`, `ElementDisadvantage`, `Matchup` |

### 1.3 영구/인런 성장

| status | mechanic | current value | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | meta stat caps/effects | each stat cap `10`; Attack `+3%/point`; Vitality `+8 HP/point`; Swiftness `+2% speed/point` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.MaxStatPoints`, `AttackPerPoint`, `VitalityHealthPerPoint`, `SwiftnessSpeedPerPoint` |
| `[OBSERVED-RUNTIME]` | equipment caps/effects | rank cap `5`; Weapon `+6% damage/rank`; Lantern `+8% regen/rank`; Cloak `+8 HP/rank` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.MaxEquipRank`, `WeaponDamagePerRank`, `LanternRegenPerRank`, `CloakHealthPerRank` |
| `[OBSERVED-RUNTIME]` | composed level-1 stats | damage `58×(1+0.03a)×(1+0.06w)`; HP `100+8v+8c`; speed `218×(1+0.02s)`; oil regen `7×(1+0.08l)` | `Assets/Scripts/Sim/HackTypes.cs::HackConfig.PlayerDamage`, `PlayerMaxHealth`, `PlayerSpeed`, `LanternRegenPerSecond` |
| `[OBSERVED-RUNTIME]` | equipment purchase | T0→T5 costs `{2,4,7,11,16}` relics | `Assets/Scripts/View/ProgressionGuide.cs::ProgressionGuide.EquipCosts`, `EquipCap` |
| `[OBSERVED-RUNTIME]` | sigil purchase/loadout | unlock cost `12` relics each; `5` catalog sigils; equip slots `2` | `Assets/Scripts/View/ProgressionGuide.cs::ProgressionGuide.SigilCost`, `SigilOrder`; `Assets/Scripts/Sim/HackTypes.cs::SigilLoadout.Slots` |
| `[OBSERVED-RUNTIME]` | level growth | cap `12`; XP kill/elite/boss `{10,25,150}`; XP curve `{30,55,85,120,160,205,255,310}`, then `+60/level`; per level `+4% damage`, `+6 HP`, `+0.3 regen` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.LevelCap`, `XpPerKill`, `XpPerElite`, `XpPerBoss`, `XpToNextLevel`, `XpPerLevelBeyondCurve`, `LevelDamageBonus`, `LevelHealthBonus`, `LevelRegenBonus` |
| `[OBSERVED-RUNTIME]` | growth offer | decision window `5 s`; Attack `+8%`; Vitality `+6 HP`; Swiftness `+4% speed`, dash cooldown `−6%/point` with `0.55` floor | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.GrowthOfferSeconds`, `GrowthAttackBonus`, `GrowthVitalityHealth`, `GrowthSwiftnessSpeed`, `GrowthSwiftnessCooldown`, `GrowthSwiftnessCooldownFloor` |
| `[OBSERVED-RUNTIME]` | momentum | max `100`; hit `+9`; kill bonus `+14`; grace `1.6 s`; decay `12/s`; hurt `−25`; tier thresholds `{0,30,60,90}` and melee multipliers `{1.00,1.08,1.18,1.30}` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.MomentumMax`, `MomentumPerHit`, `MomentumPerKill`, `MomentumGraceSeconds`, `MomentumDecayPerSecond`, `MomentumHurtPenalty`, `MomentumTierThresholds`, `MomentumTierDamageMul` |
| `[OBSERVED-RUNTIME]` | Ember Rest offer magnitude | variants `1..3`, magnitude `1..2`; stat `+magnitude`; skill damage `+10%×magnitude`; guardian cadence `−10%×magnitude` (floor `0.5 s`), range `+20×magnitude`, damage `+10%×magnitude` | `Assets/Scripts/Sim/CinderSim.cs::ApplyPreparation`, `ApplyGuardianResonance`; `Assets/Scripts/Sim/RunPreparationSnapshot.cs::PreparationOffer` |

### 1.4 동료

| status | archetype | basic tuple `(cadence, range, player-damage scale)` | signature tuple `(skill, cooldown, radius, damage scale, targets, pulses, knockback)` | code authority |
|---|---|---|---|---|
| `[OBSERVED-RUNTIME]` | ember-cohort | `(1.10s,200,0.60)` | `(Flare,7s,200,1.10,1,1,0)` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.CompanionStats`, `CompanionSkill` |
| `[OBSERVED-RUNTIME]` | scout | `(0.85s,240,0.50)` | `(Volley,6s,240,0.55,3,2,0)` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.CompanionStats`, `CompanionSkill` |
| `[OBSERVED-RUNTIME]` | shade | `(1.30s,260,0.65)` | `(Hex,8s,260,0.40,8,2,0)` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.CompanionStats`, `CompanionSkill` |
| `[OBSERVED-RUNTIME]` | possessed | `(1.45s,150,0.80)` | `(Quake,9s,170,0.70,6,2,90)` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.CompanionStats`, `CompanionSkill` |
| `[OBSERVED-RUNTIME]` | roster/steering limits | max active `3`; fanout `{0,+64,−64}`; acquire `300`; leash `320`; pursuit `×1.05`; target lock `2 s`; return grace `0.35 s`; skill cue `0.35 s`; target cap `8` | — | `Assets/Scripts/Sim/HackTypes.cs::HackConfig.NormalizeCompanionSlots`; `HackSpec.CompanionSlotFanout`, `CompanionAcquireRadius`, `CompanionLeashRadius`, `CompanionPursuitSpeedScale`, `CompanionTargetLockSeconds`, `CompanionReturnGraceSeconds`, `CompanionSkillFlashSeconds`, `CompanionSkillTargetCap` |

### 1.5 난이도

| status | tier | incoming damage | enemy cooldown | attack tokens | group AI | ring radius | flank bias | code authority |
|---|---|---:|---:|---:|---|---:|---:|---|
| `[OBSERVED-RUNTIME]` | Story (`1`) | `×0.65` | `×1.22` | `2` | off | `×1.00` | `1.00` | `Assets/Scripts/Sim/DifficultySpec.cs::DifficultySpec.For(Story)` |
| `[OBSERVED-RUNTIME]` | Normal (`0`, default) | `×1.00` | `×1.00` | `0` = unlimited | off | `×1.00` | `1.00` | `Assets/Scripts/Sim/DifficultySpec.cs::DifficultySpec.For(Normal)` |
| `[OBSERVED-RUNTIME]` | Hard (`2`) | `×1.35` | `×0.84` | `3` | on | `×1.55` | `0.75` | `Assets/Scripts/Sim/DifficultySpec.cs::DifficultySpec.For(Hard)` |
| `[OBSERVED-RUNTIME]` | Nightmare (`3`) | `×1.70` | `×0.70` | `4` | on | `×1.35` | `0.75` | `Assets/Scripts/Sim/DifficultySpec.cs::DifficultySpec.For(Nightmare)` |
| `[OBSERVED-RUNTIME]` | group-AI geometry | ring slots `8`; arrive tolerance `16`; forward threshold `−18` | — | — | — | — | — | `Assets/Scripts/Sim/DifficultySpec.cs::DifficultySpec.RingSlots`, `RingArriveTolerance`, `ForwardThreshold` |

Standalone dotnet에서 기록했던 이전 “최소 공격 간격/평균 거리” 표는 Unity shipping
evidence가 아니므로 active evidence에서 제거했다. 새 관측치는 Unity batch/EditMode 원시
행으로만 들어온다.

## 2. 던전 진행·적·보스

### 2.1 활성 point-budget/DDA

| status | mechanic | current value | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | expanded dungeon clamp | half-width `554`; half-height `418` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::DungeonBoundsSpec.ExpandedHalfWidth`, `ExpandedHalfHeight`; activation `Assets/Scripts/View/GameView.cs::DungeonProgression` |
| `[OBSERVED-RUNTIME]` | wave budget | `min(600,100+(wave−1)×26)` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::WaveBudgetSpec.BudgetBase`, `BudgetPerWave`, `BudgetCap`, `BaseBudget` |
| `[OBSERVED-RUNTIME]` | DDA budget scale | band `−2..+2`; permille `{780,890,1000,1120,1250}`; movement cap `1` band/wave | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::WaveBudgetSpec.BandMin`, `BandMax`, `BandPermille`, `StepCap` |
| `[OBSERVED-RUNTIME]` | DDA signals | healthy `≥0.75`; struggle `<0.35`; fast `≤18 s`; slow `≥42 s`; clean hits `≤2`; battered hits `≥9` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::WaveBudgetSpec.HealthyFraction`, `StruggleFraction`, `FastWaveSeconds`, `SlowWaveSeconds`, `CleanHits`, `BatteredHits` |
| `[OBSERVED-RUNTIME]` | budget spend | grunt cost `16`; spawns clamp `4..14`; full roster spend `224`; elite per `150` points, cap `3`; health surplus bonus cap `1.7` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::WaveBudgetSpec.GruntCost`, `MinSpawns`, `MaxSpawns`, `FullRosterSpend`, `ElitePointCost`, `EliteAllowanceCap`, `HealthSurplusCap` |
| `[OBSERVED-RUNTIME]` | mob HP | `86×HealthMultiplierForBudget(effective budget)`; elites additionally `×3` | `Assets/Scripts/Sim/CinderSim.cs::SpawnEnemy`; `Assets/Scripts/Sim/HackTypes.cs::HackSpec.DungeonEnemyBaseHealth`, `EliteHealthMul`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::WaveBudgetSpec.HealthMultiplierForBudget` |
| `[OBSERVED-RUNTIME]` | enemy cadence | `(1.22 + min(0.38,wave×0.025)) × difficulty cooldown multiplier` | `Assets/Scripts/Sim/CinderSim.cs::UpdateEnemyAttacks` (`EnemyCooldownWaveCap`, `EnemyCooldownPerWave`); `Assets/Scripts/Sim/SimTypes.cs::SimConfig.EnemyAttackCooldown`; `Assets/Scripts/Sim/DifficultySpec.cs::DifficultyProfile.AttackCooldownMul` |

### 2.2 활성 graded loot

| status | mechanic | current value | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | deterministic grade roll | modulus `100`; Fine threshold `70`; Epic threshold `92` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::LootGradeSpec.RollModulus`, `FineThreshold`, `EpicThreshold`, `Roll` |
| `[OBSERVED-RUNTIME]` | pity | force at least Fine after `5` Basic; force Epic after `18` non-Epic; boss grade always Epic and outside pity ledger | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::LootGradeSpec.FinePityLimit`, `EpicPityLimit`, `BossGrade`, `Resolve`, `Advance` |
| `[OBSERVED-RUNTIME]` | grade payload | Basic/Fine/Epic value multipliers `{1.00,1.45,2.10}`; equipment rank steps `{1,1,2}` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::LootGradeSpec.GradeValueMul`, `GradeRankSteps` |
| `[OBSERVED-RUNTIME]` | base pickup payload | lifetime `12 s`; magnet `78`; ember shard `+18 HP`; oil flask `+35`; relic score `250` | `Assets/Scripts/Sim/SimTypes.cs::SimConfig.PickupLifetime`, `PickupMagnetRadius`, `EmberShardHeal`, `OilFlaskCharge`, `RelicScore` |

### 2.3 활성 boss variety

각 tuple은 `(phase count; P2/P3 HP fraction; cadence; speed; range; damage;
contact frame; phase escorts; health multiplier)` 순서다.

| status | archetype | current tuple | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | Warden | `2; .55/.55; {1.55,1.34,1.34}; {.82,.96,.96}; {1.34,1.48,1.48}; {1.34,1.72,1.72}; {3,3,3}; {0,0,0}; ×1.28` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.WardenProfile` |
| `[OBSERVED-RUNTIME]` | Tactician | `3; .72/.38; {.72,.62,.54}; {1.30,1.52,1.74}; {.90,.95,1.00}; {.84,.94,1.06}; {1,1,1}; {0,3,2}; ×.78` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.TacticianProfile` |
| `[OBSERVED-RUNTIME]` | Sovereign | `3; .66/.33; {1.12,.90,.68}; {1.00,1.28,1.60}; {1.06,1.16,1.26}; {1.00,1.22,1.48}; {3,2,1}; {0,1,2}; ×1.00` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.SovereignProfile` |
| `[OBSERVED-RUNTIME]` | Monarch | `3; .50/.20; {1.00,.85,.72}; {1.05,1.32,1.55}; {1.00,1.10,1.22}; {1.05,1.32,1.58}; {2,2,1}; {0,3,0}; ×1.15` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.MonarchProfile` |
| `[OBSERVED-RUNTIME]` | contact-frame timing | attack clip `12 fps`; legal contact frame `1..4`; profile telegraph seconds = `contactFrame/12` | `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.AttackClipFps`, `MinContactFrame`, `MaxContactFrame`; `BossArchetypeProfile.TelegraphSeconds` |
| `[OBSERVED-RUNTIME]` | boss HP formula | `86×HealthMultiplierForBudget(boss-wave budget)×6×6×profile.HealthMul`; budget/DDA and archetype make HP run-dependent | `Assets/Scripts/Sim/CinderSim.cs::StartWave`, `SpawnEnemy`; `Assets/Scripts/Sim/SimTypes.cs::SimConfig.BossHealthMul`; `Assets/Scripts/Sim/HackTypes.cs::HackSpec.DungeonBossHealthMul`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.For` |
| `[UNCONTRACTED]` | boss TTK | **no numeric target exists**; measure and report, but do not compare to an invented target | no code symbol and no approved design/QA target as of this snapshot |

## 3. 기믹 기준값

| status | gimmick | current value | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | ember vent | radius `90`; period `2.4 s`; telegraph `0.8 s`; player damage `8`; enemy damage `0` unless Ignition-B | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.VentRadius`, `VentPeriod`, `VentTelegraph`, `VentDamage`; `Assets/Scripts/Sim/CinderSim.cs::UpdateHazards` |
| `[OBSERVED-RUNTIME]` | obsidian pillar | radius `40`; player push radius `26`; enemy push radius `22`; solid blocker | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.PillarRadius`, `PlayerPushRadius`, `EnemyPushRadius`; `HazardConfig.Pillar` |
| `[OBSERVED-RUNTIME]` | relic altar | radius `70`; hold `1.2 s`; oil `+18`; cooldown `6 s` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.AltarRadius`, `AltarHoldSeconds`, `AltarOilBurst`, `AltarCooldown` |
| `[OBSERVED-RUNTIME]` | tide current | rectangular half-size `(520,110)`; period `6 s`; telegraph `0.8 s`; active `3.2 s`; push magnitude `200`; direct damage `0`; player/enemy both pushed | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.CurrentHalfW`, `CurrentHalfH`, `CurrentPeriod`, `CurrentTelegraph`, `CurrentActive`, `CurrentPush`; `HazardConfig.Current`; `Assets/Scripts/Sim/CinderSim.cs::UpdateHazards` |
| `[OBSERVED-RUNTIME]` | ember pylon | body radius `30`; aura radius `280`; HP `300`; in-aura enemy damage taken `×0.40`; no respawn; movement non-blocking | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.PylonBodyRadius`, `PylonAuraRadius`, `PylonHp`, `PylonAuraDamageTakenMult`; `HazardConfig.Pylon` |
| `[OBSERVED-RUNTIME]` | ash wall | edges `x=248/1288`; max depth `560`; rest/telegraph/advance/hold/recede `{4.5,1.5,7,3,7}s`; period `23 s`; speed `80`; tick `10` every `0.6 s`; player/enemy both damaged | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.WallEdgeX`, `WallEdgeRightX`, `WallDepthMax`, `WallRest`, `WallTelegraph`, `WallAdvance`, `WallHold`, `WallRecede`, `WallPeriod`, `WallSpeed`, `WallTickDamage`, `WallTickPeriod`; `HazardConfig.Wall` |

## 4. 현재 9개 logical stage

`W`는 일반 웨이브 수이고 보스는 `W+1`에서 등장한다. 일반 웨이브의 몸 수/HP는
§2.1 point budget가 결정하므로 과거의 고정 HP 열은 현재값으로 사용하지 않는다.

| status | logical stage → sim anchor | W | boss archetype | effective base hazard placement | code authority |
|---|---|---:|---|---|---|
| `[OBSERVED-RUNTIME]` | `cinder-span → cinder-span` | `5` | Warden | vent `(560,480,φ0)`, vent `(980,720,φ1.2)` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignStages.CinderSpanHazards`, `CampaignStages.Build`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |
| `[OBSERVED-RUNTIME]` | `ember-gallery → cinder-span` | `5` | Warden | vents `(560,480,0)`, `(980,480,.6)`, `(980,720,1.2)`, `(560,720,1.8)`; pillar `(768,604)` | `Assets/Scripts/View/StageCatalog.cs::EmberGalleryHazards`, `AllEntries`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |
| `[OBSERVED-RUNTIME]` | `abyss-chancel → abyss-chancel` | `6` | Tactician | pillars `(640,500)`, `(900,700)`, `(768,604)`; vent `(1100,450,.6)` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignStages.AbyssChancelHazards`, `CampaignStages.Build`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |
| `[OBSERVED-RUNTIME]` | `witness-well → abyss-chancel` | `6` | Tactician | altars `(560,500)`, `(980,700)`; pillar `(768,604)`; vents `(560,700,.3)`, `(980,500,1.5)` | `Assets/Scripts/View/StageCatalog.cs::WitnessWellHazards`, `AllEntries`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |
| `[OBSERVED-RUNTIME]` | `echo-throne → echo-throne` | `7` | Sovereign | altar `(768,604)`; vents `(500,700,0)`, `(1030,480,1.2)`; current `(768,604,+120,.3)` | `Assets/Scripts/View/StageCatalog.cs::EchoThroneHazards`, `AllEntries`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |
| `[OBSERVED-RUNTIME]` | `ash-verdict → echo-throne` | `7` | Sovereign | altar `(768,604)`; pylon `(960,540)`; vents `(560,480,0)`, `(980,720,1.2)` | `Assets/Scripts/View/StageCatalog.cs::AshVerdictHazards`, `AllEntries`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |
| `[OBSERVED-RUNTIME]` | `cinder-sluice → cinder-sluice` | `8` | Tactician | currents `(768,470,+200,0)`, `(768,740,−200,3)`; vents `(500,604,.9)`, `(1030,604,2.1)`; pillar `(768,604)` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignStages.CinderSluiceHazards`, `CampaignStages.Build`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |
| `[OBSERVED-RUNTIME]` | `ember-bastion → ember-bastion` | `8` | Warden | pylons `(560,500)`, `(980,700)`, `(768,430)`; pillars `(640,650)`, `(900,560)`; vent `(768,604,.6)` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignStages.EmberBastionHazards`, `CampaignStages.Build`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |
| `[OBSERVED-RUNTIME]` | `ash-march → ash-march` | `9` | Monarch | walls `(left,0)`, `(right,11.5)`; altar `(768,604)`; pylon `(768,520)`; vents `(560,760,.6)`, `(980,450,1.8)` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignStages.AshMarchHazards`, `CampaignStages.Build`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec.StageTable` |

### Verdict Pact append-only placements

| status | stage | appended placement | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | cinder-span | vent `(768,604,.6)` | `Assets/Scripts/View/StageCatalog.cs::CinderSpanPact` |
| `[OBSERVED-RUNTIME]` | ember-gallery | pillars `(768,468)`, `(768,740)` | `Assets/Scripts/View/StageCatalog.cs::EmberGalleryPact` |
| `[OBSERVED-RUNTIME]` | abyss-chancel | pillar `(900,500)` | `Assets/Scripts/View/StageCatalog.cs::AbyssChancelPact` |
| `[OBSERVED-RUNTIME]` | witness-well | vent `(560,500,.9)` | `Assets/Scripts/View/StageCatalog.cs::WitnessWellPact` |
| `[OBSERVED-RUNTIME]` | echo-throne | current `(768,740,−120,3.3)` | `Assets/Scripts/View/StageCatalog.cs::EchoThronePact` |
| `[OBSERVED-RUNTIME]` | ash-verdict | pylon `(576,668)` | `Assets/Scripts/View/StageCatalog.cs::AshVerdictPact` |
| `[OBSERVED-RUNTIME]` | cinder-sluice | vent `(768,604,1.7)` | `Assets/Scripts/View/StageCatalog.cs::CinderSluicePact` |
| `[OBSERVED-RUNTIME]` | ember-bastion | pylon `(620,720)` | `Assets/Scripts/View/StageCatalog.cs::EmberBastionPact` |
| `[OBSERVED-RUNTIME]` | ash-march | vent `(768,796,1.2)` | `Assets/Scripts/View/StageCatalog.cs::AshMarchPact` |

## 5. 각인(sigil) modifier 표

Permanent A/B modifiers는 상수 교체다. Peril clauses는 Countercurrent/Executioner/
Witness 중 낮은 slot 하나만 발동하고, Verdict/Ignition surge clause는 face-independent다.

| status | sigil ↔ gimmick | face A | face B | peril/surge clause | code authority |
|---|---|---|---|---|---|
| `[OBSERVED-RUNTIME]` | Countercurrent ↔ current | player push `×0.5` (`200→100`) | enemy push `×1.5` | Peril 동안 player current push `0`; peril 우선순위 1개만 | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.SigilCurrentPlayerPushMult`, `SigilCurrentEnemyPushMult`, `SigilLoadout.PerilPriority`; `Assets/Scripts/Sim/CinderSim.cs::ResolveSigils`, `PlayerCurrentPushMult` |
| `[OBSERVED-RUNTIME]` | Verdict ↔ pylon | aura damage-taken multiplier `0.40→0.70` | pylon strike `×2` | Surge 동안 pylon aura multiplier `1.0` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.SigilPylonAuraRelief`, `SigilPylonStrikeMult`; `Assets/Scripts/Sim/CinderSim.cs::ResolveSigils`, `PylonAuraMultiplierAt` |
| `[OBSERVED-RUNTIME]` | Executioner ↔ wall | player tick `10→6` | enemy tick `10→18` | Peril 동안 A tick 추가 `×0.5` (`6→3`); peril 우선순위 1개만 | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.SigilWallPlayerTick`, `SigilWallEnemyTick`, `SigilLoadout.PerilPriority`; `Assets/Scripts/Sim/CinderSim.cs::ResolveSigils`, `UpdateHazards` |
| `[OBSERVED-RUNTIME]` | Ignition ↔ vent | player vent hit마다 oil `+12`, damage `8` 불변 | vent enemy damage `14` | Surge 동안 enemy hazard damage `×3` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.SigilVentOilRefund`, `SigilVentEnemyDamage`, `SigilSurgeEnemyHazardMult`; `Assets/Scripts/Sim/CinderSim.cs::ResolveSigils`, `SurgeEnemyMult`, `UpdateHazards` |
| `[OBSERVED-RUNTIME]` | Witness ↔ altar | hold `1.2→0.8 s` | oil burst `18→30` | Peril 동안 hold `0`; peril 우선순위 1개만 | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.SigilAltarHoldSeconds`, `SigilAltarOilBurst`, `SigilLoadout.PerilPriority`; `Assets/Scripts/Sim/CinderSim.cs::ResolveSigils`, `UpdateHazards` |

## 6. Surge/Training/economy surfaces

| status | mechanic | current value | code authority |
|---|---|---|---|
| `[OBSERVED-RUNTIME]` | Peril window | arm `<35% HP`; re-arm `>50% HP`; run cap `2`; duration `3 s` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.PerilHealthFraction`, `PerilRearmFraction`, `PerilRunCap`, `PerilSeconds` |
| `[OBSERVED-RUNTIME]` | Surge window | every `12` cumulative kills; cap `1/wave`; duration `6 s`; window alone has no multiplier | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.SurgeKillInterval`, `SurgeWaveCap`, `SurgeSeconds`; `Assets/Scripts/Sim/CinderSim.cs::SurgeEnemyMult` |
| `[OBSERVED-RUNTIME]` | Training | `5` trials; `60 s`; `3` tiers with period rates `{1.00,.85,.70}`; all-top-tier one-time reward `2` relics | `Assets/Scripts/Sim/CampaignTypes.cs::TrainingTrials.Ids`; `Assets/Scripts/Sim/HackTypes.cs::HackSpec.TrainingSeconds`, `TrainingTiers`, `TrainingTierRate`, `TrainingMasteryRelics` |
| `[OBSERVED-RUNTIME]` | clear points | first clear `+3`, repeat `+2` | `Assets/Scripts/View/GameDirector.cs::BankDungeonClear` |
| `[OBSERVED-RUNTIME]` | cycle-2 first-clear relic bonus | cinder-sluice `+6`; ember-bastion `+8`; ash-march `+10`; other six `0` | `Assets/Scripts/View/GameDirector.cs::FirstClearRelicBonus` |
| `[OBSERVED-RUNTIME]` | Verdict Pact payout | in-run relics `×2`; first-clear bonus not doubled | `Assets/Scripts/View/GameDirector.cs::PactRelicMultiplier`, `BankDungeonClear` |
| `[OBSERVED-RUNTIME]` | duplicate extraction | `+30` relics; new extraction damage bonus `+8%` | `Assets/Scripts/Sim/HackTypes.cs::HackSpec.ExtractionDuplicateRelics`, `ExtractionDamageBonus` |

## 7. G2 measurement targets — 런타임 값과 분리

아래는 코드가 소비하는 balance 값이 아니다. 따라서 `code authority`가 없고 QA/문서
권한을 적는다.

| status | metric | target | authority |
|---|---|---|---|
| `[TARGET]` | matchup win rate | `45–55%` per declared cell | `_workspace/current/qa/test-plan.md::§1 G3 sampling contract`, `§7 G2`; no runtime code symbol |
| `[TARGET]` | mob-wave clear TTK | waves `1–3: 12 s`, `4–6: 22 s`, `7–9: 34 s`, each `±15%` | this balance sheet is the target owner; consumed by `_workspace/current/qa/test-plan.md::G2`; no runtime code symbol |
| `[UNCONTRACTED]` | boss TTK | no numeric target | no approved target owner and no runtime code symbol |
| `[TARGET]` | pair EV | every pair `≤1.3×` median pair EV | `_workspace/current/qa/test-plan.md::§7 G2`; no runtime code symbol |
| `[TARGET]` | hazard damage share | kiter run `10–35%` | `_workspace/current/qa/test-plan.md::G2 measurement table`; no runtime code symbol |
| `[TARGET]` | single gimmick hit | `≤30%` of max HP | `_workspace/current/qa/test-plan.md::G2 measurement table`; no runtime code symbol |
| `[TARGET]` | simultaneous telegraphs | total `≤3`, same kind `≤2` | `Assets/Scripts/View/StageCatalog.cs` table contract comments; QA census required, not a runtime clamp |

No target row is an observation. Cycle-9 raw Unity rows must carry run/build/policy/stage/
difficulty/loadout/input-script/outcome/timing identity before any target comparison.

## 8. HISTORICAL — NOT ACTIVE: v1 drift record

이 표는 문서 역사를 보존하기 위한 교정 기록이다. 왼쪽 수치는 현재값이 아니다.

| historical surface | superseded v1 value | current runtime correction | current code authority |
|---|---|---|---|
| tide-current band | half-height `70`; active `2.4 s`; push `140` | half-height `110`; active `3.2 s`; push `200` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.CurrentHalfH`, `CurrentActive`, `CurrentPush` |
| cinder-sluice placement | two currents only | two currents plus vents `(500,604,.9)/(1030,604,2.1)` and pillar `(768,604)` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignStages.CinderSluiceHazards` |
| ember-pylon tuning | aura `220`; HP `240`; aura multiplier `0.60` | aura `280`; HP `300`; aura multiplier `0.40` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.PylonAuraRadius`, `PylonHp`, `PylonAuraDamageTakenMult` |
| ember-bastion placement | two pylons | three pylons `(560,500)/(980,700)/(768,430)`, two pillars, centre vent | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignStages.EmberBastionHazards` |
| ash-wall timetable | depth `360`; rest/advance/recede `9/4.5/4.5 s`; period `22.5 s`; tick `8` | depth `560`; rest/advance/recede `4.5/7/7 s`; period `23 s`; tick `10` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignSpec.WallDepthMax`, `WallRest`, `WallAdvance`, `WallRecede`, `WallPeriod`, `WallTickDamage` |
| ash-march placement | one left wall; altar `(1100,604)`; vent `(980,480,1.2)` | opposing walls at phases `0/11.5`; altar `(768,604)`; pylon `(768,520)`; vents `(560,760,.6)/(980,450,1.8)` | `Assets/Scripts/Sim/CampaignTypes.cs::CampaignStages.AshMarchHazards` |
| fixed wave enemy HP | `86+min(140,(wave−1)×11)` | shipping Dungeon uses active point-budget HP `86×HealthMultiplierForBudget(effective budget)` | `Assets/Scripts/View/GameView.cs::DungeonProgression`; `Assets/Scripts/Sim/CinderSim.cs::SpawnEnemy`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::WaveBudgetSpec` |
| fixed boss HP `1044` | assumed `(86+88)×6` | shipping HP is DDA/boss-wave/archetype dependent; see §2.3 formula | `Assets/Scripts/Sim/CinderSim.cs::StartWave`, `SpawnEnemy`; `Assets/Scripts/Sim/DungeonProgressionSpec.cs::BossVarietySpec` |

이 교정은 런타임을 retune하지 않는다. frozen contract 파일과 simulation 숫자는
변경하지 않았고, 현재 코드 권한을 문서에 다시 연결했을 뿐이다.
