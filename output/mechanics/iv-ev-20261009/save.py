from pathlib import Path
import shutil,json,hashlib
repo=Path(r'C:\Users\dofus\Documents\GitHub\AeroStadium')
work=Path(__file__).resolve().parent
proof=repo/'output/mechanics/iv-ev-20261009'
tests=(repo/'Builds/Reports/core-checks.log').read_text(encoding='utf-8-sig')
assert '20/20 checks passed.' in tests
unity=(repo/'Logs/iv-ev-unity-validation-20261009.log').read_text(encoding='utf-8-sig')
assert 'Catalog validated: 151 species' in unity and 'return code 0' in unity
for name in ['implement.py','save.py']:
 shutil.copy2(work/name,proof/name)
shutil.copy2(repo/'Builds/Reports/core-checks.log',proof/'core-checks.log')
doc='''# IV et EV par Pokémon

Les membres des équipes portent désormais leurs propres `ivs` et `evs`, sous forme de six valeurs : hp, attack, defense, specialAttack, specialDefense et speed. Deux Pokémon de la même espèce peuvent donc avoir des caractéristiques différentes.

IV :0 à31 par statistique. EV :0 à252 par statistique, total maximum510. Les profils illégaux sont rejetés à la création de la bataille, sans correction silencieuse. Seul un profil null/absent déclenche le défaut IV31/EV0 ; un IV explicitement nul est conservé. Les valeurs sont copiées au début du combat et les propriétés de lecture renvoient des copies.

Calcul pour le niveau L : plancher((2×base+IV+plancher(EV/4))×L/100), puis +L+10 pour les PV, +5 pour les autres statistiques. Les natures restent neutres. Les six valeurs calculées alimentent les dégâts physiques/spéciaux, les PV et la Vitesse après priorité des capacités.

Exemple de membre d’équipe en C# :

```csharp
new TeamMember(6, "none") {
    ivs = new StatValues(31),
    evs = new StatValues { hp = 4, specialAttack = 252, speed = 252 }
};
```

Au niveau50, ce Dracaufeu possède154PV,104Attaque,98Défense,161AttaqueSpéciale,105DéfenseSpéciale et152Vitesse. Les anciennes équipes sans profil conservent leur comportement précédent. Les champs sont sérialisables pour les futures sauvegardes et équipes.

Intégration actuelle : paramétrage de l’équipe et calcul du combat. Le prototype ne possède pas encore de menu d’édition des IV/EV ni de progression persistante qui distribue automatiquement des EV après un K.O. Le moteur ne génère pas aléatoirement les IV à chaque lancement.

Validation :20/20 contrôles du moteur réussis, avec limites, total510/511, IV0, arrondis3/4EV, valeurs de référence Dracaufeu, isolation des profils, sérialisation, influence des dégâts et de l’initiative. Compilation du code et lecture du catalogue Unity réussies (sortie0). Aucun nouveau Windows.exe construit à cette étape. Rapports : output/mechanics/iv-ev-20261009/core-checks.log et Logs/iv-ev-unity-validation-20261009.log.

Référence primaire consultée : code de [Pokémon Showdown](https://github.com/smogon/pokemon-showdown), notamment [pokemon.ts](https://github.com/smogon/pokemon-showdown/blob/master/sim/pokemon.ts) pour les profils individuels et [team-validator.ts](https://github.com/smogon/pokemon-showdown/blob/master/sim/team-validator.ts) pour les contrôles des équipes. L’implémentation C# et ses tests sont propres à AeroStadium.
'''
(repo/'docs/IV_EV.md').write_text(doc,encoding='utf-8')
path=repo/'docs/KANTO_BASE_STATS.md'
text=path.read_text(encoding='utf-8')
text=text.replace('Le prototype utilise actuellement IV31, EV0 et nature neutre.','Les IV/EV sont désormais propres à chaque Pokémon : IV31/EV0 par défaut, profils configurables et nature neutre. Voir [IV_EV.md](IV_EV.md).')
path.write_text(text,encoding='utf-8')
result={'core_checks_passed':20,'core_checks_total':20,'unity_compilation_exit':0,'limits':{'iv_per_stat':[0,31],'ev_per_stat':[0,252],'ev_total':510},'windows_build_updated':False,'source_backup':'before','status':'integrated_and_validated_in_unity_project','credits_balance_last_observed':'272.2102050000'}
(proof/'validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
journal=repo/'LastProgressLogs.md'
text=journal.read_text(encoding='utf-8-sig')
heading='## 2026-10-09 — IV/EV individuels intégrés au moteur'
entry='''- TeamMember porte des profils sérialisables ivs/evs à six valeurs. Null/absent conserve IV31/EV0, IV0 explicite respecté. IV0–31, EV0–252 par stat, totalEV≤510 ; valeurs invalides rejetées. Snapshots indépendants au début du combat, lectures copiées : aucune mutation extérieure ni partage entre combattants.
- Calcul standard avec plancherEV/4 avant multiplication par le niveau ; PV et cinq autres caractéristiques utilisent leurs IV/EV distincts. Les dégâts physiques/spéciaux et l’initiative utilisent ces caractéristiques. Natures neutres conservées. La simulation actuelle utilise encore les profils par défaut ; éditeur de profils et progression automatique des EV non ajoutés.
-20/20 tests du moteur passent, sortie0 : références Dracaufeu niveau50, limites, EV510 accepté/511 rejeté, arrondi3/4EV, IV0, isolation et JSON, dégâts physiques/spéciaux, initiative, tests antérieurs et catalogue151. Rapport copié dans output/mechanics/iv-ev-20261009/core-checks.log.
- Compilation Unity et validation du catalogue réussies, sortie0 : Logs/iv-ev-unity-validation-20261009.log. Réglages restaurés après commande. Pas de runtime ni capture écran, pas de nouveau .exe construit. Sources et tests précédents sauvegardés dans output/mechanics/iv-ev-20261009/before ; documentation docs/IV_EV.md et docs/KANTO_BASE_STATS.md actualisées.
- Sources consultées : implémentation Pokémon Showdown sim/pokemon.ts et sim/team-validator.ts ; implémentation C# et tests propres au projet. Aucun commit/push. Dernière lecture quota78%/47%, solde272.2102050000 inchangé ; aucun crédit supplémentaire autorisé ou observé consommé.
'''.replace('\n+','\n')
if heading not in text:
 first,rest=text.split('\n',1)
 journal.write_text(first+'\n\n'+heading+'\n\n'+entry+'\n'+rest.lstrip(),encoding='utf-8')
print('IV/EV documentation, verification and progress saved.')
