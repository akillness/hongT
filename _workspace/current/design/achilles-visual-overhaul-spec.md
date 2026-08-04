# Achilles Visual Overhaul Spec — 파츠 연출·전투 임팩트·입력·UI 개편

2026-08-04 · 오케스트레이터 작성 · 아킬레우스 레퍼런스 분석(.omc/specs/deep-interview-cinder-court-dungeon-revival.md §Original Reference Direction)의 워크스페이스 반영 + 신규 요구 8건 통합(파츠 파손 / 등급 외형 / 전투 연출 / 8·16방향+조이스틱 / UI 전면 개편 / 소환수 행동·인벤토리 / 로비 보스 자세 / 6스테이지 조합 캠페인 §T).

**레인 경계 선언 (CLAUDE.md §1, AGENTS.md)**
- `Assets/Scripts/Sim/` 3파일은 전부 `// FROZEN CONTRACT`(SimTypes.cs L1, CampaignTypes.cs L1, HackTypes.cs L1). 본 문서의 View 항목은 심 무변경. **심 변경이 필요한 항목은 §S에 격리**했고, 각각 `FROZEN CONTRACT AMENDMENT #3` + `docs/SIM_SPEC_HACKSLASH.md` 개정 + 결정론 EditMode 테스트를 선행 게이트로 명시한다.
- 히트 판정은 심의 순수 2D 수학(`dx*facing ≥ -18`, CinderSim L1202/L1311)이다. **Unity 물리 콜라이더는 게임플레이에 관여하지 않는다** — "콜리더 분할" 요구는 실제로는 **렌더러/파츠 분할 연출**로 번역된다(§P). 물리 콜라이더 추가 금지.

## 아킬레우스 레퍼런스 → 원본 번역 (분석결과 요약)

인터뷰 스펙에서 확정된 번역 원칙에 이번 요구를 합류시킨다. 복제 금지 계약(캐릭터·UI 레이아웃·보스 해부 구조) 유지.

| 레퍼런스 관찰 | 원본 번역 (이 저장소) |
|---|---|
| 장비 화면: 전신 실루엣 + 큰 슬롯 + 판독 가능한 스탯 블록 | 컴팩트 장비 패널: Lantern Reaver 실루엣 + 3슬롯(무기/랜턴/클록) T0-T5 랭크 표시 + 스탯 델타 (§U2) |
| 등급별 장비 외형 차별화 | 랭크 구간별 엠버→골드 발광 틴트 + 파츠 노출 단계 (§P2, §P3) |
| 피격 부위 파손·갑옷 붕괴 연출 | 사전 저작 파손 조각 + HP 임계 파츠 오프 연출 — 런타임 프랙처 금지 (§P1) |
| 묵직한 콤보·무기 궤적·히트 임팩트 | presentation-impact-spec #1-#8 사수 + 무기 이펙트 확장 (§C) |
| 자유로운 8방향 이동감 | 조이스틱 임의각 이동(이미 존재) + 16분할 표시 요 (§M1) |

## 기준 사실 (코드 검증 완료)

| 항목 | 값 | 위치 |
|---|---|---|
| 심 이동 입력 | `SimInput.MoveX/MoveY` float −1..1, 심이 병합 벡터를 정규화 → **임의각 이미 지원** | InputAdapter L94-110, CinderSim L1013-1016 |
| 키보드 샘플링 | WASD/화살표 ±1 누산 → 대각 포함 **8방향** | InputAdapter L98-101 |
| 가상 조이스틱 | 구현 완료, D-pad 대체(catch 260×260, deadzone 0.15, 정규화) | HudView L1241-1263, VirtualJoystick |
| 조이스틱 게이트 | `Application.isMobilePlatform \|\| (touchscreen && !mouse)` — **마우스 있는 데스크톱에선 미생성** | HudView L262-265 |
| 표시 요 | `_targetYaw = facing >= 0 ? 90f : 270f` — **모델은 좌/우 2방향만 봄** | ActorView L178-181 |
| 심 Facing | `int ±1`, 전방판정 `dx*facing ≥ -18`의 권위값 (아레나 §2 수치계약) | SimTypes L38, SimConfig.FacingArcTolerance L195 |
| 장비 계약 | 3슬롯 × T0-T5 (`EquipTiers.Weapon/Lantern/Cloak`), 스냅샷 `WeaponRank/LanternRank/CloakRank`, 이벤트 `EquipDropped(1<<13)` | HackTypes L45-57, CampaignTypes L105-107 |
| 내구도 | **심에 존재하지 않음** (`Durability` 0건) — 파손 연출의 심측 근사값은 `EnemyState.Health/MaxHealth`, `BossHp/BossMaxHp/BossPhase` | 스냅샷 계약 |
| 소환수 | 항상 플레이어 −80px 추적(전투 중에도 이동 지속), 1.1s 간격 200px 최근접 타격, 피격 불가·HP 없음. 공격 시에만 타깃 facing 발행 | CinderSim.UpdateCompanion L1004-1046 |
| 소환수 모션 | View는 `attacking ? Attack : Move` 2상태만 재생 | ActorView.SyncCompanion L130-140 |
| 스킬 UI 충돌 | phone 티어에서 스킬 행·조이스틱 박스 실측 겹침 다수 — mobile-layout-spec §기준사실에 4건 확정 | mobile-layout-spec L27-35 |
| 로비 보스 | `Compose(prefab, BossSpot, 232f, 1.45f)` + `ActorAction.Show` 루프 | LobbyStaging L50-58 |
| 보스 리스킨 실측 | monarch: heightOvershoot **1.221**(mesh 1.86m vs skeleton 1.73m, scaleMode=span), `80141 vertex weights limited`, `Applied modifier was not first` 경고 2건, 25k tri로 데시메이트 | reskin/broken-court-monarch-boss.json L13-20, .log L17-19 |

