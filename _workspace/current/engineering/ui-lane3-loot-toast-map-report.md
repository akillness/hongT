# ui-lane3 — 획득 팝업 + 지도/이정표 중복 해소

Lane: ui-lane3 · 2026-08-08 · branch `akillness/main` (커밋 없음)
검증 제약: **Unity 미실행** (레인 규칙). 아래 `[OBSERVED]`는 대역외 컴파일과
순수 C# 실행으로 실제 확인한 것, `[INFERENCE]`는 uGUI 앵커 산술로 계산한 값,
`[TARGET]`은 오케스트레이터의 Unity 실행이 판정할 항목이다.

---

## 0. 대역외 검증 (실행한 명령과 결과)

`[OBSERVED]` 세 어셈블리 모두 컴파일 클린. Unity 6000.5.6f1의 asmdef 참조
집합 + .NET 8 ref pack으로 Roslyn 직접 호출:

```
dotnet /usr/local/share/dotnet/sdk/8.0.301/Roslyn/bincore/csc.dll \
  -nostdlib -target:library -langversion:9.0 @refs.rsp ...
=== CinderCourt.Sim ===            (에러 0)
=== CinderCourt.View ===           (에러 0)
=== CinderCourt.Tests.EditMode === (에러 0)
```

스크립트: `<scratchpad>/check.sh`.

`[OBSERVED]` **LootToastQueue의 규칙 9건 전부 실제 실행하여 통과.**
큐는 순수 C#이고 Sim 어셈블리는 UnityEngine을 참조하지 않으므로(저장소 계약
§1), 둘 다 순수 .NET으로 컴파일해 `LootToastQueueTests.cs`의 단언을
1:1로 옮긴 하네스를 돌렸다:

```
bash <scratchpad>/runqueue.sh
→ ALL QUEUE RULES PASS
```

하네스: `<scratchpad>/QueueCheck.cs`. 즉 EditMode 픽스처가 Unity에서 통과할
것이라는 근거는 "작성했다"가 아니라 "같은 단언을 돌려봤다"이다.

`[OBSERVED]` **신규 한글 글자 0개.** 폰트 재생성 불필요.

```
python3 (fontTools) — Assets/Scripts/View/*.cs 전체 한글 harvest vs
                       Assets/Resources/Fonts/HudKorean.otf cmap
→ required: 467  missing: 0
git HEAD 대비 추가된 한글 문자: 0  (467 → 467)
```

추가한 문자열은 `잿불 파편 / 랜턴 기름 / 유물 / 장비 파편 / 정교한 / 전설의`
와 주석이며, 모든 음절이 이미 View 소스와 서브셋 폰트에 존재한다.
`tools/gen_hud_font.sh` 재실행 **불필요**.

---

## 1. 과제 2 — "우측 이정표" 실측 (1단계)

### 1.1 로비 데스크톱 좌표 (1280×720 레퍼런스, 화면 좌상단 원점)

`[INFERENCE]` `LobbyView.ApplyLobbyTier` / `BuildMapPanel` / `BuildSortiePanel`의
앵커 값에서 계산:

| 패널 | x | y | 소스 |
|---|---|---|---|
| SANCTUM | 16 … 416 | 72 … 632 | `LobbyView.cs:707-711` |
| **심연 지도 (중앙)** | **432 … 856** | **72 … 392** | `LobbyView.cs:730-734` |
| ↳ 지도 필드 | 440 … 848 | 118 … 268 | `LobbyView.cs:274` |
| ↳ ProgressLine | 450 … 838 | 122 … 138 | `CampaignMapView.cs:79` |
| **SORTIE (우측)** | **872 … 1264** | 72 … 692 | `LobbyView.cs:701-705` |
| ↳ 스테이지 카드 상태칩 ×9 | 1140 … 1240 | 카드 상단 +8 … +26 | `LobbyView.cs:977`(`AnchorTopRight(-12,-8)`) |

`[OBSERVED]` 사용자 표현 "우측 이정표"는 두 해석이 가능하고 **둘 다 같은
위젯 계열을 가리킨다**: (a) 우측 SORTIE 패널 전체, (b) 각 카드의 우측 정렬
상태 라벨(`_stageStatus[i]`, `AnchorTopRight`). 어느 쪽이든 대상은
`_stageStatus`다. 이 모호성은 해소하지 않고 명시한다.

