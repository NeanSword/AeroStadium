# Références Stadium et présentation moderne

Le catalogue local contient 48 décors issus des archives natives : 30 de Stadium 2 et 18 de Stadium 1. Il s'agit de 48 enregistrements, dont certains ne sont pas des stades de combat distincts. Les sources et ressources récupérées restent locales et ignorées par Git.

## Comparaison avec les décors d'origine

```powershell
Builds/WindowsReference/AeroStadium.exe --stadium-original --stadium-review
```

La revue du 9 octobre 2026 a chargé les 48 décors et deux Pokémon animés, sans erreur technique et avec une sortie 0. Ce résultat vérifie leur chargement ; la fidélité artistique et pixel par pixel reste à établir.

`NativeN64Sampler.hlsl` applique l'adressage entier clamp/mirror/mask. Les métadonnées F5/F2 couvrent les 534 liaisons de texture primaires et les 81 secondaires de Stadium 2, y compris les descripteurs de première image des callbacks démontrés par la source. Le filtrage nearest est volontaire ; le filtrage N64 à trois points et l'ensemble des états de couverture/profondeur RDP restent à reproduire. Le défilement des textures en cours d'exécution et le callback `0x81000150` sont incomplets.

Le shader reconstruit l'alpha I4/I8 depuis l'intensité encodée avant le combiner ; IA/RGBA conservent leur alpha indépendant. Les PNG d'origine sont inchangés. Stadium 1 applique le combiner natif selector4 à 36 groupes layer5 vérifiés dans 12 décors, avec deux cycles, les couleurs primitives de la source et LOD1 par défaut. Les autres selectors, les overrides FC explicites et les états hérités de rendu/éclairage à la racine restent à retrouver. Le marquage au sol d'arena19/21 est encore visuellement incomplet.

## Orientation et placement

L'orientation du combat suit les sols rectangulaires vérifiés de huit entrées : `arena_14`, `arena_19`, `arena_21`, `cianwood_gym`, `stadium1_brock`, `stadium1_blaine`, `stadium1_giovanni` et `stadium1_surge`. Le fichier local `Resources/Stadiums/battle-layouts.json` conserve leurs centres bruts, grands axes et limites du sol. Les points de combat et trajectoires de caméra natifs restent non vérifiés ; les autres entrées gardent leur placement précédent.

Les sauvegardes et preuves de cette passe se trouvent dans `LocalStadiumSources/NativeMaterialFix_20261009`. La version stable `Builds/Windows` est conservée. Pour construire la version de comparaison :

```text
ProjectBuild.BuildWindows --reuse-prepared-assets --prepare-stadiums --stadium-only-validation --stadium-reference-build
```

`--stadium KEY` sélectionne un décor. [LastProgressLogs.md](../../LastProgressLogs.md) contient la provenance, les observations et les travaux de fidélité restants. La validation de chaque référence historique précède sa réinterprétation moderne.
