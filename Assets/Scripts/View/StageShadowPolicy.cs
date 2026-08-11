// Dungeon-only realtime character-shadow policy.
//
// This component owns the stage-scoped global rendering lease, the positive
// character caster allow-list, the continuous floor receiver, and the one-way
// WebGL quality ladder. It is presentation-only: no simulation value is read
// or written, and caster membership never changes as a performance response.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CinderCourt.View
{
    [DefaultExecutionOrder(1000)]
    public sealed class StageShadowPolicy : MonoBehaviour
    {
        public const string ReceiverName = "stage-shadow-receiver";
        public const string ReceiverResourcePath = "Materials/StageShadowReceiver";
        public const string ReceiverShaderName = "CinderCourt/StageShadowReceiver";

        public const uint DefaultRenderingLayerMask = 1u << 0;
        public const uint CharacterShadowRenderingLayerMask = 1u << 1;
        public const uint ActorRenderingLayerMask =
            DefaultRenderingLayerMask | CharacterShadowRenderingLayerMask;
        public const float CharacterShadowDepthBias = 0.05f;
        public const float CharacterShadowNormalBias = 0f;

        const float ReceiverHeight = 0.018f;
        const float ReceiverEdgeMargin = 0.35f;
        const float MinimumReceiverMargin = 1.5f;
        const float CoverageRefreshSeconds = 0.25f;

        public enum Tier
        {
            High,
            Medium,
            Low,
            Failed,
        }

        static readonly HashSet<ActorView> ActiveActors = new HashSet<ActorView>();
        static Tier _sessionTier = Tier.High;

        public static StageShadowPolicy Current { get; private set; }
        public static Tier SessionTier => _sessionTier;

        Light _keyLight;
        UniversalAdditionalLightData _keyData;
        GameObject _receiverObject;
        Mesh _receiverMesh;
        MeshRenderer _receiverRenderer;
        readonly Vector3[] _receiverVertices = new Vector3[4];

        Light _previousSun;
        AmbientMode _previousAmbientMode;
        Color _previousAmbient;
        Color _previousFog;
        UniversalRenderPipelineAsset _capturedPipelineAsset;
        int _capturedShadowResolution;
        float _capturedShadowDistance;
        bool _ownsLease;

        float _halfWidthSim;
        float _halfHeightSim;
        float _receiverHalfX;
        float _receiverHalfZ;
        float _shadowDistanceFloor;
        bool _coverageDirty;
        float _nextCoverageRefresh;
        Camera _coverageCamera;
        Vector3 _lastCameraPosition;
        Quaternion _lastCameraRotation;
        float _lastCameraAspect;

        ShadowQualityGate _qualityGate;

        public bool OwnsLease => _ownsLease;
        public Light KeyLight => _keyLight;
        public MeshRenderer ReceiverRenderer => _receiverRenderer;
        public float ReceiverHalfX => _receiverHalfX;
        public float ReceiverHalfZ => _receiverHalfZ;
        public float ShadowDistanceFloor => _shadowDistanceFloor;
        public ShadowQualityGate QualityGate => _qualityGate;

        public uint KeyLightingRenderingLayers =>
            _keyData != null ? _keyData.renderingLayers : 0u;

        public uint KeyShadowRenderingLayers =>
            _keyData != null ? _keyData.shadowRenderingLayers : 0u;

        public bool KeyUsesCustomShadowLayers =>
            _keyData != null && _keyData.customShadowLayers;

        public bool KeyUsesPipelineShadowBias =>
            _keyData != null && _keyData.usePipelineSettings;

        public bool CapturedPipelineAssetHasOriginalSettings =>
            _capturedPipelineAsset == null
            || (_capturedPipelineAsset.mainLightShadowmapResolution
                == _capturedShadowResolution
                && Mathf.Approximately(
                    _capturedPipelineAsset.shadowDistance,
                    _capturedShadowDistance));

        public static bool IsEligibleCaster(Renderer renderer)
            => renderer is MeshRenderer || renderer is SkinnedMeshRenderer;

        public static bool TryConfigureCaster(Renderer renderer)
        {
            if (renderer == null) return false;
            if (!IsEligibleCaster(renderer))
            {
                ConfigureExcludedRenderer(renderer);
                return false;
            }

            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = false;
            renderer.renderingLayerMask = ActorRenderingLayerMask;
            return true;
        }

        public static void ConfigureExcludedRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var mask = renderer.renderingLayerMask & ~CharacterShadowRenderingLayerMask;
            renderer.renderingLayerMask = mask == 0u ? DefaultRenderingLayerMask : mask;
        }

        public static int ResolutionFor(Tier tier)
        {
            switch (tier)
            {
                case Tier.High: return 1024;
                case Tier.Medium: return 512;
                default: return 256;
            }
        }

        internal static bool SetReceiverEnabledForDiagnostics(bool enabled)
        {
            var renderer = Current != null ? Current._receiverRenderer : null;
            if (renderer == null
                || !IsValidReceiverMaterial(renderer.sharedMaterial))
                return false;
            renderer.enabled = enabled;
            return renderer.enabled == enabled;
        }

        public static Tier NextTier(Tier tier)
        {
            switch (tier)
            {
                case Tier.High: return Tier.Medium;
                case Tier.Medium: return Tier.Low;
                case Tier.Low: return Tier.Failed;
                default: return Tier.Failed;
            }
        }

        static float DistanceTargetFor(Tier tier)
        {
            switch (tier)
            {
                case Tier.High: return 50f;
                case Tier.Medium: return 42f;
                default: return 34f;
            }
        }

        internal void Acquire(
            Light keyLight,
            Color accent,
            float halfWidthSim,
            float halfHeightSim)
        {
            if (_ownsLease) return;
            if (Current != null && Current != this) Current.RestoreOnce();

            _previousSun = RenderSettings.sun;
            _previousAmbientMode = RenderSettings.ambientMode;
            _previousAmbient = RenderSettings.ambientLight;
            _previousFog = RenderSettings.fogColor;
            _capturedPipelineAsset =
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_capturedPipelineAsset != null)
            {
                _capturedShadowResolution =
                    _capturedPipelineAsset.mainLightShadowmapResolution;
                _capturedShadowDistance = _capturedPipelineAsset.shadowDistance;
            }

            _keyLight = keyLight;
            _halfWidthSim = Mathf.Max(0f, halfWidthSim);
            _halfHeightSim = Mathf.Max(0f, halfHeightSim);
            _qualityGate = new ShadowQualityGate(_sessionTier);
            _ownsLease = true;
            Current = this;

            ConfigureKeyLight();
            CreateReceiver();
            RenderSettings.sun = _keyLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = StageMood.AmbientColor(accent);
            RenderSettings.fogColor = StageMood.FogColor(accent);

            _coverageDirty = true;
            RefreshCoverage();
            ApplyQualityTier();
            SynchronizeMeasurementEpoch();
        }

        void ConfigureKeyLight()
        {
            if (_keyLight == null) return;
            _keyLight.shadows = LightShadows.Hard;
            _keyData = _keyLight.GetUniversalAdditionalLightData();
            // Mobile_RPAsset's 1/1 depth/normal defaults erase thin limbs and
            // equipment at the single-cascade WebGL resolutions used here.
            // This key lights only non-receiving actor casters onto a separate
            // floor receiver, so preserve their full silhouette with a tiny
            // depth offset and no normal inset.
            _keyData.usePipelineSettings = false;
            _keyLight.shadowBias = CharacterShadowDepthBias;
            _keyLight.shadowNormalBias = CharacterShadowNormalBias;
            _keyData.renderingLayers = DefaultRenderingLayerMask;
            _keyData.shadowRenderingLayers = CharacterShadowRenderingLayerMask;
            _keyData.customShadowLayers = true;
            // UALD synchronizes Light.renderingLayerMask to the shadow mask when
            // custom shadow layers are active. Set it explicitly as a fail-closed
            // assertion of that runtime contract.
            _keyLight.renderingLayerMask =
                unchecked((int)CharacterShadowRenderingLayerMask);
        }

        void CreateReceiver()
        {
            _receiverObject = new GameObject(ReceiverName);
            _receiverObject.transform.SetParent(transform, false);
            _receiverObject.transform.position =
                ViewWorld.ArenaCenter + Vector3.up * ReceiverHeight;

            _receiverMesh = new Mesh { name = "StageShadowReceiverMesh" };
            _receiverMesh.MarkDynamic();
            _receiverMesh.vertices = _receiverVertices;
            _receiverMesh.normals = new[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            };
            _receiverMesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 0f),
            };
            _receiverMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };

            _receiverObject.AddComponent<MeshFilter>().sharedMesh = _receiverMesh;
            _receiverRenderer = _receiverObject.AddComponent<MeshRenderer>();
            _receiverRenderer.enabled = false;
            _receiverRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _receiverRenderer.receiveShadows = true;
            _receiverRenderer.renderingLayerMask = DefaultRenderingLayerMask;
            var material = Resources.Load<Material>(ReceiverResourcePath);
            if (!IsValidReceiverMaterial(material))
            {
                _receiverRenderer.sharedMaterial = null;
                Debug.LogError(
                    $"Stage shadow receiver material missing or invalid at Resources/{ReceiverResourcePath}");
                return;
            }
            _receiverRenderer.sharedMaterial = material;
            _receiverRenderer.enabled = true;
        }

        internal static bool IsValidReceiverMaterial(Material material)
            => material != null
                && material.shader != null
                && material.shader.name == ReceiverShaderName;

        void LateUpdate()
        {
            if (!_ownsLease) return;

            var camera = Camera.main;
            var cameraChanged = camera != _coverageCamera;
            if (camera != null && !cameraChanged)
            {
                cameraChanged = camera.transform.position != _lastCameraPosition
                    || camera.transform.rotation != _lastCameraRotation
                    || !Mathf.Approximately(camera.aspect, _lastCameraAspect);
            }

            if (_coverageDirty || cameraChanged
                || Time.unscaledTime >= _nextCoverageRefresh)
                RefreshCoverage();

            if (PostFxGate.CurrentMeasurementOwner
                    != PostFxGate.MeasurementOwner.Shadow
                || _qualityGate == null
                || _qualityGate.Tier == Tier.Failed)
                return;

            if (_qualityGate.Epoch != PostFxGate.StageEpoch)
                _qualityGate.BeginEpoch(PostFxGate.StageEpoch);
            if (!_qualityGate.Sample(Time.unscaledDeltaTime)) return;

            _sessionTier = _qualityGate.Tier;
            ApplyQualityTier();
            Debug.Log(
                _sessionTier == Tier.Failed
                    ? "stage-shadows: FAILED at Low; caster membership preserved"
                    : $"stage-shadows: degraded to {_sessionTier} "
                      + $"({ResolutionFor(_sessionTier)})");
        }

        void SynchronizeMeasurementEpoch()
        {
            if (_qualityGate == null) return;
            if (PostFxGate.CurrentMeasurementOwner
                == PostFxGate.MeasurementOwner.Shadow)
                _qualityGate.BeginEpoch(PostFxGate.StageEpoch);
        }

        internal static void BeginShadowMeasurementEpoch(int epoch)
        {
            if (Current == null || Current._qualityGate == null) return;
            Current._qualityGate.BeginEpoch(epoch);
        }

        void ApplyQualityTier()
        {
            if (_capturedPipelineAsset == null) return;
            _capturedPipelineAsset.mainLightShadowmapResolution =
                ResolutionFor(_sessionTier);
            _capturedPipelineAsset.shadowDistance = Mathf.Max(
                DistanceTargetFor(_sessionTier), _shadowDistanceFloor);
        }

        void RefreshCoverage(Camera explicitCamera = null)
        {
            _coverageDirty = false;
            _nextCoverageRefresh = Time.unscaledTime + CoverageRefreshSeconds;

            var maximumHeight = 1.8f;
            var maximumHorizontalRadius = 0.5f;
            foreach (var actor in ActiveActors)
            {
                if (actor == null || !actor.isActiveAndEnabled) continue;
                actor.AccumulateShadowCasterExtents(
                    ref maximumHeight, ref maximumHorizontalRadius);
            }

            var projection = maximumHeight
                / Mathf.Tan(StageMood.MinimumCharacterShadowPitch * Mathf.Deg2Rad);
            var margin = Mathf.Max(
                MinimumReceiverMargin,
                maximumHorizontalRadius + projection + ReceiverEdgeMargin);
            var halfX = _halfWidthSim * ViewWorld.Scale + margin;
            var halfZ = _halfHeightSim * ViewWorld.Scale + margin;
            var camera = explicitCamera != null ? explicitCamera : Camera.main;
            var center = _receiverObject != null
                ? _receiverObject.transform.position
                : ViewWorld.ArenaCenter + Vector3.up * ReceiverHeight;
            ExpandToViewport(camera, center, ref halfX, ref halfZ);

            // Grow-only inside a stage. Animation bounds and late equipment can
            // expand the need; shrinking on an idle frame would clip the next
            // swing and churn the runtime mesh.
            if (halfX > _receiverHalfX + 0.001f
                || halfZ > _receiverHalfZ + 0.001f)
            {
                _receiverHalfX = Mathf.Max(_receiverHalfX, halfX);
                _receiverHalfZ = Mathf.Max(_receiverHalfZ, halfZ);
                UpdateReceiverMesh();
            }

            RefreshShadowDistanceFloor(maximumHeight, camera);
            ApplyQualityTier();
        }

        static void ExpandToViewport(
            Camera camera, Vector3 center, ref float halfX, ref float halfZ)
        {
            if (camera == null) return;
            var plane = new Plane(Vector3.up, center);
            for (var x = 0; x <= 1; x++)
            for (var y = 0; y <= 1; y++)
            {
                var ray = camera.ViewportPointToRay(new Vector3(x, y, 0f));
                if (!plane.Raycast(ray, out var enter) || enter <= 0f
                    || float.IsNaN(enter) || float.IsInfinity(enter))
                    continue;
                var hit = ray.GetPoint(enter) - center;
                halfX = Mathf.Max(halfX, Mathf.Abs(hit.x) + ReceiverEdgeMargin);
                halfZ = Mathf.Max(halfZ, Mathf.Abs(hit.z) + ReceiverEdgeMargin);
            }
        }

        void UpdateReceiverMesh()
        {
            if (_receiverMesh == null) return;
            _receiverVertices[0] = new Vector3(-_receiverHalfX, 0f, -_receiverHalfZ);
            _receiverVertices[1] = new Vector3(-_receiverHalfX, 0f, _receiverHalfZ);
            _receiverVertices[2] = new Vector3(_receiverHalfX, 0f, _receiverHalfZ);
            _receiverVertices[3] = new Vector3(_receiverHalfX, 0f, -_receiverHalfZ);
            _receiverMesh.vertices = _receiverVertices;
            _receiverMesh.RecalculateBounds();
        }

        void RefreshShadowDistanceFloor(float maximumHeight, Camera camera)
        {
            _coverageCamera = camera;
            if (camera == null)
            {
                _shadowDistanceFloor = Mathf.Max(
                    _shadowDistanceFloor,
                    Mathf.Sqrt(_receiverHalfX * _receiverHalfX
                        + _receiverHalfZ * _receiverHalfZ) + maximumHeight);
                return;
            }

            _lastCameraPosition = camera.transform.position;
            _lastCameraRotation = camera.transform.rotation;
            _lastCameraAspect = camera.aspect;
            var center = _receiverObject != null
                ? _receiverObject.transform.position
                : ViewWorld.ArenaCenter;
            var floor = 0f;
            for (var x = -1; x <= 1; x += 2)
            for (var y = 0; y <= 1; y++)
            for (var z = -1; z <= 1; z += 2)
            {
                var point = center + new Vector3(
                    x * _receiverHalfX, y * maximumHeight, z * _receiverHalfZ);
                floor = Mathf.Max(floor,
                    Vector3.Distance(camera.transform.position, point));
            }
            foreach (var actor in ActiveActors)
            {
                if (actor == null || !actor.isActiveAndEnabled) continue;
                actor.AccumulateShadowCasterDistance(camera, ref floor);
            }
            _shadowDistanceFloor = floor + ReceiverEdgeMargin;
        }

        internal void RefreshCoverageForTests(Camera camera)
        {
            _coverageDirty = true;
            RefreshCoverage(camera);
        }

        public void RestoreOnce()
        {
            if (!_ownsLease) return;

            // Sun is restored while the old key still exists. GameDirector calls
            // this before Destroy, and OnDisable/OnDestroy only provide the same
            // idempotent safety net.
            RenderSettings.sun = _previousSun;
            RenderSettings.ambientMode = _previousAmbientMode;
            RenderSettings.ambientLight = _previousAmbient;
            RenderSettings.fogColor = _previousFog;
            if (_capturedPipelineAsset != null)
            {
                _capturedPipelineAsset.mainLightShadowmapResolution =
                    _capturedShadowResolution;
                _capturedPipelineAsset.shadowDistance = _capturedShadowDistance;
            }

            _ownsLease = false;
            if (Current == this) Current = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public static void RestoreCurrent()
        {
            if (Current != null) Current.RestoreOnce();
        }

        void OnDisable() => RestoreOnce();

        void OnDestroy()
        {
            RestoreOnce();
            if (_receiverMesh != null)
            {
                if (Application.isPlaying) Destroy(_receiverMesh);
                else DestroyImmediate(_receiverMesh);
                _receiverMesh = null;
            }
        }

        internal static void RegisterActor(ActorView actor)
        {
            if (actor == null) return;
            ActiveActors.Add(actor);
            NotifyCasterBoundsChanged();
        }

        internal static void UnregisterActor(ActorView actor)
        {
            if (actor == null) return;
            ActiveActors.Remove(actor);
            NotifyCasterBoundsChanged();
        }

        internal static void NotifyCasterBoundsChanged()
        {
            if (Current != null) Current._coverageDirty = true;
        }

        internal static int FallbackActorCount
        {
            get
            {
                var count = 0;
                foreach (var actor in ActiveActors)
                    if (actor != null && actor.isActiveAndEnabled
                        && actor.UsesFallbackForShadowDiagnostics)
                        count++;
                return count;
            }
        }

        internal static void ResetSessionForTests()
        {
            RestoreCurrent();
            _sessionTier = Tier.High;
        }
    }

    /// <summary>
    /// Allocation-free shadow-only watchdog. It reuses the PostFx numeric gate,
    /// but owns a distinct epoch/window and never disables caster membership.
    /// </summary>
    public sealed class ShadowQualityGate
    {
        public enum Status
        {
            Measuring,
            Holding,
            Failed,
        }

        readonly bool[] _over = new bool[PostFxGate.WindowFrames];
        int _cursor;
        float _warmup;
        float _breachHeld;

        public StageShadowPolicy.Tier Tier { get; private set; }
        public Status Current { get; private set; }
        public int Epoch { get; private set; } = -1;
        public int OverBudgetInWindow { get; private set; }
        public int SamplesInWindow { get; private set; }

        public ShadowQualityGate(StageShadowPolicy.Tier tier)
        {
            Tier = tier;
            Current = tier == StageShadowPolicy.Tier.Failed
                ? Status.Failed
                : Status.Measuring;
        }

        public void BeginEpoch(int epoch, float warmup = PostFxGate.WarmupSeconds)
        {
            Epoch = epoch;
            ResetWindow(warmup);
            Current = Tier == StageShadowPolicy.Tier.Failed
                ? Status.Failed
                : Status.Measuring;
        }

        public bool Sample(float delta)
        {
            if (Tier == StageShadowPolicy.Tier.Failed) return false;
            if (delta <= 0f || delta > PostFxGate.StallCeilingSeconds) return false;
            if (_warmup > 0f)
            {
                _warmup -= delta;
                return false;
            }

            if (SamplesInWindow >= PostFxGate.WindowFrames && _over[_cursor])
                OverBudgetInWindow--;
            var over = delta > PostFxGate.FrameBudgetSeconds;
            _over[_cursor] = over;
            if (over) OverBudgetInWindow++;
            _cursor = _cursor + 1 >= PostFxGate.WindowFrames ? 0 : _cursor + 1;
            if (SamplesInWindow < PostFxGate.WindowFrames) SamplesInWindow++;

            if (SamplesInWindow < PostFxGate.WindowFrames)
            {
                Current = Status.Measuring;
                return false;
            }

            if (!WindowBreaches(OverBudgetInWindow, SamplesInWindow))
            {
                _breachHeld = 0f;
                Current = Status.Holding;
                return false;
            }

            Current = Status.Measuring;
            _breachHeld += delta;
            if (_breachHeld + 0.0001f < PostFxGate.HoldSeconds) return false;

            Tier = StageShadowPolicy.NextTier(Tier);
            Current = Tier == StageShadowPolicy.Tier.Failed
                ? Status.Failed
                : Status.Measuring;
            ResetWindow(PostFxGate.WarmupSeconds);
            return true;
        }

        public static bool WindowBreaches(int overBudgetFrames, int samples)
            => samples >= PostFxGate.WindowFrames
                && overBudgetFrames >= PostFxGate.OverBudgetTrip;

        void ResetWindow(float warmup)
        {
            Array.Clear(_over, 0, _over.Length);
            _cursor = 0;
            _warmup = Mathf.Max(0f, warmup);
            _breachHeld = 0f;
            OverBudgetInWindow = 0;
            SamplesInWindow = 0;
        }
    }
}
