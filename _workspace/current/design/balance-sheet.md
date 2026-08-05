# Balance Sheet — cycle 2 신규 기믹/스테이지 수치 (v1)

작성: game-designer. QA 밴드(qa/benchmark-notes.md §Derived bands) 준수.
데이터 미러: 구현 시 CampaignSpec 상수 + CampaignStages 테이블 (programmer 동기화 책임).

## 신규 기믹 수치

### tide-current (잿물 해류)
```yaml
system: hazard-tide-current
shape: axis-aligned band (halfW 520, halfH 70)   # 유일한 비원형 해저드
period_s: 6.0
active_s: 2.4
telegraph_s: 0.8          # QA band: 무피해 → light tier 하한 적용
push_x_u_per_s: 140       # 플레이어 218 대비 64% — 역주행 가능(68 u/s 순속)
push_y_u_per_s: 0         # y 이동 배율 0.68 회피, 가독성 위해 순수 X류
damage: 0
symmetric: true           # 플레이어+적 동일 적용 (독트린 변경, AMENDMENT 명문화)
apply_order: "이동 적용 후 → 푸시 가산 → 아레나 클램프 → 필러 푸시아웃"
data_mirror: CampaignSpec.Current*
```
- 배치(cinder-sluice): lane A {y 470, push +140, phase 0} · lane B {y 740, push −140, phase 3.0}
  — 대향류 2줄, 위상 반주기 오프셋(동시 텔레그래프 ≤2, QA band 5 충족).

### ember-pylon (불씨 방벽주)
```yaml
system: hazard-ember-pylon
body_radius: 30           # 공격 판정 반경(전방판정·사거리 160 규칙 상속)
aura_radius: 220
hp: 240                   # 콤보 1.18세트(58+58+87=203) + 1타 — 우선 파괴 코스트 체감
enemy_damage_taken_mult_in_aura: 0.60    # 받는 피해 −40%
respawn: none             # 파괴 시 런 내 영구 소멸, SimEvents.PylonDown
blocks_movement: false    # pillar와 역할 분리
damage: 0
data_mirror: CampaignSpec.Pylon*
```
- 배치(ember-bastion): pylon {560,500} · pylon {980,700} + pillar {640,650} ·
  pillar {900,560} (파일런 접근로 차폐) + vent {768,604, phase 0.6}.
- TTK 영향: 오라 내 적 실효 TTK ×1.67 — G2 TTK 밴드는 "파일런 선파괴 경로"
  기준으로 측정(QA 방법론 주석 필수).

### ash-wall (재의 장벽)
```yaml
system: hazard-ash-wall
edge: left                # x=248(전투면 좌변)에서 전진
depth_max_px: 360         # 최대 커버 x 248..608 — 중심(768)은 항상 안전
cycle: {rest: 9.0, telegraph: 1.5, advance: 4.5, hold: 3.0, recede: 4.5}   # 주기 22.5 s
advance_speed_px_s: 80    # 플레이어 218 대비 37% — 도보 이탈 가능
tick_damage: 8            # base HP의 8% = light tier (QA band 1: telegraph ≥0.8 충족)
tick_period_s: 0.6        # 지속 노출 시 ~13.3 dps
single_hit_cap_check: 8 ≤ 0.30 × 100 (max-HP 최소치)   # QA band 2 통과
symmetric: true           # 적 동일 피해, 처치 크레딧 정상(점수/기름/드롭)
ward_grace: 플레이어 규칙 그대로(Ward 무효화, grace 소모)
data_mirror: CampaignSpec.Wall*
```
- 배치(ash-march): wall {left, phase 0} + altar {1100, 604} (벽 반대편 —
  리스크/보상 축) + vent {980, 480, phase 1.2}.

## 신규 anchor 수치 (기존 공식 상속 — 신규 곡선 없음)

| anchor | StageIndex | W | 보스 | 웨이브 적 HP(공식) | 호위 | 보스 HP |
|---|---|---|---|---|---|---|
| cinder-sluice | 3 | 8 | Commander | 86+min(140,(w−1)·11) | min(8,3+6)=8 | (86+88)×6=1044 |
| ember-bastion | 4 | 8 | Commander | 〃 | 8 | 1044 |
| ash-march | 5 | 9 | Monarch | 〃 | 8 | (86+88)×6=1044 |

- 파편 슬롯 로테이션: StageIndex%3 = 0/1/2 (weapon/lantern/cloak 순환 유지).
- 스폰/간격/점수/XP/정예: 기존 공식 그대로 (수치 계약 무변경).

## G2 측정 타깃 (QA test-plan §C 소비)

```yaml
win_rate_pve: {ranks_555: must-clear, ranks_213: contested}   # QA 해석 그대로
ttk_wave_clear_target_s: {w1-3: 12, w4-6: 22, w7-9: 34}       # ±15%
hazard_damage_share_band: [0.10, 0.35]     # ash-wall·vent 합산, kiter 기준
tide_current_band: "푸시 유발 접촉피해 >0 그리고 ≤0.35 (직접 피해 0이므로 대체 측정)"
pylon_band: "오라 내 TTK ≤ 오라 외 ×1.8 (선파괴 경로 존재 증명)"
simultaneous_telegraph_max: 3               # LCM 센서스, D3
band-overrides: 없음 (하니스 기본 대역 사용)
```
