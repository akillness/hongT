# 릴리스 중단 — 후보 `4358a499` (2026-08-12 21:38)

배포를 요청받아 릴리스 게이트를 완주했으나 **동시 세션의 소스 편집**이
빌드 시퀀스 도중에 들어와 중단했다. CLAUDE.md §5: *"다른 세션의 변경 복원·폐기·
강제 덮어쓰기 금지. 충돌 시 중단·기록·명시 해결."* 그쪽 파일은 하나도 건드리지
않았다.

## 무엇이 잡았나 — pre/post 스냅샷 쌍이 처음으로 일을 했다

직전 릴리스(`713d95e9`)에서 이 쌍은 빌드가 아니라 프로비넌스 생성만 감쌌고,
그래서 인계문서에 **"다음 릴리스는 stage 1 전에 pre, stage 2 후에 post"**라고
적었다. 이번엔 그 순서를 지켰고, **바로 그 post 스냅샷이 침입을 잡았다.**

```
pre  (stage 1 직전)   tracked 0 · untracked 0 · ignored 0     — 깨끗
post (stage 2 직후)   FATAL: cannot freeze dirty source snapshot
                      GameView.cs · HudView.cs · RoomObjectiveTests.cs
```

순서를 안 지켰으면 이 릴리스는 **출처를 증명할 수 없는 바이트를 동결한 채로
나갔을 것이다.** 규칙 하나가 한 사이클 만에 값을 했다.

## 타임라인 [OBSERVED]

```
21:30:39  Development 빌드 종료
21:35:01  Release 빌드 시작
21:37:23  HudView.cs            수정   <- 다른 세션
21:37:37  GameView.cs           수정   <- 다른 세션
21:37:45  build-webgl.data.unityweb 기록
21:37:54  build-webgl/index.html    기록
21:38:03  RoomObjectiveTests.cs 수정   <- 다른 세션
```

Release 빌드는 2분 31.7초 걸렸고 스크립트 컴파일은 시작부(≈21:35)에 일어나므로
**편집이 이 바이트에 들어갔을 가능성은 낮다.** 그러나 낮다는 것은 증명이
아니고, 프로비넌스가 주장하는 것이 정확히 "이 바이트가 어느 소스에서 나왔는가"다.
**증명할 수 없는 것을 동결하지 않는다** — 그래서 중단이다.

## 동시 세션이 하는 일 (읽기만 함, 손대지 않음)

```
Assets/Art/Terrain/Materials/*.mat        19개
Assets/Resources/Environment/kit-stone.mat
Assets/Editor/TerrainImportPipeline.cs
Assets/Scripts/View/GameView.cs · HudView.cs
Assets/Tests/EditMode/RoomObjectiveTests.cs   (+61줄, 신규 테스트)
```

`.mat` 변경의 내용은 전부 같다 — `m_Shader`를 **CinderToonLit
(`966269d468b9e4a96aa9f55d9c2f7511`)로 전환**한다.

즉 **저쪽은 내가 못 푼 문제를 작업 중이다.** 직전 커밋(`4358a49`)에 미해결로
적어둔 것: 아레나 경계 링이 창백한 평면으로 남고, 바닥(quad/`_floorMaterial`)은
바뀌는데 링(cube/`_stoneMaterial`)은 안 바뀐다. 지형·키트 머티리얼이 아직
비-툰 셰이더를 쓰고 있었다면 그것이 설명이고, 저 전환이 수정이다.

**내가 기각한 세 가설**(바인딩 누락 / 툰 텍스처 평면성 / SRP Batcher의 MPB 무시)
은 전부 `_stoneMaterial` 경로만 봤고 **터레인·키트 머티리얼의 셰이더 자체는
안 봤다.** 그쪽을 봤어야 했다.

## 완료된 것 — 재사용 가능

후보 `4358a499`에 대해 증거 6종이 전부 PASS로 동결돼 있다:

| evidenceId | mode | 결과 |
|---|---|---|
| shadow-focused-editmode | Source | PASS |
| full-editmode | Source | PASS |
| release-build | Source | PASS |
| development-build | Development | PASS |
| browser-shadow-desktop | Development | PASS · SNR 117.94 · footprint 0.369% |
| browser-shadow-mobile | Development | PASS · SNR 20.08 · footprint 0.252% |

`candidate-clean-pre-4358a499.json`도 동결돼 있다(깨끗).
**post만 없다.** 이 증거들은 `4358a499`의 것이므로, 동시 세션 작업이 커밋되면
후보 SHA가 바뀌고 **전부 다시 만들어야 한다.**

## 재개 조건

1. 동시 세션이 자기 작업을 커밋·푸시한다 (그쪽 소유).
2. `git status --porcelain -- Assets Packages ProjectSettings web tools/deploy`가 빈다.
3. 새 HEAD로 게이트를 처음부터: **pre 스냅샷 → stage 1 → 브라우저 증거 2종 →
   stage 2 → post 스냅샷 → 증거 → 프로비넌스 → 씰 → 배포.**

## 현재 라이브

`713d95e9` — 원버튼 진입·타격 범례·토스트 드웰이 포함된 빌드다. 사용자에게
보이는 화면에 회귀는 없다. 이번에 못 나간 것은 **툰 텍스처 14장 재촬영**뿐이다.
