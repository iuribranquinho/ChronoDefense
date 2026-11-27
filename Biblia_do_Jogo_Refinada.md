# Chrono Defense — Bíblia do Jogo Refinada
### Universo, Narrativa, Direção de Arte 2.5D e Especificação de Assets

**Motor:** Unity 6 · URP (Mobile) · **Gênero:** Tower Defense Histórico · **Campanha:** 500 fases
**Versão:** 2.1 (27/11/2026) · **Status:** Referência oficial de design, narrativa e arte
**Padrão vigente:** o **mapa-modelo "Prado"** (fase de teste visual, estilo Bloons TD 6) e tudo o que foi ajustado nele em campeões, heróis, inimigos, interface e efeitos (seções 3 e 3.10–3.12). O que conflitar com isso nas versões anteriores está substituído.

> Este documento define *o que* o jogo é e *como ele deve parecer*. Equações de dano, curvas de ouro e balanceamento numérico pertencem ao **GDD Técnico**. Os valores numéricos das habilidades aqui descritos são **alvos de design provisórios**, a validar em playtest.

---

## Sumário

0. Registro de Mudanças (v1.0 → v2.0)
1. Visão Geral e Pilar Narrativo
2. Estrutura da Campanha (500 Fases)
3. Diretriz Visual 2.5D — Padrão do Mapa-Modelo "Prado" (estilo BTD6)
4. As 5 Eras Históricas e Seus Biomas
5. Catálogo dos 22 Heróis Históricos
6. Bestiário e Chefes das 50 Fases de Chefe
7. Especificação de Assets — Hunyuan3D / TripoSR / Blender / Unity
8. Notas de Sensibilidade Cultural e Legal
9. Pontos em Aberto para Decisão

---

## 0. Registro de Mudanças (v1.0 → v2.0)

| Área | v1.0 | v2.0 |
| :--- | :--- | :--- |
| Formato de arte | 2D isométrico, *character sheets* de 5 poses | **2.5D**: modelos 3D low-poly *Stylized Chibi* com *cel-shading* em URP, câmera isométrica fixa |
| Pipeline | Prompts de IA de imagem (Midjourney) | Prompts de **modelo 3D (.glb) via Hunyuan3D / TripoSR**, limpeza e rig no **Blender** |
| Numeração de capítulos | Inconsistente (ex.: "Capítulo 11 – Fase 110" na Idade Média, "Capítulo 21 – Fase 210" na Contemporânea) | Corrigida: 5 Eras × 10 Capítulos × 10 Fases = **500 fases / 50 chefes** |
| Liberação de heróis | Implícita | Tabela fixa: **22 resgates distribuídos entre os 50 chefes** (seção 2.3) |
| Chefes | 5 exemplos, alguns *exigindo* um herói específico | 50 chefes com **contra-indicação suave** (um herói recomendado, nunca obrigatório) |
| Precisão histórica | "Rei Xerxes I" contra Alexandre | **Dario III** (Gaugamela, 331 a.C.). Xerxes morreu 150 anos antes de Alexandre |
| Precisão científica | "Singularidade Gravitacional (3ª Lei de Newton)" | Renomeada para **Lei da Gravitação Universal** (a 3ª Lei trata de ação e reação) |
| Revisão de texto | "Estandarde", "Bastioso", "Martinho Luthero" | **Estandarte**, **Bastião**, **Martinho Lutero** |
| Era 5 | "Idade Contemporânea" | **"Contemporânea & Futuro"**: do industrial ao bunker sci-fi |
| Câmera | Isométrica diagonal a 45° | **Frontal elevada** (X 42°, Y 0°), com céu visível, conforme a arte conceitual da Fase 10 |
| Chefe da Fase 10 | Mamute-Lanudo Alfa | **Mamute-Lanudo Alfa**, que tomou a caverna do Krog (decisão de 26/11; a versão com o Krog Corrompido como chefe foi descartada) |

**v2.0 → v2.1 (26–27/11): o mapa-modelo "Prado" vira o padrão do jogo**

| Área | v2.0 | v2.1 (vigente) |
| :--- | :--- | :--- |
| Referência visual | Arte conceitual da Fase 10 (câmera 42°, céu, mapa numa faixa central) | **Mapa-modelo "Prado"**, inspirado no enquadramento do Bloons TD 6 (`Referencia_BTD6.md`) |
| Câmera | Frontal elevada a 42°, céu visível | **55°**, sem céu; o mapa ocupa a tela toda menos a loja (14% à direita) |
| Enquadramento | Área jogável nos 75% inferiores | O quadro **encosta no caminho**: só 4,5 m além do eixo (meia pista + uma unidade) |
| Posicionamento | 8–14 pedestais fixos | **Livre**: qualquer chão fora do caminho, sem plataformas |
| Bases | Portal/covil no topo e Núcleo na borda | **Sem bases em nenhuma fase**: inimigos entram por uma borda da tela e saem pela outra |
| Caminho | Terra escura 2,0/2,5 m com runas | **Lajotas de pedra** de 3,5 m com contorno escuro grosso e sombra; runas discretas |
| Chão | Neve lisa | **Pintado no bioma da Era** (§4, mantido): variação de tom, decalques e moldura de props. O prado verde do mapa-modelo foi só o teste de acabamento |
| Escala das unidades | Herói ≈ 2% da largura do mapa | Campeões ≈ 6% (escala visual 3,6×); inimigos 2,5× |
| HUD | Painéis escuros, 5 velocidades, menu sobre o pedestal | **Estilo BTD6**: vidas e ouro por cima do mapa, loja de madeira com fotos e nome, botão verde ▶/▶▶, raio da habilidade, melhorias à esquerda |
| Vida das unidades | Estáticas | **Animação procedural**: inimigos saltitam; campeões surgem com quique, respiram, giram para o alvo e dão tranco ao atacar |
| Projéteis | Bolinhas | **O que cada um arremessa** (lança, pedra, orbe, lança em chamas), saindo da mão, com recarga visível |
| Guardião | Ataque corpo a corpo | **Quartel**: 3 soldados que seguram, apanham e revidam |
| Habilidade do herói | Sem visual | Mira com a área exata, lançamento da mão, chão em fogo e inimigos queimando |

---

## 1. Visão Geral e Pilar Narrativo

### 1.1 Premissa
Uma força chamada **A Entropia** (nome provisório) devora o tempo e tenta apagar as conquistas da humanidade. Em cada Era, ela corrompe as ameaças reais daquele período (megafauna, impérios, pestes, máquinas de guerra) e as lança contra o **Núcleo Cronológico**, o coração do tempo que o jogador protege (ele não aparece no mapa: quem atravessa a tela até a borda de saída o atinge e custa vidas).

A cada 10 fases surge um **Chefe Histórico**. Ao derrotá-lo, o jogador às vezes **resgata uma figura histórica** que estava aprisionada pela Entropia. Ela entra para o acervo permanente de heróis.

### 1.2 Pilares de Experiência
1. **Legibilidade antes de tudo.** Em uma tela de 6", o jogador precisa reconhecer em menos de 0,5 s o que é inimigo, torre, herói, caminho e perigo.
2. **A história como fantasia de poder.** Cada herói transforma uma conquista real em mecânica: a lei de Hamurabi vira reflexão de dano, a gravitação de Newton vira um buraco negro.
3. **Progressão visível do tempo.** O jogador *vê* a humanidade evoluir: da pedra lascada ao bronze, ao aço, ao latão e ao neon.
4. **Respeito às figuras.** Os heróis são heroicos e estilizados, nunca ridicularizados (seção 8).

### 1.3 Tom
Aventura épica e luminosa com humor leve, no espírito de uma HQ histórica para todas as idades. Violência estilizada: inimigos se desfazem em **fragmentos de areia temporal**, sem sangue nem gore. A classificação-alvo é Livre/PEGI 7.

---

## 2. Estrutura da Campanha (500 Fases)

### 2.1 Hierarquia
- **Era** = 100 fases = 10 capítulos
- **Capítulo** = 10 fases (fases 1–9 comuns + fase 10 de chefe)
- **Total** = 5 Eras · 50 capítulos · 500 fases · 50 chefes

| Era | Fases | Capítulos | Heróis resgatáveis |
| :--- | :--- | :--- | :---: |
| 1. Pré-História | 1–100 | 1–10 | 2 |
| 2. Antiguidade | 101–200 | 11–20 | 5 |
| 3. Idade Média | 201–300 | 21–30 | 4 |
| 4. Idade Moderna | 301–400 | 31–40 | 5 |
| 5. Contemporânea & Futuro | 401–500 | 41–50 | 6 |

### 2.2 Ritmo dentro do capítulo
| Fases | Função |
| :--- | :--- |
| 1–3 | Apresentam um tipo de inimigo novo do capítulo |
| 4–6 | Combinam o novo inimigo com os anteriores |
| 7–8 | Pico de dificuldade e variação de layout |
| 9 | Fase de "respiro": recompensa, mapa aberto |
| 10 | **Chefe**: layout no Padrão Matriz (seção 3) |

### 2.3 Tabela de Resgates
O herói resgatado é sempre o que melhor responde ao chefe **seguinte** ou ao tema do capítulo, nunca um pré-requisito para vencer o próprio chefe.

| Fase | Chefe | Herói resgatado |
| :---: | :--- | :--- |
| 10 | Mamute-Lanudo Alfa (Covil do Krog) | **Krog, o Primordial** |
| 60 | Nuvem de Gafanhotos da Seca | **Gara, a Guardiã do Neolítico** |
| 110 | Lamassu Corrompido | **Hammur-Abi, o Juiz Supremo** |
| 130 | Hidra de Lerna | **Trino Socrático** |
| 150 | Dario III & Elefantes de Guerra | **Alexandros, o Conquistador Solar** |
| 170 | Hordas Gaulesas | **Imperator Julius** |
| 190 | Legião das Sombras do Vale | **Yeshua, o Redentor Radiante** |
| 220 | Saqueadores das Dunas | **Al-Mu'allim, o Unificador** |
| 240 | Muralha de Escudos Saxã | **Karolus, o Paladino Imperial** |
| 260 | Fortaleza Jin & Bestas de Cerco | **Temüjin, o Flagelo das Estepes** |
| 280 | Arqueiros de Longo Alcance | **Jehanne, a Donzela de Fogo** |
| 310 | Os Censores Encapuzados | **Gutenberg, o Mestre dos Tipos** |
| 330 | Serpente da Tempestade Atlântica | **Almirante Colombo, o Desbravador** |
| 350 | Máquinas de Cerco Corrompidas | **Leonardo, o Polímata Arcano** |
| 370 | Golem Dogmático | **Lutero, o Martelo da Reforma** |
| 390 | Autômato Astronômico | **Sir Newton, o Lorde Gravitacional** |
| 410 | Gigante do Inverno Russo | **Napoléon, o Grande Artilheiro** |
| 430 | Golem Fabril | **Marx, o Arquiteto do Proletariado** |
| 450 | Elemental Atômico | **Prof. Einstein, o Mestre Quântico** |
| 460 | Exércitos da Discórdia | **Bapu, o Bastião da Paz** |
| 480 | Máquina Cifradora Colossal | **Turing, o Decodificador** |
| 500 | A Entropia, Devoradora do Tempo | **Commander Neil, o Pioneiro Lunar** |

As 28 fases de chefe sem resgate concedem **torres novas, fragmentos de evolução de heróis ou cosméticos de era**. A lista completa está na seção 6.2.

