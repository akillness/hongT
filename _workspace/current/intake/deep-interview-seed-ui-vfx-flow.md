# Deep Interview Seed — UI·연출·흐름 전면 개선 (2026-08-07)

**상태: SEED (미동결).** 아래 D1–D9 결정이 확정되면 동결하고 `ralplan`으로 넘긴다.
표기 규약은 CLAUDE.md §4를 따른다: `[OBSERVED]`(파일:라인 확인) /
`[INFERENCE]`(추론) / `[TARGET]`(목표치).

## 0. 입력 (사용자 요청 원문 요약)

핵앤슬래시 연출·타격감 강화, 유니티 쉐이더/이펙트, 디펜스 웨이브 난이도(포인트+DDA),
던전 구성, 아이템 드롭률, 미스트 안개, 카메라 연출, 동료 자율성 + 동료별 고유 스킬,
플레이어 근접 반응 범위 확대, 캐릭터 절대 크기 축소(던전 해상도 향상), 이동 범위 ≤ 표시 맵,
UI 스케일 개선, 보스 다양화, 인트로→로비→스토리→스테이지→로딩→스토리→던전 흐름,
로비 캠페인 미니맵(밝혀가는 UI), ElevenLabs BGM(인트로/로비/로딩/스테이지별) + SFX
(버튼/획득/발자국/공격), 최종보스 `broken-court-monarch-boss-character` / 플레이어
`human-command-boss-character`, 모션(공격/스킬/이동/콤보/대시), 무기 외형 단검·활·해머,
커맨드 입력을 동작 조합 시퀀스(작업 큐)로 → 이벤트 단위 발동, 텍스트 삭제, 한글 입력.

**레퍼런스 이미지 3종** (OCR 증거: `_workspace/current/intake/reference-ui-ocr.txt`)
— 전부 액션RPG **아킬레우스(Achilleos)**:
- s1/s2: YouTube 리뷰 프레임. 장비/능력/제작/지도/도움말 탭, 등급 라벨(신화), 갑옷 방어력·
  저항력 3열 스탯, XP 바, **보스 대사 자막 밴드**(`포보스: 감히 내 영역 안에서…`).
- s3 (1920×1080 게임 원본): 상단 탭바 `STORY | EQUIPMENT | SKILLS | CRAFTING | MAP |
  TUTORIALS`, 우상단 XP/골드, 중앙 아이템 상세(`ATALANTA'S SPEAR` / `EPIC`),
  3열 스탯(DAMAGE / DAMAGE REDUCTION / ATTRIBUTE BONUS), 좌측 무기 카테고리(`SPEARS`),
  하단 힌트(`SELECT` / `EQUIP` / `ESC BACK`).

## 1. [OBSERVED] 이미 구현되어 있어 **재제안 금지**인 것

