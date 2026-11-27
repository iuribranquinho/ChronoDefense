# ChronoDefense — Relatório de Progresso

**Data:** 25/11/2026 · **Referências:** `GDD_Tecnico_Unity6.md` (v2.0) · `Biblia_do_Jogo_Refinada.md` (v2.0) · `CLAUDE.md`
**Motor:** Unity 6000.6.10f1 · URP 17.6 · Burst 2.0 · Collections 6.6 · Input System 1.20 · Addressables 5.0 · Localization 1.5.14

**Status geral:** projeto Unity criado e compilando. Marcos **M1 (núcleo de runtime)** e **M2 (apresentação jogável)** implementados e validados por testes automatizados. O teste de alocação zero do GDD §14.2 passa num **player standalone Windows**.

---

## 1. Resumo

| Entrega | Situação | Validação |
| :--- | :--- | :--- |
| Projeto Unity 6 + URP (Low/Medium/High) + TMP + Input System | Concluído | Compila sem erros |
| Camada de dados e ScriptableObjects (GDD §10) | Concluído | 60 testes EditMode verdes |
| Dados do MVP — Era 1, Cap. 1 (158 assets gerados) | Concluído | Testes de tabela §8.4 e §9.2 |
| **M1** Pools zero-GC (`StructPool`, `ComponentPool`, `ObjectPoolManager`) | Concluído | Testes EditMode |
| **M1** Flow Field 128×128 (Burst + Jobs) | Concluído | Todas as células alcançam o Núcleo; nenhum vetor nulo |
| **M1** Spatial Hash 64×64 (counting sort, O(K)) | Concluído | Igual à força bruta em 50 consultas |
| **M1** `DamageCalculator` + `CombatSimulation` (tick 30 Hz) | Concluído | Exemplos §5.2, regras de CC de chefe, ondas, chefe Krog, habilidade do Krog |
| **M2** `LevelRenderer`, `UnitRenderer` (instancing), `RangeIndicator` | Concluído | Captura de frame renderizado (PNG) |
| **M2** `TowerPlacer`, `CameraRig`, `HudController` | Concluído | Captura de frame; sem teste de entrada por toque |
| **PlayMode** `StressWave_AllocatesZero` | **Passa no player** | 1.800 frames · 14.400 ticks · 133–150 creeps · 0 frames com GC do jogo |

---

## 2. Fase 1 — Verificação e testes EditMode

1. **Projeto criado** em batchmode a partir de `Packages/manifest.json` (versões tiradas do editor instalado e do registro da Unity). O Unity acrescentou pacotes padrão ao manifest (Purchasing, Analytics, Collab, AI Navigation, Multiplayer Center etc.).
2. **Input System:** `activeInputHandler` estava em `0` (Input Manager antigo); mudei para `1` (Input System), como pede o GDD §2.
3. **Assemblies:** `ChronoDefense.Data`, `ChronoDefense.Editor`, `ChronoDefense.Runtime`, `ChronoDefense.Tests.EditMode` e `ChronoDefense.Tests.PlayMode` compilam sem erros.
4. **Gerador do MVP** executado: 158 ScriptableObjects em `Assets/_ChronoDefense/Data/`.
5. **EditMode:** 60/60 verdes. Um teste meu estava errado e foi corrigido: G0(100) = min(800, 300 + 5·99) = **795**, não 800 (o teto de 800 nunca é atingido dentro de uma Era).

---

## 3. Fase 2 — M1: núcleo de runtime zero-GC

### 3.1 Arquivos (`Assets/_ChronoDefense/Scripts/`, assembly `ChronoDefense.Runtime`)

