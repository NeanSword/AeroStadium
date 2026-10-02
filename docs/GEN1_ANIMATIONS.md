# Animations de la génération I

## Intégration native ordonnée — en cours

Les sources normales du second dump sont collectées pour #001–151 dans `LocalModelSources/BDSPNative`, avec SHA-256/provenance. Le catalogue natif a sa propre voie `Resources/NativeModels/NNN/Pokemon.prefab` et garde chaque squelette associé à ses clips. L'import remplace la présentation d'une espèce seulement lorsque son prefab natif est préparé ; les modèles antérieurs restent sauvegardés.

Premier pilote : Bulbizarre #001, 26 clips, import batch réussi, hauteur de référence 0,7 m. Contrôle visuel du player et lot complet encore en cours. Les flags de boucle natifs et durées sont conservés ; les courbes sont rééchantillonnées à 60 Hz avec clés natives et raffinement adaptatif contrôlé (tolérance vectorielle 10⁻⁵ unité et rotation 10⁻⁴ rad). Cette représentation interpolée est une approximation mesurée, pas un transfert bit à bit du moteur d'origine.

Les mouvements de racine restent documentés ; la présentation de combat bloque la translation locomotrice, sans modifier les clips sauvegardés. Les expressions UV et la visibilité des accessoires suivent les os de contrôle. Les rôles absents ont des secours nommés, sans ajout de faux clip officiel. Ressources et dérivés restent locaux et ignorés par Git. Rapport de progression : miroir Codex `tmp/bdsp-animation-export/kanto-progress.json`. Voir `LastProgressLogs.md` pour le dernier état testé.


## Couverture native mise à jour — second dump

Les 52 espèces qui manquaient dans le premier relevé sont toutes présentes dans le second ROMFS fourni (`010018E011D92000`). Ses bundles Unity contiennent modèles, squelettes, textures normales et clips mobiles pour 151/151 espèces. Les sources natives du projet couvrent donc désormais tout Kanto.

Inventaire : 3 214 occurrences de clips / 3 084 noms distincts par espèce, zéro erreur de lecture ; contrôle de Mesh/squelette des 151 modèles et contrôle indépendant des chemins d'os de 1 025 occurrences de clips pour les 52 manquants. Les animations sont présentes dans `common` ainsi que dans les bundles spécifiques `battle/animations` et `field/animations` ; leurs courbes sont natives dans `m_MuscleClip.m_Clip.data`.

Rapports locaux : `output/animations/bdsp-inspection/README.md`, CSV de couverture, inventaire SHA-256 et rapport indépendant. Aucun asset de ce second dump n'est encore converti/importé dans le jeu. L'interpolation, le rendu des matériaux, les boucles et la taille seront contrôlés à la conversion. Garder les clips avec leur squelette natif avant tout retargeting. Le pilote Pikachu Écarlate/Violet reste conservé séparément. Les essais procéduraux ci-dessous restent non validés visuellement.


## État actuel — 2 octobre 2026

**Les animations originales ne sont pas validées visuellement.** Le dernier build compile sans erreur, mais le runtime présente encore des tailles, cadrages et appuis incorrects. Trois espèces statiques échouent encore au contrôle du mouvement. Ne pas présenter `passed=True` comme une validation de qualité ni promouvoir les recettes en attente.

