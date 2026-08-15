using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public readonly struct StageHazardTextureResult
    {
        public readonly bool Found;
        public readonly Texture2D Texture;
        public readonly bool IsFallback;
        public readonly HazardSurfaceBinding Binding;

        public StageHazardTextureResult(
            bool found, Texture2D texture, bool isFallback, HazardSurfaceBinding binding)
        {
            Found = found;
            Texture = texture;
            IsFallback = isFallback;
            Binding = binding;
        }
    }

    /// <summary>Loads and caches generated stage hazard surface textures from Resources.</summary>
    public sealed class StageHazardTextureResolver
    {
        readonly Dictionary<string, Texture2D> _loaded =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        readonly HashSet<string> _missing =
            new HashSet<string>(StringComparer.Ordinal);

        public StageHazardTextureResult Resolve(string stageId, HazardKind kind)
        {
            if (!StageHazardVisualCatalog.TryGetBinding(stageId, kind, out var binding))
                return new StageHazardTextureResult(false, null, true, default);

            var path = binding.ResourcePath;
            if (_loaded.TryGetValue(path, out var texture))
                return new StageHazardTextureResult(true, texture, false, binding);

            if (_missing.Contains(path))
                return new StageHazardTextureResult(false, null, true, binding);

            texture = Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                _loaded[path] = texture;
                return new StageHazardTextureResult(true, texture, false, binding);
            }

            _missing.Add(path);
            return new StageHazardTextureResult(false, null, true, binding);
        }
    }
}
