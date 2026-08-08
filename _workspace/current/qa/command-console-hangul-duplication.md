# QA — 명령 콘솔 한글 중복 입력 수정

대상: `Assets/Scripts/View/HudView.cs` (companion command console),
신규 `Assets/Scripts/View/CommandConsoleBuffer.cs`.
검증: Unity 6000.5.6f1 EditMode, `test-results-153251.xml` (276/276 Passed).

## [OBSERVED] 증상

명령 콘솔(던전에서 Enter)에 한글을 입력하면 음절이 두 번씩 들어갔다("한" → "한한").

## [OBSERVED] 원인 — 같은 InputField에 writer가 둘

1. **uGUI InputField 자체 경로.** `InputField.OnUpdateSelected`가 IMGUI 이벤트를
   꺼내 `KeyPressed` → `Append(c)`를 호출한다
   (`Library/PackageCache/com.unity.ugui@67707a67a4ab/Runtime/UGUI/UI/Core/InputField.cs:2034,1980,2444`).
   IME가 확정한 한글 음절은 이 경로로 들어온다.
2. **HudView의 수동 미러.** 이 프로젝트는 `activeInputHandler: 1`
   (`ProjectSettings/ProjectSettings.asset:954`, 신규 Input System 전용)이라
   uGUI가 읽는 레거시 `Input.inputString` 스트림이 죽어 있다. 그래서
   `OpenCommandConsole`이 `Keyboard.onTextInput`을 구독해
   `_consoleField.text`에 직접 문자를 덧붙였다.

두 writer가 같은 문자를 각각 한 번씩 써서 정확히 2배가 됐다. ASCII는 IME를
거치지 않아 (1)이 문자를 싣지 않는 경우가 많아 증상이 한글에서 두드러졌다.

## [OBSERVED] 수정

- `_consoleField.readOnly = true` — uGUI의 `Append`/`Backspace`/`ForwardSpace`는
  readOnly에서 즉시 반환하므로(같은 파일 `2286/2306/2329/2354/2420/2450`)
  writer (1)이 구조적으로 죽는다. `ActivateInputField`의
  `imeCompositionMode = On`(`3191`)과 캐럿은 readOnly와 무관하게 유지된다.
  `text` 프로퍼티 setter는 readOnly 게이트가 없어 미러는 그대로 동작한다.
- 편집 규칙 전체를 순수 C# `CommandConsoleBuffer`로 분리(제어문자 차단,
  백스페이스, 60자 상한). 콘솔 필드는 이 버퍼의 읽기 전용 표시다.
- 버퍼는 **같은 프레임에 도착한 동일 문자**를 한 번만 받는다. 중복 이벤트
  소스의 서명이며, 사람 타이핑·OS 키 리피트(≤33 Hz)는 60 fps 기준 항상 다른
  프레임에 떨어지므로 "ㅋㅋ" 같은 정상 연타는 그대로 통과한다.
- `onTextInput` 구독을 구독했던 **그 디바이스**(`_consoleTextKeyboard`)에서만
  해제하고, 이미 구독 중이면 다시 구독하지 않는다. `Keyboard.current`가 콘솔이
  열린 사이 바뀌면 이전 구독이 살아남아 다음 세션 전체가 2배가 됐다.

## [OBSERVED] 테스트 (신규 16 케이스, 259 → 276)

| 테스트 | 고정하는 계약 |
|---|---|
| `CommandConsoleBufferTests.TheSameCharacterDeliveredTwiceInOneFrameIsAcceptedOnlyOnce` | 같은 프레임 에코 1회만 수용 |
| `…ARealDoubleLetterOnTheNextFrameIsKept` | 정상 연타는 유지 |
| `…DifferentCharactersInOneFrameAreBothKept` | 동일 문자만 에코로 본다 |
| `…BackspaceRemovesTheLastCharacterAndIsANoOpWhenEmpty` | 삭제 경계 |
| `…RetypingTheSameCharacterAfterABackspaceInTheSameFrameIsKept` | 삭제가 중복 창을 닫는다 |
| `…ControlCharactersNeverEnterTheText` (5 케이스) | `\n \r \t ESC` + 임의 제어문자 |
| `…TheCharacterLimitIsHardAndLeavesTheTextIntact` | 상한 초과 거부 + 삭제 후 재수용 |
| `…ClearResetsTextAndTheDuplicateWindow` | 세션 경계 |
| `…PlainTypingAppendsEachCharacterExactlyOnce` | 기본 경로 |
| `CommandConsoleFieldTests.TheConsoleFieldNeverWritesItself` | `readOnly` + `ProcessEvent(KeyDown 'a')`가 무반응 |
| `…TheConsoleFieldKeepsTheSixtyCharacterCommandCap` | 필드 상한/단일행 |
| `…EachConsoleSessionStartsFromAnEmptyCommandLine` | 열 때 필드+버퍼 동시 초기화 |

## [OBSERVED] 뮤테이션 검증 (테스트가 실제로 문다)

클론 `/tmp/hongt-unity-test`에서만 변형, 저장소 트리는 그대로.

| # | 변형 | 결과 |
|---|---|---|
| M1 | `IsSameFrameEcho` → 항상 `false` | 276 중 1 실패 — `TheSameCharacterDeliveredTwiceInOneFrameIsAcceptedOnlyOnce` (exit 2) |
| M2 | `_consoleField.readOnly = true` 제거 | 276 중 1 실패 — `TheConsoleFieldNeverWritesItself` (exit 2) |

복원 후 재실행: `total=276 passed=276 failed=0 skipped=0`, exit 0.

## [OBSERVED] 실행 명령

```
rsync -a --delete Assets/Scripts/ /tmp/hongt-unity-test/Assets/Scripts/
rsync -a --delete Assets/Tests/   /tmp/hongt-unity-test/Assets/Tests/
cd /tmp/hongt-unity-test && rm -f results.xml tests.log && \
/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath /tmp/hongt-unity-test \
  -runTests -testPlatform EditMode \
  -testResults /tmp/hongt-unity-test/results.xml -logFile /tmp/hongt-unity-test/tests.log
# -quit 금지 (러너가 끝나기 전에 에디터가 닫혀 results.xml이 생기지 않는다)
```

아티팩트: `_workspace/current/engineering/unity-logs/test-results-153251.xml`,
같은 폴더 `tests-153251.log`(`*.log`는 gitignore, 로컬 보관).

## [INFERENCE] 남은 범위

- 배포 WebGL 프레임워크(`build-webgl/Build/build-webgl.framework.js.unityweb`)에는
  `compositionstart/update/end` 핸들러가 없다(emscripten `keydown/keypress/keyup`만).
  즉 브라우저 IME 조합 자체는 Unity WebGL이 받지 못하며, 이번 수정은 중복 writer를
  제거한 것이지 WebGL IME 지원을 추가한 것이 아니다. 브라우저에서의 한글 조합
  입력 지원 여부는 별도 항목(숨은 input 엘리먼트 플러그인)이며 아직 미측정이다.
