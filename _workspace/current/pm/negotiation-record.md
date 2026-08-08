# Negotiation Record — cycle 2 round 1 (designer ↔ PM)

방식: 하니스 협상 규칙 — 밸런스 수치를 건드리는 모든 경제 포인트는 서명 엔트리.
1교환 내 미합의 → director 수치 중재.

```yaml
entry: 1
revenue_point: 신규 스테이지 첫클리어 유물 보너스
balance_number: CampaignStore.Relics 지급(뷰 레인, 심 무관)
designer_bound: "0 — 인런 드롭·+30 중복추출로 충분, 경제 인플레 경계"
pm_bound: "+8/+10/+12 (sluice/bastion/march) — T4(11)·T5(16) 도달 가시화가 신규 콘텐츠 재방문 동기"
agreed: "+6/+8/+10, 첫클리어 한정. 근거: 합산 +24 = T4 비용 2.2배 억제, 기존 스테이지 재플레이 가치 보존. 사이클 내 QA 실측(kiter 평균 유물/런)으로 재검증 — 평균 런 수입의 25% 초과 시 하향"
signed: [game-designer, game-pm]
```

```yaml
entry: 2
revenue_point: ash-march 첫클리어 동료 보상
balance_number: CampaignStore.Roster (기존 -echo 변형)
designer_bound: "불필요 — 로스터 6종 충분, 신규 메시 금지 계약"
pm_bound: "필요 — 최종 신규 스테이지에 영구 보상 없으면 체인 완주 동기 약함"
agreed: "scout-echo 지급(기존 추출 변형 재사용, 신규 자산 0). cinder-sluice/ember-bastion은 동료 없음(전 스테이지 지급 시 추출 시스템 가치 붕괴)"
signed: [game-designer, game-pm]
```

```yaml
entry: 3
revenue_point: 파일런 오라 강도 (세션 완주율)
balance_number: PylonAuraDamageTakenMult 0.60
designer_bound: "0.60 (−40%) — 선파괴 전술이 '정답'으로 읽히는 최소 강도"
pm_bound: "0.70 (−30%) — 저랭크(2/1/3) 이탈 리스크, 세션 완주율 우선"
agreed: "0.60 유지 + 완화 장치는 강도가 아닌 배치로: 파일런 2기 위상 분리(동시 교전 강제 없음), G2 실측에서 2/1/3 contested 판정 실패 시 0.65 재협상"
signed: [game-designer, game-pm]
```

```yaml
entry: 4
revenue_point: 스탯 포인트 지급률
balance_number: 클리어 +2 / 첫보스 +1 (기존 계약)
designer_bound: "변경 없음"
pm_bound: "변경 없음 — 신규 3스테이지로 총 공급 +9pt, 캡(각 10) 대비 여유 적정"
agreed: "무변경"
signed: [game-designer, game-pm]
```

## G5 밴드 매핑 (reward-bands.md)
- comeback 상당: 첫클리어 보너스는 일회성 — 반복 수급 대비 ≤25% 계약(entry 1).
- steady 상당: 신규 스테이지 포함 T5 도달 12–18세션 밴드(10–20 하니스 밴드 내).
- fairness 상당: 결제 부재 — paid/free delta 항목 N/A(웨이버, director 승인).

```yaml
entry: 5
revenue_point: 심판 서약 (Verdict Pact) 재도전 유물 배수
balance_number: 서약 클리어 유물 ×2 (뷰 지급)
designer_bound: "×2 — 강화 고정 배치의 정당 보상, 첫클리어 보너스 비중복(클리어 후 해금이라 자연 배제), 인플레 제한적"
pm_bound: "T5 도달 밴드(10-20세션) 하한 침식 우려 — 해금 조건이 '해당 스테이지 클리어 후'라 초반 가속 없음 확인 후 승인"
agreed: "서약 클리어 유물 ×2, 첫클리어 보너스 비중복, 세이브 미기록(출정별 옵트인). QA 실측: 서약 런 평균 유물 ≤ 일반 런 ×2.2 검증 조건부"
signed: [game-designer, game-pm]
```

