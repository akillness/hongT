# Release Notes

## 프레젠테이션 사이클 12 — 막 시네마틱 · 콤보 모션 · 캠페인 밸런스 · 모바일 · 2026-08-10

사용자 지시: *"3개 스테이지 끝나면 넘어갈때 시네마틱영상 적용"* →
*"3d 모션 공격, 스킬 이상하지않게 개선"* → *"밸런스도 검토하고 재조정"* →
*"모바일에서도 잘 플레이가능하도록"*.

### 모션 — 콤보가 4배속으로 재생되고 있었다

`ActorView.ResolvePoseSpeed`가 스윙 클립을 `HackSpec.ComboSwing` 창에 욱여넣고
`MaxPoseSpeed 4`로 클램프합니다. **클램프가 함정입니다** — 조용하고, 4배를 넘게
필요한 클립은 빨리 재생되는 게 아니라 **끝나지 않습니다.**

| 액션 | 클립 | 창 | 배속 | 보이는 비율 |
|---|---|---|---|---|
| attack2 | 1.79 s | 0.30 s | 4.00x | 93% |
| attack3 | 3.83 s | 0.42 s | 4.00x | **43%** |

지난 사이클에 "프리앰블이 대체된 클립보다 짧으니 무트림"이라고 적었는데, 그건
*이 테이크가 더 나은가*의 비교지 *들어맞는가*의 비교가 아니었습니다. 콤보
인덱스별 창에 맞춰 트림했습니다 — `attack2` 8..15(0.97x), `attack3`
27..37(0.99x), 각 창은 심의 활성 구간에 중심을 맞췄습니다.

`SwingClipsPlayAtAReadableSpeed`로 고정. 상한은 4가 아니라 **1.5**입니다: 4는
엔진이 포기하는 지점이지 사람이 스윙을 읽는 지점이 아닙니다.

### 막 시네마틱 — 3스테이지 경계

9스테이지 3막이므로 `CatalogIndex` 2/5/8에서 막이 끝납니다. id 목록이 아니라
**인덱스에서 유도**했습니다 — 10번째 스테이지가 붙어도 패턴이 이어집니다.

`EnterLobby`에서 재생합니다. `StageCleared` 시점엔 승리 카드가 떠 있고 씬이
살아있습니다. `EnterLobby`는 `_game.EndRun()` 뒤 — 부트 릴이 통과해야 했던 것과
같은 "아래에서 아무것도 안 돈다" 시험입니다. 사망·포기도 여기로 합류하므로
도착이 아니라 **클리어가 래치**합니다.

### 밸런스 — 캠페인이 뒤로 갈수록 쉬워지고 있었다

플레이어 화력은 런을 거치며 복리로 오르는데(메타 스탯 10, 장비 랭크 5) 웨이브
예산은 스테이지를 모릅니다. 예산은 웨이브와 DDA 밴드로만 오르고 **둘 다 런
시작마다 리셋**됩니다.

```
완성 빌드    피해 1.69x · 체력 2.20x  ->  3.72x
적 부담      스테이지 0 3612 -> 8 9010 =  2.49x
스테이지 8 상대 난이도                =  0.67x   (단조 감소)
```

DDA로는 못 고칩니다. +2 천장은 모든 스테이지를 똑같이 1.250배 해서 곡선을
**평행이동만** 시킵니다(0.67 → 0.80, 여전히 처짐).

FROZEN은 건드리지 않았습니다. `CampaignTypes.cs`는 헤더가 FROZEN CONTRACT이고
웨이브 표는 골든이 바이트 고정합니다. 대신 던전 전용·게이트된
`DungeonProgressionSpec`에 앵커 인덱스 항(90 permille/앵커)을 넣었고, **게이트가
판정**했습니다 — 886/886, 골든 재핀 0.

실측 앵커별 웨이브1 예산: **100 · 109 · 118 · 127 · 136 · 145**(=1.45x). 스테이지
8은 2.49 × 1.45 = 3.61x 대 플레이어 3.72x → **0.97 상대**. 아레나는
`EffectiveBudget(wave, band)` 오버로드로 바이트 동일함을 12웨이브에 걸쳐
어서션했습니다.

### 모바일 — 전투 HUD는 한 번도 44px를 재본 적이 없었다

로비는 cycle-9에 감사받고 동결 부채표까지 있는데, 전투 HUD 테스트는 "존재하고
활성이고 탭을 받는다"까지만 물었습니다. **탭 가능과 엄지로 닿는다는 다른
질문**입니다.

| 컨트롤 | 크기 | @0.488 | @0.4383(SE2) |
|---|---|---|---|
| 조이스틱 | 260u | 126.9 | 114.0 |
| 타격 | 110u | 53.7 | 48.2 |
| 질주 | 96u | 46.8 | **42.1 미달** |

질주를 102u로 올렸습니다(49.8 / 44.7). 피벗이 (1,0)이라 위로만 자라 타격과의
12u 간격은 그대로고 겹침 테스트도 초록입니다.

### 스펙 원장 — 출하본과 어긋나 있었다

#13/#14/#15/#16이 "DRAFT — 서명 대기"로 남아 있었는데 `GameView.cs:91`이
`Everything`을 출하하므로 **넷 다 라이브에서 켜져 있습니다.** "구현·배포 완료,
서명 대기"로 고쳤습니다(서명 자체는 오퍼레이터 몫이라 frozen으로 올리지
않았습니다). 작업 중 #15를 "유일하게 꺼져 있다"고 잘못 적었다가 출하 코드를
읽고 정정했습니다.

### 검증

EditMode **886/886**(+8). 변이 증명 4건: 큐 전진 · attack3 트림 · 막 경계 2→3 ·
스테이지 항 permille 0. WebGL 84MB. 라이브: 콤보 3타가 각각 다른 자세로 읽히고
오류 0, 막 시네마틱 3편 HTTP 200.

## 프레젠테이션 사이클 11 — 전투 모션 6종 교체 + 첫 실행 3비트 인트로 · 2026-08-10

사용자 지시: *"skill, 모션 등 vfx 힉스필드로 전면개선해줘. 모션도 싹다
바꿔주고"* → *"비디오는 컨셉에 대한 인트로, 컷씬, 네러이션 씬등 생성하고
추가해야해"*.

### 모션 — 6종 교체

Higgsfield `3d_rigging`이 실제로 Unity 휴머노이드 클립을 만든다. 다만
`animation_action_id`가 enum 없는 불투명 정수라 20개를 샘플링해 라이브러리를
지도로 만들었다 — 전투는 **190~260 대역**에 있다.

| 액션 | id | 새 클립 | 대체된 것 |
|---|---|---|---|
| attack | 200 | `Punch_Combo_1` | Standing Melee Attack Horizontal |
| attack2 | 195 | `Right_Upper_Hook_from_Guard` | Hook Punch |
| attack3 | 205 | `Punch_Combo_5` | Standing Melee Combo Attack Ver. 2 |
| critical | 240 | `Thrust_Slash` | Illegal Elbow Punch |
| defence | 220 | `Shield_Push_Left` | Body Block |
| bighit | 190 | `Knock_Down_1` | Receive Uppercut To The Face |

**측정이 뒤집은 두 가지.** 22개짜리 Mixamo→Unity 리네임 표를 먼저 썼는데,
`ClipAvatarProbe`로 물어보니 생성 리그가 **리네임 없이 isHuman True 15/15**로
매핑됐다(기존 14클립도 Mixamo 원본 그대로 통과 중이었다). 표는 죽은 코드라
지웠다. 루트모션도 벗기지 않는다 — `ReimportClips`가 이미 in-place 투영을
건다.

**진짜 결함은 씬 범위였다.** 평범한 glb→fbx 변환이 2.23초 액션을 10.38초
클립으로 뽑았다. Blender 기본 씬 범위 `1-250`이 그대로 export된 것이다.
키프레임에서 범위를 읽게 고쳤다.

**트림 재측정.** 심이 잡는 포즈는 0.4167s(=클립 24fps 기준 10프레임), 활성
구간은 0.167~0.333s다. `attack` 13..23, `critical` 58..68 — 둘 다 속도
1.00x로 활성 구간에 안착. Thrust Slash는 3초 중 86.7%가 윈드업이라 트림
없이는 4x로 클램프되고도 윈드업만 보인다.

### 드리프트 2건

`ClipWindowProbe`와 `ClipTrimFitTests`가 각자 클립 표 **사본**을 들고 있어서,
교체 후에도 대체된 클립을 계속 쟀다. 트림 테스트는 구 `attack` 창을
살아있는 것으로 보고했다. 둘 다 이제 `CharacterImportPipeline.ClipFileAt`을
읽는다.