---

## §P. 파츠 연출 — 파손·등급 외형·파츠 노출 (View + Blender 사전 저작)

### P1. HP 임계 파츠 파손 연출 (적/보스)
- **WHAT**: 적 HP 25%/50%/75% 임계 통과 순간 갑옷 파츠(어깨/흉갑/투구 등 사전 분할 조각)가 분리·낙하·페이드. 보스는 `BossPhase2`에서 대형 파츠 1개 강제 탈락 + 잔해 버스트.
- **WHERE**: `ActorView` — presentation-impact-spec #5의 체력 델타 캐시(`_lastHealth`)를 임계 크로싱 검출로 확장. 파손 조각은 프리팹 자식 노드(`Break_*` 네이밍)로 사전 저작.
- **HOW**:
  - **자산**: Blender headless(`tools/blender/`)에서 기존 리스킨 파이프라인에 파츠 분리 패스 추가 — 갑옷 영역을 별도 서브메시로 분할해 본에 리지드 바인딩(스키닝 아님, 파손 조각은 단일 본 추종). 원작의 절차적 영역분할 스키닝 재사용 금지(CLAUDE.md §3) — **파손 조각은 저작 시점 분할이지 런타임 프랙처가 아니다**. 캐릭터 합계 ≤25k tri 유지(조각 포함).
  - **런타임**: 임계 크로싱 프레임에 해당 `Break_*` 노드를 detach → 0.6s 포물선 낙하(뷰 로컬 시뮬, 물리 금지) + 회전 + 알파 페이드 → 풀 반환. 잔해는 기존 8-풀 버스트 재사용.
  - 내구도 개념은 심에 없으므로 **HP 비율이 파손 게이지의 유일한 진실**. "진짜 내구도"(부위별 축적 피해)가 필요해지면 §S1로 승격.
- **COST**: L (Blender 패스 M + 런타임 M) / **RISK**: 조각 분리로 실루엣 붕괴 — 파츠당 원메시 실루엣의 ≤15%로 제한, 코어 바디는 불변.

### P2. 장비 랭크별 외형 틴트 (플레이어)
- **WHAT**: `WeaponRank/LanternRank/CloakRank`(T0-T5)를 파츠별 발광 틴트로 표현: T0-T1 무발광 / T2-T3 엠버 림 / T4 골드 림 / T5 골드 + 0.8s 펄스. `EquipDropped` 픽업 순간 해당 파츠 0.4s 화이트 플래시.
- **WHERE**: `ActorView` — 신규 `SetEquipRanks(int w, int l, int c)`; `GameView.SyncViews`에서 스냅샷 랭크 전달(dirty-check). 파츠 매핑: 무기=오른손 본 서브트리, 랜턴=왼손, 클록=상체.
- **HOW**: 기존 MaterialPropertyBlock 무할당 경로(`_block`) 재사용 — `_EmissionColor`를 랭크 구간 상수로. 파츠별 렌더러 참조는 `Create()`에서 1회 캐시(`HumanBodyBones.RightHand/LeftHand` 서브트리 스캔, 실패 시 전신 폴백). 신규 셰이더·머티리얼 클론 금지.
- **COST**: M / **RISK**: 리타겟 모델의 본 조회 실패 — 전신 틴트 폴백으로 안전; URP Lit emission 켜진 머티리얼인지 확인 필요 `[TARGET]`.

### P3. 파츠 노출 연출 — 랭크 업 시 장비 파츠 등장
- **WHAT**: 랭크가 오를 때(런 중 `EquipDropped` 또는 로비 구매 반영) 해당 파츠가 시안 디졸브 0.5s로 **등장**하는 연출. 인터뷰 스펙 §Shader/VFX의 "low-cost dissolve" 계약 이행.
- **WHERE**: P2와 동일 표면. 로비는 `LobbyStaging`의 warden에도 동일 적용(장비 화면 미리보기).
- **HOW**: 디졸브는 알파 클립 프로퍼티 순차 트윈(MaterialPropertyBlock) — 전용 셰이더 없으면 틴트 램프(어두움→본색)로 다운그레이드. 파츠 메시 자체는 P1의 저작 분할 결과를 공유.
- **COST**: S (P1/P2 위에서) / **RISK**: 없음 — 순수 장식.

