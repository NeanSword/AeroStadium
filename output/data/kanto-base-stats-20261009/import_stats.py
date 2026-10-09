from pathlib import Path
import csv,io,json,hashlib,urllib.request,datetime,copy

repo=Path(r'C:\Users\dofus\Documents\GitHub\AeroStadium')
root=repo/'output/data/kanto-base-stats-20261009'
root.mkdir(parents=True,exist_ok=True)
headers={'User-Agent':'AeroStadium-data-validation','Accept':'application/vnd.github+json'}
def fetch(url):
 req=urllib.request.Request(url,headers=headers)
 with urllib.request.urlopen(req,timeout=25) as response:return response.read()
commit=json.loads(fetch('https://api.github.com/repos/PokeAPI/pokeapi/commits/master'))['sha']
sources={}
tables={}
for name in ['pokemon.csv','stats.csv','pokemon_stats.csv']:
 url=f'https://raw.githubusercontent.com/PokeAPI/pokeapi/{commit}/data/v2/csv/{name}'
 raw=fetch(url)
 (root/name).write_bytes(raw)
 sources[name]={'url':url,'sha256':hashlib.sha256(raw).hexdigest()}
 tables[name]=list(csv.DictReader(io.StringIO(raw.decode('utf-8-sig'))))
mapping={'hp':'hp','attack':'attack','defense':'defense','special-attack':'specialAttack','special-defense':'specialDefense','speed':'speed'}
stat_ids={int(row['id']):mapping[row['identifier']] for row in tables['stats.csv'] if row['identifier'] in mapping}
assert len(stat_ids)==6
pokemon={int(row['id']):row for row in tables['pokemon.csv'] if 1<=int(row['id'])<=151}
assert set(pokemon)==set(range(1,152))
assert all(int(row['species_id'])==i and row['is_default']=='1' for i,row in pokemon.items())
official={i:{} for i in range(1,152)}
for row in tables['pokemon_stats.csv']:
 i=int(row['pokemon_id']);sid=int(row['stat_id'])
 if i in official and sid in stat_ids:
  key=stat_ids[sid]
  assert key not in official[i]
  value=int(row['base_stat']);assert 1<=value<=255
  official[i][key]=value
assert all(set(stats)==set(mapping.values()) for stats in official.values())
catalog_path=repo/'Assets/AeroStadium/Resources/Data/catalog.json'
raw=catalog_path.read_bytes()
catalog=json.loads(raw.decode('utf-8-sig'))
assert len(catalog['species'])==151 and {s['id'] for s in catalog['species']}==set(range(1,152))
before=copy.deepcopy(catalog)
changes=[]
for species in catalog['species']:
 stats=official[species['id']]
 if species['stats']!=stats:
  changes.append({'id':species['id'],'name':species['name'],'before':species['stats'],'after':stats})
  species['stats']=stats
 (root/f"pokemon-{species['id']:03}.json").write_text(json.dumps({'id':species['id'],'name':species['name'],'source_identifier':pokemon[species['id']]['identifier'],'stats':stats},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
other_before=copy.deepcopy(before);other_after=copy.deepcopy(catalog)
for c in [other_before,other_after]:
 for s in c['species']:s.pop('stats')
assert other_before==other_after
(root/'catalog-before.json').write_bytes(raw)
if changes:catalog_path.write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
assert all(s['stats']==official[s['id']] for s in json.loads(catalog_path.read_text(encoding='utf-8-sig'))['species'])
report={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'source':'PokéAPI database','source_commit':commit,'sources':sources,'policy':'Current base stats; standard default forms of Kanto species1..151; no regional/Mega/Gigantamax variants; not historical generation1stats','species_count':151,'values_checked':906,'changed_species_count':len(changes),'changes':changes,'non_stat_data_unchanged':True,'catalog_sha256_before':hashlib.sha256(raw).hexdigest(),'catalog_sha256_after':hashlib.sha256(catalog_path.read_bytes()).hexdigest()}
(root/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
out=io.StringIO(newline='')
writer=csv.writer(out);writer.writerow(['pokedex','name_fr','name_source','hp','attack','defense','specialAttack','specialDefense','speed','total'])
for s in sorted(catalog['species'],key=lambda s:s['id']):
 stats=official[s['id']]
 writer.writerow([s['id'],s['name'],pokemon[s['id']]['identifier'],*[stats[k] for k in mapping.values()],sum(stats.values())])
(repo/'docs/KANTO_BASE_STATS.csv').write_text(out.getvalue(),encoding='utf-8')
(repo/'docs/KANTO_BASE_STATS.md').write_text(f'''# Statistiques de base des 151 Pokémon de Kanto

Les six statistiques actuelles (PV, Attaque, Défense, Attaque Spéciale, Défense Spéciale, Vitesse) des formes normales sont intégrées dans `Assets/AeroStadium/Resources/Data/catalog.json`. Il ne s’agit pas des anciennes valeurs de Rouge/Bleu : les changements ultérieurs sont conservés. Les variantes régionales, Méga-Évolutions et formes Gigamax ne sont pas utilisées.

Source de données : [PokéAPI](https://pokeapi.co/docs/v2#pokemon), dépôt au commit [{commit}](https://github.com/PokeAPI/pokeapi/tree/{commit}/data/v2/csv). Les trois fichiers CSV source, leurs SHA-256, la copie du catalogue avant vérification et le rapport sont conservés localement dans `output/data/kanto-base-stats-20261009`.

Vérification du 9 octobre 2026 :151 espèces, ID1–151 uniques,906 valeurs comparées ; {len(changes)} espèce(s) modifiée(s). Tous les autres champs du catalogue sont conservés. La liste complète consultable est [KANTO_BASE_STATS.csv](KANTO_BASE_STATS.csv).

Le moteur utilise ces valeurs pour calculer les caractéristiques au niveau choisi. Le prototype utilise actuellement IV31, EV0 et nature neutre. Les dégâts physiques utilisent Attaque/Défense ; les dégâts spéciaux utilisent Attaque Spéciale/Défense Spéciale. La Vitesse contribue à l’initiative après la priorité des capacités.

Les statistiques déjà présentes provenaient de l’import PokéAPI réalisé par `Tools/prepare_generation_one.py`. Cette vérification ne recrée pas les modèles et ne modifie pas les attaques, objets ou tailles.
''',encoding='utf-8')
print(json.dumps({'species':151,'values':906,'changed_species':len(changes),'commit':commit,'changes':changes},ensure_ascii=True))
