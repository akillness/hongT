# HongT 4-레인 병렬 구현 통합에서 재사용 가능한 결론 (2026-08-08)

시드 `_workspace/current/intake/deep-interview-seed-ui-vfx-flow.md` (FROZEN)의
W4~W16 + W-MV를 4개 병렬 레인(sim/ui/vfx/asset)으로 구현하고 통합한 사이클에서
얻은, 다음 세션·다른 프로젝트에서 재사용 가능한 결론.

## 1. [OBSERVED] Unity float vs dotnet double — 테스트 핀 드리프트

- 증상: `(int)(250 * 2.10f)`가 dotnet 8 하네스에서 525, Unity 런타임에서 **524**
  (2.10f×250 = 524.999…). 스탠드얼론에서 작성·통과한 핀이 Unity EditMode에서 깨짐.
- 해법: 정수 캐스트 지점에 `+0.5f` 반올림(런타임·테스트 양쪽 동일 식).
  `Assets/Scripts/Sim/CinderSim.cs` CollectPickup relic 분기,
  `Assets/Tests/EditMode/LootGradeTests.cs:254` 참조.
- 일반화: **스탠드얼론 dotnet으로 작성한 수치 핀은 반드시 Unity 런에서 재확인**
  (CLAUDE.md §4의 "다이제스트 회귀는 런타임 내 비교만 유효"와 같은 뿌리).

## 2. [OBSERVED] Unity 배치모드 락 시 unity-mcp 경유 검증 경로

- 다른 Unity 인스턴스(열린 에디터/동시 세션)가 프로젝트를 열고 있으면
  `tools/unity_batch.sh`는 "Multiple Unity instances cannot open the same
  project"로 즉사한다.
- 대체 경로(전부 실증됨): `npx unity-mcp-cli` →
  ① `run-tool assets-refresh` (임포트+컴파일, 에러가 텍스트로 돌아옴)
  ② `run-tool script-execute` — `public class Script { public static object
  Main() { … } }` 형태 필수. 에디터 배치 파이프라인 호출 가능
  (`CharacterImportPipeline.ImportAll()` 등)
  ③ `run-tool tests-run --timeout 900000` (ms 단위! `--timeout 900`은 0.9초)
- **도메인 리로드 경합 증상**: 리컴파일 직후 tests-run이 "No tests found"를
  반환한다. 실패가 아니라 경합 — 30초 대기 후 재호출하면 정상.
- CLI 60초 기본 타임아웃에 걸려도 에디터 안에서는 작업이 계속 실행 중일 수
  있다 — 재트리거하지 말고 산출물(프리팹 등)을 폴링할 것.

## 3. [OBSERVED] cinder-sluice는 앰비언트 바닥 슬래브가 0개인 스테이지

- `EnvironmentBuilder.Build("cinder-sluice")` 실측: `env-floor-*` 14개 전부
  해저드 링 가구(`part-` 자식, 500번대)이고 앰비언트 슬래브(`piece-` 자식,
  000번대)는 **0개** — 해저드 테이블이 조밀해 `NearAnyHazard` 필터가 전부 걸러냄.
- "모든 스테이지가 앰비언트 패널 6~10개를 낸다"는 전제로 쓴 테스트/장식 레이어는
  이 스테이지에서 깨진다. 장식 컴포넌트는 0개에서 무동작 폴백이 정답이고,
  테스트 가드는 `env-floor-*` 직계의 `piece-` 자식만 검사해야 한다
  (`piece-` 접두사는 바닥 밖 지오메트리도 재사용함 — 전역 스캔 금지).
  `Assets/Tests/EditMode/TerrainFlipbookTests.cs` 참조.

## 4. [OBSERVED] opt-in 심 게이트를 런타임에 켤 때의 테스트 원칙

- AMENDMENT #13/#14(`DungeonProgressionConfig`)는 게이트 OFF 시 코드 경로
  무분기라 골든 digest가 불변 — 그러나 **런타임 동등성 테스트**(기대 심을 직접
  생성해 실런과 digest 비교하는 류)는 런타임이 게이트를 켜는 순간 깨진다.
- 원칙: 골든 digest는 게이트 OFF 생성자를 계속 핀하고, 런타임 동등성 테스트는
  런타임과 같은 게이트로 기대 심을 만든다
  (`GameDirectorCampaignRouteTests.ExpectedEmberGalleryRun` 참조).

## 5. [OBSERVED] 병렬 레인 자산-코드 계약은 "소비자가 정본"

