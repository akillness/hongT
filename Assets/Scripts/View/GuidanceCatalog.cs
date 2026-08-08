// AMENDMENT #9 — in-game guidance (design/ingame-guidance-spec.md).
//
// The 23 things a first-time player does not know, and the two tiers they are
// delivered in. Every number in the copy below is READ FROM THE SIM CONSTANT,
// never retyped: a balance change moves the lesson with it, and a lesson that
// drifts from the sim is worse than no lesson.
//
// Two tiers, from negotiation entry 13:
//   Pause (8)  — six gimmicks + win + lose. These kill you if you do not know
//                them, and the survey found the genre leaves exactly this gap
//                (G6 hazard-only guidance: 0 of 7).
//   Toast (15) — controls, pickups, surges. Read them or don't.
//
// The pause count is DELIBERATELY outside the genre band and is recorded as
// such: surveyed titles pause a median of 0 times, confirmed max 1, and the one
// title that spread pauses across a run (Darkest Dungeon 2) drew the sample's
// worst onboarding reception. What the survey actually found is that the
// variable is not the count but whether the player can refuse — Returnal pauses
// zero times and was still panned for over-explaining, while Into the Breach
// pauses and draws no complaints at all. So every pause card here is dismissed
// by any key or tap, and each is capped at 33 words (entry 13).
using CinderCourt.Sim;

namespace CinderCourt.View
{
    /// <summary>How a guidance entry reaches the player.</summary>
    public enum GuidanceTier
    {
        /// <summary>Freezes the run until dismissed. Eight of these, total.</summary>
        Pause = 0,
        /// <summary>Non-blocking line on the existing toast surface.</summary>
        Toast = 1,
    }

    /// <summary>What kind of thing an entry teaches — the codex's grouping.</summary>
    public enum GuidanceGroup
    {
        Control = 0,
        Pickup = 1,
        Hazard = 2,
        Outcome = 3,
        Surge = 4,
    }

    /// <summary>One thing the player has to learn, once.</summary>
    public readonly struct GuidanceEntry
    {
        /// <summary>Bit index in <see cref="CampaignData.GuidanceSeen"/>. Stable
        /// forever: appending is safe, reordering silently re-teaches everything.</summary>
        public readonly int Bit;
        public readonly GuidanceGroup Group;
        public readonly GuidanceTier Tier;
        /// <summary>Short name — codex row, and the pause card's heading.</summary>
        public readonly string Title;
        /// <summary>Desktop body. <= 33 words (entry 13 cap).</summary>
        public readonly string Body;
        /// <summary>Touch body, or null when the copy has no input words in it.</summary>
        public readonly string TouchBody;

        public GuidanceEntry(int bit, GuidanceGroup group, GuidanceTier tier,
                             string title, string body, string touchBody = null)
        {
            Bit = bit;
            Group = group;
            Tier = tier;
            Title = title;
            Body = body;
            TouchBody = touchBody;
        }

        /// <summary>The body for the active input mode.</summary>
        public string BodyFor(bool touch)
            => touch && TouchBody != null ? TouchBody : Body;
    }

    public static class GuidanceCatalog
    {
        // Numbers pulled from the sim so the copy cannot drift. Composed once at
        // type-init; the arrays below are static readonly, so this costs nothing
        // per frame.
        const string Dash = "SHIFT";

        /// <summary>
        /// Every entry, in bit order. Bit index IS the array index — pinned by a
        /// test, because a mismatch would mark the wrong lesson as seen.
        /// </summary>
        public static readonly GuidanceEntry[] Entries = BuildEntries();

        /// <summary>
        /// Bits needed. 23 today; the ceiling is 31, NOT 32.
        ///
        /// Bit 31 is the sign bit, and CampaignStore serialises the field with
        /// Append(int) but parses it with ExtractInt, which consumes digits
        /// only — a leading '-' ends the loop immediately and yields 0. Setting
        /// bit 31 would therefore make the whole field read back as zero on the
        /// next load and silently re-teach all 23 lessons. Found by the test
        /// lane; a test pins the ceiling.
        /// </summary>
        public static int Count => Entries.Length;

