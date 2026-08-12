# 던전 가이드 — 스테이지별 개정 2026-08-12

**[OBSERVED]** 9 스테이지의 지배 기믹 정체성, 시각 톤, 환경 구성을 일괄 통합 정의.  
**[TARGET]** 기믹 계보(예고→숙달)가 환경 디자인과 일치하고, 각 스테이지의 텍스처·재질 언어가 그 기믹을 강화한다.  
**근거**: `Assets/Scripts/View/StageCatalog.cs` (Accent, Epithet), `StageHazardVisualCatalog.cs` (Palette, MaterialLanguage), `StageMood.cs` (light rig per stage), `docs/SIM_SPEC_DUNGEONS.md` AMENDMENT #5 (gimmick placement).

이 문서는 **"어느 던전이 어떻게 다른가, 왜 그렇게 보이는가"** 하나만 답한다.  
수치 진실은 `SIM_SPEC_CAMPAIGN.md`, `SIM_SPEC_DUNGEONS.md`, `SIM_SPEC_ENVIRONMENT.md` 그리고 코드가 갖는다.

---

## 준비: 기믹 6종의 시각·음향 언어

던전은 **새 몬스터가 아니라 새 배치**로 갈린다. 6가지 기믹 재료와 그것을 둘러싼 환경 톤으로 스테이지 정체성이 정해진다.

| 기믹 | 물리 역할 | 시각 톤 (Palette) | 음향 신호 |
|---|---|---|---|
| **분출구**(vent) | 주기 범위 공격 (반경 90) | 지열 동공, 용암 가장자리 주황 분사 | 울음 → 예고(청색 번개) → 폭발 음파 |
| **흑요석 기둥**(pillar) | 고정 장애물, 회피 구조 (반경 40) | 검은 거울 표면, 방사선 균열 | (무음 — 항상 거기 있음) |
| **제단**(altar) | 채널 지점(자발적 노출, 1.2초) | 연마 석판, 황금 문양 인레이 | 영혼의 울음(채널 중) → 기름 수수음 |
| **잿물 해류**(current) | 주기 푸시(밀기 200 u/s, 3.2초 활성) | 수평선 소용돌이, 흐름 화살표 데칼 | 물 흐름음 → 예고(황색 쉐브론) → 밀어내기 음 |
| **불씨 방벽주**(pylon) | 적 보호(HP 300, 오라 −60% 피해) | 불타는 기둥, 주변 공기 청색 소용돌이 | 겹겹이 울음(오라 범위 280) |
| **재의 장벽**(wall) | 침식 압축(최대 560px, 주기 23초) | 가장자리 검은 불길 커튼, 바닥 그을림 | 천천한 으르렁음 → 예고 점멸 → 쩌렁음 |

---

## Stage 0: Cinder Span (재의 다리) — 분출구 입문

**컨셉**: 온보딩. 낙장이 된 왕국의 첫 진입로. 화산재 다리의 대각선 두 기둥에서만 분출구가 터진다. **이 스테이지는 예측을 배우는 곳이다.** v1.0 이후 무변경 — 첫인상은 고정이다.

**지배 기믹**: `vent` ×2 (560,480) 위상 0 / (980,720) 위상 1.2  
**부보조 기믹**: 없음  
**보스**: Cinder Warden (Commander 틴트, 색 0.9/0.3/0.45)  
**환경 톤**: 담금질한 석다리 · 열에 그을린 상판 · 가장자리 흰 화산재 · Accent #F25A2B (0.95, 0.35, 0.17) 따뜻한 주황

**기믹 톤**: vent는 담금질 검은색(cold burn), 가장자리만 비취색(지열). 두 동공은 대칭이라 배치 자체가 규칙을 암시한다.

**라이트 리그**: 낮은 key pitch(34°), hard contrast (key 0.58 / fill 0.20)

---

## Stage 1: Ember Gallery (불씨 회랑) — 불씨 윤무

**컨셉**: 제국의 영광을 밝혔던 회랑 — 이제 분출구는 오직 집행의 규칙만 남겼다. 정확히 4기가 2.4초마다 시계방향 순환하며 터진다. **위상 규칙이 배치 수보다 중요하다.**(v1.2: 위상 0/0.6/1.2/1.8로 정규화, 기믹 계보 강화)

