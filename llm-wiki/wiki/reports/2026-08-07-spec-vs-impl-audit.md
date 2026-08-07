# 기획-구현 대조 감사 종합 (2026-08-07)

- 방식: 읽기 전용 병렬 감사 5레인(주요항목/드롭률/연출/보스·플레이어/레벨디자인).
- 상세 증거: `_workspace/current/qa/audit-20260807/audit-Audit{CoreSystems,LootDrops,Presentation,BossPlayer,LevelDesign}.md` (파일:라인 인용 전수).
- 판정 기준: `docs/SIM_SPEC*.md` + `CLAUDE.md` §2. 외부 레퍼런스
  [[wiki/sources/2026-08-07-hackslash-design-guide-reference]]는 비교축으로만 사용.
- 테스트 기반선 [OBSERVED]: `_workspace/current/engineering/unity-logs/test-results-094459.xml`
  **365/365 통과** (2026-08-07 00:45Z, A7/A8/A9 포함; 루트 `unity-logs/`의 195/195본은 08-05 구판).

## 0. 사람 판단 필요 항목 (의사결정 큐 — 병합·중복 제거)

| # | 결정 | 배경 | 출처 레인 |
|---|---|---|---|
| D1 | **보스 패턴 착수 여부 (S8-b/c)** — §7 페이즈 표의 공격간격/텔레그래프/스킬쿨 3열은 상수+테스트만 존재, 심 소비 0건. 패턴 6종(`boss-phase-metric-definition.md` §4) 미착수. FROZEN AMENDMENT 게이트 필요 | 보스 공격은 접촉 1종뿐. 텔레그래프 0.80 s 공정성 계약(§12.1 차지 0.45 s < 0.80 s)의 런타임 실체 없음 | BossPlayer G1 = Presentation G2 |
| D2 | **최종보스 고유성 정책** — ash-verdict "Gate Sovereign"은 echo-throne 보스와 심 완전 동일체(HP 5,868·호위 7·간격 1.42 s). 차이는 틴트/스케일/해저드/대사뿐. 스펙에 최종보스 조항 자체가 없음 → 스펙 결정 선행 | 6스테이지 체인 마지막 방이 재탕 보스 | BossPlayer G2 |
| D3 | **§10 보스 웨이브 아레나 클램프 15% 축소 + 1.5 s 링** 미구현 — 구현할지, 스펙에서 §W 링(0.9 s)으로 대체 개정할지 | 보스전 공간 압박(방 감각) 부재 | LevelDesign G1 (= BossPlayer G4) |
| D4 | **노바 AoE 링 250 vs 던전 판정 230** — frozen §2 L65-66이 명시 금지("원작 결함 계승 금지")인데 view-vfx-research는 수용. 뷰 수정(모드 인지 반경) vs 스펙 개정 | 문서 간 상충 | Presentation G3 |
| D5 | **캠페인 보스 relic-mote 유령 스폰** — 보스킬 tick에 스폰 직후 GameOver라 회수 구조적 불가(`CinderSim.cs:822-825` 게이트 + `2670→2677` 순서). (i) 캠페인 보스 스폰 생략 (ii) 자동 회수 +1 (iii) 현상 유지+명문화 | 던전 보스킬 유물 +1 유무가 경제(만렙 120 유물)에 직결 | LootDrops G-1 |
| D6 | **A9.4 문언 "스윙당 1회 샘플링" vs 구현 스윙-틱당** — 교차-틱에 늦게 진입한 적이 승급 배율 수령 가능. (a) 스펙 문언 개정 추인 (b) 스윙 시작 래치로 심 수정(Digest 이동 동반) | 테스트도 같은 해석 공유 — 미커버 케이스 | CoreSystems G-1 |
| D7 | **패배 시 인런 장비 랭크 폐기** — 코드 주석은 "spec §3/§6 contract"를 인용하나 스펙에 조항 없음. 명문화 or 정책 변경 | 클리어 시만 max 병합 | LootDrops G-3/J-2 |
| D8 | **보스 인트로 0.45 s vs 스펙 1.2 s** — 의도적 페이싱 단축인지 드리프트인지 개정 기록 부재 | 구성요소는 전부 구현 | Presentation G4 |
| D9 | 프롤로그 런 유물 뱅킹 제외(스펙 침묵) / 시간당 유물 효율 정체(동적 조정 미채택) — 파밍 루프 길이 의도 확인 | 런당 수입 ~3-10 vs 만렙 120 | LootDrops J-1/J-4 |
| D10 | 3룸→웨이브 그룹 재해석·체인 "(보스)" 표기 vs 전 노드 보스 종결 — 승인된 축소인지 | integrated §1.2/§3.1 vs 구현 | LevelDesign §6-1/2 |
| D11 | AMENDMENT #6 DRAFT 동결 승격 (구현·테스트 완료, operator sign-off 대기 명시) | SIM_SPEC_HACKSLASH.md:439 | CoreSystems H-5 |
| D12 | 플레이테스트 2건: §3.3 플레이타임 25–30분 실측, 후반 스테이지 체감 난이도 역전(메타 성장이 스테이지 곡선을 앞지름) | 정적 감사로 판정 불가 | LevelDesign G3/§6-3,4 |

