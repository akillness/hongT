// The single scene component. Assembles the whole runtime object graph in
// Awake and hands control to GameDirector (v0.2 single-scene state machine).
// Boot routing, persistence, and mode rules live in GameDirector —
// this class only loads assets and wires components.
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        public GameObject PlayerPrefab { get; private set; }

        readonly Dictionary<EnemyVisual, GameObject> _enemyPrefabs =
            new Dictionary<EnemyVisual, GameObject>(6);

        void Awake()
        {
            Application.targetFrameRate = -1;   // browser vsync owns pacing

            // W13 (seed §8, 2026-08-07): the player is human-command-boss,
            // superseding the 08-04 lantern-reaver decision. Falls back to the
            // lantern-reaver prefab until the imported FBX has been built into
            // a Humanoid prefab (CharacterImportPipeline.ImportAll).
            PlayerPrefab = Resources.Load<GameObject>("Characters/human-command-boss");
            if (PlayerPrefab == null)
                PlayerPrefab = Resources.Load<GameObject>("Characters/lantern-reaver");
            LoadEnemy(EnemyVisual.EmberCohort, "Characters/ember-cohort");
            LoadEnemy(EnemyVisual.Scout, "Characters/scout");
            LoadEnemy(EnemyVisual.Shade, "Characters/shade");
            LoadEnemy(EnemyVisual.Possessed, "Characters/possessed");
            LoadEnemy(EnemyVisual.BossCommander, "Characters/shadow-commander-boss");
            LoadEnemy(EnemyVisual.BossMonarch, "Characters/broken-court-monarch-boss");

            var input = gameObject.AddComponent<InputAdapter>();
            var audio = gameObject.AddComponent<AudioDirector>();
            var vfx = gameObject.AddComponent<VfxDirector>();
            var rig = gameObject.AddComponent<CameraRig>();

            var hud = gameObject.AddComponent<HudView>();
            hud.Input = input;
            hud.Audio = audio;
            hud.Build();

            var game = gameObject.AddComponent<GameView>();
            game.Input = input;
            game.Hud = hud;
            game.Audio = audio;
            game.Vfx = vfx;
            game.Rig = rig;
            game.Bootstrap = this;

            var lobbyObject = new GameObject("Lobby");
            lobbyObject.transform.SetParent(transform, false);
            var lobby = lobbyObject.AddComponent<LobbyView>();

            var stagingObject = new GameObject("LobbyStaging");
            stagingObject.transform.SetParent(transform, false);
            var staging = stagingObject.AddComponent<LobbyStaging>();

            var speechObject = new GameObject("SpeechBubble");
            speechObject.transform.SetParent(transform, false);
            var speech = speechObject.AddComponent<SpeechBubbleView>();

            var cutsceneObject = new GameObject("Cutscene");
            cutsceneObject.transform.SetParent(transform, false);
            var cutscene = cutsceneObject.AddComponent<CutsceneView>();

            var introObject = new GameObject("IntroVideo");
            introObject.transform.SetParent(transform, false);
            var intro = introObject.AddComponent<IntroVideoView>();

            var director = gameObject.AddComponent<GameDirector>();
            director.Attach(this, lobby, staging, rig, input, hud, audio, vfx,
                game, speech, cutscene, intro);

        }

        void LoadEnemy(EnemyVisual visual, string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
                Debug.LogWarning($"[Bootstrap] prefab missing: {path} — capsule fallback");
            _enemyPrefabs[visual] = prefab;
        }

        /// <summary>
        /// Companion id -> (prefab, tint). Companions ALWAYS get a tint so they
        /// read as allies among identical enemy meshes (payload contract:
        /// material variants only, no new meshes). "-echo" extraction variants
        /// are cyan; boss-reward companions warm gold. Null prefab for unknown ids.
        /// </summary>
        public (GameObject prefab, Color? tint) CompanionVisual(string companionId)
        {
            if (string.IsNullOrEmpty(companionId)) return (null, null);
            var isEcho = companionId.EndsWith("-echo");
            var baseId = isEcho
                ? companionId.Substring(0, companionId.Length - "-echo".Length)
                : companionId;
            var prefab = Resources.Load<GameObject>($"Characters/{baseId}");
            Color? tint = isEcho
                ? new Color(0.62f, 0.95f, 0.88f)    // extraction echo: cyan
                : new Color(1f, 0.86f, 0.55f);      // boss-reward ally: warm gold
            return (prefab, tint);
        }

        // AMENDMENT #16 (W6): per-archetype boss reskins. Lazy-loaded and
        // null-cached — a build without the imported prefabs falls back to the
        // shared EnemyVisual prefab in GameView.RentBoss. Monarch keeps its
        // existing dedicated prefab (loaded via the EnemyVisual table above).
        readonly Dictionary<BossArchetype, GameObject> _bossArchetypePrefabs =
            new Dictionary<BossArchetype, GameObject>(3);

        public GameObject BossArchetypePrefab(BossArchetype archetype)
        {
            string id = archetype switch
            {
                BossArchetype.Warden => "s1-cinder-warden",
                BossArchetype.Tactician => "s2-veil-tactician",
                BossArchetype.Sovereign => "s3-gate-sovereign",
                _ => null,
            };
            if (id == null) return null;
            if (!_bossArchetypePrefabs.TryGetValue(archetype, out var prefab))
            {
                prefab = Resources.Load<GameObject>("Characters/" + id);
                _bossArchetypePrefabs[archetype] = prefab;   // cache misses too
            }
            return prefab;
        }

        public (GameObject prefab, Color fallback, float scale) EnemyVisualFor(EnemyVisual visual)
        {
            _enemyPrefabs.TryGetValue(visual, out var prefab);
            var color = visual switch
            {
                EnemyVisual.EmberCohort => new Color(1f, 0.5f, 0.3f),
                EnemyVisual.Scout => new Color(1f, 0.72f, 0.35f),
                EnemyVisual.Shade => new Color(0.55f, 0.5f, 0.85f),
                EnemyVisual.Possessed => new Color(0.45f, 0.8f, 0.7f),
                EnemyVisual.BossCommander => new Color(0.9f, 0.3f, 0.45f),
                _ => new Color(0.75f, 0.3f, 0.9f),
            };
            return (prefab, color, 1f);
        }
    }
}
