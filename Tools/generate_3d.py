"""ChronoDefense — gerador 3D unificado (PNG -> .glb) para GPU AMD no Windows.

    py Tools\\generate_3d.py --engine triposr  entrada.png -o saida.glb
    py Tools\\generate_3d.py --engine hunyuan  entrada.png -o saida.glb

Pode ser chamado com qualquer Python: o script se reexecuta no venv do motor escolhido
(Tools\\TripoSR\\venv ou Tools\\Hunyuan3D\\venv). Dispositivo: HIP/ROCm -> DirectML -> CPU
(veja device_select.py). Detalhes e limitações: README_3D.md.
"""
from __future__ import annotations

import argparse
import json
import logging
import os
import subprocess
import sys
import time

TOOLS = os.path.dirname(os.path.abspath(__file__))
ENGINES = {"triposr": "TripoSR", "hunyuan": "Hunyuan3D"}
HUNYUAN_MODELS = {  # nome -> (repo HF, subpasta do DiT)
    "mini-turbo": ("tencent/Hunyuan3D-2mini", "hunyuan3d-dit-v2-mini-turbo"),
    "mini-fast": ("tencent/Hunyuan3D-2mini", "hunyuan3d-dit-v2-mini-fast"),
    "mini": ("tencent/Hunyuan3D-2mini", "hunyuan3d-dit-v2-mini"),
    "v2-turbo": ("tencent/Hunyuan3D-2", "hunyuan3d-dit-v2-0-turbo"),
    "v2": ("tencent/Hunyuan3D-2", "hunyuan3d-dit-v2-0"),
}

log = logging.getLogger("generate_3d")


def parse_args(argv=None):
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("image", help="PNG de entrada (RGBA com fundo transparente é o ideal)")
    p.add_argument("-o", "--output", help="arquivo .glb de saída (padrão: <imagem>_<motor>.glb)")
    p.add_argument("--engine", choices=ENGINES, default="triposr")
    p.add_argument("--device", default="auto", choices=["auto", "hip", "directml", "cpu"])
    p.add_argument("--no-remove-bg", action="store_true", help="não remover o fundo (rembg)")
    p.add_argument("--seed", type=int, default=1234)
    p.add_argument("--no-chunked-attn", action="store_true",
                   help="desliga o SDPA fatiado (ativo por padrão em HIP; fatia via CD3D_ATTN_CHUNK_MB=256)")
    p.add_argument("--report",help="grava um JSON com tempos/dispositivo/estatísticas da malha")
    g = p.add_argument_group("TripoSR")
    g.add_argument("--mc-resolution", type=int, default=256)
    g.add_argument("--chunk-size", type=int, default=8192)
    g.add_argument("--foreground-ratio", type=float, default=0.85)
    g.add_argument("--fp16", action="store_true", help="pesos em fp16 (menos VRAM, ~30%% mais rápido na RX 6500 XT)")
    g.add_argument("--bake-texture", action="store_true", help="textura UV (xatlas+moderngl) em vez de cor por vértice")
    g.add_argument("--texture-resolution", type=int, default=1024)
    h = p.add_argument_group("Hunyuan3D")
    h.add_argument("--hunyuan-model", choices=HUNYUAN_MODELS, default="mini-turbo")
    h.add_argument("--steps", type=int, help="passos de difusão (padrão: 5 turbo, 30 demais)")
    h.add_argument("--octree-resolution", type=int, default=256)
    h.add_argument("--num-chunks", type=int, default=8000, help="pontos por lote no decodificador do VAE")
    h.add_argument("--guidance", type=float, default=5.0)
    h.add_argument("--no-flashvdm", action="store_true", help="desliga o decodificador FlashVDM")
    h.add_argument("--max-faces", type=int, default=40000, help="redução de faces (0 = não reduz)")
    h.add_argument("--texture", action="store_true",
                   help="pintura de textura (Hunyuan3D-Paint) — NÃO suportada neste ambiente, veja README")
    h.add_argument("--offload", choices=["auto", "none", "conditioner", "full"], default="auto",
                   help="auto = 'conditioner' se a GPU tiver < 10 GB; full = enable_model_cpu_offload do diffusers")
    p.add_argument("--_worker", action="store_true", help=argparse.SUPPRESS)
    return p.parse_args(argv)


# ---------------------------------------------------------------- lançador
def venv_python(engine: str) -> str:
    return os.path.join(TOOLS, ENGINES[engine], "venv", "Scripts", "python.exe")


def relaunch(args) -> int:
    py = venv_python(args.engine)
    if not os.path.isfile(py):
        sys.exit(f"venv não encontrado: {py} (veja README_3D.md, seção Instalação)")
    env = dict(os.environ, HF_HUB_DISABLE_SYMLINKS_WARNING="1", PYTHONIOENCODING="utf-8")
    return subprocess.call([py, os.path.abspath(__file__), *sys.argv[1:], "--_worker"], env=env)


