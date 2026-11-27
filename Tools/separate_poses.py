"""Separação sem perdas de um .glb com N poses (ex.: export do Tripo3D com as 5 poses de um herói)
em N arquivos .glb, um por pose.

    python Tools/separate_poses.py ENTRADA.glb --out-dir Assets/_ChronoDefense/Art/Heroes/HER_01_Krog \
        --prefix HER_01_Krog --height 1.8 [--ref gabarito.glb] [--report Tools/output/separate_quality_report.json]

Princípios (sem cortes, sem reamostragem):
  * Nada é fatiado: a malha é dividida em PEÇAS SOLTAS (componentes conectados) e cada peça vai inteira
    para uma pose. É o equivalente a Blender "Separate ▸ By Loose Parts", feito direto nos buffers glTF.
  * As peças são conectadas pela POSIÇÃO dos vértices (soldagem só para a análise): no glTF os vértices
    vêm duplicados nas bordas das ilhas de UV, e por índice cada ilha de UV pareceria uma peça.
  * Peças soltas são agrupadas por proximidade 3D (voxels vizinhos), não por centroide: uma lança que
    passa por cima de outra pose não é puxada para ela.
  * Todos os atributos (POSITION, NORMAL, TEXCOORD_n, COLOR_n, TANGENT…) são copiados bit a bit; só
    POSITION recebe a escala uniforme + translação do pivô (NORMAL/TANGENT não mudam com escala uniforme).
  * Materiais, texturas (base, metal/rugosidade, normal), samplers e extensões são copiados sem alteração.

Pivô e escala:
  * --pivot bbox (padrão): centro X/Z da caixa da pose, Y mínimo = 0. --pivot feet: centro dos pés.
    Para poses que se alternam no mesmo pedestal use feet: com bbox, uma lança esticada desloca a caixa e
    o personagem "pula" ao trocar de pose (no Krog do Tripo, o Ataque ficaria 0,7 m fora do lugar).
  * Escala ÚNICA para todas as poses: a pose 1 (Idle) fica com --height metros; as outras mantêm a
    proporção (uma pose agachada fica mais baixa, como deve).

Requer só numpy (a validação visual contra --ref também). Roda no venv do TripoSR/Hunyuan ou no Python do
Blender. Não reduz polígonos: para o orçamento mobile, use Tools/split_poses.py depois.
"""
import argparse
import json
import os
import struct
import sys

import numpy as np

POSES = ["Idle", "Attack", "Skill", "Move", "Victory"]  # Bíblia §5.0; casa com HeroPose no Unity
COMPONENT_DTYPE = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
TYPE_SIZE = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


# --------------------------------------------------------------------------------------------- GLB I/O
def read_glb(path):
    data = open(path, "rb").read()
    magic, version, _ = struct.unpack_from("<III", data, 0)
    if magic != 0x46546C67 or version != 2:
        raise SystemExit(f"{path}: não é um GLB 2.0")
    off, gltf, binary = 12, None, b""
    while off < len(data):
        length, ctype = struct.unpack_from("<II", data, off)
        chunk = data[off + 8: off + 8 + length]
        if ctype == 0x4E4F534A:
            gltf = json.loads(chunk.decode("utf-8"))
        elif ctype == 0x004E4942:
            binary = chunk
        off += 8 + length
    return gltf, binary


def read_accessor(gltf, binary, index):
    acc = gltf["accessors"][index]
    if "sparse" in acc:
        raise SystemExit("accessor esparso não suportado")
    view = gltf["bufferViews"][acc["bufferView"]]
    dtype = np.dtype(COMPONENT_DTYPE[acc["componentType"]])
    ncomp = TYPE_SIZE[acc["type"]]
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = view.get("byteStride", 0) or dtype.itemsize * ncomp
    count = acc["count"]
    if stride == dtype.itemsize * ncomp:
        arr = np.frombuffer(binary, dtype, count * ncomp, start)
    else:  # dados intercalados
        raw = np.frombuffer(binary, np.uint8, (count - 1) * stride + dtype.itemsize * ncomp, start)
        rows = np.lib.stride_tricks.as_strided(raw, (count, dtype.itemsize * ncomp), (stride, 1))
        arr = np.ascontiguousarray(rows).view(dtype)
    return arr.reshape(count, ncomp) if ncomp > 1 else arr.copy(), acc


