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
