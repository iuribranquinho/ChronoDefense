// Estado entre cenas: fase escolhida no menu e melhor resultado (estrelas) de cada fase.
// Estrelas ficam em PlayerPrefs até existir o save completo (GDD §15, M5).
using ChronoDefense.Data;
using UnityEngine;

namespace ChronoDefense.Core
{
    public static class GameSession
    {
        public const string MenuScene = "MainMenu";
        public const string GameplayScene = "Gameplay";

        /// <summary>Fase escolhida no menu; null = usa a fase configurada na cena Gameplay (testes, editor).</summary>
        public static LevelDataSO SelectedLevel;

        static string Key(LevelDataSO level) => "ChronoDefense.Stars." + level.name;

        public static int BestStars(LevelDataSO level) => level != null ? PlayerPrefs.GetInt(Key(level), 0) : 0;

        /// <summary>Guarda só se for melhor que o anterior (chamado uma vez, no fim da partida).</summary>
        public static void SaveStars(LevelDataSO level, int stars)
        {
            if (level == null || stars <= BestStars(level)) return;
            PlayerPrefs.SetInt(Key(level), stars);
            PlayerPrefs.Save();
        }
    }
}
