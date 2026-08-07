# 감사 리포트 — 플레이어/보스 오브젝트 · 스테이지 보스 다양화 (AuditBossPlayer)

- 대상: `docs/SIM_SPEC_HACKSLASH.md` §7/§2.4/§4, `docs/SIM_SPEC_CAMPAIGN.md`, `docs/SIM_SPEC.md` §Bosses, CLAUDE.md §3 캐릭터 계약
- 구현: `Assets/Scripts/Sim/CinderSim.cs`, `HackTypes.cs`, `CampaignTypes.cs`, `CharacterRoster.cs`, `Assets/Scripts/View/StageCatalog.cs`, `GameBootstrap.cs`, `GameView.cs`, `LobbyStaging.cs`, `ActorView.cs`, `HudView.cs`, `GameDirector.cs`, `StoryCatalog.cs`, `Assets/Resources/Characters/*`
- 판정 기준은 프로젝트 계약(스펙+CLAUDE.md+설계문서)이며, 외부 레퍼런스 가이드는 일반론 참고자료로만 대조한다(§3).

---

## 1. 스펙 조항별 대조표

### 1.1 스테이지 × 보스 매트릭스 (수용 기준 산출물)

6개 논리 스테이지는 심 앵커 3종에 매핑된다 [OBSERVED] `StageCatalog.cs:109-154`, `CampaignTypes.cs:232-249`. 모든 캠페인 런은 `GameDirector.StartDungeon` → `HackConfig.TryDungeon` → `GameMode.Dungeon`이므로 [OBSERVED] `GameDirector.cs:277`, 6스테이지 전부 3페이즈 규칙이 적용된다.

