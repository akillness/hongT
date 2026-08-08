---
title: "AI 활용 기술 문서"
subtitle: "NAN 2026 Game X AI 해커톤 사전 과제 · 제출물 4 · Abyssal Lantern — Hold the Cinder Court (Unity 재구현)"
author: "Hong팀 · 정장영 · 이석민 · 정우영"
lang: ko
---

# 0. 요약

이 게임은 **Unity 6000.5.6f1 / URP / WebGL** 재구현이며, AI는 보조 도구가
아니라 **제작 파이프라인 그 자체**입니다. 3D 캐릭터 리스킨, 애니메이션 리타겟,
사운드, UI 아이콘, 지형 리맵, 게임 코드·검증, 제출용 플레이 영상 캡처까지 전부
AI 에이전트 워크플로 위에서 만들어졌습니다.

다만 **배포된 게임 런타임에는 기본적으로 AI가 실행되지 않습니다.** GitHub Pages로
배포된 빌드는 정적 WebGL 페이지이며, 외부 추론 호출·API 키·네트워크 요청이
없습니다. AI 산출물은 제작 시점에 결정론적 자산·코드·mp3로 고정됩니다. 심사자가
링크를 열었을 때 유료 API 비용이나 지연이 발생하지 않습니다.

**단 하나의 opt-in 예외**가 동료 명령 콘솔입니다. 기본 경로는 네트워크를 전혀
쓰지 않는 로컬 키워드 파서이고, 플레이어가 **자신의** Gemini API 키를 런타임에
직접 등록한 경우에만 미분류 자유 문장을 원격 분류합니다(§4).

| 축 | 도구 | 산출물 |
|---|---|---|
| 3D 캐릭터 재스키닝 | Blender 5.x headless + 에이전트 워크플로 | 휴머노이드 FBX (본히트 자동 웨이트), 근거 `docs/provenance/lantern-reaver-reskin.json` |
| 모션 | Mixamo 벤치 FBX → Unity Humanoid 리타겟 | 11액션 공유 라이브러리 (`idle move run hit bighit attack critical avoid defence die show`) |
| 사운드 | **ElevenLabs sound-generation API** | SFX + 로어 앰비언트 + BGM 루프, `Assets/Resources/Audio/*.mp3`, 근거 `docs/provenance/audio.json` |
| 게임 코드·검증 | 오케스트레이터(Claude) + 위임 레인 | 결정론 심·캠페인·View·애니메이션; EditMode 테스트 `Assets/Tests/EditMode/` |
| 플레이 영상 | Playwright 녹화 + CDP 실입력 + CompressO 압축 | 배포 빌드 실플레이 58 s, `tools/video/capture-unity-play.mjs` |
| 동료 명령 콘솔 | 로컬 키워드 파서(기본) + **선택적 Gemini 2.5 Flash Lite** 자유 문장 폴백 | 자연어 명령 → 닫힌 의도 집합 → 결정론 SimInput 래치, `Assets/Tests/EditMode/CompanionCommandParserTests.cs` |
| UI 아이콘 세트 | god-tibo-imagen + 마젠타 키 매팅 | 스킬·장비·스탯·픽업·앱·버튼 아이콘, `Assets/Resources/Icons/` |
| 스테이지 지형 | 원작 terrain GLB → Blender 헤드리스 FBX → URP Unlit 리맵 | 6단계 캠페인 지형 프리팹, `Assets/Resources/Terrain/` |
| 시각 오버홀 | Socratic 딥인터뷰 → 스펙 동결 → 레인별 게이트 | 원소 파티클·시전 글로우·URP 블룸/비네트, `VfxDirector.cs`·`PostFxGate.cs` |
| 브랜드 범퍼 | Remotion 프로그래매틱 렌더 | `assets/video/hongt-brand-bumper.mp4` |

