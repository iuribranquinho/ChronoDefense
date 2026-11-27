using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace ChronoDefense.Data
{
    /// <summary>GDD §8.4 / §10.5. Chefes usam SkinnedMesh (ViewPrefab), não VAT; não usam α.</summary>
    [CreateAssetMenu(menuName = "Chrono/Boss", fileName = "BOS_000_Boss")]
    public sealed class BossDataSO : EnemyDataSO
    {
        public LocalizedString DisplayName;          // barra do chefe (Bíblia §3.9)
        [Tooltip("B_chefe (§8.4) — HP final = B × K_era × K_in × Saw(10)")]
        public float HpBase;
        public BossPhase[] Phases;
        public AssetReferenceGameObject ViewPrefab;
        public AssetReferenceGameObject LairPrefab;  // covil (Bíblia §3.6)
        public AssetReferenceSprite Portrait;
        public LocalizedString LairSignText;         // "Krog's Lair"

        public float GetScaledHp(int globalLevel) => BalanceFormulas.BossHp(HpBase, globalLevel);

#if UNITY_EDITOR
        private void OnValidate()
        {
            Traits |= EnemyTraits.Boss;
            if (GoldOverride < 0) GoldOverride = BalanceFormulas.BossKillGold;
        }
#endif
    }

    [Serializable]
    public struct BossPhase
    {
        [Tooltip("Dispara quando HP ≤ limiar (0..1). 1 = desde o início (ações periódicas).")]
        [Range(0, 1)] public float HpThreshold;
        public BossAction Action;
        [Tooltip("Repetição em s (0 = uma vez)")]
        public float Interval;
        public float Value;
        public EnemyDataSO SummonType;
        public int SummonCount;
    }
}
