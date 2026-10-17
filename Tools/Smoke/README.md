# Textures locales de fumée

`generate_smoke_atlas.py` génère de façon déterministe les textures de présentation de Smogo (#109) et Smogogo (#110). Le script utilise NumPy et Pillow localement, sans réseau ni service de génération d'images.

Depuis la racine du projet :

```powershell
python Tools/Smoke/generate_smoke_atlas.py Assets/AeroStadium/Resources/NativeModels/SharedSmoke
```

Les PNG restent dans le répertoire des modèles natifs, ignoré par Git. L'importeur attend `NativeSmokeAtlas.png` (1024 px, Clamp) et `NativeSmokeFlow.png` (256 px, Repeat), tous deux linéaires, bilinéaires, non compressés et avec mipmaps. Le RGBA de l'atlas contient la densité, la normale projetée XY et l'auto-ombre. Son alpha est une donnée ; le shader calcule la transparence depuis la densité.

Les 16 volutes connectées possèdent des bords de tuiles de densité nulle. Une advection UV continue et bornée fait évoluer les plis sans changement d'image de flipbook. Les os, visibilités et tailles encodées des particules natives restent actifs. Le rendu est une adaptation pour AeroStadium ; il ne reproduit pas exactement le shader du jeu source.

## Cheminées et émission

`NativePersistentSmoke` émet depuis chaque cheminée vérifiée du corps : 22 pour #109, 21 pour #110 (14 sur la grande tête, 7 sur la petite). `NativeSmokeVentLayout` conserve les coordonnées des extrémités calculées depuis les éventails de triangles natifs et les matrices inverse-bind, avec un seul miroir pour glTFast. Chaque point suit son propre os de protubérance ; les appendices de mâchoire sont exclus. La validation compare indépendamment les sorties aux sommets skinnés du corps pour détecter une mauvaise conversion des coordonnées.

Chaque cheminée émet 0,8 petit nuage par seconde. Un pool borné à 96 conserve environ 76 à 80 nuages actifs, avec un plateau de densité de 3 s puis une disparition progressive de 1,5 s. L'opacité du matériau est de 0,72. Les nuages partent vers l'extérieur, puis montent et grossissent ; leur rayon initial vaut 3,6 à 4,4 % de la hauteur du modèle. UV5 porte l'alpha lié à la durée de vie ; UV4.A identifie chaque nuage de façon stable. Les cartes natives dépourvues d'UV5 gardent leur ancien comportement de shader.

L'émission continue tant que le corps est visible, en adaptant les drapeaux de fumée intermittente de la source. Les anciennes cartes `SmokeGeom` utilisent `forceRenderingOff` pendant l'émission du pool ; leurs drapeaux sont restaurés à la désactivation. Les animations et la visibilité du corps sont conservées. Un seul mesh dynamique et un seul matériau sont réutilisés hors de `ModelRoot`, ce qui exclut ces nuages des mesures de taille et de contact au sol du corps natif. Après un changement des émetteurs, préparer à nouveau les deux prefabs.

Fantominus utilise le shader distinct `NativeGasSurface`, avec une opacité de 0,85 et le mélange RGB violet de la source conservé.