| Arquivo | Conteúdo |
| :--- | :--- |
| `Core/StructPool.cs` | Slots em `NativeArray<T>` + free list; contador também em `NativeArray`, então Rent/Return funcionam dentro de jobs Burst |
| `Core/ComponentPool.cs` | `IPoolable`, pool de GameObjects com pilha de índices livres; esgotado → recicla o mais antigo (floaty text, VFX) |
| `Core/ObjectPoolManager.cs` | Dono dos `ComponentPool`s, ids `int` atribuídos no prewarm; `PoolManifest` com `ceil(pico × 1,2)` |
| `Systems/FlowField.cs` | Custo (16 KB), integração (32 KB), direções `float2` (128 KB). `BuildCostJob` e `BuildFlowJob` (`IJobParallelFor`) + `BuildIntegrationJob` (BFS reverso), todos `[BurstCompile]`. `Bake()` no carregamento e `Recalculate()` sob demanda. Amostragem bilinear e "progresso gratuito" pela integração |
| `Systems/SpatialHashGrid.cs` | 64×64 células de 2 m; reconstrução por counting sort em 3 passos (Burst); `QueryRadius` sem alocação |
| `Systems/DamageCalculator.cs` | Fórmula completa §5.2 (crítico, mitigação, maior shred, vulnerabilidade com teto), escudo, lentidão (teto 90%/60% chefe), atordoamento com resistência de chefe (×0,5; imune por 5 s após 3 em 10 s), DoT |
| `Systems/SimTypes.cs` | `CreepState`, `TowerState`, `ProjectileState`, `HitPayload`, `SkillZone` (blittable) |
| `Systems/CombatJobs.cs` | `StatusMoveJob` (paralelo), `TargetingJob` (paralelo, prioridades §4.5), `FireAndResolveJob` (zonas, disparo, projéteis e dano; single-thread para determinismo) |
| `Systems/CombatSimulation.cs` | Tick fixo de 30 Hz: spawn por linha do tempo, jobs, commit (ouro, vazamentos, estrelas), ondas e intervalos, chefe Krog (Tocha com telegrafia de 1,5 s + Rugido aos 66%/33%), comandos do jogador (construir, upgrade com trava 5-2-0 e onboarding, vender a 70%, prioridade, invocar herói, habilidade) |
| `Systems/GameEventBuffer.cs` | Ring buffer pré-alocado (GDD §11.1) |

Ordem do tick (GDD §4.2): spawn → status/movimento → spatial hash → targeting → disparo/projéteis/dano → commit.

### 3.2 Adições ao Data
- `BalanceFormulas`: `MaxTierForLevel`, `IsArchetypeUnlocked`, `HeroSlotsForLevel` (§1.3) e `Stars` (§1.2).
- `EffectType.CrowdBonus`: o +15% por inimigo extra do Krog (teto de +150%) agora está nos dados da habilidade.

---

## 4. Fase 3 — M2: apresentação e gameplay

| Arquivo | Conteúdo |
| :--- | :--- |
| `UI/ArtLibrary.cs` | **Arte provisória procedural** gerada no carregamento com a paleta da Bíblia: neve, terra lamacenta, pedestais de pedra com centro escuro, glifos de runa, covil de gelo, Núcleo-ampulheta, céu com aurora, ícones de torre por arquétipo, Krog chibi, fichas de creep com contorno violeta `#3B1E54` e olhos magenta `#E0369B` (§3.7). Nenhuma primitiva cinza |
| `UI/LevelRenderer.cs` | Implementa `ILevelRenderer` (§11.2): `LoadAsync` com Addressables quando o tema/layout define prefabs e fallback procedural; iluminação e ambiente do tema; caminho em faixa contínua; runas a cada 3 m com estados 20%/60%/vermelho pulsante de chefe; anéis de pedestal com os estados da Bíblia §3.4; estágios L1–L3 das torres (§7.6) |
| `UI/UnitRenderer.cs` | Creeps e projéteis via `Graphics.RenderMeshInstanced` direto dos arrays da simulação, 1 draw call por tipo, interpolação entre ticks |
| `UI/RangeIndicator.cs` | Plano 2D no chão XZ, escala `(raio×2, 0,01, raio×2)`, cor por `MaterialPropertyBlock` |
| `UI/CameraRig.cs` | FOV 32°, rotação fixa (42°, 0°, 0°). Enquadra os 8 cantos do `CameraBounds` na região da tela livre do HUD (`SafeViewport`, de 14% a 88,5% da altura) pela menor distância possível, com o mapa centralizado; reenquadra se a proporção da tela mudar. Pan travado no centro com zoom ≥ 1; ao aproximar (até −15%) libera só o que saiu da tela. Pan com 1 dedo, WASD, setas ou botão direito/meio; pinça e roda do mouse; toque sem arrasto vira "tap"; raio × plano y=0 (sem física) |
| `UI/TowerPlacer.cs` | Toque → pedestal mais próximo (1,2 m); construir, upgrade, vender, prioridade; invocar herói num pedestal; mirar a habilidade no mapa; estados visuais dos pedestais em tempo real |
| `UI/HudController.cs` | Moedas, vidas, onda X/N, barra do chefe, velocidades **1×/2×/4×/16×**, pausa, painel do Krog (invocar/recarga radial/mirar), "Chamar onda" com bônus §8.5, menu de construção, painel de 3 caminhos com cadeado e motivo, vender, fim de partida com estrelas, safe area |
| `UI/FloatyText.cs` | "+ouro" por abate via `ComponentPool` (64, recicla o mais antigo) |
| `Core/GameBootstrap.cs` | Física em `SimulationMode.Script`; tick acumulado no `Update` (sem `FixedUpdate`); `GC.Collect` + `UnloadUnusedAssets` antes da onda 1; consumo de eventos |
| `Editor/ProjectSetup.cs` | Menu **Tools ▸ Chrono ▸ Configurar projeto**: URP_Low/Medium/High, shaders em *Always Included*, TMP, dados do MVP e cena `Scenes/Gameplay.unity` (no Build Settings, nível 10 + Krog) |

