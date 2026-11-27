// GDD §14.2 — teste de alocação zero durante combate de estresse ("roda no CI e em aparelho").
// Cenário "Test_Stress_150" montado em código: 150 creeps simultâneos (reposição contínua),
// 14 torres em tier alto, 2 heróis com habilidade em uso, HUD, floaty text e render instanciado ativos.
//
// Critério estrito (0 B em todos os 1.800 frames) só vale num PLAYER: dentro do editor o próprio
// editor/test runner aloca centenas de KB por frame mesmo sem o jogo (medido em AllocationProbeTests),
// então lá o teste roda o estresse completo (erros/exceções/estabilidade) e reporta Inconclusive.
using System;
using System.Collections;
using ChronoDefense.Core;
using ChronoDefense.Data;
using ChronoDefense.Systems;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;

namespace ChronoDefense.Tests
{
    [PrebuildSetup(typeof(StressTestPrebuild))]
    [PostBuildCleanup(typeof(StressTestPrebuild))]
    public class StressWaveTests
    {
        const int WarmupFrames = 300;
        const int MeasuredFrames = 1800;     // GDD §14.2
        const int TicksPerFrame = 8;         // 16× a 60 FPS (16 ticks por 1/30 s)
        const int TargetCreeps = CombatSimulation.HardCreepCap;

        internal GameObject Root, EventSystemGo;
        internal GameBootstrap Game;
        Unity.Mathematics.Random _rng;
        Vector3[] _path;
        float _pathLength;

        internal IEnumerator SetUpStress()
        {
            var cfg = Resources.Load<StressTestConfig>(StressTestConfig.ResourceName);
            Assume.That(cfg != null && cfg.Campaign != null && cfg.Level != null && cfg.Hero != null,
                "rode Tools ▸ Chrono ▸ Configurar projeto");

            EventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Root = new GameObject("Game");
            var cam = new GameObject("Camera", typeof(Camera));
            cam.transform.SetParent(Root.transform, false);
            Game = Root.AddComponent<GameBootstrap>();
            Game.AutoLoad = false;
            Game.Campaign = cfg.Campaign;
            Game.Level = cfg.Level;
            Game.Loadout = new[] { cfg.Hero, cfg.Hero };
            Game.ManualTicksPerFrame = TicksPerFrame;
            Game.CreepCapacityOverride = TargetCreeps;

            var load = Game.LoadAsync();
            while (!load.IsCompleted) yield return null;
            load.GetAwaiter().GetResult();

            CombatSimulation sim = Game.Sim;
            sim.InfiniteLives = true;
            sim.AutoStartWaves = false;
            sim.Gold = 1_000_000;
            sim.StartNextWave(false);

            _rng = new Unity.Mathematics.Random(99);
            _path = cfg.Level.Layout.PathPolyline;
            _pathLength = 0f;
            for (int i = 1; i < _path.Length; i++) _pathLength += Vector3.Distance(_path[i - 1], _path[i]);

            // 14 torres ao longo do caminho, alternando Atirador e Lançador (estado 5-2-0)
            for (int i = 0; i < 14; i++)
            {
                float d = _pathLength * (i + 0.5f) / 14f;
                Vector3 p = PointAlong(d, out Vector3 dir);
                Vector3 side = new Vector3(-dir.z, 0f, dir.x) * (i % 2 == 0 ? 2.6f : -2.6f);
                int def = i % 2 == 0 ? 0 : 1;
                Assert.GreaterOrEqual(sim.PlaceTowerFree(new float2(p.x + side.x, p.z + side.z), def, new UpgradeState { A = 5, B = 2 }), 0);
            }
            // 2 heróis
            for (int h = 0; h < 2; h++)
            {
                Vector3 p = PointAlong(_pathLength * (0.3f + 0.4f * h), out _);
                Assert.GreaterOrEqual(sim.PlaceHeroFree(h, new float2(p.x, p.z + 2f)), 0);
            }
        }

