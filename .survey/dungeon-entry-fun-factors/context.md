# Context: Cinder Court — 던전 입장·인런 재미 요소

조사 범위: 액션 로그라이트/핵앤슬래시 던전 게임 15종. 레인 A(맥락·플레이어
목소리) + 레인 C(출시 후 반응) 통합. 증거 라벨은 `direct page retrieval` /
`stable official substitution` / `indexed snippet` / `thin evidence`.

레인 A는 Steam 공개 리뷰 API에서 영어 리뷰 약 3,000건을 커서 페이지네이션으로
수집해 문장 단위로 6개 테마를 채굴했다. 인용된 39개 recommendationid는 전부
수집 코퍼스와 대조 검증했다. Reddit은 전 서브레딧 검색이 HTTP 403이라
인용에 쓰지 않았다.

## Workflow Context

플레이어의 세션은 **허브 → 문턱 → 런 → 죽음/클리어 → 복귀**의 반복이다.
잘 만들어진 타이틀은 이 네 지점 전부에 재미 비트를 놓는다. 우리는 문턱이
비어 있다.

| 비트 | 잘 된 사례의 실측 | Cinder Court 현황 |
|---|---|---|
| 문턱(threshold) | Hades는 **모든 방의 보상 아이콘을 입구 문에 표시**하고, 런당 최대 73개 방을 지난다 (`stable official substitution`) → 보상 형태의 선택이 대략 분당 1회 | 컷신 스프라이트 + 키커/타이틀 + 나레이션 1줄 + 말풍선. 결정 0개 |
| 첫 60초 | RoR2는 난이도 계수가 **초 단위로 증가**(Drizzle 50% / Monsoon 150% 배속) (`direct page retrieval`) · Dead Cells Malaise는 생존 적 비율에 따라 **초당 1.3점**으로 차오름(100% 잔존 시), 한 티어 약 42초 (`direct page retrieval`) | 웨이브가 자기 시간표대로 도착. 플레이 방식에 따라 움직이는 수치가 적 HP뿐 |
| 런 중 결정 | Hades 바이옴당 8–14회 · 서바이버류 10분당 20–30회 드래프트 (`indexed snippet`) | 레벨업 5–8회, 매번 같은 3개 |
| 복귀 | Hades는 귀환할 때마다 대사가 **바뀐다** — 반복이 장벽이 아니라 진행으로 읽힘 (`indexed snippet`) | 고정 |

**입장 분류표** — 15종 중 문턱이 명확한 실시간 결정인 곳 **7**, 부분적 **3**,
순수 전환 **4**. 교훈적 실패 사례는 Diablo IV다: 시길 선택은 진짜 수정자
결정인데 **다른 화면 다른 시점**에 내려지므로 문 앞은 여전히 통로로 읽힌다.
(`indexed snippet`)

**의식(ceremony) 비용은 산술적으로 누적된다.** 고정 10초 도입부는 40분 런의
0.4%지만 3분 런의 5.5%이고, 죽을 때마다 영원히 지불된다. 게다가 그 위치가
**죽음의 교훈과 재시도 사이**라 학습 루프를 직접 갉는다. (`indexed snippet`)

## Affected Users

| 플레이어 타입 | 던전 런에서 원하는 것 | 그것을 죽이는 것 |
|---|---|---|
| 첫 플레이어 | 이 게임이 무엇에 대한 것인지 60초 안에 아는 것 | 스킵 불가 도입부, 아무것도 고르지 않고 시작되는 첫 런. 장르의 답(계약·서약·승천)은 전부 **클리어 이후 해금**이라 애초에 못 본다 |
| 복귀·숙련 플레이어 | 배운 것을 증명할 자리 | 숙달을 측정하는 표면이 없음. 점수도 랭킹도 등급도 없으면 "완벽하게 돈 런"과 "간신히 돈 런"이 같은 화면으로 끝남 |
| 빌드 크래프터 | 압박 속에서 저작하는 빌드 | 규칙 변경이 전부 런 밖에 잠겨 있고 런 안에는 스칼라만 있음. Hades Chaos는 **N개 조우 동안 지속되는 디버프**를 대가로 이후의 힘을 판다 (`direct page retrieval`) — 저작은 대가가 있는 선택이다 |
| 컴플리셔니스트 | 체크할 수 있는 목록 | 스테이지 클리어 외에 세분화된 달성 축이 없음 |
| 모바일·짧은 세션 | 10분 안에 닫히는 단위 | **입장 시점에 런 길이를 읽을 수 없음.** DRG는 미션 선택 터미널에 예상 길이를 커밋 전에 표시한다 (`direct page retrieval`) |
| 스트리머·관전자 | 보여줄 순간 | 공유 가능한 산출물 0. 결정론은 비교 가능성의 이상적 기반인데 리더보드가 없음 |
| 저에너지 세션 플레이어 | 생각 없이 10분 | 모바일 소탕(sweep)이 이 일을 더 잘한다 — 숙달된 스테이지를 전체 수동 클리어시키면 이 플레이어를 잃는다 (`indexed snippet`) |
| 공정성 민감 플레이어 | 내 실수로 지는 것 | 해당 없음 — 무RNG는 이 축에서 이미 최상급이다 |