### 1.2 중복 실측 표

| 정보 | 지도(중앙)에서의 표현 | SORTIE 카드(우측)에서의 표현 | 판정 |
|---|---|---|---|
| 스테이지 클리어 여부 | 노드 알파 1.0 + 코어 채움 (`CampaignMapView.cs:186-191`) | `"정화 완료"` 텍스트 (`LobbyView.cs:321`) | **중복** — 단 카드 쪽은 "재강하 가능한 클리어본"이라는 추가 의미가 있음 |
| 강하 가능 여부 | 노드 알파 0.55 + ProgressLine `"다음 <제목>"` (`CampaignMapLayout.cs:180`) | `"강하 가능"` 텍스트 + **바로 옆의 활성 `강하` 버튼** | **완전 중복** (3중) |
| 잠김 여부 | 노드 알파 0.16 + 라벨 `"???"` (`CampaignMapLayout.cs:77,116`) | `"잠김"` 텍스트 + 카드 알파 0.45 + 비활성 버튼 | **완전 중복** (4중) |
| 스테이지 제목 | 노드 라벨 | Eyebrow 제목 (`LobbyView.cs:951`) | 중복이나 지도는 좌표, 카드는 목록 — 유지 |
| Epithet | 메타 지도 탭만 (`showEpithets:true`) | 클리어 시 서브라벨 (`LobbyView.cs:332`) | 컴팩트 지도엔 자리 없음 — 유지 |
| 진행도 `정화 N/9` | ProgressLine | (9장 카드에 암묵적) | 지도 단독 — OK |

로비 외 지점도 훑었고 **던전 HUD에는 지도/이정표 중복이 없다**:
방 목표 칩은 상단 **중앙** `(0,-108)`(`HudView.cs:1947`)이고, 우측 상단 스탯
블록은 웨이브/점수/유물/적/난이도로 경로 정보가 아니다. 메타 화면 지도 탭은
같은 `CampaignMapView` 위젯 하나를 재사용하므로 자기 자신과 중복되지 않는다.

---

## 2. 과제 2 — 해소 (2단계)

지시는 "빼거나 중복되지 않게"이고 기본안은 "중복 정보를 지도로 통합, 우측
요소는 제거/축소". 적용한 분할:

- **지도가 갖는다**: 어떤 구역이 존재하는가, 무엇이 아직 감춰졌는가(`???`),
  다음이 어디인가(`다음 X` + 프론티어 마커), 전체 진행도(`정화 N/9`).
- **카드가 갖는다**: 행동(`강하` 버튼의 활성/비활성)과 재강하 표식(`정화 완료`).

즉 **어떤 단어도 두 곳에 동시에 나오지 않는다.**

### 2.1 제거한 것과 정보 손실 감사

| 제거 | 그 정보는 어디서 읽히는가 | 손실 |
|---|---|---|
| `"강하 가능"` | 같은 카드의 **활성 `강하` 버튼**(문구·상호작용 모두), 지도 ProgressLine `"다음 <제목>"` | 없음 |
| `"잠김"` | 지도 노드 라벨 **`"???"`**(텍스트), 비활성 버튼, 카드 알파 0.45 | 없음 — 단 §2.2 전제 |

`"정화 완료"`는 남겼다. 클리어 카드도 재강하용 `강하` 버튼이 활성이라
카드 안에서 클리어를 구별하는 다른 요소가 없고, 컴팩트 지도에는 epithet을
띄울 자리가 없다.

신선 세이브에서 우측 상태칩은 **9개 → 0개**, cinder-span 클리어 후 **1개**.

접근성: 키보드/터치 경로 무변화(버튼 자체를 건드리지 않음). 색상 단독 신호
없음 — 잠김은 `???` 텍스트, 프론티어는 `다음 X` 텍스트 + 라벨 색이 함께 간다.

### 2.2 지도 개선 (실측 근거 있는 2건)

**M1 — 잠김 라벨 가독성 (`CampaignMapView.cs:LabelAlphaLocked`)**

`"잠김"`을 뺀 순간 지도의 `"???"`가 **잠김을 말로 진술하는 유일한 곳**이
됐는데, 기존 코드는 라벨을 노드와 같은 0.16 알파로 그렸다.

