---
title: "Abyssal Lantern — Hold the Cinder Court (Unity)"
subtitle: "NAN 2026 Game X AI 해커톤 사전 과제 · 제출물 3. 게임 소개 및 설명"
author: "Hong팀 · 정장영 · 이석민 · 정우영"
lang: ko
---

# 1. 게임 제목 및 한 줄 소개

**Abyssal Lantern — Hold the Cinder Court**

> 마지막 등불을 든 Dusk Warden이 되어, 등불의 기름을 태워 Ember Cohort의
> 밀려오는 파도로부터 심연의 재의 법정을 지켜내는 2.5D 아이소메트릭 아레나
> 디펜스 — 이제 **Unity 6 / WebGL 3D 캐릭터 빌드**로.

브라우저 링크 하나로 즉시 실행되는 웹 게임입니다. 설치, 로그인, 계정,
네트워크 연결이 모두 필요 없습니다. 이번 빌드는 원작 Canvas 2.5D 프로토타입의
**수치 계약을 그대로 보존**한 채, Unity + URP + Humanoid 3D 캐릭터로
재구현했습니다.

## 페이지 구성

| 페이지 | 내용 |
|---|---|
| `/` (index) | 아레나 방어전 — 무한 웨이브 (원작 Cinder Court 규칙) |
| `/campaign.html` | **메인 캠페인 허브** — 심연 3구역 스테이지 선택·진행도·장비 |

캠페인은 구역마다 웨이브 + 경계 보스전으로 구성되며, 보스를 꺾어야 다음
구역이 열립니다. 진행도·장비는 localStorage에 저장됩니다 (서버 전송 없음).

---

# 2. 게임 방법

## 2.1 목표

**아레나**: 끝없이 증원되는 Ember Cohort를 격파하며 웨이브를 최대한 깊게
밀어내는 것. **캠페인**: 각 구역의 웨이브를 정리하고 경계 보스를 처치해
심연 3구역(Cinder Span → Abyss Chancel → Echo Throne)을 모두 정화하는 것.

핵심 긴장은 **등불 기름(Lantern oil)의 배분**에 있습니다. 기름은 초당 7씩
자동 회복되고 처치당 6이 추가로 들어오지만, 두 개의 권능이 그 기름을
경쟁적으로 소비합니다. 언제 태우고 언제 아끼는가가 한 판의 실력입니다.

![Cinder Court 코어 루프](assets/core-loop.svg)

## 2.2 조작

| 입력 | 동작 |
|---|---|
| `W` `A` `S` `D` 또는 방향키 | Dusk Warden 이동 |
| `Space` | 근접 공격 (Strike) |
| `Q` | **Ember Nova** — 반경 안의 모든 적에게 광역 피해 |
| `E` | **Lantern Ward** — 3초간 모든 피해 무효화 |
| `R` | 즉시 재시작 (Rekindle) |
| 화면 방향 패드 / 타격 버튼 | 터치 조작 (모바일 동등 입력) |
| 스킬 카드 클릭·탭 | Ember Nova / Lantern Ward 발동 |

## 2.3 전투 규칙 (실제 구현 수치 — 원작 계약 보존)

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

## 2.4 캠페인 — 스테이지·기믹·장비

| 구역 | 웨이브 | 경계 보스 | 던전 기믹 |
|---|---|---|---|
| Cinder Span | 5 + 보스전 | Cinder Warden | 잿불 분출구 ×2 (주기 폭발, 예고 후 8 피해) |
| Abyss Chancel | 6 + 보스전 | Veil Tactician | 흑요석 기둥 ×3 (이동 차단) + 분출구 ×1 |
| Echo Throne | 7 + 보스전 | Gate Sovereign | 유물 제단 (1.2 s 체류 → 기름 +18) + 분출구 ×2 |

- 보스: 체력 ×6, 접촉 피해 ×2, 이동 ×0.7, 크기 ×1.6. 호위대 동반 등장.
- **장비 파편 3슬롯** — 무기(공격 +6 %/랭크), 랜턴(기름 재생 +8 %/랭크),
  망토(체력 +8/랭크). 랭크 0–5.
  - 보스 처치: 파편 확정 드롭 (슬롯은 구역 순환).
  - 일반 처치: 결정적 규칙(7체 중 1)으로 파편 낙하, 회수 시 랭크 상승.
- 스테이지 클리어 시 진행도·장비가 저장되어 다음 강하에 이어집니다.
- 분출구는 예고(0.8 s 점멸) 후 폭발하며, Lantern Ward로 무효화할 수 있습니다.

## 2.5 종료 조건

체력이 0이 되면 런이 종료됩니다. 게임 오버 패널이 최종 점수·웨이브·유물·처치
수를 제시하고, `R` 또는 재점화 버튼으로 즉시 다시 시작합니다. 캠페인에서는
보스 처치 시 "구역 정화" 패널이 뜨고 허브로 돌아가 다음 구역을 엽니다.

런 결과는 `localStorage`의 `abyssal-lantern:cinder-court:last-run` 키에
다이제스트로 남습니다 (원작과 동일 키). 서버 전송은 없습니다.

---

# 3. 실행 방법

## 3.1 플레이 링크 (권장)

<https://akillness.github.io/hongT/> — 아레나 방어전
<https://akillness.github.io/hongT/campaign.html> — 메인 캠페인

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
bash tools/unity_batch.sh tests   # EditMode 30 테스트 (아레나 20 + 캠페인 10)
```

결정론 심(순수 C#)은 Unity 밖에서도 `dotnet test`로 동일하게 검증됩니다.

---

# 4. 플레이 영상

- **저장소 원본**: `docs/nan2026/assets/video/nan2026-cinder-court-unity-play.mp4`
  — H.264 1280×854 30 fps, 48.1초
- **YouTube**: (제출 시 링크 기재)

영상은 **배포된 GitHub Pages 빌드를 실제 브라우저에서 실제 키 입력으로
플레이한 화면을 CDP 스크린캐스트로 그대로 캡처**한 것입니다. 프레임 합성·
보간·재생성이 없습니다.

---

# 5. 저장소

- **소스**: <https://github.com/akillness/hongT> (공개)
- 게임 전체 소스, Unity 프로젝트, 자산 파이프라인 도구, 커밋 기록 포함.

## 주요 파일

| 경로 | 역할 |
|---|---|
| `Assets/Scripts/Sim/CinderSim.cs` | 결정론 시뮬레이션 (60 Hz, 순수 C#) |
| `Assets/Scripts/Sim/CampaignTypes.cs` | 캠페인 스테이지·기믹·장비 계약 |
| `Assets/Scripts/View/` | 프레젠테이션 (HUD·VFX·오디오·카메라) |
| `Assets/Editor/` | 캐릭터 임포트·씬 생성·WebGL 빌드 자동화 |
| `tools/blender/reskin_character.py` | 3D 캐릭터 재스키닝 파이프라인 |
| `tools/audio/gen_sfx.py` | ElevenLabs SFX/BGM 생성 |
| `web/campaign.html` | 캠페인 허브 (정적 페이지) |
| `docs/SIM_SPEC.md` · `docs/SIM_SPEC_CAMPAIGN.md` | 동결 수치 계약 |
