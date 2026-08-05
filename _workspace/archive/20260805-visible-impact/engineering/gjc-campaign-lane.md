# LANE: Campaign Sim Extension (owner: gjc)

## Mission
`docs/SIM_SPEC_CAMPAIGN.md`를 구현한다. 수정 허용 파일:
- `Assets/Scripts/Sim/SimTypes.cs` — **오직 Amendment §SimTypes 증분 목록만**
  (SimEvents 4개 플래그, PickupKind.EquipShard). 기존 멤버 변경 금지.
- `Assets/Scripts/Sim/CampaignTypes.cs` — 신규.
- `Assets/Scripts/Sim/CinderSim.cs` — 캠페인 오버로드 추가. 기본 생성자 경로의
  기존 동작/수치는 절대 불변 (아레나 회귀 금지가 1순위 게이트).
- `Assets/Tests/EditMode/CampaignSimTests.cs` — 신규 테스트.
- `Assets/Tests/EditMode/CinderSimTests.cs` — 수정 금지 (기존 20개가 회귀 게이트).

## Binding docs
- `docs/SIM_SPEC.md` (기존 계약) + `docs/SIM_SPEC_CAMPAIGN.md` (증분).
- 구현 스타일: 기존 CinderSim.cs 패턴 준수 — UnityEngine/LINQ/foreach/RNG 금지,
  per-tick 할당 0.

## Test requirements (CampaignSimTests.cs)
1. 아레나 회귀: 기본 생성자 600틱 Digest가 캠페인 코드 추가 전후 동일해야 함
   (기존 CinderSimTests가 이미 커버 — 전부 계속 통과할 것).
2. 스테이지 클리어: cinder-span 6웨이브(5+보스), 보스 킬 → StageCleared 이벤트
   + Digest.Reason "stage-clear", 잔존 적 페이드.
3. 보스 웨이브 조성: W+1 웨이브에 IsBoss 1기 + 호위 min(8, 3+idx*2)기.
4. ember-vent: 사이클 경계 반경 안 8 피해, Ward 무효(grace 소모), 반경 밖 무해,
   텔레그래프 창 HazardState.telegraphing 노출.
5. obsidian-pillar: 플레이어가 기둥 중심으로 이동 시도 → 원 밖 유지
   (dist >= r + 26 - epsilon). 적도 동일 (r + 22).
6. relic-altar: 1.2 s 체류 → oil +18, AltarBlessing 이벤트, 6 s 쿨.
7. 장비: 보스킬 확정 드롭 슬롯 stageIndex%3, 랭크 캡 5;
   `enemyId%7==3` EquipShard 픽업 스폰, 회수 시 kills%3 슬롯 +1;
   런 시작 스탯 적용식 (58*(1+0.06r), 7*(1+0.08r), 100+8r) 검증.
8. 결정론: 같은 CampaignConfig+입력 → 같은 Digest.

## Verification
csc로 Sim 어셈블리 단독 컴파일 (기존 방식과 동일):
`csc -nologo -t:library -langversion:9.0 Assets/Scripts/Sim/*.cs`
/tmp 임시 프로젝트에서 dotnet test로 신규+기존 테스트 전부 실행, 결과 보고.

## Reporting
`_workspace/current/engineering/gjc-campaign-lane-report.md`:
해석 갈림 지점, 실행 명령, 테스트 결과. git/포매터 금지.
