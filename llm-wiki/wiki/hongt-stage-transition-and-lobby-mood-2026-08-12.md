# HongT: 스테이지 전환 연출 2결함 + 로비 무드/그림자 (2026-08-12)

사용자 제보: "각 스테이지 넘어가는 씬부분에 알맞는 이미지와 비디오가 적절히
동작하지않고있어. 밀려서 나오거나 들어갈때마다 고정된 이미지 씬이나오는데" +
후속 지시 "로비에도 배경과 그림자가 나올수있도록".

## 결함 1 — "들어갈 때마다 고정된 이미지"

[OBSERVED] `GameDirector.StageCutsceneSprite`의 폴백이 1단계
(`<generic>-<stageId>` → `<generic>`)뿐이었다. 리소스 실측
(`Assets/Resources/Scenes/`): `scene-stage-entry-<id>.png`는 9스테이지 전부
존재, `scene-transition-<id>`는 0장, `scene-boss-entry-<id>`는 ash-march
1장. 그래서 Ember Rest 연속 진입은 전부 같은 `scene-transition.png`,
BossMonarch 스테이지(echo-throne/ash-verdict/ash-march 중 앞 둘)는 전부 같은
`scene-boss-entry.png`로 붕괴 — 9장의 스테이지 키아트가 사장돼 있었다.

수리: 3단계 체인 `<generic>-<id>` → `scene-stage-entry-<id>` → `<generic>`
(`GameDirector.cs` StageCutsceneSprite). **컨텍스트 프레임이 있으면 그것이
이기고, 없으면 스테이지 키아트가 generic을 이긴다.** 캐시 키는 generic+id
유지. 새 프레임 파일 추가만으로 옵트인되는 계약은 그대로다.

일반화: **폴백 체인을 설계할 때는 각 단계의 리소스가 실제로 몇 장
존재하는지 세라.** "per-X 프레임이 있으면 쓴다"는 체인은 per-X가 0장이면
분기 전체가 데드코드다 — 이 경우 그 데드 분기가 2년치 아트 계획처럼 보였다.

## 결함 2 — "비디오가 밀려서 나온다"

[OBSERVED] 막(act) 시네마틱 래치 `_pendingActReel`은 클리어 시점에
세팅되고 `EnterLobby`와 Ember Rest continue에서만 소비됐다. 승리 카드에는
로비를 거치지 않는 경로가 셋 있다(재도전, 직접 출격, 지금-플레이). 그
경로를 타면 릴이 래치에 남아 **몇 스테이지 뒤 무관한 로비 진입에서**
재생됐다.

수리: `StartDungeon` 최상단에
`if (TryPlayPendingActCinematic(stageId, preparation)) return;` — 모든 던전
진입이 지나는 단일 초크포인트. 릴 완료 콜백(`ContinueAfterActCinematic`)이
래치가 비워진 채 재진입하므로 재귀 없음. `ContinueFromEmberRest`의 중복
게이트는 제거. `TryPlayPendingActCinematic`은 `_intro == null`일 때 래치를
소비하지 않도록 재정렬(전달 불가한 비트를 버리지 않는다).

일반화: **"다음 안전한 전환에서 재생"이라는 래치는 전환 집합이 닫혀 있어야
한다.** 전환 경로를 하나라도 빠뜨리면 비트가 시간축에서 미끄러진다. 전환이
전부 지나는 초크포인트(여기서는 StartDungeon)에 게이트를 두면 집합이
구조적으로 닫힌다.

## 로비 배경 + 그림자

[OBSERVED] 로비는 `SetStageTerrain(null)`/`SetStageEnvironment(null)`로
맨 코트였고, `StageMood.Apply`(디렉셔널 키/필 + `StageShadowPolicy` 리스)의
유일한 호출처가 스테이지 진입이라 로비에는 그림자가 구조적으로 없었다.

수리(`GameDirector.cs` + `LobbyStaging.cs`):
- `CurrentCampaignStageId()` — StartPlayNow의 규칙(첫 미클리어 해금
  스테이지, 없으면 마지막 해금)을 추출해 로비 드레싱과 지금-플레이가 같은
  스테이지를 가리키게 함. 프롤로그 전(전부 잠김)엔 null → `_selectedStage`
  의 cinder-span 플로어 유지.
- EnterLobby: 그 스테이지의 terrain 프리팹 + `SetLobbyMood(stageId)` =
  `StageMood.Apply(id, ArenaHalfWidth, ArenaHalfHeight)` (동결 아레나
  반경). EnvironmentBuilder/PostFxGate/플레이필드는 던전 전용 그대로
  (AMENDMENT #12/#15, §V4).
- 로비 무드 수명: `SetStageEnvironment` 최상단에서 `ClearLobbyMood()` —
  모든 모드 전환이 이 함수를 지나므로 로비 키라이트가 스테이지 리그와
  공존할 수 없다. StageMood 루트는 항상 정확히 1개 (테스트 핀).
- `LobbyStaging.Compose`가 클론 렌더러를 `StageShadowPolicy.
  TryConfigureCaster`로 캐스터 레이어에 올림 — 키라이트는
  `shadowRenderingLayers`가 별도라 미설정 클론은 그림자를 만들지 않는다.

주의: **`StageMood.Apply`는 StageCatalog에 없는 id에 null을 반환한다.**
"lobby" 같은 리터럴 id로 부르면 조용히 no-op — 로비 무드는 반드시 실존
스테이지 id로 건다.

## 검증

- EditMode 1008/1008 (신규 4: 스프라이트 체인 전수, 직접 출격 릴 게이트,
  로비 무드/지형/캐스터, 프롤로그 전 로비 플로어).
- §4m 변이 증명: 소스만 되돌린 뮤테이션 런에서 정확히 신규 4건만 RED
  (`test-results-222542.xml`), 복원 후 전부 GREEN(`test-results-222654.xml`).
- §4c 브라우저 스모크: 로비 3상태(신규 세이브=ember 판, clearedMask 3=
  chancel 석판, clearedMask 15=echo-throne 창백한 법정 + MONARCH 디오라마
  + 캐스트 그림자), abyss-chancel 진입 프레임(보라 대성당 키아트),
  echo-throne 진입 프레임(왕좌 키아트 — generic 보스 프레임이 아님),
  콘솔 에러 0.
- 로비 디오라마 보스가 진행에 따라 바뀌는 것은 이 수리의 부수 효과다:
  `_selectedStage`는 원래 아무도 재할당하지 않는 상수였다.