### 영상 — 첫 실행 3비트

로고 → 전제(등불 법정) → 위협(보스). `IntroVideoView`가 경로를 받게 하고
`PlaySequence`를 추가했다: `OnFinished`는 시퀀스 전체에 1회, 스킵은 다음
클립이 아니라 인트로 전체를 버린다. 재방문자는 localStorage 플래그로 브랜드
릴만 본다.

**부트가 유일한 자리다.** 스테이지 컷씬은 `_game.Begin` 뒤에 뜨고 보스 비트는
전투 중이라, 둘 다 5초 오버레이가 곧 **안 보이는 5초의 플레이**다.

### VFX — 두 번 시도, 두 번 되돌림

t2v는 "위에서 본 용암"을 **하늘의 달**로 해석했다(프레임마다 밝은 원반).
시트 셀을 시드로 준 i2v는 표면에는 붙었지만 프레임 간 변화가 거의 없어
출하본보다 정지에 가까웠다. **출하 시트가 낫다.**
`tools/video/fx_sheet_from_video.py`는 계약(4×4, 크로스페이드, `.meta` 보존)을
담아 남긴다.

### 검증

EditMode **874/874**. 변이 증명 2건: 큐 전진 제거 → 시퀀스 테스트 RED,
트림 13..23 → 16..28 → 트림 테스트 RED. WebGL 82MB(상한 120). 라이브
스모크: 컨셉 릴 재생 확인, 시퀀스 완주 후 로비 진입, 콘솔 오류 0.

### 사이클 11b — 감시자 내레이션 + AOE 그을음 데칼 · 2026-08-10

**내레이션.** 부트 3비트가 게임에서 유일하게 목소리 없는 스토리 비트였다.
`IntroVideoView.Beat(클립, 내레이션)`로 시퀀스를 확장하고 캡션 행을 추가했다.
문안은 `StoryCatalog.StageStart`와 같은 문법 — 감시자, 2인칭, 비트당 한 문장.
로고에는 캡션을 달지 않는다(워드마크에 자막을 다는 셈이다).

오디오가 아니라 텍스트인 이유: WebGL 브라우저는 오디오 트랙이 있는 미디어의
자동재생을 사용자 제스처 전까지 막는다. 부트 릴은 클릭 이전에 시작하므로
내레이션 트랙은 Safari에서 조용히 버려진다. 캡션은 항상 도착한다.

**그을음 데칼.** `SpawnScorch`가 단색 쿼드였고 노바·펄스·파일런 파괴 셋이
전부 그것을 쓴다. 텍스처 하나로 세 곳이 같이 올라간다. 계약은
`TerrainFlipbook`과 같은 분리 — 텍스처가 형태(동심 화상 링·균열·용융 코어),
호출별 틴트가 정체성(노바 잉걸갈색 / 펄스 짙은 녹색).

알파를 한 번 틀렸고 **측정이 잡았다.** 처음엔 휘도를 알파로 썼는데, 원본이
대부분 검게 탄 암석이라 어두운 틴트를 곱하고 나니 화상 영역이
0.19/0.08/0.03 불투명도가 됐다 — 코어만 남고 화면에선 안 보였다. 그림을 본 게
아니라 PNG를 샘플링해 출하 틴트를 적용해서 쟀다. 고친 근거: **알파는
커버리지고 커버리지는 반경이다.** 0.62R까지 꽉 차고 림에서 0으로(쿼드의 사각
모서리가 절대 안 나온다), 휘도는 변조만 한다(바닥 0.55). 그을음은 "어두운 곳이
없는 것"이 아니라 어두운 화상이다.

부재-가드는 `SpawnPickupIcon`과 같은 모양이다. `VfxDirector` 헤더가 자산 하드
의존을 금지하므로 텍스처가 없으면 이전 그대로 평평한 디스크로 내려간다. 그
가드 때문에 런타임은 부재를 절대 보고하지 않으므로 `ScorchDecalShips`가 유일한
신고자다 — png를 숨기면 RED.

**검증.** EditMode 877/877. 라이브 첫 실행에서 세 비트 전부 확인: t=13s 컨셉
릴 + 캡션, t=19s 위협 릴 + 캡션, 한글 글리프 정상. 아레나에서 노바 발동 시
노바 링과 균열 부채 안쪽에 검붉은 화상 디스크가 별개로 읽힌다.

**미착수로 남긴 것.** `idle`/`move`/`run`은 `loop: true`라 이음매 없는 루프가
필요하고, 이번에 넣은 6종은 전부 원샷이다. 라이브러리에 로코모션은 있으나
(id 1~100) 루프 품질은 별개 문제라 다음 사이클로 미룬다.

## 프레젠테이션 사이클 10 — 조용히 실패하던 3건 + 연출 자산 · 2026-08-09

사용자 지시: *"리소스 개선작업진행하자, 특히 vfx와 던전구성, 그리고 매시와
모션"* → *"연출용 영상, 음성, 사운드등도 추가해"*. Higgsfield CLI를 새로
프로비저닝해 이미지·TTS 경로를 열었고(§3 표 개정), 그 과정에서 **주장과 구현이
갈라진 결함 3건**이 드러났다.

### 근본 원인이 같았던 3건 — "성공을 보고하는 실패"

| 무엇이 | 얼마나 조용했나 | 원인 |
|---|---|---|
| §V3 파티클 seed가 **한 번도 생성된 적 없음** | 프로젝트 수명 전체. 4개 원소 시스템이 flat-color 폴백으로 돌아 color-over-lifetime이 죽어 있었다 | `Shader.Find("...Particles Unlit")` — 실제 이름은 `Particles/Unlit`(슬래시). null 분기가 `return true`("장식이 빌드를 깨면 안 된다")라 경고만 남고 넘어갔다 |
| `show`/`cast` 클립 적합이 **주석에만 존재** | 수개월. Mutant Roaring 5.42 s가 1.1 s 창에 통째로 들어가 로어의 ~20%만 재생되고 잘렸다 | ClipTrims 행 없음 · 컨트롤러 `m_Speed: 1` · `PoseValueForClip`에 행 없음 |
| witness-well과 echo-throne이 **같은 accent** | 두 인접 스테이지의 무드·틴트·조명·플립북이 한 값에 붕괴 | 둘 다 `Color(0.45,0.78,1)` |

공통 구조는 **폴백이 성공을 보고한다**는 것이다. 셋 다 테스트가 통과하고 있었는데,
틀려서가 아니라 정답과 오답이 같아지는 좌표계에서 재고 있었기 때문이다(§4m의
4·5번째 사례). 분석: `llm-wiki/wiki/concepts/generator-fallback-that-reports-success.md`.

### VFX
- **파티클 seed 실체화**: `Assets/Resources/Materials/particle-additive-seed.mat`를
  URP `Particles/Unlit` **GUID 직접 참조**로 저작해 생성기 의존을 끊었다. 빌드
  로그가 상시 경고 대신 `[MaterialSeeds] particle-additive-seed ready`를 찍는다.
  `RuntimeMaterialSeeds`는 `AssetDatabase.LoadAssetAtPath` 우선 + `Shader.Find`
  폴백으로 고쳤다(배치모드에서 패키지 셰이더가 등록 목록에 없을 수 있다).
- **소프트글로우 스프라이트**: `tools/gen_fx_sprites.py`가 256² 방사 감쇠를
  **수식으로** 생성한다(의존성 없음, 재생성 시 바이트 동일). 이미지 모델을 쓰지
  않은 이유는 이것이 미술이 아니라 수학이기 때문 — 밴딩·비대칭·중심 이탈이 없다.
- **죽음 파티클**: 킬은 가장 잦은 보상 비트인데 유일하게 이펙트가 없었다.
  `EnemyKilled`에 id 링 래치로 1회 방출(일반 8 / 엘리트 16, 모션 약함 시 절반).
  FadeTime 값 창을 쓰지 않은 이유: 60 Hz에서 이중 발화 또는 무발화가 된다.
- **워드 셸 fresnel 셰이더**(신규 `Assets/Shaders/Vfx-WardFresnel.shader`):
  화면 체류가 가장 긴 방어 비주얼이 기본 Sphere + 평면 알파 0.28이었다. 프레넬
  림으로 교체해 정면은 비치고 실루엣이 빛난다. 만료 경고도 **렌더러 10 Hz
  토글(스트로브)** 에서 셰이더 밝기 펄스로 바꿨고, 진폭은 `player.WardTime <
  0.5f`(심 상태)로 구동하며 모션 약함에서 0이 된다.

