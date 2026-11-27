using System;
using UnityEngine;
using UnityEngine.Localization;

namespace ChronoDefense.Data
{
    /// <summary>GDD §7 / §10.4. Custos iguais em todas as Eras; dano × P(e) em runtime; skin vem do EraBiomeThemeSO.</summary>
    [CreateAssetMenu(menuName = "Chrono/Tower", fileName = "TWR_Tower")]
    public sealed class TowerDataSO : ScriptableObject
    {
        public int Id;
        public TowerArchetype Archetype;
        public int BaseCost;

        [Header("Ataque (Era 1)")]
        public DamageType DamageType;
        public float Damage;
        public float Interval;
        public float Range;
        public float SplashRadius;
        public bool HitsFlying;
        public bool DetectsCamo;

        [Header("Guarda (só Archetype = Guard)")]
        public int SoldierCount;
        public float SoldierHp;
        public float SoldierDamage;
        public float SoldierInterval;

        [Header("Caminhos A, B, C")]
        public UpgradePath[] Paths = new UpgradePath[UpgradeState.PathCount];

        /// <summary>Custo de comprar o tier t (1..5) em qualquer caminho: B × k_t (§7.3).</summary>
        public int GetTierCost(int path, int tier)
        {
            float k = Paths[path].Tiers[tier - 1].CostMultiplier;
            return Mathf.RoundToInt(BaseCost * k);
        }

        /// <summary>
        /// Atributo com os upgrades comprados: (base + ΣAdd) × ΠMul sobre todos os tiers comprados
        /// em todos os caminhos. Sem alocação; chamado no carregamento e ao comprar um upgrade.
        /// </summary>
        public float GetStat(StatId stat, float baseValue, in UpgradeState state)
        {
            float add = 0f, mul = 1f;
            for (int p = 0; p < Paths.Length; p++)
            {
                UpgradeTier[] tiers = Paths[p].Tiers;
                if (tiers == null) continue;
                int bought = state.Get(p);
                for (int k = 0; k < bought && k < tiers.Length; k++)
                {
                    StatModifier[] mods = tiers[k].Modifiers;
                    if (mods == null) continue;
                    for (int m = 0; m < mods.Length; m++)
                    {
                        if (mods[m].Stat != stat) continue;
                        if (mods[m].Op == ModifierOp.Add) add += mods[m].Value;
                        else mul *= mods[m].Value;
                    }
                }
            }
            return (baseValue + add) * mul;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Paths == null || Paths.Length != UpgradeState.PathCount)
                Array.Resize(ref Paths, UpgradeState.PathCount);

            for (int p = 0; p < Paths.Length; p++)
            {
                ref UpgradePath path = ref Paths[p];
                if (path.Tiers == null || path.Tiers.Length != UpgradeState.MaxTier)
                {
                    int oldLen = path.Tiers?.Length ?? 0;
                    Array.Resize(ref path.Tiers, UpgradeState.MaxTier);
                    for (int t = oldLen; t < UpgradeState.MaxTier; t++)
                        path.Tiers[t].CostMultiplier = BalanceFormulas.TierCostMultipliers[t];
                }
            }
        }
#endif
    }

    [Serializable]
    public struct UpgradePath
    {
        public LocalizedString Name;
        public UpgradeTier[] Tiers;   // 5
    }

    [Serializable]
    public struct UpgradeTier
    {
        [Tooltip("k_t (§7.3): 0,6 · 1 · 2 · 5 · 15")]
        public float CostMultiplier;
        [Tooltip("Acumulam com os tiers anteriores: (base + ΣAdd) × ΠMul")]
        public StatModifier[] Modifiers;
        [Tooltip("Substituem os do tier anterior do mesmo caminho (vale o maior tier comprado)")]
        public SkillEffect[] OnHitEffects;
        public bool GrantsCamoDetection;
        [Tooltip("Só para o pipeline de arte (editor)")]
        public string SocketAccessory;
    }
}
