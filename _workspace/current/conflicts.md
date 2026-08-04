# Cross-session conflicts log

## 2026-08-05 01:26 — CharacterRosterAnimationTests.cs 컴파일 차단

- **관측**: EditMode 게이트가 `CS0234: 'EditorTools' does not exist in 'CinderCourt'`로 전체 차단.
  파일은 다른 세션 산출물(untracked, 01:20경 등장 — 이 세션 전사에 생성 기록 없음).
- **원인**: 테스트가 `using CinderCourt.EditorTools`를 정적 참조하지만, `Assets/Editor/`에는
  asmdef가 없어 기본 `Assembly-CSharp-Editor`로 컴파일됨 — asmdef 어셈블리
  (`CinderCourt.Tests.EditMode`)는 기본 어셈블리를 참조할 수 없다(Unity 규칙).
- **판단**: 컴파일 에러는 `-testFilter`보다 선행하므로 필터 우회 불가. 전체 게이트가
  막혀 있어 대기 불가. **최소 침습 수정** 선택: `using` 1줄 제거 +
  `GetImportRoster()`의 정적 typeof를 AppDomain 어셈블리 순회 리플렉션으로 교체
  (테스트는 이미 필드를 리플렉션으로 읽고 있었음 — 의도 보존, 구조 변경 0).
- **비선택 대안**: `Assets/Editor/CinderCourt.EditorTools.asmdef` 신설은 다른 세션이
  작업 중인 코드의 컴파일 어셈블리를 바꾸는 구조 변경이라 기각.
- **후속**: 해당 세션이 정적 참조를 원하면 asmdef 신설 + 테스트 참조 추가로 대체 가능.
  이 수정은 그 결정을 막지 않는다.
