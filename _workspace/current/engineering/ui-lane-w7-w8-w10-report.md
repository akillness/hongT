# UI 레인 — W7 탭 메타화면 · W8 캠페인 미니맵 · W10 커맨드 작업 큐

작성: 2026-08-07 · 표기 규약 CLAUDE.md §4 (`[OBSERVED]`/`[INFERENCE]`/`[TARGET]`)
범위: View 레이어 전용. Sim(`Assets/Scripts/Sim/**`) 무수정. 커밋/스테이징/push 없음.

---

## 0. 요약

| # | 결과 |
|---|---|
| W7 | 신규 전체화면 탭 메타화면(`MetaScreenView`) — 장비/각인/지도/조작 4탭. 로비 전용, 전투 HUD 무수정 |
| W8 | `StageEntry`에 노드 좌표 확장 + 순수 모델(`CampaignMapLayout`) + 렌더러(`CampaignMapView`). 로비 중앙 패널 + 메타화면 지도 탭 두 곳에서 공유 |
| W10 | 이벤트 트리거 작업 큐(`CommandQueue`, 순수 C#) + HUD 대기열 표시. 기존 `CommandSequenceRunner`는 무수정 재사용 |
| W15-적용 | 잿불 휴식 팝업 배경 이미지 로드 + 가독성 스크림. **자산 부재 시 기존 표시로 무음 폴백** (§8이 자산 레인 계약 정본) |

EditMode 신규 테스트 **24개**(`CommandQueueTests` 14 + `CampaignMapLayoutTests` 10)
및 기존 `LobbyLayoutTests`에 1개(3→4), `HudLayoutTests`에 1개 추가.

**Unity 배치모드 미실행**(지시대로). 아래 §6이 실제 수행한 검증의 전부다.

---

## 1. [OBSERVED] 착수 시점 실측 — 시드 §2 표와 다른 부분

시드는 W10을 "전무(단일 인텐트 즉시 소비)"로 기록했으나, 착수 시점 트리에는
이미 **커맨드 시퀀스 에이전트가 존재**했다:

- `Assets/Scripts/View/CommandPlan.cs` — 문장 → 순서형 `CommandPlan`(ParseLocal/ParseJson)
- `Assets/Scripts/View/CommandAgent.cs` — `CommandSequenceRunner`, gate→ack→settle 상태기계
- `Assets/Scripts/View/HudView.CommandAgent.cs` — 글루 3개 seam
- 기존 테스트 `CommandPlanParserTests`(20), `CommandSequenceRunnerTests`(15)

즉 W10에서 **실제로 비어 있던 것은 두 가지뿐**이다:
1. 진행 축이 **타이머(쿨다운/ack/settle)뿐** — SimEvents 단위 발동이 없음.
2. `CommandSequenceRunner.Begin`이 진행 중 플랜을 **교체**(`CommandAgent.cs:266-280`)
   — 여러 명령을 **적재**할 자료구조가 없음.

이번 작업은 이 두 구멍만 메웠고 러너/플랜 파서는 건드리지 않았다.

---

## 2. W7 — 아킬레우스형 탭 메타화면

### 신규 파일
`Assets/Scripts/View/MetaScreenView.cs` (648줄, `MonoBehaviour`)

### 화면 구성
레퍼런스(`_workspace/current/intake/reference-ui-ocr.txt` s3)에서 **원리만** 차용:
상단 탭바 · 좌측 카테고리 레일 · 중앙 대형 상세 카드(이름 바로 아래 등급) ·
3열 스탯 · 하단 키 힌트 밴드. 어휘·팔레트·수치는 전부 본작 것이다.

- **상단 탭바** (`MetaScreenView.cs:284`, 높이 74) — `장비 / 각인 / 지도 / 조작`,
  각 탭에 EN 키커(EQUIPMENT/SIGILS/MAP/CONTROLS). 우측에 유물·포인트·`닫기`.
- **장비 탭** (`:349`) — 좌측 레일 3행(무기/랜턴/망토, 각 T등급 표시) +
  중앙 700×250 상세 카드: 대형 아이템명(`LobbyView.EquipTierNames`) →
  바로 아래 **등급 라벨**(`GradeNames` = 평범/단련/정예/희귀/영웅/전설, T0..T5) →
  3열 스탯 `피해 / 생존 / 기름`.
- **각인 탭** (`:434`) — 같은 레일+상세 문법, 5각인의 A/B 면 효과와 장착 상태.
- **지도 탭** (`:481`) — W8 `CampaignMapView`를 920×340로 렌더(에피셋 포함).
- **조작 탭** (`:497`) — 던전 키맵 6행.
- **하단 힌트 밴드** (`:326`) — 탭별 문구, `탭 전환 Tab • 닫기 ESC`.

### 수치 정직성
표시되는 모든 능력치는 심의 파생 프로퍼티(`HackConfig.PlayerDamage` /
`PlayerMaxHealth` / `PlayerSpeed` / `LanternRegenPerSecond`)를 스택 probe로 읽는다
(`MetaScreenView.cs:Probe`) — `LobbyView.Probe`와 동일 seam. 뷰가 공식을 재조합하는
곳은 없다. 등급 보정 %는 `CampaignSpec.WeaponDamagePerRank` 등 동결 상수 ×랭크.

**조작 탭 6행은 `InputAdapter.ReadKeyboard`의 `Profile.Dungeon` 분기
(`InputAdapter.cs:76-88`)를 그대로 전사**했다. 초안에 있던 "노바 R / 화살 1 /
파동 2 / 좌클릭 공격"은 실제 바인딩(Q 화살 · E 파동 · R 노바 · F 방패 ·
Shift 질주 · Space 공격 · G/H/V 동료)과 달라 폐기했다.

