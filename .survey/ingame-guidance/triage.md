# Triage

- Problem: Cinder Court 던전에 처음 들어온 플레이어가 **무엇이 승리이고, 무엇이
  패배이며, 아이템이 무엇을 하고, 조작이 무엇이며, 해저드가 무엇인지**를 화면에서
  알 수 있는 경로가 거의 없다. 안내 대상 **23종**(조작 9 + 아이템 4 + 기믹 6 +
  승패 2 + 돌발 2) 중 실제로 안내되는 것은 **조작 2종(이동·타격)뿐**이며, 그 2종마저
  `HudView.cs:958-972`의 4단계 프롤로그 토스트로 **프롤로그에서만** 뜬다
  (`DesktopPrologueSteps` / `TouchPrologueSteps`, 실측 4개 문자열) `[OBSERVED]`.
  아이템 4종(`SimTypes.cs:18` `PickupKind`)과 기믹 6종(`CampaignTypes.cs:13-21`
  `HazardKind` = EmberVent / ObsidianPillar / RelicAltar / TideCurrent / EmberPylon /
  AshWall)은 설명 문자열이 **0곳**이다 `[OBSERVED]`.
  동시에 **런을 중단할 경로가 0**이다 — 이기거나 죽어야만 던전에서 나온다.
  본 서베이는 구현 전에 **로그라이트/액션RPG 장르가 이 안내를 언제 어떤 방식으로
  주는가, 그리고 런 중단 경로를 어떻게 주는가**를 실측 빈도로 확정한다.
  조사 전용이며 코드는 건드리지 않는다.

- Audience:
  - **1차 — Stage 1b 설계자/구현자**: 이 문서의 12축 빈도 교차표가 무엇을 채택하고
    무엇을 기각할지의 유일한 근거다. 특히 **정지 예산 8회가 장르 대역 안인가**의
    판정은 §solutions.md "정지 빈도 정량화" 표에서만 나온다.
  - **2차 — QA(`GuidanceQA`)**: 표본 5타이틀 공유 합의 완료
    (Hades / Dead Cells / Risk of Rain 2 / Vampire Survivors / Slay the Spire).
    QA가 Into the Breach·Returnal 2건을 본 풀에서 당겨 총 8타이틀로 계측한다.
    본 레인이 "첫런 정지횟수 + 강제확인 여부 + 1회 길이"를 제공하고, QA가 그 위에
    "안내 1건당 단어수"와 "커버리지율"을 얹는다.
  - **3차 — 신규 플레이어**: 실패 사례 6건이 **양방향**으로 갈린다 — 3건은
    "외부 위키 없이는 규칙을 알 수 없음"(부족), 3건은 "안내를 끌 수 없음"(과잉).
    예산 8이 그 사이 어디에 앉는지가 이 문서의 존재 이유다.

- Why now:
  - **사용자 제보가 실측 결함으로 확인됐다**: "죽어도 로비로 못 간다"는 제보의 정체는
    `HudView.cs:655`의 `캠페인으로` 버튼이 `:279`의 사망 사유 텍스트에
    **26u 깔린 것**이다. 본 조사에서 산술을 독립 재현했다 — `Panel()`이
    `rect.pivot = anchorMin`(`HudView.cs:1867`)이므로 버튼은 앵커 (0.5,0),
    anchoredPosition (0,76), 높이 40 → **y ∈ [-34, 6]**. `Label()`은
    pivot (0,1)(`:1911`)이므로 텍스트는 anchoredPosition (0,-70), 높이 60 →
    **y ∈ [-20, 40]**. 겹침 **26u × 200u = 5200u²** `[OBSERVED]`.
    기능은 정상이고 화면이 숨겼다.
  - **deep-interview가 5라운드로 마감됐다**(ambiguity 17%, `PASSED`). 정지 8회,
    최초 1회 23비트, 몰수 + 확인 모달, 도감 양쪽이 확정됐다. **이 조사의 역할은
    그 결정을 뒤집는 게 아니라 장르 관례에 비추어 어떻게 구현할지를 채우는 것**이다.
    단, 정면 충돌 증거가 나오면 별도 절로 경고한다(→ §solutions.md 마지막 절).
  - **선행 조사 재활용 0**: `.survey/progression-navigation/`의 11축은 전부
    **로비 메타 진행 화면**이다. 인게임 안내 축이 하나도 없다. 새 조사가 필요하다.
  - **제약이 지금 결정을 강제한다**: 폰트 서브셋을 본 조사에서 재실측했다 —
    `Assets/Resources/Fonts/HudKorean.otf` cmap **501 글리프**, `·`(U+00B7) **없음**,
    `−`(U+2212) **없음**, `•`(U+2022) 있음, `—`(U+2014) 있음, ASCII `-` 있음
    `[OBSERVED]`. 23종 신규 문자열은 전부 이 501자 안에서 쓰거나
    `tools/gen_hud_font.sh` 재생성을 동반해야 한다.