### entry 5 — QA 조건부 항목 해소 (2026-08-06, v1.4)

`agreed`의 조건 "서약 런 평균 유물 ≤ 일반 런 ×2.2"를 EditMode 라우트
테스트로 실측 충족. 같은 스테이지(cinder-span)를 같은 파일럿이 두 번
클리어 — 한 번은 일반, 한 번은 실제 로비 서약 토글을 눌러 출정:

| 런 | 지급 유물 | 비율 |
|---|---|---|
| 일반 클리어 | 3 | 1.00× |
| 서약 클리어 | 6 | **2.00×** (밴드 ≤2.2 충족) |

[OBSERVED, `qa/../engineering/unity-logs/test-results-093217.xml` —
`ArmedPactSortie_DoublesOnlyTheInRunRelicPayout` 출력 블록]
함께 기계 고정된 계약: 배수는 **인런 유물에만** 적용(첫클리어 보너스 라인
비배수·독립), 반복 클리어 포인트 +2(첫클리어 +3 아님), 진행도 불변.
비율은 게이트 실행마다 재측정되므로 밴드 이탈 시 테스트가 실패한다.

```yaml
entry: 6
revenue_point: 각인(Sigil) 해금 비용 — 유물 싱크
balance_number: LobbyView.SigilCost = 12 유물/각인 × 5종 = 60
designer_bound: "12 — 장비 T5 합계 40의 1.5배. 각인은 장비와 달리 5종 중
  2개만 장착하므로 전부 사는 것이 필수가 아니고, 비싸도 진행을 막지 않는다"
pm_bound: "미제출 — 협상 미개시"
agreed: "**미서명**. 구현은 상수 1개(LobbyView.SigilCost)로 두었으므로
  협상 결과를 코드 한 줄로 반영할 수 있다. 근거 자료: T5 도달 후 유물
  사용처 소멸(.survey/meta-upgrade-gimmick-interaction §Adjacent Problems),
  v1.3 서약 ×2가 수급을 늘려 이 문제를 키운 사실"
signed: []
```

### entry 6 — 협상 대기 항목 (2026-08-06, v1.5)

각인 자체의 **효과 수치**(HackSpec §13)는 협상 대상이 아니다 — 심 수치이고
`sigil-spec.md`의 설계 규칙 3개(면역 금지·무작위 금지·사이드그레이드)로
제약되며 10면 전부 EditMode가 고정한다. 협상이 필요한 건 **가격 하나**다.

PM이 확인할 것: 각인 60유물이 장비 40유물과 경쟁하면서 T5 도달 밴드
(10-20세션)를 밀어내지 않는가. 밀어낸다면 12를 낮추거나, 각인을 장비 T5
이후 해금으로 게이팅하는 안이 대안이다.

### entry 7~9 — 훈련장·돌발·각인 서지 (2026-08-06, v1.6 설계, round 1)

근거: `design/training-and-surge-spec.md` · `.survey/roguelike-training-and-surge/`
· `qa/benchmark-notes.md` §훈련·돌발 · `pm/revenue-map.md` §run-id 20260806

```yaml
entry: 7
revenue_point: 훈련장 완주 보상
balance_number: 5기믹 전부 판결 등급 -> 일회성 +2유물, 반복 지급 0
designer_bound: "등급 사다리에 종착점이 있어야 재방문이 성립한다"
pm_bound: "일회성 <=2유물 등가까지. 반복 통화 금지 (revenue-map 후보표 판정)"
agreed: "일회성 +2유물 1회. 훈련장은 적을 스폰하지 않으므로 처치 드롭
  경로도 0 - PM이 지적한 '훈련 심이 이미 유물을 굴린다' 문제의 구조적 해결"
signed: [game-designer, game-pm]
```

```yaml
entry: 8
revenue_point: 각인 위기 조항 - 사실상 면역의 허용 범위
balance_number: 집행인 벽 틱 면제 6초 -> **3초 x 틱 절반(10->5)로 강등**
designer_bound: "위기 탈출 도구가 없으면 서지가 상태 표시로 끝난다"
pm_bound: "G5 comeback 밴드 - 단일 발동 즉시역전 <=30% maxHP, 캡/쿨다운 기록"
agreed: "director 산술 중재. 상세는 아래 표"
signed: [game-designer, game-pm, game-production-director]
```