| # | 스테이지 | 심 앵커(웨이브) | 보스 id (HudName) | 원소 | 보스 HP | 호위 | 접촉공격 간격 | 비주얼(메시/틴트/스케일) | 페이즈 구현 | 판정 |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | cinder-span | cinder-span (5+1) | shadow-commander-boss ("Cinder Warden") | veil | 5,076 | 3 | 1.370s | commander FBX / 로즈레드 (0.9,0.3,0.45) / ×1.00 | 3페이즈 (50/20%) | 구현됨 |
| 1 | ember-gallery | cinder-span (5+1) | shadow-commander-boss ("Cinder Warden") | veil | 5,076 | 3 | 1.370s | commander FBX / 엠버오렌지 (0.95,0.45,0.16) / ×1.08 | 〃 | 구현됨(심 동일체) |
| 2 | abyss-chancel | abyss-chancel (6+1) | shadow-commander-boss ("Veil Tactician") | veil | 5,472 | 5 | 1.395s | commander FBX / 바이올렛 (0.56,0.40,1) / ×1.10 | 〃 | 구현됨 |
| 3 | witness-well | abyss-chancel (6+1) | shadow-commander-boss ("Veil Tactician") | veil | 5,472 | 5 | 1.395s | commander FBX / 아이스블루 (0.45,0.78,1) / ×1.12 | 〃 | 구현됨(심 동일체) |
| 4 | echo-throne | echo-throne (7+1) | broken-court-monarch-boss ("Gate Sovereign") | void | 5,868 | 7 | 1.420s | monarch FBX / 퍼플 (0.75,0.3,0.9) / ×1.15 | 〃 + P2 호위 3기 소환 | 구현됨 |
| 5 | **ash-verdict (최종)** | echo-throne (7+1) | broken-court-monarch-boss ("Gate Sovereign") | void | 5,868 | 7 | 1.420s | monarch FBX / 골드 (0.87,0.78,0.41) / ×1.18 | 〃 + P2 호위 3기 소환 | 구현됨(심에서 #4와 완전 동일) |

산출 근거 [OBSERVED]:
- HP = `(86 + min(140, W×11)) × BossHealthMul(6) × DungeonBossHealthMul(6)` — `CinderSim.cs:2858-2883`, `HackTypes.cs:412-414,527`, `SimTypes.cs:227`. W=5→141×36=5,076 / W=6→152×36=5,472 / W=7→163×36=5,868.
- 접촉공격 간격 = `1.22 + min(0.38, wave×0.025)` — `CinderSim.cs:2469-2470,25-26`, `SimTypes.cs:199` (보스 웨이브 = W+1 대입). **페이즈와 무관하게 고정** (→ 갭 G1).
- 호위 = `min(8, 3 + stageIndex×2)` — `CinderSim.cs:620-624,630-636`, 스펙 `SIM_SPEC_CAMPAIGN.md:28` 일치.
- 원소 = `HackTypes.cs:815-816` (BossCommander=Veil, BossMonarch=Void), 스펙 §2.4(`SIM_SPEC_HACKSLASH.md:73-74`) **일치**. 스킬만 상성 적용 — `CinderSim.cs:1158-1160`.
- 비주얼 = `StageCatalog.cs:114-150` (BossPresentation: Visual/ResourceId/Tint/Scale/HudName), 인게임 적용 `GameView.cs:495-497,721-744` (틴트 MPB + `localScale ×= stage.Boss.Scale`), 로비 적용 `LobbyStaging.cs:51-70`. 심 스케일 1.6(`SimTypes.cs:227`, `CinderSim.cs:2907`) 위에 카탈로그 배율이 곱해져 월드 스케일 1.6~1.888.
- 메시 2종은 별도 FBX [OBSERVED]: `shadow-commander-boss.prefab` → guid `6cd5bba2...` = `Assets/Art/Characters/shadow-commander-boss.fbx`; `broken-court-monarch-boss.prefab` → guid `dd5db15c...` = `broken-court-monarch-boss.fbx`.

### 1.2 §7 보스전 개편 (AMENDMENT #4, 3페이즈)

| 조항 | 스펙 값 [TARGET] | 구현 위치 | 판정 |
|---|---|---|---|
| 페이즈 경계 HP 50%/20% | §7 표 | `HackSpec.BossPhase2/3HealthFraction` `HackTypes.cs:753-754`; `BossPhaseIndexFor` `HackTypes.cs:775-780`; `UpdateBossPhase` `CinderSim.cs:1850-1902` (경계 래치, 힐 재발화 방지) | **구현됨** |
| 이속 ×1.00/1.25/1.45 | §7 표 | `HackSpec.BossSpeedMul` `HackTypes.cs:765`; 소비 `CinderSim.cs:2624-2625` (기저 ×0.7 위) | **구현됨** |
| 범위 ×1.00/1.10/1.20 | §7 표 | `HackSpec.BossRangeMul` `HackTypes.cs:766`; 소비 `CinderSim.cs:2559-2563` | **구현됨** |
| 접촉 ×1.00/1.25/1.45 | §7 표 | `BossPhase2/3DamageMul` `HackTypes.cs:769-770`; 소비 `CinderSim.cs:2577-2585` (기저 ×2 위) | **구현됨** |
| **공격간격 1.37/1.16/0.99s** | §7 표 | `HackSpec.BossAttackInterval` `HackTypes.cs:758` — **심 소비자 0건** [OBSERVED] grep: 소비는 `Assets/Tests/EditMode`뿐. 실제 보스 쿨은 일반 적 공식 `CinderSim.cs:2469-2470` | **미구현** (→ G1) |
| **텔레그래프 0.80s 고정** | §7 표 | `HackSpec.BossTelegraph` `HackTypes.cs:759` — 심 소비자 0건. 심의 보스 공격에 텔레그래프 상태 자체가 없음(접촉 판정은 `EnemyContactFrame=2` 클립 프레임 지연뿐, `CinderSim.cs:49,2551`) | **미구현** (→ G1) |
| **스킬쿨 5.00/4.00/3.25s** | §7 표 | `HackSpec.BossSkillCooldown` `HackTypes.cs:760` — 심 소비자 0건. **보스 스킬이 존재하지 않음** | **미구현** (→ G1) |
| Monarch P2 호위 3기 1회 | §7 | `MonarchPhase2Escorts=3` `HackTypes.cs:771`; `CinderSim.cs:1887-1891` (`_pendingSpawns += 3`, Visual==BossMonarch 분기) | **구현됨** |
| `BossPhase2` 이벤트 모든 경계 발화 | §7 | `CinderSim.cs:1883-1897` (P2·P3 경계 각 1회, 같은 플래그) | **구현됨** (의도적 단일 플래그, 스펙 명기) |
| 스냅샷 `BossPhase` 1/2/3 | §7/§12 | `CinderSim.cs:1901,529`; 클리어 후 마지막 페이즈 유지 | **구현됨** — 단 `HackTypes.cs:374` doc 주석이 "then 1 or 2"로 낡음 (→ G9) |
| `DungeonBossHealthMul = 6` (던전 한정) | §7 B-1 | `HackTypes.cs:527`; `CinderSim.cs:2874-2883`; 게이트 `_dungeon` = `UpdateBossPhase` 게이트(`CinderSim.cs:814-817`)와 동일 | **구현됨** |
| 보스 HP 바 + 페이즈 핍 PHASE I/II/III + 페이즈별 색 | §7 | `HudView.cs:1548-1550,1740-1744,1986-1994` | **구현됨** |
| 보스 처치 → 장비 드롭 + 동료 해금 + StageCleared | §7 | 드롭 `CinderSim.cs:2673-2677` (`RaiseRank(StageIndex%3)`), 해금 `GameDirector.cs:537-560`, `ClearStage` `CinderSim.cs:3140-3141` | **구현됨** |
| P3 슬램 넉백 (모션 뎁스, 스펙 외 additive) | 설계문서 | `CinderSim.cs:2588-2599`, `HackSpec.BossSlamKnockback*` `HackTypes.cs:493-494` | 구현됨 (P3 전용 차별화 +1) |

### 1.3 §4 동료 해금 / §2.4 원소

| 조항 | 스펙 값 | 구현 | 판정 |
|---|---|---|---|
| cinder-span→ember-cohort, abyss-chancel→shade-echo, echo-throne→possessed-echo | §4 `SIM_SPEC_HACKSLASH.md:96-97` | `StageCatalog.cs:116,130,144` (CompanionReward; 조합 스테이지 1/3/5는 null — "Only base stages retain" `GameDirector.cs:552-555`) | **구현됨** |
| 첫 처치 보상 +1pt | §5 | `GameDirector.cs:540` (`firstClear ? 3 : 2`) | **구현됨** |
| 동료 틴트: -echo=청록, 보스보상=웜골드, 스케일 0.92 | §4 | `GameBootstrap.CompanionVisual` `GameBootstrap.cs:88-100`; 스케일 0.92 `GameView.cs:209` | **구현됨** — shade-echo/possessed-echo는 보스 보상이면서 `-echo` 접미라 청록으로 해석됨(스펙 자체 중의성; 코드는 접미사 우선) [INFERENCE] |
| BossCommander=veil, BossMonarch=void | §2.4 | `HackTypes.cs:815-816` | **구현됨** |

### 1.4 SIM_SPEC.md §Bosses (아레나, frozen)

| 조항 | 구현 | 판정 |
|---|---|---|
| 웨이브 5의 배수 보스 1기 | `IsBossWave` `CinderSim.cs:611`, `SimConfig.BossEveryWaves=5` `SimTypes.cs:226` | 구현됨 |
| wave%10==5 commander / %10==0 monarch | `CinderSim.cs:2892-2895` (`BossVisualPeriod=10` `CinderSim.cs:53`) | 구현됨 |
| HP×6 접촉×2 속도×0.7 스케일×1.6 점수 1000×wave relic-mote 고정 | `SimTypes.cs:227`, `CinderSim.cs:2570,2624,2907,2667,2703-2706` (`BossKillScorePerWave=1000` L43) | 구현됨 |
| run은 보스 전용 예약 | `CinderSim.cs:2521-2522` | 구현됨 |

### 1.5 CLAUDE.md §3 캐릭터 계약 + 플레이어 오브젝트 체크리스트 (수용 기준 산출물)

| 항목 | 증거 | 판정 |
|---|---|---|
| 메시 | 플레이어 = `Assets/Art/Characters/lantern-reaver.fbx` (guid `3ad9b1f...`) → `Assets/Resources/Characters/lantern-reaver.prefab`; 로드 `GameBootstrap.cs:22` | ✅ |
| 스켈레톤/아바타 | FBX `animationType: 3`(Humanoid) [OBSERVED meta]; 아바타 valid+isHuman+RightHand 매핑을 로스터 8종 전원에 대해 테스트가 고정 — `CharacterRosterAnimationTests.cs:13-42` | ✅ (mixamo 휴머노이드 재바인딩 계약 충족) |
| 액션 11종 | `ActorAction` enum 11종 `SimTypes.cs:12` == 컨트롤러 상태(`CinderActor.controller`: idle move run hit bighit attack critical avoid defence die show + 뷰 전용 attack2/attack3/cast) == 클립 테이블 인덱스 — `ClipTableTests.cs:55-58,88-110`; 소스 모션 FBX 15종 `Assets/Art/Motion/` (Unarmed Idle/Walking/Running/Punching/Hook Punch/Illegal Elbow Punch/Standing 2H Magic.../Mutant Roaring/Dying 등) | ✅ (11종 + 뷰 전용 3종) |
| 컨트롤러 공유 | 전 프리팹 동일 guid `f7c49ca6...` = `Assets/Art/Motion/CinderActor.controller` [OBSERVED prefab diff]; 파라미터 `action`(int) `SetInteger` 경로 `ActorView.cs:779` | ✅ |
| 입력 매핑 | Arena: Q=Nova E=Ward R=Restart Space=Attack; Dungeon: Q=Bolt E=Pulse R=Nova F=Ward Shift=Dash Space=Combo(hold latch) G/H/V=동료 1/2/3=성장; WASD/화살표 이동 — `InputAdapter.cs:5-8,64-87,145-161` | ✅ 스펙 §2 키 배치 일치 |
| 콤보 뷰 단계화 | `SetComboTier` → attack/attack2/attack3 (11/12/13) `ActorView.cs:50,392-435`, 호출 `GameView.cs:456` | ✅ |
| 플레이어 전용 장식 | 스윙 트레일 `GameView.cs:148`, 대시 잔상 `ActorView.cs:552-558`, 캐스트 글로우 `ActorView.cs:656-696`, 장비 프롭 소켓(RightHand/LeftHand/Chest, basic/fine 2밴드) `ActorView.cs:314-356` + `Assets/Resources/Props/` 6종 | ✅ |
| 폴백 | 프리팹 없으면 캡슐 프리미티브 (`ActorView.Create` `ActorView.cs:97-100`), 경고 로그 `GameBootstrap.cs:78` | ✅ |
| 수치 | HP 100 / 이속 218 / 공격 58 `SimTypes.cs:184-188` = CLAUDE.md §2 | ✅ |
| 로스터 | `CharacterRoster.cs:8-18` 8종 = `Assets/Resources/Characters/*.prefab` 8종 전부 존재 [OBSERVED glob] | ✅ — 단 `guard`는 런타임 로드 경로 없음 (→ G8) |

---

## 2. 갭/상충 목록 (심각도)

### G1 — **High** · 보스 페이즈 표의 시간 열 3종(공격간격/텔레그래프/스킬쿨) 심 미구현, 패턴 6종 전무
[OBSERVED] `HackSpec.BossAttackInterval/BossTelegraph/BossSkillCooldown`(`HackTypes.cs:758-760`)의 소비자는 EditMode 테스트뿐(`HackSimTests.cs:2664-2682,3004`; 단조성·상수성 검증만). 심의 보스 공격 주기는 일반 적과 같은 `1.22 + min(0.38, wave×0.025)`(`CinderSim.cs:2469-2470`) — **페이즈가 올라도 공격이 빨라지지 않는다**. 보스 스킬·텔레그래프 상태기계는 존재하지 않고, 공격 수단은 접촉 1종뿐(설계문서 `boss-phase-metric-definition.md:107` 자체 인정: "보스는 현재 일반 적과 동일한 접촉 공격 하나뿐"). §7 표는 페이즈 계약으로 제시되나 실제 구현 범위는 HP경계/이속/범위/접촉피해/호위소환/P3슬램. 패턴 6종(집중공격·지면돌출·광역·구체소환·소환·공중폭격, `metric-definition.md §4`)은 실행 순서 §7의 S8-b/S8-c로 **명시적 미착수** [OBSERVED] 심 grep 패턴 키워드 0건. 단 P1 간격 1.37s는 cinder-span 보스 웨이브(w=6)의 현행 공식값과 일치하도록 역산된 값(`metric-definition.md:61`)이라 P1 한정 수치는 우연히 일치 — abyss(1.395)/echo(1.42)는 그마저 불일치 [OBSERVED 공식 대입].

### G2 — **High** · 보스 간 기전 차이 최소: 최종보스가 5번째 스테이지 보스와 심 레벨 완전 동일
[OBSERVED] Commander vs Monarch의 심 차이는 (a) 원소 veil/void(`HackTypes.cs:815-816`) — 플레이어 스킬 상성 ±에만 작용, (b) Monarch P2 호위 3기(`CinderSim.cs:1887-1891`) **2가지뿐**. HP/이속/접촉/범위/페이즈 규칙 전부 웨이브 수 함수의 동일 공식. 최종보스 ash-verdict "Gate Sovereign"은 echo-throne과 같은 앵커(echo-throne, W=7)라 HP 5,868·호위 7·간격 1.42s까지 **완전 동일체**이고, 차이는 틴트(퍼플→골드)·스케일(1.15→1.18)·해저드 배치·스토리 대사뿐(`StageCatalog.cs:139-152`). "최종보스 고유 기전"은 스펙에도 없으므로 **스펙 자체의 갭**이지 구현 결함이 아님 — 그러나 6스테이지 체인의 마지막 방이 재탕 보스라는 다양화 부족은 실재.

### G3 — **Med** · §8 P3 말풍선 계약 위반: "P2와 다른 대사"가 없음
[TARGET] `SIM_SPEC_HACKSLASH.md:154` "보스 20% (P3) | 최후 경고 (P2와 다른 대사)". [OBSERVED] `StoryCatalog.cs`에는 `BossPhase2` 비트만 존재(L12, 스테이지별 1줄), `GameDirector.cs:615-619`는 `SimEvents.BossPhase2`가 P2·P3 **양쪽 경계에서 발화**하므로 같은 대사를 두 번 출력. P3 전용 대사·분기 없음. 뷰(비frozen)에서 `sim.BossPhase` 스냅샷으로 분기 가능 — 심 무변경 수정 경로 존재 [INFERENCE].

### G4 — **Med** · §10 보스 웨이브 아레나 클램프 15% 축소 + 텔레그래프 1.5s 링 미구현
[TARGET] `SIM_SPEC_HACKSLASH.md:180-181`. [OBSERVED] `ClampToArena`는 고정 반경(`CinderSim.cs:3295-3300`, `SimTypes.cs:182`)이며 보스 웨이브 분기 없음. 뷰에도 15%/0.85 축소 검색 0건. (웨이브 스폰 경고 링 0.6s는 §W로 별개 구현 — `VfxDirector.cs:432` StepWarningPool.)

### G5 — **Med** · 설계문서(achilles §T3)의 조합 스테이지 보스 바디 3종 미채택 — 6스테이지가 메시 2종/이름 3종 재사용
[TARGET] `achilles-visual-overhaul-spec.md` §T3: S2="회랑 감독관"(s1-cinder-warden GLB), S4="우물의 증인"(s2-veil-tactician), S6="판결자"(s3-gate-sovereign) — retained GLB 3종을 View 프리팹 매핑으로 채택 계획. [OBSERVED] `StageCatalog.cs`의 ResourceId는 `shadow-commander-boss`/`broken-court-monarch-boss` 2종뿐이고 HudName도 앵커당 1개 재사용(Cinder Warden×2, Veil Tactician×2, Gate Sovereign×2) — §T3의 조합 스테이지 전용 이름 3종("회랑 감독관"/"우물의 증인"/"판결자")과 불일치. 워크스페이스 설계문서와의 갭이며 frozen 스펙 위반은 아님. 현재 보스 비주얼 차별화는 6-틴트 + 6-스케일 + 스토리 대사(스테이지별 고유, `StoryCatalog.cs` 6 storyKey)로 성립.

### G6 — **Low** · 로비 보스 'show' 루프 → Idle 의도적 이탈 (스펙 §9 vs 코드)
[TARGET] `SIM_SPEC_HACKSLASH.md:161-162` "해당 보스 'show' 루프". [OBSERVED] `LobbyStaging.cs:57-59` `SetAction(_boss, ActorAction.Idle)` + 주석 "Show's source motion is a foreign rig… Idle keeps the lobby silhouette clean". achilles §L1 진단 사다리 2단계(이종 릭 리타겟이 구겨짐 원인)의 조치 [OBSERVED `achilles-visual-overhaul-spec.md:155`]. 코드가 옳고 스펙 §9가 미개정 — 스펙 문장 갱신 필요.

### G7 — **Low** · 던전 카메라 거리 스펙 17/21 vs 코드 20/24.5
[OBSERVED] `CameraRig.cs:36-40` — 주석에 "character-shrink decision (2026-08, camera-distance-only) scales both tiers ×1.17" 근거 명시. 의도적 개정이나 `SIM_SPEC_HACKSLASH.md:175-176` 미갱신.

### G8 — **Low** · `guard` 프리팹: 로스터/에셋 존재, 런타임 로드 경로 없음
[OBSERVED] `CharacterRoster.cs:10`에 있고 프리팹·FBX 존재, 테스트도 통과 대상이지만 `GameBootstrap`은 6 visual+player만 로드(L22-28), `EnemyVisual`에 guard 없음. `CompanionVisual`(L95)이 `Characters/{baseId}`를 로드하므로 guard-echo 류 동료가 생기면 쓰일 잠재 경로만 존재. 미사용 예약 자산 — 결함 아님 [INFERENCE].

### G9 — **Low** · `IHackSnapshot.BossPhase` doc 주석 낡음
[OBSERVED] `HackTypes.cs:373-377` "then 1 or 2" — 실제 1/2/3(`CinderSim.cs:1901`). 3페이즈 개정 시 주석 미갱신.

---

## 3. 외부 레퍼런스 가이드 대비 갭 (`llm-wiki/raw/sources/2026-08-07-hackslash-design-guide-reference.md`)

프레이밍: 가이드는 일반론 참고자료. 판정 기준은 프로젝트 계약. 가이드의 Unity 구현 관점(AttackData SO, OnTriggerEnter, Animator 상태머신 등)은 CLAUDE.md §1 순수 C# 결정론 심 경계와 충돌하므로 수정 제안 대상이 아니다.

| 가이드 항목 | 프로젝트 상태 | 분류 |
|---|---|---|
| 클라이맥스(보스) 웨이브 = 소수 강력 유닛 + 지원 유닛 | 보스 1기 + 호위 3/5/7기로 구현 (`CinderSim.cs:620-636`) | [OBSERVED] 정합 |
| 포인트 기반 웨이브 생성 / DDA(동적 난이도) | 고정 공식 웨이브(`WaveSpawnBase + wave×…`), 적응 없음 | [INFERENCE] 의도적 미채택 — §13 결정론 계약(RNG·적응 금지)과 정면 충돌하므로 결함 아님 |
| Common/Rare/Epic/Legendary 드롭 등급 테이블 | 없음 — `EquipTiers` T0-T5 랭크 + 결정적 모듈러 드롭(`enemyId%7==3`, 보스 확정 1drop)이 대체 (`HackTypes.cs`, `CinderSim.cs:2694-2708`) | [INFERENCE] 의도적 설계 분기 |
| bad-luck protection (미획득 슬롯 가중) | 없음 — 슬롯은 `stageIndex%3`/`킬수%3` 순환으로 결정적 분배 | [INFERENCE] 의도적 미채택 (결정론) |
| 전투방–루팅–챌린지–보스 리듬 | Ember Rest 방 체인(6스테이지 + 오퍼)으로 구현 | [OBSERVED] 정합 |
| 보스별 고유 패턴·기믹 (가이드 일반론) | 패턴 없음 — **스펙 자체의 갭**(G1/G2와 동일 지점). 가이드가 아니라 자체 설계문서(`metric-definition.md §4`)가 이미 6종을 계획하고 미착수 | [OBSERVED] 스펙 갭 |

---

## 4. 이월 증거 vs 신규 증거

**이월 (재검증됨):**
- `_workspace/current/engineering/gjc-hackslash-lane-report.md:133-137` — "§7 보스 페이즈 2 (HP≤50% 1회)": **당시(S3) 기준 사실이며 현행 코드는 3페이즈로 개정됨** — 현재 `CinderSim.cs:1850-1902`에서 재검증. Monarch 호위 +3은 그대로 유효.
- `jeo-view-lane.md:18-33` — 프리팹 로딩/캡슐 폴백/스케일 1.6/Animator SetInteger 문법: 현행 `GameBootstrap.cs`/`ActorView.cs`에서 전부 재확인.
- `achilles-visual-overhaul-spec.md:49` — monarch 리스킨 25k tri 데시메이트 실측(reskin 로그): **재검증 안 됨** (FBX 바이너리 직접 검증 불가, 로그 인용만).
- 테스트 결과 [읽기 전용]: `_workspace/current/engineering/unity-logs/test-results-094459.xml` (2026-08-07 00:45Z) **365/365 Passed** — `BossPhases_FireOnceEachAtTheirThresholds`, `EveryBossPhase_LastsLongEnoughToRead`, `BossPhaseTimeBudget_DecreasesMonotonically`, `CharacterRoster_ActorsRenderAndAnimateSharedAttack`, `LanternReaverPrefab_*` 포함. (루트 `unity-logs/test-results-175105.xml` 195/195는 08-05 구판 — 3페이즈 테스트 이전.)

**신규 (이번 감사에서 직접 관측):**
- §7 시간 열 3종 상수의 심 소비자 부재 (grep 전수).
- ash-verdict=echo-throne 심 동일체 판정 (StageCatalog 앵커 + CampaignTypes Build 대조).
- P3 말풍선 동일 대사 재생 경로 (`GameDirector.cs:615-619` + StoryCatalog 비트 부재).
- 아레나 클램프 15% 축소 부재 (심·뷰 전수 검색).
- 프리팹→FBX guid 매핑 8종 전수 (메시 2종 별도 확인).
- 보스 HP/간격/호위 수치 산출 (공식 대입).

**알려진 함정 확인:** `llm-wiki/wiki/hongt-companion-autonomy-tick-order-trap.md`의 Amendment #7 앵커 오프바이원은 본 감사 범위(보스/플레이어)와 무관하며 재보고하지 않음.

---

## 5. 사람 판단 필요 항목

1. **G1/G2 착수 여부** — S8-b/S8-c 패턴 6종과 페이즈별 공격간격은 FROZEN AMENDMENT 게이트(스펙 개정 + 결정론 테스트 + Digest 불변 증명)가 필요한 심 변경. 착수 우선순위와 자산(보스 스킬 모션·VFX 6종) 예산 결정은 오퍼레이터 몫.
2. **최종보스 고유성 정책** — ash-verdict에 전용 기전(예: 고유 패턴 1종, 전용 페이즈 규칙, 웨이브 수 차별)을 줄지, 현행 "비주얼+해저드+스토리 차별"로 충분한지. 스펙에 최종보스 조항 자체가 없어 스펙 결정이 선행.
3. **G3 수정 방식** — P3 전용 대사는 StoryCatalog(비frozen 뷰)에 비트 추가 + GameDirector에서 `BossPhase` 스냅샷 분기로 심 무변경 가능해 보이나, §8 대사 원문(원작 이식 계약)의 저작 판단 필요.
4. **G5** — achilles §T3의 GLB 3종 채택(조합 스테이지 보스 바디/이름 차별화)을 이월할지 폐기할지. 폐기 시 설계문서 갱신 필요.
5. **G6/G7 스펙 문구 개정** — 코드가 옳은 두 건(§9 show→Idle, §10 카메라 17/21)의 스펙 반영.
6. **25k tri 게이트** — monarch 리스킨 로그의 `80141 vertex weights limited` 경고 2건이 잔존 리스크인지 자산 파이프라인 담당 확인 필요 (FBX 바이너리라 본 감사에서 미검증).
