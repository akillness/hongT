# WebGL 레인 — W11: WebGL 한글 IME 입력

시드: `_workspace/current/intake/deep-interview-seed-ui-vfx-flow.md` W11 / D9(포함, 최후 레인).
선행 근거: `_workspace/current/qa/command-console-hangul-duplication.md` §"[INFERENCE] 남은 범위".

---

## 1. 문제 [OBSERVED]

배포 프레임워크(`build-webgl/Build/build-webgl.framework.js.unityweb`)의 키보드 경로는
`keydown/keypress/keyup`만 등록한다. `compositionstart/compositionupdate/compositionend`
핸들러가 없다. 브라우저 IME는 **포커스된 편집 가능 엘리먼트**에만 조합하는데 WebGL 캔버스는
편집 가능 엘리먼트가 아니다. 결과: 커맨드 콘솔에서 한글 조합 중간 음절(ㄱ → 가 → 각)이
화면에 뜨지 않고 확정 문자열도 도착하지 않는다.

이전 사이클의 중복 입력 수정은 *writer 두 개*를 하나로 줄인 것이지 IME 지원이 아니었다.
이 레인이 그 미해결 항목을 닫는다.

## 2. 구조 — 숨은 HTML input + `.jslib` 브리지

```
브라우저 IME
   │  (한글 조합은 <input>에서만 일어난다)
   ▼
#cinder-ime-input  ← 화면 밖 1px 투명 실 input, 콘솔 열릴 때 focus()
   │  DOM 이벤트
   │    compositionstart / compositionupdate / compositionend
   │    input(insertText)  keydown(Enter/Escape/Backspace)
   ▼
Assets/Plugins/WebGL/hangul_ime.jslib      CinderImeOpen(cb) / CinderImeClose()
   │  makeDynCall('vii')  →  (int kind, char* utf8)
   ▼
WebGLHangulIme.Dispatch  [AOT.MonoPInvokeCallback]      (WebGLHangulIme.cs:47)
   │  Marshal.PtrToStringUTF8 → Action<ConsoleImeEvent,string>
   ▼
HudView.OnConsoleImeEvent                               (HudView.cs:1527)
   │  순수 C# 상태만 변경 (프레임 사이에 실행되므로 uGUI는 건드리지 않는다)
   ▼
CommandConsoleImeComposition  (조합 tail)  +  CommandConsoleBuffer (확정 텍스트)
   │  다음 틱
   ▼
HudView.DrainConsoleIme → _consoleField.text / caret / 콘솔 닫기
                                                        (HudView.cs:1561)
```

이벤트 종류(와이어 계약, 재번호 금지 — `ConsoleImeEvent`, `WebGLHangulIme.cs:19`):

| kind | 의미 | 소스 이벤트 |
|---|---|---|
| 0 | CompositionStart | `compositionstart` |
| 1 | CompositionUpdate | `compositionupdate` (pre-edit **교체**) |
| 2 | CompositionEnd | `compositionend` (`data==""`면 취소) |
| 3 | Insert | `input` (`insertText`/붙여넣기) |
| 4 | DeleteBackward | `keydown` Backspace (비조합 시) |
| 5 | Submit | `keydown` Enter (비조합 시) |
| 6 | Cancel | `keydown` Escape (비조합 시) |

### 2.1 왜 Enter/ESC가 브리지를 거쳐 돌아오는가 [INFERENCE]

Unity WebGL은 기본적으로 DOM 포커스와 무관하게 키보드를 가로챈다
(`WebGLInput.captureAllKeyboardInput` 기본 true). 그대로 두면 숨은 input과
`Keyboard.onTextInput`이 **동시에** 같은 키를 받아 지난 사이클의 중복 입력이 그대로
재발한다. 그래서 `WebGLHangulIme.Open`이 열기 직전
`captureAllKeyboardInput = false`로 내리고(`WebGLHangulIme.cs:81`) 닫을 때 되돌린다.
Unity가 키를 못 보게 되므로 Enter/ESC도 브리지가 직접 전달해야 한다(kind 5/6).
`HudView.UpdateCommandConsole`의 기존 `enterKey/escapeKey` 폴링은 **삭제하지 않았다** —
브리지가 없는 환경(에디터, 스탠드얼론, 브리지 설치 실패)에서 그대로 유효한 경로다.

