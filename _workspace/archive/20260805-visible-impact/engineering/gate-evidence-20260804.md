# Gate Evidence — 2026-08-04 (커밋 가능한 전사본)

`.gitignore` L21 `*.log` 전역 무시로 원본 로그는 커밋 불가 — 스펙이 인용하는 핵심 수치를 여기 전사한다. 원본은 로컬 `_workspace/current/engineering/unity-logs/`.

## EditMode 테스트 (커밋됨 — XML 원본)

- `unity-logs/test-results-002618.xml` [OBSERVED]: `result="Passed" total="73" passed="73" failed="0"`
- `unity-logs/lantern-reaver-prefab-tests-final.xml` [OBSERVED]: `result="Passed" total="2" passed="2"`

## WebGL 빌드 (원본 build-002752.log — gitignored, 전사)

```
[BuildWebGL] result=Succeeded size=80678252 errors=0 warnings=0 time=00:00:16.6826610
```

- [OBSERVED] BuildScript.cs L46 출력. 80.7 MB ≤ 120 MB (CLAUDE.md §1 예산 통과).
- 게이트 확인 문자열은 `result=Succeeded`로 고정할 것 — "Build succeeded"는 출력되지 않는다.

## 보스 리스킨 (원본 reskin/broken-court-monarch-boss.log — gitignored, 전사)

```
RESKIN OK model.glb: tris 41355->25000, heatOrphans 36, filled 36, rigidResidual 0, badWeights 0, removedBones 2
Info: 80141 vertex weights limited
Info: Applied modifier was not first, result may not be as expected  (2건)
```

- 수치 리포트 `reskin/broken-court-monarch-boss.json`(커밋됨): `heightOvershoot 1.221`, `scaleMode span`, mesh 1.863 m vs skeleton 1.731 m.
- achilles-visual-overhaul-spec §L1 진단 사다리의 [OBSERVED] 근거.

## index.html 상대경로 계약

- [OBSERVED] `build-webgl/index.html`에 루트 절대경로(`/Build`·`/assets`·`/TemplateData`) 0건 — CLAUDE.md §0 통과.
