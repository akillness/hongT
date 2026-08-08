// The acquisition popup's rules, asserted without a canvas.
//
// Everything that decides WHAT the player reads and for how long lives in
// LootToastQueue, so the widget side (HudView.SyncLootToasts) is left with
// colour writes only. These are the rules that are cheap to get subtly wrong
// and expensive to notice in a browser: a magnet sweep that evicts the relic
// the player actually cared about, a row that reorders under its widget mid
// fade, or a reduced-motion run that still ramps.
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class LootToastQueueTests
    {
        private LootToastQueue _queue;

        [SetUp]
        public void SetUp() => _queue = new LootToastQueue();

        [Test]
        public void Push_PutsTheNewestRowFirstAndEvictsPastCapacity()
        {
            _queue.Push(LootToastKind.Shard, LootGrade.Basic);
            _queue.Push(LootToastKind.Flask, LootGrade.Basic);
            _queue.Push(LootToastKind.Relic, LootGrade.Epic);
            _queue.Push(LootToastKind.Equip, LootGrade.Fine);

            Assert.That(_queue.Count, Is.EqualTo(LootToastQueue.Capacity));
            Assert.That(_queue.SlotAt(0).Kind, Is.EqualTo(LootToastKind.Equip),
                "row 0 is the newest arrival");
            Assert.That(_queue.SlotAt(3).Kind, Is.EqualTo(LootToastKind.Shard),
                "the first pickup has been pushed to the bottom");

            // A fifth arrival must drop the OLDEST, never the newest.
            _queue.Push(LootToastKind.Relic, LootGrade.Basic);
            Assert.That(_queue.Count, Is.EqualTo(LootToastQueue.Capacity),
                "the column never grows past its measured four rows");
            Assert.That(_queue.SlotAt(0).Kind, Is.EqualTo(LootToastKind.Relic));
            Assert.That(_queue.SlotAt(0).Grade, Is.EqualTo(LootGrade.Basic));
            Assert.That(_queue.SlotAt(3).Kind, Is.EqualTo(LootToastKind.Flask),
                "the evicted row is the shard that arrived first");
        }

        [Test]
        public void IdenticalConsecutivePickups_StackOnOneRowAndRestampIt()
        {
            _queue.Push(LootToastKind.Shard, LootGrade.Basic);
            _queue.Tick(LootToastQueue.RiseSeconds + 0.4f);
            Assert.That(_queue.SlotAt(0).Age, Is.GreaterThan(0f));

            _queue.Push(LootToastKind.Shard, LootGrade.Basic);
            _queue.Push(LootToastKind.Shard, LootGrade.Basic);

            Assert.That(_queue.Count, Is.EqualTo(1),
                "a magnet sweep through one kind must not evict three other rows");
            Assert.That(_queue.SlotAt(0).Count, Is.EqualTo(3));
            Assert.That(_queue.SlotAt(0).Age, Is.EqualTo(0f),
                "a stack increment restarts the row's hold");
        }

        [Test]
        public void GradeIsPartOfTheRowIdentity_SoARarerDropNeverHidesInAStack()
        {
            _queue.Push(LootToastKind.Shard, LootGrade.Basic);
            _queue.Push(LootToastKind.Shard, LootGrade.Epic);

            Assert.That(_queue.Count, Is.EqualTo(2),
                "an Epic shard is the announcement — folding it into the Basic "
                + "row would silently downgrade it");
            Assert.That(_queue.SlotAt(0).Grade, Is.EqualTo(LootGrade.Epic));
            Assert.That(_queue.SlotAt(1).Grade, Is.EqualTo(LootGrade.Basic));
            Assert.That(_queue.SlotAt(1).Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_RetiresFromTheTailSoRowsNeverReorderUnderTheirWidgets()
        {
            _queue.Push(LootToastKind.Shard, LootGrade.Basic);
            _queue.Tick(0.9f);
            _queue.Push(LootToastKind.Relic, LootGrade.Epic);
            Assert.That(_queue.Count, Is.EqualTo(2));

            // Enough to expire the shard (age 0.9 + step) but not the relic.
            _queue.Tick(LootToastQueue.LifeSeconds - 0.8f);

            Assert.That(_queue.Count, Is.EqualTo(1), "the older row retires first");
            Assert.That(_queue.SlotAt(0).Kind, Is.EqualTo(LootToastKind.Relic),
                "the surviving row keeps index 0 — HudView's widget mapping is positional");

            _queue.Tick(LootToastQueue.LifeSeconds);
            Assert.That(_queue.Count, Is.EqualTo(0));
            Assert.That(_queue.AlphaAt(0), Is.EqualTo(0f),
                "a retired row reports zero opacity, not a stale value");
        }

        [Test]
        public void Alpha_RampsHoldsAndFadesInsideTheLifeWindow()
        {
            _queue.Push(LootToastKind.Flask, LootGrade.Fine);
            Assert.That(_queue.AlphaAt(0), Is.EqualTo(0f).Within(0.001f), "ramp starts at 0");

            _queue.Tick(LootToastQueue.RiseSeconds * 0.5f);
            Assert.That(_queue.AlphaAt(0), Is.EqualTo(0.5f).Within(0.01f), "linear ramp");

            _queue.Tick(LootToastQueue.RiseSeconds * 0.5f + LootToastQueue.HoldSeconds * 0.5f);
            Assert.That(_queue.AlphaAt(0), Is.EqualTo(1f).Within(0.001f), "full during hold");

            _queue.Tick(LootToastQueue.HoldSeconds * 0.5f + LootToastQueue.FadeSeconds * 0.5f);
            Assert.That(_queue.AlphaAt(0), Is.EqualTo(0.5f).Within(0.02f), "linear fade");

            _queue.Tick(LootToastQueue.FadeSeconds);
            Assert.That(_queue.Count, Is.EqualTo(0), "the row is gone by LifeSeconds");
        }

        [Test]
        public void ReducedMotion_ShowsAndHidesAtFullOpacityWithNoRamp()
        {
            _queue.Instant = true;
            _queue.Push(LootToastKind.Relic, LootGrade.Epic);
            Assert.That(_queue.AlphaAt(0), Is.EqualTo(1f), "no ramp-in under reduced motion");

            _queue.Tick(LootToastQueue.RiseSeconds + LootToastQueue.HoldSeconds
                + LootToastQueue.FadeSeconds * 0.5f);
            Assert.That(_queue.AlphaAt(0), Is.EqualTo(1f),
                "no fade-out either — the row stays solid until it is simply gone");
            Assert.That(_queue.Count, Is.EqualTo(1), "and it holds for the same total time");

            _queue.Tick(LootToastQueue.FadeSeconds);
            Assert.That(_queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void Revision_MovesOnlyWhenTheVisibleTextCanHaveChanged()
        {
            var start = _queue.Revision;
            _queue.Tick(0.1f);
            Assert.That(_queue.Revision, Is.EqualTo(start), "an empty tick changes nothing");

            _queue.Push(LootToastKind.Shard, LootGrade.Basic);
            var afterPush = _queue.Revision;
            Assert.That(afterPush, Is.Not.EqualTo(start), "a new row is new text");

            _queue.Tick(0.1f);
            Assert.That(_queue.Revision, Is.EqualTo(afterPush),
                "a plain fade must not force HudView to rebuild its strings");

            _queue.Push(LootToastKind.Shard, LootGrade.Basic);
            Assert.That(_queue.Revision, Is.Not.EqualTo(afterPush),
                "a stack increment changes the 'x2' suffix");

            var afterStack = _queue.Revision;
            _queue.Tick(LootToastQueue.LifeSeconds);
            Assert.That(_queue.Revision, Is.Not.EqualTo(afterStack), "an eviction changes the set");
        }

        [Test]
        public void Clear_DropsEveryRowWithoutAFade()
        {
            _queue.Push(LootToastKind.Shard, LootGrade.Basic);
            _queue.Push(LootToastKind.Relic, LootGrade.Epic);
            var before = _queue.Revision;

            _queue.Clear();

            Assert.That(_queue.Count, Is.EqualTo(0),
                "a retry must not open on the previous run's last pickup");
            Assert.That(_queue.Revision, Is.Not.EqualTo(before));
            Assert.That(_queue.SlotAt(0).Count, Is.EqualTo(0), "no row is readable after Clear");
        }

        [Test]
        public void KindOf_GivesEveryPickupKindExactlyOneRowIdentity()
        {
            Assert.That(LootToastQueue.KindOf(PickupKind.EmberShard),
                Is.EqualTo(LootToastKind.Shard));
            Assert.That(LootToastQueue.KindOf(PickupKind.OilFlask),
                Is.EqualTo(LootToastKind.Flask));
            Assert.That(LootToastQueue.KindOf(PickupKind.RelicMote),
                Is.EqualTo(LootToastKind.Relic));
            Assert.That(LootToastQueue.KindOf(PickupKind.EquipShard),
                Is.EqualTo(LootToastKind.Equip));
        }
    }
}
