# Presentation Impact Spec — Hack & Slash 연출 극대화 (View-only)

2026-08-04 · ResearchSpec 레인 · 구현 대상: `Assets/Scripts/View/**` + `Assets/Editor/BuildScript.cs`(웹 템플릿 후처리)만.
**Sim 불가침**: `Assets/Scripts/Sim/**` 및 모든 수치/타이밍 변경 금지. 모든 항목은 SimEvents 플래그·스냅샷 필드 **구독**만 한다.

## 트리거 어휘 (코드 원문 — SimTypes.cs `SimEvents`, 검증 완료)

`PlayerStruck(1<<0)` `EnemyHit(1<<1)` `EnemyKilled(1<<2)` `NovaCast(1<<3)` `WardCast(1<<4)`
`PickupCollected(1<<5)` `WaveStarted(1<<6)` `GameOver(1<<7)` `PlayerDamaged(1<<8)` `BossSpawned(1<<9)`
`StageCleared(1<<10)` `HazardPulse(1<<11)` `AltarBlessing(1<<12)` `EquipDropped(1<<13)`
`DashUsed(1<<14)` `BoltCast(1<<15)` `PulseCast(1<<16)` `LevelUp(1<<17)` `EliteDown(1<<18)`
`ExtractionComplete(1<<19)` `BossPhase2(1<<20)` `ComboFinisher(1<<21)`

스냅샷 필드(ISimSnapshot/ICampaignSnapshot/IHackSnapshot): `Player.Action/ActionTime/AttackId/DamageCooldown/WardTime`,
`EnemyState.Health/MaxHealth/Dead/FadeTime/Scale/IsBoss`, `NovaX/NovaY`, `Charge`, `Hazards[].CycleT/Telegraphing`,
`ComboIndex`, `SkillCooldowns`, `ExtractionProgress/Target`, `BossHp/BossMaxHp/BossPhase`, `LivingEnemies`.

