"""Deterministic, locally authored smoke assets. No network or image service.

Requires numpy and Pillow; the bundled Codex Python already supplies both.
Usage: python generate_smoke_atlas.py OUTPUT_DIRECTORY [--proof PROOF_DIRECTORY]
Atlas: R optical density, GB projected volume normals, A self-shadow.
Flow: RG periodic curl displacement, B broad turbulence, A fine eddies.
"""
from pathlib import Path
import argparse
import json
import numpy as np
from PIL import Image


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def noise3(x, y, z, seed, period=0):
    """Continuous gradient noise, rather than pixel noise or blurred disks."""
    ix, iy, iz = np.floor(x).astype(np.int32), np.floor(y).astype(np.int32), np.floor(z).astype(np.int32)
    fx, fy, fz = x - ix, y - iy, z - iz
    ux, uy, uz = [t*t*t*(t*(t*6-15)+10) for t in (fx, fy, fz)]
    out = np.zeros(np.broadcast_shapes(x.shape, y.shape, z.shape), np.float32)
    grads = np.array([[1,1,0],[-1,1,0],[1,-1,0],[-1,-1,0],
                      [1,0,1],[-1,0,1],[1,0,-1],[-1,0,-1],
                      [0,1,1],[0,-1,1],[0,1,-1],[0,-1,-1]], np.float32)
    with np.errstate(over='ignore'):
        for dz in (0, 1):
            for dy in (0, 1):
                for dx in (0, 1):
                    a, b, c = ix + dx, iy + dy, iz + dz
                    if period:
                        a, b, c = a % period, b % period, c % period
                    h = a.astype(np.uint32)*np.uint32(73856093) ^ b.astype(np.uint32)*np.uint32(19349663) ^ c.astype(np.uint32)*np.uint32(83492791) ^ np.uint32(seed*971)
                    h ^= h >> 13
                    h *= np.uint32(1274126177)
                    g = grads[h % 12]
                    d = g[...,0]*(fx-dx) + g[...,1]*(fy-dy) + g[...,2]*(fz-dz)
                    w = (ux if dx else 1-ux)*(uy if dy else 1-uy)*(uz if dz else 1-uz)
                    out += (w*d).astype(np.float32)
    return out


def shift_zero(a, dz, dy, dx):
    out = np.roll(a, (dz,dy,dx), axis=(0,1,2))
    for axis, count in enumerate((dz,dy,dx)):
        if count:
            sl = [slice(None)]*3
            sl[axis] = slice(0,count) if count>0 else slice(count,None)
            out[tuple(sl)] = 0
    return out


def make_tile(index, n=256, depth=40):
    rng = np.random.default_rng(4139 + index*103)
    u = np.linspace(-1,1,n,dtype=np.float32)
    z = np.linspace(-.85,.85,depth,dtype=np.float32)[:,None,None]
    y, x = u[None,:,None], u[None,None,:]
    # Connected rising plume: narrow root, larger rolled lobes above it.
    # Each ellipsoid is warped by the same 3D field, so lobes merge into vapor.
    warp = noise3(x*2.3+13,y*2.3+11,z*2.3+17,index+2)
    wx = x + noise3(x*2.8+23,y*2.8+9,z*2.8+4,index+41)*.14
    wy = y + warp*.14
    wz = z + noise3(x*2.5+2,y*2.5+21,z*2.5+14,index+72)*.13
    sdf = np.full((depth,n,n), 10, np.float32)
    for j in range(9):
        height = -.60 + j*.15 + rng.uniform(-.10,.10)
        radius = .22 + .14*(height+.60) + rng.uniform(-.035,.045)
        cx = rng.uniform(-.20,.20) + .10*np.sin(j*1.7+index)
        cz = rng.uniform(-.16,.16)
        ellipse = np.sqrt(((wx-cx)/(radius*rng.uniform(.90,1.25)))**2 +
                          ((wy-height)/(radius*rng.uniform(.8,1.1)))**2 +
                          ((wz-cz)/(radius*rng.uniform(.85,1.25)))**2)-1
        # Smooth union retains a continuous connected body between billows.
        h = np.clip(.5 + .5*(ellipse-sdf)/.42, 0, 1)
        sdf = ellipse*(1-h) + sdf*h - .42*h*(1-h)
    coarse = noise3(wx*4.1+7,wy*4.1+17,wz*4.1+12,index+101)
    folds = noise3(wx*9.3+2,wy*9.3+5,wz*9.3+13,index+151)
    eddies = noise3(wx*20.7+16,wy*20.7+19,wz*20.7+4,index+211)
    fine = noise3(wx*43.1+3,wy*43.1+29,wz*43.1+23,index+271)
    turbulent_surface = sdf + coarse*.54 + folds*.24 + eddies*.095
    volume = smooth(.25,-.16,turbulent_surface)
    # A turbulent density field supplies hollow folds and wisps inside the rim.
    volume *= np.clip(.68 + coarse*.78 + folds*.43 + eddies*.25 + fine*.12, .04, 1.25)
    edge = smooth(.045,.14,1-np.maximum(np.abs(x),np.abs(y)))
    volume *= edge
    dz = 1.7/depth
    optical = volume*dz*2.5
    alpha_step = 1-np.exp(-optical)
    # Offline shadow integration from the upper left and front, not screen noise.
    shadow_depth = np.zeros_like(volume)
    for step in range(1,9):
        shadow_depth += shift_zero(volume, -step, step*3, step*3)*dz*1.4
    lighting = .43 + .57*np.exp(-shadow_depth*1.85)
    trans = np.ones((n,n),np.float32)
    density = np.zeros((n,n),np.float32)
    light = np.zeros((n,n),np.float32)
    mean_z = np.zeros((n,n),np.float32)
    for k in range(depth):
        w = trans*alpha_step[k]
        density += w
        light += w*lighting[k]
        mean_z += w*(k/depth)
        trans *= 1-alpha_step[k]
    density = np.clip(density*1.55,0,1)*edge[0]
    shade = light/np.maximum(1-trans,1e-5)
    projected_depth = mean_z/np.maximum(1-trans,1e-5)
    gy, gx = np.gradient(projected_depth)
    # Smooth volume normals are explicitly stored, avoiding fragment derivatives.
    nx, ny = -gx*n*.45, -gy*n*.45
    scale = np.sqrt(nx*nx+ny*ny+1)
    rgba = np.stack((density,.5+.5*nx/scale,.5+.5*ny/scale,shade),-1)
    rgba[...,1:3] = np.where((density>0)[...,None],rgba[...,1:3],.5)
    rgba[...,3] = np.where(density>0,rgba[...,3],1)
    tile=np.round(np.clip(rgba,0,1)*255).astype(np.uint8)
    return finish_tile(tile)


