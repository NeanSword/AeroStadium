# Musique du menu principal

Piste active choisie par lâ€™utilisateur : [PokÃ©mon : Main Title Intro [EPIC COVER] (Fan music)](https://www.youtube.com/watch?v=JckTGvghi0k), chaÃ®ne [Alexis DL](https://www.youtube.com/channel/UCi0rPiqZNemz3hV40NPePOA), publiÃ©e le 27 fÃ©vrier 2025. La description indique Junâ€™ichi Masuda comme compositeur et Alexis DL pour lâ€™orchestration et lâ€™arrangement. Ces crÃ©dits sont conservÃ©s tels que dÃ©clarÃ©s par la source.

La source dÃ©codÃ©e dure 97,8140625 secondes. Elle contient une entrÃ©e douce et une fin avec dÃ©croissance ; elle nâ€™est pas une rÃ©pÃ©tition exacte du morceau entier. Le segment retenu va de 15,26 Ã  91,14 secondes et conserve le passage final Ã©nergique. Les quatre derniÃ¨res secondes sont fondues vers les quatre secondes prÃ©cÃ©dant le dÃ©but retenu, avec compensation de puissance. Ce raccord est une adaptation musicale, sans changement de tempo ni de hauteur. Une corrÃ©lation harmonique aide Ã  choisir les phrases, mais ne prouve pas Ã  elle seule la qualitÃ© perceptive du raccord.

| PropriÃ©tÃ© | Valeur |
| --- | --- |
| DurÃ©e de la boucle | 75,88 s |
| Format | PCM16 stÃ©rÃ©o, 48 kHz |
| Trames | 3 642 240 |
| CrÃªte | âˆ’1,2 dBFS |
| RMS | âˆ’14,1613 dBFS |
| Ã‰chantillons Ã©crÃªtÃ©s | 0 |
| Blocs de 100 ms silencieux sous âˆ’90 dBFS | 0 |
| Erreur de raccord par rapport au saut naturel de la source | 0,925 LSB maximum |
| SHA-256 WAV | 73BA584C947AA1287B407D9A75700CE6FF9C7F2FC32189E2D3BD2EE4CA2DA70E |

Ressource : `Assets/AeroStadium/Resources/Audio/AeroStadiumMenuTheme.wav`, chargÃ©e par `MainMenuAudio`. Le GUID et les rÃ©glages dâ€™import prÃ©cÃ©dents sont conservÃ©s : PCM, DecompressOnLoad, preload et frÃ©quence source. La source est 2D, loop=true, volume0,68, fondus1,2/0,9s. La musique du titre reste celle de Vetrom.

Source, mÃ©tadonnÃ©es, crÃ©dits, sauvegarde prÃ©cÃ©dente, prÃ©paration et mesures : `output/audio/youtube-JckTGvghi0k/`. Lâ€™ancienne musique Epic PokeMix et sa documentation sont conservÃ©es dans le sous-dossier `before` et dans leur dossier source historique. Les WAV et sources restent locaux et ignorÃ©s par Git.

## Validation Unity

Première compilation interrompue par manque de mémoire dans CompileGameResourcesFolderDependencies (textures). Journal `Logs/menu-alexis-build-20261009.log`.

Relance réussie, sortie0 : `Logs/menu-alexis-low-memory-build-20261009.log`. L’exécutable expérimental `Builds/WindowsReference/AeroStadium.exe` inclut la nouvelle piste et les151 modèles natifs. Les151 anciens modèles de secours ont été déplacés temporairement hors de Resources pendant la compilation, puis restaurés ; ils ne sont pas inclus dans ce paquet expérimental. Pixels, résolution et animations natifs conservés. Le script local `output/audio/youtube-JckTGvghi0k/build_reference.ps1` vérifie les chemins et restaure les fichiers dans finally. Les réglages de début de session et la musique du titre sont vérifiés inchangés.

Essai prévu170s non validé : `Logs/menu-alexis-170s-20261009.log`, passage au combat avant toute boucle, menuMusicLoops0, absence de runtime-result, sortie0. Une sortie0 seule ne constitue pas une validation. Computer Use a ensuite signalé un arrêt utilisateur par Échap ; aucune nouvelle observation ni relance. La tentative d’observation immédiatement avant lancement a échoué (sky undefined), puis le helper a été réinitialisé et le menu observé pendant le test. Ne pas prétendre que l’observation était correctement active avant cette relance.

À reprendre : réactiver l’observation avant tout nouveau runtime et vérifier son résultat avant lancement ; refaire un essai complet avec deux répétitions. Si nécessaire, renforcer la protection contre la sélection anticipée uniquement dans menu-audio-test. La qualité perceptive du raccord reste à apprécier lors du test ; les mesures ne prétendent pas à une écoute des enceintes.
