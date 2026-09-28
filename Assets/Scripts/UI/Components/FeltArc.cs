using UnityEngine;
using UnityEngine.UI;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// The table's padded front rail: a band that follows a shallow circular arc across the
    /// screen, shaded across its thickness by a colour profile (gold trim on the felt side,
    /// then lit leather falling into shadow), with the dark apron below it filled in.
    ///
    /// Generated as a mesh rather than drawn as a sprite, so it is crisp at any resolution
    /// and simply re-computes for any aspect ratio — the same component is the table edge
    /// on a tall phone and on a 16:9 monitor.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FeltArc : MaskableGraphic
    {
        [Tooltip("Band centre line at the rect's horizontal centre, measured down from the rect's top edge.")]
        [SerializeField] private float _centreFromTop = 900f;
        [Tooltip("How much lower the middle of the arc is than its ends, over the reference chord.")]
        [SerializeField] private float _sag = 70f;
        [Tooltip("Half the width the sag is measured over (the arc continues past it).")]
        [SerializeField] private float _halfChord = 580f;
        [SerializeField] private float _thickness = 46f;
        [Tooltip("How far past the rect's sides the band continues, so it always reaches the screen edges.")]
        [SerializeField] private float _overscan = 800f;
        [SerializeField, Range(8, 160)] private int _segments = 96;

        [Header("Shading (felt side first)")]
        [SerializeField] private float[] _stops = { 0f, 0.07f, 0.1f, 0.4f, 0.8f, 1f };
        [SerializeField] private Color[] _colors =
        {
            new Color(0.788f, 0.643f, 0.361f),
            new Color(0.788f, 0.643f, 0.361f),
            new Color(0.188f, 0.173f, 0.157f),
            new Color(0.102f, 0.094f, 0.086f),
            new Color(0.035f, 0.039f, 0.039f),
            new Color(0.035f, 0.039f, 0.039f),
        };

        [Header("Apron")]
        [SerializeField] private bool _fillBelow = true;
        [SerializeField] private Color _fillColor = new Color(0.039f, 0.047f, 0.043f);
        [SerializeField] private float _fillDepth = 4000f;

        /// <summary>Re-shapes the arc; the mesh rebuilds on the next canvas update.</summary>
        public void Configure(float centreFromTop, float sag, float halfChord, float thickness)
        {
            _centreFromTop = centreFromTop;
            _sag = Mathf.Max(0.5f, sag);
            _halfChord = Mathf.Max(1f, halfChord);
            _thickness = Mathf.Max(1f, thickness);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int rows = Mathf.Min(_stops?.Length ?? 0, _colors?.Length ?? 0);
            if (rows < 2) return;

            Rect r = rectTransform.rect;
            float cx = r.center.x;
            float top = r.yMax;
            float radius = (_halfChord * _halfChord + _sag * _sag) / (2f * _sag);
            // Circle centre, in "down from the top" terms, above the band's lowest point.
            float centreDown = _centreFromTop - radius;

            float reach = Mathf.Min(r.width / 2f + _overscan, radius * 0.995f);
            int columns = _segments + 1;
            Color tint = color;

            for (int s = 0; s < columns; s++)
            {
                float dx = Mathf.Lerp(-reach, reach, s / (float)_segments);
                float root = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dx * dx));
                float lineDown = centreDown + root;
                // Outward normal of the circle, still in down-positive terms.
                float nx = dx / radius;
                float nyDown = root / radius;

                for (int k = 0; k < rows; k++)
                {
                    float offset = (_stops[k] - 0.5f) * _thickness;
                    float x = cx + dx + nx * offset;
                    float down = lineDown + nyDown * offset;
                    vh.AddVert(new Vector3(x, top - down, 0f), (Color32)(_colors[k] * tint), Vector4.zero);
                }

                if (_fillBelow)
                {
                    float offset = 0.5f * _thickness;
                    float x = cx + dx + nx * offset;
                    float down = lineDown + nyDown * offset;
                    Color32 fill = _fillColor * tint;
                    vh.AddVert(new Vector3(x, top - down, 0f), fill, Vector4.zero);
                    vh.AddVert(new Vector3(x, top - down - _fillDepth, 0f), fill, Vector4.zero);
                }
            }

            int perColumn = rows + (_fillBelow ? 2 : 0);
            for (int s = 0; s < _segments; s++)
            {
                int a = s * perColumn;
                int b = (s + 1) * perColumn;
                for (int k = 0; k < rows - 1; k++)
                {
                    vh.AddTriangle(a + k, b + k, b + k + 1);
                    vh.AddTriangle(a + k, b + k + 1, a + k + 1);
                }
                if (_fillBelow)
                {
                    int f = rows;
                    vh.AddTriangle(a + f, b + f, b + f + 1);
                    vh.AddTriangle(a + f, b + f + 1, a + f + 1);
                }
            }
        }
    }
}
