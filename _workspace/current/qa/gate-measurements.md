# Gate Measurements — cycle 2 (run-id 20260805-dungeon-gimmicks)

측정 = 값 + 방법 + 증거 경로. [INFERENCE] 값은 게이트 인용 금지(벤치마크 규칙).

## #g2 — 규칙·밸런스

- 단일 히트 상한: wall tick 8 ≤ 30%×maxHP(최소 100) — 정적 감사
  [OBSERVED, CampaignSpec.WallTickDamage=8]. **지속 노출 결정 기록**: 벽 대역
  잔류 시 이론상 ~12틱(96) 노출 가능 — D3/D4 "DoT는 서 있으면 치명" 관례 채택,
  이탈 속도 여유 218 vs 80 (2.7×). 상한 위반 아님(사유 기록됨).
- 해류 직접 피해 0 — 대체 밴드: 푸시 유발 접촉피해 (EditMode 후 측정).
- TTK/클리어 매트릭스: EditMode 게이트 후 채움 (kiter+rusher @2/1/3, 5/5/5).
- 사전 행동 프로브 [OBSERVED, dotnet 스탠드얼론 — golden-digests-cycle2.md]:
  wallKin 스펙 일치(f9=248, f12=368), wallDmg 8×3 in [10.5,18),
  current drift +447px, pylon down 2.48s(콤보 3스윙 263≥240).

## #g5 — 경제 (밴드: pm/reward-bands.md)

- 첫클리어 보너스 +6/+8/+10 — kiter 평균 런 유물 수입 실측 후 25% 계약 검증.
  (참고: 1800틱 kiter relics 1-4/런 — 30초 단면이므로 풀런 실측 필요.)

## #g7 — 코어 루프

- 루프 주기 모델: design/core-loop.md — N1 6.0s(상위 25-40s), N2 30-60s,
  N3 22.5s 고정. 전부 30-180s 대역 내(상위 루프 기준). 이벤트 트레이스는
  EditMode 후.

## #g8 — 참신성

- 빈도표: tide-current 1/11, ember-pylon 1/11, ash-wall 2/11 — 전부 ≤2/≥5
  기준 통과 [design/trend-survey/dungeon-gimmick-trends.md, QA 분모 검증:
  11 타이틀 ≥ 5 충족]. thin-evidence 셀(current ETG(t), pylon PoE(t))은
  솔루션 문서 출처 라벨 확인 완료 — 반증 출현 시 재판정.
- 인상 점수 ≥4/5: 배포 후 구조화 플레이테스트 (미측정).

## #g6 — ops (텔레그래프 예산 포함)

- 동시 텔레그래프 센서스(사전 산술): sluice 0(위상 3.0s 반주기 오프셋),
  ash-march 최대 2([9.0,9.2) 벽×vent 겹침), bastion vent 1 — 전부 ≤3.
  LCM 센서스 테스트(D3)가 기계 고정 예정.
- EditMode 전체 초록 + WebGL 빌드 ≤120MB: Unity 6000.5.6f1 로컬 설치 완료
  (WebGLSupport 확인) — 게이트 실행 대기.