### 던전 구성
- **빈 드레싱 2표 채움**(abyss-chancel · echo-throne). 코드에 넣기 전 산술 검증:
  최악 해저드 여유 +75.2, 최근접 이웃 ≥275, 사분면 2/3/2/2.
  `StageDressingTests.DressedStages`에 등재해 기존 6표와 **같은 무결성 검사**를
  받는다 — 등재하지 않으면 검사받지 않는 표가 된다.
- **accent 분리**: witness-well을 옥빛 `(0.22,0.76,0.66)`으로. 플립북 Ice 밴드
  유지를 실측 확인(floor warmth −0.1495 vs 임계 −0.05). boss tint·시놉시스
  문서·스테이지 진입 아트까지 따라 옮겼다(5/9가 boss tint == accent 진영이고 이
  스테이지가 거기 속했다).
- **스테이지별 무드 표** 9행. 이전에는 key pitch 42/yaw 28·강도 0.55/0.22가 전
  스테이지 공통이라 accent 색만 달랐다. 조명 추가 없음(§E6 ≤4 point 불변).
- **환경 텍스처 이음매 수리 4/18 → 0/18**. 1.28 월드유닛마다 타일링되므로 벽
  전체에 줄이 반복된다. 이미지 모델에 "seamless"를 **지시해도** 1/4만 개선되고
  3/4는 악화됐다(수용 규칙이 막았다) — 이음매는 양식이 아니라 기하라서
  `tools/seamless_env_textures.py`가 오프셋·디램프·랩블렌드 3패스를 모두 시도하고
  **측정상 더 나은 것만** 채택한다.

### 메시 · 모션
- **show/cast 트림**(실측): `Assets/Editor/ClipWindowProbe.cs`가 휴머노이드 리그
  위 `SampleAnimation`으로 피크 프레임을 찾는다. show f8–34(1.083 s, 목표 1.1),
  cast f23–30(0.292 s, 목표 0.30) — 오차 17 ms · 8 ms, speed 1로 자연 재생.
  리타이밍 대신 트림인 이유: 로어에 4.9배가 필요해 `MaxPoseSpeed` 4를 넘는다.
- **반응 클립 4종은 측정 후 의도적 미트림**: hit/bighit preamble 2.2%/5.3%로
  이미 촘촘하고, avoid/defence는 고정 창에 눌리지 않는다. 결정이지 누락이 아니다.
- **리스킨 드라이버 8 → 12 id**: s1/s2/s3는 스켈레톤 입력이 자기 id가 아니라
  shadow-commander라 오버라이드를 추가했다(리포트 `input` 필드에서 복원).
- `Punching.fbx` 삭제(참조 0건, 213 KB LFS), `docs/provenance/motion.json` 신규.

### 연출 영상 · 음성 · 사운드
- **인트로 릴 beat 6 복구**(6.6 → 7.8 s). 2026-08-06에 "피사체가 과일로 읽힌다"고
  컷됐던 비트다. 원인은 `gti`의 codex-cli 프로바이더가 **이미지 입력을 거부**해
  일관성을 텍스트 STYLE 접미사에만 의존한 것이었고, 참조 이미지를 받는
  `nano_banana_flash`로 한 번에 통과했다. 타이틀 록업은 랜턴 발광부와 겹쳐
  `h*0.34 → h*0.18`로 이동(서브라인 밝기 56.8 → 28.2 실측).
- **한국어 VO 8줄**: StoryCatalog 대사를 화자 클래스별 보이스로
  (감시자 Yoona / 보스 Hyunwoo / 워든 Seojun). 키는 `vo-<stageId>-<beat>` —
  비트만으로 키를 잡으면 한 스테이지의 음성이 다른 스테이지 자막 위에서 재생된다.
  전용 AudioSource(피치 지터 금지·풀 미사용), BGM 0.4 더킹, 무스케일 램프,
  `EnterLobby`에서 정지(§4o). **말풍선 hold를 음성 길이로 덮어쓴다** — hold 공식은
  읽기 속도(~17자/초) 기준인데 TTS는 ~7자/초라 6/8이 말풍선 소멸 후에도 말했다.
- **던전킷 9큐**: 그동안 기본 클립의 볼륨 변주("interim contract")였다. 최악은
  BossPhase2 = `cue-gameover` 0.35 — 보스가 강해지는 순간에 **패배음**이 났다.
  전 큐가 `PlayOrFallback`으로 기존 매핑을 폴백으로 유지한다.
- API가 `.mp3` 이름으로 **비압축 PCM WAV**(768 kbps)를 반환해 트랜스코딩했다:
  3,145,536 → 371,810 B(88.2% 감소). SFX 4개는 peak 0.05–0.18로 들리지 않아
  정규화(0.62–0.92, 기존 큐대와 정렬).

### 도구 계약 개정(§3)
VO 행과 연출 스틸 행 추가. **VO는 지시 변경**(2026-08-04 "음성 금지" → 2026-08-09
개정, 스토리 내레이션 한정 — cue-* 효과음은 여전히 vocals 금지), **연출 스틸은
능력 차이**(gti는 참조 이미지 불가). ElevenLabs 키가 HTTP 401이라 SFX/BGM
파이프라인이 실행 불가인 것도 VO가 Higgsfield로 간 직접 원인이다.

### 검증
- EditMode **870/870**(신규 19건), 컴파일 0 에러, WebGL 81 MB(120 MB 캡 내)
- 브라우저 스모크: 부트 · 던전 · 전투 · 사망 4경로, 콘솔 에러 0
- 신규 어서션 4묶음 전부 **GREEN → RED → GREEN 변이 증명**

### 미해결(정직한 이월)
- **ElevenLabs 키 401** — SFX/BGM 재생성 불가. 키 갱신 필요.
- **`reskin_all.sh`는 12 id를 알지만 실행 불가** — `~/orca/Abyssal-Surge`가 Unity로
  재구축되며 `assets/motion`·`assets/mesh`가 사라졌다. 추가한 4개가 아니라
  **원래 8개도** 미스한다. 출하된 FBX가 기록물이다.
- **보스 전용 모션** — 12개 메시가 컨트롤러 하나를 공유해 보스가 잡몹처럼 휘두른다.
  기구는 append-only로 준비돼 있으나 신규 Mixamo 클립이 필요하고 mixamo.com은
  비대화형 다운로드가 없다(Adobe 세션 필요).
- **캐릭터 메시 생성은 Higgsfield로 불가** — tripo/meshy 출력은 미리깅이고
  `CharacterImportPipeline.cs:163`이 humanoid 아바타가 아니면 하드 throw한다.
  정적 소품은 가능하다.

## 난이도 4단계 + 적 그룹 협동 AI + 타격감 개편 · 2026-08-08

리뷰 영상 <https://youtu.be/wbDv6nawEeY> (쿼터뷰 액션 RPG 'Achilles: Legends
Untold' 리뷰) 분석에서 출발했다. 리뷰어가 그 게임에 70점 이상을 주지 못한 이유가
**타격감**이었고, 두 번째 지적이 **보통 난이도의 적이 멍청하다 / 그룹 AI 는
어려움 난이도에서 개선된다**였다. 두 축을 그대로 우리 게임에 적용했다.
분석 원문: `_workspace/current/design/video-review-analysis-amendment11.md`.

### 시뮬레이션 (FROZEN CONTRACT AMENDMENT #11 — SIM_SPEC_HACKSLASH.md §16)
- **난이도 4티어**: 입문(Story) / 보통(Normal) / 어려움(Hard) / 악몽(Nightmare).
  축은 받는 피해 배수(0.65 / 1.00 / 1.35 / 1.70), 적 공격 쿨다운 배수
  (1.22 / 1.00 / 0.84 / 0.70), 동시 공격 인원 상한(2 / 무제한 / 3 / 4),
  그룹 AI(off / off / on / on).
- **적 그룹 협동 AI (어려움 이상)**: 매 틱 사전 패스가 공격 차례를 배분한다.
  차례를 못 받은 적은 플레이어 주위 8슬롯 포위 링(사거리 ×1.55 / ×1.35)의 자기
  슬롯으로 물러나 대기하고, 방금 휘둘러 쿨다운에 들어간 적은 차례를 잃고 링으로
  빠지면서 교대가 만들어진다. 정면이 아닌 적은 거리에 0.75 를 곱해 채점하므로
  첫 타가 측·후방에서 들어온다. RNG 없음, id 기반 타이브레이크로 결정론 유지.
- **보통(Normal)은 값 0** 이라 `default(HackConfig)` 와 기존 모든 초기화가 개정
  이전 시뮬레이션을 그대로 재현한다.

