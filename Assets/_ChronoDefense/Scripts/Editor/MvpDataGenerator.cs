// Gera os dados do MVP (GDD §15): Era 1, Capítulo 1 (níveis 1–10), chefe Mamute-Lanudo Alfa no Covil do Krog.
// Menu: Tools ▸ Chrono ▸ Gerar dados do MVP. Reexecutável: atualiza os assets existentes
// no lugar (mantém GUIDs e referências).
//
// Origem dos valores:
//  - Exatos do GDD: creeps (§9.2), chefe (§8.4, §9.2), torres (§7.1, §7.3), Krog (§6.2), ondas da Fase 10 (§9.2).
//  - Exatos da Bíblia: paleta e iluminação da Tundra (§4.1), largura de caminho (§3.5).
//  - PROVISÓRIOS (marcados "PROV"): layout, ondas das fases 1–9, intervalos de spawn, ShadowTint,
//    velocidade do Caçador Corrompido. A calibrar no simulador (GDD §14.3).
using System.Collections.Generic;
using ChronoDefense.Data;
using UnityEditor;
using UnityEngine;

namespace ChronoDefense.Editor
{
    public static class MvpDataGenerator
    {
        const string Root = "Assets/_ChronoDefense/Data";
        const string E1 = Root + "/E1";

        // ---------------- Layouts da Era 1 no modelo "Prado" (GDD §10.7, 27/11) ----------------
        // Plano XZ em metros, +Z = topo da tela. Caminho de 3,5 m, SEM cruzamentos, ≥ 6,5 m entre eixos de trechos
        // vizinhos. Sem bases: o primeiro e o último ponto ficam ≥ 8 m FORA do quadro (inimigos entram por uma
        // borda e saem por outra). Quadro = pontos internos do caminho + 4,5 m (FrameMargin).
        public readonly struct LayoutDef
        {
            public readonly string File, Name;
            public readonly int Id;
            public readonly Vector3[] Path, Spots;
            public LayoutDef(string file, int id, string name, Vector3[] path, Vector3[] spots)
            { File = file; Id = id; Name = name; Path = path; Spots = spots; }
            public Bounds Frame => FrameBounds(Path);
        }

        public static readonly LayoutDef[] Layouts =
        {
            // Vale do Rio (era "Vale Gelado") — fases 1–2, mata: S largo e simples (onboarding)
            new LayoutDef("LAY_E1_A_ValeGelado", 111, "Vale do Rio",
                new[]
                {
                    new Vector3(-36f, 0f, 12f),
                    new Vector3(18f, 0f, 12f),
                    new Vector3(18f, 0f, 0f),
                    new Vector3(-18f, 0f, 0f),
                    new Vector3(-18f, 0f, -12f),
                    new Vector3(36f, 0f, -12f),
                },
                new[] { new Vector3(12.8f, 0f, 6.3f), new Vector3(-12.7f, 0f, -5.7f), new Vector3(14.3f, 0f, -5.2f), new Vector3(7.3f, 0f, -5.2f), new Vector3(5.3f, 0f, 6.8f), new Vector3(-1.7f, 0f, 6.8f), new Vector3(-2.2f, 0f, -5.2f), new Vector3(-9.2f, 0f, 5.3f) }),
            // Trilha dos Lobos — fases 3–4: zigue-zague vertical, entra no topo e sai embaixo
            new LayoutDef("LAY_E1_B_TrilhaDosLobos", 112, "Trilha dos Lobos",
                new[]
                {
                    new Vector3(-18f, 0f, 30f),
                    new Vector3(-18f, 0f, -14f),
                    new Vector3(-9f, 0f, -14f),
                    new Vector3(-9f, 0f, 14f),
                    new Vector3(0f, 0f, 14f),
                    new Vector3(0f, 0f, -14f),
                    new Vector3(9f, 0f, -14f),
                    new Vector3(9f, 0f, 14f),
                    new Vector3(18f, 0f, 14f),
                    new Vector3(18f, 0f, -30f),
                },
                new[] { new Vector3(13.8f, 0f, 7.8f), new Vector3(4.8f, 0f, -7.2f), new Vector3(-4.2f, 0f, 7.8f), new Vector3(-13.2f, 0f, -7.2f), new Vector3(5.8f, 0f, 7.8f), new Vector3(-3.2f, 0f, -8.7f), new Vector3(14.3f, 0f, 0.3f), new Vector3(13.8f, 0f, -6.7f) }),
            // Pedras Erguidas (era "Lago Congelado") — fases 5–6, savana: serpentina com entrada e saída à direita
            new LayoutDef("LAY_E1_C_LagoCongelado", 113, "Pedras Erguidas",
                new[]
                {
                    new Vector3(36f, 0f, 14f),
                    new Vector3(-18f, 0f, 14f),
                    new Vector3(-18f, 0f, 4f),
                    new Vector3(18f, 0f, 4f),
                    new Vector3(18f, 0f, -6f),
                    new Vector3(-18f, 0f, -6f),
                    new Vector3(-18f, 0f, -16f),
                    new Vector3(36f, 0f, -16f),
                },
                new[] { new Vector3(12.3f, 0f, -0.7f), new Vector3(-11.7f, 0f, 9.8f), new Vector3(-11.7f, 0f, -10.2f), new Vector3(15.3f, 0f, 8.8f), new Vector3(15.3f, 0f, -10.7f), new Vector3(8.3f, 0f, 10.3f), new Vector3(8.3f, 0f, -11.2f), new Vector3(5.3f, 0f, 0.3f) }),
            // Desfiladeiro — fases 7–9: o traçado do mapa-modelo "Prado"
            new LayoutDef("LAY_E1_D_Desfiladeiro", 114, "Desfiladeiro",
                new[]
                {
                    new Vector3(-34f, 0f, 4.5f),
                    new Vector3(-13.75f, 0f, 4.5f),
                    new Vector3(-13.75f, 0f, 15.5f),
                    new Vector3(0f, 0f, 15.5f),
                    new Vector3(0f, 0f, -3f),
                    new Vector3(11.25f, 0f, -3f),
                    new Vector3(11.25f, 0f, 8.5f),
                    new Vector3(20.5f, 0f, 8.5f),
                    new Vector3(20.5f, 0f, -9.5f),
                    new Vector3(-19.5f, 0f, -9.5f),
                    new Vector3(-19.5f, 0f, -17f),
                    new Vector3(-3.75f, 0f, -17f),
                    new Vector3(-3.75f, 0f, -30f),
                },
                new[] { new Vector3(16.3f, 0f, 2.3f), new Vector3(-12.7f, 0f, -13.2f), new Vector3(6.3f, 0f, 2.8f), new Vector3(14.3f, 0f, -4.7f), new Vector3(6.8f, 0f, -6.2f), new Vector3(-6.7f, 0f, 11.8f), new Vector3(-5.7f, 0f, -13.2f), new Vector3(-0.2f, 0f, -6.2f) }),
            // Covil do Krog — fase 10 (chefe): serpentina longa, entra à direita e sai à esquerda
            new LayoutDef("LAY_E1_E_CovilDoKrog", 115, "Covil do Krog",
                new[]
                {
                    new Vector3(36f, 0f, 16f),
                    new Vector3(-18f, 0f, 16f),
                    new Vector3(-18f, 0f, 6f),
                    new Vector3(18f, 0f, 6f),
                    new Vector3(18f, 0f, -4f),
                    new Vector3(-18f, 0f, -4f),
                    new Vector3(-18f, 0f, -14f),
                    new Vector3(-36f, 0f, -14f),
                },
                new[] { new Vector3(12.3f, 0f, 1.3f), new Vector3(-11.7f, 0f, 11.8f), new Vector3(15.3f, 0f, 10.8f), new Vector3(8.3f, 0f, 12.3f), new Vector3(5.3f, 0f, 2.3f), new Vector3(1.3f, 0f, 10.3f), new Vector3(-1.7f, 0f, 0.8f), new Vector3(-8.7f, 0f, 1.8f) }),
        };

