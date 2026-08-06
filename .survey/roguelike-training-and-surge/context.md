# Context: Cinder Court — 로그라이크의 훈련 모드와 돌발(surge) 개입 지점

Bounded question: 로그라이크는 (a) 반복 가치를 갖는 훈련/연습 모드를 어떻게 만들고,
(b) 메타 강화가 **돌발 상황(위기·기회의 순간)**에 개입하게 하려면 어떤 형태를 쓰는가?
무RNG 결정론 게임에 이식 가능한 것은 무엇인가?

표본 13타이틀: Hades(HAD), Hades II(H2), Dead Cells(DC), Enter the Gungeon(ETG),
Warframe(WF), Deep Rock Galactic(DRG), Slay the Spire(StS), Vampire Survivors(VS),
Halls of Torment(HoT), Hollow Knight(HK), Risk of Rain 2(RoR2), Returnal(RTN),
Binding of Isaac(BoI). 훈련 축은 11타이틀에 직접 증거가 있고 RTN·BoI는 조사하지
않아 `?`로 표기한다(분모에 넣지 않는다).

증거 등급: 타이틀별 기제는 대부분 `indexed snippet`(검색 계층이 발췌만 반환).
우리 쪽 수치는 `direct page retrieval`(코드·문서 직접 열람). 추론은 `[INFERENCE]`.

## Workflow Context

훈련과 돌발은 서로 다른 워크플로에 산다.

**훈련 워크플로 — "출정 전에 무엇을 확인하는가".** 장르는 이 자리를 두 방식으로
채운다. 하나는 **허브에 붙은 상시 샌드박스**(HAD의 Skelly, H2의 Skelemeus,
WF의 Simulacrum)로, 사망도 자원 소모도 없는 무위험 공간이다. 다른 하나는
**등급이 붙은 시련**(HK의 Hall of Gods 3난이도, DC의 Boss Rush 4시련)으로,
같은 상대를 난이도별로 다시 만나고 기록이 남는다. 전자는 "숫자를 재는 곳",
후자는 "숙련을 증명하는 곳"이다. 두 성격이 한 시설에 동시에 있는 사례는 표본
중 HK뿐이다(Hall of Gods가 선택형 연습장이면서 등급 기록 장소). [indexed snippet]

**돌발 워크플로 — "예상 못 한 순간에 무엇이 개입하는가".** 여기서 장르는
압도적으로 **시계**를 쓴다. VS는 스테이지를 분 단위 구간으로 쪼개 매 분 적 풀을
교체하고 30:00에 Reaper를 보낸다. RoR2는 런 타이머로 난이도 바를 계속 올린다.
HoT는 30분 런 구조 자체가 압박이다. 시계 다음으로 흔한 것은 **플레이어가 자발적으로
서명하는 계약**(RoR2 Shrine of Blood가 현재 체력의 50/75/93%를 골드로 바꾸고,
BoI Devil Deal이 하트 컨테이너를 공격 아이템으로 바꾼다)이다. [indexed snippet]

두 워크플로의 접점이 이 조사의 표적이다: **훈련장에서 배운 것이 돌발에서 쓰이는가.**
표본에서 이 고리가 닫힌 사례는 없다 — 연습장은 스탯을 재고(WF: "one-dimensional"
통계 — 순수 피해량·발사속도·상태이상 적용률), 돌발은 시계가 만든다. 연습장에서
돌발을 연습하는 타이틀은 확인되지 않았다. [indexed snippet]

## Affected Users

| Role | Responsibility | Skill Level |
|---|---|---|
| 신규 플레이어 | 기믹 6종을 처음 만나 살아남기 | 낮음 — 현재 훈련에 기믹 0종이라 실전이 첫 만남 |
| 복귀 플레이어 | 조작·패턴 기억 회복 후 출정 | 중 — 재훈련 버튼이 같은 3웨이브만 줘서 확인이 안 됨 |
| 숙련 플레이어 | 고정 시간표를 외운 뒤 더 어려운 목표 찾기 | 높음 — 외운 뒤 강화가 개입할 순간이 없음 |
| 빌드 실험자 | 각인 조합(5종×양면, 슬롯 2)의 실제 효과 확인 | 중~높음 — 실전 외에 시연 장소 없음 |
| designer (본 레인) | 훈련 성격·돌발 축 개념 수립 | — |
| pm 레인 | 훈련·돌발이 만드는 보상 지점 초안 | — 유물 헤드룸 런당 +0.3이 상한(PM 산술) |
| qa 레인 | 돌발의 공정성 수치화, 훈련의 검증 방법 | — |

