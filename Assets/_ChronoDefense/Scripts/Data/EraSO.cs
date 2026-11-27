using UnityEngine;
using UnityEngine.Localization;

namespace ChronoDefense.Data
{
    /// <summary>GDD §10.1 — uma Era (100 níveis = 10 capítulos).</summary>
    [CreateAssetMenu(menuName = "Chrono/Era", fileName = "ERA_1_PreHistory")]
    public sealed class EraSO : ScriptableObject
    {
        public int Id;
        [Range(1, 5)] public int EraIndex = 1;
        public LocalizedString DisplayName;
        public EraBiomeThemeSO Theme;
        [Tooltip("100 níveis, índice = l − 1")]
        public LevelDataSO[] Levels = new LevelDataSO[BalanceFormulas.LevelsPerEra];

        public float Power => BalanceFormulas.EraPower(EraIndex);       // P(e)
        public float HpFactor => BalanceFormulas.EraHpFactor(EraIndex); // K_era(e)

        /// <summary>levelInEra em 1..100.</summary>
        public LevelDataSO GetLevel(int levelInEra) => Levels[levelInEra - 1];
    }
}
