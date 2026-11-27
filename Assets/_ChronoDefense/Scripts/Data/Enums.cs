// Enums compartilhados pela camada de dados (GDD v2.0).
// Todos são byte/uint-backed e blittable, para poderem ser copiados para NativeArray
// sem boxing (GDD §3.3: chaves int, sem enum como chave genérica).
using System;

namespace ChronoDefense.Data
{
    /// <summary>GDD §5.1. Físico → Armadura; Mágico → RM; Verdadeiro ignora mitigação.</summary>
    public enum DamageType : byte
    {
        Physical = 0,
        Magical = 1,
        True = 2,
    }

    /// <summary>GDD §7.1. O índice é usado em <see cref="EraBiomeThemeSO.TowerSkins"/>.</summary>
    public enum TowerArchetype : byte
    {
        Marksman = 0,   // Atirador
        Launcher = 1,   // Lançador
        Alchemist = 2,  // Alquimista
        Guard = 3,      // Guarda
    }

    /// <summary>GDD §9.1 — bitmask de atributos de inimigo.</summary>
    [Flags]
    public enum EnemyTraits : uint
    {
        None        = 0,
        Armored     = 1u << 0,
        MagicResist = 1u << 1,
        Flying      = 1u << 2,
        Camo        = 1u << 3,
        Regen       = 1u << 4,
        Shield      = 1u << 5,
        Healer      = 1u << 6,
        Spawner     = 1u << 7,
        Light       = 1u << 8,   // morre na hora para Julius
        Undead      = 1u << 9,   // morre na hora para Yeshua
        Shadow      = 1u << 10,  // morre na hora para Yeshua
        Boss        = 1u << 11,  // regras de chefe (GDD §5.3)
    }

    /// <summary>GDD §10.3.</summary>
    public enum SkillTargeting : byte
    {
        Point = 0,
        Global = 1,
        Self = 2,
        Line = 3,
        Sector = 4,
    }

    /// <summary>
    /// GDD §10.3 — efeitos interpretados pelo SkillSystem (Burst).
    /// Convenção para <see cref="Shred"/>: o campo <c>DamageType</c> do efeito indica a resistência
    /// afetada (Physical → Armadura, Magical → RM, True → ambas).
    /// </summary>
    public enum EffectType : byte
    {
        Damage = 0,
        DoT = 1,
        Slow = 2,
        Stun = 3,
        Knockback = 4,
        Knockup = 5,
        Confuse = 6,
        Shred = 7,
        Vulnerability = 8,
        RemoveShield = 9,
        Pull = 10,
        Reflect = 11,
        HealCore = 12,
        CoreShield = 13,
        CritBuff = 14,
        AttackSpeedBuff = 15,
        Execute = 16,
        // Extensões além da lista do §10.3 (necessárias para §6.2 / §7.2):
        Polymorph = 17,   // Alquimista C T5
        Summon = 18,      // tanque do Leonardo, prensa do Gutenberg
        CrowdBonus = 19,  // Krog: +Magnitude de dano por inimigo extra na zona (teto +150%, GDD §6.2)
    }

    /// <summary>GDD §10.5 — ações de fase de chefe.</summary>
    public enum BossAction : byte
    {
        Summon = 0,
        DisablePedestal = 1,
        Shield = 2,
        MapChange = 3,
        StunTowers = 4,   // Rinoceronte, Dario III, Trabuco (Bíblia §6.2)
        Regen = 5,        // Urso-das-Cavernas, Hidra
    }

    /// <summary>Atributo alvo de um <see cref="StatModifier"/> (GDD §10.4).</summary>
    public enum StatId : byte
    {
        Damage = 0,
        Interval = 1,
        Range = 2,
        SplashRadius = 3,
        ProjectileCount = 4,
        PierceCount = 5,
        CritChance = 6,
        ArmorIgnore = 7,
        SoldierCount = 8,
        SoldierHp = 9,
        SoldierArmor = 10,
        SoldierDamage = 11,
        LifeSteal = 12,
    }

    public enum ModifierOp : byte
    {
        Add = 0,
        Mul = 1,
    }

    /// <summary>GDD §4.5.</summary>
    public enum TargetPriority : byte
    {
        First = 0,
        Last = 1,
        Strongest = 2,
        Closest = 3,
    }

    /// <summary>Bíblia §8 — revisão obrigatória por herói.</summary>
    [Flags]
    public enum SensitivityFlags : byte
    {
        None               = 0,
        CulturalReview     = 1 << 0,  // heróis 7, 8, 15, 18, 21, 22
        Aniconic           = 1 << 1,  // rosto nunca modelado (Al-Mu'allim)
        NoMeleeCombat      = 1 << 2,  // sem armas/golpes (Yeshua, Al-Mu'allim)
        NoHumor            = 1 << 3,
        ControlledSymbols  = 1 << 4,  // foice e martelo, insígnias, bandeiras
        PersonalityRights  = 1 << 5,  // espólios (Einstein, Gandhi, Turing, Armstrong)
    }
}