### 뷰
- **타격감(`Assets/Scripts/View/ImpactBudget.cs`)**: 일반 근접 적중에 히트스톱과
  카메라 펀치가 **처음으로** 생겼다(이전에는 처치·콤보 피니셔에만 있었다).
  티어는 Light 0.028 s / Kill 0.045 s / Finisher 0.075 s 로 통일했고, 같은 틱에
  여러 이벤트가 겹치면 가장 무거운 티어 하나로 해소한다. 짧은 요청이 진행 중인 긴
  히트스톱을 깎지 못한다. Light 는 0.14 s 재발동 간격이 있어 군집을 연타해도
  화면이 슬로우모션으로 눌어붙지 않는다. 모션 약함 설정은 기존 게이트를 그대로
  존중한다.
- **난이도 선택 UI**: 로비 → 성소 정비 → 성장 탭 하단의 "난이도" 순환 버튼.
  버튼 라벨의 수치는 `DifficultySpec.For` 에서 직접 읽어 표시하므로 밸런스가
  바뀌어도 화면의 약속이 어긋나지 않는다. `PlayerPrefs` 키 `al:difficulty` 에
  문자열 id 로 저장되며, 키가 없거나 손상되면 조용히 보통으로 이관된다.

### 검증 상태
- **[OBSERVED] 보통 경로 무변경 증명**: 개정 전(git HEAD) 심과 개정 후 심에 동일
  입력을 먹여 arena 5400틱 / prologue 3600틱 / dungeon(cinder-span) 5400틱을
  돌리고 97틱마다 플레이어 좌표·HP·점수·웨이브와 전체 적 좌표/액션/HP 를 덤프한
  153행이 **완전히 동일**했다. 골든 다이제스트 재핀 불필요.
- **[OBSERVED]** 신규 EditMode 테스트 25건 추가
  (`DifficultyGroupAiTests` 8, `ImpactBudgetTests` 8, `DifficultySelectionTests` 9).
  이 중 UnityEngine 비의존 16건을 dotnet 격리 실행으로 **16/16 통과** 확인.
  `dotnet build CinderCourt.Tests.EditMode.csproj` 0 에러.
- **[BLOCKED]** Unity 배치모드 EditMode 전체 스위트는 이번에 실행하지 못했다 —
  다른 세션의 Unity 에디터가 프로젝트를 점유 중이라 배치모드가 거부된다.
  에디터 점유 해제 후 `bash tools/unity_batch.sh tests` 재실행이 필요하다.


## GitHub Pages 배포 — 툴링 경계 가드 + 재임포트 검증 · 2026-08-05

### 변경 (배포 위생 사이클 — 게임플레이 변경 없음)
- **Unity-MCP 에디터 툴링을 수렴 상태로 커밋**: OpenUPM 스코프 레지스트리 +
  `com.ivanmurzak.*` UPM 패키지 11종, 리졸버가 `Assets/Plugins/NuGet`에
  설치한 DLL 42종(17 MB). 전부 에디터 전용 — 아래 가드가 플레이어 유입을
  차단한다.
- **`BuildScript.ExcludeEditorToolingFromWebGl()` 신설**: 리졸버
  (`NuGetPluginConfigurator`)는 도메인 리로드마다 DLL을 `anyPlatform=1`로
  수렴시키므로 손 편집 메타는 유지되지 않는다. 대신 빌드 시점마다
  (1) `UNITY_MCP_READY`를 WebGL 그룹에서 **빌드 스코프로 strip**
  (`finally` 복원 — 빌드가 tracked 설정에 추가 churn을 남기지 않음),
  (2) NuGet DLL 임포터에 `Exclude WebGL` 설정(리졸버 수렴 검사는
  per-platform exclude를 읽지 않으므로 영구 유지).
- **`ProjectSettings.asset` 정직 기록**: `scriptingDefineSymbols`가 `{}` →
  `UNITY_MCP_READY` 19개 그룹으로 변했다. 이는 리졸버가 세션마다 주입하는
  값이라 되돌리면 클린 클론에서 tracked 파일이 영구적으로 dirty해진다 —
  수렴 상태로 커밋하고, WebGL 그룹만 빌드마다 strip되는 구조를 택했다.
- **스테일 텍스처 진단 (직전 세션 이월 항목)**: 배치 임포트 결과
  **재임포트 0건** — Library는 이미 디스크(=HEAD 텍스처)와 일치했고,
  에디터의 구버전 표시는 **열린 에디터의 메모리 캐시**였다. 씬 미저장
  상태로 에디터를 종료(디스크 = 진실 소스)하고 배치 파이프라인으로 재검증.
- `GrowthChoiceSnapshot.cs.meta` 페어링 커밋 (3d599f0가 .cs만 커밋해
  GUID가 임포트마다 재생성될 수 있었다).

### 게이트·배포
- EditMode **225/225 통과** (`test-results-214651.xml`).
- WebGL 빌드 Succeeded, 62,068,944 bytes(디스크 39 MB ≤ 계약 120 MB) —
  `build-214745.log`: strip 라인(604행) + **40 DLL 제외** 라인(2166행),
  플레이어 빌드 로그에 McpPlugin/SignalR 문자열 **0건**.
- gh-pages `73f7c11`, 캐시 `c920df31f01c03eb` — curl 폴링 2회차에 로컬과
  일치. main: `d9b79f2` → `c7c8cfa` → `8c61544`.

### 검증 상태
- **라이브 확인**: 배포 빌드 로비 부팅(에러 배너 없음), 장비 탭에서
  `equip-weapon`(검)·`equip-lantern`(주황 랜턴)·`equip-cloak`(청색 망토)
  3종이 고유 팔레트로 렌더 (`deployed-tooling-equip.png`). ui-button
  플레이트·성장 아이콘·해저드 글리프도 같은 프레임에 렌더.
- **체인 검증 (라이브 육안과 별개)**: 아이콘 *신선도*의 근거는 체인이다 —
  재임포트 0건(Library=디스크) → 그 Library로 빌드 → 라이브 캐시버전
  일치. 스크린샷은 렌더 확인이며 구버전 대비 픽셀 diff가 아니다
  (구버전 대조 캡처가 존재하지 않는다).
- **미확인**: 전투 중 `pickup-ember/flask/relic` 아이콘은 화면 확인 못 함
  (전투 진입 필요, VfxDirector 로드 경로는 기존 배포에서 검증된 경로
  그대로). 에디터 재기동 후 MCP 자동 재연결은 다음 인터랙티브 세션에서
  확인될 항목.

## GitHub Pages 배포 — §K3 스킬 원소색 피격 + 플래시 지속 수정 · 2026-08-05

### 변경 (spec `combat-feel-boss-phase-spec` §K3)
- **원소색 피격 플래시**: 스킬 시전 후 0.4s 창 동안 입은 피해는 그 스킬의
  원소색으로 메시가 번쩍인다 (볼트=보이드 바이올렛 / 파동=그레이브 그린 /
  노바=엠버 / 에이기스=시안). 맞은 쪽이 **무엇에 맞았는지**를 스스로 알린다.
- **색 정의 단일화**: V1 손 글로우가 쓰던 네 색 리터럴이 중복될 뻔했다.
  `GameView.TryElementColor` 하나로 합쳐, 시전자의 손과 피격 메시가 **항상
  같은 색**을 쓰도록 보장한다.
- **보스 틴트 양보 (기존 결함)**: `ApplyBossPresentation`은 매 프레임
  `SyncEnemy` **뒤에** 카탈로그 틴트를 무조건 덮어썼다. 즉 **보스만은 어떤
  피격 플래시도 표시할 수 없었다** — 원소를 읽는 게 가장 중요한 대상인데도.
  플래시가 블록을 점유한 동안 카탈로그 틴트가 양보하도록 수정. 스케일 곱은
  매 프레임 절대 설정이라 조건 없이 유지(생략 시 보스 크기가 튄다).
- **사망 시 플래시 해제 (위 수정이 드러낸 결함)**: 사망 분기는 감쇠 블록보다
  **위에서 return**하므로, 치명타로 죽은 액터의 플래시는 영원히 만료되지
  않는다. 양보 규칙과 겹치면 보스가 페이드 내내 카탈로그 틴트를 잃는다.
  사망 진입 시 플래시를 해제.
- **플래시가 전체 지속시간을 갖도록 수정**: 기존 순서는 **점화 즉시 같은
  프레임에 감쇠**를 태웠다. 100ms 프레임에서 130ms 플래시가 첫 프레임에
  77%를 잃어 타격이 흐릿한 번짐으로 보였다. 점화 프레임은 감쇠하지 않도록
  변경(만료 프레임의 복원 경로는 그대로 유지).