### 진입 경로
로비 캠페인 지도 패널의 `정비` 버튼 → `TabEquip`, `지도` 버튼 → `TabMap`
(`LobbyView.cs:275-288`). 공개 API `LobbyView.OpenMetaScreen()`(`:293`)도 있다.

- 캔버스 `sortingOrder = 12` (로비 5 위, 인트로 릴 520 아래).
- 배경 전면 판이 `raycastTarget = true` — 아래 로비로 탭이 새지 않는다.
- 키보드 전접근: `Tab` 탭 순환 · `↑↓` 행 이동 · `ESC` 닫기 (`MetaScreenView.cs:Update`).
- `LobbyView.Hide()`가 메타화면도 같이 닫는다(`LobbyView.cs:619`) —
  던전 시작 시 메타 캔버스가 전장 위에 남는 것을 막는다.

### 전투 HUD
`HudView`의 전투 위젯은 **하나도 건드리지 않았다**. W7이 만든 HudView 변경은 0줄이다.
HudView를 만지는 것은 W10(§4, 4곳)과 W15(§8, 잿불 휴식 패널)뿐이며 둘 다 전투
위젯이 아니다.

---

## 3. W8 — 로비 캠페인 미니맵

### 카탈로그 확장
`Assets/Scripts/View/StageCatalog.cs:63-102` — `StageEntry`에 `NodeX, NodeY`
(정규화 0..1) 추가, 생성자 인자 2개 확장, 9개 엔트리 전부 좌표 지정
(`:170, 178, 186, 194, 202, 210, 221, 229, 237`).

**해저드/수치/ID/프리즈 계약은 한 글자도 바뀌지 않았다.** 추가된 것은 표현용
좌표 필드뿐이며 Sim은 이 필드를 읽지 않는다. `new StageEntry(` 호출부는
`StageCatalog.cs` 내부 9곳이 전부다(전 저장소 grep 확인).

좌표는 선형 prereq 체인(0→…→8)을 진행바가 아니라 성좌로 읽히게 하는 수작업
배치다. float32 기준 **최소 축간 분리 0.18** (게이트 임계 0.10).

### 순수 모델
`Assets/Scripts/View/CampaignMapLayout.cs` (183줄, UnityEngine 비의존)

- `CampaignNodeState { Locked, Unlocked, Cleared }` — `CampaignStore`/`StageCatalog`의
  `IsCleared`/`IsUnlocked`를 그대로 읽으므로 출정 카드와 어긋날 수 없다.
