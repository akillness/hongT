# Cinder Court — 씬 시놉시스 (영상형 콘티)

> 게임 내 실제 데이터(`StoryCatalog.cs`, `StageCatalog.cs`, `CameraRig.cs`,
> `GameDirector.cs`, `HudView`)를 근거로 작성한 **영상형 시놉시스**다. 모든
> 대사는 `StoryCatalog.cs`에서 **원문 그대로(frozen)** 인용하며, 카메라·연출은
> 실제 구현된 `CameraRig.Profile`·`FocusPulse`·`Shake`·`ShowBossIntro` 기능만
> 사용한다. 표기: `[OBSERVED]` = 코드에서 확인, `[TARGET]` = 연출 의도.
>
> - 아레나 1536×1024, 중심 (768,604), 반경 520×270 (수치 계약 §2.3)
> - 스테이지 6종·보스 3종(Cinder Warden / Veil Tactician / Gate Sovereign)
> - 캠페인 체인: `1 → 1+2(boss) → 2 → 2+3(boss) → 3 → 1+3(final)`
>
> **구성 이미지**(god-tibo-imagen 생성, `docs/nan2026/assets/scenes/`):
>
> | 씬 | 구성 이미지 |
> |---|---|
> | 인트로 | ![intro](assets/scenes/01-intro.png) |
> | 스테이지 입장 | ![stage-entry](assets/scenes/02-stage-entry.png) |
> | 이동 전이 | ![transition](assets/scenes/03-transition.png) |
> | 보스 등장 | ![boss-entry](assets/scenes/04-boss-entry.png) |
>
> **런타임 적용** `[OBSERVED]`: 이 4장은 `Assets/Resources/Scenes/`에 스프라이트로
> 복사되어 `CutsceneView`가 로딩 컷씬으로 표시한다. `GameDirector.StartPrologue`는
> `scene-intro`("PROLOGUE / 잿불의 법정")를, `StartDungeon`은 컨텍스트별로
> `scene-transition`(Ember Rest 이어가기) / `scene-boss-entry`(BossMonarch 스테이지) /
> `scene-stage-entry`(그 외)를 골라 `entry.Kicker`·`entry.Title`과 frozen
> `StoryCatalog` stageStart 내레이션을 캡션으로 얹는다. 로비 복귀 시 `_cutscene.Hide()`.
>
> **부트 브랜드 영상** `[OBSERVED]`: 엔진 기본 로딩 화면은 게임 컨셉·브랜드
> 인트로 영상으로 대체됐다. `god-tibo-imagen`으로 생성한 6장
> (`_workspace/current/design/intro-video/frames/`)을 ffmpeg Ken-Burns +
> 크로스페이드로 이어 붙인 7.8 s H.264 1280×720 무음 클립이며, 마지막 홀드에
> `ABYSSAL LANTERN` / `잿불의 법정을 지켜라` 타이틀 록업을 `HudKorean.otf`로
> 구웠다. 산출물은 `Assets/StreamingAssets/Video/cinder-court-intro.mp4`,
> 재생은 `IntroVideoView`(ScreenSpaceOverlay, sortingOrder 520 — `CutsceneView`
> 500 위)가 담당한다. 기본 부트 경로에서만 재생하고 QA 딥링크(`?mode=...`)와
> `?intro=off`에서는 건너뛴다. 아무 키·탭으로 스킵 가능하며, 4 s 준비 타임아웃과
> 20 s 워치독이 있어 브라우저가 클립을 디코드하지 못해도 부트를 막지 않는다.
> 스토리 비트는 `_workspace/current/design/intro-video/scenario.md`,
> 생성·조립 근거는 `docs/provenance/intro-video.json`.


---

## 1. 인트로씬 — 등불을 든 자의 첫 강하

**목적**: 2D 격투기 프레임에서 2.5D 던전 원근으로 전환되는 세계관 개막.

