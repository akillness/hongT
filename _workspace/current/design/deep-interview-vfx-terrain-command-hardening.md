# Deep Interview Spec: VFX 심화 · 던전 오브젝트 구성 · 키 난독화 · 프롭 외형

## Metadata
- Interview ID: `vfx-terrain-command-hardening-20260805`
- Rounds: 1 (5문항 분기) + interjection 1 (파티클 도입 확정)
- Type: brownfield (라이브 서비스 위 증분)
- Generated: 2026-08-05
- Status: PASSED — 실행 승인 대기
- Survey: 저장소 실측 완료 (아래 §Survey Findings — 전부 [OBSERVED])

## Survey Findings (질문의 근거가 된 실측)

| # | 사실 | 위치 |
|---|---|---|
| S1 | cinder-span 프리팹은 **이미 94개 개별 MeshRenderer** (feature-001..040 + prop-001..050 + floor) — "atlas 융합"이 아니라 자식으로 전부 살아 있음 | `Resources/Terrain/terrain-cinder-span.prefab` 실측 GameObjects=95 |
| S2 | abyss-chancel/echo-throne 프리팹은 **바닥 슬랩만** (GO 6/7) — feature/object가 임포트된 적 없음 | 동 프리팹 실측 |
| S3 | 진짜 융합 메시는 그 2스테이지의 **retained** `textured-cleaned.glb` (1노드 1메시) | 소스 GLB 청크 실측 (1,1) |
| S4 | 더 풍부한 분해 소스(abyss object-cleaned 72개, echo feature-billboards 39개+background 35개)는 **manifest delete** — 사용 불가. echo 후보는 빌보드(2D 평면)라 55° 카메라에서 종잇장 | manifest 전수 + GLB 노드 census |
| S5 | 변환 파이프라인은 join 없음 — 분리는 저작 시점 유지가 원칙 (§3 절차적 분할 금지) | `tools/blender/convert_terrain.py` L161-167 |
| S6 | Gemini 키는 PlayerPrefs **평문** (`al:gemini-key`) — 기기당 1회 입력, 빌드 미포함은 이미 계약 | `GeminiCommandClient.cs` |
| S7 | 프롭 외형: 캐릭터당 머티리얼 1개·전신 틴트(P2)까지 — 파츠별 표면 없음, 본 소켓은 스윙 트레일이 이미 RightHand 본 사용 중(선례) | `ActorView.EnableSwingTrail` |
| S8 | VFX 현행: 스코치·스트릭·스파크·히트스톱·셰이크·이중 충격파 (LineRenderer/쿼드 문법, ParticleSystem 미사용) | `VfxDirector.cs` |

## Interview Transcript

### Round 1 (5문항)
1. **던전 구성** → **승인분 + 융합 터레인 Blender 분할**: cinder-span 90오브젝트 활용 + retained 융합 GLB(S3)를 Blender headless로 서브오브젝트 분할.
2. **API 키** → **저장 난독화 (기기 파생 AES)**: "암호화"가 아닌 "로컬 저장 난독화"로 정직 표기. 정적 배포에 서버가 없으므로 클라이언트 암호화는 난독화임을 사용자 인지.
3. **프롭 외형** → **본 소켓 프롭 부착 (무기/랜턴/클록 메시)**: 최대 체감안. 자산 비용 M-L 감수.
4. **VFX 연출** → **전부 단계 적용**: 시전 동기화 → 벤트 fill → URP 포스트 순서, 각 게이트 통과 시 다음.
5. **스킬 이펙트** → **원소별 임팩트 차별화**.

### Interjection
- **"파티클시스템도 도입해"** → 5번과 병합: 원소별 임팩트를 **풀링된 ParticleSystem 레이어**로 구현 (LineRenderer 문법과 병행, 대체 아님).

## Goal

라이브 서비스(main=gh-pages 동기) 위에 4개 레인을 증분 적용한다: (T) 개별 오브젝트 기반 던전 드레싱 구성, (K) 키 저장 난독화, (P) 랭크 티어 본 소켓 프롭, (V) 시전 동기화·벤트 fill·원소별 파티클 임팩트·URP 포스트 순차 적용.

## Lane T — 던전 오브젝트 구성 (스테이지별 상이한 실태 기준)

