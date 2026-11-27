// Importa a arte de interface pintada (Assets/_ChronoDefense/Resources/Sprites/UI/**) como Sprite: tamanho original
// (sem reescala para potência de 2, que distorceria a proporção), sem mipmaps, transparência preservada.
// Menu: Tools ▸ Chrono ▸ Reimportar arte da interface (use se as imagens entraram antes deste script compilar).
using UnityEditor;
using UnityEngine;

namespace ChronoDefense.Editor
{
    public sealed class UiArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/_ChronoDefense/Resources/Sprites/UI";
        public const string EnvFolder = "Assets/_ChronoDefense/Resources/Sprites/Environment";

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (path.StartsWith(EnvFolder)) { PreprocessEnvironment(path); return; }
            if (!path.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        /// <summary>
        /// Vegetação pintada por bioma: textura comum (o ArtLibrary cria os Sprites), mipmaps ligados (vista de longe),
        /// chão e caminho repetem; props presos na borda (sem sangrar).
        /// </summary>
        void PreprocessEnvironment(string path)
        {
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            bool tile = file == "ENV_Ground" || file == "ENV_Path";
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = !tile || file == "ENV_Path";
            ti.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        [MenuItem("Tools/Chrono/Reimportar arte da interface")]
        public static void ReimportAll()
        {
            if (AssetDatabase.IsValidFolder(EnvFolder))
                foreach (string g in AssetDatabase.FindAssets("t:Texture2D", new[] { EnvFolder }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
            if (!AssetDatabase.IsValidFolder(Folder)) { Debug.LogWarning("[UI] Pasta não encontrada: " + Folder); return; }
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Folder });
            foreach (string g in guids) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
            Debug.Log($"[UI] {guids.Length} imagens da interface reimportadas como Sprite.");
        }
    }
}
