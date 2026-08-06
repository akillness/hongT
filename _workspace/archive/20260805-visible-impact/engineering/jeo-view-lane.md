# LANE: Presentation / View (owner: jeo)

## Mission
`Assets/Scripts/View/` 아래 파일만 생성한다 (asmdef `CinderCourt.View`는 이미 존재).
추가로 `Assets/Plugins/WebGL/storage.jslib` 하나만 예외로 생성 허용.
`Assets/Scripts/Sim/**`, `Assets/Editor/**` 수정 금지.
`Assets/Scripts/Sim/SimTypes.cs`는 // FROZEN CONTRACT — 읽기 전용.

## Binding docs
- `docs/SIM_SPEC.md` — 좌표 변환·수치·오디오·입력 계약 (단일 진실).
- `Assets/Scripts/Sim/SimTypes.cs` — `ICinderSim`/`ISimSnapshot`/`SimEvents`/enum 정의.
- 시뮬 구현은 `Assets/Scripts/Sim/CinderSim.cs` (`new CinderSim()`)이 이미 존재.

## Files to create (전부 namespace `CinderCourt.View`)
1. `GameBootstrap.cs` — 씬의 GameRoot에 붙는 유일한 씬 컴포넌트.
   Awake에서 나머지 전부(GameView, HudView, InputAdapter, AudioDirector,
   VfxDirector, CameraRig)를 코드로 조립. Resources 로드:
   - 캐릭터 프리팹 `Resources/Characters/<assetId>` (guard, ember-cohort, scout,
     shade, possessed, shadow-commander-boss, broken-court-monarch-boss).
     프리팹 없으면 **캡슐 프리미티브 폴백** 생성 (개발 진행 차단 금지).
   - 오디오 `Resources/Audio/cue-<name>` (AudioClip, mp3). 없으면 무음 스킵.
2. `GameView.cs` — `CinderSim` 소유, 자체 고정스텝 어큐뮬레이터
   (`Time.deltaTime`, FixedStep 1/60, MaxFrameDelta 0.25, MaxCatchUpSteps 5 —
   Unity FixedUpdate 사용 금지, SimConfig 상수 사용).
   InputAdapter에서 SimInput 조립 → `sim.Tick(input)` → 스냅샷을 하위 뷰에 배포.
   적 뷰 풀링: EnemyState.Id 키 Dictionary, 사망 페이드 후 풀 반환.
3. `ActorView.cs` — 액터 1기.
   - 좌표: `sim (x,y) → Unity (x*0.01, 0, -y*0.01)`.
   - facing: Y회전 (+1 → 90°, -1 → 270°) 부드럽게 보간 (원본은 스프라이트 플립).
   - 스케일: EnemyState.Scale (보스 1.6). 원본 깊이스케일(0.62-1.0)은 3D 원근이
     대체하므로 적용하지 않음 (주석으로 명시).
   - Animator: `SetInteger("action", (int)state.Action)` — SimTypes의
     ActorAction enum 순서 그대로 (0 Idle … 10 Show). Animator/컨트롤러 없으면
     no-op (통합 전에도 컴파일+실행 가능해야 함).
   - 체력바: 머리 위 쿼드 2장(배경+필) 코드 생성, 매 LateUpdate 카메라 빌보드.
     HP 가득이면 숨김.
   - 사망: FadeTime(0.34 s) 동안 스케일 축소 페이드 (URP 머티리얼 투명 전환 비용
     회피).
4. `HudView.cs` — 런타임 생성 Screen Space Overlay Canvas (uGUI).
   - 좌상: HP 바+수치, 기름(charge) 바+수치.
   - 우상: 웨이브/점수/유물/적 수.
   - 하단 중앙: Nova(Q)/Ward(E) 스킬 카드 2장 — 쿨다운 fillAmount 오버레이,
     기름 부족 시 흐리게. 클릭 → InputAdapter로 큐잉.
   - 게임오버 패널: 최종 점수·웨이브·유물·처치, "재점화 (R)" 버튼.
   - 웨이브 시작 시 로어 1줄 표시. LORE_BEATS 6줄 순환(뷰 로컬 상수로 소유):
     "잿불 법정은 군단이 그 기름을 용광로로 바꾸기 전까지 성유물고였다.",
     "잿불 군단의 몸은 비어 있다. 그 안에서 타는 것은 훔쳐온 랜턴 기름이다.",
     "당신이 줍는 유물 조각 하나하나가 심연이 지우려 한 이름이다.",
     "파수꾼의 결계는 갑옷이 아니다. 어둠이 읽지 못하도록 봉인된 기억이다.",
     "더 깊은 군단은 이미 타오르며 온다. 보내지기 전에 불붙여진 것이다.",
     "랜턴은 심연을 죽이지 않는다. 다만 심연이 셈을 끝내지 못하게 막을 뿐이다."
   - 한국어 라벨 유지 ("웨이브", "점수", "유물", "재점화"). 폰트는
     `LegacyRuntime.ttf` (Resources.GetBuiltinResource<Font>).
   - 텍스트는 값이 변할 때만 갱신 (per-frame 문자열 할당 금지).
