# HongT — 스테이지 커버리지 결함과 Unity WebGL 헤드리스 입력 (2026-08-11)

AMENDMENT #17b에서 나온, 다음 세션·다른 프로젝트에서도 재사용 가능한 결론.

## 1. 오버라이드는 합성을 우회한다 — 커버리지가 조용히 반토막 난다

[OBSERVED] `Assets/Scripts/View/StageCatalog.cs`의 스테이지 엔트리는
`HazardOverride ?? 심 앵커` 규칙을 쓴다. #17은 심의 앵커 6개에
`DungeonLayoutSpec.Compose`를 걸었는데, 플레이 가능한 스테이지는 9개이고 그중
4개가 오버라이드를 갖는다. 오버라이드는 앵커를 **확장이 아니라 대체**하므로
그 4개는 인테리어를 하나도 못 받았다 — #17이 완료로 보고된 상태에서 9개 중 4개.

[INFERENCE] 일반화: **"A ?? B" 형태의 라우팅이 있는 곳에서 B만 고치면 A 경로는
조용히 남는다.** 결과가 "일부 스테이지에 기능 없음"이라는 형태로 나타나므로,
전수 스윕을 돌리기 전에는 보이지 않는다. 기능을 추가할 때 물어야 하는 질문은
"이 표를 고쳤나"가 아니라 **"이 사실의 출처가 몇 개인가"**다(CLAUDE.md §4i·§4v).

[OBSERVED] 부수 효과가 하나 더 있었다. 오버라이드 표가 뷰(UnityEngine 참조)에
있었기 때문에, 심을 스탠드얼론 dotnet으로 컴파일해 재는 하네스(§4w)가 9개 중
4개를 **볼 수 없었다.** 게이트를 6/9에서 재고 전수라고 보고하는 상태였다.
→ 해결: 해저드 표를 `Assets/Scripts/Sim/StageOverrideHazards.cs`(심)로 이동.
**측정 도구의 커버리지가 대상의 커버리지보다 좁으면 그 차집합은 영원히 사각이다.**

## 2. 핀치를 만들 수 있는 것은 "막는 것"뿐이다

[OBSERVED] 레이아웃 생성기가 기믹 클리어런스로 회랑 폭(150 px)을 요구했는데,
그 대상에 **통과 가능한** 분출구(반경 90)가 포함돼 있었다. 분출구 4개짜리
스테이지에서 커버 격자 16개가 전멸했고, 3개 스테이지가 인테리어 0이 됐다.

[INFERENCE] 핀치(끼임)는 **두 고체 사이**에서만 생긴다. 통과 가능한 해저드
옆의 커버는 두 번째 벽이 없으므로 회랑을 만들지 않는다. 바닥 해저드에 필요한
것은 회랑이 아니라 **탈출 폭** = 액터 지름(2 × push radius = 52). 150은 다른
문제에서 빌려온 상수였다.

[OBSERVED] 같은 규칙이 `WithoutPinchPoints`에 복제돼 있어 완화가 반쪽만 먹었다.
클리어런스 테스트를 통과한 4조각이 핀치 단계에서 전부 죽었다. → 두 곳이
`ClearanceFor(kind)` 하나를 공유하게 함.

## 3. `WithoutLayoutBlockers` 같은 "스트립 규칙"은 모든 미러가 호출해야 한다

[OBSERVED] #17은 확장 경계(735×390) 런에만 인테리어를 남기고 동결 런에서는
벗기도록 `WithoutLayoutBlockers`를 만들었고, "모든 미러가 같은 규칙을 쓰라"며
public으로 공개했다. 그런데 `CinderSim(in CampaignConfig)` (v0.1 호환 경로,
동결 아레나 520×270)는 그것을 호출하지 않고 표를 그대로 받았다.

[OBSERVED] 증상은 원인의 모양으로 나타나지 않았다: 샤드 수집 봇이 300초 안에
죽기 시작했고, 실패 메시지는 "봇이 죽었다"였지 "스트립 규칙이 빠졌다"가
아니었다. 그리고 골든이 조용히 움직여 **재핀돼 있었다.**

[OBSERVED] 생성자에서 고치자 `classic-cinder-span`과 `classic-echo-throne`이
#17 이전 골든 행을 **정수·소수까지 그대로** 재현했다. `classic-abyss-chancel`만
기둥 3개가 있어 #17의 조향(별개 결정) 때문에 남았다.

