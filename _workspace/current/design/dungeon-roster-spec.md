# Dungeon Roster Spec — cycle 2 신규 던전 3종 (Phase 1b draft)

작성: game-designer. 근거: trend-survey/dungeon-gimmick-trends.md 빈도표,
production/design-analysis.md §6-7, engineering/dungeon-code-map.md 시임.
상태: 협상 1R(1c) 및 QA 벤치마크 수치 반영 전 draft.

## 설계 원칙 (서베이 도출)

1. **스테이지 정체성 = 지배 기믹 1개** — 공유 가구(pillar/vent/altar)는 조연.
2. **공정성은 대칭 또는 예측가능성으로 구매** — 신규 기믹은 전부 고정
   타임테이블(결정론 = 게임 정체성. 서베이 key gap: 학습 가능한 해저드
   안무는 시장 공백).
3. 기존 6스테이지와 달리 **신규 sim anchor**(웨이브 수/보스/기믹 세트가 다른
   기계적 차이) — 지리 변형 반복 탈피.

## 로스터 (StageCatalog 7..9번, prereq 체인 연장)

| # | id | 이름(한) | anchor(신규) | W | 지배 기믹 | 보조 | 보스 | 스토리 비트 |
|---|---|---|---|---|---|---|---|---|
| 6 | cinder-sluice | 재의 수문 | cinder-sluice | 8 | tide-current ×2 | pillar ×1 | Commander 틴트 "Sluice Keeper / 수문지기" | 법정 아래 잿물 수로 — 기록 말소의 강 |
| 7 | ember-bastion | 불씨 요새 | ember-bastion | 8 | ember-pylon ×2 | pillar ×2 + vent ×1 | Commander 틴트 "Bastion Sentinel / 요새 감시자" | 위증자들의 방벽 — 방패 뒤에 숨은 증인들 |
| 8 | ash-march | 재의 행진 | ash-march | 9 | ash-wall ×1 | altar ×1 + vent ×1 | Monarch 틴트 "Ash Magistrate / 재의 집행관" | 판결 집행 — 다가오는 재의 장벽 |

- 해금 체인: ash-verdict → cinder-sluice → ember-bastion → ash-march.
- 진행도: ClearedMask 0x3F → 0x1FF (기존 6비트 의미 불변, 비트 6-8 추가).
- 신규 anchor StageIndex 3/4/5 → 호위 min(8, 3+2·idx) = 8 (자동), 파편 슬롯
  로테이션 StageIndex%3 = 0/1/2 (자동 순환).
- Ember Rest 연속 루트: 룸 인덱스 검증 1..5 → 1..8 확장 필요(CinderSim:532).

## 신규 HazardKind 3종 — 기믹 계약 (수치는 balance-sheet 확정본 우선)

### tide-current (잿물 해류) — 주기 푸시 레인
- 필드: `{x, y, halfW, halfH, period, activeSeconds, telegraph, pushX, pushY}`
  (축정렬 밴드, 사각 판정 — 유일한 비원형 해저드).
- 사이클: `t = fmod(stageTime + phase, period)`. 텔레그래프(흐름 예고 셰이더)
  → active 동안 밴드 내 **플레이어+적 전원**에게 push 속도 가산(이동 후,
  클램프 전). 피해 없음.
- **독트린 변경(대칭)**: 기믹 최초로 적에게도 작용 — 적을 레인으로 유인해
  진형을 무너뜨리는 전술이 스테이지의 학습 목표. AMENDMENT에 명문화.
- 뷰: 스크롤 UV 흐름 데칼 + 방향 셰브론, reduced-motion = 정적 화살표 데칼.

### ember-pylon (불씨 방벽주) — 적 보호 오브젝트 (파괴 가능)
- 필드: `{x, y, radius(몸통), auraRadius, hp}`.
- 오라 내 적 전원 **받는 피해 −40%** (뷰: 적 틴트/실드 링). 플레이어 공격·
  스킬이 파일런을 타격 가능(전방 판정·사거리 동일 규칙), 파괴 시 영구 소멸
  (`SimEvents.PylonDown`), 웨이브 리셋 없음.
- 전술 문제: 선-파일런 후-전투 vs 실드 낀 적과 정면 승부.
- 이동 차단 없음(pillar와 역할 분리). 적 피해·이동에는 불간섭.

### ash-wall (재의 장벽) — 시간표 침식 벽
- 필드: `{edge: left, depthMax, advance, hold, recede, rest, telegraph, tickDamage, tickPeriod}`.
- 사이클: rest → telegraph(경계 셰이더 점멸) → advance(좌변 x=248에서
  depthMax까지 전진) → hold → recede. 벽 밴드 내 **플레이어+적** 모두
  tick 피해(대칭 — Ward 무효화·grace 소모는 플레이어 규칙 그대로, 적 처치
  크레딧 정상 지급).
- 스테이지의 학습 목표: 안전지대 리듬 암기 + 적을 벽에 몰아넣는 처형 플레이.
- 뷰: 전진 파티클 커튼 + 바닥 그을림 데칼, reduced-motion = 경계선만.

## 비트 (StoryCatalog 추가분 초안 — G1 세계관 정합)

세계관: Cinder Court = 기억의 감옥. 신규 3막 = "집행부" — 판결(ash-verdict)
이후 형 집행의 공간들. Lantern Reaver는 말소된 기록의 흔적을 따라 내려간다.

| stage | stageStart | bossEntry | bossPhase2 | completion |
|---|---|---|---|---|
| cinder-sluice | "판결문은 잿물이 되어 수문 아래로 흐른다." | "수문지기: 기록은 흘려보내야 한다." | "수문지기: 역류는… 허락되지 않는다!" | "말소된 이름 하나가 물살을 거슬러 떠올랐다." |
| ember-bastion | "위증자들이 방벽 뒤에서 숨죽인다." | "감시자: 증언은 방패다. 뚫어 보아라." | "감시자: 방벽이 무너져도 위증은 남는다!" | "방벽이 꺼지자 위증의 불씨가 사그라들었다." |
| ash-march | "재의 장벽이 행진한다 — 판결은 멈추지 않는다." | "집행관: 형은 이미 집행되고 있다." | "집행관: 재 앞에서 모든 걸음은 무의미하다!" | "행진이 멎었다. 랜턴이 마지막 기록을 비춘다." |

## 뷰 파급 (Phase 1d 태스크로)

- 로비 카드 9장: 70px 피치 초과 → 피치 압축 또는 스크롤(HudLayoutTests 검증).
- HazardIcon: cinder-sluice=skill-dash(흐름), ember-bastion=skill-ward(방벽),
  ash-march=skill-strike(집행). 기존 아이콘 재사용(신규 생성 없음).
- 터레인: 신규 FBX 없이 기존 terrain 재사용(sluice=abyss-chancel,
  bastion=cinder-span, march=echo-throne) + DressingPlacement 테이블 3종.
- AccentColor: sluice #3FA8C8(잿물 청록), bastion #E88A2E(방벽 주황),
  march #B8B0A4(재 회백).
