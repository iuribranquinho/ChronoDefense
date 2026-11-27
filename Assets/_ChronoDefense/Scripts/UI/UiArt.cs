// Arte de interface pintada pelo usuário (Resources/Sprites/UI). Carregada uma vez e guardada em cache.
// Se um arquivo não existir, o chamador usa o desenho procedural (ArtLibrary) — o jogo nunca quebra por arte faltando.
using System.Collections.Generic;
using ChronoDefense.Data;
using UnityEngine;

namespace ChronoDefense.UI
{
    public static class UiArt
    {
        const string Root = "Sprites/UI/";
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Textura em Resources/Sprites/UI/<paramref name="path"/> (sem extensão) ou null.</summary>
        public static Texture2D Get(string path)
        {
            if (!Cache.TryGetValue(path, out Texture2D t))
            {
                t = Resources.Load<Texture2D>(Root + path);
                Cache[path] = t;
            }
            return t;
        }

        // ---- Loja (02_Painel_Invocacao_Torres_Pre_Historica)
        /// <summary>Ícone redondo de moldura de presas com a plaquinha de preço vazia (o jogo escreve o preço).</summary>
        public static Texture2D TowerIcon(TowerArchetype a, int era = 1) => Get($"Shop/TWR_E{era}_Icon_{a}");
        /// <summary>Área do número na plaquinha, normalizada no ícone (x0, y0, x1, y1 com y de BAIXO para cima).</summary>
        public static readonly Rect PricePlate = Rect.MinMaxRect(0.433f, 0.043f, 0.732f, 0.206f);

        // ---- Habilidade do herói (03_Botao_Habilidade_Letal_Krog_Caca_Coordenada_Fogo)
        public static Texture2D SkillReady(int heroId) => Get($"Skills/SKL_{heroId:00}_Ready");
        public static Texture2D SkillGlow(int heroId) => Get($"Skills/SKL_{heroId:00}_Glow");
        /// <summary>Diâmetro do disco interno (pintura) em relação à largura do botão.</summary>
        public const float SkillInnerDisc = 0.64f;

        // ---- Menu inicial pintado (tela inteira: mapa da saga + logo + painel da fase + botões)
        // Arte do usuário de 27/11 (1672×941). Removidos da pintura: título/nome da fase e as estrelas das pedras 3 e 10,
        // que o jogo desenha conforme a fase escolhida e o progresso. Coordenadas em px da arte, y de CIMA para baixo.
        public static Texture2D MenuMain(int era) => Get($"Menu/MENU_E{era}_Main");
        public static Texture2D MenuStar => Get("Menu/MENU_StarGold");
        public static Texture2D MenuRing => Get("Menu/MENU_SelectRing");
        public static readonly Vector2 MenuSize = new Vector2(1672f, 941f);
        /// <summary>Centro do disco numerado de cada fase (x, y) e o raio do disco (z).</summary>
        public static readonly Vector3[] MenuStones =
        {
            new Vector3(520f, 762f, 40f), new Vector3(627.5f, 660f, 35f), new Vector3(450f, 602.5f, 33f),
            new Vector3(600f, 565f, 33f), new Vector3(700f, 490f, 32f), new Vector3(632.5f, 405f, 30f),
            new Vector3(450f, 370f, 30f), new Vector3(612.5f, 307.5f, 30f), new Vector3(500f, 186f, 27f),
            new Vector3(600f, 150f, 38f),
        };
        // Painel da direita (retângulos x0, y0, x1, y1)
        public static readonly Rect MenuTitle = Rect.MinMaxRect(1180f, 284f, 1425f, 346f);
        public static readonly Rect MenuSubtitle = Rect.MinMaxRect(1180f, 343f, 1425f, 380f);
        public static readonly Rect MenuThumb = Rect.MinMaxRect(1088f, 384f, 1517f, 587f);
        public static readonly Vector2[] MenuStarSlots = { new Vector2(1238f, 620f), new Vector2(1312f, 620f), new Vector2(1385f, 620f) };
        public const float MenuStarSlotSize = 62f;
        public static readonly Rect MenuPlay = Rect.MinMaxRect(1105f, 660f, 1540f, 775f);
        public static readonly Rect MenuTest = Rect.MinMaxRect(1005f, 808f, 1385f, 890f);
        public static readonly Rect MenuQuit = Rect.MinMaxRect(1410f, 805f, 1635f, 892f);
        public static readonly Rect MenuGear = Rect.MinMaxRect(1578f, 26f, 1654f, 100f);

        // ---- Mapa da saga (04_Interface_Mapa_Saga)
        public static Texture2D SagaMap(int era) => Get($"Saga/SAGA_E{era}_Map");
        public static Texture2D SagaStone(int level) => Get($"Saga/SAGA_Stone_{level:00}");
        public static Texture2D SagaStar => Get("Saga/SAGA_Star");
        public static readonly Vector2 SagaMapSize = new Vector2(829f, 1136f);
        /// <summary>Centro de cada pedra de fase no mapa (px da arte, y de CIMA para baixo) e a largura da pedra pintada.</summary>
        public static readonly Vector3[] SagaStones =
        {
            new Vector3(350.0f, 1047.5f, 94f),   // 1
            new Vector3(472.5f, 912.5f, 89f),    // 2
            new Vector3(277.5f, 835.0f, 89f),    // 3
            new Vector3(467.5f, 782.5f, 89f),    // 4
            new Vector3(570.0f, 675.0f, 84f),    // 5
            new Vector3(497.5f, 557.5f, 89f),    // 6
            new Vector3(280.0f, 520.0f, 84f),    // 7
            new Vector3(465.0f, 427.5f, 84f),    // 8 (no portão)
            new Vector3(327.5f, 250.0f, 79f),    // 9
            new Vector3(483.5f, 172.0f, 87f),    // 10 (covil)
        };
    }
}
