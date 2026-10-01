# AeroStadium — journal de reprise

Mis à jour le 1er octobre 2026.

## Décision du projet

Le projet est désormais un **jeu Unity autonome**, Pokémon non officiel, inspiré de Stadium 2. Le développement se fait dans `NeanSword/AeroStadium`. `NeanSword/Aero-Stadium-2-FR` reste une référence ; ne pas continuer la recompilation N64 ou modifier son runtime pour ce nouveau travail.

Le premier mode demandé est **solo contre une IA**. Les priorités sont un stade original, un HUD propre, les modèles des générations récentes, les capacités classées individuellement en physique/spécial et la compatibilité manette. Les indications Xbox servent de défaut ; PlayStation et Nintendo utilisent leurs indications adaptées.

## Ce qui existe

- Projet Unity 6000.3.25f1, URP 17.3.0, Input System 1.20.0, uGUI ; version Windows Mono.
- Moteur C# indépendant de Unity, identifiants entiers au-delà de 255, équipes jusqu'à six membres, hasard reproductible, priorité/Vitesse, PP/Lutte, STAB, 18 types, quelques effets de capacités et objets.
- Nitrocharge est physique ; Lance-Flammes est spéciale. Le type Feu ne choisit pas les statistiques offensives.
- Interface actuelle : **1 contre 1 miroir**, sélection Germignon/Ho-Oh et objet, PV, quatre capacités avec catégorie/puissance/PP, événements animés, pause, résultat et nouveau combat.
- Stade construit pour AeroStadium : terrain, marquages, tribunes, sièges, éclairage, tableau d'affichage. Caméra rapprochée pour les petits Pokémon, sans agrandir leur modèle.
- Manettes : Xbox par défaut, DualShock/DualSense, Switch Pro ; usages natifs Submit/Cancel, stick/D-pad, Start pour pause, débranchement et changement de manette active.
- Import local réel de Germignon et Ho-Oh de Pokémon Écarlate/Violet. UV, textures, géométrie et squelette conservés. Prefabs de 0,9 m et 3,8 m avec pivot au sol.
- Export/import reproductibles, contrôles du catalogue, compilation Windows et rapports.

## Contenu limité et adaptations provisoires

Le catalogue a quatre espèces, quatorze capacités et quatre choix d'objet, dont aucun objet. Seuls deux modèles sont disponibles. Carchacrok/Nymphali sont des entrées de données, pas des Pokémon sélectionnables.

Les talents, critiques, altérations de statut, effets secondaires, météo, terrains, formes et mécaniques complètes ne sont pas implémentés. La sélection d'équipe et le changement de Pokémon dans le HUD restent à ajouter. Le moteur sait déjà représenter les réserves.

Germignon utilise une attente créée localement, pas un clip original Switch. Ho-Oh n'a pas de clip de squelette. Les attaques sont des déplacements/impulsions de présentation ; le K.O. utilise une disparition provisoire. L'audio et les animations complètes restent à construire.

## Vérifications

- **16/16 contrôles Core réussis**, sur le catalogue JSON réel, y compris catégories, objets, K.O./réserves, PP/Lutte et replays.
- **16/16 tests Input System réussis**, avec périphériques simulés : Xbox, DualShock, DualSense, Switch, clavier, branchement/retrait, stick/D-pad, pause. Cela ne constitue pas un essai matériel de toutes les manettes.
- Préparation Unity et imports réussis ; hauteurs des prefabs vérifiées. Export Blender : triangles, sommets, UV et poids d'os identiques après aller-retour FBX.
- Compilation Windows réussie, zéro erreur et zéro avertissement dans son rapport.
- Premier essai visible de 120 secondes sans erreur du jeu ; navigation et combats Ho-Oh/Germignon observés, jusqu'à des résultats victoire/défaite.
- Corrections issues des essais : orientation vers l'adversaire, caméra pour Germignon, sélection conservée après changement, PP préservés en pause, Lutte accessible, récupération immédiate d'une manette restante après retrait, conservation du focus après clic sur le décor.
- Démonstration automatique Ho-Oh après orientation/K.O. : `passed=True`, zéro erreur, combat terminé. Dernière compilation après correction du focus également réussie. Vérification manuelle exhaustive de la pause et de la navigation à compléter ; la publication GitHub a été priorisée à la demande de l'utilisateur.

Une première session manuelle a produit un faux échec du contrôleur de test après retour à la sélection (un modèle attendu au lieu de deux). Le contrôleur de test a été corrigé pour tenir compte de l'écran courant. Le mode automatique désactive les boutons afin de conserver un scénario reproductible.

## Fichiers utiles et reprise locale

Le checkout local est le dossier `AeroStadium` du workspace du projet ChatGPT. Le jeu construit est `Builds/Windows/AeroStadium.exe` ; conserver les fichiers voisins et `AeroStadium_Data`. Les rapports sont sous `Builds/Reports`, les journaux sous `Logs`.

```powershell
.\Tools\test.ps1 -UnityTests
.\Tools\build.ps1
```

Le script attend le processus Unity lui-même, sans attendre indéfiniment ses services enfants. Fermer l'éditeur de ce projet avant une autre instance batch. Les tests Input System sont EditMode ; ne pas ajouter `-quit` à la commande Test Runner.

Pour une démonstration automatique : `--smoke-test --seconds 120 --species 152 --seed 20261001`. La valeur 250 choisit Ho-Oh. Sans `--smoke-test`, le jeu reste interactif. Sans `--seconds`, il ne se ferme pas automatiquement.

**Avant tout lancement visible**, activer l'observation de la fenêtre demandée par l'utilisateur, observer les essais, puis arrêter l'observation après fermeture du jeu. Les compilations/imports batch sont sans fenêtre de jeu.

## Modèles et publication

Les modèles, textures et prefabs sont locaux dans `Assets/AeroStadium/Resources/LocalModels` et ignorés par Git. Les GLB préparés sont dans le workbench graphique voisin, sous `graphics-workbench/local/pokemon-models`. `Tools/export_switch_models.py` lit ces sources depuis Blender ; `LocalModelImporter.PrepareModels()` crée les matériaux/prefabs.

Ne pas publier de ROM, dump, archive de modèle ou payload Pokémon. Les sources et outils du prototype, les réglages Unity et la documentation sont publiables dans AeroStadium. Un clone propre nécessite les modèles locaux pour afficher les deux Pokémon. Les pages sources et crédits figurent dans `docs/CREDITS.md`.

## Suite

Passer à une sélection de trois Pokémon et au menu de changement, puis ajouter des modèles/animations et étendre les capacités, objets et règles par étapes testées. L'objectif d'un catalogue complet est encore à développer ; ce journal décrit un prototype jouable, pas le jeu final.
