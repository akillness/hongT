// Act-boundary cinematics: the catalog is nine stages in three acts, and the
// player is promised a beat when an act closes.
//
// What can go wrong here is silent in every direction. The reel plays from
// EnterLobby, which is also where death and abandon land, so a mis-latched
// beat plays over a defeat. The mapping is derived from CatalogIndex, so a
// tenth stage appended to the catalog moves every boundary. And a missing clip
// finishes immediately by design, so nothing at runtime reports it.
//
// This file asks the three questions a player would notice:
//   * does an act END on the stage the fiction says it does,
//   * does a NON-boundary clear stay quiet,
//   * is the mapping still three-of-three if the catalog grows.
using System.Linq;
using System.Reflection;
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ActCinematicTests
    {
        /// <summary>GameDirector.ActBeatFor, which is private on purpose — the
        /// mapping is an implementation detail of the clear route, not API.</summary>
        static (string reel, string narration)? ActBeat(string stageId)
        {
            var method = typeof(GameDirector).GetMethod("ActBeatFor",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null,
                "GameDirector.ActBeatFor is what decides where an act ends");
            var result = method.Invoke(null, new object[] { stageId });
            if (result == null) return null;
            var t = result.GetType();
            // Nullable<ValueTuple<string,string>> unwraps to the tuple itself.
            var value = t.GetProperty("Value")?.GetValue(result) ?? result;
            var vt = value.GetType();
            return ((string)vt.GetField("Item1").GetValue(value),
                    (string)vt.GetField("Item2").GetValue(value));
        }

        [Test]
        public void ActsEndOnTheThirdSixthAndNinthStage()
        {
            var entries = StageCatalog.Entries;
            Assert.That(entries.Count, Is.EqualTo(9),
                "three acts of three — a different count means the boundaries "
                + "below are describing a catalog that no longer exists");

            foreach (var entry in entries)
            {
                var beat = ActBeat(entry.Id);
                var isBoundary = (entry.CatalogIndex + 1) % 3 == 0;
                if (isBoundary)
                {
                    Assert.That(beat, Is.Not.Null,
                        $"{entry.Id} (index {entry.CatalogIndex}) closes an act "
                        + "and must carry a cinematic");
                    Assert.That(beat.Value.reel, Is.Not.Null.And.Not.Empty);
                    Assert.That(beat.Value.narration, Is.Not.Null.And.Not.Empty,
                        $"{entry.Id}: an act beat with no line is a video with "
                        + "nothing to say");
                }
                else
                {
                    Assert.That(beat, Is.Null,
                        $"{entry.Id} (index {entry.CatalogIndex}) is mid-act — a "
                        + "cinematic here fires after an ordinary clear");
                }
            }
        }

        [Test]
        public void EachActUsesItsOwnReel()
        {
            var reels = StageCatalog.Entries
                .Select(e => ActBeat(e.Id))
                .Where(b => b != null)
                .Select(b => b.Value.reel)
                .ToArray();
            Assert.That(reels.Length, Is.EqualTo(3), "three act boundaries");
            Assert.That(reels, Is.Unique,
                "three acts closing on the same footage is one act shown thrice");
            foreach (var reel in reels)
                Assert.That(reel, Does.StartWith("Video/"),
                    "act reels stream from StreamingAssets like every other clip");
        }

        [Test]
        public void UnknownStageHasNoActBeat()
        {
            Assert.That(ActBeat("no-such-stage"), Is.Null);
            Assert.That(ActBeat(string.Empty), Is.Null);
        }
    }
}