**지배 기믹**: `vent` ×4 (560,480)·(980,480)·(980,720)·(560,720) 위상 0/0.6/1.2/1.8 + `pillar` ×1 (768,604)  
**부보조 기믹**: 없음  
**보스**: Cinder Warden (밝은 색 0.95, 0.45, 0.16)  
**환경 톤**: 전시 갤러리 석판 · 불 자국의 동심원 문양 · 벽 열주 위로 솟은 분출 · Accent #F36D33 (0.95, 0.43, 0.20) 더 밝은 주황

**기믹 톤**: vent는 갤러리 흑색(formal), 점화 리듬이 원형으로 읽힌다. pillar는 지름길 차단용.

**라이트 리그**: 낮은 key(30°), 강한 대비 (key 0.60 / fill 0.18) — 불이 한쪽에서 태운다.

---

## Stage 2: Abyss Chancel (서약의 성당) — 흑요석 미로

**컨셉**: 서약이 새겨진 검은 성당 — 기둥 3개가 미로를 만든다. 분출구 1개는 조연. **약속은 구조 자체가 된다.**(v1.0 이후 무변경)

**지배 기믹**: `pillar` ×3 (640,500)·(900,700)·(768,604) + `vent` ×1 (1100,450) 위상 0.6  
**부보조 기믹**: 없음  
**보스**: Veil Tactician (Monarch 틴트, 색 0.56, 0.40, 1.0)  
**환경 톤**: 검은 석재 벽(강석) · 흰색 룬 음각 · 천정에서 떨어지는 물의 반사광 · Accent #8F66FF (0.56, 0.40, 1.0) 깊은 자주

**기믹 톤**: pillar는 검은 거울(obsidian), 음각 룬이 기둥과 일체. vent는 구석에 고립(조연 신호).

**라이트 리그**: 높은 key(52°), 부드러운 대비 (key 0.52 / fill 0.26) — 성당 내부의 확산광.

---

## Stage 3: Witness Well (증언의 우물) — 쌍 제단

**컨셉**: 기억이 고인 우물 — 대각선 양쪽에 제단이 섰고, 각각을 분출구가 감시한다. 물이 계속 깊게 고여 있다. **증언은 짝을 이룬다.**(v1.2: 대각선 배치, 위상 규칙)

**지배 기믹**: `altar` ×2 (560,500)·(980,700) + `vent` ×2 (560,700) 위상 0.3 / (980,500) 위상 1.5 + `pillar` ×1 (768,604)  
**부보조 기믹**: 없음  
**보스**: Veil Tactician (같은 색)  
**환경 톤**: 습지 석판(비취색) · 이끼 낀 침전 고리 · 중앙 기둥은 우물 정자처럼 · Accent #38C2A8 (0.22, 0.76, 0.66) 찬 비취

**기믹 톤**: altar는 연마 석판, 황금 문양. vent는 감시 분출. pillar는 우물 구조의 중심.

**라이트 리그**: 높은 key(56°), 밝은 fill (key 0.50 / fill 0.28) — 우물의 확산 반사.

---

## Stage 4: Echo Throne (메아리 왕좌) — 왕좌의 조류

**컨셉**: 왕권의 잔향 — 중앙 제단은 그대로지만, 약한 해류(밀기 120)가 동심원 채널을 밀어간다. **권력도 이제는 흐름에 불과하다.**(v1.2: 약한 해류 추가, 흐름 예고편)

**지배 기믹**: `altar` ×1 (768,604) + `current` ×1 약함(밀기 120) (768,604) 위상 0.3 + `vent` ×2 (500,700) 위상 0 / (1030,480) 위상 1.2  
**부보조 기믹**: 없음  
**보스**: Gate Sovereign (Monarch, 색 0.75, 0.3, 0.9)  
**환경 톤**: 남색 화강암 · 은색 맥(권위의 유산) · 동심 채널에 물이 흐른 자국 · Accent #7EC7FF (0.45, 0.78, 1.0) 밝은 청

**기믹 톤**: current는 약하므로 채널 경계로만 보인다. altar는 왕좌 밑단에 황금 문양. vent는 보좌 양쪽.

**라이트 리그**: 중간 key(24°, 가장 낮음), 깊은 대비 (key 0.56 / fill 0.14) — 보스 무대의 긴 그림자.

---

## Stage 5: Ash Verdict (재의 판결) — 판결의 방벽

