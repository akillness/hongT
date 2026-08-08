---
title: "AI 활용 기술 문서"
subtitle: "제출물 4 · AI 활용 기술 문서 · Abyssal Lantern — Hold the Cinder Court (Unity 재구현)"
author: "Hong팀 · 정장영 · 이석민 · 정우영"
lang: ko
---

# 0. 요약

이 게임은 **Unity 6000.5.6f1 / URP 17.5 / WebGL** 빌드이며, AI는 보조 도구가
아니라 **제작 파이프라인 그 자체**입니다. 3D 캐릭터 메시, 리스킨과 애니메이션
리타겟, 효과음과 BGM, UI 아이콘과 HUD 아틀라스, 스테이지 지형 텍스처, 인트로
영상, 게임 코드와 검증 하니스, 제출용 플레이 영상 캡처까지 전부 AI 워크플로
위에서 만들어졌습니다.

다만 **배포된 게임 런타임에는 기본적으로 AI가 실행되지 않습니다.** GitHub Pages에
올라간 빌드는 정적 WebGL 페이지이며 외부 추론 호출·API 키·네트워크 요청이
없습니다. AI 산출물은 제작 시점에 결정론적 자산(png / mp3 / fbx / otf / mp4)으로
고정되어 저장소에 커밋됩니다. 심사자가 링크를 열었을 때 유료 API 비용이나 추론
지연이 발생하지 않습니다. **유일한 opt-in 예외**는 동료 명령 콘솔이며, 기본
경로는 네트워크를 전혀 쓰지 않는 로컬 키워드 파서입니다(§5).

이 문서가 다른 AI 활용 사례와 구분되는 지점은 "AI로 코드와 그림을 만들었다"가
아니라, **AI 산출물을 신뢰하지 않는 운용 구조**를 먼저 세웠다는 점입니다.
결정론 계약, 스펙 동결, 병렬 레인 분리, 증거 기반 검증, 변이 테스트, 지식
축적 루프 — 이 다섯 가지가 §1의 내용이며 이 프로젝트의 실제 방법론입니다.

| 축 | AI 도구 | 배포물에 실린 산출물 |
|---|---|---|
| 3D 캐릭터 메시 | 자체 컨셉 이미지 → **Hyper3D Rodin** (image-to-3D) | 캐릭터 소스 메시 12종 |
| 리스킨·스키닝 | **Blender 5.x headless** + `tools/blender/reskin_character.py` | `Assets/Art/Characters/*.fbx` 12종 |
| 모션 | **Adobe Mixamo** 벤치 클립 → Unity Mecanim Humanoid 리타겟 | `Assets/Art/Motion/*.fbx` 14종 + `CinderActor.controller` |
| 사운드 | **ElevenLabs** sound-generation API + Music API | `Assets/Resources/Audio/*.mp3` 19종 |
| UI·환경 이미지 | **god-tibo-imagen (gti)** | 아이콘 44종, HUD 아틀라스 16타일, 환경 텍스처 18장, 컷씬 5장 |
| 지형 플립북 | gti hero 텍스처 + 결정론적 프레임 조립 스크립트 | `terrain-fx-{lava,ice,shift}-sheet.png` |
| 무기 프롭 | Blender headless 절차적 저폴리 오써링 | `Assets/Art/Props/equip-weapon-*.fbx` 6종 |
| 한글 폰트 | fontTools 서브셋 (나눔바른고딕 기반) | `Assets/Resources/Fonts/HudKorean.otf` |
| 인트로 영상 | gti 프레임 5장 + ffmpeg Ken-Burns 조립 | `Assets/StreamingAssets/Video/cinder-court-intro.mp4` |
| 게임 코드·검증 | Claude 멀티에이전트 레인 | `Assets/Scripts/` 58개 `.cs`, EditMode 테스트 66파일 |
| 플레이 영상 | Playwright + CDP 실입력 녹화 | 제출 영상 (합성 없음, §6.3) |

---

# 1. AI 운용 방법론 — 이 프로젝트의 구조

## 1.1 자유 프롬프트가 아니라 계약 문서

저장소 루트의 `CLAUDE.md`(및 이를 가리키는 `AGENTS.md`)가 모든 에이전트 세션의
**단일 운영 계약**입니다. 세션마다 "이렇게 해줘"를 다시 설명하지 않고, 규칙을
버전 관리되는 문서로 고정했습니다. 계약의 핵심 조항은 다음과 같습니다.

| 조항 | 내용 | 효과 |
|---|---|---|
| §1 엔진 관점 고정 | Unity + WebGL 전용. Three.js/DOM 가이드 적용 금지 | 원작(Three.js) 문맥이 새 코드에 오염되는 것을 차단 |
| §1 결정론 경계 | `CinderCourt.Sim`은 순수 C#·`UnityEngine` 참조 금지. View는 심 상태를 **읽기만** | AI가 편의상 렌더러에서 게임 상태를 고치는 것을 원천 차단 |
| §2 수치 계약 | 고정스텝 1/60, 아레나 1536×1024, 워든 HP 100 / 이동 218 u·s⁻¹ … 전부 명시 | **"숫자는 게이트다. 형용사는 게이트를 못 넘는다."** |
| §3 자산 클래스별 도구 고정 | 텍스처=gti, 스프라이트=ppgen, 3D=Blender headless, SFX=ElevenLabs | 같은 자산이 세션마다 다른 도구로 만들어지는 표류 방지 |
| §4 증거 표기 | 모든 주장에 `[OBSERVED]` / `[INFERENCE]` / `[TARGET]` 표기 | 목표치를 측정치로 위장하는 것을 금지 |
| §5 동시 세션 Git 안전 | `git add -A` 금지, 명시적 pathspec만, force-push 금지 | 병렬 레인이 서로의 변경을 덮어쓰는 사고 방지 |
| §7 지식 위키 | 반복 재현되는 결론은 `llm-wiki/wiki/`에 파일로 남긴다 | 세션이 끝나도 교훈이 남는다 |

## 1.2 Socratic 딥인터뷰 → 스펙 동결

