---
title: "플레이 콘셉트 부록"
subtitle: "Achilles: Legends Untold 스토어 페이지를 기준으로 한 HongT PRD 수준 플레이 개념 정리"
author: "Hong팀"
lang: ko
---

# 0. 목적과 경계

- `[OBSERVED]` 기준 소스는 Steam 공식 스토어 페이지 `Achilles: Legends Untold`이며, 캡처 시각은 `2026-08-09`이다.
- `[OBSERVED]` 페이지는 개발자 설명, 태그, 기능, 리뷰 요약을 제공하지만 런타임 수치나 실제 밸런스 결과는 제공하지 않는다.
- `[TARGET]` 이 문서는 외부 제목을 따라하지 않고, HongT의 Cinder Court를 더 명확한 PRD 문법으로 다듬기 위한 **플레이 질문**만 추출한다.

## 0.1 외부 기준에서 읽힌 것

| Status | Source claim | What it means for design |
|---|---|---|
| `[OBSERVED]` | 2023-11-02 정식 출시, Dark Point Games 개발, Dark Point Games/Galaktus 배급 | 상용 액션 RPG의 메인 라인으로 읽힌다. |
| `[OBSERVED]` | 최근 평가 56개 중 76% 긍정, 전체 2,093개 중 78% 긍정 | 전달력과 경험 만족이 평균 이상이지만, 세부 원인은 페이지 단독으로 알 수 없다. |
| `[OBSERVED]` | 스토어 태그는 소울라이크, 액션 RPG, 전투, 핵 앤 슬래시, 탐험, 약탈, 인벤토리 관리, 싱글 플레이어, 온라인 협동 등을 포함 | 전투 압박, 빌드 선택, 환경 탐험, 협동 외형이 동시에 보인다. |
| `[OBSERVED]` | 설명은 stamina-based combat, dodge/block/strike, divine skills, constellation-style skill tree, one-on-one duels, handcrafted world, coordinated AI, boss fights를 강조 | 전투의 핵심은 "읽기 → 답 고르기 → 보상 받기"의 반복이다. |
| `[OBSERVED]` | 개발자 성인 콘텐츠 설명은 보스 피니셔의 줌/슬로모션을 명시 | 임팩트 연출이 전투 피드백의 일부라는 뜻이지, 특정 컷을 복제하라는 뜻은 아니다. |

## 0.2 HongT로 옮길 수 있는 질문

1. `[INFERENCE]` 공격을 눌렀을 때, 플레이어가 실제로 **다른 선택을 포기했다**고 느끼는가?
2. `[INFERENCE]` 적 압박이 "많다"가 아니라 "누가 먼저 말했는지 알 수 있다"로 읽히는가?
3. `[INFERENCE]` 빌드가 숫자만 바꾸는가, 아니면 플레이어가 쓰는 문법 자체를 바꾸는가?
4. `[INFERENCE]` 보스가 체력 덩어리가 아니라, 룸 규칙과 한 번씩 협상하는 직책처럼 보이는가?
5. `[INFERENCE]` 환경이 배경색이 아니라, 결정의 일부로 사용되는가?

# 1. HongT 플레이 문법

`[TARGET]` HongT의 최종 문법은 다음 한 문장으로 요약된다.

> **읽고(Read), 커밋하고(Commit), 국면을 바꾸고(Turn), 판결을 받는다(Adjudicate).**

`[TARGET]` 이 문법은 `design/concept.md`, `design/core-loop.md`, `design/presentation-spec.md`, `design/worldview.md`의 합성본이다. 이 부록은 그 문법을 제출용 언어로 더 읽히게 정리한다.

## 1.1 전투 답안 3종

- `[TARGET]` **Committed Strike**: 명중이 아니라 "지금 이 답을 썼다"는 선언이다. Recovery가 존재해야 한다.
- `[TARGET]` **Lantern Dodge**: 생존 이동이 아니라 "공간을 지불해 답을 바꾼다"는 선언이다. Oil과 cooldown이 비용이다.
- `[TARGET]` **Witness Guard**: future-facing answer다. 방어가 답이 되려면, 읽을 수 있는 cue와 반격 가능한 창이 있어야 한다.

`[TARGET]` 이 세 답은 서로를 잠식하지 않는다. 하나가 강해질수록 다른 하나의 가치가 커지는 구조가 되어야 한다.

## 1.2 빌드 문법

