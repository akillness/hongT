// The single scene component. Assembles the whole runtime object graph in
// Awake: input, audio, VFX, camera rig, game view, HUD. Loads prefabs and
// audio from Resources with graceful fallbacks so the game always boots.
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

            PlayerPrefab = Resources.Load<GameObject>("Characters/guard");
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
        }

        void LoadEnemy(EnemyVisual visual, string path)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
                Debug.LogWarning($"[Bootstrap] prefab missing: {path} — capsule fallback");
            _enemyPrefabs[visual] = prefab;
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
