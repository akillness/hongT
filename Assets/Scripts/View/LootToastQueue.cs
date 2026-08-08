// Loot pickup toast model — WHAT the acquisition popup shows and for how long,
// never HOW it is drawn.
//
// Pure logic, no UnityEngine — same contract as CommandConsoleBuffer and
// CampaignMapLayout: every stacking, eviction and fade rule is assertable in
// EditMode without building a canvas. HudView owns the widgets and does nothing
// but read this model, so the renderer has no timing decision of its own.
//
// Allocation contract: the slot ring is allocated once at construction and
// never grows. Push/Tick/AlphaAt touch only value-typed fields, so a run that
// collects loot every frame allocates nothing here.
using CinderCourt.Sim;

namespace CinderCourt.View
{
    /// <summary>Which pickup the toast is announcing. Mirrors
    /// <see cref="PickupKind"/> one-for-one; kept separate so the presentation
    /// layer can name its own rows without the sim enum leaking into label
    /// tables that only the HUD understands.</summary>
    public enum LootToastKind
    {
        Shard = 0,
        Flask = 1,
        Relic = 2,
        Equip = 3,
    }

    /// <summary>One live toast row. Value type: the queue stores these inline,
    /// so reading a slot never allocates.</summary>
    public readonly struct LootToastSlot
    {
        public readonly LootToastKind Kind;
        public readonly LootGrade Grade;
        /// <summary>How many identical pickups this row has absorbed (>= 1).</summary>
        public readonly int Count;
        /// <summary>Seconds since the row was created or last re-stamped.</summary>
        public readonly float Age;

        public LootToastSlot(LootToastKind kind, LootGrade grade, int count, float age)
        {
            Kind = kind;
            Grade = grade;
            Count = count;
            Age = age;
        }
    }

    public sealed class LootToastQueue
    {
        /// <summary>Rows on screen at once. Four is the measured ceiling for the
        /// left-centre column: a fifth row at the 40 u pitch would reach y -94,
        /// inside the 260 u touch joystick catch box at the bottom-left corner
        /// (HudView.BuildTouchControls).</summary>
        public const int Capacity = 4;

        /// <summary>Ramp-in, full-opacity hold, and fade-out, in seconds. The
        /// hold is deliberately shorter than the 12 s pickup lifetime: the toast
        /// reports an event, it is not a persistent inventory readout.</summary>
        public const float RiseSeconds = 0.12f;
        public const float HoldSeconds = 1.5f;
        public const float FadeSeconds = 0.5f;
        public const float LifeSeconds = RiseSeconds + HoldSeconds + FadeSeconds;

        readonly LootToastKind[] _kind = new LootToastKind[Capacity];
        readonly LootGrade[] _grade = new LootGrade[Capacity];
        readonly int[] _count = new int[Capacity];
        readonly float[] _age = new float[Capacity];
        int _live;

        /// <summary>Reduced motion: rows appear and vanish at full opacity with
        /// no ramp, and hold for the same total time. Set from
        /// <see cref="ViewPrefs.ReducedMotion"/> by the renderer — this type
        /// never reads a pref itself so the rules stay testable.</summary>
        public bool Instant { get; set; }

        /// <summary>Live rows, newest first.</summary>
        public int Count => _live;

        /// <summary>Bumped whenever the visible text of any row changes (a new
        /// row, a stack increment, or an eviction). The renderer rebuilds its
        /// label strings only when this moves, so a steady-state fade costs no
        /// string allocation.</summary>
        public uint Revision { get; private set; }

        /// <summary>Row <paramref name="index"/>, 0 = newest.</summary>
        public LootToastSlot SlotAt(int index)
        {
            if (index < 0 || index >= _live) return default;
            return new LootToastSlot(_kind[index], _grade[index], _count[index], _age[index]);
        }

        /// <summary>Opacity of row <paramref name="index"/> in [0,1]. Rows past
        /// the live count report 0 so the renderer can hide them unconditionally.</summary>
        public float AlphaAt(int index)
        {
            if (index < 0 || index >= _live) return 0f;
            if (Instant) return 1f;
            var age = _age[index];
            if (age < RiseSeconds) return age / RiseSeconds;
            var fadeStart = RiseSeconds + HoldSeconds;
            if (age < fadeStart) return 1f;
            var fade = 1f - (age - fadeStart) / FadeSeconds;
            return fade < 0f ? 0f : fade;
        }

        /// <summary>Announces one collected pickup.
        ///
        /// Consecutive identical (kind, grade) pickups fold into the newest row
        /// and re-stamp its age instead of pushing a second row: a magnet sweep
        /// through four shards must read as "shards x4", not as four rows that
        /// evict every other kind the player just picked up. A different kind or
        /// grade always takes its own row, pushing older rows down and dropping
        /// the oldest once <see cref="Capacity"/> is reached.</summary>
        public void Push(LootToastKind kind, LootGrade grade)
        {
            if (_live > 0 && _kind[0] == kind && _grade[0] == grade)
            {
                _count[0] += 1;
                _age[0] = 0f;
                Revision += 1;
                return;
            }

            var shifted = _live < Capacity ? _live : Capacity - 1;
            for (var i = shifted; i > 0; i--)
            {
                _kind[i] = _kind[i - 1];
                _grade[i] = _grade[i - 1];
                _count[i] = _count[i - 1];
                _age[i] = _age[i - 1];
            }
            _kind[0] = kind;
            _grade[0] = grade;
            _count[0] = 1;
            _age[0] = 0f;
            if (_live < Capacity) _live += 1;
            Revision += 1;
        }

        /// <summary>Ages every live row and retires the expired ones.
        ///
        /// Age is monotonically non-decreasing with index — a push only ever
        /// inserts at 0 or re-stamps 0 — so expiry is always a tail truncation
        /// and rows can never reorder underneath the renderer's widgets.</summary>
        public void Tick(float deltaTime)
        {
            if (_live == 0 || deltaTime <= 0f) return;
            for (var i = 0; i < _live; i++) _age[i] += deltaTime;
            var kept = _live;
            while (kept > 0 && _age[kept - 1] >= LifeSeconds) kept -= 1;
            if (kept == _live) return;
            _live = kept;
            Revision += 1;
        }

        /// <summary>Drops every row without a fade. Called on run start/retry so
        /// a new run never inherits the previous run's loot.</summary>
        public void Clear()
        {
            if (_live == 0) return;
            _live = 0;
            Revision += 1;
        }

        /// <summary>Sim pickup kind -> toast row kind. Total by construction:
        /// every <see cref="PickupKind"/> has exactly one row identity.</summary>
        public static LootToastKind KindOf(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.OilFlask: return LootToastKind.Flask;
                case PickupKind.RelicMote: return LootToastKind.Relic;
                case PickupKind.EquipShard: return LootToastKind.Equip;
                default: return LootToastKind.Shard;
            }
        }
    }
}
