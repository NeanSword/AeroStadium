# Sources et crédits

AeroStadium est un projet de fans indépendant développé pour NeanSword. Il n'est ni édité, ni approuvé, ni contrôlé par les ayants droit de Pokémon. Les noms, personnages et modèles Pokémon restent des créations de leurs ayants droit ; le présent dépôt ne distribue pas les modèles ou leurs textures.

## Développement

Le moteur de combat C#, le stade, les menus, l'adaptation des commandes et les outils d'import sont propres à ce projet. Le dépôt [Aero-Stadium-2-FR](https://github.com/NeanSword/Aero-Stadium-2-FR) sert de référence pour l'expérience recherchée, sans dépendance d'exécution ni reprise d'une ROM.

## Outils et dépendances

| Projet | Utilisation |
| --- | --- |
| [Unity](https://unity.com/) | Éditeur et moteur du jeu. |
| [Universal Render Pipeline](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/index.html) | Rendu du stade, matériaux et éclairage. |
| [Unity Input System](https://github.com/Unity-Technologies/InputSystem) | Clavier, souris, manettes et usages Submit/Cancel. |
| Unity uGUI et Test Framework | Interface et vérifications dans l'éditeur. |
| [Blender](https://www.blender.org/) | Conversion et vérification des modèles locaux. |
| [glTF Transform](https://gltf-transform.dev/) | Normalisation locale des GLB (Draco, WebP et accessoires clairsemés). |
| [.NET](https://dotnet.microsoft.com/) | Vérifications du moteur sans Unity. |
| [Pokémon Showdown / Smogon](https://github.com/smogon/pokemon-showdown/blob/master/data/typechart.ts) | Référence consultée pour vérifier la table des types ; aucune dépendance d'exécution. |

Les licences et conditions de ces projets restent applicables à leurs composants respectifs.

## Modèles utilisés localement

| Pokémon | Jeu source | Page et contributeur |
| --- | --- | --- |
| Germignon | Pokémon Écarlate / Violet, Nintendo Switch | [The Models Resource, asset 468036](https://models.spriters-resource.com/nintendo_switch/pokemonscarletviolet/asset/468036/) — Poké-Brother. |
| Ho-Oh | Pokémon Écarlate / Violet, Nintendo Switch | [The Models Resource, asset 352007](https://models.spriters-resource.com/nintendo_switch/pokemonscarletviolet/asset/352007/) — stormygaret15. |
| Les 151 Pokémon de Kanto | Modèles standards préparés par Pokémon 3D API | [Pokemon-3D-api/assets](https://github.com/Pokemon-3D-api/assets/tree/main/models/opt/regular) — seuls les fichiers réguliers `1.glb` à `151.glb` sont utilisés localement ; aucun modèle chromatique. |

Les crédits disponibles dans les métadonnées individuelles sont listés dans [GEN1_MODEL_ATTRIBUTIONS.md](GEN1_MODEL_ATTRIBUTIONS.md), avec leur licence déclarée. Ces contributeurs sont les personnes indiquées sur les pages sources, pas une attribution des droits sur les personnages. Tous les fichiers 3D et textures restent locaux. Les manifestes consignent les empreintes et les adaptations effectuées. Les modèles et personnages Pokémon restent la propriété de leurs ayants droit. L'animation de repos de Germignon est une création de prévisualisation ; aucune animation originale du jeu Switch n'est revendiquée.
