using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    /// <summary>
    /// Editor-only dev conveniences (gimmick-retune-spec §R4). The new dungeons
    /// sit at the END of the unlock chain, so an editor Play session on a fresh
    /// PlayerPrefs save can never reach them — this seeds a save with the six
    /// original stages cleared and maxed meta so cinder-sluice/ember-bastion/
    /// ash-march are immediately playable. Editor assembly only; never ships.
    /// </summary>
    public static class DevUnlockMenu
    {
        // Mirrors CampaignStore.Save output (view-owned schema; keep in sync).
        const string Key = "abyssal-lantern:unity:campaign";

        [MenuItem("CinderCourt/Dev/Unlock All Stages (maxed meta)")]
        public static void UnlockAll()
        {
            var json = "{\"clearedMask\":63,\"equipment\":{\"weapon\":5,\"lantern\":5,\"cloak\":5},"
                + "\"stats\":{\"attack\":10,\"vitality\":10,\"swiftness\":10,\"points\":0},"
                + "\"relics\":30,\"roster\":[\"ember-cohort\",\"shade-echo\"],"
                + "\"active\":\"ember-cohort\",\"prologueDone\":true}";
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
            Debug.Log("DevUnlockMenu: campaign save seeded — six stages cleared, "
                + "new dungeon chain (cinder-sluice → ember-bastion → ash-march) unlocked.");
        }

        [MenuItem("CinderCourt/Dev/Reset Campaign Save")]
        public static void ResetSave()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Debug.Log("DevUnlockMenu: campaign save deleted — fresh-player state.");
        }
    }
}
