<div align="center">

![Abyssal Lantern — 잿불의 법정을 지켜라](docs/assets/readme/title.png)

[![Play](https://img.shields.io/badge/▶%20지금%20플레이-akillness.github.io%2FhongT-ff9a52?style=for-the-badge&logo=googlechrome&logoColor=white)](https://akillness.github.io/hongT/)

[![Version](https://img.shields.io/badge/version-v0.2.0-2cadd6?style=flat-square)](docs/RELEASE_NOTES.md)
[![Unity](https://img.shields.io/badge/Unity-6000.5.6f1%20URP-000000?style=flat-square&logo=unity)](https://unity.com)
[![WebGL](https://img.shields.io/badge/target-WebGL-8f67ff?style=flat-square)](https://akillness.github.io/hongT/)
[![Tests](https://img.shields.io/badge/EditMode-890%20passed-3fb950?style=flat-square)](Assets/Tests/EditMode)
[![Deploy](https://img.shields.io/badge/Pages-gh--pages-24292f?style=flat-square&logo=github)](https://github.com/akillness/hongT/deployments)

</div>

마지막 등불을 든 **황혼의 파수꾼**이 되어, 등불의 기름을 태워 잿불 군단의 파도를
밀어내고 심연의 세 구역을 정화하는 **2.5D 핵앤슬래시 아레나 디펜스**입니다.
링크 하나로 즉시 시작 — 설치도, 로그인도, 서버도 없습니다.

<div align="center">

![게임플레이](docs/assets/readme/gameplay.gif)

*Ember Gallery · 웨이브 2/5 — 현재 빌드 실제 플레이*

**[▶ 전체 플레이 영상 (55초)](docs/nan2026/assets/video/nan2026-cinder-court-cycle13-final.mp4)** ·
[인트로 시네마틱](docs/nan2026/assets/video/cinder-court-intro.mp4)

</div>

---

## 한눈에 보기

| | |
|---|---|
| **장르** | 2.5D 핵앤슬래시 아레나 디펜스 (싱글플레이) |
| **플랫폼** | 브라우저 (WebGL) · 데스크톱 + 모바일 터치 |
| **분량** | 9구역 3막 · 스테이지당 5웨이브 · 막마다 3페이즈 보스 |
| **핵심 재미** | 기름을 태워 화력을 사는 자원 압박 + 3타 콤보 · 대시 · 스킬 4종 |
| **원작** | [Abyssal-Lantern](https://github.com/jellyggumi/Abyssal-Lantern) (Canvas 2.5D, NAN 2026) — 수치 계약을 보존한 Unity 재구현 |

## 어떻게 노는 게임인가

**등불은 무기이자 자원입니다.** 기름은 초당 7씩만 차오르고 적을 처치하면 6이
추가됩니다. 노바 한 방이 45, 방벽이 30 — 지금 쓸지 아낄지가 매 웨이브의 결정입니다.

1. **로비** — 라이브 3D 배경 위에서 성장·장비·동료를 정비하고 강하합니다.
2. **프롤로그** — 탑다운 시점으로 조작과 기름 경제를 익힙니다. 클리어하면
   카메라가 55°로 내려오며 2.5D 던전이 열립니다.
3. **던전** — 3타 콤보와 대시로 붙고, 원소 상성으로 약점을 찌르고, 정예를 처치해
   **동료로 추출**합니다. 각 막의 끝에는 3페이즈 보스가 기다립니다.

## 조작

| 입력 | 던전 | 아레나 / 프롤로그 |
|---|---|---|
| `WASD` · 방향키 | 이동 | 이동 |
| `Space` | **3타 콤보** (3타에 넉백) | 타격 |
| `Shift` | **대시** — 무적 0.22초, 기름 8 | — |
| `Q` `E` `R` `F` | 균열 화살 · 묘지 파동 · 잿불 노바 · 공허 방패 | `Q` 노바 · `E` 방벽 · `R` 재시작 |
| 터치 | 가상 패드 + 스킬 카드 | 가상 패드 + 버튼 |

## 수치 계약

시뮬레이션은 **60 Hz 고정스텝 순수 C#** 이며 난수를 쓰지 않습니다. 같은 입력은
언제나 같은 결과를 냅니다 — 아래 식이 그 계약의 전부입니다.

**아이소메트릭 거리** — 화면이 기울어져 있으므로 세로 거리를 $1.42$ 배로 셉니다.
사거리 판정은 모두 이 거리로 이뤄집니다.

$$d(A,B)=\sqrt{(x_A-x_B)^2+\bigl(1.42\,(y_A-y_B)\bigr)^2}$$

**전방 판정** — 타격은 바라보는 쪽으로만 들어갑니다. $f=\pm 1$ 은 바라보는 방향,
$-18$ 은 등 뒤 약간까지 허용하는 여유값입니다.

$$(x_{\text{target}}-x_{\text{self}})\cdot f \;\ge\; -18$$

**등불 기름** — 시간당 회복과 처치 보상이 함께 차오르고 $100$ 에서 멈춥니다.
$k$ 는 누적 처치 수입니다.

$$O(t)=\min\bigl(100,\; O_0 + 7t + 6k\bigr)$$

**원소 상성** — `ember → frost → veil → void → ember` 순환입니다. 스킬 피해에만
적용되며 평타에는 적용되지 않습니다.

$$
\text{dmg} = \text{base}\times
\begin{cases}
1.20 & \text{advantage}\\
1.00 & \text{neutral}\\
0.85 & \text{disadvantage}
\end{cases}
$$

우위 $+20\%$ · 열세 $-15\%$ 입니다.

**타격 활성 구간** — 공격 모션 전체가 아니라 이 구간에 있는 적만 맞습니다.
$12\,\text{fps}$ 포즈 기준 2–4프레임, 즉 $0.167\text{–}0.333$ 초입니다.

$$t_{\text{active}} \in \left[\tfrac{2}{12},\ \tfrac{4}{12}\right]\ \text{s}$$

전체 수치표는 [docs/SIM_SPEC.md](docs/SIM_SPEC.md) · [캠페인 증보](docs/SIM_SPEC_CAMPAIGN.md) ·
[핵앤슬래시 증보](docs/SIM_SPEC_HACKSLASH.md).

## 지표

| 항목 | 값 | 계약 |
|---|---:|---|
| EditMode 테스트 | **890 통과 / 891** | 실패 0 (1건 명시적 skip) |
| WebGL 빌드 크기 | **85 MB** | ≤ 120 MB |
| 시뮬레이션 틱 | **60 Hz** 고정스텝 | 결정론 · RNG 없음 |
| 동시 적 상한 | **20** | `EnemyCap` |
| 아레나 반경 | **520 × 270** | 중심 (768, 604) |
| 캐릭터 폴리곤 | ≤ **25k** tri | WebGL 예산 |
| 텍스처 상한 | **1024** px | WebGL 예산 |
| 스테이지 | **9** 구역 · 3막 | 보스 3종 |
| 모션 클립 | **14** 종 | Humanoid 리타겟 |
| 저장 | localStorage | 서버 전송 없음 |

## 시스템 하이라이트

- **결정론 시뮬레이션** — `CinderCourt.Sim` 은 UnityEngine을 참조하지 않는 순수
  C# 어셈블리라 Unity 없이 `dotnet` 만으로도 전체 검증이 가능합니다.
- **정예 추출** — 7번째 스폰마다 나오는 정예를 처치하고 시체 곁에서 2초 채널하면
  **적이 동료가 됩니다**.
- **던전 기믹** — 잿불 분출구(주기 AoE) · 흑요석 기둥(이동 차단) · 유물 제단(버프).
  배치는 스테이지별로 결정적입니다.
- **난이도 4단계** — 받는 피해, 적 공격 간격, 동시 공격 인원, 그룹 AI가 함께
  움직입니다. **어려움 이상**에서 적은 8슬롯 포위 링으로 교대하며 측·후방에서
  먼저 들어옵니다.
- **타격감** — 적중 0.028초 · 처치 0.045초 · 피니셔 0.075초의 히트스톱과 카메라
  펀치가 하나의 예산에서 해소됩니다. 모션 약함 설정에서는 꺼집니다.

자세한 스테이지별 공략은 [docs/DUNGEON_GUIDE.md](docs/DUNGEON_GUIDE.md).

## 빌드

```bash
# Unity 6000.5.6f1 (URP 17.5) 필요
bash tools/unity_batch.sh method CinderCourt.EditorTools.CharacterImportPipeline.ImportAll
bash tools/unity_batch.sh tests      # EditMode 전체
bash tools/unity_batch.sh build      # → build-webgl/
python3 -m http.server 4173 --directory build-webgl
```

자산 파이프라인(캐릭터 리깅·모션·오디오·폰트·배포)은
[CLAUDE.md §3](CLAUDE.md) 의 자산 클래스별 도구 표를 따릅니다.

## 문서

[릴리즈 노트](docs/RELEASE_NOTES.md) ·
[던전 가이드](docs/DUNGEON_GUIDE.md) ·
[수치 계약](docs/SIM_SPEC.md) ·
[NAN 2026 제출 문서](docs/nan2026/)

## 팀

**Hong팀** — 정장영 (기획·게임구현·리소스제작) · 이석민 (기획·UI·QA) ·
정우영 (기획·QA)
