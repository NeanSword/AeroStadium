# Menu principal AeroStadium

2 octobre 2026 — interface de sélection affichée après l’écran titre.

## Parcours du prototype

L’introduction mène à l’écran titre. Une pression sur Start, Entrée ou le bouton « Appuyez sur Start » ouvre désormais le menu principal. Celui-ci présente quatre cartes :

| Entrée | Intention | Comportement actuel |
| --- | --- | --- |
| Solo | Modes de jeu en solo. | Lance la simulation de combat existante. |
| Multijoueur local | Affrontements sur la même machine. | Lance la simulation de combat existante. |
| Multijoueur en ligne | Affrontements à distance. | Lance la simulation de combat existante. |
| Options | Paramètres du jeu. | Lance la simulation de combat existante. |

Les quatre boutons passent par le même point d’entrée de combat dans `GameBootstrap`. Ils constituent les accès visuels des futurs modes et paramètres ; leurs destinations actuelles répondent au parcours de démonstration demandé. B à la manette ou Échap au clavier ramène du menu à l’écran titre.

## Présentation

Le menu est construit sur un canevas de référence de 1600 × 900, adapté à la taille de la fenêtre par le `CanvasScaler` existant. Quatre cartes verticales occupent la gauche. Une illustration de stade nocturne expose Dracaufeu, Tortank, Florizarre et Pikachu sur la droite. Une ombre en dégradé protège la lecture des textes sans faire partie de l’illustration.

Chaque carte possède son propre emblème, un titre, une description, une bordure colorée et une flèche. Le survol ou la sélection éclaircit sa surface, modifie les couleurs du texte et produit un léger agrandissement. Le logo AeroStadium reste un élément distinct du fond. Les panneaux, icônes et le curseur sont dessinés par le code à la résolution du canevas.

L’image de fond contient uniquement la scène et ses Pokémon. Elle ne contient aucun menu, logo, texte ni bouton intégré : les zones interactives et leurs animations sont de véritables éléments d’interface Unity.

## Commandes

| Périphérique | Navigation | Validation | Retour au titre |
| --- | --- | --- | --- |
| Clavier | Flèches haut et bas. | Entrée. | Échap. |
| Souris | Survol des cartes. | Clic gauche sur une carte. | Échap au clavier. |
| Manette | Stick ou croix directionnelle, haut et bas. | Touche de confirmation du contrôleur. | Touche d’annulation du contrôleur. |

La sélection commence sur Solo. La navigation verticale est cyclique entre les quatre cartes. Les indications de touches suivent la famille de la manette : Xbox par défaut, PlayStation ou Nintendo lorsqu’elle est reconnue par `ControllerHints`.

Un curseur Poké Ball apparaît à gauche de la carte sélectionnée lorsque la manette est utilisée. `ControllerHints.UsingGamepad` suit l’entrée utilisée et permet de masquer ce curseur lors du retour au clavier ou à la souris. Le curseur et les éléments décoratifs ne reçoivent pas de raycasts ; ils n’interceptent pas les clics des boutons.

## Musique

Le menu possède désormais sa propre boucle : « Pokémon Center – Epic Pokémon Theme Remix », choisie par l’utilisateur. Le composant `MainMenuAudio` la lance avec un fondu pendant que `TitleScreenAudio` diminue la musique du titre. B/Échap effectue la transition inverse. La sélection d’une carte fait décroître les sources avant leur arrêt au combat. Voir [MENU_AUDIO.md](MENU_AUDIO.md) et [TITLE_AUDIO.md](TITLE_AUDIO.md).

## Fichiers d’intégration

| Fichier | Rôle |
| --- | --- |
| `Assets/AeroStadium/Presentation/MainMenuView.cs` | Construction et animation des cartes, fond, emblèmes, focus et curseur. |
| `Assets/AeroStadium/Presentation/GameBootstrap.cs` | Transitions titre, menu et combat ; callbacks des quatre cartes. |
| `Assets/AeroStadium/Presentation/ControllerHints.cs` | Famille de manette, indications des touches et suivi de l’entrée utilisée. |
| `Assets/AeroStadium/Presentation/MainMenuAudio.cs` | Boucle dédiée du menu et fondus. |
| `Assets/AeroStadium/Presentation/TitleScreenAudio.cs` | Musique et cris du titre, transition vers le menu. |
| `Assets/AeroStadium/Resources/UI/AeroStadiumMenuBackground.png` | Illustration locale chargée par `UI/AeroStadiumMenuBackground`. |
| `Assets/AeroStadium/Resources/UI/AeroStadiumLogo.png` | Logo existant utilisé comme couche séparée. |

L’illustration du menu a été créée avec le générateur d’images intégré à Codex. Elle n’a pas été produite par le fallback CLI ni par une requête effectuée avec la clé API locale. Le prompt de conception est conservé dans [artwork/main-menu-background.prompt.md](artwork/main-menu-background.prompt.md).

| Propriété du PNG intégré | Valeur |
| --- | --- |
| Dimensions natives | 1672 × 941 pixels. |
| Composition | Paysage proche de 16:9, ajusté par cadrage UV à l’affichage 16:9. |
| SHA-256 | `DE136409EE52EACC1930DFC136A9D08824666B767A396FC3124BB8D2DCB25DC0` |

Le PNG et les autres ressources locales du dossier `Resources/UI` restent ignorés par Git. Le code, ses fichiers `.meta` et la documentation sont destinés au suivi dans le dépôt.

## Validation

- Compilation Windows Unity 6000.3.25f1 réussie : zéro erreur et zéro avertissement.
- 31/31 tests Input System réussis avec périphériques simulés : changement d’entrée, branchement/retrait, Xbox/PlayStation/Nintendo et nettoyage des callbacks.
- Essai automatique visible final de 55 s avec la nouvelle musique : quatre routes vers la simulation, garde du double Start, D-pad, curseur, clavier, retour B, transitions audio. La dernière route Options est validée par un véritable survol et un clic MouseState via le raycast et l’InputSystemUIInputModule. `errors=0`, `menuRoutes=4`, `menuRouteMask=15`, `menuMouseClick=True`, `passed=True`. Journal : `Logs/main-menu-pointer-debug-20261002.log`.
- Essai audio visible de 90 s : une boucle complète de la piste du menu, progression de la lecture, passage Solo et fondu d’arrêt de la musique validés, zéro erreur. Journal : `Logs/menu-audio-90s-20261002.log`.
- Essai interactif précédent de 60 s : navigation matérielle à la manette observée, 46 changements de sélection, zéro erreur sur le menu. Les actions clavier/souris de Codex étaient interrompues par les entrées de l’utilisateur ; elles n’ont pas été déclarées comme un essai manuel achevé.
- Observation activée avant chaque lancement et arrêtée après fermeture. Capture : `output/imagegen/main-menu-preview-20261002.png`.

Le premier scénario automatique avait reçu des commandes matérielles pendant ses assertions. Les modes de test suspendent maintenant les entrées matérielles uniquement dans l’Input System de cette instance du jeu, avec `keepSendingEvents: true`, puis les restaurent. Aucune commande de désactivation du matériel n’est envoyée. Le fonctionnement interactif conserve ses entrées normales. Ces scénarios ne remplacent pas un essai matériel de chaque modèle de manette.

Scénario du menu : `AeroStadium.exe --main-menu-test --seconds 60`. Scénario audio : `AeroStadium.exe --menu-audio-test --seconds 90`. Accès interactif direct au titre : `AeroStadium.exe --skip-intro`.