### 게이트·배포
- EditMode **182/182 통과** — 신규 `ElementTintTests` 5종(리터럴 정확도,
  4색 상호 구별성, 비시전 틱 오염 없음, 결정론적 우선순위, 순수성),
  `BossFlashYieldTests` 7종(대여 가드, 피해 시 점화, 회복/무변화 비점화,
  풀 반납 시 해제·기준선 재무장, 사망 시 해제).
- **플래키 테스트 실증 후 근본 수정**: 구조가 동일한 두 테스트가 한 번의
  실행에서 서로 다른 결과를 냈다(`test-results-153032.xml`, 181/182).
  원인은 점화-후-즉시-감쇠 순서 + EditMode에서 `timeScale=0`이
  `Time.deltaTime`을 0으로 만들지 못한다는 점. 테스트를 완화하지 않고
  **제품 코드의 순서를 고쳤다**. 이후 3회 연속 182/182 안정
  (`153327`, 그리고 반복 2회).
- gh-pages `00bad68`, 캐시 `5ea72429f8b2fe84` 라이브 확인.

### 검증 상태
- **라이브 확인**: 배포 빌드에서 볼트 시전 시 피격된 두 적이 **바이올렛**으로,
  같은 프레임의 미피격 적은 **엠버 오렌지**로 렌더
  (`deployed-k3-element-flash.png`, `deployed-k3-element-compare.png`).
  플래시 타이밍 수정 이후 재확인하여 회귀 없음. 런타임 오류 0.
- **미확인**: 보스 틴트 양보의 **화면 확인은 못 했다.** 보스는 웨이브 5에
  등장하는데 헤드리스 드라이버가 도달하지 못한다. 대신 그 수정이 의존하는
  `FlashLive` 계약을 EditMode 7종으로 고정했다.
- **알려진 단순화**: 원소창은 시간 기반이라 0.4s 안의 **근접 타격도** 원소색을
  띤다. 심이 "어떤 피해원이 누구를 때렸는지"를 View에 알려주지 않으므로
  View 전용 레인에서는 이 방식이 유일하다. 창 안의 지배적 피해는 스킬이다.

## GitHub Pages 배포 — §W 웨이브 등장 알림 (수축 링) · 2026-08-05

### 변경 (spec `combat-feel-boss-phase-spec` §W, 신규 스펙 첫 레인)
- **웨이브 도착 텔레그래프**: `WaveStarted` 시 다음 웨이브가 사용할 스폰
  지점에 0.9s 경고 링 4개. 심의 **public 결정론 함수**
  `CinderSim.SpawnPointIndexFor`를 그대로 읽어 배치하므로 View가 스폰 규칙을
  복제하지 않는다. 보스 웨이브는 적색·대형 링.
- **수축 링 문법 (라이브 증거로 교정)**: 1차 구현은 스코치와 같은 **평면
  쿼드**였다. 라이브 프레임 diff 결과 4개가 정확한 위치·타이밍에 뜨는 것은
  확인됐으나, **채워진 사각형이라 무대 바닥 데칼과 구분되지 않았다**
  (`w-telegraph-quad-defect-diff.png` — diff 없이는 식별 불가).
  `Burst`의 `LineRenderer` 링 문법으로 재작성하고, 버스트가 밖으로
  **팽창**하며 "여기서 터졌다"를 읽히는 것과 반대로 **안으로 수축**시켜
  "여기로 온다"를 읽히게 했다. 알파는 닫히는 동안 유지 후 끝에서만 해제.
- **전용 풀 분리**: 웨이브 링 4개를 기존 풀에 넣으면 **살아있는 스킬 시각을
  전부 축출**한다(스코치 풀은 노바+펄스 기준 정확히 4슬롯). 히트 스파크를
  버스트에서 분리한 선례대로 `_waveWarnings` 전용 링 풀 신설. 재작성으로
  단일 호출자가 된 `SpawnScorchIn` 파라미터화는 되돌렸다(불필요한 일반화 제거).
- **셰이크 티어**: `WaveStarted` 0.05/0.15를 우선순위 **최하위**에 배치 —
  보스 웨이브는 `BossSpawned`와 `WaveStarted`를 동시에 올리므로, 순서를
  뒤집으면 0.35 보스 펀치가 약해진다.

### 게이트·배포
- EditMode **170/170 통과** — 신규 `WaveTelegraphTests` 3종(매핑 순수성·범위,
  웨이브 내 4지점 비충돌, 전 스폰 지점 아레나 내부)
  (`unity-logs/test-results-144927.xml`).
- gh-pages `95d48f0`, 캐시 버전 `ebcc6e84b5364db7` 라이브 확인.

### 검증 상태
- **라이브 렌더 확인 완료**: 배포된 빌드에서 웨이브 1→2 전환 순간 캡처.
  4개 링이 서로 다른 스폰 지점에 아이소 압착 윤곽으로 렌더
  (`deployed-w-telegraph-rings.png`). 같은 프레임에서 적 아래 노바 디스크가
  **살아있음** — 전용 풀이 실제로 축출을 막았다는 증거.
- **수축 동작 확인**: 4프레임 시계열에서 링이 단조 축소하며 색·불투명도를
  유지 (`deployed-w-telegraph-contract.png`). 페이드아웃이 아닌 수축임을 확인.
- 라이브 구동 중 런타임 오류 0.

## GitHub Pages 배포 — 프롤로그 26° 측면 카메라 · 2026-08-05

### 변경 (교차 세션 레인 `0674535`)
- 훈련 화면이 90° 탑다운에서 **26° 측면 오쏘 뷰**로 전환 — 사용자 보고
  "평평해서 불편함" 해소. PrologueReveal 스윕을 26→55°로 재앵커하고 시작
  거리를 재계산(3.6 / tan21° = 9.4u)해 오쏘→퍼스펙티브 핸드오프에서 팝이
  생기지 않게 했다.
- 소스만 있고 배포되지 않은 상태였으므로 **소스/라이브 드리프트를 닫는**
  배포 사이클로 처리했다.

### 게이트·배포
- EditMode **167/167 통과** (`unity-logs/test-results-141542.xml`).
- gh-pages `043072c`, 캐시 버전 `2d44354d10a7fa7c` 라이브 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- 신규 프로필 → 점화 훈련 진입: 캐릭터가 탑다운 실루엣이 아니라 **전신
  측면 실루엣**으로 읽히는 레터박스 프레임 확인
  (`_workspace/current/engineering/deployed-prologue-side-camera.png`).
- 이동·타격 8사이클 동안 카메라 팝 없음, 함락 패널까지 프레임 일관.

## GitHub Pages 배포 — 세로 모드 로어 겹침 수정 + 폰트 커버리지 게이트 · 2026-08-05

### 변경 (회고 디자인 후보 소진)
- **세로 모드 로어 겹침 해소**: 폰 티어 던전에서 로어 라인(y=118)이 4카드
  스킬 행(y 54–146) 위에 그대로 얹혀 있었다 (모바일 QA 실측 발견). 던전
  활성 + 폰 티어일 때 로어를 컨트롤 스택 위(y=262+lift)로 이동 — 스피커
  라인(232, 스팬 ≈219–245)과도 비충돌. `SetCampaignSurfacesVisible`이
  `ApplyLayoutTier`를 재호출하도록 배선해 던전↔아레나 전환 시 앵커가
  낡지 않게 했다.
- **폰트 커버리지 게이트 신설** (`FontCoverageTests`): `HudKorean.otf`는
  생성된 **서브셋**이라 새 한글 문자열이 글리프 없이 배포되면 WebGL에서
  OS 폴백 없이 글자가 사라진다 (Lane K "난독화" 토스트가 라이브에서 실제로
  당함). View 소스에서 한글을 재수확해 폰트가 전부 커버하는지 검사하고,
  실패 시 `bash tools/gen_hud_font.sh` 안내를 낸다 — 함정을 게이트로 고정.

### 게이트·배포
- EditMode **167/167 통과** (`unity-logs/test-results-113158.xml`).
- gh-pages `0d3fba5`, 캐시 버전 `efb632aac6ccf3e5` 라이브 확인.

### 배포 후 스모크 (라이브, 390×844 DPR 2, 오류 0)
- Ember Gallery 세로 강하: 위→아래 **로어 / 콤보 핍 / Q·E·R·F 행 / SHIFT**
  순으로 정렬, 겹침 없음 (`_workspace/current/engineering/portrait-lore-fixed.png`).

