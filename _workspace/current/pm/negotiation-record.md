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