# ---------------------------------------------------------------- utilidades (worker)
class Timer:
    def __init__(self, device):
        self.device, self.items = device, {}

    def __call__(self, name):
        timer = self

        class _Ctx:
            def __enter__(self):
                from device_select import sync
                sync(timer.device)
                self.t = time.perf_counter()
                log.info("%s ...", name)

            def __exit__(self, *exc):
                from device_select import sync
                sync(timer.device)
                timer.items[name] = round(time.perf_counter() - self.t, 2)
                log.info("%s: %.2f s", name, timer.items[name])

        return _Ctx()


def load_rgba(path, remove_bg: bool):
    from PIL import Image

    img = Image.open(path)
    if remove_bg and not (img.mode == "RGBA" and img.getextrema()[3][0] < 250):
        import rembg  # onnxruntime-directml -> DmlExecutionProvider na GPU AMD

        session = rembg.new_session(providers=["DmlExecutionProvider", "CPUExecutionProvider"])
        img = rembg.remove(img.convert("RGB"), session=session)
        log.info("rembg: fundo removido (providers=%s)", session.inner_session.get_providers())
        # A sessão DirectML ocupa VRAM da mesma GPU; na RX 6500 XT (4 GB) mantê-la viva
        # empurra o modelo para a memória do sistema (inferência ~6x mais lenta).
        del session
        import gc
        gc.collect()
    return img.convert("RGBA")


def mesh_stats(path):
    import trimesh

    m = trimesh.load(path, force="mesh")
    return {"vertices": int(len(m.vertices)), "faces": int(len(m.faces)),
            "watertight": bool(m.is_watertight), "extents": [round(float(x), 4) for x in m.extents],
            "bytes": os.path.getsize(path)}


# ---------------------------------------------------------------- TripoSR
def run_triposr(args, device, timer):
    import numpy as np
    import torch
    import trimesh
    from PIL import Image

    sys.path.insert(0, os.path.join(TOOLS, "TripoSR"))
    from tsr.system import TSR
    from tsr.utils import resize_foreground, to_gradio_3d_orientation

    with timer("carregar modelo"):
        model = TSR.from_pretrained("stabilityai/TripoSR", config_name="config.yaml", weight_name="model.ckpt")
        model.renderer.set_chunk_size(args.chunk_size)
        model.to(device)
        if args.fp16:
            model.half()

    with timer("pré-processar imagem"):
        img = load_rgba(args.image, not args.no_remove_bg)
        if not args.no_remove_bg:
            img = resize_foreground(img, args.foreground_ratio)
        a = np.asarray(img).astype(np.float32) / 255.0
        a = a[:, :, :3] * a[:, :, 3:4] + (1 - a[:, :, 3:4]) * 0.5
        img = Image.fromarray((a * 255.0).astype(np.uint8))

    torch.manual_seed(args.seed)
    with timer("inferência (triplano)"), torch.no_grad():
        if args.fp16 and device.type == "cuda":
            with torch.autocast("cuda", dtype=torch.float16):
                codes = model([img], device=device)
        else:
            codes = model([img], device=device)
        if args.fp16:
            codes = codes.float()
            model.float()  # consulta de densidade/cor em fp32 para a malha

    with timer("extrair malha (marching cubes)"):
        mesh = model.extract_mesh(codes, not args.bake_texture, resolution=args.mc_resolution)[0]

    if args.bake_texture:
        from tsr.bake_texture import bake_texture

        with timer("bake de textura"):
            out = bake_texture(mesh, model, codes[0], args.texture_resolution)
            tex = Image.fromarray((out["colors"] * 255.0).astype(np.uint8)).transpose(Image.FLIP_TOP_BOTTOM)
            mesh = trimesh.Trimesh(vertices=mesh.vertices[out["vmapping"]], faces=out["indices"],
                                   visual=trimesh.visual.TextureVisuals(uv=out["uvs"], image=tex), process=False)

    to_gradio_3d_orientation(mesh)  # Z-up do TripoSR -> Y-up do glTF/Unity
    mesh.export(args.output)


# ---------------------------------------------------------------- Hunyuan3D
def swap_conditioner(pipe, device):
    """Pesos do Hunyuan3D-2mini em fp16: condicionador DINOv2 1,1 B (2,3 GB) + DiT 0,56 B (1,1 GB)
    + VAE (0,6 GB) = 3,75 GB — numa GPU de 4 GB qualquer ativação transborda para a RAM do sistema
    e tudo fica ~10x mais lento. O condicionador só roda uma vez por geração: durante encode_cond
    ele fica sozinho na GPU (DiT e VAE descem para a CPU); depois troca de volta. Custo: ~1 s de PCIe."""
    import torch

    pipe.conditioner.to("cpu")
    torch.cuda.empty_cache()
    original = pipe.encode_cond

    def encode_cond(*a, **kw):
        pipe.model.to("cpu")
        pipe.vae.to("cpu")
        torch.cuda.empty_cache()
        pipe.conditioner.to(device)
        try:
            return original(*a, **kw)
        finally:
            pipe.conditioner.to("cpu")
            torch.cuda.empty_cache()
            pipe.model.to(device)
            pipe.vae.to(device)

    pipe.encode_cond = encode_cond
    log.info("Hunyuan3D: condicionador em CPU, sobe para a GPU só no encode (offload=conditioner)")


