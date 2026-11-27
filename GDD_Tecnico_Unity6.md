# GDD Técnico — Chrono Defense (Unity 6 / URP Mobile)

**Versão:** 2.1 (27/11/2026) · **Motor:** Unity 6 (6000.x LTS) · **Pipeline:** URP · **Plataformas:** Android 10+ / iOS 15+
**Documento irmão:** `Biblia_do_Jogo_Refinada.md` (v2.1)
**Padrão vigente:** o mapa-modelo **"Prado"** (§10.7) e os sistemas ajustados nele: posicionamento livre (§6.1, §12), sem bases (§10.6), soldados do Guardião (§7.7), alcances de 8 m (§7.1), projéteis e efeitos (§11.4). Biomas, temas por Era, economia e escalonamento continuam como estavam.

> **Regra de precedência.** Em caso de conflito, este GDD vale para **números, sistemas e código**, e a Bíblia vale para **arte, narrativa e estilo**. Os valores de habilidades da Bíblia são provisórios e são substituídos pela seção 6 deste documento.

---

## Sumário

0. Registro de Mudanças (v1.0 → v2.0)
1. Loop de Jogo e Progressão
2. Pilha Técnica Unity 6
3. Envelope de Performance e Arquitetura Zero-GC
4. Simulação: Flow Field, Spatial Hash e Tick
5. Matemática de Combate
6. Heróis (22) — Dados de Balanceamento
7. Torres — Matriz de 3 Caminhos (Trava 5-2-0)
8. Escalonamento de Dificuldade por Era (500 Níveis)
9. Bestiário Mecânico e Roteiro da Fase 10
10. Estrutura de Dados — ScriptableObjects
11. Interface com o `LevelRenderer`
12. HUD, Entrada e Fluxos de Controle
13. Economia, Monetização e LiveOps
14. Ferramentas, Testes e Critérios de Aceite
15. Plano do MVP
16. Pontos em Aberto

---

## 0. Registro de Mudanças (v1.0 → v2.0)

| Área | v1.0 | v2.0 |
| :--- | :--- | :--- |
| Chefe da Fase 10 | Mamute Alfa com *script* "invoque o Krog" (o Krog ainda não estava desbloqueado) | **Mamute-Lanudo Alfa** sem *script* de herói; o Krog é resgatado ao vencê-lo (26/11) |
| Construção | Toque em "célula livre" do grid | ~~Apenas em pedestais~~ → v2.1: **livre** fora do caminho |
| Heróis | Arrastar para qualquer ponto da rota | Herói ocupa um ponto de construção e intercepta inimigos num **raio de guarda** (v2.1: ponto livre, sem pedestal) |
| Upgrades | Especialização no nível 4 | **3 caminhos × 5 tiers**, com trava **5-2-0** |
| Flow Field | Vetor "tridimensional" e recálculo por barricada | Vetor **2D** (`float2`), **bake no carregamento** via Burst; recálculo só por evento de mapa |
| Escalonamento | Só por onda | Por **Era × progresso na Era × posição no capítulo × onda** |
| α (modo Hard) | 0,15 (28,6× de vida na onda 25) | **0,11** (12,2× na onda 25) |
| Estrelas | Baseadas em vidas restantes | Baseadas em **vazamentos**, para que as curas de Yeshua e o escudo de Marx não "comprem" estrelas |
| Creeps simultâneos | 150 (GDD) vs. 60 (Bíblia) | **150**, viabilizados por **VAT + instancing** (seção 3.5); substitui a Bíblia §7.1 |
| FPS | 60 FPS em aparelhos médios | **60 FPS** nos tiers Medium e High, **30 FPS** no tier Low (substitui a Bíblia §7.1) |
| Bestiário | "Armadura reduz até 70%" sem valor | Tabela de armadura com valores exatos (seção 5.4) |
| Texto | "Dentes-de-Sabão", "Solare", "Mortero" | **Dente-de-Sabre**, **Solar**, **Morteiro** |

---

**v2.0 → v2.1 (26–27/11): mapa-modelo "Prado" e sistemas ajustados nele**

| Área | v2.0 | v2.1 (vigente) |
| :--- | :--- | :--- |
| Construção | Apenas em pedestais | **Posicionamento livre** fora do caminho (§6.1, §12) |
| Bases | Portal/covil de onde os inimigos saem e Núcleo de destino | **Sem bases**: o caminho começa e termina fora da tela (§10.6) |
| Mapa | Layout livre, câmera 42° com céu | **Modelo "Prado"** (§10.7): caminho 3,5 m sem cruzamentos, quadro = caminho + 4,5 m, câmera 55° |
| Alcance mínimo | 6,0 m | **8,0 m** para todos os campeões e o Krog (§7.1) |
| Guarda | Ataque corpo a corpo somando os soldados | **Quartel** com 3 soldados que seguram, apanham, revidam e renascem (§7.7) |
| Krog — Caça de Fogo | 40 DPS direto na zona | 50% direto + 50% como queimadura que dura 1,5 s após sair (§6.2) |
| Projéteis | Só posição | Guardam a origem (`Origin`) para o arco visual a partir da mão (§11.4) |
| Fase de teste | — | `LVL_TEST_Prado`: 20 vidas, 650 de ouro, ondas da fase 3, as 4 torres liberadas (§10.7) |

---

## 1. Loop de Jogo e Progressão

### 1.1 Hierarquia
**Campanha** (500 níveis) → **Era** (100 níveis) → **Capítulo** (10 níveis) → **Nível** (10–25 ondas).

| Camada | Escala | Conteúdo | Recompensa |
| :--- | :--- | :--- | :--- |
| Era | 100 níveis | Bioma, inimigos, torres com a skin da era, trilha sonora | Próxima Era + skin lendária |
| Capítulo | 10 níveis | 9 comuns + 1 de chefe | Herói (em 22 dos 50 chefes; tabela na Bíblia §2.3), torre, fragmentos ou cosmético |
| Nível | 1 partida | 10 ondas (chefe) ou 12–25 (comum) | Ouro de conta, XP de conta, até 3 estrelas |

Índices canônicos usados em todo o código e nas fórmulas:
```
L ∈ [1, 500]                 nível global
e = ceil(L / 100)            Era (1..5)
l = ((L − 1) mod 100) + 1    nível dentro da Era (1..100)
p = ((L − 1) mod 10) + 1     posição no capítulo (1..10; 10 = chefe)
```

### 1.2 Estrelas (por vazamentos)
Um **vazamento** é um inimigo que chega ao Núcleo; cada um custa vidas segundo o seu peso (creep = 1, elite = 2, chefe = todas as vidas).

| Estrelas | Condição |
| :---: | :--- |
| ★ | Vencer com pelo menos 1 vida |
| ★★ | Vazamentos ≤ 20% das vidas iniciais |
| ★★★ | **Zero vazamentos** |

As estrelas abrem os **Portões de Era** no Saga Map e dão Chaves de Era para as *Side Stories*.

### 1.3 Liberação de recursos dentro da Era 1 (onboarding)
| Capítulos | Tier máximo de upgrade | Arquétipos de torre | Heróis no *loadout* |
| :--- | :---: | :--- | :---: |
| 1 | 2 | Atirador, Lançador | 0 (Krog é resgatado na fase 10) |
| 2–3 | 3 | + Alquimista | 1 |
| 4–6 | 4 | + Guarda | 1 |
| 7+ | 5 | Todos | **2** |

A partir da Era 2, tudo fica liberado. O *loadout* máximo é de 2 heróis por partida.

---

## 2. Pilha Técnica Unity 6

| Camada | Tecnologia | Observação |
| :--- | :--- | :--- |
| Motor | Unity 6 LTS (6000.x) | API `Awaitable` nativa para carregamento assíncrono (sem dependência de UniTask) |
| Render | URP, renderer Forward (Forward+ opcional no High) | Renderer Features do outline escritas na **API Render Graph** (padrão do URP no Unity 6) |
| Simulação | **C# Jobs + Burst + Collections + Mathematics** | Simulação híbrida: dados em `NativeArray`, sem pacote Entities (ver 4.1) |
| Física | **Desligada** | `Physics.simulationMode = SimulationMode.Script` e `Physics2D.simulationMode = SimulationMode2D.Script`; nenhum `Rigidbody`. Toques resolvidos por raio × plano (seção 12.3) |
| Conteúdo | **Addressables** | Grupos por Era; carregamento e descarregamento por bioma |
| Entrada | Input System (Enhanced Touch) | — |
| UI | uGUI + TextMeshPro | Canvas estático e dinâmico separados |
| Texto localizado | Localization package | Nomes de heróis, chefes e placas de covil (Bíblia §3.6) |
| Térmico | Adaptive Performance (Android) | Rebaixa o tier quando o aparelho esquenta |
| Perfilamento | `ProfilerMarker`, `ProfilerRecorder` | Marcadores sem alocação; testes automatizados de GC (14.2) |

---

## 3. Envelope de Performance e Arquitetura Zero-GC

### 3.1 Alvos por tier
| Tier | Aparelho de referência | FPS | CPU main thread | GPU |
| :--- | :--- | :---: | :---: | :---: |
| Low | 3 GB de RAM, Adreno 610 / A12 | 30 | ≤ 28 ms | ≤ 28 ms |
| Medium | Snapdragon 765G / Apple A14 | **60** | ≤ 14 ms | ≤ 14 ms |
| High | Snapdragon 8 Gen 1+ / A15+ | 60 | ≤ 12 ms | ≤ 12 ms |

### 3.2 Orçamento da main thread (Medium, 16,67 ms)
Os jobs rodam nas *worker threads*; a tabela mede o tempo da **main thread**, incluindo o `Complete()` dos jobs.

