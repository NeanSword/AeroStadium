# Tailles du Pokédex et tailles dans l’arène

Source : [liste Poképédia](https://www.pokepedia.fr/Liste_des_Pokémon_par_données_du_Pokédex), récupérée le 4 octobre 2026.

Le fichier `Assets/AeroStadium/Resources/Data/pokemon-heights.json` contient les
tailles de référence des 1 025 espèces numérotées et 1 407 entrées de formes.
Les formes Dynamax, Gigamax et Infinimax sont exclues (35 lignes).
Trois entrées sans numéro national dans la source sont conservées séparément
dans `unassigned` et ne sont pas affectées arbitrairement à une espèce.
Les premières entrées non exclues de chaque numéro définissent sa forme par
défaut ; les autres sont accessibles par leur nom complet dans `forms`.

La taille du Pokédex reste une donnée descriptive. Elle peut être une longueur
chez les Pokémon serpentiformes ; elle n’est pas présentée comme une mesure
littérale du modèle dressé dans le stade. Les 151 espèces actuellement intégrées
ont les mêmes tailles de référence que cette liste.

## Groupes d’affichage

| Taille du Pokédex | Hauteur de référence du corps dans l’arène |
|---|---|
| Jusqu’à 0,40 m | 0,75 m |
| Plus de 0,40 à 0,70 m | 0,95 m |
| Plus de 0,70 à 1,00 m | 1,20 m |
| Plus de 1,00 à 1,50 m | 1,55 m |
| Plus de 1,50 à 2,00 m | 1,95 m |
| Plus de 2,00 à 3,00 m | 2,40 m |
| Plus de 3,00 à 5,00 m | 2,85 m |
| Plus de 5,00 m | 3,30 m |

Les seuils et dimensions sont éditables dans
`Assets/AeroStadium/Resources/Data/pokemon-display-size-policy.json`.
Nidoqueen (1,30 m) et Nidoking (1,40 m) partagent une hauteur de 1,55 m dans
l’arène. Taupiqueur gagne une taille lisible, Onix et Léviator sont contenus.
Une limite de 5,50 m sur l’envergure/profondeur de référence réduit uniformément
les modèles très étalés si nécessaire : leurs proportions restent intactes.
La taille visuelle peut alors être inférieure à celle du groupe.

`PokemonDisplaySize` règle uniquement l’échelle uniforme du personnage entier
dans `ArenaView.ShowPokemon`. Les modèles sources, `NativeNormalization`, les
os, les clips et les tailles originales restent inchangés. Caméra commune,
hauteur des effets d’attaque, étiquettes de dégâts et dimensions des panaches
utilisent cette échelle d’affichage. Le gros plan d’introduction adapte sa caméra
au corps sans annuler l’échelle du personnage.

## Extraction reproductible

Enregistrer la page HTML UTF-8 puis exécuter depuis le dépôt :

```powershell
python Tools/Sizing/import_pokepedia_sizes.py chemin/page.html chemin/sortie
```

Le script utilise la bibliothèque standard Python, développe les cellules
fusionnées et produit données, politique, CSV lisible et rapport d’exclusion.
Il ne télécharge aucun modèle et ne modifie aucun asset natif.
La copie HTML brute et les rapports détaillés restent locaux.

## Vérification

Les builds exécutent `PokemonSizeValidation` : couverture de la liste, ordre
des groupes, exemples proches/petit/grand, mesure réelle de chaque corps natif
dans les 151 prefabs après mise à l’échelle, sol, emprise et proportions.
Les mesures utilisent les indices des sous-maillages colorés NativeLayeredLit
et les matrices d’os ; elles excluent gaz, fumée et anciennes bornes de rendu.

`--size-review` lance cinq comparaisons animées dans la même caméra :
Nidoqueen/Nidoking, Taupiqueur/Évoli, Onix/Léviator, Dracaufeu/Tortank,
Smogo/Smogogo. Il mesure les corps en coordonnées du monde pendant les actions
et écrit `size-review-world-bounds.json`. Ce contrôle démontre l’échelle et la
stabilité sur ces cas représentatifs, sans valoir acceptation artistique de
tous les modèles ou de toutes leurs animations.
