// Campaign minimap renderer. Builds once, then only re-tints — the same
// build-once/refresh-state contract every other lobby surface keeps.
//
// Not a MonoBehaviour: it is a widget, so both hosts (the lobby's compact panel
// and the meta screen's 지도 tab) can own one without a second GameObject
// lifecycle to reason about. Every decision about WHAT to show lives in
// CampaignMapLayout; this file is placement and colour only.
//
// Geometry is expressed in fixed UI units passed in by the host rather than
// read back from RectTransform.rect. Reading the rect would make the node
// positions depend on when Unity last ran layout, which is exactly the kind of
// frame-order dependency the EditMode rect audits cannot pin.
using UnityEngine;
using UnityEngine.UI;

namespace CinderCourt.View
{
    public sealed class CampaignMapView
    {
        // Dark-fantasy three-token palette, matching LobbyView's own tokens so
        // the map does not read as a different product than the panel holding it.
        static readonly Color Charcoal = new Color(0.043f, 0.047f, 0.075f, 0.92f);
        static readonly Color Cyan = new Color(0x2C / 255f, 0xAD / 255f, 0xD6 / 255f);
        static readonly Color Ember = new Color(0xF3 / 255f, 0x59 / 255f, 0x2C / 255f);
        static readonly Color Ink = new Color(0.92f, 0.94f, 1f);

        /// <summary>Inset from the field edge to the node centres, in UI units.
        /// Labels hang below their node, so the bottom pad is the deeper one.</summary>
        const float PadX = 26f, PadTop = 18f, PadBottom = 34f;

        Font _font;
        RectTransform _field;
        Text _progress;
        Image[] _nodeMarks;
        Image[] _nodeCores;
        Text[] _nodeLabels;
        Text[] _nodeEpithets;
        Image[] _linkLines;
        CampaignMapLink[] _links;
        RectTransform[] _nodeRects;
        float[] _nodeBaseSize;
        int _frontier = -1;
        bool _showEpithets;

        /// <summary>Node count — the catalog width, fixed at build time.</summary>
        public int NodeCount => _nodeMarks == null ? 0 : _nodeMarks.Length;
        public RectTransform Field => _field;

        /// <summary>Test seam: the opacity a node is actually rendering at.
        /// Asserting on the model alone would pass even if the renderer wired
        /// the wrong node, which is the mistake worth catching here.</summary>
        internal float AlphaAt(int index) => _nodeMarks[index].color.a;
        internal string LabelAt(int index) => _nodeLabels[index].text;
        internal bool LinkLitAt(int index) => _linkLines[index].color.a > 0.5f;
        internal int LinkCount => _linkLines == null ? 0 : _linkLines.Length;

        /// <param name="size">Field size in UI units. The host owns it because
        /// the compact lobby panel and the full-screen map tab are deliberately
        /// different scales of the same constellation.</param>
        /// <param name="showEpithets">Full map only: the compact panel has no
        /// room for a second line under each node.</param>
        public void Build(Transform parent, Font font, Vector2 anchoredPosition, Vector2 size,
            bool showEpithets, int labelSize = 10)
        {
            _font = font;
            _showEpithets = showEpithets;

            var fieldObject = new GameObject("CampaignMap");
            fieldObject.transform.SetParent(parent, false);
            var background = fieldObject.AddComponent<Image>();
            background.color = Charcoal;
            background.raycastTarget = false;
            _field = fieldObject.GetComponent<RectTransform>();
            _field.anchorMin = _field.anchorMax = new Vector2(0f, 1f);
            _field.pivot = new Vector2(0f, 1f);
            _field.anchoredPosition = anchoredPosition;
            _field.sizeDelta = size;

            _progress = MakeText(_field, new Vector2(10f, -4f), new Vector2(size.x - 20f, 16f),
                "", labelSize + 1, TextAnchor.MiddleLeft);
            _progress.color = Cyan;

            var entries = StageCatalog.Entries;
            var innerWidth = Mathf.Max(1f, size.x - PadX * 2f);
            var innerHeight = Mathf.Max(1f, size.y - PadTop - PadBottom);
            var centres = new Vector2[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                // Catalog y is "up"; UI anchoredPosition under a top-left pivot
                // is "down", hence the flip. Doing it here and nowhere else
                // keeps the catalog readable as a map instead of as a screen.
                centres[i] = new Vector2(
                    PadX + Mathf.Clamp01(entry.NodeX) * innerWidth,
                    -(PadTop + (1f - Mathf.Clamp01(entry.NodeY)) * innerHeight));
            }

            // Links first so every thread sits UNDER the node it connects.
            _links = CampaignMapLayout.BuildLinks(default);
            _linkLines = new Image[_links.Length];
            for (var i = 0; i < _links.Length; i++)
            {
                var from = centres[_links[i].FromIndex];
                var to = centres[_links[i].ToIndex];
                var delta = to - from;
                var line = new GameObject("Link");
                line.transform.SetParent(_field, false);
                var image = line.AddComponent<Image>();
                image.raycastTarget = false;
                var rect = line.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = from;
                rect.sizeDelta = new Vector2(delta.magnitude, 2f);
                rect.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                _linkLines[i] = image;
            }

            var markSize = Mathf.Max(12f, labelSize + 4f);
            _nodeMarks = new Image[entries.Count];
            _nodeCores = new Image[entries.Count];
            _nodeLabels = new Text[entries.Count];
            _nodeEpithets = showEpithets ? new Text[entries.Count] : null;
            _nodeRects = new RectTransform[entries.Count];
            _nodeBaseSize = new float[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                var centre = centres[i];
                // 45° square = diamond. Filled reads "cleared", the dark core
                // hollows it out for everything the player has not finished.
                var mark = new GameObject("Node");
                mark.transform.SetParent(_field, false);
                var markImage = mark.AddComponent<Image>();
                markImage.raycastTarget = false;
                var markRect = mark.GetComponent<RectTransform>();
                markRect.anchorMin = markRect.anchorMax = new Vector2(0f, 1f);
                markRect.pivot = new Vector2(0.5f, 0.5f);
                markRect.anchoredPosition = centre;
                markRect.sizeDelta = new Vector2(markSize, markSize);
                markRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                _nodeMarks[i] = markImage;
                _nodeRects[i] = markRect;
                _nodeBaseSize[i] = markSize;

                var core = new GameObject("NodeCore");
                core.transform.SetParent(mark.transform, false);
                var coreImage = core.AddComponent<Image>();
                coreImage.color = Charcoal;
                coreImage.raycastTarget = false;
                var coreRect = core.GetComponent<RectTransform>();
                coreRect.anchorMin = Vector2.zero;
                coreRect.anchorMax = Vector2.one;
                coreRect.offsetMin = new Vector2(3f, 3f);
                coreRect.offsetMax = new Vector2(-3f, -3f);
                _nodeCores[i] = coreImage;

                var labelWidth = 108f;
                _nodeLabels[i] = MakeText(_field,
                    new Vector2(centre.x - labelWidth * 0.5f, centre.y - markSize * 0.75f),
                    new Vector2(labelWidth, 14f), "", labelSize, TextAnchor.UpperCenter);

                if (!showEpithets) continue;
                _nodeEpithets[i] = MakeText(_field,
                    new Vector2(centre.x - labelWidth * 0.5f, centre.y - markSize * 0.75f - 15f),
                    new Vector2(labelWidth, 13f), "", labelSize - 2, TextAnchor.UpperCenter);
                _nodeEpithets[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f);
            }
        }