def finish_tile(tile):
    """Preserve billow contrast after optical projection, and face the plume up."""
    q=tile.astype(np.float32)/255
    # Distinguish thick shadowed folds from thin sunlit vapor even where the
    # accumulated optical density previously reached unity.
    q[...,0] *= .65 + .35*(1-q[...,3])/.57
    q[...,1:3]=smooth_normals(q[...,1:3])
    return np.round(np.clip(q[::-1],0,1)*255).astype(np.uint8)


def smooth_normals(normals):
    # Integrate small ray-step normal variations into a coherent volume surface.
    weights=(1,4,6,4,1)
    for axis in (0,1):
        padded=np.pad(normals,((2,2),(2,2),(0,0)),mode='edge')
        n=normals.shape[axis]
        slices=[]
        for j,w in enumerate(weights):
            if axis==0:
                slices.append(padded[j:j+n,2:-2]*w/16)
            else:
                slices.append(padded[2:-2,j:j+n]*w/16)
        normals=sum(slices)
    return normals


def make_flow(n=256):
    u = np.arange(n,dtype=np.float32)/n
    y, x = u[:,None],u[None,:]
    field = sum(noise3(x*f+5,y*f+8,np.zeros((n,n),np.float32)+3,390+i,period=f)*a
                for i,(f,a) in enumerate(((3,1.0),(6,.28),(12,.055))))
    # Central periodic differences form a divergence-free curled vector field.
    dx = np.roll(field,-1,1)-np.roll(field,1,1)
    dy = np.roll(field,-1,0)-np.roll(field,1,0)
    curl = np.stack((dy,-dx),-1)
    curl /= max(np.percentile(np.abs(curl),98),1e-5)
    broad = .5 + noise3(x*8+4,y*8+9,np.zeros((n,n),np.float32)+7,803,period=8)*.8
    detail = .5 + sum(noise3(x*f+2,y*f+4,np.zeros((n,n),np.float32)+1,821+i,period=f)*a
                      for i,(f,a) in enumerate(((32,.58),(64,.24),(128,.10))))
    return np.round(np.clip(np.dstack((.5+curl[...,0]*.5,.5+curl[...,1]*.5,broad,detail)),0,1)*255).astype(np.uint8)


def preview_tile(tile):
    q = tile.astype(np.float32)/255
    base = np.array([.85,.8204,.61456],np.float32)
    dense = np.array([.59696,.55006,.26409],np.float32)
    amount = (1-q[...,3])*.38 + q[...,0]*.10
    rgb = base[None,None,:]*(1-amount[...,None])+dense[None,None,:]*amount[...,None]
    rgb *= (.90+.10*q[...,3])[...,None]
    alpha = q[...,0]*.58
    bg = np.full_like(rgb,[.115,.125,.155])
    return np.round(np.clip(rgb*alpha[...,None]+bg*(1-alpha[...,None]),0,1)*255).astype(np.uint8)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('output',type=Path)
    parser.add_argument('--proof',type=Path)
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    atlas=np.zeros((1024,1024,4),np.uint8)
    stats=[]
    for i in range(16):
        tile=make_tile(i)
        row,col=divmod(i,4)
        atlas[row*256:(row+1)*256,col*256:(col+1)*256]=tile
        d=tile[...,0].astype(np.float32)/255
        gy,gx=np.gradient(d)
        stats.append({'tile':i,'boundary_max':int(max(tile[0,:,0].max(),tile[-1,:,0].max(),tile[:,0,0].max(),tile[:,-1,0].max())),
                      'density_std':float(d[d>.03].std()),'coverage':float((d>.03).mean()),
                      'gradient_mean':float(np.sqrt(gx*gx+gy*gy).mean())})
        print(f'Integrated billow {i+1}/16',flush=True)
    Image.fromarray(atlas).save(args.output/'NativeSmokeAtlas.png')
    Image.fromarray(make_flow()).save(args.output/'NativeSmokeFlow.png')
    if args.proof:
        args.proof.mkdir(parents=True,exist_ok=True)
        montage=np.zeros((1024,1024,3),np.uint8)
        for i in range(16):
            row,col=divmod(i,4)
            montage[row*256:(row+1)*256,col*256:(col+1)*256]=preview_tile(atlas[row*256:(row+1)*256,col*256:(col+1)*256])
        Image.fromarray(montage).save(args.proof/'billow-contact-sheet.png')
        (args.proof/'atlas-statistics.json').write_text(json.dumps(stats,indent=2))
    print('Smoke atlas and periodic curl flow saved.',flush=True)


if __name__=='__main__':
    main()
