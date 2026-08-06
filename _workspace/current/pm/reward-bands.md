# Reward Bands — cycle 2 (G5 게이트 블록)

```yaml
comeback:
  # 무결제 게임 — '일발역전' 상당 = 첫클리어 일회성 유물 보너스
  first_clear_bonus: {cinder-sluice: 6, ember-bastion: 8, ash-march: 10}
  bonus_vs_run_income_max: 0.25     # 계약값 — 실측이 반박함, 아래 주 참조
  paths: [first-clear]              # 반복 경로 없음(일회성)
steady:
  parity_sessions_band: [10, 20]    # T5 도달 세션 수 실측 12-18 [TARGET]
fairness:
  paid_free_winrate_delta_max_pp: N/A   # 결제 부재 — director 웨이버 (만료: 결제 도입 시)
```

- 근거 서명: pm/negotiation-record.md entries 1-4.
- QA 검증 행: gate-measurements.md#g5 (kiter 유물/런 실측, 보너스 비율).
- **실측 반박 (2026-08-05 플레이스루)**: 봇 런 유물 수입 17-18 대비 보너스
  비율 33-59% — 0.25 계약 초과 [OBSERVED, qa/playtest-report.md]. 일회성
  (mask 비트 재지급 차단 확인)이라 per-activation 개념엔 비저촉. Stage 2
  협상 2R에서 분모를 "풀런(전 웨이브+보스) 수입"으로 재정의 후 재서명할 것 —
  그 전까지 이 밴드는 미검증 상태로 취급.
