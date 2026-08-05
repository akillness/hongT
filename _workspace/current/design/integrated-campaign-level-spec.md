# Integrated Spec B — 캠페인·레벨 디자인 통합 스펙 (View-only + §S5 경계 1곳)

2026-08-05 · Achilles Visual Overhaul 통합 문서 B · 구현 대상: `Assets/Scripts/View/**`(레벨 프레젠테이션·전환) + 캠페인 구성 데이터.

**Sim 불가침**: `Assets/Scripts/Sim/**` 및 수치·타이밍 변경 금지. §S5 경계 리터럴 1곳만 예외(AMENDMENT #4 게이트).

## 통합 매핑 (원본 → 본 문서)

| 원본 문서 | 원본 항목 | 본 문서 |
|---|---|---|
| deep-interview-cinder-court-dungeon-revival.md | 인수 7행표·3룸+2페이즈 게이트·Transcript R0-1 | §1, §2 |
| achilles-visual-overhaul-spec.md | §T 6스테이지 체인·§T1·§T2(ClearedMask)·§T4(자산)·§T5(검증)·런 모델·준비 교체 | §3, §4, §5, §6, §7 |
| combat-feel-boss-phase-spec.md | §B 플레이타임 | §3.3 |
| deep-interview-vfx-terrain-command-hardening.md | Lane T(터레인) | §6 |

## §1. 캠페인 구조 (cinder 인터뷰 확정)

### 1.1 인수 7행표
| # | 인수 | 상태 |
|---|---|---|
| 1 | 3구역 × 2페이즈 게이트(보스 2기) | 확정 |
| 2 | 3룸 + Ember Rest 휴식 | 확정 |
| 3 | 시드 결정론(RNG 금지) | 확정 |
| 4 | 캠페인 전용 규칙은 SIM_SPEC_CAMPAIGN.md | 확정 |
| 5 | URL 계약 `?mode=campaign&stage=<id>` | 확정 |
| 6 | 준비 화면에서 로스터·장비 선택 | §5 |
| 7 | 스테이지 체인은 achilles §T | §3 |

### 1.2 3룸 + 2페이즈 게이트
- 룸 구성: **전투 룸 → (보스 룸) → Ember Rest → 다음 구역**.
- 페이즈 게이트: 구역 1·2는 보스 페이즈로 종결, 구역 3은 최종 보스(2페이즈 + 소환).
- Ember Rest 경계 [OBSERVED]: 휴식 룸 진입 시 상태 보존·회복·장비 교체. 룸 전환은 결정적 시퀀스.

### 1.3 Transcript R0-1 계약
- R0(점화 훈련)→R1(첫 구역 강하): 스토리 자막 + 카메라 강하 연출은 View — 심 상태 전환과 분리.
- 대사는 월드공간 말풍선(원작 문법) 유지, 번역본 사용.

## §2. 시드·결정론 규칙

- 스테이지/룸 배치·스폰·드롭 전부 결정적. **RNG 사용 금지** — `System.Random`·`UnityEngine.Random` 금지.
- 룸 시퀀스는 캠페인 정의 데이터(아래 §3 체인)에서 파생.
- View가 참조하는 레벨 데이터는 빌드 타임 직렬화된 불변 리스트(ScriptableObject 또는 정적 테이블) — 심과 동일 소스만.

## §3. 6스테이지 체인 (achilles §T)

### 3.1 체인 정의
```
1 → 1+2(보스) → 2 → 2+3(보스) → 3 → 1+3(최종)
```
- 구역 모티프: 1=Cinder Span(다리), 2=Abyss Chancel(성소), 3=Echo Throne(왕좌).
- 각 노드는 (룸 구성, 적 조합, 보스 유무)를 결정적으로 참조.

### 3.2 §T1 하드코딩 8곳 — 데이터화
- 기존 스테이지 진입·종료 하드코딩 8곳을 체인 테이블 조회로 교체.
- `stage` 파라미터 유효성: 체인 외 ID는 로비 리다이렉트.

### 3.3 §B 플레이타임
- 플레이타임 조정은 **6스테이지 체인으로만** — 개별 전투 수치·타이밍 변경 금지.
- 타겟: 전체 25-30분(구역당 8-10분). 초과/미달 시 체인 노드 구성만 조정.

## §4. 보스 페이즈 프레젠테이션 (View)

- 보스 2페이즈 전환(`BossPhase2`): 페이즈 게이트 연출은 문서 A §3.4(A4)와 공유.
- 최종 보스 소환(Monarch 호위): 문서 A §3.1(A1) 인트로 문법 재사용.
- 보스 월드공간 말풍선: LobbyView `BossDisplayName` 동일 소스.

## §5. 런 준비·준비 교체

### 5.1 런 모델
- 로비 → 준비 화면(`?mode=campaign&stage=1` 시작 시) → 체인 순회.
- 준비 화면에서: 로스터 캐릭터·장비 슬롯·동료 선택. 결정적 확정(확정 시 스냅샷 고정).

### 5.2 준비 교체 계약
- `RunPreparationSnapshot` + `IRunPreparationSnapshot` additive read seam(심 시임) 사용.
- `PreparationOfferKind { None, Stat, SkillRune, GuardianResonance }` — 선택 UI는 View.
- 확정 전 변경 자유, 확정 후 불변. 리셋 시 준비 상태 초기화.

### 5.3 스테이지 진입 연출
- 문서 A §4.3(§C 전환 컷신)로 체인 노드 사이 연결. 스킵 가능.

## §6. 터레인 레인 T (vfx-terrain)

| 항목 | 내용 |
|---|---|
| T-a | 구역별 터레인 시각 분화(다리/성소/왕좌) — 프리팹 배리언트 또는 라이트 조절 |
| T-b | 유물 제단·흑요석 기둥·잿불 분출구 가시성 강화(발광·전조) |
| 게이트 | `tools/blender/convert_terrain.py` 분할 유지(절차적 분할 금지) — 저작 시점 유지 |

- 터레인 조명/발광은 View 전용 라이트·머티리얼 — 지오메트리 불변.

## §S. 심 변경 격리 (AMENDMENT #4 후보)

| ID | 항목 | 심 변경 | 게이트 |
|---|---|---|---|
| S5 | 경계 리터럴 1곳 | 스테이지 경계 상수 교체 | AMENDMENT #4 + 해시 불변 테스트(기존 1..3 결과 보존) |

- **S5는 해시 불변** — 기존 스테이지 결과가 바뀌면 안 된다. 리터럴 치환만, 논리 변경 금지.
- S5 미승인 시: 체인 테이블을 View 레이어(준비 데이터)에 두고 심 경계 미변경으로 유지.

## §7. 구현 순서·검증

1. 체인 테이블 데이터화(§T1 8곳) — EditMode 테스트로 체인 순회 검증.
2. 준비 화면 교체(§5) — RunPreparationSnapshot 계약.
3. 룸·보스 프레젠테이션(A1/A4 공유, §4).
4. 터레인 레인 T(§6).
5. §S5 경계 — 승인 후.
2. 준비 화면 교체(§5) — RunPreparationSnapshot 계약.
3. 룸·보스 프레젠테이션(A1/A4 공유, §4).
4. 터레인 레인 T(§6).
5. §S5 경계 — 승인 후.
**§T2 CampaignStore 마이그레이션**: 클리어 영속을 개별 bool 3개 → `ClearedMask` 비트마스크 1필드로 승격(카탈로그 순서 비트). 레거시 로드 호환 — 구 3 bool을 비트 0/2/4로 매핑, `unlocked(s)=cleared(prereq(s))||cleared(s)`로 소급 잠금 없음. 단일 라이터 경로 유지. COST S / RISK 세이브 손상 — 구 필드 읽되 쓰기는 신규만, 마이그레이션 EditMode 테스트 필수.
**§T4 자산 생성 계획 (CLAUDE.md §3 도구 고정)**: 키 아트/액센트 텍스처(≤1024)는 `gti --dry-run` 선행 후 생성 + `docs/provenance/` 기록. OBJ 191행 전부 `disposition=delete` → retained GLB 32종 전량 채택(보스 바디 3·플레이어 1·추가 릭 2·프롭 2·터레인 8·스테이지 VFX 6·모션 코어 10). 예산 게이트: 현 빌드 80.7/120 MB(여유 39.3) — GLB 증분은 T5 스모크에서 tri/MB 실측, 초과 시 features/props 팩부터 데시메이트. 프롭 킷은 Blender MCP 대화형 반복 후 배치 스크립트 역기록.
**§T5 검증**: EditMode 신규 (a) 카탈로그 6엔트리 무결성(앵커·prereq 무순환·terrainId 프리팹), (b) 해저드 override 2규칙 분리(전 종류 비중첩 `거리>r합` + 기둥 쌍만 회피통로 `거리≥r합+2×PlayerPushRadius(=52)`), (c) T2 마이그레이션, (d) 보상 분포 `anchorStageIndex%3 == {0,0,1,1,2,2}`. 스모크: 6스테이지 순차 클리어 1회 통주 + 해금 체인 확인.

**완료 조건**: EditMode 66/66 유지 + 캠페인 10 + 핵앤슬래시 31 전부 패스 + 체인 순회 결정론 검증. 심 diff 0(S5 미승인 시).