> **Fases 1–9 sem herói.** Como Krog só é resgatado na Fase 10, as fases 1–9 funcionam como tutorial apenas com torres. Ver Ponto em Aberto nº 1.

---

## 3. Diretriz Visual 2.5D — Padrão do Mapa-Modelo "Prado" (estilo BTD6)

O **mapa-modelo "Prado"** (menu **Tools ▸ Chrono ▸ Fase de teste visual (estilo BTD6)**, layout `LAY_TEST_Prado`) é a referência visual e de leitura de **todos os mapas a partir de 27/11**. Ele segue os princípios de enquadramento, escala e interface do Bloons TD 6 (`Referencia_BTD6.md`), com a arte e a história do Chrono Defense. Toda fase das 500 precisa poder ser colocada ao lado dele sem destoar.

**O que o mapa-modelo estabelece:**
- **O mapa é a tela:** câmera inclinada a 55°, sem céu nem horizonte; o quadro encosta no caminho e deixa só o espaço de uma unidade ao lado dele.
- **Caminho de lajotas** claras com contorno escuro grosso, serpenteante, **sem cruzamentos**, entrando por uma borda da tela e saindo por outra. **Não há bases** (portal, covil ou Núcleo) em nenhuma fase.
- **Chão pintado e cheio de vida, no bioma da Era** (seção 4, que continua valendo), com props altos só emoldurando as bordas. O verde do "Prado" foi o teste de acabamento; a Era 1 segue a Tundra.
- **Posicionamento livre** de campeões em qualquer chão fora do caminho.
- **Unidades grandes e vivas**, legíveis sem zoom, com animação procedural.
- **HUD que não cobre o jogo** (seção 3.9).

**Biomas e temas por Era (seção 4) continuam valendo** — paletas, sub-biomas, props e iluminação. A arte conceitual da Fase 10 (`fase_10_krog_map_concept.png`) continua valendo para **história, paleta da Tundra e props** (esqueletos de mamute, fogueiras, blocos de gelo), mas **não** para câmera, enquadramento, pedestais, bases ou HUD.

### 3.1 Estilo: AAA Hand-Painted / Cel-Shaded
| Elemento | Regra |
| :--- | :--- |
| **Textura base (albedo)** | Pintada à mão, com volume, oclusão e gradientes suaves *pintados na textura*. Sem fotografia nem ruído fotográfico. Pinceladas visíveis apenas em superfícies grandes (chão, rochas, paredes). |
| **Sombreamento dinâmico** | *Toon ramp* de **2 faixas** (luz/sombra) com transição suave de 0,05–0,1. A sombra recebe a cor ambiente da era, nunca preto puro. |
| **Contorno (outline)** | *Inverted hull* em personagens, torres e chefes. Espessura de 0,015 un (heróis/chefes), 0,01 un (creeps) e 0,02 un (torres). Cor = albedo escurecido em 70%, sem preto chapado. O cenário também tem contorno, mas **pintado na textura** (linhas de tinta nas bordas das rochas, gelo e pedestais), não por shader. Assim as unidades, com outline mais grosso e dinâmico, continuam se destacando. |
| **Rim light** | Ativo apenas em unidades: 15% de intensidade na cor de destaque da era. Ajuda a destacar silhuetas sobre o chão. |
| **Proporção** | *Stylized Chibi* (seção 5.0). Mãos, armas e acessórios-assinatura 20–40% maiores que o real. |
| **Saturação** | O cenário é 15–20% **menos saturado** que as unidades. Inimigos usam a paleta "corrompida" (seção 3.7). |

### 3.2 Câmera
- **Projeção:** perspectiva, FOV **26°**, rotação fixa **(X 55°, Y 0°, Z 0°)**. A 55° a câmera vê a frente das unidades (rosto, arma) e ainda lê o mapa inteiro. Sem céu, sem *backdrop*.
- **Enquadramento:** o `CameraBounds` do layout cabe exatamente na tela **menos a coluna da loja** (14% da largura, à direita). Vidas, ouro e onda ficam **por cima** do mapa, sem faixas reservadas.
- **Zoom:** opcional (pinça/roda) de 0,5× a 1,15×; com o mapa inteiro visível, o *pan* fica travado. O jogo precisa ser jogável **sem zoom**.

### 3.3 Iluminação URP

**Princípio:** o cenário tem a luz **pintada na textura**; personagens, torres e inimigos recebem luz **dinâmica** pelo shader toon. Assim o custo fica baixo e a leitura, consistente.

| Parâmetro | Valor padrão |
| :--- | :--- |
| **Luz principal** | 1 Directional (Realtime). Rotação **(50°, 135°, 0°)**, com a câmera a Y 0°: na tela, a luz vem do canto superior esquerdo (10:30) e a sombra cai para o inferior direito. |
| **Renderer** | URP Forward, *Main Light* com sombras; *Additional Lights* por objeto: 2 (High), 1 (Medium), 0 (Low) |
| **Sombras de unidades** | **Sombra de contato** (decal elíptico, 60% de opacidade) em todas as unidades e em todos os tiers. Sombra realtime de 1 cascata apenas no tier High, com distância de 30 un. |
| **Cenário** | Shader *Simple Lit / Toon* sem sombra realtime recebida; AO leve assado em lightmap (resolução 8–12 texels/un) |
| **Ambiente** | *Gradient* (céu / horizonte / chão) definido por era (seção 4) |
| **Neblina** | Linear, cor da era, de 40 a 90 un: só dá profundidade às bordas, nunca cobre a área jogável |
| **Pós-processo** | *Tonemapping* Neutral; *Bloom* só no High (limiar 1,0, intensidade 0,6); *Color Adjustments* por era; *Vignette* 0,2 |
| **Glow no Low/Medium** | Sem bloom: o brilho vem de **sprites aditivos "fake glow"** sobre runas, portais e projéteis |

**Tiers de qualidade:** Low (aparelhos com 3 GB de RAM), Medium (4–6 GB) e High (8 GB ou mais). A leitura de jogo precisa ser **idêntica nos três**; muda apenas o polimento.

### 3.4 Posicionamento Livre (substitui os pedestais)
Desde 26/11 **não há pedestais**. Campeões (torres) e heróis são colocados em **qualquer chão** que cumpra:

| Regra | Valor |
| :--- | :--- |
| Base da unidade | círculo de Ø 2,6 m (raio 1,3 m) |
| Distância ao caminho | o círculo inteiro fora da pista (centro a ≥ meia largura + 1,3 m do eixo) |
| Distância entre unidades | bases sem sobreposição (centros a ≥ 2,6 m) |
| Área | dentro do quadro do mapa (`CameraBounds`), com a base inteira |

**Fluxo de construção:** toca o cartão na loja → aparece a **prévia do alcance** no chão (azul = pode, vermelho = não pode) seguindo o mouse; no celular, o 1º toque mostra a prévia e o 2º toque no mesmo lugar confirma. A unidade surge com um quique.

**Indicadores no chão** (só quando úteis, como no BTD6 — nada fica permanentemente sob a unidade além da sombra):

| Estado | Visual |
| :--- | :--- |
| Selecionada | Anel branco girando + círculo de alcance dourado |
| Melhoria disponível | Anel dourado `#FFC21A` pulsando fraco |
| Congelada pelo chefe (Pisão Glacial) | Anel cinza + aviso laranja pulsando antes |
| Ocupada | Só a sombra de contato |

### 3.5 Caminho (lajotas de pedra)
- **Largura:** **3,5 m** em todos os mapas novos (≈ 5% da largura do quadro, como no BTD6).
- **Visual:** lajotas claras em duas fileiras, cada uma com contorno escuro, sobre rejunte médio; faixa de **contorno escuro grosso** (0,25 m de cada lado) e **sombra suave** no chão (0,7 m). Cada Era troca o material: pedra (1) · arenito (2) · paralelepípedo (3) · tijolo (4) · asfalto/metal (5).
- **Contraste:** o caminho sempre difere do chão em ≥ 25% de luminância.
- **Runas do caminho:** discretas sobre as lajotas — apagadas entre ondas, 35% durante a onda, vermelhas `#FF3B30` pulsando na onda de chefe. A direção do fluxo vem do próprio traçado (entra numa borda, sai em outra).
- **Traçado:** serpenteante, **sem cruzamentos** (o *flow field* faria os inimigos cortarem caminho), com ≥ 6,5 m entre eixos de trechos vizinhos (3 m de chão entre as bordas, espaço para uma unidade). Começa e termina **fora da tela** (≥ 8 m além da borda).

### 3.6 Entrada e Saída dos Inimigos (sem bases)
- **Em todas as fases**, sem bases: os inimigos (inclusive os chefes) surgem de **fora da tela**, entrando por uma borda, e saem pela borda oposta. Quem sai custa vidas. Não há portal, covil nem Núcleo como objeto no mapa.
- O **Núcleo Cronológico** continua na história: aparece nas telas de evento, no HUD (vidas) e nas cinemáticas, não no mapa.
- **Chefes:** a entrada é anunciada pela barra do chefe, pelo aviso no centro da tela e pelas runas vermelhas no caminho.
- **Fase 10 (Krog):** o **Mamute-Lanudo Alfa** entra pela borda como os demais. A caverna de gelo do Krog pode aparecer como **prop de cenário** na moldura (fora do caminho, não é ponto de saída). Ao derrotá-lo, ele se desfaz em areia temporal e o Krog é libertado (cinemática de 4 s). Modelo: `BOS_010_MammothAlpha` (Tripo, `Tools/tripo/modelos_era1.json`).

### 3.7 Linguagem Visual da Entropia (inimigos)
Para que o inimigo nunca se confunda com um aliado:
- **Cor corrompida universal:** violeta-escuro `#3B1E54` com veios magenta `#E0369B`, aplicados como detalhes (olhos, rachaduras, armas) sobre a paleta histórica do inimigo.
- **Aliados nunca usam magenta.**
- Ao morrer, o inimigo se desfaz em **areia temporal** (partículas em ampulheta).

### 3.8 Composição de Cena
1. **Quadro justo:** o quadro do mapa vai só 4,5 m além do eixo do caminho (meia pista + espaço de uma unidade). Não sobra terreno vazio.
2. **Chão pintado no bioma da Era/sub-bioma (seção 4):** textura com variação de tom e pinceladas, mais decalques espalhados fora do caminho (na Tundra: neve, gelo, musgo, pedras, ossos). Nada é cor chapada.
3. **Moldura:** árvores e props altos (> 1,5 m) só **na borda e fora do quadro**, nunca no chão de construção; ficam parcialmente cortados pela tela, como no BTD6.
4. **Hierarquia de contraste:** unidades > caminho > chão > moldura. Saturação alta, contorno escuro em tudo.
5. **Pontos de interesse:** 2–3 props grandes por mapa (ex.: esqueleto de mamute) fora do caminho; não bloqueiam construção (são decalques ou ficam na moldura).

