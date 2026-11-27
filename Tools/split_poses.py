"""Separa um .glb com várias poses do mesmo personagem (ex.: export do Tripo com as 5 poses lado a lado)
em um .glb por pose, prontos para o Unity: escala comum, pivô entre os pés (Y=0), frente em +Z e malha
reduzida (padrão 10 mil triângulos) mantendo a textura.
A malha é soldada pela posição antes da redução, para não abrir frestas nas bordas das ilhas de UV.

    python split_poses.py HER_ALL_Krog.glb --out-dir <pasta> --prefix HER_01_Krog --height 1.8 --faces 10000

Como funciona
1. Agrupa a malha em "ilhas" 3D: ocupação em voxels (dilatada 1 voxel) + rótulos conectados. Cada pose
   vira uma ilha; peças soltas (lança, tocha) ficam com a ilha que tocam. Uma lança que passa POR CIMA de
   outra pose não se mistura, porque a separação é em 3D, não na vista de cima.
2. Nomeia as poses pela ordem da Bíblia §5.0 usando a regra --order (padrão: o arquivo do Tripo do Krog).
3. Escala única (a pose Idle fica com --height metros), pivô no centro da base de cada pose.
4. Redução por "quadric edge collapse with texture" do MeshLab (pymeshlab), que preserva as UVs.

Entrada: frente dos personagens em +Z (padrão do Tripo/glTF). Saída: <prefix>_<Pose>.glb.
"""
import argparse
import json
import os
import tempfile

import numpy as np
import trimesh
from PIL import Image
from scipy import ndimage

POSES = ["Idle", "Attack", "Skill", "Move", "Victory"]  # Bíblia §5.0 (1..5)


def islands(mesh, n, voxel):
    """Rótulo de pose (0..n-1) por face, pelas n maiores ilhas 3D."""
    pts = np.vstack([mesh.vertices, mesh.triangles_center])
    lo = pts.min(0)
    idx = np.floor((pts - lo) / voxel).astype(int)
    grid = np.zeros(idx.max(0) + 1, bool)
    grid[tuple(idx.T)] = True
    grid = ndimage.binary_dilation(grid, iterations=1)
    lab, count = ndimage.label(grid, structure=np.ones((3, 3, 3)))
    if count < n:
        raise SystemExit(f"Só {count} ilhas encontradas, esperava {n}: as poses se tocam (aumente o espaço entre elas).")
    sizes = np.bincount(lab.ravel())
    sizes[0] = 0
    keep = np.argsort(-sizes)[:n]
    remap = np.full(count + 1, -1)
    remap[keep] = np.arange(n)
    cidx = np.floor((mesh.triangles_center - lo) / voxel).astype(int)
    face_lab = remap[lab[tuple(cidx.T)]]
    # peças inteiras (componentes conectados da malha) ficam numa ilha só: voto da maioria
    comp = trimesh.graph.connected_component_labels(mesh.face_adjacency, node_count=len(mesh.faces))
    for c in np.unique(comp):
        f = comp == c
        votes = face_lab[f]
        votes = votes[votes >= 0]
        face_lab[f] = np.bincount(votes).argmax() if len(votes) else -1
    return face_lab


def pose_order(centers, rule):
    """Índices das ilhas na ordem Idle, Attack, Skill, Move, Victory."""
    if rule == "tripo-krog":
        # Fileira de trás (Z menor): da esquerda para a direita = Idle, Attack, Skill.
        # Fileira da frente (Z maior): da esquerda para a direita = Move, Victory.
        back = sorted([i for i in range(len(centers)) if centers[i][2] < 0], key=lambda i: centers[i][0])
        front = sorted([i for i in range(len(centers)) if centers[i][2] >= 0], key=lambda i: centers[i][0])
        if len(back) != 3 or len(front) != 2:
            raise SystemExit("Layout diferente do esperado (3 atrás, 2 na frente); use --order x")
        return back + front
    return sorted(range(len(centers)), key=lambda i: centers[i][0])  # "x": da esquerda para a direita


def smooth_normals(mesh):
    """Normais suaves por posição. Os vértices vêm duplicados nas bordas das ilhas de UV (milhares no Tripo):
    calculadas por vértice, cada ilha teria a própria borda de sombreamento e o modelo pareceria facetado.
    Aqui as faces que se tocam na mesma posição somam as normais (ponderadas pela área)."""
    key = np.round(mesh.vertices / 1e-6).astype(np.int64)
    _, weld = np.unique(key, axis=0, return_inverse=True)
    weld = weld.ravel()
    acc = np.zeros((weld.max() + 1, 3))
    np.add.at(acc, weld[mesh.faces].ravel(), np.repeat(mesh.face_normals * mesh.area_faces[:, None], 3, axis=0))
    n = acc[weld]
    return n / np.linalg.norm(n, axis=1, keepdims=True).clip(1e-12)