## GitHub Pages 배포 — Lane V1 시전 동기화 + Lane V4 URP 포스트 (스펙 전 레인 완료) · 2026-08-05

### V1 — 시전 동기화 손 글로우 (`a9bd7ff`, 캐시 `30f826ca74f49b95`)
- `ActorView.FlashCastGlow`: RightHand 본에 원소색 수렴 글로우 0.12s
  (0.16→0.055wu 수축 + 안쪽으로 증휘) — 볼트 보라 / 파동 녹색 / 노바 엠버 /
  에이기스·워드 시안. 심은 즉발 시전이라 글로우는 시전 이벤트에서 시작해
  "방출 직후 잔광"으로 읽힘. 판정 불변(SimEvents 소비 전용), 풀 리셋 정리,
  비휴머노이드 리그 무표시.
- 라이브 스모크: Q 볼트 보라 글로우+스트릭, F 에이기스 시안 글로우+링
  (0.12s 윈도 내 캡처, 오류 0).

### V4 — URP 포스트 (블룸+비네트) (`7669414`, 캐시 `2442aaa76e15f544`)
- **게이트 실측 선행** (스펙 요구): 라이브 빌드 전투 중 rAF 720프레임 —
  p50 8.3 / **p95 10.0 / p99 10.2 ms** (예산 16.7 ms, 여유 ~6.7 ms) → 적용.
- `CinderPostProfile.asset` (직렬화 자산 — WebGL 셰이더 변형 보존): Bloom
  intensity 0.55·threshold 1.05 (진짜 발광체만 블룸)·scatter 0.6, Vignette
  0.22/0.45 다크 네이비. SceneBuilder가 글로벌 볼륨+카메라 포스트 배선.
- **PostFxGate**: `Application.isMobilePlatform`이면 카메라 포스트 플래그
  OFF — 모바일 티어는 이 하니스에서 미실측이므로 스펙 규칙(강등, 방치 금지)
  적용. 데스크톱 전용 적용.
- 포스트 ON 재실측: p50 8.3 / p95 10.0 / p99 10.3 ms — **포스트 비용이 노이즈
  이내**, 게이트 통과 확정. 라이브 스모크 오류 0
  (`deployed-v4-post-lobby.png`, `deployed-v4-post-combat.png`).

### 스펙 현황
- `deep-interview-vfx-terrain-command-hardening` **전 레인 배포 완료**:
  T-a 드레싱 → V2 벤트 fill → V3 원소 파티클 → K 키 난독화 → P 본 소켓
  프롭 → T-b 터레인 분할 → V1 시전 동기화 → V4 URP 포스트.

## GitHub Pages 배포 — Lane T-b 융합 터레인 연결성 분할 · 2026-08-05

### 변경 (spec `deep-interview-vfx-terrain-command-hardening` §Lane T-b)
- **abyss-chancel 융합 GLB 분할**: retained `textured-cleaned.glb`(1노드
  1메시)를 `convert_terrain.py --parts` 신설 경로로 연결성 기반 362개 섬
  분리 → 결정론 정렬(트라이 수 내림차순, 위치 타이브레이크) → ≥150 tri
  상위 48개 유지·명명(`terrain-abyss-chancel-part-NNN`) → 독립 등록(자체
  bbox, X 스팬 17 적합) + 섬별 접지(min-z→0) → 기존 TerrainImportPipeline
  경로로 임포트. **저작 시점 분리**(§3 계약), delete 자산 불사용.
- 프리팹 실측: slab 4 + apron 1 불변, part 48 신규. 분할 산출 알베도의
  DefaultTexturePlatform 2048 유입은 텍스처 상한 계약(≤1024)으로 즉시 보정
  — 상한 게이트가 실제로 회귀를 잡음.
- **echo-throne은 의도적 미분할**: 후보 자산이 전부 2D 빌보드(8×8 평면
  1섬)라 55° 카메라에서 종잇장 — §S4 비목표 확정, 테스트로 고정.
- `git tag -f pre-terrain-split-20260805` 사전 태깅(파괴적 자산 작업 계약).

### 게이트·배포
- EditMode **166/166 통과** — 신규 `TerrainPartsTests` 2종(48파츠+바닥
  불변, echo-throne 빌보드 비목표) (`unity-logs/test-results-104008.xml`).
- gh-pages `ad40851`, 캐시 버전 `d8d55ea7d9b6df7c` 라이브 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- Abyss Chancel 강하: 분할 파츠 유적 밴드(질감 있는 붕괴 콜로네이드)가
  상단 비전투 지대에 렌더, 기둥 해저드 3종·전투 판정 불변
  (`_workspace/current/engineering/deployed-tb-abyss-parts.png`).

## GitHub Pages 배포 — Lane P 본 소켓 장비 프롭 · 2026-08-05

### 변경 (spec `deep-interview-vfx-terrain-command-hardening` §Lane P)
- **랭크 티어 본 소켓 프롭**: 무기(RightHand)/랜턴(LeftHand)/클록(Chest) 3슬롯
  × 2밴드 — T0-1 없음 / T2-3 basic / T4-5 fine. `ActorView.AttachEquipProps`
  가 밴드별 멱등 갱신(런 중 랭크업 즉시 반영), `ResetForPool`에서 정리.
  비휴머노이드 리그는 무프롭 — §P2 전신 틴트가 하한.
- **자산 파이프라인**: retained 원작 프롭 2종(블레이드 .03/렐릭 .05, 원작
  런타임의 PROP_BLADE/RELIC_MESH와 동일 소스) + 절차 저작 클록 →
  `tools/blender/convert_equip_props.py` (소켓 공간 정규화, ≤800 tri 강제,
  총 3,832 tri) → `PropImportPipeline` (URP Lit 명시 머티리얼: FBX 임포트가
  emission을 드랍하고 차콜이 바닥에 묻히는 문제를 밴드 코딩 발광으로 해소
  — basic 미광 / fine 강발광: 무기 엠버·랜턴 시안·클록 진홍).
- delete 마킹 소스 불사용(§Non-Goals), 신규 EquipPropTests 5종(존재·렌더러·
  트라이 예산·착용 가능 월드 크기·URP 셰이더·휴머노이드 소켓 본).

### 게이트·배포
- EditMode **164/164 통과** (`unity-logs/test-results-102032.xml`).
- gh-pages `04e64c6`, 캐시 버전 `a64367c75e1720b4` 라이브 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- T5 시드 → Ember Gallery 강하: 우수 엠버 발광 블레이드 + 배면 진홍 클록 +
  좌수 시안 랜턴 글로우 3점 동시 렌더
  (`_workspace/current/engineering/deployed-lanep-props.png`).
- 로컬 검증: basic 밴드(차콜, T2) 근접 캡처와 T5 fine 밴드 대비 확인
  (`lanep-props-basic-closeup.png`, `lanep-props-fine.png`).
- 주의(실측): localStorage 시드는 **오리진 스코프** — localhost에서 시드 후
  github.io로 이동하면 미적용. 라이브 검증 시 라이브 오리진에서 재시드.

## GitHub Pages 배포 — V2 벤트 fill · V3 원소 파티클 · Lane K 키 난독화 · 2026-08-05

### 변경 (spec `deep-interview-vfx-terrain-command-hardening` §V2/§V3/§K)
- **V2 벤트 임박도 fill** (교차 세션 레인 `3a15a87`+`de34dc3`): 텔레그래프 링
  내부 디스크가 CycleT/VentPeriod에 비례해 0→반경으로 차오름 — "언제
  터지는가"가 한눈에 읽힘. 벤트당 1회 생성, 프레임당 할당 0.
- **V3 원소별 파티클 임팩트** (동 레인): 사전 생성 풀링 ParticleSystem 4종
  (볼트 보라 잔광 / 파동 녹색 리플 / 노바 엠버 파편 / 에이기스 시안 흡수),
  `Emit(count)` 전용, maxParticles 96, reduced-motion 시 count 절반. 검증된
  MakeUnlit 시드 경로 사용(URP Particles 셰이더는 빌드 내 참조 0으로 변형
  스트리핑 — per-particle 그라디언트는 사양 수정으로 면제). 링/스파크 문법
  증강, 대체 아님. 초기 미검증 URP Particles 시드 자산은 정리(`849dcbc`).
- **Lane K 키 저장 난독화** (`f017d3e`): `KeyVault` — 기기 파생 키(AES-CBC,
  `deviceUniqueIdentifier`+salt SHA-256) 위 `enc1:` 마킹 저장. 레거시 평문은
  로드 시 제자리 마이그레이션, 복호 실패(기기/브라우저 변경·변조)는 자동
  삭제 후 재입력 안내 — 기능 잠김 없음. UI 문구는 정직 계약대로
  "이 기기에만 난독화 저장" (암호화/안전 표현 금지). KeyVaultTests 8종.
