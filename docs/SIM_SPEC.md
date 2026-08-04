# Cinder Court — Frozen Simulation Spec (Unity port)

// FROZEN CONTRACT — 수치 변경 금지. 원본: Abyssal-Surge sprite-2-5d.js (관측 추출).
모든 구현(C# Sim, View, 테스트)은 이 파일을 단일 진실로 삼는다.

## World

| 항목 | 값 |
|---|---|
| 고정스텝 | `1/60` s, accumulator, `MAX_FRAME_DELTA 0.25`, `MAX_CATCH_UP_STEPS 5` |
| 월드 | 1536 × 1024 (원본 px 단위 유지; Unity에선 1 unit = 1 px, XZ 평면) |
| 아레나 중심 | (768, 604) |
| 아레나 반경 | half-width 520, half-height 270 (타원 클램프 아님: AABB 클램프) |
| 깊이 스케일 | far 0.62 → near 1.0, y 정규화 후 9단계 양자화 (View 전용) |

- 좌표 계약: 시뮬레이션은 원본 2D 좌표(x→우, y→화면 아래=near)를 그대로 쓴다.
  View가 `world(x, y) → Unity (x * S, 0, -y * S)` 로 변환한다 (`S = 0.01`).
- 클램프 (다이아몬드/L1, 원본 그대로):
  `halfW = 520 − margin`, `halfH = 270 − margin*0.5`,
  `n = |x−768|/halfW + |y−604|/halfH`; `n > 1`이면 `localX /= n`, `localY /= n`.
  AABB 클램프 아님. 플레이어 margin 34, 적 margin 24.
- 아이소 전투 거리: `dist² = dx² + (dy*1.42)²`.
- 전방 판정: `dx * facing ≥ -18`.

## Player (Dusk Warden)

| 항목 | 값 |
|---|---|
| HP | 100 |
| 이동속도 | 218 u/s (x축), y축은 ×0.68 |
| 공격 중 이동배율 | 0.42 |
| 공격력 / 사거리 / 쿨다운 | 58 / 160 / 0.48 s |
| 피격 무적(grace) | 0.38 s |
| 시작 위치 | (768, 646), facing +1 |
| 공격 활성창 | 공격 시작 후 0.167–0.333 s (원본 5프레임 12fps 클립의 frame 2–3) |

- 대각 이동은 정규화. facing은 x 입력이 있을 때만 갱신.
- 공격은 큐잉: 쿨다운 0 & 비공격 상태일 때 발동, attackId 증가.
- 한 번의 공격(attackId)당 적 1기는 1회만 피격 (`lastHitAttack`).

## Enemy (Ember Cohort 계열)

| 항목 | 값 |
|---|---|
| 기본 HP | `58 + min(92, (wave-1)*9)` |
| 사거리 / 쿨다운 | 76 / `1.22 + min(0.38, wave*0.025)` s |
| 이동속도 | `min(128, 78 + wave*3.2 + (id%3)*2.5)` u/s, y축 ×0.68 |
| 동시 상한 | 20 |
| 접촉 피해 | `min(18, 7 + floor(wave*0.8))`, 공격 클립 frame≥2 시점(≈0.167 s), 판정 반경 `(76+14)` |
| 분리(boid) | 반경 70, 가중치 `(70−d)/70 × 0.76` |
| 첫 공격 지연 | `(id % 3) * 0.18` s |
| 사망 페이드 | 0.34 s (reduced-motion 0.08) |

- 추적: 거리 > 사거리−5 이면 이동, 아니면 idle. facing은 |dx|>4일 때 갱신.
- 스폰 포인트 8개: (284,577) (421,405) (694,350) (1027,389) (1239,570)
  (1138,743) (848,840) (536,798).
- 스폰 선택: `(waveSeed + enemyId*3) % 8`, `waveSeed = (wave*3) % 8`.
- 스폰 간격: 첫 0.18 s, 이후 `max(0.28, 0.62 − wave*0.018)` s.

## Wave

- 웨이브 n 스폰 수: `min(20, 3 + floor(n*1.2))`.
- 전멸 + 스폰 큐 소진 → `wave-clear`, 인터미션 2.15 s 후 다음 웨이브.
- 킬 점수 `100 * wave`.

## Lantern oil (charge)

| 항목 | 값 |
|---|---|
| 최대 | 100 (시작 100) |
| 재생 | +7/s |
| 처치 | +6 |
| Nova | 비용 45, 쿨 6.5 s, 반경 250, 피해 96 |
| Ward | 비용 30, 쿨 9 s, 지속 3 s (모든 피해 무효, 접촉은 grace 0.38 s 소모) |

## Pickups

- 드롭 종류: `enemyId % 3` → 0 ember-shard(+18 HP), 1 oil-flask(+35 oil),
  2 relic-mote(+250 score, relics+1).
- 수명 12 s, 자력 반경 78 (아이소 거리), 회수 즉시 적용.

## Bosses (Unity 확장 — 원본 계약 외 신규)

- 웨이브 5, 10, 15… (5의 배수) 시작 시 보스 1기 추가 스폰 (상한 무시하지 않음).
- wave%10==5 → `shadow-commander-boss`, wave%10==0 → `broken-court-monarch-boss`.
- 스탯: HP ×6, 접촉피해 ×2, 이동속도 ×0.7, 스케일 ×1.6, 점수 `1000*wave`,
  드롭은 relic-mote 고정.

## Game over

- HP 0 → run 종료(reason "overrun"). 최종 점수/웨이브/유물/처치 표시, R 재시작.
- 런 다이제스트를 `localStorage["abyssal-lantern:cinder-court:last-run"]`에 기록
  (WebGL: `PlayerPrefs` 대신 jslib로 localStorage 직접 기록, 키 동일).

## Audio cues (ElevenLabs sound-generation API → mp3)

아래 표는 **원본 WebAudio 합성 의미(음색 근거)**다. dur/gain은 재생 계약이 아니다.

| cue | wave type | from→to Hz | dur s | gain |
|---|---|---|---|---|
| strike | square | 320→140 | 0.12 | 0.05 |
| hit | sawtooth | 210→90 | 0.14 | 0.05 |
| kill | triangle | 420→120 | 0.24 | 0.06 |
| nova | sawtooth | 620→70 | 0.55 | 0.09 |
| ward | sine | 180→720 | 0.42 | 0.07 |
| pickup | sine | 640→1180 | 0.16 | 0.05 |
| wave | triangle | 240→480 | 0.42 | 0.06 |
| gameover | sine | 300→60 | 0.9 | 0.09 |

생성 규칙: `tools/audio/gen_sfx.py`가 ElevenLabs `/v1/sound-generation`
(하한 0.5 s)에 큐별 프롬프트로 요청한다. 생성 길이는 스크립트 소관(0.7–2.2 s,
원본보다 김). 재생 계약: AudioDirector는 `PlayOneShot`으로 재생하고 **겹침을
허용**한다(트리밍·컷 없음). 산출: `Assets/Art/Audio/cue-<name>.mp3` +
`docs/provenance/audio.json`. 키는 env/`.env.game-audio`(커밋 금지)에서만 읽는다.

## Input

| 입력 | 동작 |
|---|---|
| WASD/방향키 | 이동 |
| Space | Strike |
| Q | Nova |
| E | Ward |
| R | 재시작 |
| 터치 D-pad + 버튼 | 모바일 동등 입력 |

## Animation action set (11)

`idle move run hit bighit attack critical avoid defence die show`
- 루프: idle, move, run. 원샷: 나머지 (die는 클램프 유지).
- Unity: Humanoid 리타겟, 벤치 Mixamo FBX가 클립 소스.
- 상태 매핑: 이동 중 move(웨이브≥6 상당 속도면 run 아님 — 원본은 walk/idle/attack만
  사용하므로 run은 보스 전용 예약), 공격 attack, 피격 hit, 사망 die.

## Roster

| 역할 | assetId | 소스 |
|---|---|---|
| Player | guard (ember-greatsword-guard) | motion library model.glb |
| Enemy tier 1 (기본) | ember-cohort | 〃 |
| Enemy tier 2 (쾌속) | scout | 〃 |
| Enemy tier 3 (측면) | shade | 〃 |
| Enemy tier 4 (원거리 외형, 동일 규칙) | possessed | 〃 |
| Boss A | shadow-commander-boss | 〃 |
| Boss B | broken-court-monarch-boss | 〃 |

적 tier는 웨이브에 따라 외형 로테이션(`(wave + spawnIndex) % 4`)이며 전투 수치는
동일(수치 계약 보존). 시뮬레이션은 `visualKind`만 구분한다.

## Determinism

- 시뮬레이션은 `UnityEngine` 참조 금지 (System.Math만). RNG 없음 — 원본은 전부
  결정적 산술(모듈러)이다. 같은 입력 시퀀스 → 같은 `RunDigest`.
- `RunDigest { score, wave, kills, relics, healthRemaining, reason }`.
