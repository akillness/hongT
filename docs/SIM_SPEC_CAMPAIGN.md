# Cinder Court — Campaign Amendment (2026-08-04)

// FROZEN CONTRACT AMENDMENT — SIM_SPEC.md의 모든 규칙은 유지된다. 이 문서는
// 캠페인 모드가 **추가**하는 규칙만 정의한다. 사용자 지시: campaign.html까지
// 개발, 아이템 드롭 + 던전 기믹 배치, 원작 구성 참고해 페이지 흐름 연결.
//
// ── 2026-08-07 최신화 (additive — 본문 재작성 없음) ──────────────────
// 이 문서는 개정 원장 #1이다 (SIM_SPEC_HACKSLASH.md 부록 A).
// 후속 개정이 대체(supersede)한 조항:
// - §Page flow의 campaign.html 정적 허브 → HACKSLASH §0이 대체:
//   campaign.html은 index.html 즉시 리다이렉트(구 링크 보존), 허브 역할은
//   단일 씬 로비(§9)가 승계. 딥링크 `?mode=campaign&stage=<id>`는 유지.
// - §Stages 보스 스탯 "SIM_SPEC §Bosses 그대로" → HACKSLASH §7(3페이즈,
//   DungeonBossHealthMul ×6 추가)이 던전 경로에서 대체. v0.2.0부터 캠페인
//   런은 전부 GameMode.Dungeon(타원 클램프·던전 전투킷) 경로다.
// - 스테이지 3종 → 9종: 논리 6종(StageCatalog, HazardOverride) +
//   앵커 3종(SIM_SPEC_DUNGEONS.md #5). 환경 시각 구조는
//   SIM_SPEC_ENVIRONMENT.md #12.
// - localStorage 스키마는 v2로 확장(HACKSLASH §11 — 하위호환 유지).

## Page flow (원작 index → campaign 구성 이식)

| 페이지 | 역할 | 소스 |
|---|---|---|
| `index.html` | Unity 게임 (아레나 = 기본 모드, `?mode=campaign&stage=<id>`로 캠페인 런) | Unity 빌드 산출물 |
| `campaign.html` | 정적 허브: 스테이지 카드 3장, 진행도 표시, 런 시작 링크 | `web/campaign.html` (배포 오버레이) |

- 게임오버/스테이지 클리어 패널에 "캠페인으로" 버튼 → `campaign.html`.
- 진행도 localStorage 키: `"abyssal-lantern:unity:campaign"` =
  `{"cleared":["cinder-span",...],"equipment":{"weapon":N,"lantern":N,"cloak":N}}`
  (jslib CinderStorageGet/Set — 게임과 정적 페이지가 같은 키를 읽는다).

## Stages (원작 campaign-state.js STAGES 3종 이식)

| id | 이름 | 보스 표기 | 보스 모델 | 웨이브 수 W |
|---|---|---|---|---|
| cinder-span | Cinder Span | Cinder Warden | shadow-commander-boss | 5 |
| abyss-chancel | Abyss Chancel | Veil Tactician | shadow-commander-boss | 6 |
| echo-throne | Echo Throne | Gate Sovereign | broken-court-monarch-boss | 7 |

- 웨이브 1..W는 아레나 규칙 그대로 (수치 계약 불변).
- 웨이브 W+1 = **보스 웨이브**: 보스 1기 + 호위 `min(8, 3+stageIndex*2)`기.
  보스 스탯은 SIM_SPEC §Bosses (HP×6, 접촉×2, 속도×0.7, 스케일×1.6).
- 보스 처치 → `SimEvents.StageCleared`. 남은 잡몹은 즉시 페이드(전투 종료).
  클리어 시 진행도 기록 + 장비 드롭 1개 확정.
- 캠페인 런의 플레이어 사망 = 기존 GameOver (스테이지 재도전).
- 아레나 모드(기본)는 기존 무한 웨이브 그대로 — 회귀 금지.

## Item drops (장비 파편 3슬롯)

- 슬롯: `weapon`(공격 +6%/랭크), `lantern`(기름재생 +8%/랭크), `cloak`(HP +8/랭크).
  랭크 0..5. 효과는 **런 시작 시 1회 적용** (파생 스탯 계약: 공격 58→58*(1+0.06r),
  재생 7→7*(1+0.08r), HP 100→100+8r).
