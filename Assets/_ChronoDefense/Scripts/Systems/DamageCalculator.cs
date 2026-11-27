// GDD §5 — matemática de combate. Funções estáticas puras, Burst-compatíveis.
using ChronoDefense.Data;
using Unity.Mathematics;

namespace ChronoDefense.Systems
{
    public static class DamageCalculator
    {
        public const float CritMultiplier = 2f;
        public const float MaxVulnerability = 1f;   // teto de +100%
        public const float MaxSlow = 0.9f;
        public const float MaxBossSlow = 0.6f;
        public const float BossStunMul = 0.5f;
        public const float BossCcWindow = 10f, BossCcImmune = 5f;
        public const int BossCcLimit = 3;

        /// <summary>
        /// D_final = D_base × (1 + ΣB_atk) × C × Mit × (1 + ΣV).
        /// R_ef = max(0, R × (1 − shred) − penFlat). Verdadeiro ignora mitigação.
        /// </summary>
        public static float Compute(float baseDamage, float attackerBuffs, bool crit, byte damageType,
                                    float resist, float shred, float flatPen, float vulnerability)
        {
            float mit = 1f;
            if (damageType != (byte)DamageType.True)
            {
                float rEff = math.max(0f, resist * (1f - math.saturate(shred)) - flatPen);
                mit = 100f / (100f + rEff);
            }
            float c = crit ? CritMultiplier : 1f;
            float v = math.min(vulnerability, MaxVulnerability);
            return baseDamage * (1f + attackerBuffs) * c * mit * (1f + v);
        }

        /// <summary>Dano final contra um creep, usando o maior shred ativo (sem somar, §5.2).</summary>
        public static float AgainstCreep(in CreepState c, float baseDamage, byte damageType, bool crit, float attackerShred)
        {
            bool magical = damageType == (byte)DamageType.Magical;
            float resist = magical ? c.MagicResist : c.Armor;
            float targetShred = magical ? c.ShredMagic : c.ShredArmor;
            float shred = math.max(targetShred, attackerShred);
            return Compute(baseDamage, 0f, crit, damageType, resist, shred, 0f, c.Vuln);
        }

        /// <summary>Escudo absorve antes do HP (§5.2). Marca morte.</summary>
        public static void ApplyDamage(ref CreepState c, float damage)
        {
            if (damage <= 0f || (c.Flags & CreepFlags.Alive) == 0) return;
            if (c.Shield > 0f)
            {
                float absorbed = math.min(c.Shield, damage);
                c.Shield -= absorbed;
                damage -= absorbed;
            }
            c.Hp -= damage;
            c.HitFlash = 0.08f;
            if (c.Hp <= 0f)
            {
                c.Hp = 0f;
                c.Flags = (byte)((c.Flags & ~CreepFlags.Alive) | CreepFlags.Dead);
            }
        }

        /// <summary>Lentidão: vale a maior ativa (não soma); teto de 90% (chefe: 60%).</summary>
        public static void ApplySlow(ref CreepState c, float pct, float duration)
        {
            float cap = c.IsBoss != 0 ? MaxBossSlow : MaxSlow;
            pct = math.min(pct, cap);
            if (pct > c.SlowPct || c.SlowTimer <= 0f) c.SlowPct = pct;
            c.SlowTimer = math.max(c.SlowTimer, duration);
        }

        /// <summary>Atordoamento: renova, não soma. Chefe: ×0,5 e imunidade após 3 em 10 s.</summary>
        public static void ApplyStun(ref CreepState c, float duration)
        {
            if (duration <= 0f) return;
            if (c.IsBoss != 0)
            {
                if (c.CcImmuneTimer > 0f) return;
                duration *= BossStunMul;
                if (c.CcWindowTimer <= 0f) { c.CcWindowTimer = BossCcWindow; c.CcCount = 0; }
                c.CcCount++;
                if (c.CcCount >= BossCcLimit) { c.CcImmuneTimer = BossCcImmune; c.CcCount = 0; }
            }
            c.StunTimer = math.max(c.StunTimer, duration);
        }

        /// <summary>DoT: mesma fonte renova; aqui um slot por creep guarda o maior DPS ativo (simplificação).</summary>
        public static void ApplyDot(ref CreepState c, float dps, float duration, byte damageType)
        {
            if (dps <= 0f || duration <= 0f) return;
            if (dps >= c.DotDps || c.DotTimer <= 0f)
            {
                c.DotDps = dps;
                c.DotType = damageType;
            }
            c.DotTimer = math.max(c.DotTimer, duration);
        }

        /// <summary>Aplica um impacto completo (dano + efeitos de on-hit) a um creep.</summary>
        public static float ApplyHit(ref CreepState c, in HitPayload p)
        {
            if ((c.Flags & CreepFlags.Alive) == 0) return 0f;
            float dmg = AgainstCreep(c, p.Damage, p.DamageType, p.Crit != 0, p.ArmorIgnore);
            ApplyDamage(ref c, dmg);
            if ((c.Flags & CreepFlags.Alive) == 0) return dmg;
            if (p.DotDps > 0f) ApplyDot(ref c, p.DotDps, p.DotDuration, p.DotType);
            if (p.Knockback > 0f) c.PendingKnockback += c.IsBoss != 0 ? p.Knockback * p.KnockbackBossMul : p.Knockback;
            if (p.StunDuration > 0f) ApplyStun(ref c, p.StunDuration);
            return dmg;
        }
    }
}