class GlbWriter:
    """Monta um GLB novo: mesmo JSON de materiais/texturas, buffers novos para a malha."""

    def __init__(self):
        self.blob = bytearray()
        self.views, self.accessors = [], []

    def _view(self, payload, target=None):
        while len(self.blob) % 4:
            self.blob.append(0)
        v = {"buffer": 0, "byteOffset": len(self.blob), "byteLength": len(payload)}
        if target:
            v["target"] = target
        self.blob += payload
        self.views.append(v)
        return len(self.views) - 1

    def accessor(self, arr, template, target, minmax=False):
        arr = np.ascontiguousarray(arr)
        acc = {k: v for k, v in template.items() if k not in ("bufferView", "byteOffset", "count", "min", "max", "sparse")}
        acc["bufferView"] = self._view(arr.tobytes(), target)
        acc["count"] = int(arr.shape[0])
        if minmax:
            flat = arr.reshape(arr.shape[0], -1)
            acc["min"] = [float(x) for x in flat.min(0)]
            acc["max"] = [float(x) for x in flat.max(0)]
        self.accessors.append(acc)
        return len(self.accessors) - 1

    def image(self, payload):
        return self._view(payload)

    def save(self, path, gltf):
        gltf["bufferViews"] = self.views
        gltf["accessors"] = self.accessors
        gltf["buffers"] = [{"byteLength": len(self.blob)}]
        js = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
        js += b" " * (-len(js) % 4)
        bn = bytes(self.blob) + b"\0" * (-len(self.blob) % 4)
        total = 12 + 8 + len(js) + 8 + len(bn)
        with open(path, "wb") as f:
            f.write(struct.pack("<III", 0x46546C67, 2, total))
            f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
            f.write(struct.pack("<II", len(bn), 0x004E4942) + bn)


# ------------------------------------------------------------------------------------------ geometria
def find_roots(parent):
    while True:
        gp = parent[parent]
        if np.array_equal(gp, parent):
            return parent
        parent = gp


def union_find(n, a, b):
    """Componentes conectados (rótulo = menor índice do grupo), vetorizado: hook + compressão."""
    parent = np.arange(n)
    while True:
        pa, pb = parent[a], parent[b]
        diff = pa != pb
        if not diff.any():
            return find_roots(parent)
        lo, hi = np.minimum(pa[diff], pb[diff]), np.maximum(pa[diff], pb[diff])
        np.minimum.at(parent, hi, lo)
        parent = find_roots(parent)


def weld(positions, extent):
    key = np.round(positions / (extent * 1e-6)).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    return inv.ravel()


def loose_parts(tris, weld_id):
    """Rótulo de peça solta por triângulo (vértices soldados por posição)."""
    w = weld_id[tris]
    n = int(weld_id.max()) + 1
    root = union_find(n, np.concatenate([w[:, 0], w[:, 1]]), np.concatenate([w[:, 1], w[:, 2]]))
    _, part = np.unique(root[w[:, 0]], return_inverse=True)
    return part.ravel()


def group_parts(positions, tris, part, n_groups, gap):
    """Agrupa peças soltas que ficam a menos de `gap` uma da outra (voxels vizinhos em 3D)."""
    n_parts = int(part.max()) + 1
    vpart = np.empty(len(positions), np.int64)
    vpart[tris.ravel()] = np.repeat(part, 3)
    used = np.unique(tris.ravel())
    vox = np.floor((positions[used] - positions[used].min(0)) / gap).astype(np.int64)
    vp = vpart[used]
    base = np.unique(np.column_stack([vox, vp]), axis=0)          # (voxel, peça) únicos
    offs = np.array([(i, j, k) for i in (-1, 0, 1) for j in (-1, 0, 1) for k in (-1, 0, 1)])
    a_list, b_list = [], []
    dims = vox.max(0) + 3
    for o in offs:                                                  # peças em voxels vizinhos se unem
        cells = base[:, :3] + o + 1
        key = (cells[:, 0] * dims[1] + cells[:, 1]) * dims[2] + cells[:, 2]
        own = ((base[:, 0] + 1) * dims[1] + base[:, 1] + 1) * dims[2] + base[:, 2] + 1
        allk = np.concatenate([own, key])
        allp = np.concatenate([base[:, 3], base[:, 3]])
        order = np.lexsort((allp, allk))
        k_s, p_s = allk[order], allp[order]
        same = k_s[1:] == k_s[:-1]
        a_list.append(p_s[1:][same])
        b_list.append(p_s[:-1][same])
    group = union_find(n_parts, np.concatenate(a_list), np.concatenate(b_list))
    _, group = np.unique(group, return_inverse=True)
    group = group.ravel()
    faces_per_group = np.bincount(group[part])
    big = np.argsort(-faces_per_group)[:n_groups]
    if len(big) < n_groups or faces_per_group[big[-1]] < 0.02 * len(tris):
        raise SystemExit(f"Encontrei menos de {n_groups} poses separadas (as poses se tocam?). "
                         f"Tamanhos dos grupos: {sorted(faces_per_group.tolist(), reverse=True)[:8]}")
    # grupos pequenos que sobraram (lascas soltas longe de tudo) vão para a pose de centro mais próximo
    tri_center = positions[tris].mean(1)
    centers = np.array([tri_center[group[part] == g].mean(0) for g in big])
    final = np.full(int(group.max()) + 1, -1)
    final[big] = np.arange(n_groups)
    orphans = 0
    for g in np.nonzero(final < 0)[0]:
        sel = group[part] == g
        if not sel.any():
            continue
        c = tri_center[sel].mean(0)
        final[g] = int(np.argmin(((centers - c) ** 2).sum(1)))
        orphans += 1
    return final[group[part]], orphans


