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
| [io_scene_gfbanm](https://github.com/Shararamosh/io_scene_gfbanm) | Référence de recherche pour importer les formats Switch `.gfbanm`/`.tranm` ; ni son code ni les fichiers de jeu ne sont intégrés ici. |
| [.NET](https://dotnet.microsoft.com/) | Vérifications du moteur sans Unity. |
| [Pokémon Showdown / Smogon](https://github.com/smogon/pokemon-showdown/blob/master/data/typechart.ts) | Référence consultée pour vérifier la table des types ; aucune dépendance d'exécution. |

Les licences et conditions de ces projets restent applicables à leurs composants respectifs.

## Modèles utilisés localement

| Pokémon | Jeu source | Page et contributeur |
| --- | --- | --- |
| Germignon | Pokémon Écarlate / Violet, Nintendo Switch | [The Models Resource, asset 468036](https://models.spriters-resource.com/nintendo_switch/pokemonscarletviolet/asset/468036/) — Poké-Brother. |
| Ho-Oh | Pokémon Écarlate / Violet, Nintendo Switch | [The Models Resource, asset 352007](https://models.spriters-resource.com/nintendo_switch/pokemonscarletviolet/asset/352007/) — stormygaret15. |
| Les 151 Pokémon de Kanto | Modèles standards préparés par Pokémon 3D API | [Pokemon-3D-api/assets](https://github.com/Pokemon-3D-api/assets/tree/main/models/opt/regular) — seuls les fichiers réguliers `1.glb` à `151.glb` sont utilisés localement ; aucun modèle chromatique. |

Les crédits disponibles dans les métadonnées individuelles sont listés dans [GEN1_MODEL_ATTRIBUTIONS.md](GEN1_MODEL_ATTRIBUTIONS.md). Le README de Pokemon-3D-api/assets indique que son pipeline récupère les GLB depuis Sketchfab ; la provenance des clips embarqués n'est pas vérifiée comme officielle pour chaque espèce. Les modèles, textures et animations restent locaux. Les manifestes consignent les empreintes et les adaptations effectuées. Voir [GEN1_ANIMATIONS.md](GEN1_ANIMATIONS.md) pour le mapping des clips et l'import des animations natives plus tard.

## Audio

La musique active de l’écran titre est un extrait mis en boucle de [« Battle! L - Remix Cover (Pokémon Legends: Z-A) »](https://www.youtube.com/watch?v=Joo7_AKDKi4), publié par [Vetrom](https://www.youtube.com/channel/UCc8Z-QX87IY--16O9unVXpQ) et choisi par l’utilisateur. La description de la vidéo crédite la composition à **Minako Adachi, Hiromitsu Maeba, Carlos Eiene, Shinji Hosoe, Ayako Saso, Takahiro Eguchi, Hitomi Sato et Shota Kageyama**, et porte la mention **« Music by Vetrom »**. Elle crédite également **Mixeli** pour les images de jeu de [la vidéo source citée](https://www.youtube.com/watch?v=Orj3598VrfU).

AeroStadium a préparé localement le découpage, le raccord de boucle et le niveau du WAV. Le traitement hors du jeu utilise [FFmpeg](https://ffmpeg.org/) et NumPy via `Tools/prepare_title_music.py`. La boucle dure 111,4285625 s et conserve le tempo de la source. Les crédits de la musique et ses droits restent ceux de leurs ayants droit respectifs. La provenance et les détails techniques figurent dans [TITLE_AUDIO.md](TITLE_AUDIO.md) et dans le rapport local `output/audio/AeroStadiumTitleTheme.json`.

Les cris de [Pikachu #25](https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon/latest/25.ogg), [Noctali #197](https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon/latest/197.ogg) et [Lucario #448](https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon/latest/448.ogg) proviennent des jeux Pokémon et ont été obtenus via [PokéAPI/cries](https://github.com/PokeAPI/cries). La [licence du dépôt](https://github.com/PokeAPI/cries/blob/main/LICENSE) attribue les droits des contenus audio à The Pokémon Company et distingue ces contenus du dépôt distribué sous CC0. Les OGG ont été décodés en WAV mono, à leur fréquence d’origine, puis normalisés à une crête de −1,2 dBFS, sans changement de hauteur ni de durée. La provenance et les mesures de préparation sont conservées dans `output/audio/Cries-LICENSE.txt` et `output/audio/cry-normalization.json`.

Les anciennes compositions originales « Au sommet de l’arène » (v1) et « Le serment des champions » (v2) sont conservées dans `output/audio/archive-v1/` et `output/audio/archive-v2/`. Elles avaient été rendues avec [FluidSynth 2.6.1](https://github.com/FluidSynth/fluidsynth/releases/tag/v2.6.1), sous [LGPL 2.1](https://github.com/FluidSynth/fluidsynth/blob/v2.6.1/LICENSE), et [GeneralUser GS 2.0.3](https://github.com/mrbumpy409/GeneralUser-GS) de S. Christian Collins. La [licence de cette banque](https://github.com/mrbumpy409/GeneralUser-GS/blob/main/documentation/LICENSE.txt) autorise la création musicale privée ou commerciale et précise les limites de provenance de certains échantillons ; sa copie locale est `output/audio/GeneralUser-LICENSE.txt`. Les crédits FluidSynth et GeneralUser GS concernent ces compositions archivées.

Les fichiers audio préparés, les cris et les archives restent locaux et ignorés par Git. Les licences et droits des outils, banques et enregistrements demeurent applicables à leurs composants respectifs.

## Musique du menu principal

Piste active : [Pokémon : Main Title Intro [EPIC COVER] (Fan music)](https://www.youtube.com/watch?v=JckTGvghi0k), chaîne [Alexis DL](https://www.youtube.com/channel/UCi0rPiqZNemz3hV40NPePOA). La description crédite Jun’ichi Masuda pour la composition et Alexis DL pour l’orchestration et l’arrangement. AeroStadium réalise le découpage, le raccord et le niveau de la boucle locale. Les métadonnées et ces crédits sont sauvegardés pour les crédits finaux dans `output/audio/youtube-JckTGvghi0k/request-and-credits.json`. Voir [MENU_AUDIO.md](MENU_AUDIO.md).

L’ancienne piste [Pokémon Center – Epic Pokémon Theme Remix](https://www.youtube.com/watch?v=YMBPE0KaOv4), Epic PokeMix, reste archivée localement. La musique du titre reste celle de Vetrom. Les sources et enregistrements audio sont locaux et ignorés par Git.

## Références et inspection des animations

Les [GIFs de génération I publiés par theSLAYER sur Project Pokémon](https://projectpokemon.org/home/docs/spriteindex_148/3d-models-generation-1-pokémon-r90/) ont servi à observer les attitudes et les mouvements secondaires. Aucun GIF ni courbe de cette référence n'est intégré au jeu. Les performances procédurales et les articulations générées de cette étape sont originales et restent en validation.

L'inspection locale du dump fourni utilise les schémas et listes de noms documentés par [PokeDocs](https://github.com/pkZukan/PokeDocs), la définition d'empreinte de [GFTool](https://github.com/pkZukan/gftool) et les descriptions d'archives de [SCVI_Extract](https://github.com/psthrn42/SCVI_Extract). Ces outils de documentation sont distincts des animations natives Pokémon. Aucun fichier du dump n'est publié ou intégré à cette étape. Voir [GEN1_ANIMATIONS.md](GEN1_ANIMATIONS.md).

Après autorisation de l'utilisateur, un pilote Pikachu natif (modèle, squelette, textures et animations) est extrait localement du ROMFS fourni. La conversion des textures utilise les modules non modifiés de [BNTX Extractor](https://github.com/aboood40091/BNTX-Extractor), AboodXD, sous GPL-3.0-or-later, avec licence et hashes conservés dans le workbench local ; les champs BRTI modernes sont documentés par [BNTX Editor](https://github.com/aboood40091/BNTX-Editor). Pillow décode les blocs BCn. Ces outils hors du jeu sont distincts des fichiers natifs Pokémon ; aucun asset du dump n'est publié. Le rendu PBR de test n'est pas une reproduction complète des shaders du jeu source.

L'inspection du second ROMFS Unity fourni utilise [UnityPy 1.25.2](https://github.com/K0lb3/UnityPy), lecteur sous MIT installé uniquement dans le workbench local. Les modèles et AnimationClip natifs sont distincts de cet outil. Les vérifications indépendantes des courbes s'appuient sur les formats documentés dans [AssetStudio AnimationClip](https://github.com/Perfare/AssetStudio/blob/master/AssetStudio/Classes/AnimationClip.cs) ; aucun modèle ou clip de ce second dump n'est publié ou intégré à cette étape. Rapports : `output/animations/bdsp-inspection`.


Contrôles de matériaux natifs : comportement des propriétés et os documenté par le code source public [TeamLumi/opendpr](https://github.com/TeamLumi/opendpr), classes PokemonCustomNodeAnim / CustomNodeMaterial / CustomNodeVisibility. Le projet utilise une adaptation URP locale distincte. Les sources Pokémon et dérivés natifs du second dump restent locaux ; les outils UnityPy et glTFast ne sont pas les auteurs de ces ressources. Le pilote #001 est maintenant importé techniquement ; les contrôles visuels et l'import complet restent en cours.
