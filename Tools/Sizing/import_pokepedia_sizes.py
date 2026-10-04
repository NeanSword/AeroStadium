"""Extract factual heights from the user's Pokepedia table, preserving forms.

Uses only the Python standard library. Input is a saved UTF-8 HTML page.
No model, animation or source height is modified by this tool.
"""
from pathlib import Path
from html.parser import HTMLParser
import argparse, csv, hashlib, json, re
from datetime import datetime, timezone

SOURCE = 'https://www.pokepedia.fr/Liste_des_Pokémon_par_données_du_Pokédex'
BANDS = [(0.4,0.75,'Très petit'),(0.7,0.95,'Petit'),(1.0,1.2,'Compact'),
         (1.5,1.55,'Moyen'),(2.0,1.95,'Grand'),(3.0,2.4,'Très grand'),
         (5.0,2.85,'Massif'),(10000,3.3,'Colossal')]

class Tables(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.depth=0;self.tables=[];self.rows=None;self.row=None;self.cell=None
    def handle_starttag(self,tag,attrs):
        attrs=dict(attrs)
        if tag=='table':
            self.depth+=1
            if self.depth==1:self.rows=[]
        if self.depth!=1:return
        if tag=='tr':self.row=[]
        if tag in ('td','th') and self.row is not None:
            # Source contains a malformed rowspan="2\""; browsers recover it as 2.
            self.cell={'text':[],'rowspan':int(str(attrs.get('rowspan',1)).strip('"')),'colspan':int(str(attrs.get('colspan',1)).strip('"'))}
        if tag=='br' and self.cell is not None:self.cell['text'].append(' ')
    def handle_data(self,data):
        if self.depth==1 and self.cell is not None:self.cell['text'].append(data)
    def handle_endtag(self,tag):
        if self.depth==1:
            if tag in ('td','th') and self.cell is not None:
                self.cell['text']=' '.join(''.join(self.cell['text']).split())
                self.row.append(self.cell);self.cell=None
            if tag=='tr' and self.row is not None:
                self.rows.append(self.row);self.row=None
            if tag=='table':self.tables.append(self.rows);self.rows=None
        if tag=='table':self.depth-=1

def expand(rows):
    pending={};grid=[]
    for cells in rows:
        row={col:value for col,(count,value) in pending.items()}
        following={col:(count-1,value) for col,(count,value) in pending.items() if count>1}
        col=0
        for cell in cells:
            while col in row:col+=1
            for offset in range(cell['colspan']):
                c=col+offset;assert c not in row,'Overlapping table cell'
                row[c]=cell['text']
                if cell['rowspan']>1:following[c]=(cell['rowspan']-1,cell['text'])
            col+=cell['colspan']
        grid.append([row.get(i,'') for i in range(max(row,default=-1)+1)])
        pending=following
    return grid

def extract(source):
    parser=Tables();parser.feed(source.read_text(encoding='utf-8'))
    matches=[expand(t) for t in parser.tables if t and 'Taille' in [c['text'] for c in t[0]] and 'Numéro' in [c['text'] for c in t[0]]]
    assert len(matches)==1,'Expected exactly one Pokedex table'
    table=matches[0];header=table[0]
    ix={x:header.index(x) for x in ('Numéro','Nom','Taille')}
    forms=[];excluded=[];unassigned=[];seen=set()
    for row in table[1:]:
        assert len(row)==len(header),(len(row),row[:3])
        number,name,size=(row[ix[k]] for k in ('Numéro','Nom','Taille'))
        if re.search(r'dynamax|gigamax|infinimax',name,re.I):
            excluded.append({'number':number,'name':name,'rawHeight':size});continue
        match=re.fullmatch(r'(\d+(?:[,.]\d+)?)\s*m',size)
        assert match,('Unexpected non-excluded size',number,name,size)
        height=float(match.group(1).replace(',','.'));assert 0<height<1000
        if not re.fullmatch(r'\d{4}',number):
            unassigned.append({'name':name,'heightMetres':height,'reason':'No assigned national Pokedex number in the source'});continue
        number=int(number);key=(number,name,height)
        if key in seen:continue
        seen.add(key)
        forms.append({'species':number,'name':name,'heightMetres':height})
    base={}
    # First non-excluded table entry is the species' source default form.
    # Alternate forms stay separate rather than overwriting that default.
    for form in forms:base.setdefault(form['species'],form)
    assert set(base)==set(range(1,max(base)+1)),'National catalogue has gaps'
    assert max(base)>=1025 and len(base)==max(base)
    assert base[31]['heightMetres']==1.3 and base[34]['heightMetres']==1.4
    assert base[50]['heightMetres']==.2 and base[95]['heightMetres']==8.8
    return table,forms,list(base.values()),excluded,unassigned

def run():
    p=argparse.ArgumentParser();p.add_argument('html',type=Path);p.add_argument('output',type=Path);a=p.parse_args()
    table,forms,species,excluded,unassigned=extract(a.html)
    a.output.mkdir(parents=True,exist_ok=True)
    data={'schemaVersion':1,'sourceUrl':SOURCE,'retrievedUtc':datetime.now(timezone.utc).isoformat(),
          'sourceSha256':hashlib.sha256(a.html.read_bytes()).hexdigest(),
          'species':species,'forms':forms,'unassigned':unassigned}
    (a.output/'pokemon-heights.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    policy={'schemaVersion':1,'maximumHorizontalMetres':5.5,'bands':[
        {'maximumPokedexMetres':m,'displayMetres':d,'name':n} for m,d,n in BANDS]}
    (a.output/'pokemon-display-size-policy.json').write_text(json.dumps(policy,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    with (a.output/'pokemon-size-preview.csv').open('w',newline='',encoding='utf-8-sig') as f:
        w=csv.writer(f);w.writerow(['NationalDex','FrenchName','PokedexMetres','DisplayGroup','DisplayMetres'])
        for entry in species:
            band=next(b for b in BANDS if entry['heightMetres']<=b[0])
            w.writerow([entry['species'],entry['name'],entry['heightMetres'],band[2],band[1]])
    report={'rows':len(table)-1,'species':len(species),'forms':len(forms),'excludedDynamaxGigamax':excluded,
            'unassigned':unassigned,'baseKantoCount':sum(x['species']<=151 for x in species)}
    (a.output/'extraction-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in ('excludedDynamaxGigamax','unassigned')}|{'excluded':len(excluded),'unassigned':len(unassigned)}))

if __name__=='__main__':run()