| Subsistema | Main thread | Workers (paralelo) | Estratégia |
| :--- | :---: | :---: | :--- |
| Flow Field | 0,0 ms (estático) | — | *Bake* no carregamento; recálculo só por evento (≤ 0,6 ms) |
| Movimento de creeps | 0,4 ms | 0,8 ms | `IJobParallelFor` com Burst |
| Rebuild do Spatial Hash | 0,2 ms | 0,3 ms | *Counting sort* em Burst |
| Targeting e disparo | 0,5 ms | 1,0 ms | `IJobParallelFor` por torre |
| Projéteis | 0,3 ms | 0,6 ms | Slots *struct* + Burst |
| Status e auras | 0,4 ms | 0,4 ms | Timers em array, sem eventos C# |
| Sincronizar views (transform) | 0,6 ms | 0,6 ms | `IJobParallelForTransform` |
| Render e submissão (CPU) | 4,0 ms | — | SRP Batcher + `RenderMeshInstanced` |
| UI / HUD | 1,0 ms | — | Canvas dinâmico mínimo |
| Lógica geral e economia | 0,6 ms | — | Tick fixo |
| Áudio, Input e Misc | 1,0 ms | — | — |
| **Margem** | **≥ 7,0 ms** | — | Picos, térmico, SO |

### 3.3 Política Zero-GC (obrigatória)
**Escopo:** todo código que roda durante a partida (`Update`, `LateUpdate`, tick de simulação, callbacks de UI de combate). O carregamento de nível e os menus estão **fora** do escopo.

**Proibido no hot loop**
| Prática | Alternativa |
| :--- | :--- |
| `new` de classe, array ou `List` | Pré-alocar no carregamento; reutilizar buffers |
| `Instantiate` / `Destroy` | `ObjectPoolManager` (3.4) |
| LINQ, `foreach` sobre `IEnumerable`/interfaces | `for` com índice; `foreach` só em `List<T>`/arrays (enumerador *struct*) |
| Lambdas que capturam variáveis, `delegate` criado por frame | Métodos estáticos; delegates cacheados no `Awake` |
| Concatenação ou formatação de `string` | `TMP_Text.SetText("{0}", valor)` (sem alocação) ou buffer `char[]` |
| *Boxing* (enum como chave genérica, `object`) | Chaves `int`; `IEqualityComparer` próprio |
| `GetComponent`, `Find*`, `Camera.main` | Referências cacheadas na inicialização |
| `renderer.material` | `MaterialPropertyBlock` ou propriedades globais de shader |
| `Animator.SetX("nome")` | `Animator.StringToHash` cacheado |
| `yield return new WaitForSeconds` | Timers em float dentro do tick |
| Queries de física | Não há física (Spatial Hash, seção 4.3) |
| `Debug.Log` | Wrapper com `[Conditional("DEV_BUILD")]` |

**Rede de segurança:** *Incremental GC* ligado no Player Settings. Antes da onda 1 rodam `GC.Collect()` e `Resources.UnloadUnusedAssets()` uma única vez, durante a tela de carregamento.

**Critério de aceite:** `ProfilerRecorder("GC Allocated In Frame") == 0 B` em 100% dos frames de uma partida de estresse automatizada (seção 14.2).

### 3.4 `ObjectPoolManager`
Duas famílias de pool, porque nem tudo precisa ser `GameObject`:

| Família | Tipo | Conteúdo | Por quê |
| :--- | :--- | :--- | :--- |
| **`StructPool<T>`** | Slots num `NativeArray<T>` + *free list* (`NativeArray<int>`) | **Creeps** (estado de simulação), **projéteis** | Lidos e escritos por jobs Burst; renderizados por instancing, sem `Transform` por unidade |
| **`ComponentPool<T>`** | `GameObject`s desativados + pilha de índices | **Floaty text**, VFX (`ParticleSystem`), views de **heróis**, **chefes** e creeps "especiais" (com *attachments*), moedas | Precisam de componentes Unity |

**Pré-alocação (*prewarm*).** No carregamento, o `LevelLoader` gera um `PoolManifest` a partir dos `WaveDataSO`: simula a linha do tempo de *spawns* e calcula a **concorrência máxima** por tipo de inimigo. O tamanho de cada pool é `ceil(concorrência_máx × 1,2)`.

| Pool | Tamanho padrão | Esgotamento |
| :--- | :---: | :--- |
| Creeps (soma de todos os tipos) | ≤ 150 (teto duro de simulação) | Dev: `assert`. Release: o *spawn* espera um slot (atrasa no máximo 1 tick) |
| Projéteis | 1.024 slots | Recicla o projétil mais antigo |
| Floaty text | 64 | Recicla o mais antigo |
| VFX por tipo | 8–32 (manifest) | Recicla o mais antigo |
| Heróis / Chefe | 2 / 1 + adds | Pré-carregados (nunca esgotam) |

```csharp
public interface IPoolable
{
    void OnRent();    // reset de estado; NÃO alocar
    void OnReturn();  // desativar, parar partículas
}

public sealed class ObjectPoolManager : MonoBehaviour
{
    public void Prewarm(in PoolManifest manifest);              // único ponto que aloca
    public T Rent<T>(int poolId) where T : Component, IPoolable;
    public void Return<T>(int poolId, T item) where T : Component, IPoolable;
    public void ReleaseAll();                                   // fim de partida
}

public struct StructPool<T> : System.IDisposable where T : unmanaged
{
    public NativeArray<T> Items;
    NativeArray<int> _free; int _freeCount;
    public int Rent();               // -1 se esgotado
    public void Return(int index);
    public void Dispose();
}
```
Os ids de pool (`int`) são atribuídos no *prewarm* e guardados no `EnemyDataSO`/`VfxDataSO` em tempo de execução, nunca strings.

### 3.5 Renderização de 150 creeps
- **Creeps** usam **VAT** (*Vertex Animation Textures*, assadas no pipeline Blender → Unity) com `MeshRenderer`-less: são desenhados com **`Graphics.RenderMeshInstanced`** a partir dos arrays da simulação. Os dados por instância (matriz, *clip*, tempo de animação, *hit flash*, dissolução) vão num `NativeArray` de *structs*. Resultado: 1 draw call por tipo de creep, sem `SkinnedMeshRenderer` e sem `GameObject` por creep.
- **Heróis e chefes** usam `SkinnedMeshRenderer` com GPU skinning, porque têm poucas instâncias e animações ricas.
- **Projéteis** usam `RenderMeshInstanced` por tipo.
- **Orçamento:** ≤ 120 draw calls (Medium).

---

## 4. Simulação: Flow Field, Spatial Hash e Tick

### 4.1 Por que simulação híbrida (sem Entities)
Com 150 creeps e cerca de 14 torres, *structs* em `NativeArray` + Jobs/Burst dão o desempenho necessário sem a complexidade de *authoring*, *baking* e depuração do pacote Entities. As views (heróis, VFX, UI) continuam sendo `GameObject`s. Se no futuro o teto de 150 creeps subir para milhares, o modelo de dados já está pronto para migrar.

### 4.2 Tick fixo
- A simulação roda em **tick fixo de 30 Hz** (`Δt = 1/30 s`), acumulado manualmente no `Update`, sem `FixedUpdate`. A renderização roda a 60 FPS com **interpolação** entre os dois últimos estados.
- A velocidade 2× executa 2 ticks por frame de 30 Hz; o tick nunca muda de duração.
- **Determinismo:** mesma *seed* e mesma ordem de execução produzem o mesmo resultado na mesma plataforma (requisito para *replays* de QA). Não se garante igualdade bit a bit entre plataformas.

**Ordem do tick**
```
1. SpawnSystem            (main)     lê o WaveRuntime, aluga slots de creep
2. StatusTickJob          (Burst)    timers de DoT, slow, stun, buffs
3. MoveCreepsJob          (Burst)    flow field + offset de faixa + knockback
4. BuildSpatialHashJob    (Burst)    counting sort por célula
5. TargetingJob           (Burst)    escolhe o alvo de cada torre/herói
6. FireJob                (Burst)    gera os pedidos de projétil e de dano instantâneo
7. ProjectileJob          (Burst)    move os projéteis e resolve os impactos
8. DamageResolveJob       (Burst)    aplica a fórmula da seção 5; marca as mortes
9. Commit                 (main)     ouro, vazamentos, eventos → GameEventBuffer
10. PresentationSync      (main+job) interpolação, instancing, floaty text, VFX
```

### 4.3 Flow Field 128×128 (Burst + Jobs)
| Campo | Tipo | Tamanho |
| :--- | :--- | :--- |
| Custo | `NativeArray<byte>` (1 = caminho, 255 = bloqueado) | 16 KB |
| Integração (distância até o Núcleo) | `NativeArray<ushort>` | 32 KB |
| Fluxo | `NativeArray<float2>` (vetor unitário **2D** no plano XZ) | 128 KB |

