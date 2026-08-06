# 훈련(프롤로그) 스테이지 이동/공격 모션 — Unity MCP 플레이모드 검증

- 실행 저장소: `~/orca/workspaces/HongT/main` (복사본 아님, 열려 있는 에디터에 직접 접속)
- 접속 경로: `com.ivanmurzak.unity.mcp` 0.87.0 플러그인 ↔ `gamedev-mcp-server` 9.2.5,
  `http://localhost:29280/mcp` (streamableHttp, auth=none)
- 사용 툴: `editor-application-get-state`/`set-state`, `script-execute`,
  `console-get-logs`, `console-clear-logs`, `assets-refresh`,
  `screenshot-game-view`, `tests-run`
- 측정일: 2026-02-04

---

## 0. 재현 방법

`script-execute`로 `GameDirector.StartPrologue()`(private)를 리플렉션 호출해
훈련 스테이지에 진입시키고, `InputAdapter.TouchMoveX`(이동) 및
`InputAdapter.QueueAttack()`(공격)로 입력을 주입한 뒤, 매 프레임
`CinderSim`의 `PlayerState.Action`과 `Animator`의 `action` 파라미터·현재 클립·
`normalizedTime`을 같이 찍었다. 심(원인)과 애니메이터(결과)를 한 줄에 놓는 것이
핵심이다 — 둘 중 어디서 끊기는지는 로그를 보기 전에는 알 수 없었다.

---

## 1. [OBSERVED] 결함 A — 걸으면 피격 모션이 나온다

수정 전 프롤로그 로그(발췌):

```
[ANIM-WALK] i=3  sim=Move at=0.48 moving=True param=4   ← bighit
[ANIM-WALK] i=6  sim=Move at=0.95 moving=True param=4
[ANIM-WALK] i=12 sim=Move at=1.87 moving=True param=4
```

심은 1.9초 내내 `Move`를 내보내는데 애니메이터에는 `4`(=`bighit`,
Receive Uppercut To The Face)가 들어가 있었다. 즉 **걷는 동안 계속
"얻어맞는" 클립이 재생**됐다.

원인은 `ActorView.SyncPlayer`의 넉백 추론이다. 심은 보스 슬램 넉백 플래그를
공개하지 않으므로(`PlayerState`는 FROZEN) 뷰가 **속도**로 추론하는데,
그 나눗셈의 분모가 `Time.deltaTime`이었다:

```csharp
var speed = step / Time.deltaTime;      // 잘못된 분모
if (speed > 400f && speed < 1500f) _knockbackTime = ...;
```

위치 스텝은 고정 1/60 s 틱이 만든다. `GameView.Update`는 고정스텝보다 짧은
프레임에서 틱을 0회 또는 1회만 돌린다. 따라서 렌더 델타로 나누면 몫이
`(1/60)/deltaTime` 배 부풀고, **120 fps에서 218 px/s 걷기가 436 px/s로 읽혀
400–1500 밴드 안에 들어간다.** 프레임레이트가 높을수록 확실하게 터지는 버그다.

[OBSERVED] 수정 후 같은 구간:

```
[ANIM-WALK] i=3  sim=Move param=1 spd=1.00 clip=move w=1.00 nt=0.34
[ANIM-WALK] i=9  sim=Move param=1 spd=1.00 clip=move w=1.00 nt=1.00
[ANIM-WALK] i=18 sim=Move param=1 spd=1.00 clip=move w=1.00 nt=2.02
```

`nt`가 0.34 → 1.00 → 2.02로 진행 = Walking 루프가 실제로 돌고 있다.

**수정**: `GameView`가 이번 프레임에 실제로 진행한 심 시간
(`steps × SimConfig.FixedStep`)을 `SyncPlayer`/`SyncEnemy`에 넘기고, 속도는
그 값으로 나눈다. 적 쪽(300 px/s 게이트)도 같은 결함을 갖고 있어 같이 고쳤다.

---