---

## 조사 질문 (하나로 고정)

> **로그라이트/액션RPG는 조작·아이템·해저드·승패조건을 플레이어에게 언제 어떤
> 방식으로 가르치는가, 그리고 런을 중단하는 경로를 어떻게 주는가?**

## survey_run

```yaml
survey_run:
  primary_mode: market-landscape
  scope: medium
  evidence_floor: indexed-snippets-allowed
  output_language: user-language(한국어)
  needs_platform_map: false
  reuse_existing: false      # .survey/ingame-guidance/ 신규
  run_id: 20260807-ingame-guidance
```

## 표본과 축

- **표본 19 타이틀.** 직전 사이클과 비교 가능성을 위해 같은 풀을 유지하되
  온보딩이 특징적인 타이틀을 교체 투입했다. 요구 하한 12 타이틀을 상회.

  Hades, Hades II, Dead Cells, Slay the Spire, Rogue Legacy 2, Risk of Rain 2,
  Returnal, Vampire Survivors, Cult of the Lamb, Darkest Dungeon 2, Skul,
  Curse of the Dead Gods, Enter the Gungeon, Monster Train, Loop Hero,
  Hollow Knight, Celeste, Into the Breach, Slice & Dice.

- **직전 사이클 대비 변경**: Have a Nice Death / Children of Morta / Wizard of
  Legend 3건을 빼고 **Hollow Knight / Celeste / Into the Breach / Slice & Dice**
  4건을 넣었다. 사유 — 앞 3건은 메타 진행 화면 조사용 표본이었고 온보딩 문헌이
  얇다. 뒤 4건은 **교습 설계 자체가 논의 대상인 타이틀**이다(Celeste·Hollow Knight는
  무텍스트 환경 교습의 표준 인용 사례, Into the Breach는 정지 튜토리얼 + 스킵의
  드문 사례, Slice & Dice는 "미수행 동작만 제안"하는 상태 추적형 사례).

- **축 12개**: 지정 12축(G1~G12) 전수. 추가 축 없음 — 지정 축이 이미
  전달형태(G1/G2) · 열람(G3/G4) · 감수성(G5) · 분류(G6) · 시점(G7) ·
  규칙명시(G8/G9) · 이탈(G10/G11) · 통제권(G12)을 덮는다.

## 증거 규칙

- 검색은 영어, 산출은 한국어.
- 확인 못 한 셀은 `?`로 남기고 **N에서 제외**한다. 추측으로 채우지 않는다.
  본 런에서 `?`가 가장 많은 축은 **G6(12/19 미확인)**과 **G11(12/19 미확인)**이며,
  두 축은 N이 각각 7·5로 작다는 점을 판정에 반영했다.
- 구조적으로 축이 성립하지 않는 셀은 `-`(n/a)로 두고 N에서 제외한다
  (예: 도감이 없는 타이틀의 G4, 런 구조가 없는 Hollow Knight/Celeste의 G10/G11).
- 표기: `[OBSERVED]` 리포지토리 실측 / `[INFERENCE]` 추론 / `[TARGET]` 목표치.
- 출처 강도 라벨: 장르 사실은 전 항목 `indexed snippet`(검색 인덱스 경유),
  리포지토리 사실은 `direct page retrieval`(소스 직접 판독 + 산술 재현).
