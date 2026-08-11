# AMENDMENT #17c 실행 계획 — 잔여 6항목 + VFX 텍스처화

작성 2026-08-11. #17b(b82f8a1) 직후 상태 기준. **구현 세션(Opus 5)이 이 문서만
읽고 착수할 수 있도록** 각 항목에 (근거 → 스펙 → 검증 → 함정)을 적는다.
표기: `[OBSERVED]` 실측 / `[TARGET]` 목표 / `[DECIDED]` 사용자 결정.

착수 전 필독: CLAUDE.md §4c(브라우저 스모크) · §4i(한 사실 한 출처) ·
§4m(정답=오답 좌표계) · §4q(만장일치 무방비) · §4y(측정 프로토콜) ·
§5(동시 세션 git). 하네스: `/tmp/simprobe` (사라졌으면
`llm-wiki/wiki/hongt-amendment-17b-stage-coverage-and-headless-input.md` §4와
이 문서 부록으로 재생성).

---

## 항목 1 — 스톨 재측정 ✅ 완료 (이 문서 작성 시점에 닫음)

[OBSERVED] 2026-08-11, `/tmp/simprobe` Stall.cs, 9스테이지 × 2분(7,200틱),
패트롤 봇 + Nova/Ward 매 틱 큐(§4z), 귀속 규칙: **다음 걸음이 블로커 안**
(`sim.IsBlocked`) + 30틱 지속.

```
stage          enemyTicks  stall% maxRun     게이트: ≤ 2.89% (17b 이전 밴드)
cinder-span         24004    1.39    312
ember-gallery       17491    1.44    184
abyss-chancel       18892    2.33    213
witness-well        26613    0.76     97
echo-throne         40401    0.28     60
ash-verdict         12968    1.06     66
cinder-sluice       27185    0.09     53
ember-bastion       23741    2.70    297
ash-march           29297    0.46     72
```

전 스테이지 통과. 클리어런스 완화(ClearanceFor)는 스톨 부채를 만들지 않았다.
**후속 없음.** 단 maxRun 312(cinder-span, ~5.2초 단일 최악 정체)는 항목 5의
육안 검수에서 해당 스테이지 북서 커버 군집을 한 번 볼 것.

---

## 항목 2 — 제단·방벽주 실물 충돌 적용 [DECIDED: 적용한다]

#17b가 구현했다가 서명 계약과 충돌해 되돌린 것을, **계약을 함께 개정하며**
다시 넣는다. 되돌린 코드는 git 이력에 있다 — 재발명하지 말 것.

### 2a. 심 변경 (한 곳)

`CinderSim.SolidRadius` (Assets/Scripts/Sim/CinderSim.cs, `ApplyBlockers` 아래):

```csharp
case HazardKind.RelicAltar: return CampaignSpec.AltarBodyRadius;  // 24
case HazardKind.EmberPylon: return CampaignSpec.PylonBodyRadius;  // 30 (이미 존재)
```

`CampaignSpec.AltarBodyRadius = 24f`를 복원한다(b82f8a1 직전 이력에 독스트링
포함 원문 있음). 산술: 채널 가능 조건 `body + PlayerPushRadius(26) < AltarRadius(70)`
→ 링 깊이 20 iso px. **이 부등식 자체를 테스트로 박는다**(수치 재진술 금지, §4i).

조향은 이미 `SolidRadius`를 공유하므로 **추가 편집 불요.**

### 2b. 개정해야 하는 서명 계약 (전부 실측된 실패 목록)

[OBSERVED] 17b에서 이 변경으로 깨진 테스트와 각각의 개정 방향:

