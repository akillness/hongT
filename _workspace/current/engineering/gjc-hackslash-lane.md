# LANE: Hack & Slash Sim Extension (owner: gjc)

## Mission
`docs/SIM_SPEC_HACKSLASH.md` §0–§7, §12–§13을 시뮬레이션에 구현한다.

수정 허용 파일 (이 목록 외 금지):
- `Assets/Scripts/Sim/SimTypes.cs` — **§12 증분만** (SimInput 3필드, SimEvents 8플래그).
- `Assets/Scripts/Sim/HackTypes.cs` — 신규 (§12 계약 그대로).
- `Assets/Scripts/Sim/CinderSim.cs` — `CinderSim(in HackConfig)` 오버로드.
  기본/캠페인 생성자 경로 동작 절대 불변 (기존 30테스트 = 회귀 게이트).
- `Assets/Tests/EditMode/HackSimTests.cs` — 신규.
- 기존 테스트 파일 수정 금지.

## Binding docs
`docs/SIM_SPEC.md` + `docs/SIM_SPEC_CAMPAIGN.md` + `docs/SIM_SPEC_HACKSLASH.md`.
스타일: 기존 CinderSim 패턴 (no UnityEngine/LINQ/foreach/RNG, 0 alloc/tick).

## 구현 명세 (스펙 §번호 참조)
1. **모드**: `GameMode { Arena, Prologue, Dungeon }`. HackConfig.Mode.
   - Prologue (§1): 아레나 수치, 스킬/대시/콤보 비활성(입력 무시), 웨이브 3개
     (4/6/8기), 보스 없음, 전멸 시 `StageCleared`(reason "prologue-clear").
   - Dungeon (§2–§7): 캠페인 규칙(해저드/보스/장비드롭) 상속 + 아래 전투킷.
2. **콤보** (§2.1): 3타 체인, 피해 58/58/87, 스윙 0.30/0.30/0.42 s,
   활성창 [0.10,0.22)/[0.10,0.22)/[0.14,0.30), 3타 넉백 120px/0.18s,
   링크 윈도 0.9 s. 던전 적 HP `86 + min(140,(wave-1)*11)`.
   `ComboFinisher` 이벤트(3타 명중 시).
3. **대시** (§2.2): Shift→`DashQueued`. 190px/0.22s, 전구간 무적(접촉·해저드),
   쿨 1.6s, 기름 8. 대시 중 이동입력 방향(무입력 시 facing). `DashUsed`.
4. **스킬4** (§2.3): 표 수치 그대로. 심은 `SimInput` 불리언만 신뢰한다 —
   **키→불리언 리맵은 View(InputAdapter) 소관, 심 관심사 아님**
   (아레나: Q=Nova/E=Ward 유지, 던전: Q=Bolt/E=Pulse/R=Nova(ash-nova)/
   F=Ward(aegis), 던전 재시작은 패널 버튼 전용).
   aegis = 실드 40 흡수, 8s 만료, 스냅샷에 Shield 노출.
   grave-pulse는 지속 필드(3s, 0.5s 틱) — 필드 상태는 심 내부, 뷰엔 이벤트만.
5. **원소** (§2.4): 스킬만 상성. 유리 +20% 불리 -15%.
6. **XP/레벨** (§2.5): 곡선 [30,55,85,120,160,205,255,310]+60/lv, 캡 12.
   레벨업 즉시 적용 + `LevelUp`.
7. **정예/추출** (§3): spawnOrdinal%7==0(웨이브당 1), HP×3 접촉×1.5 스케일×1.35.
   시체 10s, 반경 90px 정지 2.0s 채널(피격 리셋), 웨이브당 1회.
   `EliteDown`, `ExtractionComplete`. 스냅샷에 ExtractionProgress/Target 노출.
8. **동료** (§4): HackConfig.CompanionId != null이면 활성. 80px 추종,
   1.1s 주기 200px 최근접에 플레이어피해×0.6. 피격 불가. 스냅샷에
   CompanionX/Y/Attacking.
9. **스탯/장비** (§5,§6): HackConfig.MetaStats(attack/vitality/swiftness 0-10)
   + EquipTiers → 런 시작 파생 스탯 (공격 ×(1+0.03a)×(1+0.06w)×레벨보정,
   HP 100+8v+8c, 이속 218×(1+0.02s), 재생 7×(1+0.08l)).
10. **보스 페이즈2** (§7): HP≤50% 1회 — 이속+25%, 접촉×1.25, `BossPhase2`.
    Monarch면 호위 3기 소환(스폰 큐에 추가). 스냅샷 BossHp/BossMaxHp/BossPhase.
11. **결정론** (§13): RNG 금지.

## Tests (HackSimTests.cs — 최소 12)
- 아레나/캠페인 회귀: 기본·캠페인 생성자 Digest가 기존과 동일 (기존 30테스트
  통과가 곧 게이트 — 여기선 smoke 1개면 충분).
- 프롤로그: 3웨이브 전멸 → StageCleared("prologue-clear"); 스킬입력 무시 확인.
- 콤보: 타이밍 체인/리셋, 3타 넉백 발생, ComboFinisher.
- 대시: 무적(접촉 통과), 쿨/기름 소모, 거리.
- 스킬: bolt 최근접 타겟+스플래시, pulse 필드 틱 피해, nova 반경/넉백,
  aegis 흡수 40 후 소진.
- 원소: ember 스킬 vs frost 적 +20%, vs ember 적 0%(중립 아님 주의 — 사이클 확인).
- XP: 곡선 경계, 레벨업 스탯 적용.
- 정예: 7번째 스폰 정예, 추출 채널 2s(중간 피격 리셋), 보상 분기.
- 동료: 주기 공격 피해, 오프셋 추종.
- 보스 페이즈2: 50% 트리거 1회, Monarch 호위 3기.
- 결정론: 같은 HackConfig+입력 2회 → Digest 동일.

## Slice discipline (필수 — 절반 상태 금지)
3슬라이스로 구현하고, 각 슬라이스 완료 시점마다 기존 30테스트+신규 테스트를
dotnet test로 돌려 초록 확인 후 다음 슬라이스로 진행한다:
- S1: HackTypes + 모드 스캐폴딩 + 프롤로그 (+회귀 확인)
- S2: 콤보/대시/스킬4/원소/XP (+회귀 확인)
- S3: 정예/추출/동료/보스페이즈2 (+회귀 확인)
어느 슬라이스에서 막히면 그 지점까지의 초록 상태를 리포트에 명시하고 멈춘다
(다음 슬라이스를 반쯤 얹은 채 종료 금지).

## Verification
`csc -nologo -t:library -langversion:9.0 Assets/Scripts/Sim/*.cs` 컴파일 확인 후
/tmp 임시 프로젝트 dotnet test로 신규+기존 테스트 전부 실행, 결과 보고.

## Reporting
`_workspace/current/engineering/gjc-hackslash-lane-report.md`.
git/포매터 금지.