### 2.2 조합 중 Enter/ESC는 IME 것 [OBSERVED-BROWSER-SPEC]

`keydown` 핸들러는 `e.isComposing || e.keyCode === 229`면 즉시 반환한다
(`hangul_ime.jslib:112`). 조합 중 Enter는 음절 확정, ESC는 음절 취소이고 둘 다 콘솔을
건드리면 안 된다. `keyCode 229`는 구형 Safari가 조합 시작 keydown에 `isComposing`을
세우지 않아 필요한 이중 판정이다. ESC가 콘솔까지 온 경우
(`ConsoleImeEvent.Cancel`)에도 `IsComposing`이면 조합만 취소하고 콘솔은 유지한다
(`HudView.cs:1554`).

### 2.3 중복 커밋 방지

`compositionend`와 짝을 이루는 `input(inputType: "insertCompositionText")`은
Chrome이 end 앞, Firefox가 end 뒤에 쏜다. 이걸 같이 먹으면 음절이 두 번 들어간다.
`input` 핸들러는 `e.isComposing`, 내부 `composing` 플래그, `insertCompositionText`
세 조건으로 걸러낸다(`hangul_ime.jslib:90-104`).

Backspace를 `input` 이벤트로 받지 않고 `keydown`에서 직접 처리하는 이유: 숨은 input의
`value`는 커밋 직후 항상 비워지므로 브라우저가 지울 게 없어 `input` 이벤트 자체가 안 뜬다.
텍스트의 소유자는 `CommandConsoleBuffer`이지 DOM 엘리먼트가 아니다.

## 3. 변경/신규 파일

### 신규

| 경로 | 줄 | 내용 |
|---|---|---|
| `Assets/Plugins/WebGL/hangul_ime.jslib` | 177 | 숨은 input 생성·포커스·DOM 이벤트 → `makeDynCall('vii')` |
| `Assets/Plugins/WebGL/hangul_ime.jslib.meta` | 2 | guid `3b321591cdf64b06ba73f9a7d8aa283e` |
| `Assets/Scripts/View/CommandConsoleImeComposition.cs` | 173 | **UnityEngine 비의존** 조합 상태 머신 |
| `Assets/Scripts/View/WebGLHangulIme.cs` | 105 | `[DllImport("__Internal")]` 래퍼 + `ConsoleImeEvent` |
| `Assets/Tests/EditMode/CommandConsoleImeCompositionTests.cs` | 214 | EditMode 16 케이스 |

### 수정

| 위치 | 변경 |
|---|---|
| `Assets/Scripts/View/CommandConsoleBuffer.cs:88` | `AppendComposed(string)` 추가 |
| `Assets/Scripts/View/HudView.cs:1388-1399` | W11 필드 + `ConsoleIme` 지연 생성 |
| `Assets/Scripts/View/HudView.cs:1484-1502` | `OpenCommandConsole`: 브리지 우선, 실패 시 기존 `onTextInput` 미러 |
| `Assets/Scripts/View/HudView.cs:1523-1574` | `OnConsoleImeEvent` / `DrainConsoleIme` 신규 |
| `Assets/Scripts/View/HudView.cs:1581-1603` | `CloseCommandConsole`: `Flush` 후 브리지 해제 |
| `Assets/Scripts/View/HudView.cs:1732-1734` | `UpdateCommandConsole` 진입부에서 드레인 |

HudView 수정은 전부 커맨드 콘솔 영역이다. 메타화면/Ember Rest/큐 패널/`Sim/**`,
그리고 다른 레인 소유 파일(EnvironmentBuilder, VfxDirector, CameraRig, GameView)은
무접촉이다(`git status --short`로 확인).

### 3.1 `CommandConsoleBuffer.AppendComposed`가 왜 필요한가

기존 `Feed(char, frame)`은 **같은 프레임에 도착한 동일 문자**를 하나로 접는다. 두 이벤트
소스가 한 키를 두고 경쟁하는 상황의 서명이기 때문이다. 그런데 IME 커밋은 *하나의 이벤트가
여러 문자를 한꺼번에* 나르므로, `"ㅋㅋ"` 커밋을 `Feed`로 흘리면 절반이 조용히 사라진다.
`AppendComposed`는 에코 가드를 건너뛰고, 커밋 직후 중복 창을 닫아
(`_hasAccepted = false`) 커밋과 같은 문자를 바로 타이핑해도 먹히지 않게 한다.
두 성질 모두 아래 뮤테이션 M3가 문다.