**director 중재 (수치가 판정했다)** — 벽 DoT 16.67 dmg/s 기준:

| 조항 | 회피량 | 기본HP 대비 | 판정 |
|---|---|---|---|
| 역류인 위기(밀기 0) | 0 (해류 직접피해 0) | 0% | PASS - 밴드 무관 |
| **집행인 위기(벽 면제 6초)** | **100.0** | **100%** | **FAIL** |
| 집행인 강등안(3초 x 틱 절반) | 25.0 | 25% (만개 11%) | PASS |
| 증언인 위기(제단 즉시완료) | 0 (기름 획득, 피해 회피 아님) | 0% | PASS - 단 기름->스킬->생존 간접 경로 QA 측정 |
| 판결인/점화인 기세 조항 | 0 (적 대상) | 0% | PASS |

추가 캡 3개 (강등안과 함께 계약):
1. **런당 위기 발동 총 2회** (웨이브당 1회 -> 9웨이브 스테이지에서 최대 10회
   발동은 캡 없는 역전 공급). 히스테리시스 50%는 유지.
2. **위기 조항 중첩 금지** - 슬롯 2개에 위기 각인 2종을 끼워도 한 번에 하나만
   발동(슬롯0 우선). 중첩 시 회피량이 밴드를 재돌파한다.
3. **지속 3초 · 예고 없음** - 위기는 이미 "체력 35% 미만"이라는 플레이어
   가시 상태가 예고를 대신한다(HUD 체력바가 곧 텔레그래프).

```yaml
entry: 9
revenue_point: 돌발(Surge) 자체의 보상
balance_number: 0 - 상태 변화만, 유물/포인트/진행도 미접촉
designer_bound: "돌발은 리듬 장치이지 보상 장치가 아니다"
pm_bound: "comeback 밴드 미접촉이면 이견 없음"
agreed: "무보상 확정. 돌발은 세이브에 기록되지 않는다(런 스코프)"
signed: [game-designer, game-pm]
```

### entry 6 갱신 — 각인 가격, 여전히 미서명이지만 좁혀졌다 (2026-08-06, v1.6)

**서명 못 한다. 다만 남은 불확실성이 하나로 줄었다.**

상수 모델(A)을 만개 앵커 r=17.67로 재계산했고 **PM의 독립 계산과 일치**했다:

| c | 순 필요액 R(c)=96+5c | N=R/17.67 | 하한 10 |
|---|---|---|---|
| 12 | 156 | **8.83** | **위반** |
| 14 | 166 | 9.39 | 위반 |
| **17** | **181** | **10.24** | **통과 — 최소 정수** |

c=12가 하한을 지키려면 `r ≤ 15.60 유물/런`이어야 한다. 실측 만개 17.67은
초과, 실측 무성장 7.00은 통과 — **판정이 랭크에 따라 갈린다.** 그래서 PM이
상수 모델을 버리고 부트스트랩 곡선을 주모델로 채택한 것이 옳다.

**내가 못 한 것과 왜 못 했는지.** 곡선의 앵커는 **클리어한 런**의 수입이어야
하는데, 내 프로브 파일럿은 6스테이지 × 6랭크 전부에서 **0/6 클리어**다
(최고 19킬, 4웨이브 사망). 부분런 수입 3.5~4.5는 실패한 런의 숫자라
앵커로 인용하면 안 된다 — 인용하면 모든 c가 통과로 나와서 판정이 무의미해진다.

```yaml
entry: 6-update
revenue_point: 각인 해금 비용 c
balance_number: "상수 모델 최소 정수 c=17 (독립 재현 완료). c=12는 만개 앵커에서 8.83런 — 하한 10 위반"
designer_bound: "12는 장비 T5(16)보다 낮아야 한다는 직관에서 나온 값 — 근거 없음을 인정"
pm_bound: "부트스트랩 곡선 임계 0.714 스텝/런. 클리어 런 수입 곡선 없이는 판정 불가"
agreed: "미서명 유지. 단 필요한 측정이 '드롭율' -> '랭크별 클리어 런 수입 곡선'으로 특정됐다"
blocked_on: "사람 플레이 또는 클리어 가능한 파일럿. 봇 0/6 클리어로 이번 사이클 불가"
signed: []
```