## 1. 총평

**수치 계약은 견고하다.** 심 코어(§0–§6, §12/§12.1, A3/A4/A6/A7/A8/A9)와 드롭 경제(17개 조항)
전 조항이 파일:라인 대조로 구현 확인, **상충 0건·미구현 0건** (심 범위). 연출 임팩트 코어 8종
(#1 히트스톱 40/70 ms, #2 셰이크 티어, #5 피격 플래시, #6 데미지 숫자, #7 파동 링, #14 정예
금펄스, A9 모멘텀 HUD, #18 오디오 9/9)도 수치까지 일치. 프롤로그→캠페인 해금 체인
(심→GameDirector→CampaignStore→StageCatalog.IsUnlocked→딥링크) 전 구간 구현.

**갭은 보스전에 집중된다.** High 5건 중 3건(D1/D2/D3)이 보스 웨이브의 미완성 — §7 표의
시간 열이 장식 상수로만 존재하고, 패턴 6종은 미착수(S8-b/c), 최종보스는 5스테이지 보스의
리컬러다. 나머지 2건은 플레이어 체감 공정성 결함: H4(원소 피격색 데드 배선)는 View 1줄
수정으로 **결정 없이 즉시 착수 가능한 유일한 항목**, H5(노바 링 오버슛)는 D4 결정 대상.

## 2. High 갭 (중복 제거 후 5건)

| # | 내용 | 근거 | 수정 경로 |
|---|---|---|---|
| H1 | §7 보스 페이즈 시간 열 3종(공격간격 1.37/1.16/0.99·텔레그래프 0.80·스킬쿨 5.0/4.0/3.25) **심 소비자 0건** + 보스 패턴 6종 전무. 실제 보스 쿨 = 일반 적 공식 `1.22+min(0.38,w×0.025)`, 선딜 ≈0.167 s. **가장 중요한 함의: 게이트 테스트가 상수 산술만 검증**(`ChargeWindow_FitsInsideABossTelegraph`는 0.45<0.80 상수 비교 — 런타임과 무관하게 영원히 통과) → §12.1 공정성 계약이 **런타임 실체 없이 365/365 녹색**. "기능 부재"가 아니라 "스위트가 미구현 계약을 충족으로 보고"하는 상태 | `HackTypes.cs:758-760`(선언) vs 소비 0건(grep 전수: 참조는 HackSimTests.cs:2669,2680,2784,3004뿐); `CinderSim.cs:2469-2470,2543-2551` | S8 AMENDMENT (D1). 심 무변경 임시안: 실존 선딜 0.167 s 전방 호 표시 (Presentation §4-2) |
| H2 | 최종보스 심 동일체 — Commander/Monarch 차이는 원소 + Monarch P2 호위 3기 **2가지뿐**; ash-verdict = echo-throne 완전 동일 | `StageCatalog.cs:139-152`, `HackTypes.cs:815-816`, `CinderSim.cs:1887-1891` | 스펙 결정 선행 (D2) |
| H3 | §10 보스 웨이브 클램프 15% 축소 + 1.5 s 텔레그래프 링 미구현 (§W 0.9 s 링은 별개 스펙) | `CinderSim.cs:3295-3297`, `VfxDirector.cs:427` | 구현 or 스펙 개정 (D3) |
| H4 | **`SetElementTint` 데드 배선** — GameView가 liveTint 계산만 하고 적 뷰에 미전달 → §K3 원소 피격색 미작동. 저장소 전체 호출자 0건 | `GameView.cs:462-466`(계산) vs 469-515(호출 부재), `ActorView.cs:218` | 적 루프 1줄 배선 (View, 비frozen) — 즉시 수정 가능 |
| H5 | **노바 링 250 vs 던전 판정 230 상충** — 뷰가 `SimConfig.NovaRadius`(250)로 링을 그리고 던전 판정은 `HackSpec.AshNovaRadius`(230) → 링이 히트박스를 8.7% 오버슛. frozen §2 L65-66이 **명시 금지한 원작 결함의 계승** — 플레이어가 체감하는 공정성 결함(링 안인데 안 맞음) | `VfxDirector.cs:257-260,1156`, `SimTypes.cs:211` vs `HackTypes.cs:445` | 뷰 모드 인지 반경(캐스트 시점 값 캡처, 비용 S) or 스펙 개정 (D4) |

## 3. Med 갭 (중복 제거 후 9건)

| # | 내용 | 근거 |
|---|---|---|
| M1 | 캠페인 보스 relic-mote 유령 스폰 (회수 불가) → D5 | `CinderSim.cs:2670→2677`, 822-825 |
| M2 | §8 위반: P3 말풍선이 P2와 같은 대사 재생 (StoryCatalog에 BossPhase2 비트뿐; `sim.BossPhase` 분기로 심 무변경 수정 가능) | `StoryCatalog.cs:12`, `GameDirector.cs:615-619` |
| M3 | 적 조합 전 6노드 동일 — integrated §3.1 "노드별 적 조합" 부분 구현. visual=(wave+spawnIndex)%4 고정, CampaignConfig에 적 조합 필드 부재 | `CinderSim.cs:2896`, `CampaignTypes.cs:71-93` |
| M4 | 스테이지 곡선이 웨이브 번호에만 결박 — 메타 성장(T5 +30%·스탯 10pt·동료 3슬롯)이 앞지름 → D12 플레이테스트 | LevelDesign §4 곡선 표 |
| M5 | 보스 인트로 1.2 s → 0.45 s (구성요소는 전부 존재) → D8 | `HudView.cs:142` |
| M6 | 던전 카메라 거리 스펙 17/21 vs 코드 20/24.5 — 결정은 정당(캐릭터 축소 ×1.17), 스펙 §10 미개정 | `CameraRig.cs:36-40` |
| M7 | §2.4 matchMedia('prefers-reduced-motion') BuildScript 주입 부재 (PlayerPrefs 토글만 존재) | BuildScript grep 0건 |
| M8 | #15 심박음 루프 부재 (시각 비네트만) | `AudioDirector.cs` 전수 |
| M9 | 테스트 공백 3건: EmberShard/OilFlask 회수 효과, EquipShard `kills%3` 슬롯, 로비 구매 경제 — 드롭 경제의 화폐 축인데 행동 테스트 없음 | LootDrops T-1~T-3 |

## 4. Low / 정보성 (대표)

- 낡은 주석 2건: `CinderSim.cs:1873`(70/40 → 실코드 50/20), `HackTypes.cs:373-377`(BossPhase "1 or 2" → 실제 1/2/3). FROZEN 파일이라 수정에 승인 필요.
- `EquipCosts {2,4,7,11,16}` 이중 정의 (`LobbyView.cs:51` + `GameDirector.cs:426`) — 드리프트 위험.
- §W 링 0.6 s(설계) vs 0.9 s(구현·릴리즈 노트) 수치 표류; V3 maxParticles 96 vs 스펙 40/24/32/24(실 Emit ≤26); V4 블룸 0.55 vs 0.35 미문서; 벤트 분화 셰이크 누락; 근접 전방 호 예고 미구현; particle-additive-seed.mat 명칭 불일치(기능 등가).
- 로비 보스 show→Idle, `IHackSnapshot.Mode`→`HackMode` 개명, §12 목록 외 HackConfig 필드 3종(RosterMask/PreparationOffer/CompanionIds) — 전부 코드가 옳고 스펙 문언만 후행. achilles §T3 조합 스테이지 보스 바디/이름 3종 미채택(이월/폐기 결정 필요). guard 프리팹 미사용 예약. 보스 웨이브 호위 정예화 가능(ordinal 관통 — 의도 불명).

## 5. 외부 레퍼런스 대비 (요약)

- **채택/정합**: 모멘텀 게이지(A9), 콤보 분기(피니셔 변형+차지), 클라이맥스 스파이크(보스 총 HP 3.8~3.9×), 소수 강유닛+지원 문법(보스+호위), 학습 구간(프롤로그), 텔레그래프 공정성(벤트/§W — 보스 공격 예고만 H1로 부재), 트레일·히트스톱·드롭 보장.
- **아키텍처상 비양립 (결함 아님)**: C/R/E/L 확률 희귀도 테이블·bad-luck protection·DDA·포인트 웨이브 — 전부 §13 결정론(RNG 금지, 동일 입력→동일 Digest) 계약과 충돌. 결정적 모듈러(21-주기: ember/oil/relic 각 2/7, EquipShard 1/7)와 T0–T5 랭크가 의도적 대체. 가이드의 Unity 구현론(SO/OnTriggerEnter/Animator/PostFX 볼륨)은 CLAUDE.md §1 심 경계와 충돌 — 채택 불가.
- **스펙 차원의 실질 갭 [OBSERVED]**: ① 적 행동 타입 분화 부재(전 적 동일 근접 추적 — 레퍼런스의 "빌드 체크 포인트" 기능 부재), ② 선택형 경로/조건부 보상 부재(단선 체인은 명시적 설계), ③ 정예 픽업 차등 부재(추출 채널만), ④ 진행 기반 재화 스케일 부재.

## 6. 검증 상태

- 이월 증거(재검증됨): 레인 리포트 4종의 구현 주장 → 현재 코드 파일:라인으로 전수 재확인.
- 이월 증거(재검증 안 됨): 0-alloc 측정, PostFxGate p95 10.0 ms, 25k tri 데시메이트 로그 — 읽기 전용·배치모드 금지 제약으로 재실행 안 함.
- 함정 준수: [[wiki/concepts/hongt-companion-autonomy-tick-order-trap]]의 앵커 오프바이원은 5레인 모두 재보고하지 않음.