사용자의 요청은 대부분 자연어 문단입니다("타격감 강화, 보스 다양화, 한글 입력,
UI 스케일 개선…"). 이것을 바로 구현에 넘기지 않고, **딥인터뷰 에이전트**가
저장소를 실측해 다음 4가지로 정형화한 뒤 사용자 승인으로 **동결(FROZEN)** 합니다.
실물은 `_workspace/current/intake/deep-interview-seed-ui-vfx-flow.md`입니다.

1. **이미 구현되어 있어 재제안 금지인 것** — 파일:라인 근거와 함께 14항목 표.
   (예: "넉백" 요청 → `CinderSim.cs:1069, 1093, 978`에 이미 존재)
2. **실제 작업면** — W1~W16 워크스트림으로 쪼개고 각각 레인 배정.
3. **변경 불가 설계 제약** — 예: `ViewWorld.Scale = 0.01f`는 Sim↔View 유일
   분리점이므로 건드리면 카메라 거리·포그·모든 링 반경이 동시에 깨진다.
4. **블로킹 결정 D1–D9** — 각 항목에 근거 있는 **제안 기본값**을 붙여 사용자가
   승인/거부만 하면 되도록. (예: D7 "BGM은 sound-generation API의 22초 상한
   때문에 부적합 → ElevenLabs Music API 신규 경로 추가")

이 단계의 목적은 "AI에게 무엇을 시킬지"를 정하는 것이 아니라, **AI가 임의로
해석할 여지를 없애는 것**입니다. 동결 이후에는 시드 문서가 진실의 원천이고,
레인들은 시드를 참조해 서로 다른 작업면을 병렬로 파고듭니다.

## 1.3 병렬 멀티에이전트 레인

동결된 시드를 4~7개 레인으로 나눠 병렬 실행했습니다. 각 레인은 담당 워크스트림,
쓰기 허용 경로, 산출 리포트 경로를 미리 배정받습니다. 최근 사이클의 실물 편성:

| 레인 | 담당 | 리포트 |
|---|---|---|
| sim-lane | W4 웨이브 포인트 예산 + DDA, W5 등급 드롭 + pity | `_workspace/current/engineering/sim-lane-w4-w5-report.md` |
| sim-lane2 | W6 보스 아키타입 4종 (AMENDMENT #16) | `sim-lane2-w6-report.md` |
| ui-lane | W7 탭 메타화면, W8 캠페인 미니맵, W10 커맨드 작업 큐 | `ui-lane-w7-w8-w10-report.md` |
| ui-lane3 | 획득 팝업 토스트, 지도/이정표 중복 해소 | `ui-lane3-loot-toast-map-report.md` |
| vfx-lane | W9 미스트/스킬 이펙트, 카메라 줌·롤 연출 | `vfx-lane-w9-v2-v3-report.md` |
| view-lane2 | W-MV 던전 경계 확장, V1 시전 동기화, V4 URP 포스트 + 프레임 워치독 | `view-lane2-wmv-v1-v4-report.md` |
| asset-lane / 2 / 3 | W12 BGM·SFX, W13 캐릭터, W14 무기, W6 보스 리스킨 3종, 획득음 3종 | `asset-lane-w12-w13-w14-report.md` 외 2건 |
| webgl-lane | W11 WebGL 한글 IME | `webgl-lane-w11-report.md` |

레인 분리가 성립하는 이유는 §1.1의 **결정론 경계**가 코드 레벨에서 이미
Sim / View를 갈라놓았기 때문입니다. 아키텍처 경계가 곧 병렬화 경계이고, 그래서
레인끼리 같은 파일을 놓고 싸우지 않습니다. 통합 EditMode 검증은 전 레인 완료 후
1회 수행합니다.

## 1.4 결정론 계약이 AI의 가드레일

AI 코드 생성에서 가장 위험한 실패는 "그럴듯하지만 재현 불가능한 코드"입니다.
이 프로젝트는 그것을 **테스트가 아니라 아키텍처**로 막습니다.

- **RNG 전면 금지.** 모든 확률은 모듈러·카운터·해시로만 구현합니다
  (`EliteSpawnModulus = 7`, 장비 드롭 `id % 7`, Ember Rest 결정론 오퍼 해시).
  W5 등급 드롭도 난수 대신 **pity 카운터 + 모듈러**로 설계했습니다(D5 결정).
- **골든 다이제스트 회귀.** 심 상태를 해시로 고정해 `DungeonGoldenDigestTests`가
  비교합니다. 어떤 레인의 변경도 기존 다이제스트를 **재-bless 없이** 통과해야
  합니다.
- **런타임 내 비교만 유효.** 스탠드얼론 dotnet 다이제스트와 Unity 다이제스트는
  float 하위비트가 다릅니다. 실제로 이번 사이클에서 `(int)(250 * 2.10f)`가
  dotnet 8에서 525, Unity에서 524로 갈라져 테스트 핀이 깨졌고, 정수 캐스트 지점에
  `+0.5f` 반올림을 넣어 양쪽을 일치시켰습니다
  (`llm-wiki/wiki/hongt-parallel-lane-integration-2026-08-08.md` §1).

## 1.5 증거 기반 검증 — 통과가 의미를 갖는지까지 증명

"테스트가 전부 초록"은 게이트가 실제로 무언가를 잡고 있다는 증거가 아닙니다.
그래서 **변이 검증(mutation check)** 을 병행합니다.

- 최근 전체 EditMode 결과: **808 / 808 passed, 0 failed**
  (`_workspace/current/engineering/unity-logs/test-results-164448.xml`).
  중간 마일스톤은 371/371 → 383/383 → 737/737 → 785/785 → 800/800 → 808/808로
  증가했으며, 어느 단계에서도 실패를 남긴 채 진행하지 않았습니다.
- 변이 증명 예: `SyncRoomObjective`의 `active`를 강제로 `false`로 뒤집자 **정확히
  6개 HUD 칩 테스트만** 실패했습니다(`test-results-104021.xml`). 게이트가
  동어반복이 아니라 실제로 문다는 증거입니다.
  커밋 `58b5226`은 같은 방식으로 "371/371 + 변이 12/12"를 기록했습니다.
- 자산 파이프라인도 수치 게이트를 갖습니다. Blender 리스킨 스크립트는 `armFit`
  0.7 미만이면 치명 실패로 중단하며, 실제로 `guard` 도너 스켈레톤은 armFit
  0.35~0.42로 **세 번 거부**되었고 `shadow-commander-boss` 도너로 교체해 0.76 /
  0.86 / 0.91을 얻었습니다(`docs/provenance/s{1,2,3}-*-reskin.json`).

## 1.6 지식 축적 루프 — 같은 실수를 두 번 하지 않는다

세션은 끝나도 교훈은 남아야 합니다. 반복 재현되는 원인·해법은
`llm-wiki/wiki/`에 파일로 기록하고, 다음 세션이 먼저 읽습니다. 실제로 기록된
것들:

- Unity float vs dotnet double 핀 드리프트와 그 일반 규칙
- Unity 배치모드 락 시 `unity-mcp` 경유 우회 검증 경로(도메인 리로드 경합 포함)
- 이미지 백엔드 HTTP 429의 실체와 `--provider codex-cli` 폴백
- "패배 스크림이 화면을 뭉갠 게 아니라 비네트였다" 같은 **철회된 오진 기록**

특히 마지막 유형이 중요합니다. 커밋 `8c005b5`("9/9 all cutscene 주장을 철회한다 —
실제는 clean 5, vignette 3, cutscene 1")처럼 **AI가 앞서 내린 결론을 스스로
철회한 기록**을 남깁니다. 틀린 결론을 조용히 지우면 다음 세션이 같은 오진을
반복합니다.

---

# 2. 런타임 구조 — AI 비의존

| 계층 | 구성 요소 | 성격 |
|---|---|---|
| 시뮬레이션 | `Assets/Scripts/Sim/` (`CinderSim.cs`, `SimTypes.cs`, `CampaignTypes.cs`, `HackTypes.cs`, `DifficultySpec.cs`) | 순수 C# 결정론 고정 60 Hz, `UnityEngine` 비참조 |
| 프레젠테이션 | `Assets/Scripts/View/` (`GameView.cs`, `GameDirector.cs`, `HudView.cs`, `ActorView.cs`, `VfxDirector.cs`, `PostFxGate.cs`, `CameraRig.cs`) | 심 상태를 읽기만 하고 write-back 금지 |
| 입력 | `InputAdapter.cs`, `CommandConsoleBuffer.cs`, `CommandConsoleImeComposition.cs` | 키보드 / 포인터 / 터치 / 화면 버튼 / 한글 IME → 결정론 `SimInput` 부울 래치 |
| 오디오 | `AudioDirector.cs` | SimEvent → mp3 one-shot 큐 + BGM 루프 (6-보이스 풀 + 결정론적 ±6% 피치 지터) |
| 저장 | `CampaignStore.cs` / `WebGLStorage.cs` | localStorage 전용, 서버 전송 없음 |

배포 빌드는 추론 호출을 하지 않습니다. 흥미롭게도 이 원칙을 **빌드 스크립트가
강제**합니다. 에디터 자동화용 MCP 패키지와 그 NuGet DLL 묶음은 리졸버가 매
도메인 리로드마다 `anyPlatform=1`로 되돌려 놓기 때문에, `BuildScript`가
`BuildPlayer` 직전에 `UNITY_MCP_READY` 디파인을 벗기고
`Assets/Plugins/NuGet/**`를 WebGL 대상에서 제외합니다
(`Assets/Editor/BuildScript.cs:82-120`, `ExcludeEditorToolingFromWebGl`).
이 조치가 없으면 약 17 MB의 SignalR/Roslyn 인접 관리코드가 wasm에 IL2CPP되고,
WebGL이 실행할 수 없는 소켓 API를 참조하게 됩니다.

---

# 3. 3D 캐릭터 — 자체 창작 이미지에서 출발한 메시

## 3.1 출처 체인과 IP 관계

이 프로젝트의 캐릭터 3D 자산은 외부 IP 모델을 가져온 것이 **아닙니다.** 체인은
다음과 같습니다.

```
자체 창작 컨셉 이미지 (팀이 직접 생성/작성)
  → Hyper3D Rodin image-to-3D 로 메시화
  → Blender headless 리스킨 (본히트 자동 웨이트, Unity 표준 휴머노이드 본 이름)
  → Unity Mecanim Humanoid 아바타
  → Mixamo 모션 클립 리타겟
```

- **메시**: 팀이 직접 만든 이미지를 Rodin으로 3D화한 결과물입니다. 소스
  라이브러리의 파일 구조(`<character>/glb/base_basic_pbr.glb`,
  `base_basic_shaded.glb`, `textureBasicPack/`)가 Rodin image-to-3D의 표준
  출력 규약과 일치하며, 컨셉 원본 이미지도 메시와 같은 디렉터리에 남아 있습니다.
  파이프라인의 프롬프트 계약은 `docs/concept-to-web-game-3d-pipeline.md`
  Phase 2와 `docs/character-asset-pipeline.md` §3에 문서화돼 있습니다.
  즉 **타 IP 캐릭터를 스캔하거나 내려받은 것이 아니라, 우리 그림을 우리가
  3D로 바꾼 것**입니다.
- **모션**: Adobe Mixamo의 표준 휴머노이드 애니메이션을 사용했습니다. Mixamo는
  Adobe 계정으로 로그인하면 캐릭터·애니메이션을 상업/비상업 프로젝트에
  로열티 없이 사용할 수 있는 라이선스를 제공합니다(§7.3에 조건 명시).
- 따라서 이 게임의 캐릭터 외형은 **우리 창작물**, 움직임은 **정당하게
  라이선스된 표준 모션 라이브러리**입니다. 타 게임 IP에서 가져온 요소는
  없습니다.

## 3.2 리스킨 — 원작의 결함을 폐기하고 표준 경로로

원작(Three.js 판)은 절차적 영역분할 스키닝을 썼고, 애니메이션 중 메시가 찢어지는
결함이 있었습니다. Unity 포트는 이 방식을 **재사용 금지**로 못박고
(`CLAUDE.md` §3) 표준 경로로 재구축했습니다.

실행 명령(보스 3종 중 1건, `docs/provenance/s1-cinder-warden-reskin.json`):

```
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup \
  --python-exit-code 1 -P tools/blender/reskin_character.py -- \
  --glb  <도너 리그 model.glb> \
  --mesh-glb <자체 메시 base_basic_pbr.glb> \
  --out Assets/Art/Characters/s1-cinder-warden.fbx \
  --report _workspace/current/engineering/reskin/s1-cinder-warden.json \
  --max-tris 25000
```

검증 결과 표(`docs/provenance/*-reskin.json`의 `verification` 필드 실측):

| 캐릭터 | 최종 tri | armFit | heat orphan | 정규화 실패 정점 | 비고 |
|---|---:|---:|---:|---:|---|
| lantern-reaver | 8,271 | 0.756 | 0 | 0 | 병합 1,301정점 위생 처리 |
| human-command-boss | 7,711 | — | 0 | 0 | 도너 교체 불필요 |
| s1-cinder-warden | 17,264 | 0.864 | 0 | 0 | 도너 대체(guard 0.40 거부) |
| s2-veil-tactician | 18,104 | 0.757 | 0 | 0 | 도너 대체(guard 0.35 거부) |
| s3-gate-sovereign | 25,000 | 0.912 | 0 | 0 | 원본 38,657 → 예산 초과분 데시메이트 |

`--max-tris 25000`은 `CLAUDE.md` §1의 WebGL 제약(캐릭터 ≤ 25k tri)을 스크립트
레벨에서 강제한 것이고, 텍스처는 리스킨 스크립트가 FBX 임베드 전에 1024로
다운스케일하며 `.png.meta`도 `maxTextureSize: 1024`(WebGL 플랫폼 포함)로
고정합니다.

## 3.3 모션 — 14클립, 인덱스가 계약

`Assets/Art/Motion/`의 Mixamo FBX 14종을 `Assets/Editor/CharacterImportPipeline.cs`가
`CinderActor.controller`로 조립합니다. **배열 인덱스가 애니메이터의 `action`
조건값이므로 순서 자체가 동결 계약**이며, 0~10행은 심이 방출하는
`ActorAction` 열거형과 정렬돼 있어야 합니다(`ClipTableTests`가 핀).

| # | 액션 | Mixamo 클립 | 루프 |
|---:|---|---|---|
| 0 | idle | Unarmed Idle | ○ |
| 1 | move | Walking | ○ |
| 2 | run | Running | ○ |
| 3 | hit | Standing React Small From Left | |
| 4 | bighit | Receive Uppercut To The Face | |
| 5 | attack | Standing Melee Attack Horizontal | |
| 6 | critical | Illegal Elbow Punch | |
| 7 | avoid | Dodging | |
| 8 | defence | Body Block | |
| 9 | die | Dying | |
| 10 | show | Mutant Roaring | |
| 11 | attack2 (View 전용) | Hook Punch | |
| 12 | attack3 (View 전용) | Standing Melee Combo Attack Ver. 2 | |
| 13 | cast (View 전용) | Standing 2H Magic Attack 01 | |

11~13행은 **심이 모르는 View 전용 서브스테이트**입니다. 콤보 2·3타와 스킬 시전
연출을 동결된 심 계약을 개정하지 않고 얹기 위한 설계입니다.

한 가지 측정 사례를 덧붙입니다. Mixamo 공격 클립은 "자세 잡기 → 예비동작 →
타격 → 회복"의 독립 퍼포먼스로 저작돼 있는데, 심의 공격 포즈 창은 고정 0.417초라
2.4초짜리 스윙은 **예비동작만 보이고 끝났습니다**. 에디터에서
`AnimationClip.SampleAnimation`으로 오른손 속도를 프레임별로 측정한 결과 f18에
가속·f21~25 피크(f24에서 6.9 u/s)·f26에 소진이었고, 16~28프레임만 잘라
`SimConfig.AttackActiveFrom/To`(0.167~0.333 s) 안에 타격 프레임이 들어오게
했습니다(`CharacterImportPipeline.cs` `ClipTrims`). **연출 문제를 감각이 아니라
측정으로 해결한 사례**입니다.

---

# 4. 이미지·오디오 생성 파이프라인

## 4.1 도구 고정 계약과 실제 가용성

`CLAUDE.md` §3이 자산 클래스별 도구를 고정합니다. 다만 실측 결과와 다른 부분은
문서에 그대로 남겼습니다.

| 자산 클래스 | 계약 도구 | 실제 |
|---|---|---|
| 컨셉·텍스처·아틀라스 | god-tibo-imagen (`gti`) | 사용. 기본 `private-codex` 백엔드가 이 머신에서 **HTTP 429 상시 반환** → `--provider codex-cli`로 고정 |
| 2D 스프라이트/시트 | perfectpixel (`ppgen`) | **미설치**(`ppgen: command not found`, 2026-08-07 재확인). gti + 결정론적 조립으로 대체(§4.3) |
| 3D 리스킨·프롭 | Blender 5.x headless | 사용 |
| SFX·BGM | ElevenLabs API | 사용 |

`codex-cli` 프로바이더는 `--size`와 `--image` 입력을 지원하지 않습니다. 그래서
크로스-프레임 일관성은 참조 이미지가 아니라 **공유 STYLE 접미사**로 유지하고,
반환된 ~1254px 이미지는 Unity 임포터(`EnvTextureImportPipeline.cs`)가 1024로
클램프합니다. 4-way 병렬 호출은 즉시 429를 받으므로 생성 스크립트는 **엄격
직렬 + 지수 백오프 + 8초 예의 간격**으로 작성돼 있습니다
(`tools/gen_env_textures.sh`).

## 4.2 UI 아이콘 — 스타일 계약 + 마젠타 키 매팅

아이콘은 모델에게 매번 다른 지시를 주지 않고, **고정 STYLE 문자열 + 아이콘별
코어 설명**의 조합으로 생성합니다. 스타일 계약 원문
(`tools/icons/gen_icons.sh`):

> `flat vector game icon, dark fantasy hack-and-slash style, bold readable silhouette, thin ember-orange rim light, deep navy interior shading, consistent 3px outline, centered, fills 80% of frame. Solid pure magenta #FF00FF background on every pixel outside the icon. No text, no watermark, no border frame.`

배경을 **순수 마젠타 #FF00FF**로 강제하는 이유는 후단의 `tools/icons/mat_icons.py`가
크로마 키로 알파를 뽑아내기 때문입니다. 이 규약 때문에 게임 팔레트에서
마젠타 계열을 전면 금지했고, 아군(플레이어 등불 코어)의 시안과 적을 색으로
분리했습니다. **아트 지시가 곧 게임플레이 가독성 사양이 된 사례**입니다.
현재 `Assets/Resources/Icons/`에 44장의 아이콘이 실려 있으며, 각 아이콘의
프롬프트 전문과 매팅 후 투명 픽셀 비율까지
`docs/provenance/ui-hud-icons.json`, `ui-icons.json`에 기록돼 있습니다.

## 4.3 HUD 아틀라스와 지형 플립북 — "생성" 다음에 "결정론적 조립"

이미지 모델에게 격자 레이아웃을 한 번에 그리게 하면 정렬이 무너지고, 실제로
과거에 생성 아틀라스가 시트 위에 **없는 섹션 라벨을 그려 넣어 거부된 전례**가
있습니다(`docs/character-asset-pipeline.md`). 그래서 두 단계로 나눴습니다.

- **HUD 아틀라스**: gti가 4×4 그리드 소스 1장을 생성하고, 슬라이서가 numpy로
  실제 PNG의 열/행 밝기 피크를 검출해 격자 경계를 **검증한 뒤**(x/y ≈ 0, 313,
  627, 940, 1254) ImageMagick으로 잘라냅니다. 16타일 각각이 `HudView.cs`의 어느
  요소에 9-slice 보더 몇으로 물리는지까지 `docs/provenance/hud-atlas.json`에
  적혀 있습니다.
- **지형 플립북**: gti는 테마별 **hero 텍스처 1장**만 생성하고,
  `tools/gen_terrain_fx_sheets.py`가 4×4 = 16프레임을 결정론적으로 조립합니다.
  위상이 `[0, 2π)`에서 정확히 감기므로 **수학적으로 완벽한 루프**가 보장됩니다.
  vfx 레인의 소비 계약이 "색 없는 그레이스케일 패턴"이었기 때문에 hero를
  `max(R,G,B)` + 콘트라스트 스트레치로 변환했는데, 표준 휘도식
  (0.299R+0.587G+0.114B)을 쓰지 않은 이유는 청색 가중치 0.114가 ice/shift
  테마의 시안 발광을 씻어내기 때문입니다. 근거까지 provenance에 남겼습니다.

## 4.4 환경 텍스처 — 스테이지 9곳 × 2클래스

`tools/gen_env_textures.sh`가 스테이지별 stone/floor 알베도 18장을 생성해
`Assets/Resources/Textures/Env/`에 넣고, `EnvironmentBuilder.ApplyStageTextures`가
스테이지 진입 시 두 공유 머티리얼의 `_BaseMap`을 재바인딩합니다(한 번에 한
스테이지만 살아 있으므로 4-머티리얼 환경 예산 불변). 모든 프롬프트에 붙는
공통 절:

> `seamless tileable square texture, flat even lighting with no baked shadows or highlights, orthographic flat albedo map for a game engine, edges wrap perfectly, no text, no logo, no border, no vignette`

스테이지별 컨셉 절 예시(스크립트의 `STAGES` 테이블이 진실의 원천):

| 스테이지 | stone 프롬프트 절 |
|---|---|
| cinder-span | weathered charcoal basalt block masonry veined with dull orange ember cracks |
| ember-gallery | fire-blackened brick gallery wall with glowing molten orange fissures |
| abyss-chancel | violet-grey cathedral stone masonry with pale indigo runic carving |
| witness-well | damp pale blue-grey well stone blocks with wet mineral staining |

게이트: `DungeonFramingAndMoodTests.StageTextures_*`(EditMode)가 맵 누락,
non-Repeat 랩모드, 1024 초과를 실패시킵니다.

## 4.5 사운드 — 두 개의 엔드포인트를 나눠 쓴 이유

| 용도 | 엔드포인트 | 스크립트 | 산출물 |
|---|---|---|---|
| 효과음 15종 | `POST /v1/sound-generation` | `tools/audio/gen_sfx.py` | `Assets/Resources/Audio/cue-*.mp3` |
| BGM 4종 | `POST /v1/music` | `tools/audio/gen_bgm.py` | `Assets/Resources/Audio/bgm-*.mp3` |

**분리 이유(D7 결정)**: sound-generation 엔드포인트는 22초 상한이 있어 BGM
루프로 부적합합니다. Music API는 3,000~600,000 ms를 허용하고 기악 베드에
특화돼 있어 별도 경로를 새로 팠습니다. 두 스크립트 모두 API가 실패하면
**절차적 오디오로 조용히 대체하지 않고 크게 실패**합니다 — 소리가 나긴 나는데
출처를 모르는 상태를 만들지 않기 위해서입니다.

실제 프롬프트 인용(`docs/provenance/audio.json`, `bgm.json` 원문):

- `cue-nova` — "Massive fiery shockwave nova burst radiating outward, deep
  descending roar with ember sizzle tail, arena AoE blast, game SFX"
  (1.6 s, promptInfluence 0.55)
- `cue-toast` — "Extremely short soft UI toast popup sound, gentle upward slide
  into a light airy pop that decays almost instantly, midrange and high
  frequencies only, no bass, no low end, no reverb tail, subtle dark-fantasy
  interface notification, dry, game SFX one-shot"
- `cue-lore` — "Ethereal abyssal ambience swell, ghostly airy texture with faint
  ash-wind and deep sub rumble, mysterious ancient reliquary atmosphere,
  instrumental sound design only, absolutely no voice, no whispering words,
  no speech, no vocals"
- `bgm-stage` — "Dark fantasy dungeon combat ambient bed, driving low ember
  drone with a slow ominous two-note pulse, distant deep choir-like synth pads,
  smoldering coals crackle texture sparsely, seamless loop, instrumental only,
  no melody spikes, no percussion breaks, no vocals" (60,000 ms,
  `forceInstrumental: true`)

`cue-lore`와 `bgm-*`의 프롬프트가 유난히 부정어로 도배된 것은 의도적입니다.
사용자 지시(2026-08-04)로 **음성 내레이션을 넣지 않기로** 했기 때문에, 모델이
보컬·속삭임을 얹지 못하도록 프롬프트와 `forceInstrumental` 플래그로 이중
차단했습니다.

`AudioDirector.cs`는 동일 파형이 위상 겹침으로 버징되는 문제를 6-보이스
라운드로빈 풀과 결정론적 ±6% 피치 지터(`[0.94, 1.06]`)로 막습니다. 지터 RNG는
View 전용이라 심 틱에 관여하지 않고, 그래서 결정론 계약을 건드리지 않습니다
(`AudioPitchJitterTests.cs`).

## 4.6 그 외 생성 자산

- **무기 프롭 6종**: image-to-3D 도구가 이 환경에 없고 소스 라이브러리에
  단검/활/해머 메시가 없어서, D8 결정에 따라 Blender 헤드리스 **절차적 저폴리
  오써링**으로 만들었습니다(`tools/blender/gen_weapon_props.py`, 예산 800 tri,
  실측 46~200 tri). 그립을 원점에, 타격단을 +Y로 두는 소켓 규약이
  `ActorView.AttachEquipProps`의 RightHand 포즈와 맞춰져 있습니다.
- **컷씬 이미지 5장**: `Assets/Resources/Scenes/scene-{intro, stage-entry,
  transition, boss-entry, ember-rest}.png`, gti 생성
  (`docs/provenance/scene-synopsis-art.json`, `scene-ember-rest.json`).
- **인트로 영상**: 기획(비트시트) → gti 프레임 5장 → ffmpeg Ken-Burns + 크로스
  페이드 + 타이틀 락업. **6번째 비트는 렌더 결과가 등불을 든 워든이 아니라
  "과일"처럼 보여서 재생성 대신 컷**했습니다(`docs/provenance/intro-video.json`).
  WebGL의 `VideoPlayer`는 URL 스트리밍만 가능해 `StreamingAssets`에 두었고,
  4초 준비 타임아웃과 20초 워치독으로 **부팅이 영상 때문에 막히지 않는다**는
  불변조건을 `IntroVideoViewTests.cs`가 검증합니다.
- **한글 HUD 폰트**: `tools/gen_hud_font.sh`가 `Assets/Scripts/View/*.cs`의
  모든 문자열 리터럴에서 사용 글리프를 수확해 fontTools로 서브셋합니다. 이
  스크립트에는 **자기 자신을 검증하지 못했던 버그의 기록**이 주석으로 남아
  있습니다: 정규식 `"([^"\\]*)"`이 백슬래시를 포함한 리터럴을 거부해
  `"Companion cadence −{...}%"`의 U+2212가 charset에 들어가지 못했는데, 커버리지
  검사도 **같은 charset**과 비교하다 보니 FULL이라고 출력했습니다. "자기 사각지대와
  비교하는 검사기는 검사기가 아니다" — 두 규칙의 합집합으로 수정했습니다.

---

# 5. 동료 명령 콘솔 — 유일한 opt-in AI

던전 중 `Enter`로 텍스트 콘솔을 열고 자연어 명령을 입력하면 **닫힌 의도 집합**으로
분류되어, 키 입력과 완전히 동일한 결정론 `SimInput` 래치로 진입합니다.

**기본 경로(네트워크 0)** — `CompanionCommandParser.Parse`가 한국어 우선·순서
있는 키워드 규칙표를 스캔합니다(구체 규칙이 일반 규칙보다 먼저: "결계"가 일반
"방어"보다 먼저 SkillAegis에 매칭). 의도는 FocusAttack / Defend / Recall /
SkillNova / SkillAegis / SkillPulse / SkillBolt / SkillDash / PickupInfo로
닫혀 있고, 심이 지원하지 않는 PickupInfo는 **정직한 안내 토스트**를 띄웁니다.
분류 정확성은 `CompanionCommandParserTests.cs`가 게이트합니다.

**opt-in 폴백** — 로컬 파서가 Unknown을 반환하고 **플레이어가 자신의 Gemini API
키를 런타임에 직접 등록한 경우에만**, 미분류 자유 문장을 Gemini 2.5 Flash Lite
(`models/gemini-2.5-flash-lite:generateContent`)로 분류합니다
(`Assets/Scripts/View/GeminiCommandClient.cs:24`). 키는 빌드·저장소에 포함되지
않고 `KeyVault.Protect`로 난독화되어 PlayerPrefs에만 저장됩니다
(`KeyVaultTests.cs`). 응답은 **의도 단어 1개로 제한**되며, 네트워크 실패는
Unknown으로 강등되어 토스트만 띄울 뿐 입력을 잠그지 않습니다. 즉
**시뮬레이션은 어떤 경로로도 AI 출력에 의존하지 않습니다.**

---

# 6. 코드·검증·영상에서의 AI 활용

## 6.1 검증 하니스

| 명령 | 검증 내용 |
|---|---|
| `Unity -batchmode -runTests -testPlatform EditMode` | `Assets/Tests/EditMode/` 66개 테스트 파일 — 결정론 심, 캠페인 라우트, 명령 파서·큐·IME, 애니메이션 클립 정렬, HUD 레이아웃, WebGL 텍스처 상한, 오디오 지터, 환경 빌더, 골든 다이제스트 |
| `-executeMethod CinderCourt.EditorTools.BuildScript.BuildWebGL` | WebGL 빌드. 최근 관측: `result=Succeeded size=70619948 errors=0 warnings=8` |
| `bash tools/deploy/deploy_pages.sh` | gh-pages 배포 후 라이브 URL의 HTTP 200 및 바이트 크기 대조 |
| `node tools/video/capture-unity-play.mjs` | 배포 빌드 실입력 플레이 + 실프레임 영상 캡처 |

**에이전트에게 코드를 대신 쓰게 하는 것이 아니라, 사람이 놓치는 것을 측정으로
잡게 하는 방식**입니다. 배포 검증도 "올렸다"가 아니라 라이브 URL의 index
9,317 B / loader 48,106 B / wasm 10,478,554 B / data 36,189,852 B를 로컬 빌드와
바이트 단위로 대조하는 형태로 기록합니다.

## 6.2 AI가 짠 코드의 대표 사례 — WebGL 한글 IME

emscripten의 키보드 경로는 `keydown/keypress/keyup`만 등록합니다. 브라우저 IME는
**포커스된 편집 가능 요소**에 조합하는데 WebGL 캔버스는 편집 요소가 아니어서,
배포 빌드에서 한글 조합 중간 음절이 아예 나타나지 않았습니다. 해법은 화면 밖에
실제 `<input>`을 만들어 콘솔이 열려 있는 동안 포커스를 주고 DOM composition
이벤트를 Unity로 전달하는 것입니다(`Assets/Plugins/WebGL/hangul_ime.jslib`).
Unity 쪽은 콘솔을 열기 전에 `WebGLInput.captureAllKeyboardInput = false`로
자체 핸들러를 비켜 줍니다. 관련 테스트 29건 전량 통과
(`_workspace/current/engineering/webgl-lane-w11-report.md`).

## 6.3 플레이 영상의 진정성

대회 규정은 *"AI를 이용한 조작·합성이나 타인 영상의 도용은 불가"*이며
*"실제 플레이 화면 그대로"*를 요구합니다.

**채택하지 않은 방법**: 스크린샷을 이어붙이고 가상 커서·줌을 얹는
screenshot-to-video 렌더링은 사내 도구로 가능했지만 규정이 금지하는 합성에
해당하므로 **의도적으로 배제**했습니다.

**채택한 방법** (`tools/video/capture-unity-play.mjs`):

1. 실제 Chromium이 배포 페이지 <https://akillness.github.io/hongT/>를 로드
2. 드라이버가 **CDP 입력 도메인**으로 키를 보냄 — 물리 키보드와 동일 경로이며
   게임 상태를 직접 조작하는 코드는 없음
3. 브라우저가 **실제로 렌더링한 프레임**을 그대로 녹화 — 보간·리타이밍·합성·생성 없음
4. 후처리는 로딩 스플래시 헤드트림, 30 fps H.264 메자닌, CompressO
   (`compresso_ffmpeg`, crf 28 · slow · faststart) 압축뿐

---

# 7. 외부 에셋 / 오픈소스 출처 및 라이선스

## 7.1 배포 빌드(WebGL 아티팩트)에 실리는 것

| 항목 | 출처 | 라이선스 / 권리 근거 |
|---|---|---|
| Unity 엔진 런타임 | Unity Technologies, 6000.5.6f1 | Unity Editor Software Terms (구독 등급에 따른 조건 — **§7.4 확인 필요 1**) |
| URP 17.5, uGUI, Input System, Timeline, AI Navigation 등 Unity 퍼스트파티 패키지 | Unity Technologies | **Unity Companion License**(각 패키지 `LICENSE.md` 실측) |
| 캐릭터 메시 12종 (`Assets/Art/Characters/*.fbx`) | 팀 자체 컨셉 이미지 → Hyper3D Rodin image-to-3D → Blender 리스킨 | 자체 창작물. 생성 도구 약관 — **§7.4 확인 필요 2** |
| 모션 클립 14종 (`Assets/Art/Motion/*.fbx`) | **Adobe Mixamo** | Mixamo 이용약관(§7.3) — **§7.4 확인 필요 3** |
| 오디오 19종 (`Assets/Resources/Audio/*.mp3`) | **ElevenLabs** sound-generation + Music API | ElevenLabs 이용약관(플랜별 상업 이용) — **§7.4 확인 필요 4** |
| UI 아이콘 44종, HUD 아틀라스 16타일, 환경 텍스처 18장, 컷씬 5장 | **god-tibo-imagen (gti)** 생성 | 생성 이미지, 프롬프트 전문 `docs/provenance/` 기록 — **§7.4 확인 필요 5** |
| 지형 플립북 3종 | gti hero + `tools/gen_terrain_fx_sheets.py` 결정론 조립 | 상동 |
| 무기 프롭 6종 (`Assets/Art/Props/equip-weapon-*.fbx`) | Blender 절차적 생성 (팀 작성 스크립트) | 자체 창작물 |
| 인트로 영상 (`StreamingAssets/Video/cinder-court-intro.mp4`) | gti 프레임 5장 + ffmpeg 조립 | 상동 |
| **한글 HUD 폰트** (`Assets/Resources/Fonts/HudKorean.otf`) | **나눔바른고딕OTF** 서브셋 | 폰트 내부 name 테이블: *"Copyright © 2013 NHN Corporation. All rights reserved. Font designed by FONTRIX Inc."* — **§7.4 확인 필요 6** |
| 게임 코드 (`Assets/Scripts/`, `Assets/Editor/`, `Assets/Plugins/WebGL/*.jslib`) | 팀 자체 작성(AI 에이전트 협업) | 자체 저작물 |

> **정정 고지**: 이전 판(`docs/nan2026/02-ai-tech.md` §8.1)은 "배포 빌드는 외부
> 폰트를 포함하지 않는다"고 기술했습니다. 이는 정확하지 않습니다. 현재 빌드는
> 나눔바른고딕에서 파생한 서브셋 폰트 `HudKorean.otf`를 `Resources`로 싣고
> `HudView` / `LobbyView` / `CutsceneView` / `DamageNumberPool` /
> `IntroVideoView`에서 로드합니다. 위 표와 §7.4가 정정된 내용입니다.

## 7.2 저장소에는 있으나 배포 빌드에서 제외되는 것

에디터 자동화용 MCP 툴체인은 **빌드 시점에 명시적으로 제외**됩니다
(`BuildScript.ExcludeEditorToolingFromWebGl`, §2 참조).

| 항목 | 라이선스 (실측) | 비고 |
|---|---|---|
| `com.ivanmurzak.unity.mcp` 0.87.0 | **Apache-2.0** (패키지 `LICENSE` 헤더 확인) | 에디터 전용 |
| `com.ivanmurzak.unity.mcp.{animation, cinemachine, inputsystem, navigation, particlesystem, probuilder, splines, terrain, tilemap, timeline}` | **MIT** (패키지 `LICENSE` 헤더 확인) | 에디터 전용, openupm 스코프 레지스트리 |
| `Assets/Plugins/NuGet/` 내 `Microsoft.*` · `System.*` DLL 40여 개 | MIT (.NET Foundation) | MCP 리졸버가 설치, WebGL 제외 |
| `R3.dll` | MIT | 상동 |
| `McpPlugin.dll`, `McpPlugin.Common.dll`, `ReflectorNet.dll` | **§7.4 확인 필요 7** | 상동 |

## 7.3 개발 전용 도구 (저장소·배포물 미포함 또는 미실행)

| 도구 | 라이선스 | 용도 |
|---|---|---|
| Blender 5.x | GPL (애플리케이션). 생성 결과물의 권리는 사용자 | 리스킨, 절차적 프롭, 지형 변환 |
| Playwright | Apache-2.0 | 플레이 영상 캡처 드라이버 |
| ffmpeg (CompressO 번들 및 시스템) | LGPL/GPL (빌드 구성별) | 영상 조립·압축 |
| CompressO | **§7.4 확인 필요 8** | 제출 영상 최종 압축 |
| Remotion 4.0.245 (`tools/video/brand/`) | **Remotion License** — 개인·직원 3인 이하 영리법인·비영리는 무료, 그 외는 company license 필요 — **§7.4 확인 필요 9** | 브랜드 범퍼 렌더 |
| fontTools, Pillow, numpy | MIT / MIT-CMU / BSD-3-Clause | 폰트 서브셋, 이미지 조립 |
| yt-dlp | Unlicense | 설계 리서치용 리뷰 영상 자막 추출(§7.5) |

## 7.4 라이선스 "확인 필요" 목록

아래는 **저장소 안에서 확정할 수 없어 추측하지 않은 항목**입니다. 제출 전
계정·약관 확인이 필요합니다.

| # | 항목 | 확인해야 할 내용 |
|---:|---|---|
| 1 | Unity 라이선스 등급 | 사용 중인 시트가 Personal / Plus / Pro 중 무엇인지, 매출 기준 자격과 스플래시 스크린 요건 충족 여부 |
| 2 | Hyper3D Rodin 생성물 권리 | 사용한 플랜(무료/유료)에서 생성 메시의 상업적 사용·재배포 권리가 어떻게 규정되는지 |
| 3 | **Mixamo FBX의 저장소 공개 커밋** | Adobe 계정 라이선스는 게임 내 사용을 허용하지만, **애니메이션 파일 자체를 독립 자산으로 재배포하는 것은 제한**합니다. 현재 `Assets/Art/Motion/*.fbx` 14개 원본이 공개 GitHub 저장소에 커밋되어 있습니다. 제출물 1이 "전체 소스 공개"를 요구하므로, 원본 FBX 커밋이 허용 범위인지 확인이 필요합니다 |
| 4 | ElevenLabs 플랜 | 생성 오디오의 소유권·상업 이용은 플랜 등급에 따라 다르며 무료 등급은 출처 표기 의무가 있습니다. 사용한 계정 등급과 그에 따른 표기 의무 확인 |
| 5 | god-tibo-imagen 백엔드 약관 | `gti`는 로컬 CLI이지만 실제 생성은 백엔드 모델(`codex-cli` 프로바이더, 기록상 `gpt-5.4`)이 수행합니다. 해당 백엔드의 이미지 출력물 상업 이용 조건 확인 |
| 6 | **나눔바른고딕 서브셋 재배포** | 나눔글꼴 계열은 통상 SIL Open Font License 1.1로 배포되나, **이 폰트 파일의 name 테이블에는 OFL 표기가 없고 "All rights reserved" + NHN 상표 고지만 있습니다.** 확인 항목 두 가지: ⑴ 사용한 배포본의 실제 라이선스 문서, ⑵ OFL이라면 파일명을 `HudKorean.otf`로 바꾸면서 **내부 family name은 여전히 `NanumBarunGothicOTF`** 이므로 Reserved Font Name 조항 위반 소지 확인 |
| 7 | `McpPlugin.dll` / `ReflectorNet.dll` | 어셈블리 자체에 라이선스 파일이 동봉돼 있지 않음. 상위 프로젝트(IvanMurzak/Unity-MCP, Apache-2.0)와 동일한지 확인. 배포물에는 미포함 |
| 8 | CompressO | 애플리케이션 라이선스 확인 (개발 전용, 배포물 미포함) |
| 9 | **Remotion company license** | 무료 등급은 "개인 또는 직원 3인 이하 영리법인". 팀이 법인 소속으로 간주될 경우 company license가 필요할 수 있음. 브랜드 범퍼가 최종 제출물에 포함되지 않는다면 해당 없음 |

## 7.5 설계 참고 자료 (자산 미사용)

정직성을 위해 밝힙니다. 다음은 **분석 대상**이었을 뿐 어떤 픽셀·모델·코드도
가져오지 않았습니다.

- 액션RPG *아킬레우스: 레전드 언톨드(Achilles: Legends Untold)* 의 리뷰 영상
  1건(자막 추출, `yt-dlp`)과 UI 스크린샷 3장. 탭 메타화면 구조와 난이도·군집
  AI 사양을 **읽기 위한 참고**로만 사용했고, OCR 결과를
  `_workspace/current/intake/reference-ui-ocr.txt`에, 분석 결론을
  `docs/provenance/video-analysis-wbDv6nawEeY.md`에 기록했습니다.
- 링크 프리뷰 키 아트 생성 시에도 참조 이미지를 생성기에 **넣지 않았고**,
  프롬프트에 "No copied characters, named game symbols, logos, UI, captions,
  typography, watermarks, or readable text"를 명시했습니다
  (`docs/provenance/cinder-court-link-preview.json`).
- 원작 *Abyssal Lantern* (Three.js 판)은 **같은 팀의 자체 선행 프로젝트**이며,
  이 저장소는 그 자산·수치 계약을 읽기 전용 소스로 참조합니다.

---

# 8. 사용한 AI 도구 전체 목록

| 도구 | 제공자 / 모델 | 용도 | 배포물 영향 |
|---|---|---|---|
| Claude 에이전트 워크플로 | Anthropic | 딥인터뷰, 병렬 레인 구현, 테스트, 문서, 캡처 하니스 | 소스 코드·문서 |
| god-tibo-imagen (`gti`) | private-codex / codex-cli (기록상 gpt-5.4) | 아이콘·HUD 아틀라스·환경 텍스처·컷씬·인트로 프레임·키 아트 | PNG 자산 |
| Hyper3D Rodin | Rodin (image-to-3D) | 자체 컨셉 이미지 → 캐릭터 소스 메시 | FBX 캐릭터 |
| Adobe Mixamo | Adobe | 휴머노이드 모션 라이브러리 | FBX 모션 14종 |
| ElevenLabs sound-generation | ElevenLabs | 효과음 15종 | mp3 |
| ElevenLabs Music API | ElevenLabs | BGM 4종 | mp3 |
| Blender 5.x headless | 오픈소스 | 리스킨·리타겟·절차적 프롭·지형 변환 | FBX |
| Gemini 2.5 Flash Lite | Google | 동료 콘솔 opt-in 자유 문장 분류 | **런타임 opt-in만**(플레이어 본인 키) |
| Remotion | Remotion | 브랜드 범퍼 렌더 | 홍보 영상(게임 빌드 미포함) |
| perfectpixel (`ppgen`) | — | 계약상 2D 스프라이트 담당이나 **이 환경에 미설치** | 사용되지 않음 |

---

# 9. 맺음 — 이 프로젝트가 주장하는 것

AI로 게임을 만들었다는 주장은 흔합니다. 이 문서가 실제로 보여주려 한 것은
그것이 아니라, **AI 산출물을 믿지 않는 구조를 먼저 만들면 AI를 훨씬 넓게 쓸 수
있다**는 것입니다.

- 결정론 계약이 있었기에 7개 레인을 동시에 돌려도 서로를 깨뜨리지 않았습니다.
- 수치 게이트(`armFit ≥ 0.7`, `≤ 25k tri`, `≤ 1024 px`, `≤ 120 MB`)가 있었기에
  자산 생성 실패를 사람이 눈으로 찾지 않아도 됐습니다.
- 변이 검증이 있었기에 "808/808 초록"이 의미 있는 문장이 됐습니다.
- 철회 기록을 남겼기에 틀린 결론이 다음 세션으로 전파되지 않았습니다.

모든 생성 자산의 프롬프트·도구·프로바이더·검증 수치는 `docs/provenance/`
22개 파일에 남아 있고, 이 문서의 모든 수치는 그 파일들과 저장소 실측에서
가져왔습니다.
