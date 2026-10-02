# Illustration du menu principal — prompt de conception

2 octobre 2026.

## Intention

Créer une scène inédite pour le menu principal d’AeroStadium, dans l’esprit visuel d’un jeu Pokémon de combat en arène. L’illustration doit fonctionner derrière de véritables éléments d’interface : la moitié gauche reste calme et sombre, tandis que les Pokémon et la lumière du stade animent la droite.

## Prompt

```text
Create a new premium cinematic Pokémon game key art illustration for the main menu of AeroStadium, in a wide 16:9 landscape composition. A magnificent original outdoor Pokémon stadium at night, with deep indigo sky, luminous arena architecture, blue and gold floodlights, atmospheric depth and a sense of anticipation before a championship battle. Polished high quality game illustration, expressive and recognizable Pokémon, crisp anatomy and detailed materials, coherent lighting and a strong competitive Pokémon atmosphere.

Reserve the left side, approximately the left half of the image, as a calm dark indigo area with subtle stadium silhouettes and soft lighting. This negative space will later receive four interactive menu cards. Keep bright focal points and character silhouettes away from this left area.

On the right side, arrange exactly four distinct Pokémon in a believable cinematic scene: Charizard, Blastoise, Venusaur and Pikachu. Use their normal original species forms and normal colors, never shiny variants. Show confident natural battle-ready poses, individual readable silhouettes and correct characteristic anatomy. Charizard with its proper wings and tail flame; Blastoise with its characteristic shell and two cannons; Venusaur with its large flower and broad leaves; Pikachu with its recognizable ears and lightning-shaped tail. Keep them separate and unmistakable, with no merged bodies, no hybrids, no extra creatures, and no invented transformations. Compose them as a cohesive group within the stadium environment rather than collectible figurines.

This must be a fresh scene, not a copy of the previous title screen. No user interface in the illustration. No text, no lettering, no logo, no menu cards, no button, no press-start prompt, no watermark, no frame. The scene and its Pokémon are the entire artwork; all interface elements will be built separately in Unity.
```

## Production et ressource

L’illustration a été générée par le générateur d’images intégré à Codex, sans utilisation du fallback CLI ou de la clé API locale. Le PNG intégré conserve ses dimensions natives de 1672 × 941 pixels ; l’interface le cadre par UV pour couvrir la surface 16:9.

- Ressource locale : `Assets/AeroStadium/Resources/UI/AeroStadiumMenuBackground.png`.
- Chemin de chargement Unity : `UI/AeroStadiumMenuBackground`.
- SHA-256 : `DE136409EE52EACC1930DFC136A9D08824666B767A396FC3124BB8D2DCB25DC0`.
- Image locale ignorée par Git ; ce document est destiné au suivi dans le dépôt.

Le logo AeroStadium, les quatre cartes, les indications des commandes et le curseur Poké Ball sont des couches d’interface indépendantes. Aucun de ces éléments n’est incorporé au fond.

## Validation visuelle

Validation dans le runtime en attente. Compléter après observation de la lisibilité des cartes, de la visibilité des quatre Pokémon et du cadrage à 1600 × 900.
