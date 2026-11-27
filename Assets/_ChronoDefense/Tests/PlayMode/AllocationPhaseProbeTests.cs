// Diagnóstico por fases (roda no editor e no player): média de "GC Allocated In Frame" em cada fase,
// para separar o custo do test runner do custo do jogo.
using System.Collections;
using ChronoDefense.Core;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChronoDefense.Tests
{
    [PrebuildSetup(typeof(StressTestPrebuild))]
    [PostBuildCleanup(typeof(StressTestPrebuild))]
    public class AllocationPhaseProbeTests
    {
        const int Frames = 180;
        readonly StressWaveTests _stress = new StressWaveTests();
        ProfilerRecorder _rec;

        [TearDown]
        public void TearDown()
        {
            _rec.Dispose();
            _stress.TearDown();
        }

        IEnumerator Measure(bool feed, long[] result, int slot)
        {
            yield return null;
            long sum = 0, nonZero = 0;
            for (int f = 0; f < Frames; f++)
            {
                if (feed) _stress.Feed();
                yield return null;
                long b = _rec.LastValue;
                sum += b;
                if (b != 0) nonZero++;
            }
            result[slot * 2] = sum / Frames;
            result[slot * 2 + 1] = nonZero;
        }

        [UnityTest]
        public IEnumerator Probe_AllocationByPhase()
        {
            _rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var r = new long[10];
            for (int i = 0; i < 60; i++) yield return null;
            yield return Measure(false, r, 0);                       // P0: frames vazios (test runner)

            yield return _stress.SetUpStress();
            GameBootstrap game = _stress.Game;
            for (int i = 0; i < 200; i++) { _stress.Feed(); yield return null; }

            game.TogglePause();
            yield return Measure(false, r, 1);                       // P1: jogo carregado, simulação pausada
            game.TogglePause();

            game.FloatyTextEnabled = false;
            yield return Measure(true, r, 2);                        // P2: simulação + render + HUD, sem floaty text
            game.FloatyTextEnabled = true;

            yield return Measure(true, r, 3);                        // P3: completo

            game.Hud.enabled = false;
            yield return Measure(true, r, 4);                        // P4: completo sem o Update do HUD
            game.Hud.enabled = true;

            Debug.Log($"[Phases] player={!Application.isEditor} média B/frame (frames≠0): " +
                      $"vazio={r[0]} ({r[1]}) · pausado={r[2]} ({r[3]}) · semFloaty={r[4]} ({r[5]}) · completo={r[6]} ({r[7]}) · semHUD={r[8]} ({r[9]})");
            Assert.Pass();
        }
    }
}
