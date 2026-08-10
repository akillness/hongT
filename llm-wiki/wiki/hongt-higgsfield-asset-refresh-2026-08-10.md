# HongT 힉스필드 자산 리프레시 — 파이프라인 성문화와 실측 결론 (2026-08-10)

사용자 지시: 힉스필드로 3D 메시·모션·VFX·인트로/컷씬 영상·이미지 리소스를
업데이트하고 미사용 리소스를 제거. 본 문서는 그 첫 슬라이스의 지속 결론.

## 1. 이미 적용돼 있던 것 — 중복 작업 방지 지도

- **막 시네마틱 act1~3 = kling2_6 산출물.** [OBSERVED] 잡
  `1e12dc12`(보라 성당)·`091bd2d7`(고딕 홀)·`dede82fe`(재폭풍 성채)가
  cycle-12(74ec491)에서 1764×1176→1280×854 재인코딩으로 출하됨.
  원본과 잡 ID는 `_workspace/current/engineering/mesh-gen/manifest.json`.
- **전투 모션 3종 = 3d_rigging 산출물.** [OBSERVED] 잡 스윕의
  id 238/241/242가 커밋 1742c10(Axe Spin Attack·Weapon Combo 2·Charged
  Slash)으로 소비됨. `animation_action_id` 지도(전투 190~242 · 아이들
  243~252 · 감정 255~270)는 같은 manifest.json에 기록.
- **seedance_2_0_mini 픽셀아트 테스트는 기각.** 실사 고딕 톤과 충돌.
  스타일 게이트를 CLAUDE.md §3 2026-08-10 개정에 성문화함.

## 2. 교체하지 않기로 한 것 — 판정 근거

- **scorch-decal.png는 건강하다.** [OBSERVED] PIL 실측: RGBA, 모서리/변
  중앙 alpha 0, 중심 255 — "사각 모서리 전 alpha 0" 계약(VfxDirector.cs:903
  주석) 충족. 이미지 뷰어의 검은 배경 합성이 불투명 착시를 만든다.
  **알파 판정은 뷰어가 아니라 채널 실측으로 하라.**
- **컨셉·위협 인트로 비트는 캐릭터 씬** (등불 워든 / 황금 군주). 환경 온리
  클립으로 갈면 서사가 빠진다 — 다운그레이드 금지를 §3 개정에 명시.

## 3. 다음 슬라이스로 넘긴 실측 발견

- **아이콘 로더 경로 불일치.** HudView.cs:1228은
  `regenerated→generated→root` 체인, LobbyView/MetaScreenView/HudViewCodex/
  VfxDirector는 root 직행. root 사본 17종은 HudView 기준 데드웨이트지만
  직행 로더 때문에 삭제 불가 — **통일이 선행**이고 그 후 삭제가 안전해진다.
- **빌드 무게의 지배항은 FBX가 아니라 Resources 텍스처.** [OBSERVED]
  `Textures/Env/` 57MB + `Scenes/` 27MB = Resources 93MB의 90%. 압축/아틀라스
  설정이 자산 삭제보다 훨씬 크게 움직인다.
- **스킬 VFX는 전부 절차식** (LineRenderer+ParticleSystem, 텍스처 2장뿐,
  VFX Graph는 WebGL 예산으로 금지 — VfxDirector.cs:343). 텍스처화 후보 4종
  (nova ring·ward hex·range ring·hit spark)을 GPT Image 2로 생성해
  `_workspace/current/engineering/vfx-gen/`에 보관 — 와이어링은 뷰 코드
  변경이므로 §4c 브라우저 스모크와 함께 갈 것.
- **터레인 프리팹은 9스테이지 중 3개만 존재** — 나머지는 맨 코트 폴백.
  던전 구성 리프레시의 실제 갭.

## 4. 제거 실행 (pre-asset-purge-20260810 태그)

참조 0건 검증(rg, 주석 포함) 후 git rm 7건: `Punch Combo 5.fbx`,
`Right Upper Hook.fbx`, `LanternReaver/lantern-reaver-character.glb`
(+메타 3, 폴더 메타 1) — 계 12.6MB. 프로브 입력 7종(FBX)은 SwingArcProbe/
ClipWindowProbe가 소비하므로 보존.