## Current Workarounds

개발사가 스테이지가 같아 보이기 시작할 때 덧붙이는 것들, 그리고 플레이어가
스스로 하는 것들.

1. **수정자 시스템을 얹는다.** Hades Pact of Punishment는 16개 조건 / 최대
   63 Heat (`direct page retrieval`). Slay the Spire Ascension 20단
   누적 (`direct page retrieval`). Dead Cells Boss Stem Cell 5단
   (`direct page retrieval`).
2. **재도전 사다리에 보상을 건다.** Hades Bounty는 무기별로 Heat 요구치를
   따로 추적한다 (`direct page retrieval`).
3. **런 저작 층을 신설한다.** Vampire Survivors는 스테이지만 늘린 게 아니라
   **Arcana**(런 시작 시 임의 해금분 1장 선택 + 11:00/21:00 보스 상자에서
   2장)를 무료 패치로 넣었다 (`direct page retrieval`).
4. **문턱 자체를 구매 결정으로 바꾼다.** Halls of Torment 1.0의 The Vault는
   코인을 내고 **긍정·부정 수정자를 런에 직접 붙이고**, 최종 보스를 30분
   타이머가 아니라 **플레이어가 수동으로 소환**한다 (`indexed snippet`).
5. **무작위 제거 토글을 1급 콘텐츠로 출시한다.** RoR2 Artifact of Command:
   "Choose your items." (`direct page retrieval`)
6. **플레이어가 직접 핸디캡을 만든다.** StS 베테랑이 "10–12가지 자체 제약"을
   건다 (`indexed snippet`). Nuzlocke가 같은 계열이다.
7. **수지가 안 맞으면 런을 최적화해 버린다.** Hades 자살 리셋 — 보상이
   비용보다 작다고 계산되면 플레이어는 콘텐츠를 건너뛴다 (`indexed snippet`).

## Adjacent Problems

"던전이 지루하다"와 함께 다니는 문제들. 전부 이 저장소에 해당한다.

- **보상 가독성.** 목표 문자열이 있는데 판정이 없으면 보상이 무엇에 대한
  것인지 알 수 없다. `witness-well`의 목표는 "우물의 증언이 꺼지기 전 기둥
  사이 전선을 유지하라"인데 심에는 증언도 꺼짐도 판정도 없다.
- **난이도 평탄함.** 웨이브 5의 최적 전략이 웨이브 50과 같으면 그것은
  난이도가 아니라 길이다 (`indexed snippet`).
- **아레나에서 움직일 이유.** 기믹 6종 중 플레이어에게 이득을 주는 것은
  제단 1종뿐이다. 나머지는 전부 회피 대상이라 이동은 뒷걸음질뿐이다.
- **텔레그래프 피로.** 밀도 군비 경쟁 — 적이 늘면 지시자를 더 그리고, 그
  지시자가 곧 잡음이 된다 (`indexed snippet`). 우리 ash-march·cinder-sluice는
  예고 점유율이 이미 71–75%다.
- **메타 진행이 실력을 앞지름.** 스탯이 학습을 대체하면 학습이 멈춘다.
- **위험 결정의 부재.** 문턱에서 아무것도 걸지 않으면 첫 60초에 긴장이 없다.
- **보상 스팸.** 양이 의미와 분리되면 드롭의 도파민이 붕괴한다 — D4는
  Season 4 "Loot Reborn" 이후에도 이 불만이 남았다 (`indexed snippet`).

## User Voices

전부 Steam 리뷰 원문(수집 코퍼스 대조 완료) 또는 명시된 2차 출처.

