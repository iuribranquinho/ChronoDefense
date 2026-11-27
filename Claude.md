# ChronoDefense — Diretrizes de Engenharia e Projeto (Unity 6 / URP)

## 📌 Documentos Oficiais de Referência
- **Sistemas, Matemática e Performance:** Siga estritamente o `GDD_Tecnico_Unity6.md`.
- **Arte, Lore, Prompts 3D e Interface:** Siga estritamente a `Biblia_do_Jogo_Refinada.md`.
- **Regra de Precedência:** Em caso de divergência numérica, o GDD Técnico se sobrepõe para código e matemática; a Bíblia de Jogo se sobrepõe para direção de arte e estilo visual.

## ⚙️ Especificações Principais do Projeto
- **Engine:** Unity 6 LTS (6000.x) com URP (Render Graph API nativa).
- **Target:** Mobile Android 10+ / iOS 15+ (60 FPS nos tiers Medium/High, 30 FPS no Low).
- **Escopo:** 5 Eras Históricas × 10 Capítulos × 10 Níveis = 500 Níveis (50 Chefes de capítulo, 22 Heróis Históricos).
- **Arquitetura:** Orientada a dados (Data-Driven via ScriptableObjects e C# Jobs/Burst).

## 🛠️ Regras Estritas de Código C# & Performance Mobile

### 1. Política Zero-GC no Hot Loop (Obrigatória)
- **Zero Alocação:** NUNCA aloque memória (`new`, concatenação de strings, LINQ, boxing de enums, lambdas com captura) em `Update()`, `FixedUpdate()`, no tick de simulação (30 Hz) ou em callbacks de UI de combate.
- **Pooling Reutilizável:** 
  - `StructPool<T>` (NativeArray + free list) para simulação de Creeps e Projéteis sem GameObjects.
  - `ComponentPool<T>` para views de Heróis, Chefes, Floaty Text (dano) e Partículas (VFX).
- **Strings e Materiais:** Use `TMP_Text.SetText("{0}", valor)` e altere propriedades via `MaterialPropertyBlock` sem instanciar novos materiais.

### 2. Simulação, Pathfinding & Combat
- **Simulação Híbrida (Burst/Jobs):** Não utilize o pacote `Entities` completo. Use `NativeArray<T>` + `IJobParallelFor` com `[BurstCompile]`.
- **Flow Field 128×128:** Pathfinding via BFS reverso em grade de 128×128 células com vetores unitários 2D (`float2`). Bake no carregamento de nível; recálculo apenas em eventos de alteração de mapa.
- **Spatial Hash Grid 64×64:** Consultas de alcance de torres em O(K) usando células de 2 m.
- **Física Desligada:** `Physics.simulationMode` e `Physics2D.simulationMode` em `Script`. Toques resolvidos por raio × plano Y=0.

### 3. Shaders & Compatibilidade URP (Unity 6)
- **Sprites 2D / UI / Terreno:** `Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")` ou `Sprite-Unlit-Default`.
- **Modelos 3D (.glb / .fbx):** `Shader.Find("Universal Render Pipeline/Lit")` ou `Unlit`.
- **PROIBIDO:** Nunca use `Shader.Find("Sprites/Default")` ou `Shader.Find("Standard")` em projetos URP.

## 📁 Convenção de Nomes e Estrutura de Pastas
Namespace padrão do código: `ChronoDefense.Data`, `ChronoDefense.Systems`, `ChronoDefense.Core`.

```text
Assets/_ChronoDefense/
├── Art/
│   ├── Heroes/HER_01_Krog/        HER_01_Krog.fbx · HER_01_Krog_Albedo.png · HER_01_Krog.prefab
│   ├── Creeps/E1/                 CRP_E1_SabreTiger.fbx …
│   ├── Bosses/                    BOS_010_MammothAlpha.fbx …
│   ├── Towers/E1/                 TWR_E1_BoneWatch_L1.fbx · _L2 · _L3
│   ├── Environment/E1_Tundra/     ENV_E1_Pedestal.fbx · ENV_E1_Atlas_Albedo.png
│   └── VFX/                       VFX_HER_01_FireCircle.prefab …
├── Resources/
│   ├── Sprites/UI/                (botões, molduras, barras)
│   ├── Sprites/Environment/       (texturas de terreno, caminhos, pedestais)
│   └── Sprites/Heroes/            (poses dos 22 heróis)
├── Scripts/
│   ├── Core/                      (Bootstrap, GameFlow, ObjectPoolManager)
│   ├── Data/                      (EraBiomeThemeSO, HeroDataSO, TowerDataSO, EnemyDataSO)
│   ├── Systems/                   (FlowField, SpatialHashGrid, DamageCalculator)
│   └── UI/                        (HudController, MapController, CameraRig)
└── Settings/URP/                  (URP_Low.asset, URP_Medium.asset, URP_High.asset)
```

## 🧪 Testes Automatizados & Critérios de Aceite
- Executar e manter verdes as suítes de teste `EditMode` e `PlayMode`.
- **Validação de Alocação:** O teste `StressWave_AllocatesZero` deve garantir 0 bytes alocados pelo GC durante 1800 frames de combate ativo.