- Les 151 GIFs de formes normales de Kanto ont été examinés comme références visuelles. Aucun GIF, clip ou ensemble de poses de ces références n'a été copié dans le jeu. Les performances créées sont originales : attention, anticipation, action, récupération et mouvements secondaires.
- Inventaire réel des modèles : 117 possèdent un squelette avec skin ; 34 n'ont pas de skin. Sept rigs générés sont actifs. Les 21 autres recettes de génération sont bloquées par `Pending` en attente de vérification. Cette étape ne fournit pas 151 animations officielles.
- Les trois rigs dédiés de Kokiyas (#090), Electrode (#101) et Kabuto (#140) sont **uniquement en staging**. Compilation autonome et géométrie contrôlées : repos exact, déformation maximale des arêtes de 25,2 % pour charnière/langue, 19,2 % pour pattes, négligeable pour la sphère solide. Le contrôle Unity `BakeMesh` précède toute activation. Ils ne sont pas installés ni validés dans le jeu.
- Le contrôle des supports couvre les 151 espèces ; seules ces trois espèces à coquille/sphère restent statiques et échouent encore au critère de mouvement. La mesure CPU du sol correspond à `BakeMesh` à environ 4,2 × 10⁻⁷ m près. La dernière revue automatique CPU couvre quatre modèles, sans erreur technique, médiane 5 ms et P95 10 ms ; elle ne lève pas le rejet visuel. La revue Rich a été interrompue à la détection du mauvais cadrage.
- Blocage concret : hauteurs attendues → mesurées dans le player pour Pikachu, Dracaufeu et Roucarnage : **0,4 → 2,111 m**, **1,7 → 4,415 m**, **1,5 → 3,638 m**. Le cadrage et les appuis dérivés de ces tailles sont donc faux. Les échelles sont correctement sérialisées dans le build, tous les os vérifiés descendent du Model et les bindposes correspondent aux sources. Désactiver l'Animator n'a pas corrigé le défaut. La cause runtime reste en enquête ; helper de comparaison matrices natives/TRS prêt en staging, sans installation ni lancement supplémentaire.

## Reprise immédiate

L'utilisateur autorise désormais aussi l'extraction des modèles du dump. Un pilote Pikachu normal complet est extrait : 37 fichiers / 8 449 040 octets, 81 nœuds / 55 os, LOD0 de 2 482 sommets / 3 950 triangles, 18 textures et quatre paires TRANM/TRACM (attente, course, attaque en boucle, dégâts). Sources natives et conversion sauvegardées dans `LocalModelSources/ScarletVioletNative/025`, ignoré par Git. Version GLB courante : `conversion/pikachu-native-textured-animated-uv-fixed_ssc.glb`. Les UV et normales sont adaptés à glTF ; les PNG natifs restent inchangés.

La compensation d'échelle `SegmentScaleCompensate` exige 55 helpers supplémentaires en glTF, sans changement des canaux natifs. La variante compensée correspond à la règle du viewer primaire GFTool : 242 poses à 60 Hz, erreur de sommet maximale 8,31 × 10⁻⁸ unité native ; 956 poses intermédiaires, approximation maximale 0,000301 unité. Treize tests du lecteur et 15 977 comparaisons de quaternions passent. Quatre poses de l'attente sont importées et rendues dans Blender. Ces vérifications ne prétendent pas comparer la lecture au moteur Switch lui-même.

Les shaders PBR restent adaptés, les yeux exigent un rendu des couches/masques et de leurs animations UV ; un albédo blanc ne suffit pas. La course conserve son déplacement racine de 1,2 unité, avec InitData non interprété. Aucun remplacement des modèles du jeu ni import Unity du pilote à cette étape. Conversion GLB en staging : `tmp/romfs-conversion-audit`, lecteur de courbes : `tmp/romfs-animation-conversion`, PNG : `tmp/romfs-native-textures`. Préparer l'import d'une seule espèce, calibrer sa hauteur et valider son rendu avant extension aux autres.

L'inspection **en lecture seule du ROMFS fourni** confirme 23 589 packs. Les **5 558 animations `.tranm` de forme de base (`00_00`) ont toutes été vérifiées physiquement**, dans 99 packs et pour 99 espèces de Kanto : aucune entrée recherchée manquante ou invalide. Elles sont compressées avec Oodle de type 3. Pikachu en compte 78, Dracaufeu 83 et Tortank 86. Cinquante-deux espèces n'ont pas d'animation de base vérifiée dans ce relevé, notamment Roucarnage, Alakazam, Ponyta et Nosferalto ; cette recherche par noms documentés ne démontre pas l'absence absolue d'un fichier inconnu. En incluant les autres formes, 8 408 noms `.tranm` sont identifiés, sans vérification physique exhaustive de ce second ensemble.

Trois fichiers réels ont ensuite été décodés **en mémoire seulement**, avec une bibliothèque Oodle déjà installée : course de Pikachu (60 i/s, 25 échantillons, 37 pistes d'os), attente de Dracaufeu (60 i/s, 193 échantillons, 81 pistes), début d'attaque de Tortank (60 i/s, 47 échantillons, 69 pistes). Les tables, noms d'os et vecteurs de clés sont valides ; 31, 74 et 75 canaux respectivement contiennent des valeurs variables. Ce contrôle démontre la présence de mouvements squelettiques lisibles, sans validation visuelle.

Rapports et mapping : `output/animations/romfs-inspection` dans le dépôt local, ignoré par Git. Scripts reproductibles : `tmp/romfs-animation-inspection` dans le miroir Codex. Cette inspection initiale n'exportait aucun buffer de jeu ; le pilote extrait ensuite, après autorisation, est décrit ci-dessus. Aucun fichier du dump n'a été modifié et aucune animation native n'est encore intégrée à Unity.

Préserver les modèles/meshes sources, le travail de staging et les journaux. Corriger les transformations réellement utilisées par le player avant une nouvelle validation visuelle des performances originales. Les GIFs sont des références, les rigs générés et leurs poids sont une création du projet. Travail local seulement : aucun commit/push de cette étape.

## Historique conservé — inventaire précédent

## État vérifié localement

La préparation Unity a généré les 151 prefabs du Pokédex de Kanto. L’inventaire des 151 GLB sources montre toutefois que seuls **19 modèles contiennent des animations**, pour **162 clips** au total. Les **132 autres GLB n’ont aucun tableau `animations`**. Les espèces animées sont : 001, 006, 015, 025, 040, 041, 081, 088, 089, 093, 095, 132, 133, 134, 135, 136, 146, 149 et 150.

Pour les fichiers qui en ont, Unity conserve chaque clip dans un contrôleur du modèle sous un état `Source_000`, `Source_001`, etc. `PokemonAnimationDriver.PlaySourceAnimation(index)` rend chaque état sélectionnable par le code. Le contrôleur choisit un repos seulement quand le nom permet de le reconnaître ; il ne boucle pas une séquence reconnaissable comme attaque ou dégâts pour la faire passer pour une attente. Les rôles combat sont associés uniquement quand le nom est explicite : Bulbizarre expose attaque, dégâts et K.O. ; Pikachu expose une attaque ; Dracaufeu expose des dégâts ; Dracolosse et Mewtwo exposent attaque, dégâts et K.O. Le reste demeure disponible comme clip source, sans attribution sémantique inventée.

Les animations et contrôleurs générés, comme les modèles, restent dans `Assets/AeroStadium/Resources/LocalModels`, exclu de Git. Les GLB sources sont dans `LocalModelSources`, également exclu de Git.

## Authenticité et prochaines sources nécessaires

Les GLB locaux proviennent de la bibliothèque communautaire [Pokemon-3D-api/assets](https://github.com/Pokemon-3D-api/assets). Son README décrit un pipeline qui récupère des GLB depuis Sketchfab avant optimisation et conseille de vérifier modèle par modèle la présence d’animations. Les clips intégrés ne sont donc pas confirmés comme les animations officielles récentes de chaque espèce.

Aucun fichier `.gfbanm` ou `.tranm` n’est présent dans `LocalModelSources`. L’extension Blender [io_scene_gfbanm](https://github.com/Shararamosh/io_scene_gfbanm) documente l’import des animations natives de Let's Go, Épée/Bouclier, Légendes Pokémon : Arceus et Écarlate/Violet. Pour compléter les 132 espèces et authentifier les mouvements, il faudra les animations extraites et leurs squelettes correspondants ; ces ressources ne peuvent pas être reconstituées à partir des GLB statiques seuls.

Le jeu garde ici ces essais en local. Les sources natives devront être raccordées à leurs armatures avant de remplacer les clips communautaires ; un retargeting automatique non contrôlé pourrait déformer les Pokémon.
