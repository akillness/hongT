// Owns the CinderSim and the fixed-step accumulator (spec §World — NOT Unity
// FixedUpdate). Distributes snapshots to actor views, HUD, audio, VFX.
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class GameView : MonoBehaviour
    {
        public InputAdapter Input;
        public HudView Hud;
        public AudioDirector Audio;
        public VfxDirector Vfx;
        public CameraRig Rig;
        public GameBootstrap Bootstrap;

        public ICinderSim Sim => _sim;

        CinderSim _sim;
        readonly Dictionary<int, ActorView> _enemyViews = new Dictionary<int, ActorView>(SimConfig.EnemyCap * 2);
        readonly Stack<ActorView>[] _pools = new Stack<ActorView>[6];
        readonly List<int> _toRecycle = new List<int>(SimConfig.EnemyCap);

        ActorView _playerView;
        float _accumulator;
        bool _digestWritten;
        bool _isCampaign;
        bool _campaignPersisted;
        CampaignConfig _campaignConfig;

        void Start()
        {
            if (Bootstrap != null && Bootstrap.HasCampaign)
            {
                _isCampaign = true;
                _campaignConfig = Bootstrap.Campaign;
                _sim = new CinderSim(in _campaignConfig);
                if (Hud != null)
                    Hud.EnableCampaignUi(Bootstrap.CampaignStageName, _campaignConfig.Waves);
            }
            else
            {
                _sim = new CinderSim();
            }

            for (var i = 0; i < _pools.Length; i++)
                _pools[i] = new Stack<ActorView>(8);
            _playerView = ActorView.Create(
                Bootstrap != null ? Bootstrap.PlayerPrefab : null,
                new Color(0.55f, 0.75f, 1f), 1f);
            _playerView.name = "Player";
        }

        void Update()
        {
            if (_sim == null) return;   // Start() not run yet
            var delta = Mathf.Min(Time.deltaTime, SimConfig.MaxFrameDelta);
            _accumulator += delta;
            var steps = 0;
            var input = Input != null ? Input.Sample() : default;
            while (_accumulator >= SimConfig.FixedStep && steps < SimConfig.MaxCatchUpSteps)
            {
                _sim.Tick(in input);
                DispatchEvents();
                // One-shot flags must fire exactly once per sample batch.
                input.AttackQueued = false;
                input.NovaQueued = false;
                input.WardQueued = false;
                input.RestartQueued = false;
                _accumulator -= SimConfig.FixedStep;
                steps++;
            }
            if (_accumulator >= SimConfig.FixedStep)
                _accumulator = SimConfig.FixedStep; // drop backlog beyond catch-up
            // Only consume latches when at least one tick sampled them —
            // otherwise a 144 Hz frame with no step would eat Q/E presses.
            if (steps > 0 && Input != null) Input.ClearLatches();

            SyncViews();
        }

        void DispatchEvents()
        {
            var events = _sim.Events;
            if (events == SimEvents.None) return;
            if (Audio != null) Audio.OnEvents(events);
            if (Vfx != null) Vfx.OnEvents(events, _sim);
            if (Rig != null) Rig.OnEvents(events);
            if (Hud != null) Hud.OnEvents(events, _sim);

            if ((events & SimEvents.GameOver) != 0 && !_digestWritten)
            {
                _digestWritten = true;
                WebGLStorage.WriteRunDigest(_sim.Digest);
            }
            if ((events & SimEvents.StageCleared) != 0)
            {
                if (!_digestWritten)
                {
                    _digestWritten = true;
                    WebGLStorage.WriteRunDigest(_sim.Digest);
                }
                if (!_campaignPersisted && _isCampaign)
                {
                    _campaignPersisted = true;
                    PersistCampaign();
                }
                if (Hud != null) Hud.ShowStageClear(_sim.Digest);
            }
            if ((events & SimEvents.WaveStarted) != 0)
            {
                _digestWritten = false;
                _campaignPersisted = false;
            }
        }

        void PersistCampaign()
        {
            var progress = WebGLStorage.ReadCampaign();
            switch (_campaignConfig.StageId)
            {
                case CampaignStages.CinderSpan: progress.CinderSpanCleared = true; break;
                case CampaignStages.AbyssChancel: progress.AbyssChancelCleared = true; break;
                case CampaignStages.EchoThrone: progress.EchoThroneCleared = true; break;
            }
            // Cleared-run ranks are the new meta baseline (spec: equipment is
            // the reward for closing a stage; defeat keeps the old baseline).
            progress.Weapon = _sim.WeaponRank;
            progress.Lantern = _sim.LanternRank;
            progress.Cloak = _sim.CloakRank;
            WebGLStorage.WriteCampaign(in progress);
        }

        void SyncViews()
        {
            _playerView.SyncPlayer(_sim.Player);

            var enemies = _sim.Enemies;
            // Mark-and-sweep: sync live ids, recycle views whose id vanished.
            for (var i = 0; i < enemies.Count; i++)
            {
                var state = enemies[i];
                if (!_enemyViews.TryGetValue(state.Id, out var view))
                {
                    view = Rent(state.Visual);
                    _enemyViews[state.Id] = view;
                }
                view.SyncEnemy(in state);
            }
            if (_enemyViews.Count != enemies.Count)
            {
                _toRecycle.Clear();
                foreach (var pair in _enemyViews)
                {
                    var alive = false;
                    for (var i = 0; i < enemies.Count; i++)
                        if (enemies[i].Id == pair.Key) { alive = true; break; }
                    if (!alive) _toRecycle.Add(pair.Key);
                }
                for (var i = 0; i < _toRecycle.Count; i++)
                {
                    var id = _toRecycle[i];
                    Return(_enemyViews[id]);
                    _enemyViews.Remove(id);
                }
            }

            if (Vfx != null) Vfx.SyncPickups(_sim.Pickups);
            if (Vfx != null) Vfx.SyncWard(_sim.Player);
            if (Vfx != null && _isCampaign) Vfx.SyncHazards(_sim.Hazards);
            if (Hud != null) Hud.Sync(_sim);
            if (Hud != null && _isCampaign)
                Hud.SyncCampaign(_sim.Wave, _sim.BossAlive,
                    _sim.WeaponRank, _sim.LanternRank, _sim.CloakRank);
        }

        ActorView Rent(EnemyVisual visual)
        {
            var pool = _pools[(int)visual];
            if (pool.Count > 0)
            {
                var pooled = pool.Pop();
                pooled.gameObject.SetActive(true);
                pooled.ResetForPool();
                return pooled;
            }
            var (prefab, color, scale) = Bootstrap != null
                ? Bootstrap.EnemyVisualFor(visual)
                : (null, Color.red, 1f);
            var view = ActorView.Create(prefab, color, scale);
            view.name = visual.ToString();
            view.GetComponent<ActorView>().enabled = true;
            var marker = view.gameObject.AddComponent<VisualMarker>();
            marker.Visual = visual;
            return view;
        }

        void Return(ActorView view)
        {
            var marker = view.GetComponent<VisualMarker>();
            var visual = marker != null ? marker.Visual : EnemyVisual.EmberCohort;
            view.gameObject.SetActive(false);
            _pools[(int)visual].Push(view);
        }

        sealed class VisualMarker : MonoBehaviour
        {
            public EnemyVisual Visual;
        }
    }
}
