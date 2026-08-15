# HongT — 목표 단계 추적기 + 지형 툰 전환 배포 (2026-08-12, 후보 34710f1)

강신진 교수님 피드백 2차분 적용 + 사용자 지시 2건(툰 전면 적용, "이미지
덧씌워진 느낌" 제거)을 한 릴리스로 출하한 기록. 라이브:
<https://akillness.github.io/hongT/> (gh-pages `7a478b3`, tree `914c0f2b`).

## 1. "무얼 해야 할지 모르겠다"의 답은 상시 지문이 아니라 단계 추적기다 [OBSERVED]

목표 칩(`HudView.SyncRoomObjective`)은 룸의 지문("다리를 건너오는 전열을
끊고…")을 런 내내 보여줬다. 지문은 목적지를 말하지만 **지금 할 일**을 말하지
않는다. 교수님 지시 "단계별로 한개씩 목적 주면서 진행"을 그대로 구조로 옮겼다:

```
웨이브 중 (남은 > 0)   목표 • 적을 처치하라 — 남은 N
필드 클리어            목표 • {룸 지문}
보스 생존              최종 목표 • {룸 지문}  (호박색)
```

- `남은 = LivingEnemies + PendingSpawns` (`ISimSnapshot` 기존 표면 둘의 합).
  화면에 있는 적만 세면 스폰 스태거 때문에 수가 **올라가기도** 한다 — 대기
  스폰을 합치면 킬로만 내려가서 모든 처치가 눈에 보이는 진행이 된다.
- 보스 박자는 잔몹 수를 **무시**한다. 보스 웨이브의 추가 스폰이 최종 목표를
  "잔몹 처치"로 강등시키면 안 된다.
- 시그니처는 additive(`remaining = -1` 기본값 = 카운트 없음): 기존 2-인자
  호출 6곳(테스트 포함)이 지문-온리 동작을 그대로 유지한다. 심 개정 0.
- 파일: `Assets/Scripts/View/HudView.cs` (SyncRoomObjective),
  `Assets/Scripts/View/GameView.cs:858-865` (호출),
  `Assets/Tests/EditMode/RoomObjectiveTests.cs` §step tracker (신규 4).

## 2. 의도된 Unlit 예외는 아트 디렉션이 바뀌면 뒤집힌다 [OBSERVED]

`TerrainImportPipeline` 헤더는 "Unlit on purpose"였다 — PBR 시절에는 옳았다
(구운 라이팅을 URP/Lit이 이중조명). 시안 02 툰 전환 후에는 **그 예외 자체가
결함이 됐다**: 언릿 사진체 플레이트가 셀 밴딩 키트 옆에 앉은 것이 사용자가
말한 "이미지 덧씌워진 느낌"의 실체다. 같은 결론의 선례가 저장소 안에 이미
있었다 — VfxDirector 기믹 바디 주석("언릿 오버라이드는 툰 전환을 조용히
되돌린다").

- **"이중조명" 반론은 밴딩 아래에서 소멸한다.** ToonLit의 밴드는 라이트
  밴드당 **상수 곱**이라 포스터리제이션이지 두 번째 그라디언트가 아니다.
  구운 알베도 디테일은 살아남고 스테이지 무드 라이팅만 얹힌다.
- 지형 .mat 18장 + kit-stone.mat의 m_Shader를 `CinderCourt/ToonLit`
  (guid `966269d468b9e4a96aa9f55d9c2f7511`)로 전환. 화면 실측:
  남측 에이프런 밴드 mean|Δ| 3.59 (87.6→84.0 luma) — 원경 암석은 fogEnd
  22.5 뒤라 설계상 거의 안 움직인다(Δ0.65).
- **생성기 정렬이 절반이다**: `TerrainImportPipeline`·`CharacterImportPipeline`
  둘 다 ToonLit-우선으로. 892768d가 캐릭터 재질만 바꾸고 생성기를 안 바꿔서
  다음 FBX 재임포트가 캐스트 전체를 PBR로 되돌릴 상태였다 — **직렬화 자산을
  바꿨으면 그 자산을 재생성하는 생성기도 같은 커밋에서 바꿔라** (§4o의
  자산판).

## 3. 선언된 제외 — 다음 사이클이 "고치면" 안 되는 것들 [OBSERVED]

| 대상 | 이유 | 근거 위치 |
|---|---|---|
| equip 프롭 12장 (URP/Lit) | fine/basic 등급 구분이 `_EmissionColor`인데 ToonLit에는 에미션 항이 없다. 전환하면 등급 판독 소멸 | `PropImportPipeline.BandMaterial` 독스트링 |
| CourtBackdrop / VoidFloor (URP/Unlit) | **던전 표면이다, 로비가 아니다** (첫 판은 "로비 키아트"로 오기했고 여기서 정정). 제외 근거는 측정된 미도달: VoidFloor는 프레임의 0.25%만 렌더하고 grain·tone 패스가 지배 색상 버킷을 0.0pt 움직였다(`CameraRig.cs:81-83`, 마젠타 베이크 실측) — 텍스처/톤 작업이 닿지 않는 표면이다. 배포 프레임 시임 실측: 최악 1.55x (결함 기록 4x, 수정 전 2.17x, 목표 ~1.6x) — 툰 전환 후에도 시임 보정 유효 | `SceneBuilder.cs:139-236`, `CameraRig.cs:81-83`, `tools/qa/measure_outskirt_seam.py` |

프롭을 툰으로 옮기려면 셰이더에 에미션 항을 먼저 넣어야 한다 — 재질 스왑이
아니라 셰이더 수정이 전제다.

시임 후속 판정(2026-08-12, 배포 프레임): SceneBuilder의 VoidFloor 톤은 PBR
에이프런 휘도(48.34)에 맞춰 보정됐고 툰 전환이 그 변수를 움직였으므로 재측정
대상이었다. 실측 4밴드 최악 1.55x — 목표 대역 안. **툰 지형 옆에서도 외곽
어둠은 절벽이 아니라 그라디언트로 읽힌다.** VoidFloorTexture가 Toon/이 아닌
Env/ 세트를 가리키는 것은 사실이지만, 시임이 목표 안이고 표면이 측정상
미도달이므로 SceneBuilder.Build 재실행(씬 재생성 + 커밋)을 정당화할 결함이
없다. 다음 사이클이 이 수치를 넘는 시임을 재면 그때가 교체 시점이다.

## 4. 릴리스 게이트 실무 메모 [OBSERVED]

- **중단된 릴리스의 동결 증거는 지우지 말고 아카이브한다**: `snapshot-clean`이
  `refusing to overwrite frozen artifact`로 거부하므로
  `stage-character-shadows-4358a499-halted/`로 `mv` 후 새 증거 세트 생성.
- `outsideInputAllowList`는 이전 릴리스 프로비넌스에서 복사해 시작하는 것이
  빠르다 (`.gh-pages-seal-<sha>/release-build-provenance.json`).
- probeHashes(§5 규격): baseline == candidate
  (`9c6f5d70…`) — 골든 다이제스트 파일 무변경 = 심 거동 무변경의 서명.
  이번 릴리스는 뷰-온리라는 주장의 기계 검증 가능한 형태.
- 시퀀스 전체가 이번에는 무충돌로 완주 — pre/post 스냅샷 둘 다 클린. 직전
  후보 4358a499는 정확히 이 지점(post)에서 동시 세션 편집에 걸려 중단됐다.

## 5. 상충 판정은 디스크가 이긴다 [OBSERVED]

세션 중 외부 리뷰 채널이 "지형 .mat가 여전히 URP/Unlit guid를 갖고 있다"고
세 차례 차단 판정을 냈다. 실측은 반대였다: `966269d4…`는
`CinderToonLit.shader.meta`의 guid이고 URP/Unlit은 `650dd952…`
(`Library/PackageCache/...Shaders/Unlit.shader.meta`). 리뷰는 .meta를 열지
않고 guid 정체를 단정했다가 철회했다. **셰이더 guid 주장은
`.shader.meta`와 PackageCache 대조가 1차 판정**이고, 캐릭터 12장(892768d,
검증 완료 출하본)이 같은 guid를 쓴다는 것이 교차 증거였다.
