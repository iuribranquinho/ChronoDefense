# ⏳ Chrono Defense

**Tower defense histórico para celular**, feito em **Unity 6 (URP)**. O jogador defende o caminho com campeões e heróis de cada época da humanidade, da Pré-História em diante, numa campanha estilo mapa da saga.

> 🚧 **Em desenvolvimento.** A Era 1 (Pré-História) está jogável como MVP: 10 fases, menu de seleção, 4 campeões, herói Krog e chefe Mamute Alfa.

---

## 🎮 O jogo

| | |
| :-- | :-- |
| **Gênero** | Tower defense (visual e leitura inspirados no Bloons TD 6) |
| **Plataformas** | Android 10+ · iOS 15+ |
| **Desempenho alvo** | 60 FPS (aparelhos médios e bons) · 30 FPS (modo Low) |
| **Escopo final** | 5 Eras × 10 capítulos × 10 fases = **500 fases**, 50 chefes, 22 heróis históricos |

### O que já funciona
- **Menu da saga pintado:** toque nas pedras das fases, estrelas por progresso e painel com nome, miniatura e estrelas da fase.
- **10 fases da Era 1** com 5 traçados e biomas que seguem a estrada do mapa da saga:
  - fases 1–4: Mata;
  - fases 5–7: Savana;
  - fase 8: Deserto;
  - fase 9: Planalto;
  - fase 10: Tundra, com o covil do chefe.
- **Posicionamento livre:** o campeão vai em qualquer lugar fora do caminho, sem plataformas. Os inimigos entram e saem pela borda da tela.
- **Campeões da Era 1:**
  - **Caçador:** arremessa lanças.
  - **Catapulta de Pedra**
  - **Xamã:** ataca com fogo.
  - **Guardião:** invoca soldados que seguram e enfrentam os inimigos.
- **Herói Krog:** tem a habilidade *Caça Coordenada de Fogo*, que é lançada à mão e deixa o chão queimando.
- **Animações procedurais:** pulo e rolamento dos inimigos, respiração e mira dos campeões, projéteis que saem da mão e recarregam.
- **HUD estilo BTD6:** loja com ícones pintados e preço dinâmico, botão de habilidade com recarga, painel de evolução com 3 caminhos, pausa e velocidade ×1/×3.

---

## 🛠️ Tecnologia

- **Unity 6000.6.10f1** com **URP 17** (Render Graph).
- **Simulação híbrida em C# Jobs + Burst**: `NativeArray` + `IJobParallelFor`, sem o pacote Entities.
- **Caminho por flow field** de 128×128 células (BFS reverso), calculado ao carregar a fase.
- **Spatial hash grid** de 64×64 (células de 2 m) para as torres encontrarem alvos rapidamente.
- **Física desligada**: toques resolvidos por raio × plano Y=0.
- **Dados em ScriptableObjects**: `LevelDataSO`, `LevelLayoutSO`, `TowerDataSO`, `HeroDataSO`, `EnemyDataSO`, `CampaignSO`.
- **UI em código**: uGUI + TextMeshPro, com arte pintada carregada de `Resources/Sprites/UI`.
- **Pacotes principais**:
  - `render-pipelines.universal`
  - `burst`
  - `collections`
  - `mathematics`
  - `addressables`
  - `cloud.gltfast` (modelos .glb)
  - `inputsystem`
  - `localization`
  - `test-framework`

### Regras de código (obrigatórias)
- **Zero alocação no laço de jogo:** nada de `new`, LINQ, concatenação de strings ou lambdas com captura em `Update`, no tick da simulação (30 Hz) ou nos callbacks da UI de combate.
- **Pools:** `StructPool<T>` para inimigos e projéteis da simulação; `ComponentPool<T>` para views, números de dano e partículas.
- **Texto e materiais sem alocar:** `TMP_Text.SetText("{0}", valor)` e `MaterialPropertyBlock`.
- **Shaders do URP:** só `Universal Render Pipeline/Lit` e `Unlit`, ou `2D/Sprite-Lit-Default` e `Sprite-Unlit-Default`. Os shaders `Sprites/Default` e `Standard` são proibidos.

---

## 🚀 Como abrir e jogar