## 2. [OBSERVED] 결함 B — 공격해도 주먹/무기를 휘두르지 않는다

수정 전:

```
[ANIM-ATK] sim=Attack param=5 clip=attack nt=0.10
[ANIM-ATK] sim=Attack param=5 clip=attack nt=0.23
[ANIM-ATK] sim=Attack param=5 clip=attack nt=0.35   ← 여기서 Idle로 복귀
```

애니메이터 값은 정상(5)인데 **클립이 항상 10~35%에서 잘렸다.** 심은 공격
포즈를 고정 창(아레나/프롤로그 5프레임 @ 12 fps = 0.417 s, 던전은
`HackSpec.ComboSwing` = 0.30/0.30/0.42 s)만 유지하고 즉시 내려놓는데,
mixamo 원본 클립은 2.4 s짜리다. 속도 1로는 준비동작만 나오고 타격 구간에
도달하기 전에 포즈가 회수된다 — 사용자가 본 "휘두르지 않는다"가 이것이다.

**수정 두 갈래:**

1. **클립 교체** — `attack`을 `Punching`(직선 잽)에서
   `Standing Melee Attack Horizontal`(수평으로 휘두르는 동작)로 바꿨다.
   `CharacterImportPipeline.Clips`의 attack 행만 변경, 행 순서는 그대로라
   `ClipTableTests`의 인덱스 정렬 계약은 영향 없다.

2. **클립 트리밍 + 시간 스케일** — 스윙 구간을 실측해서 잘라냈다.
   `AnimationClip.SampleAnimation`으로 클립 전체를 훑고 힙 기준 오른손 속도를
   재면(24 fps 소스):

   | 구간 | 프레임 | 내용 |
   |---|---|---|
   | 0–17 | 스탠스 | 손 속도 0.3–1.5 u/s |
   | 18–20 | 준비 | 가속 시작 |
   | **21–25** | **타격** | 최대 6.9 u/s (f24) |
   | 27– | 회수 | 감속·복귀 |

   → `ClipTrims`에 `("attack", 16, 28)`을 추가해 12프레임(0.5 s)만 임포트한다.
   `ActorView`는 클립 길이/스윙 창으로 애니메이터 속도를 정하므로
   0.5/0.4167 = **1.2배**로 재생되고, 타격 프레임이 포즈 시작 후
   0.17–0.31 s에 놓인다. 이는 심의 실제 판정 창
   (`SimConfig.AttackActiveFrom/To` = 0.167–0.333 s)과 겹친다 —
   보이는 타격과 판정되는 타격이 같은 시점이 됐다.

[OBSERVED] 수정 후 (프롤로그, 공격 연타):

```
[ANIM-ATK] sim=Attack param=5 spd=1.20 clip=attack w=1.00 nt=0.23
[ANIM-ATK] sim=Attack param=5 spd=1.20 clip=attack w=1.00 nt=0.42
[ANIM-ATK] sim=Attack param=5 spd=1.20 clip=attack w=1.00 nt=0.55
[ANIM-ATK] sim=Attack param=5 spd=1.20 clip=attack w=1.00 nt=0.72
[ANIM-ATK] sim=Attack param=5 spd=1.20 clip=attack w=1.00 nt=0.84
[ANIM-ATK] sim=Idle             spd=1.00 clip=attack w=0.68 nt=1.00
```

`nt`가 1.00까지 도달한 뒤 심이 포즈를 내려놓는다. 스윙 전체가 재생된다.

게임 뷰 스크린샷(캡처 시각의 애니메이터 상태를 함께 기록):

| 파일 | 캡처 시점 상태 |
|---|---|
| `attack-nt022-windup.png` | clip=attack nt=0.22 |
| `attack-nt053-contact.png` | clip=attack nt=0.53 |
| `attack-nt078-followthrough.png` | clip=attack nt=0.78 |
| `idle-between-swings.png` | clip=idle nt=0.07 |

12장 연속 캡처 전부 md5가 서로 달랐다(정지 화면이 아님).

