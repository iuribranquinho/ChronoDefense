// Valida as constantes do gerador do MVP sem depender dos assets gerados.
using ChronoDefense.Data;
using ChronoDefense.Editor;
using UnityEngine;
using NUnit.Framework;

namespace ChronoDefense.Tests
{
    public class MvpDataTests
    {
        // ---- Layouts da Era 1 no modelo "Prado" (GDD §10.7) ----
        static System.Collections.Generic.IEnumerable<int> LayoutIndices()
        {
            for (int i = 0; i < MvpDataGenerator.Layouts.Length; i++) yield return i;
        }

        [TestCaseSource(nameof(LayoutIndices))]
        public void Layout_NoCrossings_AndRoomBetweenSegments(int i)
        {
            var def = MvpDataGenerator.Layouts[i];
            Assert.GreaterOrEqual(MvpDataGenerator.MinGapBetweenSegments(def.Path), MvpDataGenerator.MinSegmentGap, def.Name);
        }

        [TestCaseSource(nameof(LayoutIndices))]
        public void Layout_NoBases_EntersAndLeavesOffscreen(int i)
        {
            var def = MvpDataGenerator.Layouts[i];
            Bounds frame = def.Frame;
            Assert.GreaterOrEqual(MvpDataGenerator.OutsideBy(frame, def.Path[0]), MvpDataGenerator.OffscreenMin, $"{def.Name}: entrada");
            Assert.GreaterOrEqual(MvpDataGenerator.OutsideBy(frame, def.Path[def.Path.Length - 1]), MvpDataGenerator.OffscreenMin, $"{def.Name}: saída");
            for (int k = 1; k < def.Path.Length - 1; k++)
                Assert.LessOrEqual(MvpDataGenerator.OutsideBy(frame, def.Path[k]), -MvpDataGenerator.FrameMargin + 1e-3f, $"{def.Name}: curva {k} longe da borda");
        }

        [TestCaseSource(nameof(LayoutIndices))]
        public void Layout_FrameHugsPath_AndFillsScreen(int i)
        {
            var def = MvpDataGenerator.Layouts[i];
            Bounds frame = def.Frame;
            float aspect = frame.size.x / frame.size.z;
            Assert.That(aspect, Is.InRange(1.1f, 1.5f), $"{def.Name}: proporção {aspect:0.00}");
            Assert.AreEqual(1f, frame.size.y, 1e-4f, "altura 1 m (sem arte alta)");
        }

        [TestCaseSource(nameof(LayoutIndices))]
        public void Layout_SpotsAreBuildable_AndReachFarSide(int i)
        {
            var def = MvpDataGenerator.Layouts[i];
            Assert.That(def.Spots.Length, Is.InRange(4, 8));
            Assert.GreaterOrEqual(MvpDataGenerator.MinPedestalClearance(def.Path, def.Spots, MvpDataGenerator.PathWidth), 0f, $"{def.Name}: base encosta na pista");
            Bounds frame = def.Frame;
            foreach (var s in def.Spots)
                Assert.LessOrEqual(MvpDataGenerator.OutsideBy(frame, s), -MvpDataGenerator.PedestalRadius, $"{def.Name}: ponto {s} fora do quadro");
            float reach = MvpDataGenerator.RequiredReach(def.Path, def.Spots, MvpDataGenerator.PathWidth);
            foreach (float r in MvpDataGenerator.TowerBaseRange) Assert.GreaterOrEqual(r, reach, def.Name);
            Assert.GreaterOrEqual(MvpDataGenerator.KrogAttackRange, reach, def.Name);
        }

        [Test]
        public void EveryLevel_HasALayout()
        {
            Assert.AreEqual(10, MvpDataGenerator.LevelLayout.Length);
            foreach (int k in MvpDataGenerator.LevelLayout) Assert.That(k, Is.InRange(0, MvpDataGenerator.Layouts.Length - 1));
            Assert.AreEqual("Covil do Krog", MvpDataGenerator.Layouts[MvpDataGenerator.LevelLayout[9]].Name, "fase 10");
        }

