# AeroStadium

AeroStadium est un jeu Pokémon non officiel développé dans Unity, inspiré des combats en stade de Pokémon Stadium 2. Il possède son propre moteur de combat, un stade original et une interface adaptée à la manette. L'objectif est d'intégrer progressivement des Pokémon, objets et capacités récents, avec une catégorie physique ou spéciale propre à chaque attaque.

Le développement se fait dans [NeanSword/AeroStadium](https://github.com/NeanSword/AeroStadium). Le dépôt [Aero-Stadium-2-FR](https://github.com/NeanSword/Aero-Stadium-2-FR) reste une référence pour comprendre l'expérience recherchée. Ce nouveau projet ne dépend ni de son exécutable recompilé ni d'une ROM.

## Premier prototype

Le mode actuel est un combat solo **1 contre 1 face à une IA simple**. Le catalogue et les modèles locaux couvrent les 151 Pokémon de Kanto. La sélection d'équipe complète n'est pas encore proposée.

- Stade créé pour AeroStadium : terrain circulaire, tribunes, éclairage et tableau d'affichage.
- Interface en français : sélection du Pokémon et de l'objet, PV, quatre capacités, puissance, catégorie, PP, journal du combat, pause et résultat.
- Navigation au clavier, à la souris ou à la manette. Les indications suivent la famille de manette détectée, avec Xbox comme référence par défaut et des indications PlayStation et Nintendo. Le changement de périphérique est pris en compte pendant l'exécution.
- 151 modèles locaux de génération I, avec leur taille et placement réglés dans les prefabs Unity.
- Classification moderne des capacités : Nitrocharge utilise l'Attaque et la Défense, tandis que Lance-Flammes utilise l'Attaque Spéciale et la Défense Spéciale.

Le catalogue de combat utilise les données de génération I ; les autres générations, objets et capacités restent à intégrer.

Le moteur prend en charge les 18 types ordinaires, le STAB, la précision, les PP, la priorité, la Vitesse, quelques effets de capacités et trois objets tenus : Restes, Orbe Vie et Charbon. Les statistiques sont calculées au niveau 50, avec une nature neutre, 31 IV et aucun EV.

**Ce prototype n'est pas un moteur complet des générations récentes.** Les talents, coups critiques, altérations de statut, effets secondaires, météo, terrains, formes particulières et nombreux effets de capacités ou d'objets restent à développer. Les arrondis de dégâts sont simplifiés. L'IA privilégie des attaques efficaces et quelques actions de soin ou de préparation ; elle n'est pas conçue comme un adversaire compétitif.

Les 151 prefabs de Kanto sont préparés localement. Dans les GLB disponibles, seuls 19 modèles contiennent des animations, soit 162 clips ; 132 GLB sont statiques. Unity conserve chaque clip reçu et relie les rôles attaque, dégâts ou K.O. quand les noms les identifient sans ambiguïté. Ces clips communautaires ne sont pas vérifiés comme les animations officielles de chaque espèce ; voir [le relevé des animations](docs/GEN1_ANIMATIONS.md).

L'écran titre local affiche une illustration originale réunissant les neuf générations, un logo AeroStadium transparent et un texte Appuyez sur Start clignotant. Start, Entrée et le clic ouvrent le menu principal : Solo, Multijoueur local, Multijoueur en ligne et Options. Les quatre cartes animées mènent actuellement à la simulation de combat. La navigation à la manette possède un curseur Poké Ball et le menu dispose de sa propre musique en boucle. Voir [le menu principal](docs/MAIN_MENU.md) et [sa musique](docs/MENU_AUDIO.md). Les images restent locales ; leur résolution native et les prompts sont documentés dans [l'écran titre](docs/artwork/title-assets-20261002.md).

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

**Les modèles, textures et animations Pokémon ne sont pas distribués dans ce dépôt.** Un clone contient le code et les outils, mais pas les 151 modèles nécessaires à leur affichage. Les GLB, clips, matériaux, contrôleurs et prefabs locaux sont exclus de Git sous `Assets/AeroStadium/Resources/LocalModels`.

`Tools/prepare_generation_one.ps1` prépare les GLB et manifestes locaux. Le menu Unity importe les clips, crée un contrôleur par modèle et relie aux événements les mouvements reconnus par leur nom. Voir [le relevé des animations](docs/GEN1_ANIMATIONS.md) et [l'architecture](docs/ARCHITECTURE.md#modèles-et-présentation).

Les modèles locaux de génération I viennent de [Pokemon-3D-api/assets](https://github.com/Pokemon-3D-api/assets/tree/main/models/opt/regular), dont le pipeline récupère les GLB sources depuis Sketchfab. Les fichiers préparés, leurs empreintes et leurs informations de provenance sont conservés dans les manifestes locaux. Aucune ROM, aucun dump ni payload de modèle ou d'animation n'est inclus dans ce dépôt.

## État des vérifications

Au **1er octobre 2026**, la préparation et l'import Unity ont réussi. Les **16 vérifications du moteur de combat ont réussi**, notamment la classification physique/spéciale, les effets d'objets, les équipes, la validation des données et la reproductibilité avec une graine fixe.

La compilation Windows a réussi, sans erreur ni avertissement. Une session visible de 120 secondes a permis d'observer les modèles, le HUD et des combats allant jusqu'à leur résultat. Les **16 tests Input System ont réussi** : commandes Xbox/PlayStation/Nintendo, clavier, navigation, pause et débranchement, avec des périphériques simulés. Les essais de chaque modèle de manette sur du matériel réel restent à faire.

Le journal de reprise détaillé est [LastProgressLogs.md](LastProgressLogs.md). Les sources et crédits figurent dans [docs/CREDITS.md](docs/CREDITS.md).

Les prochaines étapes sont les équipes de trois Pokémon, le changement de Pokémon pendant le combat, l'ajout progressif de modèles et d'animations, puis l'extension des mécaniques et du catalogue. L'architecture est décrite dans [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

AeroStadium est un projet de fans indépendant. Il n'est ni édité, ni approuvé, ni contrôlé par les ayants droit de Pokémon.
