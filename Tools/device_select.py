"""Seleção automática de dispositivo para GPUs AMD no Windows.

Ordem: HIP/ROCm (torch.cuda via HIP) -> DirectML (torch-directml) -> CPU.
Uso:
    from device_select import pick_device, sync
    dev, backend = pick_device()          # ou pick_device("cpu") / "directml" / "hip"
"""
from __future__ import annotations

import logging
import os

import torch

log = logging.getLogger("device_select")


def _cpu() -> tuple[torch.device, str]:
    n = int(os.environ.get("CD3D_THREADS", os.cpu_count() or 4))
    torch.set_num_threads(n)
    log.info("Dispositivo: CPU (%d threads)", n)
    return torch.device("cpu"), "cpu"


def pick_device(prefer: str = "auto") -> tuple[torch.device, str]:
    prefer = (prefer or "auto").lower()
    if prefer == "cpu":
        return _cpu()

    if prefer in ("auto", "hip", "cuda") and torch.cuda.is_available():
        backend = "hip" if getattr(torch.version, "hip", None) else "cuda"
        props = torch.cuda.get_device_properties(0)
        log.info("Dispositivo: %s | %s %s | VRAM %.1f GB | torch %s", backend.upper(),
                 props.name, getattr(props, "gcnArchName", ""), props.total_memory / 2**30,
                 torch.__version__)
        return torch.device("cuda:0"), backend

    if prefer in ("auto", "directml", "dml"):
        try:
            import torch_directml  # type: ignore

            dev = torch_directml.device()
            log.info("Dispositivo: DirectML | %s",
                     torch_directml.device_name(torch_directml.default_device()))
            return dev, "directml"
        except Exception as e:  # pacote ausente ou sem adaptador
            if prefer != "auto":
                log.warning("DirectML indisponível (%s); caindo para CPU", e)

    if prefer not in ("auto", "cpu"):
        log.warning("Backend '%s' indisponível; caindo para CPU", prefer)
    return _cpu()


def install_chunked_sdpa(max_mb: int | None = None) -> None:
    """Substitui torch.nn.functional.scaled_dot_product_attention por uma versão fatiada.

    Em GPUs sem kernel flash/mem-efficient (RDNA2 via HIP no Windows) o SDPA cai no backend
    MATH, que materializa a matriz de scores inteira (B*H*Lq*Lk). No Hunyuan3D isso passa de 1 GB
    por camada e transborda a VRAM de 4 GB. Aqui a atenção é calculada em fatias (cabeças x linhas
    de query) de no máximo `max_mb` MB — resultado idêntico, pico de memória limitado.
    Chame ANTES de importar módulos que fazem `sdpa = F.scaled_dot_product_attention`.
    """
    import torch.nn.functional as F

    if getattr(F.scaled_dot_product_attention, "_cd3d_chunked", False):
        return
    original = F.scaled_dot_product_attention
    limit = (max_mb or int(os.environ.get("CD3D_ATTN_CHUNK_MB", "256"))) * 2**20

    def chunked(query, key, value, attn_mask=None, dropout_p=0.0, is_causal=False, scale=None, **kw):
        lq, lk = query.shape[-2], key.shape[-2]
        lead = query.shape[:-2]
        n = 1
        for s in lead:
            n *= s
        row_bytes = lk * 4  # o softmax do backend MATH acumula em fp32
        if (attn_mask is not None or is_causal or kw or query.device.type != "cuda"
                or n * lq * row_bytes <= limit or key.shape[:-2] != lead or value.shape[:-2] != lead):
            return original(query, key, value, attn_mask=attn_mask, dropout_p=dropout_p,
                            is_causal=is_causal, scale=scale, **kw)
        q = query.reshape(n, lq, query.shape[-1])
        k = key.reshape(n, lk, key.shape[-1])
        v = value.reshape(n, lk, value.shape[-1])
        out = q.new_empty(n, lq, value.shape[-1])
        rows = max(1, limit // row_bytes)  # linhas de query (somando cabeças) por fatia
        if rows >= lq:
            heads = max(1, rows // lq)
            for h in range(0, n, heads):
                out[h:h + heads] = original(q[h:h + heads], k[h:h + heads], v[h:h + heads],
                                            dropout_p=dropout_p, scale=scale)
        else:
            for h in range(n):
                for r in range(0, lq, rows):
                    out[h:h + 1, r:r + rows] = original(q[h:h + 1, r:r + rows], k[h:h + 1], v[h:h + 1],
                                                        dropout_p=dropout_p, scale=scale)
        return out.reshape(*lead, lq, value.shape[-1])

    chunked._cd3d_chunked = True
    F.scaled_dot_product_attention = chunked
    torch.nn.functional.scaled_dot_product_attention = chunked
    log.info("SDPA fatiado ativo (fatias <= %d MB)", limit // 2**20)


def sync(device) -> None:
    """Sincroniza a GPU para medições de tempo corretas."""
    if getattr(device, "type", str(device)).startswith("cuda") and torch.cuda.is_available():
        torch.cuda.synchronize()
