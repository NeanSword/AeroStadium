# Optimisation mémoire des modèles natifs

L’objectif est de rendre la préparation et la compilation du catalogue Kanto
compatibles avec la machine de 16 Go, tout en conservant les 151 modèles normaux,
leurs couleurs et les 3 084 animations natives.

## Cause et portée

Les compilations du 10 octobre ont échoué pendant l’allocation de textures.
Le journal signalait environ 2,73 Gio dans ALLOC_GFX et 6,29 Gio dans
ALLOC_CACHEOBJECTS. Ces catégories Unity ne mesurent pas toute la mémoire du
processus. L’ancien catalogue LocalModels était déjà exclu pendant ces essais.

L’audit a trouvé les 151 GLB sources imbriqués dans les prefabs préparés. Cela
gardait leurs textures, meshes et clips importés en dépendance, en plus des assets
préparés. Resources contenait aussi toutes les textures PNG externes, y compris
893 sans référence de matériau.

À partir des dimensions PNG, une estimation RGBA à quatre octets par pixel avec
une chaîne complète de mipmaps représente environ 2,09 Gio pour les textures
embarquées et 0,67 Gio pour les PNG externes sans référence de matériau. Cette
estimation est un ordre de grandeur théorique des données évitables ; elle
n’est ni une mesure de RAM économisée ni une promesse de réduction du pic Unity.
Elle n’inclut pas les économies éventuelles sur les meshes et les animations.
Les formats réels, les données temporaires et le chargement modifient le résultat.

## Organisation sans réduction de qualité

NativeAssetMemoryOptimization détache le modèle de son prefab GLB source.
Les éventuels meshes et avatars encore issus du GLB sont enregistrés en assets
indépendants. Les sources et les textures externes sont déplacées vers
Assets/AeroStadium/NativeModelAuthoring avec AssetDatabase.MoveAsset, qui garde
leurs GUID. Les matériaux continuent donc à référencer les textures nécessaires.
Les sources restent disponibles localement pour une future préparation.

Les prefabs, matériaux, meshes, contrôleurs et clips préparés restent dans
Resources/NativeModels. La suppression de la dépendance GLB et le déplacement
des PNG hors de Resources permettent d’inclure les dépendances réellement
utilisées. Aucune nouvelle compression, baisse de résolution ou modification
des courbes d’animation n’est appliquée. Les meshes nécessaires au grounding
et aux effets de fumée restent lisibles.

NativeModelImporter et NativeMaterialImport cherchent chaque source à son
emplacement d’origine, puis dans NativeModelAuthoring. Une nouvelle préparation
détache également les modèles avant de sauvegarder leurs prefabs. La conversion
vérifie le nombre de clips, leurs noms et durées, leurs cibles animées, la
hiérarchie et l’absence finale de dépendance GLB. Les prefabs précédents sont
sauvegardés sous output/memory/20261010/before ; les sauvegardes existantes sont
préservées lors d’une reprise.

La conversion du 10 octobre a terminé avec 151 modèles, 3 084 clips et passed=true
dans output/memory/20261010/native-pack-optimization.json. Ce résultat confirme
la conversion des assets ; il ne confirme pas encore la compilation ni le
fonctionnement en jeu après optimisation.

## Préparation, compilation et restauration

La préparation et la validation purgent les assets inutilisés par lots de cinq
espèces. Une autre purge précède la compilation pour ne pas conserver le
catalogue de validation dans le cache de l’éditeur.

NativeOnlyBuildScope exclut temporairement Resources/LocalModels lorsque les
151 prefabs natifs sont présents. Le catalogue de secours est déplacé vers
Assets/AeroStadium/LegacyModelsBuildHold et restauré à la fin, y compris si la
compilation lève une exception. Si la purge échoue pendant la construction du
scope, celui-ci restaure lui-même le catalogue avant de relancer l’erreur.
Si la restauration échoue aussi, les deux erreurs sont conservées.

