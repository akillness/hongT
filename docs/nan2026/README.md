# NAN 2026 Game X AI 해커톤 — 사전 과제 제출물 (Unity 빌드)

**팀명**: Hong팀
**팀 프로젝트**: Abyssal Lantern — Hold the Cinder Court (Unity 6 / WebGL)
**팀원**: 정장영 (기획·게임구현·리소스제작) · 이석민 (기획·UI·QA) · 정우영 (기획·QA)

> 원작 Canvas 2.5D 제출본의 후속 빌드. 수치 계약을 보존한 채 Unity 3D
> 캐릭터·캠페인 모드·아이템 드롭·던전 기믹을 추가했다.

## 제출물 현황

| # | 제출물 | 제출 형태 | 이 저장소의 산출물 | 상태 |
|---|---|---|---|---|
| 1 | 플레이 가능한 빌드 및 소스 코드 | GitHub Pages + 전체 소스 | <https://akillness.github.io/hongT/> · <https://github.com/akillness/hongT> | **배포 완료** |
| 2 | 플레이 동영상 | YouTube 링크 (30~60초) | `assets/video/nan2026-cinder-court-unity-play.mp4` (48.1 s) | 캡처 완료 · **업로드 필요** |
| 3 | 게임 소개 및 설명 문서 | PDF | [`01-game-overview.md`](01-game-overview.md) | 마크다운 갱신 완료 · PDF 재생성 필요 |
| 4 | AI 활용 기술 문서 | PDF | [`02-ai-tech.md`](02-ai-tech.md) (0-bis Unity 증보 포함) | 마크다운 갱신 완료 · PDF 재생성 필요 |
| 5 | 팀원 롤 기술서 | PDF | [`03-team-roles.md`](03-team-roles.md) | 원본 유지 |

## 페이지 구성 (제출물 1)

- `/` — 아레나 방어전 (원작 Cinder Court 규칙, 무한 웨이브)
- `/campaign.html` — 메인 캠페인: 3구역 스테이지 선택, 진행도, 장비 파편

## 사람이 해야 하는 남은 작업

1. **YouTube 업로드** — `assets/video/nan2026-cinder-court-unity-play.mp4`를
   업로드하고 `01-game-overview.md` 4장에 링크 기재.
2. **PDF 재생성** — 원작 저장소의 `node scripts/build-nan2026-pdf.mjs`
   파이프라인(pandoc + rsvg-convert + D2Coding)을 이 폴더에 맞춰 재실행.
3. **신청서 제출** — 개인정보 수집·이용 및 저작권 동의 포함.

## 플레이 영상 재캡처

배포 빌드를 실제 브라우저 키 입력으로 플레이하며 CDP 스크린캐스트로
캡처했다 (프레임 합성·보간·재생성 없음). 재캡처는 오케스트레이션 세션의
브라우저 하니스로 수행한다 — 근거: 이 저장소 커밋 히스토리와
`docs/provenance/`.
