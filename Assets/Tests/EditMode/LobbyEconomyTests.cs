// Lobby purchase economy (2026-08-07 audit M9, LootDrops T-3).
// Target seam: GameDirector.TryBuyEquip — the pure judgment + mutation half of
// OnBuyEquip (tier lookup -> tier-5 cap -> cost ladder -> balance check ->
// relic debit -> tier increment). Persistence (CampaignStore.Save) and the UI
// refresh stay in OnBuyEquip, so these tests never touch PlayerPrefs and need
// no MonoBehaviour: CampaignData is a plain struct.
//
// Cost truth: spec §6 L117 — relics for T(i)->T(i+1) = { 2, 4, 7, 11, 16 },
// single-sourced at GameDirector.EquipCosts (LobbyView delegates to it).
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class LobbyEconomyTests
    {
        static readonly string[] Slots = { "weapon", "lantern", "cloak" };

        static CampaignData Data(int relics, int weapon = 0, int lantern = 0, int cloak = 0)
        {
            var data = default(CampaignData);
            data.Relics = relics;
            data.Weapon = weapon;
            data.Lantern = lantern;
            data.Cloak = cloak;
            return data;
        }

        static int TierOf(in CampaignData data, string slot) => slot switch
        {
            "weapon" => data.Weapon,
            "lantern" => data.Lantern,
            _ => data.Cloak,
        };

        // Regression caught: anyone editing the { 2, 4, 7, 11, 16 } ladder (or
        // splitting it back into per-file copies with different values).
        [Test]
        public void CostLadder_IsTheSpecTable()
        {
            Assert.That(GameDirector.EquipCosts, Is.EqualTo(new[] { 2, 4, 7, 11, 16 }),
                "spec §6 L117: relic costs for T0->T1 .. T4->T5");
        }

        // Regression caught: charging the wrong ladder step for the current tier,
        // debiting the wrong amount, or bumping the wrong slot's tier.
        [Test]
        public void Buy_ChargesTheTierCost_AndRaisesOnlyThatSlot(
            [Values("weapon", "lantern", "cloak")] string slot,
            [Range(0, 4)] int tier)
        {
            var data = Data(relics: 99,
                weapon: slot == "weapon" ? tier : 0,
                lantern: slot == "lantern" ? tier : 0,
                cloak: slot == "cloak" ? tier : 0);

            Assert.That(GameDirector.TryBuyEquip(ref data, slot), Is.True);
            Assert.That(data.Relics, Is.EqualTo(99 - GameDirector.EquipCosts[tier]),
                "debit must be the ladder cost of the tier being left");
            Assert.That(TierOf(in data, slot), Is.EqualTo(tier + 1));

            foreach (var other in Slots)
            {
                if (other != slot)
                {
                    Assert.That(TierOf(in data, other), Is.EqualTo(0),
                        $"buying {slot} must not move {other}");
                }
            }
        }

        // Regression caught: a purchase going through with insufficient relics
        // (balance check dropped or moved after the debit).
        [Test]
        public void Buy_RefusedWhenBalanceIsOneShort([Range(0, 4)] int tier)
        {
            var data = Data(relics: GameDirector.EquipCosts[tier] - 1, weapon: tier);

            Assert.That(GameDirector.TryBuyEquip(ref data, "weapon"), Is.False);
            Assert.That(data.Relics, Is.EqualTo(GameDirector.EquipCosts[tier] - 1),
                "a refused purchase must not touch the balance");
            Assert.That(data.Weapon, Is.EqualTo(tier), "a refused purchase must not raise the tier");
        }

        // Regression caught: exact-balance purchases being rejected (< vs <=
        // confusion) — the spec ladder is spendable down to zero.
        [Test]
        public void Buy_ExactBalanceSucceeds_AndDrainsToZero()
        {
            var data = Data(relics: GameDirector.EquipCosts[3], lantern: 3);

            Assert.That(GameDirector.TryBuyEquip(ref data, "lantern"), Is.True);
            Assert.That(data.Relics, Is.EqualTo(0));
            Assert.That(data.Lantern, Is.EqualTo(4));
        }

        // Regression caught: buying past T5 (cap check dropped => next buy would
        // also index EquipCosts[5] out of bounds).
        [Test]
        public void Buy_RefusedAtTierFiveCap([Values("weapon", "lantern", "cloak")] string slot)
        {
            var data = Data(relics: 999, weapon: 5, lantern: 5, cloak: 5);

            Assert.That(GameDirector.TryBuyEquip(ref data, slot), Is.False);
            Assert.That(data.Relics, Is.EqualTo(999), "a capped slot must charge nothing");
            Assert.That(TierOf(in data, slot), Is.EqualTo(5));
        }

        // Regression caught: the T0->T5 walk costing anything but 2+4+7+11+16 = 40,
        // or the ladder ending anywhere but exactly at the cap.
        [Test]
        public void Buy_FullLadderWalk_CostsFortyRelicsTotal()
        {
            var data = Data(relics: 40);

            var purchases = 0;
            while (GameDirector.TryBuyEquip(ref data, "cloak"))
            {
                purchases += 1;
                Assert.That(purchases, Is.LessThanOrEqualTo(5), "ladder must stop at five buys");
            }

            Assert.That(purchases, Is.EqualTo(5));
            Assert.That(data.Cloak, Is.EqualTo(5));
            Assert.That(data.Relics, Is.EqualTo(0), "T0->T5 must cost exactly 40 relics");
        }

        // Pins the PRESERVED historical behavior (audit decision, safe default):
        // both original switches routed unknown slot strings through their
        // default arm, i.e. an unrecognized id silently buys CLOAK. If that is
        // ever tightened to an explicit refusal, this test must flip WITH the
        // change — it exists so the tightening is loud, not accidental.
        [Test]
        public void Buy_UnknownSlot_KeepsBuyingCloak_HistoricalContract()
        {
            var data = Data(relics: 10);

            Assert.That(GameDirector.TryBuyEquip(ref data, "no-such-slot"), Is.True);
            Assert.That(data.Cloak, Is.EqualTo(1), "unknown slot historically falls through to cloak");
            Assert.That(data.Weapon, Is.EqualTo(0));
            Assert.That(data.Lantern, Is.EqualTo(0));
            Assert.That(data.Relics, Is.EqualTo(10 - GameDirector.EquipCosts[0]));
        }
    }
}
