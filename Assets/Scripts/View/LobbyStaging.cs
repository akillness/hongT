// Live 3D lobby backdrop (spec §9): warden idle center-left, active companion
// beside, selected stage's boss in a distant 'show' loop, stage accent light.
// Pure presentation — built from the same prefabs the run uses.
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class LobbyStaging : MonoBehaviour
    {
        static readonly int ActionParam = Animator.StringToHash("action");
        static readonly Vector3 WardenSpot = ViewWorld.ToWorld(640f, 700f);
        static readonly Vector3 CompanionSpot = ViewWorld.ToWorld(540f, 640f);
        static readonly Vector3 BossSpot = ViewWorld.ToWorld(940f, 380f);

        GameBootstrap _bootstrap;
        GameObject _warden;
        GameObject _companion;
        GameObject _boss;
        Light _accent;
        string _bossVisualShown = "";
        string _companionShown = "";

        public void Attach(GameBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            if (_accent != null) return;   // idempotent — re-entering the lobby
            var accentObject = new GameObject("LobbyAccent");
            accentObject.transform.SetParent(transform, false);
            _accent = accentObject.AddComponent<Light>();
            _accent.type = LightType.Point;
            _accent.range = 9f;
            _accent.intensity = 2.4f;
            accentObject.transform.position = ArenaMid(1.8f);
        }

        static Vector3 ArenaMid(float height)
            => ViewWorld.ToWorld(768f, 604f) + new Vector3(0f, height, 0f);

        /// <summary>Compose (or retint) the diorama for the selected stage.</summary>
        public void Show(string stageId, string companionId)
        {
            gameObject.SetActive(true);
            if (_warden == null && _bootstrap != null)
            {
                _warden = Compose(_bootstrap.PlayerPrefab, WardenSpot, 152f);
                SetAction(_warden, ActorAction.Idle);
            }

            var bossVisual = stageId == CampaignStages.EchoThrone
                ? "broken-court-monarch-boss" : "shadow-commander-boss";
            if (_bossVisualShown != bossVisual)
            {
                _bossVisualShown = bossVisual;
                if (_boss != null) Destroy(_boss);
                var prefab = Resources.Load<GameObject>($"Characters/{bossVisual}");
                _boss = Compose(prefab, BossSpot, 232f, 1.45f);
                SetAction(_boss, ActorAction.Show);   // menacing loop at distance
            }

            var wantCompanion = companionId ?? "";
            if (_companionShown != wantCompanion)
            {
                _companionShown = wantCompanion;
                if (_companion != null) { Destroy(_companion); _companion = null; }
                if (wantCompanion.Length > 0 && _bootstrap != null)
                {
                    var (prefab, tint) = _bootstrap.CompanionVisual(wantCompanion);
                    _companion = Compose(prefab, CompanionSpot, 128f, 0.92f);
                    if (_companion != null && tint.HasValue)
                        TintRenderers(_companion, tint.Value);
                    SetAction(_companion, ActorAction.Idle);
                }
            }

            // Stage accent tint (original two-layer lighting contract).
            _accent.color = stageId switch
            {
                CampaignStages.AbyssChancel => new Color(0.56f, 0.40f, 1f),
                CampaignStages.EchoThrone => new Color(0.45f, 0.78f, 1f),
                _ => new Color(0.95f, 0.35f, 0.17f),
            };
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        GameObject Compose(GameObject prefab, Vector3 position, float facingYaw,
                           float scale = 1f)
        {
            GameObject instance;
            if (prefab != null)
            {
                instance = Instantiate(prefab);
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(instance.GetComponent<Collider>());
            }
            instance.transform.SetParent(transform, false);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, facingYaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            return instance;
        }

        static void SetAction(GameObject actor, ActorAction action)
        {
            if (actor == null) return;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null && animator.isActiveAndEnabled)
                animator.SetInteger(ActionParam, (int)action);
        }

        public static void TintRenderers(GameObject root, Color tint)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_BaseColor"), tint);
            var renderers = root.GetComponentsInChildren<Renderer>();
            for (var i = 0; i < renderers.Length; i++)
                renderers[i].SetPropertyBlock(block);
        }
    }
}
