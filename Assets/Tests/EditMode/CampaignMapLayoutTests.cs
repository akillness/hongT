// Campaign minimap model (W8) + the meta screen's grade ladder (W7).
//
// The map is a REVEAL surface: its whole job is to say exactly as much as the
// save has earned and not one word more. That makes the interesting assertions
// negative ones — a locked node must not leak its name, an entry must not light
// a road nobody has walked — so they are pinned here rather than eyeballed in a
// screenshot.
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CampaignMapLayoutTests
    {
        static CampaignData Save(bool prologueDone, params int[] clearedIndices)
        {
            var data = new CampaignData { PrologueDone = prologueDone };
            for (var i = 0; i < clearedIndices.Length; i++)
                data.ClearedMask |= 1 << clearedIndices[i];
            return data;
        }

        [Test]
        public void BeforeThePrologue_EveryNodeIsLockedAndUnnamed()
        {
            var nodes = CampaignMapLayout.BuildNodes(Save(prologueDone: false));
            Assert.That(nodes.Length, Is.EqualTo(StageCatalog.Entries.Count));
            foreach (var node in nodes)
            {
                Assert.That(node.State, Is.EqualTo(CampaignNodeState.Locked));
                Assert.That(node.Label, Is.EqualTo(CampaignMapLayout.HiddenLabel),
                    $"{node.Id} leaked its name before the prologue");
                Assert.That(node.Epithet, Is.Empty);
            }
        }

        [Test]
        public void PrologueOpensExactlyTheFirstStage()
        {
            var nodes = CampaignMapLayout.BuildNodes(Save(prologueDone: true));
            Assert.That(nodes[0].State, Is.EqualTo(CampaignNodeState.Unlocked));
            Assert.That(nodes[0].Label, Is.EqualTo(StageCatalog.Entries[0].Title));
            // An unlocked-but-unfinished node names itself and nothing else: the
            // epithet is the reward for clearing it.
            Assert.That(nodes[0].Epithet, Is.Empty);
            for (var i = 1; i < nodes.Length; i++)
                Assert.That(nodes[i].State, Is.EqualTo(CampaignNodeState.Locked), nodes[i].Id);
        }

        [Test]
        public void ClearingAStage_LightsItAndOpensItsSuccessor()
        {
            var nodes = CampaignMapLayout.BuildNodes(Save(prologueDone: true, 0));
            Assert.That(nodes[0].State, Is.EqualTo(CampaignNodeState.Cleared));
            Assert.That(nodes[0].Epithet, Is.EqualTo(StageCatalog.Entries[0].Epithet));
            Assert.That(nodes[1].State, Is.EqualTo(CampaignNodeState.Unlocked));
            Assert.That(nodes[2].State, Is.EqualTo(CampaignNodeState.Locked));
        }

        [Test]
        public void LinksLightOnlyBehindAClearedPrerequisite()
        {
            var dark = CampaignMapLayout.BuildLinks(Save(prologueDone: true));
            Assert.That(dark.Length, Is.EqualTo(StageCatalog.Entries.Count - 1),
                "the current catalog is one chain: every stage but the first has a prereq");
            foreach (var link in dark) Assert.That(link.Lit, Is.False);

            var lit = CampaignMapLayout.BuildLinks(Save(prologueDone: true, 0));
            Assert.That(lit[0].FromIndex, Is.EqualTo(0));
            Assert.That(lit[0].ToIndex, Is.EqualTo(1));
            Assert.That(lit[0].Lit, Is.True);
            for (var i = 1; i < lit.Length; i++)
                Assert.That(lit[i].Lit, Is.False, $"link {i} lit without its prereq cleared");
        }

        [Test]
        public void EveryLinkResolvesToARealNodePair()
        {
            var links = CampaignMapLayout.BuildLinks(Save(prologueDone: true));
            var count = StageCatalog.Entries.Count;
            foreach (var link in links)
            {
                Assert.That(link.FromIndex, Is.InRange(0, count - 1));
                Assert.That(link.ToIndex, Is.InRange(0, count - 1));
                Assert.That(link.FromIndex, Is.Not.EqualTo(link.ToIndex));
            }
        }

        [Test]
        public void NodeCoordinatesStayInsideTheViewportAndApart()
        {
            var entries = StageCatalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.That(entries[i].NodeX, Is.InRange(0f, 1f), entries[i].Id);
                Assert.That(entries[i].NodeY, Is.InRange(0f, 1f), entries[i].Id);
            }
            // Hand-placed coordinates: without this gate a careless edit is
            // invisible until two labels overlap in the shipped build.
            for (var a = 0; a < entries.Count; a++)
            {
                for (var b = a + 1; b < entries.Count; b++)
                {
                    var dx = Mathf.Abs(entries[a].NodeX - entries[b].NodeX);
                    var dy = Mathf.Abs(entries[a].NodeY - entries[b].NodeY);
                    Assert.That(Mathf.Max(dx, dy),
                        Is.GreaterThanOrEqualTo(CampaignMapLayout.MinSeparation),
                        $"{entries[a].Id} and {entries[b].Id} collide on the map");
                }
            }
        }

        [Test]
        public void AlphaLadderIsStrictlyDescending()
        {
            // The three-step reveal IS the design ("밝음 / 반명 / 어둡게"), and a
            // locked node is dim, never invisible — hiding it would hide that
            // the campaign continues at all.
            Assert.That(CampaignMapLayout.AlphaOf(CampaignNodeState.Cleared),
                Is.GreaterThan(CampaignMapLayout.AlphaOf(CampaignNodeState.Unlocked)));
            Assert.That(CampaignMapLayout.AlphaOf(CampaignNodeState.Unlocked),
                Is.GreaterThan(CampaignMapLayout.AlphaOf(CampaignNodeState.Locked)));
            Assert.That(CampaignMapLayout.AlphaOf(CampaignNodeState.Locked), Is.GreaterThan(0f));
        }

        [Test]
        public void FrontierIsTheFirstReachableUnfinishedStage()
        {
            Assert.That(CampaignMapLayout.FrontierIndex(Save(prologueDone: false)), Is.EqualTo(-1));
            Assert.That(CampaignMapLayout.FrontierIndex(Save(prologueDone: true)), Is.EqualTo(0));
            Assert.That(CampaignMapLayout.FrontierIndex(Save(prologueDone: true, 0, 1)),
                Is.EqualTo(2));

            var all = Save(prologueDone: true);
            for (var i = 0; i < StageCatalog.Entries.Count; i++) all.ClearedMask |= 1 << i;
            Assert.That(CampaignMapLayout.FrontierIndex(all), Is.EqualTo(-1));
        }

        [Test]
        public void ProgressLineCountsClearsAndNamesTheNextRoom()
        {
            var data = Save(prologueDone: true, 0, 1);
            var line = CampaignMapLayout.ProgressLine(in data);
            Assert.That(line, Does.Contain("2"));
            Assert.That(line, Does.Contain(StageCatalog.Entries.Count.ToString()));
            Assert.That(line, Does.Contain(StageCatalog.Entries[2].Title));
        }

        [Test]
        public void GradeLadderCoversEveryEquipmentTier()
        {
            // The meta screen grades T0..T5; a short ladder would throw the
            // first time a player maxed a slot.
            Assert.That(MetaScreenView.GradeNames.Length,
                Is.EqualTo(LobbyView.EquipTierNames[0].Length));
            foreach (var tierNames in LobbyView.EquipTierNames)
                Assert.That(tierNames.Length, Is.EqualTo(MetaScreenView.GradeNames.Length));
        }
    }
}