- 밝혀가기 규약: 잠김 노드는 이름을 `"???"`로 **가린다**(라벨 자체가 공개 상태).
  정화 노드만 에피셋을 준다. 투명도 `1.0 / 0.55 / 0.16` — 잠김도 0이 아니다
  (안 보이면 캠페인이 계속된다는 사실 자체가 숨겨진다).
- `BuildLinks`는 `PrereqId`를 걸어 만들므로 향후 분기 추가 시 간선이 자동 생성된다.
- `FrontierIndex`, `ProgressLine`("정화 3 / 9 • 다음 …") 단일 출처.

### 렌더러
`Assets/Scripts/View/CampaignMapView.cs` (246줄, MonoBehaviour 아님 — 위젯)

- 노드 = 45° 회전 사각(다이아). **채워짐 = 정화**, 코어가 어두우면 미정화.
  색은 `StageEntry.AccentColor`, 알파는 상태.
- 간선 = 회전한 1px 라인, 선행 정화 시 엠버로 점등.
- 프론티어 노드만 1.4 s 심장박동(`Tick`) — `ViewPrefs.ReducedMotion`이면 생략.
- 좌표는 호스트가 주는 **고정 UI 단위**로 계산한다. `RectTransform.rect`를
  되읽으면 노드 위치가 "유니티가 마지막으로 레이아웃을 돌린 시점"에 의존하게 되고,
  그건 EditMode rect 감사로 고정할 수 없는 프레임 순서 의존성이다.

### 로비 배치
`LobbyView.BuildMapPanel`(`LobbyView.cs:260-289`), 티어 전환 `:717-734`.

- 폭은 **양 티어 모두 424 고정**. 데스크톱은 SANCTUM(400)과 SORTIE(392) 사이
  중앙 거터 `(432,-72)`, 스택(폰)은 SANCTUM 아래 `(0,-1284)`에 가운데 정렬.
- 패널 내부: 심연 지도 헤더 → 맵 필드 408×150 → `지도`/`정비` 버튼 196×96 2개.

---

## 4. W10 — 커맨드 시퀀스 → 작업 큐 → 이벤트 발동

### 순수 큐
`Assets/Scripts/View/CommandQueue.cs` (372줄, UnityEngine 비의존)

- `CommandTriggerKind` = `Immediate / Kills / WaveStart / BossSpawn / Pickup /
  PlayerDamaged / Extraction` — 각각 `SimEvents.EnemyKilled / WaveStarted /
  BossSpawned / PickupCollected / PlayerDamaged / ExtractionComplete`에 대응.
- `CommandTrigger.Count` 상한 10 (오타 "300킬"이 플랜을 영구 대기시키지 않게).
- `CommandQueue`는 **엄격 FIFO이며 헤드의 트리거만 평가**한다. 뒤 항목이 몰래
  진척을 적립하지 않으므로 HUD가 설명할 조건은 항상 하나다. 깊이 상한 4.
- `CommandTriggerParser.TrySplit(raw, out trigger, out remainder, out prefix)` —
  선행 트리거 구절 분해. 두 가지 안전 규칙:
  1. **트리거 뒤에 아무것도 없으면 트리거가 아니다.** "보스 등장" 단독은 그대로
     기존 인텐트 테이블로 흘러간다.
  2. `prefix`(트리거 앞 절)가 실제 플랜으로 파싱되면 **지금 즉시** 실행한다.
     "노바 쓰고 셋 잡으면 결계"에서 앞 절을 버리면 문장 절반을 삼키게 된다.
     한정어("적 셋")는 파싱 결과가 비어 있어 자연히 무시된다.
- 대소문자 폴딩은 **문자 단위**로 한다(`IndexOfFolded`). `ToLowerInvariant`는
  일부 문자에서 길이를 바꿀 수 있고, 1글자 어긋나면 문장을 엉뚱한 데서 자른다 —
  `CompanionCommandParser.TryMatchAt`이 문서화한 동일 함정.

