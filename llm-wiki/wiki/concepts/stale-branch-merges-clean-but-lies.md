# 오래 방치된 브랜치는 "충돌 없이" 병합되면서 거짓말을 들여온다

Repo: `~/orca/workspaces/HongT/main` (Unity 6000.5.6f1 / URP / WebGL).
사례: `origin/docs/ai-native-builder-flow` (cb82427) → `main` 병합, 2026-08-07.
관련 계약: `CLAUDE.md` §4(증거 표기·수치 게이트), §5(동시 세션 git 안전).

## [OBSERVED] 병합 충돌 0건과 내용 정확성은 서로 무관하다

cb82427은 `2db4942`에서 분기했고, 병합 시점 기준 main보다 **94커밋** 뒤였다.
그런데도 `git merge`는 완전히 조용했다 — 추가 경로 `docs/ai-native-builder/`
5파일이 main에 아예 없었기 때문이다. ort 전략은 "겹치는 줄이 없다"만 판정하고,
"이 문장이 아직 참인가"는 판정하지 않는다.

실제로 그 5파일이 들여온 낡은 주장:

| 주장 | 브랜치 시점 | 병합 시점 실측 | 확인 방법 |
|---|---|---|---|
| EditMode 게이트 | 166/166 | **502/502** | `_workspace/current/engineering/unity-logs/test-results-135336.xml` (`total=502 passed=502 failed=0`) |
| Campaign 레인 범위 | 6단계 던전 | **9단계** | `Assets/Scripts/View/StageCatalog.cs`의 `new StageEntry` 9건 |
| 레인 런북 위치 | `_workspace/current/engineering/gjc-*.md` | `_workspace/archive/20260805-visible-impact/engineering/` | cycle-1 종료 시 아카이브 이동. current에는 `view-lane-cycle2.md`만 잔존 |

수치는 md 본문뿐 아니라 **SVG 텍스트 노드에도 박혀 있었다**(`166/166` 2곳,
`6단계` 1곳). 그림은 grep 대상에서 빠지기 쉽다.

## [OBSERVED] 파생 산출물은 소스보다 조용히 낡는다

같은 디렉터리의 `.html` / `.pdf` / `.png`는 `.md` / `.svg`에서 생성된 파생물인데,
git은 이 의존을 모른다. 소스만 고치면 PDF는 계속 `166/166`을 인쇄한다.
문서가 재생성 명령을 자체 포함하고 있던 것이 유일한 구제책이었다 —
`rsvg-convert` → `pandoc gfm→html5` → Chrome headless `--print-to-pdf`.

단, 그 명령 자체가 존재하지 않는 파일명(`ai-native-builder.md`, `.pdf`, `.html`
— 실제는 전부 `ai-native-builder-flow.*`)을 가리켜 **복붙하면 실패**했다.
재생성 명령을 문서에 적었다면, 그 명령이 실제로 도는지 한 번은 실행해야 한다.

## [INFERENCE] 규칙: 오래된 브랜치 병합은 2단계다

1. `git merge` — 경로 충돌 해소.
2. **분기점 이후 main이 바꾼 사실 재검증** — 병합된 파일이 인용하는 수치·경로를
   전부 실측과 대조. `git log --oneline <merge-base>..origin/main`으로 그 사이
   무엇이 바뀌었는지 먼저 읽는다.

2단계를 별도 커밋으로 분리하면 "무엇이 남의 원본이고 무엇이 내 정정인지"가
diff에 남는다(여기서는 `af85491` 병합 / `07711c8` 정정).

체크리스트 (병합된 문서에 대해):
- 테스트 통과 수 · 스테이지/자산 개수 등 **모든 정수**를 최신 아티팩트와 대조
- 인용된 저장소 경로가 아직 존재하는가 (`_workspace/current/` ↔ `archive/` 이동)
- 머신 절대경로(`/Users/<남의계정>/...`)가 섞여 들어왔는가
- `.svg` / `.png` / `.pdf` 안의 텍스트도 같은 수치를 들고 있는가
- 문서에 적힌 재생성 명령을 실제로 한 번 실행

## [OBSERVED] 부수 확인: 배포는 다시 안 해도 됐다

병합 후 `git diff --name-only de476cc..HEAD -- Assets Packages ProjectSettings`가
0건 → gh-pages `b4ef488`(de476cc 기준 빌드)이 여전히 런타임 최신.
"main이 움직였으니 재배포"가 아니라 **런타임 경로가 움직였는지**로 판단한다.