- 생성(asset)과 소비(ui/vfx) 레인을 병렬로 돌리면 경로/포맷이 반드시 어긋난다
  (이번 사이클 실례: `Textures/Env/` vs `Resources/Terrain/`, 컬러 vs
  그레이스케일, 1024×683 vs 1024×694).
- 동작한 규약: **소비 코드가 리포트에 계약(경로·해상도·그리드·임포터 설정)을
  [정본]으로 명시**하고, 오케스트레이터가 생성 레인에 매칭 라운드를 지시.
  `.meta`(Sprite type, wrapMode)까지 계약에 포함해야 한다 — 임포터 설정이
  틀리면 실패가 아니라 **무음 폴백**이라 늦게 발견된다.

## 5-B. [OBSERVED] WebGL 빌드는 열린 에디터(MCP 활성)에서 구조적으로 불가

- unity-mcp 패키지 Runtime이 WebGL 플레이어 컴파일에서 CS0246/CS0115로 깨진다.
  `BuildScript.ExcludeEditorToolingFromWebGl()`이 빌드 직전 `UNITY_MCP_READY`
  define을 제거하지만, **살아 있는 에디터에서는 MCP 리졸버가 도메인 리로드마다
  define을 재설치**해 제거가 유지되지 않는다. 즉 MCP 브리지로 빌드를 걸면
  그 브리지가 빌드를 깨뜨린다 → **WebGL 빌드는 반드시 에디터 종료 후
  배치모드**(`tools/unity_batch.sh`가 `UNITY_MCP_KEEP_CONNECTED=false`로
  리졸버를 잠재움).
- 에디터 원격 종료: `EditorApplication.delayCall += Exit`는 발화하지 않을 수
  있다 — **동기 `EditorApplication.Exit(0)`** 이 확실하다 (응답이 안 돌아오는
  것이 정상, 종료 전 씬 dirty 확인 필수).
- 에디터 강제 종료 직후 첫 배치 빌드는 Bee 내부 오류("Backend has requested
  a buildprogram run 6 times")로 실패할 수 있다 — **단순 재시도로 해소**
  (이전 진행분을 이어받아 2차는 41초 만에 성공).
- 빌드 로그의 `size=`(uncompressed player summary, 86.4MB)와 배포 디렉터리
  실크기(gzip unityweb, 62MB)는 다른 수치다. 120MB 게이트 판정은 배포
  디렉터리 기준.

## 6. [OBSERVED] 이월분 완료 기록 (같은 날 2차 사이클)

당초 이월했던 항목 전부 같은 날 2차 병렬 사이클로 완료했다
(커밋 d5c2fe9/6f62d2d/41aabd7/dea1357/a70d88d, EditMode 714/716,
빌드 67MB, gh-pages 바이트 일치 배포):

- **W-MV 게이트 ON** — 핵심 발견: 환경 빌드가 심 생성보다 **먼저**라 심
  스냅샷 주입은 구조적으로 불가. `GameView.DungeonProgression`(정적 단일
  출처)을 뷰·심 양쪽이 `DungeonBoundsSpec.Resolve`로 함께 통과시키는 것이
  정답. 부수 실결함 2건 발견·수정: Zone C 북측 림 테라스가 확장 링과 겹침,
  `VfxDirector.WallSpanWorld` 하드코딩으로 재의 벽 커튼이 296px 모자람.
- **W6(AMENDMENT #16)** — 아키타입 4종 5축 차별화. 보스 프리팹은
  **비풀링**(`RentBoss`)으로 생성해야 풀 오염(Warden 메시가 Tactician으로
  재등장)이 없다. 보스 3종 리스킨은 리깅 도너가 없어 shadow-commander-boss
  골격을 도너로 사용(armFit 0.76~0.91; generic 도너는 0.35~0.42로 게이트 실패).
- **W11** — emscripten 키보드 경로에 composition 핸들러가 아예 없다는 것이
  근본 원인. 숨은 input + `.jslib` 브리지 + 순수 C# 조합 상태머신.
- **V4** — "p95 실측 없이 PASS 불가" 게이트는 런타임 프레임 워치독(초과 시
  포스트 자동 강등)으로 상시화했다. 강등 로그 자체가 게이트 작동 증거.
- 테스트 정밀도 교훈 재확인: double로 저작한 기하 핀 2건이 Unity float에서
  1e-5~1e-7 차이로 깨짐 — 핀에는 의미 최소 변화량보다 넉넉히 작은 허용오차를
  처음부터 줄 것.
