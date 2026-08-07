# 개선 브레인스토밍 — 게임 제작 관점 부족분 (View-only 우선)

2026-08-05 · subagent(planner×2) 딥리서치 통합 · 구현 대상 우선순위 = `Assets/Scripts/View/**` 만.
**Sim 불가침**: `Assets/Scripts/Sim/**`, `docs/SIM_SPEC*.md`, `// FROZEN CONTRACT` 파일 편집 금지.
심 변경이 필요한 항목은 `§S`에 격리하고 별도 AMENDMENT + EditMode 결정론 테스트 선행을 요구한다.

## 근거 (코드로 확인 [OBSERVED])

- `SimEvents`(SimTypes.cs:87-114)는 22비트 전부 "무엇이 일어났다"만 알린다 — 가해자/피해자 ID·위치
  필드가 하나도 없다. `ISimSnapshot`에도 `NovaX/NovaY`만 존재.
- 원소 플래시 휴리스틱: `GameView.cs`의 `ElementTintWindow=0.4f` 시간창 — 창 안의 **근접 콤보 타격도**
  원소색으로 오염된다(사용자 지적 = 코드 확인).
- **SFX 공백**: `AudioDirector.cs`는 클립 9종(`cue-strike/hit/kill/nova/ward/pickup/wave/gameover/lore`)
  + BGM뿐. 던전 킷 이벤트(Dash/Bolt/Pulse/LevelUp/Extraction/ComboFinisher…)는 전부 **기존 클립의 볼륨
  변주**이며 코드 주석이 "interim contract until dedicated cues land"라고 자백. `AudioSource` 단 1개,
  **풀링 없음·pitch 랜덤화 없음** → 같은 SFX 연타 시 위상 겹침으로 기계적으로 들린다.
- 딥링크/치트 부재: URL 파라미터 파싱 코드 0건. `HackConfig.TryDungeon`은 이미 public 진입점.
- `IHackSnapshot`은 텔레메트리에 필요한 지표(Level/Xp/ComboIndex/Cooldowns/Extraction/BossPhase)를
  전부 이미 노출 → 데이터 수집은 심 수정 0으로 가능.
- `SimInput` 주석이 "버퍼링은 심 밖에서" 라고 이미 계약 → 입력 버퍼/코요테타임은 InputAdapter 단독.

## 후보 (문제 → 개선안 → 계약 판정 → 우선순위)

| # | 문제 | 개선안 | 계약 판정 | 우선 |
|---|---|---|---|---|
| 1 | 던전 킷 SFX가 기존 클립 볼륨 변주뿐, 단일 AudioSource·풀 없음·pitch 고정 → 연타 시 위상 겹쳐 기계음 | AudioSource **풀(라운드로빈)** + **pitch 미세 랜덤화**로 연타 큐를 자연스럽게. 전용 클립 도입 전에도 손맛 즉시 개선 | **View-only, Sim 불변** | **TOP 1** |
| 2 | 헤드리스 QA가 보스 웨이브(웨이브 5)에 도달 못 함 → 보스 연출 화면 확인 불가 | `?mode=dungeon&stage=<id>` + `?wave=<n>` 딥링크(잠금검사 통과)로 QA가 특정 웨이브에서 시작 | View-only(`HackConfig` public) | TOP 2 |
| 3 | 입력 버퍼가 "다음 틱까지" 1틱뿐, 코요테/선입력 윈도 없음 → 콤보 마지막 프레임 입력 유실 | InputAdapter에 시간기반 선입력 버퍼(≈120 ms) | View-only(SimInput 계약이 명시) | TOP 3 |
| 4 | 밸런싱 근거 데이터 없음(체감뿐) | 런 종료 시 텔레메트리(웨이브별 처치/피격/쿨 사용) localStorage 기록 | View-only(`IHackSnapshot` 노출됨) | 중 |
| 5 | 원소 플래시가 근접타격 오염 | 정확 해결엔 가해자 정보 필요 → 근사 개선(스킬 창 짧게+근접 배제 휴리스틱)만 View 가능 | **§S: 정확 해결은 Sim 변경** | 중(근사만) |
| 6 | ~80 MB 초기 로드 | Resources 오디오/캐릭터 Addressables 분할 지연로드 | 빌드 설정 영역, 성격 다름 | 하([미확인] 실측 필요) |

## 이번 사이클 실구현 = 후보 1 (AudioSource 풀 + pitch 랜덤화)

가장 임팩트 큰 **View-only·Sim 불변·테스트 가능** 항목. Unity docs 근거는
`research/audio-time-input-unity-docs.md` 참조. 나머지 후보는 다음 사이클로 이월.

## VFX·화려함 서베이 ($survey) — 아킬레오스류 핵앤슬래시 웰메이드 룩

