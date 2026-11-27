// Arte provisória gerada no carregamento (texturas, sprites, malhas e materiais URP).
// Enquanto os assets hand-painted da Bíblia §3/§7 não existem, tudo é desenhado proceduralmente
// com a paleta oficial (Bíblia §3.4, §3.7, §4.1). Nada de primitivas cinzas.
// Shaders: Sprite-Lit-Default (cenário, pedestais, runas, torres) e URP/Unlit (creeps instanciados).
using System;
using System.Collections.Generic;
using ChronoDefense.Data;
using Unity.Mathematics;
using UnityEngine;

namespace ChronoDefense.UI
{
    public sealed class ArtLibrary : IDisposable
    {
        public const string SpriteLitShader = "Universal Render Pipeline/2D/Sprite-Lit-Default";
        public const string SpriteUnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        public const string UnlitShader = "Universal Render Pipeline/Unlit";

        // Paleta (Bíblia)
        public static readonly Color Snow = Hex("#E8F1F5"), Ice = Hex("#9EC9DB"), Stone = Hex("#6B625A");
        public static readonly Color Mud = Hex("#6B6259"), Fire = Hex("#F26B1D"), Bone = Hex("#EDE3CF");
        public static readonly Color Corruption = Hex("#3B1E54"), Magenta = Hex("#E0369B");
        public static readonly Color PedestalFree = Hex("#FFE08A"), UpgradeGold = Hex("#FFC21A"), LockedGrey = Hex("#6E6E6E");
        public static readonly Color BossRed = Hex("#FF3B30"), NightSky = Hex("#1C2A4A"), Aurora = Hex("#5FE3C0");
        // Prado da tundra (Referencia_BTD6.md, Fase C): grama viva com neve derretendo, lajotas claras com contorno.
        public static readonly Color GrassA = Hex("#77BE43"), GrassB = Hex("#92D152"), GrassDark = Hex("#4E9A30");
        public static readonly Color StoneA = Hex("#CFCAC0"), StoneB = Hex("#B2ACA1"), PathEdge = Hex("#4B463F");

        public Material SpriteLit, SpriteUnlit;
        public Material GroundMaterial, PathMaterial, BackdropMaterial, FlatMaterial;
        public Sprite Shadow, SnowPatch, Flowers, GrassTuft, Rock, Pine;
        /// <summary>Árvores da moldura do bioma (pinheiro, árvore copada, palmeira…); o LevelRenderer sorteia entre elas.</summary>
        public Sprite[] BorderTrees;
        /// <summary>
        /// Vegetação pintada (Resources/Sprites/Environment/&lt;bioma&gt;, gerada por Tools/gen_env_art.py no estilo da
        /// referência do usuário): arbustos e samambaias, pedras com musgo. Vazios = bioma sem arte pintada (usa o procedural).
        /// </summary>
        public Sprite[] Shrubs = Array.Empty<Sprite>(), Boulders = Array.Empty<Sprite>();
        /// <summary>true quando o chão/caminho/árvores vieram da arte pintada.</summary>
        public bool PaintedProps => Shrubs.Length > 0 || Boulders.Length > 0;
        /// <summary>Cor da faixa entre o caminho e o chão (contorno escuro nas lajotas; "beiço" de grama no caminho de terra).</summary>
        public Color PathEdgeColor { get; private set; } = PathEdge;
        /// <summary>Ponto de contato com o chão nas imagens de props pintados (x, y normalizados).</summary>
        public static readonly Vector2 PropPivot = new Vector2(0.5f, 0.1f);
        public Sprite PlayIcon, FastIcon, BoltIcon;   // HUD estilo BTD6
        public Material SoldierMaterial, BarBackMaterial, BarFillMaterial;   // soldados do Guardião (instanciados)
        public Material FireGroundMaterial, FlameMaterial;   // habilidade de fogo: chão queimando e chamas
        /// <summary>Metros cobertos por uma repetição da textura do chão.</summary>
        public const float GroundTileMeters = 10f;
        public Sprite Pedestal, RuneRing, RuneGlyph, RangeRing, Lair, Core, Warning;
        public Sprite White, Rounded, Circle, Coin, Heart, Portrait;
        public Sprite[] TowerIcons;        // índice = TowerArchetype
        public Sprite HeroIcon;
        public Mesh QuadXZ, QuadUpright, QuadCenter;
        public Material[] CreepMaterials;   // índice = EnemyDataSO.PoolId
        public float[] CreepScale;

        // Escala visual das unidades (Referencia_BTD6.md §2). No BTD6 uma torre ocupa ~7% da largura do mapa;
        // aqui, com 1,8 m num mapa de ~53 m, ocupava ~2%. Só o visual cresce: alcances, velocidades e
        // colisões da simulação continuam em metros, então o balanceamento não muda.
        // 26/11: 2,5 → 3,6 para ver o personagem sem zoom (Krog ≈ 6% da largura do mapa, como no BTD6).
        public const float UnitScale = 3.6f;
        /// <summary>Escala visual dos inimigos: um lobo fica da largura do caminho (balão do BTD6 ≈ caminho).</summary>
        public const float CreepVisualScale = 2.5f;
        /// <summary>Pedestal Ø 1,6 → 2,8 m para caber a unidade ampliada (MvpDataGenerator.PedestalRadius).</summary>
        public const float PedestalScale = 1.75f;
        public Mesh[] CreepMeshes;          // QuadUpright (sprite) ou EnemyDataSO.VatMesh (modelo 3D)
        public bool[] CreepIs3D;            // 3D: gira na direção do movimento em vez de billboard
        public Material[] ProjectileMaterials; // índice = TowerArchetype (+ herói no 4)

        // ---- Projéteis e "item na mão" (índice = TowerArchetype; 4 = herói Krog). Medidas em metros de jogo.
        // Caçador: lança · Catapulta: pedra · Xamã: orbe · Guardião: não arremessa (soldados) · Krog: lança em chamas.
        public static readonly bool[] ProjectileOriented = { true, false, false, false, true };   // lança aponta para o voo
        public static readonly float[] ProjectileLength = { 2.4f, 1.2f, 1.0f, 0.6f, 2.6f };
        public static readonly float[] ProjectileThickness = { 0.6f, 1.2f, 1.0f, 0.6f, 0.65f };
        public static readonly bool[] HoldsProjectile = { true, true, true, false, true };
        /// <summary>
        /// Onde o item fica antes do arremesso, no espaço da unidade (x = direita, y = altura, z = frente), em metros
        /// já na escala do jogo (UnitScale). Ajuste fino aqui quando os modelos 3D finais chegarem.
        /// </summary>
        public static readonly Vector3[] HandLocal =
        {
            new Vector3(0.75f, 2.7f, 0.2f),    // Caçador: braço erguido com a lança
            new Vector3(0f,    2.5f, -0.5f),   // Catapulta: pedra na concha do braço
            new Vector3(0.7f,  3.3f, 0.3f),    // Xamã: orbe na ponta do cajado
            new Vector3(0.5f,  2.0f, 0.3f),    // Guardião (sem uso)
            new Vector3(0.8f,  2.5f, 0.2f),    // Krog: lança em chamas na mão direita
        };

        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        public GroundStyle Ground { get; private set; }
        /// <summary>Densidade relativa de flores no chão.</summary>
        public float FlowerDensity => Ground switch
        {
            GroundStyle.Tundra => 0.25f, GroundStyle.Desert => 0.08f, GroundStyle.Savanna => 0.4f,
            GroundStyle.Highland => 0.5f, _ => 1f,
        };
        /// <summary>Densidade relativa das árvores da moldura (a mata é fechada; o deserto, ralo).</summary>
        public float TreeDensity => Ground switch
        {
            GroundStyle.Forest => 1.5f, GroundStyle.Desert => 0.55f, GroundStyle.Savanna => 0.8f, _ => 1f,
        };
        /// <summary>Densidade relativa de pedras soltas no chão.</summary>
        public float RockDensity => Ground switch
        {
            GroundStyle.Highland => 2.2f, GroundStyle.Desert => 1.3f, GroundStyle.Forest => 1.2f, _ => 1f,
        };
        /// <summary>false = mantém a luz do tema da Era (Tundra e o Prado aprovado); true = usa LightColor/FogColor.</summary>
        public bool OverridesLighting => Ground >= GroundStyle.Forest;
        public Color LightColor { get; private set; } = Color.white;
        public Color FogColor { get; private set; } = Color.white;
        public Color AmbientSky { get; private set; } = Color.white;
        public Color AmbientGround { get; private set; } = Color.gray;

