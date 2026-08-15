# 제1·2·3부 WebGL VFX 검증 매트릭스

작성: 2026-08-11. 대상 빌드: `build-webgl` (VFX 통합 후 2026-08-11 16:15 빌드).

## 실행 방법과 판정 경계

- `drive_three_acts.mjs`가 1440×900 Chrome/SwiftShader에서 9개 스테이지를
  서로 독립된 컨텍스트로 연다.
- 각 정상 런은 출정 카드, 진입, NE/SW 이동, 근접, Q/E/Shift/F/R, 경고 주기까지
  15장을 저장한다. 총 135장이다.
- 각 부의 감소 모드는 환기구가 있는 대표 스테이지 1개씩 총 3런, 15장을 저장했다.
- `pageErrors=0`만으로는 통과시키지 않았다. 최초 제3부 좌표가 로비에 남았는데도
  자동 로그가 성공했던 것을 3×3 연락판이 잡았다. 제3부 버튼 중심을
  `[433,555,677]`에서 `[381,503,624]`로 보정해 세 런을 덮어쓰고
  `full-browser-report.json`을 다시 조립했다.
- 드라이버는 이 발견 뒤 `GameFlowAgentAPI.observe()`의 `wave >= 1`,
  `current_phase != loading`, `max_hp > 0`와 이동 거리 `> 1`도 검증하도록 강화했다.

## 정상 모드 — 9스테이지

| 부 | 스테이지 | 카드/진입 HUD | 인테리어·누락 텍스처 | 이동 | 근접·Q/E/Shift/F/R·경고 | 브라우저 오류 | 판정 |
|---|---|---|---|---|---|---:|---|
| 제1부 기록 | `cinder-span` | `Cinder Span — 웨이브 1/5` | 다리/용암/잔해 가시, 흰·마젠타 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |
| 제1부 기록 | `ember-gallery` | `Ember Gallery — 웨이브 1/5` | 환기구 윤무/기둥 가시, 누락 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |
| 제1부 기록 | `abyss-chancel` | `Abyss Chancel — 웨이브 1/6` | 미로/빙결 타일 가시, 누락 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |
| 제2부 증언 | `witness-well` | `Witness Well — 웨이브 1/6` | 쌍 제단/환기구 가시, 누락 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |
| 제2부 증언 | `echo-throne` | `Echo Throne — 웨이브 1/7` | 조류/제단/수면 가시, 누락 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |
| 제2부 증언 | `ash-verdict` | `Ash Verdict — 웨이브 1/7` | 방벽주/제단 가시, 누락 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |
| 제3부 집행 | `cinder-sluice` | `Cinder Sluice — 웨이브 1/8` | 조류 체브런/잔해 가시, 누락 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |
| 제3부 집행 | `ember-bastion` | `Ember Bastion — 웨이브 1/8` | 방벽주 군집/용암 가시, 누락 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |
| 제3부 집행 | `ash-march` | `Ash March — 웨이브 1/9` | 잿벽/제단/환기구 가시, 누락 쿼드 없음 | 2프레임 | 7종 캡처 | 0 | PASS |

연락판: `contact-entered.png`, `contact-moved.png`, `contact-rift.png`,
`contact-eruption.png`, `contact-shard.png`, `contact-aegis.png`,
`contact-nova.png`, `contact-warning.png`.

## 감소 모드 — 부별 대표

| 부 | 대표 스테이지 | 스크린샷 | pageErrors | 수치/육안 판정 |
|---|---|---:|---:|---|
| 제1부 | `ember-gallery` | 5 | 0 | 외곽 링 120ms 평균 휘도 변화 0.3559% `<2%` — PASS |
| 제2부 | `echo-throne` | 5 | 0 | 감소 모드 실환경 렌더 PASS; 두 샘플이 경고 상태 경계를 지나 수치 게이트에서는 제외 |
| 제3부 | `ash-march` | 5 | 0 | 감소 모드 실환경 렌더 PASS; 두 샘플이 피격 비네트 경계를 지나 수치 게이트에서는 제외 |

수치 원본은 `reduced-motion-metrics.json`, 재현 코드는
`measure_reduced_roi.py`다. VFX 런타임의 frame 7 고정 자체는
`VfxRuntimeSheetTests`에서도 독립 검증된다.

## 남은 범위

- 이 매트릭스는 VFX와 스테이지 환경 렌더를 닫는다. 2/5/8 스테이지 실제 클리어 뒤
  액트 릴이 재생되는 장시간 브라우저 경로는 별도 수용 항목이다.
- 이동 입력과 실제 위치 변화는 확인하지만, 각 스테이지의 특정 블로커를 따라가는
  장거리 우회 궤적을 정량화하지는 않았다.