---

## 3. 회귀 테스트

`Assets/Tests/EditMode/SwingPacingTests.cs` (신규 15케이스):

- 이동은 **어떤 프레임레이트에서도** 넉백 창을 열지 않는다(1틱 프레임 /
  4틱 배치 프레임 양쪽), 반대로 실제 슬램(577 px/s)은 연다.
- 틱이 0회인 프레임은 속도를 추론하지 않는다(0으로 나누지 않는다).
- 대시(864 px/s)는 `Avoid`가 포즈를 소유하므로 제외된다.
- 적: 추격(128 px/s) 중 피격은 넉백이 아니고, 콤보 런치(667 px/s)는 넉백이다.
- `ArenaSwingSeconds`가 **실제 `CinderSim`을 돌려 측정한** 공격 포즈 유지
  시간과 같다. (`AttackClipFrames`/`AttackClipFps`는 심의 private 상수라
  뷰가 미러링하고 있고, 이 테스트가 그 미러를 실측으로 고정한다.)
- 던전 티어별 창은 `HackSpec.ComboSwing[tier]`, 범위를 넘는 티어는 클램프.
- `PoseSpeed`는 클립을 창에 정확히 맞추고(1.0 s / 0.4167 s = 2.4배),
  레일(0.5–4.0)을 벗어나지 않으며, 항이 0이면 원본 속도로 폴백한다.
- 스윙만 시간 스케일 대상이다 — idle/move/run/hit/bighit/avoid/defence/die/
  show/cast는 전부 -1(스케일 안 함).

[OBSERVED] `tests-run` EditMode **290/290 통과**.

**변이 검증**: `SyncPlayer`의 분모를 `Time.deltaTime`으로 되돌리면
`SlamStep_OpensTheLaunchWindow`만 정확히 실패(289/290)하고, 되돌리면 다시
전부 통과한다. 테스트가 실제로 이 수정을 잡고 있다는 증거다.

---

## 4. 운영 주의 [OBSERVED]

`CharacterImportPipeline.BuildController()`는 컨트롤러 에셋을
`DeleteAsset` 후 재생성한다. guid는 보존되지만(`f7c49ca6…`, 프리팹 참조
8개 모두 무손상) **에디터가 이미 로드해 둔 인메모리 참조는 끊긴다.**
클립 재임포트 직후 플레이하면 `Animator.runtimeAnimatorController = NULL`,
`layerCount = 0`으로 관측됐다. `AssetDatabase.ImportAsset`으로
`Assets/Resources/Characters`를 강제 재임포트하면 복구된다
(재확인: `ctrl=CinderActor`). 배치 임포트 후에는 반드시 리임포트/도메인
리로드를 한 번 거친 뒤 플레이 검증할 것.

---

## 5. 미검증 [TARGET] — 이후 해소됨

- ~~WebGL 브라우저 경로~~ → 해소. `7343cd0`를 WebGL로 빌드해 gh-pages
  `ce76295`(캐시 버전 `a78283f49ff7e483`)로 배포하고, 로컬 무압축 서빙과
  라이브 <https://akillness.github.io/hongT/> 양쪽에서 헤드리스 Chromium
  부팅을 확인했다: 요청 실패 0, 콘솔 에러 0, 페이지 예외 0, 진행률 100 %,
  webgl2 컨텍스트, 인트로 릴 스트리밍. 상세는
  `qa/deployed-release-verification.md`의 2026-08-06 사이클.
- ~~새 FBX 1개(261 KB) 추가에 따른 빌드 용량 영향(≤120 MB 상한)~~ →
  해소. 배포 산출물 47.2 MB(data 36,196,342 B · wasm 10,472,622 B),
  상한의 39 %.
- `Punching.fbx`는 이제 어떤 행에서도 참조되지 않는다. 빌드에는 포함되지
  않지만(Resources 밖) 저장소에는 남겨 뒀다 — 되돌릴 때의 참조용.