`[INFERENCE]` 대비비 계산 (Ink `#EBF0FF` over Charcoal `#0B0C13`):

| | 알파 | 합성색 | 상대휘도 | 대비비 |
|---|---|---|---|---|
| 이전 | 0.16 | (0.183,0.190,0.223) | 0.0347 | **1.56 : 1** |
| 현재 | 0.62 | (0.577,0.590,0.633) | 0.301 | **≈ 6.5 : 1** |

노드 **마크**의 리빌 사다리(1.0 / 0.55 / 0.16)는 그대로 두고 **라벨만** 자체
바닥값(잠김 0.62 / 강하가능 0.85 / 정화완료 1.0)을 쓴다. 테스트가 두 축을
따로 단언한다(`AlphaAt` vs `LabelAlphaAt`).

**M2 — 정적 프론티어 마커 (`CampaignMapView.cs:FrontierRingPad`)**

`[OBSERVED]` 기존 "현재 위치" 신호는 `Tick()`의 크기 펄스뿐이고, 그 함수는
`if (ViewPrefs.ReducedMotion) return;`(`CampaignMapView.cs:219`)로 **모션 약함
사용자에게는 아예 아무 강조도 남지 않았다.** 링은 애니메이션이 아니라
배치이므로 pref와 무관하게 유지된다:

- 프론티어 노드 위치에 45° 회전 다이아 아웃라인(마크 크기 +10 u, 스트로크 3 u),
  Gold. 링은 링크와 노드 **사이** 형제 순서에 만들어 노드가 항상 위에 그려진다.
- 프론티어 노드 **라벨을 Gold 풀알파**로 — 색만이 유일한 단서가 되지 않도록
  형태(링)와 색(라벨)을 같이 준다.
- 프론티어는 구조상 단일(`CampaignMapLayout.FrontierIndex`는 인덱스 하나 또는
  -1)이라 링은 인스턴스 1개를 재앵커한다.

**보류(측정만 함)**: 미점등 링크 대비. Cyan α0.18 over Charcoal = **1.34 : 1**
로 3:1 바닥값 미달이다. 다만 "미점등 = 의도적으로 물러난 장식"이 리빌 문법의
핵심이고, 3:1을 맞추려면 α≈0.55가 필요해 강하가능 노드만큼 밝아진다. 수치만
남기고 손대지 않았다 — 디자인 판단 필요 항목.

---

## 3. 과제 1 — 아이템 획득 팝업

### 3.1 사양 결함 하나를 먼저 보고한다

`[OBSERVED]` **브리프가 지시한 `_sim.LastLootGrade`는 "방금 주운 것"의 등급이
아니다.** `CinderSim.cs:3254`에서 `_lastLootGrade`는 `SpawnPickup` 안, 즉
**드랍 시점**에 기록된다. `CollectPickup`(`:3300`)은 등급을 인자로 받지만
이 필드를 갱신하지 않는다. 따라서 Epic이 떨어진 직후 Basic 파편을 주우면
팝업이 금색으로 뜬다 — 등급 차별화라는 기능의 목적 자체가 무너진다.
`SimEvents.PickupCollected`도 종류·개수를 담지 않는다.

Sim은 수정 금지이므로 **뷰 쪽에서 발행된 픽업 리스트를 Id로 틱마다 diff**한다
(`GameView.ReconcilePickups`). `CinderSim.UpdatePickups`의 제거 경로는 자석
수거와 수명 만료 **둘뿐**이므로, 사라진 Id는:

- 직전 틱의 `Life > SimConfig.FixedStep` → 만료 불가 ⇒ **수거됨** (정확)
- `Life <= FixedStep` → 모호 ⇒ 위치로 판정. 픽업은 이동하지 않으므로
  `PickupMagnetRadius(78) + 8` 슬랙(1틱 이동 218 u/s × 1/60 = 3.6 u 커버)으로
  아이소 거리 비교.

`LastLootGrade`는 사용하지 않았다. 배열 6개(`SimConfig.EnemyCap`=20)로
1회 할당, 이후 프레임 할당 0.

### 3.2 배치 — 좌측 세로 중앙 (실측 근거)

`[INFERENCE]` uGUI 앵커 산술을 스크립트로 재현해 캔버스 로컬 좌표(원점 중앙)로
계산. 토스트: anchor/pivot `(0, 0.5)`, x 16, 행 간격 40 u, 230×34 u, 4행.

