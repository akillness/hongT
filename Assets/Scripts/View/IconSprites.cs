using UnityEngine;

namespace CinderCourt.View
{
    /// <summary>The one icon lookup chain: regenerated > generated > root.
    ///
    /// Before 2026-08-10 only HudView's ember-rest panel walked this chain;
    /// the other 16 load sites across five files hit "Icons/{key}" directly,
    /// so the same key could render TWO different arts depending on which
    /// screen asked (the regenerated set shadows 17 root files for chain
    /// readers only). One loader makes "which art does this key mean" a
    /// single fact — and only once every direct reader is gone can the
    /// shadowed root duplicates be deleted (their pixel diff must be judged
    /// first, per the §4p LFS-compare rule).
    ///
    /// Null return is the caller's business: every site already guards it
    /// (absent-asset contract, VfxDirector header).</summary>
    public static class IconSprites
    {
        public static Sprite Load(string iconKey)
        {
            var sprite = Resources.Load<Sprite>("Icons/regenerated/" + iconKey);
            if (sprite == null) sprite = Resources.Load<Sprite>("Icons/generated/" + iconKey);
            if (sprite == null) sprite = Resources.Load<Sprite>("Icons/" + iconKey);
            return sprite;
        }
    }
}
