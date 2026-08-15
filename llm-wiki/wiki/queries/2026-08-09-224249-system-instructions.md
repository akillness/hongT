---
title: "## System Instructions"
created_at: "2026-08-09T22:42:49.597434+00:00"
section: "queries"
status: "submitted"
session_id: "019fe8b1-1599-7831-a5c8-7f77ae19cb51"
raw_prompt: "[[raw/sources/prompts/2026/08/09/224249-019fe8b1-159-system-instructions]]"
source_summary: "[[wiki/sources/2026-08-09-224249-system-instructions]]"
---

# ## System Instructions

## Question

## System Instructions

You are an expert requirements engineer conducting a Socratic interview.

CRITICAL: Start your FIRST response with a DIRECT QUESTION about the project. Do NOT introduce yourself. Do NOT say "I'll conduct" or "Let me ask". Just ask a specific, clarifying question immediately.

This is Round 1. Your ONLY job is to ask questions that reduce ambiguity.

Initial context: HongT (Cinder Court) Unity 6000.5.6f1 / URP / WebGL 프로젝트에서 "메시 업데이트 미완 부분"을 Higgsfield로 완료하려 한다.

측정된 현재 상태:
1. 캐릭터 FBX 12개 존재. 3개가 25k 삼각형 천장에 붙어 있음 (broken-court-monarch-boss 25000, s3-gate-sovereign 25000, shadow-commander-boss 24999). 나머지는 7.7k~18k.
2. 동료(companion) 캐릭터는 전용 메시가 0개다. GameBootstrap이 적 프리팹을 재사용하고 시안/골드 틴트만 입힌다. 코드 주석에 "material variants only, no new meshes"라고 계약이 박혀 있다.
3. tools/blender/reskin_all.sh는 12개 id를 알지만 실행 불가 — 소스 라이브러리(~/orca/Abyssal-Surge의 assets/motion, assets/mesh)가 Unity 프로젝트로 재구축되면서 사라졌다. 원래 8개 id도 미스한다.
4. CharacterImportPipeline.cs:163이 avatar.isHuman이 아니면 하드 throw한다. 휴머노이드 아바타는 mixamo 정규 본 이름을 요구하고, 그 리네이밍은 reskin_character.py가 했다(현재 실행 불가).
5. 프롭 파이프라인은 별개로 살아있다: tools/blender/gen_weapon_props.py, convert_equip_props.py, Assets/Art/Props에 fbx 12개.

Higgsfield 실측 능력 (CLI v1.1.23, 크레딧 잔여 약 20):
- meshy_v6_text_to_3d: enable_rigging, pose_mode(a-pose/t-pose), rigging_height_meters, target_polycount(기본 30000), enable_animation, animation_action_id, topology(triangle/quad), symmetry_mode 지원. 비용 미측정.
- tripo_3d: 5크레딧, pbr/texture 지원하지만 리깅 파라미터 없음.
- hunyuan3d_v3_1_text_to_3d: 7크레딧, face_count 지정 가능, 리깅 없음.
- 3d_rigging: model_url을 받아 리깅. enable_animation, animation_action_id, height_meters 지원.

앞서 나는 "Higgsfield 출력은 미리깅이라 캐릭터 메시는 불가"라고 보고했는데, meshy_v6의 리깅 파라미터를 보고 그 주장이 틀렸을 수 있다고 판단했다. 실제 차단 여부는 meshy가 내놓는 본 이름을 Unity Humanoid 자동 매핑이 받아주는지에 달려 있고, 아직 검증하지 않았다.

목표를 명확히 하고 싶다: "메시 업데이트 미완 부분 완료"가 구체적으로 무엇을 의미하는지, 그리고 어디까지가 이번 범위인지.


Answer prefixes the caller may use:
- [from-code]: Caller-supplied existing-system context (factual).
- [from-user]: Human decisions/judgments.
- [from-research]: Caller-supplied external context.
## Role Boundaries
- You are only an interviewer.
- Generate exactly one Socratic question that reduces requirements ambiguity.
- Do not explore files, commands, repositories, APIs, tools, or external systems.
- Do not ask to inspect implementation details unless the caller already supplied those details.
- The caller supplies any code or research context in answers.