| 테스트 | 깨진 이유 | 개정 |
|---|---|---|
| `CampaignSimTests.AshMarch_FinalePylon_ShieldsAltarWithoutBlockingCorridor:1875` "y 604 ±14" | 플레이어가 코어에 막혀 y 639.2에 섬 (604 + 50/1.42, 정확한 산술) | y밴드는 **전제**였지 계약이 아님. `IsoDistance(제단) ≤ AltarRadius` ∧ `IsoDistance(방벽주) ≤ PylonAuraRadius`로 교체 — 17b에서 작성했다 되돌린 개정문이 이력에 있음 |
| 같은 테스트 `:1880` "the player must pass THROUGH the pylon body row" (x 768 직진 통과) | 방벽주 코어가 막음 | **회랑 불변식을 '통과'에서 '우회 관통'으로 개정**: 시작 y > 604, 목표 y < 420, 도착 성공 + `소요틱 ≤ 직선틱 × 2.5`(§3.6 우회비 게이트 재사용). ash-march는 전진 잿벽 스테이지이므로 **시간 상한이 안전 계약의 실체**다 — 상한 없는 개정은 금지 |
| `AshVerdict_PylonAura_ShieldsAltarUntilPylonDown` "×0.40 … But was: -1.0" | 봇이 제단 중심 도달 실패로 타격 자체가 안 나감 | 봇 목적지를 제단 중심 → **채널 링 위 최근접 서기 가능점**으로. `sim.IsStandable` 나선 탐색이 17b Reach.cs `NearestOpen`에 있음 |
| `NewStages_MutatedPlacement_ChangesDigest` (ember-bastion/ash-march) | 변이 대상 좌표가 새 충돌로 등가가 됨 | 변이 지점을 solid 반경 밖 좌표로 이동. **변이가 RED를 내는지 먼저 확인**(§4m) |

### 2c. 뷰 동반 변경

- 제단: 17b가 넣은 sigil 바닥 타일(VfxDirector `RelicAltar` 케이스)의 주석이
  "고체가 아니므로 대좌를 주지 않는다"고 적고 있다 — **이 전제가 뒤집히므로**
  `kit-altar-plinth`를 body 24 스케일로 복원(이력에 코드 있음)하고 주석 개정.
  채널 원반(70)은 판정 텔레그래프로 유지.
- 방벽주: brazier 셸이 이미 실물(30)과 일치 — 변경 없음.

### 2d. 검증 순서 (이 순서대로)

1. dotnet 컴파일(1.7s) → 2. `/tmp/simprobe` 전 게이트(특히 **제단 도달 9/9**와
   **스톨** 재실행 — 코어가 새 정체점이 될 수 있음) → 3. EditMode 전체 →
   4. 골든: fun-pass 계열이 움직인다. **classic-* 3행은 움직이면 안 된다**
   (동결 경로는 인테리어·기믹 충돌 모두 스트립… 주의: `WithoutLayoutBlockers`는
   StoneWall만 벗긴다. classic 경로에 제단이 있는 스테이지(echo-throne)는
   **골든이 정당하게 움직인다** — 이 경우 §4h 방식으로 사유를 주석에 적고 재핀).

