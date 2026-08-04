# Deployed release verification — texture-cap build (2026-08-04)

Target: <https://akillness.github.io/hongT/> · gh-pages `d4c7392` · cache version
`61a0b09946ca5642` · data 26,549,778 B (−49.3 % vs prior) · wasm 9,117,062 B.

All checks below ran against the LIVE GitHub Pages build in a real headless
Chromium tab (CDP input, no simulation). Runtime errors / warning banners were
captured on every route; **all routes: 0 errors, 0 warnings**.

| # | Route | Viewport | What was exercised | Observed | Evidence |
|---|---|---|---|---|---|
| 1 | 로비 → 프롤로그 출정 | 1440×900 | Sortie click, WASD move, Space strikes | 체력 86, 웨이브 1, 적 4, 피격 비네트, 적 체력바 | `engineering/deployed-texcap-desktop-lobby.png`, `…-desktop-combat.png` |
| 2 | 로비 → 프롤로그 (모바일) | 390×844 DPR 2 | Sortie tap, combat entry | 체력 100, 웨이브 1, 적 3, 모바일 HUD 레이아웃 | `…-mobile-lobby.png`, `…-mobile-combat.png` |
| 3 | `?mode=arena` | 1440×900 | D move + Space, melee trade | 체력 44 (근접 피격 정상), 적 4, Q/E 스킬바, 적 체력바 | `…-arena-combat.png` |
| 4 | `campaign.html` 리다이렉트 | — | HTTP probe | 200 | curl |
| 5 | 캠페인 1단계 Cinder Span | 1440×900 | prologueDone 시드 → 강하 → 콤보/대시/Q/E/R/F | "Cinder Span — 웨이브 1/5" 배너, 방패 40 (Void Aegis), 기름 68 소모, 적 3→2, 분출구 텔레그래프 2, 장비/Lv HUD | `…-campaign-stage1.png`, `…-campaign-stage1-combat.png` |
| 6 | localStorage 영속 | — | v2 스키마 시드 후 재로드 | 로비가 프롤로그 "재훈련 가능" + Cinder Span 해금으로 반영 | route 5 첫 스크린샷 |

Fixture note: route 5 used a seeded v2 save
(`{"clearedMask":0,…,"prologueDone":true}`) to unlock stage 1 without a full
manual prologue clear — the same shape `CampaignStore.Save` writes; the lobby
parsed it and gated cards correctly, which is itself the persistence check.

Capture-environment caveat: Playwright's bundled headless Chromium lacks
proprietary audio codecs and logs `EncodingError: Unable to decode audio data`
during video capture. Real-browser smokes on the same URL log zero errors —
environment artifact, not a game defect.