**런타임 무 AI 원칙**은 오디오나 자산 생성 여부가 아니라 **추론 호출 부재**로
유지됩니다. ElevenLabs SFX·god-tibo 아이콘·Blender 리스킨은 모두 제작 시점 1회
생성이며 빌드에는 고정된 산출물만 들어갑니다.

---

# 1. 시스템 구조

## 1.1 런타임 구조 (AI 비의존)

| 계층 | 구성 요소 | 성격 |
|---|---|---|
| 시뮬레이션 | `Assets/Scripts/Sim/` (`CinderSim.cs`, `SimTypes.cs`, `CampaignTypes.cs`, `HackTypes.cs`) | 순수 C# 결정론 고정 60 Hz 심, `UnityEngine` 비참조 |
| 프레젠테이션 | `Assets/Scripts/View/` (`GameView.cs`, `GameDirector.cs`, `HudView.cs`, `ActorView.cs`, `VfxDirector.cs`, `PostFxGate.cs`) | 심 상태를 읽기만 하고 write-back 금지 |
| 입력 | `InputAdapter.cs` | 키보드 / 포인터 / 터치 / 화면 버튼 → 결정론 `SimInput` 부울 래치 |
| 오디오 | `AudioDirector.cs` | SimEvent → mp3 one-shot 큐 + BGM 루프 (음성 풀 + ±6% 피치 지터) |
| 저장 | `CampaignStore.cs` / `WebGLStorage.cs` | localStorage `abyssal-lantern:unity:campaign`, 서버 전송 없음 |

**결정론은 하드 인바리언트입니다.** 렌더러·오디오·VFX는 시뮬레이션 상태에
write-back할 수 없습니다. 이 경계는 EditMode 테스트(`CinderSimTests.cs`,
`CampaignSimTests.cs`, `HackSimTests.cs`)로 게이트됩니다.

## 1.2 제작 파이프라인 (AI 사용 구간)

![AI 제작 파이프라인](assets/ai-pipeline.svg)

제작 시점에만 동작하는 AI 구간은 (1) 캐릭터 리스킨/리타겟, (2) 사운드 생성,
(3) UI 아이콘·지형 리맵 이미지 생성, (4) 코드·검증 에이전트 워크플로,
(5) 플레이 영상 캡처입니다. 산출물은 모두 저장소에 커밋된 결정론적 파일입니다.

---

# 2. 이미지 생성 — UI 아이콘과 지형 리맵

배포 빌드가 실제로 로드하는 생성 이미지는 **UI 아이콘 세트**
(`Assets/Resources/Icons/` — 앱·장비·픽업·스킬·스탯·버튼)와 **URP 지형 프리팹**
(`Assets/Resources/Terrain/` — 6단계 캠페인 바닥·소품)입니다.

## 2.1 도구와 팔레트 규약

- 도구: **god-tibo-imagen** (컨셉/텍스처/아틀라스), **PerfectPixel `ppgen`**
  (2D 스프라이트/시트). 저장소 계약(`CLAUDE.md` §3)이 자산 클래스별 도구를
  고정합니다.
- 팔레트 규약: 매팅 키 컬러 충돌을 막기 위해 **마젠타 계열을 전면 금지**하고,
  아군(플레이어 등불 코어)의 시안과 적을 색으로 분리합니다. 아트 지시가 곧
  게임플레이 가독성 사양인 사례입니다.
- 모든 생성 산출물의 프롬프트·모델·출처는 `docs/provenance/`에 기록합니다.

## 2.2 지형 리맵

원작 terrain GLB를 Blender 헤드리스로 FBX 변환한 뒤 URP Unlit 머티리얼로
리맵해 6단계 캠페인이 공유하는 지형 프리팹으로 만들었습니다
(`terrain-cinder-span.prefab`, `terrain-abyss-chancel.prefab`,
`terrain-echo-throne.prefab` 등). 지형 파츠 무결성은
`Assets/Tests/EditMode/TerrainPartsTests.cs`가 검증합니다.