        /// <param name="ground">Bioma já resolvido (LevelDataSO.ResolvedBiome).</param>
        public void Build(EnemyDataSO[] enemies, Color accent, GroundStyle ground = GroundStyle.Meadow)
        {
            Ground = ground == GroundStyle.Auto ? GroundStyle.Tundra : ground;
            bool tundra = Ground == GroundStyle.Tundra;
            Biome look = BiomeLook(Ground);
            LightColor = look.Light; FogColor = look.Fog; AmbientSky = look.Sky; AmbientGround = look.AmbientGround;
            SpriteLit = Own(new Material(FindShader(SpriteLitShader)) { name = "MAT_SpriteLit" });
            SpriteUnlit = Own(new Material(FindShader(SpriteUnlitShader)) { name = "MAT_SpriteUnlit" });

            QuadXZ = Own(BuildQuad(true));
            QuadUpright = Own(BuildQuad(false));
            QuadCenter = Own(BuildCenteredQuad());

            GroundMaterial = Own(new Material(SpriteLit) { name = "MAT_Ground", mainTexture = Own(
                tundra ? TundraTexture() : Ground == GroundStyle.Meadow ? GrassTexture() : BiomeTexture(look)) });
            PathMaterial = Own(new Material(SpriteLit) { name = "MAT_Path", mainTexture = Own(StoneTexture(look.StoneA, look.StoneB, look.Grout)) });
            BackdropMaterial = Own(new Material(SpriteUnlit) { name = "MAT_Backdrop", mainTexture = Own(BackdropTexture()) });

            White = MakeSprite("White", 8, 8, p => Color.white, 8);
            FlatMaterial = Own(new Material(SpriteLit) { name = "MAT_Flat", mainTexture = White.texture });
            Shadow = MakeSprite("Shadow", 128, 128, ShadowPixel, 128 / 2.2f);
            SnowPatch = tundra ? MakeSprite("IcePatch", 256, 256, IcePatchPixel, 256 / 4f)
                      : Ground == GroundStyle.Meadow ? MakeSprite("SnowPatch", 256, 256, SnowPatchPixel, 256 / 4f)
                      : MakeSprite("Patch", 256, 256, p => PatchPixel(p, look.PatchIn, look.PatchOut), 256 / 4f);
            Flowers = MakeSprite("Flowers", 128, 128, FlowersPixel, 128 / 1.2f);
            GrassTuft = MakeSprite("GrassTuft", 64, 64, p => TuftPixel(p, look.TuftBottom, look.TuftTop), 64 / 0.8f);
            Rock = MakeSprite("Rock", 128, 128, p => RockPixel(p, look.RockDark, look.RockLight, tundra || Ground == GroundStyle.Meadow), 128 / 1.2f);
            Pine = MakeSprite("Pine", 128, 192, p => PinePixel(p, tundra || Ground == GroundStyle.Meadow), 128 / 3f, new Vector2(0.5f, 0.02f));
            Sprite Leafy() => MakeSprite("Tree", 128, 192, p => TreePixel(p, look.LeafDark, look.LeafLight), 128 / 3.4f, new Vector2(0.5f, 0.02f));
            Sprite Palm() => MakeSprite("Palm", 128, 192, PalmPixel, 128 / 3.2f, new Vector2(0.5f, 0.02f));
            Sprite tree;
            switch (Ground)
            {
                case GroundStyle.Forest: tree = Leafy(); BorderTrees = new[] { tree, tree, Pine }; break;   // mata: copadas + alguns pinheiros
                case GroundStyle.Savanna: BorderTrees = new[] { Palm(), Leafy() }; break;
                case GroundStyle.Desert: BorderTrees = new[] { Palm() }; break;
                case GroundStyle.Highland: tree = Leafy(); BorderTrees = new[] { Pine, Pine, tree }; break;
                default: BorderTrees = new[] { Pine }; break;
            }
            PlayIcon = MakeSprite("PlayIcon", 128, 128, p => Triangle(p, 0.5f), 128);
            FastIcon = MakeSprite("FastIcon", 128, 128, p => Over(Triangle(p + new float2(0.17f, 0f), 0.36f), Triangle(p - new float2(0.17f, 0f), 0.36f)), 128);
            BoltIcon = MakeSprite("BoltIcon", 128, 128, BoltPixel, 128);
            Rounded = MakeSliced("Rounded", 64, 18f);
            Circle = MakeSprite("Circle", 64, 64, p => Disc(p, 0.5f, Color.white), 64);
            Pedestal = MakeSprite("Pedestal", 128, 128, PedestalPixel, 128 / 1.6f);
            RuneRing = MakeSprite("RuneRing", 128, 128, p => Ring(p, 0.47f, 0.035f, Color.white), 128 / 1.75f);
            RuneGlyph = MakeSprite("RuneGlyph", 64, 64, GlyphPixel, 64 / 0.8f);
            RangeRing = MakeSprite("RangeRing", 256, 256, RangePixel, 256f);
            Warning = MakeSprite("Warning", 128, 128, p => Ring(p, 0.44f, 0.06f, Fire) + Disc(p, 0.4f, new Color(Fire.r, Fire.g, Fire.b, 0.25f)), 128 / 2f);
            Lair = MakeSprite("Lair", 256, 256, LairPixel, 256 / 5f, new Vector2(0.5f, 0.05f));
            Core = MakeSprite("Core", 128, 192, CorePixel, 128 / 1.6f, new Vector2(0.5f, 0.05f));
            Coin = MakeSprite("Coin", 64, 64, p => Outlined(p, 0.42f, Hex("#F2C14E")), 64);
            Heart = MakeSprite("Heart", 64, 64, HeartPixel, 64);

            TowerIcons = new Sprite[4];
            TowerIcons[(int)TowerArchetype.Marksman] = MakeSprite("TWR_Marksman", 128, 192, p => TowerPixel(p, 0), 128 / 1.3f, new Vector2(0.5f, 0.06f));
            TowerIcons[(int)TowerArchetype.Launcher] = MakeSprite("TWR_Launcher", 128, 192, p => TowerPixel(p, 1), 128 / 1.4f, new Vector2(0.5f, 0.06f));
            TowerIcons[(int)TowerArchetype.Alchemist] = MakeSprite("TWR_Alchemist", 128, 192, p => TowerPixel(p, 2), 128 / 1.3f, new Vector2(0.5f, 0.06f));
            TowerIcons[(int)TowerArchetype.Guard] = MakeSprite("TWR_Guard", 128, 192, p => TowerPixel(p, 3), 128 / 1.4f, new Vector2(0.5f, 0.06f));
            HeroIcon = MakeSprite("HER_01_Krog", 128, 192, KrogPixel, 128 / 1.1f, new Vector2(0.5f, 0.04f));
            Portrait = MakeSprite("HER_01_Krog_Portrait", 128, 128, KrogPortraitPixel, 128);

            // Creeps: um material instanciado por tipo (1 draw call por tipo, GDD §3.5)
            CreepMaterials = new Material[enemies.Length];
            CreepScale = new float[enemies.Length];
            CreepMeshes = new Mesh[enemies.Length];
            CreepIs3D = new bool[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyDataSO e = enemies[i];
                if (e.VatMesh != null && e.VatMaterial != null)
                {
                    // Modelo 3D (Tools ▸ Chrono ▸ Importar modelos 3D): já está em metros, pivô na base.
                    // Assets do projeto: NÃO passam por Own() (Dispose não pode destruí-los).
                    CreepMeshes[i] = e.VatMesh;
                    CreepMaterials[i] = e.VatMaterial;
                    CreepScale[i] = 1f;
                    CreepIs3D[i] = true;
                    continue;
                }
                CreepMeshes[i] = QuadUpright;
                bool boss = e is BossDataSO;
                Color body = CreepColor(e);
                var tex = Own(DrawTexture(128, 128, p => CreepPixel(p, body, boss)));
                CreepMaterials[i] = Own(InstancedUnlit(tex, "MAT_" + e.name));
                CreepScale[i] = boss ? 2.6f : math.lerp(0.95f, 1.35f, math.saturate((e.Mass - 10f) / 70f));
            }

            // Projéteis (26/11): o que cada campeão realmente arremessa. Lanças são desenhadas na horizontal
            // (ponta em +X) e o UnitRenderer as alinha com a direção do voo; pedras e orbes giram de frente.
            ProjectileMaterials = new Material[5];
            var spear = Own(DrawTexture(256, 64, p => SpearPixel(p, false)));
            var boulder = Own(DrawTexture(64, 64, BoulderPixel));
            var orb = Own(DrawTexture(64, 64, OrbPixel));
            var fireSpear = Own(DrawTexture(256, 64, p => SpearPixel(p, true)));
            ProjectileMaterials[0] = Own(InstancedUnlit(spear, "MAT_Proj_Spear"));          // Caçador
            ProjectileMaterials[1] = Own(InstancedUnlit(boulder, "MAT_Proj_Boulder"));      // Catapulta
            ProjectileMaterials[2] = Own(InstancedUnlit(orb, "MAT_Proj_Orb"));              // Xamã
            ProjectileMaterials[3] = ProjectileMaterials[1];                                // Guardião: pedra menor
            ProjectileMaterials[4] = Own(InstancedUnlit(fireSpear, "MAT_Proj_FireSpear"));  // Krog

            SoldierMaterial = Own(InstancedUnlit(Own(DrawTexture(128, 192, SoldierPixel)), "MAT_Soldier"));
            SoldierMaterial.SetFloat("_Cull", 0f);   // espelhado (escala X negativa) quando olha para a esquerda
            FireGroundMaterial = Own(InstancedUnlit(Own(DrawTexture(256, 256, FireGroundPixel)), "MAT_FireGround"));
            FireGroundMaterial.SetFloat("_Cull", 0f);
            FlameMaterial = Own(InstancedUnlit(Own(DrawTexture(64, 96, FlamePixel)), "MAT_Flame"));
            FlameMaterial.SetFloat("_Cull", 0f);
            BarBackMaterial = Own(InstancedUnlit(Own(DrawTexture(8, 8, p => Hex("#1E1A16"))), "MAT_BarBack"));
            BarFillMaterial = Own(InstancedUnlit(Own(DrawTexture(8, 8, p => Hex("#5BD43A"))), "MAT_BarFill"));

            LoadPaintedEnvironment(look);
        }

