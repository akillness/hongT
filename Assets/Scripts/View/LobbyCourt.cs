// The lobby's court, built from the kit that was already paid for.
//
// WHY THIS EXISTS. Measured 2026-08-13 (design/concept-gap-check-20260813.md): the
// lobby scene contains exactly TWO baked MeshRenderers, and both are Unlit,
// zero-thickness floor quads —
//
//   CourtBackdrop   Universal Render Pipeline/Unlit   19.2 x 0.0 x 12.8
//   VoidFloor       Universal Render Pipeline/Unlit   40.0 x 0.0 x 26.0
//
// so the lobby had no standing geometry at all and no surface that any light could
// touch. Its depth was a painted texture, with three characters standing in front of
// it. The worldview asks for a COURT — a memory prison whose every hazard is a court
// function made physical — and none of that was physically present.
//
// WHY THE KIT AND NOT NEW ART. Assets/Resources/Environment already ships 20 kit
// prefabs: round/broken/fallen columns, straight/corner/arch walls, a buttress, an
// altar plinth, a statue base, a sarcophagus, a great brazier, a candelabra, hanging
// chain, rail balusters, stair blocks, rubble. That IS a courthouse kit, and the
// dungeon uses it for four gimmick decorations — measured at 0.003% of the frame.
// The meshes and their recovered textures are already in the WebGL payload, so this
// costs no download and no generation pass; it spends something already bought.
//
// WHAT THIS DOES NOT DO. No sim constant, no save field, no catalog entry — this is
// AMENDMENT #8-shaped pure presentation, so the golden digests must not move. If they
// move, this file did something it had no business doing (CLAUDE.md §4e).
using System.Collections.Generic;
using UnityEngine;

namespace CinderCourt.View
{
    /// <summary>
    /// Composes the lobby's standing court geometry from the shipped kit prefabs.
    /// Placement is in SIM coordinates so it shares one frame of reference with
    /// LobbyStaging's actor spots and with everything else in this project.
    /// </summary>
    public static class LobbyCourt
    {
        /// <summary>
        /// One placement. Height is a TARGET in world units and the piece is scaled to
        /// meet it, because the kit's source meshes have no documented scale and
        /// hard-coding per-part multipliers would encode a fact nobody measured — the
        /// same trap as an anchor that references an unmeasured quantity (§4t).
        /// </summary>
        readonly struct Piece
        {
            public readonly string Part;
            public readonly float SimX, SimY, Yaw, TargetSize;
            /// <summary>
            /// A piece that LIES ON the floor rather than standing on it.
            ///
            /// Declared, not inferred from the part name. A floor tile is read by its
            /// FOOTPRINT and is correctly almost flat, so scaling it to a height and
            /// then judging it by that height asks the wrong question twice — the
            /// first version of the size test failed the sigil tile at 0.10 u for
            /// being exactly what a floor tile should be. Flat pieces are scaled and
            /// checked on their width instead.
            /// </summary>
            public readonly bool Flat;
            public Piece(string part, float simX, float simY, float yaw, float targetSize,
                         bool flat = false)
            {
                Part = part; SimX = simX; SimY = simY; Yaw = yaw;
                TargetSize = targetSize; Flat = flat;
            }
        }

