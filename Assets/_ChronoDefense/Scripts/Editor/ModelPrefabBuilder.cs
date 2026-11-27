// Importa os modelos 3D gerados por Tools/gerar_modelos_e1.ps1 (.glb via glTFast) e liga aos dados do MVP:
//  - material URP/Lit por modelo (textura base do glTF → _BaseMap; GPU instancing ligado)
//  - prefab PFB_<nome>.prefab ao lado do .glb
//  - herói: HeroDataSO.ViewPrefab (Addressables) → LevelRenderer.ShowHero. Se existirem <nome>_Idle.glb,
//    _Attack, _Skill, _Move e _Victory (Tools/split_poses.py), o prefab ganha um filho por pose + HeroPoseView
//  - material com as duas faces visíveis: modelos do Tripo têm cascas abertas (camadas de pelo)
//  - chefe e creeps: EnemyDataSO.VatMesh/VatMaterial → UnitRenderer (RenderMeshInstanced)
//  - torres: THM_E1_Tundra.TowerSkins[arquétipo].BaseL1 → LevelRenderer.ShowTowerStage
//  - cenário: pedestal → tema.Pedestal · caverna → tema.Portal e BossDataSO.LairPrefab · Núcleo → tema.Core
//  - props: só o prefab (espalhar props no mapa é a Fase C do Referencia_BTD6.md)
// Lista de modelos e prompts do Tripo: Tools/tripo/modelos_era1.json (mesmos caminhos e medidas daqui).
// Menu: Tools ▸ Chrono ▸ Importar modelos 3D (Era 1). Batchmode: -executeMethod ChronoDefense.Editor.ModelPrefabBuilder.Run
// Rodar de novo depois de Tools ▸ Chrono ▸ Configurar projeto se o gerador recriar os ScriptableObjects.
using System.IO;
using System.Text;
using ChronoDefense.Data;
using ChronoDefense.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ChronoDefense.Editor
{
    public static class ModelPrefabBuilder
    {
        public enum Kind { Hero, Boss, Creep, Tower, Pedestal, Core, Lair, Prop }

        public readonly struct Entry
        {
            public readonly string Glb, Data;
            public readonly Kind Kind;
            public readonly float Height;   // metros (altura); 0 = medido pela largura. Unidades são ampliadas no jogo
                                            // por ArtLibrary.UnitScale; cenário entra em tamanho real.
            public readonly float Width;    // metros (maior medida horizontal), para cenário
            public readonly int Archetype;  // torres: índice de TowerArchetype
            public Entry(string glb, string data, Kind kind, float height, float width = 0f, int archetype = -1)
            { Glb = glb; Data = data; Kind = kind; Height = height; Width = width; Archetype = archetype; }
            public string Name => Path.GetFileNameWithoutExtension(Glb);
            public override string ToString() => Name;   // nome do caso no Test Runner
            public string Dir => Path.GetDirectoryName(Glb).Replace('\\', '/');
            public string MaterialPath => $"{Dir}/MAT_{Name}.mat";
            public string PrefabPath => $"{Dir}/PFB_{Name}.prefab";
        }

        const string Art = "Assets/_ChronoDefense/Art";
        const string E1 = "Assets/_ChronoDefense/Data/E1";
        const string Theme = E1 + "/THM_E1_Tundra.asset";
        const string Env = Art + "/Environment/E1_Tundra";
        const string Boss10 = E1 + "/Bosses/BOS_010_MammothAlpha.asset";
        const string LitShader = "Universal Render Pipeline/Lit";

        public static readonly Entry[] Entries =
        {
            new Entry($"{Art}/Heroes/HER_01_Krog/HER_01_Krog.glb", $"{E1}/Heroes/HER_01_Krog.asset", Kind.Hero, 1.8f),
            new Entry($"{Art}/Bosses/BOS_010_MammothAlpha.glb", Boss10, Kind.Boss, 2.4f),
            new Entry($"{Art}/Creeps/E1/CRP_E1_GreyWolf.glb", $"{E1}/Enemies/CRP_E1_GreyWolf.asset", Kind.Creep, 0.9f),
            new Entry($"{Art}/Creeps/E1/CRP_E1_SabreTiger.glb", $"{E1}/Enemies/CRP_E1_SabreTiger.asset", Kind.Creep, 1.0f),
            new Entry($"{Art}/Creeps/E1/CRP_E1_WoollyRhino.glb", $"{E1}/Enemies/CRP_E1_WoollyRhino.asset", Kind.Creep, 1.3f),
            new Entry($"{Art}/Towers/E1/TWR_E1_Marksman.glb", Theme, Kind.Tower, 1.8f, archetype: (int)TowerArchetype.Marksman),
            new Entry($"{Art}/Towers/E1/TWR_E1_Launcher.glb", Theme, Kind.Tower, 1.8f, archetype: (int)TowerArchetype.Launcher),
            new Entry($"{Art}/Towers/E1/TWR_E1_Alchemist.glb", Theme, Kind.Tower, 1.8f, archetype: (int)TowerArchetype.Alchemist),
            new Entry($"{Art}/Towers/E1/TWR_E1_Guard.glb", Theme, Kind.Tower, 1.8f, archetype: (int)TowerArchetype.Guard),
            new Entry($"{Env}/ENV_E1_Pedestal.glb", Theme, Kind.Pedestal, 0f, width: 2.8f),
            new Entry($"{Env}/ENV_E1_KrogCave.glb", Boss10, Kind.Lair, 0f, width: 7.0f),
            new Entry($"{Env}/ENV_E1_ChronoCore.glb", Theme, Kind.Core, 3.0f),
            new Entry($"{Env}/ENV_E1_MammothSkeleton.glb", "", Kind.Prop, 0f, width: 7.0f),
            new Entry($"{Env}/ENV_E1_Campfire.glb", "", Kind.Prop, 0f, width: 1.6f),
            new Entry($"{Env}/ENV_E1_SnowyPine.glb", "", Kind.Prop, 7.0f),
            new Entry($"{Env}/ENV_E1_Rock.glb", "", Kind.Prop, 0f, width: 2.5f),
            new Entry($"{Env}/ENV_E1_IceBlock.glb", "", Kind.Prop, 0f, width: 3.0f),
        };

        /// <summary>Arquivo de uma pose do herói (ordem de HeroPose).</summary>
        public static string PoseGlb(Entry e, HeroPose pose) => $"{e.Dir}/{e.Name}_{pose}.glb";
        public static bool HasPoses(Entry e) => e.Kind == Kind.Hero && File.Exists(PoseGlb(e, HeroPose.Idle));
        /// <summary>O .glb que define escala e textura: a pose Idle, se houver poses; senão o modelo único.</summary>
        public static string MainGlb(Entry e) => HasPoses(e) ? PoseGlb(e, HeroPose.Idle) : e.Glb;

        [MenuItem("Tools/Chrono/Importar modelos 3D (Era 1)")]
        public static void Run()
        {
            var log = new StringBuilder("ModelPrefabBuilder:\n");
            foreach (Entry e in Entries)
            {
                if (!File.Exists(MainGlb(e))) { log.AppendLine($"  — {e.Name}: .glb não encontrado (pulado)"); continue; }
                log.AppendLine($"  {e.Name}: {Build(e)}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        /// <summary>Malha e textura base de um .glb importado pelo glTFast (null se não importou).</summary>
        static Mesh LoadMesh(string glb, out Texture baseMap)
        {
            baseMap = null;
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(glb);
            var mf = imported != null ? imported.GetComponentInChildren<MeshFilter>(true) : null;
            if (mf == null || mf.sharedMesh == null) return null;
            var r = mf.GetComponent<Renderer>();
            baseMap = r != null ? FindBaseTexture(r.sharedMaterial) : null;
            return mf.sharedMesh;
        }

        static string Build(Entry e)
        {
            Mesh mesh = LoadMesh(MainGlb(e), out Texture baseMap);
            if (mesh == null) return "ERRO: o Unity não importou o .glb ou ele não tem malha (o pacote com.unity.cloud.gltfast está instalado?)";

            // Material URP/Lit (o URP/Lit ignora cor de vértice: a cor tem de vir de textura, ver normalize_glb.py)
            var mat = AssetDatabase.LoadAssetAtPath<Material>(e.MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find(LitShader)) { name = "MAT_" + e.Name };
                AssetDatabase.CreateAsset(mat, e.MaterialPath);
            }
            mat.shader = Shader.Find(LitShader);
            mat.enableInstancing = true;   // creeps: Graphics.RenderMeshInstanced (GDD §3.5)
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", baseMap != null ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);   // Render Face: Both
            mat.doubleSidedGI = true;
            EditorUtility.SetDirty(mat);

            // Prefab: malha já normalizada (pivô na base, frente em +Z). Com poses: um filho por pose,
            // todos com o mesmo material (a textura do Tripo é um atlas único para as 5 poses).
            var go = new GameObject("PFB_" + e.Name);
            int poses = 0;
            if (HasPoses(e))
            {
                var view = go.AddComponent<HeroPoseView>();
                foreach (HeroPose pose in System.Enum.GetValues(typeof(HeroPose)))
                {
                    Mesh pm = LoadMesh(PoseGlb(e, pose), out _);
                    if (pm == null) continue;
                    var child = new GameObject(pose.ToString());
                    child.transform.SetParent(go.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = pm;
                    child.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    child.SetActive(pose == HeroPose.Idle);
                    view.Poses[(int)pose] = child;
                    poses++;
                }
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, e.PrefabPath);
            Object.DestroyImmediate(go);

            string link = Link(e, prefab, mesh, mat);
            string check = Validate(e);
            return $"{mesh.triangles.Length / 3} tris, textura {(baseMap != null ? "sim" : "NÃO")}, " +
                   (poses > 0 ? $"{poses} poses, " : "") + link +
                   (check != null ? $" — AVISO: {check}" : " — OK");
        }

        static string Link(Entry e, GameObject prefab, Mesh mesh, Material mat)
        {
            switch (e.Kind)
            {
                case Kind.Hero:
                {
                    var hero = AssetDatabase.LoadAssetAtPath<HeroDataSO>(e.Data);
                    if (hero == null) return "ERRO: HeroDataSO não encontrado";
                    hero.ViewPrefab = MakeAddressable(prefab, e.Name);
                    EditorUtility.SetDirty(hero);
                    return "HeroDataSO.ViewPrefab";
                }
                case Kind.Boss:
                {
                    var boss = AssetDatabase.LoadAssetAtPath<BossDataSO>(e.Data);
                    if (boss == null) return "ERRO: BossDataSO não encontrado";
                    boss.VatMesh = mesh;          // o chefe anda pelo caminho: desenhado pelo UnitRenderer
                    boss.VatMaterial = mat;
                    boss.ViewPrefab = MakeAddressable(prefab, e.Name);   // para cinemáticas futuras
                    EditorUtility.SetDirty(boss);
                    return "BossDataSO.VatMesh/VatMaterial + ViewPrefab";
                }
                case Kind.Creep:
                {
                    var enemy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(e.Data);
                    if (enemy == null) return "ERRO: EnemyDataSO não encontrado";
                    enemy.VatMesh = mesh;
                    enemy.VatMaterial = mat;
                    EditorUtility.SetDirty(enemy);
                    return "EnemyDataSO.VatMesh/VatMaterial";
                }
                case Kind.Lair:
                {
                    var boss = AssetDatabase.LoadAssetAtPath<BossDataSO>(e.Data);
                    var theme = AssetDatabase.LoadAssetAtPath<EraBiomeThemeSO>(Theme);
                    if (boss == null || theme == null) return "ERRO: BossDataSO ou tema não encontrado";
                    var r = MakeAddressable(prefab, e.Name);
                    boss.LairPrefab = r;          // fase de chefe
                    theme.Portal = r;             // fases comuns: a mesma caverna (Covil do Krog)
                    EditorUtility.SetDirty(boss);
                    EditorUtility.SetDirty(theme);
                    return "BossDataSO.LairPrefab + tema.Portal";
                }
                case Kind.Tower:
                case Kind.Pedestal:
                case Kind.Core:
                {
                    var theme = AssetDatabase.LoadAssetAtPath<EraBiomeThemeSO>(Theme);
                    if (theme == null) return "ERRO: tema THM_E1_Tundra não encontrado (rode Gerar dados do MVP)";
                    var r = MakeAddressable(prefab, e.Name);
                    if (e.Kind == Kind.Pedestal) theme.Pedestal = r;
                    else if (e.Kind == Kind.Core) theme.Core = r;
                    else theme.TowerSkins[e.Archetype].BaseL1 = r;
                    EditorUtility.SetDirty(theme);
                    return e.Kind == Kind.Tower ? $"tema.TowerSkins[{(TowerArchetype)e.Archetype}].BaseL1" : $"tema.{e.Kind}";
                }
                default:
                    return "prefab (props ainda não são espalhados no mapa: Fase C)";
            }
        }

        static AssetReferenceGameObject MakeAddressable(GameObject prefab, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = address;
            return new AssetReferenceGameObject(guid);
        }

        static Texture FindBaseTexture(Material m)
        {
            if (m == null) return null;
            // glTFast (shader graphs glTF) usa baseColorTexture; os demais cobrem URP/Built-in.
            foreach (string p in new[] { "baseColorTexture", "_BaseMap", "_MainTex", "_BaseColorMap" })
                if (m.HasProperty(p) && m.GetTexture(p) != null) return m.GetTexture(p);
            return null;
        }

        /// <summary>Verifica pivô, altura e ligação aos dados. null = tudo certo. Usado pelos testes EditMode.</summary>
        public static string Validate(Entry e)
        {
            Mesh mesh = LoadMesh(MainGlb(e), out _);
            if (mesh == null) return "malha não importada";
            Bounds b = mesh.bounds;
            if (Mathf.Abs(b.min.y) > 0.01f) return $"pivô fora da base (min.y = {b.min.y:0.000})";
            if (e.Height > 0f && Mathf.Abs(b.size.y - e.Height) > e.Height * 0.02f) return $"altura {b.size.y:0.00} m, esperado {e.Height:0.00} m";
            float w = Mathf.Max(b.size.x, b.size.z);
            if (e.Width > 0f && Mathf.Abs(w - e.Width) > e.Width * 0.02f) return $"largura {w:0.00} m, esperado {e.Width:0.00} m";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(e.MaterialPath);
            if (mat == null || mat.shader == null || mat.shader.name != LitShader) return $"material sem {LitShader}";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.PrefabPath);
            if (prefab == null) return "prefab não criado";
            if (HasPoses(e))
            {
                var view = prefab.GetComponent<HeroPoseView>();
                if (view == null || view.Poses.Length < 5 || view.Poses[(int)HeroPose.Idle] == null) return "prefab sem HeroPoseView/poses";
                foreach (HeroPose pose in System.Enum.GetValues(typeof(HeroPose)))
                    if (view.Poses[(int)pose] == null) return $"pose {pose} ausente ({PoseGlb(e, pose)})";
            }

            switch (e.Kind)
            {
                case Kind.Hero:
                    var hero = AssetDatabase.LoadAssetAtPath<HeroDataSO>(e.Data);
                    if (hero == null || hero.ViewPrefab == null || !hero.ViewPrefab.RuntimeKeyIsValid()) return "HeroDataSO.ViewPrefab vazio";
                    break;
                case Kind.Boss:
                case Kind.Creep:
                    var enemy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(e.Data);
                    if (enemy == null || enemy.VatMesh != mesh || enemy.VatMaterial != mat) return "EnemyDataSO.VatMesh/VatMaterial não ligados";
                    break;
                case Kind.Lair:
                    var boss = AssetDatabase.LoadAssetAtPath<BossDataSO>(e.Data);
                    if (boss == null || boss.LairPrefab == null || !boss.LairPrefab.RuntimeKeyIsValid()) return "BossDataSO.LairPrefab vazio";
                    break;
                case Kind.Tower:
                case Kind.Pedestal:
                case Kind.Core:
                    var theme = AssetDatabase.LoadAssetAtPath<EraBiomeThemeSO>(Theme);
                    AssetReferenceGameObject r = theme == null ? null
                        : e.Kind == Kind.Pedestal ? theme.Pedestal : e.Kind == Kind.Core ? theme.Core : theme.TowerSkins[e.Archetype].BaseL1;
                    if (r == null || !r.RuntimeKeyIsValid()) return $"tema sem ligação para {e.Name}";
                    break;
            }
            return null;
        }
    }
}