### 3.9 HUD (estilo BTD6)
| Elemento | Posição | Visual |
| :--- | :--- | :--- |
| Vidas e ouro | Superior esquerdo, **por cima do mapa** | Coração e moeda grandes + números brancos grossos com contorno escuro, sem painel |
| Onda e próxima onda | Superior direito do mapa | "Onda X/N" + "Próxima onda em Xs (adiantar: +N ouro)" |
| Pausa | Ao lado da onda | Botão redondo azul |
| Barra do chefe | Topo central | Retrato + nome + barra vermelha (só com chefe em campo) |
| **Loja** | Coluna direita (14% da largura) | Moldura de **madeira**; **cabeçalho com o nome e a função** do campeão tocado (como o "QUINCY" do BTD6); cartão do herói na linha inteira; cartões das torres em 2 colunas com **foto do modelo 3D** e preço (vermelho se não dá; "Trava" se bloqueado) |
| Iniciar/acelerar | Canto inferior direito | **Botão verde redondo**: ▶ chama a próxima onda (com bônus); durante a onda, ▶▶ alterna 1× ↔ 3× (amarelo quando rápido) |
| Habilidade do herói | Ao lado do botão verde | **Raio amarelo** redondo com recarga circular; só aparece com o herói em campo |
| Melhorias | Lado esquerdo, ao tocar numa unidade | Painel azul: **foto + nome**, alvo ("Alvo: Primeiro"), 3 caminhos com botão verde "+", vender (vermelho) |
| Dicas | Faixa escura embaixo do mapa | Só durante posicionamento ou mira |
| Fotos | — | Retratos renderizados dos próprios modelos 3D ao abrir a fase (herói: rosto e ombros; torres: corpo inteiro), fundo azul |
| Área segura | — | Respeita *safe area*; nunca cobre o caminho de forma permanente |

### 3.10 Unidades em Campo (campeões, heróis, inimigos)
**Escala visual** (a simulação continua em metros reais; só o desenho cresce):

| Unidade | Escala | Leitura |
| :--- | :-: | :--- |
| Campeões (torres) e heróis | **3,6×** o modelo | ≈ 6% da largura do mapa; detalhes visíveis sem zoom |
| Inimigos | **2,5×** | Lobo ≈ largura do caminho (como o balão do BTD6) |
| Chefe (Mamute) | 2,5× de 2,4 m | ≈ 6 m: domina a tela |
| Soldados do Guardião | 2,6 m de altura | Menores que um campeão |

**Sombra de contato** elíptica sob cada campeão e herói.

**Animação procedural** (os modelos não têm esqueleto; tudo por código, zero-GC):
- **Inimigos:** passada saltitante, balanço lateral e "amasse" a cada pisada; o chefe anda pesado e lento; atordoado ou **segurado por soldado** para de andar. O relógio é o da simulação (congela na pausa, acelera junto).
- **Campeões e heróis:** surgem (ou sobem de nível) com **quique**, **respiram** parados, **giram para o alvo** (como os macacos do BTD6) e dão um **tranco para trás** a cada ataque. O herói também troca de **pose** (Idle, Ataque, Habilidade, Movimento, Vitória).

**Nomes dos campeões** (arquétipo → nome por Era; Era 1 definida):

| Arquétipo | Era 1 | Função (subtítulo da loja) |
| :--- | :--- | :--- |
| Atirador | **Caçador** | longo alcance |
| Lançador | **Catapulta de Pedra** | dano em área |
| Alquimista | **Xamã** | magia |
| Guarda | **Guardião** | bloqueia o caminho (soldados) |
| Herói | **Krog, o Primordial** | herói |

### 3.11 Projéteis, Item na Mão e Recarga
Cada campeão arremessa **o que ele é**, nunca uma bolinha genérica:

| Campeão | Item | Voo |
| :--- | :--- | :--- |
| Caçador | Lança de madeira com ponta de sílex e penas | Arco baixo; a ponta segue a direção do voo |
| Catapulta | Pedra grande rachada | Arco alto, girando |
| Xamã | Orbe azul com brilho | Quase reto, flutuando, com rastro |
| Guardião | — (luta com soldados) | — |
| Krog | Lança em chamas | Arco baixo, como a do Caçador |

**Ciclo do arremesso:** (1) o item fica **na mão** enquanto a unidade espera, acompanhando o giro e a respiração; (2) no disparo ele **some da mão e sai exatamente dali**, alinhando-se ao alvo durante o voo; (3) um item novo **reaparece na mão** com um quique após a recarga (metade do intervalo de ataque, máx. 0,9 s), sempre antes do próximo disparo. A posição da mão de cada modelo fica em `ArtLibrary.HandLocal` (ajustar quando chegarem os modelos finais).

### 3.12 Soldados do Guardião e Efeitos de Habilidade
**Soldados:** guerreiros da Era (Era 1: homem das cavernas com escudo de couro e porrete), que saem do quartel em fila, viram para o inimigo, dão passinhos ao andar e inclinam o corpo a cada golpe. **Barra de vida** verde sobre a cabeça quando feridos. Somem ao morrer e voltam do quartel depois (regras no GDD §7.7).

**Toda habilidade de herói mostra 3 momentos:**
1. **Mira:** a área exata onde vai cair (círculo laranja do tamanho real), seguindo o mouse; no celular, 1º toque mostra e 2º confirma.
2. **Lançamento:** sai **do herói** (ele vira para o ponto, faz a pose de habilidade e arremessa).
3. **Impacto e duração:** a área fica visível **durante toda a duração**, e os inimigos afetados mostram o efeito no corpo.

**Matriz — Caça Coordenada de Fogo (Krog):** lança em chamas voa em arco da mão até o ponto em 0,35 s → o chão vira **terra queimada com rachaduras em brasa** (estouro ao cair) → ~16 **chamas** tremulam espalhadas pela área por 6 s e apagam no último 0,6 s → inimigos no fogo ganham **chama no corpo** e continuam queimando 1,5 s depois de sair. A mesma chama aparece em qualquer inimigo queimando (ex.: Catapulta Incendiária).

---

## 4. As 5 Eras Históricas e Seus Biomas

Cada Era tem **um bioma-âncora**, que é o do covil final, e **sub-biomas** que variam de capítulo em capítulo para evitar repetição em 100 fases.

### 4.1 Era 1 — Pré-História (Fases 1–100)
**Bioma-âncora:** Tundra, Neve e o **Covil do Krog** (arte de referência: `fase_10_krog_map_concept.png`)

| Item | Especificação |
| :--- | :--- |
| **Paleta** | Neve `#E8F1F5` · Gelo `#9EC9DB` · Terracota `#B5562F` · Musgo `#5E7A3A` · Fogo `#F26B1D` · Pedra `#6B625A` |
| **Destaque da era (runas)** | Azul-gelo `#6FD3FF` (o laranja-fogo `#F26B1D` fica reservado às fogueiras e ao VFX do Krog) |
| **Luz principal** | `#FFF1DC`, intensidade 1,1 |
| **Ambiente (céu/horiz./chão)** | `#BFD6E6` / `#E6D8C3` / `#5A4A3E` |
| **Neblina** | `#D5E3EC` |
| **Céu de fundo** | Noite polar azul-marinho `#1C2A4A` com **aurora boreal** verde-ciano `#5FE3C0`, animada por *UV scroll* lento no *backdrop* |
| **Terreno** | Neve fofa clara, gelo azul translúcido (paredões e blocos), rochas cinza-azuladas com neve no topo; caminho de **terra escura e lamacenta** `#6B6259` com pedrinhas |
| **Props** | **Esqueletos de mamute** (ponto de interesse principal), ossos soltos, **fogueiras em anel de pedras**, blocos de gelo, montanhas nevadas ao fundo; variações: tendas de pele, menires, pinturas rupestres, lanças cravadas |
| **Iluminação especial** | Pontos quentes de fogueira (luz adicional laranja no tier High; *fake glow* nos demais) contra o frio azulado da tundra e das runas |
| **Partículas de ambiente** | Neve leve caindo, brasas subindo das fogueiras |
| **Sub-biomas** | Cap. 1–3 Tundra Gelada · Cap. 4–6 Estepe Glacial · Cap. 7–8 Crescente Fértil (primeiros campos de trigo; transição para o Neolítico) · Cap. 9–10 Cavernas Profundas |

#### 4.1.1 Capítulo 1: a estrada do mapa da saga (27/11)

Dentro de um capítulo, o chão de cada fase **segue o bioma que a estrada atravessa no mapa da saga** (`Resources/Sprites/UI/Saga/SAGA_E1_Map.png`). A neve fica reservada à chegada no covil; o jogador "sobe" da mata até a montanha.

| Fases | Bioma (`GroundStyle`) | No mapa da saga | Chão | Moldura (bordas) |
| :-: | :--- | :--- | :--- | :--- |
| 1–4 | **Mata** (`Forest`) | Rio com ponte, aldeia de palha, paliçada | Verde saturado `#5DA83A`–`#7BC247`, tapetes de musgo, flores | Árvores copadas e alguns pinheiros |
| 5–7 | **Savana** (`Savanna`) | Pedras erguidas → areia com palmeiras | Capim amarelado `#A8B655` com manchas de areia `#DDC387` | Palmeiras e copadas |
| 8 | **Deserto** (`Desert`) | Portão azul (Ishtar) e casas de adobe | Areia `#E2C88C` com dunas, pedras de arenito | Palmeiras ralas |
| 9 | **Planalto** (`Highland`) | Penhasco verde com cascalho | Verde escuro `#5E9646`, cascalho, muitas pedras | Pinheiros sem neve |
| 10 | **Tundra** (`Tundra`) | Covil-caveira entre montanhas nevadas | Neve (tabela acima) | Pinheiros nevados |

**Regras:** a estrada é sempre de **lajotas bege** `#DCCBA4` como no mapa da saga (na Tundra, lajotas escuras por contraste). Mata, savana, deserto e planalto usam luz e neblina próprias (quentes no deserto); a Tundra mantém a luz polar acima. Fases que dividem o mesmo traçado têm decoração sorteada diferente. Quando houver chão pintado à mão por fase, ele substitui o procedural, mas mantém o bioma desta tabela.

### 4.2 Era 2 — Antiguidade (Fases 101–200)
**Bioma-âncora:** Deserto, Arenito e **Templo Egípcio**

| Item | Especificação |
| :--- | :--- |
| **Paleta** | Arenito `#E3B878` · Ouro `#F2C14E` · Lápis-lazúli `#2E4A9E` · Púrpura imperial `#6B2D6B` · Bronze `#A8692E` · Mármore `#F3EFE6` |
| **Destaque da era** | Lápis-lazúli `#2E4A9E` |
| **Luz principal** | `#FFE8B8`, intensidade 1,3 (sol forte) |
| **Ambiente** | `#9FC4E8` / `#F0D9A8` / `#8A6A40` |
| **Neblina** | `#F2DDB0` (poeira dourada) |
| **Terreno** | Dunas, arenito esculpido, lajotas de templo, margens férteis do Nilo |
| **Props** | Obeliscos, esfinges, colunas papiriformes, zigurates em miniatura, ânforas, palmeiras, estátuas gregas e romanas, mosaicos |
| **Iluminação especial** | Sombras curtas de meio-dia; reflexo dourado no ouro dos templos; tochas em interiores |
| **Partículas de ambiente** | Poeira dourada em suspensão, ondas de calor (*heat haze* apenas no High) |
| **Sub-biomas** | Cap. 11–12 Mesopotâmia (tijolo de barro, zigurates) · Cap. 13–14 Templos Egípcios · Cap. 15–16 Grécia (mármore e oliveiras) · Cap. 17–19 Roma (estradas pavimentadas, aquedutos) · Cap. 20 Colosso de Rodes |

### 4.3 Era 3 — Idade Média (Fases 201–300)
**Bioma-âncora:** Florestas, Pedra Gótica e **Castelo**