| 컷 | 카메라 | 화면 | 사운드/사운드감 |
|---|---|---|---|
| 1 | `Profile.Prologue` — 26° 측면, 직교 투영 (`orthographicSize`), 재의 궁정 발판 위 | 워든이 court plate 위에 홀로 선다. 잿불이 수직 패럴랙스로 흩날린다. | 저음 앰비언스, 사슬 삐걱임 |
| 2 | 정지 프레임. 튜토리얼 토스트 진행(`AdvancePrologueToast`): 이동→타격→기름 게이지→웨이브 | 플레이어가 3-웨이브 튜토리얼을 소화한다(`HackConfig.Prologue`, `PrologueWaves`). | 타격 임팩트, 기름 차오르는 소리 |
| 3 | **리빌 스윕** `Profile.PrologueReveal` — 2.2 s 동안 26°→55°, 거리 9.4→17 이징(`1-(1-t)³`) | 측면 2D 프레임이 부드럽게 궁정 전경을 향해 기울며 3D 아레나가 열린다. 팝 없는 블렌드다운(FOV 42°). | 상승하는 스트링, 공간감 확장 |
| 4 | `_revealReturnTimer` 2.6 s 만료 → `EnterLobby()` | 로비로 안착. 성장/장비/군단 탭과 9단계 출정 카드가 드러난다. | 로비 테마 진입 |

**나레이션 톤** `[TARGET]`: "등불은 문을 여는 열쇠가 아니라, 문이 다시 올라오지
못하게 붙드는 무게였다." — 캠페인 완주 시 DUSK WARDEN 회고(`cinder-span`
Completion, l.50)와 수미상관을 이루도록 인트로에 복선만 심는다.

---

## 2. 스테이지 입장씬 — 감시자의 하강 자막

**공통 문법** `[OBSERVED]`: 강하 시 `Profile.Dungeon`(55° 원근, 거리 17)로
진입하고, 감시자(`Watcher`)의 `StageStart` 나레이션이 월드 스페이스
`SpeechBubbleView.Show(speaker, text, worldAnchor)` 캡션으로 뜬다. 스테이지
`AccentColor`가 조명 틴트를 물들인다.

| 스테이지 | AccentColor | 감시자 나레이션 (원문) |
|---|---|---|
| 재의 다리 (Cinder Span) | `(0.95, 0.35, 0.17)` 잿불 주황 | 서쪽 불씨를 버티고 사슬의 진실을 확인하세요. |
| 불씨 회랑 (Ember Gallery) | `(0.95, 0.43, 0.20)` 밝은 불씨 | 불씨가 늘어선 회랑을 지나, 같은 사슬의 다른 매듭을 찾으세요. |
| 서약의 성당 (Abyss Chancel) | `(0.56, 0.40, 1.0)` 서약의 보라 | 거울이 먼저 내놓은 답을 거부하세요. |
| 증언의 우물 (Witness Well) | `(0.22, 0.76, 0.66)` 우물의 옥빛 | 증언의 우물은 대답보다 먼저, 무엇을 잊었는지 묻습니다. |
| 메아리 왕좌 (Echo Throne) | `(0.45, 0.78, 1.0)` 왕좌의 청보라 | 빈 왕좌보다 오래 남은 명령을 끓으세요. |
| 재의 판결 (Ash Verdict) | `(0.87, 0.78, 0.41)` 판결의 재금색 | 재의 판결 앞에서, 왕좌가 남긴 명령의 무게를 견디세요. |

**컷 구성** `[TARGET]`:
1. 강하 직후 카메라가 `AccentColor` 틴트 아래 텅 빈 아레나를 잡는다.
2. 감시자 자막이 페이드 인 — 목소리 없는 낮은 속삭임, 화면 하단 캡션.
3. `TerrainId` 드레싱(다리/회랑/성당/우물/단상)이 프레임을 채우고 첫 웨이브가
   경계에서 스폰된다. `SetDungeonCrowd(bigWave)`로 군중이 많으면 카메라가
   거리 21로 물러난다 `[OBSERVED]`.

---

## 3. 스테이지 간 이동씬 — 사슬을 따라가는 전이

