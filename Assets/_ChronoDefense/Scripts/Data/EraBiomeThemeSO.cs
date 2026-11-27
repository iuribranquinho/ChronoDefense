using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;

namespace ChronoDefense.Data
{
    /// <summary>
    /// GDD §10.2 — tema visual de uma Era (cores e iluminação seguem a Bíblia §4).
    /// Consumido apenas pelo LevelRenderer em LoadAsync (GDD §11.3).
    /// </summary>
    [CreateAssetMenu(menuName = "Chrono/Era Biome Theme", fileName = "THM_E1_Tundra")]
    public sealed class EraBiomeThemeSO : ScriptableObject
    {
        public int Id;
        [Range(1, 5)] public int EraIndex = 1;

        [Header("Paleta (Bíblia §4)")]
        [Tooltip("Cor das runas do caminho (destaque da era)")]
        public Color AccentRune;
        [Tooltip("Cor da sombra no toon ramp (nunca preto puro)")]
        public Color ShadowTint;
        [Tooltip("Violeta da Entropia #3B1E54 (Bíblia §3.7)")]
        public Color CorruptionTint = new Color(0.23f, 0.12f, 0.33f);

        [Header("Iluminação URP (Bíblia §3.3)")]
        public Color MainLightColor = Color.white;
        public float MainLightIntensity = 1f;
        public Vector3 MainLightEuler = new Vector3(50f, 135f, 0f);
        public Color AmbientSky;
        public Color AmbientEquator;
        public Color AmbientGround;
        public Color FogColor;
        public float FogStart = 40f;
        public float FogEnd = 90f;
        public AssetReferenceT<VolumeProfile> PostProcess;

        [Header("Prefabs de cenário")]
        public AssetReferenceGameObject Backdrop;    // céu e montanhas (cards)
        public AssetReferenceGameObject Pedestal;
        public AssetReferenceGameObject RuneDecal;
        public AssetReferenceGameObject Portal;
        public AssetReferenceGameObject Core;        // Núcleo Cronológico
        public PropSet[] Props;                      // conjuntos ponderados por sub-bioma

        [Header("Skins de torre (índice = TowerArchetype)")]
        public TowerSkin[] TowerSkins = new TowerSkin[4];

        public AudioCueSO Music;
    }

    [Serializable]
    public struct TowerSkin
    {
        public TowerArchetype Archetype;
        public AssetReferenceGameObject BaseL1;
        public AssetReferenceGameObject[] PathL2;    // 3 (um por caminho)
        public AssetReferenceGameObject[] PathL3;    // 3
    }

    /// <summary>Props de um sub-bioma (Bíblia §4: ex. Cap. 1–3 Tundra Gelada).</summary>
    [Serializable]
    public struct PropSet
    {
        public string SubBiomeName;                  // só editor/designer
        [Tooltip("Capítulos (1..50) em que este conjunto vale")]
        public int FirstChapter;
        public int LastChapter;
        public WeightedProp[] Entries;
    }

    [Serializable]
    public struct WeightedProp
    {
        public AssetReferenceGameObject Prefab;
        [Min(0f)] public float Weight;
        [Tooltip("Props > 1,5 m proibidos na metade inferior da tela (Bíblia §3.8)")]
        public bool IsTall;
    }
}
