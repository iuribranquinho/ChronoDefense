// GDD §14.1 — validação das fórmulas de escala, ouro, dano e da trava 5-2-0.
using ChronoDefense.Data;
using NUnit.Framework;

namespace ChronoDefense.Tests
{
    public class BalanceFormulasTests
    {
        [TestCase(1, 1, 1, 1)]
        [TestCase(10, 1, 10, 10)]
        [TestCase(100, 1, 100, 10)]
        [TestCase(101, 2, 1, 1)]
        [TestCase(150, 2, 50, 10)]
        [TestCase(500, 5, 100, 10)]
        public void CanonicalIndices(int L, int e, int l, int p)
        {
            Assert.AreEqual(e, BalanceFormulas.EraOf(L));
            Assert.AreEqual(l, BalanceFormulas.LevelInEra(L));
            Assert.AreEqual(p, BalanceFormulas.ChapterPosition(L));
        }

        // Tabela §8.2
        [TestCase(1, 1.000f, 1.000f)]
        [TestCase(2, 1.750f, 2.188f)]
        [TestCase(3, 3.063f, 4.594f)]
        [TestCase(4, 5.359f, 9.379f)]
        [TestCase(5, 9.379f, 18.758f)]
        public void EraFactors(int era, float power, float hpFactor)
        {
            Assert.AreEqual(power, BalanceFormulas.EraPower(era), 0.001f);
            Assert.AreEqual(hpFactor, BalanceFormulas.EraHpFactor(era), 0.001f);
        }

        // Tabela §8.4
        [TestCase(10, 3000f, 3538f)]
        [TestCase(50, 6000f, 9187f)]
        [TestCase(100, 12000f, 23654f)]
        [TestCase(150, 8000f, 26796f)]
        [TestCase(500, 40000f, 1479016f)]
        public void BossHp_MatchesTable(int L, float bossBase, float expected)
        {
            float hp = BalanceFormulas.BossHp(bossBase, L);
            Assert.AreEqual(expected, hp, expected * 1e-4f + 0.5f);
        }

        // Tabela §9.2 completa (Fase 10). HP_tipo: Lobo 70 · Tigre 180 · Rinoceronte 450.
        [TestCase(70f, 1, 83)]
        [TestCase(70f, 2, 89)]
        [TestCase(70f, 3, 96)]
        [TestCase(180f, 4, 267)]
        [TestCase(70f, 4, 104)]
        [TestCase(180f, 5, 289)]
        [TestCase(70f, 5, 112)]
        [TestCase(180f, 6, 312)]
        [TestCase(450f, 7, 842)]
        [TestCase(70f, 7, 131)]
        [TestCase(450f, 8, 909)]
        [TestCase(180f, 8, 364)]
        [TestCase(450f, 9, 982)]
        [TestCase(70f, 10, 165)]
        public void CreepHp_Level10Table(float baseHp, int wave, int expected)
        {
            float hp = BalanceFormulas.CreepHp(baseHp, 10, wave, BalanceFormulas.AlphaNormal);
            Assert.AreEqual(expected, UnityEngine.Mathf.RoundToInt(hp));
        }

        [Test]
        public void Gold()
        {
            Assert.AreEqual(345, BalanceFormulas.StartGold(10));
            Assert.AreEqual(795, BalanceFormulas.StartGold(100));   // 300 + 5·99; o teto de 800 não é atingido em l ≤ 100
            Assert.AreEqual(4, BalanceFormulas.KillGold(70f));
            Assert.AreEqual(9, BalanceFormulas.KillGold(180f));
            Assert.AreEqual(23, BalanceFormulas.KillGold(450f));
            Assert.AreEqual(55, BalanceFormulas.WaveGold(1));
        }

        // Tabela §7.3
        [TestCase(100, 1, 60)]
        [TestCase(150, 5, 2250)]
        [TestCase(140, 1, 84)]
        [TestCase(120, 4, 600)]
        public void TierCosts(int baseCost, int tier, int expected)
        {
            Assert.AreEqual(expected, BalanceFormulas.TierCost(baseCost, tier));
        }

        // Exemplos de validação §5.2
        [Test]
        public void Mitigation_Examples()
        {
            Assert.AreEqual(80f, 120f * BalanceFormulas.Mitigation(DamageType.Physical, 50f, 0f, 0f), 0.01f);
            Assert.AreEqual(71.43f, 100f * BalanceFormulas.Mitigation(DamageType.Physical, 80f, 0.5f, 0f), 0.01f);
            Assert.AreEqual(107.69f, 100f * BalanceFormulas.Mitigation(DamageType.Magical, 30f, 0f, 0f) * 1.4f, 0.01f);
            Assert.AreEqual(1f, BalanceFormulas.Mitigation(DamageType.True, 300f, 0f, 0f));
        }

        // §7.4 — as 216 combinações de (tA, tB, tC)
        [Test]
        public void UpgradeLock_AllCombinations()
        {
            for (int a = 0; a <= 5; a++)
            for (int b = 0; b <= 5; b++)
            for (int c = 0; c <= 5; c++)
            {
                var s = new UpgradeState { A = (byte)a, B = (byte)b, C = (byte)c };
                for (int path = 0; path < 3; path++)
                {
                    int na = a + (path == 0 ? 1 : 0), nb = b + (path == 1 ? 1 : 0), nc = c + (path == 2 ? 1 : 0);
                    int active = (na > 0 ? 1 : 0) + (nb > 0 ? 1 : 0) + (nc > 0 ? 1 : 0);
                    int deep = (na > 2 ? 1 : 0) + (nb > 2 ? 1 : 0) + (nc > 2 ? 1 : 0);
                    bool expected = na <= 5 && nb <= 5 && nc <= 5 && active <= 2 && deep <= 1;
                    if (s.CanUpgrade(path, 5) != expected)
                        Assert.Fail($"({a},{b},{c}) path {path}: esperado {expected}");
                }
            }
        }

        [Test]
        public void UpgradeLock_KeyCases()
        {
            var s = new UpgradeState { A = 5, B = 2 };
            Assert.IsFalse(s.CanUpgrade(2, 5), "terceiro caminho");
            Assert.IsFalse(s.CanUpgrade(1, 5), "segundo caminho acima de 2");
            Assert.IsFalse(s.CanUpgrade(0, 5), "acima de T5");

            var onboarding = new UpgradeState { A = 2 };
            Assert.IsFalse(onboarding.CanUpgrade(0, 2), "trava de onboarding T2 (§1.3)");
            Assert.AreEqual(3, new UpgradeState { C = 5 }.VisualStage);
            Assert.AreEqual(2, new UpgradeState { B = 3 }.VisualStage);
            Assert.AreEqual(1, new UpgradeState { A = 2, B = 2 }.VisualStage);
        }
    }
}