**컨셉**: 법정의 최종 심판 — 제단 주변에 방벽주의 실드가 있어 어쩔 수 없이 그 아래서 싸운다. **법은 강제다.**(v1.2: pylon 추가, 제단 지배에서 pylon 소개로)

**지배 기믹**: `altar` ×1 (768,604) + `pylon` ×1 (960,540) + `vent` ×2 (560,480) 위상 0 / (980,720) 위상 1.2  
**부보조 기믹**: 없음  
**보스**: Gate Sovereign (황금빛, 색 0.87, 0.78, 0.41)  
**환경 톤**: 재 사암 · 황금 잉크 점수(판결 기록) · 방벽주 원형의 열기 소용돌이 · Accent #DEC869 (0.87, 0.78, 0.41) 황금

**기믹 톤**: pylon은 검은 원기둥, 주변이 청색으로 흔들림(오라). altar는 황금 문양(판결 증거). vent는 구석.

**라이트 리그**: 중간 key(38°), 중간 대비 (key 0.57 / fill 0.19).

---

## Stage 6: Cinder Sluice (재의 수문) — 해류 숙달

**컨셉**: 기억 배출지 — 위아래 두 줄의 해류가 반대 방향으로 흐르고(밀기 ±200), 중앙 안전 회랑을 분출구가 감시한다. **흐름을 타지 않으면 갇힌다.**(v1.1: 배치 기하 리튠, 기믹이 실제로 느껴짐)

**지배 기믹**: `current` ×2 (768,470) 밀기 +200 위상 0 / (768,740) 밀기 −200 위상 3.0 + `pillar` ×1 (768,604) + `vent` ×2 (500,604) 위상 0.9 / (1030,604) 위상 2.1  
**부보조 기믹**: 없음  
**보스**: Sluice Keeper (aquamarine)  
**환경 톤**: 고철 수문 그릴 · 습한 현무암 · 물에 깎인 바닥 채널 · 빠진 기름 웅덩이 · Accent #3FA8C8 (0.247, 0.659, 0.784) 찬 청록

**기믹 톤**: current는 반대 화살표 데칼. pillar는 중앙 안전 회랑의 닻. vent는 회랑 양 옆(감시).

**라이트 리그**: 높은 key(48°), 밝은 fill (key 0.54 / fill 0.24) — 수문의 반사광.

---

## Stage 7: Ember Bastion (불씨 요새) — 방벽 숙달

**컨셉**: 이단자의 요새 — 3개 방벽주의 오라가 전장을 덮고, 2개 기둥이 진입로를 꺾는다. **방어는 형태가 된다.**(v1.1: 방벽주 3기, 오라 280 확보, 오라 내 적 시각적 표시)

**지배 기믹**: `pylon` ×3 (560,500)·(980,700)·(768,430) + `pillar` ×2 (640,650)·(900,560) + `vent` ×1 (768,604) 위상 0.6  
**부보조 기믹**: 없음  
**보스**: Bastion Sentinel (따뜻한 주황, 색 0.910, 0.541, 0.180)  
**환경 톤**: 철판 갑옷 바닥 · 불에 탄 압흔 · 방벽주 3개의 검은 원 기둥 · 겹친 청색 오라 · Accent #E88A2E (0.910, 0.541, 0.180) 요새 주황

**기믹 톤**: pylon 오라 3개가 겹쳐 중앙이 진하게 청색(−60% 피해). pillar는 스턴트 수치처럼 갑옷 틈을 막는다.

**라이트 리그**: 낮은 key(28°), 강한 대비 (key 0.62 / fill 0.17) — 요새의 엄격한 빛.

---

## Stage 8: Ash March (재의 행진) — 집행 수렴

**컨셉**: 형 집행의 길 — 양쪽 벽이 반주기 어긋나게 밀려오고(위상 0/11.5), 중앙 제단은 그 사이에서 노출되며, 방벽주가 마지막 보루를 지킨다. **모든 힘이 한 점으로 모인다.**(v1.2: pylon 추가, 피날레 수렴)

**지배 기믹**: `wall` ×2 좌측 위상 0 / 우측 위상 11.5 + `altar` ×1 (768,604) + `pylon` ×1 (768,520) + `vent` ×2 (560,760) 위상 0.6 / (980,450) 위상 1.8  
**부보조 기믹**: 없음  
**보스**: Ash Magistrate (Monarch 틴트, 회백색 0.722, 0.690, 0.643)  
**환경 톤**: 처형로 아스팔트(회색 석재) · 순회 의전 트림(황금 축소) · 벽 가장자리 검은 불 커튼 · 바닥 그을림 · Accent #B8B0A4 (0.722, 0.690, 0.643) 회백