        // Layout in sim coordinates, sized against the LOBBY camera — measured, not
        // assumed. CameraRig's Lobby profile is pitch 18 deg, offset (0, 2.6, -9.5)
        // from the arena centre, FOV 36 vertical, looking at centre + 1.1 up:
        //
        //     visible at the focal plane   6.17 u tall  x  9.88 u wide
        //     in sim units (Scale 0.015)    412         x   659
        //
        // The first draft sized everything against the DUNGEON camera (distance 21,
        // FOV 42 — 16 u of visible height) and shipped 4.4 u columns into a 6.17 u
        // frame. They filled 71% of the screen and hid every actor. Heights below are
        // fractions of the measured frame instead: a column at 32% reads as
        // architecture, and nothing exceeds a third of the picture.
        //
        // Three rules shaped the placement and each is a constraint, not a taste:
        //
        //   1. NOTHING between the camera and the actors. Warden (640, 700), companion
        //      (540, 640) and boss (940, 380) are what the screen is about, so the
        //      colonnade runs outside x 430..1110 and the bench sits behind y 340.
        //   2. NOTHING under the left rail. The lobby's navigation column owns the
        //      left edge; geometry there would sit behind buttons and read as clutter.
        //   3. The aisle points AT the boss. A colonnade framing the centre would
        //      frame nothing; framing the boss is what makes the room read as a court
        //      with a defendant at the far end.
        static readonly Piece[] Layout =
        {
            // ---- the bench ---------------------------------------------------
            // A court is a room with a SEAT at one end. This is what turns an
            // enclosure into a courtroom, so it is built first and everything else is
            // arranged to point at it: a raised dais, an arched back wall behind it,
            // and a statue either side standing for the authority that seats there.
            new Piece("kit-stair-block",    768f, 330f,   0f, 0.55f),
            new Piece("kit-altar-plinth",   768f, 300f,   0f, 0.62f),
            new Piece("kit-wall-arch",      768f, 262f,   0f, 1.98f),
            new Piece("kit-wall-straight",  600f, 270f,   0f, 1.60f),
            new Piece("kit-wall-straight",  936f, 270f,   0f, 1.60f),
            new Piece("kit-statue-base",    652f, 322f,   0f, 1.15f),
            new Piece("kit-statue-base",    884f, 322f,   0f, 1.15f),

            // ---- the room's corners ------------------------------------------
            // Corner pieces, not more straight wall. An enclosure whose corners are
            // open reads as a stage flat seen from the wings; closing them is what
            // makes the camera's ±6° orbit feel like it is inside something.
            new Piece("kit-wall-corner",    452f, 288f,   0f, 1.60f),
            new Piece("kit-wall-corner",   1084f, 288f, -90f, 1.60f),

            // ---- the aisle ---------------------------------------------------
            // Colonnade, three a side, receding toward the bench. Broken and fallen
            // columns are mixed in deliberately: a prison of memory that is perfectly
            // maintained reads as a lobby set, not as a place with a history.
            new Piece("kit-column-round",   430f, 430f,   0f, 1.98f),
            new Piece("kit-column-round",   430f, 640f,   0f, 1.98f),
            new Piece("kit-column-broken",  430f, 850f,   0f, 1.40f),
            new Piece("kit-column-round",  1106f, 430f,   0f, 1.98f),
            new Piece("kit-column-round",  1106f, 640f,   0f, 1.98f),
            new Piece("kit-column-fallen", 1106f, 850f,  15f, 0.60f),

            // The oath mark on the floor of the aisle. Every hazard in this game is a
            // court function made physical (worldview.md); the sigil is that idea at
            // rest, and it gives the empty centre of the room a reason to be empty.
            new Piece("kit-floor-tile-sigil", 768f, 560f, 0f, 1.60f, flat: true),

            // Candelabra flanking the aisle and braziers at the bench. These are the
            // only warm sources in a cold room, and they are placed where LobbyAccent
            // already sits so the one light this room owns has something to land on.
            new Piece("kit-candelabra",     600f, 470f,   0f, 0.95f),
            new Piece("kit-candelabra",     936f, 470f,   0f, 0.95f),
            new Piece("kit-brazier-great",  672f, 352f,   0f, 0.80f),
            new Piece("kit-brazier-great",  864f, 352f,   0f, 0.80f),

            // Hanging chain over the bench — the vocabulary is a memory PRISON, and
            // this is the one kit part that says so without a caption.
            new Piece("kit-chain-hanging",  704f, 300f,   0f, 1.30f),
            new Piece("kit-chain-hanging",  832f, 300f,   0f, 1.30f),

            // ---- outboard and near edge --------------------------------------
            // Buttresses at the frame edge, where the painted plate stops being
            // convincing; the gallery rail across the near edge, low enough to frame
            // without standing between the camera and the warden.
            new Piece("kit-buttress",       330f, 540f,  90f, 1.67f),
            new Piece("kit-buttress",      1206f, 540f, -90f, 1.67f),
            new Piece("kit-rail-baluster",  620f, 900f,   0f, 0.37f),
            new Piece("kit-rail-baluster",  768f, 900f,   0f, 0.37f),
            new Piece("kit-rail-baluster",  916f, 900f,   0f, 0.37f),

            // Debris. Rubble reads as age; a sarcophagus reads as what this court keeps.
            new Piece("kit-rubble-heap",    500f, 340f,   0f, 0.34f),
            new Piece("kit-rubble-heap",   1040f, 340f,   0f, 0.34f),
            new Piece("kit-sarcophagus",    480f, 800f,  20f, 0.45f),
        };

