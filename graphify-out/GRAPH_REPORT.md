# Graph Report - main  (2026-08-04)

## Corpus Check
- 45 files · ~112,265 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1159 nodes · 1305 edges · 93 communities (87 shown, 6 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 1 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `8b9c188e`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]
- [[_COMMUNITY_Community 42|Community 42]]
- [[_COMMUNITY_Community 43|Community 43]]
- [[_COMMUNITY_Community 44|Community 44]]
- [[_COMMUNITY_Community 45|Community 45]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_Community 47|Community 47]]
- [[_COMMUNITY_Community 48|Community 48]]
- [[_COMMUNITY_Community 49|Community 49]]
- [[_COMMUNITY_Community 50|Community 50]]
- [[_COMMUNITY_Community 51|Community 51]]
- [[_COMMUNITY_Community 52|Community 52]]
- [[_COMMUNITY_Community 54|Community 54]]
- [[_COMMUNITY_Community 56|Community 56]]
- [[_COMMUNITY_Community 57|Community 57]]
- [[_COMMUNITY_Community 58|Community 58]]
- [[_COMMUNITY_Community 61|Community 61]]
- [[_COMMUNITY_Community 62|Community 62]]
- [[_COMMUNITY_Community 63|Community 63]]
- [[_COMMUNITY_Community 64|Community 64]]
- [[_COMMUNITY_Community 65|Community 65]]
- [[_COMMUNITY_Community 66|Community 66]]
- [[_COMMUNITY_Community 67|Community 67]]
- [[_COMMUNITY_Community 68|Community 68]]
- [[_COMMUNITY_Community 69|Community 69]]
- [[_COMMUNITY_Community 70|Community 70]]
- [[_COMMUNITY_Community 71|Community 71]]
- [[_COMMUNITY_Community 72|Community 72]]
- [[_COMMUNITY_Community 75|Community 75]]
- [[_COMMUNITY_Community 77|Community 77]]
- [[_COMMUNITY_Community 78|Community 78]]
- [[_COMMUNITY_Community 79|Community 79]]
- [[_COMMUNITY_Community 80|Community 80]]
- [[_COMMUNITY_Community 81|Community 81]]
- [[_COMMUNITY_Community 82|Community 82]]
- [[_COMMUNITY_Community 83|Community 83]]
- [[_COMMUNITY_Community 84|Community 84]]
- [[_COMMUNITY_Community 85|Community 85]]
- [[_COMMUNITY_Community 86|Community 86]]
- [[_COMMUNITY_Community 87|Community 87]]
- [[_COMMUNITY_Community 88|Community 88]]
- [[_COMMUNITY_Community 89|Community 89]]
- [[_COMMUNITY_Community 90|Community 90]]
- [[_COMMUNITY_Community 91|Community 91]]
- [[_COMMUNITY_Community 92|Community 92]]
- [[_COMMUNITY_Community 94|Community 94]]

## God Nodes (most connected - your core abstractions)
1. `files.exclude` - 45 edges
2. `CinderSim` - 40 edges
3. `CinderSimTests` - 32 edges
4. `ActorView` - 21 edges
5. `Three.js Runtime Animation Asset Contract` - 17 edges
6. `com.unity.modules.jsonserialize` - 15 edges
7. `state` - 15 edges
8. `ReadmeEditor` - 14 edges
9. `Cinder Court — Frozen Simulation Spec (Unity port)` - 14 edges
10. `Concept → T-Pose → 3D Mesh → Rigging → Motion → Audio Pipeline` - 12 edges

## Surprising Connections (you probably didn't know these)
- `CinderSimTests` --references--> `float`  [EXTRACTED]
  Assets/Tests/EditMode/CinderSimTests.cs → Assets/Editor/SceneBuilder.cs
- `CinderSimTests` --references--> `int`  [EXTRACTED]
  Assets/Tests/EditMode/CinderSimTests.cs → Assets/Scripts/Sim/CinderSim.cs
- `CinderSim` --references--> `float`  [EXTRACTED]
  Assets/Scripts/Sim/CinderSim.cs → Assets/Editor/SceneBuilder.cs
- `ReadmeEditor` --references--> `float`  [EXTRACTED]
  Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs → Assets/Editor/SceneBuilder.cs