### 3.2 BuildScript 영향 없음 [OBSERVED]

`Assets/WebGLTemplates/`는 존재하지 않는다. 빌드는 스톡 템플릿을 쓰고
`BuildScript.PolishIndexHtml`(`Assets/Editor/BuildScript.cs:172`)이 산출물
`build-webgl/index.html`을 후처리한다. 숨은 input은 **런타임에 jslib가 생성**하므로
index.html에 들어갈 마크업이 없고, `VerifyWebGlShell`의 필수 마커
(`Assets/Editor/BuildScript.cs:374-394`)와도 무관하다. BuildScript 변경 0줄.
`ExcludeEditorToolingFromWebGl`은 `Assets/Plugins/NuGet/` 경로만 훑으므로
`Assets/Plugins/WebGL/hangul_ime.jslib`는 그대로 링크된다(`storage.jslib`와 동일 경로·동일
메타 형식).

## 4. 검증 [OBSERVED] — Unity 미실행, 대역외 컴파일 + 실행

Unity 실행 금지 제약이라 Unity가 마지막 컴파일에 쓴 응답 파일
(`Library/Bee/artifacts/*.dag/*.rsp`)을 그대로 재사용해 Roslyn으로 컴파일했다.
정의(define) 집합·참조 집합이 Unity와 동일하다.

| # | 명령 | 결과 |
|---|---|---|
| C1 | `dotnet exec .../Roslyn/bincore/csc.dll @view-editor.rsp` (2000b0aEDbg.dag = **에디터** 정의) | exit 0, 경고 0 |
| C2 | 같은 컴파일러 `@view-webgl.rsp` (2000b0aP.dag = **WebGL 플레이어** 정의, `UNITY_WEBGL` 있음 / `UNITY_EDITOR` 없음) | exit 0 |
| C3 | `@tests.rsp` (EditMode 테스트 어셈블리 전체 + 신규 테스트) | exit 0, error 0 |
| J1 | `node --check` (`{{{ makeDynCall }}}` 매크로만 치환) | JS SYNTAX OK |

C2가 중요한 이유: `[DllImport("__Internal")]`, `AOT.MonoPInvokeCallback`,
`Marshal.PtrToStringUTF8`, `WebGLInput.captureAllKeyboardInput` — WebGL 전용 분기는
에디터 컴파일에서 아예 배제되므로 C1만으로는 문법조차 검증되지 않는다.

C3에서 `Assets/Tests/EditMode/EnvironmentBuilderTests.cs`는 제외했다.
`CameraRig.DungeonCrowdDistance` 미정의로 깨지는데 이는 **다른 레인의 진행 중 변경**이며
이 레인 변경과 무관하다(내 변경 파일 목록에 `CameraRig.cs` 없음).

### 4.1 순수 C# 테스트 실행

`CommandConsoleImeComposition`·`CommandConsoleBuffer`는 UnityEngine 비의존이라
최소 NUnit 대역(attribute + Assert 셰임)으로 **실제 테스트 파일을 그대로** 컴파일해
실행했다(`csc -langversion:9` + `mono`).

```
passed=29 failed=0
```

29 = 신규 `CommandConsoleImeCompositionTests` 16 + 기존 `CommandConsoleBufferTests` 13
(`[Test]` 8 + `[TestCase]` 5).

신규 16 케이스가 고정하는 계약:

| 테스트 | 계약 |
|---|---|
| `TheComposingSyllableIsReplacedNeverAppended` | ㄱ→가→각은 **교체** 3회 |
| `CommittingTheSyllableMovesItIntoTheCommandText` | end에서만 확정 텍스트로 이동 |
| `ANewCompositionAfterACommitStartsFromAnEmptyPreEdit` | 새 조합이 앞 음절을 상속하지 않음 |
| `BackspaceWhileComposingEatsThePreEditAndNeverTheCommittedText` | 조합 중 백스페이스는 확정 텍스트 불가침 |
| `AShorteningCompositionUpdateIsTheImeOwnBackspace` | 실제 브라우저가 보내는 짧아진 pre-edit |
| `CancellingAKeepsTheCommittedTextAndDropsThePreEdit` | ESC = 음절 취소, 콘솔 유지 |
| `EnglishTypingAndHangulCompositionShareOneCommandLine` | `"nova 각!"` 영한 혼용 |
| `APlainInsertCommitsAnyLivePreEditFirst` | 살아있는 pre-edit는 버리지 않고 확정 |
| `ARepeatedSyllableInsideOneCommitSurvivesTheEchoGuard` | `"ㅋㅋ"` 단일 커밋 보존 |
| `AKeystrokeMatchingTheJustCommittedCharacterIsNotAnEcho` | 커밋이 중복 창을 닫는다 |
| `TheCharacterLimitCountsTheLiveSyllableToo` | 60자 상한에 pre-edit 포함 |
| `ControlCharactersReachNeitherThePreEditNorTheCommit` | 제어문자 차단 |
| `DeleteBackwardOutsideACompositionRemovesCommittedText` | 비조합 백스페이스 경계 |
| `FlushCommitsThePreEditSoAnEarlyEnterKeepsTheSyllable` | 한 박자 이른 Enter에도 음절 보존 |
| `ClearEndsAnyLiveCompositionSoTheNextSessionOpensClean` | 세션 경계 |
| `ACompositionEndWithNoTextIsACancelNotACommit` | Chrome의 `compositionend(data:"")` |

### 4.2 뮤테이션 검증 (테스트가 실제로 무는지)

스크래치패드 사본에서만 변형. 저장소 트리는 그대로.

| # | 변형 | 결과 |
|---|---|---|
| M1 | `UpdateComposition`이 교체 대신 **append** | 29 중 6 실패 (`…ReplacedNeverAppended`가 `ㄱ가각` 검출) |
| M2 | 조합 중 백스페이스가 pre-edit을 건너뜀 | 29 중 1 실패 (`BackspaceWhileComposing…`) |
| M3 | 커밋을 `AppendComposed` 대신 문자별 `Feed`로 | 29 중 2 실패 (`"ㅋㅋ"`→`"ㅋ"`, 중복 창 미해제) |

원복 후 재실행: `passed=29 failed=0`.

### 4.3 구현 중 잡은 실제 결함

첫 통과 시 `Insert`가 살아있는 pre-edit을 `EndComposition("")`으로 **버렸다**
(`APlainInsertCommitsAnyLivePreEditFirst`가 `가x` 대신 `x`를 관측). 플레이어가 이미 화면에서
본 음절이 사라지는 동작이라 `EndComposition(_composition, frame)`로 고쳤다
(`CommandConsoleImeComposition.cs:117`).

## 5. 배포 후 실검증 절차 (수동 — 아직 미측정 [TARGET])

전제: `Unity -batchmode -executeMethod CinderCourt.EditorTools.BuildScript.BuildWebGL`
→ `python3 -m http.server` → 브라우저에서 상대 경로로 로드. 던전 진입 후 **Enter**로 콘솔을 연다.

각 브라우저에서 아래를 순서대로:

| # | 입력 | 기대 화면 |
|---|---|---|
| 1 | (한글 IME로) `ㄱ` | 콘솔에 `ㄱ` 1글자. **두 개가 아님** |
| 2 | 이어서 `ㅏ` | `ㄱ`이 `가`로 **바뀜**(`ㄱ가` 아님) |
| 3 | 이어서 `ㄱ` | `각`으로 바뀜 |
| 4 | Backspace | `가` |
| 5 | Space (음절 확정) | `가 ` — 확정 후에도 1개 |
| 6 | `집중공격` 입력 후 Enter | 콘솔 닫히고 워든이 집중공격 명령 수행, 중복 문자 없음 |
| 7 | Enter로 재개 → `nova` 영문 입력 | `nova` (한글 경로가 영문을 깨지 않음) |
| 8 | 조합 중(`ㄱ` 상태) ESC | **콘솔은 열린 채** 음절만 사라짐 |
| 9 | 비조합 상태에서 ESC | 콘솔 닫힘, 게임 복귀 |
| 10 | 콘솔 열고 캔버스 클릭 후 계속 타이핑 | 글자가 계속 들어감(blur 재포커스) |
| 11 | 콘솔 닫은 뒤 이동 키(WASD) | 워든 정상 이동 — 키보드가 Unity로 돌아왔는지 확인 |
| 12 | 61자 이상 입력 시도 | 60자에서 멈춤, 조합 중 음절도 상한에 포함 |