| 요청 항목 | 실제 상태 | 근거 |
|---|---|---|
| 콤보 스트링 | 3타(58/58/87) + 홀드차지(×1.8) + 피니셔 4변형 완비 | 스펙 §2.1, `HackSpec.Combo*`, AMENDMENT #5 |
| 넉백 | `Knockback`/`KnockbackPlayer` 프리미티브 존재 | `CinderSim.cs:1069, 1093, 978` |
| 대시 | 190px/0.22s/쿨1.6s/무적, `SimEvents.DashUsed` | 스펙 §2.2 |
| 히트스톱·셰이크·데미지 숫자 | View에 구현됨 | `improvement-brainstorm.md` "재제안 금지" |
| 무기 궤적 트레일 | RightHand 본 `TrailRenderer`, 콤보 티어별 굵기/색 | `ActorView.cs:510-531, 392-401` |
| 안개(포그) | `RenderSettings` 리니어 포그를 카메라 거리에 매 프레임 동기 | `CameraRig.cs:50-51, 264-266` |
| 동료 다중 슬롯(0..3) + 아키타입 스탯 | AMENDMENT #6 DRAFT로 **구현 완료** | `CinderSim.cs:187-329`, `CompanionSlots_*` 테스트 8종 |
| 보스 3페이즈 | AMENDMENT #4 완비 | `BossPhaseIndexFor` 등 |
| 단일 씬 상태머신 흐름 | `Lobby/Prologue/Dungeon/Arena` + 인트로 영상 + 컷씬 로딩 | `GameDirector.cs:11, 82-97, 263-308` |
| 스테이지→로딩→스토리 | `CutsceneView.Show(sprite,kicker,title,narration)` 이미 호출됨 | `GameDirector.cs:302-308` |
| 캠페인 저장/해금 | `ClearedMask`+`IsUnlocked`, 6스테이지 프리즈 카탈로그 | `CampaignStore.cs:11-29`, `StageCatalog.cs:100-138, 266-273` |
| 커맨드 콘솔 텍스트 삭제 | 백스페이스/Delete 1글자 삭제 구현 | `CommandConsoleBuffer.cs:60-68` |
| 한글 중복 입력 버그 | 수정 완료(readOnly 필드 + 순수 C# 버퍼) | `qa/command-console-hangul-duplication.md` |
| 카메라 거리 기반 캐릭터 축소 | 2026-08 결정으로 이미 ×1.17 축소 | `CameraRig.cs:36-42` |

## 2. [OBSERVED] 전무하거나 부분뿐 — 이번 사이클의 실제 작업면

| # | 워크스트림 | 상태 | 레인 |
|---|---|---|---|
| ~~W1~~ | 동료 자율성(타깃락·리시 추격·자동 복귀) | **완료** — AMENDMENT #7 (`b9a728c`), EditMode 316/316 | ✅ landed |
| ~~W2~~ | 동료별 고유 스킬 1종 | **완료** — AMENDMENT #8 (`d0fe934`). #3 non-goal 중 "companion skills/cooldowns" 한 줄만 supersede, equipment/persistence는 유지. 4종(Volley/Hex/Quake/Flare)이 쿨·반경·피해·타깃수 **4축 모두** 상이 | ✅ landed |
| W3 | 모멘텀 게이지(공격할수록 강해짐) | 전무. `HackSpec`/`IHackSnapshot`/`SimEvents` 어디에도 없음 | **Sim amendment** |
| W4 | 웨이브 포인트 기반 난이도 + DDA | 전무. 현재 `86 + min(140,(wave-1)×11)` 고정식 | **Sim amendment** |
| W5 | 아이템 등급 드롭테이블 + bad-luck protection | 전무. 현재 보스 확정 + `id%7` 파편뿐(§6) | **Sim amendment** |
| W6 | 보스 다양화 | 부분. visual 2종, 페이즈 테이블 전 보스 공유 | Sim + Asset |
| W7 | UI 스케일/전투 몰입(아킬레우스형 탭 메타화면) | 부분. HUD 티어(Full/Compact/Phone) 존재, 메타화면 탭 UI 없음 | **View-only** |
| W8 | 로비 캠페인 미니맵(밝혀가는 노드) | 전무. `StageEntry`에 노드 좌표 필드 없음(`StageCatalog.cs:29-66`) | View + 카탈로그 확장 |
| W9 | 미스트/스킬 이펙트 화려함, 카메라 줌·롤 연출 | 부분. 셰이크는 2D Perlin 오프셋만, **FOV 펀치/롤 없음**(`CameraRig.cs:318-322`) | **View-only** |
| W10 | 커맨드 시퀀스 → 작업 큐 → 이벤트 발동 | 전무(단일 인텐트 즉시 소비). 확장점은 `CompanionCommandParser.Parse` + `HudView.ApplyCommandIntent`(`HudView.cs:1351`) + `GameDirector.OnRunEvents`(`:492-533`) | **View-only** |
| W11 | WebGL 한글 IME 입력 | 미지원 추정. emscripten이 `compositionstart/update/end` 미처리 → 숨은 HTML input + `.jslib` 필요 | View + 빌드 템플릿 |
| W12 | BGM(인트로/로비/로딩/스테이지) + SFX 4종 | 부분. `cue-*.mp3` 10종 존재, BGM 세트 없음. `gen_sfx.py`는 sound-generation **22초 상한** | **Asset 파이프라인** |
| W13 | 플레이어=`human-command-boss`, 최종보스=`broken-court-monarch-boss` | 부분. `human-command-boss`는 `CharacterRoster.Ids`에 없음 | Sim 로스터 + Asset |
| W14 | 단검·활·해머 외형 | 전무. `AttachEquipProps`가 `equip-weapon-{basic,fine}`만 로드, image→3D 툴 부재 → **Blender 절차적 저폴리 오써링이 유일 경로** | Asset(Blender MCP) |

## 3. 확정된 설계 제약 (변경 불가)

- `ViewWorld.Scale = 0.01f`(`ViewWorld.cs:9`)가 Sim↔View 유일 분리점. **건드리면 안 됨**
  (카메라 거리·포그 오프셋·모든 링 반경·헬스바 높이가 이 값 기준 튜닝) [INFERENCE].
  캐릭터만 축소하려면 `ActorView.Create(..., baseScale)` 호출처 3곳
  (`GameView.cs:141` 플레이어 1f / `:200` 동료 0.92f / `:717` 적) + `Bootstrap.EnemyVisualFor` 테이블.
- 모든 신규 머티리얼은 `ViewWorld.MakeUnlit` / `MakeAdditive` 시드를 클론해야 WebGL 변이
  스트리핑을 통과(`ViewWorld.cs:28-42`). **VFX Graph 금지**(compute 미지원, `VfxDirector.cs:193-198`).
- §13 **전 모드 RNG 금지**. 확률은 모듈러/카운터/해시로만 (선례: `EliteSpawnModulus=7`,
  장비 드롭 `id%7`, Ember Rest deterministic offer hash). → W4 DDA·W5 드롭등급은
  **pity 카운터 + 모듈러**로 설계하거나 §13을 개정해 시드고정 PRNG를 도입해야 함(D5).
- 작업 큐(W10)는 반드시 View 레이어. `InputAdapter.Queue*` 래치가 프레임당 1개 소비이므로
  결정론 무해 [INFERENCE].

## 4. Non-goals (이번 사이클 제외)

- `ViewWorld.Scale` 변경, 아레나/프롤로그 회귀 digest 파기, VFX Graph/compute 도입.
- 멀티 씬 아키텍처 전환(현재 단일 씬 코드 조립 계약 유지).
- 동료 피격 대상화(untargetable 유지).
- 신규 3D 캐릭터 메시 생성(리스킨/리타겟만).

## 5. 블로킹 결정 (D1–D9) — 각 항목에 **제안 기본값** 명시

| ID | 결정 | 제안 기본값 |
|---|---|---|
| D1 | AMENDMENT #6(동료 0..3 슬롯)을 DRAFT→FROZEN 승격 후 #7을 얹는가? | **승격 먼저.** #7은 #6 위에서만 정의 |
| D2 | 동료 자율성을 **모든 슬롯(0..3)** 에 적용하고, 단일동료 digest는 파기하지 않는가? (사용자: "단일동료면 안 돼") | **전 슬롯 적용 + `_dungeon` 게이트 한정**, Arena/Prologue/무동료 digest 불변 |
| D3 | 근접 반응 범위 확대·캐릭터 축소가 아레나 회귀 digest를 건드려도 되는가? | **불가.** 던전 한정 상수로 분리 |
| D4 | 캐릭터 축소를 카메라 거리(기존 방식)로 더 할지, `baseScale`로 할지? | **baseScale 축소(전 액터 동일 배율)** + 카메라 거리 유지 → 비율 보존, 던전 시야 확대 |
| D5 | 드롭 등급/DDA 확률 구현 방식 | **pity 카운터 + 모듈러**(§13 무개정) |
| D6 | 아킬레우스형 탭 UI 적용 범위 | **로비/메타 화면 한정.** 전투 HUD는 축소·정리만 |
| D7 | BGM 생성 경로 | ElevenLabs **Music API 신규 경로 추가**(22초 루프는 BGM으로 부적합). 실패 시 Abyssal-Surge `audio/elevenlabs/loops/` 재사용 |
| D8 | 무기 단검·활·해머 | **Blender MCP(포트 9876 가동 확인) 절차적 저폴리 오써링 ≤800 tri**, `equip-weapon-{archetype}-{basic,fine}` 네이밍으로 `AttachEquipProps` 확장 |
| D9 | WebGL 한글 IME(숨은 HTML input + `.jslib`) 이번 범위 포함? | **포함하되 마지막 레인**(배포 검증 필요) |

## 6. 수용 기준 (Acceptance Criteria) 초안

1. EditMode 테스트 **전량 그린**, 신규 기능마다 테스트 추가(동료 자율성 ≥8, 커맨드 큐 ≥6,
   드롭/DDA ≥6). 기존 digest 테스트는 **재-bless 없이** 통과해야 한다(D2/D3 전제).
2. WebGL 빌드 `errors=0`, 총 용량 ≤120 MB, 텍스처 ≤1024, 캐릭터 ≤25k tri.
3. 데스크톱 p95 프레임 ≤16.7 ms 유지(PostFx 켠 상태).
4. 배포 후 https://akillness.github.io/hongT/ 에서 인트로→로비→스테이지→던전 흐름과
   BGM/SFX 재생, 커맨드 큐 발동을 실제 관측하고 증거를 기록.
5. 모든 생성 자산은 `docs/provenance/`에 프롬프트·도구·소스 기록.

## 7. 잔여 모호성 (계획을 바꾸는 것만)

- "이동 범위가 보이는 맵을 넘지 않도록": Sim 클램프(520×270)와 던전 바닥 메시(SceneBuilder)
  실측 대조가 아직 안 됨 → 기준값 미확정. **Sim 수치는 게이트이므로 View 바닥/카메라 쪽에서
  맞추는 것이 기본안.**
- "동료별 고유 스킬"의 발동 주체: 자동(쿨다운) vs 커맨드 큐 지시. 기본안은 **자동 + 커맨드로 강제 발동 가능**.
- 보스 다양화의 개수/성격(신규 패턴 몇 종인지) 미정. 기본안 **최종보스 1 + 스테이지 보스 3종 차별화**.
