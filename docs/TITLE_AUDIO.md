# Musique de titre AeroStadium

2 octobre 2026 — thème actif : « Battle! L - Remix Cover (Pokémon Legends: Z-A) », version musicale de Vetrom, choisie par l’utilisateur.

## Morceau et crédits

L’écran titre utilise un extrait mis en boucle de [Battle! L - Remix Cover (Pokémon Legends: Z-A)](https://www.youtube.com/watch?v=Joo7_AKDKi4), publié sur la chaîne [Vetrom](https://www.youtube.com/channel/UCc8Z-QX87IY--16O9unVXpQ). AeroStadium a préparé le découpage, le raccord de boucle et le niveau du fichier utilisé localement.

La description de la vidéo donne les crédits suivants :

- **Composition** : Minako Adachi, Hiromitsu Maeba, Carlos Eiene, Shinji Hosoe, Ayako Saso, Takahiro Eguchi, Hitomi Sato, Shota Kageyama.
- **Musique** : Vetrom, selon la mention « Music by Vetrom ».
- **Images de jeu de la vidéo source** : Mixeli, avec [la vidéo citée dans la description](https://www.youtube.com/watch?v=Orj3598VrfU).

La provenance du morceau et ses crédits restent ceux de cette source. La mention des auteurs documente l’origine du fichier ; les droits de la musique demeurent ceux de leurs ayants droit respectifs.

## Extrait et raccord de boucle

La source décodée dure 283,8186667 s. L’extrait retenu commence à **55,664 s** et se termine à **167,0925625 s**, pour une boucle de **111,4285625 s**. Le choix du segment suit une répétition musicale mesurée d’environ 111,42857 s.

Sur les dernières **0,4285625 s**, correspondant au dernier temps du segment, un fondu cosinus relevé fait progressivement passer la fin vers le pré-roll de la source situé juste avant le début, à la même phase musicale. Le raccord conserve la durée complète du cycle et le tempo de la source. Le dernier échantillon de la boucle rejoint ainsi le début suivant selon la variation naturelle des échantillons adjacents de la source.

Le traitement retire la composante continue, ajuste uniformément le gain vers une crête de −1,2 dBFS, puis écrit un WAV PCM 16 bits. Les caractéristiques mesurées du fichier final sont :

| Propriété | Valeur |
| --- | --- |
| Format | WAV PCM 16 bits, stéréo, 48 000 Hz. |
| Durée | 111,4285625 s, soit 5 348 571 trames stéréo. |
| Crête | −1,2 dBFS. |
| RMS | −14,811 dBFS ; cette valeur n’est pas une mesure LUFS. |
| Échantillons écrêtés | 0. |
| Blocs de 100 ms sous −90 dBFS RMS | 0. |
| Erreur du saut de raccord par rapport au saut naturel de la source | 7,02 × 10⁻⁶, en amplitude normalisée. |
| Modification du tempo | Aucune. |

Empreinte SHA-256 du WAV final : `D49BF774F9AE47FF6612E265DCCDBFD0CCD6D6A932A3C567431A526139CED354`.

Le rapport `output/audio/AeroStadiumTitleTheme.json` consigne la source, son empreinte, les limites de l’extrait, le raccord, les mesures et l’empreinte du WAV final. Ces contrôles numériques décrivent le fichier préparé ; l’essai en jeu est indiqué séparément ci-dessous.

## Intégration dans Unity

Le fichier `Assets/AeroStadium/Resources/Audio/AeroStadiumTitleTheme.wav` est chargé par le chemin de ressource `Audio/AeroStadiumTitleTheme`. Le composant `TitleScreenAudio` joue la musique en boucle uniquement sur l’écran titre, avec un volume nominal de 0,68. Il monte progressivement sur 1,2 s, puis effectue un fondu de sortie de 0,9 s lorsque Start ouvre le menu principal ou qu’une transition fait entrer en combat. Les fondus et le calendrier des cris utilisent le temps non affecté par la vitesse du jeu.

Les cris utilisent une seconde source audio, au volume nominal de 0,24. Le premier arrive après 14 s ; Pikachu, Noctali puis Lucario se succèdent avec des intervalles de 24, 27 et 30 s. Un cri n’en chevauche pas un autre. Pendant chaque cri et pendant 0,35 s après sa fin, la musique baisse progressivement vers 78 % de son niveau nominal, puis revient à son niveau normal. Le départ de l’écran titre annule les prochains cris et fait décroître les deux sources avant leur arrêt.

L’import dans `ProjectBuild.ConfigureTitleAudio` utilise PCM, chargement anticipé, décompression au chargement et conservation de la fréquence d’échantillonnage. La musique reste stéréo et les cris sont importés en mono. Le chargeur de ressources fonctionne avec WAV ou OGG ; la préparation privilégie le WAV lorsqu’il est présent. L’AudioListener actif de la caméra est réutilisé, avec création d’un listener seulement si aucun n’est actif.

Les WAV préparés, les cris et les archives audio restent locaux et sont ignorés par Git (`Assets/AeroStadium/Resources/Audio/` et `output/audio/`). Les outils de préparation, le composant Unity et les documents peuvent être suivis dans le dépôt.

## Cris et provenance

Les trois cris correspondent à des Pokémon présents dans l’illustration de titre. Ils sont issus des jeux Pokémon et obtenus via le dépôt [PokéAPI/cries](https://github.com/PokeAPI/cries).

| Pokémon | OGG source | WAV local | Fréquence conservée |
| --- | --- | --- | --- |
| Pikachu, #25 | [latest/25.ogg](https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon/latest/25.ogg) | `Resources/Audio/Cries/pikachu.wav` | 48 000 Hz. |
| Noctali, #197 | [latest/197.ogg](https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon/latest/197.ogg) | `Resources/Audio/Cries/umbreon.wav` | 32 728 Hz. |
| Lucario, #448 | [latest/448.ogg](https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon/latest/448.ogg) | `Resources/Audio/Cries/lucario.wav` | 32 728 Hz. |

Les OGG ont été décodés en WAV mono avec leur fréquence d’origine, sans changement de hauteur ni de durée. La crête a été ramenée à −1,2 dBFS afin d’éviter les dépassements présents après décodage Vorbis. Le rapport `output/audio/cry-normalization.json` consigne les fréquences, durées et niveaux ; il indique zéro échantillon écrêté pour les trois WAV préparés. La [licence du dépôt](https://github.com/PokeAPI/cries/blob/main/LICENSE) indique que les contenus audio sont protégés par les droits de The Pokémon Company, tandis que le dépôt est distribué sous CC0. La copie consultée est conservée localement dans `output/audio/Cries-LICENSE.txt`.

## Préparation hors du jeu

`Tools/prepare_title_music.py` prépare la boucle à partir d’un fichier audio fourni localement. Il utilise [FFmpeg](https://ffmpeg.org/) pour décoder la source en stéréo 48 kHz et NumPy pour le raccord, le gain et les contrôles numériques. Le fichier de référence a été préparé avec FFmpeg 7.1. Ces outils interviennent avant l’import du WAV dans Unity.

```text
python Tools/prepare_title_music.py --input CHEMIN/source-audio.webm --ffmpeg CHEMIN/ffmpeg.exe --output output/audio/AeroStadiumTitleTheme.wav --start 55.664 --duration 111.4285714286 --crossfade 0.4285714286 --peak-dbfs -1.2 --source-url "https://www.youtube.com/watch?v=Joo7_AKDKi4" --source-title "Battle! L - Remix Cover (Pokémon Legends: Z-A)" --source-credit "Vetrom"
```

Les durées demandées sont arrondies à la trame la plus proche à 48 kHz ; les valeurs effectives sont celles du rapport JSON et du tableau ci-dessus. Le rapport conserve également l’empreinte SHA-256 du fichier source utilisé : `14192D2EE785329CFA0F756343BFB3665E6AEFD8F855469A53C54E2468F3140A`.

## Anciennes compositions conservées

Les deux essais originaux du projet restent archivés : « Au sommet de l’arène » (v1, 102,4 s) dans `output/audio/archive-v1/`, et « Le serment des champions » (v2, 128 s) dans `output/audio/archive-v2/`. Le MIDI de la v2 est `output/audio/archive-v2/source-composition.mid`. Le code de composition est conservé dans `Tools/compose_title_theme.py`.

Ces anciennes compositions avaient été rendues avec [FluidSynth 2.6.1](https://github.com/FluidSynth/fluidsynth/releases/tag/v2.6.1), sous [LGPL 2.1](https://github.com/FluidSynth/fluidsynth/blob/v2.6.1/LICENSE), et la banque [GeneralUser GS 2.0.3](https://github.com/mrbumpy409/GeneralUser-GS) de S. Christian Collins. Sa [licence](https://github.com/mrbumpy409/GeneralUser-GS/blob/main/documentation/LICENSE.txt) autorise la création musicale privée ou commerciale avec la banque et mentionne une incertitude sur la provenance de certains échantillons ; une copie est conservée localement dans `output/audio/GeneralUser-LICENSE.txt`. Ces crédits concernent les deux compositions archivées.

## Vérification de la piste active

Essai visible de 140 secondes réussi le 2 octobre 2026, après un build Windows sans erreur ni avertissement : 1 boucle complète, 5 cris, lecture vérifiée puis passage en combat et arrêt par fondu. Résultat : `errors=0`, `models=2`, `screen=Battle`, `titleAssetsVerified=True`, `musicPlaybackVerified=True`, `musicLoops=1`, `criesPlayed=5`, `musicPlaying=False`, `titleAudioStopped=True`, `passed=True`. Journal : `Logs/title-audio-youtube-20261002.log`.

L’observation a été activée avant le lancement puis arrêtée après fermeture. Une revue numérique indépendante du WAV confirme l’absence de silence de 100 ms même avec des fenêtres glissantes ; le raccord suit le mouvement naturel de la source à 0,23 LSB près, et le fondu local baisse de seulement 0,35–0,55 dB. Ces vérifications contrôlent les échantillons et les sources Unity, sans mesurer la sortie physique des enceintes.


Le mode explicite `--title-audio-test` dure 140 s par défaut. Il laisse passer la boucle de 111,4285625 s, puis entre en combat 2,5 s avant la fin du test pour contrôler le fondu et l’arrêt des sources. Il vérifie le chargement de la musique, la présence d’un listener actif, l’avancée des échantillons, au moins un retour au début de la boucle et l’arrêt des deux sources. Lorsque des cris sont disponibles, il contrôle également qu’au moins un a été déclenché. Les diagnostics consignent notamment `musicLoops`, `cryClips`, `criesPlayed`, `musicVolume` et `titleAudioStopped`.

```text
AeroStadium.exe --title-audio-test --species 6 --seed 42
```

Le `--title-test` existant conserve sa durée par défaut de 12 s et ses critères visuels ; il ajoute les diagnostics audio et reste sur le titre. Les diagnostics décrivent l’état des sources dans Unity et ne mesurent pas le son physique des enceintes, l’équilibre perçu ou l’appréciation musicale de l’utilisateur.

Les crédits audio sont également regroupés dans [CREDITS.md](CREDITS.md).

Retest visible de 185 s réussi le 2 octobre 2026 : une boucle complète, sept cris et 102 cycles de clignotement, puis passage en combat et arrêt par fondu. Résultat : errors=0, models=2, titleAudioStopped=True, passed=True. Journal : Logs/title-audio-185s-20261002.log. Observation arrêtée après fermeture.

Le menu principal utilise désormais une piste distincte et le composant `MainMenuAudio`. Les deux sources effectuent des fondus lors du passage titre ↔ menu et s’arrêtent au combat. Le WAV de titre n’a pas été remplacé. Voir [MENU_AUDIO.md](MENU_AUDIO.md).