- **Célula:** 1 m × 1 m, ou seja, um mapa de 128 × 128 m, compatível com o Spatial Hash (64 × 64 células de 2 m).
- **Custo:** o `LevelLayoutSO` guarda a polilinha do caminho; as células a até `largura/2` dela recebem custo 1, as demais 255.
- **Integração:** `BuildIntegrationJob : IJob` com `[BurstCompile]`. É um BFS a partir das células do Núcleo; como os custos são inteiros pequenos, usa o algoritmo de *Dial* (filas por custo) em vez de Dijkstra com *heap*. Fica abaixo de 0,6 ms para 16.384 células num aparelho Medium.
- **Direção:** `BuildFlowJob : IJobParallelFor` (uma iteração por célula) aponta cada célula para a vizinha (8 direções) de menor integração e normaliza o vetor.
- **Movimento:** `MoveCreepsJob` amostra o fluxo com interpolação bilinear (curvas suaves) e soma um **offset de faixa** fixo por creep (±0,6 m, sorteado no *spawn*). Isso evita creeps empilhados sem custo de *separation steering*.
- **Progresso gratuito:** o valor de integração na posição do creep é a sua **distância até o Núcleo**. A prioridade de alvo "Primeiro" usa esse valor diretamente.
- **Voadores** ignoram o campo e vão em linha reta até o Núcleo, com o progresso medido pela distância euclidiana.
- **Quando recalcular:** só nos eventos de mapa (`MapChangedEvent`, por exemplo uma mecânica de chefe que abre ou fecha um atalho). Como as torres ficam em pedestais fora do caminho, **o jogador nunca bloqueia rotas**, e o campo é estático em 99% das fases.

### 4.4 Spatial Hash
- Grade de 64 × 64 células de 2 m.
- Reconstruída a cada tick por *counting sort* em dois passes (contagem → prefixo → preenchimento), em `NativeArray<int> cellStart/cellCount/entityIndex`. Não usa `NativeParallelMultiHashMap`, para ter custo previsível.
- Consulta de alcance: percorre as células do retângulo envolvente do círculo, testa a distância ao quadrado e aplica os filtros (voador, camuflado sem detecção, morto).

### 4.5 Prioridades de alvo
| Prioridade | Critério |
| :--- | :--- |
| Primeiro (padrão) | Menor distância de integração até o Núcleo |
| Último | Maior distância de integração |
| Mais Forte | Maior HP atual (empate → Primeiro) |
| Mais Perto | Menor distância à torre |

---

## 5. Matemática de Combate

### 5.1 Tipos de dano
**Físico** (mitigado por Armadura), **Mágico** (mitigado por Resistência Mágica) e **Verdadeiro** (ignora mitigação).

### 5.2 Fórmula completa (ordem de aplicação)
$$D_{final} = D_{base} \times (1 + \Sigma B_{atk}) \times C \times Mit \times (1 + \Sigma V)$$

| Termo | Definição |
| :--- | :--- |
| $D_{base}$ | Dano do ataque (já multiplicado pelo poder da Era, seção 8.2) |
| $\Sigma B_{atk}$ | Soma dos buffs de dano do atacante (aditivos entre si) |
| $C$ | Crítico: 2,0 se acertar, senão 1,0 |
| $Mit$ | $\dfrac{100}{100 + R_{ef}}$ para Físico/Mágico; **1** para Verdadeiro |
| $R_{ef}$ | $\max(0,\ R \times (1 - S_{shred}) - P_{flat})$, onde $R$ é Armadura ou RM, $S_{shred} \in [0, 1]$ a redução percentual (usa o **maior** shred ativo, sem somar) e $P_{flat}$ a penetração fixa do atacante |
| $\Sigma V$ | Vulnerabilidade do alvo (aditiva entre fontes, teto de +100%) |

**Escudo** (atributo Shield) absorve $D_{final}$ antes do HP. Habilidades com "remover escudo" (Lutero) zeram o escudo.

**Exemplos de validação (testes unitários, seção 14.1)**
- Armadura 50, físico 120: $120 \times 100/150 = 80$.
- Armadura 80, físico 100, shred de 50% (Alexandros): $R_{ef} = 40$, logo $100 \times 100/140 = 71{,}4$.
- Mágico 100, RM 30, Vulnerabilidade de +40% (Gutenberg): $100 \times 100/130 \times 1{,}4 = 107{,}7$.

### 5.3 Efeitos de controle (CC) e empilhamento
| Efeito | Regra de empilhamento | Chefes |
| :--- | :--- | :--- |
| Lentidão | Vale a **maior** ativa (não soma); teto de 90% | Teto de 60% |
| Atordoamento / Paralisia | Mesma fonte renova a duração; fontes diferentes não somam a duração | **Resistência a CC:** após 3 controles em 10 s, fica imune por 5 s; duração × 0,5 |
| Empurrão (*knockback*) | Converte em metros ao longo da integração (recua no caminho) | Distância × 0,25 |
| Lançar ao ar (*knockup*) | Igual ao atordoamento | 1 s fixo |
| Confusão | Ataca o creep mais próximo e anda no sentido contrário | Vira lentidão de 50% |
| DoT (queimadura, sangramento) | Mesma fonte renova; fontes diferentes somam | Normal |

### 5.4 Faixas de Armadura e RM
| Categoria | Armadura / RM | Redução |
| :--- | :---: | :---: |
| Sem | 0 | 0% |
| Leve | 15 | 13% |
| Média | 40 | 29% |
| Pesada | 100 | 50% |
| Muito pesada | 230 | **70%** |
| Chefe blindado | 150–300 | 60–75% |

---

## 6. Heróis (22) — Dados de Balanceamento

### 6.1 Regras gerais
- **Posicionamento:** livre, com as mesmas regras das torres (§12.3): toca o cartão do herói na loja e depois o chão fora do caminho. O herói ataca o que entra no seu alcance (o maior entre o alcance de ataque e o raio de guarda).
- **Custo de invocação:** ouro da partida, cobrado uma vez. Um herói por *slot* do *loadout*.
- **Morte:** o herói renasce no mesmo ponto após `20 s + 2 s × Era`.
- **Escala:** todos os números abaixo estão **normalizados para a Era 1**. Dentro de um nível da Era *e*, HP e dano são multiplicados por $P(e)$ (seção 8.2), o que mantém os 22 heróis viáveis em qualquer Era.
- **Habilidade:** botão do raio (canto inferior direito, só com o herói em campo), com recarga circular. Habilidades `Point` mostram a **área exata** antes de lançar (Bíblia §3.12); no celular, 1º toque mira e 2º confirma.

### 6.2 Tabela de atributos
F = Físico · M = Mágico · V = Verdadeiro · CD = recarga · r = raio

| # | Herói | Custo | HP | Arm | RM | Ataque básico | Alcance | Habilidade (CD) |
| :-: | :--- | :-: | :-: | :-: | :-: | :--- | :-: | :--- |
| 1 | Krog | 250 | 1.200 | 20 | 0 | 45 F / 1,2 s | 8,0 m (lança arremessada) | **Caça Coordenada de Fogo (30 s):** zona r3 m por 6 s, 40 DPS M: metade direta (com o bônus de +15% por inimigo extra, máx. +150%) e metade como **queimadura** que continua 1,5 s depois de sair da zona |
| 2 | Gara | 200 | 850 | 0 | 15 | 30 F / 0,9 s | 3,5 m | **Raízes do Sedentarismo (25 s):** r4 m, atordoa os terrestres por 2,5 s e depois aplica 60% de lentidão por 5 s |
| 3 | Hammur-Abi | 300 | 1.000 | 15 | 30 | 35 M / 1,3 s | 4,0 m | **Código Retributivo (40 s):** setor de 6 m por 6 s; 200% do dano causado por inimigos no setor volta como V para toda a onda |
| 4 | Trino Socrático | 320 | 900 | 5 | 40 | 3×18 M / 1,0 s | 5,0 m | **Dilema Socrático (35 s):** r4 m, confusão por 5 s |
| 5 | Alexandros | 400 | 1.800 | 45 | 10 | 85 F / 1,5 s (perfura em linha) | 2,0 m | **Falange Implacável (45 s):** empurra a onda 8 m e aplica 50% de shred de armadura por 8 s |
| 6 | Julius | 380 | 1.500 | 35 | 15 | 60 F / 1,1 s | 1,8 m | **Veni, Vidi, Vici (40 s):** após 1 s, bombardeio r3 m de 250 F; mata na hora inimigos com a flag `Light`; 40% de shred por 8 s |
| 7 | Yeshua | 350 | 1.000 | 10 | 50 | 25 M / 1,0 s | 4,5 m | **Purificação Radiante (50 s):** feixe em linha de 12 m; mata na hora `Undead`/`Shadow` (chefe: 30% do HP máx.); +5 vidas no Núcleo (até o valor inicial) |
| 8 | Al-Mu'allim | 420 | 1.100 | 15 | 35 | 40 M / 1,2 s | 4,0 m | **Tempestade do Deserto (60 s):** global por 4 s; inimigos com velocidade 0 (chefe: -60%) e 30 DPS V |
| 9 | Karolus | 450 | 2.200 | 60 | 20 | 95 F / 1,4 s | 1,8 m | **Carga dos Doze Paladinos (40 s):** linha de 10 m ao longo do caminho, 300 F com crítico garantido e atordoamento de 3,5 s |
| 10 | Temüjin | 450 | 1.300 | 25 | 10 | 55 F / 0,7 s | 5,5 m | **Horda Nômade (35 s):** 8 s de flechas em cadeia (70 F, 5 saltos) + sangramento de 20 DPS V por 5 s |
| 11 | Jehanne | 420 | 1.600 | 40 | 30 | 60 M / 1,1 s | 2,0 m | **Estandarte da Fúria Divina (45 s):** r5 m por 8 s; torres no raio ganham +100% de velocidade de ataque; inimigos sofrem 35 DPS M |
| 12 | Gutenberg | 380 | 1.100 | 20 | 20 | 70 F / 1,6 s | 4,5 m | **Prensa de Tipos (30 s):** 3 blocos de 150 F (r1,5 m) + Vulnerabilidade de +40% por 6 s |
| 13 | Colombo | 420 | 1.200 | 15 | 20 | 90 F / 2,0 s | 7,0 m | **Salva das Caravelas (45 s):** 3 salvas de 200 F (r2 m) + 70% de lentidão nos terrestres por 5 s |
| 14 | Leonardo | 480 | 1.000 | 15 | 25 | 40 M / 1,0 s | 5,0 m | **Tanque & Ornitóptero (55 s):** tanque por 10 s (360°, 30 F / 0,4 s, r4 m) + 3 bombas de 120 M com queimadura de 25 DPS por 4 s |
| 15 | Lutero | 450 | 1.700 | 45 | 25 | 80 F / 1,5 s | 1,8 m | **95 Teses (50 s):** onda r5 m; remove escudos e imunidades e zera a armadura por 8 s |
| 16 | Newton | 520 | 1.100 | 10 | 40 | 50 M / 1,2 s | 5,0 m | **Singularidade Gravitacional (60 s):** puxa o r6 m para o centro por 3 s; dano V = 2 × massa (Leve 10, Média 30, Pesada 80, Chefe 200) por tick de 0,5 s |
| 17 | Napoléon | 520 | 1.500 | 30 | 15 | 100 F / 2,2 s (r1,5 m) | 6,0 m | **Grande Bateria (50 s):** 6 balas de 250 F (r2 m); +50% contra `Armored` e chefes |
| 18 | Marx | 480 | 1.300 | 20 | 30 | 45 M / 1,2 s | 4,0 m | **União Proletária (60 s):** drena 25% do HP atual (chefe: 10%) de todos os inimigos; o Núcleo ganha um escudo de `1 + floor(HPdrenado / (1500 × K(e)))` vazamentos (máx. 5) por 20 s |
| 19 | Einstein | 560 | 1.000 | 10 | 45 | 55 M / 1,1 s | 5,0 m | **Dilatação Temporal (70 s):** r6 m, 90% de lentidão por 6 s (chefe: 50%); no fim, explosão V de 15% do HP máx. (chefe: 5%) |
| 20 | Bapu | 400 | 1.400 | 0 | 60 | — (não causa dano) | Aura 4 m | **Resistência Pacífica (45 s):** zona r4 m por 5 s em que os inimigos param e não atacam (chefe: 50% de lentidão). Passiva: inimigos na aura -20% de velocidade |
| 21 | Turing | 500 | 900 | 5 | 35 | 30 M / 0,6 s | 5,0 m | **Overclock Algorítmico (60 s):** 100% de crítico para todas as torres e heróis por 8 s |
| 22 | Commander Neil | 600 | 1.600 | 35 | 35 | 70 F / 1,2 s | 2,0 m | **Um Pequeno Passo (50 s):** r5 m, 200 F + lançamento ao ar de 3 s (chefe: 1 s) |