| Item | Especificação |
| :--- | :--- |
| **Paleta** | Floresta `#2F5A34` · Esmeralda `#1F8A5B` · Carmesim `#A3202F` · Prata `#B8C0C8` · Pedra gótica `#5B5F66` · Carvalho `#4A3222` |
| **Destaque da era** | Esmeralda `#1F8A5B` |
| **Luz principal** | `#E6ECFF`, intensidade 1,0 (dia nublado) |
| **Ambiente** | `#7F8FA6` / `#A7B3A0` / `#3A3A2E` |
| **Neblina** | `#8E9AA8` (névoa de floresta) |
| **Terreno** | Grama escura, raízes, estradas de pedra irregular, pontes levadiças, pátios de castelo |
| **Props** | Estandartes de seda, muralhas com ameias, vitrais, carroças, trabucos, tendas de torneio, cruzes celtas, mercados de feira |
| **Iluminação especial** | Feixes coloridos de vitral (cookies de luz projetados como decal); céu de tempestade em fases de chefe |
| **Partículas de ambiente** | Folhas caindo, névoa baixa, chuva nas fases de chefe |
| **Sub-biomas** | Cap. 21–22 Costas Vikings · Cap. 23 Rotas do Deserto · Cap. 24–25 Florestas Francas · Cap. 26–27 Estepes Mongóis · Cap. 28–29 Cerco ao Castelo · Cap. 30 Muralhas de Constantinopla |

### 4.4 Era 4 — Idade Moderna (Fases 301–400)
**Bioma-âncora:** Tijolos, Vapor Industrial e **Oficina**

> **Nota de direção:** o vapor industrial é um **anacronismo intencional e estilizado** (estética "Renascimento mecânico" / proto-steampunk), coerente com as invenções de Leonardo e com a transição para a Era 5.

| Item | Especificação |
| :--- | :--- |
| **Paleta** | Tijolo `#A4472F` · Cobre `#C26A3A` · Latão `#C9A646` · Pergaminho `#E6D3A3` · Azul-marinho `#1F2F55` · Vapor `#D9DDE0` · Violeta-prisma `#7A4FD0` |
| **Destaque da era** | Latão `#C9A646` |
| **Luz principal** | `#FFE2C2`, intensidade 1,05 (luz morna de oficina) |
| **Ambiente** | `#8DA2B8` / `#D8BFA0` / `#4A3528` |
| **Neblina** | `#C7B9A6` (vapor e fuligem) |
| **Terreno** | Calçamento de tijolo, docas de madeira, pisos de oficina, cais portuários |
| **Props** | Engrenagens gigantes, prensas, mesas com projetos, globos terrestres, astrolábios, lentes e prismas, caravelas atracadas, canos de vapor, guindastes de madeira |
| **Iluminação especial** | Janelas emissivas quentes; feixes de arco-íris de prismas (efeito de assinatura de Newton) |
| **Partículas de ambiente** | Vapor saindo de válvulas, poeira de papel, faíscas de forja |
| **Sub-biomas** | Cap. 31–32 Oficinas de Mainz · Cap. 33–34 Portos e Oceano Atlântico · Cap. 35–36 Ateliês de Florença · Cap. 37–38 Cidades Reformadas (Wittenberg) · Cap. 39–40 Observatórios e Fábricas a Vapor |

### 4.5 Era 5 — Contemporânea & Futuro (Fases 401–500)
**Bioma-âncora:** Aço Escovado, Neon e **Bunker Sci-Fi**

A Era 5 é uma **rampa tecnológica**: começa industrial e termina em sci-fi pleno na Lua.

| Item | Especificação |
| :--- | :--- |
| **Paleta** | Aço `#8E979F` · Grafite `#2A2E33` · Neon ciano `#22E3F2` · Verde-matriz `#3BFF7A` · Branco lunar `#F2F4F7` · Ouro atômico `#FFB627` |
| **Destaque da era** | Neon ciano `#22E3F2` |
| **Luz principal** | `#E8F4FF`, intensidade 1,0 (luz fria) |
| **Ambiente** | `#4A5B70` / `#7C8A99` / `#1E2228` |
| **Neblina** | `#33404F` |
| **Terreno** | Chapas de aço com rebites, concreto, grades de metal, piso de bunker com faixas de sinalização, regolito lunar |
| **Props** | Trincheiras e sacos de areia (início), torres de rádio, reatores, terminais com monitores CRT, servidores, antenas parabólicas, módulos lunares |
| **Iluminação especial** | Faixas de LED e neon emissivas; luzes de alerta giratórias em fases de chefe; brilho de reator atômico |
| **Partículas de ambiente** | Fumaça industrial (início), *glitches* digitais e dados flutuantes (fim), poeira lunar em baixa gravidade (Cap. 50) |
| **Sub-biomas** | Cap. 41–42 Campos Napoleônicos e Inverno Russo · Cap. 43–44 Cidades Fabris · Cap. 45–46 Laboratórios Atômicos · Cap. 47–48 Bunkers de Criptografia · Cap. 49 Data Center Sentinela · Cap. 50 Base Lunar |

> A paleta da Era 5 **não usa magenta**, reservado à Entropia (seção 3.7). O "Néon Violeta" da v1.0 foi movido para o violeta-prisma da Era 4.

---

## 5. Catálogo dos 22 Heróis Históricos

### 5.0 Padrão Comum a Todos os Heróis

**Proporções Stylized Chibi 2.5D**
| Medida | Valor |
| :--- | :--- |
| Altura total | **2,8 cabeças** (altura de referência em cena: 1,1 un) |
| Cabeça | 36% da altura; olhos grandes, com expressão legível do topo |
| Mãos | 1,3× o tamanho anatômico; pés 1,2× |
| Arma / acessório-assinatura | 1,3–1,5× o tamanho real, sempre visível na silhueta vista do alto |
| Silhueta | Reconhecível a 64 px de altura e em preto chapado |
| Montados | Montaria chibi com ~1,6× a altura do herói; o par inteiro respeita o orçamento de heróis |

**Animações obrigatórias (por herói)**
| # | Animação | Duração | Observação |
| :--- | :--- | :--- | :--- |
| 1 | **Pose parada (idle)** | Loop de 2–3 s | Pose estática característica + respiração |
| 2 | Ataque básico | 0,3–0,6 s | O frame de impacto marca o evento de dano |
| 3 | Habilidade ativa | 1,0–1,8 s | Antecipação clara de 0,3 s ou mais |
| 4 | Deslocamento | Loop de 0,8 s | Usado no reposicionamento entre pedestais |
| 5 | **Vitória** | 2,0–3,0 s | Tocada no fim da fase |
| 6 | Resgate (*cinemática*) | 4 s | Só na fase de chefe que o liberta |

**Template de prompt para Hunyuan3D.** Os prompts estão em inglês, que dá melhores resultados nesses modelos. São mantidos curtos e descrevem o corpo **em A-pose com as mãos vazias**; a arma é gerada separadamente como *prop*, para facilitar o rig.
```text
stylized chibi 3D game character, [DESCRIÇÃO], 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, simple clean shapes, mobile game asset, low poly
```

**Legenda das fichas:** *Papel* = função tática no TD · *CD* = recarga da habilidade (provisória).

---

### 5.1 Era 1 — Pré-História

#### 1. Krog, o Primordial (Caçadores-Coletores) — Resgate: Fase 10
* **Contexto:** a Revolução Cognitiva: linguagem para a caça em grupo e o domínio do fogo.
* **Papel:** Dano contínuo em área (queimadura).
* **Proporção e silhueta:** compacto e largo, ombros cobertos de pele, lança de osso maior que o corpo, pintura corporal tribal luminosa.
* **Paleta:** Pele `#8A5A3B` · Fogo `#F26B1D` · Osso `#EDE3CF`
* **Habilidade ativa — *Caça Coordenada de Fogo* (CD 14 s):** cria um círculo de chamas no caminho, com dano de queimadura por segundo. Aura *Cooperação Cognitiva*: +15% de DPS para cada inimigo adicional dentro do círculo (máximo de +150%).
* **Pose parada (idle):** agachado com a lança apoiada no ombro, olhando o horizonte; a tocha na outra mão solta brasas.
* **Vitória:** bate a lança no chão e solta um rugido; as brasas sobem em espiral.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, prehistoric hunter-gatherer man, thick brown fur pelts, leather wraps, bone necklace, glowing orange tribal body paint, wild hair, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `bone spear with flint tip, oversized, hand-painted, low poly` + `burning torch`

#### 2. Gara, a Guardiã do Neolítico (Primeiros Agricultores) — Resgate: Fase 60
* **Contexto:** a Revolução Agrícola no Crescente Fértil, o sedentarismo e as ferramentas de pedra polida.
* **Papel:** Controle (atordoamento) e dano perfurante.
* **Proporção e silhueta:** túnica de linho em forma de sino, cajado alto com espigas de trigo, foice de sílex na cintura.
* **Paleta:** Linho `#D9C9A3` · Musgo `#5E7A3A` · Terracota `#B5562F`
* **Habilidade ativa — *Raízes do Sedentarismo* (CD 16 s):** espinhos agrícolas brotam do caminho, atordoam os primeiros 5 inimigos por 3 s e deixam um campo de dano perfurante contínuo por 6 s.
* **Pose parada (idle):** cajado plantado no chão, a outra mão espalhando sementes que brilham levemente.
* **Vitória:** gira o cajado e uma pequena plantação de trigo floresce em volta dele.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, neolithic farmer woman, rustic woven flax tunic, flint jewelry, braided hair with wheat ornaments, leather sandals, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `wooden staff with wheat bundle top` + `flint sickle`

---

### 5.2 Era 2 — Antiguidade

#### 3. Hammur-Abi, o Juiz Supremo (Hamurabi) — Resgate: Fase 110
* **Contexto:** rei da Babilônia e autor de um dos primeiros códigos de leis escritos, baseado no princípio de reciprocidade ("olho por olho").
* **Papel:** Reflexão de dano / anti-chefe.
* **Proporção e silhueta:** túnica longa em forma de cone, barba trançada em blocos, coroa alta, estela de basalto quase da altura dele.
* **Paleta:** Ouro `#F2C14E` · Lápis `#2E4A9E` · Basalto `#2B2B30`
* **Habilidade ativa — *Código Retributivo* (CD 20 s):** marca um setor do caminho por 6 s; 200% de todo dano causado por inimigos nesse setor é devolvido como **Dano Verdadeiro** a toda a onda.
* **Pose parada (idle):** estela apoiada no chão à frente, uma mão sobre ela, olhar severo; glifos cuneiformes brilham em sequência.
* **Vitória:** ergue a estela acima da cabeça e os glifos se projetam no ar formando um selo.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, ancient Babylonian king, long gold-embroidered Mesopotamian robe, tall royal crown, braided square beard, lapis lazuli jewelry, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `large basalt stele tablet with cuneiform carvings, glowing runes`

