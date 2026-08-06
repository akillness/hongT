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
