# Relatório — Ambiente local de geração 3D em GPU AMD

**Projeto:** ChronoDefense · **Data:** 25/11/2026 · **Pasta:** `Tools/` (fora de `Assets/`)
**Objetivo:** gerar `.glb` a partir de PNG com TripoSR (creeps, props, testes) e Hunyuan3D (heróis e chefes), conforme a Bíblia §7.2, numa máquina com GPU AMD.

## 1. Conclusão em uma frase

**Os dois motores funcionam na GPU AMD:** o TripoSR gera um modelo em ~18 s e o Hunyuan3D-2mini (só a forma) em ~65 s. A **textura automática do Hunyuan3D não funciona** nesta máquina.

## 2. Status das entregas

| Entrega | Status |
|---|---|
| PyTorch acelerado na GPU AMD | ✅ ROCm/HIP nativo no Windows (não precisou de DirectML) |
| `Tools/TripoSR` + `venv` | ✅ `.glb` válido gerado na GPU |
| `Tools/Hunyuan3D` + `venv` | ✅ `.glb` válido gerado na GPU (só forma) |
| Textura do Hunyuan3D (Paint) | ❌ bloqueada (ver §5) |
| `Tools/generate_3d.py` (interface única) | ✅ `--engine triposr\|hunyuan`, PNG → `.glb` |
| `Tools/README_3D.md` | ✅ instalação, uso e limitações |

## 3. Máquina e decisões técnicas

| Item | Valor / decisão |
|---|---|
| GPU | AMD Radeon RX 6500 XT — gfx1034 (RDNA2), **4 GB**, PCIe 4.0 x4 |
| Driver | 32.0.21045.29003 |
| SO / CPU / RAM | Windows 11 Pro 26300 · i5-14600KF · 32 GB |
| Python | 3.12.10 nos venvs. O 3.15 do sistema não é compatível com esses pacotes |
| PyTorch | `2.15.0+rocm10.3.0a20261124`, wheels **nightly** do TheRock (AMD), extra `device-gfx1034` |
| Remoção de fundo | `rembg` + `onnxruntime-directml` (GPU via DirectML) |
| Repositório Hunyuan | `Tencent/Hunyuan3D-2.0` **não existe**; o oficial é `Tencent-Hunyuan/Hunyuan3D-2` |

**Por que HIP e não DirectML:** o `torch-directml` está parado no torch 2.4.1 (setembro de 2024). Já o TheRock publica PyTorch ROCm atual com suporte à gfx1034 no Windows, e ele funcionou: `torch.cuda.is_available() == True`, ~4,6 TFLOPS num matmul fp32. O DirectML ficou só como alternativa no seletor de dispositivo.

## 4. Resultados medidos (entrada: `horse.png`)

| Motor | GPU (HIP) | CPU (20 threads) | Malha |
|---|---|---|---|
| **TripoSR** | **18 s** no total (3,2 s de inferência) | ~8 min | 45k vértices, estanque |
| **Hunyuan3D-2mini Turbo** (5 passos) | **65 s** no total (46 s de geração) | interrompido: 2 de 5 passos em 46 min | 40k faces, estanque |

Os dois saem em **Y-up** (padrão glTF/Unity) e prontos para limpeza e rig no Blender.

## 5. O que aprendemos

1. **Nesta placa, a VRAM disponível é menor que 4 GB.** Uma alocação única de 512 MB ou mais é colocada na RAM do sistema e fica ~10× mais lenta (12 GB/s contra 120 GB/s). Qualquer outro programa usando a GPU (Unity Editor, a sessão do rembg) derruba o desempenho.
2. **Sem atenção rápida no RDNA2.** Os kernels de atenção otimizados (flash / mem-efficient) não existem para a gfx1034, e o modo padrão chegava a pedir 9,6 GB. A **atenção fatiada** dá o mesmo resultado (diferença ≤ 6e-5), é 4–11× mais rápida e usa ~0,3 GB.
3. **Os pesos do Hunyuan3D-2mini somam 3,75 GB.** O maior é o leitor de imagem DINOv2, com 2,3 GB. A solução foi trocar os modelos entre CPU e GPU: cada um só fica na GPU enquanto é usado.
4. **Efeito das otimizações:**
   - Hunyuan3D: de **8–12 min para 65 s**.
   - TripoSR: inferência de **20 s para 3,2 s**, só por liberar a VRAM do rembg antes de gerar.

