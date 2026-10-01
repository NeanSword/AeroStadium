# Architecture du prototype AeroStadium

Ce document décrit le premier prototype Unity, au 1er octobre 2026. La présentation propose un combat solo 1 contre 1 ; le moteur permet déjà de représenter des équipes plus grandes. Les comportements décrits comme futurs ne font pas encore partie de l'interface jouable.

## Organisation

| Emplacement | Rôle |
| --- | --- |
| `Assets/AeroStadium/Core` | Moteur de combat en C# sans dépendance à Unity, validation des données et table des types. |
| `Assets/AeroStadium/Resources/Data/catalog.json` | Catalogue des espèces, capacités et objets utilisés par le prototype. |
| `Assets/AeroStadium/Presentation` | Construction du stade, interface, entrée utilisateur et animation des événements de combat. |
| `Assets/AeroStadium/Editor` | Préparation du projet, import des modèles, validation du catalogue et compilation Windows. |
| `Assets/AeroStadium/Resources/LocalModels` | Modèles, textures, matériaux, animations et prefabs locaux ; exclus de Git. |
| `Tests/CoreChecks` | Vérifications exécutables du moteur et du catalogue, sans lancement de Unity. |
| `Tools` | Scripts de compilation, de vérification et d'export des modèles locaux. |

Le projet utilise Unity 6000.3.25f1, URP 17.3.0, Input System 1.20.0 et uGUI. La version Windows est compilée avec le backend Mono. Les fichiers du jeu recompilé de Stadium 2 ne sont pas utilisés au lancement.

## Données et règles de combat

`Catalog` charge des définitions sérialisables. Les identifiants d'espèces et de capacités sont des entiers stables, distincts de leur position dans les tableaux ; ils ne sont pas limités à 255. Les objets utilisent des identifiants textuels. `Validate()` rejette notamment les références inconnues et les effets non pris en charge.

Une capacité contient sa propre catégorie **Physical**, **Special** ou **Status**, indépendamment de son type. Le calcul des dégâts utilise Attaque/Défense pour une capacité physique et Attaque Spéciale/Défense Spéciale pour une capacité spéciale. Nitrocharge et Lance-Flammes illustrent cette distinction, bien qu'elles soient toutes deux de type Feu.

`BattleEngine` conserve les deux équipes, le Pokémon actif de chaque côté, les PV, PP et modifications temporaires de statistiques. Les équipes peuvent contenir un à six membres. La présentation actuelle lui transmet une équipe d'un membre pour le joueur et une équipe miroir pour l'IA.

Le déroulement d'un tour suit ces étapes :

1. La présentation recueille le choix du joueur ; l'IA choisit une action à partir de l'état courant.
2. Le moteur valide les deux actions avant de modifier l'état ou de consommer des nombres aléatoires.
3. Il détermine l'ordre selon la priorité et la Vitesse. Une graine fixe départage les égalités et produit les tirages de précision et de dégâts.
4. Il résout les actions, les effets et les objets de fin de tour. Une action est ignorée si son Pokémon a été mis K.O. avant de pouvoir agir.
5. Les Pokémon actifs K.O. sont remplacés par une réserve disponible, puis le résultat du combat est déterminé.
6. La présentation reçoit une suite d'événements ordonnés et les affiche.

Le moteur propose aussi une commande de changement de Pokémon, avec priorité dédiée et remise à zéro des modifications temporaires. Cette commande n'a pas encore de menu dans le prototype 1 contre 1.

Le hasard appartient au moteur, et non à Unity. Deux simulations utilisant les mêmes données, la même graine et les mêmes commandes doivent donner le même résultat. Le temps des animations et le nombre d'images par seconde n'influencent pas les règles du combat.

## Interface et événements

`GameBootstrap` charge le catalogue, prépare la scène et affiche successivement la sélection, le combat, la pause et le résultat. Le joueur sélectionne Germignon ou Ho-Oh ainsi qu'un objet tenu. Pendant le combat, les quatre capacités sont directement accessibles ; leur type, catégorie, puissance et PP sont visibles.

Le moteur renvoie des `BattleEvent` pour les attaques, dégâts, soins, changements de statistiques, objets, K.O. et résultat. Le texte du journal et les animations utilisent ces événements. Les PV affichés peuvent évoluer progressivement pendant l'animation, puis sont synchronisés avec l'état final calculé par le moteur.

La sélection d'équipe, les menus de changement et les animations complètes de K.O. restent à ajouter. Le moteur de combat ne doit pas dépendre de leur durée ou de leur présence.

## Manettes et indications

`ControllerHints` observe la manette courante de Unity Input System et les changements de périphérique. Le nom du layout, le fabricant et le produit servent à reconnaître les familles PlayStation et Nintendo ; les autres manettes utilisent les indications Xbox par défaut. Sans manette, l'interface affiche les indications clavier.

La navigation des menus passe par `InputSystemUIInputModule`. La validation et le retour utilisent les usages **Submit** et **Cancel** des layouts Input System, plutôt qu'une correspondance imposée entre le nom d'une touche et sa position. Cela permet de respecter les différences de disposition entre Xbox et Nintendo lorsqu'un layout adapté est fourni. Le bouton Start ouvre la pause ; Échap reste disponible au clavier.