        /// <summary>Layout de cada fase 1–10 (índice em <see cref="Layouts"/>).</summary>
        public static readonly int[] LevelLayout = { 0, 0, 1, 1, 2, 2, 3, 3, 3, 4 };
        /// <summary>
        /// Bioma de cada fase, seguindo a estrada do mapa da saga (27/11): rio e aldeia em mata fechada (1–4),
        /// quase deserto depois das pedras erguidas (5–7), areia do portão azul (8), planalto rochoso (9) e neve no covil (10).
        /// </summary>
        public static readonly GroundStyle[] LevelBiome =
        {
            GroundStyle.Forest, GroundStyle.Forest, GroundStyle.Forest, GroundStyle.Forest,
            GroundStyle.Savanna, GroundStyle.Savanna, GroundStyle.Savanna,
            GroundStyle.Desert, GroundStyle.Highland, GroundStyle.Tundra,
        };

        /// <summary>Fase 1 (compatibilidade com testes e ferramentas antigas).</summary>
        public static Vector3[] PathPolyline => Layouts[0].Path;
        public static Vector3[] Pedestals => Layouts[0].Spots;

        public const float PathWidth = 3.5f;         // GDD §10.7: todos os mapas novos
        public const float CommonPathWidth = PathWidth;
        public const float BossPathWidth = PathWidth;
        public const float FrameMargin = 4.5f;       // meia pista + uma unidade
        public const float OffscreenMin = 8f;        // entrada/saída além da borda
        public const float MinSegmentGap = 6.5f;     // entre eixos de trechos não vizinhos
        public const float PedestalRadius = 1.3f;    // = CombatSimulation.FootprintRadius (base da unidade)
        public const float MinEdgeClearance = 0f;    // base inteira fora da pista

        /// <summary>Quadro do mapa: caixa dos pontos internos do caminho + FrameMargin; altura 1 m (sem arte alta).</summary>
        public static Bounds FrameBounds(Vector3[] path)
        {
            var b = new Bounds(path[1], Vector3.zero);
            for (int i = 2; i < path.Length - 1; i++) b.Encapsulate(path[i]);
            b.Expand(new Vector3(2f * FrameMargin, 0f, 2f * FrameMargin));
            b.center = new Vector3(b.center.x, 0.5f, b.center.z);
            b.size = new Vector3(b.size.x, 1f, b.size.z);
            return b;
        }

