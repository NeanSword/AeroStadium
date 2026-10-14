#!/usr/bin/env python3
"""Rebuild AeroStadium's catalog from pinned public data. Python stdlib only.

SV+DLC and Legends Z-A ordinary learnsets are unioned per species. Missing
species use Champions, then Legends Arceus, BDSP, or Sword/Shield, in that order.
Historical transfer moves are never added merely because Showdown lists them.
"""
from __future__ import annotations
import argparse, collections, concurrent.futures, csv, hashlib, json, pathlib, re, urllib.request

POKEAPI_COMMIT = "c80757193bd0889054e36f4209762360bdaa4b95"
SHOWDOWN_COMMIT = "942cda71d14bf0618bd57991708a2e2734b6daba"
CSV_FILES = "moves move_names move_flavor_text move_meta move_meta_stat_changes move_effect_prose move_targets move_meta_ailments move_meta_categories move_damage_classes types items item_names item_flavor_text item_categories item_flag_map item_flags item_prose version_groups generations pokemon pokemon_species pokemon_moves item_game_indices".split()
TS_FILES = ["data/learnsets.ts", "data/pokedex.ts", "data/moves.ts", "data/items.ts", "data/mods/gen9legends/learnsets.ts", "data/mods/gen8bdsp/learnsets.ts", "data/mods/champions/learnsets.ts", "sim/global-types.ts"]
STAT = {2:"attack",3:"defense",4:"specialAttack",5:"specialDefense",6:"speed",7:"accuracy",8:"evasion"}
PS_STAT = {"atk":"attack","def":"defense","spa":"specialAttack","spd":"specialDefense","spe":"speed","accuracy":"accuracy","evasion":"evasion"}
AILMENT = {"brn":"burn","par":"paralysis","psn":"poison","tox":"toxic","slp":"sleep","frz":"freeze"}
TYPE_BOOST = {"black-belt":"Fighting","black-glasses":"Dark","charcoal":"Fire","dragon-fang":"Dragon","hard-stone":"Rock","magnet":"Electric","metal-coat":"Steel","miracle-seed":"Grass","mystic-water":"Water","never-melt-ice":"Ice","poison-barb":"Poison","sharp-beak":"Flying","silk-scarf":"Normal","silver-powder":"Bug","soft-sand":"Ground","spell-tag":"Ghost","twisted-spoon":"Psychic","fairy-feather":"Fairy"}
PLATE_TYPES = dict(zip("fist sky toxic earth stone insect spooky iron flame splash meadow zap icicle draco dread pixie mind".split(), "Fighting Flying Poison Ground Rock Bug Ghost Steel Fire Water Grass Electric Ice Dragon Dark Fairy Psychic".split()))

def excluded_item_family(slug, category, ps_items):
    # Both bag and held Z-Crystal records represent the same excluded crystal.
    canonical = re.sub(r"--(?:held|bag)$", "", slug)
    block = ps_items.get(slug_key(canonical), "")
    if category == "mega-stones" or re.search(r"^\t\tmegaStone:", block, re.M):
        return "mega-stones"
    if (category == "z-crystals" or re.search(r"^\t\tzMove:", block, re.M)
            or slug == "ilimas-normalium-z"):
        return "z-crystals"
    return None