### [OBSERVED] 정직한 한계 — 반드시 기록
`SimEvents`는 **틱당 플래그 마스크이지 카운터가 아니다**(`SimTypes.cs:90-92`).
한 고정스텝에 적 둘이 죽어도 `EnemyKilled`는 한 번 선다. 따라서 "3 처치" 트리거는
**시체가 아니라 처치 틱을 센다.** 시체를 세려면 뷰가 소유하지 않은 주기로 심 상태를
폴링해야 하고, 그건 이 레이어가 거부하는 결합이다. 이 한계는 `CommandQueue.cs`
파일 헤더에 그대로 적혀 있다.

### 결정론
Sim은 전혀 손대지 않았다. 큐가 하는 일은 **키 입력이 세울 것과 똑같은
`InputAdapter` 래치를 언제 세울지**를 정하는 것뿐이며, 발동 경로는 기존
`StartCommandPlan` → `CommandSequenceRunner` → `ApplyCommandIntent`를 그대로 탄다.
래치는 프레임당 1개 소비이므로 결정론 무해(시드 §3 [INFERENCE] 유지).

### HUD 글루 — `HudView.CommandAgent.cs`
| 위치 | 내용 |
|---|---|
| `:20-31` | `_queue` + 대기열 패널 필드 |
| `:175` | `TryQueueCommand` — 트리거 있으면 적재, 없으면 `false`(기존 즉시 경로 무변경) |
| `:208` | `ObserveCommandQueueEvents(SimEvents)` — 헤드 진척 |
| `:219` | `ReleaseQueuedCommand` — 러너가 **비었을 때만** 헤드 방출 |
| `:256` | `SyncCommandQueuePanel` — 대기 목록 + 다음 발동 조건 렌더 |
| `TryHandleAgentControl` | "취소/중단/stop"이 **진행 중 + 대기열 전부** 정리 |
| `TickCommandAgent` | 던전을 벗어나면 러너 취소 + 큐 삭제 |

### HUD 시각화
대기열 패널은 콘솔 위 `(0, 380)`, 460 × (26 + 4×20). 헤더가
`대기 명령 2/4 — 다음 처치 2/3`, 아래 행이 `1. 처치 2/3 • 잿불 노바 → 공허 방패`.
헤드만 밝고 뒤는 반투명(감시 중인 조건이 하나뿐임을 문장 없이 말한다).
큐가 비면 패널 자체가 꺼진다.

### `HudView.cs` 변경 — W10은 4곳뿐
- `:1345` 콘솔 플레이스홀더에 트리거 문법 노출
  (`명령: 집중공격 • 노바 • 결계 / 대기: 셋 잡으면 노바`).
- `:1363` `BuildCommandQueuePanel()` 호출(콘솔과 함께 지연 생성).
- `:1463` `SubmitCommand`에 `TryQueueCommand(raw)` 1줄.
- `:2802` `OnEvents`에 `ObserveCommandQueueEvents(events)` 1줄.

**`GameView.cs`는 수정하지 않았다.** `Hud.OnEvents`(`GameView.cs:382`)와
`Hud.SyncCommandAgent`(`:651`)가 이미 호출되고 있어 신규 훅이 불필요했다.
VFX 레인과의 공유 위험 파일 접촉 0.

---

## 5. 변경/신규 파일 전체 목록

### 신규 (5)
| 경로 | 줄 | 성격 |
|---|---|---|
| `Assets/Scripts/View/CampaignMapLayout.cs` | 183 | 순수 C# 모델 |
| `Assets/Scripts/View/CampaignMapView.cs` | 246 | uGUI 위젯 |
| `Assets/Scripts/View/MetaScreenView.cs` | 648 | MonoBehaviour |
| `Assets/Scripts/View/CommandQueue.cs` | 372 | 순수 C# |
| `Assets/Tests/EditMode/CommandQueueTests.cs` | — | 14 tests |
| `Assets/Tests/EditMode/CampaignMapLayoutTests.cs` | — | 10 tests |

