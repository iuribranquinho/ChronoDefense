// Fórmulas canônicas do GDD v2.0 (§1.1, §5.2, §6.1, §7.3, §7.5, §8.2–8.5).
// Funções puras, sem alocação: podem ser chamadas no carregamento e em testes EditMode.
// Os valores retornados NÃO são arredondados (as tabelas do GDD arredondam só para exibição),
// exceto onde o GDD define ceil explicitamente (ouro).
using UnityEngine;

namespace ChronoDefense.Data
{
    public static class BalanceFormulas
    {
        public const int LevelsPerEra = 100;
        public const int LevelsPerChapter = 10;
        public const int TotalLevels = 500;
        public const int EraCount = 5;
        public const int BossChapterPosition = 10;

        public const float AlphaNormal = 0.08f;   // §8.3
        public const float AlphaHard = 0.11f;     // §8.3
        public const float SellRatio = 0.70f;     // §7.5
        public const float DefaultGuardRadius = 3.5f; // §6.1
        public const int BossKillGold = 150;      // §8.5
        public const int MaxEarlyCallGold = 30;   // §8.5
        public const int MaxLoadoutHeroes = 2;    // §1.3

        /// <summary>k_t de §7.3, índice = tier − 1.</summary>
        public static readonly float[] TierCostMultipliers = { 0.6f, 1.0f, 2.0f, 5.0f, 15.0f };

        // ---------- §1.1 Índices canônicos ----------
        public static int EraOf(int globalLevel) => (globalLevel + LevelsPerEra - 1) / LevelsPerEra;          // e = ceil(L/100)
        public static int LevelInEra(int globalLevel) => ((globalLevel - 1) % LevelsPerEra) + 1;              // l
        public static int ChapterPosition(int globalLevel) => ((globalLevel - 1) % LevelsPerChapter) + 1;     // p
        public static int ChapterOf(int globalLevel) => (globalLevel + LevelsPerChapter - 1) / LevelsPerChapter; // 1..50
        public static bool IsBossLevel(int globalLevel) => ChapterPosition(globalLevel) == BossChapterPosition;

        // ---------- §8.2 Poder e pressão da Era ----------
        public static float EraPower(int era) => Mathf.Pow(1.75f, era - 1);          // P(e)
        public static float EraPressure(int era) => 1f + 0.25f * (era - 1);          // R(e)
        public static float EraHpFactor(int era) => EraPower(era) * EraPressure(era); // K_era(e)

        // ---------- §8.3 ----------
        public static float InEraProgress(int levelInEra) => 1f + 0.008f * (levelInEra - 1);   // K_in(l)
        public static float ChapterSaw(int chapterPos) => 0.92f + 0.02f * (chapterPos - 1);     // Saw(p)

        /// <summary>Fator de escala de nível sem onda: K_era × K_in × Saw.</summary>
        public static float LevelScale(int globalLevel) =>
            EraHpFactor(EraOf(globalLevel)) * InEraProgress(LevelInEra(globalLevel)) * ChapterSaw(ChapterPosition(globalLevel));

        /// <summary>HP(L, w) = HP_tipo × K_era × K_in × Saw × (1+α)^(w−1).</summary>
        public static float CreepHp(float baseHp, int globalLevel, int wave, float alpha) =>
            baseHp * LevelScale(globalLevel) * Mathf.Pow(1f + alpha, wave - 1);

        /// <summary>§8.4 — chefes não usam α e sempre usam Saw(10).</summary>
        public static float BossHp(float bossBase, int globalLevel) =>
            bossBase * EraHpFactor(EraOf(globalLevel)) * InEraProgress(LevelInEra(globalLevel)) * ChapterSaw(BossChapterPosition);

        /// <summary>Dificuldade relativa D = R(e) × K_in(l) × Saw(p).</summary>
        public static float RelativeDifficulty(int globalLevel) =>
            EraPressure(EraOf(globalLevel)) * InEraProgress(LevelInEra(globalLevel)) * ChapterSaw(ChapterPosition(globalLevel));

        // ---------- §8.5 Ouro ----------
        public static int KillGold(float baseHp) => Mathf.CeilToInt(baseHp / 20f);
        public static int WaveGold(int wave) => 50 + 5 * wave;
        public static int StartGold(int levelInEra) => Mathf.Min(800, 300 + 5 * (levelInEra - 1));

        // ---------- §7.3 / §7.5 Torres ----------
        public static int TierCost(int baseCost, int tier) => Mathf.RoundToInt(baseCost * TierCostMultipliers[tier - 1]);
        public static int SellValue(int accumulatedCost) => Mathf.CeilToInt(accumulatedCost * SellRatio);

        // ---------- §1.3 Onboarding (Era 1) ----------
        /// <summary>Tier máximo de upgrade liberado no nível L.</summary>
        public static int MaxTierForLevel(int globalLevel)
        {
            if (EraOf(globalLevel) > 1) return 5;
            int ch = ChapterOf(globalLevel);
            return ch <= 1 ? 2 : ch <= 3 ? 3 : ch <= 6 ? 4 : 5;
        }

        /// <summary>Arquétipos de torre liberados no nível L.</summary>
        public static bool IsArchetypeUnlocked(TowerArchetype a, int globalLevel)
        {
            if (EraOf(globalLevel) > 1) return true;
            int ch = ChapterOf(globalLevel);
            switch (a)
            {
                case TowerArchetype.Marksman:
                case TowerArchetype.Launcher: return true;
                case TowerArchetype.Alchemist: return ch >= 2;
                default: return ch >= 4;   // Guard
            }
        }

        /// <summary>Heróis permitidos no loadout no nível L.</summary>
        public static int HeroSlotsForLevel(int globalLevel)
        {
            if (EraOf(globalLevel) > 1) return MaxLoadoutHeroes;
            int ch = ChapterOf(globalLevel);
            return ch <= 1 ? 0 : ch <= 6 ? 1 : 2;
        }

        // ---------- §1.2 Estrelas (por vazamentos) ----------
        public static int Stars(bool won, int livesLost, int startLives)
        {
            if (!won) return 0;
            if (livesLost == 0) return 3;
            return livesLost <= startLives * 0.2f ? 2 : 1;
        }

        // ---------- §6.1 Heróis ----------
        public static float HeroRespawnSeconds(int era) => 20f + 2f * era;

        // ---------- §5.2 Mitigação ----------
        /// <summary>R_ef = max(0, R × (1 − shred) − penFlat).</summary>
        public static float EffectiveResist(float resist, float shred, float flatPen) =>
            Mathf.Max(0f, resist * (1f - Mathf.Clamp01(shred)) - flatPen);

        /// <summary>Mit = 100 / (100 + R_ef) para Físico/Mágico; 1 para Verdadeiro.</summary>
        public static float Mitigation(DamageType type, float resist, float shred, float flatPen) =>
            type == DamageType.True ? 1f : 100f / (100f + EffectiveResist(resist, shred, flatPen));
    }
}
