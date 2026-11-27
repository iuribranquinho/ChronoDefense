// Estado de runtime da simulação (GDD §4, §10.1: "estado mutável fica em structs de runtime").
// Tudo blittable para NativeArray + Burst.
using Unity.Mathematics;

namespace ChronoDefense.Systems
{
    public static class CreepFlags
    {
        public const byte Alive = 1;
        public const byte Dead = 2;
        public const byte Leaked = 4;
    }

    /// <summary>Parâmetros de dano carregados por um ataque/projétil (copiados no disparo).</summary>
    public struct HitPayload
    {
        public float Damage;
        public byte DamageType;      // ChronoDefense.Data.DamageType
        public byte Crit;
        public float Splash;
        public float ArmorIgnore;    // fração de resistência ignorada (shred do atacante)
        public float DotDps, DotDuration;
        public byte DotType;
        public float Knockback, StunDuration;
        public float KnockbackBossMul;   // atordoamento de chefe usa a regra fixa ×0,5 (DamageCalculator.ApplyStun)
    }

    public struct CreepState
    {
        public float2 BasePos;       // na linha central (segue o flow field)
        public float2 Pos;           // BasePos + offset de faixa (targeting e render)
        public float2 PrevPos;       // interpolação de render
        public float2 Dir;
        public float LaneOffset;     // ±0,6 m sorteado no spawn (GDD §4.3)
        public float Progress;       // distância de integração até o Núcleo (menor = mais perto)
        public float Hp, MaxHp, Shield;
        public float Armor, MagicResist, Speed, Mass;
        public uint Traits;          // EnemyTraits
        public int TypeIndex;
        public int Serial;           // único por spawn: projéteis não seguem um slot reciclado
        public int LeakWeight, Gold;
        public byte Flags;
        public byte IsBoss;

        // Status (GDD §5.3)
        public float SlowPct, SlowTimer, StunTimer;
        public float DotDps, DotTimer;
        public byte DotType;
        public float ShredArmor, ShredMagic, ShredTimer;
        public float Vuln, VulnTimer;
        public float PendingKnockback;
        // Resistência a CC de chefe: 3 controles em 10 s → imune por 5 s
        public int CcCount;
        public float CcWindowTimer, CcImmuneTimer;
        public float HitFlash;
        public byte Blocked;          // 1 = segurado por um soldado do Guardião (não anda neste tick)
    }

    /// <summary>
    /// Soldado do Guardião (26/11): sai do quartel, fica num posto no caminho e segura um inimigo por vez.
    /// Índice = unidade do Guardião × CombatSimulation.SoldiersPerGuard + k.
    /// </summary>
    public struct SoldierState
    {
        public float2 Pos, PrevPos, Post;
        public float Hp, MaxHp, AttackTimer, Interval, Respawn, Swing;
        public int Engaged, EngagedSerial;   // creep segurado (-1 = nenhum)
        public byte Active, Alive;
        public byte Holder;                  // 1 = é quem segura (apanha); 0 = está ajudando outro soldado
    }

    public static class UnitKind
    {
        public const byte Tower = 0;
        public const byte Hero = 1;
    }

    public struct TowerState
    {
        public float2 Pos;
        public int Pedestal;          // -1 = posição livre (testes de estresse)
        public int DefIndex;          // índice no catálogo de torres/heróis
        public byte Kind;             // UnitKind
        public byte Archetype;        // TowerArchetype
        public byte Active;
        public byte HitsFlying, DetectsCamo, Priority;
        public float Interval, Range, CritChance, ProjectileSpeed;
        public HitPayload Payload;
        public float Cooldown;
        public float DisabledTimer;   // Pisão Glacial do chefe (pedestal congelado)
        public float AttackSpeedTimer, AttackSpeedBonus;
        public int Target;
    }

    public struct ProjectileState
    {
        public float2 Pos, PrevPos, TargetPos;
        public float2 Origin;         // de onde saiu (só visual: altura do arco da lança/pedra)
        public int Target, TargetSerial;
        public float Speed;
        public byte Visual;           // TowerArchetype de origem (escolhe a malha instanciada)
        public HitPayload Payload;
    }

    /// <summary>Zona persistente de habilidade (ex.: Caça Coordenada de Fogo do Krog).</summary>
    public struct SkillZone
    {
        public float2 Center;
        public float Radius, Timer, Dps;
        public float Duration;        // total (visual: chão pegando fogo e apagando)
        public byte DamageType;
        public float CrowdBonusPerEnemy, CrowdBonusMax;
        public byte Active;
    }
}
