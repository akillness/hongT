using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>Canonical character import/prefab roster IDs.</summary>
    public static class CharacterRoster
    {
        private static readonly string[] AllIds =
        {
            "guard",
            "lantern-reaver",
            "ember-cohort",
            "scout",
            "shade",
            "possessed",
            "shadow-commander-boss",
            "broken-court-monarch-boss",
        };

        /// <summary>Character IDs in import/prefab order.</summary>
        public static IReadOnlyList<string> Ids => AllIds;
    }
}
