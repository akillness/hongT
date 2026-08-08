// AMENDMENT #8 — progression navigation (design/progression-navigation-spec.md).
//
// Every answer this file gives is DERIVED from state the save already holds:
// ClearedMask, PrologueDone, TrialTiers, Relics, Points, equipment tiers,
// SigilsOwned. There is no new persisted field and no new sim number. The
// cycle-4 finding was that the lobby knows where the player is and never says
// so — this file is the saying, not the knowing.
//
// Pure static, no UnityEngine state, no allocation on the Refresh path (every
// method returns a struct or an int; the only strings built are the lock
// reasons, which Refresh assigns to a Text and are therefore unavoidable).
//
// It deliberately owns NO rule of its own:
//   unlock     -> StageCatalog.IsUnlocked
//   mastery    -> CampaignStore.MasteryComplete / BestTier
//   hazards    -> StageEntry.HazardOverride ?? CampaignStages anchor
//   prices     -> the tables below, read at runtime and never inlined as a
//                 threshold constant (negotiation entry 10: a hard-coded "72"
//                 or a static tab priority flips the entry-6 verdict).
using CinderCourt.Sim;

namespace CinderCourt.View
{
    /// <summary>What kind of thing the lobby is pointing at.</summary>
    public enum GuideTargetKind
    {
        /// <summary>Nothing left to point at — every stage cleared and mastery
        /// claimed. A legitimate terminal state, not a failure.</summary>
        None = 0,
        Prologue = 1,
        Stage = 2,
        Trial = 3,
    }

    /// <summary>Why a stage cannot be entered. Exactly the two causes
    /// <see cref="StageCatalog.IsUnlocked"/> can produce.</summary>
    public enum LockReason
    {
        /// <summary>Not locked.</summary>
        None = 0,
        PrologueIncomplete = 1,
        PrerequisiteUncleared = 2,
    }

