# Duvar iç köşe (concave) üretici: kiremit.png'nin kenar profillerinden, köşe noktasına ortalanmış
# opak iç-köşe sprite'ı üretir (kenar kalınlıkları RIM, köşe yarıçapı r0). bake_wall.py kullanır.
# Gereken: pip install numpy pillow
import numpy as np
from PIL import Image
W="/Users/turgayp/Development Projects/Unity/tinyfixers/Assets/_Project/Art/UI/Obstacles/RobotStyle/Wall/"
C=1212
kir=np.asarray(Image.open(W+"kiremit.png").convert("RGBA").resize((C,C),Image.LANCZOS)).astype(np.float32)
RIM={'T':131,'L':106,'R':98,'B':94}
M=C//2
# edge profiles: index d = distance from outer boundary into piece
prof={
 'T':kir[:,M,:],            # top-facing rim (piece below boundary)
 'B':kir[::-1,M,:],
 'L':kir[M,:,:],
 'R':kir[M,::-1,:],
}
def concave(outside, r0):
    """outside: 'TL','TR','BL','BR' quadrant that is empty. returns (img RGBA, vertex(x,y))"""
    # local frame: outside is up-left; flip later
    rimA = RIM['T'] if outside[0]=='T' else RIM['B']   # rim facing the outside vertically
    rimB = RIM['L'] if outside[1]=='L' else RIM['R']
    pA = prof['T'] if outside[0]=='T' else prof['B']
    pB = prof['L'] if outside[1]=='L' else prof['R']
    R = int(max(rimA,rimB)+r0+40)
    N=2*R
    ys,xs=np.mgrid[0:N,0:N].astype(np.float32)
    # canonical: outside at top-left, vertex at (R,R); x right, y down
    px=xs-R; py=ys-R
    cx=-r0; cy=-r0
    out=np.zeros((N,N,4),np.float32)
    # distances
    inA = (px<=cx)            # left of arc zone -> boundary is horizontal line y=0 (rim faces up/outside)
    inB = (py<=cy)
    dA = py; dB = px
    dist=np.where(inA, dA, np.where(inB, dB, np.hypot(px-cx,py-cy)-r0))
    ang=np.where(inA, 1.0, np.where(inB, 0.0, np.clip(np.arctan2(py-cy,px-cx)/(np.pi/2),0,1)))  # 1 => A (vertical facing), 0 => B
    inside = dist>=0
    # in both inA and inB region (x<=cx and y<=cy) is outside
    inside &= ~(inA & inB)
    w = ang*rimA + (1-ang)*rimB
    t = np.clip(dist/np.maximum(w,1),0,1.0)
    def sample(p,rim):
        d=np.clip(t*rim,0,C-1); i=np.floor(d).astype(int); f=(d-i)[...,None]
        return p[i]*(1-f)+p[np.minimum(i+1,C-1)]*f
    col = sample(pA,rimA)*ang[...,None] + sample(pB,rimB)*(1-ang[...,None])
    # antialias edge
    alpha = np.clip(dist+0.5,0,1)*inside
    # Kare sınırında ton farkı görünmesin: köşe merkezinden uzaklaştıkça son 60px'te alttaki karoya erir
    edge=np.maximum(np.abs(px),np.abs(py))
    alpha = alpha*np.clip((R-edge)/60.0,0,1)
    out[...,:3]=col[...,:3]; out[...,3]=alpha*255
    img=Image.fromarray(out.astype(np.uint8),'RGBA')
    vx,vy=R,R
    if outside[1]=='R': img=img.transpose(Image.FLIP_LEFT_RIGHT); vx=N-R
    if outside[0]=='B': img=img.transpose(Image.FLIP_TOP_BOTTOM); vy=N-R
    return img,(vx,vy)
