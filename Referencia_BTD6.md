# Chrono Defense — Referência Visual: Bloons TD 6

**Status (27/11):** cumprido. O mapa-modelo "Prado" virou o **padrão oficial** e foi incorporado à **Bíblia v2.1** (§3, §3.9–3.12) e ao **GDD v2.1** (§6.1, §7.1, §7.7, §10.7, §11.4, §12). Este documento fica como histórico da análise do BTD6; em caso de dúvida, valem a Bíblia e o GDD. Biomas e temas por Era seguem a Bíblia §4; **não há bases** (portal/covil/Núcleo) em nenhuma fase.
**Precedência:** a Bíblia v2.1 e o GDD v2.1 já incorporam tudo daqui e valem sobre este documento.

**Fontes analisadas (26/11/2026):** quadros capturados de 3 vídeos de gameplay em 6 mapas (Monkey Meadow em 3 momentos da partida + tela de derrota + desbloqueio; mapa do deserto; cidade com neve; castelo; Downstream, rodada 58/80) e dados da Bloonswiki: área de jogo do BTD6 mobile = **300 × 232 unidades**; base das torres = **raio 6** (Dardo, Ninja, Sniper), **7** (maioria), **8** (Bucaneiro), **11** (Vila, Morteiro, Super Macaco).

---

## 1. O que faz o BTD6 parecer "bonito, moderno e fluido"

| # | Princípio | Como aparece no BTD6 | Como estava no Chrono Defense |
|:-:|:--|:--|:--|
| 1 | **O mapa É a tela** | Câmera quase vertical, sem céu nem horizonte; o mapa preenche tudo e as bordas são decoradas (árvores, neve, rochas) que "emolduram" a área | Câmera a 42° com céu; mapa numa faixa central cercada de chão vazio |
| 2 | **Unidades grandes** | Torre/herói ≈ **7% da largura** do mapa; balões ≈ largura do caminho | Herói ≈ 2% (1,8 m em ~53 m); creeps eram bolinhas |
| 3 | **Caminho largo e marcante** | ≈ 6% da largura; pedras com contorno escuro; contraste forte com o chão | 2 m (≈ 3,8%), fino, cinza sobre branco |
| 4 | **Chão pintado e cheio de vida** | Grama com variação de tom, flores, poças, neve; nada é cor chapada | Chão quase branco e liso |
| 5 | **Saturação alta, sombras suaves** | Verdes e azuis vivos, contorno escuro em tudo, sombra de contato | Paleta fria e lavada |
| 6 | **Interface que não cobre o jogo** | Loja = coluna estreita à direita (≈ 14% da largura) com cartões (retrato + preço); topo-esquerda vidas e dinheiro; topo-direita rodada + engrenagem; canto inferior direito: botão de habilidades (raio) e play/acelerar (verde) | Painel da torre embaixo cobrindo ⅓ do mapa; 5 botões de velocidade |
| 7 | **Painel de melhorias lateral** | Ao tocar numa torre, o painel abre **do lado oposto** ao da torre: retrato grande, alvo ("Primeiro" com setas), 3 caminhos em linhas com ícone + botão verde de preço, "Vender" vermelho com o valor | Painel inferior com 3 botões de texto |
| 8 | **Feedback em vez de números** | Estouro = flash branco em estrela + partículas; não mostra ouro por abate | "+4 +6" de ouro espalhados por toda a tela |
| 9 | **Um botão de velocidade** | Play → acelerar (alterna) | 1×, 2×, 4×, 16× e pausa |
| 10 | **Telas de evento chamativas** | Desbloqueio com personagem girando sobre raios dourados; "DERROTA" com personagem tonto, painel azul e 4 botões grandes com ícone | — |

## 2. Fase A — Enquadramento e escala (**implementada**)

| Parâmetro | Antes | Agora | Arquivo |
|:--|:-:|:-:|:--|
| Inclinação da câmera | 42° | **65°** | `CameraRig.Pitch` |
| Campo de visão | 32° | **26°** | `CameraRig.Fov` |
| Escala visual das unidades (herói, creeps, torres, projéteis) | 1× | **2,5×** | `ArtLibrary.UnitScale` |
| Pedestal (visual e lógico) | Ø 1,6 m | **Ø 2,8 m** | `ArtLibrary.PedestalScale`, `MvpDataGenerator.PedestalRadius` |
| Caminho comum / chefe | 2,0 / 2,5 m | **3,0 / 3,5 m** | `MvpDataGenerator.CommonPathWidth/BossPathWidth` |
| Raio de toque no pedestal | 1,2 m | **2,0 m** | `TowerPlacer.PickRadius` |
| Ouro flutuante por abate | ligado | **desligado** | `GameBootstrap.FloatyTextEnabled` |

**Por que escalar o visual e não encolher o mapa:** a simulação (alcances, velocidades, flow field) continua em metros, então nada do balanceamento do GDD muda. Resultado: herói ≈ 4,5 m de altura visual (~2,2 m de largura) num mapa de ~53 m ≈ **5–7% da largura**, como no BTD6. Proporção alcance ÷ largura da unidade ≈ 2,7 (BTD6 Dardo: 32 ÷ ~20 ≈ 1,6–2,7).