        Vector3 PointAlong(float d, out Vector3 dir)
        {
            for (int i = 1; i < _path.Length; i++)
            {
                float seg = Vector3.Distance(_path[i - 1], _path[i]);
                dir = (_path[i] - _path[i - 1]).normalized;
                if (d <= seg) return _path[i - 1] + dir * d;
                d -= seg;
            }
            dir = Vector3.forward;
            return _path[_path.Length - 1];
        }

        /// <summary>Mantém 150 creeps vivos e as habilidades em uso. Zero-GC (roda dentro da medição).</summary>
        internal void Feed()
        {
            CombatSimulation sim = Game.Sim;
            int types = sim.EnemyTypes.Length;
            int guard = 0;
            while (sim.Creeps.ActiveCount < TargetCreeps && guard++ < 40)
            {
                float d = _rng.NextFloat(0f, _pathLength * 0.85f);
                Vector3 p = PointAlong(d, out _);
                int type = _rng.NextInt(0, types);
                if (sim.EnemyTypes[type] is BossDataSO) type = 0;
                if (sim.Spawn(type, new float2(p.x, p.z)) < 0) break;
            }
            for (int h = 0; h < 2; h++)
                if (sim.CanCastHeroSkill(h)) sim.CastHeroSkill(h, sim.Units[sim.HeroUnit[h]].Pos + new float2(0f, -2f));
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (Root != null) UnityEngine.Object.Destroy(Root);
            if (EventSystemGo != null) UnityEngine.Object.Destroy(EventSystemGo);
        }