def decimate(mesh, faces):
    import pymeshlab

    with tempfile.TemporaryDirectory() as tmp:
        src = os.path.join(tmp, "in.obj")
        mesh.export(src)
        ms = pymeshlab.MeshSet()
        ms.load_new_mesh(src)
        if not ms.current_mesh().has_wedge_tex_coord():
            ms.compute_texcoord_transfer_vertex_to_wedge()
        # As UVs ficam nos cantos dos triângulos (wedge); agora os vértices duplicados nas bordas das ilhas de
        # UV podem ser soldados pela posição. Sem isso cada borda de ilha vira uma borda solta, a redução as
        # afasta e abrem frestas (o fundo branco aparece no Unity). Soldada, a pose do Tripo é estanque.
        ms.meshing_merge_close_vertices(threshold=pymeshlab.PercentageValue(0.0001))
        # Em passes: cada um para quando a qualidade cai; repetir chega ao alvo.
        for _ in range(6):
            if ms.current_mesh().face_number() <= faces * 1.05:
                break
            ms.meshing_decimation_quadric_edge_collapse_with_texture(
                targetfacenum=faces, qualitythr=0.3, extratcoordw=1.0,
                preserveboundary=False, boundaryweight=1.0, optimalplacement=True, preservenormal=True)
        dst = os.path.join(tmp, "out.obj")
        ms.save_current_mesh(dst, save_wedge_texcoord=True, save_vertex_normal=False)
        out = trimesh.load(dst, force="mesh", process=False)
    return out


p = argparse.ArgumentParser()
p.add_argument("glb")
p.add_argument("--out-dir", required=True)
p.add_argument("--prefix", required=True)
p.add_argument("--height", type=float, default=1.8, help="altura da pose Idle em metros")
p.add_argument("--faces", type=int, default=10000,
               help="triângulos por pose; 2.500 (Bíblia §7.1) destrói o pelo dos modelos do Tripo, 10 mil fica fiel")
p.add_argument("--texture", type=int, default=2048, help="lado máximo da textura exportada")
p.add_argument("--order", choices=["tripo-krog", "x"], default="tripo-krog")
p.add_argument("--voxel", type=float, default=0.006, help="tamanho do voxel, nas unidades do arquivo")
args = p.parse_args()

mesh = trimesh.load(args.glb, force="mesh")
labels = islands(mesh, len(POSES), args.voxel)
parts = [mesh.submesh([np.nonzero(labels == k)[0]], append=True) for k in range(len(POSES))]
order = pose_order([q.bounds.mean(0) for q in parts], args.order)
parts = [parts[i] for i in order]

scale = args.height / (parts[0].bounds[1][1] - parts[0].bounds[0][1])
tex = mesh.visual.material.baseColorTexture if hasattr(mesh.visual.material, "baseColorTexture") else mesh.visual.material.image
tex = tex.convert("RGB")
if max(tex.size) > args.texture:
    tex = tex.resize((args.texture, args.texture * tex.height // tex.width), Image.LANCZOS)

os.makedirs(args.out_dir, exist_ok=True)
report = []
for name, part in zip(POSES, parts):
    v = part.vertices
    lo_y = v[:, 1].min()
    h = v[:, 1].max() - lo_y
    feet = v[v[:, 1] < lo_y + 0.04 * h]
    pivot = np.array([feet[:, 0].mean(), lo_y, feet[:, 2].mean()])
    m = np.eye(4)
    m[:3, :3] *= scale
    m[:3, 3] = -scale * pivot
    part.apply_transform(m)
    part.visual = trimesh.visual.TextureVisuals(uv=part.visual.uv, image=tex)
    low = decimate(part, args.faces)
    low.visual = trimesh.visual.TextureVisuals(uv=low.visual.uv, image=tex)
    # Sem NORMAL no .glb o glTF manda calcular normais planas: cada triângulo vira uma faceta e as viradas
    # para a luz ficam quase brancas no Unity.
    low.vertex_normals = smooth_normals(low)
    path = os.path.join(args.out_dir, f"{args.prefix}_{name}.glb")
    low.export(path, include_normals=True)
    lo, hi = low.bounds
    report.append({"pose": name, "file": path, "triangles": int(len(low.faces)),
                   "height_m": round(float(hi[1] - lo[1]), 3), "min_y": round(float(lo[1]), 4)})
print(json.dumps(report, indent=1, ensure_ascii=False))