        /// <summary>Highest usable bit index + 1. See <see cref="Count"/> for
        /// why this is 31 and not 32.</summary>
        public const int BitCeiling = 31;

        /// <summary>Entries that freeze the run. Exactly 8 (entry 13).</summary>
        public const int PauseBudget = 8;
        /// <summary>Word cap for one pause card (entry 13: ItB's whole tutorial
        /// is ~120 s; 8 cards at 3.3 words/s of reading gives 33).</summary>
        public const int PauseWordCap = 33;

        static GuidanceEntry[] BuildEntries()
        {
            var e = new System.Collections.Generic.List<GuidanceEntry>(24);
            var bit = 0;

            // ---------------------------------------------------- controls --
            // Toast tier. A control the player already pressed needs confirming,
            // not explaining, so these stay out of the way (entry 13).
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "이동", "W A S D 또는 방향키로 걷는다.",
                "왼쪽 조이스틱을 끌어 걷는다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "연격", $"Space를 이어 치면 {HackSpec.ComboLength}타. 마지막 타가 가장 세다.",
                $"타격 버튼을 이어 누르면 {HackSpec.ComboLength}타. 마지막 타가 가장 세다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "질주", $"{Dash} — 기름 {HackSpec.DashCost:F0}, 재사용 {HackSpec.DashCooldownSeconds:0.#}초. 무적은 없다, 거리만 번다.",
                $"질주 버튼 — 기름 {HackSpec.DashCost:F0}, 재사용 {HackSpec.DashCooldownSeconds:0.#}초. 무적은 없다, 거리만 번다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "균열 화살", $"Q — 기름 {HackSpec.BoltCost:F0}, 재사용 {HackSpec.BoltCooldown:0.#}초. 관통한다.",
                $"Q 카드 — 기름 {HackSpec.BoltCost:F0}, 재사용 {HackSpec.BoltCooldown:0.#}초. 관통한다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "묘지 파동", $"E — 기름 {HackSpec.PulseCost:F0}, 재사용 {HackSpec.PulseCooldown:0.#}초. 반경 {HackSpec.PulseRadius:F0} 전방위.",
                $"E 카드 — 기름 {HackSpec.PulseCost:F0}, 재사용 {HackSpec.PulseCooldown:0.#}초. 반경 {HackSpec.PulseRadius:F0} 전방위."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "잿불 노바", $"R — 기름 {HackSpec.AshNovaCost:F0}, 재사용 {HackSpec.AshNovaCooldown:0.#}초. 반경 {HackSpec.AshNovaRadius:F0}.",
                $"R 카드 — 기름 {HackSpec.AshNovaCost:F0}, 재사용 {HackSpec.AshNovaCooldown:0.#}초. 반경 {HackSpec.AshNovaRadius:F0}."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "공허 방패", $"F — 기름 {HackSpec.AegisCost:F0}. 피해 {HackSpec.AegisShield:F0}을 {HackSpec.AegisDuration:0.#}초간 흡수한다.",
                $"F 카드 — 기름 {HackSpec.AegisCost:F0}. 피해 {HackSpec.AegisShield:F0}을 {HackSpec.AegisDuration:0.#}초간 흡수한다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "동료 대기", "G — 동료를 그 자리에 세운다. 길목을 막게 한다.",
                "동료 대기 — 동료를 그 자리에 세운다. 길목을 막게 한다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Control, GuidanceTier.Toast,
                "동료 호출", "H — 동료를 곁으로 부른다.",
                "동료 호출 — 동료를 곁으로 부른다."));

            // ----------------------------------------------------- pickups --
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Pickup, GuidanceTier.Toast,
                "잿불 조각", $"체력 +{SimConfig.EmberShardHeal:F0}. 밟으면 줍는다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Pickup, GuidanceTier.Toast,
                "기름 플라스크", $"기름 +{SimConfig.OilFlaskCharge:F0}. 스킬이 곧 기름이다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Pickup, GuidanceTier.Toast,
                "유물 파편", $"점수 +{SimConfig.RelicScore}. 강하가 끝나면 성소 통화가 된다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Pickup, GuidanceTier.Toast,
                "장비 파편", "장비 등급이 이 강하 동안 오른다. 로비 등급은 그대로다."));

