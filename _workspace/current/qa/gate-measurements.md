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

## v1.1 리튠 재게이트 (2026-08-05)

- EditMode: **225/225** [OBSERVED, unity-logs/test-results-220506.xml] —
  v1.1 상수/배치/듀얼 벽/오라 0.40/센서스/골든 전부 포함.
- WebGL 빌드: `result=Succeeded size=57024061 errors=0` [OBSERVED,
  unity-logs/build-220518.log] (warnings 2건은 main 세션 코드 소유 — 기존).
- 골든 재고정: 신규 3행 Unity 재기록 — **정수 필드는 dotnet과 Unity 완전
  일치**(1400/3/9/0/124 · 2500/3/11/2/112 · 3600/4/16/0/142), float만 교체
  (기록된 ~ULP 드리프트 패턴 그대로). 기존 12행 무변경.
- 브라우저 육안 [OBSERVED, qa/retune-*.webp 3장]: sluice 밴드가 스폰을 물고
  회랑 vent 착탄, march 양측 경계선(엠버)+우벽 침식 오버레이, 벽 틱 피해
  비네트. 헤드리스 스크린샷 스톨 1회는 오토파일럿 interval 과부하로 추정
  [INFERENCE] — 실기기 프로파일은 G6 최종(Stage 3) 항목.
- 텔레그래프 센서스(테스트 고정): sluice 최대 2 · bastion 1 · march 2 (≤3).
- 벽 단일 히트 10 ≤ 30%×100 · 회랑 불변식 gap ≥ 600px(테스트 고정,
  실측 599.99994 — float, 계약 의미 내).

## v1.2 재미 패스 재게이트 (2026-08-05)

- EditMode: **231/231** [OBSERVED, unity-logs/test-results-231022.xml].
  빌드: 57.03MB errors 0 [build-231034.log].
- 골든 v1.2 분리: 무버 5행(1·3·4·5·8) 재고정 — **정수 필드는 dotnet/Unity
  일치**(에코스론 4100/4/16/2/142 등), float만 교체. 불변 안전망
  (아레나×2·프롤로그·스테이지 0·2·6·7·클래식 3) 무변경 [OBSERVED].
- 게이트 중 잡은 것: EmberGallery 링 테스트가 정확한 틱 id를 어서션 —
  fmod/floor 경계 1틱이 런타임별로 갈림(dotnet 통과·Unity 실패). 격자
  패턴(36±1틱 간격+랩 순서) 어서션으로 재작성 → 231/231.
- **난이도 실측 신호 (Stage 2 협상 입력)**: ash-march 골든이 hp 8로 종료
  (v1.1은 142) — 5/5/5 풀장비 kiter가 30초를 간신히 생존. 리튠 체감 달성의
  증거인 동시에 피날레 구간 과열 후보. 사람 플레이테스트에서 사망률 확인 후
  wall tick 10→9 또는 vent 위상 완화를 협상 안건으로.
- 골든 취약성 주지: ash-march 행은 사망 1틱 마진 — reason 필드가 바뀌면
  회귀가 아니라 이 민감도의 산물일 수 있음(테스트 주석에 재고정 프로토콜).
