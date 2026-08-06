# Triage

- Problem: Cinder Court의 훈련(프롤로그)은 일회용이고, 강화는 정적 상황만 다룬다.
  코드 실측(direct page retrieval, `Assets/Scripts/Sim/HackTypes.cs` §1 `HackSpec`):
  훈련은 `PrologueWaves = 3`, `PrologueSpawns = {4, 6, 8}`, 기믹 0종, 보스 0,
  스킬·대시 전면 무효(`CinderSim.cs:766-771` — "the prologue is movement + basic
  attack only"), 클리어 시 `prologueDone=true`. 재훈련 버튼은 같은 3웨이브를
  반복할 뿐 보상도 진행도도 없다. 강화 쪽은 각인 5종 전부 **상시 적용 상수 배율**
  (direct page retrieval, `_workspace/current/design/sigil-spec.md` §각인 표:
  해류 밀기 ×0.5, 벽 틱 10→6, 제단 채널 1.2→0.8초 …) — 고정 시간표를 외운 뒤에는
  강화가 개입할 "순간"이 없다. 두 구멍은 같은 곳을 가리킨다: 플레이어가 기믹을
  **연습할 자리**가 없고, 강화가 **예측 못 한 순간**에 붙을 자리가 없다.
  이 서베이는 그 두 자리를 장르가 어떻게 채우는지 조사한다 — 설계 결정은 Stage 1b.
- Audience: 신규 플레이어(기믹 6종을 실전에서 처음 만난다), 복귀 플레이어(훈련이
  숙련 확인 장소가 못 된다), 숙련 플레이어(고정 시간표를 외운 뒤 강화가 개입할
  순간이 없다), 그리고 이 사이클의 designer/pm/qa 하니스 역할.
- Why now: cycle-2가 던전 기믹(v1.0-v1.2)과 강화-기믹 접점(v1.5 각인)을 채웠고
  남은 구멍이 훈련·돌발 둘이다. 선행 서베이 2건(`.survey/dungeon-gimmick-trends/`,
  `.survey/meta-upgrade-gimmick-interaction/`)은 기믹 **배치**와 강화-기믹 **접점**만
  다뤘고 훈련 모드와 돌발 이벤트는 아직 조사한 적이 없다. Stage 1a 서베이가
  concept shift의 선행 조건이며, 조사 없이 발명하지 않는다는 것이 이번 사이클의
  operating mode다.