        /// <summary>
        /// Substitui chão, caminho e props pelo que houver em Resources/Sprites/Environment/&lt;bioma&gt;:
        /// ENV_Ground (tile 512 sem emenda), ENV_Path (terra; x = ao longo, y = atravessa, bordas transparentes onduladas),
        /// ENV_Tree_*/ENV_Palm_*/ENV_Pine_* (moldura), ENV_Bush_*/ENV_Fern_* (arbustos), ENV_Rock_* (pedras).
        /// As texturas são assets do projeto: não passam por Own() (Dispose não pode destruí-las); só os Sprites criados aqui.
        /// </summary>
        void LoadPaintedEnvironment(Biome look)
        {
            if (Ground == GroundStyle.Tundra || Ground == GroundStyle.Meadow) return;   // aprovados como estão
            Texture2D[] all = Resources.LoadAll<Texture2D>("Sprites/Environment/" + Ground);
            if (all == null || all.Length == 0) return;
            var trees = new List<Sprite>();
            var shrubs = new List<Sprite>();
            var rocks = new List<Sprite>();
            for (int i = 0; i < all.Length; i++)
            {
                Texture2D t = all[i];
                string n = t.name;
                if (n == "ENV_Ground") { GroundMaterial.mainTexture = t; continue; }
                if (n == "ENV_Path") { PathMaterial.mainTexture = t; PathEdgeColor = look.PathLip; continue; }
                float h = n.StartsWith("ENV_Tree_") ? 5.6f : n.StartsWith("ENV_Palm_") ? 5f : n.StartsWith("ENV_Pine_") ? 5.2f
                        : n.StartsWith("ENV_Bush_") ? 1.9f : n.StartsWith("ENV_Fern_") ? 1.7f : n.StartsWith("ENV_Rock_") ? 1.5f : 0f;
                if (h <= 0f) continue;
                Sprite s = Own(Sprite.Create(t, new Rect(0, 0, t.width, t.height), PropPivot, t.height / h));
                s.name = n;
                if (n.StartsWith("ENV_Rock_")) rocks.Add(s);
                else if (n.StartsWith("ENV_Bush_") || n.StartsWith("ENV_Fern_")) shrubs.Add(s);
                else trees.Add(s);
            }
            if (trees.Count > 0) BorderTrees = trees.ToArray();
            Shrubs = shrubs.ToArray();
            Boulders = rocks.ToArray();
        }

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i] != null) UnityEngine.Object.Destroy(_owned[i]);
            _owned.Clear();
        }

        // ------------------------------------------------------------------ fábrica
        T Own<T>(T obj) where T : UnityEngine.Object { _owned.Add(obj); return obj; }

        static Shader FindShader(string name)
        {
            Shader s = Shader.Find(name);
            if (s == null) throw new InvalidOperationException($"Shader '{name}' não encontrado. O URP está ativo (Tools ▸ Chrono ▸ Configurar projeto)?");
            return s;
        }

        Material InstancedUnlit(Texture2D tex, string name)
        {
            var m = new Material(FindShader(UnlitShader)) { name = name, enableInstancing = true };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            return m;
        }

        Sprite MakeSprite(string name, int w, int h, Func<float2, Color> px, float ppu, Vector2? pivot = null)
        {
            var tex = Own(DrawTexture(w, h, px));
            tex.name = "TEX_" + name;
            var s = Own(Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), ppu));
            s.name = name;
            return s;
        }

        Sprite MakeSliced(string name, int size, float radius)
        {
            var tex = Own(DrawTexture(size, size, p =>
            {
                float2 q = math.abs(p - 0.5f) * size - (size * 0.5f - radius);
                float d = math.length(math.max(q, 0f)) - radius;
                return new Color(1, 1, 1, math.saturate(0.5f - d));
            }, true));
            var border = new Vector4(radius, radius, radius, radius);
            var s = Own(Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border));
            s.name = name;
            return s;
        }

        /// <summary>p em [0,1]² (y para cima). Função chamada só no carregamento.</summary>
        static Texture2D DrawTexture(int w, int h, Func<float2, Color> px, bool clamp = true)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var data = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                data[y * w + x] = px(new float2((x + 0.5f) / w, (y + 0.5f) / h));
            tex.SetPixels32(data);
            tex.Apply(true, true);
            return tex;
        }

        /// <summary>Quad 1 × 1 centrado na origem, no plano XY (projéteis).</summary>
        static Mesh BuildCenteredQuad()
        {
            var m = new Mesh { name = "MSH_QuadCenter" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildQuad(bool flatXZ)
        {
            var m = new Mesh { name = flatXZ ? "MSH_QuadXZ" : "MSH_QuadUpright" };
            if (flatXZ)
                m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f) };
            else   // pivô na base (pés no chão)
                m.vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(-0.5f, 1, 0), new Vector3(0.5f, 1, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ "pintura"
        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        static Color Ink(Color c) => new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, 1f);   // albedo −70% (Bíblia §3.1)
        static float Aa(float d, float px = 0.012f) => math.saturate(0.5f - d / px);
        static float Hash(float2 p) => math.frac(math.sin(math.dot(p, new float2(127.1f, 311.7f))) * 43758.5453f);

        static Color Disc(float2 p, float r, Color c)
        {
            float a = Aa(math.distance(p, 0.5f) - r);
            return new Color(c.r, c.g, c.b, c.a * a);
        }

        static Color Ring(float2 p, float r, float w, Color c)
        {
            float a = Aa(math.abs(math.distance(p, 0.5f) - r) - w);
            return new Color(c.r, c.g, c.b, c.a * a);
        }

        static Color Outlined(float2 p, float r, Color fill)
        {
            float d = math.distance(p, 0.5f);
            if (d > r + 0.02f) return Color.clear;
            Color c = d > r - 0.05f ? Ink(fill) : Color.Lerp(fill, Color.white, math.saturate(0.25f - (p.y - 0.5f)) * 0.4f);
            c.a = Aa(d - r - 0.01f);
            return c;
        }

        static Color Over(Color under, Color over) => Color.Lerp(under, new Color(over.r, over.g, over.b, 1f), over.a) * new Color(1, 1, 1, math.max(under.a, over.a));

        static Color SnowTexture_(float2 p)
        {
            float n = Hash(math.floor(p * 64f)) * 0.5f + Hash(math.floor(p * 16f)) * 0.5f;
            return Color.Lerp(Snow, Hex("#D3E2EA"), n * 0.45f);
        }

        static Texture2D SnowTexture()
        {
            var t = DrawTexture(256, 256, SnowTexture_, false);
            t.name = "TEX_E1_Snow";
            return t;
        }

        // ------------------------------------------------------------------ prado (Fase C)
        /// <summary>Ruído de valor que se repete a cada <paramref name="period"/> células (textura sem emenda).</summary>
        static float VNoise(float2 p, int period)
        {
            float2 i0 = math.floor(p), f = p - i0;
            float2 u = f * f * (3f - 2f * f);
            float H(float2 c) => Hash(new float2((c.x % period + period) % period, (c.y % period + period) % period));
            float a = H(i0), b = H(i0 + new float2(1, 0)), c = H(i0 + new float2(0, 1)), d = H(i0 + new float2(1, 1));
            return math.lerp(math.lerp(a, b, u.x), math.lerp(c, d, u.x), u.y);
        }

        static Texture2D GrassTexture()
        {
            var t = DrawTexture(256, 256, p =>
            {
                float big = VNoise(p * 4f, 4), mid = VNoise(p * 16f, 16);
                Color c = Color.Lerp(GrassA, GrassB, big);
                c = Color.Lerp(c, GrassDark, math.saturate(mid - 0.55f) * 0.6f);
                // traços de folha: pequenos riscos escuros e pontos claros (pincel)
                float2 cell = math.floor(p * 48f), local = math.frac(p * 48f);
                float h = Hash(cell);
                if (h > 0.86f && math.abs(local.x - 0.5f + (local.y - 0.5f) * 0.3f) < 0.11f && local.y > 0.15f && local.y < 0.85f)
                    c = Color.Lerp(c, GrassDark, 0.55f);
                else if (h < 0.04f && math.distance(local, 0.5f) < 0.22f)
                    c = Color.Lerp(c, Hex("#C8EB7A"), 0.7f);
                c.a = 1f;
                return c;
            }, false);
            t.name = "TEX_E1_Grass";
            return t;
        }

        /// <summary>Lajotas de pedra (u ao longo do caminho, 1 repetição por largura; v atravessa o caminho).</summary>
        static Texture2D StoneTexture(Color stoneA, Color stoneB, Color grout)
        {
            var t = DrawTexture(256, 256, p =>
            {
                float row = math.floor(p.y * 2f), lv = math.frac(p.y * 2f);
                float uu = p.x * 2f + row * 0.5f;
                float col = math.floor(uu) % 2f, lu = math.frac(uu);
                float h = Hash(new float2(col + 3f, row + 11f));
                float gap = 0.05f + 0.03f * h, rad = 0.14f;
                float2 q = math.abs(new float2(lu - 0.5f, lv - 0.5f) + new float2(h - 0.5f, Hash(new float2(row, col)) - 0.5f) * 0.04f)
                           - (0.5f - gap - rad);
                float d = math.length(math.max(q, 0f)) + math.min(math.max(q.x, q.y), 0f) - rad;
                if (d > 0f) return grout;                                 // rejunte
                if (d > -0.045f) return PathEdge;                         // contorno da lajota
                Color c = Color.Lerp(stoneA, stoneB, h * 0.8f + VNoise(p * 24f, 24) * 0.25f);
                c = Color.Lerp(c, Color.white, math.saturate(lv - 0.62f) * 0.35f);   // luz no topo de cada pedra
                if (Hash(math.floor(p * 96f)) > 0.97f) c *= 0.88f;                   // pintinhas
                c.a = 1f;
                return c;
            }, false);
            t.name = "TEX_E1_Stone";
            return t;
        }

        /// <summary>Triângulo "play" branco com contorno escuro, centrado em (0,5; 0,5).</summary>
        static Color Triangle(float2 p, float size)
        {
            float2 q = (p - 0.5f) / size;
            // lados: x > -0,45 ; y < 0,5 − 0,58·(x + 0,45) ; y > −(0,5 − 0,58·(x + 0,45))
            float lim = 0.5f - 0.58f * (q.x + 0.45f);
            float d = math.max(-0.45f - q.x, math.abs(q.y) - lim);
            if (d > 0.06f) return Color.clear;
            Color c = d > -0.08f ? Hex("#1F3B12") : Color.white;
            c.a = Aa(d - 0.05f, 0.02f);
            return c;
        }

        static Color BoltPixel(float2 p)
        {
            // raio em zigue-zague: dois paralelogramos
            float2 q = p - 0.5f;
            float upper = math.abs(q.x - 0.05f + (q.y - 0.15f) * 0.55f) - 0.11f;
            float lower = math.abs(q.x + 0.05f + (q.y + 0.15f) * 0.55f) - 0.11f;
            bool inU = q.y > -0.05f && q.y < 0.42f, inL = q.y < 0.05f && q.y > -0.42f;
            float d = math.min(inU ? upper : 1f, inL ? lower : 1f);
            if (d > 0.04f) return Color.clear;
            Color c = d > -0.04f ? Hex("#5A3A00") : Hex("#FFE14D");
            c.a = Aa(d - 0.03f, 0.02f);
            return c;
        }

        // ------------------------------------------------------------------ projéteis
        /// <summary>Lança de madeira com ponta de sílex (256 × 64, ponta à direita); fire = lança em chamas do Krog.</summary>
        static Color SpearPixel(float2 p, bool fire)
        {
            float y = (p.y - 0.5f) * 4f;          // espaço quadrado: altura vira 4 × (textura 4:1)
            Color outc = Color.clear;
            // haste
            if (p.x > 0.04f && p.x < 0.74f && math.abs(y) < 0.24f)
            {
                Color c = Color.Lerp(Hex("#A8743F"), Hex("#7A4E2A"), math.saturate(y + 0.3f));
                if (math.abs(y) > 0.15f) c = Ink(Hex("#7A4E2A"));
                float wrap = math.frac(p.x * 38f);
                if ((p.x < 0.16f || (p.x > 0.66f && p.x < 0.74f)) && wrap < 0.5f) c = Hex("#5A3A1F");   // amarras de couro
                outc = c;
            }
            // ponta de sílex (triângulo)
            float tx = (p.x - 0.72f) / 0.28f;    // 0..1 ao longo da ponta
            if (tx >= 0f && tx <= 1f)
            {
                float half = 0.6f * (1f - tx);
                float d = math.abs(y) - half;
                if (d < 0.08f)
                {
                    Color c = d > -0.07f ? Ink(Hex("#8EA3B0")) : Color.Lerp(Hex("#B9C8D2"), Hex("#7F95A3"), math.saturate(y + 0.5f));
                    outc = c;
                }
            }
            // penas na cauda
            if (p.x < 0.12f && math.abs(y) < 0.55f && math.abs(y) > 0.18f && math.frac(p.x * 50f) < 0.6f)
                outc = math.abs(y) > 0.48f ? Ink(Hex("#E9E2D0")) : Hex("#E9E2D0");
            if (fire)
            {
                // chamas em volta da ponta
                float2 q = new float2((p.x - 0.84f) * 4f, y);
                float flame = math.length(q * new float2(0.9f, 1.1f)) - 0.75f - 0.18f * math.sin(math.atan2(q.y, q.x) * 6f);
                if (flame < 0f && outc.a < 0.5f)
                    outc = Color.Lerp(Hex("#FFE14D"), Fire, math.saturate(-flame * 1.6f + 0.2f));
            }
            return outc;
        }

        /// <summary>Soldado do Guardião: homem das cavernas chibi com escudo de couro e porrete (olha para a direita).</summary>
        static Color SoldierPixel(float2 p)
        {
            Color skin = Hex("#C68B59"), fur = Hex("#8B5E3C"), hair = Hex("#4A3222"), wood = Hex("#9C6B3A"), hide = Hex("#D9C29A");
            Color outc = Color.clear;
            void Put(float d, Color c)
            {
                if (d > 0.012f) return;
                Color k = d > -0.02f ? Ink(c) : c;
                k.a = Aa(d - 0.006f, 0.008f);
                outc = Over(outc, k);
            }
            float2 q = new float2(p.x, p.y * 1.5f);   // espaço quadrado (textura 2:3)
            // pernas
            Put(math.max(math.abs(q.x - 0.42f) - 0.06f, math.abs(q.y - 0.12f) - 0.12f), fur * 0.8f);
            Put(math.max(math.abs(q.x - 0.58f) - 0.06f, math.abs(q.y - 0.12f) - 0.12f), fur * 0.8f);
            // porrete (atrás do corpo, na mão direita, erguido)
            float2 c0 = new float2(0.78f, 0.55f), c1 = new float2(0.92f, 1.05f);
            float2 cd = c1 - c0;
            float ct = math.saturate(math.dot(q - c0, cd) / math.dot(cd, cd));
            Put(math.length(q - (c0 + cd * ct)) - (0.035f + 0.06f * ct), wood);
            // corpo (túnica de pele)
            float2 b = math.abs(q - new float2(0.5f, 0.48f)) - new float2(0.2f, 0.2f);
            Put(math.length(math.max(b, 0f)) + math.min(math.max(b.x, b.y), 0f) - 0.06f, fur);
            // cabeça e cabelo
            Put(math.distance(q, new float2(0.52f, 0.95f)) - 0.22f, skin);
            Put(math.max(math.distance(q, new float2(0.5f, 1.0f)) - 0.23f, 0.98f - q.y), hair);
            if (math.distance(q, new float2(0.6f, 0.95f)) < 0.03f) outc = Hex("#1E1A16");   // olho
            // escudo redondo de couro na mão esquerda (na frente)
            float sd = math.distance(q, new float2(0.32f, 0.5f)) - 0.19f;
            Put(sd, wood);
            if (sd < -0.045f) outc = Over(outc, hide);
            if (math.distance(q, new float2(0.32f, 0.5f)) < 0.045f) outc = Over(outc, Hex("#6E6E6E"));
            return outc;
        }

        /// <summary>Chão em brasa: mancha queimada irregular com rachaduras laranja e brasas.</summary>
        static Color FireGroundPixel(float2 p)
        {
            float2 q = p - 0.5f;
            float ang = math.atan2(q.y, q.x);
            float edge = 0.46f + 0.03f * math.sin(ang * 7f) + 0.02f * math.sin(ang * 13f + 1f);
            float d = math.length(q);
            if (d > edge) return Color.clear;
            float n = VNoise(p * 10f, 10);
            Color c = Color.Lerp(Hex("#2A1A12"), Hex("#4A2614"), n);                      // terra queimada
            float crack = math.abs(VNoise(p * 6f + 3.1f, 6) - 0.5f);
            if (crack < 0.06f) c = Color.Lerp(Hex("#FFB13B"), Hex("#FF5A1F"), crack / 0.06f);   // rachaduras em brasa
            if (Hash(math.floor(p * 40f)) > 0.965f) c = Hex("#FFE14D");                     // brasas
            if (d > edge - 0.05f) c = Color.Lerp(c, Hex("#FF7A1F"), 0.6f);                 // borda acesa
            c.a = Aa(d - edge, 0.01f);
            return c;
        }

        /// <summary>Chama desenhada (pivô na base): núcleo amarelo, laranja e contorno vermelho.</summary>
        static Color FlamePixel(float2 p)
        {
            float2 q = new float2((p.x - 0.5f) * 1.5f, p.y);
            // gota invertida: larga embaixo, ponta em cima
            float w = 0.42f * math.sqrt(math.saturate(1f - q.y)) * math.saturate(q.y * 4f + 0.2f);
            float d = math.abs(q.x + 0.06f * math.sin(q.y * 9f)) - w;
            if (d > 0.02f || q.y > 0.98f) return Color.clear;
            float inner = math.abs(q.x) - w * 0.55f;
            Color c = d > -0.03f ? Hex("#C2261A") : inner < 0f && q.y < 0.7f ? Hex("#FFE14D") : Hex("#FF7A1F");
            c.a = Aa(d, 0.015f);
            return c;
        }

        static Color BoulderPixel(float2 p)
        {
            float2 q = p - 0.5f;
            float ang = math.atan2(q.y, q.x);
            float r = 0.4f + 0.04f * math.sin(ang * 5f + 0.7f) + 0.025f * math.sin(ang * 11f);
            float d = math.length(q) - r;
            if (d > 0.01f) return Color.clear;
            Color c = Color.Lerp(Hex("#6F6A63"), Hex("#A49C91"), math.saturate(q.y * 1.6f + 0.55f - q.x * 0.4f));
            if (math.abs(q.x * 0.8f - q.y + 0.05f) < 0.025f && math.length(q) < 0.25f) c = Ink(Hex("#6F6A63"));   // rachadura
            if (d > -0.06f) c = Ink(Hex("#6F6A63"));
            c.a = Aa(d, 0.02f);
            return c;
        }

        static Color OrbPixel(float2 p)
        {
            float d = math.distance(p, 0.5f);
            if (d > 0.46f) return Color.clear;
            Color c = d < 0.14f ? Color.white : d < 0.3f ? Hex("#7FE8FF") : d < 0.4f ? Hex("#3FA9E8") : Hex("#5B3BB0");
            float spark = math.abs(math.sin(math.atan2(p.y - 0.5f, p.x - 0.5f) * 4f));
            if (d > 0.3f && d < 0.4f && spark > 0.92f) c = Color.white;
            c.a = Aa(d - 0.44f, 0.02f);
            return c;
        }

        static Color ShadowPixel(float2 p)
        {
            float d = math.distance(p, 0.5f) * 2f;
            float a = math.saturate(1f - d);
            return new Color(1, 1, 1, a * a * (3f - 2f * a));
        }

        static Color SnowPatchPixel(float2 p)
        {
            float2 q = p - 0.5f;
            float d = math.length(q) * 2f + (VNoise(p * 6f, 6) - 0.5f) * 0.55f;
            if (d > 0.92f) return Color.clear;
            Color c = Color.Lerp(Hex("#F6FBFF"), Hex("#CFE3F2"), math.saturate((d - 0.55f) * 3f));
            c.a = Aa(d - 0.9f, 0.03f);
            return c;
        }

        static Color FlowersPixel(float2 p)
        {
            Color outc = Color.clear;
            for (int k = 0; k < 6; k++)
            {
                float2 c = 0.2f + new float2(Hash(new float2(k, 1f)), Hash(new float2(k, 2f))) * 0.6f;
                float2 q = p - c;
                float r = math.length(q), ang = math.atan2(q.y, q.x);
                float petal = 0.07f * (0.65f + 0.35f * math.cos(ang * 5f));
                if (r > petal + 0.01f) continue;
                float pick = Hash(new float2(k, 3f));
                Color col = pick < 0.5f ? Color.white : pick < 0.8f ? Hex("#FFE14D") : Hex("#FF9BC8");
                if (r < 0.022f) col = Hex("#F2A91E");
                else if (r > petal - 0.012f) col = Ink(col) * 1.6f;
                col.a = Aa(r - petal - 0.005f, 0.01f);
                outc = Over(outc, col);
            }
            return outc;
        }

        static Color TuftPixel(float2 p, Color bottom, Color top)
        {
            float a = 0f;
            for (int k = -2; k <= 2; k++)
            {
                float lean = k * 0.16f;
                float2 tip = new float2(0.5f + lean, 0.85f - math.abs(k) * 0.12f);
                float2 b = new float2(0.5f + k * 0.04f, 0.15f);
                float2 ab = tip - b, ap = p - b;
                float t = math.saturate(math.dot(ap, ab) / math.dot(ab, ab));
                float w = 0.06f * (1f - t);
                a = math.max(a, Aa(math.length(ap - ab * t) - w, 0.02f));
            }
            if (a <= 0f) return Color.clear;
            Color c = Color.Lerp(bottom, top, p.y);
            c.a = a;
            return c;
        }

        // ------------------------------------------------------------------ biomas do mapa da saga (27/11)
        /// <summary>Paleta de um bioma. Tons tirados da arte do mapa da saga e da referência de mata do usuário.</summary>
        struct Biome
        {
            public Color GroundA, GroundB, GroundDark, Speck, Blotch;
            public float BlotchAmount;   // 0 = sem manchas; areia/cascalho espalhados no chão
            public bool Ripples;         // dunas (deserto)
            public Color StoneA, StoneB, Grout;
            public Color PatchIn, PatchOut, TuftBottom, TuftTop, RockDark, RockLight, LeafDark, LeafLight;
            public Color Light, Fog, Sky, AmbientGround;
            public Color PathLip;        // beiço de grama do caminho de terra pintado
        }

        static Biome BiomeLook(GroundStyle g)
        {
            // Estrada de lajotas bege do mapa da saga (todas as fases, exceto o Prado e a Tundra, que já foram aprovados assim)
            var b = new Biome
            {
                StoneA = Hex("#DCCBA4"), StoneB = Hex("#C4AF86"), Grout = Hex("#8C7756"),
                TuftBottom = GrassDark, TuftTop = Hex("#3C7D25"),
                RockDark = Hex("#8F969A"), RockLight = Hex("#C3C9CC"),
                LeafDark = Hex("#2E7A34"), LeafLight = Hex("#6CC04A"),
                PatchIn = Hex("#4E9A30"), PatchOut = Hex("#3F7F27"),
                Light = Hex("#FFF4DC"), Fog = Hex("#9CCB7A"), Sky = Hex("#BFD9E8"), AmbientGround = Hex("#5E6E3A"),
                PathLip = Hex("#8ED14B"),
            };
            switch (g)
            {
                case GroundStyle.Tundra:
                    b.StoneA = Hex("#A39C92"); b.StoneB = Hex("#847D74"); b.Grout = Hex("#6A645B");   // escuras: contraste com a neve
                    b.TuftBottom = Hex("#8F8A55"); b.TuftTop = Hex("#6B6A40");
                    b.RockDark = Hex("#9AA3A8"); b.RockLight = Hex("#C7CED2");
                    break;
                case GroundStyle.Meadow:
                    b.StoneA = StoneA; b.StoneB = StoneB; b.Grout = Hex("#857F74");
                    b.RockDark = Hex("#9AA3A8"); b.RockLight = Hex("#C7CED2");
                    break;
                case GroundStyle.Forest:     // mata fechada: verde saturado, musgo, samambaia
                    b.GroundA = Hex("#5DA83A"); b.GroundB = Hex("#7BC247"); b.GroundDark = Hex("#3B7E28"); b.Speck = Hex("#B8E06A");
                    b.Blotch = Hex("#3E8A2C"); b.BlotchAmount = 0.35f;
                    b.PatchIn = Hex("#6FB83F"); b.PatchOut = Hex("#4A8F2E");                          // tapete de musgo
                    b.TuftBottom = Hex("#3B7E28"); b.TuftTop = Hex("#5DBA3C");
                    b.RockDark = Hex("#7F8A80"); b.RockLight = Hex("#B9C2B4");
                    b.Fog = Hex("#6FA857"); b.AmbientGround = Hex("#4A6A32");
                    break;
                case GroundStyle.Savanna:    // quase deserto: capim amarelado com areia aparecendo
                    b.GroundA = Hex("#A8B655"); b.GroundB = Hex("#C3C36C"); b.GroundDark = Hex("#7F8C3B"); b.Speck = Hex("#EBDC9C");
                    b.Blotch = Hex("#DDC387"); b.BlotchAmount = 0.55f;
                    b.PatchIn = Hex("#E3CB92"); b.PatchOut = Hex("#C9AC72");                          // areia
                    b.TuftBottom = Hex("#9C8F45"); b.TuftTop = Hex("#C9B55E");
                    b.RockDark = Hex("#A0907A"); b.RockLight = Hex("#D2C3A8");
                    b.LeafDark = Hex("#4F8A2E"); b.LeafLight = Hex("#93C04E");
                    b.PathLip = Hex("#C6C670");
                    b.Light = Hex("#FFEBC8"); b.Fog = Hex("#D9C98F"); b.Sky = Hex("#E8DCC0"); b.AmbientGround = Hex("#7A6A44");
                    break;
                case GroundStyle.Desert:     // areia do portão: dunas e pedra arenito
                    b.GroundA = Hex("#E2C88C"); b.GroundB = Hex("#D2B272"); b.GroundDark = Hex("#B58F57"); b.Speck = Hex("#F4E4B8");
                    b.Ripples = true;
                    b.PatchIn = Hex("#C9A66A"); b.PatchOut = Hex("#A9854F");                          // areia batida
                    b.TuftBottom = Hex("#8C7A3E"); b.TuftTop = Hex("#B9A35A");
                    b.RockDark = Hex("#B08A5C"); b.RockLight = Hex("#E0C08E");
                    b.LeafDark = Hex("#4F8A2E"); b.LeafLight = Hex("#93C04E");
                    b.Light = Hex("#FFE6B8"); b.Fog = Hex("#E6CF98"); b.Sky = Hex("#F0E2C4"); b.AmbientGround = Hex("#8A6E46");
                    break;
                case GroundStyle.Highland:   // planalto rochoso antes da neve: verde escuro, cascalho, pinheiros
                    b.GroundA = Hex("#5E9646"); b.GroundB = Hex("#6EA552"); b.GroundDark = Hex("#3E6B33"); b.Speck = Hex("#A8C47C");
                    b.Blotch = Hex("#8E918A"); b.BlotchAmount = 0.4f;
                    b.StoneA = Hex("#C9C2B2"); b.StoneB = Hex("#AFA796"); b.Grout = Hex("#7C7466");
                    b.PatchIn = Hex("#A4A69F"); b.PatchOut = Hex("#7F827B");                          // cascalho
                    b.TuftBottom = Hex("#3E6B33"); b.TuftTop = Hex("#5E9646");
                    b.RockDark = Hex("#7E878C"); b.RockLight = Hex("#B5BCC0");
                    b.Light = Hex("#F2F2EA"); b.Fog = Hex("#8FA88A"); b.Sky = Hex("#C8D6E0"); b.AmbientGround = Hex("#4E5A44");
                    break;
            }
            return b;
        }

        /// <summary>Chão pintado de um bioma: dois tons, pinceladas, manchas (areia/cascalho/musgo) ou dunas.</summary>
        static Texture2D BiomeTexture(Biome b)
        {
            var t = DrawTexture(256, 256, p =>
            {
                float big = VNoise(p * 4f, 4), mid = VNoise(p * 16f, 16);
                Color c = Color.Lerp(b.GroundA, b.GroundB, big);
                c = Color.Lerp(c, b.GroundDark, math.saturate(mid - 0.55f) * 0.6f);
                if (b.BlotchAmount > 0f)
                {
                    float m = VNoise(p * 3f + 4.1f, 3) + (VNoise(p * 12f + 1.7f, 12) - 0.5f) * 0.25f;
                    float cut = 1f - b.BlotchAmount * 0.5f;
                    if (m > cut) c = Color.Lerp(c, b.Blotch, math.saturate((m - cut) * 8f) * 0.85f);
                }
                if (b.Ripples)
                {
                    float w = math.sin((p.y * 10f + VNoise(p * 2f, 2) * 2.5f) * 6.2832f);   // ondas de duna
                    c = Color.Lerp(c, w > 0.75f ? b.Speck : b.GroundDark, math.saturate(math.abs(w) - 0.75f) * 1.6f);
                }
                float2 cell = math.floor(p * 48f), local = math.frac(p * 48f);
                float h = Hash(cell);
                if (h > 0.86f && math.abs(local.x - 0.5f + (local.y - 0.5f) * 0.3f) < 0.11f && local.y > 0.15f && local.y < 0.85f)
                    c = Color.Lerp(c, b.GroundDark, 0.5f);
                else if (h < 0.04f && math.distance(local, 0.5f) < 0.22f)
                    c = Color.Lerp(c, b.Speck, 0.7f);
                c.a = 1f;
                return c;
            }, false);
            t.name = "TEX_Biome";
            return t;
        }

        /// <summary>Mancha orgânica com contorno pintado (musgo, areia, cascalho).</summary>
        static Color PatchPixel(float2 p, Color inner, Color outer)
        {
            float2 q = p - 0.5f;
            float d = math.length(q * new float2(1f, 1.2f)) * 2f + (VNoise(p * 6f, 6) - 0.5f) * 0.5f;
            if (d > 0.92f) return Color.clear;
            Color c = Color.Lerp(inner, outer, math.saturate((d - 0.45f) * 2.2f));
            if (Hash(math.floor(p * 40f)) > 0.93f) c = Color.Lerp(c, outer, 0.6f);   // grãos
            c.a = Aa(d - 0.9f, 0.03f) * 0.9f;
            return c;
        }

        /// <summary>Árvore copada (mata): tronco e copa de bolas sobrepostas, luz de cima à esquerda, contorno escuro.</summary>
        static Color TreePixel(float2 p, Color dark, Color light)
        {
            float2 q = new float2(p.x, p.y * 1.5f);          // 128×192: trabalha em unidades da largura
            Color outc = Color.clear;
            // tronco com raízes
            float tw = 0.055f + math.saturate(0.12f - q.y) * 0.6f;
            if (q.y < 0.75f && math.abs(q.x - 0.5f) < tw + 0.02f)
            {
                Color bark = Hex("#7A5030");
                outc = math.abs(q.x - 0.5f) > tw - 0.01f || q.y < 0.02f ? Ink(bark) : Color.Lerp(bark, Hex("#9A6A40"), q.x > 0.5f ? 0f : 0.5f);
            }
            // copa: 6 bolas
            float d = 9f;
            float4 b0 = new float4(0.5f, 1.02f, 0.31f, 0f), b1 = new float4(0.27f, 0.86f, 0.21f, 0f), b2 = new float4(0.73f, 0.86f, 0.21f, 0f);
            float4 b3 = new float4(0.35f, 1.2f, 0.2f, 0f), b4 = new float4(0.64f, 1.2f, 0.21f, 0f), b5 = new float4(0.5f, 1.3f, 0.17f, 0f);
            d = math.min(d, math.distance(q, b0.xy) - b0.z);
            d = math.min(d, math.distance(q, b1.xy) - b1.z);
            d = math.min(d, math.distance(q, b2.xy) - b2.z);
            d = math.min(d, math.distance(q, b3.xy) - b3.z);
            d = math.min(d, math.distance(q, b4.xy) - b4.z);
            d = math.min(d, math.distance(q, b5.xy) - b5.z);
            d += (VNoise(p * 14f, 14) - 0.5f) * 0.03f;
            if (d < 0.012f)
            {
                float lit = math.saturate(0.55f + (q.y - 1.0f) * 1.2f - (q.x - 0.5f) * 0.9f);
                Color c = Color.Lerp(dark, light, lit);
                if (Hash(math.floor(q * 22f)) > 0.82f) c = Color.Lerp(c, lit > 0.5f ? Hex("#B8E880") : dark, 0.45f);   // folhas
                if (d > -0.03f) c = Ink(dark) * 1.4f;
                c.a = Aa(d, 0.012f);
                outc = Over(outc, c);
            }
            return outc;
        }

        /// <summary>Palmeira (savana/deserto): tronco curvo em anéis e leque de folhas caídas.</summary>
        static Color PalmPixel(float2 p)
        {
            float2 q = new float2(p.x, p.y * 1.5f);
            Color outc = Color.clear;
            // tronco: curva de (0.45, 0) até (0.58, 0.98)
            float t = math.saturate(q.y / 0.98f);
            float cx = 0.45f + 0.13f * t * t;
            float w = 0.055f - 0.02f * t;
            float dx = math.abs(q.x - cx);
            if (q.y < 1.0f && dx < w + 0.012f)
            {
                Color bark = math.frac(q.y * 11f) < 0.22f ? Hex("#8A6438") : Hex("#B08452");
                outc = dx > w - 0.012f ? Ink(Hex("#8A6438")) : bark;
            }
            // folhas: 7 folhas que saem da copa e caem com o peso (linha central = dir·s − (0, k·s²))
            float2 crown = new float2(0.58f, 1.0f);
            const float len = 0.54f, droop = 1.5f;
            for (int k = 0; k < 7; k++)
            {
                float ang = math.radians(k == 0 ? 200f : k == 1 ? 158f : k == 2 ? 122f : k == 3 ? 90f : k == 4 ? 58f : k == 5 ? 22f : -18f);
                float2 dir = new float2(math.cos(ang), math.sin(ang));
                float2 v = q - crown;
                float s = math.dot(v, dir);
                if (s < 0f || s > len + 0.02f) continue;
                float2 center = dir * s - new float2(0f, droop * s * s);
                float off = math.length(v - center);
                float half = 0.065f * math.sin(math.PI * math.saturate(s / len)) + 0.004f;
                float dd = off - half;
                if (dd > 0.012f) continue;
                Color c = Color.Lerp(Hex("#3E8A34"), Hex("#7CC24E"), math.saturate(0.4f + dir.y * 0.4f + (half - off) * 4f));
                if (off < 0.006f) c = Hex("#2F6B2A");                      // nervura
                if (dd > -0.014f) c = Ink(Hex("#3E8A34")) * 1.4f;
                c.a = Aa(dd, 0.01f);
                outc = Over(outc, c);
            }
            // cocos
            outc = Over(outc, Disc(q - crown + new float2(0.03f, 0.04f) + 0.5f, 0.035f, Hex("#6B4A2A")));
            outc = Over(outc, Disc(q - crown + new float2(-0.035f, 0.035f) + 0.5f, 0.032f, Hex("#5C3F24")));
            return outc;
        }

        // ------------------------------------------------------------------ Tundra (Era 1, Bíblia §4.1)
        /// <summary>Neve pintada: dois tons, sombras azuladas, manchas de tundra exposta e brilhos.</summary>
        static Texture2D TundraTexture()
        {
            var t = DrawTexture(256, 256, p =>
            {
                float big = VNoise(p * 4f, 4), mid = VNoise(p * 16f, 16);
                Color c = Color.Lerp(Hex("#EEF5FA"), Hex("#D5E5F0"), big);
                c = Color.Lerp(c, Hex("#B9D1E2"), math.saturate(mid - 0.58f) * 0.9f);          // sombras azuis
                float bare = VNoise(p * 3f + 7.3f, 3);
                if (bare > 0.74f) c = Color.Lerp(c, Hex("#9AA284"), math.saturate((bare - 0.74f) * 6f) * 0.6f);   // tundra exposta
                float2 cell = math.floor(p * 48f), local = math.frac(p * 48f);
                float h = Hash(cell);
                if (h > 0.9f && math.abs(local.y - 0.5f + (local.x - 0.5f) * 0.4f) < 0.08f) c = Color.Lerp(c, Hex("#C9DCEA"), 0.6f);   // pinceladas
                if (Hash(math.floor(p * 96f) + 5.7f) > 0.988f) c = Color.white;                 // brilhos
                c.a = 1f;
                return c;
            }, false);
            t.name = "TEX_E1_TundraSnow";
            return t;
        }

        /// <summary>Poça congelada: gelo azul com riscos brancos e contorno escuro.</summary>
        static Color IcePatchPixel(float2 p)
        {
            float2 q = p - 0.5f;
            float d = math.length(q * new float2(1f, 1.25f)) * 2f + (VNoise(p * 5f, 5) - 0.5f) * 0.45f;
            if (d > 0.9f) return Color.clear;
            Color c = Color.Lerp(Hex("#C3E6F6"), Hex("#8CC6E3"), math.saturate(d * 1.1f));
            float streak = math.abs(math.frac((p.x + p.y * 0.6f) * 6f) - 0.5f);
            if (streak < 0.04f && d < 0.75f) c = Color.Lerp(c, Color.white, 0.7f);
            if (d > 0.84f) c = Hex("#5C93B5");
            c.a = Aa(d - 0.88f, 0.03f);
            return c;
        }

        static Color RockPixel(float2 p, Color dark, Color light, bool snow)
        {
            float2 q = p - new float2(0.5f, 0.46f);
            float ang = math.atan2(q.y, q.x);
            float r = 0.36f + 0.05f * math.sin(ang * 3f + 1.2f) + 0.03f * math.sin(ang * 7f);
            float d = math.length(q * new float2(1f, 1.25f)) - r;
            if (d > 0.01f) return Color.clear;
            Color c = Color.Lerp(dark, light, math.saturate(q.y * 2f + 0.5f));
            if (snow && q.y > 0.12f + 0.04f * math.sin(q.x * 20f)) c = Hex("#F4FAFF");   // neve no topo
            if (d > -0.04f) c = Ink(dark) * 1.5f;
            c.a = Aa(d, 0.012f);
            return c;
        }

        static Color PinePixel(float2 p, bool snow)
        {
            Color outc = Color.clear;
            // tronco
            if (math.abs(p.x - 0.5f) < 0.06f && p.y < 0.2f)
                outc = math.abs(p.x - 0.5f) > 0.035f || p.y < 0.02f ? Ink(Hex("#7A4E2A")) : Hex("#7A4E2A");
            // três andares de galhos, de baixo para cima
            for (int k = 0; k < 3; k++)
            {
                float y0 = 0.13f + k * 0.23f, y1 = y0 + 0.42f - k * 0.04f, half = 0.46f - k * 0.1f;
                if (p.y < y0 || p.y > y1) continue;
                float t = (p.y - y0) / (y1 - y0);
                float w = half * (1f - t);
                float dx = math.abs(p.x - 0.5f);
                if (dx > w + 0.015f) continue;
                Color c = Color.Lerp(Hex("#2F7D46"), Hex("#45A35E"), t);
                float snowLine = y0 + (y1 - y0) * (0.55f + 0.1f * math.sin(p.x * 40f));
                if (snow && p.y > snowLine && dx > w - 0.12f) c = Hex("#F4FAFF");   // neve nas pontas
                if (dx > w - 0.018f || p.y < y0 + 0.015f) c = Ink(Hex("#2F7D46"));
                c.a = Aa(dx - w - 0.005f, 0.01f);
                outc = Over(outc, c);
            }
            return outc;
        }

        static Texture2D MudTexture()
        {
            var t = DrawTexture(128, 128, p =>
            {
                float n = Hash(math.floor(p * 32f));
                Color c = Color.Lerp(Mud, Hex("#5A524A"), n * 0.6f);
                if (Hash(math.floor(p * 20f) + 3.1f) > 0.92f) c = Color.Lerp(c, Hex("#8E857B"), 0.7f);   // pedrinhas
                float edge = math.min(p.y, 1f - p.y);
                if (edge < 0.08f) c = Color.Lerp(Ink(Mud), c, edge / 0.08f);                           // borda pintada
                return c;
            }, false);
            t.name = "TEX_E1_Mud";
            return t;
        }

        static Texture2D BackdropTexture()
        {
            var t = DrawTexture(256, 128, p =>
            {
                Color sky = Color.Lerp(Hex("#3B5A7A"), NightSky, p.y);
                float wave = 0.62f + 0.08f * math.sin(p.x * 9f) + 0.04f * math.sin(p.x * 23f + 1.3f);
                float band = math.saturate(1f - math.abs(p.y - wave) * 9f);
                sky = Color.Lerp(sky, Aurora, band * 0.6f);
                float mountain = 0.28f + 0.12f * math.abs(math.sin(p.x * 7.3f)) + 0.05f * math.sin(p.x * 31f);
                if (p.y < mountain) sky = Color.Lerp(Hex("#C9DCE8"), Hex("#7E97AE"), (mountain - p.y) * 3f);
                return sky;
            });
            t.name = "TEX_E1_Backdrop";
            return t;
        }

        static Color PedestalPixel(float2 p)
        {
            float d = math.distance(p, 0.5f);
            if (d > 0.5f) return Color.clear;
            if (d > 0.465f) return Ink(Stone);
            if (d > 0.3f)
            {
                float ang = math.atan2(p.y - 0.5f, p.x - 0.5f);
                float block = math.frac(ang / (math.PI * 2f) * 10f);
                Color s = Color.Lerp(Stone, Hex("#857B71"), Hash(new float2(math.floor(ang * 1.6f), 1f)) * 0.6f);
                if (block < 0.06f || d < 0.32f) s = Ink(Stone);
                return s;
            }
            return Color.Lerp(Hex("#2E2A26"), Hex("#3A342F"), d / 0.3f);   // centro escuro rebaixado
        }

        static Color GlyphPixel(float2 p)
        {
            float2 q = p - 0.5f;
            float a = math.min(math.abs(q.x), math.abs(q.y - q.x * 0.5f));
            float shape = Aa(a - 0.05f) * Aa(math.length(q) - 0.42f);
            return new Color(1, 1, 1, shape);
        }

        static Color RangePixel(float2 p)
        {
            float d = math.distance(p, 0.5f);
            float edge = Aa(math.abs(d - 0.49f) - 0.008f, 0.004f);
            float fill = d < 0.49f ? 0.12f : 0f;
            return new Color(1, 1, 1, math.max(edge * 0.9f, fill));
        }

        static Color LairPixel(float2 p)
        {
            float2 q = new float2((p.x - 0.5f) * 2f, p.y);
            float outer = math.length(new float2(q.x, (q.y - 0.05f) * 1.1f)) - 0.9f;
            if (outer > 0f) return Color.clear;
            float inner = math.length(new float2(q.x * 1.25f, (q.y - 0.05f) * 1.5f)) - 0.75f;
            Color ice = Color.Lerp(Ice, Hex("#D8EEF6"), Hash(math.floor(p * 18f)) * 0.5f);
            if (outer > -0.04f) return Ink(Ice);
            if (inner < 0f) return Color.Lerp(Hex("#0E1626"), Corruption, 0.3f + 0.3f * p.y);
            if (inner < 0.04f) return Ink(Ice);
            return ice;
        }

        static Color CorePixel(float2 p)
        {
            float2 q = new float2(math.abs(p.x - 0.5f) * 2f, p.y);
            float width = q.y < 0.5f ? math.lerp(0.75f, 0.12f, q.y * 2f) : math.lerp(0.12f, 0.75f, (q.y - 0.5f) * 2f);
            if (q.y < 0.04f || q.y > 0.96f) return q.x < 0.9f ? Ink(Hex("#C9A646")) : Color.clear;
            if (q.x > width) return Color.clear;
            if (q.x > width - 0.08f) return Ink(Hex("#6FD3FF"));
            return Color.Lerp(Hex("#6FD3FF"), Hex("#F2C14E"), q.y < 0.5f ? 0.8f - q.y : 0.1f);
        }

        static Color HeartPixel(float2 p)
        {
            float2 q = (p - new float2(0.5f, 0.45f)) * 2.2f;
            float a = q.x * q.x + math.pow(q.y - math.sqrt(math.abs(q.x)) * 0.8f, 2f) - 0.8f;
            if (a > 0f) return Color.clear;
            return a > -0.18f ? Ink(Hex("#E8434A")) : Hex("#E8434A");
        }

        static Color TowerPixel(float2 p, int kind)
        {
            Color wood = Hex("#8B5A2B"), stone = Hex("#8C8279");
            Color body = kind == 0 ? wood : kind == 1 ? stone : kind == 2 ? Hex("#5E7A3A") : Hex("#B5562F");
            float2 q = new float2(math.abs(p.x - 0.5f), p.y);
            bool inside;
            bool roof = false;
            if (kind == 0)        // Atirador: torre de vigia alta com telhado
            {
                inside = (q.x < 0.22f && q.y < 0.7f) || (q.y >= 0.7f && q.y < 0.95f && q.x < (0.95f - q.y) * 1.3f);
                roof = q.y >= 0.7f;
            }
            else if (kind == 1)   // Lançador: base atarracada + pedra
            {
                bool baseBox = q.x < 0.38f && q.y < 0.45f;
                bool rock = math.distance(p, new float2(0.5f, 0.62f)) < 0.2f;
                inside = baseBox || rock;
                roof = rock && !baseBox;
            }
            else if (kind == 2)   // Alquimista: totem com chama
            {
                inside = (q.x < 0.18f && q.y < 0.65f) || math.distance(p, new float2(0.5f, 0.76f)) < 0.17f;
                roof = q.y >= 0.6f;
            }
            else                  // Guarda: paliçada
            {
                inside = q.x < 0.42f && q.y < 0.55f + 0.08f * math.abs(math.sin(p.x * 25f));
            }
            if (!inside) return Color.clear;
            Color c = roof ? (kind == 1 ? Hex("#A79C90") : kind == 2 ? Hex("#7FE8FF") : Fire) : body;
            float n = Hash(math.floor(p * 24f));
            c = Color.Lerp(c, c * 0.8f, n * 0.4f);
            // contorno: amostra vizinhos
            float e = 0.025f;
            bool edge = !Inside(p + new float2(e, 0), kind) || !Inside(p - new float2(e, 0), kind) || !Inside(p + new float2(0, e), kind) || !Inside(p - new float2(0, e), kind);
            if (edge) c = Ink(c);
            c.a = 1f;
            return c;
        }

        static bool Inside(float2 p, int kind)
        {
            float2 q = new float2(math.abs(p.x - 0.5f), p.y);
            if (p.y < 0f || p.y > 1f) return false;
            switch (kind)
            {
                case 0: return (q.x < 0.22f && q.y < 0.7f) || (q.y >= 0.7f && q.y < 0.95f && q.x < (0.95f - q.y) * 1.3f);
                case 1: return (q.x < 0.38f && q.y < 0.45f) || math.distance(p, new float2(0.5f, 0.62f)) < 0.2f;
                case 2: return (q.x < 0.18f && q.y < 0.65f) || math.distance(p, new float2(0.5f, 0.76f)) < 0.17f;
                default: return q.x < 0.42f && q.y < 0.55f + 0.08f * math.abs(math.sin(p.x * 25f));
            }
        }

        static Color KrogPixel(float2 p)
        {
            // Chibi 2,8 cabeças: cabeça grande, corpo largo de pele, lança de osso
            Color skin = Hex("#8A5A3B"), fur = Hex("#6E4A2E");
            float head = math.distance(p, new float2(0.5f, 0.72f)) - 0.2f;
            float2 b = math.abs(p - new float2(0.5f, 0.32f)) - new float2(0.26f, 0.24f);
            float body = math.length(math.max(b, 0f)) + math.min(math.max(b.x, b.y), 0f) - 0.04f;
            float spear = math.abs((p.x - 0.82f) - (p.y - 0.5f) * 0.15f) - 0.025f;
            bool spearIn = spear < 0f && p.y > 0.05f && p.y < 0.98f;
            float d = math.min(math.min(head, body), spearIn ? -0.01f : 1f);
            if (d > 0.012f) return Color.clear;
            Color c = head < 0f ? skin : body < 0f ? fur : Bone;
            if (head < 0f && math.distance(p, new float2(0.5f, 0.62f)) < 0.07f) c = Fire;   // pintura tribal
            if (spearIn && p.y > 0.88f) c = Hex("#4A4A52");
            if (d > -0.015f) c = Ink(c);
            c.a = 1f;
            return c;
        }

        static Color KrogPortraitPixel(float2 p)
        {
            Color bg = Color.Lerp(Hex("#2B3A55"), Hex("#1C2A4A"), p.y);
            float head = math.distance(p, new float2(0.5f, 0.45f)) - 0.32f;
            if (head > 0f) return bg;
            Color c = Hex("#8A5A3B");
            if (p.y > 0.62f) c = Hex("#4A3222");                                              // cabelo
            if (math.abs(p.y - 0.47f) < 0.03f && math.abs(p.x - 0.5f) > 0.08f && math.abs(p.x - 0.5f) < 0.18f) c = Fire;   // pintura
            if (head > -0.03f) c = Ink(c);
            return c;
        }

        static Color CreepColor(EnemyDataSO e)
        {
            if (e is BossDataSO) return Hex("#7A5BA8");
            switch (e.Id)
            {
                case 101: return Hex("#8A97A6");   // Lobo-Cinzento
                case 102: return Hex("#D08A3A");   // Tigre Dente-de-Sabre
                case 103: return Hex("#7A6553");   // Rinoceronte-Lanudo
                case 104: return Hex("#9A6A4A");   // Caçador Corrompido
                default:
                    var h = Hash(new float2(e.Id, 7f));
                    return Color.HSVToRGB(h, 0.45f, 0.75f);
            }
        }

        /// <summary>Ficha de creep: corpo + contorno violeta da Entropia + olhos magenta (Bíblia §3.7).</summary>
        static Color CreepPixel(float2 p, Color body, bool boss)
        {
            float d = math.distance(p, new float2(0.5f, 0.45f));
            float r = 0.42f;
            if (d > r) return Color.clear;
            Color c = Color.Lerp(body, Color.white, math.saturate(p.y - 0.55f) * 0.5f);
            if (d > r - 0.06f) c = Corruption;
            float eyeY = 0.52f;
            if (math.distance(p, new float2(0.38f, eyeY)) < 0.055f || math.distance(p, new float2(0.62f, eyeY)) < 0.055f) c = Magenta;
            if (boss && p.y > 0.7f && math.abs(p.x - 0.5f) < 0.2f) c = Color.Lerp(c, Magenta, 0.5f);   // veios
            return c;
        }
    }
}