Les indications sont des noms de touches adaptés à la famille détectée : A/B pour Xbox et Nintendo, Croix/Rond pour PlayStation. Un périphérique présenté par son pilote comme une manette XInput générique peut être identifié comme Xbox. Les layouts et le nom détecté ne remplacent pas des essais sur les manettes physiques visées.

## Modèles et présentation

`ArenaView` construit un stade original à partir d'éléments de scène : terrain circulaire, marquages, tribunes, supports, éclairage et tableau d'affichage. La caméra et les effets URP assurent une présentation commune aux deux Pokémon. Aucun stade du jeu N64 n'est importé.

Les modèles sont chargés sous `Resources/LocalModels/<identifiant>/Pokemon`. Pour le premier prototype, seuls les identifiants 152 et 250 possèdent un modèle. L'absence d'un prefab attendu est une erreur visible dans les journaux ; aucun Pokémon de remplacement n'est généré pour masquer cette absence.

Le traitement local suit deux étapes :

1. `Tools/export_switch_models.py`, exécuté dans Blender, lit les GLB préparés, copie leurs textures intégrées et exporte un FBX. Un aller-retour d'import vérifie les sommets, triangles, coordonnées UV et poids du squelette. Le manifeste local consigne les empreintes, matériaux, provenance et limites du modèle.
2. `LocalModelImporter` importe le FBX dans Unity, prépare ses matériaux URP et crée un prefab. Un objet parent ajuste uniformément la taille et place le modèle au niveau du sol, sans modifier la géométrie source.

Les tailles de présentation actuelles sont de 0,9 m pour Germignon et de 3,8 m pour Ho-Oh. Ho-Oh utilise encore une pose de repos et un réglage provisoire de l'envergure. Germignon dispose d'une animation de repos créée pour la prévisualisation ; ce clip ne provient pas du jeu Switch. Ho-Oh ne possède pas encore de clip de squelette. Les petits déplacements et effets de combat sont réalisés dans la présentation Unity.

Les sources locales proviennent de Pokémon Écarlate/Violet, via les pages de modèles de Germignon et Ho-Oh documentées dans le README. Le dépôt publie les outils d'import et le code de présentation. Les fichiers de modèles et textures, les archives sources, les ROM, les dumps et les exécutables ne sont pas distribués dans Git.

## Préparation, compilation et vérifications

`ProjectBuild.Prepare()` crée les réglages URP et la scène, puis prépare les modèles locaux disponibles. `ProjectBuild.Validation()` valide le catalogue. `ProjectBuild.BuildWindows()` effectue ces étapes avant de produire l'exécutable et un rapport de compilation. Les scripts de `Tools` donnent accès à ce workflow depuis PowerShell.

Les vérifications de `Tests/CoreChecks` compilent le même moteur de combat sous .NET, sans dépendance à Unity ni paquet de test externe. Elles portent notamment sur la classification des capacités, les types, les objets, les équipes, le rejet des commandes invalides et la reproductibilité. Au 1er octobre 2026, les 16 vérifications ont réussi ; la préparation, l'import Unity et la compilation Windows ont également réussi, sans erreur ni avertissement de compilation.

Les 16 cas de `Assets/AeroStadium/Tests/Editor` ont réussi avec des périphériques simulés. Ils vérifient les commandes réellement reçues par l'interface, les familles de manette, le branchement/retrait, le stick, le D-pad et la pause. Une session visible de 120 secondes a permis d'observer les modèles et les combats. Les essais matériels de toutes les manettes restent à réaliser.

Le mode `--smoke-test --seconds 120` de l'exécutable sert à lancer une session automatisée bornée : sélection, combat et résultat. Ses boutons sont désactivés pour garder un scénario reproductible. `--species 152` ou `250` choisit le modèle ; `--seed` fixe le hasard. Il vérifie les erreurs remontées et la présence des deux instances de Pokémon. Ce test ne remplace pas une observation du rendu, des animations et de la navigation, ni les essais sur manette physique.

## Limites et extension

Le catalogue actuel contient quatre espèces, quatorze capacités et quatre choix d'objet, dont l'absence d'objet. Les deux espèces sans modèle ne sont pas sélectionnables. Le calcul utilise le niveau 50, une nature neutre, 31 IV et aucun EV. Les dégâts et certains effets suivent des règles simplifiées ; la compatibilité exacte avec une génération officielle n'est pas revendiquée.

Les talents, coups critiques, altérations de statut, effets secondaires, météo, terrains, pièges, formes spéciales, règles de combats doubles et effets exhaustifs des capacités et objets restent hors du prototype. Les effets non pris en charge doivent être développés explicitement avant d'ajouter le contenu correspondant.

La prochaine étape de présentation est un combat avec des équipes de trois et un menu de changement de Pokémon. L'ajout de modèles et d'animations peut se faire espèce par espèce, sans changer les identifiants du catalogue. L'extension des règles doit rester dans `Core`, avec des vérifications portant sur leur comportement, tandis que `Presentation` prend en charge leur affichage et leurs animations.
