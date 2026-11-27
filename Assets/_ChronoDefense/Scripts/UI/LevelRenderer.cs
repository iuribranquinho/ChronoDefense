// GDD §11 — LevelRenderer: carrega o cenário a partir do LevelLayoutSO + EraBiomeThemeSO e expõe
// estados visuais sem alocar durante a partida (tudo é criado em LoadAsync).
// Prefabs do Addressables são usados quando o tema/layout os define; caso contrário, arte provisória
// procedural (ArtLibrary) com shaders Sprite-Lit-Default.
using System.Collections.Generic;
using System.Threading;
using ChronoDefense.Data;
using ChronoDefense.Systems;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Rendering;

namespace ChronoDefense.UI
{
    public enum PedestalVisualState : byte { Free, Highlighted, Selected, Occupied, UpgradeAvailable, Disabled, Locked }
    public enum RuneVisualState : byte { Idle, WaveActive, BossWave }
    public enum QualityTier : byte { Low, Medium, High }

    public interface ILevelRenderer
    {
        Awaitable LoadAsync(LevelDataSO level, EraBiomeThemeSO theme, CancellationToken ct);
        Vector3 GetPedestalPosition(int pedestalIndex);
        void SetPedestalState(int pedestalIndex, PedestalVisualState state);
        void SetRuneState(RuneVisualState state);
        void ShowTowerStage(int pedestalIndex, TowerArchetype a, int path, int stage);
        void ClearPedestal(int pedestalIndex);
        Awaitable PlayLairOpenAsync(CancellationToken ct);
        Awaitable PlayRescueAsync(HeroDataSO hero, CancellationToken ct);
        void ApplyQualityTier(QualityTier tier);
        void Unload();
    }

    public sealed class LevelRenderer : MonoBehaviour, ILevelRenderer
    {
        // Ordem de desenho dos transparentes (Bíblia §3.8: unidades > runas/pedestais > caminho > terreno)
        const int OrderGround = -100, OrderDecal = -97, OrderPathShadow = -94, OrderPathEdge = -92, OrderPath = -90,
                  OrderRunes = -80, OrderShadow = -72, OrderPedestal = -70, OrderRing = -65, OrderRange = -60, OrderUnits = 0;

        /// <summary>PROV: sprites provisórios ampliados para leitura em tela de 6" (Bíblia §1.2) até haver modelos 3D,
        /// na mesma escala visual das outras unidades (ArtLibrary.UnitScale).</summary>
        const float TowerReadabilityScale = 0.85f * ArtLibrary.UnitScale;
        /// <summary>Anel de seleção/aviso do tamanho da base da unidade (sprite RuneRing = Ø 1,75 m).</summary>
        const float RingScale = (2f * CombatSimulation.FootprintRadius + 0.4f) / 1.75f;
        /// <summary>Torres 3D de cada tipo criadas no carregamento (zero-GC ao construir).</summary>
        const int TowerViewsPerArchetype = 12;

        // Pegada da arte do covil e do Núcleo (usada também no cálculo do CameraBounds do layout).
        public static readonly Vector3 LairArtOffset = new Vector3(0f, 0f, 1.5f);
        // Covil 3D do Tripo: 7 m de largura × ~5,5 m (Tools/tripo/modelos_era1.json), cabe o Mamute (2,4 m × UnitScale).
        public const float LairArtHalfWidth = 3.5f, LairArtHeight = 5.5f;
        public static readonly Vector3 CoreArtOffset = new Vector3(-1.2f, 0f, 0f);
        public const float CoreArtHalfWidth = 1.0f, CoreArtHeight = 3.0f;   // Núcleo 3D: 3 m de altura

        public ArtLibrary Art { get; private set; }
        public Camera Camera;   // para orientar sprites em pé (billboards)

        LevelLayoutSO _layout;
        int _decorSeed;
        EraBiomeThemeSO _theme;
        Transform _root;
        Color _accent;

        SpriteRenderer[] _pedestalRings, _pedestalUnits, _pedestalWarnings, _shadows;
        Vector3[] _spotPos;          // posição de cada ponto (posicionamento livre: muda quando o jogador constrói)
        GameObject[][] _towerPool;   // [arquétipo][k]: modelos 3D da skin da Era (vazio = sprite provisório)
        int[] _spotView;             // ponto → índice no _towerPool[_spotViewArch] ou -1
        int[] _spotViewArch;
        bool[][] _towerPoolUsed;

        // Movimento procedural das unidades (26/11): surge com quique, "respira" parada, recua ao atacar e gira
        // para o alvo (como os macacos do BTD6). Arrays por ponto, criados no carregamento (zero-GC).
        Transform[] _spotObj;        // o que está aparecendo no ponto (modelo 3D ou sprite)
        float[] _spotBase;           // escala base desse objeto
        bool[] _spotBillboard;       // sprite: não gira em Y
        float[] _punch, _pop, _yaw, _yawTarget;

        // Item na mão (lança, pedra, orbe): visível enquanto a unidade está pronta; some no arremesso e volta
        // depois da recarga com um quique. Um quad por ponto com o material do projétil (criado no carregamento).
        MeshRenderer[] _held;
        int[] _heldVisual;           // índice de ArtLibrary.ProjectileMaterials ou -1 (não segura nada)
        float[] _reload, _heldPop;

        // Lançamento da habilidade do herói: lança em chamas voando da mão até o ponto (o chão pega fogo ao cair,
        // UnitRenderer.RenderFireZones). Um quad, criado no carregamento.
        MeshRenderer _skillThrow;
        Vector3 _throwFrom, _throwTo;
        float _throwT = 1f;
        public const float SkillThrowTime = 0.35f;

        // Fotos para a loja e o painel (renderizadas dos próprios modelos 3D no carregamento)
        public Texture[] TowerPortraits { get; private set; }   // [arquétipo]; null = sem modelo 3D
        public Texture[] HeroPortraits { get; private set; }    // [herói do loadout]
        readonly List<RenderTexture> _portraitTargets = new List<RenderTexture>();
        static readonly Vector3 PortraitStudio = new Vector3(0f, -500f, 0f);   // longe do mapa e da luz da neblina
        static readonly Color PortraitBackground = new Color32(74, 138, 196, 255);
        static readonly Quaternion FacingCamera = Quaternion.Euler(0f, 180f, 0f);   // modelos com a frente em +Z
        PedestalVisualState[] _pedestalState;
        float[] _pulse;
        SpriteRenderer[] _runes;
        RuneVisualState _runeState;
        SpriteRenderer _lair;
        GameObject[] _heroViews;   // modelo 3D por herói do loadout (null = sprite provisório)
        HeroPoseView[] _heroPoses; // null se o prefab do herói tem uma pose só
        Light _mainLight;
        GameObject _backdrop;
        QualityTier _tier = QualityTier.Medium;
        float _time;
        readonly List<AsyncOperationHandle<GameObject>> _handles = new List<AsyncOperationHandle<GameObject>>();

