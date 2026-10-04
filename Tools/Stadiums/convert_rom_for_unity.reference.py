from pathlib import Path
import json,shutil,hashlib
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parent
OUT=ROOT/'unity';OUT.mkdir(exist_ok=True)
# Named identity verified against public geometry; other records retain their ROM index.
names={0:('violet_gym','Arène de Mauville',312612),1:('azalea_gym','Arène d’Écorcia',312613),2:('goldenrod_gym','Arène de Doublonville',312741),3:('ecruteak_gym','Arène de Rosalia',312742),4:('olivine_gym','Arène d’Oliville',312744),5:('cianwood_gym','Arène d’Irisia',312743),6:('mahogany_gym','Arène d’Acajou',313504),7:('blackthorn_gym','Arène d’Ébènelle',313505),8:('team_rocket_battle','Repaire Rocket',312923),27:('classroom','Salle de classe',312538),28:('free_battle','Stade de combat libre',312530)}
for i in range(9,14):names[i]=(f'elite_four_{i-8}',f'Ligue — salle {i-8}',313718)
# Reuse bounded, source-derived texture reconstruction; no top-level rerun.
namespace={'__file__':str(ROOT/'prepared/prepare_assets.py')}
script=(ROOT/'prepared/prepare_assets.py').read_text(encoding='utf-8')
exec(script[:script.index('def read_mtl')],namespace)
namespace['OUT']=OUT;namespace['SOURCE']=ROOT/'rom-arenas'
report=[]
for folder in sorted((ROOT/'rom-arenas').glob('arena_*')):
    index=int(folder.name[-2:]);data=json.loads((folder/'model.json').read_text())
    key,name,asset=names.get(index,(folder.name,f'Décor Stadium 2 — index {index:02d}',0))
    verts=[];groups=[];planes={}
    for pi,prim in enumerate(data['prims']):
        xyz=np.array(prim['bakedPos']).reshape(-1,3);xyz=np.stack((-xyz[:,2],xyz[:,1],-xyz[:,0]),axis=1)*.01
        uv=np.array(prim['uv']).reshape(-1,2);uv[:,1]=1-uv[:,1]
        colors=np.array(prim['color']).reshape(-1,4)/255 if prim.get('vertexSemantics')=='color' else np.ones((len(xyz),4))
        node=np.array(prim.get('nodeColor',[255]*4))/255
        tri=np.array(prim['idx']).reshape(-1,3)[:,[0,2,1]]
        indices=[]
        for face in tri:
            p=xyz[face];normal=np.cross(p[1]-p[0],p[2]-p[0]);length=np.linalg.norm(normal)
            if length<1e-8:continue
            normal/=length
            if np.max(p[:,1])-np.min(p[:,1])<.001 and abs(float(p[0,1]))<.4:
                y=round(float(p[0,1]),3);planes[y]=planes.get(y,0)+length*.5
            for k in face:
                indices.append(len(verts));verts.append(dict(p=xyz[k].tolist(),n=normal.tolist(),uv=uv[k].tolist(),color=colors[k].tolist()))
        texture=normalmap=''
        tex=prim.get('tex',-1)
        if tex>=0:
            source=folder/data['textures'][tex]['image'];texture,normalmap,_=namespace['prepare_texture'](source,None,key)
        sampler=prim.get('sampler',{})
        groups.append(dict(name=f'Native batch {pi:03d}',texture=texture,normalTexture=normalmap,rgba=node.tolist(),triangles=indices,
            alphaMode=prim.get('alphaMode','opaque'),wrapU=int(sampler.get('cms',0)),wrapV=int(sampler.get('cmt',0))))
    floor=max(planes,key=planes.get) if planes else 0
    obj=dict(key=key,name=name,sourceUrl=f'https://models.spriters-resource.com/nintendo_64/pokemonstadium2/asset/{asset}/' if asset else 'Local NP3F ROM, arena '+str(index),floor=floor,scale=.01,vertices=verts,groups=groups)
    target=OUT/key/'mesh.json';target.parent.mkdir(exist_ok=True);target.write_text(json.dumps(obj,separators=(',',':'),ensure_ascii=False),encoding='utf-8')
    report.append(dict(index=index,key=key,name=name,floor=floor,triangles=sum(len(g['triangles'])//3 for g in groups),vertices=len(verts),groups=len(groups),sourceSha256=hashlib.sha256((folder/'model.json').read_bytes()).hexdigest()))
    print('Prepared',index,key,len(verts),flush=True)
(OUT/'manifest.json').write_text(json.dumps(dict(arenas=report,textures=namespace['texture_records'],method='Static ROM geometry, true native vertex colors, original UVs, uniform 0.01 scale. Source-derived resampling and relief; not AI-recovered detail.'),indent=2,ensure_ascii=False),encoding='utf-8')
assert len(report)==30
print('COMPLETE',len(report),'arenas',sum(r['triangles'] for r in report),'nondegenerate triangles')