[INFERENCE] `AreUnchanged`라는 이름의 테스트가 드리프트를 냈을 때, **재핀은
마지막 수단이지 첫 수단이 아니다.** 이전 값과 대조하면 그 드리프트가 의도인지
누수인지 대개 몇 분 안에 갈린다.

## 4. Unity WebGL을 헤드리스 브라우저에서 조작하는 법 (재사용도 높음)

[OBSERVED] Playwright chromium(`--use-gl=swiftshader --enable-unsafe-swiftshader`)
에서 이 프로젝트의 WebGL 빌드는 **정상 로드·렌더된다.** 앞서 "헤드리스에서는
WebGL 컨텍스트가 없다"고 판단했던 것은 **도구의 한계였지 대상의 성질이
아니었다**(CLAUDE.md §4z).

[OBSERVED] 입력에는 두 가지 함정이 있고 둘 다 실측으로만 드러났다:

1. **`page.mouse.click()`은 무시된다.** Unity가 프레임 단위로 입력을 샘플링해
   순간 down/up이 삼켜진다. `move → 대기 → 2px 이동 → down → 240ms 유지 → up`
   이어야 등록된다. 같은 좌표가 click으로는 아무 일도 없었고 유지형으로는
   패널이 열렸다.
2. **첫 프레스는 포커스로 소비된다.** 빈 바닥에 한 번 버려야 두 번째부터
   UI에 닿는다.

[OBSERVED] 이 둘을 모르면 **스크립트 로그가 거짓말한다.** 45초 캡처가
"공격 44회 · 스킬 30회"를 기록했는데 게임은 45초 내내 로비에 있었다 — 로그는
스크립트가 **보낸 키**를 셌지 게임이 한 일을 세지 않았다.

[OBSERVED] 스테이지는 프롤로그 클리어 전까지 잠겨 있다. 세이브 파서가 additive
라 부분 JSON이 유효한 세이브다:
`localStorage['abyssal-lantern:unity:campaign'] = '{"clearedMask":0,"prologueDone":true}'`
(`page.addInitScript`로 로드 전에 심는다.)

이 조합으로 §4c의 "브라우저 스모크"를 **로비가 아니라 스테이지 안에서**
자동 수행할 수 있다. 프로브: 세션 스크래치의 `stage_smoke.mjs`.

## 5. 설계를 뒤집기 전에 그것이 결함인지 설계인지 확인하라

[OBSERVED] "기믹에 충돌을 붙여라"는 지시를 받고 제단·방벽주를 고체화했다.
둘 다 근거를 보고 되돌렸다:

- **제단**: 반경 70은 채널 **판정 범위**이고, 저장소가 `AssertRadialClearance`
  독스트링에 "altars are pure channel discs"로 명시해 둔 설계다. 코어 24로
  고체화하니 플레이어가 y 604가 아닌 639에 서서 서명된 테스트 3건이 깨졌다.
- **방벽주**: `ash-march`가 회랑 불변식("the pylon body never blocks")을
  서명했고, 하필 **전진하는 잿벽**이 있는 스테이지다. 막히는 것이 곧 죽음인 곳.

[INFERENCE] 콜라이더가 없는 것이 곧 콜라이더를 빠뜨린 것은 아니다. 서명된
테스트가 그 자리에 있다면 그것은 대개 **결정의 화석**이다. 지시가 광범위할수록
(“기믹 충돌”) 각 대상이 그 지시에 해당하는지를 개별로 물어야 한다.

## 실측 요약 (dotnet 하네스, 9스테이지 전수)

```
인테리어 8/9   (cinder-sluice는 조류 밴드가 y 260..580·628..948을 덮어
                격자 전 행이 밀림 밴드 안 — 밴드 안 고체는 스톨로 기각된 규칙)
연결 100% · 섬 1개(바닥 미절단) · 우회비 1.11~1.53 (게이트 2.5)
EditMode 891 중 890 통과 · 실패 0 · 스킵 1
배포 검증: 라이브 = 로컬 72,354,629 bytes
```

관련: `Assets/Scripts/Sim/DungeonLayoutSpec.cs`,
`Assets/Scripts/Sim/StageOverrideHazards.cs`,
`Assets/Scripts/Sim/CinderSim.cs` (`SolidRadius`),
`Assets/Tests/EditMode/DungeonGoldenDigestTests.cs`.
