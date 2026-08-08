// Campaign minimap model — WHAT the lobby map shows, never HOW it is drawn.
//
// The lobby had no map at all: progress was readable only as nine stacked
// sortie cards, which says "list" and not "descent". This turns the same
// CampaignData the cards already read into a constellation of nodes and links,
// with one revealed-ness state per node, so the renderer (CampaignMapView) is
// left with nothing to decide.
//
// Pure logic, no UnityEngine — same contract as CommandConsoleBuffer: every
// reveal rule is testable without building a canvas. StageCatalog is the only
// source of node positions and prereq edges, so the map can never disagree
// with the route the sortie panel actually offers.
using System.Collections.Generic;

namespace CinderCourt.View
{
    /// <summary>How much of a node the player has earned the right to see.</summary>
    public enum CampaignNodeState
    {
        /// <summary>Prereq unmet (or prologue undone): position only, no name.</summary>
        Locked = 0,
        /// <summary>Reachable, never finished: name shown, objective withheld.</summary>
        Unlocked = 1,
        /// <summary>Finished at least once: fully lit, epithet shown.</summary>
        Cleared = 2,
    }

    /// <summary>One placed, state-resolved map node.</summary>
    public readonly struct CampaignMapNode
    {
        public readonly int CatalogIndex;
        public readonly string Id;
        /// <summary>Display name, or <see cref="CampaignMapLayout.HiddenLabel"/>
        /// while locked — the reveal IS the label, so the renderer never has to
        /// re-decide what a locked node is allowed to say.</summary>
        public readonly string Label;
        /// <summary>Cleared only: the stage's gimmick epithet. "" otherwise.</summary>
        public readonly string Epithet;
        public readonly float X, Y;
        public readonly CampaignNodeState State;

        public CampaignMapNode(int catalogIndex, string id, string label, string epithet,
            float x, float y, CampaignNodeState state)
        {
            CatalogIndex = catalogIndex;
            Id = id;
            Label = label;
            Epithet = epithet;
            X = x;
            Y = y;
            State = state;
        }
    }

    /// <summary>A prereq edge, drawn from the prerequisite to its dependant.</summary>
    public readonly struct CampaignMapLink
    {
        public readonly int FromIndex, ToIndex;
        /// <summary>True once the PREREQUISITE is cleared — a lit thread means
        /// "this road is walked", which is exactly when the far node stops
        /// being locked.</summary>
        public readonly bool Lit;

        public CampaignMapLink(int fromIndex, int toIndex, bool lit)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
            Lit = lit;
        }
    }

    public static class CampaignMapLayout
    {
        /// <summary>What a locked node is allowed to say. ASCII on purpose: the
        /// HUD font is a generated Korean subset and a placeholder is the last
        /// string that should be able to fail glyph coverage.</summary>
        public const string HiddenLabel = "???";

        /// <summary>Per-state opacity — the whole "밝혀가는" grammar in three
        /// numbers. Cleared is full, unlocked is the half-lit invitation, locked
        /// is present-but-unreadable (never zero: an invisible node would hide
        /// that the campaign continues at all).</summary>
        public const float ClearedAlpha = 1f;
        public const float UnlockedAlpha = 0.55f;
        public const float LockedAlpha = 0.16f;

        /// <summary>Smallest normalised gap any two nodes keep on at least one
        /// axis. Pinned by test, because node coordinates are hand-placed and a
        /// careless edit is otherwise invisible until two labels overlap in the
        /// shipped build.</summary>
        public const float MinSeparation = 0.10f;

        public static float AlphaOf(CampaignNodeState state)
            => state == CampaignNodeState.Cleared ? ClearedAlpha
             : state == CampaignNodeState.Unlocked ? UnlockedAlpha
             : LockedAlpha;

        public static CampaignNodeState StateOf(in CampaignData data, in StageEntry entry)
            => StageCatalog.IsCleared(in data, in entry) ? CampaignNodeState.Cleared
             : StageCatalog.IsUnlocked(in data, in entry) ? CampaignNodeState.Unlocked
             : CampaignNodeState.Locked;

        /// <summary>Every catalog stage, in catalog order, resolved against one
        /// save. Order is the contract: the renderer builds its widgets once and
        /// then only re-reads this array, so index i is always stage i.</summary>
        public static CampaignMapNode[] BuildNodes(in CampaignData data)
        {
            var entries = StageCatalog.Entries;
            var nodes = new CampaignMapNode[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var state = StateOf(in data, in entry);
                nodes[i] = new CampaignMapNode(
                    entry.CatalogIndex, entry.Id,
                    state == CampaignNodeState.Locked ? HiddenLabel : entry.Title,
                    state == CampaignNodeState.Cleared ? entry.Epithet : "",
                    entry.NodeX, entry.NodeY, state);
            }
            return nodes;
        }

        /// <summary>Prereq edges. Stage 0 has no prerequisite, so the link count
        /// is one short of the node count for the current linear chain — but the
        /// walk is by PrereqId, so a future branch produces its edges for free.</summary>
        public static CampaignMapLink[] BuildLinks(in CampaignData data)
        {
            var entries = StageCatalog.Entries;
            var links = new List<CampaignMapLink>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.PrereqId)) continue;
                if (!StageCatalog.TryGet(entry.PrereqId, out var prerequisite)) continue;
                links.Add(new CampaignMapLink(prerequisite.CatalogIndex, entry.CatalogIndex,
                    StageCatalog.IsCleared(in data, in prerequisite)));
            }
            return links.ToArray();
        }

        public static int ClearedCount(in CampaignData data)
        {
            var entries = StageCatalog.Entries;
            var count = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (StageCatalog.IsCleared(in data, in entry)) count++;
            }
            return count;
        }

        /// <summary>Catalog index of the first unlocked-but-uncleared stage —
        /// the node the map should point at. -1 when nothing is reachable (no
        /// prologue) or everything is done.</summary>
        public static int FrontierIndex(in CampaignData data)
        {
            var entries = StageCatalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (StateOf(in data, in entry) == CampaignNodeState.Unlocked)
                    return entry.CatalogIndex;
            }
            return -1;
        }

        /// <summary>Progress line for the map header: "정화 3 / 9 • 다음 서약의 성당".
        /// Composed here so the lobby panel and the meta screen's map tab can
        /// never drift into two different summaries of the same save.</summary>
        public static string ProgressLine(in CampaignData data)
        {
            var total = StageCatalog.Entries.Count;
            var cleared = ClearedCount(in data);
            var frontier = FrontierIndex(in data);
            if (frontier < 0)
                return cleared >= total
                    ? $"정화 {cleared} / {total} • 전 구역 정화"
                    : $"정화 {cleared} / {total} • 훈련 필요";
            return $"정화 {cleared} / {total} • 다음 {StageCatalog.Entries[frontier].Title}";
        }
    }
}
