# 소스: 핵앤슬래시 디자인 가이드 (외부 레퍼런스, 2026-08-07)

- Raw: `raw/sources/2026-08-07-hackslash-design-guide-reference.md`
- 성격: 사용자 제공 일반론 레퍼런스. **이 프로젝트의 기획서가 아니다.**
  판정 기준은 항상 `docs/SIM_SPEC*.md` + `CLAUDE.md` §2 수치계약.
- 용도: 2026-08-07 기획-구현 대조 감사의 갭 분석 비교축.

## 핵심 주장 요약

1. **연출**: 콤보 모멘텀(공격할수록 강해짐), 히트스톱 1–3프레임, 카메라 셰이크,
   무기 TrailRenderer, 피격 리액션, 필살기 포스트프로세싱, AoE 텔레그래프 공정성.
2. **전투 구조**: 상태머신 + 데이터 드리븐 콤보(다음 공격 리스트 데이터화) + 피드백.
3. **웨이브**: 포인트 기반 생성(적 타입별 코스트) + 난이도 곡선(학습→복합→클라이맥스)
   + DDA(성과 기반 ±10% 조정).
4. **던전 리듬**: 전투방–루팅–챌린지–보스 반복, 리스크-리워드 경로, 빌드 체크포인트.
5. **드롭**: 희귀도 global 테이블(C/R/E/L 가중치 100/20/5/1), 소스별 테이블,
   보스 드롭 보장, bad-luck protection, 진행 기반 동적 조정.

## 프로젝트 계약과의 알려진 충돌 (감사 시 결함으로 오인 금지)

- 가이드의 Unity 구현론(AttackData ScriptableObject, OnTriggerEnter 히트박스,
  Animator 상태머신, PostProcessingVolume)은 CLAUDE.md §1의
  **순수 C# 결정론 심(UnityEngine 참조 금지) / View 읽기전용** 경계와 충돌.
  → 채택 불가 아이디어로 분류.
- 가이드의 C/R/E/L 희귀도 등급은 이 프로젝트에 없음. `HackTypes.cs`의
  EquipTiers(weapon/lantern/cloak T0–T5 랭크, +6%/+8%/+8HP)가 의도적 대체.
