// Code-generated effects only: nova ring, ward shell, pickup gems.
// No asset dependencies; everything survives missing-prefab scaffolding.
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class VfxDirector : MonoBehaviour
    {
        const int RingSegments = 48;

        LineRenderer _novaRing;
        Material _novaMaterial;
        float _novaTime;
        float _novaX, _novaY;

        GameObject _wardShell;
        Material _wardMaterial;

        readonly Dictionary<int, Transform> _pickupViews = new Dictionary<int, Transform>(16);
        readonly List<int> _stale = new List<int>(16);
        Material[] _pickupMaterials;
        Transform _playerTransform;

        // --- campaign hazards (built once on first SyncHazards call) ---------
        struct HazardView
        {
            public Transform Root;
            public Renderer Ring;       // vent telegraph / altar glow
            public Material RingMaterial;
        }
        HazardView[] _hazardViews;

        void Awake()
        {
            var ringObject = new GameObject("NovaRing");
            ringObject.transform.SetParent(transform, false);
            _novaRing = ringObject.AddComponent<LineRenderer>();
            _novaRing.loop = true;
            _novaRing.positionCount = RingSegments;
            _novaRing.widthMultiplier = 0.09f;
            _novaRing.useWorldSpace = true;
            _novaMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.62f, 0.25f, 1f), true);
            _novaRing.sharedMaterial = _novaMaterial;
            _novaRing.enabled = false;

            _wardShell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_wardShell.GetComponent<Collider>());
            _wardShell.name = "WardShell";
            _wardShell.transform.localScale = Vector3.one * 1.7f;
            _wardMaterial = ViewWorld.MakeUnlit(new Color(0.45f, 0.85f, 1f, 0.28f), true);
            _wardShell.GetComponent<Renderer>().sharedMaterial = _wardMaterial;
            _wardShell.SetActive(false);

            _pickupMaterials = new[]
            {
                ViewWorld.MakeUnlit(new Color(1f, 0.604f, 0.322f), false),  // shard #ff9a52
                ViewWorld.MakeUnlit(new Color(1f, 0.831f, 0.537f), false),  // flask #ffd489
                ViewWorld.MakeUnlit(new Color(0.561f, 0.914f, 1f), false),  // relic #8fe9ff
                ViewWorld.MakeUnlit(new Color(0.78f, 0.62f, 1f), false),    // equip shard (campaign)
            };
        }

        public void OnEvents(SimEvents events, ISimSnapshot sim)
        {
            if ((events & SimEvents.NovaCast) != 0)
            {
                _novaTime = 0.42f;
                _novaX = sim.NovaX;
                _novaY = sim.NovaY;
                _novaRing.enabled = true;
            }
        }

        /// <summary>Campaign only. Builds static visuals once, animates per frame.</summary>
        public void SyncHazards(System.Collections.Generic.IReadOnlyList<HazardState> hazards)
        {
            if (_hazardViews == null)
            {
                _hazardViews = new HazardView[hazards.Count];
                for (var i = 0; i < hazards.Count; i++)
                    _hazardViews[i] = BuildHazardView(hazards[i]);
            }
            for (var i = 0; i < hazards.Count && i < _hazardViews.Length; i++)
            {
                var hazard = hazards[i];
                var view = _hazardViews[i];
                switch (hazard.Kind)
                {
                    case HazardKind.EmberVent:
                    {
                        // Telegraph: ring brightens and pulses before the burst.
                        var color = view.RingMaterial.color;
                        if (hazard.Telegraphing)
                        {
                            var pulse = 0.55f + 0.45f * Mathf.PingPong(Time.time * 6f, 1f);
                            color.a = pulse;
                            color.r = 1f; color.g = 0.42f; color.b = 0.18f;
                        }
                        else
                        {
                            color.a = 0.22f;
                            color.r = 1f; color.g = 0.6f; color.b = 0.3f;
                        }
                        view.RingMaterial.color = color;
                        break;
                    }
                    case HazardKind.RelicAltar:
                    {
                        var ready = hazard.CooldownT <= 0f;
                        var color = view.RingMaterial.color;
                        color.a = ready ? 0.5f : 0.14f;
                        view.RingMaterial.color = color;
                        view.Root.localRotation = Quaternion.Euler(0f, Time.time * 24f, 0f);
                        break;
                    }
                    // ObsidianPillar is static.
                }
            }
        }

        HazardView BuildHazardView(in HazardState hazard)
        {
            var view = new HazardView();
            var root = new GameObject($"Hazard-{hazard.Kind}");
            root.transform.SetParent(transform, false);
            root.transform.position = ViewWorld.ToWorld(hazard.X, hazard.Y);
            view.Root = root.transform;

            switch (hazard.Kind)
            {
                case HazardKind.EmberVent:
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(disc.GetComponent<Collider>());
                    disc.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    disc.transform.localScale = new Vector3(r * 2f, 0.012f, r * 2f / SimConfig.IsoY);
                    view.RingMaterial = ViewWorld.MakeUnlit(new Color(1f, 0.6f, 0.3f, 0.22f), true);
                    view.Ring = disc.GetComponent<Renderer>();
                    view.Ring.sharedMaterial = view.RingMaterial;
                    break;
                }
                case HazardKind.ObsidianPillar:
                {
                    var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(pillar.GetComponent<Collider>());
                    pillar.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    pillar.transform.localScale = new Vector3(r * 2f, 1.1f, r * 2f);
                    pillar.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                    var material = ViewWorld.MakeUnlit(new Color(0.12f, 0.1f, 0.2f), false);
                    pillar.GetComponent<Renderer>().sharedMaterial = material;
                    // Faint cyan edge ring at the base for readability.
                    var baseRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(baseRing.GetComponent<Collider>());
                    baseRing.transform.SetParent(root.transform, false);
                    baseRing.transform.localScale = new Vector3(r * 2.3f, 0.008f, r * 2.3f);
                    baseRing.GetComponent<Renderer>().sharedMaterial =
                        ViewWorld.MakeUnlit(new Color(0.35f, 0.6f, 0.8f, 0.3f), true);
                    break;
                }
                case HazardKind.RelicAltar:
                {
                    var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Destroy(disc.GetComponent<Collider>());
                    disc.transform.SetParent(root.transform, false);
                    var r = hazard.Radius * ViewWorld.Scale;
                    disc.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f / SimConfig.IsoY);
                    view.RingMaterial = ViewWorld.MakeUnlit(new Color(0.56f, 0.91f, 1f, 0.5f), true);
                    view.Ring = disc.GetComponent<Renderer>();
                    view.Ring.sharedMaterial = view.RingMaterial;
                    // Center relic gem.
                    var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(gem.GetComponent<Collider>());
                    gem.transform.SetParent(root.transform, false);
                    gem.transform.localScale = Vector3.one * 0.22f;
                    gem.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    gem.GetComponent<Renderer>().sharedMaterial =
                        ViewWorld.MakeUnlit(new Color(0.56f, 0.91f, 1f), false);
                    break;
                }
            }
            return view;
        }

        public void SyncWard(in PlayerState player)
        {
            var active = player.WardTime > 0f;
            if (_wardShell.activeSelf != active)
                _wardShell.SetActive(active);
            if (!active) return;
            _wardShell.transform.position = ViewWorld.ToWorld(player.X, player.Y, 0.85f);
            // Blink during the last 0.5 s.
            if (player.WardTime < 0.5f)
            {
                var on = Mathf.FloorToInt(player.WardTime * 10f) % 2 == 0;
                _wardShell.GetComponent<Renderer>().enabled = on;
            }
            else
            {
                _wardShell.GetComponent<Renderer>().enabled = true;
            }
        }

        public void SyncPickups(IReadOnlyList<PickupState> pickups)
        {
            for (var i = 0; i < pickups.Count; i++)
            {
                var pickup = pickups[i];
                if (!_pickupViews.TryGetValue(pickup.Id, out var view))
                {
                    var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(gem.GetComponent<Collider>());
                    gem.name = "Pickup";
                    gem.transform.localScale = Vector3.one * 0.18f;
                    gem.GetComponent<Renderer>().sharedMaterial =
                        _pickupMaterials[Mathf.Min((int)pickup.Kind, _pickupMaterials.Length - 1)];
                    view = gem.transform;
                    _pickupViews[pickup.Id] = view;
                }
                var bobHeight = 0.25f + Mathf.Sin(pickup.Bob * 3.4f) * 0.07f;
                view.position = ViewWorld.ToWorld(pickup.X, pickup.Y, bobHeight);
                view.rotation = Quaternion.Euler(45f, pickup.Bob * 90f, 45f);
            }

            if (_pickupViews.Count != pickups.Count)
            {
                _stale.Clear();
                foreach (var pair in _pickupViews)
                {
                    var alive = false;
                    for (var i = 0; i < pickups.Count; i++)
                        if (pickups[i].Id == pair.Key) { alive = true; break; }
                    if (!alive) _stale.Add(pair.Key);
                }
                for (var i = 0; i < _stale.Count; i++)
                {
                    Destroy(_pickupViews[_stale[i]].gameObject);
                    _pickupViews.Remove(_stale[i]);
                }
            }
        }

        void Update()
        {
            if (_novaTime > 0f)
            {
                _novaTime -= Time.deltaTime;
                var progress = 1f - Mathf.Clamp01(_novaTime / 0.42f);
                var radius = SimConfig.NovaRadius * ViewWorld.Scale * progress;
                var center = ViewWorld.ToWorld(_novaX, _novaY, 0.05f);
                for (var i = 0; i < RingSegments; i++)
                {
                    var angle = (Mathf.PI * 2f * i) / RingSegments;
                    _novaRing.SetPosition(i, center + new Vector3(
                        Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius * (1f / SimConfig.IsoY)));
                }
                var color = _novaMaterial.color;
                color.a = 1f - progress;
                _novaMaterial.color = color;
                if (_novaTime <= 0f) _novaRing.enabled = false;
            }
        }
    }
}
