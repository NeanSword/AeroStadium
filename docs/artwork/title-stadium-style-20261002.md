# AeroStadium — logo et Start, direction Pokémon Stadium

2 octobre 2026. Les deux fichiers sont des PNG RGBA créés avec le générateur intégré imagegen, avec un véritable alpha. Le fond natif 2560 × 1440 reste celui validé précédemment.

## Assets sélectionnés

- `Assets/AeroStadium/Resources/UI/AeroStadiumLogo.png` : 1774 × 887 ; deux lignes AERO jaune/bleu et STADIUM rouge, lettres massives à empattements, contours blancs/noirs.
- `Assets/AeroStadium/Resources/UI/AeroStadiumStart.png` : 2172 × 724 ; APPUYEZ SUR START, capitales droites jaunes bordées de bleu.

Les PNG originaux ne sont pas recadrés ni agrandis localement. Le rendu utilise une découpe UV mesurée, avec 8 pixels de marge autour de l'alpha visible (>16), pour éviter les marges transparentes inutiles et les pixels alpha résiduels isolés.

| Asset | Découpe PNG, origine en haut à gauche | UV Unity |
| --- | --- | --- |
| Logo | x0=10, y0=36, x1=1773, y1=856 | (10/1774, 31/887, 1763/1774, 820/887) |
| Start | x0=39, y0=219, x1=2132, y1=493 | (39/2172, 231/724, 2093/2172, 274/724) |

Le logo occupe un cadre de 740 × 345 dans le Canvas 1600 × 900, centré en x=430, y=36. Le visuel Start occupe 540 × 64, centré dans sa zone de clic inchangée 820 × 112. Le fond, le logo et Start restent des calques indépendants. Le clignotement, la manette, Entrée et le clic gardent leur fonctionnement.

Les versions précédentes et les nouvelles sorties sont archivées dans `output/imagegen/` avec les suffixes `before-stadium-v2-20261002` et `stadium-v2-20261002`. Les images restent locales et ignorées par Git.

## Prompts exacts

### Logo — générateur intégré, transparent_background=true

```text
Use case: logo-brand. Create a finished premium raster game logo on a genuinely transparent background for AeroStadium. Exact text in TWO stacked lines: "AERO" above "STADIUM"; no other lettering, no number 2. The requested artistic direction is the classic Pokémon Stadium Nintendo 64 title-screen spirit: bold friendly oversized golden-yellow AERO letterforms with the playful irregular upright geometry of a creature-adventure game, very thick royal-blue/navy edging and restrained blue extrusion; STADIUM underneath in powerful broad deep-red BLOCK SLAB-SERIF CAPITALS, short thick geometric serifs, strong beveled faces, dark red undersides, heavy black depth. STADIUM is the wider bottom line. Compact balanced lockup, readable open letter counters, subtle arched top line, close overlap between the two lines, very strong sculpted presence. Finish the combined mark with a clean thick WHITE silhouette keyline surrounded by a thin BLACK outer edge, as in classic late-1990s arena-battle game branding. Warm gold and vivid crimson, smooth painterly enamel gradients and precise clean contours, excellent quality for a 1440p title screen. An ORIGINAL AeroStadium mark with convincing old-school Pokémon Stadium character. Upright block letters, NO cursive, NO racing-game italic typography, NO blue wind swirls, NO swooshes, NO lightning bolts, NO futuristic chrome, NO badge, NO Poké Ball, no Pokémon word or Nintendo word. Only this two-line logo, no scenery, rectangle, panel, frame, checkerboard or solid backdrop; true alpha transparency. Center the full mark with only a small even transparent safety margin; the lettermark should fill most of the canvas.
```

### Start — générateur intégré, transparent_background=true

```text
Use case: logo-brand. Create a final high-resolution transparent raster title-screen prompt, in the classic Pokémon Stadium Nintendo 64 spirit. Exact French text on ONE straight line: "APPUYEZ SUR START". Bold compact UPRIGHT playful rounded BLOCK CAPITALS with consistent letter heights and calm regular spacing, friendly and immediately readable. Warm golden-yellow letter faces with a subtle light-yellow upper highlight and restrained orange lower shading; a thick clean ROYAL-BLUE outline, a very thin dark-navy outer edge and small dark drop shadow for readability over a colorful illustrated title background. Classic creature-battle console-game menu typography, polished sharp smooth outlines at 1440p, understated shallow relief; this should look like a classic Pokémon Stadium press-start instruction. NO slanted italic racing typography, NO huge dramatic 3D extrusion, NO script lettering, NO swooshes, NO metal, NO panel, NO oval, NO box, NO button frame or icon, NO scenery, no extra text, no watermark. Only the words on TRUE TRANSPARENT BACKGROUND, complete and horizontally centered, with a SMALL EVEN transparent margin around the line. Lettering nearly fills a wide low-height canvas. Unity will animate its blinking; generate the fully visible frame only.
```
## Validation

- Build Windows réussi : 0 erreur, 0 avertissement.
- Essai visible de 40 secondes : rendu des nouvelles images confirmé, 20 cycles avec phases visible et masquée. Clic sur le texte Start : passage au combat observé.
- Résultat final : errors=0, models=2, screen=Battle, titleAssetsVerified=True, blinkVisible=True, blinkHidden=True, passed=True. titleTextures=False après le clic indique seulement que les calques du titre ont été détruits ; la validation mémorisée reste True.
- Observation activée avant le runtime, puis arrêtée après fermeture. Capture de livraison : `output/imagegen/title-screen-stadium-style-preview-20261002.png`.
- Fond natif 1440p conservé à l'identique. Les images, leurs archives et les captures restent ignorées par Git. Les paramètres réécrits automatiquement par Unity ont été sauvegardés et rétablis à leur état antérieur au build.
