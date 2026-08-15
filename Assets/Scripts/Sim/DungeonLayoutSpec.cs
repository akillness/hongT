// AMENDMENT #17 — dungeon interior layout.
// Design truth: _workspace/current/design/dungeon-interior-spec.md §3.
//
// NOT a frozen contract file. Everything here is reachable only from the dungeon
// stage tables, and every value is a placement — no combat number lives in this
// file. It follows the DifficultySpec.cs / DungeonProgressionSpec.cs precedent.
//
// WHY LANES AND NOT CONCENTRIC RINGS. The first draft banded the ellipse into
// core / ring / bays / wall. It was refuted by arithmetic before any code: a
// passage needs 205 px of clear width, which on the y axis is an ellipse
// parameter of 205 / 390 = 0.526, and four such bands need 2.10 of a parameter
// that only runs to 1.0. A 1.885 aspect ellipse cannot be divided concentrically.
// Lanes along the LONG axis are what the geometry allows, and they happen to be
// what the level-design literature recommends anyway (1-3 lanes, never a maze).
//
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
using System;
using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>
    /// The lane-spine grammar every dungeon stage is composed from. Stages differ by
    /// which spines they raise, where the passages sit, and what free cover stands in
    /// the lanes — not by inventing new geometry.
    /// </summary>
    public static class DungeonLayoutSpec
    {
        /// <summary>
        /// Lane spine y positions. The playfield's 780 px of height splits into three
        /// 228 px lanes separated by two 48 px walls (2 * <see cref="CampaignSpec.StoneWallRadius"/>).
        /// 228 - 2 * 26 leaves 176 px of free travel per lane for a player of push
        /// radius 26, comfortably over the 150 px floor the spec sets.
        /// </summary>
        public const float SpineNorthY = 466f;
        public const float SpineSouthY = 742f;

        /// <summary>
        /// Half of <see cref="CampaignSpec.LanePassageWidth"/>. Named because the
        /// segment arithmetic below uses it four times and a passage that is not
        /// symmetric about its centre is a bug nobody would see.
        /// </summary>
        const float PassageHalf = CampaignSpec.LanePassageWidth * 0.5f;

        /// <summary>
        /// A wall stub shorter than this is dropped. Below roughly two thicknesses a
        /// capsule reads as a post rather than a wall, and it adds a pinch point
        /// against the boundary without adding a route decision.
        /// </summary>
        const float MinSegment = 2f * (2f * CampaignSpec.StoneWallRadius);   // 96 px

        /// <summary>
        /// Playfield half-width at a given y, on the expanded clamp ellipse. This is
        /// how far a spine can run before it leaves the arena — computed, never
        /// tabulated, so moving the bounds moves the spines with them.
        /// </summary>
        public static float HalfWidthAt(float y)
        {
            float dy = (y - SimConfig.ArenaY) / DungeonBoundsSpec.ExpandedHalfHeight;
            float inside = 1f - dy * dy;
            if (inside <= 0f)
            {
                return 0f;
            }
            return DungeonBoundsSpec.ExpandedHalfWidth * MathF.Sqrt(inside);
        }

        /// <summary>
        /// Minimum open run between a spine's end cap and the boundary.
        ///
        /// A SPINE MUST NEVER REACH THE ARENA WALL. Measured, and it is the single
        /// hardest constraint in this file: the shipped steering aims at the angular
        /// silhouette of one blocker, so for a wall it aims past an END CAP. If that
        /// cap is sealed against the ellipse there is nothing to round, and the pack
        /// walks into the wall and stays there — cinder-span read a 12.70% stall rate
        /// and ash-march 22.92% with full-width spines, WORSE than having no layout at
        /// all. The dense two-spine stage read 0.24% over the same run, because its
        /// extra passages gave the cone somewhere to point.
        ///
        /// This is the "cannot escape a concave pocket" limit arriving in practice: a
        /// lane closed by a wall-to-wall spine IS a pocket whose only doors are the
        /// passages, and a per-blocker cone cannot see a door. Keeping both ends open
        /// turns every lane into a through-route, which is also what the Jaquays
        /// requirement asked for on paper.
        /// </summary>
        public const float EndOpening = 300f;

        /// <summary>
        /// Separation a spine's doorway leaves around the gimmick that opened it.
        ///
        /// EQUAL TO <see cref="MinCorridor"/>, and it has to be. When this was smaller
        /// (20 px) the two rules fought: the doorway opened a 20 px gap and the pinch
        /// filter then rejected the very segments that gap had created, taking every
        /// spine on all six stages with it. A door sized under the corridor minimum is
        /// not a door.
        /// </summary>
        public const float GimmickClearance = MinCorridor;

        /// <summary>
        /// No two solids may leave a corridor narrower than this WITHOUT touching.
        /// Adjacent is safe — two solids that meet are one bigger solid. Nearly
        /// adjacent is the trap.
        ///
        /// This replaced a global keep-out radius, and the reason is that a keep-out
        /// cannot express the property. Sweeping it over {20, 60, 100, 140} moved a
        /// ~50% stall spike BETWEEN stages rather than removing it — abyss-chancel at
        /// 20, ember-bastion at 60, echo-throne at 100 — because the trap is an
        /// emergent pair, not a distance. Widening the radius far enough to be safe by
        /// luck erased the layout instead: at a full passage width the six stages
        /// retained ONE spine segment between them, which is not an interior.
        ///
        /// Rejecting the specific piece that forms a narrow gap keeps everything else.
        /// </summary>
        public const float MinCorridor = 150f;

        /// <summary>
        /// True for gimmicks with a LOCAL footprint. Bands are excluded: an ash wall
        /// sweeps the whole arena from an edge and a tide current is a full-width lane,
        /// so neither has a place for a wall to be "too close to" — and treating them as
        /// circles at their anchor would carve a hole in every spine for no reason.
        /// </summary>
        static bool HasLocalFootprint(HazardKind kind)
            => kind != HazardKind.AshWall
            && kind != HazardKind.TideCurrent
            && kind != HazardKind.StoneWall;

        /// <summary>
        /// The keep-out a gimmick demands from a blocker, which depends on whether the
        /// gimmick is itself SOLID.
        ///
        /// AMENDMENT #17b. <see cref="GimmickClearance"/> is a full corridor, and a
        /// corridor is what two SOLIDS need between them — the pinch rule exists because
        /// an actor that fits through neither gap gets caught in the middle. A vent is
        /// not solid: the sim lets anyone walk straight over it, so a cover piece beside
        /// one cannot form a pinch, because there is no second wall. Charging a passable
        /// hazard the price of a wall was measured, not theorised: with four vents on the
        /// board it sterilised the ENTIRE cover lattice, and ember-gallery, witness-well
        /// and cinder-sluice each ended with zero interior pieces — three of nine stages
        /// with no layout, produced by a rule that was protecting a corridor that could
        /// not exist.
        ///
        /// What a passable hazard does need is an ESCAPE gap: the vent erupts on a
        /// period, and a player standing in it must be able to leave. Two push radii is
        /// exactly the width of the walking actor, so the annulus outside the vent stays
        /// traversable at every bearing. That is a physical requirement with a derivation,
        /// where 150 was a borrowed constant.
        /// </summary>
        static float ClearanceFor(HazardKind kind)
            => kind == HazardKind.EmberVent
                ? 2f * CampaignSpec.PlayerPushRadius
                : GimmickClearance;

        /// <summary>How far a gimmick's keep-out reaches along a spine at height y.</summary>
        static float GapHalfWidthAt(in HazardConfig gimmick, float y, float stoneRadius)
        {
            float need = gimmick.Radius + stoneRadius + ClearanceFor(gimmick.Kind);
            float dy = gimmick.Y - y;
            float inside = need * need - dy * dy;
            return inside <= 0f ? 0f : MathF.Sqrt(inside);
        }

        /// <summary>
        /// True when a circular blocker of <paramref name="radius"/> at
        /// (<paramref name="x"/>, <paramref name="y"/>) keeps clear of every gimmick.
        /// </summary>
        public static bool ClearOfGimmicks(
            HazardConfig[] gimmicks, float x, float y, float radius)
        {
            for (int index = 0; index < gimmicks.Length; index += 1)
            {
                HazardConfig gimmick = gimmicks[index];
                if (!HasLocalFootprint(gimmick.Kind)) continue;
                float need = gimmick.Radius + radius + ClearanceFor(gimmick.Kind);
                float dx = gimmick.X - x;
                float dy = gimmick.Y - y;
                if (dx * dx + dy * dy < need * need) return false;
            }
            return true;
        }

        /// <summary>
        /// One spine: a horizontal run of capsules at <paramref name="y"/> spanning
        /// <paramref name="x0"/>..<paramref name="x1"/>, broken by a passage centred on
        /// each entry of <paramref name="passageCentres"/> (which must be ascending).
        /// The span is clamped so <see cref="EndOpening"/> of arena survives at each
        /// end whatever the caller asks for.
        /// </summary>
        public static void AddSpine(
            List<HazardConfig> into, float y, float x0, float x1, params float[] passageCentres)
            => AddSpine(into, System.Array.Empty<HazardConfig>(), y, x0, x1, passageCentres);

        /// <summary>
        /// Gimmick-aware spine. Every gimmick whose keep-out reaches this spine's line
        /// opens an EXTRA passage in front of it, sized to that gimmick rather than to
        /// the standard passage width.
        ///
        /// The first version of this file ignored gimmicks entirely and put a spine
        /// 112 px from a vent whose radius is 90 — under the 114 no-overlap floor, and
        /// worse, a wall standing on a damage telegraph. Turning the conflict into a
        /// doorway rather than refusing to build is what keeps the lane grammar intact:
        /// the gimmick becomes the thing guarding the gap, which is what a dungeon
        /// would do with it anyway.
        /// </summary>
        public static void AddSpine(
            List<HazardConfig> into,
            HazardConfig[] gimmicks,
            float y,
            float x0,
            float x1,
            params float[] passageCentres)
        {
            float halfWidth = HalfWidthAt(y);
            if (halfWidth <= 0f)
            {
                return;
            }

            float limit = halfWidth - EndOpening;
            if (limit <= 0f)
            {
                return;
            }

            float cursor = MathF.Max(x0, SimConfig.ArenaX - limit);
            float end = MathF.Min(x1, SimConfig.ArenaX + limit);
            if (end - cursor < MinSegment)
            {
                return;
            }

            // Authored passages plus one per gimmick standing on this spine's line,
            // as (start, end) intervals. Sorted and merged so overlapping keep-outs
            // become one wide doorway instead of a run of sub-MinSegment stubs.
            var gaps = new List<(float Start, float End)>(
                passageCentres.Length + gimmicks.Length);
            for (int index = 0; index < passageCentres.Length; index += 1)
            {
                gaps.Add((passageCentres[index] - PassageHalf,
                          passageCentres[index] + PassageHalf));
            }
            for (int index = 0; index < gimmicks.Length; index += 1)
            {
                HazardConfig gimmick = gimmicks[index];
                if (!HasLocalFootprint(gimmick.Kind)) continue;
                float gapHalf = GapHalfWidthAt(in gimmick, y, CampaignSpec.StoneWallRadius);
                if (gapHalf <= 0f) continue;
                gaps.Add((gimmick.X - gapHalf, gimmick.X + gapHalf));
            }
            gaps.Sort((left, right) => left.Start.CompareTo(right.Start));

            for (int index = 0; index < gaps.Count; index += 1)
            {
                float gapStart = gaps[index].Start;
                float gapEnd = gaps[index].End;
                if (gapStart - cursor >= MinSegment)
                {
                    into.Add(HazardConfig.StoneSpan(cursor, gapStart, y));
                }
                // Never walk the cursor backwards: an overlapping pair would otherwise
                // re-emit stone across the gap the earlier one just opened.
                cursor = MathF.Max(cursor, gapEnd);
            }

            if (end - cursor >= MinSegment)
            {
                into.Add(HazardConfig.StoneSpan(cursor, end, y));
            }
        }

        /// <summary>Free-standing cover: a circular blocker in a lane.</summary>
        public static HazardConfig Cover(float x, float y, float radius)
            => HazardConfig.Stone(x, y, 0f, 0f, radius);

        /// <summary>
        /// Adds a cover piece unless it would crowd a gimmick, in which case it is
        /// DROPPED rather than nudged. Nudging would need a search, and a search would
        /// make placement depend on iteration order — the layout is a fixed table on
        /// purpose. Losing one rock is cheaper than a telegraph nobody can read.
        /// </summary>
        static void AddCover(
            List<HazardConfig> into, HazardConfig[] gimmicks, float x, float y, float radius)
        {
            if (!ClearOfGimmicks(gimmicks, x, y, radius)) return;
            if (InsidePushBand(gimmicks, x, y, radius)) return;
            into.Add(Cover(x, y, radius));
        }

        /// <summary>
        /// Candidate cover lattice, in sim px offsets from the arena centre. Four rings
        /// of four, deliberately asymmetric in x so the result never reads as a grid.
        /// </summary>
        static readonly (float X, float Y)[] CoverLattice =
        {
            // Every point is pre-checked against the three static constraints —
            // outside SanctumRadius, inside RingStandoff, and inside the reachable
            // ellipse — so the runtime filters only ever have to judge the STAGE's
            // gimmicks. The feasible band is narrow: at dy 0 it is |dx| 240..585, and
            // it closes to 0..265 by dy +-260, which is why the outer columns pull in
            // as the rows move away from the waist.
            ( -245f,  -250f), ( -120f,  -250f), (  120f,  -250f), (  245f,  -250f),
            ( -440f,   -60f), ( -290f,   -60f), (  290f,   -60f), (  440f,   -60f),
            ( -440f,    60f), ( -290f,    60f), (  290f,    60f), (  440f,    60f),
            ( -245f,   250f), ( -120f,   250f), (  120f,   250f), (  245f,   250f),
        };
        /// <summary>Cover radii, cycled by lattice index. Three values, never one.</summary>
        static readonly float[] CoverRadii = { 46f, 40f, 44f, 38f };

        /// <summary>
        /// Iso radius of the central sanctum, which stays empty. It is the focal point,
        /// the boss floor, and the negative space the spec asks for after a combat peak
        /// (§3.5) — a rock in the middle of it is all three of those things spoiled.
        /// </summary>
        public const float SanctumRadius = 240f;

        /// <summary>
        /// Ellipse parameter beyond which no cover is placed.
        ///
        /// The layout has no visibility of the View's boundary-ring modules, so this is
        /// the only thing keeping cover off them. 0.85 was not enough — a cover at
        /// (228, 544) measured 29 px from a ring wall at (205, 526) against a required
        /// 96. The outermost COLUMN was pulled in instead (|dx| 540 -> 440), which
        /// clears it at 124 px; dropping the standoff to 0.78 also worked but moved
        /// every row and sent ash-march's stall rate to 90.71%. The lattice is far
        /// more sensitive to where cover sits than to how much of it there is.
        /// </summary>
        public const float RingStandoff = 0.85f;

        /// <summary>
        /// The stage's layout blockers, appended after its gimmicks.
        ///
        /// GENERATED from the stage's own gimmicks, not hand-tabulated per stage, and
        /// that is a reversal. The first design was a per-stage table of lane spines;
        /// it does not survive contact with this game's gimmick density. A vent has
        /// radius 90, so with a 150 px corridor floor it sterilises roughly 470 px of
        /// any wall line within 264 px of it. The playfield is 1470 wide, the spine
        /// ends must stay 300 px off the boundary at each side, and the gimmicks are
        /// contractually anchored near the arena centre where the spines wanted to run.
        /// Two vents and a wall line simply cannot share a stage. Measured across the
        /// six stages, the authored spines survived as 0-2 segments TOTAL.
        ///
        /// Cover pieces do fit: at radius 40 the same vent needs 280 px, which a
        /// 1470x780 ellipse has plenty of. So the interior is built from the thing that
        /// fits, filtered against the stage's actual gimmicks, and a spine appears only
        /// where one genuinely survives.
        ///
        /// Still a pure function of (gimmicks, stageId) — no RNG, no iteration-order
        /// dependence — so it is as reproducible as the table it replaces.
        /// </summary>
        public static HazardConfig[] For(HazardConfig[] gimmicks, string stageId)
        {
            var blockers = new List<HazardConfig>(20);

            // A spine is attempted only on stages whose gimmicks leave room for one.
            // Both survivors are stages with a single sparse hazard band.
            switch (stageId)
            {
                case CampaignStages.CinderSpan:
                    AddSpine(blockers, gimmicks, SpineNorthY, 368f, 1168f, 768f);
                    break;
                case CampaignStages.AshMarch:
                    AddSpine(blockers, gimmicks, SpineNorthY, 418f, 1118f, 768f);
                    break;
            }

            for (int index = 0; index < CoverLattice.Length; index += 1)
            {
                float x = SimConfig.ArenaX + CoverLattice[index].X;
                float y = SimConfig.ArenaY + CoverLattice[index].Y;
                float radius = CoverRadii[index % CoverRadii.Length];

                // Inside the sanctum: skip. Outside the reachable ellipse: skip.
                float isoX = x - SimConfig.ArenaX;
                float isoY = (y - SimConfig.ArenaY) * SimConfig.IsoY;
                if (MathF.Sqrt(isoX * isoX + isoY * isoY) < SanctumRadius) continue;

                // Stay well inside the boundary ring, not merely inside the clamp. The
                // View builds its ring wall just past the stop line, and a cover placed
                // at e 0.94 measured 68 px from a ring module against a required 96 —
                // the layout has no visibility of those modules, so the margin has to
                // come from here. 0.85 leaves ~110 px of ellipse beyond the outermost
                // cover at every bearing.
                float unitX = (x - SimConfig.ArenaX) / (DungeonBoundsSpec.ExpandedHalfWidth - radius);
                float unitY = (y - SimConfig.ArenaY) / (DungeonBoundsSpec.ExpandedHalfHeight - radius);
                if (unitX * unitX + unitY * unitY > RingStandoff * RingStandoff) continue;

                AddCover(blockers, gimmicks, x, y, radius);
            }

            return WithoutPinchPoints(gimmicks, blockers);
        }

        /// <summary>
        /// True when a cover would stand inside a tide-current push band.
        ///
        /// Bands are excluded from the clearance test because they have no local
        /// footprint to be "too close to" — but a SOLID inside one is a different
        /// problem: the current shoves actors against it and they have nowhere to go.
        /// cinder-sluice, the only stage with currents, was the one stage still over
        /// the stall gate (3.10%) with six covers, two of them sitting in a band.
        /// The rect test mirrors the sim's own band test, which is deliberately NOT
        /// iso-weighted.
        /// </summary>
        static bool InsidePushBand(HazardConfig[] gimmicks, float x, float y, float radius)
        {
            for (int index = 0; index < gimmicks.Length; index += 1)
            {
                HazardConfig gimmick = gimmicks[index];
                if (gimmick.Kind != HazardKind.TideCurrent) continue;
                if (MathF.Abs(x - gimmick.X) <= gimmick.HalfW + radius
                 && MathF.Abs(y - gimmick.Y) <= gimmick.HalfH + radius)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Distance from a point to a segment given by centre +- half-vector.</summary>
        static float PointToSegment(
            float px, float py, float cx, float cy, float halfX, float halfY)
        {
            float lengthSq = halfX * halfX + halfY * halfY;
            float t = lengthSq > 0f
                ? ((px - cx) * halfX + (py - cy) * halfY) / lengthSq
                : 0f;
            if (t > 1f) t = 1f;
            else if (t < -1f) t = -1f;
            float dx = px - (cx + t * halfX);
            float dy = py - (cy + t * halfY);
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Surface-to-surface gap between two blockers, treating each as a capsule.
        /// Zero or negative means they touch or overlap, which is SAFE — two solids
        /// that meet are one bigger solid, and a pack walks around the union.
        ///
        /// Both segments here are horizontal or degenerate, so the closest pair is
        /// always realised at an endpoint; four point-to-segment probes cover it
        /// without a full segment-segment solve.
        /// </summary>
        static float SurfaceGap(in HazardConfig a, in HazardConfig b)
        {
            float aHalfX = a.Kind == HazardKind.StoneWall ? a.HalfW : 0f;
            float aHalfY = a.Kind == HazardKind.StoneWall ? a.HalfH : 0f;
            float bHalfX = b.Kind == HazardKind.StoneWall ? b.HalfW : 0f;
            float bHalfY = b.Kind == HazardKind.StoneWall ? b.HalfH : 0f;

            float best = PointToSegment(a.X + aHalfX, a.Y + aHalfY, b.X, b.Y, bHalfX, bHalfY);
            best = MathF.Min(best,
                PointToSegment(a.X - aHalfX, a.Y - aHalfY, b.X, b.Y, bHalfX, bHalfY));
            best = MathF.Min(best,
                PointToSegment(b.X + bHalfX, b.Y + bHalfY, a.X, a.Y, aHalfX, aHalfY));
            best = MathF.Min(best,
                PointToSegment(b.X - bHalfX, b.Y - bHalfY, a.X, a.Y, aHalfX, aHalfY));
            return best - a.Radius - b.Radius;
        }

        /// <summary>
        /// Drops any layout piece that would leave a corridor narrower than the pair
        /// requires, against a gimmick or an already-accepted piece.
        ///
        /// Gimmicks are immovable — their coordinates are contracts — so the layout is
        /// always the side that yields. Acceptance is in table order, which keeps the
        /// result a pure function of the table: no search, no iteration-order
        /// dependence, nothing to re-derive when a stage is edited.
        ///
        /// AMENDMENT #17b: the threshold against a gimmick comes from
        /// <see cref="ClearanceFor"/>, not from <see cref="MinCorridor"/> directly.
        /// This routine held the SECOND copy of the "how far from a gimmick" rule, and
        /// the two copies disagreed the moment the first one learned that a passable
        /// hazard is not a wall — ember-gallery's lattice passed the clearance test with
        /// four pieces and then lost all four here, to a corridor requirement measured
        /// against vents you can walk over. Sharing the function is what stops the pair
        /// from drifting again (CLAUDE.md §4i); piece-to-piece stays MinCorridor,
        /// because two layout pieces are both solid and genuinely can pinch.
        /// </summary>
        static HazardConfig[] WithoutPinchPoints(HazardConfig[] gimmicks, List<HazardConfig> layout)
        {
            var accepted = new List<HazardConfig>(layout.Count);
            foreach (HazardConfig piece in layout)
            {
                bool pinches = false;
                foreach (HazardConfig gimmick in gimmicks)
                {
                    if (!HasLocalFootprint(gimmick.Kind)) continue;
                    float gap = SurfaceGap(in piece, in gimmick);
                    if (gap > 0f && gap < ClearanceFor(gimmick.Kind)) { pinches = true; break; }
                }
                if (!pinches)
                {
                    foreach (HazardConfig other in accepted)
                    {
                        float gap = SurfaceGap(in piece, in other);
                        if (gap > 0f && gap < MinCorridor) { pinches = true; break; }
                    }
                }
                if (!pinches) accepted.Add(piece);
            }
            return accepted.ToArray();
        }

        /// <summary>
        /// Gimmicks first, layout second. Order is the serialized identity of every
        /// hazard index — the steering commitment, the per-hazard runtime array and
        /// the View's draw order all key off it — so appending is the only safe way to
        /// add blockers to a table that already ships.
        /// </summary>
        public static HazardConfig[] Compose(HazardConfig[] gimmicks, string stageId)
        {
            HazardConfig[] layout = For(gimmicks, stageId);
            if (layout.Length == 0)
            {
                return gimmicks;
            }
            var composed = new HazardConfig[gimmicks.Length + layout.Length];
            Array.Copy(gimmicks, composed, gimmicks.Length);
            Array.Copy(layout, 0, composed, gimmicks.Length, layout.Length);
            return composed;
        }
    }
}
