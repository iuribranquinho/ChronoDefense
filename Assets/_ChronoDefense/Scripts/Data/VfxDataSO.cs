using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ChronoDefense.Data
{
    /// <summary>GDD §3.4 / §10.1 — efeito visual servido pelo ComponentPool.</summary>
    [CreateAssetMenu(menuName = "Chrono/VFX", fileName = "VFX_Effect")]
    public sealed class VfxDataSO : ScriptableObject
    {
        public int Id;
        public AssetReferenceGameObject Prefab;
        [Tooltip("Tamanho padrão do pool (8–32); o PoolManifest pode sobrescrever")]
        [Range(1, 64)] public int DefaultPoolSize = 8;
        [Tooltip("s até devolver ao pool")]
        public float Lifetime = 1f;

        /// <summary>Atribuído no prewarm do ObjectPoolManager (§3.4). Nunca serializado.</summary>
        [NonSerialized] public int PoolId = -1;
    }
}
