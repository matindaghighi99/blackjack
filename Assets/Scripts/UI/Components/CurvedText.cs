using TMPro;
using UnityEngine;

namespace BlackjackGame.UI.Components
{
    /// <summary>
    /// Bends a TextMeshPro line along a shallow arc, the way lettering is printed on a
    /// blackjack felt — concentric with the table's curved front edge.
    ///
    /// Works on the generated mesh: after TMP builds the glyph quads, each is moved onto
    /// the arc and turned to follow its tangent. The text stays live (it comes from the
    /// active rule set), so the felt can never advertise a payout the engine doesn't pay.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class CurvedText : MonoBehaviour
    {
        [Tooltip("Arc radius in canvas units. Larger is flatter. The arc is a 'smile': the " +
                 "ends of the line sit higher than its middle.")]
        [SerializeField] private float _radius = 1400f;

        private TMP_Text _text;
        private bool _dirty = true;
        private bool _warping;

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            _dirty = true;
        }

        private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        public void SetRadius(float radius)
        {
            if (Mathf.Approximately(radius, _radius)) return;
            _radius = radius;
            _dirty = true;
        }

        private void OnTextChanged(Object changed)
        {
            // TMP regenerated the mesh (text, size or scale changed), undoing the warp.
            if (!_warping && changed == _text) _dirty = true;
        }

        private void LateUpdate()
        {
            if (_dirty) Warp();
        }

        private void Warp()
        {
            _dirty = false;
            if (_text == null || _radius <= 1f) return;

            _warping = true;
            _text.ForceMeshUpdate();
            _warping = false;

            TMP_TextInfo info = _text.textInfo;
            if (info == null || info.characterCount == 0) return;

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible) continue;

                Vector3[] v = info.meshInfo[ch.materialReferenceIndex].vertices;
                int vi = ch.vertexIndex;

                // Pivot each glyph about the midpoint of its baseline.
                float midX = (v[vi].x + v[vi + 2].x) * 0.5f;
                var pivot = new Vector3(midX, ch.baseLine, 0f);

                float angle = midX / _radius;
                // Ends rise: y grows with distance from the centre (UI space is y-up).
                var onArc = new Vector3(_radius * Mathf.Sin(angle), ch.baseLine + _radius * (1f - Mathf.Cos(angle)), 0f);
                Quaternion turn = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);

                for (int k = 0; k < 4; k++)
                    v[vi + k] = onArc + turn * (v[vi + k] - pivot);
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}
