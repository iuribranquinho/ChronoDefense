using UnityEngine;

namespace ChronoDefense.Data
{
    /// <summary>GDD §10.1 — raiz da campanha (5 Eras × 100 níveis) e catálogos globais.</summary>
    [CreateAssetMenu(menuName = "Chrono/Campaign", fileName = "Campaign")]
    public sealed class CampaignSO : ScriptableObject
    {
        public EraSO[] Eras = new EraSO[BalanceFormulas.EraCount];

        [Header("Catálogos (Id estável → save/analytics)")]
        [Tooltip("22 heróis, índice = Id − 1")]
        public HeroDataSO[] Heroes = new HeroDataSO[22];
        public TowerDataSO[] Towers;

        /// <summary>L em 1..500.</summary>
        public LevelDataSO GetLevel(int globalLevel) =>
            Eras[BalanceFormulas.EraOf(globalLevel) - 1].GetLevel(BalanceFormulas.LevelInEra(globalLevel));

        public HeroDataSO GetHero(int heroId) => Heroes[heroId - 1];
    }
}
