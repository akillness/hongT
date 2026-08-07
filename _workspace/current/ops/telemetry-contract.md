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