**Full 1280×720 (터치 활성 최악):**

| 표면 | x | y |
|---|---|---|
| **토스트 열** | **-624 … -378** | **-71 … +83** |
| 체력/기름 미터 | -624 … -324 | 270 … 344 |
| 실드 스트립 | -624 … -434 | 196 … 224 |
| 장비 스트립 | -624 … -384 | 228 … 262 |
| **조이스틱 캐치박스** | -640 … -380 | **-360 … -100** |
| 방 목표 칩 | -260 … 260 | 226 … 252 |
| 스킬 행(최좌) | -152 … -56 | -222 … -146 |
| 대시 카드 | -256 … -160 | -222 … -146 |
| 타격 버튼 | 506 … 616 | -210 … -100 |

가장 좁은 여유는 **토스트 최하단 -71 vs 조이스틱 상단 -100 = 29.0 u**
(이 캔버스에서 29 CSS px). 그 외 모든 표면은 세로로 100 u 이상, 또는 가로로
중앙/우측에 있어 교차하지 않는다.

**Phone 799×1729:** 토스트 열 x -383.5 … -137.4, y -71 … +83.
조이스틱 상단 -604.5(여유 533 u), 장비 스트립 하단 616.5(여유 533 u),
방 목표 칩 730.5(여유 647 u), 스킬 행 -552.5(여유 481 u).

**4행이 상한인 이유**: 5행째는 y -94까지 내려가 Full 티어에서 조이스틱
캐치박스(-100)에 6 u 침범한다. `LootToastQueue.Capacity = 4`의 근거다.

`[TARGET]` 이 배치는 `HudLayoutTests.LootToastColumn_CoversNoCombatSurfaceAtPhoneTier`
가 Unity에서 실측 판정한다(4행 전부 + 방 목표 칩 활성 + 모든 인터랙티브 rect).

### 3.3 등급 표현 (다크판타지 3색 팔레트 준수)

| 등급 | 접두어 | 색 | 기존 토큰 |
|---|---|---|---|
| Basic | (없음) | `#D1DBF2` HUD ink | 차분, 흔한 경우에 단어를 붙이지 않아 희귀 등급의 대비를 지킴 |
| Fine | `정교한` | `#2BAED6` cyan | `StageClearColor`와 동일 |
| Epic | `전설의` | `#FFD473` gold | 유물/프론티어 토큰과 동일, 추가로 **스케일 1.07** |

- 각 행 선두에 4 u 등급 핍(색 막대) — 텍스트 색만이 단서가 되지 않도록.
- Epic 스케일은 **정적 크기 차이**라 모션 약함에서도 유지된다.
- 문구: `"{등급} {품목}"`, 누적 시 `" x{n}"` (ASCII `x` — 서브셋 폰트에 없는
  `×`(U+00D7)를 피함).

### 3.4 큐 규칙 (`LootToastQueue`, 순수 C#)

- 최신이 index 0, 4행 상한, 초과 시 **가장 오래된 것부터** 밀려남.
- **동일 (종류, 등급) 연속 획득은 최상단 행에 누적**하고 나이를 리셋한다.
  자석으로 파편 4개를 쓸어담았을 때 다른 3행을 밀어내지 않기 위함.
  등급이 다르면 별도 행 — Epic이 Basic 스택에 숨는 일 방지.
- 나이는 인덱스에 대해 단조 비감소(삽입은 항상 0번)이므로 만료는 항상 꼬리
  절단이고, **행이 위젯 밑에서 재정렬될 수 없다.**
- 타이밍 0.12 s 램프 / 1.5 s 유지 / 0.5 s 페이드.
  **모션 약함(`Instant`)**: 램프·페이드 없이 즉시 표시/소멸, 총 지속 동일.
- `Revision`은 텍스트가 바뀔 수 있을 때만 증가 → HudView는 페이드 중 색만
  쓰고 문자열을 재조립하지 않는다(**프레임당 할당 0**).
- `ResetRunUi()`에서 `Clear()` — 재도전이 이전 런의 마지막 획득 위에서 열리지 않음.

---

## 4. 과제 3 — 획득음 배선

`[OBSERVED]` asset-lane3 산출물 3종 모두 존재:
`Assets/Resources/Audio/cue-loot-fine.mp3`, `cue-loot-epic.mp3`, `cue-toast.mp3`.

