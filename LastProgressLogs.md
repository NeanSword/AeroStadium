# AeroStadium — journal de reprise

Mis à jour le 1er octobre 2026.
## Avancement du 1er octobre 2026

- Le périmètre demandé pour cette étape est le Pokédex de Kanto : les 151 formes standards (#001–151) sont maintenant les seuls modèles Pokémon présents dans `Resources/LocalModels`. L'introduction met en scène 18 d'entre eux avec des transitions horizontales, verticales et circulaires.
- Les 151 GLB ont été convertis pour Unity (Draco et WebP retirés, textures converties en PNG, accessoires clairsemés densifiés). Une texture de Smogogo utilisait un troisième canal UV identique au second ; la conversion l'a réaffectée sans changer les coordonnées visibles.
- Préparation Unity réussie : 151/151 prefabs importés sans erreur. Le lot local contient 293 maillages, 121 squelettes, 162 clips d'animation et 421 images PNG. Les modèles suivent la hauteur Pokédex et utilisent un pivot au sol.
- Le test visuel Windows de Smogogo a montré son modèle texturé dans l'arène et le HUD. Le smoke test s'est terminé en trois tours : `errors=0`, `models=2`, `battleEnded=True`, `passed=True`. La première tentative a révélé que le shader URP de l'arène était éliminé du build ; un matériau de référence a été ajouté dans `Resources/Materials/ArenaLit.mat` et l'essai suivant a réussi.
- Le build Windows réussit. Les tests Core et Input System passent chacun 16/16. Le catalogue contient 151 espèces et 85 capacités tirées des données PokéAPI Rouge/Bleu ; la sélection est paginée sur 16 pages.
- La nouvelle illustration de titre reste reportée à la demande de l'utilisateur. `OPENAI_API_KEY` est présente dans l'environnement ; sa valeur n'a jamais été affichée ni utilisée pour cette étape. Le prompt est conservé dans `docs/artwork/aerostadium-title-background-prompt.txt`.


## Décision du projet

Le projet est désormais un **jeu Unity autonome**, Pokémon non officiel, inspiré de Stadium 2. Le développement se fait dans `NeanSword/AeroStadium`. `NeanSword/Aero-Stadium-2-FR` reste une référence ; ne pas continuer la recompilation N64 ou modifier son runtime pour ce nouveau travail.

Le premier mode demandé est **solo contre une IA**. Le projet reste extensible, mais l'étape actuelle utilise uniquement les 151 modèles standards de Kanto. Les priorités comprennent un stade original, un HUD propre, les capacités classées individuellement en physique/spécial et la compatibilité manette. Les indications Xbox servent de défaut ; PlayStation et Nintendo utilisent leurs indications adaptées.

## Ce qui existe

- Projet Unity 6000.3.25f1, URP 17.3.0, Input System 1.20.0, uGUI ; version Windows Mono.
- Moteur C# indépendant de Unity, identifiants entiers au-delà de 255, équipes jusqu'à six membres, hasard reproductible, priorité/Vitesse, PP/Lutte, STAB, 18 types, quelques effets de capacités et objets.
- Nitrocharge est physique ; Lance-Flammes est spéciale. Le type Feu ne choisit pas les statistiques offensives.
- Interface actuelle : **1 contre 1 miroir**, sélection parmi les 151 espèces de Kanto par pages de dix, objet, PV, capacités avec catégorie/puissance/PP, événements animés, pause, résultat et nouveau combat.
- Stade construit pour AeroStadium : terrain, marquages, tribunes, sièges, éclairage, tableau d'affichage. Caméra rapprochée pour les petits Pokémon, sans agrandir leur modèle.
- Manettes : Xbox par défaut, DualShock/DualSense, Switch Pro ; usages natifs Submit/Cancel, stick/D-pad, Start pour pause, débranchement et changement de manette active.
- Import local des 151 modèles standards de Kanto. Les prefabs ont la hauteur officielle PokéAPI et un pivot au sol ; source GLB, modèle préparé et données de crédit sont conservés localement.
- Export/import reproductibles, contrôles du catalogue, compilation Windows et rapports.

## Contenu limité et adaptations provisoires

Le catalogue a 151 espèces, 85 capacités et quatre choix d'objet. Les 151 modèles sont sélectionnables. Les talents, critiques, altérations de statut, effets secondaires, météo, terrains, formes et mécaniques complètes ne sont pas encore implémentés. Les capacités ne couvrent pas encore l'ensemble des générations.

Les talents, critiques, altérations de statut, effets secondaires, météo, terrains, formes et mécaniques complètes ne sont pas implémentés. La sélection d'équipe et le changement de Pokémon dans le HUD restent à ajouter. Le moteur sait déjà représenter les réserves.

Germignon utilise une attente créée localement, pas un clip original Switch. Ho-Oh n'a pas de clip de squelette. Les attaques sont des déplacements/impulsions de présentation ; le K.O. utilise une disparition provisoire. L'audio et les animations complètes restent à construire.

## Vérifications

- **16/16 contrôles Core réussis**, sur le catalogue JSON réel, y compris catégories, objets, K.O./réserves, PP/Lutte et replays.
- **16/16 tests Input System réussis**, avec périphériques simulés : Xbox, DualShock, DualSense, Switch, clavier, branchement/retrait, stick/D-pad, pause. Cela ne constitue pas un essai matériel de toutes les manettes.
- Préparation Unity et imports réussis : 151 prefabs, 151 GLB importés, aucune erreur. SHA-256 et nombre de maillages/squelettes/animations comparés aux sources ; aucune animation ni squelette perdus.
- Compilation Windows réussie après ajout d'un matériau URP référencé dans `Resources`, afin que le shader survive au stripping du build.
- Smoke test visible de Smogogo (#110) réussi : modèle texturé et HUD affichés, combat terminé, `errors=0`, `models=2`, `passed=True`.
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

Pour une démonstration automatique : `--smoke-test --seconds 30 --species 110 --seed 20261001`. Les valeurs de sélection acceptées sont 1–151. Sans `--smoke-test`, le jeu reste interactif. Sans `--seconds`, il ne se ferme pas automatiquement.

**Avant tout lancement visible**, activer l'observation de la fenêtre demandée par l'utilisateur, observer les essais, puis arrêter l'observation après fermeture du jeu. Les compilations/imports batch sont sans fenêtre de jeu.

## Modèles et publication

Les modèles, textures et prefabs sont locaux dans `Assets/AeroStadium/Resources/LocalModels` et ignorés par Git. Les originaux sont préservés dans `LocalModelSources/Pokemon3D/gen1`, également ignoré. Pour les régénérer et créer les crédits : `Tools/prepare_generation_one.ps1`, puis `Tools/build.ps1 -Action Prepare`. `LocalModelImporter.PrepareModels()` vérifie les 151 manifestes et crée les prefabs.

Ne pas publier de ROM, dump, archive de modèle ou payload Pokémon. Les sources, outils, manifestes de crédit, réglages Unity et documentation sont séparés des modèles. Les GLB ne sont pas inclus dans Git ; les métadonnées listent 50 crédits individuels. Certaines sources déclarent CC BY-NC 4.0 ou CC BY-SA 4.0 : conserver les fichiers en local et respecter leurs conditions avant toute redistribution ou usage commercial. Voir `docs/CREDITS.md`, `docs/GEN1_MODELS.md` et `docs/GEN1_MODEL_ATTRIBUTIONS.md`.

## Publication GitHub confirmée

- Les 91 fichiers du prototype ont été publiés sur `main`, dans [NeanSword/AeroStadium](https://github.com/NeanSword/AeroStadium), avec le [commit initial du prototype](https://github.com/NeanSword/AeroStadium/commit/46401dcd9c1abf99453f1f29f504b23d3be8d729).
- L'arbre publié correspond exactement aux fichiers locaux vérifiés. Aucun modèle, image, exécutable, DLL ou ROM n'a été inclus.
- [GitHub Actions — Core checks](https://github.com/NeanSword/AeroStadium/actions/runs/36821743841) a réussi : compilation C# 9 / .NET Standard 2.1 sans erreur ni avertissement, puis **16/16 contrôles du catalogue réel**. Ce workflow tourne sur les modifications de `main` et les pull requests.
- Les tests Unity/manettes et les builds Windows restent locaux, car le dépôt ne contient pas les modèles. Leurs résultats sont décrits plus haut.
- La publication a été effectuée via le connecteur GitHub. La lecture Git locale fonctionne ; pour les écritures depuis cette session, utiliser ce connecteur. Le checkout local suit `origin/main`.
- Les champs de mot de passe/secret de la configuration Unity publique ont été vidés avant publication. Vérifier qu'une future sauvegarde de l'éditeur ne les réintroduit pas dans un commit public.

## Suite

Reprendre plus tard l'image originale du titre, puis développer les équipes et changements de Pokémon, les animations propres à chaque espèce et les règles/objets/capacités manquants par étapes testées. Ce journal décrit un prototype jouable, pas le jeu final.