레퍼런스: Achilleos(스팀) 계열 아이소 핵앤슬래시의 "화려함"은 세 축으로 온다 —
(a) **가산 발광(additive/bloom)** 으로 스킬·타격 이펙트가 겹칠수록 밝아지고,
(b) **대시/스텝 잔상(afterimage)** 이 이동에 스피드감을 주고,
(c) 히트스톱·셰이크·데미지 넘버 등 **임팩트 피드백**이 타격을 무겁게 만든다.

이 저장소 [OBSERVED] 현황:
- (c) 임팩트 피드백은 대체로 성숙 — 셰이크(`CameraRig`), 데미지 넘버
  (`DamageNumberPool`), 원소 플래시(§K3), 스윙 트레일(§C1), 캐스트 글로우(§V1),
  파티클 4종(`VfxDirector`) 이 구현돼 있다. **채널 자체의 재제안은 금지.**
  - **[정정 2026-08-08]** "히트스톱도 성숙"은 틀렸다. 감사 결과 히트스톱과 카메라
    펀치는 **처치(0.04 s)와 콤보 피니셔(0.07 s)에만** 걸려 있었고 일반 근접
    적중(`SimEvents.EnemyHit`)에는 두 채널 모두 **전혀 없었다** — 평타가 통과하는
    느낌의 실제 원인. AMENDMENT #11 사이클에서 `Assets/Scripts/View/ImpactBudget.cs`
    로 Light 0.028 s / Kill 0.045 s / Finisher 0.075 s 티어를 단일 표로 통일하고,
    Light 에 0.14 s 재발동 간격을 둬 군집 연타 시 슬로우모션 눌어붙음을 막았다.
    근거 영상: <https://youtu.be/wbDv6nawEeY> — 리뷰어가 동종 게임에 70점 이상을
    주지 못한 유일한 이유가 타격감이었다.
- (a) 발광: `CinderPostProfile`에 URP **Bloom(threshold 1.05 / intensity 0.55)**
  이 이미 설정돼 있으나, 모든 VFX 머티리얼이 `MakeUnlit(...,transparent)` =
  **straight alpha(OneMinusSrcAlpha)** 라서 겹쳐도 밝아지지 않고 탁해진다 →
  블룸 문턱(1.05)을 넘기지 못해 발광이 죽어 있었다. **격차.**
- (b) 잔상: 대시(`SimEvents.DashUsed`)에 파티클 버스트만 있고 캐릭터 실루엣
  잔상이 없다. **격차.**

### 이번 사이클 실구현 (VFX = View-only·Sim 불변·테스트 가능)

1. **가산 발광 머티리얼** — `ViewWorld.MakeAdditive(color)` 신설: 검증된
   투명 시드(`unlit-transparent-seed`, WebGL 변형 스트리핑 생존)를 클론하되
   목적지 블렌드만 `One`으로 바꿔 **SrcAlpha/One 가산**. 겹치는 글로우가
   누적돼 Bloom 문턱을 넘는다. `VfxDirector`의 글로우류(Nova·Pulse 링,
   KitBurst, HitSpark, 원소 파티클, Bolt 스트릭)를 전부 가산으로 전환.
   지면 스코치(`SpawnScorch`)·픽업·체력바는 알파 유지(누적 발광이 부적절).
2. **대시 잔상(afterimage)** — `ActorView.TriggerAfterimages()`: 스킨드
   메시를 `BakeMesh`로 3장 월드 고정 스냅샷, 55 ms 간격, 0.28 s 가산 앰버
   페이드. `SimEvents.DashUsed`에서 트리거(GameView). 캡슐 폴백(스킨드 메시
   없음)·`ReducedMotion`은 전면 no-op. 풀 반환/파괴 시 베이크 메시·클론
   머티리얼까지 정리(누수 방지).

Unity docs 근거: URP Bloom은 HDR 임계값 초과 픽셀만 블룸 → 가산 누적이
문턱 통과의 정공법. `SkinnedMeshRenderer.BakeMesh`로 현재 포즈를 정적 메시로
스냅샷하면 애니메이션과 무관한 월드 고정 잔상을 얻는다.

### 검증 (EditMode)
`AdditiveMaterialTests`(신규 3종): SrcAlpha/One 블렌드+ZWrite 0 계약,
`MakeUnlit` 대비 목적지 블렌드만 차이·렌더큐 동일, 색 보존. 잔상은
BakeMesh가 PlayMode 스킨을 요구해 EditMode에서 포즈 검증 불가(§K3 flash와
동일한 이유) → PlayMode 이월로 문서화.

### 다음 사이클 이월 (VFX)
- 무기 궤적 메시 트레일 고도화(현 TrailRenderer → 리본 메시), 보스 페이즈
  전환 화면 왜곡, 원소별 지면 데칼 셰이더 — 모두 View-only 후보.
