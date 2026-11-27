// Configuração do projeto (rodável em batchmode):
//  1. URP: cria URP_Low/Medium/High em Settings/URP e ativa o Medium como pipeline padrão.
//  2. TextMeshPro: importa os TMP Essential Resources (fonte padrão do HUD).
//  3. Gera os dados do MVP e cria a cena Gameplay (adicionada ao Build Settings).
// Menu: Tools ▸ Chrono ▸ Configurar projeto (URP + TMP + cena)
using System.IO;
using ChronoDefense.Core;
using ChronoDefense.Data;
using ChronoDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ChronoDefense.Editor
{
    public static class ProjectSetup
    {
        const string UrpFolder = "Assets/_ChronoDefense/Settings/URP";
        const string ScenePath = "Assets/_ChronoDefense/Scenes/Gameplay.unity";
        const string MenuScenePath = "Assets/_ChronoDefense/Scenes/MainMenu.unity";
        const string DataRoot = "Assets/_ChronoDefense/Data";

        [MenuItem("Tools/Chrono/Configurar projeto (URP + TMP + cena)")]
        public static void SetupAll()
        {
            SetupUrp();
            ImportTmpEssentials();
            MvpDataGenerator.Generate();
            CreateGameplayScene();
            CreateMainMenuScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Projeto configurado.");
        }

        [MenuItem("Tools/Chrono/Configurar URP")]
        public static void SetupUrp()
        {
            EnsureFolder(UrpFolder);
            var low = CreateUrpAsset("URP_Low", msaa: 1, hdr: false, shadowDistance: 0f, renderScale: 0.85f);
            var medium = CreateUrpAsset("URP_Medium", msaa: 2, hdr: false, shadowDistance: 0f, renderScale: 1f);
            var high = CreateUrpAsset("URP_High", msaa: 4, hdr: true, shadowDistance: 30f, renderScale: 1f);

            GraphicsSettings.defaultRenderPipeline = medium;
            // Cada nível de qualidade usa o asset correspondente (Low/Medium/High por nome; demais → Medium).
            string[] names = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                string n = names[i].ToLowerInvariant();
                QualitySettings.renderPipeline = n.Contains("low") || n.Contains("fast") ? low
                    : n.Contains("high") || n.Contains("ultra") || n.Contains("fantastic") ? high : medium;
            }
            QualitySettings.SetQualityLevel(current, false);
            IncludeRuntimeShaders();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] URP ativo: " + medium.name);
        }

        /// <summary>Shaders obtidos por Shader.Find em runtime precisam estar em "Always Included Shaders".</summary>
        public static void IncludeRuntimeShaders()
        {
            string[] names = { ArtLibrary.SpriteLitShader, ArtLibrary.SpriteUnlitShader, ArtLibrary.UnlitShader };
            var graphics = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(graphics);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (string n in names)
            {
                Shader s = Shader.Find(n);
                if (s == null) { Debug.LogError("[Setup] Shader não encontrado: " + n); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == s) { present = true; break; }
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static UniversalRenderPipelineAsset CreateUrpAsset(string name, int msaa, bool hdr, float shadowDistance, float renderScale)
        {
            string path = $"{UrpFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (existing != null) return existing;

            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, $"{UrpFolder}/{name}_Renderer.asset");
            var asset = UniversalRenderPipelineAsset.Create(rendererData);
            asset.msaaSampleCount = msaa;
            asset.supportsHDR = hdr;
            asset.shadowDistance = shadowDistance;
            asset.renderScale = renderScale;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        [MenuItem("Tools/Chrono/Importar TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return;
            string pkg = null;
            foreach (string dir in Directory.GetDirectories("Library/PackageCache", "com.unity.ugui*"))
            {
                string candidate = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
                if (File.Exists(candidate)) { pkg = candidate; break; }
            }
            if (pkg == null) { Debug.LogError("[Setup] TMP Essential Resources.unitypackage não encontrado."); return; }
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Debug.Log("[Setup] TMP Essential Resources importados.");
        }

        [MenuItem("Tools/Chrono/Criar cena Gameplay")]
        public static void CreateGameplayScene()
        {
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<CameraRig>();
            cam.fieldOfView = CameraRig.Fov;
            camGo.transform.rotation = Quaternion.Euler(CameraRig.Pitch, 0f, 0f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, 135f, 0f);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var game = new GameObject("Game").AddComponent<GameBootstrap>();
            game.Campaign = AssetDatabase.LoadAssetAtPath<CampaignSO>($"{DataRoot}/Campaign.asset");
            game.Level = AssetDatabase.LoadAssetAtPath<LevelDataSO>($"{DataRoot}/E1/Levels/LVL_010.asset");
            var krog = AssetDatabase.LoadAssetAtPath<HeroDataSO>($"{DataRoot}/E1/Heroes/HER_01_Krog.asset");
            game.Loadout = krog != null ? new[] { krog } : new HeroDataSO[0];

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildScenes();
            Debug.Log("[Setup] Cena criada: " + ScenePath);
        }

        /// <summary>Cena MainMenu: câmera simples + EventSystem + MainMenuController (fases da Era 1 e fase de teste).</summary>
        [MenuItem("Tools/Chrono/Criar menu inicial")]
        public static void CreateMainMenuScene()
        {
            UiArtImporter.ReimportAll();   // garante a arte da interface importada como Sprite (proporção original)
            EnsureFolder(Path.GetDirectoryName(MenuScenePath).Replace('\\', '/'));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(23, 58, 99, 255);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var menu = new GameObject("MainMenu").AddComponent<MainMenuController>();
            menu.Campaign = AssetDatabase.LoadAssetAtPath<CampaignSO>($"{DataRoot}/Campaign.asset");
            menu.TestLevel = AssetDatabase.LoadAssetAtPath<LevelDataSO>($"{DataRoot}/E1/Levels/LVL_TEST_Prado.asset");
            if (menu.Campaign == null) Debug.LogWarning("[Setup] Campaign.asset não encontrado: rode Gerar dados do MVP e depois este menu de novo.");

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            UpdateBuildScenes();
            Debug.Log("[Setup] Menu inicial criado: " + MenuScenePath + ". Abra a cena e aperte Play.");
        }

        /// <summary>Build Settings: MainMenu (índice 0, abre primeiro) e Gameplay.</summary>
        static void UpdateBuildScenes()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if (File.Exists(MenuScenePath)) list.Add(new EditorBuildSettingsScene(MenuScenePath, true));
            if (File.Exists(ScenePath)) list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }
    }
}
