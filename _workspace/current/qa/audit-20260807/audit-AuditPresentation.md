# 감사 리포트 — 핵앤슬래시 연출·게임필 (AuditPresentation)

2026-08-07 · Combat-Feel & VFX Auditor · 읽기 전용 감사.
대상: `Assets/Scripts/View/{VfxDirector,ActorView,CameraRig,PostFxGate,DamageNumberPool,GameView,HudView,AudioDirector}.cs`, `Assets/Shaders/`, `Assets/Resources/Materials/`.
판정 기준: `docs/SIM_SPEC_HACKSLASH.md`(frozen v0.2.0) + `_workspace/current/design/{integrated-combat-vfx-spec, presentation-impact-spec, combat-feel-boss-phase-spec, view-vfx-research}.md`.
외부 레퍼런스 가이드(`llm-wiki/raw/sources/2026-08-07-hackslash-design-guide-reference.md`)는 일반론 참고자료 — 판정 기준이 아님(프레이밍 가드 준수).

테스트 기반선: EditMode **365/365 PASS** [OBSERVED] `unity-logs/test-results-094459.xml` (읽기만, 재실행 안 함).

---

## 1. 스펙 조항별 대조표 (연출 요소 매트릭스)

판정: ✅구현됨(스펙 일치) / 🟡부분 / ❌미구현 / ⚠️스펙과 상충

### 1.1 SIM_SPEC_HACKSLASH.md 연출 조항