> **주의**: `DidDamage`·`LastHitAttack`·`KnockX/Y`는 `CinderSim.Enemy` **내부 필드**로 스냅샷에 노출되지 않는다
> (CinderSim.cs L61-65). 적 개별 피격 검출은 View 측 체력 델타 캐시(#5)로 해결한다. 적은 피격 시
> `ActorAction.Hit`로 전환되지 **않는다** (`DamageEnemy` L1479-1528은 사망 시 `Die`만 설정) — Action 감시로는 피격을 잡을 수 없다.

## 이미 존재하는 것 (재제안 금지 — 코드 확인 완료)

| 기존 연출 | 위치 |
|---|---|
| 셰이크: NovaCast(0.2s/0.06) · BossPhase2(0.3s/0.09) · PlayerDamaged(0.12s/0.045) | CameraRig.OnEvents L88-93 |
| 플레이어 피격 적색 플래시 0.13 s | ActorView.SyncPlayer L87 (`DamageCooldown > PlayerHitGrace-0.16` → `_flashTime`) |
| 킷 버스트 링 8-풀: DashUsed/ComboFinisher/LevelUp/ExtractionComplete/BossPhase2/BoltCast | VfxDirector.OnEvents L76-96 + SpawnBurst |
| 노바 확장 링 0.42 s + 워드 셸(끝 0.5 s 점멸) | VfxDirector L68-74, SyncWard L290-307 |
| 사망 축소 페이드 0.34 s (`FadeTime/SimConfig.EnemyFade`) | ActorView.Apply L120-133 |
| 정예 금색 틴트(정적) | GameView.SyncViews L184-185 |
| 던전 군중 카메라 거리 17→21 | CameraRig.SetDungeonCrowd + GameDirector.Update L414-420 |
| 보스 말풍선(등장/페이즈2/처치) | GameDirector.DispatchStory L381-393 |
| 콤보 핍 색 전환, 보스바+PHASE 핍, 추출 채널 바 | HudView.SyncDungeon L486-533 |
| 벤트 텔레그래프 점멸·제단 회전 | VfxDirector.SyncHazards L190-221 |

## 결정론 판정 — view-side timeScale (항목 #1·#3의 전제)

GameView.Update L114-144: `_accumulator += min(Time.deltaTime, MaxFrameDelta)` 후 `FixedStep(1/60)` 단위로 Tick.
`Time.timeScale`은 `Time.deltaTime`을 스케일할 뿐이며 **틱 크기(1/60)와 틱당 입력 소비 규칙은 불변** —
느린 프레임과 동일한 경로로 처리되고 `MaxCatchUpSteps=5`가 복구 폭주를 막는다. 같은 입력 시퀀스 → 같은 틱 열 → 같은 Digest.
따라서 **timeScale 펄스는 심 결정론에 무해**하다 (단, `timeScale=0` 정지는 금지 — 최소 0.05; 복구 타이머는
`Time.unscaledDeltaTime`로 감쇠). BGM/원샷 오디오는 AudioSource pitch 미변경 시 영향 없음.

---

## 항목 (전부 View-only)

### 1. 히트스톱 — 킬/콤보 피니셔 순간 정지감
- **WHAT**: `EnemyKilled` 40 ms·`ComboFinisher` 70 ms 동안 `Time.timeScale=0.05` 후 지수 복귀.
- **WHERE**: `GameView` 신규 필드 `_hitStopTimer` — `DispatchEvents()` L146에서 세팅, `Update()` 마지막에 `Time.unscaledDeltaTime`로 감쇠·복구.
- **HOW**: `if ((events & SimEvents.EnemyKilled)!=0) _hitStopTimer=Mathf.Max(_hitStopTimer,0.04f);` 피니셔는 0.07 s.
  `Update()` 끝에서 `Time.timeScale = _hitStopTimer>0 ? 0.05f : Mathf.MoveTowards(Time.timeScale,1f,4f*Time.unscaledDeltaTime);`
  `EndRun()`에서 `Time.timeScale=1` 강제 복구.
- **COST**: S
- **RISK**: GameOver 패널 등장과 겹치면 UI 트윈이 느려 보임 — `GameOver` 이벤트 시 타이머 즉시 0으로.

### 2. 카메라 셰이크 티어 확장 — 킬/피니셔/보스 등장
- **WHAT**: `EnemyKilled` 미세(0.08s/0.02)·`ComboFinisher` 중(0.14s/0.05)·`BossSpawned` 쿵(0.35s/0.07) 셰이크 추가.
- **WHERE**: `CameraRig.OnEvents` L88-93 — 기존 else-if 체인에 3분기 추가 (기존 Nova/Phase2/PlayerDamaged 값 유지).
- **HOW**: 우선순위 체인 유지: Nova > BossSpawned > BossPhase2 > ComboFinisher > PlayerDamaged > EnemyKilled.
  기존 `Shake(duration, amplitude)` 재사용 — 신규 코드 3줄.
- **COST**: S
- **RISK**: 킬 셰이크가 다중 킬 웨이브에서 연속 트리거 — 진폭 0.02로 억제, 불쾌하면 컷.

### 3. 보스 페이즈2 슬로모 비트
- **WHAT**: `BossPhase2` 시 0.5 s 동안 `Time.timeScale=0.35` → 1.0 지수 복귀 (도발 말풍선과 동기).
- **WHERE**: `GameView.DispatchEvents()` — #1과 같은 `_hitStopTimer` 메커니즘에 `_slowMoTimer` 추가 (또는 timer에 target scale 쌍 저장).
- **HOW**: `if ((events & SimEvents.BossPhase2)!=0) { _slowMoTimer=0.5f; _slowMoScale=0.35f; }`
  복구는 #1과 공유. 결정론 판정 §참조 — accumulator 경로만 통과, 심 무변경.
- **COST**: S
- **RISK**: 히트스톱과 동시 발화 시 우선순위 — `min(scale)` 적용으로 해소.

### 4. 킬 팝 — 사망 스케일 펀치
- **WHAT**: 적 사망 첫 프레임에 스케일 1.18× 펀치 후 기존 0.34 s 축소 페이드로 이행.
- **WHERE**: `ActorView.Apply` L120-133 — `if (!_dead)` 최초 사망 분기에 펀치 타이머 시작.
- **HOW**: 사망 감지 프레임에 `_deathPop=0.09f`. 페이드 스케일 계산을
  `_baseScale * (0.4f+0.6f*f) * (1f + 0.18f * Mathf.Clamp01(_deathPop/0.09f))`로 확장, `_deathPop -= Time.deltaTime`.
  `ResetForPool()`에서 0 초기화. FadeTime은 심 소유 — 읽기만.
- **COST**: S
- **RISK**: 보스(스케일 1.6)에서 과장 — 펀치 배율을 `1+0.18/scale`로 나눠 상쇄.

### 5. 적 피격 플래시 — 체력 델타 캐시
- **WHAT**: 적 개별 피격 순간 0.13 s 백-레드 플래시 (플레이어 피격 플래시와 동일 문법).
- **WHERE**: `ActorView` — 신규 필드 `_lastHealth`; `SyncEnemy` L90-95에서 델타 검출 후 기존 `hitFlash` 인자에 전달.
- **HOW**: `var hit = state.Health < _lastHealth - 0.01f && !state.Dead; _lastHealth = state.Health;`
  `Apply(..., hitFlash: hit)` — 기존 `_flashTime=0.13f` 경로 L143-151 재사용(색만 `Color.white→(1,.45,.2)` 엠버 톤).
  `ResetForPool()`에서 `_lastHealth=float.MaxValue`.
- **COST**: S
- **RISK**: 없음에 가까움 — 스냅샷 `EnemyState.Health`는 public 계약, MaterialPropertyBlock 경로는 이미 무할당.

### 6. 플로팅 데미지 숫자 — 월드공간 풀
- **WHAT**: 적 체력 감소량을 머리 위 0.6 s 상승·페이드 텍스트로 표시 (풀 16, 초과 시 최고령 재사용).
- **WHERE**: 신규 `Assets/Scripts/View/DamageNumberPool.cs` (View asmdef) — `GameView.SyncViews` L175-188 적 루프에서 #5와 같은 델타로 스폰. 폰트는 HudView와 동일 `Resources/Fonts/HudKorean`.
- **HOW**: `TextMesh` 16개 사전 생성(비활성). 스폰: `pool.Show(x, y, amount)` → `ViewWorld.ToWorld(x,y,1.9f)`에 배치,
  LateUpdate에서 카메라 빌보드 + `y+=1.2f*dt` + 알파 페이드. 문자열은 `Dictionary<int,string>` 캐시(피해량 정수 반올림,
  이벤트 시점 1회 조회)로 정상상태 무할당. 크리티컬 구분 없음(심에 크리 개념 없음) — 피니셔 틱은 금색.
- **COST**: M
- **RISK**: 20적 동시 노바 시 16풀 초과 — 최고령 축출로 수용; TextMesh 드로우콜 +16 상한은 WebGL 예산 내.

### 7. 묘지 파동(E) 지속 필드 링 — 가독성 결손 보수
- **WHAT**: `PulseCast` 시 반경 190 px 링을 **3 s 지속** 표시 (현재 0.2 s 버스트뿐 — 심 판정 반경 상시 표시는 §2.3 계약).
- **WHERE**: `VfxDirector.OnEvents` L95-96 옆 — 전용 LineRenderer 1개(`_pulseRing`) + `_pulseTime` 신규.
- **HOW**: `PulseCast` 수신 시 `sim.Player.X/Y` 캡처, `_pulseTime=3f`(= HackSpec.PulseDuration 값이지만 **View 상수로 별도 보유**, 심 참조는 읽기 전용 상수라 무해). `Update()`에서 기존 노바 링 문법으로 반경 `190*ViewWorld.Scale` 고정 링 + 0.5 s 주기 알파 펄스(틱 리듬 0.5 s와 공명). 종료 시 disable.
- **COST**: S
- **RISK**: 필드는 시전 위치 고정(심 규칙) — 링도 고정이므로 오해 없음; 링 지속을 하드코딩하므로 스펙 변경 시 2곳 수정.

### 8. 공격 스윙 트레일
- **WHAT**: `Player.Action == ActorAction.Attack` 활성창 동안 무기 손 본에 TrailRenderer 활성화.
- **WHERE**: `ActorView` — `Create()`에서 플레이어 전용으로 `Animator.GetBoneTransform(HumanBodyBones.RightHand)`에 트레일 부착; `SyncPlayer`에서 on/off.
- **HOW**: `trail.emitting = state.Action==ActorAction.Attack && state.ActionTime>=0.10f && state.ActionTime<0.34f;`
  (아레나 활성창 0.167-0.333, 던전 콤보 0.10-0.30 — 합집합 창으로 충분, 판정과 무관한 순수 장식).
  width 0.06→0, time 0.18 s, 엠버 그라디언트(#f3592c→투명), `ViewWorld.MakeUnlit` 투명 머티리얼.
- **COST**: M
- **RISK**: Humanoid 본 조회 실패 시(리타겟 누락 모델) 모델 루트+높이 오프셋으로 폴백; 트레일은 카메라 각도에 따라 얇아 보일 수 있음.

### 9. 저체력 비네트 펄스
- **WHAT**: `Player.Health < 35` 시 화면 가장자리 적색 비네트가 심박 리듬(0.9 s)으로 맥동, `PlayerDamaged` 순간 1펄스 강조.
- **WHERE**: `HudView` — `Build()`에서 풀스트레치 Image 1장(레이캐스트 off) 추가; `Sync()` L737에서 체력 체크; `OnEvents()` L720에서 데미지 펀치.
- **HOW**: 절차 텍스처 1회 생성(128², 중심 투명→가장자리 α0.55 방사 그라디언트, `#f3592c` 틴트) — 에셋 불요.
  `alpha = health<35 ? 0.25f+0.2f*Mathf.Sin(Time.time*7f) : 0f` 를 목표로 MoveTowards; `PlayerDamaged` 시 `alpha=0.6f` 즉시 셋.
- **COST**: S
- **RISK**: 풀스크린 오버드로 1장 — 알파 0일 때 `enabled=false`로 완전 오프.

### 10. 스킬 캐스트 스크린 플래시
- **WHAT**: `NovaCast`(엠버)·`WardCast`(시안)·`BoltCast`(보라)·`AltarBlessing`(골드) 순간 90 ms 풀스크린 틴트 플래시.
- **WHERE**: `HudView.OnEvents` L720 — #9와 같은 오버레이 Image 재사용(색·알파만 이벤트별 세팅) + `Sync()`에서 감쇠.
- **HOW**: `_flashColor=new Color(0.95f,0.35f,0.17f,0.28f)` 등 이벤트별 상수; `alpha -= Time.deltaTime/0.09f`.
  비네트(#9)와 같은 텍스처, 별도 Image 인스턴스(합성 단순화).
- **COST**: S
- **RISK**: 연타 시 플래시 피로 — 알파 0.28 상한 고정, reduced-motion 사용자 배려로 추후 토글 고려.

### 11. 콤보 핍 펀치 + 피니셔 골드 플래시
- **WHAT**: `ComboIndex` 증가 프레임에 해당 핍 스케일 1.5→1.0 펀치, `ComboFinisher` 시 3핍 동시 골드 플래시 후 리셋.
- **WHERE**: `HudView.SyncDungeon` L486-493 콤보 분기 + `OnEvents`에 `ComboFinisher` 분기 추가.
- **HOW**: 핍 RectTransform `localScale` 트윈(타이머 필드 3개, per-frame lerp — 무할당).
  피니셔: 3핍 `color=Gold, scale=1.6f` 세팅 후 0.25 s 감쇠.
- **COST**: S
- **RISK**: 없음 — 기존 dirty-check 패턴 안에 타이머만 추가.

### 12. 보스 인트로 — 포커스 풀 + 레터박스
- **WHAT**: `BossSpawned` 시 1.2 s 카메라 포커스를 보스 좌표로 끌었다 복귀 + 상하 레터박스 바 슬라이드 (spec §10 "보스 인트로 1.2 s 푸시인" 미구현분).
- **WHERE**: `CameraRig` 신규 `FocusPulse(Vector3 target, float seconds)` — `PlaceOrbit` L167-172의 focus 인자를 `Vector3.Lerp(ArenaCenter, _focusTarget, ease)`로 확장. 호출은 `GameDirector.OnRunEvents` L265(BossSpawned 분기 신설, `BossAnchor(sim)` 재사용). 레터박스는 `HudView`에 검은 Image 2장.
- **HOW**: Dungeon 프로파일 한정. `ease = Mathf.Sin(π * t/1.2)` 왕복 — 종료 시 정확히 ArenaCenter 복귀(기존 군중 티어와 합성 안전).
  레터박스: anchorMin/Max (0,1)-(1,1)·(0,0)-(1,0) 바 2장, 높이 화면 9%, 1.2 s in-out.
- **COST**: M
- **RISK**: 군중 티어 거리 전환(17→21)과 동시 발화 — 거리는 기존 lerp에 맡기고 focus만 펄스하므로 시각 충돌 없음(확인 필요).

### 13. 픽업 수집 스파클 — 플레이어로 흡인
- **WHAT**: 픽업 수집 순간 아이콘이 플레이어를 향해 0.22 s 날아가며 축소·소멸 (현재: 즉시 Destroy L348-352).
- **WHERE**: `VfxDirector.SyncPickups` L338-353 스테일 스윕 — `PickupCollected` 이벤트와 조합해 수집/만료 구분.
- **HOW**: 스윕에서 제거 대상의 `Life > 0.05f`(만료 아님=수집)이면 Destroy 대신 `_flyList`(구조체 리스트, 용량 8)로 이관:
  `pos = Lerp(pos, playerWorld, t/0.22)` + `scale *= 1-t`. 완료 시 Destroy. `_playerTransform` 필드는 이미 존재(L25).
  만료(`Life<=0`)는 기존 즉시 제거 유지.
- **COST**: M
- **RISK**: `PickupState.Life` 수집 시점 잔존값 확인 필요 — 수집 제거와 만료 제거의 구분이 어긋나면 만료도 날아감(시각적 애교 수준).

### 14. 정예 금색 틴트 펄스화
- **WHAT**: 정적 금색 틴트(현행)를 1.2 s 주기 밝기 펄스로 승격 — spec §3 "금색 틴트 **펄스**" 원문 이행.
- **WHERE**: `ActorView` — `SetEliteTint(bool)` 신규; `GameView.SyncViews` L184-185의 `LobbyStaging.TintRenderers` 1회 호출을 대체.
- **HOW**: ActorView에 `_eliteTint` bool + `Apply()` 끝에서 `_block.SetColor(BaseColorId, gold * (0.85f+0.3f*Mathf.PingPong(Time.time*0.83f,1f)))`
  — 기존 `_block`/`_renderers` 무할당 경로 재사용. 피격 플래시(#5)와 겹칠 땐 플래시 우선.
- **COST**: S
- **RISK**: 매 프레임 SetPropertyBlock 호출 증가(정예 1기 한정) — 무시 가능.

### 15. 저기름 랜턴 플리커
- **WHAT**: `Charge < NovaCost(45)` 시 기름 바 채움색이 등불 꺼질 듯 플리커(불규칙 0.15-0.4 s), `Charge < 20` 시 적색 경고 톤.
- **WHERE**: `HudView.Sync` L746-752 charge 분기 — `_chargeFill.color` 변조 추가.
- **HOW**: `flicker = Mathf.PerlinNoise(Time.time*6f, 0f)` — `color = Lerp(dim, base, 0.55f+0.45f*flicker)`;
  20 미만이면 base를 `(0.95,0.42,0.3)`로 스왑. 텍스트 갱신은 기존 dirty-check 그대로.
- **COST**: S
- **RISK**: 상시 컬러 세팅(프레임당 1회) — uGUI 버텍스 리빌드 소폭 증가, 바 1개라 무시 가능.

### 16. 추출 채널 연출 — 시체 마커 링 + 채널 빔
- **WHAT**: `EliteDown` 시 시체 위치에 시안 명멸 지면 링(10 s), `ExtractionTarget > 0` 채널 중 플레이어→시체 빔 + 수축 링.
- **WHERE**: `VfxDirector` — `EliteDown` 분기(L82 옆)에서 죽은 정예 좌표 캡처(`enemies[i].Dead && !IsBoss && Scale>1.2f`), 전용 LineRenderer 링+빔. 채널 진행은 `GameView.SyncViews`에서 `hack.ExtractionProgress/Target` 전달.
- **HOW**: 마커: 반경 `90*Scale` 링, 10 s 카운트다운 알파. 채널 중: `ring.radius = 0.9f*(1-progress/target)` 수축 +
  빔 2점 LineRenderer(player↔corpse). `ExtractionComplete` 수신 시 즉시 클리어(기존 버스트가 완료 팡 담당).
- **COST**: M
- **RISK**: 시체 좌표는 View 캐시 — 심 시체 TTL(10 s)과 드리프트 가능성 있으나 마커는 순수 장식이라 무해.

### 17. 벤트 분화 버스트
- **WHAT**: 잿불 분출구 폭발 프레임에 엠버 버스트 링 + 셰이크 미세 1회 (현재 텔레그래프만 있고 폭발 순간 시각 무).
- **WHERE**: `VfxDirector.SyncHazards` L186-221 — 벤트별 `prevCycleT` 캐시, 랩어라운드(`CycleT < prev`) 검출 시 `SpawnBurst(hazard.X, hazard.Y, ember, 0.9f, 0.3f)`. (`SimEvents.HazardPulse`는 **매 사이클 경계마다** 발화(CinderSim L1983-1984, 피격 여부 무관)하지만 어느 벤트인지 식별 불가 — 다중 벤트 스테이지에서 버스트 좌표를 잡으려면 CycleT 랩 검출이 정답. 오디오는 기존 HazardPulse 배선 유지.)
- **HOW**: `_hazardViews`에 float 필드 1개 추가. 버스트는 기존 8-풀 재사용.
- **COST**: S
- **RISK**: 첫 SyncHazards 프레임 오발화 — prev 초기값을 현재 CycleT로 시드.

### 18. 미배선 이벤트 오디오 큐 매핑
- **WHAT**: `DashUsed`·`BoltCast`·`PulseCast`·`LevelUp`·`EliteDown`·`ExtractionComplete`·`BossPhase2`·`ComboFinisher`·`BossSpawned` 9종 무음 해소 — 기존 8클립 볼륨 변주로 배선 (신규 에셋 0).
- **WHERE**: `AudioDirector.OnEvents` L71-94 — 캠페인 이벤트 재사용 문법(L85-93) 그대로 확장.
- **HOW**: Dash→`_strike` 0.5 / Bolt→`_nova` 0.45 / Pulse→`_ward` 0.6 / LevelUp→`_pickup` 1.0+`_wave` 0.4 /
  EliteDown→`_kill` 1.0 / Extraction→`_ward` 0.9 / BossPhase2→`_gameover` 0.35(저역 위협) / ComboFinisher→`_kill` 0.7 / BossSpawned→`_wave` 0.9.
- **COST**: S
- **RISK**: 볼륨 변주만으론 식별력 한계 — 차기 ElevenLabs 배치에서 전용 큐 교체 전 임시 계약.

### 19. 레벨업 HUD 세리머니
- **WHAT**: `LevelUp` 시 XP 바 골드 플래시 0.4 s + "Lv N" 라벨 스케일 펀치 + 시안 토스트 1.4 s ("레벨 업! 피해 +4% · 체력 +6").
- **WHERE**: `HudView` — `OnEvents`에 LevelUp 분기, `SyncDungeon` L474-484 레벨 분기에 펀치 타이머.
- **HOW**: `_xpFill.color`를 골드로 스냅 후 시안 복귀 lerp; `_levelText.rectTransform.localScale` 1.6→1 트윈;
  토스트는 프롤로그 토스트 패널 문법(L291-310) 재사용한 별도 1장.
- **COST**: S
- **RISK**: 월드 버스트(기존 LevelUp 링)와 이중 연출 — 의도적 중복(월드+HUD)으로 승인.

### 20. 웨이브 배너 펀치인
- **WHAT**: `WaveStarted` 시 상단에 "웨이브 N" 대형 배너가 0.25 s 스케일 펀치인 후 1.2 s 유지·페이드 (현재 로어 텍스트만 교체되어 웨이브 전환 체감 약함).
- **WHERE**: `HudView.OnEvents` L722-726 WaveStarted 분기 — 배너 패널 1장 신규(중앙 상단, 레이캐스트 off).
- **HOW**: `scale: 1.4→1` easeOut + alpha 0→1→0. `sim.Wave` 텍스트는 이벤트 시 1회 갱신(무할당 캐시 불필요 — 웨이브당 1회).
  보스 웨이브(`sim.BossAlive`)면 배너 색 엠버→적색.
- **COST**: S
- **RISK**: 스테이지 배너(캠페인)와 위치 겹침 — y를 배너 아래 -60으로 오프셋.

---

## 컷 라인 (시간 압박 시 아래부터 순서대로 드랍)

**드랍 순서**: #20 웨이브 배너 → #17 벤트 버스트 → #16 추출 마커/빔 → #14 정예 펄스 → #12 보스 인트로 레터박스(포커스 풀만 남기고 바 드랍 → 그래도 부족하면 전체) → #13 픽업 흡인 → #8 스윙 트레일.

**절대 사수 (임팩트 코어)**: #1 히트스톱, #5 적 피격 플래시, #6 데미지 숫자, #4 킬 팝, #2 셰이크 티어, #7 묘지 파동 링(가독성 계약), #18 오디오 배선(무음 버그에 준함), #9+#15 생존 신호(저체력/저기름).

**예산 노트**: 전 항목 무할당 원칙(풀·MaterialPropertyBlock·타이머 필드) 준수 — 유일한 정상상태 할당원은 #6 신규 데미지 문자열(Dictionary 캐시로 봉인). 드로우콜 증가 상한: TextMesh 16 + LineRenderer 4 + Image 5 ≈ 25, p95 16.7 ms 예산 내 [INFERENCE — 빌드 프로파일로 확인 필요].
