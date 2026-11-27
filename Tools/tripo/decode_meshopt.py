"""Descomprime um .glb exportado pelo Tripo (EXT_meshopt_compression + KHR_mesh_quantization) para um .glb comum.

    Tools\\TripoSR\\venv\\Scripts\\python.exe Tools\\tripo\\decode_meshopt.py entrada.glb saida.glb

Por que existe: o trimesh (usado pelo normalize_glb.py) não lê meshopt, e o glTFast só lê com o pacote
com.unity.meshopt.decompress. Implementação em Python puro + numpy (sem dependências novas), seguindo o
decodificador de referência do meshoptimizer:
  - ATTRIBUTES: codec de vértices v0 (header 0xA0); filtros NONE e EXPONENTIAL (OCTAHEDRAL/QUATERNION: erro).
  - TRIANGLES: codec de índices v0/v1 (header 0xE0/0xE1).
Atributos inteiros normalizados (KHR_mesh_quantization) viram float32, então a saída não exige extensões.
"""
import json
import struct
import sys

import numpy as np

# ---------------------------------------------------------------- vértices (ATTRIBUTES, v0)


def _decode_bytes(data, pos, size):
    """Decodifica `size` bytes (múltiplo de 16) em grupos de 16; devolve (bytes, nova posição)."""
    out = bytearray(size)
    groups = size // 16
    header = pos
    pos += (groups + 3) // 4
    for g in range(groups):
        bitslog2 = (data[header + g // 4] >> ((g % 4) * 2)) & 3
        base = g * 16
        if bitslog2 == 0:
            continue  # tudo zero
        if bitslog2 == 3:
            out[base:base + 16] = data[pos:pos + 16]
            pos += 16
            continue
        bits = 1 << bitslog2  # 2 ou 4
        sentinel = (1 << bits) - 1
        fixed = 16 * bits // 8
        var = pos + fixed
        per_byte = 8 // bits
        for i in range(16):
            byte = data[pos + i // per_byte]
            enc = (byte >> (8 - bits * (i % per_byte + 1))) & sentinel
            if enc == sentinel:
                enc = data[var]
                var += 1
            out[base + i] = enc
        pos = var
    return out, pos


def decode_vertex_buffer(data, count, stride):
    if data[0] != 0xA0:
        raise ValueError(f"codec de vértices não suportado: header 0x{data[0]:02x}")
    tail = max(stride, 32)  # tail = enchimento com zeros + primeiro vértice (nos últimos `stride` bytes)
    last = bytearray(data[len(data) - stride:])
    block = min((8192 // stride) & ~15, 256)
    out = np.zeros((count, stride), dtype=np.uint8)
    pos = 1
    for start in range(0, count, block):
        n = min(block, count - start)
        aligned = (n + 15) & ~15
        for k in range(stride):
            raw, pos = _decode_bytes(data, pos, aligned)
            v = np.frombuffer(bytes(raw[:n]), dtype=np.uint8).astype(np.int32)
            delta = (-(v & 1)) ^ (v >> 1)  # unzigzag8
            col = (np.cumsum(delta) + last[k]) & 0xFF
            out[start:start + n, k] = col
            last[k] = int(col[-1])
    if pos > len(data) - tail:
        raise ValueError("stream de vértices corrompido (passou do tail)")
    return out.tobytes()


def filter_exponential(raw, stride):
    v = np.frombuffer(raw, dtype=np.int32)
    e = v >> 24
    m = (v << 8) >> 8
    return np.ldexp(m.astype(np.float64), e).astype(np.float32).tobytes()


# ---------------------------------------------------------------- índices (TRIANGLES, v0/v1)


def _decode_index(data, pos, last):
    v, shift = 0, 0
    while True:
        b = data[pos]
        pos += 1
        v |= (b & 0x7F) << shift
        shift += 7
        if b < 0x80:
            break
    d = (v >> 1) ^ -(v & 1)
    return (last + d) & 0xFFFFFFFF, pos


def decode_index_buffer(data, count):
    if data[0] & 0xF0 != 0xE0:
        raise ValueError(f"codec de índices não suportado: header 0x{data[0]:02x}")
    version = data[0] & 0x0F
    fecmax = 13 if version >= 1 else 15
    edge = [(0, 0)] * 16
    vfifo = [0] * 16
    eo = vo = 0
    nxt = last = 0
    code = 1
    dpos = 1 + count // 3
    aux_table = data[len(data) - 16:]
    out = np.zeros(count, dtype=np.uint32)

    def push_v(x, cond=True):
        nonlocal vo
        if cond:
            vfifo[vo] = x
            vo = (vo + 1) & 15

    def push_e(x, y):
        nonlocal eo
        edge[eo] = (x, y)
        eo = (eo + 1) & 15

    for i in range(0, count, 3):
        codetri = data[code]
        code += 1
        if codetri < 0xF0:
            fe = codetri >> 4
            a, b = edge[(eo - 1 - fe) & 15]
            fec = codetri & 15
            if fec < fecmax:
                c = nxt if fec == 0 else vfifo[(vo - 1 - fec) & 15]
                fec0 = fec == 0
                nxt += fec0
                out[i:i + 3] = (a, b, c)
                push_v(c, fec0)
                push_e(c, b)
                push_e(a, c)
            else:
                if fec != 15:
                    c = last = (last + (fec - (fec ^ 3))) & 0xFFFFFFFF  # 13 -> -1, 14 -> +1
                else:
                    c, dpos = _decode_index(data, dpos, last)
                    last = c
                out[i:i + 3] = (a, b, c)
                push_v(c)
                push_e(c, b)
                push_e(a, c)
        elif codetri < 0xFE:
            aux = aux_table[codetri & 15]
            feb, fec = aux >> 4, aux & 15
            a = nxt
            nxt += 1
            b = nxt if feb == 0 else vfifo[(vo - feb) & 15]
            feb0 = feb == 0
            nxt += feb0
            c = nxt if fec == 0 else vfifo[(vo - fec) & 15]
            fec0 = fec == 0
            nxt += fec0
            out[i:i + 3] = (a, b, c)
            push_v(a)
            push_v(b, feb0)
            push_v(c, fec0)
            push_e(b, a)
            push_e(c, b)
            push_e(a, c)
        else:
            aux = data[dpos]
            dpos += 1
            fea = 0 if codetri == 0xFE else 15
            feb, fec = aux >> 4, aux & 15
            if aux == 0:
                nxt = 0
            a = b = c = 0
            if fea == 0:
                a = nxt
                nxt += 1
            if feb == 0:
                b = nxt
                nxt += 1
            else:
                b = vfifo[(vo - feb) & 15]
            if fec == 0:
                c = nxt
                nxt += 1
            else:
                c = vfifo[(vo - fec) & 15]
            if fea == 15:
                a, dpos = _decode_index(data, dpos, last)
                last = a
            if feb == 15:
                b, dpos = _decode_index(data, dpos, last)
                last = b
            if fec == 15:
                c, dpos = _decode_index(data, dpos, last)
                last = c
            out[i:i + 3] = (a, b, c)
            push_v(a)
            push_v(b, feb == 0 or feb == 15)
            push_v(c, fec == 0 or fec == 15)
            push_e(b, a)
            push_e(c, b)
            push_e(a, c)
    if dpos > len(data) - 16:
        raise ValueError("stream de índices corrompido")
    return out


# ---------------------------------------------------------------- glb

COMP = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def read_glb(path):
    b = open(path, "rb").read()
    if b[:4] != b"glTF":
        raise ValueError("não é .glb")
    jlen = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + jlen])
    bin_ = b""
    p = 20 + jlen
    if p < len(b):
        blen, btype = struct.unpack("<II", b[p:p + 8])
        if btype == 0x004E4942:
            bin_ = b[p + 8:p + 8 + blen]
    return j, bin_


def write_glb(path, j, bin_):
    js = json.dumps(j, separators=(",", ":")).encode()
    js += b" " * ((4 - len(js) % 4) % 4)
    bin_ += b"\0" * ((4 - len(bin_) % 4) % 4)
    total = 12 + 8 + len(js) + 8 + len(bin_)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        f.write(struct.pack("<II", len(bin_), 0x004E4942) + bin_)


def main(src, dst):
    j, bin_ = read_glb(src)
    views = j["bufferViews"]

    # 1. bytes descomprimidos de cada bufferView
    data = []
    for v in views:
        ext = v.get("extensions", {}).get("EXT_meshopt_compression")
        if ext:
            if ext["buffer"] != 0:
                raise ValueError("stream meshopt fora do buffer 0 (BIN)")
            raw = bin_[ext["byteOffset"]:ext["byteOffset"] + ext["byteLength"]]
            mode, count, stride = ext["mode"], ext["count"], ext["byteStride"]
            if mode == "ATTRIBUTES":
                out = decode_vertex_buffer(raw, count, stride)
                flt = ext.get("filter", "NONE")
                if flt == "EXPONENTIAL":
                    out = filter_exponential(out, stride)
                elif flt != "NONE":
                    raise ValueError(f"filtro meshopt não suportado: {flt}")
            elif mode == "TRIANGLES":
                idx = decode_index_buffer(raw, count)
                if int(idx.max()) >= 1 << (8 * stride):
                    raise ValueError("índice fora da faixa do tipo")
                out = idx.astype({2: np.uint16, 4: np.uint32}[stride]).tobytes()
            else:
                raise ValueError(f"modo meshopt não suportado: {mode}")
            if len(out) != v["byteLength"]:
                raise ValueError(f"tamanho decodificado {len(out)} != {v['byteLength']}")
        else:
            if v.get("buffer", 0) != 0:
                raise ValueError("bufferView sem meshopt fora do buffer 0")
            out = bin_[v.get("byteOffset", 0):v.get("byteOffset", 0) + v["byteLength"]]
        data.append(out)

    # 2. accessors quantizados (inteiros normalizados ou não-float em atributos) -> float32 em view própria
    attr_acc = set()
    for m in j["meshes"]:
        for prim in m["primitives"]:
            attr_acc.update(prim["attributes"].values())
            for t in prim.get("targets", []):
                attr_acc.update(t.values())
    new_views = [dict(v) for v in views]
    for ai in sorted(attr_acc):
        a = j["accessors"][ai]
        if a["componentType"] == 5126:
            continue
        v = views[a["bufferView"]]
        nc = NCOMP[a["type"]]
        dt = np.dtype(COMP[a["componentType"]])
        stride = v.get("byteStride", nc * dt.itemsize)
        buf = np.frombuffer(data[a["bufferView"]], dtype=np.uint8)
        start = a.get("byteOffset", 0)
        rows = np.stack([buf[start + r * stride:start + r * stride + nc * dt.itemsize] for r in range(a["count"])])
        vals = rows.copy().view(dt).reshape(a["count"], nc).astype(np.float32)
        if a.get("normalized"):
            vals = np.maximum(vals / float(np.iinfo(dt).max), -1.0) if np.issubdtype(dt, np.signedinteger) else vals / float(np.iinfo(dt).max)
        if "NORMAL" in [k for m in j["meshes"] for p in m["primitives"] for k, x in p["attributes"].items() if x == ai]:
            n = np.linalg.norm(vals, axis=1, keepdims=True)
            vals = vals / np.where(n > 0, n, 1)
        data.append(vals.astype(np.float32).tobytes())
        new_views.append({"buffer": 0, "byteLength": len(data[-1]), "target": 34962})
        a["bufferView"] = len(new_views) - 1
        a["byteOffset"] = 0
        a["componentType"] = 5126
        a.pop("normalized", None)
        a["min"] = vals.min(0).tolist()
        a["max"] = vals.max(0).tolist()

    # 3. remonta um buffer único sem extensões
    blob = bytearray()
    for v, d in zip(new_views, data):
        while len(blob) % 4:
            blob.append(0)
        v.pop("extensions", None)
        v["buffer"] = 0
        v["byteOffset"] = len(blob)
        v["byteLength"] = len(d)
        blob += d
    j["bufferViews"] = new_views
    j["buffers"] = [{"byteLength": len(blob)}]
    for key in ("extensionsUsed", "extensionsRequired"):
        if key in j:
            j[key] = [e for e in j[key] if e not in ("EXT_meshopt_compression", "KHR_mesh_quantization")]
            if not j[key]:
                del j[key]
    write_glb(dst, j, bytes(blob))
    print(json.dumps({"in": src, "out": dst, "bytes": len(blob)}))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