#### 4. Trino Socrático (Sócrates, Platão e Aristóteles) — Resgate: Fase 130
* **Contexto:** os fundadores da filosofia ocidental, da lógica formal e das bases do pensamento científico.
* **Papel:** Controle de multidão (confusão).
* **Proporção e silhueta:** **três figuras em formação triangular na mesma base (contam como uma unidade)**. Sócrates é baixo e careca, com barba; Platão é alto e largo; Aristóteles é magro, com pergaminho. A diferença de silhueta entre os três é obrigatória.
* **Orçamento especial:** 2.500 tris no total (~830 por figura), um material compartilhado e uma única textura 1024².
* **Paleta:** Mármore `#F3EFE6` · Ouro `#F2C14E` · Azul-egeu `#3C7DBF`
* **Habilidade ativa — *Dilema Socrático* (CD 18 s):** uma onda de lógica deixa os inimigos atingidos **confusos** por 5 s: param a marcha e atacam uns aos outros. Chefes ficam apenas lentos (-50%).
* **Pose parada (idle):** em debate, os três gesticulando; um pergaminho geométrico flutua no centro.
* **Vitória:** os três apontam para cima ao mesmo tempo e um diagrama dourado (sólido platônico) se forma sobre eles.
* **Prompt Hunyuan3D** (gerar cada figura separadamente e montar no Blender):
```text
stylized chibi 3D game character, ancient Greek philosopher, white toga with gold trim, [bald with short beard | tall broad shoulders curly beard | slim with neat beard], sandals, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `glowing geometric scroll`, `glowing dodecahedron`

#### 5. Alexandros, o Conquistador Solar (Alexandre, o Grande) — Resgate: Fase 150
* **Contexto:** o jovem rei da Macedônia que criou um dos maiores impérios da Antiguidade e espalhou a cultura helenística.
* **Papel:** Empurrão em massa (*knockback*) e perfuração de armadura.
* **Proporção e silhueta:** **montado em Bucéfalo** (cavalo negro chibi), capa púrpura esvoaçante, sarissa longa na diagonal.
* **Orçamento especial:** 2.500 tris para cavaleiro e montaria (≈1.100 + 1.400); esqueleto *Generic* com 45 ossos.
* **Paleta:** Bronze `#A8692E` · Púrpura `#6B2D6B` · Ouro `#F2C14E`
* **Habilidade ativa — *Falange Implacável de Sarissas* (CD 22 s):** uma parede de lanças avança contra a rota, empurra a onda **8 un** para trás (chefes: 2 un) e reduz a armadura física em 50% por 6 s.
* **Pose parada (idle):** Bucéfalo empinando de leve e a sarissa apontada para a frente.
* **Vitória:** Bucéfalo empina por completo enquanto Alexandros ergue a espada contra um disco solar dourado.
* **Prompt Hunyuan3D** (cavaleiro e cavalo gerados separadamente):
```text
stylized chibi 3D game character, young Macedonian king, polished bronze cuirass, royal purple cloak, lion-crest helmet under arm, short golden hair, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
```text
stylized chibi 3D black stallion, ancient Greek bronze horse armor, short stocky legs, big head, standing neutral pose, side view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `long golden sarissa spear`, `short Greek sword`

#### 6. Imperator Julius (Júlio César) — Resgate: Fase 170
* **Contexto:** general e estadista romano; conquistou a Gália e abriu caminho para a transição da República ao Império.
* **Papel:** Bombardeio de área e redução de armadura.
* **Proporção e silhueta:** armadura segmentada larga, capa vermelha longa, coroa de louros, bastão de comando erguido.
* **Paleta:** Vermelho romano `#A3202F` · Ouro `#F2C14E` · Ferro `#7A7F85`
* **Habilidade ativa — *Veni, Vidi, Vici* (CD 20 s):** marca uma zona e, após 1 s, catapultas a bombardeiam; elimina tropas leves na hora e aplica *Armor Shred* de -40% nos chefes por 8 s.
* **Pose parada (idle):** braço estendido com o bastão apontando para o mapa, a outra mão no gládio embainhado.
* **Vitória:** ergue o gládio e pétalas de louro caem; um estandarte da águia romana surge atrás dele.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, Roman general, lorica segmentata armor, long red cape, golden laurel wreath, short combed hair, leather sandals, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `Roman gladius sword`, `commander baton with eagle tip`

#### 7. Yeshua, o Redentor Radiante (Jesus de Nazaré) — Resgate: Fase 190
* **Contexto:** figura central do cristianismo, com influência profunda na ética e na cultura do Ocidente.
* **Papel:** Suporte (cura da base) e anti-mortos-vivos.
* **Proporção e silhueta:** túnica simples de linho e manto, cabelos longos, aura dourada em halo. **Sem arma.**
* **Paleta:** Linho `#EDE6D6` · Ouro suave `#F5D77A` · Azul-manto `#5C7FA8`
* **Habilidade ativa — *Purificação Radiante* (CD 25 s):** um feixe de luz dissolve inimigos das categorias Mortos-Vivos e Sombras (chefes sofrem 30% da vida máxima) e restaura **+5 de vida** do Núcleo.
* **Pose parada (idle):** de pé, sereno, mãos abertas na altura da cintura, a aura pulsando devagar.
* **Vitória:** ergue as mãos e uma onda de luz suave cobre o mapa; flores brotam em volta dele.
* **Tom:** sereno e protetor, nunca agressivo. Sem gestos de combate corpo a corpo (seção 8).
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, first-century Judean teacher, simple woven linen robe, light blue mantle, long brown hair, short beard, gentle expression, leather sandals, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop/VFX:** aura dourada feita apenas com shader e partículas, sem malha.

---

### 5.3 Era 3 — Idade Média

#### 8. Al-Mu'allim, o Unificador (Maomé) — Resgate: Fase 220
* **Contexto:** profeta fundador do Islã; unificou as tribos da Península Arábica no século VII.
* **Papel:** Controle global (redução de velocidade e precisão).
* **⚠ Representação anicônica obrigatória.** Ver a seção 8.1 antes de produzir. O rosto **nunca** é modelado: fica coberto por um véu branco e por luz, como na tradição das miniaturas otomanas e persas. Recomenda-se consultoria cultural antes da produção.
* **Proporção e silhueta:** túnicas fluidas verdes e douradas, turbante, cajado esculpido. A silhueta é definida pelo tecido, não pelo rosto.
* **Paleta:** Esmeralda `#1F8A5B` · Ouro `#F2C14E` · Areia `#E3C38A`
* **Habilidade ativa — *Tempestade Unificadora do Deserto* (CD 30 s):** um turbilhão de areia cobre o mapa por 4 s, zera a velocidade e a precisão dos inimigos (chefes: -60%) e causa dano de erosão por segundo.
* **Pose parada (idle):** de pé, cajado apoiado, tecidos ondulando com o vento.
* **Vitória:** ergue o cajado e a areia se transforma em padrões geométricos dourados de arabesco.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, seventh-century Arabian desert leader, flowing green and gold robes, white turban, face fully covered by a plain white veil, no facial features, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `ancient carved wooden staff`

#### 9. Karolus, o Paladino Imperial (Carlos Magno) — Resgate: Fase 240
* **Contexto:** imperador dos francos, unificador da Europa Ocidental e patrono do Renascimento Carolíngio. Os "Doze Paladinos" vêm da tradição lendária (*A Canção de Rolando*).
* **Papel:** Atordoamento em área e dano físico crítico.
* **Proporção e silhueta:** armadura pesada e larga, coroa imperial sobre o elmo, barba branca, espada Joyeuse com brilho, escudo com águia.
* **Paleta:** Prata `#B8C0C8` · Ouro `#F2C14E` · Azul real `#2B4C9B`
* **Habilidade ativa — *Carga dos Doze Paladinos* (CD 20 s):** cavaleiros fantasmas atropelam a linha de frente; dano crítico e atordoamento em área por 3,5 s.
* **Pose parada (idle):** espada cravada no chão à frente e as duas mãos no punho, em vigília.
* **Vitória:** ergue a Joyeuse enquanto doze estandartes fantasmas surgem em semicírculo.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, Frankish emperor, heavy silver medieval armor with imperial eagle crest, royal blue tabard, golden crown on helmet, long white beard, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `ornate longsword Joyeuse with golden hilt`, `round shield with eagle emblem`

#### 10. Temüjin, o Flagelo das Estepes (Gengis Khan) — Resgate: Fase 260
* **Contexto:** fundador do Império Mongol, o maior império terrestre contínuo da história, conhecido pela arquearia montada.
* **Papel:** Dano em cadeia e sangramento ao longo da rota.
* **Proporção e silhueta:** **montado** em cavalo das estepes (baixo e robusto), chapéu de pele com abas, arco composto bem recurvado.
* **Orçamento especial:** 2.500 tris para cavaleiro e montaria; esqueleto *Generic* com 45 ossos.
* **Paleta:** Couro escuro `#4A3222` · Carmesim `#A3202F` · Pele/pelagem `#B08A5E`
* **Habilidade ativa — *Horda dos Arqueiros Nômades* (CD 18 s):** arqueiros fantasmas percorrem a rota disparando flechas em chamas; dano em cadeia (até 5 saltos) e sangramento por 5 s.
* **Pose parada (idle):** montado, com o arco meio tensionado e o cavalo batendo a pata.
* **Vitória:** dispara uma flecha flamejante para o céu e o cavalo empina.
* **Prompt Hunyuan3D** (cavaleiro e cavalo separados):
```text
stylized chibi 3D game character, Mongol warlord, layered leather and fur armor, fur-trimmed hat with flaps, long mustache, leather boots, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
```text
stylized chibi 3D stocky Mongolian steppe horse, short legs, thick mane, leather saddle, standing neutral pose, side view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `heavy composite recurve bow`, `quiver with arrows`

#### 11. Jehanne, a Donzela de Fogo (Joana d'Arc) — Resgate: Fase 280
* **Contexto:** heroína nacional francesa e figura militar central da Guerra dos Cem Anos.
* **Papel:** Suporte (buff de velocidade de ataque das torres) e dano sagrado.
* **Proporção e silhueta:** armadura prateada justa, cabelo curto, estandarte alto (a silhueta mais alta do elenco).
* **Paleta:** Prata `#C9D0D6` · Branco `#F7F7F2` · Amarelo sagrado `#FFD54A`
* **Habilidade ativa — *Estandarte da Fúria Divina* (CD 22 s):** finca o estandarte por 8 s; as torres no raio ganham +100% de velocidade de ataque e os inimigos próximos queimam com chamas sagradas.
* **Pose parada (idle):** estandarte erguido na vertical, tremulando, com a espada embainhada.
* **Vitória:** crava o estandarte e uma coluna de luz dourada sobe dele.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, fifteenth-century French heroine, polished fitted silver plate armor, white surcoat with gold fleur-de-lis, short dark bob haircut, determined expression, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `tall white war banner on pole`, `longsword`

---

### 5.4 Era 4 — Idade Moderna

#### 12. Gutenberg, o Mestre dos Tipos (Johannes Gutenberg) — Resgate: Fase 310
* **Contexto:** inventor da prensa de tipos móveis no Ocidente (c. 1440), que tornou o conhecimento muito mais acessível.
* **Papel:** Debuff (vulnerabilidade) e dano físico.
* **Proporção e silhueta:** avental de couro largo, barba cheia, gorro de artesão, bandeja de tipos de chumbo nos braços.
* **Paleta:** Bronze `#A8692E` · Chumbo `#6D7278` · Carvalho `#4A3222`
* **Habilidade ativa — *Prensa de Tipos de Chumbo* (CD 16 s):** dispara blocos de letras metálicas que esmagam os alvos e aplicam **Vulnerabilidade** (+40% de dano recebido de todas as fontes por 6 s).
* **Pose parada (idle):** examina um tipo de chumbo com uma lupa; uma mini-prensa ao lado solta vapor.
* **Vitória:** puxa a alavanca da prensa e uma folha impressa sai voando com a palavra "VICTORIA" em letras góticas.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, fifteenth-century German craftsman, thick leather work apron over dark robe, artisan cap, full beard, ink-stained hands, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `tray of lead movable type`, `small wooden printing press` (invocação, 800 tris)

