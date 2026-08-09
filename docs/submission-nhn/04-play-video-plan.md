---
title: "제출물 2 · 플레이 동영상 촬영 대본"
subtitle: "내부 작업 지시서 — 제출물 아님. 촬영·편집 담당자가 읽는 문서"
author: "HongT"
lang: ko
---

> **이 문서는 제출물이 아니다.** 제출물 2(30~60초 플레이 동영상)를 찍기 위한
> 내부 지시서다. 심사자에게 전달되는 것은 YouTube 영상 링크뿐이다.
> 근거 표기 규약(`[OBSERVED]` / `[INFERENCE]` / `[TARGET]`)을 그대로 쓴다.

---

# 0. 심사 규정 (제출물 2)

`docs/nan2026/00-submission-guide.md` §2 원문:

- 30~60초 분량, **실제 게임 플레이 장면 중심**
- **AI를 이용한 조작·합성이나 타인 영상의 도용 불가.** 실제 플레이 화면 그대로
- 공개 또는 링크 공유 상태로 YouTube 업로드

**따라서 허용되는 후처리는 컷 편집과 자막뿐이다.** 금지: 화면 합성, 속도 조작
(배속·슬로 모션 추가), 생성형 AI로 만든 장면·전환·나레이션 영상, 게임 화면
위에 얹는 가짜 UI. 게임 자체가 만드는 슬로 모션(콘솔 입력 중 `timeScale` 0.2)은
편집이 아니라 **게임 기능**이므로 그대로 담는다 —
`[OBSERVED]` `Assets/Scripts/View/HudView.cs:2090` "GameView caps timeScale at
0.2 while this is true — typing …".

## 0.1 기존 산출물에 대한 판단 필요

`docs/nan2026/assets/video/nan2026-cinder-court-cycle13-final.mp4` (55.00초)는
최종 로컬 WebGL 후보를 실제 Chromium에서 렌더링했지만,
`tools/video/capture-unity-play.mjs`로 **Playwright가 키 입력을 대신 넣어**
캡처한 것이다. 동반 JSON은 로컬 빌드 ID `e853ef3b27239fab`, 입력 횟수, SHA-256,
브라우저 오류를 기록한다.

- `[OBSERVED]` 렌더링은 실제 게임 화면이고 합성·재생성은 없다(스크립트 주석 L3-8).
- `[INFERENCE]` 그러나 "AI를 이용한 조작"의 해석 여지가 있다. 자동화 스크립트가
  입력을 넣은 영상을 "실제 플레이"로 볼지는 심사자 재량이다.
- `[TARGET]` **사람이 직접 손으로 플레이한 테이크로 교체한다.** 이 대본은 그
  전제로 쓰였다. 자동화 영상은 기술 검증 증적으로 보존하되 제출하지 않는다.

---

# 1. 조작키 (던전 프로파일)

`[OBSERVED]` `Assets/Scripts/View/InputAdapter.cs:76-88`(모드별 분기),
`L145-148`(이동), `L64`(공격), `HudView.cs:2453-2466`(콘솔).

