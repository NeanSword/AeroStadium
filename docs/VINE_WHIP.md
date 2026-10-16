# Fouet Lianes22

La présentation prépare une boucle plane complète, arme le geste vers l’arrière puis transmet la courbure à la pointe. Deux lianes natives se décalent de45ms ; le claquement arrive vers1,14s, suivi de recul et de rassemblement. Les2,5s de mouvement guidé sont mesurées, sans prétendre à une corde inextensible.

Le contact corporel utilise les triangles animés, évalués après Animator. Les snapshots de mesh/indices/vertices sont réutilisés ; aucun MeshCollider ni clone d’acteur n’est fabriqué. Les volumes englobants rejettent seulement les recherches inutiles. Un échec de contact réel reste un échec de validation. La pointe rejoint la surface avant de reculer, avec25ms de contact lisible ; une seule réaction de dégâts est jouée. Abri conserve sa surface dédiée et bloque les deux pointes sans dégâts.

La caméra prépare un trois-quarts source, laisse lire la trajectoire puis revient par interpolation. Les points de la courbe guident le cadrage, sans grande boîte vide imposant une vue aérienne. Les vérifications des obstacles, du corps entier, du sens de lecture et des interruptions demeurent.

WhipActionReview vérifie Bulbizarre→Tortank de chaque côté, contact/Abri, progression de la pointe, longueurs, restauration, cinq annulations et fallback orthographique. Les captures surface-closeup sont des diagnostics séparés de la caméra live. Les résultats précis et les éventuelles limites figurent dans LastProgressLogs.md. Cela ne valide pas automatiquement les autres espèces ni les autres attaques.