Os `.glb` continuam em 1,8 m / 1,0 m / 4,0 m (padrão de exportação): a ampliação é só no jogo.

## 2b. Rodada de 26/11 — "igual ao print" (**implementada**)

| Mudança | Antes | Agora | Arquivo |
|:--|:-:|:-:|:--|
| Posicionamento | 10 pedestais fixos | **livre**: qualquer chão fora do caminho (base Ø 2,6 m; longe do covil/Núcleo; sem sobrepor) | `CombatSimulation.CanPlaceAt/AcquireSpot`, `TowerPlacer` |
| Construir | toca pedestal → menu embaixo | **toca o cartão na loja → toca o chão** (prévia de alcance azul/vermelha; toque de novo confirma; no PC, clique) | `TowerPlacer`, `HudController` |
| Inclinação da câmera | 65° | **55°** (vê rosto e arma das unidades) | `CameraRig.Pitch` |
| Área do mapa na tela | faixa central (HUD reservava 25% da altura) | **tela inteira menos a loja** (14% à direita); vidas/ouro/onda por cima do mapa | `CameraRig.SafeViewport/ShopFraction` |
| Escala visual heróis/torres | 2,5× | **3,6×** | `ArtLibrary.UnitScale` |
| Escala visual inimigos | 2,5× | 2,5× (lobo ≈ largura do caminho) | `ArtLibrary.CreepVisualScale` |
| Chão | neve lisa | **prado pintado**: grama com variação, manchas de neve, flores, tufos, pedras; pinheiros na borda | `ArtLibrary`, `LevelRenderer.BuildDecor` |
| Caminho | terra marrom | **lajotas de pedra** com contorno escuro grosso e sombra | `ArtLibrary.StoneTexture`, `LevelRenderer.Ribbon` |
| HUD | painéis escuros, 5 botões de velocidade | loja em madeira (cartões com retrato e preço), botão verde ▶/▶▶, raio amarelo da habilidade, painel de melhorias à esquerda | `HudController` |
| Sombra de contato | — | elipse escura sob cada unidade | `LevelRenderer` |
| Fase de teste | — | **"Prado"**: 66 × 49,5 m, caminho 3,5 m (≈ 5% da largura), proporções do print do Monkey Meadow, traçado próprio sem cruzamentos (o flow field cortaria caminho em cruzamentos) | `MvpDataGenerator.GenerateVisualTest` (menu **Tools ▸ Chrono ▸ Fase de teste visual (estilo BTD6)**) |

## 3. Próximas fases (propostas)

**Fase B — Interface no estilo BTD6**
- Coluna de loja à direita (≈ 14% da largura): cartões 2 colunas com retrato da torre/herói e preço; cadeado nos bloqueados; cartão do herói no topo.
- Topo-esquerda: coração + vidas, moeda + ouro (números grandes com contorno). Topo-direita: "Onda X/10" + engrenagem.
- Canto inferior direito: botão de habilidade do herói (raio amarelo, com recarga circular) e **um** botão play/acelerar (verde).
- Painel de melhorias lateral, abrindo do lado oposto ao pedestal tocado; nunca embaixo.

**Fase C — Cenário pintado**
- Chão da Tundra com variação: neve fofa, manchas de musgo, pedrinhas, pegadas; bordas emolduradas por pinheiros nevados, rochas e blocos de gelo **fora** da área jogável (Bíblia §3.8 continua valendo para oclusão).
- Caminho de lajes de pedra com contorno escuro (como as pedras do Monkey Meadow), em vez de terra lisa.
- Saturação +20% nas unidades; sombra de contato em tudo.

**Fase D — Sensação ("juice")**
- Morte do creep: flash branco em estrela de 0,1 s + 4–6 partículas (pool de VFX, zero GC).
- Torre atirando: recuo curto (escala 0,92 → 1 em 0,1 s); herói já troca de pose (HeroPoseView).
- Construção: a torre "cai" no pedestal com quique (escala 1,25 → 1 em 0,2 s) e poeira.
- Barra de onda: transição curta "ONDA 3" no centro ao iniciar.

**Fase E — Telas de evento**
- Resgate de herói no estilo da tela de desbloqueio (raios dourados girando, nome, botão verde).
- Vitória/derrota com painel grande, personagem expressivo e 4 botões com ícone (Início, Reiniciar, Rever mapa, Continuar).

## 4. O que NÃO copiar
- A identidade visual (macacos, balões, logotipos e interface idêntica) é da Ninja Kiwi: usamos os **princípios** (enquadramento, escala, hierarquia, legibilidade), com a arte histórica e as paletas por Era do Chrono Defense.
- ~~Posicionamento livre~~: adotado em 26/11 por decisão do usuário (substitui os pedestais do GDD §6–7).
- O traçado exato dos mapas do BTD6: a fase "Prado" usa só as proporções; os mapas finais são nossos.
