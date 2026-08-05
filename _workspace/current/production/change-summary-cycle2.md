# Cycle 2 변경 정리 — 조사가 무엇을 바꿨는가

run-id 20260805-dungeon-gimmicks · 커밋 `8267eea`(구현) + `cc096e4`(마감)
· 174파일 +7,699/−50 · 푸시: `leeseockmin/hongT` 포크 `dungen` 브랜치

## 1. 조사(서베이)가 결정한 것

**질문**: 유사 장르는 던전별 기믹을 어떻게 구현하는가? (11타이틀: Hades,
Hades II, Dead Cells, Vampire Survivors, Halls of Torment, Diablo IV,
PoE, Enter the Gungeon, Isaac, Moonlighter, Death Must Die)

| 조사 결과 | 우리 게임에 적용된 결정 |
|---|---|
| 스테이지 정체성 = **지배 기믹 1개** (공유 가구는 조연) | 신규 던전 3종 각각 고유 기믹 1종이 지배, 기존 기믹은 보조 배치 |
| 이동차단물(10/11)·제단(5/11)은 포화 — 기존 6스테이지가 이 3종 조합의 재배치일 뿐이라 참신성 게이트(G8) 구조적 미달 | 포화 영역을 피해 **빈도 ≤2/11 아키타입만 채택**: 푸시 레인(1/11), 적 보호 오브젝트(1/11), 침식 벽(2/11) |
| 공정성은 대칭(Gungeon) 또는 예측가능성(HoT/VS)으로 구매 — 플레이어 상대 매복형(D4)은 반감 | 신규 기믹 전부 **고정 시간표**(위상 산술, RNG 0) + 해류/벽은 **적에게도 동일 적용**(대칭) |
| **시장 공백**: 학습 가능한 고정 해저드 타임라인은 어느 타이틀에도 없음 | 무RNG 결정론 심의 천연 강점을 게임 정체성으로 채택 — "스테이지 = 보스 패턴처럼 암기 가능한 안무" |
| QA 캘리브레이션(6타이틀): 텔레그래프 ≥0.8s(경량 피해), 단일 히트 ≤30% HP, 동시 텔레그래프 ≤3 | wall telegraph 1.5s / tick 8dmg(8%) / 동시 텔레그래프 최대 2 — 전 밴드 통과 |

전문: `design/trend-survey/dungeon-gimmick-trends.md`, `qa/benchmark-notes.md`

## 2. 플레이어가 보게 되는 변화

**던전 6개 → 9개.** ash-verdict(재의 판결) 클리어 후 신규 체인 해금:

| # | 던전 | 웨이브 | 고유 기믹 | 플레이 감각 |
|---|---|---|---|---|
| 7 | **재의 수문** (Cinder Sluice) | 8+보스 | 잿물 해류 — 6초 주기 대향 푸시 레인 2줄 (셰브론 흐름 표시) | 활성창에 레인에 서면 초당 140px 떠밀림. 적도 밀리므로 **적을 해류에 태워 진형 붕괴**시키는 게 공략 |
| 8 | **불씨 요새** (Ember Bastion) | 8+보스 | 불씨 방벽주 — 파괴 가능한 기둥 2기, 오라(반경 220) 내 적 **받는 피해 −40%** | 방벽주부터 부수는가(HP 240, 콤보 3-4스윙), 실드 낀 적과 정면 승부하는가의 우선순위 문제 |
| 9 | **재의 행진** (Ash March) | 9+보스 | 재의 장벽 — 22.5초 고정 사이클(휴지9→예고1.5→전진4.5→유지3→후퇴4.5)로 좌측에서 벽이 밀려옴 | 벽 리듬 암기 + 0.6초마다 8피해(적 포함 — **적을 벽에 몰아 처형** 가능). 우측 제단이 리스크/보상 축 |
| — | 보상 | | 첫클리어 유물 +6/+8/+10, 재의 행진 첫클리어 시 동료 scout-echo | 디자이너↔PM 협상 서명 수치 (`pm/negotiation-record.md`) |

로비: 스테이지 리스트가 9장 스크롤로 확장. 신규 카드 3장(청록/주황/회백
액센트, 보스명 Sluice Keeper/Bastion Sentinel/Ash Magistrate). 스토리
말풍선 12개 추가(3부 "집행부" — 수문=기록 말소, 요새=위증 방벽, 행진=형 집행).

## 3. 코드 레벨 변경 (어느 파일이 왜)

### 심 (결정론 코어) — FROZEN 증분은 AMENDMENT #5 문서로 명문화
| 파일 | 변경 |
|---|---|
| `docs/SIM_SPEC_DUNGEONS.md` **신규** | 수치 진실: 기믹 3종 공식·배치 테이블·앵커 3종·동결 해제 목록 |
| `Assets/Scripts/Sim/CampaignTypes.cs` | HazardKind +3종, HazardConfig 필드/팩토리, CampaignSpec 상수 22개, 앵커 3종(cinder-sluice W8 / ember-bastion W8 / ash-march W9) |
| `Assets/Scripts/Sim/CinderSim.cs` | UpdateHazards 분기(해류 이벤트·벽 킨매틱/틱), ApplyCurrents(플레이어·대시·적 3경로), StrikePylons(기본공격·콤보), PylonAuraMultiplier(DamageEnemy 단일 지점), Ember Rest 룸 경계 1..5→1..8 |
| `Assets/Scripts/Sim/SimTypes.cs` | `SimEvents.PylonDown = 1<<22` 1줄 |

