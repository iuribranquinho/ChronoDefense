using System;
using UnityEngine;

namespace ChronoDefense.Data
{
    /// <summary>GDD §10.5. BaseHp é o HP_tipo sem escala (§8.3); a escala é aplicada no carregamento.</summary>
    [CreateAssetMenu(menuName = "Chrono/Enemy", fileName = "CRP_E1_Enemy")]
    public class EnemyDataSO : ScriptableObject
    {
        public int Id;
        public float BaseHp;
        public float Armor;
        public float MagicResist;
        [Tooltip("m/s")]
        public float Speed;
        public EnemyTraits Traits;
        [Tooltip("Vidas custadas ao vazar: creep 1, elite 2 (chefe = todas, §1.2)")]
        public int LeakWeight = 1;
        [Tooltip("Leve 10 · Média 30 · Pesada 80 · Chefe 200 (Newton, §6.2)")]
        public float Mass = 10f;

        [Header("Instancing VAT (§3.5)")]
        public Mesh VatMesh;
        public Material VatMaterial;

        [Tooltip("-1 → ceil(BaseHp / 20)")]
        public int GoldOverride = -1;

        /// <summary>Atribuído no prewarm do ObjectPoolManager (§3.4). Nunca serializado.</summary>
        [NonSerialized] public int PoolId = -1;

        public int KillGold => GoldOverride >= 0 ? GoldOverride : BalanceFormulas.KillGold(BaseHp);
        public bool HasTrait(EnemyTraits t) => (Traits & t) == t;
    }
}