- `ActorView` --references--> `bool`  [EXTRACTED]
  Assets/Scripts/View/ActorView.cs → Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs

## Communities (93 total, 6 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.04
Nodes (46): dependencies, com.unity.ai.navigation, com.unity.collab-proxy, com.unity.ide.rider, com.unity.ide.visualstudio, com.unity.inputsystem, com.unity.modules.accessibility, com.unity.modules.adaptiveperformance (+38 more)

### Community 1 - "Community 1"
Cohesion: 0.22
Nodes (9): 2-A. Rodin Positive / Negative Prompt, 2-B. Rodin 조건 설정, 2-C. Rodin 제출 스크립트, 2-D. 다운로드 & Candidate Lane, code:text (Generate a game-ready humanoid character source mesh in a ge), code:text (terrain, floor, pedestal, rocks, platform, weapon, shield, s), code:bash (# 계획만 생성 (GUI Rodin 없이)), code:text (1. 다운로드한 GLB를 candidate lane에 복사) (+1 more)

### Community 2 - "Community 2"
Cohesion: 0.04
Nodes (45): files.exclude, **/*.3ds, **/*.asset, **/*.booproj, build/, **/*.cubemap, **/*.dll, **/.DS_Store (+37 more)

### Community 3 - "Community 3"
Cohesion: 0.06
Nodes (40): dependencies, depth, source, version, dependencies, depth, source, version (+32 more)

### Community 4 - "Community 4"
Cohesion: 0.20
Nodes (10): dependencies, depth, source, version, dependencies, depth, source, version (+2 more)

### Community 5 - "Community 5"
Cohesion: 0.08
Nodes (14): bool, Editor, CharacterImportPipeline, CinderCourt.EditorTools, ReadmeEditor, CinderCourt.EditorTools, SceneBuilder, GUIStyle (+6 more)

### Community 6 - "Community 6"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.collab-proxy

### Community 7 - "Community 7"
Cohesion: 0.05
Nodes (42): dependencies, depth, source, version, dependencies, depth, source, url (+34 more)

### Community 8 - "Community 8"
Cohesion: 0.06
Nodes (18): Animator, float, int, MaterialPropertyBlock, Mesh, MonoBehaviour, Renderer, Shader (+10 more)

### Community 9 - "Community 9"
Cohesion: 0.09
Nodes (25): depth, source, version, dependencies, depth, source, version, dependencies (+17 more)

### Community 10 - "Community 10"
Cohesion: 0.09
Nodes (22): 1. 절대 계약, 2. 현재 lane layout, 3. Rodin Bridge prompt, 4. Cartoon texture generation and mapping, 5-bis. Per-character cartoon albedo bake, 5. Character-only rigging and animation, 6. Verification checklist, 7. Useful references (+14 more)

### Community 11 - "Community 11"
Cohesion: 0.10
Nodes (20): blockers, consecutiveFailures, queued, schemaVersion, snapshotSeq, state, ambiguities, answers (+12 more)

### Community 12 - "Community 12"
Cohesion: 0.11
Nodes (18): 5. OVERLAY ANIMATION SYSTEM (Retargeting Layer), Bone Chain, Caching Strategy, code:block10 (idle (loop), move (loop), run (loop), hit, bighit, attack, c), code:block11 (delta[X][t] = inverse(target_rig_rest[X]) * absolute_retarge), code:block12 (adapted_clip[X][t] = C_rest[X] * delta[X][t]), code:javascript (function restQuatsFromGLB(gltf) {), code:javascript (adaptedOverlayEntriesByModel: Map<string, AdaptedEntry[]>) (+10 more)

### Community 13 - "Community 13"
Cohesion: 0.12
Nodes (17): 6-A. 환경 설정, 6-B. 오디오 생성 파이프라인, 6-C-1. 배경음악 (BGM), 6-C-2. 전투 효과음 (Combat SFX), 6-C-3. 적 등장/패배 효과음, 6-C-4. TTS 나레이션 (Character Voice), 6-C. 오디오 카테고리 및 프롬프트, 6-D. 출력 구조 (+9 more)

### Community 14 - "Community 14"
Cohesion: 0.12
Nodes (16): 5-A-1. Previs 실행, 5-A-2. Previs 설정 파라미터, 5-A-3. Export Bundle 구조, 5-A. Motion Previs Studio를 이용한 모션 분석, 5-B-1. 모션 데이터 임포트, 5-B-2. 전신 액션 Clip Authoring, 5-B-3. 저스트/하체 본드 스킨 (선택), 5-B. Blender NLA에서 애니메이션 Authoring (+8 more)

### Community 15 - "Community 15"
Cohesion: 0.09
Nodes (22): branch, current_turn_id, cwd, ended_at, event, final_response, artifact_path, format (+14 more)

### Community 16 - "Community 16"
Cohesion: 0.13
Nodes (14): Animation action set (11), Audio cues (ElevenLabs sound-generation API → mp3), Bosses (Unity 확장 — 원본 계약 외 신규), Cinder Court — Frozen Simulation Spec (Unity port), Determinism, Enemy (Ember Cohort 계열), Game over, Input (+6 more)

### Community 17 - "Community 17"
Cohesion: 0.13
Nodes (15): dependencies, depth, source, version, dependencies, depth, source, version (+7 more)

### Community 18 - "Community 18"
Cohesion: 0.06
Nodes (30): bones, decimatedTo, finalTriCount, finalVertexCount, heatOrphans, hygiene, looseRemoved, mergedVerts (+22 more)

### Community 19 - "Community 19"
Cohesion: 0.10
Nodes (20): bones, finalTriCount, finalVertexCount, heatOrphans, hygiene, looseRemoved, mergedVerts, input (+12 more)

### Community 20 - "Community 20"
Cohesion: 0.10
Nodes (20): bones, finalTriCount, finalVertexCount, heatOrphans, hygiene, looseRemoved, mergedVerts, input (+12 more)

### Community 21 - "Community 21"
Cohesion: 0.17
Nodes (12): 11. EXPECTED ASSET REFERENCES, Bosses (10), code:block27 (./assets/images/battle/glb/bosses/cinder-warden.glb), code:block28 (./assets/images/battle/glb/enemies/scout.glb), code:block29 (./assets/images/battle/glb/companions/ember-cohort.glb), code:block30 (./assets/images/battle/glb/commander/dusk-warden.glb), code:block31 (./assets/motion/ingame/unarmed-core.glb), Commander (1) (+4 more)

### Community 22 - "Community 22"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.inputsystem

### Community 23 - "Community 23"
Cohesion: 0.18
Nodes (11): 7-A. GLB 런타임 Lane 배포, 7-B. Three.js GLB 로딩 프롬프트 패턴, 7-C. 상태 머신 기반 애니메이션 전환, 7-D. 애니메이션 이벤트 → SFX 동기화, 7-E. GLB+Canvas Fallback 검증, code:bash (# 모든 캐릭터 일괄 리깅), code:javascript (// ex: battle-realtime-three.js — GLB 캐릭터 로딩 패턴), code:javascript (// 전투 FSM과 애니메이션 동기화) (+3 more)

### Community 24 - "Community 24"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, url, version, dependencies, depth, source (+3 more)

### Community 25 - "Community 25"
Cohesion: 0.18
Nodes (10): dotnet.defaultSolution, explorer.fileNesting.enabled, explorer.fileNesting.patterns, *.sln, *.slnx, files.associations, *.asset, *.meta (+2 more)

### Community 26 - "Community 26"
Cohesion: 0.20
Nodes (10): 2. CHARACTER IDENTIFIER & MODEL PATH CONTRACT, BOSS ACTORS (10 models), Character Identifiers by Type, code:block2 (const MODEL_ROOT = "./assets/images/battle/glb/";  // battle), code:block3 (const COMMANDER_MODEL = "commander/dusk-warden.glb";), COMMANDER (1 model), COMPANION ACTORS (9 models), ENEMY ARCHETYPES (4 models → 4 entity kinds) (+2 more)

### Community 27 - "Community 27"
Cohesion: 0.33
Nodes (6): gameover, bytes, durationSeconds, file, prompt, promptInfluence

### Community 28 - "Community 28"
Cohesion: 0.13
Nodes (15): dependencies, depth, source, version, dependencies, depth, source, version (+7 more)

### Community 29 - "Community 29"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, url (+3 more)

### Community 30 - "Community 30"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.particlesystem

### Community 31 - "Community 31"
Cohesion: 0.22
Nodes (8): 0. Lineage, 1. Engine perspective: Unity + WebGL only, 2. Numeric contract (제출문서 §2.3 이식), 3. Asset generation: fixed tool per asset class, 4. Workspace and evidence, 5. Concurrent-session Git safety (원작 이식, 무수정), 6. Reporting, HongT — Cinder Court (Unity) repository operating rules

### Community 32 - "Community 32"
Cohesion: 0.10
Nodes (3): CinderCourt.Tests, CinderSimTests, SimInput

### Community 33 - "Community 33"
Cohesion: 0.25
Nodes (8): 8. ANIMATION PLAYBACK CONTRACTS, Animation Mixer Update, code:javascript (// Locomotion (idle, move, run)), code:javascript (heightRatio      = targetHeight / MOTION_PROFILE_REFERENCE_H), Directional Hit Reaction Routing (2026-07-30 amendment), Loop Behavior, Mesh-Size-Aware Motion Profile (2026-07-30 amendment), Playback Integration

### Community 34 - "Community 34"
Cohesion: 0.25
Nodes (7): 10. ASSET MANIFEST & CHARACTER IDENTIFIERS, 12. IMPLEMENTATION CHECKLIST FOR BENCH RETARGETING, 14. CODE REFERENCES (Exact File:Line), 15. SUMMARY, Defense Asset Manifest, Motion Manifest, Three.js Runtime Animation Asset Contract

### Community 35 - "Community 35"
Cohesion: 0.25
Nodes (8): 4. SKELETON & RIG COMPATIBILITY CONTRACT, Bone Mapping Details, code:block7 (DEF-spine, DEF-spine.001, DEF-spine.002, DEF-spine.003, DEF-), code:block8 (mixamorig:HeadTop_End, mixamorig:LeftToe_End, mixamorig:Righ), code:block9 (DEF-pelvis.L, DEF-pelvis.R), Loop vs. One-Shot Expectations, Source Skeleton (Motion Bench), Target Skeleton (Runtime)

### Community 36 - "Community 36"
Cohesion: 0.29
Nodes (7): 3. ANIMATION CLIP NAMING & STRUCTURE CONTRACT, Animation Action Keys (11-13 per character), Animation Clip Requirements, Clip Naming Convention, code:block4 (unarmed-core::idle::v01), code:javascript ([), code:block6 (["idle", "move", "run"])

### Community 37 - "Community 37"
Cohesion: 0.29
Nodes (6): Binding docs (모두 읽을 것), LANE: Deterministic Simulation (owner: gjc), Mission, Reporting, Requirements, Verification (직접 실행할 것)

### Community 38 - "Community 38"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.accessibility

### Community 39 - "Community 39"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.ide.visualstudio

### Community 40 - "Community 40"
Cohesion: 0.33
Nodes (6): 7. SKELETON CLONING & ANIMATION BUILDING, Animation Mixer & Actions, code:javascript (const mixer = new THREE.AnimationMixer(instance);), code:javascript (const [gltf, overlayEntries] = await Promise.all([), Overlay Composition in instantiateActorModel(), Skeleton Cloning

### Community 41 - "Community 41"
Cohesion: 0.33
Nodes (6): 9. VALIDATION & TESTING CONTRACTS, code:bash (node tests/ingame-motion-pack.test.mjs), code:bash (node tests/combat-presentation-contract.test.mjs), code:bash (node tests/release-closure.test.mjs), Test Files, Validation Commands

### Community 42 - "Community 42"
Cohesion: 0.33
Nodes (6): 6. MODEL LOADING & INSTANTIATION CONTRACT, Actor Types & Their Loaders, code:block18 (boss: WORLD_SCALE * 0.9,        // 12.6 units), code:block19 (instantiationQueue = Promise.resolve();  // Chain work), Serialized Instantiation, Sizing Contract

### Community 43 - "Community 43"
Cohesion: 0.08
Nodes (14): _find_env_fallback(), generate(), main(), Walk up from the repo root looking for Abyssal-Surge/.env.game-audio., resolve_key(), Enemy, ICinderSim, List (+6 more)

### Community 44 - "Community 44"
Cohesion: 0.22
Nodes (8): code:text (┌───────────────────────────────────────────────────────────), Concept → T-Pose → 3D Mesh → Rigging → Motion → Audio Pipeline, 목차, 문서 참조, 스크립트 인덱스, 외부 툴 참조, 워크플로우 다이어그램, 참고 자료

### Community 45 - "Community 45"
Cohesion: 0.25
Nodes (8): 1-A. Reference Style을 위한 gti 프롬프트, 1-B. UV Atlas (텍스처 시트) 프롬프트, 1-C. T-Pose Blockout Mesh (Blender Procedural), code:bash (# dry-run으로 프롬프트 검증), code:bash (# refstyle 참고용 이미지 생성), code:bash (gti --prompt " \), code:bash (blender -b -P scripts/tpose_blockout.py -- \), Phase 1: 컨셉 이미지 기획(T-Pose 생성)

### Community 46 - "Community 46"
Cohesion: 0.40
Nodes (5): 0. 2026-07-29 NATURAL JOINT-MOTION AMENDMENT, Action and transition ownership, Release evidence, Skinning and joint articulation, Source and mesh boundary

### Community 47 - "Community 47"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.vehicles

### Community 48 - "Community 48"
Cohesion: 0.40
Nodes (5): 1. ASSET LOADER & FORMAT CONTRACT, code:block1 (1. If path is absolute or has ./ / ../ prefix → use as-is), File Format, Loader: GLTFLoader (Three.js), URL Resolution: `modelUrl(path)` (Line 729)

### Community 49 - "Community 49"
Cohesion: 0.40
Nodes (4): counters, gates, runtimeInstanceId, version

### Community 50 - "Community 50"
Cohesion: 0.14
Nodes (15): depth, source, version, dependencies, depth, source, version, dependencies (+7 more)

### Community 51 - "Community 51"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.wind

### Community 61 - "Community 61"
Cohesion: 0.25
Nodes (8): Phase 1 — 컨셉 → T-Pose 이미지, Phase 2 — 3D 메시 생성 (Rodin), Phase 3 — 텍스처 매핑, Phase 4 — 리깅, Phase 5 — 모션 애니메이션, Phase 6 — 오디오 (ElevenLabs), Phase 7 — 런타임 통합, 검증 체크리스트

### Community 62 - "Community 62"
Cohesion: 0.33
Nodes (6): 4-A. 리깅 스크립트 실행, 4-B. 리깅 규칙, 4-C. 11개 Action Clip 정의, code:bash (blender -b -P scripts/rig-character-asset-blender.py -- \), code:bash (python3 scripts/build-motion-prompt-batch.py \), Phase 4: 리깅(Rigging) & 스키닝

### Community 63 - "Community 63"
Cohesion: 0.33
Nodes (6): dependencies, dependencies, depth, source, version, com.unity.mathematics

### Community 64 - "Community 64"
Cohesion: 0.20
Nodes (11): dependencies, depth, dependencies, depth, source, url, version, source (+3 more)

### Community 65 - "Community 65"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.nuget.mono-cecil

### Community 66 - "Community 66"
Cohesion: 0.40
Nodes (5): 3-A. 텍스처 적용 스크립트, 3-B. Per-Character Albedo Bake, code:bash (# Candidate lane에 굽기), code:bash (python3 scripts/apply-cartoon-texture-blender.py \), Phase 3: 텍스처 매핑 & 카툰 렌더링

### Community 67 - "Community 67"
Cohesion: 0.07
Nodes (29): bones, decimatedTo, finalTriCount, finalVertexCount, heatOrphans, hygiene, looseRemoved, mergedVerts (+21 more)

### Community 68 - "Community 68"
Cohesion: 0.07
Nodes (28): bones, finalTriCount, finalVertexCount, heatOrphans, hygiene, looseRemoved, mergedVerts, input (+20 more)

### Community 69 - "Community 69"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.terrain

### Community 70 - "Community 70"
Cohesion: 0.07
Nodes (28): bones, finalTriCount, finalVertexCount, heatOrphans, hygiene, looseRemoved, mergedVerts, input (+20 more)

### Community 71 - "Community 71"
Cohesion: 0.07
Nodes (28): bones, finalTriCount, finalVertexCount, heatOrphans, hygiene, looseRemoved, mergedVerts, input (+20 more)

### Community 72 - "Community 72"
Cohesion: 0.15
Nodes (12): 1. 산출물, 2. 참조한 진실 소스, 3. 스펙 해석이 갈렸던 지점과 선택, 4. 결정론·성능 구현 노트, 5.1 문법/컴파일 게이트 (Mono csc, 레인 지정 명령), 5.2 EditMode 테스트 실제 실행 (Unity 없이), 5.3 할당 측정, 5. 실행한 검증 (+4 more)

### Community 77 - "Community 77"
Cohesion: 0.25
Nodes (7): Binding docs, Files to create (전부 namespace `CinderCourt.View`), Hard constraints, LANE: Presentation / View (owner: jeo), Mission, Reporting, Verification

### Community 78 - "Community 78"
Cohesion: 0.33
Nodes (6): bytes, durationSeconds, file, prompt, promptInfluence, bgm

### Community 79 - "Community 79"
Cohesion: 0.18
Nodes (11): dependencies, depth, source, version, dependencies, depth, source, version (+3 more)

### Community 80 - "Community 80"
Cohesion: 0.33
Nodes (6): hit, bytes, durationSeconds, file, prompt, promptInfluence

### Community 81 - "Community 81"
Cohesion: 0.10
Nodes (21): dependencies, depth, source, version, dependencies, depth, source, version (+13 more)

### Community 82 - "Community 82"
Cohesion: 0.40
Nodes (5): 13. COMPATIBILITY RISKS & CONSTRAINTS, OBSERVED Constraints, Risk: Adding New Character, Risk: Changing Clip Naming, Risk: Changing Motion Pack

### Community 83 - "Community 83"
Cohesion: 0.33
Nodes (6): kill, bytes, durationSeconds, file, prompt, promptInfluence

### Community 84 - "Community 84"
Cohesion: 0.33
Nodes (6): lore, bytes, durationSeconds, file, prompt, promptInfluence

### Community 85 - "Community 85"
Cohesion: 0.33
Nodes (6): nova, bytes, durationSeconds, file, prompt, promptInfluence

### Community 86 - "Community 86"
Cohesion: 0.33
Nodes (6): pickup, bytes, durationSeconds, file, prompt, promptInfluence

### Community 87 - "Community 87"
Cohesion: 0.33
Nodes (6): strike, bytes, durationSeconds, file, prompt, promptInfluence

### Community 88 - "Community 88"
Cohesion: 0.33
Nodes (6): ward, bytes, durationSeconds, file, prompt, promptInfluence

### Community 89 - "Community 89"
Cohesion: 0.33
Nodes (6): wave, bytes, durationSeconds, file, prompt, promptInfluence

### Community 90 - "Community 90"
Cohesion: 0.40
Nodes (4): cues, endpoint, generatedAt, tool

### Community 91 - "Community 91"
Cohesion: 0.40
Nodes (5): depth, source, url, version, com.unity.burst

### Community 92 - "Community 92"
Cohesion: 0.40
Nodes (5): depth, source, version, dependencies, com.unity.collections

### Community 94 - "Community 94"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.umbra

## Knowledge Gaps
- **753 isolated node(s):** `model`, `messages`, `schema_version`, `session_id`, `state` (+748 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `dependencies` connect `Community 50` to `Community 3`, `Community 4`, `Community 6`, `Community 7`, `Community 9`, `Community 17`, `Community 22`, `Community 24`, `Community 28`, `Community 29`, `Community 30`, `Community 38`, `Community 39`, `Community 47`, `Community 51`, `Community 63`, `Community 64`, `Community 65`, `Community 69`, `Community 79`, `Community 81`, `Community 91`, `Community 92`, `Community 94`?**
  _High betweenness centrality (0.063) - this node is a cross-community bridge._
- **Why does `CinderSim` connect `Community 43` to `Community 8`, `Community 5`?**
  _High betweenness centrality (0.009) - this node is a cross-community bridge._
- **Why does `float` connect `Community 8` to `Community 32`, `Community 43`, `Community 5`?**
  _High betweenness centrality (0.008) - this node is a cross-community bridge._
- **What connects `model`, `messages`, `Walk up from the repo root looking for Abyssal-Surge/.env.game-audio.` to the rest of the system?**
  _754 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.0425531914893617 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.044444444444444446 - nodes in this community are weakly interconnected._
- **Should `Community 3` be split into smaller, more focused modules?**
  _Cohesion score 0.05512820512820513 - nodes in this community are weakly interconnected._