---

## v1.7 진행 네비게이션 (run-id 20260807-progression-navigation)

근거: `design/progression-navigation-spec.md`, `pm/revenue-map.md` §v1.7,
`qa/test-plan.md` §v1.7 T-A4/T-A9, `design/trend-survey/progression-navigation.md`.

### entry 10 — 탭 배지가 어느 탭을 가리키는가

이 사이클에서 **경제에 실제로 닿는 유일한 지점**이다. 배지는 소비 순서를 밀고,
소비 순서는 스텝 효율을 바꾼다.

PM 실측: 유물 싱크는 정확히 2개(장비 랭크업 2/4/7/11/16, 각인 해금 12).
누적 장비지출 **72유물**(순 필요액 96의 75.0%)까지 장비가 각인보다 스텝 효율
**무조건 우위**. 각인 5종은 동일가 12인데 기믹 도달률은 점화인 9/9(100%) ↔
집행인 1/9(11.1%) — **9배 스프레드**. 오지목 시 12유물 = 0.679런 = 브래킷의
16.8%가 유휴자본(해상도 기준선 2유물의 6배).

designer 측 근거: N5 형태 빈도 1/6이라 G8 후보이긴 하나, 단독 채택은 DD2
Altar of Hope 실패 경로("진행 없음" 불만에 화면으로 답했다가 "그라인드페스트")를
그대로 밟는다.

```yaml
entry: 10
revenue_point: SANCTUM 탭 배지
balance_number: "EquipCosts {2,4,7,11,16} · SigilCost 12 · badge_misdirect_relics max 2"
designer_bound: "배지는 '사라'가 아니라 '다음 재판에 유효하다'만 말한다"
pm_bound: "유물 탭 동시 점등 금지. 오도 |지목비용 − 최저가능비용| <= 2유물"
agreed: |
  유물 탭(장비/각인)은 최저비용 항목이 속한 쪽 하나만 점등.
  Points 탭(성장)과 무료 탭(군단)은 통화가 달라 독립 점등.
  규칙은 런타임에 EquipCosts[tier]와 SigilCost를 읽어 비교한다 —
  가격을 상수로 박지 않는다.
  실측: T0/T0/T0 + 유물 12에서 현행 의미론 오도 10 (밴드 5배 위반) -> 규칙 적용 후 0.
signed: [game-designer, game-pm]
```

### entry 11 — 각인 컨텍스트 라벨이 구매 유도가 아님을 고정

```yaml
entry: 11
revenue_point: 각인 행 컨텍스트 라벨 (N11)
balance_number: "기믹 도달률 9/9 ~ 1/9. 가격은 이 차이를 표현하지 못한다"
designer_bound: "'다음 재판 대상' / '휴면' / '대기' 3종. 가격·구매 문구 금지"
pm_bound: "라벨이 구매 CTA로 읽히면 DD2 경로. 문구에 '구매'·'사라'·가격 미포함이 조건"
agreed: |
  라벨은 다음 목표 스테이지의 실효 해저드 표(HazardOverride ?? 앵커)에
  해당 각인의 바인딩 기믹이 있는지만 말한다. 구매 가능 여부는 배지 소관이고
  라벨과 분리한다. 두 신호가 한 행에서 겹쳐도 의미가 섞이지 않는다.
signed: [game-designer, game-pm]
```

### entry 12 — 그룹 헤더 4개의 터치 래칫 부채 등재