| 입력 | 동작 | 근거 |
|---|---|---|
| `W` `A` `S` `D` / 방향키 | 이동 | `InputAdapter.cs:145-148` |
| `Space` (누르고 있기) | 일반 공격 · 콤보 | `InputAdapter.cs:64`, `L154-155` |
| `Shift` (좌/우) | 대시 | `InputAdapter.cs:83-84` |
| `Q` | 볼트 (Bolt) | `InputAdapter.cs:77` |
| `E` | 펄스 (Pulse) | `InputAdapter.cs:78` |
| `R` | **노바 (Nova)** — 던전에서 R은 재시작이 아니다 | `InputAdapter.cs:79-81` |
| `F` | 결계 (Ward/Aegis) | `InputAdapter.cs:82` |
| `G` | 동료 정지·현 위치 사수 | `InputAdapter.cs:85` |
| `H` | 동료 복귀 | `InputAdapter.cs:86` |
| `V` | **동료 시그니처 스킬** (AMENDMENT #8) — 준비된 모든 슬롯에 한 번에 명령 | `InputAdapter.cs:87`, `L53-55` |
| `1` `2` `3` | 레벨업 선택 | `InputAdapter.cs:159-161` |
| `Enter` | 텍스트 커맨드 콘솔 **열기** / 열려 있으면 **제출** | `HudView.cs:2455-2466` |
| `Esc` | 콘솔 닫기(제출 안 함) | `HudView.cs:2456` |

주의할 점:

- `[OBSERVED]` 콘솔은 **던전 화면이 떠 있고 게임오버 패널이 없을 때만** 열린다
  (`HudView.cs:2461-2463`). 로비에서 `Enter`는 아무 일도 하지 않는다.
- `[OBSERVED]` 콘솔이 열려 있는 동안 `InputAdapter.TextInputActive`가 켜져
  WASD·핫키가 전부 막힌다(`InputAdapter.cs:136-141`). 문장을 치는 동안 캐릭터가
  멈춰 보이는 것은 정상이다.
- `[OBSERVED]` 아레나 모드는 키 배치가 다르다(Q=노바, E=결계, R=재시작).
  **영상은 던전(캠페인) 모드로만 찍는다.**

---

# 2. 실제로 파싱되는 텍스트 커맨드

`[OBSERVED]` 어휘는 `Assets/Scripts/View/CompanionCommandParser.cs:44-76`,
계획 분해는 `CommandPlan.cs:124-150`, 조건 예약은 `CommandQueue.cs:241-258`.
아래 문장은 전부 코드의 키워드 표에 실재하는 것만 골랐다.

## 2.1 단일 명령

| 입력 문장 | 결과 | 대응 키 |
|---|---|---|
| `노바` | 플레이어가 노바 시전 | `R` |
| `결계` (또는 `방패`, `보호막`) | 플레이어가 결계 시전 | `F` |
| `파동` (또는 `펄스`) | 플레이어가 펄스 시전 | `E` |
| `화살` (또는 `볼트`) | 플레이어가 볼트 시전 | `Q` |
| `질주` (또는 `대시`, `돌진`) | 플레이어 대시 | `Shift` |
| `특기` (또는 `필살기`, `시그니처`) | **동료가 자기 시그니처 스킬 사용** | `V` |
| `집중공격` (또는 `잡아`, `공격해`) | 동료 집중 공격 | — |
| `방어` (또는 `지켜`, `호위`) | 동료 방어 태세 | `G` 계열 |
| `복귀` (또는 `돌아와`, `따라와`) | 동료 복귀 | `H` |

`[OBSERVED]` `Skill*` 계열은 **플레이어 본인**이 쓰는 것이고, `특기`만이
동료가 직접 수행하는 유일한 명령이다(`CompanionCommandParser.cs:17-28`).
자막을 붙일 때 이 구분을 틀리지 말 것.

## 2.2 다단계 시퀀스 (한 문장 → 순서 있는 계획)

`[OBSERVED]` `CommandPlanParser.ParseLocal`은 **문장에 나타난 순서대로** 계획을
만든다(`CommandPlan.cs:95-101`). 최대 6단계(`CommandPlan.MaxSteps`).

| 입력 문장 | 계획 |
|---|---|
| `노바 쓰고 결계 쳐` | 노바 → 결계 |
| `결계 치고 3초 뒤에 노바` | 결계 → 3초 대기 → 노바 |
| `질주하고 집중공격` | 대시 → 동료 집중 공격 |

`[OBSERVED]` 대기 시간은 0.1~10초로 강제 클램프된다(`CommandPlan.cs:32-33`).
`[OBSERVED]` 시퀀스가 시작되면 화면에 `시퀀스 N단계` 토스트가 뜬다
(`HudView.CommandAgent.cs:129-133`).

## 2.3 조건 예약 (트리거)

`[OBSERVED]` `CommandTriggerParser`가 문장 앞부분의 조건 어구를 떼어내고,
뒤에 오는 명령을 대기열에 건다(`CommandQueue.cs:264-319`).

| 입력 문장 | 조건 | 예약될 계획 |
|---|---|---|
| `셋 잡으면 노바` | 처치 3회 | 노바 |
| `보스 나오면 특기` | 보스 등장 | 동료 시그니처 |
| `다음 웨이브에 결계` | 웨이브 시작 | 결계 |
| `맞으면 복귀` | 피격 | 동료 복귀 |
| `노바 쓰고 셋 잡으면 결계` | (노바는 즉시) + 처치 3회 | 결계 |

`[OBSERVED]` 한글 수사 `하나/둘/셋/넷/다섯`을 숫자로 읽는다
(`CommandQueue.cs:262`, `CountIn` L348-369). 숫자 표기(`3 잡으면`)도 된다.
`[OBSERVED]` 대기열은 최대 4개(`CommandQueue.MaxEntries`), 처치 카운트 상한 10.
`[OBSERVED]` 대기열 항목은 HUD에 `처치 1/3 • 잿불 노바` 형태로 진행도가 표시된다
(`CommandTrigger.Describe` L76-88, `CommandQueueEntry.StatusLine` L143,
표시 이름은 `CommandAgentSpec.LabelOf` L217-230: 노바=`잿불 노바`,
결계=`공허 방패`, 볼트=`균열 화살`, 펄스=`묘지 파동`, 특기=`동료 특기`).

**정직한 한계 — 자막에서 과장하지 말 것.** `[OBSERVED]` `CommandQueue.cs:20-25`:
처치 트리거는 시체가 아니라 **킬 틱**을 센다. 같은 고정스텝에 둘이 죽으면 1회로
집계되므로, "3 처치" 조건이 실제로는 3명보다 많은 사망을 요구할 수 있다.

## 2.4 취소

`[OBSERVED]` `취소` / `중단` / `정지` / `그만` / `멈춰` / `stop` / `cancel` /
`abort` 중 아무거나 입력하면 진행 중 시퀀스와 대기열 전체가 비워진다
(`HudView.CommandAgent.cs:35-38`, `L136-166`). 촬영 중 사고 복구용으로 기억해 둘 것.

---

# 3. 촬영 전 사전 조건 — 동료 3명 동시 출전

`[OBSERVED]` 동시 활성 슬롯 상한은 **3**이다(`GameDirector.cs:735` `slots.Length < 3`,
`HackTypes.cs:361-371`). 슬롯을 채우려면 해당 동료를 **보유(roster)** 하고 있어야
하고(`LobbyView.cs:752` `_rosterButtons[i+1].interactable = owned`), 보유는
스테이지 클리어 보상으로만 얻는다.

`[OBSERVED]` 동료 획득 경로 (`Assets/Scripts/View/StageCatalog.cs:163-236`,
`LobbyView.cs:114-117`):

| 동료 id | 표시 이름 | 획득처 |
|---|---|---|
| `ember-cohort` | 잿불 사도 | 재의 다리 (cinder-span) 클리어 |
| `shade-echo` | 그림자 메아리 | 서약의 성당 (abyss-chancel) 클리어 |
| `possessed-echo` | 홀린 자 메아리 | 메아리 왕좌 (echo-throne) 클리어 |
| `scout-echo` | 정찰꾼 메아리 | 재의 행진 (ash-march) 클리어 |
| `ember-cohort-echo` 등 | 잿불 메아리 등 | 엘리트 추출 시 에코 획득 (`GameDirector.cs:1102-1116`) |

→ `[INFERENCE]` **3명을 정직하게 모으려면 최소한 cinder-span · abyss-chancel ·
echo-throne 세 스테이지를 클리어해야 한다.** 촬영용으로 매번 이걸 다시 하는 것은
비현실적이므로, 아래 세이브 주입을 쓴다.

## 3.1 방법 A — 브라우저 localStorage에 세이브 주입 (배포본 촬영용, 권장)

`[OBSERVED]` 세이브는 localStorage 키 `abyssal-lantern:unity:campaign` 하나에
JSON 문자열로 저장된다(`CampaignStore.cs:56`,
`Assets/Plugins/WebGL/storage.jslib:6`). 형식은 `CampaignStore.Save`
(`CampaignStore.cs:128-178`)의 출력 그대로다.

<https://akillness.github.io/hongT/> 를 연 뒤 **개발자 도구 콘솔**에서 실행:

```js
localStorage.setItem(
  "abyssal-lantern:unity:campaign",
  JSON.stringify({
    clearedMask: 63,
    equipment: { weapon: 5, lantern: 5, cloak: 5 },
    stats: { attack: 10, vitality: 10, swiftness: 10, points: 0 },
    relics: 30,
    roster: ["ember-cohort", "shade-echo", "possessed-echo"],
    active: "ember-cohort",
    activeSlots: ["ember-cohort", "shade-echo", "possessed-echo"],
    prologueDone: true,
    sigilsOwned: 0, sigilFaces: 0, sigilSlot0: 0, sigilSlot1: 0,
    trialTiers: 0, trainingMastery: false,
    guidanceSeen: 2147483647
  })
);
location.reload();
```

각 필드의 근거:

- `clearedMask: 63` — 원작 6스테이지 클리어(비트 0-5). 카탈로그 폭으로 마스킹되므로
  안전(`CampaignStore.cs:78-80`).
- `activeSlots` 3개 — 이 배열이 그대로 출전 동료가 된다. `active`는 슬롯 0의
  하위호환 별칭이므로 첫 원소와 같게 둔다(`CampaignStore.cs:150-157`).
- `prologueDone: true` — 프롤로그 재생 방지.
- `guidanceSeen: 2147483647` — **중요.** `[OBSERVED]` 안내 카드는 이 비트마스크로
  "이미 봤음"을 기록하고, 안 본 항목이 있으면 플레이 도중 일시정지 카드로 뜬다
  (`GuidanceCatalog.Seen` L192-193, `BitCeiling = 31` L103). 촬영이 끊기지 않게
  전부 본 것으로 표시한다. 부호 비트를 세우면 0으로 되돌아 읽히므로
  `2147483647`(비트 0-30)이 안전 최대값이다.
- `[INFERENCE]` JSON 키 이름·중첩 구조는 `Save`의 출력과 일치해야 한다.
  파서는 고정 형태 문자열 스캔이므로(`ExtractInt` 계열) 키를 빠뜨리면 그 값만
  0/""로 읽힌다 — 즉 누락은 크래시가 아니라 조용한 기본값이다.

주입 후 로비 → **군단 탭**에서 세 이름이 금색(활성)으로 표시되는지 눈으로
확인한다(`LobbyView.cs:745-756`).

## 3.2 방법 B — Unity 에디터 메뉴 (에디터 플레이 촬영 시)

`[OBSERVED]` `Assets/Editor/DevUnlockMenu.cs:18` —
메뉴 `CinderCourt / Dev / Unlock All Stages (maxed meta)`.
단, 이 메뉴가 심는 roster는 `["ember-cohort","shade-echo"]` **2명뿐이고
`activeSlots`가 없다**(L21-24). 3명 촬영에는 부족하므로, 실행 후 로비 군단
탭에서 세 번째를 직접 눌러 채우거나 방법 A를 쓴다.
`[OBSERVED]` 이 어셈블리는 에디터 전용이라 배포본에는 없다(L11).

## 3.3 딥링크 (스테이지 바로 진입)

`[OBSERVED]` `GameDirector.cs:104-121`. **WebGL 빌드에서만 동작**한다
(`WebGLStorage.QueryParam`은 에디터에서 항상 `""`, `WebGLStorage.cs:44-51`).

| URL | 동작 |
|---|---|
| `?mode=campaign&stage=cinder-span` | 해당 스테이지 즉시 시작 — **단, 그 스테이지가 해금돼 있어야 한다**(`IsStageUnlocked`) |
| `?mode=arena` | 아레나 모드 (키 배치 다름, 영상에는 쓰지 말 것) |
| `?mode=training` | 훈련장 |
| `?intro=off` | 인트로 릴 스킵 |

`[OBSERVED]` `mode` 파라미터가 있으면 인트로는 자동으로 스킵된다(L110-111).
즉 `?mode=campaign&stage=…`만 붙여도 인트로는 안 나온다.

**촬영용 최종 URL 후보:**

```
https://akillness.github.io/hongT/?intro=off
```

→ 로비부터 보여주는 안(3.1 주입 후). 로비 군단 탭 3슬롯이 화면에 잡히므로
"동료 3명"의 근거를 영상 안에서 스스로 증명한다. **이 안을 권장한다.**

## 3.4 보스까지 걸리는 시간

`[OBSERVED]` `Assets/Scripts/Sim/CampaignTypes.cs:129`, `L366-400`:
웨이브 `1..N`이 끝나면 웨이브 `N+1`이 보스 웨이브다.

| 스테이지 | 일반 웨이브 수 | 보스 |
|---|---|---|
| 재의 다리 (cinder-span) | **5** | Cinder Warden |
| 서약의 성당 (abyss-chancel) | 6 | Veil Tactician |
| 메아리 왕좌 (echo-throne) | 7 | Gate Sovereign |
| 재의 수문 / 불씨 요새 | 8 | Sluice Keeper / Bastion Sentinel |
| 재의 행진 (ash-march) | 9 | Ash Magistrate |

→ `[INFERENCE]` **cinder-span이 보스까지 가장 짧다.** 그래도 실제 플레이는 수 분이
걸리므로, **한 세션을 통으로 녹화한 뒤 컷 편집으로 60초 이내로 줄인다.** 배속은
쓰지 않는다(§0).

---

# 4. 샷 리스트 (목표 58초)

전제: §3.1 세이브 주입 완료, `?intro=off`로 로비 진입, cinder-span 출정.
한 번의 연속 플레이를 녹화하고 아래 5개 구간만 남겨 잇는다. 컷 사이는 하드컷,
전환 효과 없음.

### 0:00 – 0:06 · 로비 / 동료 3명 (6초)

- **보이는 것**: 로비 **군단 탭**. `잿불 사도` · `그림자 메아리` · `홀린 자
  메아리` 세 줄이 금색(활성)으로 켜져 있다.
- **입력**: 군단 탭 클릭 → 출정 버튼 클릭.
- **왜**: 이 영상의 전제인 "동료 3명"을 UI로 먼저 증명한다. 나중에 필드에서
  세 마리가 따라다니는 장면만 보여주면 심사자는 그게 동료인지 적인지 모른다.
- **자막(선택)**: `동료 3인 편성 · 최대 슬롯 3`

### 0:06 – 0:16 · 기본 전투 + 스킬 (10초)

- **보이는 것**: 던전 진입 직후. 워든과 동료 3명이 함께 이동, 적 무리와 교전.
  HUD의 스킬 아이콘(Q/E/R/F)과 기름 게이지가 프레임 안에 들어와야 한다.
- **입력**: `W`/`A`/`S`/`D` 이동 → `Space` 유지(일반 공격) → `Q`(볼트) →
  `Shift`(대시로 파고들기) → `E`(펄스).
- **왜**: 텍스트 커맨드가 "키 입력을 대체하는 또 하나의 경로"라는 점을 뒤에서
  보여주려면, 먼저 **키 입력이 원래 어떻게 생겼는지**를 보여줘야 한다.
- **주의**: `R`은 여기서 쓰지 않는다. 노바는 0:36 구간에 아껴 둔다.
- **자막(선택)**: `WASD 이동 · Space 공격 · Q/E/R/F 스킬 · Shift 대시`

### 0:16 – 0:26 · 텍스트 커맨드 ① 다단계 시퀀스 (10초)

- **보이는 것**: `Enter`를 누르는 순간 화면이 0.2배속으로 느려지고 하단에 입력창이
  열린다. 한국어 문장이 한 글자씩 찍힌다. 제출하면 `시퀀스 2단계` 토스트가 뜨고,
  노바 → 결계가 차례로 터진다.
- **입력**: `Enter` → `노바 쓰고 결계 쳐` 타이핑 → `Enter`.
- **왜**: 이 게임의 AI 상호작용 핵심. **한 문장이 순서 있는 다단계 계획으로
  분해된다**는 것이 한 컷에 다 보인다. 슬로 모션도 게임 기능이라 별도 설명 없이
  "입력할 시간을 준다"는 설계 의도가 전달된다.
- **주의**: 한글 IME가 켜져 있어야 한다(§5).
- **자막(선택)**: `한 문장 → 순서 있는 계획 (최대 6단계)`

### 0:26 – 0:36 · 텍스트 커맨드 ② 조건 예약 (10초)

- **보이는 것**: 다시 콘솔을 열고 조건부 문장 입력. 제출 후 HUD 대기열 패널에
  `보스 등장 • 동료 특기`가 뜬 채로 **전투가 계속된다**(계획은 콘솔을 닫아도 살아
  있다). 그동안 웨이브를 계속 정리하는 화면.
- **입력**: `Enter` → `보스 나오면 특기` 타이핑 → `Enter` → 이어서 `Space` 유지로
  교전 지속.
- **왜**: 단순 명령이 아니라 **게임 이벤트에 걸리는 예약**이라는 걸 보여준다.
  대기열 패널에 조건이 문자로 남아 있어 다음 컷의 발동이 우연이 아님을 증명한다.
- **자막(선택)**: `게임 이벤트에 명령을 예약`

### 0:36 – 0:52 · 보스전 (16초)

- **보이는 것**: 보스 등장 연출 → **예약해 둔 명령이 스스로 발동**하여 동료 3명이
  시그니처 스킬을 쓴다 → 플레이어가 `R`(노바)·`F`(결계)로 마무리 → 보스 HP바가
  0으로.
- **입력**: (예약 자동 발동) → `Space` 유지 → `R` → `F` → `Shift`로 회피 →
  `Space` 마무리.
- **왜**: 이 영상의 클라이맥스이자, 앞의 두 커맨드 컷이 실제 전투에 영향을 준다는
  증거. 동료·스킬·텍스트 커맨드 세 축이 한 장면에서 만난다.
- **주의**: 보스 HP바와 대기열 패널이 동시에 프레임에 있어야 발동 인과가 읽힌다.
- **자막(선택)**: `보스 등장 → 예약 명령 자동 발동`

### 0:52 – 0:58 · 클리어 (6초)

- **보이는 것**: 스테이지 클리어 결과 화면(점수·유물·처치 수).
- **입력**: 없음.
- **왜**: 루프가 닫혔음을 보여주고 영상을 끝낸다.
- **자막(선택)**: 게임 타이틀 + `https://akillness.github.io/hongT/`

**합계 58초** (규정 30~60초 내). `[TARGET]`

## 4.1 컷이 안 나올 때의 대체안

- 보스까지 못 버티면: 장비/스탯을 최대로 심은 세이브(§3.1은 이미 최대)로 다시
  하거나, 0:36 구간을 별도 세션에서 다시 따서 잇는다. 컷 편집이므로 문제없다.
- 예약 명령이 보스 등장 전에 다른 이유로 소모되면: `취소`를 입력해 대기열을 비우고
  다시 예약한다(§2.4).
- 텍스트 입력이 너무 길어 컷이 늘어지면: `노바 쓰고 결계 쳐` → `노바 쓰고 결계`로
  줄여도 동일하게 파싱된다(`결계`가 키워드).

---

# 5. 녹화 세팅

- **브라우저**: Chrome 최신. 확대 100%. `F11` 전체화면으로 주소창·북마크 제거.
- **해상도**: `[INFERENCE]` 1920×1080 권장(YouTube 기본). 기존 캡처는 1440×900로
  잡혀 있었다(`tools/video/capture-unity-play.mjs:68`) — 새 테이크는 1080p로 올린다.
- **프레임레이트**: 60fps로 녹화, 60fps로 업로드.
- **녹화 도구**: macOS 화면 기록 또는 OBS. **게임 오디오를 함께 캡처**한다
  (효과음·BGM은 직접 만든 자산이다).
- **음소거 상태 확인**: `[OBSERVED]` 음소거는 localStorage
  `abyssal-lantern:cinder-court:muted`에 저장된다(`AudioDirector.cs:21`). 이전
  세션에서 껐다면 켜고 시작할 것.
- **한글 IME**: `[OBSERVED]` 브라우저 IME는 전용 브리지로 처리된다
  (`Assets/Plugins/WebGL/hangul_ime.jslib`, `CommandConsoleImeComposition.cs`).
  녹화 전에 콘솔을 한 번 열어 한글이 제대로 들어가는지 반드시 확인한다.
- **첫 로딩**: WebGL 빌드 초기 로딩은 영상에 넣지 않는다. 로딩 완료 후 녹화 시작.
- **커서**: 마우스 커서가 전투 화면을 가리지 않게 화면 밖으로 치워 둔다.

---

# 6. 편집 · 업로드

1. 녹화 원본(무편집)을 보관한다. 심사 문의 시 원본이 근거가 된다.
2. §4의 5개 구간만 하드컷으로 잇는다. **전환 효과·배속·색보정·합성 금지.**
3. 자막은 넣어도 되지만 화면 하단 안전 영역에만, 게임 UI를 가리지 않게.
4. 길이가 60초를 넘으면 0:06/0:26 구간을 먼저 줄인다. 30초 미만이 되지 않게 주의.
5. YouTube 업로드: **공개 또는 일부 공개(링크 공유)**. 제목에 게임 제목과 팀명.
   설명란에 플레이 링크와 저장소 링크.
6. 확정된 링크를 `docs/submission-nhn/00-submission-index.md`의 제출물 2 행과
   제출물 3(게임 소개 문서)의 "플레이 영상 링크" 항목에 반영한다.

---

# 7. 촬영 담당자 체크리스트

- [ ] localStorage에 §3.1 JSON 주입 완료, 로비 군단 탭에 3명 금색 확인
- [ ] `?intro=off`로 접속, 인트로 스킵 확인
- [ ] 콘솔에서 한글 입력 정상 확인 (`Enter` → 아무 한글 → `Esc`)
- [ ] 오디오 음소거 해제 확인
- [ ] 1080p / 60fps / 전체화면 / 커서 치움
- [ ] cinder-span 출정 → 보스까지 완주 (한 세션 통 녹화)
- [ ] §4의 5개 구간이 모두 원본에 담겼는지 확인 후 편집 시작