        public int PedestalCount => _pedestalState?.Length ?? 0;

        // ================================================================== carregamento (GDD §11.3)
        public async Awaitable LoadAsync(LevelDataSO level, EraBiomeThemeSO theme, CancellationToken ct)
        {
            Unload();
            _layout = level.Layout;
            _theme = theme;
            _accent = theme != null ? theme.AccentRune : ArtLibrary.Hex("#6FD3FF");
            _root = new GameObject("LevelRoot").transform;
            _root.SetParent(transform, false);

            // 1. Arte (Addressables quando houver; senão procedural)
            Art = new ArtLibrary();
            _decorSeed = level.Biome != GroundStyle.Auto ? level.GlobalIndex : 0;   // fases que dividem o layout ganham decoração própria
            Art.Build(CollectEnemyTypes(level), _accent, level.ResolvedBiome);
            GameObject env = await TryInstantiate(_layout.EnvironmentArt, Vector3.zero, ct);
            if (env == null) { BuildGroundAndPath(); BuildDecor(); }
            // Sem céu: a câmera olha para baixo e o mapa preenche a tela (Referencia_BTD6.md §1).
            _backdrop = await TryInstantiate(theme != null ? theme.Backdrop : null, new Vector3(0, 0, 45f), ct);

            // 2. Iluminação (Bíblia §3.3 / tema)
            ApplyLighting(theme);
            ApplyBiomeLighting();

            // 3. Pontos de construção (posicionamento livre, sem pedestal visível): anel de seleção, aviso do chefe,
            // sombra de contato e sprite provisório, um conjunto por ponto possível (CombatSimulation.MaxSpots).
            // Montados em locais e publicados só no fim: o Update sai cedo enquanto _pedestalState == null.
            int n = CombatSimulation.MaxSpots;
            var rings = new SpriteRenderer[n];
            var units = new SpriteRenderer[n];
            var warnings = new SpriteRenderer[n];
            var shadows = new SpriteRenderer[n];
            var state = new PedestalVisualState[n];
            _spotPos = new Vector3[n];
            _pulse = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = _layout.Pedestals != null && i < _layout.Pedestals.Length ? _layout.Pedestals[i] : Vector3.zero;
                _spotPos[i] = pos;
                shadows[i] = Flat("Shadow_" + i, Art.Shadow, pos + Vector3.up * 0.02f, new Color(0f, 0f, 0f, 0.35f), OrderShadow);
                shadows[i].transform.localScale = Vector3.one * (ArtLibrary.UnitScale / 2.5f);
                shadows[i].enabled = false;
                rings[i] = Flat("SpotRing_" + i, Art.RuneRing, pos + Vector3.up * 0.03f, Color.clear, OrderRing);
                rings[i].transform.localScale = Vector3.one * RingScale;
                warnings[i] = Flat("SpotWarning_" + i, Art.Warning, pos + Vector3.up * 0.04f, Color.white, OrderRing + 1);
                warnings[i].enabled = false;
                units[i] = Upright("Unit_" + i, null, pos, OrderUnits);
                units[i].enabled = false;
                state[i] = PedestalVisualState.Free;
            }
            _pedestalRings = rings;
            _pedestalUnits = units;
            _pedestalWarnings = warnings;
            _shadows = shadows;
            _spotView = new int[n];
            _spotViewArch = new int[n];
            _spotObj = new Transform[n];
            _spotBase = new float[n];
            _spotBillboard = new bool[n];
            _punch = new float[n];
            _pop = new float[n];
            _yaw = new float[n];
            _yawTarget = new float[n];
            for (int i = 0; i < n; i++) _yaw[i] = _yawTarget[i] = 180f;   // de frente para a câmera
            {
                var go = new GameObject("SkillThrow");
                go.transform.SetParent(_root, false);
                go.AddComponent<MeshFilter>().sharedMesh = Art.QuadCenter;
                _skillThrow = go.AddComponent<MeshRenderer>();
                _skillThrow.sharedMaterial = Art.ProjectileMaterials[4];
                _skillThrow.shadowCastingMode = ShadowCastingMode.Off;
                _skillThrow.enabled = false;
            }
            _held = new MeshRenderer[n];
            _heldVisual = new int[n];
            _reload = new float[n];
            _heldPop = new float[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Held_" + i);
                go.transform.SetParent(_root, false);
                go.AddComponent<MeshFilter>().sharedMesh = Art.QuadCenter;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Art.ProjectileMaterials[0];
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.enabled = false;
                _held[i] = mr;
                _heldVisual[i] = -1;
            }
            for (int i = 0; i < n; i++) _spotView[i] = -1;
            _pedestalState = state;

            // 4. Runas a cada 3 m no sentido do fluxo
            BuildRunes();

            // 4b. Torres 3D (EraBiomeThemeSO.TowerSkins[arquétipo].BaseL1): um estoque desativado por tipo, criado
            // aqui para que ShowTowerStage nunca instancie durante a partida (zero-GC).
            _towerPool = new GameObject[TowerArchetypeCount][];
            _towerPoolUsed = new bool[TowerArchetypeCount][];
            for (int a = 0; a < TowerArchetypeCount; a++)
            {
                var list = new List<GameObject>(TowerViewsPerArchetype);
                if (theme != null && theme.TowerSkins != null && a < theme.TowerSkins.Length)
                    for (int k = 0; k < TowerViewsPerArchetype; k++)
                    {
                        GameObject v = await TryInstantiate(theme.TowerSkins[a].BaseL1, Vector3.zero, ct);
                        if (v == null) break;   // sem modelo para este tipo: fica o sprite
                        v.transform.rotation = FacingCamera;
                        v.SetActive(false);
                        list.Add(v);
                    }
                _towerPool[a] = list.ToArray();
                _towerPoolUsed[a] = new bool[_towerPool[a].Length];
            }