```yaml
entry: 12
revenue_point: 없음 (접근성 부채)
balance_number: "신규 헤더 4개 = 179.6 x 21.5 CSS px @390x844 portrait"
designer_bound: |
  접지 않는 라벨 헤더면 신규 컨트롤 0개지만 콘텐츠가 1058 -> 1258u로 늘어
  가시율이 41.0% -> 34.5%로 악화된다. N1이 요구하는 것은 헤더가 아니라
  위치를 알 수 있는 상태다. 접는 헤더는 래칫 비용을 내고 축을 산다.
pm_bound: "새 위반 '등급'이 아닌지가 조건. 기존 최악(25.4폭 / 13.7높이)을 갱신하면 거부"
agreed: |
  등재 승인. 높이 21.5는 탭 4개와 동일 등급이고, 폭 179.6은 로비 최광이라
  최악 갱신이 아니다. LobbyLayoutTests 동결표에 4행 추가.
  기존 44px 부채 해소는 여전히 이월 안건 — 이 등재가 그것을 대체하지 않는다.
signed: [game-designer, game-pm]
```

### entry 6 재확인 — 이 사이클은 영향 없음

PM 판정: 배지·지목 규칙이 `Relics >= 비용` 부울 비교이고 "최저비용 우선"으로
쓰므로 런타임에 가격을 읽어 스스로 답을 낸다. c가 12든 17이든 규칙이 동일하게
동작한다. **단 조건 하나** — 배지 규칙에 `72`나 정적 우선순위를 상수로 박으면
판정이 뒤집힌다(c≥16이면 임계 자체가 소멸). 박지 않는 것이 entry 10의 `agreed`
조항이다.

유물 가중 진척 게이지는 **영향 있음**(분모 R(c)=96+5c가 c=12→17에서 표시
완주율 8.5%p 이동)이었으나, 이 사이클의 게이지는 `정화 n/9` **이산 스테이지
카운트**라 분모에 가격이 들어가지 않는다 → 영향 없음으로 확정.

---

## v1.8 인게임 안내 (run-id 20260807-ingame-guidance, AMENDMENT #9)

근거: `.omc/specs/deep-interview-ingame-guidance.md` (deep-interview 5R, 17%),
`.survey/ingame-guidance/` (19타이틀×12축), `qa/benchmark-notes.md` §v1.8,
`qa/test-plan.md` §v1.8.

### entry 13 — 정지 8회: 대역 밖임을 인정하고 통제권으로 상쇄한다

이 사이클에서 **조사가 확정 사항에 정면으로 이의를 제기한 유일한 축**이다.

서베이 실측: 정지를 쓰는 타이틀 19 중 **5건(26%)**, 정지 총 횟수 **중앙값 0 ·
확인 최댓값 1**. 그중 4건이 정지 1회를 앞에 몰아넣고 끝낸다. **분산 배치 선례는
Darkest Dungeon 2 단 하나이며, 그 하나가 표본 온보딩 혹평 최댓값이다.**

그러나 서베이의 결정적 발견은 횟수가 아니었다:

> Returnal은 **무정지인데 과잉으로 혹평**받았고 Into the Breach는 **정지를 쓰는데
> 불만이 0**이다. 차이는 정지 여부가 아니라 **거절 가능성**이다.

```yaml
entry: 13
revenue_point: 없음 (온보딩 마찰)
balance_number: "정지 8회 · 스테이지당 <=4 · 카드 1장당 <=33어절"
designer_bound: |
  8을 유지한다. 정지 대상 8종(기믹 6 + 승패 2)은 서베이가 G6 0/7로 짚은
  장르 최대 공백이고, 우리 기믹은 규칙이 외형으로 환원되지 않는다.
pm_bound: |
  대역 밖 채택은 완충 없이는 거부. Returnal 선례(무정지인데 과잉 혹평)가
  분량이 1차 변수가 아님을 보이므로, 통제권을 완충으로 인정한다.
agreed: |
  정지 8회 유지 + 세 가지 완충:
  (1) 거절 가능 — 아무 키/탭으로 즉시 닫힘. ItB가 불만 0인 이유를 직접 공략
  (2) 길이 상한 — 카드 1장 <=33어절 (ItB 전체 120초 / 8건 / 3.3어절초 유도)
  (3) 재발 차단 — 23비트 EncounterRecord로 항목 단위 자동 1회
  **"장르 관례를 따랐다"고 서술하지 않는다. 의도적 이탈로 기록한다.**
signed: [game-designer, game-pm]
```