- `[TARGET]` 빌드는 장비 희귀도가 아니라 **Force / Mobility / Testimony**의 비중으로 읽힌다.
- `[TARGET]` Force는 확정 타격과 방어선 붕괴를, Mobility는 위험 회피와 위치 전환을, Testimony는 수호자 각도와 룸 판결을 강화한다.
- `[TARGET]` 좋은 빌드는 숫자만 올리지 않고, 플레이어가 선호하는 답의 순서를 바꾼다.

## 1.3 적과 방의 문법

- `[TARGET]` 적의 압박은 개별 몬스터가 아니라 **court order**처럼 읽혀야 한다.
- `[TARGET]` 공격 토큰 보유자, flank, guardian angle, hazard phase가 하나의 문장으로 읽혀야 한다.
- `[TARGET]` 방은 장식이 아니라 판결 장치다. current, wall, pylon, altar, pillar는 모두 답을 바꾸는 규칙이어야 한다.

# 2. 다섯 사이클 개념 진화

| Cycle | Stance | What changes | Exit proof |
|---|---|---|---|
| `[TARGET]` 9 | Baseline | 현재 결정론 코트와 빌드가 이미 충분히 인상적인지 측정한다. | G8 impression now, G5 parity remeasure, one implementation owner named. |
| `[TARGET]` 10 | Committed verdicts | Strike / dodge / guard의 기회비용을 명시한다. | threat windows expose ≥2 answers, no answer >60% share, digest stable. |
| `[TARGET]` 11 | Ordered host | token handoff, flank, guardian angle, two-sided hazards를 한 몸처럼 보이게 한다. | ≥70% waves show handoff + reposition, salience cap respected. |
| `[TARGET]` 12 | Authored build / named duel | 빌드가 보스 답안과 연결되도록 만든다. | ≥3 viable archetypes, answer identification ≥70%, impression ≥4/5. |
| `[TARGET]` 13 | Final judgment | 성능, 접근성, 연출, 문서, 영상, 위키를 제출 상태로 묶는다. | G4/G6/G1 final, no unresolved readability defects. |

`[TARGET]` 이 구조는 새로운 장르를 도입하라는 뜻이 아니다. **같은 코트를 더 잘 읽히게 만드는 순서**를 정한 것이다.

# 3. PRD 수준 수용 기준

## 3.1 전투

- `[TARGET]` 적어도 하나의 교전에서 플레이어는 "왜 맞았는지"를 말할 수 있어야 한다.
- `[TARGET]` 적어도 하나의 교전에서 플레이어는 "왜 지금 공격/회피/방어 중 하나를 골랐는지"를 말할 수 있어야 한다.
- `[TARGET]` 공격 판정은 피해가 아니라 **판단의 결과**처럼 보여야 한다.

## 3.2 읽기성

- `[TARGET]` 활성 위협은 3개를 넘기지 않는다.
- `[TARGET]` 방 전환과 보스 전환은 항상 room rule을 먼저 보여준다.
- `[TARGET]` reduced motion에서도 정보는 빠지지 않는다. 움직임이 줄어도 판결의 형태는 남아야 한다.

## 3.3 성장

- `[TARGET]` 성장 보상은 전투의 답안을 바꾸어야 한다.
- `[TARGET]` 하나의 보상은 Force / Mobility / Testimony 중 하나를 강화하고, 최소 한 가지 전투 장면의 선택을 바꾸어야 한다.
- `[TARGET]` free path와 paid path는 이 우위를 깨지 않는 선에서만 설계된다.

## 3.4 연출

- `[TARGET]` 보스는 영웅의 이름이 아니라 **직책과 룸 규칙**으로 기억되어야 한다.
- `[TARGET]` 피니시 연출은 카메라 자랑이 아니라, "이 방의 판결이 끝났다"는 문장이다.
- `[TARGET]` 연출의 목적은 놀람이 아니라 **이해도 상승**이다.

# 4. 원본성 경계

- `[TARGET]` Achilles의 그리스 신화, 이름, 이야기, 아트, 레이아웃, 자산, 컷 연출, 슬로모션 타이밍은 사용하지 않는다.
- `[TARGET]` 대신 HongT의 코트, 등불, testimony, verdict, guardian, pylon, ash wall 같은 토착 용어만 쓴다.
- `[TARGET]` 외부 소스는 질문을 정교하게 만들 뿐, 숫자나 컷을 대신하지 않는다.

# 5. 연결 문서

- [게임 소개 및 설명](01-game-overview.md)
- [핵심 루프](../../_workspace/current/design/core-loop.md)
- [프레젠테이션 스펙](../../_workspace/current/design/presentation-spec.md)
- [개념 문서](../../_workspace/current/design/concept.md)
- [Achilles 벤치마크 분석](../../_workspace/current/design/trend-survey/achilles-analysis.md)