            // 5. Covil e Núcleo (estilo BTD6: sem bases; o caminho entra e sai pelas bordas da tela)
            if (_layout.ShowLairAndCore) await BuildLairAndCoreAsync(level, theme, ct);

            SetRuneState(RuneVisualState.Idle);
            await Awaitable.NextFrameAsync(ct);
        }

        async Awaitable BuildLairAndCoreAsync(LevelDataSO level, EraBiomeThemeSO theme, CancellationToken ct)
        {
            GameObject lairPrefab = level.Boss != null && level.Boss.LairPrefab != null && level.Boss.LairPrefab.RuntimeKeyIsValid()
                ? await TryInstantiate(level.Boss.LairPrefab, _layout.LairPosition, ct)
                : await TryInstantiate(theme != null ? theme.Portal : null, _layout.LairPosition, ct);
            if (lairPrefab == null) _lair = Upright("Lair", Art.Lair, _layout.LairPosition + LairArtOffset, OrderUnits - 1);
            else lairPrefab.transform.rotation = FacingCamera;
            GameObject core = await TryInstantiate(theme != null ? theme.Core : null, _layout.CorePosition, ct);
            if (core == null) Upright("Core", Art.Core, _layout.CorePosition + CoreArtOffset, OrderUnits - 1);
            else core.transform.rotation = FacingCamera;
        }

        /// <summary>
        /// GDD §11.3 passo 7: pré-carrega as views 3D dos heróis do loadout (HeroDataSO.ViewPrefab).
        /// Ficam desativadas até ShowHero; sem prefab, o herói continua com o sprite provisório.
        /// </summary>
        public async Awaitable LoadHeroViewsAsync(HeroDataSO[] loadout, CancellationToken ct)
        {
            _heroViews = new GameObject[loadout.Length];
            _heroPoses = new HeroPoseView[loadout.Length];
            for (int h = 0; h < loadout.Length; h++)
            {
                GameObject view = loadout[h] != null ? await TryInstantiate(loadout[h].ViewPrefab, Vector3.zero, ct) : null;
                if (view != null)
                {
                    _heroPoses[h] = view.GetComponent<HeroPoseView>();
                    view.SetActive(false);
                }
                _heroViews[h] = view;
            }
        }

        static EnemyDataSO[] CollectEnemyTypes(LevelDataSO level)
        {
            // Mesma ordem/ids de CombatSimulation.BuildEnemyCatalog (PoolId já atribuído).
            var list = new List<EnemyDataSO>();
            void Add(EnemyDataSO e) { if (e != null && !list.Contains(e)) list.Add(e); }
            if (level.Waves != null)
                foreach (var w in level.Waves)
                    if (w != null && w.Groups != null)
                        foreach (var g in w.Groups) Add(g.Enemy);
            if (level.Boss != null)
            {
                Add(level.Boss);
                if (level.Boss.Phases != null) foreach (var p in level.Boss.Phases) Add(p.SummonType);
            }
            return list.ToArray();
        }

        async Awaitable<GameObject> TryInstantiate(AssetReferenceGameObject reference, Vector3 pos, CancellationToken ct)
        {
            if (reference == null || !reference.RuntimeKeyIsValid()) return null;
            var handle = reference.InstantiateAsync(pos, Quaternion.identity, _root);
            _handles.Add(handle);
            while (!handle.IsDone)
            {
                ct.ThrowIfCancellationRequested();
                await Awaitable.NextFrameAsync(ct);
            }
            return handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
        }