**세계관 근거**: 체인 모티프 `1 → 1+2 → 2 → 2+3 → 3 → 1+3`. 이동씬은 클리어
직후 DUSK WARDEN/VEIL TACTICIAN의 `Completion` 회고(월드 스페이스 버블)로
다음 매듭을 예고한다. `NextStageId`(StageEntry) 링크가 실제 전이 대상이다
`[OBSERVED]`.

### 3.1 재의 다리 → 불씨 회랑
- **회고 (DUSK WARDEN)**: "그는 문을 지킨 게 아니었다. 문이 올라오지 못하게 묶고 있었다."
- **전이 연출** `[TARGET]`: 무너지려는 다리 위로 카메라가 서쪽 불씨 행렬을 훑고,
  불씨 회랑의 밝은 주황(`0.95,0.43,0.20`)으로 틴트가 크로스페이드.
- **보상**: Cinder Span 클리어 시 `ember-cohort` 군단 합류.

### 3.2 불씨 회랑 → 서약의 성당
- **회고 (DUSK WARDEN)**: "불씨들은 길을 비췄다. 이제 어느 문이 진짜인지 골라야 한다."
- **전이 연출** `[TARGET]`: 불씨 주황이 서약의 보라(`0.56,0.40,1.0`)로 식으며,
  거울 파사드가 프레임을 반사로 채운다. 선택의 갈림길 암시.

### 3.3 서약의 성당 → 증언의 우물
- **회고 (VEIL TACTICIAN)**: "그렇다면 왕좌도 너를 분류하지 못하겠군."
- **전이 연출** `[TARGET]`: 깨진 거울 파편 사이로 카메라가 하강, 우물의
  옥빛(`0.22,0.76,0.66`) 수면 반사가 서서히 드러난다.
- **보상**: Abyss Chancel 클리어 시 `shade-echo` 합류.

### 3.4 증언의 우물 → 메아리 왕좌
- **회고 (DUSK WARDEN)**: "우물은 잠잠해졌다. 남은 목소리는 네 선택을 기다린다."
- **전이 연출** `[TARGET]`: 잔잔해진 수면에서 카메라가 상승, 빈 왕좌의
  청보라 단상이 원경에서 다가온다.

### 3.5 메아리 왕좌 → 재의 판결 (최종)
- **회고 (DUSK WARDEN)**: "왕좌는 비었다. 그런데 명령은 내 등불 안에서 계속된다."
- **전이 연출** `[TARGET]`: 청보라가 판결의 재금색(`0.87,0.78,0.41`)으로
  타들어가며, 체인 최종 결선(`1+3`)의 이중 지형이 겹쳐 나타난다.
- **보상**: Echo Throne 클리어 시 `possessed-echo` 합류.

---

## 4. 보스 등장씬 — 세 수호자의 대면

**공통 문법** `[OBSERVED]`:
- `HudView.ShowBossIntro(bossName)`이 `— {HudName} —` 플레이트를 3.5 s 페이드로
  띄운다(`BossNameFor` → `StageCatalog...Boss.HudName`).
- `CameraRig.FocusPulse(bossAnchor, 0.45f)`가 보스 앵커로 0.45 s 포커스 펄스.
- 보스 페이즈2 진입 시 `SimEvents.BossPhase2` → `Shake(0.3f, 0.09f)` 강한 흔들림.
- 보스 `Tint`/`Scale`이 등장 스케일과 색을 결정한다.

### 4.1 Cinder Warden (재의 다리 / 불씨 회랑)
- **비주얼**: `BossCommander` / `shadow-commander-boss`. 재의 다리 Tint
  `(0.9,0.3,0.45)` Scale 1.0 → 불씨 회랑 Tint `(0.95,0.45,0.16)` Scale 1.08.
- **등장 (BossEntry)**:
  - 재의 다리: "등불을 내려라. 네가 찾는 길은 내 사슬 아래서 끝난다."
  - 불씨 회랑: "불꽃이 늘었다고 길이 늘어난 것은 아니다."
- **페이즈2 (BossPhase2)** — 카메라 강타 동반:
  - 재의 다리: "봉인을 풀면 길이 열리는 게 아니다. 네 뒤의 다리가 먼저 무너진다."
  - 불씨 회랑: "회랑 끝의 재는 네 발자국을 모두 기억한다."