#### 13. Almirante Colombo, o Desbravador (Cristóvão Colombo) — Resgate: Fase 330
* **Contexto:** navegador cuja frota chegou às Américas em 1492, marco da Era dos Descobrimentos.
* **Papel:** Lentidão em massa e dano de longo alcance.
* **Proporção e silhueta:** casaco naval longo, chapéu largo, luneta de latão estendida.
* **Paleta:** Azul-marinho `#1F2F55` · Ouro `#C9A646` · Verde-espuma `#7FC8B8`
* **Habilidade ativa — *Salva Lateral das Caravelas* (CD 20 s):** as caravelas *Santa María*, *Pinta* e *Niña* surgem na borda do mapa e bombardeiam; a rota é inundada e os inimigos terrestres ficam -70% mais lentos por 5 s.
* **Pose parada (idle):** olhando pela luneta, com a bússola na outra mão.
* **Vitória:** planta uma bandeira genérica de navegação (sem brasão real) e tira o chapéu.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, fifteenth-century navigator admiral, long navy blue naval coat with gold buttons, wide-brimmed hat, white ruffled collar, leather boots, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `brass spyglass`, `navigational compass`; **invocação:** `stylized chibi caravel ship, low poly` (800 tris, usado 3×)

#### 14. Leonardo, o Polímata Arcano (Leonardo da Vinci) — Resgate: Fase 350
* **Contexto:** símbolo do Renascimento; mestre da arte, da anatomia e da engenharia, autor de projetos militares visionários.
* **Papel:** Invocador (torreta temporária e bombardeio aéreo).
* **Proporção e silhueta:** túnica de veludo, barba longa ondulada, rolo de projetos debaixo do braço, chapéu macio.
* **Paleta:** Pergaminho `#E6D3A3` · Cobre `#C26A3A` · Veludo vinho `#6E2335`
* **Habilidade ativa — *Tanque Giratório & Ornitóptero* (CD 25 s):** posiciona um tanque de madeira e bronze que atira em 360° por 10 s e chama um ornitóptero que joga bombas de óleo incendiário na rota.
* **Pose parada (idle):** estudando um projeto aberto enquanto uma engrenagem flutuante gira ao lado.
* **Vitória:** o ornitóptero dá uma volta sobre ele e ele abre os braços, com projetos voando em volta.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, Renaissance polymath, burgundy velvet tunic, soft dark beret, long wavy grey beard, leather belt with pouches, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `rolled parchment blueprint`; **invocações:** `chibi wooden armored round tank with cannons` (800 tris), `chibi wooden ornithopter flying machine` (800 tris)

#### 15. Lutero, o Martelo da Reforma (Martinho Lutero) — Resgate: Fase 370
* **Contexto:** líder da Reforma Protestante; suas 95 Teses (Wittenberg, 1517) redesenharam o cenário religioso e político europeu.
* **Papel:** Quebra de defesas (escudos, armaduras e imunidades).
* **Proporção e silhueta:** hábito escuro de monge e acadêmico, rosto largo, martelo pesado, rolo de pergaminho.
* **Paleta:** Carvalho `#4A3222` · Ferro `#6D7278` · Pergaminho dourado `#E6C77A`
* **Habilidade ativa — *Impacto das 95 Teses* (CD 22 s):** uma martelada no chão gera uma onda de choque que **remove escudos, armaduras e imunidades** de todos os inimigos atingidos por 8 s.
* **Pose parada (idle):** martelo apoiado no ombro, segurando um pergaminho de teses que brilha.
* **Vitória:** prega o pergaminho numa porta de madeira que surge do chão; a onda de choque espalha páginas.
* **Tom:** heroico e convicto, sem caricatura de outras confissões (seção 8).
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, sixteenth-century German monk scholar, dark brown academic robe, rope belt, short tonsured hair, broad face, stern expression, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `heavy iron hammer`, `glowing parchment scroll`

#### 16. Sir Newton, o Lorde Gravitacional (Isaac Newton) — Resgate: Fase 390
* **Contexto:** formulou as leis do movimento e a Lei da Gravitação Universal e foi um dos criadores do cálculo; também estudou a decomposição da luz em prismas.
* **Papel:** Agrupamento (puxa os inimigos) e dano proporcional à massa.
* **Proporção e silhueta:** casaco barroco longo, peruca cacheada volumosa, prisma de cristal, maçã orbitando.
* **Paleta:** Violeta-veludo `#4B2E6B` · Ouro `#C9A646` · Espectro do prisma (arco-íris)
* **Habilidade ativa — *Singularidade Gravitacional* (CD 28 s):** cria um poço gravitacional que puxa os inimigos num raio de 6 un para o centro por 3 s e causa dano proporcional à massa (tropas pesadas sofrem mais; chefes são puxados 50% mais devagar).
* **Pose parada (idle):** examinando o prisma contra a luz, com o arco-íris projetado no chão; a maçã orbita devagar.
* **Vitória:** a maçã cai na cabeça dele, ele sorri e um diagrama de órbitas se desenha no ar.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, seventeenth-century English scientist, long baroque violet coat with gold buttons, white cravat, voluminous curly grey wig, buckled shoes, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `crystal glass prism`, `red apple`

---

### 5.5 Era 5 — Contemporânea & Futuro

#### 17. Napoléon, o Grande Artilheiro (Napoleão Bonaparte) — Resgate: Fase 410
* **Contexto:** imperador dos franceses; revolucionou o uso concentrado da artilharia e redesenhou a Europa no século XIX.
* **Papel:** Dano explosivo em área / anti-chefe blindado.
* **Proporção e silhueta:** bicorne enorme (o elemento-assinatura), casaco azul imperial, mão dentro do colete, canhão de latão ao lado.
* **Paleta:** Azul imperial `#1F3A8A` · Latão `#C9A646` · Fumaça de pólvora `#B8B2A7`
* **Habilidade ativa — *Grande Bateria Imperial* (CD 24 s):** salva sincronizada de canhões de 12 libras com dano explosivo massivo em área (+50% contra unidades blindadas e chefes).
* **Pose parada (idle):** mão dentro do colete e luneta na outra mão, junto ao canhão.
* **Vitória:** levanta o bicorne e os canhões disparam salvas de fumaça tricolor.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, early nineteenth-century French emperor, huge black bicorne hat, imperial blue military coat with gold epaulettes, white waistcoat, tall black boots, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `military telescope`, `saber`; **invocação:** `chibi brass twelve-pound cannon on wooden carriage` (800 tris)

#### 18. Marx, o Arquiteto do Proletariado (Karl Marx) — Resgate: Fase 430
* **Contexto:** filósofo, sociólogo e economista; teórico do socialismo científico e da análise da história pela luta de classes.
* **Papel:** Drenagem de vida e escudo para a base.
* **Proporção e silhueta:** barba enorme e volumosa (o elemento-assinatura), terno vitoriano escuro, livro grosso.
* **Paleta:** Carvão `#2A2A2E` · Carmesim luminoso `#D62839` · Papel `#E9E1CC`
* **Habilidade ativa — *União Proletária* (CD 26 s):** confisca 25% da vida atual de todos os inimigos em campo (chefes: 10%) e converte a energia num escudo temporário para o Núcleo.
* **Pose parada (idle):** lendo um livro grosso aberto, com páginas vermelhas brilhando.
* **Vitória:** fecha o livro com força e engrenagens vermelhas sobem em espiral.
* **Símbolo:** a v1.0 usava a foice e o martelo. A v2.0 substitui por **livro + engrenagem**, porque alguns países restringem a foice e o martelo por lei (seção 8.2). Reversível se a distribuição permitir.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, nineteenth-century German philosopher, huge bushy grey beard and hair, dark Victorian frock coat, waistcoat with pocket watch chain, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `thick leather-bound book`, `glowing red gear`

#### 19. Prof. Einstein, o Mestre Quântico (Albert Einstein) — Resgate: Fase 450
* **Contexto:** físico teórico criador das teorias da Relatividade Restrita e Geral, pilares da física moderna.
* **Papel:** Lentidão extrema seguida de explosão.
* **Proporção e silhueta:** cabelo branco desgrenhado (o elemento-assinatura), suéter e casaco de tweed, giz luminoso.
* **Paleta:** Tweed `#7D7568` · Azul elétrico `#2F8CFF` · Laranja atômico `#FFB627`
* **Habilidade ativa — *Dilatação Temporal & E=mc²* (CD 30 s):** desacelera os inimigos em 90% por 6 s (chefes: 50%); no fim, uma explosão converte massa em dano absoluto.
* **Pose parada (idle):** escrevendo equações luminosas no ar com giz, pensativo.
* **Vitória:** escreve "E=mc²" em letras gigantes e mostra a língua num sorriso travesso.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, early twentieth-century physicist, wild messy white hair, bushy white mustache, grey tweed jacket over knit sweater, baggy trousers, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `glowing chalk stick`

#### 20. Bapu, o Bastião da Paz (Mahatma Gandhi) — Resgate: Fase 460
* **Contexto:** líder da independência da Índia pela não violência (*Ahimsa*) e pela desobediência civil.
* **Papel:** Pacificação (impede ataques e congela a marcha). **Não causa dano direto**, coerente com a figura.
* **Proporção e silhueta:** magro, dhoti e xale brancos, óculos redondos, cajado de caminhada.
* **Paleta:** Linho branco `#F5F2EA` · Dourado suave `#F2CF7A` · Açafrão `#E8912D`
* **Habilidade ativa — *Resistência Pacífica* (CD 24 s):** projeta uma zona translúcida no caminho; dentro dela, os inimigos não atacam e param por 5 s. Chefes ficam apenas desacelerados.
* **Pose parada (idle):** caminhando no lugar com o cajado, sereno.
* **Vitória:** une as mãos (namastê) e uma roca de fiar dourada gira atrás dele.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, elderly Indian independence leader, simple white dhoti and white shawl, round wire spectacles, bald head, gentle smile, barefoot sandals, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `simple wooden walking staff`

#### 21. Turing, o Decodificador (Alan Turing) — Resgate: Fase 480
* **Contexto:** matemático britânico, pioneiro da ciência da computação e da inteligência artificial; teve papel decisivo na quebra das cifras Enigma na Segunda Guerra Mundial.
* **Papel:** Buff global de acerto crítico.
* **Proporção e silhueta:** casaco esportivo de tweed, cabelo penteado de lado, painéis holográficos binários em volta.
* **Paleta:** Tweed `#7D7568` · Verde-matriz `#3BFF7A` · Latão `#C9A646`
* **Habilidade ativa — *Overclock Algorítmico* (CD 30 s):** decodifica o padrão da onda; todas as torres e heróis acertam **100% de críticos** contra a onda atual por 8 s.
* **Pose parada (idle):** calculando em painéis holográficos flutuantes, com rotores de máquina cifradora girando.
* **Vitória:** os painéis exibem "DECODED" e engrenagens de latão se abrem como flor.
* **Restrição de arte:** **nenhuma** suástica ou símbolo nazista em props, inimigos ou VFX; a máquina cifradora inimiga usa uma insígnia fictícia (seção 8.2).
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, 1940s British mathematician, brown tweed sport jacket, knit tie, neatly parted dark hair, thoughtful expression, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop/VFX:** painéis binários (sprites), `brass cipher machine rotors` (prop, 400 tris)

