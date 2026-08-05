# NAN 2026 Game X AI 해커톤 — 사전 과제 제출물 (Unity 빌드)

**팀명**: Hong팀
**팀 프로젝트**: Abyssal Lantern — Hold the Cinder Court (Unity 6000.5.6f1 / URP / WebGL)
**팀원**: 정장영 (기획·게임구현·리소스제작) · 이석민 (기획·UI·QA) · 정우영 (기획·QA)

> 원작 Canvas 2.5D 제출본의 후속 빌드. 수치 계약을 보존한 아레나와 프롤로그,
> 6단계 던전 캠페인, Ember Rest 다음-방 준비 흐름을 Unity 3D 캐릭터로 제공한다.

## 제출물 현황

| # | 제출물 | 제출 형태 | 이 저장소의 산출물 | 상태 |
|---|---|---|---|---|
| 1 | 플레이 가능한 빌드 및 소스 코드 | GitHub Pages + 전체 소스 | <https://akillness.github.io/hongT/> · <https://github.com/akillness/hongT> | Unity 6000.5.6f1 WebGL 빌드 통과 · EditMode 166/166 통과 |
| 2 | 플레이 동영상 | YouTube 링크 (30~60초) | `assets/video/nan2026-cinder-court-unity-play.mp4` (55.0 s, 1440×900) | 배포 빌드 재캡처 완료 (로비→Ember Gallery 드레싱/프롭/블룸→명령 콘솔) · **업로드 필요** |
| 3 | 게임 소개 및 설명 문서 | PDF | [`01-game-overview.md`](01-game-overview.md) → [`pdf/01-game-overview.pdf`](pdf/01-game-overview.pdf) | 마크다운·PDF 재생성 완료 (166/166 반영) |
| 4 | AI 활용 기술 문서 | PDF | [`02-ai-tech.md`](02-ai-tech.md) → [`pdf/02-ai-tech.pdf`](pdf/02-ai-tech.pdf) (0-bis Unity 증보 + 명령 콘솔 포함) | 마크다운·PDF 재생성 완료 (166/166 반영) |
| 5 | 팀원 롤 기술서 | PDF | [`03-team-roles.md`](03-team-roles.md) → [`pdf/03-team-roles.pdf`](pdf/03-team-roles.pdf) | 마크다운·PDF 재생성 완료 |

## 페이지 구성 (제출물 1)

- `/` — 로비 기본 진입점: 프롤로그와 6단계 캠페인 선택
- `/?mode=arena` — 원작 Cinder Court 규칙의 무한 웨이브 아레나
- `/campaign.html` — 이전 링크 호환용 `/` 즉시 리다이렉트

## PDF 재생성

```bash
node tools/docs/build-nan2026-pdf.mjs        # 3종 전체
node tools/docs/build-nan2026-pdf.mjs --only 02   # 단일 문서
```

pandoc + XeLaTeX + rsvg-convert 필요. 본문 Apple SD Gothic Neo, 코드
D2Coding (`brew install --cask font-d2coding`). SVG 다이어그램은 빌드 시
벡터 PDF로 변환되어 포함된다.

## 사람이 해야 하는 남은 작업

1. **YouTube 업로드** — `assets/video/nan2026-cinder-court-unity-play.mp4`를
   업로드하고 `01-game-overview.md` 4장에 링크 기재.
2. **신청서 제출** — 개인정보 수집·이용 및 저작권 동의 포함.

## 플레이 영상 재캡처

배포된 GitHub Pages 빌드를 실제 브라우저에서 실제 키·마우스 입력으로
플레이하며 녹화한다 (프레임 합성·보간·재생성 없음).

```bash
node tools/video/capture-unity-play.mjs --seconds 55
```

Playwright 녹화 + CDP 입력. 로딩 스플래시 head-trim과 H.264 30 fps 트랜스코드만
후처리한다. 복귀 플레이어 시점을 위해 프롤로그 클리어 저장(`CampaignStore` v2
스키마)을 localStorage에 시드한다. headless 환경은 한글 IME 조합이 불가하므로
콘솔 명령은 파서의 영문 별칭(`shield`/`nova`)을 쓴다 — 화면 피드백은 한국어
그대로다. 근거: 이 저장소 커밋 히스토리와 `docs/provenance/`.
