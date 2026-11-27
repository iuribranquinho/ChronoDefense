// Texto flutuante (ouro por abate) servido pelo ComponentPool (GDD §3.4). Zero-GC: SetText com formato.
using ChronoDefense.Core;
using TMPro;
using UnityEngine;

namespace ChronoDefense.UI
{
    public sealed class FloatyText : MonoBehaviour, IPoolable
    {
        public const float Lifetime = 0.9f;
        TextMeshPro _text;
        float _age;
        Vector3 _start;
        public bool Alive => _age < Lifetime;

        public static FloatyText Create(Transform parent, Quaternion billboard, TMP_FontAsset font)
        {
            var go = new GameObject("FloatyText");
            go.transform.SetParent(parent, false);
            go.transform.rotation = billboard;
            var f = go.AddComponent<FloatyText>();
            f._text = go.AddComponent<TextMeshPro>();
            if (font != null) f._text.font = font;
            f._text.fontSize = 9f;
            f._text.alignment = TextAlignmentOptions.Center;
            f._text.color = new Color(1f, 0.85f, 0.25f);
            f._text.outlineWidth = 0.25f;
            f._text.outlineColor = new Color32(40, 24, 8, 255);
            f._text.rectTransform.sizeDelta = new Vector2(4f, 1.2f);
            f._text.sortingOrder = 50;
            return f;
        }

        public void Show(Vector3 worldPos, int gold)
        {
            _start = worldPos + Vector3.up * 1.2f;
            transform.position = _start;
            _text.SetText("+{0}", gold);
            _age = 0f;
        }

        public void Tick(float dt)
        {
            _age += dt;
            float k = _age / Lifetime;
            transform.position = _start + Vector3.up * (k * 0.9f);
            Color c = _text.color;
            c.a = 1f - k * k;
            _text.color = c;
        }

        public void OnRent() { _age = 0f; }
        public void OnReturn() { _age = Lifetime; }
    }
}
