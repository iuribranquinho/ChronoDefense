using System;
using UnityEngine;

namespace ChronoDefense.Data
{
    /// <summary>GDD §10.5. O PoolManifest é calculado a partir destes dados no carregamento (§3.4).</summary>
    [CreateAssetMenu(menuName = "Chrono/Wave", fileName = "WAV_000_00")]
    public sealed class WaveDataSO : ScriptableObject
    {
        public SpawnGroup[] Groups;
        [Tooltip("-1 → 50 + 5w")]
        public int BonusGoldOverride = -1;

        public int GetBonusGold(int waveNumber) =>
            BonusGoldOverride >= 0 ? BonusGoldOverride : BalanceFormulas.WaveGold(waveNumber);

        public int TotalCount
        {
            get
            {
                int n = 0;
                if (Groups != null)
                    for (int i = 0; i < Groups.Length; i++) n += Groups[i].Count;
                return n;
            }
        }
    }

    [Serializable]
    public struct SpawnGroup
    {
        public EnemyDataSO Enemy;
        public int Count;
        [Tooltip("s desde o início da onda")]
        public float StartDelay;
        [Tooltip("s entre spawns do grupo")]
        public float Interval;
        [Tooltip("Portal ou covil")]
        public int SpawnPointIndex;
    }
}