> Os valores da coluna Habilidade substituem os da Bíblia (§5). Por exemplo, a recarga do Krog é 30 s, não 14 s. Todos são **alvos iniciais** a calibrar no simulador (seção 14.3).

---

## 7. Torres — Matriz de 3 Caminhos (Trava 5-2-0)

### 7.1 Arquétipos
Há **4 arquétipos**. Cada Era troca a **skin** (pelo `EraBiomeThemeSO`) e multiplica o dano por $P(e)$. Os custos são **iguais em todas as Eras**, o que mantém a economia estável.

| Arquétipo | Custo base B | Dano (Era 1) | Cadência | Alcance | Atinge voador | Detecta camuflado |
| :--- | :-: | :--- | :-: | :-: | :-: | :-: |
| **Atirador** (lança / arco / mosquete / rifle) — Era 1: **Caçador** | 100 | 18 F | 0,8 s | 8,0 m | Sim | Só pelo caminho C |
| **Lançador** (pedra / catapulta / canhão / artilharia) — Era 1: **Catapulta de Pedra** | 150 | 40 F, r1,5 m | 2,2 s | 8,0 m | Não | Não |
| **Alquimista** (xamã / sacerdote / alquimista / tesla) — Era 1: **Xamã** | 140 | 30 M | 1,4 s | 8,0 m | Sim | Não |
| **Guarda** (caçadores / hoplitas / cavaleiros / soldados) — Era 1: **Guardião** | 120 | Quartel: 3 soldados (150 HP, 10 F / 1,1 s) que seguram inimigos (§7.7) | — | 8,0 m (alcance dos soldados) | Não | Não |

**Regra de alcance:** tudo que se coloca no mapa alcança no mínimo a **borda oposta do caminho** quando está colado nele. Com posicionamento livre: meia pista + base + pista inteira = 1,75 + 1,3 + 3,5 ≈ 6,6 m; o alcance base de **8,0 m** dá folga. Velocidade do projétil: Atirador 22 m/s, Lançador 11 m/s, Alquimista 16 m/s, Krog 18 m/s; Guarda não dispara.

### 7.2 Caminhos de upgrade (3 × 5 tiers)
| Arquétipo | Caminho A | Caminho B | Caminho C |
| :--- | :--- | :--- | :--- |
| Atirador | **Precisão** (dano e alcance) → T5 *Sniper de Elite*: 600 F, ignora 50% da armadura | **Rajada** (cadência) → T5 *Chuva de Flechas*: 6 projéteis por disparo | **Batedor** (T2 detecta camuflados) → T5 *Marca do Caçador*: alvos marcados sofrem +30% de dano |
| Lançador | **Impacto** (dano e raio) → T5 *Morteiro Pesado*: r3 m, +100% contra `Armored` | **Incendiário** (queimadura) → T5 *Catapulta de Fogo*: área em chamas por 4 s | **Concussão** (atordoar e empurrar) → T5 *Bombarda Sísmica*: atordoa 1,5 s em área |
| Alquimista | **Arcano** (dano perfurante) → T5 *Raio Arcano*: feixe que atravessa 8 alvos | **Corrosão** (shred) → T5 *Ácido Alquímico*: -60% de armadura e RM | **Transmutação** (lentidão) → T5 *Polimorfia*: transforma não-chefes em criatura inofensiva por 3 s |
| Guarda | **Veteranos** (HP e armadura) → T5 *Paladinos de Elite* | **Fúria** (dano) → T5 *Berserkers*: roubo de vida | **Formação** (+soldados e escudo) → T5 *Muralha Viva*: 5 soldados + escudo de grupo |

### 7.3 Custos
$$Custo_{tier}(t) = B \times k_t \qquad k = [0{,}6;\ 1{,}0;\ 2{,}0;\ 5{,}0;\ 15{,}0]$$

| Tier | Atirador (B 100) | Lançador (B 150) | Alquimista (B 140) | Guarda (B 120) |
| :-: | :-: | :-: | :-: | :-: |
| T1 | 60 | 90 | 84 | 72 |
| T2 | 100 | 150 | 140 | 120 |
| T3 | 200 | 300 | 280 | 240 |
| T4 | 500 | 750 | 700 | 600 |
| T5 | 1.500 | 2.250 | 2.100 | 1.800 |

### 7.4 Regra da trava 5-2-0
Estado de upgrade: $(t_A, t_B, t_C)$, cada um em $[0, 5]$. Um upgrade é válido se, **depois** dele:
1. **No máximo 2 caminhos** têm tier maior que 0;
2. **No máximo 1 caminho** tem tier maior que 2.

Combinações máximas: 5-2-0, 2-5-0, 0-2-5, 2-0-5 etc. A UI mostra o caminho travado com um cadeado e o motivo.

```csharp
public struct UpgradeState
{
    public byte A, B, C;
    public byte Get(int path) => path == 0 ? A : path == 1 ? B : C;

    public bool CanUpgrade(int path, int maxTierUnlocked)
    {
        int a = A + (path == 0 ? 1 : 0);
        int b = B + (path == 1 ? 1 : 0);
        int c = C + (path == 2 ? 1 : 0);
        int next = path == 0 ? a : path == 1 ? b : c;
        if (next > maxTierUnlocked) return false;                       // trava de onboarding (1.3)
        int active = (a > 0 ? 1 : 0) + (b > 0 ? 1 : 0) + (c > 0 ? 1 : 0);
        int deep   = (a > 2 ? 1 : 0) + (b > 2 ? 1 : 0) + (c > 2 ? 1 : 0);
        return active <= 2 && deep <= 1;
    }
}
```

### 7.5 Venda
$$Gold_{sell} = \lceil Custo_{acumulado} \times 0{,}70 \rceil$$
**Proposta:** reembolso de 100% se a venda acontecer antes do início da onda seguinte à compra (desfazer).

### 7.6 Mapeamento visual (para o `LevelRenderer`)
O tier mais alto define o estágio visual (Bíblia §7.1: ≤ 1,2 k tris por estágio):

| Maior tier | Estágio visual |
| :-: | :--- |
| 0–2 | L1 (base) + acessórios do caminho |
| 3–4 | L2 do caminho dominante |
| 5 | L3 do caminho dominante |

Os acessórios são malhas modulares acopladas a *sockets*, o que evita 7 modelos completos por arquétipo e Era.

---

### 7.7 Quartel do Guarda — soldados
O Guarda **não ataca**: mantém até `SoldierCount` (3; T5 Formação: 5) soldados que **seguram** inimigos no caminho. Implementação: `CombatSimulation.UpdateSoldiers` (array fixo `SoldierState[MaxUnits × 3]`, zero-GC), rodando depois dos jobs e antes do `Commit`.