def pose_order(centers, rule):
    if rule == "tripo-krog":
        back = sorted([i for i in range(len(centers)) if centers[i][2] < 0], key=lambda i: centers[i][0])
        front = sorted([i for i in range(len(centers)) if centers[i][2] >= 0], key=lambda i: centers[i][0])
        if len(back) == 3 and len(front) == 2:
            return back + front, "tripo-krog (3 atrás + 2 na frente, da esquerda para a direita)"
    order = sorted(range(len(centers)), key=lambda i: centers[i][0])
    return order, "x (da esquerda para a direita)"


def boundary_edges(tris, weld_id):
    w = np.sort(weld_id[tris][:, [0, 1, 1, 2, 2, 0]].reshape(-1, 2), axis=1)
    _, counts = np.unique(w, axis=0, return_counts=True)
    return int((counts == 1).sum())


def silhouette(positions, view, size=(64, 96)):
    """Máscara frontal (view='front': X,Y) por splat de vértices, normalizada à caixa."""
    p = positions[:, [0, 1]] if view == "front" else positions[:, [2, 1]]
    p = (p - p.min(0)) / np.maximum(p.max(0) - p.min(0), 1e-9)
    ix = np.clip((p[:, 0] * (size[0] - 1)).astype(int), 0, size[0] - 1)
    iy = np.clip(((1 - p[:, 1]) * (size[1] - 1)).astype(int), 0, size[1] - 1)
    m = np.zeros((size[1], size[0]), bool)
    m[iy, ix] = True
    return m


def iou(a, b):
    return float((a & b).sum()) / max(float((a | b).sum()), 1.0)


