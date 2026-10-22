# Éco-Sphère412 — version expérimentale

J’ai préparé une charge végétale devant Bulbizarre1, un cœur vert parcouru de filaments, des feuilles et du pollen, puis une sphère détachée et un éclatement. L’origine flottante utilise les limites de repos transformées du modèle ; elle ne prétend pas suivre une bouche anatomique vérifiée. L’animation spéciale native est choisie par rôle, sans index de clip supposé. Le projectile suit la direction fixée à son départ, sans guidage sur la cible qui esquive.

## Reprise du21octobre2026

J’ai remplacé le contrôle du seul point avant par le balayage continu d’une enveloppe sphérique, de même rayon que le cœur dessiné, contre les triangles animés de la cible. Le calcul traite les faces, les arêtes et les sommets, les contacts initiaux et les sphères immobiles. Les boîtes ne servent qu’à rejeter des calculs : elles ne produisent aucun impact. Le mouvement du projectile est continu ; l’animation du défenseur est figée au cliché de la frame courante. Ce n’est donc pas un balayage continu du défenseur entre deux poses. Le maillage visuel est une approximation polygonale de la sphère ; les feuilles et filaments décoratifs ne sont pas des corps solides.

Abri fournit uniquement sa coque visible triangulée, sans son halo au sol. L’effet d’énergie peut rebondir sur cette coque et sa géométrie intérieure est masquée. L’accès ajouté à la coque ne modifie pas le comportement des autres attaques. Les autres effets conservent leur ancien contrôle de segment ; le nouveau balayage est optionnel.

La marge d’esquive de412 tient maintenant compte de la largeur du défenseur dans la direction de sortie et du rayon maximal de l’orbe. Le placement reste borné par la place disponible sur le terrain ; cela ne garantit pas à lui seul une esquive réussie. La progression de cette attaque pilote sa présentation, sans saut de temps ajouté au contact. Les rapports distinguent l’absence de géométrie et un vrai passage sans contact ; aucune réaction de dégâts n’est demandée pour une efficacité nulle.

Le cœur, les filaments et les feuilles ont trois couches distinctes. Le nouveau shader anime les nervures d’énergie et le relief lumineux du cœur ; il ne recolore pas un projectile d’eau. Le bruitage reste une composition PCM originale : charge, souffle et impulsion d’impact. Son enveloppe d’impact a été raccordée sans saut au passage de45ms. L’écoute et le mixage restent à vérifier. Les ressources temporaires et l’état du Pokémon propriétaire sont libérés ou restaurés à l’annulation, avec un rapport interrompu distinct.

Les diagnostics de contact sont des mesures techniques : erreur du rayon au point retourné et distance du sommet visible le plus proche. Ils ne certifient pas la qualité artistique ni une coïncidence parfaite des deux maillages. La preuve de contact en jeu reste à produire.

## État et références

Compilation Windows expérimentale réussie le21octobre2026, réglages restaurés et version stable conservée. Preuves locales : output/ui/energy-ball-20261021-v1/build-process.json et compiled-sources.json. Aucun test, aucun lancement du jeu et aucune écoute en situation pour ce lot, conformément à la consigne. Statut implemented_pending_validation. Première revue prévue : Bulbizarre1→Tortank9, des deux côtés ; aucune validation des151espèces.

La version du20octobre2026 utilisait seulement le point avant et le shader des effets Plante : les paragraphes ci-dessus décrivent son remplacement, sans attribuer rétroactivement une validation à cette version.

La référence visuelle récente exacte reste à inspecter. L’index de https://bulbapedia.bulbagarden.net/wiki/Energy_Ball_(move) identifie une capture Energy_Ball_IX.png d’Écarlate/Violet, mais ses pixels et sa séquence animée n’ont pas pu être observés pendant cette recherche. Ce n’est pas une vidéo publiée par l’éditeur. Le document officiel https://assets.pokemon.com/assets/cms2/pdf/video-game/pokken/Gardevoir_Chart_EN.pdf confirme Energy Ball dans Pokkén ; il ne prouve pas le rendu récent. Aucun média officiel copié et aucun timing récent revendiqué.

Reprise sur autorisation de tests : observer la référence récente, puis revue dédiée120s avec impact/Abri/raté réellement résolus par le moteur, deux côtés, immunité/efficacité, interruptions, remplacement, caméra orthographique, observation de l’écran et écoute. Vérifier le geste de Bulbizarre, l’origine, le volume visible, l’absence de traversée et l’esquive avant arrivée. Ne pas utiliser la revue de Croc Feu pour certifier412.


## 22 octobre 2026 — tests avec vidéo et son
J’ai repris les tests : passé=True, erreurs=0, durée=120.50s, géométrie19cas=True, son enregistré=True. Les douze cas, trois interruptions, remplacements et caméra orthographique sont détaillés dans output/attacks/energy-ball-20261022-v2/verified-summary.json. La vidéo complète eco-sphere-avec-son.mp4 contient le mix AudioListener réellement enregistré, aligné aux horodatages des images. V1 a révélé des erreurs de vérification : rejet à tort des trajectoires hors triangles, snapshot de vitesse pris pendant l’action et contrôle de nettoyage avant fin de réaction. V2 conserve les exigences et corrige ces moments de mesure. Portée Bulbizarre1→Tortank9 uniquement ; aucune validation des151espèces. La capture statique Écarlate/Violet Energy_Ball_IX.png a été observée sur Bulbagarden ; elle ne prouve pas le timing officiel.