| Regra | Valor |
| :--- | :--- |
| Posto | Ponto do caminho mais perto do quartel (dentro do alcance), soldados lado a lado a cada 1,4 m ao longo do trecho |
| Entrada | Saem do quartel em fila (0,3 s + 0,6 s × k) e caminham a **3,5 m/s** |
| Alcance | **O do Guarda (8,0 m)**: vão atrás de qualquer inimigo no círculo do quartel; largam se ele passar 1 m da borda |
| Escolha do alvo | 1º um inimigo **livre** (o mais perto do soldado) → ele **segura** (o inimigo para de andar); sem livres, **ajuda** outro soldado no inimigo já seguro mais perto |
| Quem apanha | Só quem segura. Dano recebido/s = $(12 + 0{,}35 \times massa) \times \frac{HP_{max}}{HP_{base}}$ do inimigo (cresce com fase/onda/Era). Base: Lobo 15,5 · Tigre 22,5 · Rinoceronte 40 · Mamute 82 |
| Golpe do soldado | `SoldierDamage × P(e)` (com modificadores de Dano das melhorias) a cada `SoldierInterval` (1,1 s), alcance de golpe 1,4 m, aplica os efeitos de *on-hit* do Guarda |
| Vida | `SoldierHp × P(e)` (150 na Era 1); parado, recupera **3/s** |
| Morte | Some e renasce no quartel após **10 s** |
| Exceções | Voadores não são seguros. O **chefe** nunca é seguro: os soldados batem nele e ele atinge todos em volta |
| Venda do Guarda | Remove os soldados e solta quem estava seguro |

**Leitura de equilíbrio:** um soldado aguenta ~10 s um Lobo (que morre antes), ~7 s um Tigre e ~4 s um Rinoceronte. Três soldados seguram 3 inimigos e o resto passa: o Guarda **atrasa**, não segura a onda sozinho. Constantes em `CombatSimulation` (`SoldierRespawn`, `SoldierSpeed`, `SoldierReach`, `SoldierSpacing`, `SoldierRegen`, `CreepMeleeBase`, `CreepMeleePerMass`).

---

## 8. Escalonamento de Dificuldade por Era (500 Níveis)

### 8.1 Princípio
A dificuldade vem de dois eixos: **vida dos inimigos relativa ao poder das torres** (esta seção) e **novos atributos** (voadores, camuflados, escudos), introduzidos pelo roteiro de ondas. O ouro **não escala** com a Era, porque os custos das torres são fixos.

### 8.2 Poder e pressão da Era
$$P(e) = 1{,}75^{\,e-1} \qquad R(e) = 1 + 0{,}25\,(e-1) \qquad K_{era}(e) = P(e) \times R(e)$$

| Era | $P(e)$ — poder de torres e heróis | $R(e)$ — pressão | $K_{era}(e)$ — vida dos inimigos |
| :-: | :-: | :-: | :-: |
| 1 | 1,000 | 1,00 | 1,000 |
| 2 | 1,750 | 1,25 | 2,188 |
| 3 | 3,063 | 1,50 | 4,594 |
| 4 | 5,359 | 1,75 | 9,379 |
| 5 | 9,379 | 2,00 | 18,758 |

### 8.3 Vida dos creeps
$$HP(L, w) = HP_{tipo} \times K_{era}(e) \times K_{in}(l) \times Saw(p) \times (1 + \alpha)^{w-1}$$

| Termo | Fórmula | Faixa |
| :--- | :--- | :--- |
| $K_{in}(l)$ — progresso dentro da Era | $1 + 0{,}008\,(l - 1)$ | 1,000 → 1,792 |
| $Saw(p)$ — dente de serra do capítulo | $0{,}92 + 0{,}02\,(p - 1)$ | 0,92 (fase 1) → 1,10 (chefe) |
| $\alpha$ — crescimento por onda | Normal **0,08** · Hard **0,11** | onda 25: 6,34× / 12,24× |

**Dificuldade relativa** (vida ÷ poder): $D = R(e) \times K_{in}(l) \times Saw(p)$. Ela cresce dentro de cada Era, alivia na virada (Era nova, heróis novos) e sobe de patamar a cada Era: vai de 0,92 (nível 1) a 3,94 (nível 500).

### 8.4 Vida dos chefes
$$HP_{chefe}(L) = B_{chefe} \times K_{era}(e) \times K_{in}(l) \times Saw(10)$$
Os chefes **não** usam $\alpha$: sempre aparecem na onda 10.

| Fase | Chefe | $B_{chefe}$ | HP final |
| :-: | :--- | :-: | :-: |
| 10 | Mamute-Lanudo Alfa | 3.000 | **3.538** |
| 50 | Terror Alado | 6.000 | **9.187** |
| 100 | Fera Primordial da Entropia | 12.000 | **23.654** |
| 150 | Dario III & Elefantes | 8.000 | **26.796** |
| 500 | A Entropia (5 fases, HP total) | 40.000 | **1.479.016** |

### 8.5 Ouro da partida
| Fonte | Fórmula |
| :--- | :--- |
| Abate | $Gold_{kill} = \lceil HP_{tipo} / 20 \rceil$ (usa o HP **base** do tipo, sem escala) |
| Fim de onda | $Gold_{wave} = 50 + 5w$ |
| Ouro inicial | $G_0(l) = \min(800,\ 300 + 5\,(l - 1))$ |
| Chefe abatido | 150 fixo |
| Chamar a onda mais cedo | +1 ouro por segundo antecipado (máx. 30) |

### 8.6 Invariante de balanceamento (verificado pelo simulador)
Para cada onda $w$ de cada nível:
$$\sum_{torres\ compráveis} DPS_{ef}(Ouro_{acum}(w)) \times T_{cobertura} \ \ge\ 1{,}2 \times \sum_{creeps\ da\ onda} HP_{ef}$$
onde $T_{cobertura}$ é o tempo que os creeps passam no alcance das torres e $HP_{ef}$ é o HP dividido por $Mit$. Os níveis que violarem a invariante são reprovados no CI (seção 14.3).

---

## 9. Bestiário Mecânico e Roteiro da Fase 10

### 9.1 Flags de inimigo (`EnemyTraits`, *bitmask*)
| Flag | Comportamento | Contra |
| :--- | :--- | :--- |
| `Armored` | Armadura da faixa Pesada ou acima (5.4) | Mágico, shred, Napoléon |
| `MagicResist` | RM da faixa Pesada ou acima | Físico pesado |
| `Flying` | Ignora o flow field; o Guarda e o Lançador não o atingem | Atirador, Alquimista |
| `Camo` | Invisível sem detecção | Atirador do caminho C (T2+) |
| `Regen` | +2% do HP máximo por segundo | Dano de rajada, Lutero |
| `Shield` | Camada extra = 50% do HP | Lutero, alto DPS |
| `Healer` | Cura 5% do HP por segundo dos aliados num r3 m | Atirador A (alvo único) |
| `Spawner` | Gera 3 *minions* ao morrer | Lançador (área) |
| `Light` | Morre na hora para Julius | — |
| `Undead` / `Shadow` | Morre na hora para Yeshua | — |
| `Boss` | Regras de chefe (5.3) | — |

### 9.2 Fase 10 — Covil do Krog (Era 1, Capítulo 1)
**Parâmetros:** 10 ondas · ouro inicial 345 ($G_0(10)$) · 20 vidas · posicionamento livre · tier máximo T2 (1.3) · torres disponíveis: Atirador e Lançador · sem herói. Layout: **Covil do Krog** (§10.7), sem caverna nem Núcleo: o Mamute entra pela borda direita.
Fator de escala da fase: $K_{era} \times K_{in}(10) \times Saw(10) = 1 \times 1{,}072 \times 1{,}10 = 1{,}1792$.

| Onda | Composição | HP por unidade | Arm | Vel. | Ouro/abate |
| :-: | :--- | :-: | :-: | :-: | :-: |
| 1 | 8 Lobos-Cinzentos | 83 | 0 | 2,2 m/s | 4 |
| 2 | 10 Lobos | 89 | 0 | 2,2 | 4 |
| 3 | 14 Lobos | 96 | 0 | 2,2 | 4 |
| 4 | 5 Tigres Dente-de-Sabre + 6 Lobos | 267 / 104 | 15 / 0 | 1,6 / 2,2 | 9 / 4 |
| 5 | 7 Tigres + 6 Lobos | 289 / 112 | 15 / 0 | — | — |
| 6 | 9 Tigres | 312 | 15 | 1,6 | 9 |
| 7 | 3 Rinocerontes-Lanudos + 6 Lobos | 842 / 131 | 40 / 0 | 0,9 / 2,2 | 23 / 4 |
| 8 | 4 Rinocerontes + 4 Tigres | 909 / 364 | 40 / 15 | — | — |
| 9 | 6 Rinocerontes | 982 | 40 | 0,9 | 23 |
| 10 | **Mamute-Lanudo Alfa** + escolta de 6 Lobos | **3.538** / 165 | 40 (RM 0) | 0,5 / 2,2 | 150 / 4 |

$HP_{tipo}$: Lobo 70 · Tigre 180 · Rinoceronte 450 · Caçador Corrompido 120.

**Mecânicas do chefe Mamute-Lanudo Alfa**
1. **Pisão Glacial** (a cada 12 s): telegrafado por 1,5 s sobre uma unidade em campo, que fica **congelada por 3 s**.
2. **Chamado da Manada** (aos 66% e aos 33% de HP): 4 Lobos-Cinzentos entram pela borda de entrada.
3. **Resgate (vitória):** com HP = 0, o mamute se dissolve e a cinemática de resgate começa (4 s, Bíblia §3.6); o Krog entra no acervo e no *loadout*.

**Alcance mínimo:** ver §7.1 (8,0 m para todos; nos 5 layouts da Era 1 o pior ponto sugerido pede 7,05 m, `MvpDataGenerator.RequiredReach`).

**O que a fase testa:** posicionar Lançadores nas curvas internas (cobrem 2 trechos) e espalhar as unidades para que o Pisão Glacial não congele a defesa inteira.

---