# ----------------------------------------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("glb")
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--prefix", required=True)
    ap.add_argument("--poses", type=int, default=5)
    ap.add_argument("--height", type=float, default=1.8, help="altura da pose 1 (Idle) em metros; 0 = não escalar")
    ap.add_argument("--pivot", choices=["bbox", "feet"], default="bbox")
    ap.add_argument("--order", choices=["tripo-krog", "x"], default="tripo-krog")
    ap.add_argument("--gap", type=float, default=0.006, help="distância de agrupamento, fração do tamanho do modelo")
    ap.add_argument("--ref", help="gabarito .glb de 1 pose (Tripo) para o comparativo de fidelidade")
    ap.add_argument("--report", help="caminho do relatório JSON")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]   # Blender: após "--"
    args = ap.parse_args(argv)

    gltf, binary = read_glb(args.glb)
    prims = [(mi, pi) for mi, m in enumerate(gltf["meshes"]) for pi, _ in enumerate(m["primitives"])]
    if len(prims) != 1 or len(gltf.get("nodes", [])) != 1:
        raise SystemExit("Suportado: 1 nó com 1 malha de 1 primitiva (formato do Tripo3D).")
    node = gltf["nodes"][0]
    if any(k in node for k in ("matrix", "rotation", "scale", "translation")):
        raise SystemExit("O nó tem transformação; aplique-a antes (o export do Tripo não tem).")
    prim = gltf["meshes"][0]["primitives"][0]
    if prim.get("mode", 4) != 4 or "indices" not in prim or prim.get("targets"):
        raise SystemExit("Suportado: triângulos indexados sem morph targets.")

    attrs = {name: read_accessor(gltf, binary, idx) for name, idx in prim["attributes"].items()}
    tris_flat, idx_acc = read_accessor(gltf, binary, prim["indices"])
    tris = tris_flat.reshape(-1, 3).astype(np.int64)
    pos = attrs["POSITION"][0].astype(np.float64)
    extent = float((pos.max(0) - pos.min(0)).max())

    weld_id = weld(pos, extent)
    part = loose_parts(tris, weld_id)
    pose_of_tri, orphans = group_parts(pos, tris, part, args.poses, args.gap * extent)
    centers = [pos[tris[pose_of_tri == k]].reshape(-1, 3).mean(0) for k in range(args.poses)]
    order, order_rule = pose_order(centers, args.order if args.poses == 5 else "x")
    names = POSES if args.poses == 5 else [f"Pose{i + 1}" for i in range(args.poses)]

    # escala única a partir da pose 1
    first = np.unique(tris[pose_of_tri == order[0]])
    h0 = pos[first, 1].max() - pos[first, 1].min()
    scale = args.height / h0 if args.height > 0 else 1.0

    images = [binary[gltf["bufferViews"][im["bufferView"]].get("byteOffset", 0):
                     gltf["bufferViews"][im["bufferView"]].get("byteOffset", 0) + gltf["bufferViews"][im["bufferView"]]["byteLength"]]
              for im in gltf.get("images", [])]
    os.makedirs(args.out_dir, exist_ok=True)

    poses_report, outputs = [], []
    total_tris = 0
    for slot, k in enumerate(order):
        sel_tris = tris[pose_of_tri == k]
        used, local = np.unique(sel_tris, return_inverse=True)
        local = local.reshape(-1, 3)
        p = pos[used]
        lo, hi = p.min(0), p.max(0)
        if args.pivot == "feet":
            feet = p[p[:, 1] < lo[1] + 0.04 * (hi[1] - lo[1])]
            pivot = np.array([feet[:, 0].mean(), lo[1], feet[:, 2].mean()])
        else:
            pivot = np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2])

        w = GlbWriter()
        out = json.loads(json.dumps({k2: v for k2, v in gltf.items() if k2 not in ("bufferViews", "accessors", "buffers")}))
        new_attrs = {}
        for name, (arr, acc) in attrs.items():
            sub = arr[used]
            if name == "POSITION":
                sub = ((sub.astype(np.float64) - pivot) * scale).astype(np.float32)
            new_attrs[name] = w.accessor(sub, acc, 34962, minmax=(name == "POSITION"))
        idx_dtype = np.uint16 if len(used) < 65535 else np.uint32
        idx_template = dict(idx_acc, componentType=5123 if idx_dtype == np.uint16 else 5125)
        new_idx = w.accessor(local.astype(idx_dtype).ravel(), idx_template, 34963)
        for i, im in enumerate(out.get("images", [])):
            im["bufferView"] = w.image(images[i])
        new_prim = dict(prim, attributes=new_attrs, indices=new_idx)
        out["meshes"] = [dict(gltf["meshes"][0], primitives=[new_prim])]
        out["nodes"] = [dict(node, name=f"{args.prefix}_{names[slot]}")]
        out.setdefault("asset", {})["generator"] = "ChronoDefense separate_poses.py (de " + gltf.get("asset", {}).get("generator", "?") + ")"
        path = os.path.join(args.out_dir, f"{args.prefix}_{names[slot]}.glb")
        w.save(path, out)
        outputs.append(path)

        # ---- fidelidade desta pose: relê o arquivo escrito e compara com a fonte
        g2, b2 = read_glb(path)
        p2 = g2["meshes"][0]["primitives"][0]
        checks = {}
        for name in attrs:
            back, _ = read_accessor(g2, b2, p2["attributes"][name])
            src = attrs[name][0][used]
            if name == "POSITION":
                back = back.astype(np.float64) / scale + pivot
                checks["POSITION_max_err_m"] = float(np.abs(back - src).max())
            else:
                checks[f"{name}_bit_exact"] = bool(np.array_equal(back, src))
        idx2, _ = read_accessor(g2, b2, p2["indices"])
        checks["triangles_identical"] = bool(np.array_equal(used[idx2.reshape(-1, 3)], sel_tris))
        checks["textures_byte_identical"] = all(
            b2[g2["bufferViews"][im["bufferView"]]["byteOffset"]:][:g2["bufferViews"][im["bufferView"]]["byteLength"]] == images[i]
            for i, im in enumerate(g2.get("images", [])))
        checks["materials_identical"] = g2.get("materials") == gltf.get("materials")
        b_in_pose = boundary_edges(sel_tris, weld_id)
        pos_out = read_accessor(g2, b2, p2["attributes"]["POSITION"])[0]
        total_tris += len(sel_tris)
        poses_report.append({
            "pose": names[slot], "file": path, "vertices": int(len(used)), "triangles": int(len(sel_tris)),
            "loose_parts": int(len(np.unique(part[pose_of_tri == k]))),
            "height_m": round(float(pos_out[:, 1].max() - pos_out[:, 1].min()), 4),
            "min_y": round(float(pos_out[:, 1].min()), 6),
            "center_xz": [round(float((pos_out[:, 0].max() + pos_out[:, 0].min()) / 2), 6),
                          round(float((pos_out[:, 2].max() + pos_out[:, 2].min()) / 2), 6)],
            "open_boundary_edges": b_in_pose, "fidelity": checks,
        })

    # nenhuma aresta compartilhada entre poses diferentes = nenhum buraco criado pela separação
    wt = weld_id[tris]
    edges = np.sort(wt[:, [0, 1, 1, 2, 2, 0]].reshape(-1, 2), axis=1)
    edge_pose = np.repeat(pose_of_tri, 3)
    _, inv = np.unique(edges, axis=0, return_inverse=True)
    inv = inv.ravel()
    pmin = np.full(inv.max() + 1, 1 << 30)
    pmax = np.full(inv.max() + 1, -1)
    np.minimum.at(pmin, inv, edge_pose)
    np.maximum.at(pmax, inv, edge_pose)
    cut_edges = int((pmin != pmax).sum())

    report = {
        "input": args.glb,
        "source": {"vertices": int(len(pos)), "triangles": int(len(tris)), "loose_parts": int(part.max() + 1),
                   "open_boundary_edges": boundary_edges(tris, weld_id),
                   "images": [im.get("name") for im in gltf.get("images", [])]},
        "method": "peças soltas (componentes conectados por posição) + agrupamento por proximidade 3D; sem cortes",
        "pose_order_rule": order_rule, "pivot": args.pivot, "uniform_scale": scale,
        "orphan_groups_assigned": int(orphans),
        "separation": {"triangles_assigned": int(total_tris), "triangles_lost": int(len(tris) - total_tris),
                       "edges_cut_between_poses": cut_edges,
                       "holes_created": cut_edges > 0},
        "poses": poses_report,
    }

    if args.ref:
        rg, rb = read_glb(args.ref)
        rp = rg["meshes"][0]["primitives"][0]
        rpos = read_accessor(rg, rb, rp["attributes"]["POSITION"])[0].astype(np.float64)
        rtris = read_accessor(rg, rb, rp["indices"])[0].reshape(-1, 3).astype(np.int64)
        rweld = weld(rpos, float((rpos.max(0) - rpos.min(0)).max()))
        rlo, rhi = rpos.min(0), rpos.max(0)
        sims = []
        for pr in poses_report:
            g2, b2 = read_glb(pr["file"])
            pp = read_accessor(g2, b2, g2["meshes"][0]["primitives"][0]["attributes"]["POSITION"])[0]
            sims.append(round(iou(silhouette(pp, "front"), silhouette(rpos, "front")), 3))
        best = int(np.argmax(sims))
        bp = poses_report[best]
        report["reference_comparison"] = {
            "reference": args.ref,
            "reference_vertices": int(len(rpos)), "reference_triangles": int(len(rtris)),
            "reference_loose_parts": int(loose_parts(rtris, rweld).max() + 1),
            "reference_open_boundary_edges": boundary_edges(rtris, rweld),
            "reference_watertight": boundary_edges(rtris, rweld) == 0,
            "reference_pivot": {"min_y": float(rlo[1]), "center_xz": [float((rlo[0] + rhi[0]) / 2), float((rlo[2] + rhi[2]) / 2)],
                                "height": float(rhi[1] - rlo[1])},
            "same_attributes": sorted(rp["attributes"]) == sorted(prim["attributes"]),
            "same_material_structure": [sorted(m.keys()) for m in rg.get("materials", [])] == [sorted(m.keys()) for m in gltf.get("materials", [])],
            "front_silhouette_iou_per_pose": dict(zip([p["pose"] for p in poses_report], sims)),
            "most_similar_pose": bp["pose"],
            "triangles_ratio_vs_reference": round(bp["triangles"] / len(rtris), 3),
            "note": ("O arquivo de N poses do Tripo usa o mesmo orçamento total de polígonos que um arquivo de 1 pose: "
                     "cada pose extraída tem ~1/N do detalhe do gabarito. A separação preserva 100% do que existe "
                     "no arquivo de entrada, mas não recupera detalhe que ele não tem."),
        }

    if args.report:
        os.makedirs(os.path.dirname(os.path.abspath(args.report)), exist_ok=True)
        with open(args.report, "w", encoding="utf-8") as f:
            json.dump(report, f, indent=1, ensure_ascii=False)
    print(json.dumps({"poses": [{k: p[k] for k in ("pose", "triangles", "height_m", "min_y")} for p in poses_report],
                      "separation": report["separation"]}, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
