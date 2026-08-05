# Integrated Spec A — 전투 임팩트·VFX 통합 스펙 (View-only)

2026-08-05 · Achilles Visual Overhaul 통합 문서 A · 구현 대상: `Assets/Scripts/View/**` + `Assets/Editor/BuildScript.cs`(웹 템플릿 후처리)만.

**Sim 불가침**: `Assets/Scripts/Sim/**` 및 모든 수치/타이밍 변경 금지. 모든 항목은 SimEvents 플래그·스냅샷 필드 **구독**만 한다. 심 변경이 필요한 항목은 §S에 격리(AMENDMENT #4 게이트).

## 통합 매핑 (원본 → 본 문서)

| 원본 문서 | 원본 항목 | 본 문서 |
|---|---|---|
| presentation-impact-spec.md | #1-21 | §1 임팩트 코어, §2, §3.1-§3.12 |
| combat-feel-boss-phase-spec.md | §W, §K(비심), §M, §C | §3.5, §4 |
| view-vfx-research.md | 적용 5종·차기 후보 | §2, §6 |
| deep-interview-vfx-terrain-command-hardening.md | Lane V(V1-V4) | §5 |
| cycle2-spec.md | A1-A4 | §3.1-§3.4 |

## §1. 임팩트 코어 (절대 사수)

> presentation-impact-spec의 **절대 사수 8종**(#1 히트스톱, #2 셰이크, #4 킬 팝, #5 피격 플래시, #6 데미지 숫자, #7 파동 링, #9/#15 생존 신호, #18 오디오)이 임팩트 코어다. 본 문서는 중복 재제안 없이 **추가분만** 정의한다. (achilles §C)

### 1.1 히트스톱 (#1)
- `EnemyKilled` 40 ms·`ComboFinisher` 70 ms 동안 `Time.timeScale=0.05` 후 지수 복귀.
- `GameView` 신규 `_hitStopTimer` — `DispatchEvents()`에서 세팅, `Update()` 마지막에 `Time.unscaledDeltaTime`로 감쇠·복구.
- `if ((events & SimEvents.EnemyKilled)!=0) _hitStopTimer=Mathf.Max(_hitStopTimer,0.04f);` 피니셔 0.07 s.
- `Update()` 끝: `Time.timeScale = _hitStopTimer>0 ? 0.05f : Mathf.MoveTowards(Time.timeScale,1f,4f*Time.unscaledDeltaTime);`
- `EndRun()`에서 `Time.timeScale=1` 강제 복구. GameOver 이벤트 시 타이머 즉시 0.
- COST S. **timeScale=0 금지 — 최소 0.05** (심 결정론 무해, accumulator 경로만 통과).

### 1.2 카메라 셰이크 티어 확장 (#2)
- `EnemyKilled` 미세(0.08s/0.02)·`ComboFinisher` 중(0.14s/0.05)·`BossSpawned` 쿵(0.35s/0.07) 추가.
- `CameraRig.OnEvents` else-if 체인에 3분기. 우선순위: Nova > BossSpawned > BossPhase2 > ComboFinisher > PlayerDamaged > EnemyKilled.
- 기존 `Shake(duration, amplitude)` 재사용 — 신규 코드 3줄. COST S.

### 1.3 킬 팝 (#4)
- 적 사망 첫 프레임 scale 1.18× 펀치 후 기존 0.34 s 축소 페이드로 이행.
- `ActorView.Apply` — `if (!_dead)` 최초 사망 분기에 `_deathPop=0.09f`. 페이드 스케일 `_baseScale * (0.4f+0.6f*f) * (1f + 0.18f * Mathf.Clamp01(_deathPop/0.09f))`.
- `ResetForPool()` 0 초기화. 보스(scale 1.6) 과장 방지: 펀치 배율 `1+0.18/scale`로 상쇄. COST S.

### 1.4 적 피격 플래시 (#5)
- 적 개별 피격 순간 0.13 s 백-엠버 플래시. `_lastHealth` field; `SyncEnemy`에서 델타 검출.
- `var hit = state.Health < _lastHealth - 0.01f && !state.Dead; _lastHealth = state.Health;` → `Apply(..., hitFlash: hit)` — 기존 `_flashTime=0.13f` 경로 재사용(색만 엠버 톤). ResetForPool `_lastHealth=float.MaxValue`.
- **주의**: `DidDamage`·`LastHitAttack`·`KnockX/Y`는 `CinderSim.Enemy` 내부 필드로 스냅샷에 노출되지 않음(CinderSim.cs L61-65). 적은 피격 시 `ActorAction.Hit`로 전환되지 않음(`DamageEnemy` L1479-1528은 사망 시 `Die`만) — Action 감시로는 피격을 잡을 수 없다. COST S.

### 1.5 플로팅 데미지 숫자 (#6)
- 적 체력 감소량을 머리 위 0.6 s 상승·페이드 텍스트 (풀 16, 초과 시 최고령 재사용).
- 신규 `Assets/Scripts/View/DamageNumberPool.cs` (View asmdef). `GameView.SyncViews` 적 루프에서 #5와 같은 델타로 스폰. 폰트는 HudView와 동일 `Resources/Fonts/HudKorean`.
- TextMesh 16개 사전 생성(비활성). `pool.Show(x, y, amount)` → `ViewWorld.ToWorld(x,y,1.9f)` 배치, LateUpdate 카메라 빌보드 + `y+=1.2f*dt` + 알파 페이드. 문자열 `Dictionary<int,string>` 캐시(정수 반올림, 이벤트 시점 1회 조회) — 정상상태 무할당. 피니셔 틱은 금색. COST M.
- 20적 동시 노바 시 16풀 초과 — 최고령 축출 수용. TextMesh drawcall +16 상한은 WebGL 예산 내.

### 1.6 묘지 파동(E) 지속 필드 링 (#7)
- `PulseCast` 시 반경 190 px 링을 **3 s 지속** 표시 (현재 0.2 s 버스트뿐 — 심 판정 반경 상시 표시는 §2.3 계약).
- `VfxDirector.OnEvents` 옆 — 전용 LineRenderer 1개(`_pulseRing`) + `_pulseTime` 신규.
- `PulseCast` 수신 시 `sim.Player.X/Y` 캡처, `_pulseTime=3f`(= HackSpec.PulseDuration 값이지만 **View 상수로 별도 보유**). Update()에서 기존 노바 링 문법으로 반경 `190*ViewWorld.Scale` 고정 링 + 0.5 s 주기 알파 펄스(틱 리듬 0.5 s와 공명). 종료 시 disable. COST S.
- 필드는 시전 위치 고정(심 규칙) — 링도 고정이므로 오해 없음.

### 1.7 공격 스윙 트레일 (#8)
- `Player.Action == ActorAction.Attack` 활성창(`ActionTime` 0.10-0.34 s 합집합) 동안 무기 손 본(`RightHand`)에 TrailRenderer.
- ActorView 전용 트레일 1개 — emitting = 조건 부합 시, width 0.06→0·0.18 s, 엠버 그라디언트 `#f3592c`→투명. Material은 `RuntimeMaterialSeeds.Seed()` 시드 블록(§5.4 계약)에서.
- 피니셔/콤보 2·3타는 동일 문법 — 추가 코드 없음. COST M.

## §2. 연출 계층 규칙 (view-vfx-research + presentation-impact)

### 2.1 VFX 레이어 우선순위
- 하위 → 상위 렌더: (0) 바닥 데칼·그림자 < (1) 필드 링·파동 < (2) 월드 파티클·트레일 < (3) 스크린 스페이스(HUD 피드백·플래시·레터박스).
- 같은 레이어 충돌 시 **최신 이벤트 승리**, 동일 이벤트 재수신 시 재시작(재트리거).
- 월드 VFX는 `ViewWorld` 스케일 계약 사용. URP 포스트는 스크린 스페이스 예외.

### 2.2 풀링·무할당 계약 (전 레이어)
- ParticleSystem/LineRenderer/TextMesh 전부 **사전 생성 + 비활성 유지**, 활성화 재사용. 런타임 `Instantiate`/`new GameObject` 금지.
- `Emit(count)`·`SetPosition`만 사용. MainModule 캐시 후 Setter 호출 — 프로퍼티 Getter 매 프레임 금지.
- 문자열 결합 금지: `Dictionary<int,string>` 캐시, 이벤트 시점 1회 조회.

### 2.3 시전 판정 반경 표시 (§2.3 계약)
- 심은 시전 시점에만 범위 판정(결정론) — View는 **시전 순간 0.25 s 원형 텔레그래프 후 판정**. '항상 반경 표시'는 시전 후 지속 필드(#7)에 한정.
- 스킬 원소색: 볼트 보라 `#bf8cff`·노바 엠버 `#f3592c`·파동 그린 `#7fdc7f`·에이기스 시안 `#7fe3dc`. VFX 전반의 원소 코드.

### 2.4 reduced-motion 계약
- WebGL은 `SystemInfo` 감지 불가 — `matchMedia('(prefers-reduced-motion: reduce)')`(BuildScript 삽입) + PlayerPrefs 토글.
- 시행 시: 파티클 count 50%, 히트스톱 비활성(0.05→1), 셰이크·트레일·데미지 숫자 비활성, 컷신 자동 스킵.

## §3. HUD·카메라 임팩트 연출 (cycle2 A1-A4 + presentation-impact #9/#15/#19/#20)

### 3.1 보스 인트로 (A1)
- 보스 스폰 시 1.2 s: 레터박스(상하) + 보스 이름 큰 글리프 + `BossSpawned` 셰이크 쿵. 입력 비차단.
- HudView 신규 `_bossIntroTimer`. OnEvents `BossSpawned` 분기에서 세팅, Update 감쇠. 이름 문자열은 `BossDisplayName`(LobbyView와 동일 소스).
- 레터박스 = 상하 2개 Image(전화면 캔버스) — 화면 비율 무관. COST S.
- **#12 포커스 풀**: CameraRig `FocusPulse(target, 1.2f)` — `PlaceOrbit` focus 인자를 `Vector3.Lerp(ArenaCenter, BossAnchor(sim), sin(π·t/1.2))`로 왕복, 종료 시 ArenaCenter 정확 복귀(기존 군중 티어와 합성 안전). Dungeon 프로파일 한정. 레터박스 높이 화면 9%·1.2 s in-out. COST M.
- 동시 발화(군중 거리 17→21): 거리는 기존 lerp에 위임, focus만 펄스 — 시각 충돌 없음.

### 3.2 클리어 세리머니 (A2)
- 보스 사망 → `VictoryStarted` 시점 1.0 s: 레터박스 유지 + 골드/엠버 파티클 버스트 + 'CLEARED' 글리프 스케일 펀치인.
- 파티클 §2.2 풀 문법, 스크린 스페이스 캔버스 파티클(신규 풀 1종 48 max). 글리프는 A1 배너 재사용. COST S.

### 3.3 콤보 핍 (A3)
- 콤보 카운터 도달 시 기존 '콤보!' 텍스트 팝 외 **핍 사운드 + 작은 링 버스트**(카운터 위치).
- 링 = VfxDirector 월드 풀 문법, 플레이어 위 0.35 s. 사운드 = AudioDirector `_strike` 0.3 변주(#18 매핑 포함). COST S.

### 3.4 보스바 슬라이드다운 (A4)
- `BossPhase2`(50% 이하) 최초 진입: 보스바 컨테이너 0.25 s 상단 슬라이드다운 + 좌측 'PHASE 2' 배지 교체.
- HudView 보스바 캔버스 그룹 + anchoredPosition 트윈(코루틴 1개). 재진입 시 재생 안 함(플래그). COST S.
- **#3 페이즈2 슬로모**: `BossPhase2` 시 0.5 s `Time.timeScale=0.35` → 1.0 지수 복귀(도발 말풍선과 동기). #1과 같은 `_hitStopTimer` 메커니즘에 `_slowMoTimer=0.5f`·`_slowMoScale=0.35f` 추가, 복구는 #1 공유. 히트스톱 동시 발화 시 **`min(scale)` 적용**. COST S.

### 3.5 레벨업·웨이브 배너 (#19-#20)
- **#19 레벨업 세리머니**: `LevelUp` 시 XP 바 골드 플래시 0.4 s → 시안 lerp, 'Lv N' 스케일 펀치 1.6→1, 시안 토스트 1.4 s '레벨 업! 피해 +4% · 체력 +6'. 월드 버스트와 HUD 이중 연출은 의도적(#11 계약).
- **#20 웨이브 배너**: `WaveStarted` 시 상단 배너 0.25 s 펀치인 → 1.2 s 유지 → 페이드. 배너 패널 1장 신규(재사용).
- 둘 다 HudView.OnEvents 분기 + 신규 트윈 코루틴. COST S.

### 3.6 방향 화살표·생존 신호 (#9/#15)
- #9: 플레이어 정지 0.4 s 이상 시 적 최근접 방향 화살표 — InputAdapter 이동 벡터 0 감지, VfxDirector 화살표 1개 풀.
- #15: 체력 30% 이하 지속 시 화면 가장자리 심박 비네트(URP Vignette 강도 펄스) + 낮은 심박음 루프. COST S.

### 3.7 스크린 플래시 (#10)
- `NovaCast`(엠버)·`WardCast`(시안)·`BoltCast`(보라)·`AltarBlessing`(골드) 순간 90 ms 풀스크린 틴트 플래시 — HudView.OnEvents, #9 비네트와 같은 오버레이 Image 재사용(색·알파만 이벤트별 세팅) + 별도 Image 인스턴스. 알파 0.28 상한 고정(연타 피로 방지). COST S.

### 3.8 픽업 수집 스파클 (#13)
- 픽업 수집 순간 아이콘이 플레이어를 향해 0.22 s 날아가며 축소·소멸(현재 즉시 제거 대체). VfxDirector.SyncPickups 스테일 스윕에서 `Life > 0.05f`(수집)면 `_flyList`(용량 8)로 이관: `Lerp(pos, playerWorld, t/0.22)` + `scale *= 1-t`. 만료(`Life<=0`)는 기존 즉시 제거 유지. COST M.

### 3.9 정예 금색 틴트 펄스화 (#14)
- 정적 금색 틴트를 1.2 s 주기 밝기 펄스로 승격 — ActorView `SetEliteTint(bool)` 신규, GameView.SyncViews의 1회 호출을 대체. `_block.SetColor(BaseColorId, gold*(0.85f+0.3f*Mathf.PingPong(Time.time*0.83f,1f)))` — 기존 무할당 경로 재사용. 피격 플래시(#5) 겹침 시 플래시 우선. COST S.

### 3.10 추출 채널 연출 (#16)
- `EliteDown` 시 시체 위치 시안 명멸 지면 링(10 s) + `ExtractionTarget > 0` 채널 중 플레이어→시체 빔·수축 링. VfxDirector `EliteDown` 분기에서 `enemies[i].Dead && !IsBoss && Scale>1.2f` 좌표 캡처, 전용 LineRenderer 링+빔. `ExtractionComplete` 수신 시 즉시 클리어(기존 버스트가 완료 담당). COST M.

### 3.11 벤트 분화 버스트 (#17)
- 분출구 폭발 프레임 엠버 버스트 링 + 셰이크 미세 1회 — SyncHazards에서 벤트별 `prevCycleT` 캐시, `CycleT < prev` 랩 검출 시 `SpawnBurst(x, y, ember, 0.9f, 0.3f)`(8-풀 재사용). `SimEvents.HazardPulse`는 벤트 식별 불가 — CycleT 랩이 정답. prev 초기값을 현재 CycleT로 시드(첫 프레임 오발화 방지). 오디오는 기존 HazardPulse 배선 유지. COST S.

### 3.12 오디오 큐 배선 (#18)
- 미배선 9종 → 기존 8클립 볼륨 변주로 배선 (신규 에셋 0). AudioDirector.OnEvents 확장:

  | 이벤트 | 클립 변주 |
  |---|---|
  | DashUsed | `_strike` 0.5 |
  | BoltCast | `_nova` 0.45 |
  | PulseCast | `_ward` 0.6 |
  | LevelUp | `_pickup` 1.0 + `_wave` 0.4 |
  | EliteDown | `_kill` 1.0 |
  | ExtractionComplete | `_ward` 0.9 |
  | BossPhase2 | `_gameover` 0.35(저역 위협) |
  | ComboFinisher | `_kill` 0.7 |
  | BossSpawned | `_wave` 0.9 |

- COST S. 볼륨 변주 한계는 차기 ElevenLabs 전용 큐 교체 전 임시 계약.

## §4. 전투 느낌 보강 (combat-feel §W·§K 비심 부분)

### 4.1 예고·판정 창 정합 (§W)
- 심의 순수 2D 수학(`dx*facing >= -18`) 유지 — View는 **시각적 예고**만 추가.
- 근접: 공격 시작 0.1 s 전 작은 전방 호. 원거리: 시전 직후 0.25 s 범위 텔레그래프(§2.3).
- §K3 메시 색상 우선순위: 피격 플래시(0.13 s) > 원소 틴트(0.4 s) > 랭크 글로우. Tint는 소유 원소에만.

### 4.2 §M 신규 모션 (비심 경로)
- 신규 클립 4종: `attack2`/`attack3`(콤보 2·3타) · `cast_loading`(0.3 s 루프) · `recoil`(반동) · `knockdown`(넉백). `blender -b -P` + Mixamo 리타겟(CLAUDE.md §3).
- `ActorAction` enum 확장은 **SimTypes FROZEN** — View 전용 서브스테이트(콤보 인덱스 field)로 구현. §S6 승인 시에만 심 확장.
- 컨트롤러 스테이트 추가는 `CinderActor.controller`(View 어셋) — 심 무변경.

### 4.3 §C 전환 컷신
- Stage 전환(Ember Rest → 다음 방) 3 s: 카메라 궤도 + 스토리 자막 + 페이드. `CameraRig.Profile.Cutscene` 신규.
- 입력 비차단 스킵 가능. 배경 키아트 6종은 `gti --dry-run` 선행 승인 후 제작(§7 자산). docs/provenance 기록.

## §5. 스킬 VFX 증강 (vfx-terrain Lane V)

### 5.1 V1 시전 동기화
- 시전 시작 0.12 s 손 본 수렴 글로우(양손). 판정 불변. COST S.

### 5.2 V2 벤트 텔레그래프 fill
- 벤트 채널(V) fill 임박도를 시전 범위 원에 fill로 표시 — 리서치 1순위. 시전 시 §2.3과 결합. COST M.

### 5.3 V3 원소별 파티클 (풀링 4종)
- 기존 링/스파크 **증강** (교체 금지).
- 볼트: 보라 관통 잔광 6발 · 파동: 녹색 틱 리플 0.5 s 공명 · 노바: 엠버 낙하 파편 10 · 에이기스: 시안 흡수 플래시.
- `Emit(count)` 무할당, maxParticles 상한(40/24/32/24), reduced-motion 50%. COST M.

### 5.4 WebGL 셰이더 스트리핑 계약
- `RuntimeMaterialSeeds.Seed()`에 `particle-additive-seed.mat` 시드 블록 추가 — 파티클 Material은 여기서만.
- **`new Material(Shader.Find(...))` 직접 생성 금지** (WebGL 스트리핑 시 누락).

### 5.5 V4 URP 포스트 블룸·비네트
- 블룸(강도 0.35) + 비네트(§3.6 심박). p95 16.7 ms 게이트 — 초과 시 품질 티어 강등/컷.
- EditMode 테스트 + 데스크톱 스모크 + 프로파일 수치 없이 PASS 불가. COST S.

## §6. 카메라·제미나이 콘솔 (view-vfx-research)

### 6.1 카메라 판정
- 저FOV·피치 조정은 **보류** (프로필 3종 상한 2.2 유지). 직교 카메라 전환 기각.
- `CameraRig.Profile.Cutscene`(§4.3)만 신규 — 기존 던전 프로필 불변.

### 6.2 Gemini 아키텍처
- URL 프래그먼트 `#gemini=<명령>`만 사용 — 쿼리스트링 금지(GitHub Pages 정적 호스팅 404 방지).
- `GeminiCommandClient.cs` 확장: 프래그먼트 파싱 → 게임 내 콘솔 입력으로 변환(키 전달)
- API 키는 프래그먼트+PlayerPrefs(키 노출 방지, 브라우저 로컬 전용).
- CLI 명령: 시전·리셋·카메라 덤프. 비동기 안전·실패 시 조용한 폴백.

## §7. 이미지 컨셉 자산 (god-tibo-imagen)

| # | 용도 | 프롬프트 시드 | 생성 순서 |
|---|---|---|---|
| 1 | 보스 인트로 배경 | 2.5D 다크 판타지, 잿불 군단 왕좌, 엠버 비, 실루엣, 16:9 | A1 이후 |
| 2 | 클리어 배경 | 정화된 회랑, 시안 빛, 잔불 잔해, 16:9 | A2 이후 |
| 3-8 | 스테이지 전환 키아트 6종 | 각 구역 모티프(다리·성소·왕좌), 16:9 | §4.3와 동시 |

- **사전 게이트**: `gti --dry-run` 선행 → 사용자 승인 → 제작. `docs/provenance` 기록.
- 배치: `build-webgl/index.html` 배경 레이어(BuildScript PolishIndexHtml L61-72). 로딩 점진.

## §S. 심 변경 격리 (AMENDMENT #4 후보)

> 아래 항목만 심 변경이 필요하다. 각각 `// FROZEN CONTRACT AMENDMENT #4` + `docs/SIM_SPEC_HACKSLASH.md` 개정 + 결정론 EditMode 테스트를 선행 게이트로 명시한다. **View 구현과 병행하지 않는다 — 승인 후 별도 작업.**

| ID | 항목 | 심 변경 | 게이트 |
|---|---|---|---|
| S6 | 보스 페이즈 벡터 | `BossPhase` 노출 확장 | AMENDMENT #4 + 테스트 |
| S7 | 예고 범위 | 시전 직후 0.25 s 범위 표시용 판정 노출 | AMENDMENT #4 + 테스트 |
| S8 | 콤보 쿨타임 감소 | '합이 작아지는' = 쿨타임 감소 해석(**승인 확인 필요**) | AMENDMENT #4 + 테스트 |

- S1-S4는 문서 C §S로 이동(UI·시스템). S5는 문서 B §S로 이동(캠페인 경계).
- S6/S7 미승인 시 §4.1·§2.3 View 텔레그래프로 대체(심 무변경 유지 가능).

## §8. 구현 순서·검증

1. §1 임팩트 코어(#1-#8) — EditMode 테스트 병행.
2. §3 HUD/카메라(A1-A4, #3/#10/#12/#13/#14/#16/#17/#18, #19-#20) — HudLayoutTests 갱신 계약 준수.
3. §5 VFX 증강(V1-V4) — 프로파일 게이트.
4. §4 예고/모션(§W, §M 비심, §C) — 자산 파이프라인.
5. §6 Gemini 콘솔 — BuildScript 후처리.
6. §7 컨셉 자산 — gti 승인 후.

**완료 조건**: EditMode 66/66 + 데스크톱 스모크 + WebGL 빌드 ≤120MB + p95 16.7ms. 심 파일 diff 0 (S1-S8 미승인 시).
