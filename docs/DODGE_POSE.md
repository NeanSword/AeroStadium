# Esquives — refonte expressive

## Proposition du 18 octobre 2026

- J’ai repris le geste : Tortank fait un pas latéral bas avec transfert de poids, bras décalés, tête en contre-mouvement et queue retardée. Bulbizarre utilise des appuis diagonaux ; Arcanin une esquive basse plus étirée. Les objectifs des pieds sont bornés à la portée réelle des membres ; ce n’est pas une simulation physique complète.
- Expressions ajoutées sur les contrôles natifs des yeux et de la mâchoire des trois modèles : attention, concentration, bref clignement pendant le passage, relâchement et retour. Atlas locaux inspectés ; portraits de Tortank vérifiés dans le rendu. Aucune texture nouvelle ni animation native remplacée.
- La caméra cadre le défenseur de trois quarts puis recule progressivement. Premier essai v1 échoué : recherche de tête sous le mauvais parent, recul trop court pour Bulbizarre ; corrigés en v2 avec accès direct au modèle et cadrage de sa position courante. Le contrôle de queue ne s’applique pas à Bulbizarre, qui n’en a pas.
- Revue v2 passée=True, erreurs=0, durée120.00s ; neuf cas et cinq interruptions. Résultats : 0/9/accuracy-miss: True; 0/1/accuracy-miss: True; 0/59/accuracy-miss: True; 1/9/accuracy-miss: True; 1/1/accuracy-miss: True; 1/59/accuracy-miss: True; 0/9/direct-hit: True; 0/9/protect: True; 0/9/non-hit-without-resolved-miss: True. Évitement avant arrivée, zéro croisement des triangles animés et restauration contrôlés dans les cas réussis. Preuves output/attacks/dodge-expressive-20261018-v2/verified-summary.json, portraits et vidéo locale. Les gros plans sont des diagnostics distincts de la caméra jouée. Les réussites techniques ne constituent pas une acceptation artistique définitive.
- Build WindowsReference réussi, réglages restaurés ; stable conservé. Observation avant/après les deux essais, jeux fermés. Deux séries archivées et décodées, puis514PNG numérotés redondants supprimés (~0,284Gio) ; portraits, images utiles, rapports et vidéos conservés. Les dégâts flottants restent retirés ; refonte des barres de vie différée. Autres espèces/attaques et Croc Feu restent à traiter ensuite.

## Historique : premier essai rejeté

# Esquives articulées — essai à reprendre

**Retour visuel du 18 octobre : essai trop rigide, rejeté.** Les réussites techniques ci-dessous concernent le timing, les collisions et la restauration ; la qualité du geste reste à refaire. Priorité aux appuis, transferts de poids, contre-mouvements et expressions faciales distincts selon le Pokémon. Aucune nouvelle expression faciale n’est encore intégrée.

Le raté de précision déclenche un geste anticipé : flexion, prise d’appui, impulsion latérale, réception puis retour en garde. La couche de pose articule le squelette natif et conserve l’animation courante. Tortank garde deux appuis et relève les bras ; Bulbizarre et Arcanin répartissent la poussée et la réception sur quatre pattes.

Le déplacement horizontal appartient au directeur d’action. Les pieds suivent des objectifs au sol et en l’air calculés avec les longueurs réelles des membres ; les objectifs hors de portée sont bornés. Le corps tourne et s’incline, la tête continue à regarder l’adversaire. Le geste reste une animation cinématique, pas une simulation physique complète des muscles ou des appuis.

L’ordre d’évaluation est natif80 → esquive90 → placement au sol95 → collision Plante100 → caméra150. Les modifications temporaires du squelette sont retirées avant chaque nouvelle évaluation native. Annulation, désactivation et remplacement restaurent la pose et le placement. Abri et une simple absence de dégât ne déclenchent pas d’esquive.

## Vérification du 18 octobre 2026

Revue de 120.00 s réussie, erreurs0. Six vrais ratés de Tranch’Herbe, sur les deux côtés, contre Tortank9, Bulbizarre1 et Arcanin59 : aucune réaction de dégâts, aucun dégât et aucun croisement entre les pointes des feuilles et les triangles animés. Le dégagement est mesuré 49 à 113 ms avant l’arrivée dans le volume initial immobile. Cette référence de timing est un volume de départ ; elle ne prétend pas calculer le contact exact d’un modèle hypothétique resté immobile.

Les témoins impact direct, Abri et absence d’événement raté passent. Cinq interruptions restaurent la couche articulée, les deux acteurs et la caméra. Les nombres flottants de dégâts sont retirés ; les barres de vie restent inchangées. Leur refonte est prévue pour une étape ultérieure.

Preuves locales : output/attacks/dodge-20261018-v1/verified-summary.json et runtime/demonstration.mp4. Vidéo muette, WAV séparé. Compilation expérimentale WindowsReference réussie et réglages restaurés. Cette revue ne valide pas les151espèces, les morphologies volantes/serpentines, toutes les arènes ni le timing de toutes les attaques. L’orthographique n’est pas couvert par cette nouvelle revue. Croc Feu424 reste à vérifier séparément.