    /// <summary>The single thing the lobby points at this frame.</summary>
    public readonly struct GuideTarget
    {
        public readonly GuideTargetKind Kind;
        /// <summary>Catalog index for <see cref="GuideTargetKind.Stage"/>, trial
        /// index for <see cref="GuideTargetKind.Trial"/>, else -1.</summary>
        public readonly int Index;

        public GuideTarget(GuideTargetKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static readonly GuideTarget Nothing = new GuideTarget(GuideTargetKind.None, -1);
    }

    /// <summary>Which SANCTUM tab, if any, the badge is lit on.</summary>
    public readonly struct SanctumBadges
    {
        public readonly bool Growth;    // Points
        public readonly bool Equip;     // Relics
        public readonly bool Legion;    // free
        public readonly bool Sigil;     // Relics

        public SanctumBadges(bool growth, bool equip, bool legion, bool sigil)
        {
            Growth = growth;
            Equip = equip;
            Legion = legion;
            Sigil = sigil;
        }
    }

    public static class ProgressionGuide
    {
        // ---------------------------------------------------------- prices --
        /// <summary>Relics for T(i) -> T(i+1). Single source: LobbyView showed
        /// the buy line from its own copy and GameDirector charged from a
        /// second one. Two copies of a price is one price and one bug waiting.</summary>
        public static readonly int[] EquipCosts = { 2, 4, 7, 11, 16 };
        /// <summary>Highest equipment tier. Derived so a sixth cost can never
        /// disagree with a hard-coded cap.</summary>
        public static readonly int EquipCap = EquipCosts.Length;
        /// <summary>Relics to unlock one sigil, once (negotiation entry 6 —
        /// still unsigned; the badge rule reads it instead of assuming it).</summary>
        public const int SigilCost = 12;
        /// <summary>Allocated-stat ceiling per stat.</summary>
        public const int StatCap = 10;
        /// <summary>The five real sigils in catalog order. SigilKind.None is
        /// excluded on purpose: it is the inert zero, not a row. Owning the
        /// order here (rather than in the view) is what lets the badge rule ask
        /// "is any sigil still unowned" without importing a UI type.</summary>
        public static readonly SigilKind[] SigilOrder =
        {
            SigilKind.Countercurrent, SigilKind.Verdict, SigilKind.Executioner,
            SigilKind.Ignition, SigilKind.Witness,
        };

        // ------------------------------------------------------------ acts --
        /// <summary>worldview.md §공간 계보: nine stages are three acts of three.
        /// The catalog is authored in act order, so the act is the index / 3 —
        /// pinned by ProgressionNavigationTests against the catalog itself.</summary>
        public const int ActCount = 3;
        public const int StagesPerAct = 3;
        /// <summary>Group index for the training ground — the fourth accordion
        /// group, which is not an act.</summary>
        public const int TrainingGroup = ActCount;
        public const int GroupCount = ActCount + 1;

        /// <summary>Act titles. worldview.md §공간 계보 is the single source;
        /// changing one here without the table there is a G1 violation.</summary>
        public static readonly string[] ActTitles = { "제1부 기록", "제2부 증언", "제3부 집행" };
        public static readonly string[] ActKickers = { "ACT I RECORD", "ACT II TESTIMONY", "ACT III EXECUTION" };
        public const string TrainingTitle = "훈련장";
        public const string TrainingKicker = "TRAINING";

        /// <summary>Which act a catalog stage belongs to.</summary>
        public static int ActOf(int catalogIndex) => catalogIndex / StagesPerAct;

        // ---------------------------------------------------------- target --
        /// <summary>
        /// The one thing to do next, in the order the game itself enforces:
        /// prologue, then the lowest-index unlocked-but-uncleared stage, then
        /// the lowest-index trial short of the top tier.
        ///
        /// Index order is a total order, so there is never a tie — the slot
        /// count is structurally 0 or 1, never 2. When it is 0 the player has
        /// finished everything the lobby can offer, which is a real state and
        /// gets its own copy rather than a fabricated suggestion.
        /// </summary>
        public static GuideTarget NextTarget(in CampaignData data)
        {
            if (!data.PrologueDone) return new GuideTarget(GuideTargetKind.Prologue, -1);

            for (var i = 0; i < StageCatalog.Entries.Count; i++)
            {
                var entry = StageCatalog.Entries[i];
                if (StageCatalog.IsCleared(in data, in entry)) continue;
                if (StageCatalog.IsUnlocked(in data, in entry))
                    return new GuideTarget(GuideTargetKind.Stage, i);
            }

            if (!CampaignStore.MasteryComplete(in data))
            {
                var top = HackSpec.TrainingTiers - 1;
                for (var i = 0; i < TrainingTrials.Ids.Length; i++)
                    if (CampaignStore.BestTier(in data, i) < top)
                        return new GuideTarget(GuideTargetKind.Trial, i);
            }

            return GuideTarget.Nothing;
        }

        /// <summary>Which accordion group the target lives in — the group the
        /// lobby opens on entry so "지금 여기" is never behind a fold.</summary>
        public static int GroupOfTarget(in GuideTarget target)
        {
            switch (target.Kind)
            {
                case GuideTargetKind.Stage: return ActOf(target.Index);
                case GuideTargetKind.Trial: return TrainingGroup;
                case GuideTargetKind.Prologue: return 0;   // act I is where a new player starts
                default: return TrainingGroup;             // finished: the repeatable lane
            }
        }

        // ------------------------------------------------------------ locks --
        /// <summary>Why this stage is locked. Mirrors
        /// <see cref="StageCatalog.IsUnlocked"/> branch for branch — it does not
        /// re-derive the rule, it names the branch that fired.</summary>
        public static LockReason LockReasonFor(in CampaignData data, in StageEntry entry)
        {
            if (StageCatalog.IsUnlocked(in data, in entry)) return LockReason.None;
            if (!data.PrologueDone) return LockReason.PrologueIncomplete;
            return LockReason.PrerequisiteUncleared;
        }

        /// <summary>Korean display name of the prerequisite, or "" when the
        /// stage has none / the id is unknown.</summary>
        public static string PrerequisiteTitle(in StageEntry entry)
        {
            if (string.IsNullOrEmpty(entry.PrereqId)) return string.Empty;
            return StageCatalog.TryGet(entry.PrereqId, out var prerequisite)
                ? prerequisite.Title
                : string.Empty;
        }

        /// <summary>
        /// The stage card's sub-line. The epithet always leads: it is the
        /// preview (N8), and a locked card that stops previewing stops teaching.
        /// The tail is the reason, the reward, or nothing.
        /// </summary>
        public static string StageSubLine(in CampaignData data, in StageEntry entry, string rewardText)
        {
            var epithet = entry.Epithet;
            switch (LockReasonFor(in data, in entry))
            {
                case LockReason.PrologueIncomplete:
                    return epithet + " • 점화 훈련 필요";
                case LockReason.PrerequisiteUncleared:
                {
                    var prerequisite = PrerequisiteTitle(in entry);
                    return prerequisite.Length == 0
                        ? epithet + " • 선행 정화 필요"
                        : epithet + " • 선행: " + prerequisite;
                }
                default:
                    // Unlocked. Cleared cards drop the reward tail (the reward is
                    // already redeemed); uncleared ones keep advertising it.
                    return StageCatalog.IsCleared(in data, in entry)
                        ? epithet
                        : epithet + " • 보상: " + rewardText;
            }
        }

        // --------------------------------------------------------- counters --
        /// <summary>Cleared stages in one act.</summary>
        public static int ClearedInAct(in CampaignData data, int act)
        {
            var cleared = 0;
            var first = act * StagesPerAct;
            for (var i = first; i < first + StagesPerAct && i < StageCatalog.Entries.Count; i++)
            {
                var entry = StageCatalog.Entries[i];
                if (StageCatalog.IsCleared(in data, in entry)) cleared++;
            }
            return cleared;
        }

        /// <summary>Cleared stages overall — the 정화 n/9 gauge.</summary>
        public static int ClearedTotal(in CampaignData data)
        {
            var cleared = 0;
            for (var i = 0; i < StageCatalog.Entries.Count; i++)
            {
                var entry = StageCatalog.Entries[i];
                if (StageCatalog.IsCleared(in data, in entry)) cleared++;
            }
            return cleared;
        }

        /// <summary>Trials standing at the top tier — the training group's n/5.</summary>
        public static int MasteredTrials(in CampaignData data)
        {
            var top = HackSpec.TrainingTiers - 1;
            var mastered = 0;
            for (var i = 0; i < TrainingTrials.Ids.Length; i++)
                if (CampaignStore.BestTier(in data, i) >= top) mastered++;
            return mastered;
        }

        // ----------------------------------------------------------- badges --
        /// <summary>Cheapest affordable equipment step, or -1 when none is.</summary>
        public static int CheapestEquipCost(in CampaignData data)
        {
            var cheapest = -1;
            for (var slot = 0; slot < 3; slot++)
            {
                var tier = slot == 0 ? data.Weapon : slot == 1 ? data.Lantern : data.Cloak;
                if (tier >= EquipCap) continue;
                var cost = EquipCosts[tier];
                if (data.Relics < cost) continue;
                if (cheapest < 0 || cost < cheapest) cheapest = cost;
            }
            return cheapest;
        }

        /// <summary>True when relics can unlock a sigil that is not owned yet.</summary>
        public static bool CanBuyAnySigil(in CampaignData data)
        {
            if (data.Relics < SigilCost) return false;
            for (var i = 0; i < SigilOrder.Length; i++)
                if ((data.SigilsOwned & (1 << (int)SigilOrder[i])) == 0) return true;
            return false;
        }

        /// <summary>
        /// Which tabs to badge (negotiation entry 10).
        ///
        /// Growth and Legion spend different currencies (Points, nothing), so
        /// they light independently. Equipment and sigils both spend relics, so
        /// at most ONE of them lights: the cheapest affordable step. That single
        /// rule is what keeps misdirection inside the PM band — pointing at a
        /// 12-relic sigil while a 2-relic equipment step is affordable measures
        /// |12-2| = 10 against a band of 2.
        ///
        /// The rule compares prices it reads at runtime. It does not know that
        /// equipment starts at 2, and it must not: entry 6 has never signed the
        /// sigil price, and a rule that inlines today's numbers would have to be
        /// re-litigated the moment that signature lands.
        /// </summary>
        public static SanctumBadges Badges(in CampaignData data)
        {
            var growth = data.Points > 0 &&
                (data.Attack < StatCap ||
                 data.Vitality < StatCap ||
                 data.Swiftness < StatCap);

            var legion = false;
            if (data.Roster != null)
            {
                for (var i = 0; i < data.Roster.Length; i++)
                {
                    var id = data.Roster[i];
                    if (!string.IsNullOrEmpty(id) && id != data.Active) { legion = true; break; }
                }
            }

            var equipCost = CheapestEquipCost(in data);
            var sigilAffordable = CanBuyAnySigil(in data);

            // Cheapest wins; a tie cannot happen while the tables differ, but if
            // the prices ever meet, equipment takes it — it is the step whose
            // effect applies in every stage rather than one gimmick's.
            var equipBadge = equipCost >= 0 && (!sigilAffordable || equipCost <= SigilCost);
            var sigilBadge = sigilAffordable && !equipBadge;

            return new SanctumBadges(growth, equipBadge, legion, sigilBadge);
        }

        // ---------------------------------------------------------- context --
        /// <summary>
        /// Does this sigil's bound gimmick appear in the next target stage?
        ///
        /// This is the question the genre could not ask (novelty-scorecard N11,
        /// 0/17): everywhere else a meta upgrade is a global constant, so "is it
        /// live next run" has no answer. Ours bind to gimmicks — 집행인 fires in
        /// one stage of nine, 점화인 in nine of nine, and both cost 12.
        /// </summary>
        public static bool SigilLiveInTarget(in CampaignData data, SigilKind kind, in GuideTarget target)
        {
            // SigilKind.None is the inert zero, not a sigil. Nothing binds to it,
            // so nothing can be live for it.
            if (kind == SigilKind.None) return false;
            if (target.Kind != GuideTargetKind.Stage) return false;
            var hazards = EffectiveHazards(target.Index);
            if (hazards == null) return false;
            var bound = HazardOf(kind);
            for (var i = 0; i < hazards.Length; i++)
                if (hazards[i].Kind == bound) return true;
            return false;
        }

        /// <summary>Gimmick each sigil binds — the same pairing CinderSim applies
        /// its clauses through (CinderSim.ApplySigils).
        ///
        /// Witness is the explicit last case rather than the default, so a
        /// caller passing SigilKind.None gets 제단 by an ArgumentOutOfRange
        /// rather than by silence. Under `default:` the inert zero mapped to a
        /// real hazard, which is unreachable today only because every caller
        /// walks SigilOrder — a guarantee held by convention, not by the type.</summary>
        public static HazardKind HazardOf(SigilKind kind)
        {
            switch (kind)
            {
                case SigilKind.Countercurrent: return HazardKind.TideCurrent;
                case SigilKind.Verdict: return HazardKind.EmberPylon;
                case SigilKind.Executioner: return HazardKind.AshWall;
                case SigilKind.Ignition: return HazardKind.EmberVent;
                case SigilKind.Witness: return HazardKind.RelicAltar;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(kind), kind, "SigilKind.None binds no gimmick.");
            }
        }

        /// <summary>The hazard table a sortie to this stage would actually run:
        /// the catalog override when it has one, else the frozen sim anchor.
        /// The verdict pact is deliberately NOT consulted — it is a per-visit
        /// opt-in, and a label that changed under a toggle would be advice about
        /// a run the player has not chosen yet.</summary>
        public static HazardConfig[] EffectiveHazards(int catalogIndex)
        {
            if (catalogIndex < 0 || catalogIndex >= StageCatalog.Entries.Count) return null;
            var entry = StageCatalog.Entries[catalogIndex];
            if (entry.HazardOverride != null) return entry.HazardOverride;
            return CampaignStages.TryGet(entry.SimAnchorId, 0, 0, 0, out var config)
                ? config.Hazards
                : null;
        }
    }
}
