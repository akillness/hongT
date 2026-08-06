# Production Brief — run-id 20260806-training-and-upgrade

작성: game-production-director · 2026-08-06 · bmad-gds 스키마

```yaml
game_type: 아이소메트릭 핵앤슬래시 로그라이크 (Unity 6000.5.6f1 / URP / WebGL)
team_shape: 1인 + 하니스 5역할 (director/designer/pm/programmer/qa), 교차 세션 존재
engine: Unity WebGL — 결정론 고정스텝 60Hz 순수 C# 심 + 읽기전용 뷰
current_stage: 라이브 (던전 9종 · 기믹 6종 · 각인 5종까지 구현, cycle-2 마감)
next_public_beat: "gh-pages 라이브 — 훈련이 반복 가치를 갖고, 강화가 돌발까지 다룬다"
source_packet:
  - 사용자 요청: "강화쪽 훈련쪽 다시 기획 + 기믹/돌발 추가해서 재구현. 강화 훈련 둘다"
  - 코드 실측: 훈련(프롤로그) = 3웨이브(4/6/8) · 기믹 0 · 보스 0 · 토스트 4단계
  - 코드 실측: 강화 = 스탯 3 + 장비 3 + 각인 5종(v1.5, AMENDMENT #6)
  - 선행 조사: .survey/dungeon-gimmick-trends/ (기믹), .survey/meta-upgrade-gimmick-interaction/ (강화-기믹 접점)
main_constraint: |
  ① 심 수치 FROZEN — 추가만 허용, 기존 값 변경 시 AMENDMENT 문서 필수
  ② 무RNG 결정론 — "돌발"도 난수가 아니라 고정 시간표/조건으로 만들어야 한다
  ③ 골든 15행 불변이 안전망 — 신규 경로는 옵트인이어야 기존 플레이가 안 움직인다
  ④ WebGL 예산 — compute/threads 금지, 빌드 ≤120MB (현재 57.4MB)
main_question: |
  훈련(프롤로그)은 왜 한 번 하고 버려지는가, 그리고 강화는 왜 돌발 상황을
  다루지 못하는가. 두 질문은 같은 공백을 가리킨다 — 플레이어가 기믹을
  **연습할 자리**가 없고, 강화가 **예측 못 한 순간**에 개입하지 못한다.
```

## 왜 지금인가

cycle-2가 던전 기믹(v1.0-v1.2)과 강화-기믹 접점(v1.5 각인)을 채웠다. 남은 구멍
두 개가 이번 사이클의 대상이다:

1. **훈련이 일회용이다.** 3웨이브 튜토리얼을 깨면 `prologueDone=true`가 되고,
   이후 "재훈련" 버튼은 같은 3웨이브를 다시 줄 뿐이다. 던전 기믹 6종 중
   훈련에 등장하는 것은 **0개** — 플레이어는 기믹을 실전에서 처음 만난다.
2. **강화가 정적 상황만 다룬다.** 각인 5종은 전부 **상시 적용 배율**이다
   (해류 밀기 ×0.5, 벽 틱 10→6…). 고정 시간표를 외운 뒤에는 강화가 개입할
   "순간"이 없다 — 위기·기회 같은 **돌발 상황에 반응하는 축**이 비어 있다.

## 이번 사이클의 operating mode

**Stage 2 재진입이 아니라 Stage 1 (concept shift).** 훈련 모드의 성격 자체와
강화의 축 하나를 새로 세우는 일이라 개념 단계부터 시작한다.

단, 조사 없이 발명하지 않는다 — 훈련 모드와 돌발 이벤트는 **아직 조사한 적이
없는 영역**이다(선행 서베이 2건은 기믹 배치와 강화-기믹 접점만 다뤘다).
Stage 1a 서베이가 선행 조건이다.

## 범위 밖 (one operating mode per cycle)

- gh-pages 배포 (origin 권한 403 — 사람 판단 대기)
- 44px 터치 하한 로비 재배치 (designer+pm 협상 안건, 이월)
- ash-march 과열 최종 판정 (사람 플레이테스트 필요)
- 각인 가격 서명 (negotiation entry 6, PM 대기)