#### 22. Commander Neil, o Pioneiro Lunar (Neil Armstrong) — Resgate: Fase 500
* **Contexto:** comandante da Apollo 11 e primeiro ser humano a pisar na Lua (1969), durante a Corrida Espacial.
* **Papel:** Lançamento ao ar (*knockup*) em massa.
* **Proporção e silhueta:** traje espacial volumoso (o mais "arredondado" do elenco), capacete com viseira dourada refletiva, mochila de suporte de vida.
* **Paleta:** Branco lunar `#F2F4F7` · Azul `#1F4FA8` · Ouro da viseira `#E8B23A`
* **Habilidade ativa — *Um Pequeno Passo* (CD 26 s):** salto orbital em gravidade zero seguido de pisão; a onda de choque lança todos os inimigos no ar por 3 s (chefes: 1 s).
* **Pose parada (idle):** flutuando levemente acima do chão, segurando a bandeira da missão.
* **Vitória:** finca a bandeira e deixa uma pegada luminosa no chão.
* **Restrição de arte:** **sem** logotipo da NASA, emblema da Apollo 11 ou bandeira nacional, pois são marcas e insígnias de uso controlado (seção 8.2). Usar um emblema de missão fictício.
* **Prompt Hunyuan3D:**
```text
stylized chibi 3D game character, 1969 astronaut, bulky white spacesuit with blue accents, round helmet with reflective gold visor, life-support backpack, thick gloves and moon boots, plain mission patch without logos, 2.8 heads tall, big head, oversized hands, A-pose, empty hands, full body, front view, hand-painted texture, flat even lighting, no baked shadows, mobile game asset, low poly
```
* **Prop separado:** `flag pole with blank white flag`

---

## 6. Bestiário e Chefes

### 6.1 Atributos de TD e Personificação Histórica

| Atributo | Mecânica | Exemplos por Era | Sinal visual (*telegraph*) |
| :--- | :--- | :--- | :--- |
| **Armor** | Reduz % de dano físico | Hoplita (2), Cavaleiro de placas (3), Tanque Mk I (5) | Faíscas metálicas no impacto; números de dano amarelos e menores |
| **Magic Resist** | Reduz % de dano mágico e elemental | Sacerdote de templo (2), Censor encapuzado (4), Jammer eletrônico (5) | Aura azul pulsante; projéteis mágicos se refratam |
| **Flying** | Ignora o caminho; exige torre antiaérea | Harpia (2), Ornitóptero corrompido (4), Zepelim e Drone (5) | Sombra projetada no chão + ícone de asa |
| **Stun / Hard CC** | Desativa torres temporariamente | Rinoceronte-lanudo (1), Elefante de guerra (2), Trabuco (3) | Anéis de choque no solo; faíscas sobre a torre afetada |
| **Camo / Stealth** | Invisível sem detector | Tigre-dente-de-sabre na neve (1), Shinobi (3), Submarino (5) | Shader de refração e contorno piscante a cada 2 s |
| **Regen** | Recupera vida por segundo | Hidra (2), Alquimista (4), Enxame de nanobots (5) | Partículas verdes subindo; a barra de vida pulsa |
| **Shield** | Camada extra de vida | Formação testudo (2), Muralha de escudos (3), Campo de plasma (5) | Barra azul sobre a vermelha |
| **Swarm** | Muitas unidades fracas | Gafanhotos (1), Ratos da peste (3), Drones (5) | Unidades pequenas em grupo, com som de enxame |

**Regras de creep:** 600 tris, silhueta legível a 32 px de altura e cor corrompida aplicada (seção 3.7). Animações: andar, ataque (se houver), dano (*hit flash* branco de 0,08 s), morte (dissolução em areia temporal, via shader e sem animação esquelética).

### 6.2 Os 50 Chefes (Fase 10 de cada capítulo)

> **Regra de design:** cada chefe tem um **contra recomendado** (herói ou tipo de torre), mas **nunca** exige um herói específico. Toda luta precisa ser vencível só com torres bem posicionadas.

**Era 1 — Pré-História**
| Fase | Chefe | Mecânica-chave | Contra recomendado | Recompensa |
| :---: | :--- | :--- | :--- | :--- |
| 10 | **Mamute-Lanudo Alfa** (Covil do Krog) | Pisão Glacial congela um campeão por 3 s a cada 12 s; aos 66% e 33% de vida chama 4 Lobos-Cinzentos | Lançador (área) e Atirador | **Krog** (resgatado) |
| 20 | Matilha Dente-de-Sabre | Camuflagem na neve | Torre detectora | Torre: Vigia de Osso |
| 30 | Urso-das-Cavernas | Regeneração ao "hibernar" | Krog (queimadura) | Fragmentos |
| 40 | Rinoceronte-Lanudo | Carga que atordoa torres | Torres de lentidão | Torre: Fosso de Estacas |
| 50 | Terror Alado (ave gigante) | Voador rápido | Antiaérea | Cosmético |
| 60 | Nuvem de Gafanhotos da Seca | Enxame voador | Dano em área | **Gara** |
| 70 | Gliptodonte | Escudo de carapaça | Krog / perfurante | Fragmentos |
| 80 | Megatério | Vida massiva | Gara (atordoamento) | Torre: Catapulta de Pedra |
| 90 | Leão-das-Cavernas | Invoca filhotes | Dano em área | Cosmético |
| 100 | **Fera Primordial da Entropia** | Fusão de megafauna, 3 fases | Combinação | Abre a Era 2 |

**Era 2 — Antiguidade**
| Fase | Chefe | Mecânica-chave | Contra recomendado | Recompensa |
| :---: | :--- | :--- | :--- | :--- |
| 110 | Lamassu Corrompido | Escudo + voo baixo | Antiaérea | **Hammur-Abi** |
| 120 | Escorpião-Rei & Múmias do Templo | Mortos-vivos com regeneração | Dano verdadeiro | Torre: Obelisco Solar |
| 130 | Hidra de Lerna | Cabeças regeneram (dividir dano) | Hammur-Abi | **Trino Socrático** |
| 140 | Esfinge | Alta resistência mágica | Dano físico | Fragmentos |
| 150 | Dario III & Elefantes de Guerra | Blindado + atordoamento de torres | Trino (confusão) | **Alexandros** |
| 160 | Elefantes de Aníbal | Carga em linha | Alexandros (empurrão) | Torre: Balista |
| 170 | Hordas Gaulesas | Ondas numerosas | Alexandros | **Imperator Julius** |
| 180 | Revoada de Harpias | Voadores em enxame | Antiaérea | Cosmético |
| 190 | Legião das Sombras do Vale | Mortos-vivos e escudos | Julius (*Armor Shred*) | **Yeshua** |
| 200 | **Colosso de Rodes Corrompido** | Vida massiva, pisões atordoam | Yeshua (cura) + Julius | Abre a Era 3 |

**Era 3 — Idade Média**
| Fase | Chefe | Mecânica-chave | Contra recomendado | Recompensa |
| :---: | :--- | :--- | :--- | :--- |
| 210 | Drakkar Viking | Desembarca tropas no meio da rota | Dano em área | Torre: Besteiros |
| 220 | Saqueadores das Dunas | Camuflagem na areia | Detector | **Al-Mu'allim** |
| 230 | Cavaleiros Negros | Armadura alta | Perfurante | Fragmentos |
| 240 | Muralha de Escudos Saxã | Escudo compartilhado | Al-Mu'allim | **Karolus** |
| 250 | Clã Shinobi | Camuflagem + teleporte | Detector | Torre: Sino de Alarme |
| 260 | Fortaleza Jin & Bestas de Cerco | Atordoa torres à distância | Karolus | **Temüjin** |
| 270 | Ratos da Peste Negra | Enxame com regeneração | Temüjin (em cadeia) | Cosmético |
| 280 | Arqueiros de Longo Alcance | Atacam torres à distância | Karolus (carga) | **Jehanne** |
| 290 | Trabuco Colossal | Atordoamento em área nas torres | Jehanne (buff) | Torre: Trabuco Aliado |
| 300 | **A Grande Bombarda de Constantinopla** (1453) | Disparos em área + muralha com escudo | Jehanne + Temüjin | Abre a Era 4 |

**Era 4 — Idade Moderna**
| Fase | Chefe | Mecânica-chave | Contra recomendado | Recompensa |
| :---: | :--- | :--- | :--- | :--- |
| 310 | Os Censores Encapuzados | Silenciam torres mágicas | Torres físicas | **Gutenberg** |
| 320 | Kraken do Porto | Tentáculos bloqueiam pedestais | Gutenberg (vulnerabilidade) | Torre: Canhão de Cais |
| 330 | Serpente da Tempestade Atlântica | Voa e mergulha | Antiaérea | **Colombo** |
| 340 | Galeões Piratas | Ondas navais com armadura | Colombo | Fragmentos |
| 350 | Máquinas de Cerco Corrompidas | Armadura muito alta | Gutenberg | **Leonardo** |
| 360 | Mosqueteiros em Formação | Escudo + tiro em torres | Leonardo (ornitóptero) | Torre: Mosquete |
| 370 | Golem Dogmático | Imunidades múltiplas | Leonardo | **Lutero** |
| 380 | Alquimista Corrompido & Homúnculos | Regeneração em grupo | Lutero (remove buffs) | Cosmético |
| 390 | Autômato Astronômico | Escudos rotativos | Lutero | **Newton** |
| 400 | **Dreadnought Leviatã a Vapor** | Voador gigante, -60% físico | Newton (puxão) + Lutero | Abre a Era 5 |

**Era 5 — Contemporânea & Futuro**
| Fase | Chefe | Mecânica-chave | Contra recomendado | Recompensa |
| :---: | :--- | :--- | :--- | :--- |
| 410 | Gigante do Inverno Russo | Congela torres | Fogo | **Napoléon** |
| 420 | Tanque Mk I | Armadura muito alta | Napoléon | Torre: Artilharia |
| 430 | Golem Fabril | Invoca operários-autômatos | Dano em área | **Marx** |
| 440 | Zepelim de Guerra | Voador que solta tropas | Antiaérea | Fragmentos |
| 450 | Elemental Atômico | Explosão em área ao morrer | Marx (escudo) | **Einstein** |
| 460 | Exércitos da Discórdia | Ondas que se fortalecem quando atacadas | Einstein (lentidão) | **Bapu** |
| 470 | Submarino Furtivo | Camuflagem + emerge no meio da rota | Detector | Torre: Radar |
| 480 | Máquina Cifradora Colossal | Embaralha alvos das torres | Bapu | **Turing** |
| 490 | Mainframe Criptografado Sentinela | Invisibilidade + escudo de dados; vulnerável a críticos | Turing | Cosmético |
| 500 | **A Entropia, Devoradora do Tempo** (Base Lunar) | 5 fases, uma por Era, com mecânicas de todos os chefes | Elenco completo | **Commander Neil** + créditos |

**Especificação de chefe:** até **5.000 tris** (proposta: o pedido original não definia este orçamento), textura 1024², até 60 ossos, escala em cena de 2,5–4 un. Barra de vida no topo da tela com o nome histórico.

---

## 7. Especificação de Assets — Hunyuan3D / TripoSR / Blender / Unity

### 7.1 Orçamentos Técnicos