### 수정
| 경로 | 내용 |
|---|---|
| `Assets/Scripts/View/StageCatalog.cs:63-102, 170-237` | `NodeX/NodeY` 필드 + 9엔트리 좌표 |
| `Assets/Scripts/View/LobbyView.cs:161-176, 225-293, 302-303, 619-627, 632, 717-734` | 맵 패널·메타화면 소유·티어 배치 |
| `Assets/Scripts/View/LobbyView.cs` (sigil 상수 4개) | `static` → `internal` (메타화면이 같은 어휘를 읽도록) |
| `Assets/Scripts/View/HudView.CommandAgent.cs` | 큐 글루 전부 |
| `Assets/Scripts/View/HudView.cs` | W10 위 4곳 + W15 잿불 휴식 배경(§8) |
| `Assets/Scripts/View/CommandAgent.cs` | 문자열 구분자 `·`→`•` 2곳 (§7) |
| `Assets/Tests/EditMode/LobbyLayoutTests.cs` | 신규 테스트 1개 |
| `Assets/Tests/EditMode/HudLayoutTests.cs` | 신규 테스트 1개 (W15) |
| `Assets/Resources/Fonts/HudKorean.otf` | 재생성 (§7) |

**타 세션 수정 중 파일(`SceneBuilder.cs`, `EnvironmentBuilder.cs`, `graphify-out/*`)과
타 레인 소유 파일(`Sim/**`, `VfxDirector.cs`, `CameraRig.cs`, `RuntimeMaterialSeeds`,
`tools/audio/**`, `tools/blender/**`) 접촉 0.** `git status --short`로 확인.

---

## 6. [OBSERVED] 실제 수행한 검증

Unity 배치모드는 지시대로 실행하지 않았다. 수행한 것:

1. **구문 검사** — `mcs -target:library -langversion:latest`로 신규 4파일 +
   수정 3파일 + 신규/수정 테스트 3파일 일괄 컴파일.
   결과: `CS0246`(UnityEngine/NUnit/Sim 타입 없음) 417건, `CS0234` 8건 — **구문
   오류 0**. (참고: `mcs`는 C# 8 switch expression을 파싱하지 못하므로 기존
   `CommandAgent.cs`는 이 검사에서 제외했다. 해당 파일 수정은 문자열 2곳뿐.)
2. **노드 좌표 분리 검증** — float32로 45쌍 전수 계산, 최소 축간 분리 **0.18**
   (게이트 0.10). `CampaignMapLayoutTests`가 런타임에서 다시 잡는다.
3. **폰트 cmap 게이트 재현** — `FontCoverageTests.ShippedCmap`과 동일한 OTF cmap
   파서를 파이썬으로 구현해 `Assets/Scripts/View/*.cs`의 전 한글 음절 대조.
   재생성 후 **결손 0** (cmap 572 코드포인트).

**미검증(오케스트레이터 통합 실행 필요):** EditMode 실행 결과, 실제 렌더 결과,
WebGL 빌드, 프레임 예산.

---

## 7. 폰트 — 재생성 완료, 그러나 마지막에 한 번 더 필요

`Assets/Resources/Fonts/HudKorean.otf`를 `bash tools/gen_hud_font.sh`로
**재생성했다**(549 → 572 코드포인트, 59064 bytes).

- 신규 한글 27자(평범/단련/희귀/영웅/전설, 탭/조작/콘솔/좌측/좌클릭, 탈출/전리품,
  하나/둘/셋/넷/다섯, 가득 찼다 …)가 기존 서브셋에 없었다.
- 재생성은 **다른 레인의 결손도 같이 고쳤다**: 착수 시점 `VfxDirector.cs`의 `낙`,
  작업 중 착지한 `TerrainFlipbook.cs`의 `젝`이 모두 서브셋에 없어 게이트가
  이미 red였다.

**[TARGET] 오케스트레이터 조치:** 다른 레인이 한글 문자열을 추가하면 게이트가 다시
깨진다. **모든 레인 착지 후 `bash tools/gen_hud_font.sh`를 한 번 더 실행할 것.**

### 부수 발견 — `·`(U+00B7)는 소스 폰트에 없다
`NanumBarunGothic.otf` 자체가 U+00B7을 담고 있지 않다. 그런데 착수 시점
`CommandAgent.cs`("시퀀스 완료 · N단계"), `LobbyView.cs`("협동 AI ON · 동시 N"),
`HudView.CommandAgent.cs`(" · 제미나이")의 **화면 출력 문자열**이 이 글자를
쓰고 있었다 — WebGL에는 폰트 폴백이 없으므로 이미 두부로 렌더되고 있었을 것이다
[INFERENCE: 런타임 미관측].

