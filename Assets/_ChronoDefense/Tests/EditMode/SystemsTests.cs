// GDD §14.1 — testes do núcleo de runtime (M1): pools, flow field, spatial hash, dano e simulação.
using ChronoDefense.Core;
using ChronoDefense.Data;
using ChronoDefense.Editor;
using ChronoDefense.Systems;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ChronoDefense.Tests
{
    public class StructPoolTests
    {
        [Test]
        public void RentReturn_ReusesSlotsAndReportsExhaustion()
        {
            var pool = new StructPool<int>(3);
            try
            {
                Assert.AreEqual(0, pool.Rent());
                Assert.AreEqual(1, pool.Rent());
                Assert.AreEqual(2, pool.Rent());
                Assert.AreEqual(-1, pool.Rent(), "esgotado");
                Assert.AreEqual(3, pool.ActiveCount);
                pool.Return(1);
                pool.Return(1);   // retorno duplo ignorado
                Assert.AreEqual(2, pool.ActiveCount);
                Assert.AreEqual(1, pool.Rent());
                pool.Clear();
                Assert.AreEqual(0, pool.ActiveCount);
            }
            finally { pool.Dispose(); }
        }
    }

    public class FlowFieldTests
    {
        FlowField _flow;

        [SetUp]
        public void Bake()
        {
            _flow = new FlowField();
            _flow.Bake(MvpDataGenerator.PathPolyline, MvpDataGenerator.CommonPathWidth, FlowField.DefaultOrigin);
        }

        [TearDown] public void Dispose() => _flow.Dispose();

        [Test]
        public void EveryPathCellReachesCore_AndHasNonZeroVector()
        {
            int walkable = 0;
            for (int i = 0; i < FlowField.CellCount; i++)
            {
                if (_flow.Cost[i] == FlowField.Blocked) continue;
                walkable++;
                Assert.AreNotEqual(FlowField.Unreached, _flow.Integration[i], $"célula {i} não alcança o Núcleo");
                Assert.Greater(math.lengthsq(_flow.Flow[i]), 0.5f, $"vetor nulo na célula {i}");
            }
            Assert.Greater(walkable, 100);
        }

        [Test]
        public void WalkerFromLairArrivesAtCore()
        {
            var view = _flow.View;
            float2 pos = new float2(MvpDataGenerator.PathPolyline[0].x, MvpDataGenerator.PathPolyline[0].z);
            float2 core = _flow.CorePos;
            for (int tick = 0; tick < 30 * 120; tick++)
            {
                pos += view.SampleDirection(pos) * 2.2f / 30f;
                if (math.distance(pos, core) <= CombatSimulation.LeakRadius) return;
            }
            Assert.Fail($"o caminhante parou em {pos}");
        }

        [Test]
        public void IntegrationDecreasesTowardCore()
        {
            var view = _flow.View;
            float lair = view.SampleIntegration(new float2(24f, 22f));
            float mid = view.SampleIntegration(new float2(4f, 6f));
            float core = view.SampleIntegration(_flow.CorePos);
            Assert.Greater(lair, mid);
            Assert.Greater(mid, core);
        }
    }

    public class SpatialHashTests
    {
        [Test]
        public void QueryMatchesBruteForce()
        {
            const int n = 150;
            var creeps = new NativeArray<CreepState>(n, Allocator.TempJob);
            var active = new NativeArray<byte>(n, Allocator.TempJob);
            var scratch = new NativeArray<int>(SpatialHashGrid.CellCount, Allocator.TempJob);
            var results = new NativeArray<int>(n, Allocator.TempJob);
            var grid = new SpatialHashGrid(n, FlowField.DefaultOrigin);
            try
            {
                var rng = new Unity.Mathematics.Random(7);
                for (int i = 0; i < n; i++)
                {
                    creeps[i] = new CreepState { Pos = rng.NextFloat2(new float2(-30f), new float2(30f)), Flags = CreepFlags.Alive };
                    active[i] = (byte)(i % 7 == 0 ? 0 : 1);
                }
                grid.ScheduleBuild(creeps, active, scratch, default).Complete();

                for (int q = 0; q < 50; q++)
                {
                    float2 center = rng.NextFloat2(new float2(-30f), new float2(30f));
                    float radius = rng.NextFloat(1f, 9f);
                    int count = grid.QueryRadius(creeps, center, radius, results);
                    int expected = 0;
                    for (int i = 0; i < n; i++)
                        if (active[i] != 0 && math.distance(creeps[i].Pos, center) <= radius) expected++;
                    Assert.AreEqual(expected, count, $"consulta {q}");
                }
            }
            finally
            {
                creeps.Dispose(); active.Dispose(); scratch.Dispose(); results.Dispose(); grid.Dispose();
            }
        }
    }

    public class DamageCalculatorTests
    {
        // GDD §5.2 — exemplos de validação
        [Test]
        public void GddExamples()
        {
            Assert.AreEqual(80f, DamageCalculator.Compute(120f, 0f, false, (byte)DamageType.Physical, 50f, 0f, 0f, 0f), 0.01f);
            Assert.AreEqual(71.43f, DamageCalculator.Compute(100f, 0f, false, (byte)DamageType.Physical, 80f, 0.5f, 0f, 0f), 0.01f);
            Assert.AreEqual(107.69f, DamageCalculator.Compute(100f, 0f, false, (byte)DamageType.Magical, 30f, 0f, 0f, 0.4f), 0.01f);
        }

        [Test]
        public void CritTrueDamageAndVulnerabilityCap()
        {
            Assert.AreEqual(200f, DamageCalculator.Compute(100f, 0f, true, (byte)DamageType.True, 999f, 0f, 0f, 0f), 0.01f);
            Assert.AreEqual(200f, DamageCalculator.Compute(100f, 0f, false, (byte)DamageType.True, 0f, 0f, 0f, 5f), 0.01f);
        }

        [Test]
        public void ShieldAbsorbsFirst_AndKillMarksDead()
        {
            var c = new CreepState { Hp = 100f, MaxHp = 100f, Shield = 50f, Flags = CreepFlags.Alive };
            DamageCalculator.ApplyDamage(ref c, 70f);
            Assert.AreEqual(0f, c.Shield);
            Assert.AreEqual(80f, c.Hp);
            DamageCalculator.ApplyDamage(ref c, 100f);
            Assert.AreEqual(CreepFlags.Dead, c.Flags);
        }

        [Test]
        public void BossCrowdControlRules()
        {
            var boss = new CreepState { IsBoss = 1, Flags = CreepFlags.Alive };
            DamageCalculator.ApplySlow(ref boss, 0.9f, 3f);
            Assert.AreEqual(0.6f, boss.SlowPct, 1e-5f, "teto de lentidão de chefe");
            DamageCalculator.ApplyStun(ref boss, 2f);
            Assert.AreEqual(1f, boss.StunTimer, 1e-5f, "duração × 0,5");
            DamageCalculator.ApplyStun(ref boss, 2f);
            DamageCalculator.ApplyStun(ref boss, 2f);
            Assert.Greater(boss.CcImmuneTimer, 0f, "imune após 3 controles");
            boss.StunTimer = 0f;
            DamageCalculator.ApplyStun(ref boss, 2f);
            Assert.AreEqual(0f, boss.StunTimer, "ignora CC enquanto imune");
        }
    }

    public class CombatSimulationTests
    {
        const string LevelPath = "Assets/_ChronoDefense/Data/E1/Levels/LVL_{0:000}.asset";
        const string CampaignPath = "Assets/_ChronoDefense/Data/Campaign.asset";

        static CombatSimulation Load(int level, out CampaignSO campaign)
        {
            campaign = AssetDatabase.LoadAssetAtPath<CampaignSO>(CampaignPath);
            var data = AssetDatabase.LoadAssetAtPath<LevelDataSO>(string.Format(LevelPath, level));
            Assume.That(campaign != null && data != null, "rode Tools ▸ Chrono ▸ Gerar dados do MVP");
            var sim = new CombatSimulation();
            sim.Load(data, campaign.Towers, new[] { campaign.Heroes[0] });
            return sim;
        }

        [Test]
        public void Level10_StartsWithGddValues()
        {
            var sim = Load(10, out _);
            try
            {
                Assert.AreEqual(345, sim.Gold);         // G0(10)
                Assert.AreEqual(20, sim.Lives);
                Assert.AreEqual(10, sim.WaveCount);
                Assert.AreEqual(2, sim.MaxTier);        // capítulo 1: T2
                Assert.AreEqual(SimPhase.Building, sim.Phase);
            }
            finally { sim.Dispose(); }
        }

        [Test]
        public void Wave1_UndefendedWolvesLeak()
        {
            var sim = Load(1, out _);
            try
            {
                sim.StartNextWave(false);
                for (int i = 0; i < 30 * 90 && sim.Phase == SimPhase.WaveActive; i++) sim.Step();
                Assert.Greater(sim.LivesLost, 0, "sem torres, os lobos devem vazar");
                Assert.AreNotEqual(SimPhase.WaveActive, sim.Phase);
            }
            finally { sim.Dispose(); }
        }

        [Test]
        public void TowersKillWolves_AndPayGold()
        {
            var sim = Load(1, out var campaign);
            try
            {
                int marksman = System.Array.IndexOf(campaign.Towers, campaign.Towers[0]);
                sim.Gold = 10000;
                for (int p = 0; p < sim.Pedestals.Length; p++) sim.BuildTower(p, marksman);
                int goldBefore = sim.Gold;
                sim.StartNextWave(false);
                for (int i = 0; i < 30 * 90 && sim.Phase == SimPhase.WaveActive; i++) sim.Step();
                Assert.AreEqual(0, sim.LivesLost, "Atiradores nos pontos sugeridos seguram a onda 1");
                Assert.Greater(sim.Gold, goldBefore);
                Assert.AreEqual(SimPhase.Intermission, sim.Phase);
            }
            finally { sim.Dispose(); }
        }

        [Test]
        public void UpgradeAndSell_FollowCostRules()
        {
            var sim = Load(1, out _);
            try
            {
                sim.Gold = 1000;
                int slot = sim.BuildTower(0, 0);                       // Atirador 100
                Assert.IsTrue(sim.Upgrade(slot, 0));                   // T1 = 60
                Assert.IsTrue(sim.Upgrade(slot, 0));                   // T2 = 100
                Assert.IsFalse(sim.Upgrade(slot, 0), "T3 travado no capítulo 1");
                Assert.IsTrue(sim.Upgrade(slot, 1));
                Assert.IsTrue(sim.IsPathLocked(slot, 2), "trava 5-2-0: terceiro caminho");
                Assert.AreEqual(260 + 60, sim.Spent[slot]);
                int gold = sim.Gold;
                sim.Sell(slot);
                Assert.AreEqual(gold + 224, sim.Gold);                 // ceil(320 × 0,7)
                Assert.AreEqual(-1, sim.PedestalUnit[0]);
            }
            finally { sim.Dispose(); }
        }

        [Test]
        public void FreePlacement_GrassYes_PathLandmarksAndOverlapNo()
        {
            var sim = Load(1, out _);
            try
            {
                sim.Gold = 10000;
                Vector3[] path = MvpDataGenerator.PathPolyline;
                Vector3 onPath = (path[2] + path[3]) * 0.5f;
                Assert.IsFalse(sim.CanPlaceAt(onPath), "em cima do caminho");
                Vector3 edge = onPath + new Vector3(0f, 0f, MvpDataGenerator.CommonPathWidth * 0.5f + 0.2f);   // trecho horizontal z = 0
                Assert.IsFalse(sim.CanPlaceAt(edge), "a base encostaria no caminho");
                Assert.IsFalse(sim.CanPlaceAt(new Vector3(30f, 0f, 6f)), "fora do quadro do mapa");

                Vector3 grass = new Vector3(4f, 0f, 6f);    // Vale Gelado: entre os trechos z = 12 e z = 0
                Assert.IsTrue(sim.CanPlaceAt(grass));
                int spot = sim.AcquireSpot(grass);
                Assert.GreaterOrEqual(spot, 0);
                Assert.AreEqual(grass.x, sim.Pedestals[spot].x, 1e-4f);
                Assert.GreaterOrEqual(sim.BuildTower(spot, 0), 0);
                Assert.IsFalse(sim.CanPlaceAt(grass + new Vector3(1f, 0f, 0f)), "sobreposta à torre");
                Assert.IsTrue(sim.CanPlaceAt(grass + new Vector3(2f * CombatSimulation.FootprintRadius + 0.1f, 0f, 0f)));
                Assert.AreEqual(spot, sim.OccupiedSpotNear(grass + new Vector3(0.5f, 0f, 0.5f), 2f));
            }
            finally { sim.Dispose(); }
        }

        [Test]
        public void GuardSoldiers_SpawnBlockFightAndRespawn()
        {
            var sim = Load(1, out var campaign);
            try
            {
                int guard = System.Array.FindIndex(campaign.Towers, t => t.Archetype == TowerArchetype.Guard);
                Assume.That(guard >= 0);
                // Vale Gelado: (−6; 6,5) fica a 5,5 m do trecho de entrada (z = 12), dentro do alcance
                int slot = sim.PlaceTowerFree(new float2(-6f, 6.5f), guard, default);
                Assert.GreaterOrEqual(slot, 0);
                int first = slot * CombatSimulation.SoldiersPerGuard;
                for (int i = 0; i < 30 * 3; i++) sim.Step();   // saem do quartel em fila
                for (int k = 0; k < campaign.Towers[guard].SoldierCount; k++)
                    Assert.AreEqual(1, sim.Soldiers[first + k].Alive, $"soldado {k} em campo");
                Assert.AreEqual(12f, sim.Soldiers[first].Post.y, 0.01f, "posto no caminho");

                sim.StartNextWave(false);
                bool blocked = false, hurt = false;
                for (int i = 0; i < 30 * 60 && sim.Phase == SimPhase.WaveActive; i++)
                {
                    sim.Step();
                    for (int c = 0; c < sim.Creeps.Capacity; c++)
                        if (sim.Creeps.Active[c] != 0 && sim.Creeps.Items[c].Blocked != 0) blocked = true;
                    for (int k = 0; k < CombatSimulation.SoldiersPerGuard; k++)
                        if (sim.Soldiers[first + k].Alive != 0 && sim.Soldiers[first + k].Hp < sim.Soldiers[first + k].MaxHp) hurt = true;
                }
                Assert.IsTrue(blocked, "soldados seguram os lobos");
                Assert.IsTrue(hurt, "lobos revidam");

                // Morto volta depois de SoldierRespawn
                sim.Soldiers[first].Alive = 0;
                sim.Soldiers[first].Respawn = CombatSimulation.SoldierRespawn;
                for (int i = 0; i < (int)(30 * CombatSimulation.SoldierRespawn) + 2; i++) sim.Step();
                Assert.AreEqual(1, sim.Soldiers[first].Alive);
            }
            finally { sim.Dispose(); }
        }

        [Test]
        public void FreePlacement_ReusesEmptySpots_ZeroGc()
        {
            var sim = Load(1, out _);
            try
            {
                sim.Gold = 100000;
                // aquece (primeira construção)
                sim.BuildTower(sim.AcquireSpot(new Vector3(4f, 0f, 6f)), 0);
                long before = System.GC.GetAllocatedBytesForCurrentThread();
                int built = 0;
                for (int i = 0; i < 40; i++)
                {
                    int spot = sim.AcquireSpot(new Vector3(-20f + (i % 8) * 2.8f, 0f, i < 8 ? 6f : -6f + (i / 8) * 0.01f));
                    if (spot >= 0 && sim.BuildTower(spot, 0) >= 0) built++;
                }
                Assert.AreEqual(0, System.GC.GetAllocatedBytesForCurrentThread() - before, "construir não pode alocar");
                Assert.Greater(built, 0);
                Assert.LessOrEqual(sim.SpotCount, CombatSimulation.MaxSpots);
            }
            finally { sim.Dispose(); }
        }

        [Test]
        public void KrogBoss_HasScaledHp_AndSummonsHunters()
        {
            var sim = Load(10, out _);
            try
            {
                sim.InfiniteLives = true;
                for (int w = 0; w < 9; w++) { sim.StartNextWave(false); DrainWave(sim); }
                sim.StartNextWave(false);
                int boss = -1;
                for (int i = 0; i < 30 && boss < 0; i++)
                {
                    sim.Step();
                    for (int k = 0; k < sim.Creeps.Capacity; k++)
                        if (sim.Creeps.Active[k] != 0 && sim.Creeps.Items[k].IsBoss != 0) boss = k;
                }
                Assert.GreaterOrEqual(boss, 0, "Krog Corrompido nasce na onda 10");
                Assert.AreEqual(3537.6f, sim.Creeps.Items[boss].MaxHp, 1f);

                int before = sim.Creeps.ActiveCount;
                var c = sim.Creeps.Items[boss];
                c.Hp = c.MaxHp * 0.6f;   // abaixo de 66%
                sim.Creeps.Items[boss] = c;
                sim.Step();
                Assert.GreaterOrEqual(sim.Creeps.ActiveCount, before + 4 - 1, "Rugido da Caçada invoca 4 Caçadores");
            }
            finally { sim.Dispose(); }
        }

        [Test]
        public void KrogSkill_OpensBurningZone()
        {
            var sim = Load(1, out _);
            try
            {
                sim.Gold = 1000;
                Assert.GreaterOrEqual(sim.SummonHero(0, 0), 0);
                sim.StartNextWave(false);
                for (int i = 0; i < 60; i++) sim.Step();
                float2 at = sim.LairPos;
                Assert.IsTrue(sim.CastHeroSkill(0, at));
                Assert.AreEqual(1, sim.Zones[0].Active);
                Assert.AreEqual(30f, sim.HeroCooldown[0], 1e-4f);
                Assert.IsFalse(sim.CastHeroSkill(0, at), "em recarga");
            }
            finally { sim.Dispose(); }
        }

        static void DrainWave(CombatSimulation sim)
        {
            for (int i = 0; i < 30 * 200 && sim.Phase == SimPhase.WaveActive; i++)
            {
                sim.Step();
                // Mata tudo o que existe para acelerar o teste.
                for (int k = 0; k < sim.Creeps.Capacity; k++)
                {
                    if (sim.Creeps.Active[k] == 0) continue;
                    var c = sim.Creeps.Items[k];
                    DamageCalculator.ApplyDamage(ref c, 1e9f);
                    sim.Creeps.Items[k] = c;
                }
            }
        }
    }
}