- **T-a. cinder-span 배치 시스템 (자산 작업 0)**: 프리팹의 94자식(S1)은 이미 개별 접근 가능. `StageCatalog`에 **드레싱 테이블** 추가: `{ objectName, position, rotationY, scale }[]` — 논리 스테이지(조합 S2/S4/S6 포함)별로 자식을 활성/재배치. **배치 대상은 `-feature-*`/`-prop-*` 접두사만** — `-slab-*`(바닥 판)·`-apron-*`(경계 판)은 전투 평면 자체이므로 고정 지면으로 불변(움직이면 아레나가 깨진다). 결정론: 테이블은 정적 데이터, RNG 없음. 비보행 계약(§T3) 유지 — 배치물은 combat plane 밖 또는 기존 hazard 좌표와 비충돌(T5(b) 규칙 재사용).
- **T-b. abyss-chancel/echo-throne Blender 분할**: retained `textured-cleaned.glb`(S3)를 `tools/blender/split_terrain_objects.py`(신규)로 연결성 기반 서브오브젝트 분할 → 명명(`<stage>-part-NNN`) → GLB 재익스포트 → 기존 TerrainImportPipeline 경로로 임포트. **저작 시점 분리**(S5 계약). delete 자산(S4) 불사용. 분할 산출물은 `docs/provenance/` 기록 + `git tag -f pre-terrain-split-<date>`.
- **T-c. 프리팹 자식 → 드레싱 테이블 소비자**: `GameDirector.SetStageTerrain` 후 카탈로그 드레싱 패스 1회 실행 (프레임당 작업 0).
- **인수**: S2/S4/S6 조합 스테이지가 시각적으로 구분되는 드레싱 세트를 갖는다; EditMode에 드레싱 테이블 무결성 테스트(오브젝트명 존재·hazard 비충돌); 60fps 유지.

## Lane K — 키 저장 난독화

- `GeminiCommandClient`에 XOR→AES 마이그레이션: `SystemInfo.deviceUniqueIdentifier` 파생 키로 AES-CBC, PlayerPrefs에는 base64 암호문. 로드 실패(기기 변경/WebGL identifier 불안정) 시 평문 폴백 후 재암호화 저장.
- **정직 표기 계약**: UI 문구 "이 기기에만 난독화 저장" — "암호화"·"안전" 문구 금지. 빌드 내 키 내장 금지 불변.
- WebGL 주의: `deviceUniqueIdentifier`가 브라우저별 가변일 수 있음 — 실패 폴백이 기능 상실로 이어지지 않게(재입력 토스트).
- **인수**: PlayerPrefs 원문에 `AIza` 평문 부재; 재시작 후 키 유지; 복호 실패 시 정중한 재입력 안내.

## Lane P — 본 소켓 프롭 (랭크 티어 외형)

- 소켓: RightHand(무기)/LeftHand(랜턴)/Chest(클록) — 스윙 트레일 선례(S7) 재사용. `ActorView.AttachEquipProps(w,l,c)`: 티어 구간(T0-1 없음 / T2-3 기본 / T4-5 상급)별 프롭 프리팹 활성.
- 자산: 티어당 3슬롯 × 2단계 = 6프롭. 소스: retained prop 2종 + `gti`→Blender 신규 4종 (≤800 tri each, §T4 예산 내). 프로비넌스 기록.
- 폴백: 본 조회 실패(비휴머노이드) 시 무프롭 — P2 전신 틴트가 하한 보장.
- **인수**: 던전 중 랭크업 시 다음 방 진입 시점(또는 EquipDropped 즉시 — 구현 재량)에 프롭 갱신; 조인트 게이트(RUNTIME_ANIMATION_CONTRACT §3) 통과; 캐릭터 합계 ≤25k tri 유지.

## Lane V — VFX 단계 적용 (사용자: 전부, 순차 게이트)

- **V1. 시전 동기화** (C2 개조): Attack/skill 액션 프레임에 손 본 수렴 글로우 0.12s → 방출. 판정 불변(장식 선행).
- **V2. 벤트 fill 임박도**: 텔레그래프 링 내부 fill이 CycleT에 비례해 차오름 — 기존 HazardView 문법 확장. 리서치 1순위 항목.
- **V3. 원소별 파티클 임팩트 (+interjection)**: **풀링 ParticleSystem 4종**(원소당 1, `Emit(count)` 무할당) — 볼트=보라 관통 잔광, 파동=녹색 틱 리플(0.5s 공명), 노바=엠버 낙하 파편, 에이기스=시안 흡수 플래시. maxParticles 상한, 머티리얼 원소당 1개 고정(MakeUnlit 시드 계약), reduced-motion 시 count 절반.
- **V4. URP 포스트 (블룸/비네트)**: 마지막 — WebGL p95 16.7ms 게이트 실측 선행, 초과 시 quality tier로 강등 또는 컷.
- **인수**: 각 단계 EditMode 초록 + 데스크톱 스모크 스크린샷; V4는 프로파일 수치 첨부 없이 PASS 불가.

## 우선순위 (한 사이클 1레인)

**T-a**(자산 0·체감 큼) → **V2+V3**(파티클 도입 포함) → **K**(작음) → **P**(자산 파이프라인) → **T-b**(Blender 분할) → **V1** → **V4**(게이트 최후).

## Non-Goals
- delete 마킹 소스 사용(S4) — manifest 개정 전 금지.
- echo-throne 빌보드의 근경 배치 — 원경 드레싱 전용.
- 런타임 메시 분할 — 저작 시점 분리만(S5).
- "암호화로 안전해졌다"는 표기 — 난독화의 정직한 한계 유지.
