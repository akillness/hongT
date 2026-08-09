# Telemetry Contract — cycle 2

원칙(docs/nan2026/02-ai-tech.md): 배포 빌드는 **무 네트워크 정적 페이지** —
외부 추론·API·원격 애널리틱스 없음. 텔레메트리 = localStorage 로컬 계약 +
배포 검증 아티팩트.

## 필드 (localStorage)

| 키 | 필드 | 쓰는 쪽 | 소비 게이트 |
|---|---|---|---|
| `abyssal-lantern:cinder-court:last-run` | RunDigest{score,wave,kills,relics,healthRemaining,reason} | GameView(런 종료) | G2/G7 세션 증거 |
| `abyssal-lantern:unity:campaign` | cleared[](신규 3 id 추가), equipment, stats, relics, roster, active, prologueDone | CampaignStore | G5(유물 수입), R8(스키마 불변) |

- cycle-2 증분: `cleared[]`에 `cinder-sluice|ember-bastion|ash-march` 추가
  가능(스키마 변화 없음 — R8 준수). ClearedMask 9비트 확장은 뷰 내부 표현.
- PM 예측 필드(G5): relics/런은 last-run digest로 산출 가능 — 신규 필드 불요.

## QA 검증 필드

- 배포 스모크: 신규 스테이지 클리어 → campaign 키에 id 추가 확인(콘솔).
- R8 회귀: 구 스키마 블롭 로드 시 동일 동작(레거시 테스트 — StageCatalogTests).

---

## v3.0 기믹 무기화 (run-id 20260809-dungeon-fun-authorship)

원칙 불변: **무 네트워크 정적 페이지.** 외부 애널리틱스 없음, localStorage만.

### 신규 필드 — **0개**

이번 사이클이 요구하는 측정은 전부 **기존 필드 또는 스냅샷에서 산출된다.**
스키마 변화 0 → R8(구 스키마 호환) 재검증 불요.

| 측정 항목 | 소스 | 신규 필드 |
|---|---|---|
| 선택 압력 | 해저드 표 + 각인 바인딩 **정적 산술** | 없음 (런타임 아님) |
| W4 발동률 | `SkillRoll` 입력이 `(enemyId, wave, attackOrdinal)` — 전부 재현 가능 | 없음 |
| W4 천장 기여 | 동일 — 해시를 오프라인 재실행하면 나온다 | 없음 |
| W2 환급 총량 | 처치 수 × 환급 상수. 처치 수는 `RunDigest.kills` | 없음 |
| 기름 순수지 (entry 17↔19) | `IHackSnapshot.Charge` 시계열 — **이미 노출됨** | 없음 |
| 오퍼 수용률 | `IGrowthChoiceSnapshot` — 이미 존재 | 없음 |

**결정론이 텔레메트리를 대체한다.** 같은 입력이 같은 출력을 내므로, "무슨
일이 일어났는가"를 기록할 필요 없이 **재현하면 된다.** 이것이 무RNG의
운영상 이점이고 이 사이클이 처음으로 그것을 명시적으로 쓴다.

### 예외 하나 — 사람이 만드는 값

| 항목 | 왜 재현 불가 | 수집 방법 |
|---|---|---|
| G8 인상 점수 (T-Z1~Z4) | 사람의 판단 | 세션 기록지, `qa/gate-measurements.md#g8`에 수기 |
| 오퍼 **수용** 여부 | 플레이어 입력이라 재현하려면 입력을 기록해야 함 | 세션 중 관찰 (≥5 세션) |

두 번째가 미묘하다. 오퍼 수용률은 **입력에 의존**하므로 심을 재현해도 안
나온다 — 재현하려면 입력 시퀀스를 저장해야 하고 그건 신규 필드다.

→ **이번 사이클은 신규 필드를 만들지 않고 세션 관찰로 대체한다.**
표본이 작아지지만(≥5 세션), C7 판정(레벨업 정지 전환 여부)에 필요한 것은
"만료율이 높은가"라는 **방향**이지 정밀한 비율이 아니다.

만약 다음 사이클이 정밀한 수용률을 요구하면 그때 입력 로그 필드를 만든다 —
그것은 스키마 변경이고 R8 재검증이 붙는다. **지금 미리 만들지 않는다.**

### 배포 스모크 확인 항목 (v3.0 추가)

- 던전 클리어 → `campaign` 키에 id 추가 (기존)
- **적 4종이 화면에서 구분된다** (T-V1 — 콘솔이 아니라 육안)
- **아레나·프롤로그에서 4종 행동 분화가 안 보인다** (T-V4·V5)