---

# 3. 3D 캐릭터 리스킨 · 애니메이션

원작의 절차적 영역분할 스키닝은 애니메이션 중 메시가 찢어지는 결함이 있어
**폐기**했습니다. Unity 포트는 표준 경로로 재구축했습니다.

1. 메시를 원본 저작 메시로 교체
2. Blender 본히트 자동 웨이트로 재스키닝 (근거 `docs/provenance/lantern-reaver-reskin.json`)
3. Unity Mecanim Humanoid로 Mixamo 11액션 클립을 리타겟

런타임 게이트는 프리팹의 유효 Humanoid Avatar·공유 액션 컨트롤러·활성
Animator·SkinnedMeshRenderer와 공격 시 오른손 모션을 검증합니다
(`CharacterRosterAnimationTests.cs`, `LanternReaverPrefabTests.cs`,
`ClipTableTests.cs`, `PoseResolveTests.cs`). 재현 파이프라인은
`docs/character-asset-pipeline.md`에 기록되어 있습니다.

---

# 4. 동료 명령 콘솔 — 유일한 opt-in AI

던전 중 `Enter`로 텍스트 콘솔을 열고 자연어 명령을 입력하면, **닫힌 의도
집합**으로 분류되어 키 입력과 동일한 결정론 `SimInput` 래치로 진입합니다.
설계 계약은 `Assets/Scripts/View/CompanionCommandParser.cs`에 있습니다.

## 4.1 기본 경로 — 로컬 키워드 파서 (네트워크 0)

`CompanionCommandParser.Parse`는 한국어 우선·순서 있는 키워드 규칙표를
스캔합니다(구체적인 규칙이 일반 규칙보다 먼저 매칭 — 예: "결계"가 일반 "방어"보다
먼저 SkillAegis에 걸림). 의도 집합:

| 의도 | 키워드(발췌) | 래치 |
|---|---|---|
| FocusAttack | 집중공격, 공격해, 잡아 | 동료 홀드 |
| Defend / Recall | 방어태세, 지켜, 복귀, 돌아와 | 동료 복귀 |
| SkillNova | 노바, 폭발, nova | Nova 시전 |
| SkillAegis | 결계, 방패, 실드, ward | Void Aegis 시전 |
| SkillPulse / SkillBolt / SkillDash | 파동, 화살, 질주 | 각 스킬 래치 |
| PickupInfo | 아이템, 획득, 주워 | (심 미지원 — 정직한 안내 토스트) |

피드백 문구는 **행위자에 대해 정직**합니다. 동료 명령은 "수호자: …"로,
스킬 시전은 "…시전"으로 표기합니다(심에는 동료 스킬이 없고 플레이어가 시전).
분류 정확성은 `Assets/Tests/EditMode/CompanionCommandParserTests.cs`가
게이트합니다.

## 4.2 opt-in 폴백 — Gemini 2.5 Flash Lite

로컬 파서가 Unknown을 반환하고 플레이어가 **자신의** Gemini API 키를 런타임에
등록한 경우에만("키 <key>" 콘솔 명령 또는 `#gemini=` URL 프래그먼트 — 프래그먼트는
서버 로그에 남지 않음) 미분류 자유 문장을 **Gemini 2.5 Flash Lite**
(`models/gemini-2.5-flash-lite:generateContent`)로 분류합니다. 구현은
`Assets/Scripts/View/GeminiCommandClient.cs`이며, 키는 빌드·저장소에 포함되지
않고 `KeyVault.Protect`로 난독화되어 PlayerPrefs에만 저장됩니다
(`KeyVaultTests.cs`).

응답은 **의도 단어 1개로 제한**되어 키 입력과 동일한 결정론 래치로만 진입합니다.
네트워크 실패는 Unknown으로 강등되어 정직한 토스트를 띄울 뿐 입력을 잠그지
않습니다. 즉 **시뮬레이션은 어떤 경로로도 AI 출력에 의존하지 않습니다.**