- **HudKorean 글리프 갱신**: 새 토스트 글자(난독화 등)가 서브셋 폰트에
  없어 라이브에서 탈락 — `tools/gen_hud_font.sh` 재생성(436 glyphs, FULL
  coverage) 후 재배포로 해소.

### 게이트·배포
- EditMode **159/159 통과** (`unity-logs/test-results-095439.xml`).
- gh-pages `6d83ad8` → 글리프 수정 `b7431d0`, 최종 캐시 버전
  `e6ab57862f88d16b` 라이브 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- V2: Ember Gallery 벤트 3개가 서로 다른 위상의 fill 상태로 렌더
  (`deployed-v2-vent-fill.png`).
- V3: R 노바 직후 링+파편, F 에이기스 시전 시 시안 흡수 플래시 + 방패 40
  (`deployed-v3-nova-debris.png`, `deployed-v3-aegis-flash.png`).
- Lane K: 콘솔 `key <dummy>` 등록 → "Gemini 키 저장됨 (이 기기에만 난독화
  저장) — 자유 문장 명령 활성화" 토스트 전체 글자 정상 렌더
  (`deployed-lanek-key-toast.png`).

## GitHub Pages 배포 — 조합 스테이지 드레싱 (Lane T-a) · 2026-08-05

### 변경
- **드레싱 테이블 시스템** (spec `deep-interview-vfx-terrain-command-hardening`
  §Lane T-a): cinder-span 프리팹의 feature/prop 90종을 공용 드레싱
  라이브러리로 재사용, `StageCatalog.DressingPlacement` 정적 테이블(무 RNG)로
  조합 스테이지 3종에 스테이지별 드레싱 부여 — Ember Gallery(상단 능선 암괴
  4 + 좌하 포켓 + 하단 소품), Witness Well(좌우 대칭 감시자 4 + 상단 소품 열
  + 하단 아치 기념물), Ash Verdict(상단 재판정 매스 3 + 코너 기념물 + 하단
  소품). 배치는 전투 평면(248..1288 × 334..874) 밖 + 모든 해저드 반경+50
  클리어런스 준수. slab/apron은 불변.
- `GameDirector.ApplyStageDressing`: 스테이지 전환당 1회 실행(프레임당 0),
  라이브러리 자식의 **베이크드 피벗**(로컬 0, 메시에 위치 베이크)을 라이브
  렌더러 바운즈로 측정해 피벗 앵커 하위로 중심 정렬 — yaw/스케일이 메시
  중심 기준으로 작동.
- 라이브러리 원본이 밀리미터급 마이크로 데칼(바운즈 0.05–0.12 world unit)
  이라 테이블 스케일은 ×11–22 대역. Ash Verdict 측면 거석 2점은 시각 침범
  피드백으로 축소·후퇴 튜닝.

### 게이트·배포
- EditMode **151/151 통과** — 신규 `StageDressingTests` 5종 포함(테이블
  무결성: 라이브러리 자식 실존·feature/prop 접두사 강제·전투 평면 밖·해저드
  클리어런스·결정론) (`unity-logs/test-results-092856.xml`).
- gh-pages `ead692d`, 캐시 버전 `de0be8ac3e61a30f` — 라이브 데이터 리소스
  새 버전 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- Ember Gallery 강하: 드레싱 9/9 렌더
  (`_workspace/current/engineering/deployed-dressing-ember-gallery.png`).
- 로컬 최종 빌드에서 Witness Well·Ash Verdict 드레싱 확인
  (`dressing-witness-well.png`, `dressing-ash-verdict.png`).

## GitHub Pages 배포 — 동료 명령 콘솔 + VFX 임팩트 패스 · 2026-08-04

### 변경 (소스 `7256cb5`, 교차 세션 레인)
- **동료 명령 콘솔** (던전, Enter): 한국어 우선 키워드 파서 → 닫힌 의도 집합
  (집중공격/방어·복귀/스킬 시전), 선택적 Gemini 자유문장 폴백 (키는 런타임
  전용, 빌드에 미포함). 입력 중 0.2x 슬로모. 분류 테스트 20종 포함.
- **AOE/스킬 VFX 임팩트 패스**: 노바 번 데칼 1.2 s, 펄스 필드 필 3 s,
  Aegis/Ward 시전 링 (전부 풀링, ClearTransient 커버).

### 게이트·배포
- EditMode **146/146 통과**
  (`_workspace/current/engineering/unity-logs/test-results-082947.xml`).
- Unity 6000.5.6f1 WebGL 빌드 성공
  (`_workspace/current/engineering/unity-logs/build-083019.log`), data
  26,558,801 B, wasm 9,140,333 B.
- gh-pages `6ddd724`, 캐시 버전 `18b0fc1a992f9312`. 라이브 index.html·4개
  리소스 모두 새 버전 확인.

### 배포 후 스모크 (라이브, 1440×900, 오류 0)
- 캠페인 1단계 강하 → Enter 콘솔 오픈 (명령 힌트·슬로모) → `nova` 제출 →
  **"잿불 노바 시전"** 피드백, 기름 100→55, 적 4→3, 점수 100, 노바 번 데칼
  렌더 (`_workspace/current/engineering/deployed-console-nova.png`).
- 로컬 빌드 사전 검증에서 동일 경로 + 콘솔 열림/닫힘/ESC 탈출 확인.
- 참고: headless CDP `keyboard.type()`은 한글 IME 조합이 없어 ASCII 별칭
  (`nova`)으로 실행 경로를 증명 — 한글 키워드는 파서 단위테스트 20종이 커버.

## GitHub Pages 배포 — WebGL 텍스처 상한 빌드 · 2026-08-04

### 배포
- gh-pages 커밋 `d4c7392` (`deploy: WebGL texture-cap verified build 2026-08-04`).
  data 26,549,778 bytes (이전 52,380,884 대비 −49.3 %), wasm 9,117,062 bytes,
  캐시 버전 `1bc1f4b712e762e5` → `61a0b09946ca5642`.
- 라이브 확인: `https://akillness.github.io/hongT/`가 새 캐시 버전 index.html을
  서빙하고 `Build/build-webgl.data.unityweb?v=61a0b09946ca5642`가 HTTP 200
  / content-length 26,549,778로 응답했다.

### 배포 후 스모크
- 데스크톱 1440×900: 로비 → 출정 → 전투 진입, WASD 이동·Space 타격 입력 후
  체력 86/웨이브 1/적 4·피격 비네트 확인, 런타임 오류·경고 배너 0
  (`_workspace/current/engineering/deployed-texcap-desktop-lobby.png`,
  `_workspace/current/engineering/deployed-texcap-desktop-combat.png`).
- 모바일 390×844 DPR 2: 로비 → 출정 → 전투 진입, 체력 100/웨이브 1/적 3,
  런타임 오류 0
  (`_workspace/current/engineering/deployed-texcap-mobile-lobby.png`,
  `_workspace/current/engineering/deployed-texcap-mobile-combat.png`).
- 아레나 `?mode=arena` 1440×900: 웨이브 전투 부팅, D 이동·Space 근접 교전
  (체력 44 정상 피격), Q/E 스킬바·적 체력바 렌더, 런타임 오류 0
  (`_workspace/current/engineering/deployed-texcap-arena-combat.png`).
- 캠페인 1단계 Cinder Span 1440×900: prologueDone 시드 후 강하 →
  "웨이브 1/5" 배너, 3타 콤보·대시·Q/E/R/F 스킬, Void Aegis 방패 40,
  기름 68 소모, 적 3→2, 분출구 텔레그래프, 런타임 오류 0
  (`_workspace/current/engineering/deployed-texcap-campaign-stage1*.png`).
  localStorage v2 스키마 시드가 로비 카드 게이팅(프롤로그 재훈련·1단계
  해금)에 정상 반영 — 영속 경로 확인.
- 종합: `_workspace/current/qa/deployed-release-verification.md`.

## 로컬 검증 — WebGL 텍스처 상한 보정 · 2026-08-04

### 변경
- 임포터 파일 43개의 Default 또는 WebGL 항목 65개를 1024로 상한 조정했고, 아이콘
  Default 항목 20개는 256을 유지했다.

