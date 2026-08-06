# Context: Cinder Court 스킬 VFX 강화

## Workflow Context

플레이어는 아이소메트릭(프롤로그는 26° 사이드뷰 직교) 카메라에서 최대 20마리 동시 교전을 치른다. 스킬 창은 짧다 — 콤보 스윙 0.30 s, 대시 0.22 s, 노바 쿨 6.5 s. 이 시간 안에 "무엇이 발동했는가 / 어디까지 닿는가 / 지금 안전한가"가 읽혀야 한다.

현재 `VfxDirector`가 쓰는 원시 어휘는 네 가지뿐이다 (`Assets/Scripts/View/VfxDirector.cs`, direct page retrieval — 저장소 내 코드 직접 확인):

| 원시 | 구현 | 사용처 |
|---|---|---|
| 팽창 링 | `LineRenderer` 28세그먼트 loop | 노바, 대시, 콤보 피니셔, 레벨업, 추출, 보스 페이즈, 볼트, 워드, 파일런 |
| 지속 링 | `LineRenderer` 상시 | 묘지 파동(3 s), 시체 마커, 웨이브 경고 |
| 2점 선 | `LineRenderer` positionCount=2 | 균열 화살 스트릭, 추출 빔, 위협 화살표 |
| 지면 그을음 | Quad 데칼 | 노바, 묘지 파동 |
| 파티클 | `ParticleSystem` ×4 | 볼트 스파크, 펄스 리플, 노바 잔해, 이지스 플래시 |

**핵심 관찰: 9개 이벤트가 같은 `SpawnBurst()` 링을 호출한다.** 색과 반경만 다르다. 대시(0.56, 0.91, 1.0 시안)와 워드(0.56, 0.85, 1.0 시안)는 색 차이가 0.06 — 사실상 같은 이펙트다.

## Affected Users

| Role | Responsibility | Skill Level |
|------|----------------|-------------|
| 던전 플레이어 | 4스킬+대시를 군중 속에서 0.3 s 단위로 판단 | 중~상 (기믹 시간표를 학습함) |
| 프롤로그 플레이어 | 기본 공격만으로 전투 문법 첫 학습 | 초심자 (여기서 이탈하면 전부 잃음) |
| View 레인 유지보수자 | `VfxDirector` 코드 생성 전용 유지, 에셋 의존 0 | 상 (심/뷰 경계 계약 준수 필요) |
| WebGL 배포 | 총 빌드 ≤120 MB, 텍스처 ≤1024 | — (CLAUDE.md §1 하드 제약) |

## Current Workarounds

1. **색상으로만 구분** — 6스킬이 링 하나를 공유하고 팔레트로 정체성을 낸다. Hades가 검증한 방식이지만(보온별 색 코딩), Hades는 색과 **실루엣을 함께** 바꾼다. 여기는 색만 바뀐다.
2. **반경으로 위력 표현** — 노바 250, 묘지 190. 크기는 읽히지만 "종류"는 안 읽힌다.
3. **지면 그을음으로 접지** — 아이소메트릭 가독성 대응으로 이미 올바른 선택(공중 글로우보다 지면 형상이 우선). 노바/펄스에만 적용됨.
4. **파티클 4종을 요소별로 분리** — V3에서 추가됨. 방향은 맞으나 링이 주 실루엣이라 파티클이 보조로 묻힌다.
5. **`ViewPrefs.ReducedMotion`으로 파티클 수 절반** — 접근성 폴백은 이미 있음.

## Adjacent Problems

- **히트스톱은 이미 있고 VFX와 분리돼 있다** — `GameView.ApplyTimeScale()`이 킬 40 ms / 피니셔 70 ms를 `timeScale 0.05`로 처리. 강렬함의 절반은 이미 구현돼 있는데 VFX 타임라인과 동기화되어 있지 않다.
- **애니메이션 리타이밍이 방금 끝났다** — 직전 커밋에서 `attack`이 클립의 24%만 보이던 문제를 speed-fit으로 해결. 스킬 VFX도 같은 종류의 "약속 미이행" 문제다.
- **6개 클립이 MISCAST 상태** — `attack3` 10.0×, `cast` 9.0× 등. VFX를 강화해도 그 밑의 포즈가 블러면 합이 안 맞는다.
- **`_stageClearFlash`만 스프라이트를 가진다** — 나머지 Filled 이미지는 방금 고쳤지만, VFX 머티리얼 쪽에도 동일한 "코드 생성 기본값이 조용히 틀린" 패턴이 있을 수 있다.

## User Voices

- "VFX must be in constant communication with designers to ensure they align with gameplay mechanics" — GDC 2013 Julian Love, Diablo 3 (indexed snippet, confidence medium — 다수 2차 출처가 이 발표를 업계 표준으로 인용)
- "visual soup, where the sheer volume of effects made it impossible to see enemies or projectiles" — Hades 얼리액세스 플레이어 피드백을 Supergiant가 수용한 사례 (indexed snippet, confidence medium)
- "there is a constant tension between adding visual flair and maintaining the crisp readability required for high-level ARPG play" — Last Epoch 플레이어 커뮤니티 (indexed snippet, confidence medium)
- "Treat WebGL mobile targets as ~2015-era hardware. Memory is your tightest bottleneck" — Unity WebGL 최적화 가이드 (indexed snippet, confidence high — 복수 Unity 공식 문서가 동일 취지)
- "particles dissipate immediately after their communication task is finished so they don't linger and become noise" — VFX Apprentice (indexed snippet, confidence high)