---

# 5. 사운드 — ElevenLabs 생성 후 고정

SFX·BGM·로어 앰비언트는 **ElevenLabs sound-generation API**로 제작 시점에 생성해
mp3로 고정하고 `Assets/Resources/Audio/`에 커밋했습니다. 생성 근거(엔드포인트,
생성 시각, 큐별 프롬프트·바이트·길이·promptInfluence)는
`docs/provenance/audio.json`에 기록되어 있습니다. 사용자 지시(2026-08-04)에 따라
**음성 내레이션은 넣지 않고 SFX + BGM만** 사용합니다.

`AudioDirector.cs`는 WebGL 오디오 계약을 지킵니다. 동일 파형이 위상 겹침으로
버징되는 문제를 막기 위해 6-보이스 라운드로빈 풀과 결정론적 ±6% 피치 지터
(`[0.94, 1.06]`, WebGL에서 `AudioSource.pitch`가 0을 넘지 않아야 함)를 씁니다.
지터 RNG는 View 전용이며 심 틱에 관여하지 않아 결정론 계약을 건드리지 않습니다
(`AudioPitchJitterTests.cs`).

---

# 6. 코드·검증에서의 AI 활용

## 6.1 에이전트 운영 계약

저장소 루트의 `CLAUDE.md`(및 이를 가리키는 `AGENTS.md`)가 모든 에이전트 세션의
단일 운영 계약입니다. 자유 형식 프롬프트가 아니라 **강제 규칙 문서**로 운용했습니다.

- 작업 산출물은 `_workspace/current/` 담당 레인에만 기록하고, 이전 사이클은
  `_workspace/archive/`로 동결한다.
- 모든 주장에 `[OBSERVED]` / `[INFERENCE]` / `[TARGET]`를 표시하고, 목표치를
  측정치로 위장하는 것을 금지한다. 파일 존재는 근거가 아니며 측정·명령·테스트
  결과를 인용한다.
- 렌더러는 시뮬레이션 상태에 write-back할 수 없다. 결정론은 불변 조건이다.
- Three.js/DOM 가이드는 이 저장소에 적용하지 않는다. **Unity + WebGL 전용**이다.

## 6.2 검증 하니스

| 명령 | 검증 내용 |
|---|---|
| `Unity -batchmode -runTests -testPlatform EditMode` | `Assets/Tests/EditMode/` 전체 — 결정론 심, 캠페인 라우트, 명령 파서, 애니메이션 클립, HUD 레이아웃, WebGL 텍스처 상한, 오디오 지터 등 |
| `-executeMethod CinderCourt.EditorTools.BuildScript.BuildWebGL` | WebGL 빌드 (`BuildScriptWebGlPostprocessTests.cs`가 후처리 검증) |
| `node tools/video/capture-unity-play.mjs` | 배포 빌드 실입력 플레이 + 실프레임 영상 캡처 (§7) |

에이전트에게 코드를 대신 쓰게 하는 것이 아니라, **사람이 놓치는 것을 측정으로
잡게** 하는 방식입니다.

---

# 7. 플레이 영상의 진정성

대회 규정은 *"AI를 이용한 조작·합성이나 타인 영상의 도용은 불가"*이며
*"실제 플레이 화면 그대로"*를 요구합니다. 제출 영상은 다음으로 이를 만족합니다.

![플레이 영상 캡처 경로](assets/capture-authenticity.svg)

## 7.1 채택하지 않은 방법

스크린샷을 이어붙이고 가상 커서·줌을 얹는 스크린샷-투-비디오 렌더링 방식은
사내 도구로 가능했지만 **의도적으로 배제**했습니다. 규정이 금지하는 합성에
해당하기 때문입니다.

## 7.2 실제 채택한 방법 — `tools/video/capture-unity-play.mjs`

