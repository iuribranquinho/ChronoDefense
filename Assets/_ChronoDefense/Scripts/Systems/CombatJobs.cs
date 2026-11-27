// GDD §4.2 — jobs do tick de 30 Hz (Burst). Ordem: StatusMove → BuildSpatialHash → Targeting → Fire/Projectile.
using ChronoDefense.Core;
using ChronoDefense.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace ChronoDefense.Systems
{
    /// <summary>Passos 2 e 3: timers de status, DoT, regeneração, empurrão e movimento pelo flow field.</summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct StatusMoveJob : IJobParallelFor
    {
        public NativeArray<CreepState> Creeps;
        [ReadOnly] public NativeArray<byte> Active;
        public FlowFieldView Flow;
        public float2 CorePos;
        public float Dt, LeakRadius;

        public void Execute(int i)
        {
            if (Active[i] == 0) return;
            CreepState c = Creeps[i];
            if ((c.Flags & CreepFlags.Alive) == 0) return;
            float dt = Dt;
            c.PrevPos = c.Pos;
            if (c.HitFlash > 0f) c.HitFlash -= dt;

            // DoT
            if (c.DotTimer > 0f)
            {
                float dmg = DamageCalculator.AgainstCreep(c, c.DotDps * dt, c.DotType, false, 0f);
                DamageCalculator.ApplyDamage(ref c, dmg);
                c.DotTimer -= dt;
                if (c.DotTimer <= 0f) c.DotDps = 0f;
                if ((c.Flags & CreepFlags.Alive) == 0) { Creeps[i] = c; return; }
            }
            if ((c.Traits & (uint)EnemyTraits.Regen) != 0)
                c.Hp = math.min(c.MaxHp, c.Hp + 0.02f * c.MaxHp * dt);

            // Timers
            if (c.SlowTimer > 0f) { c.SlowTimer -= dt; if (c.SlowTimer <= 0f) c.SlowPct = 0f; }
            if (c.StunTimer > 0f) c.StunTimer -= dt;
            if (c.ShredTimer > 0f) { c.ShredTimer -= dt; if (c.ShredTimer <= 0f) { c.ShredArmor = 0f; c.ShredMagic = 0f; } }
            if (c.VulnTimer > 0f) { c.VulnTimer -= dt; if (c.VulnTimer <= 0f) c.Vuln = 0f; }
            if (c.CcWindowTimer > 0f) c.CcWindowTimer -= dt;
            if (c.CcImmuneTimer > 0f) c.CcImmuneTimer -= dt;

            bool flying = (c.Traits & (uint)EnemyTraits.Flying) != 0;

            // Empurrão: recua ao longo do caminho (GDD §5.3)
            if (c.PendingKnockback > 0f)
            {
                float2 back = c.BasePos - c.Dir * c.PendingKnockback;
                if (flying || Flow.IsWalkable(back)) c.BasePos = back;
                c.PendingKnockback = 0f;
            }

            // Movimento (segurado por um soldado = parado, lutando)
            if (c.StunTimer <= 0f && c.Blocked == 0)
            {
                float2 dir = flying ? math.normalizesafe(CorePos - c.BasePos) : Flow.SampleDirection(c.BasePos);
                c.BasePos += dir * c.Speed * (1f - c.SlowPct) * dt;
                c.Dir = math.normalizesafe(math.lerp(c.Dir, dir, 0.25f), dir);
            }
            float2 perp = new float2(-c.Dir.y, c.Dir.x);
            c.Pos = c.BasePos + perp * c.LaneOffset;
            c.Progress = flying ? math.distance(c.BasePos, CorePos) : Flow.SampleIntegration(c.BasePos);

            if (math.distance(c.BasePos, CorePos) <= LeakRadius)
                c.Flags = (byte)((c.Flags & ~CreepFlags.Alive) | CreepFlags.Leaked);

            Creeps[i] = c;
        }
    }

    /// <summary>Passo 5: escolhe o alvo de cada torre/herói pela grade espacial (O(K)).</summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct TargetingJob : IJobParallelFor
    {
        public NativeArray<TowerState> Towers;
        [ReadOnly] public NativeArray<CreepState> Creeps;
        [ReadOnly] public NativeArray<int> CellStart, CellCount, Entities;
        public float2 Origin;

        public void Execute(int i)
        {
            TowerState t = Towers[i];
            if (t.Active == 0) return;
            if (t.DisabledTimer > 0f) { t.Target = -1; Towers[i] = t; return; }
            // Guardião é um quartel: quem luta são os soldados (CombatSimulation.UpdateSoldiers).
            if (t.Kind == UnitKind.Tower && t.Archetype == (byte)TowerArchetype.Guard) { t.Target = -1; Towers[i] = t; return; }

            var grid = new SpatialHashGrid { CellStart = CellStart, CellCount_ = CellCount, Entities = Entities, Origin = Origin };
            grid.GetRange(t.Pos, t.Range, out int2 min, out int2 max);
            float r2 = t.Range * t.Range;
            int best = -1;
            float bestScore = float.MaxValue;
            float bestTie = float.MaxValue;

            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
            {
                int cell = y * SpatialHashGrid.Size + x;
                int start = CellStart[cell], end = start + CellCount[cell];
                for (int k = start; k < end; k++)
                {
                    int e = Entities[k];
                    CreepState c = Creeps[e];
                    if ((c.Flags & CreepFlags.Alive) == 0) continue;
                    float d2 = math.distancesq(c.Pos, t.Pos);
                    if (d2 > r2) continue;
                    if ((c.Traits & (uint)EnemyTraits.Flying) != 0 && t.HitsFlying == 0) continue;
                    if ((c.Traits & (uint)EnemyTraits.Camo) != 0 && t.DetectsCamo == 0) continue;

                    float score;
                    switch ((TargetPriority)t.Priority)
                    {
                        case TargetPriority.Last: score = -c.Progress; break;
                        case TargetPriority.Strongest: score = -c.Hp; break;
                        case TargetPriority.Closest: score = d2; break;
                        default: score = c.Progress; break;   // First
                    }
                    float tie = c.Progress;
                    if (score < bestScore || (score == bestScore && tie < bestTie))
                    {
                        bestScore = score;
                        bestTie = tie;
                        best = e;
                    }
                }
            }
            t.Target = best;
            Towers[i] = t;
        }
    }

    /// <summary>
    /// Passos 6–8 (single-thread para manter determinismo e evitar corridas de escrita no HP):
    /// zonas de habilidade, disparo das torres, voo dos projéteis e resolução de dano.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct FireAndResolveJob : IJob
    {
        public NativeArray<TowerState> Towers;
        public NativeArray<CreepState> Creeps;
        public NativeArray<SkillZone> Zones;
        public StructPool<ProjectileState> Projectiles;
        [ReadOnly] public NativeArray<int> CellStart, CellCount, Entities;
        public NativeArray<int> Scratch;
        public float2 Origin;
        public float Dt;
        public uint Seed;

        public void Execute()
        {
            var rng = new Random(Seed);
            var grid = new SpatialHashGrid { CellStart = CellStart, CellCount_ = CellCount, Entities = Entities, Origin = Origin };

            // Zonas (Caça Coordenada de Fogo etc.)
            for (int z = 0; z < Zones.Length; z++)
            {
                SkillZone zone = Zones[z];
                if (zone.Active == 0) continue;
                int n = grid.QueryRadius(Creeps, zone.Center, zone.Radius, Scratch);
                float bonus = math.min(zone.CrowdBonusMax, zone.CrowdBonusPerEnemy * math.max(0, n - 1));
                float baseDmg = zone.Dps * (1f + bonus) * Dt;
                for (int k = 0; k < n; k++)
                {
                    int e = Scratch[k];
                    CreepState c = Creeps[e];
                    // Metade do dano é direta (com o bônus de multidão) e metade vira queimadura (DoT), que continua
                    // por 1,5 s depois que o inimigo sai do fogo. Dentro da área o total fica igual ao de antes.
                    DamageCalculator.ApplyDamage(ref c, DamageCalculator.AgainstCreep(c, baseDmg * 0.5f, zone.DamageType, false, 0f));
                    DamageCalculator.ApplyDot(ref c, zone.Dps * 0.5f, 1.5f, zone.DamageType);
                    Creeps[e] = c;
                }
                zone.Timer -= Dt;
                if (zone.Timer <= 0f) zone.Active = 0;
                Zones[z] = zone;
            }

            // Disparo
            for (int i = 0; i < Towers.Length; i++)
            {
                TowerState t = Towers[i];
                if (t.Active == 0) continue;
                if (t.DisabledTimer > 0f) { t.DisabledTimer -= Dt; Towers[i] = t; continue; }
                float speedMul = 1f;
                if (t.AttackSpeedTimer > 0f) { t.AttackSpeedTimer -= Dt; speedMul += t.AttackSpeedBonus; }
                t.Cooldown -= Dt * speedMul;

                if (t.Cooldown <= 0f && t.Target >= 0)
                {
                    CreepState target = Creeps[t.Target];
                    if ((target.Flags & CreepFlags.Alive) != 0)
                    {
                        HitPayload p = t.Payload;
                        p.Crit = (byte)(rng.NextFloat() < t.CritChance ? 1 : 0);
                        if (t.ProjectileSpeed <= 0f)
                        {
                            Impact(ref grid, t.Target, target.Serial, target.Pos, p);
                        }
                        else
                        {
                            int slot = Projectiles.Rent();
                            if (slot >= 0)
                            {
                                Projectiles.Items[slot] = new ProjectileState
                                {
                                    Pos = t.Pos, PrevPos = t.Pos, TargetPos = target.Pos, Origin = t.Pos,
                                    Target = t.Target, TargetSerial = target.Serial,
                                    Speed = t.ProjectileSpeed, Visual = t.Archetype, Payload = p,
                                };
                            }
                        }
                        t.Cooldown = math.max(t.Cooldown + t.Interval, 0f);
                    }
                }
                if (t.Cooldown < 0f) t.Cooldown = 0f;   // pronto, esperando alvo
                Towers[i] = t;
            }

            // Projéteis
            for (int i = 0; i < Projectiles.Capacity; i++)
            {
                if (Projectiles.Active[i] == 0) continue;
                ProjectileState p = Projectiles.Items[i];
                p.PrevPos = p.Pos;
                if (p.Target >= 0)
                {
                    CreepState c = Creeps[p.Target];
                    if ((c.Flags & CreepFlags.Alive) != 0 && c.Serial == p.TargetSerial) p.TargetPos = c.Pos;
                    else p.Target = -1;   // alvo morreu: segue para o último ponto
                }
                float2 d = p.TargetPos - p.Pos;
                float dist = math.length(d);
                float step = p.Speed * Dt;
                if (dist <= step)
                {
                    Impact(ref grid, p.Target, p.TargetSerial, p.TargetPos, p.Payload);
                    Projectiles.Return(i);
                }
                else
                {
                    p.Pos += d / dist * step;
                    Projectiles.Items[i] = p;
                }
            }
        }

        void Impact(ref SpatialHashGrid grid, int target, int serial, float2 pos, in HitPayload p)
        {
            if (p.Splash > 0f)
            {
                int n = grid.QueryRadius(Creeps, pos, p.Splash, Scratch);
                for (int k = 0; k < n; k++)
                {
                    int e = Scratch[k];
                    CreepState c = Creeps[e];
                    DamageCalculator.ApplyHit(ref c, p);
                    Creeps[e] = c;
                }
                return;
            }
            if (target < 0) return;
            CreepState t = Creeps[target];
            if (t.Serial != serial) return;
            DamageCalculator.ApplyHit(ref t, p);
            Creeps[target] = t;
        }
    }
}
