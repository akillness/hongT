using UnityEngine;
using UnityEngine.UI;

namespace CinderCourt.View
{
    /// <summary>
    /// Single font and raster policy for every runtime-built player-facing label.
    /// The bundled subset is mandatory on WebGL because the builtin editor font
    /// has no Hangul fallback there.
    /// </summary>
    internal static class ViewTypography
    {
        internal const float LineSpacing = 1.05f;
        internal const int HeadingMinimumSize = 20;

        static Font _font;

        internal static Font ResolveFont()
        {
            if (_font != null) return _font;

            _font = Resources.Load<Font>("Fonts/HudKorean");
            if (_font != null) return _font;

            Debug.LogError("Required WebGL font Resources/Fonts/HudKorean is missing; Korean text cannot render.");
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        internal static void Configure(Text text, Font font, int size, TextAnchor anchor)
        {
            var resolved = font != null ? font : ResolveFont();
            text.font = resolved;
            text.material = resolved.material;
            text.fontSize = size;
            text.fontStyle = size >= HeadingMinimumSize ? FontStyle.Bold : FontStyle.Normal;
            text.lineSpacing = LineSpacing;
            text.alignment = anchor;
            text.resizeTextForBestFit = false;
        }

        internal static void Configure(TextMesh text, Font font)
        {
            var resolved = font != null ? font : ResolveFont();
            text.font = resolved;
            text.fontStyle = FontStyle.Bold;
            text.GetComponent<MeshRenderer>().sharedMaterial = resolved.material;
        }
    }
}
