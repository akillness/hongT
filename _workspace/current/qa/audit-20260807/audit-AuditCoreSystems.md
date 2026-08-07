# 감사 리포트 — 심 코어 시스템 (§0–§6 + §12/§12.1 + AMENDMENT #7/#8/#9 + SIM_SPEC 수치계약)

- 감사자: AuditCoreSystems (Sim Systems Auditor)
- 일자: 2026-08-07
- 대상: `docs/SIM_SPEC_HACKSLASH.md` §0–§6, §12, §12.1, A3/A4(준비오퍼)/A6/A7/A8/A9 ↔
  `Assets/Scripts/Sim/{CinderSim.cs, HackTypes.cs, SimTypes.cs, CampaignTypes.cs, CharacterRoster.cs}`
  + `docs/SIM_SPEC.md` 아레나 수치계약 ↔ `SimTypes.cs(SimConfig)`/`CinderSim.cs`
- 방법: 스펙 전문 통독 → 조항별 코드 대조(파일:라인) → EditMode 테스트 커버리지 매핑 →
  레인 리포트(이월 증거) 재검증 → 외부 레퍼런스 가이드 갭 분석(프레이밍 가드 적용).
- 범위 외: 뷰 구현(카메라/토스트/로비/HUD/VFX — 타 에이전트), §7 보스전 세부(AuditBossPlayer),
  드롭률 상세(AuditLootDrops). 단, §5 포인트 획득·§6 유물가격처럼 스펙 §번호가 요구한 수치가
  뷰에 있는 경우 값 일치만 확인해 기록했다.