**함정**: `CompositeHazards` 테스트의 v1.2 예외 독스트링("altars are pure
channel discs", `AssertRadialClearance:358` 부근)이 설계 서술이다. 코드만 고치고
이 문장을 남기면 §4i 위반 — **독스트링을 같은 커밋에서 개정.**

---

## 항목 3 — cinder-sluice 인테리어: 심 블로커는 불가능, 뷰 드레싱으로 해소

[OBSERVED] 이 스테이지만 인테리어 0인 것은 규칙의 올바른 결과다:

- 조류 밴드 y 260..580 · 628..948 (중심 420/788, HalfH 160), **전폭**(HalfW=735).
- `InsidePushBand`는 반경 마진 포함: 커버 r40이 서려면 |y−420|>200 ∧ |y−788|>200
  → 유효 y 구간 **공집합** (620 < y < 588). 밴드 사이 48px 틈도, 외곽 스트립
  (링 스탠드오프 |y−604|≤~297)도 전부 밴드 안.
- 밴드 안 고체 = 스톨 3.10% 실측 기각(#17). 밴드 축소는 CurrentHalfH가 훈련
  트라이얼과 공유 계약이라 불가(#17에서 시도·철회 이력 있음).

[TARGET] 따라서 **심은 0을 유지하고, 밀도 동등성은 뷰에서** 만든다:

- `VfxDirector`가 아니라 **`EnvironmentBuilder`**(정적 환경 소유자)에
  sluice 전용 통과 가능 드레싱을 추가: `kit-rubble-heap`·`kit-column-fallen`
  같은 **낮은(높이 ≤ 0.25 world) 조각** 6~10개를 밴드 밖 시각 여백 — 아레나
  타원 밖 & 플레이트 안 — 에 배치. 조류 위에는 절대 놓지 않는다(§E0.5:
  해저드 판독 오염).
- 규칙: **충돌이 없는 것은 충돌이 있어 보이면 안 된다**(§4k 역방향) — 높이와
  실루엣으로 '잔해'로 읽히게, '벽'으로 읽히지 않게.

검증: 항목 4 스모크에서 sluice 스크린샷 → 조류 체브런 가독 + 잔해가 벽으로
안 읽히는지 육안. rect 감사(`OutsideCombatPlane`) 통과.

---

## 항목 4 — 브라우저 스모크 확장: 9스테이지 + 4모드

[OBSERVED] 재료는 전부 확보됨 (위키 §4에 상세):

- Playwright chromium + `--use-gl=swiftshader --enable-unsafe-swiftshader`로
  WebGL 정상 렌더.
- 입력 함정 2개: ①`mouse.click()`은 프레임 샘플링에 삼켜진다 —
  `move→320ms→2px 이동→220ms→down→240ms→up` 시퀀스 필수. ②**첫 프레스는
  포커스로 소비** — 빈 바닥(760,760)에 1회 버릴 것.
- 세이브 시딩: `page.addInitScript`로
  `localStorage['abyssal-lantern:unity:campaign']='{"clearedMask":511,"prologueDone":true}'`
  (511 = 9비트; 커밋 전 `StageCatalog.ValidClearMask`와 대조).
- 기존 프로브: 세션 스크래치 `stage_smoke.mjs` (cinder-span 전용). 이것을
  일반화한다.

[TARGET] 매트릭스와 각 판정(스크린샷은 판정의 증거이지 판정이 아님):

| 경로 | 판정 항목 |
|---|---|
| 던전 9스테이지 각각 | 진입 성공(웨이브 HUD) · 인테리어 가시 · 이동으로 블로커 우회 관찰 · 근접+스킬 1회 이펙트 렌더 |
| Prologue | 적 4 스폰 · 스킬 봉인 · **`캠페인으로` 버튼 존재**(§4l 재발 방지) |
| Training | 기믹 렌더 · 적 0 |
| Arena | 무한 웨이브 진입 · StoneWall **부재**(frozen 경로 스트립 확인) |

구현 메모: 출정 패널의 2·3막 카드는 아코디언 뒤에 있다 — 막 헤더 프레스로
전환 후 좌표 재계산. 좌표는 하드코딩하지 말고 **한 번 스크린샷 찍고 그
프레임에서 읽어 상수표로** 만들 것(£4c CanvasScaler 공식).

산출물: `_workspace/current/qa/amendment17c-smoke/` 에 스크린샷 + 판정표
(스테이지 × 항목, PASS/FAIL/비고).

---

## 항목 5 — 스테이지별 톤앤매너 육안 검수 (항목 4와 같은 런에서)

[OBSERVED] 17b에서 육안 확인된 것은 cinder-span 1개뿐. 기믹 조합이 다른
스테이지는 미확인: 제단×2(witness-well) · 방벽주×3(ember-bastion) ·
조류(echo-throne, cinder-sluice) · 잿벽(ash-march).

체크리스트 (스테이지당):

1. 키트 메시가 서 있고 **흰 쿼드가 없다** (sprite/재질 누락 신호, §4k 계열)
2. 석재 톤 일치: 기둥(kit-column-round) · 벽(kit-wall-straight) ·
   방벽주 셸(brazier) · 제단 타일(sigil) — 전부 `kit-stone.mat` 계열 값 대비
   (stone 0.155 vs floor 0.235)
3. 베이스 링(발밑 시안 링)이 실물 크기와 일치 — 특히 항목 2 적용 후 제단은
   링이 **24 코어**를 가리켜야 함(70 채널 원반과 혼동 금지)
4. 겹침: 새 배치가 기존 UI/드레싱과 겹치지 않음 (§4f)
5. cinder-span 북서 커버 군집(maxRun 312 지점) 부근에서 적 정체가 육안으로
   거슬리는지

판정 기록은 항목 4 판정표에 열 추가.

---

## 항목 6 — dual-bot 밸런스 재측정 (측정 전용, 코드 변경 없음)

[OBSERVED] #17 골든 재핀에서 점수 이동: echo-throne 2250→4600(+104%) ·
abyss-chancel +22% · ash-march +17%. 인테리어가 난이도를 **낮췄을** 가능성
(커버 뒤 안전 지대). 단일 봇 측정 금지 — §4y가 이 저장소에서 계약이다.

[TARGET] 프로토콜:

- 봇 2종을 `/tmp/simprobe`에 구현: **harvest-bot**(제단·드롭 수거 우선,
  17b Stall.cs 패트롤 봇 확장) / **kite-bot**(적 최근접 반대 방향 + 사거리
  가장자리 유지). 둘 다 Nova/Ward 매 틱 큐(§4z).
- 측정: 6스테이지(심 앵커) × 2봇 × #17 이전/이후 해저드표(이전 표는
  `git show d85cd21^:Assets/Scripts/Sim/CampaignTypes.cs`에서 추출) = 24런.
  각 런 고정 300초, 산출: 점수·클리어 여부·사망 시각.
- 판정: 두 봇 **모두**에서 이후 점수가 이전 대비 +50% 이상이면 난이도 하락
  실재로 등재 → 밸런스 조정은 **별도 개정으로 협상**(이 계획의 범위 밖,
  스폰 수·커버 반경이 후보라는 메모만 남긴다).
- 산출물: `_workspace/current/qa/amendment17c-parity.md` 브래킷 보고(§4x:
  두 점 사이를 곡선으로 채우지 말 것).

---

## 항목 8 + VFX — Codex-first 생성 이미지로 이펙트 개선 (gti fallback)

**이 절이 VFX 작업의 본체다.** 목표는 "절차적 도형을 시트로 바꾸기"가 아니라
**생성된 실제 이미지를 이펙트의 형태 소스로 삼는 것**이다. 2026-08-11 사용자
후속 지시가 아래의 초기 gti 결정보다 우선한다: Codex 내장 생성이 1순위이고,
실패할 때만 `$god-tibo-imagen`을 fallback으로 쓴다.

### 도구 결정 (2026-08-11 실측, 추정 아님)

| 경로 | 결과 |
|---|---|
| Codex built-in imagegen | ✅ V1~V4 원본 4건 모두 생성 성공. 1254² RGB, 흑배경·무문자·무그리드 육안 확인 |
| `gti --prompt ... --output ...` (기본 = private-codex) | ✅ **HTTP 200, 1024²급 PNG 생성.** 2026-08-06 provenance의 429 쿼터는 해소됨 |
| `gti --provider codex-cli` | ❌ `codex exec`가 `reasoning.effort: 'max'` 거부 (모델 `gpt-5.4-…-premium`이 none/low/medium/high/xhigh만 지원, HTTP 400) |
| `gti --size ...` + codex-cli | ❌ `The codex-cli provider does not support output size selection` |

→ **현재 결정은 Codex built-in 우선이다.** 이번 4건이 모두 성공했으므로 gti는
호출하지 않았다. Codex가 실패하는 후속 생성에서만 gti 기본 프로바이더를 쓰고
`--provider`·`--size`를 붙이지 않는다. 셀 크기는 조립기가 256으로 리샘플하므로
원본 해상도는 무관하다. codex-cli 제약은 gti 버그가 아니라 계정 모델 설정이다.

[HISTORICAL OBSERVED] 초기 gti 검증에서 **첫 비교 후보를 생성했다**:
`_workspace/current/engineering/fx-gen/eruption-base.png` (1.5 MB,
중앙 정렬 용암 왕관 · 순수 흑배경 · 흰 코어). 후보는 보존하지만 Codex-first
생성이 성공해 런타임 V2 시트에는 사용하지 않았다.

### 생성 규칙 (실패 이력에서 나온 것들 — 재발견 금지)

1. **프레임 그리드를 모델에게 그리게 하지 마라.** 베이스 1장만 생성하고
   프레임은 `tools/gen_combat_fx_sheets.py`가 산술로 조립한다. 근거:
   `docs/provenance/combat-fx.json:why_base_only`, `tools/gen_terrain_fx_sheets.py`,
   그리고 캐릭터 아틀라스 시도가 시트에 섹션 라벨을 그려 기각된 이력.
2. **프롬프트 필수 요소**: `on pure black` · `centred` · `no text, no border` ·
   `square` · `high contrast for additive blending`. 흑배경이 곧 마스크다
   (`load_mask`가 휘도를 그대로 알파로 쓴다).
3. **컬러가 아니라 그레이스케일로 소비된다.** 텍스처는 형태, 틴트는 정체성
   (`SpawnScorch` 독스트링이 원전). 그래서 같은 베이스가 일반타·마무리타에
   각각 다른 색으로 재사용된다 — 색을 프롬프트로 고정하려 하지 마라.
4. **1024 상한.** 신규 PNG의 `.meta`는 기본 2048로 임포트된다 —
   `maxTextureSize`를 손으로 1024로 내려야 하고, 안 내리면
   `TextureImporters_…DoNotExceed1024`가 잡는다(17b 실측 적발).
5. 생성마다 `docs/provenance/combat-fx.json`에 프롬프트·베이스 경로·조립 인자를
   추가한다(§3 계약).

### 생성 → 조립 → 배선 표

각 행은 (프롬프트 → 베이스 → 조립 → 코드 접점) 4단계가 모두 적혀 있어야
착수 가능하다. 조립기 인자: `--base <png> --out <sheet> --mode burst|ring`.

| # | 표면 | 생성 프롬프트 요지 | 조립 | 배선 |
|---|---|---|---|---|
| **V2** | `SpawnEruptionCrown` | Codex `eruption-base-codex.png` — 전면 eruption crown, 하단 고정 | `--mode burst` → `Fx/eruption-sheet.png` | 중앙 vertical crown quad 1장. 리소스/shape gate 실패 시 기존 LineRenderer crown fallback |
| **V1** | 분출구 텔레그래프 [항목 8] | "a thin cracked warning ring of glowing fissures on pure black, seen from above, concentric, no fill in the centre…" | `--mode ring` → `Fx/telegraph-ring-sheet.png` | **아래 접근성 절 필독** |
| **V3** | `SpawnCrackFan` (:1230) | "a fan of jagged glowing cracks radiating from one point on pure black, thin bright fissures…" | 시트 아님 — 단일 데칼 | `scorch-decal` 방식(정적 텍스처 + 스폰별 회전) |
| **V4** | `SpawnShard` (:1281) | "a single elongated molten shard streak on pure black, bright head fading to a thin tail…" | 단일 마스크 | 쿼드 지향(velocity 정렬)은 기존 코드 유지, `_BaseMap`만 교체 |
| **V5** | `SpawnWaveWarnings` (:720) | **생성 없음** | — | 기존 `shockwave-sheet` 재사용 + 경고색 틴트. 새 자산을 만들지 않는 것이 요점 |

**전환하지 않는 것**: 조류 체브런·잿벽 전면(기하가 곧 판독 정보),
게이지류(uGUI, §4k 영역).

### V1 상세 — 접근성 계약을 유지한 채 텍스처화

[HISTORICAL OBSERVED] `combat-fx.json`의 이전 `not_textured` 항목이 이유를 서명했다:
안전 경고 표면이라 reduced-motion에서 **피크 밝기로 고정**되며,
"경고는 그것이 가장 필요한 플레이어에게 더 조용해지면 안 된다." 현재 provenance는
이 결정을 `superseded_decisions`로 보존하고 텍스처화된 경계를 기록한다.

[IMPLEMENTED] 소유권을 쪼갰다:

- **시트는 형태만** 소유 → `_BaseMap` + `_BaseMap_ST` 프레임 스텝.
- **밝기·알파·틴트는 기존 절차 코드가 계속 소유** → MaterialPropertyBlock으로
  곱한다. 이러면 reduced-motion 분기(피크 고정)가 시트와 **무관하게** 성립한다.
- 검증: reduced-motion 켠 스모크 1런에서 주기 내 프레임 2장을 뽑아 텔레그래프
  영역 평균 휘도 차 < 2%임을 증명. 프레임을 눈으로 보는 것으로 대체하지 말 것 —
  §4m(정답과 오답이 같아지는 좌표계)이 정확히 이런 상황을 말한다.

### VFX 검증 (전환 1건마다)

① 베이스 PNG 육안 → ② 조립된 시트 육안(프레임이 **작→큼** 순인지;
`scaled()`가 한 번 나눗셈으로 뒤집혀 히트가 거꾸로 재생된 이력) →
③ `.meta` maxTextureSize 1024 확인 → ④ `bash tools/unity_batch.sh import-only`
(~15s) → ⑤ 항목 4 스모크 프레임에서 렌더 확인 → ⑥ provenance 갱신.

### VFX 구현·검증 결과 (2026-08-11)

- Codex 원본 4건으로 V1 텔레그래프, V2 eruption crown, V3 crack fan,
  V4 shard streak을 조립·배선했다. V5는 계획대로 기존 shockwave 시트를 재사용했다.
- reduced-motion은 실측 최고 총휘도 frame 7(212226)에 고정한다. WebGL 두 프레임의
  환기구 외곽 링 평균 휘도는 91.1026→91.0982, 차이 **0.0048%**로 `<2%` 통과했다.
  근거: `_workspace/current/qa/vfx-codex-reduced/reduced-motion-metrics.json`.
- import-only 성공, 신규 VFX EditMode 9/9 통과, 텍스처 1024 상한 통과,
  WebGL 빌드 성공(오류 0), 브라우저 일반 13장·축소 모션 5장 모두 page error 0.
- 제1·2·3부 전체 9스테이지를 독립 WebGL 컨텍스트로 추가 검증했다. 정상 모드
  135장(스테이지당 15장), 부별 감소 모드 대표 15장, 총 12런 모두 page error 0.
  출정 카드·실제 웨이브 HUD·인테리어·이동·근접·Q/E/Shift/F/R·경고를 담았고
  흰/마젠타 누락 텍스처 쿼드는 없었다. 근거:
  `_workspace/current/qa/amendment17c-smoke/three-act-vfx-matrix.md`.
- 첫 제3부 자동 런은 잘못된 버튼 y좌표 때문에 로비에 남았지만 page error는 0이었다.
  연락판 검수가 이 거짓 양성을 잡았고 좌표를 보정해 제3부 3런과 통합 보고서를
  다시 생성했다. 이후 드라이버는 `GameFlowAgentAPI.observe()`의 실제 wave/phase와
  위치 변화도 통과 조건으로 사용한다.
- 새 감소 모드 대표값은 Ember Gallery 외곽 링 91.2460→90.9213,
  **0.3559%**로 `<2%`를 통과했다. 제2·3부 대표도 실환경 렌더와 page error 0을
  확인했으나, 샘플 쌍이 각각 경고 상태/피격 비네트 경계를 지나 수치 게이트에서는
  제외했다.
- 전체 EditMode 901개 중 4개 실패는 동시 작업 중인 Sim collision/golden digest 변경이며,
  VFX 테스트와 무관하다. VFX 완료 판정에서 숨기지 않고 validation gap으로 남긴다.

### 현존 생성 자산 (재확인 완료, 재생성 불요)

[OBSERVED] 2026-08-11 기준:

| 자산 | 경로 | 용도(코드 접점) |
|---|---|---|
| impact-sheet.png (121KB, 4×4 · 1024) | Assets/Resources/Fx/ | `SpawnHitSpark` (VfxDirector:913) — 배선 완료 |
| shockwave-sheet.png (69KB, 4×4 · 1024) | Assets/Resources/Fx/ | `SpawnBurst` (:843) — 배선 완료 |
| scorch-decal.png (800KB) | Assets/Resources/Fx/ | `SpawnScorch` (:1022) — 배선 완료 |
| terrain-fx-lava/ice/shift-sheet.png | Assets/Resources/Terrain/ | TerrainFlipbook 루프 — 배선 완료 |
| 베이스 impact/shockwave-base.png | _workspace/current/engineering/fx-gen/ | 재조립용 마스터 |
| eruption-base.png (1.5MB) | 같은 경로 | 초기 gti 비교 후보 — 보존, 런타임 미사용 |
| **eruption-base-codex.png** | 같은 경로 | **V2 active source → eruption-sheet.png** |
| **telegraph-ring-base.png** | 같은 경로 | **V1 active source → telegraph-ring-sheet.png** |
| **crack-fan-base.png** | 같은 경로 | **V3 active source → crack-fan.png** |
| **shard-streak-base.png** | 같은 경로 | **V4 active source → shard-streak.png** |

조립기 `tools/gen_combat_fx_sheets.py`는 `--mode burst|ring` 두 곡선을 갖는다:
burst는 접촉 순간이 가장 빠르고 밝기를 1/5 구간 유지, ring은 거의 0에서 시작해
가장자리까지 **도달**하고 홀드 없이 얇아진다. 새 표면이 둘 중 어디에도 안 맞으면
모드를 추가하되 **곡선의 이유를 주석에 적을 것** — 기존 두 모드가 그렇게 돼 있다.

---

## 실행 순서 제안 (의존성)

```
2(심 충돌+계약) ──→ 골든 재핀 ──→ 6(parity, 이후표 기준으로 1회만)
3(sluice 드레싱) ─┐
VFX V1~V5 ────────┼→ 빌드 1회 ──→ 4+5(스모크·육안, 한 런) ──→ 배포
```

2를 먼저 — 골든·parity가 충돌 유무에 종속. 빌드는 한 번으로 몰아 스모크
비용을 아낀다. 커밋은 §5: 명시 pathspec, 다른 세션의
`tools/video/capture-unity-play.mjs` 미커밋 변경(123줄)을 **절대 스테이징 금지.**

## 부록 — 하네스 재생성 (사라진 경우)

`/tmp/simprobe/simprobe.csproj`: net8.0 콘솔,
`<Compile Include="Assets/Scripts/Sim/**/*.cs" />` + Program/Diag/Reach/Stall.
Program은 9스테이지 표(오버라이드는 `StageOverrideHazards.*`), 심 생성은
반드시 `new CinderSim(config, DungeonProgressionConfig.Everything)` —
1인자 생성자는 동결 경계라 StoneWall이 전부 스트립된다(17b에서 36% 균일
walk%로 실측된 함정).
