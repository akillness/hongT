# Golden Digests — cycle 2 (R1-R3 + 신규 3스테이지)

## 런타임 주의 (2026-08-05 게이트에서 확정)

**dotnet 다이제스트와 Unity 다이제스트는 비트 비교 불가** — ARM64 FMA로
float 하위비트가 다르다(정수 필드는 15행 전부 양 런타임 동일 [OBSERVED]).
- 이 파일의 아래 표 = **dotnet 8 스탠드얼론** 기록. 용도: pre(HEAD 719a587)
  == post 바이트 동일성으로 **AMENDMENT #5 추가성 증명** (동일 런타임 내 비교).
- **배포 진실 = Unity 런타임 골든**: `golden-rows-unity.md` (GoldenDigestRecorder
  기록, DungeonGoldenDigestTests 리터럴로 고정). 이후 사이클의 회귀 기준은
  Unity 골든이다. 스탠드얼론 하니스를 다시 만들 때 이 벽에 부딪히지 말 것.

방법: 순수 심 스탠드얼론 컴파일(dotnet 8) + kiter 봇(CampaignSimTests.BotInput
바이트 미러) 1800틱. pre = HEAD 719a587 워크트리, post = cycle-2 심 수정 후.
**pre/post 기존 12행 바이트 동일 [OBSERVED]**. Unity EditMode 재확인 완료
(166→183 테스트, 골든 재고정 후 게이트 결과는 gate-measurements.md).

포맷: `label|score|wave|kills|relics|healthRemaining(R)|reason|playerX(R)|playerY(R)`

## R2/R3 (모드 회귀)

```
arena-hack|3700|4|15|2|82|(running)|1035.2732|717.864
arena-frozen|3700|4|15|2|82|(running)|1035.2732|717.864
prologue|1650|2|9|1|36|(running)|930.1258|435.3988
```

## R1 기존 6스테이지 (hack lane, ranks 2/1/3)

```
cinder-span|4200|3|15|4|142|(running)|588.8484|763.738
ember-gallery|3150|3|14|1|136|(running)|719.4032|831.70166
abyss-chancel|3150|3|14|1|136|(running)|719.3043|831.6502
witness-well|3400|3|14|2|136|(running)|459.74823|696.5315
echo-throne|4200|3|15|4|142|(running)|588.8484|763.738
ash-verdict|4200|3|15|4|142|(running)|588.8484|763.738
```

## Campaign classic lane (CinderSim(in CampaignConfig), ranks 2/1/3)

```
classic-cinder-span|3700|4|15|2|99|(running)|1035.2732|717.864
classic-abyss-chancel|3700|4|15|2|115|(running)|1035.9244|717.52496
classic-echo-throne|3700|4|15|2|106|(running)|1035.2732|717.864
```

## 신규 3스테이지 (post 최초 기록 — cycle-2 골든)

```
cinder-sluice|4200|4|15|4|142|(running)|979.5375|631.2188
ember-bastion|3150|3|14|1|142|(running)|1217.3184|620.4511
ash-march|4200|3|15|4|142|(running)|588.8484|763.738
```

주: ash-march 1800틱 다이제스트가 echo-throne과 동일한 것은 정상 —
30초 시점까지 벽 첫 사이클(전진 10.5s~)에 kiter가 대역 밖에 있었고 필드
수치가 동일해서다. 벽 동작 자체는 행동 프로브로 별도 증명(아래).

## 기믹 행동 프로브 [OBSERVED]

```
wallKin|f9=248.0(248)|f12=368.0(368)  — 전진 킨매틱 스펙 일치
wallDmg|wallHits=3(정확히 8dmg, [10.5,18) 대역 내)|otherDrops=15(적 접촉)
current|xIdle=607.3|xMax=1054.3|drift=+447.0px (활성창 푸시)
pylon|down=True|downT=2.48s|lastHp=0 (weapon5 콤보 3스윙 = 263 ≥ 240)
```

## QA 밴드 사전 판정 (benchmark-notes 대비)

- 텔레그래프 티어: wall tick 8 = base HP 8% → light(≥0.8s), 실제 1.5s PASS.
  current 무피해, 0.8s PASS.
- 단일 히트 상한: 8 ≤ 30 (30% of 100) PASS. **지속 노출 명시 결정**: 벽 대역
  잔류 시 최대 ~12틱×8=96 노출 가능 — D3/D4 계열 "DoT는 서 있으면 치명" 관례
  준수(이탈 속도 218 vs 벽 80, 3배 여유). 상한 위반 아님으로 기록.
- 동시 텔레그래프 ≤3: sluice 대향류 위상 3.0s 오프셋 = 동시 0. ash-march
  벽[9,10.5)×vent(phase1.2)[1.2,2.0 mod 2.4] 겹침 [9.0,9.2) = 최대 2. PASS
  (LCM 센서스 테스트로 고정 예정 — D3).
