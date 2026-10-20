# Éco-Sphère412 — première version expérimentale

J’ai commencé une mise en scène originale pour Bulbizarre1 : charge flottante devant le corps, cœur vert, filaments courbes, feuilles et pollen, puis projectile détaché et éclatement. La source utilise les limites actuelles du corps et la direction du défenseur ; elle ne prétend pas correspondre à un os de bouche vérifié. La trajectoire reste fixée au départ.

Le point avant réellement dessiné est suivi en LateUpdate100 et interrogé contre les triangles animés de la cible. Aucun impact de remplacement par minuterie ou boîte englobante. Ce contrôle concerne le point avant, pas toute la sphère : une collision volumique complète reste à étudier. Abri reçoit un rebond du projectile d’énergie et une coupure de sa géométrie intérieure. Une seule réaction est demandée après contact ; un raté ne déclenche pas de réaction de dégâts. Ces comportements sont implémentés mais ne sont pas encore vérifiés en jeu.

Le son est une première composition PCM originale : montée d’énergie, souffle végétal et impulsion au contact réel. Il reste à écouter dans le mixage et à retravailler si nécessaire. Les sons, maillages et matériaux créés pour l’effet sont libérés à l’annulation ; la vitesse de l’animation est restaurée. L’attaque spéciale native est sélectionnée par rôle, sans index de clip supposé.

Compilation Windows expérimentale réussie le20octobre2026, réglages restaurés, version stable conservée. Preuves locales : output/ui/energy-ball-20261020-v1/build-process.json et compiled-sources.json. Aucun runtime lancé pour ce lot. Statut implemented_pending_validation, aucune validation artistique ou physique revendiquée. Première cible de revue : Bulbizarre1→Tortank9, des deux côtés.

## Référence et reprise

La référence visuelle récente officielle reste à inspecter : les recherches de cette séance ne suffisent pas à identifier précisément son animation dans Écarlate/Violet. Le document officiel https://assets.pokemon.com/assets/cms2/pdf/video-game/pokken/Gardevoir_Chart_EN.pdf confirme le nom Energy Ball dans Pokkén, sans prouver le rendu de la version récente. Aucun média officiel copié.

Prochaine séance : observer l’animation récente, créer une revue dédiée de120secondes avec de vrais résultats du moteur pour impact/Abri/raté, les deux côtés, interruption à chaque phase, remplacement, caméra orthographique et écoute du son. Vérifier l’expression de Bulbizarre, le rythme, le volume de la sphère, son absence de traversée, la réaction par efficacité et l’esquive avant arrivée. Ne pas utiliser la revue de Croc Feu pour certifier Éco-Sphère. Conserver le registre en attente jusqu’aux preuves ; aucune extrapolation aux151espèces.