- 드롭 규칙 (결정적, RNG 금지):
  - 보스 처치: 확정 1드롭, 슬롯 = `stageIndex % 3`, 즉시 랭크+1 (최대 5).
  - 일반 처치: `enemyId % 7 == 3`일 때 파편 픽업 스폰(PickupKind.Shard 신설 아님 —
    기존 3종 유지, 4번째 kind `EquipShard` 추가), 회수 시 `킬수 % 3` 슬롯 랭크+1.
- HUD: 좌하단 장비 3슬롯 미니 패널 (슬롯명 + 랭크 pip).
- 지속성: 캠페인 모드에서만 localStorage 캠페인 키에 저장. 아레나 모드는 미적용.

## Dungeon gimmicks (결정적 배치)

좌표는 심 좌표(px). 모든 판정은 아이소 거리(`dy*1.42`) — SIM_SPEC 동일.

### ember-vent (분출구) — 주기 AoE
- 필드: `{x, y, radius: 90, period: 2.4 s, telegraph: 0.8 s, damage: 8}`
- 사이클: `t = fmod(stageTime + phase, period)`; `t ∈ [period−telegraph, period)`
  동안 텔레그래프(뷰: 링 점멸), 사이클 경계에서 반경 내 플레이어에게 8 피해
  (Ward는 무효화, grace 소모 규칙 동일). 적에게는 무해(원작 정신: 기믹은
  플레이어 리스크).
- `SimEvents.HazardPulse` 발생 (뷰/오디오 큐).

### obsidian-pillar (흑요석 기둥) — 이동 차단
- 필드: `{x, y, radius: 40}` 원형 블로커. 플레이어·적 모두 이동 후 원 밖으로
  푸시아웃 (`dist < r+actorRadius` → 법선 방향 밀어냄; actorRadius: 플레이어 26,
  적 22). 아레나 클램프 후 적용. **거리는 다른 모든 전투 판정과 동일하게
  아이소 계량(`dy×1.42`)** — 화면상 원, 심 px 공간에선 타원.

### relic-altar (유물 제단) — 스탠드 버프
- 필드: `{x, y, radius: 70, holdSeconds: 1.2, oilBurst: 18, cooldown: 6 s}`
- 반경 내 연속 체류 1.2 s → 기름 +18 (`SimEvents.AltarBlessing`), 쿨 6 s.

### 배치 테이블

| stage | gimmicks |
|---|---|
| cinder-span | vent(560,480,phase 0) · vent(980,720,phase 1.2) |
| abyss-chancel | pillar(640,500) · pillar(900,700) · pillar(768,604) · vent(1100,450,phase 0.6) |
| echo-throne | altar(768,604) · vent(500,700,phase 0) · vent(1030,480,phase 1.2) |

## SimTypes 증분 (동결 해제 항목 — 이 목록 외 수정 금지)

- `SimEvents`: `StageCleared = 1<<10`, `HazardPulse = 1<<11`,
  `AltarBlessing = 1<<12`, `EquipDropped = 1<<13`.
- `PickupKind.EquipShard = 3`.
- 신규 파일 `CampaignTypes.cs`: `CampaignConfig`(stage id/index, waves, boss visual,
  hazards[], equipment ranks), `HazardKind`, `HazardState`(뷰 노출용: kind, x, y,
  radius, cycleT, telegraphing, cooldownT), `ICampaignSnapshot : ISimSnapshot`
  (`string StageId`, `bool BossAlive`, `bool StageCleared`,
  `IReadOnlyList<HazardState> Hazards`, `int WeaponRank/LanternRank/CloakRank`).
- `CinderSim`에 생성자 오버로드 `CinderSim(in CampaignConfig)`. 기본 생성자 동작
  불변(아레나). `ICinderSim` 불변.

## Determinism

캠페인 역시 RNG 금지. 해저드 위상/드롭 모듈러는 위 수식 그대로.
동일 스테이지+장비+입력 → 동일 Digest (Digest.Reason: "overrun" | "stage-clear").