1. Instale o **Unity 6000.6.10f1** pelo Unity Hub, com os módulos Android e/ou iOS.
2. Clone o repositório e abra a pasta no Unity Hub (**Add project from disk**).
3. Espere a primeira importação; ela demora porque gera a pasta `Library/`.
4. No menu **Tools ▸ Chrono**, rode nesta ordem:

   | Comando | O que faz |
   | :-- | :-- |
   | **Configurar projeto (URP + TMP + cena)** | Só na primeira vez: liga o URP, importa o TextMeshPro e cria a cena de jogo |
   | **Gerar dados do MVP (Era 1 · Cap. 1)** | Cria as fases, os traçados, os biomas, campeões, inimigos e o chefe da Era 1 |
   | **Criar menu inicial** | Monta a cena `MainMenu` e ajusta a ordem das cenas no build |
   | **Reimportar arte da interface** | Reaplica as configurações de importação da arte de UI e de cenário |

5. Abra `Assets/_ChronoDefense/Scenes/MainMenu` e aperte **Play**.

> Atalhos de teste: **Tools ▸ Chrono ▸ Fase de teste visual (estilo BTD6)** abre o *Prado*, e **Voltar para a Fase 10 (chefe)** abre a fase do chefe.

---

## 📁 Estrutura

```text
Assets/_ChronoDefense/
├── Art/                 modelos 3D (.glb/.fbx), heróis, inimigos, chefes, torres, VFX
├── Data/                ScriptableObjects gerados (fases, traçados, campeões, inimigos)
├── Resources/Sprites/
│   ├── UI/              menu da saga, ícones da loja, botão de habilidade
│   └── Environment/     vegetação pintada por bioma (Forest, Savanna, Desert, Highland)
├── Scenes/              MainMenu · Gameplay
├── Scripts/
│   ├── Core/            Bootstrap, GameSession, fluxo de jogo, pools
│   ├── Data/            definições dos ScriptableObjects
│   ├── Systems/         CombatSimulation, CombatJobs, FlowField, SpatialHashGrid, dano
│   ├── UI/              HUD, menu, LevelRenderer, UnitRenderer, ArtLibrary, câmera
│   └── Editor/          geradores de dados, montagem de cenas, importadores
├── Settings/URP/        URP_Low · URP_Medium · URP_High
└── Tests/               EditMode e PlayMode

Tools/                   pipeline de arte (Python / PowerShell)
├── gen_env_art.py       gera a vegetação pintada dos biomas (chão, caminho, árvores, arbustos, pedras)
├── normalize_glb.py     normaliza modelos .glb (escala, pivô, texturas até 2048)
├── separate_poses.py    separa as poses de um herói em um .glb por pose
└── README_3D.md         como gerar os modelos 3D (Tripo3D)
```

---

## 📚 Documentação

| Documento | Para quê |
| :-- | :-- |
| [`GDD_Tecnico_Unity6.md`](GDD_Tecnico_Unity6.md) | Sistemas, matemática, números de balanceamento e desempenho. **Manda no código.** |
| [`Biblia_do_Jogo_Refinada.md`](Biblia_do_Jogo_Refinada.md) | Arte, história, biomas, prompts 3D e interface. **Manda na direção de arte.** |
| [`Referencia_BTD6.md`](Referencia_BTD6.md) | Análise visual do Bloons TD 6 usada como referência |
| [`Claude.md`](Claude.md) | Diretrizes de engenharia do projeto |
| [`Tools/README_3D.md`](Tools/README_3D.md) | Pipeline de geração de modelos 3D |

Quando os dois documentos divergem num número, vale o GDD para código e matemática, e a Bíblia para arte e estilo.

---

## 🧪 Testes

Abra **Window ▸ General ▸ Test Runner** e rode as suítes **EditMode** e **PlayMode**.

- **Traçados:** sem cruzamentos, pontas fora da tela, enquadramento e alcance dos pontos de construção.
- **Sistemas:** posicionamento livre, soldados do Guardião, flow field e dano.
- **Alocação:** `StressWave_AllocatesZero` exige **0 bytes** de lixo durante 1800 quadros de combate.

---

## 🗺️ Próximos passos

- [ ] Modelos 3D finais dos campeões e ajuste da posição da mão no arremesso
- [ ] Nomes e campeões das Eras 2 a 5
- [ ] Tela de configurações
- [ ] Chão pintado à mão por fase (rio, ponte, aldeia)
- [ ] Capítulos 2 a 10 da Era 1

---

## 📄 Licença

© Todos os direitos reservados. Código e arte de uso exclusivo do projeto Chrono Defense.