**Limites da área jogável:** `MvpDataGenerator.ComputeCameraBounds` calcula a caixa exata que envolve caminho, pedestais (com o raio), covil e Núcleo (com a pegada da arte em pé, constantes em `LevelRenderer`) e soma 1,5 m de cada lado em X e Z. No layout do Krog o resultado é 55,5 × 28,5 m. É um pouco maior que o valor fixo anterior (54 × 28 m), que cortava a arte do covil e do Núcleo. A tela fica mais cheia porque o enquadramento agora é exato, não por causa de uma caixa menor. A câmera continua em perspectiva (Bíblia §3.2), então no lugar de "orthographic size" calcula-se a distância.

**Shaders:** `Sprite-Lit-Default` para chão, caminho, pedestais, runas, torres e herói; `Sprite-Unlit-Default` para céu e indicador de alcance; `URP/Unlit` com GPU instancing para creeps e projéteis. Nenhum `Sprites/Default` ou `Standard`.

**Ajustes feitos a partir das capturas de tela:**
- A neblina do tema começava a 40 m, mas a câmera fica a ~50 m e "lavava" os creeps. Agora ela começa após a área jogável (Bíblia §3.3).
- Bounds de câmera mais justos.
- Céu desenhado depois do chão distante.
- Torres ampliadas 1,5× para leitura em 6".
- Painel de torre sem sobreposições.

---

## 5. Fase 4 — `StressWave_AllocatesZero`

**Cenário:** 150 creeps mantidos vivos (reposição contínua), 14 torres em estado 5-2-0, 2 heróis com habilidade em uso, HUD, floaty text e render instanciado ativos. 8 ticks por frame (= 16× a 60 FPS) e `Time.timeScale = 16`.

**O que descobri ao medir:**
1. **Dentro do editor**, "GC Allocated In Frame" registra **~360 KB/frame mesmo com o jogo destruído**. É o próprio editor e o test runner, então a medição ali não é válida. No editor, o teste roda o estresse completo (detecta exceções e instabilidade) e termina como **Inconclusive** de propósito.
2. **No player standalone** sobra a infraestrutura do runner: um piso de 428–972 B/frame, idêntico com e sem o jogo, mais uma mensagem "player alive" de ~4–5 KB a cada ~7,5 s. Isso foi provado por `AllocationPhaseProbeTests`: os mesmos 378 B/frame em média nas 5 fases, inclusive antes de o jogo existir.
3. **Critério estrito adotado:** primeiro mede-se uma linha de base de 900 frames vazios nas mesmas condições. Depois, cada um dos 1.800 frames de combate precisa ter **exatamente** um dos valores produzidos pelo runner sozinho; qualquer byte alocado pelo jogo tira o frame do conjunto. Picos de mensagem só são aceitos na mesma cadência por segundo da linha de base.

**Resultado no player Windows:** `Passed`. Foram 0 frames fora da linha de base e 0,13 mensagem/s, tanto na base quanto no combate. Uma falha intermediária de 724 B era do próprio teste (arrays de diagnóstico criados no frame medido) e foi corrigida.

> **Sobre o pedido de "`GC.GetTotalMemory()` estritamente zerado":** esse valor é o tamanho total do heap gerenciado, nunca zero. A variação dele também não serve de critério enquanto o runner aloca: no player, a variação de ~0,9 MB em 30 s corresponde exatamente às alocações do runner. Além disso, `GC.GetTotalMemory` só enxerga blocos de 8 KB (calibrado em teste) e `GC.GetAllocatedBytesForCurrentThread` retorna sempre 0 neste Mono. O critério acima é o equivalente rigoroso.