### 회귀 게이트
- 집중 텍스처-상한 테스트와 최종 111/111 테스트가 통과했다
  (`_workspace/current/engineering/unity-logs/test-results-071245.xml`). Unity
  6000.5.6f1 로컬 WebGL 빌드는
  `_workspace/current/engineering/unity-logs/build-071018.log`에서 성공했고,
  54,819,218 bytes, `errors=0`, `warnings=2`를 기록했다. 로컬 데스크톱·모바일에서
  로비에서 전투로 전환하는 스모크를 수행했고,
  `_workspace/current/engineering/post-cap-desktop-combat.png` 및
  `_workspace/current/engineering/post-cap-mobile-combat.png`에 화면을 보존했다.
  GitHub Pages는 배포하지 않았다.

## v0.2.0 — 심연 강하 (Hack & Slash Overhaul) · 2026-08-04

`index.html?mode=campaign&stage=cinder-span` 단일 페이지 흐름을 **로비 중심
단일 씬 상태머신**으로 전면 개편. 원작(Abyssal-Lantern)의 3D 인게임·로비
구성 리서치를 근거로 던전 전투를 핵앤슬래시로 재설계했다.

### 신규
- **로비 (단일 씬)** — 라이브 3D 디오라마 배경(워든/동료/보스 대치, 스테이지
  액센트 라이트, 슬로우 오빗), 성장/장비/군단 탭, 출정 카드.
- **프롤로그 "점화 훈련"** — 탑다운 오소그래픽 2D 디펜스 3웨이브로 장르 학습
  → 클리어 시 90°→55° 카메라 스윕(2.5D 전환 연출) → 캠페인 해금.
- **핵앤슬래시 전투 킷 (던전)** — 3타 콤보(87 마무리+넉백), 대시(무적
  0.22 s), 스킬 4종(균열 화살/묘지 파동/잿불 노바/공허 방패), 원소 상성
  사이클(ember>frost>veil>void, +20 %/−15 %).
- **인런 성장** — XP 곡선(원작 [30..310]+60), 레벨 캡 12, 레벨업 시
  피해 +4 %/HP +6/재생 +0.3.
- **정예 & 추출** — 7번째 스폰 정예(HP×3, 금색), 시체 채널 2 s로 동료화
  (`<visual>-echo`), 중복 시 유물 +30.
- **동료 동행** — 보스 첫 처치 보상 + 추출 로스터에서 1체 선택, 80 px 추종,
  플레이어 피해 60 % 자동 공격. 메시 재사용 + 틴트 변형(페이로드 0 증가).
- **보스전 개편** — 상단 보스 바, 2페이즈(50 %: 이속 +25 %, 접촉 ×1.25,
  도발 말풍선), Monarch 호위 3기 소환.
- **스토리 말풍선** — 원작 stage-story-catalog 대사 이식(스테이지 시작/보스
  등장/페이즈2/클리어), 월드공간 빌보드, 우선순위 큐.
- **메타 성장** — 스탯 포인트(클리어 +2, 첫 보스 +1), 장비 T0–T5 유물 구매
  ([2,4,7,11,16]), localStorage v2 (하위호환).
- **6단계 캠페인** — Cinder Span → Ember Gallery → Abyss Chancel → Witness Well →
  Echo Throne → Ash Verdict를 순서대로 해금. 마지막 Ash Verdict를 정화하면
  Ember Rest 없이 최종 결과 오버레이를 표시한다. 플레이어는 이 패널에서
  재도전하거나 명시적으로 로비 복귀를 선택한다.
- **Ember Rest** — 비최종 스테이지를 마친 뒤 결과 패널 없이 즉시 열리는
  결정론적 준비 제안 3개 중 하나를 선택하거나 건너뛴다. 선택은 다음 던전
  1회에만 적용되며 저장·재시도·이후 스테이지로 이월되지 않는다.
- **휴머노이드 런타임 애니메이션 게이트** — 재스키닝한 전 캐릭터 프리팹에
  유효한 Humanoid Avatar·공유 액션 컨트롤러·활성 Animator·SkinnedMeshRenderer와
  공격 시 오른손 모션을 요구한다.

### 변경
- `campaign.html` → `index.html` 즉시 리다이렉트 (로비가 게임 안으로 통합).
- 던전 키맵: Q/E/R/F = 스킬 4종, 재시작은 패널 버튼 전용 (아레나는 기존
  Q/E/R 유지).
- 던전 적 HP `86 + min(140, (wave−1)×11)` (콤보 DPS 보정).
- 노멀맵 활성화 (`_BumpMap` + NormalMap 임포터 타입).
- **잿불 분출구** — 활성 Lantern Ward는 분출구 펄스 피해를 무효화하며, 기존 피해 유예는 보존.
- **세로 WebGL 기동 안정화** — Unity 6의 자동 캔버스-백킹스토어 동기화가
  390×844 전체화면에서 WASM 호출 스택을 재귀적으로 소진하던 경로를 끄고,
  CSS로 렌더된 캔버스 사각형과 DPR 상한 2를 사용해 로더 전과
  resize·orientation·visualViewport 변경 뒤 백킹스토어를 명시적으로 동기화한다.
  기동 실패는 브라우저 `alert` 대신 게임 경고 배너에 표시해 로딩 대기와 실제
  오류를 구분한다.
- **WebGL 한글 로비 글리프 게이트** — 모든 View 문자열로 `HudKorean` 서브셋을 재생성하고, 라이브 모션 버튼이 같은 리소스를 사용한 채 `모션: 보통`과 `모션: 약함` 두 상태의 모든 글리프를 보유하는지 EditMode에서 검증한다.
- **동료 명령 캐치업 안전성** — 고정 스텝 캐치업 배치에서 동료 대기/회수
  명령도 첫 틱에서만 소비해 반복 재적용되지 않는다.

### 회귀 게이트
- 최종 EditMode 전체 회귀 110/110 통과, 실패 0
  (`_workspace/current/engineering/unity-logs/test-results-065139.xml`). WebGL 셸
  회귀는 자동 동기화 해제, DPR≤2, 초기/이후 viewport 동기화, 세로·데스크톱 CSS
  계약, 기동 오류 배너, 멱등 postprocess를 검증한다.
- 최종 Unity 6000.5.6f1 캐시-버스트 WebGL 빌드 통과
  (`_workspace/current/engineering/unity-logs/build-055336.log`,
  `Build Finished, Result: Success`, `errors=0`, `warnings=2`, 80,731,744 bytes).
- GitHub Pages(<https://akillness.github.io/hongT/>)의 iPhone UA/DPR 3 에뮬레이션
  390×844→844×390 회전에서 100 % 로드, `unity-mobile` 분기, 2× DPR 상한
  백킹스토어(780×1688→1688×780), 로딩 바 숨김·경고/런타임 오류 0을 확인했다.
  데스크톱 1280×720→1440×900 확대에서도 CSS/백킹스토어가
  1080×720→1280×853으로 함께 갱신되고 오류가 없었다.
- 최종 Pages iPhone UA/DPR 3 수동 스모크에서 로비 → 프롤로그 진입 → 전투 HUD →
  패배 패널 → `다시 도전` 후 HUD 재초기화까지 동작했고, 전투 HUD의 드래그
  조이스틱과 `타격` 터치 컨트롤이 표시됐다. 이 스모크는 전체 웨이브 클리어
  검증을 대신하지 않는다.
- 동료 대기·회수 원샷 명령의 캐치업 회귀 2/2 통과, 실패 0
  (`_workspace/current/engineering/unity-logs/test-results-companion-one-shot.xml`).
- 한글 로비 글리프 회귀 1/1 통과, 실패 0
  (`_workspace/current/engineering/unity-logs/test-results-lobby-motion-label-font.xml`).
  Unity 6000.5.6f1 WebGL 보정 빌드는 `build-063448.log`에서 성공했다.
  GitHub Pages 모바일 스모크의 실제 토글 전·후 상태는
  `_workspace/current/engineering/deployed-mobile-font-normal.png`와
  `_workspace/current/engineering/deployed-mobile-font-weak.png`에 각각 보존했다.

---

## v0.1.0 — Unity 재구현 초판 · 2026-08-04

- 원작 Cinder Court(Canvas 2.5D)의 수치 계약을 보존한 Unity 6/URP/WebGL 재구현.
- 결정론 60 Hz 순수 C# 심과 Unity 6/URP/WebGL 재구현의 기반을 도입했다.
- 3D 캐릭터 8종에 Blender 본히트 재스키닝과 Unity Humanoid 리타겟 경로를
  적용했다.
- 캠페인 초기 기반: 보스 웨이브, 장비 파편, 던전 기믹.
- ElevenLabs SFX 8종 + 로어 앰비언트 + BGM 루프.
- GitHub Pages 배포: <https://akillness.github.io/hongT/>
