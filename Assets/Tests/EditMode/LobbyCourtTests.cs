// The lobby court has to be the size it says it is.
//
// WHY THIS TEST EXISTS, SPECIFICALLY. The first version of LobbyCourt read
// Renderer.bounds immediately after Instantiate — a world-space AABB maintained by
// the rendering system, which in the frame an object is created has not been computed
// from its geometry yet. Every piece came out more than an order of magnitude too
// large: the shipped lobby was a wall of stone with the warden, the companion and the
// boss all hidden behind it.
//
// Nothing in EditMode caught that, because nothing asked how big the pieces were.
// Compiling proved the code ran; a browser capture is what showed it was wrong. This
// file closes that gap — it asks the one question the defect could not survive.
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class LobbyCourtTests
    {
        /// <summary>Visible world height at the lobby camera's focal plane:
        /// 2 * 9.5 * tan(36/2 deg). Written as the arithmetic rather than as 6.17 so a
        /// change to CameraRig's Lobby distance or FOV shows up here as a wrong number
        /// instead of as a stale constant nobody re-derived.</summary>
        const float LobbyFrameHeight = 2f * 9.5f * 0.32492f;

        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void EveryPiece_ResolvesToItsDeclaredHeight()
        {
            var host = new GameObject("LobbyCourtTests");
            _root = host;
            var court = LobbyCourt.Build(host.transform);

            Assert.That(court, Is.Not.Null,
                "no kit prefab loaded from Resources/Environment — the lobby would be "
                + "empty and this test would otherwise pass by measuring nothing");

            var pieces = court.transform.childCount;
            Assert.That(pieces, Is.GreaterThan(0), "court built with zero pieces");
            // Not an exact equality: a missing prefab is survivable by design (Build
            // skips it rather than dropping the room), so this asserts the layout is
            // MOSTLY present. A build that placed one piece out of twenty-five would
            // still look like a court in a screenshot thumbnail; it would not be one.
            Assert.That(pieces, Is.GreaterThanOrEqualTo(LobbyCourt.PlacementCountForTest - 2),
                $"{pieces} of {LobbyCourt.PlacementCountForTest} placements survived — "
                + "kit prefabs are missing from Resources/Environment");

            var report = new System.Text.StringBuilder();
            var measured = 0;
            var offenders = new System.Collections.Generic.List<string>();
            foreach (Transform child in court.transform)
            {
                if (!LobbyCourt.TryMeasureHeightForTest(child.gameObject, out var height))
                    continue;
                measured += 1;
                report.AppendLine($"  {child.name,-34} {height,6:F2} u");
                // MEASURED bound, not a guess. CameraRig's Lobby profile (pitch 18,
                // distance 9.5, FOV 36 vertical) frames 6.17 world units of height at
                // the focal plane. A piece taller than a third of that is a wall
                // across the picture — the first draft's 4.4 u columns were 71% of the
                // frame and hid all three actors. Under 0.15 u nothing is visible at
                // all. Both ends of this band come from the camera, so the assertion
                // moves if and only if the framing does.
                if (height > LobbyFrameHeight / 3f || height < 0.15f)
                    offenders.Add($"{child.name} = {height:F2} u");
            }

            TestContext.WriteLine($"[lobby court: {pieces} pieces, {measured} measured]\n" + report);
            Assert.That(measured, Is.EqualTo(pieces),
                "some pieces carry no mesh, so their size was never checked");
            Assert.That(offenders, Is.Empty,
                "pieces are not architecture-sized at the lobby camera:\n  "
                + string.Join("\n  ", offenders));
        }

        /// <summary>
        /// The court is scenery. A collider on it would take raycasts the lobby's UI
        /// and the actor picking depend on — the same guard LobbyStaging and
        /// VfxDirector already carry, asserted here because "we remembered to strip
        /// them" is exactly the kind of unanimous decision that ends up undefended
        /// (CLAUDE.md §4q).
        /// </summary>
        [Test]
        public void Court_CarriesNoColliders()
        {
            var host = new GameObject("LobbyCourtTests");
            _root = host;
            var court = LobbyCourt.Build(host.transform);
            Assert.That(court, Is.Not.Null);

            var colliders = court.GetComponentsInChildren<Collider>(true);
            Assert.That(colliders.Length, Is.Zero,
                $"{colliders.Length} collider(s) survived on lobby decoration");
        }
    }
}
