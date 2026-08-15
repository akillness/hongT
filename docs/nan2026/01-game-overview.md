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

브라우저 링크 하나로 실행되며 설치·로그인·계정이 필요 없습니다. 게임 자산과
구현물은 자체 AI 생성·AI 에이전트 제작 파이프라인으로 제작했고, Unity 빌드에는
팀의 선행 AI 생성 자산을 재가공해 사용했습니다. 외부 레퍼런스는 아이디어 구상 단계에서만 참고했습니다.

## 1.1 플레이 문법 요약

이 빌드의 전투 문법은 “맞히는 게임”보다 **판단하는 게임**에 가깝습니다.

- `[TARGET]` **Read**: 활성 공격자, 방의 룰, 수호자 위치, 안전한 통로를 먼저 읽습니다.
- `[TARGET]` **Commit**: Committed Strike, Lantern Dodge, Witness Guard 중 하나를 고르고 다른 답을 포기합니다.
- `[TARGET]` **Turn**: current, wall, pylon, altar, guardian angle 같은 코트 장치로 국면을 바꿉니다.
- `[TARGET]` **Adjudicate**: 처치·정화·보상·다음 방 선택으로 판결을 닫습니다.

이 구조는 단순한 액션 숫자 싸움이 아니라, 매 교전마다 “왜 이 답을 골랐는가”를 읽게 만드는 것을 목표로 합니다.

## 1.2 콘텐츠 구성

- **프롤로그**: 세계관과 기본 조작을 익히는 도입부
- **캠페인**: 순서대로 해금되는 9개 던전과 다음 방 준비 단계인 Ember Rest
- **아레나**: 원작 Cinder Court 수치 규칙을 보존한 무한 웨이브 모드

진행도·장비·메타 성장 데이터만 브라우저 localStorage에 저장되며, Ember Rest
선택은 다음 방에만 적용됩니다.

---

# 2. 게임 방법

## 2.1 목표

**아레나**: 끝없이 증원되는 Ember Cohort를 격파하며 웨이브를 최대한 깊게
밀어내는 것. **캠페인**: 프롤로그 뒤 Cinder Span → Ember Gallery → Abyss
Chancel → Witness Well → Echo Throne → Ash Verdict → Cinder Sluice →
Ember Bastion → Ash March를 순서대로 정화하는 것.

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
| `Enter` | 텍스트 커맨드 콘솔 — 수호자 명령(집중공격/방어/복귀) 및 스킬 시전 명령(노바/결계/파동/화살/질주) |
| 결과 패널 버튼 | 던전 재도전 또는 로비 복귀 |

4장 플레이 영상은 이 표의 세 가지 입력 계열을 순서대로 시연합니다: (a) **일반
공격**(Space 콤보), (b) **스킬 핫키 로테이션**(Q/E/Shift/F/R), (c) **텍스트
커맨드 콘솔**(수호자 집중공격/방어 명령 + 결계/노바 스킬 시전).

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

## 2.4 캠페인 — 9단계·Ember Rest·장비

| 순서 | 스테이지 | 보스 | 던전 기믹 |
|---|---|---|---|
| 1 | Cinder Span | Cinder Warden | 잿불 분출구 ×2 |
| 2 | Ember Gallery | Cinder Warden | 잿불 분출구 ×3 + 흑요석 기둥 ×1 |
| 3 | Abyss Chancel | Veil Tactician | 흑요석 기둥 ×3 + 잿불 분출구 ×1 |
| 4 | Witness Well | Veil Tactician | 유물 제단 ×1 + 흑요석 기둥 ×2 + 잿불 분출구 ×1 |
| 5 | Echo Throne | Gate Sovereign | 유물 제단 ×1 + 잿불 분출구 ×2 |
| 6 | Ash Verdict | Gate Sovereign | 유물 제단 ×1 + 잿불 분출구 ×3 |
| 7 | Cinder Sluice | Sluice Keeper | 조류 레인 + 해류 숙달 |
| 8 | Ember Bastion | Bastion Sentinel | 적 보호 불씨 기둥 + 방벽 숙달 |
| 9 | Ash March | Ash Magistrate | 수렴 잿벽 + 최종 집행 |

