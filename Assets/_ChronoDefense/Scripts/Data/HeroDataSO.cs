using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace ChronoDefense.Data
{
    /// <summary>GDD §6.2 / §10.3. Valores normalizados na Era 1; em runtime multiplicar HP e dano por P(e).</summary>
    [CreateAssetMenu(menuName = "Chrono/Hero", fileName = "HER_00_Hero")]
    public sealed class HeroDataSO : ScriptableObject
    {
        [Tooltip("1..22, numeração da Bíblia §5")]
        public int Id;
        public LocalizedString DisplayName;
        [Range(1, 5)] public int Era = 1;
        [Tooltip("Nível global do resgate (ex.: Krog = 10)")]
        public int UnlockLevel;
        public int SummonCost;

        [Header("Atributos (Era 1)")]
        public float MaxHp;
        public float Armor;
        public float MagicResist;

        [Header("Ataque básico")]
        public DamageType AttackType;
        [Tooltip("0 = não causa dano (ex.: Bapu)")]
        public float AttackDamage;
        public float AttackInterval;
        public float AttackRange;
        [Tooltip("Projéteis/alvos por ataque (ex.: Trino Socrático = 3)")]
        public int AttackHits = 1;
        public float GuardRadius = BalanceFormulas.DefaultGuardRadius;
        [Tooltip("Usado pela Singularidade de Newton (Leve 10, Média 30, Pesada 80)")]
        public float MassClass = 30f;

        [Header("Habilidade e apresentação")]
        public SkillDataSO Skill;
        public AssetReferenceGameObject ViewPrefab;   // SkinnedMesh + Animator
        public AssetReferenceSprite Portrait;
        public SensitivityFlags Sensitivity;          // Bíblia §8 (revisão obrigatória)
    }
}