        /// <summary>Menor distância entre eixos de trechos não vizinhos (detecta cruzamentos: 0).</summary>
        public static float MinGapBetweenSegments(Vector3[] path)
        {
            float best = float.MaxValue;
            for (int i = 0; i < path.Length - 1; i++)
                for (int j = i + 2; j < path.Length - 1; j++)
                    for (int k = 0; k <= 50; k++)
                    {
                        Vector3 p = Vector3.Lerp(path[i], path[i + 1], k / 50f);
                        best = Mathf.Min(best, DistanceToSegment(p, path[j], path[j + 1]));
                    }
            return best;
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 pp = new Vector2(p.x, p.z), aa = new Vector2(a.x, a.z), ab = new Vector2(b.x - a.x, b.z - a.z);
            float t = Mathf.Clamp01(Vector2.Dot(pp - aa, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(pp, aa + ab * t);
        }

        /// <summary>Quanto um ponto está fora do quadro (≤ 0 = dentro).</summary>
        public static float OutsideBy(Bounds frame, Vector3 p) =>
            Mathf.Max(Mathf.Max(frame.min.x - p.x, p.x - frame.max.x), Mathf.Max(frame.min.z - p.z, p.z - frame.max.z));

        /// <summary>Menor folga entre a borda de um pedestal e a borda do caminho.</summary>
        public static float MinPedestalClearance(Vector3[] path, Vector3[] pedestals, float pathWidth)
        {
            float min = float.MaxValue;
            for (int i = 0; i < pedestals.Length; i++)
            {
                Vector2 p = new Vector2(pedestals[i].x, pedestals[i].z);
                for (int s = 0; s < path.Length - 1; s++)
                {
                    Vector2 a = new Vector2(path[s].x, path[s].z), b = new Vector2(path[s + 1].x, path[s + 1].z);
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    float d = Vector2.Distance(p, a + ab * t) - pathWidth * 0.5f - PedestalRadius;
                    if (d < min) min = d;
                }
            }
            return min;
        }

        /// <summary>
        /// Alcance mínimo de qualquer unidade de pedestal: do centro do pedestal até a borda OPOSTA do caminho
        /// mais próximo (o chão do outro lado da rua), com a largura do caminho de chefe. Neste layout: 6 + 1,75 = 7,75 m.
        /// </summary>
        public static float RequiredReach(Vector3[] path, Vector3[] pedestals, float pathWidth)
        {
            float worst = 0f;
            for (int i = 0; i < pedestals.Length; i++)
            {
                Vector2 p = new Vector2(pedestals[i].x, pedestals[i].z);
                float nearest = float.MaxValue;
                for (int s = 0; s < path.Length - 1; s++)
                {
                    Vector2 a = new Vector2(path[s].x, path[s].z), b = new Vector2(path[s + 1].x, path[s + 1].z);
                    Vector2 ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    nearest = Mathf.Min(nearest, Vector2.Distance(p, a + ab * t));
                }
                worst = Mathf.Max(worst, nearest + pathWidth * 0.5f);
            }
            return worst;
        }

        // Alcances base (m). Regra: >= RequiredReach (tudo no pedestal alcança o outro lado do caminho).
        // Antes: Lançador 4,5 · Alquimista 5,0 · Guarda 2,0 · Krog 1,5 (corpo a corpo, alcance real 3,5 pela guarda);
        // depois 6,0 para o layout antigo (5,75 m). Layout atual exige 7,75 m → 8,0.
        public static readonly float[] TowerBaseRange = { 8.0f, 8.0f, 8.0f, 8.0f };   // Atirador, Lançador, Alquimista, Guarda
        public const float KrogAttackRange = 8.0f;                                     // lança arremessada (vira atacante à distância)

        // ---------------- Fase de teste visual "Prado" (26/11, proporções do BTD6) ----------------
        // Caminho de 3,5 m, traçado próprio e SEM cruzamentos (o flow field, GDD §4.3, faria os inimigos
        // cortarem caminho onde a estrada passa por cima dela mesma). Estilo BTD6: sem caverna nem Núcleo; o
        // caminho começa fora da borda esquerda e termina fora da borda de baixo. O quadro (TestBounds) encosta
        // no caminho: 4,5 m além do eixo = meia pista + espaço para uma unidade.
        public static readonly Vector3[] TestPath =
        {
            new Vector3(-34f,    0f,   4.5f),   // fora da tela (entrada pela borda esquerda)
            new Vector3(-13.75f, 0f,   4.5f),
            new Vector3(-13.75f, 0f,  15.5f),
            new Vector3(  0f,    0f,  15.5f),
            new Vector3(  0f,    0f,  -3f),
            new Vector3( 11.25f, 0f,  -3f),
            new Vector3( 11.25f, 0f,   8.5f),
            new Vector3( 20.5f,  0f,   8.5f),
            new Vector3( 20.5f,  0f,  -9.5f),
            new Vector3(-19.5f,  0f,  -9.5f),
            new Vector3(-19.5f,  0f, -17f),
            new Vector3( -3.75f, 0f, -17f),
            new Vector3( -3.75f, 0f, -30f),     // fora da tela (saída pela borda de baixo)
        };
        /// <summary>Pontos sugeridos (invisíveis; o jogador constrói em qualquer chão livre).</summary>
        public static readonly Vector3[] TestSpots =
        {
            new Vector3(-6.9f, 0f, 10f), new Vector3(5.6f, 0f, 3f), new Vector3(15.9f, 0f, 0f),
            new Vector3(-10f, 0f, -13f), new Vector3(-19f, 0f, 11f), new Vector3(6f, 0f, 12f),
        };
        /// <summary>x −24…25 · z −21,5…20 (49 × 41,5 m): eixo do caminho + 4,5 m. Altura 1 m (sem arte alta).</summary>
        public static readonly Bounds TestBounds = new Bounds(new Vector3(0.5f, 0.5f, -0.75f), new Vector3(49f, 1f, 41.5f));
        const string TestLevelPath = E1 + "/Levels/LVL_TEST_Prado.asset";

        [MenuItem("Tools/Chrono/Fase de teste visual (estilo BTD6)")]
        public static void GenerateVisualTest()
        {
            var era = AssetDatabase.LoadAssetAtPath<EraSO>($"{E1}/ERA_1_PreHistory.asset");
            var source = AssetDatabase.LoadAssetAtPath<LevelDataSO>($"{E1}/Levels/LVL_003.asset");
            if (era == null || source == null)
            {
                Generate();
                era = AssetDatabase.LoadAssetAtPath<EraSO>($"{E1}/ERA_1_PreHistory.asset");
                source = AssetDatabase.LoadAssetAtPath<LevelDataSO>($"{E1}/Levels/LVL_003.asset");
            }

            var l = GetOrCreate<LevelLayoutSO>($"{E1}/Layouts/LAY_TEST_Prado.asset");
            l.Id = 900;
            l.DisplayName = "Prado (teste)";
            l.PathPolyline = (Vector3[])TestPath.Clone();
            l.PathWidth = BossPathWidth;
            l.Pedestals = (Vector3[])TestSpots.Clone();
            l.LairPosition = TestPath[0];
            l.CorePosition = TestPath[TestPath.Length - 1];
            l.CameraBounds = TestBounds;
            l.ShowLairAndCore = false;
            l.Ground = GroundStyle.Meadow;       // o "Prado" guarda o acabamento verde do teste
            EditorUtility.SetDirty(l);

            var level = GetOrCreate<LevelDataSO>(TestLevelPath);
            level.GlobalIndex = 31;              // capítulo 4: as 4 torres liberadas para avaliar a loja inteira
            level.Era = era;
            level.Layout = l;
            level.Waves = source.Waves;          // ondas da fase 3 (lobos e tigres)
            level.Boss = null;
            level.StartGoldOverride = 650;       // como o início do BTD6
            level.Lives = 20;                    // padrão do jogo (GDD §1.2); 150 deixava a fase sem risco
            level.UnlocksHero = null;
            level.Rewards = default;
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            UseLevelInOpenScene(level);
        }

        [MenuItem("Tools/Chrono/Voltar para a Fase 10 (chefe)")]
        public static void UseLevel10() => UseLevelInOpenScene(AssetDatabase.LoadAssetAtPath<LevelDataSO>($"{E1}/Levels/LVL_010.asset"));

        static void UseLevelInOpenScene(LevelDataSO level)
        {
            var game = Object.FindAnyObjectByType<ChronoDefense.Core.GameBootstrap>();
            if (game == null || level == null)
            {
                Debug.LogWarning("[MVP] Abra a cena Assets/_ChronoDefense/Scenes/Gameplay.unity e rode o menu de novo.");
                return;
            }
            Undo.RecordObject(game, "Trocar fase");
            game.Level = level;
            EditorUtility.SetDirty(game);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[MVP] Cena Gameplay agora abre {level.name}. Aperte Play.");
        }

        // ---------------- Ondas da Fase 10 (GDD §9.2, exato) ----------------
        public enum E { Wolf, Tiger, Rhino, Mammoth }
        public static readonly (E type, int count)[][] Level10Waves =
        {
            new[] { (E.Wolf, 8) },
            new[] { (E.Wolf, 10) },
            new[] { (E.Wolf, 14) },
            new[] { (E.Tiger, 5), (E.Wolf, 6) },
            new[] { (E.Tiger, 7), (E.Wolf, 6) },
            new[] { (E.Tiger, 9) },
            new[] { (E.Rhino, 3), (E.Wolf, 6) },
            new[] { (E.Rhino, 4), (E.Tiger, 4) },
            new[] { (E.Rhino, 6) },
            new[] { (E.Mammoth, 1), (E.Wolf, 6) },
        };

        [MenuItem("Tools/Chrono/Gerar dados do MVP (Era 1 · Cap. 1)")]
        public static void Generate()
        {
            // Valida os layouts antes de gravar qualquer asset (GDD §10.7)
            float clearance = float.MaxValue, reach = 0f;
            foreach (var def in Layouts)
            {
                Bounds frame = def.Frame;
                float gap = MinGapBetweenSegments(def.Path);
                if (gap < MinSegmentGap) { Debug.LogError($"[MVP] {def.Name}: trechos a {gap:0.00} m (mínimo {MinSegmentGap}). Geração abortada."); return; }
                if (OutsideBy(frame, def.Path[0]) < OffscreenMin || OutsideBy(frame, def.Path[def.Path.Length - 1]) < OffscreenMin)
                { Debug.LogError($"[MVP] {def.Name}: entrada/saída precisam ficar {OffscreenMin} m fora da tela. Geração abortada."); return; }
                clearance = Mathf.Min(clearance, MinPedestalClearance(def.Path, def.Spots, PathWidth));
                reach = Mathf.Max(reach, RequiredReach(def.Path, def.Spots, PathWidth));
            }
            if (clearance < MinEdgeClearance)
            {
                Debug.LogError($"[MVP] Ponto sugerido a {clearance:0.00} m do caminho. Geração abortada.");
                return;
            }
            if (Mathf.Min(KrogAttackRange, Mathf.Min(TowerBaseRange[0], Mathf.Min(TowerBaseRange[1], Mathf.Min(TowerBaseRange[2], TowerBaseRange[3])))) < reach)
            {
                Debug.LogError($"[MVP] Há alcance menor que {reach:0.00} m (outro lado do caminho). Geração abortada.");
                return;
            }

            // Sem StartAssetEditing: dentro do lote, IsValidFolder não enxerga pastas recém-criadas.
            {
                var theme = BuildTheme();
                var enemies = BuildEnemies(out var hunter);
                var mammothBoss = BuildMammothBoss(enemies[E.Wolf]);
                var krogHero = BuildKrogHero();
                var towers = BuildTowers();
                var layouts = new LevelLayoutSO[Layouts.Length];
                for (int k = 0; k < Layouts.Length; k++) layouts[k] = BuildLayout(Layouts[k]);

                var era = GetOrCreate<EraSO>($"{E1}/ERA_1_PreHistory.asset");
                era.Id = 1;
                era.EraIndex = 1;
                era.Theme = theme;
                if (era.Levels == null || era.Levels.Length != BalanceFormulas.LevelsPerEra)
                    era.Levels = new LevelDataSO[BalanceFormulas.LevelsPerEra];

                for (int L = 1; L <= 10; L++)
                {
                    bool boss = BalanceFormulas.IsBossLevel(L);
                    var level = GetOrCreate<LevelDataSO>($"{E1}/Levels/LVL_{L:000}.asset");
                    level.GlobalIndex = L;
                    level.Era = era;
                    level.Layout = layouts[LevelLayout[L - 1]];
                    level.Biome = LevelBiome[L - 1];
                    level.Waves = boss ? BuildLevel10Waves(enemies, mammothBoss) : BuildCommonWaves(L, enemies);
                    level.Boss = boss ? mammothBoss : null;
                    level.StartGoldOverride = -1;        // G0(l)
                    level.Lives = 20;
                    level.UnlocksHero = boss ? krogHero : null;
                    level.Rewards = default;
                    EditorUtility.SetDirty(level);
                    era.Levels[L - 1] = level;
                }
                EditorUtility.SetDirty(era);

                var campaign = GetOrCreate<CampaignSO>($"{Root}/Campaign.asset");
                if (campaign.Eras == null || campaign.Eras.Length != BalanceFormulas.EraCount)
                    campaign.Eras = new EraSO[BalanceFormulas.EraCount];
                campaign.Eras[0] = era;
                if (campaign.Heroes == null || campaign.Heroes.Length != 22)
                    campaign.Heroes = new HeroDataSO[22];
                campaign.Heroes[0] = krogHero;
                campaign.Towers = towers;
                EditorUtility.SetDirty(campaign);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MVP] Dados da Era 1 · Capítulo 1 gerados em {Root}. 5 layouts (sem bases), alcance exigido {reach:0.00} m.");
        }

        // ---------------- Tema (Bíblia §4.1) ----------------
        static EraBiomeThemeSO BuildTheme()
        {
            var t = GetOrCreate<EraBiomeThemeSO>($"{E1}/THM_E1_Tundra.asset");
            t.Id = 1;
            t.EraIndex = 1;
            t.AccentRune = Hex("#6FD3FF");
            t.AmbientSky = Hex("#BFD6E6");
            t.AmbientEquator = Hex("#E6D8C3");
            t.AmbientGround = Hex("#5A4A3E");
            t.ShadowTint = t.AmbientSky * 0.6f; t.ShadowTint.a = 1f;   // PROV: céu da era escurecido (nunca preto)
            t.CorruptionTint = Hex("#3B1E54");
            t.MainLightColor = Hex("#FFF1DC");
            t.MainLightIntensity = 1.1f;
            t.MainLightEuler = new Vector3(50f, 135f, 0f);
            t.FogColor = Hex("#D5E3EC");
            t.FogStart = 40f;
            t.FogEnd = 90f;
            if (t.TowerSkins == null || t.TowerSkins.Length != 4) t.TowerSkins = new TowerSkin[4];
            for (int i = 0; i < 4; i++) t.TowerSkins[i].Archetype = (TowerArchetype)i;
            if (t.Props == null || t.Props.Length == 0)   // não apaga props já curados pelo designer
                t.Props = new[]
                {
                    new PropSet { SubBiomeName = "Tundra Gelada", FirstChapter = 1, LastChapter = 3, Entries = new WeightedProp[0] },
                };
            EditorUtility.SetDirty(t);
            return t;
        }

        // ---------------- Inimigos (GDD §9.2) ----------------
        static Dictionary<E, EnemyDataSO> BuildEnemies(out EnemyDataSO hunter)
        {
            var d = new Dictionary<E, EnemyDataSO>
            {
                [E.Wolf]  = Enemy("CRP_E1_GreyWolf",       101,  70f,  0f, 0f, 2.2f, 10f),
                [E.Tiger] = Enemy("CRP_E1_SabreTiger",     102, 180f, 15f, 0f, 1.6f, 30f),
                [E.Rhino] = Enemy("CRP_E1_WoollyRhino",    103, 450f, 40f, 0f, 0.9f, 80f),
            };
            hunter = Enemy("CRP_E1_CorruptedHunter", 104, 120f, 0f, 0f, 2.0f /* PROV */, 10f);
            return d;
        }

        static EnemyDataSO Enemy(string file, int id, float hp, float armor, float mr, float speed, float mass)
        {
            var e = GetOrCreate<EnemyDataSO>($"{E1}/Enemies/{file}.asset");
            e.Id = id;
            e.BaseHp = hp;
            e.Armor = armor;
            e.MagicResist = mr;
            e.Speed = speed;
            e.Mass = mass;
            e.Traits = EnemyTraits.None;
            e.LeakWeight = 1;
            e.GoldOverride = -1;                  // ceil(HP/20): 4 · 9 · 23 · 6
            EditorUtility.SetDirty(e);
            return e;
        }

        // ---------------- Chefe: Mamute-Lanudo Alfa (substitui o Krog Corrompido, 26/11) ----------------
        // O mamute tomou a caverna do Krog; ao vencê-lo, o Krog é resgatado (level.UnlocksHero).
        static BossDataSO BuildMammothBoss(EnemyDataSO wolf)
        {
            var b = GetOrCreate<BossDataSO>($"{E1}/Bosses/BOS_010_MammothAlpha.asset");
            b.Id = 10010;
            b.HpBase = 3000f;                     // → 3.538 na fase 10 (mesma dificuldade do chefe anterior)
            b.BaseHp = 3000f;
            b.Armor = 40f;                        // couro e pelo grosso: o Lançador (área) e o Atirador T2 resolvem
            b.MagicResist = 0f;
            b.Speed = 0.5f;
            b.Mass = 200f;
            b.Traits = EnemyTraits.Boss;
            b.LeakWeight = 999;                   // §1.2: chefe custa todas as vidas
            b.GoldOverride = BalanceFormulas.BossKillGold;
            b.Phases = new[]
            {
                // Pisão Glacial: a cada 12 s congela por 3 s a torre de um pedestal ocupado (telegrafia 1,5 s no sistema).
                new BossPhase { HpThreshold = 1f, Action = BossAction.DisablePedestal, Interval = 12f, Value = 3f },
                // Chamado da Manada aos 66% e 33%: 4 Lobos-Cinzentos saem da caverna.
                new BossPhase { HpThreshold = 0.66f, Action = BossAction.Summon, SummonType = wolf, SummonCount = 4 },
                new BossPhase { HpThreshold = 0.33f, Action = BossAction.Summon, SummonType = wolf, SummonCount = 4 },
            };
            EditorUtility.SetDirty(b);
            return b;
        }

        // ---------------- Herói: Krog (GDD §6.2) ----------------
        static HeroDataSO BuildKrogHero()
        {
            var s = GetOrCreate<SkillDataSO>($"{E1}/Heroes/SKL_01_CoordinatedFireHunt.asset");
            s.Id = 1;
            s.Cooldown = 30f;
            s.CastTime = 0.3f;                    // antecipação mínima (Bíblia §5.0)
            s.Targeting = SkillTargeting.Point;
            s.Radius = 3f;
            s.Length = 0f;
            s.Duration = 6f;
            s.Effects = new[]
            {
                // Magnitude de DoT = DPS.
                new SkillEffect { Type = EffectType.DoT, DamageType = DamageType.Magical, Magnitude = 40f,
                                  Duration = 6f, TickInterval = 0.5f, BossMultiplier = 1f },
                // +15% de DPS por inimigo extra na zona (teto +150%).
                new SkillEffect { Type = EffectType.CrowdBonus, Magnitude = 0.15f, BossMultiplier = 1f },
            };
            EditorUtility.SetDirty(s);

            var h = GetOrCreate<HeroDataSO>($"{E1}/Heroes/HER_01_Krog.asset");
            h.Id = 1;
            h.Era = 1;
            h.UnlockLevel = 10;
            h.SummonCost = 250;
            h.MaxHp = 1200f;
            h.Armor = 20f;
            h.MagicResist = 0f;
            h.AttackType = DamageType.Physical;
            h.AttackDamage = 45f;
            h.AttackInterval = 1.2f;
            h.AttackRange = KrogAttackRange;
            h.AttackHits = 1;
            h.GuardRadius = BalanceFormulas.DefaultGuardRadius;
            h.MassClass = 30f;
            h.Skill = s;
            h.Sensitivity = SensitivityFlags.None;
            EditorUtility.SetDirty(h);
            return h;
        }

        // ---------------- Torres (GDD §7.1, §7.3) ----------------
        static TowerDataSO[] BuildTowers()
        {
            var marksman = Tower("TWR_Marksman", 1, TowerArchetype.Marksman, 100, DamageType.Physical, 18f, 0.8f, TowerBaseRange[0], 0f, true);
            var launcher = Tower("TWR_Launcher", 2, TowerArchetype.Launcher, 150, DamageType.Physical, 40f, 2.2f, TowerBaseRange[1], 1.5f, false);
            var alchemist = Tower("TWR_Alchemist", 3, TowerArchetype.Alchemist, 140, DamageType.Magical, 30f, 1.4f, TowerBaseRange[2], 0f, true);
            var guard = Tower("TWR_Guard", 4, TowerArchetype.Guard, 120, DamageType.Physical, 0f, 0f, TowerBaseRange[3], 0f, false);
            guard.SoldierCount = 3;
            guard.SoldierHp = 150f;
            guard.SoldierDamage = 10f;
            guard.SoldierInterval = 1.1f;

            FillMarksmanTiers(marksman);
            FillLauncherTiers(launcher);

            EditorUtility.SetDirty(guard);
            EditorUtility.SetDirty(marksman);
            EditorUtility.SetDirty(launcher);
            return new[] { marksman, launcher, alchemist, guard };
        }

        static TowerDataSO Tower(string file, int id, TowerArchetype arch, int baseCost, DamageType type,
                                 float dmg, float interval, float range, float splash, bool hitsFlying)
        {
            var t = GetOrCreate<TowerDataSO>($"{E1}/Towers/{file}.asset");
            t.Id = id;
            t.Archetype = arch;
            t.BaseCost = baseCost;
            t.DamageType = type;
            t.Damage = dmg;
            t.Interval = interval;
            t.Range = range;
            t.SplashRadius = splash;
            t.HitsFlying = hitsFlying;
            t.DetectsCamo = false;
            EnsureTierStructure(t);
            EditorUtility.SetDirty(t);
            return t;
        }

        /// <summary>Cria a estrutura 3 × 5 e fixa k_t, preservando modificadores já preenchidos.</summary>
        public static void EnsureTierStructure(TowerDataSO t)
        {
            if (t.Paths == null || t.Paths.Length != UpgradeState.PathCount)
                t.Paths = new UpgradePath[UpgradeState.PathCount];
            for (int p = 0; p < t.Paths.Length; p++)
            {
                if (t.Paths[p].Tiers == null || t.Paths[p].Tiers.Length != UpgradeState.MaxTier)
                    t.Paths[p].Tiers = new UpgradeTier[UpgradeState.MaxTier];
                for (int k = 0; k < UpgradeState.MaxTier; k++)
                    t.Paths[p].Tiers[k].CostMultiplier = BalanceFormulas.TierCostMultipliers[k];
            }
        }

        // ---------------- Tiers T1–T2 (PROV, temas do GDD §7.2) ----------------
        // Acúmulo: (base + ΣAdd) × ΠMul (TowerDataSO.GetStat). OnHitEffects: vale o do maior tier.
        // Só preenche tiers vazios; para regenerar um tier, apague seus Modifiers/OnHitEffects no Inspector.

        /// <summary>Atirador (B 100 · 18 F / 0,8 s · 6 m).</summary>
        public static void FillMarksmanTiers(TowerDataSO t)
        {
            // A — Precisão (dano e alcance): 18 → 26 → 38 F; alcance 6,75 m no T2.
            Fill(t, 0, 0, Mods(Add(StatId.Damage, 8f)));
            Fill(t, 0, 1, Mods(Add(StatId.Damage, 12f), Add(StatId.Range, 0.75f)));
            // B — Rajada (cadência): 0,80 → 0,68 → 0,51 s.
            Fill(t, 1, 0, Mods(Mul(StatId.Interval, 0.85f)));
            Fill(t, 1, 1, Mods(Mul(StatId.Interval, 0.75f)));
            // C — Batedor: alcance 7 → 7,5 m; T2 detecta camuflados (GDD §7.1) e ganha 10% de crítico.
            Fill(t, 2, 0, Mods(Add(StatId.Range, 1.0f)));
            Fill(t, 2, 1, Mods(Add(StatId.Range, 0.5f), Add(StatId.CritChance, 0.10f)));
            t.Paths[2].Tiers[1].GrantsCamoDetection = true;
        }

        /// <summary>Lançador (B 150 · 40 F r1,5 m / 2,2 s · 4,5 m).</summary>
        public static void FillLauncherTiers(TowerDataSO t)
        {
            // A — Impacto (dano e raio): 40 → 55 → 75 F; raio 1,8 m no T2.
            Fill(t, 0, 0, Mods(Add(StatId.Damage, 15f)));
            Fill(t, 0, 1, Mods(Add(StatId.Damage, 20f), Add(StatId.SplashRadius, 0.3f)));
            // B — Incendiário: queimadura mágica em área por 3 s (renova a cada impacto, §5.3): 5 → 12 DPS.
            Fill(t, 1, 0, Mods(), Fx(Burn(5f)));
            Fill(t, 1, 1, Mods(), Fx(Burn(12f)));
            // C — Concussão: empurrão 0,6 → 0,8 m (chefe × 0,25, §5.3); T2 soma atordoamento de 0,3 s (chefe × 0,5).
            Fill(t, 2, 0, Mods(), Fx(Knockback(0.6f)));
            Fill(t, 2, 1, Mods(), Fx(Knockback(0.8f),
                new SkillEffect { Type = EffectType.Stun, DamageType = DamageType.Physical, Duration = 0.3f, BossMultiplier = 0.5f }));
        }

        static void Fill(TowerDataSO t, int path, int tierIndex, StatModifier[] mods, SkillEffect[] fx = null)
        {
            ref UpgradeTier u = ref t.Paths[path].Tiers[tierIndex];
            bool curated = (u.Modifiers != null && u.Modifiers.Length > 0) || (u.OnHitEffects != null && u.OnHitEffects.Length > 0);
            if (curated) return;
            u.Modifiers = mods;
            u.OnHitEffects = fx ?? new SkillEffect[0];
        }

        static StatModifier[] Mods(params StatModifier[] m) => m;
        static SkillEffect[] Fx(params SkillEffect[] e) => e;
        static StatModifier Add(StatId s, float v) => new StatModifier { Stat = s, Op = ModifierOp.Add, Value = v };
        static StatModifier Mul(StatId s, float v) => new StatModifier { Stat = s, Op = ModifierOp.Mul, Value = v };

        static SkillEffect Burn(float dps) => new SkillEffect
        {
            Type = EffectType.DoT, DamageType = DamageType.Magical, Magnitude = dps,
            Duration = 3f, TickInterval = 0.5f, BossMultiplier = 1f,
        };

        static SkillEffect Knockback(float meters) => new SkillEffect
        {
            Type = EffectType.Knockback, DamageType = DamageType.Physical, Magnitude = meters, BossMultiplier = 0.25f,
        };

        // ---------------- Layout ----------------
        static LevelLayoutSO BuildLayout(LayoutDef def)
        {
            var l = GetOrCreate<LevelLayoutSO>($"{E1}/Layouts/{def.File}.asset");
            l.Id = def.Id;
            l.DisplayName = def.Name;
            l.PathPolyline = (Vector3[])def.Path.Clone();
            l.PathWidth = PathWidth;
            l.Pedestals = (Vector3[])def.Spots.Clone();
            l.LairPosition = def.Path[0];                       // fora da tela (entrada)
            l.CorePosition = def.Path[def.Path.Length - 1];     // fora da tela (saída)
            l.CameraBounds = def.Frame;
            l.ShowLairAndCore = false;                          // sem bases (Bíblia §3.6)
            l.Ground = GroundStyle.Auto;                        // bioma da Era (Era 1: Tundra)
            EditorUtility.SetDirty(l);
            return l;
        }

        // ---------------- Ondas ----------------
        static WaveDataSO[] BuildLevel10Waves(Dictionary<E, EnemyDataSO> enemies, BossDataSO boss)
        {
            var waves = new WaveDataSO[Level10Waves.Length];
            for (int w = 0; w < waves.Length; w++)
            {
                var src = Level10Waves[w];
                var groups = new SpawnGroup[src.Length];
                for (int g = 0; g < src.Length; g++)
                {
                    EnemyDataSO e = src[g].type == E.Mammoth ? boss : enemies[src[g].type];
                    groups[g] = Group(e, src[g].count, g * 3f);   // PROV: grupos defasados em 3 s
                }
                waves[w] = Wave(10, w + 1, groups);
            }
            return waves;
        }

        /// <summary>
        /// PROV — modelo simples para as fases 1–9 seguindo o ritmo da Bíblia §2.2:
        /// 1–3 lobos · 4–6 + tigres · 7–8 + rinocerontes (pico) · 9 respiro.
        /// O número de ondas fica em 12–16 (GDD §1.1: 12–25). Calibrar no simulador (§14.3).
        /// </summary>
        static WaveDataSO[] BuildCommonWaves(int L, Dictionary<E, EnemyDataSO> enemies)
        {
            int p = BalanceFormulas.ChapterPosition(L);
            int waveCount = p <= 3 ? 12 : p <= 6 ? 14 : p <= 8 ? 16 : 12;
            var waves = new WaveDataSO[waveCount];
            var groups = new List<SpawnGroup>(3);
            for (int w = 1; w <= waveCount; w++)
            {
                groups.Clear();
                int wolves, tigers = 0, rhinos = 0;
                if (p == 9)
                {
                    wolves = 4 + w;
                    if (w >= 4) tigers = w / 4;
                }
                else
                {
                    wolves = 5 + w + (p - 1);
                    if (p >= 4 && w >= 3) tigers = (w - 1) / 3 + (p - 4);
                    if (p >= 7 && w >= 5) rhinos = (w - 3) / 4;
                }
                float delay = 0f;
                if (rhinos > 0) { groups.Add(Group(enemies[E.Rhino], rhinos, delay)); delay += 3f; }
                if (tigers > 0) { groups.Add(Group(enemies[E.Tiger], tigers, delay)); delay += 3f; }
                groups.Add(Group(enemies[E.Wolf], wolves, delay));
                waves[w - 1] = Wave(L, w, groups.ToArray());
            }
            return waves;
        }

        static SpawnGroup Group(EnemyDataSO e, int count, float startDelay) => new SpawnGroup
        {
            Enemy = e,
            Count = count,
            StartDelay = startDelay,
            // PROV: espaçamento por classe de massa (pesados mais espaçados).
            Interval = e.HasTrait(EnemyTraits.Boss) ? 0f : e.Mass >= 80f ? 2.5f : e.Mass >= 30f ? 1.4f : 0.8f,
            SpawnPointIndex = 0,
        };

        static WaveDataSO Wave(int L, int w, SpawnGroup[] groups)
        {
            var wave = GetOrCreate<WaveDataSO>($"{E1}/Waves/L{L:000}/WAV_{L:000}_{w:00}.asset");
            wave.Groups = groups;
            wave.BonusGoldOverride = -1;          // 50 + 5w
            EditorUtility.SetDirty(wave);
            return wave;
        }

        // ---------------- Utilitários ----------------
        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