**Como rodar no player:**
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.6.10f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform StandaloneWindows64 -testFilter StressWave_AllocatesZero -testResults Logs\Batch\Player.xml -logFile Logs\Batch\player_tests.log
```

---

## 6. Suítes de teste

| Suíte | Onde | Resultado |
| :--- | :--- | :--- |
| EditMode (67 testes: fórmulas, dados, limites do layout, enquadramento e trava da câmera, pools, flow field, spatial hash, dano, simulação) | Editor | **67/67** |
| PlayMode `Gameplay_RendersFrameToPng` | Editor | Passa (gera `Logs/Batch/gameplay.png`) |
| PlayMode `Probe_AllocationByPhase` | Editor e player | Passa (diagnóstico) |
| PlayMode `StressWave_AllocatesZero` | **Player Windows** | **Passa** |
| PlayMode `StressWave_AllocatesZero` | Editor | Inconclusive (intencional, ver §5) |

Script de apoio: `Tools/unity_batch.ps1` (`-Mode compile | method | tests`).

---

## 7. Desvios e simplificações (MVP)

| # | Item | Situação |
| :-: | :--- | :--- |
| 1 | Velocidades 4× e 16× | Pedidas para esta etapa; o GDD §12.1 prevê só 1×/2×. Sugiro restringir 4×/16× a builds de desenvolvimento |
| 2 | Pan com 1 dedo | Conflita com "arrastar herói" do GDD §12.2. O herói é posicionado pelo botão do retrato + toque no pedestal |
| 3 | Krog no capítulo 1 | O GDD §1.3 prevê 0 heróis no cap. 1. A flag `DevUnlockHeroes` (ligada na cena) libera o Krog para teste (marco M4) |
| 4 | Herói | Ataca do pedestal tudo o que entra no raio de guarda (3,5 m); ainda não sai do pedestal para interceptar |
| 5 | Guarda | Soldados ainda não bloqueiam o caminho; a Guarda causa dano corpo a corpo (não está liberada no cap. 1) |
| 6 | DoT | Um slot por creep com o maior DPS ativo; o GDD manda somar fontes diferentes |
| 7 | Projéteis esgotados | O disparo é perdido; o GDD §3.4 pede reciclar o mais antigo |
| 8 | Traits/ações não usados na Era 1 | `Healer`, `Spawner`, `BossAction.StunTowers/Regen/MapChange` não implementados; `FlowField.Recalculate()` existe, mas nenhum evento de mapa o chama |
| 9 | Valores PROV | Velocidade de projétil por arquétipo, 30 s de preparação, 12 s entre ondas |
| 10 | TMP Essentials | `AssetDatabase.ImportPackage` é assíncrono e não termina em batchmode; nesta sessão extraí o `.unitypackage` manualmente. No editor interativo o menu funciona |
| 11 | Arte | Toda procedural e provisória; os modelos 3D hand-painted e o shader toon/outline da Bíblia §3.1 ainda não existem |
| 12 | Cinemática de resgate e abertura do covil | Placeholders (espera de 4 s e "respiração" de escala) |

---

## 8. Pendências

| # | Pendência |
| :-: | :--- |
| 1 | Teste em aparelho Android/iOS real (tiers Low/Medium) e medição de FPS/tempo de main thread (GDD §14.4) |
| 2 | Modificadores T3–T5 das torres; T1–T2 do Alquimista e da Guarda |
| 3 | Itens 4–8 da tabela de desvios (interceptação do herói, bloqueio da Guarda, DoT somado, reciclagem de projéteis, traits restantes) |
| 4 | Tabelas de Localization (nomes, caminhos, HUD) |
| 5 | Pipeline de arte real (VAT dos creeps, prefabs no Addressables, shader toon + outline) |
| 6 | Revisar pacotes adicionados automaticamente ao manifest (Purchasing criou `Assets/Resources/BillingMode.json`) |
| 7 | Simulador headless de balanceamento (GDD §14.3) |
| 8 | Inicializar um repositório git (a pasta não é versionada) |

---

## 9. Próximos passos sugeridos

1. Abrir `Assets/_ChronoDefense/Scenes/Gameplay.unity` no editor e jogar o nível 10 (Krog Corrompido).
2. Fazer um build Android de desenvolvimento e rodar `StressWave_AllocatesZero` num aparelho do tier Medium.
3. Iniciar o **M3/M4** do GDD §15: Alquimista e Guarda completos, interceptação do herói e cinemática de resgate.