### 뷰
| 파일 | 변경 |
|---|---|
| `StageCatalog.cs` | 논리 스테이지 3행 + **ClearedMask 0x3F → 파생 (1<<N)−1** (카탈로그 확장 시 마스크 절단 버그 클래스 원천 차단) + 드레싱 테이블 3종 |
| `CampaignStore.cs` | 저장 마스크 폭 동일 파생값 참조(단일 진실), 레거시 v0.1 호환 유지 |
| `VfxDirector.cs` | 기믹 렌더 3종: 해류 밴드+셰브론 스크롤 / 방벽주 몸통+HP 발광+오라 디스크+파괴 버스트 / 벽 경계선+침식 오버레이+커튼. 전부 풀링, 신규 파티클/라이트 0, reduced-motion 대응 |
| `LobbyView.cs` | 9카드 스크롤(ScrollRect — 44px 터치 플로어 보존) |
| `GameDirector.cs` | 첫클리어 유물 보너스(뷰 지급) |
| `StoryCatalog.cs` | 12비트 추가(기존 라인 무수정 — additive 계약) |
| `Assets/Resources/Fonts/HudKorean.otf` | 서브셋 재생성 456글리프(신규 한글 15자 포함, coverage FULL) |

## 4. 검증 (전부 [OBSERVED])

- **EditMode 223/223** — 기믹 행동·결정론·골든 15행·마스크 라운드트립 포함
  (`qa/test-lane-cycle2.md` 게이트 매핑)
- **WebGL 빌드 57.05MB ≤ 120MB, errors 0**
- **기존 콘텐츠 무영향 증명**: 변경 전/후 다이제스트 12행 바이트 동일
  (아레나·프롤로그·기존 6스테이지) — `qa/golden-digests-cycle2.md`
- **라이브 스모크 4캡처**: `qa/smoke-*.webp` (해류 레인·방벽주 오라·벽
  경계선 렌더 확인)
- 게이트 판정: G7/G1/G6 draft PASS, G8 빈도 조건 PASS(인상 점수는 배포 후
  플레이테스트 항목) — `production/gate-reviews/stage1-gates.md`

## 5. 저장소 상태 (현재 위치)

- 로컬 브랜치 `dungen` = origin/main 최신(3e2e3a1, 타 세션 S8-a 보스 3페이즈
  포함) 리베이스 + 커밋 2개. **머지 충돌 없음, 양 세션 테스트 전부 초록.**
- 푸시: `leeseockmin/hongT` 포크에 완료. **원본(akillness/hongT) 미푸시** —
  이 머신 계정에 쓰기 권한 없음 → akillness 승인 또는 collaborator 추가 필요.
- gh-pages 라이브: **아직 cycle-1 상태** (신규 던전 미배포).

## 6. 게임 실행 방법

- **에디터에서**: CinderCourt.unity 씬 열고 Play(⌘P). (원격 트리거
  `Temp/autoplay-once` 마커도 준비돼 있음 — touch 후 에디터 포커스.)
- **브라우저에서**: `cd build-webgl && python3 -m http.server 8901` →
  `http://localhost:8901/index.html`
- 신규 던전 바로 확인: 위 URL 뒤에 `?mode=campaign&stage=cinder-sluice`
  (선행 미클리어 시 로비로 폴백 — 정상 해금 검사)

---

# v1.1 리튠 (2026-08-05 플레이테스트 피드백 반영)

피드백: "기믹이 안 느껴진다". **근본 원인 = 배치 기하** — 세 기믹 전부
전투가 실제로 수렴하는 중심(768,604)에 닿지 않았다:

| 기믹 | v1.0의 문제 | v1.1 처방 |
|---|---|---|
| 해류 | 밴드 간극(130px)이 정확히 스폰 y — 세로로 안 움직이면 평생 안 밀림 | halfH 70→**110**(간극 50px), push 140→**200**(역주행 사실상 불가), active 2.4→**3.2s**, **안전 회랑에 vent 2기 폭격** |
| 방벽주 | 오라(220)가 스폰에서 3px 차이로 밖 — 실드가 걸린 적을 볼 일이 없음 | 오라 **280**(스폰이 3기 오라 안 — 중심 전투가 항상 실드전), 배율 0.60→**0.40(−60%)**, hp 300, **3기째 추가**, 적 실드 시안 틴트(뷰) |
| 벽 | 최대 침식 x608 — 중심에서 160px 못 미침, 갈 일 없는 왼쪽 구석만 위협 | **양벽(좌+우) 반주기 교대**, depthMax **560**(양벽 모두 중심 통과: 좌 x808/우 x728), 휴지 40%→20%, tick 8→**10**. 제단을 중앙으로 — 사이클당 35%는 벽이 삼킴(리스크/보상) |

- 안전 보장: 양벽 동시 침식 시 depth 합 = 440 상수 → 회랑 항상 ≥600px
  (탈출 불가 없음). 동시 텔레그래프 ≤2 (예산 3).
- 접근성: Unity 메뉴 **CinderCourt/Dev/Unlock All Stages** 신설 — 에디터
  Play에서 신규 던전 즉시 진입(“에디터에서 안 보임” 원인 해소).
- 기존 6스테이지·아레나·프롤로그는 여전히 불변.