1. 실제 Chromium이 배포 페이지 <https://akillness.github.io/hongT/>를 로드합니다.
2. 드라이버가 **CDP 입력 도메인**으로 키 입력을 보냅니다. 물리 키보드와 동일한
   경로이며 게임 상태를 직접 조작하는 코드는 없습니다.
3. 브라우저가 **실제로 렌더링한 페이지 프레임**을 그대로 녹화합니다.
   보간·리타이밍·합성·생성이 없습니다.
4. 후처리는 로딩 스플래시 헤드트림, 30 fps H.264 메자닌, **CompressO**
   (`compresso_ffmpeg`, crf 28 · slow · faststart) 압축뿐입니다.

## 7.3 영상이 담는 3가지 입력 패턴

영상은 다음을 순서대로 실플레이로 담습니다.

1. **일반 공격** — `Space` 콤보로 근접 코호트를 정리
2. **스킬 핫키 로테이션** — `Q` Rift Bolt → `E` Grave Pulse → `Shift` Dash →
   `F` Void Aegis → `R` Ash Nova
3. **텍스트 커맨드 콘솔** — `Enter`로 콘솔을 열고 수호자 명령(집중공격/방어)과
   스킬 시전 명령(결계 → Void Aegis, 노바 → Ash Nova)을 입력. 헤드리스
   Chromium에는 한글 IME가 없어 콘솔 명령은 파서의 문서화된 ASCII 별칭을 쓰지만,
   화면에 뜨는 피드백 문구는 한국어로 표시됩니다.

출력 사양: H.264 1440×900 30 fps, 58초. 산출물
`assets/video/nan2026-cinder-court-unity-play.mp4` (**YouTube 업로드 예정**).

---

# 8. 외부 에셋 / 오픈소스 출처

## 8.1 런타임 포함 외부 저작물

배포 빌드는 외부 폰트·이미지·오디오·JS 라이브러리를 별도로 포함하지 않습니다.
모든 자산은 본 프로젝트를 위해 생성한 오리지널이며, 근거는 `docs/provenance/`에
SHA/프롬프트/도구와 함께 기록되어 있습니다.

| 근거 파일 | 대상 |
|---|---|
| `docs/provenance/audio.json` | ElevenLabs 생성 SFX/BGM 큐 |
| `docs/provenance/lantern-reaver-reskin.json` | Blender 리스킨/리타겟 메시 |
| `docs/provenance/cinder-court-link-preview.json` | 링크 프리뷰 이미지 |
| `docs/character-asset-pipeline.md` | 캐릭터 재생성 절차 |

## 8.2 개발 전용 의존성 (배포물 미포함)

플레이 영상 캡처용 `playwright`(Apache-2.0)와 CompressO 내장 `ffmpeg`는 개발
도구이며 GitHub Pages 아티팩트에는 포함되지 않습니다.

## 8.3 사용한 AI 도구 전체 목록

| 도구 | 제공자 / 모델 | 용도 | 배포물 영향 |
|---|---|---|---|
| god-tibo-imagen | private-codex | UI 아이콘·지형 텍스처 생성 | PNG 자산 |
| PerfectPixel `ppgen` | god-tibo-imagen 어댑터 | 스프라이트/시트 | PNG + 매니페스트 |
| Blender 5.x headless | 오픈소스 | 리스킨/리타겟 FBX | 캐릭터 메시 |
| ElevenLabs sound-generation | ElevenLabs | SFX·BGM·앰비언트 | mp3 자산 |
| Claude 에이전트 워크플로 | Anthropic | 코드·테스트·문서·캡처 하니스 | 소스 코드 |
| Gemini 2.5 Flash Lite | Google | 동료 콘솔 opt-in 자유 문장 분류 | **런타임 opt-in만**(플레이어 키) |
| Remotion | 오픈소스 | 브랜드 범퍼 렌더 | 홍보 영상 |
