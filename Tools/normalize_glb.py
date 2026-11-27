"""Normaliza um .glb para o Unity: uma malha só, pivô no centro da base (Y=0), altura exata em metros
e frente do modelo em +Z (convenção glTF; no Unity, via glTFast, também +Z).

    Tools\\TripoSR\\venv\\Scripts\\python.exe Tools\\normalize_glb.py modelo.glb --image ref.png --height 1.1 --front image
    ... --project      # malha sem cor (Hunyuan só-forma): projeta a imagem como textura
    Tools\\TripoSR\\venv\\Scripts\\python.exe Tools\\normalize_glb.py cenario.glb --width 2.8     # sem imagem (Tripo)

Sem --image (modelos do Tripo gerados por texto, já com textura e frente em +Z) o alinhamento por silhueta
é pulado; --yaw corrige a frente se precisar. --width escala pela maior medida horizontal (pedestal, covil,
props), em vez da altura.

Orientação: o Hunyuan e o TripoSR não devolvem o modelo sempre no mesmo ângulo (e um personagem em pose de
três-quartos sai girado). Por isso o script gira o modelo em volta de Y e escolhe o ângulo em que a silhueta
melhor coincide com a da imagem (IoU). Frente e costas têm silhuetas espelhadas, então personagens assimétricos
(lança de um lado, por exemplo) são distinguidos; o IoU é impresso no resultado para conferência.

--front diz para onde o personagem olha NA IMAGEM:
    image = de frente para quem vê (heróis)      left/right = de perfil, cabeça à esquerda/direita (creeps)

Idempotente: rodar de novo no mesmo .glb dá o mesmo resultado (útil para corrigir sem gerar de novo).
As transformações vão para os vértices (não para um nó), porque o UnitRenderer desenha a malha crua com
RenderMeshInstanced. Não reduz polígonos: a redução ao orçamento da Bíblia §7.1 é feita no Blender.
"""
import argparse
import json

import numpy as np
import trimesh
from PIL import Image, ImageDraw

MASK = (64, 96)  # resolução da comparação de silhuetas (largura, altura)
FRONT_FACING = 0.25  # face "de frente" se a normal aponta para a câmera da imagem (componente Z > 0,25)


def yaw(deg):
    return trimesh.transformations.rotation_matrix(np.radians(deg), [0, 1, 0])


def crop_to_alpha(img):
    bbox = img.getchannel("A").point(lambda a: 255 if a > 16 else 0).getbbox()
    return img.crop(bbox) if bbox else img


def image_mask(img):
    return np.asarray(img.getchannel("A").resize(MASK)) > 16


def silhouette(mesh, cam_deg):
    """Silhueta ortográfica vista de uma câmera na direção (sin, 0, cos) olhando para o centro."""
    t = np.radians(cam_deg)
    c = np.array([np.sin(t), 0.0, np.cos(t)])
    right = np.cross([0.0, 1.0, 0.0], c)
    u, w = mesh.vertices @ right, mesh.vertices[:, 1]
    W, H = 256, 384
    px = (u - u.min()) / max(u.max() - u.min(), 1e-9) * (W - 1)
    py = (w.max() - w) / max(w.max() - w.min(), 1e-9) * (H - 1)
    canvas = Image.new("L", (W, H), 0)
    draw = ImageDraw.Draw(canvas)
    for f in mesh.faces:
        draw.polygon([(px[f[0]], py[f[0]]), (px[f[1]], py[f[1]]), (px[f[2]], py[f[2]])], fill=255)
    return np.asarray(canvas.resize(MASK)) > 127


def iou(a, b):
    return float((a & b).sum()) / max(float((a | b).sum()), 1.0)


def best_camera(mesh, ref):
    coarse = max((iou(silhouette(mesh, d), ref), d) for d in range(0, 360, 10))
    return max((iou(silhouette(mesh, d), ref), d) for d in range(coarse[1] - 8, coarse[1] + 9, 2))


p = argparse.ArgumentParser()
p.add_argument("glb")
p.add_argument("--image", help="PNG RGBA de referência (o mesmo usado na geração); omita para modelos do Tripo")
size = p.add_mutually_exclusive_group(required=True)
size.add_argument("--height", type=float, help="altura final em metros (Y)")
size.add_argument("--width", type=float, help="maior medida horizontal (X ou Z) em metros")
p.add_argument("--yaw", type=float, default=0.0, help="rotação extra em Y (graus) aplicada no fim, para corrigir a frente")
p.add_argument("--front", choices=["image", "left", "right"], default="image")
p.add_argument("--project", action="store_true", help="projeta a imagem de frente como textura UV")
p.add_argument("--max-texture", type=int, default=2048, help="lado máximo das texturas em px (reduz se maior; padrão 2048)")
args = p.parse_args()
if args.project and not args.image:
    p.error("--project precisa de --image")

loaded = trimesh.load(args.glb)
if isinstance(loaded, trimesh.Scene):
    parts = loaded.dump()  # malhas com as transformações dos nós já aplicadas
    mesh = parts[0] if len(parts) == 1 else trimesh.util.concatenate(parts)