## 10. Estrutura de Dados — ScriptableObjects

### 10.1 Hierarquia
```
CampaignSO
 └── EraSO ×5 ──────────────► EraBiomeThemeSO (visual)
      └── LevelDataSO ×100
           ├── LevelLayoutSO       (caminho, pedestais, covil, Núcleo; reutilizável)
           ├── WaveDataSO ×10–25
           │    └── SpawnGroup[] ──► EnemyDataSO ──► (opcional) BossDataSO
           ├── BossDataSO          (só p = 10)
           └── Rewards             (HeroDataSO desbloqueado, torre, cosmético)

HeroDataSO ──► SkillDataSO
TowerDataSO ──► UpgradePath[3] ──► UpgradeTier[5]
VfxDataSO · AudioCueSO
```

**Princípios**
- Os SOs são **somente leitura em tempo de execução**. O estado mutável fica em *structs* de runtime (`EnemyState`, `TowerState`…), copiadas dos SOs no carregamento para `NativeArray`s.
- Prefabs e texturas são referenciados por **`AssetReference`** (Addressables), nunca por referência direta, para não carregar as 5 Eras na memória.
- Todo SO tem um `int Id` estável, usado em *saves*, analytics e no `NativeArray`. Nada é identificado por string em runtime.

### 10.2 `EraBiomeThemeSO`
```csharp
[CreateAssetMenu(menuName = "Chrono/Era Biome Theme")]
public sealed class EraBiomeThemeSO : ScriptableObject
{
    public int Id;
    public int EraIndex;                         // 1..5

    [Header("Paleta (Bíblia §4)")]
    public Color AccentRune;                     // cor das runas do caminho
    public Color ShadowTint;                     // cor da sombra no toon ramp
    public Color CorruptionTint = new(0.23f, 0.12f, 0.33f);

    [Header("Iluminação URP")]
    public Color MainLightColor;
    public float MainLightIntensity = 1f;
    public Vector3 MainLightEuler = new(50f, 135f, 0f);
    public Color AmbientSky, AmbientEquator, AmbientGround;
    public Color FogColor; public float FogStart = 40f, FogEnd = 90f;
    public AssetReferenceT<VolumeProfile> PostProcess;

    [Header("Prefabs de cenário")]
    public AssetReferenceGameObject Backdrop;    // obsoleto v2.1 (sem céu)
    public AssetReferenceGameObject Pedestal;    // obsoleto v2.1 (posicionamento livre)
    public AssetReferenceGameObject RuneDecal;
    public AssetReferenceGameObject Portal;      // obsoleto v2.1 (sem bases); pode virar prop de moldura
    public AssetReferenceGameObject Core;        // obsoleto v2.1 (Núcleo não aparece no mapa)
    public PropSet[] Props;                      // conjuntos ponderados por sub-bioma

    [Header("Skins de torre por arquétipo (índice = TowerArchetype)")]
    public TowerSkin[] TowerSkins;

    public AudioCueSO Music;
}

[System.Serializable] public struct TowerSkin
{
    public TowerArchetype Archetype;
    public AssetReferenceGameObject BaseL1;
    public AssetReferenceGameObject[] PathL2;    // 3 (um por caminho)
    public AssetReferenceGameObject[] PathL3;    // 3
}
```

### 10.3 `HeroDataSO` e `SkillDataSO`
```csharp
[CreateAssetMenu(menuName = "Chrono/Hero")]
public sealed class HeroDataSO : ScriptableObject
{
    public int Id;                               // 1..22 (Bíblia §5)
    public LocalizedString DisplayName;
    public int Era;
    public int UnlockLevel;                      // ex.: Krog = 10
    public int SummonCost;

    public float MaxHp, Armor, MagicResist;      // normalizados na Era 1
    public DamageType AttackType;
    public float AttackDamage, AttackInterval, AttackRange;
    public float GuardRadius = 3.5f;
    public float MassClass;                      // usado por Newton (Leve, Média…)

    public SkillDataSO Skill;
    public AssetReferenceGameObject ViewPrefab;  // SkinnedMesh + Animator
    public AssetReferenceSprite Portrait;
    public SensitivityFlags Sensitivity;         // Bíblia §8 (revisão obrigatória)
}

[CreateAssetMenu(menuName = "Chrono/Skill")]
public sealed class SkillDataSO : ScriptableObject
{
    public int Id;
    public float Cooldown, CastTime;
    public SkillTargeting Targeting;             // Point, Global, Self, Line, Sector
    public float Radius, Length, Duration;
    public SkillEffect[] Effects;                // aplicados em ordem
    public AssetReferenceGameObject Vfx;
}

[System.Serializable] public struct SkillEffect      // blittable → copiado para NativeArray
{
    public EffectType Type;      // Damage, DoT, Slow, Stun, Knockback, Knockup, Confuse,
                                 // Shred, Vulnerability, RemoveShield, Pull, Reflect,
                                 // HealCore, CoreShield, CritBuff, AttackSpeedBuff, Execute
    public DamageType DamageType;
    public float Magnitude, Duration, TickInterval;
    public float BossMultiplier;                  // ex.: Confuse → 0 (vira Slow), Pull → 0,5
    public EnemyTraits RequiresTraits;           // ex.: Execute só em Undead|Shadow
}
```
As habilidades são **dados**, não classes: um único `SkillSystem` em Burst interpreta o array de `SkillEffect`. Habilidades novas raramente exigem código novo.

### 10.4 `TowerDataSO`
```csharp
[CreateAssetMenu(menuName = "Chrono/Tower")]
public sealed class TowerDataSO : ScriptableObject
{
    public int Id;
    public TowerArchetype Archetype;
    public int BaseCost;
    public DamageType DamageType;
    public float Damage, Interval, Range, SplashRadius;
    public bool HitsFlying, DetectsCamo;
    public UpgradePath[] Paths = new UpgradePath[3];
}

[System.Serializable] public struct UpgradePath
{
    public LocalizedString Name;
    public UpgradeTier[] Tiers;                  // 5
}

[System.Serializable] public struct UpgradeTier
{
    public float CostMultiplier;                 // k_t (7.3)
    public StatModifier[] Modifiers;             // Add/Mul em Damage, Interval, Range…
    public SkillEffect[] OnHitEffects;           // ex.: queimadura, shred
    public bool GrantsCamoDetection;
    public string SocketAccessory;               // só para o pipeline de arte (editor)
}
```

### 10.5 `EnemyDataSO`, `BossDataSO` e `WaveDataSO`
```csharp
[CreateAssetMenu(menuName = "Chrono/Enemy")]
public class EnemyDataSO : ScriptableObject
{
    public int Id;
    public float BaseHp, Armor, MagicResist, Speed;
    public EnemyTraits Traits;                   // [Flags] (9.1)
    public int LeakWeight = 1;
    public float Mass = 10f;
    public Mesh VatMesh; public Material VatMaterial;   // instancing (3.5)
    public int GoldOverride = -1;                // -1 → ceil(BaseHp / 20)
}

[CreateAssetMenu(menuName = "Chrono/Boss")]
public sealed class BossDataSO : EnemyDataSO
{
    public float HpBase;                         // B_chefe (8.4)
    public BossPhase[] Phases;                   // gatilhos por % de HP
    public AssetReferenceGameObject ViewPrefab;  // SkinnedMesh (não VAT)
    public AssetReferenceGameObject LairPrefab;  // covil (Bíblia §3.6)
    public LocalizedString LairSignText;         // "Krog's Lair"
}

[System.Serializable] public struct BossPhase
{
    [Range(0, 1)] public float HpThreshold;
    public BossAction Action;                    // Summon, DisablePedestal, Shield, MapChange…
    public float Interval, Value;
    public EnemyDataSO SummonType; public int SummonCount;
}

[CreateAssetMenu(menuName = "Chrono/Wave")]
public sealed class WaveDataSO : ScriptableObject
{
    public SpawnGroup[] Groups;
    public int BonusGoldOverride = -1;           // -1 → 50 + 5w
}

[System.Serializable] public struct SpawnGroup
{
    public EnemyDataSO Enemy;
    public int Count;
    public float StartDelay, Interval;
    public int SpawnPointIndex;                  // portal ou covil
}
```

### 10.6 `LevelDataSO` e `LevelLayoutSO`
```csharp
[CreateAssetMenu(menuName = "Chrono/Level")]
public sealed class LevelDataSO : ScriptableObject
{
    public int GlobalIndex;                      // L (1..500)
    public EraSO Era;
    public LevelLayoutSO Layout;
    public WaveDataSO[] Waves;
    public BossDataSO Boss;                      // null se p != 10
    public int StartGoldOverride = -1, Lives = 20;
    public HeroDataSO UnlocksHero;
    public LevelRewards Rewards;
}

[CreateAssetMenu(menuName = "Chrono/Level Layout")]
public sealed class LevelLayoutSO : ScriptableObject
{
    public int Id;
    public Vector3[] PathPolyline;               // usado no bake do flow field
    public float PathWidth = 3.5f;               // v2.1: 3,5 m em mapas novos
    public Vector3[] Pedestals;                  // v2.1: só pontos SUGERIDOS (invisíveis); construção é livre
    public Vector3 LairPosition, CorePosition;   // = primeiro e último ponto do caminho (fora da tela)
    public Bounds CameraBounds;                  // quadro do mapa (caminho + 4,5 m), §10.7
    public bool ShowLairAndCore;                 // v2.1: false em todas as fases (sem bases)
    public GroundStyle Ground;                   // Auto = bioma da Era · Tundra · Meadow (teste)
    public AssetReferenceGameObject EnvironmentArt;   // cenário pintado deste layout
}
```
**Produção de 500 níveis:** os níveis **compartilham layouts** (proposta: cerca de 100 layouts, 20 por Era) e variam ondas, sub-bioma e props. Uma ferramenta de editor (`LevelGenerator`) gera os `LevelDataSO` a partir de modelos de onda + as fórmulas da seção 8, e o designer ajusta só as exceções.