        static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        /// <summary>
        /// Builds the court under <paramref name="parent"/> and returns its root, or
        /// null when no kit prefab could be loaded at all. Idempotent by construction:
        /// the caller owns the root and destroying it removes everything this made.
        /// </summary>
        public static GameObject Build(Transform parent)
        {
            var root = new GameObject("LobbyCourt");
            root.transform.SetParent(parent, false);

            var placed = 0;
            for (var i = 0; i < Layout.Length; i++)
            {
                var piece = Layout[i];
                var prefab = Load(piece.Part);
                if (prefab == null) continue;   // a missing part must not take the room with it

                var instance = Object.Instantiate(prefab, root.transform);
                instance.name = $"court-{i:D2}-{piece.Part}";
                instance.transform.position = ViewWorld.ToWorld(piece.SimX, piece.SimY);
                instance.transform.rotation = Quaternion.Euler(0f, piece.Yaw, 0f);
                ScaleToSize(instance, piece.TargetSize, piece.Flat);
                StripColliders(instance);
                EnableShadowCasting(instance);
                placed += 1;
            }

            if (placed == 0)
            {
                Object.Destroy(root);
                return null;
            }
            return root;
        }

        static GameObject Load(string part)
        {
            if (Prefabs.TryGetValue(part, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>("Environment/" + part);
            Prefabs[part] = prefab;
            return prefab;
        }

        /// <summary>
        /// Scales a spawned piece so its rendered height matches the target.
        ///
        /// Reads the bounds rather than trusting a per-part constant: the kit meshes
        /// came out of an import pipeline whose scale is documented nowhere, and a
        /// table of magic multipliers would be a second, unverifiable source for a
        /// fact the mesh already knows (§4i).
        /// </summary>
        static void ScaleToSize(GameObject instance, float targetSize, bool flat)
        {
            // MESH bounds, not Renderer.bounds.
            //
            // The first version of this read Renderer.bounds immediately after
            // Instantiate and produced a lobby whose columns filled the entire frame
            // and hid every actor. Renderer.bounds is a world-space AABB maintained
            // by the rendering system; querying it in the same frame an object was
            // created returns whatever that system last computed, which for a fresh
            // instance is not its geometry. Mesh.bounds is local, authored, and
            // correct the moment the asset loads — so the size is read from the thing
            // that knows it instead of from a cache that has not run yet.
            //
            // Composing it by hand also keeps the two passes below honest: the same
            // local box drives both the scale and the re-seat, so they cannot disagree.
            if (!TryLocalBounds(instance, out var local)) return;

            // Standing pieces are sized by height, floor pieces by width. Choosing the
            // axis from the piece's declared role rather than from whichever extent
            // happens to be largest keeps a wide, low bench from being treated as a
            // floor and a tall narrow tile from being treated as a wall.
            var extent = flat ? local.size.x : local.size.y;
            var current = flat
                ? extent * instance.transform.localScale.x
                : extent * instance.transform.localScale.y;
            if (current <= 0.0001f) return;   // degenerate: leave the authored scale

            var factor = targetSize / current;
            instance.transform.localScale *= factor;

            // Re-seat on the floor AFTER scaling. Scaling happens about the transform
            // origin, which for these imported meshes is not reliably the base, so a
            // piece standing on y=0 before the scale is floating or sunk after it.
            // Measuring again is cheaper than assuming a pivot convention the import
            // pipeline never promised.
            var baseOffset = local.min.y * instance.transform.localScale.y;
            instance.transform.position -= new Vector3(0f, baseOffset, 0f);
        }

        /// <summary>
        /// Union of every child mesh's local bounds, expressed in the instance's own
        /// space. Returns false when the prefab has no mesh at all — a case that must
        /// leave the piece untouched rather than divide by zero.
        /// </summary>
        static bool TryLocalBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            var filters = instance.GetComponentsInChildren<MeshFilter>(true);
            var found = false;
            for (var i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null) continue;
                // Child transforms are part of the prefab's authored composition, so
                // a child's local box has to be lifted into the ROOT's space before
                // it can be unioned with its siblings'.
                var childBounds = mesh.bounds;
                var toRoot = instance.transform.worldToLocalMatrix * filters[i].transform.localToWorldMatrix;
                var centre = toRoot.MultiplyPoint3x4(childBounds.center);
                var extents = toRoot.MultiplyVector(childBounds.extents);
                var box = new Bounds(centre, new Vector3(
                    Mathf.Abs(extents.x) * 2f, Mathf.Abs(extents.y) * 2f, Mathf.Abs(extents.z) * 2f));
                if (!found) { bounds = box; found = true; }
                else bounds.Encapsulate(box);
            }
            return found;
        }