| Categoria | Triângulos | Textura (albedo) | Ossos | Materiais | Escala em cena |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Herói** (montaria inclusa) | **≤ 2.500** | 1024² (512² no Low) | ≤ 40 (montado ≤ 45) | 1 | 1,1 un |
| **Creep** | **≤ 600** | 256² | ≤ 16 | 1 | 0,6–0,9 un |
| **Torre** (por nível, L1/L2/L3) | **≤ 1.200** | 512² (atlas por era) | 0–8 | 1 | 1,2–1,8 un |
| Chefe *(proposta)* | ≤ 5.000 | 1024² | ≤ 60 | 1–2 | 2,5–4 un |
| Invocação (tanque, caravela, canhão) | ≤ 800 | 512² | ≤ 12 | 1 | variável |
| ~~Pedestal~~ | — | — | — | — | Obsoleto desde v2.1 (posicionamento livre) |
| Prop de cenário | ≤ 400 | atlas da era | — | atlas | — |
| Covil de chefe / Portal | ≤ 3.000 / ≤ 800 | 1024² / 512² | — | 1–2 | Só como prop de cenário na moldura (não há bases desde v2.1) |

**Orçamento de cena (Medium tier):** ≤ 150 mil tris visíveis · ≤ 120 *draw calls* (SRP Batcher + GPU Instancing nos creeps) · ≤ 60 inimigos simultâneos · 30 FPS fixos (60 FPS opcional no High).

**Compressão:** ASTC 6×6 para albedo (ASTC 4×4 em rostos e UI 3D) · mipmaps ligados · sem mapa normal (o volume vem da pintura) · máscara de emissão no canal alfa quando necessário.

### 7.2 Pipeline de Produção

```
[1] Referência ──► [2] Geração 3D ──► [3] Limpeza & Retopo ──► [4] UV & Textura
                                                                      │
[8] QA in-game ◄── [7] Unity ◄── [6] Export ◄── [5] Rig & Animação ◄──┘
```

**[1] Referência de conceito.** Gerar a imagem do personagem **em A-pose, de frente, fundo branco, luz chapada e sem arma**, usando o prompt da ficha num gerador de imagem. É essa imagem que alimenta o passo 2 (o *image-to-3D* é mais fiel que o *text-to-3D*).

**[2] Geração 3D (.glb)**
| Ferramenta | Entrada | Quando usar |
| :--- | :--- | :--- |
| **Hunyuan3D** (2.x) | Texto **ou** imagem → malha + textura | **Padrão** para heróis e chefes: melhor forma e textura |
| **TripoSR** | Só imagem → malha | Iteração rápida de creeps, props e testes de silhueta; a textura costuma exigir repintura |

Regras: um personagem por geração; a arma é um *prop* separado; montaria separada do cavaleiro; o Trino Socrático é gerado em três figuras separadas.

**[3] Blender: limpeza e retopologia**
1. Importar o `.glb` e aplicar a escala (1 un = 1 m; altura de herói = 1,1 m).
2. `Merge by Distance`, apagar geometria solta, fechar buracos.
3. Reduzir ao orçamento: **Decimate (Collapse)** para creeps e props; **retopologia manual ou semiautomática** para heróis e chefes, garantindo *edge loops* em ombros, cotovelos, joelhos e pescoço para deformação.
4. Checar a silhueta vista pela câmera do jogo (perspectiva com FOV de 32°, X 42°, Y 0°) e em preto chapado.

**[4] UV e textura hand-painted**
1. *Unwrap* com prioridade de texels para **rosto e mãos** (25–30% do UV no herói).
2. **Bake** da textura da malha gerada (alta) para a malha final (baixa).
3. **Remover iluminação assada (*delight*).** Modelos de IA costumam pintar sombra e brilho na textura, o que conflita com o *cel-shading*. Achatar os valores e repintar o volume no estilo da Bíblia: luz suave em cima à esquerda e AO pintado discreto.
4. Passe final de pintura à mão no Blender (*Texture Paint*) ou em ferramenta externa: reforçar formas, bordas desgastadas e a paleta da ficha.
5. Máscara de emissão (runas, olhos mágicos, armas brilhantes) no canal alfa ou em textura separada de 256².

**[5] Rig e animação**
- **Heróis humanoides:** Rigify ou Mixamo (o chibi funciona bem em A-pose com braços afastados do corpo). Importados no Unity como **Humanoid**, para reaproveitar animações base com *retargeting*.
- **Montados, chefes e criaturas:** rig próprio, importado como **Generic**.
- No máximo **4 influências de osso por vértice**.
- Lista de animações por herói: seção 5.0. Creeps: andar, ataque, dano.
- Ritmo chibi: antecipação exagerada, contato rápido e *follow-through* elástico.

**[6] Exportação**
- **FBX** (recomendado para personagens animados no Unity, cuja importação de Humanoid é mais madura) **ou glTF/.glb via pacote glTFast** (bom para props estáticos).
- Eixos: −Z forward, Y up, "Apply Transform"; animações em *takes* nomeados.

**[7] Integração no Unity 6 / URP**
- Material: shader **Toon** do projeto (Shader Graph: *ramp* de 2 faixas, rim light, emissão, cor de sombra por era via parâmetro global).
- Outline: segunda passada *inverted hull* (Renderer Feature) com espessura por categoria (seção 3.1).
- Sombra de contato: decal elíptico como prefab filho.
- Creeps: GPU Instancing ligado; *Animator Culling Mode* = *Cull Update Transforms*.
- *Prefab variants* por nível de torre (L1/L2/L3).

**[8] Checklist de QA (por asset)**
- [ ] Dentro do orçamento de tris, ossos e textura
- [ ] Silhueta reconhecível a 64 px (herói), 32 px (creep) e em preto chapado
- [ ] Sem iluminação assada conflitante na textura
- [ ] Leitura correta sobre o fundo das 5 eras
- [ ] Inimigos com a cor corrompida; aliados sem magenta
- [ ] Animações com eventos de dano nos frames certos
- [ ] Testado nos tiers Low, Medium e High num aparelho real
- [ ] Revisão de sensibilidade cultural concluída (heróis 7, 8, 15, 18, 21, 22)

### 7.3 Convenção de Nomes e Pastas

```
Assets/_ChronoDefense/
├── Art/
│   ├── Heroes/HER_01_Krog/        HER_01_Krog.fbx · HER_01_Krog_Albedo.png · HER_01_Krog_Emis.png · HER_01_Krog.prefab
│   ├── Creeps/E1/                 CRP_E1_SabreTiger.fbx …
│   ├── Bosses/                    BOS_010_MammothAlpha.fbx …
│   ├── Towers/E1/                 TWR_E1_BoneWatch_L1.fbx · _L2 · _L3
│   ├── Environment/E1_Tundra/     ENV_E1_Atlas_Albedo.png · props da moldura (pedestal e covil obsoletos desde v2.1)
│   └── VFX/                       VFX_HER_01_FireCircle.prefab …
├── Shaders/                       SG_Toon.shadergraph · SG_Outline · SG_RuneScroll · SG_TemporalDissolve
└── Settings/URP/                  URP_Low.asset · URP_Medium.asset · URP_High.asset
```
Prefixos: `HER_` herói · `CRP_` creep · `BOS_` chefe · `TWR_` torre · `ENV_` cenário · `VFX_` efeito · `SG_` shader graph. Heróis numerados de 01 a 22 conforme este catálogo.

---

## 8. Notas de Sensibilidade Cultural e Legal

O jogo trata figuras religiosas, políticas e históricas reais. Isso é parte da sua força e também o seu maior risco de reputação e de distribuição. As regras abaixo valem para arte, animação, texto e marketing.

### 8.1 Figuras Religiosas
| Herói | Risco | Regra de produção |
| :--- | :--- | :--- |
| **Al-Mu'allim (Maomé)** | **Alto.** A representação visual do Profeta é considerada ofensiva por grande parte dos muçulmanos e pode levar a remoção da loja e boicote em vários mercados. | Representação **anicônica**: rosto nunca modelado, coberto por véu branco e luz. Sem falas inventadas, sem humor, sem animações de combate corpo a corpo. **Recomenda-se fortemente consultoria cultural** e avaliar substituí-lo por uma figura histórica do mundo islâmico medieval não profética (ver Ponto em Aberto nº 2). |
| **Yeshua (Jesus)** | Médio | Papel apenas protetor e curativo; sem armas, sem golpes, sem humor físico. |
| **Lutero** | Baixo/Médio | Heroico, sem satirizar outras confissões; os "Censores" (fase 310) são uma facção fictícia, não uma instituição real. |

### 8.2 Símbolos e Marcas Controlados
| Elemento | Problema | Solução adotada |
| :--- | :--- | :--- |
| Foice e martelo (Marx) | Proibidos ou restritos por lei em alguns países | Livro + engrenagem (seção 5.5, nº 18) |
| Simbologia nazista (contexto de Turing / Segunda Guerra) | Proibida na Alemanha e em outros mercados; risco nas lojas | Facção inimiga com insígnia fictícia; nenhuma suástica, águia nazista ou similar |
| Logotipo da NASA, emblema da Apollo 11, bandeiras nacionais | Marcas e insígnias com uso controlado | Emblema de missão fictício, bandeira branca |
| Brasões reais (Colombo, Joana, Carlos Magno) | Em geral domínio público, mas politicamente carregados | Versões estilizadas e genéricas |

### 8.3 Figuras Políticas e Controversas
Marx, Napoleão, Colombo e Gengis Khan têm legados disputados. Diretriz: retratar **a conquista ou contribuição histórica** que justifica a habilidade, sem endossar nem condenar o legado completo. Os textos de contexto são factuais e neutros. Revisão recomendada com um historiador consultor antes do lançamento.

### 8.4 Pessoas com direitos de imagem recentes
Einstein, Gandhi, Turing e Armstrong morreram no século XX, e seus nomes e imagens podem ter **direitos de personalidade e marca administrados por espólios** em algumas jurisdições. **Recomenda-se verificação jurídica** antes do uso comercial do nome e da aparência. Este documento não é parecer jurídico.

---

## 9. Pontos em Aberto para Decisão

| # | Questão | Proposta deste documento |
| :---: | :--- | :--- |
| 1 | Fases 1–9 ficam sem herói (o Krog é resgatado na Fase 10)? | Sim: as fases 1–9 funcionam como tutorial só com torres, e vencer o Mamute-Lanudo Alfa e libertar o Krog vira o primeiro grande momento do jogo. |
| 1b | Os inimigos saem do covil ou marcham até ele? | **Resolvido (v2.1):** não há bases; entram por uma borda da tela e saem pela outra. |
| 2 | Manter Maomé como herói jogável? | Manter apenas com representação anicônica e consultoria. Alternativa de menor risco: substituir por **Saladino** ou **Ibn al-Haytham** (pai da óptica, que combinaria com uma habilidade de luz e lentes). |
| 3 | Orçamento de chefes | 5.000 tris / 1024² (não especificado no pedido original). |
| 4 | Nome da força antagonista | "A Entropia" (provisório). |
| 5 | Símbolo de Marx | Livro + engrenagem; voltar à foice e martelo só se a lista de países de distribuição permitir. |
| 6 | Valores das habilidades e recargas | Alvos provisórios; balanceamento final no GDD Técnico. |

---

*Fim da Bíblia do Jogo Refinada v2.0. Próximo documento: **GDD Técnico** (fórmulas de dano, economia de ouro, curvas de dificuldade por fase, colisão e pathfinding).*
