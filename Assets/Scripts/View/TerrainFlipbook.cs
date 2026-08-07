// W16 — 지형 애니메이션: UV flipbook decals on the ambient floor panels.
//
// THE PROBLEM. The dungeon floor reads as "평면 오브젝트" because that is
// literally what it is: EnvironmentBuilder lays 6..10 ambient floor panels as
// flat quads with a tiling masonry map and nothing on them ever moves. Lights
// flicker, hazards pulse, actors walk — the ground itself is inert.
//
// THE FIX, AND ITS LIMITS. A sprite-sheet flipbook driven through _BaseMap_ST
// gives the ground motion for one texture fetch and zero new geometry. It is
// decoration only: no sim state is read, no judgement is drawn, and every
// surface it touches is already proven hazard-clear (see §Placement below).
//
// WHY THIS FILE OWNS ITS OWN ROOT. StageMood documents the same reasoning:
// "StageEnvironment" is budget-gated by
// EnvironmentBuilderTests.Budget_VerticesMaterialsAndLights (§E7 materials,
// §E6 lights) and its child names are vocabulary-gated. Parenting decals under
// it would break both gates for a purely decorative layer, so the decals live
// under a SEPARATE root and the gates stay honest.
//
// WHY IT SELF-ATTACHES. GameDirector / SceneBuilder / EnvironmentBuilder are
// off-limits to this lane (two are being edited concurrently), so the driver
// boots itself and discovers the stage root by polling. That also means this
// whole file can be deleted without touching another line of code.
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class TerrainFlipbook : MonoBehaviour
    {
        /// <summary>Which sheet family a stage gets. None = nothing to play.</summary>
        public enum Theme { None = 0, Lava = 1, Ice = 2, Shift = 3 }

        /// <summary>Root this component creates for its decals.</summary>
        public const string RootName = "StageTerrainFx";

        /// <summary>
        /// EnvironmentBuilder's stage root (it hardcodes the literal at its
        /// Build site). Mirrored here rather than referenced because that file
        /// is owned by another lane this cycle.
        /// </summary>
        public const string EnvironmentRootName = "StageEnvironment";

        /// <summary>Ambient floor pivots are "env-floor-NNN"; the hazard-ring
        /// FURNITURE pivots share that prefix, so the prefix alone is not the
        /// discriminator — see <see cref="PanelOf"/>.</summary>
        const string FloorPivotPrefix = "env-floor-";

        /// <summary>An ambient panel's mesh child. Library furniture children are
        /// named "part-NN" instead — that difference is what separates a flat
        /// ground slab from a rock silhouette without needing hazard positions.</summary>
        const string PanelChildPrefix = "piece-";

        // ---- sheet contract (asset lane: terrain-fx-{lava,ice,shift}-sheet) ---
        // [TARGET] Uniform grid, row-major, top-left first frame. 4x4 = 16 frames
        // at 12 fps is a 1.333 s loop — long enough not to read as a strobe,
        // short enough that a 1024 sheet holds 256 px cells.
        public const int GridCols = 4;
        public const int GridRows = 4;
        public const int FrameCount = GridCols * GridRows;
        public const float FramesPerSecond = 12f;

        /// <summary>Decal cap. The layout emits 6..10 ambient panels, so this
        /// binds the worst case without ever leaving the stage bare.</summary>
        public const int MaxDecals = 8;

        /// <summary>Lift above the panel, world units. Large enough to beat
        /// z-fighting at the 55° dungeon pitch, small enough that the decal
        /// still reads as ground rather than a hovering card.</summary>
        const float DecalLift = 0.006f;

        /// <summary>
        /// Warmth band that counts as "neither". The floor tint is
        /// Lerp(floorBase, accent, 0.30) with floorBase carrying a 0.025 cool
        /// bias, so (r − b) recovers 0.30 × the accent's own warmth − 0.025.
        /// Across the shipped nine stages that lands 5 warm / 3 cold and leaves
        /// ash-march (grey #B8B0A4) at −0.001, i.e. genuinely neutral.
        /// </summary>
        internal const float NeutralBand = 0.05f;

        /// <summary>How often discovery re-scans while no stage is up.</summary>
        const float DiscoveryInterval = 0.5f;

        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        struct Decal
        {
            public Renderer Renderer;
            public float Phase;    // 0..1 of a loop, so panels never flash in unison
            public int Frame;      // last written frame; -1 forces the first write
        }

        Decal[] _decals;
        static Mesh _quadMesh;
        MaterialPropertyBlock _block;
        Material _material;
        GameObject _environment;
        GameObject _decalRoot;
        float _discoveryTimer;
        // A theme whose sheet is absent is marked dead so discovery stops paying
        // for it. All three dead => the component switches itself off entirely.
        readonly bool[] _deadThemes = new bool[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var host = new GameObject("TerrainFlipbookDriver");
            host.AddComponent<TerrainFlipbook>();
            DontDestroyOnLoad(host);
        }

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            // Lobby boots before any stage exists; discover on the first tick.
            _discoveryTimer = 0f;
        }

        void Update()
        {
            if (_environment == null)
            {
                if (_decalRoot != null) TearDown();
                _discoveryTimer -= Time.unscaledDeltaTime;
                if (_discoveryTimer > 0f) return;
                _discoveryTimer = DiscoveryInterval;
                var found = GameObject.Find(EnvironmentRootName);
                if (found == null) return;
                _environment = found;
                Build(found.transform);
                if (_decals == null) return;
            }
            Animate();
        }

        void OnDestroy() => TearDown();

        /// <summary>
        /// Drops the decal root and its one material. The material is a runtime
        /// clone (seed path), so nothing here touches a serialized asset.
        /// </summary>
        void TearDown()
        {
            if (_decalRoot != null)
            {
                if (Application.isPlaying) Destroy(_decalRoot);
                else DestroyImmediate(_decalRoot);
                _decalRoot = null;
            }
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
                _material = null;
            }
            _decals = null;
            _environment = null;
            // A stage SWAP destroys one root and builds the next in the same
            // frame. Re-arming discovery here means the new stage is decorated
            // on the next tick instead of after a visible half-second of bare
            // floor.
            _discoveryTimer = 0f;
        }

        // ------------------------------------------------------------ build ---

        /// <summary>
        /// The ambient floor panel's renderer under a stage-root child, or null
        /// when that child is not an ambient panel.
        ///
        /// This is the whole placement rule, and it is deliberately structural
        /// rather than positional. EnvironmentBuilder gives BOTH the ambient
        /// ground slabs and the hazard-ring furniture the "env-floor-NNN" name,
        /// but only the slabs get a code quad ("piece-NN"); furniture gets a
        /// cloned library mesh ("part-NN"). Keying off the CHILD kind therefore
        /// selects exactly the flat ground and never the rock dressing that
        /// rings a vent — without this file needing to know where any hazard is.
        ///
        /// The safety property that matters falls out of the same fact: the
        /// ambient panels are placed through a NearAnyHazard(FloorSkipMargin)
        /// filter, so every surface this animates is already proven clear of
        /// every telegraph disc. That is what keeps an ember-coloured lava
        /// flipbook from camouflaging a vent warning, which is the failure mode
        /// EnvironmentBuilder's own tint comments call out as worse than
        /// invisibility.
        /// </summary>
        internal static Renderer PanelOf(Transform child)
        {
            if (!child.name.StartsWith(FloorPivotPrefix, System.StringComparison.Ordinal))
                return null;
            for (var i = 0; i < child.childCount; i++)
            {
                var piece = child.GetChild(i);
                if (!piece.name.StartsWith(PanelChildPrefix, System.StringComparison.Ordinal))
                    continue;
                var renderer = piece.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null) return renderer;
            }
            return null;
        }

        /// <summary>
        /// The panel's stage tint, or false when the panel does not carry one.
        ///
        /// EnvironmentBuilder keeps ONE shared floor material and creates it as
        /// <c>MakeLit(Color.white, null)</c>, carrying the per-stage colour in a
        /// per-renderer MaterialPropertyBlock instead (§E7 caps the material
        /// budget). So <c>sharedMaterial.color</c> is WHITE on every stage —
        /// reading it would collapse all nine stages onto one theme. The block
        /// is where the stage identity actually lives.
        ///
        /// <paramref name="scratch"/> is a caller-owned block, and it must not
        /// be the one used for animation: GetPropertyBlock OVERWRITES it with
        /// the panel's own _BaseColor and _BaseMap_ST, which would then ride
        /// along onto every decal the animation pass touches.
        /// </summary>
        internal static bool TryFloorTint(
            Renderer renderer, MaterialPropertyBlock scratch, out Color tint)
        {
            tint = default;
            if (renderer == null || !renderer.HasPropertyBlock()) return false;
            renderer.GetPropertyBlock(scratch);
            tint = scratch.GetColor(BaseColorId);
            // An unset property reads back as (0,0,0,0); a real stage tint never
            // does, because every branch writes an opaque colour.
            return tint.a > 0f;
        }

        /// <summary>
        /// Sheet family from the panel's own stage tint. Deciding from the
        /// surface being decorated keeps this self-contained — no stage id, no
        /// StageCatalog lookup, no coupling to whoever built the stage.
        /// </summary>
        internal static Theme ThemeForFloorTint(Color floor)
        {
            var warmth = floor.r - floor.b;
            if (warmth > NeutralBand) return Theme.Lava;
            if (warmth < -NeutralBand) return Theme.Ice;
            return Theme.Shift;
        }

        /// <summary>Resources path of a theme's sheet, or null for None.</summary>
        internal static string SheetPath(Theme theme) => theme switch
        {
            Theme.Lava => "Terrain/terrain-fx-lava-sheet",
            Theme.Ice => "Terrain/terrain-fx-ice-sheet",
            Theme.Shift => "Terrain/terrain-fx-shift-sheet",
            _ => null,
        };

        /// <summary>
        /// Decal colour [TARGET]. The SHEET carries luminance/alpha only; the
        /// stage's own floor tint carries identity. That split is what lets
        /// three sheets serve nine stages without any of them reading as a
        /// foreign palette — abyss-chancel's violet "ice" is crystalline drift
        /// in the stage's colour, not a literal glacier.
        ///
        /// Alpha is held low on purpose. This is ambient ground texture, and the
        /// moment it competes for attention with a telegraph it has stopped
        /// being decoration.
        /// </summary>
        internal static Color DecalTint(Color floor, Theme theme) => theme switch
        {
            // Magma seams: warm channel lifted hard, cool channel pulled down, so
            // the read is "glowing cracks in dark rock" rather than a bright disc.
            Theme.Lava => new Color(
                Mathf.Clamp01(floor.r * 1.9f + 0.20f),
                Mathf.Clamp01(floor.g * 1.05f + 0.04f),
                Mathf.Clamp01(floor.b * 0.65f),
                0.32f),
            // Frost sheen: the mirror operation on the cool channel.
            Theme.Ice => new Color(
                Mathf.Clamp01(floor.r * 1.05f + 0.05f),
                Mathf.Clamp01(floor.g * 1.45f + 0.10f),
                Mathf.Clamp01(floor.b * 1.9f + 0.18f),
                0.30f),
            // Drifting ash: no channel wins. Lower alpha still — this one is not
            // an emissive phenomenon and it renders on the alpha path, so the
            // same number reads heavier than it does above.
            _ => new Color(
                Mathf.Clamp01(floor.r * 1.35f + 0.06f),
                Mathf.Clamp01(floor.g * 1.35f + 0.06f),
                Mathf.Clamp01(floor.b * 1.30f + 0.06f),
                0.22f),
        };

        void Build(Transform environment)
        {
            var count = 0;
            Decal[] decals = null;
            // Read-only scratch, deliberately NOT _block — see TryFloorTint.
            // One allocation per stage entry, never per frame.
            var scratch = new MaterialPropertyBlock();

            for (var i = 0; i < environment.childCount && count < MaxDecals; i++)
            {
                var panel = PanelOf(environment.GetChild(i));
                if (panel == null) continue;
                if (!TryFloorTint(panel, scratch, out var floorTint)) continue;

                if (_material == null)
                {
                    // First panel decides the theme for the whole stage: every
                    // ambient panel carries the same stage tint, so a per-panel
                    // decision would be the same answer computed N times.
                    var theme = ThemeForFloorTint(floorTint);
                    if (_deadThemes[(int)theme]) return;
                    _material = MakeSheetMaterial(theme, floorTint);
                    if (_material == null)
                    {
                        // Sheet not delivered yet — complete no-op, and the theme
                        // is retired so discovery stops re-probing it.
                        _deadThemes[(int)theme] = true;
                        if (_deadThemes[1] && _deadThemes[2] && _deadThemes[3])
                            enabled = false;
                        return;
                    }
                    _decalRoot = new GameObject(RootName);
                    _decalRoot.transform.SetParent(transform, false);
                    decals = new Decal[MaxDecals];
                }

                decals[count] = SpawnDecal(panel.transform, count);
                count++;
            }

            if (count == 0) { _decals = null; return; }
            if (count < MaxDecals) System.Array.Resize(ref decals, count);
            _decals = decals;
        }

        /// <summary>
        /// The decal quad mesh: a unit XZ plane with 0..1 UVs, matching
        /// EnvironmentBuilder's "env-quad" vertex-for-vertex (up-facing winding
        /// included) so a decal lands exactly on the panel it covers.
        ///
        /// It is AUTHORED rather than borrowed from the panel's own MeshFilter,
        /// and that is not a style choice. EnvironmentBuilder ends Build with
        /// StaticBatchingUtility.Combine, which REPLACES every filter's
        /// sharedMesh with the one combined mesh and moves the per-renderer
        /// sub-mesh range into the renderer. Copying that mesh onto a fresh
        /// renderer would draw the entire batched stage on top of one floor tile.
        /// </summary>
        static Mesh QuadMesh()
        {
            if (_quadMesh != null) return _quadMesh;
            _quadMesh = new Mesh { name = "terrain-fx-quad" };
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
            _quadMesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };   // up-facing
            _quadMesh.RecalculateBounds();
            return _quadMesh;
        }

        /// <summary>One decal quad riding a panel's world placement.</summary>
        Decal SpawnDecal(Transform panel, int index)
        {
            var host = new GameObject("terrain-fx-" + index.ToString("D2"));
            host.transform.SetParent(_decalRoot.transform, false);
            // The decal root is unscaled and unrotated, so local == world here.
            host.transform.SetPositionAndRotation(
                panel.position + Vector3.up * DecalLift, panel.rotation);
            host.transform.localScale = panel.lossyScale;
            host.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var renderer = host.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new Decal
            {
                Renderer = renderer,
                // Deterministic per-panel offset from its own placement, so the
                // set never pulses as one organism and never needs a random seed.
                Phase = Mathf.Repeat(
                    panel.position.x * 0.37f + panel.position.z * 0.61f, 1f),
                Frame = -1,
            };
        }

        /// <summary>
        /// Seed-clone material for a theme, or null when the sheet is absent.
        /// Lava/Ice are emissive phenomena and take the additive path so they
        /// accumulate past the bloom threshold; drifting ash is not, so Shift
        /// takes straight alpha. Both are ViewWorld seed clones — the WebGL
        /// variant-stripping contract forbids constructing either directly.
        /// </summary>
        Material MakeSheetMaterial(Theme theme, Color floorTint)
        {
            var path = SheetPath(theme);
            if (path == null) return null;
            var sheet = Resources.Load<Texture2D>(path);
            if (sheet == null) return null;
            var tint = DecalTint(floorTint, theme);
            var material = theme == Theme.Shift
                ? ViewWorld.MakeUnlit(tint, true)
                : ViewWorld.MakeAdditive(tint);
            material.SetTexture("_BaseMap", sheet);
            material.mainTexture = sheet;
            return material;
        }

        // ---------------------------------------------------------- animate ---

        /// <summary>
        /// Advances every decal's frame window. Zero allocation per frame: one
        /// reused MaterialPropertyBlock, a stack Vector4, and a frame-equality
        /// early-out so a 60 Hz Update only issues 12 SetPropertyBlock calls per
        /// decal per second instead of 60.
        /// </summary>
        void Animate()
        {
            if (_decals == null) return;
            // Reduced motion halves the cadence rather than freezing the frame:
            // this is ambient ground texture with no information content, so the
            // accessibility goal is LESS motion, and a dead-still stage loses the
            // whole point of the layer. The brief permits either; half rate is
            // the one that keeps the stage alive.
            var fps = ViewPrefs.ReducedMotion ? FramesPerSecond * 0.5f : FramesPerSecond;
            var t = Time.time * fps;
            for (var i = 0; i < _decals.Length; i++)
            {
                ref var decal = ref _decals[i];
                if (decal.Renderer == null) continue;
                var frame = Mathf.FloorToInt(t + decal.Phase * FrameCount) % FrameCount;
                if (frame < 0) frame += FrameCount;
                if (frame == decal.Frame) continue;
                decal.Frame = frame;
                _block.SetVector(BaseMapStId, FrameSt(frame));
                decal.Renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// _BaseMap_ST for a frame index: xy = tiling, zw = offset. Row 0 is the
        /// TOP row of the sheet, which is why v is measured down from 1 — UV
        /// origin is bottom-left and sheets are authored top-left first.
        /// </summary>
        internal static Vector4 FrameSt(int frame)
        {
            var col = frame % GridCols;
            var row = frame / GridCols;
            return new Vector4(
                1f / GridCols,
                1f / GridRows,
                col / (float)GridCols,
                1f - (row + 1f) / GridRows);
        }
    }
}
