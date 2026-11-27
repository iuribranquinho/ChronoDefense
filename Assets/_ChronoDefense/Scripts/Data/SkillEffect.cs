using System;

namespace ChronoDefense.Data
{
    /// <summary>
    /// GDD §10.3 — efeito de habilidade/on-hit. Blittable: copiado para NativeArray no carregamento
    /// e interpretado pelo SkillSystem em Burst. Habilidades são dados, não classes.
    /// </summary>
    [Serializable]
    public struct SkillEffect
    {
        public EffectType Type;
        public DamageType DamageType;
        public float Magnitude;
        public float Duration;
        public float TickInterval;
        /// <summary>Multiplicador contra chefes (ex.: Confuse → 0 vira Slow, Knockback → 0,25).</summary>
        public float BossMultiplier;
        /// <summary>Filtro de alvo (ex.: Execute só em Undead|Shadow). None = todos.</summary>
        public EnemyTraits RequiresTraits;
    }

    /// <summary>GDD §10.4 — modificador Add/Mul aplicado por um tier de upgrade.</summary>
    [Serializable]
    public struct StatModifier
    {
        public StatId Stat;
        public ModifierOp Op;
        public float Value;
    }
}