## Current Workarounds

장르 플레이어들이 연습 시설의 부재를 메우는 방법. 부재가 흔하다는 것 자체가 증거다.

1. **솔로 미션을 시작하고 낙오시킨다(abort).** DRG는 연습장이 없어서 플레이어가
   낮은 위험도 솔로 미션을 열고 초반 적 몇 마리로 무기를 시험한 뒤 미션을
   중단한다. 개발자가 연습장을 거부한 이유가 명시적이다: Space Rig가 전투·피해
   계산·적 스폰을 지원하지 않는 구조이고, 로딩 스크린이 붙으면 "빠른 시험 공간"의
   목적이 사라지며, 무엇보다 **단순 표적실은 실제 미션에서의 빌드 성능을 반영하지
   못한다**. [indexed snippet]
2. **모드를 깐다.** StS는 공식 연습 모드가 없어 Workshop의 "Practice Mode"(덱·유물을
   구성해 임의의 적/엘리트/보스와 즉시 교전)와 "Challenge The Spire"(Boss Rush /
   Elite Rush)로 메운다. RoR2는 DebugToolkit으로 `next_stage moon2`(Mithrix 직행),
   `give_item`, lategame 상태 부여, `noclip`, `kill_all`을 쓴다. 단, 모드는 업적을
   끄고 Prismatic Trial에서는 **사용 자체가 시도를 무효화**한다. [indexed snippet]
3. **저장 후 종료(save-scum)로 턴을 되돌린다.** StS 플레이어는 전투 중
   Save and Quit으로 현재 턴 시작 지점으로 되돌려 보스 행동·피해 임계·최적 플레이를
   학습한다. 일부는 치팅으로 보지만 널리 쓰인다. [indexed snippet]
4. **영상으로 고정 배치를 외운다.** ETG의 Winchester 표적 미니게임은 **배치가
   스크립트**되어 있어서, 플레이어들이 특정 방 레이아웃 영상을 보고 표적을 전부
   맞히는 데 필요한 정확한 위치와 타이밍을 학습한다. 게임 밖 학습이 게임 안
   결정론을 이용하는 형태다. [indexed snippet]
5. **접근권을 화폐로 산다.** WF Simulacrum은 Cephalon Simaris 키오스크에서
   Simacrum Access Key를 **50,000 Simaris Standing**으로 구매해야 열린다. 연습장이
   보상을 주는 게 아니라 **연습장이 비용을 받는다**. [indexed snippet]
6. **유료 DLC로 받는다.** HoT의 Training Ground는 통상 Supporter Pack(유료 DLC)에
   포함되며 캠프에서 접근한다. [indexed snippet]

## Adjacent Problems

- **연습장의 대표성 문제.** WF Simulacrum 비판이 정확히 이 지점이다: 적 경로,
  맵 지형, 스쿼드 버프가 빠져 있어서 Simulacrum에서 잘 나온 무기가 빠른 실전에서는
  다르게 느껴진다. 또 소유하지 않은 장비는 시험할 수 없어 신규 플레이어에게는
  효용이 제한된다. DC 훈련실도 실제 런의 장비·난이도를 정확히 복제하지 않아
  피해량이 5BC 런과 다르게 느껴진다는 지적이 있다. [indexed snippet]
- **연습 대상의 사전 조우 요구.** DC 훈련실은 정규 런에서 **먼저 만난** 적·보스만
  선택 가능하다. HK Hall of Gods도 이미 조우한(또는 Pantheon에서 해금된) 보스만
  도전 가능하다. 즉 연습장은 신규 플레이어를 위한 것이 아니라 **재도전자를 위한
  것**이다. [indexed snippet]
