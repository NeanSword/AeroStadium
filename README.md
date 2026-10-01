# AeroStadium

AeroStadium est un jeu Pokémon non officiel développé dans Unity, inspiré des combats en stade de Pokémon Stadium 2. Il possède son propre moteur de combat, un stade original et une interface adaptée à la manette. L'objectif est d'intégrer progressivement des Pokémon, objets et capacités récents, avec une catégorie physique ou spéciale propre à chaque attaque.

Le développement se fait dans [NeanSword/AeroStadium](https://github.com/NeanSword/AeroStadium). Le dépôt [Aero-Stadium-2-FR](https://github.com/NeanSword/Aero-Stadium-2-FR) reste une référence pour comprendre l'expérience recherchée. Ce nouveau projet ne dépend ni de son exécutable recompilé ni d'une ROM.

## Premier prototype

Le mode actuel est un combat solo **1 contre 1 face à une IA simple**. Le joueur choisit Germignon ou Ho-Oh et un objet tenu ; l'adversaire utilise la même espèce, sans objet. La sélection d'équipe complète n'est pas encore proposée.

- Stade créé pour AeroStadium : terrain circulaire, tribunes, éclairage et tableau d'affichage.
- Interface en français : sélection du Pokémon et de l'objet, PV, quatre capacités, puissance, catégorie, PP, journal du combat, pause et résultat.
- Navigation au clavier, à la souris ou à la manette. Les indications suivent la famille de manette détectée, avec Xbox comme référence par défaut et des indications PlayStation et Nintendo. Le changement de périphérique est pris en compte pendant l'exécution.
- Modèles locaux de Germignon et Ho-Oh provenant de Pokémon Écarlate/Violet. Leurs textures et leur géométrie sont conservées ; la taille et le placement sont réglés dans les prefabs Unity.
- Classification moderne des capacités : Nitrocharge utilise l'Attaque et la Défense, tandis que Lance-Flammes utilise l'Attaque Spéciale et la Défense Spéciale.

Le catalogue contient **4 espèces, 14 capacités et 4 choix d'objet**, dont l'absence d'objet. Carchacrok et Nymphali servent à préparer l'extension des données : leurs modèles ne sont pas encore disponibles dans l'interface. Seuls Germignon et Ho-Oh sont actuellement visualisables.

Le moteur prend en charge les 18 types ordinaires, le STAB, la précision, les PP, la priorité, la Vitesse, quelques effets de capacités et trois objets tenus : Restes, Orbe Vie et Charbon. Les statistiques sont calculées au niveau 50, avec une nature neutre, 31 IV et aucun EV.

**Ce prototype n'est pas un moteur complet des générations récentes.** Les talents, coups critiques, altérations de statut, effets secondaires, météo, terrains, formes particulières et nombreux effets de capacités ou d'objets restent à développer. Les arrondis de dégâts sont simplifiés. L'IA privilégie des attaques efficaces et quelques actions de soin ou de préparation ; elle n'est pas conçue comme un adversaire compétitif.

Les animations sont également provisoires : Germignon utilise une animation de repos créée pour la prévisualisation, et Ho-Oh n'a pas encore d'animation de squelette. Les mouvements de présentation et les effets d'attaque du prototype ne sont pas des animations extraites du jeu Switch.

## Ouvrir et construire le projet

Version de référence : **Unity 6000.3.25f1**, avec **Universal Render Pipeline 17.3.0**, **Input System 1.20.0** et le module de compilation Windows. Les vérifications du moteur de combat utilisent le SDK .NET 10.

Ouvrir ce dossier comme projet dans Unity Hub. Les dépendances sont décrites dans `Packages/manifest.json`. Le menu **AeroStadium** de l'éditeur permet de préparer le projet, de valider le catalogue et de créer la version Windows.

Depuis la racine du dépôt, les scripts PowerShell du projet servent à construire et vérifier le prototype :

```powershell
.\Tools\test.ps1
.\Tools\build.ps1
```

`Tools/build.ps1 -Action Prepare` prépare le projet sans compiler ; `-Action Validation` vérifie le catalogue. `Tools/test.ps1 -UnityTests` ajoute les vérifications Input System dans l'éditeur. Les chemins d'installation peuvent être précisés avec `-UnityEditor` et, pour les vérifications, `-DotNet`.

La préparation crée la scène `Assets/AeroStadium/Scenes/Arena.unity` et les réglages URP, puis importe les modèles disponibles localement. La compilation produit `Builds/Windows/AeroStadium.exe`. Il faut conserver les fichiers et le dossier de données situés à côté de l'exécutable pour le lancer.

Les rapports de validation et de compilation sont écrits dans `Builds/Reports`. Les journaux Unity sont conservés dans `Logs`. Ces résultats de travail sont exclus du dépôt.

GitHub Actions compile le moteur en C# 9 / .NET Standard 2.1 et exécute ses contrôles sur chaque modification de `main` et chaque pull request. Les tests Unity et la compilation du jeu restent locaux : ils utilisent l'éditeur installé et les modèles absents du dépôt.

Pour lancer les vérifications du moteur seules :

```powershell
dotnet run --project Tests/CoreChecks/CoreChecks.csproj -- Assets/AeroStadium/Resources/Data/catalog.json
```

## Modèles locaux

**Les modèles et textures Pokémon ne sont pas distribués dans ce dépôt.** Un clone contient le code et les outils, mais pas les deux modèles nécessaires à leur affichage. Les FBX, textures, matériaux et prefabs locaux sont exclus de Git sous `Assets/AeroStadium/Resources/LocalModels`.

`Tools/export_switch_models.py` s'exécute dans Blender pour convertir les GLB préparés localement en FBX et extraire leurs textures. Il vérifie la géométrie, les coordonnées UV et les poids du squelette après un aller-retour FBX. Le menu de préparation Unity crée ensuite les matériaux URP et les prefabs chargés par le jeu. Voir [l'architecture et les limites de l'import](docs/ARCHITECTURE.md#modèles-et-présentation).

Les sources utilisées pour le développement local sont les pages [Germignon](https://models.spriters-resource.com/nintendo_switch/pokemonscarletviolet/asset/468036/) et [Ho-Oh](https://models.spriters-resource.com/nintendo_switch/pokemonscarletviolet/asset/352007/). Les fichiers préparés, leurs empreintes et leurs informations de provenance sont conservés dans les manifestes locaux. Aucune ROM, aucun dump et aucun contenu du projet de recompilation ne sont publiés avec ce prototype.

## État des vérifications

Au **1er octobre 2026**, la préparation et l'import Unity ont réussi. Les **16 vérifications du moteur de combat ont réussi**, notamment la classification physique/spéciale, les effets d'objets, les équipes, la validation des données et la reproductibilité avec une graine fixe.

La compilation Windows a réussi, sans erreur ni avertissement. Une session visible de 120 secondes a permis d'observer les modèles, le HUD et des combats allant jusqu'à leur résultat. Les **16 tests Input System ont réussi** : commandes Xbox/PlayStation/Nintendo, clavier, navigation, pause et débranchement, avec des périphériques simulés. Les essais de chaque modèle de manette sur du matériel réel restent à faire.

Le journal de reprise détaillé est [LastProgressLogs.md](LastProgressLogs.md). Les sources et crédits figurent dans [docs/CREDITS.md](docs/CREDITS.md).

Les prochaines étapes sont les équipes de trois Pokémon, le changement de Pokémon pendant le combat, l'ajout progressif de modèles et d'animations, puis l'extension des mécaniques et du catalogue. L'architecture est décrite dans [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

AeroStadium est un projet de fans indépendant. Il n'est ni édité, ni approuvé, ni contrôlé par les ayants droit de Pokémon.