### entry 14 — 이탈 전량 몰수 유지 (등급 몰수 기각)

서베이 실측: 전량 몰수는 **2/16 (13%) 소수파**. Loop Hero는 등급 몰수
(모닥불 100% / 루프 중 60% / 사망 30%)로 이탈을 처벌이 아니라 전략으로 만든다.

```yaml
entry: 14
revenue_point: 런 중 이탈 정산
balance_number: "이탈 시 Relics 증가 0, ClearedMask 불변"
designer_bound: "등급 몰수가 장르 다수파이고 이탈을 전략으로 승격시킨다"
pm_bound: |
  등급 몰수는 시간축 익스플로잇을 연다. 우리 유물은 wave 진행에 따라 증가하고
  패배도 적립하므로(GameDirector.cs:614-624), 60% 등급이면 "위험 구간 직전
  이탈"이 최적 전략이 된다. Loop Hero는 런 길이가 플레이어 통제라 다르다.
agreed: |
  전량 몰수 유지. 대신 모달이 몰수를 **명시**한다 — StS 실패 사례
  (확인 다이얼로그가 Save & Quit와 시각 유사 -> 근육기억으로 런 소실)를
  피하기 위해 모달은 안전해 보이면 안 된다. 몰수 경고는 엠버 #F3592C
  (대비 6.11, AA 통과).
signed: [game-designer, game-pm]
```

### entry 15 — 선행 겹침 수정 A·B1·D를 이 사이클 범위에 포함

QA의 D2 유형 전수 감사가 **추가 겹침 8건**을 찾았다. 총 교차면적
**101,792u² = D2의 19.6배**. 그중 셋이 안내가 쓸 좌표대와 같은 자리다.

| # | 겹침 | 면적 | 성격 |
|---|---|---|---|
| A | 레벨업 토스트 × 성장 판 | 14,960u² | 100% 매장, 같은 프레임 동시 표시 |
| B1 | 성장 판 × 스킬행 | 27,528u² | **권장 구성**(터치+가로)에서 81.6% 매장 |
| D | 위기/기세 배너 × 웨이브 배너 | 9,000u² | 완전 포함. **돌발 2종 = 안내 대상** |

```yaml
entry: 15
revenue_point: 없음 (선행 결함)
balance_number: "겹침 면적 >1u^2 인 쌍 = 0 (A/B1/D 한정)"
designer_bound: "안내를 얹기 전에 그 자리를 비워야 한다. D는 안내 대상 자체다"
pm_bound: "범위 확대는 사이클 지연 위험. 8건 전부는 거부, 3건 한정이면 승인"
agreed: |
  A·B1·D 3건만 이 사이클. 나머지 5건 + 조건부 2건은 다음 사이클 안건으로
  등재하되 qa/test-plan.md §v1.8에 좌표와 함께 기록되어 있으므로 소실되지 않는다.
  근본 원인 규칙("두 곳 이상에서 만든 대역은 전부 겹침, 예외 0")은
  **단일 빌더**로 설계 제약에 승격한다.
signed: [game-designer, game-pm]
```

### entry 16 — 도감 "23종 전부 열람" 문구 해석 확정

서베이 G4가 **13/13 (100%, 예외 0)**으로 조우 후 해금이고, 처음부터 전부 보이는
도감은 표본에 없다. 스펙 문구를 *처음부터 내용이 다 보임*으로 읽으면 정면 충돌.

```yaml
entry: 16
revenue_point: 없음 (문구 해석)
balance_number: "23종 전부가 자리를 갖고 양쪽 표면에서 접근 가능"
agreed: |
  "23종 전부 열람" = 전부가 목록에 자리를 갖고 로비/인게임 양쪽에서 접근 가능.
  내용은 조우 후 해금(G4 13/13). 비용 0 — 정지 중복 차단용 23비트가
  그대로 해금 비트다. 미해금은 실루엣(StS/Gungeon형).
  잠금 표시는 **색이 아니라 형태**로 한다 — 잠금 회색 4.38은 AA 미달이다.
signed: [game-designer, game-pm]
```