---

### 10.7 Mapa-modelo "Prado" (padrão para todos os mapas)
Menu: **Tools ▸ Chrono ▸ Fase de teste visual (estilo BTD6)** (cria `LAY_TEST_Prado` + `LVL_TEST_Prado` e coloca na cena Gameplay); **Voltar para a Fase 10 (chefe)** desfaz.

**Especificação do layout:**

| Item | Regra | Prado |
| :--- | :--- | :--- |
| Largura do caminho | 3,5 m | 3,5 m |
| Traçado | Polilinha ortogonal (ou suave), **sem cruzamentos** | 13 pontos, 11 curvas |
| Distância entre eixos de trechos vizinhos | ≥ 6,5 m (3 m de chão entre bordas; o *flow field* precisa de ≥ 2 células bloqueadas entre trechos) | 6,5 m mín. |
| Entrada e saída | Primeiro e último ponto **fora do quadro** (≥ 8 m além da borda); `LairPosition`/`CorePosition` = esses pontos | entra pela esquerda, sai por baixo |
| Quadro (`CameraBounds`) | Caixa do caminho visível + **4,5 m** de cada lado (meia pista + uma unidade); altura 1 m | 49 × 41,5 m |
| Proporção do quadro | Próxima da área de jogo na tela (≈ 1,2–1,5 de largura por profundidade) para preencher sem sobra | 1,18 |
| Comprimento do caminho | ~150–180 m na Era 1 (Lobo a 2,2 m/s atravessa em ~75 s) | 164 m |
| Pontos sugeridos | 4–8, nas curvas internas (o bot do simulador usa) | 6 |
| Bases | Nenhuma (`ShowLairAndCore = false`) | — |
| Chão e props | Bioma **da fase** (`LevelDataSO.Biome`, Bíblia §4.1.1: segue a estrada do mapa da saga); props altos só na moldura | teste: prado |

**Layouts da Era 1 (capítulo 1), refeitos em 27/11** — `MvpDataGenerator.Layouts`, chão de Tundra, sem bases:

| Fases | Layout | Traçado | Quadro | Caminho visível | Menor distância entre trechos |
| :-: | :--- | :--- | :-: | :-: | :-: |
| 1–2 | `LAY_E1_A_ValeGelado` ("Vale do Rio") | S largo: entra à esquerda, sai à direita (onboarding) | 45 × 33 m | 141 m | 12 m |
| 3–4 | `LAY_E1_B_TrilhaDosLobos` | Zigue-zague vertical de 5 colunas: entra no topo, sai embaixo | 45 × 37 m | 185 m | 9 m |
| 5–6 | `LAY_E1_C_LagoCongelado` ("Pedras Erguidas") | Serpentina de 4 faixas: entra e sai pela direita | 45 × 39 m | 183 m | 10 m |
| 7–9 | `LAY_E1_D_Desfiladeiro` | O traçado do "Prado" (11 curvas) | 49 × 41,5 m | 171 m | 6,5 m |
| 10 | `LAY_E1_E_CovilDoKrog` | Serpentina de 4 faixas: entra à direita, sai à esquerda (chefe) | 45 × 39 m | 147 m | 10 m |

**Biomas por fase (27/11):** `MvpDataGenerator.LevelBiome` = Mata (1–4) · Savana (5–7) · Deserto (8) · Planalto (9) · Tundra (10). O bioma fica na fase, não no layout, porque fases que dividem um layout podem estar em biomas diferentes (7–9). Ordem de resolução: `LevelDataSO.Biome` → `LevelLayoutSO.Ground` → Era. A semente da decoração usa o `GlobalIndex` da fase. Enum `GroundStyle` só cresce no fim (valores 0–2 já salvos em assets).

Cada layout tem 8 pontos sugeridos nas curvas internas (alcance exigido ≤ 7,05 m). `LevelLayoutSO.Ground = Auto` usa o bioma da Era (Era 1: neve pintada, poças de gelo, capim seco, lajotas mais escuras); o mapa de teste usa `Meadow`.

**Câmera e escala** (iguais em todos os mapas): pitch 55°, FOV 26°, quadro na tela menos a loja (14%); escala visual 3,6× (campeões/heróis) e 2,5× (inimigos) — Bíblia §3.2 e §3.10.

**Fase de teste:** `GlobalIndex` 31 (as 4 torres liberadas), 20 vidas, 650 de ouro, ondas da fase 3.

**Checklist de um mapa novo:** (1) traçar a polilinha no papel quadriculado de 1 m; (2) conferir os 6,5 m entre trechos e a ausência de cruzamentos; (3) estender início e fim 8 m para fora; (4) quadro = caminho visível + 4,5 m; (5) 4–8 pontos sugeridos; (6) testes `MvpDataTests`/`CameraRigTests` e uma partida com o bot.

---

## 11. Interface com o `LevelRenderer`

### 11.1 Separação simulação × apresentação
A simulação **nunca** referencia o `LevelRenderer` nem `GameObject`s. A comunicação acontece por dois canais, ambos sem alocação:
1. **Pontos de construção**: a simulação cria o ponto onde o jogador construiu (`CombatSimulation.AcquireSpot`) e a apresentação move os visuais para lá (`LevelRenderer.MoveSpot`). Capacidade fixa `MaxSpots = 32`, sem alocação.
2. **`GameEventBuffer`**: um *ring buffer* pré-alocado de `struct GameEvent` que a simulação escreve no `Commit` e a apresentação consome no mesmo frame.

```csharp
public enum GameEventType : byte
{ Damage, Kill, Leak, TowerBuilt, TowerUpgraded, TowerSold, PedestalDisabled,
  SkillCast, WaveStarted, BossSpawned, BossPhase, BossDefeated, HeroRescued, MapChanged }

public struct GameEvent
{
    public GameEventType Type;
    public int A, B;            // ids (torre, pedestal, inimigo, skill…)
    public float3 Position;
    public float Value;         // dano, ouro…
}
```

### 11.2 Contrato
> **Nomes no código:** desde v2.1, "pedestal" nos identificadores (`GetPedestalPosition`, `PedestalUnit`, `PedestalDisabled`…) significa **ponto de construção livre** (§12.3). Os nomes foram mantidos para não quebrar código e testes. O contrato ganhou `MoveSpot(i, pos)`, `Throw(i, intervalo)`, `SetAim(i, alvo)`, `PlaySkillThrow(i, ponto)` e `BuildPortraits()`.

```csharp
public interface ILevelRenderer
{
    // Carrega o cenário, aplica o tema e instancia pedestais, runas, covil e Núcleo.
    Awaitable LoadAsync(LevelDataSO level, EraBiomeThemeSO theme, CancellationToken ct);

    // Âncoras em espaço de mundo (índices iguais aos do LevelLayoutSO).
    Vector3 GetPedestalPosition(int pedestalIndex);

    // Estados visuais (Bíblia §3.4–3.6).
    void SetPedestalState(int pedestalIndex, PedestalVisualState state);
    void SetRuneState(RuneVisualState state);                // Idle, WaveActive, BossWave
    void ShowTowerStage(int pedestalIndex, TowerArchetype a, int path, int stage); // 7.6
    void ClearPedestal(int pedestalIndex);
    Awaitable PlayLairOpenAsync(CancellationToken ct);       // abertura do covil (2 s)
    Awaitable PlayRescueAsync(HeroDataSO hero, CancellationToken ct);

    void ApplyQualityTier(QualityTier tier);                 // Low / Medium / High
    void Unload();                                           // libera handles do Addressables
}

public enum PedestalVisualState : byte { Free, Highlighted, Selected, Occupied, UpgradeAvailable, Disabled, Locked }
public enum RuneVisualState     : byte { Idle, WaveActive, BossWave }
```

### 11.3 O que `LoadAsync` faz (em ordem)
1. Carrega via Addressables o `Layout.EnvironmentArt`, o `theme.Backdrop` e o `PostProcess`.
2. Aplica a iluminação: `RenderSettings` (ambiente em gradiente, neblina) e a rotação, cor e intensidade da luz principal a partir do tema.
3. Cria os visuais de cada ponto possível (anel de seleção, aviso do chefe, sombra de contato, item na mão) e o estoque de torres 3D por tipo (12 por arquétipo), tudo desativado. Sem pedestal visível.
4. Distribui `theme.RuneDecal` ao longo de `PathPolyline` a cada 3 m, orientado no sentido do fluxo (*UV scroll*).
5. Pinta o chão (bioma da Era), o caminho de lajotas (sombra + contorno + lajotas) e espalha os decalques e a moldura de props. Covil e Núcleo só se `ShowLairAndCore` (desligado em v2.1).
6. Pré-carrega as skins de torre da Era (`TowerSkins`) para que `ShowTowerStage` não carregue nada durante a partida.
7. Pré-carrega as views dos heróis do *loadout* e do chefe, e fotografa os modelos 3D para a loja (`BuildPortraits`, RenderTextures 256²).
8. Devolve o controle; o `LevelLoader` então roda o *prewarm* dos pools (3.4), o *bake* do flow field (4.3) e o `GC.Collect()`.

**Regra:** nenhum método do `ILevelRenderer` chamado durante a partida pode carregar asset ou alocar. Todo carregamento ocorre em `LoadAsync`.

---

