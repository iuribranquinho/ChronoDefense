using System;
using UnityEngine;

namespace ChronoDefense.Data
{
    /// <summary>GDD §10.6 — um dos 500 níveis.</summary>
    [CreateAssetMenu(menuName = "Chrono/Level", fileName = "LVL_000")]
    public sealed class LevelDataSO : ScriptableObject
    {
        [Tooltip("L (1..500)")]
        [Range(1, BalanceFormulas.TotalLevels)] public int GlobalIndex = 1;
        public EraSO Era;
        public LevelLayoutSO Layout;
        public WaveDataSO[] Waves;
        [Tooltip("Só quando p = 10")]
        public BossDataSO Boss;
        [Tooltip("-1 → G0(l) = min(800, 300 + 5(l−1))")]
        public int StartGoldOverride = -1;
        public int Lives = 20;
        [Tooltip("Bioma desta fase (mapa da saga). Auto = o do layout; se o layout também for Auto, o da Era")]
        public GroundStyle Biome = GroundStyle.Auto;
        public HeroDataSO UnlocksHero;
        public LevelRewards Rewards;

        public int EraIndex => BalanceFormulas.EraOf(GlobalIndex);
        public int LevelInEra => BalanceFormulas.LevelInEra(GlobalIndex);
        public int ChapterPosition => BalanceFormulas.ChapterPosition(GlobalIndex);
        public bool IsBossLevel => BalanceFormulas.IsBossLevel(GlobalIndex);
        /// <summary>Bioma efetivo: o da fase, senão o do layout, senão o da Era (Era 1 = Tundra).</summary>
        public GroundStyle ResolvedBiome =>
            Biome != GroundStyle.Auto ? Biome
            : Layout != null && Layout.Ground != GroundStyle.Auto ? Layout.Ground
            : EraIndex == 1 ? GroundStyle.Tundra : GroundStyle.Meadow;
        public int StartGold => StartGoldOverride >= 0 ? StartGoldOverride : BalanceFormulas.StartGold(LevelInEra);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!IsBossLevel && Boss != null)
                Debug.LogWarning($"[{name}] Boss definido em nível não-chefe (p = {ChapterPosition}).", this);
            if (IsBossLevel && Boss == null)
                Debug.LogWarning($"[{name}] Nível de chefe (p = 10) sem BossDataSO.", this);
            if (Era != null && Era.EraIndex != EraIndex)
                Debug.LogWarning($"[{name}] EraSO {Era.EraIndex} não corresponde a ceil(L/100) = {EraIndex}.", this);
        }
#endif
    }

    [Serializable]
    public struct LevelRewards
    {
        public int AccountGold;
        public int AccountXp;
        public TowerDataSO UnlocksTower;
        public int HeroFragments;
        [Tooltip("0 = nenhum")]
        public int CosmeticId;
    }
}