- "-Hades style boon system does not really make sense if player doesn't have a choice in what will be offered" — Death Must Die, Steam review id 153537233 (`direct page retrieval`). **선택의 형태를 한 UI에 선택이 없으면 선택창이 아예 없는 것보다 나쁘게 읽힌다.**
- "all of the runs with the same squad just feel identical" — Into the Breach, Steam review id 40512406 (`direct page retrieval`). 절차적 맵을 가진 게임에도 이 불만이 나온다 — **변수는 콘텐츠 양이 아니라 결정 밀도다.**
- Halls of Torment는 첫 능력을 **플레이어 선택제로 바꿔서** 나쁜 오프닝을 고쳤다 — Steam review id 143149560 (`direct page retrieval`). **무작위를 빼서 선택을 준 사례**이고, 방향이 우리와 같다.
- "We know it is bad. We know it is not fun" — Adam Fletcher, Diablo IV Campfire Chat 2023-07-21 (`indexed snippet`). 직후 패치 1.1.1이 반감 큰 어픽스 3종(Resource Burn / Backstabbers / Empowered Elites)을 삭제하고 몹 밀도를 올렸다 (`indexed snippet`).
- Returnal은 반복 사망 시 **의무적 분위기 연출**이 지겨워진다는 비판을 받았다 — 서사 목표와 "빨리 다시 쏘고 싶다" 사이의 불협화음 (`indexed snippet`). **우리 입장 컷신이 정확히 이 자리에 있다.**
- Archnemesis는 리그 중 **플레이어가 조합할 때는 호평**받았고, 3.18에서 코어로 **강제되자 지속적 반발**을 샀다 — 동일 콘텐츠, 다른 저작권 (`indexed snippet`).
- D4 후반부 여론: Nightmare Dungeon이 **다양성·개성으로 가장 호평**받고 The Pit은 "soulless", D3 균열 재탕이라 불린다 (`indexed snippet`). **단일 반복 아레나가, 보상 효율이 더 높은데도, 다양한 레이아웃에 졌다.**
- Hades Pom of Power(순수 스탯 상승)는 "새 빌드 정의 부운의 도파민을 거의 주지 못한다"는 것이 커뮤니티 중론이다 (`indexed snippet`). **우리 레벨업 3종이 정확히 Pom이다.**
- PoE 소형 패시브 노드는 "선택의 환상", 의무적 이동 필러라고 불린다 (`indexed snippet`).
- Sid Meier: 명백한 최선이 있으면 그것은 지배 전략이고, 정답이 있는 결정은 "결정이 아니라 작업 수행"이다. 흥미로운 결정은 **상황 의존적 가치**를 요구한다 (`indexed snippet`).
- RoR2 Simulacrum 후반 웨이브는 적이 다 스폰되기까지 **15–45분**이 걸린다는 보고 — 위협이 아니라 인내 시험 (`indexed snippet`).
- Dead Cells Malaise 개편은 **양극화**됐다: 수동적 대기를 없앴다는 옹호와, 신중한 숙련 플레이를 처벌한다는 비판 (`indexed snippet`).
- Enter the Gungeon 플레이어는 **암기를 의도된 숙달 경로로** 서술한다 (`indexed snippet`). 예측 가능성 자체를 비난하는 목소리는 코퍼스에 없었다.
- Devil Daggers는 모든 스폰이 매 판 동일한데도 성공했다 — 개발자는 모든 죽음을 **운이 아니라 실행 실패**로 프레이밍한다 (`indexed snippet`).
- Isaac·Dead Cells는 고정 콘텐츠(데일리)를 **공정성 보증으로 상품화**한다 (`indexed snippet`).
- Wordle의 흡인력은 **모두가 같은 인스턴스를 푼다**는 희소성과 비교 가능성에 귀속된다 (`indexed snippet`).
- "the daily chore" — 모바일 소탕이 해결한 문제. 숙달된 콘텐츠를 전체 수동 클리어시키는 게임은 이 상품과 직접 경쟁해서 진다 (`indexed snippet`).
- D4 베타·출시 목표들("돌을 제단으로 나르기", "정예에게서 Animus 수집")은 **잡일**로 읽혔고, 적이 리스폰하지 않는 빈 구역 되돌아가기는 "walking simulator"라 불렸다 (`indexed snippet`). **목표를 붙일 때 코어 루프를 끊으면 안 된다는 경고.**

### 코퍼스가 말하지 않은 것

**예측 가능하다는 이유로 게임을 비난하는 인용은 하나도 없었다.** 결정할 것이
없다는 이유로 비난하는 인용은 많았다. 증거는 주사위가 아니라 **결정 표면**을
가리킨다 — 따라서 "무RNG를 유지하는 권고"는 플레이어 정서와 싸우지 않는다.

### 한계

- 런 길이·방당 시간 수치는 커뮤니티 집계뿐이라 `indexed snippet`이고 점값이 아니라 밴드로만 적었다.
- Steam 리뷰는 글을 쓸 만큼 관여한 플레이어로 편향된다 — 조용히 이탈한 층은 구조적으로 과소 대표된다.
- Reddit 403으로 포럼 뉘앙스가 빠졌다. 모든 인용이 Steam 출처다.