Un arrêt brutal du processus Unity ne peut pas exécuter Dispose. Le lancement
externe doit donc aussi restaurer le dossier dans son finally. Avant de relancer,
vérifier qu’un dossier LegacyModelsBuildHold résiduel est restauré avec son
fichier .meta. Ne pas démarrer deux instances Unity sur le même projet.

## Libération en jeu

RuntimeAssetMemory regroupe les changements de modèles. Après deux secondes
sans changement, il appelle Resources.UnloadUnusedAssets lorsque huit changements
se sont accumulés, ou après un retrait de modèle. Deux collectes normales sont
espacées d’au moins quinze secondes. Application.lowMemory déclenche une collecte
plus rapide. Les objets actifs et leurs références restent protégés ; aucun
UnloadAsset ciblé ne vise les modèles affichés.

Les lignes [asset-memory] indiquent les octets alloués par Unity avant et après
la collecte, sa durée et sa cause. Pour évaluer la machine entière, relever aussi
le pic de mémoire privée et le working set du processus Windows. Une collecte
peut occasionner du travail ponctuel ; sa fluidité doit être contrôlée en jeu,
notamment pendant la navigation rapide entre partenaires.

## Reproduire les vérifications

Dans Unity, utiliser les entrées AeroStadium « Optimiser les données natives
sans perte », « Préparer les modèles natifs » et « Build Windows ». La validation
complète utilise AeroStadium.EditorTools.NativeModelValidation.VerifyAll.

Depuis le dossier du projet, la préparation et la compilation habituelles sont :

```powershell
.\Tools\build.ps1 -Action Prepare
.\Tools\build.ps1 -Action BuildWindows
```

Ces commandes créent la version Builds/Windows. Pour tester une version séparée
et conserver la version stable, lancer ProjectBuild.BuildWindows avec l’argument
--stadium-reference-build ; --reuse-prepared-assets évite une préparation déjà
réalisée. Ne pas utiliser --stadium-only-validation comme preuve de validation
complète du catalogue natif. Les méthodes d’optimisation et de validation peuvent
également être appelées avec -executeMethod dans un processus Unity batch attendu
jusqu’à sa fermeture.

Preuves à contrôler :

- output/memory/20261010/native-pack-optimization.json : 151 modèles, 3 084 clips,
  passed=true, aucune dépendance GLB restante.
- output/animations/bdsp-inspection/unity-native-validation.json : validation
  complète des modèles, matériaux et poses natives.
- Builds/Reports/build-windows.json et journal Unity : compilation réussie,
  taille du paquet et durée ; compléter avec les mesures du processus Windows.
- Test visible --selection-test --seconds 120 : observer les modèles et leurs
  couleurs, les contrôles manette/souris, le combat, le remplacement après K.O.,
  le retour à l’équipe et les collectes [asset-memory]. Initialiser l’observation
  de l’écran avant de lancer le jeu, puis la fermer lorsque le jeu est fermé.

Une copie expérimentale de DLL ou une compilation interrompue ne constitue pas
une compilation Windows réussie. Les gains réellement mesurés et les résultats
des tests postérieurs doivent être enregistrés dans LastProgressLogs.md.

## Mesures validées le 11 octobre 2026

Validation native complète : 151 modèles, 3 084 clips, 15 420 poses ; réussite.
Compilation expérimentale : 7 478 222 187 → 3 615 270 683 octets, soit −51,66 %.
Pic du processus Unity pendant cette compilation : 6,041 Gio RAM et 6,803 Gio
privés. Le minimum de RAM physique libre de Windows approchait 12 Mo ; cette
mesure ne prouve pas l’absence de pression mémoire sur toute la machine.
Première revue visible de 120 s : pic RAM 0,788 Gio et 1,317 Gio privés, réussite.
Trois collectes ont libéré 43 260 732 octets d’allocations Unity, chacune en 6–7 ms.
Les nouveaux effets de cartes ont ensuite été compilés et testés séparément ;
leurs preuves sont sous output/ui/card-effects-20261011.
