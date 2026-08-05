# Reward Bands — cycle 2 (G5 게이트 블록)

```yaml
comeback:
  # 무결제 게임 — '일발역전' 상당 = 첫클리어 일회성 유물 보너스
  first_clear_bonus: {cinder-sluice: 6, ember-bastion: 8, ash-march: 10}
  bonus_vs_run_income_max: 0.25     # kiter 평균 런 유물 수입 대비, QA 실측 검증
  paths: [first-clear]              # 반복 경로 없음(일회성)
steady:
  parity_sessions_band: [10, 20]    # T5 도달 세션 수 실측 12-18 [TARGET]
fairness:
  paid_free_winrate_delta_max_pp: N/A   # 결제 부재 — director 웨이버 (만료: 결제 도입 시)
```

- 근거 서명: pm/negotiation-record.md entries 1-4.
- QA 검증 행: gate-measurements.md#g5 (kiter 유물/런 실측, 보너스 비율).