        /// <summary>Test seam: the world-space height a placement resolves to, so a
        /// test can assert the scaling actually landed instead of trusting it. The
        /// first draft's columns were off by more than an order of magnitude and the
        /// only thing that noticed was a browser capture.</summary>
        public static bool TryMeasureHeightForTest(GameObject instance, out float height)
        {
            height = 0f;
            if (!TryLocalBounds(instance, out var local)) return false;
            height = local.size.y * instance.transform.localScale.y;
            return true;
        }

        /// <summary>Test seam: the declared role of the i-th placement, so the size
        /// test can judge a floor tile on its footprint and a column on its height
        /// instead of applying one band to both.</summary>
        public static bool IsFlatPlacementForTest(int index)
            => index >= 0 && index < Layout.Length && Layout[index].Flat;

        /// <summary>
        /// Same guard LobbyStaging and VfxDirector use: Destroy is a no-op outside
        /// play mode, so an unguarded strip leaves live colliders on decoration and
        /// logs an editor error. The court is scenery — it must never take a raycast
        /// or a physics query.
        /// </summary>
        static void StripColliders(GameObject instance)
        {
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying) Object.Destroy(colliders[i]);
                else Object.DestroyImmediate(colliders[i]);
            }
        }

        /// <summary>
        /// The lobby's diorama contract: every standing mesh casts into the key light.
        ///
        /// Not a preference — the lobby lighting test asserts it, and it caught this
        /// file twice. The kit prefabs ship with casting OFF because the DUNGEON
        /// spawns them as gimmick decoration, where VfxDirector decides per-part
        /// whether a piece earns a shadow. The lobby's rule is the opposite and
        /// simpler: one lit room, one key, and a column that throws no shadow in it
        /// reads as a sticker rather than as stone.
        ///
        /// ROUTED THROUGH StageShadowPolicy, not done by hand. The first attempt set
        /// shadowCastingMode directly and still failed, because casting is only half
        /// of it: the key light filters by renderingLayerMask, so a caster that has
        /// not joined that layer is configured and invisible to the light at the same
        /// time. TryConfigureCaster owns both halves and is what ActorView already
        /// uses — one place decides what a caster is (CLAUDE.md §4e: call the rule,
        /// do not restate it).
        /// </summary>
        static void EnableShadowCasting(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
                StageShadowPolicy.TryConfigureCaster(renderers[i]);
        }

        /// <summary>Test seam: how many placements the table declares. A build that
        /// silently places nothing would otherwise look like a build that placed
        /// everything.</summary>
        public static int PlacementCountForTest => Layout.Length;
    }
}
