# Musique du menu principal

Source choisie par l’utilisateur : [Pokémon Center – Epic Pokémon Theme Remix](https://www.youtube.com/watch?v=YMBPE0KaOv4), chaîne [Epic PokeMix](https://www.youtube.com/channel/UC6rhI9J4hkPIXC6joginohg), publiée le 19 juin 2025. La description de la vidéo cite Junichi Masuda et Go Ichinose comme compositeurs originaux et indique un remix avec Suno AI. Ces mentions proviennent de la source et ne constituent pas une vérification indépendante des crédits de chaque mélodie.

Le fichier source décodé dure 219,684 s. Le segment retenu commence à 49,750770833 s et se termine à 117,515479167 s. Sa période de 67,764708333 s correspond à une répétition musicale mesurée d’environ 67,765 s : 192 pulsations à 170 BPM, ou 96 à 85 BPM. Le tempo et la hauteur restent inchangés.

Les dernières 2,823520833 s sont raccordées au pré-roll précédant le début, à la même phase musicale. Un fondu cosinus avec compensation de puissance évite la baisse d’énergie typique d’un mélange de deux formes d’onde peu corrélées. Les deux extrémités du fondu restent exactement celles de la source, avant gain et conversion PCM. Aucun silence n’est ajouté.

| Propriété | Valeur |
| --- | --- |
| Fichier préparé | AeroStadiumMenuTheme.wav |
| Format | PCM 16 bits, stéréo, 48 000 Hz |
| Durée | 67,764708333 s |
| Trames | 3 252 706 |
| Crête | −1,2 dBFS |
| RMS | −13,095724 dBFS, sans prétendre mesurer les LUFS |
| Échantillons écrêtés | 0 |
| Blocs de 100 ms sous −90 dBFS | 0 |
| Écart du raccord au saut naturel entre deux échantillons adjacents | 0,41 LSB |
| SHA-256 | 768AC02D25788B92A48315CA1BA60BE311E3397F32A1FAFF3315C45A8B014C74 |

Ressource Unity intégrée : `Assets/AeroStadium/Resources/Audio/AeroStadiumMenuTheme.wav`, chargée par `Audio/AeroStadiumMenuTheme`. Le fichier et son rapport JSON doivent rester locaux dans les dossiers ignorés par Git. La musique actuelle de titre conserve son fichier et son identité.

Le script de préparation généralisé reste compatible avec la piste de titre : un contrôle avec ses paramètres d’origine produit exactement le SHA-256 historique `D49BF774F9AE47FF6612E265DCCDBFD0CCD6D6A932A3C567431A526139CED354`. L’option `--preserve-crossfade-power` est activée uniquement pour cette nouvelle piste.

Commande de préparation, en remplaçant les chemins locaux :

```text
python Tools/prepare_title_music.py --input CHEMIN/YMBPE0KaOv4.webm --ffmpeg CHEMIN/ffmpeg.exe --output output/audio/AeroStadiumMenuTheme.wav --start 49.75076470588234 --duration 67.76470588235294 --crossfade 2.823529411764706 --preserve-crossfade-power --peak-dbfs -1.2 --source-url "https://www.youtube.com/watch?v=YMBPE0KaOv4" --source-title "Pokémon Center – Epic Pokémon Theme Remix" --source-credit "Epic PokeMix"
```

Ces résultats portent sur le fichier préparé. Ils ne remplacent pas l’essai de la transition titre → menu → combat et du bouclage dans Unity, qui doit être consigné séparément.

Une revue numérique indépendante a également contrôlé des fenêtres de 100 ms décalées à chaque échantillon, y compris le retour cyclique fin → début : minimum −18,5178 dBFS, aucune fenêtre silencieuse à −90 dBFS. L’écart maximal au saut naturel reste de 0,409 LSB. Au milieu du raccord, le niveau compensé se situe à +0,739 dB de la référence pondérée, alors qu’un mélange linéaire aurait créé un creux de −2,12 dB. Rapport local : `independent-loop-review.json`. Ce contrôle ne prétend pas avoir écouté la sortie des enceintes.

## Intégration et essai Unity

`MainMenuAudio` charge la piste locale en stéréo PCM avec `DecompressOnLoad`, `preloadAudioData` et conservation du taux d’échantillonnage. La source est 2D, `loop=true`, au volume nominal de 0,68. Elle réutilise l’AudioListener actif, monte progressivement sur 1,2 s et diminue sur 0,9 s. Les fondus utilisent le temps non affecté par la vitesse du jeu.

Start ouvre le menu et lance sa piste pendant le fondu de la musique du titre. B/Échap restaure la musique du titre et fait diminuer celle du menu. Chaque carte lance la simulation et arrête les deux sources après leur fondu. La piste du titre est conservée, avec son SHA-256 historique intact.

Compilation Windows réussie sans erreur ni avertissement. Essai visible `--menu-audio-test --seconds 90` : une boucle complète, lecture progressive, listener actif, transition en combat et arrêt après fondu ; `errors=0`, `menuMusicLoops=1`, `menuAudioStopped=True`, `passed=True`. Journal : `Logs/menu-audio-90s-20261002.log`. Essai des quatre routes et du clic souris : `Logs/main-menu-pointer-debug-20261002.log`, réussi. Observation activée avant les lancements puis arrêtée après fermeture.

La source WebM, les métadonnées YouTube, les rapports et les crédits déclarés sont conservés localement dans `output/audio/youtube-YMBPE0KaOv4`, ignoré par Git. Le WAV sous `Resources/Audio` est également ignoré. Le code, le script de préparation et la documentation restent suivis séparément.
