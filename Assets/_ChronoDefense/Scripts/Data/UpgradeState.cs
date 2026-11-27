using System;

namespace ChronoDefense.Data
{
    /// <summary>
    /// GDD §7.4 — estado de upgrade (t_A, t_B, t_C) com a trava 5-2-0.
    /// Struct de runtime, blittable, sem alocação.
    /// </summary>
    [Serializable]
    public struct UpgradeState
    {
        public const int PathCount = 3;
        public const int MaxTier = 5;

        public byte A, B, C;

        public byte Get(int path) => path == 0 ? A : path == 1 ? B : C;

        /// <summary>Maior tier entre os caminhos (define o estágio visual, §7.6).</summary>
        public int HighestTier => A > B ? (A > C ? A : C) : (B > C ? B : C);

        /// <summary>Caminho com maior tier (empate → menor índice).</summary>
        public int DominantPath => A >= B && A >= C ? 0 : B >= C ? 1 : 2;

        /// <summary>Estágio visual L1/L2/L3 (1..3) conforme §7.6.</summary>
        public int VisualStage
        {
            get
            {
                int h = HighestTier;
                return h >= 5 ? 3 : h >= 3 ? 2 : 1;
            }
        }

        public bool CanUpgrade(int path, int maxTierUnlocked)
        {
            int a = A + (path == 0 ? 1 : 0);
            int b = B + (path == 1 ? 1 : 0);
            int c = C + (path == 2 ? 1 : 0);
            int next = path == 0 ? a : path == 1 ? b : c;
            if (next > MaxTier || next > maxTierUnlocked) return false;   // trava de onboarding (§1.3)
            int active = (a > 0 ? 1 : 0) + (b > 0 ? 1 : 0) + (c > 0 ? 1 : 0);
            int deep = (a > 2 ? 1 : 0) + (b > 2 ? 1 : 0) + (c > 2 ? 1 : 0);
            return active <= 2 && deep <= 1;
        }

        /// <summary>Aplica o upgrade se válido. Retorna false sem alterar o estado caso contrário.</summary>
        public bool TryUpgrade(int path, int maxTierUnlocked)
        {
            if (!CanUpgrade(path, maxTierUnlocked)) return false;
            if (path == 0) A++; else if (path == 1) B++; else C++;
            return true;
        }
    }
}