5. `InputAdapter.cs` — New Input System 직접 폴링 (`Keyboard.current`,
   `Pointer.current`). WASD+화살표, Space(Strike)/Q(Nova)/E(Ward)/R(재시작).
   터치: 좌하 가상 D-pad 4버튼 + 우하 Strike/Nova/Ward 버튼 (HudView가 생성해
   연결, `Touchscreen.current != null`일 때만 표시). 버튼 press 상태를 폴링해
   SimInput으로 병합.
6. `AudioDirector.cs` — SimEvents → PlayOneShot 매핑:
   PlayerStruck→strike, EnemyHit→hit, EnemyKilled→kill, NovaCast→nova,
   WardCast→ward, PickupCollected→pickup, WaveStarted→wave, GameOver→gameover.
   겹침 허용(트리밍 금지). HUD 우상단 음소거 토글, PlayerPrefs 키
   "abyssal-lantern:cinder-court:muted".
   추가 (2026-08-04 갱신, 사용자 지시: 음성 내레이션 금지):
   - `Resources/Audio/cue-bgm` — 전용 AudioSource(loop=true, volume 0.35)로
     씬 시작 시 재생. 음소거 토글에 함께 반응.
   - `Resources/Audio/cue-lore` — WaveStarted 시 wave 스팅어와 **함께** 재생
     (volume 0.5). 로어 텍스트의 앰비언트 밑깔개 역할.
7. `VfxDirector.cs` — 코드 생성 이펙트만 (에셋 의존 금지):
   - Nova: LineRenderer 원형 링, 반경 0→2.5 (250*0.01), 0.42 s 확장+페이드.
     스냅샷 NovaX/NovaY 원점.
   - Ward: 플레이어 추종 반투명 구 셸 3 s, 마지막 0.5 s 점멸.
   - 피격: 액터 렌더러 emission 펄스 0.13 s (MaterialPropertyBlock 사용).
   - 픽업: 종류별 색 소형 옥타헤드론 회전+바운스 (shard #ff9a52, flask #ffd489,
     relic #8fe9ff). PickupState 목록과 동기화, 수집 시 제거.
8. `CameraRig.cs` — Main Camera를 찾아 아레나 프레이밍 유지 (기준 3:2, 좁은
   화면에선 FOV 확대). 피격 0.12 s / Nova 0.2 s 소진폭 셰이크 (진폭 0.06 이하).
9. `WebGLStorage.cs` + `Assets/Plugins/WebGL/storage.jslib` — 게임오버 시
   RunDigest JSON을 localStorage 키 "abyssal-lantern:cinder-court:last-run"에
   기록. 비WebGL/에디터는 PlayerPrefs 동일 키 폴백. jslib는
   `mergeInto(LibraryManager.library, { ... })` 표준형, `DllImport("__Internal")`.

## Hard constraints
- Unity 6000.5 / URP 17.5 API만. `GameObject.Find`류 매 프레임 호출 금지.
- per-frame 힙 할당 금지 (문자열 포함 — HUD 수치는 변할 때만 SetText).
- 모든 클래스에 프리팹/Animator/AudioClip 부재 시 no-op 가드.
- WebGL 제약: threads/compute/Reflection.Emit 금지.
- LINQ 금지 (View 어셈블리 전체).

## Verification
Unity 컴파일은 Integration 단계(오케스트레이터)가 실행한다. 여기서는:
- SimTypes.cs의 실제 멤버명과 대조해 모든 참조가 존재하는지 재확인.
- 확신 없는 API는 보수적 API(레거시 uGUI, LineRenderer, PlayerPrefs)로 대체.

## Reporting
완료 시 `_workspace/current/engineering/jeo-view-lane-report.md`:
파일 목록, 설계 선택, 통합 단계에서 확인 필요한 지점.
git 조작 금지. 포매터 실행 금지. 완료 후 즉시 종료.