## §C. 전투 연출 — 스킬 VFX·모션 연계·콤보·무기 이펙트

presentation-impact-spec의 **절대 사수 8종**(#1 히트스톱, #2 셰이크, #4 킬 팝, #5 피격 플래시, #6 데미지 숫자, #7 파동 링, #9/#15 생존 신호, #18 오디오)이 임팩트 코어다. 본 문서는 중복 재제안 없이 **추가분만** 정의한다.

### C1. 콤보 단계별 무기 궤적 차별화
- **WHAT**: 스윙 트레일(#8)을 콤보 인덱스별로 차별화: 1타 얇은 엠버 / 2타 폭 1.5× / 3타(피니셔, 87dmg) 골드 + 잔광 0.3s. `ComboSwing{0.30,0.30,0.42}` 창과 동기.
- **WHERE**: `ActorView` 트레일(#8 구현 위) + `IHackSnapshot.ComboIndex` 전달.
- **HOW**: TrailRenderer width/gradient를 콤보 프레임에 상수 스왑(머티리얼 클론 금지, startWidth/colorGradient 필드만). 활성창은 `ComboActiveFrom/To` 값 재사용.
- **COST**: S / **RISK**: 없음.

### C2. 스킬 시전 모션-VFX 연계 (선행 텔레그래프)
- **WHAT**: 스킬 4종(Bolt/Pulse/Nova/Aegis) 시전 순간 이펙트가 "터지기만" 하는 현행을, **모션 예열 → 방출** 2박자로: 캐스트 이벤트 수신 프레임에 손 본 위치에서 0.12s 수렴 글로우 → 기존 버스트/링 방출. 원소색: Void 보라 / Ember 엠버 / Frost 시안.
- **WHERE**: `VfxDirector.OnEvents` — `BoltCast/PulseCast/NovaCast/WardCast` 분기 앞단에 수렴 파티클 1페어(풀 4). 손 위치는 ActorView가 노출하는 본 캐시 참조.
- **HOW**: 수렴은 파티클 2개를 손 본으로 lerp(무할당 타이머 필드). 심 판정 타이밍은 불변 — 순수 장식 선행이며 게임플레이 지연 없음(캐스트는 이미 발생한 뒤).
- **COST**: M / **RISK**: 0.12s 선행이 "이펙트 늦음"으로 오독될 수 있음 — 방출 프레임에 기존 버스트를 유지하므로 판정 시각은 동일.

### C3. 무기 히트 스파크 — 타격 접촉점 연출
- **WHAT**: 적 피격 프레임(#5 델타 검출)에 피격 적 위치에 4-6 스파크 파티클 + 지면 링 8px. 피니셔 히트는 스파크 2× + 골드.
- **WHERE**: `VfxDirector` — GameView 적 루프의 #5/#6 델타 지점에서 호출(이미 순회 중, 추가 순회 없음).
- **HOW**: 기존 8-풀 버스트의 소형 변형(스케일 0.3). 20적 노바 동시타는 프레임당 스폰 상한 6으로 클램프.
- **COST**: S / **RISK**: 과밀 — 상한 클램프로 봉인.

## §M. 이동·입력

### M1. 16분할 표시 요 — "4방향처럼 보이는" 문제의 실제 해소
- **WHAT**: 이동 중 모델 요를 이동 벡터에서 유도해 **16분할 스냅**(22.5° 단위, 720°/s 회전 유지). 정지 시 마지막 요 유지. 공격 순간은 심 `Facing`(±1) 방향으로 즉시 스냅(전방판정 시각 일치).
- **WHERE**: `ActorView.Apply` L178-181 — 요 유도원을 `facing 2값`에서 `이동 델타(프레임 간 X/Y 차) → atan2 → 22.5° 스냅`으로 교체. 플레이어·소환수·적 공통.
- **HOW**: `_prevX/_prevY` 필드로 델타 계산(스냅샷 읽기만, 심 무변경). 델타 < 엡실론이면 요 유지. `ActorAction.Attack/Critical` 동안은 `facing >= 0 ? 90 : 270` 기존 규칙 우선 — **심 전방판정(`dx*facing ≥ -18`)의 권위는 불변**이고 표시 요만 풍부해진다.
- **COST**: S / **RISK**: 대각 이동 중 공격 시 요가 좌/우로 홱 도는 체감 — 의도된 정직함(판정이 실제로 좌/우 아크). 판정 자체의 8방향화는 §S2 게이트.
- **주의**: 심은 이미 임의각 이동을 지원한다(기준 사실). 이 항목은 심 수정 0의 **View 전용**이다. `SimInput` 양자화 필드 추가 류 제안은 FROZEN CONTRACT 위반 — 금지.

### M2. 가상 조이스틱 데스크톱 노출 정책
- **WHAT**: 조이스틱은 이미 D-pad를 대체했으나 게이트(L262-265)가 마우스 보유 데스크톱에서 터치 컨트롤 전체를 숨긴다. 터치 이벤트가 실제 발생하면(하이브리드 노트북·터치 모니터) 지연 생성하도록 게이트 완화.
- **WHERE**: `HudView` — Build의 정적 게이트에 더해, Update에서 최초 `Touchscreen` 입력 감지 시 1회 `BuildTouchControls` 지연 호출.
- **HOW**: `Touchscreen.current?.primaryTouch.press.wasPressedThisFrame` 감시(프레임당 1회 null 체크, 무할당). 생성 후 감시 중단. 키보드 전용 사용자에겐 UI 불변.
- **COST**: S / **RISK**: 마우스+터치 동시 기기에서 조이스틱이 스킬 카드와 겹침 — mobile-layout-spec §4의 재배치 규칙이 선행 조건.

## §U. UI 전면 개편 — 아킬레우스 판독성 원칙

원칙(레퍼런스 번역): **전투 정보는 가장자리, 장비는 실루엣 중심, 스킬은 오버레이 없는 고정 슬롯**. 기존 cycle2-spec·mobile-layout-spec 항목은 유효하며, 아래는 그 위의 구조 개편이다.

### U1. 스킬 카드 오버레이 해소 — 고정 슬롯 바
- **WHAT**: 스킬 카드(108u, 5장 1행 589u)가 조이스틱·타격 버튼과 겹치는 구조(실측 4건)를 폐기하고, 하단 중앙 **고정 슬롯 바**(카드 72u, 아이콘+키캡+쿨다운 링만, 라벨 제거)로 축소. 카드 상세(이름·설명)는 길게 누름/호버 툴팁으로 이동.
- **WHERE**: `HudView.EnableDungeonUi`/`ApplyDungeonTier` — 카드 빌더를 슬롯 문법으로 교체. mobile-layout-spec §3의 phone 2단 배치는 이 슬롯 바 치수로 재계산.
- **HOW**: 슬롯 72u ≈ 43 CSS px(0.597 스케일) — 터치 하한 44px에 1px 미달하므로 phone 티어만 76u. 쿨다운은 기존 radial 오버레이 재사용. 조이스틱 catch(260u)와 슬롯 바 좌단 간격 ≥24u를 레이아웃 검사로 고정.
- **테스트 동반 갱신 (필수)**: `HudLayoutTests`가 레이아웃 계약을 코드로 고정하고 있다 — Phone 티어 분류·0.488 CSS px/u 스펙 크로스체크(L99-104 확인), 상호작용 rect 무겹침·44px 하한·rect 개수 하한·티어 상수 하드코딩. 카드→슬롯 교체는 이 계약에 직접 걸리므로 **테스트를 같은 PR에서 갱신**한다. 추가로 현행 `InteractiveRects()`는 `IPointerDownHandler`만 수집(L121-129 확인)하므로 **스킬 슬롯 ↔ 비상호작용 리드아웃(체력/기름/보스바/콤보 핍/웨이브 텍스트) 겹침은 미검사** — 사용자가 지적한 오버레이가 정확히 이 사각지대다. 리드아웃 rect를 포함하는 신규 무겹침 테스트를 U1 완료 정의에 포함한다.
- **COST**: M / **RISK**: 라벨 제거로 신규 유저 학습 저하 — 프롤로그 첫 시전 시 1회 슬롯 라벨 토스트로 보완.

### U2. 장비/소환수 패널 — 실루엣 중심 재설계
- **WHAT**: 인터뷰 스펙 §UI 계약("실루엣 + 아이템 + 스탯 델타 + 단일 장착 액션") 이행: 로비 SANCTUM 장비 탭을 좌 실루엣(warden 렌더 또는 정면 라인아트) + 우 3슬롯 카드(랭크 T배지, P2 틴트 미리보기) + 하단 스탯 델타 1행으로 재구성. 소환수 탭 동일 문법(소환수 실루엣 + 로스터 + 공명 상태).
- **WHERE**: `LobbyView` SANCTUM 빌더 — 기존 성장 탭 문법 위에서 재배치. `LobbyStaging` warden에 P2/P3 틴트 연동.
- **HOW**: 실루엣은 신규 아트 없이 LobbyStaging 3D 배치를 패널 컷아웃 뒤로 정렬(카메라 고정이라 위치 계산 1회). 레이아웃은 653u 포트레이트 폭에서 세로 스택 폴백.
- **COST**: M / **RISK**: 아킬레우스 장비 화면 **레이아웃 복제 금지** — 슬롯 배열·아이콘 스타일을 저장소 기존 카드 문법으로 유지, 스크린샷 대조를 검증 계약에 포함.

### U3. 소환수 인벤토리 설정 (Phase 2 게이트)
- **WHAT**: 소환수에 아이템/스킬을 장착하는 설정 UI. **현행 심에는 소환수 장비·스킬 개념이 없다** — UI만 먼저 내면 가짜 기능이 된다. §S3 심 계약이 승인되기 전까지는 **로스터 선택 + 공명(런 스코프 준비 상태) 표시**까지만 구현.
- **WHERE**: U2 소환수 탭.
- **COST**: S (표시부) / **RISK**: 없음 — 장착 기능은 §S3에 종속.

## §G. 소환수 행동 개편

### G1. View 선반영 — 전투 모션·타깃 응시 (심 무변경 한도)
- **WHAT**: 현행 View는 `attacking ? Attack : Move` 2상태(SyncCompanion). 이를 (a) 정지 상태(플레이어 근접+타깃 없음)에서 `Idle`, (b) `CompanionAttacking` 동안 `Attack` + 타깃 facing(이미 발행됨, `CompanionFacing`) 응시 유지, (c) 공격 간 쿨다운 중에도 최근접 적이 200px 내면 **응시만** 전투 방향 유지(M1 요 유도 위에서)로 확장.
- **WHERE**: `ActorView.SyncCompanion` + `GameView` L332-338 전달부.
- **HOW**: (c)의 "적 근접 여부"는 View가 이미 순회하는 적 스냅샷에서 계산(추가 순회 없음, 200px는 HackSpec.CompanionAttackRange 상수 읽기). 위치는 심 소유라 불변 — **추적 이동 자체를 멈추는 것은 G2(심) 없이는 불가능**함을 명시.
- **COST**: S / **RISK**: 위치는 계속 플레이어를 따라가므로 "몸은 따라가는데 고개는 적을 봄" — G2 전까지의 정직한 중간 상태.

### G2 → §S3. 전투 우선 행동(추적 정지·교전 위치 유지)은 심 변경 — §S로 격리.

## §L. 로비 보스 구겨짐 수정

### L1. 첫 화면 보스 배경 자세 붕괴
- **WHAT**: 로비 첫 화면의 보스(`Show` 루프, 1.45× 스케일)가 구겨져 보이는 문제 수정. 진단은 **저비용→고비용 사다리**로, 각 단계에서 원인 확정 시 조기 종료.
- **관측 사실 [OBSERVED]**:
  - `ActorAction.Show`는 저장소 전체에서 `LobbyStaging.cs:58` 한 곳에서만 재생 — **런 경로는 Show를 쓰지 않는다**. `show` 스테이트의 소스 모션은 `Mutant Roaring.fbx`로 `idle`(`Unarmed Idle.fbx`)과 **다른 소스 릭**이며, 둘 다 Humanoid 자동 아바타 리타겟.
  - 로비 `Compose`(LobbyStaging L103-106)는 런 경로(`ActorView.Create` L58-65: 빈 `Actor` 래퍼에 `SetParent(root, false)`, 스케일은 래퍼에만)와 달리 **프리팹 루트 localScale을 직접 덮어쓰고 position/rotation을 월드 공간에 세팅**한다.
  - 리스킨 리포트: monarch `heightOvershoot 1.221`(mesh 1.863m vs skeleton 1.731m, `scaleMode: span`), `80141 vertex weights limited`, `Applied modifier was not first` 2건, 41355→25000 tri.
- **진단 사다리 (순서 고정)**:
  1. **런 중 보스 스크린샷 1장** (코드 변경 0): 던전 보스가 정상이면 리스킨 FBX 가설 즉시 기각 — 동일 FBX·동일 프리팹을 쓰기 때문. [INFERENCE] 로비만 구겨진다는 보고와 정합.
  2. **`SetAction(_boss, ActorAction.Idle)` 1줄 스왑** 후 로비 확인: 해소되면 원인은 Show 클립(이종 릭 리타겟) 확정 → Show 클립을 보스 릭 호환 모션으로 교체하거나 Idle+P2 틴트/엠버 림으로 위협감 대체.
  3. **Compose 트랜스폼 경로 정렬**: 빈 래퍼 GameObject → `SetParent(wrapper, false)` → 위치/회전/스케일을 래퍼의 local*에만 적용 — 런 경로와 동일 문법으로 통일. 비균등 스케일 부모 아래 월드 rotation 세팅이 만드는 셰어(skew) 가능성 제거. [INFERENCE — LobbyStaging 상위 체인 스케일 (1,1,1) 여부 함께 확인]
  4. **최후에만 Blender 재익스포트** (`scaleMode` height 스왑 / 조인트 링 보존 데시메이트): 동일 FBX를 공유하는 **런 보스까지 바꾸는 파괴적 자산 작업** — CLAUDE.md §5 `git tag -f pre-reskin-<date>` 선행 + RUNTIME_ANIMATION_CONTRACT §3 조인트 게이트 리그 리포트 + 던전 스모크 병행.
- **검증**: 로비 스크린샷(1280×853 + 390×844) — 어깨/허리/망토 접힘 없음 + 사다리 어느 단계에서 종료했는지 기록.
- **COST**: S(사다리 1-3) ~ M(4 도달 시) / **RISK**: 4단계만 파괴적 — 1-3에서 끝나면 자산 무변경.

---

## §T. 조합 캠페인 — 6스테이지 레벨디자인 (View 레인 + 자산)

**요구**: 기존 1→2→3 선형을 유지하되 조합 스테이지 3개(1+2, 2+3, 1+3)를 사이에 끼워 `1 → 1+2(보스) → 2 → 2+3(보스) → 3 → 1+3(최종)` 6스테이지 체인으로 재구성. 체인 완주 후 Tribunal Arena(인터뷰 스펙 4실)로 진입 — 인터뷰 스펙의 캡스톤 구조는 불변.

**성립 근거 [OBSERVED]**: `HackConfig.Hazards`는 공개 필드이고 `ToCampaignConfig()`가 `if (Hazards != null) stage.Hazards = Hazards;`(HackTypes L142-145)로 스테이지 테이블을 덮어쓴다 — **기믹 조합은 심 무변경으로 성립**. 반면 웨이브 수·보스 비주얼·적 로테이션은 앵커 스테이지에 고정된다(적 비주얼은 `(_wave + _spawnIndexInWave) % VisualRotation`, 보스는 `_config.BossVisual` — CinderSim L1853-1857). 보스 프리팹은 2종뿐이고 `EnemyVisual`은 frozen enum — **조합 보스는 기존 2종의 틴트/스케일 변주로 성립**시키고 신규 enum은 §S4로 격리.

### T1. StageCatalog 리팩터 — 하드코딩 3 제거 (선행 필수)
- **WHAT**: 스테이지 지식이 View 8곳에 `3` 하드코딩으로 산재(`GameDirector.IsStageUnlocked`/`StageDisplayName`/클리어 기록 switch, `LobbyView for(i<3)`+해금 삼항+`_stageStatus[3]`, `LobbyStaging` 보스 2분기+accent switch, `GameView.BossNameFor`, `StoryCatalog` switch, `SetStageTerrain`의 `Terrain/terrain-<stageId>` 로드). 스위치를 늘리지 말고 **데이터 주도 카탈로그 1개**로 수렴: `StageCatalog = { id, displayName, simAnchorId, hazardOverride[], prereqId, terrainId, accentColor, bossVariant(tint/scale/이름), storyKey }` 배열.
- **WHERE**: 신규 `Assets/Scripts/View/StageCatalog.cs` (View asmdef — frozen 아님). `CampaignStages`(frozen)는 심 앵커 3종으로 불변 유지.
- **HOW**: 순수 스테이지 3종은 pass-through 엔트리(override=null). 조합 스테이지는 앵커 id로 `HackConfig.TryDungeon` 호출 후 `config.Hazards = catalog.hazardOverride` 주입(GameDirector). 위 8곳 소비자를 전부 카탈로그 조회로 교체.
- **COST**: M / **RISK**: 소비자 누락 — 8곳 목록을 PR 체크리스트로 사용.

### T2. CampaignStore 스키마 마이그레이션
- **WHAT**: 클리어 영속이 개별 bool 3개(`CinderSpanCleared/AbyssChancelCleared/EchoThroneCleared`) + 수기 JSON — 6스테이지 확장 불가. `ClearedMask` 비트마스크 1필드(카탈로그 순서 비트)로 승격.
- **HOW**: 레거시 로드 호환 — 구 세이브의 3 bool을 비트 0/2/4로 매핑. 해금 규칙: `unlocked(s) = cleared(prereq(s)) || cleared(s)` — 레거시 유저가 이미 깬 2·3스테이지는 직접 cleared로 인정되어 소급 잠금이 없다. 단일 라이터 경로(인터뷰 스펙 §Inter-Stage) 유지.
- **COST**: S / **RISK**: 세이브 손상 — 구버전 필드를 읽되 쓰기는 신규 필드만, 마이그레이션 EditMode 테스트 필수.

### T3. 레벨디자인 — 6스테이지 사양

좌표는 전부 frozen 배치 테이블(CampaignTypes L163-182)의 검증된 값 재사용 — 아레나 경계 내 성립이 자동 보장된다. 해저드 4개 동시는 AbyssChancel로 기존 증명. 반경 실측 [OBSERVED]: `VentRadius=90` / `PillarRadius=40` / `AltarRadius=70` / `PlayerPushRadius=26` (CampaignSpec L113-122). **통로를 실제로 막는 해저드는 기둥뿐** — 밀어내기 루프가 `Kind != ObsidianPillar → continue`(CinderSim L2163-2167). 제단은 목적지(1.2s 홀드), 벤트는 주기 타이밍 존이라 통과 가능. 따라서 통로 규칙은 기둥 쌍에만 건다(T5(b)).

**앵커 = 보상 슬롯이다 [OBSERVED]**: 캠페인 보스 처치의 확정 보상은 `RaiseRank(_config.StageIndex % EquipSlotCount)`(CinderSim L1647, `EquipSlotCount=3` CampaignTypes L127) — 앵커 StageIndex가 무기(0)/랜턴(1)/클록(2) 슬롯을 결정한다. 조합 스테이지의 앵커는 **두 부모 중 슬롯 분포가 2/2/2가 되는 쪽**으로 선택했다(해저드는 override가 이기므로 앵커는 웨이브 수·보스 비주얼·보상 슬롯 3가지만 결정): 완주 시 무기×2/랜턴×2/클록×2. 앵커를 임의로 바꾸면 이 분포가 깨진다 — 카탈로그 주석에 이 제약을 남길 것. 랭크는 런 시작 1회 적용 계약이라 획득 랭크는 **다음 런부터 반영**(L1727 주석 문법 동일) — Ember Rest/결과 화면에 "다음 강하부터" 표기.

| # | id | 앵커(심) | 웨이브+보스 | 보상 슬롯 | 해저드 구성 | 전술 의도 |
|---|---|---|---|---|---|---|
| S1 | cinder-span | cinder-span | 5+Commander | 무기 | (불변) Vent(560,480,φ0)·Vent(980,720,φ1.2) | 벤트 타이밍 학습 |
| S2 | ember-gallery (1+2) | cinder-span | 5+Commander | 무기 | Vent(560,480,φ0)·Vent(980,720,φ1.2)·Vent(1100,450,φ0.6)·Pillar(768,604) | 중앙 기둥을 등지고 3벤트 로테이션 판독 — 스팬의 "타이밍"과 챈슬의 "엄폐"가 한 판에. 최소 페어거리 241.7 |
| S3 | abyss-chancel | abyss-chancel | 6+Commander | 랜턴 | (불변) Pillar×3·Vent(1100,450,φ0.6) | 기둥 홀드 교전 |
| S4 | witness-well (2+3) | abyss-chancel | 6+Commander | 랜턴 | Altar(768,604)·Pillar(640,500)·Pillar(900,700)·Vent(1030,480,φ1.2) | 제단 축성 채널을 기둥 페어로 엄호, 동측 카이트 레인은 벤트가 봉쇄. 최소 페어거리 163.2(제단-기둥 — 반경 실측 게이트 대상) |
| S5 | echo-throne | echo-throne | 7+Monarch | 클록 | (불변) Altar·Vent(500,700,φ0)·Vent(1030,480,φ1.2) | 제단+벤트 압박 |
| S6 | ash-verdict (1+3, 최종) | echo-throne | 7+Monarch | 클록 | Altar(768,604)·Vent(560,480,φ0)·Vent(980,720,φ1.2)·Vent(1030,480,φ0.6) | 제단 채널 vs 3벤트 1/3박 로테이션 — 캠페인 전술 총결산. 최소 페어거리 242 |

웨이브 곡선 5,5,6,6,7,7 단조 증가 + 최종 2연전만 Monarch — 난이도 클라이맥스 유지.

- **보스 변주 (View, §P2 문법 재사용)**: S2 "회랑 감독관" = Commander + 엠버 림 / S4 "우물의 증인" = Commander + 바이올렛 틴트 + 스케일 1.1(S3 무변주 Commander와 구분) / S6 "판결자" = Monarch + 골드-재 틴트. 이름은 StoryCatalog 확장(카탈로그 storyKey).
- **스토리 비트 (원본, 복제 금지 계약 내)**: S2 아카이브의 잿불이 회랑으로 번지며 **집행자가 판결문을 위조하는 첫 융합 기억** 목격 → S4 증언 기둥이 우물로 가라앉고 수호자 메아리가 **위조 전 원본 증언** 회수 → S6 재와 제단이 겹친 최종 법정에서 회수한 기억 전부를 제단에 세워 **집행자의 이름을 판결문에 되새김** → Tribunal Arena 개방.
- **터레인/조명**: 신규 FBX 없이 시작 — S2 = terrain-abyss-chancel + 스팬 엠버 벤트 프롭·엠버 accent, S4 = terrain-echo-throne + 챈슬 기둥 프롭·바이올렛-시안 accent, S6 = terrain-echo-throne + 스팬 재 잔해 프롭·골드-재 accent. accent는 카탈로그 필드로 LobbyStaging switch 대체.

### T4. 자산 생성 계획 (CLAUDE.md §3 도구 고정 계약)
- **키 아트/텍스처 액센트**: 스테이지별 컨셉 1장 + 액센트 텍스처(≤1024) — `gti --dry-run` 선행 후 생성, `docs/provenance/`에 프롬프트·소스·도구 기록.
- **프롭 킷**: 벤트 링/재 잔해/기둥 파편 소품 — 정본은 `blender -b -P tools/blender/<script>.py` 배치 스크립트. 이 세션에서는 **Blender MCP로 대화형 반복** 후, 확정 파라미터를 배치 스크립트로 역기록해 재현성 유지(생성 산출물 커밋 전 `git tag -f pre-props-<date>`).
- **예산**: 캐릭터 ≤25k tri·총 빌드 ≤120 MB 불변 — 프롭 킷은 스테이지당 합계 ≤8k tri 목표 `[TARGET]`.

### T5. 검증
- EditMode 신규: (a) 카탈로그 6엔트리 무결성(앵커 존재·prereq 체인 무순환·terrainId 프리팹 존재), (b) 해저드 override 배치 — **2규칙 분리**: ① 전 종류 쌍별 비중첩 `거리 > r합` (S4 제단-기둥 163.2 > 110, S6 벤트-벤트 245.2 > 180 — 전 스테이지 통과 확인), ② **기둥 쌍만** 회피 통로 `거리 ≥ r합 + 2×PlayerPushRadius(=52)` — 상수 유도 규칙, 임의 여유값 금지. S4 기둥쌍(640,500)-(900,700) 328.0 ≥ 132 통과. 단일 규칙 "+80"은 제단/벤트에 개념 오류 + 기존 frozen 배치도 아슬아슬해 폐기, (c) T2 마이그레이션(레거시 3 bool → 마스크), (d) **보상 분포 — 카탈로그 6엔트리의 `anchorStageIndex % 3`이 정확히 {0,0,1,1,2,2}** (완주 시 무기+2/랜턴+2/클록+2 — 앵커 변경 회귀를 여기서 잡는다). 기존 `CampaignSimTests`는 frozen 앵커 불변이므로 무수정 통과가 곧 회귀 증명.
- 스모크: 6스테이지 순차 클리어 1회 통주 — 각 스테이지 진입 스크린샷 + 해금 체인 확인.

---

## §S. 심 변경 후보 (FROZEN CONTRACT AMENDMENT #3 게이트)

아래는 View로 불가능한 항목. 각각 **docs/SIM_SPEC_HACKSLASH.md 개정 + additive 계약 + 결정론 EditMode 테스트 + Digest 불변(기존 모드) 증명**이 선행되어야 착수 가능. 승인 전 구현 금지.

| ID | 내용 | 계약 스케치 | 기존 경로 영향 |
|---|---|---|---|
| S1 | 부위 내구도(진짜 파손 게이지) | `EnemyState`에 파츠 HP 배열 추가는 스냅샷 계약 변경 — 대안: 파생 스칼라(HP 임계)로 P1을 유지하고 S1은 **보류 권고** | Arena/Campaign Digest 불변 필수 |
| S2 | 전방판정 8방향화 (`Facing` 벡터화) | 던전 모드 한정 `HackSpec` 신규 상수 + 판정 분기. 아레나 `dx*facing ≥ -18` 불변 | 아레나 불변 / 던전 리플레이 Digest 갱신 |
| S3 | 소환수 전투 우선 행동 + 스킬/장비 | `UpdateCompanion` 상태기계(추적↔교전 정지↔복귀), `CompanionSkill*` 상수, `HackConfig` 장착 필드, 스냅샷 노출 확장 | 던전 한정, additive. 인벤토리 영속화는 CampaignStore 스키마 감사 별도 |
| S4 | 조합 스테이지 전용 웨이브 수·신규 보스 enum·전용 terrain id | `CampaignStages` 테이블 확장 또는 신규 스테이지 레코드 — AMENDMENT #3로 `AllIds` 6원소화 + `CampaignSimTests` 갱신 | Tier A(§T, 앵커 재사용)로 선출시 후 필요 시에만 심사 |

## 구현 우선순위 제안

1. **U1 + M1** (체감 최대·비용 소) → 2. **L1 사다리 1-3** (첫인상 결함, 저비용 진단 먼저) → 3. **P2 + P3** (등급 외형) → 4. **C1-C3** (임팩트 확장) → 5. **T1 + T2** (카탈로그·스토어 — §T 선행 게이트) → 6. **T3 + T4** (조합 스테이지 롤아웃, 자산 병행) → 7. **G1** (소환수 View 선반영) → 8. **P1** (Blender 파손 패스, 최대 비용) → 9. **M2 + U2 + U3** → 10. §S 게이트 심사(S2/S3/S4).

## 검증 계약

- EditMode 전체 유지 — 단 **U1은 `HudLayoutTests` 동반 갱신**(rect 개수 하한·티어 상수) + 비상호작용 리드아웃 무겹침 신규 테스트, §T는 T5 신규 테스트, §S 항목은 결정론 테스트 신규.
- 데스크톱 스모크: 콤보 3타 트레일 차별화·히트 스파크·파츠 파손 임계 스크린샷, 16분할 요 회전 영상 1개.
- 390×844 포트레이트: 슬롯 바-조이스틱 간격 ≥24u, 터치 타깃 ≥44 CSS px 전수.
- 로비: 보스 자세 스크린샷 2해상도 + 리그 리포트 조인트 게이트 통과.
- 아킬레우스 대조: 장비 패널 스크린샷을 레퍼런스 캡처와 나란히 놓고 **레이아웃 비복제** 확인 기록(docs/provenance/ 관례).
- 6스테이지: T5 통주 스모크 + 해금 체인 스크린샷, 레거시 세이브 마이그레이션 테스트 통과.