            // ----------------------------------------------------- hazards --
            // Pause tier. The survey's strongest gap (G6: 0 of 7 surveyed titles
            // document hazards separately) — because their hazards are spikes
            // and acid, self-evident from the art. A pylon that shields enemies
            // and a wall that grinds across the floor are not.
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Hazard, GuidanceTier.Pause,
                "분출구", $"{CampaignSpec.VentPeriod:0.#}초마다 뿜는다. {CampaignSpec.VentTelegraph:0.#}초 전에 링이 뜬다 — 그때 나가라. 반경 {CampaignSpec.VentRadius:F0}, 피해 {CampaignSpec.VentDamage:F0}."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Hazard, GuidanceTier.Pause,
                "흑요석 기둥", $"부술 수 없다. 시야와 직선을 막는다. 반경 {CampaignSpec.PillarRadius:F0} — 뒤에 숨을 수도, 갇힐 수도 있다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Hazard, GuidanceTier.Pause,
                "제단", $"{CampaignSpec.AltarHoldSeconds:0.#}초 서 있으면 기름 +{CampaignSpec.AltarOilBurst:F0}. 그동안 움직일 수 없다. 재사용 {CampaignSpec.AltarCooldown:F0}초."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Hazard, GuidanceTier.Pause,
                "해류", $"{CampaignSpec.CurrentPush:F0}만큼 민다. 걸음이 {SimConfig.PlayerSpeed:F0}이니 역류로는 거의 못 간다. 순류를 타라."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Hazard, GuidanceTier.Pause,
                "방벽주", $"반경 {CampaignSpec.PylonAuraRadius:F0} 안의 적이 받는 피해를 {(1f - CampaignSpec.PylonAuraDamageTakenMult) * 100f:F0}% 깎는다. 체력 {CampaignSpec.PylonHp:F0} — 먼저 부숴라."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Hazard, GuidanceTier.Pause,
                "재의 장벽", $"양끝에서 밀려와 중앙을 지난다. 닿으면 {CampaignSpec.WallTickPeriod:0.#}초마다 {CampaignSpec.WallTickDamage:F0}. 예고 뒤 {CampaignSpec.WallPeriod:F0}초 주기."));

            // ---------------------------------------------------- outcomes --
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Outcome, GuidanceTier.Pause,
                "승리 조건", "웨이브를 전부 비우면 보스가 온다. 보스를 쓰러뜨리면 구역이 정화되고 유물이 성소로 들어간다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Outcome, GuidanceTier.Pause,
                "패배 조건", "체력이 0이면 강하가 끝난다. 그때까지 모은 유물은 남는다 — 클리어 기록만 없다."));

            // ------------------------------------------------------ surges --
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Surge, GuidanceTier.Toast,
                "위기", $"체력이 {HackSpec.PerilHealthFraction * 100f:F0}% 아래로 떨어지면 {HackSpec.PerilSeconds:0.#}초간 열린다. 각인이 있으면 그때 발동한다."));
            e.Add(new GuidanceEntry(bit++, GuidanceGroup.Surge, GuidanceTier.Toast,
                "기세", $"{HackSpec.SurgeKillInterval}처치마다 {HackSpec.SurgeSeconds:0.#}초간 열린다. 웨이브당 {HackSpec.SurgeWaveCap}회."));

            return e.ToArray();
        }

        // ------------------------------------------------------------ bits --
        /// <summary>Has this entry already been shown?</summary>
        public static bool Seen(in CampaignData data, int bit)
            => bit >= 0 && bit < BitCeiling && (data.GuidanceSeen & (1 << bit)) != 0;

        /// <summary>Marks an entry shown. Returns true when this was the first
        /// time — the caller only saves on a change.</summary>
        public static bool MarkSeen(ref CampaignData data, int bit)
        {
            if (bit < 0 || bit >= BitCeiling) return false;
            var mask = 1 << bit;
            if ((data.GuidanceSeen & mask) != 0) return false;
            data.GuidanceSeen |= mask;
            return true;
        }

        /// <summary>How many entries the player has met. Codex header.</summary>
        public static int SeenCount(in CampaignData data)
        {
            var n = 0;
            for (var i = 0; i < Entries.Length; i++)
                if (Seen(in data, Entries[i].Bit)) n++;
            return n;
        }

        /// <summary>
        /// Entries in one codex group, in bit order. Allocates — call it once
        /// per codex build, never per frame. The codex is the only caller and
        /// it builds on open.
        /// </summary>
        public static GuidanceEntry[] ByGroup(GuidanceGroup group)
        {
            var n = 0;
            for (var i = 0; i < Entries.Length; i++)
                if (Entries[i].Group == group) n++;
            var outp = new GuidanceEntry[n];
            var w = 0;
            for (var i = 0; i < Entries.Length; i++)
                if (Entries[i].Group == group) outp[w++] = Entries[i];
            return outp;
        }

        /// <summary>Every group, in display order. Hazards first: they are the
        /// ones that kill a player who does not know them.</summary>
        public static readonly GuidanceGroup[] GroupOrder =
        {
            GuidanceGroup.Hazard,
            GuidanceGroup.Control,
            GuidanceGroup.Pickup,
            GuidanceGroup.Surge,
            GuidanceGroup.Outcome,
        };

        /// <summary>Group heading. Korean, because every other codex string is.</summary>
        public static string GroupTitle(GuidanceGroup group)
        {
            switch (group)
            {
                case GuidanceGroup.Hazard:  return "위험";
                case GuidanceGroup.Control: return "조작";
                case GuidanceGroup.Pickup:  return "습득";
                case GuidanceGroup.Surge:   return "기세";
                default:                    return "결과";
            }
        }

        /// <summary>Icon for a group heading. Borrowed from the existing set —
        /// no glyph exists for "hazard" or "outcome", and inventing three
        /// icons is a bigger change than reusing three that read correctly.</summary>
        public static string GroupIcon(GuidanceGroup group)
        {
            switch (group)
            {
                case GuidanceGroup.Hazard:  return "skill-nova";
                case GuidanceGroup.Control: return "skill-dash";
                case GuidanceGroup.Pickup:  return "pickup-ember";
                case GuidanceGroup.Surge:   return "skill-aegis";
                default:                    return "pickup-relic";
            }
        }

        // -------------------------------------------------------- triggers --
        /// <summary>
        /// The entry a hazard kind teaches. Hazards are the pause tier's whole
        /// reason to exist, so this mapping is the one that must not drift —
        /// a test walks HazardKind and asserts every value resolves.
        /// </summary>
        public static int BitForHazard(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.EmberVent: return IndexOf("분출구");
                case HazardKind.ObsidianPillar: return IndexOf("흑요석 기둥");
                case HazardKind.RelicAltar: return IndexOf("제단");
                case HazardKind.TideCurrent: return IndexOf("해류");
                case HazardKind.EmberPylon: return IndexOf("방벽주");
                case HazardKind.AshWall: return IndexOf("재의 장벽");
                default: return -1;
            }
        }

        /// <summary>The entry a pickup teaches.</summary>
        public static int BitForPickup(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.EmberShard: return IndexOf("잿불 조각");
                case PickupKind.OilFlask: return IndexOf("기름 플라스크");
                case PickupKind.RelicMote: return IndexOf("유물 파편");
                case PickupKind.EquipShard: return IndexOf("장비 파편");
                default: return -1;
            }
        }

        /// <summary>Bit for a title. Linear over 23 entries, called on discrete
        /// events (a hazard first entering the stage table, a pickup first
        /// collected) — never per frame.</summary>
        public static int IndexOf(string title)
        {
            for (var i = 0; i < Entries.Length; i++)
                if (string.Equals(Entries[i].Title, title, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }

        /// <summary>Well-known bits the director triggers directly.</summary>
        public static int VictoryBit => IndexOf("승리 조건");
        public static int DefeatBit => IndexOf("패배 조건");
        public static int PerilBit => IndexOf("위기");
        public static int SurgeBit => IndexOf("기세");

        /// <summary>Control entries in row order — the prologue walks these.</summary>
        public static int FirstControlBit => IndexOf("이동");
    }
}
