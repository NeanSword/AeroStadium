# Caméra de Surf

Le directeur conserve la propriété de la caméra. Surf place une vue arrière surépaule relative au sens d’attaque pendant1,65s, suit le déplacement réel du Pokémon pendant l’effet de2,90s, puis revient au cadrage initial pendant1,60s. Position, visée et FOV sont interpolés ensemble ; aucune correction dure de distance ne s’ajoute au suivi. L’impact ne saute plus la progression de caméra.

La trajectoire courbe et plusieurs hauteurs sont vérifiées contre les obstacles. Si le plan arrière est indisponible, la vue d’ensemble est conservée. Une caméra orthographique peut refuser ce suivi sans empêcher l’animation de l’attaque. La durée de vie des animations est indépendante de l’éligibilité de la caméra : annulation et remplacement disposent les itérateurs actifs, restaurent les acteurs et rendent la caméra de décision.

SurfCameraReview couvre Tortank→Dracaufeu depuis les deux côtés, impacts directs et Abri, un obstacle temporaire, le refus de caméra orthographique et cinq instants d’interruption. Les métriques de visibilité concernent le centre de l’acteur et complètent l’inspection visuelle. Les autres attaques gardent leur cadrage actuel. Résultats exacts dans LastProgressLogs.md et les rapports locaux.