- `AudioDirector.PlayLootCue(LootGrade)` — Epic→`cue-loot-epic`,
  Fine→`cue-loot-fine`, Basic→`cue-pickup`. 등급 큐가 없으면 `cue-pickup`으로
  떨어져 자산 부재 빌드도 획득이 들린다(`PlayClick`/`PlayFootstep`과 동일 계약).
- `AudioDirector.PlayToastCue()` — `cue-toast` at **0.45** 볼륨(획득음과 같은
  프레임에 겹치므로 아래에 깔림). 자산 부재 시 무음.
- **이중 재생 제거**: `AudioDirector.OnEvents`에서 `PickupCollected`와
  `EquipDropped` 두 줄을 **삭제**했다. 장비 파편은 한 틱에 두 플래그를 모두
  올리므로 기존 코드는 아이템 1개에 `cue-pickup`을 2번 울리고 있었다.
  이제 `GameView.ReconcilePickups`가 픽업 1개당 정확히 1회 `PlayLootCue`를 부른다.
- **토스트 훅음은 틱당 1회**. 파편 4개 자석 수거는 도착 1회이지 4회가 아니다.
- 지상에 파편 없이 랭크가 오르는 경우(보스 보상 `EquipDropped`)만
  `Equip`/`Fine` 토스트 + 큐를 자체 발행한다.

---

## 5. 변경 파일

### 신규
| 경로 | 내용 |
|---|---|
| `Assets/Scripts/View/LootToastQueue.cs` | 순수 C# 토스트 큐 모델 (`LootToastKind`, `LootToastSlot`, `LootToastQueue`) |
| `Assets/Tests/EditMode/LootToastQueueTests.cs` | EditMode 9건 |
| (+ 두 파일의 `.meta`) | 신규 GUID |

### 수정
| 경로:라인 | 변경 |
|---|---|
| `Assets/Scripts/View/HudView.cs:162-206` | 토스트 필드/상수/문자열·색 테이블 |
| `Assets/Scripts/View/HudView.cs:374` | `Build()`에서 `BuildLootToasts(root)` |
| `Assets/Scripts/View/HudView.cs:408-410` | `Update()`에서 `SyncLootToasts(Time.unscaledDeltaTime)` |
| `Assets/Scripts/View/HudView.cs:419-543` | `BuildLootToasts`(424) / `PushLootToast`(470) / `SyncLootToasts`(480) / 테스트 시임 4종(517-543) |
| `Assets/Scripts/View/HudView.cs:1207-1211` | `ResetRunUi()`에서 큐 Clear |
| `Assets/Scripts/View/AudioDirector.cs:42-45` | `_lootFine`/`_lootEpic`/`_toast` 필드 |
| `Assets/Scripts/View/AudioDirector.cs:72-74` | 3종 로드 |
| `Assets/Scripts/View/AudioDirector.cs:109-134` | `PlayLootCue` / `PlayToastCue` |
| `Assets/Scripts/View/AudioDirector.cs:207-210, 221-223` | `OnEvents`의 `PickupCollected`·`EquipDropped` 재생 제거 |
| `Assets/Scripts/View/GameView.cs:120-144` | 픽업 추적 배열 6종(134-139) + `PickupCollectSlack` |
| `Assets/Scripts/View/GameView.cs:370-372` | `EndRun()`에서 추적 리셋 |
| `Assets/Scripts/View/GameView.cs:473-477` | `DispatchEvents` 진입부에서 `ReconcilePickups(events)` |
| `Assets/Scripts/View/GameView.cs:585-657` | `ReconcilePickups`(595) / `WasCollected`(648) |
| `Assets/Scripts/View/LobbyView.cs:295-303` | 테스트 시임 `StageStatusReadout` / `CompactMap` |
| `Assets/Scripts/View/LobbyView.cs:331-346` | 상태칩 중복 제거 (`강하 가능`·`잠김` 삭제) |
| `Assets/Scripts/View/LobbyView.cs:1002-1005` | 빌드 시 초기 문자열 `""` (제거된 상태의 1프레임 깜빡임 방지) |
| `Assets/Scripts/View/CampaignMapView.cs:25-72` | Gold 토큰, 라벨 알파 바닥값(42-43), 링 상수(47), 링/센터 필드 |
| `Assets/Scripts/View/CampaignMapView.cs:80-93` | 테스트 시임 5종 |
| `Assets/Scripts/View/CampaignMapView.cs:160-184` | 프론티어 링 빌드(링크와 노드 사이) |
| `Assets/Scripts/View/CampaignMapView.cs:255-291` | 라벨 알파 바닥값 적용 + 프론티어 라벨 Gold + 링 재앵커 |
| `Assets/Tests/EditMode/HudLayoutTests.cs:409-506` | 신규 3건 (420 / 480 / 494) |
| `Assets/Tests/EditMode/LobbyLayoutTests.cs:35` | `ReducedMotionKey` 상수 |
| `Assets/Tests/EditMode/LobbyLayoutTests.cs:194-281` | 신규 3건 (205 / 224 / 253) |