        /// <summary>
        /// GDD §14.2. "GC Allocated In Frame" é um contador global do processo: no player de testes ele
        /// inclui ~378 B/frame do próprio test runner (medido em frames vazios, antes de o jogo existir —
        /// ver AllocationPhaseProbeTests). Por isso o teste mede primeiro uma LINHA DE BASE nas mesmas
        /// condições (mesmo laço, timeScale 16) e exige que cada frame de combate não aloque nada além dela.
        /// </summary>
        [UnityTest]
        public IEnumerator StressWave_AllocatesZero()
        {
            Time.timeScale = 16f;   // tempo acelerado (a simulação roda 8 ticks/frame = 16× a 60 FPS)
            using var gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            for (int f = 0; f < 60; f++) yield return null;

            // 1) Linha de base: o mesmo laço sem o jogo. Guarda o conjunto de valores por frame do runner.
            // Valores ≥ RunnerMessageBytes são as mensagens periódicas "player alive" do runner (tamanho variável,
            // período em tempo real); são contadas por segundo, não comparadas por valor.
            long baseSum = 0, baseMax = 0;
            var baseValues = new long[MaxBaselineValues];
            int baseValueCount = 0, baseSpikes = 0;
            float baseStart = Time.realtimeSinceStartup;
            for (int f = 0; f < BaselineFrames; f++)
            {
                yield return null;
                long b = gcRecorder.LastValue;
                baseSum += b;
                if (b > baseMax) baseMax = b;
                if (b >= RunnerMessageBytes) { baseSpikes++; continue; }
                if (IndexOf(baseValues, baseValueCount, b) < 0 && baseValueCount < MaxBaselineValues) baseValues[baseValueCount++] = b;
            }
            float baseSeconds = Time.realtimeSinceStartup - baseStart;

            // 2) Partida de estresse. Buffers de diagnóstico alocados ANTES da janela medida.
            var badFrames = new int[16];
            var badBytes = new long[16];
            yield return SetUpStress();
            for (int f = 0; f < WarmupFrames; f++) { Feed(); yield return null; }
            GC.Collect();
            for (int f = 0; f < 10; f++) { Feed(); yield return null; }   // o frame do Collect e seguintes ficam fora

            int gameSpikes = 0;
            float gameStart = Time.realtimeSinceStartup;
            int overBaseline = 0, firstBadFrame = -1;
            long firstBadBytes = 0, gameSum = 0, gameMax = 0, maxCreeps = 0, minCreeps = long.MaxValue;
            long heapBefore = GC.GetTotalMemory(false);
            int ticksBefore = Game.Sim.TickCount;
            for (int f = 0; f < MeasuredFrames; f++)
            {
                Feed();
                yield return null;
                long bytes = gcRecorder.LastValue;
                gameSum += bytes;
                if (bytes > gameMax) gameMax = bytes;
                // Estrito: o valor do frame tem de ser um dos produzidos pelo runner sozinho.
                // Qualquer byte alocado pelo jogo tira o frame do conjunto. (Sem string aqui.)
                if (bytes >= RunnerMessageBytes) gameSpikes++;
                else if (IndexOf(baseValues, baseValueCount, bytes) < 0)
                {
                    if (overBaseline == 0) { firstBadFrame = f; firstBadBytes = bytes; }
                    if (overBaseline < badFrames.Length) { badFrames[overBaseline] = f; badBytes[overBaseline] = bytes; }
                    overBaseline++;
                }
                int alive = Game.Sim.Creeps.ActiveCount;
                if (alive > maxCreeps) maxCreeps = alive;
                if (alive < minCreeps) minCreeps = alive;
            }
            long heapAfter = GC.GetTotalMemory(false);
            float gameSeconds = Time.realtimeSinceStartup - gameStart;
            float baseRate = baseSpikes / Math.Max(0.001f, baseSeconds), gameRate = gameSpikes / Math.Max(0.001f, gameSeconds);
            int ticks = Game.Sim.TickCount - ticksBefore;
            double baseAvg = (double)baseSum / BaselineFrames, gameAvg = (double)gameSum / MeasuredFrames;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < baseValueCount; i++) sb.Append(baseValues[i]).Append(i + 1 < baseValueCount ? "," : "");
            sb.Append("] foraDaBase=[");
            for (int i = 0; i < Math.Min(overBaseline, badFrames.Length); i++) sb.Append('f').Append(badFrames[i]).Append(':').Append(badBytes[i]).Append(' ');
            string values = sb.ToString();
            string summary = $"[Stress] player={!Application.isEditor} frames={MeasuredFrames} ticks={ticks} creeps(min/max)={minCreeps}/{maxCreeps} " +
                             $"base(média/máx)={baseAvg:0.#}/{baseMax} B valores=[{values}] · combate(média/máx)={gameAvg:0.#}/{gameMax} B · " +
                             $"framesForaDaBase={overBaseline} (1º: frame {firstBadFrame}, {firstBadBytes} B) · " +
                             $"mensagens do runner/s base={baseRate:0.00} ({baseSpikes} em {baseSeconds:0.0}s) combate={gameRate:0.00} ({gameSpikes} em {gameSeconds:0.0}s) · heapDelta={heapAfter - heapBefore} B";
            Debug.Log(summary);

            Assert.IsTrue(gcRecorder.Valid, "ProfilerRecorder 'GC Allocated In Frame' indisponível");
            Assert.AreEqual(MeasuredFrames * TicksPerFrame, ticks, "a simulação precisa rodar todos os ticks");
            Assert.GreaterOrEqual(maxCreeps, TargetCreeps - 5, "o estresse precisa manter ~150 creeps");

            if (Application.isEditor)
                Assert.Inconclusive("Medição de GC por frame só é válida num player (o editor aloca centenas de KB/frame por conta própria). " + summary);

            Assert.AreEqual(0, overBaseline, $"frame {firstBadFrame}: {firstBadBytes} B não corresponde a nenhum valor do runner sozinho · {overBaseline} frames. " + summary);
            // Picos ≥ 3 KB só são aceitos na cadência das mensagens do runner (1 a cada ~7,5 s), com folga de 1.
            Assert.LessOrEqual(gameSpikes, Mathf.CeilToInt(baseRate * gameSeconds) + 1, "picos de alocação acima da cadência do runner. " + summary);
        }

        const int BaselineFrames = 900;
        const long RunnerMessageBytes = 3000;
        const int MaxBaselineValues = 64;

        static int IndexOf(long[] values, int count, long v)
        {
            for (int i = 0; i < count; i++) if (values[i] == v) return i;
            return -1;
        }
    }
}
