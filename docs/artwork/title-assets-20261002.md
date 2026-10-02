# Écran titre AeroStadium — 2 octobre 2026

Le titre assemble trois PNG indépendants. Le logo et le texte Start conservent une véritable transparence. Le clignotement est calculé par Unity, pas inclus dans le fond. Start, Entrée et le clic sur le texte lancent le combat.

## Fichiers locaux

| Fichier Unity | Taille native | Contenu |
| --- | --- | --- |
| `Assets/AeroStadium/Resources/UI/AeroStadiumTitle.png` | 2560 × 1440, RGB | Illustration du stade et neuf Pokémon, un par génération. |
| `Assets/AeroStadium/Resources/UI/AeroStadiumLogo.png` | 1774 × 887, RGBA | Logo AERO jaune/bleu et STADIUM rouge, direction Pokémon Stadium, transparent. |
| `Assets/AeroStadium/Resources/UI/AeroStadiumStart.png` | 2172 × 724, RGBA | Texte APPUYEZ SUR START jaune/bleu, capitales droites, transparent. |

Les originaux sélectionnés sont aussi conservés dans `output/imagegen/` avec le suffixe `20261002`. Le fond natif est archivé dans `aerostadium-title-background-1440p-20261002.png` ; le fond initial 1672 × 940 est préservé dans `aerostadium-title-background-before1440p-20261002.png`. Les PNG et leurs métadonnées Unity restent ignorés par Git. Le logo, le texte Start et le fond initial ont été créés avec le générateur intégré OpenAI ; le fond final a été retouché via l'API. Aucun modèle 3D des générations II–IX n'a été ajouté : les représentants des neuf générations font partie de l'illustration uniquement, et le roster jouable reste Kanto.

Le fond final est maintenant natif 1440p : l'API `gpt-image-2` a réussi une retouche en qualité haute, avec le fond approuvé comme image d'entrée et une sortie PNG RGB de 2560 × 1440 (6 164 548 octets). Le fichier a remplacé uniquement `AeroStadiumTitle.png`. Les deux premières tentatives avaient reçu `429 credit_balance_exhausted` et n'avaient produit aucune image ; cet échec est historique, les crédits API permettent désormais la génération. La clé n'a jamais été affichée ou enregistrée dans les fichiers du projet.

## Assemblage et vérification

`GameBootstrap.ShowTitle()` masque l'arène puis affiche le fond à son rapport d'aspect, le logo dans la zone supérieure centrale, et le texte Start près du bas. Les noms des ressources sont `UI/AeroStadiumTitle`, `UI/AeroStadiumLogo` et `UI/AeroStadiumStart`.

Les PNG transparents utilisent des découpes UV mesurées, sans modifier leurs pixels : logo x=10, y=36, droite=1773, bas=856 dans 1774 × 887 ; Start x=39, y=219, droite=2132, bas=493 dans 2172 × 724. L'axe Y des UV Unity est inversé. Le logo est centré dans 740 × 345, et le visuel Start dans 540 × 64, à l'intérieur de la zone de clic 820 × 112. Les prompts actuels et les détails sont dans [title-stadium-style-20261002.md](title-stadium-style-20261002.md). Mettre ces coordonnées à jour si les images sont remplacées.

Cycle du texte : 1,35 s visible, 0,10 s de disparition, 0,20 s masqué, 0,15 s de réapparition. La zone de clic ne clignote pas. Une légère pulsation agit seulement sur le visuel. Une petite indication suit la manette ou le clavier détecté.

`ProjectBuild` prépare les trois textures en sRGB, Clamp, Bilinear, sans mipmaps ni compression destructive, avec les dimensions originales préservées et une limite de 4096 pixels. L'alpha est conservé pour le logo et le prompt.

`--skip-intro` ouvre directement le titre pour les essais. `--title-test --seconds 120` reste interactif et valide la présence des trois textures, l'absence de modèles 3D sur le titre et au moins deux cycles avec des phases visibles et masquées. Attendre au moins 3,6 secondes sur le titre avant de lancer le combat avec Start/Entrée ou un clic. La validation des images et les observations du clignotement restent mémorisées après cette transition ; le contrôle final tient compte du nombre de modèles attendu sur la page courante. Le démarrage ordinaire conserve l'introduction.

## Prompts finaux

### Fond initial — générateur intégré

```text
Create a beautiful final background illustration for a Pokémon fan game's title screen. Landscape 16:9, highest available resolution and fine detail, suitable for a 1440p monitor. A brand new original composition: a grand outdoor tournament stadium amid a lush valley with trees, distant mountains, waterfalls, colorful stands, brilliant blue sky and warm sunlight. Polished colorful anime game key art with smooth accurate character outlines, expressive faces, richly shaded volume and detailed environment. Show exactly nine recognizable Pokémon together, one representative of each generation: Pikachu (1), Umbreon (2), Gardevoir (3), Lucario (4), Zoroark (5), Greninja (6), Rowlet (7), Corviknight (8), Sprigatito (9). Use their familiar normal colors and accurate original anatomy. Ground Pokémon in energetic friendly natural poses around the lower half and side thirds; Corviknight flying at the side and Rowlet perched near the ensemble. Individual readable silhouettes, appropriate relative size, no merged bodies or duplicate characters. Preserve a large calm patch of open sky in the upper center for a separate logo and a quiet ground strip near the lower center for a separate Start prompt. Draw the background and characters ONLY, with no text, logo, letters, button, border or watermark. Bright, magical, joyful, authentic Pokémon world atmosphere; finished professional game art.
```