**기믹 톤**: wall은 검은 불 커튼(양쪽). altar는 안전 회랑 중앙(위험). pylon은 회랑 상단(수호). vent는 경계.

**라이트 리그**: 중간 key(44°), 밝은 fill (key 0.53 / fill 0.23) — 행진로의 균형.

---

## 톤 테이블: 모든 스테이지의 기믹 텍스처 일관성

이 테이블은 각 스테이지의 **모든 기믹이 단일 시각 언어를 말하는** 사실 기반이다.  
`[OBSERVED]` 각 행은 코드 상수 + 배치 좌표 추출.

| # | 스테이지 | 지배 기믹 | Accent | Palette(tone) | 해저드 톤 일관성 |
|---|---|---|---|---|---|
| 0 | Cinder Span | vent ×2 | #F25A2B 따뜻한 주황 | "cinder bridge stone with ember-burnt edges" | vent = 담금질 검정 + 비취 가장자리 |
| 1 | Ember Gallery | vent ×4 링 | #F36D33 밝은 주황 | "formal gallery floor with vent rhythm scorch" | vent = 갤러리 흑색 + 동심원 점수 |
| 2 | Abyss Chancel | pillar ×3 | #8F66FF 깊은 자주 | "obsidian chapel with oath-rune seams" | pillar = 검은 거울 + 흰 룬 음각 |
| 3 | Witness Well | altar ×2 | #38C2A8 찬 비취 | "slick testimony-well with jade sediment rings" | altar = 비취 석판 + 침전 고리 |
| 4 | Echo Throne | current ×1 weak | #7EC7FF 밝은 청 | "throne-floor with concentric echo-current channels" | current = 채널 경계 + 은 맥 |
| 5 | Ash Verdict | pylon ×1 | #DEC869 황금 | "verdict-court slabs with muted gold ash scoring" | pylon = 검은 원 + 청색 오라 |
| 6 | Cinder Sluice | current ×2 | #3FA8C8 청록 | "water-worn iron and basalt sluice surfaces" | current = 반대 화살표 + 철 수문 |
| 7 | Ember Bastion | pylon ×3 | #E88A2E 요새 주황 | "fortress floor armor plates with ember burns" | pylon ×3 = 3개 검은 원 + 중첩 청색 |
| 8 | Ash March | wall ×2 | #B8B0A4 회백 | "execution-road ash stone with ceremonial trim" | wall = 검은 불 커튼 + 그을림 |

**톤 정렬 규칙** `[INFERENCE]`:
- 각 스테이지의 색상(Accent)과 재질(Palette)은 독립적이지 않다 — **함께 그 기믹의 시각 정체성을 정의한다**.
- 기믹이 바뀌거나 추가되면(v1.2 위상 변경, 기믹 추가), 배치 좌표도 함께 개정되어야 한다. 기믹의 목적이 바뀌면 환경도 따라간다.
- 톤 불일치는 학습 신호 오염이다: "이 분출구는 왜 다른 색인가?" → 실제로는 다른 배치/위상이다.

---

## 이동 범위 · 오브젝트 스케일 개정 (2026-08-12)

**[OBSERVED]** 현재 상태 (동결 계약):
- 시뮬레이션 아레나: 1536×1024, 중심 (768, 604), 클램프 타원 half 520×270
- 이동 가능 면적(플레이어 마진 34): 타원 도달 ~386k u²
- 오브젝트 뷰 스케일: 1.0 (액터, 프롭, 이펙트)
- 해저드 반경: 고정 상수 (vent 90, pillar 40, altar 70, current half 110, pylon 30/280, wall edge)
- 카메라: pitch 55°, FOV 42, 거리 20/24.5 u(calm/crowd)

**[TARGET]** 개정 (사용자 요청 + 환경 설계 §E0-E2):
- 시뮬레이션 아레나: **클램프 반축 확장** half 735×390 (2.04× 면적 확장)
- 이동 가능 면적: **~770k u²** (1.75~1.84배, 실측 DUNGEON_GUIDE_INTERIOR §3.4)
- 오브젝트 뷰 스케일: **0.70배 축소** (화면상 비율 조정, 프레임 내 밀도)
- 해저드 반경: **변경 없음** (시뮬레이션 불변)
- 카메라: **ViewWorld.Scale 재계산** + 거리 조정 프레임 유지