        static TowerDataSO NewTower(System.Action<TowerDataSO> fill)
        {
            var t = UnityEngine.ScriptableObject.CreateInstance<TowerDataSO>();
            MvpDataGenerator.EnsureTierStructure(t);
            fill(t);
            return t;
        }

        [Test]
        public void Marksman_T1T2_Stack()
        {
            var t = NewTower(MvpDataGenerator.FillMarksmanTiers);
            Assert.AreEqual(26f, t.GetStat(StatId.Damage, 18f, new UpgradeState { A = 1 }), 1e-4f);
            Assert.AreEqual(38f, t.GetStat(StatId.Damage, 18f, new UpgradeState { A = 2 }), 1e-4f);
            Assert.AreEqual(6.75f, t.GetStat(StatId.Range, 6f, new UpgradeState { A = 2 }), 1e-4f);
            Assert.AreEqual(0.51f, t.GetStat(StatId.Interval, 0.8f, new UpgradeState { B = 2 }), 1e-4f);
            // 2-2-0: A e B acumulam entre caminhos
            var ab = new UpgradeState { A = 2, B = 2 };
            Assert.AreEqual(38f, t.GetStat(StatId.Damage, 18f, ab), 1e-4f);
            Assert.AreEqual(0.51f, t.GetStat(StatId.Interval, 0.8f, ab), 1e-4f);
            Assert.AreEqual(7.5f, t.GetStat(StatId.Range, 6f, new UpgradeState { C = 2 }), 1e-4f);
            Assert.IsFalse(t.Paths[2].Tiers[0].GrantsCamoDetection);
            Assert.IsTrue(t.Paths[2].Tiers[1].GrantsCamoDetection);
            UnityEngine.Object.DestroyImmediate(t);
        }

        [Test]
        public void Launcher_T1T2()
        {
            var t = NewTower(MvpDataGenerator.FillLauncherTiers);
            Assert.AreEqual(75f, t.GetStat(StatId.Damage, 40f, new UpgradeState { A = 2 }), 1e-4f);
            Assert.AreEqual(1.8f, t.GetStat(StatId.SplashRadius, 1.5f, new UpgradeState { A = 2 }), 1e-4f);
            Assert.AreEqual(EffectType.DoT, t.Paths[1].Tiers[1].OnHitEffects[0].Type);
            Assert.AreEqual(12f, t.Paths[1].Tiers[1].OnHitEffects[0].Magnitude);
            Assert.AreEqual(2, t.Paths[2].Tiers[1].OnHitEffects.Length);
            Assert.AreEqual(0.25f, t.Paths[2].Tiers[0].OnHitEffects[0].BossMultiplier);
            UnityEngine.Object.DestroyImmediate(t);
        }

        [Test]
        public void Fill_PreservesCuratedTiers()
        {
            var t = UnityEngine.ScriptableObject.CreateInstance<TowerDataSO>();
            MvpDataGenerator.EnsureTierStructure(t);
            t.Paths[0].Tiers[0].Modifiers = new[] { new StatModifier { Stat = StatId.Damage, Op = ModifierOp.Add, Value = 99f } };
            MvpDataGenerator.FillMarksmanTiers(t);
            Assert.AreEqual(117f, t.GetStat(StatId.Damage, 18f, new UpgradeState { A = 1 }), 1e-4f);
            UnityEngine.Object.DestroyImmediate(t);
        }

        [Test]
        public void Level10_MatchesGddTable()
        {
            var w = MvpDataGenerator.Level10Waves;
            Assert.AreEqual(10, w.Length);                            // fases de chefe têm 10 ondas
            Assert.AreEqual((MvpDataGenerator.E.Wolf, 8), w[0][0]);
            Assert.AreEqual((MvpDataGenerator.E.Rhino, 6), w[8][0]);
            Assert.AreEqual((MvpDataGenerator.E.Mammoth, 1), w[9][0]);
            Assert.AreEqual((MvpDataGenerator.E.Wolf, 6), w[9][1]);
        }
    }
}