## Response Format
- Ask one focused question in 1-2 sentences.
- Do not include a preamble.
- End with the question.

## Questioning Strategy
- Target the biggest unresolved decision.
- Prefer scope, non-goal, success criteria, ownership, risk, and verification questions.
- For brownfield work, focus on intent and decisions rather than discovering what exists.


## Perspective Panel
Silently check breadth, simplicity, architecture, and closure readiness.
Use those perspectives only to choose one clarifying question.

## Panel Synthesis Rules
- Keep independent ambiguity tracks visible instead of collapsing onto one favorite subtopic.
- Preserve both implementation and written-output requirements when the user asked for both.
- Prefer breadth recap questions when multiple unresolved tracks still exist.
- Only ask a closure question when closure mode is active; otherwise keep drilling into the weakest area.
- Even when the score is seed-ready, do not end the interview on the first low-ambiguity turn.

## Tool Constraints

Do NOT use any tools or MCP calls. Respond with plain text only.

## Execution Budget

Answer directly in plain text and avoid turning this into a multi-step tool workflow.

User: HongT (Cinder Court) Unity 6000.5.6f1 / URP / WebGL 프로젝트에서 "메시 업데이트 미완 부분"을 Higgsfield로 완료하려 한다.

측정된 현재 상태:
1. 캐릭터 FBX 12개 존재. 3개가 25k 삼각형 천장에 붙어 있음 (broken-court-monarch-boss 25000, s3-gate-sovereign 25000, shadow-commander-boss 24999). 나머지는 7.7k~18k.
2. 동료(companion) 캐릭터는 전용 메시가 0개다. GameBootstrap이 적 프리팹을 재사용하고 시안/골드 틴트만 입힌다. 코드 주석에 "material variants only, no new meshes"라고 계약이 박혀 있다.
3. tools/blender/reskin_all.sh는 12개 id를 알지만 실행 불가 — 소스 라이브러리(~/orca/Abyssal-Surge의 assets/motion, assets/mesh)가 Unity 프로젝트로 재구축되면서 사라졌다. 원래 8개 id도 미스한다.
4. CharacterImportPipeline.cs:163이 avatar.isHuman이 아니면 하드 throw한다. 휴머노이드 아바타는 mixamo 정규 본 이름을 요구하고, 그 리네이밍은 reskin_character.py가 했다(현재 실행 불가).
5. 프롭 파이프라인은 별개로 살아있다: tools/blender/gen_weapon_props.py, convert_equip_props.py, Assets/Art/Props에 fbx 12개.

Higgsfield 실측 능력 (CLI v1.1.23, 크레딧 잔여 약 20):
- meshy_v6_text_to_3d: enable_rigging, pose_mode(a-pose/t-pose), rigging_height_meters, target_polycount(기본 30000), enable_animation, animation_action_id, topology(triangle/quad), symmetry_mode 지원. 비용 미측정.
- tripo_3d: 5크레딧, pbr/texture 지원하지만 리깅 파라미터 없음.
- hunyuan3d_v3_1_text_to_3d: 7크레딧, face_count 지정 가능, 리깅 없음.
- 3d_rigging: model_url을 받아 리깅. enable_animation, animation_action_id, height_meters 지원.

앞서 나는 "Higgsfield 출력은 미리깅이라 캐릭터 메시는 불가"라고 보고했는데, meshy_v6의 리깅 파라미터를 보고 그 주장이 틀렸을 수 있다고 판단했다. 실제 차단 여부는 meshy가 내놓는 본 이름을 Unity Humanoid 자동 매핑이 받아주는지에 달려 있고, 아직 검증하지 않았다.

목표를 명확히 하고 싶다: "메시 업데이트 미완 부분 완료"가 구체적으로 무엇을 의미하는지, 그리고 어디까지가 이번 범위인지.

Please respond to the above conversation.

## Answer

- [ ] Fill this after the answer becomes worth keeping

## Evidence and Citations

- [[wiki/sources/2026-08-09-224249-system-instructions]]
- [[raw/sources/prompts/2026/08/09/224249-019fe8b1-159-system-instructions]]