**왜 이렇게 나뉘는가** `[INFERENCE]` (CLAUDE.md §1, §2, DUNGEON_INTERIOR_SPEC §4.1-4.3):

1. **시뮬레이션 계약은 금융 계약이다.** `CinderCourt.Sim`(순수 C#, `UnityEngine` 없음)의 수치와 골든 다이제스트는 **애플리케이션 진실의 유일한 출처**. 아레나 반축, 기믹 반경, 타이밍은 프로그램 정의 — 바꾸면 모든 플레이 결과가 바뀐다(회귀). 따라서 이동 범위를 2배로 늘려도 **심은 원래 타원만 본다**.

2. **뷰는 심을 읽기만 한다.** `ViewWorld.Scale` (심 1 u = 뷰 **0.0150** 월드)이 유일한 변환 지점. 뷰 오브젝트(`ActorView`, `EnvironmentBuilder`, `VfxDirector`)는 심의 좌표를 이 스케일로 곱해 배치한다. **반경/판정/골든은 심에만 속한다.**

3. **오브젝트 축소는 뷰 비율 문제다.** 이동 대역이 넓어질수록 프레임 내 배우·기믹의 화면상 크기가 작아진다. 플레이어는 화면을 봐야 하므로 **보이는 비율이 맞아야 한다**. 따라서:
   - 심은 기존 좌표를 그대로 쓴다 (이미 출하된 확장 타원, 기존 반경).
   - 뷰 오브젝트는 0.70배로 축소된다 (`ViewWorld.DungeonObjectScale`).
   - 카메라는 `ViewWorld.Scale`과 **같은 ×1.2**로 거리를 올려 프레임 여유를 불변으로 유지한다.


4. **해저드 반경은 시뮬레이션 상수** — 플레이 시간표가 달라진다는 뜻이므로 변경 불가. 뷰는 반경을 읽어 **시각 이펙트만** 그릴 뿐(분출 원, 기둥 충돌 링, 벽 밴드) — 판정 자체는 시뮬레이션.
   - 결과: 플레이어(또는 적)가 시각상 0.70배 작아도, 심은 여전히 원래 크기 클램프 반경을 쓴다(PlayerPushRadius 26, EnemyPushRadius 22, PillarRadius 40).
   - 이것이 "게임플레이는 무이동, 화면만 변한다"는 뜻이다.

**산술 (ViewWorld 스케일)** [OBSERVED, 2026-08-12 출하]:
- 심 확장은 **이번 사이클의 변경이 아니다.** half 520 → half 735 (1.4135×)는
  AMENDMENT #15/#17로 **이미 출하된 `DungeonBoundsSpec.ExpandedHalfWidth=735 /
  ExpandedHalfHeight=390`**이다. 이번 개정은 심을 건드리지 않았고 골든도 움직이지
  않았다(`Golden_*` 7종 Passed).
- 이번 사이클의 확대는 **`ViewWorld.Scale` 0.0125 → 0.0150 (×1.2)** 하나뿐이다.
- 뷰 오브젝트 축소: **0.70×** (`ViewWorld.DungeonObjectScale` 단일 출처).
- 프레임 유지: 카메라 거리도 **같은 ×1.2** (calm 17.5 → 21.0, crowd 21.5 → 25.8).
  축소율 0.70의 역수(×1.43)가 아니라 **Scale과 같은 배율**이어야 한다 —
  프레임 여유 `e = D·tan21°·1.5 / (735·Scale)`에서 D와 Scale이 같이 커져야
  약분되어 e가 1.097로 불변이고, 벽 링 e 1.02 대비 7.5% 여유가 유지된다.
  거리만 1.43배로 올리면 e가 어긋나 타원이 프레임을 넘거나 헐거워진다.


---

## 검증 체크리스트 (구현자용) [OBSERVED, 2026-08-12 · EditMode 966 total / 964 passed / 0 failed / 2 skipped]

- [x] 9개 스테이지의 기믹 배치가 코드(`StageCatalog.PactFor(stageId)`)와 일치하는가
      — `Bindings_CoverExactlyTheSourceDerivedCampaignStageHazardPairs` Passed
- [x] 톤 테이블 일관성: 각 Accent + Palette(MaterialLanguage)가 배치 문맥을 강화하는가
      — `StageHazardTextures_EveryCatalogBindingHasOpaque512NonFlatAlbedo` /
      `..._RepeatRolesUsePositiveRuntimeBaseMapSt` Passed
- [x] StageMood.cs 라이트 리그: 각 스테이지의 key pitch/yaw/intensity가 기믹 톤과 일치하는가
      — `Mood_*` 3종 Passed
- [x] 이동 범위 확장: `ViewWorld.Scale` 0.0125 → 0.0150 후 골든 무이동 + 프레임 여유 e 1.097 불변
- [x] 오브젝트 스케일: `ActorView.GlobalScale` 0.70, `EnvironmentBuilder` 프롭 0.70
      (`SpawnLibraryPart`가 해결된 factor에 `DungeonObjectScale`을 곱한다)
- [ ] **VfxDirector 장식 0.7 — 의도적으로 적용하지 않음 [OBSERVED].** `VfxDirector.cs`의
      `localScale` 전수 감사 결과 남은 사용처가 전부 `span`/`diameterWorld`/`radiusWorld`/
      ward 계열, 즉 **심 반경에 묶인 텔레그래프**다. 이걸 줄이면 예고 원과 실제 판정
      반경이 어긋난다(§4k 계열의 "필드는 맞고 화면만 틀림"의 역방향 — 화면이 판정을
      속인다). 장식 전용 쿼드가 분리되기 전까지 이 항목은 열어 둔다.
- [x] 시뮬레이션 반경: vent/pillar/altar/current/pylon/wall 심 상수 전부 불변
      — `DungeonProgressionSpec.cs` diff는 주석 전용
- [x] 골든 다이제스트: `Golden_*` 7종 + `Bounds_*`/`ExpandedBounds_*` 6종 전부 무이동


---

## 부록: 기믹 배치 검증 표

`[OBSERVED]` `StageCatalog.PactFor(stageId)` 추출 + `StageOverrideHazards` 오버라이드 테이블.

| 스테이지 | ID | 고정 앵커 | 배치 (좌표 링크: SIM_SPEC_DUNGEONS.md) | 오버라이드 |
|---|---|---|---|---|
| 0 | cinder-span | ✓ cinder-span | vent(560,480/0) vent(980,720/1.2) | — |
| 1 | ember-gallery | ✓ cinder-span | vent×4 ring + pillar(768,604) | ✓ EmberGalleryHazards |
| 2 | abyss-chancel | ✓ abyss-chancel | pillar×3 + vent(1100,450/0.6) | — |
| 3 | witness-well | ✓ abyss-chancel | altar×2 + vent×2 + pillar(768,604) | ✓ WitnessWellHazards |
| 4 | echo-throne | ✓ echo-throne | altar(768,604) + current(768,604/0.3 weak) + vent×2 | ✓ EchoThroneHazards |
| 5 | ash-verdict | ✓ echo-throne | altar(768,604) + pylon(960,540) + vent×2 | ✓ AshVerdictHazards |
| 6 | cinder-sluice | ✓ cinder-sluice (新) | current×2(±200) + pillar(768,604) + vent×2 | — |
| 7 | ember-bastion | ✓ ember-bastion (新) | pylon×3 + pillar×2 + vent×1 | — |
| 8 | ash-march | ✓ ash-march (新) | wall×2(phase 0/11.5) + altar(768,604) + pylon(768,520) + vent×2 | — |

---

## 참고 문헌

- `Assets/Scripts/Sim/CampaignTypes.cs` — HazardKind enum, HazardConfig fields, CampaignSpec constants
- `Assets/Scripts/View/StageCatalog.cs` — AllEntries 테이블, AccentColor, Epithet, PactFor()
- `Assets/Scripts/View/StageHazardVisualCatalog.cs` — ToneProfileArray (Palette, MaterialLanguage)
- `Assets/Scripts/View/StageMood.cs` — Rigs 테이블 (light pitch/yaw/intensity per stage)
- `docs/SIM_SPEC_DUNGEONS.md` AMENDMENT #5 — 신규 3스테이지 배치 확정
- `docs/SIM_SPEC_ENVIRONMENT.md` §E4 — 존별 팔레트, 기믹→환경 매핑
- `DUNGEON_INTERIOR_SPEC.md` §3 — 레인 아키텍처, 결정론 배치
