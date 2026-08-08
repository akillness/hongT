# 스킬 VFX 모드 커버리지 — 훈련 던전 미배선 결함

run-id 20260806-skill-vfx · View 레인 · 인수인계 문서
근거: 코드 직접 확인(`[OBSERVED]`), 서베이 `.survey/skill-vfx-intensity/`(validator PASS)

---

## 요약

스킬 실루엣 VFX 5종(§S1)을 `VfxDirector`에 넣었고 **캠페인 던전에서는 5/5 동작**한다.
**훈련 던전(`GameMode.Training`)에서는 2/5만 동작**한다 — VFX 결함이 아니라
심이 해당 스킬을 캐스팅하지 않기 때문이다. VFX는 발행되지 않은 이벤트를 그릴 수 없다.

이 문서는 그 미배선을 기록한다. **소유자는 AMENDMENT #10(훈련장) 레인이다.**
View 레인은 심 계약을 건드리지 않으므로 여기서 고치지 않았다.

---

## [OBSERVED] 모드별 스킬 커버리지

| 스킬 | 심 이벤트 | §S1 실루엣 | 캠페인 | 훈련 |
|---|---|---|---|---|
| Q 균열 화살 | `BoltCast` | 크랙 팬 4갈래 | O | **X** |
| E 묘지 파동 | `PulseCast` | 수직 융기 10 | O | **X** |
| R 잿불 노바 | `NovaCast` | 크랙 팬 8갈래 | O | O |
| F 공허 방패 | `WardCast` | 수축 껍질 8 | O | O |
| Shift 질주 | `DashUsed` | 잔상 2줄 | O | **X** |

## [OBSERVED] 원인

`Assets/Scripts/Sim/CinderSim.cs:863` — 스킬 분기가 `_dungeon` 단독 게이트:

```csharp
// §2.2/§2.3: the dungeon replaces the arena kit with dash + four skills.
if (_dungeon)
{
    CastDungeonSkills(in input);
    return;
}
// 아래는 아레나 2스킬(Nova/Ward)만 읽는다
```

`_dungeon`은 `CinderSim.cs:308`에서 `config.Mode == GameMode.Dungeon`으로만 설정된다.
`GameMode.Training`은 여기 포함되지 않아 **아레나 경로로 낙하**한다.

반면 `GameDirector.cs:299`는 훈련 진입 시 입력 프로파일을 던전으로 건다:

```csharp
_input.Mode = InputAdapter.Profile.Dungeon;   // full kit: you practise with your tools
```

주석이 선언한 의도("full kit")와 심의 실제 동작이 어긋나 있다.

### 키 매핑 실측 (`InputAdapter.cs:72-83` × `CinderSim.CastSkills`)

| 키 | 입력이 발행 | 심(아레나 분기) | 결과 |
|---|---|---|---|
| Q | `_boltLatch` | 안 읽음 | 먹통 |
| E | `_pulseLatch` | 안 읽음 | 먹통 |
| R | `_novaLatch` | `NovaQueued` ✓ | 동작 |
| F | `_wardLatch` | `WardQueued` ✓ | 동작 |
| Shift | `_dashLatch` | 안 읽음 | 먹통 |

## [OBSERVED] 딸린 결함 2건

### 1. HUD가 잘못된 카드를 띄운다

`GameView.cs:146`이 `_isDungeon = (Mode == Dungeon)`이라 훈련에서
`Hud.EnableDungeonUi()`(`GameView.cs:161` 블록)가 호출되지 않는다.
→ 던전 4카드 대신 **아레나 2카드**가 남는다.
→ Q 카드 라벨은 "잿불 노바"인데 Q는 `Bolt`를 보내고 그건 무시된다. **라벨과 동작 불일치.**

### 2. 시련의 기믹이 렌더되지 않는다

`Vfx.SyncHazards(_sim.Hazards)`도 같은 `_isDungeon` 블록 안(`GameView.cs:519-521`)이다.
훈련장은 설계상 **기믹 1종 전용 시련**(`design/training-and-surge-spec.md` §2.2 —
불씨/해류/방벽/행진/증언)인데, 연습 대상인 기믹이 화면에 없다.

심은 훈련에서 해저드를 정상 구동한다(`CinderSim.cs:818` — `if ((_campaign || _training))`).
**심은 맞고 View만 못 따라간 상태다.**

## [OBSERVED] 배선 진행도

`GameView._isTraining`은 이미 존재하나(`GameView.cs:147`) 현재 소비처가
`Hud.SyncSurge` 한 곳뿐이다. AMENDMENT #10이 배선 중이며 미완이다.

---

## 수정 시 필요한 것 (제안, 소유 레인 판단)

1. **심**: 스킬 분기를 `_dungeon || _training`으로 확장. 단
   `CinderSim.cs`의 `_dungeon` 게이트는 총 8곳이며 각각 의미가 다르다 —
   XP(2294), 엘리트 스폰(2511), 보스 페이즈(830), 성장 오퍼(1222)까지
   훈련에 열면 "60초·스폰 없음·비화폐" 설계와 충돌한다.
   **스킬(863)·쿨다운(1539)·콤보(1632) 세 곳만 여는 것이 설계 의도로 보인다.**
2. **View**: `EnableDungeonUi`와 `SyncHazards`를 `_isDungeon || _isTraining`으로.
   단 XP바·보스바·추출링은 훈련에 무의미하므로 전부 열면 안 된다.
3. **골든**: 훈련은 신규 모드라 기존 골든에 영향 없음. 단 스킬을 열면
   `DungeonGoldenDigestTests`가 아닌 훈련 전용 골든이 필요하다.

## 배선 후 View 레인이 할 일

**없다.** §S1 VFX는 `SimEvents` 5종에 걸려 있으므로, 심이 훈련에서
`BoltCast`/`PulseCast`/`DashUsed`를 발행하는 순간 **코드 변경 없이** 5/5 동작한다.
회귀 가드는 `Assets/Tests/EditMode/SkillShapeVocabularyTests.cs`가 이미 보유
(5 테스트, `OnEvents`를 직접 구동하므로 모드와 무관하게 형상을 고정).

## 미검증 항목 — 전부 해소됨 (2026-08-06 15:40)

에디터 점유가 풀린 뒤 실측했다. 상세: `qa/skill-vfx-frame-cost.md`.

- ~~실기 화면 확인 없음~~ → **확인됨.** WebGL 빌드를 브라우저에서 플레이해
  융기 크라운(링 테두리 수직 조각 10)과 크랙 팬(방사형 호박색 팔)을 육안 확인.
  게이지는 체력 135→128→37→0으로 매 단계 렌더.
- ~~WebGL 프레임타임 미측정~~ → **측정됨.** draw call은 실제로 지침을
  초과한다(던전 유휴 146 → 스킬 중첩 182, +29~35). 그러나 프레임 비용은
  CPU 20x 스로틀(20 fps)에서도 측정 분산 이하다. 최적화 불필요.
- **남는 것**: GPU fill rate는 CDP 스로틀이 재현하지 않으므로 여전히 미측정.
  shard가 0.05 폭 선이라 overdraw 기여는 기존 quad·파티클보다 작다는 것은
  `[INFERENCE]`이지 측정이 아니다. 실제 저사양 기기 검증도 미실시.