### 11.4 Unidades, projéteis, soldados e efeitos (apresentação, zero-GC)
- **Inimigos, projéteis, soldados, chão em fogo e chamas** são desenhados com `Graphics.RenderMeshInstanced` a partir dos arrays da simulação (`UnitRenderer`), 1 *draw call* por tipo.
- **Animação procedural** com o relógio da simulação (`TickCount × Dt`): inimigos saltitam e balançam (param quando atordoados ou seguros); campeões quicam ao surgir, respiram, giram para o alvo (`TowerState.Target`) e recuam a cada disparo (a recarga subiu).
- **Projéteis:** o arco é só visual: `ProjectileState.Origin` + `TargetPos` definem a parábola; sai de `ArtLibrary.HandLocal[tipo]` (mão do modelo, virada para o alvo). Item na mão some no disparo e volta após `min(0,9 s; intervalo × 0,5)`.
- **Habilidade em zona (`SkillZone`):** guarda `Duration` para o visual (estouro ao cair, apagar no fim). Inimigos com `DotTimer > 0` mostram chama no corpo.

---

## 12. HUD, Entrada e Fluxos de Controle

### 12.1 Layout (alinhado à Bíblia §3.9)
| Região | Elementos |
| :--- | :--- |
| Superior esquerda (sobre o mapa) | Vidas · Ouro |
| Superior direita do mapa | Onda X/N · próxima onda e bônus de adiantar · Pausa |
| Superior central | Barra do chefe (retrato, nome, HP), só com chefe em campo |
| Coluna direita (14%) | **Loja**: cabeçalho com nome e função · cartão do herói · cartões das torres (foto + preço) · raio da habilidade · botão verde ▶/▶▶ |
| Esquerda (contextual) | Painel de melhorias da unidade tocada: foto, nome, alvo, 3 caminhos (cadeado da trava 5-2-0), vender |
| Inferior central (contextual) | Dica durante posicionamento/mira |

Velocidades: **1× e 3×** (um botão alterna, como no BTD6). Entre ondas o mesmo botão chama a próxima onda com o bônus de §8.5.

### 12.2 Gestos
| Gesto | Ação |
| :--- | :--- |
| Toque no cartão da loja | Entra no modo de posicionamento (cabeçalho mostra o nome); tocar de novo cancela |
| Mouse sobre o mapa (PC) | Prévia do alcance segue o cursor: azul = pode, vermelho = não pode; clique constrói |
| Toque no chão (celular) | 1º toque mostra a prévia; 2º toque a ≤ 1,5 m confirma |
| Toque numa unidade (≤ 2 m) | Seleciona e abre as melhorias |
| Toque no raio → toque no mapa | Mira com a área exata da habilidade; mesmo esquema de prévia/confirmação |
| Pinça / roda | Zoom 0,5×–1,15× (opcional; o jogo é feito para ser jogado sem zoom) |

### 12.3 Posicionamento livre (sem física)
O toque gera um raio da câmera intersectado com o plano `y = 0`. `CombatSimulation.CanPlaceAt` aceita o ponto se: está dentro do quadro com a base inteira; está a ≥ meia pista + **1,3 m** (raio da base) do eixo do caminho; está a ≥ 4 m de covil/Núcleo (se existirem); e não encosta em outra unidade (centros a ≥ 2,6 m). `AcquireSpot` reaproveita um ponto vazio ou cria um novo (máx. 32). Tudo em laços sobre arrays fixos, sem colliders e sem alocação.

---

## 13. Economia, Monetização e LiveOps

### 13.1 Fair-play (zero pay-to-win)
- Não se vende poder: nada de boosters de dano, vidas extras permanentes ou ouro de partida.
- **Gemas** servem apenas para cosméticos e para o acesso antecipado a *Side Stories*, que também são liberadas de graça com 100% das estrelas da Era.
- Não há caixas de recompensa aleatórias (*loot boxes*).

### 13.2 Produtos
| Produto | Detalhe |
| :--- | :--- |
| Passe de Era | R$ 29,90 por temporada de 60 dias: skins, animações de vitória, molduras |
| Skins de herói | Modelo 3D, animações e VFX alternativos, sem alterar atributos |
| Side Stories | 10 fases cada (ex.: *Termópilas*, *Queda de Constantinopla*) |
| Anúncios premiados (opcionais) | Dobrar o ouro de conta após uma vitória ou 1 nova tentativa após derrota. Nunca forçados |

### 13.3 LiveOps
- **Desafio semanal:** um nível existente com modificadores (sem Alquimistas, apenas heróis da Era 3 etc.) e placar por vazamentos e tempo.
- **Eventos de efeméride:** datas históricas (20/07, a chegada à Lua; 15/03, os Idos de Março) com cosméticos temáticos.
- **Balanceamento remoto:** um `BalanceOverrides` JSON (assinado) pode sobrescrever multiplicadores de $\alpha$, $K_{in}$ e de cada SO sem novo build. É aplicado **no carregamento**, nunca durante a partida.

---

## 14. Ferramentas, Testes e Critérios de Aceite

### 14.1 Testes unitários (EditMode)
- Fórmula de dano (5.2) com os três exemplos de validação.
- `UpgradeState.CanUpgrade` com todas as 216 combinações de $(t_A, t_B, t_C)$.
- Fórmulas de escala (8.3–8.5) contra a tabela 8.4.
- Flow field: toda célula de caminho chega ao Núcleo; nenhum vetor nulo em célula transitável.
- Posicionamento livre: aceita grama, recusa caminho/borda/sobreposição; construir não aloca (`FreePlacement_*`).
- Soldados do Guarda: saem, seguram, apanham e renascem (`GuardSoldiers_SpawnBlockFightAndRespawn`).
- Câmera: o quadro cabe na tela menos a loja e a preenche em 16:9, 19,5:9 e 4:3 (`CameraRigTests`).
- Alcance: todos os alcances ≥ o exigido pelo layout (`EveryPedestalUnit_ReachesFarSideOfPath`).

### 14.2 Teste de alocação zero (PlayMode, roda no CI e em aparelho)
```csharp
[UnityTest]
public IEnumerator StressWave_AllocatesZero()
{
    yield return LoadLevel("Test_Stress_150");     // 150 creeps, 14 torres T5, 2 heróis
    using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    for (int f = 0; f < 1800; f++)                 // 60 s a 30 Hz
    {
        yield return null;
        Assert.AreEqual(0, gc.LastValue, $"GC no frame {f}");
    }
}
```
A mensagem com string só é montada quando a asserção falha, então não contamina a medição.

### 14.3 Simulador de balanceamento (headless)
Ferramenta de editor que roda a simulação Burst **sem renderização**, em velocidade máxima, com um "jogador-bot" que posiciona e evolui torres por heurística gulosa (maior DPS efetivo por ouro, por ponto sugerido do layout). Para cada um dos 500 níveis, ele:
- verifica a invariante 8.6;
- mede a taxa de vitória em 50 *seeds* (meta: 70–90% no Normal para o bot);
- exporta um CSV com a curva de dificuldade, que vira um gráfico no relatório de CI.

### 14.4 Critérios de aceite de performance
| Métrica | Medium | Low |
| :--- | :-: | :-: |
| FPS (P95 de frame time) | ≥ 60 (≤ 16,7 ms) | ≥ 30 (≤ 33 ms) |
| GC durante a partida | 0 B | 0 B |
| Carregamento de nível | ≤ 4 s | ≤ 7 s |
| Memória de pico | ≤ 900 MB | ≤ 600 MB |
| Partida de 20 min sem *throttling* térmico | Sim | Sim |

---

## 15. Plano do MVP
Escopo: **Era 1, Capítulo 1 (fases 1–10)**, encerrando no chefe **Mamute-Lanudo Alfa**.

| Marco | Entrega |
| :--- | :--- |
| M1 — Núcleo | Tick, flow field (Burst), Spatial Hash, dano (5.2), pools, teste GC = 0 |
| M2 — Torres | Atirador e Lançador com 3 caminhos até T2 (trava de onboarding), loja lateral e posicionamento livre |
| M3 — Conteúdo | `LevelRenderer` + `EraBiomeThemeSO` da Tundra, 10 `LevelDataSO`, 3 creeps (VAT) |
| M4 — Chefe | `BossDataSO` do Mamute-Lanudo Alfa, covil, cinemática de resgate, Krog jogável na fase 11 (teste) |
| M5 — Polimento | HUD, estrelas, *save*, tiers de qualidade, testes em 3 aparelhos de referência |

---

## 16. Pontos em Aberto

| # | Questão | Proposta |
| :-: | :--- | :--- |
| 1 | Progressão permanente (meta) de heróis e torres? | Não incluída: o poder vem de $P(e)$ e de desbloqueios. Avaliar níveis de herói só cosméticos |
| 2 | Quantidade de layouts únicos | ~100 (20 por Era) |
| 3 | Reembolso de 100% antes da próxima onda | Proposto; validar em playtest |
| 4 | Número de heróis por partida | 2 |
| 5 | Os valores de $R(e)$, $\beta = 0{,}008$ e $k_t$ | Iniciais; calibrar no simulador (14.3) |
| 6 | Atualizar a Bíblia §7.1 (60 creeps e 30 FPS no Medium) | Substituídos por este GDD (150 creeps com VAT; 60 FPS no Medium) |
| 7 | ~~Refazer os layouts das fases 1–10 no modelo "Prado"~~ | **Feito (27/11):** 5 layouts (§10.7) |
| 8 | Nomes dos campeões das Eras 2–5 (Era 1: Caçador, Catapulta de Pedra, Xamã, Guardião) | Definir na Bíblia por Era |
| 9 | Posição da mão de cada modelo final (`ArtLibrary.HandLocal`) | Ajustar quando os modelos do Tripo entrarem |