- **콘티** `[TARGET]`: 사슬이 발판을 조이며 커맨더가 강림. 페이즈2에서 다리가
  실제로 흔들리듯 `Shake(0.3,0.09)`가 프레임을 요동시킨다.

### 4.2 Veil Tactician (서약의 성당 / 증언의 우물)
- **비주얼**: `BossCommander` / `shadow-commander-boss`. 성당 Tint
  `(0.56,0.40,1.0)` Scale 1.1 → 우물 Tint `(0.22,0.76,0.66)` Scale 1.12.
- **등장 (BossEntry)**:
  - 서약의 성당: "또 같은 등불, 또 같은 서약."
  - 증언의 우물: "우물은 거짓말하지 않는다. 다만 전부 말하지 않을 뿐이지."
- **페이즈2 (BossPhase2)**:
  - 서약의 성당: "거울이 깨져도, 왕좌가 사라지는 것은 아니다."
  - 증언의 우물: "네가 들은 증언은 아직 결말을 고르지 못했다."
- **콘티** `[TARGET]`: 거울 다중 반사 속 진짜 하나가 앞으로 걸어 나온다.
  페이즈2에서 반사상들이 동시에 갈라지며 카메라가 진짜를 `FocusPulse`로 고정.

### 4.3 Gate Sovereign (메아리 왕좌 / 재의 판결)
- **비주얼**: `BossMonarch` / `broken-court-monarch-boss`. 왕좌 Tint
  `(0.75,0.3,0.9)` Scale 1.15 → 판결 Tint `(0.87,0.78,0.41)` Scale 1.18 (최대 위압).
- **등장 (BossEntry)**:
  - 메아리 왕좌: "마침내 내가 놓았던 등불을 네가 들고 왔다."
  - 재의 판결: "판결은 끝났다. 남은 것은 네가 복종할 차례다."
- **페이즈2 (BossPhase2)**:
  - 메아리 왕좌: "단상을 차지해도 왕좌의 명령은 너에게 돌아온다."
  - 재의 판결: "재가 되어도 명령은 사라지지 않는다."
- **완주 (DUSK WARDEN, ash-verdict Completion)**: "판결은 끝났다. 이제 등불은 네
  손에서 다른 길을 밝힌다." — 인트로 복선을 회수하는 최종 대사.
- **콘티** `[TARGET]`: 가장 큰 Scale로 강림, 재금색 판결광이 아레나를 압도.
  최종 결선(`1+3` 이중 지형)에서 왕좌가 무너지며 등불이 화면을 밝히고 페이드아웃.

---

## 부록 — 연출 자산 대응표 (`[OBSERVED]`)

| 연출 요소 | 코드 소스 |
|---|---|
| 인트로 측면 2D 프레임 | `CameraRig.Profile.Prologue` (26°, ortho) |
| 인트로 2.5D 리빌 스윕 | `CameraRig.Profile.PrologueReveal` (2.2 s, 26°→55°) |
| 리빌 후 로비 안착 | `GameDirector._revealReturnTimer` 2.6 s → `EnterLobby()` |
| 튜토리얼 진행 | `GameDirector.AdvancePrologueToast` (이동/타격/기름/웨이브) |
| 던전 카메라 | `CameraRig.Profile.Dungeon` (55°, 거리 17, 대군중 21) |
| 나레이션·대사 버블 | `SpeechBubbleView.Show(speaker, text, worldAnchor)` |
| 보스 네임 플레이트 | `HudView.ShowBossIntro` (`— {HudName} —`, 3.5 s) |
| 보스 포커스 펄스 | `CameraRig.FocusPulse(bossAnchor, 0.45f)` |
| 페이즈2 카메라 강타 | `CameraRig.OnEvents` → `Shake(0.3f, 0.09f)` |
| 스토리 대사 원문 | `StoryCatalog.cs` (frozen — 편집 금지) |
| 스테이지 색/보스/보상 | `StageCatalog.cs` StageEntry / BossPresentation |