**Sim/** 미수정. FROZEN 미수정. 커밋 없음. 타 세션 파일(`MetaScreenView.cs`,
`MetaScreenLayoutTests.cs`) **미접촉**.

---

## 6. 테스트 목록 (신규 9 + 6 = 15건)

`LootToastQueueTests` (9) — **9건 전부 대역외 실행 통과** (§0):
1. `Push_PutsTheNewestRowFirstAndEvictsPastCapacity`
2. `IdenticalConsecutivePickups_StackOnOneRowAndRestampIt`
3. `GradeIsPartOfTheRowIdentity_SoARarerDropNeverHidesInAStack`
4. `Tick_RetiresFromTheTailSoRowsNeverReorderUnderTheirWidgets`
5. `Alpha_RampsHoldsAndFadesInsideTheLifeWindow`
6. `ReducedMotion_ShowsAndHidesAtFullOpacityWithNoRamp`
7. `Revision_MovesOnlyWhenTheVisibleTextCanHaveChanged`
8. `Clear_DropsEveryRowWithoutAFade`
9. `KindOf_GivesEveryPickupKindExactlyOneRowIdentity`

`HudLayoutTests` (+3) `[TARGET]` — 컴파일만 확인, Unity 판정 필요:
10. `LootToastColumn_CoversNoCombatSurfaceAtPhoneTier`
11. `LootToastRows_NameTheItemItsGradeAndTheStackCount`
12. `ResetRunUi_ClearsTheLootToastColumn`

`LobbyLayoutTests` (+3) `[TARGET]`:
13. `SortieCards_StateOnlyWhatTheMapCannot`
14. `CampaignMap_DrawsLockedLabelsAtAReadableOpacity`
15. `CampaignMap_MarksTheFrontier_WithMotionReducedToo`

기존 픽스처 영향 검토: `LobbyLayoutTests`의 `FindButton("강하")`·
`PrimarySortieActions_...`는 **버튼 라벨을 건드리지 않았으므로 무영향**
(상태 라벨만 변경). `CampaignMapLayoutTests`는 전부 모델 레벨(`CampaignMapLayout`)
이라 렌더러 변경과 무관. `MetaScreenLayoutTests`는 Text만 순회하고 링은
Image라 무영향. `FontCoverageTests`는 §0에서 이미 0 missing 확인.

---

## 7. 미해결 / 사람 판단 필요

1. **"우측 이정표" 해석** — §1.2에 두 해석을 명시했고 둘 다 `_stageStatus`를
   가리키지만, 사용자가 SORTIE 패널 **전체**의 축소/이동을 원했다면 이번 변경은
   부족하다. 그 경우 지도를 경로 선택기로 승격하는 재설계가 필요하고, 이는
   레인 범위를 넘는다.
2. **미점등 링크 대비 1.34:1** (§2.2 보류) — 리빌 문법 vs 접근성 바닥값의
   트레이드오프. 수치만 남김.
3. **훈련 시련 카드의 `잠김`은 남겨뒀다.** 시련은 지도에 노드가 없어 중복이
   아니고, 상태 문구가 `미도전`/`최고 견습`처럼 실질 정보를 담는다. 다만
   스테이지 카드와 문법이 갈리는 점은 의도적 범위 경계로 기록한다.
4. **Unity 실행 판정 미완** — 테스트 12건이 `[TARGET]`이다. `.meta` 파일은
   손으로 생성한 GUID이므로 Unity 첫 임포트에서 확인 필요.
