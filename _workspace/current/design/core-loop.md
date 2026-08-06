# Core Loop — cycle 2 (G7 수치 모델)

기존 인런 루프(전투→웨이브→픽업→레벨→보스)는 cycle-1에서 G7 PASS 이력.
cycle 2는 **기믹 상호작용 루프**를 신설 스테이지별로 추가 정의한다.

## Loop N1 — cinder-sluice "해류 리듬" (tide-current)
- 주기: 6.0 s (해저드 주기와 동일) — 대역 30–180 s는 상위 웨이브 루프
  (스폰→해류 활용 진형 붕괴→섬멸→픽업, 실측 목표 25–40 s)로 충족.
- 액션/루프 ≥3: ① 텔레그래프 확인 ② 레인 횡단/역주행 결정 ③ 적 유인
  ④ 밀린 적 처치.
- 보상 이벤트 ≥1: 처치 픽업(기존) + 진형 붕괴로 인한 다중 처치(점수).

## Loop N2 — ember-bastion "선파괴 우선순위" (ember-pylon)
- 주기: 파일런 1기 교전 사이클 실측 목표 30–60 s (접근→pillar 차폐 돌파→
  파일런 파괴→실드 해제된 적 섬멸).
- 액션 ≥3: ① 오라 범위 파악 ② 접근로 선택 ③ 파일런 타격(콤보/스킬 배분)
  ④ 잔적 처리. 보상: PylonDown 이벤트 + TTK 정상화(체감 보상) + 픽업.

## Loop N3 — ash-march "행진 리듬" (ash-wall)
- 주기: 22.5 s 고정 사이클 — 대역 내.
- 액션 ≥3: ① rest 중 좌측 픽업 회수 ② telegraph에 우측 이탈 ③ 적을 벽으로
  유인 ④ altar(우측) 체류 보상 판단.
- 보상 ≥1: 벽 처치 크레딧(점수/기름) + AltarBlessing(+18 기름).

## 측정 계약
- QA event-trace 세그먼트(HazardPulse/PylonDown/AltarBlessing/WaveStarted
  타임스탬프) → gate-measurements.md#g7.
- repeat-rate 프록시 ≥70%: 배포 빌드 퀵 리트라이 사용률, ≥5 세션.