| 조항 | 스펙 값 [TARGET] | 구현 위치 [OBSERVED] | 판정 | 개선 제안 |
|---|---|---|---|---|
| §2 L65-66 AoE 링 = 심 판정 반경 동일("원작 결함 계승 금지") | 던전 ash-nova 반경 230 (HackTypes.cs:445) | 노바 링 확장 반경 `SimConfig.NovaRadius`=**250** 고정 (VfxDirector.cs:1156, SimTypes.cs:211); 스코치도 250 (VfxDirector.cs:260) | ⚠️ **상충** — 던전에서 8.7% 오버슛. 코드 주석은 "decoration" 수용(VfxDirector.cs:257-259), view-vfx-research.md도 "8% 오버슛 장식 허용"이라 적었으나 **frozen 스펙 조항이 명시 금지** | 모드 인지 반경(던전=230): `_novaRadius` 필드에 캐스트 시점 값 캡처. 비용 S, WebGL 무관. 또는 스펙 개정 — 사람 판단 필요(§5-1) |
| §2 L64-66 펄스(E) 반경 표시 | PulseRadius 190 / 3 s (HackTypes.cs:437-438) | 190*Scale 고정 링 3 s + 0.5 s 알파 펄스 (VfxDirector.cs:31,1189,1200-1204) + 스코치 fill (308-309) | ✅ | — |
| §3 L87 정예 "금색 틴트 펄스" | 펄스 (정적 아님) | `SetEliteTint` + 1.2 s PingPong(0.83f) 밝기 펄스, 피격 플래시 우선 (ActorView.cs:290,818-822; GameView.cs:478-479) | ✅ | — |
| §4 L102-104 동료 틴트 | 항상 틴트: `-echo`=청록, 보스 보상=웜골드, 스케일 0.92, 신규 메시 금지 | echo=(0.62,0.95,0.88) 시안, 보상=(1,0.86,0.55) 웜골드 (GameBootstrap.cs:96-99); 스케일 0.92 + `TintRenderers` MPB (GameView.cs:206-214) — 기존 메시 재사용 | ✅ | — |
| §8(S8-a) 보스 페이즈 연출 | P1/P2/P3, HP 50%/20% 경계 | HUD: PHASE I/II/III 핍 + P3 전용 색(1,0.24,0.55) (HudView.cs:1990-1995); VFX: BossPhase2 버스트를 살아있는 보스 좌표에서 (VfxDirector.cs:279-289); P3 진입도 BossPhase2 이벤트 재사용(CinderSim.cs:1893-1897, 스냅샷 BossPhase가 which를 답함) | ✅ | — |
| §8 표 L124-131 보스 텔레그래프 0.80 s 전 페이즈 고정 | `BossTelegraph {0.80,0.80,0.80}` + AttackInterval + SkillCooldown (HackTypes.cs:758-760) | **CinderSim이 세 배열을 소비하지 않음** — 보스 포함 전 적이 `SimConfig.EnemyAttackCooldown` 1.22 s + 웨이브 가산 (CinderSim.cs:2469-2470), 선딜 = 공용 5프레임 클립의 접촉프레임 2 @12 fps ≈ **0.167 s** (CinderSim.cs:45-49,2543-2551). 테스트는 상수 예산만 검증 (HackSimTests.cs:2669-2681) | ❌ **미구현(상수 선언만)** — 뷰가 그릴 예고 신호 자체가 부재. §12.1 "차지 0.45 s < 텔레그래프 0.80 s" 공정성 계약(L227-228)의 런타임 실체 없음 | 심 변경은 S8 게이트(AMENDMENT 필요). **심 무변경 대안**: 보스 `Action==Attack && ActionTime < 2/12s` 구간(실존 선딜 0.167 s)에 전방 호 표시 — §4.1 문법, LineRenderer만, 비용 S |
| §10 L175-176 던전 카메라 거리 2티어 | 평시 17 / 빅웨이브 21, 전환 1.5 s 지수 | 평시 **20** / 크라우드 **24.5** (CameraRig.cs:39-40) — 주석 "character-shrink decision (2026-08) 두 티어 ×1.17" | ⚠️ 스펙 미개정 — 결정은 코드 주석에만 존재 | 스펙 §10 개정 커밋 필요(§5-3) |
| §10 L177 보스 인트로 1.2 s 푸시인 + 말풍선 | 1.2 s | 레터박스+이름판+FocusPulse+말풍선 구현되나 **0.45 s** (HudView.cs:142 `BossIntroDuration=0.45f`; GameDirector.cs:612-613) | 🟡 부분 — 구성요소 전부 존재, 지속시간만 스펙의 37% | 1.2 s 복원 또는 개정 근거 문서화(§5-2) |
| §10 L180-181 보스 웨이브 아레나 클램프 15% 축소 + 텔레그래프 1.5 s 링 | 15% / 1.5 s | 심 `ClampToArena` 고정 마진 (CinderSim.cs:3294-3297); §W 링 수명 0.9 s (VfxDirector.cs:427) | ❌ / ⚠️ — **판정 소유: AuditLevelDesign** (IRC 교차 확인, 판정 동일) | — |
| §10 L182-183 VFX 추가 5종 | 대시 트레일·콤보 스파크·레벨업 버스트·추출 빔·정예 마커 | 대시: 버스트+베이크드메시 고스트 (VfxDirector.cs:271-272; ActorView.cs:552-613); 콤보 스파크 6/frame 예산 (VfxDirector.cs:540-565); 레벨업 (275-276); 추출 빔+링 (755-788); 정예 마커 (360-373) | ✅ | — |
| §10 L183 풀 상한 40 + 크리티컬 축출 면제 | 40, 면제 | 전용 풀 분리로 구현: bursts 8 + sparks 12 + warnings 4 + scorch 4 + 파티클 4시스템 — 스킬/보스 이벤트가 스파크 홍수에 축출되지 않는 구조 (VfxDirector.cs:468-477 주석) | ✅ [INFERENCE: 분리 풀 = 면제 계약의 등가 구현] | — |
| A9.4 L798-799 HUD가 "기세 x1.18" 형태로 버프 명시 | 티어 기반 라벨 | `기세 x{multiplier:0.00}` 티어 변경 시만 갱신 (HudView.cs:1192-1198); fill=momentum/MomentumMax, 티어 색 4종 (1186-1187,1204-1213) | ✅ | — |
| A9.5 L807-810 MomentumTierUp 엣지 트리거 큐 | 승격당 1회 | 티어 스케일 버스트 1회 (VfxDirector.cs:347-356) — 색은 HUD와 동일 계열 유지 주석 (489-491) | ✅ | 오디오 큐는 없음 — 임시 계약(#18) 확장 후보, 비용 S |

### 1.2 presentation-impact-spec #1-#20 (절대 사수 8종 = 임팩트 코어)

| # | 요소 | 스펙 값 | 구현 위치 | 판정 |
|---|---|---|---|---|
| #1 | 히트스톱 | 킬 40 ms·피니셔 70 ms @0.05, unscaled 감쇠, GameOver 즉시 0, EndRun/OnDisable 복구 | GameView.cs:355-365(세팅), 328-342(min-merge+MoveTowards 4/s), 373-378(GameOver), 244-253·256-262(복구), HitStopScale=0.05(:103) | ✅ 수치 전항 일치 |
| #2 | 셰이크 티어 | Kill 0.08/0.02·Finisher 0.14/0.05·BossSpawned 0.35/0.07, 우선순위 체인 | GameView.cs:393-397 `Rig.Punch`; 기존 티어 Nova 0.2/0.06·Phase2 0.3/0.09·Damaged 0.12/0.045 (CameraRig.cs:146-148) | ✅ — 우선순위는 else-if 대신 진폭 비교 non-stomping `Punch`(CameraRig.cs:329-341)로 구현: 스펙 의도(강한 것 우선) 충족 [INFERENCE] |
| #3 | 페이즈2 슬로모 | 0.5 s @0.35, 히트스톱과 min(scale) | GameView.cs:368-372, 330-342 | ✅ |
| #4 | 킬 팝 | 1.18× 펀치 0.09 s, 보스 1+0.18/scale 상쇄 | ActorView.cs:743(`_deathPop=0.09f`), 761-763 | ✅ |
| #5 | 적 피격 플래시 | 체력 델타 캐시, 0.13 s | ActorView.cs:211-218(델타), 795(0.13 s), ResetForPool 871 | ✅ (원소색 부분은 갭 G1 참조) |
| #6 | 데미지 숫자 | 풀 16·0.6 s·상승 1.2·최고령 축출·문자열 캐시·피니셔 금색 | DamageNumberPool.cs:11-16,62-87,100-125; 피니셔 플래그 GameView.cs:361 | ✅ — GameView가 리플렉션으로 풀 내부 필드 캐시(GameView.cs:635-643)해 시각별 색 주입: 동작하나 캡슐화 위반 스멜 [OBSERVED] |
| #7 | 파동 지속 링 | 3 s·190px·0.5 s 공명 펄스·View 상수 | VfxDirector.cs:31,297-310,1180-1213 | ✅ |
| #8 | 스윙 트레일 | RightHand 본, 0.18 s, 0.06→0, #f3592c, 창 0.10-0.34 | ActorView.cs:510-530(생성), 194-196(창); 플레이어 한정 GameView.cs:148; 콤보 티어별 폭/금색 확장 392-401 | ✅+확장 |
| #9 | 저체력 비네트+화살표 | HP<35 심박 펄스 + 정지 0.4 s 화살표 | HudView.cs:2653-2668(sin*7, 0.25+0.2); VfxDirector.cs:943-1037(0.4 s, 최근접, 램프) — idle을 InputAdapter 대신 심 좌표 델타로 유도(주석 938-942, 정당한 문서화된 이탈) | ✅ |
| #10 | 캐스트 스크린 플래시 | 4이벤트 원소색, 90 ms, α≤0.28 | HudView.cs:2536-2543, 감쇠 2670-2680 | ✅ |
| #11 | 콤보 핍 펀치+피니셔 골드 | 1.5→1.0 펀치, 골드 플래시 0.25-0.4 s | HudView.cs:2826-2850, 트리거 2532-2533 | ✅ |
| #12 | 보스 인트로 | 1.2 s 포커스 풀+레터박스 9% | FocusPulse 55% blend (CameraRig.cs:274-293); 레터박스 고정 90 px (HudView.cs:2743-2744) | 🟡 0.45 s(상기), 높이 9%→90 px 고정 |
| #13 | 픽업 흡인 | 0.22 s, Life>0.05 수집 판별, 용량 8 | VfxDirector.cs:1090-1104, 1217-1234 | ✅ |
| #14 | 정예 펄스 | 1.2 s 주기 | ActorView.cs:818-822 | ✅ |
| #15 | 랜턴 플리커 | Perlin 6f, <20 적색 | HudView.cs:2681-2692 | ✅ (시각) / ❌ 심박**음** 루프 — AudioDirector에 부재 (AudioDirector.cs 전수 확인, loop는 BGM뿐 :66-70) |
| #16 | 추출 연출 | 시체 링 10 s + 채널 빔 + 수축 | VfxDirector.cs:360-380, 755-788 | ✅ |
| #17 | 벤트 버스트 | CycleT 랩 + prev 시드 + **셰이크 미세 1회** | 버스트+시드 VfxDirector.cs:810-813, 866-868; **셰이크 호출 없음**(SyncHazards에 Punch 부재) | 🟡 |
| #18 | 오디오 9종 배선 | 표 9행 | AudioDirector.cs:141-153 — 9/9 클립·볼륨 전항 일치 | ✅ |
| #19 | 레벨업 세리머니 | XP 골드 플래시+Lv 펀치+토스트 1.4 s | HudView.cs:309-310(토스트), 2711+(감쇠), 이벤트 분기 | ✅ |
| #20 | 웨이브 배너 | 0.25 펀치+1.2 유지 (=1.45), 보스 적색 | HudView.cs:2521-2531(1.45/1.8+색), 2695-2709 | ✅ |

### 1.3 integrated-combat-vfx-spec §2/§4/§5/§6 + view-vfx-research

| 항목 | 스펙 | 구현 | 판정 |
|---|---|---|---|
| §2.3 시전 0.25 s 원형 텔레그래프 후 판정 | "시전 순간 0.25 s 텔레그래프" | 심은 즉발 판정(캐스트 딜레이 없음, §S7 미승인) — 뷰는 사후 링/스코치. combat-feel §K L42가 이 제약을 명문화("예고는 심 타이밍보다 앞설 수 없다") | 🟡 부분 = **설계상 한계** (S7 게이트, 결함 아님) |
| §2.3 원소색 4종 통일 | 볼트 #bf8cff·노바 #f3592c·파동 그린·에이기스 시안 | 파티클/링/플래시/캐스트글로우 전반 일관 (VfxDirector.cs:159-166, HudView.cs:2536-2543, GameView TryElementColor) | ✅ |
| §2.4 reduced-motion | matchMedia + PlayerPrefs, 파티클 50%·히트스톱 오프·셰이크 오프 | ViewPrefs(MotionScale 0.4, TimeEffectsAllowed) + 로비 토글 (LobbyView.cs:506-513); 파티클 반감 4곳 (VfxDirector.cs:267,319,654,1212); **matchMedia 주입 없음** (BuildScript.cs grep 0건) | 🟡 |
| §4.1 근접 공격 0.1 s 전 전방 호 | 전방 호 | 없음 — 근접 예고 미구현 | ❌ |
| §4.1/K3 원소 틴트 우선순위 | 플래시>원소>랭크 | ActorView.cs:218(원소 우선 플래시색), 794-822(플래시>정예>랭크글로우 체인) | ✅ 구조 / ❌ 배선 — **갭 G1** |
| §4.2 신규 모션 attack2/attack3/cast_loading/recoil/knockdown | 클립 4-5종 | attack2/attack3/cast = View 서브스테이트 11/12/13 (ActorView.cs:407,463-465; ClipTableTests.cs:56-58; PoseResolveTests) — 컨트롤러에 실재. recoil/knockdown = **미제작**, BigHit 재사용 + 속도 추론 넉백 창 (ActorView.cs:219-235,414-424) | 🟡 — ActorAction FROZEN 하 최대치 [INFERENCE: 의도적] |
| §4.3 전환 컷신 3 s 카메라 궤도 + Profile.Cutscene | 카메라 궤도 | `CameraRig.Profile`에 Cutscene 없음 (CameraRig.cs:11); 대신 CutsceneView = 로딩스크린 오버레이(페이드 0.35/유지 2.6/아웃 0.5, unscaled) (CutsceneView.cs:19-22) | 🟡 대체 구현 — 사람 판단(§5-4) |
| §5.1 V1 캐스트 글로우 0.12 s | 수렴 글로우 | FlashCastGlow 0.12 s, 0.16→0.055 수렴, RightHand (ActorView.cs:650-697; 호출 GameView.cs:423-428) — 스펙 "양손" 대비 우측 1점 | ✅(경미 축소) |
| §5.2 V2 벤트 fill | 임박도 fill | FillDisc 성장 0..radius (VfxDirector.cs:833-842,884-891) | ✅ |
| §5.3 V3 파티클 4종 + maxParticles 40/24/32/24 | 상한 4종별 | 4시스템 Emit-only 구현 (159-166); **maxParticles 전부 96** (VfxDirector.cs:184) | ⚠️ 상한 상충 — 실사용 Emit 수(≤26)는 예산 내라 실해 없음 [INFERENCE] |
| §5.4 particle-additive-seed.mat 시드 | 전용 시드 | 파일 부재 — `MakeAdditive`가 검증된 unlit-transparent-seed 클론에 블렌드 팩터만 변경 (ViewWorld.cs:71-76; AdditiveMaterialTests가 변형 생존 고정) | 🟡 명칭 상충·계약 목적(스트리핑 생존)은 충족 [INFERENCE: 의도적 대체] |
| §5.5 V4 블룸 0.35+비네트, p95 16.7 게이트 | 강도 0.35 | 씬 볼륨 bloom **0.55**/threshold 1.05 (SceneBuilder.cs:219-222); PostFxGate 모바일 오프 (PostFxGate.cs:15-22); p95 10.0 ms 실측 기록 (PostFxGate.cs:1-6) | 🟡 강도 드리프트(0.35→0.55) 미문서 |
| research 적용 5종 | 스코치·볼트 스트릭·노바 에코·에이기스 링·콘솔 슬로모 | SpawnScorch(570-593)·FireBoltStreak(614-656)·에코(256)·WardCast 버스트(312-314)·콘솔 0.2 ReducedMotion 밖 (GameView.cs:315-326) | ✅ 5/5 |
| §W 웨이브 링 | 스폰포인트 수축 링, 보스 적색 | SpawnWaveWarnings + StepWarningPool + 전용 풀 4 (VfxDirector.cs:398-457,477); 결정론 매핑 테스트 (WaveTelegraphTests) | ✅ (수명 0.9 vs §10 1.5 s는 AuditLevelDesign 소유) |

---

## 2. 갭/상충 목록

| ID | 심각도 | 내용 | 근거 |
|---|---|---|---|
| **G1** | **High** | **`SetElementTint` 데드 배선**: GameView가 `liveTint`를 계산(GameView.cs:465-466)하고 주석은 "hand the live color to every enemy"라 하나, 적 루프(469-515)에서 `view.SetElementTint(liveTint)` 호출이 **없음**. 저장소 전체에 호출자 0건 [OBSERVED grep]. 결과: 적 피격 플래시는 항상 기본 엠버(EnemyFlashColor) — §K3 "스킬 피격 메시 원소색 0.4 s" 미작동. ElementTintTests는 색 매핑 순수함수만 검증, 배선은 미검증 | GameView.cs:462-466, ActorView.cs:218,245 |
| **G2** | **High** | **보스 텔레그래프 0.80 s의 런타임 실체 부재**: `HackSpec.BossTelegraph/BossAttackInterval/BossSkillCooldown` 미소비(§1.1 표). 실제 보스 선딜 ≈0.167 s(공용 클립). 스펙 §8 난이도 서사와 §12.1 차지 공정성 계약(L227-228)이 상수+테스트로만 존재. 뷰 차원 보스 공격 예고 연출도 그릴 신호가 없어 부재 | CinderSim.cs:2464-2551, HackTypes.cs:758-760, HackSimTests.cs:2669-2681 |
| G3 | Med | 노바 AoE 링 250 vs 던전 판정 230 — frozen 스펙 §2 L65-66 명시 금지 조항과 상충 (연구 문서와 스펙이 서로 충돌) | VfxDirector.cs:257-260,1156 |
| G4 | Med | 보스 인트로 1.2 s → 0.45 s 축소, 개정 문서 없음; 레터박스 화면 9% → 고정 90 px | HudView.cs:142, GameDirector.cs:613 |
| G5 | Med | 던전 카메라 거리 17/21 → 20/24.5 — 결정 자체는 정당해 보이나 스펙 §10 미개정(코드 주석만) | CameraRig.cs:36-40 |
| G6 | Med | §2.4 matchMedia('prefers-reduced-motion') BuildScript 주입 부재 — 접근성 자동 감지 미완 | BuildScript.cs grep 0건 |
| G7 | Med | #15 심박**음** 루프 부재(시각 비네트만) — 저체력 오디오 신호 무 | AudioDirector.cs 전수 |
| G8 | Low | V3 maxParticles 96 vs 스펙 40/24/32/24 (실 Emit ≤26이라 실해 없음) | VfxDirector.cs:184 |
| G9 | Low | V4 블룸 강도 0.55 vs 스펙 0.35 — 미문서 드리프트 | SceneBuilder.cs:220 |
| G10 | Low | #17 벤트 분화 "셰이크 미세 1회" 누락(버스트만) | VfxDirector.cs:805-813 |
| G11 | Low | §4.1 근접 전방 호 예고 미구현 | — |
| G12 | Low | §5.4 particle-additive-seed.mat 명칭 불일치(기능 등가 대체) | ViewWorld.cs:71-76 |
| (외부 소유) | — | 아레나 클램프 15% 축소 미구현 / §W 링 0.9 s vs 1.5 s — **AuditLevelDesign 소유**, 교차 확인 판정 동일 | CinderSim.cs:3294-3297, VfxDirector.cs:427 |

## 3. 외부 레퍼런스 가이드 대비 (일반론 — 판정 기준 아님)

| 체크리스트 | 판정 | 비고 |
|---|---|---|
| 무기 TrailRenderer(그라디언트/폭/페이드) [9] | **있음** | ActorView.cs:510-530 — 가이드 문법 그대로(폭 0.06→0, 엠버 그라디언트, 0.18 s) |
| 히트스톱 1~3프레임 [8][5][4] | **있음** | 40/70 ms = 60 fps 기준 2.4/4.2프레임, 완전 정지 대신 0.05 스케일 — 가이드 범위 상회는 의도적(지수 복귀+reduced-motion 게이트) [INFERENCE] |
| 피격 리액션(넉백/히트스턴/공중) [3][8] | **부분** | 넉백 ✅(심 120px/0.18 s + 뷰 속도 추론 BigHit 포즈, ActorView.cs:229-235). 히트스턴 ❌ — [INFERENCE] **의도적 미채택**: 피격 인터럽트는 콤보 판정을 바꾸는 심 계약(§S6 게이트, combat-feel L79). 공중 추락 ❌ — 동일 게이트 |
| 필살기 스크린 포스트프로세싱 온오프 [5] | **부분** | 상시 bloom+vignette(V4) + HUD 풀스크린 틴트 플래시(#10)로 대체. 캐스트 순간 포스트 파라미터 펄스는 없음 — [INFERENCE] 의도적(p95 게이트+절제 원칙). 가이드의 PostProcessingVolume 온오프 문법 자체는 CLAUDE.md §1 심 경계와 무관한 View 영역이므로 "수정 제안"이 아닌 View-only 개선 후보로만 기재(§4 제안 3) |
| 텔레그래프 공정성(지면 프로젝션) [10] | **부분** | 벤트 fill(V2)·웨이브 링(§W)·스킬 사후 링/스코치 ✅. **보스 공격 예고 ❌** — 근본 원인은 G2(심 신호 부재), 뷰 결함 아님 |
| 히트 이펙트 본 위치 파티클 + 타입별 프리셋 [5] | **부분** | 적 위치 스파크 링(§C3)+원소 파티클 4종(V3) — 무기 본이 아닌 피격자 좌표(2.5D 탑다운에서 등가). 타입 구분은 원소색 4종 설계 존재하나 적 메시 쪽은 G1로 미작동 |
| "공격할수록 강해진다" 게이지 [3] | **있음** | A9 모멘텀 = 가이드 개념의 프로젝트 고유 구현(HUD+버스트+심 계약). 가이드의 "게이지→필살기 해금" 형태 대신 근접 배율 티어 — [INFERENCE] 의도적 설계 분기 |
| 데이터 드리븐 콤보(ScriptableObject) [4][7] | 해당 없음 | CLAUDE.md §1 순수 C# 결정론 심 경계와 충돌하는 유니티 구현 관점 — 수치는 HackSpec 컴파일타임 상수로 동일 목적 달성. 결함 아님 |

## 4. 개선 제안 (WebGL 제약 준수: compute 금지·텍스처≤1024 위반 없음)

1. **G1 원소 틴트 배선 (S)**: 적 루프에서 `view.SetElementTint(liveTint)` 1줄 — 기존 MPB 무할당 경로 재사용, 신규 셰이더/텍스처 0. 가이드 [5] "피격 타입별 이펙트" 취지의 이미 설계된 기능 완성.
2. **G2 보스 선딜 가시화 (S, 심 무변경)**: 보스 `Action==Attack && ActionTime<0.167s` 구간에 전방 호 LineRenderer(§4.1 문법) — 실존 선딜을 정직하게 표시. 0.80 s 계약의 진짜 이행은 S8 AMENDMENT 필요(사람 판단).
3. **필살기 포스트 펄스 (S, 선택)**: 씬 Volume이 이미 빌드에 있으므로(V4) NovaCast/피니셔 순간 `Volume.weight` 0.3 s 펄스는 신규 셰이더 변형 0, 가이드 [5] 문법 — ReducedMotion·p95 게이트 하 채택 검토.
4. **G6 matchMedia 주입 (S)**: BuildScript PolishIndexHtml에 프래그먼트 1줄 + PlayerPrefs 브리지 — §2.4 원문 이행.
5. **G7 심박음 (S)**: 기존 클립 저볼륨 루프 변주(#18 임시 계약 문법) 또는 ElevenLabs 배치로 이월.

## 5. 사람 판단 필요 항목

1. **노바 링 250 vs 230 (G3)**: frozen 스펙 §2는 금지, view-vfx-research는 수용 — 두 문서 상충. 뷰 수정(모드 인지 반경) vs 스펙 개정 중 결정 필요.
2. **보스 인트로 0.45 s (G4)**: 의도적 페이싱 단축인지 드리프트인지 — 개정 기록 부재.
3. **카메라 거리 20/24.5 (G5)**: 결정은 합리적으로 보이나 스펙 §10 개정 커밋이 필요.
4. **컷신 대체 구현**: §4.3 "3 s 카메라 궤도" vs 현행 로딩스크린 오버레이 — 자산 게이트(§7 키아트) 대기 중인 의도적 축소로 추정 [INFERENCE], 확정 필요.
5. **G2 근본 해결**: BossTelegraph 소비는 심 상태기계 확장(S8) — AMENDMENT 승인 절차 대상.

## 6. 이월 증거 vs 신규 증거

- **이월(재검증됨)**: presentation-impact-spec "이미 존재하는 것" 표의 전 항목 — 셰이크 3티어(현행 CameraRig.cs:146-148, 라인 이동), 킷 버스트/노바 링/워드 셸(VfxDirector), 사망 페이드(ActorView.cs:760-764), 카메라 군중 티어(값은 G5로 변경됨), 벤트 텔레그래프 점멸(VfxDirector.cs:815-828). jeo-view-lane.md의 기본 배선 계약(코드 생성 이펙트, 무할당, no-op 가드) 전부 현행 코드에서 유지 확인.
- **이월(재검증 불필요)**: PostFxGate p95 10.0 ms 실측(주석 기록, 이번 감사에서 재측정 안 함 — 빌드 프로파일 필요), EditMode 365/365(결과 파일 읽기만).
- **신규 발견**: G1(SetElementTint 데드 배선), G2(BossTelegraph 미소비의 연출 공정성 함의), G4(0.45 s), G8(maxParticles 96), G9(블룸 0.55), G10(벤트 셰이크 누락), G12(시드 명칭). 함정 가드 준수: Amendment #7 앵커 오프바이원은 본 감사 범위 외이며 재보고하지 않음.

## 7. 에셋 실사 (glob 결과)

- **셰이더**: 커스텀 1종 `Assets/Shaders/UI-Icon-Glow.shader` (UI 전용). 월드 VFX는 전부 URP Unlit + `Assets/Resources/Materials/unlit-transparent-seed.mat` 클론(WebGL 변형 스트리핑 생존 계약, RuntimeMaterialSeeds.Seed → BuildScript.cs:35). 지면 프로젝션/데칼 셰이더 없음 — 스코치는 쿼드+언릿(WebGL 안전한 선택) [OBSERVED].
- **머티리얼**: Resources/Materials 6(글로우 5+시드 1), Resources/Props 장비 6, Art/Characters 8, Art/Terrain 20, Art 기타 2. `particle-additive-seed.mat` 부재(G12).
