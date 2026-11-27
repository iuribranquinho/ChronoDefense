# Geração 3D local (TripoSR + Hunyuan3D) em GPU AMD — ChronoDefense

Gera `.glb` a partir de um PNG, para a pipeline da Bíblia §7.2:
**Hunyuan3D** (heróis e chefes) e **TripoSR** (creeps, props, testes rápidos).
Os `.glb` saem *crus*: limpeza, retopo, UV e rig continuam no Blender antes de entrar em `Assets/_ChronoDefense/Art/`.

> Esta pasta (`Tools/`) fica fora de `Assets/` de propósito: o Unity não a importa. Não mova os venvs nem os pesos para `Assets/`.

## 1. Máquina validada

| Item | Valor |
|---|---|
| SO | Windows 11 Pro 10.0.26300 |
| GPU | AMD Radeon RX 6500 XT — **gfx1034 (RDNA2), 4 GB**, PCIe 4.0 x4 |
| Driver | Adrenalin 32.0.21045.29003 |
| CPU / RAM | Intel i5-14600KF (20 threads) / 32 GB |
| Python dos venvs | 3.12.10 (instalado com `py install 3.12`) |
| PyTorch | `2.15.0+rocm10.3.0a20261124` (TheRock nightly, HIP 7.18) |

### Por que ROCm/HIP e não DirectML
- O **PyTorch ROCm para Windows** do projeto oficial da AMD [TheRock](https://github.com/ROCm/TheRock/blob/main/RELEASES.md)
  publica wheels *multi-arch* com o extra `device-gfx1034` (RX 6500 XT). É **nightly / não verificado oficialmente**
  pela AMD para essa GPU, mas aqui funcionou: `torch.cuda.is_available() == True`,
  `get_device_name(0) == "AMD Radeon RX6500 XT"`, arquitetura `gfx1034`.
- `torch-directml` (último: 0.2.5.dev240914) está parado no **torch 2.4.1** e só aceita Python 3.8–3.12.
  Ficou como *fallback* no seletor de dispositivo, mas **não está instalado** (não foi necessário).
- `rembg` (remoção de fundo) usa ONNX Runtime: instalado `onnxruntime-directml` 1.24.4 →
  provider `DmlExecutionProvider` (GPU via DirectML).

Prova (`Tools/gpu_probe.py`, venv do TripoSR):
```
torch 2.15.0+rocm10.3.0a20261124 | hip: 7.18.26470
device: AMD Radeon RX6500 XT gfx1034
backend=hip tensor.device=cuda:0 | 20x matmul 2048: 74.1 ms (4.63 TFLOPS) | CPU: 17258.5 ms
```

## 2. Estrutura

```
Tools/
├── generate_3d.py       interface única: --engine triposr|hunyuan, PNG -> .glb
├── device_select.py     seleção HIP -> DirectML -> CPU (torch.set_num_threads)
├── gpu_probe.py         teste rápido do acelerador
├── README_3D.md         este arquivo
├── output/              saídas de teste
├── TripoSR/             clone de github.com/VAST-AI-Research/TripoSR (+ venv/)
└── Hunyuan3D/           clone de github.com/Tencent-Hunyuan/Hunyuan3D-2 (+ venv/)
```

Repositório do Hunyuan3D: o endereço `github.com/Tencent/Hunyuan3D-2.0` **não existe**; o oficial é
`Tencent-Hunyuan/Hunyuan3D-2` (o antigo `Tencent/Hunyuan3D-2` redireciona para ele).

Pesos ficam no cache do Hugging Face (`%USERPROFILE%\.cache\huggingface\hub`) e do rembg (`%USERPROFILE%\.u2net`),
fora do projeto. Tamanhos baixados:

| Modelo | Arquivo(s) | Tamanho |
|---|---|---|
| TripoSR | `stabilityai/TripoSR/model.ckpt` (+ config do `facebook/dino-vitb16`) | 1,56 GB |
| rembg | `u2net.onnx` | ~0,17 GB |
| Hunyuan3D-2mini Turbo | `hunyuan3d-dit-v2-mini-turbo/model.fp16.safetensors` | 3,56 GB |
| VAE turbo (FlashVDM) | `hunyuan3d-vae-v2-mini-turbo/model.fp16.safetensors` | 0,38 GB |
| *(não baixado)* Hunyuan3D-2 v2-0 completo | DiT 4,59 GB | — |
| *(não baixado)* Paint + Delight (textura) | ~9 GB | — |

## 3. Instalação (do zero)

```powershell
py install 3.12
cd C:\Users\iurib\Documents\ChronoDefense\Tools
git clone --depth 1 https://github.com/VAST-AI-Research/TripoSR.git TripoSR
git clone --depth 1 https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git Hunyuan3D
```

Em **cada** pasta (`TripoSR`, `Hunyuan3D`):
```powershell
py -V:3.12 -m venv venv
.\venv\Scripts\python.exe -m pip install --upgrade pip
.\venv\Scripts\python.exe -m pip install --index-url https://nightly.repo.amd.com/rocm/whl-next/ "torch[device-gfx1034]" "torchvision[device-gfx1034]"
```
(~1,1 GB por venv; o segundo reaproveita o cache do pip.) Outra GPU AMD: troque `gfx1034` pelo alvo da tabela do TheRock.

Requisitos adaptados — TripoSR (troca `torchmcubes`, que exige compilar C++, por `PyMCubes`; `onnxruntime-directml` para o rembg):
```powershell
.\venv\Scripts\python.exe -m pip install "omegaconf==2.3.0" "einops>=0.7" "transformers>=4.40,<4.50" "trimesh>=4.0.5" "rembg>=2.0.60" onnxruntime-directml huggingface-hub "imageio[ffmpeg]" "xatlas>=0.0.9" "moderngl>=5.10" PyMCubes
```
Hunyuan3D (sem gradio/fastapi; `onnxruntime-directml` no lugar de `onnxruntime`):
```powershell
.\venv\Scripts\python.exe -m pip install ninja pybind11 diffusers einops opencv-python "transformers>=4.48,<5" omegaconf tqdm trimesh pymeshlab pygltflib xatlas accelerate "rembg>=2.0.60" onnxruntime-directml
```

Não instale `onnxruntime` (CPU) junto com `onnxruntime-directml` no mesmo venv: eles conflitam.

### Alterações feitas nos clones
- `TripoSR/tsr/models/isosurface.py`: se `torchmcubes` não existir, usa `PyMCubes` (CPU) com a mesma convenção de eixos.
- `TripoSR/tsr/models/transformer/attention.py`: fatia a atenção por cabeças quando a matriz de scores passa de
  `CD3D_ATTN_CHUNK_MB` (padrão 256 MB) — ver "Armadilha de VRAM" abaixo.
- `TripoSR/run.py`: `--device auto|hip|directml|cpu` via `device_select.py` (padrão `auto`) e libera a sessão do rembg antes da inferência.
- `Hunyuan3D/hy3dgen/shapegen/utils.py`: com `use_safetensors`, o download ignora os `.ckpt` duplicados
  (o original baixava a subpasta inteira: 7,9 GB em vez de 3,9 GB para o mini-turbo).
- A troca de modelos e o SDPA fatiado do Hunyuan3D ficam em `generate_3d.py`/`device_select.py`, sem tocar no clone.
  Os scripts originais do Hunyuan (`minimal_demo.py`, `gradio_app.py`) **não** têm esses ajustes: use `generate_3d.py`.

## 4. Uso

Pode chamar com qualquer Python — o script se reexecuta no venv certo:
```powershell
py Tools\generate_3d.py --engine triposr entrada.png -o saida.glb
py Tools\generate_3d.py --engine hunyuan entrada.png -o saida.glb --report saida.json
```

Opções principais:

| Opção | Motor | Efeito |
|---|---|---|
| `--device auto\|hip\|directml\|cpu` | ambos | `auto` = HIP → DirectML → CPU. Variável `CD3D_THREADS` define threads na CPU |
| `--no-remove-bg` | ambos | pula o rembg (PNG RGBA com fundo transparente já pula sozinho) |
| `--report x.json` | ambos | grava backend, dispositivo, tempos por etapa, VRAM de pico e estatísticas da malha |
| `--mc-resolution 256` | TripoSR | resolução do marching cubes (densidade da malha) |
| `--fp16` | TripoSR | pesos fp16 no backbone (menos VRAM, um pouco mais rápido; qualidade pode variar) |
| `--bake-texture --texture-resolution 1024` | TripoSR | textura UV (xatlas + moderngl/OpenGL) em vez de cor por vértice |
| `--hunyuan-model mini-turbo\|mini-fast\|mini\|v2-turbo\|v2` | Hunyuan | DiT a usar (padrão `mini-turbo`; `v2*` baixa 4,6 GB e provavelmente não cabe em 4 GB) |
| `--steps N` | Hunyuan | passos de difusão (padrão 5 nos turbo, 30 nos demais) |
| `--octree-resolution 256` | Hunyuan | resolução da grade de decodificação |
| `--max-faces 40000` | Hunyuan | redução de faces (pymeshlab); 0 desliga |
| `--offload auto\|none\|conditioner\|full` | Hunyuan | gestão de VRAM (ver seção 5) |
| `--no-chunked-attn` | ambos | desliga o SDPA fatiado (`CD3D_ATTN_CHUNK_MB` ajusta o tamanho da fatia) |

Saída: `.glb` em Y-up (convenção glTF/Unity). O TripoSR é Z-up nativo e é rotacionado na exportação.

## 5. Resultados de teste (RX 6500 XT)

Entrada: `TripoSR/examples/horse.png` (RGBA). Saídas em `Tools/output/`.

### TripoSR — **roda na GPU (HIP)**
`output/test_triposr.glb` / `.json`:

| Etapa | GPU (HIP) | CPU (20 threads, `--device cpu`) |
|---|---|---|
| Carregar modelo | 7,6 s | 4,6 s |
| Inferência (triplano) | **3,2 s** | 385 s |
| Marching cubes 256³ (consulta de densidade na GPU + PyMCubes na CPU) | 5,3 s | 116 s |
| Total | **18 s** | ~8 min |

Malha: 45.006 vértices / 89.988 faces, estanque, enrolamento consistente; VRAM de pico 2,31 GB.
Com imagem opaca (`examples/captured.jpeg`) o rembg em DirectML soma ~42 s na 1ª sessão; a inferência continua em 3,4 s.

### Hunyuan3D-2mini Turbo (só forma) — **roda na GPU (HIP)**
`output/test_hunyuan.glb` / `.json` — 5 passos, `octree-resolution 256`, FlashVDM, redução para 40k faces:

| Etapa | GPU (HIP), versão final | GPU sem os ajustes abaixo | CPU (`--device cpu`, fp32) |
|---|---|---|---|
| Carregar modelo | 14,8 s | 15 s | 11,9 s |
| Difusão + decodificação do VAE + marching cubes | **46 s** | 476–530 s | interrompido: 2 de 5 passos em 46 min (~11–12 min/passo após ~20 min de encode) |
| Limpeza / redução de faces (pymeshlab) | 1,3 s | 1,4 s | — |
| Total | **65 s** | ~8–12 min | estimado > 1h30 |

Malha: 20.002 vértices / 40.000 faces, estanque, Y-up (cavalo: 0,37 × 1,44 × 1,95).

Dois ajustes foram necessários para caber em 4 GB (ambos ligados automaticamente pelo `generate_3d.py` em HIP):
1. **SDPA fatiado** (`device_select.install_chunked_sdpa`): o encoder DINOv2 roda em 1022² px (~5.300 tokens) e o DiT faz
   atenção conjunta sobre ~5.800 tokens. Sem flash/mem-efficient no gfx1034, o backend MATH montava matrizes de até
   9,6 GB. Fatiado em blocos de ≤256 MB: resultado idêntico (diferença máx. 6e-5), 4–11× mais rápido, pico +0,3 GB.
2. **Troca exclusiva de modelos** (`--offload auto` → `conditioner` em GPUs < 10 GB): os pesos somam 3,75 GB
   (DINOv2-giant 1,1 B parâmetros = 2,3 GB; DiT 0,56 B = 1,1 GB; VAE 0,6 GB). No encode só o condicionador fica na GPU;
   na difusão/decodificação só DiT + VAE. `--offload full` usa o `enable_model_cpu_offload` do diffusers (não testado);
   `--offload none` deixa tudo residente.

O pico registrado (`peak_vram_gb` 4,36) ocorre no encode do DINOv2 e transborda um pouco para a RAM; o resto roda
dentro da VRAM. Modelos maiores (`--hunyuan-model v2`/`v2-turbo`, DiT de 4,6 GB) **não foram testados** e não devem
caber em 4 GB sem `--offload full`.

## 6. O que roda onde

| Etapa | Onde roda | Observação |
|---|---|---|
| rembg (u2net) | GPU via DirectML (ONNX Runtime) | sessão é destruída antes da inferência para liberar VRAM |
| TripoSR: encoder DINO + transformer do triplano | GPU (HIP) | atenção só no backend MATH (sem flash/mem-efficient no gfx1034) |
| TripoSR: consulta de densidade/cor | GPU (HIP) | |
| TripoSR: marching cubes | **CPU** (PyMCubes) | `torchmcubes` precisa compilar C++/CUDA |
| TripoSR: bake de textura | GPU via OpenGL (moderngl) + xatlas na CPU | |
| Hunyuan3D: condicionador DINOv2 (encode da imagem) | GPU (HIP) | sobe para a GPU só durante o encode |
| Hunyuan3D: DiT de difusão (flow matching) | GPU (HIP), fp16 | atenção fatiada (MATH) |
| Hunyuan3D: decodificador do VAE (FlashVDM) | GPU (HIP), fp16 | |
| Hunyuan3D: marching cubes (`mc`, scikit-image) | **CPU** | `dmc` precisa do pacote `diso` (CUDA) |
| Hunyuan3D: limpeza / redução de faces | **CPU** (pymeshlab) | |
| Hunyuan3D-Paint (textura) | **NÃO FUNCIONA** | ver abaixo |

## 7. Limitações e bloqueios

### Hunyuan3D-Paint (textura) — desativado
- `custom_rasterizer` é um `CUDAExtension` (`rasterizer_gpu.cu` + `.cpp`). Não há compilador C++ nesta máquina
  (Visual Studio 2026 Community está instalado **sem** o workload "Desenvolvimento para desktop com C++"),
  e compilar para HIP no Windows exigiria hipificar o kernel e usar o `hipcc` do ROCm SDK — não testado.
  DirectML não tem como executar extensões CUDA.
- `differentiable_renderer/mesh_processor` tem versão em Python puro (`mesh_processor.py`) — esse ponto não bloqueia.
- O próprio README do Hunyuan3D-2 indica **16 GB de VRAM** para forma + textura; a Paint (UNet multivista baseada em
  SD 2.1 + modelo de delight) com `enable_model_cpu_offload` em 4 GB seria, na melhor hipótese, muito lenta.
- `generate_3d.py --engine hunyuan --texture` recusa com mensagem explicativa.
- **Alternativas hoje:** textura no Blender (projeção da imagem de conceito / pintura), ou gerar a forma no Hunyuan3D e a
  cor com `--engine triposr --bake-texture` como referência.
- **Para tentar habilitar no futuro:** instalar o workload C++ do Visual Studio, compilar `custom_rasterizer` em modo CPU
  (removendo o `.cu` do `setup.py` e usando `CppExtension`, se o código CPU de `rasterizer.cpp` cobrir o caminho usado)
  ou hipificado; baixar `hunyuan3d-paint-v2-0-turbo` + `hunyuan3d-delight-v2-0` (~9 GB); usar `--offload`.

### Armadilha de VRAM na RX 6500 XT
Medido aqui: buffers de até 384 MB rodam a ~120 GB/s; **uma alocação única ≥ 512 MB cai para ~12 GB/s**
(o driver a coloca na memória do sistema, via PCIe x4). Qualquer coisa que dispute a VRAM — outra sessão de GPU,
o Unity Editor aberto, a sessão DirectML do rembg — deixa a inferência ~6× mais lenta (TripoSR: 3,5 s → 20 s).
Feche o Unity/jogos antes de gerar lotes grandes.

### Outras
- PyTorch ROCm de `nightly.repo.amd.com` é **nightly**: uma atualização pode quebrar. Para reproduzir, fixe as versões
  (`pip freeze > requirements-lock.txt` em cada venv).
- MIOpen avisa `CK grouped conv library not found for device gfx1034` e falta de banco de kernels — inofensivo, só custa
  alguns segundos na primeira execução.
- O cache do Hugging Face no Windows sem Modo de Desenvolvedor não usa symlinks (ocupa um pouco mais de disco).
- **Licenças:** TripoSR é MIT. Hunyuan3D-2 usa a *Tencent Hunyuan 3D 2.0 Community License*: **não vale na União Europeia,
  Reino Unido e Coreia do Sul**, e produtos com mais de 1 milhão de usuários ativos mensais precisam de licença à parte
  da Tencent. Leia `Hunyuan3D/LICENSE` antes de usar os modelos em assets comerciais.