def dump(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")

def fetch_sources(cache, offline=False):
    tasks = [(f+".csv", f"https://raw.githubusercontent.com/PokeAPI/pokeapi/{POKEAPI_COMMIT}/data/v2/csv/{f}.csv") for f in CSV_FILES]
    tasks += [("showdown-"+f.replace("/","-"), f"https://raw.githubusercontent.com/smogon/pokemon-showdown/{SHOWDOWN_COMMIT}/{f}") for f in TS_FILES]
    cache.mkdir(parents=True, exist_ok=True)
    def fetch(task):
        name,url=task; dst=cache/name
        if not dst.exists():
            if offline: raise FileNotFoundError(dst)
            req=urllib.request.Request(url, headers={"User-Agent":"AeroStadium-reproducible-catalog-import"})
            dst.write_bytes(urllib.request.urlopen(req,timeout=90).read())
        return {"file":name,"url":url,"sha256":hashlib.sha256(dst.read_bytes()).hexdigest(),"bytes":dst.stat().st_size}
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        manifest=list(pool.map(fetch,tasks))
    dump(cache/"sources.json", {"pokeapiCommit":POKEAPI_COMMIT,"showdownCommit":SHOWDOWN_COMMIT,"files":manifest})
    return manifest

def ts_table(path):
    # These data tables indent their top-level records with exactly one tab.
    return dict(re.findall(r"^\t([a-z0-9]+): \{\n(.*?)^\t\},", path.read_text(encoding="utf-8"), re.M|re.S))

def int_value(value, default=0): return int(value) if value not in (None,"") else default
def ps_value(block,key,default=None):
    m=re.search(r"^\t\t"+re.escape(key)+r": ([^,\n]+)",block,re.M)
    if not m:return default
    v=m.group(1).strip()
    if v in ("true","false"):return v=="true"
    if re.fullmatch(r"-?\d+",v):return int(v)
    if len(v)>1 and v[0] in "\"'" and v[-1]==v[0]:return v[1:-1]
    return default

def learn_sources(block):
    return {k:json.loads(v) for k,v in re.findall(r"^\t\t\t([a-z0-9]+): (\[[^\n]+\])",block,re.M)}

def slug_key(s): return re.sub("[^a-z0-9]","",s.lower())

def localized(rows,id_key,text_key,version_key=None):
    result={}; ranks={}
    for r in rows:
        if r.get("local_language_id",r.get("language_id"))!="5":continue
        n=int(r[id_key]); rank=int_value(r.get(version_key),0) if version_key else 0
        if n not in result or rank>=ranks[n]:
            result[n]=" ".join(r[text_key].replace("\f"," ").split());ranks[n]=rank
    return result

def build(repo,cache,output,apply=False,offline=False):
    manifest=fetch_sources(cache,offline)
    def rows(name):return list(csv.DictReader((cache/(name+".csv")).open(encoding="utf-8")))
    old_path=repo/"Assets/AeroStadium/Resources/Data/catalog.json"
    old=json.loads(old_path.read_text(encoding="utf-8-sig"))
    baseline_path=output/"catalog-before.json"
    baseline=json.loads(baseline_path.read_text(encoding="utf-8")) if baseline_path.exists() else old
    original_defaults={s["id"]:s["moves"] for s in baseline["species"]}
    if len(old["species"])!=151 or {s["id"]for s in old["species"]}!=set(range(1,152)):raise ValueError("Expected the existing Kanto 151 species")
    moves_rows=rows("moves"); move_rows={int(x["id"]):x for x in moves_rows if int(x["id"])<10000}
    move_keys={slug_key(x["identifier"]):i for i,x in move_rows.items()}
    move_names=localized(rows("move_names"),"move_id","name")
    move_text=localized(rows("move_flavor_text"),"move_id","flavor_text","version_group_id")
    item_names=localized(rows("item_names"),"item_id","name")
    item_text=localized(rows("item_flavor_text"),"item_id","flavor_text","version_group_id")
    type_by_id={int(x["id"]):x["identifier"].title() for x in rows("types")}
    target_by_id={int(x["id"]):x["identifier"] for x in rows("move_targets")}
    meta={int(x["move_id"]):x for x in rows("move_meta")}
    ailments={int(x["id"]):x["identifier"]for x in rows("move_meta_ailments")}
    changes=collections.defaultdict(list)
    for x in rows("move_meta_stat_changes"):
        changes[int(x["move_id"])].append({"stat":STAT.get(int(x["stat_id"]),"unknown"),"change":int(x["change"])})
    ps_moves=ts_table(cache/"showdown-data-moves.ts")
    ps_items=ts_table(cache/"showdown-data-items.ts")
    pokedex=ts_table(cache/"showdown-data-pokedex.ts")
    # Form records share National Dex numbers, so take base species only.
    species_keys={ps_value(b,"num"):k for k,b in pokedex.items() if isinstance(ps_value(b,"num"),int) and 1<=ps_value(b,"num")<=151 and "baseSpecies:"not in b}
    if len(species_keys)!=151:raise ValueError("Base species mapping is incomplete")
    name_keys={slug_key(ps_value(b,"name",k)):k for k,b in pokedex.items()}
    main_ls=ts_table(cache/"showdown-data-learnsets.ts")
    za_ls=ts_table(cache/"showdown-data-mods-gen9legends-learnsets.ts")
    champion_ls=ts_table(cache/"showdown-data-mods-champions-learnsets.ts")
    bdsp_ls=ts_table(cache/"showdown-data-mods-gen8bdsp-learnsets.ts")
    csv_learn=collections.defaultdict(lambda:collections.defaultdict(dict))
    for x in rows("pokemon_moves"):
        n=int(x["pokemon_id"]);g=int(x["version_group_id"])
        if n<=151 and g in (20,23,24,25,26,27,30,31,32):
            mid=int(x["move_id"]);csv_learn[g][n].setdefault(mid,[]).append({"methodId":int(x["pokemon_move_method_id"]),"level":int(x["level"])})
    unknown_moves=[];evidence=[];coverage=collections.Counter()
    def lineage(key):
        chain=[];seen=set()
        while key and key not in seen:
            seen.add(key);chain.append(key)
            p=ps_value(pokedex.get(key,""),"prevo","")
            key=name_keys.get(slug_key(p)) if p else None
        return chain
    def add_table(found,key,table,game,generation):
        for origin in lineage(key):
            for mk,sources in learn_sources(table.get(origin,"")).items():
                kept=[s for s in sources if s.startswith(str(generation))]
                if not kept:continue
                mid=move_keys.get(mk)
                if mid is None:unknown_moves.append({"species":key,"origin":origin,"game":game,"move":mk});continue
                found.setdefault(mid,[]).append({"game":game,"origin":origin,"sources":kept,"inherited":origin!=key})
    for species in sorted(old["species"],key=lambda s:s["id"]):
        n=species["id"]; key=species_keys[n];found={}
        add_table(found,key,main_ls,"scarlet-violet-dlc",9)
        add_table(found,key,za_ls,"legends-za-mega-dimension",9)
        games=sorted({e["game"] for es in found.values()for e in es})
        if not found:
            add_table(found,key,champion_ls,"champions",9)
            games=sorted({e["game"] for es in found.values()for e in es})
        if not found and csv_learn[24].get(n):
            for mid,methods in csv_learn[24][n].items():found[mid]=[{"game":"legends-arceus","origin":key,"methods":methods,"inherited":False}]
            games=["legends-arceus"]
        if not found:
            add_table(found,key,bdsp_ls,"brilliant-diamond-shining-pearl",8)
            games=["brilliant-diamond-shining-pearl"] if found else []
        if not found:
            for mid,methods in csv_learn[20].get(n,{}).items():found[mid]=[{"game":"sword-shield","origin":key,"methods":methods,"inherited":False}]
            games=["sword-shield"] if found else []
        if not found:raise ValueError(f"No learnset for {n} {key}")
        # Struggle and powered battle variants cannot be taught as ordinary moves.
        filtered=[]
        for mid in found:
            block=ps_moves.get(slug_key(move_rows[mid]["identifier"]),"")
            if mid!=165 and not ps_value(block,"isZ",False) and not ps_value(block,"isMax",False):filtered.append(mid)
        species["learnableMoves"]=sorted(filtered)
        species["learnsetVersion"]=" + ".join(games)
        for g in games:coverage[g]+=1
        evidence.append({"speciesId":n,"slug":key,"versions":games,"lineage":lineage(key),"moves":[{"moveId":mid,"sources":found[mid]}for mid in sorted(filtered)]})
    if unknown_moves:raise ValueError("Unknown canonical moves: "+str(unknown_moves[:20]))
    legal_union={mid for s in old["species"]for mid in s["learnableMoves"]}
    moves=[];effect_audit=[]
    for mid,row in sorted(move_rows.items()):
        slug=row["identifier"];block=ps_moves.get(slug_key(slug),"");m=meta.get(mid,{})
        category={1:"Status",2:"Physical",3:"Special"}[int(row["damage_class_id"])]
        power=ps_value(block,"basePower",int_value(row["power"]))
        accuracy=ps_value(block,"accuracy",int_value(row["accuracy"]))
        if accuracy is True:accuracy=0
        elif accuracy is False:accuracy=0
        target=ps_value(block,"target",target_by_id.get(int(row["target_id"]),"selected-pokemon"))
        ailment=ailments.get(int_value(m.get("meta_ailment_id")),"")
        if ailment=="none":ailment=""
        status=re.search(r"(?:^\t\tstatus:|^\t\t\tstatus:) ['\"]([a-z]+)['\"]",block,re.M)
        if status:ailment=AILMENT.get(status.group(1),ailment)
        mechanics={"support":"implemented","ailment":ailment,"ailmentChance":int_value(m.get("ailment_chance")),"flinchChance":int_value(m.get("flinch_chance")),"statChance":int_value(m.get("stat_chance")),"statChanges":changes[mid],"healing":int_value(m.get("healing")),"drain":int_value(m.get("drain")),"minHits":int_value(m.get("min_hits"),1),"maxHits":int_value(m.get("max_hits"),1),"minTurns":int_value(m.get("min_turns")),"maxTurns":int_value(m.get("max_turns")),"fixedDamage":0,"fixedDamageKind":"","selfTarget":target in ("self","user") or int_value(m.get("meta_category_id"))==7,"critRate":ps_value(block,"critRatio",int_value(m.get("crit_rate"))+1)-1,"contact":bool(re.search(r"\bcontact: 1",block))}
        # Showdown is current; CSV metadata still omits many post-generation-7 effects.
        boost=re.search(r"\bboosts: \{([^}]+)\}",block)
        if boost:
            mechanics["statChanges"]=[{"stat":PS_STAT[a],"change":int(v)}for a,v in re.findall(r"(atk|def|spa|spd|spe|accuracy|evasion): (-?\d+)",boost.group(1))]
            if "self:"in block[:boost.start()]:mechanics["selfTarget"]=True
        chance=re.search(r"\bchance: (\d+)",block)
        if chance:
            if status:mechanics["ailmentChance"]=int(chance.group(1))
            if boost:mechanics["statChance"]=int(chance.group(1))
            if "volatileStatus: 'flinch'"in block:mechanics["flinchChance"]=int(chance.group(1))
        for field in ("heal","drain","recoil"):
            ratio=re.search(r"^\t\t"+field+r": \[(\d+), (\d+)\]",block,re.M)
            if ratio:mechanics["healing"if field=="heal"else"drain"]=round(100*int(ratio.group(1))/int(ratio.group(2)))*(-1 if field=="recoil"else 1)
        fixed=ps_value(block,"damage")
        if isinstance(fixed,int):mechanics["fixedDamage"]=fixed
        elif fixed=="level":mechanics["fixedDamageKind"]="level"
        if slug in ("super-fang","natures-madness","ruination"):mechanics["fixedDamageKind"]="half-hp"
        if mechanics["drain"]==-33:mechanics["drain"]=-33
        effect="metadata"
        if slug=="protect":effect="Protect"
        elif slug=="transform":effect="transform"
        elif slug=="splash":effect="splash"
        # Callback based moves need bespoke behavior. Do not present them as complete.
        callback=bool(re.search(r"^\t\t(?:on[A-Z]\w*|basePowerCallback|damageCallback|condition)\b",block,re.M))
        special=bool(re.search(r"^\t\t(?:volatileStatus|sideCondition|pseudoWeather|weather|terrain|forceSwitch|selfSwitch|selfdestruct|overrideDefensiveStat|overrideOffensiveStat|ignoreDefensive|ignoreOffensive|ignoreImmunity|willCrit|mindBlownRecoil|stealsBoosts|breaksProtect|onTryImmunity)\b",block,re.M))
        family=bool(mechanics["statChanges"]or mechanics["healing"]or mechanics["ailment"]in AILMENT.values())
        if ailment and ailment not in AILMENT.values():special=True
        if mechanics["minTurns"] or mechanics["maxTurns"]:special=True
        if category=="Status" and not family and effect not in ("Protect","splash"):mechanics["support"]="unsupported"
        elif callback or special:mechanics["support"]="partial"if category!="Status"and(power>0 or mechanics["fixedDamage"]or mechanics["fixedDamageKind"])else"unsupported"
        if effect in ("Protect","splash") or mechanics["fixedDamage"] or mechanics["fixedDamageKind"]:mechanics["support"]="implemented"
        if effect=="transform":mechanics["support"]="partial"
        if int_value(m.get("meta_category_id"))==9:mechanics["support"]="unsupported"
        if category!="Status"and power==0 and not mechanics["fixedDamage"]and not mechanics["fixedDamageKind"]:mechanics["support"]="unsupported"
        powered=bool(ps_value(block,"isZ",False)or ps_value(block,"isMax",False))
        nonstandard=ps_value(block,"isNonstandard","")
        availability="powered-variant"if powered else ("modern-learnset"if mid in legal_union else("legacy"if nonstandard else"current"))
        if powered:mechanics["support"]="unsupported"
        moves.append({"id":mid,"slug":slug,"name":move_names.get(mid,slug),"description":move_text.get(mid,""),"type":ps_value(block,"type",type_by_id[int(row["type_id"])]),"category":category,"power":power,"accuracy":accuracy,"pp":ps_value(block,"pp",int_value(row["pp"],1)),"priority":ps_value(block,"priority",int_value(row["priority"])),"effect":effect,"target":target,"availability":availability,"supported":mechanics["support"]!="unsupported","generation":int(row["generation_id"]),"mechanics":mechanics})
        effect_audit.append({"id":mid,"slug":slug,"csvEffectId":int_value(row["effect_id"]),"csvMetaCategory":int_value(m.get("meta_category_id"),-1),"support":mechanics["support"],"callback":callback,"special":special})
    by_mid={m["id"]:m for m in moves}
    default_changes=[]
    for species in old["species"]:
        legal=species["learnableMoves"]
        candidates=[by_mid[mid]for mid in legal if by_mid[mid]["mechanics"]["support"]=="implemented"]
        damaging=[m for m in candidates if m["category"]!="Status"]
        strongest="Physical"if species["stats"]["attack"]>=species["stats"]["specialAttack"]else"Special"
        def rank(m):return (m["type"]in species["types"],m["category"]==strongest,m["power"]*(m["accuracy"] or 100)/100,m["pp"],-m["id"])
        chosen=[];types=set()
        for m in sorted(damaging,key=rank,reverse=True):
            if m["type"] not in types:chosen.append(m["id"]);types.add(m["type"])
            if len(chosen)==4:break
        for m in sorted(candidates,key=lambda m:(m["category"]!="Status",rank(m)),reverse=True):
            if len(chosen)==4:break
            if m["id"]not in chosen:chosen.append(m["id"])
        if not chosen:chosen=legal[:4]
        if chosen!=original_defaults[species["id"]]:default_changes.append({"speciesId":species["id"],"old":original_defaults[species["id"]],"new":chosen})
        species["moves"]=chosen
    categories={int(x["id"]):x["identifier"]for x in rows("item_categories")}
    flags=collections.defaultdict(set)
    for x in rows("item_flag_map"):flags[int(x["item_id"])].add(int(x["item_flag_id"]))
    old_item_ids={"life-orb":"lifeorb"} # Existing saved teams use this ID.
    items=[{"id":"none","slug":"none","dexId":0,"name":"Aucun objet","description":"Aucun objet tenu.","category":"none","effect":"none","type":"","multiplier":1,"holdable":True,"availability":"current","support":"implemented","recoverPercent":0,"fixedHeal":0,"cureStatus":""}]
    item_rows=rows("items")
    missing_french=[]
    excluded_items=[]
    for row in item_rows:
        iid=int(row["id"]);slug=row["identifier"];block=ps_items.get(slug_key(slug),"");category=categories[int(row["category_id"])]
        excluded_family=excluded_item_family(slug, category, ps_items)
        if excluded_family:
            excluded_items.append({"dexId":iid,"slug":slug,"name":item_names.get(iid,slug),"family":excluded_family})
            continue
        # Holdable passive/active flags are sparse for newer generations; the
        # current simulator's held-item records supply the missing classification.
        holdable=bool(flags[iid]&{5,6,7})or bool(block and not ps_value(block,"isPokeball",False))or category in ("held-items","choice","bad-held-items","plates","species-specific","type-enhancement","jewels","mega-stones","memories")
        availability="legacy"if ps_value(block,"isNonstandard","")else("current"if block else"catalog")
        effect="none";support="unsupported";itype="";mult=1;recover=0;fixed=0;cure=""
        if slug=="leftovers":effect="leftovers";support="implemented"
        elif slug=="life-orb":effect="lifeorb";mult=1.3;support="implemented"
        elif slug in TYPE_BOOST:effect="typeboost";itype=TYPE_BOOST[slug];mult=1.2;support="implemented"
        elif slug.endswith("-plate")and slug[:-6]in PLATE_TYPES:effect="typeboost";itype=PLATE_TYPES[slug[:-6]];mult=1.2;support="partial"
        elif slug in ("choice-band","choice-specs","choice-scarf","assault-vest","focus-sash","black-sludge","rocky-helmet"):
            effect=slug_key(slug);support="implemented";mult=1.5 if slug.startswith("choice-")or slug=="assault-vest" else 1
        elif slug=="sitrus-berry":effect="sitrusberry";support="implemented";recover=25
        elif slug=="oran-berry":effect="oranberry";support="implemented";fixed=10
        elif slug=="lum-berry":effect="lumberry";support="implemented";cure="all"
        elif slug in ("cheri-berry","chesto-berry","pecha-berry","rawst-berry","aspear-berry"):
            effect="statusberry";support="implemented";cure={"cheri-berry":"paralysis","chesto-berry":"sleep","pecha-berry":"poison","rawst-berry":"burn","aspear-berry":"freeze"}[slug]
        name=item_names.get(iid)
        # The upstream item table repeats Roseli Berry with a new ID but omits
        # its existing French localization on that duplicate row.
        if not name:
            earlier=next((i for i in items if i["slug"]==slug),None)
            if earlier:name=earlier["name"]
        if not name and slug=="hopo-berry":name="Baie Hopo"
        if not name:
            missing_french.append({"id":iid,"slug":slug})
            name=ps_value(block,"name",slug.replace("-"," ").title())
        item_id=old_item_ids.get(slug,slug)
        if any(i["id"]==item_id for i in items):item_id=f"{item_id}-dex-{iid}"
        items.append({"id":item_id,"slug":slug,"dexId":iid,"name":name,"description":item_text.get(iid,""),"category":category,"effect":effect,"type":itype,"multiplier":mult,"holdable":holdable,"availability":availability,"support":support,"recoverPercent":recover,"fixedHeal":fixed,"cureStatus":cure})
    old["moves"]=moves;old["items"]=items
    old["schemaVersion"]=2
    old["itemPolicy"]={"excludedFamilies":["mega-stones","z-crystals"],"includeBagVariants":True}
    old["sources"]={"pokeapiCommit":POKEAPI_COMMIT,"showdownCommit":SHOWDOWN_COMMIT,"learnsetPolicy":"SV+DLC union Legends Z-A+Mega Dimension; current-game pre-evolution inheritance; latest explicit fallback; ordinary moves only"}
    validate(old,evidence)
    assert not any(excluded_item_family(i["slug"],i["category"],ps_items) for i in items)
    old_preserved=json.loads(old_path.read_text(encoding="utf-8-sig"))
    immutable=("id","name","types","stats","color","height")
    for a,b in zip(sorted(old_preserved["species"],key=lambda s:s["id"]),old["species"]):
        if any(a[k]!=b[k]for k in immutable):raise ValueError("Species attributes changed")
    summary={"passed":True,"date":"2026-10-11","pokeapiCommit":POKEAPI_COMMIT,"showdownCommit":SHOWDOWN_COMMIT,"species":151,"moves":len(moves),"items":len(items)-1,"heldItems":sum(x["holdable"]for x in items)-1,"legalLearnsetEntries":sum(len(s["learnableMoves"])for s in old["species"]),"learnsetCoverage":dict(coverage),"moveSupport":dict(collections.Counter(m["mechanics"]["support"]for m in moves)),"itemSupport":dict(collections.Counter(i["support"]for i in items)),"heldItemSupport":dict(collections.Counter(i["support"]for i in items if i["holdable"]and i["id"]!="none")),"missingFrenchItemNames":missing_french,"excludedShadowMoves":len(moves_rows)-len(moves),"unchangedSpeciesAttributes":list(immutable),"defaultMovesUpdated":len(default_changes)}
    summary["excludedItemFamilies"]=dict(collections.Counter(i["family"] for i in excluded_items))
    summary["excludedItems"]=len(excluded_items)
    summary["sourceItems"]=len(item_rows)
    dump(output/"excluded-items.json",{"policy":old["itemPolicy"],"items":excluded_items})
    dump(output/"catalog.json",old);dump(output/"learnset-evidence.json",{"schemaVersion":1,"pokemon":evidence});dump(output/"import-audit.json",summary);dump(output/"move-support-audit.json",effect_audit);dump(output/"default-move-changes.json",default_changes);dump(output/"sources.json",{"pokeapiCommit":POKEAPI_COMMIT,"showdownCommit":SHOWDOWN_COMMIT,"files":manifest})
    if apply:
        if not(output/"catalog-before.json").exists():dump(output/"catalog-before.json",old_preserved)
        for f in ("catalog.json","learnset-evidence.json"):(old_path.parent/f).write_bytes((output/f).read_bytes())
    print(json.dumps(summary,ensure_ascii=False,indent=2))

def validate(catalog,evidence):
    moves={m["id"]:m for m in catalog["moves"]}
    assert len(moves)==919 and len(catalog["moves"])==919
    assert len({i["id"]for i in catalog["items"]})==len(catalog["items"])
    ev={p["speciesId"]:p for p in evidence}
    for s in catalog["species"]:
        legal=s["learnableMoves"]
        assert legal==sorted(set(legal))and legal
        assert 1<=len(s["moves"])<=4 and set(s["moves"])<=set(legal)
        assert all(mid in moves and moves[mid]["availability"]!="powered-variant"for mid in legal)
        assert set(legal)=={m["moveId"]for m in ev[s["id"]]["moves"]}
        assert all(m["sources"]for m in ev[s["id"]]["moves"])
    assert next(s for s in catalog["species"]if s["id"]==132)["learnableMoves"]==[144]
    assert moves[488]["category"]=="Physical"and moves[53]["category"]=="Special"
    assert 53 not in next(s for s in catalog["species"]if s["id"]==1)["learnableMoves"]
    assert 85 in next(s for s in catalog["species"]if s["id"]==25)["learnableMoves"]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo",type=pathlib.Path,default=pathlib.Path(__file__).resolve().parents[1])
    parser.add_argument("--cache",type=pathlib.Path)
    parser.add_argument("--output",type=pathlib.Path)
    parser.add_argument("--apply",action="store_true",help="Write catalog after validation; preserve previous catalog")
    parser.add_argument("--offline",action="store_true",help="Use pinned cached sources only")
    args=parser.parse_args();out=args.output or args.repo/"output/data/battle-catalog-20261011"
    build(args.repo,args.cache or out/"sources",out,args.apply,args.offline)
if __name__=="__main__":main()
