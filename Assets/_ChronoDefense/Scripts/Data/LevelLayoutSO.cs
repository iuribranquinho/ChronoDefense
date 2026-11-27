using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ChronoDefense.Data
{
    /// <summary>
    /// Acabamento do chão (bioma do mapa da saga, 27/11). Auto = bioma da Era.
    /// Era 1 segue a estrada do mapa da saga: Mata (1–4) → Savana (5–7) → Deserto (8, portão) → Planalto (9) → Tundra (10, covil).
    /// Os valores antigos (Auto, Tundra, Meadow) ficam nos mesmos números para não estragar assets já salvos.
    /// </summary>
    public enum GroundStyle : byte { Auto, Tundra, Meadow, Forest, Savanna, Desert, Highland }

    /// <summary>
    /// GDD §10.6 — layout reutilizável entre níveis (~20 por Era).
    /// Coordenadas em metros no plano XZ; o mapa cabe em 128 × 128 m (flow field §4.3).
    /// </summary>
    [CreateAssetMenu(menuName = "Chrono/Level Layout", fileName = "LAY_E1_00")]
    public sealed class LevelLayoutSO : ScriptableObject
    {
        public const float MapSize = 128f;

        public int Id;
        [Tooltip("Nome do mapa no menu (ex.: Vale Gelado)")]
        public string DisplayName;
        [Tooltip("Do covil/portal ao Núcleo; usado no bake do flow field")]
        public Vector3[] PathPolyline;
        [Tooltip("2,0 comum · 2,5 chefe (Bíblia §3.5)")]
        public float PathWidth = 2f;
        [Tooltip("Pontos sugeridos (invisíveis). Desde 26/11 o posicionamento é livre: qualquer chão fora do caminho")]
        public Vector3[] Pedestals;
        public Vector3 LairPosition;
        public Vector3 CorePosition;
        [Tooltip("Desligado = estilo BTD6: sem caverna nem Núcleo; os inimigos entram e saem pelas bordas da tela " +
                 "(o caminho começa e termina fora do CameraBounds)")]
        public bool ShowLairAndCore = false;
        [Tooltip("Auto = bioma da Era. Meadow = prado verde do mapa-modelo de teste")]
        public GroundStyle Ground = GroundStyle.Auto;
        public Bounds CameraBounds;
        public AssetReferenceGameObject EnvironmentArt;

    }
}