## 6. Limitações e bloqueios

| Item | Situação | Motivo |
|---|---|---|
| Textura Hunyuan3D-Paint | ❌ não funciona | `custom_rasterizer` precisa compilar CUDA/C++; o Visual Studio 2026 não tem o pacote de C++; o Paint pede ≥ 12–16 GB de VRAM |
| Hunyuan3D-2 completo (DiT 4,6 GB) | ⚠️ não testado | não deve caber em 4 GB sem `--offload full` |
| Marching cubes | ⚠️ roda na CPU | `torchmcubes` precisa de compilador; troquei por `PyMCubes` (5 s, aceitável) |
| PyTorch nightly | ⚠️ risco | uma atualização pode quebrar; recomendado fixar as versões com `pip freeze` |
| Scripts originais do Hunyuan (`gradio_app.py`, `minimal_demo.py`) | ⚠️ lentos | não têm as otimizações; use o `generate_3d.py` |

**Alternativas para textura hoje:** projetar ou pintar no Blender, ou usar o TripoSR com `--bake-texture` como referência de cor.

## 7. Licenças

- **TripoSR:** MIT. Uso livre.
- **Hunyuan3D-2:** *Tencent Hunyuan 3D 2.0 Community License*.
  - **Não vale na União Europeia, no Reino Unido nem na Coreia do Sul.**
  - Produtos com mais de 1 milhão de usuários ativos por mês precisam de licença à parte da Tencent.
  - Revisar `Tools/Hunyuan3D/LICENSE` antes de usar os assets gerados comercialmente.

## 8. Recomendações para a pipeline da Bíblia §7.2

1. **Creeps, props e iteração rápida:** TripoSR, ~18 s por modelo. Dá para gerar várias variações e escolher.
2. **Heróis e chefes:** Hunyuan3D-2mini Turbo, ~1 min por forma, com malha mais limpa e detalhada. Textura e rig no Blender.
3. Feche o Unity Editor antes de gerar lotes.
4. **Para destravar a textura do Hunyuan** (opcional, trabalho futuro):
   - instalar o pacote "Desenvolvimento para desktop com C++" do Visual Studio;
   - compilar o `custom_rasterizer` em modo CPU;
   - baixar ~9 GB de pesos do Paint e do Delight.

   Mesmo assim, com 4 GB de VRAM deve ficar muito lento. **Uma GPU com 12 GB ou mais é o caminho realista para textura automática.**

## 9. Uso rápido

```powershell
py Tools\generate_3d.py --engine triposr entrada.png -o saida.glb
py Tools\generate_3d.py --engine hunyuan entrada.png -o saida.glb --report saida.json
```

## 10. Arquivos

| Arquivo | Função |
|---|---|
| `Tools/generate_3d.py` | interface única (escolhe o venv, mede tempos e grava o relatório JSON) |
| `Tools/device_select.py` | escolhe o dispositivo (HIP → DirectML → CPU) e instala a atenção fatiada |
| `Tools/gpu_probe.py` | teste rápido da GPU |
| `Tools/README_3D.md` | documentação completa |
| `Tools/output/test_triposr.glb` · `.json` | teste do TripoSR |
| `Tools/output/test_hunyuan.glb` · `.json` | teste do Hunyuan3D |

**Arquivos alterados nos clones:**
- TripoSR: `isosurface.py` (fallback PyMCubes), `attention.py` (atenção fatiada) e `run.py` (seletor de dispositivo e liberação do rembg).
- Hunyuan3D: `shapegen/utils.py` (não baixa os `.ckpt` duplicados, economiza 3,6 GB).
