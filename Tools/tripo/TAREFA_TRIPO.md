# Tarefa para o Claude Code: gerar os modelos 3D da Era 1 no Tripo3D

Abra o Claude Code na pasta do projeto (`C:\Users\iurib\Documents\ChronoDefense`) e diga:
**"Execute Tools/tripo/TAREFA_TRIPO.md"**.

## 0. Preparação (uma vez, no terminal)

```
claude mcp add --transport http --scope user --client-id studio-claude-code --callback-port 3000 tripo https://msapi.tripo3d.ai/mcp
```
Na primeira chamada de uma ferramenta do Tripo, o navegador abre para você entrar na sua conta Tripo.

## 1. Regras para o Claude Code

- Siga o `CLAUDE.md` do projeto. A lista do que gerar, com prompts e medidas, está em `Tools/tripo/modelos_era1.json`.
- **Antes de gerar qualquer coisa**: consulte o saldo de créditos pelo MCP do Tripo (se houver ferramenta para isso), estime o custo das 16 gerações com os parâmetros abaixo e **peça confirmação ao usuário**. Não gere sem o "sim".
- Monte o prompt final de cada modelo substituindo `{estilo_comum}` e `{corrupcao}` pelos textos do JSON; use `negativo_comum` como prompt negativo, se a ferramenta aceitar.
- Parâmetros: texto → 3D, com textura e PBR (`parametros_tripo`): geração `texture_quality: detailed` (4K) e exportação do `.glb` com `texture_size: 2048`; `face_limit` do item. O `normalize_glb.py` também reduz qualquer textura acima de 2048 px (`--max-texture`), então um arquivo que venha em 4K não chega pesado ao Unity. Se a ferramenta tiver outro nome para algum parâmetro, use o equivalente e registre no relatório.
- Gere em ordem: chefe e inimigos primeiro, depois torres, depois cenário.

## 2. Para cada modelo

1. Gere pelo MCP do Tripo e espere a tarefa terminar.
2. **Confira a imagem de prévia** que o Tripo devolve: um objeto só, sem chão/base, inteiro, no estilo pedido. Para animais e personagens, a frente precisa estar visível na prévia (olhando para a câmera). Se falhar, gere de novo (no máximo 2 novas tentativas por modelo; registre o motivo).
3. Baixe o `.glb` para o caminho `saida` do item (crie as pastas).
4. Normalize (pivô no centro da base em Y=0, medida exata, malha única com as transformações aplicadas):
   ```
   Tools\TripoSR\venv\Scripts\python.exe Tools\normalize_glb.py <saida> <normalizar>
   ```
   Se o modelo estiver de lado ou de costas (a frente precisa ficar em +Z), rode de novo com `--yaw 90`, `--yaw -90` ou `--yaw 180`. Para conferir a frente, renderize uma vista de cima com trimesh/matplotlib no mesmo venv e veja para onde aponta a cabeça.
5. Registre em `Tools/tripo/relatorio_tripo.json`: id, prompt final, id da tarefa no Tripo, créditos gastos, tentativas, triângulos, medidas finais (saída do `normalize_glb.py`) e observações.

## 3. Depois de todos os modelos

1. Não apague nem sobrescreva `Assets/_ChronoDefense/Art/Heroes/HER_01_Krog/*` (as poses do Krog já estão prontas).
2. No Unity (ou em batchmode): **Tools ▸ Chrono ▸ Gerar dados do MVP (Era 1 · Cap. 1)** e depois **Tools ▸ Chrono ▸ Importar modelos 3D (Era 1)**. O log precisa mostrar "OK" em todos os modelos gerados.
3. Rode as suítes EditMode e PlayMode; `ModelImportTests` valida pivô, medida, material e ligação de cada modelo.
4. Rode o `GameplayCaptureTests` e abra `Logs/Batch/gameplay.png` para conferir o resultado na câmera do jogo.
5. Resuma para o usuário: o que foi gerado, créditos gastos, o que precisou de nova tentativa e qualquer modelo reprovado.

## Onde cada modelo aparece no jogo

| Modelo | Uso |
| :-- | :-- |
| `BOS_010_MammothAlpha` | Chefe da Fase 10 (anda pelo caminho; ampliado por `ArtLibrary.UnitScale`) |
| `CRP_E1_*` | Inimigos das ondas |
| `TWR_E1_*` | Torres nos pedestais (`tema.TowerSkins`) |
| `ENV_E1_Pedestal` | Pedestais (`tema.Pedestal`) |
| `ENV_E1_KrogCave` | Covil de onde saem os inimigos (`tema.Portal` e `BossDataSO.LairPrefab`) |
| `ENV_E1_ChronoCore` | Núcleo Cronológico, a base do jogador (`tema.Core`) |
| `ENV_E1_MammothSkeleton`, `Campfire`, `SnowyPine`, `Rock`, `IceBlock` | Decoração (o posicionamento no mapa vem na Fase C) |
