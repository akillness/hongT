---
title: "Abyssal Lantern — Hold the Cinder Court (Unity)"
subtitle: "NAN 2026 Game X AI 해커톤 사전 과제 · 제출물 3. 게임 소개 및 설명"
author: "Hong팀 · 정장영 · 이석민 · 정우영"
lang: ko
---

# 1. 게임 제목 및 한 줄 소개

**Abyssal Lantern — Hold the Cinder Court**

> 마지막 등불을 든 Dusk Warden이 되어 심연의 재의 법정을 지켜내는 전투 게임 —
> **Unity 6000.5.6f1 / URP / WebGL** 기반의 3D 캐릭터 빌드입니다.

브라우저 링크 하나로 실행되며 설치·로그인·계정이 필요 없습니다. 이번 빌드는
원작 Canvas 2.5D 프로토타입의 수치 계약을 보존하는 아레나와, 6단계 3D 던전
캠페인을 하나의 Unity 씬 상태머신으로 제공합니다.

## 페이지 구성

| 페이지 | 내용 |
|---|---|
| `/` (index) | 기본 진입점 — 로비에서 프롤로그와 6단계 캠페인을 선택 |
| `/?mode=arena` | 원작 Cinder Court 규칙의 무한 웨이브 아레나 |
| `/campaign.html` | 이전 링크 호환용 — `/` 로 즉시 리다이렉트 |

캠페인은 프롤로그를 마친 뒤 순서대로 해금되는 6개 논리 스테이지입니다. 진행도·
장비·메타 성장 데이터만 localStorage에 저장되며, Ember Rest 선택은 저장되지
않는 다음 방 전용 준비 효과입니다.

---

# 2. 게임 방법

## 2.1 목표

**아레나**: 끝없이 증원되는 Ember Cohort를 격파하며 웨이브를 최대한 깊게
밀어내는 것. **캠페인**: 프롤로그 뒤 Cinder Span → Ember Gallery → Abyss
Chancel → Witness Well → Echo Throne → Ash Verdict를 순서대로 정화하는 것.

핵심 긴장은 **등불 기름(Lantern oil)의 배분**에 있습니다. 기름은 초당 7씩
자동 회복되고 처치당 6이 추가로 들어오지만, 두 개의 권능이 그 기름을
경쟁적으로 소비합니다. 언제 태우고 언제 아끼는가가 한 판의 실력입니다.

![Cinder Court 코어 루프](assets/core-loop.svg)

## 2.2 조작

| 입력 | 동작 |
|---|---|
| `W` `A` `S` `D` 또는 방향키 | Dusk Warden 이동 |
| `Space` | 아레나·프롤로그 기본 공격 / 던전 3타 콤보 |
| `Shift` | 던전 대시 (아레나·프롤로그에서는 비활성) |
| 던전 `Q` `E` `R` `F` | Rift Bolt / Grave Pulse / Ash Nova / Void Aegis |
| 아레나 `Q` `E` `R` | Ember Nova / Lantern Ward / Rekindle |
| 결과 패널 버튼 | 던전 재도전 또는 로비 복귀 |

## 2.3 아레나 전투 규칙 (동결 수치 계약)

