// EnvironmentBuilder — docs/SIM_SPEC_ENVIRONMENT.md AMENDMENT #12 (§E2–§E7).
// View-only, deterministic, built ONCE at stage entry (frame-loop alloc 0).
//
// Architecture: EnvironmentLayout (bottom of file) is a PURE layout core —
// System.Math doubles only, no scene API — that turns a stageId into a flat
// module list. EnvironmentBuilder.Build materializes that list into shared-
// mesh/shared-material GameObjects and static-batches the root. Keeping the
// core pure lets determinism be proven byte-for-byte outside the editor
// (mono harness) while the EditMode suite stays the official §E8 gate.
//
// Module kit note (§E2 "소스" column): WallPillar/TorchPost are speced as
// library-prop clones "as needed"; the shipped library children are baked
// terrain micro-decals (rocks) whose renderer-bounds recentring would add a
// bounds-dependent step to the deterministic sequence and whose FBX materials
// would eat 2 of the 8-material budget. Both modules are therefore code boxes
// sharing the kit's stone/ember materials — same silhouette contract, tighter
// budget, zero bounds dependency.
//
// Determinism: FNV-1a stage seed ^ golden-ratio module-kind streams
// (CinderSim.PreparationHash finalizer grammar). No System.Random, no
// UnityEngine.Random, no scene state reads.
using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    /// <summary>
    /// docs/SIM_SPEC_ENVIRONMENT.md AMENDMENT #12. Deterministic, view-only
    /// dungeon environment: Zone A floor accents, Zone B boundary wall ring +
    /// two gate arches, Zone C outer verticality, §E6 lights.
    /// </summary>
    public static class EnvironmentBuilder
    {
        // §E3: derived from SimConfig margins — NO literals. The clamp stops
        // enemies at halfW-margin on x and halfH-margin/2 on y; the min of the
        // two axis ratios is the x one, so a single conservative constant is
        // the x-axis quotient (spec §E3 "구현은 보수적으로 min").
        //
        // AMENDMENT #15 (W-MV, MV-3): the derivation itself now lives in the sim
        // (DungeonBoundsSpec.EnemyStopE/PlayerStopE) so the clamp and the wall
        // ring can never disagree — sim test W-MV-7 pins that feeding the frozen
        // half-width back through it reproduces these exact numbers. The
        // no-argument properties stay FROZEN-derived on purpose: they are the
        // §E8 contract constants the EditMode gate pins. Callers that build an
        // expanded ring pass the published half-width to the *For overloads.
        /// <summary>Enemy stop line as an ellipse parameter at the FROZEN half-width (≈0.9538).</summary>
        public static float EnemyStopE => EnemyStopEFor(SimConfig.ArenaHalfWidth);

        /// <summary>Player stop line as an ellipse parameter at the FROZEN half-width (≈0.935).</summary>
        public static float PlayerStopE => PlayerStopEFor(SimConfig.ArenaHalfWidth);

        /// <summary>Enemy stop line for a sim-published half-width (AMENDMENT #15).</summary>
        public static float EnemyStopEFor(float halfWidth)
            => DungeonBoundsSpec.EnemyStopE(halfWidth);

        /// <summary>Player stop line for a sim-published half-width (AMENDMENT #15).</summary>
        public static float PlayerStopEFor(float halfWidth)
            => DungeonBoundsSpec.PlayerStopE(halfWidth);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        /// <summary>UV tiles per world unit (§E9 candidate 3 texture pass). One
        /// tile per 1.28 u = the §E2 module grid (128 sim-px), so a wall segment
        /// reads exactly one masonry tile and a long gallery deck repeats it
        /// instead of stretching.</summary>
        const float TilesPerWorldUnit = 1f / 1.28f;

        // ---- shared meshes (created once per domain, reused every build) ----
        static Mesh _cubeMesh;   // unit cube, ±0.5 — size rides localScale
        static Mesh _quadMesh;   // unit up-facing quad in XZ, ±0.5

        // ---- shared materials (≤4 of the 8 budget; tint via MPB only) ------
        static Material _stoneMaterial;    // opaque body: walls, decks, posts
        static Material _floorMaterial;    // opaque gloss-read accent panels
        static Material _emberMaterial;    // additive glow: gates, torch heads
        static Material _waterMaterial;    // transparent: sunken channels

        /// <summary>
        /// Builds the whole environment for a stage; returns a root named
        /// "StageEnvironment". Deterministic: same stageId → identical child
        /// (name, pos, rot, scale) sequence. Unknown stageId → null (no throw).
        /// </summary>
        public static GameObject Build(string stageId)
            => Build(stageId, SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);

        /// <summary>
        /// AMENDMENT #15 (W-MV, MV-2): same contract, but the Zone A/B geometry
        /// is laid out against the half-axes the SIM actually clamps to instead
        /// of the frozen constants. Feeding the frozen values reproduces the
        /// pre-amendment layout byte-for-byte (see MV-2 tests), so the arena,
        /// prologue and lobby paths — which call the one-argument overload —
        /// are structurally unable to move.
        /// </summary>
        public static GameObject Build(string stageId, float halfWidth, float halfHeight)
        {
            var modules = EnvironmentLayout.Compute(stageId, halfWidth, halfHeight);
            if (modules == null) return null;
            StageCatalog.TryGet(stageId, out var entry); // Compute proved it exists

            EnsureMeshes();
            EnsureMaterials();
            ApplyStageTextures(stageId);
            var tints = StageTints(entry.AccentColor);

            var root = new GameObject("StageEnvironment");
            for (var i = 0; i < modules.Count; i++)
                Materialize(root.transform, modules[i], in tints, entry.AccentColor);

            // §E7 (a): one combine at stage entry — never per frame.
            StaticBatchingUtility.Combine(root);
            return root;
        }

        // ------------------------------------------------------------ tints --
        readonly struct Tints
        {
            public readonly Color Stone, Floor, Ember, Water;
            public Tints(Color stone, Color floor, Color ember, Color water)
            {
                Stone = stone;
                Floor = floor;
                Ember = ember;
                Water = water;
            }
        }

        static Tints StageTints(Color accent)
        {
            var stoneBase = new Color(0.155f, 0.145f, 0.175f, 1f);
            var floorBase = new Color(0.235f, 0.225f, 0.26f, 1f);
            var stone = Color.Lerp(stoneBase, accent, 0.16f);
            var floor = Color.Lerp(floorBase, accent, 0.30f);
            // Additive glow: accent pushed toward the bloom threshold.
            var ember = new Color(
                Mathf.Clamp01(accent.r * 1.35f + 0.20f),
                Mathf.Clamp01(accent.g * 1.25f + 0.12f),
                Mathf.Clamp01(accent.b * 1.15f + 0.06f),
                0.85f);
            var water = new Color(
                accent.r * 0.25f + 0.03f,
                accent.g * 0.30f + 0.06f,
                accent.b * 0.45f + 0.12f,
                0.78f);
            return new Tints(stone, floor, ember, water);
        }

        // ------------------------------------------------------ materialize --
        static void Materialize(
            Transform root, in EnvironmentLayout.Module module, in Tints tints, Color accent)
        {
            var pivot = new GameObject(module.Name);
            pivot.transform.SetParent(root, false);
            pivot.transform.SetPositionAndRotation(
                ViewWorld.ToWorld(module.SimX, module.SimY, module.HeightWorld),
                Quaternion.Euler(0f, module.YawDeg, 0f));

            if (module.Kind == EnvironmentLayout.Kind.Light)
            {
                // §E6: exactly four realtime points — 2 gates + 2 seeded accents.
                var light = pivot.AddComponent<Light>();
                light.type = LightType.Point;
                // Ranges are world units and the floor grew 25% with
                // ViewWorld.Scale, so the legacy 4.2/3.2 radii would now light a
                // smaller share of the dungeon. Scaled by LegacyScaleRatio and
                // then opened up (×1.6) so the pools read as mood lighting
                // rather than four dots, with intensity raised to match.
                light.range = (module.LightRole < 2 ? 4.2f : 3.2f)
                              * ViewWorld.LegacyScaleRatio * 1.6f;
                light.intensity = module.LightRole < 2 ? 3.4f : 2.2f;
                light.color = module.LightRole == 0
                    ? new Color(1f, 0.62f, 0.30f)      // entrance: warm ember
                    : accent;                          // boss + accents: stage color
                light.shadows = LightShadows.None;     // §E6: caster 0
                // Torch mood: deterministic-phase flicker, no allocation.
                var flicker = pivot.AddComponent<LightFlicker>();
                flicker.Configure(light.intensity, module.LightRole);
                return;
            }

            var pieces = module.Pieces;
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                if (!string.IsNullOrEmpty(piece.LibraryPart))
                {
                    // Authored terrain part: carries its own mesh AND material,
                    // so it needs neither the code cube nor the env materials —
                    // and it is how gimmick furniture gets real rock silhouettes
                    // while the generated-texture path is still blocked.
                    SpawnLibraryPart(pivot.transform, piece, i, in tints);
                    continue;
                }
                var child = new GameObject("piece-" + i.ToString("D2"));
                child.transform.SetParent(pivot.transform, false);
                child.transform.localPosition =
                    new Vector3(piece.LocalX, piece.LocalY, piece.LocalZ);
                child.transform.localScale =
                    new Vector3(piece.SizeX, piece.SizeY, piece.SizeZ);

                child.AddComponent<MeshFilter>().sharedMesh =
                    piece.Quad ? _quadMesh : _cubeMesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                Color tint;
                switch (piece.Part)
                {
                    case EnvironmentLayout.Part.FloorPanel:
                        renderer.sharedMaterial = _floorMaterial;
                        tint = tints.Floor;
                        break;
                    case EnvironmentLayout.Part.Ember:
                        renderer.sharedMaterial = _emberMaterial;
                        tint = tints.Ember;
                        break;
                    case EnvironmentLayout.Part.Water:
                        renderer.sharedMaterial = _waterMaterial;
                        tint = tints.Water;
                        break;
                    default:
                        renderer.sharedMaterial = _stoneMaterial;
                        // Per-piece brightness variation stays in the ONE
                        // material via MPB (§E7 material budget).
                        tint = tints.Stone * piece.Shade;
                        tint.a = 1f;
                        break;
                }
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, tint);
                // Size-proportional tiling: pieces are unit meshes stretched by
                // localScale, so a fixed 0-1 UV would smear one texture across a
                // 5 u wall and squash it on a 0.3 u post. Tile at
                // TilesPerWorldUnit so texel density is constant. Rides the MPB
                // (not a material instance) to keep the §E7 material budget at 4
                // and preserve static batching.
                //
                // A cube's six faces have three different world size pairs
                // (X×Z, X×Y, Z×Y) but one MPB carries a single _BaseMap_ST, so
                // exact density on all six is impossible without per-face UV
                // scaling in a shared mesh. Resolve to the VISUALLY DOMINANT
                // face — the two largest extents — which is the face the 55°
                // camera actually reads on a wall/deck/post. The remaining thin
                // face is a sliver where density error is not perceptible.
                float su, sv;
                if (piece.Quad)
                {
                    su = piece.SizeX;          // quad lies in XZ
                    sv = piece.SizeZ;
                }
                else
                {
                    var mn = Mathf.Min(piece.SizeX, Mathf.Min(piece.SizeY, piece.SizeZ));
                    if (mn == piece.SizeY) { su = piece.SizeX; sv = piece.SizeZ; }
                    else if (mn == piece.SizeX) { su = piece.SizeZ; sv = piece.SizeY; }
                    else { su = piece.SizeX; sv = piece.SizeY; }
                }
                block.SetVector(BaseMapStId, new Vector4(
                    Mathf.Max(0.01f, su) * TilesPerWorldUnit,
                    Mathf.Max(0.01f, sv) * TilesPerWorldUnit, 0f, 0f));
                renderer.SetPropertyBlock(block);
            }
        }

        static GameObject _library;
        static readonly List<Transform> _propParts = new List<Transform>();
        static readonly List<Transform> _featureParts = new List<Transform>();

        /// <summary>
        /// Indexes the shared terrain library once per domain. prop (50) and
        /// feature (40) are the two families that cover 90 of the 94 authored
        /// parts between just TWO materials — staying inside them keeps the §E7
        /// material budget at 6 of 8 (env 4 + these 2).
        /// </summary>
        static void EnsureLibrary()
        {
            if (_library != null) return;
            _library = Resources.Load<GameObject>(
                "Terrain/terrain-" + StageCatalog.DressingLibraryTerrainId);
            if (_library == null) return;
            foreach (Transform child in _library.transform)
            {
                if (child.name.Contains("-prop-")) _propParts.Add(child);
                else if (child.name.Contains("-feature-")) _featureParts.Add(child);
            }
            // Deterministic order: Find/hierarchy order is stable, but sort by
            // name so a prefab re-serialize can never reshuffle the build.
            _propParts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _featureParts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        /// <summary>
        /// Clones one authored library child under the module pivot. Mirrors the
        /// GameDirector dressing grammar: the part's authored position lives in
        /// BAKED mesh vertices, so after cloning we measure live renderer bounds
        /// and counter the XZ offset — otherwise the rock lands wherever the FBX
        /// baked it instead of on the gimmick we are framing.
        /// </summary>
        static void SpawnLibraryPart(
            Transform pivot, EnvironmentLayout.Piece piece, int i, in Tints tints)
        {
            EnsureLibrary();
            var pool = piece.LibraryPart == "feature" ? _featureParts : _propParts;
            if (pool.Count == 0) return;

            // Which part: derived from the module's own placement so it is
            // deterministic and varied without another seed channel.
            var hash = (uint)(Mathf.RoundToInt(pivot.position.x * 977f)
                            ^ Mathf.RoundToInt(pivot.position.z * 613f) ^ (i * 131));
            var source = pool[(int)(hash % (uint)pool.Count)];

            var clone = UnityEngine.Object.Instantiate(source.gameObject, pivot);
            clone.name = "part-" + i.ToString("D2");
            clone.transform.localPosition = source.localPosition;
            clone.transform.localRotation = source.localRotation;
            clone.transform.localScale = source.localScale;

            var renderers = clone.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            // ORDER MATTERS. An authored part's position is baked into its mesh
            // vertices, so the XZ centering below solves P + S•C = pivot for a
            // FIXED scale S. Changing S afterwards re-breaks it by (k-1)•S•C —
            // the part drifts off the gimmick it is framing, and no gate sees
            // it because the clearance test inspects module PIVOTS, not these
            // child clones. So: finalise scale first, then centre.
            //
            // SizeY is a TARGET WORLD HEIGHT, not a multiplier. Multiplying and
            // then capping cancels the factor algebraically
            // (s•k•(cap/(h•k)) == s•cap/h), which would flatten every part to
            // one cap height and silently delete the per-gimmick grammar.
            // Solving for the height directly keeps vent shards low and altar
            // stones tall while §E8's occlusion cap still binds the maximum: a
            // part taller than the telegraph read height hides the ground disc
            // behind it at the 55° dungeon pitch.
            var probe = renderers[0].bounds;
            for (var r = 1; r < renderers.Length; r++) probe.Encapsulate(renderers[r].bounds);
            var sourceHeight = probe.size.y;
            var sourceHalf = Mathf.Max(probe.extents.x, probe.extents.z);
            // TWO constraints, and the tighter one wins. Solving height alone
            // scaled XZ by the same factor: a flat library decal has a small
            // h0, took a large multiplier, and the measured silhouette came
            // back at 0.701u half-extent - wide enough to sit ON the damage
            // disc it frames. The footprint cap is what keeps the inner edge on
            // the clearance line; the height cap keeps it from occluding the
            // telegraph behind it at the 55° pitch (§E0.5).
            // Uncapped pieces solve for their requested height only. Both caps
            // are E0.5 telegraph protection, and the outer ring stands past the
            // stop ellipse where no telegraph exists - clamping there would
            // render every silhouette as a 0.425 u chip, which is precisely the
            // flatness this pass was added to break.
            var target = piece.Uncapped
                ? piece.SizeY
                : Mathf.Min(piece.SizeY, EnvironmentLayout.FurnitureMaxHeight);
            var byHeight = sourceHeight > 1e-4f ? target / sourceHeight : float.MaxValue;
            var byFootprint = piece.Uncapped || sourceHalf <= 1e-4f
                ? float.MaxValue
                : EnvironmentLayout.FurnitureMaxHalfExtent / sourceHalf;
            // 2026-08 오브젝트 축소: decorative library parts (env-floor-*/feature/
            // prop pool) read at 0.70× — the same shrink actors take (ViewWorld
            // .DungeonObjectScale). Applied AFTER the height/footprint caps solve,
            // so shrinking can only ever move a part further UNDER the §E0.5
            // occlusion/clearance budget, never over it. Hazard bodies and
            // telegraphs are NOT built here (they size off sim Radius × Scale), so
            // this cannot desync a collision footprint. The sentinel branch is
            // guarded FIRST: MaxValue means "no measurable source bounds, leave the
            // authored scale" — multiplying the sentinel by 0.70 would turn it into
            // a finite ~2.4e38 that slips past the guard and explodes the part.
            var factor = Mathf.Min(byHeight, byFootprint);
            if (factor < float.MaxValue)
                clone.transform.localScale =
                    source.localScale * (factor * ViewWorld.DungeonObjectScale);

            // Re-measure at the FINAL scale, then counter the baked XZ offset.
            var bounds = renderers[0].bounds;
            for (var r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
            var delta = pivot.position - bounds.center;
            clone.transform.position += new Vector3(delta.x, 0f, delta.z);

            foreach (var r in renderers)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                // Same palette channel the cube path uses, and deliberately
                // NOT the accent: the vent telegraph is Color(1,0.6,0.3), so
                // orange IS the hazard channel on this stage. Tinting scenery
                // toward ember to make it pop would give decoration a live
                // disc's hue - camouflage, which is a worse §E0.5 break than
                // the invisibility it fixes. Separate by VALUE instead: stone
                // base is 0.155 against floorBase 0.235, so a charred multiplier
                // reads as burnt ground without borrowing the warning colour.
                // Nothing else is available - every env light is
                // LightShadows.None and these cast no shadow, so value contrast
                // is the only channel left.
                var tint = tints.Stone * piece.Shade;
                if (piece.Uncapped)
                {
                    // Outer silhouettes desaturate toward neutral stone.
                    // MEASURED at Shade 3.2: the ring came back rgb(34,15,13),
                    // saturation 0.63 against the floor's 0.13 and R/G 2.35
                    // against 1.12 - it read as raw flesh, not rock. tints.Stone
                    // carries the stage ACCENT (cinder-span is ember red) and
                    // the Shade boost amplifies that hue along with the value.
                    // Luminance was already right (20.2 vs floor 41.2 = 0.49,
                    // inside the E0.5 rule that the centre stays brightest), so
                    // only the chroma needed pulling.
                    var grey = tint.r * 0.299f + tint.g * 0.587f + tint.b * 0.114f;
                    tint = Color.Lerp(tint, new Color(grey, grey, grey, 1f), 0.72f);
                }
                tint.a = 1f;
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, tint);
                r.SetPropertyBlock(block);
            }
        }

        static void EnsureMeshes()
        {
            if (_cubeMesh == null)
            {
                // 24 verts (4 per face), NOT 8 shared corners: a shared corner
                // belongs to three faces that each need a different UV, so an
                // 8-vert cube cannot be textured at all. Unity treats a missing
                // uv channel as (0,0) everywhere, which samples one texel and
                // renders every face flat — the reason this had to change before
                // any albedo work (#12 §E9 candidate 3).
                _cubeMesh = new Mesh { name = "env-cube" };
                const float h = 0.5f;
                var v = new Vector3[24];
                var uv = new Vector2[24];
                var tri = new int[36];
                // face order: bottom, top, -z, +z, +x, -x
                var faces = new[]
                {
                    // origin, right, up. Unity normal = cross(right, up), and the
                    // seed material culls back faces (_Cull 2) — a flipped face
                    // vanishes from outside. top/-z/+x need r,u swapped relative
                    // to the naive axis order to keep their normals outward.
                    (new Vector3(-h, -h, -h), new Vector3(1, 0, 0), new Vector3(0, 0, 1)), // bottom -y
                    (new Vector3(-h,  h, -h), new Vector3(0, 0, 1), new Vector3(1, 0, 0)), // top    +y
                    (new Vector3(-h, -h, -h), new Vector3(0, 1, 0), new Vector3(1, 0, 0)), // -z
                    (new Vector3(-h, -h,  h), new Vector3(1, 0, 0), new Vector3(0, 1, 0)), // +z
                    (new Vector3( h, -h, -h), new Vector3(0, 1, 0), new Vector3(0, 0, 1)), // +x
                    (new Vector3(-h, -h, -h), new Vector3(0, 0, 1), new Vector3(0, 1, 0)), // -x
                };
                for (var f = 0; f < 6; f++)
                {
                    var (o, r, u) = faces[f];
                    var b = f * 4;
                    v[b] = o; v[b + 1] = o + r; v[b + 2] = o + r + u; v[b + 3] = o + u;
                    uv[b] = new Vector2(0, 0); uv[b + 1] = new Vector2(1, 0);
                    uv[b + 2] = new Vector2(1, 1); uv[b + 3] = new Vector2(0, 1);
                    var t = f * 6;
                    tri[t] = b; tri[t + 1] = b + 1; tri[t + 2] = b + 2;
                    tri[t + 3] = b; tri[t + 4] = b + 2; tri[t + 5] = b + 3;
                }
                _cubeMesh.vertices = v;
                _cubeMesh.uv = uv;
                _cubeMesh.triangles = tri;
                // NORMALS. Their absence is the bug that made the arena's boundary
                // wall render as blank pale slabs, and it took nine refuted hypotheses
                // to find because every one of them was about materials or textures.
                //
                // A Mesh with no normal channel feeds the shader normalOS = (0,0,0).
                // Everything downstream then collapses at once:
                //   lambert = saturate(dot(N, L))            -> 0, so the surface only
                //                                               ever gets _ShadowFloor
                //   rim     = pow(1 - dot(N, V), _RimPower)  -> pow(1, anything) = 1,
                //                                               so the rim runs at FULL
                //                                               strength over whole faces
                // and the result is a flat surface painted in _RimColor's blue with no
                // texture read and no directional light. MEASURED: deleting the rim
                // term moved the ring from (60, 66, 85) to (8, 9, 12) — 87% of it was
                // rim — while changing _RimPower from 4 to 10 changed nothing at all,
                // which is only possible when the base of that pow() is 1.
                //
                // 24 verts already exist precisely so each face can carry its own
                // normal (see the comment above), so RecalculateNormals produces hard
                // per-face normals here rather than the smoothed ones it would give a
                // shared-corner cube.
                _cubeMesh.RecalculateNormals();
                _cubeMesh.RecalculateBounds();
            }
            if (_quadMesh == null)
            {
                _quadMesh = new Mesh { name = "env-quad" };
                _quadMesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
                };
                _quadMesh.uv = new[]
                {
                    new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(1, 1), new Vector2(0, 1),
                };
                _quadMesh.triangles = new[] { 0, 3, 2, 0, 2, 1 }; // up-facing
                // Same omission as the cube, same consequence — see there. The quad is
                // planar and up-facing, so this is (0,1,0) at every vertex; it is
                // written as a Recalculate rather than a literal so the two meshes
                // cannot drift into disagreeing about how they get their normals.
                _quadMesh.RecalculateNormals();
                _quadMesh.RecalculateBounds();
            }
        }

        /// <summary>
        /// Resources path of the per-stage albedo maps generated by
        /// tools/gen_env_textures.sh (god-tibo-imagen, CLAUDE.md §3).
        /// </summary>
        internal const string StageTexturePath = "Textures/Env/";

        /// <summary>
        /// The cel-shaded stage albedo set (tools/gen_toon_env_textures.sh), tried
        /// BEFORE the PBR set above.
        ///
        /// Two directories rather than one, on purpose. They are different art
        /// directions that have to coexist while stage 02 is staged, and overwriting
        /// the PBR maps in place would destroy the set the shipped build still uses —
        /// this repo has already lost 36 committed textures once to an in-flight
        /// regeneration. Preferring Toon and falling back to Env means a partially
        /// generated toon set degrades stage by stage instead of leaving holes.
        /// </summary>
        internal const string ToonTexturePath = "Textures/Toon/";

        static string _texturedStageId;

        /// <summary>
        /// Loads the stage's generated stone/floor albedo maps, if present.
        /// Returns the stage id that is now bound to the shared materials.
        /// Only ONE stage is live at a time (GameDirector destroys the previous
        /// StageEnvironment before building the next), so rebinding the two
        /// shared materials keeps the §E7 4-material env budget intact instead
        /// of leaking one material pair per stage.
        /// </summary>
        internal static void ApplyStageTextures(string stageId)
        {
            if (_texturedStageId == stageId) return;
            _texturedStageId = stageId;
            var stone = Resources.Load<Texture2D>(ToonTexturePath + stageId + "-stone")
                     ?? Resources.Load<Texture2D>(StageTexturePath + stageId + "-stone");
            var floor = Resources.Load<Texture2D>(ToonTexturePath + stageId + "-floor")
                     ?? Resources.Load<Texture2D>(StageTexturePath + stageId + "-floor");
            BindAlbedo(_stoneMaterial, stone);
            BindAlbedo(_floorMaterial, floor);
        }

        static void BindAlbedo(Material material, Texture2D texture)
        {
            if (material == null) return;
            // A missing map must leave the flat-tint look intact, not a black
            // surface: clearing _BaseMap makes URP sample white.
            if (texture != null) texture.wrapMode = TextureWrapMode.Repeat;
            material.SetTexture(BaseMapId, texture);
        }

        static void EnsureMaterials()
        {
            // Stone/floor are LIT so the §E6 point lights and the StageMood rig
            // shade them (unlit surfaces ignore every light); ember/water stay
            // on the unlit-transparent seed grammar so their stripped WebGL
            // variants keep surviving the build.
            if (_stoneMaterial == null)
            {
                _stoneMaterial = ViewWorld.MakeLit(Color.white, null);
                _stoneMaterial.name = "env-stone";
            }
            if (_floorMaterial == null)
            {
                _floorMaterial = ViewWorld.MakeLit(Color.white, null);
                _floorMaterial.name = "env-floor";
            }
            if (_emberMaterial == null)
            {
                _emberMaterial = ViewWorld.MakeAdditive(Color.white);
                _emberMaterial.name = "env-ember";
            }
            if (_waterMaterial == null)
            {
                _waterMaterial = ViewWorld.MakeUnlit(Color.white, true);
                _waterMaterial.name = "env-water";
            }
        }
    }

    /// <summary>
    /// Pure deterministic layout core for AMENDMENT #12. System.Math doubles
    /// only — no scene API, no Unity math — so the module sequence can be
    /// proven byte-identical outside the editor. All coordinates are SIM
    /// coordinates (§E2); heights are world units.
    /// </summary>
    internal static class EnvironmentLayout
    {
        internal enum Kind { Floor, Wall, Pillar, Gate, Gallery, Bridge, Channel, Torch, Light }

        internal enum Part { Body, FloorPanel, Ember, Water }

        internal struct Piece
        {
            public Part Part;
            public bool Quad;
            public float LocalX, LocalY, LocalZ;   // world units, module-local
            public float SizeX, SizeY, SizeZ;      // world units
            public float Shade;                    // stone brightness multiplier
            /// <summary>
            /// Authored terrain library child to clone instead of the code cube
            /// (null = code primitive). Terrain parts carry their own authored
            /// mesh AND material, which is how gimmick furniture gets real rock
            /// silhouettes without generated textures. Size fields become the
            /// uniform scale source (SizeY) rather than a box extent.
            /// </summary>
            public string LibraryPart;
            /// <summary>
            /// Exempts this piece from the E0.5 occlusion cap. The cap exists so
            /// a gimmick's rim decoration cannot hide the ground telegraph
            /// behind it; outside the stop ellipse there are no telegraphs, so
            /// applying it there would flatten the exact silhouette being added.
            /// Only the outer ring sets this - everything inside stays capped.
            /// </summary>
            public bool Uncapped;
        }

        internal struct Module
        {
            public Kind Kind;
            public string Name;
            public float SimX, SimY;       // sim px — ViewWorld.ToWorld at materialize
            public float HeightWorld;      // world Y of the pivot
            public float YawDeg;
            public int LightRole;          // 0 entrance, 1 boss, 2/3 accents
            public List<Piece> Pieces;
        }

        // ---------------------------------------------------- sim geometry --
        const double Cx = SimConfig.ArenaX;
        const double Cy = SimConfig.ArenaY;
        // Mirrors ViewWorld.Scale (const there too). Grown with it 0.01 ->
        // 0.0125 -> 0.0150 so module footprints keep matching sim-space geometry.
        // DungeonFramingAndMoodTests.WorldScale_EnvironmentLayoutUsesTheSameQuotientAsViewWorld
        // asserts this equals ViewWorld.Scale to 1e-9.
        const double SimToWorld = 0.0150;

        // AMENDMENT #15 (W-MV, MV-2). These were `const double HalfW/HalfH =
        // SimConfig.Arena*` — the sim can now clamp to an expanded ellipse, and
        // a ring built against the frozen constants would let the player walk
        // straight through it. They are the ONLY per-build inputs besides the
        // stage id, and Compute assigns both at entry before touching anything
        // else, so the core stays a pure function of (stageId, halfW, halfH):
        // no call can observe another call's values. Defaults are the frozen
        // constants so an unconverted caller is a no-op.
        static double _halfW = SimConfig.ArenaHalfWidth;
        static double _halfH = SimConfig.ArenaHalfHeight;

        internal static double HalfW => _halfW;
        internal static double HalfH => _halfH;

        /// <summary>
        /// Stop-line ellipse parameter for the ACTIVE half-width. Algebraically
        /// identical to <see cref="DungeonBoundsSpec.EnemyStopE"/> — the sim's
        /// clamp derivation — but evaluated in DOUBLE on purpose: the whole
        /// layout core is double math, and rounding this one term through float
        /// would shift every ring module by ~1e-9 e and cost the byte-exact
        /// reproduction of the pre-amendment layout at frozen half-axes.
        /// </summary>
        static double StopE => (_halfW - SimConfig.EnemyMarginClamp) / _halfW;

        /// <summary>Painted backdrop plate's far edge in sim y (SceneBuilder's
        /// CourtBackdrop quad is 1536×1024 centred on sim (768, 512)).</summary>
        const double PlateBottomY = 1024.0;

        /// <summary>Floor left to a y-facing terrace after it retreats.</summary>
        const double TerraceMinDepth = 120.0;

        /// <summary>
        /// How far the stop-line ring has grown along y against the frozen
        /// geometry, in sim px. Exactly 0 at the frozen half-axes — the two
        /// operands are then the identical expression — so every placement that
        /// subtracts it is bit-for-bit unchanged on the arena/prologue path.
        /// </summary>
        static double RingGrowthY()
            => _halfH * StopE
               - SimConfig.ArenaHalfHeight
                 * ((SimConfig.ArenaHalfWidth - (double)SimConfig.EnemyMarginClamp)
                    / SimConfig.ArenaHalfWidth);

        // §E3 heights (contract): gallery +0.8, bridge +1.1, channel -0.5.
        const float GalleryH = 0.8f;

        /// <summary>
        /// AMENDMENT #17 stone-wall body height, world units. ONE constant read by both
        /// renderers of this blocker — EnvironmentBuilder's static module and
        /// VfxDirector's per-hazard primitive. Two literals would drift, and the
        /// symptom would be a wall that changes height depending on which path drew it.
        ///
        /// Equal to <see cref="GalleryH"/> rather than taller: at the 55 degree dungeon
        /// pitch a wall hides 0.70x its own height of ground behind it, and the lanes
        /// have vents in them whose telegraphs must stay readable (§E0.5). Waist height
        /// still reads solid from this angle because the top face is visible.
        /// </summary>
        internal const float StoneWallHeightWorld = GalleryH;
        const float BridgeH = 1.1f;
        const float ChannelH = -0.5f;

        // Clearance grammar (§E3/§E8): hazard.Radius + DressingHazardClearance
        // is the TEST line; per-kind extra margin keeps module EDGES clear too.
        const double ClearBase = 50.0;     // == StageCatalog.DressingHazardClearance
        const double WallSkipMargin = 64.0;   // half segment + relief jitter
        const double PostSkipMargin = 30.0;   // pillar/torch half width + jitter
        const double FloorSkipMargin = 40.0;  // keeps panel corners off telegraphs

        const double GateHalfArcPx = 160.0;   // gate half width 128 + 32 margin
        const double WallSegmentGapPx = 6.0;
        const double WallThicknessPx = 24.0;
        const double AlcoveTriggerPx = 160.0; // altar/pylon near ring → retreat
        const double AlcoveRetreatPx = 40.0;

        // -------------------------------------------------------- hash core --
        // §E5: seed = FNV1a(stageId) ^ (moduleKind * 0x9E3779B9); finalizer is
        // the CinderSim.PreparationHash grammar (frozen sim file, read-only).
        static uint Fnv1a(string text)
        {
            unchecked
            {
                var hash = 2166136261u;
                for (var i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        static uint Finalize(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0xC2B2AE35u;
                value ^= value >> 13;
                return value;
            }
        }

        static uint ModuleSeed(uint stageSeed, Kind kind, int index)
        {
            unchecked
            {
                return Finalize(stageSeed
                    ^ (uint)(kind + 1) * 0x9E3779B9u
                    ^ (uint)(index + 1) * 0x85EBCA6Bu);
            }
        }

        /// <summary>Hash stream for Fisher–Yates (§E5) — no System.Random.</summary>
        static uint NextInStream(ref uint stream)
        {
            unchecked { stream = Finalize(stream + 0x9E3779B9u); }
            return stream;
        }

        static void Shuffle<T>(IList<T> list, ref uint stream)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = (int)(NextInStream(ref stream) % (uint)(i + 1));
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Signed unit float from seed bits: [-1, +1].</summary>
        static double Signed(uint seed, int shift, int bits)
        {
            var mask = (1u << bits) - 1u;
            var raw = (seed >> shift) & mask;
            return raw / (double)mask * 2.0 - 1.0;
        }

        // -------------------------------------------------------- ring math --
        /// <summary>Point on the raw arena ellipse scaled by e; θ in radians,
        /// θ=π/2 is the entrance apex (sim +y = screen bottom).</summary>
        static void EllipsePoint(double e, double theta, out double x, out double y)
        {
            x = Cx + HalfW * e * Math.Cos(theta);
            y = Cy + HalfH * e * Math.Sin(theta);
        }

        static double EllipseE(double x, double y)
        {
            var nx = (x - Cx) / HalfW;
            var ny = (y - Cy) / HalfH;
            return Math.Sqrt(nx * nx + ny * ny);
        }

        const int ArcSamples = 2048;

        /// <summary>
        /// Equal-arc-length parametrization of the stop-line ring (§E5): the
        /// cumulative chord table maps arc lengths to θ. Start θ=π/2 (entrance),
        /// increasing θ. Deterministic — pure double math.
        /// </summary>
        static double[] BuildArcTable(double e, out double totalLength)
        {
            var table = new double[ArcSamples + 1];
            var previousX = 0.0;
            var previousY = 0.0;
            EllipsePoint(e, Math.PI / 2.0, out previousX, out previousY);
            table[0] = 0.0;
            for (var i = 1; i <= ArcSamples; i++)
            {
                var theta = Math.PI / 2.0 + i * (Math.PI * 2.0 / ArcSamples);
                EllipsePoint(e, theta, out var x, out var y);
                var dx = x - previousX;
                var dy = y - previousY;
                table[i] = table[i - 1] + Math.Sqrt(dx * dx + dy * dy);
                previousX = x;
                previousY = y;
            }
            totalLength = table[ArcSamples];
            return table;
        }

        static double ThetaAtArc(double[] table, double totalLength, double arc)
        {
            arc %= totalLength;
            if (arc < 0.0) arc += totalLength;
            // Binary search the cumulative table.
            int low = 0, high = ArcSamples;
            while (low + 1 < high)
            {
                var mid = (low + high) >> 1;
                if (table[mid] <= arc) low = mid;
                else high = mid;
            }
            var span = table[high] - table[low];
            var t = span > 0.0 ? (arc - table[low]) / span : 0.0;
            return Math.PI / 2.0 + (low + t) * (Math.PI * 2.0 / ArcSamples);
        }

        /// <summary>Outward unit radial (from arena center) at a sim point.</summary>
        static void Radial(double x, double y, out double rx, out double ry)
        {
            var dx = x - Cx;
            var dy = y - Cy;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9) { rx = 1.0; ry = 0.0; return; }
            rx = dx / length;
            ry = dy / length;
        }

        // (world yaw for arbitrary sim directions is derived inline where
        // needed — ring tangents in AddRingAndGates are the only consumers)

        // -------------------------------------------------- hazard filters --
        static bool NearAnyHazard(
            HazardConfig[] hazards, double x, double y, double extraMargin)
        {
            for (var i = 0; i < hazards.Length; i++)
            {
                var dx = x - hazards[i].X;
                var dy = y - hazards[i].Y;
                var limit = hazards[i].Radius + ClearBase + extraMargin;
                if (dx * dx + dy * dy < limit * limit) return true;
            }
            return false;
        }

        static bool NearAltarOrPylon(HazardConfig[] hazards, double x, double y)
        {
            for (var i = 0; i < hazards.Length; i++)
            {
                if (hazards[i].Kind != HazardKind.RelicAltar
                    && hazards[i].Kind != HazardKind.EmberPylon) continue;
                var dx = x - hazards[i].X;
                var dy = y - hazards[i].Y;
                if (dx * dx + dy * dy < AlcoveTriggerPx * AlcoveTriggerPx) return true;
            }
            return false;
        }

        static HazardKind NearestHazardKind(HazardConfig[] hazards, double x, double y)
        {
            var best = double.PositiveInfinity;
            var bestKind = HazardKind.EmberVent;
            for (var i = 0; i < hazards.Length; i++)
            {
                if (hazards[i].Kind == HazardKind.AshWall
                    || hazards[i].Kind == HazardKind.TideCurrent)
                    continue;

                var distance = DistanceFromHazardFootprint(hazards[i], x, y);
                if (distance >= best) continue;
                best = distance;
                bestKind = hazards[i].Kind;
            }

            return bestKind;
        }

        static double DistanceFromHazardFootprint(HazardConfig hazard, double x, double y)
        {
            if (hazard.Kind == HazardKind.StoneWall)
            {
                var ax = hazard.X - hazard.HalfW;
                var ay = hazard.Y - hazard.HalfH;
                var sx = hazard.HalfW * 2.0;
                var sy = hazard.HalfH * 2.0;
                var segmentSq = sx * sx + sy * sy;
                if (segmentSq > 1e-9)
                {
                    var t = ((x - ax) * sx + (y - ay) * sy) / segmentSq;
                    if (t < 0.0) t = 0.0;
                    else if (t > 1.0) t = 1.0;
                    var nx = ax + sx * t;
                    var ny = ay + sy * t;
                    var ndx = x - nx;
                    var ndy = y - ny;
                    return Math.Sqrt(ndx * ndx + ndy * ndy) - hazard.Radius;
                }
            }

            var dx = x - hazard.X;
            var dy = y - hazard.Y;
            return Math.Sqrt(dx * dx + dy * dy) - hazard.Radius;
        }

        // ------------------------------------- gimmick terrain furniture --
        //
        // The sim gimmicks ARE the level design (DUNGEON_GUIDE §0: dungeons are
        // told apart by their LAYOUT, not by new enemy types), but until now
        // they only acted as an exclusion mask — the environment stepped around
        // them and the floor stayed anonymous. This pass makes each gimmick
        // CARVE its own terrain:
        // a shaped ring of authored library parts that says "a vent burned this
        // ground" / "a sluice cut this channel" before the telegraph ever fires.
        //
        // Placement rule (§E8 row 3, unmodified): furniture sits OUTSIDE the
        // hazard clearance (Radius + ClearBase), never inside it. Ringing the
        // gimmick from beyond the safe margin frames it without luring the
        // player to stand at the edge of a damage disc — a vent's disc is
        // radius 90 and standing on its rim is exactly how a run dies. Kind is
        // therefore a normal GROUND kind and stays inside the existing
        // clearance contract; no spec amendment, no whitelist exemption.
        //
        // Height is capped (FurnitureMaxHeight) so a part never occludes the
        // ground telegraph behind it at the 55° dungeon pitch — the fairness
        // property §E0.5 exists to protect. As arithmetic rather than hope: a
        // part of height h hides ground up to h/tan(55°) behind it, so
        // 0.34/1.428 = 0.238u = 23.8 sim px — comfortably under ClearBase 50,
        // which means a capped part CANNOT reach the disc it stands beside.
        // MEASURED, not guessed. The first cut used 12px and the library's
        // actual silhouettes came back at 0.701u XZ half-extent (70 sim px):
        // a vent's furniture pivot sat at 152px but its inner edge reached
        // 81.9px - INSIDE the radius-90 damage disc it was supposed to frame.
        // The margin IS the silhouette budget, so it must be at least as large
        // as the half-extent we allow: pivot at Radius+ClearBase+margin with a
        // half-extent capped at margin puts the inner edge exactly on the
        // clearance line, never past it. Kept SMALL (30px, not 70) because a
        // wider ring pushes spokes past StopE and into neighbouring discs, so
        // most of them cull and the ring thins to debris - these parts are
        // micro-decals (file header), and a flat scorch read at the 55° pitch
        // is what they are shaped for.
        // ONE truth. The earlier draft had margin here and two more copies in
        // the Builder; the copies were the live pair and these were dead, so a
        // margin edit could compile clean and change nothing - the same
        // one-truth-in-several-places shape as the HalfW/HalfH mix-up. The
        // derived cap is a const expression, so it cannot drift.
        internal const double FurnitureRingMargin = 30.0;  // beyond Radius+ClearBase
        internal const float FurnitureMaxHalfExtent =
            (float)(FurnitureRingMargin * ViewWorld.Scale);
        /// <summary>
        /// Occlusion cap, expressed in SIM px like every other geometry term
        /// here (ClearBase, Radius, the ring margin) so it tracks ViewWorld.Scale
        /// instead of freezing at whatever quotient it was tuned under. It was
        /// a raw 0.34f world constant and a Scale bump 0.01 -> 0.0125 silently
        /// tightened it 34 -> 27.2 sim px; the footprint cap tracked because it
        /// was derived, this one did not.
        ///
        /// Why 34: a part of height h hides ground up to h/tan(55°) behind it at
        /// the dungeon pitch, so the reach is 34/1.428 = 23.8 sim px - inside
        /// ClearBase 50, which means a capped part provably CANNOT reach the
        /// telegraph disc it stands beside (§E0.5).
        /// </summary>
        const double FurnitureMaxHeightPx = 34.0;
        internal const float FurnitureMaxHeight =
            (float)(FurnitureMaxHeightPx * ViewWorld.Scale);

        /// <summary>
        /// Per-gimmick furniture grammar: how many parts ring it, which library
        /// family they come from, and how big. Families are limited to prop/
        /// feature — the two materials that cover 90 of the 94 authored parts —
        /// so the §E7 material budget stays at 6 of 8.
        /// </summary>
        static void AddGimmickTerrain(
            List<Module> modules, uint stageSeed, HazardConfig[] hazards)
        {
            var index = 0;
            for (var h = 0; h < hazards.Length; h++)
            {
                var hazard = hazards[h];
                // Band gimmicks get no furniture. AshWall sweeps the arena from
                // an edge and TideCurrent is a full-width push lane (HalfW 520
                // == the arena's own half-width): neither has a local
                // silhouette to frame, and the tide bands leave only 26/50/24px
                // of clear floor - the middle 50px being the documented safe
                // corridor a player survives in. Rails there would be smaller
                // than ClearBase and would clutter the one readable escape.
                // StoneWall is owned by VfxDirector's capsule path; adding floor
                // furniture here makes the blocker read as two overlapping props.
                if (hazard.Kind == HazardKind.AshWall
                 || hazard.Kind == HazardKind.TideCurrent
                 || hazard.Kind == HazardKind.StoneWall) continue;

                var ringR = hazard.Radius + ClearBase + FurnitureRingMargin;
                // scale = target height in SIM PX (× ViewWorld.Scale at use).
                // These were raw world literals tuned at Scale 0.01 - the same
                // drift trap as the cap above, one level down: after the 0.0125
                // bump every one of them fell UNDER the cap, so the ceiling went
                // inert and the real heights had quietly shrunk 20%.
                int count; string family; float scale; float shade;
                switch (hazard.Kind)
                {
                    case HazardKind.EmberVent:
                        // Scorched crater rim: sparse shards; texture carries the tone.
                        count = 4; family = "prop"; scale = 17f; shade = 0.55f;
                        break;
                    case HazardKind.ObsidianPillar:
                        // Outcrop base: few, chunkier, clustered.
                        count = 3; family = "feature"; scale = 26f; shade = 0.95f;
                        break;
                    case HazardKind.RelicAltar:
                        // Dais corners: symmetric, deliberate.
                        count = 3; family = "feature"; scale = 32f; shade = 1.08f;
                        break;
                    default: // EmberPylon
                        // Buttress feet around the breakable column.
                        count = 4; family = "prop"; scale = 22f; shade = 1.0f;
                        break;
                }

                for (var k = 0; k < count; k++)
                {
                    var seed = ModuleSeed(stageSeed, Kind.Floor, 9000 + h * 16 + k);
                    // Even spokes + seeded jitter so a ring never reads as a
                    // machine-stamped circle.
                    var theta = (2.0 * Math.PI * k) / count
                              + Signed(seed, 0, 5) * 0.14;
                    var r = ringR * (1.0 + Signed(seed, 5, 4) * 0.05);
                    var x = hazard.X + Math.Cos(theta) * r;
                    var y = hazard.Y + Math.Sin(theta) * r;
                    if (EllipseE(x, y) > StopE) continue;      // stays in Zone A
                    // Ringing hazard A must not park furniture inside hazard B.
                    // extraMargin 0 => limit == Radius+ClearBase; our own ring
                    // sits FurnitureRingMargin beyond that, so this rejects only
                    // foreign discs - the same filter every other pass uses.
                    if (NearAnyHazard(hazards, x, y, 0.0)) continue;
                    if (NearestHazardKind(hazards, x, y) != hazard.Kind) continue;

                    var module = NewModule(Kind.Floor,
                        "env-floor-" + (500 + index).ToString("D3"), x, y, 0f,
                        (float)(theta * 180.0 / Math.PI + Signed(seed, 9, 6) * 12.0));
                    module.Pieces.Add(new Piece
                    {
                        Part = Part.Body,
                        LibraryPart = family,
                        SizeY = scale * (float)ViewWorld.Scale
                                * (1f + (float)Signed(seed, 15, 4) * 0.12f),
                        Shade = shade,
                    });
                    modules.Add(module);
                    index++;
                }
            }
        }


        // ------------------------------------------------------ stage entry --
        /// <summary>Null for unknown stage ids (no throw) — Build's contract.</summary>
        internal static List<Module> Compute(string stageId)
            => Compute(stageId, SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);

        /// <summary>
        /// AMENDMENT #15 (W-MV): lay the stage out against the sim's ACTIVE
        /// clamp half-axes. The two assignments below are the first statements
        /// on purpose — every helper reads them, so nothing may run before they
        /// are set. Shrinking is refused the same way the sim refuses it
        /// (DungeonBoundsSpec.Resolve), so a bad caller cannot pull the ring
        /// inside the hazards.
        /// </summary>
        internal static List<Module> Compute(string stageId, double halfWidth, double halfHeight)
        {
            _halfW = halfWidth < SimConfig.ArenaHalfWidth ? SimConfig.ArenaHalfWidth : halfWidth;
            _halfH = halfHeight < SimConfig.ArenaHalfHeight ? SimConfig.ArenaHalfHeight : halfHeight;
            if (string.IsNullOrEmpty(stageId)) return null;
            if (!StageCatalog.TryGet(stageId, out _)) return null;
            // Pact table = effective base (override ?? anchor) + extras — a
            // SUPERSET of what test row 3 checks, so filtering against it is
            // strictly safer. Non-null for every catalog id (M3 contract).
            var hazards = StageCatalog.PactFor(stageId);
            if (hazards == null) return null;

            var stageSeed = Fnv1a(stageId);
            var palette = PaletteFor(stageId);
            var modules = new List<Module>(96);

            AddFloorPanels(modules, stageSeed, hazards);
            // Gimmick-carved terrain: runs after the generic floor so its parts
            // read as answers to the sim's own hazards, not as scatter.
            AddGimmickTerrain(modules, stageSeed, hazards);
            var gateInfo = AddRingAndGates(modules, stageSeed, hazards, palette);
            // The sim's hard blockers are NOT drawn here. VfxDirector owns per-hazard
            // geometry and already builds the StoneWall capsule; emitting a module too
            // drew every wall twice, and the duplicate tripped three audits that walk
            // env-* modules — the boundary-ring sync test read a blocker at e 0.58 as
            // "the ring never moved", the stop-line test read it as scenery standing on
            // the combat floor, and the hazard-clearance test measured the module
            // against the very hazard it was drawing. A blocker is a hazard, so it
            // belongs on the hazard path.
            // Zone C STAYS. #17 briefly removed it alongside the silhouettes, on the
            // reading that both dressed an annulus the widened playfield had eaten.
            // That was a misread of what this pass is: the silhouette ring is
            // decoration at e 1.35/1.50, but Zone C's terraces are the VOID-FLOOR
            // COVERAGE system — they tile out to the frustum (sim x -1650..3200), not
            // to a ring radius. Removing them exposed bare floor across 27.15% of the
            // frame against a pinned gate of 2%.
            AddZoneC(modules, stageId, stageSeed, palette);
            //
            // AddOuterSilhouettes is gone, and only that. It stands at e 1.35/1.50,
            // which at half 735 is sim x -224..1760 — past the painted plate's
            // -82..1618 and far past the camera frame's e 1.097. It would hang over
            // void to decorate something nobody can see. The boundary read it used to
            // provide now comes from the ring in AddRingAndGates, which tracks the
            // active half-axes (#15) and stands where the player actually stops.
            AddTorchesAndLights(modules, stageSeed, hazards, gateInfo);
            return modules;
        }
        // -------------------------------------- outer silhouettes (§E1.5) --
        //
        // MEASURED motivation. After the stage textures landed, the frame's
        // dominant 24-step colour bucket was still 61%, and a spatial map of
        // where that bucket lives put it in the TOP, BOTTOM and SIDE bands —
        // the annulus outside Zone C. Zone C stops at e 1.22 while the terrain
        // plate reaches e 1.63 in x and 2.92 in z, so that annulus is bare
        // lit plate with nothing standing on it. Pulling the camera to 17.5
        // brought it further into frame, which is what makes it worth filling.
        //
        // These are the key art's vertical elements (statues and spires ringing
        // the court) and they are the one thing the environment has never had:
        // silhouette. Every other module is a low slab.
        //
        // Height is UNCAPPED here, deliberately. FurnitureMaxHeight exists so a
        // gimmick's rim decoration cannot hide the ground telegraph behind it
        // (§E0.5). Out here there are no telegraphs — hazards live inside the
        // stop ellipse at e <= 0.954 — so the cap would only be flattening the
        // exact quality being added. What replaces it is a distance rule: these
        // stand at e >= 1.40, which is 0.45 past the stop line, so nothing they
        // occlude is anything the player has to read.
        // Radii are bounded by the VISIBLE WINDOW, computed for the pulled-in
        // camera rather than guessed: frustum half-width at the focus plane is
        // D*tan(21)*1.5 = 10.08 u, and the arena half-width is 6.5 u, so the
        // side of the frame lands at e ~ 1.55. The plate reaches e 1.63 in x -
        // already off-screen. Anything placed past 1.55 is only visible when
        // the player pans that way, which is how the VoidFloor passes wasted
        // two deploys. Zone C ends at 1.22, so the usable band is 1.22..1.55.
        internal const double SilhouetteE = 1.35;
        internal const double SilhouetteEOuter = 1.50;
        const float SilhouetteMinHeight = 90f;    // sim px
        const float SilhouetteMaxHeight = 210f;   // sim px

        static void AddOuterSilhouettes(
            List<Module> modules, uint stageSeed, StagePalette palette)
        {
            // Twelve slots on the clock, alternating radius so the ring reads as
            // a broken colonnade rather than a fence. Gate arcs stay clear: the
            // entrance and boss doors must stay legible from across the arena.
            var index = 0;
            for (var slot = 0; slot < 12; slot++)
            {
                var theta = slot * (2.0 * Math.PI / 12.0);
                var degrees = slot * 30.0;
                // 0 deg = entrance side, 180 deg = boss side (AddRingAndGates).
                if (degrees < 25 || degrees > 335) continue;
                if (degrees > 155 && degrees < 205) continue;

                var seed = ModuleSeed(stageSeed, Kind.Pillar, 4000 + slot);
                var e = (slot & 1) == 0 ? SilhouetteE : SilhouetteEOuter;
                e *= 1.0 + Signed(seed, 0, 4) * 0.04;
                EllipsePoint(e, theta, out var x, out var y);

                // No clamping. The first draft clamped into StageCatalog's
                // dressing plane and the gates caught two bugs from it: a
                // rectangular clamp moves points toward the CENTRE, which put
                // pillars at e 0.85 (inside the combat floor, E3 banned) and
                // 76 px from a vent (inside its clearance). The second draft
                // shrank along the ray instead, which was safe but built
                // nothing - the dressing plane is exactly the arena rectangle
                // (+-520x270), so every slot fell below Zone C.
                //
                // The plane was the wrong constraint entirely: it governs
                // StageCatalog's authored dressing, and Zone C already stands
                // outside it. What actually bounds this ring is the terrain
                // plate (+-850x788 sim px) and the visible window, and e 1.50
                // sits inside both - 780 px of 850 in x, 405 of 788 in z. So
                // the radii are the bound, and no clamp is needed.

                var height = SilhouetteMinHeight
                    + (float)((seed >> 8 & 0xFFu) / 255.0)
                      * (SilhouetteMaxHeight - SilhouetteMinHeight);
                var module = NewModule(Kind.Pillar,
                    "env-pillar-" + (700 + index).ToString("D3"), x, y, 0f,
                    (float)(degrees + Signed(seed, 18, 5) * 15.0));
                module.Pieces.Add(new Piece
                {
                    Part = Part.Body,
                    LibraryPart = "feature",
                    Uncapped = true,
                    SizeY = height * (float)SimToWorld,
                    // MEASURED: at Shade 0.88 the ring rendered at luminance
                    // 1.22 against a floor of 41.5 - 34x darker, internal std
                    // 0.58, i.e. a flat black cutout rather than a statue.
                    //
                    // The cause is the DOUBLE MULTIPLY, not lighting. I first
                    // wrote here that the ring "stands outside the four E6
                    // point lights, so it is lit by ambient alone" — that is
                    // FALSE and it is worth leaving the correction visible.
                    // StageMood's key (0.55) and fill (0.22) are DIRECTIONAL
                    // lights, which have no distance falloff and reach this
                    // ring exactly as they reach the arena; the E6 budget of 4
                    // covers POINT lights only. What actually crushed it is
                    // that SpawnLibraryPart writes tints.Stone x Shade into the
                    // MPB _BaseColor and URP Lit then multiplies that by the
                    // authored FBX albedo: 0.155 x 0.88 x ~0.35 = 0.048.
                    //
                    // So the fix is inverse-albedo compensation, expressed in
                    // the units this struct already uses. Still darker than the
                    // floor - the key art's statues are dark shapes against a
                    // lit court, just not holes in it.
                    Shade = 3.2f + (float)Signed(seed, 24, 4) * 0.35f,
                });
                // Warm point on the colonnade. The key art rings the court with
                // lit braziers, and that is the one element of it the frame
                // still lacks — the eight §E6 torches all sit at e ~1.01, just
                // outside the stop line, so the outer ring reads unlit.
                //
                // No new machinery and no budget: Part.Ember already routes to
                // _emberMaterial (ViewWorld.MakeAdditive), the same additive
                // quad the torch heads use, so this is a COUNT and PLACEMENT
                // change on shipping code. It also spends no light — §E6's
                // ceiling of 4 is POINT lights, and an additive quad is none.
                //
                // Height rides the silhouette it sits on, so a taller statue
                // carries its flame higher. Kept small (0.16 u) so the ring
                // reads as points, not as a second light source competing with
                // the arena centre (§E0.5).
                //
                // MEASURED, and this is why there are TWO. StageTints Clamp01s
                // the ember colour on purpose, so ONE additive quad lands just
                // UNDER CinderPostProfile's 1.05 bloom threshold and can never
                // flare on its own. The documented way past it is the one
                // ViewWorld.MakeAdditive states outright - overlapping quads
                // ACCUMULATE - so the head is two concentric shells rather
                // than a brighter tint. Doing it here keeps the change local:
                // unclamping StageTints would have relit every gate, bridge
                // and torch in all nine stages.
                //
                // Deployed numbers, one shell -> two, at the nine ring cores
                // visible in frame: core peak 131 -> 169 (+29%), and the
                // radial falloff softened from a 26% single-step drop to 20%.
                //
                // An earlier draft of this comment cited a "140 -> 44 in one
                // 3px step" cliff. That reading is RETRACTED: the sample sat
                // on a tan floor prop, not on a head. The conclusion survives
                // the correction - the numbers above are from the heads.
                var headY = height * (float)SimToWorld * 0.92f;
                module.Pieces.Add(new Piece
                {
                    Part = Part.Ember,
                    LocalY = headY,
                    SizeX = 0.16f, SizeY = 0.16f, SizeZ = 0.16f,
                    Shade = 1f,
                });
                module.Pieces.Add(new Piece   // inner shell: the accumulator
                {
                    Part = Part.Ember,
                    LocalY = headY,
                    SizeX = 0.11f, SizeY = 0.11f, SizeZ = 0.11f,
                    Shade = 1f,
                });
                modules.Add(module);
                index++;
            }
        }



        // ------------------------------------------------------ Zone A (§E3) --
        // Accent panels only (the plate is the real floor): target 6–10 glossy
        // quads at y+0.015 (z-fight guard), filtered against the PACT hazard
        // table (superset of the run table). The candidate grid keeps the WHOLE
        // panel inside the stop ellipse ("전부 e≤EnemyStopE 내접"): worst-case
        // half-extent is 75px (128px ±10% at yaw-snap ±3°), so with stop radii
        // (496, 257.5)px the mid row (|dy|=0) allows |dx| ≤ 399 and the ±108px
        // rows allow |dx| ≤ 274. Hazard-saturated stages legitimately keep
        // fewer panels — clearance always wins over the count target.
        //
        // AMENDMENT #15 (W-MV, MV-5): the tables are stored as OFFSETS from the
        // arena centre and scaled by the active half-axes, because the rows used
        // to be absolute sim coordinates tuned against a 520×270 ellipse. At the
        // frozen half-axes both scale factors are exactly 1.0, so `768 + (-368)
        // * 1.0` reproduces the old literal 400 bit-for-bit — this is a rewrite
        // of how the numbers are spelled, not of the numbers.
        static readonly double[] FloorMidRowDx =
            { -368, -272, -176, -80, 0, 80, 176, 272, 368 };
        static readonly double[] FloorOuterRowDx = { -256, -128, 0, 128, 256 };
        const double FloorOuterRowDy = 108.0;

        /// <summary>Worst-case panel half-extent: 128px ±10% at yaw-snap ±3°.</summary>
        const double FloorPanelWorstHalfExtent = 75.0;

        /// <summary>
        /// MV-5 expansion rows, as a fraction of the stop-line half-height. The
        /// three shipped rows cover |dy| ≤ 108 of a 257.5px stop radius — 42% of
        /// the floor. Expanding the ellipse to a 400px stop radius would leave
        /// everything past ±167px bare, which is exactly the "확장부 민무늬"
        /// failure MV-5 names. These two rows only exist when the ring actually
        /// grew, so the frozen candidate list is untouched.
        /// </summary>
        const double FloorExpansionRowFraction = 0.72;
        static readonly double[] FloorExpansionRowUnitDx = { -1.0, -0.5, 0.0, 0.5, 1.0 };
        /// <summary>Keeps the outermost expansion slot off the ring's own relief.</summary>
        const double FloorExpansionRowInset = 0.88;

        static void AddFloorPanels(
            List<Module> modules, uint stageSeed, HazardConfig[] hazards)
        {
            var scaleX = _halfW / SimConfig.ArenaHalfWidth;
            var scaleY = _halfH / SimConfig.ArenaHalfHeight;
            var outerDy = FloorOuterRowDy * scaleY;

            var candidates = new List<(double x, double y)>(
                FloorMidRowDx.Length + FloorOuterRowDx.Length * 2
                + FloorExpansionRowUnitDx.Length * 2);
            for (var i = 0; i < FloorOuterRowDx.Length; i++)
                candidates.Add((Cx + FloorOuterRowDx[i] * scaleX, Cy - outerDy));
            for (var i = 0; i < FloorMidRowDx.Length; i++)
                candidates.Add((Cx + FloorMidRowDx[i] * scaleX, Cy));
            for (var i = 0; i < FloorOuterRowDx.Length; i++)
                candidates.Add((Cx + FloorOuterRowDx[i] * scaleX, Cy + outerDy));
            AddFloorExpansionRows(candidates);

            var stream = ModuleSeed(stageSeed, Kind.Floor, 0);
            Shuffle(candidates, ref stream);
            var want = 6 + (int)((stageSeed >> 8) % 5u);   // 6..10 (§ change 4)
            // Panel DENSITY, not count, is what the §E3 target is about: an
            // expanded floor asking for the same 6..10 quads would read emptier
            // than the frozen one. Scale by the ring's area ratio (1.0 → no-op).
            var areaRatio = scaleX * scaleY;
            if (areaRatio > 1.0) want = (int)(want * areaRatio);

            var index = 0;
            for (var i = 0; i < candidates.Count && index < want; i++)
            {
                var (x, y) = candidates[i];
                if (NearAnyHazard(hazards, x, y, FloorSkipMargin)) continue;

                var seed = ModuleSeed(stageSeed, Kind.Floor, i + 1);
                var size = 1.28 * (1.0 + Signed(seed, 0, 4) * 0.10);   // 128px ±10%
                var yaw = ((seed >> 4) & 1u) * 90.0 + Signed(seed, 5, 6) * 3.0;
                var module = NewModule(Kind.Floor, "env-floor-" + index.ToString("D3"),
                    x, y, 0.015f, (float)yaw);
                module.Pieces.Add(new Piece
                {
                    Part = Part.FloorPanel,
                    Quad = true,
                    SizeX = (float)size,
                    SizeY = 1f,
                    SizeZ = (float)size,
                    Shade = 1f,
                });
                modules.Add(module);
                index++;
            }
        }

        /// <summary>
        /// MV-5: two extra candidate rows near the top and bottom of an EXPANDED
        /// stop ellipse. No-op at the frozen half-height — the guard is the ring
        /// itself, not a flag: the rows are only reachable once the ellipse is
        /// tall enough that a whole panel fits at ±0.72 of the stop radius with
        /// its worst-case half-extent clear of the ring.
        /// </summary>
        static void AddFloorExpansionRows(List<(double x, double y)> candidates)
        {
            if (_halfH <= SimConfig.ArenaHalfHeight) return;
            var e = StopE;
            var stopHalfW = _halfW * e;
            var stopHalfH = _halfH * e;
            var dy = FloorExpansionRowFraction * stopHalfH;
            // Ellipse half-chord at this row, minus the panel's own half-extent.
            var span = stopHalfW
                * Math.Sqrt(1.0 - FloorExpansionRowFraction * FloorExpansionRowFraction)
                - FloorPanelWorstHalfExtent;
            if (span <= 0.0) return;
            var reach = span * FloorExpansionRowInset;
            for (var i = 0; i < FloorExpansionRowUnitDx.Length; i++)
            {
                var x = Cx + FloorExpansionRowUnitDx[i] * reach;
                candidates.Add((x, Cy - dy));
                candidates.Add((x, Cy + dy));
            }
        }

        // ------------------------------------------------- Zone B ring (§E3) --
        internal struct GateInfo
        {
            public double EntranceX, EntranceY, BossX, BossY;
        }

        static GateInfo AddRingAndGates(
            List<Module> modules, uint stageSeed,
            HazardConfig[] hazards, StagePalette palette)
        {
            var e = StopE;
            var table = BuildArcTable(e, out var length);
            var slotCount = (int)Math.Round(length / 128.0);   // N ≈ 20 (§E5)
            var slotLength = length / slotCount;

            // Gates sit at the EXACT ring intersections of x=768 (§E3 landmark
            // rule — manual anchors, not procedural): entrance θ=π/2 (arc 0),
            // boss θ=3π/2 (arc L/2 by symmetry).
            EllipsePoint(e, Math.PI / 2.0, out var entranceX, out var entranceY);
            EllipsePoint(e, Math.PI * 1.5, out var bossX, out var bossY);

            var wallIndex = 0;
            var pillarBoundaries = new List<double>(4);
            var pillarEvery = palette.PillarEvery;
            for (var k = 0; k < slotCount; k++)
                if (k % pillarEvery == pillarEvery / 2)
                    pillarBoundaries.Add(k * slotLength);

            for (var k = 0; k < slotCount; k++)
            {
                var arc = (k + 0.5) * slotLength;
                var arcFromEntrance = Math.Min(arc, length - arc);
                var arcFromBoss = Math.Abs(arc - length * 0.5);
                if (arcFromEntrance < GateHalfArcPx || arcFromBoss < GateHalfArcPx)
                    continue;   // gate exclusion

                var theta = ThetaAtArc(table, length, arc);
                EllipsePoint(e, theta, out var baseX, out var baseY);

                // Breach rule: a slot whose base sits inside a vent telegraph
                // (+ clearance + half segment) or on an ash-wall crush edge is
                // SKIPPED — reads as a ruined ring, keeps test row 3 green.
                if (NearAnyHazard(hazards, baseX, baseY, WallSkipMargin)) continue;

                var seed = ModuleSeed(stageSeed, Kind.Wall, k);
                var relief = Signed(seed, 0, 4) * 6.0;                // ±6px (§E5)
                var retreat = NearAltarOrPylon(hazards, baseX, baseY)
                    ? AlcoveRetreatPx : 0.0;                          // alcove (§E3)
                Radial(baseX, baseY, out var rx, out var ry);
                var push = WallThicknessPx * 0.5 + relief + retreat;
                var x = baseX + rx * push;
                var y = baseY + ry * push;

                // Tangent (θ increasing) → module local +X along the ring.
                var tx = -HalfW * e * Math.Sin(theta);
                var ty = HalfH * e * Math.Cos(theta);
                var yaw = Math.Atan2(-ty, tx) * (-180.0 / Math.PI);
                var heightJitter = 1.0 + Signed(seed, 8, 4) * 0.06;

                var module = NewModule(Kind.Wall, "env-wall-" + wallIndex.ToString("D3"),
                    x, y, 0f, (float)yaw);
                var segment = (slotLength - WallSegmentGapPx) * SimToWorld;
                var wallHeight = 0.96 * heightJitter;                 // h96 (§E2)
                module.Pieces.Add(new Piece
                {
                    Part = Part.Body,
                    LocalY = (float)(wallHeight * 0.5),
                    SizeX = (float)segment,
                    SizeY = (float)wallHeight,
                    SizeZ = (float)(WallThicknessPx * SimToWorld),
                    Shade = (float)(0.92 + Signed(seed, 12, 4) * 0.08),
                });
                // Cap stone: slightly proud of the face — hand-laid read.
                module.Pieces.Add(new Piece
                {
                    Part = Part.Body,
                    LocalY = (float)(wallHeight + 0.035),
                    SizeX = (float)(segment * 1.04),
                    SizeY = 0.07f,
                    SizeZ = (float)(WallThicknessPx * SimToWorld * 1.3),
                    Shade = 1.12f,
                });
                modules.Add(module);
                wallIndex++;
            }

            // Joint pillars at slot boundaries (§E2: every 16 slots; palette
            // may densify). Same breach/gate filters as walls.
            var pillarIndex = 0;
            for (var p = 0; p < pillarBoundaries.Count; p++)
            {
                var arc = pillarBoundaries[p];
                var arcFromEntrance = Math.Min(arc, length - arc);
                var arcFromBoss = Math.Abs(arc - length * 0.5);
                if (arcFromEntrance < GateHalfArcPx || arcFromBoss < GateHalfArcPx)
                    continue;
                var theta = ThetaAtArc(table, length, arc);
                EllipsePoint(e, theta, out var baseX, out var baseY);
                if (NearAnyHazard(hazards, baseX, baseY, PostSkipMargin)) continue;

                var seed = ModuleSeed(stageSeed, Kind.Pillar, p);
                Radial(baseX, baseY, out var rx, out var ry);
                var x = baseX + rx * (WallThicknessPx * 0.5);
                var y = baseY + ry * (WallThicknessPx * 0.5);
                var module = NewModule(Kind.Pillar,
                    "env-pillar-" + pillarIndex.ToString("D3"), x, y, 0f,
                    (float)(Signed(seed, 0, 5) * 8.0));
                module.Pieces.Add(new Piece
                {
                    Part = Part.Body,
                    LocalY = 0.64f,
                    SizeX = 0.48f, SizeY = 1.28f, SizeZ = 0.48f,   // 48×h128 (§E2)
                    Shade = 1.05f,
                });
                module.Pieces.Add(new Piece
                {
                    Part = Part.Body,
                    LocalY = 1.31f,
                    SizeX = 0.6f, SizeY = 0.1f, SizeZ = 0.6f,
                    Shade = 1.15f,
                });
                modules.Add(module);
                pillarIndex++;
            }

            AddGate(modules, "env-gate-entrance", entranceX, entranceY, 0.0,
                stageSeed, palette.GateScale);
            AddGate(modules, "env-gate-boss", bossX, bossY, 180.0,
                stageSeed, palette.GateScale);

            return new GateInfo
            {
                EntranceX = entranceX, EntranceY = entranceY,
                BossX = bossX, BossY = bossY,
            };
        }

        static void AddGate(
            List<Module> modules, string name, double x, double y, double yaw,
            uint stageSeed, float scale)
        {
            // GateArch 256×h160 (§E2): two posts + lintel + additive glow strip.
            var module = NewModule(Kind.Gate, name, x, y, 0f, (float)yaw);
            var s = scale;
            for (var side = -1; side <= 1; side += 2)
            {
                module.Pieces.Add(new Piece
                {
                    Part = Part.Body,
                    LocalX = side * 1.04f * s,
                    LocalY = 0.80f * s,
                    SizeX = 0.48f * s, SizeY = 1.60f * s, SizeZ = 0.48f * s,
                    Shade = 1.08f,
                });
            }
            module.Pieces.Add(new Piece   // lintel
            {
                Part = Part.Body,
                LocalY = 1.72f * s,
                SizeX = 2.56f * s, SizeY = 0.30f * s, SizeZ = 0.52f * s,
                Shade = 1.16f,
            });
            module.Pieces.Add(new Piece   // ember glow strip under the lintel
            {
                Part = Part.Ember,
                LocalY = 1.52f * s,
                SizeX = 1.60f * s, SizeY = 0.10f * s, SizeZ = 0.20f * s,
                Shade = 1f,
            });
            modules.Add(module);
        }

        // ------------------------------------------------------ Zone C (§E4) --
        internal struct StagePalette
        {
            public Kind GenericKind;       // seeded outer-slot module kind
            public int PillarEvery;        // ring joint-pillar cadence
            public float GateScale;
            public bool EmberSeams;        // bridge lava/march glow strips
            public bool OuterWallRing;     // ember-bastion double ring
            public (Kind kind, double x, double y, float h,
                double sizeXpx, double sizeZpx, float yaw)[] Fixed;
        }

        // One factory per stage (§E4 table, verbatim palette → module grammar).
        // Deliberately SPLIT: a single switch holding every tuple-array
        // initializer inline concentrates enough IL locals in one method to
        // trip conservative runtimes (mono interp locals_size cap; IL2CPP
        // frame growth) — small per-stage factories stay trivially sized.
        static StagePalette PaletteFor(string stageId)
        {
            switch (stageId)
            {
                case "cinder-span": return CinderSpanPalette();
                case "ember-gallery": return EmberGalleryPalette();
                case "abyss-chancel": return AbyssChancelPalette();
                case "witness-well": return WitnessWellPalette();
                case "echo-throne": return EchoThronePalette();
                case "ash-verdict": return AshVerdictPalette();
                case "cinder-sluice": return CinderSluicePalette();
                case "ember-bastion": return EmberBastionPalette();
                case "ash-march": return AshMarchPalette();
                default:
                    // Unreachable behind StageCatalog.TryGet, but a future
                    // catalog stage without a palette must degrade to a plain
                    // ring, not divide-by-zero on PillarEvery.
                    return new StagePalette
                    {
                        GenericKind = Kind.Gallery, PillarEvery = 16, GateScale = 1f,
                        Fixed = Array.Empty<(Kind, double, double, float, double, double, float)>(),
                    };
            }
        }

        static StagePalette CinderSpanPalette() => new StagePalette
        {
            GenericKind = Kind.Bridge, PillarEvery = 16, GateScale = 1f,
            EmberSeams = true,
            Fixed = new[]
            {
                (Kind.Bridge, 320.0, 1010.0, BridgeH, 384.0, 64.0, 8f),
                (Kind.Bridge, 768.0, 1035.0, BridgeH, 384.0, 64.0, -4f),
                (Kind.Bridge, 1216.0, 1010.0, BridgeH, 384.0, 64.0, 6f),
            },
        };

        static StagePalette EmberGalleryPalette() => new StagePalette
        {
            GenericKind = Kind.Gallery, PillarEvery = 16, GateScale = 1f,
            Fixed = new[]
            {
                (Kind.Gallery, 380.0, 235.0, GalleryH, 240.0, 120.0, 0f),
                (Kind.Gallery, 640.0, 225.0, GalleryH, 240.0, 120.0, 0f),
                (Kind.Gallery, 900.0, 225.0, GalleryH, 240.0, 120.0, 0f),
                (Kind.Gallery, 1160.0, 235.0, GalleryH, 240.0, 120.0, 0f),
            },
        };

        static StagePalette AbyssChancelPalette() => new StagePalette
        {
            GenericKind = Kind.Gallery, PillarEvery = 4, GateScale = 1f,
            Fixed = new[]
            {
                (Kind.Gallery, 140.0, 604.0, GalleryH, 200.0, 380.0, 90f),
                (Kind.Gallery, 1396.0, 604.0, GalleryH, 200.0, 380.0, -90f),
            },
        };

        static StagePalette WitnessWellPalette() => new StagePalette
        {
            GenericKind = Kind.Channel, PillarEvery = 16, GateScale = 1f,
            Fixed = BuildWellRing(),
        };

        static StagePalette EchoThronePalette() => new StagePalette
        {
            GenericKind = Kind.Gallery, PillarEvery = 16, GateScale = 1f,
            Fixed = new[]
            {
                (Kind.Gallery, 768.0, 235.0, GalleryH, 420.0, 200.0, 0f),
                (Kind.Gallery, 768.0, 225.0, GalleryH + 0.25f, 340.0, 160.0, 0f),
                (Kind.Gallery, 768.0, 215.0, GalleryH + 0.5f, 260.0, 120.0, 0f),
            },
        };

        static StagePalette AshVerdictPalette() => new StagePalette
        {
            GenericKind = Kind.Gallery, PillarEvery = 16, GateScale = 1f,
            Fixed = new[]
            {
                (Kind.Gallery, 185.0, 604.0, GalleryH, 240.0, 420.0, 90f),
                (Kind.Gallery, 95.0, 604.0, GalleryH + 0.35f, 160.0, 420.0, 90f),
                (Kind.Gallery, 1351.0, 604.0, GalleryH, 240.0, 420.0, -90f),
                (Kind.Gallery, 1441.0, 604.0, GalleryH + 0.35f, 160.0, 420.0, -90f),
            },
        };

        static StagePalette CinderSluicePalette() => new StagePalette
        {
            GenericKind = Kind.Channel, PillarEvery = 16, GateScale = 1f,
            Fixed = new[]
            {
                // Two lanes visually extending the sim current bands
                // (y 470/740) past the ring on both sides (§E4).
                (Kind.Channel, 40.0, 470.0, ChannelH, 420.0, 140.0, 0f),
                (Kind.Channel, 40.0, 740.0, ChannelH, 420.0, 140.0, 0f),
                (Kind.Channel, 1496.0, 470.0, ChannelH, 420.0, 140.0, 0f),
                (Kind.Channel, 1496.0, 740.0, ChannelH, 420.0, 140.0, 0f),
            },
        };

        static StagePalette EmberBastionPalette() => new StagePalette
        {
            GenericKind = Kind.Gallery, PillarEvery = 16, GateScale = 1.18f,
            OuterWallRing = true,
            Fixed = Array.Empty<(Kind, double, double, float, double, double, float)>(),
        };

        static StagePalette AshMarchPalette() => new StagePalette
        {
            GenericKind = Kind.Bridge, PillarEvery = 16, GateScale = 1f,
            EmberSeams = true,
            Fixed = new[]
            {
                (Kind.Bridge, 420.0, 200.0, BridgeH, 220.0, 70.0, 0f),
                (Kind.Bridge, 650.0, 190.0, BridgeH, 220.0, 70.0, 0f),
                (Kind.Bridge, 880.0, 190.0, BridgeH, 220.0, 70.0, 0f),
                (Kind.Bridge, 1110.0, 200.0, BridgeH, 220.0, 70.0, 0f),
                // 행진로 발광 스트립: glowing road strips on the void
                // floor south of the ring (pivot -0.3, ember quad).
                (Kind.Channel, 560.0, 950.0, -0.30f, 360.0, 40.0, 0f),
                (Kind.Channel, 980.0, 950.0, -0.30f, 360.0, 40.0, 0f),
            },
        };

        static (Kind, double, double, float, double, double, float)[] BuildWellRing()
        {
            // 우물 링 (§E4): six sunken-channel arcs orbiting at e 1.16.
            var ring = new (Kind, double, double, float, double, double, float)[6];
            for (var i = 0; i < 6; i++)
            {
                var theta = i * (Math.PI / 3.0);
                EllipsePoint(1.16, theta, out var x, out var y);
                var yaw = (float)(Math.Atan2(
                    HalfH * Math.Cos(theta), HalfW * Math.Sin(theta)) * 180.0 / Math.PI);
                ring[i] = (Kind.Channel, x, y, ChannelH, 220.0, 110.0, yaw);
            }
            return ring;
        }

        // Generic outer slots: 12 static ring positions (§E5 slot table),
        // Fisher–Yates seeded shuffle, 90° snap ± ≤12° jitter.
        static readonly double[] GenericSlotTheta =
            { 15, 45, 75, 105, 135, 165, 195, 225, 255, 285, 315, 345 };

        static void AddZoneC(
            List<Module> modules, string stageId, uint stageSeed, StagePalette palette)
        {
            var counters = new Dictionary<Kind, int>
            {
                { Kind.Gallery, 0 }, { Kind.Bridge, 0 }, { Kind.Channel, 0 },
            };

            // 1) Coverage terraces — the §E1.5 backbone (Main 2026-08-07: the
            // goal is REDUCING VoidFloor exposure in frame, so Zone C must
            // stretch to the frustum). Camera-frame arithmetic (pitch 55°,
            // FOV 42, crowd orbit 24.5, 21:9): the UNFOGGED ground band spans
            // sim x ≈ -1650..3200 and sim y ≈ 256..1510; the plate covers
            // 0..1536 × 0..1024. Four terraces tile the remainder — wings
            // OVERLAP the plate edge slightly (x 0/1536) so no sample-width
            // sliver survives at the seam (32×32 grid, verified numerically:
            // bare 526/1024 → 0/1024). Terraces sit far outside the ring
            // (nearest edge e ≥ 1.3), so zone tests never see them inside.
            //
            // AMENDMENT #15 (W-MV, MV-5): "far outside the ring" was true of the
            // FROZEN ring only. The north rim spans y −70..270 while an expanded
            // ring reaches y ≈ 192 and the player's own reach reaches y ≈ 212 —
            // the player would walk into a +0.8 u raised deck. The two y-facing
            // terraces therefore keep their OUTER edges (frustum bounds, which
            // do not move) and retreat their INNER edges by exactly the ring's
            // growth. The x-facing wings need nothing: even the expanded ring
            // reaches only x 238..1298 and they end at x 20 / start at x 1500.
            var growthY = RingGrowthY();
            // The south apron never retreats past the plate's own bottom edge —
            // the band it would vacate is bare VoidFloor, and that is the one
            // thing the §E8 coverage gate measures.
            var apronInner = Math.Min(1010.0 + growthY, PlateBottomY);
            AddTerrace(modules, counters, palette.GenericKind, stageSeed, 0,
                775.0, (apronInner + 1570.0) * 0.5, 4900.0, 1570.0 - apronInner);
            AddTerrace(modules, counters, palette.GenericKind, stageSeed, 1,
                -860.0, 730.0, 1760.0, 1740.0);      // west wing x -1740..20
            AddTerrace(modules, counters, palette.GenericKind, stageSeed, 2,
                2380.0, 730.0, 1760.0, 1740.0);      // east wing x 1500..3260
            // The north rim retreats ONTO the plate (y 0..1024), so nothing it
            // vacates is void either.
            var rimInner = Math.Max(270.0 - growthY, -70.0 + TerraceMinDepth);
            AddTerrace(modules, counters, palette.GenericKind, stageSeed, 3,
                775.0, (rimInner - 70.0) * 0.5, 4900.0, rimInner + 70.0);   // north rim, derived

            // 2) Fixed landmarks (§E4 identity — manual anchors, no shuffle).
            for (var i = 0; i < palette.Fixed.Length; i++)
            {
                var (kind, x, y, h, sizeX, sizeZ, yaw) = palette.Fixed[i];
                AddZoneCModule(modules, counters, kind, x, y, h, sizeX, sizeZ, yaw,
                    ModuleSeed(stageSeed, kind, 100 + i), palette.EmberSeams);
            }

            // 3) Seeded generic slots: count tops the palette up into the §E5
            // 8–14 band (terraces 4 + fixed + generic).
            var slots = new List<int>(GenericSlotTheta.Length);
            for (var i = 0; i < GenericSlotTheta.Length; i++) slots.Add(i);
            var stream = ModuleSeed(stageSeed, palette.GenericKind, 999);
            Shuffle(slots, ref stream);
            var generic = 8 + (int)((stageSeed >> 16) % 7u) - 4 - palette.Fixed.Length;
            if (generic < 0) generic = 0;
            if (generic > slots.Count) generic = slots.Count;

            for (var g = 0; g < generic; g++)
            {
                var slot = slots[g];
                var seed = ModuleSeed(stageSeed, palette.GenericKind, 200 + slot);
                var e = (slot & 1) == 0 ? 1.10 : 1.22;
                var theta = GenericSlotTheta[slot] * Math.PI / 180.0;
                EllipsePoint(e, theta, out var x, out var y);
                var snap = 90f * (int)((seed >> 12) & 3u);
                var yaw = snap + (float)(Signed(seed, 16, 6) * 12.0);   // §E5
                var h = palette.GenericKind == Kind.Channel ? ChannelH
                    : palette.GenericKind == Kind.Bridge ? BridgeH : GalleryH;
                double sizeX = palette.GenericKind == Kind.Bridge ? 200.0 : 256.0;
                double sizeZ = palette.GenericKind == Kind.Bridge ? 64.0 : 128.0;
                AddZoneCModule(modules, counters, palette.GenericKind,
                    x, y, h, sizeX, sizeZ, yaw, seed, palette.EmberSeams);
            }

            // 4) ember-bastion 이중 링 (§E4): sparse outer battlement ring.
            if (palette.OuterWallRing)
                AddOuterWallRing(modules, stageSeed);
        }

        static void AddTerrace(
            List<Module> modules, Dictionary<Kind, int> counters, Kind kind,
            uint stageSeed, int terraceIndex, double x, double y,
            double sizeXpx, double sizeZpx)
        {
            var seed = ModuleSeed(stageSeed, kind, 50 + terraceIndex);
            var h = kind == Kind.Channel ? ChannelH
                : kind == Kind.Bridge ? BridgeH : GalleryH;
            var module = NewModule(kind, NextName(counters, kind), x, y, h, 0f);
            var worldX = (float)(sizeXpx * SimToWorld);
            var worldZ = (float)(sizeZpx * SimToWorld);

            if (kind == Kind.Channel)
            {
                // Sunken slab: top -0.28 clears the VoidFloor (-0.35); water on top.
                module.Pieces.Add(new Piece
                {
                    Part = Part.Body, LocalY = 0.11f,
                    SizeX = worldX, SizeY = 0.22f, SizeZ = worldZ,
                    Shade = (float)(0.85 + Signed(seed, 0, 4) * 0.06),
                });
                module.Pieces.Add(new Piece
                {
                    Part = Part.Water, Quad = true, LocalY = 0.26f,
                    SizeX = worldX * 0.96f, SizeY = 1f, SizeZ = worldZ * 0.9f,
                    Shade = 1f,
                });
            }
            else
            {
                module.Pieces.Add(new Piece   // deck slab (pivot = deck top)
                {
                    Part = Part.Body, LocalY = -0.125f,
                    SizeX = worldX, SizeY = 0.25f, SizeZ = worldZ,
                    Shade = (float)(0.9 + Signed(seed, 0, 4) * 0.08),
                });
                module.Pieces.Add(new Piece   // skirt mass down to the void floor
                {
                    Part = Part.Body,
                    LocalY = (float)(-(module.HeightWorld + 0.35) * 0.5 - 0.125),
                    SizeX = worldX * 0.94f,
                    SizeY = (float)(module.HeightWorld + 0.35 - 0.25),
                    SizeZ = worldZ * 0.94f,
                    Shade = 0.72f,
                });
            }
            modules.Add(module);
        }

        static void AddZoneCModule(
            List<Module> modules, Dictionary<Kind, int> counters, Kind kind,
            double x, double y, float h, double sizeXpx, double sizeZpx, float yaw,
            uint seed, bool emberSeams)
        {
            var module = NewModule(kind, NextName(counters, kind), x, y, h, yaw);
            var worldX = (float)(sizeXpx * SimToWorld);
            var worldZ = (float)(sizeZpx * SimToWorld);
            switch (kind)
            {
                case Kind.Bridge:
                {
                    if (h <= 0f)
                    {
                        // March strip variant (ash-march): pure glow quad.
                        module.Pieces.Add(new Piece
                        {
                            Part = Part.Ember, Quad = true,
                            SizeX = worldX, SizeY = 1f, SizeZ = worldZ, Shade = 1f,
                        });
                        break;
                    }
                    // BrokenBridge 384×64, 2 spans + gap (§E2).
                    var spanA = worldX * 0.46f;
                    var spanB = worldX * 0.375f;
                    module.Pieces.Add(new Piece
                    {
                        Part = Part.Body, LocalX = -worldX * 0.25f, LocalY = -0.07f,
                        SizeX = spanA, SizeY = 0.14f, SizeZ = worldZ, Shade = 0.95f,
                    });
                    module.Pieces.Add(new Piece
                    {
                        Part = Part.Body, LocalX = worldX * 0.29f, LocalY = -0.09f,
                        SizeX = spanB, SizeY = 0.14f, SizeZ = worldZ, Shade = 0.88f,
                    });
                    for (var side = -1; side <= 1; side += 2)
                        module.Pieces.Add(new Piece   // support piers to ground
                        {
                            Part = Part.Body,
                            LocalX = side * worldX * 0.42f,
                            LocalY = (float)(-(h + 0.35) * 0.5 - 0.14),
                            SizeX = 0.3f, SizeY = (float)(h + 0.35 - 0.14), SizeZ = 0.3f,
                            Shade = 0.78f,
                        });
                    if (emberSeams)
                        module.Pieces.Add(new Piece   // lava glow in the break
                        {
                            Part = Part.Ember, Quad = true,
                            LocalX = worldX * 0.035f,
                            LocalY = (float)(-h - 0.28),   // just above VoidFloor
                            SizeX = worldX * 0.16f, SizeY = 1f, SizeZ = worldZ * 1.3f,
                            Shade = 1f,
                        });
                    break;
                }
                case Kind.Channel:
                {
                    if (h > -0.4f)
                    {
                        // Shallow strip variant (march road): glow quad only.
                        module.Pieces.Add(new Piece
                        {
                            Part = Part.Ember, Quad = true,
                            SizeX = worldX, SizeY = 1f, SizeZ = worldZ, Shade = 1f,
                        });
                        break;
                    }
                    module.Pieces.Add(new Piece   // bed slab, top above VoidFloor
                    {
                        Part = Part.Body, LocalY = 0.11f,
                        SizeX = worldX, SizeY = 0.22f, SizeZ = worldZ, Shade = 0.8f,
                    });
                    module.Pieces.Add(new Piece   // water sheet
                    {
                        Part = Part.Water, Quad = true, LocalY = 0.26f,
                        SizeX = worldX * 0.94f, SizeY = 1f, SizeZ = worldZ * 0.82f,
                        Shade = 1f,
                    });
                    for (var side = -1; side <= 1; side += 2)
                        module.Pieces.Add(new Piece   // rim walls
                        {
                            Part = Part.Body,
                            LocalY = 0.29f, LocalZ = side * worldZ * 0.5f,
                            SizeX = worldX, SizeY = 0.58f,
                            SizeZ = 0.08f, Shade = 0.95f,
                        });
                    break;
                }
                default:   // Gallery deck (§E2: 난간 포함)
                {
                    module.Pieces.Add(new Piece
                    {
                        Part = Part.Body, LocalY = -0.06f,
                        SizeX = worldX, SizeY = 0.12f, SizeZ = worldZ,
                        Shade = (float)(0.92 + Signed(seed, 4, 4) * 0.08),
                    });
                    module.Pieces.Add(new Piece   // railing on the arena-far edge
                    {
                        Part = Part.Body, LocalY = 0.09f, LocalZ = worldZ * 0.46f,
                        SizeX = worldX, SizeY = 0.18f, SizeZ = 0.06f, Shade = 1.1f,
                    });
                    module.Pieces.Add(new Piece   // skirt to the void floor
                    {
                        Part = Part.Body,
                        LocalY = (float)(-(h + 0.35) * 0.5 - 0.06),
                        SizeX = worldX * 0.9f,
                        SizeY = (float)(h + 0.35 - 0.12),
                        SizeZ = worldZ * 0.9f, Shade = 0.7f,
                    });
                    break;
                }
            }
            modules.Add(module);
        }

        static void AddOuterWallRing(List<Module> modules, uint stageSeed)
        {
            // ember-bastion 이중 링: 12 battlement segments at e 1.12, gate
            // arcs left open (요새 문법 — the fort has walls BEHIND the walls).
            var wallIndex = CountKind(modules, Kind.Wall);
            for (var i = 0; i < 12; i++)
            {
                var thetaDeg = i * 30.0 + 15.0;
                // Skip the two gate meridians (±25° of 90/270).
                var fromEntrance = Math.Abs(NormalizeDeg(thetaDeg - 90.0));
                var fromBoss = Math.Abs(NormalizeDeg(thetaDeg - 270.0));
                if (fromEntrance < 25.0 || fromBoss < 25.0) continue;

                var theta = thetaDeg * Math.PI / 180.0;
                EllipsePoint(1.12, theta, out var x, out var y);
                var seed = ModuleSeed(stageSeed, Kind.Wall, 300 + i);
                var tx = -HalfW * Math.Sin(theta);
                var ty = HalfH * Math.Cos(theta);
                var yaw = Math.Atan2(-ty, tx) * (-180.0 / Math.PI);

                var module = NewModule(Kind.Wall,
                    "env-wall-" + wallIndex.ToString("D3"), x, y, 0f, (float)yaw);
                module.Pieces.Add(new Piece
                {
                    Part = Part.Body, LocalY = 0.62f,
                    SizeX = 1.9f, SizeY = 1.24f, SizeZ = 0.3f,
                    Shade = (float)(0.9 + Signed(seed, 0, 4) * 0.08),
                });
                module.Pieces.Add(new Piece   // crenellation cap
                {
                    Part = Part.Body, LocalY = 1.31f,
                    SizeX = 1.98f, SizeY = 0.14f, SizeZ = 0.4f, Shade = 1.1f,
                });
                modules.Add(module);
                wallIndex++;
            }
        }

        static double NormalizeDeg(double degrees)
        {
            degrees %= 360.0;
            if (degrees > 180.0) degrees -= 360.0;
            if (degrees < -180.0) degrees += 360.0;
            return degrees;
        }

        // --------------------------------------------- torches + lights (§E6) --
        static void AddTorchesAndLights(
            List<Module> modules, uint stageSeed, HazardConfig[] hazards, GateInfo gates)
        {
            var torchE = StopE + 0.055;
            var positions = new List<(double x, double y)>(8);
            for (var i = 0; i < 8; i++)
            {
                var theta = (22.5 + i * 45.0) * Math.PI / 180.0;
                EllipsePoint(torchE, theta, out var x, out var y);
                if (NearAnyHazard(hazards, x, y, PostSkipMargin)) continue;
                positions.Add((x, y));
            }

            for (var i = 0; i < positions.Count; i++)
            {
                var seed = ModuleSeed(stageSeed, Kind.Torch, i);
                var module = NewModule(Kind.Torch, "env-torch-" + i.ToString("D3"),
                    positions[i].x, positions[i].y, 0f,
                    (float)(Signed(seed, 0, 5) * 15.0));
                module.Pieces.Add(new Piece   // post 32×h96 (§E2)
                {
                    Part = Part.Body, LocalY = 0.48f,
                    SizeX = 0.32f, SizeY = 0.96f, SizeZ = 0.32f, Shade = 0.9f,
                });
                module.Pieces.Add(new Piece   // ember head — light-free glow (§E6)
                {
                    Part = Part.Ember, LocalY = 1.04f,
                    SizeX = 0.2f, SizeY = 0.2f, SizeZ = 0.2f, Shade = 1f,
                });
                modules.Add(module);
            }

            // Exactly 4 realtime lights: gates 2 + seed-picked torch accents 2.
            modules.Add(LightModule("env-light-000", gates.EntranceX,
                gates.EntranceY + 40.0, 1.5f, 0));
            modules.Add(LightModule("env-light-001", gates.BossX,
                gates.BossY - 40.0, 1.5f, 1));

            double accentAx, accentAy, accentBx, accentBy;
            if (positions.Count >= 2)
            {
                var pickSeed = ModuleSeed(stageSeed, Kind.Light, 0);
                var first = (int)(pickSeed % (uint)positions.Count);
                var second = (int)((pickSeed >> 8) % (uint)(positions.Count - 1));
                if (second >= first) second++;
                (accentAx, accentAy) = positions[first];
                (accentBx, accentBy) = positions[second];
            }
            else
            {
                // Degenerate fallback (hazards ate the ring): flank the gates.
                (accentAx, accentAy) = (gates.EntranceX - 200.0, gates.EntranceY - 60.0);
                (accentBx, accentBy) = (gates.BossX + 200.0, gates.BossY + 60.0);
            }
            modules.Add(LightModule("env-light-002", accentAx, accentAy, 1.25f, 2));
            modules.Add(LightModule("env-light-003", accentBx, accentBy, 1.25f, 3));
        }

        static Module LightModule(string name, double x, double y, float h, int role)
        {
            var module = NewModule(Kind.Light, name, x, y, h, 0f);
            module.LightRole = role;
            return module;
        }

        // ------------------------------------------------------------ misc --
        static Module NewModule(
            Kind kind, string name, double x, double y, float h, float yaw)
            => new Module
            {
                Kind = kind,
                Name = name,
                SimX = (float)x,
                SimY = (float)y,
                HeightWorld = h,
                YawDeg = yaw,
                Pieces = new List<Piece>(6),
            };

        static string NextName(Dictionary<Kind, int> counters, Kind kind)
        {
            var index = counters[kind];
            counters[kind] = index + 1;
            var label = kind == Kind.Gallery ? "gallery"
                : kind == Kind.Bridge ? "bridge" : "channel";
            return "env-" + label + "-" + index.ToString("D3");
        }

        static int CountKind(List<Module> modules, Kind kind)
        {
            var count = 0;
            for (var i = 0; i < modules.Count; i++)
                if (modules[i].Kind == kind) count++;
            return count;
        }
    }
}