- **돌발의 예측 가능성 논쟁.** 선행 서베이가 기록한 모순이 이번에도 유효하다:
  D4 아플릭션(플레이어 상대 매복)은 "anti-chill" 지속 불만을 받았고 HoT 고스트 벽
  (동일하게 플레이어를 벌하는)은 칭송받았다 — 차이는 예측 가능성이다
  (`.survey/dungeon-gimmick-trends/solutions.md` §Contradictions, 인용).
- **컴백 기제의 남용 위험.** 설계 문헌은 체력 임계 버프가 "항상 켜져 있으면"
  플레이어가 의도적으로 체력을 낮게 유지해 파워 스파이크를 유지한다고 경고한다 —
  그건 Last Stand가 아니라 Glass Cannon 빌드다. 대응책으로 **히스테리시스**
  (50% 미만에서 발동, 55~60% 초과에서만 해제)와 **방/교전당 1회 제한**을 제시한다.
  HAD Stubborn Defiance가 정확히 후자다(방당 1회, 30% 부활, 방 진입마다 갱신).
  [indexed snippet]
- **유물 경제 헤드룸.** PM 레인 산술(본 사이클 IRC): 총 싱크 180유물(장비 120 +
  각인 60), 관측 런당 수입 17.7 → 포화 10.2세션. 밴드 하한 10이라 **런당 평균
  +0.3유물이 한계**이고 반복 통화 지급은 산술적으로 불가. 이 서베이는 그 제약과
  장르 관례가 같은 방향임을 확인했다(§Key Insight).

## User Voices

아래는 **검색 계층이 종합한 진술의 요약**이며 축약 인용이다(verbatim 아님).
전부 `indexed snippet`.

- "Skelly는 불멸이고 죽여도 즉시 재생되므로, 해금한 아스펙트들의 기계적 차이를
  통제된 환경에서 시험할 수 있다 — 공격 속도, 범위, 특수 속성." — fandom.com /
  fextralife.com (HAD Skelly, 900 HP)
- "훈련장은 기초 전투 연습으로 제한된다. 보스를 스폰할 수도, 특정 보온을
  시뮬레이션할 수도, 커스텀 전투 시나리오(보스 러시 등)를 만들 수도 없다." —
  H2 커뮤니티 종합
- "실전 미션은 혼란스럽고 예측 불가능한데 Simulacrum은 안정적인 환경을 준다.
  미션 목표나 적 AI 행동이라는 변수 없이 순수 피해량·발사속도·상태이상 적용률
  같은 '1차원' 통계를 시험할 수 있다." — reddit r/Warframe
- "단순 표적실은 실제 미션에서 빌드가 어떻게 작동하는지 정확히 반영하지 못한다.
  완전한 조절식 시험 환경을 만드는 건 개발 자원을 크게 먹는다." —
  steamcommunity.com (DRG 개발자 입장 종합)
- "배치가 스크립트되어 있기 때문에, 많은 플레이어가 특정 방 레이아웃 영상을 보고
  모든 표적을 맞히는 데 필요한 정확한 위치와 타이밍을 학습한다." — reddit
  (ETG Winchester)
- "보스에서 막히면 이기려 하지 말고 패턴을 배우려는 목적으로 들어가라. 텔레그래프,
  오디오 큐, 특정 기술의 준비 동작을 관찰하는 데 집중하라." — H2 커뮤니티 조언
  (전용 보스 연습 공간이 없기 때문에 나오는 조언)
- "Lasting Consequences로 회복이 줄거나 사라졌을 때, Stubborn Defiance는 체력이
  낮으면 방에서 의도적으로 죽어 '회복'하는 신뢰할 만한 반복 수단이 된다." —
  HAD 고Heat 전략 종합 (돌발 기제가 자원 관리 도구로 전용되는 사례)
- "Godhome의 보상은 대체로 자랑거리, 로어, 그리고 게임의 다른 구간을 위해 보스를
  연습할 수 있다는 유용성에 집중되어 있다." — reddit r/HollowKnight
- "Extreme Measures 활성화 자체에는 고유 보상·화폐·업적이 없다. 그런데도 많은
  플레이어가 선호하는 이유는 보스전에 새로운 기술·페이즈·전용 대사가 들어가서,
  다른 Pact 옵션의 평평한 피해·체력 증가보다 더 재미있기 때문이다." —
  steamcommunity.com / thegamer.com (HAD Heat)
