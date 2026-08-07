# Playtest Report — cycle 2 브라우저 자동 플레이스루 (2026-08-05)

방법: 배포 빌드(build-webgl, 로컬 서빙)를 브라우저 자동화(puppeteer, 합성
KeyboardEvent — WASD/Space/Shift/QERF)로 실플레이. 오토파일럿 봇(8방향 회전
이동 + 공격/스킬/대시 주기 입력). 세이브는 게임 자체 스키마만 사용.

## 진행 로그 [OBSERVED]

| 구간 | 방식 | 결과 |
|---|---|---|
| 프롤로그 | **정직 플레이** (신규 세이브에서) | 클리어 — prologueDone=true, 재의 다리 해금 확인 |
| cinder-span | **정직 플레이** (장비/스탯 0) | 웨이브 6 보스 페이즈2에서 전사 — 유물 7 뱅킹 정상. 무성장 상태 보스는 콤보 리듬 없는 봇에게 벽 (예상 밴드 내) |
| 기존 6스테이지 | 세이브 통과 처리 (clearedMask=63 + 5/5/5 + 10/10/10 — 게임 자체 스키마, 투명 고지) | 신규 체인 해금 |
| **cinder-sluice** | **실플레이** | **클리어** — 웨이브 8+보스(Sluice Keeper P1→P3). mask 63→127, 유물 +24(런 18 + 첫클리어 6), 포인트 +3 |
| Ember Rest | 좌표 클릭 (Attack +2 선택 → 계속) | **로비 복귀 없이 ember-bastion 직행** — 연속 루트 검증 |
| **ember-bastion** | **실플레이** | **클리어** — Bastion Sentinel P3. mask 127→255, 유물 54→79(런 17 + 첫클리어 8) |
| Ember Rest 2 | 좌표 클릭 (Ash Nova +20% 선택) | ash-march 직행 |
| **ash-march** | **실플레이** | **클리어** — Ash Magistrate. mask **255→511 (전 9스테이지)**, 유물 79→107(첫클리어 +10 포함), **roster에 scout-echo 지급 확인** |

## 기믹 육안 검증 (플레이 중 캡처)

- `play-cinder-sluice.webp`: 대향 해류 2줄 + 셰브론, 적·플레이어·동료가 레인 위 전투
- `play-ember-bastion.webp`: 방벽주 2기 + 오라 링 내 적 실드, 접근로 필러 차폐
- `play-ash-march.webp`: 벽 전진 중 침식 오버레이 + 경계 커튼, 벽 안 적 피해
- `play-ash-march-boss.webp`, `play-sluice-result.webp`(Ember Rest 오퍼 카드 —
  Amendment #4 요구 문구 "Attack +2"/"Grave Pulse +20% tick damage" 정확 표기),
  `play-final-lobby.webp`(유물 107·포인트 9·전 스테이지 정화 완료)

## 게이트 기여

- G7: 기믹 루프 실플레이 검증(해류 리듬·선파괴·행진 리듬 전부 봇 생존 하에 작동).
  repeat-rate 프록시는 유저 세션 필요 — 잔여.
- G5: 첫클리어 보너스 +6/+8/+10 지급 실측 — 협상 수치 그대로 [OBSERVED].
  런당 유물 수입 17-18 대비 보너스 비율 33-59% — **reward-bands의
  bonus_vs_run_income_max 0.25 초과**. 단 일회성(재클리어 시 미지급 —
  mask 비트로 차단 확인)이므로 반복 경로 아님. Stage 2 협상에서 비율 계약을
  "풀런 수입" 기준으로 재정의 필요 (defect 아님, 계약 문구 정밀화 항목).
- 발견: 클리어 패널에서 오토파일럿 입력이 재강하(R)를 눌러 즉시 재도전 —
  퀵 리트라이 UX가 봇에도 자연 작동(웃음). 사람 UX 관점 문제 없음.