else:
    mesh = loaded

# 1. alinha à imagem: câmera da imagem em +Z (direita da imagem = +X)
score, cam = None, 0
if args.image:
    ref_img = crop_to_alpha(Image.open(args.image).convert("RGBA"))
    score, cam = best_camera(mesh, image_mask(ref_img))
    mesh.apply_transform(yaw(-cam))

# 2. textura projetada de frente (u = direita da imagem, v = cima).
#    A projeção atravessa o modelo: sem cuidado, as costas recebem a frente espelhada (rosto atrás da cabeça)
#    e as laterais viram faixas esticadas. Por isso a textura tem duas metades: à esquerda a imagem nítida,
#    usada só nas faces viradas para a frente; à direita a mesma imagem bem desfocada (só as cores de cada
#    região), usada nas costas e laterais. Os vértices da fronteira são duplicados, mas mantêm as normais
#    originais, então o sombreamento continua suave.
if args.project:
    from PIL import ImageFilter

    lo, hi = mesh.bounds
    front = np.repeat(mesh.face_normals[:, 2] > FRONT_FACING, 3)   # por canto de face
    # um vértice por (vértice original, lado): só os da fronteira frente/costas ficam duplicados
    key = mesh.faces.ravel() * 2 + front
    uniq, inverse = np.unique(key, return_inverse=True)
    src, side_front = uniq // 2, (uniq % 2).astype(bool)
    v = mesh.vertices[src]
    u = (v[:, 0] - lo[0]) / (hi[0] - lo[0])
    w = (v[:, 1] - lo[1]) / (hi[1] - lo[1])
    u = np.where(side_front, u * 0.5, 0.5 + u * 0.5)
    # fundo transparente vira a cor média do personagem (evita bordas brancas na silhueta)
    rgb = np.asarray(ref_img).astype(np.float32)
    alpha = rgb[:, :, 3:4] / 255.0
    mean = (rgb[:, :, :3] * alpha).sum((0, 1)) / max(alpha.sum(), 1.0)
    rgb = rgb[:, :, :3] * alpha + mean * (1 - alpha)
    sharp = Image.fromarray(rgb.astype(np.uint8), "RGB")
    blurred = sharp.filter(ImageFilter.GaussianBlur(radius=max(sharp.size) * 0.04))
    tex = Image.new("RGB", (sharp.width * 2, sharp.height))
    tex.paste(sharp, (0, 0))
    tex.paste(blurred, (sharp.width, 0))
    mesh = trimesh.Trimesh(vertices=v, faces=inverse.reshape(-1, 3), vertex_normals=mesh.vertex_normals[src],
                           visual=trimesh.visual.TextureVisuals(uv=np.column_stack([u, w]), image=tex),
                           process=False)

# 3. frente do personagem para +Z (perfil: a cabeça está em -X se olha para a esquerda da imagem)
mesh.apply_transform(yaw({"image": 0, "left": 90, "right": -90}[args.front] + args.yaw))

# 4. pivô no centro da base e altura em metros
lo, hi = mesh.bounds
s = args.height / (hi[1] - lo[1]) if args.height else args.width / max(hi[0] - lo[0], hi[2] - lo[2])
m = np.eye(4)
m[:3, :3] *= s
m[:3, 3] = -s * np.array([(lo[0] + hi[0]) / 2, lo[1], (lo[2] + hi[2]) / 2])
mesh.apply_transform(m)

# 5. texturas no máximo --max-texture px (o Tripo "detailed" pode sair em 4K; 2048 basta no celular)
tex_px = []
mat = getattr(mesh.visual, "material", None)
for attr in ("baseColorTexture", "metallicRoughnessTexture", "normalTexture", "occlusionTexture", "emissiveTexture", "image"):
    img = getattr(mat, attr, None) if mat is not None else None
    if isinstance(img, Image.Image):
        if max(img.size) > args.max_texture:
            k = args.max_texture / max(img.size)
            img = img.resize((max(1, round(img.width * k)), max(1, round(img.height * k))), Image.LANCZOS)
            setattr(mat, attr, img)
        tex_px.append(max(img.size))

mesh.export(args.glb, include_normals=True)

lo, hi = mesh.bounds
print(json.dumps({"file": args.glb, "height_m": round(float(hi[1] - lo[1]), 4), "min_y": round(float(lo[1]), 5),
                  "triangles": int(len(mesh.faces)), "camera_deg": int(cam), "silhouette_iou": None if score is None else round(score, 2),
                  "width_m": round(float(max(hi[0] - lo[0], hi[2] - lo[2])), 4),
                  "textured": isinstance(mesh.visual, trimesh.visual.TextureVisuals),
                  "texture_px": max(tex_px) if tex_px else None}))
if score is not None and score < 0.6:
    print(f"AVISO: silhueta pouco parecida com a imagem (IoU {score:.2f}); confira a orientação no Unity.")
