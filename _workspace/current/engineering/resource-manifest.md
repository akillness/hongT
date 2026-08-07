# Resource Manifest — cycle 2 (G6-ops draft 입력)

신규 스테이지 3종의 자산 예산: **신규 임포트 0** — 전부 기존 자산 재사용.

| 용도 | 자산 | 상태 |
|---|---|---|
| 터레인 | terrain-abyss-chancel(sluice) / terrain-cinder-span(bastion) / terrain-echo-throne(march) | 기존 프리팹 재사용 [OBSERVED, Resources/Terrain] |
| 드레싱 | cinder-span 라이브러리 자식(기존 90 파츠) | 기존 — 신규 테이블만(코드) |
| 보스 | shadow-commander-boss ×2 틴트, broken-court-monarch-boss ×1 틴트 | 기존 프리팹 + MPB 틴트 |
| 카드 글리프 | skill-dash / skill-ward / skill-strike | 기존 Resources/Icons [OBSERVED] |
| 기믹 VFX | 풀드 쿼드/실린더/링(코드 생성, VfxDirector 문법) | 신규 메시·텍스처 0 |
| 오디오 | HazardPulse 기존 큐 재사용 | 신규 생성 0 |
| 동료 보상 | scout-echo(기존 추출 변형 틴트) | 기존 |

- WebGL 빌드 크기 영향: 코드+데이터만 — 예산 ≤120MB 위협 없음
  [TARGET — G6 최종에서 빌드 실측].
- 폰트 서브셋: 신규 한글 문자열(StoryCatalog 12비트, 카드 3종) — ViewLane
  리포트의 신규 글리프 목록을 로비 폰트 테스트로 검증(기존 게이트).