- 보스 웨이브는 각 스테이지의 고정 수치 계약을 계승하며, 보스는 체력 ×6,
  접촉 피해 ×2, 이동 ×0.7, 크기 ×1.6과 호위대를 사용합니다.
- **Ember Rest**: 1–8단계 정화 뒤 결과 패널 없이 즉시 제시되는 결정론적 준비
  제안 3개 중 하나를 선택하거나 건너뛸 수 있습니다. 선택한 효과는 바로 다음
  던전 한 방에만 적용되고, 저장·재도전·이후 스테이지로 이월되지 않습니다.
- 9번째이자 마지막 스테이지인 Ash March 정화 뒤에는 Ember Rest를 열지 않고
  최종 결과 오버레이를 표시합니다. 이 패널에서 재도전 또는 명시적 로비 복귀를
  선택합니다.
- **장비 3슬롯** — 무기(공격 +6 %/티어), 랜턴(기름 재생 +8 %/티어),
  망토(체력 +8/티어). T0–T5이며, 보스 처치와 인런 파편 드롭·로비 유물 구매로
  성장합니다.
- 활성 Lantern Ward는 분출구 펄스 피해를 무효화하며, 기존 피해 유예는 유지됩니다.

## 2.5 종료 조건

체력이 0이 되면 런이 종료됩니다. 결과 오버레이는 점수·처치·획득 요약과
재도전·로비 복귀를 제공합니다. 1–8단계 보스 처치는 결과 요약을 표시하지 않고
즉시 Ember Rest로 이어집니다. Ash March 정화만 최종 결과 오버레이를 표시하며,
로비 전환은 플레이어가 그 패널에서 선택할 때만 발생합니다.

캠페인 진행·장비·메타 데이터는 localStorage의
`abyssal-lantern:unity:campaign`에 저장됩니다. Ember Rest 선택은 저장하지
않으며, 서버 전송은 없습니다.

---

# 3. 실행 방법

## 3.1 플레이 링크 (권장)

<https://akillness.github.io/hongT/> — 로비 / 프롤로그 / 9단계 캠페인
<https://akillness.github.io/hongT/?mode=arena> — 무한 웨이브 아레나

최신 Chrome / Edge / Safari / Firefox에서 동작하며, 모바일 브라우저에서는
화면 방향 패드와 타격 버튼으로 조작합니다.


---

# 4. 플레이 영상

**YouTube**: [abyssal lantern HongT](https://www.youtube.com/watch?v=u2o0DA3Gqcs)  
YouTube 표시 길이: **1분 20초**



---

# 5. 저장소

- **소스**: <https://github.com/akillness/hongT> (공개)
- 게임 전체 소스, Unity 프로젝트, 자산 파이프라인 도구, 커밋 기록 포함.

## 주요 파일

| 경로 | 역할 |
|---|---|
| `Assets/Scripts/Sim/CinderSim.cs` | 결정론 시뮬레이션 (60 Hz, 순수 C#) |
| `Assets/Scripts/View/StageCatalog.cs` | 9단계 캠페인 카탈로그와 해금 순서 |
| `Assets/Scripts/View/` | 로비·HUD·Ember Rest·프레젠테이션 |
| `Assets/Editor/` | 휴머노이드 캐릭터 임포트·씬 생성·WebGL 빌드 자동화 |
| `Assets/Tests/EditMode/CharacterRosterAnimationTests.cs` | 유효 Humanoid Avatar·공유 컨트롤러·공격 오른손 모션 게이트 |
| `tools/blender/reskin_character.py` | 3D 캐릭터 재스키닝 파이프라인 |
| `docs/SIM_SPEC.md` · `docs/SIM_SPEC_CAMPAIGN.md` · `docs/SIM_SPEC_HACKSLASH.md` | 동결 수치·캠페인·핵앤슬래시 계약 |
