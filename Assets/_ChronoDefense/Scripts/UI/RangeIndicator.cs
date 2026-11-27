// Indicador de alcance: plano 2D no chão (XZ), escala (raio×2, 0,01, raio×2). Sem esfera 3D.
using UnityEngine;
using UnityEngine.Rendering;

namespace ChronoDefense.UI
{
    public sealed class RangeIndicator : MonoBehaviour
    {
        public static readonly Color ValidColor = new Color(1f, 0.88f, 0.54f, 1f);   // #FFE08A
        public static readonly Color PreviewColor = new Color(0.44f, 0.83f, 1f, 1f); // destaque da Era 1

        MeshRenderer _renderer;
        MaterialPropertyBlock _mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");

        public void Init(ArtLibrary art)
        {
            gameObject.AddComponent<MeshFilter>().sharedMesh = art.QuadXZ;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            var mat = new Material(art.SpriteUnlit) { name = "MAT_RangeIndicator", mainTexture = art.RangeRing.texture };
            _renderer.sharedMaterial = mat;
            _renderer.sortingOrder = -60;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _mpb = new MaterialPropertyBlock();
            Hide();
        }

        public void Show(Vector3 center, float radius, Color color)
        {
            transform.position = new Vector3(center.x, 0.05f, center.z);
            transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(RendererColorId, color);
            _renderer.SetPropertyBlock(_mpb);
            _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        public bool Visible => _renderer != null && _renderer.enabled;

        void OnDestroy()
        {
            if (_renderer != null && _renderer.sharedMaterial != null) Destroy(_renderer.sharedMaterial);
        }
    }
}
