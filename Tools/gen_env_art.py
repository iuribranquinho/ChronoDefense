# Gera a vegetação pintada (estilo da referência do usuário): contorno escuro, cel-shading, verde saturado.
import numpy as np, math, os, sys, random
from PIL import Image, ImageDraw, ImageFilter
import cv2
OUT=sys.argv[1] if len(sys.argv)>1 else '/tmp/env/out'
SS=4
def hx(h,a=255): h=h.lstrip('#'); return tuple(int(h[i:i+2],16) for i in (0,2,4))+(a,)
def mix(a,b,t): return tuple(int(round(a[i]+(b[i]-a[i])*t)) for i in range(3))+(255,)
def dark(c,k=0.35): return (int(c[0]*k),int(c[1]*k),int(c[2]*k),255)

def outline(img, px, col):
    """Contorno externo de px pixels (no canvas supersampled) por dilatação do alfa."""
    a=np.array(img)[...,3]
    k=cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(2*px+1,2*px+1))
    d=cv2.dilate(a,k)
    base=Image.new('RGBA',img.size,col[:3]+(0,)); arr=np.array(base); arr[...,3]=d
    base=Image.fromarray(arr); base.alpha_composite(img); return base

PIVOT_Y=0.1   # todos os props: ponto de contato com o chão em (0.5, 0.1) da imagem (C#: ArtLibrary.PropPivot)
def finish(img, w, h, name, sil=3, anchor=None):
    img=outline(img, sil*SS//1, (28,40,18,255))
    if anchor is not None:
        a=np.array(img)[...,3]; ys,xs=np.nonzero(a>8)
        ax,ay=anchor; W2,H2=img.size
        if ay is None: ay=ys.max()-(ys.max()-ys.min())*0.06   # base = fundo do desenho
        k=min(1.0, (W2*0.49)/max(1,max(ax-xs.min(),xs.max()-ax)), (H2*(1-PIVOT_Y)*0.99)/max(1,ay-ys.min()),
              (H2*PIVOT_Y*0.98)/max(1,ys.max()-ay))
        big=img.resize((int(W2*k),int(H2*k)),Image.LANCZOS)
        canvas=Image.new('RGBA',img.size,(0,0,0,0))
        canvas.alpha_composite(big,(int(W2/2-ax*k),int(H2*(1-PIVOT_Y)-ay*k)))
        img=canvas
    img=img.resize((w,h),Image.LANCZOS)
    os.makedirs(os.path.dirname(name),exist_ok=True); img.save(name)
    return img

def ellipse_poly(cx,cy,rx,ry,ang,n=24):
    ca,sa=math.cos(ang),math.sin(ang); pts=[]
    for i in range(n):
        t=2*math.pi*i/n; x,y=rx*math.cos(t),ry*math.sin(t)
        pts.append((cx+x*ca-y*sa, cy+x*sa+y*ca))
    return pts

def leaflet(cx,cy,ang,L,W):
    """folha pontuda (gota alongada) começando em (cx,cy) na direção ang"""
    pts=[]; n=14
    for i in range(n+1):
        t=i/n; w=W*math.sin(math.pi*t)**0.8; pts.append((t*L, w))
    for i in range(n,-1,-1):
        t=i/n; w=W*math.sin(math.pi*t)**0.8; pts.append((t*L,-w*0.8))
    ca,sa=math.cos(ang),math.sin(ang)
    return [(cx+x*ca-y*sa, cy+x*sa+y*ca) for x,y in pts]

# ---------------------------------------------------------------- samambaia / folhagem tropical
def frond(d, base, ang, L, droop, col, colL, ink, leaf_w, rng, palm=False):
    pts=[]; N=40
    for i in range(N+1):
        t=i/N; x=base[0]+math.cos(ang)*L*t; y=base[1]-math.sin(ang)*L*t+droop*t*t
        pts.append((x,y))
    # folíolos dos dois lados
    for i in range(4,N,3 if not palm else 2):
        t=i/N; p=pts[i]; q=pts[min(N,i+1)]
        tang=math.atan2(q[1]-p[1],q[0]-p[0])
        size=L*(0.24 if not palm else 0.26)*math.sin(math.pi*min(1,t*1.15))**0.7*(1-0.5*t)
        if size<3: continue
        for s in (-1,1):
            a=tang+s*math.radians(55 if not palm else 70)+ (math.radians(18) if palm else 0)
            poly=leaflet(p[0],p[1],a,size,size*leaf_w)
            d.polygon(poly, fill=col, outline=ink, width=max(2,SS))
            # nervura clara
            d.line([p,(p[0]+math.cos(a)*size*0.75,p[1]+math.sin(a)*size*0.75)], fill=colL, width=max(1,SS//2))
    d.line(pts, fill=ink, width=SS*2); d.line(pts, fill=colL, width=SS)

def fern(path, W=256, H=256, pal=None, seed=1, palm_like=False):
    rng=random.Random(seed); S=W*SS
    img=Image.new('RGBA',(S,H*SS),(0,0,0,0)); d=ImageDraw.Draw(img)
    base=(S*0.5,H*SS*0.86); dark_c,mid_c,light_c=pal; ink=dark(dark_c,0.45)
    angs=[150,125,100,80,55,30, 165,15, 112,68]
    order=sorted(range(len(angs)),key=lambda i: -abs(angs[i]-90))  # de lado (fundo) primeiro
    for k,i in enumerate(angs):
        a=math.radians(i+rng.uniform(-6,6)); L=S*rng.uniform(0.36,0.44)*(0.9 if abs(i-90)<20 else 1)
        dr=S*0.22*abs(math.cos(a))+S*0.05
        c=mix(dark_c,mid_c,0.3) if k>=6 else mix(mid_c,light_c,rng.uniform(0,0.4))
        frond(d,base,a,L,dr,c,mix(c,light_c,0.6),ink,0.42,rng)
    return finish(img,W,H,path,anchor=(base[0],None))

def palm(path, W=512, H=512, seed=3, trunk=('#8A6438','#B5875A'), pal=None):
    rng=random.Random(seed); S=W*SS; HS=H*SS
    img=Image.new('RGBA',(S,HS),(0,0,0,0)); d=ImageDraw.Draw(img)
    tk_d,tk_l=hx(trunk[0]),hx(trunk[1]); ink=dark(tk_d,0.4)
    top=(S*0.53,HS*0.30); bot=(S*0.47,HS*0.97)
    segs=14
    for i in range(segs):
        t0=i/segs; t1=(i+1)/segs
        def P(t): return (bot[0]+(top[0]-bot[0])*t + math.sin(t*2.2)*S*0.04, bot[1]+(top[1]-bot[1])*t)
        a,b=P(t0),P(t1); w0=S*(0.045-0.018*t0); w1=S*(0.045-0.018*t1)
        poly=[(a[0]-w0,a[1]),(a[0]+w0,a[1]),(b[0]+w1*1.08,b[1]),(b[0]-w1*1.08,b[1])]
        d.polygon(poly,fill=tk_l,outline=ink,width=SS*2)
        d.polygon([(a[0]+w0*0.3,a[1]),(a[0]+w0,a[1]),(b[0]+w1*1.08,b[1]),(b[0]+w1*0.3,b[1])],fill=tk_d)
    dark_c,mid_c,light_c=pal; lk=dark(dark_c,0.45)
    for k,ang in enumerate([175,150,120,95,70,40,8, 200,-20]):
        a=math.radians(ang+rng.uniform(-5,5)); L=S*rng.uniform(0.34,0.42)
        c=mix(dark_c,mid_c,0.35) if k<3 or k>=7 else mix(mid_c,light_c,rng.uniform(0.1,0.5))
        frond(d,top,a,L,S*0.16,c,mix(c,light_c,0.6),lk,0.22,rng,palm=True)
    for o in ((-0.04,0.02),(0.03,0.03),(0,0.05)):
        cx,cy=top[0]+o[0]*S*0.5,top[1]+o[1]*S*0.5; r=S*0.02
        d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=hx('#6B4A2A'),outline=ink,width=SS*2)
    return finish(img,W,H,path,anchor=bot)

# ---------------------------------------------------------------- arbusto / copa (bolas recortadas)
def lobe_cluster(d, cx, cy, R, n, pal, rng, ink, spread=(1.0,0.7), lumpy=True, rscale=(0.38,0.55)):
    dark_c,mid_c,light_c=pal; blobs=[]
    for i in range(n):
        ang=rng.uniform(0,2*math.pi); rr=math.sqrt(rng.random())
        x=cx+math.cos(ang)*R*spread[0]*rr; y=cy+math.sin(ang)*R*spread[1]*rr
        r=R*rng.uniform(*rscale); blobs.append((x,y,r))
    blobs.sort(key=lambda b: b[1])          # de cima (fundo) para baixo (frente)... e luz pelo topo
    for x,y,r in blobs:
        tt=(y-(cy-R))/(2*R); shade=max(0,min(1,1-tt))
        base=mix(dark_c,mid_c,0.35+0.4*shade)
        pts=[]
        for k in range(36):
            a=2*math.pi*k/36; rad=r*(1+(0.07*math.sin(a*9+x) if lumpy else 0))
            pts.append((x+math.cos(a)*rad, y+math.sin(a)*rad))
        d.polygon(pts,fill=base,outline=ink,width=SS*2)
        # luz em cima à esquerda
        hl=[]
        for k in range(36):
            a=2*math.pi*k/36; rad=r*0.72*(1+(0.07*math.sin(a*9+x) if lumpy else 0))
            hl.append((x-r*0.16+math.cos(a)*rad, y-r*0.2+math.sin(a)*rad*0.8))
        d.polygon(hl,fill=mix(base,light_c,0.45))
        # traços de folha
        for _ in range(4):
            a=rng.uniform(math.pi*0.9,math.pi*2.1); rad=r*rng.uniform(0.35,0.7)
            px,py=x+math.cos(a)*rad,y+math.sin(a)*rad
            d.arc([px-r*0.18,py-r*0.12,px+r*0.18,py+r*0.12],200,340,fill=mix(dark_c,ink,0.3),width=SS)
    return blobs

def bush(path,W=256,H=256,pal=None,seed=5):
    rng=random.Random(seed); S=W*SS
    img=Image.new('RGBA',(S,H*SS),(0,0,0,0)); d=ImageDraw.Draw(img); ink=dark(pal[0],0.45)
    lobe_cluster(d,S*0.5,H*SS*0.55,S*0.34,12,pal,rng,ink)
    return finish(img,W,H,path,anchor=(S*0.5,None))

def tree(path,W=1024,H=1024,pal=None,seed=7,bark=('#6E4A2C','#9A6B44'),moss=True):
    rng=random.Random(seed); S=W*SS; HS=H*SS
    img=Image.new('RGBA',(S,HS),(0,0,0,0)); d=ImageDraw.Draw(img)
    bd,bl=hx(bark[0]),hx(bark[1]); ink=dark(bd,0.45)
    # tronco com raízes
    cx=S*0.5; y0=HS*0.97; y1=HS*0.6
    trunk=[(cx-S*0.06,y1),(cx+S*0.055,y1),(cx+S*0.07,y0-HS*0.1),(cx+S*0.16,y0),(cx+S*0.05,y0-HS*0.02),(cx,y0+HS*0.005),
           (cx-S*0.06,y0-HS*0.02),(cx-S*0.17,y0),(cx-S*0.08,y0-HS*0.1)]
    d.polygon(trunk,fill=bl,outline=ink,width=SS*3)
    d.polygon([(cx+S*0.01,y1),(cx+S*0.055,y1),(cx+S*0.07,y0-HS*0.1),(cx+S*0.15,y0-HS*0.005),(cx+S*0.045,y0-HS*0.025),(cx+S*0.015,y0-HS*0.03)],fill=bd)
    for i in range(7):   # veios da casca
        x=cx+rng.uniform(-0.045,0.04)*S; ya=y1+rng.uniform(0.05,0.3)*(y0-y1); yb=ya+rng.uniform(0.15,0.3)*(y0-y1)
        d.line([(x,ya),(x+rng.uniform(-1,1)*S*0.02,(ya+yb)/2),(x,yb)],fill=dark(bd,0.6),width=SS*2)
    if moss:
        mc=pal[2]
        for i in range(3):
            x=cx+rng.uniform(-0.05,0.03)*S; y=y1+rng.uniform(0.2,0.7)*(y0-y1); r=S*0.025
            d.ellipse([x-r,y-r*0.7,x+r,y+r*0.7],fill=mc,outline=dark(pal[0],0.45),width=SS*2)
    # copa: 3 aglomerados
    lk=dark(pal[0],0.45)
    for (fx,fy,fr,n) in ((0.25,0.42,0.18,5),(0.75,0.41,0.18,5),(0.5,0.34,0.34,8),(0.36,0.2,0.2,4),(0.64,0.2,0.2,4)):
        lobe_cluster(d,S*fx,HS*fy,S*fr,n,pal,rng,lk,spread=(1.0,0.6),rscale=(0.5,0.68))
    if moss:   # musgo pendurado
        for i in range(9):
            x=S*rng.uniform(0.12,0.88); y=HS*rng.uniform(0.45,0.56); L=HS*rng.uniform(0.05,0.11)
            pts=[(x+math.sin(t*6+i)*S*0.008,y+L*t) for t in np.linspace(0,1,12)]
            d.line(pts,fill=lk,width=SS*5); d.line(pts,fill=pal[2],width=SS*3)
    return finish(img,W,H,path,anchor=(cx,y0))

def pine(path,W=256,H=512,pal=None,seed=9):
    rng=random.Random(seed); S=W*SS; HS=H*SS
    img=Image.new('RGBA',(S,HS),(0,0,0,0)); d=ImageDraw.Draw(img)
    ink=dark(pal[0],0.45); cx=S*0.5
    d.rectangle([cx-S*0.05,HS*0.8,cx+S*0.05,HS*0.97],fill=hx('#7A5030'),outline=dark(hx('#7A5030'),0.4),width=SS*2)
    for k in range(5):
        yb=HS*(0.86-k*0.155); yt=yb-HS*0.27; w=S*(0.46-k*0.075)
        pts=[(cx,yt)]
        n=7
        for i in range(n+1):   # borda serrilhada
            t=i/n; pts.append((cx+w*t+ (S*0.02 if i%2 else 0), yt+(yb-yt)*t + (0 if i%2 else -HS*0.012)))
        for i in range(n,-1,-1):
            t=i/n; pts.append((cx-w*t-(S*0.02 if i%2 else 0), yt+(yb-yt)*t + (0 if i%2 else -HS*0.012)))
        c=mix(pal[0],pal[1],0.4+0.12*k)
        d.polygon(pts,fill=c,outline=ink,width=SS*2)
        d.polygon([(cx,yt+HS*0.02),(cx-w*0.85,yb-HS*0.03),(cx-w*0.2,yb-HS*0.02)],fill=mix(c,pal[2],0.35))
    return finish(img,W,H,path,anchor=(cx,HS*0.97))

# ---------------------------------------------------------------- pedra com musgo
def boulder(path,W=256,H=256,seed=11,stone=('#8E9490','#B8BEBA','#D6DAD6'),moss=None):
    rng=random.Random(seed); S=W*SS; HS=H*SS
    img=Image.new('RGBA',(S,HS),(0,0,0,0)); d=ImageDraw.Draw(img)
    sd,sm,sl=[hx(c) for c in stone]; ink=dark(sd,0.35)
    cx,cy=S*0.5,HS*0.6; rx,ry=S*0.4,HS*0.32; pts=[]
    for k in range(11):
        a=2*math.pi*k/11+rng.uniform(-0.15,0.15); r=rng.uniform(0.85,1.05)
        pts.append((cx+math.cos(a)*rx*r, cy+math.sin(a)*ry*r*(1.0 if math.sin(a)<0 else 0.85)))
    d.polygon(pts,fill=sm,outline=ink,width=SS*3)
    # facetas: luz em cima/esquerda, sombra embaixo/direita
    hl=[(x*0.62+cx*0.38-S*0.05, y*0.6+cy*0.4-HS*0.07) for x,y in pts]
    d.polygon(hl,fill=sl)
    sh=[p for p in pts if p[1]>cy-HS*0.02 or p[0]>cx+S*0.15]
    if len(sh)>2: d.polygon(sh+[(cx+S*0.1,cy+HS*0.02)],fill=sd)
    d.line([(cx-S*0.05,cy-HS*0.05),(cx+S*0.02,cy+HS*0.06),(cx+S*0.1,cy+HS*0.1)],fill=ink,width=SS*2)
    if moss:
        md,mm,ml=moss
        rock=np.zeros((HS,S),np.uint8); cv2.fillPoly(rock,[np.array(pts,np.int32)],255)
        top=min(p[1] for p in pts)
        xs=np.arange(S); capy=top+HS*0.16+HS*0.035*np.sin(xs/S*14+seed)+HS*0.02*np.sin(xs/S*31)
        yy=np.arange(HS)[:,None]
        m=((yy<capy[None,:])&(rock>0)).astype(np.uint8)*255
        # gotas de musgo escorrendo
        for _ in range(2):
            x=int(rng.uniform(cx-rx*0.5,cx+rx*0.5)); y=int(capy[x]); r=int(S*0.03); L=int(HS*rng.uniform(0.02,0.045))
            cv2.ellipse(m,(x,y+L//2),(r,L//2+r),0,0,360,255,-1)
        m=np.minimum(m,rock)
        layer=np.zeros((HS,S,4),np.uint8); layer[...]=mm; layer[...,3]=m
        img.alpha_composite(Image.fromarray(layer))
        # contorno do musgo (só dentro da pedra) e luz
        edge=cv2.morphologyEx(m,cv2.MORPH_GRADIENT,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(SS*2+1,SS*2+1)))
        edge=np.minimum(edge,cv2.erode(rock,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(SS*3,SS*3))))
        el=np.zeros((HS,S,4),np.uint8); el[...]=dark(md,0.55); el[...,3]=edge
        img.alpha_composite(Image.fromarray(el))
        d=ImageDraw.Draw(img)
        for _ in range(9):
            x=rng.uniform(cx-rx*0.6,cx+rx*0.5); y=rng.uniform(top+HS*0.02,top+HS*0.09)
            if m[int(y),int(x)]>0: d.ellipse([x-SS*6,y-SS*3.5,x+SS*6,y+SS*3.5],fill=ml)
        d.polygon(pts,outline=ink,width=SS*3)
    return finish(img,W,H,path,anchor=(cx,None))

# ---------------------------------------------------------------- chão e caminho (tiles sem emenda)
def pnoise(n, cells, seed):
    r=np.random.default_rng(seed); w=r.standard_normal((n,n))
    f=np.fft.fft2(w); ky=np.fft.fftfreq(n)[:,None]*n; kx=np.fft.fftfreq(n)[None,:]*n
    g=np.exp(-(kx**2+ky**2)/(2*cells**2)); v=np.real(np.fft.ifft2(f*g))
    return (v-v.min())/(v.max()-v.min())

def stamp_wrap(d, n, fn):
    for ox in (-n,0,n):
        for oy in (-n,0,n): fn(d,ox,oy)

def ground(path, n=512, A='#78BE3C', B='#8FCD48', D='#4E9A2C', L='#B8E46A', blotch=None, blotch_amt=0, ripples=False, seed=1, blade=1.0):
    S=n*2; rng=np.random.default_rng(seed); R=random.Random(seed)
    big=pnoise(S,2,seed); mid=pnoise(S,5,seed+1)
    a,b,dk=[np.array(hx(c)[:3],float) for c in (A,B,D)]
    img=a[None,None]*(1-big[...,None])+b[None,None]*big[...,None]
    img=img*(1-np.clip(mid-0.6,0,1)[...,None]*1.2)+dk*np.clip(mid-0.6,0,1)[...,None]*1.2
    if blotch:
        bm=pnoise(S,4,seed+5); cut=1-blotch_amt*0.5; t=np.clip((bm-cut)*8,0,1)[...,None]*0.9
        img=img*(1-t)+np.array(hx(blotch)[:3],float)*t
    if ripples:
        yy=np.arange(S)[:,None]/S; w=np.sin((yy*8+pnoise(S,2,seed+7)*2)*2*np.pi)
        img=img*(1-np.clip(w-0.7,0,1)[...,None]*1.5)+np.array(hx(L)[:3],float)*np.clip(w-0.7,0,1)[...,None]*1.5
        img=img*(1-np.clip(-w-0.8,0,1)[...,None]*1.2)+dk*np.clip(-w-0.8,0,1)[...,None]*1.2
    im=Image.fromarray(np.clip(img,0,255).astype('uint8')).convert('RGBA'); d=ImageDraw.Draw(im)
    dcol=hx(D); lcol=hx(L)
    def tuft(d,ox,oy,x,y,s,c,w):
        for k in (-1,0,1):
            d.line([(x+ox,y+oy),(x+ox+k*s*0.45,y+oy-s*(1-0.25*abs(k)))],fill=c,width=w)
    for i in range(int(650*blade)):
        x,y=R.uniform(0,S),R.uniform(0,S); s=R.uniform(12,24)
        c=dcol if R.random()<0.7 else lcol
        stamp_wrap(d,S,lambda d,ox,oy: tuft(d,ox,oy,x,y,s,c,3))
    im=im.resize((n,n),Image.LANCZOS); os.makedirs(os.path.dirname(path),exist_ok=True); im.save(path); return im

def dirt(path, n=256, base='#D8B77A', light='#E8CC93', darkc='#BF9A5E', rim='#6E4A2A', seed=3):
    S=n*4; R=random.Random(seed)
    big=pnoise(S,3,seed); bA=np.array(hx(base)[:3],float); bL=np.array(hx(light)[:3],float); bD=np.array(hx(darkc)[:3],float)
    img=bA*(1-np.clip(big-0.5,0,1)[...,None]*2)+bL*np.clip(big-0.5,0,1)[...,None]*2
    m2=pnoise(S,9,seed+2); t=np.clip(m2-0.62,0,1)[...,None]*2.5
    img=img*(1-t)+bD*t
    # borda ondulada (y = atravessa o caminho; x = ao longo, periódico)
    xs=np.arange(S)/S
    e1=0.065+0.025*np.sin(xs*2*np.pi*3+1)+0.015*np.sin(xs*2*np.pi*7)
    e2=0.065+0.025*np.sin(xs*2*np.pi*2+4)+0.015*np.sin(xs*2*np.pi*9+2)
    yy=np.arange(S)[:,None]/S
    dtop=yy-e1[None,:]; dbot=(1-yy)-e2[None,:]; dd=np.minimum(dtop,dbot)
    shade=np.clip(1-dd/0.07,0,1)[...,None]*0.35
    img=img*(1-shade)+bD*0.8*shade
    alpha=np.clip(dd*S/2.0+0.5,0,1)
    rimm=(dd<0.014)&(dd>-0.002)
    img[rimm]=hx(rim)[:3]
    im=Image.fromarray(np.dstack([np.clip(img,0,255),alpha*255]).astype('uint8'),'RGBA'); d=ImageDraw.Draw(im)
    for i in range(70):   # pedrinhas e marcas
        x=R.uniform(0,S); y=R.uniform(0.16,0.84)*S; r=R.uniform(5,14)
        for ox in (-S,0,S):
            if R.random()<0.65:
                d.ellipse([x+ox-r,y-r*0.7,x+ox+r,y+r*0.7],fill=hx(darkc))
            else:
                d.ellipse([x+ox-r*0.7,y-r*0.5,x+ox+r*0.7,y+r*0.5],fill=hx('#A89A86'),outline=hx(rim),width=3)
    im=im.resize((n,n),Image.LANCZOS); im.save(path); return im

GREEN=(hx('#2F7A2A'),hx('#58B43A'),hx('#A6E05A'))
DEEP=(hx('#24602A'),hx('#3F8E33'),hx('#86C94A'))
OLIVE=(hx('#6E7A2E'),hx('#A3A64A'),hx('#D9D27E'))
PALMG=(hx('#2F7A2A'),hx('#5DB33E'),hx('#A6E05A'))
PINEG=(hx('#1F5A33'),hx('#2F7D46'),hx('#6DB36A'))
MOSS=(hx('#3E7A22'),hx('#7FC23A'),hx('#B8E46A'))

def biome(name):
    o=f'{OUT}/{name}'
    if name=='Forest':
        ground(f'{o}/ENV_Ground.png'); dirt(f'{o}/ENV_Path.png')
        fern(f'{o}/ENV_Fern_A.png',pal=GREEN,seed=1); fern(f'{o}/ENV_Fern_B.png',pal=DEEP,seed=2)
        bush(f'{o}/ENV_Bush_A.png',pal=GREEN,seed=3); bush(f'{o}/ENV_Bush_B.png',pal=DEEP,seed=4)
        boulder(f'{o}/ENV_Rock_A.png',seed=5,moss=MOSS); boulder(f'{o}/ENV_Rock_B.png',seed=6,moss=MOSS)
        tree(f'{o}/ENV_Tree_A.png',pal=DEEP,seed=7); tree(f'{o}/ENV_Tree_B.png',pal=GREEN,seed=8)
        palm(f'{o}/ENV_Palm_A.png',pal=PALMG,seed=9)
    if name=='Savanna':
        ground(f'{o}/ENV_Ground.png',A='#A9B653',B='#C2C46B',D='#7C8A38',L='#E6DC9A',blotch='#DDC387',blotch_amt=0.55,seed=11,blade=0.8)
        dirt(f'{o}/ENV_Path.png',base='#DDBF86',light='#EED6A2',darkc='#C4A46B',seed=12)
        bush(f'{o}/ENV_Bush_A.png',pal=OLIVE,seed=13); fern(f'{o}/ENV_Fern_A.png',pal=OLIVE,seed=14)
        boulder(f'{o}/ENV_Rock_A.png',seed=15,stone=('#A08C70','#C9B594','#E2D2B2'))
        palm(f'{o}/ENV_Palm_A.png',pal=PALMG,seed=16); palm(f'{o}/ENV_Palm_B.png',pal=PALMG,seed=17)
        tree(f'{o}/ENV_Tree_A.png',pal=(hx('#4F7A2A'),hx('#86AE45'),hx('#C9D87A')),seed=18,moss=False)
    if name=='Desert':
        ground(f'{o}/ENV_Ground.png',A='#E3C98D',B='#D4B474',D='#B8925A',L='#F4E4B8',ripples=True,seed=21,blade=0.12)
        bush(f'{o}/ENV_Bush_A.png',pal=OLIVE,seed=22)
        boulder(f'{o}/ENV_Rock_A.png',seed=23,stone=('#A9824F','#D2AC74','#EACD98'))
        boulder(f'{o}/ENV_Rock_B.png',seed=24,stone=('#A9824F','#D2AC74','#EACD98'))
        palm(f'{o}/ENV_Palm_A.png',pal=PALMG,seed=25); palm(f'{o}/ENV_Palm_B.png',pal=PALMG,seed=26)
    if name=='Highland':
        ground(f'{o}/ENV_Ground.png',A='#5E9646',B='#70A853',D='#3E6B33',L='#A9C77C',blotch='#8E918A',blotch_amt=0.35,seed=31)
        bush(f'{o}/ENV_Bush_A.png',pal=DEEP,seed=32); fern(f'{o}/ENV_Fern_A.png',pal=DEEP,seed=33)
        boulder(f'{o}/ENV_Rock_A.png',seed=34,moss=MOSS); boulder(f'{o}/ENV_Rock_B.png',seed=35)
        pine(f'{o}/ENV_Pine_A.png',pal=PINEG,seed=36); pine(f'{o}/ENV_Pine_B.png',pal=PINEG,seed=37)
        tree(f'{o}/ENV_Tree_A.png',pal=DEEP,seed=38)

if __name__=='__main__':
    for b in (sys.argv[2:] or ['Forest','Savanna','Desert','Highland']): biome(b)