`FontCoverageTests`는 `[가-힣]`만 검사하므로 이 결손을 잡지 못한다.

조치: 내 신규 문자열과 위 기존 3파일의 **문자열 리터럴 안 `·`를 `•`(U+2022,
서브셋에 이미 존재)로 교체**했다. 주석 안 `·`는 그대로 두었다(렌더되지 않음).
남은 미포함 문자는 `−`(주석)와 `✓`(`HudView.Integration.cs`의 `Debug.Log`)뿐 —
둘 다 화면에 나오지 않는다.

**[TARGET] 후속:** `FontCoverageTests`의 harvest 규칙을 한글에서 "문자열 리터럴의
전 문자"로 넓히면 이 부류를 구조적으로 막을 수 있다. 이번 범위 밖이라 제안만 남긴다.

---

## 8. W15-적용 — 잿불 휴식 팝업 배경 이미지

### 대상 코드
`Assets/Scripts/View/HudView.cs` — `BuildEmberRestPanel`(`:830`) 내부.
deterministic offer hash 선례가 있는 그 패널이 맞다
(`ShowEmberRest`/`SelectEmberRestOffer`/`DeferEmberRest`, `HudView.cs:786-910`).

### 구현
| 위치 | 내용 |
|---|---|
| `HudView.cs:74-76` | `_emberRestBackdrop`, `_emberRestScrim` (아트 부재 시 둘 다 null 유지) |
| `HudView.cs:841-846` | `BuildEmberRestBackdrop()` 호출 — **title/카드/버튼보다 먼저** |
| `HudView.cs:895-...` | 상수·테스트 seam·빌더·옵션 스프라이트 로더 |

- **레이어 순서**: uGUI는 sibling 순서로 그리므로, 배경(0) → 스크림(1) → 읽는 것 전부(2+)를
  **생성 순서만으로** 보장한다. 나중에 `SetSiblingIndex`를 부르는 코드가 없으니 잊힐 수도 없다.
- **스크림**: `new Color(0.02f, 0.03f, 0.05f, 0.62f)`, 상수 `EmberRestScrimAlpha`.
  아트가 없으면 스크림도 만들지 않는다 — 패널 본래 채움색이 이미 거의 불투명이라
  어둡게 한 겹 더 까는 건 의미 없이 탁해질 뿐이다.
- **폴백**: 스프라이트가 null이면 `BuildEmberRestBackdrop`이 즉시 return한다.
  패널은 이 기능이 없던 때와 **픽셀 단위로 동일**하게 그려진다. NRE 경로 없음,
  소프트락 경로 없음.
- **탭 안전**: 배경·스크림 모두 `raycastTarget = false`. 장식 레이어가 오퍼 탭을
  삼키면 그게 곧 소프트락이다.
- **스트레치**: `preserveAspect = false`로 패널을 꽉 채운다. 레터박스는 두 변에
  본래 채움색을 노출시켜 액자가 아니라 버그로 읽힌다. 대신 자산 레인에 정확한
  종횡비를 넘겨서 스트레치가 명목상 0이 되게 했다(아래).

### [정본] 자산 레인 계약 — 이 경로가 코드가 실제로 읽는 값이다

**파일명(확장자·디렉터리 제외):** `ui-ember-rest-bg`
**코드 상수:** `HudView.EmberRestBackdropId = "ui-ember-rest-bg"` (`HudView.cs`)

**허용 드롭 경로 3곳** — `HudIconIntegration`과 동일한 탐색 순서(먼저 찾은 것 사용):
1. `Assets/Resources/Icons/regenerated/ui-ember-rest-bg.png`
2. `Assets/Resources/Icons/generated/ui-ember-rest-bg.png`
3. `Assets/Resources/Icons/ui-ember-rest-bg.png`  ← **권장**