| 항목 | 값 |
|---|---|
| 시뮬레이션 | 고정 스텝 60 Hz (순수 C# 결정론 심, `UnityEngine` 비참조) |
| 아레나 | 1536 × 1024 월드, 중심 (768, 604), 반경 520 × 270 |
| 워든 체력 / 이동속도 | 100 / 218 u·s⁻¹ |
| 워든 공격력 / 사거리 / 쿨다운 | 58 / 160 / 0.48 s |
| 적 체력 / 사거리 / 쿨다운 | 58 / 76 / 1.22 s |
| 동시 등장 상한 | 20 |
| 등불 기름 | 최대 100, 초당 +7, 처치당 +6 |
| Ember Nova | 45 기름, 쿨다운 6.5 s, 반경 250, 피해 96 |
| Lantern Ward | 30 기름, 쿨다운 9 s, 지속 3 s |
| 드롭 | Ember shard(+18 체력) / Oil flask(+35 기름) / Relic mote(+250 점수) |
| 드롭 수명 / 자력 반경 | 12 s / 78 |

**설계 의도 — 사거리 비대칭.** 워든의 사거리(160)는 코호트의 사거리(76)보다
두 배 넘게 깁니다. 무작정 파고들면 포위당해 죽고, 거리를 유지하며 치고 빠지면
한 대도 맞지 않고 정리할 수 있습니다.

![사거리 비대칭 — 스탠드오프 밴드](assets/range-asymmetry.svg)

**아이소메트릭 거리 판정.** 모든 전투 판정은 세로축에 1.42배 가중치를 적용한
거리(`hypot(dx, dy × 1.42)`)를 씁니다. 타격은 워든이 바라보는 방향
(`dx × facing ≥ -18`)에서만 성립합니다.

## 2.4 캠페인 — 6단계·Ember Rest·장비

| 순서 | 스테이지 | 보스 | 던전 기믹 |
|---|---|---|---|
| 1 | Cinder Span | Cinder Warden | 잿불 분출구 ×2 |
| 2 | Ember Gallery | Cinder Warden | 잿불 분출구 ×3 + 흑요석 기둥 ×1 |
| 3 | Abyss Chancel | Veil Tactician | 흑요석 기둥 ×3 + 잿불 분출구 ×1 |
| 4 | Witness Well | Veil Tactician | 유물 제단 ×1 + 흑요석 기둥 ×2 + 잿불 분출구 ×1 |
| 5 | Echo Throne | Gate Sovereign | 유물 제단 ×1 + 잿불 분출구 ×2 |
| 6 | Ash Verdict | Gate Sovereign | 유물 제단 ×1 + 잿불 분출구 ×3 |

- 보스 웨이브는 각 스테이지의 고정 수치 계약을 계승하며, 보스는 체력 ×6,
  접촉 피해 ×2, 이동 ×0.7, 크기 ×1.6과 호위대를 사용합니다.
- **Ember Rest**: 1–5단계 정화 뒤 결과 패널 없이 즉시 제시되는 결정론적 준비
  제안 3개 중 하나를 선택하거나 건너뛸 수 있습니다. 선택한 효과는 바로 다음
  던전 한 방에만 적용되고, 저장·재도전·이후 스테이지로 이월되지 않습니다.
- 6번째이자 마지막 스테이지인 Ash Verdict 정화 뒤에는 Ember Rest를 열지 않고
  최종 결과 오버레이를 표시합니다. 이 패널에서 재도전 또는 명시적 로비 복귀를
  선택합니다.
- **장비 3슬롯** — 무기(공격 +6 %/티어), 랜턴(기름 재생 +8 %/티어),
  망토(체력 +8/티어). T0–T5이며, 보스 처치와 인런 파편 드롭·로비 유물 구매로
  성장합니다.
- 활성 Lantern Ward는 분출구 펄스 피해를 무효화하며, 기존 피해 유예는 유지됩니다.

## 2.5 종료 조건

체력이 0이 되면 런이 종료됩니다. 결과 오버레이는 점수·처치·획득 요약과
재도전·로비 복귀를 제공합니다. 1–5단계 보스 처치는 결과 요약을 표시하지 않고
즉시 Ember Rest로 이어집니다. Ash Verdict 정화만 최종 결과 오버레이를 표시하며,
로비 전환은 플레이어가 그 패널에서 선택할 때만 발생합니다.

캠페인 진행·장비·메타 데이터는 localStorage의
`abyssal-lantern:unity:campaign`에 저장됩니다. Ember Rest 선택은 저장하지
않으며, 서버 전송은 없습니다.

---

# 3. 실행 방법

## 3.1 플레이 링크 (권장)

<https://akillness.github.io/hongT/> — 로비 / 프롤로그 / 6단계 캠페인
<https://akillness.github.io/hongT/?mode=arena> — 무한 웨이브 아레나

최신 Chrome / Edge / Safari / Firefox에서 동작하며, 모바일 브라우저에서는
화면 방향 패드와 타격 버튼으로 조작합니다.

## 3.2 소스에서 직접 빌드

```bash
git clone https://github.com/akillness/hongT.git
cd hongT
# Unity 6000.5.6f1 필요
bash tools/unity_batch.sh method CinderCourt.EditorTools.CharacterImportPipeline.ImportAll
bash tools/unity_batch.sh method CinderCourt.EditorTools.SceneBuilder.Build
bash tools/unity_batch.sh build          # build-webgl/ 생성
python3 -m http.server 4173 --directory build-webgl
# 브라우저에서 http://127.0.0.1:4173/ 열기
```

## 3.3 검증 실행

```bash
bash tools/unity_batch.sh tests   # EditMode 테스트 146/146 통과, 실패 0
```
Unity 6000.5.6f1 WebGL 빌드도 통과했습니다.

결정론 심(순수 C#)은 Unity 밖에서도 `dotnet test`로 동일하게 검증됩니다.

---

# 4. 플레이 영상

- **저장소 원본**: `docs/nan2026/assets/video/nan2026-cinder-court-unity-play.mp4`
  — H.264 1440×900 30 fps, 55.0초
- **YouTube**: (제출 시 링크 기재)

영상은 **배포된 GitHub Pages 빌드를 실제 브라우저에서 실제 키·마우스 입력으로
플레이한 화면을 그대로 녹화**한 것입니다 (`tools/video/capture-unity-play.mjs`,
Playwright 녹화 + CDP 입력, 프롤로그 클리어 저장 시드 후 복귀 플레이어 시점).
프레임 합성·보간·재생성이 없으며, 로비(성장/장비/군단 탭·6단계 출정 카드) →
캠페인 1단계 Cinder Span 강하 → 근접 전투·레벨업 → **동료 명령 콘솔**(Enter,
`shield`/`nova` 명령 → Void Aegis·잿불 노바 시전) → 웨이브 2 (점수 1,100·유물
2) → 함락 → 재강하까지 실제 게임 루프가 담겨 있습니다. headless 캡처 환경은
한글 IME 조합이 불가해 콘솔 명령은 파서의 영문 별칭을 사용했습니다 — 화면
피드백("잿불 노바 시전" 등)은 한국어 그대로입니다.

---

# 5. 저장소

- **소스**: <https://github.com/akillness/hongT> (공개)
- 게임 전체 소스, Unity 프로젝트, 자산 파이프라인 도구, 커밋 기록 포함.

## 주요 파일

| 경로 | 역할 |
|---|---|
| `Assets/Scripts/Sim/CinderSim.cs` | 결정론 시뮬레이션 (60 Hz, 순수 C#) |
| `Assets/Scripts/View/StageCatalog.cs` | 6단계 캠페인 카탈로그와 해금 순서 |
| `Assets/Scripts/View/` | 로비·HUD·Ember Rest·프레젠테이션 |
| `Assets/Editor/` | 휴머노이드 캐릭터 임포트·씬 생성·WebGL 빌드 자동화 |
| `Assets/Tests/EditMode/CharacterRosterAnimationTests.cs` | 유효 Humanoid Avatar·공유 컨트롤러·공격 오른손 모션 게이트 |
| `tools/blender/reskin_character.py` | 3D 캐릭터 재스키닝 파이프라인 |
| `docs/SIM_SPEC.md` · `docs/SIM_SPEC_CAMPAIGN.md` · `docs/SIM_SPEC_HACKSLASH.md` | 동결 수치·캠페인·핵앤슬래시 계약 |