def run_hunyuan(args, device, backend, timer):
    import torch

    if args.texture:
        sys.exit("Textura do Hunyuan3D-Paint não está disponível neste ambiente "
                 "(custom_rasterizer/mesh_processor exigem compilador C++ e o Paint exige >=12 GB de VRAM). "
                 "Gere só a forma e use --engine triposr --bake-texture ou texture no Blender. Veja README_3D.md.")
    if backend == "directml":
        log.warning("Hunyuan3D não foi validado em DirectML; se falhar use --device cpu")

    sys.path.insert(0, os.path.join(TOOLS, "Hunyuan3D"))
    from hy3dgen.shapegen import (DegenerateFaceRemover, FaceReducer, FloaterRemover,
                                  Hunyuan3DDiTFlowMatchingPipeline)

    repo, sub = HUNYUAN_MODELS[args.hunyuan_model]
    turbo = "turbo" in args.hunyuan_model
    steps = args.steps or (5 if turbo else 30)
    dtype = torch.float16 if device.type == "cuda" else torch.float32
    dev_str = "cuda" if device.type == "cuda" else str(device)

    with timer("carregar modelo"):
        pipe = Hunyuan3DDiTFlowMatchingPipeline.from_pretrained(
            repo, subfolder=sub, use_safetensors=True, variant="fp16", device=dev_str, dtype=dtype)
        if not args.no_flashvdm:
            pipe.enable_flashvdm(mc_algo="mc")
        if device.type == "cuda":
            vram = torch.cuda.get_device_properties(0).total_memory / 2**30
            if args.offload == "full":
                pipe.enable_model_cpu_offload()
            elif args.offload == "conditioner" or (args.offload == "auto" and vram < 10):
                swap_conditioner(pipe, device)

    with timer("pré-processar imagem"):
        img = load_rgba(args.image, not args.no_remove_bg)

    with timer("difusão + decodificação (forma)"), torch.no_grad():
        gen = torch.Generator(device="cpu").manual_seed(args.seed)
        mesh = pipe(image=img, num_inference_steps=steps, octree_resolution=args.octree_resolution,
                    num_chunks=args.num_chunks, guidance_scale=args.guidance, generator=gen,
                    output_type="trimesh")[0]

    with timer("limpeza da malha"):
        mesh = FloaterRemover()(mesh)
        mesh = DegenerateFaceRemover()(mesh)
        if args.max_faces and len(mesh.faces) > args.max_faces:
            mesh = FaceReducer()(mesh, max_facenum=args.max_faces)
    mesh.export(args.output)


# ---------------------------------------------------------------- worker
def worker(args) -> int:
    logging.basicConfig(format="%(asctime)s %(levelname)s %(message)s", level=logging.INFO)
    sys.path.insert(0, TOOLS)
    from device_select import pick_device

    device, backend = pick_device(args.device)
    if backend == "hip" and not args.no_chunked_attn:
        from device_select import install_chunked_sdpa
        install_chunked_sdpa()  # antes de importar os modelos (eles guardam a referência do SDPA)
    timer = Timer(device)
    t0 = time.perf_counter()
    if args.engine == "triposr":
        run_triposr(args, device, timer)
    else:
        run_hunyuan(args, device, backend, timer)
    total = round(time.perf_counter() - t0, 2)

    import torch

    info = {"engine": args.engine, "backend": backend, "device": str(device),
            "device_name": torch.cuda.get_device_name(0) if device.type == "cuda" else backend,
            "torch": torch.__version__, "input": os.path.abspath(args.image),
            "output": os.path.abspath(args.output), "seconds": timer.items, "total_seconds": total,
            "mesh": mesh_stats(args.output)}
    if device.type == "cuda":
        info["peak_vram_gb"] = round(torch.cuda.max_memory_allocated() / 2**30, 2)
    log.info("OK -> %s", json.dumps(info, ensure_ascii=False))
    if args.report:
        with open(args.report, "w", encoding="utf-8") as f:
            json.dump(info, f, indent=2, ensure_ascii=False)
    return 0


def main() -> int:
    args = parse_args()
    if args.output is None:
        args.output = os.path.splitext(args.image)[0] + f"_{args.engine}.glb"
    if not args.output.lower().endswith(".glb"):
        sys.exit("A saída precisa ser .glb")
    args.image, args.output = os.path.abspath(args.image), os.path.abspath(args.output)
    os.makedirs(os.path.dirname(args.output), exist_ok=True)
    return worker(args) if args._worker else relaunch(args)


if __name__ == "__main__":
    sys.exit(main())