**총평**: 감사 범위의 수치/규칙 조항 **전부 구현 확인**. 스펙과 코드가 상충하는 심 로직 결함은
발견하지 못했다. 발견한 갭은 (a) 스펙 문언 대비 구현 의미론이 미세하게 다른 1건(A9.4 "스윙당
1회 샘플링" — Low), (b) 오래된 주석 1건(Low), (c) 스펙 자체가 §12 동결 목록과 후속 조항 사이에서
자기모순인 지점의 문서화된 해소 3건(정보성)이다.

---

## 1. 조항별 대조표

판정: ✅ 구현됨(값 일치) / ⚠ 부분/해석 개입 / ❌ 미구현 / ✖ 상충

### §0 모드와 상태머신

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| 시뮬레이션 모드 3종 | Arena/Prologue/Dungeon | HackTypes.cs:16 `enum GameMode { Arena=0, Prologue=1, Dungeon=2 }` | ✅ |
| Arena = 기존 그대로(회귀 게이트) | 동일 경로 | CinderSim.cs:252-256 (`HackConfig.Arena()` 경로가 `_prologue=false,_dungeon=false`), 테스트 `Regression_HackArenaConfig_ReproducesTheFrozenArenaRun` (HackSimTests.cs:271) | ✅ |
| 씬/URL 계약, GameDirector 소유 | 뷰 | (범위 외 — 타 에이전트) | — |

### §1 Prologue

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| 웨이브 3개: 4/6/8기 | {4,6,8} | HackTypes.cs `HackSpec.PrologueWaves=3`, `PrologueSpawns={4,6,8}`, `PrologueSpawnCount()`; CinderSim.cs:2783-2787 (StartWave 분기) | ✅ |
| 기믹 없음·보스 없음 | — | CinderSim.cs:2786 `_pendingBoss=false`; CinderSim.cs:363 해저드는 `_dungeon`만 로드 | ✅ |
| 스킬/대시/콤보 비활성 | 이동+기본공격만 | CinderSim.cs:909-912 (`CastSkills`가 `_prologue`면 즉시 return — Nova/Ward/대시/Q/E 전부), CinderSim.cs:1998-2001 (콤보는 `_dungeon`만; 프롤로그는 아레나 공격 경로 2004-2050) | ✅ |
| 아레나 수치 그대로 (이동 218, 공격 58/160/0.48, 기름) | SimConfig 값 | CinderSim.cs:2964-2973 (`_hack && !_dungeon` → 랭크 0, `SimConfig.Player*` 베이스); 적 HP도 아레나 곡선(CinderSim.cs:2858-2862 `_dungeon` 분기의 else) | ✅ |
| 클리어 → prologueDone 저장 | localStorage | 심: CinderSim.cs:2837-2840 `ClearRun("prologue-clear")`; 저장은 뷰(CampaignStore.cs:74 `prologueDone` 파싱 확인) | ✅ (심 몫) |
| 카메라/토스트 4단계 | 뷰 | (범위 외) | — |

### §2.1 기본 콤보

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| **3타 피해 비율 1:1:1.5** (58/58/87) | `playerDamage × {1,1,1.5}` | HackTypes.cs `ComboDamageScale = { 1f, 1f, 87f/58f }` (87/58 = 정확히 1.5); 적용 CinderSim.cs:2268 | ✅ |
| 스윙 0.30/0.30/0.42 s | {0.30,0.30,0.42} | HackTypes.cs `ComboSwing = { 0.30f, 0.30f, 0.42f }` | ✅ |
| 활성창 [0.10,0.22)/[0.10,0.22)/[0.14,0.30) | from/to 배열 | HackTypes.cs `ComboActiveFrom={0.10,0.10,0.14}`, `ComboActiveTo={0.22,0.22,0.30}`; 판정 CinderSim.cs:2195 (`>= from && < to` — 반개구간 일치) | ✅ |
| 3타 넉백 120 px/0.18 s | 120/0.18 | HackTypes.cs `ComboKnockbackDistance=120f`, `ComboKnockbackTime=0.18f`; CinderSim.cs:2297-2299 | ✅ |
| 콤보 연결: 종료 후 0.9 s 내 재입력 | 0.9 | HackTypes.cs `ComboLinkWindow=0.9f`; CinderSim.cs:2206-2207 (스윙 종료 시 링크 오픈), 2128 (링크 내 재입력 → 다음 타), 2161-2164 (만료 → 1타 리셋) | ✅ |
| 사거리 160/전방 dx·facing≥−18/1스윙 1피격 상속 | 아레나 계약 | CinderSim.cs:2281-2293 (`PlayerAttackRange`, `FacingArcTolerance`, `LastHitAttack`) | ✅ |
| 던전 적 HP `86+min(140,(wave−1)*11)` | 86/11/140 | HackTypes.cs `DungeonEnemyBaseHealth=86, PerWave=11, Cap=140`; CinderSim.cs:2858-2860 | ✅ |
| `ComboFinisher` 이벤트 | 1<<21 | SimTypes.cs:118; 발화 CinderSim.cs:2306-2309 | ✅ |

### §2.2 대시

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| **거리 190 px / 시간 0.22 s** | 190/0.22 | HackTypes.cs `DashDistance=190f, DashTime=0.22f`; 이동 CinderSim.cs:2059-2062 (마지막 스텝을 잔여시간으로 클립 → 총 이동 정확히 190) | ✅ |
| **무적 전구간** | i-frame | CinderSim.cs:2351-2354 (`DamagePlayer`가 `_dashTime > 0f`면 grace조차 소모 없이 return) | ✅ |
| **쿨 1.6 s / 기름 8** | 1.6/8 | HackTypes.cs `DashCooldownSeconds=1.6f, DashCost=8f`; 소비/설정 CinderSim.cs:1011, 1017 | ✅ |
| 이동 입력 방향(무입력 시 facing) | — | CinderSim.cs:997-1009 | ✅ |
| 콤보 캔슬 가능 | — | CinderSim.cs:1027-1028 (`_comboSwing=-1`, 링크 윈도 재오픈) | ✅ |
| 아레나 클램프 적용 | — | CinderSim.cs:2063 `ClampToArena(...)` | ✅ |
| 이벤트 `DashUsed` | 1<<14 | SimTypes.cs:111; CinderSim.cs:1030 | ✅ |
| (§12.1.2) 민첩 성장 → 대시 쿨 −6%/pt, 하한 ×0.55 | 0.06/0.55 | HackTypes.cs `GrowthSwiftnessCooldown=0.06f`, `Floor=0.55f`; CinderSim.cs:1017-1019 | ✅ |

### §2.3 스킬 4종

| 키/id | 스펙 값 | 구현 위치 (HackTypes.cs 상수 + CinderSim.cs 캐스트) | 판정 |
|---|---|---|---|
| Q rift-bolt (void) | 420 px 최근접 145 피해 + 반경 115 스플래시 60%, 쿨 6.5, 기름 25 | `BoltRange=420, BoltDamage=145, BoltSplashRadius=115, BoltSplashScale=0.6, BoltCooldown=6.5, BoltCost=25, BoltElement=Void`; CastRiftBolt CinderSim.cs:1034-1068 (스플래시는 1차 타깃 좌표 기준, 1차 타깃 제외 — 스펙 문언과 합치) | ✅ |
| E grave-pulse (ember) | 자기 위치 필드 반경 190, 3 s, 0.5 s마다 26, 쿨 4.0, 기름 30 | `PulseRadius=190, PulseDuration=3, PulseTickInterval=0.5, PulseTickDamage=26, PulseCooldown=4, PulseCost=30, PulseElement=Ember`; CastGravePulse CinderSim.cs:1071-1080 (시전 좌표 고정), 틱 UpdatePulseField 1124-1154 | ✅ |
| R ash-nova (ember) | 360° 반경 230, 110 피해, 넉백 120, 쿨 8.0, 기름 45 | `AshNovaRadius=230, AshNovaDamage=110, AshNovaKnockback=120, AshNovaCooldown=8, AshNovaCost=45, AshNovaElement=Ember`; CastAshNova CinderSim.cs:1083-1104 | ✅ |
| F void-aegis (frost) | 실드 +40, 8 s 또는 소진, 시전 무적 0.2 s, 쿨 12.0, 기름 30 | `AegisShield=40, AegisDuration=8, AegisCastInvuln=0.2, AegisCooldown=12, AegisCost=30, AegisElement=Frost`; CastVoidAegis CinderSim.cs:1107-1121; 흡수 DamagePlayer 2365-2380; 만료 UpdateSkills 1914-1921 | ✅ |
| 즉발(캐스팅 바 없음) | — | 각 Cast*가 입력 틱에 즉시 판정 (CastDungeonSkills CinderSim.cs:970-992) | ✅ |
| 입력 매핑: Nova→R, Ward→F 재사용, Q/E 신규 | SimInput | SimTypes.cs:26-32 (`NovaQueued`/`WardQueued` 재사용 — CinderSim.cs:984-991에서 ash-nova/aegis로 라우팅, `BoltQueued`/`PulseQueued` 신규) | ✅ |
| 이벤트 `BoltCast`/`PulseCast` 신설, `NovaCast`/`WardCast` 재사용 | 1<<15/1<<16 | SimTypes.cs:112-113; CinderSim.cs:1038, 1079, 1090(NovaCast 재사용), 1120(WardCast 재사용) | ✅ |
| AoE 링 = 심 판정 반경 동일 | 뷰 | (범위 외 — AuditPresentation) | — |

### §2.4 원소 상성

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| 사이클 ember>frost>veil>void>ember | 1스텝 우위 | HackTypes.cs `Element {None=0,Ember=1,Frost=2,Veil=3,Void=4}` + `Beats()`: `(int)attacker % 4 + 1 == (int)defender` (Void(4)%4+1=1=Ember — 순환 폐합) | ✅ |
| **유리 +20% / 불리 −15%** | ×1.2 / ×0.85 | HackTypes.cs `ElementAdvantage=1.2f, ElementDisadvantage=0.85f`; `Matchup()` | ✅ |
| 적 원소 매핑 | cohort=ember, scout=frost, shade=veil, possessed=void, Commander=veil, Monarch=void | HackTypes.cs `ElementOf()` — 6개 전부 일치 | ✅ |
| 기본공격/콤보 무원소, 스킬만 상성 | — | 콤보 SwingCombo/ReleaseCharge는 `ElementalDamage` 미경유(CinderSim.cs:2250, 2301 — 원시 damage); 스킬 3종만 `ElementalDamage()` 경유 (1061, 1066, 1101, 1144) | ✅ |

### §2.5 인런 성장 (XP)

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| XP: 일반 10 / 정예 25 / 보스 150 | 10/25/150 | HackTypes.cs `XpPerKill=10, XpPerElite=25, XpPerBoss=150`; 지급 CinderSim.cs:2660-2666 (`_dungeon` 게이트) | ✅ |
| **곡선 [30,55,85,120,160,205,255,310], 이후 +60, 캡 12** | 배열+60/캡12 | HackTypes.cs `XpCurve={30,55,85,120,160,205,255,310}`, `XpPerLevelBeyondCurve=60`, `LevelCap=12`; `XpToNextLevel()` (9→370, 10→430, 11→490, 12→0) | ✅ |
| 레벨업: 피해 +4%, 최대 HP +6(+6 회복), 재생 +0.3/s | 0.04/6/0.3 | HackTypes.cs `LevelDamageBonus=0.04f, LevelHealthBonus=6f, LevelRegenBonus=0.3f`; ApplyLevelStats CinderSim.cs:3016-3022; 회복 GainXp 1304-1306 (max 증가분만큼) | ✅ |
| 이벤트 `LevelUp` | 1<<17 | SimTypes.cs:114; CinderSim.cs:1307 | ✅ |
| XP 오버플로 이월 | (스펙 무언) | GainXp 1282-1292 (`_xp -= required` 루프 — 잉여 이월, 다중 레벨업 지원) | ✅ [INFERENCE — 합리적 해석] |

### §3 정예와 추출

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| **정예: 7번째 스폰마다** (`spawnOrdinal % 7 == 0`) | mod 7 | HackTypes.cs `EliteSpawnModulus=7`; CinderSim.cs:2865-2869 (`_dungeon && !boss`에서만 ordinal 증가 — 보스는 카운트 제외) | ✅ |
| 웨이브당 최대 1 | — | CinderSim.cs:2868 `!_eliteThisWave &&`, StartWave 2795에서 웨이브마다 리셋. ordinal 자체는 런 전역(ResetHackRun 3065에서만 0) — 스펙 공식이 리셋을 명시하지 않으므로 합치 [INFERENCE] | ✅ |
| **HP ×3 / 접촉 ×1.5 / 스케일 ×1.35** | 3/1.5/1.35 | HackTypes.cs `EliteHealthMul=3f, EliteDamageMul=1.5f, EliteScale=1.35f`; 적용 CinderSim.cs:2885-2888(HP), 2574-2575(접촉), 2907(스케일) | ✅ |
| 정예 사망 → 시체 마커 10 s | 10 | HackTypes.cs `CorpseLifetime=10f`; DropCorpse CinderSim.cs:1376-1389, 노화 UpdateExtraction 1398-1406 | ✅ |
| **추출: 반경 90 px, 정지 2.0 s 연속** | 90/2.0 | HackTypes.cs `ExtractionRadius=90f, ExtractionSeconds=2f`; UpdateExtraction 1418-1452 (아이소 반경 90, `_player.Moving`이면 리셋) | ✅ |
| 피격 시 채널 리셋 | — | CinderSim.cs:1442 `(_events & SimEvents.PlayerDamaged) != 0` → progress=0 (같은 틱 판정 — UpdateExtraction이 적 갱신 뒤에 돎) | ✅ |
| **보상: 신규 → 로스터 등록 + 이번 런 피해 +8% / 중복 → 유물 +30** | 0.08/30 | HackTypes.cs `ExtractionDamageBonus=0.08f, ExtractionDuplicateRelics=30`; CompleteExtraction CinderSim.cs:1465-1479 (RosterMask 비트 분기) | ✅ |
| 웨이브당 추출 1회 | — | CinderSim.cs:1410 `_extractedThisWave` 게이트, StartWave 2796 리셋 | ✅ |
| 이벤트 `EliteDown`/`ExtractionComplete` | 1<<18/1<<19 | SimTypes.cs:115-116; CinderSim.cs:2658, 1478 | ✅ |
| `<visual>-echo` 명명 | 뷰/영속성 | 심은 비트마스크(RosterMask)만 소유 — 문자열 변환은 뷰 몫. 문서화된 설계(§12 대비 RosterMask 추가, 하단 §3-비고) | ✅ (심 몫) |

### §4 동료 (+A3 홀드/리콜, A6 멀티슬롯, A7 자율성, A8 시그니처 스킬)

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| **80 px 오프셋 추종** | 80 | HackTypes.cs `CompanionFollowOffset=80f`; 앵커 CinderSim.cs:1532 (`player.X − 80×facing`) | ✅ |
| **1.1 s마다 / 200 px 내 최근접 / 플레이어 피해의 60%** | 1.1/200/0.6 | HackTypes.cs `CompanionAttackInterval=1.1f, CompanionAttackRange=200f, CompanionDamageScale=0.6f`; 스윙 CinderSim.cs:1610-1634 | ✅ |
| 상성 무원소 | — | CinderSim.cs:1634 `DamageEnemy(..., _playerDamage × scale)` — ElementalDamage 미경유 | ✅ |
| 피격 대상 아님 (untargetable) | — | 동료는 `_enemies` 배열 외부의 별도 좌표 배열 — 적 접촉/판정 루프에 구조적으로 등장 불가 | ✅ |
| 뷰: 틴트/스케일 0.92 | 뷰 | (범위 외) | — |
| 해금 매핑 (보스 첫 처치 보상) | 뷰/영속성 | (범위 외 — GameDirector/CampaignStore) | — |
| A3: `CompanionHoldQueued`/`RecallQueued`, 리콜 우선 | — | SimTypes.cs:33-34; UpdateCompanionBehavior CinderSim.cs:1493-1503 (recall 우선), Restart → Follow (3079) | ✅ |
| A3: 홀드는 이동만 중지, 전투 유지 | — | CinderSim.cs:1531-1533 (held면 자기 위치가 앵커), 1540 (Follow만 이동), 스윙은 무조건 | ✅ |
| A6: 0..3 슬롯, dedupe, CompanionIds 우선 | — | HackTypes.cs `NormalizeCompanionSlots()`; 팬아웃 `CompanionSlotFanout={0,+64,−64}` (CinderSim.cs:1525, 1533) | ✅ |
| A6 D6.3 아키타입 튜플 | scout 0.85/240/0.50, shade 1.30/260/0.65, possessed 1.45/150/0.80, ember-cohort=§4 | HackTypes.cs `CompanionStats()` — 4행 전부 일치 (ember-cohort는 승인된 정정대로 §4 고정) | ✅ |
| **A7: Acquire 300 / Leash 320 / 추격 ×1.05 / 락 2 s / 복귀 그레이스 0.35 s** | 상수 5종 | HackTypes.cs `CompanionAcquireRadius=300, LeashRadius=320, PursuitSpeedScale=1.05, TargetLockSeconds=2, ReturnGraceSeconds=0.35`; ResolveCompanionTarget CinderSim.cs:1649-1686 (id 락·1틱 릴리스), 추격/리시 1540-1576 | ✅ |
| A7: 앵커 기준 반경, 스윙 지오메트리 불변, 락이 스윙을 잃게 하지 않음 | — | CinderSim.cs:1527-1533 (앵커), 1607-1619 (락 우선 + 최근접 폴백) | ✅ |
| **A8 테이블: Volley 6.0/240/0.55/3/2/0 · Hex 8.0/260/0.40/8/2/0 · Quake 9.0/170/0.70/6/2/90 · Flare 7.0/200/1.10/1/1/0** | 4행×7열 | HackTypes.cs `CompanionSkill()` — 28개 값 전부 일치; `FlashSeconds=0.35`, `TargetCap=8` | ✅ |
| A8: 쿨 풀차지 시작·자동발사 MinAutoTargets·명령 우회(1기)·비버퍼·홀드 중 시전 가능 | — | ResetCompanion CinderSim.cs:3116 (풀 쿨), UpdateCompanionSkill 1698-1721 (`required = skillQueued ? 1 : MinAutoTargets`, 쿨 중 return = 비버퍼), 홀드와 무관하게 호출(1594) | ✅ |
| A8: 순서 targeting→movement→skill→swing | — | UpdateCompanionSlot: 1536(타깃)→1540-1576(이동)→1594(스킬)→1597-1634(스윙) | ✅ |
| A8: `CompanionSkillCast = 1<<22`, 중립 피해, GuardianResonance 미적용 | — | SimTypes.cs:121; CastCompanionSkill 1731-1762 (`_playerDamage × skill.DamageScale`, 원소 없음); 생성자 353 주석+구현 (스킬 스펙은 resonance 폴딩 전에 캡처) | ✅ |
| A4 GuardianResonance (선택 오퍼 시만): cadence `max(0.5, 1.1×(1−0.1m))` / range `+20m` / damage `×(1+0.1m)` | — | ApplyGuardianResonance CinderSim.cs:442-468 (0.5 floor, +20m, ×(1+0.1m)); 매 슬롯 D6.3 베이스 뒤 적용 (354-358) | ✅ |

### §5 메타 스탯

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| **공격 +3%/pt · 체력 +8 HP/pt · 이속 +2%/pt** | 0.03/8/0.02 | HackTypes.cs `AttackPerPoint=0.03f, VitalityHealthPerPoint=8f, SwiftnessSpeedPerPoint=0.02f`; 적용식 `HackConfig.PlayerDamage/MaxHealth/Speed` (HackTypes.cs:282-293) | ✅ |
| **캡 각 10** | 10 | HackTypes.cs `MaxStatPoints=10`, `ClampStat()` — 적용식 전부 경유 | ✅ |
| 던전 런 시작 시 적용, 프롤로그/아레나 미적용 | — | ResetCampaignRun CinderSim.cs:2950-2973 (`_dungeon`만 `_hackConfig.Player*`; 프롤로그/아레나는 SimConfig 베이스) | ✅ |
| 획득: 클리어 +2, 보스 첫 처치 +1 | 뷰 | GameDirector.cs:539-540 `_data.Points += firstClear ? 3 : 2` — 값 일치. (스테이지 클리어=보스 처치이므로 firstClear가 두 조건을 겸함 [INFERENCE]) | ✅ (뷰 몫) |

### §6 장비 T1–T5

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| 슬롯 3종 유지, **랭크 0-5 = T0-T5** | 캡 5 | CampaignTypes.cs:128 `MaxEquipRank=5`, `ClampRank()`; 심 진입 CinderSim.cs:2956-2958 | ✅ |
| **무기 +6%/T · 랜턴 +8%/T · 망토 +8HP/T** | 0.06/0.08/8 | CampaignTypes.cs:129-131 `WeaponDamagePerRank=0.06f, LanternRegenPerRank=0.08f, CloakHealthPerRank=8f`; 합성식 HackTypes.cs:282-297 (`58×(1+0.03a)×(1+0.06w)`, `100+8v+8c`, `7×(1+0.08l)`) | ✅ |
| 획득 (a) 인런 드롭: 보스 확정 + id%7 파편 | — | 보스: DamageEnemy CinderSim.cs:2673-2677 (`RaiseRank` 확정); 파편: SpawnPickup 2707-2708 (`enemyId % 7 == 3` — CampaignSpec.ShardDropModulus=7/Remainder=3, 캠페인 스펙 문서화 값) | ✅ |
| 획득 (b) **로비 구매 [2,4,7,11,16] 유물/티어** | {2,4,7,11,16} | 뷰: LobbyView.cs:51 / GameDirector.cs:426 `EquipCosts = { 2, 4, 7, 11, 16 }` — 두 곳 값 일치 | ✅ (뷰 몫) |
| 유물 = 메타 화폐 (런 종료 누적) | — | 심: RunDigest.Relics + 추출 중복 +30 (CinderSim.cs:1476); 누적 GameDirector.cs:541 `_data.Relics += sim.Relics`; 파싱 CampaignStore.cs:71 | ✅ |

### §12 / §12.1 SimTypes 증분 · 입력 뎁스

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| SimInput 추가: Dash/Bolt/PulseQueued, AttackHeld, GrowthChoice, CompanionHold/Recall/SkillQueued | bool×7 + int | SimTypes.cs:29-48 — 전부 존재, 기본값 false/0 | ✅ |
| SimEvents 8+2종 (1<<14 … 1<<23) | 비트 일치 | SimTypes.cs:110-127 — `DashUsed=1<<14`…`ComboFinisher=1<<21`, `CompanionSkillCast=1<<22`, `MomentumTierUp=1<<23` 전부 스펙 비트와 일치 | ✅ |
| `IHackSnapshot` 표면 | §12 목록 | HackTypes.cs:301-385 — Level/Xp/XpNext/ComboIndex/DashCooldown/SkillCooldowns[4]/Shield/ElitesAlive/Extraction*/Companion*/Boss*/Mode | ⚠ `Mode`→`HackMode` 개명 (하단 갭 G-3) |
| `CinderSim(in HackConfig)` 오버로드, 기본/캠페인 생성자 불변 | — | CinderSim.cs:301(신규), 기본 생성자·CampaignConfig 생성자 각각 유지 (252, 271) | ✅ |
| §12.1.1 차지: 무장 0.45 s / ×1.8 / 넉백 ×2.0 / 이동 ×0.45 / 미완성 폐기 / 체인 완료 후만 | 상수 4종 | HackTypes.cs `ChargeReadySeconds=0.45, ChargeDamageMul=1.8, ChargeKnockbackMul=2.0, ChargeMoveScale=0.45`; UpdateCombo CinderSim.cs:2119-2189 (chainSpent 게이트, 미완성 릴리스 폐기 2177-2181), ReleaseCharge 2214-2258, 이동 감속 1981 | ✅ |
| §12.1.2 성장 선택: 오퍼 5 s 자동확정 / 심 무정지 / 새 레벨업 시 대기 오퍼 자동확정 / 공격 +8% / 생명 +6(즉시 회복) / 민첩 +4% & 대시쿨 −6%(하한 0.55) | 상수 6종 | HackTypes.cs `GrowthOfferSeconds=5, GrowthAttackBonus=0.08, GrowthVitalityHealth=6, GrowthSwiftnessSpeed=0.04, GrowthSwiftnessCooldown=0.06, Floor=0.55`; GainXp 1314-1322 (교체 규칙), ApplyGrowthChoice 1329-1352, UpdateGrowthOffer 1356-1373 | ✅ |
| `IGrowthChoiceSnapshot` 추가 (IHackSnapshot 미개정) | — | GrowthChoiceSnapshot.cs (별도 파일, 인터페이스 추가 방식) — 스펙 §12.1.2 노출 규칙 그대로 | ✅ |

### AMENDMENT #9 모멘텀

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| Max 100 / PerHit 9 / PerKill 14 | 100/9/14 | HackTypes.cs `MomentumMax=100, MomentumPerHit=9, MomentumPerKill=14`; GainMomentum CinderSim.cs:867-876 | ✅ |
| 그레이스 1.6 s / 감쇠 12/s / 피격 −25 & 그레이스 취소 | 1.6/12/25 | HackTypes.cs `MomentumGraceSeconds=1.6, DecayPerSecond=12, HurtPenalty=25`; UpdateMomentumDecay 850-862, SpendMomentumOnHurt 880-888 (`_momentumGrace=0`) | ✅ |
| 티어 {0,30,60,90} → ×{1.00,1.08,1.18,1.30}, 포함 경계, 전역 함수 | 배열 2종 | HackTypes.cs `MomentumTierThresholds={0,30,60,90}`, `MomentumTierDamageMul={1,1.08,1.18,1.30}`, `MomentumTierOf()` (>= 포함, 상하 클램프) | ✅ |
| 멜레만 적립 (스킬/동료 제외) | — | GainMomentum 호출처는 SwingCombo(2302)와 ReleaseCharge(2251) 두 곳뿐 — 스킬/동료/펄스 경로 없음 | ✅ |
| 감쇠는 스윙 해소 전 | — | Tick 순서: UpdateMomentumDecay(804) → UpdatePlayer(806) | ✅ |
| `MomentumTierUp = 1<<23` 승급 엣지 트리거, 1틱 1회 | — | SimTypes.cs:127; PublishMomentumTier CinderSim.cs:892-900 (Tick 말미 840에서 1회 비교) | ✅ |
| 재시작 → 빈 게이지, 던전 전용 | — | ResetHackRun 3040-3042; `!_dungeon` 게이트 852, 869, 882 | ✅ |
| 배율 "스윙당 1회 샘플링" | — | SwingCombo 2268 / ReleaseCharge 2227 — 스윙 **틱당** 재계산 (하단 갭 G-1) | ⚠ |

### §13 결정론 · SIM_SPEC 아레나 수치계약 (발췌 대조)

| 조항 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|
| RNG 금지 / UnityEngine 금지 | — | Sim 폴더 전체 grep: `System.Random`/`UnityEngine` 참조 0건 (주석 제외). 준비 오퍼는 결정적 해시(PreparationHash CinderSim.cs:764-776) | ✅ |
| 고정스텝 1/60, MAX_FRAME_DELTA 0.25, CATCH_UP 5 | — | SimTypes.cs:176-178 | ✅ |
| 아레나 중심 (768,604), half 520/270, L1 다이아몬드(플레이어 margin 34/적 24) | — | SimTypes.cs:181-182, 190, 201; ClampToArena CinderSim.cs:3294-3313 (`halfH − margin×0.5` 포함, 공식 자구 일치) | ✅ |
| AMENDMENT #4: 던전만 L2 타원 | — | CinderSim.cs:3303-3305 (`_dungeon ? sqrt : L1`) | ✅ |
| 플레이어 HP 100/이동 218(y×0.68)/공격 58/160/0.48/grace 0.38/시작 (768,646) | — | SimTypes.cs:184-193 (`PlayerStartYOffset=42` → 604+42=646) | ✅ |
| 적 HP `58+min(92,(wave−1)*9)`, 사거리 76, 쿨 `1.22+min(0.38,wave*0.025)`, 속도식, 상한 20, 접촉식, 분리 70/0.76 | — | SimTypes.cs:197-206 + CinderSim.cs:23-41 (private const, 각 줄 스펙 주석) | ✅ |
| 기름 100/+7/+6, Nova 45/6.5/250/96, Ward 30/9/3 | — | SimTypes.cs:208-212 | ✅ |
| 픽업 `id%3`, 수명 12, 자력 78 | — | SimTypes.cs:214-218; SpawnPickup CinderSim.cs:2709 | ✅ |
| 웨이브 `min(20, 3+floor(n*1.2))`, 인터미션 2.15, 점수 100×wave | — | CinderSim.cs:37-42; SimTypes.cs:223 | ✅ |
| 보스 5의 배수, HP ×6/접촉 ×2/이속 ×0.7/스케일 ×1.6/점수 1000×wave | — | SimTypes.cs:226-227; CinderSim.cs:43, 2871-2884 | ✅ |
| 아이소 `dy×1.42`, 전방 `dx·facing ≥ −18` | — | SimTypes.cs:220-221 | ✅ |

---

## 2. 갭 / 상충 목록

| # | 심각도 | 내용 |
|---|---|---|
| G-1 | **Low** | [OBSERVED] **A9.4 "배율은 스윙당 1회 샘플링" 문언 vs 구현은 스윙-틱당 샘플링.** 스펙(SIM_SPEC_HACKSLASH.md:800-801) "sampled once per swing, before that swing's own hits feed the gauge". 구현 SwingCombo(CinderSim.cs:2264-2268)는 활성창(예: 3타 [0.14,0.30) ≈ 9틱) 동안 **매 틱 재호출**되며 damage를 틱마다 재계산한다. 같은 틱 안에서는 게이지 적립 전에 샘플하므로 "자기 히트가 자기 배율을 못 올린다"가 지켜지지만, **틱 N에서 적 A를 치고 티어가 승급하면, 같은 스윙(같은 attackId)의 틱 N+1에 활성창 안으로 걸어 들어온 적 B는 승급된 배율을 받는다.** 게이트 테스트(MomentumTests.cs:481 `…SampledOncePerSwing`)도 틱-전 티어 기준으로 검증하므로 이 교차-틱 케이스를 구분하지 못한다(같은 해석을 공유). 발생 조건이 드물고(스윙 중 티어 경계 통과 + 후속 틱 신규 진입 적) 플레이어에게 유리한 방향이며 다이제스트 계약(A9.1 실측값)은 현 구현 기준으로 고정되어 있으므로 Low. → 사람 판단 H-1. |
| G-2 | **Low** | [OBSERVED] **오래된 주석.** CinderSim.cs:1873 "S8-a: three phases on HP thresholds (70% / 40%)" — 실제 상수는 `BossPhase2HealthFraction=0.50 / BossPhase3HealthFraction=0.20`(HackTypes.cs)이고 코드는 상수를 쓴다. **동작은 스펙(50/20)과 일치**, 주석만 낡음. `// FROZEN CONTRACT` 파일이라 이 감사에서는 수정하지 않음. |
| G-3 | 정보성 | [OBSERVED] **§12 문언 대비 문서화된 개명:** `IHackSnapshot.Mode` → `HackMode`(HackTypes.cs:305-310). 사유(`ISimSnapshot.Mode`가 SimMode 타입으로 선점)는 코드 XML 주석과 레인 리포트 §6-1에 명기. 스펙 §12는 개정되지 않았으므로 문언 불일치 자체는 남아 있다. |
| G-4 | 정보성 | [OBSERVED] **§12 동결 목록 외 HackConfig 필드 3종:** `RosterMask`(§3 보상 분기의 필수 입력 — 스펙 자체가 §3과 §12 사이에서 자기모순), `PreparationOffer`(A4가 소급 추가), `CompanionIds`(A6가 소급 추가). 전부 A4/A6 조항 또는 레인 리포트 §6-2로 문서화됨. 스펙 §12 본문은 갱신되지 않았다. |
| G-5 | 정보성 | [INFERENCE] **레벨 피해 보너스의 합성 방식.** 스펙 §2.5 "피해 +4%"는 복리/단리를 명시하지 않는다. 구현은 단리 `×(1+0.04×(lv−1))`(CinderSim.cs:3016-3017) — 레인 리포트가 채택을 기록했고 테스트가 고정. 스펙 문언만으로는 두 해석 모두 가능하므로 스펙에 합성식 1줄을 못박는 것이 안전. |
| G-6 | 정보성 | [OBSERVED] **실드 전량 흡수는 PlayerDamaged를 안 띄움 → 추출 채널이 안 끊김**(CinderSim.cs:2365-2379, 1442). 스펙 §3 "피격 시 채널 리셋"의 "피격"을 "체력이 실제로 깎임"으로 해석한 문서화된 판단(레인 리포트 §6-7). aegis(§2.3 F)의 존재 의의와 정합적. 스펙 미개정 상태. |

**스펙과 상충(✖) 판정 조항: 0건.** 미구현(❌) 판정 조항: 0건 (심 범위 내).

## 3. EditMode 테스트 커버리지

최신 커밋된 Unity 실행 결과 [OBSERVED]:
`_workspace/current/engineering/unity-logs/test-results-094459.xml` —
**365/365 통과, 실패 0** (start-time 2026-08-07 00:45:12Z). Momentum_* 14, CompanionSkill_* 17,
CompanionAutonomy_* 다수 포함 — A7/A8/A9까지 전부 이 XML에 들어 있다.
(루트 `unity-logs/test-results-175105.xml`(195/195, 08-05)은 A7/A8/A9 테스트 **이전** 산출물 —
최신 증거는 위 파일이다.)

| 스펙 조항 | 게이트 테스트 (파일:테스트명) | 커버 |
|---|---|---|
| §1 프롤로그 4/6/8·보스없음·스킬무시·클리어 | HackSimTests: `Prologue_RunsThreeWavesOfFourSixEight_WithNoBoss`, `Prologue_IgnoresSkillAndDashInput`, `Prologue_ClearingAllThreeWaves_EndsWithPrologueClear` | ✅ |
| §2.1 콤보 타이밍/비율/링크/피니셔 | HackSimTests: `Combo_ChainsThreeHitsWithSpecSwingTimesAndActiveWindows`, `Combo_LinkWindowExpiresAfterNinetenthsAndRestartsTheChain`, `Combo_FinisherScalesDamageKnocksBackAndRaisesComboFinisher` | ✅ |
| §2.1 던전 적 HP 곡선 | `DungeonEnemyHealth_UsesTheComboCurveNotTheArenaCurve` | ✅ |
| §2.2 대시 190/기름/쿨/무적 | `Dash_TravelsOneHundredNinetyPixelsAndSpendsOilOnCooldown`, `Dash_IsInvulnerableForItsWholeDuration` | ✅ |
| §2.3 스킬 4종 수치 | `RiftBolt_HitsTheNearestTargetAndSplashesAtSixtyPercent`, `GravePulse_TicksEveryHalfSecondAndRollsTheElementCycle`, `GravePulse_LastsExactlyThreeSecondsOnADurableTarget`, `AshNova_DamagesInsideTheRadiusAndKnocksBack`, `VoidAegis_AbsorbsFortyDamageBeforeHealthMoves`, `Skills_CostsAndCooldownsMatchTheSkillTable` | ✅ |
| §2.4 상성 | `ElementCycle_AdvantageIsOneStepAhead` | ✅ |
| §2.5 XP 곡선/레벨업 효과 | `XpCurve_MatchesSpecAndCapsAtTwelve`, `Xp_LevellingRaisesDamageHealthAndRegen` | ✅ |
| §3 정예 | `Elite_EverySeventhDungeonSpawnIsATripleHealthElite`, `Elite_HasTripleHealthAndOneAndAHalfContact` | ✅ |
| §3 추출 | `Extraction_NeedsTwoStationarySecondsInsideNinetyPixels`, `Extraction_ChannelResetsWhenTheNextWaveLandsAHit`, `Extraction_NewVisualJoinsTheRosterAndBuffsRunDamage`, `Extraction_DuplicateVisualPaysThirtyRelics` | ✅ |
| §4 동료 기본 | `Companion_TrailsThePlayerAndAttacksOnItsOwnCadence` 외 hold/recall/inert/digest 7종 | ✅ |
| §5/§6 합성식 | `DerivedStats_CombineMetaStatsAndEquipTiers` | ✅ |
| §12.1 차지/성장 | `HoldingAttack_DoesNotAlterAMashingRun`, `HoldingAttack_ActuallyReachesAFullCharge`, `ChargeWindow_FitsInsideABossTelegraph`, `GrowthOffer_AutoConfirmsAndCostsNothingWhenIgnored`, `GrowthOffer_DoesNotPauseTheSim`, `FinisherVariant_ResolvesRelativeToFacing` 등 | ✅ |
| A6 슬롯 | `CompanionSlots_*` 8종 (스펙 D6.7 증명 맵과 1:1) | ✅ |
| A7 자율성 | `CompanionAutonomy_*` (HackSimTests 12 + CompanionAutonomyTests 14) | ✅ |
| A8 스킬 | `CompanionSkill_*` 17 (스펙 A8.7 증명 맵 14행 + 3) | ✅ |
| A9 모멘텀 | `Momentum_*` 14 (스펙 A9.7 증명 맵과 1:1) | ✅ |
| §13 결정론 | `Tick_SameInputScript_ProducesIdenticalRun`, `Companion_CommandSequencesProduceIdenticalSnapshotsAndDigests`, `Momentum_IdenticalInputsYieldIdenticalGaugeAndDigest` 등 각 계열 결정론 테스트 | ✅ |
| 미커버 (테스트 없음) | G-1의 교차-틱 배율 케이스 (테스트가 구현과 같은 해석을 공유) | ⚠ |

## 4. 외부 레퍼런스 가이드 대비 갭

기준: 레퍼런스(`llm-wiki/raw/sources/2026-08-07-hackslash-design-guide-reference.md`)는 일반론
참고자료이며 판정 기준이 아니다(프로젝트 계약 = docs/SIM_SPEC*.md + CLAUDE.md §2). 가이드의
"유니티 구현 관점"(AttackData ScriptableObject, OnTriggerEnter 히트박스, Animator 상태머신,
PostProcessingVolume)은 CLAUDE.md §1 순수 C# 결정론 심 경계와 충돌하므로 수정 제안 대상이 아니다.

| 가이드 항목 | 프로젝트 상태 | 분류 |
|---|---|---|
| "공격할수록 강해지는" 모멘텀 게이지 | A9가 정확히 이 요구를 유한·감쇠 게이지로 채택 (HackSpec.Momentum*) | 채택됨 |
| 입력 방향 분기 콤보 | §12.1 피니셔 변형(Launcher/Retreat/Spin — ResolveFinisherVariant) + 홀드 차지 | 채택됨 (변형) |
| 범위 공격 사전 표시(telegraph) | 보스 텔레그래프 0.80 s 고정(HackSpec.BossTelegraph) + AoE 링=판정 반경 계약(§2.3) | 채택됨 |
| Common/Rare/Epic/Legendary 희귀도 테이블 | 코드에 없음 — **결함 아님, 의도적 설계 분기.** HackTypes.cs `EquipTiers`(T0-T5 랭크) + 결정적 드롭(보스 확정 + `id%7==3` 파편 + `id%3` 픽업)이 대체. 확률 테이블은 §13 "전 모드 RNG 금지"와 양립 불가 | [INFERENCE] 의도적 미채택 |
| 동적 난이도 조정(DDA) | 없음 — §13 "같은 config+입력 → 같은 Digest" 계약과 정면 충돌하므로 채택 불가 | [INFERENCE] 의도적 미채택 |
| 포인트 기반 웨이브 생성(baseWavePoints/growthFactor) | 없음 — SIM_SPEC 동결 수식 `min(20, 3+floor(n*1.2))` 유지 (동결 계약) | [INFERENCE] 의도적 미채택 |
| bad-luck protection 드롭 가중치 | 없음 — 결정적 드롭이라 "운" 자체가 없음. (드롭 상세는 AuditLootDrops 소관) | [INFERENCE] 의도적 미채택 |
| 히트스톱(1-3프레임)/카메라 셰이크 | 심에는 없음 — 있다면 뷰 소관 (AuditPresentation 확인 대상) | 범위 외 라우팅 |
| 상태머신+데이터드리븐 콤보 구조론 | 심은 콤보를 HackSpec 상수 배열(데이터)+UpdateCombo(상태)로 구현 — 구조 목적은 충족, 구현 수단(SO/Animator)은 계약상 채택 불가 | 목적 충족 |
| **[OBSERVED] 스펙 자체의 갭 (가이드 대비): 0건** — 가이드가 요구하고 스펙이 누락한 심-로직 개념은 없다. 가이드의 나머지는 뷰/콘텐츠 구성론이다. | | |

## 5. 이월 증거 vs 신규 증거

**이월 (레인 리포트 프라이어 → 현재 코드로 재검증됨):**
- `gjc-hackslash-lane-report.md` §4의 §0-§7 조항별 구현 주장 — 본 리포트 §1 표의 파일:라인으로
  전부 재확인 (콤보 비율/대시/스킬4/상성/XP/정예/추출/동료/스탯/장비). 라인 번호는 이후 수정으로
  이동했으나 값은 전부 유지.
- 레인 리포트의 판단 6건(HackMode 개명, RosterMask 추가, 비율 콤보, 대시 y×0.68, 넉백 가산,
  BossPhase 유지, 실드-채널) — 코드 주석과 함께 현존 확인. G-3~G-6으로 재분류.
- 레인 리포트 61/61 테스트 주장 — **대체됨**: 현재 게이트는 365/365 (아래 신규).
- `gjc-sim-lane-report.md`의 아레나 20테스트/수치 대조 — SimConfig 대조로 재확인.
- (재검증 안 된 이월) 레인 리포트의 0-alloc 측정, dotnet 콘솔 실행, 파일럿 클리어 기록 —
  이번 감사에서 재실행하지 않음(읽기 전용·배치모드 금지 제약). 주장으로만 인용.

**신규 (이번 감사에서 처음 확립):**
- [OBSERVED] A7/A8/A9가 커밋된 Unity 실행 증거로 게이트됨: `test-results-094459.xml` 365/365
  (2026-08-07). 루트 `unity-logs/`의 195/195본(08-05)만 보면 A7/A8/A9가 미증명으로 오판된다.
- [OBSERVED] G-1 (A9.4 스윙당-1회 문언 vs 틱당 샘플링) — 레인/스펙/테스트 어디에도 없는 신규 발견.
- [OBSERVED] G-2 (70/40 낡은 주석) — 신규 발견.
- [OBSERVED] §5 포인트 획득 뷰 구현(GameDirector.cs:540 `firstClear ? 3 : 2`)과 §6 유물가격
  [2,4,7,11,16] 2개소(LobbyView.cs:51, GameDirector.cs:426) 값 일치 — 신규 확인.
- [OBSERVED] A8 테이블 28개 값 전수 대조, A9 상수 8종 전수 대조 — 레인 리포트에 없던 조항
  (리포트 작성 이후 개정분)의 첫 전수 감사.
- 알려진 함정 준수: `hongt-companion-autonomy-tick-order-trap.md`의 "앵커 오프바이원"은 테스트
  하네스 아티팩트이며 심 구현은 정확 — 본 감사도 A7 구현을 정확으로 판정(재보고 아님).

## 6. 사람 판단 필요 항목

| # | 항목 | 선택지 |
|---|---|---|
| H-1 | **G-1 (A9.4 문언 vs 구현).** 교차-틱에서 같은 스윙의 늦게 진입한 적이 승급 배율을 받는 현 의미론을 (a) 스펙 문언을 "스윙-틱당 1회, 해당 틱 히트 적립 전"으로 개정해 추인할지, (b) 스윙 시작 시 배율을 래치하도록 심을 수정할지. (b)는 A9.1의 고정 다이제스트(`3350/3/13/3/71.5`)를 움직일 수 있어 스펙 개정을 동반한다. FROZEN 문서라 어느 쪽이든 오퍼레이터 승인 필요. |
| H-2 | **G-2 낡은 주석**(CinderSim.cs:1873 "70%/40%") — FROZEN 파일 1줄 주석 수정 승인. |
| H-3 | **G-3/G-4 스펙 본문 후행 갱신** — §12 목록에 HackMode 개명·RosterMask·(A4/A6 소급분) 각주를 넣어 문언-코드 불일치를 소거할지. 동작 영향 없음, 순수 문서 위생. |
| H-4 | **G-5 레벨 보너스 합성식** — 스펙 §2.5에 "단리 `×(1+0.04·(lv−1))`" 1줄 명문화 권고. |
| H-5 | **AMENDMENT #6 DRAFT 승격** — 스펙이 "operator sign-off 대기"로 명시(SIM_SPEC_HACKSLASH.md:439). 구현·테스트 완료 상태이므로 동결 승격 여부는 오퍼레이터 결정. |
