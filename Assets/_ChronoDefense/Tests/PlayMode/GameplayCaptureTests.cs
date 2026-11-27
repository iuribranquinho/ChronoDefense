// Renderiza um frame da partida em PNG (Logs/Batch/gameplay.png) para inspeção visual. Só no editor.
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChronoDefense.Tests
{
    [PrebuildSetup(typeof(StressTestPrebuild))]
    [PostBuildCleanup(typeof(StressTestPrebuild))]
    public class GameplayCaptureTests
    {
        readonly StressWaveTests _stress = new StressWaveTests();

        [TearDown] public void TearDown() => _stress.TearDown();

        [UnityTest]
        public IEnumerator Gameplay_RendersFrameToPng()
        {
            yield return _stress.SetUpStress();
            var game = _stress.Game;
            game.ManualTicksPerFrame = 2;
            for (int f = 0; f < 120; f++) { _stress.Feed(); yield return null; }

            // Seleciona um pedestal para mostrar o indicador de alcance e o painel da torre.
            game.Sim.Gold = 5000;
            game.Placer.Select(0);
            game.Placer.Build(0);
            yield return null;

            Camera cam = game.Rig.Cam;
            var canvas = _stress.Root.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            yield return null;

            var rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            cam.aspect = 1920f / 1080f;
            game.Rig.Refit();   // enquadra para o aspecto 16:9 da captura
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Directory.CreateDirectory("Logs/Batch");
            File.WriteAllBytes("Logs/Batch/gameplay.png", tex.EncodeToPNG());

            // O frame não pode ser só a cor de fundo: amostra pixels e exige variedade de cor.
            int distinct = 0;
            Color first = tex.GetPixel(960, 300);
            for (int y = 100; y < 1000; y += 90)
            for (int x = 100; x < 1800; x += 170)
                if (((Vector4)(tex.GetPixel(x, y) - first)).sqrMagnitude > 0.01f) distinct++;
            Object.Destroy(tex);
            rt.Release();
            Assert.Greater(distinct, 20, "o frame renderizado parece vazio");
        }
    }
}
#endif
