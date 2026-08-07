# HongT — 명령 에이전트 (텍스트 커맨드 → 순차 실행)

Repo: `~/orca/workspaces/HongT/main` (Unity 6000.5.6f1 / URP / WebGL).
구현: `Assets/Scripts/View/CommandPlan.cs`(계획·파서),
`Assets/Scripts/View/CommandAgent.cs`(러너),
`Assets/Scripts/View/HudView.CommandAgent.cs`(글루),
`Assets/Scripts/View/GeminiCommandClient.cs`(`Plan` 코루틴),
`Assets/Editor/GeminiDevKey.cs`(에디터 전용 키 로더).
게이트: `Assets/Tests/EditMode/CommandPlanParserTests.cs`(20),
`Assets/Tests/EditMode/CommandSequenceRunnerTests.cs`(15).

기존 콘솔은 한 문장 → **의도 1개**였다. 이 레인은 한 문장 → **순서 있는 시퀀스**로
바꾸고, 각 단계가 **시뮬레이션이 끝냈다고 말한 뒤에만** 다음으로 넘어가게 한다.

## [OBSERVED] 규칙표 우선순위는 시퀀스에 그대로 쓸 수 없다

`CompanionCommandParser.Parse`는 **규칙 순서**로 첫 매치를 돌려준다(구체 규칙이
일반 규칙보다 위). 그래서 "노바 쓰고 결계 쳐"는 규칙표에서 Aegis가 Nova보다
위라는 이유만으로 `SkillAegis` **하나**가 되고 노바는 사라진다.

시퀀스는 문장을 따라야 하므로 **위치 순서** 스캔이 필요하다:
`CompanionCommandParser.TryMatchAt(text, index, out intent, out length)`가
"이 인덱스에서 시작하는 키워드"만 답하고, `CommandPlanParser.ParseLocal`이
매치 길이만큼 커서를 밀며 스캔한다. 결과 `[Nova, Aegis]`.

한국어 접속사("쓰고/그리고/한 뒤")로 문장을 **쪼개지 않는다** — 동사 어미 목록은
반드시 썩는다. 키워드 자체가 구분자다.

부수 효과 2가지(둘 다 테스트로 고정):
- 대소문자 폴딩은 **문자 단위**로 한다. `ToLowerInvariant()`를 문자열 전체에
  걸면 문화권에 따라 길이가 바뀌어 이후 인덱스가 전부 밀린다.
- 규칙 내부에서는 **가장 긴 키워드**가 이긴다. 안 그러면 "방어태세"가 "방어"로
  먼저 걸려 Defend가 두 번 나온다.

## [OBSERVED] "이벤트가 끝났다"는 4단계로만 정직하게 표현된다

`CommandSequenceRunner`의 단계별 상태기계 (`CommandAgentSpec`이 수치 소유):

| 단계 | 조건 | 실패 시 |
|---|---|---|
| Gate | 쿨다운 ≤ 0 · 기름 ≥ 비용 · (동료 명령이면) 슬롯 > 0 | 시퀀스면 `GateTimeout`(= `HackSpec.AegisCooldown + 2` = 14 s)까지 대기 후 사유(`쿨다운`/`기름 부족`)와 함께 스킵 |
| Dispatch | 키 입력과 **같은 래치**를 1회 세트 | — |
| Ack | 심이 받았다는 증거: 쿨다운이 디스패치 시점보다 `AckEpsilon`(0.05) 이상 상승 / `CompanionBehavior`가 명령한 값 / 동료 시전 플래시 | `AckTimeout` 1.5 s → "반응 없음" 보고 후 다음 단계로 |
| Settle | 이벤트 창(스킬 0.45 s · 대시 0.3 s · 태세 0.35 s · 동료 특기 0.6 s) | — |

핵심은 **Ack가 관측이지 가정이 아니라는 것**이다. 기름이 모자라 심이 래치를
무시하면 쿨다운이 오르지 않고, 러너는 성공을 꾸며내는 대신 "반응 없음"을 띄운다.

## [OBSERVED] 단일 명령은 큐잉하면 안 된다

