using ChronoDefense.Data;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChronoDefense.Tests
{
    /// <summary>
    /// Referências de conteúdo para o teste de estresse. Criado em Resources só durante o build/execução
    /// de testes (StressTestPrebuild) para funcionar também num player standalone, sem AssetDatabase.
    /// </summary>
    public sealed class StressTestConfig : ScriptableObject
    {
        public const string ResourceName = "StressTestConfig";
        public CampaignSO Campaign;
        public LevelDataSO Level;
        public HeroDataSO Hero;
    }

    public sealed class StressTestPrebuild : IPrebuildSetup, IPostBuildCleanup
    {
        const string Folder = "Assets/_ChronoDefense/Tests/PlayMode/Resources";
        const string AssetPath = Folder + "/" + StressTestConfig.ResourceName + ".asset";
        const string DataRoot = "Assets/_ChronoDefense/Data";

        public void Setup()
        {
#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.IsValidFolder(Folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_ChronoDefense/Tests/PlayMode", "Resources");
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<StressTestConfig>(AssetPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<StressTestConfig>();
                UnityEditor.AssetDatabase.CreateAsset(cfg, AssetPath);
            }
            cfg.Campaign = UnityEditor.AssetDatabase.LoadAssetAtPath<CampaignSO>($"{DataRoot}/Campaign.asset");
            cfg.Level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelDataSO>($"{DataRoot}/E1/Levels/LVL_010.asset");
            cfg.Hero = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroDataSO>($"{DataRoot}/E1/Heroes/HER_01_Krog.asset");
            UnityEditor.EditorUtility.SetDirty(cfg);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.DeleteAsset(Folder);
#endif
        }
    }
}