대상 브라우저(최소): macOS Chrome, macOS Safari, Windows Chrome, Windows Edge,
iOS Safari, Android Chrome. 각각 OS 한글 IME(두벌식) 사용.

추가 확인:
- 개발자 도구 Elements에서 `#cinder-ime-input`이 콘솔 열림과 함께
  `document.activeElement`가 되는지, 닫으면 캔버스로 포커스가 돌아오는지.
- Console에 `[WebGLHangulIme]` 경고가 찍히지 않는지(찍히면 역-P/Invoke에서 예외).

## 6. 리스크 [INFERENCE]

1. **iOS/Android 소프트 키보드가 안 뜰 수 있다 (가장 큼).** 모바일 브라우저는 실제
   사용자 제스처 콜스택 안에서 호출된 `focus()`에만 키보드를 연다. 콘솔은 Unity 프레임
   처리 중(제스처 핸들러 밖)에 열리므로 iOS Safari가 무시할 개연성이 높다. 지금은
   **콘솔을 여는 경로가 Enter 키뿐**(`ToggleCommandConsole` 호출자가 테스트 외 없음 —
   `grep -rn "ToggleCommandConsole" Assets/`)이라 모바일에서 콘솔 자체에 도달할 수 없고,
   따라서 이 리스크는 현재 미노출이다. 모바일 콘솔 버튼이 생기면(UI 레인) DOM 터치
   핸들러에서 직접 `focus()`를 부르는 추가 작업이 필요하다.
2. **브라우저별 IME 이벤트 순서차.** `insertCompositionText` 위치(Chrome: end 앞,
   Firefox: end 뒤), Safari의 `isComposing` 누락(→ `keyCode 229`로 보강),
   Firefox의 `compositionupdate` 발행 빈도 차이. 세 조건 필터로 방어했으나
   실브라우저 측정 전까지는 [INFERENCE]다.
3. **`WebGLInput.captureAllKeyboardInput = false` 부작용.** 콘솔이 열린 동안 Unity가
   키보드를 전혀 못 본다. 콘솔 밖 단축키가 그 사이 죽는데, 콘솔 열림 중에는
   `InputAdapter.TextInputActive`로 이미 게이트되어 있어 의도와 일치한다.
   다만 `CloseCommandConsole`이 어떤 경로로든 반드시 불려야 복구된다 — 런 종료 시
   강제 닫기(`HudView.cs:1026`)가 이미 그 역할을 한다.
4. **브리지 설치 실패 시.** `CinderImeOpen`이 0을 반환하면 `Open`이 즉시
   `captureAllKeyboardInput`을 되돌리고 false를 반환하므로 기존
   `Keyboard.onTextInput` 미러가 그대로 동작한다(= 지금 배포본과 동일 동작, 한글 조합만
   불가). 회귀는 없다.
5. **한 프레임 지연.** DOM 이벤트가 프레임 사이에 도착해 다음 `UpdateCommandConsole`에서
   화면에 반영된다. 60 fps에서 최대 16.7 ms — 타이핑 체감상 무시 가능하지만
   `timeScale 0.2` 상태에서도 `UpdateCommandConsole`이 매 프레임 도는지는 실검증 항목 1~3에
   포함된다.

## 7. 미해결 / 사람 판단

- **Unity 실검증 미실시** (제약: Unity 실행 금지). EditMode 러너 실측치와 WebGL 빌드
  산출물은 통합 검증 단계에서 필요하다. 위 §4는 Unity와 동일한 define/reference 집합의
  대역외 컴파일 + 순수 C# 실행까지다.
- **실브라우저 IME 측정 0건.** §5 체크리스트가 그 계획이다. 배포 전 최소
  Chrome/Safari 2종은 사람이 돌려야 한다.
- 모바일 콘솔 진입점 부재(§6-1)는 UI 레인 범위로 판단해 이 레인에서 만들지 않았다.