        /// <summary>Re-reads one save. Colour, text and link opacity only — the
        /// widget tree is never rebuilt, so a Refresh cannot reorder anything.</summary>
        public void Refresh(in CampaignData data)
        {
            if (_nodeMarks == null) return;
            var nodes = CampaignMapLayout.BuildNodes(in data);
            var entries = StageCatalog.Entries;
            _frontier = CampaignMapLayout.FrontierIndex(in data);
            _progress.text = CampaignMapLayout.ProgressLine(in data);

            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                var alpha = CampaignMapLayout.AlphaOf(node.State);
                var accent = entries[i].AccentColor;
                _nodeMarks[i].color = new Color(accent.r, accent.g, accent.b, alpha);
                // Cleared nodes lose the dark core: a solid diamond is the one
                // shape on this map that means "done".
                _nodeCores[i].color = node.State == CampaignNodeState.Cleared
                    ? new Color(accent.r, accent.g, accent.b, alpha)
                    : new Color(Charcoal.r, Charcoal.g, Charcoal.b, 0.95f);

                _nodeLabels[i].text = node.Label;
                _nodeLabels[i].color = node.State == CampaignNodeState.Cleared
                    ? new Color(Ink.r, Ink.g, Ink.b, 1f)
                    : new Color(Ink.r, Ink.g, Ink.b, alpha);
                if (_nodeEpithets != null)
                    _nodeEpithets[i].text = node.Epithet;

                // Reset any pulse the previous frontier left behind.
                _nodeRects[i].sizeDelta = new Vector2(_nodeBaseSize[i], _nodeBaseSize[i]);
            }

            var links = CampaignMapLayout.BuildLinks(in data);
            for (var i = 0; i < _linkLines.Length && i < links.Length; i++)
            {
                _linkLines[i].color = links[i].Lit
                    ? new Color(Ember.r, Ember.g, Ember.b, 0.85f)
                    : new Color(Cyan.r, Cyan.g, Cyan.b, 0.18f);
            }
        }

        /// <summary>Frontier heartbeat: the ONE node the player can attempt next
        /// breathes so the map answers "where do I go" without a legend. Pure
        /// size write, skipped entirely under reduced motion.</summary>
        public void Tick(float unscaledTime)
        {
            if (_nodeRects == null || _frontier < 0 || _frontier >= _nodeRects.Length) return;
            if (ViewPrefs.ReducedMotion) return;
            var phase = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(unscaledTime * (2f / 1.4f), 1f));
            var size = _nodeBaseSize[_frontier] * Mathf.Lerp(1f, 1.35f, phase);
            _nodeRects[_frontier].sizeDelta = new Vector2(size, size);
        }

        Text MakeText(Transform parent, Vector2 anchoredPosition, Vector2 size,
            string content, int fontSize, TextAnchor anchor)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            var text = labelObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.text = content;
            text.color = Ink;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return text;
        }
    }
}