### Fond natif 1440p — retouche API

Le prompt exact de la retouche est conservé dans [title-background-1440p-prompt.txt](title-background-1440p-prompt.txt). La CLI fournie par la compétence imagegen a utilisé le mode `edit` de `gpt-image-2`, qualité `high`, taille `2560x1440`, avec le fond initial comme référence pour conserver sa composition. Le logo et le texte Start restent les sorties transparentes du générateur intégré.

### Logo initial — générateur intégré, transparence native (historique)

```text
Use case: logo-brand. Asset type: final transparent raster logo for AeroStadium, a Pokémon-inspired fan game. Create a striking original high-quality game wordmark on TRUE TRANSPARENT BACKGROUND. Exact text, once: "AeroStadium" (capital A and S; letters A e r o S t a d i u m). No number 2, subtitle, extra lettering or trademark marks. Original playful athletic typography with large smoothly sculpted angular rounded letters, joyful adventurous creature-battling game energy. Bright rich golden-yellow Aero lettering and warm red Stadium accents, powerful cobalt-blue outer outline, clean white keyline, deep navy three-dimensional extrusion, crisp glossy beveled highlights. A subtle sweeping wind/swoosh motif may connect the wordmark, fitting the name Aero, but keep the letterforms perfectly legible. Balanced compact two-tier wordmark permitted: Aero above Stadium, tightly unified as one brand. Inspired by the colorful bold spirit of Pokémon game logos while clearly a NEW AeroStadium design. Refined premium illustration quality, sharp smooth edges, attractive substantial volume. Centered complete logo with transparent padding around all edges, no rectangle, mockup, scenery, checkerboard pattern or solid background. Large high-resolution asset that remains crisp at 1440p.
```

### Start initial — générateur intégré, transparence native (historique)

```text
Use case: logo-brand. Asset type: isolated transparent start-prompt text for a polished Pokémon-inspired game title screen. Exact French text on ONE LINE, once: "APPUYEZ SUR START". Perfect spelling A P P U Y E Z / S U R / S T A R T. Create original stylish bold rounded lettering, playful adventurous energetic game typography matching a bright gold and cobalt blue title logo, carefully polished bright golden enamel letter faces, strong clean cobalt blue outline, thin white highlight keyline and restrained deep blue dimensional shadow. Smooth crisp contours, subtle glossy bevel and excellent readability at smaller screen sizes. Only the stylized words: no panel, rectangle, oval badge, frame, button icon, Pokémon, illustration, scenery, extra wording, stars or watermark. GENUINE transparent background with generous alpha padding, highest available detail, horizontally centered. This is the visible frame; blinking will be animated by the game, so render complete readable text.
```
## Résultats du 2 octobre 2026

- Build Windows final : Succeeded, 0 erreur, 0 avertissement.
- Session visible de 120 secondes : titre et lancement du combat à la manette observés, 0 erreur du jeu. Le diagnostic initial attendait à tort une fermeture sur la page titre ; sa validation a ensuite été corrigée pour rester mémorisée après Start.
- Contrôles finaux sur le titre : 45 s (26 cycles) et 60 s (34 cycles), phases visible et masquée détectées, trois textures validées, 0 modèle 3D sur le titre, passed=True.
- Le dernier contrôle est resté sur le titre pendant l'activité de l'utilisateur. La transition vers le combat est inchangée depuis la session de 120 secondes ; le nouveau contrôle mémorisé a également été relu dans le code.
- Les paramètres Unity réécrits automatiquement pendant le build ont été sauvegardés dans Builds/Reports puis rétablis ; les changements précédents du projet ont été conservés.
- Après deux tentatives initiales refusées pour solde insuffisant, la retouche API 1440p a réussi : PNG RGB natif 2560 × 1440 installé localement, fond initial sauvegardé. La session utilise bien la clé utilisateur Windows actuelle.
## Validation du fond natif 1440p

Après recharge, la retouche API a réussi en 80,9 secondes. Le PNG de 2560 × 1440 RGB a été inspecté, installé et reconstruit dans le jeu ; son original et le fond précédent restent archivés localement.

Le build Windows final est Succeeded, 0 erreur, 0 avertissement. Le contrôle visible de 30 secondes, dans une fenêtre 1600 × 900, a confirmé le fond, le logo et le texte Start séparés : errors=0, models=0, screen=Title, titleAssetsVerified=True, blinkCycles=17, blinkVisible=True, blinkHidden=True, passed=True. Ce contrôle concerne la nouvelle image ; les contrôles précédents restent décrits plus haut. La taille native du fichier a été vérifiée séparément, car le contrôle runtime ne teste pas les dimensions des textures.

La capture de livraison est `output/imagegen/title-screen-native1440-preview-20261002.png`. L'observation a été activée avant le runtime puis arrêtée après sa fermeture. Aucun code de combat ou de contrôle manette n'a été modifié pour cette retouche.