시퀀스의 Gate 대기는 기능이지만, 혼자 친 "노바"에 그대로 적용하면 8초 뒤에
갑자기 터지는 **유령 시전**이 된다. 그래서 `Begin`에서
`_gateTimeout = plan.IsSequence ? GateTimeout : 0`으로 갈라놓았다. 단일 명령은
기존 콘솔 의미(지금 쏘거나, 왜 못 쏘는지 말하거나)를 100% 유지한다.

`동료 없음`만은 예외로 **대기 없이 즉시 스킵**한다. 기다려서 해결될 수 있는
게이트(쿨다운·기름)와 달리, 없는 동료는 14초 뒤에도 없다.

## [OBSERVED] Gemini 응답은 JSON 안의 JSON이다 — 이스케이프를 건너뛰면 안 된다

`generateContent`는 계획을 `candidates[0].content.parts[0].text`에 **문자열로**
담아 돌려준다. 즉 계획의 모든 `"`는 `\"`로 도착한다. 기존
`GeminiCommandClient.ExtractFirstText`는 `if (c == '\\') { i++; continue; }`로
이스케이프를 **버렸다** — 의도 단어 1개일 때는 무해했지만 계획 payload에서는
문서를 부순다. 지금은 `\" \\ \n \uXXXX`를 전부 디코드하고 길이 상한을
인자로 받는다(단어 64 · 계획 2048).

파서는 코드펜스/산문 래핑도 벗긴다(첫 `{`/`[` ~ 마지막 `}`/`]`). 모델이
`responseMimeType`을 무시해도 콘솔이 막히지 않는다.

## [OBSERVED] 배포 키는 만료됐고, 그래서 실패 문구에 상태코드를 넣었다

`.env.game-audio`의 `GEMINI_API_KEY`는 2026-08-07 기준 **모든 모델에서 429**
(`Your prepayment credits are depleted`, `gemini-2.5-flash-lite` /
`gemini-2.5-flash` / `gemini-2.0-flash` 동일). 따라서 라이브 왕복은 이 세션에서
검증 불가다.

그 결과 실패 경로를 "해석 실패" 한 줄로 뭉개지 않고 `(요청 실패 429)`처럼
**HTTP 상태를 그대로** 콘솔에 노출하도록 만들었다. 키 소진과 응답 파손은 다른
문제이고, 플레이어가 조치할 수 있는 건 전자뿐이다.

## [INFERENCE] 에디터 키 로더는 보안 계약을 깨지 않는다

`Assets/Editor/GeminiDevKey.cs`는 `.env.game-audio`(gitignore `.env*`)를 읽어
PlayerPrefs에 넣는다. `Assets/Editor/`는 모든 플레이어 빌드에서 제거되므로
"키는 빌드·저장소에 없다"는 계약은 유지된다. 이미 저장된 키가 있으면 덮어쓰지
않는다(사람이 콘솔로 넣은 키가 우선).

## [OBSERVED] 검증 우회로: 에디터 점유 시 msbuild + 표준 harness

Unity 에디터가 다른 세션에 점유(pid 16568)되면 `-runTests`는 불가능하다. 대신:
1. `msbuild CinderCourt.View.csproj` / `Assembly-CSharp-Editor.csproj` — Roslyn
   컴파일 게이트(생성된 csproj는 gitignore, 에디터가 재생성).
2. 순수 C# 부분은 `/tmp`에 net8.0 콘솔 하네스를 만들고 `NUnit` 3.14 패키지 +
   빌드된 `CinderCourt.View.dll`을 참조해 리플렉션으로 픽스처 실행.
   **하네스 어셈블리 이름을 `CinderCourt.Tests.EditMode`로 맞춰야**
   `InternalsVisibleTo` 덕에 `internal` 멤버(`ExtractFirstText`)가 보인다.
3. `mcs`는 쓰지 말 것 — Mono 6.12는 참조 어셈블리의 `in` 파라미터를 `ref`로
   읽어 100개 넘는 가짜 CS1620을 만든다(코드 결함 아님).