**요구 사양**
| 항목 | 값 | 근거 |
|---|---|---|
| 해상도 | **1024 × 694** | 패널 620×420(고정, 전 티어 동일 — `HudView.cs:834`)의 종횡비 1.4762. WebGL 텍스처 상한 1024(CLAUDE.md §1)에 정확히 맞춤 |
| 내용 | **텍스트 없는 순수 배경** | 위에 한국어 UI가 전부 올라간다 |
| 구도 | 중앙~하단은 비교적 평탄하게 | 오퍼 카드 3장(y −88..−216)과 하단 버튼 2개가 그 위에 온다 |
| 임포터 | `textureType: 8` (Sprite), `spriteMode: 1` (Single) | **필수.** 기본값 `textureType: 0`이면 `Resources.Load<Sprite>`가 null을 반환해 조용히 폴백된다 |

> 임포트 설정이 틀리면 **실패가 아니라 무음 폴백**이다. 통합 검증 때
> `HudView.EmberRestBackdropPresent`가 true인지로 실제 적용 여부를 확인할 것.

### 테스트
`Assets/Tests/EditMode/HudLayoutTests.cs` — `EmberRestBackdrop_IsOptional_AndNeverBlocksTheOffers`.

아트가 아직 없으므로 **양쪽 분기를 모두 검증**한다: `EmberRestBackdropPresent`가
true면 레이어 순서(0/1/2+)·스크림 알파·raycast 차단 없음을, false면 두 레이어가
아예 생성되지 않았음을 잡는다. 어느 쪽이든 오퍼 선택 → `계속` 활성화까지 동작해야
한다. 자산이 착지하면 같은 테스트가 자동으로 반대 분기를 검사하기 시작한다.

### 검증
- `mcs` 구문 검사 통과(신규 코드 오류 0). `HudLayoutTests.cs:957`의 CS1547/CS0128 7건은
  **HEAD에도 동일하게 존재**하는 mcs의 로컬 함수 파싱 한계로, `git show HEAD:` 판을
  같은 컴파일러에 넣어 재현 확인했다. 내 추가분과 무관하다.
- 신규 한글 없음 — 폰트 게이트 영향 0.
- **미검증:** 실제 이미지가 없어 렌더 결과·가독성은 관측하지 못했다. 스크림 알파 0.62는
  [TARGET] 초기값이며, 아트 착지 후 실제 대비로 조정할 여지가 있다.

---

## 9. 미해결 / 사람 판단 항목

1. **`.meta` 미생성** — 신규 `.cs` 6개는 아직 `.meta`가 없다. 유니티가 첫 임포트에
   생성한다. 커밋 전 확인 필요.
2. **스택 레이아웃 세로 총장** — 폰 스택 컬럼이 기존 -1268에서 맵 패널 추가로
   -1604까지 내려간다. 실측 최악 뷰포트(390×844, portrait match 0.35)의 유효 높이는
   ≈1729이므로 들어간다. **그보다 세로가 짧은 스택 상황(가로 좁은 데스크톱 창)에서는
   맵 패널 하단이 잘릴 수 있다** — 로비는 스크롤하지 않는다(출정 카드 리스트만 뷰포트).
   해결하려면 로비 전체를 스크롤 뷰로 바꿔야 하고, 그건 이번 범위를 넘는다.
3. **`LobbyLayoutTests` 터치 부채표** — `InteractiveLobbyRects_HoldTheMeasuredTouchFloorDebt`는
   부채 집합을 **정확 일치**로 잠근다. 신규 `지도`/`정비` 버튼은 196×96 =
   95.6 × 46.8 CSS px로 바닥을 넘으므로 표는 그대로다. 다른 레인이 로비에 버튼을
   추가하면 이 테스트가 먼저 깨진다는 점을 공유한다.
4. **트리거 어휘 확장** — 현재 6종. "체력 절반 이하", "제단 점화" 같은 조건은
   대응 `SimEvents`가 없어 넣지 않았다. 필요하면 Sim amendment 사안이다.
5. **Gemini 원격 플랜 + 트리거** — 원격 경로(`PlanRemote`)는 트리거를 만들지 않는다.
   `TryQueueCommand`가 `SubmitCommand`에서 원격 호출보다 먼저 돌기 때문에
   "트리거 + 로컬 파싱 가능한 명령"만 적재된다. 자유 문장 + 트리거 조합은 미지원.
