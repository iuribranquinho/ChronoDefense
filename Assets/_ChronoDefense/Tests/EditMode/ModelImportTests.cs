// Valida os modelos 3D da Era 1 depois de Tools ▸ Chrono ▸ Importar modelos 3D (Era 1).
// Modelos ainda não gerados (.glb ausente) ficam como Ignored, não como falha.
using System.IO;
using ChronoDefense.Editor;
using NUnit.Framework;

namespace ChronoDefense.Tests
{
    public class ModelImportTests
    {
        static ModelPrefabBuilder.Entry[] Entries => ModelPrefabBuilder.Entries;

        [TestCaseSource(nameof(Entries))]
        public void Model_IsNormalizedAndLinked(ModelPrefabBuilder.Entry e)
        {
            if (!File.Exists(ModelPrefabBuilder.MainGlb(e))) Assert.Ignore($"{e.Name}: .glb ainda não gerado (Tools/gerar_modelos_e1.ps1)");
            string problem = ModelPrefabBuilder.Validate(e);
            Assert.IsNull(problem, $"{e.Name}: {problem}");
        }
    }
}