        void ApplyLighting(EraBiomeThemeSO theme)
        {
            _mainLight = null;
            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Directional) { _mainLight = lights[i]; break; }
            if (_mainLight == null)
            {
                _mainLight = new GameObject("MainLight").AddComponent<Light>();
                _mainLight.type = LightType.Directional;
            }
            if (theme == null) return;
            _mainLight.color = theme.MainLightColor;
            _mainLight.intensity = theme.MainLightIntensity;
            _mainLight.transform.rotation = Quaternion.Euler(theme.MainLightEuler);
            _mainLight.shadows = LightShadows.None;   // sombra realtime só no High (ApplyQualityTier)

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = theme.AmbientSky;
            RenderSettings.ambientEquatorColor = theme.AmbientEquator;
            RenderSettings.ambientGroundColor = theme.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = theme.FogColor;
            RenderSettings.fogStartDistance = theme.FogStart;
            RenderSettings.fogEndDistance = theme.FogEnd;
            if (Camera != null)
            {
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.backgroundColor = ArtLibrary.NightSky;
            }
        }

        /// <summary>Luz e neblina do bioma (mata, savana, deserto, planalto). Tundra e Prado mantêm a luz do tema.</summary>
        void ApplyBiomeLighting()
        {
            if (Art == null || !Art.OverridesLighting) return;
            if (_mainLight != null) _mainLight.color = Art.LightColor;
            RenderSettings.ambientSkyColor = Art.AmbientSky;
            RenderSettings.ambientEquatorColor = Color.Lerp(Art.AmbientSky, Art.AmbientGround, 0.5f);
            RenderSettings.ambientGroundColor = Art.AmbientGround;
            RenderSettings.fogColor = Art.FogColor;
            if (Camera != null) Camera.backgroundColor = Art.FogColor;
        }

        // ================================================================== geometria provisória
        void BuildGroundAndPath()
        {
            Bounds b = _layout.CameraBounds;
            var ground = new GameObject("Ground");
            ground.transform.SetParent(_root, false);
            ground.transform.position = new Vector3(b.center.x, 0f, b.center.z);
            const float margin = 200f;   // o chão some na neblina antes da borda
            ground.transform.localScale = new Vector3(b.size.x + margin, 1f, b.size.z + margin);
            ground.AddComponent<MeshFilter>().sharedMesh = Art.QuadXZ;
            var gr = ground.AddComponent<MeshRenderer>();
            gr.sharedMaterial = Art.GroundMaterial;
            gr.sortingOrder = OrderGround;
            gr.shadowCastingMode = ShadowCastingMode.Off;

            Art.GroundMaterial.mainTextureScale = new Vector2((b.size.x + margin) / ArtLibrary.GroundTileMeters, (b.size.z + margin) / ArtLibrary.GroundTileMeters);

            // Caminho no estilo do BTD6: sombra suave + contorno escuro grosso + lajotas de pedra.
            Ribbon("PathShadow", _layout.PathWidth + 1.4f, 0.004f, Art.FlatMaterial, new Color(0f, 0f, 0f, 0.18f), OrderPathShadow);
            Ribbon("PathEdge", _layout.PathWidth + 0.5f, 0.007f, Art.FlatMaterial, Art.PathEdgeColor, OrderPathEdge);
            Ribbon("Path", _layout.PathWidth, 0.01f, Art.PathMaterial, Color.white, OrderPath);
        }

        void Ribbon(string name, float width, float y, Material mat, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(0, y, 0);
            go.AddComponent<MeshFilter>().sharedMesh = BuildRibbon(_layout.PathPolyline, width, color);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Decoração pintada (Referencia_BTD6.md, Fase C): manchas de neve, flores, tufos e pedras no chão, fora do
        /// caminho; pinheiros só na faixa da borda, emoldurando o mapa. Semente fixa por layout (sempre igual).
        /// </summary>
        void BuildDecor()
        {
            Bounds b = _layout.CameraBounds;
            var rng = new System.Random(_layout.Id * 7919 + _decorSeed * 104729 + 17);
            float area = b.size.x * b.size.z;
            float half = _layout.PathWidth * 0.5f;
            Vector3 lair = _layout.LairPosition, core = _layout.CorePosition;

            bool FarFromLandmarks(Vector3 p, float r) =>
                (p - lair).sqrMagnitude > r * r && (p - core).sqrMagnitude > r * r;
            Vector3 RandomIn(float grow) => new Vector3(
                b.min.x - grow + (float)rng.NextDouble() * (b.size.x + 2f * grow), 0f,
                b.min.z - grow + (float)rng.NextDouble() * (b.size.z + 2f * grow));

            void Scatter(Sprite sprite, int count, float minPathGap, float minScale, float maxScale, int order, float grow)
            {
                for (int k = 0, tries = 0; k < count && tries < count * 20; tries++)
                {
                    Vector3 p = RandomIn(grow);
                    if (DistanceToPath(p) < half + minPathGap || !FarFromLandmarks(p, 4.5f)) continue;
                    var r = Flat("Decor_" + sprite.name, sprite, p + Vector3.up * 0.003f, Color.white, order);
                    float s = minScale + (float)rng.NextDouble() * (maxScale - minScale);
                    r.transform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -s : s, s, s);
                    r.transform.Rotate(0f, 0f, (float)rng.NextDouble() * 360f, Space.Self);
                    k++;
                }
            }

            bool painted = Art.PaintedProps;   // vegetação pintada: o chão já tem as folhinhas desenhadas
            Scatter(Art.SnowPatch, Mathf.RoundToInt(area / (painted ? 220f : 110f)), 0.8f, 0.6f, 1.4f, OrderDecal, 6f);
            Scatter(Art.Flowers, Mathf.RoundToInt(area / 22f * Art.FlowerDensity), 0.4f, 0.7f, 1.2f, OrderDecal + 1, 6f);
            Scatter(Art.GrassTuft, Mathf.RoundToInt(area / (painted ? 40f : 14f)), 0.2f, 0.8f, 1.3f, OrderDecal + 1, 6f);
            if (!painted) Scatter(Art.Rock, Mathf.RoundToInt(area / 90f * Art.RockDensity), 0.6f, 0.7f, 1.5f, OrderDecal + 2, 6f);
            else BuildPaintedProps(b, rng, half, area, FarFromLandmarks, RandomIn);

            // Árvores do bioma (pinheiros, copadas, palmeiras): da borda até 8 m para fora (emolduram sem ocupar o chão).
            int pines = Mathf.RoundToInt((b.size.x + b.size.z) * 2f / 3.2f * Art.TreeDensity);
            Sprite[] trees = Art.BorderTrees;
            for (int k = 0, tries = 0; k < pines && tries < pines * 30; tries++)
            {
                Vector3 p = RandomIn(8f);
                float edge = Mathf.Min(Mathf.Min(p.x - b.min.x, b.max.x - p.x), Mathf.Min(p.z - b.min.z, b.max.z - p.z));
                if (edge > 0.5f) continue;
                if (DistanceToPath(p) < half + 2.5f || !FarFromLandmarks(p, 7f)) continue;
                Sprite tree = trees[rng.Next(trees.Length)];
                var r = Upright(tree.name, tree, p, OrderUnits - 3);
                float s = 0.8f + (float)rng.NextDouble() * 0.6f;
                r.transform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -s : s, s, s);
                k++;
            }
        }

        /// <summary>
        /// Vegetação pintada em pé (estilo da referência de mata): moita fechada de arbustos e samambaias na faixa da borda,
        /// algumas moitas e pedras com musgo soltas no chão (longe do caminho), sombra de contato em cada uma.
        /// </summary>
        void BuildPaintedProps(Bounds b, System.Random rng, float half, float area,
                               System.Func<Vector3, float, bool> farFromLandmarks, System.Func<float, Vector3> randomIn)
        {
            float Edge(Vector3 p) => Mathf.Min(Mathf.Min(p.x - b.min.x, b.max.x - p.x), Mathf.Min(p.z - b.min.z, b.max.z - p.z));
            void Put(Sprite[] set, Vector3 p, float minS, float maxS)
            {
                Sprite s = set[rng.Next(set.Length)];
                float k = minS + (float)rng.NextDouble() * (maxS - minS);
                var r = Upright(s.name, s, p, OrderUnits - 4);
                r.transform.localScale = new Vector3(rng.NextDouble() < 0.5 ? -k : k, k, k);
                var sh = Flat("Shadow", Art.Shadow, p + Vector3.up * 0.004f, new Color(0f, 0f, 0f, 0.28f), OrderDecal + 3);
                float w = s.bounds.size.x * k * 0.75f;
                sh.transform.localScale = new Vector3(w / 2.2f, w / 2.2f * 0.6f, 1f);
            }
            void Scatter(Sprite[] set, int count, float minPathGap, float maxEdge, float minS, float maxS, float grow)
            {
                if (set.Length == 0) return;
                for (int k = 0, tries = 0; k < count && tries < count * 25; tries++)
                {
                    Vector3 p = randomIn(grow);
                    if (Edge(p) > maxEdge) continue;
                    if (DistanceToPath(p) < half + minPathGap || !farFromLandmarks(p, 4.5f)) continue;
                    Put(set, p, minS, maxS);
                    k++;
                }
            }
            float perimeter = (b.size.x + b.size.z) * 2f;
            Scatter(Art.Shrubs, Mathf.RoundToInt(perimeter / 1.6f), 1.2f, 2.2f, 0.85f, 1.3f, 4f);     // moita da borda
            Scatter(Art.Shrubs, Mathf.RoundToInt(area / 70f), 1.8f, 999f, 0.6f, 0.9f, 0f);           // moitas soltas
            Scatter(Art.Boulders, Mathf.RoundToInt(perimeter / 9f * Art.RockDensity), 1.2f, 2.5f, 0.8f, 1.3f, 4f);
            Scatter(Art.Boulders, Mathf.RoundToInt(area / 140f * Art.RockDensity), 2f, 999f, 0.55f, 0.85f, 0f);
        }

        float DistanceToPath(Vector3 p)
        {
            var pts = _layout.PathPolyline;
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector3 a = pts[i], ab = pts[i + 1] - pts[i];
                ab.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(new Vector3(p.x - a.x, 0f, p.z - a.z), ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                Vector3 q = a + ab * t;
                best = Mathf.Min(best, new Vector2(p.x - q.x, p.z - q.z).magnitude);
            }
            return best;
        }

        /// <summary>Faixa contínua ao longo da polilinha, com juntas em mitra e UV ao longo do comprimento.</summary>
        static Mesh BuildRibbon(Vector3[] pts, float width, Color color)
        {
            int n = pts.Length;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var cols = new Color[n * 2];
            var tris = new int[(n - 1) * 6];
            float half = width * 0.5f, dist = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 dirIn = i > 0 ? (pts[i] - pts[i - 1]).normalized : (pts[1] - pts[0]).normalized;
                Vector3 dirOut = i < n - 1 ? (pts[i + 1] - pts[i]).normalized : dirIn;
                Vector3 tangent = (dirIn + dirOut).normalized;
                Vector3 normal = new Vector3(-tangent.z, 0, tangent.x);
                float miter = half / Mathf.Max(0.35f, Vector3.Dot(normal, new Vector3(-dirIn.z, 0, dirIn.x)));
                if (i > 0) dist += Vector3.Distance(pts[i], pts[i - 1]);
                verts[i * 2] = pts[i] + normal * miter;
                verts[i * 2 + 1] = pts[i] - normal * miter;
                uvs[i * 2] = new Vector2(dist / width, 1f);
                uvs[i * 2 + 1] = new Vector2(dist / width, 0f);
                cols[i * 2] = cols[i * 2 + 1] = color;
            }
            for (int i = 0; i < n - 1; i++)
            {
                int v = i * 2, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v + 1; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            var m = new Mesh { name = "MSH_PathRibbon", vertices = verts, uv = uvs, colors = cols, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        GameObject BuildBackdrop()
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(_root, false);
            Bounds b = _layout.CameraBounds;
            go.transform.position = new Vector3(b.center.x, -2f, b.max.z + 16f);
            go.transform.localScale = new Vector3(b.size.x * 8f, 40f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = Art.QuadUpright;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Art.BackdropMaterial;
            // Transparentes ordenam por sortingOrder antes da profundidade: o céu/montanhas precisam
            // ser desenhados depois do chão distante (a base deles fica além da área jogável).
            r.sortingOrder = OrderGround + 5;
            return go;
        }

        void BuildRunes()
        {
            var pts = _layout.PathPolyline;
            float total = 0f;
            for (int i = 1; i < pts.Length; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
            int count = Mathf.Max(1, Mathf.FloorToInt(total / 3f));
            _runes = new SpriteRenderer[count];
            for (int k = 0; k < count; k++)
            {
                float d = 1.5f + k * 3f;
                Vector3 pos = PointAlong(pts, d, out Vector3 dir);
                var r = Flat("Rune_" + k, Art.RuneGlyph, pos + Vector3.up * 0.015f, _accent, OrderRunes);
                r.transform.rotation = Quaternion.LookRotation(Vector3.down, dir);   // glifo aponta no sentido do fluxo
                _runes[k] = r;
            }
        }

        static Vector3 PointAlong(Vector3[] pts, float d, out Vector3 dir)
        {
            for (int i = 1; i < pts.Length; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]);
                dir = (pts[i] - pts[i - 1]).normalized;
                if (d <= seg) return pts[i - 1] + dir * d;
                d -= seg;
            }
            dir = Vector3.forward;
            return pts[pts.Length - 1];
        }

        SpriteRenderer Flat(string name, Sprite sprite, Vector3 pos, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.sharedMaterial = Art.SpriteLit;
            r.sortingOrder = order;
            return r;
        }

        SpriteRenderer Upright(string name, Sprite sprite, Vector3 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            go.transform.rotation = Camera != null ? Camera.transform.rotation : Quaternion.Euler(42f, 0f, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sharedMaterial = Art.SpriteLit;
            r.sortingOrder = order;
            return r;
        }

        // ================================================================== estados (zero-GC)
        public Vector3 GetPedestalPosition(int pedestalIndex) => _spotPos[pedestalIndex];

        /// <summary>Posicionamento livre: leva os visuais do ponto para a posição onde o jogador construiu.</summary>
        public void MoveSpot(int i, Vector3 pos)
        {
            pos.y = 0f;
            _spotPos[i] = pos;
            _pedestalRings[i].transform.position = pos + Vector3.up * 0.03f;
            _pedestalWarnings[i].transform.position = pos + Vector3.up * 0.04f;
            _shadows[i].transform.position = pos + Vector3.up * 0.02f;
            _pedestalUnits[i].transform.position = pos;
        }

        public void SetPedestalState(int i, PedestalVisualState state)
        {
            if (_pedestalState[i] == state) return;
            _pedestalState[i] = state;
            _pulse[i] = 0f;
        }

        public void ShowPedestalWarning(int i, bool on) => _pedestalWarnings[i].enabled = on;

        public void SetRuneState(RuneVisualState state)
        {
            _runeState = state;
        }

        /// <summary>GDD §7.6: estágio 1–3 escala a torre; o caminho dominante tinge levemente.</summary>
        public void ShowTowerStage(int i, TowerArchetype a, int path, int stage)
        {
            _shadows[i].enabled = true;
            if (_spotView[i] >= 0 && _spotViewArch[i] != (int)a) HideTowerViews(i);
            GameObject view = _spotView[i] >= 0 ? _towerPool[(int)a][_spotView[i]] : TakeTowerView(i, (int)a);
            if (view != null)
            {
                view.transform.SetPositionAndRotation(_spotPos[i], Quaternion.Euler(0f, _yaw[i], 0f));
                float k = ArtLibrary.UnitScale * (1f + 0.15f * (stage - 1));
                view.transform.localScale = Vector3.one * k;
                view.SetActive(true);
                _pedestalUnits[i].enabled = false;
                Track(i, view.transform, k, false, (int)a);
                return;
            }
            SpriteRenderer r = _pedestalUnits[i];
            r.sprite = Art.TowerIcons[(int)a];
            float s = TowerReadabilityScale * (1f + 0.15f * (stage - 1));
            r.transform.localScale = new Vector3(s, s, s);
            r.color = path == 0 ? Color.white : path == 1 ? new Color(1f, 0.93f, 0.85f) : new Color(0.9f, 0.97f, 1f);
            r.enabled = true;
            Track(i, r.transform, s, true, (int)a);
        }

        void Track(int i, Transform t, float baseScale, bool billboard, int visual)
        {
            _spotObj[i] = t;
            _spotBase[i] = baseScale;
            _spotBillboard[i] = billboard;
            _pop[i] = 1f;   // surge (ou sobe de nível) com um quique
            bool holds = visual >= 0 && visual < ArtLibrary.HoldsProjectile.Length && ArtLibrary.HoldsProjectile[visual];
            _heldVisual[i] = holds ? visual : -1;
            if (holds) _held[i].sharedMaterial = Art.ProjectileMaterials[visual];
            _held[i].enabled = holds && _reload[i] <= 0f;
            if (holds) _heldPop[i] = 1f;
        }

        /// <summary>Recuo curto ao atacar (chamado quando a recarga da unidade reinicia).</summary>
        public void Punch(int i) => _punch[i] = 1f;

        /// <summary>
        /// Arremesso: recuo + o item sai da mão (o projétil voa a partir dela, UnitRenderer) e um novo aparece
        /// depois de uma recarga de metade do intervalo de ataque (máx. 0,9 s), sempre antes do próximo disparo.
        /// </summary>
        public void Throw(int i, float attackInterval)
        {
            _punch[i] = 1f;
            if (_heldVisual[i] < 0) return;
            _reload[i] = Mathf.Min(0.9f, attackInterval * 0.5f);
            _held[i].enabled = false;
        }

        /// <summary>Gira a unidade (modelo 3D) para o alvo, como os macacos do BTD6.</summary>
        public void SetAim(int i, float2 target)
        {
            Vector3 p = _spotPos[i];
            float dx = target.x - p.x, dz = target.y - p.z;
            if (dx * dx + dz * dz > 0.01f) _yawTarget[i] = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }

        /// <summary>Sem alvo: volta devagar a olhar para a câmera.</summary>
        public void ClearAim(int i) => _yawTarget[i] = 180f;

        public void ShowHero(int i) => ShowHero(i, 0);

        /// <summary>Troca a pose do herói 3D (sem efeito se ele não tem poses). Ver HeroPoseView.</summary>
        public void PlayHeroPose(int hero, HeroPose pose, float seconds)
        {
            if (_heroPoses == null || hero < 0 || hero >= _heroPoses.Length) return;
            HeroPoseView v = _heroPoses[hero];
            if (v != null && v.gameObject.activeSelf) v.Play(pose, seconds);
        }

        /// <param name="i">Pedestal.</param>
        /// <param name="hero">Índice no loadout (GameEvent.A de HeroSummoned).</param>
        public void ShowHero(int i, int hero)
        {
            GameObject view = _heroViews != null && hero >= 0 && hero < _heroViews.Length ? _heroViews[hero] : null;
            if (view != null)
            {
                // Modelo com a frente em +Z (Tools/normalize_glb.py); a câmera olha para +Z → gira 180°.
                view.transform.SetPositionAndRotation(_spotPos[i], Quaternion.Euler(0f, _yaw[i], 0f));
                view.transform.localScale = Vector3.one * ArtLibrary.UnitScale;
                view.SetActive(true);
                _pedestalUnits[i].enabled = false;
                _shadows[i].enabled = true;
                Track(i, view.transform, ArtLibrary.UnitScale, false, 4);
                return;
            }
            _shadows[i].enabled = true;
            SpriteRenderer r = _pedestalUnits[i];
            r.sprite = Art.HeroIcon;
            r.transform.localScale = Vector3.one * TowerReadabilityScale;
            r.color = Color.white;
            r.enabled = true;
            Track(i, r.transform, TowerReadabilityScale, true, 4);
        }

        public void SetUnitDimmed(int i, bool dimmed)
        {
            SpriteRenderer r = _pedestalUnits[i];
            if (!r.enabled) return;
            Color c = r.color;
            c.a = dimmed ? 0.45f : 1f;
            r.color = c;
        }

        const int TowerArchetypeCount = 4;

        GameObject TakeTowerView(int spot, int a)
        {
            if (_towerPool == null) return null;
            bool[] used = _towerPoolUsed[a];
            for (int k = 0; k < used.Length; k++)
            {
                if (used[k]) continue;
                used[k] = true;
                _spotView[spot] = k;
                _spotViewArch[spot] = a;
                return _towerPool[a][k];
            }
            return null;   // estoque esgotado: usa o sprite provisório
        }

        void HideTowerViews(int i)
        {
            if (_spotView == null || _spotView[i] < 0) return;
            int a = _spotViewArch[i], k = _spotView[i];
            _towerPool[a][k].SetActive(false);
            _towerPoolUsed[a][k] = false;
            _spotView[i] = -1;
        }

        public void ClearPedestal(int i)
        {
            HideTowerViews(i);
            _shadows[i].enabled = false;
            _spotObj[i] = null;
            _yaw[i] = _yawTarget[i] = 180f;
            _held[i].enabled = false;
            _heldVisual[i] = -1;
            _reload[i] = 0f;
            _pedestalUnits[i].enabled = false;
            _pedestalUnits[i].transform.localScale = Vector3.one;
        }

        public async Awaitable PlayLairOpenAsync(CancellationToken ct)
        {
            if (_lair == null) return;
            float t = 0f;
            Vector3 baseScale = _lair.transform.localScale;
            while (t < 2f)
            {
                t += Time.deltaTime;
                float k = 1f + 0.12f * Mathf.Sin(t * Mathf.PI);
                _lair.transform.localScale = baseScale * k;
                await Awaitable.NextFrameAsync(ct);
            }
            _lair.transform.localScale = baseScale;
        }

        public async Awaitable PlayRescueAsync(HeroDataSO hero, CancellationToken ct)
        {
            await Awaitable.WaitForSecondsAsync(4f, ct);   // cinemática de resgate (Bíblia §3.6) — placeholder
        }

        /// <summary>
        /// Bíblia §3.3: a neblina só dá profundidade às bordas e nunca cobre a área jogável.
        /// Os valores do tema (40–90 un) são relativos ao fim da área jogável, não à câmera.
        /// </summary>
        public void FitFogToCamera(float cameraDistance)
        {
            if (_theme == null) return;
            float start = cameraDistance * 1.3f;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = start + (_theme.FogEnd - _theme.FogStart) * 1.5f;
        }

        public void ApplyQualityTier(QualityTier tier)
        {
            _tier = tier;
            if (_mainLight != null) _mainLight.shadows = tier == QualityTier.High ? LightShadows.Soft : LightShadows.None;
            if (_backdrop != null) _backdrop.SetActive(true);
        }

        void Update()
        {
            if (_pedestalState == null) return;
            float dt = Time.deltaTime;
            _time += dt;

            // Anéis dos pedestais (Bíblia §3.4)
            for (int i = 0; i < _pedestalState.Length; i++)
            {
                SpriteRenderer ring = _pedestalRings[i];
                Color c;
                switch (_pedestalState[i])
                {
                    case PedestalVisualState.Highlighted:
                        c = ArtLibrary.PedestalFree; c.a = 0.45f + 0.45f * Mathf.Sin(_time * Mathf.PI * 2f / 1.5f); break;
                    case PedestalVisualState.Selected:
                        c = Color.white; ring.transform.Rotate(0f, 0f, 60f * dt, Space.Self); break;
                    case PedestalVisualState.Occupied:
                        c = Color.clear; break;   // BTD6: nada embaixo da unidade além da sombra
                    case PedestalVisualState.UpgradeAvailable:
                        c = ArtLibrary.UpgradeGold; c.a = 0.25f + 0.25f * Mathf.Sin(_time * 4f); break;
                    case PedestalVisualState.Disabled:
                    case PedestalVisualState.Locked:
                        c = ArtLibrary.LockedGrey; c.a = 0.6f; break;
                    default:
                        c = Color.clear; break;
                }
                ring.color = c;
                if (_pedestalWarnings[i].enabled)
                {
                    float k = RingScale * (1f + 0.1f * Mathf.Sin(_time * 12f));
                    _pedestalWarnings[i].transform.localScale = new Vector3(k, k, k);
                }
            }

            // Movimento das unidades: quique ao surgir, respiração, recuo ao atacar e giro até o alvo
            for (int i = 0; i < _spotObj.Length; i++)
            {
                Transform t = _spotObj[i];
                if (t == null) continue;
                _pop[i] = Mathf.Max(0f, _pop[i] - dt * 3.5f);
                _punch[i] = Mathf.Max(0f, _punch[i] - dt * 7f);
                float pop = _pop[i], punch = _punch[i];
                float bounce = 1f + 0.35f * pop * Mathf.Cos((1f - pop) * 9f);              // mola amortecida
                float breathe = 1f + 0.025f * Mathf.Sin(_time * 2.3f + i * 1.7f);
                float k = _spotBase[i] * bounce;
                float sy = k * breathe * (1f - 0.14f * punch);                              // amassa no disparo
                float sxz = k * (1f + 0.09f * punch);
                t.localScale = new Vector3(sxz, sy, sxz);
                if (!_spotBillboard[i])
                {
                    _yaw[i] = Mathf.MoveTowardsAngle(_yaw[i], _yawTarget[i], 540f * dt);
                    t.rotation = Quaternion.Euler(-6f * punch, _yaw[i], 0f);                // tranco para trás
                }
                UpdateHeld(i, dt, breathe, bounce);
            }

            UpdateSkillThrow(dt);

            // Runas (Bíblia §3.5), discretas sobre as lajotas: apagadas entre ondas · 35% em onda · vermelho no chefe
            Color rune = _accent;
            float alpha = 0f;
            if (_runeState == RuneVisualState.WaveActive) alpha = 0.35f;
            else if (_runeState == RuneVisualState.BossWave) { rune = ArtLibrary.BossRed; alpha = 0.55f + 0.35f * Mathf.Sin(_time * 5f); }
            rune.a = alpha;
            for (int k = 0; k < _runes.Length; k++)
            {
                // UV scroll simulado: onda de brilho correndo no sentido do fluxo
                float wave = 0.75f + 0.25f * Mathf.Sin(k * 0.9f - _time * 3f);
                Color c = rune;
                c.a *= wave;
                _runes[k].color = c;
            }
        }

        /// <summary>
        /// Habilidade do herói: gira para o ponto, solta a lança em chamas da mão (some da mão e recarrega) e ela
        /// voa em arco até o chão em <see cref="SkillThrowTime"/>.
        /// </summary>
        public void PlaySkillThrow(int spot, Vector3 target)
        {
            target.y = 0f;
            Vector3 p = _spotPos[spot];
            float dx = target.x - p.x, dz = target.z - p.z;
            if (dx * dx + dz * dz > 0.01f) _yawTarget[spot] = _yaw[spot] = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            float yaw = _yaw[spot] * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)), right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 hand = ArtLibrary.HandLocal[4];
            _throwFrom = p + right * hand.x + fwd * hand.z + Vector3.up * hand.y;
            _throwTo = target;
            _throwT = 0f;
            _skillThrow.enabled = true;
            Throw(spot, 1.6f);
        }

        void UpdateSkillThrow(float dt)
        {
            if (_throwT >= 1f) return;
            _throwT = Mathf.Min(1f, _throwT + dt / SkillThrowTime);
            float u = _throwT;
            float span = Vector3.Distance(new Vector3(_throwFrom.x, 0f, _throwFrom.z), _throwTo);
            float arc = 0.25f * span;
            Vector3 pos = Vector3.Lerp(_throwFrom, _throwTo, u) + Vector3.up * (4f * arc * u * (1f - u));
            Vector3 vel = (_throwTo - _throwFrom) + Vector3.up * (4f * arc * (1f - 2f * u));
            Vector3 dir = vel.sqrMagnitude > 1e-6f ? vel.normalized : Vector3.forward;
            Quaternion camRot = Camera != null ? Camera.transform.rotation : Quaternion.Euler(55f, 0f, 0f);
            Vector3 camF = camRot * Vector3.forward;
            Vector3 f = camF - Vector3.Dot(camF, dir) * dir;
            if (f.sqrMagnitude < 1e-6f) f = Vector3.up;
            f.Normalize();
            _skillThrow.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(f, Vector3.Cross(f, dir)));
            _skillThrow.transform.localScale = new Vector3(ArtLibrary.ProjectileLength[4] * 1.2f, ArtLibrary.ProjectileThickness[4] * 1.2f, 1f);
            if (_throwT >= 1f) _skillThrow.enabled = false;   // caiu: o chão já está pegando fogo
        }

        /// <summary>Item na mão: segue o giro da unidade; some no arremesso e reaparece após a recarga.</summary>
        void UpdateHeld(int i, float dt, float breathe, float bounce)
        {
            int v = _heldVisual[i];
            if (v < 0) return;
            MeshRenderer held = _held[i];
            if (_reload[i] > 0f)
            {
                _reload[i] -= dt;
                if (_reload[i] > 0f) return;
                held.enabled = true;   // recarregou: novo item na mão
                _heldPop[i] = 1f;
            }
            _heldPop[i] = Mathf.Max(0f, _heldPop[i] - dt * 4f);

            float yaw = (_spotBillboard[i] ? 180f : _yaw[i]) * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 hand = ArtLibrary.HandLocal[v];
            float grow = _spotBase[i] / ArtLibrary.UnitScale * bounce;   // acompanha nível e quique da unidade
            Vector3 pos = _spotPos[i] + (right * hand.x + fwd * hand.z) * grow + Vector3.up * (hand.y * grow * breathe);
            float pop = _heldPop[i];
            float k = 1f - 0.6f * pop * pop;   // cresce de 40% até 100% ao reaparecer
            Quaternion camRot = Camera != null ? Camera.transform.rotation : Quaternion.Euler(55f, 0f, 0f);
            Quaternion rot;
            if (ArtLibrary.ProjectileOriented[v])
            {
                // lança erguida, apontando para o alvo e um pouco para cima (pronta para o arremesso)
                Vector3 dir = (fwd + Vector3.up * 0.45f).normalized;
                Vector3 camF = camRot * Vector3.forward;
                Vector3 f = camF - Vector3.Dot(camF, dir) * dir;
                if (f.sqrMagnitude < 1e-6f) f = Vector3.up;
                f.Normalize();
                rot = Quaternion.LookRotation(f, Vector3.Cross(f, dir));
            }
            else rot = camRot * Quaternion.Euler(0f, 0f, _time * 40f + i * 37f);
            held.transform.SetPositionAndRotation(pos, rot);
            held.transform.localScale = new Vector3(ArtLibrary.ProjectileLength[v] * k, ArtLibrary.ProjectileThickness[v] * k, 1f);
        }

        // ================================================================== fotos (carregamento)
        /// <summary>
        /// Fotografa cada torre 3D e cada herói num "estúdio" fora do mapa e guarda em RenderTextures pequenas
        /// (256²), usadas como retrato na loja e no painel. Chamado uma vez, depois de LoadHeroViewsAsync.
        /// Sem modelo 3D, fica null e o HUD usa o desenho provisório.
        /// </summary>
        public void BuildPortraits()
        {
            TowerPortraits = new Texture[TowerArchetypeCount];
            HeroPortraits = new Texture[_heroViews != null ? _heroViews.Length : 0];
            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(_root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = PortraitBackground;
            cam.fieldOfView = 20f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 120f;
            for (int a = 0; a < TowerArchetypeCount; a++)
                if (_towerPool != null && _towerPool[a].Length > 0)
                    TowerPortraits[a] = Snap(cam, _towerPool[a][0], false);
            for (int h = 0; h < HeroPortraits.Length; h++)
                if (_heroViews[h] != null) HeroPortraits[h] = Snap(cam, _heroViews[h], true);
            Destroy(camGo);
        }

        Texture Snap(Camera cam, GameObject go, bool face)
        {
            Transform t = go.transform;
            bool wasActive = go.activeSelf;
            Vector3 pos = t.position;
            Quaternion rot = t.rotation;
            Vector3 scale = t.localScale;
            t.SetPositionAndRotation(PortraitStudio, Quaternion.Euler(0f, 160f, 0f));   // três-quartos
            t.localScale = Vector3.one * ArtLibrary.UnitScale;
            go.SetActive(true);
            RenderTexture rt = null;
            var renderers = go.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int k = 1; k < renderers.Length; k++) b.Encapsulate(renderers[k].bounds);
                // Herói: cabeça e ombros (BTD6). Torre: corpo inteiro.
                float span = face ? b.size.y * 0.62f : Mathf.Max(b.size.y, Mathf.Max(b.size.x, b.size.z)) * 1.08f;
                Vector3 focus = new Vector3(b.center.x, face ? b.min.y + b.size.y * 0.7f : b.center.y, b.center.z);
                float dist = span * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) + b.extents.z;
                cam.transform.position = focus + new Vector3(0f, span * 0.3f, -dist);
                cam.transform.LookAt(focus);
                rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { name = "RT_Portrait_" + go.name, antiAliasing = 4 };
                rt.Create();
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                _portraitTargets.Add(rt);
            }
            t.SetPositionAndRotation(pos, rot);
            t.localScale = scale;
            go.SetActive(wasActive);
            return rt;
        }

        public void Unload()
        {
            for (int i = 0; i < _portraitTargets.Count; i++)
                if (_portraitTargets[i] != null) { _portraitTargets[i].Release(); Destroy(_portraitTargets[i]); }
            _portraitTargets.Clear();
            TowerPortraits = null;
            HeroPortraits = null;
            for (int i = 0; i < _handles.Count; i++)
                if (_handles[i].IsValid()) Addressables.ReleaseInstance(_handles[i]);
            _handles.Clear();
            if (_root != null) Destroy(_root.gameObject);
            _root = null;
            _heroViews = null;
            _towerPool = null;
            _towerPoolUsed = null;
            _spotView = null;
            _spotObj = null;
            _heroPoses = null;
            Art?.Dispose();
            Art = null;
            _pedestalState = null;
        }

        void OnDestroy() => Unload();
    }
}
